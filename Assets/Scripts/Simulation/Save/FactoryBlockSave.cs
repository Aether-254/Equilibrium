using System;
using System.Collections.Generic;
using Equilibrium.Content;
using Equilibrium.Simulation.World;

namespace Equilibrium.Simulation.Save
{
    public sealed class BlockSaveRecords
    {
        public SaveRecord Block { get; }
        public SaveRecord Container { get; }
        public SaveRecord Mechanical { get; }
        internal BlockSaveRecords(SaveRecord block, SaveRecord container, SaveRecord mechanical)
        { Block = block; Container = container; Mechanical = mechanical; }
    }

    /// <summary>Persistent runtime handles are translated to canonical items and named slots at this boundary.</summary>
    public static class FactoryBlockSave
    {
        public static BlockSaveRecords Capture(PersistentBag bag, ContentRegistry registry, string id, ItemId blockItem)
        {
            var block = SaveFields.Copy(bag.SavedBlock); block.Id = id; block.ItemId = blockItem.Canonical; block.Count = 1;
            var container = SaveFields.Copy(bag.SavedContainer); container.Id = id;
            container.Extras["progress"] = (double)bag.Progress; container.Extras["energy"] = (double)bag.Energy;
            var slots = SaveFields.Section(container, "slots", true); var oldSlots = SaveFields.Index(slots.Records); slots.Records.Clear();
            foreach (var slot in bag.Slots)
            {
                string key = SlotId(slot.Container, slot.Key);
                oldSlots.TryGetValue(key, out var old); var saved = SaveFields.Copy(old);
                saved.Id = key; saved.ContainerKey = slot.Container; saved.SlotKey = slot.Key;
                saved.ItemId = slot.Item < 0 ? null : registry.Items.FromHandle(slot.Item).Canonical;
                saved.Count = slot.Count; saved.Extras["reserved"] = slot.Reserved;
                slots.Records.Add(saved);
            }
            SaveFields.SetSection(container, "slots", slots);
            var attributes = SaveFields.Section(container, "attributes", true);
            var oldAttributes = SaveFields.Index(attributes.Records); attributes.Records.Clear();
            foreach (var pair in bag.Extras)
            {
                if (pair.Key == "slots") continue; // Legacy in-memory encoding, never a second inventory source.
                oldAttributes.TryGetValue(pair.Key, out var old); var attribute = SaveFields.Copy(old);
                attribute.Id = pair.Key; attribute.Extras["value"] = pair.Value; attributes.Records.Add(attribute);
            }
            SaveFields.SetSection(container, "attributes", attributes);

            var mech = SaveFields.Copy(bag.SavedMachine); mech.Id = id;
            mech.Extras["fuelDebt"] = bag.FuelDebt; mech.Extras["hasMachine"] = bag.Machine != null;
            mech.Reservations.Clear();
            if (bag.Machine != null) CaptureMachine(bag, registry, mech);
            return new BlockSaveRecords(block, container, mech);
        }

        private static void CaptureMachine(PersistentBag bag, ContentRegistry registry, SaveRecord saved)
        {
            var machine = bag.Machine;
            saved.Extras["phase"] = (int)machine.Phase; saved.Extras["reason"] = (int)machine.Reason;
            saved.Extras["recipe"] = machine.RecipeId; saved.Extras["pinnedRecipe"] = machine.PinnedRecipe;
            saved.Extras["work"] = machine.WorkDone; saved.Extras["watts"] = machine.ReceivedWatts;
            saved.Extras["completed"] = machine.CompletedBatches;
            var history = new byte[60]; for (int i = 0; i < 60; i++) history[i] = machine.PoweredHistory[i] ? (byte)1 : (byte)0;
            saved.Extras["history"] = history; saved.Extras["historyIndex"] = machine.HistoryIndex;
            saved.Extras["observed"] = machine.Observed;
            var oldReservations = new Dictionary<string, SaveReservation>(StringComparer.Ordinal);
            if (bag.SavedMachine != null)
                foreach (var old in bag.SavedMachine.Reservations) oldReservations[old.Id] = old;
            var reservations = new Dictionary<string, SaveReservation>(StringComparer.Ordinal);
            foreach (var held in machine.Held)
            {
                var slot = bag.Slots[held.LocalSlot]; string key = SlotId(slot.Container, slot.Key);
                if (reservations.TryGetValue(key, out var existing)) { existing.Count = checked(existing.Count + held.Count); continue; }
                var reservation = SaveReservation.FromResolved(key, slot.Container, slot.Key, registry.Items.FromHandle(held.Item), held.Count);
                if (oldReservations.TryGetValue(key, out var old) && old.ItemId == reservation.ItemId)
                {
                    foreach (var pair in old.Extras) reservation.Extras.Add(pair);
                    reservation.InlineExtras.UnionWith(old.InlineExtras);
                }
                reservations.Add(key, reservation); saved.Reservations.Add(reservation);
            }
            var products = SaveFields.Section(saved, "products", true);
            var oldProducts = SaveFields.Index(products.Records); products.Records.Clear();
            for (int i = 0; i < machine.Pending.Count; i++)
            {
                var product = machine.Pending[i]; string id = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                oldProducts.TryGetValue(id, out var old);
                var record = SaveFields.Copy(old?.ItemId == product.Item.Canonical ? old : null);
                record.Id = id; record.ItemId = product.Item.Canonical; record.Count = product.Count;
                record.Extras["byproduct"] = product.Byproduct; products.Records.Add(record);
            }
            SaveFields.SetSection(saved, "products", products);
        }

        public static PersistentBag Restore(SaveRecord block, SaveRecord container, SaveRecord mechanical,
            BlockDefinition definition, ContentRegistry registry)
        {
            var bag = new PersistentBag { SavedBlock = block, SavedContainer = container, SavedMachine = mechanical,
                Progress = (float)SaveFields.Number(container, "progress", float.MaxValue),
                Energy = (float)SaveFields.Number(container, "energy", float.MaxValue),
                FuelDebt = SaveFields.Number(mechanical, "fuelDebt", 1) };
            if (bag.FuelDebt >= 1) throw SaveFields.Invalid("fuel debt");
            var slots = SaveFields.Index(SaveFields.Section(container, "slots").Records);
            var localByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var spec in definition.Containers)
                foreach (var slot in spec.Slots)
                {
                    string key = SlotId(spec.Key, slot.Key);
                    if (!slots.TryGetValue(key, out var record) || record.ContainerKey != spec.Key || record.SlotKey != slot.Key)
                        throw SaveFields.Invalid("slot binding missing: " + key);
                    slots.Remove(key);
                    int count = Count(record.Count), reserved = SaveFields.Int(record, "reserved", 0, count);
                    ItemId item = record.ItemId == null ? ItemId.None : registry.ResolveOrUnknown(record.ItemId).Id;
                    if (item.IsNone != (count == 0)) throw SaveFields.Invalid("empty slot identity");
                    if (!item.IsNone && !registry.Item(item).IsUnknown && count > Capacity.Effective(slot.Capacity, registry.MaxStackOf(item)))
                        throw SaveFields.Invalid("slot capacity");
                    localByKey.Add(key, bag.Slots.Count);
                    bag.Slots.Add(new PersistentSlot(spec.Key, slot.Key, item.IsNone ? -1 : item.Handle, count, reserved));
                    if (count > 0) bag.Items.Add((item.Handle, count));
                    if (reserved > 0) bag.Reservations.Add((item.Handle, reserved));
                }
            if (slots.Count != 0) throw SaveFields.Invalid("unrecognized slot binding");
            foreach (var attribute in SaveFields.Section(container, "attributes").Records)
            {
                if (attribute.Id == "slots") throw SaveFields.Invalid("legacy slot attributes");
                bag.Extras.Add(attribute.Id, SaveFields.Text(attribute, "value", true));
            }
            if (SaveFields.Bool(mechanical, "hasMachine")) RestoreMachine(bag, mechanical, localByKey, registry);
            else if (mechanical.Reservations.Count > 0) throw SaveFields.Invalid("reservation without machine");
            var heldBySlot = new int[bag.Slots.Count];
            if (bag.Machine != null)
                foreach (var held in bag.Machine.Held) heldBySlot[held.LocalSlot] = checked(heldBySlot[held.LocalSlot] + held.Count);
            for (int i = 0; i < heldBySlot.Length; i++)
                if (heldBySlot[i] != bag.Slots[i].Reserved) throw SaveFields.Invalid("reservation total");
            return bag;
        }

        private static void RestoreMachine(PersistentBag bag, SaveRecord saved, Dictionary<string, int> localByKey, ContentRegistry registry)
        {
            var machine = new MachineState {
                Phase = (MachinePhase)SaveFields.Int(saved, "phase", 0, 3),
                Reason = (ReasonCode)SaveFields.Int(saved, "reason", 0, int.MaxValue),
                RecipeId = SaveFields.Text(saved, "recipe", true), PinnedRecipe = SaveFields.Text(saved, "pinnedRecipe", true),
                WorkDone = SaveFields.Number(saved, "work"), ReceivedWatts = SaveFields.Number(saved, "watts"),
                CompletedBatches = SaveFields.Long(saved, "completed"), HistoryIndex = SaveFields.Int(saved, "historyIndex", 0, 59),
                Observed = SaveFields.Int(saved, "observed", 0, 60) };
            if (!Enum.IsDefined(typeof(ReasonCode), machine.Reason)) throw SaveFields.Invalid("machine reason");
            if (machine.RecipeId != null && !registry.TryGetRecipe(machine.RecipeId, out _)) throw SaveFields.Invalid("recipe unavailable");
            if (machine.PinnedRecipe != null && !registry.TryGetRecipe(machine.PinnedRecipe, out _)) throw SaveFields.Invalid("pinned recipe unavailable");
            if (machine.Phase == MachinePhase.Working && machine.RecipeId == null) throw SaveFields.Invalid("working recipe missing");
            var history = SaveFields.Bytes(saved, "history");
            if (history.Length != 60 || (machine.Observed < 60 && machine.HistoryIndex != machine.Observed)) throw SaveFields.Invalid("utilization history");
            for (int i = 0; i < 60; i++)
            {
                if (history[i] > 1 || (machine.Observed < 60 && i >= machine.Observed && history[i] != 0)) throw SaveFields.Invalid("utilization sample");
                machine.PoweredHistory[i] = history[i] != 0; if (history[i] != 0) machine.Powered++;
            }
            var reservedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var held in saved.Reservations)
            {
                if (held == null || held.ItemId == null || held.Count <= 0) throw SaveFields.Invalid("reservation");
                string key = SlotId(held.ContainerKey, held.SlotKey);
                if (!reservedKeys.Add(key) || !localByKey.TryGetValue(key, out int local)) throw SaveFields.Invalid("reservation slot");
                int count = Count(held.Count); var slot = bag.Slots[local];
                var item = registry.ResolveOrUnknown(held.ItemId).Id;
                if (slot.Item != item.Handle || count > slot.Count) throw SaveFields.Invalid("reservation content");
                machine.Held.Add(new MachineReservation(local, item.Handle, count));
            }
            foreach (var product in SaveFields.Section(saved, "products").Records)
            {
                if (product.ItemId == null || product.Count <= 0) throw SaveFields.Invalid("pending product");
                machine.Pending.Add(new MachineProduct(registry.ResolveOrUnknown(product.ItemId).Id,
                    Count(product.Count), SaveFields.Bool(product, "byproduct")));
            }
            if ((machine.Pending.Count > 0) != (machine.Phase == MachinePhase.Blocked)) throw SaveFields.Invalid("pending batch phase");
            bag.Machine = machine;
        }

        public static string SlotId(string container, string slot)
        {
            if (string.IsNullOrEmpty(container) || string.IsNullOrEmpty(slot)) throw SaveFields.Invalid("slot key");
            return container.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + container + slot;
        }
        public static int Count(long count)
        { if (count < 0 || count > int.MaxValue) throw SaveFields.Invalid("count out of range"); return (int)count; }
    }
}

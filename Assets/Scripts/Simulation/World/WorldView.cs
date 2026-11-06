using System;
using System.Collections.Generic;
using Equilibrium.Content;

namespace Equilibrium.Simulation.World
{
    /// <summary>
    /// Read-only surface over <see cref="WorldState"/> (ADR-0004 IWorldView). Every accessor is a
    /// pure read; the engine hands this to systems as the tick snapshot.
    /// </summary>
    public sealed class WorldView : IWorldView
    {
        internal readonly WorldState S;
        public WorldView(WorldState s) { S = s; }

        public int Width => S.Width;
        public int Height => S.Height;
        public int BlockIndexCount => S.Alive.Count;
        public AccessorConfiguration Accessor(BlockRef block, Side localSide)
            => IsAlive(block) ? S.Accessors[block.Index]?[(int)localSide] : null;
        public ContentRegistry Registry => S.Registry;
        public bool InBounds(CellRef c) => S.InBounds(c);

        public BlockRef BlockAt(CellRef c)
        {
            if (!S.InBounds(c)) return BlockRef.None;
            int b = S.CellToBlock[S.CellIndex(c)];
            return b < 0 ? BlockRef.None : new BlockRef(b);
        }

        public bool IsVoid(CellRef c) => !S.InBounds(c);
        public bool IsEmpty(CellRef c) => S.InBounds(c) && S.CellToBlock[S.CellIndex(c)] < 0;

        public BlockDefHandle DefOf(BlockRef b) => S.BlockDef[b.Index];
        public int OrientationOf(BlockRef b) => S.Orientation[b.Index];
        public CellRef AnchorOf(BlockRef b) => S.AnchorCell[b.Index];
        public CellRef ControllerOf(BlockRef b) => S.ControllerCoord[b.Index];
        public bool IsAlive(BlockRef b) => b.Index >= 0 && b.Index < S.Alive.Count && S.Alive[b.Index];
        public PersistentBag BagOf(BlockRef b) => S.Bags[b.Index];

        public bool IngressEnabled(CellRef c, Side side) => S.InBounds(c) && (S.Ingress[S.CellIndex(c)] & (1 << (int)side)) != 0;

        // ---- Ports ----
        public int PortCount(BlockRef b) => S.PortCount[b.Index];
        public PortDef Port(BlockRef b, int local) => S.Ports[S.PortStart[b.Index] + local];
        public Side WorldFacing(BlockRef b, int local) => Port(b, local).LocalFacing.Rotate(S.Orientation[b.Index]);

        /// <summary>Cell a port sits on (single-cell blocks: the anchor). Multiblock offsets are added later.</summary>
        public CellRef PortCell(BlockRef b, int local) => S.AnchorCell[b.Index];

        /// <summary>
        /// grid-world GDD connection predicate: adjacent, mutual facings, compatible kinds, ingress enabled.
        /// </summary>
        public bool Connected(BlockRef a, int outPort, out BlockRef target, out int inPort)
        {
            target = BlockRef.None; inPort = -1;
            if (!IsAlive(a)) return false;
            PortDef op = Port(a, outPort);
            if (!op.Kind.IsOutput()) return false;
            Side facing = op.LocalFacing.Rotate(S.Orientation[a.Index]);
            CellRef from = PortCell(a, outPort);
            CellRef to = from.Step(facing);
            if (!S.InBounds(to)) return false;
            if (!IngressEnabled(to, facing.Opposite())) return false;
            BlockRef tb = BlockAt(to);
            if (tb.IsNone) return false;
            int n = PortCount(tb);
            for (int i = 0; i < n; i++)
            {
                PortDef ip = Port(tb, i);
                if (ip.Kind.IsOutput()) continue;
                if (!PortKindExt.Compatible(op.Kind, ip.Kind)) continue;
                if (ip.LocalFacing.Rotate(S.Orientation[tb.Index]) != facing.Opposite()) continue;
                if (PortCell(tb, i).X != to.X || PortCell(tb, i).Y != to.Y) continue;
                target = tb; inPort = i;
                return true;
            }
            return false;
        }

        // ---- Slots ----
        public int SlotCount(BlockRef b) => S.SlotCount[b.Index];
        public int SlotGlobal(BlockRef b, int local) => S.SlotStart[b.Index] + local;
        public int SlotItemHandle(int g) => S.SlotItem[g];
        public int SlotCountOf(int g) => S.SlotCountArr[g];
        public int SlotCapacityOf(int g) => S.SlotCapacity[g];
        public int SlotReservedOf(int g) => S.SlotReserved[g];
        public string SlotKeyOf(int g) => S.SlotKey[g];
        public string ContainerKeyOf(int g) => S.ContainerKeyOfSlot[g];
        public int SlotFilterOf(int g) => S.SlotFilter[g];

        /// <summary>Resolves a port's bound container/slot range for block b. Returns false if binding is missing.</summary>
        public bool ResolveBinding(BlockRef b, in PortDef port, out int slotStart, out int slotEnd)
        {
            slotStart = slotEnd = -1;
            if (port.BindingMissing) return false;
            int s = S.SlotStart[b.Index], e = s + S.SlotCount[b.Index];
            int first = -1, last = -1;
            for (int g = s; g < e; g++)
            {
                if (!string.Equals(S.ContainerKeyOfSlot[g], port.ContainerKey, StringComparison.Ordinal)) continue;
                if (port.SlotKey != null && !string.Equals(S.SlotKey[g], port.SlotKey, StringComparison.Ordinal)) continue;
                if (first < 0) first = g;
                last = g;
            }
            if (first < 0) return false;
            slotStart = first; slotEnd = last + 1;
            return true;
        }

        public bool Filtered(int g, ItemId item)
        {
            int f = S.SlotFilter[g];
            return f < 0 || S.Filters[f].Accepts(S.Registry, item);
        }

        public int EffectiveCapacity(int g, ItemId item) => Capacity.Effective(S.SlotCapacity[g], S.Registry.MaxStackOf(item));
    }

    public static class Capacity
    {
        /// <summary>container GDD rule 3: effectiveCapacity = min(slot.capacity, itemDef.maxStack).</summary>
        public static int Effective(int slotCapacity, int maxStack) => Math.Min(slotCapacity, maxStack);
    }

    /// <summary>
    /// Persistent attributes that travel with a dropped block item (grid-world GDD rule 5). Typed small
    /// map; unknown keys are preserved in <see cref="Extras"/> for save-load.
    /// </summary>
    public sealed class PersistentBag
    {
        public readonly List<(int item, int count)> Items = new List<(int, int)>();
        public readonly List<(int item, int count)> Reservations = new List<(int, int)>();
        public float Progress { get; internal set; }
        public float Energy { get; internal set; }
        public MachineState Machine { get; internal set; }
        public double FuelDebt { get; internal set; }
        internal readonly List<PersistentSlot> Slots = new List<PersistentSlot>();
        internal Save.SaveRecord SavedBlock, SavedContainer, SavedMachine;
        public readonly Dictionary<string, string> Extras = new Dictionary<string, string>(StringComparer.Ordinal);

        public PersistentBag Clone()
        {
            var b = new PersistentBag { Progress = Progress, Energy = Energy, Machine = Machine?.Clone(), FuelDebt = FuelDebt };
            b.Items.AddRange(Items);
            b.Reservations.AddRange(Reservations);
            b.Slots.AddRange(Slots);
            b.SavedBlock = SavedBlock; b.SavedContainer = SavedContainer; b.SavedMachine = SavedMachine;
            foreach (var kv in Extras) b.Extras[kv.Key] = kv.Value;
            return b;
        }

        public bool IsEmpty => Items.Count == 0 && Reservations.Count == 0 && Progress == 0f && Energy == 0f && Extras.Count == 0 && Machine == null && FuelDebt == 0
            && SavedBlock == null && SavedContainer == null && SavedMachine == null;
    }

    internal readonly struct PersistentSlot
    {
        internal readonly string Container, Key;
        internal readonly int Item, Count, Reserved;
        internal PersistentSlot(string container, string key, int item, int count, int reserved)
        { Container = container; Key = key; Item = item; Count = count; Reserved = reserved; }
    }

    /// <summary>Removal result (grid-world GDD rule 6): the dropped item plus its persistent bag; never orientation.</summary>
    public readonly struct DropRecord
    {
        public readonly ItemId Item;
        public readonly PersistentBag Bag;
        public DropRecord(ItemId item, PersistentBag bag) { Item = item; Bag = bag; }
    }
}

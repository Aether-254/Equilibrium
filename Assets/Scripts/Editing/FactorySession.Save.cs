using System;
using System.Collections.Generic;
using System.Globalization;
using Equilibrium.Content;
using Equilibrium.Simulation;
using Equilibrium.Simulation.Save;
using Equilibrium.Simulation.World;

namespace Equilibrium.Editing
{
    public sealed partial class FactorySession
    {
        private SaveDocument _saveTemplate;
        private readonly Dictionary<string, SaveRecord> _stockTemplates = new Dictionary<string, SaveRecord>(StringComparer.Ordinal);

        public SaveDocument CaptureSave(DateTimeOffset wallTime)
        {
            if (!AtSaveBoundary) throw new InvalidOperationException("Save requires a committed tick boundary.");
            if (PendingCommands != 0) throw new InvalidOperationException("Save is waiting for queued edits to commit.");
            var document = _saveTemplate == null ? new SaveDocument() : SaveDocumentCodec.Deserialize(SaveDocumentCodec.Serialize(_saveTemplate));
            document.Header.TickIndex = Engine.TickIndex;
            document.Header.LastSaveWallTime = wallTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            document.Header.MarketBinding.Mode = MarketMode; document.Header.MarketBinding.ServerId = MarketServerId;
            document.World.Extras["factoryVersion"] = 1; document.World.Extras["width"] = World.Width; document.World.Extras["height"] = World.Height;
            document.World.Records.Clear(); document.Containers.Records.Clear(); document.Mech.Records.Clear();
            for (int i = 0; i < World.BlockIndexCount; i++)
            {
                var block = new BlockRef(i); if (!World.IsAlive(block)) continue;
                var cell = World.AnchorOf(block);
                var records = FactoryBlockSave.Capture(_writer.CaptureBag(block), Registry, CellId("block", cell),
                    Registry.Items.FromHandle(_definitions.Get(World.DefOf(block)).DropItemHandle));
                records.Block.Extras["kind"] = "block";
                WritePose(records.Block, cell, World.OrientationOf(block));
                var controller = World.ControllerOf(block);
                records.Block.Extras["controllerX"] = controller.IsNone ? -1 : controller.X;
                records.Block.Extras["controllerY"] = controller.IsNone ? -1 : controller.Y;
                int ingress = 0; for (int side = 0; side < 4; side++) if (World.IngressEnabled(cell, (Side)side)) ingress |= 1 << side;
                records.Block.Extras["ingress"] = ingress;
                var accessors = SaveFields.Section(records.Block, "accessors", true);
                var prior = SaveFields.Index(accessors.Records); accessors.Records.Clear();
                for (int side = 0; side < 4; side++)
                {
                    var config = World.Accessor(block, (Side)side); if (config == null) continue;
                    string id = side.ToString(CultureInfo.InvariantCulture); prior.TryGetValue(id, out var old);
                    var record = SaveFields.Copy(old); record.Id = id; record.ContainerKey = config.ContainerKey;
                    record.ItemId = config.Filter.IsNone ? null : config.Filter.Canonical;
                    record.Count = config.Amount; record.Extras["side"] = side;
                    record.Extras["flow"] = (int)config.Flow; record.Extras["quantity"] = (int)config.Quantity;
                    accessors.Records.Add(record);
                }
                SaveFields.SetSection(records.Block, "accessors", accessors);
                AddRecords(document, records);
            }
            for (int i = 0; i < _recovered.Count; i++)
            {
                var drop = _recovered[i]; var records = FactoryBlockSave.Capture(drop.Bag, Registry, "drop/" + i.ToString(CultureInfo.InvariantCulture), drop.Item);
                records.Block.Extras["kind"] = "drop";
                foreach (string key in new[] { "x", "y", "orientation", "controllerX", "controllerY", "ingress", "accessors" }) records.Block.Extras.Remove(key);
                AddRecords(document, records);
            }
            foreach (var ghost in _ghosts)
            {
                var record = SaveFields.Copy(ghost.SaveTemplate); record.Id = CellId("ghost", ghost.Cell);
                record.ItemId = _buildItems[ghost.Kind].Canonical; record.Count = 1; record.Extras["kind"] = "ghost";
                WritePose(record, ghost.Cell, ghost.Orientation); record.Extras["reason"] = (int)ghost.Reason;
                document.World.Records.Add(record);
            }
            foreach (var stock in _stock)
            {
                var item = Registry.Items.FromHandle(stock.Key); _stockTemplates.TryGetValue(item.Canonical, out var old);
                var record = SaveFields.Copy(old); record.Id = "warehouse/" + item.Canonical;
                record.ItemId = item.Canonical; record.Count = stock.Value; record.Extras["kind"] = "stock";
                document.Containers.Records.Add(record);
            }
            return document;
        }

        public static FactorySession FromSave(SaveDocument source, int maxWidth = 512, int maxHeight = 512)
        {
            // Detach before hydration so mutable inspection DTOs cannot modify a live session later.
            var document = SaveDocumentCodec.Deserialize(SaveDocumentCodec.Serialize(source));
            if (document.World.Version != 1 || document.Containers.Version != 1 || document.Mech.Version != 1 ||
                SaveFields.Int(document.World, "factoryVersion", 1, int.MaxValue) != 1) throw SaveFields.Invalid("factory schema version");
            int width = SaveFields.Int(document.World, "width", 1, Math.Min(512, maxWidth));
            int height = SaveFields.Int(document.World, "height", 1, Math.Min(512, maxHeight));
            var session = new FactorySession(width, height, null, document.Header.MarketBinding.Mode, document.Header.MarketBinding.ServerId);
            var states = SaveFields.Index(document.Containers.Records); var mechanical = SaveFields.Index(document.Mech.Records);
            SaveFields.Index(document.World.Records);
            var stockIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in document.Containers.Records)
            {
                if (SaveFields.Text(record, "kind") != "stock") continue;
                if (record.ItemId == null || !stockIds.Add(record.ItemId) || record.Reservations.Count != 0) throw SaveFields.Invalid("warehouse item");
                var item = session.Registry.ResolveOrUnknown(record.ItemId).Id;
                session.AddStock(item, FactoryBlockSave.Count(record.Count)); session._stockTemplates.Add(record.ItemId, record); states.Remove(record.Id);
            }
            foreach (var record in document.World.Records)
            {
                if (record.ItemId == null || record.Count != 1) throw SaveFields.Invalid("block item");
                Buildable kind = session.BuildKind(record.ItemId);
                string type = SaveFields.Text(record, "kind");
                if (type == "ghost")
                {
                    var cell = ReadCell(record, session.World); int orientation = SaveFields.Int(record, "orientation", 0, 3);
                    if (!session.CanPreview(cell)) throw SaveFields.Invalid("ghost overlap");
                    var reason = (ReasonCode)SaveFields.Int(record, "reason", 0, int.MaxValue);
                    if (!Enum.IsDefined(typeof(ReasonCode), reason)) throw SaveFields.Invalid("ghost reason");
                    var ghost = new ConstructionGhost(cell, kind, orientation) { Reason = reason, SaveTemplate = record };
                    session._ghosts.Add(ghost); session._reserved.Add(cell, ghost); continue;
                }
                if (type != "block" && type != "drop") throw SaveFields.Invalid("unknown factory record");
                if (!states.TryGetValue(record.Id, out var container) || !mechanical.TryGetValue(record.Id, out var mech) ||
                    SaveFields.Text(container, "kind") != "state") throw SaveFields.Invalid("block state missing");
                states.Remove(record.Id); mechanical.Remove(record.Id);
                var definition = session._definitions.Get(session._buildDefs[kind]);
                var bag = FactoryBlockSave.Restore(record, container, mech, definition, session.Registry);
                if (bag.Machine != null && kind != Buildable.Saw) throw SaveFields.Invalid("machine state on unsupported block");
                if (bag.FuelDebt != 0 && kind != Buildable.Engine) throw SaveFields.Invalid("fuel state on unsupported block");
                if (type == "drop")
                {
                    if (kind == Buildable.Belt) throw SaveFields.Invalid("recovered belt contents");
                    session._recovered.Add(new DropRecord(session._buildItems[kind], bag)); continue;
                }
                var position = ReadCell(record, session.World);
                if (session.IsReserved(position)) throw SaveFields.Invalid("block overlaps ghost");
                var result = session._writer.Place(session._buildDefs[kind], position, SaveFields.Int(record, "orientation", 0, 3), bag, out var block);
                if (result != ReasonCode.None) throw SaveFields.Invalid("block placement: " + result);
                int cx = SaveFields.Int(record, "controllerX", -1, width - 1), cy = SaveFields.Int(record, "controllerY", -1, height - 1);
                if ((cx == -1) != (cy == -1)) throw SaveFields.Invalid("controller coordinate");
                session._writer.SetController(block, cx == -1 ? CellRef.None : new CellRef(cx, cy));
                int ingress = SaveFields.Int(record, "ingress", 0, 15);
                if (kind != Buildable.Belt && ingress != 15) throw SaveFields.Invalid("blocker on unsupported block");
                for (int side = 0; side < 4; side++) session._writer.SetIngress(position, (Side)side, (ingress & (1 << side)) != 0);
                var sides = new HashSet<int>();
                foreach (var accessor in SaveFields.Section(record, "accessors").Records)
                {
                    int side = SaveFields.Int(accessor, "side", 0, 3);
                    if (kind != Buildable.Belt || !sides.Add(side) || accessor.Count < 1 || accessor.Count > 3000 || string.IsNullOrEmpty(accessor.ContainerKey))
                        throw SaveFields.Invalid("accessor");
                    ItemId? filter = accessor.ItemId == null ? (ItemId?)null : session.Registry.ResolveOrUnknown(accessor.ItemId).Id;
                    session._writer.ConfigureAccessor(block, (Side)side, new AccessorConfiguration(
                        (AccessorFlow)SaveFields.Int(accessor, "flow", 0, 2), (AccessorQuantity)SaveFields.Int(accessor, "quantity", 0, 2),
                        (int)accessor.Count, filter, accessor.ContainerKey));
                }
            }
            if (states.Count != 0 || mechanical.Count != 0) throw SaveFields.Invalid("orphan block state");
            var recoveredCounts = new Dictionary<int, int>();
            foreach (var drop in session._recovered)
            {
                recoveredCounts.TryGetValue(drop.Item.Handle, out int count); recoveredCounts[drop.Item.Handle] = count + 1;
                if (count + 1 > session.Stock(drop.Item)) throw SaveFields.Invalid("recovered items exceed warehouse stock");
            }
            session.Engine.RestoreTickIndex(document.Header.TickIndex); session._saveTemplate = document;
            return session;
        }

        private Buildable BuildKind(string id)
        {
            foreach (var pair in _buildItems) if (pair.Value.Canonical == id) return pair.Key;
            throw SaveFields.Invalid("block definition unavailable: " + id);
        }
        private static CellRef ReadCell(SaveRecord record, WorldView world)
            => new CellRef(SaveFields.Int(record, "x", 0, world.Width - 1), SaveFields.Int(record, "y", 0, world.Height - 1));
        private static string CellId(string kind, CellRef cell)
            => kind + "/" + cell.X.ToString(CultureInfo.InvariantCulture) + "/" + cell.Y.ToString(CultureInfo.InvariantCulture);
        private static void WritePose(SaveRecord record, CellRef cell, int orientation)
        { record.Extras["x"] = cell.X; record.Extras["y"] = cell.Y; record.Extras["orientation"] = orientation; }
        private static void AddRecords(SaveDocument document, BlockSaveRecords records)
        {
            records.Container.Extras["kind"] = "state";
            document.World.Records.Add(records.Block); document.Containers.Records.Add(records.Container); document.Mech.Records.Add(records.Mechanical);
        }
    }
}

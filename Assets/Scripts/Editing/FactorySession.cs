using System;
using System.Collections.Generic;
using Equilibrium.Content;
using Equilibrium.Simulation;
using Equilibrium.Simulation.World;
using Equilibrium.Simulation.Save;

namespace Equilibrium.Editing
{
    public enum Buildable : byte { Chest, Belt, Saw, Engine, Shaft }

    public sealed class ConstructionGhost
    {
        public CellRef Cell { get; }
        public Buildable Kind { get; }
        public int Orientation { get; }
        public ReasonCode Reason { get; internal set; }
        internal SaveRecord SaveTemplate;
        internal ConstructionGhost(CellRef cell, Buildable kind, int orientation)
        { Cell = cell; Kind = kind; Orientation = orientation & 3; }
    }

    /// <summary>
    /// One live factory and its local construction stock. Input queues intents; the edit batch
    /// commits after logistics, so edits take effect for connectivity on the following tick.
    /// No Unity dependency, market backend, free per-click materials or frame-time world writes.
    /// </summary>
    public sealed partial class FactorySession : ITickSystem, IWorldWriter
    {
        private enum Edit { Build, Remove, Accessor, Blocker, Deposit, Collect, CancelMachine }
        private sealed class Command
        {
            public Edit Kind;
            public CellRef[] Cells;
            public Buildable Buildable;
            public int Orientation;
            public BlockRef Expected = BlockRef.None;
            public Side Side;
            public AccessorConfiguration Configuration;
            public ItemId Item;
            public int Amount;
        }
        private readonly List<Command> _queued = new List<Command>(), _commands = new List<Command>();
        private readonly List<ConstructionGhost> _ghosts = new List<ConstructionGhost>();
        private readonly Dictionary<CellRef, ConstructionGhost> _reserved = new Dictionary<CellRef, ConstructionGhost>();
        private readonly Dictionary<int, int> _stock = new Dictionary<int, int>();
        private readonly List<DropRecord> _recovered = new List<DropRecord>();
        private readonly WorldWriter _writer;
        private readonly BlockDefTable _definitions;
        private readonly BlockDefHandle _belt, _chest;
        private readonly Dictionary<Buildable, BlockDefHandle> _buildDefs = new Dictionary<Buildable, BlockDefHandle>();
        private readonly Dictionary<Buildable, ItemId> _buildItems = new Dictionary<Buildable, ItemId>();
        private readonly ItemId _beltItem, _chestItem, _accessorItem, _blockerItem;
        private TransactionId _batch;
        private int _systemIndex;
        public WorldView World => _writer.View;
        public ContentRegistry Registry { get; }
        public TickEngine Engine { get; }
        public LogisticsSystem Logistics { get; }
        public ProcessingSystem Processing { get; }
        public IReadOnlyList<ConstructionGhost> Ghosts => _ghosts.AsReadOnly();
        public IReadOnlyList<DropRecord> RecoveredBlocks => _recovered.AsReadOnly();
        public ReasonCode LastEditReason { get; private set; }
        public int PendingCommands => _queued.Count;
        public int Revision { get; private set; }
        public bool AtSaveBoundary => Engine.Phase == TickPhase.Idle || Engine.Phase == TickPhase.Emit;
        public string MarketMode { get; }
        public string MarketServerId { get; }

        public FactorySession(int width = 64, int height = 64, IDictionary<string, int> startingStock = null,
            string marketMode = "LocalNpc", string marketServerId = null)
        {
            if (marketMode != "LocalNpc" && marketMode != "RemoteServer") throw SaveFields.Invalid("market mode");
            if (marketMode == "RemoteServer" ? string.IsNullOrWhiteSpace(marketServerId) : marketServerId != null)
                throw SaveFields.Invalid("market server identity");
            MarketMode = marketMode; MarketServerId = marketServerId;
            Registry = new ContentRegistry();
            if (new ContentLoader().Load(Registry, Tier1Content.Build(), Array.Empty<ContentPack>()).BaseRejected)
                throw new InvalidOperationException("Tier 1 content failed validation.");
            _definitions = new BlockDefTable();
            _beltItem = Registry.Items.Parse("belt"); _chestItem = Registry.Items.Parse("chest");
            _accessorItem = Registry.Items.Parse("accessor"); _blockerItem = Registry.Items.Parse("blocker");
            _belt = _definitions.Register(BlockDefinition.Belt("belt", _beltItem.Handle));
            _chest = _definitions.Register(BlockDefinition.Chest("chest", _chestItem.Handle, 4, 200));
            _buildDefs[Buildable.Chest] = _chest; _buildItems[Buildable.Chest] = _chestItem;
            _buildDefs[Buildable.Belt] = _belt; _buildItems[Buildable.Belt] = _beltItem;
            var saw = Registry.Items.Parse("saw"); var engine = Registry.Items.Parse("engine"); var shaft = Registry.Items.Parse("shaft");
            _buildDefs[Buildable.Saw] = _definitions.Register(BlockDefinition.Machine("saw", saw.Handle, 64, 64));
            _buildDefs[Buildable.Engine] = _definitions.Register(BlockDefinition.FuelEngine("engine", engine.Handle));
            _buildDefs[Buildable.Shaft] = _definitions.Register(BlockDefinition.Shaft("shaft", shaft.Handle));
            _buildItems[Buildable.Saw] = saw; _buildItems[Buildable.Engine] = engine; _buildItems[Buildable.Shaft] = shaft;
            _writer = new WorldWriter(new WorldState(width, height, Registry), _definitions);
            Processing = new ProcessingSystem(_writer, _definitions);
            Logistics = new LogisticsSystem(_writer, _definitions, Processing);
            Engine = new TickEngine(World, this);
            Logistics.Attach(Engine);
            Processing.Attach(Engine, Logistics);
            Engine.Register(this); // Logistics commits first; all building/configuration is next-tick input.
            if (startingStock != null)
                foreach (var entry in startingStock)
                {
                    var item = Registry.Items.Parse(entry.Key);
                    if (entry.Value < 0) throw new ArgumentOutOfRangeException(nameof(startingStock));
                    AddStock(item, entry.Value);
                }
        }

        public BlockKind Kind(BlockRef block) => _definitions.Get(World.DefOf(block)).Kind;
        public string BlockId(BlockRef block) => _definitions.Get(World.DefOf(block)).Id;
        public void ApplyMigrationCredential(string credential) => throw new NotSupportedException("Market migration verification is not implemented.");
        public int Stock(ItemId item) => _stock.TryGetValue(item.Handle, out int n) ? n : 0;
        public int Stock(string item) => Stock(Registry.Items.Parse(item));
        public bool IsReserved(CellRef cell) => _reserved.ContainsKey(cell);
        public bool CanPreview(CellRef cell) => World.InBounds(cell) && World.IsEmpty(cell) && !IsReserved(cell);

        public void QueueBuild(Buildable kind, IReadOnlyList<CellRef> cells, int orientation)
        {
            if (cells == null || cells.Count == 0) return;
            var copy = new CellRef[cells.Count]; for (int i = 0; i < copy.Length; i++) copy[i] = cells[i];
            _queued.Add(new Command { Kind = Edit.Build, Buildable = kind, Cells = copy, Orientation = orientation & 3 });
        }
        public void QueueRemove(CellRef cell) => _queued.Add(Target(Edit.Remove, cell));
        public void QueueAccessor(CellRef cell, Side localSide, AccessorConfiguration config)
        { var c = Target(Edit.Accessor, cell); c.Side = localSide; c.Configuration = config; _queued.Add(c); }
        public void QueueBlocker(CellRef cell, Side worldSide)
        { var c = Target(Edit.Blocker, cell); c.Side = worldSide; _queued.Add(c); }
        public void QueueDeposit(CellRef cell, ItemId item, int amount = 16)
        { var c = Target(Edit.Deposit, cell); c.Item = item; c.Amount = Math.Max(1, Math.Min(3000, amount)); _queued.Add(c); }
        public void QueueCollect(CellRef cell) => _queued.Add(Target(Edit.Collect, cell));
        public void QueueCancelMachine(CellRef cell) => _queued.Add(Target(Edit.CancelMachine, cell));
        private Command Target(Edit kind, CellRef cell)
            => new Command { Kind = kind, Cells = new[] { cell }, Expected = World.BlockAt(cell) };

        public void Collect(in TickSnapshot snapshot, TransactionBuffer buffer, int systemIndex)
        {
            _commands.Clear(); _commands.AddRange(_queued); _queued.Clear(); _systemIndex = systemIndex;
            _batch = TransactionId.None;
            if (_commands.Count == 0 && _ghosts.Count == 0) return;
            var tx = Transaction.Create(EntityRef.None, EntityRef.None, new Payload(PayloadKind.FactoryEditBatch), validator: 0);
            _batch = buffer.Add(in tx, systemIndex);
            buffer.AddDependency(_batch, Processing.CurrentBatch);
        }
        public ReasonCode Validate(in TickSnapshot snapshot, in Transaction tx)
            => tx.Id.Equals(_batch) ? ReasonCode.None : ReasonCode.ValidationFailed;

        public void Apply(in Transaction transaction)
        {
            if (transaction.Payload.Kind != PayloadKind.FactoryEditBatch) { _writer.Apply(in transaction); return; }
            if (!transaction.Id.Equals(_batch) || transaction.SystemIndex != _systemIndex)
                throw new InvalidOperationException("Unexpected construction transaction.");
            LastEditReason = ReasonCode.None;
            foreach (var command in _commands)
            {
                ReasonCode reason = Execute(command);
                if (reason != ReasonCode.None) LastEditReason = reason;
            }
            BuildGhosts(); Revision++;
        }

        private ReasonCode Execute(Command c)
        {
            if (c.Kind == Edit.Build) return ReserveGhosts(c);
            CellRef cell = c.Cells[0];
            if (c.Kind == Edit.Remove && c.Expected.IsNone && _reserved.TryGetValue(cell, out var ghost))
            { _reserved.Remove(cell); _ghosts.Remove(ghost); return ReasonCode.None; }
            var b = World.BlockAt(cell);
            if (b.IsNone || !b.Equals(c.Expected)) return ReasonCode.TargetInvalid;
            switch (c.Kind)
            {
                case Edit.Remove: return Remove(b);
                case Edit.CancelMachine: return _writer.CancelMachine(b);
                case Edit.Accessor:
                    if (Kind(b) != BlockKind.Conveyor || (uint)c.Side > 3) return ReasonCode.TargetInvalid;
                    var old = World.Accessor(b, c.Side);
                    if (old == null && c.Configuration != null && Stock(_accessorItem) < 1) return ReasonCode.SourceEmpty;
                    _writer.ConfigureAccessor(b, c.Side, c.Configuration);
                    if (old == null && c.Configuration != null) AddStock(_accessorItem, -1);
                    if (old != null && c.Configuration == null) AddStock(_accessorItem, 1);
                    return ReasonCode.None;
                case Edit.Blocker:
                    if (Kind(b) != BlockKind.Conveyor || (uint)c.Side > 3) return ReasonCode.TargetInvalid;
                    bool open = World.IngressEnabled(cell, c.Side);
                    if (open && Stock(_blockerItem) < 1) return ReasonCode.SourceEmpty;
                    _writer.SetIngress(cell, c.Side, !open); AddStock(_blockerItem, open ? -1 : 1);
                    return ReasonCode.None;
                case Edit.Deposit:
                    if (Kind(b) != BlockKind.Container && Kind(b) != BlockKind.Machine) return ReasonCode.TargetInvalid;
                    int start, inputEnd;
                    if (Kind(b) == BlockKind.Container) { start = World.SlotGlobal(b, 0); inputEnd = start + World.SlotCount(b); }
                    else
                    {
                        int portIndex = -1;
                        for (int p = 0; p < World.PortCount(b); p++) if (World.Port(b, p).Kind == PortKind.ItemIn) { portIndex = p; break; }
                        if (portIndex < 0) return ReasonCode.NoReceivingPort;
                        var port = World.Port(b, portIndex);
                        var admission = Processing.Check(World, b, portIndex, c.Item, true);
                        if (admission != ReasonCode.None) return admission;
                        if (!World.ResolveBinding(b, in port, out start, out inputEnd)) return ReasonCode.PortBindingMissing;
                    }
                    int available = Math.Min(c.Amount, Stock(c.Item));
                    if (available <= 0) return ReasonCode.SourceEmpty;
                    int placed = _writer.Insert(start, inputEnd, c.Item, available);
                    AddStock(c.Item, -placed); return placed > 0 ? ReasonCode.None : ReasonCode.TargetFull;
                case Edit.Collect:
                    if (Kind(b) != BlockKind.Container && Kind(b) != BlockKind.Machine) return ReasonCode.TargetInvalid;
                    for (int s = World.SlotGlobal(b, 0), end = s + World.SlotCount(b); s < end; s++)
                    {
                        if (Kind(b) == BlockKind.Machine && World.ContainerKeyOf(s) != "output" && World.ContainerKeyOf(s) != "byproduct") continue;
                        int handle = World.SlotItemHandle(s); if (handle < 0) continue;
                        var item = Registry.Items.FromHandle(handle);
                        int take = _writer.Extract(s, s + 1, item, int.MaxValue); AddStock(item, take);
                    }
                    return ReasonCode.None;
                default: return ReasonCode.ValidationFailed;
            }
        }

        private ReasonCode ReserveGhosts(Command c)
        {
            if (!_buildDefs.ContainsKey(c.Buildable)) return ReasonCode.ValidationFailed;
            if (c.Buildable != Buildable.Belt && c.Cells.Length != 1) return ReasonCode.ValidationFailed;
            if (c.Buildable == Buildable.Belt && c.Cells.Length < 2) return ReasonCode.SegmentTooShort;
            var seen = new HashSet<CellRef>();
            Side direction = Side.East.Rotate(c.Orientation);
            for (int i = 0; i < c.Cells.Length; i++)
            {
                var cell = c.Cells[i];
                if (!World.InBounds(cell)) return ReasonCode.OutOfBounds;
                if (!CanPreview(cell) || !seen.Add(cell)) return ReasonCode.CellOccupied;
                if (i > 0)
                {
                    CellRef prev = c.Cells[i - 1];
                    if (!prev.Step(direction).Equals(cell) && !prev.Step(direction.Opposite()).Equals(cell)) return ReasonCode.ValidationFailed;
                    if (i > 1 && (cell.X - prev.X != prev.X - c.Cells[i - 2].X || cell.Y - prev.Y != prev.Y - c.Cells[i - 2].Y))
                        return ReasonCode.ValidationFailed;
                }
            }
            foreach (var cell in c.Cells)
            {
                var ghost = new ConstructionGhost(cell, c.Buildable, c.Orientation);
                _ghosts.Add(ghost); _reserved.Add(cell, ghost);
            }
            return ReasonCode.None;
        }

        private void BuildGhosts()
        {
            for (int i = 0; i < _ghosts.Count;)
            {
                var ghost = _ghosts[i]; var item = _buildItems[ghost.Kind];
                if (!World.IsEmpty(ghost.Cell)) { ghost.Reason = ReasonCode.CellOccupied; i++; continue; }
                if (Stock(item) == 0) { ghost.Reason = ReasonCode.SourceEmpty; i++; continue; }
                int restored = _recovered.FindIndex(d => d.Item.Equals(item));
                var bag = restored >= 0 ? _recovered[restored].Bag : null;
                var reason = _writer.Place(_buildDefs[ghost.Kind], ghost.Cell, ghost.Orientation, bag, out _);
                if (reason != ReasonCode.None) { ghost.Reason = reason; i++; continue; }
                AddStock(item, -1); if (restored >= 0) _recovered.RemoveAt(restored);
                _reserved.Remove(ghost.Cell); _ghosts.RemoveAt(i);
            }
        }

        private ReasonCode Remove(BlockRef block)
        {
            CellRef cell = World.AnchorOf(block); BlockKind kind = Kind(block);
            int accessors = 0, blockers = 0;
            for (int side = 0; side < 4; side++)
            {
                if (World.Accessor(block, (Side)side) != null) accessors++;
                if (!World.IngressEnabled(cell, (Side)side)) blockers++;
            }
            var reason = _writer.Remove(block, out var drop);
            if (reason != ReasonCode.None) return reason;
            AddStock(drop.Item, 1); AddStock(_accessorItem, accessors); AddStock(_blockerItem, blockers);
            if (kind == BlockKind.Conveyor)
            {
                foreach (var held in drop.Bag.Items) AddStock(Registry.Items.FromHandle(held.item), held.count);
            }
            else if (!drop.Bag.IsEmpty) _recovered.Add(drop);
            return ReasonCode.None;
        }

        private void AddStock(ItemId item, int delta)
        {
            int value = checked(Stock(item) + delta);
            if (value < 0) throw new InvalidOperationException("Construction stock underflow.");
            _stock[item.Handle] = value;
        }
    }
}

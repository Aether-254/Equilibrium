using System;
using System.Collections.Generic;
using System.Linq;
using Equilibrium.Content;

namespace Equilibrium.Simulation.World
{
    /// <summary>
    /// Stages processing on the logistics batch's planned slots, then commits after logistics.
    /// All changes (fuel, reservations, progress, pending products) are prepared before Commit.
    /// New reservations start work next tick; new products cannot leave their output this tick.
    /// </summary>
    public sealed class ProcessingSystem : ITickSystem, ITickBatchPlanner, ILogisticsPortPolicy
    {
        private readonly WorldView _world;
        private readonly BlockDefTable _defs;
        private LogisticsSystem _logistics;
        private ulong _revision, _tick;
        private int _systemIndex;
        private bool _planned;
        internal int[] Items, Counts, Reserved;
        internal readonly Dictionary<int, PersistentBag> Bags = new Dictionary<int, PersistentBag>();
        private MechanicalResult[] _power = Array.Empty<MechanicalResult>(), _publishedPower = Array.Empty<MechanicalResult>();
        private LogisticsTransfer[] _pendingTransfers = Array.Empty<LogisticsTransfer>(), _transfers = Array.Empty<LogisticsTransfer>();
        public TransactionId CurrentBatch { get; private set; } = TransactionId.None;
        public ReadOnlySpan<LogisticsTransfer> Transfers => _transfers;
        public MechanicalResult Power(BlockRef b) => _world.IsAlive(b) && b.Index < _publishedPower.Length ? _publishedPower[b.Index] : default;
        public MachineState State(BlockRef b) => _world.IsAlive(b) ? _world.BagOf(b).Machine : null;

        public ProcessingSystem(WorldWriter writer, BlockDefTable definitions)
        { _world = writer.View; _defs = definitions; writer.BindProcessing(this); }

        public void Attach(TickEngine engine, LogisticsSystem logistics)
        { _logistics = logistics; engine.Register(this); }

        internal ItemDef Item(BlockRef b)
        {
            int handle = _defs.Get(_world.DefOf(b)).DropItemHandle;
            return handle >= 0 && _world.Registry.TryGetItem(_world.Registry.Items.FromHandle(handle), out var item) ? item : null;
        }
        internal bool IsEngine(BlockRef b) => Item(b)?.Extras.ContainsKey("omega_src") == true;
        internal bool IsShaft(BlockRef b) => Item(b)?.Extras.ContainsKey("eta_shaft") == true;
        internal bool TryMachine(BlockRef b, out MachineDef machine)
        {
            machine = null; var item = Item(b);
            return item?.BlockDef != null && !IsEngine(b) && _world.Registry.TryGetMachine(item.BlockDef, out machine);
        }

        public ReasonCode Check(WorldView world, BlockRef block, int port, ItemId item, bool receiving)
        {
            if (!receiving) return ReasonCode.None;
            if (IsEngine(block)) return world.Registry.Item(item).EnergyJ > 0 ? ReasonCode.None : ReasonCode.FilterMismatch;
            if (!TryMachine(block, out var machine)) return ReasonCode.None;
            var state = State(block);
            if (state?.Phase == MachinePhase.Working && world.Registry.TryGetRecipe(state.RecipeId, out var active) && !active.AcceptWhileWorking)
                return ReasonCode.MachineNotAccepting;
            foreach (var recipe in world.Registry.RecipesFor(machine))
            {
                if (state?.PinnedRecipe != null && state.PinnedRecipe != recipe.Id) continue;
                foreach (var input in recipe.Inputs) if (RecipeInputPlanner.Matches(world.Registry, input, item.Handle)) return ReasonCode.None;
            }
            return ReasonCode.FilterMismatch;
        }

        public void Collect(in TickSnapshot snapshot, TransactionBuffer buffer, int systemIndex)
        {
            if (!ReferenceEquals(snapshot.World, _world)) throw new InvalidOperationException("Processing uses a different world.");
            _revision = _world.S.MutationRevision; _tick = snapshot.TickIndex; _systemIndex = systemIndex; _planned = false;
            var tx = Transaction.Create(EntityRef.None, EntityRef.None, new Payload(PayloadKind.ProcessingBatch), validator: 0);
            CurrentBatch = buffer.Add(in tx, systemIndex); buffer.AddDependency(CurrentBatch, _logistics.CurrentBatch);
        }
        public ReasonCode Validate(in TickSnapshot snapshot, in Transaction transaction)
            => snapshot.TickIndex == _tick && transaction.Id.Equals(CurrentBatch) ? ReasonCode.None : ReasonCode.ValidationFailed;

        public void Plan(in TickSnapshot snapshot, TransactionBuffer buffer, ITieBreakSource random)
        {
            if (!buffer.Get(CurrentBatch).Accepted || !buffer.Get(_logistics.CurrentBatch).Accepted) return;
            Items = (int[])_logistics.Slots.Items.Clone(); Counts = (int[])_logistics.Slots.Counts.Clone();
            Reserved = _world.S.SlotReserved.ToArray(); Bags.Clear(); _power = new MechanicalResult[_world.BlockIndexCount];
            var machines = new List<(BlockRef block, ulong key)>();
            for (int i = 0; i < _world.BlockIndexCount; i++)
            {
                var b = new BlockRef(i); if (!_world.IsAlive(b)) continue;
                if (TryMachine(b, out _)) { Bags[i] = _world.BagOf(b).Clone(); machines.Add((b, random.NextKey())); }
                else if (IsEngine(b)) Bags[i] = _world.BagOf(b).Clone();
            }
            StraightMechanicalSolver.Solve(this, _world, _power);
            var transfers = new List<LogisticsTransfer>();
            machines.Sort((a, b) => a.key != b.key ? a.key.CompareTo(b.key) : a.block.Index.CompareTo(b.block.Index));
            foreach (var entry in machines) PushOutputs(entry.block, transfers);
            foreach (var entry in machines) Advance(entry.block, random);
            _pendingTransfers = transfers.ToArray(); _planned = true;
        }

        private void Advance(BlockRef b, ITieBreakSource random)
        {
            var bag = Bags[b.Index]; var state = bag.Machine ?? (bag.Machine = new MachineState());
            state.ReceivedWatts = 0;
            bool working = state.Phase == MachinePhase.Working;
            var power = _power[b.Index];
            state.Observe(working && power.Watts > 0);
            if (state.Phase == MachinePhase.Blocked)
            {
                if (InsertProducts(b, state.Pending)) { state.Pending.Clear(); state.Phase = MachinePhase.Idle; state.Reason = ReasonCode.None; }
                else state.Reason = ReasonCode.OutputFull;
                return;
            }
            if (working)
            {
                if (!_world.Registry.TryGetRecipe(state.RecipeId, out var recipe) || !ReservationIntact(b, state))
                {
                    Release(b, state); state.Phase = MachinePhase.Starved; state.Reason = ReasonCode.ReservationBroken;
                    state.WorkDone = 0; bag.Progress = 0; return;
                }
                state.ReceivedWatts = power.Watts; state.Reason = power.Reason;
                state.WorkDone += power.Watts; bag.Progress = (float)state.WorkDone;
                if (state.WorkDone + 1e-9 < recipe.WorkRequired) return;
                foreach (var held in state.Held)
                {
                    int g = _world.SlotGlobal(b, held.LocalSlot);
                    Reserved[g] -= held.Count; Counts[g] -= held.Count; if (Counts[g] == 0) Items[g] = -1;
                }
                state.Held.Clear(); state.CompletedBatches++;
                state.WorkDone = Math.Min(recipe.WorkRequired, Math.Max(0, state.WorkDone - recipe.WorkRequired));
                bag.Progress = (float)state.WorkDone;
                foreach (var product in recipe.Outputs) state.Pending.Add(new MachineProduct(product.Item, product.Count, false));
                foreach (var product in recipe.Byproducts)
                    if (product.Probability >= 1 || (product.Probability > 0 && (random.NextKey() >> 11) / 9007199254740992.0 < product.Probability))
                        state.Pending.Add(new MachineProduct(product.Item, product.Count, true));
                if (InsertProducts(b, state.Pending)) { state.Pending.Clear(); state.Phase = MachinePhase.Idle; state.Reason = ReasonCode.None; }
                else { state.Phase = MachinePhase.Blocked; state.Reason = ReasonCode.OutputFull; }
                return; // At most one completion per tick, including zero-work recipes.
            }

            TryMachine(b, out var definition); bool hasRecipe = false;
            foreach (var recipe in _world.Registry.RecipesFor(definition))
            {
                if (state.PinnedRecipe != null && state.PinnedRecipe != recipe.Id) continue;
                hasRecipe = true;
                var plan = RecipeInputPlanner.Find(_world, b, recipe, Items, Counts, Reserved);
                if (plan == null) continue;
                // Surplus belongs to the previous recipe, never to a newly selected recipe.
                if (state.RecipeId != recipe.Id) state.WorkDone = 0;
                state.RecipeId = recipe.Id; state.Held = plan;
                foreach (var held in plan) Reserved[_world.SlotGlobal(b, held.LocalSlot)] += held.Count;
                state.Phase = MachinePhase.Working; state.Reason = ReasonCode.None; return;
            }
            bool selected = state.PinnedRecipe != null || state.RecipeId != null;
            for (int i = 0; i < _world.SlotCount(b); i++)
            { int g = _world.SlotGlobal(b, i); if (_world.ContainerKeyOf(g) == "input" && Counts[g] > 0) selected = true; }
            state.Phase = hasRecipe && selected ? MachinePhase.Starved : MachinePhase.Idle;
            state.Reason = !hasRecipe ? ReasonCode.NoRecipe : selected ? ReasonCode.MissingInput : ReasonCode.NoInput;
        }

        private bool ReservationIntact(BlockRef b, MachineState state)
        {
            foreach (var held in state.Held)
            {
                if (held.LocalSlot < 0 || held.LocalSlot >= _world.SlotCount(b)) return false;
                int g = _world.SlotGlobal(b, held.LocalSlot);
                if (Items[g] != held.Item || Counts[g] < held.Count || Reserved[g] < held.Count) return false;
            }
            return true;
        }
        private void Release(BlockRef b, MachineState state)
        {
            foreach (var held in state.Held)
                if (held.LocalSlot >= 0 && held.LocalSlot < _world.SlotCount(b))
                {
                    int g = _world.SlotGlobal(b, held.LocalSlot);
                    if (Items[g] == held.Item) Reserved[g] = Math.Max(0, Reserved[g] - held.Count);
                }
            state.Held.Clear();
        }

        private bool InsertProducts(BlockRef b, List<MachineProduct> products)
        {
            int start = _world.SlotGlobal(b, 0), length = _world.SlotCount(b);
            var oldItems = new int[length]; var oldCounts = new int[length];
            Array.Copy(Items, start, oldItems, 0, length); Array.Copy(Counts, start, oldCounts, 0, length);
            foreach (var product in products)
            {
                int left = product.Count; string container = product.Byproduct ? "byproduct" : "output";
                for (int pass = 0; pass < 2; pass++)
                    for (int g = start; g < start + length && left > 0; g++)
                    {
                        if (_world.ContainerKeyOf(g) != container || !_world.Filtered(g, product.Item) || Items[g] != (pass == 0 ? product.Item.Handle : -1)) continue;
                        int amount = Math.Min(left, Math.Max(0, _world.EffectiveCapacity(g, product.Item) - Counts[g]));
                        if (amount > 0) { Items[g] = product.Item.Handle; Counts[g] += amount; left -= amount; }
                    }
                if (left == 0) continue;
                Array.Copy(oldItems, 0, Items, start, length); Array.Copy(oldCounts, 0, Counts, start, length); return false;
            }
            return true;
        }

        private void PushOutputs(BlockRef b, List<LogisticsTransfer> transfers)
        {
            for (int p = 0; p < _world.PortCount(b); p++)
            {
                var port = _world.Port(b, p);
                if ((port.Kind != PortKind.ItemOut && port.Kind != PortKind.ByproductOut) || !port.AcceptsDirectBelt ||
                    !_world.Connected(b, p, out var target, out _) || _defs.Get(_world.DefOf(target)).Kind != BlockKind.Conveyor ||
                    !_world.ResolveBinding(b, in port, out int start, out int end)) continue;
                int destination = _world.SlotGlobal(target, 0); if (Counts[destination] != 0) continue;
                // Push only a group that was present at the tick snapshot, never this tick's products.
                for (int g = start; g < end; g++)
                {
                    if (Items[g] < 0 || Items[g] != _world.SlotItemHandle(g)) continue;
                    int count = Math.Min(Counts[g] - Reserved[g], _world.SlotCountOf(g) - _world.SlotReservedOf(g));
                    if (count <= 0) continue;
                    var item = _world.Registry.Items.FromHandle(Items[g]);
                    if (!_world.Filtered(destination, item) || _world.EffectiveCapacity(destination, item) < count) break;
                    Counts[g] -= count; if (Counts[g] == 0) Items[g] = -1;
                    Counts[destination] = count; Items[destination] = item.Handle;
                    transfers.Add(new LogisticsTransfer(b, target, item, count, TransferKind.DirectPort)); break;
                }
            }
        }

        internal bool CanCommit(in Transaction tx, ulong revision)
            => _planned && tx.Id.Equals(CurrentBatch) && tx.SystemIndex == _systemIndex && revision == _revision + 1;
        internal void DidCommit() { _planned = false; _publishedPower = _power; _transfers = _pendingTransfers; }
    }
}

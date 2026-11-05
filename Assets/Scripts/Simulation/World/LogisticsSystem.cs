using System;
using System.Collections.Generic;
using Equilibrium.Content;

namespace Equilibrium.Simulation.World
{
    /// <summary>
    /// One atomic logistics batch per tick. Collect captures stock; Resolve stages container delivery,
    /// whole-group dependency chains, then accessor intake using this tick's stall results. Commit
    /// swaps slot values through WorldWriter. Per-transfer results are published only after success.
    /// </summary>
    public sealed class LogisticsSystem : ITickSystem, ITickBatchPlanner, ITickListener
    {
        private readonly WorldView _view;
        private readonly BlockDefTable _defs;
        private readonly ILogisticsPortPolicy _policy;
        internal readonly LogisticsSlots Slots = new LogisticsSlots();
        private TransactionId _batch;
        private int _systemIndex;
        private ulong _revision, _tick;
        private bool _planned, _committed;
        private int[] _slot = Array.Empty<int>(), _original = Array.Empty<int>(), _item = Array.Empty<int>();
        private int[] _out = Array.Empty<int>(), _next = Array.Empty<int>(), _winner = Array.Empty<int>();
        private byte[] _visit = Array.Empty<byte>();
        private bool[] _feedClaim = Array.Empty<bool>();
        private ulong[] _targetTie = Array.Empty<ulong>();
        private ReasonCode[] _reason = Array.Empty<ReasonCode>();
        private readonly List<int> _belts = new List<int>(), _path = new List<int>();
        private readonly List<Request> _deposits = new List<Request>(), _withdrawals = new List<Request>();
        private readonly List<LogisticsTransfer> _pending = new List<LogisticsTransfer>();
        private LogisticsTransfer[] _transfers = Array.Empty<LogisticsTransfer>();
        private int _transferCount;
        private int _capturedBlockCount;
        private BeltTickResult[] _results = Array.Empty<BeltTickResult>();
        private AccessorTickResult[] _accessorPending = Array.Empty<AccessorTickResult>(), _accessorResults = Array.Empty<AccessorTickResult>();
        public ulong LastCommittedTick { get; private set; }
        public bool LastBatchSucceeded { get; private set; }
        /// <summary>Valid after Collect, for batches that must commit after logistics this tick.</summary>
        public TransactionId CurrentBatch => _batch;
        public ReadOnlySpan<LogisticsTransfer> Transfers => new ReadOnlySpan<LogisticsTransfer>(_transfers, 0, _transferCount);

        private struct Request
        {
            public int Belt, Side, Target, Port;
            public SlotRange Range;
            public AccessorConfiguration Config;
            public AccessorFlow Flow;
            public ulong Tie;
            public int Priority => Config == null ? 0 : 1;
        }

        public LogisticsSystem(WorldWriter writer, BlockDefTable definitions, ILogisticsPortPolicy policy = null)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            _view = writer.View; _defs = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _policy = policy; writer.BindLogistics(this);
        }

        /// <summary>Registers collection and committed-result publication together.</summary>
        public void Attach(TickEngine engine) { engine.Register(this); engine.AddListener(this); }
        public BeltTickResult BeltResult(BlockRef block) => _view.IsAlive(block) && block.Index < _results.Length ? _results[block.Index] : default;
        public AccessorTickResult AccessorResult(BlockRef block, Side side)
            => _view.IsAlive(block) && (uint)side < 4 && block.Index * 4 + (int)side < _accessorResults.Length
                ? _accessorResults[block.Index * 4 + (int)side] : default;

        public void Collect(in TickSnapshot snapshot, TransactionBuffer buffer, int systemIndex)
        {
            if (!ReferenceEquals(snapshot.World, _view)) throw new InvalidOperationException("Logistics uses a different world.");
            _systemIndex = systemIndex; _tick = snapshot.TickIndex; _revision = _view.S.MutationRevision;
            _planned = _committed = false; _pending.Clear(); _deposits.Clear(); _withdrawals.Clear(); _belts.Clear();
            Slots.Capture(_view);
            int n = _view.BlockIndexCount;
            _capturedBlockCount = n;
            Ensure(n);
            Array.Fill(_slot, -1); Array.Fill(_next, -1); Array.Fill(_winner, -1);
            Array.Clear(_original, 0, n); Array.Clear(_out, 0, n); Array.Clear(_reason, 0, n);
            Array.Clear(_visit, 0, n); Array.Clear(_accessorPending, 0, n * 4);
            Array.Clear(_feedClaim, 0, n);
            for (int b = 0; b < n; b++)
            {
                var block = new BlockRef(b);
                if (!_view.IsAlive(block) || Kind(block) != BlockKind.Conveyor) continue;
                int s = _view.SlotGlobal(block, 0);
                _belts.Add(b); _slot[b] = s; _original[b] = Slots.Counts[s]; _item[b] = Slots.Items[s];
            }
            var transaction = Transaction.Create(EntityRef.None, EntityRef.None,
                new Payload(PayloadKind.LogisticsBatch), validator: 0);
            _batch = buffer.Add(in transaction, systemIndex);
        }

        public ReasonCode Validate(in TickSnapshot snapshot, in Transaction tx)
            => snapshot.TickIndex == _tick && tx.Id.Equals(_batch) ? ReasonCode.None : ReasonCode.ValidationFailed;

        public void Plan(in TickSnapshot snapshot, TransactionBuffer buffer, ITieBreakSource tieBreak)
        {
            if (!buffer.Get(_batch).Accepted) return;
            foreach (int b in _belts) CollectRequests(b, tieBreak);
            _deposits.Sort(CompareRequests);
            foreach (var request in _deposits) Deposit(request);
            foreach (var request in _withdrawals) ClaimFeed(request);
            PlanBelts(tieBreak);
            _withdrawals.Sort(CompareRequests);
            foreach (var request in _withdrawals) Withdraw(request);
            _planned = true;
        }

        private static int CompareRequests(Request a, Request b)
        {
            int p = b.Priority.CompareTo(a.Priority);
            if (p != 0) return p;
            int c = a.Tie.CompareTo(b.Tie);
            return c != 0 ? c : (a.Belt * 4 + a.Side).CompareTo(b.Belt * 4 + b.Side);
        }

        private BlockKind Kind(BlockRef block) => _defs.Get(_view.DefOf(block)).Kind;
        private Side Direction(int b) => Side.East.Rotate(_view.OrientationOf(new BlockRef(b)));

        private void CollectRequests(int b, ITieBreakSource tie)
        {
            var belt = new BlockRef(b);
            for (int side = 0; side < 4; side++)
            {
                var config = _view.Accessor(belt, (Side)side);
                if (config == null) continue;
                Side worldSide = ((Side)side).Rotate(_view.OrientationOf(belt));
                var target = _view.BlockAt(_view.AnchorOf(belt).Step(worldSide));
                AccessorFlow flow = config.Flow;
                if (flow == AccessorFlow.Auto)
                    flow = worldSide == Direction(b) || HasUpstream(b) ? AccessorFlow.BeltToContainer : AccessorFlow.ContainerToBelt;
                var request = new Request { Belt = b, Side = side, Config = config, Flow = flow, Target = target.Index, Port = -1, Tie = tie.NextKey() };
                ReasonCode reason = ResolveAccessorTarget(request, worldSide, out request.Range, out request.Port);
                if (reason != ReasonCode.None)
                {
                    SetAccessor(request, 0, reason);
                    if (flow == AccessorFlow.BeltToContainer && _original[b] > 0) _reason[b] = reason;
                    continue;
                }
                if (flow == AccessorFlow.BeltToContainer) _deposits.Add(request);
                else _withdrawals.Add(request);
            }

            if (_original[b] == 0) return;
            if (!_view.Connected(belt, 1, out var forward, out int port))
            {
                if (_reason[b] == ReasonCode.None) _reason[b] = ReasonCode.NoReceivingPort;
                return;
            }
            if (Kind(forward) == BlockKind.Conveyor) { _next[b] = forward.Index; return; }
            var input = _view.Port(forward, port);
            if (!input.AcceptsDirectBelt) { _reason[b] = ReasonCode.NoReceivingPort; return; }
            if (!_view.ResolveBinding(forward, in input, out int start, out int end))
            { _reason[b] = ReasonCode.PortBindingMissing; return; }
            _deposits.Add(new Request { Belt = b, Side = -1, Target = forward.Index, Port = port,
                Range = new SlotRange(start, end), Flow = AccessorFlow.BeltToContainer, Tie = tie.NextKey() });
        }

        private bool HasUpstream(int b)
        {
            CellRef at = _view.AnchorOf(new BlockRef(b));
            for (int s = 0; s < 4; s++)
            {
                var adjacent = _view.BlockAt(at.Step((Side)s));
                if (adjacent.IsNone || Kind(adjacent) != BlockKind.Conveyor) continue;
                if (_view.Connected(adjacent, 1, out var target, out _) && target.Index == b) return true;
            }
            return false;
        }

        private ReasonCode ResolveAccessorTarget(Request r, Side worldSide, out SlotRange range, out int portIndex)
        {
            range = default; portIndex = -1;
            var target = new BlockRef(r.Target);
            if (!_view.IsAlive(target) || Kind(target) == BlockKind.Conveyor) return ReasonCode.TargetInvalid;
            bool deposit = r.Flow == AccessorFlow.BeltToContainer;
            var ingressCell = _view.AnchorOf(deposit ? target : new BlockRef(r.Belt));
            if (!_view.IngressEnabled(ingressCell, deposit ? worldSide.Opposite() : worldSide)) return ReasonCode.NoReceivingPort;
            if (Kind(target) == BlockKind.Container)
            {
                var binding = new PortDef(PortKind.ItemIn, worldSide.Opposite(), r.Config.ContainerKey, null, false);
                if (!_view.ResolveBinding(target, in binding, out int start, out int end)) return ReasonCode.PortBindingMissing;
                range = new SlotRange(start, end); return ReasonCode.None;
            }
            // A machine must expose the correct side and direction; accessors never bypass ports.
            for (int p = 0; p < _view.PortCount(target); p++)
            {
                var port = _view.Port(target, p);
                bool compatible = deposit ? port.Kind == PortKind.ItemIn : port.Kind == PortKind.ItemOut || port.Kind == PortKind.ByproductOut;
                if (!compatible || _view.WorldFacing(target, p) != worldSide.Opposite()) continue;
                if (!_view.ResolveBinding(target, in port, out int start, out int end)) return ReasonCode.PortBindingMissing;
                portIndex = p; range = new SlotRange(start, end); return ReasonCode.None;
            }
            return ReasonCode.NoReceivingPort;
        }

        private void Deposit(Request r)
        {
            int b = r.Belt;
            if (_original[b] == 0) { SetAccessor(r, 0, ReasonCode.SourceEmpty); return; }
            if (_out[b] != 0) { SetAccessor(r, 0, ReasonCode.Contended); return; }
            var item = _view.Registry.Items.FromHandle(_item[b]);
            ReasonCode reason = ReasonCode.None;
            if (r.Config != null && !r.Config.Filter.IsNone && !r.Config.Filter.Equals(item)) reason = ReasonCode.FilterMismatch;
            else if (Slots.Available(new SlotRange(_slot[b], _slot[b] + 1), item) < _original[b]) reason = ReasonCode.SourceEmpty;
            else if (!Slots.Allows(r.Range, item)) reason = ReasonCode.FilterMismatch;
            else if (_policy != null && r.Port >= 0) reason = _policy.Check(_view, new BlockRef(r.Target), r.Port, item, true);
            int count = reason == ReasonCode.None ? Slots.Insert(r.Range, item, _original[b], r.Config == null) : 0;
            if (count > 0)
            {
                Slots.Take(new SlotRange(_slot[b], _slot[b] + 1), item, count);
                _out[b] = count;
                _pending.Add(new LogisticsTransfer(new BlockRef(b), new BlockRef(r.Target), item, count,
                    r.Config == null ? TransferKind.DirectPort : TransferKind.AccessorDeposit));
            }
            if (reason == ReasonCode.None && count < _original[b]) reason = ReasonCode.DownstreamCapacity;
            _reason[b] = reason; SetAccessor(r, count, reason);
        }

        private void PlanBelts(ITieBreakSource tie)
        {
            // A target has exactly one winning incoming group, regardless of ItemId or item count.
            // Accessor intake has priority +1; failed/conflicted claims are retried next tick, like
            // the generic resolver's rejected dependents, rather than rerolling within this tick.
            foreach (int b in _belts)
            {
                if (_original[b] == 0 || _out[b] > 0 || _next[b] < 0) continue;
                int target = _next[b];
                if (_feedClaim[target]) { _reason[b] = ReasonCode.Contended; continue; }
                var item = _view.Registry.Items.FromHandle(_item[b]);
                if (_view.SlotReservedOf(_slot[b]) > 0) { _reason[b] = ReasonCode.SourceEmpty; continue; }
                if (!_view.Filtered(_slot[target], item)) { _reason[b] = ReasonCode.FilterMismatch; continue; }
                if (_view.EffectiveCapacity(_slot[target], item) < _original[b]) { _reason[b] = ReasonCode.DownstreamCapacity; continue; }
                ulong key = tie.NextKey();
                if (_winner[target] < 0 || key < _targetTie[target])
                { _winner[target] = b; _targetTie[target] = key; }
            }
            foreach (int b in _belts)
                if (_original[b] > 0 && _out[b] == 0 && _next[b] >= 0 && _winner[_next[b]] != b && _reason[b] == ReasonCode.None)
                    _reason[b] = ReasonCode.Contended;
            foreach (int b in _belts) ResolveChain(b);

            // Remove every accepted original group before inserting any incoming group.
            foreach (int b in _belts)
            {
                if (_original[b] == 0 || _out[b] > 0 || _visit[b] != 2) continue;
                var item = _view.Registry.Items.FromHandle(_item[b]);
                Slots.Take(new SlotRange(_slot[b], _slot[b] + 1), item, _original[b]);
            }
            foreach (int b in _belts)
            {
                if (_original[b] == 0 || _out[b] > 0 || _visit[b] != 2) continue;
                int target = _next[b];
                var item = _view.Registry.Items.FromHandle(_item[b]);
                Slots.PutGroup(_slot[target], item, _original[b]);
                _out[b] = _original[b]; _reason[b] = ReasonCode.None;
                _pending.Add(new LogisticsTransfer(new BlockRef(b), new BlockRef(target), item, _original[b], TransferKind.Belt));
            }
        }

        private void ClaimFeed(Request r)
        {
            var item = r.Config.Filter.IsNone ? Slots.FirstAvailable(r.Range) : r.Config.Filter;
            if (item.IsNone || !_view.Filtered(_slot[r.Belt], item)) return;
            int available = Slots.Available(r.Range, item);
            if (available <= 0) return;
            if (_policy != null && r.Port >= 0 && _policy.Check(_view, new BlockRef(r.Target), r.Port, item, false) != ReasonCode.None) return;
            int capacity = _view.EffectiveCapacity(_slot[r.Belt], item);
            if (capacity <= 0) return;
            if (r.Config.Quantity == AccessorQuantity.Exact && (available < r.Config.Amount || capacity < r.Config.Amount)) return;
            if (r.Config.Quantity == AccessorQuantity.Keep && Slots.OriginalStock(r.Range, item) <= r.Config.Amount) return;
            _feedClaim[r.Belt] = true;
        }

        private void ResolveChain(int start)
        {
            if (_visit[start] != 0) return;
            _path.Clear();
            int b = start;
            bool success;
            while (true)
            {
                if (_original[b] == 0 || _out[b] == _original[b]) { success = true; break; }
                if (_visit[b] == 2) { success = true; break; }
                if (_visit[b] == 3) { success = false; break; }
                if (_visit[b] == 1) { _reason[b] = ReasonCode.TransactionCycle; success = false; break; }
                if (_out[b] > 0 || _next[b] < 0 || _winner[_next[b]] != b)
                { success = false; break; }
                _visit[b] = 1; _path.Add(b); b = _next[b];
            }
            foreach (int p in _path)
            {
                _visit[p] = success ? (byte)2 : (byte)3;
                if (!success && _reason[p] == ReasonCode.None) _reason[p] = ReasonCode.DependencyFailed;
            }
        }

        private void Withdraw(Request r)
        {
            int b = r.Belt, slot = _slot[b];
            var item = r.Config.Filter.IsNone ? Slots.FirstAvailable(r.Range) : r.Config.Filter;
            if (item.IsNone) { SetAccessor(r, 0, ReasonCode.SourceEmpty); return; }
            int available = Slots.Available(r.Range, item);
            if (available == 0) { SetAccessor(r, 0, ReasonCode.SourceEmpty); return; }
            if (Slots.Counts[slot] > 0 && (Slots.Items[slot] != item.Handle || _original[b] <= _out[b]))
            { SetAccessor(r, 0, ReasonCode.TargetFull); return; }
            if (!_view.Filtered(slot, item)) { SetAccessor(r, 0, ReasonCode.FilterMismatch); return; }
            if (_policy != null && r.Port >= 0)
            {
                ReasonCode permission = _policy.Check(_view, new BlockRef(r.Target), r.Port, item, false);
                if (permission != ReasonCode.None) { SetAccessor(r, 0, permission); return; }
            }
            int count;
            switch (r.Config.Quantity)
            {
                case AccessorQuantity.Exact:
                    if (available < r.Config.Amount) { SetAccessor(r, 0, ReasonCode.ExactUnsatisfied); return; }
                    count = r.Config.Amount; break;
                case AccessorQuantity.Keep:
                    count = Math.Min(available, Math.Min(3000, Math.Max(0, Slots.OriginalStock(r.Range, item) - r.Config.Amount))); break;
                default: count = Math.Min(available, r.Config.Amount); break;
            }
            int inserted = Slots.Insert(new SlotRange(slot, slot + 1), item, count, r.Config.Quantity == AccessorQuantity.Exact);
            if (inserted == 0)
            {
                SetAccessor(r, 0, count == 0 ? ReasonCode.None : r.Config.Quantity == AccessorQuantity.Exact ? ReasonCode.ExactUnsatisfied : ReasonCode.TargetFull);
                return;
            }
            Slots.Take(r.Range, item, inserted);
            _pending.Add(new LogisticsTransfer(new BlockRef(r.Target), new BlockRef(b), item, inserted, TransferKind.AccessorWithdraw));
            SetAccessor(r, inserted, ReasonCode.None);
        }

        private void SetAccessor(Request r, int count, ReasonCode reason)
        {
            if (r.Side >= 0) _accessorPending[r.Belt * 4 + r.Side] = new AccessorTickResult(r.Flow, count, reason);
        }

        internal bool CanCommit(in Transaction transaction, ulong revision)
            => _planned && !_committed && revision == _revision && transaction.Id.Equals(_batch)
                && transaction.SystemIndex == _systemIndex;
        internal void DidCommit() => _committed = true;

        public void OnTickCommitted(EventBus events, IWorldView world, ulong tickIndex)
        {
            LastBatchSucceeded = _committed; _transferCount = 0;
            if (_committed)
            {
                LastCommittedTick = tickIndex;
                if (_transfers.Length < _pending.Count) Array.Resize(ref _transfers, Math.Max(_pending.Count, _transfers.Length * 2));
                _pending.CopyTo(_transfers); _transferCount = _pending.Count;
                Array.Copy(_accessorPending, _accessorResults, _capturedBlockCount * 4);
            }
            else
            {
                for (int i = 0; i < _capturedBlockCount * 4; i++)
                    _accessorResults[i] = new AccessorTickResult(_accessorPending[i].Flow, 0, ReasonCode.CommitFault);
            }
            foreach (int b in _belts)
                _results[b] = new BeltTickResult(_original[b], _committed ? _out[b] : 0,
                    _committed ? _reason[b] : ReasonCode.CommitFault);
        }

        private void Ensure(int n)
        {
            if (_slot.Length >= n) return;
            int size = Math.Max(n, _slot.Length * 2);
            Array.Resize(ref _slot, size); Array.Resize(ref _original, size); Array.Resize(ref _item, size);
            Array.Resize(ref _out, size); Array.Resize(ref _next, size); Array.Resize(ref _winner, size);
            Array.Resize(ref _visit, size); Array.Resize(ref _reason, size); Array.Resize(ref _results, size);
            Array.Resize(ref _feedClaim, size); Array.Resize(ref _targetTie, size);
            Array.Resize(ref _accessorPending, size * 4); Array.Resize(ref _accessorResults, size * 4);
        }
    }
}

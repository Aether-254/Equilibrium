using System;
using System.Collections.Generic;
using Equilibrium.Simulation;

namespace Equilibrium.Tests.EditMode.TickSimulation
{
    /// <summary>Deterministic tie-break for tests; production code cannot reference this assembly.</summary>
    public sealed class FixedTieBreakSource : ITieBreakSource
    {
        private readonly ulong[] _keys;
        private int _cursor;
        public int BeginTickCalls { get; private set; }

        public FixedTieBreakSource(params ulong[] keys) { _keys = keys.Length == 0 ? new ulong[] { 0 } : keys; }

        public void BeginTick(ulong tickIndex) { BeginTickCalls++; _cursor = 0; }
        public ulong NextKey() => _keys[_cursor++ % _keys.Length];
    }

    /// <summary>
    /// N-slot integer world used until the real tables exist. Slots have a capacity; a move
    /// transaction (Payload.Items, B = amount) moves from Source.Index to Target.Index.
    /// Apply stages then swaps so a fault leaves state unchanged. Live reads assert the
    /// snapshot guard; snapshot values are copied at Snapshot time via <see cref="TakeSnapshot"/>.
    /// </summary>
    public sealed class FakeWorld : IWorldView, IWorldWriter, IConflictKeyProvider, ITickSystem
    {
        private readonly int[] _count;
        private readonly int[] _capacity;
        private int[] _snap;
        public int Applied;
        public bool ThrowMidApply;
        public readonly List<Transaction> Pending = new List<Transaction>();
        public readonly List<(int dependent, int dependency)> PendingDeps = new List<(int, int)>();
        public Func<Transaction, ReasonCode> ExtraValidator = _ => ReasonCode.None;

        public FakeWorld(int slots = 2, int capacity = int.MaxValue)
        {
            _count = new int[slots];
            _capacity = new int[slots];
            for (int i = 0; i < slots; i++) _capacity[i] = capacity;
            _snap = new int[slots];
        }

        public int this[int slot]
        {
            get { SnapshotGuard.AssertNotCollecting($"slot{slot}"); return _count[slot]; }
            set { _count[slot] = value; }
        }
        public int LiveRead(int slot) => this[slot];
        public int SnapshotRead(int slot) => _snap[slot];
        public void SetCapacity(int slot, int cap) => _capacity[slot] = cap;

        public static EntityRef SlotRef(int index) => new EntityRef(0, index);

        // ITickSystem: snapshot copy happens in Collect (first thing), then scripted submissions.
        public void Collect(in TickSnapshot snapshot, TransactionBuffer buffer, int systemIndex)
        {
            Array.Copy(_count, _snap, _count.Length);
            var ids = new List<TransactionId>(Pending.Count);
            foreach (var tx in Pending) ids.Add(buffer.Add(in tx, systemIndex));
            foreach (var (dependent, dependency) in PendingDeps) buffer.AddDependency(ids[dependent], ids[dependency]);
        }

        public ReasonCode Validate(in TickSnapshot snapshot, in Transaction tx)
        {
            int amount = tx.Payload.B;
            if (!tx.Source.IsNone && _snap[tx.Source.Index] < amount) return ReasonCode.ValidationFailed;
            return ExtraValidator(tx);
        }

        // IConflictKeyProvider: target slot capacity is the contested resource; the source slot
        // gains that much free capacity when the move commits.
        public void Describe(in Transaction tx, in TickSnapshot snapshot,
            out long resourceKey, out int demand, out int capacity,
            out long releaseKey, out int releaseAmount)
        {
            releaseKey = 0; releaseAmount = 0;
            if (!tx.Source.IsNone && _capacity[tx.Source.Index] != int.MaxValue)
            {
                releaseKey = 1000 + tx.Source.Index;
                releaseAmount = tx.Payload.B;
            }
            if (tx.Target.IsNone || _capacity[tx.Target.Index] == int.MaxValue) { resourceKey = 0; demand = 0; capacity = 0; return; }
            resourceKey = 1000 + tx.Target.Index;
            demand = tx.Payload.B;
            capacity = _capacity[tx.Target.Index] - _snap[tx.Target.Index];
        }

        // IWorldWriter: stage then swap.
        public void Apply(in Transaction accepted)
        {
            int amount = accepted.Payload.B;
            int s = accepted.Source.IsNone ? -1 : accepted.Source.Index;
            int t = accepted.Target.IsNone ? -1 : accepted.Target.Index;
            int newS = s >= 0 ? _count[s] - amount : 0;
            if (ThrowMidApply) throw new InvalidOperationException("fault injected");
            int newT = t >= 0 ? _count[t] + amount : 0;
            if (s >= 0) _count[s] = newS;
            if (t >= 0) _count[t] = newT;
            Applied++;
        }

        public static Transaction Move(int from, int to, int amount, int priority = 0)
            => Transaction.Create(SlotRef(from), SlotRef(to), Payload.Items(0, amount), priority, validator: 0);
    }

    public sealed class RecordingListener : ITickListener
    {
        public readonly List<TickPhase> PhasesAtCallback = new List<TickPhase>();
        public int CommittedCount;
        public int RejectedCount;
        private readonly TickEngine _engine;
        public RecordingListener(TickEngine engine) { _engine = engine; }

        public void OnTickCommitted(EventBus events, IWorldView world, ulong tickIndex)
        {
            PhasesAtCallback.Add(_engine.Phase);
            CommittedCount = events.Committed.Length;
            RejectedCount = events.Rejected.Length;
        }
    }

    public static class EngineFactory
    {
        public static TickEngine For(FakeWorld world, ITieBreakSource tie = null)
        {
            var engine = new TickEngine(world, world, tie ?? new FixedTieBreakSource(1, 2, 3), world);
            engine.Register(world);
            return engine;
        }
    }
}

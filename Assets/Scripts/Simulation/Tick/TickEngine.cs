using System;
using System.Collections.Generic;

namespace Equilibrium.Simulation
{
    /// <summary>
    /// Owns the tick lifecycle: Snapshot -> Collect -> Validate -> Resolve -> Commit -> Emit.
    /// Resolve orders transactions topologically by declared dependencies, groups conflicting
    /// candidates by contested resource, and sorts each group by (-priority, tieBreakKey).
    /// Commit applies accepted transactions through the single <see cref="IWorldWriter"/>;
    /// a writer fault rejects that transaction with <see cref="ReasonCode.CommitFault"/>.
    /// </summary>
    public sealed class TickEngine
    {
        private readonly List<ITickSystem> _systems = new List<ITickSystem>();
        private readonly List<ITickListener> _listeners = new List<ITickListener>();
        private readonly TransactionBuffer _buffer;
        private readonly EventBus _events = new EventBus();
        private readonly IWorldView _worldView;
        private readonly IWorldWriter _worldWriter;
        private readonly ITieBreakSource _tieBreak;
        private readonly IConflictKeyProvider _conflicts;
        private readonly Resolver _resolver;

        private bool _advancing;

        public ulong TickIndex { get; private set; }
        public TickPhase Phase { get; private set; } = TickPhase.Idle;
        public EventBus Events => _events;
        public IWorldView World => _worldView;

        /// <summary>Dependency chains longer than this emit a performance warning (GDD edge case).</summary>
        public int DependencyWarnThreshold { get; set; } = 1000;
        public int LastDependencyChainLength => _resolver.LastLongestChain;
        public bool LastTickHadDependencyWarning { get; private set; }

        /// <summary>Raised at the tick boundary after Emit; SaveService hooks here (ADR-0005).</summary>
        public event Action<ulong> TickBoundary;

        /// <summary>Live sessions obtain entropy only inside Simulation; editing previews never own RNG.</summary>
        public TickEngine(IWorldView worldView, IWorldWriter worldWriter)
            : this(worldView, worldWriter, new TieBreakSource()) { }

        public TickEngine(IWorldView worldView, IWorldWriter worldWriter, ITieBreakSource tieBreak,
            IConflictKeyProvider conflicts = null, int initialBufferCapacity = 1024)
        {
            _worldView = worldView ?? throw new ArgumentNullException(nameof(worldView));
            _worldWriter = worldWriter ?? throw new ArgumentNullException(nameof(worldWriter));
            _tieBreak = tieBreak ?? throw new ArgumentNullException(nameof(tieBreak));
            _conflicts = conflicts ?? NoConflicts.Instance;
            _buffer = new TransactionBuffer(initialBufferCapacity, initialBufferCapacity);
            _resolver = new Resolver(initialBufferCapacity);
        }

        /// <summary>Registers a system. Registration order is the Collect order; semantics never depend on it.</summary>
        public int Register(ITickSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (_advancing) throw new InvalidOperationException("Cannot register during a tick.");
            _systems.Add(system);
            return _systems.Count - 1;
        }

        public void AddListener(ITickListener listener)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            _listeners.Add(listener);
        }

        public void RemoveListener(ITickListener listener) => _listeners.Remove(listener);

        /// <summary>Hydration only: a new, never-advanced engine resumes at a committed save boundary.</summary>
        public void RestoreTickIndex(ulong tickIndex)
        {
            if (_advancing || Phase != TickPhase.Idle || TickIndex != 0)
                throw new InvalidOperationException("Tick restoration requires a fresh idle engine.");
            if (tickIndex == ulong.MaxValue) throw new ArgumentOutOfRangeException(nameof(tickIndex));
            TickIndex = tickIndex;
        }

        /// <summary>Runs exactly one whole tick. Never yields mid-phase.</summary>
        public void Advance()
        {
            if (_advancing) throw new InvalidOperationException("Re-entrant Advance.");
            _advancing = true;
            try
            {
                ulong tick = TickIndex + 1;

                Phase = TickPhase.Snapshot;
                _buffer.Clear();
                _events.Begin(tick);
                var snapshot = new TickSnapshot(tick, _worldView);

                Phase = TickPhase.Collect;
                SnapshotGuard.Enter();
                try
                {
                    for (int i = 0; i < _systems.Count; i++)
                        _systems[i].Collect(in snapshot, _buffer, i);
                }
                finally
                {
                    SnapshotGuard.Exit();
                }

                Phase = TickPhase.Validate;
                for (int i = 0; i < _buffer.Count; i++)
                {
                    ref Transaction tx = ref _buffer[i];
                    ReasonCode r = tx.Validator < 0
                        ? ReasonCode.None
                        : _systems[tx.SystemIndex].Validate(in snapshot, in tx);
                    tx.Reason = r;
                    tx.Accepted = r == ReasonCode.None;
                }

                Phase = TickPhase.Resolve;
                _tieBreak.BeginTick(tick);
                _resolver.Run(_buffer, _tieBreak, _conflicts, in snapshot);
                for (int i = 0; i < _systems.Count; i++)
                    if (_systems[i] is ITickBatchPlanner planner)
                        planner.Plan(in snapshot, _buffer, _tieBreak);
                LastTickHadDependencyWarning = _resolver.LastLongestChain > DependencyWarnThreshold;

                Phase = TickPhase.Commit;
                for (int i = 0; i < _buffer.Count; i++)
                {
                    ref Transaction tx = ref _buffer[i];
                    if (!tx.Accepted) continue;
                    try
                    {
                        _worldWriter.Apply(in tx);
                    }
                    catch (Exception)
                    {
                        tx.Accepted = false;
                        tx.Reason = ReasonCode.CommitFault;
                    }
                }

                Phase = TickPhase.Emit;
                for (int i = 0; i < _buffer.Count; i++)
                {
                    ref Transaction tx = ref _buffer[i];
                    if (tx.Accepted) _events.AddCommitted(in tx);
                    else _events.AddRejected(in tx);
                }
                TickIndex = tick;
                for (int i = 0; i < _listeners.Count; i++)
                    _listeners[i].OnTickCommitted(_events, _worldView, tick);

                TickBoundary?.Invoke(tick);
            }
            finally
            {
                Phase = TickPhase.Idle;
                _advancing = false;
            }
        }

        /// <summary>Advances <paramref name="ticks"/> whole ticks in sequence.</summary>
        public void Advance(int ticks)
        {
            for (int i = 0; i < ticks; i++) Advance();
        }
    }
}

using System;

namespace Equilibrium.Simulation
{
    /// <summary>
    /// Emitted once per committed transaction during Emit. Presentation, ledger, unlock and task
    /// systems consume these and never infer state from animation (GDD rule 7).
    /// </summary>
    public readonly struct CommittedEvent
    {
        public readonly TransactionId Id;
        public readonly EntityRef Source;
        public readonly EntityRef Target;
        public readonly Payload Payload;
        public readonly int SystemIndex;

        public CommittedEvent(in Transaction tx)
        {
            Id = tx.Id;
            Source = tx.Source;
            Target = tx.Target;
            Payload = tx.Payload;
            SystemIndex = tx.SystemIndex;
        }
    }

    /// <summary>Emitted once per rejected transaction so consumers can attribute losses and stalls.</summary>
    public readonly struct RejectedEvent
    {
        public readonly TransactionId Id;
        public readonly EntityRef Source;
        public readonly EntityRef Target;
        public readonly Payload Payload;
        public readonly int SystemIndex;
        public readonly ReasonCode Reason;

        public RejectedEvent(in Transaction tx)
        {
            Id = tx.Id;
            Source = tx.Source;
            Target = tx.Target;
            Payload = tx.Payload;
            SystemIndex = tx.SystemIndex;
            Reason = tx.Reason;
        }
    }

    /// <summary>
    /// Per-tick event lists. Cleared at Snapshot; readable during Emit callbacks and until the next
    /// tick begins. Backed by growable arrays that are reused across ticks.
    /// </summary>
    public sealed class EventBus
    {
        private CommittedEvent[] _committed = new CommittedEvent[256];
        private RejectedEvent[] _rejected = new RejectedEvent[64];
        private int _committedCount;
        private int _rejectedCount;

        public ulong TickIndex { get; private set; }

        public ReadOnlySpan<CommittedEvent> Committed => new ReadOnlySpan<CommittedEvent>(_committed, 0, _committedCount);
        public ReadOnlySpan<RejectedEvent> Rejected => new ReadOnlySpan<RejectedEvent>(_rejected, 0, _rejectedCount);

        internal void Begin(ulong tickIndex)
        {
            TickIndex = tickIndex;
            _committedCount = 0;
            _rejectedCount = 0;
        }

        internal void AddCommitted(in Transaction tx)
        {
            if (_committedCount == _committed.Length) Array.Resize(ref _committed, _committed.Length * 2);
            _committed[_committedCount++] = new CommittedEvent(tx);
        }

        internal void AddRejected(in Transaction tx)
        {
            if (_rejectedCount == _rejected.Length) Array.Resize(ref _rejected, _rejected.Length * 2);
            _rejected[_rejectedCount++] = new RejectedEvent(tx);
        }
    }

    /// <summary>
    /// Consumer of a tick's committed batch. Presentation and UI implement this (ADR-0007).
    /// </summary>
    public interface ITickListener
    {
        void OnTickCommitted(EventBus events, IWorldView world, ulong tickIndex);
    }
}

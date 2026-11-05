namespace Equilibrium.Simulation
{
    /// <summary>
    /// Immutable read view of the world for the current tick. Systems read only through this
    /// during Collect (tick-simulation GDD rule 3). The concrete world (ADR-0004) supplies
    /// <see cref="World"/>; story 001 only needs the tick index and a reference handle.
    /// </summary>
    public readonly struct TickSnapshot
    {
        public readonly ulong TickIndex;
        public readonly IWorldView World;

        public TickSnapshot(ulong tickIndex, IWorldView world)
        {
            TickIndex = tickIndex;
            World = world;
        }
    }

    /// <summary>
    /// Read-only surface over simulation state. Grid/container stories extend this interface;
    /// the engine requires only that implementations never expose mutation.
    /// </summary>
    public interface IWorldView
    {
    }

    /// <summary>
    /// The only object permitted to mutate simulation state, invoked exclusively during Commit.
    /// Implementations MUST be all-or-nothing per call: stage every change, then swap. If Apply
    /// throws, the engine treats the transaction as rejected with <see cref="ReasonCode.CommitFault"/>
    /// and expects no partial mutation to have occurred.
    /// </summary>
    public interface IWorldWriter
    {
        void Apply(in Transaction accepted);
    }

    /// <summary>
    /// A simulated system participating in the tick. Collect submits transactions against the
    /// snapshot; Validate is invoked per owned transaction with the snapshot to accept or reject.
    /// </summary>
    public interface ITickSystem
    {
        void Collect(in TickSnapshot snapshot, TransactionBuffer buffer, int systemIndex);

        /// <summary>
        /// Returns None to accept or a reason to reject. Default implementations may accept
        /// everything; capacity-aware systems consult the snapshot.
        /// </summary>
        ReasonCode Validate(in TickSnapshot snapshot, in Transaction tx);
    }

    /// <summary>
    /// Resolves an already collected atomic system batch without mutating the world. Called in
    /// Resolve after the generic transaction resolver; entropy belongs to the current tick.
    /// The system may stage its batch but must not change other systems' acceptance decisions.
    /// </summary>
    public interface ITickBatchPlanner
    {
        void Plan(in TickSnapshot snapshot, TransactionBuffer buffer, ITieBreakSource tieBreak);
    }

    /// <summary>
    /// Supplies the per-tick tie-break stream (ADR-0002). Story 004 implements the CSPRNG source;
    /// tests inject a fixed source.
    /// </summary>
    public interface ITieBreakSource
    {
        void BeginTick(ulong tickIndex);
        ulong NextKey();
    }
}

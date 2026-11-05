namespace Equilibrium.Simulation
{
    /// <summary>
    /// Ordered phases of one tick. Every phase runs to completion before the next starts
    /// and a tick never yields mid-phase (tick-simulation GDD rule 8).
    /// </summary>
    public enum TickPhase
    {
        Idle = 0,
        Snapshot,
        Collect,
        Validate,
        Resolve,
        Commit,
        Emit,
    }
}

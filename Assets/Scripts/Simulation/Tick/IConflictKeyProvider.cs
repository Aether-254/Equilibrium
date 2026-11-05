namespace Equilibrium.Simulation
{
    /// <summary>
    /// Tells the resolver which transactions compete for the same finite resource and how much
    /// of it exists. Two transactions conflict when they share a non-zero resource key and the
    /// summed demand exceeds the available amount (GDD conflict rule 1). A transaction may also
    /// release capacity on a resource when it commits (a group leaving a belt cell); dependents
    /// that declared a dependency on it may consume that released capacity in the same tick
    /// (conveyor GDD rule 10). World implementations provide keys; the engine stays agnostic.
    /// </summary>
    public interface IConflictKeyProvider
    {
        /// <summary>
        /// Describes the contested resource this transaction consumes (key 0 = none) and the
        /// resource it frees on commit (releaseKey 0 = none). Capacity is the free amount at
        /// snapshot time.
        /// </summary>
        void Describe(in Transaction tx, in TickSnapshot snapshot,
            out long resourceKey, out int demand, out int capacity,
            out long releaseKey, out int releaseAmount);
    }

    /// <summary>Default provider: nothing conflicts. Used before the world model exists and by simple tests.</summary>
    public sealed class NoConflicts : IConflictKeyProvider
    {
        public static readonly NoConflicts Instance = new NoConflicts();
        public void Describe(in Transaction tx, in TickSnapshot snapshot,
            out long resourceKey, out int demand, out int capacity,
            out long releaseKey, out int releaseAmount)
        {
            resourceKey = 0; demand = 0; capacity = 0; releaseKey = 0; releaseAmount = 0;
        }
    }
}

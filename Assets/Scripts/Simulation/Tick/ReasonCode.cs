namespace Equilibrium.Simulation
{
    /// <summary>
    /// Stable reason codes attached to rejected transactions and to committed events.
    /// Systems extend this list in their own stories; values are never renumbered.
    /// </summary>
    public enum ReasonCode : ushort
    {
        None = 0,

        // Engine-level
        ValidationFailed = 1,
        Contended = 2,
        DependencyFailed = 3,
        TransactionCycle = 4,
        SnapshotViolation = 5,
        CellContested = 6,
        CommitFault = 7,

        // Reserved ranges (allocated by later stories):
        // 100-199 container, 200-299 conveyor, 300-399 accessor,
        // 400-499 machine, 500-599 mechanical, 600-699 market,
        // 700-799 grid/structural, 800-899 content/save.

        // Container (100-199)
        PortBindingMissing = 100,
        TargetFull = 101,
        SourceEmpty = 102,
        FilterMismatch = 103,

        // Conveyor (200-299)
        NoReceivingPort = 200,
        DownstreamCapacity = 201,
        MachineNotAccepting = 202,
        SegmentTooShort = 203,

        // Accessor (300-399)
        TargetInvalid = 300,
        ExactUnsatisfied = 301,

        NoRecipe = 400,
        MissingInput = 401,
        ReservationBroken = 402,
        OutputFull = 403,
        Irreversible = 404,
        NoInput = 405,
        Stalled = 500,
        NoFuel = 501,
        NoPowerSource = 502,
        SourceConflict = 503,

        // Grid / structural (700-799)
        CellOccupied = 700,
        OutOfBounds = 701,
        WarehouseFull = 702,
    }
}

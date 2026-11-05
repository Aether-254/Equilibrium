using System;

namespace Equilibrium.Simulation
{
    /// <summary>
    /// Debug-only guard that lets a world detect live reads during Collect (GDD edge case
    /// "SnapshotViolation"). The engine sets <see cref="InCollect"/> around the Collect phase;
    /// world implementations call <see cref="AssertNotCollecting"/> from live accessors.
    /// Compiled out of release builds via the EQ_DEBUG_SNAPSHOT define.
    /// </summary>
    public static class SnapshotGuard
    {
        [ThreadStatic] private static bool _inCollect;

        public static bool InCollect => _inCollect;

        internal static void Enter() { _inCollect = true; }
        internal static void Exit() { _inCollect = false; }

        /// <summary>Throws <see cref="SnapshotViolationException"/> when called during Collect in debug builds.</summary>
        [System.Diagnostics.Conditional("EQ_DEBUG_SNAPSHOT")]
        public static void AssertNotCollecting(string what)
        {
            if (_inCollect) throw new SnapshotViolationException(what);
        }
    }

    public sealed class SnapshotViolationException : InvalidOperationException
    {
        public SnapshotViolationException(string what)
            : base($"Live world read of '{what}' during Collect; read the TickSnapshot instead.") { }
    }

    /// <summary>Raised when a writer faults mid-Apply; the engine converts it into a rejected transaction.</summary>
    public sealed class CommitFaultException : Exception
    {
        public TransactionId Transaction { get; }
        public CommitFaultException(TransactionId tx, Exception inner)
            : base($"Commit fault in {tx}: {inner.Message}", inner) { Transaction = tx; }
    }
}

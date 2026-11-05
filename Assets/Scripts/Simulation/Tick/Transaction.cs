using System;

namespace Equilibrium.Simulation
{
    /// <summary>
    /// One requested state change. Collected against the tick snapshot, validated, resolved,
    /// and either committed atomically or rejected with a <see cref="ReasonCode"/>.
    /// Dependencies are declared by <see cref="TransactionId"/> within the same tick and are
    /// stored in a side table on the <see cref="TransactionBuffer"/> to keep this struct flat.
    /// </summary>
    public struct Transaction
    {
        public TransactionId Id;
        public EntityRef Source;
        public EntityRef Target;
        public Payload Payload;
        /// <summary>Explicit priority; higher wins conflicts. Default 0. Range -1000..1000.</summary>
        public int Priority;
        /// <summary>Index into the owning system's validator table; -1 means always valid.</summary>
        public int Validator;
        /// <summary>System that submitted this transaction (index into engine registration order).</summary>
        public int SystemIndex;
        /// <summary>Set by Validate/Resolve; None while pending.</summary>
        public ReasonCode Reason;
        /// <summary>Set by Resolve.</summary>
        public bool Accepted;

        public const int PriorityMin = -1000;
        public const int PriorityMax = 1000;

        public static Transaction Create(EntityRef source, EntityRef target, Payload payload, int priority = 0, int validator = -1)
        {
            if (priority < PriorityMin || priority > PriorityMax)
                throw new ArgumentOutOfRangeException(nameof(priority));
            return new Transaction
            {
                Id = TransactionId.None,
                Source = source,
                Target = target,
                Payload = payload,
                Priority = priority,
                Validator = validator,
                SystemIndex = -1,
                Reason = ReasonCode.None,
                Accepted = false,
            };
        }
    }
}

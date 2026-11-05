using System;

namespace Equilibrium.Simulation
{
    /// <summary>
    /// Identity of a transaction within one tick. Valid only for the tick it was created in.
    /// </summary>
    public readonly struct TransactionId : IEquatable<TransactionId>
    {
        public readonly int Index;

        public TransactionId(int index) { Index = index; }

        public static readonly TransactionId None = new TransactionId(-1);
        public bool IsNone => Index < 0;

        public bool Equals(TransactionId other) => Index == other.Index;
        public override bool Equals(object obj) => obj is TransactionId o && Equals(o);
        public override int GetHashCode() => Index;
        public override string ToString() => IsNone ? "tx:none" : $"tx:{Index}";
    }

    /// <summary>
    /// Opaque reference to a simulated entity (block, container, warehouse...).
    /// The world data model (ADR-0004) defines how it resolves; the engine treats it as a key.
    /// </summary>
    public readonly struct EntityRef : IEquatable<EntityRef>
    {
        public readonly int Kind;
        public readonly int Index;

        public EntityRef(int kind, int index) { Kind = kind; Index = index; }

        public static readonly EntityRef None = new EntityRef(-1, -1);
        public bool IsNone => Kind < 0;

        public bool Equals(EntityRef other) => Kind == other.Kind && Index == other.Index;
        public override bool Equals(object obj) => obj is EntityRef o && Equals(o);
        public override int GetHashCode() => (Kind * 397) ^ Index;
        public override string ToString() => IsNone ? "ent:none" : $"ent:{Kind}/{Index}";
    }
}

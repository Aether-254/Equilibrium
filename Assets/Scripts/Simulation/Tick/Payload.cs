namespace Equilibrium.Simulation
{
    /// <summary>
    /// Discriminates what a transaction carries. Systems interpret the numeric fields per kind.
    /// Kept as a flat struct so buffers stay allocation-free (ADR-0001).
    /// </summary>
    public enum PayloadKind : byte
    {
        None = 0,
        Items = 1,
        Energy = 2,
        Torque = 3,
        Currency = 4,
        Structural = 5,
        LogisticsBatch = 6,
        FactoryEditBatch = 7,
        ProcessingBatch = 8,
        Custom = 255,
    }

    /// <summary>
    /// Flat payload record. For Items: A = item handle, B = count. For Energy/Torque/Currency: F = amount.
    /// Structural payloads encode operation in A and parameters in B/C. Systems may define
    /// their own conventions for Custom.
    /// </summary>
    public readonly struct Payload
    {
        public readonly PayloadKind Kind;
        public readonly int A;
        public readonly int B;
        public readonly int C;
        public readonly float F;

        public Payload(PayloadKind kind, int a = 0, int b = 0, int c = 0, float f = 0f)
        {
            Kind = kind; A = a; B = b; C = c; F = f;
        }

        public static Payload Items(int itemHandle, int count) => new Payload(PayloadKind.Items, itemHandle, count);
        public static Payload Structural(int op, int p0 = 0, int p1 = 0) => new Payload(PayloadKind.Structural, op, p0, p1);
        public static readonly Payload None = new Payload(PayloadKind.None);
    }
}

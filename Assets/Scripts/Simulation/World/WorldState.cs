using System;
using System.Collections.Generic;
using Equilibrium.Content;

namespace Equilibrium.Simulation.World
{
    /// <summary>
    /// Struct-of-arrays world (ADR-0004). Owns the grid, block instances, slots, ports, ingress
    /// bits and persistent bags. Only <see cref="WorldWriter"/> mutates it, and only during Commit.
    /// Reads go through <see cref="WorldView"/>. Compaction happens only between ticks.
    /// </summary>
    public sealed class WorldState
    {
        public const int ChunkSize = 16;

        public readonly int Width;
        public readonly int Height;
        public readonly ContentRegistry Registry;

        // Grid
        internal readonly int[] CellToBlock;        // block index or -1
        internal readonly byte[] Ingress;            // 4 bits per cell: bit s set => ingress from side s enabled
        internal readonly ulong[] DirtyChunks;       // one bit per chunk
        internal readonly List<int> DirtyChunkIndices = new List<int>();
        internal readonly int ChunksX, ChunksY;
        internal ulong SnapshotRevision;
        internal WorldSnapshotDelta EmptySnapshotDelta = WorldSnapshotDelta.InitialEmpty;

        // Blocks (parallel arrays)
        internal readonly List<BlockDefHandle> BlockDef = new List<BlockDefHandle>();
        internal readonly List<byte> Orientation = new List<byte>();
        internal readonly List<CellRef> AnchorCell = new List<CellRef>();
        internal readonly List<CellRef> ControllerCoord = new List<CellRef>();
        internal readonly List<int> SlotStart = new List<int>();
        internal readonly List<int> SlotCount = new List<int>();
        internal readonly List<int> PortStart = new List<int>();
        internal readonly List<int> PortCount = new List<int>();
        internal readonly List<PersistentBag> Bags = new List<PersistentBag>();
        internal readonly List<bool> Alive = new List<bool>();
        internal int LiveBlockCount;
        internal ulong MutationRevision;
        internal readonly List<AccessorConfiguration[]> Accessors = new List<AccessorConfiguration[]>();

        // Slots (parallel arrays)
        internal readonly List<int> SlotItem = new List<int>();       // ItemId handle or -1
        internal readonly List<int> SlotCountArr = new List<int>();
        internal readonly List<int> SlotCapacity = new List<int>();
        internal readonly List<int> SlotFilter = new List<int>();     // filter index or -1
        internal readonly List<int> SlotReserved = new List<int>();
        internal readonly List<int> SlotContainerLocal = new List<int>(); // which container of the block
        internal readonly List<string> SlotKey = new List<string>();
        internal readonly List<string> ContainerKeyOfSlot = new List<string>();

        // Ports
        internal readonly List<PortDef> Ports = new List<PortDef>();

        // Filters
        internal readonly List<SlotFilter> Filters = new List<SlotFilter>();

        public WorldState(int width, int height, ContentRegistry registry)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            Width = width; Height = height; Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            CellToBlock = new int[width * height];
            Array.Fill(CellToBlock, -1);
            Ingress = new byte[width * height];
            Array.Fill(Ingress, (byte)0xF);
            ChunksX = (width + ChunkSize - 1) / ChunkSize;
            ChunksY = (height + ChunkSize - 1) / ChunkSize;
            DirtyChunks = new ulong[(ChunksX * ChunksY + 63) / 64];
        }

        public bool InBounds(CellRef c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;
        internal int CellIndex(CellRef c) => c.Y * Width + c.X;

        internal void MarkDirty(CellRef c)
        {
            int chunk = (c.Y / ChunkSize) * ChunksX + (c.X / ChunkSize);
            ulong mask = 1UL << (chunk & 63);
            int word = chunk >> 6;
            if ((DirtyChunks[word] & mask) != 0) return;
            DirtyChunks[word] |= mask;
            DirtyChunkIndices.Add(chunk);
        }

        public int DirtyChunkCount() => DirtyChunkIndices.Count;

        internal void ClearDirty()
        {
            for (int i = 0; i < DirtyChunkIndices.Count; i++)
            {
                int chunk = DirtyChunkIndices[i];
                DirtyChunks[chunk >> 6] &= ~(1UL << (chunk & 63));
            }
            DirtyChunkIndices.Clear();
        }

        public int RegisterFilter(SlotFilter f) { Filters.Add(f); return Filters.Count - 1; }
    }

    public readonly struct CellRef : IEquatable<CellRef>
    {
        public readonly int X, Y;
        public CellRef(int x, int y) { X = x; Y = y; }
        public static readonly CellRef None = new CellRef(int.MinValue, int.MinValue);
        public bool IsNone => X == int.MinValue;
        public CellRef Step(Side s) => s switch
        {
            Side.North => new CellRef(X, Y + 1),
            Side.East => new CellRef(X + 1, Y),
            Side.South => new CellRef(X, Y - 1),
            _ => new CellRef(X - 1, Y),
        };
        public bool Equals(CellRef o) => X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is CellRef o && Equals(o);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";
    }

    public readonly struct BlockRef : IEquatable<BlockRef>
    {
        public readonly int Index;
        public BlockRef(int index) { Index = index; }
        public static readonly BlockRef None = new BlockRef(-1);
        public bool IsNone => Index < 0;
        public bool Equals(BlockRef o) => Index == o.Index;
        public override bool Equals(object obj) => obj is BlockRef o && Equals(o);
        public override int GetHashCode() => Index;
    }

    /// <summary>Orthogonal sides in clockwise order so rotation is addition mod 4.</summary>
    public enum Side : byte { North = 0, East = 1, South = 2, West = 3 }

    public static class SideExt
    {
        public static Side Rotate(this Side local, int orientation) => (Side)(((int)local + orientation) & 3);
        public static Side Opposite(this Side s) => (Side)(((int)s + 2) & 3);
        /// <summary>Direction from a to b when adjacent; throws otherwise.</summary>
        public static Side Dir(CellRef a, CellRef b)
        {
            if (b.X == a.X && b.Y == a.Y + 1) return Side.North;
            if (b.X == a.X + 1 && b.Y == a.Y) return Side.East;
            if (b.X == a.X && b.Y == a.Y - 1) return Side.South;
            if (b.X == a.X - 1 && b.Y == a.Y) return Side.West;
            throw new ArgumentException("cells not adjacent");
        }
    }

    public enum PortKind : byte
    {
        ItemIn, ItemOut, ByproductOut, RotationIn, RotationOut, EnergyIn, EnergyOut, FluidIn, FluidOut,
    }

    public static class PortKindExt
    {
        public static bool Compatible(PortKind outKind, PortKind inKind) =>
            (outKind == PortKind.ItemOut || outKind == PortKind.ByproductOut) && inKind == PortKind.ItemIn
            || outKind == PortKind.RotationOut && inKind == PortKind.RotationIn
            || outKind == PortKind.EnergyOut && inKind == PortKind.EnergyIn
            || outKind == PortKind.FluidOut && inKind == PortKind.FluidIn;

        public static bool IsOutput(this PortKind k) => k == PortKind.ItemOut || k == PortKind.ByproductOut || k == PortKind.RotationOut || k == PortKind.EnergyOut || k == PortKind.FluidOut;
    }

    /// <summary>Port definition as resolved for a block instance (facing is local; rotate at query time).</summary>
    public readonly struct PortDef
    {
        public readonly PortKind Kind;
        public readonly Side LocalFacing;
        public readonly string ContainerKey;
        public readonly string SlotKey;      // may be null: any slot in the container
        public readonly bool AcceptsDirectBelt;
        public readonly bool BindingMissing;

        public PortDef(PortKind kind, Side localFacing, string containerKey, string slotKey, bool acceptsDirectBelt, bool bindingMissing = false)
        {
            Kind = kind; LocalFacing = localFacing; ContainerKey = containerKey; SlotKey = slotKey; AcceptsDirectBelt = acceptsDirectBelt; BindingMissing = bindingMissing;
        }
        public PortDef WithBindingMissing() => new PortDef(Kind, LocalFacing, ContainerKey, SlotKey, AcceptsDirectBelt, true);
    }

    /// <summary>Allow/deny filter over ItemIds and Tags; deny beats allow (container GDD rule 4).</summary>
    public sealed class SlotFilter
    {
        public readonly HashSet<int> AllowItems = new HashSet<int>();
        public readonly HashSet<TagId> AllowTags = new HashSet<TagId>();
        public readonly HashSet<int> DenyItems = new HashSet<int>();
        public readonly HashSet<TagId> DenyTags = new HashSet<TagId>();

        public bool Accepts(ContentRegistry reg, ItemId item)
        {
            if (DenyItems.Contains(item.Handle)) return false;
            foreach (var t in DenyTags) if (reg.InTag(item, t)) return false;
            if (AllowItems.Count == 0 && AllowTags.Count == 0) return true;
            if (AllowItems.Contains(item.Handle)) return true;
            foreach (var t in AllowTags) if (reg.InTag(item, t)) return true;
            return false;
        }
    }

    /// <summary>Handle to a block definition in <see cref="BlockDefTable"/>.</summary>
    public readonly struct BlockDefHandle
    {
        public readonly int Index;
        public BlockDefHandle(int index) { Index = index; }
        public bool IsNone => Index < 0;
        public static readonly BlockDefHandle None = new BlockDefHandle(-1);
    }
}

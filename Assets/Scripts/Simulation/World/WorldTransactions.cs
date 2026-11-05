using System;

namespace Equilibrium.Simulation.World
{
    /// <summary>Stable EntityRef kinds owned by the world module.</summary>
    public static class WorldEntityKinds
    {
        public const int Cell = 1;
        public const int Block = 2;
    }

    /// <summary>Stable operation values encoded in Payload.Structural.A.</summary>
    public enum StructuralOperation : int
    {
        Place = 1,
        Remove = 2,
        Rotate = 3,
    }

    /// <summary>
    /// Allocation-free structural transaction constructors. Cell targets are flattened using the
    /// world's width; the writer validates the encoded target against its own dimensions.
    /// </summary>
    public static class WorldTransactions
    {
        public static Transaction Place(
            EntityRef source,
            BlockDefHandle definition,
            CellRef cell,
            int worldWidth,
            int orientation,
            int priority = 0,
            int validator = -1)
        {
            if (worldWidth <= 0) throw new ArgumentOutOfRangeException(nameof(worldWidth));
            if (cell.X < 0 || cell.Y < 0 || cell.X >= worldWidth)
                throw new ArgumentOutOfRangeException(nameof(cell));
            int index = checked(cell.Y * worldWidth + cell.X);
            return Transaction.Create(
                source,
                new EntityRef(WorldEntityKinds.Cell, index),
                Payload.Structural((int)StructuralOperation.Place, definition.Index, orientation),
                priority,
                validator);
        }

        public static Transaction Remove(
            EntityRef source,
            BlockRef block,
            int priority = 0,
            int validator = -1)
            => Transaction.Create(
                source,
                new EntityRef(WorldEntityKinds.Block, block.Index),
                Payload.Structural((int)StructuralOperation.Remove),
                priority,
                validator);

        public static Transaction Rotate(
            EntityRef source,
            BlockRef block,
            int orientation,
            int priority = 0,
            int validator = -1)
            => Transaction.Create(
                source,
                new EntityRef(WorldEntityKinds.Block, block.Index),
                Payload.Structural((int)StructuralOperation.Rotate, orientation),
                priority,
                validator);

        internal static CellRef DecodeCell(EntityRef target, int width, int height)
        {
            if (target.Kind != WorldEntityKinds.Cell || target.Index < 0)
                throw new InvalidOperationException($"Expected cell target, got {target}.");
            int x = target.Index % width;
            int y = target.Index / width;
            if (y >= height) throw new InvalidOperationException($"Cell target {target.Index} is outside {width}x{height}.");
            return new CellRef(x, y);
        }
    }
}

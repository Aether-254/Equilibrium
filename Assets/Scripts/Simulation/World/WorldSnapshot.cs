using System;
using System.Collections.Generic;

namespace Equilibrium.Simulation.World
{
    /// <summary>
    /// Immutable occupancy and ingress view for one 16x16 (or edge-clipped) world chunk.
    /// Block details remain addressable through the committed <see cref="WorldView"/>; this view
    /// is the structural delta consumed by renderers, save preparation, and debug overlays.
    /// </summary>
    public sealed class WorldChunkView
    {
        private readonly int[] _cellToBlock;
        private readonly byte[] _ingress;
        private readonly BlockDefHandle[] _definitions;
        private readonly byte[] _orientations;
        private readonly CellRef[] _controllers;

        public int ChunkX { get; }
        public int ChunkY { get; }
        public int OriginX { get; }
        public int OriginY { get; }
        public int Width { get; }
        public int Height { get; }

        private WorldChunkView(
            int chunkX,
            int chunkY,
            int originX,
            int originY,
            int width,
            int height,
            int[] cellToBlock,
            byte[] ingress,
            BlockDefHandle[] definitions,
            byte[] orientations,
            CellRef[] controllers)
        {
            ChunkX = chunkX;
            ChunkY = chunkY;
            OriginX = originX;
            OriginY = originY;
            Width = width;
            Height = height;
            _cellToBlock = cellToBlock;
            _ingress = ingress;
            _definitions = definitions;
            _orientations = orientations;
            _controllers = controllers;
        }

        public BlockRef BlockAtLocal(int x, int y)
        {
            int index = LocalIndex(x, y);
            int block = _cellToBlock[index];
            return block < 0 ? BlockRef.None : new BlockRef(block);
        }

        public bool IngressEnabledLocal(int x, int y, Side side)
        {
            int index = LocalIndex(x, y);
            return (_ingress[index] & (1 << (int)side)) != 0;
        }

        public BlockDefHandle DefinitionAtLocal(int x, int y) => _definitions[LocalIndex(x, y)];
        public int OrientationAtLocal(int x, int y) => _orientations[LocalIndex(x, y)];
        public CellRef ControllerAtLocal(int x, int y) => _controllers[LocalIndex(x, y)];

        public CellRef WorldCell(int localX, int localY)
        {
            LocalIndex(localX, localY);
            return new CellRef(OriginX + localX, OriginY + localY);
        }

        private int LocalIndex(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                throw new ArgumentOutOfRangeException($"local cell ({x},{y}) outside {Width}x{Height} chunk");
            return y * Width + x;
        }

        internal static WorldChunkView Capture(WorldState state, int chunkIndex)
        {
            int chunkX = chunkIndex % state.ChunksX;
            int chunkY = chunkIndex / state.ChunksX;
            int originX = chunkX * WorldState.ChunkSize;
            int originY = chunkY * WorldState.ChunkSize;
            int width = Math.Min(WorldState.ChunkSize, state.Width - originX);
            int height = Math.Min(WorldState.ChunkSize, state.Height - originY);
            var cells = new int[width * height];
            var ingress = new byte[width * height];
            var definitions = new BlockDefHandle[width * height];
            var orientations = new byte[width * height];
            var controllers = new CellRef[width * height];

            for (int y = 0; y < height; y++)
            {
                int source = (originY + y) * state.Width + originX;
                int target = y * width;
                Array.Copy(state.CellToBlock, source, cells, target, width);
                Array.Copy(state.Ingress, source, ingress, target, width);
            }

            for (int i = 0; i < cells.Length; i++)
            {
                int block = cells[i];
                if (block < 0)
                {
                    definitions[i] = BlockDefHandle.None;
                    controllers[i] = CellRef.None;
                    continue;
                }
                definitions[i] = state.BlockDef[block];
                orientations[i] = state.Orientation[block];
                controllers[i] = state.ControllerCoord[block];
            }

            return new WorldChunkView(
                chunkX,
                chunkY,
                originX,
                originY,
                width,
                height,
                cells,
                ingress,
                definitions,
                orientations,
                controllers);
        }
    }

    /// <summary>
    /// O(dirty chunks) structural snapshot delta. The changed chunk collection and every contained
    /// chunk are immutable after construction.
    /// </summary>
    public sealed class WorldSnapshotDelta
    {
        internal static readonly WorldSnapshotDelta InitialEmpty =
            new WorldSnapshotDelta(0, Array.Empty<WorldChunkView>());

        public ulong Revision { get; }
        public IReadOnlyList<WorldChunkView> ChangedChunks { get; }

        internal WorldSnapshotDelta(ulong revision, WorldChunkView[] changedChunks)
        {
            Revision = revision;
            ChangedChunks = Array.AsReadOnly(changedChunks);
        }
    }
}

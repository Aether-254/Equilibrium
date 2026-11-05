using System;
using System.Collections.Generic;
using Equilibrium.Simulation.World;

namespace Equilibrium.Editing
{
    /// <summary>Pure drag state. The first crossed cell fixes the axis; reversing never commits.</summary>
    public sealed class StraightBeltPreview
    {
        private readonly List<CellRef> _cells = new List<CellRef>();
        public IReadOnlyList<CellRef> Cells => _cells;
        public CellRef Origin { get; private set; }
        public bool Active { get; private set; }
        public bool AxisLocked { get; private set; }
        public bool Reversed { get; private set; }
        public int Orientation { get; private set; }
        private bool _horizontal;
        private Side _initialDirection;

        public void Begin(CellRef origin)
        { Origin = origin; Active = true; AxisLocked = Reversed = false; _initialDirection = Side.East; SetOrientation(); _cells.Clear(); _cells.Add(origin); }
        public void Reverse() { if (Active) { Reversed = !Reversed; SetOrientation(); } }
        public void Cancel() { Active = false; _cells.Clear(); }

        public void Update(CellRef cursor, Func<CellRef, bool> available)
        {
            if (!Active) return;
            int dx = cursor.X - Origin.X, dy = cursor.Y - Origin.Y;
            if (!AxisLocked && (dx != 0 || dy != 0))
            {
                _horizontal = Math.Abs(dx) >= Math.Abs(dy); AxisLocked = true;
                _initialDirection = _horizontal ? (dx > 0 ? Side.East : Side.West) : (dy > 0 ? Side.North : Side.South);
            }
            _cells.Clear(); _cells.Add(Origin); SetOrientation();
            if (!AxisLocked || !available(Origin)) return;
            int signed = _horizontal ? dx : dy;
            int step = Math.Sign(signed), length = Math.Abs(signed);
            // Returning across the origin shortens the drag; it never silently reverses its flow.
            bool initialPositive = _initialDirection == Side.East || _initialDirection == Side.North;
            if ((step > 0) != initialPositive) return;
            for (int i = 1; i <= length; i++)
            {
                var cell = new CellRef(Origin.X + (_horizontal ? i * step : 0), Origin.Y + (_horizontal ? 0 : i * step));
                if (!available(cell)) break;
                _cells.Add(cell);
            }
        }

        private void SetOrientation()
        {
            Side direction = Reversed ? _initialDirection.Opposite() : _initialDirection;
            Orientation = ((int)direction - (int)Side.East + 4) & 3;
        }
    }
}

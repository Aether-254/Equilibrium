using System;

namespace Equilibrium.Simulation.World
{
    public sealed partial class WorldWriter
    {
        private LogisticsSystem _logistics;

        private void Touch()
        {
            SnapshotGuard.AssertNotCollecting("WorldWriter mutation");
            _s.MutationRevision++;
        }

        internal void BindLogistics(LogisticsSystem system)
        {
            if (_logistics != null) throw new InvalidOperationException("Only one logistics system may own this world.");
            _logistics = system;
        }

        /// <summary>Setup/boundary API, like Place/Rotate. Null removes the attachment on that local side.</summary>
        public void ConfigureAccessor(BlockRef belt, Side localSide, AccessorConfiguration configuration)
        {
            if (!_view.IsAlive(belt) || _defs.Get(_view.DefOf(belt)).Kind != BlockKind.Conveyor)
                throw new ArgumentException("An accessor must be attached to a live conveyor.", nameof(belt));
            if ((uint)localSide > 3) throw new ArgumentOutOfRangeException(nameof(localSide));
            Touch();
            _s.Accessors[belt.Index][(int)localSide] = configuration;
        }

        private void ApplyLogistics(in Transaction transaction)
        {
            if (_logistics == null || !_logistics.CanCommit(in transaction, _s.MutationRevision))
                throw new InvalidOperationException("Logistics plan is stale or belongs to a different tick.");
            // Every rule, source budget, reservation and target capacity was resolved on scratch data.
            // No allocation or validation is left after this point; consumers observe only Emit.
            var slots = _logistics.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                _s.SlotItem[i] = slots.Items[i];
                _s.SlotCountArr[i] = slots.Counts[i];
            }
            _s.MutationRevision++;
            _logistics.DidCommit();
        }
    }
}

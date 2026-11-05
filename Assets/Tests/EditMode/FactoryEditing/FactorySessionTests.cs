using System.Collections.Generic;
using Equilibrium.Editing;
using Equilibrium.Simulation;
using Equilibrium.Simulation.World;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.FactoryEditing
{
    public sealed class FactorySessionTests
    {
        private static CellRef C(int x, int y = 1) => new CellRef(x, y);
        private static FactorySession Session(int belts = 8) => new FactorySession(12, 8,
            new Dictionary<string, int> { ["belt"] = belts, ["chest"] = 4, ["accessor"] = 4, ["blocker"] = 4, ["wood"] = 256 });
        private static int Count(FactorySession s, CellRef cell)
        {
            var b = s.World.BlockAt(cell); if (b.IsNone) return 0;
            int n = 0; for (int slot = 0; slot < s.World.SlotCount(b); slot++) n += s.World.SlotCountOf(s.World.SlotGlobal(b, slot));
            return n;
        }
        private static int TotalWood(FactorySession s)
        {
            int n = s.Stock("wood"), wood = s.Registry.Items.Parse("wood").Handle;
            for (int i = 0; i < s.World.BlockIndexCount; i++)
            {
                var b = new BlockRef(i); if (!s.World.IsAlive(b)) continue;
                for (int slot = 0; slot < s.World.SlotCount(b); slot++)
                { int g = s.World.SlotGlobal(b, slot); if (s.World.SlotItemHandle(g) == wood) n += s.World.SlotCountOf(g); }
            }
            foreach (var drop in s.RecoveredBlocks) foreach (var item in drop.Bag.Items) if (item.item == wood) n += item.count;
            return n;
        }

        [Test]
        public void BuildWaitsForTickConsumesFiniteStockAndQueuesMissingMaterials()
        {
            var s = Session(2); s.QueueBuild(Buildable.Belt, new[] { C(1), C(2), C(3) }, 0);
            Assert.IsTrue(s.World.IsEmpty(C(1))); Assert.AreEqual(2, s.Stock("belt"));
            s.Engine.Advance();
            Assert.IsFalse(s.World.IsEmpty(C(1))); Assert.IsFalse(s.World.IsEmpty(C(2)));
            Assert.IsTrue(s.World.IsEmpty(C(3))); Assert.AreEqual(1, s.Ghosts.Count); Assert.AreEqual(0, s.Stock("belt"));
            s.QueueRemove(C(1)); s.Engine.Advance();
            Assert.IsTrue(s.World.IsEmpty(C(1))); Assert.IsFalse(s.World.IsEmpty(C(3))); Assert.AreEqual(0, s.Ghosts.Count);
            Assert.AreEqual(0, s.Stock("belt"));
        }

        [Test]
        public void OccupiedPathRejectsWholePlacementWithoutMaterialLoss()
        {
            var s = Session(); s.QueueBuild(Buildable.Chest, new[] { C(2) }, 0); s.Engine.Advance();
            s.QueueBuild(Buildable.Belt, new[] { C(1), C(2), C(3) }, 0); s.Engine.Advance();
            Assert.AreEqual(ReasonCode.CellOccupied, s.LastEditReason); Assert.AreEqual(8, s.Stock("belt"));
            Assert.IsTrue(s.World.IsEmpty(C(1))); Assert.AreEqual(0, s.Ghosts.Count);
        }

        [Test]
        public void OneCellAndBentBeltsAreRejected()
        {
            var s = Session(); s.QueueBuild(Buildable.Belt, new[] { C(1) }, 0); s.Engine.Advance();
            Assert.AreEqual(ReasonCode.SegmentTooShort, s.LastEditReason);
            s.QueueBuild(Buildable.Belt, new[] { C(1), C(2), C(2, 2) }, 0); s.Engine.Advance();
            Assert.AreEqual(ReasonCode.ValidationFailed, s.LastEditReason); Assert.AreEqual(8, s.Stock("belt"));
        }

        [Test]
        public void GhostCanBeCancelledWithoutReturningUnspentMaterial()
        {
            var s = Session(0); s.QueueBuild(Buildable.Belt, new[] { C(1), C(2) }, 0); s.Engine.Advance();
            s.QueueRemove(C(1)); s.Engine.Advance();
            Assert.AreEqual(1, s.Ghosts.Count); Assert.AreEqual(0, s.Stock("belt")); Assert.IsFalse(s.IsReserved(C(1)));
        }

        [Test]
        public void ChestContentsTravelWithRecoveredChestButOrientationDoesNot()
        {
            var s = Session(); s.QueueBuild(Buildable.Chest, new[] { C(1) }, 1); s.Engine.Advance();
            s.QueueDeposit(C(1), s.Registry.Items.Parse("wood"), 80); s.Engine.Advance();
            s.QueueRemove(C(1)); s.Engine.Advance();
            Assert.AreEqual(176, s.Stock("wood")); Assert.AreEqual(1, s.RecoveredBlocks.Count); Assert.AreEqual(256, TotalWood(s));
            s.QueueBuild(Buildable.Chest, new[] { C(4) }, 3); s.Engine.Advance();
            Assert.AreEqual(80, Count(s, C(4))); Assert.AreEqual(3, s.World.OrientationOf(s.World.BlockAt(C(4))));
            Assert.AreEqual(0, s.RecoveredBlocks.Count); Assert.AreEqual(256, TotalWood(s));
        }

        [Test]
        public void ChestDepositUsesEffectiveCapacityAndCollectionConservesStock()
        {
            var s = Session(); s.QueueBuild(Buildable.Chest, new[] { C(1) }, 0); s.Engine.Advance();
            s.QueueDeposit(C(1), s.Registry.Items.Parse("wood"), 3000); s.Engine.Advance();
            Assert.AreEqual(256, Count(s, C(1))); Assert.AreEqual(0, s.Stock("wood"));
            s.QueueCollect(C(1)); s.Engine.Advance(); Assert.AreEqual(256, s.Stock("wood")); Assert.AreEqual(0, Count(s, C(1)));
        }

        [Test]
        public void AttachmentsAreRefundedAndBlockerDoesNotSurviveRemoval()
        {
            var s = Session(); s.QueueBuild(Buildable.Belt, new[] { C(1), C(1, 2) }, 1); s.Engine.Advance();
            s.QueueAccessor(C(1), Side.West, new AccessorConfiguration()); s.QueueBlocker(C(1), Side.North); s.Engine.Advance();
            Assert.AreEqual(3, s.Stock("accessor")); Assert.AreEqual(3, s.Stock("blocker"));
            Assert.IsFalse(s.World.IngressEnabled(C(1), Side.North));
            s.QueueRemove(C(1)); s.Engine.Advance();
            Assert.AreEqual(4, s.Stock("accessor")); Assert.AreEqual(4, s.Stock("blocker"));
            Assert.IsTrue(s.World.IngressEnabled(C(1), Side.North));
        }

        [Test]
        public void StaleClickCannotEditReplacementBlockInSameCell()
        {
            var s = Session(); s.QueueBuild(Buildable.Chest, new[] { C(1) }, 0); s.Engine.Advance();
            s.QueueRemove(C(1)); s.QueueBuild(Buildable.Chest, new[] { C(1) }, 0);
            s.QueueDeposit(C(1), s.Registry.Items.Parse("wood"), 16); s.Engine.Advance();
            Assert.AreEqual(ReasonCode.TargetInvalid, s.LastEditReason); Assert.AreEqual(0, Count(s, C(1)));
            Assert.AreEqual(256, TotalWood(s));
        }

        [Test]
        public void EditingAfterLogisticsDoesNotInvalidateBatchOrLoseItems()
        {
            var s = Session();
            s.QueueBuild(Buildable.Chest, new[] { C(0) }, 0); s.QueueBuild(Buildable.Chest, new[] { C(4) }, 0);
            s.QueueBuild(Buildable.Belt, new[] { C(1), C(2), C(3) }, 0); s.Engine.Advance();
            s.QueueDeposit(C(0), s.Registry.Items.Parse("wood"), 64);
            s.QueueAccessor(C(1), Side.West, new AccessorConfiguration());
            s.QueueAccessor(C(3), Side.East, new AccessorConfiguration()); s.Engine.Advance();
            Assert.AreEqual(0, Count(s, C(1)), "A new connection starts next tick.");
            s.Engine.Advance(); Assert.AreEqual(16, Count(s, C(1)));
            s.Engine.Advance(); Assert.AreEqual(16, Count(s, C(2)));
            s.QueueRemove(C(2)); s.Engine.Advance();
            Assert.IsTrue(s.Logistics.LastBatchSucceeded); Assert.AreEqual(256, TotalWood(s));
            Assert.AreEqual(16, Count(s, C(3))); Assert.IsTrue(s.World.IsEmpty(C(2)));
            s.Engine.Advance(); Assert.AreEqual(16, Count(s, C(4))); Assert.AreEqual(256, TotalWood(s));
        }

        [Test]
        public void FirstCrossingLocksAxisAndReverseOnlyChangesPreviewDirection()
        {
            var drag = new StraightBeltPreview(); drag.Begin(C(2)); drag.Update(C(3), _ => true);
            drag.Update(C(6, 5), _ => true);
            Assert.AreEqual(5, drag.Cells.Count); Assert.AreEqual(C(6), drag.Cells[4]); Assert.AreEqual(0, drag.Orientation);
            drag.Reverse(); Assert.AreEqual(2, drag.Orientation); Assert.AreEqual(5, drag.Cells.Count);
            drag.Reverse(); Assert.AreEqual(0, drag.Orientation);
            drag.Update(C(1), _ => true); Assert.AreEqual(1, drag.Cells.Count);
        }

        [Test]
        public void DragStopsAtFirstObstacleAndCancelClearsPreview()
        {
            var drag = new StraightBeltPreview(); drag.Begin(C(1)); drag.Update(C(8), c => c.X != 4);
            Assert.AreEqual(3, drag.Cells.Count); Assert.AreEqual(C(3), drag.Cells[2]);
            drag.Cancel(); Assert.IsFalse(drag.Active); Assert.AreEqual(0, drag.Cells.Count);
            drag.Begin(C(2)); Assert.AreEqual(0, drag.Orientation); Assert.IsFalse(drag.Reversed);
        }
    }
}

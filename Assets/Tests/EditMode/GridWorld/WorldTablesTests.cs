using System.Diagnostics;
using Equilibrium.Content;
using Equilibrium.Simulation;
using Equilibrium.Simulation.World;
using Equilibrium.Tests.EditMode.TickSimulation;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.GridWorld
{
    /// <summary>Shared fixture: Tier 1 registry, a 16x16 world with machine/chest/belt defs.</summary>
    public sealed class WorldFixture
    {
        public readonly ContentRegistry Registry = new ContentRegistry();
        public readonly BlockDefTable Defs = new BlockDefTable();
        public readonly WorldState State;
        public readonly WorldWriter Writer;
        public readonly WorldView View;
        public readonly BlockDefHandle Machine, Chest, Belt;
        public readonly ItemId Wood, Log, Plank, Saw, ChestItem, BeltItem;

        public WorldFixture(int w = 16, int h = 16)
        {
            new ContentLoader().Load(Registry, Tier1Content.Build(), System.Array.Empty<ContentPack>());
            Wood = Registry.Items.Parse("wood"); Log = Registry.Items.Parse("log"); Plank = Registry.Items.Parse("plank");
            Saw = Registry.Items.Parse("saw"); ChestItem = Registry.Items.Parse("chest"); BeltItem = Registry.Items.Parse("belt");
            State = new WorldState(w, h, Registry);
            Machine = Defs.Register(BlockDefinition.Machine("saw", Saw.Handle, 64, 64));
            Chest = Defs.Register(BlockDefinition.Chest("chest", ChestItem.Handle, 4, 200));
            Belt = Defs.Register(BlockDefinition.Belt("belt", BeltItem.Handle));
            Writer = new WorldWriter(State, Defs);
            View = Writer.View;
        }

        public BlockRef Place(BlockDefHandle def, int x, int y, int orientation = 0)
        {
            Assert.AreEqual(ReasonCode.None, Writer.Place(def, new CellRef(x, y), orientation, null, out var b));
            return b;
        }

        public (int start, int end) Slots(BlockRef b, string containerKey)
        {
            int s = View.SlotGlobal(b, 0), e = s + View.SlotCount(b);
            int first = -1, last = -1;
            for (int g = s; g < e; g++) if (View.ContainerKeyOf(g) == containerKey) { if (first < 0) first = g; last = g; }
            return (first, last + 1);
        }
    }

    /// <summary>Story grid-world-001 (TR-grid-001).</summary>
    public class WorldTablesTests
    {
        private sealed class OneShotStructuralSystem : ITickSystem
        {
            private Transaction _transaction;
            private bool _pending;

            public void Submit(in Transaction transaction)
            {
                _transaction = transaction;
                _pending = true;
            }

            public void Collect(in TickSnapshot snapshot, TransactionBuffer buffer, int systemIndex)
            {
                if (!_pending) return;
                buffer.Add(in _transaction, systemIndex);
                _pending = false;
            }

            public ReasonCode Validate(in TickSnapshot snapshot, in Transaction tx) => ReasonCode.None;
        }

        [Test]
        public void test_out_of_bounds_expected_void_not_empty()
        {
            var f = new WorldFixture();
            Assert.IsTrue(f.View.IsVoid(new CellRef(-1, 0)));
            Assert.IsTrue(f.View.IsVoid(new CellRef(16, 3)));
            Assert.IsFalse(f.View.IsEmpty(new CellRef(16, 3)));
            Assert.IsTrue(f.View.BlockAt(new CellRef(99, 99)).IsNone);
        }

        [Test]
        public void test_place_on_occupied_cell_expected_cell_occupied_unchanged()
        {
            var f = new WorldFixture();
            var a = f.Place(f.Chest, 2, 2);
            Assert.AreEqual(ReasonCode.CellOccupied, f.Writer.Place(f.Machine, new CellRef(2, 2), 0, null, out var b));
            Assert.IsTrue(b.IsNone);
            Assert.AreEqual(a, f.View.BlockAt(new CellRef(2, 2)));
        }

        [Test]
        public void test_remove_expected_cell_empty_and_drop_item()
        {
            var f = new WorldFixture();
            var a = f.Place(f.Machine, 3, 3, orientation: 2);
            Assert.AreEqual(ReasonCode.None, f.Writer.Remove(a, out var drop));
            Assert.IsTrue(f.View.IsEmpty(new CellRef(3, 3)));
            Assert.IsFalse(f.View.IsAlive(a));
            Assert.AreEqual(f.Saw, drop.Item);
        }

        [Test]
        public void test_rotate_expected_orientation_updated()
        {
            var f = new WorldFixture();
            var a = f.Place(f.Machine, 1, 1);
            f.Writer.Rotate(a, 3);
            Assert.AreEqual(3, f.View.OrientationOf(a));
            f.Writer.Rotate(a, 5);
            Assert.AreEqual(1, f.View.OrientationOf(a), "orientation wraps mod 4");
        }

        [Test]
        public void test_structural_transaction_expected_visible_only_after_commit()
        {
            var f = new WorldFixture();
            var system = new OneShotStructuralSystem();
            var engine = new TickEngine(f.View, f.Writer, new FixedTieBreakSource(1));
            engine.Register(system);

            Transaction place = WorldTransactions.Place(
                EntityRef.None,
                f.Machine,
                new CellRef(4, 5),
                f.View.Width,
                orientation: 1);
            system.Submit(in place);
            Assert.IsTrue(f.View.IsEmpty(new CellRef(4, 5)), "Collect has not started; command is not visible");
            engine.Advance();

            BlockRef placed = f.View.BlockAt(new CellRef(4, 5));
            Assert.IsFalse(placed.IsNone);
            Assert.AreEqual(1, f.View.OrientationOf(placed));
            Assert.AreEqual(1, engine.Events.Committed.Length);

            Transaction rotate = WorldTransactions.Rotate(EntityRef.None, placed, orientation: 3);
            system.Submit(in rotate);
            Assert.AreEqual(1, f.View.OrientationOf(placed), "rotation remains old before the next Commit");
            engine.Advance();
            Assert.AreEqual(3, f.View.OrientationOf(placed));

            Transaction remove = WorldTransactions.Remove(EntityRef.None, placed);
            system.Submit(in remove);
            engine.Advance();
            Assert.IsTrue(f.View.IsEmpty(new CellRef(4, 5)));
            DropRecord[] drops = f.Writer.CaptureCommittedDrops();
            Assert.AreEqual(1, drops.Length);
            Assert.AreEqual(f.Saw, drops[0].Item);
            Assert.AreEqual(0, f.Writer.CaptureCommittedDrops().Length, "drop delta is consumed once");
        }

        [Test]
        public void test_block_table_expected_definition_orientation_controller_ranges_and_bag()
        {
            var f = new WorldFixture();
            BlockRef block = f.Place(f.Machine, 5, 6, orientation: 2);
            f.Writer.SetController(block, new CellRef(7, 8));

            Assert.AreEqual(f.Machine.Index, f.View.DefOf(block).Index);
            Assert.AreEqual(2, f.View.OrientationOf(block));
            Assert.AreEqual(new CellRef(5, 6), f.View.AnchorOf(block));
            Assert.AreEqual(new CellRef(7, 8), f.View.ControllerOf(block));
            Assert.Greater(f.View.SlotCount(block), 0);
            Assert.Greater(f.View.PortCount(block), 0);
            Assert.IsTrue(f.View.BagOf(block).IsEmpty);
        }

        [Test]
        public void test_structural_change_expected_dirty_chunk_bits()
        {
            var f = new WorldFixture(64, 64);
            Assert.AreEqual(0, f.State.DirtyChunkCount());
            f.Place(f.Chest, 0, 0);
            f.Place(f.Chest, 40, 40);
            Assert.AreEqual(2, f.State.DirtyChunkCount());
            WorldSnapshotDelta delta = f.Writer.CaptureDirtyChunkViews();
            Assert.AreEqual(2, delta.ChangedChunks.Count);
            Assert.AreEqual(0, f.State.DirtyChunkCount());
        }

        [Test]
        public void test_snapshot_delta_expected_immutable_changed_chunk_views()
        {
            var f = new WorldFixture(20, 20);
            BlockRef a = f.Place(f.Chest, 17, 19);
            WorldSnapshotDelta first = f.Writer.CaptureDirtyChunkViews();
            Assert.AreEqual(1UL, first.Revision);
            Assert.AreEqual(1, first.ChangedChunks.Count);
            WorldChunkView chunk = first.ChangedChunks[0];
            Assert.AreEqual(1, chunk.ChunkX);
            Assert.AreEqual(1, chunk.ChunkY);
            Assert.AreEqual(4, chunk.Width);
            Assert.AreEqual(4, chunk.Height);
            Assert.AreEqual(a, chunk.BlockAtLocal(1, 3));
            Assert.AreEqual(f.Chest.Index, chunk.DefinitionAtLocal(1, 3).Index);
            Assert.AreEqual(0, chunk.OrientationAtLocal(1, 3));
            Assert.IsTrue(chunk.ControllerAtLocal(1, 3).IsNone);

            Assert.AreEqual(ReasonCode.None, f.Writer.Rotate(a, 3));
            WorldSnapshotDelta second = f.Writer.CaptureDirtyChunkViews();
            Assert.AreEqual(2UL, second.Revision);
            Assert.AreEqual(3, second.ChangedChunks[0].OrientationAtLocal(1, 3));
            Assert.AreEqual(0, chunk.OrientationAtLocal(1, 3), "prior orientation remains immutable");

            Assert.AreEqual(ReasonCode.None, f.Writer.Remove(a, out _));
            WorldSnapshotDelta third = f.Writer.CaptureDirtyChunkViews();
            Assert.AreEqual(3UL, third.Revision);
            Assert.IsTrue(third.ChangedChunks[0].BlockAtLocal(1, 3).IsNone);
            Assert.AreEqual(a, chunk.BlockAtLocal(1, 3), "prior chunk view remains immutable");
            WorldSnapshotDelta emptyA = f.Writer.CaptureDirtyChunkViews();
            WorldSnapshotDelta emptyB = f.Writer.CaptureDirtyChunkViews();
            Assert.AreEqual(0, emptyA.ChangedChunks.Count);
            Assert.AreSame(emptyA, emptyB, "steady-state capture reuses the empty delta without allocation");
        }

        [Test]
        public void test_compaction_recycles_dead_indices_expected_reuse()
        {
            var f = new WorldFixture();
            var a = f.Place(f.Chest, 0, 0);
            var b = f.Place(f.Chest, 1, 0);
            f.Writer.Remove(a, out _);
            Assert.IsTrue(f.View.IsAlive(b), "live BlockRef survives within the tick");
            int dirtyBeforeCompact = f.State.DirtyChunkCount();
            int recycled = f.Writer.Compact();
            Assert.AreEqual(1, recycled);
            Assert.AreEqual(dirtyBeforeCompact, f.State.DirtyChunkCount(), "compaction must not discard pending chunk deltas");
            var c = f.Place(f.Chest, 2, 0);
            Assert.AreEqual(a.Index, c.Index, "dead index is reused after compaction");
            Assert.IsTrue(f.View.IsAlive(b));
        }

        [Test]
        public void test_snapshot_dirty_rebuild_512_expected_under_0_2_ms()
        {
            var f = new WorldFixture(512, 512);
            var blocks = new BlockRef[10];
            for (int i = 0; i < blocks.Length; i++) blocks[i] = f.Place(f.Chest, i * 40, i * 40);
            f.Writer.CaptureDirtyChunkViews(); // JIT and allocation warm-up

            const int repetitions = 100;
            var sw = new Stopwatch();
            for (int rep = 0; rep < repetitions; rep++)
            {
                for (int i = 0; i < blocks.Length; i++) f.Writer.Rotate(blocks[i], rep + i);
                sw.Start();
                WorldSnapshotDelta delta = f.Writer.CaptureDirtyChunkViews();
                sw.Stop();
                Assert.AreEqual(10, delta.ChangedChunks.Count);
            }
            double perRebuild = sw.Elapsed.TotalMilliseconds / repetitions;
            TestContext.WriteLine($"10 dirty chunk rebuild={perRebuild:F4} ms");
            Assert.Less(perRebuild, 0.2);
        }
    }
}

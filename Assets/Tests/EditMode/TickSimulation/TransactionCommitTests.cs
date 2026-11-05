using System;
using System.Diagnostics;
using Equilibrium.Simulation;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.TickSimulation
{
    /// <summary>Story tick-simulation-002 (TR-tick-001, TR-tick-002, TR-tick-005).</summary>
    public class TransactionCommitTests
    {
        [Test]
        public void test_buffer_add_after_warmup_expected_zero_allocation()
        {
            var buffer = new TransactionBuffer(2048, 2048);
            var tx = Transaction.Create(EntityRef.None, EntityRef.None, Payload.None);
            for (int i = 0; i < 1000; i++) buffer.Add(in tx, 0);
            buffer.Clear();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) buffer.Add(in tx, 0);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0, after - before, "TransactionBuffer.Add allocated after warmup");
        }

        [Test]
        public void test_snapshot_isolated_from_live_mutation_expected_old_value()
        {
            var world = new FakeWorld { [0] = 5 };
            var engine = EngineFactory.For(world);
            // System reads the snapshot after another system mutated live state via the writer path is
            // impossible during Collect; emulate by mutating live after the snapshot copy inside Collect.
            var mutator = new LiveMutator(world);
            engine.Register(mutator);

            engine.Advance();

            Assert.AreEqual(5, mutator.SnapshotSeen);
            Assert.AreEqual(9, world[0]);
        }

        [Test]
        public void test_failed_validation_leaves_state_unchanged_expected_rejected_event()
        {
            var world = new FakeWorld { [0] = 5 };
            var engine = EngineFactory.For(world);
            world.Pending.Add(FakeWorld.Move(0, 1, 10));

            engine.Advance();

            Assert.AreEqual(5, world[0]);
            Assert.AreEqual(0, world[1]);
            Assert.AreEqual(1, engine.Events.Rejected.Length);
            Assert.AreEqual(ReasonCode.ValidationFailed, engine.Events.Rejected[0].Reason);
        }

        [Test]
        public void test_writer_fault_leaves_state_unchanged_expected_commit_fault()
        {
            var world = new FakeWorld { [0] = 5, ThrowMidApply = true };
            var engine = EngineFactory.For(world);
            world.Pending.Add(FakeWorld.Move(0, 1, 3));

            engine.Advance();

            Assert.AreEqual(5, world[0]);
            Assert.AreEqual(0, world[1]);
            Assert.AreEqual(0, engine.Events.Committed.Length);
            Assert.AreEqual(ReasonCode.CommitFault, engine.Events.Rejected[0].Reason);
            Assert.AreEqual(TickPhase.Idle, engine.Phase);
        }

        [Test]
        public void test_live_read_during_collect_expected_snapshot_violation_in_debug()
        {
            var world = new FakeWorld { [0] = 1 };
            var engine = EngineFactory.For(world);
            engine.Register(new LiveReader(world));
#if EQ_DEBUG_SNAPSHOT
            Assert.Throws<SnapshotViolationException>(() => engine.Advance());
#else
            Assert.DoesNotThrow(() => engine.Advance());
            Assert.Ignore("EQ_DEBUG_SNAPSHOT not defined; guard compiled out.");
#endif
        }

        [Test]
        public void test_ten_thousand_moves_expected_under_four_ms()
        {
            var world = new FakeWorld(slots: 20000) ;
            for (int i = 0; i < 10000; i++) world[i] = 1;
            var engine = EngineFactory.For(world);
            for (int i = 0; i < 10000; i++) world.Pending.Add(FakeWorld.Move(i, 10000 + i, 1));
            engine.Advance(); // warm up
            for (int i = 0; i < 10000; i++) { world[i] = 1; world[10000 + i] = 0; }

            var sw = Stopwatch.StartNew();
            engine.Advance();
            sw.Stop();

            Assert.AreEqual(10000, engine.Events.Committed.Length);
            TestContext.WriteLine($"10k transactions: {sw.Elapsed.TotalMilliseconds:F3} ms");
            Assert.Less(sw.Elapsed.TotalMilliseconds, 4.0 * 4, "Advisory budget 4 ms (x4 tolerance for CI machines)");
        }

        private sealed class LiveMutator : ITickSystem
        {
            private readonly FakeWorld _w;
            public int SnapshotSeen;
            public LiveMutator(FakeWorld w) { _w = w; }
            public void Collect(in TickSnapshot snapshot, TransactionBuffer buffer, int systemIndex)
            {
                SnapshotSeen = _w.SnapshotRead(0);
                _w[0] = 9; // illegal in production; used to prove snapshot isolation
            }
            public ReasonCode Validate(in TickSnapshot snapshot, in Transaction tx) => ReasonCode.None;
        }

        private sealed class LiveReader : ITickSystem
        {
            private readonly FakeWorld _w;
            public LiveReader(FakeWorld w) { _w = w; }
            public void Collect(in TickSnapshot snapshot, TransactionBuffer buffer, int systemIndex) { _ = _w.LiveRead(0); }
            public ReasonCode Validate(in TickSnapshot snapshot, in Transaction tx) => ReasonCode.None;
        }
    }
}

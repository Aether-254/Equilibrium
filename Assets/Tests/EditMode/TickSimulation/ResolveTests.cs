using System.Linq;
using Equilibrium.Simulation;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.TickSimulation
{
    /// <summary>Story tick-simulation-003 (TR-tick-002, TR-tick-003).</summary>
    public class ResolveTests
    {
        [Test]
        public void test_equal_priority_contention_expected_exactly_one_commits()
        {
            // A(0) and C(2) both move 1 into B(1), which has capacity 1.
            var world = new FakeWorld(slots: 3) { [0] = 1, [2] = 1 };
            world.SetCapacity(1, 1);
            var engine = EngineFactory.For(world, new FixedTieBreakSource(50, 10)); // second draw smaller => C wins
            world.Pending.Add(FakeWorld.Move(0, 1, 1));
            world.Pending.Add(FakeWorld.Move(2, 1, 1));

            engine.Advance();

            Assert.AreEqual(1, world[1]);
            Assert.AreEqual(1, engine.Events.Committed.Length);
            Assert.AreEqual(1, engine.Events.Rejected.Length);
            Assert.AreEqual(ReasonCode.Contended, engine.Events.Rejected[0].Reason);
            Assert.AreEqual(0, world[2], "C should have won with the smaller tie-break key");
            Assert.AreEqual(1, world[0]);
        }

        [Test]
        public void test_priority_beats_tie_break_expected_high_priority_commits()
        {
            var world = new FakeWorld(slots: 3) { [0] = 1, [2] = 1 };
            world.SetCapacity(1, 1);
            var engine = EngineFactory.For(world, new FixedTieBreakSource(1, 99)); // would favor A by key
            world.Pending.Add(FakeWorld.Move(0, 1, 1, priority: 0));
            world.Pending.Add(FakeWorld.Move(2, 1, 1, priority: 5));

            engine.Advance();

            Assert.AreEqual(0, world[2]);
            Assert.AreEqual(1, world[0]);
        }

        [Test]
        public void test_chain_moves_in_one_tick_expected_dependency_ordered_commit()
        {
            // A -> B -> C with A=100, B=200, C empty; each cell holds one group (capacity 1 group => model as count capacity 200).
            var world = new FakeWorld(slots: 3) { [0] = 100, [1] = 200 };
            world.SetCapacity(1, 200);
            world.SetCapacity(2, 200);
            var engine = EngineFactory.For(world);
            world.Pending.Add(FakeWorld.Move(1, 2, 200)); // idx 0: B->C
            world.Pending.Add(FakeWorld.Move(0, 1, 100)); // idx 1: A->B depends on B->C
            world.PendingDeps.Add((1, 0));

            engine.Advance();

            Assert.AreEqual(0, world[0]);
            Assert.AreEqual(100, world[1]);
            Assert.AreEqual(200, world[2]);
            Assert.AreEqual(2, engine.Events.Committed.Length);
        }

        [Test]
        public void test_dependency_cycle_expected_both_rejected_transaction_cycle()
        {
            var world = new FakeWorld(slots: 2) { [0] = 5, [1] = 5 };
            var engine = EngineFactory.For(world);
            world.Pending.Add(FakeWorld.Move(0, 1, 1));
            world.Pending.Add(FakeWorld.Move(1, 0, 1));
            world.PendingDeps.Add((0, 1));
            world.PendingDeps.Add((1, 0));

            Assert.DoesNotThrow(() => engine.Advance());

            var reasons = engine.Events.Rejected.ToArray().Select(r => r.Reason).ToArray();
            Assert.AreEqual(2, reasons.Length);
            Assert.That(reasons, Is.All.EqualTo(ReasonCode.TransactionCycle));
            Assert.AreEqual(5, world[0]);
            Assert.AreEqual(5, world[1]);
        }

        [Test]
        public void test_dependency_on_failed_transaction_expected_dependency_failed()
        {
            var world = new FakeWorld(slots: 3) { [0] = 0, [1] = 5 };
            var engine = EngineFactory.For(world);
            world.Pending.Add(FakeWorld.Move(0, 2, 1)); // fails: source empty
            world.Pending.Add(FakeWorld.Move(1, 2, 1)); // depends on the failing one
            world.PendingDeps.Add((1, 0));

            engine.Advance();

            var rejected = engine.Events.Rejected.ToArray();
            Assert.AreEqual(2, rejected.Length);
            Assert.That(rejected.Select(r => r.Reason), Does.Contain(ReasonCode.ValidationFailed));
            Assert.That(rejected.Select(r => r.Reason), Does.Contain(ReasonCode.DependencyFailed));
            Assert.AreEqual(5, world[1]);
        }

        [Test]
        public void test_contention_loser_cascades_to_dependents_expected_dependency_failed()
        {
            // X and Y contend for slot 1 (cap 1). Z depends on Y. Fixed keys make X win.
            var world = new FakeWorld(slots: 5) { [0] = 1, [2] = 1, [3] = 1 };
            world.SetCapacity(1, 1);
            var engine = EngineFactory.For(world, new FixedTieBreakSource(1, 2));
            world.Pending.Add(FakeWorld.Move(0, 1, 1)); // X
            world.Pending.Add(FakeWorld.Move(2, 1, 1)); // Y
            world.Pending.Add(FakeWorld.Move(3, 4, 1)); // Z depends on Y
            world.PendingDeps.Add((2, 1));

            engine.Advance();

            Assert.AreEqual(1, engine.Events.Committed.Length);
            var reasons = engine.Events.Rejected.ToArray().Select(r => r.Reason).ToArray();
            Assert.That(reasons, Does.Contain(ReasonCode.Contended));
            Assert.That(reasons, Does.Contain(ReasonCode.DependencyFailed));
            Assert.AreEqual(1, world[3]);
        }

        [Test]
        public void test_long_dependency_chain_expected_warning_flag_and_completion()
        {
            const int n = 1200;
            var world = new FakeWorld(slots: n + 1);
            for (int i = 0; i < n; i++) world[i] = 1;
            var engine = EngineFactory.For(world);
            engine.DependencyWarnThreshold = 1000;
            for (int i = 0; i < n; i++) world.Pending.Add(FakeWorld.Move(i, i + 1, 1));
            for (int i = 0; i < n - 1; i++) world.PendingDeps.Add((i, i + 1)); // i depends on i+1 (downstream first)

            engine.Advance();

            Assert.AreEqual(n, engine.Events.Committed.Length);
            Assert.IsTrue(engine.LastTickHadDependencyWarning);
            Assert.GreaterOrEqual(engine.LastDependencyChainLength, 1000);
        }
    }
}

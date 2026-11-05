using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Equilibrium.Simulation;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.TickSimulation
{
    /// <summary>Story tick-simulation-005 (TR-tick-004, TR-tick-001).</summary>
    public class EventBusAndBoundaryTests
    {
        [Test]
        public void RestoredTickContinuesEventAndBoundaryNumbersWithoutRewindingALiveEngine()
        {
            var engine = EngineFactory.For(new FakeWorld());
            ulong boundary = 0;
            engine.TickBoundary += tick =>
            {
                boundary = tick;
                Assert.Throws<System.InvalidOperationException>(() => engine.RestoreTickIndex(0));
            };
            Assert.Throws<System.ArgumentOutOfRangeException>(() => engine.RestoreTickIndex(ulong.MaxValue));
            engine.RestoreTickIndex(42);
            engine.Advance();
            Assert.AreEqual(43UL, engine.TickIndex);
            Assert.AreEqual(43UL, engine.Events.TickIndex);
            Assert.AreEqual(43UL, boundary);
            Assert.Throws<System.InvalidOperationException>(() => engine.RestoreTickIndex(0));
        }

        [Test]
        public void test_events_one_per_transaction_expected_counts_and_reasons()
        {
            var world = new FakeWorld(slots: 6) { [0] = 1, [1] = 1, [2] = 1, [3] = 0, [4] = 0 };
            var engine = EngineFactory.For(world);
            world.Pending.Add(FakeWorld.Move(0, 5, 1));
            world.Pending.Add(FakeWorld.Move(1, 5, 1));
            world.Pending.Add(FakeWorld.Move(2, 5, 1));
            world.Pending.Add(FakeWorld.Move(3, 5, 1)); // fails: empty
            world.Pending.Add(FakeWorld.Move(4, 5, 1)); // fails: empty

            engine.Advance();

            Assert.AreEqual(3, engine.Events.Committed.Length);
            Assert.AreEqual(2, engine.Events.Rejected.Length);
            Assert.That(engine.Events.Rejected.ToArray().Select(r => r.Reason), Is.All.EqualTo(ReasonCode.ValidationFailed));
            Assert.AreEqual(1UL, engine.Events.TickIndex);
        }

        [Test]
        public void test_boundary_hook_sees_post_commit_state_expected_once_after_emit()
        {
            var world = new FakeWorld { [0] = 5 };
            var engine = EngineFactory.For(world);
            world.Pending.Add(FakeWorld.Move(0, 1, 2));
            int calls = 0; int seenB = -1;
            engine.TickBoundary += t => { calls++; seenB = world[1]; };

            engine.Advance();

            Assert.AreEqual(1, calls);
            Assert.AreEqual(2, seenB);
        }

        [Test]
        public void test_events_replay_reconstructs_final_state_expected_equal()
        {
            var world = new FakeWorld(slots: 4) { [0] = 10, [1] = 5 };
            var engine = EngineFactory.For(world);
            world.Pending.Add(FakeWorld.Move(0, 2, 3));
            world.Pending.Add(FakeWorld.Move(1, 3, 5));
            world.Pending.Add(FakeWorld.Move(0, 3, 20)); // rejected
            int[] initial = { 10, 5, 0, 0 };

            engine.Advance();

            var replay = (int[])initial.Clone();
            foreach (var ev in engine.Events.Committed.ToArray())
            {
                replay[ev.Source.Index] -= ev.Payload.B;
                replay[ev.Target.Index] += ev.Payload.B;
            }
            CollectionAssert.AreEqual(new[] { world[0], world[1], world[2], world[3] }, replay);
        }

        [Test]
        public void test_events_cleared_at_next_tick_expected_zero_when_idle()
        {
            var world = new FakeWorld { [0] = 5 };
            var engine = EngineFactory.For(world);
            world.Pending.Add(FakeWorld.Move(0, 1, 1));
            engine.Advance();
            Assert.AreEqual(1, engine.Events.Committed.Length);
            world.Pending.Clear();
            engine.Advance();
            Assert.AreEqual(0, engine.Events.Committed.Length);
            Assert.AreEqual(2UL, engine.Events.TickIndex);
        }

        [Test]
        public void test_simulation_assembly_has_no_public_mutable_state_fields_expected_none()
        {
#if EQ_PURE
            Assert.Ignore("Assembly audit only meaningful under Unity asmdef separation.");
#endif
            var asm = typeof(TickEngine).Assembly;
            var offenders = new List<string>();
            foreach (var t in asm.GetTypes())
            {
                if (!t.IsClass || t.IsNested && !t.IsNestedPublic) continue;
                if (!t.IsPublic) continue;
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!f.IsInitOnly && !t.IsValueType) offenders.Add($"{t.FullName}.{f.Name}");
                }
            }
            Assert.IsEmpty(offenders, "Public mutable instance fields on classes in Equilibrium.Simulation: " + string.Join(", ", offenders));
        }

        [Test]
        public void test_world_writer_is_only_commit_path_expected_eventbus_mutators_internal()
        {
            var bus = typeof(EventBus);
            var publicMutators = bus.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name.StartsWith("Add") || m.Name == "Begin").ToArray();
            Assert.IsEmpty(publicMutators, "EventBus mutators must be internal to the engine");
        }
    }
}

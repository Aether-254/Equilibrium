using System;
using System.Linq;
using Equilibrium.Presentation;
using Equilibrium.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Equilibrium.Tests.EditMode.TickSimulation
{
    /// <summary>Story tick-simulation-001 acceptance criteria (TR-tick-001, TR-tick-004).</summary>
    public class TickEngineSkeletonTests
    {
        [Test]
        public void test_pure_assemblies_have_no_unityengine_reference_expected_true()
        {
            var pure = new[] { typeof(TickEngine).Assembly, typeof(Equilibrium.Content.AssemblyMarker).Assembly, typeof(Equilibrium.Editing.AssemblyMarker).Assembly };
            foreach (var asm in pure)
            {
                var refs = asm.GetReferencedAssemblies().Select(a => a.Name).ToArray();
                Assert.IsFalse(refs.Any(r => r.StartsWith("UnityEngine", StringComparison.Ordinal)),
                    $"{asm.GetName().Name} references UnityEngine: {string.Join(",", refs)}");
                Assert.IsFalse(refs.Any(r => r.StartsWith("UnityEditor", StringComparison.Ordinal)));
            }
        }

        [Test]
        public void test_advance_runs_phases_in_order_expected_sequence()
        {
            var world = new FakeWorld();
            var engine = EngineFactory.For(world);
            var probe = new PhaseProbe(engine);
            engine.Register(probe);
            var listener = new RecordingListener(engine);
            engine.AddListener(listener);

            engine.Advance();

            Assert.AreEqual(1UL, engine.TickIndex);
            Assert.AreEqual(TickPhase.Idle, engine.Phase);
            CollectionAssert.AreEqual(new[] { TickPhase.Collect, TickPhase.Validate }, probe.Observed);
            CollectionAssert.AreEqual(new[] { TickPhase.Emit }, listener.PhasesAtCallback);
        }

        [Test]
        public void test_advance_increments_tick_index_expected_n()
        {
            var engine = EngineFactory.For(new FakeWorld());
            engine.Advance(5);
            Assert.AreEqual(5UL, engine.TickIndex);
        }

        [Test]
        public void test_committed_transaction_applies_through_writer_expected_moved()
        {
            var world = new FakeWorld { [0] = 10 };
            var engine = EngineFactory.For(world);
            world.Pending.Add(FakeWorld.Move(0, 1, 4));

            engine.Advance();

            Assert.AreEqual(6, world[0]);
            Assert.AreEqual(4, world[1]);
            Assert.AreEqual(1, engine.Events.Committed.Length);
            Assert.AreEqual(0, engine.Events.Rejected.Length);
        }

        [Test]
        public void test_tie_break_begin_tick_called_once_per_tick_expected_count()
        {
            var tie = new FixedTieBreakSource(7);
            var engine = EngineFactory.For(new FakeWorld(), tie);
            engine.Advance(3);
            Assert.AreEqual(3, tie.BeginTickCalls);
        }

        [Test]
        public void test_tick_boundary_fires_after_emit_expected_once_per_tick()
        {
            var engine = EngineFactory.For(new FakeWorld());
            int fired = 0;
            ulong lastTick = 0;
            engine.TickBoundary += t => { fired++; lastTick = t; Assert.AreEqual(TickPhase.Emit, engine.Phase); };
            engine.Advance(2);
            Assert.AreEqual(2, fired);
            Assert.AreEqual(2UL, lastTick);
        }

        [Test]
        public void test_register_during_advance_throws_expected_invalid_operation()
        {
            var engine = EngineFactory.For(new FakeWorld());
            engine.Register(new ReentrantRegisterSystem(engine));
            Assert.Throws<InvalidOperationException>(() => engine.Advance());
        }

        [Test]
        public void test_tickpump_advances_whole_ticks_expected_two_with_carry()
        {
            var engine = EngineFactory.For(new FakeWorld());
            var go = new GameObject("pump");
            try
            {
                var pump = go.AddComponent<TickPump>();
                pump.Attach(engine);
                pump.TimeScale = 2.5f;

                int ticks = pump.Step(1.0f);

                Assert.AreEqual(2, ticks);
                Assert.AreEqual(2UL, engine.TickIndex);
                Assert.AreEqual(0.5f, pump.Interpolation, 1e-5f);

                pump.Paused = true;
                Assert.AreEqual(0, pump.Step(10f));
                Assert.AreEqual(2UL, engine.TickIndex);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private sealed class PhaseProbe : ITickSystem
        {
            public readonly System.Collections.Generic.List<TickPhase> Observed = new System.Collections.Generic.List<TickPhase>();
            private readonly TickEngine _engine;
            public PhaseProbe(TickEngine engine) { _engine = engine; }
            public void Collect(in TickSnapshot snapshot, TransactionBuffer buffer, int systemIndex)
            {
                Observed.Add(_engine.Phase);
                buffer.Add(Transaction.Create(EntityRef.None, EntityRef.None, Payload.None, validator: 0), systemIndex);
            }
            public ReasonCode Validate(in TickSnapshot snapshot, in Transaction tx)
            {
                if (Observed[Observed.Count - 1] != _engine.Phase) Observed.Add(_engine.Phase);
                return ReasonCode.None;
            }
        }

        private sealed class ReentrantRegisterSystem : ITickSystem
        {
            private readonly TickEngine _engine;
            public ReentrantRegisterSystem(TickEngine engine) { _engine = engine; }
            public void Collect(in TickSnapshot snapshot, TransactionBuffer buffer, int systemIndex) => _engine.Register(this);
            public ReasonCode Validate(in TickSnapshot snapshot, in Transaction tx) => ReasonCode.None;
        }
    }
}

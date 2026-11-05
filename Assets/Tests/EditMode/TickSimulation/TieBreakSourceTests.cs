using System;
using System.Linq;
using Equilibrium.Simulation;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.TickSimulation
{
    /// <summary>Story tick-simulation-004 (TR-tick-003).</summary>
    public class TieBreakSourceTests
    {
        [Test]
        public void test_two_sources_same_tick_expected_different_sequences()
        {
            var a = new TieBreakSource();
            var b = new TieBreakSource();
            a.BeginTick(1); b.BeginTick(1);
            var ka = Enumerable.Range(0, 1000).Select(_ => a.NextKey()).ToArray();
            var kb = Enumerable.Range(0, 1000).Select(_ => b.NextKey()).ToArray();
            CollectionAssert.AreNotEqual(ka, kb);
        }

        [Test]
        public void test_same_source_two_ticks_expected_different_sequences()
        {
            var s = new TieBreakSource();
            s.BeginTick(1);
            var k1 = Enumerable.Range(0, 100).Select(_ => s.NextKey()).ToArray();
            s.BeginTick(1);
            var k2 = Enumerable.Range(0, 100).Select(_ => s.NextKey()).ToArray();
            CollectionAssert.AreNotEqual(k1, k2, "fresh OS entropy per BeginTick, even for the same tick index");
        }

        [Test]
        public void test_two_way_contention_fairness_expected_between_48_and_52_percent()
        {
            var s = new TieBreakSource();
            int aWins = 0;
            const int trials = 10000;
            for (int i = 0; i < trials; i++)
            {
                s.BeginTick((ulong)i);
                ulong ka = s.NextKey(), kb = s.NextKey();
                if (ka < kb) aWins++;
            }
            double share = aWins / (double)trials;
            TestContext.WriteLine($"A wins {share:P2}");
            Assert.That(share, Is.InRange(0.48, 0.52));
        }

        [Test]
        public void test_entropy_failure_twice_expected_unavailable_exception()
        {
            var s = new TieBreakSource(new FailingEntropy(failures: 2));
            Assert.Throws<TieBreakEntropyUnavailableException>(() => s.BeginTick(1));
        }

        [Test]
        public void test_entropy_failure_once_expected_retry_succeeds()
        {
            var s = new TieBreakSource(new FailingEntropy(failures: 1));
            Assert.DoesNotThrow(() => s.BeginTick(1));
            Assert.AreNotEqual(0UL, s.NextKey() | s.NextKey());
        }

        [Test]
        public void test_engine_halts_when_entropy_unavailable_expected_no_commit()
        {
            var world = new FakeWorld { [0] = 3 };
            var engine = new TickEngine(world, world, new TieBreakSource(new FailingEntropy(2)), world);
            engine.Register(world);
            world.Pending.Add(FakeWorld.Move(0, 1, 1));

            Assert.Throws<TieBreakEntropyUnavailableException>(() => engine.Advance());
            Assert.AreEqual(3, world[0]);
            Assert.AreEqual(0UL, engine.TickIndex);
            Assert.AreEqual(TickPhase.Idle, engine.Phase);
        }

        [Test]
        public void test_production_assemblies_expose_single_tiebreak_implementation_expected_one()
        {
#if EQ_PURE
            Assert.Ignore("Assembly audit only meaningful under Unity asmdef separation.");
#endif
            var prodAssemblies = new[] { typeof(TickEngine).Assembly, typeof(Equilibrium.Editing.AssemblyMarker).Assembly, typeof(Equilibrium.Content.AssemblyMarker).Assembly };
            var impls = prodAssemblies.SelectMany(a => a.GetTypes())
                .Where(t => typeof(ITieBreakSource).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Select(t => t.FullName).ToArray();
            CollectionAssert.AreEquivalent(new[] { typeof(TieBreakSource).FullName }, impls);
        }

        [Test]
        public void test_editing_assembly_does_not_reference_tiebreak_expected_absent()
        {
#if EQ_PURE
            Assert.Ignore("Assembly audit only meaningful under Unity asmdef separation.");
#endif
            var editing = typeof(Equilibrium.Editing.AssemblyMarker).Assembly;
            var referenced = editing.GetTypes()
                .SelectMany(t => t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Select(f => f.FieldType)
                    .Concat(t.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly)
                        .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))))
                .Distinct().ToArray();
            Assert.IsFalse(referenced.Any(t => t == typeof(ITieBreakSource) || t == typeof(TieBreakSource)),
                "Equilibrium.Editing must never use the tie-break source (deterministic previews).");
        }

        private sealed class FailingEntropy : IEntropyProvider
        {
            private int _remaining;
            public FailingEntropy(int failures) { _remaining = failures; }
            public void Fill(Span<byte> buffer)
            {
                if (_remaining-- > 0) throw new InvalidOperationException("entropy down");
                for (int i = 0; i < buffer.Length; i++) buffer[i] = (byte)(i * 31 + 7);
            }
        }
    }
}

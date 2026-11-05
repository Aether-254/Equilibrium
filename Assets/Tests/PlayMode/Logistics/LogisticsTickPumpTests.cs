using System;
using System.Collections;
using Equilibrium.Content;
using Equilibrium.Presentation;
using Equilibrium.Simulation;
using Equilibrium.Simulation.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Equilibrium.Tests.PlayMode.Logistics
{
    public sealed class LogisticsTickPumpTests
    {
        [UnityTest]
        public IEnumerator RuntimePumpAdvancesRealLogisticsAndStopsWhilePaused()
        {
            var registry = new ContentRegistry();
            new ContentLoader().Load(registry, Tier1Content.Build(), Array.Empty<ContentPack>());
            var wood = registry.Items.Parse("wood");
            var defs = new BlockDefTable(); var belt = defs.Register(BlockDefinition.Belt("belt", -1));
            var writer = new WorldWriter(new WorldState(3, 1, registry), defs);
            writer.Place(belt, new CellRef(0, 0), 0, null, out var a);
            writer.Place(belt, new CellRef(1, 0), 0, null, out var b);
            writer.Place(belt, new CellRef(2, 0), 0, null, out var c);
            writer.Insert(writer.View.SlotGlobal(a, 0), writer.View.SlotGlobal(a, 0) + 1, wood, 16);
            var logistics = new LogisticsSystem(writer, defs);
            var engine = new TickEngine(writer.View, writer, new TieBreakSource()); logistics.Attach(engine);
            var host = new GameObject("LogisticsTickPumpTest");
            try
            {
                var pump = host.AddComponent<TickPump>(); pump.MaxTicksPerFrame = 1; pump.Attach(engine);
                float deadline = Time.realtimeSinceStartup + 5;
                while (engine.TickIndex == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                pump.Paused = true;
                Assert.AreEqual(1UL, engine.TickIndex);
                Assert.AreEqual(0, writer.View.SlotCountOf(writer.View.SlotGlobal(a, 0)));
                Assert.AreEqual(16, writer.View.SlotCountOf(writer.View.SlotGlobal(b, 0)));
                Assert.AreEqual(0, writer.View.SlotCountOf(writer.View.SlotGlobal(c, 0)));
                yield return null;
                Assert.AreEqual(1UL, engine.TickIndex);
                pump.Paused = false; pump.Step(1); pump.Paused = true;
                Assert.AreEqual(16, writer.View.SlotCountOf(writer.View.SlotGlobal(c, 0)));
                Assert.IsTrue(logistics.LastBatchSucceeded);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}

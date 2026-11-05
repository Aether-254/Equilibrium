using System;
using System.Collections.Generic;
using Equilibrium.Content;
using Equilibrium.Editing;
using Equilibrium.Simulation;
using Equilibrium.Simulation.World;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.FactoryEditing
{
    public sealed class MachineProcessingTests
    {
        private sealed class RandomSource : ITieBreakSource
        {
            private ulong _key;
            public void BeginTick(ulong tickIndex) { _key = tickIndex; }
            public ulong NextKey() => ++_key;
        }
        private sealed class Rig
        {
            public readonly ContentRegistry Registry = new ContentRegistry();
            public readonly BlockDefTable Definitions = new BlockDefTable();
            public readonly WorldWriter Writer;
            public WorldView World => Writer.View;
            public readonly LogisticsSystem Logistics;
            public readonly ProcessingSystem Processing;
            public readonly TickEngine Tick;
            public readonly BlockDefHandle SawDef, EngineDef, ShaftDef;
            public BlockRef Saw, Engine;
            public Rig(Action<ContentPack> configure = null, int shafts = 0, int inputSlots = 1, bool byproducts = false)
            {
                var pack = Tier1Content.Build(); configure?.Invoke(pack);
                Assert.IsFalse(new ContentLoader().Load(Registry, pack, Array.Empty<ContentPack>()).BaseRejected);
                var saw = BlockDefinition.Machine("saw", Id("saw").Handle, 64, 64, byproducts ? 64 : 0);
                for (int i = 1; i < inputSlots; i++) saw.Containers[0].Slot("in_" + i, 64);
                SawDef = Definitions.Register(saw);
                EngineDef = Definitions.Register(BlockDefinition.FuelEngine("engine", Id("engine").Handle));
                ShaftDef = Definitions.Register(BlockDefinition.Shaft("shaft", Id("shaft").Handle));
                Writer = new WorldWriter(new WorldState(40, 40, Registry), Definitions);
                Processing = new ProcessingSystem(Writer, Definitions); Logistics = new LogisticsSystem(Writer, Definitions, Processing);
                Tick = new TickEngine(World, Writer, new RandomSource()); Logistics.Attach(Tick); Processing.Attach(Tick, Logistics);
                Writer.Place(SawDef, new CellRef(4, 2), 0, null, out Saw);
                for (int i = 0; i < shafts; i++) Writer.Place(ShaftDef, new CellRef(4, 3 + i), 0, null, out _);
                Writer.Place(EngineDef, new CellRef(4, 3 + shafts), 0, null, out Engine);
            }
            public ItemId Id(string item) => Registry.Items.Parse(item);
            public int Slot(BlockRef b, string container)
            {
                for (int i = 0; i < World.SlotCount(b); i++) { int g = World.SlotGlobal(b, i); if (World.ContainerKeyOf(g) == container) return g; }
                throw new InvalidOperationException(container);
            }
            public void Put(BlockRef b, string container, string item, int count)
            { int g = Slot(b, container); Assert.AreEqual(count, Writer.Insert(g, g + 1, Id(item), count)); }
            public int Count(BlockRef b, string container) => World.SlotCountOf(Slot(b, container));
            public void Run(int ticks = 1)
            {
                for (int i = 0; i < ticks; i++)
                {
                    Tick.Advance();
                    foreach (var rejected in Tick.Events.Rejected) Assert.Fail("Transaction rejected: " + rejected.Reason);
                }
            }
            public MachineState State => Processing.State(Saw);
        }

        [Test]
        public void SawReservesWithoutConsumingAndCompletesOnTenthPoweredTick()
        {
            var r = new Rig(p => p.Items.Find(i => i.Id == "engine").Extras["omega_src"] = "4.516666666666667");
            r.Put(r.Saw, "input", "log", 1); r.Put(r.Engine, "fuel", "wood", 2); r.Run();
            Assert.AreEqual(MachinePhase.Working, r.State.Phase); Assert.AreEqual(0, r.State.WorkDone);
            Assert.AreEqual(1, r.World.SlotReservedOf(r.Slot(r.Saw, "input")));
            Assert.AreEqual(0, r.Writer.Extract(r.Slot(r.Saw, "input"), r.Slot(r.Saw, "input") + 1, r.Id("log"), 1));
            r.Run(9); Assert.AreEqual(1, r.Count(r.Saw, "input")); Assert.AreEqual(0, r.Count(r.Saw, "output"));
            r.Run(); Assert.AreEqual(0, r.Count(r.Saw, "input")); Assert.AreEqual(2, r.Count(r.Saw, "output"));
            Assert.AreEqual(5, r.State.WorkDone, 1e-8); Assert.AreEqual(1, r.State.CompletedBatches);
        }

        [Test]
        public void NoFuelForHundredTicksPreservesInputReservationAndProgress()
        {
            var r = new Rig(); r.Put(r.Saw, "input", "log", 1); r.Run(101);
            Assert.AreEqual(MachinePhase.Working, r.State.Phase); Assert.AreEqual(ReasonCode.NoFuel, r.State.Reason);
            Assert.AreEqual(0, r.State.WorkDone); Assert.AreEqual(1, r.Count(r.Saw, "input"));
            Assert.AreEqual(1, r.World.SlotReservedOf(r.Slot(r.Saw, "input")));
        }

        [Test]
        public void FullOutputConsumesInputOnceAndRetainsBatchUntilRoomExists()
        {
            var r = new Rig(); r.Put(r.Saw, "input", "log", 3); r.Put(r.Saw, "output", "plank", 64); r.Put(r.Engine, "fuel", "wood", 8);
            r.Run(6); Assert.AreEqual(MachinePhase.Blocked, r.State.Phase); Assert.AreEqual(2, r.Count(r.Saw, "input"));
            Assert.AreEqual(2, r.State.PendingProducts[0].Count); double fuel = r.World.BagOf(r.Engine).FuelDebt;
            r.Run(8); Assert.AreEqual(1, r.State.CompletedBatches); Assert.AreEqual(fuel, r.World.BagOf(r.Engine).FuelDebt);
            int output = r.Slot(r.Saw, "output"); r.Writer.Extract(output, output + 1, r.Id("plank"), 2); r.Run();
            Assert.AreEqual(64, r.Count(r.Saw, "output")); Assert.AreEqual(0, r.State.PendingProducts.Count);
            Assert.AreEqual(1, r.State.CompletedBatches);
        }

        [Test]
        public void ByproductCapacityMakesWholeProductBatchAtomicAndRollIsRetained()
        {
            var r = new Rig(p => p.Recipes[0].Byproducts.Add(new RecipeIoData { Item = "wood", Count = 1, Probability = 0.5f }), byproducts: true);
            r.Put(r.Saw, "input", "log", 1); r.Put(r.Engine, "fuel", "wood", 4); r.Put(r.Saw, "byproduct", "wood", 64);
            r.Run(6); Assert.AreEqual(0, r.Count(r.Saw, "output")); Assert.AreEqual(2, r.State.PendingProducts.Count);
            r.Run(5); Assert.AreEqual(2, r.State.PendingProducts.Count);
            int g = r.Slot(r.Saw, "byproduct"); r.Writer.Extract(g, g + 1, r.Id("wood"), 1); r.Run();
            Assert.AreEqual(2, r.Count(r.Saw, "output")); Assert.AreEqual(64, r.Count(r.Saw, "byproduct"));
        }

        [Test]
        public void RemovingWorkingSawPreservesReservationAndWorkAtANewPosition()
        {
            var r = new Rig(); r.Put(r.Saw, "input", "log", 2); r.Put(r.Engine, "fuel", "wood", 5); r.Run(3);
            double work = r.State.WorkDone; r.Writer.Remove(r.Saw, out var drop);
            Assert.AreEqual(work, drop.Bag.Machine.WorkDone);
            r.Writer.Place(r.SawDef, new CellRef(10, 10), 1, drop.Bag, out r.Saw); r.Run();
            Assert.AreEqual(work, r.State.WorkDone); Assert.AreEqual(1, r.World.OrientationOf(r.Saw));
            Assert.AreEqual(1, r.World.SlotReservedOf(r.Slot(r.Saw, "input")));
            Assert.AreEqual(ReasonCode.NoPowerSource, r.State.Reason);
        }

        [Test]
        public void CapturedBagKeepsReservedSlotsAndWorkDetachedFromLaterTicks()
        {
            var r = new Rig(); r.Put(r.Saw, "input", "log", 2); r.Put(r.Engine, "fuel", "wood", 5); r.Run(3);
            double work = r.State.WorkDone;
            var bag = r.Writer.CaptureBag(r.Saw);
            r.Run();
            Assert.Greater(r.State.WorkDone, work);
            Assert.AreEqual(work, bag.Machine.WorkDone);
            Assert.IsTrue(r.World.IsAlive(r.Saw), "Capturing must not remove the live block.");

            var restored = new WorldWriter(new WorldState(16, 16, r.Registry), r.Definitions);
            Assert.AreEqual(ReasonCode.None, restored.Place(r.SawDef, new CellRef(1, 1), 2, bag, out var saw));
            int input = restored.View.SlotGlobal(saw, 0);
            Assert.AreEqual(r.Id("log").Handle, restored.View.SlotItemHandle(input));
            Assert.AreEqual(2, restored.View.SlotCountOf(input));
            Assert.AreEqual(1, restored.View.SlotReservedOf(input));
            Assert.AreEqual(work, restored.View.BagOf(saw).Machine.WorkDone);
            Assert.AreEqual(1, restored.Extract(input, input + 1, r.Id("log"), 2));
            Assert.AreEqual(0, restored.Extract(input, input + 1, r.Id("log"), 1));
        }

        [TestCase(false, ReasonCode.None)]
        [TestCase(true, ReasonCode.Irreversible)]
        public void CancelHonorsIrreversibility(bool irreversible, ReasonCode expected)
        {
            var r = new Rig(p => p.Recipes[0].Irreversible = irreversible); r.Put(r.Saw, "input", "log", 1); r.Run();
            Assert.AreEqual(expected, r.Writer.CancelMachine(r.Saw));
            Assert.AreEqual(irreversible ? 1 : 0, r.World.SlotReservedOf(r.Slot(r.Saw, "input")));
            Assert.AreEqual(1, r.Count(r.Saw, "input"));
        }

        [Test]
        public void BrokenReservationCannotProduceOrConsumeAnotherInput()
        {
            var r = new Rig(); r.Put(r.Saw, "input", "log", 1); r.Put(r.Engine, "fuel", "wood", 8); r.Run();
            int g = r.Slot(r.Saw, "input"); r.Writer.ConsumeReserved(g, g + 1, r.Id("log"), 1); r.Run();
            Assert.AreEqual(MachinePhase.Starved, r.State.Phase); Assert.AreEqual(ReasonCode.ReservationBroken, r.State.Reason);
            Assert.AreEqual(0, r.Count(r.Saw, "output"));
        }

        [Test]
        public void WorkingAdmissionRejectsWrongItemsAndDisabledBuffering()
        {
            var r = new Rig(p => p.Recipes[0].AcceptWhileWorking = false);
            Assert.AreEqual(ReasonCode.FilterMismatch, r.Processing.Check(r.World, r.Saw, 0, r.Id("wood"), true));
            r.Put(r.Saw, "input", "log", 1); r.Run();
            Assert.AreEqual(ReasonCode.MachineNotAccepting, r.Processing.Check(r.World, r.Saw, 0, r.Id("log"), true));
        }

        [Test]
        public void FuelAccumulatorConsumesOnTenthTickAndIdleDoesNotBurn()
        {
            var r = new Rig(p => { p.Machines[0].TauReq = 20; p.Recipes[0].WorkRequired = 100000; });
            r.Put(r.Engine, "fuel", "wood", 1); r.Run(20); Assert.AreEqual(1, r.Count(r.Engine, "fuel"));
            r.Put(r.Saw, "input", "log", 1); r.Run(); r.Run(9); Assert.AreEqual(1, r.Count(r.Engine, "fuel"));
            r.Run(); Assert.AreEqual(0, r.Count(r.Engine, "fuel")); Assert.AreEqual(2000, r.State.WorkDone, 1e-6);
            r.Run(); Assert.AreEqual(ReasonCode.NoFuel, r.State.Reason); Assert.AreEqual(2000, r.State.WorkDone, 1e-6);
        }

        [Test]
        public void FinalFuelFractionCannotCreateMoreEnergyThanAvailable()
        {
            var r = new Rig(p => p.Recipes[0].WorkRequired = 100000); r.Put(r.Engine, "fuel", "wood", 1); r.Put(r.Saw, "input", "log", 1);
            r.Run(20); Assert.AreEqual(2000, r.State.WorkDone, 1e-6); Assert.AreEqual(0, r.Count(r.Engine, "fuel"));
        }

        [TestCase(29, ReasonCode.Stalled, 0)]
        [TestCase(30, ReasonCode.None, 300)]
        public void TorqueLimitUsesStrictGreaterThan(double limit, ReasonCode reason, double watts)
        {
            var r = new Rig(p => p.Items.Find(i => i.Id == "engine").Extras["tau_max"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture));
            r.Put(r.Engine, "fuel", "wood", 4); r.Put(r.Saw, "input", "log", 1); r.Run(2);
            Assert.AreEqual(reason, r.State.Reason); Assert.AreEqual(watts, r.State.ReceivedWatts);
            if (watts == 0) Assert.AreEqual(0, r.World.BagOf(r.Engine).FuelDebt);
        }

        [Test]
        public void TwentyShaftCellsIncreaseSourceDemandWithOneLossAndDisconnectStopsWork()
        {
            var r = new Rig(shafts: 20); r.Put(r.Saw, "input", "log", 1); r.Put(r.Engine, "fuel", "wood", 8); r.Run(2);
            var source = r.Processing.Power(r.Engine);
            Assert.AreEqual(300, r.State.ReceivedWatts, 1e-6);
            Assert.AreEqual(30 / Math.Pow(0.99, 20), source.SourceDemand, 1e-6);
            Assert.AreEqual(source.Watts * Math.Pow(0.99, 20), r.State.ReceivedWatts, 1e-6);
            double work = r.State.WorkDone; r.Writer.Remove(r.World.BlockAt(new CellRef(4, 12)), out _); r.Run();
            Assert.AreEqual(ReasonCode.NoPowerSource, r.State.Reason); Assert.AreEqual(work, r.State.WorkDone);
        }

        [Test]
        public void OneWoodThroughOneShaftDeliversExactlyOneTransmissionLoss()
        {
            var r = new Rig(p => p.Recipes[0].WorkRequired = 100000, shafts: 1);
            r.Put(r.Saw, "input", "log", 1); r.Put(r.Engine, "fuel", "wood", 1); r.Run(2);
            Assert.AreEqual(300, r.State.ReceivedWatts, 1e-9);
            Assert.AreEqual(300 / 0.99, r.Processing.Power(r.Engine).Watts, 1e-9);
            r.Run(20); Assert.AreEqual(8000 * 0.25 * 0.99, r.State.WorkDone, 1e-6);
            Assert.AreEqual(0, r.Count(r.Engine, "fuel"));
        }

        [TestCase(false, MachinePhase.Starved)]
        [TestCase(true, MachinePhase.Working)]
        public void TagMixingUsesConcreteIdsAndHonorsRecipeFlag(bool mixed, MachinePhase expected)
        {
            var r = new Rig(p => {
                p.Tags.Add(new TagDefData { Name = "#timber", Add = { "wood", "log" } });
                p.Recipes[0].Inputs.Clear(); p.Recipes[0].Inputs.Add(new RecipeIoData { Item = "#timber", Count = 5 });
                p.Recipes[0].AllowMixedTagInputs = mixed;
            }, inputSlots: 2);
            int g = r.Slot(r.Saw, "input"); r.Writer.Insert(g, g + 1, r.Id("wood"), 3); r.Writer.Insert(g + 1, g + 2, r.Id("log"), 2);
            r.Run(); Assert.AreEqual(expected, r.State.Phase);
            if (mixed) Assert.AreEqual(2, r.State.Reservations.Count);
        }

        [Test]
        public void OverlappingTagAndExactInputFindAFeasibleReservation()
        {
            var r = new Rig(p => {
                p.Tags.Add(new TagDefData { Name = "#timber", Add = { "wood", "log" } });
                p.Recipes[0].Inputs.Clear(); p.Recipes[0].Inputs.Add(new RecipeIoData { Item = "#timber", Count = 1 });
                p.Recipes[0].Inputs.Add(new RecipeIoData { Item = "log", Count = 1 });
            }, inputSlots: 2);
            int g = r.Slot(r.Saw, "input"); r.Writer.Insert(g, g + 1, r.Id("log"), 1); r.Writer.Insert(g + 1, g + 2, r.Id("wood"), 1);
            r.Run(); Assert.AreEqual(MachinePhase.Working, r.State.Phase); Assert.AreEqual(2, r.State.Reservations.Count);
        }

        [Test]
        public void HighPowerCompletesAtMostOnceAndCapsCarry()
        {
            var r = new Rig(p => p.Items.Find(i => i.Id == "engine").Extras["omega_src"] = "200");
            r.Put(r.Engine, "fuel", "wood", 16); r.Put(r.Saw, "input", "log", 16); r.Run(2);
            Assert.AreEqual(1, r.State.CompletedBatches); Assert.AreEqual(1350, r.State.WorkDone); Assert.AreEqual(2, r.Count(r.Saw, "output"));
        }

        [Test]
        public void StableRecipeOrderAndPinChooseDifferentProducts()
        {
            var r = new Rig(p => p.Recipes.Add(new RecipeDefData { Id = "alternate", Machine = "saw", WorkRequired = 1,
                Inputs = { new RecipeIoData { Item = "log", Count = 1 } }, Outputs = { new RecipeIoData { Item = "wood", Count = 1 } } }));
            r.Put(r.Saw, "input", "log", 2); r.Run(); Assert.AreEqual("log_to_plank", r.State.RecipeId);
            Assert.AreEqual(ReasonCode.MachineNotAccepting, r.Writer.PinRecipe(r.Saw, "alternate"));
            r.Writer.CancelMachine(r.Saw); Assert.AreEqual(ReasonCode.None, r.Writer.PinRecipe(r.Saw, "alternate"));
            r.Run(); Assert.AreEqual("alternate", r.State.RecipeId);
        }

        [Test]
        public void ZeroWorkCompletesOnlyOnFirstWorkingTickWithoutPower()
        {
            var r = new Rig(p => p.Recipes[0].WorkRequired = 0); r.Put(r.Saw, "input", "log", 2); r.Run();
            Assert.AreEqual(0, r.State.CompletedBatches); r.Run(); Assert.AreEqual(1, r.State.CompletedBatches);
            Assert.AreEqual(2, r.Count(r.Saw, "output")); Assert.AreEqual(1, r.Count(r.Saw, "input"));
        }

        [Test]
        public void UtilizationCountsFortyTwoPoweredTicksOutOfSixty()
        {
            var r = new Rig(p => p.Recipes[0].WorkRequired = 100000); r.Put(r.Saw, "input", "log", 1); r.Run(18);
            r.Put(r.Engine, "fuel", "wood", 64); r.Run(42); Assert.AreEqual(0.70, r.State.Utilization, 1e-9);
        }

        [Test]
        public void EmptyMachineIsIdleAndInvalidPortDoesNotDrainOutput()
        {
            var r = new Rig(); r.Run(); Assert.AreEqual(MachinePhase.Idle, r.State.Phase); Assert.AreEqual(ReasonCode.NoInput, r.State.Reason);
            r.Put(r.Saw, "output", "plank", 2); r.Run(3); Assert.AreEqual(2, r.Count(r.Saw, "output"));
        }

        [Test]
        public void FactorySessionBuildsPoweredLineAndDeliversProductsIntoChest()
        {
            var s = new FactorySession(16, 16, new Dictionary<string, int> { ["saw"] = 1, ["engine"] = 1, ["shaft"] = 1, ["belt"] = 2, ["chest"] = 1, ["accessor"] = 1, ["log"] = 1, ["wood"] = 8 });
            var saw = new CellRef(4, 4); var engine = new CellRef(4, 6); var chest = new CellRef(7, 4);
            s.QueueBuild(Buildable.Saw, new[] { saw }, 0); s.QueueBuild(Buildable.Shaft, new[] { new CellRef(4, 5) }, 0);
            s.QueueBuild(Buildable.Engine, new[] { engine }, 0); s.QueueBuild(Buildable.Chest, new[] { chest }, 0);
            s.QueueBuild(Buildable.Belt, new[] { new CellRef(5, 4), new CellRef(6, 4) }, 0); s.Engine.Advance();
            s.QueueDeposit(saw, s.Registry.Items.Parse("log"), 1); s.QueueDeposit(engine, s.Registry.Items.Parse("wood"), 8);
            s.QueueAccessor(new CellRef(6, 4), Side.East, new AccessorConfiguration()); s.Engine.Advance(14);
            var b = s.World.BlockAt(chest); Assert.AreEqual(2, s.World.SlotCountOf(s.World.SlotGlobal(b, 0)));
            Assert.AreEqual(s.Registry.Items.Parse("plank").Handle, s.World.SlotItemHandle(s.World.SlotGlobal(b, 0)));
            Assert.AreEqual(1, s.Processing.State(s.World.BlockAt(saw)).CompletedBatches);
        }
    }
}

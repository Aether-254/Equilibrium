using System;
using System.Diagnostics;
using System.Linq;
using Equilibrium.Content;
using Equilibrium.Simulation;
using Equilibrium.Simulation.World;
using Equilibrium.Tests.EditMode.TickSimulation;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.Logistics
{
    public sealed class LogisticsTests
    {
        private sealed class Fixture
        {
            public readonly ContentRegistry Registry = new ContentRegistry();
            public readonly BlockDefTable Defs = new BlockDefTable();
            public readonly WorldState State;
            public readonly WorldWriter Writer;
            public readonly WorldView View;
            public readonly BlockDefHandle Belt, Chest, Machine;
            public readonly ItemId Wood, Plank;
            public readonly LogisticsSystem Logistics;
            public readonly TickEngine Engine;

            public Fixture(int maxStack = 3000, int width = 16, ILogisticsPortPolicy policy = null, bool attach = true)
            {
                var pack = Tier1Content.Build();
                foreach (var item in pack.Items) if (item.Id == "wood" || item.Id == "plank") item.MaxStack = maxStack;
                Assert.IsFalse(new ContentLoader().Load(Registry, pack, Array.Empty<ContentPack>()).BaseRejected);
                Wood = Registry.Items.Parse("wood"); Plank = Registry.Items.Parse("plank");
                State = new WorldState(width, 8, Registry); Writer = new WorldWriter(State, Defs); View = Writer.View;
                Belt = Defs.Register(BlockDefinition.Belt("belt", Registry.Items.Parse("belt").Handle));
                Chest = Defs.Register(BlockDefinition.Chest("chest", Registry.Items.Parse("chest").Handle, 1, 200));
                Machine = Defs.Register(BlockDefinition.Machine("saw", Registry.Items.Parse("saw").Handle, 200, 200));
                Logistics = new LogisticsSystem(Writer, Defs, policy);
                Engine = new TickEngine(View, Writer, new FixedTieBreakSource(9, 2, 7, 1, 6, 3));
                if (attach) Logistics.Attach(Engine);
            }

            public BlockRef Put(BlockDefHandle definition, int x, int y = 2, int orientation = 0)
            { Assert.AreEqual(ReasonCode.None, Writer.Place(definition, new CellRef(x, y), orientation, null, out var b)); return b; }
            public int Slot(BlockRef b) => View.SlotGlobal(b, 0);
            public int Count(BlockRef b) => View.SlotCountOf(Slot(b));
            public void Fill(BlockRef b, int count, ItemId? item = null)
                => Assert.AreEqual(count, Writer.Insert(Slot(b), Slot(b) + View.SlotCount(b), item ?? Wood, count));
            public void Accessor(BlockRef b, Side side, AccessorFlow flow, AccessorQuantity quantity = AccessorQuantity.UpTo,
                int amount = 16, ItemId? item = null)
                => Writer.ConfigureAccessor(b, side, new AccessorConfiguration(flow, quantity, amount, item));
            public void Tick(int n = 1)
            {
                Engine.Advance(n);
                Assert.IsTrue(Logistics.LastBatchSucceeded);
                Assert.IsFalse(Engine.Events.Rejected.ToArray().Any(e => e.Reason == ReasonCode.CommitFault));
            }
        }

        [Test]
        public void WholeGroupsVacateAndRefillTogetherWithoutMergingOrDoubleHop()
        {
            var f = new Fixture();
            var a = f.Put(f.Belt, 1); var b = f.Put(f.Belt, 2); var c = f.Put(f.Belt, 3);
            f.Fill(a, 100); f.Fill(b, 200);
            f.Tick();
            Assert.AreEqual(0, f.Count(a)); Assert.AreEqual(100, f.Count(b)); Assert.AreEqual(200, f.Count(c));
            Assert.AreEqual(2, f.Logistics.Transfers.Length);
            Assert.IsFalse(f.Logistics.BeltResult(b).Stalled);
            f.Tick();
            Assert.AreEqual(100, f.Count(b)); Assert.AreEqual(200, f.Count(c));
            Assert.IsTrue(f.Logistics.BeltResult(b).Stalled);
        }

        [Test]
        public void DifferentItemGroupsCanExchangeVacatedCells()
        {
            var f = new Fixture(); var a = f.Put(f.Belt, 1); var b = f.Put(f.Belt, 2); var c = f.Put(f.Belt, 3);
            f.Fill(a, 100); f.Fill(b, 200, f.Plank); f.Tick();
            Assert.AreEqual(f.Wood.Handle, f.View.SlotItemHandle(f.Slot(b)));
            Assert.AreEqual(f.Plank.Handle, f.View.SlotItemHandle(f.Slot(c)));
        }

        [Test]
        public void SmallTargetRejectsWholeGroupWithoutSplitting()
        {
            var f = new Fixture(); var narrow = BlockDefinition.Belt("narrow", -1);
            narrow.Containers[0].Slots[0] = new SlotSpec("cell", 64, -1);
            var a = f.Put(f.Belt, 1); var b = f.Put(f.Defs.Register(narrow), 2); f.Fill(a, 3000); f.Tick();
            Assert.AreEqual(3000, f.Count(a)); Assert.AreEqual(0, f.Count(b));
            Assert.AreEqual(ReasonCode.DownstreamCapacity, f.Logistics.BeltResult(a).Reason);
        }

        [Test]
        public void SideInputsCompeteForOneGroupSlot()
        {
            var f = new Fixture(); var a = f.Put(f.Belt, 1); var b = f.Put(f.Belt, 2, 3, 1); var target = f.Put(f.Belt, 2);
            f.Fill(a, 100); f.Fill(b, 200); f.Tick();
            Assert.That(f.Count(target), Is.EqualTo(100).Or.EqualTo(200));
            Assert.AreEqual(300, f.Count(a) + f.Count(b) + f.Count(target));
            Assert.AreEqual(1, f.Logistics.Transfers.Length);
        }

        [Test]
        public void NoAccessorMeansNoChestConnectionAndOpposingBeltsReject()
        {
            var f = new Fixture(); var a = f.Put(f.Belt, 1); var chest = f.Put(f.Chest, 2); f.Fill(a, 10); f.Tick();
            Assert.AreEqual(10, f.Count(a)); Assert.AreEqual(0, f.Count(chest));
            Assert.AreEqual(ReasonCode.NoReceivingPort, f.Logistics.BeltResult(a).Reason);
            f.Writer.Remove(chest, out _); f.Put(f.Belt, 2, orientation: 2); f.Tick();
            Assert.AreEqual(10, f.Count(a));
        }

        [Test]
        public void PartialDeliveryStallsAndRecoversOnThisTick()
        {
            var f = new Fixture(); var b = f.Put(f.Belt, 1); var chest = f.Put(f.Chest, 2);
            f.Fill(b, 2); f.Fill(chest, 199); f.Accessor(b, Side.East, AccessorFlow.BeltToContainer); f.Tick();
            Assert.AreEqual(1, f.Count(b)); Assert.AreEqual(200, f.Count(chest));
            Assert.IsTrue(f.Logistics.BeltResult(b).Stalled);
            Assert.AreEqual(ReasonCode.DownstreamCapacity, f.Logistics.BeltResult(b).Reason);
            f.Writer.Extract(f.Slot(chest), f.Slot(chest) + 1, f.Wood, 10); f.Tick();
            Assert.AreEqual(0, f.Count(b)); Assert.AreEqual(191, f.Count(chest));
            Assert.IsFalse(f.Logistics.BeltResult(b).Stalled);
        }

        [TestCase(9, 0, 9)]
        [TestCase(10, 10, 0)]
        public void ExactRequiresSourceAndWholeDestinationCapacity(int source, int moved, int remaining)
        {
            var f = new Fixture(); var c = f.Put(f.Chest, 0); var b = f.Put(f.Belt, 1); f.Fill(c, source);
            f.Accessor(b, Side.West, AccessorFlow.ContainerToBelt, AccessorQuantity.Exact, 10); f.Tick();
            Assert.AreEqual(moved, f.Count(b)); Assert.AreEqual(remaining, f.Count(c));
            if (moved == 0) Assert.AreEqual(ReasonCode.ExactUnsatisfied, f.Logistics.AccessorResult(b, Side.West).Reason);
        }

        [Test]
        public void ExactOnStalledBeltDoesNotPartiallySucceed()
        {
            var f = new Fixture(maxStack: 64); var c = f.Put(f.Chest, 0); var b = f.Put(f.Belt, 1);
            f.Fill(c, 20); f.Fill(b, 60); f.Accessor(b, Side.West, AccessorFlow.ContainerToBelt, AccessorQuantity.Exact, 10); f.Tick();
            Assert.AreEqual(60, f.Count(b)); Assert.AreEqual(20, f.Count(c));
            Assert.AreEqual(ReasonCode.ExactUnsatisfied, f.Logistics.AccessorResult(b, Side.West).Reason);
        }

        [Test]
        public void UpToUsesRemainingSourceAndStalledMergeUsesEffectiveCapacity()
        {
            var f = new Fixture(maxStack: 64); var c = f.Put(f.Chest, 0); var b = f.Put(f.Belt, 1);
            f.Fill(c, 7); f.Accessor(b, Side.West, AccessorFlow.ContainerToBelt, amount: 10); f.Tick();
            Assert.AreEqual(7, f.Count(b)); Assert.AreEqual(0, f.Count(c));
            f.Fill(c, 64); f.Accessor(b, Side.West, AccessorFlow.ContainerToBelt, amount: 100); f.Tick();
            Assert.AreEqual(64, f.Count(b)); Assert.AreEqual(7, f.Count(c));
            Assert.IsTrue(f.Logistics.BeltResult(b).Stalled);
        }

        [TestCase(130, 50, 64, 66)]
        [TestCase(40, 50, 0, 40)]
        public void KeepHonorsReserveAndEffectiveCapacity(int source, int keep, int moved, int remaining)
        {
            var f = new Fixture(maxStack: 64);
            var large = f.Defs.Register(BlockDefinition.Chest("large", -1, 4, 200));
            var c = f.Put(large, 0); var b = f.Put(f.Belt, 1); f.Fill(c, source);
            f.Accessor(b, Side.West, AccessorFlow.ContainerToBelt, AccessorQuantity.Keep, keep); f.Tick();
            Assert.AreEqual(moved, f.Count(b));
            Assert.AreEqual(remaining, ContainerOps.Available(f.View, f.Slot(c), f.Slot(c) + 4, f.Wood));
        }

        [Test]
        public void KeepCountsReservedStockButCannotExtractIt()
        {
            var f = new Fixture(); var c = f.Put(f.Chest, 0); var b = f.Put(f.Belt, 1); f.Fill(c, 100);
            f.Writer.Reserve(f.Slot(c), f.Slot(c) + 1, f.Wood, 40);
            f.Accessor(b, Side.West, AccessorFlow.ContainerToBelt, AccessorQuantity.Keep, 50); f.Tick();
            Assert.AreEqual(50, f.Count(c)); Assert.AreEqual(50, f.Count(b)); Assert.AreEqual(40, f.View.SlotReservedOf(f.Slot(c)));
        }

        [Test]
        public void MovingOriginalGroupIsNeverMergedAndFeedCanRefillVacancy()
        {
            var f = new Fixture(); var c = f.Put(f.Chest, 0); var a = f.Put(f.Belt, 1); var b = f.Put(f.Belt, 2);
            f.Fill(c, 20); f.Fill(a, 10); f.Accessor(a, Side.West, AccessorFlow.ContainerToBelt, amount: 5); f.Tick();
            Assert.AreEqual(10, f.Count(b)); Assert.AreEqual(5, f.Count(a)); Assert.AreEqual(15, f.Count(c));
            Assert.IsFalse(f.Logistics.BeltResult(a).Stalled);
        }

        [Test]
        public void IntakePriorityBeatsBeltArrivalAndNeverMergesNewArrival()
        {
            var f = new Fixture(); var a = f.Put(f.Belt, 1); var b = f.Put(f.Belt, 2); var c = f.Put(f.Chest, 2, 3);
            f.Fill(a, 10); f.Fill(c, 5, f.Plank);
            f.Accessor(b, Side.North, AccessorFlow.ContainerToBelt, amount: 5); f.Tick();
            Assert.AreEqual(10, f.Count(a)); Assert.AreEqual(5, f.Count(b));
            Assert.AreEqual(f.Plank.Handle, f.View.SlotItemHandle(f.Slot(b)));
        }

        [Test]
        public void AccessorDepositWinsOverForwardGroupAndPartialRemainderWaits()
        {
            var f = new Fixture(); var a = f.Put(f.Belt, 1); var b = f.Put(f.Belt, 2); var c = f.Put(f.Chest, 1, 3);
            f.Fill(a, 10); f.Fill(c, 195); f.Accessor(a, Side.North, AccessorFlow.BeltToContainer); f.Tick();
            Assert.AreEqual(5, f.Count(a)); Assert.AreEqual(0, f.Count(b)); Assert.AreEqual(200, f.Count(c));
        }

        [Test]
        public void FilterAirAndBlockerProduceReasonsWithoutMoving()
        {
            var f = new Fixture(); var c = f.Put(f.Chest, 0); var b = f.Put(f.Belt, 1); f.Fill(c, 20);
            f.Accessor(b, Side.West, AccessorFlow.ContainerToBelt, item: f.Plank); f.Tick();
            Assert.AreEqual(ReasonCode.SourceEmpty, f.Logistics.AccessorResult(b, Side.West).Reason);
            f.Accessor(b, Side.North, AccessorFlow.BeltToContainer); f.Fill(b, 2); f.Tick();
            Assert.AreEqual(ReasonCode.TargetInvalid, f.Logistics.AccessorResult(b, Side.North).Reason);
            f.Writer.ConfigureAccessor(b, Side.North, null); f.Accessor(b, Side.West, AccessorFlow.ContainerToBelt);
            f.Writer.SetIngress(new CellRef(1, 2), Side.West, false); f.Tick();
            Assert.AreEqual(20, f.Count(c)); Assert.AreEqual(2, f.Count(b));
        }

        [Test]
        public void AutoFlowUsesTopologyAndRotatesAttachmentWithHost()
        {
            var f = new Fixture(); var c = f.Put(f.Chest, 2, 3); var b = f.Put(f.Belt, 2, 2, 1);
            var end = f.Put(f.Belt, 2, 1, 1); var output = f.Put(f.Chest, 2, 0);
            f.Fill(c, 7); f.Accessor(b, Side.West, AccessorFlow.Auto); f.Accessor(end, Side.East, AccessorFlow.Auto);
            f.Tick(3);
            Assert.AreEqual(7, f.Count(output)); Assert.AreEqual(0, f.Count(c));
            Assert.AreEqual(AccessorFlow.ContainerToBelt, f.Logistics.AccessorResult(b, Side.West).Flow);
            Assert.AreEqual(AccessorFlow.BeltToContainer, f.Logistics.AccessorResult(end, Side.East).Flow);
        }

        [Test]
        public void SharedSourceNeverOverdrawsAndReservationsRemain()
        {
            var f = new Fixture(); var c = f.Put(f.Chest, 2); var a = f.Put(f.Belt, 1); var b = f.Put(f.Belt, 3);
            f.Fill(c, 15); f.Writer.Reserve(f.Slot(c), f.Slot(c) + 1, f.Wood, 3);
            f.Accessor(a, Side.East, AccessorFlow.ContainerToBelt, AccessorQuantity.Exact, 10);
            f.Accessor(b, Side.West, AccessorFlow.ContainerToBelt, AccessorQuantity.Exact, 10); f.Tick();
            Assert.AreEqual(10, f.Count(a) + f.Count(b)); Assert.AreEqual(5, f.Count(c)); Assert.AreEqual(3, f.View.SlotReservedOf(f.Slot(c)));
        }

        [Test]
        public void SharedDestinationAcceptsOnlyRemainingCapacity()
        {
            var f = new Fixture(); var c = f.Put(f.Chest, 2); var a = f.Put(f.Belt, 1); var b = f.Put(f.Belt, 3);
            f.Fill(c, 190); f.Fill(a, 8); f.Fill(b, 8);
            f.Accessor(a, Side.East, AccessorFlow.BeltToContainer); f.Accessor(b, Side.West, AccessorFlow.BeltToContainer); f.Tick();
            Assert.AreEqual(200, f.Count(c)); Assert.AreEqual(6, f.Count(a) + f.Count(b));
            Assert.AreEqual(10, f.Logistics.Transfers.ToArray().Sum(t => t.Count));
        }

        [Test]
        public void NewlyDepositedChestStockCannotBeForwardedAgainThisTick()
        {
            var f = new Fixture(); var c = f.Put(f.Chest, 2); var a = f.Put(f.Belt, 1); var b = f.Put(f.Belt, 3);
            f.Fill(a, 8); f.Accessor(a, Side.East, AccessorFlow.BeltToContainer);
            f.Accessor(b, Side.West, AccessorFlow.ContainerToBelt); f.Tick();
            Assert.AreEqual(8, f.Count(c)); Assert.AreEqual(0, f.Count(b)); f.Tick(); Assert.AreEqual(8, f.Count(b));
        }

        [Test]
        public void TidyCanFreeSlotOccupiedByAnotherItemWithoutTouchingReservations()
        {
            var f = new Fixture(maxStack: 64); var def = f.Defs.Register(BlockDefinition.Chest("two", -1, 2, 64));
            var c = f.Put(def, 2); int s = f.Slot(c);
            f.Writer.Insert(s, s + 1, f.Plank, 10); f.Writer.Insert(s + 1, s + 2, f.Plank, 10);
            var b = f.Put(f.Belt, 1); f.Fill(b, 5); f.Accessor(b, Side.East, AccessorFlow.BeltToContainer); f.Tick();
            Assert.AreEqual(20, f.View.SlotCountOf(s)); Assert.AreEqual(f.Wood.Handle, f.View.SlotItemHandle(s + 1));
            Assert.AreEqual(5, f.View.SlotCountOf(s + 1)); Assert.AreEqual(0, f.Count(b));
        }

        private sealed class RefusingMachine : ILogisticsPortPolicy
        {
            public ReasonCode Check(WorldView world, BlockRef block, int port, ItemId item, bool receiving) => ReasonCode.MachineNotAccepting;
        }

        [Test]
        public void DirectMachinePortAcceptsWholeOnlyAndPolicyCanRefuse()
        {
            var f = new Fixture(); var b = f.Put(f.Belt, 1); var m = f.Put(f.Machine, 2);
            f.Writer.Insert(f.Slot(m), f.Slot(m) + 1, f.Wood, 199); f.Fill(b, 2); f.Tick();
            Assert.AreEqual(2, f.Count(b)); Assert.AreEqual(199, f.Count(m));
            f.Writer.Extract(f.Slot(m), f.Slot(m) + 1, f.Wood, 2); f.Tick(); Assert.AreEqual(0, f.Count(b));
            var refused = new Fixture(policy: new RefusingMachine()); var rb = refused.Put(refused.Belt, 1); refused.Put(refused.Machine, 2);
            refused.Fill(rb, 10); refused.Tick(); Assert.AreEqual(10, refused.Count(rb));
            Assert.AreEqual(ReasonCode.MachineNotAccepting, refused.Logistics.BeltResult(rb).Reason);
        }

        [Test]
        public void FourCellLoopMovesExactlyOneHopForOneHundredTicks()
        {
            var f = new Fixture(); var loop = new[] { f.Put(f.Belt, 1, 2), f.Put(f.Belt, 2, 2, 1),
                f.Put(f.Belt, 2, 1, 2), f.Put(f.Belt, 1, 1, 3) };
            f.Fill(loop[0], 25);
            for (int tick = 1; tick <= 100; tick++)
            {
                f.Tick(); Assert.AreEqual(25, f.Count(loop[tick % 4]));
                Assert.AreEqual(25, loop.Sum(f.Count)); Assert.AreEqual(1, f.Logistics.Transfers.Length);
            }
        }

        private sealed class RotateOnce : ITickSystem
        {
            private readonly BlockRef _block;
            public RotateOnce(BlockRef block) { _block = block; }
            public void Collect(in TickSnapshot snapshot, TransactionBuffer buffer, int systemIndex)
            { var tx = WorldTransactions.Rotate(EntityRef.None, _block, 1); buffer.Add(in tx, systemIndex); }
            public ReasonCode Validate(in TickSnapshot snapshot, in Transaction tx) => ReasonCode.None;
        }

        [Test]
        public void EarlierStructuralCommitRejectsStaleBatchWithoutPartialInventoryOrEvents()
        {
            var f = new Fixture(attach: false); var a = f.Put(f.Belt, 1); var b = f.Put(f.Belt, 2); f.Fill(a, 10);
            f.Engine.Register(new RotateOnce(a)); f.Logistics.Attach(f.Engine); f.Engine.Advance();
            Assert.IsFalse(f.Logistics.LastBatchSucceeded); Assert.AreEqual(10, f.Count(a)); Assert.AreEqual(0, f.Count(b));
            Assert.AreEqual(0, f.Logistics.Transfers.Length); Assert.AreEqual(ReasonCode.CommitFault, f.Engine.Events.Rejected[0].Reason);
        }

        [Test]
        public void ChestToChestLineConservesEveryItemAcrossStopsAndRecovery()
        {
            var f = new Fixture(); var source = f.Put(f.Chest, 0); var output = f.Put(f.Chest, 5);
            var belts = Enumerable.Range(1, 4).Select(x => f.Put(f.Belt, x)).ToArray();
            f.Fill(source, 200); f.Accessor(belts[0], Side.West, AccessorFlow.Auto, amount: 16);
            f.Accessor(belts[3], Side.East, AccessorFlow.Auto);
            for (int t = 0; t < 60; t++)
            {
                if (t == 4) f.Writer.SetIngress(new CellRef(3, 2), Side.West, false);
                if (t == 12) f.Writer.SetIngress(new CellRef(3, 2), Side.West, true);
                f.Tick(); Assert.AreEqual(200, f.Count(source) + f.Count(output) + belts.Sum(f.Count));
            }
            Assert.AreEqual(200, f.Count(output)); Assert.AreEqual(0, belts.Sum(f.Count));
        }

        [Test]
        public void LongLineUsesIterativeDependencyResolution()
        {
            const int n = 2000;
            var f = new Fixture(width: n + 2); var belts = Enumerable.Range(0, n).Select(x => f.Put(f.Belt, x)).ToArray();
            foreach (var b in belts.Take(n - 1)) f.Fill(b, 1);
            var watch = Stopwatch.StartNew(); f.Tick(); watch.Stop();
            Assert.AreEqual(n - 1, f.Logistics.Transfers.Length); Assert.AreEqual(0, f.Count(belts[0]));
            Assert.AreEqual(n - 1, belts.Sum(f.Count));
            TestContext.WriteLine($"2,000-cell cold whole tick: {watch.Elapsed.TotalMilliseconds:F3} ms (not a steady-state performance gate)");
        }

        [Test]
        public void FullDependencyCycleRejectsWithoutMutation()
        {
            var f = new Fixture(); var loop = new[] { f.Put(f.Belt, 1, 2), f.Put(f.Belt, 2, 2, 1),
                f.Put(f.Belt, 2, 1, 2), f.Put(f.Belt, 1, 1, 3) };
            foreach (var b in loop) f.Fill(b, 25);
            f.Tick(); Assert.AreEqual(0, f.Logistics.Transfers.Length); Assert.AreEqual(100, loop.Sum(f.Count));
            Assert.IsTrue(loop.All(b => f.Logistics.BeltResult(b).Stalled));
        }

        [Test]
        public void LastTicksStallNeverAuthorizesMergeIntoTodaysMovingGroup()
        {
            var f = new Fixture(); var c = f.Put(f.Chest, 0); var b = f.Put(f.Belt, 1);
            f.Fill(b, 10); f.Tick(); Assert.IsTrue(f.Logistics.BeltResult(b).Stalled);
            var next = f.Put(f.Belt, 2); f.Fill(c, 5);
            f.Accessor(b, Side.West, AccessorFlow.ContainerToBelt, amount: 5); f.Tick();
            Assert.AreEqual(10, f.Count(next)); Assert.AreEqual(5, f.Count(b));
            Assert.IsFalse(f.Logistics.BeltResult(b).Stalled);
        }

        private sealed class ObservePlan : ITickSystem, ITickBatchPlanner
        {
            private readonly Fixture _fixture;
            private readonly BlockRef _source, _target;
            public bool Observed;
            public ObservePlan(Fixture fixture, BlockRef source, BlockRef target)
            { _fixture = fixture; _source = source; _target = target; }
            public void Collect(in TickSnapshot snapshot, TransactionBuffer buffer, int systemIndex) { }
            public ReasonCode Validate(in TickSnapshot snapshot, in Transaction tx) => ReasonCode.None;
            public void Plan(in TickSnapshot snapshot, TransactionBuffer buffer, ITieBreakSource tieBreak)
            {
                Assert.AreEqual(TickPhase.Resolve, _fixture.Engine.Phase);
                Assert.AreEqual(10, _fixture.Count(_source)); Assert.AreEqual(0, _fixture.Count(_target));
                Observed = true;
            }
        }

        [Test]
        public void ResolvingLogisticsDoesNotMutateSnapshotBeforeCommit()
        {
            var f = new Fixture(); var a = f.Put(f.Belt, 1); var b = f.Put(f.Belt, 2); f.Fill(a, 10);
            var observer = new ObservePlan(f, a, b); f.Engine.Register(observer); f.Tick();
            Assert.IsTrue(observer.Observed); Assert.AreEqual(0, f.Count(a)); Assert.AreEqual(10, f.Count(b));
        }

        [Test]
        public void FailedExactInsertionRestoresTentativeTidy()
        {
            var f = new Fixture(maxStack: 64); var def = f.Defs.Register(BlockDefinition.Machine("two", -1, 64, 64));
            var machineDef = f.Defs.Get(def); machineDef.Containers[0].Slot("in_1", 64);
            var m = f.Put(def, 2); int s = f.Slot(m);
            f.Writer.Insert(s, s + 1, f.Plank, 10); f.Writer.Insert(s + 1, s + 2, f.Plank, 10);
            var b = f.Put(f.Belt, 1);
            // The item maxStack is 64, so use two partial 40-plank slots: tidy frees no slot.
            f.Writer.Insert(s, s + 1, f.Plank, 30); f.Writer.Insert(s + 1, s + 2, f.Plank, 30);
            f.Fill(b, 5); f.Tick();
            Assert.AreEqual(40, f.View.SlotCountOf(s)); Assert.AreEqual(40, f.View.SlotCountOf(s + 1));
            Assert.AreEqual(5, f.Count(b));
        }

        [Test]
        public void RemovedAccessorDoesNotSurviveBlockIndexReuse()
        {
            var f = new Fixture(); var b = f.Put(f.Belt, 1);
            f.Accessor(b, Side.West, AccessorFlow.Auto);
            f.Writer.Remove(b, out _); f.Writer.Compact(); var replacement = f.Put(f.Belt, 1);
            Assert.AreEqual(b.Index, replacement.Index); Assert.IsNull(f.View.Accessor(replacement, Side.West));
            Assert.IsTrue(new AccessorConfiguration(amount: 0).AmountClamped);
            Assert.AreEqual(1, new AccessorConfiguration(amount: 0).Amount);
            Assert.AreEqual(3000, new AccessorConfiguration(amount: 9999).Amount);
        }
    }
}

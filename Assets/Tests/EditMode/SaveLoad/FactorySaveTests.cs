using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Equilibrium.Editing;
using Equilibrium.Presentation;
using Equilibrium.Simulation;
using Equilibrium.Simulation.Save;
using Equilibrium.Simulation.World;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Equilibrium.Tests.EditMode.SaveLoad
{
    public sealed class FactorySaveTests
    {
        private static readonly DateTimeOffset Clock = new DateTimeOffset(2026, 11, 6, 0, 0, 0, TimeSpan.Zero);
        private static CellRef C(int x, int y = 2) => new CellRef(x, y);
        private static FactorySession Factory(string mode = "LocalNpc", string server = null)
            => new FactorySession(16, 16, new Dictionary<string, int> {
                ["chest"] = 4, ["belt"] = 4, ["accessor"] = 4, ["blocker"] = 4,
                ["saw"] = 2, ["engine"] = 2, ["shaft"] = 4, ["log"] = 128, ["wood"] = 128 }, mode, server);

        private static FactorySession Powered(int logs = 8)
        {
            var session = Factory();
            session.QueueBuild(Buildable.Saw, new[] { C(4) }, 0);
            session.QueueBuild(Buildable.Shaft, new[] { C(4, 3) }, 0);
            session.QueueBuild(Buildable.Engine, new[] { C(4, 4) }, 0);
            session.Engine.Advance();
            session.QueueDeposit(C(4), session.Registry.Items.Parse("log"), logs);
            session.QueueDeposit(C(4, 4), session.Registry.Items.Parse("wood"), 64);
            session.Engine.Advance(); session.Engine.Advance(); session.Engine.Advance();
            return session;
        }

        private static FactorySession Reload(FactorySession session)
            => FactorySession.FromSave(SaveDocumentCodec.Deserialize(SaveDocumentCodec.Serialize(session.CaptureSave(Clock))));

        private static void Same(FactorySession expected, FactorySession actual)
        {
            var before = JToken.Parse(SaveDocumentJson.Serialize(expected.CaptureSave(Clock)));
            var after = JToken.Parse(SaveDocumentJson.Serialize(actual.CaptureSave(Clock)));
            Assert.IsTrue(JToken.DeepEquals(before, after), "Expected:\n" + before + "\nActual:\n" + after);
        }

        [Test]
        public void WorkingFactoryRoundTripResumesPowerFuelReservationsAndUtilization()
        {
            var original = Powered(); var loaded = Reload(original); Same(original, loaded);
            for (int i = 0; i < 18; i++) { original.Engine.Advance(); loaded.Engine.Advance(); Same(original, loaded); }
            Assert.Greater(loaded.Processing.State(loaded.World.BlockAt(C(4))).CompletedBatches, 0);
        }

        [Test]
        public void LogisticsAttachmentsGhostsRecoveredInventoriesAndWarehouseSurvive()
        {
            var original = Factory();
            original.QueueBuild(Buildable.Chest, new[] { C(1) }, 3);
            original.QueueBuild(Buildable.Chest, new[] { C(6) }, 2);
            original.QueueBuild(Buildable.Belt, Enumerable.Range(2, 4).Select(x => C(x)).ToArray(), 0);
            original.Engine.Advance();
            original.QueueDeposit(C(1), original.Registry.Items.Parse("wood"), 64);
            original.QueueAccessor(C(2), Side.West, new AccessorConfiguration(AccessorFlow.ContainerToBelt, AccessorQuantity.Exact, 7,
                original.Registry.Items.Parse("wood")));
            original.QueueAccessor(C(5), Side.East, new AccessorConfiguration()); original.QueueBlocker(C(3), Side.North);
            original.QueueBuild(Buildable.Belt, new[] { C(9), C(10) }, 0);
            original.Engine.Advance(); original.Engine.Advance();
            original.QueueRemove(C(1)); original.Engine.Advance();
            var loaded = Reload(original); Same(original, loaded);
            Assert.AreEqual(2, loaded.Ghosts.Count); Assert.AreEqual(1, loaded.RecoveredBlocks.Count);
            Assert.IsFalse(loaded.World.IngressEnabled(C(3), Side.North));
            Assert.AreEqual(7, loaded.World.Accessor(loaded.World.BlockAt(C(2)), Side.West).Amount);
            loaded.QueueBuild(Buildable.Chest, new[] { C(8, 4) }, 1); loaded.Engine.Advance();
            Assert.AreEqual(0, loaded.RecoveredBlocks.Count);
            Assert.AreEqual(1, loaded.World.OrientationOf(loaded.World.BlockAt(C(8, 4))));
        }

        [Test]
        public void BlockedProductsAreNotConsumedOrAwardedTwiceAfterLoading()
        {
            var original = Powered(64);
            for (int i = 0; i < 240; i++) original.Engine.Advance();
            var state = original.Processing.State(original.World.BlockAt(C(4)));
            Assert.AreEqual(MachinePhase.Blocked, state.Phase); Assert.AreEqual(2, state.PendingProducts[0].Count);
            var loaded = Reload(original); Same(original, loaded);
            original.QueueCollect(C(4)); loaded.QueueCollect(C(4));
            for (int i = 0; i < 3; i++) { original.Engine.Advance(); loaded.Engine.Advance(); Same(original, loaded); }
            Assert.AreEqual(64, loaded.Stock("plank"));
        }

        [Test]
        public void DroppedWorkingMachineRetainsNamedReservationsAndNewPlacementOrientation()
        {
            var original = Powered(); original.QueueRemove(C(4)); original.Engine.Advance();
            var loaded = Reload(original); Same(original, loaded);
            loaded.QueueBuild(Buildable.Saw, new[] { C(8) }, 2); loaded.Engine.Advance(); loaded.Engine.Advance();
            var saw = loaded.World.BlockAt(C(8)); Assert.AreEqual(2, loaded.World.OrientationOf(saw));
            Assert.AreEqual(1, loaded.World.SlotReservedOf(loaded.World.SlotGlobal(saw, 0)));
            Assert.AreEqual(ReasonCode.NoPowerSource, loaded.Processing.State(saw).Reason);
        }

        [Test]
        public void SlotOrderAndUnknownFieldsRoundTripWithoutLosingRawValues()
        {
            var document = Powered().CaptureSave(Clock);
            document.Extras["future"] = RawSaveValue.FromBytes(new byte[] { 0xD5, 0x55, 0xAA, 0xBB });
            var container = document.Containers.Records.First(r => r.Id == "block/4/2");
            var slots = SaveFields.Section(container, "slots"); slots.Records.Reverse();
            slots.Extras["extension"] = RawSaveValue.FromBytes(new byte[] { 0xCD, 0, 7 });
            slots.Records[0].Extras["unknown"] = RawSaveValue.FromBytes(new byte[] { 0xCA, 0x3F, 0x80, 0, 0 });
            SaveFields.SetSection(container, "slots", slots);
            var loaded = FactorySession.FromSave(document); document.Header.MarketBinding.Mode = "changed";
            var saved = loaded.CaptureSave(Clock);
            CollectionAssert.AreEqual(new byte[] { 0xD5, 0x55, 0xAA, 0xBB }, ((RawSaveValue)saved.Extras["future"]).CopyBytes());
            var roundTripSlots = SaveFields.Section(saved.Containers.Records.First(r => r.Id == "block/4/2"), "slots");
            CollectionAssert.AreEqual(new byte[] { 0xCD, 0, 7 }, ((RawSaveValue)roundTripSlots.Extras["extension"]).CopyBytes());
            CollectionAssert.AreEqual(new byte[] { 0xCA, 0x3F, 0x80, 0, 0 },
                ((RawSaveValue)roundTripSlots.Records.First(r => r.SlotKey == "out_0").Extras["unknown"]).CopyBytes());
            Assert.AreEqual("LocalNpc", loaded.MarketMode);
        }

        [Test]
        public void UnknownInventoryRetainsItsCanonicalIdentityAndCount()
        {
            var session = Factory(); session.QueueBuild(Buildable.Chest, new[] { C(1) }, 0); session.Engine.Advance();
            var document = session.CaptureSave(Clock);
            var container = document.Containers.Records.First(r => r.Id == "block/1/2");
            var slots = SaveFields.Section(container, "slots"); slots.Records[0].ItemId = "farmer_addon:seed"; slots.Records[0].Count = 100;
            SaveFields.SetSection(container, "slots", slots);
            var loaded = FactorySession.FromSave(document); var slot = loaded.World.SlotGlobal(loaded.World.BlockAt(C(1)), 0);
            Assert.AreEqual(100, loaded.World.SlotCountOf(slot));
            Assert.IsTrue(loaded.Registry.Item(loaded.Registry.Items.FromHandle(loaded.World.SlotItemHandle(slot))).IsUnknown);
            Same(loaded, Reload(loaded));
        }

        [Test]
        public void MarketBindingIsImmutableAndOpaqueSectionsSurvive()
        {
            var document = Factory("RemoteServer", "server-a").CaptureSave(Clock);
            document.Market.Records.Add(new SaveRecord { Id = "escrow/future", ItemId = "wood", Count = 12 });
            var loaded = FactorySession.FromSave(document);
            Assert.AreEqual("RemoteServer", loaded.MarketMode); Assert.AreEqual("server-a", loaded.MarketServerId);
            Assert.Throws<NotSupportedException>(() => loaded.ApplyMigrationCredential("unverified"));
            Assert.AreEqual(12, loaded.CaptureSave(Clock).Market.Records[0].Count);
            Assert.IsNull(typeof(FactorySession).GetProperty(nameof(FactorySession.MarketMode)).GetSetMethod(true));
        }

        [TestCase("overlap")]
        [TestCase("capacity")]
        [TestCase("reservation")]
        [TestCase("version")]
        [TestCase("dimensions")]
        [TestCase("missing-state")]
        public void InvalidSaveNeverChangesTheRunningFactory(string failure)
        {
            var original = Powered(); var before = original.CaptureSave(Clock); var malformed = original.CaptureSave(Clock);
            if (failure == "overlap") { var block = SaveFields.Copy(malformed.World.Records[0]); block.Id = "duplicate-position"; malformed.World.Records.Add(block); }
            if (failure == "version") malformed.World.Version = 2;
            if (failure == "dimensions") malformed.World.Extras["width"] = 10000;
            if (failure == "missing-state") malformed.Mech.Records.Clear();
            if (failure == "capacity" || failure == "reservation")
            {
                var container = malformed.Containers.Records.First(r => r.Id == "block/4/2"); var slots = SaveFields.Section(container, "slots");
                if (failure == "capacity") slots.Records[0].Count = 999999;
                else slots.Records[0].Extras["reserved"] = 0;
                SaveFields.SetSection(container, "slots", slots);
            }
            Assert.Throws<InvalidOperationException>(() => FactorySession.FromSave(malformed));
            Assert.AreEqual(SaveDocumentJson.Serialize(before), SaveDocumentJson.Serialize(original.CaptureSave(Clock)));
        }

        private sealed class RequestDuringCollect : ITickSystem
        {
            private readonly Action _request;
            public RequestDuringCollect(Action request) { _request = request; }
            public void Collect(in TickSnapshot snapshot, TransactionBuffer buffer, int systemIndex) => _request();
            public ReasonCode Validate(in TickSnapshot snapshot, in Transaction transaction) => ReasonCode.None;
        }

        [Test]
        public void RequestsDuringCollectCoalesceAndCapturePostCommitState()
        {
            var session = Factory(); var writes = new List<SaveDocument>();
            using (var service = new FactorySaveService(session, doc => { writes.Add(doc); return Task.CompletedTask; }, false, clock: () => Clock))
            {
                session.Engine.Register(new RequestDuringCollect(() => {
                    Assert.Throws<InvalidOperationException>(() => session.CaptureSave(Clock));
                    service.RequestSave(); service.RequestSave(); Assert.IsEmpty(writes);
                }));
                session.QueueBuild(Buildable.Chest, new[] { C(1) }, 2); session.Engine.Advance(); service.Poll();
                Assert.AreEqual(1, writes.Count); Assert.AreEqual(1UL, writes[0].Header.TickIndex);
                Assert.AreEqual(2, SaveFields.Int(writes[0].World.Records[0], "orientation", 0, 3));
                Assert.AreEqual(1UL, service.LastSavedTick);
            }
        }

        [Test]
        public void PendingEditsWaitForCommitAndAutosaveDoesNotOverlapInFlightWrite()
        {
            var session = Factory(); var pending = new TaskCompletionSource<bool>(); int writes = 0;
            using (var service = new FactorySaveService(session, _ => { writes++; return writes == 1 ? pending.Task : Task.CompletedTask; }, true, 2, () => Clock))
            {
                session.QueueBuild(Buildable.Chest, new[] { C(1) }, 0); service.RequestSave(); service.Poll(); Assert.AreEqual(0, writes);
                session.Engine.Advance(); Assert.AreEqual(1, writes); session.Engine.Advance(); Assert.AreEqual(1, writes);
                Assert.IsTrue(service.Pending); pending.SetResult(true); service.Poll(); service.Poll();
                Assert.AreEqual(2, writes); Assert.AreEqual(2UL, service.LastSavedTick);
            }
        }

        [Test]
        public void FailedWriteIsReportedAndExplicitRetryCanSucceed()
        {
            var session = Factory(); int writes = 0;
            using (var service = new FactorySaveService(session, _ => ++writes == 1 ? Task.FromException(new IOException("disk full")) : Task.CompletedTask, false))
            {
                service.RequestSave(); service.Poll(); service.Poll(); Assert.IsInstanceOf<IOException>(service.LastError);
                Assert.IsNull(service.LastSavedTick); service.RequestSave(); service.Poll(); service.Poll();
                Assert.IsNull(service.LastError); Assert.AreEqual(0UL, service.LastSavedTick);
            }
        }

        [Test]
        public void FileStoreRotatesThreeBackupsAndFailedWriteKeepsPrimaryBytes()
        {
            string directory = Path.Combine(Path.GetTempPath(), "equilibrium-save-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FactorySaveStore(directory); var session = Powered();
                for (int i = 0; i < 5; i++) { session.Engine.Advance(); store.Write(session.CaptureSave(Clock)); }
                for (int i = 0; i <= 3; i++) Assert.AreEqual(session.Engine.TickIndex - (ulong)i, store.Read(i).Header.TickIndex);
                byte[] original = File.ReadAllBytes(store.PrimaryPath);
                using (var locked = new FileStream(store.PrimaryPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    Assert.Throws<IOException>(() => store.Write(session.CaptureSave(Clock)));
                CollectionAssert.AreEqual(original, File.ReadAllBytes(store.PrimaryPath));
                Assert.IsFalse(File.Exists(Path.Combine(directory, "save.tmp")));
                File.WriteAllBytes(store.PrimaryPath, new byte[] { 0xC1 });
                Assert.Catch(() => store.Read()); Assert.DoesNotThrow(() => FactorySession.FromSave(store.Read(1)));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}

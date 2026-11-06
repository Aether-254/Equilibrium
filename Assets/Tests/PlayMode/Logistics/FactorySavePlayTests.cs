using System;
using System.Collections;
using System.IO;
using Equilibrium.Editing;
using Equilibrium.Presentation;
using Equilibrium.Simulation.World;
using Equilibrium.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Equilibrium.Tests.PlayMode.Logistics
{
    public sealed class FactorySavePlayTests
    {
        [UnityTest]
        public IEnumerator MainHudSavesReloadsAndOffersBackupWithoutReplacingFactoryOnFailure()
        {
            string directory = Path.Combine(Application.temporaryCachePath, "equilibrium-save-play-" + Guid.NewGuid().ToString("N"));
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
#endif
            yield return null;
            var runtime = Object.FindAnyObjectByType<FactoryRuntime>(); var hud = runtime.GetComponent<FactoryHud>();
            runtime.Pump.Paused = true; runtime.ConfigureSaveDirectory(directory); hud.Refresh();
            try
            {
                var cell = new CellRef(28, 30);
                runtime.Session.QueueBuild(Buildable.Chest, new[] { cell }, 2); runtime.Step();
                runtime.Session.QueueDeposit(cell, runtime.Session.Registry.Items.Parse("wood"), 32); runtime.Step();
                hud.SaveClicked(); yield return WaitForSave(runtime);
                var store = new FactorySaveStore(directory); ulong savedTick = runtime.Session.Engine.TickIndex;
                Assert.AreEqual(savedTick, store.Read().Header.TickIndex);
                Assert.IsTrue(SaveDocumentSession.HasCurrent);
                Assert.That(hud.Document.rootVisualElement.Q<Label>("save-status").text, Does.Contain("已保存"));
                runtime.Step(); hud.SaveClicked(); yield return WaitForSave(runtime);
                Assert.AreEqual(savedTick, store.Read(1).Header.TickIndex);

                var previousSession = runtime.Session;
                runtime.Session.QueueRemove(cell); runtime.Step(); Assert.IsTrue(runtime.Session.World.IsEmpty(cell));
                hud.Input.Select(cell, Side.West); hud.LoadClicked(); Assert.IsTrue(hud.HasModal);
                hud.Input.SetTool(FactoryTool.Chest); hud.Input.PointerDown(new CellRef(1, 1), Side.West, false);
                Assert.AreEqual(0, runtime.Session.PendingCommands, "A load confirmation must block world input.");
                Click(hud.Document.rootVisualElement.Q<Button>("save-confirm"));
                float deadline = Time.realtimeSinceStartup + 15;
                while (runtime.IsLoading && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsFalse(runtime.IsLoading); Assert.AreNotSame(previousSession, runtime.Session);
                Assert.IsTrue(runtime.Pump.Paused); Assert.IsTrue(hud.Input.Selected.IsNone);
                Assert.AreSame(runtime.Session.Engine, runtime.Pump.Engine);
                var block = runtime.Session.World.BlockAt(cell); Assert.IsFalse(block.IsNone);
                Assert.AreEqual(32, runtime.Session.World.SlotCountOf(runtime.Session.World.SlotGlobal(block, 0)));
                Assert.AreEqual(2, runtime.Session.World.OrientationOf(block));

                File.WriteAllBytes(store.PrimaryPath, new byte[] { 0xC1 }); var stillLive = runtime.Session;
                var load = runtime.LoadSaveAsync();
                deadline = Time.realtimeSinceStartup + 15;
                while (!load.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(load.IsCompleted); Assert.IsFalse(load.Result); Assert.AreSame(stillLive, runtime.Session);
                Assert.AreEqual(1, runtime.SuggestedBackup);
                Assert.AreEqual(DisplayStyle.Flex, hud.Document.rootVisualElement.Q<Button>("factory-load-backup").resolvedStyle.display);
                var recover = runtime.LoadSaveAsync(runtime.SuggestedBackup);
                deadline = Time.realtimeSinceStartup + 15;
                while (!recover.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(recover.IsCompleted); Assert.IsTrue(recover.Result);
                Assert.AreEqual(savedTick, runtime.Session.Engine.TickIndex);
            }
            finally
            {
                Object.DestroyImmediate(runtime.gameObject);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [UnityTest]
        public IEnumerator FreshSessionCannotAutomaticallyOverwriteAnExistingSave()
        {
            string directory = Path.Combine(Application.temporaryCachePath, "equilibrium-existing-save-" + Guid.NewGuid().ToString("N"));
            var store = new FactorySaveStore(directory);
            store.Write(new FactorySession(16, 16).CaptureSave(DateTimeOffset.UtcNow));
            byte[] original = File.ReadAllBytes(store.PrimaryPath);
            var assets = ScriptableObject.CreateInstance<FactoryAssets>(); var host = new GameObject("Save protection test");
            try
            {
                var runtime = host.AddComponent<FactoryRuntime>(); runtime.Assets = assets;
                runtime.ConfigureSaveDirectory(directory); runtime.Initialize(); runtime.Pump.Paused = true;
                for (int i = 0; i < 300; i++) runtime.Session.Engine.Advance();
                yield return null;
                Assert.IsTrue(runtime.NeedsOverwriteConfirmation); Assert.IsFalse(runtime.RequestSave());
                Assert.IsFalse(runtime.SaveBusy); CollectionAssert.AreEqual(original, File.ReadAllBytes(store.PrimaryPath));
                Assert.IsTrue(runtime.RequestSave(true)); yield return WaitForSave(runtime);
                Assert.AreEqual(300UL, store.Read().Header.TickIndex); CollectionAssert.AreEqual(original, File.ReadAllBytes(store.PathFor(1)));
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(assets);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static IEnumerator WaitForSave(FactoryRuntime runtime)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while ((runtime.SavePending || runtime.SaveBusy) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(runtime.SavePending); Assert.IsFalse(runtime.SaveBusy);
            Assert.That(runtime.SaveStatus, Does.Contain("已保存"));
        }
        private static void Click(Button button)
        {
            using (var evt = new NavigationSubmitEvent()) { evt.target = button; button.SendEvent(evt); }
        }
    }
}

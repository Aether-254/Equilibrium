using System.Collections;
using System.Linq;
using Equilibrium.Presentation;
using Equilibrium.Simulation.World;
using Equilibrium.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Equilibrium.Tests.PlayMode.Logistics
{
    public sealed class FactorySceneTests
    {
        [UnityTest]
        public IEnumerator MainSceneBuildsAndTransportsRealInventoryWithRuntimeHud()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
#endif
            yield return null;
            var runtime = Object.FindAnyObjectByType<FactoryRuntime>();
            Assert.IsNotNull(runtime, "Main must contain an explicit factory entry point.");
            runtime.Pump.Paused = true;
            try
            {
                var hud = runtime.GetComponent<FactoryHud>(); var input = hud.Input; var s = runtime.Session;
                Assert.IsNotNull(hud.Document.panelSettings); Assert.IsNotNull(runtime.Assets.Belt); Assert.IsNotNull(runtime.Assets.Chest);
                foreach (var script in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                    if (script.GetType().Name == "MachinePlacer") Assert.IsFalse(script.enabled, "Only one placement input owner.");
                Assert.IsTrue(hud.HitTest(new Vector2(Screen.width / 2f, Screen.height - 10)));
                Assert.IsFalse(hud.HitTest(new Vector2(Screen.width / 4f, Screen.height / 2f)));
                var start = new CellRef(28, 30); var end = new CellRef(33, 30);
                var first = new CellRef(29, 30); var last = new CellRef(32, 30);
                Assert.AreEqual(start, runtime.CellAt(runtime.CellCenter(start)));
                input.SetTool(FactoryTool.Chest); input.PointerDown(start, Side.East, true);
                Assert.AreEqual(0, s.PendingCommands, "UI clicks must not leak into world edits.");
                input.PointerDown(start, Side.East, false); input.PointerDown(end, Side.West, false);
                input.SetTool(FactoryTool.Belt); input.PointerDown(first, Side.West, false);
                input.PointerMove(last, false); input.PointerUp(false);
                Assert.IsTrue(s.World.IsEmpty(first)); runtime.Step();
                Assert.AreEqual(6, s.World.BlockIndexCount); Assert.AreEqual(124, s.Stock("belt"));
                input.SetTool(FactoryTool.Accessor); input.PointerDown(first, Side.West, false); input.PointerDown(last, Side.East, false);
                s.QueueDeposit(start, s.Registry.Items.Parse("wood"), 32); runtime.Step();
                Assert.AreEqual(0, Count(runtime, first)); runtime.Step();
                Assert.AreEqual(16, Count(runtime, first)); Assert.IsTrue(s.Logistics.LastBatchSucceeded);
                Assert.Greater(runtime.WorldRenderer.VisibleGroupCount, 0);
                runtime.WorldRenderer.RenderInterpolation(0);
                Vector3 from = runtime.WorldRenderer.GroupPosition(0), to = runtime.WorldRenderer.GroupDestination(0);
                runtime.WorldRenderer.RenderInterpolation(0.5f);
                Assert.Less(Vector3.Distance(Vector3.Lerp(from, to, 0.5f), runtime.WorldRenderer.GroupPosition(0)), 0.0001f);
                input.Select(first, Side.West); Assert.That(hud.InspectionText, Does.Contain("equilibrium:wood"));
                for (int i = 0; i < 6; i++) runtime.Step();
                Assert.AreEqual(32, Count(runtime, end)); Assert.AreEqual(0, Count(runtime, start));
                Assert.AreEqual(224, s.Stock("wood"));
                input.Select(end, Side.West); Assert.That(hud.InspectionText, Does.Contain("32 / 64"));
                input.Select(first, Side.West);
                var root = hud.Document.rootVisualElement;
                root.Q<DropdownField>("accessor-flow").index = (int)AccessorFlow.ContainerToBelt;
                root.Q<DropdownField>("accessor-quantity").index = (int)AccessorQuantity.Keep;
                root.Q<IntegerField>("accessor-amount").value = 20;
                var filter = root.Q<TextField>("accessor-filter"); filter.value = "wood";
                filter.Focus(); yield return null;
                Assert.IsTrue(hud.EditingText, "Typing into the settings panel must suppress build hotkeys.");
                var apply = root.Q<Button>("accessor-apply");
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = apply; apply.SendEvent(submit); }
                var firstBlock = s.World.BlockAt(first);
                Assert.AreEqual(16, s.World.Accessor(firstBlock, Side.West).Amount, "Configuration waits for Commit.");
                Assert.Greater(s.PendingCommands, 0); runtime.Step();
                var configured = s.World.Accessor(firstBlock, Side.West);
                Assert.AreEqual(20, configured.Amount); Assert.AreEqual(AccessorQuantity.Keep, configured.Quantity);
                Assert.AreEqual("equilibrium:wood", configured.Filter.Canonical);
                // Keep a reproducible screenshot only when this Editor has a graphics device.
                if (!Application.isBatchMode && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    yield return new WaitForEndOfFrame();
                    var image = ScreenCapture.CaptureScreenshotAsTexture();
                    if (image != null)
                    {
                        System.IO.File.WriteAllBytes("Logs/factory-main-20261105.png", image.EncodeToPNG());
                        Object.Destroy(image);
                    }
                }
            }
            finally { Object.DestroyImmediate(runtime.gameObject); }
        }

        [UnityTest]
        public IEnumerator ReleasingBeltOverUiCancelsAndConfigurationUsesQueuedIntents()
        {
            var host = new GameObject("Factory input test"); var assets = ScriptableObject.CreateInstance<FactoryAssets>();
            try
            {
                var runtime = host.AddComponent<FactoryRuntime>(); runtime.Assets = assets; runtime.Initialize(); runtime.Pump.Paused = true;
                var input = host.AddComponent<FactoryInputController>(); input.Runtime = runtime;
                input.SetTool(FactoryTool.Belt); input.PointerDown(new CellRef(1, 1), Side.West, false);
                input.PointerMove(new CellRef(3, 1), false); input.PointerUp(true);
                Assert.AreEqual(0, runtime.Session.PendingCommands); Assert.IsFalse(input.Drag.Active);
                yield return null;
                Assert.AreEqual(0, runtime.Session.World.BlockIndexCount);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(assets); }
        }

        [UnityTest]
        public IEnumerator MainPoweredSawReceivesLogsAndDeliversPlanksThroughRealBelts()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
#endif
            yield return null;
            var runtime = Object.FindAnyObjectByType<FactoryRuntime>(); runtime.Pump.Paused = true;
            try
            {
                var hud = runtime.GetComponent<FactoryHud>(); var input = hud.Input; var s = runtime.Session;
                Assert.Greater(s.Stock("saw"), 0); Assert.Greater(s.Stock("engine"), 0); Assert.Greater(s.Stock("log"), 0);
                var from = new CellRef(25, 30); var saw = new CellRef(28, 30); var to = new CellRef(31, 30);
                var engine = new CellRef(28, 32);
                input.SetTool(FactoryTool.Chest); input.PointerDown(from, Side.West, false); input.PointerDown(to, Side.West, false);
                input.SetTool(FactoryTool.Saw); input.PointerDown(saw, Side.West, false);
                input.SetTool(FactoryTool.Engine); input.PointerDown(engine, Side.West, false);
                input.SetTool(FactoryTool.Shaft); input.PointerDown(new CellRef(28, 31), Side.North, false);
                input.SetTool(FactoryTool.Belt); input.PointerDown(new CellRef(26, 30), Side.West, false);
                input.PointerMove(new CellRef(27, 30), false); input.PointerUp(false);
                input.PointerDown(new CellRef(29, 30), Side.West, false); input.PointerMove(new CellRef(30, 30), false); input.PointerUp(false);
                Assert.IsTrue(s.World.IsEmpty(saw)); runtime.Step();
                input.SetTool(FactoryTool.Accessor); input.PointerDown(new CellRef(26, 30), Side.West, false);
                input.PointerDown(new CellRef(30, 30), Side.East, false);
                s.QueueDeposit(from, s.Registry.Items.Parse("log"), 1); s.QueueDeposit(engine, s.Registry.Items.Parse("wood"), 8);
                runtime.Step();
                for (int i = 0; i < 16; i++) runtime.Step();
                Assert.AreEqual(0, Count(runtime, from)); Assert.AreEqual(2, Count(runtime, to));
                var target = s.World.BlockAt(to);
                Assert.AreEqual(s.Registry.Items.Parse("plank").Handle, s.World.SlotItemHandle(s.World.SlotGlobal(target, 0)));
                Assert.AreEqual(1, s.Processing.State(s.World.BlockAt(saw)).CompletedBatches);
                input.SetTool(FactoryTool.Inspect); input.Select(saw, Side.North); hud.Refresh();
                StringAssert.Contains("锯床", hud.InspectionText); StringAssert.Contains("完成批次：1", hud.InspectionText);
                StringAssert.Contains("收到功率", hud.InspectionText);
                Assert.IsTrue(runtime.WorldRenderer.GetComponentsInChildren<TextMesh>().Any(t => t.text == "SAW"));
                yield return null;
            }
            finally { Object.DestroyImmediate(runtime.gameObject); }
        }

        private static int Count(FactoryRuntime runtime, CellRef cell)
        {
            var w = runtime.Session.World; var b = w.BlockAt(cell); int n = 0;
            for (int slot = 0; slot < w.SlotCount(b); slot++) n += w.SlotCountOf(w.SlotGlobal(b, slot));
            return n;
        }
    }
}

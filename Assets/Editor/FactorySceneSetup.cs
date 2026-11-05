using System;
using System.Linq;
using Equilibrium.Presentation;
using Equilibrium.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Equilibrium.Editor
{
    /// <summary>Explicit, idempotent opt-in. Never runs on import or when entering Play mode.</summary>
    public static class FactorySceneSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string AssetsPath = "Assets/Settings/FactoryPresentation.asset";
        public const string PanelPath = "Assets/Settings/FactoryPanelSettings.asset";

        public static void InstallBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use the menu for an open Editor.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Install();
        }

        [MenuItem("Equilibrium/Factory/Connect Main Scene")]
        public static void Install()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open Main.unity before connecting the factory.");
            var assets = AssetDatabase.LoadAssetAtPath<FactoryAssets>(AssetsPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<FactoryAssets>();
                assets.Belt = SpriteAt("Assets/Art/Conveyor/Conveyor_verticalr.png");
                // Existing white prototype sprite is a labelled stand-in, not final chest artwork.
                assets.Chest = SpriteAt("Assets/Script/Machine.png");
                AssetDatabase.CreateAsset(assets, AssetsPath);
            }
            if (assets.Belt == null || assets.Chest == null) throw new InvalidOperationException("Required sprite references are missing.");
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/FactoryTheme.tss");
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1280, 720);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; panel.match = 0.5f;
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            var runtimes = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<FactoryRuntime>(true)).ToArray();
            if (runtimes.Length > 1) throw new InvalidOperationException("Multiple factory runtimes in Main.");
            var runtime = runtimes.Length == 1 ? runtimes[0] : new GameObject("FactoryRuntime").AddComponent<FactoryRuntime>();
            Undo.RecordObject(runtime, "Connect factory");
            runtime.Assets = assets; runtime.WorldCamera = Camera.main;
            if (runtime.WorldCamera == null) throw new InvalidOperationException("MainCamera is missing.");
            var doc = runtime.GetComponent<UIDocument>() ?? Undo.AddComponent<UIDocument>(runtime.gameObject);
            doc.panelSettings = panel;
            var hud = runtime.GetComponent<FactoryHud>() ?? Undo.AddComponent<FactoryHud>(runtime.gameObject);
            hud.Runtime = runtime; hud.Styles = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/FactoryHud.uss");
            // Preserve legacy components, references and objects, but allow only one placement owner.
            foreach (var legacy in scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MachinePlacer>(true)))
            { Undo.RecordObject(legacy, "Disable legacy placement input"); legacy.enabled = false; EditorUtility.SetDirty(legacy); }
            EditorUtility.SetDirty(runtime); EditorUtility.SetDirty(doc); EditorUtility.SetDirty(hud);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Main scene did not save.");
            Debug.Log("FactorySceneSetup: Main connected; existing objects and prefab references retained.");
        }

        private static Sprite SpriteAt(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault()
               ?? throw new InvalidOperationException("No imported sprite at " + path);
    }
}

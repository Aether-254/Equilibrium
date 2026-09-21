using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShaftSpriteBakingMigration
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string LegacyFolder = "Assets/Art/Shaft/Baked";
    private const string SheetPath = LegacyFolder + "/shaft_sheet.png";
    private const int FrameCount = 32;
    private const int Columns = 8;

    [MenuItem("Tools/Equilibrium/Migrate Shaft Sprite Sheet")]
    public static void Run()
    {
        (int resolution, float pixelsPerUnit) = BuildSheetFromLegacyFrames();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject bakeRoot = FindRoot(scene, "ShaftRender");

        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(bakeRoot);
        ShaftSpriteBakerEditor.ConfigureSprites(
            SheetPath,
            FrameCount,
            resolution,
            Columns,
            pixelsPerUnit
        );
        NormalizeTextureImports(pixelsPerUnit);
        RepairPlaceholderPrefabSprites();
        AssignShaftFrames();

        bakeRoot.tag = "EditorOnly";
        EditorUtility.SetDirty(bakeRoot);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        DeleteLegacyFrames();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Debug.Log("Shaft Sprite Sheet 迁移完成：32 帧单图集、Point 导入、场景引用已更新。");
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        GameObject root = scene.GetRootGameObjects()
            .FirstOrDefault(candidate => candidate.name == name);
        if (root == null)
            throw new InvalidOperationException($"{ScenePath} 中没有根对象 {name}。");

        return root;
    }

    private static (int resolution, float pixelsPerUnit) BuildSheetFromLegacyFrames()
    {
        string[] framePaths = Enumerable.Range(0, FrameCount)
            .Select(i => $"{LegacyFolder}/shaft_{i:D3}.png")
            .ToArray();

        string missing = framePaths.FirstOrDefault(path => !File.Exists(path));
        if (missing != null)
            throw new FileNotFoundException("缺少旧 Shaft 帧，无法生成图集。", missing);

        TextureImporter firstImporter = AssetImporter.GetAtPath(framePaths[0]) as TextureImporter;
        float pixelsPerUnit = firstImporter != null
            ? firstImporter.spritePixelsPerUnit
            : 100f;

        Texture2D firstFrame = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        int resolution;
        try
        {
            if (!firstFrame.LoadImage(File.ReadAllBytes(framePaths[0]), false))
                throw new InvalidDataException($"无法读取 {framePaths[0]}");

            if (firstFrame.width != firstFrame.height)
                throw new InvalidDataException($"{framePaths[0]} 不是正方形帧。");

            resolution = firstFrame.width;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstFrame);
        }

        int rows = Mathf.CeilToInt((float)FrameCount / Columns);
        Texture2D atlas = new Texture2D(
            Columns * resolution,
            rows * resolution,
            TextureFormat.RGBA32,
            false
        );

        try
        {
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]);

            for (int i = 0; i < framePaths.Length; i++)
            {
                Texture2D frame = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!frame.LoadImage(File.ReadAllBytes(framePaths[i]), false))
                        throw new InvalidDataException($"无法读取 {framePaths[i]}");

                    if (frame.width != resolution || frame.height != resolution)
                    {
                        throw new InvalidDataException(
                            $"{framePaths[i]} 尺寸为 {frame.width}x{frame.height}，期望 {resolution}x{resolution}。"
                        );
                    }

                    atlas.SetPixels32(
                        (i % Columns) * resolution,
                        (i / Columns) * resolution,
                        resolution,
                        resolution,
                        frame.GetPixels32()
                    );
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(frame);
                }
            }

            atlas.Apply();
            File.WriteAllBytes(SheetPath, atlas.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(atlas);
        }

        return (resolution, pixelsPerUnit);
    }

    private static void NormalizeTextureImports(float shaftPixelsPerUnit)
    {
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });

        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            bool changed = false;
            changed |= SetIfDifferent(importer.filterMode, FilterMode.Point, value => importer.filterMode = value);

            if (importer.textureType == TextureImporterType.Sprite)
            {
                SpriteImportMode expectedMode = path == SheetPath
                    ? SpriteImportMode.Multiple
                    : SpriteImportMode.Single;

                changed |= SetIfDifferent(
                    importer.spriteImportMode,
                    expectedMode,
                    value => importer.spriteImportMode = value
                );
                changed |= SetIfDifferent(
                    importer.spritePixelsPerUnit,
                    GetPixelsPerUnit(path, shaftPixelsPerUnit),
                    value => importer.spritePixelsPerUnit = value
                );
                changed |= SetIfDifferent(
                    importer.mipmapEnabled,
                    false,
                    value => importer.mipmapEnabled = value
                );
                changed |= SetIfDifferent(
                    importer.textureCompression,
                    TextureImporterCompression.Uncompressed,
                    value => importer.textureCompression = value
                );
            }

            if (!changed)
                continue;

            importer.SaveAndReimport();
        }

        TextureImporter sheetImporter = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        if (sheetImporter != null)
        {
            sheetImporter.spriteImportMode = SpriteImportMode.Multiple;
            sheetImporter.SaveAndReimport();
        }
    }

    private static float GetPixelsPerUnit(string path, float shaftPixelsPerUnit)
    {
        if (path == SheetPath)
            return shaftPixelsPerUnit;

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
            return 32f;

        return Mathf.Max(texture.width, texture.height);
    }

    private static bool SetIfDifferent<T>(T current, T expected, Action<T> setter)
    {
        if (EqualityComparer<T>.Default.Equals(current, expected))
            return false;

        setter(expected);
        return true;
    }

    private static void RepairPlaceholderPrefabSprites()
    {
        RepairPrefabSprite("Assets/Script/Conveyor.prefab", "Assets/Script/Conveyor.png");
        RepairPrefabSprite("Assets/Script/Machine.prefab", "Assets/Script/Machine.png");
    }

    private static void RepairPrefabSprite(string prefabPath, string spritePath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
            throw new InvalidOperationException($"无法加载 Sprite：{spritePath}");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            SpriteRenderer renderer = prefabRoot.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null)
                throw new InvalidOperationException($"{prefabPath} 中没有 SpriteRenderer。");

            renderer.sprite = sprite;
            EditorUtility.SetDirty(renderer);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void AssignShaftFrames()
    {
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(SheetPath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();

        if (frames.Length != FrameCount)
            throw new InvalidOperationException($"Shaft 图集包含 {frames.Length} 帧，期望 {FrameCount} 帧。");

        ShaftSpriteVisual[] visuals =
            UnityEngine.Object.FindObjectsByType<ShaftSpriteVisual>(FindObjectsInactive.Include);

        foreach (ShaftSpriteVisual visual in visuals)
        {
            SerializedObject serialized = new SerializedObject(visual);
            SerializedProperty frameArray = serialized.FindProperty("frames");
            SerializedProperty rendererProperty = serialized.FindProperty("spriteRenderer");
            frameArray.arraySize = frames.Length;

            for (int i = 0; i < frames.Length; i++)
                frameArray.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();

            SpriteRenderer renderer = rendererProperty.objectReferenceValue as SpriteRenderer;
            if (renderer != null)
            {
                renderer.sprite = frames[0];
                EditorUtility.SetDirty(renderer);
            }

            EditorUtility.SetDirty(visual);
        }
    }

    private static void DeleteLegacyFrames()
    {
        for (int i = 0; i < 128; i++)
        {
            string path = $"{LegacyFolder}/shaft_{i:D3}.png";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
        }
    }
}

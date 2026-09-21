using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShaftSpriteBakerEditor
{
    [MenuItem("Tools/Equilibrium/Bake Shaft Sprite Sheet")]
    public static void BakeFromActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject bakeRoot = scene.GetRootGameObjects()
            .FirstOrDefault(candidate => candidate.name == "ShaftRender");
        if (bakeRoot == null)
            throw new InvalidDataException("当前场景没有 ShaftRender 根对象。");

        MeshRenderer shaftRenderer = bakeRoot.GetComponentInChildren<MeshRenderer>(true);
        Camera bakeCamera = bakeRoot.GetComponentsInChildren<Camera>(true)
            .FirstOrDefault(camera => camera.name == "ShaftCamera");
        if (shaftRenderer == null || bakeCamera == null)
            throw new InvalidDataException("ShaftRender 缺少 MeshRenderer 或 ShaftCamera。");

        int resolution = bakeCamera.targetTexture != null
            ? bakeCamera.targetTexture.width
            : 256;

        Bake(
            shaftRenderer.transform,
            bakeCamera,
            ShaftSpriteBakerSettings.FrameCount,
            resolution,
            ShaftSpriteBakerSettings.AtlasColumns,
            ShaftSpriteBakerSettings.PixelsPerUnit,
            ShaftSpriteBakerSettings.OutputPath
        );

        bakeRoot.tag = "EditorOnly";
        EditorUtility.SetDirty(bakeRoot);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    public static void Bake(
        Transform shaft,
        Camera bakeCamera,
        int frameCount,
        int resolution,
        int atlasColumns,
        float pixelsPerUnit,
        string outputPath
    )
    {
        if (shaft == null || bakeCamera == null)
            throw new InvalidDataException("没有指定 Shaft 或 Bake Camera。");
        if (frameCount < 1 || resolution < 1 || atlasColumns < 1)
            throw new InvalidDataException("Bake 设置必须大于零。");

        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(outputDirectory))
            throw new InvalidDataException("输出路径无效。");

        Directory.CreateDirectory(outputDirectory);

        int columns = Mathf.Min(atlasColumns, frameCount);
        int rows = Mathf.CeilToInt((float)frameCount / columns);
        RenderTexture renderTexture = new RenderTexture(
            resolution,
            resolution,
            24,
            RenderTextureFormat.ARGB32
        )
        {
            antiAliasing = 1,
            filterMode = FilterMode.Point
        };
        Texture2D atlas = new Texture2D(
            columns * resolution,
            rows * resolution,
            TextureFormat.RGBA32,
            false
        )
        {
            filterMode = FilterMode.Point
        };

        RenderTexture oldTarget = bakeCamera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        Quaternion oldRotation = shaft.localRotation;

        try
        {
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
            bakeCamera.targetTexture = renderTexture;

            for (int i = 0; i < frameCount; i++)
            {
                shaft.localRotation = Quaternion.Euler(0f, 360f / frameCount * i, 0f);
                bakeCamera.Render();
                RenderTexture.active = renderTexture;

                Texture2D frame = new Texture2D(
                    resolution,
                    resolution,
                    TextureFormat.RGBA32,
                    false
                );
                try
                {
                    frame.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                    frame.Apply();
                    atlas.SetPixels32(
                        i % columns * resolution,
                        i / columns * resolution,
                        resolution,
                        resolution,
                        frame.GetPixels32()
                    );
                }
                finally
                {
                    Object.DestroyImmediate(frame);
                }
            }

            atlas.Apply();
            File.WriteAllBytes(outputPath, atlas.EncodeToPNG());
        }
        finally
        {
            shaft.localRotation = oldRotation;
            bakeCamera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(atlas);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureSprites(
            outputPath,
            frameCount,
            resolution,
            atlasColumns,
            pixelsPerUnit
        );
        Debug.Log($"Shaft bake 完成：{frameCount} 帧，输出 {outputPath}");
    }

    public static void ConfigureSprites(
        string outputPath,
        int frameCount,
        int resolution,
        int atlasColumns,
        float pixelsPerUnit
    )
    {
        TextureImporter importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
        if (importer == null)
            throw new InvalidDataException($"无法读取 Sprite 导入器：{outputPath}");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        int columns = Mathf.Min(atlasColumns, frameCount);
        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider =
            factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        Dictionary<string, GUID> existingIds = provider.GetSpriteRects()
            .ToDictionary(rect => rect.name, rect => rect.spriteID);
        SpriteRect[] spriteRects = new SpriteRect[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            string name = $"shaft_{i:D3}";
            spriteRects[i] = new SpriteRect
            {
                name = name,
                rect = new Rect(
                    i % columns * resolution,
                    i / columns * resolution,
                    resolution,
                    resolution
                ),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = existingIds.TryGetValue(name, out GUID id)
                    ? id
                    : GUID.Generate()
            };
        }

        provider.SetSpriteRects(spriteRects);
        provider.Apply();
        importer.SaveAndReimport();
    }
}

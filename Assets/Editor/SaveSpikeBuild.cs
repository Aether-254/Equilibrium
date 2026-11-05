using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

namespace Equilibrium.Editor
{
    public static class SaveSpikeBuild
    {
        public static void Build()
        {
            if (!UnityEngine.Application.isBatchMode)
                throw new InvalidOperationException("Run this probe through unity build in a separate batch Editor");
            string output = Argument("-buildOutput");
            if (string.IsNullOrEmpty(output)) throw new ArgumentException("Pass -buildOutput via unity build --output-path");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var target = NamedBuildTarget.Standalone;
            var oldBackend = PlayerSettings.GetScriptingBackend(target);
            bool changedBackend = false;
            string folder = "Assets/__SaveSpike_" + Guid.NewGuid().ToString("N");
            try
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
                // A fresh batch Editor starts with an untitled scene; Additive refuses that state.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                string scenePath = folder + "/Probe.unity";
                if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new IOException("Cannot save probe scene");
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                changedBackend = true;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scenePath },
                    locationPathName = Path.GetFullPath(output),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development,
                    extraScriptingDefines = new[] { "EQ_SAVE_SPIKE" }
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Save spike build failed: " + report.summary.result);
            }
            finally
            {
                if (changedBackend) PlayerSettings.SetScriptingBackend(target, oldBackend);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.SaveAssets();
            }
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}

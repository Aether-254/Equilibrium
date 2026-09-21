#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Equilibrium.EditorTools
{
    /// <summary>
    /// Commits and pushes the repository automatically:
    /// after asset saves (debounced), on a fixed interval, and when the editor quits.
    /// Toggle and tune from Tools > Git Auto Sync.
    /// </summary>
    [InitializeOnLoad]
    public static class GitAutoSync
    {
        const string EnabledKey = "GitAutoSync.Enabled";
        const string PushKey = "GitAutoSync.Push";
        const string IntervalKey = "GitAutoSync.IntervalMinutes";
        const string SshKey = "GitAutoSync.SshCommand";
        const string AiKey = "GitAutoSync.AiMessages";
        const string AiScriptKey = "GitAutoSync.AiScript";

        // SOCKS proxy for SSH; leave empty if your ~/.ssh/config already works.
        const string DefaultSsh = "ssh -o \"ProxyCommand=connect -S 127.0.0.1:2080 %h %p\"";
        const string DefaultAiScript = "%USERPROFILE%/bin/git-ai-commit.mjs";

        static double nextTick;
        static double debounceUntil = -1;
        static bool running;

        static bool Enabled { get => EditorPrefs.GetBool(EnabledKey, false); set => EditorPrefs.SetBool(EnabledKey, value); }
        static bool Push { get => EditorPrefs.GetBool(PushKey, true); set => EditorPrefs.SetBool(PushKey, value); }
        static int IntervalMinutes { get => EditorPrefs.GetInt(IntervalKey, 10); set => EditorPrefs.SetInt(IntervalKey, value); }
        static string SshCommand { get => EditorPrefs.GetString(SshKey, DefaultSsh); set => EditorPrefs.SetString(SshKey, value); }
        static bool AiMessages { get => EditorPrefs.GetBool(AiKey, true); set => EditorPrefs.SetBool(AiKey, value); }
        static string AiScript => Environment.ExpandEnvironmentVariables(EditorPrefs.GetString(AiScriptKey, DefaultAiScript));

        static string RepoRoot => System.IO.Path.GetDirectoryName(Application.dataPath);

        static GitAutoSync()
        {
            nextTick = EditorApplication.timeSinceStartup + IntervalMinutes * 60.0;
            EditorApplication.update += OnUpdate;
            EditorApplication.quitting += () => { if (Enabled) SyncBlocking(); };
        }

        [MenuItem("Tools/Git Auto Sync/Enabled")]
        static void ToggleEnabled() => Enabled = !Enabled;
        [MenuItem("Tools/Git Auto Sync/Enabled", true)]
        static bool ToggleEnabledValidate() { Menu.SetChecked("Tools/Git Auto Sync/Enabled", Enabled); return true; }

        [MenuItem("Tools/Git Auto Sync/Push After Commit")]
        static void TogglePush() => Push = !Push;
        [MenuItem("Tools/Git Auto Sync/Push After Commit", true)]
        static bool TogglePushValidate() { Menu.SetChecked("Tools/Git Auto Sync/Push After Commit", Push); return true; }

        [MenuItem("Tools/Git Auto Sync/AI Commit Messages")]
        static void ToggleAi() => AiMessages = !AiMessages;
        [MenuItem("Tools/Git Auto Sync/AI Commit Messages", true)]
        static bool ToggleAiValidate() { Menu.SetChecked("Tools/Git Auto Sync/AI Commit Messages", AiMessages); return true; }

        [MenuItem("Tools/Git Auto Sync/Interval: 5 min")] static void Interval5() => IntervalMinutes = 5;
        [MenuItem("Tools/Git Auto Sync/Interval: 10 min")] static void Interval10() => IntervalMinutes = 10;
        [MenuItem("Tools/Git Auto Sync/Interval: 30 min")] static void Interval30() => IntervalMinutes = 30;

        [MenuItem("Tools/Git Auto Sync/Sync Now %#g")]
        static void SyncNowMenu() => _ = SyncAsync("manual");

        [MenuItem("Tools/Git Auto Sync/Clear SSH Proxy Override")]
        static void ClearSsh() => SshCommand = "";

        static void OnUpdate()
        {
            if (!Enabled || running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            var now = EditorApplication.timeSinceStartup;
            if (debounceUntil > 0 && now >= debounceUntil)
            {
                debounceUntil = -1;
                _ = SyncAsync("save");
            }
            else if (now >= nextTick)
            {
                nextTick = now + IntervalMinutes * 60.0;
                _ = SyncAsync("interval");
            }
        }

        // Debounce so a burst of saves (Ctrl+S on scene + prefabs) yields one commit.
        class SaveHook : AssetModificationProcessor
        {
            static string[] OnWillSaveAssets(string[] paths)
            {
                if (Enabled) debounceUntil = EditorApplication.timeSinceStartup + 20.0;
                return paths;
            }
        }

        static async Task SyncAsync(string reason)
        {
            if (running) return;
            running = true;
            try
            {
                var status = await Git("status --porcelain");
                if (status.code != 0) { Debug.LogWarning($"[GitAutoSync] git status failed: {status.err}"); return; }
                if (string.IsNullOrWhiteSpace(status.output)) return;

                var add = await Git("add -A");
                if (add.code != 0) { Debug.LogWarning($"[GitAutoSync] add failed: {add.err}"); return; }

                var fallback = $"auto({reason}): {DateTime.Now:yyyy-MM-dd HH:mm}";
                var msg = AiMessages ? await GenerateMessage(reason) : null;
                if (string.IsNullOrWhiteSpace(msg)) msg = fallback;

                var msgFile = System.IO.Path.Combine(RepoRoot, ".git", "AUTOSYNC_MSG");
                System.IO.File.WriteAllText(msgFile, msg + "\n", new UTF8Encoding(false));
                var commit = await Git("commit -q -F .git/AUTOSYNC_MSG");
                if (commit.code != 0) { Debug.LogWarning($"[GitAutoSync] commit failed: {commit.err}"); return; }
                Debug.Log($"[GitAutoSync] committed: {msg.Split('\n')[0]}");

                if (!Push) return;
                var pull = await Git("pull --rebase --autostash -q");
                if (pull.code != 0) { Debug.LogWarning($"[GitAutoSync] pull --rebase failed, not pushing:\n{pull.err}"); return; }
                var push = await Git("push -q");
                if (push.code != 0) Debug.LogWarning($"[GitAutoSync] push failed:\n{push.err}");
                else Debug.Log("[GitAutoSync] pushed.");
            }
            finally { running = false; }
        }

        // Runs the external node script against the staged diff; any failure falls back to a timestamp message.
        static async Task<string> GenerateMessage(string reason)
        {
            var script = AiScript;
            if (!System.IO.File.Exists(script)) { Debug.LogWarning($"[GitAutoSync] AI script not found: {script}"); return null; }
            var r = await Run("node", $"\"{script}\" --hint \"Unity editor auto-sync ({reason})\"", env =>
            {
                env["NODE_USE_ENV_PROXY"] = "1";
                if (string.IsNullOrEmpty(GetEnv(env, "HTTPS_PROXY"))) env["HTTPS_PROXY"] = "http://127.0.0.1:2080";
            }, 90_000);
            if (r.code != 0) { Debug.LogWarning($"[GitAutoSync] AI message failed ({r.code}): {r.err.Trim()}"); return null; }
            return r.output.Trim();
        }

        static string GetEnv(System.Collections.Generic.IDictionary<string, string> env, string k) => env.TryGetValue(k, out var v) ? v : null;

        static void SyncBlocking()
        {
            try { SyncAsync("quit").GetAwaiter().GetResult(); } catch (Exception e) { Debug.LogWarning($"[GitAutoSync] {e.Message}"); }
        }

        static Task<(int code, string output, string err)> Git(string args)
        {
            return Run("git", args, env =>
            {
                var ssh = SshCommand;
                if (!string.IsNullOrEmpty(ssh)) env["GIT_SSH_COMMAND"] = ssh;
                env["GIT_TERMINAL_PROMPT"] = "0";
            }, 120_000);
        }

        static Task<(int code, string output, string err)> Run(string exe, string args, Action<System.Collections.Generic.IDictionary<string, string>> configureEnv, int timeoutMs)
        {
            return Task.Run(() =>
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    WorkingDirectory = RepoRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                configureEnv?.Invoke(psi.Environment);

                using var p = Process.Start(psi);
                var o = p.StandardOutput.ReadToEndAsync();
                var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return (-1, "", "timeout"); }
                return (p.ExitCode, o.Result, e.Result);
            });
        }
    }
}
#endif

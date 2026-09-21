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

        // SOCKS proxy for SSH; leave empty if your ~/.ssh/config already works.
        const string DefaultSsh = "ssh -o \"ProxyCommand=connect -S 127.0.0.1:2080 %h %p\"";

        static double nextTick;
        static double debounceUntil = -1;
        static bool running;

        static bool Enabled { get => EditorPrefs.GetBool(EnabledKey, false); set => EditorPrefs.SetBool(EnabledKey, value); }
        static bool Push { get => EditorPrefs.GetBool(PushKey, true); set => EditorPrefs.SetBool(PushKey, value); }
        static int IntervalMinutes { get => EditorPrefs.GetInt(IntervalKey, 10); set => EditorPrefs.SetInt(IntervalKey, value); }
        static string SshCommand { get => EditorPrefs.GetString(SshKey, DefaultSsh); set => EditorPrefs.SetString(SshKey, value); }

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

                var msg = $"auto({reason}): {DateTime.Now:yyyy-MM-dd HH:mm}";
                var commit = await Git($"commit -q -m \"{msg}\"");
                if (commit.code != 0) { Debug.LogWarning($"[GitAutoSync] commit failed: {commit.err}"); return; }
                Debug.Log($"[GitAutoSync] committed: {msg}");

                if (!Push) return;
                var pull = await Git("pull --rebase --autostash -q");
                if (pull.code != 0) { Debug.LogWarning($"[GitAutoSync] pull --rebase failed, not pushing:\n{pull.err}"); return; }
                var push = await Git("push -q");
                if (push.code != 0) Debug.LogWarning($"[GitAutoSync] push failed:\n{push.err}");
                else Debug.Log("[GitAutoSync] pushed.");
            }
            finally { running = false; }
        }

        static void SyncBlocking()
        {
            try { SyncAsync("quit").GetAwaiter().GetResult(); } catch (Exception e) { Debug.LogWarning($"[GitAutoSync] {e.Message}"); }
        }

        static Task<(int code, string output, string err)> Git(string args)
        {
            return Task.Run(() =>
            {
                var psi = new ProcessStartInfo("git", args)
                {
                    WorkingDirectory = RepoRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                var ssh = SshCommand;
                if (!string.IsNullOrEmpty(ssh)) psi.Environment["GIT_SSH_COMMAND"] = ssh;
                psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

                using var p = Process.Start(psi);
                var o = p.StandardOutput.ReadToEndAsync();
                var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(120_000)) { try { p.Kill(); } catch { } return (-1, "", "timeout"); }
                return (p.ExitCode, o.Result, e.Result);
            });
        }
    }
}
#endif

using System;
using System.IO;
using System.Threading.Tasks;
using Equilibrium.Editing;
using Equilibrium.Simulation.Save;
using UnityEngine;

namespace Equilibrium.Presentation
{
    public sealed partial class FactoryRuntime
    {
        private FactorySaveStore _store;
        private FactorySaveService _saves;
        private bool _ownsSaveSlot, _destroyed;
        private string _saveDirectory;
        public bool IsLoading { get; private set; }
        public bool SaveBusy => _saves?.Busy == true;
        public bool SavePending => _saves?.Pending == true;
        public bool NeedsOverwriteConfirmation => !_ownsSaveSlot && _store.HasAnySave;
        public string SaveStatus { get; private set; } = "尚未保存";
        public string SaveDirectory => _store?.DirectoryPath;
        public int SuggestedBackup { get; private set; }
        public event Action SessionReplaced;

        public void ConfigureSaveDirectory(string directory)
        {
            if (IsLoading || SaveBusy || SavePending) throw new InvalidOperationException("Save/load operation is active.");
            _saveDirectory = directory ?? throw new ArgumentNullException(nameof(directory));
            if (Session != null) InitializePersistence();
        }

        private void InitializePersistence()
        {
            _store = new FactorySaveStore(_saveDirectory ?? Path.Combine(Application.persistentDataPath, "saves", "local"));
            _ownsSaveSlot = !_store.HasAnySave;
            BindSaves();
            SaveStatus = _ownsSaveSlot ? "尚未保存 · 每 300 Tick 自动保存" : "发现已有存档 · 请读取或确认覆盖，自动保存暂未启用";
        }

        private void BindSaves()
        {
            _saves?.Dispose();
            _saves = new FactorySaveService(Session, _store.WriteAsync, _ownsSaveSlot);
            _saves.Changed += SaveChanged;
            _saves.Saved += document => { SaveDocumentSession.Publish(document); SuggestedBackup = 0; };
        }

        private void Update() => _saves?.Poll();

        private void SaveChanged()
        {
            if (_destroyed) return;
            SaveStatus = _saves.LastError != null ? "保存失败 · 原存档保留，请检查磁盘空间和文件权限" :
                _saves.Busy ? "正在保存…" :
                _saves.Pending ? "等待本 Tick 提交后保存；暂停时可点击单步" :
                _saves.LastSavedTick.HasValue ? "已保存 Tick " + _saves.LastSavedTick.Value : "尚未保存";
            Changed?.Invoke();
        }

        public bool RequestSave(bool confirmOverwrite = false)
        {
            Initialize();
            if (IsLoading) return false;
            if (NeedsOverwriteConfirmation && !confirmOverwrite) return false;
            _ownsSaveSlot = true; _saves.AutosaveEnabled = true; _saves.RequestSave(); return true;
        }

        public async Task<bool> LoadSaveAsync(int backup = 0)
        {
            Initialize();
            if (IsLoading || SaveBusy || SavePending)
            { SaveStatus = "请等待当前保存或读取完成"; Changed?.Invoke(); return false; }
            IsLoading = true; bool wasPaused = Pump.Paused; Pump.Paused = true;
            SaveStatus = backup == 0 ? "正在验证并读取存档…" : "正在验证并读取备份 " + backup + "…";
            Changed?.Invoke();
            var store = _store;
            try
            {
                int width = Width, height = Height;
                var loaded = await Task.Run(() => {
                    var document = store.Read(backup);
                    return (session: FactorySession.FromSave(document, width, height), document);
                });
                if (_destroyed) return false;
                Session.Engine.TickBoundary -= OnBoundary; _saves.Dispose();
                Session = loaded.session; Pump.Attach(Session.Engine); Pump.Paused = true;
                WorldRenderer.Initialize(this); WorldRenderer.Snap();
                Session.Engine.TickBoundary += OnBoundary;
                _ownsSaveSlot = true; BindSaves(); SaveDocumentSession.Publish(loaded.document);
                SuggestedBackup = 0; SessionReplaced?.Invoke();
                SaveStatus = (backup == 0 ? "已读取" : "已从备份 " + backup + " 恢复") + " Tick " + Session.Engine.TickIndex + " · 已暂停";
                return true;
            }
            catch (Exception error)
            {
                if (_destroyed) return false;
                Pump.Paused = wasPaused; SuggestedBackup = 0;
                // Report availability only; never silently replace the world with an older backup.
                int width = Width, height = Height;
                SuggestedBackup = await Task.Run(() => {
                    for (int i = 1; i <= 3; i++)
                        try { FactorySession.FromSave(store.Read(i), width, height); return i; }
                        catch (Exception) { }
                    return 0;
                });
                SaveStatus = "读取失败，当前工厂未替换" + (SuggestedBackup > 0 ? " · 可恢复备份 " + SuggestedBackup : " · 没有可用备份");
                Debug.LogWarning("Factory save could not be loaded: " + error.Message);
                return false;
            }
            finally
            {
                if (!_destroyed) { IsLoading = false; Changed?.Invoke(); }
            }
        }
    }
}

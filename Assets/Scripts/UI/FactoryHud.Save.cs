using System;
using Equilibrium.Simulation.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace Equilibrium.UI
{
    public sealed partial class FactoryHud
    {
        private Label _saveStatus;
        private Button _save, _load, _backup;
        private VisualElement _modal;
        private bool _modalWasPaused;
        public bool HasModal => _modal != null;

        private void AddSaveControls(VisualElement row)
        {
            _save = Button(row, "保存  F5", SaveClicked); _save.name = "factory-save";
            _load = Button(row, "读取  F9", LoadClicked); _load.name = "factory-load";
            _backup = Button(row, "恢复备份", () => ConfirmLoad(Runtime.SuggestedBackup)); _backup.name = "factory-load-backup";
        }

        private void RefreshSaveControls()
        {
            _saveStatus.text = Runtime.SaveStatus;
            _save.SetEnabled(!Runtime.IsLoading);
            _load.SetEnabled(!Runtime.IsLoading && !Runtime.SaveBusy && !Runtime.SavePending);
            _backup.SetEnabled(!Runtime.IsLoading && !Runtime.SaveBusy && !Runtime.SavePending);
            _backup.style.display = Runtime.SuggestedBackup > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _backup.text = "恢复备份 " + Runtime.SuggestedBackup;
            _toolbar.SetEnabled(!Runtime.IsLoading); _inspector.SetEnabled(!Runtime.IsLoading);
        }

        public void SaveClicked()
        {
            if (Runtime.IsLoading || HasModal) return;
            if (Runtime.NeedsOverwriteConfirmation)
                Confirm("覆盖现有存档？", "当前工厂将写入本机存档槽。原存档会进入备份，之后启用当前会话的自动保存。", () => Runtime.RequestSave(true));
            else Runtime.RequestSave();
        }

        public void LoadClicked() => ConfirmLoad(0);

        private void ConfirmLoad(int backup)
        {
            if (Runtime.IsLoading || Runtime.SaveBusy || Runtime.SavePending || HasModal) return;
            Confirm(backup == 0 ? "读取本机存档？" : "恢复备份 " + backup + "？",
                "读取成功后会替换当前工厂，未保存的变化将丢失。读取失败会保留当前工厂。",
                async () => { await Runtime.LoadSaveAsync(backup); });
        }

        private void Confirm(string title, string message, Action accepted)
        {
            Input.CancelDrag(); _modalWasPaused = Runtime.Pump.Paused; Runtime.Pump.Paused = true;
            _modal = new VisualElement { name = "save-confirmation" }; _root.Add(_modal);
            var panel = new VisualElement { name = "save-confirmation-panel" }; _modal.Add(panel);
            panel.Add(new Label(title) { name = "save-confirmation-title" }); panel.Add(new Label(message));
            var actions = Row(panel);
            var cancel = Button(actions, "取消", CloseConfirmation); cancel.name = "save-cancel";
            Button(actions, "确认", () => { CloseConfirmation(); accepted(); }).name = "save-confirm";
            _modal.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Escape) { CloseConfirmation(); evt.StopPropagation(); }
            });
            cancel.Focus(); Refresh();
        }

        private void CloseConfirmation()
        {
            if (_modal == null) return;
            _modal.RemoveFromHierarchy(); _modal = null; Runtime.Pump.Paused = _modalWasPaused; Refresh();
        }

        private void LoadedSession()
        { Input.CancelDrag(); Input.SetTool(FactoryTool.Inspect); Input.Select(CellRef.None, Side.West); }
    }
}

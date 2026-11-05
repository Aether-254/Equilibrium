using System;
using System.Collections.Generic;
using Equilibrium.Content;
using Equilibrium.Simulation;
using Equilibrium.Simulation.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Equilibrium.Editor
{
    /// <summary>
    /// Editor-only, disposable logistics workbench over the production simulation. It never edits
    /// scenes or content assets. Real item transfers drive the labels and one-second slide animation.
    /// </summary>
    public sealed class LogisticsWorkbench : EditorWindow
    {
        private WorldWriter _writer;
        private WorldView _world;
        private TickEngine _engine;
        private LogisticsSystem _logistics;
        private ItemId _wood;
        private readonly BlockRef[] _blocks = new BlockRef[6];
        private readonly Label[] _counts = new Label[6];
        private readonly Button[] _cells = new Button[6];
        private readonly List<(Label label, int source, int target)> _slides = new List<(Label, int, int)>();
        private Label _summary, _inspection;
        private VisualElement _track;
        private Button _run, _block;
        private bool _running, _blocked;
        private int _selected = 1, _collected, _total;
        private double _previousTime, _accumulator, _animationStart;
        private const float CellWidth = 104;

        public ulong TickIndex => _engine?.TickIndex ?? 0;
        public string InspectionText => _inspection?.text ?? "";

        [MenuItem("Equilibrium/Logistics/Open Workbench")]
        public static void Open() => GetWindow<LogisticsWorkbench>("物流工作台").Show();

        private void OnEnable()
        {
            minSize = new Vector2(680, 390);
            _previousTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += UpdateWorkbench;
        }

        private void OnDisable() { EditorApplication.update -= UpdateWorkbench; _running = false; }

        public void CreateGUI()
        {
            var root = rootVisualElement; root.Clear(); root.style.paddingLeft = root.style.paddingRight = 16;
            root.style.paddingTop = 14; root.style.backgroundColor = new Color(0.12f, 0.14f, 0.16f);
            var heading = new Label("箱子 → 存取器 → 传送带 → 存取器 → 箱子");
            heading.style.fontSize = 18; heading.style.marginBottom = 10; root.Add(heading);
            var note = new Label("独立调试世界 · 1 Tick = 1 秒 · 整组输送 · 点选方块查看真实状态");
            note.style.marginBottom = 12; root.Add(note);
            var controls = new VisualElement(); controls.style.flexDirection = FlexDirection.Row; root.Add(controls);
            _run = AddButton(controls, "运行", () => { _running = !_running; _run.text = _running ? "暂停" : "运行"; _previousTime = EditorApplication.timeSinceStartup; });
            AddButton(controls, "单步", AdvanceOneTick);
            _block = AddButton(controls, "阻断中段入口", () => SetBlocked(!_blocked));
            AddButton(controls, "收走输出", CollectOutput);
            AddButton(controls, "重置", () => ResetLine());
            AddButton(controls, "满载出口场景", () => ResetLine(true));
            _summary = new Label(); _summary.name = "logistics-summary"; _summary.style.marginTop = 12; root.Add(_summary);
            _track = new VisualElement(); _track.name = "logistics-track";
            _track.style.width = CellWidth * 6; _track.style.height = 132; _track.style.marginTop = 16; root.Add(_track);
            var beltSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Conveyor/Conveyor_verticalr.png");
            for (int i = 0; i < 6; i++)
            {
                int index = i;
                var cell = new Button(() => SelectCell(index)); cell.name = "logistics-cell-" + i;
                cell.style.position = Position.Absolute; cell.style.left = CellWidth * i;
                cell.style.top = 0; cell.style.width = CellWidth - 8; cell.style.height = 104;
                cell.style.paddingTop = 6; cell.style.justifyContent = Justify.FlexStart;
                cell.Add(new Label(i == 0 ? "输入箱" : i == 5 ? "输出箱" : "传送带 " + i));
                if (i > 0 && i < 5 && beltSprite != null)
                {
                    var image = new Image { sprite = beltSprite, scaleMode = ScaleMode.ScaleToFit };
                    image.style.width = 64; image.style.height = 40; image.style.alignSelf = Align.Center;
                    image.pickingMode = PickingMode.Ignore; cell.Add(image);
                }
                else cell.Add(new Label("物品容器"));
                _counts[i] = new Label(); _counts[i].name = "logistics-count-" + i; cell.Add(_counts[i]);
                _cells[i] = cell; _track.Add(cell);
            }
            _inspection = new Label(); _inspection.name = "logistics-inspection";
            _inspection.style.whiteSpace = WhiteSpace.Normal; _inspection.style.marginTop = 8;
            _inspection.style.fontSize = 13; root.Add(_inspection);
            ResetLine();
        }

        private static Button AddButton(VisualElement row, string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.height = 28; button.style.marginRight = 6; row.Add(button); return button;
        }

        public void ResetLine(bool fullOutput = false)
        {
            _running = _blocked = false; _accumulator = 0; _collected = 0;
            var registry = new ContentRegistry();
            var report = new ContentLoader().Load(registry, Tier1Content.Build(), Array.Empty<ContentPack>());
            if (report.BaseRejected) throw new InvalidOperationException("Tier 1 content failed validation.");
            _wood = registry.Items.Parse("wood");
            var definitions = new BlockDefTable();
            var belt = definitions.Register(BlockDefinition.Belt("belt", registry.Items.Parse("belt").Handle));
            var chest = definitions.Register(BlockDefinition.Chest("chest", registry.Items.Parse("chest").Handle, 4, 200));
            _writer = new WorldWriter(new WorldState(6, 1, registry), definitions); _world = _writer.View;
            for (int i = 0; i < 6; i++)
                _writer.Place(i == 0 || i == 5 ? chest : belt, new CellRef(i, 0), 0, null, out _blocks[i]);
            _writer.Insert(_world.SlotGlobal(_blocks[0], 0), _world.SlotGlobal(_blocks[0], 0) + 4, _wood, 128);
            if (fullOutput) _writer.Insert(_world.SlotGlobal(_blocks[5], 0), _world.SlotGlobal(_blocks[5], 0) + 4, _wood, 256);
            _total = 128 + (fullOutput ? 256 : 0);
            _writer.ConfigureAccessor(_blocks[1], Side.West, new AccessorConfiguration(amount: 16));
            _writer.ConfigureAccessor(_blocks[4], Side.East, new AccessorConfiguration());
            _logistics = new LogisticsSystem(_writer, definitions);
            _engine = new TickEngine(_world, _writer, new TieBreakSource()); _logistics.Attach(_engine);
            _run.text = "运行"; _block.text = "阻断中段入口"; ClearSlides(); Refresh();
        }

        public int CountAt(int cell)
        {
            int start = _world.SlotGlobal(_blocks[cell], 0), count = 0;
            for (int s = start; s < start + _world.SlotCount(_blocks[cell]); s++) count += _world.SlotCountOf(s);
            return count;
        }

        public void AdvanceOneTick()
        {
            if (_engine == null) return;
            _engine.Advance(); _accumulator = 0; _animationStart = EditorApplication.timeSinceStartup;
            ClearSlides();
            foreach (var move in _logistics.Transfers)
            {
                int source = _world.AnchorOf(move.Source).X, target = _world.AnchorOf(move.Target).X;
                var label = new Label("木 ×" + move.Count) { pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute; label.style.top = 108; label.style.fontSize = 12;
                label.style.color = new Color(0.91f, 0.73f, 0.42f); _track.Add(label);
                _slides.Add((label, source, target));
            }
            Refresh(); Animate(0);
        }

        public void SetBlocked(bool blocked)
        {
            _blocked = blocked; _writer.SetIngress(new CellRef(3, 0), Side.West, !blocked);
            _block.text = blocked ? "恢复中段入口" : "阻断中段入口"; Refresh();
        }

        public void CollectOutput()
        {
            int start = _world.SlotGlobal(_blocks[5], 0);
            _collected += _writer.Extract(start, start + 4, _wood, int.MaxValue); Refresh();
        }

        public void SelectCell(int cell) { _selected = Mathf.Clamp(cell, 0, 5); Refresh(); }

        private void UpdateWorkbench()
        {
            double now = EditorApplication.timeSinceStartup;
            double delta = Math.Max(0, now - _previousTime); _previousTime = now;
            if (_engine == null || _track == null) return;
            if (_running)
            {
                _accumulator += Math.Min(delta, 1); // Workbench never catches up a sleeping editor.
                if (_accumulator >= 1) AdvanceOneTick();
            }
            Animate((float)(now - _animationStart));
        }

        private void Animate(float fraction)
        {
            foreach (var slide in _slides)
                slide.label.style.left = CellWidth * Mathf.Lerp(slide.source, slide.target, Mathf.Clamp01(fraction)) + 12;
            if (fraction >= 1) ClearSlides();
        }

        private void ClearSlides() { foreach (var slide in _slides) slide.label.RemoveFromHierarchy(); _slides.Clear(); }

        private void Refresh()
        {
            if (_summary == null || _engine == null) return;
            int total = _collected;
            for (int i = 0; i < 6; i++)
            {
                int count = CountAt(i); total += count; _counts[i].text = "木 × " + count;
                bool stalled = i > 0 && i < 5 && _logistics.BeltResult(_blocks[i]).Stalled;
                _cells[i].style.backgroundColor = stalled ? new Color(0.43f, 0.23f, 0.10f) : new Color(0.20f, 0.23f, 0.25f);
                _cells[i].style.borderBottomColor = i == _selected ? new Color(0.35f, 0.75f, 0.95f) : Color.clear;
                _cells[i].style.borderBottomWidth = 3;
            }
            _summary.text = $"Tick {TickIndex}    场内 + 已收走：{total}/{_total}    已收走：{_collected}    中段入口：{(_blocked ? "阻断" : "开放")}";
            var selected = _blocks[_selected];
            if (_selected == 0 || _selected == 5)
                _inspection.text = $"{(_selected == 0 ? "输入箱" : "输出箱")} · {_world.SlotCount(selected)} 个槽位 · 当前 {CountAt(_selected)} 个木料\n每槽有效容量 min(200, wood.maxStack 64) = 64 · 普通箱子通过存取器交换物品";
            else
            {
                var state = _logistics.BeltResult(selected);
                string status = state.Stalled ? "堵塞：" + state.Reason : CountAt(_selected) == 0 ? "空闲" : "等待下一 Tick";
                _inspection.text = $"传送带 {_selected} · {status}\n本 Tick 原有 {state.OriginalCount}，已移出 {state.MovedOut} · 当前组 {CountAt(_selected)}\n槽位容量 3000 · 木料有效容量 64 · 整组推进、禁止带间合并";
                Side side = _selected == 1 ? Side.West : Side.East;
                if (_world.Accessor(selected, side) != null)
                {
                    var result = _logistics.AccessorResult(selected, side);
                    string flow = result.Flow == AccessorFlow.ContainerToBelt ? "箱子 → 传送带" : result.Flow == AccessorFlow.BeltToContainer ? "传送带 → 箱子" : "自动";
                    _inspection.text += $"\n存取器：{flow} · 上次转移 {result.Transferred} · 原因 {result.Reason}";
                }
            }
        }
    }
}

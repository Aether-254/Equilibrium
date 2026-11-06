using System;
using System.Collections.Generic;
using System.Text;
using Equilibrium.Content;
using Equilibrium.Presentation;
using Equilibrium.Simulation;
using Equilibrium.Simulation.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace Equilibrium.UI
{
    /// <summary>Runtime UITK construction bar and inspection, bound to committed factory state.</summary>
    [RequireComponent(typeof(UIDocument), typeof(FactoryInputController))]
    public sealed partial class FactoryHud : MonoBehaviour
    {
        public FactoryRuntime Runtime;
        public StyleSheet Styles;
        public FactoryInputController Input { get; private set; }
        public UIDocument Document { get; private set; }
        public string InspectionText => _inspection?.text ?? "";
        private VisualElement _root, _toolbar, _inspector, _footer, _chestActions, _accessorActions, _machineActions;
        private Label _stock, _mode, _inspection, _status, _accessorStatus;
        private Button _pause, _depositWood, _depositLog, _collect;
        private DropdownField _side, _flow, _quantity;
        private IntegerField _amount;
        private TextField _filter;
        private readonly Dictionary<FactoryTool, Button> _tools = new Dictionary<FactoryTool, Button>();
        private bool _ready;
        private Font _font;
        private UnityEngine.TextCore.Text.FontAsset _fontAsset;
        private readonly List<string> _sides = new List<string> { "上", "右", "下", "左" };

        private void Start() => Initialize();
        public void Initialize()
        {
            if (_ready) return;
            Runtime = Runtime != null ? Runtime : GetComponent<FactoryRuntime>(); Runtime.Initialize();
            Input = GetComponent<FactoryInputController>(); Input.Runtime = Runtime; Input.Hud = this;
            Document = GetComponent<UIDocument>(); _root = Document.rootVisualElement;
            _root.Clear(); _root.name = "factory-hud"; _root.pickingMode = PickingMode.Ignore;
            if (Styles != null) _root.styleSheets.Add(Styles);
            // Uses installed system fonts without copying or redistributing third-party font files.
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 18);
            if (_font != null)
            {
                _fontAsset = UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(_font);
                if (_fontAsset != null) _root.style.unityFontDefinition = FontDefinition.FromSDFFont(_fontAsset);
            }

            _toolbar = Box(_root, "toolbar");
            _toolbar.RegisterCallback<GeometryChangedEvent>(evt => { if (_inspector != null) _inspector.style.top = evt.newRect.yMax + 12; });
            var heading = Row(_toolbar); heading.Add(new Label("EQUILIBRIUM  /  工厂") { name = "brand" });
            _stock = new Label { name = "warehouse-stock" }; heading.Add(_stock);
            var tools = Row(_toolbar);
            ToolButton(tools, FactoryTool.Inspect, "检查  B"); ToolButton(tools, FactoryTool.Chest, "箱子  M");
            ToolButton(tools, FactoryTool.Belt, "直线传送带  C"); ToolButton(tools, FactoryTool.Accessor, "存取器  I");
            ToolButton(tools, FactoryTool.Blocker, "阻拦器  K"); ToolButton(tools, FactoryTool.Remove, "拆除  X");
            ToolButton(tools, FactoryTool.Saw, "锯床  1"); ToolButton(tools, FactoryTool.Engine, "燃料机  2"); ToolButton(tools, FactoryTool.Shaft, "直轴  3");
            _pause = Button(tools, "暂停", () => { Runtime.Pump.Paused = !Runtime.Pump.Paused; Refresh(); });
            Button(tools, "单步", () => { Runtime.Pump.Paused = true; Runtime.Step(); Refresh(); });
            AddSaveControls(tools);
            _mode = new Label { name = "mode-hint" }; _toolbar.Add(_mode);

            _inspector = new ScrollView(ScrollViewMode.Vertical) { name = "inspector" }; _root.Add(_inspector);
            _inspector.Add(new Label("设备检查") { name = "inspector-title" });
            _inspection = new Label { name = "inspection-text" }; _inspector.Add(_inspection);
            _chestActions = Row(_inspector);
            _depositWood = Button(_chestActions, "存入 16 木材", () => { Runtime.Session.QueueDeposit(Input.Selected, Runtime.Session.Registry.Items.Parse("wood")); Refresh(); });
            _depositLog = Button(_chestActions, "存入 16 原木", () => { Runtime.Session.QueueDeposit(Input.Selected, Runtime.Session.Registry.Items.Parse("log")); Refresh(); });
            _collect = Button(_chestActions, "收回仓库", () => { Runtime.Session.QueueCollect(Input.Selected); Refresh(); });
            _machineActions = Row(_inspector);
            Button(_machineActions, "取消加工（释放预留）", () => { Runtime.Session.QueueCancelMachine(Input.Selected); Refresh(); });

            _accessorActions = Box(_inspector, "accessor-settings");
            _side = new DropdownField("方块侧面", _sides, 3) { name = "accessor-side" }; _accessorActions.Add(_side);
            _side.RegisterValueChangedCallback(_ => LoadAccessor());
            _flow = new DropdownField("方向", new List<string> { "自动", "存入容器", "从容器取出" }, 0) { name = "accessor-flow" }; _accessorActions.Add(_flow);
            _quantity = new DropdownField("数量模式", new List<string> { "等于", "小于等于", "取出直到剩余" }, 1) { name = "accessor-quantity" }; _accessorActions.Add(_quantity);
            _amount = new IntegerField("数量（1–3000）") { value = 16, name = "accessor-amount" }; _accessorActions.Add(_amount);
            _filter = new TextField("物品 ID") { value = "", name = "accessor-filter" }; _filter.tooltip = "留空表示不限物品，例如 wood 或 equilibrium:wood"; _accessorActions.Add(_filter);
            _accessorActions.Add(new Label("空 ID = 不限物品\n存入尽可能交付；数量模式用于取出"));
            var configButtons = Row(_accessorActions);
            Button(configButtons, "安装 / 应用", ApplyAccessor).name = "accessor-apply";
            Button(configButtons, "移除配件", () => QueueAccessor(null));
            _accessorStatus = new Label(); _accessorActions.Add(_accessorStatus);
            Button(_inspector, "拆除选中方块", () => { Runtime.Session.QueueRemove(Input.Selected); Refresh(); });
            _footer = Box(_root, "footer"); _status = new Label { name = "factory-status" }; _footer.Add(_status);
            _saveStatus = new Label { name = "save-status" }; _footer.Add(_saveStatus);
            _footer.RegisterCallback<GeometryChangedEvent>(evt => _inspector.style.bottom = evt.newRect.height + 12);
            _footer.Add(new Label("中键拖动画面 · 滚轮缩放 · 右键拆除 · Esc 返回检查 · 空格暂停"));
            Runtime.Changed += Refresh; Input.SelectionChanged += SelectedChanged;
            Runtime.SessionReplaced += LoadedSession;
            _ready = true; Refresh();
        }

        private static VisualElement Box(VisualElement parent, string name)
        { var v = new VisualElement { name = name }; parent.Add(v); return v; }
        private static VisualElement Row(VisualElement parent)
        { var v = new VisualElement(); v.AddToClassList("row"); parent.Add(v); return v; }
        private static Button Button(VisualElement parent, string text, Action click)
        { var b = new Button(click) { text = text }; parent.Add(b); return b; }
        private void ToolButton(VisualElement parent, FactoryTool tool, string text)
            => _tools.Add(tool, Button(parent, text, () => Input.SetTool(tool)));

        public bool HitTest(Vector2 screenPosition)
        {
            if (!_ready || _root.panel == null) return false;
            Vector2 at = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            // Pick includes popup menus extending beyond their parent's bounds.
            var picked = _root.panel.Pick(at);
            return picked != null && picked != _root;
        }
        public bool EditingText
        {
            get
            {
                var focused = _root?.panel?.focusController?.focusedElement as VisualElement;
                return focused != null && (focused is TextField || focused is IntegerField ||
                    focused.GetFirstAncestorOfType<TextField>() != null || focused.GetFirstAncestorOfType<IntegerField>() != null);
            }
        }

        private void SelectedChanged()
        {
            _side.SetValueWithoutNotify(_sides[(int)Input.SelectedSide]);
            LoadAccessor(); Refresh();
        }
        private bool SelectedBelt(out BlockRef b, out Side local)
        {
            var session = Runtime.Session; b = session.World.BlockAt(Input.Selected); local = Side.North;
            if (b.IsNone || session.Kind(b) != BlockKind.Conveyor) return false;
            local = ((Side)_side.index).Rotate(-session.World.OrientationOf(b)); return true;
        }
        private void LoadAccessor()
        {
            if (!SelectedBelt(out var b, out var local)) return;
            var config = Runtime.Session.World.Accessor(b, local) ?? new AccessorConfiguration();
            _flow.SetValueWithoutNotify(_flow.choices[(int)config.Flow]);
            _quantity.SetValueWithoutNotify(_quantity.choices[(int)config.Quantity]);
            _amount.SetValueWithoutNotify(config.Amount);
            _filter.SetValueWithoutNotify(config.Filter.IsNone ? "" : config.Filter.Canonical);
        }
        private void ApplyAccessor()
        {
            ItemId? filter = null;
            string id = _filter.value.Trim();
            if (id.Length > 0)
            {
                if (!Runtime.Session.Registry.Items.TryParse(id, out var item, out _))
                { _accessorStatus.text = "物品 ID 格式不正确"; return; }
                if (!Runtime.Session.Registry.TryGetItem(item, out _))
                { _accessorStatus.text = "当前内容中找不到该物品"; return; }
                filter = item;
            }
            int amount = Mathf.Clamp(_amount.value, 1, 3000); _amount.SetValueWithoutNotify(amount);
            QueueAccessor(new AccessorConfiguration((AccessorFlow)_flow.index, (AccessorQuantity)_quantity.index, amount, filter));
        }
        private void QueueAccessor(AccessorConfiguration config)
        {
            if (!SelectedBelt(out _, out var local)) return;
            Runtime.Session.QueueAccessor(Input.Selected, local, config); Refresh();
        }

        public void Refresh()
        {
            if (!_ready) return;
            var s = Runtime.Session; var w = s.World;
            _stock.text = $"仓库  原木 {s.Stock("log")}  木板 {s.Stock("plank")}  燃料木材 {s.Stock("wood")}  |  箱 {s.Stock("chest")}  带 {s.Stock("belt")}  存取 {s.Stock("accessor")}  阻拦 {s.Stock("blocker")}  锯床 {s.Stock("saw")}  燃料机 {s.Stock("engine")}  轴 {s.Stock("shaft")}";
            _pause.text = Runtime.Pump.Paused ? "继续  Space" : "暂停  Space";
            foreach (var pair in _tools) pair.Value.EnableInClassList("selected", pair.Key == Input.Tool);
            _mode.text = Input.Tool switch
            {
                FactoryTool.Chest => "左键放置箱子 · R 旋转 · 消耗仓库中的箱子",
                FactoryTool.Saw => "左键放锯床 · R 旋转 · 默认左进原木、右出木板、上接旋转力 · 1 原木 → 2 木板 / 1350 J",
                FactoryTool.Engine => "左键放燃料机 · R 旋转 · 默认左侧进燃料、下方输出动力 · 用木材作燃料",
                FactoryTool.Shaft => "左键放直轴 · R 切换轴向 · 默认上下连接 · 每格效率 99%",
                FactoryTool.Belt => "拖出至少 2 格 · 第一次跨格锁定直线方向 · F 反转 · 松开提交 · 缺料保留施工预览",
                FactoryTool.Accessor => "点击传送带靠近容器的一侧安装 · 在检查面板设置存取方向与数量",
                FactoryTool.Blocker => "点击传送带的一侧，安装 / 移除该方向的输入阻拦器",
                FactoryTool.Remove => "左键拆除 · 箱内物品随箱保留 · 带上物品收回仓库",
                _ => "点击设备查看库存与状态 · 箱子需要相邻传送带上的存取器"
            };
            _status.text = $"Tick {s.Engine.TickIndex}  ·  {(Runtime.Pump.Paused ? "已暂停" : "1 秒 / Tick")}  ·  待提交 {s.PendingCommands}  ·  待建 {s.Ghosts.Count}  ·  {Reason(s.LastEditReason)}";
            RefreshSaveControls();
            var b = w.BlockAt(Input.Selected); bool valid = !b.IsNone, belt = valid && s.Kind(b) == BlockKind.Conveyor;
            _chestActions.style.display = valid && w.SlotCount(b) > 0 && !belt ? DisplayStyle.Flex : DisplayStyle.None;
            _depositWood.style.display = valid && s.BlockId(b) != "saw" ? DisplayStyle.Flex : DisplayStyle.None;
            _depositLog.style.display = valid && s.BlockId(b) != "engine" ? DisplayStyle.Flex : DisplayStyle.None;
            _collect.style.display = valid && s.BlockId(b) != "engine" ? DisplayStyle.Flex : DisplayStyle.None;
            _machineActions.style.display = valid && s.Processing.State(b) != null ? DisplayStyle.Flex : DisplayStyle.None;
            _accessorActions.style.display = belt ? DisplayStyle.Flex : DisplayStyle.None;
            if (!valid)
            { _inspection.text = s.IsReserved(Input.Selected) ? "施工预览：等待仓库材料\n可右键取消" : "选择箱子或传送带\n\n先放两个箱子，在中间拖出传送带，在带的两端朝向箱子的侧面安装存取器，再向起点箱存入木材"; return; }
            var sb = new StringBuilder(belt ? "传送带" : s.BlockId(b) == "saw" ? "锯床" : s.BlockId(b) == "engine" ? "燃料机" : s.BlockId(b) == "shaft" ? "直轴" : "箱子");
            sb.Append("  ").Append(Input.Selected).Append("\n方向：").Append(_sides[(int)Side.East.Rotate(w.OrientationOf(b))]);
            if (belt)
            {
                var result = s.Logistics.BeltResult(b);
                sb.Append("\n状态：").Append(result.Stalled ? "停滞" : w.SlotCountOf(w.SlotGlobal(b, 0)) == 0 ? "空闲" : "运输");
                sb.Append("\n上 Tick 发出：").Append(result.MovedOut).Append("\n原因：").Append(Reason(result.Reason));
            }
            var machine = s.Processing.State(b);
            if (machine != null)
            {
                string phase = machine.Phase == MachinePhase.Working ? "加工中" : machine.Phase == MachinePhase.Blocked ? "堵塞" : machine.Phase == MachinePhase.Starved ? "缺料" : "空闲";
                sb.Append("\n状态：").Append(phase).Append(" · ").Append(Reason(machine.Reason));
                sb.Append("\n配方：").Append(machine.RecipeId ?? "待选");
                if (machine.RecipeId != null && s.Registry.TryGetRecipe(machine.RecipeId, out var recipe))
                    sb.Append("\n做功：").Append(machine.WorkDone.ToString("F1")).Append(" / ").Append(recipe.WorkRequired).Append(" J");
                sb.Append("\n收到功率：").Append(machine.ReceivedWatts.ToString("F1")).Append(" W");
                sb.Append("\n完成批次：").Append(machine.CompletedBatches).Append("\n近 60 Tick 利用率：").Append(machine.Utilization.ToString("P0"));
                if (machine.PendingProducts.Count > 0) sb.Append("\n成品待存入：").Append(machine.PendingProducts.Count).Append(" 种");
            }
            if (s.BlockId(b) == "engine" || s.BlockId(b) == "shaft" || machine != null)
            {
                var power = s.Processing.Power(b);
                sb.Append("\n动力：").Append(Reason(power.Reason)).Append("  ").Append(power.Omega.ToString("F1")).Append(" rad/s");
                sb.Append("\n源端负载：").Append(power.SourceDemand.ToString("F1")).Append(" / ").Append(power.SourceCapacity.ToString("F1")).Append(" N·m");
            }
            for (int slot = 0; slot < w.SlotCount(b); slot++)
            {
                int g = w.SlotGlobal(b, slot), handle = w.SlotItemHandle(g), count = w.SlotCountOf(g);
                sb.Append("\n").Append(w.ContainerKeyOf(g)).Append(" 槽 ").Append(slot + 1).Append(": ");
                if (handle < 0) { sb.Append("空"); continue; }
                var item = s.Registry.Items.FromHandle(handle);
                sb.Append(item.Canonical).Append("\n  ").Append(count).Append(" / ").Append(w.EffectiveCapacity(g, item));
                if (w.SlotReservedOf(g) > 0) sb.Append("（预留 ").Append(w.SlotReservedOf(g)).Append(')');
            }
            _inspection.text = sb.ToString();
            if (belt && SelectedBelt(out _, out var side))
            {
                var config = w.Accessor(b, side);
                var result = s.Logistics.AccessorResult(b, side);
                _accessorStatus.text = config == null ? "此侧未安装存取器" : $"已安装 · 上 Tick {result.Transferred} 件\n{Reason(result.Reason)}";
            }
        }

        private static string Reason(ReasonCode reason) => reason switch
        {
            ReasonCode.None => "就绪", ReasonCode.SourceEmpty => "来源缺料", ReasonCode.TargetFull => "目标已满",
            ReasonCode.NoReceivingPort => "下游无可接收端口", ReasonCode.DownstreamCapacity => "下游未腾空 / 容量不足",
            ReasonCode.ExactUnsatisfied => "整批数量未满足", ReasonCode.CellOccupied => "格子已占用",
            ReasonCode.SegmentTooShort => "传送带至少需要 2 格", ReasonCode.TargetInvalid => "目标无效",
            ReasonCode.FilterMismatch => "物品不匹配", ReasonCode.MachineNotAccepting => "目标拒收",
            ReasonCode.NoRecipe => "无配方", ReasonCode.NoInput => "等待原料", ReasonCode.MissingInput => "配方缺料", ReasonCode.OutputFull => "输出已满",
            ReasonCode.ReservationBroken => "输入预留失效", ReasonCode.Irreversible => "此配方不可取消",
            ReasonCode.NoPowerSource => "未连接动力源", ReasonCode.NoFuel => "燃料耗尽",
            ReasonCode.Stalled => "动力过载 / 停转", ReasonCode.SourceConflict => "动力源冲突",
            _ => reason.ToString()
        };
        private void OnDestroy()
        {
            if (Runtime != null) { Runtime.Changed -= Refresh; Runtime.SessionReplaced -= LoadedSession; }
            if (Input != null) Input.SelectionChanged -= SelectedChanged;
            if (_fontAsset != null) Destroy(_fontAsset);
            if (_font != null) Destroy(_font);
        }
    }
}

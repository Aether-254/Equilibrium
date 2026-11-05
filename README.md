# Equilibrium

一个用 Unity 开发的俯视角工厂模拟游戏，也是个人自娱、边做边学 Unity 的长期项目。

核心乐趣是把产线设计得更好：摆放设备、连接物流、观察瓶颈，再调整布局和动力供应。希望玩家的成长来自对系统的理解，让更好的设计带来更高的效率。

**施工中。当前已跑通基础物流与燃料驱动的锯木产线，尚未形成包含市场和成长系统的完整游戏。**

![Equilibrium 开发中的锯木产线与设备检查面板](docs/images/factory-saw-line.png)

*2026-11-05 的实际 Game View。机器上的文字、色块与部分物品标记仍是临时表现。*

## 想做成什么

- **真实优先**：尽量用物理、化学规律本身制造乐趣，逐步提高模拟深度。
- **工厂与经济共生**：长期目标是让生产、原料采购和商品销售组成一个循环。
- **深度即进度**：更深入地理解并优化系统，比单纯堆更多机器更重要。
- **从简单到深入**：先从直观的旋转动力与基础加工开始，再考虑热力学、化学和流体。

这是设计方向，不代表上述模拟和经济系统已经实现。已确认的具体规则见 [机制规范](design/confirmed-mechanics-spec.md)。

## 当前能做什么

| 系统 | 已实现的范围 |
| --- | --- |
| 建造 | 格子化放置箱子、锯床、燃料机、直轴；直线传送带拖拽预览、反向、拆除；有限仓库和缺料待建 |
| 物流 | 每秒一个逻辑 Tick；整组物品移动；相邻端口连接；存取器和方向阻拦器；堵塞与部分交付 |
| 加工 | 数据驱动配方、原料预留、耗时加工、输出堵塞、取消加工与物料回收 |
| 动力 | 单燃料源和直轴传动；燃料消耗、扭矩过载停转；传动损耗只计算一次 |
| 检查界面 | 库存、预留原料、加工进度、功率、状态、完成批次和近期设备利用率 |
| 内容基础 | `equilibrium:wood` 形式的物品 ID、分层 Tag、addon 依赖与内容校验 |
| 存档基础 | MessagePack + LZ4 编解码、未知字段保留、独立存档文档的 JSON 查看与导出 |

目前的加工例子是 **1 个原木 → 2 个木板**。燃料机通过直轴给锯床提供旋转动力，锯床累计足够的做功后完成加工，再向传送带输出成品。

**完整工厂存档尚未接通：每次进入 Play Mode 都会开始新会话，退出后当前布局和仓库变化会丢失。** 已完成的存档编解码测试不等于可以保存、恢复正在玩的工厂。

其他尚未实现的部分包括完整齿轮网络、自动寻路和复用传送带、多选搬运、多方块、GlassPipe 流体、蓝图、任务教学、科技解锁及市场玩法。1v1 工业竞赛仅保留设计，暂不开发。

## 本地开发与运行

当前验证环境是 **Windows 11 / Unity 6000.6.1f1 / URP 2D**，版本以 [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) 为准。本节是开发环境说明，使用权限见下方版权声明。

1. 安装对应版本的 Unity Editor，以及 Git 和 Git LFS；命令行逻辑测试还需要 .NET 8 SDK。
2. 获取仓库并拉取 LFS 资源：

   ```powershell
   git clone https://github.com/Aether-254/Equilibrium.git
   Set-Location Equilibrium
   git lfs pull
   ```

3. 在 Unity Hub 中添加并打开**仓库根目录**，等待 Package Manager 和 NuGetForUnity 恢复依赖、完成导入与编译。UPM 版本记录在 `Packages/manifest.json`，NuGet 版本记录在 `Assets/packages.config`；无需重新创建工程。
4. 打开 `Assets/Scenes/Main.unity`，进入 Play Mode。

初次恢复需要访问 Unity 包服务、GitHub 和 NuGet。`Assets/Packages/` 是忽略提交的恢复产物，不要手动混入多个目标框架的 DLL。`tools/repair-save-package-imports.ps1` 仅用于修复此前手动解压 NuGet 包产生的导入问题，正常恢复不需要执行。

编辑器内另有 `Equilibrium/Logistics/Open Workbench` 物流工作台。主场景中已有运行时入口，不需要再次生成场景或 Tier 1 资产。

## 基本操作

| 操作 | 输入 |
| --- | --- |
| 检查设备 / 返回检查模式 | 单击设备 / `B` 或 `Esc` |
| 放箱子、画直线传送带 | `M`、`C` |
| 安装存取器、切换方向阻拦器 | `I`、`K`，再点传送带边缘 |
| 放锯床、燃料机、直轴 | `1`、`2`、`3` |
| 旋转待放设备 | `R` |
| 反转当前传送带拖拽方向 | 拖拽中按 `F`，松手前可再次切换 |
| 拆除 | `X` 后单击，或右键 |
| 暂停 / 单步 | `Space` / 顶栏“单步”按钮 |
| 平移 / 缩放 | 鼠标中键拖动 / 滚轮 |

当前传送带至少两格，拖拽只支持直线；按住 Shift 也是直线。箱子需要相邻传送带上的存取器才能自动存取，锯床则通过自身端口进出料。建造和配置变更会在 Tick 提交时生效，暂停时可用“单步”提交。

快速试一台锯床：默认方向下，把燃料机、直轴、锯床从上到下相邻摆放；通过设备检查面板向燃料机存入燃料木材、向锯床存入原木，继续运行并观察功率、进度和输出槽。扩展物流时，在锯床左侧进料、右侧出料，箱子端通过存取器连接。

## 测试与验证

纯 C# 逻辑测试：

```powershell
dotnet test tools/purecheck/Equilibrium.PureCheck.csproj
```

这条命令覆盖内容、Tick、格子世界、物流、加工、编辑与存档编解码等逻辑，不替代 Unity 场景和渲染验证。

Unity 中可通过 Test Runner 运行 EditMode / PlayMode 测试。安装 Unity CLI 后，也可在关闭同一工程的 Editor 后运行：

```powershell
unity test . --editor-version 6000.6.1f1 --mode EditMode --output Logs/editmode.xml -- -nographics
unity test . --editor-version 6000.6.1f1 --mode PlayMode --output Logs/playmode.xml -- -nographics
```

当前检查记录见 [提交验证记录](production/qa/evidence/publish-checkpoint-evidence.md)。存档编解码的 Windows IL2CPP Player 探针另有 [验证记录](production/qa/evidence/save-il2cpp-spike-evidence.md)，不代表整款游戏已经完成 Player 构建验收或跨平台验证。

仓库包含 GitHub Actions 工作流，但运行仍依赖有效的 Unity 许可证及 CI 环境配置，本地通过不代表远端 CI 已通过。

## 目录导航

| 路径 | 内容 |
| --- | --- |
| `Assets/Scenes/Main.unity` | 当前主场景 |
| `Assets/Scripts/` | 内容、模拟、编辑、表现和 UI 实现 |
| `Assets/Script/` | 保留的早期实现 |
| `Assets/Art/` | 美术资源 |
| `Assets/Editor/` | Editor 工具，不进入普通 Player 运行时程序集 |
| `Assets/Tests/` | Unity EditMode / PlayMode 测试 |
| `design/` | 机制规范、系统 GDD、交互和美术设计 |
| `docs/architecture/` | 架构决策与实际实现说明 |
| `Bruno/MarketAPI/` | Market / addon / item / tag 的 API 契约和请求集合 |
| `production/` | 任务、进度与验证证据 |
| `tools/purecheck/` | 不依赖 Unity Editor 的逻辑测试工程 |

`Bruno/MarketAPI/` 当前是接口设计，不是可运行的市场服务器。全局市场、自部署服务器和跨版本兼容策略仍需后续实现。

## 版权

**ARR — All Rights Reserved，保留所有权利。** 本项目未采用开源许可证；仓库可见不代表授予代码、设计或原创美术的复用、修改、分发许可，具体见 [LICENSE](LICENSE)。第三方依赖与已有第三方文件继续适用各自的许可。

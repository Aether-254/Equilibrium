# Market Server 与全局市场规范

## 1. 文档状态

- 状态：已确认的机制与系统边界。
- 当前阶段：设计规范，不代表客户端、服务器或网络协议已经实现。
- 接口所有权：具体服务器实现仍由项目创作者负责；当前统一 HTTP(S)、WS(S)、认证发现、资源模型与 Bruno 示例见 `Bruno/MarketAPI/`。
- 当前非目标：现在不创建服务器、账户系统、OAuth 流程、联网 Package、市场 UI 或 Unity 网络代码。

## 2. 设计目标

Market Server 为多个玩家提供持续运行的全局经济，同时允许：

- 项目创作者部署官方或主要服务器；
- 玩家自行部署 Market Server；
- 玩家在创建存档时配置其他人的 Market Server；
- 服务器使用域名上的 HTTP(S) 与 WS(S)；
- 客户端通过 Auth Header 鉴权；
- Addon 与不同游戏版本按经济兼容性进入主市场、专属市场或被拒绝。

Market Server 是经济后端，不直接成为本地工厂模拟的权威服务器。首版属于社区实验型信任模式，同时为未来区分 Open Realm 与 Verified Realm 预留边界。

## 3. 市场后端与存档绑定

游戏侧使用稳定的市场后端边界：

```text
Market Backend
|- Local NPC Market
`- Remote Market Server
```

### 3.1 创建存档

创建存档时选择一次市场模式：

```text
新存档
|- NPC Market
`- Remote Market
   `- 选择一个 Market Server
```

### 3.2 不可变绑定

- 每个存档只能选择一个 Market Server。
- 正常游玩中不能把存档换到另一个 Market Server。
- 远程市场存档不能自行改回 NPC 模式。
- NPC 市场存档不能自行改为远程市场。
- 域名或连接地址变化不等同于服务器身份变化；存档绑定的是稳定服务器身份。

### 3.3 服务器身份

远程市场存档至少保存：

```text
ServerUrl
ServerId
ServerIdentityKey
RealmId
AccountId
CompatibilityProfileHash
MarketProtocolVersion
```

- `ServerId` 是稳定的服务器身份，不应只使用可变 URL 代替。
- `ServerIdentityKey` 用于验证服务器身份与迁移凭证；具体证书固定或公钥轮换机制为 TBD。
- Auth Token 不属于普通游戏存档字段。

## 4. 独立经济世界

每个 Market Server 是独立经济世界，分别维护：

- 账户；
- 远程市场货币；
- 玩家挂单；
- 服务器模拟挂单或流动性；
- 成交与价格历史；
- 待交付成交凭证；
- 兼容性 Realm；
- 合同、公告和维护状态。

不同 Market Server 默认不共享：

- 货币余额；
- 商品；
- 挂单；
- 成交历史；
- 市场仓位；
- 信用或限制状态。

## 5. 市场模型

### 5.1 混合市场

市场由两种参与者共同形成：

1. 玩家订单；
2. 服务器模拟的生产者、消费者或做市流动性。

这使低在线人数时仍能发生交易，同时保留玩家总供给和总需求对价格的影响。

### 5.2 交易方式

市场同时支持：

- 即时买入；
- 即时卖出；
- 限价买单；
- 限价卖单；
- 部分成交；
- 撤单。

即时交易应按当时可成交的市场深度执行，而不是使用客户端缓存价格保证成交。订单优先级、最小订单量、撮合周期与同价排序仍为 TBD。

### 5.3 服务器模拟流动性

每一种可交易物品可以单独配置服务器流动性模式：

```text
Finite
Infinite
Disabled
```

- `Finite` 使用有限库存、有限预算或可恢复额度。
- `Infinite` 允许服务器按配置持续提供买卖能力。
- `Disabled` 表示该物品完全由玩家订单形成市场。
- 不要求所有物品使用同一种模式。

### 5.4 市场保护

服务器可以按全局、Realm 或物品配置：

- 最小价格；
- 最大价格；
- 单笔最大订单量；
- 时间窗口内最大交易量；
- 涨跌幅限制；
- 短时熔断；
- 异常账户限制。

具体默认值：TBD。

## 6. 出口与卖出事务

### 6.1 出口托管

商品不能从普通仓库直接以非事务方式发送给服务器。卖出流程为：

```text
普通仓库
-> 本地统一事务
-> Export Pending
-> HTTPS 幂等请求
-> 服务器接受
-> 挂单或立即成交
```

### 6.2 幂等性

- 每次出口提交使用稳定且唯一的幂等键。
- 网络超时只表示结果未知，不表示失败。
- 结果未知时，对应商品保持在 `Export Pending`，不能被生产系统重新使用。
- 客户端恢复连接后使用同一幂等键查询或重试。
- 服务器不得对同一幂等键重复增加库存、重复挂单或重复支付货币。

## 7. 买入与仓库

### 7.1 存档级统一仓库

远程市场购买的商品直接进入存档级统一仓库，不要求玩家手动从服务器市场仓库领取，也不直接寻找世界中的普通 Container。

统一仓库与世界内箱子、机器 Container、传送带及物品存取器之间的具体存取接口：TBD。

### 7.2 完整容量预留

- 创建买单前必须为订单的完整数量预留仓库容量。
- 无法预留完整容量时，整个买单被拒绝。
- 部分成交只消耗相应比例的预留容量。
- 撤销订单或订单失效时释放未使用的预留容量。
- 已预留容量不能同时被普通生产、其他订单或手工存入占用。

### 7.3 离线成交

- 玩家离线后，已挂订单继续参与撮合。
- Market Server 不直接修改离线的本地 Unity 存档。
- 服务器为成交生成持久化成交凭证。
- 客户端下次连接时验证凭证，并通过本地统一事务把商品直接写入预留的统一仓库空间。
- 同一成交凭证只能应用一次。

成交凭证使用可分页查询、签名负载、幂等确认和单次本地应用语义；统一端点与基础 Schema 见 `Bruno/MarketAPI/openapi.yaml`。签名算法、长期保留时间与密钥轮换周期：TBD。

## 8. 货币与离线行为

### 8.1 货币权威

- Remote Market 的货币由对应 Market Server 维护。
- Local NPC Market 的货币由本地存档维护。
- 两种货币不自动兑换或迁移。
- 客户端显示的远程余额可以缓存，但服务器余额是权威结果。

### 8.2 断网

断网时允许：

- 查看最后成功同步的行情缓存；
- 继续运行本地工厂；
- 查看本地出口托管状态；
- 查看尚未完成同步的本地事务。

断网时禁止：

- 按缓存价格创建即时成交；
- 创建需要服务器确认的新远程订单；
- 假定旧订单已经或没有成交；
- 在没有成交凭证时凭空增加商品或余额。

## 9. Addon 与游戏版本兼容

### 9.1 兼容性对象

Market Server 内部可以包含多个 `Market Realm`。客户端通过 `Compatibility Profile` 匹配 Realm。

Compatibility Profile 至少可以包含：

```text
GameVersion
MarketProtocolVersion
AddonNamespaces
AddonVersions
AddonFingerprints
EconomyImpactClasses
ItemRegistryFingerprint
RecipeRegistryFingerprint
```

### 9.2 混合分区规则

- 纯视觉或明确不影响经济的 Addon 可以进入现有 Realm。
- 只增加内容的 Addon 可以由服务器配置为共享部分市场或进入专属市场。
- 修改原版配方、产率、机器速度、能源成本、资源获取或其他生产成本的 Addon 默认进入独立 Realm。
- 彻底改变核心工厂或经济的 Overhaul Addon 必须进入专属 Realm，或者被服务器拒绝。
- 重大 Game Version 经济改动使用同样的 Realm 隔离机制。

### 9.3 经济影响等级

```text
Cosmetic
Additive
BalanceAffecting
Overhaul
Unknown
```

- Addon 开发者声明初始影响等级并提供数据清单。
- 服务器执行规则检查并计算或验证内容指纹。
- 服务器管理员拥有最终批准与覆盖权。
- `Unknown` 默认被拒绝或隔离，不默认进入主市场。

### 9.4 Addon 上传与读取

服务器可以要求 Addon 开发者提前提交 Market Manifest，也可以由管理员手工建立配置。

服务器不得为了自动分析而直接执行上传的任意 Unity/Addon 代码。自动读取应限制为受控数据，例如：

```json
{
  "namespace": "farmer_addon",
  "version": "2.1.0",
  "marketImpact": "additive",
  "items": [],
  "tags": [],
  "recipes": [],
  "fingerprint": "..."
}
```

Market Manifest 的初始网络 Schema、验证与审核端点见 `Bruno/MarketAPI/openapi.yaml`；Manifest 签名算法、开发者身份审核和版本撤销策略仍为 TBD。

### 9.5 服务器策略

服务器提供默认不兼容策略，并允许每个 Realm 或 Addon 覆盖：

```text
RejectClient
IsolateIntoExclusiveRealm
AllowWithRestrictedItems
ReadOnlyOtherRealms
```

### 9.6 跨 Realm 可见性

专属市场默认只能看到其他 Realm 延迟后的：

- 价格；
- 成交量；
- 趋势统计。

这些数据是只读信息，不能用于跨 Realm 买入或卖出。延迟长度、统计粒度和服务器是否完全拒绝跨 Realm 可见性均可配置。

### 9.7 旧版本 Realm

经济不兼容升级不会把旧挂单自动迁入新 Realm。服务器可以保留旧 Realm，并允许：

- 继续作为 Legacy Realm 运行；或者
- 停止创建新挂单，但允许撤单、结算与取回已有资产。

旧商品、货币与订单迁往新 Realm 的规则不能由版本号自动推断。

## 10. 可交易物品与实例状态

### 10.1 MarketTradable

- 只有物品定义明确标记为 `MarketTradable` 的物品才能进入市场。
- 服务器可以进一步禁止物品，但不能让客户端定义为不可交易的物品绕过限制。
- `MarketTradable` 的数据字段位置与默认值：TBD。

### 10.2 交易时清空实例状态

带实例持久化状态的物品在进入市场时清空全部实例状态，并规范化为该 ItemId 的标准市场状态。可能被清空的内容包括：

- 内部物品；
- 储存电能；
- 耐久或损耗；
- 温度与压力；
- 机器配置；
- 进行中的配方状态；
- 自定义实例属性。

ItemId、物品定义及服务器用于区分商品的 Fungibility Key 不属于可清空的实例状态。

状态清空必须发生在本地统一出口事务中，不能在服务器接受后才静默修改本地物品。清空是否需要额外确认、内部内容是否允许预先卸载，以及哪些属性被列入不可交易条件：TBD。

### 10.3 Fungibility Key

市场商品不永久限定为只按 ItemId 合并。服务器认可的 `Fungibility Key` 可以包含：

```text
ItemId
Quality
Grade
Variant
OtherServerApprovedCommodityFields
```

首阶段可以只使用 ItemId，但订单、行情和成交标识应允许未来加入服务器认可的可互换性字段。

## 11. 身份验证与传输

### 11.1 登录

- 服务器提供登录流程。
- Unity 客户端通过浏览器/OAuth 类流程取得访问凭据。
- 后续请求通过 Auth Header 携带凭据。
- Token 不写入普通游戏存档。
- 系统凭据存储、刷新令牌、退出登录和账户切换细节：TBD。

### 11.2 HTTPS

HTTPS 用于：

- 创建与撤销订单；
- 即时买卖；
- 查询市场快照；
- 查询账户与余额；
- 查询订单与成交凭证；
- 幂等重试和恢复；
- 查询 Realm 与兼容性要求。

### 11.3 WSS

WSS 用于推送：

- 价格与行情变化；
- 订单状态；
- 成交；
- 余额变化；
- 待交付凭证；
- 合同变化；
- 市场公告；
- 维护和连接状态。

### 11.4 服务器时间

以下行为使用服务器时间：

- 订单过期；
- 合同期限；
- 成交时间；
- 行情统计窗口；
- 市场冷却；
- 熔断；
- 维护窗口。

客户端时间只用于 UI 展示，不作为市场结算依据。

## 12. 信任等级

### 12.1 Open / Community

首版采用社区实验型信任模式：

- 允许玩家自建服务器；
- 允许服务器选择支持的 Addon；
- 接受本地工厂客户端不能被完全信任；
- 通过额度、限流、日志、异常检测和管理员治理降低破坏；
- 不承诺强竞技级反作弊。

### 12.2 Verified Realm 预留

协议和数据结构为未来 Verified Realm 预留：

- 限定游戏版本；
- 限定 Addon 与内容指纹；
- 更严格的产出与行为验证；
- 独立经济与账户限制。

纯本地工厂模拟不能提供完全可信的产出证明。若未来要求强可信，必须另行设计服务端验证或权威模拟，不能只依赖 Auth Header。

## 13. 迁移凭证

### 13.1 唯一换绑例外

存档正常情况下永久绑定当前市场。服务器签发的有效迁移凭证是唯一允许改变绑定的正常机制。

迁移凭证可以授权：

```text
AnyCompatibleServer
AllowedServerIds
RecommendedServerIds
NpcMarket
```

- `AnyCompatibleServer` 允许玩家选择任意通过兼容检查的目标服务器。
- `AllowedServerIds` 将目标限制为凭证列出的服务器。
- `RecommendedServerIds` 向玩家展示服务器建议的迁移目标；它是否同时构成硬性允许列表由凭证字段明确表达。
- `NpcMarket` 允许远程市场存档退回本地 NPC 市场。

### 13.2 最低安全字段

迁移凭证至少需要绑定：

```text
SourceServerId
SourceRealmId
SaveId / AccountId
IssuedAt
ExpiresAt
Nonce
DestinationPolicy
AllowedServerIds or RecommendedServerIds
TargetCompatibilityConstraints
Signature
```

- 凭证由当前绑定服务器签名。
- 客户端必须验证签名、来源服务器身份、有效期与存档/账户绑定。
- 凭证必须防重放，默认按单次使用设计。
- 没有有效迁移凭证时，修改存档内的 ServerUrl 不构成合法迁移。

### 13.3 尚未决定的迁移资产

迁移凭证已经确认可以改变绑定，但下列资产是否随迁移仍为 TBD：

- 服务器货币余额；
- 未成交挂单；
- 已成交但未交付商品；
- 出口托管商品；
- 合同进度；
- 市场信用、限制或封禁状态；
- 价格与交易历史。

在这些规则确定前，迁移凭证只确认“允许换绑”，不自动承诺跨服务器复制经济资产。

## 14. 当前未决问题

1. 统一仓库与世界 Container 网络之间的存取机制。
2. 撮合优先级、撮合频率、部分成交和手续费。
3. 服务器模拟流动性的补充与回收算法。
4. 成交凭证 Schema、签名、分页、确认和保留时间。
5. Market Manifest Schema、签名、审核与版本撤销。
6. `MarketTradable` 与 `Fungibility Key` 的正式数据格式。
7. 清空实例状态前的提示、确认与内部内容处理。
8. OAuth 登录在桌面客户端中的回调方式和凭据存储。
9. 迁移时哪些远程资产能够转移、退还、冻结或作废。
10. 迁移到 NPC 市场时，远程货币如何处理。
11. 服务器永久离线而无法签发迁移凭证时是否存在恢复机制。
12. Open Realm 与未来 Verified Realm 的实际验证边界。

## 15. 当前实施禁区

在进入专门实现阶段前：

- 不创建假定的 HTTP 路径或 JSON 协议；
- 不把缓存行情当成成交保证；
- 不允许客户端自行改变存档绑定；
- 不执行上传的 Addon 代码来分析市场兼容性；
- 不把不同 Compatibility Profile 的商品无条件混入同一经济；
- 不声称社区信任模式能够彻底防作弊；
- 不为了远程市场破坏现有 ItemId、Container 或统一事务语义。

## 16. API 契约入口

统一接口设计位于：

- `Bruno/MarketAPI/API-DESIGN.md`：端点目录、跨端点不变量、状态流和验收测试。
- `Bruno/MarketAPI/openapi.yaml`：HTTP(S) OpenAPI 3.1 契约。
- `Bruno/MarketAPI/asyncapi.yaml`：WSS AsyncAPI 3.0 事件契约。
- `Bruno/MarketAPI/opencollection.yml`：Bruno/OpenCollection 1.0 集合入口。
- `Bruno/MarketAPI/*/*.yml`：可审阅的代表性请求。

API 使用 `/v1` 版本化路径，所有网络边界上的 ItemId 与 TagId 必须使用完整 Namespace。状态变更命令使用 `Idempotency-Key`；权威数量和金额使用十进制字符串；错误使用带稳定 `code` 的 `application/problem+json`。

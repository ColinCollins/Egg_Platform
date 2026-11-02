# IWanner 玩法控制与入口风险评估

## 原始行为基线

- IWanner 只有临时角色事件转发，未进入 `PlayControllerManager`，没有独立玩法资源所有权。
- Loading 不解析 IWanner Group；入口、HUD、Reset、暂停和失败回退均不存在。
- Main 场景曾保留临时 `IWannerLevelCtrl` 绑定，正式玩法接入前可能与动态加载实例同时存在。

## 受影响文件与既有系统

- 受影响代码和资源见同目录 `IWanner玩法控制与入口功能记录.md`。
- 复用：`IPlayController`、`PlayControllerManager`、`IPlayPauseHost`、`UIManager`、
  `ResourceManager`、`CustomButton`、`GameSettingPopup`、`ActorCtrl` 和 Luban 生成表。
- 未接入：DB、RemoteConfig、广告、SDK 打点、Victory、Group 存档和平台原生覆盖层。
- YooAsset 复用 `GamePackage/Level` 的既有关卡收集组，不新增 Package 或自定义规则。

## 风险项

1. **P1：组内 Q 弹进度只有静态证据**
   - 当前 YAML 已把已过关/未过关两组列表修正为各 5 个独立 Image，并把五个 Toggle 的共同横向容器绑定给
     GameplayPanel；代码按直接子节点兄弟顺序从左到右播放，全部落稳后再刷新状态。
   - 影响：HorizontalLayoutGroup 在真实 Canvas 重建时是否干预 Toggle 的 Y 位移、默认 `28px/0.08s` 节奏是否
     达到预期 Q 弹观感，以及连续 Reset/Replay 中断是否有单帧跳变，尚无 PlayMode/真机证据。
   - 处理：PlayMode 覆盖首次进入、Reset、Replay、第二关推进和动画中退出；Android/iOS 横屏核对高度、错峰、
     状态切换和布局复位，必要时只调整 prefab 暴露的高度与间隔参数。
2. **P1：当前 Group 与 LevelData 引用不一致**
   - 当前 JSON 曾出现 Group 引用 `20001...`、LevelData 为 `30001...`。
   - 影响：严格校验会拒绝入口，不会回退到未配置的关卡。
   - 处理：配置由用户维护；逻辑只记录精确 groupId/index/levelId 错误，不自动改表。
3. **P1：Main 场景临时 IWanner 对象可能残留**
   - 旧记录表明 Main 场景存在临时 `IWannerLevelCtrl`/Actor。
   - 影响：虽然新运行时未初始化前不接收输入，但可见对象、Collider 或相机表现仍可能与动态实例重叠。
   - 处理：正式联调前由资源侧确认并移除/隐藏临时场景对象；本次按“只管逻辑”未改场景。
4. **P2：异步资源未执行显式 Release**
   - 当前与 Draw 基线一致，销毁实例但未新增 `ResourceManager.Release` 调用。
   - 影响：长期多次进入/退出的资源缓存行为尚未验证。
   - 处理：在正式资源所有权与加载策略稳定后，结合 ResourceManager loader 行为补充压力验证。
5. **P2：当前 Reset 不建立标准分析周期**
   - 本次只重建实例，不写 revive/play_count/level_start/level_end。
   - 影响：不能把当前阶段日志作为正式 BI 事件证据。
   - 处理：后续接入 `IWannerAnalyticsAttempt` 时严格遵守《关卡打点生命周期约定》，并补成对事件测试。
6. **P2：移动端多指与失焦仅完成代码门禁**
   - HUD 逻辑按主线优先级处理双键（右方向优先），OnClose/禁用时释放输入。
   - 影响：Android/iOS 的指针取消、系统覆盖层和横屏触控尚未验证。
7. **P2：Reset 与 Replay 调用边界依赖后续 UI 正确接线**
   - HUD Reset 派发 `IWannerGameResetEvent` 后由活动控制器调用 `TryResetCurrentLevel()`，保持当前
     Group 和当前小关索引；共享 `GameReplayEvent` 在 IWanner 活动时调用 `TryReplayCurrentGroup()`，
     保持当前 Group 并把索引重置为 0。
   - 影响：若后续 VictoryPanel 误接 Reset API，玩家不会从 Group 第一关开始；若 HUD 误接 Replay API，
     玩家会丢失当前组内进度。
   - 处理：VictoryPanel Replay 只接 `TryReplayCurrentGroup()`，HUD Reset 只接
     `TryResetCurrentLevel()`；两条公开 API 的中文注释已固化该约束。
8. **P2：YooAsset 清单与真机构建尚未验证**
   - 已静态加入 `Assets/Game/Prefabs/WannerLevel` collector，使用 `AddressByFileName`、
     `PackSeparately`、`CollectPrefab`，目录 GUID 与 `.meta` 一致。
   - 影响：若构建侧缓存旧 Collector 配置或未重新生成 Package 清单，非 Editor 模式仍可能找不到新关卡。
   - 处理：资源侧改完关卡数据后，在 Unity 中执行 Collector 检查和 GamePackage 构建，验证
     `ILevel_001.prefab`～`ILevel_005.prefab` 可由当前完整路径加载。
9. **P1：黑幕与输入拦截只有静态证据**
   - 当前逻辑在首次进入时保持黑幕和 ClickBlock；Reset/Replay 时旧面板先渐显到黑，再同时销毁旧关卡和
     旧 GameplayPanel，新建面板立即保持全黑，加载完成后渐隐并恢复输入。三个过场参数已暴露在 Inspector。
   - 影响：若 Canvas 层级、射线目标或 Tween 生命周期与 prefab 实际状态不符，仍可能出现黑幕未覆盖、
     两个面板交接闪屏、点击穿透或输入恢复过早。
   - 处理：Unity PlayMode 覆盖首次进入、连续 Reset、Replay、面板销毁重建和退出；移动端补充多指验证。
10. **P2：小关级 GameplayPanel 重建成本尚未实测**
   - 用户已明确每次进入小关都销毁并重建 GameplayPanel，代码不再沿用旧面板刷新状态。
   - 影响：频繁 Reset 会重复执行 Bear.UI 加载、OnCreate/OnOpen、事件订阅和销毁；当前没有移动端帧时间证据。
   - 处理：连续 20 次 Reset/Replay，确认 UIManager 中始终只有一个活动 IWanner GameplayPanel，且没有旧
     Tween、事件订阅、黑幕或 ClickBlock 残留。
11. **P1：成功门持久化接线尚未执行 PlayMode 验证**
   - `IWannerLevelCtrl.AutoBind` 会找到 `IWannerGame_Door`，绑定门触发器/动画组件，并把
     `Success(Collider2D)` 持久化到 `OnEnter`；未点击按钮的 prefab 会使用运行时临时 Listener 兜底。
   - 影响：若门节点改名、门组件不在同一根节点或 Unity 未保存 Prefab Override，持久化引用可能缺失；运行时兜底
     也会在完全找不到 `IWannerGame_EnterDoorAnimCtrl` 时失效。
   - 处理：逐个包含成功门的 WannerLevel prefab 点击 AutoBind、保存并核对 OnEnter；PlayMode 验证没有重复 Listener。
12. **P2：入口门动画名和渐隐时序依赖当前资源契约**
   - 静态资源证据表明 `door_close` 对应 Spine 动画名为 `close`；控制器完成边界是“延迟 + 渐隐”，不等待
     close 动画事件或动画时长。
   - 影响：若美术资源重命名动画，或渐隐时间短于期望关门表现，可能出现动画缺失或执行器提前进入下一项。
   - 处理：资源改名时同步更新控制器常量；使用 `effectFadeDelay/effectFadeDuration` 对齐表现，并在真机检查帧率
     与横屏显示。缺少引用或动画时控制器会记录错误并避免阻塞执行器。
13. **P3：AutoBind 依赖门节点名称**
   - 编辑器按钮按 `door_close/effect` 精确名称绑定，直接子节点不存在时才递归查找同名 Spine。
   - 影响：资源节点改名或存在多个同名节点时，按钮可能找不到引用或选中层级中的首个匹配项。
   - 处理：门 prefab 保持当前命名；点击后在 Inspector 核对两个引用。按钮找不到目标时保留原引用，不清空数据。
14. **P1：Group 最后一关 Victory 尚未接入**
   - 当前组内非末关会在门表现完成后进入下一关；末关不会越界加载，并明确记录 VictoryPanel 尚未实现。
   - 影响：末关成功后只保留 Reset/Pause 操作，没有首通 Reward 表现或复通普通弹窗，不能作为完整 Group 闭环验收。
   - 处理：后续接入 Group 首通/复通状态及 `VictoryPanel_Reward`/普通 VictoryPanel，再补 Replay、退出和存档清理验证。
15. **P2：成功时角色瞬移位置依赖门根节点 Pivot**
   - 当前成功请求会清零 Actor 刚体速度，并把角色 X/Y 直接设置为成功门根节点世界坐标；角色 Z 保持不变。
   - 影响：若某关门根节点 Pivot 不是期望站位、位于碰撞体内部或高于地面，角色可能短暂重叠或受重力修正。
   - 处理：关卡制作时把 `IWannerGame_Door` 根节点放在期望角色停靠点；PlayMode 核对落点、遮挡层级和关门画面。

## 验证证据

- 定向 C# 构建：`0 error / 101 warning`。
- 新增脚本均被 `Assembly-CSharp.csproj` 收录。
- 静态核对只新增 IWanner 玩法专属 Reset 事件，未改共享 Actor/HUD 基类，未改 Excel 或 JSON。
- 静态核对 GameplayPanel 六个按钮、`BlackMask`、`ClickBlock` 及三个过场参数均已序列化绑定。
- 静态核对每次重建都会销毁旧 GameplayPanel，新实例仅执行一次组内进度初始化。
- 静态核对进度容器的五个直接子节点顺序、Toggle 组件、两组独立 Image 引用和 Q 弹 Sequence 清理入口完整；
  `Assembly-CSharp.csproj` 构建为 `0 error / 101 warning`，`git diff --check` 通过。
- Unity PlayMode、YooAsset 清单/包构建、资源真实加载、黑幕/点击拦截、连续 20 次 Reset/Replay/退出、
  Android/iOS：`uncertain`。
- 入口门的 prefab 引用、动画名和“门触发→动画完成→下一关”调用链已完成静态核对；AutoBind 持久化结果、
  PlayMode 连续推进、Reset/退出中断与 Group 末关表现：`uncertain`。

## 回滚路径

- 按功能记录中的文件清单移除 IWanner Controller/Policy/Loading/入口集成，即可恢复 Main+Draw 双玩法注册。
- 恢复 `IWannerLevelCtrl` 临时实现和 HUD 空实现不会影响用户配置与图片资源。
- 若仅入口不稳定，可先不在 prefab 挂 `IWannerGameEnter`，其余逻辑保持不可达状态。
- 若 WannerLevel 收集引入构建问题，可只移除 `AssetBundleCollectorSetting.asset` 中该目录 collector，
  不影响主线与 Draw 的既有 collector。
- 若入口门表现异常，可只移除门 prefab 上的 `IWannerGame_EnterDoorAnimCtrl` 组件与对应脚本；触发器其余
  ItemEventHandle、门碰撞和 IWanner 玩法流程不需要回滚。
- 若只回滚进度 Q 弹表现，恢复创建时即时刷新 Image 的逻辑并移除进度 Sequence/容器序列化字段；两组已修正的
  独立 Image 引用可保留，不影响原有逐格状态语义。

## 待确认

- `IWannerLevelSort.Group` 的最终业务含义仍由配置侧确认；当前唯一 Group 身份明确使用排序行 `Id`。
- 正式选组、存档、通关和 TBN 接入前，需要先同步更新 Speckit 产物中的 groupId/currentLevelIndex 语义。

## IWanner 临时 Group 选组风险补充（2026-08-11）

### 原始基线与受影响范围

- 修改前 `IWannerGameEnter` 固定调用首组入口，ChoiceLevelPopup/Item 脚本为空，prefab 根节点仍挂复制来源
  `CommmonPopup` 且 Item 没有按钮。
- 本次影响 ChoiceLevelPopup 两个脚本与 prefab、`IWannerGameEnter`、`GameManager` 和 `IWannerPlayCtrl`；
  LevelSort/LevelData 配置、存档、关卡 prefab、全局事件、Analytics 和平台 SDK 未修改。

### 风险项

1. **P1：临时列表没有解锁和通关状态**
   - 影响：当前 Loading 读到的所有非空 Group 都可以点击，不能代表正式产品规则。
   - 处理：保持“临时”边界；正式接入前先确定 Group 存档、解锁和复玩状态，再扩展 Item 显示与门禁。
2. **P1：完整配置校验延迟到点击**
   - 影响：Path 空、LevelData 缺失或重复引用的 Group 仍会显示；点击后会失败并留在弹窗。
   - 处理：继续以 `IWannerGameLevelCtrl.TryGetGroup/TryGetLevelData` 为唯一校验入口，不在 UI 复制规则。
3. **P2：prefab 射线与实例化只有静态证据**
   - 影响：Item 依赖根 `CustomButton` 接收子级 Image 射线；七项布局、重复打开和横屏触控尚未实测。
   - 处理：PlayMode 逐项点击 1～7，覆盖关闭/重开和错误保留；Android/iOS 核对横屏触控命中与布局。

### 验证、未覆盖与回滚

- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 101 warning`；静态字段与 prefab fileID 绑定一致。
- `uncertain`：Unity PlayMode、真实关卡资源加载、配置异常、连续打开/关闭以及 Android/iOS 尚未验证。
- 回滚时恢复 `IWannerGameEnter -> TryEnterIWannerFirstGroup()`，删除两个指定 Group API，并恢复/停用临时
  ChoiceLevelPopup 组件即可；无需迁移配置或存档。

## IWanner 独立 RemoteConfig 风险补充（2026-08-10）

### 原始基线与当前边界

- 原始状态只有 Luban `GlobalConst` 的 9 个 IWanner 字段，没有独立 remote key、启动拉取、统一 getter 或 Guru
  默认值；当前已补齐 `iwannergame_config` 基础链，入口已消费 IsOpen，Skip、提示和广告业务仍未消费。
- 新 key 与其他 key 一样写入 `DB.GameData.RemoteConfigCache`；不新增存档字段，也不改变当前版本校验只覆盖
  `level_config.currentRemoteConfigVersion` 的既有规则。

### 风险项

1. **P1：配置基础已接入但业务消费尚未闭环**
   - 影响：入口开关已能改变入口显隐和点击结果；其余远端值在失败/Skip、提示和广告代码正式调用 getter 前
     仍不会改变玩家表现。
   - 处理：Checklist 分别记录各项消费状态，禁止仅凭配置存在宣称 Skip、提示或广告功能已生效。
2. **P1：新顶层 key 没有独立版本字段校验**
   - 影响：远端、历史缓存和 Guru 默认值仍按 RemoteConfigService 既有来源优先级解析；旧缓存可能缺少整个 key，
     此时依赖 Guru 默认值或字段 getter 的 GlobalConst 回退。
   - 处理：上线前验证旧包升级、离线、远端缺 key/缺字段和 GM 清缓存；是否给所有业务 key 引入 schema/version
     属于跨模块设计，当前不擅自扩展。
3. **P2：数值暂未做业务下限归一化**
   - 影响：getter 忠实返回配置值；负数或 0 对 Skip、计时和广告 CD 的最终含义尚未由对应业务流程定义。
   - 处理：在每个消费流程接入时按玩家体验明确禁用/立即触发/最小值策略，并增加边界测试，不在配置层静默改值。
4. **P2：编辑器菜单实跑与真机尚未验证**
   - 静态检查和两套 C# 定向构建通过，但没有实际点击新菜单，也没有 Firebase、PlayMode、Android/iOS 证据。
   - 处理：发布前执行 Checklist 中的菜单、缓存来源和移动端启动验证。

### 回滚

- 删除 `iwannergame_config` 启动拉取、读取 API、Guru 默认行、GM 展示和 LubanGenerator 同步入口，即恢复为
  仅本地 GlobalConst 字段存在的状态；不需要回滚关卡资源或迁移存档。

## IWanner Actor 死亡震屏风险补充（2026-08-10）

### 原始基线与当前边界

- 修改前 `IWannerLevelCtrl` 没有 Actor 死亡入口；`ILevel_001` 虽已有名为 `Feedback` 的
  `MMF_Player/MMF_CameraShake`，但控制器不会播放它。
- 修改后死亡请求在关卡控制器内幂等执行并与成功互斥；本阶段仍未把死亡接入正式失败回调、失败计数、
  自动重建或 Skip。

### 风险项

1. **P1：死亡表现已建立，但失败闭环仍未接入**
   - 影响：Actor 会停止并播放死亡动画/震屏，但不会自动重建当前小关，也不会累计失败次数。
   - 处理：后续以独立失败结果契约连接 `IWannerPlayCtrl`，保持死亡现场、黑幕、重建及可能的原生覆盖层顺序明确；
     在该契约完成前不得把本项标记为完整失败流程。
2. **P2：震屏依赖关卡层级命名与 Camera Shaker 环境**
   - 影响：未序列化绑定且子物体不叫 `Feedback` 时只会执行死亡；即使找到 `MMF_Player`，场景缺少匹配的
     Camera Shaker 时也可能没有可见震动。
   - 处理：关卡 prefab 使用 Odin AutoBind 后保存并核对引用；PlayMode 验证实际相机，缺失时保留 warning
     降级，不阻断死亡主流程。
3. **P2：重复碰撞及死亡/成功竞态尚未实测**
   - 影响：代码已通过实例级 `_isResultSubmitted` 互斥，但物理同帧回调顺序和关卡具体 Collider 组合
     尚无运行证据。
   - 处理：PlayMode 覆盖同帧多 Collider、死亡后进入门、成功后触发死亡及 Reset 后新实例四类路径。

### 回滚

- 从 `IWannerLevelCtrl` 移除死亡 Feedback 引用、死亡 API、互斥状态和 AutoBind 绑定即可恢复原行为；无需修改
  `ILevel_001` 既有 Feedback 资源或共享 Actor。

## GMPanel 上一小关与当前小关成功风险补充（2026-08-10）

### 原始基线与当前边界

- 修改前，GMPanel 的上一关和非 Draw 直接胜利都会进入主线链，IWanner 没有对应调试入口。
- 修改后，GMPanel 只通过统一 `IGMFeature` 调用活动 Controller；IWanner 上一小关复用现有小关黑幕重建流程，
  直接成功复用 `IWannerLevelCtrl.TrySuccess()`，不支持的下一关返回 false。本次没有补齐 Group Victory、
  Reward、通关存档、正式 analytics、广告或失败闭环。

### 风险项

1. **P1：上一小关的黑幕和资源重建只有静态/编译证据**
   - 影响：代码沿用 Reset/Replay 的 `ReloadCurrentLevelAsync()`，但连续从第 5 小关回退到第 1 小关时的黑幕、
     GameplayPanel Q 弹、输入恢复、资源释放和旧异步过滤尚未实跑。
   - 处理：Unity PlayMode 按 5→4→3→2→1 连续回退，并在黑幕过渡中快速重复点击；确认始终只有一个关卡实例和
     一个 IWanner GameplayPanel。
2. **P2：第一小关与不可操作状态只有 warning 反馈**
   - 影响：索引为 0、加载中或运行时无效时 API 返回 false，GMPanel 只写 warning，不显示玩家提示；这是调试
     工具可接受的降级，但需要在 PlayMode 确认不会误改主线关卡。
   - 处理：覆盖第一小关、切玩法中、退出后、暂停中点击上一关，核对 IWanner 和主线索引均不发生意外变化。
3. **P1：强制成功依赖现有门与末关未完成边界**
   - 影响：`TrySuccess()` 会复用成功门停靠与进门表现；门引用/动画缺失时按既有降级继续推进。最后一小关仍只
     进入 `HandlePendingGroupCompletion()`，不会出现正式 VictoryPanel 或奖励。
   - 处理：PlayMode 覆盖普通推进、门引用缺失降级、死亡后拒绝、暂停拒绝、重复点击和末关日志；Group Victory
     正式接入前不得把 GM 末关成功视为完整通关闭环。

### 验证与回滚

- 定向构建结果为 `0 error / 101 warning`；静态确认未新增 DB、配置、资源路径、全局事件、广告、analytics 或
  平台代码。
- Unity PlayMode、连续切关、异常资源和 Android/iOS 横屏调试触控仍为 `uncertain`。
- 回滚时移除 `IWannerPlayCtrl` 的 `IGMFeature` 实现，并恢复 GMPanel 的具体玩法分流；不需要迁移或清理存档。

## ILevel_001 关卡配色资产风险补充（2026-08-10）

### 原始基线与变更入口

- 变更前 `ILevel_001` 的 Ground 与 Camera 颜色直接保存在 prefab，GameplayPanel 图片沿用 UI prefab 白色；
  运行时没有统一的关卡配色入口。
- 变更入口为新增 `IWannerLevelColorSettings`、`ILevel_001_ColorSettings.asset`、
  `ILevel_001.prefab` 资产引用，以及 LevelCtrl → PlayCtrl → GameplayPanel 的运行时应用链。
- 按用户确认只为 `ILevel_001` 配置一份资产；其它 IWanner 小关保持原始序列化颜色。

### 风险项

1. **P1：Ground 依赖名称，Trap 依赖组件层级**
   - `IWannerLevelCtrl` 修改当前关卡子层级内名称以 `Ground` 开头，或位于 `OnTriggerTrapHandle` 组件层级内的
     `SpriteRenderer`；两类目标都使用 `GroundColor`。
   - 影响：后续美术若把地面节点改为其它名称，目标会保留 prefab 原色；若非地面装饰误用 `Ground` 前缀，或把
     不应着色的视觉放入 Trap 层级，也会收到同一颜色。
   - 处理：新增/改名地面时保持 `Ground*` 命名，Trap 视觉保持在对应组件根节点下，并在 PlayMode 核对全部目标；
     需要排除局部视觉时再引入显式列表，不在当前需求中提前增加配置复杂度。
2. **P1：GameplayPanel 着色范围尚无视觉验收**
   - 当前明确覆盖六个操作按钮图标与两组进度图片，排除 BlackMask、ClickBlock 和文字。
   - 影响：非白色 HUD 配色可能与原贴图颜色相乘，导致按钮/进度对比度变化；移动端不同亮度下可读性尚未验证。
   - 处理：后续调色时同时验证未过关/已过关图案、暂停/Reset/方向/跳跃/Skip，以及 Android/iOS 横屏可读性。
3. **P2：Asset 引用和打包依赖只有静态证据**
   - `ILevel_001.prefab` 直接引用配色 asset，Unity/YooAsset 应通过 prefab 依赖收集该资产，但本次没有重新构建
     GamePackage 清单或进行非 Editor 加载。
   - 影响：若资源收集或依赖分析异常，运行时引用可能为空；当前实现会保留原始颜色并继续玩法，不造成入口阻断。
   - 处理：下一次 GamePackage 构建检查依赖清单，并在 YooAsset 实际发布模式进入 `ILevel_001` 验证。
4. **P2：Reset、Replay 与组内切关只有调用链证据**
   - 每次重建都会创建新 GameplayPanel 和新关卡实例，再读取当前 LevelCtrl 资产；没有静态跨关缓存。
   - 影响：尚未实跑快速 Reset/Replay 或跨关时，不能排除一帧显示旧 HUD 原色或视觉闪变。
   - 处理：PlayMode 连续执行进入、Reset、Replay、成功切下一关和退出，确认切色发生在黑幕淡出前。

### 验证、未覆盖与回滚

- 定向 C# 构建通过：`0 error / 102 warning`；静态确认只绑定 `ILevel_001`，GUID 链一致，且未新增编辑器生命周期、
  DB、事件、打点、广告或平台 SDK 链路。
- Unity PlayMode、YooAsset 包依赖、Android/iOS 横屏显示均为 `uncertain`。
- 仅回滚当前关卡表现时移除 `ILevel_001.prefab.colorSettings` 引用并删除资产；完全回滚时再移除新增 SO、
  LevelCtrl/PlayCtrl/GameplayPanel 配色接口，不需要迁移存档或配置。

### 2026-08-11 Trap 配色验证补充

- 新增 Trap 配色只扩展 `ApplySceneColorSettings()` 的运行时目标筛选，不改 prefab、ColorSettings 数据或资源依赖。
- Runtime 构建为 `0 error / 101 warning`，Editor 构建为 `0 error / 4 warning`；Unity PlayMode 视觉、动画覆盖色值及
  Android/iOS 显示仍为 `uncertain`。
- 单独回滚本次增量时移除 `GetComponentInParent<OnTriggerTrapHandle>(true)` 条件即可，Ground 原逻辑保持不变。

### 2026-08-11 GameplayPanel GroundColor 图片列表风险补充

- 原始基线：`GroundColor` 只作用于关卡 Ground/Trap SpriteRenderer；GameplayPanel 只消费
  `GameplayPanelColor` 与 `SkipTextColor`。当前增量影响 LevelCtrl、PlayCtrl、GameplayPanel 脚本及 GameplayPanel
  prefab，把同一 `GroundColor` 额外应用到六个显式绑定的 `colorBg` Image。
- **P1：GroundColor 与 UI 底图颜色、透明度现在共用。** 后续调低 GroundColor Alpha 或使用低对比度色值时，六个
  `colorBg` 也会同步变透明或降低 HUD 可读性。处理：每次新增/调整配色资产时，同时在 PlayMode 与 Android/iOS
  横屏检查六个操作区域；若未来需要独立调色，应新增经过确认的专用字段，而不是在面板内推导颜色。
- **P2：序列化列表需要随 prefab 节点维护。** 当前六个引用已静态确认均为 Image，且节点 `RaycastTarget` 为 false；
  未来新增按钮或替换节点若未同步列表，新节点会保留 prefab 原色。处理：修改 GameplayPanel 按钮结构时检查
  `groundColorImages` 数量、空引用和目标节点，不使用运行时层级扫描自动扩张范围。
- 定向 C# 构建通过：`0 error / 101 warning`；静态确认运行时调用链为
  `IWannerLevelColorSettings.GroundColor -> IWannerLevelCtrl -> IWannerPlayCtrl -> ApplyGameplayColors()`，未新增
  Editor 生命周期、DB/Save、事件、资源加载或逐帧逻辑。Unity PlayMode、Reset/Replay/跨小关视觉和移动端显示均为
  `uncertain`。
- 回滚时移除 GameplayPanel 列表/绑定及三层接口新增的 Ground 参数；不会改变场景 Ground/Trap 的既有应用链，
  也不需要迁移配色 asset、配置或存档。

## IWanner 入口 IsOpen 风险补充（2026-08-10）

### 原始基线与当前边界

- 修改前入口不读取 `GetIWannerGameIsOpen()`；修改后 `IWannerGameEnter` 在启用/刷新时控制整个入口根节点，
  并在点击时二次校验。本次不改变配置来源优先级，也不增加游戏内实时更新行为。

### 风险项

1. **P1：入口配置读取依赖 Loading 已完成**
   - 影响：getter 在远端缓存缺失时回退已初始化的 GlobalConst；若异常路径在 ConfigManager 初始化前创建入口，
     返回 false 会让入口以关闭状态降级。
   - 处理：保持 StartPanel 位于正常 Loading 完成之后；PlayMode 增加正常启动、缓存缺失和初始化异常三条验证。
2. **P2：关闭入口通过停用组件根节点实现**
   - 影响：当前 prefab 中组件位于完整 `WannerGameEnter` 根节点，行为正确；若未来把组件移到 `clickBtn`，关闭时
     只会隐藏按钮而残留图片或气泡。
   - 处理：调整 prefab 层级时复核组件所有者；保持组件挂在入口根节点，或届时再引入显式入口根引用。
3. **P2：游戏内配置变化不会即时重新显示入口**
   - 影响：符合当前“只在 Loading 后更新”的需求；调试时直接修改缓存不会自动触发 Refresh。
   - 处理：通过重新 Loading/重建入口验证新配置，不新增轮询或全局配置变化事件。

### 验证与回滚

- 定向 C# 构建为 `0 error / 101 warning`；静态确认未新增 DB/Save、事件、实例化、逐帧入口或平台分支。
- Unity PlayMode、真实 Firebase、旧缓存回退和 Android/iOS 横屏仍为 `uncertain`。
- 回滚时恢复 `IWannerGameEnter` 原 OnEnable/Refresh/点击流程即可；配置与 prefab 不需要回滚。

## IWanner 普通失败计数与 RewardAd Skip 风险补充（2026-08-11）

### 原始基线、受影响范围与现有系统

- 原始行为是 Actor 死亡后停留在死亡现场，GameplayPanel 的 Skip 永远隐藏；已有远端失败阈值 getter、
  `FailAreaTriggerHandle`、Actor 死亡表现和黑幕重建链均未组成失败闭环。
- 失败闭环首次接入后，Skip 达到阈值会直接进入下一小关；当前增量将其改为必须收到 RewardAd 奖励成功回调后推进。
- 本次修改 `IWannerLevelCtrl.cs`、`IWannerPlayCtrl.cs`、`IWannerGame_GamePlayPanel.cs`、
  `IWannerGame_GamePlayPanel.prefab` 和本地文档；
  RewardAd 增量只修改 `IWannerPlayCtrl.cs` 和文档，复用 Bear Event、Bear UI、UniTask、`RewardAdHelper`、
  `RewardPlacement.InGamePassLevel`、现有 `OnTriggerFailAreaEvent`、RemoteConfig getter 与关卡重建流程。
- 不修改关卡 prefab、共享 Actor、主线/Draw 生命周期、DB/Save、Analytics、广告 SDK 或资源收集配置。

### 风险项

1. **P1：共享失败区域事件需要 PlayMode 证明玩法隔离**
   - 影响：IWannerLevelCtrl 只在自身启用期间订阅 `OnTriggerFailAreaEvent`，并用死亡/成功/暂停门禁保证实例内幂等；
     但若 Main 场景仍残留其它启用的关卡失败监听者，同一个 FallArea 事件可能被多个玩法对象消费。
   - 处理：从 StartPanel 进入 IWanner 后检查活动层级和订阅效果，确认失败只重建 IWanner 当前小关；退出后再次触发
     主线失败，确认已销毁的 IWanner 实例不再响应。
2. **P1：失败计数和第 n 次显示只有静态证据**
   - 影响：代码使用 `_currentLevelFailureCount >= failureThreshold`，默认阈值 2 时第二次自动失败或手动 Reset 后，
     新建面板才显示 Skip；配置小于等于 0 时关闭。尚未实跑混合失败序列和边界值。
   - 处理：PlayMode 分别验证阈值 2、1、0、负数；覆盖“自动失败 + 手动 Reset”混合累计，并确认成功、Skip、
     Replay、GM 回退和退出都会清零。
3. **P1：ResetClickArea 层级、触控和重复提交未实跑**
   - 影响：死亡后常规 HUD 输入关闭、`ClickBlock` 解除，并显示默认隐藏的全屏 `ResetClickArea`；静态 YAML 已确认
     它使用全拉伸 RectTransform、透明射线组件和 `CustomButton`，兄弟顺序位于全部常规 HUD 控件之后、
     `BlackMask/ClickBlock` 之前。失败状态会隐藏后两者，但实际横屏触控和 EventSystem 命中仍未实跑。
   - 处理：PlayMode 与 Android/iOS 横屏分别验证死亡前不可见、死亡后全屏可点、点击空白位置派发一次
     `IWannerGameResetEvent(Failed)`、快速连点只重建一次，以及 Reset/Replay/退出后不残留射线拦截。
4. **P1：RewardAd 原生覆盖层与输入恢复尚未实机验证**
   - 影响：代码在调用广告前已设置 `_isPaused`、关闭 HUD/Actor 输入、调用 `SetPaused(true)` 并显示 ClickBlock；
     只从广告结果回调恢复或推进，不依赖原生覆盖层期间的 Unity PlayerLoop、Tween 或延时。但真实 SDK 的奖励成功、
     展示失败、玩家关闭、等待弹窗取消、应用切后台/回前台时序仍可能与编辑器静态证据不同。
   - 处理：Android/iOS 真机分别验证成功只推进一次；失败、关闭和取消不推进并恢复角色/HUD；广告期间 Reset、Replay、
     暂停和 GM 命令均不生效；回前台后遮罩、时间状态和输入无残留。
5. **P1：退出玩法与迟到广告回调存在跨会话风险**
   - 影响：`RewardAdHelper.CancelCurrent()` 只结束当前加载/等待源，不能据本地代码证明已经关闭正在展示的原生广告；
     用户退出或玩法切换后，SDK 仍可能迟到回调。
   - 处理：`Deactivate()` 先清除请求标记、递增 activation version，再清理关卡；广告回调同时校验请求状态、
     activation version、LevelCtrl 和 GameplayPanel 引用，旧会话回调不会推进或解锁新会话。真机仍需覆盖展示中退出。
6. **P1：Group 末关仍没有正式 Victory**
   - 影响：末关成功或 Skip 都清空失败次数并进入既有 `HandlePendingGroupCompletion()` 占位逻辑，只有 warning，
     不展示 VictoryPanel、不写进度或奖励。
   - 处理：正式验收 Skip 前补齐 Group Victory；当前测试把末关行为标记为 `uncertain`，不得宣称完整通关。
7. **P2：奖励广告不会更新 IWanner 的插屏策略**
   - 影响：`RewardAdHelper` 只在活动控制器实现 `IInterstitialAdPolicyHost` 时调用 `OnRewardAdShown()`；
     当前 `IWannerPlayCtrl` 未实现该接口，因此 RewardAd 成功不会记录 IWanner 插屏抑制状态。
   - 处理：本需求只约束 Skip 必须观看 RewardAd，不扩展 IWanner 插屏策略；未来接入 IWanner 插屏时需统一设计并回归。
8. **P2：失败次数只存在内存**
   - 影响：退出玩法、重启应用或 Controller 清理后次数丢失，这是当前定义的生命周期；若后续要求跨会话保留，
     需要先明确存档版本和 Group/小关身份，不能直接复用当前字段。
   - 处理：当前不写 DB，未来随 IWanner Group 进度存档统一设计和迁移。

### 验证、未覆盖与回滚

- RewardAd 增量 Runtime 定向构建通过：`0 error / 101 warning`；失败闭环首次接入时 Editor 定向构建为
  `0 error / 4 warning`，warning 均为既有项。
- 静态确认实例级失败结果事件、失败计数、ResetClickArea 默认隐藏/字段绑定/失败事件派发、阈值 getter、
  Skip 局部回调、RewardAd 成功/失败分支、广告期间互斥门禁、退出取消和跨会话校验均有明确代码入口；
  Unity PlayMode 与真实广告未执行。
- `uncertain`：Unity PlayMode、真实 RemoteConfig 覆盖、连续失败/Reset/Skip、RewardAd 成功/失败/关闭/取消、
  展示中退后台或退出玩法、末关、YooAsset 发布模式以及 Android/iOS 横屏触控均未验证。正式 analytics、存档与
  Victory 明确不在本次范围。
- 只回滚 RewardAd 增量时恢复 `TrySkipCurrentLevel()` 的旧直接推进实现并移除请求状态、回调恢复和并发门禁；
  完整回滚失败闭环时再恢复三处脚本并移除 GameplayPanel prefab 的 `ResetClickArea` 节点和字段绑定。
  没有新增存档数据，不需要数据迁移。

## IWanner LevelCtrl 与 PlayCtrl 实例事件解耦风险补充（2026-08-11）

### 原始基线与受影响范围

- 原始实现由 `IWannerPlayCtrl` 向 `IWannerLevelCtrl.InitializeRuntime(...)` 注入成功、失败两个异步委托；
  LevelCtrl 保存并执行委托，同时捕获门动画、重建和推进阶段的异常。
- 当前实现复用 DrawGame 的实例级结果通知模式：LevelCtrl 同步派发一次 `Resolved`，PlayCtrl 订阅后执行
  Success 异步流程或 Failed 点击等待流程。结果事件本身不改变配置、存档或资源地址。

### 风险项

1. **P1：订阅生命周期需要 PlayMode 证明**
   - 影响：代码保证初始化前订阅、Shutdown 前退订，并由 `_isResultSubmitted` 拒绝同一实例重复结果；但连续
     Reset/Replay/退出时是否存在 Unity 销毁帧末的旧实例触发，仍只有静态证据。
   - 处理：连续执行成功、死亡、Reset、Replay 与退出组合，确认每个结果只推进一次且旧实例不再进入 PlayCtrl。
2. **P1：异步异常所有权已迁移到 PlayCtrl**
   - 影响：门动画和推进异常不再由 LevelCtrl 恢复 Actor 输入，而由 PlayCtrl 的
     `HandleLevelResultException(...)` 进入现有打开失败清理；这避免两侧状态分裂，但降级界面需实跑确认。
   - 处理：在测试组件中主动取消/抛出门动画等待，确认旧会话静默退出，当前会话异常则完成关卡/HUD 清理。
3. **P1（已静态缓解）：同步结果处理异常可能锁死关卡**
   - 原风险：LevelCtrl 在事件触发前已锁定 `_isResultSubmitted`；若 PlayCtrl 的同步日志、UI 切换或 Bear 事件派发
     抛出异常，异常会反抛至发布者并让关卡停在已结算状态。
   - 当前处理：`OnLevelResolved()` 捕获整个同步分支；当前实例异常会递增 activation version 并进入既有
     `HandleOpenFailure()` 清理，过期实例异常只记录日志。异常注入和最终降级画面仍待 PlayMode 验证。
4. **P2：结果事件没有全局观察者**
   - 影响：这是刻意的局部边界；未来统计或其它系统若需要结果，不能假设 Bear 全局事件已经收到通知。
   - 处理：正式 Analytics 仍由 PlayCtrl 在明确生命周期节点单独接入，不把 LevelCtrl 结果事件扩散为全局事件。

### 验证缺口与回滚

- 静态检查确认旧委托字段、LevelCtrl 异步包装和调用引用已清理，订阅/退订成对存在；
  `Assembly-CSharp.csproj` 定向构建为 `0 error / 101 warning`。
- `uncertain`：Unity PlayMode、Android/iOS 横屏、门动画取消、死亡点击等待中退出及二十次连续重建尚未验证。
- `uncertain`：同步异常注入后的运行时清理、StartPanel 返回和 ClickBlock 恢复尚未实跑。
- 回滚可恢复双委托初始化和 LevelCtrl 异步包装，移除结果事件及 PlayCtrl 订阅；无需迁移数据或修改资源。

## IWanner Skip 文本独立配色风险补充（2026-08-11）

### 原始基线与受影响范围

- 原始 HUD 配色只覆盖按钮/进度图片，Skip 文本保留 prefab 色；当前新增独立 `skipTextColor`，只通过既有
  LevelCtrl → PlayCtrl → GameplayPanel 初始化链作用于已绑定的 `ULevelDevilSkipTxtBtn`。
- 本次不修改 GameplayPanel prefab、自动生成字段、其它文字、DB、事件、资源路径或平台流程。

### 风险项

1. **P1：Skip 文本对比度只有静态颜色证据**
   - 影响：ILevel_001 初值保持 prefab 原视觉色，但后续调色可能让文本与 Skip 按钮图片色对比不足；移动端不同
     屏幕亮度和语言字形下的可读性尚未验证。
   - 处理：PlayMode 与 Android/iOS 横屏同时检查 Skip 隐藏/显示、不同背景和本地化文本；调色时把图片色与文本色
     作为一组验收。
2. **P2：生成字段绑定丢失时采用静默降级**
   - 影响：代码对 `ULevelDevilSkipTxtBtn` 做空引用保护；若后续重新生成字段或 prefab 解绑，图片色仍会应用，
     Skip 文本则保留 prefab 原色，不阻断玩法但可能造成视觉不一致。
   - 处理：UI 自动绑定/重新生成后核对 `ULevelDevilSkipTxtBtn` 与 fileID，并在静态资源检查中保留该项。
3. **P2：新增 ScriptableObject 字段依赖资源序列化与打包**
   - 影响：ILevel_001 asset 已有静态 YAML 字段，但本次没有重新构建 YooAsset GamePackage 或实跑非 Editor 加载。
   - 处理：下一次资源包构建检查 prefab 对配色 asset 的依赖，并在发布模式进入 ILevel_001 验证。

### 验证、未覆盖与回滚

- Runtime 定向构建 `0 error / 0 warning`，Editor 定向构建 `0 error / 4 warning`；静态确认字段、asset 与 prefab
  绑定链成立。
- `uncertain`：Unity PlayMode 视觉验收、YooAsset 发布模式以及 Android/iOS 横屏显示未验证。
- 回滚只需移除新增颜色字段和双颜色传递，恢复原单色 HUD 应用；不涉及 prefab 或数据迁移。

## IWanner 进门成功动画节奏延迟风险补充（2026-08-11）

### 原始基线、影响范围与现有系统

- 原始成功链在 Actor 瞬移进门后立即关门，并在 effect fade 完成后立即进入下一小关重建；已有成功幂等、
  activation version、当前关卡销毁令牌、输入锁和黑幕重建边界不变。
- 本次只在 `IWannerGame_EnterDoorAnimCtrl.PlayAsync()` 前后增加两段缩放时间等待，并在共享
  `IWannerGame_Door.prefab` 序列化默认值；没有修改 LevelCtrl/PlayCtrl、DB/Save、配置、资源路径、事件、打点、
  广告或平台 SDK。

### 风险项

1. **P1：最终动画节奏只有静态和编译证据**
   - 影响：默认两段各 `0.3` 秒能形成可见停顿，但与实际关门 Spine 时长、effect fade、美术构图及黑幕衔接是否
     匹配仍需在目标关卡目测；数值过大会让成功反馈显得卡顿。
   - 处理：PlayMode 分别测试默认值、两项为 `0`、只放大第一段、只放大第二段，并在 Inspector 中按实际观感调节。
2. **P1：等待使用缩放时间**
   - 影响：实现与既有 effect DOTween 保持同一时间口径；若成功后外部把 `Time.timeScale` 设为 `0`，两段等待及
     既有 fade 都会暂停，直到恢复时间，不应误判为切关丢失。
   - 处理：验证成功过程中 Pause/Resume、退出和销毁；若产品后续要求暂停态仍推进，需把整段门表现统一改为
     unscaled time，不能只改其中一个 delay。
3. **P2：共享门 prefab 默认值会作用于所有复用该门的 IWanner 小关**
   - 影响：默认节奏一致可减少配置遗漏，但不同门动画长度可能需要关卡 prefab override；当前尚未逐关验收。
   - 处理：逐个复用门的小关核对动画长度，需要差异时只做关卡实例 override，不改共享 Actor 或 PlayCtrl。
4. **P2：直接调用 `Execute()` 不包含成功流程专用前后等待**
   - 影响：当前 prefab 的门触发器只调用 `IWannerLevelCtrl.Success(Collider2D)`，正式成功链通过 `PlayAsync()`，
     因而两段延迟生效；未来若把该组件直接交给其它 TriggerExecutor 执行，则只保留原关门与 fade 行为。
   - 处理：新增调用方前先确认是否需要完整成功节奏；需要时调用 `PlayAsync()`，或在有多处复用证据后再统一
     BaseItemEventHandle 的完成语义，当前不扩大改动。

### 验证缺口与回滚

- Runtime 定向构建为 `0 error / 101 warning`，Editor 定向构建为 `0 error / 4 warning`；静态确认两段等待顺序、
  prefab 默认值、取消令牌和调用链成立。
- `uncertain`：Unity PlayMode、门 Spine/fade 实际观感、Pause/退出竞态、各 IWanner 小关 override 及
  Android/iOS 横屏表现尚未验证。
- 回滚只需删除两个字段、两段 `UniTask.WaitForSeconds` 及共享门 prefab 对应 YAML；不涉及存档迁移、配置回滚
  或资源地址变化。

## IWanner 角色离地禁跳风险补充（2026-08-11）

### 原始基线、受影响范围与现有系统

- 原始 `IWannerLevelCtrl.TriggerJump()` 对当前 `ActorCtrl` 直接调用共享 `TriggerJump()`；共享跳跃逻辑除接地外，
  还允许土狼时间和水中跳跃，并无条件登记空中跳跃缓冲请求。
- 本次只修改 `Actor_IWanner.cs` 与 `IWannerLevelCtrl.cs`：前者提供接地限定入口，后者在收到既有
  `PlayerJumpEvent` 后对 IWanner Actor 使用该入口。共享 `ActorCtrl`、玩家输入事件、GameplayPanel、
  `Actor_IWanner.prefab`、五个关卡 prefab、DB/Save、配置、资源地址、打点和平台 SDK 均不修改。

### 风险项

1. **P1：离地边界依赖共享接地检测时序**
   - 影响：局部入口以 `ActorCtrl.IsGround` 为唯一依据；平台边缘、斜面或起跳后的同一物理帧内，
     `groundCheckSize/GroundCheckDistance/whatIsGround` 的检测结果可能仍为 true，从而影响禁跳开始的精确帧。
   - 处理：`Actor_IWanner` 已用本地落地周期锁拦截起跳后同帧重复请求，并只在 `HandleLanding()` 清除；PlayMode
     仍需覆盖走出平台后按跳、上升/下落阶段按跳、斜面边缘和重新落地，确认接地检测转 false 的时点符合手感。
2. **P1：真实触控与多指输入尚未验证**
   - 影响：HUD 使用离散 `OnClickDown -> PlayerJumpEvent`，代码不会轮询或持续跳跃；但 Android/iOS 多指快速点击
     在物理帧边界是否产生预期的一次地面跳跃，只有静态证据。
   - 处理：Android/iOS 横屏覆盖按住方向同时连续点跳、走出平台后点跳、落地前后快速点击，确认空中无二段抬升、
     落地后新请求可正常起跳。
3. **P2：非 Actor_IWanner 的配置兜底保留共享语义**
   - 影响：`IWannerLevelCtrl` 为避免扩大既有兼容性，在 actor 不是 `Actor_IWanner` 时仍调用共享入口；当前五个
     IWanner prefab 已静态确认均引用 `Actor_IWanner.prefab`，但未来错误绑定普通 Actor 会绕过本地规则。
   - 处理：新增 IWanner 关卡时沿用 Checklist 的 Actor prefab 绑定检查；若未来出现第二种 IWanner Actor，
     应在对应局部子类提供相同入口或再抽取玩法内接口，不修改共享 `ActorCtrl`。

### 验证缺口与回滚

- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 101 warning`；静态确认所有现有 IWanner 关卡引用
  `Actor_IWanner.prefab`，新调用链不会触发共享土狼时间、双跳、水中跳跃或空中缓冲分支。
- `uncertain`：Unity PlayMode 的物理帧边界、平台/斜面接地精度，以及 Android/iOS 横屏触控尚未验证。
- 回滚只需删除 `TriggerGroundedJump()`，并恢复 `IWannerLevelCtrl.TriggerJump()` 对共享入口的直接调用；
  不涉及 prefab、配置、存档或资源迁移。

## IWanner 进门震屏与中等手机震动风险补充（2026-08-11）

### 原始基线、影响范围与现有系统

- 原始成功进门只负责位置同步和结果上报；死亡路径已有 `MMF_Player/MMF_CameraShake`，项目已有
  `VibrationManager`、`VibrationIntensity.Medium` 和玩家震动开关。
- 本次只在 `IWannerLevelCtrl.TrySuccess()` 的首次成功锁内复用上述能力；五个 IWanner prefab 和
  `IWannerPlayCtrl` 均只读核对，没有新增资源、序列化字段、DB/Save、配置、全局事件、Analytics、广告或 SDK。

### 风险项

1. **P1：震屏幅度与进门节奏只有静态和编译证据**
   - 影响：成功与死亡当前共用各关卡的同一 `MMF_CameraShake` 参数，能保证资源无需新增，但死亡冲击感未必正好
     适合作为进门反馈；震屏与 0.3 秒关门前延迟的整体手感尚未目测。
   - 处理：PlayMode 逐关验证震屏一次、关门前停顿和 fade 衔接；如需不同强度，后续增加独立成功 Feedback 引用，
     不直接改死亡资源或共享相机系统。
2. **P1：手机震动存在平台差异**
   - 影响：Android Medium 在现有实现中为 100ms/振幅 128；iOS 的现有 `VibrationManager` 使用系统振动，无法按
     该实现区分 Light/Medium/Heavy。当前 AndroidManifest 未显式出现 `android.permission.VIBRATE`，最终权限是否
     由 Unity/依赖合并注入尚未验证。
   - 处理：Android/iOS 真机分别验证；Android 构建后检查最终合并 Manifest，不能只依据工程模板判断权限。
3. **P1：震动开关与初始化依赖运行时 GameSetting**
   - 影响：`VibrationManager` 首次调用会读取 `GameSetting.VibrationOn`；正常成功链位于 PlayMode 运行时，玩家关闭
     震动时应静默跳过。异常启动顺序或缺少设置时管理器会按现有逻辑默认启用并记录 warning。
   - 处理：验证震动开/关、首次进入 IWanner、从其它玩法切入和设置切换后的行为；本次不修改共享管理器策略。
4. **P2：共用字段名称与职责不完全一致**
   - 影响：`deathFeedback` 现在也承载成功进门震屏；保留名称可避免五个 prefab 序列化迁移，但阅读代码时可能产生
     仅死亡用途的误解。
   - 处理：字段注释已明确兼容原因；只有后续确需分别调参时才新增局部成功字段，不为当前单一反馈需求迁移资源。

### 验证缺口与回滚

- Runtime 定向构建为 `0 error / 101 warning`，Editor 定向构建为 `0 error / 4 warning`；静态确认五个关卡均有
  Feedback 节点和有效序列化字段，新调用位于成功幂等锁内且震屏缺失时仍会继续调用手机震动。
- `uncertain`：Unity PlayMode 视觉、重复 Collider、震动设置开关、Android 合并 Manifest、Android/iOS 真机震感
  和设备无振动器时的降级日志尚未验证。
- 回滚只需移除成功路径的本地表现方法、调用和命名空间引用，并恢复字段注释；不涉及 prefab、资源或数据迁移。

## IWanner Group VictoryPanel 风险补充（2026-08-11）

### 原始基线与受影响范围

- 原末关成功链只有占位 warning；IWanner Victory prefab 根组件仍是 DrawGame 类型，且 IWanner 独立存档没有静态
  `DB` 访问入口。
- 当前改动影响 IWanner Victory 两个脚本与 prefab、`IWannerPlayCtrl`、`IWannerGame` 集合兼容及独立 DB 入口；
  DrawGame/主线 Victory、关卡 prefab、配置、广告、Bag、SDK、RemoteConfig 和平台代码不修改。

### 风险项

1. **P1：首通/复通分流依赖 IWanner 存档成功落盘。**
   - 影响：保存失败时当前内存仍含已通关 Group Id，但应用重启后可能再次显示 Reward 版。
   - 处理：当前记录 warning 并保留 Draw 进度一致的失败基线；PlayMode 需覆盖保存成功、模拟失败与重启复通。
2. **P1：Reward 版只展示、不发真实奖励。**
   - 影响：广告按钮可见但不可交互，若视觉仍暗示可领奖，可能造成玩家预期不一致。
   - 处理：在 Unity 中确认产品表现；接入真实 RewardAd 前必须单独设计奖励幂等、Bag/Save、原生覆盖层和输入恢复。
3. **P1：Prefab 类型替换尚未由 Unity 实际导入验证。**
   - 影响：如果新脚本 GUID、基类私有序列化字段或 Resources 名称导入异常，`OpenUI<T>` 会返回 null并进入选组降级。
   - 处理：Unity 重新导入后核对根组件无 Missing Script、所有引用非空，并分别直接打开两个资源。
4. **P1：Victory 状态与导航竞态只有静态门禁。**
   - 影响：Spine 入场期间、按钮组 Tween、系统返回键、快速双击或其它 Popup 插队可能导致重复导航或输入残留。
   - 处理：`_isGroupResolved` 阻止暂停插队，面板用单次导航门禁；仍需 PlayMode 覆盖入场前后快速点击与遮罩射线。
5. **P1：正式 Group 进度、时长和打点未随 Victory 一起完成。**
   - 影响：当前只写 `PassedLevels`，选组仍不显示锁定/通关态，也没有 attempt 时长、成功次数或正式事件。
   - 处理：保持 Checklist 对 currentGroup/currentLevel、解锁、analytics 和时长条目为未完成，不把本次静态闭环扩大为完整发布验收。
6. **P2：移动端和资源模式尚未验证。**
   - 影响：Android/iOS 横屏可能出现按钮区域缩放、Canvas 射线、Spine 或转盘性能/时序差异。
   - 处理：完成 Editor PlayMode 后补 YooAsset/Resources 实际加载及 Android/iOS 横屏冒烟。

### 验证与回滚

- 定向 C# 构建为 `0 error / 25 warning`；静态确认未新增事件类型，Reward 版不调用 RewardAd/Bag，两个 prefab 根组件
  已切换到 IWanner 脚本。Unity PlayMode、存档重启、资源加载和真机仍为 `uncertain`。
- 完整回滚为：恢复末关占位逻辑、移除 Victory 事件订阅与 Group 存档写入、移除两个 IWanner Victory 脚本及 prefab
  根组件引用；已有 IWanner PlayerPrefs 不会自动删除。

## IWanner 陷阱事件失败适配风险补充（2026-08-11）

### 原始基线与改动

- 原 `IWannerLevelCtrl` 只消费 `OnTriggerFailAreaEvent`；`OnTriggerTrapHandle` 派发的 `OnTriggerTrapEvent` 只能产生
  Actor 死亡表现，不能进入 IWanner 实例结果和失败 UI。
- 当前新增 IWanner 运行实例对既有陷阱事件的订阅，并在 Actor 已被陷阱置为死亡时跳过重复 `Die()`；不修改主线
  陷阱脚本、事件定义、资源、存档或配置。

### 风险项

1. **P1：`ILevel_006` 的陷阱粒子引用为空。**
   - 影响：`OnTriggerTrapHandle.PlayKnock()` 在派发事件前访问 `knock.transform`，当前 prefab 实际命中时会被空引用
     中断，本次监听本身无法绕过该数据问题。
   - 处理：由关卡 prefab 补齐 `knock` 引用后，在 PlayMode 实际碰撞验证完整事件链。
2. **P1：陷阱事件是 Bear 全局事件。**
   - 影响：若 IWanner 运行期间仍存在其它启用的主线或 Draw 监听者，同一事件可能被多个玩法对象消费。
   - 处理：PlayMode 检查活动层级；验证进入 IWanner、退出 IWanner、返回主线后三个阶段均只有当前玩法响应。
3. **P2：陷阱结果存在既有 0.5 秒延迟。**
   - 影响：Actor 会先死亡，延迟后才显示 IWanner 失败 Reset 区域；使用 scaled time，暂停或 TimeScale 变化会影响时序。
   - 处理：保持主线陷阱现有行为不变，PlayMode 覆盖延迟期间暂停、退出和实例销毁，确认取消令牌不会产生迟到结果。

### 验证与回滚

- Runtime 构建为 `0 error / 101 warning`，Editor 构建为 `0 error / 4 warning`；Unity PlayMode 与 Android/iOS 横屏
  触控仍为 `uncertain`。
- 回滚只需移除 IWanner 对 `OnTriggerTrapEvent` 的订阅/处理并恢复死亡动画调用；无需改动主线或事件定义。

## IWanner Menu 与 Victory Next 返回 StartPanel 后选组风险补充（2026-08-11）

### 原始基线与改动

- 前一版 Menu 与 Victory next 会复用 `TryOpenLevelSelection()`，保持 `IWannerPlayCtrl` 激活并直接创建临时选组弹窗。
- 用户确认两者均应先通过 `GameManager.ReturnToStartPanel()` 切回主线、恢复 StartPanel，再创建选组弹窗；本次不新增
  全局事件、资源路径、存档字段或进度写入，弹窗创建失败时保留 StartPanel。

### 风险项

1. **P1：StartPanel 恢复与 Popup 创建是两步操作。**
   - 影响：主线恢复成功但 Resources 路径、根脚本或序列化引用损坏时，只会显示 StartPanel，无法展示 IWanner 选组。
   - 处理：保持 StartPanel 作为安全落点并记录完整错误；PlayMode 覆盖 Popup 缺失/引用损坏的降级结果。
2. **P1：旧关卡异步链与重复导航需要运行时验证。**
   - 影响：在加载、Victory 结果或玩法切换边界快速点击时，可能出现迟到回调、重复 Popup 或输入状态残留。
   - 处理：`ReturnToStartPanel()` 经控制器切换调用 IWanner `Deactivate()`，增加 activation version 并统一清理；仍需
     PlayMode 覆盖暂停后 Menu、Victory next 连点、切组及马上再次进入。
3. **P2：Menu 返回不会修改断点存档。**
   - 影响：重新选择相同 Group 会按既有 `CurrentLevel` 续关，而不是从第一小关重开；这是当前断点设计，但正式
     选组状态和玩家提示尚未完成。
   - 处理：保留现有行为，不在临时 Menu 链新增存档副作用；正式选组阶段统一补充状态展示和产品验收。
4. **P2：移动端覆盖不足。**
   - 影响：Android/iOS 横屏下可能暴露设置弹窗关闭、选组层级、Canvas 射线或系统返回键竞态。
   - 处理：补充两端横屏冒烟，核对 Menu 点击后旧 HUD 不再接收输入且选组 Item 只响应一次。

### 验证与回滚

- 静态确认 Menu 与 Victory next 共用 StartPanel/选组退出入口、玩法切换负责清理，且没有新增事件或存档写入；
  `Assembly-CSharp.csproj` 构建为 `0 error / 1119 warning`。Unity PlayMode 与移动端仍为 `uncertain`。
- 回滚只需恢复 `OnEnterNextLevelEvent()` 与 `ExitToLevelSelection()` 直接创建选组弹窗的旧实现，无需回滚弹窗、
  Victory 或断点存档。

## IWanner Reward Victory 真实广告风险补充（2026-08-11）

### 事实与直接结论

- 原 IWanner Reward 脚本明确禁用广告按钮且不写 Bag；用户已确认以 DrawGame Reward 的真实奖励逻辑替换该约定。
- 当前 IWanner 复用 `RewardAdHelper`、`RewardPlacement.GameVictory`、`HorizontalRandomAdRewardCtrl`、
  `SimpleBag.AddTool(Coin, RewardType.Ad)`、`CommonUIRewardAnimCtrl` 和 `PropBlock`，没有修改共享广告、Bag 或 SDK 实现。
- 广告请求与领奖动画均通过局部状态锁定 Victory 导航；成功标记在发奖异步流开始前写入，因此同一面板的重复回调
  或重复点击不会再次写入金币。

### 风险项

1. **P1：首通记录早于 Reward 广告完成。**
   - 影响：玩家在广告成功回调前强杀进程时不获得金币，重启后同一 Group 已按复通进入普通 Victory。
   - 处理：保持与当前 Draw 首通资格基线一致；需在 Android/iOS 真机确认产品是否接受该一次性机会边界。
2. **P1：原生广告覆盖与弱网回调未实测。**
   - 影响：SDK 成功、失败、取消、加载超时、退后台或恢复时序可能暴露按钮未恢复、转盘未重启或迟到回调。
   - 处理：调用前先停转盘并锁导航，Level/HUD/GameplayPanel Block 保持锁定；真机逐项验证回调后状态。
3. **P1：Bag 不可用时成功机会不会重试。**
   - 影响：广告成功后若 `GameManager.Instance.Bag` 为空，代码记录 error，但单面板完成标记已经阻止再次领取。
   - 处理：Loading/PlayMode 核对 Bag 初始化顺序；本次保持 Draw 逻辑一致，不另建补偿存档或跨会话领奖状态。
4. **P2：奖励表现缺失只做降级。**
   - 影响：收集动画或 PropBlock 引用丢失时金币已到账，但玩家可能看不到到账反馈。
   - 处理：prefab 静态引用已确认非空；仍需 Unity 导入和 PlayMode 验证实际动画、声音与数字刷新。

### 验证、未覆盖与回滚

- Runtime 构建为 `0 error / 1119 warning`；静态核对广告位、成功/失败分支、Coin/`RewardType.Ad`、幂等状态、
  prefab 引用和无新增事件类型。真实 SDK、Unity PlayMode、资源模式与 Android/iOS 均未验证。
- 回滚为恢复 IWanner Reward 脚本的预览态和不可交互广告按钮；不得自动扣除已经通过 Bag 发放的金币。

## IWanner QBox 进门关闭碰撞风险补充（2026-08-12）

### 原始行为与直接结论

- 原成功链在 QBox 同步到 Door 后仍保持根 `Rigidbody2D.simulated = true`，根与 `pushCollider` 的
  `BoxCollider2D` 也没有关闭，因此门表现等待期间仍可能继续参与 2D 物理接触。
- 当前改动只在 `TrySuccess()` 首次通过幂等门禁后执行：先完成门位置同步，再关闭 Actor 层级全部
  `Collider2D` 和根刚体模拟，最后沿用现有震屏、震动、实例结果与小关推进链。
- 不修改共享 `ActorCtrl`、prefab 或 ProjectSettings 碰撞矩阵，所以影响范围限定为 IWanner 成功实例。

### 风险项

1. **P1：成功流程若异常停留，旧 Actor 不具备恢复碰撞能力。**
   - 影响：`Resolved(Success)` 后若门动画或下一关重建异常，当前 QBox 会保持无物理状态。
   - 处理：这与当前成功结果不可逆、输入持续锁定的生命周期一致；PlayMode 仍需覆盖门组件缺失、动画取消、退出与
     末关 Victory，确认异常降级不会要求恢复旧关卡操作。
2. **P1：动态新增的 Actor 子碰撞体不在首次扫描结果内。**
   - 影响：若成功后又在 QBox 层级创建独立 Collider/Rigidbody，该新对象可能继续参与碰撞。
   - 处理：当前 prefab 静态证据只有根和 `pushCollider` 两个 BoxCollider2D，且成功后没有新增 Actor 子碰撞体的本地
     代码证据；后续若增加此类表现，必须同步扩展进门物理关闭边界。
3. **P2：Actor 更新仍可能切换 `pushCollider` GameObject 的 active 状态。**
   - 影响：成功后 `ActorCtrl.OnUpdate()` 尚可能执行局部检测并激活该节点。
   - 处理：helper 使用包含失活节点的扫描先把其 `BoxCollider2D.enabled` 关闭，同时根刚体 `simulated = false`；即使
     GameObject 被重新激活，也不重新启用 Collider 组件。
4. **P2：移动端与实际物理接触未验证。**
   - 影响：不同关卡 Door/Ground 布局可能暴露视觉穿层、排序或动画落点问题；这些不属于碰撞 API 静态检查可证明范围。
   - 处理：在 Android/iOS 前先用 Unity PlayMode 覆盖正常进门、贴墙进门、落地瞬间进门、连续推进和末关 Victory。

### 验证、未覆盖与回滚

- CodeGraph 已确认 helper 只有 `TrySuccess()` 一个调用者，执行顺序位于位置同步之后、成功结果上报之前；静态确认
  Actor prefab 当前碰撞体范围可被 `GetComponentsInChildren<Collider2D>(true)` 覆盖。
- Unity Editor 已检测并 Hot Reload 目标方法，未观察到本次代码编译错误；独立 dotnet 构建被
  `Temp/bin/Debug` 缺失 Unity/Package DLL 的 `CS0006` 阻断。Unity PlayMode、连续小关和移动端仍为 `uncertain`。
- 回滚为删除 `DisableActorCollisionAfterDoorEntry()` 及 `TrySuccess()` 中的单次调用；无需修改 prefab、碰撞矩阵、
  存档、配置、资源或事件。

## IWanner 左右移动反向映射风险补充（2026-08-12）

### 原始行为与直接结论

- 原 `IWannerLevelCtrl` 将右移、左移全局事件固定映射为 `1f`、`-1f`，没有实例级反向状态或外部设置入口。
- 当前改动新增默认 false 的只读状态 `IsReverse` 和公开写入入口 `SetReverse(bool)`，只在两个移动事件处理方法中交换方向；
  同目录 `IWannerGame_SetReverseListener` 为现有局部触发器提供可配置调用入口，不新增全局事件。
- `SetMoveInput(float)`、移动取消、跳跃、暂停、结果提交和 Actor 实现均未修改，因此直接影响范围限于当前
  `IWannerLevelCtrl` 实例接收到的左右移动事件。

### 风险项

1. **P1：外部调用时序决定当前实例的映射。**
   - 影响：若调用方设置了已经销毁或非活动的 LevelCtrl，当前活动小关仍保持默认方向；若复用同一实例且未显式恢复 false，反向状态会继续保留。
   - 处理：Listener 应显式绑定当前活动小关，或放在该 `IWannerLevelCtrl` 子级以使用父级查找；需要恢复时配置另一个
     `isReverse = false` 的 Listener。PlayMode 覆盖进入、Reset、切关和退出后重建。
2. **P2：运行时切换依赖下一次移动事件刷新角色方向。**
   - 影响：`SetReverse(bool)` 本身不绕过输入门禁直接写 Actor；持续按住方向按钮时由 HUD 下一帧事件更新，未按方向时不会产生额外移动。
   - 处理：保持 setter 无物理副作用，避免暂停或输入锁期间意外启动角色；验证按住左/右进入反向区域及退出区域的帧间表现。
3. **P2：移动端触控尚未验证。**
   - 影响：Android/iOS 不同帧率下动态切换可能出现一帧旧方向的可见位移，静态代码检查无法证明实际手感。
   - 处理：在 Unity PlayMode 后补充 Android/iOS 横屏长按左右、快速交替、切换瞬间松手与暂停/恢复冒烟。
4. **P2：Listener Reset 不恢复玩法状态。**
   - 影响：`BaseItemEventHandle.ResetState()` 只重置执行标志；若同一关卡实例原地 Reset，`IsReverse` 会保留最后一次写入值。
   - 处理：当前保持行为显式，避免通用触发器 Reset 暗中覆盖设计状态；需要 Reset 恢复默认值时，由关卡 Reset 链明确触发
     `isReverse = false` Listener。实际 Reset 绑定仍需 PlayMode 验证。

### 验证、未覆盖与回滚

- 精确搜索确认 `IWannerLevelCtrl.SetMoveInput()` 当前仅由本类左右/取消事件调用；静态检查确认默认 false 保持原映射，
  true 只交换左右符号。CodeGraph 本轮因仓库索引不可用，相关调用关系证据以源码精确搜索为准。
- `git diff --check` 对本次相关文件返回 0；Unity 生成的 `Assembly-CSharp.csproj` 已收录 Listener，Editor 在导入后
  完成一次 `Finished compiling in 20s` 的 C# 编译，导入点之后未出现新的 `error CS`。Unity PlayMode、关卡实际
  Listener prefab 绑定、Reset/重建及移动端仍未验证。
- 回滚为删除 `IWannerGame_SetReverseListener.cs` 及其 meta、删除 `IsReverse`、`SetReverse(bool)` 并恢复左右事件固定符号；
  不需要修改共享事件、Actor、prefab、配置或存档。

## IWanner Actor 跳跃开关与目标刚体跳跃监听风险补充（2026-08-12）

### 事实与直接结论

- 原 `IWannerLevelCtrl.TriggerJump()` 只检查关卡输入门禁，随后驱动当前 Actor 跳跃；当前改动新增默认 true 的
  `IsJumpEnabled` 与公开 `SetJumpEnabled(bool)`，false 时在 LevelCtrl 边界拒绝 Actor 跳跃。
- 新 `IWannerGame_SetJumpEnabledListener` 只通过现有触发执行器调用 LevelCtrl API；新 `IWannerGame_JumpListener`
  复用既有 `PlayerJumpEvent` 与 Bear `EventSubscriber`，只对绑定的 `Rigidbody2D` 写入 Y 速度。两个脚本互不调用，
  不修改共享 Actor、HUD 或事件定义。
- Jump Listener 具有 `Application.isPlaying` 外层保护，并在禁用时 Dispose 订阅；两个脚本均没有 `OnValidate`、`Reset`、
  `ExecuteAlways`、DB、Save、RemoteConfig、Instance 或 Instantiate 路径。

### 风险项

1. **P1：目标刚体允许空中重复响应。**
   - 影响：Listener 不掌握目标的接地语义；连续点击跳跃会重复把 Y 速度写为 `jumpVelocity`，可能形成多段跳效果。
   - 处理：这是当前“每个跳跃事件驱动目标”的直接实现；若具体关卡只允许接地跳，需补充该目标专属的接地证据与门禁，
     不能复用 Actor 的 `IsGround` 假定。
2. **P1：跳跃开关在同一实例内持续保留。**
   - 影响：`SetJumpEnabled(false)` 不会在 `ShutdownRuntime()` 自动恢复；若同一关卡实例原地复用且未显式重新设置，
     Actor 会继续禁跳。新 prefab 实例因属性默认值会恢复 true。
   - 处理：通过 `IWannerGame_SetJumpEnabledListener` 分别配置 false/true，或由其它调用方按实例生命周期显式设置；
     PlayMode 覆盖 Reset、退出和重建。
3. **P2：Rigidbody2D 配置决定实际物理表现。**
   - 影响：目标缺失、`simulated=false`、Static、冻结 Y 约束或不合适的 Gravity Scale 都可能使跳跃不可见；Listener 不改写这些配置。
   - 处理：prefab 绑定时验证 target、Body Type、Simulated、Constraints、Gravity Scale 与期望跳跃高度。
4. **P2：移动端手感尚未验证。**
   - 影响：不同帧率、连续点击及暂停边界可能影响目标跟跳体验，静态代码检查不能证明实际手感。
   - 处理：补充 Unity PlayMode、Android/iOS 横屏单击、快速连击、暂停/恢复和 Reset 冒烟。

### 验证、未覆盖与回滚

- 精确搜索确认 IWanner HUD 在 `_inputEnabled` 为 true 时才派发跳跃事件；ActorCtrl 现有跳跃也使用
  `linearVelocity = new Vector2(linearVelocityX, jumpForce)`，新 Listener 保持水平速度的实现与其一致。
- `git diff --check` 对控制器与两个 Listener 返回 0；Unity 生成项目与 rsp 已收录两个脚本，并使用 Unity 同版本
  Roslyn 按该 rsp 完整编译 Assembly-CSharp，返回 `CSC_EXIT=0`，只有工程既有 warning。
- `uncertain`：未绑定具体 prefab，未执行 PlayMode、设备或构建验证；目标实际
  Rigidbody2D 参数与接地需求尚未指定。
- 回滚为删除两个 Listener 脚本及 meta，删除 LevelCtrl 跳跃状态/API，并恢复 `TriggerJump()` 原门禁；不需要修改共享
  Actor、HUD、事件、配置、存档、资源或平台代码。

## IWanner 第 5 组地图拼接风险补充（2026-08-12）

### 事实与直接结论

- 第 5 组配置映射为 `ILevel_021`～`ILevel_025`；本次按同目录 5-1～5-5 图片的像素边界与 Sprite 100 PPU 换算世界坐标，
  复用现有 Ground、Actor、Door、FallArea、Trap 和 Knock 粒子结构，没有新增脚本、事件、配置或存档字段。
- 参考截图节点已默认失活，运行时地图由实际 SpriteRenderer、Collider2D 与嵌套 prefab 构成，不依赖截图参与碰撞或显示。
- Unity PrefabImporter 已成功导入全部 5 个 prefab；文件级本地引用完整性、fileID 唯一性和预期对象数量检查通过。

### 风险项

1. **P1：像素换算不能替代实际物理手感验收。**
   - 影响：视觉边界与 Actor 脚底、Door 触发器、Trap Collider 的有效边界可能存在约 0.1～0.3 世界单位偏差。
   - 处理：PlayMode 逐关验证出生落地、左右移动、跳跃越刺、进门和死亡重置，再按实际碰撞而非截图外观微调。
2. **P1：5-4 的顶部反向陷阱与边界最复杂。**
   - 影响：顶部 Trap 旋转 180 度，左右边界和中间落坑同时存在；相邻碰撞体若重叠或留缝，可能出现卡墙、穿角或非预期死亡。
   - 处理：重点覆盖贴墙跳、顶部擦碰、两处底部尖刺和中央坑；必要时只调整 `ILevel_024` 的局部位置/尺寸。
3. **P1：多个陷阱共享单个 Knock 粒子。**
   - 影响：当前结果门禁下首次死亡只应消费一次；如果未来允许同帧多角色或并行死亡，共享粒子位置可能被后一次触发覆盖。
   - 处理：保持当前单 Actor 关卡基线；若玩法扩展为多 Actor，再改为陷阱独立粒子或关卡级死亡表现控制器。
4. **P2：参考截图默认失活。**
   - 影响：运行时安全，但编辑器直接打开 prefab 时不会自动显示对照图。
   - 处理：需要校准时临时启用对应 `5-x` 节点，完成后恢复失活，避免截图 UI 进入运行画面。
5. **P2：移动端和连续资源切换尚未验证。**
   - 影响：Android/iOS 横屏宽高比、触控长按、连续创建/销毁和粒子表现可能暴露 Editor 静态检查无法证明的问题。
   - 处理：工程编译恢复后补 5 关连续 PlayMode 冒烟，再执行 Android/iOS 横屏真机检查。

### 验证、未覆盖与回滚

- 已完成 prefab 本地引用、重复 fileID、图片 GUID、Ground/Trap 数量检查；Unity 日志确认 5 个资源成功导入，无本批 YAML、
  反序列化或 PrefabImporter 错误。
- 当前未执行 PlayMode；完整编译受既有 `Level71ShopBtnCtrl.cs` 对缺失
  `ShopPanel.SuppressRewardTipAutoShowOnClose` 的 `CS1061` 阻断，因此地图可玩性和移动端结果均为 `uncertain`。
- 回滚范围只包含 `ILevel_021`～`ILevel_025` 本批 prefab 变更及新增 meta；共享 Ground/Trap/Actor/Door、配置、存档和事件无需回滚。

## IWanner 小关历史重试存档风险补充（2026-08-12）

### 原始行为、影响范围与直接结论

- 原实现的 `_currentLevelFailureCount` 只服务当前小关 Skip 阈值和插屏策略，`DB.IWannerGame.LevelReviveCount` 没有
  写入方；选组 Popup 虽然已绑定并汇总该字典，但新档与未写入旧档只能显示 `0`。
- 当前改动只在 `IWannerPlayCtrl` 既有死亡/手动 Reset 去重边界写入，以 `IWannerLevelData.Path` 为 Key 并调用
  `DB.SaveIWannerGame()`；不修改结果提交门禁、关卡重建、广告覆盖时序、成功/Skip/Replay 或 Analytics。

### 风险项

1. **P1：死亡后的 Reset 不得重复累计。**
   - 控制：死亡在 `ShowFailureResetArea()` 记录；后续 Reset 因 `_isAwaitingFailureReset == true` 跳过手动分支。
   - 未验证：仍需 PlayMode 实测一次死亡再连续点击重置区域，确认只有首次有效结果增加一次。
2. **P1：存档写入失败不会阻断关卡重建。**
   - 影响：内存值已经增加但落盘失败时，本会话 Popup 可能显示新值，重启后可能回退；当前沿用其它 IWanner
     进度写入的 warning 降级策略，不把存档故障升级为玩法失败。
3. **P2：历史 Key 采用小关资源 Path。**
   - 影响：与主线 `LevelPath` 口径一致且可区分每个 IWanner 小关；未来若重命名 Path，旧 Key 不会自动迁移，
     但 Popup 当前汇总全部 Value，不会因 Key 改名丢失历史总数。
4. **P2：每次失败或手动 Reset 增加一次写盘。**
   - 影响：与主线历史 revive 持久化语义一致，但可能与插屏策略的独立保存发生相邻两次 PlayerPrefs 写入，需在
     Android/iOS 高频失败场景观察性能。

### 验证、未覆盖与回滚

- 静态检查确认写入方法恰有两个调用点，Replay、成功和 Skip 链没有调用；完整 C# 构建通过，结果为
  `0 error / 101 warning`。
- `uncertain`：Unity PlayMode、保存失败注入、杀进程重启与 Android/iOS 真机连续失败尚未验证。
- 回滚为删除两个写入调用和私有记录方法；已有字典数据无需清理，选组 Popup 会继续安全汇总历史值。

## IWanner 小关运行统计与 Group 广告解锁风险补充（2026-08-12）

### 事实与直接结论

- 原实现只有进度字段投入运行；四项小关历史数据没有完整生命周期，`LevelReviveCount` 的临时补丁也直接放在
  `IWannerPlayCtrl`。当前已抽为 `IWannerLevelRuntimeStatistics`，统一使用 `IWannerLevelData.Path`，由控制器只负责
  在尝试开始、有效 Tick、Reset、Fail、成功、Skip 成功和退出边界调用。
- 该结构参考主线独立 `LevelRuntimeState`；与 DrawGame 相同，每次尝试只允许开始一次、结算一次。不同点是
  DrawGame 的次数类字典使用 Id 而时长使用 Path，IWanner 四项均使用 Path，避免同一模块混用两套键。
- 原 `UnlockGroups` 只有显示和入口读取。当前 Lock Item 把锁定点击交给 Popup，只有
  `OutGameUnlockLevel` 激励视频成功结果才幂等写入并保存；失败、取消和非 Ad 锁不写入，玩法入口仍二次校验。

### 风险项

1. **P1：尝试边界若漏接会导致次数或时长失真。**
   - 控制：开始点位于资源/运行时初始化完成之后、输入开放之前；所有正式结束边界调用同一幂等
     `CompleteAttempt()`，先清活动标志再写存档，重复结果不会重复结算。
   - 未验证：PlayMode 仍需覆盖加载异常、快速 Reset、死亡动画期间连点、成功与退出竞争、Skip 回调后切场景。
2. **P1：广告覆盖层期间的状态必须可安全恢复。**
   - 控制：Popup 请求期间禁用关闭按钮和重复请求，关闭生命周期主动取消其广告请求；成功回调还检查 Popup
     存活、打开状态和请求所有权。失败或取消仅恢复 UI，不写存档。
   - 未验证：真实 SDK 暂停 Unity PlayerLoop、应用退后台、超时和回调晚到仍需 Android/iOS 真机验证。
3. **P1：保存失败时内存解锁与磁盘状态可能暂时不一致。**
   - 影响：广告成功后先把 Id 加入内存再保存；保存失败只告警并继续进入，当前会话可玩，进程重启后可能重新锁定。
   - 处理：沿用现有 Draw/IWanner 进度保存的降级口径，不把存档故障转化为已观看广告后的玩法阻断；需补保存失败注入验证。
4. **P2：Path 变更不会迁移旧统计 Key。**
   - 影响：配置若重命名小关 Path，新记录会进入新 Key；`revive_txt` 汇总全部 Value 不丢总数，但单关历史会分裂。
   - 处理：Path 属于资源与存档稳定标识，配置迁移时必须显式合并旧 Key，不能在运行时猜测对应关系。
5. **P2：当前有效时长口径不包含加载、暂停、结果等待和控制器非活动时间。**
   - 影响：该口径更接近实际可玩时间，但与纯墙钟时长不同；若后续数据产品要求包含弹窗或广告，需要先更新生命周期约定。
6. **P2：Skip 成功计入历史成功次数。**
   - 影响：它表示该小关本次以成功结果推进，不等同于纯操作通关；当前与 DrawGame 强制胜利的成功结算语义一致。
   - 处理：若产品未来需要区分操作成功和广告跳过，应新增独立维度，不能回头篡改 `LevelSuccessCount` 的既有历史。
7. **P2：非 Ad 锁定类型没有自动解锁实现。**
   - 影响：`LevelLockType.Level` 当前显示待开放提示；不会错误写入 `UnlockGroups`，但对应产品规则仍是 `uncertain`。

### 验证、未覆盖与回滚

- 静态引用确认控制器不直接访问四项统计字典，`UnlockGroups.Add` 只存在于广告成功回调；常规 C# 构建通过，
  结果为 `0 error / 101 warning`。
- `uncertain`：Unity PlayMode、真实广告四类结果、保存失败、杀进程恢复、暂停/弹窗时长口径、快速退出竞争和
  Android/iOS 横屏触控未验证。
- 代码回滚可独立撤回统计对象或广告解锁回调；已有保存数据默认保留。若必须撤回已授予解锁，需要先定义数据迁移和
  已观看广告玩家的补偿策略，不能直接清空 `UnlockGroups`。

## IWanner 第 6 组地图拼接风险补充（2026-08-12）

### 事实与直接结论

- 配置映射已存在：`30026`～`30030` 分别加载 `ILevel_026`～`ILevel_030`；本批只新增 5 个 prefab/meta 和仓库级拼接 Skill。
- 关卡由 `ILevel_025` 的 Actor、Door、Camera、Feedback、FallArea、颜色和成功链模板生成；新增内容只有 Ground 几何、
  Actor/Door 坐标及默认失活参考图绑定，不触碰共享运行时代码、配置、存档、事件或 SDK。
- 静态验证确认 5 个 prefab 的本地引用完整、fileID 无重复、meta GUID 唯一，规格中的 Ground 几何和 Actor/Door 坐标均已写入。

### 风险项

1. **P1：100 PPU 像素换算不能证明实际可玩。**
   - 影响：Actor 脚底与平台顶面、Door 根节点与门台、跳跃距离和边缘容错仍可能需要 Scene/PlayMode 微调。
   - 本批重点：6-3 的连续横向跳台、6-5 的 0.77～0.78 单位窄踏板、6-6 每级约 0.8 单位高差。
2. **P1：固定相机边界来自当前模板而非本批网页截图的可见框线。**
   - 控制：左右与顶部 3 块边界沿用 `ILevel_025` 的实际 Collider 规格，不从含浏览器框架的截图重复推断。
   - 未验证：6-2、6-3 的低平台和 6-6 左下起点与左右边界的连接需要 Unity 内实际检查。
3. **P1：当前没有 PrefabImporter 成功与 PlayMode 证据。**
   - 现状：Unity 已检测文件且 Project Browser 可见目标资源，但 UI 自动化由用户停止，未继续触发 Refresh 或打开目标 prefab。
   - 处理：用户验收时依次打开 5 个 prefab，确认无 Missing、Scene 层级完整，并跑通进入、掉落、重开和进门。
4. **P2：深平台使用 10 单位向下延伸。**
   - 影响：该口径延续上一批，可稳定提供顶面碰撞；若后续新增从侧面或底部穿行机制，深 Collider 可能阻挡设计路径。
   - 处理：当前参考图没有此类穿行要求；若验收发现侧向路径，应只局部缩短对应 Ground，不修改共享模板。
5. **P2：自动化 Skill 依赖 Unity 文本 YAML 契约。**
   - 控制：默认拒绝覆盖已有目标，并校验模板 Ground 组件顺序、Scene 子节点、本地引用、嵌套 prefab GUID 与目标坐标。
   - 回滚：Skill 与生成结果相互独立；工具不兼容未来模板时可以停用，不影响已导入 prefab 运行。

### 验证、未覆盖与回滚

- 已完成 Skill UTF-8 结构校验、Python 语法检查、测量脚本冒烟、生成 dry-run、生成后规格/引用校验和 meta GUID 唯一性检查。
- `uncertain`：Unity PrefabImporter 成功、Inspector Missing 引用、5 关 PlayMode、连续切关、掉落与门触发，以及 Android/iOS
  横屏视觉和触控均未验证，不能据静态证据宣称关卡已验收。
- 资源回滚只需删除 `ILevel_026`～`ILevel_030` 及对应 meta；工具回滚独立删除
  `../.agents/skills/brainpunk-stitch-iwanner-level-prefab/`。配置与既有模板无需回滚。

## ILevel_023 陷阱动态跳跃控制风险补充（2026-08-12）

### 原始行为、影响范围与直接结论

- `IWannerGame_Trap(1)` 原始实例状态为 Kinematic，绑定的 `IWannerGame_JumpListener` 初始关闭，因此不会响应
  `PlayerJumpEvent`。新增 `ILevel023Ctrl.EnableTrapJump()` 后，只有外部在 PlayMode 明确调用才切换为 Dynamic、唤醒刚体
  并开启监听。
- 新脚本只绑定 `ILevel_023.prefab`，没有改写共享 `IWannerLevelCtrl`、Actor、Trap prefab 或事件定义；当前影响范围局限于
  第 23 小关的指定陷阱实例。
- 脚本没有 `OnValidate`、`Reset`、`ExecuteAlways`、DB、Save、RemoteConfig、Instance 或 Instantiate 路径；公开 API
  在最外层使用 `Application.isPlaying` 保护编辑态。

### 风险项

1. **P1：当前只提供调用入口，没有自动指定触发时机。**
   - 影响：若 prefab 中没有 UnityEvent 或其它本关运行时逻辑调用 `EnableTrapJump()`，陷阱会一直保持原始 Kinematic/关闭状态。
   - 处理：由关卡设计把无参 API 绑定到目标 TriggerExecutor 的 UnityEvent；触发时机未由本次需求指定，因此不静默选择现有 Trigger。
2. **P1：状态切换在当前实例内不可逆。**
   - 影响：调用后 Listener 持续启用，刚体保持 Dynamic；本 API 不负责恢复 Kinematic。当前实例内 Reset 若不重建 prefab，状态可能保留。
   - 处理：PlayMode 核对实际 Reset 是否重建关卡；若设计需要原地恢复，应另补明确的关闭/复位 API，而不是在共享控制器中隐式恢复。
3. **P1：动态刚体与陷阱碰撞可能改变关卡结果。**
   - 影响：目标质量、重力、连续碰撞和约束沿用 prefab 现值；Dynamic 后会参与落体及碰撞，可能与地面、Actor 或其它陷阱产生新接触。
   - 处理：重点验证首次切换瞬间、落地、碰撞失败、离开 FallArea 和门口路径，不在控制器内擅自改写既有物理参数。
4. **P2：Jump Listener 不包含接地门禁。**
   - 影响：启用后每个有效 `PlayerJumpEvent` 都能重写目标 Y 速度，快速连续点击可能产生多段跳效果。
   - 处理：本次保持既有 Listener 契约；若第 23 小关要求只在接地时跳，需要新增该陷阱可验证的局部接地条件。
5. **P2：移动端和生命周期尚未验证。**
   - 影响：Android/iOS 横屏触控节奏、暂停/恢复、失败、Reset 和连续切关可能暴露静态检查不能证明的问题。
   - 处理：补充 PlayMode 单次/连续跳跃、暂停恢复、失败 Reset、退出重进及移动端真机冒烟。

### 验证、未覆盖与回滚

- 已确认脚本/meta GUID、根组件、目标刚体和 Listener 引用完整；Prefab 本地锚点无重复，目标初始 Kinematic 与 Listener
  关闭状态未被静态资源改写。Unity 同版本 Roslyn 编译返回 `CSC_EXIT=0`；Unity Editor 完成编译与程序集重载后再次由
  PrefabImporter 导入目标 prefab，最终导入点之后没有 Missing Script 或 `error CS`。
- `uncertain`：Inspector 人工目检、PlayMode 实际调用、物理碰撞结果、Reset 生命周期和 Android/iOS 真机尚未验证；
  不能仅凭静态及导入证据宣称玩法已验收。
- 回滚只需移除 `ILevel_023.prefab` 根节点上的 `ILevel023Ctrl` 并删除脚本/meta；原陷阱和 Jump Listener 初始配置无需调整。

## IWanner 运行态 OnGUI 调试展示风险补充（2026-08-13）

### 事实与直接结论

- 最终展示范围只包含 IWanner Reset/Failed 插屏策略的资格输入与派生结果，不再显示通关插屏、Path、尝试时长或其它普通运行统计。
- HUD `OnGUI` 在 `UNITY_EDITOR || DEBUG_MODE` 下编译，并以 `Application.isPlaying`、IWanner 面板激活和 GMPanel 已打开作为外层门禁；
  GUI 仍由 `IWannerGame_GamePlayPanel` 绘制，GM 内部 View1/View2 显隐不参与门禁，GMPanel 关闭时按钮和数据区都不绘制。
- DB、RemoteConfig、Bag 和当前 Group 的读取位于 `IWannerPlayCtrl` 正常运行态的低频快照采集；`OnGUI` 只读快照，
  没有 Save、Manager/Instance、Instantiate、`OnValidate` 或 `Reset` 调用。正式非调试构建不包含采集和绘制代码。
- IWanner 已移除自身的 `GameSDKService.IsAdsReady` 前置门禁，失败位调用顺序与 DrawGame 对齐为
  `InterstitialAdHelper -> GameSDKService -> GameAdsManager`；GUI 显示实际 Helper 回调结果，不再把 SDK Ready 包装属性拼入资格结论。
- 普通小关成功和完整 Group 通关不调用 `InGameVictory`，也不累计胜利插屏计数；历史配置和存档字段仅兼容保留。
- 失败位包含手动 `Reset` 与关卡 `Failed` 两个来源；`Failed` 只在结果回调中累计一次并显示重置区，广告请求延后到玩家执行
  重置、关卡进入黑幕与输入锁保护的重建边界，调试文案不得误写成只有一种失败来源或在失败瞬间直接请求。

### 风险项

1. **P1：调试资格公式需要与策略算法保持同步。**
   - 影响：快照中的 Trigger/Ready 复现当前 `InterstitialAdPolicyBase` 的最低 Group、失败、胜利、小关与冷却条件；若未来只改
     策略而漏改调试快照，原始计数仍正确，但派生 Ready 可能误导排查。
   - 处理：修改插屏判定公式时同步更新本快照与本记录；真实广告是否展示仍以策略/SDK 日志和实际回调为权威证据。
2. **P1：Helper 回调 `false` 不能单独区分策略拒绝与 SDK 展示失败。**
   - 影响：GUI 同时给出当前策略阻断项和最近 Helper 结果；若策略项均通过但结果仍为 `false`，还需结合 SDK 日志定位素材或平台失败。
   - 处理：不在玩法层复制 SDK 内部状态机；以原始条件、Helper 结果和 SDK 日志三者共同判断。
3. **P1：PlayMode 热重载异步状态机可能产生无效 IL。**
   - 事实：曾在运行中修改 `WaitForInterstitialAsync` 的回调闭包和条件编译分支，Unity 抛出
     `InvalidProgramException`，异常发生在生成的 `MoveNext()`，广告调用尚未执行。
   - 处理：回调闭包只完成 `UniTaskCompletionSource<bool>`，调试赋值移到 `await` 后；涉及异步状态机结构变更后退出并重新进入
     PlayMode，不能用旧状态机实例作为验证结果。
4. **P2：IMGUI 位置可能与其它调试展示重叠。**
   - 影响：按钮和数据区固定在屏幕右上角，若同时打开其它 IMGUI 调试面板，可能互相遮挡。
   - 处理：先在实际调试分辨率目检；若确认冲突，只调整本地 Rect，不引入 prefab 或共享布局系统。
5. **P2：展开调试区会产生低频采集和 IMGUI 开销。**
   - 影响：展开时每 0.25 秒读取现有广告状态/配置并生成快照，OnGUI 格式化文本；折叠、GMPanel 关闭或正式非调试构建无该采集。
   - 处理：该展示只用于诊断；进行纯性能测试时关闭 DEBUG_MODE/调试展示，不能把调试构建结果直接当作 Release 性能证据。
6. **P2：GMPanel 静态打开状态依赖正常关闭生命周期。**
   - 影响：正常 `OnClose` 会清除状态；若 Editor 非正常中断 UI 生命周期，静态值可能保留到下一次脚本域重载。
   - 处理：HUD 仍要求自身激活且处于 PlayMode；Unity 正常关闭/域重载会清理，异常路径需 PlayMode 验证。

### 验证、缺失证据与回滚

- 三份相关代码通过 `git diff --check`；静态扫描确认 HUD `OnGUI` 不访问 DB/配置/单例。常规 C# 构建通过，结果为
  `0 error / 101 warning`。
- `uncertain`：Unity PlayMode 的 GMPanel 开关、GM 内部 View 显隐、按钮切换、各计数和冷却动态变化、无法触发原因与实际策略结果对照、面板重建、超窄分辨率、其它
  OnGUI 遮挡及 Android/iOS Development Build 尚未人工验证。
- 回滚只需移除 GMPanel 打开状态、控制器调试快照和 HUD 调试区域；不修改 prefab、存档数据、配置或广告运行生命周期。

## IWanner Spring 场景配色风险补充（2026-08-13）

### 事实与直接结论

- `IWannerLevelCtrl.ApplySceneColorSettings()` 只在小关运行时初始化扫描一次子层级 `SpriteRenderer`。
- 新增分支仅把父层级存在 `IWannerGame_SpingTriggerCtrl` 的 Renderer 写为已有 `GroundColor`；没有
  新配色字段或持久化变更。
- `IWannerGame_Spring.prefab` 本体当前只有视觉层级和 Trigger Collider，`ILevel_031.prefab` 通过嵌套
  prefab override 把 `IWannerGame_SpingTriggerCtrl` 加到 Spring 根节点；本次未修改这些用户资源。

### 风险项

1. **P1：新关卡若只放入基础 Spring prefab 而没有绑定控制器，不会命中配色。**
   - 影响：基础 prefab 当前未直接挂 `IWannerGame_SpingTriggerCtrl`，识别与弹跳功能都依赖关卡实例绑定。
   - 处理：后续关卡将 Spring 控制器引用列入 prefab 验证清单；不用宽泛名称扫描把无关美术节点误染色。
2. **P2：Spring 控制器层级下的全部 SpriteRenderer 都会统一改色。**
   - 影响：当前两个 Square 都应与 Ground 同色；若未来增加需保留独立色的高光或提示 Renderer，也会被覆盖。
   - 处理：当前遵循用户的整体 Spring 改色要求；出现独立色需求后再增加明确局部排除，不预先抽象。
3. **P2：配色扫描只发生在初始化。**
   - 影响：小关初始化后动态生成或从 LevelCtrl 层级外移入的 Spring 不会自动应用配色。
   - 处理：当前 Spring 是静态嵌套关卡资源；若后续引入动态生成，应在明确生成边界单次应用颜色，不增加逐帧扫描。
4. **P2：实机颜色与可读性未验证。**
   - 影响：Spring 与 Ground 同色后，在部分 Camera 背景上可能降低机关辨识度。
   - 处理：在 `ILevel_031` PlayMode 目测静止、触发位移和返位三个状态，再补 Android/iOS 横屏对比度冒烟。

### 验证、缺失证据与回滚

- 两份目标代码通过 `git diff --check`；定向构建通过，结果为 `0 error / 101 warning`。
- 静态确认 Spring 基础 prefab 有两个 `SpriteRenderer`，当前 `ILevel_031` 有 Spring 根控制器绑定；
  `uncertain`：Unity PlayMode、Reset/切关、动画期间的色值保持与 Android/iOS 真机尚未人工验证。
- 回滚仅移除 Spring 父层级判断并还原配色说明；不修改 Spring/Trap/Ground prefab、配色资产、存档或资源路径。

## IWanner Actor 死亡关闭主体碰撞风险补充（2026-08-13）

### 事实与直接结论

- 修改前共享 `ActorCtrl.Die()` 只设置死亡、播放动画并停止移动，`Actor_IWanner.prefab` 根节点启用的
  `BoxCollider2D` 在死亡期间仍参与 Physics2D 检测。
- `ActorCtrl.Die()` 本次仅增加虚方法分派能力，方法体不变；`Actor_IWanner.Die()` 先执行共享死亡表现，再关闭同对象的
  根节点 `BoxCollider2D`。`IWannerLevelCtrl` 虽以 `ActorCtrl` 字段持有角色，调用仍会动态进入 IWanner 覆写。
- 默认失活的子节点 `pushCollider` 不在本次关闭范围；失败后的旧小关实例由现有流程销毁重建，不在旧实例恢复碰撞状态。
- 本次不修改 prefab、刚体参数、输入事件、失败结果、存档、配置、资源加载、打点或平台 SDK。

### 风险项

1. **P1：关闭主体碰撞后死亡物理轨迹会改变。**
   - 影响：死亡 Actor 不再与地面、陷阱或其它实体发生主体碰撞，可能继续受刚体重力或既有击退速度影响。
   - 处理：这是需求指定的行为边界；PlayMode 必须同时观察死亡动画、击退、掉落和失败遮罩时序。
2. **P1：若未来改为原地复用 Actor，碰撞器不会自动恢复。**
   - 影响：当前证据表明失败流程销毁并重建小关实例；若生命周期改为对象池或原地 Reset，新回合可能保持关闭。
   - 处理：生命周期变更时在明确的 IWanner 重置入口恢复根碰撞器，不在当前死亡方法中延时自恢复。
3. **P2：只关闭根节点 BoxCollider2D。**
   - 影响：当前主体碰撞器位于根节点，子节点 pushCollider 默认失活；未来若新增其它活动 Collider2D，仍需明确是否属于死亡关闭范围。
   - 处理：保持用户指定的 `boxCollider2D` 最小范围；Prefab 结构变化时重新核对死亡碰撞契约。

### 验证、缺失证据与回滚

- 定向 `git diff --check` 通过；`dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal`
  通过，结果为 `0 error / 101 warning`，warning 为工程现有项。
- 静态确认根节点 `BoxCollider2D` 默认启用，`ActorCtrl` 基类引用能够虚分派到 `Actor_IWanner.Die()`；实现没有新增
  `OnValidate`、`Reset`、DB、Save、资源或全局事件路径。
- `uncertain`：Unity PlayMode 的死亡当帧碰撞关闭、陷阱/失败区、击退/掉落、Reset 重建，以及 Android/iOS 物理表现未验证。
- 回滚删除 `Actor_IWanner.Die()` 覆写，并把 `ActorCtrl.Die()` 恢复为非虚方法；无需修改 prefab、关卡、配置或存档。

## IWanner 小关正式打点风险补充（2026-08-13）

### 事实与直接结论

- IWanner 现在以每个普通小关作为一条独立关卡打点周期，Group 完成不重复发送成功事件；普通成功与奖励 Skip 成功都计为小关成功。
- `IWannerLevelRuntimeStatistics` 仍是小关次数、成功、revive 与时长的唯一写入方，并在存档写入后调用
  `GameSDKService`；失败会保留运行态 revive，成功或退出会清理该上下文。
- `GetCombinedLevelPassedCount()` 已从“主线 + DrawGame”扩展为“主线 + DrawGame + IWannerGame”。这是用户确认的 BI 语义变更，
  会同时影响三种玩法后续的 `level_start.level`、`level_end.level`、`level_end_success_N` 序号和 `b_play`。

### 风险项

1. **P1：旧档 IWanner 历史成功会在升级后立即进入跨玩法累计。**
   - 影响：IWanner 在正式 SDK 接入前已经写入 `LevelSuccessCount`；已有玩家升级后的下一次主线、Draw 或 IWanner 事件，
     其累计 `level` 和 `b_play` 可能相对旧版本发生一次历史数据跃迁，而不是只从升级后的新成功开始增加。
   - 处理：当前实现遵循用户确认的“历史累计成功次数之和”口径，不另建迁移计数或静默丢弃旧成功。BI 验收时需用带旧档账号核对是否接受该跃迁。
2. **P1：存档失败与事件上报之间没有事务。**
   - 影响：现有保存接口失败时只记录 warning；内存字典已经改变，当前会话仍会按更新值上报，若随后进程退出，落盘数据和 SDK 历史可能不一致。
   - 处理：沿用主线/Draw 的现有非阻断边界，避免保存异常卡死玩法；需用保存失败注入验证并由日志对账，不能宣称具备强一致性。
3. **P1：Unity/原生覆盖和 SDK 后台尚未验证事件顺序。**
   - 影响：Skip 激励视频、应用暂停/恢复、结果与退出竞争可能暴露静态分析无法证明的重复或缺失事件；当前只证明控制器调用点收敛和编译通过。
   - 处理：PlayMode 与真机抓取完整事件序列，至少覆盖首次成功、复通、连续 Reset、死亡后 Reset 去重、Skip 成功/取消、退出和 Group Replay。
4. **P1：TBN 没有可用于派生成功事件的实际配置 Id。**
   - 影响：当前产品要求 TBN 不可通关；若错误配置或脚本让其成功，`level_end(success)` 会发送，但 `level_end_success_N` 会被跳过并告警。
   - 处理：保持不可通关边界并验证 TBN 不产生成功结果；未来若 TBN 正式可通关，必须先配置真实小关 Id，再调整派生事件契约。
5. **P2：Path 仍是 IWanner 历史统计与 `level_name` 的稳定键。**
   - 影响：资源 Path 重命名会把同一设计关卡视为新统计对象，导致 `play_count`、`start_type`、revive 与累计时长从新 Key 开始。
   - 处理：发布后避免无迁移重命名；确需重命名时先提供明确的存档 Key 迁移，不在 SDK 层按 Id 拼接替代 Path。
6. **P2：派生成功事件只接受可解析的正整数 Id。**
   - 影响：普通 IWanner 配置若出现空、非数字或非正数 Id，普通成功结束仍会上报，但不会发送 `level_end_success_N`。
   - 处理：配置生成/验收阶段校验 `IWannerLevelData.Id`；运行时继续安全跳过，不能以 GroupId 或数组索引伪造。

### 验证、缺失证据与回滚

- C# 定向构建通过，结果为 `0 error / 101 warning`；`git diff --check` 对代码与权威约定未发现空白错误，只有工作区行尾提示。
- 静态确认正常成功与 Skip 只经同一结算入口，Group 完成没有第二次成功调用；失败/退出只有活动 attempt 才能发送结束事件。
- `uncertain`：Unity PlayMode、Firebase/Guru 实际后台、旧档跃迁、存档失败注入、真实激励覆盖层、Android/iOS 暂停恢复和跨玩法事件连续性均未验证。
- 回滚运行时 API、调用点与三玩法累计公式即可恢复到仅保存 IWanner 本地历史统计的状态；回滚累计公式时必须同时还原
  `DevelopMD/关卡打点生命周期约定.md`，已有 `DB.IWannerGame` 字典无需删除。

## IWanner Group 8 静态地图拼接风险补充（2026-08-14）

### 事实与直接结论

- Group 8 已新增 `30036`～`30040` 映射，五个 prefab 以 `ILevel_035` 为只读模板，只替换参考图、Actor/Door 坐标和 Ground。
- 五关静态结构校验通过，总 Ground 数为 `8/9/15/4/7`；参考图节点失活，未发现重复 fileID 或缺失本地引用。
- 旧 `ILevel_036` 是未跟踪资源，用户明确授权覆盖；其中的 Trigger、Trap、Spring 不在新静态规格中。

### 风险项

1. **P1：机关和尖刺缺失可能使五关无法形成完整通关路径。**
   - 影响：参考图中的方框、尖刺、弹簧或传送表现没有生成对应运行对象，复杂封闭结构不能仅靠 Ground 完成。
   - 处理：当前只作为静态骨架；对玩家开放 Group 8 前逐关补机制并验证完整流程。
2. **P1：旧 `ILevel_036` 无 Git 回滚来源。**
   - 影响：覆盖前的未跟踪机制对象无法通过提交历史恢复。
   - 处理：若需恢复只能使用 Unity 本地历史、外部备份或人工重建；本次保留原 `.meta` GUID 以避免资源引用漂移。
3. **P1：`ILevel_038` 的迷宫色块由矩形拆分。**
   - 影响：窄通道、重叠矩形和一像素边界误差可能造成卡角、穿透或不可达。
   - 处理：必须在 Scene 叠加参考图核对，并以 Actor 实际 Collider 完成每条通道的 PlayMode 验证。
4. **P1：运行时映射已生效但玩法尚未验收。**
   - 影响：若选关入口已经允许 Group 8，玩家可能进入未补机关的静态关卡。
   - 处理：发布前核对入口/解锁状态，不以配置可加载代替玩法完成。
5. **P2：Unity 静态导入通过，但资源打包和移动端证据不完整。**
   - 影响：五个 prefab 与两个 JSON 已成功导入，但 YooAsset 收集、Android/iOS 横屏和运行时物理仍未验证。
   - 处理：保持运行与平台部分为 `uncertain`，补齐 PlayMode、资源收集和真机冒烟证据。

### 验证、缺失证据与回滚

- Luban 表格检查/渲染与公式错误扫描通过，CLI 临时生成 JSON 与仓库运行时 JSON 一致；五关生成器验证和 Unity 导入均通过。
- `uncertain`：Scene 叠图、出生落地、跳跃可达、Door、失败/Reset、机关、连续切关、YooAsset、Android/iOS 均未验证。
- 回滚需同时撤销源表、运行时 JSON、Group 8 规格和新增 prefab；旧 `ILevel_036` 无 Git 恢复路径。详见
  `Assets/Game/Prefabs/WannerLevel/ILevel036-040地图拼接风险评估.md`。

## Actor_IWanner distortion API 风险补充（2026-08-17）

### 事实与直接结论

- 当前 `Qboxy.json` 可解析到 `distortion` 动画；本次没有修改 Spine 资源、`Actor_IWanner.prefab`、关卡 prefab 或调用方。
- 原 `HandleAnimators()` 每次 Actor 更新都会按接地和输入切换 `idle/walk/jump`，直接播放一次性动作会被自动状态动画覆盖。
- 新增 `Actor_IWanner.PlayDistortion()` 只在运行时、未死亡且 Spine 有效时从头非循环播放 `distortion`；本地轨道跟踪存在期间，
  `HandleAnimators()` 不切换移动状态动画。`Complete` 或 `Interrupt` 会解除跟踪，死亡入口也会先清理跟踪。
- 动作锁只影响动画切换，不停止输入、移动、刚体、碰撞、关卡状态或移动 Ground 速度合成；未调用 API 时行为与修改前一致。

### 风险项

1. **P1：调用方可能把“动作播放”误解为角色控制锁。**
   - 影响：角色播放 distortion 时仍可移动、起跳或随平台移动；若产品预期原地受击/眩晕，会出现动画与位移同时发生。
   - 处理：本次严格按“补充播放 API”保持物理与输入不变；需要控制锁时必须另行明确生命周期、死亡/Reset 与恢复边界。
2. **P1：一次性动作若不阻止自动动画，会在下一帧被覆盖。**
   - 影响：API 表面调用成功但玩家几乎看不到 distortion。
   - 处理：使用局部 `TrackEntry` 门禁，并同时监听完成和中断；不修改共享 `ActorCtrl`，避免影响非 IWanner 角色。
3. **P2：重复触发或外部动画打断可能留下过期回调。**
   - 影响：旧轨道回调若作用于新轨道，会提前恢复状态动画；中断后若不清理，则可能永久阻止移动动画。
   - 处理：每次重播先解绑旧轨道，回调只接受当前轨道；完成、中断和死亡均走同一清理方法。
4. **P2：当前没有调用点和运行时视觉证据。**
   - 影响：只能证明 API 编译、动画名存在和静态控制边界，不能证明具体关卡中的时序、混合与手感。
   - 处理：接入方需在目标关卡补 PlayMode 验证，覆盖 idle/walk/jump、连续触发、死亡打断、Reset 和 Android/iOS 横屏。

### 验证、缺失证据与回滚

- JSON 解析确认 Qboxy 当前动画集合包含 `distortion`；定向 C# 构建通过，结果为 `0 error / 0 warning`。
- 静态扫描未发现新增 Editor 生命周期、DB/Save、资源加载、实例创建、全局事件、协程或逐帧分配路径；
  代码定向 `git diff --check` 通过，仅有工作区行尾转换提示。
- `uncertain`：Unity PlayMode 的完整播放、状态恢复、移动/空中触发、连续重播、外部打断、死亡/Reset 与 Android/iOS 表现未验证。
- 回滚只需移除 `Actor_IWanner` 内的 distortion 常量、公开 API、轨道跟踪/回调和动画切换门禁；无需修改 Qboxy 资源、
  prefab、配置、存档、事件或平台代码。

## Portal 双段 distortion 与物理恢复门禁风险补充（2026-08-18）

### 事实与直接结论

- `IWannerGame_Portal` 原流程已经在入口停止 Actor 移动、清零速度并关闭 `Rigidbody2D.simulated`，但只等待出口放大
  Tween；本增量没有修改触发 Tag、落点、延迟终点、速度清零或取消不恢复的既有边界。
- 当 `target is Actor_IWanner` 时，入口缩放前触发第一次 distortion；设置到 `endPoint.position` 后触发第二次，
  第二次与出口放大并行执行。
- Portal 只有同时取得“出口缩放完成”和“第二次 TrackEntry `Complete`”才恢复 `Rigidbody2D.simulated` 与移动。
  `Interrupt`、取消、死亡或销毁均不会提前恢复；非 `Actor_IWanner` 目标保持原缩放等待流程。
- `Actor_IWanner` 只新增程序集内 TrackEntry 返回入口，没有把 Portal 状态、物理锁或关卡状态加入共享 `ActorCtrl`。

### 风险项

1. **P1：出口 distortion 被打断后 Actor 会继续保持移动与物理锁定。**
   - 影响：若关卡在不 Reset/重建 Actor 的情况下由其它系统抢占 Spine 0 号轨道，玩家可能停留在出口且无法继续操作。
   - 处理：Portal 记录中断警告，不把中断误判为播放完成；目标关卡需要验证死亡、Reset、Replay 和外部动画抢占链。
2. **P1：动画与缩放时长不一致会改变出口等待节奏。**
   - 影响：恢复时间取两者较长值；distortion 较长时 Actor 已放大完成但仍保持静止，较短时继续等待 OutBack 缩放。
   - 处理：这是“两个出口表现均完成才恢复”的直接结果，需要在 PlayMode 按实际 Qboxy 动画时长确认手感。
3. **P2：Spine 或动画轨道缺失时无法满足完整表现。**
   - 影响：若严格永久等待会造成软锁。
   - 处理：角色未死亡时记录 Bear Logger 警告并降级为只等待缩放；死亡状态不走该恢复降级。
4. **P2：入口 distortion 可能在到达出口前尚未结束。**
   - 影响：第二次播放会按现有重复调用语义从头重播并替换旧跟踪。
   - 处理：符合用户要求的两个离散触发点；需验证 `scaleDuration + teleportDelay` 与动画长度的视觉衔接。

### 验证、缺失证据与回滚

- 定向 `Assembly-CSharp` 构建通过：`0 error / 109 warning`；warning 为工程既有项，目标脚本无新增编译诊断。
- 静态确认没有新增 `Update`、Editor 生命周期、DB/Save、资源加载、全局事件、实例化或平台分支；等待回调在完成、
  中断或取消后解绑，并链接 Portal 与 Actor 销毁取消令牌。
- `uncertain`：Unity PlayMode 的双段动画可见性、移动/Rigidbody2D 恢复帧、暂停、死亡、Reset、Replay、外部动画打断、
  连续传送与 Android/iOS 横屏表现尚未验证。
- 回滚时删除 Portal 的两次 distortion 调用、TrackEntry 等待助手和双任务门禁，并删除
  `Actor_IWanner.PlayDistortionTracked()`；恢复原来只等待出口缩放的流程即可。

## Portal distortion 可见性与反向播放风险补充（2026-08-18）

### 事实

- 修改前入口 distortion 与 Scale 归零 Tween 并行，出口 distortion 为正向；本次没有修改触发 Tag、落点、传送延迟、
  出口 OutBack 缩放、物理锁、移动恢复门禁或延迟切换终点。
- 修改后 IWanner 入口先完整播放正向 distortion，再瞬间把 Scale 设为 0；出口使用 `TrackEntry.Reverse=true`，并继续与
  原放大 Tween 并行等待。非 `Actor_IWanner` 目标行为不变。

### 直接结论

- 入口不再因缩小 Tween 提前失去可见尺寸；传送开始到搬运的总时长会由“缩放时长 + 延迟”改为
  “distortion 完整时长 + 延迟”，这是本次明确的玩家可见节奏变化。
- Spine 当前运行库明确要求反向播放使用 `TrackEntry.Reverse`，负 `TimeScale` 不受支持；反向 TrackEntry 仍按正向
  TrackTime 到达完成点，因此现有 `Complete/Interrupt` 等待边界可以复用。

### 风险与假设

1. **P1：入口 distortion 中断会阻止传送继续。** 这是沿用“动作未完整完成不恢复/不推进”的门禁；若其它系统抢占 0 号轨道，
   Actor 会保持可见但移动与物理仍锁定，需要 Reset/重载恢复。
2. **P2：出口反向 distortion 与从 0 放大的首帧可能仍不清晰。** 静态代码只能确认方向和并行时序，实际可见程度取决于
   Qboxy 动画关键帧与 `scaleDuration`；需要 PlayMode 观察后再决定是否移除出口 DOScale。
3. **假设**：用户只要求移除“开始的 DOScale”，因此本次保留出口 OutBack 放大；若产品目标是完全由 distortion 控制出现，
   还需另行确认并移除出口缩放。

### 缺失证据、验证与回滚

- 原始生成工程因缺失 `ILevel041Ctrl.cs` 引用无法直接构建；临时过滤缺失 Compile 条目后完整构建为
  `0 error / 109 warning`，临时文件已删除。
- `uncertain`：未执行 Unity PlayMode、Reset/Replay、暂停/死亡、连续 Portal、Android/iOS 横屏真机验证。
- 回滚时恢复 IWanner 入口缩小路径、移除出口反向参数与 Actor 的 `TrackEntry.Reverse` 赋值；非 IWanner、prefab、资源、
  配置、存档与平台代码无需回滚。

## Portal 出口 distortion 提前播放风险补充（2026-08-18）

### 事实与直接结论

- 当前 Qboxy distortion 总时长约 `1.1667s`；反向播放前约 `0.6s` 的相关 Slot 透明关键帧解释了“Scale 先出现”的观察。
- IWanner 出口现在先启动反向 distortion，默认延迟 `0.6s` 再开始 `0.25s` 的 OutBack 放大；非 IWanner Portal 不变。
- 恢复门禁仍等待完整 distortion 和延迟后的 Scale，不改变取消、死亡、中断、物理或移动恢复条件。

### 风险、缺失证据与回滚

1. **P2：动画资源变更可能让 0.6 秒提前量失配。** 已把提前量保留为 Portal 本地序列化字段，资源或节奏调整后可在
   Inspector 局部修改，不需要改共享 Actor 或全局配置。
2. **P2：提前量会延长 Scale 的启动时间，但不会额外延长超过 distortion 总时长的恢复时间。** 当前默认 Scale 在约
   `0.85s` 完成，仍早于约 `1.1667s` 的 distortion 完成点；实际帧时序需 PlayMode 确认。
- 构建验证为 `0 error / 109 warning`；Unity PlayMode、暂停/Reset/Replay、连续传送及 Android/iOS 横屏仍为 `uncertain`。
- 回滚只需删除提前量字段和延迟缩放助手，恢复出口同帧启动；其它 Portal/Actor 行为、prefab 与资源无需回滚。

## Portal 出口取消反向播放风险补充（2026-08-18）

### 事实与直接结论

- 用户已明确要求出口 distortion 不反向播放；Portal 入口、出口均恢复为正向 TrackEntry。
- Actor 的内部方向参数与 `TrackEntry.Reverse` 已删除，避免当前没有反向需求时保留无调用能力。
- 出口提前 `0.6s` 再播放 Scale 的时序继续保留，移动与物理仍等待 distortion、Scale 两者完成。

### 风险、缺失证据与回滚

1. **P2：0.6 秒提前量最初用于解决 Scale 先于 distortion 可见的问题。** 改为正向后可见关键帧分布不同，默认值是否仍合适
   只能由 PlayMode 视觉验证确认；字段保留为局部可调，避免修改共享资源。
2. **P2：历史记录包含曾经采用反向播放的阶段。** 本节为当前有效结论，前述反向方案仅保留为变更历史，不应作为现行行为。
- 构建为 `0 error / 109 warning`；Unity PlayMode、Reset/Replay、暂停/死亡及 Android/iOS 横屏仍为 `uncertain`。
- 回滚本次纠正时恢复方向参数与出口反向标记；提前量、prefab、资源、配置、存档和平台链路无需回滚。

## Portal 接入 WorldCoinAttraction 风险补充（2026-08-18）

### 事实

- `IWannerGame_Portal` 新增可选的 `WorldSpriteParticleAttractor` 引用与默认 `0.3s` 提前量；触发点位于 IWanner 入口
  distortion 完成前，Actor Scale 写为 0 之前。
- 触发时先把特效根节点世界坐标设为 Actor 世界坐标，再调用 `Play(endPoint)`；目标来自当前 Portal 的序列化
  `endPoint`，不新增场景查找、实例化或全局事件。
- ILevel_036 的三个 Portal 共用一个关卡根节点特效，该实例局部覆写 `playOnEnable=0`；共享 prefab 与未绑定的其它关卡
  不受新表现影响。

### 直接结论

- 粒子在入口 distortion 已播放、距离完成默认还剩 `0.3s` 时出现；发射原点和吸附目标分别对应当时 Actor 与当前 Portal endPoint。
- 粒子播放是非阻塞的局部表现，不改变 Portal 的 Scale 归零、传送延迟、落点、出口 distortion/Scale、物理锁和移动恢复门禁。
- 特效失效只记录 Bear Logger 警告并继续传送，避免可选 VFX 引用把玩法流程变成新的软锁点。

### 风险、假设与缺失证据

1. **P2：共用实例的重复播放会覆盖上一轮。** `WorldSpriteParticleAttractor.Play()` 明确先清空上一轮；若三个 Portal 发生
   重叠触发，只保留最后一次 Actor 位置与 endPoint 目标。当前单 Actor 关卡下接受该局部复用，未来并发目标需拆分实例。
2. **P2：粒子 Z、摄像机与 endPoint 距离会影响实际观感。** 脚本默认保持粒子原 Z，静态绑定无法证明它与 Actor、Portal
   渲染排序完全匹配，需要 Scene/Game 视图目测。
3. **假设**：用户所说的“position 切换到 actor 位置”指特效根节点的世界坐标，而不是把特效重设父节点或修改单颗粒子坐标。
   当前实现与 World Simulation Space 及组件的发射原点计算一致。
4. `uncertain`：Unity PlayMode 的入口完成帧、粒子可见性、三个 endPoint 吸附轨迹、连续触发、Reset/Replay、暂停/死亡及
   Android/iOS 横屏表现均未验证。

### 验证与回滚

- 完整 `Assembly-CSharp` 构建为 `0 error / 109 warning`；warning 为工程既有项。
- 静态核对三个 Portal override、WorldCoinAttraction stripped 组件、prefab/script GUID 与 ILevel_036 的
  `playOnEnable=0` 局部 override；定向
  `git diff --check` 通过（仅有行尾格式提示）。
- 回滚时移除 Portal 可选引用与触发助手，删除 ILevel_036 三个引用 override；不需要回滚 Portal 的 distortion 正向播放、
  提前量、物理/移动门禁或 WorldSpriteParticleAttractor 通用 API。

## Portal 出口再次启用反向 distortion 风险补充（2026-08-18）

### 事实与直接结论

- 当前有效方向为：入口 `reverse=false`，出口 `reverse=true`；公开 `Actor_IWanner.PlayDistortion()` 仍使用默认正向。
- 方向通过 Spine `TrackEntry.Reverse` 设置，轨道仍按既有 `Complete/Interrupt` 门禁等待；出口依旧提前启动 distortion，
  延迟 `0.6s` 后才开始 Scale。
- WorldCoinAttraction 的触发点位于入口正向 distortion 完成前 `0.3s`，因此不受出口方向切换影响；传送、物理和移动恢复条件不变。

### 风险、缺失证据与回滚

1. **P2：反向出口的透明关键帧与 Scale 可见时点需人工确认。** 静态代码只能证明调用方向及 0.6 秒先后关系，不能证明
   最终 Game View 观感；若仍出现 Scale 先露出，应只调整 Portal 局部 `exitDistortionLeadTime`，不改共享动画资源。
2. **P2：历史章节包含正向、反向两种阶段。** 本节与 Portal 功能记录的最新章节是当前有效结论，旧章节仅作为变更历史。
3. `uncertain`：Unity PlayMode、Reset/Replay、暂停/死亡、连续传送以及 Android/iOS 横屏均未验证。
- 完整程序集构建为 `0 error / 109 warning`；定向静态扫描确认入口/出口布尔值与 `TrackEntry.Reverse` 赋值成立。
- 回滚时删除方向参数与 Reverse 赋值，恢复入口、出口正向调用；WorldCoinAttraction、prefab、资源、配置、存档与平台链路不变。

## WorldCoinAttraction 提前 0.3 秒风险补充（2026-08-18）

### 事实与直接结论

- Portal 使用 TrackEntry 的剩余 TrackTime 和本轨 `TimeScale` 计算完成前 `0.3s` 的游戏时间延迟；该字段是共享 Portal
  prefab 的局部序列化参数，但只有绑定 WorldCoinAttraction 的实例产生可见结果。
- 尚未到提前点时发生中断、取消或 Actor 销毁，会取消回调；正常完成或轨道启动降级路径使用一次性完成兜底，避免漏播/重复播。
- 粒子提前不改变入口必须完整 `Complete` 才 Scale 归零和继续传送的门禁，也不改变出口反向 distortion。

### 风险、缺失证据与回滚

1. **P2：最后 0.3 秒内发生中断时，已发出的粒子不会自动回收。** 玩法仍按原中断门禁停止，但纯表现可能继续吸附；共用实例下
   不在 Portal 失败分支强制 Stop，避免清理另一次较新的播放。
2. **P2：额外动画时间缩放可能让实际提前量偏离 0.3 秒。** 当前换算覆盖 TrackEntry `TimeScale`，没有本地证据证明关卡会改
   AnimationState/Skeleton 级 TimeScale；若后续引入，应改为逐帧轨道时间门禁或提供统一时间换算。
3. `uncertain`：Unity PlayMode 的实际发射帧、暂停/变速、Reset/Replay、连续 Portal 和 Android/iOS 横屏尚未验证。
- 完整程序集构建为 `0 error / 109 warning`；静态确认 prefab 默认值、取消链、一次性门禁、Scale/WorldCoin 顺序成立。
- 回滚时删除提前量字段和预完成调度，把特效触发恢复到入口 Complete 后；ILevel_036 引用、出口方向和其它 Portal 流程不变。

## Portal 出口改用 distortion_reverse 风险补充（2026-08-18）

### 事实

- 当前 Qboxy 骨骼资源同时包含 `distortion` 与 `distortion_reverse`；本次没有修改该资源。
- Portal 入口选择 `distortion`，Actor 到达 `endPoint` 后的出口选择 `distortion_reverse`；两个动画都按正向
  TrackTime 播放，目标脚本不再设置 `TrackEntry.Reverse`。
- 出口仍沿用实例现有 `exitDistortionLeadTime`、OutBack Scale、`Complete/Interrupt` 等待、物理锁和移动恢复门禁。

### 直接结论

- 出现表现现在由资源中专门的 `distortion_reverse` 动画负责，不再依赖运行时倒放 `distortion`。
- 本次只替换出口动画选择；入口隐藏、落点、传送等待、WorldCoinAttraction、取消与中断边界均未改变。

### 风险、缺失证据与回滚

1. **P2：出口提前量可能需要重新调节。** 现有数值曾与倒放 `distortion` 的透明关键帧配合，不能仅凭动画名证明
   `distortion_reverse` 与 Scale 的最佳重叠点相同；应在 Unity Game View 目测后再按 Portal 实例局部调整。
2. **P2：历史章节保留了正向与倒放阶段。** 本节、Portal 功能记录和 Actor 功能说明的最新章节是当前有效结论；
   旧章节仅作为可追溯变更历史。
3. `uncertain`：Unity PlayMode、Reset/Replay、暂停/死亡、连续 Portal 及 Android/iOS 横屏尚未验证。
- 完整 `Assembly-CSharp` 构建为 `0 error / 109 warning`；静态确认两个动画名存在，入口/出口选择值成立，
  目标代码无 `TrackEntry.Reverse` 赋值。
- 回滚时恢复固定加载 `distortion` 并由出口设置 `TrackEntry.Reverse=true`；prefab、Qboxy 资源、配置、存档、
  WorldCoinAttraction 与平台链路无需回滚。

## IWanner HUD 左右移动 Enter/Exit 风险补充（2026-08-19）

### 事实与直接结论

- 修改前脚本同时绑定 Down/Up 与 Enter/Exit，但左右按钮 prefab 的 Enter/Exit 门禁为 false，运行时有效移动入口是 Down/Up。
- 当前左右控制只绑定 `CustomButton.OnClickEnter/OnClickExit`，两个目标按钮的门禁均改为 true；逐帧移动事件、
  `IWannerLevelCtrl` 映射、Actor、暂停与输入关闭清理未修改。
- Enter 新方向会清除旧方向，Exit 只有在自己仍是当前方向时才生效；因此左右区域切换不会因旧区域的延后 Exit
  插入一次 `PlayerMoveCancelEvent`。

### 风险项

1. **P1：触点结束必须可靠产生 Exit。** 静态代码只能证明 Exit 回调后的清理，不能证明所有目标设备输入模块在触点抬起、
   系统手势抢占或原生覆盖中都按相同顺序派发；需在 Android/iOS 横屏验证按住、滑出和原地抬起均停止移动。
2. **P2：桌面鼠标使用悬停语义。** Enter/Exit 不再依赖按键状态，Editor 中鼠标移入移动区即可开始移动；这是用户指定
   事件模型的直接结果，若桌面操作也要求按住，应另行引入平台/指针类型门禁，不能静默恢复 Down/Up。
3. **P2：按钮间隙或遮挡会触发取消。** 指针经过非射线命中区域时旧按钮先 Exit、下一按钮尚未 Enter，角色会正常停止；
   需要结合最终 HUD 尺寸验证左右区域切换路径是否存在明显空档或被 `clickBlock` 之外的节点遮挡。
4. **P2：完整编译与运行验证未闭环。** `Assembly-CSharp` 构建被未修改的 `IWannerPlayCtrl.cs:553`
   `ResourceManager` 歧义阻断，Unity PlayMode 与设备触控仍为 `uncertain`。

### 回滚路径

- 恢复面板左右 Down/Up 订阅、原方向状态处理及 prefab 两个 Enter/Exit 门禁 false；不回滚共享事件、LevelCtrl、Actor、
  配置、存档、资源加载、打点或 SDK。

## 系统返回键补齐 Exit App 风险补充（2026-08-19）

### 事实与直接结论

- 修改前 IWanner 返回键直接调用 `OpenPausePopup()`；修改后参考 DrawGame，通过控制器本地
  `ShowExitPopup()` 打开默认 `CommmonPopup`，暂停按钮逻辑未修改。
- 退出弹窗文案、本体、Popup 层级和确认/取消行为复用现有主线与 DrawGame 资源；没有新增全局事件、DB/Save、配置、
  资源加载、打点、广告或平台 SDK 调用。
- `CanShowExitPopup()` 拒绝重复默认退出弹窗、活动/排队 Guide Flow、关卡加载、失败等待、Skip 广告请求和 Group 结算。
  因此不会在这些已知独占状态上插入新的 Popup。
- 返回键检查继续位于控制器既有逐帧 `Tick()`，只增加一次离散 `GetKeyDown` 与常量级状态读取，没有新增 MonoBehaviour
  `Update()`、遍历、持续实例化或 GC 热点。

### 风险与缺失证据

1. **P2：其它 Popup 的通用返回键优先级仍未统一。** 当前门禁与 DrawGame 一致，只显式处理默认退出弹窗和 Guide Flow；
   若 IWanner 存在其它非结算 Popup，返回键可能在其上方打开 Exit App。是否建立全局返回键路由不属于本次最小修复。
2. **P2：应用退出不等于玩法正常退出。** 确认按钮直接调用 `Application.Quit()`，不会主动执行
   `ExitToLevelSelection()` 或 `Deactivate()`；本次保持主线/Draw 既有 Exit App 语义，不把它改成关卡退出打点入口。
3. **P2：原生覆盖层边界仅静态隔离。** `_isSkipRewardAdRequesting` 会阻止已进入广告请求阶段的弹窗创建，但真实广告期间
   Unity PlayerLoop 与 Back 键行为仍需 Android 真机验证。
4. `uncertain`：Unity PlayMode 中各门禁状态、重复按键、取消后继续操作，以及 Android/iOS 横屏行为尚未验证。

### 验证与回滚

- 首次编译发现缺少本地化引用，整包 `using I2.Loc` 又与既有 `Bear.ResourceSystem.ResourceManager` 冲突；最终使用两处
  `I2.Loc.LocalizationManager` 全限定名保持资源加载引用不变。
- 最终完整 `Assembly-CSharp` 构建通过：`0 error / 106 warning`。静态扫描确认唯一 IWanner Escape 入口和门禁调用链成立。
- 回滚只需恢复 `Tick()` 对 `OpenPausePopup()` 的调用并删除 `ShowExitPopup()` / `CanShowExitPopup()`；UI prefab、
  本地化资源、配置、存档、打点和平台代码无需回滚。

## Portal 抵达终点直接 Scale 1 风险补充（2026-08-19）

### 事实

- 修改前所有 Portal 目标抵达 `endPoint` 后都通过 OutBack DOScale 恢复可见；IWanner 还使用序列化提前量延迟该 Tween。
- 修改后目标抵达时直接写入 `localScale=Vector3.one`，不再创建出口 Scale Tween；IWanner 仍建立并等待
  `distortion_reverse`，普通 Actor 则直接完成出口表现。
- 入口隐藏、传送等待、落点、WorldCoinAttraction、动画中断门禁、物理锁、移动恢复和预约终点切换均未改变。

### 直接结论

- Portal 出口节奏不再受 `scaleDuration` 或 `exitDistortionLeadTime` 影响；后者及其 Group 8 prefab override 已删除。
- `scaleDuration` 只剩普通 Actor 入口缩小用途；IWanner 入口继续由 `distortion` 完整播放控制隐藏时点。

### 风险、缺失证据与回滚

1. **P2：固定 Scale 1 不保留入口尺寸或镜像。** 若目标根节点在触发前不是 `(1,1,1)`，出口会按本次明确需求统一归一化；
   当前没有 PlayMode 证据证明所有 Portal 目标始终为单位缩放。
2. **P2：抵达帧可能暴露一帧动画初始姿态。** 代码先建立 `distortion_reverse` TrackEntry，再同步恢复 Scale，
   但实际 Skeleton 更新帧序仍需 Game View 观察。
3. `uncertain`：Unity PlayMode、连续传送、Reset/Replay、暂停/死亡、普通 Actor 分支及 Android/iOS 横屏未验证。
- 完整 C# 构建为 `0 error / 106 warning`；静态确认出口缩放助手、提前量字段和相关 prefab 属性路径已移除，
  定向 `git diff --check` 通过。
- 回滚时恢复触发前尺寸缓存、出口缩放等待与提前量字段/override；WorldCoinAttraction、动画资源、配置、存档与平台链路不变。

## IWanner 首个 Group 默认解锁风险补充（2026-08-21）

### 事实与直接结论

- 修改入口为 `Assets/Game/Configs/tbiwannerlevelsort.json` 的 Group 1：`lockType` 从
  `1 (LevelLockType.Ad)` 改为 `0 (LevelLockType.Unlock)`。
- UI Item 和 `IWannerPlayCtrl` 已分别在显示层与资源加载前权威入口读取同一个 `LockType.Unlock` 规则；因此本次
  无需改 UI、控制器或 `UnlockGroups` 存档。
- 影响仅为首 Group 从广告锁定改为默认可进入；Group 2～11、Boss/Passed/Current 判定、广告成功写入及组内进度不变。

### 风险项

1. **P1：Luban 再生成可能回退默认解锁。** 本次表格技能因依赖目录缺少 `@oai/artifact-tool` 无法安全编辑/渲染
   Excel 源表，当前只修改工程实际加载的生成 JSON。再次运行 Luban 前必须先把源表首 Group 的锁类型同步为 `Unlock`。
2. **P2：清档与旧档显示仍需 PlayMode 验证。** 配置放行不依赖 `UnlockGroups`，理论上两类存档都会生效，但当前只有
   静态调用链证据，尚未观察实际 Item 状态、按钮绑定和进关加载结果。
3. **P2：移动端无新增平台分支但仍需冒烟。** 变更不增加计算、分配、资源或 SDK 调用；Android/iOS 横屏下的首项点击
   与后续广告锁点击仍未验证。

### 验证、缺失证据与回滚

- 已完成 JSON 唯一性、首项/后续锁类型、Group Path 到 LevelData 的完整映射检查，并复核 UI/入口两层放行条件。
- 外部 C# 构建被当前 Unity `Temp/bin/Debug` 缺少 200 个依赖 DLL 阻断；本次未改 C#，但完整工程编译仍记为未验证。
- 待补 Unity PlayMode：清档打开、Group 1 普通态、Group 1 进入、Group 2 锁定与广告入口、退出重进。
- 回滚只需恢复首项 `lockType=1`；本次没有新增或迁移存档，已有 `UnlockGroups` 不受影响。

## 主界面入口 Group 进度展示风险补充（2026-08-25）

### 事实与直接结论

- `ui_zhuye_05_txt` 绑定 `passedGroupCountText`，只统计当前 `LevelGroups` 中已存在于 `PassedLevels` 的唯一 Group Id；
  `ui_zhuye_06_txt` 绑定 `totalGroupCountText`，展示当前唯一有效 Group 总数。
- 刷新发生在入口启用和 `StartPanel.OnOpen()`，没有新增逐帧轮询、全局事件、配置初始化或存档写入。
- 当前配置静态结果为 14 个 Group；存档中的旧 Group、重复 Group 或空集合只会被忽略，不会导致显示超过总数。

### 风险、缺失证据与回滚

1. **P2：初始化异常会显示 0/0。** 正常 Loading 顺序应先初始化 `IWannerGameLevelCtrl`，但若首页在半初始化状态打开，
   本次选择安全降级并告警，不自行拉起配置链；需在实际 Loading 流程确认不会短暂闪现。
2. **P2：文本宽度与多语言布局尚未验证。** 两个字段只写数字和 `/`，但 Group 数继续增长后仍需确认 prefab 现有自适应字号与横屏布局。
3. `uncertain`：PlayMode 的清档、部分/全部通过、IsOpen 关闭后重新开启、返回首页，以及 Android/iOS 横屏均未实测。
- C# 外部构建通过，prefab fileID 绑定与当前 JSON Group 数静态检查通过。
- 回滚时删除两个序列化字段和本地统计助手，恢复 prefab 原占位文本控制；不回滚 Group 配置、存档、入口按钮或选组流程。

## 失败空格重置风险补充（2026-08-31）

### 事实与直接结论

- 原基线仅支持失败全屏点击区重置，常规 HUD 输入在失败时关闭。本次仅修改 IWanner HUD 的 `Update()`，增加运行态保护及失败区可见时的空格按下检测。
- 空格与鼠标复用同一失败回调、既有 `IWannerGameResetEvent` 和 `TryResetCurrentLevel()`；控制器同步隐藏失败区、置 `_isOpeningLevel`，已有失败等待标记负责避免重复计数。
- 未修改玩法控制器、关卡资源、广告请求时序、打点、配置、存档或主线/Draw 输入。

### 风险、假设与缺失证据

- **P2：输入时序未实跑。** `GetKeyDown` 不因持续按住而重复，但空格与鼠标同帧、按键与死亡同帧的实际表现仍需 PlayMode 验证，不能用静态去重检查代替运行结果。
- **P2：编译验证被依赖阻断。** 定向外部构建返回 91 个依赖元数据缺失错误（`CS0006`），未获得本次完整编译通过证据。
- `uncertain`：失败后单次重建、失败次数、当前小关保持、正常状态不误重置、黑幕/原生广告覆盖期间输入，以及 Android/iOS 横屏原触控操作均未实测；验收清单见同目录功能记录的本日期章节。
- 环境证据限制：规则引用的 `037-remote-resource-loading/plan.md` 缺失，CodeGraph 服务未识别本项目索引，本次使用当前文件直接取证。
- 回滚：移除 HUD `Update()` 本次新增分支即可；保留既有鼠标重置与全部玩法/存档流程，无待确认的产品需求。

## 2026-09-07：动态切换成功门风险与验证

- facts：新增两个设置 API；触发器切换会迁移当前门的运行时与持久化 Success 监听，动画切换同时影响角色门前定位。未调用 API 时原运行路径不变。
- direct conclusions：只改动画或只改触发器会留下引用不配对；第 47 关应在同一侧 callback 中连续配置同一扇门的两个组件，再调用 Executor。
- hypotheses：Prefab 中其它未曾指定为当前门的触发器若持久化调用 Success，仍可能提交成功；API 只迁移旧门到新门，不遍历其它门。不应为非选中门保留独立通关链。
- missing evidence：本次未改 Prefab 接线；左右选择、旧门失效、新门通关、动画和角色定位一致性、连续切门与重开均未执行 PlayMode，标记 uncertain；移动端未验证。
- 验证证据：Assembly-CSharp 构建通过，0 错误、121 警告。回归需覆盖持久化门和纯运行时监听门，以及未调用 API 的普通单门关卡。
- 回滚：撤回两个 API、私有持久化状态辅助方法及接线；无存档迁移。此次不改变成功流程时序。

## 2026-10-09：OneLink Group／小关 sub2 分离风险补充

### 事实与直接结论

- `IGroup_{id}` 只按 `IWannerLevelSort.Id` 解析，进入时不传强制索引；因此同组沿用有效存档索引，换组从第 0 小关开始。
- `ILevel_XXX` 只按大小写精确的当前配置 Path 匹配，并传真实组内索引；裸数字、配置 level ID、错误大小写及未命中目标继续走原回退。
- 变更仅影响 OneLink/AppsFlyer/Facebook/GM 共用请求的 IWanner sub2 解释；普通选组、TBN、关卡成功/失败、存档结构、资源和打点实现未改。

### 风险、缺失证据与回滚

1. **P1：真实 Meta/Google Play 链路尚未验证。** 本地只有平台配置、在线 assetlinks 与替身回调证据，尚无广告点击、商店安装、首次启动 `FetchDeferredAppLinkData` 回调和实际关卡画面；必须标记 `uncertain`。
2. **P2：跳入组内后段沿用原组完成语义。** `ILevel_XXX` 进入后仍由原控制器保存当前索引并推进；若从后段完成末关，可能按原逻辑记整组完成，属于已有直达入口影响而非本次新增存档规则。
3. **P2：错误格式会安全回退但可能掩盖投放配置错误。** 验收必须检查 `target_resolved` 的 `requested/path/index/fallback=False`，不能只看应用被打开或 `entry_accepted`。
- 隔离 harness 五组平台宏各通过 647 项断言；Editor/Android 定向编译分别 0 error/113 warning、0 error/116 warning。Unity PlayMode、AAB/Gradle、真机资源加载和横屏画面仍未覆盖。
- 回滚恢复 `OneLinkLevelRouter` 原 IWanner 匹配块及两处冲突注释，保留 Facebook Hook、普通 IWanner 入口、配置与玩家存档。

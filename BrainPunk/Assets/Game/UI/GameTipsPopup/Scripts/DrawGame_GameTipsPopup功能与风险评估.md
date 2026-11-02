# DrawGame_GameTipsPopup 功能与风险评估

> 2026-09-07 起，普通提示与跳关界面均改为单金币按钮。下文广告相关章节为历史基线，当前入口以文末单金币按钮记录为准。

## 1. 需求与背景

- Draw 玩法点击 HUD 提示按钮时，需要复用现有 `GameTipsPopup` 的 Normal 界面和金币/广告获取方式。
- 提示成功后不进入主线答案弹窗，也不派发主线 `UseTipsEvent`；改为派发 `DrawGameHintEvent`。
- `DrawPlayCtrl` 分别记录当前关卡会话的 `HasGrantedHint` 获取资格和当前尝试的提示显示状态，并通过既有 `IDrawLevelRuntime.ShowHint(...)` 控制场景中的 `TipsObject`。
- 自 2026-08-18 起，同关手动 Reset、失败重载和 VictoryPanel Replay 保留已经付费或广告获得的提示资格；切换关卡、返回选关页或退出 Draw 才清除，避免同关重建后重复收费。

## 2. 影响文件与系统

- `Assets/Game/UI/GameTipsPopup/Scripts/DrawGame_GameTipsPopup.cs`
  - Draw 专用弹窗生命周期、金币/广告提示获取、暂停恢复与事件派发。
- `Assets/Game/UI/GameTipsPopup/Resources/DrawGame_GameTipsPopup.prefab`
  - 继续继承 `GameTipsPopup.prefab` 的原界面；只禁用原 `GameTipsPopup` 逻辑组件并增加 Draw 专用组件，没有修改视觉节点、图片、文本层级或布局。
- `Assets/Game/Scripts/Play/PlayEvents.cs`
  - 新增用户明确要求的 `DrawGameHintEvent`。
- `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
  - 订阅提示事件、分离提示获取资格与当前尝试显示状态，向弹窗工厂提供活动关卡就绪校验，并在同一 DLevel 重建后从资格重新派生提示显示。
- `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel.cs`
  - 提示按钮直接调用 `DrawGame_GameTipsPopup.Create()`，不再持有控制器注入的提示 Action。
- 复用但未修改：`DrawLevelCtrl.ShowHint(bool)`、`TipsObject` 序列化引用、RemoteConfig 提示金币价格、现有奖励广告帮助器和背包消耗记录。

## 3. 原有行为基线

- `DrawGame_GamePlayPanel` 的提示按钮通过 `DrawPlayCtrl.ShowCurrentHint()` 直接执行 `_drawLevelRuntime.ShowHint(true)`。
- 提示不经过金币或广告弹窗，也没有 Draw 专用提示事件和控制器级 `HasTips`。
- `DrawLevelCtrl.InitializeRuntime()` 记录 `TipsObject` 的 prefab 初始显隐；DLevel 整体重建后会回到该初始状态。
- 主线 `GameTipsPopup` 通过 `UseTipsEvent` 修改主线 `LevelRuntimeState.HasTips`，不适合直接写入 Draw 运行时。

## 4. 修改后行为

1. Draw HUD 首次点击提示时直接调用 `DrawGame_GameTipsPopup.Create()`；工厂只在活动的 `DrawPlayCtrl` 关卡运行时已就绪且尚未获得提示时创建弹窗，弹窗显示后暂停当前 Draw 输入。
2. 弹窗保持原 Normal 界面：
   - 金币按钮读取 `GetDrawGameGameTipsPopupCoinCost()`；余额不足时打开 `CoinGiftPopup`，不直接进入商店。
   - 无限金币直接发放提示。
   - 广告按钮沿用 `RewardPlacement.InGameTips`；成功后补记一次 `Tips` 消耗。
3. 成功路径只派发一次 `DrawGameHintEvent`，随后关闭弹窗并恢复 Draw。
4. 活动中的 `DrawPlayCtrl` 收到事件后同时写入 `_hasGrantedHint=true` 与当前尝试显示状态，并调用当前关卡的 `ShowHint(true)`。
5. 手动 Reset、失败重载或 VictoryPanel Replay 只开启同一关的新尝试：保留 `_hasGrantedHint`，并把新实例显示状态重新派生为该资格；玩家不需要重复付费或看广告。
6. 已获取提示后再次点击 HUD，弹窗工厂依据 `HasGrantedHint` 进入既有 Draw 跳关弹窗，不再进入首次提示收费流程。
7. `TryEnterLevel(...)` 接受新的选关进入请求、返回选关页或退出 Draw 时同时清除提示资格和显示状态；两者均不写入 DB。

## 5. 数据、UI、事件与资源流

```text
Draw HUD TipsBtn
  -> DrawGame_GameTipsPopup
  -> 校验活动 DrawPlayCtrl / IsHintInteractionReady / HasGrantedHint
  -> 金币/无限金币/奖励广告成功
  -> DrawGameHintEvent
  -> DrawPlayCtrl.HasGrantedHint = true
  -> IDrawLevelRuntime.ShowHint(true)
  -> DrawLevelCtrl 显示当前 DLevel/TipsObject
```

- Reset/失败/Replay 流：保留 `_hasGrantedHint` → 从资格派生当前尝试显示状态 → DLevel 销毁与重建 → `InitializeRuntime()` → `ShowHint(true)` 恢复提示。
- 重新选关流：`DrawGame_ChoiceLevelPanel` → `TryEnterLevel(...)` → 同时清除资格与显示状态 → 加载新 DLevel → 显式隐藏 `TipsObject`。
- 未新增存档字段、Luban 配置、资源加载路径、analytics、IAP 或平台 SDK 分支。
- 弹窗 prefab 仍以已有 Resources 名称 `DrawGame_GameTipsPopup` 加载，没有复制视觉资源。

## 6. 风险项

- `HasGrantedHint` 是 `DrawPlayCtrl` 当前关卡会话的内存资格；同关 Reset、失败重载和 Replay 保留，但返回选关、切换关卡、退出玩法、进程重启或控制器销毁后不会恢复。
- 弹窗运行时按现有 Normal prefab 的 `close_btn`、`yes_btn (1)`、`no_btn (1)` 名称解析按钮；若后续美术重命名，需要同步调整脚本或改为显式序列化绑定。
- 提示广告与 CoinGift 内的广告/商店使用现有公共链路，真实广告成功/失败、商店叠层返回和耗材记录仍依赖 PlayMode/真机验证。
- Reset 期间若存在外部强制关闭 UI 或切换玩法，旧提示事件由 `_isActive` 门禁隔离；异步广告回调的完整平台时序仍需真机补验。
- Android/iOS 共用相同 C# 路径，没有新增每帧逻辑或资源实例化峰值；横屏遮罩、触摸和暂停恢复尚未真机验证。

## 7. 验证证据

- CodeGraph 与当前源码复核确认调用关系：`DrawGame_GamePlayPanel.OnClickTips` → `DrawGame_GameTipsPopup.Create`；弹窗工厂通过活动 `DrawPlayCtrl` 校验运行时和 `HasGrantedHint`，成功事件仍为 `DrawGameHintEvent` → `DrawPlayCtrl.OnGameHintEvent` → `IDrawLevelRuntime.ShowHint`。
- 静态 prefab 证据确认当前 `DrawGame_TipsObjectSubObject.prefab` 根节点默认 active；`OpenDrawLevelAsync()` 的 `ShowHint(_shouldShowHintForCurrentAttempt)` 是关卡实例提示显隐的权威门禁。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过：0 error，109 个工程既有 warning。
- Unity Editor 已成功导入 `DrawGame_GameTipsPopup.prefab`；最新日志中没有本次脚本、prefab YAML 或编译错误。
- 静态检查确认新脚本没有 `Update`、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB 或 PlayerPrefs 路径。
- 未执行 Unity PlayMode、广告沙盒、Android/iOS 横屏真机和 Reset 玩家可见流程，以上均为待补验证项。

## 8. 手工验证清单

- 首次进入 Draw 关卡，确认 `TipsObject` 按 prefab 初始状态隐藏。
- 点击提示后关闭弹窗，确认不扣费、不显示 `TipsObject`、Draw 输入恢复。
- 金币足够、无限金币、奖励广告成功三条路径分别确认事件只触发一次且显示提示。
- 金币不足时确认先显示 CoinGift；CoinGift 关闭或从其商店入口返回后仍停留在原提示弹窗，最终关闭提示弹窗才恢复 Draw。
- 获取提示后执行手动 Reset、失败自动重载与 VictoryPanel Replay，确认新关卡实例继续显示 `TipsObject`，且再次点击不进入首次提示收费流程。
- 获取提示后再次点击提示按钮，确认进入既有 Draw 跳关弹窗，不重复扣除首次提示费用。
- 返回选关并重新进入同一关或其它关，确认 `HasGrantedHint` 重置，提示回到新 prefab 的初始状态。
- Android/iOS 横屏检查遮罩覆盖、按钮触摸、广告返回和暂停恢复。

## 9. 回滚路径

- 恢复 `DrawGame_GamePlayPanel.BindShowHintAction(...)`、`DrawPlayCtrl.ShowCurrentHint()` 及 HUD 创建后的 Action 绑定，即可回到控制器转发入口。
- 删除 `DrawPlayCtrl` 的 `_hasTips`、事件订阅/处理及 DLevel 初始化后的恢复分支。
- 删除 `DrawGameHintEvent`、`DrawGame_GameTipsPopup.cs` 及其 prefab 新增组件；保留用户原有 prefab 视觉变体即可。
- 回滚不涉及 DB、配置或资源迁移；已产生的普通 `Tips` 消耗记录不会自动减少。

## 2026-07-30：场景提示默认开放与提示按钮零消费

### 需求、基线与影响范围

- 用户要求 Draw 关卡第一次进入和刷新关卡后，场景提示线本来就在画面上，不需要先点击提示按钮。
- 本节有意覆盖上文“新选关把 `_hasTips` 清为 `false`、首次点击打开金币/广告弹窗”的旧规则；旧实现与兼容分支保留在代码中，但不再是正常新进关流程。
- 行为改动位于 `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`；复用的显示入口仍是
  `IDrawLevelRuntime.ShowHint(bool)`、`DrawLevelCtrl.ShowHint(bool)` 与
  `DrawLineFeature.ShowHint(bool)`。未修改提示弹窗 prefab、DLevel prefab、控制点、DB、
  RemoteConfig、资源地址、analytics 或平台 SDK。

### 修改后行为与调用流

- `DrawPlayCtrl.TryEnterLevel(...)` 接受新的 Draw 关卡请求后把 `_hasTips` 设为 `true`。
  `OpenDrawLevelAsync(...)` 初始化新 DLevel 后执行 `ShowHint(_hasTips)`，因此首次进入即显示
  `TipsObject`。
- 手动 Reset、失败自动重载和 Victory Replay 都复用
  `ResetGameAsync -> ReloadCurrentLevelAsync -> OpenDrawLevelAsync`；这些路径不会把
  `_hasTips` 改回 `false`，新实例继续执行 `ShowHint(true)`。
- HUD 提示按钮仍调用 `DrawGame_GameTipsPopup.Create()`。正常 Draw 关卡中
  `HasTips == true`，工厂只调用 `RestoreGrantedHint()` 并立即返回 `null`，不会创建弹窗，
  也不会进入金币扣除、提示奖励广告、Tips 消耗记录或 `DrawGameHintEvent`。
- Draw HUD 的墨水奖励广告按钮以及 Reset/失败流程原有的插屏策略是独立入口，本次没有修改，
  不能与“提示按钮零消费”混为一谈。

### 验证、风险与回滚

- 静态调用链确认当前 `_hasTips` 的两个赋值点都写入 `true`；新进关、手动 Reset、失败重载和
  Victory Replay 最终都到达 `OpenDrawLevelAsync` 的 `ShowHint(true)`。
- Unity 6000.0.59f2 已在源码更新后重新生成 `Assembly-CSharp.dll`，Editor.log 未记录本次
  `error CS`、`NullReferenceException` 或 `MissingReferenceException`。
- Unity PlayMode 窗口实测：DLevel_002 首次显示提示虚线；触发 HUD 重载后出现的新
  DLevel_004 实例仍显示提示虚线。该现场的关卡身份发生变化，因此同关卡身份保持仍以静态
  调用链为证，失败自动重载和 Victory Replay 尚未逐项人工触发。
- 点击 HUD 灯泡按钮后没有打开提示弹窗，提示线保持可见；金币余额、广告后台请求和 Tips
  记录未做运行时探针，零消费结论同时依据工厂在 `HasTips == true` 时的提前返回路径。
- Android/iOS 横屏、进程重启、广告沙盒以及十个 DLevel 逐关 PlayMode 目测仍待验证。
- 若恢复旧付费/广告获取规则，只需把 `TryEnterLevel(...)` 的初始化恢复为
  `_hasTips = false`；现有弹窗、事件、金币与广告兼容代码无需迁移或删除。

## 2026-08-04：重新选关提示引用制作期修复工具与历史记录纠正

### 需求、事实与直接结论

- 当前确认规则与主线一致：从 `DrawGame_ChoiceLevelPanel` 重新进入任意 DLevel 时，
  `DrawPlayCtrl.TryEnterLevel(...)` 必须把 `_hasTips` 重置为 `false`。自 2026-08-06 起，Reset、失败重载、
  成功结算和 Victory Replay 同样清零；只有同一次尝试内保留提示。
- Git 证据显示该重置规则由 2026-07-27 的提交 `57154d067` 引入，当前源码仍保持
  `_hasTips = false`。上方“2026-07-30：场景提示默认开放与提示按钮零消费”记录声称代码改为
  `_hasTips = true`，但对应提交没有这项源码变更；该节与当前有效规则冲突，现以本节为准。
- 直显的实际原因不是状态未清空，而是部分 DLevel 包含默认 active 的
  `DrawGame_TipsObjectSubObject`，但 `DrawLineFeature.tipsObjects` 缺失或为空，导致既有
  `ShowHint(false)` 没有目标可控制。
- 当前 DLevel_039 版本由 2026-08-04 18:05 的提交 `668507a41` 引入，提交内容中的
  `tipsObjects: []` 直接命中该问题；这不是 `DrawPlayCtrl` 生命周期在该提交中被改动。

### 工具、行为与边界

- 新增 `Assets/Editor/DrawGameTipsReferenceRepairTool.cs`，菜单入口当前为
  `Tools/Bear/DrawGame/修复丢失的 Tips 引用（跳过 001/007/022）`。
- 工具只在用户手动调用时遍历 `Assets/Game/Prefabs/DrawLevel` 下的 `DLevel_*.prefab`，比较层级中的
  `TipsObject`、`DrawGame_TipsObjectSubObject` 及 Unity 重名后缀实例与序列化 `tipsObjects` 列表，
  只追加确实缺失的引用；已有引用和没有提示对象的 prefab 不保存。
- `DLevel_001`、`DLevel_007`、`DLevel_022` 无条件排除。工具不在导入、`OnValidate`、`Reset`、prefab preview 或
  运行时自动执行，也不访问 DB、Save、配置、运行时单例、资源加载、事件或平台链。
- 已撤销运行时按名称扫描的方案，`DrawLineFeature.cs` 恢复原实现。当前任务没有执行修复菜单，
  因此没有修改任何 DLevel prefab，也没有覆盖工作区中 DLevel_039 的既有未提交调整。

### 验证、剩余风险与回滚

- 静态扫描确认 DLevel_024、025、026、027、028、031、037、039 存在提示对象但序列化列表缺失或为空；
  工具的名称规则覆盖这些对象。DLevel_999 不含提示对象，不会被追加引用。
- 已静态复核菜单门禁、DLevel_001/007/022 排除、只追加缺失引用、仅在有修改时保存、异常后卸载 prefab 内容和
  进度条清理路径；目标差异格式检查通过。
- Unity 已把新脚本加入 `Assembly-CSharp-Editor.csproj`；`Assembly-CSharp-Editor-firstpass` 编译为
  0 warning / 0 error，`Assembly-CSharp-Editor` 编译为 0 error / 4 个 `BuildToolV2.cs` 既有 warning，
  目标工具没有编译错误。用户通过 Esc 停止了编辑器窗口控制，因此本轮没有执行菜单或验证菜单视觉位置；
  任何 prefab 修复结果仍为 `uncertain`。运行工具后需检查 Console 汇总和 prefab diff，再验证
  ChoiceLevelPopup 重进与 Tips 获取/Reset 流程。
- 回滚只需删除 Editor 工具脚本及其 `.meta`，不会影响运行时代码、存档、配置或已存在的 prefab；
  如果工具已执行，需按其 Console 报告和 Git diff 逐个撤销对应 prefab 的新增引用。

## 2026-08-04：弹窗 UI 引用改为 Inspector 显式绑定

### 需求、原行为与修改后行为

- 需求：`DrawGame_GameTipsPopup` 的 UI 依赖全部暴露给 Inspector，运行时不允许通过名称或子节点扫描查找。
- 原行为：`OnCreate()` 调用 `ResolveReferences()`，遍历所有子节点 `CustomButton`
  并按节点名匹配三个按钮，再通过 `GetComponentInChildren` 获取金币文本和 Spine 控制器。
- 修改后：关闭按钮、金币按钮、广告按钮、金币消耗文本和 Spine 控制器均为
  `[SerializeField]` 字段，并在 `DrawGame_GameTipsPopup.prefab` 中显式绑定。运行时不再遍历或查找层级。

### 影响范围与流程

- 修改脚本：`Assets/Game/UI/GameTipsPopup/Scripts/DrawGame_GameTipsPopup.cs`。
- 修改资源：`Assets/Game/UI/DrawGame/TipsPopup/Resources/DrawGame_GameTipsPopup.prefab`。
- 金币价格刷新、广告回调、`DrawGameHintEvent`、暂停/恢复、存档、资源加载与平台流程均不变。
- prefab 的金币文本显式绑定为 `ui_tishi_1_txt`，不再依赖子节点遍历顺序。

### 验证、剩余风险与回滚

- 已静态确认目标脚本不再包含 `GetComponentsInChildren`、`GetComponentInChildren`、`Transform.Find`
  或 `GameObject.Find`；prefab 五个序列化引用均指向当前弹窗内的对应组件。
- `Assembly-CSharp.csproj` 使用 `BuildProjectReferences=false` 编译通过：0 error / 101 个工程既有 warning。
  完整项目引用重编译被火绒进程占用 `Temp/obj/spine-csharp/spine-csharp.dll` 阻断，错误为 `CS2012`，
  属于外部文件锁，本次未强制结束用户进程。
- `uncertain`：Unity PlayMode、金币/广告成功流、弹窗关闭恢复以及 Android/iOS 横屏触摸未在本次静态修改中完成。
- 回滚时恢复 `ResolveReferences()` 及五个运行时字段，并删除 prefab 组件上对应序列化引用。

## 2026-08-05：已显示提示后打开 Draw 专用跳关弹窗

### 需求、基线与影响范围

- 需求：DrawGame 当前选关进入周期已经获得并显示场景提示后，玩家再次点击 HUD 提示按钮，应打开
  `Assets/Game/UI/DrawGame/TipsPopup/Resources/DrawGame_GameTipsPopup_2.prefab`，允许通过金币或激励视频直接跳关。
- 原行为：`DrawGame_GameTipsPopup.Create()` 在 `DrawPlayCtrl.HasTips == true` 时只调用
  `RestoreGrantedHint()` 并返回，不创建弹窗，也没有 Draw 专用跳关入口。
- 本次修改 Draw 专用提示脚本、用户新增的 `_2` prefab 变体、Draw RemoteConfig 读取入口、Guru 默认配置和
  Luban Guru 同步白名单；不修改主线 `GameTipsPopup_3`、DLevel prefab、Draw 关卡脚本或存档结构。

### 修改后行为与数据流

1. 尚未获得提示时，HUD 继续打开 `DrawGame_GameTipsPopup`，金币和广告获取提示的原流程保持不变。
2. `HasTips == true` 且当前阶段为 `DrawPlayPhase.Drawing` 时，工厂改为加载
   `DrawGame_GameTipsPopup_2`；该 prefab 通过 `_isPassLevelPopup=true` 明确声明跳关模式。
3. 跳关金币价格读取
   `ConfigManager.RemoteConfig.GetDrawGameGameTipsPopupPassLevelCoinCost()`；余额不足时保留原商店与系统提示。
4. 跳关广告继续使用 `RewardPlacement.InGamePassLevel`。原生广告覆盖期间 Draw 保持暂停，失败回调恢复按钮；
   成功回调先释放弹窗持有的暂停，再调用 `DrawPlayCtrl.TryForceVictory("DrawGameTipsPopupPassLevel")`。
5. 胜利提交被当前 `IDrawLevelRuntime` 接受后，继续进入既有成功插屏、每日挑战、统计、Draw 进度/星级存档和
   Draw 胜利面板链；金币只在胜利提交成功后扣除，避免运行时失效时误扣。
6. Draw 首次提示与跳关价格分别读取 `drawgame_config.drawGame_gameTipsPopupCoinCost`、
   `drawgame_config.drawGame_gameTipsPopupPassLevelCoinCost`，字段缺失时回退新 GlobalConst 字段。

### 配置、资源与平台边界

- `Luban/Configs/Datas/Const/Global.xlsx` 与生成产物当前值分别为提示 `60`、跳关 `40`；
  `Assets/Guru/guru_spec.yaml` 和 `Assets/Guru/Gen/GuruAllSpecs.G.cs` 的 `drawgame_config` 已同步相同字段和值。
- `LubanGenerator.GlobalConstDrawGameConfigKeys` 已加入两个字段，后续执行 DrawGameConfig Guru 同步不会丢失它们。
- `RemoteConfigService` 已统一拉取整个 `drawgame_config`，本次不新增顶层 remote key、缓存或保存路径；
  `ConfigManager.RemoteConfig` 负责字段级远端优先与 GlobalConst 回退。
- 不假设原生广告期间 Unity 帧继续推进：广告请求前关卡已暂停，只有结果回调后才恢复并提交胜利。

### 验证、剩余风险与回滚

- JSON 静态解析确认 GlobalConst 运行时产物与 Guru `drawgame_config` 的提示/跳关价格均为 `60/40`；
  `_2` prefab 已包含 `_isPassLevelPopup` override，资源名为 `DrawGame_GameTipsPopup_2`。
- `Assembly-CSharp.csproj` 编译通过：0 error / 1119 warning；`Assembly-CSharp-Editor.csproj` 编译通过：
  0 error / 4 warning。warning 为工程既有项。
- `uncertain`：尚未执行 Unity PlayMode 的首次提示、二次点击、金币足够/不足、无限金币、广告成功/失败、
  胜利插屏/每日挑战竞争、重复点击及 Android/iOS 横屏真机验证。
- 回滚时恢复 `DrawPlayCtrl.RestoreGrantedHint()` 与 `Create()` 的 HasTips 提前返回，删除跳关按钮处理和 prefab
  模式 override；删除两个 Draw getter、Guru 默认字段和生成器白名单。若保留 GlobalConst 源字段，不影响旧客户端读取。

## 2026-08-05：Tips 引用修复工具排除 DLevel_007 与 DLevel_022

- 需求：DLevel_007、DLevel_022 与 DLevel_001 一样，不参加手动工具对
  `DrawGame_TipsObjectSubObject` 的层级扫描和缺失引用追加。
- 原行为：工具只排除 DLevel_001；手动执行时仍会加载并扫描 DLevel_007、DLevel_022。
- 修改后：按三个固定 prefab 名称直接排除；菜单、成功弹窗和 Console 汇总同步显示
  `001/007/022`。排除判断发生在 `LoadPrefabContents` 之前，因此三关不会被加载、扫描或保存。
- 数据/资源/UI/事件/平台：不修改三个关卡 prefab、运行时代码、DB、配置、存档、资源地址、事件或
  Android/iOS 路径；仅改变用户手动执行编辑器修复工具时的目标范围。
- 验证：静态检查排除判断位于单 prefab 修复入口之前，并覆盖
  `DLevel_001/007/022` 三个固定名称；目标三个 prefab 差异为空；
  `Assembly-CSharp-Editor.csproj` 编译为 `0 error / 4 warning`，warning 属于工程既有项。
- `uncertain`：尚未在 Unity Editor 中实际点击菜单核对弹窗与 Console 文案；工具也未实际执行，
  因此本次没有改写任何 DLevel prefab。
- 回滚：移除 DLevel_007、DLevel_022 的直接判断，并恢复菜单及报告文案；无需迁移资源或数据。

## 2026-08-18：同关 Reset 提示资格与显示状态分离

### 需求、原行为与修复后行为

- 用户确认同一 Draw 关卡已经通过金币或广告获得提示后，Reset、失败重试或 VictoryPanel Replay 不得再次收取首次提示费用。
- 原实现用 `_hasTips` 同时表示“已经获得提示资格”和“当前尝试显示提示”。`OnGameResetEvent()`、成功结算与 Replay 打点初始化会把该值清为 `false`，DLevel 重建后再次点击因此重新进入首次提示收费流程。
- 修复后由 `_hasGrantedHint` 保存当前关卡会话资格，由 `_shouldShowHintForCurrentAttempt` 保存当前尝试显示状态。获取提示时两者同时置为 `true`；同关重建只从资格重新派生显示状态，不修改资格。
- `TryEnterLevel(...)` 接受新选关请求、`OpenLevelSelection(...)` 返回选关页以及 `CleanupRuntime()` 退出 Draw 时统一清除两者，避免跨关卡或跨玩法继承。
- 成功结算只清理当前尝试显示状态；VictoryPanel Replay 仍属同一关卡会话，因此重新显示已经获得的提示。弹窗工厂改用 `HasGrantedHint` 判断首次提示与既有跳关模式。

### 数据、资源、事件、打点与平台边界

- 不新增 DB、PlayerPrefs、Luban、RemoteConfig 或存档字段；提示资格仍仅存在于当前 `DrawPlayCtrl` 内存会话。
- 不修改提示金币/广告消费、`DrawGameHintEvent`、TipsObject prefab、HUD prefab、资源路径、广告 placement 或 IAP。
- Reset/Replay 的 revive、`level_start`、`level_end`、playCount、duration 和 start_type 顺序不变；仅把付费提示资格从 Analytics 尝试清理状态中剥离。
- Android/iOS 与 Editor 共用相同常量级布尔状态路径，没有新增逐帧分配、扫描、实例化或平台分支。

### 验证、剩余风险与回滚

- CodeGraph 复核确认 `ResetHintGrantSession()` 只由新选关请求、返回选关页和 Draw 整体清理调用；`PrepareHintVisibilityForSameLevelAttempt()` 只由 Reset/失败重试与 VictoryPanel Replay 调用。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过：`0 error / 109 warning`，warning 为工程既有项。
- 本次 9 个目标文件的定向 `git diff --check` 通过；全工作区检查仍会命中用户原有字体资源的尾随空格，本次未改动或清理这些无关文件。
- `uncertain`：尚未在 Unity PlayMode 实测金币、广告获取提示后的手动 Reset、失败自动重载、VictoryPanel Replay、返回选关再进入，以及 Android/iOS 横屏与真实广告回调。
- 回滚时恢复单一 `_hasTips` 字段和 `HasTips` 门禁，并恢复 Reset、成功与 Replay 对该字段的清理；无需迁移存档、配置、资源或玩家数据，但会重新出现同关 Reset 后二次收费问题。

## 2026-09-03：Draw 金币不足改为打开 CoinGiftPopup

### 需求、基线与实现

- Draw 获取提示与已提示后直接通关的两个金币不足分支，均需与主线一致打开 `CoinGiftPopup`。
- 原分支直接调用 `ShopPanel.Create(false, nameof(DrawGame_GameTipsPopup))` 并显示金币不足提示；当前改为统一调用 `OpenCoinGiftPopup()`，只有 CoinGift 创建失败才回退显示旧系统提示。
- 本次只修改 `DrawGame_GameTipsPopup.cs`，未改动 Draw 提示资格、成功事件、直接通关提交、Reset/Replay、Prefab 或远程提示价格。

### 暂停、UI 与数据流

- `DrawGame_GameTipsPopup.OnShow()` 继续持有当前 `IPlayPauseHost` 并暂停 Draw。CoinGift 覆盖显示时底层提示弹窗只执行 `OnHide`，不执行 `OnClose/Resume`，因此不会在两个弹窗间短暂恢复输入。
- CoinGift 关闭后底层 Draw 提示弹窗重新 `OnShow/Pause`；`DrawPlayCtrl.Pause()` 是幂等状态写入，重复调用不会叠加暂停计数。
- CoinGift 广告或购买成功只补充金币并返回提示弹窗，不自动发放 Draw Hint、不自动通关、不扣除提示价格。
- 未新增存档、配置、资源、全局事件、analytics 或平台分支。

### 验证、剩余风险与回滚

- 静态检查确认 `TryToUseHintCoins()` 与 `PassLevelByCoin()` 两个不足分支均进入 `OpenCoinGiftPopup()`，目标脚本不再直接打开 Shop。
- 运行时程序集首次构建为 `0 error / 122 warning`，文档更新后用同命令最终复验为 `0 error / 0 warning`。
- `uncertain`：Unity PlayMode 下获取提示/直接通关两种模式、CoinGift 关闭、进入商店返回、广告/IAP 回调以及 Android/iOS 横屏暂停仍待手工验证。
- 回滚时只需恢复这两个不足分支直接打开 Shop 与系统提示；无需迁移存档、Prefab 或配置。


## 2026-09-07：新 UI 单金币按钮绑定与脚本清理

### 需求、原行为与影响范围

- 用户在主线确认普通提示、答案/跳关界面均只保留金币按钮后，要求 Draw 同样适配新 UI 并优化脚本。
- 修改前 Draw 基础 Prefab 已由用户替换为新美术，`yes_btn` 容器已有 CustomButton 和点击反馈，但 `_coinButton`、`_coinText` 为空；脚本仍包含广告按钮、广告请求状态和两组广告回调。
- 本次仅修改 `DrawGame_GameTipsPopup.cs`、`Assets/Game/UI/DrawGame/TipsPopup/Resources/DrawGame_GameTipsPopup.prefab` 及 `_2.prefab`，补充本记录和中央使用记录。保留用户的布局、图片、材质和元数据。

### 最终行为与复用边界

- Inspector 显式绑定现有金币按钮 `4549161023984431160` 与价格文本 `4083077468339625172`；跳关变体继承绑定，保留 `_isPassLevelPopup=1` 和 `U_DrawLevel_Skip_Des_01` 文案，删除两条指向已删除旧文案对象的覆盖。
- 移除 `_adButton`、`_isRewardAdRequesting`、广告请求/回调及相应按钮锁判断；OnCreate 只按提示/跳关模式给金币按钮注册对应处理方法。类说明中的“金币或广告跳关”已与新 UI 不符，因此改为“金币跳关”。
- 保留 `_isHintGranted` 与 `_isPassLevelCompleted` 防重复标志、金币/无限金币、金币不足 CoinGift、提示与跳关各自的 Draw 价格读取，以及既有 Spine 动画和 UI 生命周期。
- 提示仍是扣币 → GrantHint → DrawGameHintEvent → 关闭弹窗/恢复 Draw。跳关仍先检查活动 Draw 与 Drawing 阶段，释放本弹窗暂停 → 关闭弹窗 → TryForceVictory 成功 → 扣币；不改变打点、成功结算、每日挑战或原生覆盖层顺序。
- 不修改 DrawPlayCtrl、关卡脚本/Prefab、提示资格、Reset/Replay、存档、配置、全局事件和其它广告入口。无需新增共享抽象或运行时层级扫描。

### 验证与剩余风险

- 定向 C# 构建通过，命令同主线记录：`dotnet build ./Assembly-CSharp.csproj --no-restore --no-dependencies -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，0 错误 / 121 警告。
- 静态校验两个 Prefab anchor 无重复、本地引用完整、跳关变体所有基础目标可解析，4 个必需 UI 引用有效；基础仅保留关闭和金币两个 CustomButton，跳关模式及文案覆盖仍有效。
- `uncertain`：尚无 Unity 导入/PlayMode 与 Android/iOS 横屏验证。需补验首次提示、已提示后跳关、无限金币、余额不足 CoinGift 往返、连续点击、关闭恢复、跳关拒绝不扣币及胜利流程。静态检查与 C# 编译不替代这些运行时验证。

### 回滚条件与路径

- 如出现按钮无法点击、价格不刷新或暂停/跳关回归，撤销本次脚本及绑定调整并复核新美术引用；不要覆盖用户原有 UI 修改。
- 恢复广告代码必须同时提供有效广告按钮资源，不能只恢复空引用。无需迁移玩家存档、配置或提示资格。

## 2026-09-10：关闭按钮补齐金币不足检测

### 需求、基线与修改范围

- 按用户要求，对齐主线 `GameTipsPopup.Close` 的检测：关闭按钮始终正常关闭，余额不足时额外打开金币礼包。
- 原 `DrawGame_GameTipsPopup.Close` 仅调用 `ClosePopup()`；本次只调整该按钮回调，不修改确认消费、提示发放、跳关提交或 Prefab。
- 基础 Prefab 已显式绑定 `_closeButton`，`_2` 变体以 `_isPassLevelPopup` 区分跳关，无需新增绑定或共享抽象。

### 行为与数据/UI流

- 关闭前检查无限金币；未持有无限金币时，按当前模式读取 Draw 提示价或跳关价，以 `Coin < cost` 保存不足结果。余额等于价格时不触发礼包。
- 继续执行 `ClosePopup -> UIManager.CloseUI -> OnClose -> IPlayPauseHost.Resume`，随后不足时调用已有 `OpenCoinGiftPopup()`，创建失败仍走系统提示。
- 不扣币、不发放提示、不提交胜利、不写存档；CoinGift 不新增暂停职责。获取提示或跳关成功调用的 `ClosePopup()` 不附带此检测，避免消费成功后额外弹礼包。
- 保留确认按钮余额不足时覆盖提示弹窗并维持暂停的既有行为；关闭按钮路径则恢复 Draw，与主线关闭检测的职责一致。

### 验证与风险

- 已静态核对主线关闭逻辑、Draw 两种价格/无限金币消费条件、Prefab 关闭按钮绑定，以及 UIManager 同步调用 OnClose 的顺序。
- 常规定向 `dotnet build` 因缺少 `Temp/obj/Assembly-CSharp/project.assets.json` 报 NETSDK1004，改用现有 Unity Bee 响应文件与 Unity Roslyn 编译，输出重定向到临时目录以避免覆盖 Unity 程序集。
- CodeGraph CLI 与知识库 MCP 不可用，规则引用的 feature plan 缺失，本次证据来自本地脚本、Prefab 和既有文档。
- `uncertain`：尚未完成 Unity PlayMode 与 Android/iOS 横屏实测。需在提示/跳关两模式检查余额不足、恰好足够、余额充足、无限金币；确认关闭恢复、礼包返回、创建失败提示及真实广告/IAP 覆盖层恢复。

### 回滚

- 仅撤销 `DrawGame_GameTipsPopup.Close` 新增的检测与礼包调用，恢复单独调用 `ClosePopup()`；无需迁移存档、配置、资源或提示资格。

- 最终验证补记：Unity Roslyn 运行时程序集编译退出码 0，无 error；日志为 C:/Users/admin/AppData/Local/Temp/draw-tips-close-9f8c687f5f864bc8bf8ed392207d97d6/compile.log。三个目标文件的 git diff --check 通过。

## 2026-09-10：确认按钮点击后关闭不重复打开金币礼包

### 需求、基线与修改范围

- `DrawGame_GameTipsPopup` 在 closeBtn 增加余额检测后，玩家若先点击金币确认按钮并打开过不足礼包，返回原弹窗再关闭会重复打开 CoinGift；本次按用户要求补充单次弹窗交互标记。
- 仅修改 Draw 提示弹窗实例运行态和金币提示/跳关两个确认回调；不修改 Prefab、Draw 提示资格、价格配置、扣币、发放提示、强制胜利、暂停接口、存档、事件或 CoinGift。

### 修改后行为与 UI 流

- 每次 `OnOpen()` 将 `_hasClickedConfirmButton` 重置为 `false`；`TryToUseHintCoins()` 与 `PassLevelByCoin()` 进入时立即置为 `true`，包括余额不足、无限金币、成功消费和运行时拒绝路径。
- closeBtn 仅在标记仍为 `false` 时按当前提示/跳关价格检测余额。标记为 `true` 时仍执行 `ClosePopup -> OnClose -> Resume`，但不再调用 `OpenCoinGiftPopup()`。
- CoinGift 覆盖期间底层 Draw 弹窗只经历 `OnHide/OnShow`，不会重新执行 `OnOpen()`，所以返回后标记保留；下一次新打开弹窗时自动清空。该标记不持久化，也不跨关卡或会话。

### 验证、剩余风险与回滚

- 已静态核对两个确认回调、closeBtn 判断和 OnOpen 重置；Unity Roslyn 运行时程序集编译退出码 0、0 error，目标差异检查通过。
- `uncertain`：尚未在 Unity PlayMode 和 Android/iOS 横屏验证“确认不足 -> 关闭 CoinGift -> 关闭 Tips”链路，以及基础提示/跳关两种模式。
- 回滚时移除 `_hasClickedConfirmButton` 及其重置、置位和 closeBtn 门禁，即恢复关闭时始终按余额检测；无需迁移数据、配置或资源。

# GameTipsGuideFlow 功能说明

## 需求背景

首次提示剧情引导用于帮助从未使用过提示功能的玩家理解提示入口。玩家达到配置的最低关卡后，在当前单次尝试停留达到门槛，或主动重开与失败重试合计达到配置次数时，只触发一次头像对话、两次手指点击和首次免费提示；免费提示后的最后一句对话仅在玩家点击答案弹窗右上角 X 时出现。

## 影响文件、资源与系统

- `Assets/Game/Scripts/Guide/Scripts/Flow/GameTipsGuideFlow.cs`
  - 把头像对话、提示入口 Finger、0.5 秒停顿、首次免费提示 Finger、X 关闭判断和收尾对话集中到 `GuideFlowBase` 流程。
  - 通过 `ConfigManager.RemoteConfig` 读取独立的最低关卡、单次尝试停留秒数和重试次数门槛。
  - 使用单关 `GameTipsGuideTriggerSettings` 快照，避免 Playing 更新阶段重复解析 RemoteConfig JSON。
- `Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs`
  - 新增 `GetGameTipsGuideLevel()`、`GetGameTipsGuideFailedCount()`、`GetGameTipsGuideWaitingTime()` 三个业务读取 API。
  - 三个 API 均优先读取 `level_config` 同名字段，再回退 `GlobalConst`，最终分别回退 `0`、`0`、`0f`。
- `Assets/Game/Scripts/Play/PlayCtrl_Nodes/PlayCtrl_Playing.cs`
  - 进入 Playing 时通过 `GameTipsGuideFlow.LoadTriggerSettings()` 读取一次配置快照；复用现有更新节点与 `LevelRuntimeState`，门槛首次满足时请求 `PlayCtrl` 启动流程，没有新增 MonoBehaviour `Update` 或全局事件。
- `Assets/Game/Scripts/Play/PlayCtrl.cs`
  - 跨 Playing/Pause 状态持有、运行并取消 `GameTipsGuideFlow`；在重置、重玩、换关、关卡销毁和控制器销毁时主动取消。
  - 流程开始前通过既有 `GamePlayPanelSwitchBlockEvent` 开启 GamePlayPanel 点击遮罩，并在完成、失败、异常或取消后的 `finally` 中关闭，防止引导期间误触底层玩法按钮。
  - `UseTipsEvent` 继续只更新当前关卡的 `HasTips` 运行时状态，不为引导资格写入独立存档字段。
- `Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.cs`
  - 不再持有或运行 `GameTipsGuideFlow`，只向 `PlayCtrl` 提供 `TipsBtn`、Finger 根节点和现有暂停/提示弹窗局部入口；关闭时通知控制器取消仍引用该界面的流程。
- `Assets/Game/UI/GameTipsPopup/Scripts/GameTipsPopup.cs`
  - Normal 弹窗增加仅由引导准备的首次免费模式；免费领取继续派发 `UseTipsEvent`，不扣金币、不播放广告。
  - PassLevel 弹窗增加局部等待能力，用于区分 CloseBtn（X）、NoBtn、直接通关和外部关闭。
- `Assets/Game/DB/GameData.cs`、`GameData_Partial.cs`
  - 仅新增 `HasTriggeredGameTipsGuide` 持久状态；是否使用过提示复用既有 `ToolConsumesCache["Tips"]` 判断，不新增独立字段。
- `Assets/Game/Scripts/Guide/Scripts/GuideDialogue/GuideDialogue.cs`
  - 使用 `GuideDialogueVariant` 路由开场、免费说明和收尾三个 Prefab Variant；对话层级为 `UILayer.Top`。
- `Assets/Game/Scripts/Guide/Resources/GuideDialogue.prefab`
  - 初始 Spine 皮肤由 `box_rich` 切换为 `box_rich01`；现有 Spine JSON 证据显示该变体只比 `box_rich` 少 `rich_decoration_06`，用于落实“金色盒子、去掉眼镜”，不修改 Spine 原资源。
- `GuideDialogue.prefab` 作为 Variant 基础资源；运行时还需要 `_10001`、`_10002`、`_10003` 三个 Variant。继续复用 `GuideFinger.prefab`、`GameTipsPopup` / `GameTipsPopup_3`、I2 本地化、Bear UI、UniTask、Bear Logger 和现有提示事件链。

## 原有行为基线

- `GameTipsGuideFlow.cs` 原本是带空 `Start()` / `Update()` 的 MonoBehaviour，不存在调用方或运行行为。
- `PlayCtrl_Playing` 已按 `showTipsTimeLimit` 或 `showTipsFailCount` 显示提示警示，但不会播放剧情或提供免费提示。
- `GamePlayPanel.OnClickTips()` 直接暂停游戏并打开普通提示弹窗。
- Normal 提示只能通过金币或广告获得；成功后派发 `UseTipsEvent`，再由 `GameTipsPopup.Create()` 打开 PassLevel 答案弹窗。
- 项目已有 `ToolConsumesCache` 消耗次数字典和读取 `Tips` 次数的成就逻辑；金币提示原本只记录实际消耗道具 `Coin`，本次配套调整后 `CostType.Hint` 会额外记录一次 `Tips`。项目原本没有“首次提示引导是否触发”的持久状态。

## 当前行为

1. `PlayCtrl_Playing` 进入状态时读取 `GameTipsGuideLevel`、`GameTipsGuideWaitingTime`、`GameTipsGuideFailedCount` 快照；当前本地配置值为最低关卡 `3`、单次尝试等待 `15` 秒、重试合计 `3` 次，远端 `level_config` 同名字段可覆盖。
2. 当前玩家必须同时满足：本关尚未获得提示、`ToolConsumesCache["Tips"]` 不存在或小于等于 `0`、`HasTriggeredGameTipsGuide=false`。
3. `PlayCtrl` 在流程开始前开启 GamePlayPanel 点击遮罩；流程完成、失败、异常或取消时统一关闭，不影响 Popup 层按钮交互。
4. 流程开始前立即写入 `HasTriggeredGameTipsGuide=true` 并保存，之后播放 `GameTipsOpening` Variant。
5. 第一枚 `GuideFinger` 指向 `GamePlayPanel.TipsBtn`；Finger 点击完成后复用 GamePlayPanel 原暂停与弹窗入口，打开已锁定按钮的首次免费 Normal 弹窗。
6. 等待 0.5 秒后播放 `GameTipsFree` Variant，随后只开放 YesBtn，并由第二枚 Finger 引导点击。
7. 免费点击通过 `SimpleBag.RecordToolConsume(GameProps.Tips, CostType.Hint)` 记录并保存一次 Tips 消耗、触发 `ToolConsumes` 成就检查，再通过现有 `UseTipsEvent` 标记本关获得提示，最后复用 `Create()` 打开 `GameTipsPopup_3`；本次仍不经过背包扣费，不增加 `Coin` 消耗记录。
8. PassLevel 弹窗由 CloseBtn（X）关闭时播放 `GameTipsClosing` Variant；NoBtn、直接通关、外部关闭或流程取消均不播放收尾对话。
9. `PlayCtrl` 在 GamePlayPanel 关闭、关卡销毁、重置、重玩、换关或自身销毁时取消仍在等待的流程；如果首次免费尚未消费，会恢复 Normal 弹窗的普通按钮与价格显示。

## 数据、UI、事件与生命周期

- 触发数据：`LevelRuntimeState.LevelId`、`CurrentAttemptTimeSeconds`、`ResetCount + FailCount`，以及进入 Playing 时读取的 `GameTipsGuideTriggerSettings` 配置快照。
- 配置流：`level_config.gameTipsGuideLevel/gameTipsGuideFailedCount/gameTipsGuideWaitingTime` → 对应 RemoteConfig API → `GlobalConst` 同名字段回退 → 单关快照 → `HasReachedTrigger(...)`。
- 一次性数据：`GameData.HasTriggeredGameTipsGuide` 在剧情开始前消费并使用现有 `DB.SaveGameData()` 保存；免费提示实际消费时由 `SimpleBag.RecordToolConsume(...)` 统一写入既有 `GameData.ToolConsumesCache["Tips"]` 并保存，Popup 不直接操作存档结构，不新增 `HasUsedGameTips` 字段。
- UI 流：`GuideDialogue_10001(Top)` → `GuideFinger(GamePlayPanel)` → `GameTipsPopup` → `GuideDialogue_10002(Top)` → `GuideFinger(GameTipsPopup)` → `UseTipsEvent` → `GameTipsPopup_3` → 可选 `GuideDialogue_10003(Top)`。
- 事件流：只复用现有 `SwitchGameStateEvent`、`GamePlayPanelSwitchTipsEvent`、`GamePlayPanelSwitchBlockEvent` 和 `UseTipsEvent`；没有新增 `PlayEvents` 或全局订阅。
- 免费提示只在 `PrepareFirstGuideFree()` 设置的当前 Normal 弹窗实例生效。普通 `GameTipsPopup.Create()`、金币扣除、广告回调、答案显示和直接通关保持原入口。
- 流程必须跨越第一次点击提示后触发的 Playing → Pause 状态切换，因此生命周期归属 `PlayCtrl`，不能由 `PlayCtrl_Playing` 或 `GamePlayPanel` 持有；Panel 只保留被动 UI 适配职责。
- Android/iOS 共用同一横屏 UI 与触摸路径，没有平台条件分支。

## 验证记录

- CodeGraph 更新后确认：`HasReachedTrigger()` 只有流程自身和 `PlayCtrl_Playing` 两个调用点；`CreateFirstGuideFree()` 只有 `GamePlayPanel` 一个调用点；免费消费只有流程和该模式 YesBtn 两个入口。
- 静态检查确认 `HasUsedGameTips` / `hasUsedGameTips` 已从代码中移除；`CanStart()` 与流程正式消费前均通过 `ToolConsumesCache["Tips"] > 0` 排除已记录提示消耗的玩家。
- `git diff --check` 对本次代码文件通过，仅报告工作区 LF/CRLF 转换提示。
- 敏感项静态扫描确认未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、独立 MonoBehaviour `Update`、`async void`、直接 `Instantiate` 或新全局事件。
- 点击遮罩接入后执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly`，结果为 0 个错误、1116 个项目/依赖既有警告；目标代码没有新增编译错误。
- 静态资源核对确认 `GuideDialogue.prefab.initialSkinName=box_rich01`；`Ui_Box.json` 中 `box_rich01` 相比 `box_rich` 仅移除 `rich_decoration_06`，最终无眼镜视觉仍需 PlayMode 确认。

## 剩余风险与未验证项

- 尚未执行 Unity PlayMode。需验证配置等待时间触发、重试次数触发、两个条件同时满足、提示已使用/引导已触发时不再出现、Playing → Pause 后流程继续，以及快速重开/退出时取消。
- 需验证两枚 Finger 的 Canvas 层级与点击位置、GuideDialogue 的 Top 层表现、0.5 秒节奏、长文本/多语言换行，以及 Normal/PassLevel prefab 的按钮绑定。
- 需验证 GamePlayPanel 点击遮罩在流程启动后立即开启，第一枚后创建的 GuideFinger 位于遮罩之上且可以点击，并在正常完成、失败、异常与 Reset 取消后恢复关闭。
- 三个 GuideDialogue Variant 已按枚举值创建并配置现有剧情文案；仍需在 Unity Inspector/PlayMode 验证资源继承与加载。
- 需验证免费领取不扣金币、不拉广告，仍能正确展示答案；X 显示最后一句，而 NoBtn、直接通关和外部关闭不显示。
- `ToolConsumesCache["Tips"]` 只覆盖已记录的提示消耗。广告、无限金币以及其他未经过提示计数入口的历史提示仍不会留下该记录，因此这些玩家仍可能触发一次本引导；首次免费流程自身由 `HasTriggeredGameTipsGuide` 防重。
- `DB.SaveGameData()` 失败时当前内存仍消费状态，但下次冷启动可能再次触发；运行日志会记录保存失败。
- 尚未进行 Android/iOS 横屏真机触摸与暂停恢复 smoke。
- 若远端和 GlobalConst 均不可用，三个 API 最终返回 0；此时等待时间为 0 会使满足关卡条件的玩家立即达到时间门槛。正常启动链应在进入关卡前完成配置表初始化，异常时仍需 PlayMode 验证实际降级表现。

## 回滚路径

1. 从 `PlayCtrl_Playing` 删除 `UpdateGameTipsGuide()` 调用与局部检查状态。
2. 从 `PlayCtrl` 删除 Flow 持有、启动和取消逻辑，并从 `GamePlayPanel` 删除目标节点与首次免费弹窗适配入口。
3. 从 `GameTipsPopup` 删除首次免费状态/API，恢复 `TryToUseCoins()`、`RefreshTips()` 和 `Close()` 原行为。
4. 从 `GameData` / `GameData_Partial` 删除新增的 `HasTriggeredGameTipsGuide` 布尔字段，并从流程中移除对应读写。
5. 删除 `GameTipsGuideFlow.cs` 的流程实现或恢复空壳；将 `GuideDialogue` 恢复原 UI 层级，并把 prefab 初始皮肤恢复为 `box_rich`。
6. 从 `ConfigManager.RemoteConfig` 删除三个 GameTipsGuide 字段 key 与读取 API，并移除 Playing 状态中的配置快照传递。

回滚不需要迁移关卡资源、Global/Luban 配置、广告、analytics 或平台 SDK；已写入的 `HasTriggeredGameTipsGuide` 字段在旧代码中会被忽略。

## 2026-07-17 修正 1：前两段文本改用 NormalGuidePopup

### 需求背景与影响范围

- 原流程的开场两句和免费提示说明分别使用 `GuideDialogue_10001`、`GuideDialogue_10002`；本次要求这两段文本改由 `NormalGuidePopup` 展示。
- 修改范围为 `GameTipsGuideFlow.cs`、`NormalGuidePopup.cs` 以及 `NormalGuidePopup_20001.prefab`、`NormalGuidePopup_20002.prefab`。最后一句收尾文本仍使用 `GuideDialogue_10003`，提示按钮、免费领取、答案弹窗、配置、存档和事件入口不变。

### 修改后行为与资源流

- `GameTipsGuideStep.One=20001`、`Two=20002` 分别映射同编号 Resources prefab；`NormalGuidePopup.ShowAsync(GameTipsGuideStep, CancellationToken)` 复用既有 Bear UI 打开、重复实例防护、关闭回调和取消销毁逻辑。
- 流程开始后先等待 `NormalGuidePopup_20001` 关闭，再显示指向提示入口的第一枚 Finger；打开普通提示弹窗并等待 0.5 秒后，再等待 `NormalGuidePopup_20002` 关闭，随后开放首次免费按钮并显示第二枚 Finger。
- `NormalGuidePopup_20001` 保留原开场两句文本，`NormalGuidePopup_20002` 保留原免费提示说明；两个 prefab 的根节点名称已与实际资源编号同步。
- UI 流调整为：`NormalGuidePopup_20001(Top)` → `GuideFinger(GamePlayPanel)` → `GameTipsPopup` → `NormalGuidePopup_20002(Top)` → `GuideFinger(GameTipsPopup)` → `UseTipsEvent` → `GameTipsPopup_3` → 可选 `GuideDialogue_10003(Top)`。
- 本次不新增 DB/save、RemoteConfig、Luban、全局事件、analytics、广告、IAP 或平台分支；Android/iOS 继续共用同一横屏 UI 路径。

### 验证、剩余风险与回滚

- CodeGraph 调用链确认 `GameTipsGuideFlow.ExecuteAsync()` 的前两段文本均进入 `NormalGuidePopup.ShowAsync(GameTipsGuideStep)`，而收尾仍进入 `GuideDialogue.ShowAsync(GameTipsClosing)`；明信片 `PostcardStageStep` 调用方继续复用原等待逻辑。
- 静态 YAML 已核对两个新 prefab 的编号、根节点名称、`tipsKey` 数量和文本；`git diff --check` 通过。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 构建成功，结果为 0 个错误、1116 个项目既有警告。
- Unity PlayMode 尚未验证 Resources 实际加载、逐字动画、快速点击锁、流程取消、两枚 Finger 层级及 0.5 秒节奏；Android/iOS 横屏触摸也未验证。
- 回滚时把流程前两处调用恢复为 `GuideDialogueVariant.GameTipsOpening/GameTipsFree`，删除 `GameTipsGuideStep` 的 `ShowAsync` 重载和 `_20001/_20002` prefab；收尾对话、提示弹窗、存档与配置无需回滚。

## 2026-07-17 修正 2：FreeTips 纳入 Tips 消耗计数

### 需求背景与影响范围

- 原首次免费提示只派发 `UseTipsEvent` 并打开答案弹窗，没有经过 `SimpleBag.RemoveTool(..., CostType.Hint)`，因此不会增加 `ToolConsumesCache["Tips"]`，也不会推进对应消耗成就。
- 本次从 `SimpleBag.RemoveTool(...)` 抽出 `RecordToolConsume(...)` 公共业务 API，并让 `GameTipsPopup.UseFirstGuideFreeHint()` 调用该 API；普通金币提示继续由 `RemoveTool(...)` 转调同一入口，广告提示、无限金币、直接通关、配置、资源和 UI 时序不变。

### 修改后数据、存档与成就流

- `SimpleBag.RecordToolConsume(tool, type)` 统一负责实际道具计数、`Hint` 对 `Tips` 的语义补记、空字典初始化、`DB.SaveGameData()` 和 `ToolConsumes` 成就检查；该 API 不修改 `GameTools` 数量。
- 免费提示通过有效性检查后调用 `RecordToolConsume(GameProps.Tips, CostType.Hint)`，只把既有 `ToolConsumesCache["Tips"]` 增加 `1`，随后由 Popup 派发 `UseTipsEvent` 并打开 `GameTipsPopup_3`。
- `_isFirstGuideFree` 在计数前仍要求为 `true`，消费后立即改为 `false`；引导手指回调和 YesBtn 即使重复进入，也不会再次累计。
- `SimpleBag` 保存失败时保留当前内存中的计数并记录 Bear Logger 警告，免费提示继续发放；冷启动后的持久计数仍存在未保存风险。
- 本次不扣金币、不增加 `ToolConsumesCache["Coin"]`，不新增存档字段、全局事件、analytics、广告、IAP、RemoteConfig 或平台分支。

### 验证、剩余风险与回滚

- CodeGraph 确认免费提示只有 Flow 回调与该模式 YesBtn 两个调用入口，二者最终都进入同一个 `UseFirstGuideFreeHint()`；Popup 只调用 `SimpleBag.RecordToolConsume(...)`，计数、保存和成就检查均由 SimpleBag 持有。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 构建成功，结果为 0 个错误、98 个项目既有警告；最终 diff 检查结果见本次交付记录。
- Unity PlayMode 尚未验证首次值、新增值、重复点击、保存失败以及成就阈值命中；Android/iOS 的成就服务实际行为也未验证。
- 回滚时让 `RemoveTool(...)` 恢复内联计数，并删除 `SimpleBag.RecordToolConsume(...)` 与 `UseFirstGuideFreeHint()` 的调用；已经写入玩家存档的次数不会自动回退，如需回退必须另行设计存档迁移。

## 2026-07-22 修正 3：开场 NormalGuidePopup 使用 box_rich

### 需求与行为

- `GameTipsGuideFlow` 打开 `GameTipsGuideStep.One` 时向 `NormalGuidePopup.ShowAsync(...)` 明确传入 `box_rich`，不再只依赖 prefab 保存的初始 Skin。
- `NormalGuidePopup` 的 GameTips 等待式重载新增可选 `skinName`，默认仍为 `box_original`；弹窗创建后复用 `SetSpineSkin(...)` 应用目标 Skin。
- Skin 设置失败时保留 prefab 当前外观并继续等待关闭，不因装饰表现失败中断首次提示引导。明信片引导重载、提示按钮、免费提示、存档、配置与事件链不变。

### 验证、风险与回滚

- 静态调用链确认 `GameTipsGuideFlow.ExecuteAsync()` 传入 `box_rich`，并经 GameTips 重载、私有等待实现和 `CreateByPrefabName(...)` 到达实例 `SetSpineSkin(...)`。
- `git diff --check` 与敏感生命周期扫描通过；`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 构建通过，结果为 0 个错误、99 个项目既有警告。
- Unity PlayMode 的 `box_rich` 实际外观、动画切换后 Skin 保持和 Android/iOS 横屏仍待验证。
- 回滚时删除 `OpeningPopupSkinName` 参数，并移除 GameTips 重载到 `SetSpineSkin(...)` 的转发；prefab、Spine 资源、配置和数据无需回滚。

## 2026-07-22 修正 4：取消开场 box_rich Skin 覆盖

### 需求背景、影响范围与当前行为

- 原行为：`GameTipsGuideFlow` 打开 `GameTipsGuideStep.One` 时显式传入 `box_rich`，弹窗创建后会调用 `NormalGuidePopup.SetSpineSkin(...)` 覆盖 prefab 当前外观。
- 新行为：流程改为传入 `skinName: null`，`CreateByPrefabName(...)` 因此不再调用 Skin 设置入口，开场弹窗保留对应 prefab 自身的当前外观。
- 本次只修改 `Assets/Game/Scripts/Guide/Scripts/Flow/GameTipsGuideFlow.cs` 及本说明；`NormalGuidePopup` 公共 API、明信片引导、Spine/prefab 资源、提示按钮、免费提示、存档、配置、事件、analytics 与平台分支均不变。
- UI 流仍为 `NormalGuidePopup_20001` → 第一枚 Finger → 免费提示弹窗；仅取消开场弹窗的 `box_rich` 外观覆盖，不改变流程顺序、等待和关闭结果。

### 验证、剩余风险与回滚

- 静态检查确认 `GameTipsGuideFlow.cs` 已无 `OpeningPopupSkinName` 和 `box_rich`，开场调用显式传入 `skinName: null`；其余四个明信片 `NormalGuidePopup.ShowAsync(...)` 调用点未改动，且未新增编辑器生命周期、实例化或 `async void` 路径。
- `git diff --check` 通过，仅输出仓库既有 LF/CRLF 转换提示；`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 构建通过，结果为 0 个错误、99 个项目既有警告。
- Unity PlayMode、Android/iOS 横屏尚待验证开场 prefab 当前外观、动画播放及完整引导节奏；本次没有资源或平台代码变化。
- 回滚方式：恢复 `OpeningPopupSkinName = "box_rich"`，并把开场调用第三个参数恢复为该常量；无需迁移数据或资源。

## 2026-07-22 修正 5：等待门槛改用单次尝试时间

### 需求背景、原行为与修改后行为

- 原行为：`GameTipsGuideFlow.HasReachedTrigger(...)` 使用 `CurrentLevelTimeSeconds` 判断等待门槛，同一关卡重开、失败重试或 Replay 后仍保留此前尝试的累计时间。
- 新行为：等待门槛改为读取 `CurrentAttemptTimeSeconds`，与原有 HintBubble 的计时语义一致；重开、失败重试或 Replay 调用 `ResetAttemptPlayTime()` 后从 `0` 重新累计。
- 时间仍由 `PlayCtrl_Playing.OnUpdate()` 传入 `Time.deltaTime` 累计，受 `Time.timeScale` 影响；重试次数门槛仍使用 `ResetCount + FailCount`，两项条件保持任一满足即可触发。
- 本次只修改 `GameTipsGuideFlow.cs` 的只读门槛判断及本说明；RemoteConfig 字段与单位、存档、UI、资源、事件、analytics 和 Android/iOS 平台分支均不变。

### 验证、剩余风险与回滚

- CodeGraph 调用链确认普通重开、失败重试和 Replay 均在新一轮尝试前调用 `ResetAttemptPlayTime()`，该方法清零 `CurrentAttemptTimeSeconds`；`HasReachedTrigger(...)` 只有 `PlayCtrl_Playing` 一个外部调用点。
- 已执行 C# 工程构建与差异检查，结果见本次交付记录。
- Unity PlayMode 尚未验证等待临界帧、暂停恢复、重开、失败重试、Replay 及“时间门槛/次数门槛任一成立”的实际流程；Android/iOS 横屏真机仍未验证。
- 回滚方式：将 `HasReachedTrigger(...)` 的时间比较字段恢复为 `CurrentLevelTimeSeconds`；无需迁移配置、存档或资源。

## 2026-07-22 修正 6：角色进门后不再启动首次提示引导

### 需求背景、原行为与修改后行为

- 原行为：`BaseLevelCtrl.OnActorTrigger2D(...)` 在角色进入成功门时先把 `IsActorEnterDoor` 置为 `true`，但 `PlayCtrl_Playing` 在成功状态切换完成前仍会执行首次提示引导门槛检查；若等待时间或重试次数恰好达标，`PlayCtrl.TryStartGameTipsGuide(...)` 仍可能创建并提交 `GameTipsGuideFlow`。
- 新行为：`PlayCtrl.TryStartGameTipsGuide(...)` 在统一启动边界读取当前 `LevelCtrl.IsActorEnterDoor`；角色已经进门时直接返回 `false`，不创建、不提交 `GameTipsGuideFlow`，也不会写入首次提示引导已触发存档状态。
- 角色未进门时的等待时间、重试次数、已使用提示、引导存档、UI 引用与流程排队判断保持原样；已在角色进门前启动的引导不属于本次取消范围。
- 影响文件为 `Assets/Game/Scripts/Play/PlayCtrl.cs` 及本说明；未修改 `BaseLevelCtrl`、关卡 prefab、RemoteConfig、资源、事件、提示消费、analytics 或平台分支，Android/iOS 共用相同运行时代码路径。

### 验证、剩余风险与回滚

- CodeGraph 调用链确认 `PlayCtrl_Playing.UpdateGameTipsGuide()` 是当前唯一外部启动调用点，最终统一进入 `PlayCtrl.TryStartGameTipsGuide(...)`；`BaseLevelCtrl.OnActorTrigger2D(...)` 与 `SuccessStrightly()` 都会在成功检查前设置 `IsActorEnterDoor = true`。
- `git diff --check -- Assets/Game/Scripts/Play/PlayCtrl.cs` 通过，仅输出仓库既有 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 构建通过，结果为 0 个错误、99 个项目既有警告。
- Unity PlayMode 尚未验证“门槛达标与进门相邻帧”“第 23 关直接成功入口”和普通未进门触发路径；Android/iOS 横屏真机仍未验证。当前结论限于静态调用链与 C# 编译证据。
- 回滚方式：删除 `PlayCtrl.TryStartGameTipsGuide(...)` 中对 `LevelCtrl.IsActorEnterDoor` 的启动守卫；无需迁移配置、存档或资源。

## 2026-07-23 修正 7：首次提示引导仅限前 10 关

### 需求背景、原行为与修改后行为

- 需求：`GameTipsGuideFlow` 只允许在前 10 关触发，第 10 关包含在允许范围内，第 11 关及以后不再触发。
- 原行为：`HasReachedTrigger(...)` 只检查 `LevelRuntimeState.LevelId` 是否达到 RemoteConfig 配置的最低关卡；达到最低关卡后，任意更高关卡都可能因单次尝试等待时间或重试次数达标而触发。
- 新行为：`GameTipsGuideFlow` 增加本地上限 `MaximumGuideLevelId = 10`。`HasReachedTrigger(...)` 先要求当前 `LevelId` 同时满足“达到配置最低关卡”与“不大于 10”，再判断等待时间或 `ResetCount + FailCount` 门槛。
- `PlayCtrl_Playing.UpdateGameTipsGuide()` 的首次检查与 `GameTipsGuideFlow.ExecuteAsync()` 的启动前二次检查都复用 `HasReachedTrigger(...)`，因此第 11 关及以后不会提交或执行流程，也不会写入 `HasTriggeredGameTipsGuide`。
- 影响文件仅为 `Assets/Game/Scripts/Guide/Scripts/Flow/GameTipsGuideFlow.cs`、本说明和共享设计记录。原 XML 注释因新增上限后不再完整，已同步补充前 10 关与第 10 关包含边界；未改写其它既有注释。

### 数据、配置、UI、事件与平台边界

- 关卡数据沿用 `PlayCtrl` 调用 `LevelRuntimeState.StartLevel(...)` 时写入的当前关卡 `Id`；本地 Luban 输出证据中第 10 关为 `id=10, level=10`，下一关为 `id=11`。
- RemoteConfig 的 `gameTipsGuideLevel` 继续作为最低关卡，不新增最高关卡远端字段；若最低关卡配置大于 10，则本引导没有可触发关卡。
- 等待时间、重试次数、提示使用记录、`HasTriggeredGameTipsGuide` 存档消费、Normal/PassLevel Popup、GuideFinger、ClickBlock、事件与取消链路均保持原行为。
- 本次不修改 prefab、资源地址、Luban/GlobalConst、DB 结构、analytics、广告、IAP、全局事件或 Android/iOS 平台分支；运行时仅增加一次整数上限比较。

### 验证、剩余风险与回滚

- CodeGraph 与静态调用点检查确认：`PlayCtrl_Playing` 和 `ExecuteAsync()` 都进入同一个带上限的 `HasReachedTrigger(...)`；第 10 关使用包含比较，第 11 关及以后返回 `false`。
- `git diff --check` 对目标代码通过，仅输出仓库行尾转换提示；敏感项扫描确认本次未新增 `OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、`async void`、实例化或事件派发路径。
- C# 构建未完成验证：首次 `--no-restore` 构建因缺少 `Temp/obj/Assembly-CSharp/project.assets.json` 停止；允许还原后进入编译准备，但因 Unity 尚未生成 `Temp/bin/Debug` 下的 191 个依赖程序集而停止。该结果不能证明 Unity 编译通过。
- 尚未执行 Unity PlayMode 与 Android/iOS 横屏真机验证。需在未使用过提示且引导存档未消费的测试存档中分别验证第 10 关仍能按时间/次数门槛触发、第 11 关即使门槛达标也不触发，并回归配置最低关卡以下不触发。
- 回滚方式：删除 `MaximumGuideLevelId` 常量以及 `HasReachedTrigger(...)` 中的关卡上限比较，恢复只按配置最低关卡判断；无需迁移配置、存档或资源。

## 2026-08-19 修正 8：Pause 状态不启动首次提示引导

### 需求背景、原行为与修改后行为

- 需求：当前主线 `PlayCtrl` 处于 `PAUSE` 时，不应触发 `GameTipsGuideFlow`。
- 原行为：`PlayCtrl_Playing.UpdateGameTipsGuide()` 只在 Playing 节点内提交流程，但 `GuideFlowManager` 会串行排队；流程取得执行权时没有重新检查玩法状态，因此等待期间切到 Pause 后仍可能开始流程，并提前写入 `HasTriggeredGameTipsGuide`。
- 修改后：`PlayCtrl.TryStartGameTipsGuide(...)` 只在当前状态仍为 `PLAYING` 时创建并提交流程；`GameTipsGuideFlow.ExecuteAsync(...)` 在取得集中执行权后再次要求 `PlayCtrl` 仍为 `PLAYING`，否则返回 `Failed`，不打开引导 UI、不写入触发存档。
- 状态门禁仅约束流程首次开始。流程已在 Playing 状态正式开始后，玩家按引导点击 Tips 导致的 Playing → Pause 仍按既有设计继续，避免中断首次免费提示后半段。

### 影响边界与数据流

- 影响文件：`Assets/Game/Scripts/Play/PlayCtrl.cs`、`Assets/Game/Scripts/Guide/Scripts/Flow/GameTipsGuideFlow.cs` 及本说明。
- 启动流调整为：Playing 门槛满足 → `TryStartGameTipsGuide(...)` 首次状态检查 → `GuideFlowManager` 取得执行权 → `ExecuteAsync(...)` 二次状态检查 → 资格复核与 `HasTriggeredGameTipsGuide` 消费。
- 未修改 Pause 状态节点、通用 GuideFlow 队列、GamePlayPanel、提示弹窗、RemoteConfig、存档结构、事件、资源、analytics、广告、IAP 或平台分支；Android/iOS 共用相同运行时状态门禁。

### 验证、剩余风险与回滚

- CodeGraph 确认 `UpdateGameTipsGuide()` 是唯一外部启动点，`GuideFlowBase.RunAsync()` 会先等待 `GuideFlowManager.EnterAsync()`，因此执行前二次检查覆盖了排队期间发生的状态变化。
- `git diff --check` 对目标代码与文档通过，仅输出仓库既有 LF/CRLF 转换提示；`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 构建通过，结果为 0 个错误、106 个项目既有警告。
- Unity PlayMode 尚需验证：直接 Pause 时不触发、排队期间切到 Pause 时不触发、恢复 Playing 后按现有门槛重新判断，以及流程开始后点击 Tips 进入 Pause 仍能完成免费提示。
- Android/iOS 横屏真机暂停与触摸流程尚未验证，结论限于本地代码链和构建证据。
- 回滚时删除上述两个 `PLAYING` 状态守卫，并移除 `GameTipsGuideFlow.cs` 的 `Game.Play` 命名空间引用；无需迁移配置、存档或资源。

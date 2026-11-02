# DailyChallengeBtn 功能记录

## 需求与背景

- 每日挑战入口需要复用在设置、暂停和胜利界面，公共状态由 `DailyChallengeManager` 持有。
- 入口点击只展开或收起阶段进度气泡，不打开 `DailyChallengePopup`，也不存在详情入口。
- 奖励进度按阶段显示。以累计节点 `[4, 8]` 为例，累计进度 6 对应第二阶段 `2/4`，不是 `6/8`。
- `DailyChallengePopup` 的唯一自动入口现已由 `PlayCtrl_Success` 编排，顺序为 `RatingPopup -> DailyChallengePopup -> GameVictoryPanel`；Btn 仍不会主动打开 Popup。

## 影响文件与系统

- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeBtn.cs`
  - 从独立 `BaseUIView` 调整为可嵌入宿主 prefab 的 `MonoBehaviour`。
  - 增加入口刷新、展开/收起、阶段文字、进度条和领取勾选刷新。
- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeStepItem.cs`
  - 新增阶段奖励纯展示组件，统一刷新目标次数、已领奖底板和勾选。
  - 不读取配置、存档或 Manager，也不自行判断领取条件，状态由宿主传入。
- `Assets/Game/UI/DailyChallenge/Prefabs/DailyChallengeBtn.prefab`
  - 复用已有 `clickArea`，补齐入口脚本、两个进度前景、三个勾选、两个领奖背景和内容根节点绑定。
  - 移除独立 Canvas、CanvasRenderer 和 GraphicRaycaster，由宿主 Canvas 统一渲染和处理输入。
  - 两个阶段节点改为 `DailyChallengeStepItem.prefab` 的嵌套实例，以 `0.7` 缩放保留 Btn 的紧凑尺寸和现有排版。
  - Btn 支持显式绑定两个 Item；引用为空时从 `ProcessBubble` 的子节点中查找，并按横向位置从左到右映射第一、第二阶段，避免 prefab 重序列化后丢失功能。
- `Assets/Game/UI/DailyChallenge/Prefabs/DailyChallengeStepItem.prefab`
  - 在用户抽出的公共预制体根节点挂载 `DailyChallengeStepItem`，绑定目标文本、领奖底板和勾选。
  - `DailyChallengePopup.prefab` 中已有的两个实例自动继承该展示组件；第二个实例继续通过 prefab override 使用 Skin 图标。
- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeManager.cs`
  - 改为由 `GameManager` 持有的普通 C# 管理器。
  - 提供只读 `DailyChallengeViewState`，集中处理开启、解锁、阶段拆分和领取状态。
  - 实现 `IScheduleRefreshable`，负责当地自然日刷新时清空进度和领取记录。
- `Assets/Game/Scripts/GameManager.cs`
  - 初始化、持有并释放 `DailyChallengeManager`。
- `Assets/Game/Scripts/Schedule/Modules/DailyScheduleModule.cs`
  - 注册 `GameManager.DailyChallenge`，复用已有当地自然日刷新链。
- `Assets/Game/DB/GameSchedule_Partial.cs`
  - 初始化并公开当日累计进度和已领取奖励 id 列表。
- `Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs`
  - 增加每日挑战开关、累计节点和解锁关卡读取入口；运行时优先远程值，回退 GlobalConst。

## 原有行为基线

- `DailyChallengeBtn` 原为无业务逻辑的 `BaseUIView`，通过 `UIManager` 在 `UILayer.Normal` 打开。
- prefab 根节点带独立 World Space Canvas，已被设置界面作为嵌套 prefab 使用，但多项生成字段为空。
- `DailyChallengeManager` 原为带空 `Start/Update` 的 `MonoBehaviour`，没有 GameManager 持有、快照或每日刷新逻辑。
- `GameSchedule` 已声明累计进度和领取 id 字段，但 `Init()` 与公开访问器尚未接入。

## 新行为

- 宿主启用入口时，Btn 在运行态向 `GameManager.DailyChallenge` 请求只读快照；功能关闭、配置无效、存档未就绪或未达到解锁关卡时只隐藏内容节点。
- 每次启用默认收起气泡；点击 `clickArea` 仅切换 `ProcessBubble`，不创建任何 Popup。
- 紧凑进度条显示当前阶段比例；展开长进度条把两个阶段各映射为一半，进入第二阶段后保留第一段完成状态并从第二段继续填充。
- current 数字显示当日累计 `TotalProgress`；两个奖励目标分别显示累计配置目标 `step[0]` 和 `step[1]`。紧凑进度条仍按第二阶段增量 `step[1] - step[0]` 计算当前阶段比例。
- 完成总目标时显示入口完成勾；阶段奖励勾与背景根据 `DailyChallengeHasRewardId` 中的阶段 id 1/2 显示。
- 两个阶段节点统一调用 `DailyChallengeStepItem.Refresh(targetCount, isRewardClaimed)`；Btn 与 Popup 复用同一预制体和展示规则，奖励图标、位置与缩放仍由各自实例覆盖。
- 没有 `Update`、全局事件、运行时实例化或 Btn 直接 DB/配置读取。

## 数据与生命周期流

```text
GlobalConst / RemoteConfig
        +
GameSchedule 当日累计进度、领取 id
        -> DailyChallengeManager.GetViewState()
        -> DailyChallengeBtn.Refresh()
        -> DailyChallengeStepItem.Refresh()
        -> 阶段目标、领奖底板与勾选
        -> Btn 进度条、总完成勾与显示状态

DailyScheduleModule
        -> DailyChallengeManager.OnScheduleRefresh()
        -> 清空累计进度与领取 id
        -> DB.SaveGameSchedule()
```

Btn 的 `OnEnable`、`Refresh` 在最外层使用 `Application.isPlaying` 防护，编辑器预览不会访问 DB、配置或 Manager。

## 验证证据

- `dotnet build Assembly-CSharp.csproj --no-restore --nologo`：新增 Item 后再次通过，0 个错误、99 个项目既有警告。
- `git diff --check`：C# 与中文文档未报空白错误；用户更新后的 Unity prefab 含 5 处空值序列化尾随空格，未做会被 Unity 重写的格式性调整。
- 静态扫描确认 Btn/Manager 未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`Update`、`PlayEvents`、事件派发或 `DailyChallengePopup.Create` 调用。
- 静态 YAML 检查确认脚本新增序列化字段均有非零 prefab 引用，已移除的 Canvas 组件 fileID 不再残留。
- 以用户更新后的 Btn prefab 为准，静态 YAML 引用闭合检查确认 `DailyChallengeStepItem.prefab`、`DailyChallengeBtn.prefab`、`DailyChallengePopup.prefab` 均无重复 fileID 或缺失本地引用。

## 剩余风险与未验证项

- 尚未在 Unity PlayMode 验证设置界面中的位置、触摸命中、气泡遮挡、横向填充方向和不同宽高比适配。
- 尚未在 Android/iOS 横屏设备验证触控和宿主 Canvas 层级。
- 通关累加、阶段奖励、随机 Skin 和 `DailyChallengePopup` 自动结算已在后续实现；详细链路见 `DevelopMD/DailyChallengePopup_FeatureRecord.md`。
- Btn 仍只读取 Manager 快照并展示，不承担结算、领奖或 Popup 打开职责。
- Btn 的自动识别约定两个阶段在 `ProcessBubble` 内按 X 轴从左到右排列；若后续改为竖排、反向或增加第三个阶段，应改为显式绑定或扩展阶段标识。
- 暂停界面和两个 VictoryPanel prefab 尚未嵌入该入口；本次仅保留可复用 prefab 和设置界面已有实例。
- `GlobalConst` 节点必须至少包含两个正数且严格递增；无效配置会隐藏入口并等待配置修复。

## 回滚路径

- 恢复 `DailyChallengeBtn.cs` 的原 `BaseUIView` 空骨架和 prefab 独立 Canvas/原绑定。
- 若仅回滚本次 Item 抽取，可让 Btn 恢复直接刷新两个目标文本和四个领奖节点，并移除 `DailyChallengeStepItem.cs/.meta` 及两个 prefab 上对应组件；Popup 原有视觉层级可继续保留。
- 从 `GameManager` 与 `DailyScheduleModule` 移除 DailyChallengeManager 的持有和注册。
- 移除本次增加的 RemoteConfig 读取方法及 `GameSchedule_Partial` 访问器；`GameSchedule.cs` 中原始序列化字段可保留，不影响旧流程。
- 回滚不需要迁移存档，因为本次没有改变已有序列化字段名称和类型。

## 2026-07-20：无效代码清理

### 清理内容与行为边界

- 删除 `OnEnable()` 中与 `ApplyViewState()` 重复的 `ResolveStepItems()` 调用；入口可见时仍在刷新状态前完成 Item 解析，入口隐藏时不再做无效查找。
- `SetExpanded(...)` 在仓库内没有外部调用，收回为私有方法；点击展开、隐藏时收起和重新启用默认收起的行为不变。
- 移除只有一个调用点的 `SetActive(...)` 包装，完成勾的空引用保护和显隐条件不变。
- 未手工修改自动生成的 `DailyChallengeBtn.Generated.cs`；当前 Btn prefab 未显式绑定两个 StepItem，因此运行时按横向位置解析的兜底逻辑继续保留。

### 影响范围、验证与回滚

- 仅修改 `DailyChallengeBtn.cs` 和本记录；不修改 Prefab、Manager、存档、配置、Popup、奖励、事件或宿主接入。
- 已执行 C# 工程构建、调用点扫描和差异格式检查；Unity PlayMode 与 Android/iOS 横屏实际交互仍待验证。
- 回滚时恢复 `OnEnable()` 的提前解析、`SetExpanded(...)` 的 public 声明及单调用点显隐助手即可，不涉及数据迁移。

## 2026-07-20：Bubble 展开缩放动画

### 需求与新行为

- 原 Bubble 展开时直接 `SetActive(true)`，没有入场反馈；现在激活后仅将 X 轴从 `ScaleX 0` 补间到 `ScaleX 1`，Y、Z 轴保持原值。
- 动画时长为 0.2 秒，使用 `Ease.OutBack` 和 unscaled time；收起仍立即隐藏，不增加退出等待。
- 快速重复点击、组件禁用或重新启用时会停止旧 Tween 并恢复 `ScaleX 1`，避免 Bubble 保持零缩放或中间值，同时不改动 Y、Z 轴。

### 影响范围、验证与回滚

- 仅修改 `DailyChallengeBtn.cs` 的本地 Bubble 表现；不修改 Prefab、Manager、存档、配置、Popup、奖励、事件或宿主点击区域。
- 已执行 C# 工程构建、Tween 生命周期静态检查和差异格式检查；Unity PlayMode 与 Android/iOS 横屏下的点击节奏仍待验证。
- 回滚时移除 Bubble Tween、时长常量和禁用清理，恢复 `SetExpanded(...)` 直接切换 active 即可。

## 2026-07-20：current 数字改为累计进度

- Btn 的 current 数字由阶段内 `CurrentStageProgress` 改为当日累计 `TotalProgress`，跨入第二阶段后不再从 0 重新显示。
- 紧凑进度条继续使用 `CurrentStageProgress / CurrentStageTarget`，因此仅数字口径改变，不影响阶段填充比例、领奖状态、存档或配置。

## 2026-07-20：StepItem count 常显

- `DailyChallengeStepItem` 在普通 `Refresh(...)` 和 `PlayClaimedAnimation(...)` 两个入口均显式激活 `targetCountText`，已领取底板及对勾出现时不再允许 count 被隐藏。
- Btn 与 Popup 复用同一行为；本次不修改目标数字内容、阶段进度、领奖判定、Prefab、存档、配置或事件。
- 已执行 C# 工程构建与差异格式检查；Unity PlayMode 下已领取状态的层级视觉仍待验证。回滚时移除两个入口对 `targetCountText.gameObject` 的激活调用即可。

## 2026-07-22：StepItem 已领奖图标灰化

### 需求、基线与新行为

- 原行为仅显示已领奖底板和勾选，`mainIcon` 始终沿用 prefab 的原始颜色；现在 `Refresh(..., true)` 与 `PlayClaimedAnimation(...)` 都会把主图标 RGB 设为 `0.3`，未领奖刷新恢复该实例首次读取到的原始颜色与透明度。
- `DailyChallengeStepItem.prefab` 的 `mainIcon` 已静态确认绑定为 `Image`；Btn 与 Popup 的嵌套实例共同复用该显示规则，图标 Sprite、位置、缩放和领奖动画保持不变。

### 影响范围、数据流与验证

- 仅修改 `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeStepItem.cs` 的本地 UI 颜色；领取状态仍由宿主传入，不新增配置、存档、资源加载、事件、分析或平台分支。
- 已完成 prefab 绑定与调用入口静态检查；`dotnet build Assembly-CSharp.csproj --no-restore --nologo` 通过（0 个错误、1117 个项目既有警告），目标差异格式检查通过。Unity PlayMode 下金币/Skin 图标的实际灰度观感及 Android/iOS 横屏显示仍待验证。

### 风险与回滚

- 当前实现依赖 `mainIcon` 同节点存在 `Image`；若后续改为其他 `Graphic` 类型，颜色逻辑会安全跳过但不会灰化。
- 回滚时移除 `ApplyClaimedIconColor(...)`、对应缓存字段与 `UnityEngine.UI` 引用即可，不涉及 prefab 或数据迁移。

## 2026-07-22：Bubble StepItem 按 goals 比例同步

### 需求与行为

- 原 Bubble 长进度条固定把两阶段各映射为 50%，StepItem 也使用 prefab 固定位置；当前本地累计 goals `[4,10]` 的第一节点因此显示在 50%，而不是目标对应的 40%。
- Btn 应用 `DailyChallengeViewState` 时，现在使用展开进度条父 RectTransform 作为轨道，将第一 StepItem 放到 `FirstStageTarget / SecondStageTarget`，第二 StepItem 放到 100%；长进度填充同步改为累计进度占最终目标的比例。
- 紧凑进度条仍显示当前阶段比例，Bubble 展开/收起动画、目标数字、已领奖状态和 prefab 的 Y/缩放/旋转保持不变。位置刷新仍由 `OnEnable()` 或宿主显式 `Refresh()` 触发，不做逐帧轮询。

### 影响、验证与回滚

- 复用 `DailyChallengeStepItem.SyncHorizontalPosition(...)`；修改 Manager、Btn、Popup 与 StepItem 脚本，不修改 `DailyChallengeBtn.prefab`、配置、存档、奖励、资源、事件或平台分支。
- C# 工程构建通过（0 个错误、1117 个项目既有警告），目标差异格式与调用点静态检查通过。Unity PlayMode 的 Bubble 视觉位置、不同远端 goals 与 Android/iOS 横屏仍待验证。
- 回滚方式与完整数据流见 `DevelopMD/DailyChallengePopup_FeatureRecord.md` 的同名日期章节；脚本回滚不需要 prefab 或存档迁移。

### 2026-07-22 坐标系偏移修正

- 用户截图确认初版第一节点落在长条末端、第二节点越出 Bubble。原因不是 goals 比例，而是把父 pivot 坐标系下的换算结果错误写入 Item 的 `anchoredPosition.x`；`ProcessBubble` 左 pivot 与 Item 中心 anchor 使 Btn 额外右移约半个 Bubble 宽度。
- 公共同步 API 已改写 `localPosition.x`，Popup 与 Btn 因而使用同一种坐标语义；保留 prefab 的 Y、缩放、旋转和展开动画，也不新增逐帧刷新。
- 修正后 C# 构建通过（0 个错误、99 个项目既有警告）。Unity PlayMode 与移动端横屏的最终视觉仍待复验；完整根因、影响和回滚见 `DevelopMD/DailyChallengePopup_FeatureRecord.md`。

## 2026-07-23：PassedLevels 为空时的入口解锁修复

### 需求、基线与修复行为

- `DailyChallengeBtn.OnEnable()` 会通过 `DailyChallengeManager.GetViewState()` 无参刷新入口。原 `IsUnlocked(...)` 在没有本次完成关卡参数时只扫描 `DB.GameData.PassedLevels`，列表为空会直接返回未解锁。
- 本地调用顺序显示胜利面板在 `LevelCtrl.Victory()` 写入本关 `PassedLevels` 前创建；调试跳关等情况下也可能出现当前关卡已经达到解锁值、`PassedLevels.Count == 0` 的状态，因此 Btn 刷新会错误隐藏内容。
- 修复后，Manager 在存档可用时先判断 `GameData.CurrentLevel >= dailyChallengeUnlockLevel`，满足即视为已达到解锁关；当前关卡不足时仍保留原 `PassedLevels` 扫描，使已推进玩家重玩低关时继续保持解锁。

### 影响范围与数据流

- 仅修改 `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeManager.cs` 的本地解锁判断；Btn、Popup、Prefab、RemoteConfig 字段、GameData/GameSchedule 存档结构、每日进度、领奖、广告、事件和资源流程均未修改。
- 刷新链仍为 `DailyChallengeBtn.Refresh() -> DailyChallengeManager.GetViewState() -> TryGetRuntimeContext() -> IsUnlocked()`；不新增逐帧读取、实例化或编辑器生命周期入口。
- `DB.GameData` 不可用时仍返回未解锁；功能关闭、阶段配置无效或 `DB.GameSchedule` 未就绪时仍返回隐藏状态。

### 验证、风险与回滚

- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 个警告、0 个错误。
- 静态检查确认修改只影响 `IsUnlocked(...)`，原 `completedLevel` 判定与 `PassedLevels` 兜底均保留。尚未在 Unity PlayMode 验证空 `PassedLevels`、当前关卡等于/低于解锁值、低关重玩及 Android/iOS 横屏宿主刷新。
- 回滚时移除 `GameData.CurrentLevel` 的提前判断并恢复直接读取 `DB.GameData?.PassedLevels` 即可，不涉及存档或配置迁移。

## 2026-07-23：VictoryPanel 星星动画结束后自动展开 Bubble

### 文档依据与行为边界

- 以《每日挑战功能需求分析.xlsx》的“功能需求”工作表为准：
  - `REQ-005`：每日挑战未全部完成且胜利页星星动画结束后，自动展开每日挑战气泡。
  - `REQ-011`：当天两档奖励均完成时不再自动展开，入口保留完成状态。
  - `REQ-014`：VictoryPanel 入口支持自动与手动展开，不能影响原胜利流程。
  - `REQ-016`：点击入口继续切换 Bubble 展开/收起。
- `DailyChallengeBtn.OnEnable()` 仍先收起并刷新，不改成界面启用时立即展开；自动展开只由 VictoryPanel 在明信片星星表现阶段结束后触发。
- 有待播放星星时，`Postcard_VictoryStarGroupCtrl.Refresh(...)` 的完成回调解除原点击阻塞并尝试展开；没有待播放星星或未绑定星星控制器时，视为该表现阶段已经结束并立即尝试展开。
- 自动尝试会重新读取 `DailyChallengeViewState`。仅当入口可见且 `IsCompleted == false` 时展开；功能关闭、未解锁、状态无效或已全部完成时保持收起。
- 手动点击仍由 `DailyChallengeBtn.OnEntryClick(...)` 切换状态，Btn 不打开 `DailyChallengePopup`、不结算或发放奖励。

### 影响范围与数据流

- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeBtn.cs`
  - 新增 `TryExpandIfIncomplete()`，集中刷新当前状态并执行未完成条件判断。
- `Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel.cs`
  - 在明信片星星动画结束边界调用 Btn；普通 `GameVictoryPanel` 与继承它的 `GameVictoryPanel_Reawrd` 共用该逻辑。
  - 面板关闭或禁用造成星星动画回调时，会通过运行态、激活状态和 `IsOpen` 守卫跳过展开。
- 调用流：

```text
GameVictoryPanel.Create(...)
    -> RefreshPostcardVictoryStars(...)
    -> 有星星动画：等待 Postcard_VictoryStarGroupCtrl 完成回调
       无星星动画：直接进入完成边界
    -> DailyChallengeBtn.TryExpandIfIncomplete()
    -> DailyChallengeManager.GetViewState()
    -> 可见且未全部完成：展开 Bubble
       不可见或已全部完成：保持收起
```

- 本次不修改 VictoryPanel/DailyChallenge prefab、存档结构、RemoteConfig/Luban 配置、奖励发放、资源加载、分析、平台 SDK 或全局事件。

### 验证、风险与回滚

- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，99 个项目既有警告、0 个错误。
- 目标 C# 差异格式检查通过；CodeGraph 确认新增公共入口只有 VictoryPanel 调用，普通/奖励胜利页共用基类创建和刷新链。
- 尚未在 Unity PlayMode 验证有星动画、无星动画、挑战已完成、入口隐藏、动画中关闭面板和手动二次收起；Android/iOS 横屏下的 Bubble 遮挡和触控仍待验证。
- 回滚时删除 `GameVictoryPanel.RefreshPostcardVictoryStars(...)` 中的自动展开调用及 `TryExpandDailyChallengeBubble()`，并删除 `DailyChallengeBtn.TryExpandIfIncomplete()`；无需修改 prefab、存档或配置。

## 2026-07-23：使用持久解锁标记统一入口与进度门禁

### 需求、基线与修改后行为

- 原行为由 `DailyChallengeManager.IsUnlocked(...)` 在每次刷新时扫描 `GameData.CurrentLevel`、`PassedLevels` 或本次完成关卡，入口与进度没有独立的永久解锁状态。
- `GameData` 新增 `hasUnlockDailyChallenge`，默认值为 `false`，并通过 `HasUnlockDailyChallenge` 公开给 Manager；该状态不随每日刷新重置。
- `PlayCtrl_Success.OnEnter()` 在启动胜利异步结算前调用 `DailyChallengeManager.TryUnlock(...)`。当本次成功通关关卡序号大于或等于 `dailyChallengeUnlockLevel` 且当前标记为 `false` 时，Manager 写入 `true` 并立即保存 `GameData`。
- 已解锁时不重复保存；未达到阈值时保持 `false`；保存失败时回滚内存标记并记录警告，当前胜利不进入每日挑战进度。
- `TryGetRuntimeContext(...)` 现在要求 `HasUnlockDailyChallenge == true`。只有持久标记有效且功能开关、阶段配置、`GameSchedule` 都有效时，Btn 才显示、胜利进度才结算。

### 影响文件、存档与调用流

- `Assets/Game/DB/GameData.cs`：增加私有序列化字段和中文用途说明。
- `Assets/Game/DB/GameData_Partial.cs`：初始化字段并增加公开属性。
- `Assets/Game/Scripts/Play/PlayCtrl_Nodes/PlayCtrl_Success.cs`：在 SUCCESS 入口先执行一次解锁检测。
- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeManager.cs`：增加 `TryUnlock(int)`，并把运行时门禁改为持久标记。
- 未修改 DailyChallenge Btn/Popup prefab、`GameSchedule` 每日进度、奖励、广告、资源、事件、analytics、IAP 或平台 SDK。

```text
PlayCtrl_Success.OnEnter
    -> DailyChallengeManager.TryUnlock(本次成功通关关卡)
    -> completedLevel >= dailyChallengeUnlockLevel
    -> GameData.HasUnlockDailyChallenge = true
    -> DB.SaveGameData()
    -> TryRecordVictory()
    -> TryGetRuntimeContext()
    -> 持久标记为 true 时记录当日进度

DailyChallengeBtn.Refresh()
    -> DailyChallengeManager.GetViewState()
    -> TryGetRuntimeContext()
    -> 持久标记为 true 时展示入口与进度
```

### 验证、剩余风险与回滚

- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：99 个项目既有警告、0 个错误。
- 目标字段、API、SUCCESS 调用点和两个运行时上下文调用点的静态扫描通过；目标代码差异 `git diff --check` 通过。
- 旧存档没有该字段时按 `false` 处理，不做启动时迁移；玩家后续成功通关一个序号大于或等于当前解锁阈值的关卡后才会持久解锁。若只重玩低于阈值的关卡，入口保持隐藏，这是本次“按当前通关关卡解锁”的明确边界。
- 一旦解锁，即使后续远端阈值提高也保持解锁；这是永久标记语义。远端功能关闭、阶段配置无效或 `GameSchedule` 不可用时仍会隐藏入口并停止进度。
- 尚未在 Unity PlayMode 验证阈值前一关、恰好阈值关、阈值后一关、保存失败回滚、重启持久化、旧存档及 Android/iOS 横屏宿主刷新。
- 回滚时删除 `GameData` 字段/属性/初始化、SUCCESS 入口调用和 `TryUnlock(...)`，再把 `TryGetRuntimeContext(...)` 恢复为原 `CurrentLevel`、`PassedLevels`、本次完成关卡动态判断；已产生的存档布尔字段可由旧版本忽略。

## 2026-07-23：Popup 与 Btn 统一使用双阶段分段进度

### 需求、基线与新行为

- 原长进度条直接使用 `TotalProgress / goal2`，第一阶段节点位于 `goal1 / goal2`；Btn 收起进度条另按当前阶段内部比例显示。Popup、Btn 收起条和 Btn Bubble 因而不是同一显示口径。
- 当前仍只保存一份累计 `DailyChallengeCurrentProcess`，不改变通关累加和奖励达成判断；变化只发生在 UI 归一化比例。
- 两个阶段各占进度条一半：
  - Goal1 阶段：`(TotalProgress / goal1) * 0.5`，单次 Process 增长为 `0.5 / goal1`。
  - Goal2 阶段：`0.5 + ((TotalProgress - goal1) / (goal2 - goal1)) * 0.5`，单次 Process 增长为 `0.5 / (goal2 - goal1)`。
- `DailyChallengeManager` 在 `DailyChallengeViewState` 中统一生成 `ExpandedNormalizedProgress` 和第一阶段 50% 结束位置。Popup、Btn 收起条和 Btn Bubble 都只消费该快照，不再各自根据 goals 推导比例。
- 第一阶段 StepItem 固定同步到 50%，第二阶段固定在 100%；两个 Item 的数字仍显示累计目标 `goal1`、`goal2`。

### 影响范围与 UI 流

```text
dailyChallengeSteps = [goal1, goal2]
        +
DailyChallengeCurrentProcess
        -> DailyChallengeManager.CreateViewState()
        -> Goal1 区间：0% ~ 50%，按 goal1 等分
        -> Goal2 区间：50% ~ 100%，按 goal2 - goal1 等分
        -> DailyChallengePopup 进度条
        -> DailyChallengeBtn 收起进度条与 Bubble 长进度条
```

- 修改 `DailyChallengeManager.cs`、`DailyChallengePopup.cs` 和 `DailyChallengeBtn.cs`；不修改 prefab、配置、存档结构、每日累加、奖励、资源、事件、analytics 或平台分支。
- 为符合新事实，Btn `compactProgressFill` 的旧 Tooltip 从“当前阶段进度”更新为“双阶段分段累计进度”；未改写其他既有注释。

### 验证、剩余风险与回滚

- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：99 个项目既有警告、0 个错误。
- 目标代码 `git diff --check` 通过；静态调用检查确认 Popup、Btn 两种进度条都读取同一个 `ExpandedNormalizedProgress`，两个第一阶段节点都读取同一个 `FirstStageEndNormalizedProgress`。
- 尚未在 Unity PlayMode 验证 Goal1 首次增长、Goal1 达成边界、Goal2 首次增长、最终完成，以及 Popup 动画与 Btn 刷新后的像素位置；Android/iOS 横屏仍待验证。
- 回滚时移除 `FirstStageEndNormalizedProgress` 和分段计算，恢复 `TotalProgress / goal2`、第一节点 `goal1 / goal2`，并把 Btn 收起条恢复为 `CurrentStageNormalizedProgress`；不涉及 prefab、配置或存档迁移。

## 2026-08-04：展开气泡改用两张阶段独立进度图

### 需求、基线与新行为

- 当前 `DailyChallengeBtn.prefab` 的展开气泡已经由一张 `expandedProgressFill` 改为固定布局的 `process1`、`process2` 两张图片；两个 `DailyChallengeStepItem` 的美术位置也改为由 prefab 直接确定。
- 原脚本仍向单张图片写入 `ExpandedNormalizedProgress`，并在每次刷新时按进度条宽度重排两个 StepItem，已与当前 prefab 结构不一致。
- 修改后，收起态 `compactProgressFill` 继续显示两阶段各占 50% 的累计进度；展开态第一张图显示第一阶段内进度，第一阶段达成后保持 `fillAmount = 1`，第二张图在进入第二阶段前保持 `0`，进入后显示第二阶段内进度。
- Btn 不再修改 StepItem 的 Transform；目标数字、领奖勾选和领奖动画继续由 `DailyChallengeStepItem` 负责。

### 影响文件与 UI 数据流

- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeBtn.cs`
- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeManager.cs`
- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengeStepItem.cs`
- `Assets/Game/UI/DailyChallenge/Prefabs/DailyChallengeBtn.prefab`

```text
DailyChallengeViewState
    -> compactProgressFill = ExpandedNormalizedProgress
    -> process1 = 第一阶段内进度或 1
    -> process2 = 0 或第二阶段内进度
    -> StepItem 只刷新目标数字/领奖状态，位置沿用 prefab
```

- `DailyChallengeViewState.FirstStageEndNormalizedProgress` 与 `DailyChallengeStepItem.SyncHorizontalPosition(...)` 已无运行时职责并移除。
- 不改变每日挑战累计 Process、阶段目标、奖励达成、发奖、存档、RemoteConfig、资源加载、事件、analytics 或平台分支。

### 验证、风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore` 通过：0 个错误、0 个警告。
- 目标 `git diff --check` 通过；静态扫描确认旧单进度字段、StepItem 定位调用和定位 API 均无残留。
- Prefab YAML 静态检查确认两张新进度图引用均已声明并正确绑定，75 个本地对象无缺失本地引用候选。
- Unity PlayMode 尚未验证第一阶段首次增长、阶段切换、第二阶段增长和最终完成的实际视觉；Android/iOS 横屏仍为 `uncertain`。
- 回滚时恢复单张 `expandedProgressFill` 绑定、累计进度写入及 StepItem 定位 API；存档和配置无需迁移。

## 2026-08-06：DrawGame 普通与奖励胜利页自动展开气泡

### 需求、基线与新行为

- 用户要求 `DrawGame_GameVictoryPanel` 与 `DrawGame_GameVictoryPanel_Reward` 在星星动画结束后也自动展开每日挑战气泡。
- 修改前，两个 DrawGame 胜利页 prefab 均已嵌入 `DailyChallengeBtn.prefab`，但 `DailyChallengeBtn.OnEnable()` 会先主动收起气泡；DrawGame 的星星动画完成边界只刷新胜利按钮，没有调用 `TryExpandIfIncomplete()`，因此不会自动展开。
- 修改后，`DrawGame_GameVictoryPanel.OnVictoryStarAnimationCompleted()` 在保留原按钮刷新后，按普通 `GameVictoryPanel` 相同的运行态、激活状态与 `IsOpen` 门禁查找子节点 `DailyChallengeBtn`，并调用 `TryExpandIfIncomplete()`。
- `DrawGame_GameVictoryPanel_Reward` 继承该基类，因此普通版与奖励版共用这一入口，不在奖励版重复实现。
- 展开条件保持不变：仅当每日挑战入口可见且当天挑战未全部完成时展开；功能关闭、未解锁、状态无效或已完成时保持收起。

### 影响文件与 UI 流

- `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel.cs`
- `Assets/Game/UI/DrawGame/VictoryPanel/Resources/DrawGame_GameVictoryPanel.prefab` 与 `DrawGame_GameVictoryPanel_Reward.prefab` 仅作为已有宿主证据，本次不修改。
- 不修改 `DailyChallengeManager`、每日挑战配置、存档、奖励、资源加载、事件、analytics 或平台分支。

```text
DrawGame_VictoryStarGroup 动画完成或无动画降级完成
    -> DrawGame_GameVictoryPanel.NotifyVictoryStarAnimationCompleted()
    -> OnVictoryStarAnimationCompleted()
    -> RefreshButtonStates()
    -> DailyChallengeBtn.TryExpandIfIncomplete()
    -> DailyChallengeManager.GetViewState()
    -> 可见且未完成：展开 Bubble
       其它状态：保持收起
```

### 验证、剩余风险与回滚

- 静态调用链确认：普通版与奖励版均通过 `DrawGame_GameVictoryPanel` 基类进入星星动画完成边界；未新增 `Update` 轮询、全局事件、Prefab 绑定或公共 API。
- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：101 个项目既有警告、0 个错误；CodeGraph 与目标调用扫描确认普通版和奖励版共用新增调用，目标差异 `git diff --check` 通过。
- Unity PlayMode 尚未验证有待播星星、无待播星星、入口未解锁、挑战已完成、面板动画期间关闭，以及普通版/奖励版实际展开表现；Android/iOS 横屏下的遮挡与触控仍为 `uncertain`。
- 回滚时删除 `OnVictoryStarAnimationCompleted()` 中的展开调用及局部 `TryExpandDailyChallengeBubble()`；Prefab、存档和配置无需回滚。

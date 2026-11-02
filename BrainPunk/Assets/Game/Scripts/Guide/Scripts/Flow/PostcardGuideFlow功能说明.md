# PostcardGuideFlow 功能说明

## 2026-07-18 对齐 develop 的 HasGuide 写入与 ClickBlock 时序

### 需求与原有行为基线

- 需求是在不改变 `PostcardGuideFlow → PostcardGuideFlow2` 结构的前提下，对齐 develop 的 `HasGuide/IsGuiding` 写入位置与 Victory/Postcard 两侧 ClickBlock 阻挡边界。
- 修改前，Flow1 在入口 Finger 点击后先创建 `PostcardPopup`，直到 Stage Two 结束才调用 `StartGuding()`；Popup 首帧没有来自 DB 的 guiding 状态，只依赖 Flow1 创建后再次调用交互锁。
- 修改前 VictoryPanel 的引导遮罩持续到入口 Finger 点击完成，晚于 develop 的“Stage One 结束、Finger 出现时释放”边界。

### 当前行为

- Stage One 正常结束后，Flow1 先释放 VictoryPanel 的引导遮罩，再创建并等待入口 `GuideFinger`；星星动画仍由 VictoryPanel 独立状态参与 ClickBlock，Flow1 释放不会提前关闭仍在播放的星星表现遮罩。
- 入口 Finger 点击完成后，Flow1 在 `PostcardPopup.Create()` 之前调用 `PostcardManager.StartGuding()`，写入并保存 `HasGuide=true`、`IsGuiding=true`。
- `PostcardPopup.OnOpen()` 在页面加载前读取 `DB.Postcard.IsGuiding` 并同步 `_isGuiding`，因此 Popup 打开首帧即可按 `_isGuiding || IsPagePlayingAnim` 开启 ClickBlock。
- Flow1 创建 Popup 后仍执行一次幂等的 `SetPostcardGuideInteractionLocked(true)`，保留 Flow1 到 Flow2 的锁所有权交接；Stage Two 结束后只启动 Flow2，不再重复写入 `HasGuide/IsGuiding`。
- Stage One/Two、Flow2、GuidePart、页面完成、Stage Three/Four 的阶段顺序和所有权结构不变。

### 数据、UI、验证与回滚

- 数据仍只使用 `DB.Postcard.HasGuide/IsGuiding` 与既有 `DB.Postcard.Save()`；未新增字段、配置、资源、全局事件、analytics、广告、IAP 或平台分支，也未修改 prefab。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：0 error / 99 warnings；静态顺序检查和目标 `git diff --check` 通过，仅有行尾转换提示。
- 待验证：Popup 创建失败、Stage Two 中断、Popup 快速关闭/重开时的 `IsGuiding` 恢复策略，以及 Victory Finger、Popup ClickBlock、Flow2 四阶段和 Android/iOS 横屏触摸。
- 回滚时把 `StartGuding()` 移回 Stage Two 后的 `StartPendingPostcardGuide()`，把 Victory 引导锁释放移回入口 Finger 完成后，并删除 Popup `OnOpen()` 的 DB guiding 初始化；不涉及 DB schema 或资源迁移。

## 2026-07-18 合并后恢复 GuideFlow 所有权并兼容 develop 交互要求

### 需求背景与修改前基线

- 最新 develop 合并后，`GameVictoryPanel` 被恢复为旧 `PostcardGuidePopup` / `Postcard_GuideFinger` 编排，而 `PostcardGuideFlow` 仍调用新 Popup 契约；`PostcardPopup` 同时丢失 Flow2 持有字段、交互锁和引导切页适配，形成新旧结构混用。
- 修改前执行 `Assembly-CSharp.csproj` 构建得到 13 个错误，集中为旧引导类型不存在、`SetPostcardGuideInteractionLocked(...)` 和 `_postcardGuideFlow2` 缺失，以及 `StartPendingPostcardGuide()` 返回类型不匹配。
- 本次要求保留现有 `GuideFlowBase → PostcardGuideFlow → PostcardGuideFlow2` 结构，只吸收 develop 中“胜利页星星动画期间阻止点击”“PostcardPopup 页面动画期间阻止点击”和 `CompleteEffects` 发射后等待 2 秒的行为。

### 影响文件、当前结构与行为

- `GameVictoryPanel.cs` 重新持有 `PostcardGuideFlow`，展示门槛统一使用 `PostcardGuideFlow.IsShow(...)`，关闭时主动取消 Flow1；旧 `PostcardGuidePopup`、`Postcard_GuideFinger` 和 `NextStepGuideAsync()` 编排已移除。
- 胜利页 ClickBlock 继续由 `_isPostcardGuideClickBlocked || _isPostcardStarAnimationClickBlocked` 合并决定。Flow1 只改写引导状态，不会在星星动画仍运行时提前关闭遮罩。
- `PostcardPopup.cs` 重新持有、运行和取消 `PostcardGuideFlow2`；`StartPendingPostcardGuide()` 恢复布尔启动契约，并把 GuidePart 启动、引导专用翻页和交互锁作为局部委托交给 Flow2。
- Popup 的 ClickBlock 继续由 `_isGuiding || IsPagePlayingAnim` 合并决定；引导锁同时保留按钮 `Interactable` 与点击入口保护，Flow2 内部翻页只能通过 `TryGoNextPage(true)` 的专用边界执行。
- `PostcardPopup_PageCompleteProcess.cs` 的 `NotifyPageComplete()`、`NotifyFailed()` 与固定 2 秒 `CompleteEffectsPostDelay` 保持不变，页面完成后继续通知 Flow2 进入 Stage Three/Four。
- 当前所有权链为：`GameVictoryPanel → PostcardGuideFlow（Stage One/Two）→ PostcardPopup → PostcardGuideFlow2（GuidePart、页面完成、Stage Three/Four）`。

### 数据、资源、事件与平台边界

- 继续复用 `PostcardManager.FullNeededStarCountForGuide()`、`StartGuding()`、`DB.Postcard.HasGuide/IsGuiding` 和既有保存链；没有新增或迁移存档字段。
- 继续通过 Bear UI 的现有 Popup 创建入口和 Bear Logger 记录失败；没有新增全局事件、资源地址、配置项、analytics、广告、IAP 或平台 SDK 分支。
- 本次未修改 `PostcardPopup.prefab`；工作区中该 prefab 在任务开始前已有未提交改动，运行验证需基于用户当前绑定确认 ClickBlock 与 GuidePart 层级。

### 验证、剩余风险与回滚

- CodeGraph 和全仓静态搜索确认旧引导类型不再出现在三个宿主脚本，Flow1/Flow2 的持有、启动、页面完成通知、失败通知和关闭取消均存在。
- 目标 `git diff --check` 通过，仅有工作区行尾转换提示；`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：0 error / 99 warnings。
- 尚未执行 Unity PlayMode 和 Android/iOS 横屏真机验证；Stage Two 创建失败降级、Popup 快速关闭/复用、奖励 Finger 失败、页面完成后 Stage Three/Four、星星动画与引导遮罩重叠仍需运行验证。
- 回滚本次改动会恢复合并后的新旧混用状态并重新产生编译错误；如需放弃 GuideFlow 方案，必须同时恢复旧引导类型及全部调用契约。当前方案不涉及数据、配置或资源迁移。

## 2026-07-17 GameVictoryPanel 打开即启用引导点击遮罩

### 需求背景与原有行为基线

- `GameVictoryPanel` 原本只在入场动画完成并正式运行 `PostcardGuideFlow` 后，才由 Flow 注入的局部回调开启 `clickBlock`。
- 因此当 `PostcardGuideFlow.IsShow(_currentLevel)` 已满足时，`OnOpen()` 到入场动画完成之间仍存在可点击窗口，玩家可能提前操作胜利页按钮。

### 影响范围与当前行为

- 仅修改 `Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel.cs`：`OnOpen()` 取得当前关卡并完成本轮状态重置后，调用 `PostcardGuideFlow.IsShow(_currentLevel)`，把结果直接同步到现有 `clickBlock`。
- 满足首次明信片引导展示条件时，胜利页从打开开始即阻挡底层界面点击；不满足时明确关闭遮罩，避免面板复用后继承旧 active 状态。
- 入场动画完成后的 `TryToPostcardGuide()` 仍复用同一 `IsShow(...)` 门禁；Flow 运行、结束、失败或取消时仍通过原局部回调维护遮罩，不改变 Stage One/Two、Finger、PostcardPopup 或页内 Flow2 编排。
- `IsShow(...)` 只读取运行态、RemoteConfig 关卡门槛和 `DB.Postcard.HasGuide`。本次未修改 prefab 绑定、DB/save、配置字段、资源加载、全局事件、analytics、广告、IAP 或平台 SDK；Android/iOS 共用同一横屏 UI 路径。

### 验证、剩余风险与回滚

- CodeGraph 已确认 `IsShow(...)` 的调用点和 `GameVictoryPanel` 到 `PostcardGuideFlow` 的调用链，未发现覆盖该行为的自动化测试。
- 目标 `git diff --check` 通过，仅有工作区既有的 LF/CRLF 转换提示；敏感项扫描确认本次未新增编辑器生命周期、实例化、资源加载或全局事件入口。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 个错误、98 个既有警告。
- 尚未执行 Unity PlayMode，需要验证引导关卡打开瞬间无法点击重玩/下一关/分享/去广告、入场动画结束后引导仍可继续，以及非引导关卡和面板复用时遮罩保持关闭；Android/iOS 横屏触摸尚未验证。
- 回滚时删除 `GameVictoryPanel.OnOpen()` 中依据 `PostcardGuideFlow.IsShow(...)` 同步 `clickBlock` 的三行逻辑，即恢复为 Flow 正式启动时才开启遮罩；无需迁移数据、配置或资源。

## 2026-07-17 PostcardPopup 打开即进入引导锁

- 需求：Flow1 打开 `PostcardPopup` 后必须立即进入 `_isGuiding`，禁止玩家在 1.5 秒等待或 Stage Two 交接前关闭、上一页或普通翻页。
- 原行为在 `PostcardPopup.Create()` 与 Flow2 启动之间没有 Popup 交互锁，关闭会让后续 `_postcardPopup` 失效并使交接返回 `Failed`。
- 当前 Flow1 创建 Popup 后立即调用本地交互锁 API；Stage Two 交接成功时通过 `_hasHandedOffPostcardGuide` 把锁的释放责任交给 Flow2，交接前取消、失败或启动失败则由 Flow1 收尾解锁。
- `PostcardPopup.StartPendingPostcardGuide()` 现在返回启动结果：当前页面不可用返回 false，已有同一 Flow2 或成功启动返回 true；未复制 GuidePart、DB/save、奖励或切页逻辑。
- 普通 PostcardPopup 打开路径不使用提前锁；没有修改 prefab、资源、配置、事件、analytics 或平台 SDK，Android/iOS 共用相同逻辑。
- 工程 C# 编译通过（98 个既有警告、0 个错误），`git diff --check` 和调用点检查通过；Unity PlayMode 的快速关闭、Stage Two、取消/失败及 Android/iOS 横屏触摸仍待验证。
- 回滚可删除 Flow1 的提前锁定、所有权标记和失败解锁，并恢复 Popup 启动方法原返回类型；无需迁移数据或资源。

## 需求背景

在现有 `GuideFlowBase`、`NormalGuidePopup.ShowAsync(...)` 和 `GuideFinger.WaitForClickAsync(...)` 基础上，先创建明信片首次引导的具体流程类，把胜利页到页内引导的入口编排能力集中起来。本次只新增能力，不替换 `GameVictoryPanel`、`PostcardPopup` 或页面完成流程的现有调用逻辑。

## 影响文件与系统

- `Assets/Game/UI/Guide/Scripts/Flow/PostcardGuideFlow.cs`
  - 新增 `PostcardGuideFlow : GuideFlowBase`。
  - 注入 `PostcardManager`、胜利页明信片入口、Finger 根节点和局部交互锁回调。
- 未修改任何现有调用方、prefab、Resources 路径、配置、DB 结构、全局事件、analytics、广告、IAP 或平台 SDK。
- 当前工程中没有 `new PostcardGuideFlow(...)` 或该实例的 `RunAsync(...)` 调用点，因此不会改变玩家可见行为。

## 原有行为基线

- `GameVictoryPanel.TryToPostcardGuide()` 负责补星、显示入口、锁定胜利页交互、等待 3 秒、打开 Stage One，并在关闭后创建入口 Finger。
- Finger 点击后，`GameVictoryPanel.NextStepGuideAsync()` 打开 `PostcardPopup`、等待 1.5 秒、打开 Stage Two；Stage Two 关闭或创建失败降级时调用 `PostcardManager.StartGuding()` 与 `PostcardPopup.StartPendingPostcardGuide()`。
- Stage Three/Four 继续由 `PostcardPopup_PageCompleteProcess` 在页完成、下一页 Finger 和收尾阶段处理。

## 新增流程能力

`PostcardGuideFlow.RunAsync(...)` 在未来接入后按以下顺序执行：

1. 校验运行态与注入依赖。
2. 调用现有 `FullNeededStarCountForGuide()`，显示明信片入口并启用局部交互锁。
3. 沿用现有 3 秒等待后，通过 `NormalGuidePopup.ShowAsync(Stage One)` 等待第一阶段关闭。
4. 通过 `GuideFinger.WaitForClickAsync(...)` 等待入口 Finger 点击，随后解除交互锁。
5. 调用现有 `PostcardPopup.Create()`，沿用 1.5 秒等待后展示并等待 Stage Two。
6. Stage Two 正常关闭后调用现有 `StartGuding()` 与 `StartPendingPostcardGuide()`；Stage Two 创建失败时保留现有直接启动页内引导的降级语义。
7. Stage Three/Four 不在本类中重复实现，继续交给现有 PostcardPopup 页面完成流程；新增但未接入的 `PostcardGuideFlow2` 用于后续统一等待该页面内阶段。

## 取消、失败与生命周期

- 所有延迟、Popup 等待和 Finger 等待都沿用 `GuideFlowBase` 传入的同一 `CancellationToken`。
- 弹窗、Finger 和依赖创建失败会返回对应的 `GuideFlowResult`；同阶段 Popup 已运行时返回 `AlreadyRunning`，不会追加第二个等待者。
- 无论完成、失败、取消或异常，`OnFlowStopped()` 都会尝试解除胜利页交互锁并释放本地 Popup 引用。
- 交互锁回调异常使用 Bear Logger 记录并转换为 `Failed`，不会从 `OnFlowStopped()` 再次抛出并覆盖原流程结果。
- 若在已创建 `PostcardPopup`、尚未完成 Stage Two 时取消，本类不会主动销毁业务 Popup；该边界需在正式接入前确认宿主关闭策略。

## 数据、资源与平台边界

- 当前未接入，因此没有实际数据写入或资源加载。
- 未来运行时会复用现有 `FullNeededStarCountForGuide()` 的星星补充能力，以及 `StartGuding()` 对 `DB.Postcard.HasGuide/IsGuiding` 的既有保存链；本类不新增数据字段或重复保存实现。
- UI 只复用 `NormalGuidePopup_1/_2`、`Postcard_GuideFinger.prefab` 和 `PostcardPopup` 的现有入口。
- 没有新增全局事件、每帧轮询或平台分支；Android/iOS 共用同一流程代码。

## 验证记录

- 静态搜索确认当前唯一 `PostcardGuideFlow` 引用位于类自身，现有运行时入口未修改。
- `git diff --check` 与敏感项扫描通过；未新增 `async void`、编辑器生命周期、全局事件、每帧入口或直接 `Instantiate`。
- Unity 生成的 `Assembly-CSharp.csproj` 已包含 `PostcardGuideFlow.cs`。
- 使用 Unity 自带 Roslyn、当前 Unity/UniTask/Bear/Assembly-CSharp 参考程序集对最终源码做独立编译，结果为 0 error；临时 DLL 已删除。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore --nologo --verbosity:minimal` 通过，结果为 0 个警告、0 个错误。
- Unity 目标 `Assembly-CSharp.dll` 已在 Flow2 加入后再次完成重建，可检出 Flow1/Flow2 相关类型和宿主契约。

## 剩余风险与回滚

- 尚未执行 RunAsync，因此 Stage One/Two、取消、重复运行、Popup 创建失败降级、交互锁恢复和页内交接均未做 PlayMode 验证。
- 尚未验证 Android/iOS 横屏触摸、暂停/时间缩放对两个现有 scaled delay 的影响。
- 正式接入时由 GameVictoryPanel 持有 Flow1、PostcardPopup 持有 Flow2；仍需补齐两段交接、宿主销毁和既有 Stage Three/Four 延迟任务的中途取消策略。
- 回滚时删除 `PostcardGuideFlow.cs/.meta` 与本文档即可；由于没有调用方，不需要回滚 prefab、数据、资源或业务脚本。

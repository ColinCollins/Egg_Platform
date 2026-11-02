# DrawGame_GamePlayPanel 墨水进度与激励加墨功能风险评估

## 1. 需求与原行为基线

- 需求：Draw HUD 使用基础短状态的 `process` 宽度布置星级阈值节点；阈值来自当前
  `DrawLevelData.InkStarStage1/2/3`，单位是剩余 Ink 长度，节点位置按
  `InkStarStage / MaxInkLength` 换算。墨水达到阈值时显示
  `UI_youxizhong2_img_xingji_01_2`，不足时隐藏该前景星；RewardAD 拉伸进度条后，
  StageItem 仍保持按短状态计算的位置。
- 需求：HUD 激励视频成功后，以当前运行时 `DrawInkState.MaxInk` 作为一次加墨量；
  背板宽度动画到 150%，`processBg` 和 `process` 动画到 165%，同时通过
  `DOFillAmount` 更新剩余墨水。150%/165% 只表示 UI 宽度，不改变运行时墨水上限。
- 原行为：`DrawGame_GamePlayPanel` 只遍历 `InkProgressBars` 立即写入 `fillAmount`，
  星级节点位置固定在 prefab；HUD 的 `rewardAd_btn` 没有运行时回调。
- 原行为：`DrawLineFeature.AddInk(...)` 通过减少 `_usedInk` 返还已经使用的墨水，
  剩余墨水最多恢复到 prefab 的 `maxInkLength`，不能在满墨时增加一整份奖励额度。
- 原行为：结算星级使用 prefab 的 20%/40%/60% 阈值，与当前关卡配置
  0%/20%/50% 不一致。

## 2. 受影响文件、资源与系统

- HUD：
  - `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel.cs`
  - `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_InkProcessHandle.cs`
  - `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_ProcessStageItem.cs`
  - `Assets/Game/UI/DrawGame/GamePlayPanel/Resources/DrawGame_GamePlayPanel.prefab`
  - `Assets/Game/UI/DrawGame/Prefabs/DrawGame_ProcessStageItem.prefab`
- Draw 运行时：
  - `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLevelRuntimeTypes.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`
- 复用系统：Bear `EventSubscriber` / `DrawInkStateChangedEvent`、`RewardAdHelper`、
  `RewardPlacement.InGameTips`、DOTween、现有 `clickBlock`。
- 不新增 DB 字段、Luban 字段、资源地址、analytics、IAP、RemoteConfig 或平台分支。

## 3. 当前行为与数据流

- `DrawPlayCtrl` 在 `DrawLevelCtrl.InitializeRuntime()` 前，把当前关卡
  `MaxInkLength` 和三个 `InkStarStage` 剩余 Ink 长度同步给 `DrawLineFeature`；
  运行时结算和 HUD 使用同一配置来源。
- `InkStarStage=0` 表示通关基础一星，不生成进度节点；其余
  `0..MaxInkLength` 内阈值各生成一个 `DrawGame_ProcessStageItem`，位置为
  `baseProcessWidth * stage / MaxInkLength`。
- `DrawGame_GamePlayPanel` 只保留 Bear 事件订阅和广告请求/发奖流程；填充刷新、基础宽度
  缓存、星级节点创建/达标切换、奖励宽度动画和 Tween 清理由
  `DrawGame_InkProcessHandle` 负责。
- `DrawInkStateChangedEvent` 到达 HUD 后，普通画线仍立即更新 `fillAmount` 和星级前景；
  达标显示 `UI_youxizhong2_img_xingji_01_2`，不足时只隐藏该前景，底层未达标图保留。
- `DrawInkStateChangedEvent` 首次携带 `UsedInk > 0` 时，表示玩家已实际开始消耗墨水；
  HUD 立即将完整 `rewardAd_btn` 根节点缩放到 0 并隐藏，不等待一笔画线完成，也不新增完成事件。
- prefab 中 `UIYouxizhong2X201Btn` 只绑定 `UI_youxizhong2_btn_X2_01` 子节点；完整入口的
  `rewardAd_btn` 父节点还包含 `UI_youxizhong2_img_shiping_01` 视频图标。因此按钮交互继续
  使用 `CustomButton` 子节点，缩放、激活状态和 Tween 清理改为显式绑定的 `rewardAdButtonRoot`。
- 激励视频链路：
  `rewardAd_btn -> RewardAdHelper -> 成功回调 -> DrawPlayCtrl.TryGrantRewardInk()
  -> DrawLevelCtrl.AddInk(DrawInkState.MaxInk) -> DrawLineFeature
  -> DrawInkStateChangedEvent -> DrawGame_GamePlayPanel
  -> DrawGame_InkProcessHandle DOTween`。
- 广告成功时传入的奖励量为当前运行时基础 `DrawInkState.MaxInk`。
  `DrawLineFeature` 将该值加入当前尝试 `_bonusInk`，因此
  `奖励后剩余墨水 = 奖励前剩余墨水 + MaxInk`；例如基础 MaxInk 为 100、奖励前剩余 40，
  奖励后实际剩余为 140，当前总容量为 200。
- `DrawInkState.MaxInk` 始终表示基础容量，`CurrentCapacity` 表示包含奖励后的当前总容量。
  `RemainingNormalized` 按当前总容量计算进度填充；结算星级直接比较实际 `RemainingInk`
  与配置 Stage，HUD 达标状态也直接比较剩余 Ink 与 Stage；只有节点坐标会将
  `Stage / MaxInkLength` 归一化到基础进度条宽度。
- 奖励增加容量时总会派发新的墨水快照，HUD 同步播放宽度和 `DOFillAmount` 动画；面板仍保留
  返回快照兜底，避免异常事件链导致广告成功后没有视觉反馈。
- 广告请求期间只关闭奖励按钮交互；失败或取消时恢复点击，奖励实际发放成功后通过
  `DOScale` 隐藏完整 `rewardAd_btn` 根节点。新 GameplayPanel 打开时恢复根节点原始缩放
  和显示；
  不复用成功演出的 `clickBlock`，避免不同流程交叉解除同一个全屏门禁。
- 即使未观看广告，只要首次实际消耗墨水，完整 `rewardAd_btn` 也会播放 0.2 秒缩小动画并隐藏；
  单纯按下但尚未产生有效采样时不会隐藏。动画使用不受 `timeScale` 影响的 UI 时间。
- Draw Reset 与胜利 Replay 都先由旧 GameplayPanel 渐黑并等待 0.5 秒，随后
  `CleanupLevelRuntime()` 同时销毁旧 DLevel 和旧 HUD；新 DLevel 初始化完成后统一调用
  `DrawGame_GamePlayPanel.Create()` 创建全新 HUD。
- 新 HUD 只走首次打开初始化：`OnOpen()` 清理广告请求、倒计时、奖励按钮、进度动画和
  事件订阅；`TryRefreshGamePlayPanel()` 设置关卡配置并主动写入新运行时的初始墨水快照。
  旧的 `ResetForNewAttempt(...)` 与保留 HUD 参数已经删除。
- Reset 开始和旧面板关闭都会提升 RewardAD 请求版本；旧广告回调同时受版本、
  `isActiveAndEnabled` 和 Unity 对象销毁状态拦截，不会向新 DLevel 尝试发放墨水。
- 背板目标宽度为基础宽度的 150%，`processBg` 与 `process` 为 165%；这些比例只影响
  UI 表现，不决定奖励容量。`DOFillAmount` 与宽度同步播放，但星级节点不移动，始终保留
  基础短状态坐标；动画使用不受 `timeScale` 影响的 UI 时间。

## 4. 验证证据

- C# 编译：执行
  `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`，结果为
  `0 error / 1120 warning`；warning 为工程既有项。
- 静态绑定：
  - HUD `inkProcessHandle` 指向 `inkProcess` 节点上的 `DrawGame_InkProcessHandle`。
  - Handle 的 `process` 指向 `process.Image`。
  - Handle 的 `processBackground` 指向
    `UI_youxizhong2_imgr_beiban_01_1.RectTransform`。
  - Handle 的 `processBarBackground` 指向 `processBg.RectTransform`。
  - Handle 的 `processStageItem` 指向嵌套 `DrawGame_ProcessStageItem` 组件。
  - `DrawGame_ProcessStageItem.reachedState` 指向
    `UI_youxizhong2_img_xingji_01_2`。
- 静态配置样例：当前 `tbdrawleveldata.json` 的正式 Draw 关卡为
  `MaxInkLength=10`、`InkStarStage1/2/3=0/2/5`，因此运行时生成位于基础进度条
  20% 和 50% 位置的两个可视阈值节点。

## 5. 剩余风险与未验证缺口

- 未验证：Unity PlayMode 中两个星级节点的实际层级、锚点、遮挡顺序和不同分辨率横屏表现。
- 未验证：RewardAD 拉伸到 165% 时，固定在短状态坐标的 StageItem 与长进度条之间的最终
  美术留白是否符合设计稿。
- 未验证：真广告成功/失败/取消、SDK 切后台恢复、动画期间 Reset、退出玩法和继续画线。
- 未验证：广告成功隐藏完整 `rewardAd_btn` 后，其所在布局是否需要额外占位调整。
- 未验证：广告回调与首次墨水消耗在同一时段到达时的真机视觉顺序；当前两条路径都会
  杀掉旧 Tween 后幂等缩小隐藏按钮，成功广告仍按既有回调发放墨水。
- 未验证：手动/失败 Reset 和胜利 Replay 在资源慢加载、Intro 延迟、Popup 打开和
  RewardAD 回调未返回时，旧 HUD 渐黑到新 HUD 渐显之间是否存在可见跳帧，以及旧广告
  是否会跨尝试发奖。
- 未验证：Android/iOS 真机 60 fps、广告返回后的首帧布局重建和 DOTween 视觉节奏。
- 当前广告位复用现有 `RewardPlacement.InGameTips`；项目没有 Draw 加墨专用广告位。
  若运营或 analytics 要求单独统计，需要先在 Luban 中新增明确枚举并重新生成代码。
- 激励加墨量取运行时基础 `DrawInkState.MaxInk`；该值已由
  `DrawLevelData.MaxInkLength` 在关卡初始化前注入，和 DrawSpace 长度制使用同一单位。
- `_bonusInk` 只存在于当前 DrawLevel 实例，实例销毁时清零；同关 Reset/失败重载/Replay 时，
  `DrawPlayCtrl` 保留领取门禁，并在同关新 DLevel 初始化完成后按其基础 `MaxInk` 重新加入一份
  真实容量。进入下一关、返回 Draw 选关页或 `StartPanel` 时门禁清零，新关恢复基础容量并可重新
  领取。该状态未写入 DB、配置或存档。
- 阈值配置会被限制到 `0..MaxInkLength`，并按 1/2/3 星顺序单调归一；重复、倒序或等于
  MaxInkLength 的阈值仍需策划数据校验。右端节点当前不生成，避免与进度条右端重叠。
- `DrawGame_ProcessStageItem` 最多按三个配置值创建，当前 0/2/5 只复用模板并复制一次，
  不增加常驻 Update；首次 HUD 配置有一次小规模实例化，移动端开销待 PlayMode/真机确认。

## 6. 回滚路径

1. 从 HUD 删除奖励按钮回调和 `DrawGame_InkProcessHandle`，移除
   `DrawGame_ProcessStageItem` 动态节点逻辑，恢复 `InkProgressBars` 的固定 prefab 进度显示。
2. 从 `DrawPlayCtrl.TryGrantRewardInk(...)` 删除广告成功后的
   `AddInk(DrawInkState.MaxInk)` 调用，并从 `DrawLineFeature` 删除 `_bonusInk` 容量计算。
3. 删除 `ConfigureInk(...)` 调用并恢复原百分比判定后，运行时会重新使用各 DLevel prefab
   的最大墨水和星级阈值。
4. 从两个 prefab 解除新增脚本和序列化引用；不需要迁移或清理玩家存档。

## 7. 2026-07-28 Replay HUD 重建更新

### 7.1 需求、基线与修改

- 用户明确要求 Draw Replay 必须重建 HUD，并清理仅为 HUD 复用增加的代码。
- 修改前 `ReloadCurrentLevelAsync()` 使用 `CleanupLevelRuntime(false)` 保留旧 HUD，
  `OpenDrawLevelAsync(..., true)` 跳过 Normal UI 关闭和 HUD 创建，最后调用
  `ResetForNewAttempt(...)` 逐项恢复面板状态。
- 修改后重载统一调用 `CleanupLevelRuntime()`，销毁旧 DLevel 与旧 HUD；随后调用无分支的
  `OpenDrawLevelAsync(activationVersion)`，按首次进关路径创建全新 GameplayPanel。
- 删除 `CleanupLevelRuntime(bool)` / `keepGamePlayPanelOpen` 两个复用分支参数及
  `ResetForNewAttempt(...)`。新 HUD 初始化只保留 `OnOpen()`、`SetLevel(...)` 和
  `SetInkState(...)` 三个既有入口，不新增状态层或抽象。

### 7.2 UI、事件、资源与性能影响

- `GameReplayEvent`、`DrawGameResetEvent`、Popup 关闭、0.5 秒等待、DLevel 资源地址和
  `GamePlayPanelFadeInEvent` 保持不变。
- 旧 HUD 关闭时由 `OnClose()` 解除 Bear 事件订阅、停止 BlackMask/奖励按钮 Tween，并释放
  动态 StageItem；新 HUD 由 Bear `UIManager` 重新加载或实例化并重新订阅事件。
- 每次 Reset/Replay 新增一次 HUD 销毁与创建峰值，这是用户要求的有意变化；没有新增
  `Update`、轮询、DB、存档、配置、analytics、广告位或平台分支。

### 7.3 验证、风险与回滚

- `git diff --check` 通过；C# 单进程构建通过，结果为 `0 error / 102` 个工程既有 warning。
- 静态确认 `ResetForNewAttempt`、`destroyGamePlayPanel` 和 `keepGamePlayPanelOpen` 已无
  代码引用，重载链为 `CleanupLevelRuntime -> OpenDrawLevelAsync ->
  TryRefreshGamePlayPanel`。
- 未验证：Unity PlayMode 连续 Replay、手动/失败 Reset、旧/新 BlackMask 视觉衔接、
  RewardAD 未返回时重玩，以及 Android/iOS 横屏的实例化峰值和首帧布局。
- 回滚时恢复 `CleanupLevelRuntime(false)`、`OpenDrawLevelAsync(..., true)` 和
  `ResetForNewAttempt(...)` 的 HUD 复用链；不涉及 prefab、配置或存档迁移。

## 8. 2026-07-28 Reset 双黑屏修复

### 8.1 触发链与原行为

- 问题由 `OnTriggerTrapEvent` 暴露：`DrawLevelCtrl` 令 Actor 死亡并等待 1.5 秒后，
  通过 `TryFailAndReset(...)` 派发一次 `DrawGameResetEvent(Failed)`。
- `_isWaitingForTrapFailure` 和 `_isResolved` 已分别阻止重复陷阱等待与重复结算，因此本次
  双黑屏不是同一 DLevel 重复派发 Reset。
- 原 HUD 重建时序为：旧 HUD 渐黑、旧 HUD 销毁、异步加载 DLevel、新 HUD 创建、
  `GamePlayPanelFadeInEvent` 再把新 HUD 设置为全黑。旧 HUD 销毁到新 HUD 创建之间没有
  遮罩，表现为“黑屏、短暂露出画面、再次黑屏、淡出”。

### 8.2 修改后行为与边界

- `OpenDrawLevelAsync(...)` 在首次异步资源加载前创建新 HUD；Reset/Replay 路径创建后立即
  调用 `ShowBlackMaskImmediately()`，把新 BlackMask 设置为 active、全黑并停止旧 Tween。
- Unity 的旧 HUD `Destroy` 与新 HUD 创建发生在同一帧；旧遮罩结束后，新遮罩已经覆盖。
  DLevel 资源加载、实例化、运行时初始化和不可见 Intro 均在新遮罩后完成。
- 加载完成后仍只派发一次既有 `GamePlayPanelFadeInEvent`，新 HUD 从全黑淡出；首次正常
  进关传入 `showResetMask=false`，继续由既有 `EnterLevelLoading` 负责遮挡。
- HUD 仍完整销毁并重建；没有恢复 `ResetForNewAttempt(...)`，也没有新增全局事件、
  UI prefab、资源地址、DB、存档、配置、analytics 或平台分支。

### 8.3 验证、风险与回滚

- `git diff --check` 通过。
- C# 单进程构建通过，结果为 `0 error / 102` 个工程既有 warning。
- 静态确认新 HUD 的创建和 `ShowBlackMaskImmediately()` 位于
  `ResourceManager.LoadAsync(...)` 的第一个 `await` 之前；正常进关与重载分别明确传入
  `false/true`，没有隐藏的默认分支。
- 未验证：Unity PlayMode 的陷阱失败、手动 Reset、胜利 Replay、慢资源加载和连续重试；
  Android/iOS 横屏的首帧遮挡、实例化峰值和切后台恢复仍待验证。
- 回滚时移除 `showResetMask` 参数和 `ShowBlackMaskImmediately()`，把 HUD 创建恢复到
  DLevel 初始化后；这会恢复已确认的双黑屏窗口，不涉及数据迁移。

## 9. 2026-07-28 HUD 墨水配置来源唯一化

### 9.1 需求与原行为基线

- 用户要求 `isConfig=false` 时，HUD 使用 DLevel prefab 内最终生效的
  `maxInkLength + stage`。DLevel_004 当前应按 `2 / 30` 布置二星节点，而不是按
  `tbdrawleveldata` 的 `2 / 15`。
- 修改前 `DrawLineFeature` 已能按 `isConfig` 选择 Inspector 或表配置，但
  `DrawGame_GamePlayPanel.SetLevel(...)` 仍把 `DrawLevelData` 直接交给 HUD；
  `DrawLineFeature` 和 HUD 因此存在两条独立配置来源。

### 9.2 修改后行为与数据流

```text
Inspector 或 DrawLevelData
  -> DrawLineFeature 选择并归一化最终 maxInkLength + stage
  -> DrawInkState(MaxInk, Stage, RemainingInk, StarCount)
  -> DrawGame_GamePlayPanel
  -> DrawGame_InkProcessHandle.ConfigureStages
```

- `DrawInkState` 新增只读 `Stage`，与 `MaxInk` 一起表示 `DrawLineFeature` 最终采用的配置。
- HUD 的 `ConfigureStages(...)` 只接收 `DrawInkState`，不再引用 `DrawLevelData`。
- `SetLevel(...)` 只设置关卡显示编号；Stage 节点在运行时初始化完成后，随首次
  `SetInkState(...)` 按同一快照创建。
- `isConfig=false` 时快照来自 prefab Inspector；`isConfig=true` 时
  `DrawLineFeature.ConfigureInk(...)` 先用表配置覆盖，再生成同一种快照。HUD 不负责判断
  `isConfig`，没有第二套回退或覆盖逻辑。
- 奖励墨水继续只改变 `CurrentCapacity`，不改变基础 `MaxInk` 或 `Stage`；后续墨水事件只
  刷新填充和节点达标状态，不重复创建 Stage 节点。

### 9.3 影响、验证与回滚

- 影响文件：`DrawLevelRuntimeTypes.cs`、`DrawLineFeature.cs`、`DrawPlayCtrl.cs`、
  `DrawGame_GamePlayPanel.cs`、`DrawGame_InkProcessHandle.cs`。
- 未修改 prefab、Luban、生成配置、DB、存档、资源地址、全局事件、广告位或平台分支。
- 静态扫描确认 GameplayPanel 目录不再引用 `DrawLevelData`、`MaxInkLength` 或
  `InkStarStage1/2/3`；`DrawInkState` 仍只由 `DrawLineFeature` 创建。
- `git diff --check` 通过；C# 单进程构建通过，结果为 `0 error / 103 warning`，现有
  warning 未指向本次修改。
- 未验证 Unity PlayMode 中 DLevel_004 的 `2/30`、`5/30` 节点位置、RewardAD 后固定节点
  表现、Reset/Replay 重建，以及 Android/iOS 横屏布局。
- 回滚时恢复 HUD 的 `ConfigureStages(DrawLevelData)` 与 `SetLevel(level, levelData)`，
  并删除 `DrawInkState.Stage`；这会重新引入 Inspector 模式下的双配置来源。

## 10. 2026-07-29 Draw 时奖励按钮取消缩放

### 10.1 需求、基线与修改

- 用户要求 `DrawGame_GamePlayPanel` 的奖励按钮不要在每次 draw 时缩放。
- 修改前每次 `DrawInkStateChangedEvent` 携带 `UsedInk > 0` 时都会进入
  `HideRewardButtonAnimated()`；连续画线事件会 `DOKill()` 当前 Tween 并重新执行
  `DOScale(Vector3.zero, 0.2f)`。
- 修改后 draw 事件仍在首次实际消耗墨水后隐藏完整 `rewardAdButtonRoot`，但改为停止现有
  Tween 后直接 `SetActive(false)`，不再改变按钮缩放。广告奖励发放成功后的缩小隐藏动画
  保持不变。

### 10.2 影响范围与数据流

- 代码只修改 `DrawGame_GamePlayPanel.OnDrawInkStateChanged(...)` 的按钮隐藏表现，并新增
  私有立即隐藏方法。
- `DrawInkStateChangedEvent -> DrawGame_InkProcessHandle.SetInkState(...)` 的墨水进度与
  星级刷新保持不变；RewardAD 请求、成功发奖、失败恢复交互、广告位、DB、存档、配置、
  analytics、资源和平台流程均未修改。
- 后续 draw 墨水事件因按钮根节点已经 inactive 而直接返回，不创建 Tween，也不增加
  `Update` 或运行时分配循环。

### 10.3 验证、风险与回滚

- 静态验证：draw 事件路径只调用 `HideRewardButtonImmediately()`；`DOScale` 仅保留在广告
  奖励成功后的 `HideRewardButtonAnimated()`。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`，结果为
  `0 error / 101 warning`；warning 为工程既有项，未发现本次修改的编译诊断。
- 待验证：Unity PlayMode 中首次落笔时按钮是否无缩放直接隐藏、连续画线是否不再闪动，
  以及广告成功后的缩小隐藏动画、Reset/Replay 新 HUD 恢复和 Android/iOS 横屏表现。
- 回滚时把 `OnDrawInkStateChanged(...)` 的调用恢复为 `HideRewardButtonAnimated()`，并删除
  `HideRewardButtonImmediately()`；不涉及 prefab、配置或存档迁移。

## 11. 2026-08-03 墨水增长动画时长 Inspector 配置

### 11.1 需求、基线与修改后行为

- 需求：允许在 Inspector 调整 `inkProcess` 奖励加墨增长动画的 duration。
- 修改前 `DrawGame_InkProcessHandle` 以私有常量固定使用 `0.5` 秒，背板、底图、填充图宽度
  和 `DOFillAmount` 无法分别从 Unity Inspector 调整。
- 修改后 `DrawGame_InkProcessHandle` 暴露 `inkProcessGrowthDuration`，默认仍为 `0.5` 秒，
  并继续让上述四段 Tween 共用同一时长；字段限制为非负值，设为 `0` 时下一次奖励加墨刷新
  会立即完成动画。

### 11.2 影响范围与数据流

- 仅修改 `DrawGame_InkProcessHandle.cs` 的动画参数来源，不改变奖励墨水数值、宽度倍率、
  Ease、事件、广告回调、DB、存档、配置、资源地址或平台分支。
- 现有 prefab 未写入字段覆盖值时沿用脚本默认 `0.5` 秒；后续可在 `inkProcess` 节点的
  `DrawGame_InkProcessHandle` 组件中调整并保存 prefab 覆盖值。

### 11.3 验证、剩余风险与回滚

- 静态确认四处奖励增长 Tween 均读取 `inkProcessGrowthDuration`，原固定时长常量已移除。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`，结果为
  `0 warning / 0 error`；对本次三个修改文件执行 `git diff --check` 通过。
- 未验证 Unity Inspector 实际调参后的 PlayMode 节奏及 Android/iOS 横屏真机表现。
- 回滚时恢复 `RewardInkAnimationDuration = 0.5f` 常量，并让四处 Tween 重新读取该常量；
  不涉及数据或存档迁移。

## 12. 2026-08-03 Reset 后保持奖励墨水长宽表现

### 12.1 需求、基线与修改后行为

- 需求：当前 Draw 关卡观看 RewardAD 并成功领取奖励墨水后，手动 Reset、失败重试或胜利 Replay
  重建的 `DrawGame_GamePlayPanel` 仍保持 `inkProcess` 的伸长状态；只有退回 `StartPanel`
  并重新进入 Draw 玩法后才恢复基础短状态。
- 修改前 Reset/Replay 会销毁旧 HUD；新 HUD 的 `OnOpen()` 固定恢复基础宽度。虽然
  `DrawPlayCtrl._isRewardInkGranted` 在当前选关进入周期内保留，新 HUD 的
  `SetRewardInkGranted(true)` 只隐藏奖励按钮，没有恢复背板、底图和填充图长宽表现。
- 修改后 `SetRewardInkGranted(...)` 同时把领取状态同步给 `DrawGame_InkProcessHandle`：
  已领取时立即恢复 150% 背板与 165% 底图/填充图宽度；未领取时恢复基础宽度。
  Reset/Replay 不播放第二次奖励增长动画。

### 12.2 状态、UI 与系统边界

- 状态来源继续是 `DrawPlayCtrl._isRewardInkGranted`：奖励发放成功时置为 `true`，
  Reset/Replay 不清零；进入新 Draw 关卡的 `TryEnterLevel(...)` 或退回 `StartPanel` 导致的
  `Deactivate()` 会清零。因此新玩法进入周期会重新显示基础短状态。
- 本次只同步 HUD 宽度；不重新调用 `AddInk(...)`，不修改奖励墨水容量、星级阈值位置、
  RewardAD 请求与回调、DB、存档、配置、资源地址、analytics 或平台分支。
- `DrawGame_InkProcessHandle.SetRewardPresentationExpanded(...)` 会先停止旧宽度 Tween，
  避免 HUD 状态同步与残留动画竞争；动态 StageItem 仍按基础短宽度坐标布置。

### 12.3 验证、剩余风险与回滚

- 静态链路确认：`ReloadCurrentLevelAsync -> OpenDrawLevelAsync -> TryCreateGamePlayPanel
  -> SetRewardInkGranted(_isRewardInkGranted)`；Reset/Replay 期间该字段未清零，
  `TryEnterLevel(...)` 与 `Deactivate()` 明确清零。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`，结果为
  `0 error / 1119 warning`；warning 为工程既有项，本次新增代码未产生编译错误。
- 待验证：Unity PlayMode 中 RewardAD 成功后的手动 Reset、失败重试、胜利 Replay、
  退回 `StartPanel` 后重新进入，以及 Android/iOS 横屏布局。
- 回滚时删除 `SetRewardPresentationExpanded(...)` 及 `SetRewardInkGranted(...)` 中的同步调用，
  新 HUD 会恢复为每次打开均显示基础短宽度；不涉及数据或存档迁移。

## 13. 2026-08-04 奖励墨水同关重建容量与 HUD 一致性

### 13.1 需求、原行为与修改后行为

- 用户最终确认：奖励墨水领取后只在同关 Reset、失败重载和胜利 Replay 中继续真实生效；
  直接进入下一关也属于新关卡周期，必须恢复基础容量并允许重新领取。
- 修改前 `DrawPlayCtrl` 会在同关重建时保留 `_isRewardInkGranted`，新 HUD 因而恢复长进度条并隐藏
  RewardAD；但新 DLevel 的 `_bonusInk` 从零开始，控制器没有重新调用 `AddInk(...)`，造成“显示长条、
  不能再看广告、实际只有基础墨水”。
- 修改后同关重建的 DLevel 完成 `InitializeRuntime()` 且确认就绪后，控制器若仍持有领取门禁，就按
  当前关的 `DrawInkState.MaxInk` 调用一次 `AddInk(...)`。因此 HUD 长状态、广告资格和实际
  `CurrentCapacity` 始终对应；直接下一关经过 `TryEnterLevel(...)` 清零门禁，只使用新关基础容量。

### 13.2 状态边界与数据流

- 领取链为：`RewardAD 成功 -> TryGrantRewardInk -> AddInk(MaxInk) -> 门禁=true`；同关重建链为
  `InitializeRuntime -> RestoreRewardInkForNewRuntime -> AddInk(当前关 MaxInk) -> SetInkState`。
- `TryGrantRewardInk(...)` 现在只在 `AddInk(...)` 实际返回正数后记录领取成功。新实例恢复失败时会
  撤销领取门禁并把 HUD 恢复为基础状态，避免再次形成表现与容量不一致。
- `TryEnterLevel(...)` 会清零门禁，因此首次选关进入和直接下一关都从基础容量开始；
  `OpenLevelSelection(...)` 与 `Deactivate()` 也分别在返回选关页、`StartPanel` 或切出 Draw 玩法时清零。
  只有不经过 `TryEnterLevel(...)` 的同关 Reset/Replay 保留门禁。
- 不新增 DB、Luban、资源、全局事件、广告位、analytics 或平台分支；状态仍只保存在
  `DrawPlayCtrl` 当前内存会话中。

### 13.3 验证、剩余风险与回滚

- 静态检查已确认：`TryEnterLevel(...)` 在新关加载前清零门禁；同关重建不会调用该入口；同关新运行时的
  容量恢复位于初始化成功后、HUD 最终 `SetInkState(...)` 前。
- `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal`
  通过，结果为 `0 error / 101 warning`；warning 为工程既有项。相关文件 `git diff --check` 通过。
- 待验证：Unity PlayMode 中广告成功后的手动 Reset、失败重载、胜利 Replay、直接下一关、返回选关再进、
  返回 StartPanel 再进，以及真实广告回调和 Android/iOS 横屏恢复，均标记为 `uncertain`。
- 回滚时移除 `RestoreRewardInkForNewRuntime()` 及其调用；不涉及配置、资源或玩家存档迁移，但会重新
  出现同关 Reset/Replay 后 HUD 与实际奖励容量不一致。

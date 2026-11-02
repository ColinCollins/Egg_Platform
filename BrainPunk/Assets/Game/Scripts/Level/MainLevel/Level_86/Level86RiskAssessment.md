# Level86Ctrl Completion Risk Assessment

## Original Behavior Baseline

- Level86 had serialized fields for `trophy1` through `trophy5`, `trophy0`, `StartSign`, `scrolls`, and `chest`.
- Hat and scroll APIs were partially implemented; chest and final success APIs were placeholders.
- Existing comments described `StartSign` Y movement and trophy4 using the Level24 right movement button as reference evidence.
- Prefab/API binding is user-owned for this work and is not modified here.

## Affected Files / Resources

- Modified: `Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs`
- Added/updated docs: `Level86FeatureNotes.md`, `Level86RiskAssessment.md`
- Not modified: `Assets/Game/Prefabs/Level/Level_086.prefab`, shared click/input systems, save/config/analytics/resource/platform systems

## Existing Systems Used

- `BaseLevelCtrl.SuccessStrightly()` for direct level success.
- `ActorSpineCtrl.PlayAnimation(...)` for optional chest `box-open` feedback.
- DOTween for bounded click feedback, hat opening, entry sign animation, final trophy movement/scale, sign fade, and delayed success handoff.
- I2 localization and `SystemTips` for the separate readable tip API.

## Risk Items

- P1: User-owned binding may still point to stale or copied methods. Mitigation: APIs are implemented and documented, but prefab wiring remains a release validation item.
- P1: Missing serialized trophy or animation references could hide visual feedback. Mitigation: null-safe code skips missing optional presentation instead of throwing; validation must still catch missing required setup.
- P1: Duplicate taps could duplicate reward or success. Mitigation: per-trophy resolved flags and `successAccepted` guard.
- P1: Restart/exit during tweens could dispatch stale success. Mitigation: `OnDisable` / `OnDestroy` kill entry sequence, final success sequence, and delayed success callback.
- P1: Chest delayed trophy resolution could complete after restart/exit. Mitigation: delayed chest flow stores `attemptVersion` and exits if the attempt changed.
- P2: Trophy4 accepted condition is gameplay-panel layout dependent. Mitigation: runtime lookup is limited to the current panel `rightMove_btn`, `trophy4` is positioned from that button's world position, and the button movement check is bounded to a single cached `RectTransform`.
- P2: Chest animation name or `shiny` placement may be wrong. Mitigation: completion does not depend on the animation/effect; PlayMode must verify the real asset and visual placement.
- P2: Final trophy target point may not be the desired visual center in the prefab. Mitigation: target falls back through `signBoard`, `StartSign`, and the clicked transform; PlayMode must validate actual composition.
- P2: Mobile visual smoothness is unverified. Mitigation: implementation uses only bounded one-shot work and no resource loading or per-frame search.

## Verification Evidence

- Static code review: Level86 implementation remains local and runtime-only.
- Static compile: `dotnet build D:\GitStore\BrinPunk\BrainPunk\Assembly-CSharp.csproj --no-restore` passed with 0 errors and 92 existing project warnings.
- Static code inspection: duplicate trophy resolution and duplicate final success are guarded by local flags; restart/exit stale callbacks are mitigated by DOTween cleanup in disable/destroy; chest delayed trophy resolution is guarded by `attemptVersion`.
- `git diff --check`: passed for touched tracked files; Git reported only the existing LF-to-CRLF warning for `DevelopMD/已有功能设计与使用记录.md`.

## Unverified Gaps

- Unity PlayMode validation for fresh entry, all public APIs, separate tip API, final success, restart/fail/exit/re-entry, and missing references.
- Level86 prefab binding correctness, including stale copied persistent event names.
- Actual `StartSign`, `signBoard`, trophy visual active-state intent, and `trophy0` final presentation.
- Real chest Spine animation name `box-open` and `shiny` effect placement.
- One unaffected click/touch level regression.
- Android/iOS landscape smoke.

## Rollback Path

Rollback is reverting `Level86Ctrl.cs`, `Level86FeatureNotes.md`, and this risk assessment. Since prefab binding is not modified by this work, prefab rollback belongs to the user-owned binding change set if one exists later.

## 2026-06-23 signTips 渐隐补充风险记录

### 原有行为基线

- `Level86Ctrl` 已有 `signBoard` 成功渐隐、重开恢复 alpha、disable/destroy kill tween。
- `signTips` 字段已出现在脚本中，但未参与最终成功渐隐、重开恢复或 tween 清理。
- `Level_086.prefab` 文本证据显示告示牌附近存在从 `Assets/Game/Prefabs/LevelItems/Tips.prefab` 实例化的 `TextMeshPro` 文本，且本次未改 prefab。

### 影响文件 / 资源

- 修改：`Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs`
- 更新文档：`Level86FeatureNotes.md`、`Level86RiskAssessment.md`
- 未修改：`Assets/Game/Prefabs/Level/Level_086.prefab`、共享点击系统、存档、配置、资源加载、打点、广告、内购、平台 SDK。

### 使用的既有系统

- DOTween TMP 扩展：对 `TMP_Text` 执行 `DOFade`。
- DOTween SpriteRenderer 扩展：保留原 `signBoard.DOFade`。
- Unity 本地组件查找：仅在 `signBoard` 子级解析 `TMP_Text`，无全场景搜索。

### 风险项

- P1：`signTips` 未绑定且 `signBoard` 子级没有预期 `TMP_Text` 时，文字不会淡出。缓解：代码 null-safe 跳过；PlayMode 需确认 prefab 实际层级。
- P1：重开或退出过程中 signTips tween 残留可能导致提示文字透明度异常。缓解：`ResetAttemptState` 恢复初始 alpha，`CleanupRuntimeState` 调用 `signTips.DOKill()`。
- P2：字段从具体 `TextMeshPro`/UGUI 类型收敛为 `TMP_Text` 后，需要 Unity 序列化绑定验证。缓解：`TMP_Text` 是 `TextMeshPro` 与 `TextMeshProUGUI` 的共同基类，编译已通过；仍需 Inspector/PlayMode 检查引用。
- P2：文本淡出时长与 signBoard 一致，但实际视觉层级或 SortingGroup 可能让效果看起来不同步。缓解：逻辑使用同一 sequence、同一 duration、同一 Ease；实际视觉需 PlayMode 验证。

### 验证证据

- 静态代码检查：本次补充只触碰 Level86 本地 UI 表现，不新增存档、配置、资源加载、打点、广告、内购或平台 SDK 链路。
- 静态 prefab 检查：`Assets/Game/Prefabs/Level/Level_086.prefab` 当前存在 `Tips` 文本实例，源 prefab 为 `Assets/Game/Prefabs/LevelItems/Tips.prefab`，其文本组件类型为 `TextMeshPro`。
- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，0 error / 1118 warning。
- 格式检查：`git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs` 通过，仅报告 LF 到 CRLF 工作区提示。

### 未验证缺口

- Unity PlayMode 未验证实际 `signTips` 绑定、fallback 解析对象、成功时同步淡出、重开恢复 alpha、退出/disable tween 清理。
- Android/iOS 横屏 smoke 未执行。

### 回滚路径

回滚 `Level86Ctrl.cs` 中 signTips 相关字段、缓存、解析、渐隐、恢复和清理逻辑；如后续用户另行修改 prefab 绑定，prefab 回滚需按对应资源变更单独处理。

## 2026-06-23 脚本防护补充风险记录

### 原有行为基线

- `Level86Ctrl` 当前公开入口包括 `PlayPickUpScroll`、`PlayHatClickFeedback`、`PlayChest`、`PlayTrophy4`、`PlayTrophy5`、`PlayTrophy6`、`PlaySuccessLevel` 和 `PlaySystemTips`。
- 当前脚本已有 `Application.isPlaying`、`successAccepted`、per-trophy resolved flags、`attemptVersion` 和 DOTween cleanup 等运行时边界。
- 静态 prefab 文本显示 `Level_086.prefab` 当前存在多个 `PlaySystemTips` 绑定，一个 `PlayTrophy6` 绑定，一个 `PlayChest` 绑定；本次不修改 prefab 引用。

### 影响文件 / 资源

- 修改：`Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs`
- 更新文档：`Level86FeatureNotes.md`、`Level86RiskAssessment.md`
- 未修改：`Assets/Game/Prefabs/Level/Level_086.prefab`、共享点击系统、存档、配置、资源加载、打点、广告、内购、平台 SDK。

### 使用的既有系统

- DOTween：继续用于点击反馈、帽子表现、入口告示牌、成功表现和文本/告示牌淡出。
- UniTask：继续用于宝箱延迟解析和 Trophy4 延迟定位/显示。
- `CommonUtility.ConvertUIToWorldPosition(...)`：继续用于 Trophy4 从 UI 按钮位置转世界位置。
- `BaseLevelCtrl.SuccessStrightly()`：成功路径保持不变。

### 风险项

- P1：`chest` 未绑定时宝箱表现缺失。缓解：本次防护避免空引用阻断流程，但 PlayMode 仍需确认缺失引用是否允许作为可发布状态。
- P1：`trophy6` 未绑定时点击可能无表现。缓解：本次防护避免空引用；实际绑定仍需 prefab/PlayMode 验证。
- P1：UnityEvent 传入 `trans == null` 时提示摇晃反馈缺失。缓解：提示文本仍显示，摇晃仅在对象存在时执行。
- P1：Trophy4 第二段延迟可能在重开、退出或成功后显示旧对象。缓解：本次在第二段等待后增加运行态、版本和成功状态复核。
- P1：重开后 `hasChestOpen` 残留可能导致宝箱入口被误判为已打开。缓解：本次在 `ResetAttemptState()` 重置该标记。
- P2：hat 外层 sequence 未绑定生命周期可能带来 disable/destroy 后的 tween 残留风险。缓解：本次为外层 sequence 增加 `SetLink(gameObject)`。

### 验证证据

- 静态代码检查：本次补充只增加 Level86 本地防护，不新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、RemoteConfig、资源加载、打点、广告、内购或平台 SDK 链路。
- 静态 diff 检查：公共 API 名称、成功路径、trophy 判定条件、动画常量、已有序列化字段未改。
- 静态 prefab 检查：`Level_086.prefab` 当前存在本次涉及入口的绑定证据，但 prefab 文件未被修改。
- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，0 error / 96 warning；warning 为当前项目既有输出。
- 格式检查：`git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_86/Level86FeatureNotes.md Assets/Game/Scripts/Level/LevelItem/Level_86/Level86RiskAssessment.md DevelopMD/已有功能设计与使用记录.md` 通过，仅报告 LF 到 CRLF 工作区提示。

### 未验证缺口

- Unity PlayMode 未验证缺失引用、空事件参数、快速重开、disable/destroy、成功后延迟回调等实际运行表现。
- Android/iOS 横屏 smoke 未执行，快速点击和重开路径的触控体验、帧率和残留表现仍待确认。
- `hasChestOpen` 重置虽然符合关卡新尝试状态重置，但仍需确认策划期望的宝箱可重复尝试边界。

### 回滚路径

回滚 `Level86Ctrl.cs` 中本次防护补充和本节文档记录；由于 prefab 未修改，不涉及 prefab 回滚。

## 2026-06-24 signTips 数组适配风险记录

### 原有行为基线

- `Level86Ctrl.signTips` 已调整为 `TMP_Text[]`。
- 上一版成功淡出、重开恢复、tween 清理和 fallback 解析仍按单个 `TMP_Text` 的行为组织。
- `Level_086.prefab` 当前序列化证据显示 `signTips` 包含两个文本引用；本次未主动改写 prefab 绑定。

### 影响文件 / 资源

- 修改：`Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs`
- 更新文档：`Level86FeatureNotes.md`、`Level86RiskAssessment.md`、`DevelopMD/已有功能设计与使用记录.md`
- 未修改：共享点击系统、存档、配置、资源加载、打点、广告、内购、平台 SDK。

### 使用的既有系统

- DOTween TMP 扩展：继续对 `TMP_Text` 执行 `DOFade` 和 `DOKill`。
- DOTween Sequence：多个文本与 `signBoard` 加入同一个 final success sequence。
- Unity 本地组件查找：字段缺失时仅从 `signBoard` 子级解析 `TMP_Text[]`，无全场景搜索。

### 风险项

- P1：数组中存在空元素时，部分文本不会淡出。缓解：代码跳过空元素，PlayMode 需确认 prefab 数组完整性。
- P1：多个文本初始 alpha 不一致时，单一缓存会恢复错误。缓解：本次改为按索引缓存 `signTipsInitialAlphas`。
- P1：重开或退出过程中某个文本 tween 残留可能导致透明度异常。缓解：本次 `KillSignTipsTween()` 遍历每个非空文本执行 `DOKill()`。
- P2：fallback 解析 `signBoard` 子级全部 TMP 文本可能包含非目标文本。缓解：优先使用显式数组绑定；fallback 只在数组未绑定或全为空时启用，仍需 PlayMode 确认层级。
- P2：数组引用顺序变化不影响淡出本身，但会影响按索引恢复的初始 alpha。缓解：初始 alpha 在本次运行缓存后使用同一数组索引恢复；运行中不重排数组。

### 验证证据

- 静态代码检查：本次只改 Level86 本地成功表现辅助逻辑，不新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、RemoteConfig、资源加载、打点、广告、内购或平台 SDK 链路。
- 静态 prefab 检查：`Assets/Game/Prefabs/Level/Level_086.prefab` 当前 `signTips` 序列化为两个文本引用。
- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，0 error / 1061 warning。
- 格式检查：`git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs` 通过，仅报告 LF 到 CRLF 工作区提示。

### 未验证缺口

- Unity PlayMode 未验证两个 `signTips` 文本同步淡出、重开恢复 alpha、退出/disable tween 清理。
- Inspector/PlayMode 未验证 `Level_086.prefab` 当前两个数组引用是否正是策划期望淡出的文本。
- Android/iOS 横屏 smoke 未执行。

### 回滚路径

回滚 `Level86Ctrl.cs` 中数组 helper、缓存、解析、渐隐、恢复和清理逻辑，并回滚本节文档记录；如用户另行维护了 prefab 数组绑定，prefab 回滚按对应资源变更单独处理。

## 2026-07-13 Success 状态入口防护风险记录

### 原有行为基线

- `PlaySuccessLevel(...)` 原先只校验运行态与 `successAccepted`，未校验 `PlayCtrl` 是否为 `PLAYING`。
- UIManager Popup 层存在已打开界面时，原成功入口仍可能设置 `successAccepted` 并启动最终成功表现。
- 最终调用链为 `PlaySuccessLevel → OnSuccess → BaseLevelCtrl.SuccessStrightly()`。

### 影响文件 / 资源

- 修改：`Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs`
- 更新：`Level86FeatureNotes.md`、`Level86RiskAssessment.md`、`DevelopMD/已有功能设计与使用记录.md`
- 未修改：`Assets/Game/Prefabs/Level/Level_086.prefab`、共享 UIManager、共享 GamePlayPanel、全局事件、存档、配置、资源、打点和平台 SDK。

### 使用的既有系统

- `PlayCtrl.CheckState(GamePlayStateName.PLAYING)`：读取当前玩法 FSM 是否处于进行中。
- Bear UI `BaseUIView.Layer` / `IsOpen`：区分 Popup 层和当前打开状态。
- `UIManager.Instance` 层级：仅在 Level86 最终成功交互时枚举其子级 UI 组件。

### 风险项

- P1：Popup 打开或玩法非 `PLAYING` 时误触发结算。缓解：在 `successAccepted` 置位与成功动画前同时校验两项条件，不满足即返回。
- P1：把已关闭但缓存的 Popup 当成打开界面，造成关卡无法通关。缓解：除 `UILayer.Popup` 外还要求 `BaseUIView.IsOpen == true`。
- P1：拦截一次后永久无法成功。缓解：被拦截路径不修改 `successAccepted`，条件恢复后可重新调用成功入口。
- P2：成功动画开始后才出现 Popup 或状态变化时仍会继续进入 Success。缓解：本次按“触发时当前状态”执行入口防护；是否需要动画完成前二次校验待 PlayMode 场景确认，当前不增加可能导致视觉回滚不一致的中途撤销逻辑。
- P2：`GetComponentsInChildren<BaseUIView>(true)` 在成功交互时产生一次数组分配。缓解：不是 `Update` 或轮询路径，调用频率仅限最终成功尝试；移动端实际开销仍待设备验证。
- P2：UIManager 层级外若存在未按 Bear UI 管理的自定义弹窗，不会被本判定识别。缓解：当前项目 Popup 证据使用 Bear `UILayer.Popup`；非标准弹窗属于待验证缺口。

### 验证证据

- CodeGraph 在修改后返回当前源码和调用链，确认两个防护方法只由 Level86 成功入口调用。
- C# 项目编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 成功，0 error / 1116 warning。
- 静态代码检查：未新增共享 API、全局事件、Editor 生命周期入口、存档、配置、资源加载、打点、广告、内购或平台 SDK 修改。
- 静态顺序检查：`CanTriggerSuccess()` 在 `entrySignSequence.Kill()` 与 `OnSuccess(...)` 之前执行。

### 未验证缺口 / 待确认

- Unity PlayMode 未验证 Popup 打开时点击成功目标不会触发动画或 Success、关闭后再次点击可正常通关。
- Unity PlayMode 未逐项验证 PLAYING、PAUSE、FAILED、SUCCESS、加载/切关状态。
- 未验证嵌套 Popup、隐藏中的 Popup、关闭动画期间 `IsOpen` 的实际时序。
- Android/iOS 横屏触摸穿透与单次 UI 枚举开销未验证。
- 待确认：产品是否要求在最终成功动画约 1 秒多的播放期间再次检查 Popup / PlayCtrl 状态；当前实现按入口时刻判定。

### 回滚路径

恢复 `PlaySuccessLevel(...)` 使用 `CanHandleApi()`，删除本地 Popup / Playing 防护方法和新增命名空间引用，并回滚本节文档记录；无需回滚 prefab 或共享模块。

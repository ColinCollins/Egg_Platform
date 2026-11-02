# Level86Ctrl Completion Feature Notes

## Requirement / Background

2026-06-18 request: analyze and complete `Level86Ctrl` API-internal logic. The user owns Level86 prefab/API event binding; this implementation only changes bindable public method internals and Level86-local lifecycle state.

## Affected Files And Systems

- Runtime script: `Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs`
- Local docs: `Level86FeatureNotes.md`, `Level86RiskAssessment.md`
- Shared systems used without modification: `BaseLevelCtrl.SuccessStrightly()`, `ActorSpineCtrl.PlayAnimation(...)`, DOTween, I2 `LocalizationManager`, `SystemTips`
- Out of scope: `Assets/Game/Prefabs/Level/Level_086.prefab` binding, shared click/input, save/config/analytics/resource/platform systems

## Existing Behavior Baseline

- `PlayHatClickFeedback(Transform, Vector2)` played a scale tween and activated `trophy1`, but had no null guard or duplicate state tracking.
- `PlayPickUpScroll(Transform, Vector2)` triggered `scrolls.SetTrigger("pickup")` and activated `trophy2`, but `trophy2` had no null guard.
- `PlayChest(Transform, Vector2)` and `PlaySuccessLevel(Transform, Vector2)` were placeholders.
- `PlaySystemTips(Transform, Vector2)` used `S_HurryTips_Des_level_35` without a non-empty fallback.
- Static prefab evidence is incomplete and user-owned; code does not assume binding is valid.

## New Behavior

- `Awake` caches local initial state and resolves `BaseLevelCtrl` from the current object or parent.
- `OnEnable` resets Level86-local trophy/completion state and starts `StartSign` entry presentation when playing.
- `OnDisable` / `OnDestroy` kill local DOTween sequences, final success presentation, and delayed callbacks.
- Hat API now runs the current hat structure's local presentation: click pulse, local upward move/scale, and optional `hatRender` fade when assigned.
- Hat, scroll, chest, trophy4, and trophy5 APIs resolve their own trophy once and ignore duplicate reward resolution.
- Chest API runs the current chest structure's local presentation: click pulse, `ActorSpineCtrl.PlayAnimation("box-open", false, false)`, optional `shiny` spawn near chest, then delayed `trophy3` resolution guarded by the current attempt version.
- Trophy4 now follows the original script comment's Level24 reference: Level86 finds the current gameplay panel `rightMove_btn`, places `trophy4` at the button's world position, and resolves `trophy4` once the right button moves past the configured X threshold.
- `PlayTrophy5(Transform, Vector2)` is the bindable API for the break-box trophy.
- `PlaySuccessLevel(Transform, Vector2)` follows the current script structure directly: after the normal API guard, it calls `OnSuccess(trans)` without checking resolved trophy state.
- `OnSuccess` now has a final presentation: optional `signBoard` fade-out, `trophy0` move to the `signBoard`/`StartSign` reference point, `trophy0` scale-up, then `BaseLevelCtrl.SuccessStrightly()` once.
- Missing optional visual references degrade by skipping only the missing presentation path instead of throwing.

## Flow Notes

- Data/save/config/analytics/resource/platform flows: no new writes or loads.
- UI/event flow: existing click or UI events call public methods with `Transform, Vector2`; user-owned prefab binding remains required.
- Success flow: user-bound final API -> `PlaySuccessLevel` -> `OnSuccess` final presentation -> `BaseLevelCtrl.SuccessStrightly()`.
- Cleanup flow: disable/destroy kills entry sign sequence, final success sequence, final delayed callback, and invalidates delayed chest completion through `attemptVersion`.

## Verification Performed

- Static code review confirmed no `OnValidate`, `Reset`, `[ExecuteAlways]`, `[ExecuteInEditMode]`, editor GUI, DB, save, RemoteConfig, resource loading, analytics, or platform SDK paths were added.
- `dotnet build D:\GitStore\BrinPunk\BrainPunk\Assembly-CSharp.csproj --no-restore` passed after the current rescan with 0 errors and 92 existing project warnings.
- Code inspection confirmed trophy APIs use per-slot resolved flags and final success uses `successAccepted`, so duplicate reward/success dispatch is guarded statically.
- Code inspection confirmed `OnDisable` / `OnDestroy` kill local DOTween sequences and delayed success callbacks, and chest delayed resolution is attempt-version guarded.
- Quickstart scenarios were reviewed against the implementation; Unity PlayMode steps remain pending because the editor/runtime scene was not run in this pass.
- `git diff --check` passed for touched tracked files; Git reported only the existing LF-to-CRLF warning for `DevelopMD/已有功能设计与使用记录.md`.

## Manual Validation Checklist

- Fresh Level86 entry resets all trophy visuals and plays or safely skips `StartSign`.
- Hat API resolves `trophy1` once; repeat calls do not duplicate state.
- Scroll API resolves `trophy2` once and triggers `pickup` when `scrolls` exists.
- Chest API resolves `trophy3` once, attempts `box-open` when `chest` exists, and spawns `shiny` only when both `shiny` and `chest` are assigned.
- Right movement button setup places `trophy4` behind the current `rightMove_btn`; moving the right button past the configured X threshold resolves `trophy4` once. `PlayTrophy4` remains a direct API fallback when explicitly bound.
- Break-box event calls `PlayTrophy5` and resolves `trophy5` once.
- `PlaySystemTips(Transform, Vector2)` remains a separate readable-tip API when bound or called by another path.
- `PlaySuccessLevel(Transform, Vector2)` moves `trophy0` to the sign reference, scales it up, fades `signBoard` when assigned, and dispatches completion exactly once.
- Restart, fail, exit, disable, destroy, and re-entry do not leave stale tweens or delayed success callbacks.
- At least one unaffected click/touch level still passes entry, interaction, restart, and completion smoke.
- Android/iOS landscape smoke verifies user-bound touch targets and no obvious frame hitch.

## Remaining Risks / Unverified Gaps

- Unity PlayMode was not available in this pass, so actual object references, collider sizes, animation names, visual timing, and prefab binding remain pending validation.
- `ActorSpineCtrl` chest animation name `box-open` is based on the current script constant and must be confirmed against the real chest Spine asset.
- `StartSign` world-space Y values are implemented from the existing script comment and need visual confirmation in the actual Level86 scene.
- `OnSuccess` target position currently uses `signBoard.transform.position`, then `StartSign.position`, then the clicked transform as fallback; actual visual center must be checked in Unity.
- Android/iOS device smoke remains pending.

## Rollback Path

Revert `Level86Ctrl.cs` and the Level86 local documentation files. User-owned prefab binding changes, if any are made later, are outside this implementation rollback.

## 2026-06-23 signTips 渐隐补充

### 需求 / 背景

Level86 新增了告示牌提示文字 `signTips`，需求是让该文字在最终成功表现中与 `signBoard` 使用一致的渐隐逻辑。

### 影响范围

- 运行时代码：`Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs`
- 关卡资源边界：`Assets/Game/Prefabs/Level/Level_086.prefab` 当前已存在挂在告示牌下的 `Tips` 文本实例，但本次未修改 prefab。
- 共享系统：未修改 `BaseLevelCtrl`、点击系统、存档、配置、资源加载、打点、广告、内购或平台 SDK。

### 原有行为基线

- `OnSuccess` 只对 `signBoard` 执行 `DOFade(0f, finalSignFadeDuration)`。
- `ResetAttemptState` 只恢复 `signBoard` 初始 alpha。
- `CleanupRuntimeState` 只 kill `signBoard` tween。
- `signTips` 字段已有但未参与成功渐隐、重开恢复或 tween 清理。

### 新行为

- `signTips` 使用 `TMP_Text`，兼容当前 LevelTips prefab 的 3D `TextMeshPro` 组件，也兼容未来可能绑定的 UGUI TMP 文本。
- `CacheInitialState` 会记录 `signTips` 初始 alpha，并在字段未手动绑定时，从 `signBoard` 子级只读解析第一个 `TMP_Text`。
- `OnSuccess` 中 `signTips` 与 `signBoard` 使用同一个 `finalSignFadeDuration` 和 `Ease.OutQuad` 同步渐隐。
- `ResetAttemptState` 会恢复 `signTips` 初始 alpha，避免重开或重新进入后残留透明状态。
- `CleanupRuntimeState` 会 kill `signTips` 的 tween，避免退出、重开或 disable 后留下延迟动画。

### 流程说明

- UI 表现流：最终成功 API -> `OnSuccess` -> `signBoard` / `signTips` 同步淡出 -> `trophy0` 移动和缩放 -> `BaseLevelCtrl.SuccessStrightly()`。
- 数据、存档、配置、资源、事件、打点、广告、内购、平台流：本次无新增读写或派发。
- prefab 绑定流：如果 `signTips` 未序列化绑定，代码只在 `signBoard` 子级查找现有 `TMP_Text`；缺失时跳过文字淡出。

### 验证

- 静态扫描确认本次 signTips 补充未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、资源加载、打点或平台 SDK 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 0 error / 1118 warning；warning 为当前项目既有输出，未发现本次 `Level86Ctrl.cs` 编译错误。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs` 通过，仅报告 Git 的 LF 到 CRLF 工作区提示。

### 剩余风险 / 未验证项

- Unity PlayMode 仍需确认 `signTips` 实际绑定或 fallback 解析到的文本是否为预期告示牌文字。
- 仍需在 Level86 最终成功路径验证 `signBoard` 与 `signTips` 同步淡出、重开恢复 alpha、退出/disable 不残留 tween。
- Android/iOS 横屏真机或模拟器 smoke 仍未执行。

### 回滚路径

回滚 `Level86Ctrl.cs` 中 `signTips` 的 `TMP_Text` 字段、初始 alpha 缓存、子级解析、成功淡出、恢复和 tween 清理逻辑；同时回滚本节文档记录。

## 2026-06-24 signTips 数组适配

### 需求 / 背景

`Level86Ctrl.signTips` 已从单个 `TMP_Text` 修改为 `TMP_Text[]`，需要让最终成功淡出、重开恢复 alpha、disable/destroy tween 清理和 fallback 解析都按多个提示文本处理。

### 影响范围

- 运行时代码：`Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs`
- 关卡资源边界：`Assets/Game/Prefabs/Level/Level_086.prefab` 当前 `signTips` 序列化为数组，并包含两个告示牌文本引用；本次代码兼容字段未绑定时从 `signBoard` 子级解析全部 `TMP_Text`。
- 共享系统：未修改 `BaseLevelCtrl`、点击系统、存档、配置、资源加载、打点、广告、内购或平台 SDK。

### 原有行为基线

- 上一版 `signTips` 逻辑按单个 `TMP_Text` 调用 `DOFade`、读取 `color.a`、恢复 `color.a` 和 `DOKill()`。
- 当字段声明变为 `TMP_Text[]` 后，单对象调用路径不再匹配数组类型，不能覆盖多个告示牌提示文本。
- fallback 解析原先只取 `signBoard.GetComponentInChildren<TMP_Text>(true)` 的第一个文本。

### 新行为

- `OnSuccess` 通过 `JoinSignTipsFade(...)` 遍历 `signTips` 数组，所有非空文本使用同一 `finalSignFadeDuration` 和 `Ease.OutQuad` 加入同一个成功 sequence。
- `CacheInitialState` 通过 `CacheSignTipsInitialAlphas()` 为每个数组元素记录独立初始 alpha。
- `ResetAttemptState` 通过 `RestoreSignTipsAlpha()` 按索引恢复每个非空文本的初始 alpha，缺失缓存时回退为 1。
- `CleanupRuntimeState` 通过 `KillSignTipsTween()` 遍历清理每个文本上的 DOTween。
- `ResolveSignTips()` 在数组为空或全为空且 `signBoard` 存在时，使用 `GetComponentsInChildren<TMP_Text>(true)` 解析全部子级提示文本。

### 流程说明

- UI 表现流：最终成功 API -> `OnSuccess` -> `signBoard` 与 `signTips[]` 同步淡出 -> `trophy0` 移动和缩放 -> `BaseLevelCtrl.SuccessStrightly()`。
- 数据、存档、配置、资源、事件、打点、广告、内购、平台流：本次无新增读写或派发。
- prefab 绑定流：优先使用 Inspector 中的 `signTips` 数组；数组未绑定或只有空元素时，运行时从 `signBoard` 子级解析全部现有 TMP 文本。

### 验证

- 静态扫描确认本次数组适配未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、资源加载、打点或平台 SDK 链路。
- `rg` 检查确认 `signTips` 相关单对象调用已替换为数组遍历 helper。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 0 error / 1061 warning；warning 为当前项目既有输出。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs` 通过，仅报告 Git 的 LF 到 CRLF 工作区提示。

### 剩余风险 / 未验证项

- Unity PlayMode 仍需验证两个 `signTips` 文本在最终成功路径中同步淡出，并在重开、失败、退出、disable 后恢复 alpha 且不残留 tween。
- 仍需在 Inspector 或 PlayMode 中确认 `Level_086.prefab` 数组引用顺序和实际视觉对象符合策划预期。
- Android/iOS 横屏 smoke 仍未执行。

### 回滚路径

回滚 `Level86Ctrl.cs` 中 `signTips` 数组 helper、初始 alpha 数组缓存、数组 fallback 解析、成功淡出、恢复和清理逻辑；同时回滚本节文档记录。若 prefab 数组绑定由用户维护，资源回滚需按对应资源变更单独处理。

## 2026-06-23 脚本防护补充

### 需求 / 背景

本次需求是扫描 `Level86Ctrl` 当前逻辑并完善脚本防护，同时不调整现有玩法流程、公开入口、trophy 判定条件、成功路径或动画参数。

### 影响范围

- 运行时代码：`Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs`
- 关卡资源边界：静态 prefab 文本显示 `Level_086.prefab` 当前绑定了多个 `PlaySystemTips`、一个 `PlayTrophy6`、一个 `PlayChest`，本次未修改 prefab。
- 共享系统：未修改 `BaseLevelCtrl`、`ActorSpineCtrl`、点击系统、存档、配置、资源加载、打点、广告、内购或平台 SDK。

### 原有行为基线

- `OnPlayChestAsync()` 会直接访问 `chest.transform`，当 `chest` 未绑定时可能在宝箱入口抛空引用。
- `PlayShiny()` 已有 `shiny` / `chest` 空引用跳过，但没有显式运行时入口保护。
- `PlayTrophy6(Transform, Vector2)` 会直接访问 `trophy6.bodyType`，当 `trophy6` 未绑定时可能抛空引用。
- `PlaySystemTips(Transform, Vector2)` 会展示提示并对传入 `trans` 执行摇晃；当事件传入对象为空时可能抛空引用。
- `PlayHatAnim(Transform)` 的外层 DOTween `Sequence` 未显式 `SetLink(gameObject)`，disable/destroy 后生命周期绑定不如其他 sequence 明确。
- `ResetTrophy4PositionAsync(int)` 在第二段 1.5 秒等待后直接显示 `trophy4`，等待期间如果重开、退出或成功，缺少再次版本检查。
- `ResetAttemptState()` 已重置 trophy flags 和 `successAccepted`，但未重置宝箱点击去重状态 `hasChestOpen`。

### 新行为

- 宝箱入口在 `chest` 缺失时跳过点击反馈和 Spine 动画，保留原有音效、特效尝试、1 秒后解析 `trophy3` 的时序和判定路径。
- `PlayShiny()` 增加 `Application.isPlaying` 保护，避免非运行态调用生成实例。
- `PlayTrophy6()` 增加运行态与 `trophy6` 空引用保护，仍只在当前 body type 不是 `Dynamic` 时切到 `Dynamic`。
- `PlaySystemTips()` 增加运行态与 `trans` 空引用保护；提示文本仍按原路径展示，缺少点击对象时只跳过摇晃反馈。
- `PlayHatAnim()` 的外层 tween sequence 绑定到当前 `gameObject` 生命周期，和入口告示牌、成功表现 sequence 的清理边界保持一致。
- `ResetTrophy4PositionAsync()` 在第二段等待后再次检查 `Application.isPlaying`、`attemptVersion` 和 `successAccepted`，避免过期异步回调显示 `trophy4`。
- `ResetAttemptState()` 同步重置 `hasChestOpen`，避免新一次关卡尝试沿用上一次宝箱已打开状态。

### 流程说明

- UI / 点击流：仍由现有 prefab UnityEvent 调用公开 API；本次不改公开方法名称和参数。
- 成功流：仍是 `PlaySuccessLevel(Transform, Vector2)` -> `OnSuccess(Transform)` -> `BaseLevelCtrl.SuccessStrightly()`。
- Trophy 流：本次未新增 trophy 条件，也未改变 `ResolveTrophy(...)` 的 per-slot 去重逻辑。
- 数据、存档、配置、资源、事件、打点、广告、内购、平台流：本次无新增读写或派发。

### 验证

- 静态扫描确认本次补充未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、资源加载、打点或平台 SDK 链路。
- 静态 diff 检查确认公共 API 名称、成功路径、trophy 判定条件、动画常量和序列化字段未改。
- 静态 prefab 检查确认 `Level_086.prefab` 当前存在 `PlaySystemTips`、`PlayTrophy6`、`PlayChest` 等绑定证据；本次未改 prefab。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 0 error / 96 warning；warning 为当前项目既有输出，未发现本次 `Level86Ctrl.cs` 编译错误。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_86/Level86FeatureNotes.md Assets/Game/Scripts/Level/LevelItem/Level_86/Level86RiskAssessment.md DevelopMD/已有功能设计与使用记录.md` 通过，仅报告 Git 的 LF 到 CRLF 工作区提示。

### 剩余风险 / 未验证项

- Unity PlayMode 未验证缺失 `chest`、缺失 `trophy6`、空 `trans` 事件参数、快速重开、退出、disable 后的实际表现。
- Android/iOS 横屏 smoke 未执行，仍需确认快速点击、重开和成功表现没有可见残留或卡顿。
- `hasChestOpen` 重置符合本地 `ResetAttemptState()` 的“新尝试重置状态”边界，但仍需 PlayMode 验证重开后宝箱交互符合策划预期。

### 回滚路径

回滚 `Level86Ctrl.cs` 中本次新增的空引用保护、运行态保护、Trophy4 第二段异步版本检查、hat sequence 生命周期绑定和 `hasChestOpen` 重置；同时回滚本节文档记录。

## 2026-07-13 Success 状态入口防护

### 需求 / 背景

Level86 的最终交互在当前存在 Popup 界面，或 `PlayCtrl` 不处于 `PLAYING` 状态时，不应启动成功表现或进入 Success 状态，避免弹窗、暂停、加载、失败等流程期间误触发结算。

### 影响文件与系统

- 运行时代码：`Assets/Game/Scripts/Level/LevelItem/Level_86/Level86Ctrl.cs`
- 本地文档：`Level86FeatureNotes.md`、`Level86RiskAssessment.md`
- 复用但未修改：`PlayCtrl.CheckState(...)`、`GamePlayStateName.PLAYING`、Bear UI 的 `UIManager`、`BaseUIView.Layer`、`BaseUIView.IsOpen`
- 未修改：Level86 prefab、共享 `GamePlayPanel`、全局事件、存档、配置、资源加载、打点、广告、内购和平台 SDK。

### 原有行为基线

- `PlaySuccessLevel(...)` 只调用 `CanHandleApi()`，条件为运行态且 `successAccepted == false`。
- 条件通过后会立即停止入口告示牌动画并进入 `OnSuccess(...)`；`OnSuccess(...)` 设置 `successAccepted = true`，播放最终表现，然后调用 `BaseLevelCtrl.SuccessStrightly()`。
- 原路径不检查 `PlayCtrl` 当前 FSM 状态，也不检查 Popup 层是否仍有打开的界面。

### 新行为

- `PlaySuccessLevel(...)` 改为调用 Level86 本地 `CanTriggerSuccess()`。
- 只有同时满足以下条件才启动成功表现：处于 Unity 运行态、尚未接受过成功、`PlayCtrl` 存在且当前状态为 `PLAYING`、不存在仍打开的 `UILayer.Popup` 界面。
- Popup 判定只检查 UIManager 子层级内 `Layer == UILayer.Popup && IsOpen` 的 `BaseUIView`；已关闭但仍缓存的 UI 实例不会阻断成功。
- 条件不满足时直接返回，不设置 `successAccepted`、不停止入口动画、不播放最终成功表现，也不调用 `SuccessStrightly()`；后续条件恢复后仍可由正常交互再次尝试。

### 流程说明

- 成功入口：现有 UnityEvent / 关卡交互 → `PlaySuccessLevel(...)` → `CanTriggerSuccess()`。
- 状态判定：`PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING)`。
- Popup 判定：`UIManager.Instance` 子级 `BaseUIView` → `Layer` / `IsOpen`。
- 判定通过后的成功动画、音效、`BaseLevelCtrl.SuccessStrightly()`、后续存档/结算链保持原样。

### 验证

- CodeGraph 复核确认新调用链为 `PlaySuccessLevel → CanTriggerSuccess → HasOpenPopup`，没有新增外部调用方或共享模块修改。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，0 error / 1116 warning；输出中未发现本次 `Level86Ctrl.cs` 的编译错误。
- 静态检查确认防护发生在 `successAccepted` 置位和成功动画启动之前；被拦截时不会锁死后续正常成功尝试。
- 静态检查确认本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、RemoteConfig、资源加载或全局事件链路。

### 剩余风险 / 未验证项

- Unity PlayMode 尚未实际验证“无 Popup + PLAYING”正常通关、“有 Popup”拦截、PAUSE/FAILED/SUCCESS/加载态拦截，以及关闭 Popup 或恢复 PLAYING 后再次交互可正常通关。
- Popup 扫描只在最终成功交互时执行一次，不是逐帧逻辑；Android/iOS 横屏设备上的实际触摸穿透场景与扫描开销尚未真机验证。
- 防护校验发生在 `PlaySuccessLevel(...)` 被调用的时刻；成功动画播放期间若之后才打开 Popup 或切换状态，本次实现不会中途撤销已接受的成功流程，需按实际产品场景在 PlayMode 确认是否需要二次校验。

### 回滚路径

将 `PlaySuccessLevel(...)` 的入口恢复为 `CanHandleApi()`，移除 `CanTriggerSuccess()`、`HasOpenPopup()` 及新增的 Bear UI / Play 命名空间引用，并回滚本节文档记录。

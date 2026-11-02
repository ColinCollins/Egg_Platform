# Level60 V2 Feature Notes

Date: 2026-06-09

## Scope

Level60 V2 is implemented as Level60-local runtime behavior:

- `Level60Ctrl.cs` owns dream reveal orchestration, background color tween, patrol movement, and patrol click pause.
- `Level60Ctrl.cs` also owns the Level60-local pre-reveal bark cadence.
- `Level60ActorCtrl.cs` is a Level60-local actor subclass for guarding FakeActor `idle_random` from base `ActorCtrl` animation refresh when it is attached.
- `Level_060.prefab` binds existing `EnemyActor1`, `EnemyActor1 (1)`, `leftPoint`, `rightPoint`, `Bg`, `dayColor`, and `nightColor` through the existing `Level60Ctrl` component.

No shared `ActorCtrl`, `BaseLevelCtrl`, input event, save/config, analytics, resource loading, ProjectSettings, platform SDK, or non-Level60 files are intentionally changed for this feature.

## Original V1 Baseline

Evidence: `Level60Ctrl.cs` before V2 already handled:

- Entry sets `BaseLevelCtrl` actor to `FakeActor`.
- `DreamActor` starts stopped and with zero move input.
- `PlayerRightMoveEvent`, `PlayerLeftMoveEvent`, `PlayerJumpEvent`, and touch input reset `_waitingTime`.
- After `idleWaitDuration`, `dreamBubble` fades in over `bubbleFadeInDuration`.
- Interruption before completion fades the bubble out and restarts waiting.
- Completion calls `SwitchToDreamActor()`, stops `FakeActor`, resumes `DreamActor`, and calls `baseLevelCtrl.SetActor(dreamActor)`.
- Existing user change: `SetBubbleAlpha(float)` also updates `dreamActor.SpineCtrl.SetAlpha(_bubbleAlpha)`.

## Reference Evidence

- `Level_55Ctrl.cs` uses DOTween `DOColor` for background transition and kills/reset tweens during reset.
- `Level61ActorCtrl.cs` and `Level74ActorCtrl.cs` show the local actor subclass pattern: override `OnUpdate()` / `HandleAnimators()` to guard a special animation state from base actor animation refresh.
- `ActorCtrl.cs` exposes `SetMoveInput`, `UpdateAnimation`, `OnUpdate`, `ResumeMoving`, `StopMoving`, `RB`, `SpineCtrl`, and protected `HandleCollision` / `UpdateAirbornStatus` for subclass use.
- `ActorSpineCtrl.PlayAnimation(string, bool, bool)` returns null on failure and can skip replaying the current animation when `checkCurrent` is true.

## Implemented Behavior

### Dream reveal

- Reveal start calls `EnterFakeActorDreamIdle()` before bubble fade-in and starts background fade toward `nightColor`.
- If `Level60ActorCtrl` is attached to FakeActor, `Level60Ctrl` calls `EnterDreamIdle()` / `ExitDreamIdle()`.
- Current prefab still uses the shared `ActorCtrl` component for FakeActor, so `Level60Ctrl` includes a fallback that directly plays and maintains `idle_random` on FakeActor until interruption or reset.
- Reveal interruption calls `ExitFakeActorDreamIdle()` and returns `Bg` to `dayColor`.
- Reveal completion does not call `ExitDreamIdle()`, preserving the user's clarification that FakeActor remains in `idle_random`.
- Dream reveal completion no longer stops Level60 patrol updates; `Update()` only gates dream-wait logic behind `_isSwitchCompleted`.

### Patrol actors

- `patrolActors` supports the two non-player actors bound in the Level60 prefab.
- Valid slots move horizontally between `leftPoint` and `rightPoint`.
- Direction flips left after crossing `rightPoint` and right after crossing `leftPoint`.
- Missing actors, disabled actors, dead actors, or invalid/missing points are skipped safely.
- Patrol actors are manually updated by `Level60Ctrl` and are never passed to `BaseLevelCtrl.SetActor()`.
- Patrol actor `IsEditor` is forced false during initialization to avoid editor keyboard input affecting patrol actors.

### Click pause

- Public APIs:
  - `ClickPatrolActor1()`
  - `ClickPatrolActor2()`
  - `ClickPatrolActor(int index)`
  - `ClickPatrolActor(ActorCtrl actor)`
  - `OnPatrolActor1Click(Transform, Vector2)`
  - `OnPatrolActor2Click(Transform, Vector2)`
- Click pause stops horizontal velocity, requests `idle`, cancels any existing resume timer for the slot, and starts one 3-second delayed resume.
- Repeated clicks refresh the same slot timer instead of stacking resume callbacks.

### Pre-reveal bark

- `Level60Ctrl.Update()` drives one-shot bark feedback locally through `AudioManager.PlaySound("bark")`.
- Bark cadence uses a fixed 0.4 second interval while the level is in the pre-reveal wait state: dream switch is not completed, `dreamBubble` is not fading in, and the bubble alpha is fully hidden.
- Reveal start calls the local stop/reset path so no further bark plays while `dreamBubble` is appearing or visible.
- If player input interrupts reveal and the bubble fade-out completes, the bark cadence restarts for the next pre-reveal wait.
- `ApplyInitialActorState()`, `OnDisable()`, and `OnDestroy()` clear the cadence timer so restart, re-entry, disable, or destroy do not retain stale bark timing.
- Bark is presentation-only and does not change actor control, dream wait duration, bubble tween timing, patrol movement, save/unlock, analytics, or level result flows.

## Prefab Bindings

Current `Level_060.prefab` serialized evidence:

- `Bg`: `{fileID: 1400742326501192096}`
- `dayColor`: `{r: 0.99215686, g: 0.9843137, b: 0.9490196, a: 1}`
- `nightColor`: `{r: 0.24313726, g: 0.24705882, b: 0.4627451, a: 1}`
- `patrolActors[0]`: `EnemyActor1` via stripped ActorCtrl `{fileID: 6000000000000000601}`
- `patrolActors[1]`: `EnemyActor1 (1)` via stripped ActorCtrl `{fileID: 6000000000000000602}`
- `leftPoint`: `{fileID: 3824232334990283810}`
- `rightPoint`: `{fileID: 4302002403943889011}`
- `clickPauseDuration`: `3`
- `level60FakeActor`: currently `{fileID: 0}`; fallback path maintains FakeActor `idle_random` until a Unity-safe binding is added.

## Validation Notes

- Static compile check passed: `dotnet build Assembly-CSharp.csproj --no-restore` from `BrainPunk/`, result `0 errors`.
- Static compile check after adding pre-reveal bark passed: `dotnet build Assembly-CSharp.csproj --no-restore` from `BrainPunk/`, result `0 errors`.
- Unity PlayMode validation was not run in this session.
- Android/iOS landscape validation was not run in this session.

## 2026-06-24 DreamActor SkinPart alpha callback

### 需求背景

DreamActor 在首次进入 PlayMode 时会通过 `ActorSkinBinding.RefreshFromCurrentSelection()` 异步加载玩家当前 back/face/body SkinPart prefab。`Level60Ctrl` 原先在触发刷新后立刻调用 `SpriteRendererGroupAlpha.CollectRenderers()` 和 `SetAlpha(0)`，但此时异步皮肤部件可能尚未实例化，导致后生成的 SkinPart `SpriteRenderer` 没有被收集进 alpha 组，初始阶段仍以 alpha 1 显示。

### 实现行为

- `ActorSkinBinding` 新增 `CurrentSelectionApplied` 回调事件；当前保存的 full-body skin 处理完，并且 back/face/body 三个部件异步加载、清理或跳过都完成后触发。
- `ActorSkinBinding.ApplyPartAsync(...)` 从 fire-and-forget 的 `UniTaskVoid` 改为可等待的 `UniTask`，`ApplyCurrentSelectionAsync(...)` 使用 `UniTask.WhenAll(...)` 等待三个部件路径结束后再发出回调。
- `Level60Ctrl` 在启用时订阅 `skinBinding.CurrentSelectionApplied`，禁用和销毁时解绑。
- `Level60Ctrl.RecollectSkinAsync()` 仍触发现有 skin 刷新，并立即对已存在 renderer 应用当前 `_bubbleAlpha`；当 skin 回调到达后，再次 `CollectRenderers()` 并按当前 `_bubbleAlpha` 应用，覆盖异步实例化出来的 SkinPart。

### 边界

- 本次未修改 `Level_060.prefab`、保存字段、Skin 配置、资源路径、资源包规则、广告、内购、analytics 或平台 SDK。
- `CurrentSelectionApplied` 是组件局部 C# event，不是全局 Bear EventSystem 事件；订阅者只应用外部显示刷新，不应在回调内写存档或再次触发 Skin 绑定。
- `ActorSkinBinding` 仍只在运行时路径工作，原有 `Application.isPlaying` 入口保护不变。

### 验证

- `git diff --check -- Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs Assets/Game/Scripts/Level/LevelItem/Level_60/Level60Ctrl.cs` 通过；仅有 Git LF/CRLF 工作区提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 143 warning；warning 属于当前工程既有范围。

### 未验证项

- 尚未执行 Unity PlayMode：需要确认首次进入 Level60 时 DreamActor 的 full-body、back、face、body 在泡泡 alpha 为 0 时都不可见，并在泡泡淡入时同步渐显。
- 尚未执行 Android/iOS 横屏 smoke：需要确认异步加载回调和 alpha 重收集不会造成可见闪烁或卡顿。

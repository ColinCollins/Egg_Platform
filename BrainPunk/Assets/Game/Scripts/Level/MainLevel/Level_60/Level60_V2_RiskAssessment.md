# Level60 V2 Risk Assessment

Date: 2026-06-09

## Original Behavior Baseline

- Level60 originally controls `FakeActor` on entry and switches to `DreamActor` only after dream bubble fade-in completes.
- Movement, jump, and touch input reset idle waiting and cancel an in-progress reveal.
- Dream bubble fade controls SpriteRenderer, SkeletonRenderer, SkeletonGraphic, and existing DreamActor alpha behavior.
- V2 must preserve fail, restart, exit, re-enter, win, save/unlock, analytics, resource loading, and platform SDK behavior by not changing shared systems.

## Affected Files And Resources

- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_60/Level60Ctrl.cs`
- `BrainPunk/Assets/Game/Scripts/Level/Actor/Level60ActorCtrl.cs`
- `BrainPunk/Assets/Game/Scripts/Level/Actor/Level60ActorCtrl.cs.meta`
- `BrainPunk/Assets/Game/Prefabs/Level/Level_060.prefab`
- Local docs:
  - `Level60_V2_Feature.md`
  - `Level60_V2_RiskAssessment.md`

## Existing Systems Used

- `BaseLevelCtrl.SetActor()` remains used only for FakeActor/DreamActor handoff.
- `ActorCtrl.SetMoveInput()`, `ActorCtrl.OnUpdate()`, `ActorCtrl.UpdateAnimation()`, `ActorCtrl.ResumeMoving()`, `ActorCtrl.StopMoving()`, `ActorCtrl.RB`, and `ActorCtrl.SpineCtrl`.
- `ActorSpineCtrl.PlayAnimation(...)` for `idle_random` and `idle`.
- DOTween `DOColor`, `DOVirtual.Float`, and `DOVirtual.DelayedCall` with `SetLink(gameObject)`.
- Bear event subscription through existing `EventsUtils.ResetEvents(...)` and player movement/jump events.
- `Game.Scripts.Common.AudioManager.PlaySound("bark")` for one-shot pre-reveal bark feedback.

## Prefab Binding Evidence

- `Bg`, `dayColor`, and `nightColor` are serialized on `Level60Ctrl`.
- `EnemyActor1` and `EnemyActor1 (1)` are serialized as the two patrol actor slots.
- `leftPoint` and `rightPoint` are serialized on `Level60Ctrl`.
- `level60FakeActor` is not serialized to FakeActor because replacing or duplicating inherited `ActorCtrl` components in prefab YAML is risky without Unity Inspector validation. Runtime fallback keeps FakeActor dream animation behavior working with the current prefab.
- Click API methods exist, but UnityEvent click wiring was not verified in prefab during this session.

## Risk Items

- FakeActor subclass binding risk: current prefab uses shared `ActorCtrl`, so the new `Level60ActorCtrl` path is available but not active unless Unity-safe binding is added later.
- Patrol manual update risk: patrol actors now receive `OnUpdate()` from `Level60Ctrl`; PlayMode must verify this does not conflict with any other scene-level updater.
- Prefab YAML risk: current `Level_060.prefab` already had user changes before this implementation. Static YAML references compile, but Unity Inspector should confirm no Missing references.
- Click event risk: public APIs exist, but actual click event wiring depends on scene/prefab event handles and was not confirmed.
- Mobile risk: background tween, bubble tween, touch cancel, and patrol update were not tested on Android/iOS landscape devices.

## Failure-Case Checklist

- Missing `Bg`: dream reveal and actor handoff should continue without background tween.
- Missing patrol actor slot: that slot is skipped.
- Disabled/dead patrol actor: click and patrol movement skip safely.
- Missing or overlapping `leftPoint` / `rightPoint`: patrol movement is skipped.
- Repeated click pause: existing resume tween is killed before a new 3-second timer is created.
- Disable/destroy/restart: bubble tween, background tween, and patrol resume timers are killed.
- Pre-reveal bark: plays only while dreamBubble is fully hidden before reveal, stops on reveal start/visible bubble, resumes after interrupted reveal fade-out hides dreamBubble, and clears cadence on restart/disable/destroy.

## US4 Bark Validation Checklist

- Enter Level60 and confirm no stale bark cadence exists from a prior run.
- During pre-reveal waiting, confirm `AudioManager.PlaySound("bark")` repeats at 0.4 second cadence.
- When `dreamBubble` starts appearing, confirm bark stops within one interval.
- Interrupt reveal before completion and confirm bark does not play while bubble is fading out.
- After interrupted reveal hides `dreamBubble` and waiting restarts, confirm bark resumes at 0.4 second cadence.
- Restart/re-enter before reveal and confirm cadence starts cleanly without retained timer state.

## Validation Evidence

- `dotnet build Assembly-CSharp.csproj --no-restore` from `BrainPunk/` passed with `0 errors`.
- Build emitted existing project warnings; no Level60 V2 compile errors remained after changing patrol resume timer storage to `Tween`.
- `dotnet build Assembly-CSharp.csproj --no-restore` from `BrainPunk/` passed again after adding Level60 pre-reveal bark timing and `AudioManager.PlaySound("bark")`.
- Speckit checklist status: `requirements.md` was 16/16 complete before implementation.
- `.specify/extensions.yml` has no `after_implement` hooks; no post-implementation hook ran.

## Unverified Gaps

- Unity PlayMode Level60 dream reveal start, interruption, completion, and DreamActor handoff.
- Unity PlayMode background day/night transition and cancel return.
- Unity PlayMode FakeActor `idle_random` persistence after completed reveal.
- Unity PlayMode two patrol actors reversing for five cycles.
- Unity PlayMode click pause, repeated click refresh, and resume.
- Unity PlayMode pre-reveal bark 0.4 second cadence, reveal stop, interrupted-reveal resume, and restart/re-entry cleanup.
- Unity Inspector check for prefab Missing references.
- Android landscape smoke check.
- iOS landscape smoke check.
- Full lifecycle validation: enter, fail, restart, exit, re-enter, win, save/unlock, analytics observation.

## Rollback Path

Revert only Level60-local changes:

- `Level60Ctrl.cs`
- `Level60ActorCtrl.cs`
- `Level60ActorCtrl.cs.meta`
- `Level_060.prefab` Level60Ctrl serialized refs and stripped patrol ActorCtrl references
- Level60 V2 local docs if needed

Do not revert unrelated dirty workspace files unless separately requested.

## Unexpected Dirty Workspace Files

At implementation time, the workspace also had unrelated dirty files, including non-Level60 prefabs/scripts, font assets, ProjectSettings, StreamingAssets build info, Level74/Level61/Level58 files, and root Speckit/AGENTS files. They were not reverted or intentionally edited for this task.

## 2026-06-24 DreamActor SkinPart alpha callback risk record

### Original behavior baseline

- `Level60Ctrl.RecollectSkinAsync()` called `skinBinding.RefreshFromCurrentSelection()`, then immediately collected `actorGroupAlpha` renderers and set alpha to 0.
- `ActorSkinBinding.ApplyCurrentSelection()` started back/face/body part loads with `ApplyPartAsync(...).Forget()`, so skin part prefab instances could be created after `actorGroupAlpha.CollectRenderers()` had already run.
- `ActorSpineCtrl.SetAlpha(...)` affected DreamActor Spine alpha, but did not affect SpriteRenderer-based SkinPart prefab instances under back/face/body mounts.

### Affected files and systems

- `Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_60/Level60Ctrl.cs`
- Local docs:
  - `Assets/Game/Scripts/Level/LevelItem/Level_60/Level60_V2_Feature.md`
  - `Assets/Game/Scripts/Level/LevelItem/Level_60/Level60_V2_RiskAssessment.md`
- Systems used: Bear `ResourceManager`, UniTask async loading, `SpriteRendererGroupAlpha`, `ActorSkinSelectionChangedEvent` subscription path inside `ActorSkinBinding`.

### Risk items

- Shared callback risk: `ActorSkinBinding.CurrentSelectionApplied` is added to a shared component. Current usage is Level60-only, but future subscribers must not write save data or recursively call `RefreshFromCurrentSelection()` in the callback.
- Async completion risk: if a newer apply version starts while an older load is still pending, `NotifyCurrentSelectionApplied(...)` checks `_applyVersion` and skips stale callbacks; PlayMode should still verify rapid skin changes do not leave stale visible parts.
- Null/lifecycle risk: `Level60Ctrl` unsubscribes in `OnDisable()` and `OnDestroy()`, and `ActorSkinBinding` checks active state/version before notifying. Unity lifecycle still needs runtime validation.
- Visual flicker risk: Level60 now applies alpha once immediately and again after skin completion. PlayMode must confirm this avoids first-frame visible SkinPart without introducing a one-frame flash.

### Verification evidence

- `git diff --check -- Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs Assets/Game/Scripts/Level/LevelItem/Level_60/Level60Ctrl.cs` passed; only Git LF/CRLF worktree warnings were emitted.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` passed with 0 errors and 143 existing warnings.

### Unverified gaps

- Unity PlayMode Level60 first entry with saved back/face/body skin selected.
- Unity PlayMode interrupted reveal while skin part async load is still pending.
- Unity PlayMode restart/fail/exit/re-enter after callback subscription and unsubscription.
- Android/iOS landscape smoke for first-entry visibility and frame stability.

### Rollback path

- Revert `ActorSkinBinding.cs` additions: `CurrentSelectionApplied`, `ApplyCurrentSelectionAsync(...)`, `NotifyCurrentSelectionApplied(...)`, and the `UniTask` return change for `ApplyPartAsync(...)`.
- Revert `Level60Ctrl.cs` additions: `BindSkinCallbacks()`, `UnbindSkinCallbacks()`, `OnSkinBindingApplied(...)`, `RefreshActorGroupAlpha()`, and the `RecollectSkinAsync()` call adjustment.
- Remove this risk-record section and the matching feature-note section if the callback approach is abandoned.

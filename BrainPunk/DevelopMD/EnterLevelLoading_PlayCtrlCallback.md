# EnterLevelLoading PlayCtrl Callback Record

## Requirement / Background

2026-06-18 requirement: move the `EnterLevelLoading` post-erase gameplay completion content for `OnEraseComplete` into `PlayCtrl`, while keeping it as one callback action passed into `EnterLevelLoading`.

The clarification chose option A: keep one complete callback, only move its ownership to `PlayCtrl`.

## Affected Files And Systems

- `Assets/Game/UI/Global/Scripts/EnterLevelLoading.cs`
  - Owns page-turn, brush erase, fade, close, tween cleanup, and callback invocation.
- `Assets/Game/Scripts/Play/PlayCtrl.cs`
  - Owns selected-level and next-level entry, level creation callback, gameplay resume, announce display, and no-ads popup policy.
- Systems involved:
  - Bear UI `UIManager` / `BaseUIView`
  - Bear EventSystem `SwitchGameStateEvent`
  - Play state machine through `PlayCtrl`
  - `Announce`
  - `ScheduleSystem`, `DailyScheduleModule`, `InterstitialNOAdPopupAutoShowModule`, `InterstitialAdHelper`, `NoAdsPopup`

No prefab, generated UI binding, save schema, config table, resource package, platform SDK, purchase policy, ad policy asset, or analytics schema was intentionally changed for this feature.

## Existing Behavior Baseline

Evidence from local code before this migration:

- `EnterLevelLoading.StartEraseSequence()` dispatches `SwitchGameStateEvent(PAUSE)` before erase starts.
- `EnterLevelLoading.OnEraseComplete()` faded the loading UI, closed itself, dispatched gameplay resume through `SwitchGameStateEvent(PLAYING)`, showed `Announce` when the current level was not last, and attempted the no-ads popup through the daily schedule module.
- `PlayCtrl.EnterGame()` and `PlayCtrl.OnEnterNextLevel()` passed async level creation callbacks into `EnterLevelLoading.Create(...)`.
- Level-start analytics and `GameAttribute.Instance.SetLevelId(...)` remained outside the loading callback and were not moved.
- Failed level creation still opened `ChoiceLevelPanel`.
- First-level beginner popup scheduling remained inside the selected-level loading callback.

## New Behavior

- `EnterLevelLoading.Create(...)` now accepts:
  - `Func<UniTask> onPageTurnComplete`
  - `Action onEraseComplete`
- `EnterLevelLoading` invokes the erase-complete action after fade completion and `CloseSelf()`.
- If no erase-complete action is supplied, `EnterLevelLoading` falls back to dispatching `SwitchGameStateEvent(PLAYING)` so debug/GM/no-callback usages do not remain paused.
- `PlayCtrl.OnEnterLevelLoadingEraseComplete()` is the one grouped gameplay completion callback:
  - dispatch `SwitchGameStateEvent(PLAYING)`
  - create `Announce` for non-last levels
  - call existing `TryToShowNoAdPopup()`
- Both selected-level entry and next-level entry pass `OnEnterLevelLoadingEraseComplete` into `EnterLevelLoading.Create(...)`.

## Data / Resource / UI / Save / Event / Analytics Flow

- Data/config: no new config reads or schema changes. Existing `Level.CurrentLevelData` and no-ads schedule module reads remain.
- Resource: no new resource path or package dependency. Existing level creation path remains in the page-turn callback.
- UI:
  - Loading UI still owns visual transition and close lifecycle.
  - `Announce` and `NoAdsPopup` creation ownership is now in `PlayCtrl`.
  - Failed level creation fallback still opens `ChoiceLevelPanel`.
- Save: no new save writes.
- Events:
  - Loading still pauses gameplay at erase start.
  - `PlayCtrl` now resumes gameplay in the supplied erase-complete callback.
  - Loading keeps a no-action fallback resume event.
- Analytics:
  - `TrackLevelStart(...)` and `GameAttribute.Instance.SetLevelId(...)` remain at their existing call sites.
- Platform:
  - No Android/iOS platform SDK files changed.

## Verification Checklist

- Static code inspection:
  - `EnterLevelLoading` stores and invokes one erase-complete `Action`.
  - `EnterLevelLoading` no longer directly calls `Announce.Create`, `NoAdsPopup.Create`, `ScheduleSystem`, or `InterstitialAdHelper`.
  - `PlayCtrl` owns one grouped callback method, `OnEnterLevelLoadingEraseComplete`.
  - Selected-level and next-level `EnterLevelLoading.Create(...)` calls pass the same callback as an action.
  - `ClickTransformPanel.Create(...)` and other unrelated UI calls are not passed this callback.
- Manual PlayMode scenarios to run:
  - Normal selected non-last level entry: loading closes, level is playable, announce appears once, no duplicate no-ads popup.
  - Next-level entry: loading closes and next level is playable.
  - Failed level load: `ChoiceLevelPanel` appears and no softlock occurs.
  - Missing current level data in next-level flow: existing `ClickTransformPanel` fallback remains unchanged.
  - Last level: normal announce remains suppressed.
  - Level 1 with beginner popup eligibility: popup timing remains equivalent.
  - Five repeated entries: no duplicate callback effects or stuck pause.
  - Android/iOS landscape smoke: touch flow and pause/resume remain usable.

## Verification Performed

- `rg` static inspection confirmed gameplay-specific `Announce.Create`, `NoAdsPopup`, `ScheduleSystem`, and `InterstitialAdHelper` references are no longer in `EnterLevelLoading.cs`.
- `rg` static inspection confirmed both `EnterLevelLoading.Create(...)` call sites in `PlayCtrl.cs` pass `OnEnterLevelLoadingEraseComplete`.
- `git diff --check` on the two changed runtime files reported no whitespace errors.
- `dotnet build D:\GitStore\BrinPunk\BrainPunk\Assembly-CSharp.csproj --no-restore` passed with 0 errors and 1110 existing warnings.

## Remaining Risks / Unverified Gaps

- Full Unity Editor import/PlayMode compile is still recommended because the command-line C# build does not replace Editor import and runtime play validation.
- Unity PlayMode validation has not yet been run in this environment.
- Android/iOS landscape smoke has not been run in this environment.
- Exact visual timing should be checked in PlayMode because the resume event now runs inside the fade completion callback rather than the old outer method tail. This matches the stated ownership goal but needs runtime confirmation against expected player-visible timing.

## Rollback Path

To roll back this feature:

1. Remove `Action onEraseComplete` from `EnterLevelLoading.Create(...)` and the `_onEraseComplete` field.
2. Move gameplay resume, non-last-level `Announce.Create(...)`, and no-ads popup policy back into `EnterLevelLoading.OnEraseComplete()`.
3. Remove `PlayCtrl.OnEnterLevelLoadingEraseComplete()`.
4. Remove the second argument from the selected-level and next-level `EnterLevelLoading.Create(...)` call sites.
5. Re-run selected-level, next-level, failed-load, announce, no-ads, repeated-entry, and mobile landscape checks.

## 2026-06-24 更新：最后一关不自动弹出免广告弹窗

### 需求背景

本次需求要求 `PlayCtrl.TryToShowNoAdPopup()` 在当前关卡为最后一关时不弹出 `NoAdsPopup`。这是运行时广告弹窗策略调整，不改变进关转场、关卡创建、公告显示、插屏广告展示入口或购买流程。

### 影响文件与系统

- `Assets/Game/Scripts/Play/PlayCtrl.cs`
  - `TryToShowNoAdPopup()` 增加最后一关保护。
  - `ResetGame()` 和 `OnEnterLevelLoadingEraseComplete()` 仍复用同一个 `TryToShowNoAdPopup()` 入口。
- 相关系统：
  - `LevelCtrl.IsLastLevel`
  - `DailyScheduleModule`
  - `InterstitialNOAdPopupAutoShowModule`
  - `InterstitialAdHelper`
  - `NoAdsPopup`

本次未修改 prefab、存档结构、配置表、RemoteConfig、资源路径、IAP 购买流程、广告 SDK 封装或 analytics。

### 修改前行为基线

- `TryToShowNoAdPopup()` 会直接读取每日弹窗模块。
- 只要 `noAdModule.ShouldShowNoAdPopup()` 为 true 且 `InterstitialAdHelper.HasInsterstitial` 为 true，就会创建 `NoAdsPopup`、记录每日展示次数，并把 `HasInsterstitial` 置为 false。
- `OnEnterLevelLoadingEraseComplete()` 已经在最后一关抑制 `Announce.Create(...)`，但仍会继续调用 `TryToShowNoAdPopup()`。
- `LevelCtrl.IsLastLevel` 的判定来自当前关卡 id 与 `LevelSorts` 最后一条 id 的比较；关卡排序数据为空时返回 true。

### 修改后行为

- `TryToShowNoAdPopup()` 入口先判断 `Level != null && Level.IsLastLevel`。
- 当前关卡为最后一关时直接返回，不读取每日弹窗模块，不创建 `NoAdsPopup`，不调用 `RecordShow()`，也不改写 `InterstitialAdHelper.HasInsterstitial`。
- 非最后一关仍沿用原有条件：每日弹窗模块允许展示且插屏标记存在时才弹出、记录展示并消耗插屏标记。

### 数据、UI、事件与平台流

- 数据：最后一关路径不再增加 `DB.GameSchedule.Daily_interstitialNoAdPopupShowCount`；非最后一关保持原有计数逻辑。
- UI：最后一关不自动创建 `NoAdsPopup`；非最后一关自动弹窗策略保持不变。
- 事件：不新增事件，不改变 `SwitchGameStateEvent`、`Announce` 或关卡状态机切换。
- 广告/IAP：不改广告 SDK 调用和免广告购买流程；仅限制自动弹窗触发条件。
- 平台：无 Android/iOS 平台专用文件变化，移动端横屏仍需 PlayMode 或真机 smoke 补验。

### 验证记录

- 静态检查确认 `TryToShowNoAdPopup()` 的最后一关 guard 位于读取 `DailyScheduleModule`、调用 `ShouldShowNoAdPopup()`、创建 `NoAdsPopup` 和 `RecordShow()` 之前。
- 静态检查确认 `ResetGame()` 与 `OnEnterLevelLoadingEraseComplete()` 没有绕过该入口创建 `NoAdsPopup`。
- `git diff --check -- Assets/Game/Scripts/Play/PlayCtrl.cs DevelopMD/EnterLevelLoading_PlayCtrlCallback.md DevelopMD/PlayCtrl_NoAdsPopup_LastLevel_Record.md DevelopMD/已有功能设计与使用记录.md` 已通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 153 warnings；warnings 属于当前工程既有范围。

### 剩余风险与未验证项

- Unity PlayMode 尚需验证最后一关进入、最后一关重开后恢复 PLAYING、以及非最后一关仍能在满足每日计数和插屏标记时弹出免广告弹窗。
- Android/iOS 横屏 smoke 尚需验证最后一关结算、重开、返回选关和购买入口没有可见流程异常。

### 回滚路径

删除 `TryToShowNoAdPopup()` 中的最后一关提前返回逻辑，即可恢复最后一关也参与免广告弹窗自动展示判断的旧行为。

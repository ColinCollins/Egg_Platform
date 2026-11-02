# PlayCtrl 最后一关免广告弹窗保护记录

## 需求背景

2026-06-24 需求：`PlayCtrl.TryToShowNoAdPopup()` 增加规则，如果当前是最后一关则不弹出免广告弹窗。

本次是运行时广告弹窗策略调整，目标是限制自动弹窗触发条件，不改变关卡进入、重开、状态机恢复、插屏广告展示入口、免广告购买流程或每日刷新模块本身。

## 影响文件与系统

- `Assets/Game/Scripts/Play/PlayCtrl.cs`
  - `TryToShowNoAdPopup()` 增加最后一关提前返回。
  - `ResetGame()` 与 `OnEnterLevelLoadingEraseComplete()` 仍复用该入口。
- 相关系统：
  - `LevelCtrl.IsLastLevel`
  - `DailyScheduleModule`
  - `InterstitialNOAdPopupAutoShowModule`
  - `InterstitialAdHelper`
  - `NoAdsPopup`

本次未修改 prefab、存档结构、配置表、RemoteConfig、资源路径、广告 SDK 封装、IAP 购买、analytics 或平台文件。

## 修改前行为基线

- `TryToShowNoAdPopup()` 会直接读取 `DailyScheduleModule` 中的 `InterstitialNOAdPopupAutoShowModule`。
- 当 `ShouldShowNoAdPopup()` 为 true 且 `InterstitialAdHelper.HasInsterstitial` 为 true 时，会创建 `NoAdsPopup`、调用 `RecordShow()` 增加每日展示次数，并将 `InterstitialAdHelper.HasInsterstitial` 置为 false。
- `OnEnterLevelLoadingEraseComplete()` 已经在最后一关抑制 `Announce.Create(...)`，但仍会继续调用 `TryToShowNoAdPopup()`。
- `LevelCtrl.IsLastLevel` 通过当前关卡 id 与 `LevelSorts` 最后一条 id 比较判断；关卡排序数据为空时返回 true。

## 修改后行为

- `TryToShowNoAdPopup()` 在入口先判断 `Level != null && Level.IsLastLevel`。
- 当前关卡为最后一关时直接返回，不读取每日弹窗模块，不创建 `NoAdsPopup`，不调用 `RecordShow()`，也不改写 `InterstitialAdHelper.HasInsterstitial`。
- 非最后一关保持原有条件和流程：每日弹窗模块允许展示且插屏标记存在时才弹出、记录展示并消耗插屏标记。

## 数据、UI、事件与平台流

- 数据：最后一关路径不再增加 `DB.GameSchedule.Daily_interstitialNoAdPopupShowCount`；非最后一关保持原有计数逻辑。
- UI：最后一关不自动创建 `NoAdsPopup`；非最后一关自动弹窗策略保持不变。
- 事件：不新增事件，不改变 `SwitchGameStateEvent`、`Announce` 或关卡状态机切换。
- 广告/IAP：不改广告 SDK 调用和免广告购买流程；仅限制自动弹窗触发条件。
- 平台：无 Android/iOS 平台专用文件变化，移动端横屏仍需 PlayMode 或真机 smoke 补验。

## 验证记录

- 静态检查确认最后一关 guard 位于读取 `DailyScheduleModule`、调用 `ShouldShowNoAdPopup()`、创建 `NoAdsPopup` 和 `RecordShow()` 之前。
- 静态检查确认 `ResetGame()` 与 `OnEnterLevelLoadingEraseComplete()` 没有绕过 `TryToShowNoAdPopup()` 创建 `NoAdsPopup`。
- `git diff --check -- Assets/Game/Scripts/Play/PlayCtrl.cs DevelopMD/EnterLevelLoading_PlayCtrlCallback.md DevelopMD/PlayCtrl_NoAdsPopup_LastLevel_Record.md DevelopMD/已有功能设计与使用记录.md` 已通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 153 warnings；warnings 属于当前工程既有范围。

## 剩余风险与未验证项

- Unity PlayMode 尚需验证最后一关进入和重开后不会自动弹出免广告弹窗。
- Unity PlayMode 尚需验证非最后一关仍能在满足每日计数和插屏标记时弹出免广告弹窗。
- Android/iOS 横屏 smoke 尚需验证最后一关结算、重开、返回选关和购买入口没有可见流程异常。

## 回滚路径

删除 `TryToShowNoAdPopup()` 中的最后一关提前返回逻辑，即可恢复最后一关也参与免广告弹窗自动展示判断的旧行为。

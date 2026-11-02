# Postcard_ChoiceLevelStarGroupCtrl 功能记录

## 需求背景

2026-07-03 检查关卡选择界面明信片星星组逻辑时发现两个风险：刷新时对 `levelData`、`GameManager.Instance.Postcard`、`starToggles` 缺少空值保护，可能导致关卡选择项刷新中断；当前最高可进入关卡状态下使用 `anchoredPosition += stateOffset`，同一个组件多次刷新时可能重复累计位移。

## 影响文件与系统

- `Assets/Game/UI/Postcard/Scripts/Postcard_ChoiceLevelStarGroupCtrl.cs`
  - 负责关卡选择项中的明信片星星 Toggle 显示。
  - 读取 `PostcardManager.GetLevelCollectedStarIds(levelId)`。
  - 根据 `isNormal` 应用当前关卡状态的位置偏移。
- `Assets/Game/UI/LevelChoicePanel/Scripts/LevelChoiceItem.cs`
  - 通过 `RefreshState()` 调用 `starGroupCtrl.Refresh(_levelData, _state == State.Passed || _state == State.Max, _state != State.Max)`。
- `Assets/Game/UI/LevelChoicePanel/Prefab/LevelChoiceItem.prefab`
  - 当前实例覆盖中已绑定 3 个 `starToggles`，本次未改 prefab。

本次不涉及存档写入、等待动画消费、资源加载、广告、IAP、analytics、平台 SDK 或全局事件。

## 原有行为基线

- `levelData.Id`、`GameManager.Instance?.Postcard?.GetLevelCollectedStarIds(...)` 返回值、`starToggles.Count` 没有完整空值保护。
- `isNormal=false` 时每次执行 `starGroupRt.anchoredPosition = starGroupRt.anchoredPosition + stateOffset`，重复刷新会在当前坐标上继续叠加。
- 原始 `Postcard_ChoiceLevelStarGroup.prefab` 的 `starToggles` 为空列表，但 `LevelChoiceItem.prefab` 的实例覆盖当前绑定了 3 个 Toggle。

## 当前修复行为

- 每次刷新先解析并缓存初始 `RectTransform.anchoredPosition`。
- 每次刷新都基于初始位置应用状态位置：普通/已通关状态回到初始位置，当前最高可进入关卡状态使用 `初始位置 + stateOffset`。
- 每次刷新都会先清空已绑定 Toggle，并按 `isShow` 设置星组根物体显隐。
- 当 `isShow=false`、`levelData` 为空、`levelData.Id` 为空、`starToggles` 未绑定、`PostcardManager` 不可用或收集列表为空时安全返回，不阻断关卡选择界面。
- 星星显示仍按 `PostcardManager.GetLevelCollectedStarIds(levelId)` 返回的有效收集数量点亮前 N 个 Toggle，最多前三个。

## 数据、资源与 UI 流

1. `LevelChoiceItem.RefreshState()` 根据关卡状态决定是否显示星星组，以及是否传入普通位置。
2. `Postcard_ChoiceLevelStarGroupCtrl.Refresh()` 先恢复/应用位置，再清空 Toggle，并同步根物体显隐。
3. 仅在需要显示且依赖有效时读取 `PostcardManager.GetLevelCollectedStarIds(levelId)`。
4. 根据已收集星星数量点亮 Toggle。

该流程只读 `DB.Postcard.LevelCollectedStar` 的管理器封装结果，不写入 `DB.Postcard`，不消费 `LevelWaitingAnimStar`。

## 验证记录

- 已执行脚本静态阅读，确认修复点限定在 `Postcard_ChoiceLevelStarGroupCtrl.cs`。
- 已确认 `LevelChoiceItem.prefab` 当前对 `starToggles` 有 3 个实例覆盖绑定。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 首次执行 124 秒超时；随后以 300 秒超时复跑通过，结果为 0 error / 97 warnings。
- 尚未执行 Unity PlayMode、Android/iOS 横屏 smoke。

## 剩余风险与未验证项

- 未验证 Unity PlayMode 中关卡选择面板反复打开、翻页、刷新 Max 状态时星星位置是否符合视觉预期。
- 未验证 `starToggles` 在其他 prefab 或未来复用场景中的绑定顺序；当前代码只在未绑定时安全跳过，不会自动收集子 Toggle。
- 未验证 Android/iOS 横屏设备表现。

## 回滚路径

如本次修复导致关卡选择界面星星显示位置不符合预期，可回滚 `Postcard_ChoiceLevelStarGroupCtrl.cs` 中初始位置缓存、`ApplyStatePosition()` 和空值保护改动，恢复为直接按当前 `anchoredPosition + stateOffset` 处理；同时保留 prefab 绑定不变即可回到旧表现。

# 激励视频观看次数存档记录

## 需求与背景

- 需要在 `GameData` 中补充 `RewardADCount`，用于读取玩家累计实际开始观看激励视频的次数。
- Guru SDK 已有自身的 `user_rewarded_count` 与广告事件上报，但游戏业务存档此前没有可直接读取的累计次数。

## 影响文件与系统

- `Assets/Game/DB/GameData.cs`
  - 新增序列化字段 `rewardADCount`，默认值为 `0`。
- `Assets/Game/DB/GameData_Partial.cs`
  - 新档初始化计数为 `0`，并通过公开属性 `RewardADCount` 提供运行时读写。
- `Assets/Game/Scripts/SDK/Services/Ads/GameAdsController.cs`
  - 在激励视频 `OnAdDisplayed` 生命周期回调中累加计数并立即调用 `DB.SaveGameData()`。

本次不修改 Guru SDK 自带奖励成功计数、analytics 事件、广告 placement、奖励发放内容、各玩法的激励后插屏冷却时间戳、配置、UI 或资源。

## 原有行为基线

- `RewardAdHelper` 通过一次性回调门禁确保一次请求只处理一次结果。
- 成功结果会显示成功提示、更新当前玩法的激励后插屏冷却，并调用业务奖励回调。
- Guru SDK 的 `OnAdDisplayed` 已用于 `rads_imp` 展示事件上报，但 `GameData` 没有对应的累计展示次数。

## 修改后行为与数据流

```text
Guru SDK 激励广告 OnAdDisplayed
    -> DB.GameData.RewardADCount + 1
    -> DB.SaveGameData()

后续广告生命周期保持不变
    -> OnAdRewarded / OnAdHidden / 业务结果回调
```

- 统计口径为“激励广告实际开始展示”的次数，不是点击次数、加载次数或奖励成功次数。
- 展示失败不会累加；广告开始展示后即计入一次，即使后续关闭或未获得奖励也不回退。
- 主线、Draw、IWanner、商店及其它进入统一 Guru SDK 激励广告生命周期的展示共享同一个全局计数。
- 旧存档缺少整数字段时按默认值 `0` 读取；真实旧档兼容仍需样本验证。

## 验证证据

- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:quiet -m:1 /p:UseSharedCompilation=false /nr:false /clp:ErrorsOnly`
  最终通过：`0 error / 106 warning`；warning 为当前工程既有范围。
- 目标 `git diff --check` 通过，仅有工作副本 LF/CRLF 转换提示。
- 静态扫描确认当前 18 个游戏业务入口均通过 `RewardAdHelper` 进入统一广告 SDK；
  `RewardADCount` 只在 `GameAdsController` 的激励广告 `onAdDisplayedCallback` 中累加一次，
  奖励成功回调不再累加。

## 剩余风险与未验证项

- `uncertain`：尚未在 Unity PlayMode 或 Android/iOS 真机验证成功、失败、取消及 SDK 重复回调场景。
- `uncertain`：尚未使用真实旧版 `GameData` PlayerPrefs JSON 样本验证新增字段缺失时的读取结果。
- 当前依赖 SDK 对每次实际展示只派发一次 `OnAdDisplayed`；若底层重复派发，业务层没有额外展示 ID 可用于去重。
- 保存失败会记录当前内存计数，但不会阻止既有广告生命周期；本次保持广告主流程优先，不改变失败提示或补偿策略。

## 回滚路径

1. 删除 `GameAdsController` 的 `onAdDisplayedCallback` 计数入口和 `RecordRewardAdDisplayed()`。
2. 删除 `GameData` 的 `rewardADCount` 字段、初始化和 `RewardADCount` 属性。
3. 已有存档中的额外 JSON 字段可由当前读取器忽略，不需要迁移其它广告或奖励状态。

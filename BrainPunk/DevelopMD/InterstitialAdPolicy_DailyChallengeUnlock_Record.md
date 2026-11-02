# InterstitialAdPolicy DailyChallenge 首次解锁免插屏记录

## 需求与背景

- DailyChallenge 初次展示前，首次通关已达到解锁阈值的关卡时不展示胜利插屏，避免插屏打断第一次每日挑战结算体验。
- `GameData.HasShowDailyChallenge` 持久化记录结算弹窗是否已成功创建，使新用户与字段缺失的老存档进入同一初见保护流程。

## 影响文件与系统

- `Assets/Game/Scripts/Ads/InterstitialAdPolicy.cs`
  - 在 `CanShowInterstitial(...)` 的胜利广告位特殊拦截中增加 DailyChallenge 首次解锁判断。
  - 新增私有判断 `IsDailyChallengeFirstShow(...)`，读取 DailyChallenge 开关、解锁关卡、当前关首次通关状态和初见标记。
- `Assets/Game/DB/GameData.cs`、`GameData_Partial.cs`
  - 新增序列化字段 `hasShowDailyChallenge`、公开属性 `HasShowDailyChallenge`，新档初始化为 `false`。
- `Assets/Game/UI/DailyChallenge/Scripts/DailyChallengePopup.cs`
  - 有效结算弹窗成功创建并设置数据后，将 `HasShowDailyChallenge` 写为 `true` 并保存 GameData。
- 读取既有 `ConfigManager.RemoteConfig.GetDailyChallengeIsOpen()` 与 `GetDailyChallengeUnlockLevel()`；不新增或修改 RemoteConfig/Luban 字段。
- 不修改 `InterstitialAdHelper`、GuruSDK、广告 placement、事件、分析打点、资源或 prefab。

## 原有行为基线

- `PlayCtrl_Success.RunVictoryProcessAsync(...)` 先调用 `TryToAddSuccessCount()`，再通过 `InterstitialAdHelper` 请求 `InGameVictory` 插屏，之后才调用 `DailyChallengeManager.TryRecordVictory(...)`。
- 原策略已在明信片引导和评分弹窗触发关卡跳过胜利插屏，但没有 DailyChallenge 解锁保护。
- 插屏成功展示后才会通过 `PlayInterstitialAdEvent` 更新时间，并重置对应的胜利计数；策略返回 `false` 时不会触发 SDK 展示和计数重置。

## 新行为

- 仅当以下条件同时成立时，`CanShowInterstitial(...)` 返回 `false`：
  - placement 为 `InGameVictory`；
  - DailyChallenge 配置开关开启；
  - `HasShowDailyChallenge` 为 `false`；
  - 当前完成关卡大于等于 `dailyChallengeUnlockLevel`；
  - `PassedLevels` 中不包含当前关卡。
- 未达到解锁阈值的关卡、重玩关卡、DailyChallenge 关闭、已经展示过 DailyChallenge、失败/重试广告位均继续使用原策略。
- DailyChallengePopup 成功创建后立即保存初见标记；后续关卡不再因 DailyChallenge 跳过插屏。
- Popup 创建失败时不写标记，下一次有效的首次通关仍保留初见保护。
- 本次胜利计数仍按原顺序累计；由于解锁关没有成功展示插屏，计数不会在该关重置，后续胜利继续按原累计值和冷却/间隔配置判断。

## 数据与调用流

```text
PlayCtrl_Success
    -> InterstitialLevelSuccessCount + 1 并保存
    -> InterstitialAdHelper.TryToShowInterstitial(InGameVictory)
    -> InterstitialAdPolicy.CanShowInterstitial(currentLevel, InGameVictory)
        -> DailyChallenge 开关 / 解锁关配置 / HasShowDailyChallenge
        -> PassedLevels 是否包含当前关卡
        -> 初见时返回 false，不调用广告 SDK
    -> DailyChallengeManager.TryRecordVictory(currentLevel)
    -> DailyChallengePopup.Create(settlement)
        -> 成功创建并 SetData
        -> HasShowDailyChallenge = true
        -> DB.GameData.Save()
    -> GameVictoryPanel 原结算流程
```

Android 与 iOS 共用此 C# 策略分支，本次没有新增平台宏或平台专用调用。

## 验证证据

- `dotnet build Assembly-CSharp.csproj --no-restore --nologo --verbosity:minimal`：最终复验通过，0 个错误、99 个项目既有警告。
- `git diff --check -- BrainPunk/Assets/Game/Scripts/Ads/InterstitialAdPolicy.cs`：通过；Git 仅提示工作副本后续会按配置转为 CRLF。
- 静态调用链确认胜利插屏判断发生在 `DailyChallengeManager.TryRecordVictory(...)` 与 `Level.Victory()` 之前，因此本次判断读取的是解锁前的 `PassedLevels`。
- 静态分支检查确认没有修改失败广告位、冷却时间、激励广告冷却、胜利/失败计数阈值、广告成功事件或 SDK 调用；初见标记仅在 Popup 成功创建后写入。
- 存档模块静态证据确认使用 Newtonsoft `JsonConvert.DeserializeObject<T>` / `PopulateObject(...)`；新增布尔值缺失时保持类型默认值 `false`，未知额外 JSON 字段由默认反序列化配置忽略。

## 剩余风险与未验证项

- 尚未在 Unity PlayMode 使用新档与旧档验证：第一次有效结算前免插屏、Popup 创建成功后保存、后续新关恢复原插屏、重玩关不命中初见保护。
- 尚未在 RemoteConfig 动态关闭 DailyChallenge 或调整解锁阈值后验证运行时结果。
- 旧存档缺失布尔字段时按当前 Newtonsoft 反序列化链保持默认 `false`，但尚未用线上旧存档样本执行实文件兼容验证。
- 若老用户当日 DailyChallenge 已完成且没有待领奖阶段，Popup 不会创建，初见标记会继续保持 `false`，直到后续产生有效结算弹窗。
- 尚未在 Android/iOS 真机验证广告 SDK 请求日志和 DailyChallenge 弹窗衔接；平台代码未变化，但实际广告回调仍属于未验证项。

## 回滚路径

- 从 `CanShowInterstitial(...)` 移除 DailyChallenge 初见分支和 `IsDailyChallengeFirstShow(...)`，从 Popup 移除标记写入，即可恢复原广告与弹窗行为。
- 删除 `GameData` 字段、初始化和公开属性可完整回滚 schema；已经保存过该布尔键的存档需确认当前 JSON 读取器会忽略多余字段后再发布回滚版本。

# IWannerGame 插屏广告功能与风险记录

初始日期：2026-08-11
当前有效版本：2026-08-21

## 需求与背景

- IWanner 仅在 `Reset` 或关卡 `Failed` 后的重建边界尝试失败插屏，并使用独立 RemoteConfig、失败计数和冷却时间。
- 死亡和手动 Reset 各累计一次失败；死亡后的 Reset 点击不重复累计。
- IWanner 普通小关成功不请求插屏，也不累计胜利插屏计数；完整 Group 通关累计一次胜利计数，并按当前 Group 对应的 `ADRangeLevelCount` 请求胜利插屏。
- 原生广告覆盖期间不得让新关卡或玩家输入继续推进。

## 影响文件与系统

- `Assets/Game/Scripts/Ads/InterstitialAdPolicy.cs`
- `Assets/Game/Scripts/Ads/InterstitialAdHelper.cs`
- `Assets/Game/Scripts/Ads/InterstitialNOAdPopupAutoShowModule.cs`
- `Assets/Game/Scripts/Play/IWanner/IWannerPlayCtrl.cs`
- `Assets/Game/DB/DB.IWannerGame.cs`
- `Assets/Game/DB/IWannerGame.cs`
- 配置读取：`ConfigManager.RemoteConfig.GetIWannerGame*`
- 结算 UI：`IWannerGame_GameVictoryPanel` 与 `IWannerGame_GameVictoryPanel_Reward`
- 外部依赖：Guru Ads SDK、Bear SaveModule、ScheduleSystem、NoAdsPopup。

## 修改前行为基线

- IWanner 没有实现 `IInterstitialAdPolicyHost`，共享 `InterstitialAdHelper` 无法解析 IWanner 策略。
- 失败、手动 Reset、Skip 激励成功和 Group 胜利都不会更新 IWanner 插屏状态。
- Reset 会在黑幕下重建关卡，但新实例创建后立即渐隐黑幕，没有插屏等待点。
- Group 完成会立即创建 VictoryPanel，原生插屏若后接会让面板动画在广告覆盖期间推进。
- IWanner 已具备独立广告存档字段和 RemoteConfig getter，但每日免广告弹窗模块没有消费这些字段。

## 修改后行为

### 独立策略与门槛

- `IWannerInterstitialAdPolicy` 复用 `InterstitialAdPolicyBase` 的免广告、插屏 CD、激励后 CD和失败阈值算法。
- 时间戳、失败次数和完整 Group 胜利次数写入 `DB.IWannerGame`，不会修改主线或 Draw 存档。
- 失败位不受最低 Group 和胜利间隔配置影响。

### 失败与 Reset

1. Actor 死亡时累计本小关 Skip 失败次数和插屏失败次数。
2. 死亡后的 Reset 点击只触发重建，不重复累计；手动 Reset 会各累计一次。
3. 旧 GameplayPanel 先渐黑，随后销毁旧关卡和 HUD并创建新实例。
4. 新实例保持暂停、Actor/HUD 输入关闭、ClickBlock 显示和 BlackMask 不透明，然后请求 `InGameFailed`。
5. SDK 回调或资格拒绝后才渐隐黑幕、恢复输入；同步异常按未展示降级，不阻断重建。

### Skip 激励与通关

- Skip 激励成功后由共享 `RewardAdHelper` 对当前 IWanner 插屏策略调用一次 `OnRewardAdShown()`，以 IWanner 独立时间戳重置插屏冷却；失败或取消不更新冷却，`IWannerPlayCtrl` 不重复计数。
- 普通小关成功只切换到下一小关，不传入 `InGameVictory`。
- Group 末关完成后先锁定 Level/HUD、暂停 Level、显示 ClickBlock 并保存 Group 通关状态，再调用一次
  `OnLevelSucceeded()` 累计完整 Group 胜利计数，然后等待 `WaitForInterstitialAsync(InGameVictory)` 返回，最后创建首通 Reward 或复通普通 VictoryPanel。
- 首通和复通完整 Group 都累计；策略以一基 Group 顺序选择 `ADRangeLevelCount`，达到该区间阈值且通过最低 Group、免广告和冷却门禁时展示插屏。
- 成功展示胜利插屏后，共享策略清零胜利计数并更新时间戳；资格拒绝、SDK 未展示或同步异常时继续创建 VictoryPanel。

### 免广告弹窗

- 成功插屏只设置 `HasIWannerInterstitial`，不消费主线或 Draw 标记。
- 下一次 IWanner Gameplay 恢复可操作后，读取 IWanner 每日上限与独立计数并尝试创建 `NoAdsPopup`。
- 每日刷新同时清零 IWanner 计数；弹窗创建成功后才增加计数并消费标记。

## 验证证据

- `Assembly-CSharp.csproj`：`0 error / 101 warning`。
- `Assembly-CSharp-Editor.csproj`：`0 error / 4 warning`。
- 静态检查确认：死亡只在 `ShowFailureResetArea()` 计一次，死亡后的 Reset 因 `_isAwaitingFailureReset` 不重复计数；手动 Reset 在同一门禁分支计一次。
- 静态检查确认：普通小关成功不调用 `OnMiniLevelSucceeded()`；完整 Group 结算调用一次 `OnLevelSucceeded()` 并等待 `InGameVictory`。
- 静态检查确认：失败插屏位于新 LevelCtrl 初始化完成且黑幕未渐隐的位置；胜利插屏位于 Group 已结算、Level/HUD 输入锁定且 VictoryPanel 尚未创建的位置。
- Unity PlayMode、真实广告后台、离线/弱网、Android/iOS 原生覆盖和退后台恢复尚未执行，结论为 `uncertain`。

## 风险与未验证缺口

1. **P1：真实 SDK 回调依赖。** 请求已发出后仍依赖 Guru SDK 完成回调。若平台 SDK 永久不回调，Reset 会保持输入锁定，需要真机验证。
2. **P2：时间来源。** 共享策略使用设备 UTC 秒级时间戳，用户修改系统时间会影响 CD；本次保持主线/Draw 既有算法一致。
3. **P2：写盘频率。** 每次失败、激励成功和成功失败位插屏都会独立保存，失败阈值较大时可能增加 PlayerPrefs 写入次数。

## 最短验收清单

1. 把失败阈值临时设为 1：死亡后点击 Reset，确认新关卡黑幕后出现一次插屏，关闭后才渐隐并恢复输入。
2. 同一小关手动 Reset，确认只增加一次失败；死亡后 Reset 不出现双计数。
3. 连续通过普通小关，确认不累计、不请求胜利插屏；通过完整 Group，确认累计一次，并在满足当前 Group 的 `ADRangeLevelCount` 时先展示插屏、关闭后再创建 VictoryPanel。
4. 观看 Skip 激励成功后执行 Reset，确认激励后 CD 能阻止失败插屏。
5. Replay 当前 Group，确认从第 0 小关开始。
6. 覆盖无网、广告未初始化、广告失败、关闭广告时退出玩法、退后台再回前台，以及 Android/iOS 横屏触控恢复。

## 回滚路径

- 从 `IWannerPlayCtrl` 移除 `IInterstitialAdPolicyHost`、策略字段、失败计数写入和 `InGameFailed` 等待点，可恢复无 IWanner 插屏的玩法链。
- 移除 `IWannerInterstitialAdPolicy`、`HasIWannerInterstitial` 和每日模块的 IWanner 分支，可恢复共享广告模块原有主线/Draw 行为。
- IWanner 存档中的广告字段可保留；回滚逻辑不会影响 Group 通关记录，也不需要删除玩家存档。

## 2026-08-13：按普通小关通过次数补充触发（历史记录，已被 2026-08-21 要求部分覆盖）

### 历史需求与最终行为

- 曾新增 `iWannerGame_interstitialShowPassedLevelCount` 和独立存档字段用于胜利插屏补充条件。
- 当时要求 IWanner 通关不触发插屏，因此移除了普通小关和整组通关的计数与 `InGameVictory` 请求。
- 2026-08-21 当前有效要求恢复“完整 Group 通关”计数与 `InGameVictory` 请求；普通小关仍不计数、不请求，按普通小关通过次数补充触发仍保持停用。

### 数据与配置流

```text
Global.xlsx
  -> lubanconfig_tbglobalconst.json / GlobalConst.cs
  -> LubanGenerator IWanner 白名单
  -> guru_spec.yaml / GuruAllSpecs.G.cs
  -> ConfigManager.RemoteConfig
  -> IWannerInterstitialAdPolicy / DB.IWannerGame（兼容保留）
  -> IWannerPlayCtrl（仅完整 Group 通关调用胜利插屏）
```

### 验证、风险与回滚

- `Assembly-CSharp.csproj` 构建通过：`0 error / 101 warning`。
- 先补编 `Assembly-CSharp-Editor-firstpass.csproj` 后，`Assembly-CSharp-Editor.csproj` 构建通过：`0 error / 4 warning`。首次 Editor 构建失败仅因 firstpass DLL 尚未生成。
- 当时静态确认 `IWannerPlayCtrl` 不调用 `OnMiniLevelSucceeded()`、`OnLevelSucceeded()` 或 `InGameVictory`；该结论仅作为历史证据，2026-08-21 后不再描述当前整组通关行为。
- 配置静态确认 GlobalConst JSON、生成属性、运行时字段常量、LubanGenerator 白名单、Guru YAML 和 Guru 生成 C# 使用同名键；旧 `ShowTipsTimeLimit` 与 `interstitialShowMinLevel` 已从 Guru 默认载荷移除。
- 尚未执行 Unity PlayMode、真实 Guru 插屏、断网、退后台以及 Android/iOS 原生覆盖测试，回调恢复、黑幕所有权和输入恢复仍为 `uncertain`。
- 若后续清理兼容字段，可另行移除策略的小关条件、IWanner 存档字段与 RemoteConfig getter；本次不扩大到数据迁移。

## 2026-08-13：GMPanel 打开门禁的 IWanner 插屏资格 IMGUI

- 按钮和数据始终由 `IWannerGame_GamePlayPanel.OnGUI` 绘制；GMPanel 只提供“已经打开”的门禁。GM 内部 View1/View2
  收起不影响 IWanner GUI，GMPanel 关闭时按钮和数据停止绘制。
- 展示内容只保留真实参与 Reset/Failed 插屏链的输入：当前 Placement、存档/Bag/免广告门禁、失败计数/阈值、
  插屏与激励时间戳/CD/剩余 CD，以及失败位策略资格。
- 展示额外给出当前请求状态、活动控制器、策略和最近一次 Helper 回调结果，并列出失败位策略阻断项；
  不再显示或使用 `IsAdsReady`、`IsInterstitialAdReady`、`ChainReady` 这类未参与 DrawGame 调用边界的包装结论。
- 失败位来源明确为手动 `Reset` 与关卡 `Failed`。两者各只累计一次失败；`Failed` 路径先等待玩家点击重置区，
  实际插屏请求仍在新关卡重建、黑幕和输入锁已经就绪后执行。
- HUD `OnGUI` 不读取 DB、RemoteConfig 或 Manager；只有数据区展开时，`IWannerPlayCtrl` 在正常运行态每 0.25 秒生成一次快照。
  IWanner 已删除 `IsAdsReady` 提前返回，和 DrawGame 一样直接进入 `InterstitialAdHelper` 并等待结果；计数、策略公式、存档和
  黑幕/输入边界不变。
- 静态差异检查与 HUD `OnGUI` 禁止链扫描通过；`Assembly-CSharp.csproj` 构建结果为 `0 error / 101 warning`。
  Unity PlayMode 中的 GM 显隐、按钮操作、数据变化、策略结果对照和真实 SDK 回调仍为 `uncertain`。
- 调试快照的派生 Trigger/PolicyReady 和原因文本需要随 `InterstitialAdPolicyBase` 算法同步维护；Helper 回调 `false` 仍需结合
  策略阻断项和 SDK 日志判断。回滚只移除 GMPanel 打开状态、控制器快照和 HUD IMGUI，
  不回滚插屏功能、配置或存档字段。
- Unity PlayMode 热重载曾因异步回调闭包内的条件编译调试赋值产生无效 `MoveNext()` IL；当前回调只写入
  `UniTaskCompletionSource<bool>`，调试结果在 `await` 后更新。修改该异步方法结构后必须重新进入 PlayMode 再验证。

## 2026-08-21：完整 Group 通关恢复胜利插屏（当前有效）

### 当前要求与边界

- `ADRangeLevelCount` 的计数单位是“完整 Group 通关”，不是普通小关通过。
- 当前 Group 最后一小关完成门动画后，首通和复通都调用一次 `OnLevelSucceeded()`；普通小关成功只负责切关。
- 策略用当前 Group 在最终配置中的一基顺序选择 `ADRangeLevelCount`。本地回退配置为区间 `[1, 999]`、次数 `[1, 1]`，因此本地配置下每次完整 Group 通关都达到次数条件，但仍受最低 Group、免广告、插屏 CD 和激励后 CD 门禁约束；线上 RemoteConfig 可以覆盖。
- 控制器不再调用 `OnMiniLevelSucceeded()`，因此普通小关不会新增 `InterstitialPassedLevelCount`；共享策略仍保留该兼容触发分支，本地回退配置为 `-1`（关闭）。若线上重新配置为非负数，历史存档值仍可能参与胜利位资格判断。

### 结算与原生覆盖层时序

```text
完整 Group 最后一小关完成
  -> 标记 Group 已结算并锁定 Level/HUD 输入
  -> 暂停 Level、显示 ClickBlock
  -> 记录 Group 通关进度
  -> OnLevelSucceeded（完整 Group 胜利计数 +1）
  -> WaitForInterstitialAsync(InGameVictory)
  -> 策略拒绝 / SDK 回调 / 同步异常降级
  -> 校验当前激活版本
  -> 创建首通 Reward 或复通普通 VictoryPanel
```

- VictoryPanel 在广告等待结束前不创建，避免原生覆盖 Unity PlayerLoop 时结算动画在后台推进。
- 成功展示会由共享策略清零 `InterstitialLevelSuccessCount`、兼容的 `InterstitialPassedLevelCount` 并更新 `LastInterstitialDate`；未展示不清零。
- 成功广告设置的 IWanner 免广告弹窗标记不会在 `_isGroupResolved` 的结算态消费，留到下一次可操作的 IWanner Gameplay 尝试。

### 验证、风险与回滚

- 静态检查确认只有 `HandleGroupCompletionAsync()` 调用 `OnLevelSucceeded()` 和 `InGameVictory`；普通小关推进路径不调用 `OnMiniLevelSucceeded()`。
- 静态检查确认广告请求前已设置 `_isGroupResolved`、暂停 Level、关闭 HUD/Level 输入并显示 ClickBlock；广告等待返回且激活版本仍有效后才创建 VictoryPanel。
- 调试快照和 HUD IMGUI 已同步显示一基 Group、最低 Group、胜利累计次数、`ADRangeLevelCount`、兼容小关门槛、冷却、资格和阻断原因。
- `git diff --check` 目标文件通过；`Assembly-CSharp.csproj` 编译通过：`0 error / 106 warning`，警告为项目既有告警。
- Unity PlayMode、真实 Guru 插屏、断网、退后台以及 Android/iOS 原生覆盖测试仍为 `uncertain`。
- 现有等待点依赖 Guru SDK 最终回调；若 SDK 发起后永久不回调，结算将保持输入锁定且不创建 VictoryPanel，属于既有 P1 回调风险，需要真机验证或后续单独设计超时策略。
- 回滚时只需移除完整 Group 结算中的 `OnLevelSucceeded()` 和 `InGameVictory` 等待点，并恢复同步创建 VictoryPanel；普通小关、失败插屏、Group 存档与首通判断不需要回滚。

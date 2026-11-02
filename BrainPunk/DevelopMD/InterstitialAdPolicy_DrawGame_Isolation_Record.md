# 主线与 DrawGame 插屏策略隔离记录

## 需求与背景

- `GlobalConst` 新增 `drawGame_*` 提示、插屏和免广告弹窗参数。
- `InterstitialAdHelper` 原来固定读取 `PlayCtrl.Instance`、主线关卡和
  `DB.GameData` 插屏状态，无法让活动的 `DrawPlayCtrl` 使用独立策略。
- 用户确认主线与 DrawGame 的插屏 CD、激励视频后 CD、失败计数和胜利间隔计数必须
  完全分离，切换玩法不得继承、重置或覆盖另一套状态。
- 本次目标是先保持主线玩家可见行为与配置语义不变，再接入 DrawGame 独立策略。

## 影响文件与系统

- `Assets/Game/Scripts/Ads/InterstitialAdPolicy.cs`
  - 新增 `IInterstitialAdPolicy`、`IInterstitialAdPolicyHost` 和
    `InterstitialAdPolicyBase`。
  - 现有 `InterstitialAdPolicy` 保持主线配置、主线存档和主线弹窗保护规则。
  - 新增 `DrawInterstitialAdPolicy`，只读取 DrawGame 配置和存档。
- `Assets/Game/Scripts/Ads/InterstitialAdHelper.cs`
  - 从 `PlayControllerManager.ActiveController` 取得请求发起玩法的策略。
  - SDK 回调捕获原策略，不在回调时重新解析活动控制器。
- `Assets/Game/Scripts/Ads/RewardAdHelper.cs`
  - 激励视频成功后只更新请求发起玩法的奖励后插屏 CD。
- `Assets/Game/Scripts/Play/PlayCtrl.cs`
  - 实现 `IInterstitialAdPolicyHost`；继续持有主线 `InterstitialAdPolicy`。
  - 主线 `GameResetEvent` 继续累计原 `DB.GameData.InterstitialFailCount`。
- `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
  - 实现 `IInterstitialAdPolicyHost`；初始化并释放 `DrawInterstitialAdPolicy`。
  - Draw 失败或手动 Reset 累计独立失败次数并请求失败插屏。
  - Draw 胜利累计独立胜利次数并请求胜利插屏。
- `Assets/Game/DB/DrawGame.cs`、`DrawGame_Partial.cs`
  - 新增 Draw 专属上次插屏时间、上次激励时间、失败计数和胜利计数。
  - 新档初始化为 0；运行时只通过 `DB.SaveDrawGame()` 保存。
- `Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs`
  - 新增全部 `drawGame_*` 字段 key 和 getter。
  - RemoteConfig 缺失时回退到 `GlobalConst` 生成数据。
- `Assets/Game/UI/GM/Scripts/GMPanel.cs`
  - 插屏测试成功和 CD 显示改为读取当前活动玩法策略。

本次不修改广告 SDK placement、Guru SDK 配置、IAP、analytics、prefab、资源路径或平台宏。

## 原有行为基线

- 主线成功时，`LevelRuntimeState.TryToAddSuccessCount()` 增加
  `DB.GameData.InterstitialLevelSuccessCount`，随后请求 `InGameVictory`。
- 主线失败或手动重试的 `GameResetEvent` 增加
  `DB.GameData.InterstitialFailCount`，随后请求 `InGameFailed`。
- 主线策略读取 `ad_*` RemoteConfig，缺失时回退到主线 `GlobalConst` 字段。
- 主线胜利插屏继续受 Postcard 引导、Rating 和 DailyChallenge 首次解锁保护。
- 插屏成功后写入 `DB.GameData.LastInterstitialDate` 并按 placement 重置对应计数；
  激励视频成功后写入 `DB.GameData.LastRewardAdDate`。
- Draw 的 `DrawGameResetEvent` 原来刻意不进入主线广告链，Draw 胜利与失败均不请求插屏。

## 修改后行为

### 主线

- 主线仍使用 `ad_*` 配置和原 `DB.GameData` 四项插屏状态。
- 主线胜利计数入口、失败/重试计数语义、最小关卡、间隔、失败阈值、免广告判断、
  Postcard、Rating、DailyChallenge 保护均保持不变。
- 广告成功后更新时间和重置计数改为同一次 `DB.GameData.Save()`，最终持久化状态等价。
- `InterstitialAdHelper.HasInsterstitial` 继续只作为主线免广告弹窗待消费标记；
  Draw 插屏不会写入该标记。

### DrawGame

- Draw 当前关卡使用 `DrawLevelSort.Id`，即 `DrawPlayCtrl._drawLevelDisplayId`。
- Draw 策略读取：
  - `drawGame_interstitialCD`
  - `drawGame_rewardResetInterstitialCD`
  - `drawGame_interstitialShowMinLevel`
  - `drawGame_interstitialShowIntervalCount`
  - `drawGame_interstitialShowFailedCount`
- Draw 状态只读写：
  - `DB.DrawGame.LastInterstitialDate`
  - `DB.DrawGame.LastRewardAdDate`
  - `DB.DrawGame.InterstitialFailCount`
  - `DB.DrawGame.InterstitialLevelSuccessCount`
- Draw 成功插屏只重置 Draw 胜利计数；Draw 失败插屏只重置 Draw 失败计数。
- 主线广告、激励视频、胜败结果不会改变 Draw 状态，Draw 的对应行为也不会改变主线状态。
- SDK 重复回调由 Helper 一次性门禁忽略，避免重复更新时间、保存或回调业务。

## RemoteConfig 与数据流

```text
ActiveController
    -> IInterstitialAdPolicyHost
        -> Main: InterstitialAdPolicy + DB.GameData + ad_*
        -> Draw: DrawInterstitialAdPolicy + DB.DrawGame + drawGame_*
    -> InterstitialAdHelper
        -> CanShowInterstitial(currentLevel, placement)
        -> GameSDKService.ShowInterstitial(...)
        -> 成功回调捕获原 Policy
        -> Policy.OnInterstitialShown(placement)

RewardAdHelper
    -> 捕获请求发起时的 ActiveController Policy
    -> 激励成功
    -> Policy.OnRewardAdShown()
```

- 八个 `drawGame_*` 字段均具备 RemoteConfig getter 和 GlobalConst fallback。
- `drawGameShowTipsFailCount`、`drawGameShowTipsTimeLimit` 和
  `drawGame_dailyNOAdPopupAutoShowCount` 在本节对应的初次改动中只补齐统一配置读取入口；
  其中 Draw 免广告弹窗已于 2026-08-06 接入，当前行为见本文末尾专项记录。

## 验证证据

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal
  /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`
  最终通过：`0 error / 102 warning`；warning 为当前工程既有范围。
- `git diff --check` 对目标代码通过，仅有工作副本 LF/CRLF 转换提示。
- 静态调用链确认：
  - 主线 `PlayCtrl_Success` 仍先增加原胜利计数再请求胜利插屏。
  - 主线 `PlayCtrl.OnGameResetEvent` 只写主线策略。
  - Draw `OnGameResetEvent` 只写 Draw 策略并请求失败插屏。
  - Draw `OnLevelResolved(Success)` 只写 Draw 策略并请求胜利插屏。
  - `RewardAdHelper` 和 `InterstitialAdHelper` 都捕获请求发起时的活动策略。
- 静态搜索确认没有遗留 `InitEvents()`、`ResetFailCount()`、`ResetSuccessCount()` 或
  `PlayRewardADEvent` / `PlayInterstitialAdEvent` 的策略状态写入调用。

## 剩余风险与未验证项

- `uncertain`：尚未使用真实旧版 DrawGame PlayerPrefs 样本验证新增标量字段缺失时的
  反序列化结果；按当前字段类型默认值应为 0，但仍需旧档实测。
- 尚未在 Unity PlayMode 验证主线和 Draw 的最小关卡、胜利间隔、失败阈值、插屏成功、
  SDK 失败和重复回调。
- 尚未验证“主线看完插屏立即切 Draw”以及反向切换时两套 CD 互不影响。
- 尚未使用真实激励视频验证两套 `LastRewardAdDate` 独立更新。
- 尚未在 Android/iOS 真机验证广告遮罩、切后台、SDK 回调线程和横屏恢复。
- Draw 每日免广告弹窗的独立计数与触发点已于 2026-08-06 实现，仍需 PlayMode 与真机验证。
- 策略仍使用设备 UTC 时间，修改系统时间可能影响两套 CD；本次没有改变既有时间方案。

## 回滚路径

1. 让 `InterstitialAdHelper`、`RewardAdHelper` 恢复固定读取主线 `PlayCtrl.Instance`。
2. 将 `InterstitialAdPolicy` 恢复为主线单一实现，并恢复原事件订阅和计数重置入口。
3. 从 `DrawPlayCtrl` 移除 `IInterstitialAdPolicyHost`、Draw 胜败计数和插屏请求。
4. 删除 `DrawGame` 四项插屏字段及访问器；已保存的额外字段需先确认当前读取器会忽略。
5. 删除 Draw RemoteConfig getter 即恢复为只存在 GlobalConst 生成字段但运行时不读取。

## 2026-08-03 Draw 失败插屏与 Reset 恢复时序

- 修改前 Draw 失败/手动 Reset 在 `OnGameResetEvent()` 中先请求插屏，再启动 0.5 秒黑幕等待和整关重建。
  原生广告暂停 Unity PlayerLoop 时，无缩放时间等待也不会推进，广告返回会先露出旧死亡 Actor。
- 当前保持 Policy 计数、placement、成功状态保存和请求发起方捕获语义不变，只把实际失败插屏请求移动到
  新 DLevel、存活 Actor、全黑新 HUD 和 Reset Intro 已准备完成之后。广告结果返回后再派发既有 HUD
  FadeIn 并恢复输入；策略拒绝或 SDK 失败通过原回调立即继续。
- Victory 插屏、RewardAd、主线 Policy 和主线 Reset 未修改。Unity CLI 编译为 `failed:false / errors:[]`；
  真实 Android/iOS 插屏成功、拒绝、SDK 不回调、横屏恢复和广告期间切后台仍为 `uncertain`。
- 回滚为恢复 Draw Reset 的即时失败插屏调用，并删除 Draw Reset 重建链的等待回调，不涉及 Policy、DB、
  RemoteConfig 或玩家数据迁移。

## 2026-08-06 Draw 插屏后自动免广告弹窗

### 需求与原有行为

- 需求是让 DrawGame 在成功播放插屏后，按主线相同的自动免广告弹窗语义执行，但所有控制参数和每日计数
  使用 Draw 自己的数据。
- 修改前 Draw 胜利、失败和手动 Reset 已能通过 `DrawInterstitialAdPolicy` 请求插屏；插屏成功只更新 Draw
  的冷却和失败/胜利计数，不会设置主线 `InterstitialAdHelper.HasInsterstitial`。
- 修改前只有主线 `PlayCtrl.TryToShowNoAdPopup()` 会在进关、Reset 和 Replay 恢复可操作状态后创建
  `NoAdsPopup`；Draw 仅有 `drawGame_dailyNOAdPopupAutoShowCount` getter，没有待消费标记、独立每日计数
  或触发入口。

### 影响文件与系统

- `Assets/Game/Scripts/Ads/InterstitialAdHelper.cs`
  - 新增 Draw 专属 `HasDrawInterstitial` 待消费标记；仅 Draw 请求的成功插屏设置该标记。
  - 主线仍只设置和消费原 `HasInsterstitial`，两种玩法不互相覆盖。
- `Assets/Game/Scripts/Ads/InterstitialNOAdPopupAutoShowModule.cs`
  - 保留主线原方法和 `DB.GameSchedule` 计数；新增 Draw 专属判断与记录方法。
  - Draw 上限读取 `GetDrawGameDailyNOAdPopupAutoShowCount()`，次数读取 `DB.DrawGame`。
  - 复用既有 Daily 刷新生命周期，同时分别保存主线和 Draw 计数。
- `Assets/Game/DB/DrawGame.cs`、`DrawGame_Partial.cs`
  - 新增 `dailyInterstitialNoAdPopupShowCount` 及运行时属性，新档初始化为 0；旧档缺字段时按整型默认值 0
    处理，实际旧档兼容仍需验证。
- `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
  - `CompleteIntroAndBeginDrawing()` 在进关、下一关、失败重建和胜利 Replay 恢复可操作状态后统一尝试弹窗。
  - 最终有效排序项不自动弹窗；排序不可用时按最终关处理，避免错误弹出。

本次未修改 `NoAdsPopup` prefab、IAP 商品、广告 placement、Draw 插屏 Policy 参数、analytics、资源路径、
全局事件或平台宏。

### 修改后行为与数据流

```text
Draw 插屏成功
    -> InterstitialAdHelper.HasDrawInterstitial = true
    -> Draw 进入下一次可操作状态
    -> DrawPlayCtrl.TryToShowNoAdPopup()
        -> 非最终有效排序项
        -> drawGame_dailyNOAdPopupAutoShowCount > 0
        -> DB.DrawGame.DailyInterstitialNoAdPopupShowCount 未达上限
        -> 玩家未拥有 NoAds
        -> NoAdsPopup.Create()
        -> Draw 每日次数 +1 并 DB.SaveDrawGame()
        -> 清除 HasDrawInterstitial
```

- 失败/手动 Reset 仍先在全黑、新 DLevel 和存活 Actor 已准备后等待插屏回调；广告返回、HUD 淡入并恢复
  Drawing 后才尝试免广告弹窗，不假设原生覆盖层期间 Unity PlayerLoop 会推进。
- 胜利插屏成功后标记保留到玩家进入下一关或 Replay 并恢复 Drawing 时消费，不覆盖 Draw VictoryPanel。
- 配置上限小于等于 0、每日次数已满、玩家已拥有 NoAds、插屏失败或策略拒绝时均不创建弹窗。
- 最后一关抑制弹窗时不消费待显示标记，与主线最后一关保护一致；以后进入非最终关且条件允许时仍可消费。
- 主线每日计数继续保存在 `DB.GameSchedule`，Draw 每日计数保存在 `DB.DrawGame`；Daily 调度只共享日界线和
  刷新时机，不共享配置值或展示次数。

### 验证证据

- CodeGraph 当前调用链确认：`CompleteIntroAndBeginDrawing()` -> `TryToShowNoAdPopup()` ->
  `ShouldShowDrawNoAdPopup()`；Draw 判断和记录方法各只有一个 Draw 调用方。
- 静态源码确认成功回调按 `PlayModeType.Main` / `PlayModeType.Draw` 分别设置两个待消费标记。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1
  /nr:false /p:BuildInParallel=false` 完整通过：`0 error / 1119 warning`；warning 为当前工程既有范围。
- `git diff --check` 对本次代码与文档通过，仅有工作副本 LF/CRLF 转换提示。

### 剩余风险与未验证项

- `uncertain`：尚未用真实旧版 DrawGame PlayerPrefs 样本验证新增整型字段缺失时的反序列化结果。
- `uncertain`：尚未在 Unity PlayMode 验证失败插屏、胜利插屏、下一关、Replay、最后一关、每日上限、
  已购买 NoAds 和主线/Draw 来回切换的组合矩阵。
- `uncertain`：尚未在 Android/iOS 真机验证原生广告返回后的遮罩、输入恢复、横屏和切后台行为。
- 为对齐主线，当前仍在调用 `NoAdsPopup.Create()` 后直接记次并清标记；若 UI 创建失败，无法从返回值区分，
  会沿用主线的“视为已展示”行为。
- Daily 刷新继续使用设备当地自然日；玩家修改系统日期或时区会影响两种玩法的刷新时机，但不会混用计数。

### 回滚路径

1. 从 `DrawPlayCtrl.CompleteIntroAndBeginDrawing()` 移除 Draw 自动弹窗调用和对应私有判断方法。
2. 删除 `InterstitialAdHelper.HasDrawInterstitial` 及 Draw 成功插屏赋值分支。
3. 删除免广告模块的 Draw 判断、记录和每日重置写入，保留主线原方法。
4. 删除 DrawGame 新增每日计数字段、初始化和属性；旧存档中的额外字段无需迁移即可由读取器忽略。
5. `drawGame_dailyNOAdPopupAutoShowCount` getter 可保留供以后使用，不影响现有运行时。

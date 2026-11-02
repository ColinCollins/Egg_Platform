# GameVictoryPanel_Reward 广告奖励完成状态记录

## 需求背景

奖励版胜利面板点击 `AdReward` 后，激励广告播放成功并完成奖励发放时，`AdReward` 按钮需要保留在界面上但变灰；面板不能自动进入下一关，玩家仍通过下一关按钮继续流程；已完成的 `AdReward` 不能再次点击或再次触发广告。

## 影响范围

- `Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel_Reawrd.cs`
- `Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel.cs`
- `Assets/Game/UI/GameVictory/Resources/GameVictoryPanel_Reward.prefab`
- `Assets/Game/UI/Global/Scripts/CustomGrayRender.cs`

本轮代码只修改 `GameVictoryPanel_Reawrd.cs`。当前工作区 `GameVictoryPanel_Reward.prefab` 文本显示 `reward_btn` 已挂载 `CustomGrayRender` 并绑定到 `GameVictoryPanel_Reawrd.grayRender`；该 prefab 改动不是本轮新增。`GameVictoryPanel.cs` 当前工作区已有 `OnNextLevel(...)` 使用 `UIManager.Instance.DestroyUI(this)` 的改动，本轮未修改。

## 原有行为基线

- `GameVictoryPanel_Reawrd.OnClickAdReward(...)` 点击后调用 `RewardAdHelper.TryToShowRewardAd(RewardPlacement.GameVictory)`。
- `_isRewardAdRequesting` 与 `_isAdRewardPlaying` 会阻止广告请求或奖励结算进行中的重复点击。
- `OnRewardAdCallback(...)` 成功后会调用 `PlayRewardFlowAsync()`，随后给背包发放当前档位金币并播放奖励收集动画。
- 目标脚本中旧的 `OnNextLevel(null)` 自动进下一关逻辑已处于注释状态，不会执行。
- `_isAdRewardComplete` 已存在，但点击入口没有显式拦截完成态；新一轮打开面板时也缺少对 `grayRender` 的非灰态恢复。

## 新行为

- `ResetOpenStateExtra()` 在每次打开奖励版胜利面板时调用 `grayRender.SetGray(false)`，避免上一轮广告完成后的灰态残留到下一次打开。
- `RefreshButtonStates()` 使用 `_isAdRewardComplete` 控制 `AdRewardBtn.Interactable`；广告完成后按钮不可交互。
- `OnClickAdReward(...)` 首先检查 `_isAdRewardComplete`，即使按钮状态异常或被代码直接触发，也会直接返回，不再请求广告。
- `OnRewardAdCallback(...)` 广告成功后设置 `_isAdRewardComplete = true`、调用 `grayRender.SetGray(true)`，并立即刷新按钮状态，让按钮保留显示但变灰且不可点。
- 广告成功后的奖励流程只发放金币、播放奖励动画、刷新 `propBlock`；不调用 `OnNextLevel(null)`，不直接进入下一关。

## 数据、资源、UI、事件和平台流程

- 广告请求仍走既有 `RewardAdHelper.TryToShowRewardAd(...)` 与 `RewardPlacement.GameVictory`。
- 奖励发放仍走 `PlayCtrl.Instance.Bag.AddTool(Config.Game.GameProps.Coin, coins, RewardType.Ad)`。
- 下一关仍只由 `GameVictoryPanel.OnNextLevel(...)` 对应按钮触发，并派发既有 `EnterNextLevelEvent`。
- 本次未新增 `PlayEvents`、存档字段、RemoteConfig 字段、Luban 配置、资源加载路径、analytics、IAP 或平台 SDK 流程。
- 灰度表现依赖当前 prefab 上绑定的 `CustomGrayRender`；若 prefab 未绑定，逻辑仍能通过 `_isAdRewardComplete` 禁止再次点击，但不会出现置灰视觉。

## 验证

- 静态检查确认 `GameVictoryPanel_Reawrd.cs` 中 `OnRewardAdCallback(...)` 成功路径没有执行未注释的 `OnNextLevel(null)`。
- 静态检查确认 `_isAdRewardComplete` 同时作用于点击入口和 `AdRewardBtn.Interactable`。
- 静态检查确认当前 prefab 文本存在 `grayRender` 绑定，且 `reward_btn` 上有 `CustomGrayRender` 组件。
- `git diff --check -- Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel_Reawrd.cs Assets/Game/UI/GameVictory/GameVictoryPanel_Reward_AdRewardComplete_Record.md Assets/Game/UI/GameVictory/GameVictoryPanel_Reward_AdRewardComplete_Record.md.meta DevelopMD/已有功能设计与使用记录.md` 已通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 97 warnings。
- 尚未完成 Unity Editor PlayMode、广告 SDK 成功/失败回调、Android/iOS 横屏触摸验证；验证结果待补充。

## 剩余风险与未验证缺口

- 未在 PlayMode 验证广告成功后按钮实际置灰、不可再次点击，且下一关按钮仍可手动进入下一关。
- 未验证广告失败后奖励指针恢复摆动、按钮仍可再次点击。
- 未验证 `CustomGrayRender` 在当前 `reward_btn` 子节点上的实际灰度效果与文本颜色表现。
- 未验证 Android/iOS 横屏触控、广告 SDK 回调时序、UI 栈临时隐藏后恢复状态。

## 回滚路径

- 移除 `GameVictoryPanel_Reawrd.cs` 中 `_isAdRewardComplete` 对 `OnClickAdReward(...)` 的完成态拦截。
- 移除 `ResetOpenStateExtra()` 的 `grayRender.SetGray(false)` 和 `OnRewardAdCallback(...)` 的 `grayRender.SetGray(true)` / `RefreshButtonStates()` 调用。
- 若要恢复旧的广告完成后隐藏按钮或自动进下一关行为，需要同步更新本记录并重新验证奖励、下一关和重复点击路径。

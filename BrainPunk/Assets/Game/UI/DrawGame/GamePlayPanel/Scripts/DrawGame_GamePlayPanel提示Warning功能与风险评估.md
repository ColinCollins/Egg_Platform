# DrawGame_GamePlayPanel 提示 Warning 功能与风险评估

## 需求与原行为

- 需求：Draw 玩法的 `tips_btn` 需要像主线 `GamePlayPanel` 一样，在玩家等待过久或累计重试达到阈值时显示 `warning`，点击 Tips 后隐藏。
- 修改前 Draw HUD prefab 已有默认 inactive 的 `warning` 图片与 `RotateFloatHandle`，但 `tips_btn` 没有挂载 `GamePlayPanelTipsSwitcher`，因此该节点不监听 `GamePlayPanelSwitchTipsEvent`。
- `ConfigManager.RemoteConfig` 已提供 Draw 专属的 `drawGameShowTipsTimeLimit` 与 `drawGameShowTipsFailCount`，本地默认分别为 20 秒和 5 次，但 `DrawPlayCtrl` 原先没有消费这两个阈值。

## 影响文件与系统

- `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`：维护当前尝试的 warning 等待时间与一次性门禁，读取 Draw 专属阈值并派发既有 `GamePlayPanelSwitchTipsEvent`。
- `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel.cs`：点击 Tips 时派发隐藏事件，再沿用原入口打开 `DrawGame_GameTipsPopup`。
- `Assets/Game/UI/DrawGame/GamePlayPanel/Resources/DrawGame_GamePlayPanel.prefab`：在 `tips_btn` 上复用主线 `GamePlayPanelTipsSwitcher`，其 `target` 绑定现有 `warning` 节点。
- 只读取既有 RemoteConfig/GlobalConst 和当前 Draw 尝试的运行时计时、revive 计数；不新增配置、存档字段、资源地址、事件类型、analytics、广告、IAP 或平台 SDK 分支。

## 修改后行为与数据流

```text
进入新关 / Reset / Victory Replay
  -> 清零当前尝试 warning 计时与一次性门禁
  -> 读取 Draw 专属时间、重试阈值
  -> Drawing 且未暂停时累计 warning 等待时间
  -> 等待时间超过阈值，或当前关累计 revive 达到阈值
  -> DrawPlayCtrl 派发 GamePlayPanelSwitchTipsEvent(true)
  -> tips_btn/GamePlayPanelTipsSwitcher 将 warning 从缩放 0 展开到 1
  -> 展开完成后默认展示 3 秒并直接隐藏
  -> 玩家点击 Tips
  -> DrawGame_GamePlayPanel 派发 GamePlayPanelSwitchTipsEvent(false)
  -> 打开原 DrawGame_GameTipsPopup
```

- 时间只在 `DrawPlayPhase.Drawing`、控制器未暂停且输入尝试没有处于加载/切换状态时累计，不把 Intro、转场、Popup 暂停时间算入 warning 等待时间。
- 达到任一阈值后，同一次尝试只派发一次显示事件；玩家点击 Tips 后即使关闭弹窗而未获取提示，也不会在同次尝试重复亮起。
- 共享 switcher 当前默认使用 `0.5` 秒展开和展开后 `3` 秒展示；自动隐藏后不会清除 Draw 当前尝试的一次性门禁，
  因此同次尝试不会再次派发显示事件。显式隐藏和自动隐藏均不播放收起动画。
- Reset/失败重试开启新尝试时重置一次性门禁；累计 revive 继续沿用现有 `_analyticsReviveCount`，没有改变其增加、清零、存档或打点口径。
- 成功获取 Draw 提示时再次派发隐藏事件，并继续由 `DrawGameHintEvent` / `DrawPlayCtrl.HasTips` / `IDrawLevelRuntime.ShowHint(true)` 处理原提示展示。
- DLevel_001 是明确的关卡特例：`DrawPlayCtrl` 依据标准化资源路径隐藏整个 `TipsBtn`，并跳过本关 warning 阈值检查；Reset/Replay 重建 HUD 后仍保持隐藏，进入其它 Draw 关卡时恢复显示。

## 风险与验证

- 静态绑定要求：`tips_btn` 上的 `GamePlayPanelTipsSwitcher.target` 必须指向 `warning`；`RotateFloatHandle` 是可选表现，
  当前 Draw prefab 仍保留该组件，显示时会沿用原复位逻辑。
- 已执行目标 prefab 静态检查：187 个 YAML anchor 无重复，switcher 组件、脚本 GUID、warning target、默认 inactive 与 `RotateFloatHandle` 引用均存在。
- 本次目标代码、prefab 与中文文档的 `git diff --check` 通过，仅有工作区换行符提示。
- `Assembly-CSharp.csproj` 单进程构建通过，结果为 `0 error / 101 个工程既有 warning`，warning 未指向本次修改文件。
- `GamePlayPanelSwitchTipsEvent` 是主线与 Draw 共用的既有 UI 事件；当前发送方均受各自活动玩法生命周期约束，仍需在玩法快速切换时通过 PlayMode 确认不会出现旧 HUD 短暂响应。
- 阈值缺失时 getter 会按既有逻辑回退 GlobalConst；若最终得到 0，行为与主线一致，会在首次有效 Tick 立即满足阈值。
- `uncertain`：Unity PlayMode 的 20 秒触发、5 次 Reset/失败触发、暂停不计时、点击隐藏、获取提示后隐藏、Reset 后重新出现，以及 Android/iOS 横屏显示和触控仍待手工验证。
- `uncertain`：DLevel_001 首次进入、Reset、失败重试、Victory Replay 的隐藏状态，以及切换到其它 Draw 关卡后的恢复仍待 PlayMode 验证。
- DLevel_001 特例补充后的 `Assembly-CSharp.csproj` 构建仍为 `0 error / 101 个工程既有 warning`，目标差异检查通过。

## 回滚路径

- 从 Draw HUD `tips_btn` 移除 `GamePlayPanelTipsSwitcher` 组件，保留既有 `warning` 美术节点即可恢复为无自动控制状态。
- 删除 `DrawPlayCtrl` 的 warning 字段、`PrepareTipsWarningForAttempt()`、`UpdateTipsWarning(...)` 及其调用点。
- 删除 `DrawGame_GamePlayPanel.OnClickTips()` 中的隐藏事件派发；Draw 提示弹窗、Draw 提示获取、打点与存档无需迁移。
- DLevel_001 特例可单独回滚：删除控制器资源路径门禁和 `SetTipsButtonVisible(bool)` 调用，不需要改动 HUD prefab。

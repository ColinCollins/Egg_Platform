# DrawGame_GamePlayPanel 倒计时数字动画功能与风险评估

## 1. 需求与原行为基线

- 需求：`DrawLevelCountdownCtrl` 控制的最后五秒倒计时数字每次变化时都播放一次放大、
  缩小回原尺寸的动画，增强数字切换反馈。
- 原行为：`DrawLevelCountdownCtrl` 只在剩余整数秒变化时派发
  `DrawGameCountdownChangedEvent`；`DrawGame_GamePlayPanel` 收到事件后仅更新 `cd_txt`
  文本与显隐，没有数字切换动画。

## 2. 受影响文件、资源与系统

- 修改：
  `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel.cs`。
- 复用但未修改：
  `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCountdownCtrl.cs`、
  `DrawGameCountdownChangedEvent`、DOTween、
  `Assets/Game/UI/DrawGame/GamePlayPanel/Resources/DrawGame_GamePlayPanel.prefab` 中的
  `cd_txt` 绑定。
- 没有修改 DLevel prefab、计时配置、胜负逻辑、DB、存档、Luban、资源地址、全局事件、
  analytics、广告、IAP、SDK 或平台分支。

## 3. 新行为与 UI / 事件流

- 事件流保持为：
  `DrawLevelCountdownCtrl -> DrawGameCountdownChangedEvent
  -> DrawGame_GamePlayPanel.SetCountdownVisible(...) -> cd_txt`。
- 每次 HUD 收到可见的倒计时数字更新时，先写入新数字，再让文本从原始缩放放大到
  `1.35` 倍，随后缩回原始缩放；放大阶段为 `0.12s / Ease.OutQuad`，回缩阶段为
  `0.18s / Ease.OutBack`，单次总时长约 `0.3s`。
- HUD 首次初始化时缓存 `cd_txt` 的原始 `localScale`，不假设其必须为 `Vector3.one`；
  当前 prefab 静态值为 `(1, 1, 1)`。
- 新数字到达前会杀掉上一段缩放 Tween 并恢复原始缩放，避免连续刷新累积比例。
  倒计时隐藏或面板关闭时也会清理 Tween、恢复原始缩放并隐藏文本。
- 动画使用 `SetUpdate(true)`，即使游戏在动画中途暂停，也能在约 `0.3s` 后回到原始尺寸，
  不会因 `timeScale=0` 长时间停留在放大状态。
- 所有使用共享 `DrawGame_GamePlayPanel` 且通过既有事件显示倒计时的 Draw 关卡都会获得
  相同表现；倒计时仍只在最后五秒显示，扣时、暂停和归零结算行为不变。

## 4. 验证证据

- `git diff --check --
  Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel.cs` 通过。
- 静态确认 `DrawGame_GamePlayPanel.prefab` 的 `countdownText` 仍绑定 `cd_txt`，其
  RectTransform 原始缩放为 `(1, 1, 1)`；本次没有修改 prefab。
- 修改后 Unity 6000.0.59f2 自动刷新并记录
  `*** Tundra build success (4.24 seconds), 9 items updated, 2691 evaluated`，
  `Library/ScriptAssemblies/Assembly-CSharp.dll` 的生成时间晚于修改后的脚本；该次编译已包含
  本文件的 HUD 修改。
- 该次成功之后，当前工作区另一份未纳入本需求的
  `Assets/Game/Scripts/Level/DrawLevel/Level17/DLevel17Ctrl.cs` 出现
  `AudioManager` 未解析的 `CS0103`，因此最终全工程编译状态不是全绿；错误没有指向本次修改文件，
  本次未改动或代为修复 DLevel17。
- 独立 `dotnet build Assembly-CSharp.csproj --no-restore` 无法运行，因为当前终端只安装了
  .NET Runtime、没有 .NET SDK；Unity 编辑器编译结果作为本次编译证据。

## 5. 剩余风险与未验证缺口

- `uncertain`：尚未在 Unity PlayMode 实际观看 `5 -> 4 -> 3 -> 2 -> 1` 的完整节奏，
  `1.35` 倍峰值与 `0.3s` 时长仍需美术/策划验收。
- `uncertain`：Android/iOS 横屏真机上的字体边缘、不同安全区和 60 fps 动画平滑度尚未验证。
- `uncertain`：需在 DLevel17 的并行编译错误解决后，再执行一次最终全工程 Unity 编译复核。
- 倒计时数字 Text 节点尺寸为 `150 x 150`；1.35 倍缩放可能接近周边 UI，静态绑定未发现
  布局改动，但最终遮挡情况仍需 PlayMode 和目标分辨率确认。

## 6. 回滚路径

- 从 `DrawGame_GamePlayPanel.cs` 删除三个倒计时动画常量、原始缩放缓存和 DOTween 序列，
  将 `SetCountdownVisible(...)` 恢复为只设置显隐与文本即可。
- 无需回滚 prefab、事件、配置、存档或玩家数据。

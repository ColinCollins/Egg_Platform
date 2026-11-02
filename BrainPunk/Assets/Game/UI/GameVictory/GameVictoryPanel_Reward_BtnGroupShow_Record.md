# GameVictoryPanel_Reward 按钮组进场动画记录

## 需求背景

`GameVictoryPanel_Reawrd.ShowBtnGroup()` 原本只有占位注释，奖励版胜利面板在骨骼收起后需要让 `BtnGroups` 从界面下方滑入到 prefab 初始位置，并让绑定的 `CanvasGroup` 从透明渐显到可交互。

## 影响范围

- `Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel_Reawrd.cs`
- `Assets/Game/UI/GameVictory/Resources/GameVictoryPanel_Reward.prefab`

本次未修改 prefab。静态文本检查确认 `GameVictoryPanel_Reward.prefab` 中 `BtnGroups` 挂有 `CanvasGroup`，并已绑定到 `GameVictoryPanel_Reawrd.btnGroup`。

## 原有行为基线

- `OnOpenExtra()` 已在 `CloseBoneDelay` 后调用 `ShowBtnGroup()`。
- `ShowBtnGroup()` 为空实现，因此按钮组不会按预期执行从下方进入和淡入效果。
- `GameVictoryPanel_Reward.prefab` 中 `BtnGroups` 的当前初始锚点位置为 `(-10, -375)`，`CanvasGroup` 初始 alpha 为 1。

## 新行为

- 打开奖励版胜利面板时，`ResetOpenStateExtra()` 会先把按钮组 alpha 置为 0、关闭交互和射线，并移动到初始位置下方。
- `ShowBtnGroup()` 会记录 prefab 初始锚点位置，运行 DOTween 序列：`RectTransform.DOAnchorPos()` 回到初始位置，同时 `CanvasGroup.DOFade()` 从 0 到 1。
- 动画完成后恢复 `interactable` 和 `blocksRaycasts`，避免按钮在进场前可点。
- `OnHideExtra()` 和 `OnCloseExtra()` 会停止按钮组 tween，临时隐藏后 `OnShowExtra()` 会重新执行按钮组进场，避免 UI 栈覆盖期间残留半透明或不可点状态。

## 数据、资源和事件流

- 未新增资源、存档字段、配置字段、RemoteConfig 读取、全局事件、analytics、广告、IAP 或平台 SDK 流程。
- 仅使用现有 `btnGroup` prefab 序列化引用和 DOTween UI 动画。

## 验证

- 已静态检查 `GameVictoryPanel_Reward.prefab`：`BtnGroups` 存在 `CanvasGroup`，并绑定到脚本 `btnGroup` 字段。
- 已静态检查 `GameVictoryPanel_Reawrd.cs`：新增逻辑只触发 UI tween 和 `CanvasGroup` 状态，不访问 `DB`、`Save`、`RemoteConfig`、`Instantiate`、`OnValidate`、`Reset`、`ExecuteAlways` 或编辑器生命周期入口。
- 已执行 `git diff --check -- Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel_Reawrd.cs Assets/Game/UI/GameVictory/GameVictoryPanel_Reward_BtnGroupShow_Record.md DevelopMD/已有功能设计与使用记录.md`，结果无空白错误。
- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，结果为 0 error / 351 warnings；warnings 属于当前工程输出，未逐项审计。

## 剩余风险与未验证缺口

- 尚未在 PlayMode 验证按钮组在骨骼收起后从下方滑入、淡入并恢复点击。
- 尚未验证胜利面板被商店等 UI 临时覆盖后恢复时按钮组不会残留不可点状态。
- Android/iOS 横屏触控 smoke 未验证。

## 回滚路径

- 删除 `GameVictoryPanel_Reawrd.cs` 中 `btnGroup` 进场动画相关字段、`ResetBtnGroupView()`、`CacheBtnGroupTargetPosition()`、`KillBtnGroupTween()` 和 `OnShowExtra()` 中的 `ShowBtnGroup()` 恢复调用。
- 将 `ShowBtnGroup()` 恢复为空实现即可回到本次修改前行为。

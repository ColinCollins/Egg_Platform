# IWannerGame GamePlayPanel 左右移动 Enter/Exit 功能与风险记录

## 需求与背景

- 用户反馈 IWanner HUD 左右移动使用按下/抬起控制时操作不流畅，要求只使用指针 `Enter/Exit` 触发左右控制。
- 本次目标是让触点在左右控制区之间滑动时直接换向，并保留输入关闭、暂停、Reset、Replay 和销毁时的移动清理。

## 影响文件与系统

- `Assets/Game/UI/IWannerGame/GamePlayPanel/Scripts/IWannerGame_GamePlayPanel.cs`
- `Assets/Game/UI/IWannerGame/GamePlayPanel/Resources/IWannerGame_GamePlayPanel.prefab`
- 复用共享 `CustomButton.OnClickEnter/OnClickExit` 与既有 `PlayerLeftMoveEvent`、`PlayerRightMoveEvent`、
  `PlayerMoveCancelEvent`；没有修改共享按钮、共享 `GamePlayPanel`、LevelCtrl、Actor、配置、存档、资源、打点或 SDK。

## 修改前行为基线

- 面板左右按钮同时订阅 Down/Up 与 Enter/Exit，但 prefab 中左右 `CustomButton` 的 `isListenEnter/isListenExit` 均为 false。
- 因此 Enter/Exit 会被 `CustomButton` 门禁拦截，实际移动主要依赖 `OnClickDown/OnClickUp`。
- 任一方向的结束回调都会立即派发移动取消；若切换方向时事件顺序交错，可能在新方向已进入后被旧方向结束事件短暂取消。

## 修改后行为

- 左右按钮只订阅 `OnClickEnter/OnClickExit`，并仅为 prefab 中 `left_btn`、`right_btn` 开启 Enter/Exit 监听。
- Enter 一侧时标记该方向并清除另一方向；`Update` 继续逐帧派发该方向的既有移动事件。
- Exit 只清除自己仍持有的方向；若另一方向已经通过 Enter 接管，旧 Exit 被忽略。左右均未激活时才派发移动取消。
- `SetInputEnabled(false)`、暂停、Reset、Replay、关闭与销毁仍通过既有 `ResetMoveInput` 归零；跳跃按钮行为不变。

## UI、事件与平台流

`CustomButton.OnPointerEnter` → `OnClickEnter` → HUD 记录当前方向 → 每帧派发左/右移动事件 →
`IWannerLevelCtrl` 写入 Actor 移动输入；`OnPointerExit` → `OnClickExit` → 无其它方向时派发移动取消。

本次没有新增每帧分配、对象创建、资源加载或全局事件。Android/iOS 横屏沿用 Unity EventSystem 触控路径，但尚未实机验证。

## 验证证据

- CodeGraph 源码证据确认 `CustomButton.OnPointerEnter/OnPointerExit` 分别受 `isListenEnter/isListenExit` 控制，并派发
  `OnClickEnter/OnClickExit`。
- 静态 diff 确认左右移动不再订阅 Down/Up；prefab 仅目标 fileID `3527294826514725050`（左）与
  `6291549145164932182`（右）的 Enter/Exit 门禁由 false 改为 true。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore --nologo`，结果 `1 error / 1124 warnings`；唯一错误位于未修改的
  `Assets/Game/Scripts/Play/IWanner/IWannerPlayCtrl.cs:553`，为 I2 与 Bear 的 `ResourceManager` 引用歧义。本次目标文件
  未出现在错误列表中，但完整程序集编译仍未通过。

## 剩余风险与未验证项

- `uncertain`：Unity PlayMode 的单侧进入/移出、原地抬起、左右连续滑动、暂停/恢复、Reset/Replay 和 UI 遮挡尚未验证。
- `uncertain`：Android/iOS 横屏下触点抬起、系统手势抢占或原生覆盖后是否都可靠产生 Exit 尚未验证。
- Editor 鼠标会按纯 Enter/Exit 语义在悬停时移动；若桌面也要求按住，需要新增明确的平台或指针类型需求。
- 左右按钮之间若存在射线空档，经过空档时会先取消再进入另一侧；需在最终分辨率下目测手感。

## 回滚路径

- 恢复左右按钮的 `OnClickDown/OnClickUp` 订阅与原方向状态处理。
- 把 prefab 中目标左右按钮的 `isListenEnter/isListenExit` 恢复为 false。
- 共享事件、LevelCtrl、Actor、配置、存档、资源、打点与平台 SDK 无需回滚。

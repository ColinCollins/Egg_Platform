# Level59 功能说明

## 玩家流程

- Level59 根对象挂载 `Level59Ctrl` 和 `DoubleTouchScaleListener`。
- `Level59Ctrl.Awake()` 会把 `DoubleTouchScaleListener.Scale` 初始化为 `0.1` 并执行监听器。
- 玩家在真机上使用两根手指做缩放手势时，`DoubleTouchScaleListener` 根据两根手指沿连线方向的帧间移动量累计调整 `Scale`。
- 两根手指背向移动时 `Scale` 增大；两根手指相向移动时 `Scale` 减小；两指同向平移不会因为手指间距本身被当成绝对比例而重置缩放。
- `Level59Ctrl.Update()` 每帧读取 `Scale`，并同步调整 actor 缩放、相机 `orthographicSize`、actor 移动速度、actor 跳跃力和门碰撞开关。
- PC 编辑器路径仍保留鼠标滚轮模拟缩放，便于本地调试。

## 实现边界

- 本次只修改 `DoubleTouchScaleListener` 的真机触摸缩放计算，不改 `Level59Ctrl` 的缩放映射、不改 prefab 绑定、不改 `BaseLevelCtrl`、`ActorCtrl`、存档、配置、资源加载、打点或平台 SDK。
- 真机触摸缩放不再使用 `currentDistance / initialDistance` 作为 `Scale`，避免每次重新双指落下时把缩放比例重置到本次落指间距基准。
- 新增 `touchScaleSensitivity` 脚本字段，默认 `0.005`，用于把触摸帧间移动像素量映射为 `Scale` 增量。
- `threshold` 仍作为触摸增量触发阈值，累计触摸变化达到阈值后才更新 `Scale` 并派发 `onScaleUpEvent` 或 `onScaleDownEvent`。
- `SetScale()` 会同时清理当前触摸跟踪状态，避免初始化或外部设置缩放后残留上一次手势增量。

## Prefab 绑定

- `Level_059.prefab` 根对象绑定 `Level59Ctrl.touchScaleListener` 到同根对象上的 `DoubleTouchScaleListener`。
- `Level_059.prefab` 当前已序列化 `threshold: 1`、`enableMouseWheel: 1`、`mouseWheelSensitivity: 0.1`、`logMouseWheel: 1` 和 `scaleRange: {x: 0.1, y: 2}`。
- `touchScaleSensitivity` 使用脚本默认值 `0.005`；未手动保存 prefab 字段，避免覆盖本次任务前已存在的 prefab 工作区修改。

## 验证记录

- 静态确认：`DoubleTouchScaleListener.HandleTouchInput()` 不再读取当前双指距离并写入 `Scale = currentDistance / _initialDistance`。
- 静态确认：触摸路径通过 `Touch.deltaPosition` 沿上一帧双指连线方向投影，累计为缩放增量。
- 待验证：Unity PlayMode 进入 Level59 后，滚轮缩放、初始化 `Scale = 0.1`、门碰撞开关、actor 缩放/速度/跳跃力映射仍需确认。
- 待验证：Android/iOS 横屏真机上需要验证双指放大、双指缩小、重新落指继续缩放、两指同向平移不误缩放、暂停/重开/退出后无残留手势状态。

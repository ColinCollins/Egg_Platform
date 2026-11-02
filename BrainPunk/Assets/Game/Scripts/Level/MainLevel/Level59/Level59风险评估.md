# Level59 风险评估

## 原始行为基线

- `Level59Ctrl.Awake()` 会调用 `touchScaleListener.SetScale(0.1f)`，随后执行监听器。
- `Level59Ctrl.Update()` 使用 `touchScaleListener.Scale` 映射 actor 缩放、相机 `orthographicSize`、actor 移动速度、actor 跳跃力，并在 `scale <= 0.1f` 时启用 `doorCollider`。
- 修改前 `DoubleTouchScaleListener.HandleTouchInput()` 在真机双指路径中记录初始双指距离，并持续使用 `Scale = currentDistance / _initialDistance`。
- 修改前每次从非双指状态进入双指状态时，监听器会把 `Scale` 设为 `1f`，导致本次落指间距成为新的绝对比例基准。
- PC 鼠标滚轮路径通过 `Mouse ScrollWheel` 对 `Scale` 做增减，未依赖双指距离。

## 本次接入范围

- 修改 `Assets/Game/Scripts/Level/LevelItem/Level59/DoubleTouchScaleListener.cs` 的真机触摸计算。
- 新增本地文档 `Level59功能说明.md` 和本风险评估。
- 更新 `DevelopMD/已有功能设计与使用记录.md`，记录 Level59 当前触摸缩放事实与待验证项。
- 不修改 `Level59Ctrl.cs`、`Level_059.prefab`、共享输入系统、存档、配置、资源加载、打点、广告、IAP 或平台 SDK。

## 影响文件与资源

- `Assets/Game/Scripts/Level/LevelItem/Level59/DoubleTouchScaleListener.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level59/Level59功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level59/Level59风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`

## 已使用的现有系统

- `BaseItemEventHandle` 提供 `Execute()` / `ResetState()` 运行状态入口。
- Unity `Input.touchCount`、`Input.GetTouch(int)`、`Touch.deltaPosition` 和 `Touch.phase` 提供真机触摸输入。
- Unity `Input.GetAxis("Mouse ScrollWheel")` 保留 PC 编辑器滚轮模拟路径。
- `UnityEvent` 继续作为 `onScaleUpEvent` / `onScaleDownEvent` 分发入口；当前 `Level_059.prefab` 未绑定持久调用。
- `Level59Ctrl` 继续消费 `Scale` 并驱动 actor、camera、door collider 参数。

## 变更入口到影响链路

- 玩家双指触摸 -> `DoubleTouchScaleListener.Update()` -> `HandleTouchInput()` -> 根据两指帧间运动投影累计 `Scale` -> `Level59Ctrl.Update()` -> actor/camera/door/speed/jump 映射变化。
- 玩家 PC 滚轮 -> `HandleMouseWheelInput()` -> 原有 `Scale +=/-= mouseWheelSensitivity` 路径保持不变。
- 关卡初始化或重置 -> `SetScale()` / `ResetScale()` -> 清理触摸跟踪状态 -> 下一次双指从当前 `Scale` 继续累计。

## 风险项

- P1：`touchScaleSensitivity = 0.005` 是代码默认值，未通过真机手感验证；如果放大/缩小速度过快或过慢，需要在 Inspector 或脚本默认值中调参。
- P1：真机 `Touch.deltaPosition` 在不同设备 DPI 和刷新率下手感可能不同；需要 Android/iOS 横屏设备验证。
- P1：本次未修改 `Level59Ctrl` 映射范围，`Scale` 仍会被 clamp 到 `scaleRange`，实际 actor/camera 变化仍以现有映射为准；如果旧映射本身不符合策划手感，需要另行评审。
- P1：`onScaleUpEvent` / `onScaleDownEvent` 当前按累计阈值派发一次；如果未来 prefab 绑定了持续反馈事件，事件触发频率需要 PlayMode 验证。
- OBS：`Level_059.prefab` 在本次任务开始前已处于工作区修改状态，本次未手动写入新增字段序列化，避免覆盖既有 prefab 变更。

## 手工验证清单

- Unity Editor PlayMode：进入 Level59，确认初始 actor/camera/door 状态与 `Scale = 0.1` 映射一致。
- Unity Editor PlayMode：使用鼠标滚轮放大/缩小，确认 PC 调试路径仍生效。
- Android 横屏真机：两指背向移动 10 次，确认 actor/camera 平滑放大且不会因重新落指跳回本次间距比例。
- Android 横屏真机：两指相向移动 10 次，确认 actor/camera 平滑缩小且 door collider 在 `Scale <= 0.1` 时仍按原规则启用。
- iOS 横屏真机：重复 Android 验证，确认触摸手感、帧率和缩放方向一致。
- 真机：两指同向平移 10 次，确认不会明显误触发缩放。
- 生命周期：缩放中重开、失败、退出、重新进入 Level59，确认没有残留缩放手势状态或异常跳变。

## 已验证证据

- 2026-06-10：静态确认 `DoubleTouchScaleListener` 已移除 `_initialDistance` / `_lastDistance` 字段和 `Scale = currentDistance / _initialDistance` 路径。
- 2026-06-10：静态确认真机触摸路径使用 `Touch.deltaPosition` 沿上一帧双指连线方向投影得到缩放增量。
- 2026-06-10：静态确认 `SetScale()`、`ResetScale()`、`OnDisable()` 均清理触摸跟踪状态。

## 未验证缺口

- 尚未在 Unity Editor PlayMode 实际进入 Level59。
- 尚未在 Android/iOS 真机或模拟器验证双指缩放手感、两指同向平移过滤、重新落指连续性和横屏帧率。
- 尚未验证 `touchScaleSensitivity` 是否满足最终策划手感。
- 尚未运行完整 Unity 构建。

## 回滚路径

- 回滚 `DoubleTouchScaleListener.cs` 中触摸路径为旧的距离比例计算。
- 删除或保留本次新增的 Level59 文档，并同步回滚 `DevelopMD/已有功能设计与使用记录.md` 中 Level59 记录。
- 本次未修改存档、配置表、RemoteConfig、ProjectSettings 或平台 SDK，无需数据迁移回滚。

## 待确认

- 策划是否接受当前默认触摸灵敏度 `0.005`。
- 如果真机 DPI 差异明显，是否需要把触摸增量改为按屏幕较短边归一化后的比例。

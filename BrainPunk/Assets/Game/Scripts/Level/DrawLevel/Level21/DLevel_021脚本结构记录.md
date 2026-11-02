# DLevel_021 脚本结构记录

## 需求与结构

- 记录日期：2026-08-03。
- `DLevel021Ctrl` 接收首笔有效画线完成通知，之后只负责一个固定播放周期，并在每轮开始时读取左右 TouchArea 的 Draw 接触状态。
- Prefab 根节点显式绑定左右 `Collider2D`、左右 WaterSplash `Animator` 和 Draw LayerMask。
- `DrawLineFeature`、`DrawLevelCtrl`、Actor、胜负、存档、UI 和全局事件均不在本控制器职责内。

## 单一播放周期

`OnEnable → 等待首笔完成 → Idle（3 秒）→ Playing（3 秒）→ Idle` 持续循环，唯一计时字段为 `_cycleTimer`。
首笔完成前 `FixedUpdate()` 不累计时间；`OnDrawStrokeCompleted()` 收到 `IsFirstStroke` 后从 0 开始计时，
后续笔画不会重新启动周期。尝试重置时重新回到等待首笔状态。

每次从 Idle 进入 Playing 时只采样一次当前接触状态：

| 接触状态 | 本轮播放 |
| --- | --- |
| 无接触 | 左右同时 short |
| 仅左侧接触 | 右侧 water_2 |
| 仅右侧接触 | 左侧 water_2 |
| 两侧接触 | 本轮不播放 |

接触变化本身不播放、不停止，也不重置计时器；Playing 到时后统一清除两侧 Animator 保持参数。

## 绑定与边界

- Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_021.prefab`。
- Animator 参数：`short`、`long`、`stay_short`、`stay_long`。
- TouchArea 只检测 Layer 14 Draw；每个物理步最多在周期采样点执行两次 `IsTouchingLayers`。
- 控制器不维护独立音效计时器或声音实例，只负责 Animator 播放周期。
- 不修改共享 `WaterSplashCtrl`、`DrawLevelCtrl`、`ActorCtrl` 或 `GamePlayPanel`。
- 首笔门禁复用 `IDrawStrokeCompletedHandler`，由 `DrawLevelCtrl` 自动发现并同步通知，不新增全局事件。

## 相对参考关卡的差异

- 复用主线 Level14 的水花参数与 3 秒待机/3 秒播放节奏。
- 输入由屏幕触碰改为 Draw 线段与 TouchArea 的 Physics2D 接触。
- 喷水计时从第一笔有效画线完成时启动，进入关卡但尚未画完时不会提前累计。
- 本关不响应接触变化事件，只在固定周期边界读取当前状态，避免接触抖动影响节拍。
- 当前证据不足以抽取共享控制器，继续保持 Level21 局部实现。

## 验证、风险与回滚

- 已完成脚本静态检查、Prefab 引用检查和 Assembly-CSharp 编译，结果为 `0 error`；其余输出为项目既有 warning。
- `uncertain`：PlayMode 下的水柱托举、双侧接触、Reset/Replay、暂停恢复及 Android/iOS 表现尚未验证。
- 回滚方式：移除 Prefab 根节点的 `DLevel021Ctrl` 组件，并恢复本脚本与文档；不涉及配置或存档迁移。

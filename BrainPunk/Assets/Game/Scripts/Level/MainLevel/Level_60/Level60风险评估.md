# Level60 梦境切换风险评估

## 2026-06-09 DreamActor alpha follow-up

### Original behavior baseline

- `Level60Ctrl.SetBubbleAlpha(float)` controlled cached non-Actor `dreamBubble` `SpriteRenderer`, `SkeletonRenderer`, and `SkeletonGraphic` alpha.
- `CacheBubbleVisuals()` deliberately excluded `ActorCtrl` child visuals, so DreamActor did not follow the bubble alpha.

### Change

- `SetBubbleAlpha(float)` now also calls `dreamActor.SpineCtrl.SetAlpha(_bubbleAlpha)` when both references exist.
- The cached bubble visual lists still exclude Actor visuals to avoid duplicate alpha writes and keep the change local to the explicit DreamActor reference.

### Risk controls

- Null-conditional access keeps missing `dreamActor` or missing `SpineCtrl` from crashing the fade path.
- No prefab, save, input, event, config, analytics, resource loading, or shared actor-control code is changed.
- Rollback is one line in `Level60Ctrl.SetBubbleAlpha(float)`.

### Remaining risks

- Unity PlayMode has not verified whether DreamActor shadow or auxiliary non-spine visuals should also fade with the bubble.
- Android/iOS landscape smoke has not verified repeated touch reset, fade interruption, and completed DreamActor switch after this alpha sync.

更新时间：2026-06-04

## 评估范围

- 关卡：Level60
- 脚本：`Assets/Game/Scripts/Level/LevelItem/Level_60/Level60Ctrl.cs`
- Prefab：`Assets/Game/Prefabs/Level/Level_060.prefab`
- 规格：`specs/001-level60-dream-switch/spec.md`

## 原有行为基线

- `Level60Ctrl.cs` 原先只有注释、`dreamActor`、`fakeActor` 和空的 `Start/Update`。
- 注释定义的梦境切换机制尚未实现。
- 既有共享系统包括 `BaseLevelCtrl` 的输入转发、胜负判断、暂停/恢复、失败重开、提示气泡和销毁清理。

## 本次实现行为

- 初始状态切换 `BaseLevelCtrl` 当前 actor 为 `FakeActor`。
- `FakeActor` 可移动，`DreamActor` 停止移动。
- 监听玩家左移、右移、跳跃事件和屏幕触摸并重置等待。
- 无操作 5 秒后开始显示 `dreamBubble`。
- `dreamBubble` 2 秒淡入；淡入中玩家操作会快速淡出并重新等待。
- 淡入完成后停止 `FakeActor`、恢复 `DreamActor`，并把 `BaseLevelCtrl` 当前 actor 切到 `DreamActor`。
- 禁用或销毁时清理事件订阅和 tween。

## 使用到的既有系统

- `BaseLevelCtrl.SetActor(...)`：切换当前受控 actor 引用。
- `ActorCtrl.ResumeMoving()` / `StopMoving()` / `SetMoveInput(0f)`：控制 actor 移动状态。
- `Bear.EventSystem.EventSubscriber` 与 `EventsUtils.ResetEvents(ref ...)`：订阅和清理玩家输入事件。
- `Input.touchCount`：移动端触摸检测，用于把触摸也纳入等待重置条件。
- `DG.Tweening.DOVirtual.Float(...)`：驱动气泡透明度渐变。
- Spine `SkeletonRenderer` / `SkeletonGraphic` 与 Unity `SpriteRenderer`：控制气泡视觉透明度。

## 风险项

### P1-1 dreamBubble 淡入范围仍需确认

- 问题：脚本注释写明 `dreamBubble` 包含其子对象进行渐隐渐显，但当前 prefab 层级中存在 `DreamActor` 与 `dreamBubble` 的层级关联风险。
- 当前处理：代码排除所有属于 `ActorCtrl` 层级的视觉组件，只淡入非 Actor 气泡视觉，避免 `DreamActor` 被误隐藏、误透明或整体禁用。
- 影响：如果设计期望 `DreamActor` 随气泡一起淡入，当前表现会和预期不一致。
- 建议：先由用户/策划确认淡入范围；确认后同步调整 `Level60Ctrl.CacheBubbleVisuals()` 和本风险评估。

### P1-2 BaseLevelCtrl 当前 actor 与 prefab 初始引用可能不一致

- 问题：如果 prefab 中 `BaseLevelCtrl.Actor` 初始指向 `DreamActor`，而关卡设计要求初始控制 `FakeActor`，运行开始阶段可能出现短暂不一致。
- 当前处理：`Level60Ctrl.Start()` 会应用初始状态，并在下一帧再次应用一次，覆盖 `BaseLevelCtrl.Start()` 的恢复移动影响。
- 影响：仍需要 Unity PlayMode 观察首帧是否出现输入转发或动画状态闪动。
- 建议：正式验收时确认进入关卡第一秒内玩家输入只作用于 `FakeActor`。

### P1-3 引用兜底依赖对象命名

- 问题：`FakeActor`、`DreamActor`、`dreamBubble` 如果未在 Inspector 显式绑定，脚本会按名字兜底查找。
- 当前处理：优先使用 Inspector 引用；名字查找只作为防御性兜底。
- 影响：后续重命名对象可能导致机制不完整但不一定直接崩溃。
- 建议：正式 prefab 应显式绑定 `baseLevelCtrl`、`fakeActor`、`dreamActor`、`dreamBubble`。

### P1-4 编译验证被其他关卡阻断

- 问题：执行 `dotnet build Assembly-CSharp.csproj --no-restore` 时被 `Level57Ctrl.cs` 中 `CrowCtrl` 类型缺失错误阻断。
- 当前结果：过滤输出中未出现 `Level60Ctrl.cs` 编译错误。
- 影响：无法用完整编译证明当前工作区全项目通过。
- 建议：先修复或隔离 `Level57Ctrl.cs` 的 `CrowCtrl` 编译问题，再重新跑完整编译。

### P1-5 触摸输入需要移动端运行验证

- 问题：触摸重置依赖 Unity `Input.touchCount`，Editor 鼠标点击不会完全等同于移动端触摸行为。
- 当前处理：持续触摸期间每帧重置等待时间，只在触摸刚出现时触发一次快速淡出，避免重复创建 tween。
- 影响：Android/iOS 真机或模拟构建中仍需确认按住、连续点按、多指触摸都不会让气泡误完成淡入。
- 建议：在移动端验证触摸按住超过 5 秒、淡入期间点触、连续点触三种路径。

## 性能与移动端影响

- 每帧只累计等待时间并读取触摸数量，不创建对象。
- 气泡视觉组件在 `Awake()` 缓存，淡入淡出时只更新颜色/Spine alpha。
- tween 数量最多一个，输入中断时会先 Kill 旧 tween。
- 触摸按住期间不会重复创建淡出 tween。
- 预期移动端性能风险低，但仍需在代表性 Android/iOS 设备或模拟构建中观察淡入期间帧率和 GC。

## 验证记录

- 已读取 `Level60Ctrl.cs` 注释并完成脚本实现。
- 已确认 `Level_060.prefab` 当前工作区中存在 `FakeActor`、`DreamActor`、`dreamBubble` 名称和 `Level60Ctrl` 序列化字段。
- 已补充 `Input.touchCount > 0` 触摸重置逻辑，并避免持续触摸时重复启动淡出 tween。
- 已执行 `dotnet build Assembly-CSharp.csproj --no-restore`：
  - 结果：失败。
  - 阻断原因：`Level57Ctrl.cs` 第 23、463 行找不到 `CrowCtrl`。
  - `Level60Ctrl.cs`：过滤输出中未发现新增编译错误。

## 必做手工验证

- 进入 Level60 后不操作，确认 5 秒后气泡开始显示。
- 气泡淡入约 2 秒完成。
- 淡入期间左移、右移、跳跃都会让气泡快速淡出并重新等待。
- 淡入前或淡入期间触摸屏幕也会重置等待；按住触摸超过 5 秒不会触发气泡完成淡入。
- 多次连续触摸不会导致 tween 堆叠或明显卡顿。
- 切换完成前只有 `FakeActor` 响应输入。
- 切换完成后只有 `DreamActor` 响应输入，`FakeActor` 停止。
- 切换前后触发失败和重开，确认回到初始状态。
- 切换后通关，确认通关、存档、解锁、下一关流程不受影响。
- 在 Android/iOS 横屏环境至少各跑一次核心流程。

## 回滚路径

- 回滚 `Level60Ctrl.cs` 到空实现可移除本次运行时行为。
- 如果 prefab 仅为绑定引用和时间参数而变更，可同步回滚 `Level_060.prefab` 对应序列化字段。
- 保留文档时需要标注功能已回滚，避免后续误读为线上行为。

## 当前结论

- 当前实现风险主要集中在 `dreamBubble` 淡入范围确认和 Unity PlayMode 未验证。
- 在确认淡入范围、完成 Unity 运行验证、解决或绕过 Level57 编译阻断前，不建议视为最终验收完成。

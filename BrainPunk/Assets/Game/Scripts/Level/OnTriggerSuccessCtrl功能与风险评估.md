# OnTriggerSuccessCtrl 功能与风险评估

## 1. 需求与背景

`OnTriggerSuccessCtrl` 提供 Trigger/Collision 两种直接胜利入口。组件参考
`OnTriggerTrapHandle` 识别 Actor Layer、Player Tag；位于 DrawLevel 层级时提交
DrawGame 本地胜利结果，否则保留主线胜利状态事件。

## 2. 原行为基线

- 主线关卡常规成功链由 `BaseLevelCtrl` 判断完成条件、锁定关卡、播放可选成功动画并最终
  派发 `SwitchGameStateEvent(GamePlayStateName.SUCCESS)`。
- `PlayCtrl` 接收该事件后进入 `PlayCtrl_Success`，继续执行胜利打点、首次通关奖励、
  胜利面板与关卡存档。
- DrawGame 不订阅主线 `SwitchGameStateEvent`；其正确结算链为
  `DrawLevelCtrl.TryResolve -> Resolved -> DrawPlayCtrl.OnLevelResolved`。
- 2026-07-28 修复前，DLevel_006 已把本组件挂到 `Alien (1)/yellow_ball`，但组件仍只
  派发主线事件，因此碰撞即使进入回调也不会触发 DrawGame 胜利面板。

## 3. 新行为与影响范围

- 新增脚本：`Assets/Game/Scripts/Level/OnTriggerSuccessCtrl.cs`。
- `isCollision=false` 时只响应 `OnTriggerEnter2D`；`isCollision=true` 时只响应
  `OnCollisionEnter2D`，避免同一实例通过两个入口重复处理。
- 仅接受 Layer 为 `Actor` 且 Tag 为 `Player` 的碰撞体。
- 新增可选序列化引用 `drawLevelCtrl`；`Awake()` 和有效碰撞时都会从父层级回退解析，
  不需要跨关卡查找或全局单例。
- 解析到 DrawLevelCtrl 时调用
  `TryResolve(new DrawLevelResult(Success, nameof(OnTriggerSuccessCtrl)))`。只有提交成功
  才锁存 `isTriggered`；DrawLevelCtrl 的一次性结果锁继续阻止重复结算。
- 未解析到 DrawLevelCtrl 时保持原主线流：
  `Collider2D -> SwitchGameStateEvent(SUCCESS) -> PlayCtrl -> PlayCtrl_Success`。
- DLevel_006 显式把 `drawLevelCtrl` 绑定到同一 prefab 根节点的主控制器；没有新增
  DrawGame 全局事件，也未修改 DrawPlayCtrl、共享 Actor、UI、配置、资源、存档结构、
  广告或平台 SDK。

## 4. 风险、验证与回滚

- 静态验证：检查 Trigger/Collision 互斥、PlayMode 外层门禁、Player Layer/Tag 判定、
  DrawLevelCtrl 父层级解析、Draw/Main 分流和一次性结算门禁。
- C# 单进程构建：首次命令在 64 秒工具超时后仍由后台继续运行，不能作为结果；后台进程
  退出后用相同参数增量复核通过，`0 error / 0 warning`。
- Prefab 静态验证：DLevel_006 的组件 `isCollision=1`，`drawLevelCtrl` 指向本关根节点
  DrawLevelCtrl；目标球为 Actor Layer、Player Tag，双方 Collider2D 均非 Trigger。
- Unity Inspector/PlayMode、Android/iOS 横屏：未验证。实际使用时仍需确认目标区域的
  Collider2D、`Is Trigger`、`isCollision`、Layer Collision Matrix 和 Player Layer/Tag。
- 直接事件入口不会执行 `BaseLevelCtrl` 的成功门动画与其第三颗场景星收集前置流程；
  只适用于设计上明确需要直接结算胜利的区域。
- Draw 分支直接提交结果，不播放 `DrawLevelCtrl.OnTriggerSuccess()` 的成功门/Actor 淡出
  演出；DLevel_006 当前需求是双球碰撞直接胜利，仍需 PlayMode 确认观感。
- 回滚方式：删除 `drawLevelCtrl` 字段、父层级解析和 Draw 分支，移除 DLevel_006 上该字段
  绑定，即恢复仅派发主线胜利事件的旧行为。

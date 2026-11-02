# ILevel061Ctrl 功能与风险记录

日期：2026-08-25

## 需求与原始基线

- 需求：第 061 关监听玩家左、右移动按键；玩家首次选定方向后，即使松开按键也继续保持该方向的水平移动速度，再次按下反方向时立即反向。
- 修改前基线：`IWannerLevelCtrl` 同时监听左右移动和 `PlayerMoveCancelEvent`；松开时把 Actor 输入设为 0，下一次 `ActorCtrl.OnUpdate()` 会把 `Rigidbody2D.linearVelocity.x` 写为 0。

## 影响文件与系统

- `Assets/Game/Scripts/Level/IWannerLevel/Levels/ILevel061Ctrl.cs`
- `Assets/Game/Prefabs/WannerLevel/ILevel_061.prefab`
- `DevelopMD/已有功能设计与使用记录.md`
- 复用现有 `PlayerRightMoveEvent`、`PlayerLeftMoveEvent`、Bear `EventSubscriber`、`IWannerLevelCtrl` 输入门禁、`Actor_IWanner` 和 `Rigidbody2D`。
- 不修改共享 `ActorCtrl`、`IWannerLevelCtrl`、输入事件定义、DB/Save、配置、资源加载、UI、打点或平台 SDK。

## 修改后行为与数据流

玩家按下左/右移动按键 → 已有移动事件 → `ILevel061Ctrl` 记录最后一次物理方向
→ 每帧 `LateUpdate` 先通过 `IWannerLevelCtrl.SetMoveInput()` 复用运行时、输入、暂停与结算门禁
→ 门禁接受后把 `linearVelocity.x` 写为 `方向 * abs(Actor.MoveSpeed)`，保留当前 `linearVelocity.y`。

- 不监听 `PlayerMoveCancelEvent`；松开产生的归零会在当帧 `LateUpdate` 被本关记住的方向覆盖。
- 方向速度持续读取 `Actor.MoveSpeed`，当前 Actor prefab 的静态值为 `3.3`；后续局部速度调整也会自动生效。
- 反向映射复用 `IWannerLevelCtrl.IsReverse`，保持与当前 IWanner 输入规则一致。
- 暂停、输入关闭、成功、失败或 Actor 死亡时，现有门禁会拒绝方向写入，脚本不再覆盖水平速度。恢复输入后，会按暂停前记住的方向继续移动。
- 只写入水平分量，跳跃、下落、重力方向和最大下落速度流程保持不变。

## 验证证据

- CodeGraph 与源码确认 `PlayerMoveCancelEvent -> IWannerLevelCtrl.SetMoveInput(0) -> ActorCtrl.HandleMovement()` 会把水平速度归零；`ActorCtrl.HandleMovement()` 的基础速度为 `xInput * MoveSpeed`。
- 静态确认 061 prefab 已存在 `IWannerLevelCtrl` 与 `Actor_IWanner` 引用，新组件绑定到同一关卡根节点。
- 将新脚本临时纳入 Unity 已生成的 `Assembly-CSharp.csproj` 后执行完整编译，结果为 `0 error / 106 warning`；warning 均属工程现有诊断范围，临时 csproj 条目已在验证后移除。
- 定向 `git diff --check` 通过，仅有工作区现有 LF/CRLF 转换提示；静态扫描确认脚本未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/Save、资源加载、实例化、`Debug.Log` 或 `PlayerMoveCancelEvent` 路径。

## 风险、未验证缺口与回滚

- `uncertain`：尚未在 Unity PlayMode 验证首次启动、松手持续移动、快速反向、跳跃/下落、墙角碰撞、旜坡、死亡、成功、暂停/恢复、Reset 与关卡退出。
- `uncertain`：Android/iOS 横屏触控、60 fps 和物理手感未实机验证。
- 每帧仅执行常量次引用与刚体检查，不创建集合、不加载资源、不产生预期 GC 分配；真机 CPU 与电量影响仍以设备验证为准。
- 角落或其它物理机关可能在一个物理步内短暂修改 X 速度；本控制器在渲染帧末恢复基础速度，实际碰撞手感需 PlayMode 确认。
- 回滚时从 `ILevel_061.prefab` 移除 `ILevel061Ctrl` 组件，删除脚本及 meta，即可恢复松开按键后水平速度归零的共享 IWanner 行为；无需迁移 prefab 其它数据、配置或存档。

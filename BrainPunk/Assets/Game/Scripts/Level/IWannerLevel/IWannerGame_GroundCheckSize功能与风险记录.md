# IWannerGame_GroundCheckSize 功能与风险记录

## 需求背景

IWanner 关卡需要通过现有 `TriggerExecutor` / `BaseItemExecutor` 执行链，在可配置时间内把当前
`Actor_IWanner` 的地面 BoxCast 检测框尺寸变化到 Inspector 指定的目标 `Vector2`。

## 影响范围

- 新增脚本：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_GroundCheckSize.cs`
- 修改脚本：`Assets/Game/Scripts/Level/Actor/Actor_IWanner.cs`，新增当前实例的
  `SetGroundCheckSize(Vector2)` 与程序集内 `GetGroundCheckSize()`
- 最小共享边界调整：`Assets/Game/Scripts/Level/Actor/ActorCtrl.cs` 的既有序列化字段
  `groundCheckSize` 从 `private` 调整为 `protected`
- 复用系统：`Game.ItemEvent.BaseItemEventHandle`、DOTween、Bear Logger、现有 `ActorCtrl.HandleCollision()` BoxCast
- 未修改或绑定 prefab、关卡资源、全局事件、DB、存档、配置、资源加载、UI、打点或平台 SDK

## 原有行为基线

- `ActorCtrl.groundCheckSize` 默认值为 `(1, 0.2)`，原本是私有序列化字段。
- 共享 `HandleCollision()` 每次角色更新时用该尺寸执行向下 `Physics2D.BoxCast`；`OnDrawGizmos()` 读取同一字段绘制检测框。
- `Actor_IWanner` 原本只能运行时修改 `GroundCheckDistance`，没有 GroundCheckSize 的读写边界或平滑变化 Listener。

## 新行为

- Listener 在 Inspector 暴露目标 `Actor_IWanner`、`targetGroundCheckSize` 与 `duration`。
- `Execute()` 仅在 PlayMode 生效；Actor 未显式绑定时，依次从当前/父级 Actor 和父级
  `IWannerLevelCtrl` 的子层级查找。
- 有效执行从 Actor 当前尺寸开始，使用线性 DOTween 在 `duration` 秒内变化到目标尺寸；
  `duration <= 0` 时立即写入。
- 目标值与 Tween 每帧写入均分别把 X/Y 负值按 `0` 处理。重复执行先停止旧 Tween，再从当时当前值续接。
- `ResetState()` 与组件销毁只停止未完成 Tween，不恢复触发前尺寸；目标缺失时用 Bear Logger 警告并标记完成，
  避免顺序执行器卡住。

## 数据与运行时流程

`TriggerExecutor/BaseItemExecutor -> IWannerGame_GroundCheckSize.Execute() -> GetGroundCheckSize()` 获取起点，
DOTween 逐帧调用 `SetGroundCheckSize(Vector2)` 写入当前 Actor 实例；后续 `ActorCtrl.HandleCollision()` 和
Gizmos 继续读取同一字段。流程不新增全局事件、存档、配置、资源或平台调用。

## 验证记录

- 执行 `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:quiet -clp:ErrorsOnly`：
  通过，`0 error / 109 warning`；warning 为工程既有项。
- 定向空白检查未发现 trailing whitespace 等错误，仅有工作区 LF/CRLF 转换提示。
- 静态确认 `groundCheckSize` 字段名、类型、默认值、`HandleCollision()` 与 Gizmos 两个既有读取点不变，
  只调整 C# 可见性。
- 新脚本未新增 `Update`、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、
  RemoteConfig、Instance、Instantiate 或直接 `Debug.Log` 路径。
- 新序列化字段与公开 API 均包含中文用途、调用边界和注意事项说明。

## 剩余风险与未验证项

- `uncertain`：尚未在 Unity PlayMode 验证目标/时长 Inspector 绑定、线性变化、零时长、重复触发、
  Reset/销毁中断及顺序执行器推进。
- `uncertain`：尺寸过小或任一轴为 0 可能导致地面漏判；尺寸过大可能提前或跨邻近地块判定接地，
  并影响离地禁跳、平台边缘、斜坡、墙边与移动 Ground 手感。
- Tween 期间检测框每帧变化，`IsGround` 可能在过渡中途改变；具体目标值和 duration 必须按关卡几何验证。
- `groundCheckSize` 从 `private` 改为 `protected` 不改变既有序列化和运行时结果，但扩大派生类可修改边界；
  当前只由 `Actor_IWanner` 的局部 API 使用。
- 当前未绑定 prefab；Android/iOS 横屏实机、移动端物理手感和帧率仍为 `uncertain`。本实现不新增场景扫描、
  实例化或持续分配热点。

## 回滚方式

删除 `IWannerGame_GroundCheckSize.cs` 及其 `.meta`，从 `Actor_IWanner.cs` 删除 GroundCheckSize Set/Get，
并把 `ActorCtrl.groundCheckSize` 从 `protected` 恢复为 `private`。字段名、类型、默认值和 prefab 序列化数据无需迁移；
若后续已绑定 prefab，还需先移除对应组件或执行器引用。

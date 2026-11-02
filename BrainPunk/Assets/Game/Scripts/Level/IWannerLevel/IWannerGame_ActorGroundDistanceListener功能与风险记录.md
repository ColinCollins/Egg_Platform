# IWannerGame_ActorGroundDistanceListener 功能与风险记录

## 需求背景

IWanner 关卡需要通过现有 `TriggerExecutor` / `BaseItemExecutor` 执行链，在可配置时间内把当前
`Actor_IWanner` 的地面 BoxCast 检测距离变化到指定目标值。为保持职责局部，本次新增独立 Listener，
不把 Tween、Trigger 或关卡配置职责加入共享 `ActorCtrl`。

## 影响范围

- 新增脚本：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_ActorGroundDistanceListener.cs`
- 最小扩展：`Assets/Game/Scripts/Level/Actor/Actor_IWanner.cs` 新增程序集内只读方法
  `GetGroundCheckDistance()`，只供本地 Listener 获取 Tween 起点
- 复用系统：`Game.ItemEvent.BaseItemEventHandle`、DOTween、Bear Logger、
  `Actor_IWanner.SetGroundCheckDistance(float)`
- 未修改 prefab、关卡资源、共享事件、DB、存档、配置、资源加载、UI、打点或平台 SDK

## 原有行为基线

- `Actor_IWanner.SetGroundCheckDistance(float)` 已能在 PlayMode 立即写入当前角色实例的地面检测距离，
  负值按 `0` 处理。
- 原实现没有当前距离读取入口，也没有可被 Trigger 执行链驱动的平滑变化组件。
- 未主动调用该 API 时，Actor 继续使用 prefab 序列化的既有距离值。

## 新行为

- Listener 在 Inspector 提供 `target`、`targetGroundDistance` 与 `duration`。
- `Execute()` 仅在 PlayMode 生效；目标未显式绑定时，依次从当前 Actor 父级和父级
  `IWannerLevelCtrl` 的子层级查找 `Actor_IWanner`。
- 有效执行从 Actor 当前地面检测距离开始，使用线性 DOTween 在 `duration` 秒内变化到非负目标值；
  `duration <= 0` 时立即写入目标值。
- 重复执行先停止旧 Tween，再从当时当前值重新开始。`ResetState()` 与组件销毁会停止未完成 Tween，
  但不会恢复触发前距离。
- 执行期间按 `BaseItemEventHandle` 约定维护 `IsRunning` / `IsDone`；目标缺失时使用 Bear Logger 警告并标记完成，
  避免顺序执行器卡住。

## 数据与运行时流程

`TriggerExecutor/BaseItemExecutor -> Execute() -> ResolveTarget() -> Actor_IWanner.GetGroundCheckDistance()`
获取本次起点，DOTween 每帧调用 `SetGroundCheckDistance(float)` 写入当前 Actor 实例；共享
`ActorCtrl.HandleCollision()` 在后续角色更新中继续读取同一个字段执行 BoxCast。流程不创建全局事件，
不访问存档、配置、资源或平台接口。

## 验证记录

- 执行 `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal`：
  通过，`0 error / 109 warning`；warning 为工程既有项，没有指向本次新增 Listener。
- 定向 `git diff --check` 通过，仅有工作区既有 LF/CRLF 转换提示。
- 静态扫描未发现新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、
  RemoteConfig、Instance、Instantiate 或直接 `Debug.Log` 路径。
- 静态确认新序列化字段与执行 API 均有中文用途、调用边界和注意事项说明。

## 剩余风险与未验证项

- `uncertain`：尚未在 Unity PlayMode 验证 Trigger 实际绑定、从当前值续接、零时长、重复触发、
  Reset/销毁中断以及顺序执行器推进。
- `uncertain`：尚未验证距离过小或过大对落地、离地禁跳、斜坡、墙边和移动 Ground 的关卡手感；
  Inspector 配置仍需按具体关卡几何选择合理目标值。
- `uncertain`：尚未完成 Android/iOS 横屏实机验证。Tween 只在有效执行期间更新一个 float，
  没有场景级逐帧查找或持续数组分配，但移动端物理手感仍需实机关卡确认。
- 当前未绑定任何 prefab，因此新增脚本本身不会改变既有关卡；实际接入时必须确认 Listener 与目标 Actor
  属于同一个活动小关实例。

## 回滚方式

删除 `IWannerGame_ActorGroundDistanceListener.cs` 及其 `.meta`，并从
`Actor_IWanner.cs` 删除 `GetGroundCheckDistance()` 即可回滚本次能力。若已在 prefab 上挂载 Listener，
还需先移除对应组件或 UnityEvent/执行器引用；无需迁移 DB、配置、资源或存档。

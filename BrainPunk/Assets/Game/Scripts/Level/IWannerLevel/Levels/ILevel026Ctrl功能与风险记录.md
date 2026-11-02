# ILevel026Ctrl 功能与风险记录

日期：2026-08-13

## 需求与背景

- ILevel_026 的三块小平台需要由 Trigger 同时启动，并允许在 Inspector 配置统一水平速度和每块平台的掉落时机。
- 仅依靠 PhysicsMaterial2D 摩擦未能稳定让 Actor 跟随平台，因此本关最初采用局部乘客逻辑。2026-08-13 后续需求又在
  `Actor_IWanner` 增加“脚下 Ground 带 Rigidbody2D 时合成支撑速度”的 IWanner 通用能力；共享 `ActorCtrl` 与
  `IWannerGame_MoveTriggerListener` 仍未修改。

## 影响文件、资源与系统

- 新增 `Assets/Game/Scripts/Level/IWannerLevel/Levels/ILevel026Ctrl.cs` 及 meta。
- 修改 `Assets/Game/Prefabs/WannerLevel/ILevel_026.prefab`：根节点绑定控制器、Trigger UnityEvent、Actor 引用和三块刚体配置。
- 三块 `Ground_Move` 各自持有 Kinematic Rigidbody2D 与 BoxCollider2D；Collider 和刚体继续引用
  `Assets/Game/ArtSrc/Physics/IWanner_Actor.physicsMaterial2D`。
- 已撤回先前尝试的 `Actor_IWanner.OnUpdate()` 无输入速度覆写及共享移动 Listener 的 Rigidbody2D 分支。
- 关联修改：`Actor_IWanner.OnUpdate()` 当前使用实际 Ground 接触合成支撑刚体速度；这与已撤回的“无条件保留旧 X 速度”策略不同。
- 未新增全局事件、配置、存档、资源加载、UI、Analytics 或平台 SDK 链路。

## 修改前基线

- Trigger 使用 `IWannerGame_MoveTriggerListener` 移动三块平台的共同父 Transform；子平台刚体本身没有一致的物理速度。
- Actor 的共享移动逻辑持续控制 X 速度，PhysicsMaterial2D 摩擦不能独立保证 Actor 与平台保持相对位置。
- 原参数为水平偏移 `12.3`、移动时长 `5` 秒，等效水平速度为 `2.46`；左右平台原掉落节奏为 `2` 秒与 `3.5` 秒，
  中间平台不掉落。

## 新行为

- `StartMovingPlatforms()` 是给 ILevel_026 Trigger UnityEvent 调用的无参公开 API；单个关卡实例只响应第一次触发。
- `horizontalMoveSpeed` 控制三块 Kinematic Rigidbody2D 的统一 X 速度，正数向右、负数向左、0 表示原地等待掉落。
- 每块 `PlatformSettings.dropDelay` 独立计时；到点切换为 Dynamic 并保留水平速度，之后由该刚体的重力参数接管；
  `dropDelay < 0` 表示不掉落。
- `movementDuration` 到时只停止尚未掉落的平台，不取消晚于该时间的掉落。当前 Prefab 使用 `2.46 / 5`，掉落延迟为
  `2 / -1 / 3.5`，保持原节奏。
- FixedUpdate 会判定 Actor Collider 是否接触并站在任一尚未掉落的平台顶部。命中时把 Actor 世界 X 速度设为
  “平台速度 + Actor 输入相对速度”；因此无输入稳定随平台移动，有输入仍能相对平台行走。平台掉落后立即退出携带集合。
- `Actor_IWanner` 当前也会在实际 Ground 接触存在时绝对写入同一速度合成结果，不会在数值上重复相加；但平台切换 Dynamic 后，
  通用 Actor 能力仍可能在保持朝上接触时继续继承其 X 速度，与本关控制器“掉落后退出局部集合”的边界不同。

## 数据、资源与事件流

- 调用链为 `Trigger2DListener.onEnter -> ILevel026Ctrl.StartMovingPlatforms()`；原 TriggerExecutor 只保留关闭一次性
  Trigger Collider 的本地处理。
- 控制器只读取本关序列化引用和 Actor 的公开输入/速度状态，不订阅 Bear 全局事件，不产生运行时资源加载或存档写入。

## 验证证据

- CodeGraph 核对了 `ActorCtrl.XInput`、`MoveSpeed`、`RB` 公开边界，以及现有 Level93 平台乘客使用
  “平台速度 + Actor 相对输入速度”的项目基线。
- Prefab 静态校验确认控制器、Trigger API、Actor、三个刚体锚点均各有一个定义；无重复 YAML anchor。
- 三个刚体初始均为 Kinematic，分别绑定 `2 / -1 / 3.5` 秒掉落配置；共同父节点不再持有 Rigidbody2D。
- `dotnet build Assembly-CSharp.csproj --no-restore ...` 通过：`0 error / 101 warning`。

## 剩余风险与未验证缺口

- `uncertain`：尚未完成 Unity PlayMode 的无输入携带、顺/逆向输入、松手、跳跃、跨平台缝隙、左右块掉落、终点停止、
  Reset/重建与重复进入 Trigger 实测。
- `uncertain`：Actor 在两个平台 Collider 边界同时接触时的视觉平滑度，以及 Android/iOS 横屏不同固定帧率表现尚未验证。
- `uncertain`：尚未回归 026 局部乘客逻辑与 `Actor_IWanner` 通用 Ground 刚体合成同时存在时的执行时序、掉落瞬间和跨缝表现。
- Dynamic 掉落使用刚体当前 `gravityScale = 1`；若关卡表现要求更快掉落，应调整该平台刚体参数，而不是修改共享物理设置。

## 回滚路径

- 从 ILevel_026 根节点移除 `ILevel026Ctrl`，删除 Trigger 对 `StartMovingPlatforms()` 的 UnityEvent 调用，恢复原
  `IWannerGame_MoveTriggerListener` 组件和 items 绑定，即可回到父 Transform Tween 方案。
- 删除 `ILevel026Ctrl.cs` 及 meta；无需回滚存档、配置、资源地址或全局事件。

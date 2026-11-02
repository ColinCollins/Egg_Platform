# IWanner PlatformEffector2D 单向平台功能与风险记录

日期：2026-08-21

## 需求 / 背景

- ILevel_054 需要角色能够从平台下方向上穿过，并能从平台上方落下站立。
- 自定义 `IWannerGame_SingleSidePlatform` 方案已按用户要求移除；当前统一使用 Unity 内置 `PlatformEffector2D`。
- 共享 `ActorCtrl` 的长距离地面 BoxCast 会在密集单向平台之间持续命中 Ground，导致
  `Actor_IWanner._hasJumpedSinceLastLanding` 不能稳定经普通离地/落地状态链复位；本次要求由 IWanner Actor
  增加脚下短检测，仅在识别到 `SingleSidePlatform` 时补充落地状态，并增加默认 `-5` 的最大下落速度配置。

## 影响文件与系统

- 删除 `IWannerGame_SingleSidePlatform.cs` 及其 meta，不再由自定义脚本调用 `Physics2D.IgnoreCollision`。
- `IWanner_SinglePlatform.prefab` 当前由非 Trigger `BoxCollider2D` 与 `PlatformEffector2D` 提供单向碰撞。
- `ProjectSettings/TagManager.asset` 新增 `SingleSidePlatform` Tag，平台根节点使用该 Tag。
- `Actor_IWanner.cs` 增加脚下短 BoxCast 落地确认和最大下落速度限制；`Actor_IWanner.prefab` 的
  `maximumFallSpeed` 默认保存为 `-5`。
- 不修改共享 `ActorCtrl`、存档、业务配置、资源加载、UI、全局事件、打点或平台 SDK。

## 修改前后

- 修改前：仓库中保留自定义 Tag 单向平台脚本；调试期间 prefab 曾同时或分别尝试自定义脚本与 `PlatformEffector2D`。
- 修改后：自定义碰撞控制脚本及对应旧记录删除，平台 prefab 只由 Unity 内置 Effector 控制穿透与承载；Tag
  不参与碰撞放行，只供 Actor 识别平台类型。
- Actor 上升时脚下检测直接拒绝；下降或静止时只在角色 Collider 脚底 `0.05` 范围内检测 Ground，并要求命中对象
  Tag 为 `SingleSidePlatform`、平台顶面与脚底误差不超过 `0.03`。检测成立且本轮已跳跃时调用一次
  `HandleLanding()`，同步恢复基础落地状态并清除本地跳跃锁。
- `maximumFallSpeed` 在物理帧中只限制 Rigidbody2D 的 Y 速度下界；默认 `-5`，正值按 `0` 处理，水平速度不变。

## 数据 / 资源 / 事件流

`Actor Layer Collider2D` → `IWanner_SinglePlatform` 的非 Trigger Collider →
`PlatformEffector2D` 根据平台局部向上方向与接触法线决定阻挡或穿透。

- `Actor_IWanner` 脚下短 BoxCast → Ground Layer → `SingleSidePlatform` Tag → `HandleLanding()` →
  `_hasJumpedSinceLastLanding=false` → 下一次 `TriggerGroundedJump()` 可执行。
- Effector 仍按 Layer Mask 过滤；新增 Tag 只参与 Actor 落地确认，不改变 Unity 内置 `PlatformEffector2D` 的碰撞规则。
- 不派发事件，不读写 DB/Save，不实例化额外对象。

## 验证

- 静态 YAML 核对：平台 Collider 为非 Trigger 且 `Used By Effector` 已启用；Effector 启用 One Way，Surface Arc 为 180，Collider Mask 包含 Actor Layer 11。
- 静态 YAML 核对：`Actor_IWanner` 根节点为 Layer 11，主 Collider 与动态 Rigidbody2D 位于根节点。
- 静态 YAML 核对：TagManager 已定义 `SingleSidePlatform`，平台根节点使用该 Tag，Actor prefab 显式保存
  `maximumFallSpeed: -5`。
- 删除后定向扫描确认 prefab、ILevel_054 与其余 Assets 中没有旧脚本 GUID 或组件引用。
- `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal` 通过：
  `0 error / 106 warning`，warning 为工程既有诊断；目标 tracked 文件定向 `git diff --check` 通过。
- 静态断言确认 Tag 定义、平台 Tag 与 Actor prefab 默认限速字段同时存在；Unity PlayMode 仍需验证实际跳跃和限速手感。

## 剩余风险 / 未验证缺口

- `uncertain`：尚未在 Unity PlayMode 确认从每个下层平台穿到上层、落稳后立即再次跳跃，以及脚底位于平台边缘时的短 BoxCast 命中表现。
- 最大下落速度属于共享 `Actor_IWanner.prefab` 参数，会作用于所有复用该 Actor 的 IWanner 关卡；默认 `-5`
  可能改变高处坠落用时和手感，需要覆盖旧关的长距离下落、FallArea 和移动平台。
- Tag 必须保留在实际承载 Collider 所属对象；如果以后把 Collider 移到子节点，需要同步 Tag 或调整识别边界。
- 旋转/移动平台、Reset、Android/iOS 横屏及低帧率行为尚未验证。

## 回滚路径

- 只回滚本次落地修正时，删除 `Actor_IWanner` 的 `Single Side Platform` 区域及两个调用点，移除
  `SingleSidePlatform` Tag 和平台 Tag 赋值；保留 `PlatformEffector2D` 即可。
- 只回滚下落限速时，删除 `maximumFallSpeed`、`FixedUpdate()` 与 `ClampFallSpeed()`，并移除 Actor prefab 对应字段。
- 如需回滚此前自定义脚本删除，可从删除前 Git 版本恢复 `IWannerGame_SingleSidePlatform.cs` 与 meta；恢复前必须先决定是否移除 `PlatformEffector2D`，避免两套机制同时控制同一碰撞对。

## 2026-08-21：LayerMask 生命周期修正

- 脚下检测初版在静态字段初始化中调用 `LayerMask.GetMask`，违反 Unity MonoBehaviour 反序列化生命周期，运行时抛出
  `UnityException` 并使 `Actor_IWanner` 类型初始化失败。
- 当前改为 `Actor_IWanner.Start()` 在 `base.Start()` 后缓存实例 LayerMask 与 Collider；没有改变平台 Tag、探测范围、
  落地状态恢复或最大下落速度参数。
- 修正后 `Assembly-CSharp` 构建为 `0 error / 106 warning`；Unity Console 和 ILevel_054 PlayMode 仍需重新进入验证。

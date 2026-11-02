# Actor_IWanner 功能说明

## 需求背景

IWanner 模式使用 `Assets/Game/ArtSrc/Spines/sp_qboxy_001/Qboxy.json` 角色资源。最初建立本地角色子类时，
资源证据只有 `idle`、`walk`、`jump`、`die` 四个动画，无法直接使用共享 `ActorCtrl` 对空中上升/下降状态所要求的
`jump_up`、`jump_down` 动画名，因此新增 IWanner 局部角色子类完成映射。当前资源已扩展为
`die`、`distortion`、`idle`、`jump`、`revive`、`walk` 六个动画。

2026-08-11 追加规则、2026-09-10 更新：IWanner 角色默认在离地后拒绝玩家后续跳跃请求，不保留共享
`ActorCtrl` 的土狼时间、空中双跳、水中跳跃或空中按键缓冲语义；只有当前 Actor 实例显式开启
`enableInfiniteJump` 时，才允许每次离散跳跃请求在空中重新起跳。

2026-08-13 追加规则：IWanner Actor 接触的 Ground 若带有非 Static `Rigidbody2D`，Actor 的世界 X 速度必须合成为
“玩家相对移动速度 + Ground 刚体水平速度”；静态 Ground 与离地状态维持原移动规则，不能再依赖物理材质摩擦自动传速。

2026-08-13 追加规则：IWanner Actor 进入死亡状态时必须关闭角色根节点 `BoxCollider2D`，使死亡表现期间不再参与主体碰撞检测。

2026-08-17 追加规则：`Actor_IWanner` 对外提供运行时 `PlayDistortion()`，以非循环方式从头播放 `distortion`；
动作完成或被其它 Spine 动画打断后解除局部动作锁并恢复原有移动状态动画，不能把该动作或状态加入共享 `ActorCtrl`。

2026-08-17 追加规则：`Actor_IWanner` 对外提供运行时 `SetGroundCheckDistance(float)`，只修改当前实例继承的
地面 BoxCast 检测距离；用户已明确批准把共享 `ActorCtrl.GroundCheckDistance` 从 `private` 最小调整为 `protected`。

## 影响范围

- 修改脚本：`Assets/Game/Scripts/Level/Actor/Actor_IWanner.cs`、
  `Assets/Game/Scripts/Level/IWannerLevel/IWannerLevelCtrl.cs`
- 修改基类：`Assets/Game/Scripts/Level/Actor/ActorCtrl.cs`（仅把既有 `Die()` 改为虚方法，供 IWanner 局部覆写）
- Spine 资源：`Assets/Game/ArtSrc/Spines/sp_qboxy_001/Qboxy.json`
- 资源绑定：`Actor_IWanner.prefab`，以及所有引用该 prefab 的 IWanner 关卡；`ILevel_027` 是本次移动 Ground 的验证目标。
- 未修改上述 prefab、既有关卡 Actor 子类、DB、存档、配置、事件、资源加载、打点或平台 SDK
- 2026-08-17 distortion API 增量只修改 `Actor_IWanner.cs` 及本地说明/风险记录；`Qboxy.json` 已存在目标动画，
  本次未修改 Spine 资源、prefab 或调用方，因此未主动触发 API 时既有关卡行为不变。
- 2026-08-17 地面检测距离 API 增量修改 `Actor_IWanner.cs` 与 `ActorCtrl.cs`：共享字段只改变 C# 可见性，字段名、
  默认值 `0.42`、序列化数据和 `HandleCollision()` BoxCast 读取路径不变；没有修改 prefab 或新增调用方。

## 原有行为基线

- `Main.unity` 的 `Actor_IWanner` 原先挂载共享 `ActorCtrl`。
- 共享 `ActorCtrl` 在地面使用 `idle`、`walk`，空中使用 `jump_up` 或 `jump_down`，死亡使用 `die`。
- `Qboxy.json` 静态资源证据仅提供 `idle`、`walk`、`jump`、`die`，没有 `jump_up` 与 `jump_down`。
- distortion API 增量前，类内没有对外的 `distortion` 播放入口；即使外部直接调用 `SpineCtrl.PlayAnimation()`，
  下一次 `ActorCtrl.OnUpdate()` 仍会按移动状态切换 `idle`、`walk` 或 `jump`，一次性动作可能被下一帧覆盖。
- 地面检测距离 API 增量前，`ActorCtrl.GroundCheckDistance` 是私有序列化字段，只有 Inspector 序列化值和共享
  `HandleCollision()`/Gizmos 可读取，`Actor_IWanner` 无法在运行时局部修改。
- 离地禁跳修改前，`IWannerLevelCtrl.TriggerJump()` 直接调用共享 `ActorCtrl.TriggerJump()`；共享实现允许
  `isGrounded`、土狼时间或水中跳跃，并在每次请求后登记空中跳跃缓冲。当前 `Actor_IWanner.prefab` 虽配置
  `OpenDoubleJump: 0`、`isBuffJump: 0`，仍不能从类职责上表达“离地后一律拒绝”。
- 移动 Ground 修改前，`ActorCtrl.HandleMovement()` 每次更新都按输入重写 Actor X 速度，无输入即写为 0；即使 Ground Collider
  与 Actor 配置物理材质，也没有代码保证支撑刚体速度会保留到 Actor，因此移动平台可从 Actor 脚下滑走。
- 死亡碰撞修改前，`ActorCtrl.Die()` 只设置死亡状态、播放 `die` 并停止移动；`Actor_IWanner.prefab` 根节点启用的
  `BoxCollider2D` 在死亡表现期间仍继续参与 Physics2D 碰撞检测。

## 当前行为

- `Actor_IWanner` 继承 `ActorCtrl`，保持原有移动、朝向、碰撞和死亡能力；跳跃改由 IWanner 局部入口提交。
- `enableInfiniteJump` 默认 false 时，`Actor_IWanner.TriggerGroundedJump()` 先读取共享接地结果 `IsGround`；只有接地为 true
  才调用继承的 `JumpButton()`。本地 `_hasJumpedSinceLastLanding` 保证同一落地周期最多接受一次，避免起跳后的同一物理帧
  快速连点重置上升速度；`HandleLanding()` 调用共享落地逻辑后才清除该锁。上升、下落、走出平台后的土狼时间阶段均直接返回。
- `enableInfiniteJump` 开启时，每个有效跳跃请求直接复用 `ActorCtrl.Jump()`，保留当前 X 速度并把 Y 速度重写为原 `jumpForce`；
  坐下、`StopMoving()` 状态或 `Rigidbody2D.simulated=false` 时仍拒绝请求，不进入共享跳跃缓冲、双跳或水中跳跃分支。
- `IWannerLevelCtrl` 收到 `PlayerJumpEvent` 后，对 `Actor_IWanner` 调用上述局部入口；非 IWanner Actor 的兜底
  路径继续使用原共享入口，避免扩大到其它角色类型。
- 落地且无水平输入时循环播放 `idle`。
- 落地且有水平输入时循环播放 `walk`。
- 离地时播放一次 `jump`，落地后再根据输入切换 `idle` 或 `walk`。
- `PlayDistortion()` 仅在 PlayMode、角色未死亡且 Spine 引用有效时播放非循环 `distortion`。播放期间
  `HandleAnimators()` 暂停自动状态切换，但移动、输入、刚体与碰撞更新继续执行；完成或中断回调会解绑自身并开放下一帧状态动画。
  重复调用会先清理旧回调，再从头重播，避免旧轨道回调提前解除新动作锁。
- `SetGroundCheckDistance(float distance)` 仅在 PlayMode 写入当前 `Actor_IWanner` 实例；输入负值时按 `0` 处理，
  下一次共享 `HandleCollision()` 调用即使用新距离。API 不修改检测盒尺寸、Ground LayerMask、墙体检测或其它 Actor 实例。
- `ActorCtrl.Die()` 保持原有死亡状态、停止移动和 `die` 动画行为，只增加虚方法分派能力；`Actor_IWanner.Die()` 调用基类后，
  查找并关闭角色根节点 `BoxCollider2D`。通过 `ActorCtrl` 类型字段调用死亡时也会进入该覆写，其它 Actor 子类行为不变。
- 动画控制器、刚体或 Spine 引用缺失时跳过本帧动画切换，避免 IWanner 子类新增空引用。
- `Actor_IWanner.OnUpdate()` 先完整执行共享移动、碰撞和动画更新，再查询 Actor 刚体当前与 Ground Layer 的接触；只接受朝上的
  支撑法线，并通过支撑 Collider 的 `attachedRigidbody` 获取 Ground 刚体。
- 支撑 Ground 带有 Simulated、非 Static `Rigidbody2D` 时，Actor X 速度写成“Ground X 速度 + 玩家输入相对速度”，Y 速度保持不变。
  无输入时跟随 Ground，有输入时仍可相对 Ground 行走；触墙时相对输入速度为 0，由 Physics2D 处理平台推动与墙体约束。
- 静态 Ground、无 Ground 接触、向上起跳、死亡、击退、坐下或 Actor 物理未模拟时不合成平台速度。查询使用预分配
  `ContactPoint2D[8]`，运行时不为每帧接触查询创建数组。

## 数据、资源与事件流

IWanner HUD 派发既有 `PlayerJumpEvent`，`IWannerLevelCtrl.TriggerJump()` 完成运行时/输入/暂停门禁后调用
`Actor_IWanner.TriggerGroundedJump()`；默认关闭无限跳跃时，接地请求进入共享基础 `JumpButton()`、离地请求在本地终止；
开启无限跳跃时，有效请求直接调用受保护的 `Jump()`。两条路径都不会调用会无条件登记缓冲的 `ActorCtrl.TriggerJump()`。
`ActorCtrl.OnUpdate()` 继续更新物理与碰撞状态，并动态派发到 `Actor_IWanner.HandleAnimators()`，再由现有
`ActorSpineCtrl.PlayAnimation()` 播放 Qboxy 本地 SkeletonData 中的动画。
移动 Ground 流程不新增实例化、资源地址、全局事件、存档、配置、UI 或打点：平台控制器写 Ground `Rigidbody2D.linearVelocityX`，
Physics2D 建立 Actor 与 Ground 接触，`Actor_IWanner.OnUpdate()` 再合成支撑刚体速度。Ground Layer 名称是该能力的本地约定。
死亡流程仍由陷阱/失败区进入 `IWannerLevelCtrl.TryActorDeath()`，控制器以 `ActorCtrl` 引用调用虚方法 `Die()`，动态分派到
`Actor_IWanner.Die()`；共享死亡表现完成后仅关闭根节点主体 `BoxCollider2D`。不新增事件、资源、存档、打点或平台调用。
distortion 流程可由普通调用方调用 `Actor_IWanner.PlayDistortion()`；`IWannerGame_Portal` 通过程序集内
`PlayDistortionTracked()` 取得同一条 0 号 `TrackEntry`，用于等待出口动作完整完成并识别中断。
`Spine.TrackEntry.Complete/Interrupt` 继续负责清除 Actor 本地轨道跟踪；Portal 只订阅本次返回轨道并在等待结束后解绑。
死亡会先清除 Actor 跟踪再进入既有 `die` 流程，不新增全局事件、协程、资源加载、存档、配置、UI、打点或平台调用。
地面检测距离流程由 IWanner 关卡本地逻辑或 UnityEvent 调用 `Actor_IWanner.SetGroundCheckDistance(distance)`，写入当前实例继承字段；
`ActorCtrl.HandleCollision()` 后续继续通过同一字段执行向下 BoxCast。该流程不新增全局事件、存档、配置或资源路径。

## 验证记录

- 已通过 JSON 解析确认当前 Qboxy 动画名为 `die`、`distortion`、`idle`、`jump`、`revive`、`walk`。
- 已静态确认 `Actor_IWanner` 不包含 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、
  `DB`、`Save`、`Instance` 或 `Instantiate` 路径。
- 已静态确认 `ActorCtrl.Die()` 原方法体未变化，仅增加 `virtual`；死亡仍复用基类的 `die` 动画入口。
- 已静态确认 `ILevel_001～ILevel_005.prefab` 均引用 `Actor_IWanner.prefab`，其当前
  `OpenDoubleJump/isBuffJump` 均关闭；新门禁不依赖这些序列化开关。
- 离地禁跳增量完成后，定向 `Assembly-CSharp.csproj` 编译通过：`0 error / 101 warning`；warning 为工程现有项。
  Unity PlayMode 视觉与输入验证仍需在编辑器内补充。
- 移动 Ground 增量执行 `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal`：通过，
  `0 error / 101 warning`；目标脚本未新增编译诊断。
- 静态确认实现复用项目既有的 `Rigidbody2D.GetContacts(ContactFilter2D, ContactPoint2D[])` 无分配模式，Filter 仅接收非 Trigger 的
  Ground Layer；支撑法线 Y 小于 `0.5` 的侧面接触不会作为脚下 Ground。
- 静态确认 `ILevel_027` 的 5 个移动平台均在 Ground Layer 且带 Kinematic `Rigidbody2D`；027 控制器只负责平台速度与循环，
  不再包含第二套 Actor 乘客逻辑。
- 本次修改的脚本与中文记录已通过定向 `git diff --check`，未发现新增行尾空格错误。
- 死亡碰撞增量静态确认 `Actor_IWanner.prefab` 根节点存在启用的 `BoxCollider2D`，默认失活的 `pushCollider` 位于子节点；
  覆写只关闭根节点主体碰撞器并对缺失组件保持空安全。
- 死亡碰撞增量执行 `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal`：通过，
  `0 error / 101 warning`；warning 为工程现有项。
- distortion API 增量执行同一定向构建：通过，`0 error / 0 warning`；静态确认公开 API 带中文用途、调用边界和注意事项注释。
- distortion API 增量静态扫描未发现新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、
  `Instance` 或 `Instantiate` 路径；代码定向 `git diff --check` 通过，仅有工作区行尾转换提示。
- 地面检测距离 API 增量执行完整 `Assembly-CSharp` 构建：通过，`0 error / 109 warning`。静态确认共享字段名、默认值和
  两个既有读取点保持不变，公开 API 带中文用途、调用边界、注意事项和参数说明。

## 剩余风险与未验证项

- `uncertain`：尚未在 Unity PlayMode 实际验证待机、连续走动、起跳/落地和死亡四条表现链。
- `uncertain`：尚未在 Unity PlayMode 验证死亡当帧根节点 `BoxCollider2D.enabled` 变为 false、与地面/陷阱接触终止、
  死亡击退表现以及失败重建后新实例碰撞器恢复启用；Android/iOS 物理表现也未验证。
- `uncertain`：尚未实跑地面起跳、起跳后快速连按、平台边缘走落后按跳、下落阶段按跳、重新落地后再次起跳；
  Android/iOS 横屏多指触控也未验证。
- 禁跳判断以共享 `ActorCtrl.IsGround` 为准，实际边缘手感取决于 `groundCheckSize`、`GroundCheckDistance` 和
  `whatIsGround`；起跳后的同帧重复请求已由本地落地周期锁拦截，但走出平台时接地检测转 false 的精确帧仍需
  PlayMode 证据确认。
- `uncertain`：`Main.unity` 当前 `Actor_IWanner` 的 `pushCollider` 序列化引用为空；共享
  `ActorCtrl.HandleCollision()` 会使用该引用，正式接入 IWanner 更新循环前需要在 Inspector 补齐或确认局部处理方案。
- `uncertain`：尚未执行 Android/iOS 横屏触控、帧率及资源加载验证。
- `uncertain`：尚未在 Unity PlayMode 验证静止站台、左右相对移动、反向平台、跨平台缝隙、跳起、触墙、平台循环瞬移及
  `ILevel_001～ILevel_030` 中其它使用 Actor_IWanner 的关卡回归。
- 本实现增加最多 8 个既有接触点的线性扫描，没有逐帧数组分配、场景对象查找或资源加载；移动端实际表现仍以上述设备验证为准。
- `uncertain`：尚未在 Unity PlayMode 验证 `PlayDistortion()` 的完整播放、完成后恢复 idle/walk/jump、移动中触发、
  空中触发、连续重播、被死亡或其它动作打断，以及 Android/iOS 横屏表现。
- `IWannerGame_Portal` 已新增明确调用点；Portal 自己负责传送期间的移动与物理锁，`Actor_IWanner.PlayDistortion()` 本身仍只锁
  自动动画选择，不能在其它调用场景中把它误认为输入或物理锁。
- distortion 播放期间每帧只增加一次轨道引用空值判断，不创建数组、不轮询动画名、不加载资源；实机动画与 CPU 表现仍为 `uncertain`。
- `uncertain`：尚未在 Unity PlayMode 验证运行时把距离改小、改为 0、改大后对落地、离地禁跳、斜坡/移动平台、墙边和
  Gizmos 显示的影响，也未验证 Android/iOS 横屏关卡手感。
- 距离过小可能导致脚下 BoxCast 漏判，距离过大可能在实际接触前提前判定落地；调用方必须按具体关卡几何提供合理值。
  API 没有自动恢复初始值，当前 IWanner Reset 依赖关卡/Actor 实例重建；若未来复用原实例重置，需由调用方显式写回目标值。
- 共享字段从 `private` 改为 `protected` 不改变现有运行时结果，但扩大了派生类可修改边界；当前只有用户明确批准的
  `Actor_IWanner.SetGroundCheckDistance()` 使用该权限，后续子类不得无验证地改写。

## 2026-08-17：GroundCheckSize 运行时变化增量

- `Actor_IWanner` 新增 `SetGroundCheckSize(Vector2)` 与程序集内读取入口，只修改当前实例并把 X/Y 负值按 `0` 处理；
  `ActorCtrl.groundCheckSize` 仅从 `private` 调整为 `protected`，字段名、类型、默认值 `(1, 0.2)` 和既有读取点不变。
- 独立 `IWannerGame_GroundCheckSize` Listener 暴露目标 Actor、目标尺寸与 duration，从当前值线性过渡；零时长立即写入，
  重复执行从当前值续接，Reset/销毁停止 Tween 但不恢复旧尺寸。
- 完整 C# 构建为 `0 error / 109 warning`。PlayMode 的尺寸变化、落地判定、平台边缘、移动 Ground、Reset 与
  Android/iOS 横屏手感仍为 `uncertain`；完整边界见
  `Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_GroundCheckSize功能与风险记录.md`。

## 回滚方式

只回滚离地禁跳规则时，删除 `Actor_IWanner.TriggerGroundedJump()`，并把
`IWannerLevelCtrl.TriggerJump()` 恢复为直接调用 `actor.TriggerJump()`；无需修改 prefab、配置或存档。
只回滚移动 Ground 跟随时，删除 `Actor_IWanner.OnUpdate()` 覆写、接触缓存/Filter 和 `Moving Ground` 区域；平台脚本、prefab、
物理材质、配置和存档无需回滚。
只回滚死亡碰撞关闭时，删除 `Actor_IWanner.Die()` 覆写，并把 `ActorCtrl.Die()` 从 `virtual` 恢复为普通方法；无需修改 prefab、
关卡资源、配置或存档。
只回滚 distortion API 时，删除 `DistortionAnimationName`、`_distortionTrackEntry`、`Action Control` 区域、`Die()` 中的跟踪清理，
并从 `HandleAnimators()` 移除轨道门禁；无需回滚 Qboxy 资源、prefab、关卡、配置或存档。
只回滚地面检测距离 API 时，删除 `Actor_IWanner.Collision Control` 区域，并把
`ActorCtrl.GroundCheckDistance` 从 `protected` 恢复为 `private`；字段名、默认值和 prefab 序列化数据无需迁移。
只回滚 GroundCheckSize 变化能力时，删除 `Actor_IWanner` 的 GroundCheckSize Set/Get 与对应 Listener，
并把 `ActorCtrl.groundCheckSize` 从 `protected` 恢复为 `private`；字段名、默认值和 prefab 序列化数据无需迁移。
完整回滚 Qboxy 动画子类时，再把使用方组件恢复为 `ActorCtrl` 并删除 `Actor_IWanner.cs`、对应 `.meta` 和本说明。

## 2026-08-21：单向平台落地修正与最大下落速度

### 需求与原始基线

- ILevel_054 的密集 `PlatformEffector2D` 平台之间存在实际空间，但共享 `ActorCtrl` 使用长距离 Ground BoxCast；
  从下方穿越和落到相邻平台的过程中可能没有采样到稳定的 `isGrounded=false`，本地
  `_hasJumpedSinceLastLanding` 因此保持为 `true`，落稳后仍拒绝下一次跳跃。
- 修改前 `Actor_IWanner` 没有独立最大下落速度参数，Rigidbody2D 的向下速度由重力和其它玩法逻辑直接决定。

### 当前行为与影响范围

- `Actor_IWanner` 使用主 `BoxCollider2D` 的脚底位置做 `0.05` 世界单位的向下短 BoxCast，只查询 Ground Layer；
  上升阶段不处理，命中 Collider 必须带 `SingleSidePlatform` Tag，且平台顶面与脚底误差不得超过 `0.03`。
- 检测成立且当前落地周期已经跳过时，调用现有 `HandleLanding()`，同时恢复共享落地状态并把
  `_hasJumpedSinceLastLanding` 清为 `false`；跳跃输入当帧也会重新执行同一检测，避免落地与输入更新顺序造成一帧锁死。
- `TriggerGroundedJump()` 在共享 `IsGround` 尚未更新、但脚下短检测已确认单向平台时也允许跳跃；普通 Ground
  仍走原有共享检测，不读取该 Tag。
- 新增 Inspector 字段 `maximumFallSpeed`，默认 `-5`；物理帧只在当前 Y 速度小于该值时向上截断到配置值，
  不修改 X 速度。该字段属于共享 `Actor_IWanner.prefab`，会影响所有复用该 Actor 的 IWanner 关卡。
- 平台穿透/承载继续完全由 `PlatformEffector2D` 控制；Tag 只参与 Actor 状态识别，不恢复已删除的自定义碰撞脚本。
- 本次不修改共享 `ActorCtrl`、DB/Save、业务配置、资源加载、UI、事件、打点或平台 SDK。

### 验证、风险与回滚

- 静态确认 `ProjectSettings/TagManager.asset` 已定义 `SingleSidePlatform`，`IWanner_SinglePlatform.prefab` 的承载
  Collider 位于使用该 Tag 的根节点，Actor prefab 保存 `maximumFallSpeed: -5`。
- 完整 `Assembly-CSharp` 构建通过：`0 error / 106 warning`，warning 为工程既有诊断；目标 tracked 文件定向
  `git diff --check` 与 Tag/prefab 静态断言通过。
- PlayMode 的连续穿透/落地/再跳、平台边缘、Reset、长距离下落、FallArea、移动平台以及 Android/iOS
  横屏手感仍为 `uncertain`。
- 只回滚落地修正时删除 Tag、平台 Tag 赋值、脚下检测区域及 `OnUpdate()`/`TriggerGroundedJump()` 的调用；
  只回滚下落限速时删除 `maximumFallSpeed`、`FixedUpdate()`、`ClampFallSpeed()` 和 prefab 对应字段。

### 2026-08-21：Ground LayerMask 初始化异常修正

- 初版误将 `LayerMask.GetMask("Ground")` 放入 `Actor_IWanner` 静态字段初始化，Unity 在 MonoBehaviour
  反序列化/类型初始化阶段抛出 `NameToLayer is not allowed to be called from a MonoBehaviour constructor`，导致
  `Actor_IWanner` 类型初始化失败，IWanner 更新链无法继续。
- 修正后删除静态 Unity API 调用，新增实例字段 `_groundLayerMask`；`Actor_IWanner.Start()` 先执行
  `base.Start()`，再在运行时缓存主 Collider 和 Ground LayerMask。脚下检测、Tag 条件和最大下落速度语义不变。
- 完整 `Assembly-CSharp` 构建通过：`0 error / 106 warning`；定向扫描确认字段声明/静态初始化器中不再调用
  `LayerMask.GetMask` 或 `LayerMask.NameToLayer`。Unity 重新载入脚本后的实际 Console 清理和 PlayMode 仍需复核。
- 回滚本修正会恢复必现的类型初始化异常，因此不可单独回滚；若撤销整套脚下检测，应同时删除
  `_groundLayerMask`、`Start()` 覆写和对应 BoxCast。

## 2026-08-18：Portal distortion 完成等待增量

- 保留 `PlayDistortion()` 的公开签名和原有普通播放语义，新增程序集内 `PlayDistortionTracked()` 返回本次有效
  `Spine.TrackEntry`；播放条件不满足或 Spine 未返回轨道时返回 `null`。
- `IWannerGame_Portal` 在入口调用普通播放，在出口调用带轨道返回的入口，并通过 `Complete/Interrupt` 区分完整播放与中断；
  Actor 自身仍负责动作期间阻止 `idle/walk/jump` 覆盖，Portal 负责移动与 Rigidbody2D 锁定。
- 重复播放仍会先清除 Actor 的旧跟踪再启动新轨道；Portal 的临时回调绑定在本次返回轨道上，并在完成、取消或中断后解绑，
  不把等待状态写入 Actor 的共享移动或死亡字段。
- 定向 `Assembly-CSharp` 构建通过：`0 error / 109 warning`，warning 为工程既有项。PlayMode 的入口/出口可见时机、
  中断、死亡、Reset 与 Android/iOS 横屏表现仍为 `uncertain`。
- 只回滚该增量时，删除 `PlayDistortionTracked()`，并把 `PlayDistortion()` 恢复为直接执行原 TrackEntry 建立逻辑；
  Portal 同步恢复为不等待 distortion。Qboxy、prefab、配置、存档和共享 `ActorCtrl` 无需迁移。

## 2026-08-18：distortion 方向参数增量

- `PlayDistortion()` 的公开签名与正向播放语义不变；程序集内 `PlayDistortionTracked(bool reverse = false)` 新增方向参数，
  通过 Spine 官方 `TrackEntry.Reverse` 控制反向应用，不使用不受支持的负 `TimeScale`。
- `IWannerGame_Portal` 的入口传入 `false` 并等待完整正向播放，出口传入 `true` 并等待完整反向播放；两种方向继续复用同一条
  TrackEntry 跟踪、`Complete/Interrupt` 清理与 `HandleAnimators()` 门禁。
- 没有修改 Qboxy 动画资源、prefab、共享 `ActorCtrl`、配置、存档、事件、UI、资源或平台链路。临时过滤生成工程中的缺失
  `ILevel041Ctrl.cs` 引用后，完整 `Assembly-CSharp` 构建为 `0 error / 109 warning`。
- `uncertain`：Spine 反向 distortion 的首帧、末帧、与出口缩放叠加效果及 Android/iOS 横屏表现仍需 PlayMode/真机验证。
- 只回滚方向能力时，删除 `reverse` 参数与 `trackEntry.Reverse = reverse`，并让 Portal 两次调用恢复默认正向；其它 distortion
  跟踪、资源、prefab 和 Actor 状态逻辑不需回滚。

## 2026-08-18：distortion 方向参数撤销

- 用户确认 Portal 出口不再反向播放，因此删除程序集内 `PlayDistortionTracked(bool reverse = false)` 的方向参数与
  `TrackEntry.Reverse` 赋值，恢复为只提供正向 `PlayDistortionTracked()`。
- 公开 `PlayDistortion()`、轨道跟踪、`Complete/Interrupt` 清理、死亡清理及 `HandleAnimators()` 门禁保持不变。
- Portal 入口和出口现在都使用正向 distortion；出口仍由 Portal 自己控制提前量和 Scale 时序，不把传送状态加入 Actor。
- 完整程序集构建为 `0 error / 109 warning`；PlayMode 的正向出口表现和 Android/iOS 横屏仍为 `uncertain`。
- 回滚时恢复方向参数和 `TrackEntry.Reverse` 赋值即可；Qboxy、prefab、共享 `ActorCtrl`、配置和存档无需回滚。

## 2026-08-18：Portal 出口反向播放再次启用

- 用户再次确认 Portal 出口 distortion 可以并需要反向播放；程序集内
  `PlayDistortionTracked(bool reverse = false)` 恢复方向参数，并通过 `TrackEntry.Reverse` 应用方向。
- 公开 `PlayDistortion()` 继续不传参数，因此仍保持正向播放；只有 Portal 入口显式传 `false`、出口显式传 `true`，
  不改变其它调用方的默认语义。
- TrackEntry 的 `Complete/Interrupt` 跟踪、死亡清理与 `HandleAnimators()` 动作门禁不变；没有修改 Qboxy 动画资源、
  prefab、共享 `ActorCtrl`、配置、存档、事件、资源加载、UI、打点或平台 SDK。
- 临时过滤生成工程中缺失的 `ILevel041Ctrl.cs` Compile 条目后，完整 `Assembly-CSharp` 构建为
  `0 error / 109 warning`；临时目标已删除，warning 为工程既有项。
- `uncertain`：出口反向动画的首末帧、与延迟后的 OutBack Scale 组合及 Android/iOS 横屏表现尚未 PlayMode/真机验证。
- 只回滚本次方向恢复时，删除 `reverse` 参数与 `trackEntry.Reverse = reverse`，让 Portal 入口、出口恢复无参数正向调用；
  公开 API、轨道跟踪和 WorldCoinAttraction 接入无需回滚。

## 2026-08-18：死亡缩放反馈增量与风险评估

### 需求、基线与影响范围

- 需求：`Actor_IWanner` 触发死亡时，在总计 `0.2s` 内完成一次放大再缩回的视觉反馈。
- 修改前，`Actor_IWanner.Die()` 只清理 distortion 跟踪、复用共享 `die` Spine 动画、停止移动并关闭根节点
  `BoxCollider2D`，没有 Transform 缩放反馈。
- 本次只修改 `Assets/Game/Scripts/Level/Actor/Actor_IWanner.cs` 及中文记录；不修改共享 `ActorCtrl`、
  `Actor_IWanner.prefab`、关卡 prefab、Qboxy 资源、配置、存档、事件、UI、打点或平台 SDK。

### 当前行为与数据流

- `IWannerLevelCtrl.TryActorDeath()` 继续负责死亡幂等与输入锁定，并通过 `ActorCtrl` 引用多态调用
  `Actor_IWanner.Die()`；原有 Spine、移动停止、碰撞关闭、震屏和失败结果上报顺序不变。
- 死亡当帧记录 Actor 根节点当前 `localScale`，前 `0.1s` 放大到当前尺寸的 `1.2` 倍，后 `0.1s`
  缩回记录尺寸；使用 DOTween `Sequence`，Actor 销毁时自动终止。
- 重复进入本地缩放助手时会先停止旧序列，再从当帧尺寸启动；不创建全局事件、协程、资源加载、存档或配置链路。

### 验证证据

- `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal` 通过：
  `0 error / 109 warning`；warning 为工程既有诊断，目标脚本没有新增编译错误。
- 定向 `git diff --check` 通过，仅提示工作区现有 LF/CRLF 转换策略；静态确认没有新增 `OnValidate`、`Reset`、
  `ExecuteAlways`、`ExecuteInEditMode`、DB、Save、资源加载、实例化或全局事件路径。

### 风险、未验证项与回滚

- `uncertain`：尚未在 Unity PlayMode 目测 `1.0 → 1.2 → 1.0` 的实际节奏、与 Qboxy `die` 动画及震屏的叠加效果，
  也未验证 Android/iOS 横屏真机表现。
- 缩放使用普通游戏时间；当前失败链保持 `_isPaused=false`，但若未来外部系统在死亡当帧把 Unity 时间缩放设为 `0`，
  本反馈会随游戏时间暂停，需要重新评估是否改用不受时间缩放影响的 Tween。
- 回滚时删除 `Actor_IWanner` 的 DOTween 引用、死亡缩放常量/运行时序列、`PlayDeathScaleAnimation()` 及
  `Die()` 中的调用即可；共享死亡、碰撞关闭、prefab、资源、配置和存档无需回滚。

## 2026-08-18：Portal 出现改用 distortion_reverse

- 需求：IWanner Actor 经 Portal 搬运到终点后的出现阶段，播放独立的 `distortion_reverse`，不再反向应用
  `distortion`。
- 修改前程序集内 `PlayDistortionTracked(bool reverse)` 始终加载 `distortion`，再通过
  `TrackEntry.Reverse` 切换应用方向；公开 `PlayDistortion()` 默认正向。
- 修改后内部参数更名为 `useReverseAnimation`：`false` 正向播放 `distortion`，`true` 正向播放
  `distortion_reverse`。公开 `PlayDistortion()`、TrackEntry 跟踪、`Complete/Interrupt` 清理、死亡清理和
  `HandleAnimators()` 动作门禁保持不变。
- 静态确认当前 Qboxy 骨骼资源同时存在两个动画名；本次没有修改 Qboxy、prefab、共享 `ActorCtrl`、配置、存档、
  事件、资源加载、UI、打点或平台 SDK。
- 完整 `Assembly-CSharp` 构建为 `0 error / 109 warning`；目标脚本没有新增编译诊断。
- `uncertain`：`distortion_reverse` 的首末帧、与 Portal 出口 Scale 的组合、Reset/Replay 和 Android/iOS 横屏表现
  尚未在 Unity PlayMode/真机验证。
- 回滚时删除 `DistortionReverseAnimationName` 与动画名选择，恢复 `distortion` 固定播放和
  `TrackEntry.Reverse` 赋值；公开 API、轨道跟踪及 Portal 其它流程不需回滚。

## 2026-08-24：AreaEffector2D 重力方向、最大坠落速度与接地检测统一

- `maximumFallSpeed` 字段名、默认 `-5` 与 prefab 序列化数据保持不变；其绝对值现在作为“沿当前重力方向”的速度上限。
- `SetGroundCheckDirection(Vector2)` 接收 AreaEffector2D 计算出的世界力方向并锁定到当前 Actor；
  `PreserveCurrentGroundCheckDirection()` 复用同一入口保存最后方向。零向量被忽略。
- `GroundCheckDirection` 优先使用显式方向；没有显式设置时才继续用 `gravityScale < 0` 向上、其余向下的兼容分支。
- `ClampFallSpeed()` 通过点积取得刚体沿 Ground/重力方向的速度，只移除超过上限的分量；上下方向行为分别等价于
  原来的 `-5/+5` 上限，同时支持水平或斜向区域力，不再依赖 Actor 自身 `gravityScale`。
- 共享 `ActorCtrl` 仍只提供默认向下的受保护方向与 BoxCast/Gizmo 读取点；所有未覆写 Actor 行为保持原样。
- 关闭 AreaEffector2D 时保留最后检测方向；第 058 关清零 Actor 重力并旋转根节点后，世界 Ground 方向不会被还原或带转。
- 单向平台补充探测与移动平台支撑仍主要按上下方向设计；共享基础跳跃仍写入正 Y 冲量。任意角度下这些链路、
  PlayMode、Reset 与 Android/iOS 横屏表现为 `uncertain`。
- 回滚时删除 `SetGroundCheckDirection(Vector2)`，恢复 `PreserveCurrentGroundCheckDirection()` 的直接赋值与
  `ClampFallSpeed()` 的 gravityScale/Y 分支；现有 prefab 字段数据无需迁移。

## 2026-09-01：可叠加并衰减的外部水平速度

### 需求、原有基线与影响范围

- 用户确认撤销 Spring 内固定 `0.2s` 的横向速度保持，改由 `Actor_IWanner` 自身提供速度叠加和减速能力。
- 修改前，共享 `ActorCtrl.HandleMovement()` 每帧依据输入重写 X；横向 Spring 单次 `AddForce` 可能在下一次 Actor 更新时
  被覆盖。固定时间反复写 X 虽能保留弹射，但会暂时压制输入，实际表现不自然。
- 本次修改 `Actor_IWanner.cs` 和 `IWannerGame_SpingTriggerCtrl.cs` 及中文记录；不修改共享 `ActorCtrl`、Actor/Spring prefab、
  关卡资源、配置、存档、资源加载、UI、事件、打点或平台 SDK。

### 当前行为与数据流

- Actor 新增 `externalHorizontalVelocityDeceleration`，默认 `80` 世界单位/秒²，仅控制机关通过
  `AddDecayingHorizontalVelocity()` 提供的附加 X 速度，不改变共享 `moveSpeed`、输入或移动平台配置。
- Spring 方向含 X 且目标为 Dynamic `Actor_IWanner` 时，按 `Δvx = impulse.x / mass` 换算速度增量交给 Actor；
  多次调用直接累加。斜向的 Y 分量仍以 `ForceMode2D.Impulse` 交给 Physics2D，纯竖向弹簧保持原路径。
- Actor 更新顺序为：共享输入移动 → 单向平台接地刷新 → 移动 Ground 的 X 速度 → 外部 X 速度叠加与衰减。
  因此玩家输入和平台运动不会在一段固定时间内被锁死，而是持续参与最终速度合成。
- 每帧使用 `Mathf.MoveTowards(externalVelocity, 0, deceleration * Time.deltaTime)` 衰减；默认配置下，
  `force = 50`、`mass = 3` 产生约 `16.667` 的初始 X 增量，连续时间理论归零约需 `0.208s`，60 FPS 离散约 `0.217s`。
- 触墙时清除附加速度并停止 X，死亡或组件禁用时清除附加量。Actor 更新暂停时不推进衰减，恢复更新后继续。
- `externalHorizontalVelocityDeceleration = 0` 表示附加速度仅应用到当前 Actor 更新，随后清除，不表示无限保持。

### 验证证据

- Roslyn 语法树检查通过；定向静态检查确认 Actor 持有并累加速度、按帧衰减、更新顺序正确，Spring 按质量换算 X 增量。
- 静态确认 Spring 已不包含 `horizontalMomentumRetentionDuration`、横向保持状态、`FixedUpdate` 或 `LateUpdate`；
  共享 `ActorCtrl` 未修改。
- `git diff --check` 通过，仅有工作区既有 LF/CRLF 转换提示。
- 完整 `Assembly-CSharp.csproj` 构建在进入本次源码有效语义编译前，被 `Temp/bin/Debug` 缺少 200 个依赖 DLL 的
  `CS0006` 阻断；此环境结果不能作为脚本编译通过证据。

### 风险、未验证项与回滚

- `uncertain`：尚未在 Unity PlayMode 调整默认衰减量手感，也未验证左右/斜向、连续弹簧、反向输入、移动平台、
  斜墙、Reset/切关/暂停及 Android/iOS 横屏真机行为。
- 连续触发会累加附加速度，当前没有最大值限制；如果具体关卡可在短时间重复触发，需要依据 PlayMode 表现决定是否增加上限。
- 新序列化字段尚未通过 Unity Editor 重载和 prefab 重新序列化验证；未主动改动现有 prefab。
- 回滚时删除 Actor 的外部速度运行态字段、衰减配置、累加/应用/清理方法以及 `OnUpdate()`、`OnDisable()`、`Die()` 中的接入，
  并把 Spring 横向分支恢复为完整方向 `AddForce`；共享 `ActorCtrl` 和资源数据无需迁移。

## 2026-09-10：Actor 实例无限跳跃开关

### 需求、基线与影响范围

- 需求：在 `Actor_IWanner` Inspector 中提供无限跳跃开关，使指定 Actor 实例可在空中重复响应跳跃输入。
- 原有基线：`TriggerGroundedJump()` 只接受接地或已确认站稳单向平台的请求；同一落地周期最多跳跃一次。
- 修改 `Actor_IWanner.cs`、`ActorCtrl.cs` 与 `IWannerLevelCtrl.cs` 的事实注释；同步更新本说明和
  `DevelopMD/已有功能设计与使用记录.md`。未修改 Actor/关卡 prefab、HUD、事件、配置、存档、资源、打点或平台 SDK。

### 当前行为与数据、事件流

- 新增私有序列化字段 `enableInfiniteJump`，C# 默认值为 false。现有 prefab 未写入该字段，因此既有关卡继续保持接地跳规则；
  需要无限跳跃的 Actor 实例在 Inspector 的“`IWanner 跳跃设置`”中主动勾选。
- `PlayerJumpEvent` 仍由 HUD 按离散按下派发，先经过 `IWannerLevelCtrl` 的运行时、输入、暂停、结算和 `IsJumpEnabled` 门禁，
  再进入 `Actor_IWanner.TriggerGroundedJump()`；没有新增或扩大全局事件。
- 开关关闭时完全沿用原接地、单向平台补充探测和落地周期锁。开关开启时，每次有效请求调用既有受保护 `Jump()`：保留 X、
  将 Y 重写为当前 `jumpForce` 并播放原跳跃音效，因此连续点击可反复刷新上升速度。
- `ActorCtrl` 只新增受保护只读 `CanMove`，供派生角色复用现有 `StopMoving()/ResumeMoving()` 门禁；不改变共享字段值、
  现有跳跃判断或其它 Actor 行为。无限跳跃还要求 Actor 未坐下且 `Rigidbody2D` 存在并保持模拟。
- 不新增 Update、协程、Tween、运行时查找、实例化、存档、资源加载、UI、打点或平台调用；Android/iOS 使用同一离散输入链。

### 验证证据

- Unity 6000.0.59f2 自带 Roslyn 使用现有 `Library/Bee/artifacts/1300b0aEDbg.dag/Assembly-CSharp.rsp`
  完整编译 `Assembly-CSharp`，退出码 0；输出只有工程既有 warning，本次目标脚本无编译错误。
- 独立 `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false` 在源码编译前因
  `Temp/bin/Debug` 缺少 200 个 Unity/Package DLL 而返回 `CS0006`，该结果不能作为源码失败证据。
- 静态检查确认开关默认 false、prefab 当前未显式写入该字段、关闭分支仍保留原接地门禁；开启分支同时检查
  `IsSit`、`CanMove`、`RB != null` 与 `RB.simulated`，并只复用现有 `Jump()`。

### 风险、未验证项与回滚

- `uncertain`：尚未在 Unity PlayMode 实测开关关闭/开启、快速连点、上升/下落、平台边缘、单向平台、暂停、Portal、
  Reset/切关，以及 Android/iOS 横屏多指触控手感。
- 无限跳跃按每次离散按下刷新 Y 速度，不把按住解释为自动连跳；最大可达高度由玩家点击节奏决定，这是该开关的预期能力边界。
- 现有 `Jump()` 固定写入世界正 Y；反重力或任意方向重力下的普通跳跃原本也有同一限制，本次未扩大为方向性跳跃改造。
- 2026-09-11 贴墙跳增量已将原私有 `TriggerInfiniteJump()` 更名为两种空中能力共用的 `TriggerAirJump()`。
  如现在只回滚无限跳，应删除 `enableInfiniteJump` 及对应入口分支，保留 `TriggerAirJump()` 供贴墙跳使用；
  只有两种能力同时回滚时才删除该助手。Prefab、配置、存档和事件无需迁移。

## 2026-09-11：贴墙下落限速与连续蹬墙跳

### 需求、原有基线与影响范围

- 需求：`Actor_IWanner` 新增 `isFrictionToWall` 状态；开启后，玩家贴墙下落时使用独立可配速度，并允许每次离散跳跃输入继续蹬墙跳。
- 原有 `maximumFallSpeed` 只控制角色沿当前重力方向的全局最大坠落速度；共享 `ActorCtrl.HandleWallSlide()` 未接入更新链，`Actor_IWanner` 没有贴墙专用限速。
- 原有 `TriggerGroundedJump()` 在无限跳关闭时使用落地周期锁，空中贴墙也不能再次跳跃。
- 本次修改 `Assets/Game/Scripts/Level/Actor/Actor_IWanner.cs`、本说明与 `DevelopMD/已有功能设计与使用记录.md`；不修改共享 `ActorCtrl`、Actor/Level prefab、物理材质、HUD、配置、存档、资源、事件、打点或平台 SDK。
- 任务开始时已有用户未跟踪资产 `IWanner_Actor_065.physicsMaterial2D` 且 `ILevel_065.prefab` 已将它覆盖到 Actor Rigidbody2D；本次不改写、不自动绑定该材质。

### 当前行为与物理/输入流

- 新增私有序列化字段 `isFrictionToWall`，C# 默认为 `false`；未显式开启的现有 Actor 继续使用原有下落与接地跳规则。
- 新增 `frictionToWallFallSpeed`，默认 `-2`，配置约定与 `maximumFallSpeed` 相同：使用小于或等于 0 的值，绝对值是沿当前 `GroundCheckDirection` 的最大下落速度；0 代表不允许继续沿重力方向下落，正数按 0 处理。
- 贴墙不依赖 `ActorCtrl.IsWallDetected` 的朝向 Raycast。本地逻辑复用已预分配的 `ContactPoint2D[8]` 与 Ground Layer `ContactFilter2D`，只认非 Trigger 的真实接触；接触法线在当前重力垂直轴上的对齐度至少为 `0.8` 时才视为墙面。
- `FixedUpdate()` 先确认贴墙状态；当实际下落速度已超过 `frictionToWallFallSpeed` 上限时才移除超出分量，不会把刚进入下落的慢速角色反向加速到配置值。离墙后自动恢复 `maximumFallSpeed`。
- HUD 继续沿原 `PlayerJumpEvent` 进入 `IWannerLevelCtrl.TriggerJump()` 和 `Actor_IWanner.TriggerGroundedJump()`。无限跳关闭但当前确认摩擦墙面时，入口在落地周期锁之前调用原 `Jump()`；因此只要每次输入时仍与墙接触，就可反复刷新原有 Y 跳跃速度。
- 墙跳不增加水平反冲，不自动翻面，不改写 X 速度；死亡、击退、坐下、刚体不模拟或非 Ground Layer 接触不会进入此状态。
- 该检测与限速不创建数组、不查找场景对象、不新增全局事件，不访问 DB、Save、RemoteConfig 或资源链。

### 验证证据

- 执行 `dotnet build Assembly-CSharp.csproj --no-restore --nologo --verbosity:quiet '-consoleloggerparameters:ErrorsOnly;Summary'`：生成成功，`0 error / 126 warning`。
- 静态确认 `isFrictionToWall=false` 时 `ClampFallSpeed()` 仍读取原 `maximumFallSpeed`，`TriggerGroundedJump()` 仍保留接地、单向平台与落地周期锁。
- 静态确认 `ILevel_065.prefab` 中本次目标竖直 Ground 对象位于 Ground Layer；Actor 当前没有 `isFrictionToWall` 实例覆盖，需要使用者在 Inspector 显式开启。
- 定向检查未发现新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、运行时单例或实例化路径。

### 风险、未验证项与回滚

- `uncertain`：尚未在 Unity PlayMode 实测左/右墙、起跳后重新接触、快速连点、墙角/斜面、单向平台侧面、离墙恢复、暂停、Portal、Reset/切关和 Android/iOS 横屏多指输入。
- 角色同时产生超过 8 个 Ground 接触点时，预分配缓冲可能未包含墙面接触；当前 Actor 体积与 Level 65 几何下预期不易达到，但仍需 PlayMode 验证。
- 侧面法线阈值 `0.8` 会把较斜的表面排除；若关卡设计要求在斜墙蹬跳，需根据实际角度调整阈值或改为实例配置。
- 墙跳继续复用原 `Jump()` 的世界正 Y 速度；任意方向重力下的跳跃方向原本就没有统一，本次只让贴墙限速跟随当前重力，跳跃方向仍为 `uncertain`。
- 只回滚本能力时，删除 `MinimumWallNormalAlignment`、`isFrictionToWall`、`frictionToWallFallSpeed`、`IsTouchingFrictionWall()`，恢复 `ClampFallSpeed()` 固定使用 `maximumFallSpeed`，并删除 `TriggerGroundedJump()` 的贴墙分支即可；Prefab、物理材质、事件、配置与存档无需迁移。

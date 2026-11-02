# DLevel_036 船移动功能说明

## 1. 需求与基线

- 需求依据：`DLevel036Ctrl.cs` 类注释及用户后续要求。船需要持续向右移动，默认速度为 `2`，带可调的
  轻微上下浮动；船和乘船角色在碰到运行时 Line 前保持 Kinematic 并同步移动，碰到后一起切换为 Dynamic。
- 修改前 `DLevel036Ctrl` 只有空 `Start/Update`，没有运行行为。
- `DLevel_036.prefab` 的 Ship 初始为 Layer 0 的 Dynamic Rigidbody2D，重力为 `1`；现有 Water 的
  `_waterMask` 为 `2048`（Layer 11），不能作为 Ship 的浮力来源。
- 当前结构已调整：`Ship` 变为 `platform_Stone` 下的纯视觉子节点；物理体由带 HingeJoint2D 的
  `platform_Stone` 承担，`Sp_l93_cl_01` Static Rigidbody2D 作为铰接点，两者共同位于 `Scene/pingtai`。
- 修复前只移动 `pingtai` Transform，Actor 是其兄弟节点且保持独立 Dynamic，因此没有继承船的位移。

## 2. 影响文件与系统

- `Assets/Game/Scripts/Level/DrawLevel/Level36/DLevel036Ctrl.cs`
- `Assets/Game/Prefabs/DrawLevel/DLevel_036.prefab`
- 同目录的脚本结构记录和风险评估
- `DrawLevelCtrl` 只提供本关暂停状态、结算事件和尝试重置分发；共享 Draw、Actor、UI、DB、存档、
  配置、资源加载、analytics 与平台 SDK 均未修改。

## 3. 当前行为

- `DLevel036Ctrl` 挂在 `DLevel_036` 根节点，显式绑定 `motionRoot=Scene/pingtai` 和
  `platformRigidbody=pingtai/platform_Stone`、`actorRigidbody=Scene/Actor`。
- Line 接触前，platform_Stone 保持 Kinematic/零重力；控制器移动整个 `pingtai`，使 Static 铰接点、
  Kinematic 平台和 Ship 视觉保持相对结构；Actor 同时保持 Kinematic，并以 `Rigidbody2D.MovePosition`
  应用与 pingtai 相同的世界位移。
- X 位移为 `moveSpeed * elapsedTime`；脚本默认值为 `2`，当前 prefab 配置为 `1` 世界单位/秒。
- Y 位移为以 pingtai 初始 Y 为中心的正弦曲线；脚本默认幅度为 `0.08`，当前 prefab 配置为 `0.05`，频率 `0.5Hz`。
- 每个物理步使用固定数组查询 platform_Stone Rigidbody2D 与 Draw Layer 非 Trigger 碰撞体的接触。
  首次接触后 platform_Stone 与 Actor 同时切为 Dynamic，并继承切换瞬间的水平速度与正弦 Y 瞬时速度；
  平台 Gravity Scale 设为 `1`，Actor 保留 prefab 当前 Gravity Scale `2`。`pingtai` 停止移动，后续由
  HingeJoint2D、重力和碰撞共同接管。
- 释放后的下一物理步，控制器读取平台在 Actor 原乘坐点的真实点速度，其中包含船头受击后绕铰链产生的
  切向速度，并按 `catapultVelocityMultiplier` 一次性传给 Actor；当前 prefab 倍率为 `1.25`。因此砸中
  船头形成的转动可以把角色像投石机一样抛出，砸中支点附近时抛射速度相应更小。
- 移动从关卡启用时开始，不等待第一笔画线；暂停或结算时不继续推进。
- 结算或 Disable 会把 platform_Stone 与 Actor 固定为 Kinematic 并清速。Attempt Reset 会恢复 pingtai
  世界坐标、platform_Stone 相对位置/旋转、Actor 世界位置/旋转、等待态和相位零，再调用
  `Physics2D.SyncTransforms`。

## 4. 数据、资源、UI 与事件流

- 只使用 prefab 内已绑定的 pingtai Transform、platform_Stone/Actor Rigidbody2D 和现有 HingeJoint2D，
  不实例化或加载新资源，不修改共享 `ActorCtrl`。
- 不读写 Luban、DB、Save、RemoteConfig、PlayerPrefs 或资源地址。
- 不新增 `PlayEvents` 或其它全局事件；仅订阅当前根节点 `DrawLevelCtrl.Resolved`，Disable 时解除订阅。
- 不新增 UI、analytics、广告、IAP、登录、权限或 Android/iOS 平台分支。

## 5. 验证与剩余缺口

- `Assembly-CSharp.csproj` 全量编译通过：`0 error`；warning 未指向 `DLevel036Ctrl`。
- Prefab 静态检查确认脚本 GUID、motionRoot、platformRigidbody、actorRigidbody 和移动/浮动参数已序列化；
  抛射倍率已绑定为 `1.25`；Draw Layer 名与平台 Dynamic Gravity `1` 为本关代码常量。当前 138 个 YAML anchor 无重复，
  非零本地 fileID 无缺失。
- PlayMode 热重载首次加入接触数组时，已有场景实例出现空数组 NullReference；已改为首次查询时懒初始化
  固定 8 项缓冲区。修复 Hot Reload 后日志未再出现 DLevel036Ctrl NullReference。
- 本次新增 Actor 状态与抛射方法属于结构性脚本变更，Editor Hot Reload 日志提示无法直接补丁并要求完整
  重编译；独立 Assembly-CSharp 编译已通过，实际验收前需要退出并重新进入 PlayMode，不能沿用旧场景实例。
- `uncertain`：尚未完成本关 PlayMode 行为验证；Actor 等位移乘船、pingtai 整体位移时铰链同步、Line
  首次接触后两刚体同时 Kinematic→Dynamic 的时机、碰撞冲量、平台摆动/下沉、到达屏幕右侧后的关卡节奏、
  船头/支点不同命中位置的抛射方向与力度、暂停/恢复、Reset/Replay、结算定格与 Android/iOS 横屏表现
  仍需人工验证。

## 6. 回滚

- 仅停用本功能时，可从根节点移除 `DLevel036Ctrl` 或禁用组件；当前 pingtai/platform_Stone/Ship 层级
  和 Actor 可保留，但关卡将不再执行等待移动、角色跟随和接触释放。
- 若完整回滚到旧 Ship 独立刚体模型，必须同时恢复旧 Rigidbody2D/Collider2D、控制器绑定和旧控制代码；
  不能只把代码改回 `Scene/Ship` 查询，因为当前 Ship 是纯视觉子节点。
- 无数据迁移、存档回滚或资源清理要求。

## 7. 2026-08-03 当前结构与简化投石机修正

> 本节以当前 `DLevel_036.prefab` 和 `DLevel036Ctrl.cs` 为准，替代上文中已不存在的
> `pingtai/platform_Stone/HingeJoint2D` 结构描述。

- 当前船是 `Scene/Ship`，自身持有 `Rigidbody2D`、`PolygonCollider2D` 和 `HingeJoint2D`；
  `Scene/ShipPivot` 是同位置、无碰撞体的 Kinematic Rigidbody2D，作为移动铰链支点。
- `platformRigidbody` 显式绑定到 `Scene/Ship` Rigidbody2D，脚本后备解析路径也是 `Scene/Ship`。
  旧 `motionRoot` 已删除，这修复了结构调整后引用为空、船无法移动的问题。
- 等待阶段 Ship 和 Actor 均为 Kinematic；进入等待态时两者一次性获得
  `linearVelocity=(initialHorizontalVelocity,0)`，之后不在 `FixedUpdate` 持续写位置或速度。
  当前 prefab 的 `initialHorizontalVelocity=1`，脚本默认值为 `2`；该值允许正负方向。
- 原上下浮动参数和正弦计算已删除，Ship/Actor 等待时的 Y 速度固定为 `0`。
- 每笔 Line 完成时，控制器通过 `DrawLineFeature.TryGetCompletedStrokeObject` 记录该刚体开始下落前的质心 Y。
- 首次检测到 Ship 与 Draw Layer 接触时，Ship 和 Actor 立即切为 Dynamic。Ship 保留当前路线速度，
  后续转动由 Line 碰撞、HingeJoint2D 与 Physics2D 共同产生；Actor 保留船速，再接受 `ForceMode2D.Impulse` 抛射冲量。
- 基准冲量为 `baseCatapultForce=(0.1,0.8)`。X 符号由 Line 相对 Actor 的左右位置决定，Y 始终向上；
  实际冲量为 `方向化基准冲量 × (1 + 下落高度 × forcePerDropUnit)`。脚本默认值为 `1`，当前 prefab 调参值为 `500`。
- 当前 Actor 质量约 `0.993`、Gravity Scale 为 `2`；零落差时原始 Y 冲量 `0.8` 只能产生约 `0.8` 的瞬时上升速度，
  在有效重力 `19.62` 下表现不明显。现以 `minimumActorUpwardImpulse=8` 兜底最终 Y 冲量，X 和高度增幅算法不变。
- Ship 与 ShipPivot 同时获得一次水平初速度。Line 命中后仅 Ship 切为 Dynamic，ShipPivot 继续 Kinematic 水平移动；
  HingeJoint2D 保持两个锚点重合，因此船随移动支点前进且不下沉。
- 角度限制完全由 prefab 上的 HingeJoint2D `Use Limits` 实现，当前为 `-20° ~ +20°`；控制器不再重复持有
  `maxShipRotationAngle` 或运行时覆盖 limits。旧逐物理步硬夹角度与 `FreezePositionY` 已删除。
- Ship 切为 Dynamic 后使用轻量扭簧回正：`shipSpringStrength=10` 根据相对初始角度施加回正扭矩，
  `shipSpringDamping=2` 抵消角速度。两者只控制回弹，不制造首次旋转，配置为 `0` 可分别关闭。
- 该方案不再等待下一物理步读取接触点速度，避免 Line 在 Ship 仍为 Kinematic 时
  碰撞导致反作用速度丢失。抛射强度随记录的 Line 下落高度变化，水平方向随命中 Line 的左右位置变化；
  仍不直接使用 Line 质量或物理求解后的碰撞冲量。
- 已执行 `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal`：`0 error`、`101 warning`，
  warning 未指向 `DLevel036Ctrl`。Prefab 检查为 133 个 anchor、0 重复、133 个唯一本地引用、0 缺失。
- `uncertain`：尚未完成 Unity PlayMode 下的方向、高度、船旋转中心、Reset/Replay 和移动端手感验证。
  由于字段与方法结构发生变化，必须退出 PlayMode 并等待 Unity 完整重编译后再测试。
- 回滚可恢复之前的点速度继承逻辑和对应 prefab 字段；不涉及配置、存档或运行时资源迁移。
- 清理项：删除重复 `Awake` 初始化、旧 motionRoot/平台局部位姿缓存、逐帧 Hot Reload 补状态、
  接触数组懒初始化、重复 WakeUp、Kinematic 重力改写、持续 MovePosition、`Physics2D.SyncTransforms`、
  未使用的约束缓存、人工船体角速度，以及与 prefab 重复的铰链运行时配置。
- `uncertain`：最小向上冲量 `8` 的最终飞行高度仍受 Actor 碰撞、船体姿态和关卡触发区影响，需 PlayMode 确认。

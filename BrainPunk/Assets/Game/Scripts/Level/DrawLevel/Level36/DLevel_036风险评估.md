# DLevel_036 船移动风险评估

## 1. 原始行为基线与影响范围

- 原始控制器只有空生命周期方法，Ship 不会按需求主动向右移动或上下浮动。
- Ship 原为 Dynamic Rigidbody2D、Gravity Scale `1`；Water 只识别 Layer 11，而 Ship 为 Layer 0。
- 当前 prefab 中 Ship 已成为 platform_Stone 下的纯视觉子节点；platform_Stone Kinematic Rigidbody2D
  通过 HingeJoint2D 连接 pingtai 下的 Static `Sp_l93_cl_01`。
- 修复前 Actor 是 pingtai 的兄弟节点且保持独立 Dynamic；控制器只写 pingtai Transform，缺少乘客位移所有权，
  因而角色不会随船移动。
- 本次只修改 `DLevel036Ctrl.cs`、`DLevel_036.prefab` 和本关/中央中文记录；共享 Draw、Actor、胜负门、
  DB、存档、资源、UI、analytics、SDK 与平台配置不在修改范围。

## 2. Facts

- 控制器挂在关卡根节点并显式引用 `motionRoot=Scene/pingtai`、platform_Stone 与 Actor Rigidbody2D。
- platform_Stone 初始为 Kinematic/零重力；首次接触 Draw Layer 后切为 Dynamic/Gravity Scale `1`，
  继续受现有 HingeJoint2D 和 Static 铰接点约束。
- Actor 等待阶段为 Kinematic，并通过 MovePosition 使用与 pingtai 相同的世界位移；首次接触后与平台同时
  切为 Dynamic、继承同一释放速度，并保留 prefab Gravity Scale `2`。
- 首次释放后的下一物理步，控制器以缓存的 Actor 乘坐点调用平台 `GetPointVelocity`，把线性速度与铰链
  角速度形成的切向速度乘以 prefab 倍率 `1.25` 后一次性写给 Actor。
- `FixedUpdate` 使用固定 8 项 ContactPoint2D 数组查询 Draw 接触；Kinematic 阶段只做常量次浮点计算和
  一次 motionRoot 世界坐标写入及一次 Actor MovePosition，没有逐帧集合分配、场景扫描或实例化循环。
- 只订阅同关卡 `DrawLevelCtrl.Resolved`，并在 Disable 时解除订阅。
- Attempt Reset 恢复 pingtai、platform_Stone 与 Actor 初态、Kinematic 等待状态和浮动相位。

## 3. Direct conclusions

- Kinematic 阶段移动整个 pingtai，使静态铰接点和平台同步平移，避免只移动平台刚体造成 Hinge 拉扯。
- Actor 不再依赖父子层级或摩擦力跟随，而由 Level36 局部控制器在等待阶段显式同步位移。
- 抛射速度来自平台真实点速度，因此命中位置、线质量和平台角响应会自然影响抛射强度，不是固定方向冲量。
- 只有 Draw Layer 非 Trigger 接触能触发 Dynamic；普通地面、Actor、水体和 Trigger 不触发该状态切换。
- Dynamic 释放后停止移动 pingtai 和 Actor，HingeJoint2D 与 Physics2D 成为平台/Actor 运动的唯一所有者。
- 该修改不会直接改变存档、配置、UI、资源加载、打点、广告、IAP 或平台 SDK 流程。
- 运行时持续成本为单个关卡局部 FixedUpdate，未发现移动端 GC 分配风险。

## 4. Hypotheses / risks

- `P1`：接触由下一次 `FixedUpdate` 读取 Physics2D 已形成的 Contact，可能存在最多一个固定物理步的
  Kinematic 接触阶段；首次冲量和画线被推移程度必须在 PlayMode 验证。
- `P1`：Kinematic 阶段直接移动包含 Static/Kinematic Rigidbody2D 子节点的 pingtai Transform；正式设备上
  Physics2D 同步、接触稳定和视觉抖动需要验证。
- `P1`：切 Dynamic 时保留水平速度与浮动瞬时 Y 速度，再叠加 Line 接触和重力；实际弹开、倾覆或下沉
  手感取决于 Line 质量、接触角和物理材质。
- `P1`：Actor 在等待阶段被固定为 Kinematic，不承受重力或独立碰撞位移；这符合当前固定乘船目标，但
  若后续要求接触前可走动/跳跃，需要改为站立检测式乘客方案。
- `P1`：点速度倍率 `1.25` 只经过静态编译验证；船头命中能否达到目标高度、反向船头是否按预期方向抛射、
  支点附近是否足够弱，必须在 PlayMode 以多种 Line 长度/质量/落点验收。
- `P1`：船持续向右且没有终点/循环边界；若本关未在船离开有效镜头前完成，玩家可能失去可解对象。
- `P1`：脚本默认速度/幅度为 `2 / 0.08`，当前 prefab 为 `1 / 0.05`，频率 `0.5Hz`，最终手感需用户验收。
- `OBS`：低帧率和不同移动设备固定步累积下，Interpolation 应缓和视觉抖动，但真机表现尚无证据。

## 5. Verification evidence

- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal`：`0 error`；warning 未指向 `DLevel036Ctrl`。
- Prefab 静态检查：138 个 anchor、0 重复；138 个唯一非零本地 fileID、0 缺失；控制器 GUID 仅保留一个
  运行组件引用，motionRoot、platformRigidbody、actorRigidbody、移动/浮动参数与抛射倍率已绑定；Draw Layer 名与 Dynamic Gravity
  为代码常量。
- PlayMode 热重载曾让已存在实例的新接触数组保持 null，触发 `HasLineContact` NullReference；现改为
  `EnsureLineContactBuffer` 懒初始化。日志在 `Hot Reloading HasLineContact` 后未再出现该异常。
- 本次结构性改动的 `CacheInitialState/ApplyPendingCatapultVelocity` 未被 Editor Hot Reload 插件直接补丁，
  日志要求完整重编译；独立 Assembly-CSharp 编译为 0 error，PlayMode 验收必须重进场景后执行。
- 静态扫描：未新增 `OnValidate`、`Reset`、`ExecuteAlways`、DB/Save、全局事件、资源加载或逐帧分配。

## 6. Missing evidence

- `uncertain`：尚未完成 Unity PlayMode 的 Actor 等位移跟随、pingtai 整体移动/铰链同步、首次 Line 接触
  双刚体切换、船头/支点抛射差异、接触冲量、Dynamic 摆动、承载、
  暂停/恢复、失败重开、Replay、结算定格、屏幕右侧边界和完整可解性验证。
- `uncertain`：Android/iOS 横屏的帧率、触控画线与视觉浮动尚未真机验证。

## 7. Rollback path

- 仅停用本功能时，可移除根节点 `DLevel036Ctrl` 或禁用组件；当前 pingtai/platform_Stone/Ship 层级可保留，
  Actor 可保留，但关卡将不再执行等待移动、角色跟随和接触释放。
- 若完整回滚到旧 Ship 独立刚体模型，必须同时恢复旧 Rigidbody2D/Collider2D、控制器绑定和旧控制代码；
  不能只回滚脚本，因为当前 Ship 是纯视觉子节点。无需数据迁移或清理运行时资源。

## 8. 2026-08-03 当前结构风险更正

> 当前 prefab 已是 `Scene/Ship` 独立 Rigidbody2D 结构，本节取代上文的铰链平台基线。

- 修正前的直接故障：`motionRoot` 与 `platformRigidbody` 为空，脚本仍查找已不存在的
  `Scene/pingtai`，因此 `FixedUpdate` 早退，船不移动。
- 修正后只显式绑定 `Scene/Ship` Rigidbody2D，后备解析也使用该路径；重复 Transform 引用已删除。
- `P1`：下落高度依赖 `OnDrawStrokeCompleted` 成功取得对应 Line 并缓存初始 Y；若热重载或调用顺序导致缓存缺失，
  将回退为下落高度 `0`，仍会施加基准冲量 `(0.1,0.8)`。
- `P1`：方向只使用 Line 相对 Actor 的水平方位，垂直分量始终向上，以避免船头低于 Actor 质心时把角色抛向下方。
  实际手感受 `baseCatapultForce` 与 `forcePerDropUnit` 共同影响；当前 prefab 的高度倍率已调为 `500`（脚本默认 `1`），
  较高落差会快速放大冲量，需 PlayMode 确认不会直接飞出摄像机或越过关卡触发区。
- `P1`：零落差命中新增 `minimumActorUpwardImpulse=8`。直接证据为本关 Actor 质量约 `0.993`、Gravity Scale `2`，
  原始 Y 冲量 `0.8` 理论上仅能上升约 `0.016` 世界单位；新值的实际弹道、碰撞脱离和触发区结果仍需 PlayMode 验证。
- `P1`：Ship 通过 HingeJoint2D 连接到同位置、同水平初速度的 Kinematic ShipPivot，prefab limits 为 `±20°`。
  锚点在两者局部原点，因此 Ship 绕自身原点旋转；若美术期望船头/船尾支点，需调整 `m_Anchor` 和 ShipPivot 位置。
- `P1`：已移除人工 `shipAngularVelocity`，Ship 的转动完全依赖 Line 实际接触产生的碰撞力矩；若命中点太靠近
  铰链中心或接触求解未产生足够力矩，船可能只平移而旋转不明显，需在 PlayMode 验证船头命中手感。
- `P1`：Dynamic 阶段新增回正扭簧，当前强度/阻尼为 `10/2`。强度过大会在 limits 附近抖动，阻尼过小会长时间摆动，
  阻尼过大则可能吞掉 Line 首次碰撞产生的旋转；当前值只完成静态检查，手感仍需 PlayMode 调整。
- `P2`：控制器不再运行时覆盖铰链 limits；角度边界只有 prefab 配置这一份事实源。若 prefab 的连接体或
  `Use Limits` 被误改，脚本不会自动修正，应通过 prefab 检查发现。
- `P1`：移动铰链取代 `FreezePositionY`，Ship 高度由 ShipPivot 锚点约束。Line 持续压船时的铰链求解稳定性、
  limits 边界反弹和视觉穿插待 PlayMode 验证。
- 上下浮动已整段移除，等待与 Dynamic 两阶段的 Ship Y 均为初始值；风险转为水平运动手感是否过于机械，待 PlayMode 确认。
- `P1`：Kinematic Ship/Actor 的水平速度只在进入等待态时设置一次，命中切 Dynamic 时保留当前速度；
  若其它局部逻辑在等待期修改该速度，控制器不会自动纠正，这符合当前“不持续控速”需求。
- `P2`：接触是下一个 `FixedUpdate` 通过 `GetContacts` 检测；高度冲量不依赖已丢失的碰撞冲量，
  但极高速 Line 的连续碰撞检测仍未单独验证。
- 静态验证：Assembly-CSharp `0 error`，101 个已有 warning 未指向本脚本；Prefab 133 anchors、
  0 重复、133 个唯一本地引用、0 缺失。
- `uncertain`：Unity PlayMode、完整重编译后的首次碰撞、Reset/Replay、摄像机边界和 Android/iOS 未验证。
- 回滚路径：恢复点速度继承逻辑与对应 prefab 字段，或禁用 `DLevel036Ctrl`；无存档/配置迁移。

# DLevel_016 害怕动作、无影子与首笔落石功能及风险评估

## 1. 需求与背景

- 角色进入关卡后的默认动作复用 DLevel_008 已配置的害怕动作。
- DLevel_016 的角色不显示影子，但其它关卡和共享 Actor 的默认表现不变。
- 玩家完成一次有效画线后，场景中现有 Stone 开始下落，处理方式与 DLevel_004 相同。
- Stone 应体现明显重物感，不能比运行时画线更轻而被线轻易推着走。
- Stone 推动运行时线撞到角色时，应与玩家直接把线画到角色上一样触发死亡和失败。
- 大部分美术、物理体和失败链已由用户放入 DLevel_016，本次只补齐最小运行时绑定和
  关卡实例 override。

## 2. 原行为基线

- DLevel_016 与 DLevel_008 都引用 GUID 为 `10877d0fc30026040bef4d574b8ebaf0`
  的共享 `Actor.prefab`。
- 本任务开始时 DLevel_016 的 SkeletonAnimation `_animationName` 与 ActorSpineCtrl
  `testAnimName` 都为 `idle`；共享 Actor 的 Shadow GameObject 默认 active。
- DLevel_016 已有一个 Stone：Rigidbody2D 初始 Kinematic、Simulated、gravityScale=1，
  mass=1，并带非 Trigger CircleCollider2D。本关线条配置为 `strokeMass=30`、长度缩放和
  `minimumMassRatio=0.25`，因此实际每笔质量为 7.5～30，原 Stone 连最短线的质量都不到。
- Stone 原本用 OnTrigger2DHandle 把 Player 接触回调绑定到根节点
  `TrapEventHandlle.PlayKnock`；但 Stone 与 Actor 的 Collider 都是非 Trigger，
  `OnTriggerEnter2D` 不会响应这次普通碰撞。角色、受击粒子和失败事件资源均已存在，
  只是绑定方式与物理类型不匹配。
- 原 prefab 没有 `IDrawStrokeCompletedHandler` 把 Stone 从 Kinematic 切到 Dynamic，
  因此画线完成本身不会启动落石。
- DLevel_004 的 DLevel4Ctrl 已实现所需的精确行为：只消费第一笔有效画线，把序列化刚体
  切为 Dynamic 并 WakeUp；Reset/Replay 依赖整关重建恢复初态。
- DLevel_016 还误带入 DLevel001Ctrl：第一笔后每帧调用 ActorCtrl.OnUpdate 驱动向右移动，
  但本关 Actor Rigidbody2D 被覆盖为 Static。已有 PlayMode 日志持续出现
  `Cannot use 'linearVelocity' on a static body.`，并且 OnUpdate 会把 `cover_ears01` 切为
  walk/jump，与本关角色表现冲突。
- 当前 Actor 根 Collider 位于 Actor Layer 且 Tag 为 Player；本关另行启用了角色右侧的
  `pushCollider`，它同属 Actor 层级和 Actor Layer，但 Tag 为 `Untagged`。直接画线通常命中
  主体而可正常死亡；Stone 从右侧推线时会先撞到 `pushCollider`，旧
  `DLevel4RockTrapHandle` 只检查碰撞 Collider 自身的 Layer/Tag，因此静默漏判。

## 3. 影响范围

- `Assets/Game/Prefabs/DrawLevel/DLevel_016.prefab`
- `Assets/Game/Scripts/Level/DrawLevel/Level4/DLevel4Ctrl.cs`（仅泛化职责注释，运行时代码不变）
- `Assets/Game/Scripts/Level/DrawLevel/Level4/DLevel4RockTrapHandle.cs`（复用组件的目标识别逻辑）
- `Assets/Game/Scripts/Level/DrawLevel/Level16/` 两份本地记录
- DrawGame 中央结构演进记录
- `DevelopMD/已有功能设计与使用记录.md`

本次推线命中修复未修改 DLevel_016/DLevel_004/DLevel_008 prefab、共享 `Actor.prefab`、动画资源、
DrawLevelCtrl、DrawLineFeature、TrapEventHandlle、DB、存档、Luban、UI、全局事件、
analytics、广告、SDK 或 Android/iOS 分支。

## 4. 修改后行为

1. DLevel_016 的 Actor nested prefab instance 把 SkeletonAnimation `_animationName` 覆盖为
   `cover_ears01`，与 DLevel_008 的默认害怕动作一致。
2. 同一实例把 ActorSpineCtrl `testAnimName` 也覆盖为 `cover_ears01`，保持 Inspector
   预览与运行时初态一致；运行时主要由 `_animationName` 决定。
3. Actor 子节点 Shadow 的 GameObject `m_IsActive` 在本关覆盖为 0；不只关闭 Renderer，
   从而避免 Shadow 子树残留行为，且不影响其它关卡实例。
4. DLevel_016 根节点增加已有 DLevel4Ctrl，`boxRigidbodies` 唯一绑定本关 Stone
   Rigidbody2D。
5. 第一笔有效画线完成时，DrawLevelCtrl 发现并调用该
   `IDrawStrokeCompletedHandler`；DLevel4Ctrl 把 Stone 切为 Dynamic 并 WakeUp。
6. Stone 从该时刻开始受现有 gravityScale、Collider、斜坡、Water/Buoyancy 和画线物理影响；
   本次不写动画轨迹、不加冲量，也不修改 `allowMultipleStrokes`。
7. Stone 上与非 Trigger Collider 不匹配的 OnTrigger2DHandle 替换为已有
   DLevel4RockTrapHandle，绑定本关 Actor、既有 Knock 粒子并设置 `isCollision=1`。
   普通 Collision2D 命中 Player 后用真实 contact point 播放粒子，再执行 Actor.Die、向上击退、
   `trap` 音效和已有失败事件；Stone Collider 不改 Trigger，因此仍会碰撞地形和画线。
8. 根节点用户已有 TrapEventHandlle 保留但不再由 Stone 调用，避免删除用户已有结构。
9. 从 DLevel_016 根移除误带入的 DLevel001Ctrl；本关没有角色自动移动需求，Static Actor
   不再收到线性速度写入，也不会在首笔后由该组件覆盖害怕动作。
10. Stone Rigidbody2D mass 从 1 调为 100；它约为满长线质量的 3.33 倍、截图常见短线的
    8～10 倍，碰撞时更倾向压住/推开线而不是被线推动。gravityScale 仍为 1，因此自由落体
    加速度和首笔释放时机不变。
11. DLevel_016 的 DrawGame_DrawLine 模板根已有同一 DLevel4RockTrapHandle，运行时克隆线条
    会继承它。处理器不再要求碰撞 Collider 自身带 Player Tag，而是向父级解析
    `ActorCtrl` 并要求结果等于 Inspector 绑定的本关 Actor；因此主体 Collider 与
    `Untagged` 的 `pushCollider` 都会判死，其他 Actor 或普通 Actor Layer 物体不会误触发。
12. Reset、Replay、失败或退出继续整关重建，新实例恢复 Kinematic Stone、mass=100、
    害怕动作和无影子。

## 5. 关键流程

```text
DLevel_016 实例化
  ├─ Actor._animationName = cover_ears01
  ├─ Actor.testAnimName = cover_ears01
  ├─ Actor/Shadow inactive
  └─ Stone = Kinematic + Simulated + mass 100 + gravityScale 1

第一笔有效画线完成
  -> DrawLevelCtrl
  -> DLevel4Ctrl.OnDrawStrokeCompleted(IsFirstStroke=true)
  -> Stone bodyType = Dynamic
  -> WakeUp
  -> Unity 2D 物理驱动下落与碰撞

Stone 直接接触 Actor，或 Stone 推动运行时线接触 Actor/pushCollider
  -> DLevel4RockTrapHandle.OnCollisionEnter2D
  -> GetComponentInParent<ActorCtrl>() == 本关绑定 Actor
  -> contact point 播放既有 Knock
  -> Actor.Die + Knockback + trap SFX
  -> OnTriggerTrapEvent
  -> 既有失败/重建链
```

## 6. 风险项

| 风险 | 等级 | 控制与现状 |
| --- | --- | --- |
| 复制 DLevel_008 全部 Actor override 覆盖本关位置/朝向 | 高 | 只复制两项动画值，不复制 Transform、皮肤、排序等其它 override |
| 直接修改共享 Actor 导致其它关卡无影子或害怕 | 高 | Shadow 和动画都只写在 DLevel_016 nested prefab override |
| 只关闭 Shadow Renderer 仍留下子树行为 | 中 | 关闭完整 Shadow GameObject，沿用 DLevel_010 已有实例配置方式 |
| Stone 在落笔前已受重力 | 高 | prefab 保持 `m_BodyType=1` Kinematic，首笔回调才切 Dynamic |
| 首笔回调绑定错误刚体或空引用 | 高 | DLevel4Ctrl 列表只含 Stone Rigidbody2D fileID `2062884533412947148`；静态引用与 Unity 导入已通过 |
| 后续笔画重复改写物理状态 | 低 | DLevel4Ctrl 只处理 `result.IsFirstStroke` |
| Reset 后 Stone 仍为 Dynamic | 中 | 既有 Draw reset 销毁旧 DLevel 并重新实例化 prefab；组件 Reset 无局部持久状态 |
| 历史类名 `DLevel4Ctrl` 造成误解 | 低 | 保留脚本 GUID 与类名避免迁移风险，泛化 XML 注释并记录两个宿主 |
| 非 Trigger Stone 继续使用 OnTrigger2DHandle，撞人不失败 | 高 | 已替换为 DLevel4RockTrapHandle 的 OnCollisionEnter2D；Stone/Actor Collider 均保持非 Trigger |
| 命中特效仍按角色根节点播放产生偏差 | 中 | 复用 DLevel4RockTrapHandle，优先取 Collision2D contact point，无 contact 才回退 ClosestPoint |
| 历史类名 `DLevel4RockTrapHandle` 造成误解 | 低 | 保留脚本 GUID 与类名避免迁移风险，泛化 XML 注释并记录两个宿主 |
| 斜坡、水面或玩家位置导致落石手感不符合设计图 | 中 | 不人工写轨迹，保留用户已有物理配置；需完整 PlayMode 调整场景参数 |
| DLevel_016 prefab 是用户当前未跟踪资产 | 中 | 只追加明确组件/override，不重排或重建用户现有美术与机关 YAML |
| DLevel001Ctrl 驱动 Static Actor 并覆盖害怕动作 | 高 | 已从 DLevel_016 根移除；不把 Actor 改 Dynamic，避免改变站位和物理。后续若需移动应另立需求 |
| Stone mass=1，运行时线条反而重 7.5～30 倍 | 高 | Stone 单独调为 100，不降低 strokeMass，不影响整关其它线条配置 |
| 高质量 Stone 压细线时发生抖动或穿透 | 中 | 当前保持既有 Discrete；先验证 mass=100 的实际轨迹，若复现再单独评估 Continuous，避免无证据扩大物理改动 |
| Stone 推线先撞 `Untagged` 的 pushCollider，旧 Player Tag 门禁漏判 | 高 | 改为解析碰撞体父级 ActorCtrl，并要求等于序列化绑定 Actor；不修改 pushCollider Tag |
| 给 pushCollider 补 Player Tag 后被成功门、水区或 FailArea 当成第二个玩家碰撞体 | 高 | 保持共享 Actor/pushCollider 的 Tag 不变，目标识别只收敛在绑定 DLevel4RockTrapHandle 的危险碰撞链 |
| Actor 层级未来增加辅助 Collider 后自动进入危险命中范围 | 中 | 这是复合角色 Collider 的预期语义；新增几何仍需覆盖 Stone 直撞、直接画线、推线与防重 PlayMode |

## 7. 验证计划与当前证据

- 静态已通过：136 个 YAML 锚点唯一，内部引用无缺失；Stone 释放组件、Actor 两项动画、
  Shadow inactive、Stone Kinematic/Simulated/mass=100/gravityScale 均存在；Stone 与
  DrawGame_DrawLine 模板各有一个 DLevel4RockTrapHandle，二者三项绑定完整；本关启用的
  `pushCollider` 保持 Actor Layer/Untagged；Stone 不再使用旧 OnTrigger2DHandle，
  DLevel001Ctrl 在本关不存在。
- Unity 6000.0.59f2 已刷新：Assembly-CSharp.dll 时间戳更新，Editor.log 记录最新
  `*** Tundra build success (4.00 seconds)`、CopyFiles 和 DLevel_016 PrefabImporter/两份文档导入；
  日志尾部未见 `error CS`、Compilation failed、Prefab import failed 或引用异常。
- 目标 C#/文档 `git diff --check` 通过；用户 prefab 中 Unity 既有的空值尾随空格未清理。
- PlayMode 待测：首帧害怕动作、无影子、落笔前 Stone 静止、第一笔结束同帧启动重力、
  后续笔画不重复释放、Stone 直撞、直接画线与 Stone 推线命中主体/pushCollider、失败防重、
  Reset/Replay 恢复。
- 设备待测：Android/iOS 横屏分辨率、物理帧率差异、画线与落石时机。

## 8. 回滚

1. 从 DLevel_016 根节点组件列表移除 fileID `900000000000000005` 及其 MonoBehaviour 块。
2. 把 Actor `_animationName` 与 `testAnimName` 从 `cover_ears01` 恢复为 `idle`。
3. 移除 Shadow `m_IsActive=0` 的 nested prefab override。
4. 把 Stone 的 DLevel4RockTrapHandle 脚本与三个字段恢复为任务前 OnTrigger2DHandle 的
   Player Tag/UnityEvent 配置；CircleCollider2D 始终保持非 Trigger。
5. 若需完整恢复任务前 prefab，再恢复根节点 DLevel001Ctrl；这会重新引入 Static Actor
   速度报错和首笔后动画覆盖，只建议用于版本回退，不作为有效关卡配置。
6. 若不再跨关复用 DLevel4Ctrl/DLevel4RockTrapHandle，把其 XML 注释恢复为 Level4 专属描述；
   运行时代码不需要回滚。
7. 保留用户原有 Stone、Actor、场景美术、TrapEventHandlle、DB、资源和平台配置。
8. 本次重量调整可独立把 Stone `m_Mass` 从 100 恢复为 1；不需要回滚画线质量或重力。
9. 若只回滚推线命中修复，把 `DLevel4RockTrapHandle.IsPlayerCollider` 恢复为碰撞 Collider
   自身的 Actor Layer + Player Tag 门禁；这会重新造成 `Untagged pushCollider` 漏判，不应
   作为当前有效关卡配置。

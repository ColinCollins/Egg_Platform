# DLevel_009 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 第 9 关
- 记录日期：2026-07-31
- 交付/验收状态：气球吊门编队稳定释放、单球独立受压和门/线隔离已实现并通过隔离物理验证；
  完整整关流程与设备验收未完成
- 参考基线关卡：DLevel_004
- 选择基线理由：DLevel4 使用同一 `IDrawStrokeCompletedHandler` 在第一笔完成后把多个
  预置 Kinematic Rigidbody2D 同步切换为 Dynamic，与本关机制最接近
- 基线证据限制：DLevel4 结构记录明确标注整关尚未验收，因此其验收状态为 `uncertain`；
  本关只把它作为本地代码和 prefab 结构证据
- 主要脚本：`DLevel9Ctrl.cs`
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_009.prefab`

## 2. 脚本与组件树

```text
DLevel_009
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DelayAutoBeginDrawHandle
├─ DrawLevelCountdownCtrl
├─ DLevel9Ctrl
└─ level9
   ├─ DoorOutside (Rigidbody2D Kinematic -> Dynamic)
   ├─ ballon
   │  └─ Nail (Rigidbody2D Kinematic -> Dynamic, DistanceJoint2D)
   ├─ ballon (1)
   │  └─ Nail (Rigidbody2D Kinematic -> Dynamic, DistanceJoint2D)
   └─ ballon (2)
      └─ Nail (Rigidbody2D Kinematic -> Dynamic, DistanceJoint2D)
```

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 创建与销毁方 | 不负责事项 |
| --- | --- | --- | --- | --- | --- |
| DrawLevelCtrl | 共享主控制器 | Draw 运行时初始化、Handler 分发与结算 | `IDrawLevelRuntime` | DrawPlayCtrl | 不持有 Level9 Nail 规则 |
| DrawLineFeature | 共享功能 | 画线输入、线段与墨水状态 | MonoBehaviour | DLevel prefab | 不操作 Nail |
| DrawLevelCountdownCtrl | 共享局部 Handler | 按现有 10 秒配置处理倒计时 | `IDrawStrokeCompletedHandler` | DLevel prefab | 不释放气球吊门编队 |
| DLevel9Ctrl | 关卡局部控制器 | 首笔前锁定气球吊门编队；统一应用气球浮力；首笔后同步释放门和三个 Nail；每笔完成后隔离该笔与门实体盒碰撞；Reset 恢复初态；恢复三个气球彼此碰撞 | `IDrawStrokeCompletedHandler` | DLevel prefab | 不处理输入、倒计时、胜负、UI、存档或全局事件 |
| DoorOutside | 本关物理对象 | 首笔前保持 Kinematic，首笔后与三个 Nail 同步参与 Dynamic 2D 物理；实体盒只与地面/角色等对象接触，不直接托住玩家线 | Rigidbody2D | DLevel prefab | 不持有画线状态 |
| Nail | 本关物理对象 | 以只限制最大长度的绳关节连接门；受压时可独立缩短绳距，首笔前 Kinematic，首笔后 Dynamic | Rigidbody2D、DistanceJoint2D | DLevel prefab | 不持有画线状态 |

## 4. 生命周期与关键调用

1. DrawPlayCtrl 加载并实例化 DLevel_009，门和三个 Nail 使用 prefab 的 Kinematic 初态。
2. DLevel9Ctrl 在 `Awake()` 缓存四个刚体的初始相对位姿，清零速度并再次恢复
   Kinematic；统一写入 `balloonBuoyancy`，并明确恢复三个气球 Collider 之间的碰撞。
3. DrawLevelCtrl 初始化时发现 DLevel9Ctrl 的 `IDrawStrokeCompletedHandler`。
4. 第一笔有效画线完成后，DrawLevelCtrl 同步分发 `DrawStrokeResult`。
5. DLevel9Ctrl 对每一笔先通过 `TryGetCompletedStrokeObject` 取得运行时线对象，遍历并
   复用本地 List 收集该笔 Collider，只忽略它们与 DoorOutside 实体盒的碰撞。
6. 第一笔再通过 `result.IsFirstStroke` 和本地幂等门禁，在同一回调内把门和三个 Nail
   切换为 Dynamic、清零速度并唤醒；后续笔只补登记门/线碰撞隔离。
7. 三条 DistanceJoint2D 的锚点与可见绳 `LinePoint -> Door EndPoint` 一致，
   `Max Distance Only=true`，只限制最大绳长；既有锁 X/旋转约束保留横向稳定性，三个
   气球仍可分别沿 Y 方向受压和回升。
8. `OnDrawAttemptReset()` 恢复四个刚体的初始位姿、
   Kinematic 状态和零速度。整关重建仍会从同一 prefab 获得相同初态。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或资源 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| DLevel_009 根 | DLevel9Ctrl | 本关气球吊门编队控制器 | 必填 | 不接收画线完成回调 |
| DLevel9Ctrl | `nailRigidbodies[0]` | `level9/ballon/Nail` Rigidbody2D | 必填 | 该 Nail 不释放，其余项继续 |
| DLevel9Ctrl | `nailRigidbodies[1]` | `level9/ballon (1)/Nail` Rigidbody2D | 必填 | 该 Nail 不释放，其余项继续 |
| DLevel9Ctrl | `nailRigidbodies[2]` | `level9/ballon (2)/Nail` Rigidbody2D | 必填 | 该 Nail 不释放，其余项继续 |
| DLevel9Ctrl | `balloonBuoyancy` | 正值浮力倍率，运行时写入三个 Nail 的负 `gravityScale` | 必填 | 旧序列化缺失时使用脚本默认值 `0.6`，仍覆盖 Nail 的 `gravityScale` |
| DLevel9Ctrl | `doorRigidbody` | `level9/DoorOutside` Rigidbody2D | 必填 | 门不会随三个气球同步释放 |
| DoorOutside / Nail | Rigidbody2D | 初始 Kinematic，首笔后 Dynamic；锁 X 与旋转 | 必填 | 无法产生预期编队上升 |
| 三个 Nail | DistanceJoint2D | 连接 DoorOutside；锚点对齐可见绳；最大长度分别为 `2.610846`、`2.622491`、`2.622202`；`Max Distance Only=true` | 必填 | 绳可能伸长或退化为硬杆反馈 |
| DoorOutside | BoxCollider2D | Draw 层 14；主门体 bounds `offset=(0.5309,0.1878)`、`size=(1.5879,2.1866)` | 必填 | 会重新形成错位的不可见承点或阻断绘制 |

## 6. 数据、UI、资源与事件流

- DB/存档：本关脚本不访问；完整胜利后的既有 Draw 进度链不在本次改动范围。
- Luban/配置：无新增或读取。
- UI：不直接操作；倒计时由已有 DrawLevelCountdownCtrl 独立处理。
- 局部引用：DLevel9Ctrl 只序列化 DoorOutside 与三个 Nail Rigidbody2D；DrawLineFeature
  由同根组件在运行时缓存，气球 Collider 通过已绑定 Nail 一次性取得。
- 事件：无新全局事件、局部事件或 UnityEvent。
- 资源加载：随 DLevel_009 prefab 加载，无新增资源地址。
- Analytics/SDK：无。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| 四个 Box 改为三个 ballon/Nail | Level9 实际 prefab 结构不同 | 首笔后释放三根受关节约束的 Nail | 三项绑定、首笔、物理表现、重建 | 否，本关必要差异 |
| SpringJoint2D 改为锚点对齐、只限制最大距离的 DistanceJoint2D | 旧弹簧会伸缩；固定距离模式又会像硬杆一样抵抗压缩 | 初始绳张紧，单球受压时绳可缩短，松手后靠浮力复位 | 三条锚点、最大长度、单球压缩和恢复 | 本关局部修复 |
| 三个气球恢复彼此碰撞并缩小实体半径 | 用户需要气球图片轻微重叠，同时保留真实接触 | 半径 `0.60` 令图片保持重叠、实体初始留微隙，靠近后仍可互撞 | 三组碰撞恢复、初始间距、首笔释放、外部碰撞 | 本关局部修复 |
| DoorOutside 从直接承接线条改为每笔局部忽略 | 隐藏门盒会截断绘制并托住完成线，不符合“气球吊门” | 线只压三个气球；门继续碰地面和角色 | Draw 层阻挡排除、门盒 bounds、逐笔 IgnoreCollision | 本关局部修复 |
| 显式刚体绑定，不运行时按名称扫描 | 数量固定且 prefab 证据明确 | 无首笔时层级扫描和隐式对象选择 | prefab 引用完整性 | 延续既有局部模式 |

## 8. 可复用结论与优化候选

- 本关继续沿用的稳定结构：一个共享 DrawLevelCtrl 加一个关卡局部 Handler。
- 本关局部采用：视觉绳端与物理关节锚点使用同一套 prefab 几何；编队内部碰撞保持
  开启，不改全局 Layer 矩阵；浮力由 DLevel9Ctrl 单一参数控制；门/线只做 Collider 对级隔离。
- 暂不共享：ballon 名称、Nail 数量、门引用、三条最大长度和内部碰撞规则不上提到共享
  控制器；当前只有本关静态与隔离 PhysicsScene2D 证据，尚无多关复用证据。
- 后续关卡需继续观察的候选：固定同类刚体可通过序列化列表同步处理；DLevel9 已有
  隔离 PlayMode 证据，但 DLevel4 与两关完整整关/设备证据不足，暂不提取共享抽象。

## 9. 验证证据与缺口

- 静态检查：DoorOutside 与三个唯一 Nail 均为 Kinematic，脚本四项引用完整；三条
  DistanceJoint2D 都连接同一门刚体，`Max Distance Only=true`；物理锚点与三组可见
  绳端一致，按 prefab 世界几何重算的长度为
  `2.610846200 / 2.622490646 / 2.622202042`，与序列化值误差小于 `0.000001`。
- 静态检查：三个气球仍保留各自半径 `0.60` 的 CircleCollider2D、锁 X/旋转约束和负浮力；
  DLevel9Ctrl 通过 `balloonBuoyancy` 统一写入负 `gravityScale`，并将三组内部碰撞恢复为
  可碰撞，不改全局碰撞矩阵。
- 编译：独立 `dotnet build` 因当前机器只有 .NET host、没有可用 SDK 而无法启动；
  正在运行的 Unity 6000.0.59f2 已完成脚本重编译。`Assembly-CSharp.dll` 时间戳晚于
  `DLevel9Ctrl.cs`，二进制包含 `doorRigidbody`、`balloonBuoyancy` 和
  `RigidbodyInitialState`，最新程序集重载之后 Editor.log 中没有 C# 编译错误。
- 定向 `git diff --check` 通过；仅输出仓库现有 LF/CRLF 转换提示。
- Unity 6000.0.59f2 隔离 PhysicsScene2D 探针已执行并通过：左侧气球向门压近 `0.45`
  后，相对另外两只保留 `0.4437` 的独立位移、产生 `0.4021` 绳松弛；继续模拟后恢复
  张紧，三条绳最大超长为 `0`。
- 同一探针确认完成线 BoxCollider2D 与 DoorOutside 的
  `Physics2D.GetIgnoreCollision(...)` 为 true；之前固定模式下“四体同步位移”的探针
  只作为历史基线，不代表当前独立气球反馈。
- Reset 后四体恢复 Kinematic、初始位姿、零速度与气球互撞；长度 `1 / 6 / 12` 的线条
  实际质量为 `0.345 / 0.8200001 / 1.39`，门盒与线模板材质均解析为 `DrawStrokeFree`。
- Android/iOS：未执行。
- 未验证项：完整 DLevel009 的 LineRenderer 松绳画面、玩家实际短/中/长线压不同数量气球的手感、
  多笔累计、倒计时交互、成功门 Actor 触发、失败与 Replay、完整胜负条件和移动端一致性。

## 10. 风险与回滚

- 主要风险：四项刚体绑定或任一关节字段缺失会破坏编队；限长绳重新张紧可能产生瞬时
  抽动，且可见绳仍是两点直线，不会显示真实下垂；复杂线型的整关手感仍待确认。
- 回滚路径：把 DoorOutside 恢复 Dynamic 初态，把三条 DistanceJoint2D 恢复原
  SpringJoint2D 字段，将三个 CircleCollider2D 半径恢复为 `0.6466392`，并将本关
  `strokeMass / minimumMassRatio` 恢复为共同基线 `200 / 0.25`；移除 SuccessDoor 层、
  DoorOutside Draw 层、门盒对齐、门圆角和逐笔门/线 IgnoreCollision 后，再将
  DLevel9Ctrl 恢复为只释放三个 Nail。
  共享 DrawLevelCtrl、DrawLineFeature 源码、其它关卡和用户已有摆位无需回滚。

## 11. 2026-07-31：气球碰撞、浮力、独立绳反馈与线段承重边界调整

- 用户要求气球图片保留轻微重叠感且气球之间仍能碰撞。三个 CircleCollider2D 半径设为
  `0.60`，图片仍重叠而实体初始留约 `0.01365` 单位微隙；初始化与每次 Reset 都通过
  `Physics2D.IgnoreCollision(..., false)` 明确恢复三组内部碰撞，不屏蔽气球互撞。
- `DLevel9Ctrl.balloonBuoyancy` 以正值暴露浮力；运行时统一写入三个 Nail 的负
  `Rigidbody2D.gravityScale`，Prefab 当前值为 `0.6`。Nail 上同时序列化的 `-0.6`
  只用于编辑器初态可读性；控制器运行时始终按该字段覆盖，不把 Nail 值作为回退。
- DLevel009 作为同日 DLevel001～009 参数同步后的本关物理特例，将 `strokeMass` 从
  同步值 `200` 调为 `5`，并将 `minimumMassRatio` 从 `0.25` 调为 `0.05`；极短线
  最低质量从同步基线的 `50` 降为 `0.25`，避免一小段线就抵消编队净浮力。
- 成功门实例和 DoorOutside 在本关都使用 Draw 层（14），让画线阻挡查询忽略大范围
  Trigger 与隐藏门盒；成功触发组件与 Actor 层触发链保留。DoorOutside 实体盒按主门体
  网格校准为 `offset=(0.5309,0.1878)`、`size=(1.5879,2.1866)`、`edgeRadius=0.08`。
- DoorOutside 实体碰撞体和本关 DrawGame_DrawLine 模板刚体均绑定现有
  `DrawStrokeFree.physicsMaterial2D`（摩擦 0、弹性 0）。运行时克隆线条继承该刚体材质，
  每笔完成后只忽略该笔全部 Collider 与 DoorOutside 实体盒；线仍压气球，门仍碰地面和
  角色，但线不再通过门面法向接触直接挂住或压门。
- 以上改动只作用于 DLevel009；共享 `DrawLineFeature`、成功门源 Prefab、全局 Layer 矩阵、
  其它关卡和玩家参数不改。
- 三条 DistanceJoint2D 现为 `Max Distance Only=true`；隔离 PhysicsScene2D 已验证单球
  独立压缩 `0.4437`、松弛 `0.4021`、浮力恢复、零超长和门/线 IgnoreCollision。
  玩家实际长/短线、多笔累计、成功触发和 Android/iOS 横屏尚未实测。

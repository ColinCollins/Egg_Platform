# DLevel_009 气球 Nail 释放功能与风险评估

## 1. 需求与背景

- 用户反馈 DLevel_009 进入运行后，原本分散悬挂的三个气球会立刻缩到门旁；画线后绳索
  又会明显伸缩并互相纠缠。
- 目标是：第一笔前完整保持编辑器中的气球、绳索和门摆位；第一笔后由三个气球吊着门
  整体上升；线压到某一个气球时，该气球能够独立下沉而不是把三只强制成刚性编队。
- 用户同时反馈门附近存在不可见承点，会截断或托住玩家线。完成线应只通过三个气球
  向门传递负载，不能直接挂在 DoorOutside 的隐藏实体盒上。
- 修改保持在 DLevel_009 局部，不改变共享画线、Actor、UI、存档或其它关卡物理行为。

## 2. 原行为基线

- 三个 ballon 各有一个 `Nail` Rigidbody2D，初始为 Kinematic、`gravityScale=-0.6`；
  DoorOutside 初始却是 Dynamic、`gravityScale=1`。因此画线前门已进入物理解算。
- 三个 Nail 使用欠阻尼 SpringJoint2D 连接门，配置静止长度为 `3 / 2.6 / 2.6`，但按
  当前物理锚点测得的初始距离为 `3.592455 / 3.564276 / 3.483856`。弹簧从首个物理步
  就会主动收缩，画线后仍会弹性伸缩。
- 物理 Joint 的锚点不等于可见绳的 `LinePoint -> Door EndPoint`；LineRenderer 每帧
  跟踪可见端点，因此物理解算后会表现为视觉绳被拉长。
- 三个 CircleCollider2D 半径均为 `0.6466392`。按当前美术摆位，中间气球与左右气球
  有轻微重叠；转为 Dynamic 后会发生内部碰撞解算。四个刚体都锁 X 和旋转，气球只能沿
  Y 方向互相挤开，进一步破坏编队。

## 3. 影响范围

- `Assets/Game/Scripts/Level/DrawLevel/Level9/DLevel9Ctrl.cs`
- `Assets/Game/Prefabs/DrawLevel/DLevel_009.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_5/Level5TrackLine.cs` 仅作为可见绳跟踪逻辑
  的只读证据，未修改
- Level9 本地功能、风险与脚本结构记录
- DrawGame 中央结构演进记录
- `DevelopMD/已有功能设计与使用记录.md`

未修改共享 `DrawLevelCtrl`、`DrawLineFeature`、GameplayPanel、Actor、DB、存档、Luban、
资源地址、全局事件、analytics、SDK 或平台分支。

## 4. 修改后行为

1. DoorOutside 与三个 Nail 在 prefab 中都以 Kinematic 进入关卡；`DLevel9Ctrl.Awake()`
   缓存四个刚体的初始相对位姿、清零速度并恢复 Kinematic，画线前不会参与 Dynamic
   物理解算。
2. 三条 SpringJoint2D 改为 `Max Distance Only=true` 的 DistanceJoint2D。物理锚点
   分别使用三个可见绳 `LinePoint` 的本地位置，门端统一使用可见 `EndPoint` 的本地位置。
3. 三条最大长度为 `2.610846 / 2.622491 / 2.622202`，与当前可见绳初始世界距离一致；
   绳被拉紧时不会超过该长度，被线压近门时允许缩短并松弛，不再像硬杆一样抵抗压缩。
4. 第一笔完成后，门与三个 Nail 在同一回调内切换为 Dynamic、清零速度并唤醒。四个
   刚体继续锁 X 与旋转以避免横向缠绕，但三个气球可各自沿 Y 方向下沉；移开负载后，
   负重力浮力会使对应气球回升并重新绷紧绳子。
5. 三个单位质量气球的负重力与单位质量门的正重力合力仍向上，理论整体加速度约
   `+1.962 units/s²`；`balloonBuoyancy` 统一写入三个 Nail 的负 `gravityScale`，未新增
   逐帧施力。
6. 三个气球的 CircleCollider2D 半径由 `0.6466392` 收到 `0.60`。当前美术图片仍有
   明显重叠感，但实体碰撞体初始留有约 `0.01365` 单位间距，不会在首个物理步互相顶开；
   初始化与每次 Reset 仍两两调用 `Physics2D.IgnoreCollision(..., false)`，因此靠近后会
   发生真实碰撞，它们与玩家画线、场景和其它物体的碰撞也保留。
7. DLevel009 作为同日 DLevel001～009 参数同步后的本关物理特例，将 `strokeMass`
   从同步值 `200` 调为 `5`，并将 `minimumMassRatio` 从 `0.25` 调为 `0.05`；极短线
   最低质量从同步基线的 `50` 降为 `0.25`，长线仍按同一曲线逐渐增加承重。
8. 成功门实例根节点和 DoorOutside 实体盒都在本关使用 Draw 层（14），大范围成功 Trigger
   与门实体盒都不再被 `DrawLineFeature` 的阻挡查询提前命中；完成线仍位于 Ground 层，
   不依赖修改全局 Layer 碰撞矩阵。
9. DoorOutside 的 BoxCollider2D 按 Spine `door_idle` 主门体网格校准为
   `offset=(0.5309,0.1878)`、`size=(1.5879,2.1866)`、`edgeRadius=0.08`，不再在门左侧
   形成错位的不可见承点。
10. 每一笔完成后，`DLevel9Ctrl` 取得该笔运行时子 Collider，并只对它们与 DoorOutside
    实体盒调用 `Physics2D.IgnoreCollision(..., true)`。门仍与地面和角色碰撞，玩家线仍与
    三个气球碰撞；因此线条负载只能经气球和张紧绳传到门，不会直接挂在门面上。
11. DoorOutside 实体 BoxCollider2D 与本关 DrawGame_DrawLine 模板刚体继续绑定已有
    `DrawStrokeFree.physicsMaterial2D`（摩擦 0、弹性 0），用于未被局部忽略的其它接触。
12. `OnDrawAttemptReset()` 恢复四个刚体的初始位姿、Kinematic 状态、零速度和气球互撞；
   `_hasReleased` 防止同一次尝试重复释放。
13. 脚本没有 `Update()`、协程或实例化；只在每笔完成时扫描该笔自身 Collider，并复用
    控制器内的 List，避免逐帧扫描和每笔数组分配。

## 5. 数据、资源、UI 与事件流

```text
DLevel_009 实例创建
  -> DoorOutside 与三个 Nail 保持 Kinematic 初态
  -> 缓存初始位姿、应用浮力并恢复三个气球彼此碰撞
第一笔有效画线完成
  -> DrawLevelCtrl 分发 DrawStrokeResult
  -> DLevel9Ctrl.OnDrawStrokeCompleted
  -> 当前笔画全部 Collider 与 DoorOutside 实体盒建立局部 IgnoreCollision
  -> 门与三个 Nail 同步切换为 Dynamic 并 WakeUp
  -> 三条 DistanceJoint2D 只限制最大绳长；受压气球可独立松绳
后续每一笔完成
  -> 只补登记该笔与 DoorOutside 的 IgnoreCollision，不重复释放刚体
Reset / Replay
  -> 本地回调恢复四个刚体初始位姿、Kinematic 与零速度
  -> 整关重建时仍从同一 prefab 获得相同初态
```

- DB/存档：本关脚本不访问。
- Luban/配置：无新增或读取。
- UI：无直接操作；既有 10 秒倒计时配置不在本次修改范围。
- 事件：无新 `PlayEvents`、Bear 全局事件或 UnityEvent。
- 资源加载：随 `DLevel_009.prefab` 加载，无新增地址。
- Analytics/SDK：无。
- 平台：Android/iOS 共用 Unity 2D 物理路径，无平台分支。

## 6. 风险项

| 风险 | 等级 | 控制与现状 |
| --- | --- | --- |
| 门或 Nail 引用缺失、绑错 | 低 | prefab 显式绑定四个唯一 Rigidbody2D；静态引用与隔离 PlayMode 运行均通过 |
| 第一笔回调未分发到 DLevel9Ctrl | 中 | 隔离 PlayMode 已直接调用同一回调并确认四体同步 Dynamic；完整 DrawLevelCtrl 分发仍随整关验收 |
| 限长绳重新绷紧时产生瞬时抽动 | 中 | 只抗拉、不抗压；隔离 PhysicsScene2D 中气球可独立压缩并恢复，复杂玩家线型仍需完整 PlayMode 观察 |
| 气球内部碰撞造成初始分离或抖动 | 低 | 半径 `0.60` 后初始实体间距约 `0.01365`，三组碰撞保持启用 |
| 画线被成功门 Trigger 提前截断 | 中 | 本关 SuccessDoor 实例根节点覆盖到 Draw 层，保留成功触发但排除画线阻挡查询；需 PlayMode 验证 Actor 通关 |
| 线段在 DoorOutside 隐形实体盒挂住 | 低 | 门盒已对齐主门体并放到 Draw 层；每笔完成后只忽略线/门这一碰撞对，隔离探针确认 IgnoreCollision 生效 |
| 线不再直接压门导致难度变化 | 中 | 这是“气球吊门”的目标边界；短线只压单球，足够长/多的负载需压住更多气球才使门下降，仍需整关难度验收 |
| 松绳视觉仍显示为两点直线 | 中 | `Level5TrackLine` 不模拟绳子下垂；本次只修真实受力，不新增分段绳，需目测确认可接受 |
| Reset 时物理状态残留 | 低 | 隔离 PlayMode 已确认四体位姿、Kinematic、零速度与气球互撞全部恢复 |
| 移动端物理差异 | 中 | 无逐帧施力或平台分支；Android/iOS 横屏仍需实机验证 |

## 7. 验证证据

- 静态确认 DoorOutside 与三个 Nail Rigidbody2D 初始均为 `m_BodyType=1`
  （Kinematic），质量均为 1、约束均为 5（锁 X 与旋转）。
- 静态确认 DLevel9Ctrl 脚本 GUID 与 prefab 组件引用一致。
- 静态确认 `nailRigidbodies` 三项与 `doorRigidbody` 分别指向四个唯一刚体。
- 静态确认三条 Joint 均为 Unity class ID 232 的 DistanceJoint2D，连接同一 DoorOutside，
  `m_MaxDistanceOnly=1`，没有遗留 SpringJoint2D 的 damping/frequency 字段。
- 按当前父级位姿、三个 Nail 位姿和右侧 Nail 的 `-6.741°` 旋转重算锚点距离：
  `2.610846200 / 2.622490646 / 2.622202042`；与序列化距离误差均小于 `0.000001`。
- 静态确认脚本实现 `IDrawStrokeCompletedHandler`，没有 `Update`、编辑器生命周期入口、
  DB、Save、全局事件或运行时实例化。
- 静态确认 DLevel9Ctrl 对三个已绑定 Nail Collider 两两显式恢复碰撞，并统一写入
  `balloonBuoyancy=0.6` 对应的负 `gravityScale`。
- 静态确认三个 CircleCollider2D 半径均为 `0.60`，碰撞体小于气球图片；既保留视觉
  重叠，也避免初始实体穿透。
- 静态确认 DLevel009 的 `strokeMass=5`、`minimumMassRatio=0.05`、SuccessDoor 与
  DoorOutside 根层均为 14；DoorOutside BoxCollider2D 为
  `offset=(0.5309,0.1878)`、`size=(1.5879,2.1866)`、`m_EdgeRadius=0.08`。
- 静态确认 DoorOutside BoxCollider2D 与本关嵌套 DrawGame_DrawLine 模板 Rigidbody2D
  都引用 `DrawStrokeFree.physicsMaterial2D`；共享画线 Prefab 和其它 DLevel 不受此覆盖影响。
- Prefab 静态校验通过：188 个 Unity YAML 文档 ID 唯一，其中 166 个普通文档、
  22 个 `stripped` 文档；三条限长 DistanceJoint2D、四个 Kinematic 且锁 X/旋转的
  刚体、门绑定和脚本 GUID 均符合预期；无旧 Spring 字段。
- 独立 `dotnet build` 因当前机器只有 .NET host、没有 SDK 而无法启动。正在运行的
  Unity 6000.0.59f2 已重新生成晚于本次脚本修改的 `Assembly-CSharp.dll`，其中包含三个
  新增实现符号；最新程序集重载后 Editor.log 中没有 C# 编译错误。
- 定向 `git diff --check` 通过；仅输出仓库现有 LF/CRLF 转换提示。
- Unity 6000.0.59f2 隔离 PhysicsScene2D 探针使用真实 prefab 的三个限长 Joint：将左侧
  气球向门压近 `0.45` 后，它相对另外两只保留 `0.4437` 的独立位移，绳距缩短形成
  `0.4021` 松弛；继续模拟后松弛恢复为 `0`，三条绳最大超长为 `0`。
- 同一探针构造一笔带子 BoxCollider2D 的运行时线并调用控制器局部逻辑，
  `Physics2D.GetIgnoreCollision(doorCollider, strokeCollider)=true`；探针总结果为 PASS。
- 先前固定距离配置下“四体位移完全一致、编队漂移为 0”的隔离结果只作为历史基线，
  已被本次“单球可独立压缩”的目标行为取代，不再代表当前配置。

## 8. 剩余缺口

- 隔离物理已验证单球独立压缩、浮力恢复、最大绳长和线/门碰撞隔离；尚未通过完整
  DrawPlayCtrl 流程观察 LineRenderer 可见绳、真实玩家笔画接触和首帧画面。
- 未验证不同形状/多笔线段与门、气球和场景障碍的最终接触手感，以及失败、界面 Reset、
  Replay 的整关流程。
- 未验证成功门 Trigger 层覆盖是否完整保留 Actor 通关，以及门盒对齐后的地面/角色接触。
- 未验证 10 秒倒计时、完整成功/失败条件及 Android/iOS 横屏物理与渲染平滑度。

## 9. 回滚路径

1. 将 DoorOutside Rigidbody2D 初态恢复为 Dynamic。
2. 将三条 DistanceJoint2D 恢复为原 SpringJoint2D 的锚点、距离、damping 和 frequency。
3. 如需回退碰撞间距修复，将三个 CircleCollider2D 半径从 `0.60` 恢复为 `0.6466392`。
4. 将本关 `strokeMass / minimumMassRatio` 恢复为同日共同基线 `200 / 0.25`；移除
   SuccessDoor/ DoorOutside Draw 层、门盒对齐、圆角和门/线模板零摩擦材质局部覆盖。
5. 如只回退本轮独立受力，将 `m_MaxDistanceOnly` 恢复为 0，并从 DLevel9Ctrl 移除
   每笔完成线与 DoorOutside 的局部 IgnoreCollision；这会恢复硬杆编队和门直接托线。
6. 从 DLevel9Ctrl 移除门绑定、初态缓存/恢复、浮力覆盖和气球碰撞恢复，恢复为只释放三个 Nail。
7. 回退本关与中央文档中的本次编队稳定记录；用户已有的 Level9 摆位、绳索视觉、
   画线和玩家参数保持不变。

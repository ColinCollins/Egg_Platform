# DLevel_061 角色首笔释放功能与风险评估

## 1. 需求与原行为

- 关卡：DrawGame 061（`20061 / DLevel_061`）。
- 需求：玩家完成第一笔有效画线后，夹子张开并把所夹角色放下。
- 原因：Prefab 根只有 Draw、倒计时和 D053 蜂群组件，没有实现 `IDrawStrokeCompletedHandler` 的猫夹释放控制器；Actor Rigidbody2D 虽已预置为 Kinematic，但没有运行时路径将其脱离 `clipSwingPivot` 并切换 Dynamic。D053 控制器的 `actor` 同时为空，角色受击失败链也无法正确取得目标。

## 2. 修复方案

- 在 `DLevel_061.prefab` 根节点挂载现有 `DLevel58ActorReleaseCtrl : DLevel2Ctrl`，继续复用已经用于 058/059 的可见 Intro、闭夹持有、首笔 `clip_open`、保留世界位姿脱父级、Dynamic/WakeUp 和 Retry 完成态。
- 显式绑定 Alien 根 Transform `519152089635510330`、夹子摆动点 `910070555515108981`、Actor 根 Rigidbody2D `1138963634036635986` 和 D061 根释放父级 `8549164348132234925`。
- 为嵌套 Actor 补出 ActorCtrl stripped 引用 `1716601560298323527`，并绑定到 `DLevel53Ctrl.actor`，恢复蜂群命中角色时的标准特效/失败目标。
- 角色继续使用本关已有 `DrawStrokeStable` 防滑材质；不修改质量、重力、阻尼、约束或 Physics2D 矩阵。

## 3. 生命周期与影响边界

1. 转场完全揭开后，`DrawLevelCtrl` 调用可见 Intro；Alien 从共享 Rig 的 `entranceStartPoint` 飞到 Prefab 当前 FINAL，角色在此期间保持 Kinematic 并由闭夹持有。
2. 第一笔有效画线完成时，继承流程播放 `clip_open`，把 Actor 保持世界位姿挂到 D061 根，清零速度后切换为 Dynamic 并唤醒。
3. Retry/Replay 直接恢复 FINAL、闭夹、角色持有位姿和 Kinematic，不重播画面外飞入。
4. 保留 D061 的 `strokeMass=100`、`maxInkLength=40`、10 秒成功倒计时、蜂群参数、场景尖刺和布局；不修改脚本源码、共享 Prefab、Excel/JSON 配置、UI、存档、Analytics 或 SDK。

## 4. 验证、风险与回滚

- 已静态确认：`20061 -> DLevel_061` 和排序配置均存在；控制器、Alien、夹子、Actor Rigidbody2D、ActorCtrl、蜂巢和 Knock 引用均可解析。
- 已静态确认：Prefab 修复后有 163 个唯一 anchor，无重复 anchor 或悬空本地 fileID；全部脚本 GUID 可定位到现有 `.cs.meta`；释放控制器恰好一份。
- 风险：D061 复用 DLevel2 的猫夹动画命名和 `bone2` 契约；未来修改共享释放流程时应连同 002、020、047、058、059、061 一起回归。
- `uncertain`：Unity PlayMode 中的实际飞入轨迹、首笔开夹/落地、尖刺与蜂群碰撞、10 秒成功和 1.5 秒失败临界、Reset/Replay 及移动端尚未验证。
- 回滚：从根节点移除 fileID `900000000000000007` 及其组件块，把 `DLevel53Ctrl.actor` 恢复为空，并删除本次新增的两个 Actor stripped 引用，即恢复修复前状态；无需回滚共享脚本或其它关卡。

## 2026-08-18 尖刺命中死亡补充

### 需求、基线与结果

- 原行为：场景中的 `Caltrop_long` 尖刺保留 `TriggerItem`（Layer 9）和 `OnTrigger2DHandle`，但复制后移除了旧 `TrapEventHandlle`，各触发器的持久化目标为 `fileID: 0`，因此角色碰到尖刺没有有效的死亡入口。
- 新行为：仅在 `DLevel_061.prefab` 根新增 `DLevel61SpikeTrapHandle`。组件运行时扫描本关子节点的 `TriggerItem` 触发器，清空旧 Tag 过滤并绑定 `OnEnter`；命中本关 Actor 后先占用 `DrawLevelCtrl.TryBeginFailurePerformance()`，再播放 Knock、调用 `ActorCtrl.Die()` 与向上击退，`0.5s` 后通过 `CompleteFailurePerformance()` 复用标准失败重开。
- 失败演出占用期间共享 `DrawLevelCtrl.Tick()` 不会因 `Actor.IsDied` 提前结算，成功倒计时也会暂停；蜂群命中和尖刺命中只接受先到达的失败锁，避免互相覆盖。
- 蜂群命中仍由 `DLevel53Ctrl` 独立处理；尖刺组件不修改 D053 蜂群脚本、共享 `DLevel32SpikeTrapHandle`、`TrapEventHandlle`、Actor Prefab、物理层矩阵或倒计时配置。

### 影响文件与绑定

- Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_061.prefab` 根组件 `900000000000000008`。
- 组件脚本：`Assets/Game/Scripts/Level/DrawLevel/Level61/DLevel61SpikeTrapHandle.cs`，只服务 D061，避免改变 D032 已验收的失败时序。
- 序列化引用：`drawLevelCtrl=900000000000000001`、`knockEffect=3757748896162189796`、`failDelay=0.5`、`knockbackForce=10`。
- D061 当前扫描到 `10` 个 `OnTrigger2DHandle` 尖刺实例；原有触发器事件仍保留但空目标不会再承担死亡职责，运行时动态监听负责实际调用。

### 验证、风险与回滚

- 已完成静态检查：组件脚本 GUID 可解析；根组件列表与 MonoBehaviour 定义各恰好一份；10 个尖刺触发器均存在，且从 `Caltrop_long` 源 Prefab 继承 `TriggerItem`（Layer 9）；绑定引用均指向 D061 内对象；未修改共享资源或 D032 脚本。
- 已完成 Unity Roslyn 全量编译：`0 error / 107 warning`（warning 为工程既有诊断）；本轮再次用 Unity `DotNetSdkRoslyn/csc.dll` 配合 `Library/Bee/artifacts/1300b0aE.dag/Assembly-CSharp.rsp` 复核，结果仍为 `0 error / 107 warning`。
- `uncertain`：尚未在 Unity PlayMode/移动设备验证角色从夹子释放后实际触碰每组尖刺、击退位置、失败延迟，以及尖刺与蜂群先后命中临界；静态代码已确认失败锁会暂停成功倒计时并阻止共享 Tick 提前结算。
- 回滚：从 D061 根 `m_Component` 移除 `900000000000000008`，删除对应 MonoBehaviour 块及 `DLevel61SpikeTrapHandle.cs/.meta`；不回滚共享 `DLevel32SpikeTrapHandle`、D053 脚本或其它关卡。

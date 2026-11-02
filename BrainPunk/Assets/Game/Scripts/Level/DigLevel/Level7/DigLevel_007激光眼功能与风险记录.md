# DigLevel_007 激光眼功能与风险记录

## 2026-10-14 持续检测与移开后恢复伸长：修改前核对

- 最新验收：用户截图中阻挡物已经移开，激光仍停在原短端点；要求持续检测，允许在LateUpdate进行。当前每步仍有射线，但_growthStopped把探测长度及显示增长永久限制在第一次命中长度，所以即使Collider旋转／移走也无法探测更远目标或继续伸长。这是设计缺口，先前0.001端点容差只处理静止面的舍入，不能解决该行为。
- 保留行为：初始宽度／最大宽度／扩张速度、Ground层筛选、最近物体截断、只有实际激光接触才播放Spark、命中点跟随、Actor统一失败及其1秒等待，用户资源／地图／布局保持。DigPlayCtrl.BuildLevelAsync先销毁旧实例再Instantiate，不需公开Restart或对象复用状态。
- 拟改：仅DigGame_LaserEye手写脚本。Awake一次校验、构造过滤器、清粒子并设置初始宽度；撤OnEnable／公共RestartLaser、_growthStopped、_failureAccepted和前轮容差。LateUpdate每渲染帧以maxWidth×世界X轴缩放独立检测最近Ground／Actor，当前显示宽度按速度扩张并被当前hit距离裁剪；前端尚未到达目标则继续扩张且不播，接触时播放，目标远离后持续扩张，目标靠近立即裁剪。检测输入不再依赖上一帧截断长度，结果锁仍由所属关卡控制。
- 验证：复现旧静止宽度在Ground移开后永久锁定；新版本实际源资源／倾斜Box验证持续命中、远离伸长、靠近缩短、旋转后重新取点、移出射线后继续长／停火、下一更远同层目标、忽略其它层、跨Duration不重播、Actor失败与旧时长／暂停。复查41组非整数姿态，不用简单墙面替代截图机制；Editor／Android编译。每帧一条预分配Raycast，无新分配／对象扫描，移动端帧耗与现场最终美术仍uncertain。
- 回滚：只恢复脚本本轮前快照相关状态／FixedUpdate／初始化增量，不回写源Prefab、用户MaxWidth23、第三方Spark或地图。旧设计／验证作为历史证据保留，最终以本节收尾结果为准。

### 最终实现与快速核验

- 已移除_growthStopped、_failureAccepted、前轮端点容差、OnEnable和公开RestartLaser。Awake仅初始化一次；LateUpdate每帧按maxWidth独立检测最近有效Collider，显示长度以Time.deltaTime扩张并被当前接触距离裁剪。远处目标虽被探测到，激光前端未到达前不播放火花；物体移开后继续长，靠近后立即变短。所属关卡结果锁／暂停仍停止玩法推进，OnDisable清粒子。
- 实际眼睛源与同一Ground几何：旧实现Ground右移2米后连续100次检测，显示宽度保持16.6894341；新实现直接推进LateUpdate后恢复到18.689436，证明不再被首次短长度锁住。Editor／Android条件编译0错误，源码与隔离副本一致。
- 为响应用户明确要求加速收尾，本轮只完成上述直接行为对照和编译，没有重跑前轮25项／41样本，也没有正式Game窗口、所有旋转路线、Actor／暂停／广告和真机全链验证，这些当前版本边界为uncertain。先前容差／Restart测试属于历史版本，不当作当前全覆盖证据。
- 没有修改用户Prefab、地图、粒子或共享控制器。核验记录见 `DevelopMD/验证记录/Dig激光眼_2026-10-14/验证记录.md` 最新持续检测章节；回滚快照TEMP `brainpunk-dig-laser-continuous-20261014/DigGame_LaserEye.cs.before`。

## 2026-10-14 Ground持续播放反馈：事前排查

- 用户实测：激光已经触到Ground，火花没有一直播放；上一轮25项隔离检查只覆盖简单测试墙面，不能据此保证当前007连续显示。
- 当前可观察配置：眼睛源仍绑定Spark根ParticleSystem、Ground掩码64、偏移0；Spark／Glow的Loop为true且Rate为80／5，007新增的MaxWidth22属于用户修改，应保留。暂无现场运行实例状态日志，是否由漏检、粒子停止或渲染遮挡导致仍uncertain。
- 源码疑点：首次命中后_currentWidth截断至hit.distance/axisScale，下一步以相同_width*axisScale为射线终点；非整数坐标／缩放舍入或目标稍移可能漏检。无命中分支立刻StopEmitting，而同Collider分支不会恢复已停止发射的粒子。先用当前眼睛资源与实际坐标／墙面及多缩放样本复现，不先改生产资源。
- 拟修改边界与验证：只有取得复现证据后，定向调整检测长度与粒子播放状态处理，保留视觉真实截断点、Layer、Actor结果、粒子颜色／发射和用户MaxWidth22。验证持续命中跨粒子Duration、同Collider不清空重播、错层／离开命中、Actor失败，Editor／Android编译。完整现场主观视觉／真机仍需验证，回滚只撤脚本本轮增量。

### 修改前复现证据与确定改法

- 真实Unity6000.0.59f2加载当前眼睛源和Spark，按007右墙位置10.18／尺寸2.57及眼睛原姿态检测，初次命中宽度16.6894341；保持墙体静止，随后检测丢失且Spark.isEmitting=false。41组位置／缩放／旋转样本中7组复现同样问题。证据TEMP `brainpunk-dig-laser-continuity-20261014/continuity-before.txt` 与 `unity-before.log`。
- 确定改法：仅在激光已截断时为射线末端增加0.001世界单位容差，显示宽度继续按原真实命中距离截断，不因容差伸长；同Collider仍更新位置且正常发射时不重播，若已停止则Play(true)恢复发射而不清空粒子。无命中时也检测仍在发射的粒子，以覆盖原Collider被销毁后的清理。保持Layer／源资源／失败／用户MaxWidth配置。
- 补充验证：同一41样本修后无丢失、实际Spark跨多个Duration连续发射、暂停门禁、停止后同Collider恢复、不循环重启、真正移开后停止；复跑25项Layer／Actor回归。实际Game窗口最终渲染仍不由粒子状态检查替代。

### 最终修复与证据

- 脚本新增私有0.001世界单位接触容差，只在已经截断后的检测末端使用；视觉宽度仍按真实hit.distance及原宽度上限裁剪。持续命中且粒子正常发射时不重启，停止时只Play(true)恢复，不StopAndClear。失去命中和Collider销毁仍停止发射，Restart／失活清理保持。
- 同一41组实际眼睛资源／坐标／缩放／旋转，修前7组截断后丢失，修后0组丢失。实际007眼睛与墙面坐标下，实际Spark连续13次0.17秒采样（约2.21秒，跨过0.7秒Duration）均发射且产生粒子；显示宽度保持约16.6894341。停止后恢复、离开／返回、Collider销毁和Restart均通过。该组共22项检查，另复跑原25项Layer／Actor回归通过，Editor／Android条件编译0错误。
- 来源：`DigGame_LaserEye.cs` 与TEMP验证副本相同；证据 `DevelopMD/验证记录/Dig激光眼_2026-10-14/验证记录.md` 的持续播放修复章节。仅改脚本与记录，没有写眼睛／007／第三方Spark资源，用户MaxWidth22及粒子参数保留。原25项只覆盖简单测试墙面，不能推出实际浮点端点稳定，本轮已补该缺口。
- 尚未在用户前台Game窗口重新试玩或验证真机渲染／粒子过绘，标uncertain。回滚只撤ContactDistanceTolerance、停止后Play恢复和失去命中的发射状态检查，使用TEMP `brainpunk-dig-laser-continuity-20261014/DigGame_LaserEye.cs.before` 定向核对，不恢复用户资产。

## 2026-10-14 Layer 阻挡与持续火花：修改前核对

- 当前需求：用户明确将阻挡改为 Layer 判定；命中阻挡时播放已放入的 `SparkExplosion2D`，位置移动到实际触碰点。同一个 Collider 的接触点改变时要继续跟随，保持用户已调好的连续电焊火花，不反复清空重播。Actor 的统一死亡／失败等待保持。
- 实际基线：`DigGame_LaserEye` 仍支持显式 Collider 列表和补充层，`PlayHitEffect` 在同一 Collider 时提前返回，因此位置只更新一次。制作人已把眼睛提取为新 `DigGame_Eyes.prefab`，007 现在嵌套该源并绑定本关 Level，原单墙 Collider 仍由 override 赋值；源的 `hitEffect` 仍指向 Knock。Spark 已在源内，根／Glow 循环且 Rate over Time 为 80／5，属于任务前美术修改，保留。此前内联 007 的 fileID 已变化，不复用旧快照覆盖。
- 来源与边界：脚本是手写，源 Prefab 的 MonoBehaviour `9058373552695847348`；Spark 源 GUID `ec61825cb06be72419bb09eee03af08c`、根 ParticleSystem `19876930`、Glow `19849174`。`ProjectSettings/TagManager.asset` 已有 Ground 层6，007 三个边界和 CollisionTiles 当前都是 Default；`Physics2DSettings` 层碰撞矩阵均允许，不因边界改 Ground 关闭 Actor 接触。没有匹配的知识库粒子流程，依据本地源码／YAML及本关既有文档；CodeGraph CLI 不可用。
- 拟改位置与影响：仅撤激光精确 Collider 字段，保留 `stoppingLayers` 序列化名并将 Inspector 标为“阻挡 Layer”。源显式绑定 Spark 根 ParticleSystem、层掩码64，007只移除旧 Collider override并把三个边界设为Ground，沙土继续Default。Spark／Glow在眼睛源内覆盖Play On Awake关闭，由脚本开始命中时播放；不回写第三方源或用户已调的发射参数。命中每个物理步更新XY，保留Z及可调法线偏移；同Collider不重新Play，失去命中时停止发射，失活／重启清理。
- 验证范围：Editor／Android条件编译；真实Physics2D验证多个同层Collider、其它层忽略、最近阻挡、同Collider接触点移动且不重播、失去命中停止、新实例初态、Actor失败和1秒等待；原生Prefab导入与SerializedObject验证源／007层和Spark绑定。正式007美术、移动端粒子过绘及完整HUD／广告链仍为uncertain。定向回滚上述脚本和源／007相关块，不覆盖地图、Spark源材质或用户并行布局。

### 完成结果、验证与回滚

- Inspector 仅保留“阻挡 Layer”（序列化名 `stoppingLayers`），撤精确 Collider 列表；默认眼睛源掩码为 Ground（64）。007 三个边界设为 Ground，移除旧墙 Collider override，继续从源继承掩码。Actor 不依赖此掩码，仍通过刚体中的 `DigActorCtrl` 进入原统一失败入口。
- `DigGame_Eyes` 显式绑定自身 Spark 根粒子。Spark 和 Glow 在此实例覆盖 Play On Awake=false，保留制作人循环与80／5每秒发射、材质、姿态、颜色等参数；未改第三方源。命中特效偏移默认0，XY精确使用 `hit.point`，Z保持制作人设置。原 Knock 实例保留，但不再由激光驱动。
- 同一个 Collider 也每物理步更新火花位置，更新后才检查是否需要播放；没有反复 Stop/Clear/Play，连续火花不会因脚本每帧重启。失去命中停止发射，剩余粒子自然结束；失活或 RestartLaser 清空。脚本旧说明中“指定物体／补充层／同Collider只播一次”已与新行为冲突，定向更新对应注释，其它注释保留。
- Unity 6000.0.59f2 原生Prefab导入与真实Physics2D／DOTween／ParticleSystem共25项通过；Editor／Android条件编译0错误。源眼睛14个既有块逐字保持，只改控制器与Spark实例2块、新增1个粒子引用块；007的150块逐字保持，只改三边界Layer和眼睛实例旧override共4块。源码／Prefab与夹具副本哈希一致。记录见 `DevelopMD/验证记录/Dig激光眼_2026-10-14/验证记录.md` 的Layer增量章节。
- 夹具先遇临时粒子自动剔除导致时间不推进，又遇把Actor放在002实心沙中后被挤出射线；分别仅设置TEMP粒子AlwaysSimulate和关闭该用例TEMP地形碰撞、固定Actor测试位置后重跑通过，没有为夹具更改生产物理／粒子参数。由此不能宣称007完整路线或主观火花视觉已验收；实际关卡试玩、广告重建和移动端过绘仍uncertain。
- 本次增量回滚：按TEMP `brainpunk-dig-laser-layer-20261014` 的 `.before` 快照定向恢复上述代码和Prefab块，撤新增Spark引用／PlayOnAwake覆盖并恢复原层和单墙override。不整文件覆盖用户布局、地图或正在编辑的第三方Spark源。

## 需求、基线与范围

用户已在 `DigLevel_007.prefab` 放置眼睛和 `Sp_wd_jg_01` 激光资源，要求激光逐步伸长，碰到阻挡Layer中的物体后按实际命中点截断并持续播放火花；碰到任一本关 Actor 时播放死亡表现，进入游戏失败，并在命中约 1 秒后走原关卡重置链。速度、宽度、阻挡层、动效和等待时间由 Inspector 配置。

修改前 `Sp_wd_jg_01` 已是左侧 Pivot 的 Tiled `SpriteRenderer`，并带 `BoxCollider2D`，但宽度固定且没有检测、命中表现或失败上报。`DigLevelCtrl` 的旧失败入口在接受请求后播放约 0.5 秒 Actor 死亡动画，动画完成即提交失败。`DigPlayCtrl` 后续统计、广告、黑幕和关卡重建属于现有流程，本功能不另建重置事件。

本轮只接入用户当前未提交的 007 Prefab、Dig 局部激光脚本及统一失败入口的可选等待参数。地图烘焙、墨水、提示、ReachTarget 胜利、Actor 资源、其它关卡和第三方包不在修改范围。

## 当前结构与显式绑定

- `DigGame_Eyes` 挂载 `DigGame_LaserEye`，所属关卡显式绑定 007 根 `DigLevelCtrl`。
- 激光 Renderer、检测起点和 Trigger 均绑定现有 `Sp_wd_jg_01`；脚本只修改 `SpriteRenderer.size.x`，不会按名称搜索资源或节点。该 Sprite 的 Pivot 为 `(0, 0.5)`，因此宽度沿本地 X 正方向从眼睛向右增长。
- `Sp_wd_jg_01` 的 `BoxCollider2D` 改为 Trigger，尺寸和 X 偏移随显示宽度同步；实际命中判定使用 Physics2D Raycast，避免依赖 Trigger 回调的离散进入时机。
- 阻挡Layer当前为Ground，007三个边界使用该层；沙土CollisionTiles继续Default，因此不会截断。新增阻挡物只需设置其Collider节点的Layer，无须添加对象引用。持有 `DigActorCtrl` 的刚体始终参与检测。
- 命中特效显式绑定眼睛源内的 `SparkExplosion2D` 根粒子，命中时移动到 `hit.point + normal * offset`；同Collider只更新位置，不重播，不在运行时实例化对象。
- 当前参数：初始宽度 `0.1`、最大宽度 `20`、扩张速度 `16`、表面偏移 `0`、失败重置等待 `1` 秒。均可在 Odin/Inspector 修改。

## 运行流程与状态边界

关卡初始化完成且未暂停、未结算时，激光在 `FixedUpdate` 用固定容量 32 的 `RaycastHit2D` 缓冲区沿本地 X 正方向检测。未命中时按 `expandSpeed * fixedDeltaTime` 增加本地宽度；命中指定阻挡物或本关 Actor 时换算世界距离为本地宽度并截断，记录当前 Collider，避免对同一物体重复播放粒子。

Actor 命中后调用 `DigLevelCtrl.TryFail(actor, failureDelay)`。控制器沿用唯一结果锁、关输入、清理接触、停止病菌吞噬、冻结其它 Actor 和失败 Actor 死亡动画；等待时间从接受失败时开始，与约 0.5 秒死亡动画并行，只有动画完成且最短等待归零后才提交一次 `Resolved(Failed)`。暂停或应用切后台期间两者均保持，恢复后继续。旧 `TryFail(actor)` 和病菌过大失败仍传入 0 秒额外等待，保持死亡动画完成即提交。

失活时激光清空粒子；新关实例Awake设置初始宽度，不提供原实例Restart接口。运行逻辑外层均受 `Application.isPlaying` 保护，不在Prefab编辑态初始化玩法、关卡或服务。检测时序及动态阻挡规则以最新“持续检测”章节为准，以下首次FixedUpdate测试为历史证据。

## 性能与移动端

每个激光实例每个物理步最多执行一次无分配 Raycast，结果写入预分配的 32 项数组；没有 `GetComponentsInChildren`、对象名扫描、每帧实例化、材质复制或 GPU 读回。命中后停止宽度增长，但仍保留当前关卡状态门禁；结果锁后不再检测。32 项容量足以覆盖当前 007 布局的单束激光，若未来同一直线上放置超过 32 个可命中 Collider，需要重新评估容量或层过滤。

Editor 和 Android 条件编译均为 0 错误。Android/iOS 真机物理耗时、低帧率下的主观扩张速度及粒子过绘尚未测量，标记为 `uncertain`。

## 验证证据、剩余风险与回滚

Unity 6000.0.59f2 隔离运行使用真实 Physics2D、DOTween、实际 `DigLevelCtrl` 和 `DigActorCtrl`，共 12 项通过：右墙近表面截断、Trigger 同步、一次命中特效、命中后停止扩张、Actor 命中锁失败、约 1 秒一次提交、延迟入口、暂停保持、恢复继续，以及旧失败入口约 0.5 秒提交。Editor/Android 条件编译均为 0 错误。Prefab 静态检查确认激光脚本、Renderer、Trigger、右墙和粒子引用闭合。

隔离运行使用等价 Sprite 和测试阻挡体，没有对生产 007 做完整 Game 窗口美术验收；完整 HUD、失败广告、黑幕重建、资源加载、双端真机和多个激光实例同场仍为 `uncertain`。现有 `Sp_wd_jg_01` 使用 Tiled；若 Sprite 导入改为非 Full Rect，Unity 可能给出平铺显示警告，应保持当前资源导入与 Tiled 配合或由美术确认切片方式。

回滚时只移除 007 的 `DigGame_LaserEye` 组件和本地命中特效实例，将激光 BoxCollider 恢复原设置，删除新增脚本/meta，并定向撤销 `DigLevelCtrl` 的失败等待重载及状态字段。不要整文件还原用户未提交的 007 Prefab、地图、美术或同日其它 Dig 改动。

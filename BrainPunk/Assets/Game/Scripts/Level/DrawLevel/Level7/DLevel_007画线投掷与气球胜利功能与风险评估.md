# DLevel_007 画线投掷与气球胜利功能与风险评估

## 1. 需求与背景

- 本关只允许一笔有效画线；完成后保持线段原缩放不变，但先隐藏运行时 LineRenderer，
  不再让线段可见地停在 `level7/startPoint`。
- Actor 播放一次 `throwing`，在线性归一化进度 45% 的既有释放帧才把线段设置到
  `startPoint`，同帧启用物理并投出；经过一次物理步进及随后渲染更新后用 0.2 秒恢复
  原画线透明度，动画完成后恢复循环 `idle`。
- 投掷世界方向 `forceDirection` 与冲量 `throwForce` 由 `DLevel7Ctrl` Inspector 字段
  控制；Scene 视图从 `startPoint` 绘制青色方向箭头。
- 投掷线段触碰 Enemy 后，把现有 `Knock_Explosion_2` 移到本次 `Collision2D` 接触点播放，
  并保留粒子原有 Z；无接触点时才回退到 Enemy Collider 的最近点，
  Enemy 调用共享 `ActorCtrl.Die()`，并进入 Draw 专用失败重置。
- 气球爆炸后等待 2 秒；等待期间 Enemy 死亡则失败重置且不触发胜利，窗口结束时
  Enemy 仍存活才进入 DrawGame 胜利结算。
- 投出的线段在尚未砸中气球时进入本关左右 FallArea，立即进入 Draw 专用失败重置；
  气球 Trigger 接触当帧先记录命中，之后进入 FallArea 不再覆盖既有胜利窗口。
- ballon 不再播放缩小动画；本关局部爆炸 Handle 在作者摆放的世界爆点生成 1.3 倍粒子，
  并在粒子启动同帧隐藏气球全部视觉。承载执行器的不可见根节点会等粒子与胜利回调结束
  后再安全销毁。

## 2. 原行为基线

- 任务开始时 `DLevel7Ctrl.cs` 只有需求注释、空 `Start()` 和空 `Update()`，没有运行时行为。
- `DLevel_007.prefab` 已有 Player、Enemy、`level7/startPoint`、气球爆炸执行器和
  `DrawGame_DrawLine` 模板实例。
- 线模板在该关实例中被覆盖为 `tools` Tag、Layer 14（Draw）；气球的
  `OnTrigger2DHandle` 只接受 `tools` Tag，并执行既有缩放、粒子、音效、线条淡出和
  延迟销毁链。
- `DrawLineFeature` 完成有效笔画后先创建 `EdgeCollider2D`、启用 Rigidbody2D，再同步派发
  `StrokeCompleted`；此前没有公开边界让关卡局部 Handler 取得对应运行时线段对象。
- 本任务开始前 `DLevel_007.prefab` 已同时存在暂存与未暂存改动；这些关卡美术、机关和
  气球配置均视为用户已有改动，本次只追加 Level7 运行时集成。

## 3. 影响范围

- `Assets/Game/Scripts/Level/DrawLevel/Level7/DLevel7Ctrl.cs`
- `Assets/Game/Scripts/Level/DrawLevel/Level7/DLevel7ActorCtrl.cs`
- `Assets/Game/Scripts/Level/DrawLevel/Level7/DLevel7BalloonExplosionHandle.cs`
- `Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_6/PlayVfxListener.cs`
- `Assets/Game/Prefabs/DrawLevel/DLevel_007.prefab`
- Level7 本地文档、DrawGame 中央结构记录和 `DevelopMD/已有功能设计与使用记录.md`

未修改共享 `ActorCtrl`、`DrawLevelCtrl` 的结算实现、GameplayPanel、DB、存档、Luban、
资源地址、全局事件、analytics、广告、SDK 或平台分支。

## 4. 修改后行为

1. `DLevel7Ctrl` 实现 `IDrawStrokeCompletedHandler`，由当前 DLevel 的
   `DrawLevelCtrl.InitializeRuntime()` 在局部层级发现。
2. 第一笔有效画线完成后，通过
   `DrawLineFeature.TryGetCompletedStrokeObject(StrokeIndex, out GameObject)` 取得该笔对象。
3. 本关立即暂停该线段 Rigidbody2D 模拟，按 LineRenderer 包围中心重设局部点和
   EdgeCollider2D 点，从而保持原视觉位置并得到稳定的移动/投掷中心；线段缩放不变。
4. 线段完成时缓存本实例 LineRenderer 的原始 Gradient，将透明度置 0 并禁用 Renderer；
   不提前把可见物体放到 `startPoint`，不执行 `DOMove`、`DOScale` 或共享材质写入。
5. `DLevel7ActorCtrl : ActorCtrl` 播放 `throwing`；归一化进度 45% 时回调
   `DLevel7Ctrl`，在同一帧设置 `startPoint`、启用 Renderer、恢复线段物理模拟并按归一化
   `forceDirection` 与冲量执行 `AddForce(..., Impulse)`；等待一次物理步进及随后渲染
   更新后，在 `throwFadeDuration=0.2` 秒内恢复原 Gradient alpha，使第一帧可见时
   线段已经飞行。
6. `throwing` 完成后 Actor 回到循环 `idle`；缺失动画轨道时使用一次性回退，仍投出线段
   并恢复 idle，避免关卡软锁。
7. Enemy prefab 实例上的既有 `LayerCollisionDetector` 把目标层配置为 Draw，并通过
   动态 `Collision2D` UnityEvent 调用 `DLevel7Ctrl.OnDrawLineHitEnemy(Collision2D)`；
   DLevel7 不再给运行时线段动态添加另一套碰撞转发组件。
8. Enemy 首次被命中时，`Knock_Explosion_2` 优先移动到
   `Collision2D.GetContact(0).point` 并保留粒子原 Z；无接触点时使用 Enemy Collider
   相对投掷线段中心的 `ClosestPoint`，再调用 Enemy 的共享 `Die()`，并由
   `DrawLevelCtrl.TryFailAndReset(...)` 进入 Draw 专用整关重载。
9. 气球既有 `EventHandleListener.Handle` 调用 `OnBalloonExploded()` 后只开启 2 秒窗口。
   该窗口使用当前尝试版本和结果门禁：Enemy 死亡优先失败；窗口结束且关卡仍未结算时才
   调用 `DrawLevelCtrl.TryResolve(Success)`。
10. ballon 执行器的首项改为本关 `DLevel7BalloonExplosionHandle`：立即关闭气球全部
    Collider，按 `Knock_Explosion` 模板的明确世界位置/旋转在稳定 `level7` 父节点生成
    1.3 倍实例并显式 Play，同一调用栈随即隐藏 ballon 根下全部 Renderer/CanvasRenderer；
    0.1 秒后只放行原 SFX、线淡出和 `OnBalloonExploded`，视觉协程继续独立等待全部
    ParticleSystem `IsAlive(true)==false`。同一胜利 UnityEvent 还会回写
    `OnBalloonOutcomeStarted`，只有粒子结束且该回调已实际执行才销毁特效和不可见根节点。
    原缩放项与固定时长 DestroyListener 已移除，2 秒胜利窗口节奏不随粒子尾长延后。
11. `FallArea (4)` 与 `FallArea (5)` 实例各自增加 LayerTriggerDetector，只过滤 Draw
    Layer；动态 Collider2D 回调通过 `attachedRigidbody` 确认是本次投掷线段，且仅在
    气球尚未爆炸时调用 `DrawLevelCtrl.TryFailAndReset(...)`。
12. DLevel_007 的 `DrawLineFeature.allowMultipleStrokes=false`；第一笔完成后共享门禁关闭
    输入，后续落笔也会被 `CanBeginStroke()` 拒绝。
13. Reset/结算会取消胜利/渐显协程并保持当前透明状态直至线段销毁，避免先恢复后在帧末
    销毁造成闪帧；组件 Disable 会在层级已隐藏时恢复 Renderer，运行时线段仍由
    `DrawLineFeature.ResetAttempt()` 统一销毁。

## 5. 数据、资源、UI 与事件流

```text
DrawLineFeature 完成第一笔有效画线
  -> DrawLevelCtrl.OnStrokeCompleted
  -> DLevel7Ctrl.OnDrawStrokeCompleted
     -> 暂停线段物理、重设中心、保持缩放、隐藏 LineRenderer
     -> DLevel7ActorCtrl.PlayThrowSequence
        -> throwing 45%：设置 startPoint，同帧启用物理/冲量
        -> 下一物理步及随后渲染更新后：飞行中的线段用 0.2 秒渐显
        -> throwing 完成：Actor 回 idle
投掷线段尚未命中气球时进入左右 FallArea
  -> LayerTriggerDetector（targetLayerMask=Draw）
  -> DLevel7Ctrl.OnThrownStrokeEnteredFallArea(Collider2D)
  -> 校验 attachedRigidbody 为本次投掷线段
  -> DrawLevelCtrl.TryFailAndReset
投掷线段触碰 Enemy
  -> Enemy.LayerCollisionDetector（targetLayerMask=Draw）
  -> DLevel7Ctrl.OnDrawLineHitEnemy(Collision2D)
  -> Knock_Explosion_2 移到物理接触点并 Play（保留原 Z）
  -> Enemy.ActorCtrl.Die
  -> DrawLevelCtrl.TryFailAndReset
  -> DrawPlayCtrl 失败整关重载
运行时线段子 BoxCollider2D（Draw Layer）碰到气球
  -> LayerTriggerDetector（targetLayerMask=Draw）
  -> BaseItemExecutor
  -> DLevel7BalloonExplosionHandle
     -> 关闭 ballon 全部 Collider，不缩放 Nail
     -> 在模板世界坐标生成 1.3 倍 Knock_Explosion 并 Play
     -> 同一调用栈隐藏 ballon 全部 Renderer / CanvasRenderer
     -> 0.1 秒后放行后续执行器；视觉继续等待 ParticleSystem.IsAlive=false
  -> 既有 SFX + 线淡出 + EventHandleListener
  -> DLevel7Ctrl.OnBalloonExploded + ExplosionHandle.OnBalloonOutcomeStarted
  -> 等待 2 秒
     ├─ 窗口内 Enemy 死亡：失败重载，不提交胜利
     └─ Enemy 存活：DrawLevelCtrl.TryResolve(Success)
  -> 爆炸粒子与胜利回调均完成：销毁不可见气球根和特效实例
  -> DrawPlayCtrl 胜利面板、星级与进度链
```

- DB/存档：本关脚本不直接读写；胜利后的星级与进度继续由 DrawPlayCtrl 既有链处理。
- Luban/配置：无新增字段和读取。
- UI：无直接操作。
- 资源：复用当前 Actor 的 Box Spine `throwing`/`idle`、线模板、气球粒子和音效；
  不修改 `Knock_Explosion` 资源的 `playOnAwake`、Simulation Space 或 Sorting。模板实例
  只提供爆点与粒子配置，运行时实例由 Level7 局部 Handle 创建并清理。
- 事件：不新增 `PlayEvents` 或 Bear 全局事件；使用局部 Handler、动画回调和 prefab
  UnityEvent。
- 编辑器预览：`OnDrawGizmos()` 只读取 `forceDirection`、`startPoint` 与当前 Transform，
  不访问 DB、存档、配置、单例或运行时初始化链。
- 性能：只在第一笔完成时分配一个点数组、短渐显 Gradient 和动画协程；位置为单次赋值，
  Enemy/FallArea 均复用既有 Layer detector，胜利窗口为短协程，无新增 `Update()`、
  运行时 AddComponent 或全层级逐帧扫描。

## 6. 风险项

| 风险 | 等级 | 控制与现状 |
| --- | --- | --- |
| 线段、startPoint 或 Rigidbody2D 引用缺失 | 中 | prefab 显式绑定并保留局部回退解析；缺失时 Bear Logger 报错且不启动错误流程 |
| LineRenderer 与 EdgeCollider2D 重设中心后不一致 | 中 | 同时平移渲染点和碰撞点且不再缩放；仍需 PlayMode 观察碰撞范围 |
| Actor 专用动画污染共享 ActorCtrl | 高 | Player prefab 实例移除共享组件并添加 `DLevel7ActorCtrl : ActorCtrl`；未修改共享类 |
| 旧 Player ActorCtrl 引用悬空 | 高 | Trap、成功门和 Actor 内部 skin 绑定均切到 DLevel7ActorCtrl；静态确认旧 fileID 无引用 |
| 不同笔画长度质量导致同冲量轨迹不同 | 中 | 方向/冲量可在 Inspector 调整；共享质量按长度缩放仍生效，需覆盖短/中/长线验证 |
| forceDirection 配置为零向量 | 低 | Gizmos 不绘制方向箭头，运行时不施加方向冲量；Inspector 中文注释明确该边界 |
| Gizmos 误触发运行时初始化 | 中 | `OnDrawGizmos` 仅做本地字段读取和 Gizmos 绘制，不调用 ResolveReferences 或任何 manager/DB 链 |
| 重置时 Spine 回调访问已销毁线段 | 中 | 协程、TrackEntry 监听和流程版本均在 Reset/禁用时失效 |
| 气球重复碰撞导致重复结算 | 低 | DLevel7Ctrl 本地门禁叠加 DrawLevelCtrl 一次性 TryResolve 门禁 |
| 共享 PlayVfxListener 改变其它关卡表现 | 低 | ballon 已不再使用该共享组件；爆炸覆盖与清理由 DLevel7 局部 Handle 完成，其它关卡不迁移 |
| 粒子从屏幕外移动到气球 | 高 | 旧模板 local=(7.26,0.31) 被再次叠加到 Nail 世界坐标且随 0.3 秒缩放收拢；现使用模板明确世界坐标并挂稳定 level7 父节点，Nail 不再缩放 |
| 循环/异常粒子导致气球不消失 | 中 | 正常等待全部 IsAlive=false；`maxEffectDuration=2` 仅作误配保护上限，超时记录警告并清理 |
| 长卡帧使气球早于胜利事件销毁 | 高 | 粒子完成后还必须等待同一 EventHandleListener 回写 OnBalloonOutcomeStarted；承载 Executor 的 ballon 不再依赖剩余 item 的帧数余量 |
| LayerCollisionDetector 目标层误配 | 高 | Enemy 上的 detector 必须只检测 Draw 层；prefab 当前 `m_Bits=16384` 对应 Layer 14 `Draw` |
| LayerCollisionDetector UnityEvent 绑定丢失 | 高 | prefab `onEnter` 动态绑定 DLevel7Ctrl.OnDrawLineHitEnemy(Collision2D)；静态检查目标组件、方法名和 m_Mode |
| 气球仍按根线段 Tag 过滤 | 高 | 运行时子 BoxCollider2D 只继承 Draw Layer、不继承根 `tools` Tag；ballon 已改用 `LayerTriggerDetector(m_Bits=16384)`，并保留原 BaseItemExecutor.Execute 绑定 |
| Enemy 命中与 2 秒胜利窗口同帧竞态 | 高 | Enemy 死亡标记、DrawLevelCtrl 结果锁、尝试版本和 Resolved 回调共同门禁；仍需 PlayMode 压测边界帧 |
| Knock_Explosion_2 未显式绑定 | 低 | 当前脚本按名称递归解析；缺失时只降级粒子表现，不阻断 Die 与失败重置 |
| prefab 已有双层改动被覆盖 | 中 | 本次只追加组件、引用和气球 UnityEvent；未归并或撤销用户已有美术/机关改动 |

## 7. 验证证据

- 新需求实现后执行
  `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -p:WarningLevel=0`，
  聚焦构建通过：`0 warning / 0 error`。
- 运行中的 Unity Editor 已自动刷新并完成
  `Csc -> ILPostProcess -> CopyFiles Library/ScriptAssemblies/Assembly-CSharp.dll`；
  当前日志尾部未出现 `error CS`、`Compilation failed` 或指向 DLevel7 的编译错误。
- 静态确认 DLevel7Ctrl、DLevel7ActorCtrl、startPoint、DrawLineFeature、DrawLevelCtrl 和
  气球胜利 UnityEvent 均有 prefab 绑定。
- 静态确认 Player 原 `ActorCtrl` 已列入 nested prefab `m_RemovedComponents`，新增
  DLevel7ActorCtrl 已列入 `m_AddedComponents`，旧 stripped ActorCtrl fileID 无残留引用。
- 静态确认气球触发器为 `LayerTriggerDetector.targetLayerMask.m_Bits=16384`，原
  `BaseItemExecutor.Execute`、爆炸表现、胜利回调与延迟销毁顺序保持不变；prefab 已无
  `targetTag: tools` 气球过滤配置。
- 静态确认 ballon 执行器已无 ItemScaleListener、PlayVfxListener 和 DestroyListener；首项
  绑定 DLevel7BalloonExplosionHandle，effectParent/模板/balloonRoot、1.3 倍缩放与 0.1/2 秒
  保护参数均有序列化值，后续 SFX、线淡出和 OnBalloonExploded 顺序保持。
- 静态确认 DLevel_007 已有名为 `Enemy`、Tag 为 `enemy` 的 Actor prefab 实例，并已有
  `Knock_Explosion_2` 粒子实例；新字段缺少显式序列化值时由同关卡名称/Tag 回退解析。
- 静态确认 Enemy 上 `LayerCollisionDetector.targetLayerMask.m_Bits=16384` 对应 Draw
  Layer 14，`onEnter` 以动态 Collision2D 模式绑定
  `DLevel7Ctrl.OnDrawLineHitEnemy(Collision2D)`。
- 静态确认 DLevel7Ctrl 不再动态添加碰撞转发组件，旧
  `DLevel7StrokeCollisionRelay.cs` 及 meta 已删除。
- 静态确认 prefab 已把原 `throwAngleDegrees=25` 迁移为等价默认方向
  `forceDirection=(0.9063078, 0.4226183)`，运行时与 Gizmos 共用同一归一化入口。
- 静态确认 `OnDrawGizmos()` 未访问 DB、Save、RemoteConfig、Instance、Instantiate、
  ResolveReferences 或运行时事件链。
- 当前 DLevel_007 prefab YAML 对象锚点无重复 fileID。
- 目标 C#、prefab 与文档已通过 `git diff --check`。

## 8. 剩余缺口

- `uncertain`：尚未在 Unity PlayMode 观察任意形状线段直接定位后的视觉中心、原缩放保持、
  `throwing` 的实际释放帧和返回 idle 表现。
- 默认 `forceDirection=(0.9063078, 0.4226183)`（等价于原世界角度 25 度）和
  `throwForce=260` 仅是基于当前 startPoint 与气球位置的初始值，必须在实际关卡中调校。
- 尚未验证短/中/长画线质量差异、线段命中 Enemy、`Knock_Explosion_2` 接触点坐标、
  LayerCollisionDetector 动态参数传递、Enemy `die` 表现、0.5 秒失败淡出、线段命中
  气球、1.3 倍 Knock_Explosion 覆盖、播放同帧隐藏、粒子结束清理、2 秒胜利窗口边界、胜利面板、
  星级/进度保存、Reset、Replay 和快速退出。
- Android/iOS 横屏设备物理、触控和帧率未验证。

## 9. 回滚路径

1. 从 DLevel_007 根节点移除 DLevel7Ctrl，并删除气球 UnityEvent 的
   `OnBalloonExploded` 调用。
2. 将 Player nested prefab 的 DLevel7ActorCtrl 移除，恢复原 ActorCtrl 与 Trap/成功门/
   skin 绑定。
3. 删除 `DLevel7Ctrl.cs`、`DLevel7ActorCtrl.cs` 及其 meta，并清除 Enemy
   LayerCollisionDetector 的 `OnDrawLineHitEnemy` UnityEvent 绑定。
4. 删除 `DrawLineFeature.TryGetCompletedStrokeObject(...)`；其余关卡行为无需回滚。
5. 回滚 AutoPlay 补充时，移除 `PlayVfxListener.autoPlay` 字段及显式 Play 分支，并删除
   DLevel_007 ballon 的 `autoPlay: 1` 覆盖。
6. 保留用户任务前已有的 DLevel_007 prefab 美术、机关、startPoint 和气球爆炸配置。

## 10. 2026-07-30 子 BoxCollider2D 线段投掷兼容

- 背景：共享 DrawLineFeature 默认碰撞从根 EdgeCollider2D 改为按原始采样段生成的直接子
  BoxCollider2D 链，根节点仍是唯一 Rigidbody2D。DLevel_007 在接管完成线后会依据平滑
  LineRenderer 再次调整中心，若只移动视觉点会导致子碰撞盒与视觉错位。
- 修改：`DLevel7Ctrl.RecenterStrokePivot()` 保留旧 EdgeCollider2D 兼容，同时遍历根节点
  直接子节点；存在 Collider2D 时把 child.localPosition 减去相同 localCenter。子盒尺寸、
  旋转、根刚体质量、投掷方向、冲量和命中回调不变。
- 验证：定向 diff 检查通过；Assembly-CSharp 构建为
  `0 error / 101 个项目既有 warning`。
- 未验证风险：PlayMode 中视觉中心与 Box 链中心、复合刚体质心、投掷轨迹、碰撞接缝、
  Enemy/气球命中以及移动端物理开销。若模板将来在同一子节点放多个 Collider，本实现按
  子节点而非组件数量只平移一次，当前生成结构符合该边界。
- 回滚：共享碰撞恢复根 EdgeCollider2D 时，删除新增的直接子 Collider2D 平移循环即可；
  不需要修改 prefab、存档、事件或资源。

## 11. 2026-07-31 出界失败、飞行渐显与单笔限制

### 11.1 需求、修改前基线与影响范围

- 用户要求本关目标继续是“画线砸中气球且不砸到人”：若投掷线没有砸中气球便飞入新增
  FallArea，应立即失败；画线后不应先显示并停在 startPoint，而应在真正出手时边飞边
  渐显；整关只允许一笔。
- 修改前 `PlaceStrokeAtStartPoint()` 会在 `throwing` 开始前把可见线段放到 startPoint，
  而 Actor 到动画 45%（约 0.30 秒）才调用 `ThrowStroke()`，因此出现可见停留。
- 用户新增的 `FallArea (4)` 与 `FallArea (5)` 继承共享 `FailAreaTriggerHandle`，该脚本只
  接受 Player Tag，无法识别 Draw Layer 的运行时线段；两个实例此前都没有本关回调。
- 同日 DLevel_001～009 参数同步曾把本关 `allowMultipleStrokes` 写成 true；本次用户明确
  指定 DLevel_007 为单笔关卡，因此本关形成有记录的配置例外，其它关卡不随之改变。
- 影响文件仅为 `DLevel7Ctrl.cs`、`DLevel_007.prefab`、Level7 两份本地记录、DrawGame
  中央演进记录和 `DevelopMD/已有功能设计与使用记录.md`。未修改共享 FallArea、
  FailAreaTriggerHandle、LayerTriggerDetector、DrawLineFeature、ActorCtrl、DB、Luban、
  UI、资源地址、全局事件、analytics、广告、SDK 或平台分支。

### 11.2 修改后行为与事件流

- 第一笔完成后，DLevel7Ctrl 缓存该运行时 LineRenderer 的原始 `colorGradient`，将 alpha
  置 0 并禁用 Renderer；刚体保持 `simulated=false`，线段不会在原画线位置或 startPoint
  可见。
- 继续保留本关既有 `ReleaseNormalizedTime=0.45`。释放回调同一帧执行：设置
  `startPoint`、启用透明 Renderer、启用刚体、施加 Impulse 并唤醒。渐显协程先等待一次
  `WaitForFixedUpdate` 和一次后续帧，再用 `throwFadeDuration=0.2` 秒从透明恢复到原始 Gradient，保证
  第一次可见时已经经过物理移动；不修改共享材质或笔刷 Gradient。
- 控制器在 Actor 动画完成后继续持有本次投掷根对象与 Rigidbody2D 引用，直到 Reset、
  禁用或关卡重建，用于严格识别后续 FallArea 的子 BoxCollider2D；对象销毁权仍在
  DrawLineFeature。
- 两个 FallArea nested prefab 实例各自新增 `LayerTriggerDetector`，mask 为 Draw
  （Layer 14 / `m_Bits=16384`），`onEnter` 动态传入 Collider2D 到
  `OnThrownStrokeEnteredFallArea(...)`。回调要求线段已经释放、关卡尚未结算、Enemy
  尚未死亡、气球尚未爆炸，并要求 `other.attachedRigidbody` 等于当前投掷刚体；首次有效
  调用复用 `DrawLevelCtrl.TryFailAndReset(...)` 的结果锁与 Draw 专用重载链。
- ballon 的 LayerTriggerDetector 第一调用先把动态 Collider2D 传给
  `OnDrawLineHitBalloon(...)`，通过 `attachedRigidbody` 确认当前投掷物并立即设置
  `_hasHitBalloon`；第二调用才启动 BaseItemExecutor。此后同一线段进入 FallArea 会被
  忽略，即使低帧率下 `OnBalloonExploded()` 尚未执行也不会误判失败；
  `_hasHandledBalloonExplosion` 仍只由后续胜利回调设置并负责一次性胜利窗口。
- Prefab 将 `allowMultipleStrokes` 设为 false。该字段位于
  `DLevel_007 根节点 -> DrawLineFeature -> Allow Multiple Strokes`；代码定义在
  `DrawLineFeature.cs`，第一笔完成后与下一笔开始前各有一层门禁。

### 11.3 验证、风险、性能与回滚

- 运行中的 Unity 6000.0.59f2 已观察到脚本变化并完成
  `Csc -> ILPostProcess -> CopyFiles Assembly-CSharp.dll`，Tundra 报告 build success；
  最新相关日志未出现 `error CS` 或 Compilation failed。
- Unity PrefabImporter 已成功导入两次 `DLevel_007.prefab`。静态确认两个 detector 的
  `m_AddedComponents`、Draw mask、动态 Collider2D 方法、DLevel7Ctrl 目标 fileID、
  `throwFadeDuration=0.2` 与 `allowMultipleStrokes=0`；171 个 YAML 锚点无重复。
- 本机 `dotnet` 只有运行时、没有 SDK，因此独立 `dotnet build` 无法启动；以上 Unity Bee
  编译与 importer 结果是本次可用的编译/序列化证据。
- 渐显只持续 0.2 秒，按帧创建少量本地 Gradient；不新增 Update、全局监听、运行时
  AddComponent 或跨关扫描。两个 FallArea 可能由一笔的多个子碰撞盒重复进入，但首次
  TryFailAndReset 后结果门禁会同步阻止后续回调。
- `uncertain`：尚未执行整关 PlayMode，仍需目测释放帧不闪现、不同长度/形状线段的渐显、
  左右 FallArea 实际覆盖、砸中气球后越界不失败、未砸中直接失败、砸中 Enemy 优先失败、
  Reset/Replay、暂停/退出及 Android/iOS 横屏物理表现。用户只在当前 prefab 放置左右
  两块 FallArea；若未来还需要顶部/底部出界失败，应先补对应区域，不扩大本次检测范围。
- 回滚时删除 DLevel7Ctrl 的 FallArea API、Renderer/Gradient 渐显状态与协程，恢复旧的
  `PlaceStrokeAtStartPoint()` 和动画完成清理；从两个 FallArea 实例移除新增
  LayerTriggerDetector，并将 `allowMultipleStrokes` 恢复为回滚目标值。共享脚本、资源、
  存档和其它 DLevel 无需迁移；用户新增的 FallArea Transform/Collider 布局应保留。

## 12. 2026-07-31 气球触发与 Enemy 接触点修复

### 12.1 反馈、基线与影响范围

- 用户实测反馈“砸到气球没爆炸、砸到人的特效爆点不对”。
- 气球修改前使用 `OnTrigger2DHandle(targetTag=tools)`；当前 DrawLineFeature 完成线的实际
  碰撞体是运行时直接子节点上的 BoxCollider2D，生成时只继承根对象的 Draw Layer，未继承
  根对象的 `tools` Tag，因此气球收到子 Collider 后会被 Tag 门禁拒绝，既有执行器完全不运行。
- Enemy 的 LayerCollisionDetector 已正确传入完整 `Collision2D`，但
  `OnDrawLineHitEnemy` 进入 `HandleEnemyHit` 时丢弃了该参数，粒子随后被直接放到
  `enemyActor.transform.position`，因此爆点落在角色原点而非石头/线段与人物的接触处。
- 本次只修改 `DLevel7Ctrl.cs`、`DLevel_007.prefab` 与已有 Level7/DrawGame 记录；未修改
  DrawLineFeature、LayerTriggerDetector、LayerCollisionDetector、ActorCtrl、粒子资源、
  BaseItemExecutor、DB/Luban、UI、全局事件、analytics、SDK 或其它关卡。

### 12.2 修改后事件流

- ballon 原触发组件在相同 component fileID 上改为
  `LayerTriggerDetector(targetLayerMask=Draw / m_Bits=16384)`；原
  `onEnter -> BaseItemExecutor.Execute` 绑定以及缩放、VFX、SFX、线条淡出、
  `OnBalloonExploded` 和 0.6 秒销毁顺序全部保留。没有并存第二个旧 Tag 触发器，避免双触发。
- `OnDrawLineHitEnemy(Collision2D)` 继续把碰撞数据传给命中处理。粒子 XY 优先使用首个
  `ContactPoint2D.point`；无 contact 时，使用 Enemy 侧 Collider 对投掷线段 Collider 中心的
  `ClosestPoint`；Collider 缺失时才回退到 Enemy Transform。写入时只替换 X/Y，保留粒子
  预制体原 Z，并在激活粒子对象前完成定位，避免 Play On Awake 在旧位置先生成粒子。
- Enemy 死亡、失败重载、气球 2 秒胜利窗口、FallArea、单笔限制和飞行渐显均不改变。

### 12.3 验证、风险与回滚

- Unity 6000.0.59f2 已重新生成 `Assembly-CSharp.dll`，Tundra build success；DLevel_007
  PrefabImporter 成功。静态确认 ballon detector 为 Draw mask、执行器目标与方法名不变，
  prefab 共 171 个 YAML 对象锚点且无重复。
- 目标 C# 与四份记录的 `git diff --check` 通过。prefab 单文件检查仍报告此前两个
  FallArea added component 块中的 4 处 Unity 空字段尾随空格；本次 ballon 修改块未新增
  格式报错，引用和导入均有效。当前仍需 PlayMode 目测不同线型的气球触发、人物不同
  部位接触点、无 contact 回退、Reset/Replay 与竞态边界。
- 当前 ballon 附近没有其它静态 Draw Layer Collider。若未来把其它 Draw 物体移动进气球
  Trigger，它也会进入同一执行器；该风险应通过关卡布局或届时增加“当前投掷刚体”门禁处理，
  本次不扩大共享组件职责。
- 回滚时把 ballon 组件脚本恢复为 `OnTrigger2DHandle`、字段恢复
  `targetTag: tools`，并把 `PlayEnemyHitEffect` 恢复为 Enemy Transform 定位；其它投掷、
  胜负、FallArea、资源和存档无需回滚。

## 13. 2026-07-31 气球原尺寸爆炸覆盖与屏外出生修复

> 本节记录 2026-07-31 的历史行为；气球保留到粒子结束的显示时点已被第 14 节替代。

### 13.1 需求、修改前基线与影响范围

- 用户要求 ballon 爆炸时不要缩小，应由爆炸特效盖住原尺寸气球；特效结束后气球已经
  消失。同时反馈当前特效从屏幕外飞入。
- 修改前执行器首项把 Nail 在 0.3 秒内从 1 缩到 0，且不等待完成便运行 PlayVfxListener。
  PlayVfxListener 使用 `Instantiate(particle, target)`，Unity 两参父节点重载保留模板局部
  变换。模板在 level7 下 local=(7.26,0.31,0)，却被克隆到世界约
  (7.28,-0.004,0) 的 Nail 下，因此出生世界 X 约 14.54；Nail 同时缩小，又把这段子节点
  偏移收拢到气球，形成“从右侧屏外飞进来”的表现。
- Knock_Explosion 根粒子为 World Simulation、非循环、Duration=0.2、Lifetime=0.4，
  理论完整尾长约 0.6 秒；粒子 order=210，高于 Nail order=105，本身具备覆盖关系。
- 本次新增 `DLevel7BalloonExplosionHandle.cs` 并修改 DLevel_007 prefab 与已有记录；未改
  共享 PlayVfxListener、BaseItemExecutor、粒子资源、DrawLineFeature、胜负控制器、
  DB/Luban、UI、全局事件、analytics、SDK 或其它关卡。

### 13.2 修改后行为与事件流

- 删除 ballon 的 ItemScaleListener 与固定延迟 DestroyListener；Nail 在整个爆炸过程中
  保持原 localScale，不再通过缩小隐藏。
- 原 PlayVfxListener component fileID 原位换成本关局部 Handle。命中后先关闭 ballon 根与
  子节点全部 Collider，避免多个线段子碰撞盒再次触发；随后使用模板明确的世界位置、
  旋转和稳定 `level7` 父节点创建特效，不再把 local=(7.26,0.31) 叠加到 Nail 坐标，也不
  继承 Nail 的悬浮/缩放。
- 新实例显式 StopAndClear 后 Play 全部 ParticleSystem。视觉协程等待一次初始化帧，再以
  `IsAlive(true)` 判断真实结束；结束后销毁特效实例和 ballon 根。若未来误配循环粒子，
  `maxEffectDuration=2` 保护性超时清理并记录警告。
- Handle 在 0.1 秒后把 BaseItemExecutor 标为完成，使原 ballonExplosion SFX、线淡出和
  OnBalloonExploded 继续按旧节奏执行；同一 UnityEvent 随后调用
  `OnBalloonOutcomeStarted`。粒子约 0.6 秒的视觉等待不会推迟 2 秒胜利窗口，且无论长
  卡帧或空粒子，销毁都必须等该胜利事件真实运行后才允许发生。
- ballon Trigger 的第一回调动态传入 Collider2D 到 `OnDrawLineHitBalloon(...)`，先校验并
  设置 `_hasHitBalloon`，第二回调才启动 BaseItemExecutor。FallArea 只读取这个物理接触
  门禁；`_hasHandledBalloonExplosion` 继续由稍后的胜利事件管理，避免低帧率下一边已经
  命中气球、一边因事件链尚未走完而被 FallArea 误判失败。

### 13.3 验证、风险与回滚

- Unity 6000.0.59f2 已完成新脚本的 Assembly-CSharp Bee/Tundra 编译与 DLevel_007
  PrefabImporter。静态确认执行器 4 项顺序为局部爆炸 Handle、SFX、线淡出、胜利回调；
  ballon Trigger 顺序为立即命中门禁、BaseItemExecutor；胜利 UnityEvent 同时调用
  DLevel7Ctrl 与视觉完成门禁，旧 Scale/PlayVfx/Destroy 引用均为 0，169 个 YAML 对象
  锚点无重复。
- 静态坐标确认运行时实例使用模板世界爆点 (7.26,0.31,0)，不再得到旧错误 X≈14.54；
  粒子 Sorting Order 210 高于气球 105。目标 C# 与四份记录的 `git diff --check` 通过；
  prefab 单文件仍只有此前 FallArea added component 块的 4 处 Unity 空字段尾随空格，
  本次 Handle 与胜利门禁块未新增格式报错。
- `uncertain`：尚未执行整关 PlayMode，仍需目测特效覆盖面积、首帧是否原地、气球在粒子
  最后一帧后不回显、Collider 关闭、重复命中、Reset/Replay、Enemy 竞态和移动端帧率。
- 回滚时移除 DLevel7BalloonExplosionHandle 及 meta，把 component fileID 恢复为
  PlayVfxListener，并恢复 ItemScaleListener、DestroyListener 及原 6 项执行顺序；共享脚本、
  粒子资源、存档和其它 DLevel 不迁移。

## 14. 2026-08-02 爆炸放大与播放同帧隐藏气球

### 14.1 需求、修改前基线与影响范围

- 用户要求气球爆炸特效“更大一点”，并在特效播放的同时删除气球。
- 修改前局部 Handle 按模板 1 倍缩放播放特效，Nail 与 Line 视觉一直保留到粒子
  `IsAlive(true)==false` 后才随 ballon 根销毁，因此不符合新的同步消失时点。
- ballon 根同时承载 BaseItemExecutor 与爆炸 Handle；若命中帧直接 `Destroy` 或
  `SetActive(false)` 根节点，会触发 `OnDisable -> CancelExplosion`，并中断后续 SFX、线淡出、
  `OnBalloonExploded` 与胜利门禁。故本次把“玩家可见的删除”和“执行器根实际销毁”分离。
- 影响 `DLevel7BalloonExplosionHandle.cs`、`DLevel_007.prefab` 及既有 Level7/中央记录；不改
  共享 `Knock_Explosion` 资源、PlayVfxListener、BaseItemExecutor、胜负、DB/Luban、UI、
  analytics、SDK、平台分支或其它关卡。

### 14.2 修改后行为与事件流

- 新增序列化 `effectScaleMultiplier`，DLevel_007 配置为 `1.3`。克隆仍使用模板明确的世界
  爆点与稳定 `level7` 父节点，并在显式 Play 前把运行时实例 localScale 设置为
  `particleTemplate.localScale * 1.3`；只放大本次克隆，不修改场景模板或共享粒子资源。
- `SpawnAndPlayEffect()` 返回后，同一次 `Execute()` 立即调用 `HideBalloonVisuals()`：关闭
  ballon 根下全部 Renderer，并把全部 CanvasRenderer alpha 置 0。特效实例是 level7 下的
  兄弟节点，不在 ballonRoot 子树内，因此不会被一并隐藏；首个渲染帧应表现为“特效出现、
  气球已消失”。Collider 仍在此前先关闭，重复命中门禁不变。
- 不可见 ballon 根继续运行原执行器；0.1 秒后照常播放 SFX、线淡出并启动 2 秒胜利窗口。
  粒子和 `OnBalloonOutcomeStarted` 均完成后，才真正销毁 effectInstance 与 ballonRoot。

### 14.3 验证、风险与回滚

- Unity 6000.0.59f2 已完成 Assembly-CSharp Bee/Tundra 编译，日志无 `error CS`、
  `Compilation failed` 或 `Scripts have compiler errors`；DLevel_007 PrefabImporter 成功。
- 静态确认模板、level7 父节点均为 scale=(1,1,1)，粒子为 World Simulation / Local Scaling；
  prefab 中倍率字段唯一且为 1.3，169 个 YAML 锚点无重复，内部引用无缺失。
- `uncertain`：尚未执行整关 PlayMode，仍需目测 1.3 倍覆盖范围、命中首帧是否无气球残影、
  粒子尾帧、SFX/线淡出/胜利回调、Enemy 抢占、Reset/Replay 与 Android/iOS 横屏表现。
- 若胜利 UnityEvent 配置被破坏、未调用 `OnBalloonOutcomeStarted`，不可见根与已结束特效会
  等待门禁；当前 prefab 静态绑定正确。本风险沿用既有安全门禁，不由本次放大/隐藏新增。
- 回滚时把 `effectScaleMultiplier` 恢复为 1，并移除 `HideBalloonVisuals()` 调用/方法，即恢复
  “模板原尺寸、粒子结束后才移除气球视觉”的上一版行为；共享资源与其它关卡无需迁移。

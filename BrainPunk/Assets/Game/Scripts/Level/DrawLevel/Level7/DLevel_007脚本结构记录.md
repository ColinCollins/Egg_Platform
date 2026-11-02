# DLevel_007 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 第 7 关
- 记录日期：2026-07-29
- 交付/验收状态：单笔画线、飞行渐显投掷、Enemy/FallArea 失败与延迟气球胜利链已实现；
  Unity 编译和 prefab 导入通过，PlayMode 与设备验收未完成
- 参考基线关卡：DLevel_005（Actor 专用动画回调）、DLevel_004/006（首笔局部 Handler）
- 选择基线理由：DLevel5 已把单关动画名、释放时点和回调隔离到 Actor 子类；DLevel4/6
  已使用 `IDrawStrokeCompletedHandler` 在首笔后驱动局部机关
- 基线证据限制：参考关卡均有静态/编译证据，但整关设备验收状态为 `uncertain`
- 主要脚本：`DLevel7Ctrl.cs`、`DLevel7ActorCtrl.cs`
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_007.prefab`

## 2. 脚本与组件树

```text
DLevel_007
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DelayAutoBeginDrawHandle
├─ DLevel7Ctrl : IDrawStrokeCompletedHandler
├─ 运行时线段（不动态添加 Level7 碰撞组件）
├─ FallArea (4)
│  └─ LayerTriggerDetector(targetLayerMask=Draw) -> OnThrownStrokeEnteredFallArea
├─ FallArea (5)
│  └─ LayerTriggerDetector(targetLayerMask=Draw) -> OnThrownStrokeEnteredFallArea
├─ level7
│  ├─ Actor
│  │  └─ DLevel7ActorCtrl : ActorCtrl
│  ├─ Enemy
│  │  ├─ ActorCtrl
│  │  └─ LayerCollisionDetector(targetLayerMask=Draw)
│  ├─ startPoint
│  └─ ballon
│     ├─ LayerTriggerDetector(targetLayerMask=Draw)
│     ├─ BaseItemExecutor
│     ├─ DLevel7BalloonExplosionHandle -> level7/Knock_Explosion 模板
│     └─ EventHandleListener -> OnBalloonExploded
└─ DrawGame_DrawLine 模板实例
   └─ LineRenderer + Rigidbody2D + 运行时子 BoxCollider2D（继承 Draw Layer）
```

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 创建与销毁方 | 不负责事项 |
| --- | --- | --- | --- | --- | --- |
| DrawLevelCtrl | 共享主控制器 | 初始化、局部 Handler 分发、一次性结算 | `IDrawLevelRuntime` | DrawPlayCtrl | 不处理 Level7 定位/投掷 |
| DrawLineFeature | 共享功能 | 画线、碰撞体、线段所有权；提供只读完成线段访问 | MonoBehaviour | DLevel prefab | 不播放 Actor 动画或结算 |
| DLevel7Ctrl | 关卡局部控制器 | 单笔接管、隐藏/飞行渐显、投掷参数、Enemy/FallArea 失败、2 秒气球胜利窗口、清理 | `IDrawStrokeCompletedHandler`、`IDebuger` | DLevel prefab | 不直接操作 UI/存档，不保存动画名 |
| DLevel7ActorCtrl | 关卡 Actor 子类 | `throwing` 释放时点、完成回调、恢复 `idle` | `ActorCtrl` | Player nested prefab 实例 | 不持有线段、气球或结算 |
| DLevel7BalloonExplosionHandle | 关卡局部执行项 | 关闭气球 Collider、播放 1.3 倍世界爆点粒子、同帧隐藏气球视觉、0.1 秒放行执行器、粒子与胜利事件均完成后清理根/实例 | `BaseItemEventHandle`、`IDebuger` | ballon prefab 局部组件 | 不提交胜负、不改共享 VFX 组件或资源 |
| LayerCollisionDetector | 既有共享碰撞组件 | 在 Enemy 上过滤 Draw 层物理碰撞并派发 Collision2D UnityEvent | MonoBehaviour | Enemy prefab 实例 | 不处理死亡、粒子或关卡结算 |
| LayerTriggerDetector | 既有共享触发组件 | 在 ballon 与两个 FallArea 实例过滤 Draw 层并派发 Collider2D UnityEvent | MonoBehaviour | DLevel prefab / FallArea nested prefab 实例 | 不识别当前投掷所有权，不直接结算 |
| 气球执行器 | prefab 局部组件链 | 接受 Draw 子碰撞盒、启动局部爆炸视觉、SFX/线淡出、调用胜利 API | 既有 MainLevel executor + Level7 Handle | DLevel prefab | 不直接操作 DrawPlayCtrl |

## 4. 生命周期与关键调用

1. DLevel_007 创建时 Player 使用 DLevel7ActorCtrl，DrawLevelCtrl 缓存 DLevel7Ctrl。
2. 既有 DelayAutoBeginDrawHandle 开放画线；Level7 不新增可见 Intro。
3. 第一笔完成后 DrawLineFeature 建好物理线段并派发 `DrawStrokeResult`。
4. DLevel7Ctrl 取得该线段，暂停物理、重设中心、缓存原 Gradient 并隐藏 Renderer；
   不提前显示在 startPoint、不播放平移 Tween、不动态添加碰撞组件，缩放保持不变。
5. DLevel7ActorCtrl 播放 `throwing`；45% 时 DLevel7Ctrl 同帧设置 startPoint、启用透明
   Renderer、恢复物理并按归一化 `forceDirection` 与冲量投出线段。
6. 渐显协程等待一次物理步进及随后渲染更新，再用 0.2 秒恢复原 Gradient；动画结束后
   Actor 回到 `idle`，控制器继续持有投掷根对象引用以识别出界触发。
7. 线段命中 Enemy 时，Enemy 上的 LayerCollisionDetector 过滤 Draw 层并动态传入
   Collision2D，调用 `OnDrawLineHitEnemy`；控制器把 `Knock_Explosion_2` 放到首个物理
   接触点并保留粒子原 Z，无 contact 时回退 Collider 最近点，再调用
   Enemy `Die()`，再通过 `TryFailAndReset` 进入 Draw 失败整关重载。
8. 运行时线段子 BoxCollider2D 以 Draw Layer 触发气球；第一回调先校验当前 Rigidbody2D
   并设置 `_hasHitBalloon`，第二回调再启动执行器。局部爆炸 Handle 关闭 Collider，在模板
   世界爆点播放 1.3 倍特效，同帧隐藏 ballon 下全部 Renderer/CanvasRenderer，但保留不可见
   Executor 根；0.1 秒后放行执行器调用 `OnBalloonExploded()`，同一 UnityEvent 回写
   `OnBalloonOutcomeStarted`。粒子与该回调门禁均完成后销毁根与实例。2 秒窗口内 Enemy
   死亡则失败，窗口结束仍存活才进入 Draw 胜利链。
9. 线段未触发气球便进入左右 FallArea 时，LayerTriggerDetector 上报子 Collider；
   DLevel7Ctrl 通过 attachedRigidbody 校验当前投掷物后调用 TryFailAndReset。气球物理
   接触当帧已设置 `_hasHitBalloon`，即使爆炸/胜利回调尚未运行也会忽略随后出界。
10. Reset/结算时取消胜利/渐显协程并保持当前透明状态，避免帧末销毁前闪现；Disable
    在层级隐藏后恢复 Renderer 并取消 Spine TrackEntry，DrawLineFeature 销毁线段。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或资源 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| DLevel_007 根 | DLevel7Ctrl | 本关控制器 | 必填 | 不接收首笔/气球胜利 |
| DLevel7Ctrl | drawLineFeature | 根 DrawLineFeature | 必填 | 报错，不启动投掷 |
| DLevel7Ctrl | drawLevelCtrl | 根 DrawLevelCtrl | 必填 | 气球爆炸无法结算 |
| DLevel7Ctrl | actorCtrl | Player/DLevel7ActorCtrl | 推荐必填 | 日志警告，线段直接投出 |
| DLevel7Ctrl | startPoint | level7/startPoint | 必填 | 报错，不启动投掷 |
| DLevel7Ctrl | forceDirection/throwForce | prefab 当前 (0.9063078, 0.45) / 260 冲量 | 可调 | 方向运行时归一化；零向量不施加方向冲量 |
| DLevel7Ctrl | throwFadeDuration | 0.2 秒 | 可调 | 0 时投掷同帧恢复原透明度；不影响物理 |
| DLevel7Ctrl | enemyActor | level7 内 Enemy/ActorCtrl | 推荐绑定 | 按 Enemy 名称与 enemy Tag 从同关卡 Actor 中回退解析 |
| DLevel7Ctrl | enemyHitEffect | Knock_Explosion_2/ParticleSystem | 推荐绑定 | 按节点名递归解析；缺失只降级粒子表现 |
| Enemy | LayerCollisionDetector targetLayerMask | Draw（Layer 14 / m_Bits 16384） | 必填 | 不会上报线段命中 |
| Enemy LayerCollisionDetector | onEnter | DLevel7Ctrl.OnDrawLineHitEnemy(Collision2D) | 必填 | Enemy 不进入失败链 |
| Player | DLevel7ActorCtrl | Actor SpineCtrl、pushCollider | 必填 | throwing 无法启动 |
| ballon | EventHandleListener | DLevel7Ctrl.OnBalloonExploded | 必填 | 有爆炸表现但不胜利 |
| ballon 胜利 EventHandleListener | 第二调用 | DLevel7BalloonExplosionHandle.OnBalloonOutcomeStarted | 本关必填 | 长卡帧/空粒子时视觉清理会等待，不会提前销毁 Executor |
| ballon | DLevel7BalloonExplosionHandle | effectParent=level7、particleTemplate=Knock_Explosion、effectScaleMultiplier=1.3、balloonRoot=ballon | 本关必填 | 无法放大、原地播放并在同帧隐藏气球视觉 |
| DLevel7BalloonExplosionHandle | executorReleaseDelay / maxEffectDuration | 0.1 / 2 秒 | 本关必填 | 前者改变胜利回调节奏；后者仅是循环粒子保护上限 |
| ballon | LayerTriggerDetector mask / onEnter | Draw（16384）/ 先 OnDrawLineHitBalloon(Collider2D)，再 BaseItemExecutor.Execute | 本关必填 | 未先记录物理命中时，低帧率下可能在胜利事件前被 FallArea 误判失败 |
| 运行时线段子碰撞盒 | Layer | Draw（Layer 14） | 必填 | ballon、Enemy 与 FallArea 的 Layer detector 均不接受 |
| DrawLineFeature | allowMultipleStrokes | false | 本关必填 | true 时允许继续开始第二笔 |
| FallArea (4)/(5) | LayerTriggerDetector mask / onEnter | Draw（16384）/ OnThrownStrokeEnteredFallArea(Collider2D) | 本关必填 | 投掷线飞出时不会失败 |

## 6. 数据、UI、资源与事件流

- DB/存档：本关无直接访问；结算复用 DrawPlayCtrl。
- Luban/配置：无。
- UI：无直接操作。
- 局部引用：DLevel7Ctrl 持有当前关卡 Draw、Player、Enemy、粒子和 startPoint 引用；
  Actor 子类只持有继承的 SpineCtrl。
- 事件：无新全局事件；局部 Handler、Action 回调、Enemy LayerCollisionDetector，以及
  ballon/FallArea LayerTriggerDetector prefab UnityEvent；失败复用已有
  `DrawLevelCtrl.TryFailAndReset`。
- 资源加载：随 DLevel_007 与 Actor prefab 加载，无新增地址或实例化资源。
- 粒子：ballon 不再使用共享 PlayVfxListener；Level7 局部 Handle 按模板世界坐标创建，
  对新实例全部 ParticleSystem StopAndClear 后 Play，并以 IsAlive 判定真实结束。
- Analytics/SDK：无。
- 编辑器预览：`OnDrawGizmos()` 从 startPoint 绘制青色方向箭头，只读取本关序列化
  数据和 Transform，不调用运行时引用解析、DB、存档、配置或 manager。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| DLevel5 的箭模板生成改为接管本笔运行时线段 | 本关投掷物就是玩家画线 | 任意画线形状成为物理投掷物 | 中心、原缩放、碰撞、轨迹 | 是，完成线段只读访问边界 |
| 新增几何/碰撞中心重设，并在释放帧定位 startPoint | 线段要从 Actor 出手点开始物理飞行 | 动画等待期隐藏，无平移过程，尺寸保持不变 | 任意方向和长度线段、释放帧渐显 | 否，本关必要差异 |
| 投掷角度改为二维方向并增加 Gizmos 箭头 | 关卡编辑需要直接控制和观察世界方向 | Inspector 向量直接决定投掷方向，Scene 可视化与运行时一致 | 非单位向量、零向量、四象限 | 否，本关编辑体验 |
| Enemy 复用 LayerCollisionDetector 并调用本关公开 Collision2D API | 用户已在 prefab 用 Draw Layer 检测线段 | Enemy 命中进入局部失败链，不再运行时添加重复 detector | 动态参数、重复命中、重置 | 延续项目既有组件模式 |
| 气球胜利增加 2 秒可抢占窗口 | Enemy 死亡优先于气球成功 | 爆炸后延迟结算，窗口内死亡失败 | 边界帧、并发结算、快速退出 | 否，本关必要差异 |
| ballon 改用 Level7 局部爆炸 Handle | 父节点 Instantiate 会复制模板 local 偏移且根节点不能命中帧销毁 | 特效在作者世界爆点以 1.3 倍播放；气球视觉同帧消失，不可见执行器根稍后安全销毁 | 首帧坐标、放大覆盖、回调、重置 | 否，本关必要差异 |
| Player ActorCtrl 替换为关卡子类 | throwing 是单关动作 | 通用移动更新不接管投掷动画 | 进入、Reset、成功门 | 延续 DLevel5 已验证模式 |

## 8. 可复用结论与优化候选

- 继续沿用：单关 Actor 动画留在 `LevelXXActorCtrl : ActorCtrl`，主关卡控制器只持有局部 API。
- 局部验证：`StrokeCompleted` 同步回调后可通过 DrawLineFeature 只读 API取得该笔对象，
  同时保持对象销毁权在 DrawLineFeature。
- 暂不共享：线段重设中心、startPoint、throwing 名称、45% 时点、Enemy 命中和
  2 秒气球胜利窗口。
- 观察候选：若后续多关都需要“接管已完成线段”，再评估不可变句柄或更完整契约；当前不
  扩展 DrawStrokeResult、不迁移既有关卡。

## 9. 验证证据与缺口

- 静态检查：组件/引用/Layer/UnityEvent 存在，ballon LayerTriggerDetector 与 Enemy
  LayerCollisionDetector 均按 Draw 层过滤；Enemy 动态绑定公开 Collision2D API，Player
  原 ActorCtrl 已替换，YAML fileID 无重复。
- 编译：新需求实现后 Assembly-CSharp 聚焦构建通过，`0 warning / 0 error`。
- Unity PlayMode：未执行。
- Android/iOS：未执行。
- 未验证项：直接定位后的视觉中心、原缩放保持、动画释放帧、短中长线轨迹、
  LayerCollisionDetector 动态参数、Enemy 命中粒子/Die、失败重载、气球命中、
  ballon 1.3 倍特效原地播放与同帧隐藏、2 秒竞态窗口与完整胜利/重置流程。

## 10. 风险与回滚

- 主要风险：质量按线长变化导致轨迹差异；默认方向/冲量仍需调校；Enemy detector 的
  Draw Layer 与动态 UnityEvent 绑定不可丢失；2 秒边界竞态、nested prefab Actor 替换和任意线形中心需要
  PlayMode 验证。
- 回滚路径：移除 DLevel7Ctrl 与气球调用、清除 Enemy detector 的本关 UnityEvent、
  恢复 Player 原 ActorCtrl、删除 Actor 子类和 DrawLineFeature 只读访问 API；用户已有
  Level7 美术/机关改动保留。

## 11. 2026-07-30 子 BoxCollider2D 投掷兼容复盘

- 共享 `DrawLineFeature` 默认完成线已由根 EdgeCollider2D 改为直接子 BoxCollider2D 链，
  根节点继续持有唯一 Rigidbody2D；DLevel_007 仍只接管根线段对象和根刚体，不持有单个
  子盒引用。
- `RecenterStrokePivot()` 依据完成后视觉点再次调整根中心时，现同步平移所有直接子
  Collider2D 的 localPosition；不改变子盒旋转、尺寸或根刚体质量。
- 当前必要差异：DLevel_007 是现有代码中唯一会在 `StrokeCompleted` 后再次改写线段局部
  中心的关卡，因此兼容保留在本关控制器，不上提新的共享 API。
- 静态检查与 C# 编译通过（`0 error / 101 个项目既有 warning`）。PlayMode 中短/长线
  投掷、命中 Enemy/气球、Reset 清理以及 Android/iOS 复合刚体性能仍待验证。
- 回滚：共享碰撞恢复根 EdgeCollider2D 后，可删除对子 Collider2D 的 localPosition
  平移循环；其它 DLevel_007 投掷结构不变。

## 12. 2026-07-31 单笔、飞行渐显与 FallArea 出界失败

### 12.1 结构增量

```text
第一笔完成
  -> DLevel7Ctrl 缓存运行时线段/刚体/LineRenderer/原 Gradient
  -> simulated=false + Renderer.enabled=false
  -> DLevel7ActorCtrl.throwing
     -> 45% release
        -> transform.position = startPoint
        -> Renderer.enabled=true（alpha=0）
        -> simulated=true + AddForce(Impulse)
        -> WaitForFixedUpdate + 下一帧
        -> 0.2 秒恢复原 Gradient alpha

任意运行时子 BoxCollider 进入 FallArea (4)/(5)
  -> LayerTriggerDetector(mask=Draw)
  -> OnThrownStrokeEnteredFallArea(Collider2D)
  -> other.attachedRigidbody == 当前投掷 Rigidbody2D
     ├─ 气球未爆炸：TryFailAndReset
     └─ 气球已爆炸：忽略，保留 2 秒胜利窗口
```

- 投掷对象与 Renderer 引用不再在 throwing 完成回调立即清空；必须保留到 Attempt Reset、
  Disable 或整关重建，才能把 FallArea 传入的多个子 BoxCollider 归属到同一根刚体。
- Renderer 渐显仅操作运行时实例 Gradient。Reset/结算会停止协程但保留当前视觉状态到
  帧末销毁；Disable 在层级已隐藏时恢复原 Gradient 与 enabled。DrawLineFeature 继续
  拥有对象销毁责任。
- FallArea 使用 Trigger，因此复用 `LayerTriggerDetector`；Enemy 使用物理 Collision，继续
  复用 `LayerCollisionDetector`。两种 detector 均只负责过滤和 UnityEvent，不持有胜负。
- 单笔是 prefab 配置，不新增 Level7 专用画线代码：
  `DLevel_007/DrawLineFeature.allowMultipleStrokes=false`。Inspector 路径为
  `DLevel_007 根节点 -> DrawLineFeature -> Allow Multiple Strokes`，取消勾选。

### 12.2 验证、风险与回滚

- Unity 6000.0.59f2 Bee/Tundra 编译成功，Assembly-CSharp 完成 ILPostProcess 与 CopyFiles；
  PrefabImporter 成功导入 DLevel_007。两个 FallArea 的 added component、Draw mask、动态
  Collider2D 回调与本关控制器目标均静态确认，171 个 YAML 锚点唯一。
- 本机无 .NET SDK，无法补独立 dotnet build；Unity 编译日志未出现本次 C# 错误。
- `uncertain`：尚未 PlayMode 验证动画释放帧、0.2 秒渐显手感、左右出界、气球先命中、
  Enemy 并发命中、Reset/Replay、暂停/退出及 Android/iOS 横屏表现。
- 回滚时恢复可见 startPoint 预定位与 throwing 完成清理，删除 Gradient/Renderer 协程状态
  和 FallArea 公开 API，从两个 nested FallArea 移除 LayerTriggerDetector，并恢复目标
  `allowMultipleStrokes` 值；共享组件、用户新增的 FallArea 布局、存档和资源均不迁移。

## 13. 2026-07-31 气球 Layer 触发与 Enemy 接触点定位

```text
运行时子 BoxCollider2D（Draw Layer）进入 ballon Trigger
  -> LayerTriggerDetector(mask=Draw)
  -> BaseItemExecutor.Execute
  -> 既有 VFX/SFX/淡出/OnBalloonExploded/延迟销毁

运行时线段与 Enemy Collision
  -> LayerCollisionDetector(mask=Draw)
  -> OnDrawLineHitEnemy(Collision2D)
  -> contact[0].point
     └─ 无 contact：Enemy Collider.ClosestPoint(线段 Collider 中心)
  -> 仅更新 Knock_Explosion_2 的 X/Y，保留原 Z
  -> Die + TryFailAndReset
```

- ballon 复用原 component fileID，把 `OnTrigger2DHandle(targetTag=tools)` 原位替换为
  `LayerTriggerDetector(targetLayerMask=Draw)`；onEnter 的 BaseItemExecutor 目标、方法、参数
  模式和执行 items 均保持不变，没有新增并行触发器。
- 局部修复依据是当前运行时 BoxCollider2D 子节点只复制根线段 Layer、不复制 Tag；不修改
  共享 DrawLineFeature 的生成策略，也不要求其它关卡迁移 Tag 配置。
- Enemy 命中 API 的 prefab 动态 Collision2D 绑定不变，只在 DLevel7Ctrl 内继续传递并消费
  已有碰撞数据；无新组件、序列化字段、资源加载或全局事件。
- Unity Bee/Tundra 编译和 DLevel_007 PrefabImporter 已通过；171 个 YAML 对象锚点唯一。
  整关 PlayMode、不同身体部位爆点、气球命中、Reset/Replay 和移动端仍待验证。
- 回滚仅需恢复 ballon 的旧脚本 GUID/`targetTag: tools`，并恢复粒子按 Enemy Transform
  定位；其余单笔、渐显、FallArea 和胜负流程不受影响。

## 14. 2026-07-31 ballon 爆炸覆盖与真实粒子结束清理

> 本节保留历史结构；气球视觉等待粒子结束的时点已由第 15 节替代。

```text
Draw 子 BoxCollider 进入 ballon Trigger
  -> OnDrawLineHitBalloon(Collider2D)
     -> 校验 attachedRigidbody == 当前投掷 Rigidbody2D
     -> _hasHitBalloon=true（FallArea 立即停止判失败）
  -> BaseItemExecutor
  -> DLevel7BalloonExplosionHandle.Execute
     -> Disable ballon 全部 Collider
     -> Instantiate(template.worldPosition, template.worldRotation, level7)
     -> StopAndClear + Play(false)
     ├─ 0.1s：IsDone=true，继续 SFX / 线淡出 / OnBalloonExploded
     └─ ParticleSystem.IsAlive(true)==false
        + OnBalloonOutcomeStarted 已由同一 UnityEvent 回写
        -> Destroy(effectInstance)
        -> Destroy(ballonRoot)
```

- 旧 ItemScaleListener、PlayVfxListener 与 DestroyListener 已从 ballon 组件和 executor items
  删除。Nail 不缩放；气球消失时点由真实粒子生命结束决定，而非固定 Delay 猜测。
- 模板和新实例都以 `level7` 为稳定坐标系，但实例化时仍显式传入模板世界位置/旋转，避免
  Unity `Instantiate(original,parent)` 复制 `(7.26,0.31)` 到 Nail 后得到 X≈14.54。
- Knock_Explosion 为 World Simulation、非循环，当前理论尾长约 0.6 秒；order=210 高于
  Nail order=105。`maxEffectDuration=2` 仅用于未来误配循环粒子的保护性清理。
- ballon 承载 BaseItemExecutor，因此视觉销毁另有 `_hasStartedOutcomeFlow` 硬门禁；即使
  爆炸后发生长卡帧或粒子为空，也必须等 OnBalloonExploded 所在 UnityEvent 实际执行。
- `_hasHitBalloon` 只表示已发生有效物理接触，必须在 Trigger 第一回调同步设置；
  `_hasHandledBalloonExplosion` 仍由稍后的 OnBalloonExploded 设置，二者分别服务于
  FallArea 竞态门禁和胜利事件一次性门禁，不能合并或提前设置后者。
- Reset/关卡销毁会停止局部协程并销毁独立 effectInstance；共享 PlayVfxListener、粒子
  prefab、BaseItemExecutor、胜负事件、资源地址和其它关卡不变。
- Unity Bee/Tundra 编译与 DLevel_007 PrefabImporter 已通过，169 个 YAML 锚点唯一；完整
  PlayMode、覆盖面积、尾帧隐藏、重复碰撞和移动端仍待验证。
- 回滚时删除局部 Handle/meta，恢复旧 Scale、PlayVfx、Destroy 三组件及 executor 六项顺序。

## 15. 2026-08-02 ballon 爆炸放大与视觉同帧移除

```text
DLevel7BalloonExplosionHandle.Execute
  -> DisableBalloonColliders
  -> Instantiate(template.worldPosition, template.worldRotation, level7)
  -> clone.localScale = template.localScale * 1.3
  -> StopAndClear + Play(false)
  -> HideBalloonVisuals（同一调用栈）
     -> Renderer.enabled=false
     -> CanvasRenderer.alpha=0
  -> 不可见 ballonRoot 继续推进 Executor / OnBalloonExploded
  -> 粒子结束 + OnBalloonOutcomeStarted
  -> Destroy(effectInstance + ballonRoot)
```

- `effectScaleMultiplier` 是 Level7 Handle 的序列化关卡参数，当前 prefab 值为 1.3；模板与
  level7 父节点均为单位缩放，粒子 World Simulation / Local Scaling，因此运行时克隆得到
  稳定的 1.3 倍表现且不会修改共享资源。
- 不能对 ballonRoot 当帧 Destroy/SetActive(false)：该根承载 BaseItemExecutor 与 Handle，
  会中断 SFX、线淡出、胜利事件，并让 OnDisable 清掉新特效。视觉组件当帧关闭、执行器外壳
  稍后销毁是本关的明确生命周期边界。
- 特效实例挂在 level7，是 ballonRoot 的兄弟节点；遍历隐藏 ballon 子树不会误关特效。
- Unity Bee/Tundra 编译与 DLevel_007 PrefabImporter 已通过；169 个 YAML 锚点唯一、内部引用
  无缺失。完整 PlayMode、1.3 倍覆盖、首帧残影、回调链、Reset 与设备表现仍待验证。
- 回滚只需把倍率改回 1 并删除同帧视觉隐藏逻辑；此前的世界坐标修复和安全销毁门禁保留。

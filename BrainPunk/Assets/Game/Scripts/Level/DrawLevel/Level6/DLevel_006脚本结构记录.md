# DLevel_006 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame `DLevel_006`
- 更新日期：2026-08-02
- 当前状态：共享装置抽取、双向 Intro、首笔双球释放，以及“接触即冻结贴合、爱心上冒、
  镜头聚焦、延迟结算”的双球相遇演出已实现；Unity 编译与 Prefab 导入通过，完整 PlayMode
  与 Android/iOS 横屏验收待补
- 当前参考基线：已调整完成的 `DLevel_002` 猫猫 + Spine 夹子 + 蓝球组合
- 共享资源：
  `Assets/Game/Prefabs/DrawLevel/Shared/DrawGame_AlienClipBallRig.prefab`
- 主要脚本：
  - `Assets/Game/Scripts/Level/DrawLevel/DrawAlienClipBallRigView.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawEntrancePathView.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawTwoBallReunionSuccessHandle.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/Level6/DLevel6Ctrl.cs`
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_006.prefab`

本记录取代 2026-07-28 版本中“DLevel06 没有 Intro、不播放猫 control、继续使用旧 catch
Animator”的描述。该描述仅是历史基线，不再代表当前运行行为。

本文较早章节中“右球 `OnTriggerSuccessCtrl` 碰撞即结算、没有冻结或停留”的描述属于本次
需求实施前基线；当前有效契约以第 13 节为准。

## 2. 脚本与组件树

```text
DLevel_006
├─ DrawLevelCtrl                         # 唯一 Draw 运行时与结果锁
├─ DrawLineFeature                       # 画线、有效笔画和动态线段
├─ DelayAutoBeginDrawHandle              # 既有 enter-only 延迟，当前 delay=0
├─ DLevel001Ctrl                         # 历史组件；Actor 无独立刚体，因此不驱动球移动
├─ DLevel6Ctrl                           # 双向可见 Intro、双夹开合、双球释放与 Reset
└─ level6
   ├─ Alien                              # 左装置最终位置 (-5.90, 3.20)
   │  ├─ DrawEntrancePathView
   │  ├─ entranceStartPoint              # 左入场起点 (-16.72, 3.20)
   │  ├─ 猫 SkeletonAnimation / ActorSpineCtrl
   │  └─ catch (BoneFollower -> bone2)
   │     └─ clipSwingPivot
   │        ├─ clipVisual                # clip_close / clip_open
   │        ├─ yellow_ball               # Player；Kinematic -> Dynamic
   │        │  └─ payloadContentRoot
   │        │     └─ Actor.prefab        # Player；保留嵌套链接与 shrink 初始动画
   └─ Alien (1)                          # 右装置最终位置 (4.78, 3.20)，根 (-0.9,0.9,0.9)
   │  ├─ DrawEntrancePathView
   │  ├─ entranceStartPoint              # 镜像后的右入场起点 (15.60, 3.20)
   │  └─ ...
   │     ├─ yellow_ball                  # tools；带双球相遇成功演出组件
   │     │  └─ payloadContentRoot
   │     │     └─ Actor.prefab           # tools；box_pink + shrink
```

共享装置封装内部骨骼、夹子、球、Actor、引用入口，以及默认局部入场起点；实例根 Transform
由关卡直接摆在最终位置。共享装置不保存 Intro 时长、释放时机或胜负条件，`DLevel02` 与
`DLevel06` 继续分别由各自控制器决定这些行为。

## 3. 类职责与边界

| 类/组件 | 类型 | 当前职责 | 不负责事项 |
| --- | --- | --- | --- |
| `DrawLevelCtrl` | 共享运行时 | 初始化、Handler 分发、一次性结果提交、失败与 Reset | 不保存 Level06 的动画名、终点和双球规则 |
| `DrawLineFeature` | 共享画线 | 产生有效 `DrawStrokeResult` 和动态线条物理 | 不操作猫、夹子或球 |
| `DrawAlienClipBallRigView` | 共享 Prefab 视图 | 暴露同一装置内的猫 Spine、BoneFollower、摆动节点、夹子 Spine、球刚体、`payloadContentRoot` 与渐隐组 | 不运行 Intro、不切换物理、不判断胜负 |
| `DrawEntrancePathView` | 共享摆放视图 | 根节点表示最终位置、子节点表示起点，并绘制 Scene 轨迹/起点占位 | 不播放 Tween、不切换物理、不判断胜负 |
| `DLevel6Ctrl` | 关卡局部控制器 | 同步双向 Intro、首笔双夹/双球、镜像释放边界、Reset | 不改共享画线、UI、存档、打点或配置 |
| `DrawTwoBallReunionSuccessHandle` | 共享双球演出 | 右球校验显式配对球；接触后冻结两刚体、播放爱心与相机聚焦，再完成成功结算 | 不修改双夹 Intro、放球、UI、存档或星级 |

## 4. 生命周期与关键调用

1. `Awake()` 解析两套共享装置，初始化各自 `BoneFollower -> bone2`；在移动根节点前分别缓存
   策划最终位置与 `entranceStartPoint` 世界起点，再缓存球父级/位姿、内容根姿态和摆动静止
   姿态，最后把两套装置放回各自起点。
2. 初态为猫 `idle01(loop)`、夹子 `clip_close`、球 Kinematic/零速度；旧代理夹爪碰撞体已
   删除，夹子钩形节点仅保留 Spine/Sprite 视觉。
3. 转场遮罩完全揭开后，`DLevel6Ctrl.PlayDrawIntroAsync()` 同时驱动两边：
   - 左装置从 `X=-16.72` 到 `-5.90`；
   - 右装置从 `X=15.60` 到 `4.78`；
   - 两边行程均为 `10.82`，时长均为 `0.78s`，缓动为 `OutBack(1.9)`；
   - 右根 `(-0.9,0.9,0.9)`，因此同一组局部摆角会在屏幕上形成几何镜像，不再二次反转角度；
   - 两架子均按 `+38° -> -32° -> +15° -> -7° -> +3° -> 0°` 衰减，约 `0.85s`
     收稳；两边都完成后才开放画线。
4. 第一笔有效线完成后，先统一校验两球与两个 `payloadContentRoot`，避免只释放一边：
   - 两夹子播放 `clip_open`；
   - 两球清速度、保持世界姿态脱离 `BoneFollower/clipSwingPivot`，再切 Dynamic 并 WakeUp；
   - 两只猫播放 `control(non-loop)`，完成后回 `idle01(loop)`。
5. 右装置挂载期保持整套根镜像。释放时若球根继承了负缩放，代码将球根缩放归一为正值，
   并把等价镜像转移到 `payloadContentRoot`；球内视觉、Actor 和子碰撞体的世界二维姿态不跳变。
   CircleCollider2D 的微小 offset 同步镜像，Reset 时恢复。
6. 两个球发生普通 Collision2D 后，右球的 `DrawTwoBallReunionSuccessHandle` 只接受显式绑定
   的左球刚体；首次有效接触同帧占用成功演出状态、把两球速度与角速度清零并切为
   Kinematic + FreezeAll，因此即使在空中也停在首次接触姿态，不再受重力或碰撞响应影响。
7. 接触点上方克隆既有 `Bubble` 粒子参数，并在运行时换成程序生成的粉色心形纹理；本关
   GameCamera 在 `0.28s` 内以原视野 `91%` 轻微聚焦双球中点，约 `1.8s` 演出结束前恢复原位，
   随后才正式提交 Success。
8. 失败、手动重来或胜利 Replay 会 Kill 未完成 Tween，但不再重播 Intro；新实例在黑幕下
   直接把两套装置放到各自 FINAL，并恢复球原父级/局部姿态、内容根、Collider offset、
   Kinematic、`clip_close` 和猫 `idle01`，等待玩家第一笔后再正常放球。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/覆盖 | 当前绑定或数值 | 说明 |
| --- | --- | --- | --- |
| `DLevel6Ctrl` | `firstRig` | `Alien`，根位置 `(-5.90,3.20)` | 左侧共享实例与最终位置 |
| `DLevel6Ctrl` | `secondRig` | `Alien (1)`，根位置 `(4.78,3.20)`、`(-0.9,0.9,0.9)` | 右侧镜像共享实例与 90% 整体尺寸 |
| 两套共享实例 | `DrawEntrancePathView / entranceStartPoint` | 默认 local `(-12.022222,0,0)` | 经 0.9 根缩放后左起点仍为 `-16.72`，右起点仍为 `15.60` |
| `DLevel6Ctrl` | `ballReleaseParent` | `level6` | 两球释放后的稳定父级 |
| `DLevel6Ctrl` | Intro | `0 / 0.78s / OutBack 1.9` | 摆动分段与 DLevel02 一致 |
| 共享 Prefab | `DrawAlienClipBallRigView` | 七个内部引用全部非空 | `BoneFollower.boneName=bone2` |
| 共享 `yellow_ball` | Rigidbody2D | Kinematic、mass 10、gravity 1、Continuous | Collider 使用 DLevel02 调好的 offset/radius |
| DLevel06 两球实例 | `Rigidbody2D.Exclude Layers` | `0` | 必须覆盖共享 DLevel02 的 Actor 排除，否则两球不会碰撞 |
| 左球 / Actor | Tag | `Player / Player` | 保留碰撞成功筛选条件 |
| 两侧 Actor | SkeletonAnimation | `_animationName=shrink` | 保留迁移前 DLevel06 的球内角色初始表现 |
| 右球 / Actor | Tag / Skin | `tools / tools / box_pink` | 右球独有 `DrawTwoBallReunionSuccessHandle`，显式配对左球 |
| 双球演出 | Camera / VFX / 时长 | 本关 GameCamera / `Bubble.prefab` / `1.8s` | 视野比例 `0.91`，爱心 order `340` |
| 右装置 | Transform | `(-0.9,0.9,0.9)` | 挂载期整套镜像且统一缩小；释放时镜像转移到 payload |

当前工作区的 `DrawLineFeature` 参数完整保留：线宽 `0.2`、笔刷 `301001`、墨水 `50`、
允许多笔、满长度质量 `200`、阶段 `0/10/20`；这些不是本次共享装置改造内容。

## 6. 数据、UI、资源与事件流

```text
遮罩完全揭开
  -> 左右共享装置同步镜像飞入并收稳
  -> 开放画线
第一笔有效线完成
  -> 两夹 clip_open + 两球实际脱父级
  -> 两球脱父级 -> Dynamic
  -> 两猫 control -> idle01
两球普通 Collision2D
  -> DrawLevelCtrl.TryBeginSuccessPerformance
  -> 两球 Kinematic + FreezeAll，保持首次接触姿态
  -> 接触点上方爱心上冒 + 本关相机轻微聚焦
  -> 约 1.8s 后 CompleteSuccessPerformance
  -> DrawPlayCtrl 原有胜利面板、星级与进度链
```

- DB/Luban/存档：DLevel6Ctrl 不直接读写；成功后的星级与进度仍由原 Draw 结算链处理。
- UI：不新增面板或输入入口；相遇演出占用期间关闭画线与 GameplayPanel 切换，演出完成后
  继续进入原胜利面板。
- 资源：猫、夹子、蓝球和 Actor 通过共享 Prefab 随关卡加载；爱心复用已随包存在的
  `Bubble.prefab` 参数，心形纹理在运行时生成，无新增地址加载或二进制美术资源。
- 事件：不新增业务全局事件；`DrawLevelCtrl` 增加两阶段成功门禁，演出期间拒绝失败或其它
  结果抢占，完成后仍通过原 `Resolved` 事件进入 DrawPlayCtrl。
- Analytics/SDK：无新增或修改。
- `DLevel001Ctrl` 历史组件仍在根节点；Actor 自身 Rigidbody2D 是嵌套移除覆盖，因此其
  `OnUpdate()` 不会推动父球，本次保持这一既有无效行为，不静默新增自动吸附或推进力。

## 7. 验证证据与缺口

- Unity 6.0.59f2 已完成运行时与 Editor 程序集编译；本次目标脚本无 `error CS`。
- 一次性 Editor 迁移/校验确认：
  - DLevel02 与 DLevel06 的三个实例均引用同一共享 Prefab；
  - 内部 Spine/BoneFollower/球/内容根引用完整，`Actor.prefab` 嵌套链接已恢复；
  - DLevel06 终点、左右 Tag、两侧 Actor 的 `shrink`、`box_pink`、双球演出组件与
    画线参数完整；
  - 两球 `Exclude Layers=0`，左右行程均为 `10.82`；
  - 在隔离 Preview Scene 中执行右球实际释放方法后，球根为正缩放、内容子树的世界二维
    位置/基向量在 `0.001` 容差内不变。
- 一次性工具在验证成功后已移除，不会在下次 Unity 会话重跑并覆盖手调。
- Unity PlayMode：`uncertain`。尚未完整观看双边连续 Intro、Spine idle 叠加、首笔开夹、
  两球真实碰撞与胜利面板。
- Android/iOS：`uncertain`，尚未验证横屏边缘、目标帧率和移动端 Physics2D 表现。

## 8. 风险与回滚

- 主要剩余风险：
  - `38/-32/...` 摆动和双向 OutBack 的主观力度仍需 PlayMode 观看；
  - 右侧挂载期仍存在 Kinematic Rigidbody2D 位于镜像父层级的短暂阶段，释放后已归一，
    但目标设备上的 Physics2D 日志与接触表现仍待验证；
  - 当前贴合采用“首次接触姿态冻结”，不是 Joint；视觉稳定且不会继续下落，但若未来要求
    强制把球心吸到固定间距，必须另加可配置对齐规则，不能默认移动已经接触的球。
- 回滚路径：
  1. 将 DLevel06 的两个共享实例替换回旧 Alien/catch/yellow_ball 层级；
  2. 将 `DLevel6Ctrl` 恢复为双 Animator + 双刚体版本；
  3. 若回滚本节位置契约，恢复两个同级 endPoint、场外根位置和旧控制器字段；
  4. 仅在 DLevel02 不再引用时才删除共享 Prefab 与 `DrawAlienClipBallRigView`；
  5. 不涉及 DB、存档、Luban、UI、analytics 或平台数据迁移。

## 9. 2026-08-02 当前碰撞与显示契约

- DLevel06 当前没有 `DLevel6ColliderProxy`。两夹开合仅是 Spine 视觉，球由 Kinematic/锁位姿
  保持，首笔后脱父级并切 Dynamic 才是实际释放。
- 共享 Actor 初态为 `shrink`，根 SortingGroup order `68`；球壳 idle order `100`。
  `payloadContentRoot` 只承担右球释放镜像，不应为了视觉排序移除。
- 必须保留：两球 CircleCollider2D、Actor 主碰撞体、两球 `Exclude Layers=0` 和右球
  `DrawTwoBallReunionSuccessHandle`。删除的仅是两套旧 hook/block/catchArea 代理。
- 迁移日志确认两代理根均删除，控制器无 proxy 常量/方法/调用；PlayMode 与设备验证待补。

## 10. 2026-08-02 双装置最终位置摆放与轨迹预览

- 左右 `Alien` 根现在直接序列化在最终停靠点 `(-5.90,3.20,0)` 与 `(4.78,3.20,0)`；旧
  `endPoint` / `endPoint (1)` 已删除，策划拖动根节点时看到的就是球的最终释放位置。
- 两实例继承共享 `entranceStartPoint.localPosition=(-12.022222,0,0)`。左侧根缩放为
  `(0.9,0.9,0.9)`，世界起点仍为 `(-16.72,3.20,0)`；右根 `(-0.9,0.9,0.9)` 把同一本地偏移镜像为
  `(15.60,3.20,0)`，无需
  维护另一组方向字段。
- `DrawEntrancePathView` 在 Scene 中绘制起终点圆标、虚线路径、箭头和起点虚拟包围盒；
  右侧预览也随根镜像，组件不参与 PlayMode 物理。
- `DLevel6Ctrl` 在 Awake 首次移动前缓存两组世界起终点；Reset 使用缓存起点，Intro 使用
  完整 `DOMove` 回到缓存最终位置。由于当前两组 Y/Z 不变，既有等距 `10.82`、`0.78s /`
  `OutBack(1.9)`、夹子摆动、首笔双球释放和碰撞结算均不变。
- Unity 迁移器对四个世界坐标执行 `0.001` 容差校验；C# 编译与 PrefabImporter 已通过。
  完整双边 PlayMode、Replay、碰撞成功和 Android/iOS 仍为 `uncertain`。
- 回滚需成套恢复场外根位置、两个同级终点和旧控制器字段；只恢复其中一部分会破坏镜像起点
  或导致 Reset 回到最终位置。

## 11. 2026-08-02 共享正位与球内角色无影子

- 两套装置继承共享 Rig 的静止补偿：`clipSwingPivot.localZ=-4.22°`、
  `yellow_ball.localZ=180°`。左侧最终正位，右侧只做水平镜像；现有双向入场摆动以新基准
  缓存、叠加并恢复，时长、角度峰值和双球碰撞成功不变。
- 共享 nested Actor 的完整 Shadow GameObject inactive，因此左右球内角色都不再显示脚下
  影子；右球 `box_pink` 皮肤、`payloadContentRoot` 镜像迁移、Sorting 与碰撞设置保持不变。
- 未修改通用 `Actor.prefab`，DLevel010 继续使用其已有本地 Shadow inactive override。
  完整双装置 PlayMode、Reset/Replay、碰撞成功和 Android/iOS 仍为 `uncertain`。

## 12. 2026-08-02 双装置整体缩放 0.9

- 左装置继承 `(0.9,0.9,0.9)`，右装置使用 `(-0.9,0.9,0.9)`，两边的猫、夹子、球、
  Actor 及碰撞体都统一缩小 `10%`，不分别缩放子节点。
- 共享起点 local X 反补偿为 `-12.022222`，左右世界 START 与 `10.82` 行程不变。
- DLevel06 的双球碰撞成功阈值会随碰撞体尺寸缩小，但质量、重力和结算代码不变。
  需 PlayMode 回归双球首次接触与镜像释放；当前为 `uncertain`。
- 回滚必须同时恢复共享根 `(1,1,1)`、右侧 X `-1` 和共享起点 `-10.82`。

## 13. 2026-08-02 双球 Actor 碰撞结构

- 两套 `yellow_ball` 各保留球根 Rigidbody2D/CircleCollider2D；各自 payload 下的 Actor 继续存在，
  但共享嵌套实例移除 Actor Rigidbody2D 与 PolygonCollider2D。
- DLevel6Ctrl 的双释放、右球镜像迁移和第二球 OnTriggerSuccessCtrl 不变；DLevel001Ctrl 旧组件
  也保留，没有因本次碰撞清理改变脚本组件树。

## 13. 2026-08-02 双球接触冻结、爱心聚焦与延迟成功

- 新增通用 `DrawTwoBallReunionSuccessHandle`，挂在负责判定的球刚体根上，并显式绑定另一个
  `Rigidbody2D`、同关 `DrawLevelCtrl`、本关 GameCamera 与 `Bubble.prefab`。未来双球关复用
  该组件即可；若同关超过两个球，禁止依赖自动查找，必须在 Inspector 明确配对。
- DLevel006 右球原 `OnTriggerSuccessCtrl(isCollision=true)` 已替换。首次配对球碰撞会先调用
  `TryBeginSuccessPerformance()` 原子占用成功，再冻结两球；复合 Circle/Actor Collider 产生的
  后续回调会被本地幂等状态与 Draw 结果门禁共同拒绝。
- 冻结只清零速度/角速度并使用 Kinematic + FreezeAll，不改父级、不创建 Joint、不重新计算
  球心距离。因此球在地面或空中都维持首次接触姿态，演出结束前不会继续下落或弹开。
- 爱心特效复用现有 Bubble 发射、上浮、淡出参数，运行时材质使用程序生成白色心形并染为
  粉红渐变；Sorting Order 为 `340`。镜头只修改本关相机的 localPosition 与正交 Size，按原始
  Size 比例聚焦，保留 Camera.rect，并在正式结算前恢复。
- Draw 的 Pause 不改变 `timeScale`，因此组件显式 Pause/Play 镜头 Sequence 与 ParticleSystem；
  Reset、Disable、销毁会 Kill 演出、清粒子、恢复相机和初始刚体状态。`DrawLevelCtrl` 的两阶段
  门禁同时阻止倒计时/失败在停留期抢先结算，成功门也复用相同门禁，修复原成功动画竞态。
- 验证：Unity 6.0.59f2 Tundra 增量编译成功，无目标 `error CS`；DLevel006 Prefab 已由 Unity
  自动导入并规范化配对刚体引用，旧成功脚本 GUID 在 Draw Prefab 中已不存在。完整 PlayMode
  的实际接触位置、爱心材质、镜头边缘、Pause、Reset/Replay 和 Android/iOS 仍为 `uncertain`。
- 回滚：把右球组件恢复为 `OnTriggerSuccessCtrl(isCollision=true)`，移除
  `DrawTwoBallReunionSuccessHandle` 与 `DrawLevelCtrl` 的 Begin/Complete/Cancel 门禁，即恢复
  “碰撞同帧直接 Success”；不涉及 DB、Luban、存档、星级、analytics 或平台数据迁移。

## 14. 2026-08-05 爱心粒子尺寸与密度调整

- 用户反馈 `20006` 与其它双球相遇关的小心心偏小；原实现沿用 `Bubble.prefab` 的
  `startSize=0.2~0.4`，DLevel006 Prefab 显式配置 `heartEmissionRate=5`，并在连续发射外保留
  一颗即时 `Emit(1)`。
- `DrawTwoBallReunionSuccessHandle` 新增序列化 `heartSizeMultiplier`，脚本默认值为 `1`；本关 Prefab
  显式设为 `1.5`，只把运行时克隆 ParticleSystem 的 `main.startSize` 等比放大，等效随机范围约为
  `0.3~0.6`。持续发射率由每秒 `5` 降为 `4`，首颗即时发射、`1.8s` 演出时长与粒子生命周期不变。
- 当前主贴图是 `Assets/Game/Resources/Sp_hx_ax_01.png`；上文“程序生成心形”仅描述资源加载失败时的
  回退。`Bubble.prefab`、正式贴图、运行时材质来源、Sorting Order `340` 与发射点偏移均未修改。
- 同一共享组件目前只被 D006、D032、D033、D042、D043 使用，五个关卡 Prefab 统一显式配置
  `heartSizeMultiplier=1.5 / heartEmissionRate=4`；本次不改变双球冻结、相机聚焦、两阶段成功、
  Reset/Pause、UI、DB、存档、配置、analytics 或平台流程。
- Unity 6000.0.59f2 自带 Roslyn 使用当前 `Assembly-CSharp.rsp/.rsp2` 完整编译通过：
  `ExitCode=0 / Errors=0 / Warnings=104`，目标脚本无诊断。共享脚本 GUID 的 Prefab 消费者恰为五关；
  D006 静态检查为 `Definitions/LocalReferences/duplicate/missing=102/197/0/0`，成功组件、rate `4`、
  size `1.5` 各 1 个。Unity Editor.log 已记录五个目标 PrefabImporter 成功导入并完成脚本重编译；D006 PlayMode
  的实际尺寸、遮挡、粒子节奏、Pause/Reset/Replay，以及 Android/iOS 横屏清晰度与过绘仍为 `uncertain`。
- 仅回滚本次表现时，把五关的尺寸倍率恢复为 `1`、持续发射率恢复为 `5`；若删除新增序列化字段，
  应同时移除五个 Prefab 的对应 YAML 项。正式贴图及其程序回退、Bubble 源 Prefab、镜头和成功门禁
  均无需回滚。

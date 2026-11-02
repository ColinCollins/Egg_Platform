# DLevel_006 共享猫猫夹子蓝球、双向入场与碰撞过关功能风险评估

> 最新覆盖说明：本文早期“碰撞同帧直接结算、没有冻结/停留”的条目保留为历史基线；
> 当前有效双球成功契约以第 12 节为准。

## 1. 需求与背景

- 用户要求把 DLevel02 已调整好的蓝色 `yellow_ball`、外星猫和 Spine 夹子做成可复用
  Prefab，并在 DLevel06 同时替换两套旧装置。
- 两套装置最终停在 DLevel06 旧位置；左侧从屏幕左边进入，右侧整套镜像后从屏幕右边进入，
  入场回弹和悬挂夹子的惯性摆参考 DLevel02。
- 两侧球内 Actor 继续以 `shrink` 作为初始 Spine 动画；右球继续使用 `box_pink`，Actor 的
  局部坐标/旋转与左球一致，但画面朝向镜像。
- 第一笔有效线后，两猫播放 `control`，两夹播放 `clip_open` 并实际解除物理夹持，两球进入
  Dynamic；最新需求进一步把“两球碰撞即过关”改为“接触即冻结贴合、上冒爱心、镜头轻微
  聚焦，短暂停留后再过关”，并要求该表现可供未来双球关复用。

## 2. 修改前基线

- DLevel06 原有两套独立 Alien、旧 Sprite/Animator 夹子和 Kinematic 球。
- 第一笔后旧 `catch.controller` 把钩爪从闭合静态姿态切到打开静态姿态，两球同帧切 Dynamic。
- 右球 `OnTriggerSuccessCtrl(isCollision=true)` 在普通 Collision2D 中要求另一球属于 Actor
  Layer 且 Tag 为 Player，然后直接调用本关 `DrawLevelCtrl.TryResolve(Success)`。
- 原逻辑没有 Joint、吸引力、位置对齐或碰撞后冻结；“贴着吸住”只是碰撞后立即进入结算的
  视觉结果。
- 旧 DLevel06 没有真实装置 Intro，也不播放猫 `control`。这些历史行为被本次明确需求替换。

## 3. 影响范围

- 新共享运行时视图：
  `Assets/Game/Scripts/Level/DrawLevel/DrawAlienClipBallRigView.cs`
- 新共享双球成功演出：
  `Assets/Game/Scripts/Level/DrawLevel/DrawTwoBallReunionSuccessHandle.cs`
- 新共享 Prefab：
  `Assets/Game/Prefabs/DrawLevel/Shared/DrawGame_AlienClipBallRig.prefab`
- 更新关卡脚本：
  `Assets/Game/Scripts/Level/DrawLevel/Level6/DLevel6Ctrl.cs`、
  `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl.cs`
- 更新关卡 Prefab：
  `Assets/Game/Prefabs/DrawLevel/DLevel_002.prefab`、
  `Assets/Game/Prefabs/DrawLevel/DLevel_006.prefab`
- 同步 Level2/Level6 局部文档、DrawGame 演进记录和 `DevelopMD/已有功能设计与使用记录.md`。

未修改 `DrawLineFeature`、`ActorCtrl`、`OnTriggerSuccessCtrl` 源码、GameplayPanel、DB、Luban、
存档、资源地址、analytics、广告/IAP 或平台分支。`DrawLevelCtrl` 只新增通用两阶段成功门禁，
原 `Resolved` 结果、星级与进度出口不变。

## 4. 新结构与职责边界

共享 Prefab 内部结构：

```text
DrawGame_AlienClipBallRig
├─ DrawEntrancePathView
├─ entranceStartPoint（当前 local X=-12.022222，经 0.9 根缩放后世界偏移仍为 10.82）
├─ 猫 SkeletonAnimation / ActorSpineCtrl
└─ catch (BoneFollower -> bone2)
   └─ clipSwingPivot
      ├─ clipVisual (clip_close / clip_open)
      └─ yellow_ball (Rigidbody2D + CircleCollider2D)
         └─ payloadContentRoot
            ├─ item / idle / open 球视觉
            └─ Actor.prefab（自身 Rigidbody2D 以嵌套移除覆盖去除）
```

- `DrawAlienClipBallRigView` 只暴露共享 Prefab 内部引用；`DrawEntrancePathView` 保存默认相对
  起点并绘制 Scene 轨迹。最终位置仍由实例根 Transform 决定；两者都不保存入场时长、释放
  时机或胜负逻辑。
- `DLevel2Ctrl` 继续负责第二关单装置、倒计时联动和成功球渐隐。
- `DLevel6Ctrl` 负责第六关双装置 Intro、实例镜像、两球释放与 Reset。
- DLevel06 独有的 Tag、两侧 Actor `shrink`、`box_pink`、双球演出组件、最终位置和
  碰撞成功配置均是实例/关卡配置，不污染共享资源。

## 5. 修改后行为

### 5.1 双向可见 Intro

1. 遮罩完全揭开后，两猫均播放 `idle01`，两夹均为 `clip_close`，两球保持 Kinematic。
2. 左装置 `-16.72 -> -5.90`，右装置 `15.60 -> 4.78`；行程都为 `10.82`，时长
   `0.78s`，缓动 `OutBack(1.9)`。
3. 右实例根 `scale=(-0.9,0.9,0.9)`，猫、BoneFollower、夹子、球与摆动形成完整几何镜像；代码继续对
   左右使用同一组局部角，避免重复反转。
4. 夹子局部时间轴为 `+38 -> -32 -> +15 -> -7 -> +3 -> 0°`，总计约 `0.85s`；
   两边全部收稳后才开放画线。

### 5.2 第一笔后的实际释放

```text
第一笔有效线完成
  -> 统一验证两套球与 payloadContentRoot
  -> 两夹 clip_open
  -> 两球清速度并保持世界姿态脱离 clipSwingPivot
  -> 镜像球把负缩放从刚体根转移到 payloadContentRoot
  -> 两球 Dynamic + WakeUp
  -> 两猫 control(non-loop) -> idle01(loop)
```

- DLevel06 不再创建实体 block/hook/catchArea 代理；夹持由 Kinematic、父子关系和逐帧
  位姿保持实现，Spine `clip_open` 只负责视觉，实际释放由脱父级并切 Dynamic 实现。
- 镜像球释放后，Rigidbody2D 根缩放取绝对值；`payloadContentRoot` 承担等价 X 镜像，
  CircleCollider offset 同步翻转。隔离校验确认内容子树的世界二维姿态不跳变。
- 失败、手动重来和胜利 Replay 重建实例后，在全黑遮罩下直接把两套装置放到各自 FINAL，
  恢复原父级、局部姿态、内容根、球 Collider offset、Kinematic、夹子闭合和两猫 idle；
  不再重新播放 Intro，第一笔放球规则保持不变。

### 5.3 双球相遇演出与胜利链

- 左球/Actor Tag 为 Player；右球/Actor Tag 为 tools，右 Actor 皮肤为 `box_pink`；两侧
  Actor 的 SkeletonAnimation 都保留迁移前 `_animationName=shrink`。
- 右球使用 `DrawTwoBallReunionSuccessHandle`，显式绑定左球刚体、当前 DrawLevelCtrl、本关
  GameCamera 和现有 `Bubble.prefab` 粒子模板；旧 `OnTriggerSuccessCtrl` 实例已移除。
- DLevel06 两球必须把共享球的 `Rigidbody2D.Exclude Layers` 覆盖为 `0`。共享 DLevel02 球
  排除 Actor 层；若不覆盖，两球不会产生成功所需的 Collision2D。
- 首次配对碰撞同帧先占用成功演出状态，再清零两球速度/角速度并切 Kinematic + FreezeAll；
  不创建 Joint、不改变父级，也不重新对齐球心，因此在空中同样保持首次接触姿态。
- 接触点上方开始发射粉色心形粒子；相机在 `0.28s` 内聚焦双球中点，正交 Size 缩至原值
  `91%`，总演出约 `1.8s`，恢复镜头后才正式提交 Success。

## 6. 数据、资源、UI 与事件流

- 数据/存档：控制器无直接读写；原 DrawPlayCtrl 继续记录星级和进度。
- 配置/Luban：无新增字段或表。
- UI：无新增 UI；相遇演出期间锁住画线和 GameplayPanel 切换，完成后进入原胜利面板。
- 资源：共享 Prefab 保留嵌套 `Actor.prefab` 链接；爱心复用现有 Bubble 粒子参数，心形纹理
  在运行时程序生成，不新增二进制美术或地址加载。
- 事件：不新增业务全局事件；使用
  `DrawLineFeature -> DrawLevelCtrl -> DLevel6Ctrl` 和
  `DrawTwoBallReunionSuccessHandle -> Begin/CompleteSuccessPerformance -> DrawPlayCtrl`。
- Analytics/SDK/平台：无新增或修改。

## 7. 风险项

| 风险 | 等级 | 当前控制与状态 |
| --- | --- | --- |
| 共享球排除 Actor 导致两球永不碰撞 | 高 | DLevel06 两实例显式覆盖 `Exclude Layers=0`，Editor 校验通过；完整 PlayMode 待补 |
| 旧夹爪 Collider 干扰球或画线 | 高 | 两套 `DLevel6ColliderProxy` 已删除；只保留球与 Actor 的玩法碰撞体 |
| 右球带负缩放进入 Dynamic | 高 | 释放边界把镜像转移到 payload，刚体根归正；隔离世界姿态校验通过 |
| 左右绝对起点导致速度不同 | 中 | 改为相对各自终点同距 `10.82`；共同 `0.78s` |
| 一侧引用失效后只释放另一侧 | 中 | 消耗首笔状态前统一验证两个球和两个 payload；失败时两边都不释放并记录错误 |
| 右皮肤/Tag/双球演出组件在替换实例时丢失 | 高 | 当前实例显式绑定左球、相机与 Bubble；以后 Revert/替换实例需重新检查四个引用 |
| 共享化后 Actor 初始动画/排序回退 | 中 | 共享 base 恢复 `shrink` 与 SortingOrder 68；两实例共同继承 |
| 右根仍保留 X=-1 导致横向拉宽 | 高 | 右根同步改为 `(-0.9,0.9,0.9)`，左根继承 `(0.9,0.9,0.9)` |
| 整体缩小后双球更晚接触 | 中 | 碰撞体随视觉缩小 10%，成功代码和质量不变；需 PlayMode 验收首次接触 |
| 复合 Collider 同帧重复回调 | 高 | 碰撞前先原子占用 Success Performance，本地 `_isPresenting` 与 Draw 状态双重幂等 |
| 爱心停留期被倒计时/失败抢占 | 高 | `DrawLevelCtrl.TryResolve` 在成功演出占用期间拒绝其它结果；Reset 可取消占用 |
| 冻结后视觉仍有间隙 | 中 | 当前保持首次物理接触姿态，不强制球心对齐；完整 PlayMode 需确认球壳观感 |
| 镜头聚焦暴露场景边界 | 中 | 使用本关相机、原 Size 比例和 0.75 位置混合，并在结算前恢复；多宽高比待验收 |
| 心形为运行时程序纹理 | 中 | 无第三方 Demo/新增二进制依赖；具体美术质感需 PlayMode 观看，后续可替换正式模板 |
| 旧 `DLevel001Ctrl` 产生误解 | 低 | Actor 无自身刚体，现状不推动父球；保留历史组件以免扩大本次行为范围 |
| 主观回弹/摆动力度不合适 | 中 | 参数与 DLevel02 一致；完整 PlayMode/设备仍为 `uncertain` |

## 8. 验证证据

- Unity 6.0.59f2 成功编译更新后的运行时与 Editor 程序集，目标脚本未出现 `error CS`。
- 最新 Tundra 增量编译再次成功；Unity 自动导入 DLevel006 Prefab 并将左球 stripped
  Rigidbody 引用规范化，Draw Prefab 中不再存在旧成功组件 GUID。
- 一次性 Editor 结构校验通过：共享内部引用、`bone2`、两套装置动画名、DLevel02 渐隐引用、
  DLevel06 终点/起点/Tag、两侧 Actor `shrink`、皮肤、成功组件及当前
  DrawLineFeature 参数均完整。
- 共享 Prefab 已恢复 `Actor.prefab` 嵌套链接，并保留 Actor Rigidbody2D 的移除覆盖。
- DLevel06 两球的 `m_ExcludeLayers.m_Bits` 均为 `0`；Collider 使用用户要求复用的 DLevel02
  调整值，而不是退回旧 DLevel06 的 `offset=0/radius=0.7`。
- 隔离 Preview Scene 调用真实 `ReleaseBall` 后，右球已脱离装置、刚体根 determinant 为正，
  payload 全部后代的世界二维位置/right/up 在 `0.001` 容差内保持。
- 一次性迁移和校验脚本均已删除。
- Unity PlayMode：未完整执行，双边连续动画、真实 Physics2D 碰撞、成功 UI、Reset/Replay
  仍标记 `uncertain`。
- Android/iOS 横屏设备：未执行。

## 9. 回滚路径

1. 恢复 DLevel06 旧两套 Alien/catch/yellow_ball 层级与旧 DLevel6Ctrl。
2. 移除新增 endPoint 和两个共享实例；若回滚到旧物理夹爪设计，再显式恢复代理与同步代码。
3. 若只回滚镜像释放优化，可移除 `payloadContentRoot` 转移逻辑，但会重新引入负缩放
   Dynamic 刚体风险。
4. 若只回滚共享提取，先让 DLevel02 与 DLevel06 恢复独立完整层级，再删除共享 Prefab 和
   `DrawAlienClipBallRigView`，避免 Missing Prefab/Missing Script。
5. 不涉及 DB、存档、Luban、UI、analytics 或平台数据迁移。

仅回滚本次相遇演出时：把右球恢复为 `OnTriggerSuccessCtrl(isCollision=true)`，删除
`DrawTwoBallReunionSuccessHandle`，并移除 `DrawLevelCtrl` 的 Begin/Complete/Cancel 成功门禁；
双装置 Intro、首笔放球、镜像、球碰撞体和共享 Rig 均无需回滚。

## 10. 2026-08-02 代理碰撞体移除与共享显示修复

- 两套 `DLevel6ColliderProxy` 共 8 个 Collider2D 已连根删除；`DLevel6Ctrl` 同步移除代理
  名称/角度常量、Reset/开夹调用和 `SetColliderProxyOpen()`，避免缺失节点日志。
- 保留两球 CircleCollider2D、Actor 主碰撞体、两球 `Exclude Layers=0`、右球成功组件和
  镜像 offset 转换，因此双球 Collision2D 胜利链未改变。
- 共享 Actor 统一恢复 `shrink`、测试动画 `shrink` 和 SortingOrder `68`；左右球人物都在
  球壳 order `100` 下方。右侧 `box_pink`、根镜像与 `payloadContentRoot` 仍保留。
- Unity 一次性迁移日志确认删除 2 个代理根；静态检查确认 Prefab 只剩 DrawArea 本地 Box，
  必要球/Actor Collider 由共享/嵌套 Prefab 提供。完整双球 PlayMode 和移动端仍为
  `uncertain`。回滚时需同时恢复代理 Prefab 节点与控制器同步代码。

## 11. 2026-08-02 双装置最终位置配置与镜像轨迹

- 两个共享装置根现在直接放在最终停靠点：左 `(-5.90,3.20,0)`、右
  `(4.78,3.20,0)`。旧 `endPoint` 与 `endPoint (1)` 删除，因此策划拖动装置时看到的就是
  两球最终开始下落的位置。
- 共享 Prefab 的 `entranceStartPoint.localPosition=(-12.022222,0,0)`。左侧根 `0.9` 后的
  世界起点仍为 `-16.72`；右侧根 `-0.9` 自动把同一偏移镜像为 `15.60`，
  继续保持两边等距 `10.82`。
- Scene Gizmo 显示两边的起终点圆标、虚线路径、方向箭头与起点装置包围盒；它仅提供
  编辑器预览，不新增 Collider，也不参与双球碰撞胜利。
- DLevel6Ctrl 在 Awake 移动前缓存两组起终点，随后放到起点；Reset 使用缓存起点，Intro
  用完整 `DOMove` 回到缓存最终位置。既有 `0.78s / OutBack(1.9)`、镜像、夹子衰减摆、
  首笔双球释放、右球 payload 正缩放转换和碰撞即结算均未改变。
- Unity 迁移器已在 `0.001` 容差内验证四个世界坐标并通过 C# 编译/PrefabImporter。
  双边连续 PlayMode、Physics2D 成功、Replay 和 Android/iOS 仍为 `uncertain`。
- 回滚本节需同时恢复两个场外根、两个同级终点和旧控制器字段；不得只取消镜像子起点，
  否则右侧会从错误方向入场。

## 12. 2026-08-02 双球 Actor 多边形碰撞移除

- 左右两套共享 Rig 都保留球内 Actor、ActorCtrl 与 `shrink` 表现，只统一移除 Actor 自带的
  PolygonCollider2D；通用 Actor prefab 未修改。
- 两个 Actor 自带 Rigidbody2D 继续保持移除，左右球分别由各自 yellow_ball 根 Rigidbody2D
  统一模拟；球根 CircleCollider2D 与 Actor 既有子 BoxCollider2D 保留，只移除 Actor Polygon。
  第二球既有 `OnTriggerSuccessCtrl` 双球碰撞结算仍绑定球根，不依赖 Actor Polygon。
- 完整双球首次碰撞、镜像释放、失败和 Replay 为 `uncertain`。回滚只恢复共享 Polygon removed
  override；恢复 Actor Rigidbody2D 会形成嵌套刚体，不能作为本需求回滚方式。

## 12. 2026-08-02 可复用双球相遇成功演出

- 当前实现基线从“右球碰撞同帧直接 Success”升级为两阶段成功：碰撞帧调用
  `TryBeginSuccessPerformance()` 锁住结果，演出完成后调用 `CompleteSuccessPerformance()`。
  `CancelSuccessPerformance()` 供 Reset、Disable、销毁等中断路径解除 UI/输入锁。
- 组件必须挂在其中一个球的 Rigidbody2D 根上，并绑定另一个球。DLevel006 绑定右球为判定宿主、
  左球为 `otherBall`；只有这个刚体的复合 Collider 接触才会触发，不再依赖 Layer 名和 Tag
  推断任意 Actor。未来超过两球的关卡必须显式配对。
- 冻结策略是速度/角速度清零、两球 Kinematic、FreezeAll；它满足空中不下落和接触后不弹开，
  同时避免 Dynamic Rigidbody 父子化或临时 Joint 的求解抖动。局部 Reset 会恢复 Awake 缓存的
  bodyType、constraints、simulated，DLevel6Ctrl 仍负责原父级与初始位姿。
- 爱心使用 `Assets/Game/ArtSrc/Particles/Prefabs/Bubble.prefab` 的生产粒子参数，运行时克隆材质
  并换成程序生成心形纹理，Sorting Order `340`；源 Prefab 和共享材质不会被修改。
- 相机只操作 DLevel006 内的 GameCamera localPosition/orthographicSize，不改共享
  `GameCamera.prefab` 和 `Camera.rect`；Pause 时显式暂停 DOTween 与 ParticleSystem，Reset/退出
  立即恢复原相机状态。
- 验证已覆盖脚本编译、Prefab 导入、显式引用和旧组件移除。完整 PlayMode 的空中接触冻结、
  爱心朝向/颜色、相机边缘、暂停、Replay、真实胜利 UI 和 Android/iOS 均为 `uncertain`。

## 13. 2026-08-04 正式爱心贴图与 DLevel032 边界更正

- DLevel032 已按最新需求移除猫与夹子，不再复用 `DLevel6Ctrl` 或共享猫夹 Rig；其两颗原地球由
  共享 `DrawTwoBallReleaseCtrl` 在首笔后直接切换为 Dynamic。
- 此前只为 DLevel032 增加的 `playEntranceAnimation` 序列化开关已从 `DLevel6Ctrl` 和 DLevel006
  Prefab 撤回。DLevel006 继续保持原有双边入场、夹子惯性、开夹放球和 Reset 行为。
- 上文“爱心使用程序生成纹理”现在只描述回退路径。当前共享组件优先读取
  `Assets/Game/Resources/Sp_hx_ax_01.png`，资源存在时使用白色粒子颜色以保留原画；加载失败才生成
  旧白色程序心形。所有以后复用 `DrawTwoBallReunionSuccessHandle` 的双球关卡自动使用同一资源。
- 图片与用户指定源文件 SHA256 完全一致；本轮完整 `Assembly-CSharp` 聚焦编译通过。D006 原入场
  PlayMode 回归、爱心实际尺寸/朝向和移动端压缩仍为 `uncertain`。
- 只回滚贴图时删除 Resources 分支和 PNG 即可；D032 的局部控制器不应回滚为 DLevel6Ctrl。

## 14. 2026-08-05 爱心放大与发射密度风险补充

- 本次只调整共享 `DrawTwoBallReunionSuccessHandle` 的爱心表现参数：新增默认值为 `1` 的
  `heartSizeMultiplier`，D006/D032/D033/D042/D043 均显式设为 `1.5`；五关
  `heartEmissionRate` 均由 `5` 降为 `4`。运行时只缩放克隆粒子的 `startSize`，不修改
  `Bubble.prefab` 或 `Sp_hx_ax_01.png`。
- 原 Bubble 随机尺寸 `0.2~0.4` 放大后约为 `0.3~0.6`；连续发射率降低，但首颗 `Emit(1)`、
  `1.8s` 停留、生命周期和结束时 Clear 保持不变，因此不会因降密度出现首次接触无即时反馈。

| 风险 | 等级 | 控制与待验证项 |
| --- | --- | --- |
| 放大后遮挡球、角色或画线 | 中 | 保留接触点上方偏移与 Sorting Order；五关需逐关 PlayMode 目测 |
| 发射率降低后爱心过疏 | 中 | 保留即时 `Emit(1)`；以五关相同 `4/s` 为统一基线，实际节奏待目测 |
| 更大粒子增加移动端过绘 | 中 | 不增加生命周期、系统数量或材质实例数量，并同步降密度；Android/iOS 仍为 `uncertain` |
| 新字段只在部分 Prefab 配置 | 高 | 五个现有消费者必须同时显式保存 `1.5 / 4`，并静态核对消费者集合与 YAML |
| Reset/Pause 后残留放大粒子 | 中 | 沿用现有 Pause/Play 和 Stop/Clear/Destroy；连续 Replay 仍待动态验证 |

- 本次代码已使用 Unity 6000.0.59f2 自带 Roslyn 与当前 `Assembly-CSharp.rsp/.rsp2` 完整编译：
  `ExitCode=0 / Errors=0 / Warnings=104`，目标脚本无诊断。静态检查确认共享脚本消费者恰为五关，
  每关成功组件、rate `4`、size `1.5` 各 1 个；D006 Prefab 为 `102/197/0/0`
  （Definitions/LocalReferences/duplicate/missing）。Unity Editor.log 已记录五个目标 PrefabImporter 成功导入并完成脚本重编译；
  D006 PlayMode、低帧率和移动端均为 `uncertain`。
- 回滚只需把五关倍率恢复为 `1`、发射率恢复为 `5`，必要时成组移除新增字段及五个 Prefab YAML；
  不应删除正式贴图、修改 Bubble 模板或回滚双球冻结、相机与成功结算。

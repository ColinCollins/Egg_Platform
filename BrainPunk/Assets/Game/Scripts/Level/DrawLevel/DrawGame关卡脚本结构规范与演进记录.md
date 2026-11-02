# DrawGame 关卡脚本结构规范与演进记录

## 2026-09-16：D096踩线阴影局部表现组件复盘

D096“踩线上不要有影子”最终采用画线模板WithoutShadowGround层13、Actor接地掩码补该层，再由本关DLevel096LineShadowHandle根据实际脚底接触隐藏整组Shadow。DrawLineFeature克隆与碰撞段继承模板层；单纯ignoreLayer会遗漏线端的下层投影或误屏蔽画线之上的普通地面，最终未采用。组件参考Level143踩绳职责，显式绑定主控/主Collider/Shadow，结果锁后不重新打开影子；不修改共享Actor、画线、阴影或旧关代码。26项Unity隔离检查覆盖正式Actor形状的水平/斜线/线端接触、侧碰/顶线、普通地面及结果锁边界，完整地图与移动端未验收。此为本关表现差异，不自动推广至旧关。

## 1. 需求与背景

自 2026-07-28 起，每完成一关 DrawGame，都要按最终代码与 prefab 记录该关的脚本结构。
后续自动开发应先选择玩法最接近的已验收关卡作为参考基线，尽可能保持职责、命名、
生命周期和通信结构一致；每关收尾同时复盘结构，持续沉淀和验证可复用改进。

本规范约束开发与记录流程，不替代各关的功能说明、风险评估或验证记录。

## 2. 原流程基线

- DrawLevel 目录已经存在共享功能/风险文档和个别关卡功能文档。
- 截至本规范建立时，没有统一要求每关单独留下“脚本结构记录”，各文档对脚本树、
  生命周期、接口、prefab 绑定和相对基线差异的覆盖范围不一致。
- 当前工作区的 DrawGame 第 1/2 关仍有未提交改动；本次不修改、归并或追认这些改动，
  也不据此声明某个结构已经验收。

## 3. 每关开发闭环

### 3.1 开发前：选择参考基线

1. 阅读本规范、DrawLevel 共享文档和玩法最接近的已验收关卡结构记录。
2. 不机械地只选编号最近的关卡；优先选择玩法机制、开场、画线回调、机关、结算和
   Reset 生命周期最相近的关卡。
3. 在本关结构记录中写明参考关卡、选择理由和预期差异。
4. 若没有可用的已验收基线，明确标记 `uncertain`，从共享 DrawLevel 契约和实际代码
   建立首个局部基线，不虚构历史约定。

### 3.2 开发中：默认保持一致

默认对齐以下结构：

- 目录、类名、命名空间和 `#region` 分区；
- 共享控制器与关卡专用控制器的职责边界；
- 继承关系、已存在接口和回调入口；
- Intro、Drawing、Stroke Completed、Resolved、Reset、Disable/Destroy 生命周期；
- GameplayPanel、局部引用和事件的所有权；
- prefab 序列化字段、节点/组件绑定和资源依赖；
- 失败、成功、重开、暂停、清理及异步回调失效方式。

本关机制确实需要偏离时，可以保留局部差异，但必须记录原因、影响面、验证范围和回滚方式。
不得为了表面统一把单关状态、动画名或局部机关职责上提到共享类。

### 3.3 完成后：生成结构记录

每关对应目录：

`Assets/Game/Scripts/Level/DrawLevel/LevelN/`

必须创建或更新：

`DLevel_XXX脚本结构记录.md`

记录应以最终脚本、prefab YAML、资源和验证输出为证据。记录未完成时，该关不得报告为
交付完成。

### 3.4 收尾：自动结构复盘

1. 将本关结构与参考基线逐项比较。
2. 把差异标为：本关必要差异、临时兼容、缺陷修复、可复用候选或已验证升级。
3. 检查是否出现重复样板、职责漂移、重复事件、重复清理、生命周期缺口或 prefab
   绑定风险。
4. 将可复用结论追加到本文“结构演进记录”；证据不足的项目只记为候选。
5. 同步完成本关功能文档、风险评估和实际可执行的验证。

## 4. 单关脚本结构记录模板

```markdown
# DLevel_XXX 脚本结构记录

## 1. 基本信息

- 关卡：
- 记录日期：
- 交付/验收状态：
- 参考基线关卡：
- 选择基线理由：
- 主要脚本：
- 关卡 prefab：

## 2. 脚本与组件树

（按实际目录、类和 prefab 组件列树；区分共享脚本与本关脚本。）

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 创建与销毁方 | 不负责事项 |
| --- | --- | --- | --- | --- | --- |

## 4. 生命周期与关键调用

（记录加载、Intro、开放画线、落笔回调、机关演出、成功/失败、Reset、退出和清理顺序。）

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或资源 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |

## 6. 数据、UI、资源与事件流

- DB/存档：
- Luban/配置：
- UI：
- 局部引用：
- 事件：
- 资源加载：
- Analytics/SDK：

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |

## 8. 可复用结论与优化候选

- 本关继续沿用的稳定结构：
- 本关验证的新结构：
- 暂不共享的局部实现：
- 后续关卡需继续观察的候选：

## 9. 验证证据与缺口

- 静态检查：
- 编译：
- Unity PlayMode：
- Android/iOS：
- 未验证项：

## 10. 风险与回滚

- 主要风险：
- 回滚路径：
```

## 5. 持续优化与升级门禁

### 5.1 可以直接采用

满足以下条件的改进，可在当前或新关卡中验证后采用并记录：

- 只影响当前关卡或新增代码；
- 不改变已验收关卡的玩家可见行为、存档、资源、UI、事件或平台流程；
- 能通过静态检查、编译和该关适用的运行时验证；
- 职责与生命周期边界比参考基线更清楚，没有引入额外全局依赖。

### 5.2 保留为候选

出现以下任一情况时，不自动升级共享结构：

- 只在单关出现，尚不能证明可复用；
- 需要修改共享控制器、共享 UI、全局事件或基础 Actor；
- 会迁移或重构已验收关卡；
- 回归范围、资源绑定、移动端表现或清理路径尚未验证；
- 与需求文档或既有关卡实际行为存在冲突或歧义。

### 5.3 允许升级共享结构

共享抽象、接口升级或既有关卡迁移必须同时满足：

1. 多个已完成关卡记录显示同一模式稳定重复，而非仅代码长得相似；
2. 收益、职责边界、受影响文件和关卡清单明确；
3. 不改变既有玩家体验；若改变，先作为明确需求记录并取得确认；
4. 回归方案覆盖全部受影响关卡的进入、失败、重开、通关、退出和清理；
5. 编译、静态检查及可执行的 PlayMode/设备验证完成，未验证项明确保留；
6. 本文演进记录写明采用、暂缓或回滚决定。

不得仅为目录、命名或形式统一而自动批量改造已验收关卡。

## 6. 结构演进记录

| 日期 | 来源关卡/任务 | 候选或决策 | 状态 | 证据与影响范围 |
| --- | --- | --- | --- | --- |
| 2026-09-20 | DLevel_079 进球 FIFA Goal 表现接线补齐 | 单关中间成功表现由本关 Ctrl 占用共享成功演出，局部 Trigger Handle 只转发目标球；Prefab 必须同时验证视觉实例、Ctrl 引用和 Trigger 组件三处显式绑定 | 本关采用，暂不共享 | 修复前脚本存在但 Prefab 三处接线缺失，Git 历史无已提交接线；补齐后 175 个 YAML 锚点唯一且本地引用闭合。未改共享 Draw、主线 Level87 或 FIFA 源 Prefab；正式 PlayMode/移动端仍 `uncertain` |
| 2026-09-18 | DLevel_070 碰撞 eat 后成功 | 本关 Ctrl 复用共享成功演出占用，Actor 子类只管理 eat 与 Complete/中断回调；保留既有首笔 fruit/weight 释放配置 | 本关采用，暂不共享 | Unity 编译及 Prefab/真实 Spine 隔离检查；整关 PlayMode/移动端仍 uncertain，详见 Level70 结构记录；不修改共享 Actor、Draw 主控或旧关 |
| 2026-07-28 | 用户新增长期开发要求 | 建立逐关脚本结构记录、参考基线、差异说明、自动复盘和共享升级门禁 | 已采用 | 已固化到 `AGENTS.md`；当前未据此修改进行中的 DLevel 第 1/2 关 |
| 2026-07-28 | DLevel_004 Box 释放 | 每个 DLevel 保持一个主 DrawLevelCtrl；多个相互独立的局部控制器通过现有 Handler 接口接收生命周期 | 局部验证 | DLevel4Ctrl 只消费首笔完成；Reset/Replay 继续整关重建，未增加局部状态恢复或修改共享代码 |
| 2026-07-31 | DLevel_004 石头命中特效定位 | 只影响单关的 Collision2D 表现应由关卡局部组件消费 contact point，不修改复用范围未知的共享陷阱 | 局部验证 | 新增 `DLevel4RockTrapHandle`，10 个石头用 `GetContact(0).point` 定位既有 `Knock_Explosion`；保留死亡、击退、音效和失败事件，Unity 编译通过，PlayMode/设备待验证 |
| 2026-07-28 | DLevel_005 射箭生存 | 单关 Enemy 动画序列保留在关卡 Actor 子类，关卡控制器只管理攻击节奏和生成物生命周期 | 局部验证 | DLevel5ActorCtrl 复用 Actor 基础能力但隔离动画名/状态；箭以 Rigidbody2D Impulse 推进且不定时销毁，仍保留结算/重置/退出清理，未修改共享 ActorCtrl |
| 2026-07-31 | DLevel_005 参数化物理射箭与命中定格 | 单关发射参数、箭头 Collider、渐显、箭间隔离和命中附着保持在 Level5 局部组件；不改共享 Actor、DrawLineFeature、陷阱或全局 Layer 矩阵 | 局部验证 | 移除 Player 目标点反算；两发随机袋包含短程 `18.7~19.3 / 63°~65°` 与长程 `19.7~20.3 / 57°~59°` 各一支，以重力倍率 3、质量 8 的等效 Impulse 真实发射，组内顺序和档内参数均随机；箭头小 Collider、Continuous/Interpolate、0.06 秒渐显；动画倍率 2.25、间隔 0.2；场景命中先完成接触响应，下一物理步再扎住，PlayMode/设备待验证 |
| 2026-07-31 | DLevel_005 玩家命中定位与取消击飞 | 单关箭矢的受击表现由 Level5 局部陷阱组件消费 Collision2D 接触点，不修改共享陷阱或 Actor | 局部验证 | 箭模板换用 `DLevel5ArrowTrapHandle`，粒子使用真实 contact point，保留死亡、音效、0.5 秒失败事件与销毁取消；移除向上 Knockback 并清零玩家刚体速度。画线、其他关卡和共享 `OnTriggerTrapHandle` 行为不变；PlayMode/设备待验证 |
| 2026-07-31 | DLevel_005 连续区域分层落箭 | 真实物理发射的区域覆盖使用关卡局部分层随机袋，不重新引入 Player 目标点或手工轨迹 | 局部验证 | 原两档中间存在约 1.5 世界单位空白；当前每轮洗牌 8 段，段内继续随机，速度 `22.2~23.8` 与仰角 `65°~57°` 共用同一分层位置，重力倍率 4。静态平地落程 `10.074~13.817`、相邻边界间距 `0.454~0.476`，完整 C# 编译通过；受阻后的可见分布、PlayMode 和设备待验证 |
| 2026-07-31 | DLevel_005 线摩擦与死亡现场保留 | 单关物理手感优先用 Prefab override 复用既有 PhysicsMaterial2D；持续生成物的失败结果与真正退场清理可分开 | 局部验证 | 画线模板刚体只在 DLevel005 绑定 `DrawStrokeStable`（friction 0.8、bounce 0）；Failed `Resolved` 只停射，箭保留到 0.5 秒淡出后的旧实例禁用，Success/显式 Reset/退出仍清理。未修改共享 DrawLineFeature 或材质资源本体；完整 C# 编译 `0 error / 104 warning`，Prefab 引用检查通过，PlayMode/设备待验证 |
| 2026-07-28 | DLevel_006 双 Alien 释放 | 固定数量的单关 Animator 与刚体可由同一局部 Handler 同步驱动；Draw 碰撞胜利必须提交到 DrawLevelCtrl，不复用主线胜利事件 | 局部验证 | DLevel6Ctrl 首笔开夹并释放两球；OnTriggerSuccessCtrl 碰撞后调用本关 TryResolve(Success)，未新增 Intro、全局事件或共享 Actor API；PlayMode/移动端待验证 |
| 2026-07-31 | DLevel_002 Spine 架子与首笔释放 | 骨骼跟随、额外甩动和夹持刚体应分层；悬挂物惯性应以连续绝对角峰值表达，刚体进入物理前先保持世界姿态脱离动画父级 | 观察中 | `BoneFollower(bone2) → clipSwingPivot → clipVisual + yellow_ball` 隔离 Spine LateUpdate 与 DOTween；Intro 以 `+38 → -32 → +15 → -7 → +3 → 0°` 显式阻尼摆替代到点 Punch，首笔后先脱父级再 Dynamic。新 clip 003 原位更新 JSON 并保留 GUID/纹理设置；Unity 编译与关键帧渲染通过，完整 PlayMode/设备待验证，不升级共享 API |
| 2026-07-31 | DLevel_002/006 猫、Spine 架子与蓝球复用 | 第二个明确复用关卡加上用户显式要求，允许共享纯结构 Prefab 与强类型 View；起点/终点、Intro、释放、倒计时、皮肤、碰撞层和胜负继续留在关卡层 | 局部验证 | 新增 `DrawGame_AlienClipBallRig.prefab` 与 `DrawAlienClipBallRigView`，DLevel002 一套、DLevel006 两套实例共用；Actor 保持嵌套 Prefab。DLevel006 左右等长 `10.82` 反向入场，右根镜像在释放时转移到 payload，双球实例覆盖 `Exclude Layers=0` 维持碰撞成功，两侧 Actor 保留 `shrink` 初始动画。Unity 编译、结构校验和隔离镜像释放通过，完整 PlayMode/设备待验证 |
| 2026-08-02 | DLevel_002/006/010 猫夹子碰撞清理与共享初态修复 | 视觉夹爪不得以无业务消费者的 Collider 充当持球机制；共享 Prefab 应保存各引用关共同需要的 Actor 初态与排序 override | 局部验证 | 删除共享旧夹爪 4 个 Collider、DLevel002 Alien 根 Trigger、DLevel006 两套代理根及 DLevel010 5 个旧 Collider；DLevel6Ctrl 移除代理同步代码。保留球/Actor 玩法碰撞体。共享 Actor 恢复 `shrink`、测试动画 `shrink`、SortingOrder 68；Intro、镜像、双球成功和倒计时链不变。Unity 迁移与序列化检查通过，完整 PlayMode/设备待验证 |
| 2026-07-31 | DLevel_002 成功球同步渐隐 | Actor 父级或兄弟层级的附属 Sprite 不应扩大 Actor 子树收集边界，可由共享成功链提供可空额外组 | 观察中 | `DrawLevelCtrl.successExtraRendererGroup` 与 Actor 共用原 `0.4s` alpha；DLevel_002 在 yellow_ball 根绑定 includeInactive 组，Reset 恢复 1。未绑定关卡行为不变，不新增 Tween/等待/全局事件；静态编译通过，完整成功 PlayMode/设备待验证 |
| 2026-07-29 | DLevel_007 画线投掷 | 完成线段可通过 DrawLineFeature 只读 API 交给关卡局部 Handler，所有权与 Reset 清理仍留在 DrawLineFeature | 观察中 | DLevel7Ctrl 只接管第一笔，重设中心后直接定位到 startPoint 再投掷，不播放平移 Tween；投掷使用可配二维方向，Scene Gizmos 与运行时共用归一化方向；Enemy 复用 LayerCollisionDetector 的 Draw Layer 过滤并动态调用本关 Collision2D API，不再给线段添加重复碰撞转发器；ballon 单独开启 PlayVfxListener 默认关闭的 AutoPlay 以兼容 playOnAwake=false 粒子；Player throwing 隔离在 DLevel7ActorCtrl；目前仅单关和静态/编译证据，不扩展 DrawStrokeResult 或迁移既有关卡 |
| 2026-07-31 | DLevel_007 单笔、飞行渐显与 FallArea 出界失败 | 单关投掷表现和失败边界继续留在 DLevel7Ctrl；Trigger 过滤复用既有 LayerTriggerDetector，不修改共享 FallArea/FailAreaTriggerHandle 或 DrawLineFeature API | 局部验证 | 本关 `allowMultipleStrokes=false`；完成线先隐藏，throwing 45% 释放帧才在 startPoint 同帧启用物理/冲量，一次物理步进及随后渲染更新后用 0.2 秒恢复原 Gradient；左右 FallArea 仅监听 Draw Layer，attachedRigidbody 校验当前投掷物，气球未爆炸才 TryFailAndReset。Unity 编译和 prefab 导入通过，完整 PlayMode/设备待验证 |
| 2026-07-31 | DLevel_007 气球触发与 Enemy 接触点 | 运行时生成的子 Collider 应按稳定继承的 Layer 过滤；已有 Collision2D 应持续传递到表现层，避免退化为 Actor 原点 | 局部验证 | ballon 原 Tag trigger 原位改为 Draw LayerTriggerDetector，并保留 BaseItemExecutor 全执行链；Enemy 粒子优先使用 contact[0]，无 contact 回退 Collider.ClosestPoint，只改 XY 并保留原 Z。未改共享生成器/Detector；Unity 编译、prefab 导入和 YAML 唯一性通过，整关 PlayMode/设备待验证 |
| 2026-07-31 | DLevel_007 气球原尺寸爆炸覆盖 | 父节点 Instantiate 的局部坐标语义和视觉销毁时点属于单关表现，应由 Level7 局部 Handle 明确世界爆点并等待真实粒子结束，不修改共享 PlayVfxListener | 局部验证 | 移除 ballon Scale/PlayVfx/固定 Delay Destroy；Trigger 第一回调先记录当前投掷物已命中，第二回调启动局部 Handle，避免低帧率下胜利事件前被 FallArea 误判；Handle 在模板世界点播放 order 210 粒子，0.1 秒放行原结算事件，IsAlive=false 且同一 UnityEvent 已回写 OnBalloonOutcomeStarted 后才清理特效和原尺寸气球。Unity 编译、prefab 导入及 169 个 YAML 锚点唯一性通过；完整 PlayMode/设备待验证 |
| 2026-08-02 | DLevel_007 爆炸放大与同帧隐藏气球 | 承载执行器的机关根不能在命中帧直接销毁；玩家可见对象与执行器外壳的生命周期应分离，并继续局部配置表现参数 | 局部验证 | Level7 Handle 新增 `effectScaleMultiplier=1.3`，按模板世界点生成后放大本次克隆；Play 同一调用栈关闭 ballon 子树 Renderer/CanvasRenderer，保留不可见根继续 SFX、线淡出与胜利回调，粒子/门禁均完成后再销毁。Unity 编译、prefab 导入及 169 个 YAML 锚点/内部引用检查通过；完整 PlayMode/设备待验证 |
| 2026-07-29 | DLevel_008 weapon 首笔发射 | 预置 Kinematic 物体在第一笔后切 Dynamic 并施加一次冲量，复用现有局部 Handler；方向和大小拆分配置 | 观察中 | DLevel8Ctrl 实现 IDrawStrokeCompletedHandler，首笔后发射既有 weapon，Reset 恢复初始位姿/Kinematic/零速度；静态/编译通过，PlayMode 与移动端未验证，不形成共享抽象 |
| 2026-07-31 | DLevel_008 首笔挥刀旋转 | 在既有线性 Impulse 后追加可调的小幅旋转 Impulse，保持角色与刀的复合刚体及局部边界，不升级共享 Actor 或 Draw 接口 | 观察中 | `rotationImpulse=-60`，首笔按 `AddForce -> AddTorque` 执行，负值顺时针；Reset 继续清零角速度并恢复初始旋转；静态检查通过，本次增量编译、PlayMode、碰撞命中和移动端待验证 |
| 2026-07-31 | DLevel_009 气球吊门编队稳定释放 | 单关视觉绳与物理绳共用锚点几何；编队内部 Collider 保持真实碰撞，不改全局碰撞矩阵 | 局部验证 | DLevel9Ctrl 首笔前锁定门和三个 Nail，首笔后同步释放，Reset 恢复初态；三条 DistanceJoint2D 以 `Max Distance Only=true` 只限制最大绳长，单球受压时可独立缩短；气球碰撞半径 `0.60` 令图片重叠但实体初始留微隙；隔离 PhysicsScene2D 实测单球独立位移 `0.4437`、松弛 `0.4021`、恢复后零超长，完整整关/设备仍待验证 |
| 2026-07-31 | DLevel_009 线段承重与门碰撞边界 | 短线质量必须低于编队净浮力；成功 Trigger 和隐藏门盒不应截断画线；完成线只经气球向门传递负载 | 局部验证 | 本关以 `strokeMass=5`、`minimumMassRatio=0.05` 覆盖同日同步值；SuccessDoor 与 DoorOutside 均使用 Draw 层 14；门实体盒按主门体网格校准为 `offset=(0.5309,0.1878)`、`size=(1.5879,2.1866)`；DLevel9Ctrl 对每一笔只忽略该笔 Collider 与门实体盒的碰撞，门仍碰地面/角色；隔离探针确认门/线 IgnoreCollision，玩家实际线型、多笔、成功触发和移动端待验证 |
| 2026-07-29 | DLevel_010 Alien 入场、ball 释放与多笔线段互撞 | 可见 Intro 保持关卡局部；多笔线段互撞通过共享 DrawLineFeature 的默认关闭开关局部启用，不改变旧关 | 观察中 | DLevel10Ctrl 复用 IDrawLevelVisibleIntroHandler，抵达后设置 catch `isOpen=true` 并释放 ball；本关另启用 RDP 简化、最多 32 Capsule 与 Polygon Composite Merge，同笔重叠合并、不同笔互撞。C# 编译通过，PlayMode/设备未验证；共享开关默认 false，不迁移 DLevel_001～009 |
| 2026-08-02 | DLevel_016 害怕初态、无影子与首笔落石 | 对完全一致的首笔 Kinematic→Dynamic 机关直接复用既有 Handler；共享角色表现只做关卡实例 override；普通 Collider 的命中链必须使用 Collision2D | 局部验证 | Actor 只覆盖为 DLevel008 的 `cover_ears01` 并关闭 Shadow GameObject；移除会驱动 Static Actor 且覆盖害怕动画的误带 DLevel001Ctrl；根节点复用 DLevel4Ctrl，唯一绑定 Stone；Stone 保持非 Trigger 并复用 DLevel4RockTrapHandle，以 contact point 播放既有受击粒子和派发失败。未修改 DLevel004/008 prefab 或共享 Actor；136 个 YAML 锚点/引用、Unity 导入与编译通过，完整 PlayMode/设备待验证 |
| 2026-08-02 | DLevel_016 落石重量 | 单关重物应与运行时画线的真实质量范围比较，优先调整机关自身质量，不改整关 strokeMass 或 gravityScale | 局部验证 | 本关线条为 7.5～30，Stone 从 mass 1 调为 100，约为满长线 3.33 倍、常见短线 8～10 倍；首笔释放、落下加速度、Collider 与失败链不变。Discrete 下的细线抖动/穿透和最终手感待 PlayMode/设备验证 |
| 2026-08-02 | DLevel_016 被落石推动的画线命中角色 | 复合角色的危险碰撞身份应以宿主明确绑定的 ActorCtrl 层级为准，不要求每个辅助 Collider 重复 Player Tag | 局部验证 | 本关启用的右侧 `pushCollider` 属于 Actor Layer 但为 `Untagged`，Stone 推线时会先命中它；DLevel4RockTrapHandle 改为 `GetComponentInParent<ActorCtrl>() == actor`，主体/子 Collider 均只匹配本关 Actor，不修改共享 Actor Tag、Layer 或碰撞矩阵。Unity C# 编译成功；Stone 直撞、直接画线、Stone 推线、失败防重与设备待 PlayMode 验证 |
| 2026-07-29 | DLevel_001 Guide 整组落笔隐藏 | 有效落笔时机由 DrawLineFeature 产出局部 C# 事件，单关提示对象仍由 DLevel 控制器持有 | 观察中 | `StrokeStarted` 只在输入/UI/区域门禁通过并成功创建线段后派发；DLevel001Ctrl 隐藏整个 `Guide` 根节点，使 GuideLine 和 Finger_Scene 一起消失，不修改其它关卡；PlayMode/设备待验证，暂不升级为共享 Handler |
| 2026-07-29 | DelayAutoBeginDrawHandle 与 Erase 解耦 | 只负责开放输入的延迟 Handle 应在转场揭开后执行，不进入加载完成条件 | 已采用 | 复用 `IDrawLevelVisibleIntroHandler` 标记；各关 prefab 配置的输入延迟不再阻塞 Erase，Reset 仍由 0.5 秒黑幕门禁保护；C# 编译通过，PlayMode/设备待验证 |
| 2026-07-30 | 用户反馈 Reset 后仍有 1～2 秒不可点击 | 仅进关延迟与 Reset 必需 Intro 必须使用可验证的接口边界分离 | 已采用 | 新增 `IDrawLevelEnterOnlyIntroHandler` 与 `PlayResetIntroAsync()`；`DelayAutoBeginDrawHandle` 在 Reset/Replay 跳过，DLevel_002/010 等承担状态恢复的真实入场 Intro 仍重放，黑幕 0.5 秒门禁保留；C# 编译通过，PlayMode/设备待验证 |
| 2026-07-29 | Draw Layer 独占可绘制 | `DrawLineFeature` 统一把非 Draw Layer 的 Collider2D 视为落笔与连续采样障碍 | 已采用 | DLevel_001～010 均显式绑定 Layer 14 DrawLineArea；落笔使用 OverlapCircle，连续采样使用半线宽 CircleCast，命中时保留碰撞前有效线段；C# 编译通过，PlayMode、旧关解法和移动端待验证 |
| 2026-08-03 | 非 Draw Trigger 禁画确定化 | 输入障碍是否命中 Trigger 不应依赖可变的全局 `queriesHitTriggers`；共享查询应显式声明既有“非 Draw 都阻挡”契约 | 局部验证 | `DrawLineFeature` 用同一 `ContactFilter2D(useTriggers=true + ~Draw mask)` 驱动起笔 OverlapCircle 与续画 OverlapCircle/CircleCast；项目原配置本就为 true，Draw Layer Trigger 仍被 mask 排除；Unity 全量编译通过，D025 门 Trigger 实测回归及其它 Draw 关冒烟待验证 |
| 2026-08-03 | DLevel_025 门实体轮廓贴合 | 非 Trigger 实体碰撞应贴合可见门框，避免透明区域让画线产生“隔空碰撞”；成功 Trigger 仍保持独立 | 局部验证 | 门实体由旧 `2.3×2.55` Box 改为 12 点 Polygon，局部 bounds `x[-1.18,1.18] / y[-1.37,1.17]`，顺时针无自交；材质、Actor 排除、成功 Trigger 与画线即解锁均保持；Prefab/YAML 与 Editor 日志检查通过，PlayMode 待验证 |
| 2026-07-30 | DrawLineFeature 默认碰撞体积改进 | 默认完成线使用相邻采样段子 BoxCollider2D 链，根节点保持唯一 Rigidbody2D；碰撞宽度继续由 colliderRadius 负责 | 观察中 | Box 长度额外覆盖一个直径以消除转角接缝；DLevel_010 Composite 主路径不变，DLevel_007 二次中心调整同步平移子 Collider；C# 编译通过，DLevel_001 最长线理论接近 500 子盒，PlayMode/移动端性能待验证 |
| 2026-08-03 | DLevel_019 中点铰接物理杠杆 | 单关机关通过 Static Pivot、限位 Beam 和冻结旋转的独立 Bowl 组成真实 Physics2D 传力链；完成画线事件不主动施力 | 局部验证 | 新增 `DLevel19LeverPhysics`；Beam/Bowl Prefab 初始 Kinematic，根 DLevel10Ctrl 完整放球/双弹后才应用球可玩态参数并切 Dynamic；质量 25 的 Beam 与质量 1.5 的开放 U Bowl 托住本关 Prefab mass 10 的球，普通清线与 Retry 都按 Hold→落座→参数应用→解锁顺序复位；Unity Roslyn 全量 Assembly-CSharp 编译成功（仅既有 warnings），PlayMode/设备待验证；门与胜利不在范围内 |
| 2026-08-03 | DLevel_019 最终横向弹道参数 | 关节限位正负以 Unity jointAngle 实测为准；通过更晚的纯物理释放角、局部球参数和布局避让改善横向速度，不给球追加脚本冲量，也不排除 Ground | 局部验证 | Beam angular damping 1、limits `[-2°,78°]`；ganggan/Alien final 为 `(-4.03,-2.6858387)` / `(-7.15,2.23)`；球仅在可玩态使用 gravity 2.2 / linear damping 0.05，OnDisable 恢复共享初值；Bowl Edge 首尾 `(-0.96,0.32)` / `(0.96,0.05)`、中间五点不变；静态断言、133 个 YAML 锚点检查与 diff check 均通过，正式 PlayMode 待验收 |
| 2026-08-03 | DLevel_037 中心铰链、五秒成功与四向 FallArea | 单关控制器只管理真实物理机关，普通限时成功与越界失败直接复用共享组件，不保留角度/角速度判定 | 局部验证 | `DLevel37Ctrl` 仅配置齿轮/平台/铰链并在首笔后释放平台和 Actor；根节点 `DrawLevelCountdownCtrl(cdTime=5,isSuccess=true)` 第一笔后计时，到点直接成功；Actor 仅本关复用 friction 0.8，gravity 恢复继承 4.5；四边复用共享 FallArea；平台 angular damping 1.5、Use Limits 关闭；PlayMode/设备待验证 |

状态建议使用：`观察中`、`局部验证`、`已采用`、`暂缓`、`已回滚`。

## 7. 影响范围与系统边界

本次只新增开发规则与文档模板：

- `AGENTS.md`
- `DevelopMD/AI规则提升队列.md`
- 本文件及其 Unity `.meta`

未修改 DrawGame 运行时代码、prefab、DB、存档、Luban、资源地址、UI、事件、analytics、
SDK 或 Android/iOS 平台逻辑。

## 8. 验证、风险与回滚

验证：

- 已核对仓库级 `AGENTS.md`、项目宪法和当前 feature plan。
- 已扫描 DrawLevel 与 `DevelopMD/` 的现有 DrawGame 文档。
- 已确认规则入口、单关记录路径、必填内容、优化门禁和回滚方式均有仓库内落点。

剩余风险与未验证项：

- 进行中的 DLevel 第 1/2 关是否已经达到“交付完成”属于 `uncertain`，本次不追认其
  结构记录完整性。
- 模板需要在下一关实际收尾时验证可用性；若字段冗余或缺失，应按实际证据最小调整。

回滚：

1. 删除 `AGENTS.md` 中三条 DrawGame 结构记录与演进规则。
2. 删除本文件及其 `.meta`。
3. 将 `DevelopMD/AI规则提升队列.md` 的 `BP-AI-015` 调整为 `暂不处理`。
4. 已经形成的单关结构记录作为历史事实保留，不自动删除。

## 9. 2026-08-02 可见入场放球装置的位置配置契约

### 9.1 适用范围与结论

- 全量扫描 DrawLevel 后，当前符合“猫/夹子/球整体可见入场，随后释放物理球”的只有
  `DLevel_002`、`DLevel_006`、`DLevel_010`、`DLevel_015`。其它关卡释放箱子、箭、线、武器或气球，
  不套用本契约。
- 四关统一采用：移动实体根 Transform 表示 `FINAL`，其直接子节点
  `entranceStartPoint` 表示 `START`。关卡编辑者拖动根节点时，看到的必须是球最终开始物理
  运动的位置，而不是屏幕外起点。
- 不再使用与移动实体同级的 `endPoint`。起点随实体存放，避免根、起点、终点三套独立对象
  产生配置漂移。

### 9.2 共享边界

- 新增 `DrawEntrancePathView`，挂在移动实体根上。它提供起终点读取契约，并在非 PlayMode
  Scene 视图绘制起终点圆标、虚线路径、方向箭头和起点 Renderer 包围盒占位。
- 组件不启动 Tween、不切刚体、不播放动画、不判断胜负，也不在编辑态改写 Transform。
- 共享 `DrawGame_AlienClipBallRig.prefab` 根节点统一为 `scale=(0.9,0.9,0.9)`。
  为保持缩小前的世界起点与飞行距离，内置
  `entranceStartPoint.localPosition=(-12.022222,0,0)`，经根缩放后的世界偏移仍为 `10.82`。
  DLevel02 与 DLevel06 左侧直接复用；DLevel06 右根和 DLevel15 根使用
  `scale=(-0.9,0.9,0.9)`，自动镜像到右侧起点。DLevel10 的非共享旧装置不受影响。
- 关卡控制器必须在移动根节点之前同时缓存世界 START/FINAL；Awake/Reset 使用缓存 START，
  Intro 使用缓存 FINAL。禁止根移动后重新读取子起点的世界坐标。

### 9.3 行为兼容与位置

| 关卡 | START | FINAL | 既有时序 |
| --- | --- | --- | --- |
| DLevel02 | `(-15.40,2.41,0)` | `(-4.58,2.41,0)` | `0.78s / OutBack(1.9)`，首笔放球 |
| DLevel06 左 | `(-16.72,3.20,0)` | `(-5.90,3.20,0)` | `0.78s / OutBack(1.9)`，首笔双球 |
| DLevel06 右 | `(15.60,3.20,0)` | `(4.78,3.20,0)` | 左右同步，根镜像 |
| DLevel10 | `(-3.21,11.32,0)` | `(-3.52,6.58,0)` | 延迟 `2s`，移动 `1.2s / OutBack`，抵达即放球 |
| DLevel15 | `(16.39,2.41,0)` | `(5.41,2.41,0)` | `0.78s / OutBack(1.9)`，右侧镜像入场、首笔放球 |

- 四个关卡控制器统一改用完整 `DOMove(cachedFinalPosition)`。DLevel02/06/15 当前 Y/Z 相同，
  因此使用水平轨迹；DLevel10 继续保留原完整纵向路径。
- 夹子惯性摆、Spine/Animator、球 Kinematic→Dynamic、释放父级、倒计时、碰撞体、镜像、
  成功/失败与 UI/存档/analytics/平台逻辑均未因此改变。

### 9.4 验证、风险与回滚

- Unity 一次性迁移器对 DLevel02/06/10 起终点执行 `0.001` 世界坐标容差校验并删除旧
  `endPoint` 节点；运行时 C# 编译和相关 PrefabImporter 通过。
- DLevel15 在同一最终位置配置契约下新增右侧镜像实例；Prefab 静态确认共享起点引用、
  DLevel10 本地起点引用、DLevel06 右侧与 DLevel15 的负 X 镜像均有效，未新增 Collider。
- 同日按 Scene 可读性反馈移除 `START / FINAL` 文字，只保留圆标、路径、箭头与起点占位；
  起终点数据和运行时行为不变。
- 完整四关 PlayMode、Reset/Replay、暂停、实际物理成功与 Android/iOS 仍为 `uncertain`。
- 回滚必须把“场外根 + 同级终点 + 旧控制器字段/缓存方向”作为一个整体恢复；只回滚
  Transform 或只删除 `DrawEntrancePathView` 都会造成首帧、Reset 或 Tween 目标错误。

## 10. 2026-08-02 DLevel015 镜像单装置与共享静止基线

### 10.1 单关身份与复用边界

- DLevel015 使用独立 `DLevel15Ctrl` 组件，类继承 `DLevel2Ctrl`，只复用已经验证的单套装置
  Intro、首笔开夹、Reset 与动画流程；Prefab 子根命名为 `level15`，不再长期冒用 Level2
  控制器身份。
- `DLevel2Ctrl` 的局部引用解析优先使用 `DrawAlienClipBallRigView`，不依赖子根名称；基类同时
  增加镜像释放边界。DLevel02 是非镜像实例，玩家行为不变；DLevel06 继续使用独立双装置
  `DLevel6Ctrl`，没有改动其双球成功条件。

### 10.2 共享表现修正

- 球内角色范围经全量扫描确认是 DLevel02、DLevel06、DLevel10、DLevel15。共享 Rig 内
  `Actor/Shadow` 统一 inactive，因此 DLevel02、DLevel06 两套和 DLevel15 自动无影子；
  DLevel10 原本已有本地 inactive override。没有修改通用 `Actor.prefab`，DLevel01/03/04/05/
  07/08/09 等普通角色的影子不受影响。
- Alien `idle01` 的 `bone2` 静止基线为 `+4.22°`。共享 Rig 把
  `clipSwingPivot.localZ` 设为 `-4.22°`，并把 `yellow_ball.localZ` 成对校正为 `180°`；
  夹子和球最终正位，现有入场 Tween 仍相对缓存基准叠加并回到该基准。镜像实例只水平翻转，
  不再额外倾斜。
- DLevel02 与 DLevel15 中由 BoneFollower 编辑态采样产生的 `catch` Position/Rotation instance
  override 已清理，Scene 静止预览统一继承共享姿态；运行时的 bone2 跟随与轻微 idle 微摆保留。

### 10.3 镜像释放与稳定性

- DLevel15 的 Rig 根 `scale=(-0.9,0.9,0.9)`。球脱离挂点并 `SetParent(..., true)` 后，控制器把刚体根的
  负缩放转移到 `payloadContentRoot`，同步镜像 CircleCollider2D offset；随后才切 Dynamic。
  球、Actor 与子碰撞体保持原画面朝向，Rigidbody2D 根使用正缩放。Reset 恢复球、内容根与
  Collider 的缓存初态。
- 成功门、15 秒首笔倒计时、FallArea、画线参数、球/人物同步渐隐、存档、UI、Luban、事件、
  analytics、SDK 与平台分支沿用既有链。本关 `DrawLineFeature.isConfig=false`，因此当前使用
  Prefab 的 50 墨水，而不是表 `20015` 中的 15；这是用户现有配置，本次不静默改口径。
- 静态结构与 Unity 编译完成后仍需完整 PlayMode 观察最终微摆、镜像放球、成功渐隐、失败、
  Reset/Replay；Android/iOS 横屏结果为 `uncertain`。回滚应成组恢复 Pivot/球角度、Shadow
  override、DLevel15 控制器身份及镜像迁移，不能只回滚其中一个变换。

## 11. 2026-08-02 共享猫夹球整体缩放为 0.9

- 需求是把猫、夹子和球作为一个整体缩小到原尺寸的 `90%`，不分别修改子节点，
  避免猫、Spine 挂点、夹子和球发生二次缩放或层级错位。
- 共享 Rig 根从 `(1,1,1)` 改为 `(0.9,0.9,0.9)`。DLevel02、DLevel06 左侧自动继承；
  DLevel06 右侧和 DLevel15 的 X 镜像 override 从 `-1` 改为 `-0.9`，Y/Z 继承 `0.9`。
- 共享起点 local X 从 `-10.82` 反补偿为 `-12.022222`；DLevel15 独立起点从
  `-10.98` 反补偿为 `-12.2`。因此四套实例的世界 START、FINAL、方向和飞行距离不变。
- 整体缩放会连同球的 CircleCollider2D 和 Actor 复合碰撞体缩小 `10%`，质量、重力、
  首笔释放、镜像迁移和成功/失败事件链不变。该条完成时 DLevel10 尚未引用共享 Rig；
  同日后续已按用户需求迁移，见第 13 节。
- 静态校验要求四套根节点最终缩放分别为两套 `(0.9,0.9,0.9)` 和两套
  `(-0.9,0.9,0.9)`，且 Prefab YAML 引用完整。完整 PlayMode 物理、镜像释放与移动端为
  `uncertain`。回滚时必须成组恢复共享根缩放、两个镜像 X override 和两类起点补偿值。

## 12. 2026-08-02 DLevel017 触发式弹簧与四向失败边界

- 常规关 Level032 的实体按钮复制到 Draw 后会阻挡不能跳跃的 Actor；用户删除按钮 Collider、
  Rigidbody2D 和 SpringJoint 后，旧版 DLevel17Ctrl 又因 `springJoint == null` 静默退出，因此
  “能穿过但不发射”。
- 本关使用局部 `DLevel17Ctrl` 的非阻挡方案：Spring 根唯一感应区为 `2.2×1.4`、offsetY
  `-0.5`，并从 Layer 9 改为 Layer 14 `Draw`。DrawLineFeature 的阻挡 mask 排除 Draw，因而
  `Physics2D.queriesHitTriggers=true` 时该 Trigger 也不会阻止落笔或截断连续画线；项目 Physics2D
  矩阵当前全开，Actor 的 Trigger 回调仍有效。`Sp_button_1_02` 不挂 Collider，但恢复只负责表现的 Dynamic Rigidbody2D 与
  SpringJoint2D。Joint 使用 Level032 的 `4Hz / damping 0.1 / distance 0.005`，按钮初始重力为 0，
  释放时切到 1；现有 Level5TrackLine 同步拉伸 LineRenderer。
- Actor 前置子 Collider 为 Untagged，可能比 Player 根 Collider 先进入；因此仅把本关
  OnTrigger2DHandle.targetTag 清空，再由 `DLevel17Ctrl` 按父级 ActorCtrl 精确过滤。Actor
  Rigidbody2D 质量为 3，Prefab 暴露 `rightwardImpulse=12`、`upwardImpulse=72` 与当前用户力度
  `launchStrength=3`，通过
  `AddForce(new Vector2(rightwardImpulse,upwardImpulse)*launchStrength, ForceMode2D.Impulse)+WakeUp()`
  施加真实右上冲量；实际冲量为 `(36,216)`，mass=3 时约产生 `Δv=(12,72)`。
  DLevel001Ctrl 仍每帧驱动 Actor 向右；ActorCtrl 新增默认关闭的同向外部水平动量保留，
  DLevel17 只开启 `0.35s`，避免自动行走在下一帧覆盖真实 X 冲量。本关仍不调用
  `SetMoveInput(0)`、`StopMoving()` 或 `ResumeMoving()`，不存在移动锁；保留开关会在到期、
  Reset、Disable 和协程结束时释放。Actor 重力独立在 `1.1s` 后恢复。共享
  `DrawLevelCtrl.IsOutcomeLocked` 只用于压缩结束、实际发射前的结果门禁；暂停期间机关计时冻结。
  预压/释放时序继续采用 Level032 的 `0.2s` 总延时与 `0.1s` 压缩。
- 本关对嵌套 `DrawGame_DrawLine` 模板根 Rigidbody2D 局部 override
  `DrawStrokeFree.physicsMaterial2D`（摩擦 0、弹性 0、Minimum）。DrawLineFeature 克隆整个模板，
  运行时 Box/Capsule Collider 共用该根刚体，所以每笔完成线都使用零摩擦、零弹性接触。
  共享 `DrawGame_DrawLine.prefab` 和 `DrawStrokeFree` 资源本体未修改，其它关卡不受影响。
- 不直接复用 Level32Ctrl：它依赖 BaseLevelCtrl、常规关 UI block、纵向镜头停止区并修改
  全局 fixedDeltaTime。DLevel017 保持现有固定 GameCamera，未增加 Follow/Cinemachine。
- 固定视野外复用共享 FallArea 四份：左右中心 `x=-18.23/+18.1`，上下中心
  `y=-10.39/+10.39`；实例 Collider 均覆盖为 `10×10`，形成带四角重叠的闭合边界，顶部
  越界继续进入 DrawLevelCtrl 既有失败重建链。
- 本次最终 Assembly-CSharp 完整 C# 编译为 `0 error / 104 warning`，Prefab 166 个 YAML 锚点唯一且
  本地引用完整；已静态确认 Trigger Layer/画线阻挡 mask、全开物理矩阵、右上真实冲量、
  DrawStrokeFree 局部材质 override、无 `Knockback`/直接速度写入、无移动锁，
  以及动量保留释放路径和暂停/发射前结果门禁。
  质量变化会按 `Δv=Impulse/mass` 改变弹道，必须同步重调 X/Y 冲量；当前三倍强度的动态手感、成功门落点、
  Actor/画线滑动手感、画线/地面接触、FallArea 高速穿越、Reset/Replay 和 Android/iOS 仍为
  `uncertain`。物理发射和计时仍保持为 Level17 局部；共享 ActorCtrl 只增加默认关闭、
  仅本关调用的同向外部动量保留边界。

## 13. 2026-08-02 DLevel010 共享 Rig 的上方入场与落地双弹

- DLevel010 从本关内嵌旧猫、Animator 夹子和黄球迁移为一套
  `DrawGame_AlienClipBallRig.prefab` 嵌套实例；共享 View 提供猫 Spine、BoneFollower、夹子 Spine、
  蓝球刚体和 payload 引用，旧资源与控制器字段从本关移除。
- DLevel10Ctrl 局部增加 `FromLeft / FromRight / FromTop` 入场状态，本关使用 `FromTop`；
  起点距离来自 `DrawEntrancePathView`，运行时沿世界 Y 正方向建立纯纵向起点，整体使用
  `Ease.OutBack` 越过最终位置后回正。
- 夹子在 Intro 初态继承共享 `clip_close` 并夹紧球；抵达后播放 `clip_open`，球再脱离动画层级
  并沿用共享镜像迁移边界进入 Dynamic。稳定落地后临时 Kinematic，播放两次衰减位移与
  payload 拉伸/压缩，结束后恢复 Dynamic，Intro 才返回并开放画线。
- 该实现保持在 DLevel10Ctrl，不修改 DLevel002/006/015 的左右入场、首笔释放或胜负流程；
  不新增 DB、配置、UI、资源加载、全局事件、analytics 或平台分支。
- Unity 编译刷新和 Prefab 保存已完成；完整 PlayMode、Replay、可解性与 Android/iOS 为
  `uncertain`。详细验证和回滚见 Level10 本地功能风险文档与脚本结构记录。

## 14. 2026-08-02 DLevel010 开场退场反馈与共享 Actor 碰撞边界

- DLevel010 可见 Intro 的额外等待从 `2s` 降为 `0s`；夹子首帧与入场继承共享 `clip_close`，
  抵达后播放 `clip_open` 放球。球释放后猫和已打开的夹子以 `0.8s / InBack(1.9)` 沿上方
  入场路径退回起点。
- 退场与球的落地双弹并行，Draw Intro 等待两者都结束后才开放画线；球在退场前已脱离 Rig 并
  挂到关卡根，因此不会跟随猫夹离场。该退场差异只存在于 DLevel10Ctrl。
- 用户澄清要求是移除 Actor 内的 PolygonCollider2D，而不是删除 Actor。共享 Rig 保留完整
  Actor prefab、ActorCtrl、Spine、排序和成功身份，并在嵌套实例层移除 PolygonCollider2D；
  既有 Actor Rigidbody2D removed override 同样保留。
- DLevel002/006/010/015 的夹子球统一由 yellow_ball 根 Rigidbody2D 模拟；球根
  CircleCollider2D 与 Actor 既有子 BoxCollider2D 保留，仅角色轮廓 PolygonCollider2D 退出
  compound collider。普通 Actor 关卡和通用 Actor.prefab 不受影响；02/10/15 成功门仍通过
  ActorCtrl 结算，06 双球仍用球根 OnTriggerSuccessCtrl。
- Prefab 静态结构已确认 Actor source 和双 removed component 引用存在、YAML 锚点唯一；Unity
  完整 PlayMode、四关成功/失败/Replay 与 Android/iOS 为 `uncertain`。

## 14. 2026-08-02 双球相遇关的统一成功演出契约

### 14.1 适用范围与组件边界

- DLevel006 以及以后“两个物理球相碰即成功”的 Draw 关，统一使用
  `DrawTwoBallReunionSuccessHandle`，不再在碰撞回调中直接调用 `TryResolve(Success)`。
- 组件挂在其中一个球的 Rigidbody2D 根上，必须绑定配对球、同关 `DrawLevelCtrl` 和本关
  GameCamera；未来同场超过两个球时禁止自动推断配对，必须显式配置。
- 该组件只负责相遇判定后的冻结、爱心、相机与延迟提交；入场、开夹、放球、镜像和 Reset
  初始位姿仍由各 DLevel 控制器负责，不得塞入共享猫夹球 View。

### 14.2 原子结果与演出时序

```text
首次配对球 Collision2D
  -> TryBeginSuccessPerformance（关闭输入/界面切换，拒绝其它结果）
  -> 两球清速度与角速度，Kinematic + FreezeAll
  -> 接触点上方爱心粒子 + 相机按原视野比例轻微聚焦
  -> 停留并恢复相机
  -> CompleteSuccessPerformance
  -> 原 DrawLevelCtrl.Resolved / DrawPlayCtrl 胜利链
```

- “贴住”定义为保持首次物理接触姿态，不创建 Joint、不互设父级、不强制重算球心距离；因此
  空中接触后也不会继续下落或弹开。
- `DrawLevelCtrl.TryResolve` 在 Success Performance 占用期间拒绝外部结果，避免倒计时或失败区
  在演出停留期抢先结算。成功门同步复用 Begin/Complete 边界，原结果事件与 UI 出口不变。
- Draw Pause 不保证 `timeScale=0`；演出组件必须显式 Pause/Play Tween 与粒子。Reset、Disable、
  Destroy 必须取消 Sequence、清理粒子、恢复相机，并解除未完成的成功占用。

### 14.3 DLevel006 当前参数、验证与回滚

- DLevel006 右球是判定宿主，左球为显式配对；爱心复用 `Bubble.prefab` 的上冒/淡出参数，
  运行时生成心形纹理，Sorting Order `340`。总时长 `1.8s`，聚焦 `0.28s`，正交 Size 比例
  `0.91`，位置混合 `0.75`，结算前恢复相机。
- Unity 6.0.59f2 Tundra 增量编译和 Prefab 导入通过，旧 `OnTriggerSuccessCtrl` 已从 DLevel006
  实例移除。完整 PlayMode 的空中冻结、爱心视觉、镜头边缘、暂停/Replay 和移动端仍为
  `uncertain`。
- 回滚该契约时，恢复关卡球上的直接成功组件，并成组移除通用演出组件与 DrawLevelCtrl 的
  Begin/Complete/Cancel 门禁；不涉及 DB、Luban、存档、星级、analytics 或平台数据迁移。

## 15. 2026-08-02 重开跳过进场动画并直接应用可玩终态

### 15.1 需求、基线与统一契约

- 需求覆盖失败自动重来、HUD 手动重来和胜利页 Replay：同一关卡重建时不再播放进场动画；
  首次选择关卡和过关进入下一关仍完整播放原 Intro。
- 修改前三种重开都会销毁旧 DLevel、实例化新 Prefab，再调用 `PlayResetIntroAsync()` 重播未标记
  EnterOnly 的可见 Intro。只跳过 Intro 会让 DLevel002/006/015 留在 START，让 DLevel010/018
  留在场外且球未释放，因此不能只增加跳过标记。
- `DrawPlayCtrl.OpenDrawLevelAsync(...)` 现在显式接收 `isRetry`；重开在 GameplayPanel 全黑遮罩下
  调用 `PlayResetIntroAsync()`，该入口只同步应用 `IDrawLevelRetryIntroStateHandler`，禁止 Tween、
  等待和普通 Intro。遮罩随后按既有 `0.5s` 门禁淡入并开放画线。
- `IDrawLevelVisibleIntroHandler` 编译期同时要求直接完成态契约；未来新增可见 Intro 时必须描述
  首次动画结束后的可玩状态。普通无状态 Intro 和 `IDrawLevelEnterOnlyIntroHandler` 在重开时跳过。

### 15.2 当前关卡完成态矩阵

| 关卡 | 首次进入 | 失败/手动重来/Replay |
| --- | --- | --- |
| DLevel001/003/004/005/007/008/009/016/017 | 保留既有进关等待 | 跳过等待，无额外状态 |
| DLevel002、DLevel015 | 单装置从 START 到 FINAL，首笔后放球 | 装置直接在 FINAL；闭夹、猫 idle、球 Kinematic 并保持夹持 |
| DLevel006 | 左右装置分别入场，首笔后放两球 | 两装置直接在各自 FINAL；闭夹、两猫 idle、两球 Kinematic 并保持夹持 |
| DLevel010、DLevel018 | 入场后自动开夹放球、装置退场、球落地双弹 | 夹子已开且装置已退场；球直接位于释放点下方首个实体碰撞面并恢复 Dynamic |

- DLevel015 继承 DLevel2Ctrl 的镜像安全恢复；DLevel018 直接复用 DLevel10Ctrl，因此无需另建
  重开脚本。DLevel010/018 的直接落点由复合 Rigidbody2D 向下 Cast 获取，只接受当前新关卡
  根内无 Dynamic 刚体的实体面；命中后球保持 Dynamic 但先休眠，避免黑幕淡入期间从圆面或
  斜面滚走，后续物理接触会正常唤醒。找不到时记录配置错误并保持 Kinematic，禁止在重开
  门禁期继续下落或抢先触发失败。
- DLevel2Ctrl 与 DLevel6Ctrl 的 `OnDrawAttemptReset()` 复用同一 FINAL 完成态，防止未来启用
  原实例 Reset 时重新回到 START。

### 15.3 影响、验证、风险与回滚

- 代码影响：`DrawPlayCtrl` 的首次/重开上下文、`DrawLevelCtrl` 的 Intro 分发、
  `DrawLevelRuntimeTypes` 契约，以及 DLevel2/6/10 控制器；DLevel15/18 通过继承或复用同步生效。
  本次没有修改 Prefab、Luban/DB、资源地址、存档、星级、UI 事件、analytics、广告/IAP、SDK
  或平台分支；Reset 仍使用原整关重建与黑幕流程。
- 旧 DLevel 的 `Destroy` 会延迟到帧末，清理流程因此先 `SetActive(false)`，确保同帧重建的新关卡
  不会让落点 Cast 命中旧球、旧线或旧机关；销毁与资源释放所有权保持不变。
- 静态扫描确认当前所有 Intro 实现者均被覆盖，DLevel10Ctrl 的 Prefab 使用者只有 010/018；
  Unity 6.0.59f2 当前 Assembly-CSharp Roslyn 响应文件全量编译为 `0 error`。
- 静态确认 DLevel018 的释放路径下方存在当前关卡根内的实体圆形承接点；Unity PlayMode 的
  DLevel010/018 实际 Cast 接触位置与休眠唤醒、重开首个可见帧、连续失败/手动重来/Replay，
  以及 Android/iOS 物理一致性仍为 `uncertain`。若落点场景改为移动或触发器平台，应改为关卡
  显式落点或扩展过滤规则。
- 回滚时成组恢复 `OpenDrawLevelAsync` 的旧重开分支、移除 Retry 完成态契约和三关控制器实现；
  只回滚其中一层会造成重播 Intro 或对象停在 START/未释放状态。无需数据迁移。

## 16. 2026-08-02 球载体进入 FallArea 的玩家身份解析

### 16.1 问题、根因与统一边界

- DLevel002/006/010/015/018 的共享猫夹球使用复合刚体：`yellow_ball` 根节点持有
  `Rigidbody2D + CircleCollider2D`，玩家 `ActorCtrl` 位于子层级。旧 `FailAreaTriggerHandle`
  要求进入 Trigger 的同一 GameObject 同时带 `Player` Tag 和 `ActorCtrl`，因此球根落入失败区时
  不会派发 `OnTriggerFailAreaEvent`。
- 普通角色仍按 Collider 自身/父级解析 `ActorCtrl` 并校验 `Player` Tag。复合载体只由刚体根
  Collider 代表整套刚体触发，并在该刚体子树中寻找带 `Player` Tag、且最近父级 Rigidbody2D
  正是当前 `attachedRigidbody` 的 Actor；不依赖球根 Tag，因此兼容 DLevel006 使用 `tools` Tag
  的镜像球，同时不会把普通敌方 Actor 误判为玩家。
- 复合 Actor 子 Collider 不重复派发，由球根 CircleCollider 统一代表。该约束依赖共享 Rig
  保留根 CircleCollider；以后若删除或禁用根 Collider，必须同步调整身份解析契约。

### 16.2 结果链、验证、风险与回滚

- 身份解析成功后仍复用原 `OnTriggerFailAreaEvent -> DrawLevelCtrl.OnTriggerFailArea()`：播放失败音效，
  等待 `0.2s` 后提交失败并重建关卡；结合第 15 节，重建后不重播 Intro。FallArea 原设计不调用
  `ActorCtrl.Die()`，本次没有新增死亡动画。
- 本次只修改共享 `FailAreaTriggerHandle`。未修改 FallArea/关卡 Prefab、Physics2D Layer Matrix、
  球物理参数、倒计时、成功链、Luban/DB、存档、UI、资源、analytics、SDK 或平台分支；普通
  Player 的旧触发边界保持不变。
- 静态确认五个球关卡均引用共享 FallArea 与共享复合球结构；Assembly-CSharp Roslyn 全量编译
  为 `0 error`。Unity PlayMode 中 DLevel006 左右球分别/同时越界、五关重开后越界、普通 Draw
  玩家与敌人负例、同一物理步成功/失败竞争和 Android/iOS 表现仍为 `uncertain`。
- 回滚时恢复 `FailAreaTriggerHandle` 对碰撞 GameObject 的精确 `Player Tag + ActorCtrl` 查询即可；
  无 Prefab 或数据迁移。回滚后共享球关卡会重新无法触发 FallArea。

## 17. 2026-08-03 DLevel011 双 Actor 行走与 Key 双 Trigger 隔离
## 17. 2026-08-03 DLevel020 首笔放球与水体真实浮力

- DLevel011 保留既有 `DLevel001Ctrl` 驱动 Player；新增局部 `DLevel11Ctrl` 同样消费
  `IDrawStrokeCompletedHandler`，在第一笔完成后按 Prefab 的 `0.5s` 延迟驱动 enemy。
  两个 Handler 只绑定各自 Actor，不新增共享 Actor API、全局事件或 GamePlayPanel 修改。
- enemy 移动继续复用 `ActorCtrl.SetMoveInput(1f) -> OnUpdate()`，暂停、死亡、结算、Reset 和 Disable
  均有本地停止边界。enemy 实例删除会破坏该调用链的 `spineCtrl=null`、`pushCollider=null`、
  `whatIsGround=0` 三个 override，恢复继承共享 Actor Prefab；共享脚本和 Prefab 本体不改。
- Key 原 `Player -> TriggerExecutor.OnTriggerExecute` Trigger 内容及实例 override 保持不变；Level11
  只在 Key 实例额外挂一个过滤 `enemy` 的 `OnTrigger2DHandle`。enemy 命中后停止 Key 悬浮 Tween，
  启用已指向 enemy 的 FollowTarget；不会进入玩家开门、跳跃或销毁执行链。
- 结构复盘结论：共享 Prefab 上已有 Trigger 承担稳定业务时，单关需要另一标签语义可优先在关卡
  实例额外挂局部 Trigger，避免改写原 UnityEvent；当前只有 DLevel011 的静态和编译证据，状态为
  `观察中`，不升级共享组件或批量迁移其它关卡。
- Assembly-CSharp Roslyn 全量编译 `0 error`；Prefab 最终 140 个 YAML 锚点唯一、140 个本地引用完整，
  Unity 导入通过。两个 Actor 实际节奏、玩家取钥匙回归、同物理步竞争、完整胜负/Reset 和
  Android/iOS 仍为 `uncertain`。详细证据与回滚见 Level11 本地功能风险文档和脚本结构记录。
- DLevel020 原 Prefab 误挂不接收 StrokeCompleted 的 DLevel10Ctrl，且 `rig` 为空、层级不符合
  其兜底路径，导致夹子和 Kinematic 球始终不释放。本关新增轻量 `DLevel20Ctrl : DLevel2Ctrl`，
  复用 DLevel002 的横向可见 Intro、首笔开夹放球和重开直接 FINAL 闭夹持球状态；未修改任何
  已有控制器或共享 Rig。
- 本关水体启用已有 `BuoyancyEffector2D`。Effector 表面与可见水面统一为局部 Y `12.5`
  （世界 Y `-2.59`），Density `8`、Linear Drag `1`，Collider Mask 仅 Layer 11 蓝球。球以
  重力获得的向下速度入水后继续下冲，再由连续浮力/阻力反向浮起，不使用 Tween 或定时轨迹。
- 首轮运行反馈显示 Drag `5` 会让球在入水后过早停止，而从平台右缘到成功门
  仍需约 10.5 世界单位的水平移动。本轮只降低本关 Linear Drag，不添加 Flow，保留玩家斜线
  碰撞产生的真实惯性，不把解法改成定向水流推球。
- Layer 8 的 Dynamic 画线不受浮力，保留 Gravity Scale 5 和长度质量；Physics2D 仍允许线球
  碰撞，因此线可用真实重量压下浮球。本关成功门、FallArea、20 秒失败倒计时、成功球体同步
  渐隐、DB/存档/UI/analytics/SDK 链全部沿用既有边界。
- 结构决策：首笔猫夹球复用已有单装置生命周期；水力参数继续留在单关 Prefab，不在只有一个
  水球关证据时新增共享水系统。PrefabImporter、YAML 引用检查和 Assembly-CSharp 全量编译
  通过；下冲深度、浮起时间、压球可解性、暂停/重开和移动端均为 `uncertain`。

## 17. 2026-08-03 DLevel019 中点铰接物理杠杆与水平小碗

### 17.1 需求、基线与局部边界

- `DLevel_019/ganggan` 原来只有 SortingGroup 和三个纯 Sprite 子节点，没有任何 Rigidbody2D、
  Collider2D 或 Joint2D，画线无法撞杆，小碗也不能托住共享装置释放的球。
- 本关新增局部 `DLevel19LeverPhysics`，挂在 ganggan 上并显式绑定
  `Sp_hx_gg_01`（Pivot）、`Sp_hx_bz_01`（Beam）、`Sp_hx_w_01`（Bowl）。脚本只建立物理拓扑、
  应用参数和复位，不引用门、不判断进门、不调用结果 API。
- 共享猫夹球 Rig 与 DLevel10Ctrl 入场/放球继续复用；本关球 Prefab override mass 10，
  DrawLineFeature `strokeMass=100`。DLevel19LeverPhysics 不写球质量，只管理可玩态重力/阻尼。
  倒计时、FallArea、成功门和全局 Physics2D 配置均不改。

### 17.2 物理拓扑与真实传力

- Pivot 为 Static Rigidbody2D 且无 Collider，只提供不会漂移的 connectedBody；复位时不再向
  Static body 写线速度或角速度，消除 Unity 警告。
- Beam 参数为 mass 25、linear damping 0.15、angular damping 1、gravity 1、Interpolate、
  Continuous；非 Trigger Box 为 `0.27×5.9`、offset `(0,-0.35)`，复用 DrawStrokeStable。
  Hinge 连接 Pivot，关闭电机与连接体碰撞，当前限位 `[-2°,78°]`。
- Bowl 参数为 mass 1.5、linear damping 0.2、gravity 1、Interpolate、Continuous、FreezeRotation。
  七点开放 U 形 Edge 的首尾为 `(-0.96,0.32)` / `(0.96,0.05)`，中间五点不变；radius 0.055、
  DrawStrokeStable 与 Bowl Hinge 保持。
- Beam/Bowl 在 Prefab 中初始序列化为 Kinematic。Awake 建好参数与 Joint 后继续 Hold；
  DrawLevelCtrl 串行等待根 DLevel10Ctrl 完成入场、球落碗和双弹，随后 ganggan 子 Handler 才应用
  球 `gravityScale=2.2 / linearDamping=0.05` 并切 Dynamic，入场落球节奏不受局部参数影响。
- Unity 6 最小探针确认顺时针转动对应正 jointAngle；当前 Upper 78°，最终实际弹道待 PlayMode 验证。
- 继续保留 Ground 碰撞和 exclude mask 0。ganggan 当前为 `(-4.03,-2.6858387)`，Alien final 当前为
  `(-7.15,2.23)`；二者相对原始位置 X 同量右移 0.80，子节点、Joint 与 Layer Matrix 不变。
- OnDrawStrokeCompleted 不施加 AddForce/AddTorque，只做异常误锁的幂等解锁。画线仍必须实际撞击
  Beam，冲量才会沿 Pivot/Beam/Bowl 的 Physics2D 链传给球。
- Retry 和普通 Draw Attempt Reset 统一使用 `Hold 杠杆 → 球 Kinematic 落座 → 应用球可玩态参数并让
  杠杆 Dynamic → 球 Dynamic/清零/Sleep`。OnDisable 恢复捕获的共享球 gravityScale/linearDamping；
  这也补足 DLevel10Ctrl 通用 Retry Cast 会忽略非 Static Bowl 的边界。

### 17.3 生命周期、验证、风险与回滚

- Unity 自动重编与独立 Unity Roslyn 全量 Assembly-CSharp 编译均成功，仅有既有 warnings；
  本轮没有编译错误。
- 关键字段/时序静态断言通过，Prefab 133 个 YAML 锚点唯一；`git diff --check` 通过，只有工作区既有的 LF/CRLF 提示。
  正式 PlayMode 尚未执行，放球前静止、串行参数应用与解锁、78° 触地范围、不同线形/撞击点、
  球实际弹道/进门、Edge 穿透、Joint 抖动、普通清线回球、Retry/Reset/Replay 和 Android/iOS
  均为 `uncertain`。
- 只回滚最终调参应成组恢复修改前的 Upper、Beam 角阻尼、ganggan/Alien X、Bowl Edge 首尾点，
  并移除可玩态球重力/阻尼覆盖与 OnDisable 恢复链；不要只回滚其中一项。完整回滚需移除 ganggan 组件以及 Prefab 已序列化的
  三套 Rigidbody/Collider/Joint；共享 Rig、门、Draw 系统、配置、存档、UI 和平台数据无需迁移。

## 18. 2026-08-03 DLevel013 高弹球与上方进筐

- DLevel013 原为 DLevel015 复制结构，仍挂 `DLevel15Ctrl`。两个篮沿 BoxCollider2D 已由用户绑定为
  非 Trigger 实体，但没有进筐方向判定或成功提交。
- 新增局部 `DLevel13Ctrl : DLevel2Ctrl`，保留猫夹球入场/首笔释放/Retry，并由两篮沿 Bounds
  计算开口和上/下虚拟平面。只有球心向下依次扫过两平面且 X 位于开口内才成功；
  下方反弹、侧向移入、碰框或中途离开均不提交。
- 本关局部球材质为 friction `0.05` / bounce `0.98` / Multiply，球 Linear Damping 为 `0.03`。
  根据试玩反馈，球 Gravity Scale 从 `1` 提到 `1.5`；画线材质改为 friction `0.35` /
  bounce `0.98` / Minimum，普通地面与画线当前均按约 `0.98` 回弹试玩。不使用碰撞脚本强改速度，
  共享 Rig、共享 DrawLine 和既有 Bouncy 材质均不改。
- `DrawLevelCtrl.TryTriggerSuccessWithoutDoor()` 为篮筐复用 Actor 身份校验和 Actor/球渐隐，但不要求
  Spine 成功门。DLevel013 成功时立即锁定结果但不再清速或切 Kinematic，球继续真实物理；
  `0.35s` 后才开始 Actor/球渐隐并提交胜利。
- 当前静态开口约 `2.141`，球直径约 `1.526`，单边物理余量约 `0.307`。Assembly-CSharp 全量
  编译为 `0 error`；高弹节奏、擦框/空心、成功延迟渐隐、倒计时、Reset 和移动端仍为 `uncertain`。


## 19. 2026-08-03 DrawLineFeature 多提示对象兼容边界

- 最终结构决策：共享 `DrawLineFeature` 只保留 `tipsObjects` 列表，不保留旧单字段或额外列表形成双入口。
  20 个现有 DLevel 的原对象已迁入列表第一项，引用本身不丢失。
- `ShowHint(bool)` 统一控制列表；Attempt Reset 必须按对象分别恢复初始化时的
  `activeSelf`，不能用单一布尔值覆盖全部提示。
- 自动绑定只有在列表没有有效引用时才收集层级内全部标准命名提示对象，避免覆盖人工配置。
- DLevel_009 与 DLevel_011 当前各有两个不同提示引用；其余现有 DLevel 为单项列表。
- 结构复盘结论：这是共享组件内向后兼容、无新事件/资源/逐帧成本的稳定扩展，可以保留；关卡仍应
  显式绑定新增提示对象。Prefab 迁移静态检查通过；Assembly-CSharp rsp 显式补入新
  `TagPressButton.cs` 后全量 Roslyn 编译为 `0 error`。PlayMode 和移动端表现仍为 `uncertain`。

## 20. 2026-08-03 DLevel_011 标签按钮驱动 WaterSplash

- DLevel_011 的按钮原位从空 `TurntableButton` 替换为局部 `TagPressButton : PressButtonBase`，保留按钮
  Transform、Collider、下压距离、缓动和音效；当前 Tag 配置为 `tools`，与画线模板 Tag 一致。
- TagPressButton 用 Collider 集合维护占用，首个匹配 Collider 进入才按下、最后一个离开才释放，避免
  多 Collider Actor 提前触发 Exit。只有本关使用，当前不修改共享 PressButtonBase。
- `DLevel011Ctrl` 只负责把局部按钮 Action 映射到既有 WaterSplash Animator 的 `long` / `stay_long`，
  复用 `water_2 -> water_2_splash_idle -> water_2_hide` 状态链；不新增全局事件、资源加载或逐帧扫描。
- Unity Roslyn 全量编译 `0 error`，Prefab 脚本 GUID 与两个显式引用静态校验通过；PlayMode、快速进出、
  失败/Replay 和 Android/iOS 仍为 `uncertain`。该模式保留为局部复用候选，出现第二个完整验收关卡前
  不上提共享目录或改造既有按钮。

## 21. 2026-08-03 DLevel_011 Draw 层最大可行走坡度

- 原因基线：DLevel001Ctrl 首笔后持续给 Actor 写正向输入；共享 ActorCtrl 每帧直接写 X 速度，Ground
  检测只判断命中而不读取法线角度，因此角色能在 Physics2D 接触约束下沿高倾角画线上行。
- 本关按单关 Actor 边界新增 `DLevel11ActorCtrl : ActorCtrl`。它从现有 Rigidbody2D 接触点读取 Draw
  层支撑法线；坡度大于 prefab 配置的 50° 且输入朝上坡方向时，临时把本帧 MoveSpeed 设为 0，再继续
  `ActorCtrl.OnUpdate()`。移动输入不清除，因此角色原地继续行走动画；重力、Y 速度与下坡移动保持。
- 接触数组固定为 8 个且 ContactFilter 缓存，无逐帧集合分配；只检查 Layer 14 Draw，不改变普通地面、
  机关、Physics2D Matrix、物理材质或其它关卡 Actor。
- 结构复盘：当前只在 DLevel_011 出现“自动前进角色不可攀爬玩家陡线”的明确需求，局部子类已经满足，
  不上提共享 ActorCtrl。若多个已验收关卡重复出现，并覆盖全部受影响关卡回归，再评估共享坡度能力。
- Unity 6000.0.59f2 Assembly-CSharp 全量编译 0 error，PrefabImporter 成功；50° 手感、首次接触一步延迟、
  折线多法线、Reset/Replay 和 Android/iOS 均为 `uncertain`。完整风险与回滚见 Level11 本地文档。
### 18.1 进球点共享烟花补充

- DLevel013 在成功锁占用后，把下计分平面的精确插值穿越点传给共享
  `DrawGame_BasketSuccessFirework.prefab`。烟花不取篮筐中心，也不跟随成功后继续运动的球。
- 共享烟花复用常规成功门的 `ConfettiBlastRainbow` nested override：根系统非循环，根与子系统
  关闭 Play On Awake，保留 Burst `60`、尺寸和向上受力；wrapper 根使用 SceneLayer Order `220`，
  高于球 `150` 与篮筐前景 `151`。Reset/Disable 会 Stop/Clear 并销毁运行时实例。
- 当前 D013 篮筐经美术调整后世界开口约 `2.141`，球直径约 `1.526`；旧记录中的 `1.670`
  已不再代表当前工作区 Prefab。
- `uncertain`：D013 既有球模拟、FixedUpdate 进筐判定、无门成功等待/渐隐及新增烟花均未统一
  响应 `IsPaused`；暂停中仍可能推进物理、判定或演出，本轮不扩大修改该共享暂停边界。

## 19. 2026-08-03 DLevel014 纯物理投篮与进筐

- DLevel014 原误挂引用全空的 `DLevel15Ctrl`，篮球从开局即为 Dynamic，第一笔没有发射回调，
  两个实体篮沿也没有成功判定。现改为本关独立 `DLevel14Ctrl`。
- 生命周期参照 DLevel008：Awake/Reset 让篮球 Kinematic、零速度并恢复初始位姿；只有第一笔有效线
  完成时切 Dynamic，然后按世界方向 `(1,0.75)` 施加一次 `14` 的线性 Impulse 和 `-0.35`
  的旋转 Impulse。之后飞行、重力、撞线、撞投球器和撞框都不再由脚本写速度或位置。
- 篮球使用本关局部 PhysicsMaterial2D：friction `0.1`、bounce `0.85`、Maximum Combine，并改为
  Continuous；共享 DrawLine 和 Physics2D 全局配置不改。
- 篮筐前/后景 Scale 与两侧 BoxCollider 几何同步当前 D013，D014 整体高度保持原布局。用户手调
  投球器的位置、旋转、新 Sprite 与 Collider 变化原样保留。
- 进筐判定与 D013 同型：球心必须向下依次扫过篮沿上/下平面且 X 位于开口内。D014 没有 Actor，
  因此成功直接使用 `TryBeginSuccessPerformance()` 占用结果，在下平面插值点播放共享烟花；篮球
  保持物理 `0.35s`，再渐隐 `0.4s` 后由 `CompleteSuccessPerformance()` 提交胜利。
- D014 篮球也没有 ActorCtrl，不能依赖通用 FallArea 的角色过滤；控制器显式绑定本关 FallArea，
  球心进入其 Bounds 后播放 `failed`，等待 `0.2s` 再走 `TryFailAndReset()`；无 Actor 死亡动画，
  且该短等待与通用 FallArea 一样不随 Pause 冻结。15 秒超时链保持不变。
- D014 Pause 会关闭已发射篮球模拟并暂停烟花；恢复时保留刚体速度。成功延迟和渐隐只累计
  `IsPaused=false` 的游戏时间，避免暂停界面下继续完成结算。
- 独立 Unity Roslyn Assembly-CSharp 编译为 `0 error`，只有既有 warnings。Prefab/YAML 引用静态
  检查后仍需完整 PlayMode 验证默认弹道是否飞过、画线可解性、0.85 弹性、擦框判定、烟花层级、
  FallArea、Reset/Replay 和 Android/iOS；这些当前均为 `uncertain`。

## 20. 2026-08-03 DLevel025 柱门平衡与斜线推倒

### 20.1 基线与组件边界

## 22. 2026-08-03 DLevel_012 距离触发逃跑门与双 Layer 碰撞隔离

- DLevel_012 保留 `DLevel001Ctrl` 在第一笔后驱动玩家；新增根控制器 `DLevel012Ctrl`，当玩家与
  DoorEnemy 的世界 X 轴绝对距离不大于 2 时，复用 DoorEnemy 的 `SetMoveInput(1)`、
  `TriggerJump()` 与 `OnUpdate()` 向右跳出。触发前也刷新 DoorEnemy 的 Ground 状态，避免首次跳跃被忽略。
- DoorEnemy 原 Layer 11 `LayerCollisionDetector` 已绑定玩家碰门的 `BaseItemExecutor` 业务链，本关不扩大
  该 Mask。新增第二个只监听 Layer 14 Draw 的检测器；首次命中后只清零水平输入与 X 速度，保留
  Dynamic 刚体、Y 速度和重力。
- 暂停、死亡、结算和 Disable 停止关卡驱动；Attempt Reset 恢复 DoorEnemy 初始局部 Transform、线速度、
  角速度及本地运行标记。不修改共享 Actor、DoorEnemy、DrawLevelCtrl、GamePlayPanel 或全局事件。
- Luban `LevelData` 新增 `20012 / DLevel_012 / 跳水`，墨水与评级为 `15 / 0 / 2 / 5`；
  `LevelSort` 的 Draw 第 18 项及运行时 JSON 同步指向 `20012`。
- 结构复盘结论：同一刚体已有稳定碰撞业务而单关需要另一 Layer 语义时，可以在关卡实例追加第二个
  检测器隔离 Mask 与事件所有权；当前只有 DLevel_012 的静态证据，状态为观察中，不上提新共享抽象。
- Unity 6000.0.59f2 自动完整编译和 DLevel_012 PrefabImporter 通过，161 个 YAML 锚点唯一；实际触发
  手感、跳跃弧线、不同 Draw 停靠、原门业务竞争、Reset/Replay 和 Android/iOS 均为 `uncertain`；
  两个配置源表的 DLevel12 局部版本已渲染并确认无公式错误。

## 23. 2026-08-03 DLevel_021 双间歇泉单周期控制

- DLevel_021 复用 `WaterSplashCtrl` 的 3 秒待机、3 秒播放节奏和既有 Animator 参数，但只保留一个
  `Idle → Playing` 循环与一个 `_cycleTimer`。
- 周期通过既有 `IDrawStrokeCompletedHandler` 在第一笔有效画线完成后从 0 启动；进入关卡到首笔完成前
  不累计喷水时间，后续笔画不重新计时，尝试重置后恢复门禁。
- 每轮播放开始时读取一次左右 TouchArea：无接触播放两侧 short；单侧接触播放另一侧 water_2；
  双侧接触本轮不播放。接触变化本身不触发、不停止，也不重置周期。
- 控制器显式绑定两个 Trigger 和两个 Animator，不增加碰撞转发、全局事件、场景搜索或实例化；
  当前不提取共享控制器，也不修改主线 `WaterSplashCtrl`。
- 静态验证、编译证据和未验证风险见 Level21 本地结构与风险文档。

## 24. 2026-08-03 DLevel_022 完成线段车身与双轮加速行驶

- DLevel_022 新增局部 `DLevel022Ctrl : IDrawStrokeCompletedHandler`。第一笔完成后通过
  `DrawLineFeature.TryGetCompletedStrokeObject` 取得当前运行时线段；共享 DrawLineFeature 已先完成
  几何中心归位。本关参考 DLevel7，暂停该线段 Physics2D 模拟后把运行时 Transform 与刚体中心直接
  设置到 `Scene/startPoint`，再恢复 Dynamic 模拟，不重复改写 LineRenderer 或子 Collider，也不
  接管线段销毁权。
- Dynamic 车身和两只 Dynamic 轮体每个 FixedUpdate 只覆盖横向 `linearVelocity.x`，保留重力、Y 速度
  和碰撞响应；速度从 0 按 prefab 当前 `acceleration=1` 增加，到 `maxForwardSpeed=4` 后匀速。
  两轮 `angularVelocity` 按 CircleCollider2D 世界半径换算，不再移动 wheelsRoot 或直接旋转 Transform。
- Actor prefab 默认 Dynamic，但 DLevel_022 实例已有 Kinematic override；DLevel022Ctrl 在 Awake/Reset
  显式保持 Kinematic 和零速度，第一笔中先定位车身、再独立解析 Actor 并切为 Dynamic。Actor 或车轮
  引用缺失不会撤销或阻止已完成线段定位；车轮完整时才开始持续行驶。Disable 恢复 Kinematic，不修改
  共享 ActorCtrl 或 Actor prefab。
- 暂停时不推进；结算、Disable 和 Attempt Reset 停止本地驱动，Attempt Reset 恢复轮组位置与两轮
  旋转。只订阅同关卡 `DrawLevelCtrl.Resolved`，不新增全局事件，不修改共享 Actor、Draw、UI、存档、
  配置、analytics 或平台分支。
- 结构复盘：DLevel007 的完成线段接管能力可以作为只读访问基线，但第 22 关的 Dynamic 车身定位、
  三刚体横向速度驱动和双轮角速度换算仍是单关机制；当前不上提共享车辆或扩展 DrawStrokeResult。
  三个独立刚体无 Joint2D，受不同碰撞约束时仍可能分离，先作为本关风险验证。
- Unity 6000.0.59f2 Assembly-CSharp 独立全量 Roslyn 编译 `0 error`，PrefabImporter 成功，169 个
  YAML anchor 唯一且本地引用完整。任意车身形状承载 Actor、Dynamic 轮体碰撞、速度手感、完整
  胜负/Reset/Replay 和 Android/iOS 均为 `uncertain`；详细证据与回滚见 Level22 本地两份文档。
- D025 成功门保留原 Player Trigger，并在同一根追加本关 Rigidbody2D 与非 Trigger Polygon；正式
  `shizhu` 石柱使用独立 Rigidbody2D 与 BoxCollider2D。两者可由画线真实传力，不修改共享成功门。
- 早期控制器覆盖过用户 Inspector 质量，现已由 Inspector 作为唯一物理参数来源；当前门/柱 mass 为
  `100/50`，质量本身不会降低重力加速度。
- 用户最终明确玩法是“画了线就解锁”，而不是等待画线碰到机关。DLevel25Ctrl 因此用 Kinematic
  保持画线前初态，任意一笔有效 StrokeCompleted 后同步释放两体；不 AddForce/AddTorque，不修改玩家、
  全局 Layer Matrix 或共享成功门。

### 20.2 物理与 Trigger 隔离契约

- 门和石柱 Prefab/Awake/Reset 均为 Kinematic。任意有效笔画完成后，两体立即同步 Dynamic + WakeUp；
  `_isReleased` 保证同一 Attempt 只执行一次，后续多笔不会再次清空运动速度。
- Reset 恢复初始局部位姿、Kinematic 和零速度，并重新开放下一次有效画线释放。
- Rigidbody2D Inspector 是唯一物理参数来源：当前门 mass 100、柱 mass 50，均保留 Interpolate、
  Continuous、gravity 1；门实体 Polygon 与柱 BoxCollider2D 直接复用 DrawStrokeStable。
- 门实体由旧 `(2.3,2.55)` 方盒改为 12 点 Polygon，局部 bounds `x[-1.18,1.18]`、
  `y[-1.37,1.17]`；旧方盒在门框中下段每侧约多出 `0.1～0.22` 的透明碰撞区。
- 门根 y 为 3.374，Polygon 实体底约 y=2.004；柱根 `(4.95,-0.77)`、Box 顶约 y=1.962336，
  初始非穿透缝约 0.041664。Polygon 顺时针且无自交。
- 只有门实体 Collider 排除 Actor Layer 11（bit 2048）。门 Rigidbody 和原成功 Trigger 的 exclude
  继续为 0，因此玩家不被实体门推挡，但仍能触发既有 `DrawLevelCtrl.OnTriggerSuccess`。
- 门实例删除 Draw Layer 14 override，继承共享门 OpenDoor Layer 3。因用户实测原 Trigger 仍可画，
  DrawLineFeature 改用显式 `ContactFilter2D.useTriggers=true`；成功 Trigger 完整范围现在不依赖全局开关
  即参与落笔/续画阻挡，Draw Layer 14 Trigger 仍被 mask 排除。
- 成功门动画、倒计时、FallArea、猫夹球、DB/存档/UI/analytics 均沿用既有链。

### 20.3 验证、风险与回滚

- Unity 6000.0.59f2 先前全量 Roslyn 编译 `0 error`；本次未修改运行时代码。Prefab 129 个 anchor
  唯一，129 个唯一/254 处本地引用完整；12 点 Polygon 顺时针、无自交。Kinematic 初态、画线完成
  直达唯一释放入口、局部 2048 排除、显式 Trigger 查询、材质和原成功回调静态检查通过；Editor.log
  最近 6000 行未见 D025、反序列化或编译错误。
- 最终 Inspector、画任意有效线立即解锁但不凭空散架、实体轮廓贴合、Trigger 完整范围禁画、100/50
  质量下的斜线可解性、门柱倾倒、玩家只触发不碰撞、Reset/Replay、暂停及 Android/iOS 均为 `uncertain`。
- 回滚画线释放会导致机关始终 Kinematic；回滚显式 Trigger 查询会让禁画重新依赖全局状态。
  仅回滚实体轮廓时恢复旧 Box 类型、size `(2.3,2.55)` 与 offset `(0,-0.104367256)`；完整移除机关需
  成组移除 DLevel25Ctrl、门实例新增 Rigidbody2D/实体 Collider，并处理石柱物理；
  共享门、材质、配置和存档无需迁移。

## 21. 2026-08-03 DLevel037 中心齿轮铰接平台、五秒成功与四向边界

### 21.1 结构与物理边界

- 中央 `Sp_l93_cl_01` 保持 Static Rigidbody2D；长 `platform_Stone` 根使用 mass 10 的 Rigidbody2D，
  通过 HingeJoint2D 连接齿轮中心。实际 Transform 换算后的平台 local anchor 为 `(2.7022433,0)`，
  connected anchor 为 `(0,0)`，Motor 关闭、connected collision 关闭、Use Limits 关闭；平台可无限制连续旋转。
- 平台和 Actor Prefab 初态均为 Kinematic；第一笔有效线完成后由局部 DLevel37Ctrl 同步切 Dynamic。
  Actor 保留共享 Prefab 的 mass 3 / gravity 4.5，只在 D037 复用 friction 0.8 的 `DrawStrokeStable` 材质
  抑制侧滑；画线继续以自身刚体重量和碰撞配重。
- 平台继续使用既有 WithoutShadowGround Layer 13；D037 Actor 的 whatIsGround 局部加入 bit 8192，
  最终 mask 24904，使 Dynamic Actor 能识别旋转平台为地面，不修改共享 Actor Prefab。
- 本关不 AddForce/AddTorque、不主动回正。平台的全部运动来自重力、Actor/画线接触与关节约束。

### 21.2 成功、HUD 与生命周期

- D037 成功规则按最终确认恢复为普通限时成功：根节点使用
  `DrawLevelCountdownCtrl(cdTime=5,isSuccess=true)`，第一笔有效画线后启动，时间归零直接成功。
- 不检查平台角度、角速度或是否处于平衡；没有失衡清零、宽限或 DLevel37Ctrl 自定义成功提交。
- HUD、暂停与倒计时 Reset 全部沿用通用控制器；DLevel37Ctrl 只恢复平台/Actor 位姿、Kinematic
  与零速度。多笔画线保持开启。

### 21.3 四向 FallArea

- 根节点新增共享 FallArea 的左、右、下、上四个 nested instance，沿用 DLevel_017 固定视野外布局：
  `x=-18.23/+18.1`，`y=-10.39/+10.39`。
- FallArea 继续只通过共享 `FailAreaTriggerHandle` 识别 Player Actor 并派发既有失败事件；不新增 D037
  专用触发脚本，不把画线出界当作失败。成功倒计时与失败事件的竞争继续由 DrawLevelCtrl 结果锁处理。

### 21.4 验证、风险与回滚

- Unity 6.0.59f2 PrefabImporter 已导入当前 D037；独立 Roslyn 全量 Assembly-CSharp 编译为 `0 error`
  （只有工程既有 warnings 与旧编译器的 SourceGenerator 加载 warning）。YAML 为 146 个唯一 anchor、
  0 重复和 0 缺失本地引用；静态确认一个五秒成功倒计时、四个共享 FallArea、无自定义成功代码。
- 物理手感、首个 Dynamic 物理步的接触稳定、猫侧滑、画线质量区间、齿轮 CircleCollider 局部阻挡、
  五秒到点成功、四向越界、Reset/Replay、结果竞争、低帧率和 Android/iOS 均为 `uncertain`，
  完整 PlayMode 尚未执行。
- 完整回滚需成组移除 DLevel37Ctrl 与 Hinge，恢复平台/Actor Rigidbody override，并恢复旧两个根组件；
  Draw 系统、Actor Prefab、DB/Luban、存档、资源、UI 和平台配置无需迁移。

## 22. 2026-08-03 DLevel027 独立猫夹球控制器与静态斜坡边界

### 22.1 基线与职责

- D027 复用 DLevel002 的单套共享猫夹球契约：Alien 在 Prefab 中摆最终位置，首次可见时从路径起点横向
  飞入；夹子闭合且球保持 Kinematic；第一笔有效线后播放 `clip_open/control`，球脱离 Spine 层级再切
  Dynamic；失败、手动重开与 Replay 直接应用最终位置闭夹持球状态，不重播 Intro。
- 新增薄控制器 `DLevel27Ctrl : DLevel2Ctrl`，只建立 D027 自身脚本身份，和 D015/D020 一样直接继承
  共享 Intro/Release/Retry。D027 没有 D026 的菜单门禁卡死复现，因此不复制固定 2 秒超时，避免以后合法
  延长动画时被提前截断。
- D027 的 `platform_Stone` 根节点组件为 Transform、SortingGroup 与 BoxCollider2D，子级仅承载视觉，
  是固定斜坡而非 D026 双齿轮铰链平台；本关控制器不接管其位置或物理状态。

### 22.2 Prefab 清理与胜负链

- D027 根节点已把误绑的 `DLevel26Ctrl` 改为 `DLevel27Ctrl`；移除与可见 Intro 重复的
  `DelayAutoBeginDrawHandle(delay=0)`，首次进入只保留一条画线门禁链。
- 移除两套齿轮/平台/铰链数组全部为空的 `DLevel26PlatformCtrl`。静态斜坡的位置 `(-5.39,-0.14)`、
  角度 `34.695°`、缩放、加长 Sprite 与实体 BoxCollider2D 均保持，不新增刚体、关节或脚本冲量。
- 第一笔后同时启动 `DrawLevelCountdownCtrl(cdTime=20,isSuccess=false)`；唯一启用成功门继续回调
  `DrawLevelCtrl.OnTriggerSuccess`，球壳通过 `successExtraRendererGroup` 与子 Actor 同步渐隐；旧门明确
  禁用并命名为 DisabledLegacy。Left/Right/Bottom/Top 四个 FallArea 沿用共享复合球身份解析和失败重建。

### 22.3 影响、验证、风险与回滚

- 影响仅限 `DLevel_027.prefab`、Level27 局部脚本和文档；D026、共享猫夹球/成功门/FallArea、Draw
  公共代码、Physics2D Matrix、DB/Luban、存档、UI、资源地址、analytics、SDK 与平台配置均未修改。
- 静态验证确认根节点无 D26 控制器、重复 Delay 或空平台组件残留；猫、夹子、球、Spine/BoneFollower、
  成功附属渲染组、倒计时、门和四个 FallArea 引用均可解析；Prefab 为 139 个唯一 YAML anchor、
  139 个唯一非零本地 fileID，重复/缺失均为 0。Assembly-CSharp 全量 Roslyn 编译为 `0 error / 104 warning`，
  最近 Editor.log 未见 C#、Prefab 导入、断言或序列化错误。
- 首次入场回弹、第一笔放球、固定斜坡弹道、进门、四向越界、20 秒失败、暂停、连续重开、
  Replay 与 Android/iOS 仍为 `uncertain`，完整 PlayMode 待执行。
- 回滚局部控制器会恢复跨关卡身份误绑；不应单独恢复重复 Delay 或空数组 D26 平台组件。若未来斜坡改为
  动态机关，应为 D027 建立完整局部物理拓扑和控制器，而不是复用 D026 的空绑定组件。

## 23. 2026-08-03 DLevel036 Kinematic 移动与 Draw 接触后 Dynamic

- DLevel036Ctrl 挂在关卡根节点，当前显式绑定 `motionRoot=Scene/pingtai`、其 nested
  `platform_Stone` Rigidbody2D 和 Scene 下 Actor Rigidbody2D。Ship 已是 platform_Stone 下的纯视觉子节点。
- 接触前整体移动 pingtai，使 Static `Sp_l93_cl_01` 铰接点、Kinematic platform_Stone 与 Ship 保持相对结构；
  脚本默认速度/幅度为 `2/0.08`，当前 prefab 配置为 `1/0.05`，频率 `0.5Hz`；Actor 以同一世界偏移
  MovePosition，避免兄弟层级角色停留在原世界坐标。
- Ship 当前只是 Layer 0 的视觉子节点，现有 Water 只识别 Layer 11，不能提供本关所需浮力。等待阶段由
  Kinematic、零重力、Interpolate 的 platform_Stone 承载，并在 `FixedUpdate` 中整体写入 pingtai 世界坐标，
  让 Static 铰接点与平台沿同一确定性路径移动。
- 每个固定步用预分配 ContactPoint2D 数组查询 platform_Stone 与 Draw Layer 非 Trigger 接触。首次接触后
  platform_Stone 与 Actor 同时切为 Dynamic 并保留横向和浮动瞬时 Y 速度；平台 Gravity Scale `1`，Actor
  保留 prefab Gravity Scale `2`。pingtai 停止移动，HingeJoint2D、静态铰接点与 Physics2D 接管。
- 释放后的下一物理步读取平台在 Actor 原乘坐点的 `GetPointVelocity`，按 prefab 倍率 `1.25` 一次性传给
  Actor；船头命中形成的铰链角速度因此转化为切向抛射，支点附近命中不会获得同等固定冲量。
- 控制器只复用 `DrawLevelCtrl.IsPaused`、局部 `Resolved` 事件和 Attempt Reset 分发；暂停/结算停止推进，
  结算固定平台和 Actor 为 Kinematic，重置恢复 pingtai 世界坐标、平台相对位姿、Actor 世界位姿与浮动相位，
  并调用 SyncTransforms。不新增全局事件、共享脚本、
  资源、配置、存档、UI 或平台分支。
- 结构复盘结论：固定路线单船继续保留为本关局部机制；出现多个已验收、职责一致的移动载具前，不抽取
  共享移动平台控制器。
- Assembly-CSharp 编译 `0 error`，Prefab 138 个 YAML anchor 唯一且本地引用完整；父 Transform/铰链同步、
  首次切换、船头/支点抛射差异、接触冲量、Dynamic 摆动、承载/碰撞手感、
  右侧边界、暂停/Reset/Replay、完整可解性与 Android/iOS 均为 `uncertain`。

### DLevel_036 当前结构更正（2026-08-03）

- 上一节中的 `pingtai/platform_Stone` 是旧结构记录；当前 `Scene/Ship` 自身持有
  Rigidbody2D/PolygonCollider2D/HingeJoint2D，并连接到新增的 Kinematic `Scene/ShipPivot`。
- 当前有效机制：Ship/ShipPivot/Actor 碰前 Kinematic 且共用一次性水平初速度；首次 Draw 接触后
  Ship/Actor 切 Dynamic，ShipPivot 继续 Kinematic；Ship 由 Line 碰撞力矩和 HingeJoint2D 求解转动，prefab limits 限制在 `±20°`；
  Actor 获得以 `(0.1,0.8)` 为基准、按 Line 相对左右位置决定水平方向、
  按 Line 从画线完成到命中的下落高度放大的 Impulse。不再依赖延后一帧的平台点速度。
- Ship 的高度与水平跟随由移动 ShipPivot 的铰链锚点约束，不再使用 `FreezePositionY` 或代码角度夹取。
- 等待阶段也已删除正弦上下浮动，Ship/Actor 只在进入等待态时获得一次相同水平速度，后续不持续控速；
  旧 motionRoot、局部位姿恢复、MovePosition 与 SyncTransforms 链同步移除。
- 无效代码清理后，人工船体角速度、运行时最大旋转角字段、铰链重复配置、初始约束缓存及冗余角速度恢复均已移除；
  铰链连接和 `±20°` limits 仅由 prefab 持有。
- 为保留投石机压下后回弹的表现，Dynamic 阶段局部使用角度回正扭矩与角速度阻尼；首次旋转仍完全来自 Line 碰撞，
  不恢复人工初始角速度，也不扩展共享 Draw 或 Actor API。
- Actor 投射保留相对方向和 Line 下落高度增幅，同时增加单关最小向上冲量兜底；这是为解决近零落差时
  `0.8` 基准冲量无法对抗本关 Actor 重力的局部手感问题，不上提共享 Actor 能力。
- 结构复盘决策：这是单关、高度驱动的简化投石机机制，继续局部保留，不抽取共享 API。
- 验证：Assembly-CSharp `0 error`；Prefab 133 anchors 无重复、本地引用无缺失。PlayMode 与移动端为 `uncertain`。

## 2026-08-04：Draw 关卡序列一次性控制器

- 新增 `DrawGameLevelCtrl` 作为 Draw 排序与关卡数据查询入口，由 `GameManager` 持有、`LoadManager` 在 RemoteConfig 和 Luban 表完成后初始化一次。
- 排序构建顺序固定为本地 `TbDrawLevelSort`、可选 `drawgame_config.level_sort`、末尾 `20999`；选关面板不再拥有排序生成职责。
- 当前不提取主线与 Draw 的共享基类：两套表、存档和 RemoteConfig key 不同，局部控制器能保持已验收主线行为不变。
- 后续若出现第三种稳定重复玩法序列，再评估统一只读关卡目录接口；当前保留为候选，不迁移 `LevelCtrl`。

## 2026-08-04：DLevel_030 固定中心偏重旋转平台

- D031 复用支持方向配置的 `DLevel10Ctrl` 和共享猫夹球 Rig，以 `FromLeft` 完成“飞入、开夹放球、退场、
  落地双弹、开放画线”序列；根节点只保留这一套 Intro Handler，不复制控制器或新增全局事件。
- 球和运行时画线继续由 Physics2D 实际求解；当前 Prefab 回读值为球 mass `1`、线满长度质量 `100`、
  线 gravity scale `2.5`、collider radius `0.1`，没有 AddForce、AddTorque、Joint2D 或脚本吸附。
- 为改善杯口夹球，画线模板实例局部绑定 `DLevel31Grip`（friction `1`、bounce `0`、Maximum）；球与共享
  画线资源不变。单关接触手感继续使用“专用材质资源 + nested Prefab override”隔离，暂无共享抽象收益。
- 静态验证为 147 个唯一 YAML anchor、147 个唯一非零本地引用且无缺失，材质与 Prefab 均已被 Unity
  成功导入。首次流程、常见杯口曲率、地面卡滞、进门、失败/Reset/Replay、低帧率和 Android/iOS
  横屏表现仍为 `uncertain`；详细风险与回滚见 `Level31` 本地两份记录。

## 25. 2026-08-04 DLevel032 无猫双球相遇与局部尖刺失败

## 2026-08-04：DLevel_035 首笔 Wood 视觉旋转

- DLevel_035 根节点新增局部 `DLevel35Ctrl`，通过既有 `IDrawStrokeCompletedHandler` 接收第一笔有效画线，
  只驱动 `Scene/Wood` 根 Transform 绕自身中心匀速旋转；当前 prefab 速度为 `-50` 度/秒（顺时针），正负值控制方向。
- 暂停时停止推进，结算时停在当前角度，Attempt Reset 停转并恢复 Wood 初始局部角度；15 秒成功倒计时继续
  由通用 `DrawLevelCountdownCtrl` 管理。
- 本关不使用 Rigidbody2D/HingeJoint2D，不改变 Actor、Draw Line 或成功物理，只增加激活期间每帧一次
  Transform 写入；无新全局事件、配置、存档、资源、UI 或平台分支。
- 结构复盘：当前属于单关纯视觉机关，继续局部保留；多个已验收关卡出现相同首笔视觉旋转职责前不抽取共享组件。
- 静态验证：Assembly-CSharp `0 warning / 0 error`；prefab 125 个 anchor 无重复、125 个唯一非零本地引用无缺失。

## 2026-08-04：DLevel_040 首笔启用 AreaEffector2D 风区

- DLevel_040 根节点新增局部 `DLevel040Ctrl`，通过既有 `IDrawStrokeCompletedHandler` 在第一笔有效线后
  把 Actor Rigidbody2D 从 Kinematic 切为 Dynamic；不修改共享 `ActorCtrl` 或新增全局事件。
- 持续动力由 `Scene/Fan/WindArea` 的 AreaEffector2D 提供，当前方向 180°、力度 40、Mask 只含 Actor；
  控制器不再包含 `FixedUpdate`、`AddForce`、手动力参数或世界 X 停力阈值。
- WindArea 初始关闭，第一笔后开启；结算、Disable 和 Attempt Reset 关闭风区，Reset 同时恢复 Actor 初始
  世界位姿、Kinematic 和零速度，后续笔画不会重复启动流程。
- 结构复盘：动力参数和作用范围继续保留在 DLevel_040 prefab 的 Unity 物理组件中；多个已验收关卡出现
  相同首笔风区门禁前不抽取共享组件。
- 静态验证通过：prefab 149 个 anchor 无重复、本地引用无缺失；Assembly-CSharp
  `0 error / 101 warning`。PlayMode 风区范围、可解性和 Android/iOS 手感为 `uncertain`。

## 2026-08-04：DLevel_047 上方入场与首笔完成后放球

- DLevel_047 根节点以现有 `DLevel2Ctrl` 替换 `DelayAutoBeginDrawHandle(delay=0)`；该控制器完成可见入场后
  继续闭夹持球，`DrawLevelCtrl` 开放画线，第一笔有效线段完成时才开夹并把球切换为 Dynamic。
- 为保留当前 DLevel_009 的上方进场方向与节奏，D047 仅在自身 prefab 上把共享 Rig 的
  `entranceStartPoint` 覆写为 `(0, 12.022222, 0)`，并使用 `1.2s / OutBack(1.9)`；不复用 D009 Intro 内
  自动放球、退场和落地双弹的行为。
- `alien` 显式绑定共享 Rig 根，内部球、夹子与 Spine 引用由 Rig View 按 `DLevel2Ctrl` 既有契约解析；
  球释放父节点显式绑定 `Scene`。Retry 直接恢复 FINAL 闭夹持球态，等待新尝试第一笔。
- 结构复盘：现有 `DLevel2Ctrl` 已完整覆盖需求，因此不新增 `DLevel47Ctrl`，也不修改 `DLevel2Ctrl`、
  `DLevel10Ctrl`、D009 或其它已完成关卡脚本。已完成关卡脚本默认只读复用的边界已固化到仓库规则。
- Key、DoorLock、场景、Draw、DB、配置、UI、资源、事件和平台链均未修改。Prefab 静态引用、Unity
  PrefabImporter 与 PlayMode 结果见 `Level47/`；未验证项明确标记为 `uncertain`。
- D032 不需要猫、夹子或入场动画，Prefab 已移除共享 Rig 和 `DLevel6Ctrl`。本关局部
  `DrawTwoBallReleaseCtrl` 显式绑定两颗原地球；两球以 Kinematic、mass 10 等待，第一笔后同步切 Dynamic，
  Attempt Reset 恢复父级、位姿、镜像和 Kinematic 初态。
- 成功继续由右球的 `DrawTwoBallReunionSuccessHandle` 显式绑定左球，不改为 Tag/Layer 范围判定。
- 下方 10 个 Caltrop 由本关 `DLevel32SpikeTrapHandle` 在 OnEnable 绑定现有
  `OnTrigger2DHandle`。命中当前关卡任一球内 Actor 后移动并重播现成 Knock_Explosion、死亡、击退和
  trap 音效，0.5 秒后调用 `DrawLevelCtrl.TryFailAndReset`。
- Knock 实例与 D003 使用同一 Prefab，并移除相同 source localID `5393762992232218095`；运行时不再
  Instantiate 完整模板，因此不会带出错误的敲门子表现。
- 双球爱心现统一使用 `Resources/Sp_hx_ax_01.png`，共享组件保留程序心形缺资源回退；该资源边界适用于
  D006、D032 及以后复用同组件的关卡，碰撞和结算不依赖贴图加载成功。
- D032 的局部释放和尖刺解析均不扩展共享 D006/Actor/Physics Matrix，也不新增全局事件。Unity Roslyn
  聚焦编译通过；Prefab 为 297 个唯一 anchor、0 duplicate、0 missing local reference。最新
  PrefabImporter、完整 PlayMode、成功/失败竞争、Replay 与 Android/iOS 为 `uncertain`；详细证据和
  回滚见 `Level32` 本地两份文档。

## 26. 2026-08-04 DLevel033 纯物理双球相遇

- D033 从 D032 复制后保留相同的双球初态与成功规则：两球 Kinematic、mass 10，首笔有效画线后由共享
  `DrawTwoBallReleaseCtrl` 同步切 Dynamic；右球的 `DrawTwoBallReunionSuccessHandle` 显式绑定左球。
- 本关没有尖刺或其它脚本失败条件，已删除复制来的 `DLevel32SpikeTrapHandle` 和
  `DLevel032_Knock_Explosion`。场内五个圆环保持 Dynamic、mass 1，由 Physics2D 与画线、双球真实碰撞。
- `DrawTwoBallReleaseCtrl` 从 Level32 目录移动到 DrawLevel 公共目录并保留原 GUID，D032/D033 无需重绑；
  该共享组件仍不实现 Intro，不新增开场等待、全局事件或关卡编号判断。
- Excel 已有 `20033 / DLevel_033 / 恋爱球球3` 与排序 `29 / 20033`，本轮 Luban 生成成功并同步运行时 JSON。
  Prefab/配置静态检查和完整脚本编译结果见 Level33 本地记录；PlayMode 与 Android/iOS 为 `uncertain`。

## 27. 2026-08-04 DLevel042 无刺双球相遇

- D042 沿用用户指定的 D032 双球规则：两球 Kinematic、mass 10，第一笔有效画线后由共享
  `DrawTwoBallReleaseCtrl` 同步切 Dynamic；右球成功组件继续显式绑定左球。
- 本关明确没有尖刺，已删除从 D032 复制残留的 `DLevel32SpikeTrapHandle`、
  `DLevel032_Knock_Explosion` 及全部对应引用；无局部危险物失败控制器。
- 只对齐行为参数，不覆盖 D042 自有 Transform 和美术布局；`strokeMass` 从 20 对齐为 D032 的 100，
  排除布局和 D032 专属尖刺后，D042 与 D032 的双球、释放、成功及画线参数差异为 0。
- Excel/运行时 JSON 为 `20042 / DLevel_042`、墨水 15、星级 `0/2/5`、排序 `30 / 20042`；加入
  D034/D039 后，运行时与 Guru 默认 Draw 排序当前为完整 33 项，D042 仍保持第 30。Prefab 为 259 个唯一 anchor、
  0 duplicate、0 missing local reference；
  PlayMode、Reset/Replay 与 Android/iOS 仍为 `uncertain`，详细证据见 `Level42` 本地两份文档。

## 28. 2026-08-04 DLevel034 首笔释放双风车与四向边界

- D034 两组 `fengche` 根节点各使用 Kinematic、mass 5 的 Rigidbody2D 与连接世界中心的 HingeJoint2D；
  第一笔有效画线后复用既有 `DLevel4Ctrl` 同步切 Dynamic，避免画线前 Actor 重量提前推动机关。
- 每组十字叶片的两张木条精灵各有一个略收边 BoxCollider2D；Joint 关闭 motor、limits 和 connected
  collision，不使用脚本力、角度夹取或持续写速度，风车转动由 Physics2D 接触力矩决定。
- 四边复用共享 FallArea，坐标与 D037 已有全屏边界一致；成功继续由原有
  `DrawLevelCountdownCtrl(cdTime=7,isSuccess=true)` 在第一笔后计时并提交，没有新增关卡专用成功脚本。
- D034 当前猫夹装置复用 `DrawGame_AlienClipBallRig` 与 `DLevel2Ctrl`：保留用户摆放的终点
  `(-0.1,5.2)`，入场起点改到本地 `(0,5.277918)`。球在闭合夹持/入场阶段 Kinematic，入场收稳后
  开放画线，第一笔完成才开夹并切 Dynamic；同一首笔还释放双风车并启动 7 秒倒计时。
- 本轮不修改共享 C#、配置、存档、UI、资源或平台系统。Prefab 为 173 个唯一 anchor、0 duplicate、
  0 missing local reference，Unity PrefabImporter 与完整 C# 编译均成功；当前 Excel 与运行时 JSON 已将
  `20034` 放在第 32 项。上方闭合入场、首笔开夹、菜单可达性与移动端为 `uncertain`。
- 结构复盘结论：双风车只需现有首笔释放接口和标准 Physics2D 组件，不新增 D034 专用控制器；猫夹流程
  直接复用共享 `DLevel2Ctrl`。世界铰链中心和碰撞盒尺寸保留为本关局部参数，不扩展共享抽象。

## 29. 2026-08-04 DLevel039 猫夹球与巨石同步释放

- D039 以本关 `DLevel39Ctrl : DLevel2Ctrl` 继续复用单套 Alien/Spine/夹子/球 Intro；首笔有效画线完成后，
  同一 Handler 回调先开夹放球，再把右上方 Stone 从 Kinematic 切为 Dynamic，不新增全局事件或持续轮询。
- Attempt Reset 同时恢复夹持球与 Stone 初始位姿、Kinematic 和零速度；Intro 保留 D026 已验证过的两秒
  非缩放时间兜底。缺失 Stone 绑定时只停用巨石释放，不阻断开夹放球。
- Prefab 已用本关控制器替换跨关卡 `DLevel26Ctrl`，并删除全部引用为空、会在 Awake 报错的
  `DLevel26PlatformCtrl`；共享 D002/D026、画线、物理矩阵和成功/倒计时行为不变。
- Stone 原有空绑定的 `DLevel4RockTrapHandle` 已替换为本关 `DLevel39StoneBallFailureHandle`。处理器只在
  首笔后 armed，并以指定球 Rigidbody2D 实例识别 Collision2D；命中后在首个 contact point 重播 D003
  同源裁剪版 Knock_Explosion、播放 trap 音效，0.5 秒后直接调用 `DrawLevelCtrl.TryFailAndReset`，不执行
  Actor 死亡、击退或共享 `OnTriggerTrapEvent`。
- Excel 与运行时 JSON 已有 `20039` 和第 33 项排序；Guru 默认排序同步为相同 33 项。
- 结构复盘决策：单个附加刚体与猫夹球同步释放可由派生类局部组合，不上提 Stone 字段到共享
  `DLevel2Ctrl`，也不为一个对象引入共享数组控制器；无 ActorCtrl 球体的单关伤害继续使用显式目标的局部
  处理器，不扩展 D004 的 Actor 陷阱。Unity 全量脚本编译成功，Prefab 139 个 anchor 唯一且本地引用无
  缺失；当前 legacy 成功门 inactive，接触点表现、20 秒失败倒计时竞争、Reset/Replay 和 Android/iOS
  仍为 `uncertain`；以后若启用成功来源，需补命中时的失败结果占用边界。

## 2026-08-04：Draw 最后普通关直达 DLevel_999

- `DrawPlayCtrl.TryGetNextLevel()` 必须与选关页共同读取 `DrawGameLevelCtrl.LevelSorts`，不能绕过一次性控制器
  读取原始 `TbDrawLevelSort`；否则运行时追加的 `20999` 和非 Editor 远端排序不会进入通关推进链。
- 最后普通关胜利现在由同一下一关查询同时更新 `DB.DrawGame.UnlockLevels / CurrentLevel`，并在胜利面板点击
  下一关后加载 `DLevel_999`；普通关顺序和无有效下一关时返回选关页的降级边界保持不变。
- 本次仅修复共享 Draw 进度控制器的数据源一致性，不修改任何已验收 DLevel 脚本、prefab、配置或全局事件。
  `Assembly-CSharp` 编译为 `0 error / 101 warning`；Unity PlayMode、远端排序和移动端仍为 `uncertain`。

## 2026-08-05：Draw 标准成功门补齐开关门、ribbon 与胜利彩纸

- 共享 `DrawGame_SuccessDoor.prefab` 根节点新增 `DrawSuccessDoorEffectCtrl`，组件持有门 Spine、`ribbon`
  和 `ConfettiBlastRainbow` 根粒子的局部序列化引用：开门和 `openDoor` 后启用 `ribbon`，至少保持
  1.6 秒；随后关闭 ribbon，按 `0.2s -> dose_close -> 0.25s -> closeDoor + confetti` 收口，彩纸的
  0.8 秒与关门动画剩余时长并行等待。
- `DrawLevelCtrl` 只保留 Actor 原地渐隐、HUD/结果锁、等待门表现和胜利结算职责；Reset 会通知组件取消并
  清理 ribbon 和彩纸粒子。不引入主线的 `enterDoor`、角色销毁或震动，无门成功入口不变，也未增加全局事件。
- 静态扫描确认 32 个非空成功门绑定均引用标准 `DrawGame_SuccessDoor`，其中 21 个标准门当前 active；
  其余无门/legacy 实例及 DLevel039 的 inactive legacy 门不进入当前玩家可见链。C# 编译为
  `0 error / 101 warning`，共享 prefab 的脚本 GUID、门、ribbon 与彩纸引用静态一致；完整 PlayMode、连续
  Reset/Replay 和 Android/iOS 横屏音画同步仍为 `uncertain`。

## 2026-08-05：yellow_ball 球载体 Actor 禁用自动皮肤绑定

- 结构复盘决策：球载体是否允许玩家换肤属于 Prefab 语义，使用 nested removed-component 或本地组件删除
  表达；不把 GameObject 名称、Tag、Layer、关卡 ID 或 `GetComponentInChildren` 顺序写入运行时判断。
- 共享 `DrawGame_AlienClipBallRig` 的嵌套 Actor 移除 `ActorSkinBinding`，覆盖当前 16 个直接消费者；
  D028/D030/D032/D033/D042/D043 的本地 `yellow_ball/payloadContentRoot/Actor` 同步处理，双球两侧均移除。
- 通用 Actor.prefab、ActorSkinBinding.cs、DrawLevelCtrl.cs 不变；目标关卡不再读取玩家皮肤 DB、订阅换肤
  事件或加载部件 Prefab，普通 Actor 关卡行为不变。
- 7 个实际修改 Prefab 均通过 anchor 唯一性、本地引用完整性和差异格式检查；工程编译为
  `0 error / 895 warning`。Unity 导入、22 关完整 PlayMode、Reset/Replay、成功渐隐和移动端仍为
  `uncertain`。详细记录见同目录专项功能与风险评估。

## 2026-08-05：五个双球关爱心尺寸与密度统一

- 适用范围固定为 `20006 / DLevel_006`、`20032 / DLevel_032`、`20033 / DLevel_033`、
  `20042 / DLevel_042`、`20043 / DLevel_043`。静态引用基线表明，这五个 Prefab 是当前
  `DrawTwoBallReunionSuccessHandle` 的全部 Prefab 消费者；本次不通过关卡编号写运行时分支。
- 调整前，爱心直接沿用 `Bubble.prefab` 的随机 `startSize=0.2~0.4`，五关均显式配置
  `heartEmissionRate=5`，碰撞时另外即时 `Emit(1)`。共享组件现新增序列化
  `heartSizeMultiplier`，脚本默认值保留 `1`；五关显式设为 `1.5`，等效随机尺寸约
  `0.3~0.6`，持续发射率同步降为 `4`。首颗即时发射、`1.8s` 演出和粒子生命周期不变。
- 尺寸倍率只作用于运行时克隆 ParticleSystem 的 `main.startSize`，不修改
  `Assets/Game/ArtSrc/Particles/Prefabs/Bubble.prefab`。爱心继续优先使用
  `Assets/Game/Resources/Sp_hx_ax_01.png`，加载失败才回退程序心形；第 14.3 节把程序心形写作
  主路径的历史描述已由本条和 2026-08-04 正式贴图记录取代。
- 双球冻结、接触点上方偏移、Sorting Order `340`、相机聚焦、两阶段成功、Pause/Reset、输入、UI、
  DB/存档、Luban、Guru/RemoteConfig、事件、analytics 和平台流程均不改变。放大增加单颗粒子的像素
  覆盖，降低持续发射率用于减少密度，但 Android/iOS 过绘与纹理压缩表现仍需真机验证。
- Unity 6000.0.59f2 自带 NetCoreRuntime 与 DotNetSdkRoslyn `csc.dll` 使用当前
  `Library/Bee/artifacts/1300b0aE.dag/Assembly-CSharp.rsp/.rsp2` 完整编译：
  `ExitCode=0 / Errors=0 / Warnings=104`，目标脚本无诊断。五关 Prefab 的
  `Definitions/LocalReferences/duplicate/missing` 依次为 D006 `102/197/0/0`、D032 `259/539/0/0`、
  D033 `228/498/0/0`、D042 `252/543/0/0`、D043 `244/530/0/0`；每关成功组件、rate `4`、
  size `1.5` 各 1 个，脚本 GUID 的 Prefab 消费者全仓恰为这五个。
- Unity Editor.log 已记录五个目标 PrefabImporter 成功导入并完成脚本重编译；五关 PlayMode 的实际尺寸、密度、
  遮挡、首颗反馈、Pause/Reset/Replay、低帧率及 Android/iOS 横屏均为 `uncertain`。
- 回滚本次表现时只把五关倍率恢复为 `1`、持续发射率恢复为 `5`；若删除新字段，应同时清理五个
  Prefab 的对应 YAML。正式贴图及程序回退、Bubble 源 Prefab、镜头、物理与成功门禁无需回滚，
  也不存在配置或数据迁移。

## 2026-08-05：DLevel_999 播放 TBC 音效

- `DLevel_999` 复用主线最后一关的 `TBC` SFX；触发点位于 Draw HUD 创建成功并设置关卡 ID 后。
- 门禁使用 `TryEnterLevel()` 保存的标准化 `DLevel_999.prefab` 实际资源路径；不依赖缺少运行时追加项的
  原始 `TbDrawLevelSort`，也不把远端排序中的任意末项自动视为 TBC。
- 继续复用 `AudioManager.PlaySound()` 与现有 `SoundCollection` 标签，不新增关卡组件、全局事件或音频资源；
  背景 `musicInGame`、prefab、输入、Intro、Reset、结算和存档链保持不变。
- 结构复盘结论：单个占位关音效保持在现有 HUD 生命周期的精确路径分支，不抽象共享“关卡专用音效”系统。
  静态资源与门禁已核对，完整 C# 构建为 `0 error / 101 个项目既有 warning`；PlayMode、Reset/Replay 和移动端为
  `uncertain`，完整记录见 `Level999/`。

## 2026-08-06：DLevel_999 降采样；墨水循环暂不启用

- 本关最终把 `sampleSpacing` 从 `0.1` 提高到 `0.5`、`maxInkLength` 从 `300` 降到 `150`，理论最大采样段由约 3000 降到约 300；多笔、碰撞、质量配置和画线宽度保持不变。
- 局部 `DLevel999Ctrl : IDrawStrokeCompletedHandler` 脚本保留备用，但已从 Prefab 解除绑定；墨水耗尽时不会调用 `ResetAttempt()`，仍保留旧线直到正常 Reset/Replay。
- 结构复盘结论：当前只启用本关参数降采样，不修改共享 `DrawLineFeature`、不新增全局事件，也不迁移已验收关卡；共享碰撞体生成的进一步优化继续保留为候选。
- 静态参数与 Prefab 绑定已核对，`Assembly-CSharp` 先前构建为 `0 error / 101 个项目既有 warning`；曲线手感、墨水耗尽表现、PlayMode 和 Android/iOS 性能仍为 `uncertain`，详细记录见 `Level999/`。

## 2026-08-13：DLevel053 蜂群生存与延迟失败占用

- D053 第一笔有效画线后与共享成功倒计时同步启动：蜂巢在 3 秒窗口内按每批 1 至 3 只的随机计划生成总计 8 只蜜蜂；单蜂使用两帧动画、零重力动态刚体追踪 Player，蜂间逐对忽略碰撞。
- 蜜蜂撞击非 Trigger 场景实体时，只对 Dynamic Rigidbody2D 在接触点施加复合冲量：主方向围绕入射方向小幅随机，并叠加水平冲量；自身临时忽略当前障碍并沿原入射反方向退回配置距离，结束或禁用时恢复碰撞。玩家命中使用实际 Collision2D 接触点播放 Knock，不对 Actor 施加物理击退。
- `DrawLevelCtrl` 新增局部延迟失败演出占用：`TryBeginFailurePerformance()` 原子锁住输入、其它结算与 HUD 点击，`CompleteFailurePerformance()` 结束表现后仍走 `TryFailAndReset()` 标准失败重建，`CancelFailurePerformance()` 只供 Disable/Reset/销毁中断清理。失败占用与既有延迟成功占用互斥。
- `DrawLevelCountdownCtrl` 在失败演出占用期间停止扣时。D053 玩家命中后以非缩放时间完整等待 1.5 秒，再提交失败，因此这段演出不计入 10 秒生存成功时间；设置面板的通用 Resume 只改变 Pause，不能提前解除失败占用。
- 关卡数据已配置为 `20053 / DLevel_053 / 蜂群围攻`，Draw 排序为第 45 项；Luban 运行时 data/sort JSON 中 `20053` 均唯一。初版完整 Assembly-CSharp 编译为 `0 error / 105` 条工程既有 warning；本轮最终 D053 三脚本隔离编译为 `0 error / 21` 条同名类型 warning，当前全工程编译被无关的 Level117 缺字段阻断。三个 Prefab 无重复 fileID、悬空本地引用或 Missing Script。
- `uncertain`：尚未在 Unity PlayMode、Android/iOS 横屏验证随机批次观感、高速细线碰撞、冲量/反冲手感、剩余小于 1.5 秒时命中的倒计时冻结和重开清理。详细结构与验收清单见 `Level53/` 两份本地文档。
- 回滚时移除 D053 三个局部脚本、Bee/Hive Prefab、关卡 Prefab 绑定和 20053 配置；若同时删除共享失败占用接口，必须先确认没有其它调用方，并同步移除 Countdown 的占用门禁。无需迁移存档或 SDK 数据。

## 2026-08-13：DLevel053 蜂窝自治与均匀攻击通道

- 现场验证暴露出“只随机出生点、所有实体仍直追同一目标中心”不能形成有效蜂群随机性；D053 改为按整轮总数建立均匀角度槽、槽内随机并洗牌，近距离再收束到玩家。该方案继续保持关卡局部，不升级共享追踪系统。
- 蜂群模板、批次、出生椭圆、最小间距和攻击偏角全部归属 Hive；Ctrl 只处理玩家命中与结算。Hive 内嵌 inactive `BeeTemplate_AdjustHere`，场景 Override 能直接作用于运行克隆，同时模板不进入物理或清理列表。
- 对画线的推动仍使用接触点 `ForceMode2D.Impulse`；Bee 源 Prefab 默认主冲量由 `40` 提升至 `350`，D053 场景模板进一步 Override 为实际 `900`，并叠加水平冲量 `400`。本关局部画线质量改为按长度约 `12.5~50`，未修改共享默认质量或全局碰撞矩阵。
- 结构候选：具有“生成器 + 多实例模板”的后续单关，可继续观察将运行实例、生成参数和模板统一归宿主机关的局部结构；在第二个稳定案例出现前不抽共享基类。
- 三脚本隔离 Roslyn 编译 `0 error`，三 Prefab 引用复审与 diff 检查通过；PlayMode、移动端与最终物理手感仍为 `uncertain`。

## 2026-08-13：DLevel053 渐显、水平冲量与失败后蜂群延续

- 蜜蜂实例启用后以 `0.2s` 从透明渐显，暂停期间 Alpha 不推进；Bee Prefab 根缩放由 `0.75` 调为 `0.825`，即放大 `1.1` 倍。
- 碰撞动态刚体时使用复合冲量：沿入射方向的主冲量允许 `±8°` 随机偏转，并叠加沿飞行/目标左右方向的水平冲量。Bee 源 Prefab 默认主冲量为 `350`，但 D053 场景模板 Override 为 `900`；水平冲量为 `400`，当前画线基准质量为 `50`。调参必须以场景 Override 后的实际值为准。
- 玩家首次命中仍只占用一次失败、调用一次 `Actor.Die()` 并冻结成功倒计时；该 1.5 秒内蜂群不冻结，未完成批次继续生成，已生成蜜蜂继续运动。后续不同蜜蜂仍可各播放一次接触点 Knock/音效，但不重复失败；同蜂持续接触按实例去重。
- 真正 Pause、Success、标准 Failure 重建、Reset/Disable 仍会停止或清理蜂群。三个 D053 脚本隔离编译 `0 error`，Prefab/FileID/Missing Script 静态复审与 diff 检查通过；PlayMode、短线冲量和移动端仍为 `uncertain`。

## 2026-08-13：DLevel053 追踪实体的视觉头部朝向

- D053 证明“移动方向”和“视觉瞄准方向”需要按玩法语义拆开：Bee 仍按均匀攻击通道、近端收束和摆动计算速度，但视觉头部始终瞄准玩家主 Collider 中心，不能直接使用带偏角的飞行速度，也不能只做 X 轴翻面。
- 对具有左右 Sprite 且要求腹部不倒置的追踪实体，当前局部模式为“记录原图头部水平基准 -> 左右翻面 -> 从翻面后的头部轴计算 `SignedAngle`”，把俯仰限制在 `±90°`；垂直临界区保留上一左右状态，减少翻面跳动。该模式目前只有 D053 一个案例，不提取共享组件。
- Bee 保留 Freeze Rotation 防止物理碰撞带来乱转，脚本在 FixedUpdate 显式设置角度；中心圆形 Collider 使视觉旋转不改变碰撞轮廓。隔离编译与 Prefab 引用检查通过，PlayMode 和移动端视觉仍为 `uncertain`。
## 2026-08-17：DLevel054/055/056 静态复查与 D056 平台物理释放

- 静态复查 `DLevel_054.prefab`、`DLevel_055.prefab`、`DLevel_056.prefab`：三者的本地 anchor 均无重复，所有 `m_Script` GUID 均能解析到项目内 `.cs.meta`；054/055 的 D053 蜂群组件、蜂巢位置和场景差异均保持原样，本轮没有改动其运行逻辑。
- 056 的 `pingtai/platform_Stone` 原本已有质量 `10`、线性阻尼 `0.05`、角阻尼 `1.5`、重力倍率 `1` 的 Rigidbody2D 和中心 HingeJoint2D，但刚体为 Kinematic，且根节点没有首笔画线后的释放处理，因此画线无法让平台受力摆动。
- `DLevel_056.prefab` 原本没有运行时有效挂载的平台释放控制器；本次在根节点有效挂载单元素绑定的 `DLevel26PlatformCtrl`。它引用 `Sp_l93_cl_01` Static 刚体、`platform_Stone` 刚体和 HingeJoint2D。首笔有效画线后切换平台为 Dynamic 并唤醒；清线/重试时恢复初始位姿和 Kinematic 状态。共享脚本、D054/D055、Actor、UI、倒计时、配置表、存档和 SDK 未改动。
- D056 仍保留 `DLevel53Ctrl/DLevel53Hive`。这可能是有意的蜂群+平台组合，也可能是复制残留；缺少玩法确认，本轮标记为待确认而不删除。D054/055 的 `DrawLineFeature.isConfig=false` 也意味着运行时使用 Prefab 的 `maxInkLength=20`，不是表中的 `15`，本轮保留现有关卡口径。
- 静态验证：D056 新增引用存在、anchor 去重通过，新增绑定行无尾随空格。该 Prefab 当前为未跟踪文件，普通 `git diff --check` 不覆盖完整资源；Unity PlayMode 的左右摆动、Reset/Replay、暂停/成功时序以及 Android/iOS 横屏仍为 `uncertain`。详细记录见 `Assets/Game/Scripts/Level/DrawLevel/Level56/`。

## 2026-08-18：DLevel055/056/057 静态复查与 DLevel058 角色首笔释放

- 静态复查 `DLevel_055.prefab`、`DLevel_056.prefab`、`DLevel_057.prefab`：配置表与排序均已注册，Prefab 本地 anchor 无重复，所有 `m_Script` GUID 和本地数字引用均可解析；没有发现必须修改的复制规范问题。055 的空编辑节点、Prefab 自有 `maxInkLength` 覆盖配置表以及 057 的 `strokeMass` 差异均保留，避免覆盖用户已调参数。
- `DLevel_058.prefab` 以 D053 组件为基础，并把共享猫夹装置的原夹持球替换为 Actor Prefab。`DLevel58ActorReleaseCtrl` 现为 `DLevel2Ctrl` 的薄子类；它保留 D058 的独立脚本身份，复用单套装置的 `IDrawLevelVisibleIntroHandler` 和 `IDrawStrokeCompletedHandler` 生命周期，未修改共享 `DLevel2Ctrl`。
- 首次进关在转场揭开后执行可见 Intro：Alien 从 `entranceStartPoint` 飞到 Prefab FINAL，夹子保持 `clip_close` 并跟随 `bone2`，Actor 入场期间为 `simulated=false` + Kinematic，入场完成后仍保持 Kinematic。第一笔有效画线回调同步播放 `clip_open`、把 Actor 保持世界位姿脱离 `clipSwingPivot/catch` 并切为 Dynamic/WakeUp，猫播放 `control` 后回 `idle01`。Retry/Replay 直接应用 FINAL+闭夹+Kinematic 完成态，不重播飞入。
- Prefab 根组件显式绑定继承字段 `drawLevelCtrl`、`alien`、Actor 根 `yellowBallRigidbody`、D058 根 `ballReleaseParent` 和 `clipSwingPivot`；入场时长为 `0.78s`。058 配置 `20058 / DLevel_058` 已存在，蜂巢、蜂群、DrawLine 参数、UI、存档、SDK 和其它场景物理未改动。
- 静态引用/层级核对完成，当前薄子类已用 Unity 自带 Roslyn 隔离编译，结果 `CSC_EXIT=0`。Unity PlayMode 中的实际飞入、开夹、Actor Dynamic 释放、Retry/Replay、蜂群交互与 Android/iOS 表现尚未验证，统一标记为 `uncertain`。详细记录见 `Assets/Game/Scripts/Level/DrawLevel/Level58/`。

## 2026-08-18：DLevel058/059 角色局部防滑与 DLevel059 复制复核

- D058/D059 的 Actor 根刚体原先继承共享 `Actor.physicsMaterial2D` 的零摩擦与 Minimum 合并。释放流程本身已清零速度，因此本轮不新增停走脚本；两关只在嵌套 Actor Rigidbody2D 上局部覆盖已有 `DrawStrokeStable`（摩擦 `0.8`、弹性 `0`、Maximum 合并）。
- 该覆盖不锁定 X、不持续归零速度，也不修改质量、重力或空中阻尼，保留画线、蜜蜂和障碍真实推动角色的能力；共享 Actor、地面和 Physics2D 矩阵不受影响。
- D059 静态复核通过：继续复用 `DLevel58ActorReleaseCtrl : DLevel2Ctrl`，猫可见 Intro、首笔 `clip_open`、Actor Dynamic 释放、D053 蜂群、接触点 Knock、1.5 秒失败与 7 秒成功倒计时均已绑定。其 `strokeMass=100`、较大蜂力、两枚静态圆障碍、较低蜂巢和删除长地面均作为本关调参保留。
- 最终静态结果：D058/D059 分别为 `135/143` 个唯一 anchor，均无重复或悬空本地引用；两关目标 Actor 材质 Override 各恰好一条。Unity PlayMode、实际停止距离、结算临界、Retry/Replay 和移动端仍为 `uncertain`。详细记录见 `Level58/` 与 `Level59/`。

## 2026-08-18：DLevel061 猫夹角色首笔释放补齐

- D061 的 Actor 已在共享夹子下保持 Kinematic，但复制后的根节点缺少首笔释放 Handler，D053 蜂群控制器的 `actor` 也为空，因此画线后不会开夹/切 Dynamic，蜂群失败链也没有明确角色目标。
- 本关继续复用 `DLevel58ActorReleaseCtrl : DLevel2Ctrl`，显式绑定当前 Alien、`clipSwingPivot`、Actor Rigidbody2D 和 D061 根释放父级；首笔执行 `clip_open`、脱离动画父级并切 Dynamic，Retry 恢复 FINAL+闭夹+Kinematic。D053 同步绑定同一 Actor 的 ActorCtrl。
- D061 原有防滑材质、10 秒成功倒计时、画线质量、蜂群参数、尖刺与场景布局保持不变；没有新增脚本或修改共享 Prefab、配置、UI、存档和 SDK。
- 静态检查为 163 个唯一 anchor、0 重复、0 悬空本地引用，脚本 GUID 全部可解析；PlayMode 中的开夹落地、蜂群/尖刺、结算临界与移动端仍为 `uncertain`。详细记录见 `Level61/`。

## 2026-08-18：DLevel_999 传送门进入最小序列的已解锁未通关主线关卡

- `DLevel999Ctrl` 从未绑定的墨水耗尽重置候选改为本关实际挂载的传送门控制器；它不再实现
  `IDrawStrokeCompletedHandler`，因此 2026-08-06 确认的“墨水耗尽不自动 Reset”现行行为不变。
- 点击或碰撞传送门均复用主线 `Level999Ctrl` 的 2 秒开放门禁、一次性触发、HUD 输入遮罩、Actor 吸入、震动及等待时序；
  目标改为最终主线排序中“配置有效、已解锁、未通关”的最小 `LevelSort.Id`，正常路径不经过 `StartPanel`。
- 跨玩法入口统一调用 `GameManager.TryEnterMainLevel()`：先由 `PlayControllerManager` 退出当前 Draw 控制器，触发既有
  `level_end(exit)`，再由 `PlayCtrl.TryEnterLowestUnlockedUnpassedLevel()` 通过 `LevelCtrl` 选择目标并派发现有
  `EnterLevelEvent`，沿主线标准流程创建所选关卡并发送 `level_start`。本次没有新增全局事件、配置、存档字段或资源加载协议。
- 目标进关请求被接受后上报 `enter_tbc_portal(type=1)`；失败请求不计入。IWanner 的同类入口使用独立
  `ILevel999Ctrl`，共享的仅是跨玩法进入主线边界，避免把单关演出逻辑上提到公共控制器。
- 结构复盘结论：跨玩法“进入最小序列的已解锁未通关主线关卡”属于主线查询与控制器边界能力，可由
  `LevelCtrl`、`GameManager` 与 `PlayCtrl` 提供最小公开入口；
  传送门演出、门禁和 SDK 类型仍归属各关局部脚本，不建立新的共享传送门基类。
- 定向 `Assembly-CSharp` 构建为 `0 error / 109 warning`。DLevel_999 Prefab 静态检查为 243 个 anchor、无重复、
  243 个本地引用且无悬空；Unity PlayMode、连续点击/碰撞、目标加载失败恢复及 Android/iOS 横屏仍为 `uncertain`。
- 完整结构、风险与回滚见 `Level999/DLevel_999脚本结构记录.md`、
  `Level999/DLevel_999主线传送门功能与风险评估.md`。

## 2026-08-19：DLevel_999 主线传送门绑定恢复与点击射线隔离

- DLevel999 的传送门资源重新复制后出现组件与 UnityEvent 引用脱落。本轮只在本关 Prefab 恢复
  `DLevel999Ctrl`、点击入口、Trigger 入口、ActorDestroy target/endTarget 绑定，不修改共享主线查询和跨玩法切换接口。
- `ClickItemEventHandle` 的点击射线没有 LayerMask；独立区域 Collider 即使服务于其它系统，也可能成为射线首个命中项。
  DLevel999 的 `enableDrawAreaRestriction=0`，因此本关局部禁用 `DrawLineArea` Collider，保留传送门 Collider。
- 结构复盘结论：当单关不启用画线区域限制时，可局部关闭闲置的区域 Collider 以消除输入竞争；该做法不能推广到仍需
  区域限制的关卡。后续若出现“区域 Collider 与世界点击共存”的第二个稳定案例，再评估局部 LayerMask 点击组件或统一输入
  仲裁，不因单关问题改写已验收关卡和共享 `ClickItemEventHandle`。
- 静态校验为 245 个唯一 anchor、485 个本地引用无缺失、9 个脚本 GUID 均可解析；完整 C# 构建为
  `0 error / 1124 warning`。Unity PlayMode、SDK 打点顺序和移动端触控仍为 `uncertain`。

## 2026-08-19：DLevel_999 弹窗打开时禁止点击穿透传送门

- `ClickItemEventHandle` 使用独立 Physics2D 射线，Bear.UI 遮罩不会自动拦截世界点击。DLevel999 将按钮 UnityEvent 改绑
  本关 `EnterMainLevelByClick()`：Popup、Top 或 System 层存在已打开视图时忽略点击，关闭后可重新点击；Trigger 入门链
  保持原样。
- 结构复盘结论：本次需求只有 DLevel999 一个明确消费者，继续采用关卡局部门禁，不修改已被多个已验收关卡使用的共享
  `ClickItemEventHandle`。查询只在实际点击时执行；若后续多个关卡出现同类问题，再以回归证据评估 UIManager 无分配公开
  查询或世界输入统一仲裁。
- 最终 `Assembly-CSharp --no-dependencies` 定向构建为 `0 error / 106 warning`，此前完整依赖构建为
  `0 error / 1124 warning`；Prefab 新点击方法绑定静态确认通过，PlayMode 弹窗矩阵和移动端多点触控仍为 `uncertain`。

## 2026-08-21：Draw 胜利下一关转场对齐主线遮罩边界

- 现行统一顺序：`EnterNextLevelEvent` 解析下一排序项并通过门禁后，先销毁旧
  `DrawGame_GamePlayPanel`，但保留已 `Resolved` 且关闭输入的旧 DLevel；随后创建
  `EnterLevelLoading`。翻页完全覆盖并进入 `onPageTurnComplete` 后，才解除旧运行时订阅、
  失活并销毁旧 DLevel，然后创建新 HUD、异步加载/实例化并初始化新 DLevel，最后由 Erase 揭开。
- 该顺序复用主线 `PlayCtrl.OnEnterNextLevel()` 的遮罩所有权和 `EnterLevelLoading` 回调边界，避免旧关在
  Loading 出现前消失。新关加载失败时继续走既有 `HandleOpenFailure()`，清理残留并降级到 Draw 选关页。
- 首次从选关页进入具体关卡仍要求当前没有活动 DLevel；Reset/Fail/Victory Replay 仍沿用黑幕下整关重建，
  不因本决策改成 EnterLevelLoading。提示、奖励墨水、存档、广告和 Analytics 契约均不改变。
- 后续 Draw 下一关实现或重构默认以此顺序为优先基线。若机制必须提前销毁旧 DLevel，需先记录遮罩、
  输入、加载失败恢复和移动端风险，并取得用户明确批准，不得静默回退。
- 静态验证覆盖目标调用链与差异检查；`dotnet build Assembly-CSharp.csproj --no-restore
  --no-dependencies -m:1 --nologo` 为 `0 error / 106 warning`，warning 均为工程现有项。Unity PlayMode
  中的低帧率翻页、资源加载失败、连续点击以及 Android/iOS 横屏表现仍为 `uncertain`。回滚时恢复
  `OnEnterNextLevel()` 在进关请求前完整清理的旧顺序，并删除 `TryBeginLevelTransition()` 的保留旧关分支
  和翻页完成后的释放入口。

## 2026-09-14：DLevel_084 富有弹性的线

- D084 复用 DLevel2Ctrl 的单套猫夹球入场、首笔释放和 Draw Retry 接口，移除复制的 D039 石头专用组件绑定；本关没有石头，避免 Awake 空引用报错。没有新增运行时脚本或修改已验收关卡。
- 高弹画线采用本关独立 PhysicsMaterial2D（friction 0.35、bounciness 1、Maximum），仅覆盖本关嵌套线模板 Rigidbody2D；反弹交给 Physics2D。结构复盘确认此类纯参数差异可复用 D013 的材质隔离方式，不需要新增共享弹性系统。
- 有效目标门补齐 OnTriggerSuccess 目标及成功表现引用；Tips 从 D039 旧标题改为独立 D084 词条。当前按进门玩法假设采用首笔后 20 秒超时失败；用户素材布局、停用备用门、球参数和共享生命周期不变。
- LevelData 按 20066 → 20084 → 20999 插入，Draw Sort 末项为 59 / 20084；源表保存后与 Luban 生成 JSON 全序列核对一致。外部语言源未同步，本地新词条后续导入需保留。
- 108 个唯一 Prefab 对象与本地引用检查通过；运行时程序集编译 0 error / 113 warning；独立 Unity Physics2D 反弹测试通过，主工程 PlayMode 启动成功。完整 D084 路线、提示线、暂停、Reset/Replay 与移动端仍为 uncertain，不能把单项物理测试视为整关验收。详细结构、结果和回滚见 Level84 两份记录。

## 2026-09-14：DLevel_084 球碰线弹射，线与地面稳定

- 用户试玩反馈明确要求线落地不要太弹，主要让球撞线后弹射。本节替代同日第一版的线 Maximum 方案与球质量 5 参数。
- D084 三种本关材质采用弹性线/球/地面为 1/1/0、Multiply 合并，形成球×线=1、线×地=0、球×地=0；摩擦为 0.8/0.05/0.8、Minimum 合并。球质量降至 1，线仍按长度为 8～32，碰撞方向与速度均由 Physics2D 求解。
- 结构复盘：需要区分接触对象时，先评估成对材质与质量参数；本次无需新增碰撞脚本、速度修正器、共享接口或改动已验收关卡。只追加 D084 球/地面材质 override，不改变放球、门、倒计时和 Retry 边界。
- Unity 6000.0.59f2 独立物理测试通过：线落地反弹速度 8.829 → 0；自由短线质量 8 的对照中，球相同入射速度 -9.325，回弹 2.219 → 7.477；固定 -25° 斜线接触后球速度为 (6.377,6.351)。固定或无重力对照仅隔离接触响应，完整动态布局可解性和移动端仍待试玩。当前三材质参数与回滚见 Level84 记录。

## 2026-09-14：DLevel_085 弹性的线2

- 用户确认 D085 仍为纯物理关，无需新增机关。继续复用 D002 放球控制器及 D084 成对物理材质，保留当前场景布局、参数、提示画法和 Reset/Replay 黑幕重建边界。
- 复制残留仅需修正 Tips 的 D084 标题词条，并添加 20085 配置入口。有效门、放球、失败绑定完整，静态竖柱和长地面不需要机关脚本或额外重置。
- 结构复盘：相同物理玩法可以复用控制器；新增静态障碍应核验 Collider、层和材质，不因缺少 Rigidbody2D 误加运行时逻辑。D085 当前共享 Level84 三份材质，后续差异化调参需先独立资源。
- LevelData 按 20084 → 20085 → 20999 插入，Sort 末项 60 / 20085；Luban 生成两份 Draw JSON，各仅新增本关一条。125 个 Prefab 对象和本地引用完整，本轮只改变 Tips 实例。
- 完整进门、提示解法、暂停、Reset/Replay 与移动端仍为 uncertain；D084 的独立接触测试不作为 D085 整关验收。详细绑定、验证及回滚见 Level85 两份中文记录。

## 2026-09-14：DLevel_085 顶部尖刺死亡

- 用户后续新增顶部刺并要求同款受击死亡。D085 局部 Handler 采用 D003/D032 同源裁剪 Knock 粒子与 trap 音效，复用 D061 的失败演出窗口，先占用再 Die，0.5 秒后 CompleteFailurePerformance。
- D085 的 Actor 位于球内且已移除独立 Rigidbody2D；旧 Actor.Knockback 直接访问自身刚体，不能照搬。当前接触身份通过显式球刚体判定，致死击退作用于球载体，方向沿刺尖（顶部刺当前朝下），正常弹性不变。
- 结构复盘：单个已有刺用持久化 UnityEvent 显式绑定即可，无需运行时按 Layer 扫描。局部 Update 只在演出期间扣非缩放时间；Disable/Attempt Reset 取消所属演出和粒子，不添加额外重置流程。
- 完整运行时代码编译 0 error / 113 warning；134 个 Prefab 对象与引用闭合，隔离真实 Unity Physics2D/本关脚本的过滤、重复碰撞、延迟及中断检查通过。Draw/Actor/Audio 使用测试替身，整关正式表现、重建与移动端仍为 uncertain。详细证据与回滚见 Level85。

## 2026-09-14：D085 无材质地面导致高弹的修正

- 新增长地面/竖柱未指定材质，曾被误判为与显式零弹性材料相同。真实 Unity 6000.0.59f2 接触测试中，弹性 2 的线落空材质静态地面从 -8.633 反弹至 +17.658；相同条件改用显式 StableGround 后出射为 0。
- 已仅给 D085 两个 Collider 补齐 D084 同款 StableGround，保留线/球弹性及最新场景内容；球碰线支撑仍高弹，地面接触稳定，D084 和共享物理材质未改。
- 复盘结论：需要成对材质控制接触时，须核验新增静态表面的显式材质并验证真实接触，不能以未指定材质推导“Multiply × 0”。本结论替代 D085 首次接入的错误推断。完整关卡与移动端仍需试玩。

## 2026-09-14：D085 球的自然滚动调整

- 球旋转未锁定，原摩擦 0.05/Minimum 与角阻尼 0.5 更易形成滑动并快速衰减旋转。D085 新建独立球材质，摩擦提高到 0.25；只在本关球刚体覆盖角阻尼 0.05，保持弹性 1/Multiply 与稳定地面方案。
- 真实 Unity 斜坡对照的滚动比从 0.180 提高到 1.003，球落稳定地面不弹，撞弹性线后的发射速度基本保持；摩擦变化会降低原本滑动的切向速度，完整解法仍需试玩。
- 结构复盘：球体滚动手感优先通过接触摩擦和角阻尼调整；先独立本关材质，再修改参数，以保留旧关行为。当前无需旋转脚本或更改释放/重置接口，D084 和共享材质保持原样。

## 2026-09-14：D084/D085 球地恢复适度回弹

- 用户明确要求两关球与地板有正常弹性，本次取代此前“球地不反弹”的决定。分别将两关球材质设为 0.4/Maximum，线维持当前 2/Multiply、显式稳定地面维持 0/Multiply，保留各关摩擦/阻尼与全部 Prefab 内容。
- 真实 Unity 6000.0.59f2 物理步验证三组接触：球地适度回弹（入射 -9.325、出射 +3.845），线地稳定，球线弹射前后相同；D085 回弹后仍形成自然滚动（滚动比 1.000）。两份球材质与测试输入逐字节一致，资源范围和 Prefab 引用检查通过。
- 结构复盘：球上采用 Maximum 的优先级可覆盖地面的 Multiply，实现球地非零回弹；线仍用 Multiply，与显式零弹地面接触稳定。此结论只适用于本次明确赋材质的接触，不可延伸为空材质静态表面；无需新建弹射或旋转脚本。
- 两关仍分别持有球材质，既有放球、尖刺、重试、配置与事件不变。完整解法及移动端仍为 `uncertain`，详细数据和仅回退两个球弹性字段的路径见 Level84/Level85 最新功能与风险评估。

## 2026-09-14：D085 新增斜平台材质

- 按用户截图将新增 `StoneB (2)` 的 BoxCollider2D 显式绑定地面同款 StableGround，保留形状、角度与布局。当前球地适度回弹、线地稳定的材质规则覆盖该平台。
- 复盘：局部静态平台的差异由 Collider 材质承担；复制蓝色条形外观不代表复制了材质。本次只有一个材质字段变化，155 个对象/引用检查通过，实际材质与已通过的 Unity 接触测试相同；无需共享接口或重置扩展。完整路线和移动端待试玩，证据及回滚见 Level85 风险记录最新章节。

## 2026-09-14：D084/D085 封闭高弹线框的局部限速

- 同型封闭线框真实测试复现球峰值约 3336 并穿出；仅在碰撞后钳制球速仍不能覆盖紧凑框的单步连续增能。两关球现通过局部 DLevel84BallSpeedLimiter 设 30 上限，在相对表面距离较小时按间距进一步降速，保留原材质、Continuous、摩擦和角阻尼。
- 结构复盘：速度限制与可运动距离相关，不能只看 FixedUpdate 前/碰撞后的一次速度数值。形状 Cast 复用数组且只用于本球物理约束；显式 Inspector 绑定、随球实例销毁，无全局限速覆盖、运行时对象扫描或新增 Reset。共享 Rig、D002 和 DrawLineFeature 均保持原样。
- 两关共 12 组封闭框各 60 秒验证通过，包含质量 8/32、紧凑与旋转框；普通弹射、球地回弹和 085 自然滚动保持。全量运行时编译无错误、两关绑定核验通过。完整原始画法与移动端仍为 uncertain，限制及回滚见 Level84/Level85 最新风险记录。


## 2026-09-14：D086 弹性的线3接入与结构复盘

- 用户确认“弹性的线3”，以当前 D086 布局和 D085 最终物理方案为基线。20086 按编号插入 LevelData，Sort 61 追加末尾；独立 86 标题词条与两份 Draw JSON 已同步。
- 球限速器、尖刺处理、D002 放球、20 秒倒计时及同门成功引用已正确复制。只读复用 D084 限速器、D085 尖刺/球材质与 Level84 线/地面材质；本轮没有修改运行时代码或物理参数。
- 复盘决定：同机制新关先核验实际球宿主、每块静态平台材质与触发器引用，不因类名保留旧关编号添加空控制器或迁移脚本。普通球地回弹、球线弹射、线落地稳定和窄框降速继续由原实例组件承担，无需增加 Reset 或全局事件。只针对 D086 调材质时先复制为本关资源，避免影响前关。
- 通用化尖刺/限速组件命名保留为候选；当前改名收益不足以引入旧关迁移。角色/背景旧外观 override 与门已移除组件记录仍与 D085 一致，另行记录，不当成新关脚本绑定故障。
- 155 个对象与本地引用检查、关键资源/绑定、表格增量、Luban 生成和旧关哈希检查通过。当前布局的完整解法、重开与移动端仍为 uncertain；本次没有整关运行验收。最终结构、风险及回滚见 Level86 两份记录；证据位于 E:/WorkSpace/outputs/dlevel86-20260914/。


## 2026-09-14：084/085/086 补画线穿模的结构修正

- 用户在反弹中补画一笔后瞬间穿模。36 组真实物理复现有 33 组失稳，出现末速度已限为 30、同一步却位移 102.363；之前稀疏单框的限速证据不足以覆盖新实体接入。
- 本系列线材质由 2/Multiply 调为 0/Multiply，取消线线合并弹性 4 和球线在求解器内增能。DLevel84BallSpeedLimiter 通过显式线材质识别，每物理步最多补一次法向弹射，目标系数 2，随后执行 30/窄框限速。
- 新增 DLevel84StrokeSpawnGuard，只在三关根绑定 DrawLineFeature。完成新笔画后暂缓出生时已重叠的动态碰撞对，分离后恢复；静态地面/Trigger 与他人的忽略关系保持。AttemptReset/Disable 仅清理自己新持有的关系，没有新增全局事件或改变重开责任。
- 复盘决定：物理速度钳制不能代替初始重叠和单步位移稳定性；新增实体必须作为独立边界验证。以三关局部组件收敛修复，不修改共享 DrawLineFeature 或全局物理。复制该系列须同时保留零弹线材质、有界发射参数与出生保护，避免半套配置。
- 最终 36+16 组各 30 秒通过，原单面发射、地面回弹和滚动保持，临时接触清理及完整运行时代码编译通过。测试替代画线对象交付边界，不代替正式玩法/输入/真机验收；具体风险和回滚见 Level84/85/86 最新记录及 E:/WorkSpace/outputs/dlevel-new-stroke-tunnel-20260914/。

## 2026-09-15：D086 历史地图恢复与 D087 分离

- 用户确认将今天误存至86的新地图另存87、恢复昨日86且保留防穿模。先备份实际文件，87只换独立GUID/身份/标题；86仅恢复8个历史地图差异块，保留当前根出生保护和球有界弹射。
- D087沿用系列组件，不新建空控制器，不改变共享脚本、材质、Reset、UI和事件。新增20087及末尾排序62，“弹性的线4”本地词条，Luban两份Draw JSON各只增一条；D086入口保持原值。
- 结构复盘：历史地图恢复必须区分布局与后续功能修复，较早整文件备份不代表当前组件基线。复制时核对独立资产GUID和完整零弹线/有界弹射/出生保护三项，不迁移已验收关卡代码。
- 两关各156个对象和42个外部GUID核验通过，86历史布局/87新地图逐块断言通过，33个受保护前关/共享文件哈希保持，源表保留与导出一致性通过。整关、提示解法与手机仍为uncertain；证据及回滚见Level86/87和 E:/WorkSpace/outputs/dlevel86-87-split-20260915/。

## 2026-09-15：D088 反重力区域接入与结构复盘

- 参考普通 D002 猫夹球和 D087 职责记录。用户提供的 D088 已无弹性系列附加组件/材质，本次保留原地图与物理参数，只新增区域实例、88标题和本地入口数据。
- 本关局部采用 DLevel88GravityZone：同一轮廓生成 Trigger 和填色 Mesh，按 Rigidbody 去重，多区域引用计数避免重复反向。离开最后区域/区域停用时恢复原 gravityScale，不改速度或质量。
- 本地几何与编辑器预览分离：3～64点凹多边形剖分、64边圆形近似；编辑态使用 Handles 预览，不创建 Mesh/对象、不访问 DB，所有预置组件显式绑定。
- 保持原 DrawLevelCtrl、D002、倒计时、成功门、失败/重开/转场边界，不新增共享接口或全局事件。区域能力仅留作复用候选，不迁移旧关。
- 编译、真实 PhysicsScene2D 边界测试、URP Renderer2D 三形状渲染和 Prefab/配置增量校验通过；完整解法、主工程连续重开/通关/打点和真机仍为 uncertain。详见 Level88 两份本地记录及 outputs/dlevel88-gravity-20260915。

## 2026-09-15：D088 区域画线输入补测

- D088反重力区域原Default层Trigger被既有画线阻挡器命中，单独验证机关物理不足以证明输入可用。局部修复为区域物体Layer=Draw，不改变公共DrawLineFeature、Trigger全局查询或碰撞矩阵。
- 结构复盘：新区域组件验证应同时覆盖原有输入门禁与自身物理行为。区域物理Layer与Renderer排序层不能混淆；继续作为当前关卡局部检查项，不因此改造旧关脚本。
- 修复前后对照覆盖三种形状的起笔/跨区阻挡检测、实体墙和普通Trigger边界，以及区域重力反转与恢复。完整触控、HUD和主工程试玩不在此隔离测试覆盖范围内。详见Level88补修记录。

## 2026-09-15：D088 背景填色排序复盘

- 局部采用：区域物理Draw层保持，Renderer改Background/order2，避免区域给SceneLayer标题/前景染色；不引入新脚本或共享抽象，不改旧关。
- 验证不仅比较数值：隔离URP用正式区域资产、SpriteRenderer与SortingGroup作前后渲染对照，旧排序复现染色、新排序保留前景颜色和区域可见性；地图参数保持。整关/触控/真机仍需补验，详见Level88功能与风险记录及 outputs/dlevel88-background-20260915。

## 2026-09-15：D088 重复描边结构复盘

- 局部采用预置Outline/LineRenderer和显式引用，复用区域同源轮廓；按长度Tile配合竖条UV交换，不复制新的物理轮廓，不引入共享抽象、运行时对象搜索或编辑态实例化。
- 保留Draw物理层与Background渲染层，内部#98A0FA/50%和原色描边分开控制。56项真实渲染/网格验证与30项画线/重力回归通过，主工程整关/真机仍需补验。
- 当前仅D088使用；后续有相同需求再评估复用，不据此改造旧关。证据与回滚见Level88本地记录和 outputs/dlevel88-outline-20260915。

## 2026-09-15：D088 内部平铺与调参验证

- 局部复用现有填色网格/材质，Shader按本地尺寸平铺手绘Alpha纹理，区域形状直接裁切；不新增图块对象、Renderer、Collider或共享抽象。
- 参数验证不能只读磁盘或检查LineRenderer字段：用户当前Inspector截图为0.3，本次补充暂停提交参数后的真实BakeMesh/像素对照，同时接通Scene宽度辅助预览和运行态立即刷新；编辑态不生成实例。
- 44项平铺/宽度测试和30项画线/重力回归通过；主工程GUI/完整玩法/真机仍需补验，保持局部边界。详见Level88本地文档及 outputs/dlevel88-pattern-20260915。

## 2026-09-15：D088 纹理试验撤回

用户最终选择恢复Sp_hx_x_02原图直铺，生成图及跨边混合弃用。正式Shader/材质/区域脚本/Inspector/地图/区域Prefab与试验前哈希一致，不新增结构或共享抽象；仅保留试验记录，不自动接回候选效果。原图Repeat有周期不代表边缘无缝，验证与体验结论应分开记录。详见Level88“最终选择”记录。

## 2026-09-15：D089 无新增机制的接入

- 用户已复制并改过089地图，只新增独立数据/标题身份，保留地图，不从上一关覆盖。直接只读复用D002入场放球、Draw主控/倒计时和D088反重力组件，不新增89空壳类或共享抽象。
- 本地20089/排序64、12语言独立键接入；源表新增行及Luban生成一致、469个旧关/共享文件哈希不变，Prefab只有mTerm变化。完整试玩/解法/设备仍待验证，详见Level89两份记录。

## 2026-09-15：D090 复制机关的生命周期核对

- 原Prefab把D002首笔放球与D019杠杆组合，杠杆rig及成功球壳组为空。直接补rig会让Retry出现“根先夹球，杠杆再搬球”的状态不一致，故先向用户确认开场语义。
- 用户选“开场先放球进碗，再画线”，本关直接只读复用D10+D19组合，补同实例显式引用并移除D002；不修改旧脚本或创造90空壳类，不移动用户地图。
- 本地20090/排序65、独立12语言键接入；163对象/316本地引用、逐块地图保留、源表/Luban一致性与17项真实Unity杠杆隔离探针通过。探针未覆盖Spine/Actor/整关/真机，仍须实玩；详见Level90两份记录。
- 候选经验：复制内容检查应同时核对空绑定、首笔动作与Retry状态所有权，不能仅检查GUID闭合；暂不提升为共享抽象或批量改造旧关。

### D090 无法画线回归补修

- 实际日志暴露开场先触发顶部FailArea失败，再完成Intro；DrawLevelCtrl的Resolved拒绝输入，而开场期DrawPlayCtrl拒绝Reset。单独杠杆探针无法覆盖这个回归。
- 本关增加DLevel90EntranceCollisionGuard及两个显式Collider绑定：仅入场期间忽略球根/顶部失败区这一对碰撞，D10后恢复，重开/停用清理；无Update、不改地图/共享脚本/全局事件/打点。
- 真实失败区和保护脚本的隔离PlayMode前后对照16项通过，完整运行时编译0错误/113项警告、166对象322引用闭合。真实HUD触摸、完整Spine演出及真机仍待验收。候选检查补充为“进场轨迹与失败区相交、Intro结束后的输入状态”，不批量迁移旧关。

### D090 弹射力度调参

用户确认能转但无力后，仅采用本关Prefab参数覆盖：杆质量12、角阻尼0.15、球重力1.2；不修改只读D19，不增加发射辅助力或新的结构。原参数/两组候选共21个物理样本含空闲与重置；最终组在六种同落点对比中五种水平改善、六种高度改善，但不能代替完整场景验收。反重力取反后为-1.2的连带效果已记录，详细证据/边界/回滚见Level90风险评估。

同日用户加重至500仍微弹的跟进：实际Prefab三项控制参数回到旧值，用户否认主动恢复；只保留500并恢复授权调参，不新增机制。候选经验：外部写Prefab后的磁盘校验与编辑器实例应用是两项证据；再次反馈时必须重新读取当前资产，未确认前不能认定玩家正在测试上一版参数。编辑器旧状态覆盖原因仍为uncertain，详见Level90风险记录末节。

### D090 对照19关的完整动力链与锚点稳定性

- 本次实际D019线质量150、模板重力5/角阻尼10；此前只比杆/球/线质量漏掉了嵌套模板覆写。D090最终线改150/5/10，杆25/1，保留球重力1.2与原地图/Box线碰撞规则；旧19关、共享线模板和全局物理只读。
- 隔离诊断复现重线碰撞后的铰链位置误差，单纯换参数/增加迭代并未在全部样本消除。新增D090局部DLevel90LeverJointStabilizer显式绑定现有两条铰链，LateUpdate按支座→杆→碗投影残余平移并同步渲染；不改球/速度，不加发射力，不抢D19的入场/回碗。局部关闭杆碗插值，停用恢复。
- 结构复盘：采用局部约束补正，不向已验收D19传播行为、不引入新的共享接口或全局物理配置。物理锚点和渲染Transform必须分别检查；只验Rigidbody坐标会漏过显示仍分离的问题。位置修正改变后续接触，不能包装成“物理解算未变”。
- 44个隔离PlayMode样本、连续回碗/停用恢复、完整运行时代码编译和167对象326引用检查通过；仍缺主场景/Spine/触控/实际反重力/通关与设备证据。详情及回滚在Level90风险记录当前方案节。

### D090 2026-09-15 机制切换：首笔落球（当前，取代上方投石机方案）

- 用户删除杠杆并复制D089 Alien后，当前根D10/入场碰撞保护/成功额外组为空。以新Prefab为基线，根换回D089同款D2首笔控制器，显式绑定当前Alien及球、夹子与成功球壳组；不重建或移动用户地图。
- D2沿既有可见Intro与Retry完成态契约保持夹球，首笔一次性释放。移除D90已无依赖的两个杠杆时期辅助脚本及废弃保护组件；不改共享源码，不新建90空壳或保留并行释放控制器。
- 153对象/292本地引用闭合、143个无关对象块逐字保留、D089序列化基线与源组件核对、只读资产SHA256、完整编译0错误/113警告通过；完整新玩法PlayMode尚未验证。历史杠杆探针不作为新机制验收证据。
- 候选经验：用户替换整套机关后重新检查根生命周期与跨组件绑定（包括成功渲染组），主动清理无引用的旧机制差异；文档应明确旧方案失效。具体当前结构/限制/备份见Level90两份记录及outputs/dlevel90-first-stroke-20260915。

### D091 2026-09-16 保持平衡：首笔后限时存活通关

- 用户要求按37的方式不进门过关；当前D037实际cdTime=7，旧记录5秒已过时。本关采用当前资产的7秒，明确指出差异，不改旧37或静默套用过期数值。
- 用户复制的齿轮/平台/Actor已有物理结构，但根仍是空D2。仅替换为D37并显式绑定四个引用，复用通用成功倒计时和delay0输入门禁；无需91空壳、不改共享代码。保留用户地图及150质量画线，40墨水、0/5/15星级与独立多语言标题完成本地接入。
- 159对象/304引用、152个原对象块保留、源表/生成新行/多语言同源校验、0错误/113警告编译和23项隔离PlayMode通过。真实地图、Actor/HUD/SDK/重建链及移动端未完整测试，详见Level91两份记录。
- 候选复盘：同机制直接复用已有Handler，结果仍归通用倒计时；复制机关要核对根Handler与首笔Kinematic释放，不为编号一致另建控制器。无消费者的TrapEventHandlle不机械复制，四向失败继续共享失败区。暂不升级共享抽象。

### D091 2026-09-16 重线下的平台支点补正

- 用户实测平台脱离齿轮；真实隔离物理对照中Hinge仍连接，但重线冲击后的支点偏差可达0.492698。D091局部新增`DLevel91PlatformAnchorStabilizer`，显式绑定原Hinge，LateUpdate补正平台平移并同步显示，保留D37首笔/重置和通用倒计时。
- 27组对照中物理/显示锚点误差分别不超过0.000003/0.000002，未直接改速度或角速度，旋转、3次重置和启停恢复通过；完整编译0错误。未覆盖主场景与移动端。关闭平台插值及位置补正会影响呈现和后续接触，不声称物理轨迹保持完全一致。
- 复盘：缩放平台的Hinge局部锚点必须先乘lossyScale再使用Rigidbody2D.GetRelativePoint；先用Transform锚点交叉核对，否则会人为制造支点偏差。位置与显示需分别验证。本次保持关卡局部实现，不修改已验收D37、共享画线或全局迭代参数，不提升为全局规则。

## 2026-09-16 D092 双球相遇通关接入

- 用户指定通关方式与D006相同。D092复制资产的D6双释放和共享双球成功Handler已经完整；保留用户右侧反重力区及地图，复用首笔释放、两球接触冻结、爱心聚焦和1.8秒成功，未新增运行脚本或修改旧关。
- 独立92语言键沿用06全部译文；40墨水、星级0/10/20、质量200保留。两份Draw运行JSON仅合入20092和排序92，旧记录保持。Excel已预留92，本轮候选只修改第97行对应说明/参数；源工作簿被占用，写回待完成。既有全量排序与运行JSON差异仍待单独统一。
- 104对象/200引用无丢失，6个关键组件与D006一致，103原对象块保持；Luban生成、候选Excel内容/类型/样式保持及6组真实物理隔离PlayMode通过。完整地图、真实Spine/触控、主场景重开和移动端仍uncertain。详见Level92两份中文记录及outputs/dlevel92-20260916。
- 复盘：复制资产接线完整时无需新编号空壳；碰撞配对/排除层和反重力下冻结需独立核对。读取当前表定位ID，不能机械套用上一关排序和行号；保留打开表格的并发编辑保护。
## 2026-09-16 D093 底部反重力进门关接入

- 用户确认首笔放球、利用反重力进门、15秒超时失败，无额外规则。原D093已具备D2/倒计时/门事件完整绑定，只读复用，保留底部30×5区域、静态圆形障碍和用户地图；40墨水、0/5/15、线质量150不改。
- 新增独立93提示词条（复用90全部语言）、对齐运行数据参数和两份Excel候选第98行；承接92待写回的第97行。源表仍被打开，最新合并候选在outputs/dlevel93-20260916，写回待完成。当前运行JSON已被用户导成1～100，本轮保留全部排序和无关数据；D092早期旧顺序记录是历史状态。
- 159对象/304引用无缺失，158原对象块逐字保持；门/球/渲染组引用、I2同源、候选表内容/类型/样式保持和Luban通过。16项真实物理隔离PlayMode覆盖区域进出、画线复合碰撞、静态物体、超时/暂停/重置/成功停止计时；完整地图、Spine入场、真实门演出、重开/退出/存档/打点与移动端仍uncertain。原配置已有未制作的94，93→94连玩不在已验证范围。
- 复盘：复制关卡先核对绑定，已完整时不用新编号空壳；区域方向差异优先序列化配置。运行配置可能在两次任务之间被重新导出，必须读取当前内容并按ID定点修改，避免重复新增或撤销用户排序。详见Level93两份记录。

## 2026-09-16：D094/D095 行走角色接入与结构复盘

- 用户澄清需要行走角色。以两关现有独立Actor、D001首笔向右和成功门为准，不因当前D002已是夹球结构覆盖新关；无新增运行时组件。现有地刺均关闭，原开关、区域与地形保留。
- 两关各自独立94/95提示键复制90“反重力区域”，20墨水/0、5、10与本地配置对齐，原排序94/95保持。两份源Excel占用未写回，最新候选outputs/dlevel94-95-20260916合并92～95待写回内容，取代前序候选。
- 26项Unity隔离行走生命周期/区域物理检查、两关各132对象252引用检查、Luban/词条/Excel增量检查通过；2329份旧脚本/Prefab/meta哈希保持。正式Actor动画/地图解法、连续通关、重开、存档打点和真机仍uncertain。
- 复盘：复制来源编号不等于当前玩法，应核对实际角色、首笔行为、机关显隐和用户澄清；文案也应与实际启用机关一致。继续只读复用D001/D88，不做旧关迁移或共享抽象。详见Level94/95两份本地记录。


## 2026-09-16：D096 石头首笔释放与反重力接入

- 用户要求石头也受重力区影响。原15块Kinematic石头仅5块接入D4首笔列表，其余空项/漏绑；区域已支持Default层Dynamic刚体。仅补为15个显式引用，保留首笔前静止、角色行走、10秒失败与进门成功，不修改只读D4/D88或添加自动扫描。
- 独立96提示复用90“反重力区域”，本地20096参数对齐80墨水/0、20、40，排序96已存在且保持。源Excel占用，outputs/dlevel96-20260916最新候选合并92～96待写回内容。
- 本次仅写释放列表和Tips两个块；期间另有区域宽度更新，当前三块均4×13.3，已保留并按新尺寸补测。244对象518引用闭合、240个其它原块保持；2339份旧文件哈希一致，另有D094独立地图编辑保留，不属于本次写入。
- 54项Unity隔离前后对照通过，含旧5块/新15块、实际石头多边形反转和离区恢复、跨区所有权、暂停/超时与3次重建；完整Actor/伤害/门演出、地图解法、存档打点和设备仍uncertain。详见Level96结构与风险记录。
- 复盘：首笔释放名单应与实际可动机关集合核对，Kinematic未释放不能通过扩大重力过滤解决。保持局部Inspector绑定，不迁移旧关、不新增共享抽象；并发地图调整需保留并重新读取测试参数。


## 2026-09-16：D097沿用D090首笔放球结构

用户从D090复制D097，保留当前D2夹持入场/首笔释放、15秒失败倒计时、球壳淡出与门成功链。实际复制引用完整，本轮只独立97提示及对齐40/0/5/15本地配置，不新增空壳类或第二套释放控制。7个半径1.5圆区与装置/门位保持，圆形模式不依据残留rectangleSize推断边界。171对象/322引用闭合、170原块保持；39项隔离圆形区域/倒计时检查通过，D2真实动画/整图通关/移动端仍uncertain。两份源表占用，92～97合并候选待写回。详见Level97结构及风险记录，证据outputs/dlevel97-20260916。复盘结论：按当前参考关的实际方案核对复制引用及身份，不沿用废弃投石机文档或历史GUID，也不为相近玩法批量改旧关。

## 2026-09-16：D098保留已完成地图，沿用D087过关方式

用户明确D098地图已搭好，本轮只独立98提示键并将本地20098占位值对齐现有Prefab的50/0/10/20。首笔放球、弹性线/有界弹射、出生碰撞保护、尖刺、20秒失败及门成功直接复用，七个关键组件与当前D087完全一致，不新增编号空壳或共享行为。

481对象/958处本地引用闭合、9个夹球源组件绑定、42个外部GUID和独立GUID检查通过；只有Tips块变化，其余480块保留，2352份旧关/共享文件哈希一致。Luban隔离导出、候选表格值/样式/无关部件与局部渲染检查通过。正式整关、解法、Spine演出、重开/存档打点和移动端仍为uncertain。

两份源Excel被占用，D098源表写回待完成；候选仅含98，不混入92～97其他任务的待写内容。证据与安装前快照校验脚本在outputs/dlevel98-20260916，源表如已保存变化需重新合并。结构复盘：以用户当前地图为修改边界，复制组件完整时只做身份/配置接入，不重做成功链。详见Level98两份本地记录。

## 2026-09-16：D098黄色墙体复用画线弹射

用户追加要求仅D098蓝色结构拥有画线同款弹射并更换黄色素材。基于最新500对象地图，将100处l118蓝色端头/中段Sprite换为同尺寸黄色对应项，20个StoneB Collider改绑球限速器已有的ElasticLine材质。复用接触材质识别、2倍法向增能和30限速，不改生产C#或共享材质，不触碰D086/D087；全部Transform、碰撞几何、启停及成功链保持。两个禁用普通Ground不属于本次蓝色结构，保留原绑定。

证据outputs/dlevel98-yellow-20260916：996本地引用/42外部GUID有效，380个其它对象块逐字保持，804受保护文件哈希一致；独立Unity五方向20组真实碰撞对照与两个30秒封闭框模拟通过。黄色墙与画线同条件出射一致；30为固定更新/碰撞限速点，重力积分后允许一个步长的增量。正式整图可解性、门演出、重开和设备仍uncertain，详见Level98两份记录。复盘：通过新关局部材质绑定复用既有接触能力，避免为相同弹射新增编号脚本或影响旧关。

## 2026-09-16：D099沿用D096行走进门规则

用户要求D099与D096规则相同、走进门通关。保留D099用户地图、两块反重力区和20墨水/0、5、10阈值，沿用原D001首笔行走与成功门事件；补齐D096的10秒失败倒计时和踩线阴影组件，全部显式引用本关Actor/主控。画线Layer14改为13，Actor接地掩码补13层；没有石头的地图不添加D4控制器，不改旧关或生产C#。

164对象/310引用闭合、46外部GUID有效；4原块修改、4块增加、156原块保持，2357旧脚本/资源哈希一致。48项Unity隔离行走计时/阴影检查通过，主控、Actor行走API和HUD为替身，正式门演出、整图10秒可解性、重开/存档打点与设备仍uncertain。独立99提示复制96语言，运行20099对齐20/0/5/10；Luban和候选表保留/渲染检查通过，源Excel仍占用，99候选独立待合入。详见Level99两份中文记录及outputs/dlevel99-20260916。复盘：以玩家规则选择复用组件，保留当前地图参数，不因参考关存在机关而复制无用组件。

## 2026-09-17：D048指定支点受力旋转与回弹

- 用户允许参考D030或D091。D030固定质心/位置约束与D091已有Hinge锚点补正职责不同；本关选择D030局部Handler与固定质心基线，只新增DLevel48Ctrl，使用角差回正扭矩和可调角阻尼，不升级共享抽象或迁移旧关。
- 当前D048原本有启用的Hinge连接静态齿轮，初次检查遗漏后已纠正：保留组件，在目标Prefab停用，避免和Freeze Position X/Y同时约束支点。配置局部中心时要测世界支点；非零中心意味着Transform原点随旋转移动。
- 重置验证发现暂停模拟时只写Rigidbody2D位置/角度会被旧Transform姿态覆盖；本关在暂停模拟期间同步Transform后恢复物理。只修新关，不以复用方便改写D030。
- 当前真实接口DLL编译0错误；逐字生产脚本的Unity6000.0.59f2隔离PlayMode61项通过，覆盖两种支点、两侧1/100/500质量碰撞、回弹/阻力及各3次重置/结算冻结。Draw主控与碰撞对象为替身，完整地图、真实画线/Spine、转场/Replay与移动端仍uncertain。
- 复盘决策：保持关卡局部控制器，配置直接在Inspector绑定，不新增全局事件、位置补正组件或自动查找。当前质量10/阻力1.5/回弹80仅为初始调参值，真实500质量线可能造成大幅旋转，暂不擅加角度限制。
- 本地职责、绑定、验证边界、风险与回滚见`Level48/DLevel_048脚本结构记录.md`及`DLevel_048功能与风险评估.md`。验证工具只在外部Temp，不加入游戏代码；本轮不写D2、门和15秒失败链。收尾外部编辑移除D2及部分源组件包装，杆绑定仍闭合，但当前入场/首笔链不能据原结构保证，未回滚这些编辑。
- 后续中心可视化保持在 DLevel48Ctrl 的 UNITY_EDITOR Gizmos 分区：仅读序列化杆与局部坐标并绘制，不接运行时物理配置或共享管理器，不抽象共享调试组件。Editor/Player 两分支独立编译通过；主工程目视验收仍待完成，详细边界见本关追加记录。

## 2026-09-18：D073 刀物理支点局部修复复盘（上一阶段）

- 采用关卡prefab局部修复：物理层Z旋转/单位缩放、视觉子节点镜像/缩放、135点刀Polygon等价烘焙和正确质心；保持原D48首笔开启/结果订阅/尝试重置，不写旧48或共享接口，不新增编号空壳脚本。
- 隐藏支座图片关闭不等于碰撞关闭；本关静态圆与刀柄重叠，在准确隔离样本中无外来碰撞也能驱动165.867752°旋转。局部停用该圆，Hinge保持关闭，支点由固定质心单独拥有。支座圆接触被移除是有意行为修正，完整地图解法待验收。
- 测试证据候选：Polygon提取限定m_Points/m_Paths，不能把SpriteTiling的pivot/oldSize/newSize混作轮廓；第一轮旧数值撤回精确证据，重跑准确135点并包含实际支座。只观察worldCenterOfMass不够，须同时检查显示刀柄点和所有接触来源。保留为复盘候选，不修改长期AGENTS规则。
- 12组/84项独立Unity真实物理检查和134原块/73受保护文件保持通过；真实Draw/Actor/HUD/Trap/重开/通关和移动端仍uncertain。局部结构、风险及回滚见Level73两份记录，不把本次机关修复当作整关交付。

## 2026-09-18：D073 刀绕支点受重力下摆（当前）

- 用户明确目标后选择既有D4首笔释放+Hinge：正常质心、重力1、无XY冻结；撤下本关D48及无消费者的首笔Handle/Switch，保留已归一化刀图与135点碰撞。固定质心/重力0方案仅适用于原受力杆目标，不能被“支点稳定”局部验收替代整体重力下摆目标。
- 原生Hinge重接触测得0.025至0.029支点偏差，才复用现有D091静态铰链补正并显式绑定原Hinge；位置补正服务同一锚点，保留物理角度/角速度，同时改变接触位置和插值，不能表述为纯原生轨迹完全不变。无新生产脚本/全局事件、旧关改写或共享接口升级。
- 14组/76项隔离检查、首笔门禁/自然下摆/三次重建和轻重两侧/条形碰撞通过；真实Draw/Actor/HUD/结算/暂停/重开/移动端未验收。D48局部Resolved冻结不再存在，当前没有角限位；风险/验证/回滚见Level73当前两份记录。

## 2026-09-18：D075 首笔 enemy 右移与失败门 API

- 新增 DLevel75Ctrl，复用有效首笔 Handler、ActorCtrl 移动和 DrawLevelCtrl 失败演出门禁；本关不再挂 D11，enemy 无延迟右移。公开 PlayFailedDoorAndFail，播放 door_open 并隐藏 enemy，Complete 后才进入标准 DrawGameResetEvent(Failed)。
- FailedDoor 的 enemy Trigger 改为本关无参数 API，显式绑定该实例 Spine；成功门仍走原入口，不改源 Prefab 或共享脚本。嵌套门必须同时核对源组件、PrefabInstance 和事件模式，相同源 GUID 不等于同一实例。
- 主工程 Unity 编译/重载、126 锚点与本地引用闭合、原始动画名和两门绑定检查通过；真实首笔、行走、开门 Complete、失败重建/暂停/重开/设备未实测，均为 uncertain。详见 Level75 结构与风险记录。
- 复盘：本关组合移动与门演出已有明确共同宿主；不复制 D11 钥匙职责、不新增全局事件、不迁移旧关。共享演出封装仅留候选，等待多关稳定重复与回归证据。
## 2026-09-18：D076 双角色分别进门后通关

- 新增 DLevel76Ctrl，CuteActor 对齐玩家首笔后右移；玩家保留 D001。门改为动态 OnDoorEnter，空 Tag 过滤配合两个显式 Actor RB 身份，分别开门/停移动/隐藏，重复同一人不累计。
- 第一人不占用胜利门禁，第二人开始才 TryBeginSuccessPerformance，最终 Spine Complete 提交一次标准胜利；主动重开解绑旧轨道，外部中断恢复当前角色并取消未提交成功。保留原 10 秒超时失败。
- 120 锚点闭合、3 原块修改/2 新块/115 原块保持、十份旧关/共享文件保护哈希、主工程编译与 44 项独立 Unity PlayMode 检查通过。独立使用真实 2D 对象和 Spine 状态机，但 Draw/Actor/门包装为替身；正式地图/动画/UI/计时临界竞争/重建/存档打点/移动端仍 uncertain。详见 Level76 结构与风险记录。
- 复盘：多人成功条件需累计不同身份，不能直接重复调用共享单人成功 API；局部控制真实职责明确，不提取共享抽象，不批量改旧关。多人门流程仅保留为后续候选。

## 2026-09-20：D076 同一轮只播放一次开门

- DLevel76Ctrl 增加当前尝试的门完成状态：首个角色启动唯一一次 `door_open`；第二个角色若在开门中到达则复用现有 TrackEntry，若门已打开则跳过动画并直接完成双人胜利收口。
- 开门被外部中断时仍按原局部边界恢复本次开门角色并取消未提交成功；Attempt Reset 与 Disable 额外清除门完成状态。没有修改共享 DrawLevelCtrl、ActorCtrl、门 Prefab、事件或其它关卡。
- 当前 Bee 响应文件 Roslyn 全运行时编译退出码 0；静态检查确认唯一 `door_open` 调用、开门中/已开门两个不重播分支、Complete 置位与 Reset/Disable 清理，差异检查通过。正式 PlayMode 的两种角色顺序、同时到门、暂停/中断、重开和移动端仍为 `uncertain`。
- 复盘：该状态仅服务 D076 的双人共门时序，继续保留在关卡局部，不升级共享门抽象。

## 2026-09-18：D077机关使用角速度组件

- 用户要求通用刚体角速度API并用于Sp_boxr_dj_01。新增Rigidbody2DAngularVelocityCtrl，按用户要求放入DrawLevel目录，保留迁移前脚本内容、类型与meta GUID；ApplyAngularVelocity应用配置、SetAngularVelocity(float)设置当前速度；运行态启用自动应用可关闭，没有每帧循环或对象查找。
- D077四个Kinematic机关分别显式绑定原刚体，先配置开场90°/秒。保留角阻尼0.05、约束None、原陷阱/碰撞/美术和D2/10秒失败/门链；不新增空壳D077控制器，不改旧关或共享脚本。
- 主工程Unity编译/重载、138锚点闭合/4原块修改/4新增/130原块保持、meta保持及12项真实UnityPhysics2D检查通过。正式地图/球与Actor/碰撞/门/暂停重建/设备仍uncertain，详见Level77结构与风险记录。
- 复盘：一次设置角速度与持续恒速职责不同；本组件不强制改刚体配置、不接管禁用后的物理推进，停止通过传0。保持最小必要组件，不引入每帧恒速封装或共享Level迁移。

## 2026-09-18：D079 首笔定距行走与足球动作

- 新增局部DLevel79Ctrl持共享Draw与显式Actor/足球，首笔只启动一次，按实际RB世界X行走2单位后左转；DLevel79ActorCtrl隔离主线84的ready两次/shoot一次，保留当前复制Actor的50度Draw层坡度限制。踢球开始切足球Dynamic并施一次可调世界方向/大小的冲量，不接主线84的UI、拖球或胜负规则。
- 轨道Complete只标记，关卡Update在未暂停时推进阶段；暂停冻结自有轨道，Reset/Resolved/死亡/禁用/替换解绑取消。未修改已验收D11、Level84ActorCtrl、共享Actor/Draw/UI或源prefab。D079足球原拖拽局部关闭，避免画线输入争用。
- 主工程编译/重载、171锚点/337引用闭合、167原块保持以及42项独立Unity真实物理/Spine状态机检查通过；共享Actor/Draw为桩且空动画时间线，正式地图、坡面/弹道/视觉、计时临界、完整重开/暂停和移动端仍uncertain。见Level79结构、功能风险记录和外部Temp证据。
- 复盘决策：不同玩法只借用动画序列，不直接复用缺少阶段回调/取消边界的旧异步流程；保持新关局部Ctrl+Actor子类。局部坡度重复与动画推进共享化保留为候选，尚不足以升级共享接口或迁移旧关。

## 2026-09-18：D080 首笔敌人踢球与玩家假扑救

- DLevel80Ctrl复用首笔Handler与局部Resolved，两个DLevel80ActorCtrl实例分别持足球动作与jump_up/down视觉扑救。shoot开始一次可调世界方向/冲量，同帧扑救到offset最高点后归位；角色不自动行走，未复用D79坡度或主线84选项/拖球流程，不改旧关/共享Actor。
- 原球门目标区挂D80足球身份失败Handle并改名FailArea；球实体Collider允许Draw物理阻挡，保留原层/质量/重力/阻尼与10秒坚持成功。Enter/Stay→共享TryFailAndReset，不新增全局事件。共享暂停仍有物理推进，暂停入区记录待失败，恢复提交、Reset清除。
- 实际主DLL/API外部编译0错，45项独立Unity物理/Spine检查通过；194锚点/387引用闭合，186原块保持。主编辑器当时尚未编译新增脚本并报告Unknown，需停止播放重编译后重建；正式渲染/地图/弹道/计时竞争/重开/设备仍uncertain，见Level80两份记录。
- 复盘：相同本关子类两个实例足以分角色动作，保持局部机制与结果所有权；暂停碰撞记录是本关失败必要边界，未升级共享接口或迁移旧关。D079用户并发地图保存保留，仅脚本保护哈希作为本次不改旧代码的证据。

## 2026-09-19：D080脚本丢失恢复

- 用户确认本次返修为脚本丢失。磁盘文件、GUID和外部编译存在不足以证明主Unity已经注册：Hot Reload关闭自动刷新，主RSP/csproj原来没有D80，日志报告文件不属于项目。结束播放后完成实际主Csc编译/域重载，主DLL包含三个D80类型。
- 最新保存的prefab已撤掉根Ctrl、两个局部Actor和FailArea组件，逐块补回四个组件与必要stripped引用，使用当前已有扑救视觉Transform；足球Simulated恢复1。保留当前布局/坐标/画线参数，三个动作脚本未变；不修改共享接口、旧关、源Actor或插件/Packages内容。
- 本次181唯一锚点、引用闭合，五个原块变更/十块补回/166其它原块保持；主D080重新导入无Missing提示。Inspector逐组件与正式地图动作、弹道、失败重建、设备仍uncertain，不能把导入成功等同于整关运行验收。
- 结构复盘：本关Ctrl/Actor/失败Handle拆分继续保留。交付新增MonoBehaviour需补主编辑器编译/导入证据的候选进入AI规则提升队列；不新增强制刷新工具、不直接改只读Hot Reload包或批量迁移已验收关卡。详见Level80结构与风险补充。

## 2026-09-19：D080 Actor Spine基础倍率配套

- 用户保存Actor SkeletonAnimation.timeScale=0后，本关不能只设置TrackEntry.TimeScale=1：Spine Update会乘基础倍率0。新增本关序列化Skeleton引用，扑救临时恢复1，完成/取消恢复原倍率；暂停仍只冻结自有轨道。显式绑定既有组件，enemy留空，不改共享Actor/Spine或源prefab。
- 70项隔离真实SkeletonAnimation/Spine/物理检查通过，主工程Assets Refresh编译与域重载通过，主DLL字段元数据确认新增引用与缓存。共享Actor/Draw仍为桩、空动画；正式视觉、其它轨道/恢复0后待机死亡、重建与设备为uncertain。
- 结构复盘：本关动画子类同时拥有动作与临时视觉/时钟状态；保留局部，不引入公共Actor时间倍率API、统一时钟封装或旧关迁移。临时全Skeleton倍率会影响同对象其它轨道，按本关当前机制记录边界，后续有稳定多关证据再评估共享能力。

## 2026-09-19：D080真实跳跃、接地与足球调参复盘

- 用户追加落地保留位置/正常idle、足球轻弹及水平滚动；D80局部Actor API改真实刚体跳跃，沿重力算初速，按真实过顶点/接地结束；开场0后恢复正常Spine倍率。首笔前及蓄力/跳跃期间补完整接触和共享落地状态更新，不动已验收Actor/旧关。
- 真实跳跃暂停新增局部速度/约束缓存和FreezeAll，保持Collider/模拟，不将共享Draw暂停当成物理暂停。Ctrl仍拥有一次施球力和结果门禁，FailArea仍拥有球身份与暂停暂存失败；没有新增共享接口、足球脚本或全局事件。
- 球材质局部隔离低摩擦/Minimum与用户当前弹性/Maximum，Continuous避穿插，保留用户最后阻尼及方向/力度。零弹性方案被用户反馈否定，最终以Level80最新结构/风险记录为准。
- 结构候选：接地行为验证不能只用空HandleCollision的Actor桩；此次使用共享ActorCtrl逐字源码与真实Physics2D覆盖调用边界、空中/落地、暂停与idle，记录证据范围。保留为验证候选，不因此改共享Actor、迁移旧关或增加通用物理控制抽象。

## 2026-09-19：D081 首笔后三向循环直线陷阱

- 新增本关局部 `DLevel81Ctrl`，第一笔有效画线后按 Prefab 显式数组顺序循环上、右、左三个 `L116ObstacleVisualTemplates`；每轮由对应 RedDown 模板固定闪烁 2 次，再默认以 `3 / 0.2s / 速度10` 连发，末枚到下一预警默认等待 `1s`。
- 运行时条目复用 `ObjectPoolManager`，三个现有 RedDown 模板轮换作为克隆来源；池条目采用 Kinematic Trigger 和 Actor-only LayerOverride，沿 `-directionRoot.up` 直线移动，不与 Draw 绳子或其它场景层交互。
- 共享 `OnTriggerTrapHandle` 的 `isDead` 没有池重置入口，因此只在本关模板/克隆上停用，不修改共享脚本；命中由局部 Ctrl 去重并复用既有死亡、击退、音效、粒子和 `OnTriggerTrapEvent` 失败链。
- Pause 冻结本关计时与主动位移，Reset/Resolved/Disable 停止循环并回收全部活动陷阱；不新增全局事件，不修改 DLevel5、Level116、共享 Draw/Actor/ObjectPool 或其它旧关。
- 主 Unity 曾完成新增脚本的 Csc/域重载，最终源码另以相同 Unity Roslyn 和当前主 RSP 全量编译 0 错误；主编辑器尚未对最终结算清场小改动再次刷新。Prefab 162 锚点唯一且 3 个发射器引用闭合；正式 PlayMode 的完整循环、绳子隔离、闪烁观感、命中重建与移动端仍 `uncertain`。对象池触发机关暂保留为关卡局部模式，等待多个关卡出现相同职责和完整回归证据后再评估共享抽象。

## 2026-09-20：D081 发射顺序改为从左起顺时针

- 按用户追加要求，仅调整 `DLevel_081.prefab` 中 `DLevel81Ctrl.launchers` 的显式数组顺序为左侧 `L116ObstacleVisualTemplates (2)` → 上方 `L116ObstacleVisualTemplates` → 右侧 `L116ObstacleVisualTemplates (1)`，之后回到左侧。
- 三个方向根的局部位置分别为 `(-0.76, 0.73)`、`(0, 1.53)`、`(0.76, 0.73)`；结合既有旋转与 `-directionRoot.up`，发射方向依次为世界向左、向上、向右。未修改 C#、对象池、闪烁、数量、速度、轮内或轮间计时。
- 当前 Prefab 168 个 YAML 锚点唯一且非零本地引用全部闭合，生产脚本哈希保持；正式 PlayMode 顺序与画面观感仍 `uncertain`。顺序继续由 Inspector 数组拥有，不新增共享排序规则或运行时位置扫描。

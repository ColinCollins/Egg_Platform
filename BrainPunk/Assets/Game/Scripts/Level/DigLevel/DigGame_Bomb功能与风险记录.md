# DigGame_Bomb 功能与风险记录

## 当前使用：烧焦后左右飞出

- Actor 在炸弹左侧向左上飞，右侧或同 X 向右上飞；Y 相对位置不选方向。参考 Level50 的 `(±0.5, 0.7) × 力度` 与旋转表现，默认力度 50，即初速度 X=±25、Y=35。角色烧焦后飞出，保持原 Scale、Collider 尺寸和 SceneLayer 200。
- 在 Bomb Inspector 调整“爆炸击飞力度”（50）、“爆炸下坠加速度”（20，0 为直线斜飞）、“爆炸旋转速度”（720 度/秒，0 停止旋转、负数反向）和“爆炸烧焦颜色”（默认 RGB 0.08/0.065/0.055）。范围半径与持续时间沿用原参数。
- 直接碰撞、爆炸范围和后来进入的 Actor 使用同一死亡表现，取接受死亡的那颗炸弹为中心。Spine 只改实例 RGB、保留透明度和材质；Body 旋转由同一个死亡 Tween 控制。整个角色飞出相机边界后才完成死亡回调，结算仍等待原爆炸最短持续时间。
- `DigActorCtrl.ExplosionDeath.cs` 为本玩法手写 partial；Actor Prefab 显式绑定已有 Spine 和 Renderer，Renderer 用于保守计算各旋转角度的完整离屏范围。其它失败来源继续沿用原 Actor 上跳／下坠参数，不使用炸弹配置。Level50、Level9 和共享 Actor/Spine 脚本均未修改。

## 2026-10-16 修改前核对

- 需求与行为基线：用户已放入 DigGame_Bomb Prefab（根 Rigidbody2D、子节点 CircleCollider2D、引信 Animator、ExplosionRoundFire），尚无炸弹玩法脚本；新增接触 Actor／其他 Bomb 引爆、圆形持续检测、连锁、特效及爆炸音效。用户说明其它可触发对象后续补充，本轮限定 Actor 与 Bomb。默认世界半径 1.5、持续 1 秒，Inspector 可调，Gizmos 同源显示。
- 来源：DigGame_Bomb.prefab 的 Collider 5689527353767355252、刚体 5739807779014156404、嵌套特效 7089554941558003595；DigLevel_008.prefab 的 Bomb 实例 4883431619420888930 和关卡控制器 6695274849859263318。DigFailAreaHandle.ReportContact、DigGame_LaserEye、DigLevelCtrl.TryFail/BeginFailure/ApplyPause、AudioManager.PlaySound 及 SFX_Generated 的 rocketExplosion。
- 原流程及维护边界：TryFail 负责角色归属／已到达保护、唯一结果锁、死亡演出及现有失败重开；暂停不靠全局 timeScale。爆炸源根粒子 stopAction=Destroy，须仅在 Bomb 的嵌套实例覆盖为 None，避免本地引用被自动销毁。新脚本手写，无生成模板。资源均为游戏侧用户已有 Prefab，不改共享特效源、AudioManager、关卡控制器、SDK、存档或打点。
- 拟修改：新增 DigGame_Bomb 并显式绑定已放置组件；008 补所属关卡绑定。爆炸前记录 Collider 世界中心，关闭实体/引信、播放本地粒子和 rocketExplosion；以复用 List 的 Physics2D.OverlapCircle 即时及每物理步检测，先锁定自身防重复，允许同关连锁在失败结果锁定后完成。直接 Actor 接触仍保证半径很小时也能触发失败。失败最短等待使用爆炸剩余时间，保留既有死亡路径并让检测窗口完成。
- 生命周期：未初始化／暂停／后台冻结炸弹物理和引信；爆炸后仅暂停冻结粒子与计时，结果锁不截断已发生的连锁；未爆炸对象在结果锁后停止模拟。关卡重建产生新实例，禁用清理粒子，无全局事件、运行时搜索或动态资源创建。原 Scale、Collider 尺寸／Offset／Trigger、重力与材质保持。
- 验证：真实 Unity 隔离环境检查根刚体接收子 Collider 的 Collision/Trigger，Actor 与 Bomb 接触、圆内／圆外／晚进入／过期、连续连锁与重复去重，暂停／后台／结果锁／禁用重建、中心与不均匀缩放；检查 Prefab 引用及真实粒子播放、音效请求次数，Editor/Android 条件编译。正式 Level008 渲染／听感与 Android/iOS 真机仍 uncertain，隔离音频仅验证调用。
- 回滚：移除新增脚本和文档及 Bomb 组件／嵌套 stopAction 覆盖、008 的所属关卡 override；不得回退用户已有地图、摆放、动画、Actor/Germ 改动。本次前镜像存于 TEMP/brainpunk-dig-bomb-20261016，仅作差异核对，不整文件覆盖用户后续编辑。

## 爆炸检测、资源与使用

- `DigGame_Bomb.cs` 已挂到 `Assets/Game/Prefabs/DigLevel/DigGame_Bomb.prefab` 根节点。已绑定原根刚体、实体 CircleCollider2D、外观、引信 Animator 及 ExplosionRoundFire 的五个粒子；只在此嵌套实例将根粒子的 stopAction 改为 None，共享特效源不变。逐个 Play/Pause，恢复时不会重播已结束的粒子。
- 现有 `DigLevel_008.prefab` 的 Bomb 实例已显式绑定本关 DigLevelCtrl。复制到其它关卡时，在 Inspector 将“所属关卡”绑定到新关卡的 DigLevelCtrl；不在运行时搜索或自动绑定。
- Inspector 调“爆炸半径”（默认 1.5 世界单位）和“范围持续时间”（默认 1 秒）。选中 Bomb 并打开 Scene 的 Gizmos，会看到橙色 XY 圆；其中心和半径与物理检测一致。非均匀 Scale 不改变配置半径，爆炸中心仍取 Collider 的实际世界中心。持续时间设为 0 时只检测瞬间。
- 普通碰撞和 Trigger 均支持。Actor 接触或同关 Bomb 接触引爆，单颗只触发一次；爆炸瞬间和持续期每个 FixedUpdate 做 OverlapCircle，范围按 Collider 与圆相交判定，不要求目标中心进入。支持持续期后来进入的 Actor／Bomb 和连锁，不按沙土遮挡截断范围；Germ 与其它机关留待后续要求。
- 爆炸隐藏原外观和引信，关闭实体碰撞与物理模拟，保持根对象承载粒子及范围。XY 播放位置取爆炸中心，调用 `AudioManager.PlaySound("rocketExplosion")`，遵守已有音效开关。Actor 经 `DigLevelCtrl.TryFailFromExplosion` 进入本轮烧焦左右飞出演出，再接既有统一失败流程；不重复提交失败，不增加独立重置、广告或打点入口。
- 未初始化、暂停／后台冻结物理和引信；暂停中的爆炸冻结粒子及范围计时。结果锁冻结未爆炸物理，但不中断已经发生的爆炸链。退出清理粒子和检测时间，Reset 沿现有关卡销毁重建恢复新实例。

## 验证结果与风险边界

- 原生 Unity 6000.0.59f2 隔离场景 40 项通过：实际 Bomb Prefab 引用、根刚体收到子 Collider 的普通碰撞／Trigger、极小半径直接接触、三颗连锁／同物理步 Actor+Bomb、圆外／边缘相交／晚进入／过期／零持续、一次音效请求、暂停与后台、线速度及角速度恢复、已结束粒子不重播、非均匀缩放中心、未初始化／异关／已胜利保护、禁用与重建。
- 使用真实 ExplosionRoundFire 资源验证五个系统播放并产生非零粒子；使用真实 PlayerLoop／DOTween 验证死亡演出完成仍等待一秒最短时间，之后唯一 Failed 回调。音频在隔离环境用记录桩核对 `rocketExplosion` 和次数，不代表正式项目听感验证。隔离环境缺正式渲染素材，未据此声称正式关卡视觉正确。
- 首轮“角速度必须精确等于 30”测试失败；实际恢复读数为 `30.0000019`，线速度 `(2,3)`。改测试为 `0.0001` 浮点容差后通过，未改生产速度逻辑。首轮日志保留。
- Editor 与 Android 各 1227 源文件条件编译 0 错误（113／118 个既有警告）；未做 iOS 编译或 Android/iOS 真机。生产 Bomb 与测试副本字节一致。Prefab 前后差异核对仅增加组件、绑定和嵌套粒子覆盖；原有 Transform、Collider 参数及动画源未修改，008 仅增加所属关卡 override。代码 diff 检查通过。
- 证据：`C:/Users/admin/AppData/Local/Temp/brainpunk-dig-bomb-20261016/` 下 `checks.txt`（PASS 40）、`final-unity.log`、`compile-editor.log`、`compile-android.log`、`prefab-checks.txt`、两个 `.prefab.diff`；原始断言日志为 `first-checks.txt`、`first-unity.log`，精确浮点读数见 `velocity.txt`。Python 打印中文到终端曾发生解码乱码；证据文件均按 UTF-8 写入并用 PowerShell 显式 UTF-8 重读，不以那段终端乱码为证据。
- 剩余风险（uncertain）：正式 Level008 的美术遮挡、爆炸大小与音效听感，以及手机后台／恢复未实测。最短复查：运行 008 让 Actor 碰炸弹；再摆两颗 Bomb 相碰，检查连锁、后来进入范围和暂停恢复。持续检测遵循物理步，极高速对象穿越整圈仍可能越过采样；本轮未引入扫掠检测、炸土、冲击力或通用机关接口。
- 回滚按前置章节逐项撤销本次新增引用和组件，保留用户已有 Bomb Prefab、008 关卡、美术／动画及其它并行修改。本任务未提交 Git。


## 2026-10-16 爆炸命中跳跃参数：修改前核对

- 需求：Actor 被 Bomb 命中后的上跳表现可在 Inspector 调整；直接碰撞与持续范围命中都要生效。当前 Bomb.ReportContact／DetectBlast → DigLevelCtrl.TryFail → BeginFailure → DigActorCtrl.PlayDeath，使用 Actor 通用序列化值：高度 0.6 世界单位、上跳 0.18 秒、下坠 0.55 秒。Actor Prefab 与当前关卡没有这些参数的其它覆盖（本轮资源检索）。
- 保留基线：现有死亡保持尺寸、SceneLayer 200、上跳 OutQuad／下坠 InQuad、按相机与世界碰撞中心计算离屏终点；死亡完成与最短失败等待均结束才提交唯一结果，暂停／后台和退出取消规则保留。Trap、FailArea、墨水、超时、大小失败、激光等调用仍读取原 Actor 参数。
- 修改位置及边界：Bomb 新增三个中文注释和 Inspector 标签的序列化参数并写入现有 Prefab；两个命中入口传当次参数。DigLevelCtrl 与 Actor 保留原方法重载，新增内部重载仅透传可选值，默认路径传 null 读取 Actor 原配置，不回写 Actor 字段、不缓存上次 Bomb 参数。对非法自定义值在锁定失败之前拒绝。无生成器／模板（Actor partial 为手写）、无 DB／云控／资源加载／打点入口变更。
- 验证：真实 Unity／DOTween 检查默认与自定义高度、两个阶段时长、圆形范围／晚进入命中、直接碰撞、暂停、离屏与完整结束回调；回归普通失败沿用 Actor 自定义参数、FailArea 跳过上跳、被拒绝失败不残留覆盖、爆炸既有 40 项。Editor/Android 编译。正式 Level008 观感、手机实测仍 uncertain。
- 回滚：仅撤本次 Bomb 三字段及命中参数、Level／Actor 新内部透传重载和 Prefab 三字段；保持上一轮 Bomb、用户已修改的关卡和成长／吞噬逻辑。修改前副本在 TEMP/brainpunk-dig-bomb-hop-20261016。


### 用户追加后的实施依据：烧焦、按相对方向飞出（替代本轮未交付的上跳方案）

- 用户明确将 Bomb 死亡改为烧焦，并参考 Level50 按爆炸相对方向飞出场外；上一节的上跳三参数尚未交付，本轮移除该临时透传实现，不保留两套炸弹死亡配置。
- 参考事实：MainLevel/Level_50/Level50Ctrl.PlayKnock 调 Die、Knockback((-0.5,0.7),50) 与 Body 每 0.5 秒旋转 360 度；ActorCtrl.Knockback 实际赋值为 direction*force，Level50 未实现烧焦。Level9ActorCtrl 使用 Skeleton 实例 RGB 炭黑且保留 Alpha，可作为烧焦方法依据。未找到 Level50 本地 Markdown，飞行依据以当前代码为准。
- 最终修改边界：DigActorCtrl 新手写 ExplosionDeath partial，显式绑定已有 SkeletonAnimation，只在炸弹死亡修改实例 RGB 并旋转 Body，不修改材质／原始 Scale／Collider尺寸。Bomb 暴露飞出速度、向下加速度、旋转角速度、烧焦色，向量按 Actor 世界碰撞中心减爆炸中心，同心时向上。按相机范围及角色显示包围距离求首次完全离屏的时间，用同一受暂停控制的 Tween 推进抛物线和旋转；保留原死亡完整演出+最短等待的统一失败提交。
- LevelCtrl 增加明确的局部爆炸失败入口，复用原参与角色／结果／暂停门禁与结束清理，只给当前失败传值。普通死亡方法保持原实现。Actor Prefab 补 Spine 显式引用；Level50、Level9、共享 ActorCtrl／ActorSpineCtrl、Spine资源／材质只读。
- 验证调整：八方向、同心、极近／远边缘、大小与Offset／非均匀Scale、零重力／不同速度、旋转和烧焦RGB/Alpha、完全离屏才回调、菜单/后台与退出重建；直接碰撞／范围／连锁命中使用各自爆炸中心，普通Trap/FailArea回归原上跳/直落且不变黑。原生隔离运行与编译分别记录，正式关卡和手机仍 uncertain。

- 离屏包围范围补充：Actor 另显式绑定已有 MeshRenderer，用其实际世界 Bounds 及 Body 旋转轴到根的距离覆盖旋转后的显示范围；与 Collider 范围取较大值，避免只按碰撞半径提前结算。只绑定已有组件，不扫描层级。

### 用户最终方向简化

用户要求只分左右，不需要八方向。本轮最终方向仅按 Actor 世界碰撞中心相对爆炸中心的 X 判断：左侧用 (-0.5,0.7)，右侧及同 X 用 (0.5,0.7)，乘 Inspector 的击飞力度，默认 50，与 Level50 初始速度比例相同。爆炸中心的 Y 不影响左右选择；保留重力、旋转、烧焦色配置。验证改为左右、同 X，以及 Actor 在炸弹上／下方时仍向上抛的场景；前述八方向仅为用户简化前的验证过程，不是最终运行规则。


## 最终交付验证：烧焦与左右上抛

- 最终方向仅左右上抛，`DigActorCtrl.ExplosionDeath.cs` 按 Actor 世界碰撞中心 X 与 Bomb 中心 X 比较；水平初速度为 ±0.5×力度，竖直初速度为 0.7×力度。同 X 选右上。默认力度 50、向下加速度 20、旋转 720 度/秒，烧焦色 RGB=(0.08,0.065,0.055)。原上跳三字段及透传方案已移除，不存在两套生效配置。
- 新增专用 partial 与 `.meta`；Bomb 参数、LevelCtrl 内部 TryFailFromExplosion／演出分支、Bomb Prefab 参数及 Actor Prefab 的两个显式引用为最终生产改动。`DigActorCtrl.cs` 的原 PlayDeath 和所有旧注释与本轮前副本归一换行后完全相同。原角色 Scale、Collider 尺寸和 Offset 不写入；根移动、Body 旋转、Spine 实例 RGB 按当前失败演出变化。前后状态与用户已有其它改动分开保存，未提交 Git。
- Unity 6000.0.59f2 隔离专项 **56 项通过**：左右／上下来向仍仅按 X 选择、同 X、默认轨迹与旋转、自定义力度／零重力／反转、直接普通碰撞与范围及晚进入、烧焦实例 RGB/Alpha/材质、真实 Spine 已参与渲染的网格顶点变黑、Scale/Collider 参数不变、完整 Renderer 离屏与一次失败回调、暂停／后台／退出／重建、非法值拒绝、非均匀缩放、无 Spine 旧角色及普通 Trap／FailArea 原演出。
- 既有 Bomb **40 项回归通过**，包括实际碰撞和 Trigger、范围内外与过期、连锁、重复音效请求、暂停、粒子及真实 PlayerLoop 等待后唯一 Failed。两组覆盖不同检查点，不将隔离结果说成正式关卡或手机验证。
- Editor/Android 各 1228 源文件条件编译 0 错误，既有警告 113/118。生产脚本与测试副本字节一致，Actor Prefab 仅新增指向现有 Spine/Renderer 的绑定。未修改 Level50、Level9、共享 ActorCtrl／ActorSpineCtrl、SDK、统计或共享材质。
- 验证夹具修正：隔离工程无 Spine/Skeleton Shader，改用其已有 Sprites/Default 作网格测试；真实接触会先产生物理分离（该次 Actor 初始 X=0.0246439986），轨迹断言改为从实际接触后位置计算；Spine 顶点缓冲有 41 个未引用白色顶点，颜色断言限定真实三角形引用的顶点，不能把未渲染缓存当成白色角色。上述修正均未改变生产演出，失败记录保留。
- 证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-dig-bomb-hop-20261016/`，`flight-checks.txt`（56）、`final-flight-unity.log`、`bomb-checks.txt`（40）、`bomb-regression-unity.log`、`compile-editor.log`、`compile-android.log`、`source-and-prefab-checks.txt`。中途八方向验证只保留于 `superseded-radial-checks.txt`，不代表最终规则。
- 剩余风险（uncertain）：正式 Level008 渲染与实际手感、Android/iOS 运行和原生覆盖层未实测；本次沿用关卡原有暂停及失败重开入口。最短复查：从炸弹左侧／右侧触碰，观察烧焦旋转飞出；暂停再恢复；重开确认恢复原色。不同美术的 Renderer 包围范围采用保守计算，允许演出在已不可见后稍后结束，以确保完整离屏。
- 最终回滚：删除新增 ExplosionDeath partial/meta；移除 Actor Prefab 的 burnSkeleton/burnRenderer 两引用；撤 Bomb 四参数及 TryFailActor 的专用死亡传值，恢复上一版 TryFail；撤 LevelCtrl 的爆炸入口和分支。保留 Bomb 检测／粒子／音效、用户关卡／Actor/Germ 配置与本轮前全部修改，不整文件覆盖。

## 2026-10-16 TriggerItem Layer 接触引爆：修改前核对

- 需求与基线：用户确认 TriggerItem 指 Layer。原 Bomb.ReportContact 只识别带 Rigidbody2D 的 Actor／同关 Bomb，静态 Collider 在入口被提前拒绝。目标是 Bomb 碰到 TriggerItem 层实体或 Trigger 后复用现有爆炸；保留 Actor 死亡、连锁、粒子／音效、暂停／结果锁与一次性门禁。
- 来源与维护边界：ProjectSettings/TagManager.asset 将 TriggerItem 定义为第 9 层；Physics2DSettings.asset 的层碰撞矩阵全开。DigGame_Bomb 的四个 Collision／Trigger Enter／Stay 回调汇入 ReportContact，再到 Detonate 和 DetectBlast。脚本为手写，无生成器；本次只改该脚本，不改层设置、Prefab、Actor 死亡或其它机关逻辑。
- 拟修改：Awake 缓存 LayerMask.NameToLayer("TriggerItem")；ReportContact 不再要求所有对象带刚体。已有 Actor／Bomb 优先走原类型和关卡归属门禁，其余对象按 Collider 所在 GameObject.layer 引爆，不按父节点、Tag 或名字查找。只让 Bomb 爆炸，不给 TriggerItem 增加通用爆炸回调或销毁行为。旧类摘要因新增触发类型而更新，其它旧注释保留。
- 验证与未决：用隔离 Unity 的真实 Collision／Trigger 覆盖静态／动态 TriggerItem、普通层不触发、父层与 Collider 层区别、重复接触、暂停后恢复、结果锁；回归 Actor／Bomb 直接接触与持续范围。Editor／Android 条件编译。正式关卡、实际音效听感和 Android/iOS 真机仍 uncertain；本次无依赖产品选择的未决项。
- 回滚：仅撤销本次 Layer 缓存和 ReportContact Layer 分支，恢复刚体入口检查；不回退已有 Bomb 或用户资源。修改前脚本与状态保存在 TEMP/brainpunk-dig-bomb-triggeritem-20261016。

### TriggerItem Layer 实现与验证结果

- `DigGame_Bomb.cs:65` 缓存 TriggerItem 层，`:145–164` 的接触入口保留原 Actor／Bomb 归属检查，再按实际 Collider 节点 Layer 引爆普通机关接触；没有 Rigidbody2D 的静态对象也支持。Collider 是实体或 Trigger 均可。设置时将挂 Collider2D 的 GameObject.Layer 设为 TriggerItem；只有父节点层不同不会自动继承。
- 沿用 Detonate 的 ExplosionRoundFire、rocketExplosion 和圆形持续检测；机关不会被脚本额外销毁，也不因接触直接结束关卡，范围命中 Actor 才进入原死亡流程。没有修改任何生产 Prefab、物理层矩阵、其它脚本、存档或打点。
- Unity 6000.0.59f2 隔离物理检查 62 项通过（新增 22 项，既有 Bomb 回归 40 项），覆盖静态／动态、双方 Trigger 情况、普通层不触发、父层与 Collider 层、重复、暂停恢复、结果锁、范围连锁及 Actor 死亡。使用生产 TagManager 和 Physics2DSettings 的隔离副本；生产脚本与测试脚本 SHA256 一致。Editor／Android 各 1228 源文件编译 0 错误，既有警告 113／118。
- 首轮测试机关中心放在 X=1，箱体半宽 0.2，而炸弹实际半径为 1.01×0.6=0.606，双方没有接触；测试位置改为 X=0.7 后通过，未为此改生产检测。首轮失败记录保留，避免把测试摆放错误当成功能修复。
- 证据：`C:/Users/admin/AppData/Local/Temp/brainpunk-dig-bomb-triggeritem-20261016/` 内 `bomb-checks.txt`（PASS 62）、`final-unity.log`、`compile-editor.log`、`compile-android.log`、`first-checks.txt` 和 `first-unity.log`。定向差异空白检查无报错；未提交 Git。
- 剩余风险：正式关卡摆放／碰撞体局部 Layer Override、渲染听感和手机实测仍 uncertain。最短复查为将实际机关 Collider 节点设为 TriggerItem 后运行，让 Bomb 接触该对象。回滚按上一节仅撤本轮 Layer 分支和缓存。

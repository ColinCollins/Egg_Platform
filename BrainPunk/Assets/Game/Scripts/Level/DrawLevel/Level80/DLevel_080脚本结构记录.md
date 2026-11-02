# DLevel_080 脚本结构记录

## 需求、基线与范围

2026-09-18：有效首笔后 enemy 蓄力踢球，给足球可调方向/大小的向右冲量；Actor 做一次带可调跳跃offset的假扑救；足球进入FailArea失败。本次交付动作与失败判定，不重新定义10秒成功规则或地图。

参考中央Draw结构规范、D079首笔足球阶段推进与D070 Actor子类动画保护、主线Level84ActorCtrl的ready两次/shoot一次。上述参考不作为正式D080已验收证据；本关完整运行状态为uncertain。

修改前根节点只有DrawLevelCtrl、DrawLineFeature、10秒且isSuccess=1的倒计时；Actor为D79子类，enemy为主线84子类，无D080首笔驱动。球Kinematic/Layer11/Trigger，主线DragableItem已禁用，原球门SuccessArea只有Box Trigger，无成功事件组件。

## 最终脚本树与职责

```text
DLevel_080
├─ DrawLevelCtrl / DrawLineFeature：共享输入、尝试与结果
├─ DrawLevelCountdownCtrl：保留10秒坚持成功
├─ DLevel80Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
├─ Actor → DLevel80ActorCtrl : ActorCtrl（假扑救）
├─ Enemy → DLevel80ActorCtrl : ActorCtrl（足球动作）
├─ football → 原RB、CircleCollider、Trail等组件
└─ qiukuang/FailArea → 原Box Trigger + DLevel80FailAreaHandle
```

DLevel80Ctrl只处理局部首笔、动作推进、一次球冲量与Reset；Actor子类只拥有本关动画与视觉位移；FailAreaHandle只识别显式足球RB并调用共享TryFailAndReset。三个脚本均有运行时入口保护，不新增节点查找、编辑态初始化、全局事件或共享UI代码。

## 生命周期与调用链

1. 同关DrawLevelCtrl分发IsFirstStroke；D80验证运行时、暂停/结果、角色与球绑定，保存首笔初态并让enemy原地蓄力。重复首笔与后续笔画不重播。
2. 敌人两次非循环football_shoot_ready后开始football_shoot；Complete只记标记，未暂停TickAction切换阶段。仅踢球开始帧返回true。
3. 同帧足球转Dynamic，施加 `normalized kickDirection × max(0,kickImpulse)` 的一次Impulse；Actor开始假扑救。球质量、重力和阻尼仍由原RB决定。
4. 素材没有名为jump的动画，实际为jump_up（0.5333秒）、jump_down（0.7667秒）。视觉节点在上升动画中sin插值至offset最高点，下降动画中cos回原位；offset为世界坐标，转换至父节点局部向量，兼容本关Actor的Y轴180度翻转。刚体/碰撞不随视觉扑救移动，不给Actor物理跳跃力。
5. 暂停冻结自有Spine轨道，不消费已完成阶段；Reset/Resolved/角色死亡或禁用/脚本禁用/外部轨道替换取消自有动画并恢复扑救视觉。只清自身仍占用的轨道，不清后来替换的动画。
6. FailArea的TriggerEnter/Stay校验attachedRigidbody==football；Actor、enemy和其它球不失败。足球进入后调用既有TryFailAndReset→Resolved取消动作→DrawGameResetEvent(Failed)标准重建，不直接新发全局事件。
7. 共享暂停不停止全部物理：暂停时入区记录待失败，即使已穿出，恢复后仍提交一次；FailArea实现既有Handler只接Reset清除旧记录，笔画回调为空。共享结果门禁防Enter/Stay重复与倒计时竞争。
8. 同实例Reset由共享Draw依次通知Ctrl与FailArea，取消动作/待失败、恢复首笔时角色和球的物理初态、ResumeMoving并等待新首笔。正常Reset/Replay保留既有整关重建。

## Inspector绑定与资源

| 组件 | 字段 | 当前绑定/初值 |
| --- | --- | --- |
| D80 Ctrl | drawLevelCtrl | 根900000000000000001 |
| D80 Ctrl | actor / enemy | 原局部900000000000000011 / 6798632622893770048，均替换D80 Actor子类 |
| D80 Ctrl | football | RB 5273557179364374802 |
| D80 Ctrl | kickDirection / kickImpulse | 代码初值(1,0) / 80；prefab保留用户调参，当前值以Inspector为准；方向长度不增加力度 |
| D80 Ctrl | jumpOffset | (-0.5,1)，扑救最高点世界偏移 |
| Actor | spineCtrl / saveVisualRoot | 原3586672669756989625 / 当前stripped Transform3482993190944510139，对应原source spine根4684527501708174571 |
| Actor | saveSkeletonAnimation | 原有stripped组件4659850596202083006，对应source SkeletonAnimation3602816233948530414；prefab开场timeScale=0 |
| Enemy | spineCtrl / saveVisualRoot | 原45745699073617605 / 0，不调用假扑救 |
| FailAreaHandle | drawLevelCtrl / football | 同根Draw / 同球RB |

角色局部组件ID不变，原Spine与辅助对象引用保留；不改源Actor prefab。D80不自动行走，因此撤下复制的D79坡度字段，不引入D79行走流程。两角色IsEditor保持0。假扑救视觉根与源push Collider为不同父级分支，原物理碰撞不随视觉offset位移。

原SuccessArea重命名FailArea并挂足球专用脚本；其Transform、Layer0、尺寸1.5×3.35和Trigger保持。足球Circle仅把Trigger改为实体碰撞，使现有物理画线可挡球；保留Layer11、排除Actor层2048、半径0.38、质量1/重力20/线性阻尼5等参数及已有禁用拖拽。全局矩阵当前允许11对0、14、6，未改全局物理设置。

默认SkeletonData仍使用sp_Box_001/Box_SkeletonData.asset；原JSON有两种足球和两段跳跃动画，没有为本关增加动画/事件素材。配置、DB、UI、资源加载和打点没有新增入口；失败/超时成功仍由共享链处理。

## 首版验证证据与缺口（2026-09-18，后续修复见下文）

- 主工程实际DLL/API编译：Unity6000.0.59f2附带Roslyn，按当前Assembly-CSharp响应引用再加主DLL编译三个新脚本，0错误；CS0649为外部Csc不识别Inspector赋值，实际prefab绑定已静态核对。
- 主编辑器尚未编译/重载D80：检查时主DLL仍为23:40:41，当前编译响应没有三个新脚本；Editor.log 1391284/1392883导入D080报告Unknown脚本。需停止当前播放让Unity重编译，再重建D080；不将外部API编译替代主工程完整重载证据。
- 最终prefab194唯一锚点/387本地引用闭合，5原块修改/3新块/186原块逐字保持。meta GUID仍e488e65f091eaa74a909f6ac682b528d。
- 45项独立Unity6000.0.59f2 PlayMode通过：逐字生产三脚本，真实RB/Collider/Physics2D.Trigger与Spine状态机，使用当前物理矩阵；覆盖首笔/重复/两次蓄力/一次力/方向力度、两段跳跃/最高点/180度镜像与非均匀缩放/归位、暂停、Reset/禁用/死亡/替换/Resolved、球身份/真实目标触发/重复/暂停穿区与待失败清除/缺踢球动画。
- 外部证据在 `C:/Users/admin/AppData/Local/Temp/brainpunk-dlevel80-20260918/`：before快照、protected-hashes.json、prefab-verification.json、API编译响应及输出、Probe.cs、editor-final.log、probe-result.txt。共享Actor/Draw在隔离项目为桩、Spine为空时间线，足球测试重力/阻尼为0，不作为正式弹道验收。
- 受保护C#、DrawPlayCtrl与源Actor哈希保持；D079 prefab出现用户并发地图保存差异，已保留，不归本次修改或回滚。知识库无D80专项记录；feature plan缺失、CodeGraph有索引但CLI/MCP不可用，以当前源码/YAML/JSON为证据。
- uncertain：主编辑器重载、实际Actor/Spine视觉和骨骼挂件、正式画线阻挡/球参数下弹道与可解性、10秒临界竞态、整关暂停/转场/Replay、存档打点与Android/iOS横屏未实测。

## 2026-09-19：脚本丢失修复与当前状态

用户明确本次问题为“脚本丢失”，本次恢复组件与导入状态，不重新设计足球动作。检查时三个脚本文件、类名和唯一meta GUID完整，但主编译响应与csproj没有D80源码；Editor.log记录Hot Reload的“File is not part of any project”及prefab的Unknown脚本提示。

可观察设置为kAutoRefreshMode=0、HotReloadWindow.AllowDisableUnityAutoRefresh=1、AppliedAutoRefresh=1；只读检查Hot Reload的RequiredSettingChecker.cs与EditorCodePatcher.cs确认其关闭自动刷新、跳过.cs和.meta的资产刷新。当前播放结束后主编辑器正式重编译，Assembly-CSharp.rsp第907—909行及csproj均包含三个脚本，主DLL于2026-09-19 00:05:31更新。直接读取该DLL的类型元数据，三个D80类型均为公开类；Editor.log的Csc、CopyFiles、Tundra build success和ReloadAssembly提供实际主工程编译/重载证据。

最新保存的prefab中，根Ctrl、两个角色的局部组件及FailArea组件均已移除，嵌套角色仍保留移除原ActorCtrl的记录。本次补回四个组件与必要的六个stripped对象引用；角色使用原局部组件ID与原Spine/碰撞绑定。假扑救改绑当前已经存在的视觉Transform3482993190944510139，不重复新建视觉引用。足球RB的Simulated从0恢复1，保持原Kinematic开场及踢球时转Dynamic，否则AddForce不会推进足球。

修复前171锚点，修复后181唯一锚点；本地引用全部闭合。仅修改根/FailArea组件列表、两个嵌套实例的AddedComponents及足球Simulated这五个原块，新增十块，166个其它原块逐字保持；包括当前Actor位置x=2.65、地图、美术、摄像机和画线参数。三份生产C#与之前45项隔离检查所用文件逐字一致，共享Actor/Draw、D79/主线84脚本和源Actor prefab保护哈希一致。未写入Packages或Assets/GF。

主编辑器重新导入D080，日志的artifact为9f896b2757b0f7e0659aa2ba8dc7f1f1；本轮编译/导入区间没有error CS或referenced script missing提示。这证明本轮主编译、类型存在和prefab导入范围；未实测当前完整关卡的踢球视觉、弹道、失败转场或移动端，仍为uncertain。没有通过Inspector逐组件截图或MonoScript.GetClass调用验证，不把静态绑定及无Missing导入提示扩展为整关运行通过。

补充证据仍在外部Temp同目录：DLevel_080.repair-before.prefab、DLevel_080.repair-final.prefab、repair-prefab-verification.json、repair-main-verification.json、repair-main-import-compile-evidence.txt。Editor.log持续增长，复查优先使用保存的摘录，避免只依赖可变行号。

收尾复核时工作区再次保存了prefab：踢球方向变为(0.5,1)、力度500，并有角色嵌套姿态/敌人位置和刚体参数调整；本次未回写或覆盖这些后续变化。上述166原块保持描述的是修复写入时的快照；收尾prefab仍181锚点、引用闭合、四个本关组件及视觉绑定完整，另存DLevel_080.editor-resaved.prefab供区分后续变化与本次修复。

本次修复回滚只逐块撤回上述五块改动、十个补回对象；不要删除现有视觉Transform3482993190944510139或覆盖其它地图块。repair-before快照记录的是组件已经缺失的状态，仅供比较，不建议直接恢复后继续运行。以后新增组件交付的主工程编译/导入验证候选记入AI规则提升队列；不修改共享脚本、旧关或插件设置。

## 2026-09-19：Actor Spine timeScale配套修改

用户补充已修改Actor的Spine时间倍率，需要配套逻辑。保存的D080中Actor SkeletonAnimation.timeScale=0；此前D80只调整TrackEntry.TimeScale，未调整SkeletonAnimation倍率。Spine源码SkeletonAnimation.cs第149—152行说明倍率相乘，第243—246行在推进状态前乘Skeleton倍率，因此仅把轨道倍率设1无法让配置为0的Actor开始扑救。

本次按已告知的默认方案实现：开场仍保持用户配置的0，PlayFakeSave通过新增[SerializeField] saveSkeletonAnimation显式持有同一动画组件，保存调用前倍率并临时设为1；完整完成、Reset、禁用、死亡/结果门禁取消及外部轨道替换时还原原倍率。暂停仍只冻结本关轨道，基础倍率不反复改写。RestoreVisual现在同时恢复视觉位置与原Skeleton倍率；敌人不使用扑救，字段留0，原足球动作不改。API原边界注释因新增必要绑定条件补充了Spine组件与倍率说明，无无关注释修改。

prefab只给两个原局部角色组件新增序列化字段，Actor绑定现有4659850596202083006，enemy留空，不新增对象或查找；保留用户当前timeScale、方向/力度、球材质、地图和角色参数。未修改共享ActorSpineCtrl、ActorCtrl、源Actor prefab或其它关卡。

验证：按主DLL/API编译修改后生产脚本0错误；70项隔离Unity6000.0.59f2运行通过，此次使用真实缓存spine-unity.dll的SkeletonAnimation.Update与Spine状态机，验证原倍率0/0.5/2在扑救时为1并于完成恢复、真实轨道推进、暂停/恢复以及Reset/结果/禁用/外部替换/死亡取消恢复0；同时保留之前首笔、足球、偏移及失败区检查。共享Actor/Draw仍为桩，动画为空时间线，正式美术与整关不在隔离验证范围。

主编辑器已执行一次Assets→Refresh，主Csc编译/域重载完成，DLL于00:13:53更新；直接检查DLL字段元数据可见saveSkeletonAnimation与_initialSkeletonTimeScale，避免仅依赖Hot Reload提示。补充证据为外部Temp中的ProbeTimeScaleProject、timescale-probe-result.txt、timescale-editor.log、timescale-api-check.rsp/输出、DLevel_080.timescale-before.prefab及timescale-main-compile-evidence.txt。原45项检查对应倍率配套前版本，不能再作为当前脚本逐字版本证据，当前以70项版本为准。

编译/域重载后D080再次导入，artifact为5b8aee807aa6a752aadc561b778b86a4，无Missing提示。随后主PlayMode日志可观察Enemy进入PlayFootballShoot及Actor进入PlayFakeSave的StopMoving调用；这证明正式组件已加载并进入动作入口，不能替代动画完整完成与视觉偏移实测。

uncertain：正式Actor动画、挂件和其它轨道、恢复倍率0后的待机/死亡表现、实际暂停与失败重建、整关弹道及设备未实测。动作结束或取消会还原调用前倍率；原值0时整个Skeleton重新定格，外部替换动画不会被清轨，但也受该基础倍率影响。本方案采用固定动作倍率1，若用户需要全程使用其它倍率，需相应调整本关策略。

本次配套回滚：仅撤回DLevel80ActorCtrl新增Skeleton引用/缓存及倍率保存还原，删除prefab两个saveSkeletonAnimation字段并刷新编译；保留用户原timeScale与之前已恢复的四个组件。回滚后倍率0的扑救不再自动推进。结构复盘继续保持本关Actor子类拥有动画与临时视觉/时钟状态，不增加共享接口或对已验收关卡迁移。

## 结构复盘与回滚

与D79不同，本关不行走，用同一D80 Actor子类的两个实例隔离踢球和纯表现扑救；复用既有Handler和结果API，不修改D79或主线84共享动作。暂停入区记录属于球触发失败的必要边界，保留局部，不新增共享事件或抽象。

回滚逐块移除新根Ctrl/FailArea组件及stripped视觉引用，恢复Actor的D79 GUID2b3d37f28a40c5c4d9fbdaf093b8700c与原坡度字段、enemy的主线84 GUIDf8c368c64087fd548a915a5e12ea9cbd；恢复SuccessArea名称与足球Trigger，再移除新Level80文件/meta。勿整份覆盖用户继续编辑的地图，before快照仅供比较。

## 2026-09-19：最终接地、真实跳跃与足球表现修订

本节替换此前“仅视觉跳跃并归位、结束恢复倍率0、足球零弹性”的行为结论；历史章节保留用于追溯。

### 最终脚本树、职责与调用

仍只有根 DLevel80Ctrl、Actor/Enemy 两个 DLevel80ActorCtrl、FailArea 的 DLevel80FailAreaHandle；新增本关 DLevel080_FootballGrounded.physicsMaterial2D，不新增足球控制脚本或全局事件。Ctrl持IDrawStrokeCompletedHandler及局部Resolved，首笔开始Enemy两次ready再shoot，shoot开始一次归一化世界冲量并调用Actor.PlayFakeSave。施力前清旧线速度，单次日志记录实际实例方向/冲量/初速度/质量/重力/阻尼；选中Ctrl的方向箭头只读取本地序列化数据，编辑器不初始化管理器。

首笔前双方调用RefreshContactState（共享HandleCollision+UpdateAirbornStatus）；蓄力等待时Actor继续只更新接触，保护football_save/timeScale0开场。动作期间持续接触/空中/落地更新，完成后继续共享OnUpdate。共享ActorCtrl与旧D79/主线84不改。

Actor.PlayFakeSave保持旧API名称，最终实现已改为真实Dynamic刚体跳跃：offset.y为正跳高，offset.x为同高度落地的预期水平位移；按当前重力求初速度。jump_up在真实Y速度不再上升时切jump_down，下降且接地结束，停水平速度并ResumeMoving/idle，保留真实落点；不再移动/还原spine视觉根。接地框可能提前少量判定，阻挡或不同高度地形会改变实际位移。很小正跳高不会因接地框一直命中而卡住。

Spine跳起临时倍率1，落地/取消恢复原正倍率；开场0改为后续正常1，避免idle/外部死亡动作冻结。暂停仅冻结自有轨道，并缓存跳跃刚体速度/原约束、设置FreezeAll；保持Collider及simulated，恢复继续，取消只释放约束而不复活缓存跳速。共享Draw暂停不会停止物理，故该局部冻结是新增真实跳跃必要边界。

### 显式绑定与资源/结果流

根Ctrl保留Draw900000000000000001、Actor900000000000000011、Enemy6798632622893770048、足球RB5273557179364374802。玩家saveSkeletonAnimation继续显式绑定4659850596202083006，Enemy留空；删除双方过时saveVisualRoot字段，不删现有视觉节点。FailArea仍按football attachedRigidbody身份Enter/Stay提交TryFailAndReset，暂停暂存结果，10秒坚持成功保持。没有新增UI/存档/打点/资源加载或原生覆盖流程。

足球用本关材质，当前用户弹性0.4/Maximum，摩擦改0.02/Minimum，避免默认地面摩擦覆盖低值；保留用户最后线性阻尼0.5、重力20、质量1及方向/力度。球开场Y从-3.85抬到-3.72避开地面重叠，Continuous替代Discrete，仍排除Actor层；真实扑救不会挡球。用户过程中修改过方向、力度、offset、阻尼、弹性并移除Bouncy 1，本次持续基于最新文件保存，不还原这些地图参数。

### 证据、范围与回滚

当前生产三脚本和共享ActorCtrl逐字复制到外部Unity6000.0.59f2项目，使用真实刚体/碰撞/BoxCast/落地状态/SkeletonAnimation；Draw、事件/音效/Tween为桩，动画为空时间线。外部实际主DLL/API编译0错；主DLL00:33:40元数据包含新暂停字段/方法并不再有视觉字段或旧离地标记，主Csc/CopyFiles/Tundra及重载日志已保存。正式实例日志方向(0.15,1)/100得到冲量及初速度(14.83,98.89)，质量1/重力20/阻尼0.5，可核对实际配置读取；该日志不证明整个轨迹等于该方向。

最终运行结果及锚点/源码匹配见外部 C:/Users/admin/AppData/Local/Temp/brainpunk-dlevel80-football-20260919/realjump-result.txt、final-verification.json、main-final-compile-evidence.txt；具体统计在功能风险文档收尾节。此前70/102项属于旧行为，不能混作当前真实跳跃结果。main-kick-evidence早期中文因日志编码混合存在乱码，数字向量及最终源码/材质证据仍可复核。

uncertain：正式骨骼/挂件、真实地形/画线挡球与跳跃、超大offset/冲量、完整暂停/失败/Reset转场及Android/iOS横屏未完成整体验收。Continuous不保证超高速Trigger不漏；不依赖全局100限速保证，手动物理测试中200/500仍保持其速度。正常Reset重建回Prefab开场倍率0；同实例OnDrawAttemptReset恢复位置/速度与门禁，已开始过扑救的正常播放倍率保持，不恢复足球定格姿势。

回滚逐项撤本关真实跳跃/暂停与接地更新、Ctrl一次清速/日志/Gizmo、本关材质与球Y/CCD变更，参考外部before快照；不得整份覆盖用户方向/力度/offset/阻尼/弹性或重建已移除的Bouncy 1。注释仅因视觉跳跃已变为真实刚体、倍率0恢复策略已变更而更新，其它原注释保留。

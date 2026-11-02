# Level 103 不要跳功能说明

## 2026-08-18 黄色范围脚本精简

- 背景：合并关卡控制器与飞门控制器中重复的生命周期清理，保持小狗、陷坑、尖刺、飞门、门禁和结算行为不变。
- 影响文件：`Level103Ctrl.cs`、`FlyingDoorCtrl.cs`。两个组件均保留 `OnDisable()` / `OnDestroy()`，只把各自原有的 Tween/Sequence 清理和事件/点击解绑集中到私有 `CleanupRuntimeState()`。
- 前后差异：清理语句、执行顺序及回调覆盖范围不变；未修改 Domain、Prefab、动画、资源、UI、关卡参数、打点或重置/成功链。
- 验证：`Assembly-CSharp.csproj` 编译通过（0 错误），目标 diff 检查通过。
- 回滚：将两个私有清理体分别内联回原生命周期回调即可。

## 背景与目标

Level 103 主题为「不要跳」（PRD：`docs/issues/level103-dont-jump/PRD.md`；领域语言：`docs/contexts/levels/103/CONTEXT.md`）。全部场景事件以诱导玩家主动起跳为目标，而玩家按下上跳键即触发跳跃违规：头顶刺石（`Sp_l103_dj_01`）瞬现并固定于起跳点上空 spikeHeadOffset=2 处不再跟随（大于双方半高和约 1.38），主角上升、头部真实撞击刺石（trigger 碰撞体 `SpikeStoneTrapHandle`）时在接触点播放 `Knock_Explosion`（本关实例 `SpikeStoneImpact_103` 已禁用子节点 `Knock 1`/`KNOCK!` 文字层，根星爆保留）并同帧播放 `trap` 与 `failed`，再沿主角既有死亡链走默认失败重开。真实解法是点击悬停门体或任意翅膀；双翼直接垂直上飞，门体在点击同帧垂直落地，落稳后走进门沿既有门触发链通关。

第一屏根级另有常驻 `LevelTitlePlaceholder`，只读复用 Level 083 同款 `Tips.prefab`，显示内容暂由 `S_LevelTips_Des_level_83` 占位。它不是「不要跳」规则预告，也不表示本关正式标题已经确认。

## 组成与职责

- **`Level103Ctrl`**（本目录，全局命名空间，组合形态 Level 083 先例）：本地玩法编排器。通过 `[SerializeField] BaseLevelCtrl levelCtrl` 引用 Prefab 根节点基础控制器，根节点既有接线（actor / successAnim / enterDoorAnim / gamePlayPanelName）保持不动。负责事件流编排（含本关私有的小狗直线冲锋 Tween）、跳跃即死表现与死亡链接入、陷坑塌陷演出与自动上跳位移、门前地面尖刺破土演出。
- **`FlyingDoorCtrl`**（本目录）：飞门起飞/悬停与点击落门编排。门体或任意翅膀按下后，双翼直接垂直上飞，门体同帧垂直落地；落稳后恢复进门触发器。
- **Domain 状态机 `Level103DontJumpRule`**（`Domain/`，程序集 `Game.Level103.Domain`）：纯规则判定核心——跳跃即死判定、陷坑完全离屏与延迟违规结算、坑洞/门事件编排门禁（2026-08-06 新增 PitRisingBack 回升阶段与 `NotifyPitRiseStarted` / `NotifyPitRestored` 半落回升链路与复原标记）、飞门点击落地状态机，以及不占阶段的门前地面尖刺一次性破土门禁（`TryEmergeFloorSpikes`）与飞门起飞一次性门禁（`TryLaunchDoorFlight`，2026-08-07 新增）。小狗冲锋/完成状态与坑洞阶段独立：小狗回调不再推进或阻塞坑洞阶段。运行时两组件只做编排与表现适配，所有阶段迁移都经 Domain 的 Notify/Try 入口。坑洞在首次接近后保持一次性，缺口打开后任何阶段再次掉入都回到 InPitAutoBounce 并记录掉入前阶段，脱困后原路返回（首次掉入或复原后脱困进入 ApproachingDoor），飞门剧情不重复触发。
- **EditMode 域测试**（`Tests/EditMode/`，36 例）：既有资产按当前项目限制保留且本任务不修改或执行。其小狗→陷坑严格串行、拖门成功路径及点击后的旧 `DoorWingsDeparting` 阶段顺序断言均属于旧合同，不能作为当前小狗/陷坑解耦或点击并行落门行为证据。

## 规则通路分离（本关最关键约束）

玩家起跳输入与脚本驱动位移在代码结构上严格分离：

- 玩家起跳输入：`PlayerJumpEvent` → `Level103Ctrl.OnPlayerJump` → `rule.NotifyPlayerJumpInput()`（Domain 唯一违规登记入口）。可见阶段刺石瞬现并固定于起跳点上空（不再跟随），主角上升真实接触刺石 trigger 时由 `SpikeStoneTrapHandle` 播放无字星爆粒子（`SpikeStoneImpact_103` 禁用 `Knock 1`）与 `trap`/`failed` 并派发 `OnTriggerTrapEvent`；角色主体碰撞体完全离屏时只记录违规，重新入画后刺石以固定 0.5 重叠放置保证下一物理步接触，走同一可见死亡链。
- 陷坑自动上跳：`Level103Ctrl.UpdatePitAutoBounce` → `rule.NotifyScriptDrivenLift()`（永不判负）→ Domain 承认处于 InPitAutoBounce 后才对 `actor.RB` 施加竖直速度。起跳收紧为 Level 105 坑底跳同款三条件（站稳、水平在坑口半宽内、pivotY ≤ `pitBounceFloorY`），实际只有落到离屏承托面才弹，站坑沿墙顶或岸上绝不触发；每次落地只起弹一次（`pitBounceAirborne` 标记消费），起弹瞬间补放与 `ActorCtrl.Jump` 同源的通用跳跃音效 `jump`（含完全离屏时）。完全离屏期间 `LateUpdate` 只把实际水平速度收口为 0，不调用 `SetMoveInput`，因此玩家方向输入仍保存在 `ActorCtrl.XInput`，重新入画立即恢复。

## 事件流（时序）

1. 主角从左侧入场门出场向右移动（Entrance）。
2. 诱饵小狗开局在 `(29.4,-1.89)` 循环 `idle`。主角获得控制后，世界 X 到达小狗当前世界 X 左侧 `dogBarkTriggerDistance=8.9` 时（初始 bark 门槛 `20.5`），先保持原地播放一次非循环 `bark` Spine 动画；启动同帧以本关局部 `dogSfxVolume=1` 播放既有 `bark` 音效，并显示只读复用的 `Assets/Game/ArtSrc/Animations/Noise/Bark.prefab` 声波闪电特效。特效挂在 `DogBait/body` 下，在 bark 与随后 `dogPostBarkHold=0.4s` 最终姿态停留期间保持显示，切换 `idle`/`run` 或统一清理时隐藏。资源名义时长约 `0.6333s`，以 TrackEntry 实际完成事件为准。成功叫喊后保持最终姿态 `dogPostBarkHold=0.4s`；停留完成且主角已到原 run 门槛 `dogTriggerDistance=7`（初始 X=22.4）才切循环 `run`，若尚未到达则隐藏特效并恢复 `idle` 等待，由 `Level103Ctrl` 私有 DOTween 以 `dogRunSpeed=8` 贴地向左直线冲锋至 `dogExitX=-12`；移动段 41.4 单位、名义约 `5.175s`，从 bark 触发到隐藏约 `6.21s`。若动画播放失败则把 bark 表现视为完成并恢复 `idle`，仍等待 run 门槛；蓄力与移动共用 `dogChargeTween` 清理通路。到位后先隐藏并写入 `DogPassCompleted`，小狗完成不覆盖已开始的陷坑阶段。途经入口假坑敞口时不上跳；无碰撞与前层渲染由 Prefab 负责。bark 初始触发点 X=20.5 位于主陷坑 `[15.5,17.99]` 右侧、在悬空平台名义视觉左缘 `20.638` 之前约0.138；run 门槛 X=22.4 落在平台名义视觉范围 `[20.638,24.285]` 内；主角首次进入坑口仍独立触发塌落。
3. 在抵达小狗触发点前的右移途中，主角直接走过**入口假坑**（2026-08-06 新增，2026-08-07 整体右移 3 世界单位至范围 `[-2.61,-0.12]`；2026-08-08 用户随后确认整体左移 0.3 至 `[-2.91,-0.42]`，宽度仍为 2.49）：外观照搬 Level 105 固定陷坑（开局敞口、坑沿收口完整、无伪装与塌落演出），但坑口有一层与地面线齐平的全宽隐形承托（`Ground_airbridge`，顶面 Y=-2.576），玩家不可能掉入；坑底另有与 `Ground_safe` 同构的 `Ground_safe (1)`（`Y=-10` 纯 Collider）仅作物理兜底。
4. 主角**中心（pivot）首次进入坑洞开口**即进入 GroundDropping，不依赖小狗是否已启动、冲锋或完整离屏：开口边界由 `pitGroundSegment` 有效非 Trigger `Collider2D.bounds.min.x/max.x` 实时取得,当前整体右移 4 后为 `[15.5,17.99]`、宽度保持 2.49;2026-08-08 用户确认此规则取代原完整主体条件与固定 `pitApproachActorX=-0.15` 阈值线。碰撞保持开启,下沉开始同帧显现左右完整固定坑沿:左侧 `Sp_ground_1_03` / `Sp_ground_1_09` / `Sp_ground_1_05`,右侧 `Sp_ground_1_02` / `Sp_ground_1_08` / `Sp_ground_1_04`;六个节点贯穿半落等待与回升过程保持原位；半落地块以 `SceneLayer/22` 绘制在左岸 `Ground_mid/21` 之上，遮住坑沿位于地块内部的下段，因此左右实际可见竖线都在地块当前顶面收口,播放既有 `l89StonePillarDrop`,并让 `Sp_ground_1_07` 顶面、`Sp_ground_1_01` 实心主体与 `Sp_ground_1_06` 底边作为刚性整体垂直下坠**完整下落距离的一半**(最小 `4.8` 与相机下边界补足值取大后 ×0.5,时长 `0.3s`,2026-08-06 经 0.2s 试调后改回),站在块上的主角随块下沉(不算掉入)。下坠完成进入 GapOpen（`3.8s` 等待期，坑洞无法步行越过，是诱导起跳的核心压力点）；玩家尚未失败时，随后经 `pitRiseDelay=3.8s` 自动回升，复用同一下坠时长与同一音效，到位同帧隐藏左右六个坑沿节点、地形恢复平整并进入 ApproachingDoor。玩家失败不可逆确定后，升降流程立即锁住：尚未触发时不再启动，下降/等待/回升中则保持当时高度、实心承托和坑沿显隐，已开始的单次音效自然结束且不再播放后续阶段音效，直到失败重开。正常回升不做坑道占用检测,全程一次性、不重新武装。
5. `3.8s` 等待期或回升途中，主角从半落地块边缘主动走下坑道（站稳在块面上由 `IsActorRidingPitGround` 排除，不算掉入）且 Y ≤ `pitEnterDetectY=-3.5` 时才登记掉入：掉入陷坑不致死，进入自动上跳（InPitAutoBounce）。角色主体碰撞体完全离屏后水平速度锁为 0，落到约 `Y=-10` 的不可见 `Ground_safe` Collider 时被脚本竖直上推 `pitBounceVelocity=28`（上升量 ≈8.88，拱顶比坑口地面线高约 1.9，一次弹跳即可漂过坑沿上岸，不再依赖擦墙二段弹）；重新入画后恢复左右控制。坑内弹跳期间回升事件被 Domain 接受但只登记复原标记。
6. 离屏期间主动按跳只记录违规，重新入画时刺石重叠瞬现于头顶、接触即死；无违规时，站稳且（水平移出坑沿 > `pitHalfWidth + pitExitMargin`，或 pivotY 高于坑口地面线 `pitRimY=-2.5`——含落上坑沿墙顶）即脱困；地块已复原或首次掉入脱困后进入 ApproachingDoor，其余折返掉入脱困后原路返回，飞门剧情不重复触发。
7. 门前地面尖刺（`FloorSpikes`）当前只保留一个直接复用 Level 029 的 `Caltrop_mid.prefab` 实例；实例统一朝上，并在实例层移除 `SortingGroup`、`BoxCollider2D` 与陷阱脚本，只保留纯视觉 SpriteRenderer。按用户确认，以单个 `Caltrop_mid` 一换一替代此前唯一的 `Caltrop_long`；本轮再把 mid 从上一版右沿对齐位置整体向左移动 `0.5` 世界单位，覆盖此前“右沿保持不变”的合同。mid 本地坐标现为 `(-0.677,0.048899)`，`FloorSpikes` 根保持 `(3.885,-2.3167)`，因此完全弹出时名义世界 X 范围约为 `[2.353,4.063]`。两种素材高度同为 `0.71` 且 Sprite pivot 均居中；mid 的本地 Y 保持替换前数值，在 `Ground_mid` 地表边界 `Y=-2.365` 下继续遮住 `0.257801`。`CaltropGround` 继续移除；`FloorSpikes` 与现有场景地板 `Ground_mid` 分别直接挂在关卡根节点，`FloorSpikes` 使用 `SceneLayer/20`、`Ground_mid` 使用 `SceneLayer/21`。用户截图确认此前把整个 `Ground_mid` 提到 101 后，该段地板越过了原本位于 `SceneLayer/20` 的 `CommonBg/Mask`，因而显露原始纯色、与其它地板的纸张质感不一致；当前排序链为 `FloorSpikes/20 < Ground_mid/21 < Ground_pit_top/22 < Paper Mask/23`：升降地块遮住坑沿位于块内的下段，两屏 `CommonBg` 实例的嵌套纸张 Mask 仍在地形之上。平台/星星50、中景51、双翼99、门100、撞击粒子105/210、主角200、前景201、小狗202、刺石300 均保持原 order，不修改任何共享 Prefab。破土触发继续与坑口判定解耦，固定代码常量仍为 `FloorSpikesTriggerActorX=-4.25`；Prefab 保存已长出姿态，初始化后仍以 `floorSpikesEmergeDuration=0.3s` 执行 scaleY/Y 同步 OutBack 双 Tween。按当前主角速度计算，破土完成到抵达新视觉左沿的名义窗口约为 `1.17s`。
8. 主角 `X ≥ 0.8` 同帧起飞（代码常量 `DoorApproachTriggerActorX=0.8`；位于入口假坑右沿 `-0.42` 之后、当前坑口 `[15.5,17.99]` 之前；一次性表现门禁 `TryLaunchDoorFlight` 不占线性阶段）：飞门演出为长翅膀 → 竖直飞至 Y=3.545 → 1.0s OutCubic 水平飞至与 Level 105 第二扇真门对齐的悬停点 X=26.9 → 到位悬停（`FlyingDoorCtrl.HasReachedHover` 置位）；演出不再回写 DoorGrowingWings/DoorFlyingUp 线性阶段（两者保留为 Domain 线性 API 组成阶段），陷坑流程完成后由 `NotifyHoveringDoorReadyForClick` 把门阶段从 ApproachingDoor 直接推进到 DoorHovering 并调用 `OpenClickAtHover` 开放按下命中（极端时序下门未到位则逐帧等待，不丢失推进）。悬空平台 `Plantform_drop`（`Plantform_short` 既有素材实例）2026-08-07 起改为开局即固定摆放（用户确认按仓库既有实心平台处理，保留顶部实心承托碰撞体），不再有从屏顶落下的天降演出与落地反馈（落地粒子 `PlantformDropLand_103` 实例与 `dropPlatformLandVfx`/`dropPlatformLandSfxTag`/`dropPlatformDropDuration` 字段一并移除）：初始化时按渲染包围盒换算直接摆放到目标位——平台曾从视觉右沿 X=14.285 水平向右移动 7，随后按用户最新要求再向右平移 3；当前 Prefab 根为 `(22.635,-1.125)`，名义视觉范围约 `[20.638,24.285]`、右沿为 `X=24.285`。顶面世界 Y 位于悬停门体底部（`FlyingDoorCtrl.HoverBottomY`）以下 3 个世界单位（≈-0.805）：2026-08-09 用户要求平台整体下移 1，`dropPlatformBelowDoorBottom` 由 2 恢复为 3、序列化根 Y 由 `-0.125` 改为 `-1.125`；素材更换后对齐关系仍成立。实心承托碰撞体与开局固定摆放逻辑不变；实际位置、遮挡和通行观感未经运行时验证。
9. 门进入触发器从初始化起保持关闭；门到达 `DoorHovering` 且陷坑流程完成后，玩家点击门体或任意翅膀，共同经 `TryStartDoorLanding` 进入一次性收束流程。点击同帧关闭监听、停止悬停，双翼保持各自世界 X 和原间距，以 `1s` 直接沿世界 Y 轴飞到 `Y=10`；门体同时保持悬停点 `X=26.9` 不变，以 `0.8s / Ease.InQuart` 沿世界 Y 轴垂直落至 Prefab 缓存的地面根高度 `Y=-1.25`（DoorFalling），不等待双翼完成。`floatPivot` 本地 Y 在下落期间同步归零；门落地 Tween 完成且 `NotifyDoorLanded()` 成功后进入 DoorGrounded、开启门触发器。双翼沿用既有约 `0.5s` 扇动周期、`25°–30°` 摆幅、尺寸和透明度，不增加声音、粒子或拖尾，在 `1s` 到达目标高度后同时隐藏。主角随后走进门，`BaseLevelCtrl.IsActorEnterDoor` 置位后 `rule.TryEnterDoor()` 登记 Victory，继续沿既有通关结算链。

## 飞门与点击落门（FlyingDoorCtrl）

- **飞行编排**（设计文档 §3.4 三段式 + 2026-08-07 新增水平段）：① 翅膀由零缩放弹出至 Prefab 缓存的适配比例（`wingGrowDuration`，OutBack）→ ② 门 `DOMoveY` 至固定世界高度 `hoverHeight=3.545`（`flyDuration`，OutCubic）→ ③ 门 `DOMoveX` 水平飞至悬停点 `hoverX=26.9`（与 Level 105 第二扇真门对齐；`hoverFlyDuration=1.0s`，OutCubic）→ ④ `MoveFloatHandle.StartMove` 垂直 Yoyo 悬停。白色羽翼仅引用本关资产 `BrainPunk/Assets/Game/ArtSrc/Animations/Level_103/FlyingDoorWing.controller` → `BrainPunk/Assets/Game/ArtSrc/Animations/Level_103/flyingDoorWingFlap.anim`，以 0.5 秒周期、左翼 -25° / 右翼 +30° 的纯旋转曲线扇动，不包含位置曲线，贯穿飞行与悬停。
- **点击命中**：现有 `DragEventsListener` 只订阅 `BeginDragHandlers`，鼠标按下或触摸 Began 即调用 `OnDoorPressed`；不订阅 Drag/End、不根据指针位移移动门。根节点 `fallbackPickRadius=2.5` 覆盖门体和双翼，点击任一部位共用同一 Domain 一次性入口。
- **翅膀飞离**：`TryStartDoorLanding` 接受后立即关闭监听并停止悬停。两片 wings 同帧执行 `DOMoveY(wingFlyTargetY, wingFlyDuration)`，当前为 `1s` 直接飞到世界 `Y=10`；只写世界 Y，因此保持原间距和各自世界 X。飞离全程保持现有扇动、尺寸与透明度，不增加声音、粒子或拖尾，到时同时隐藏。
- **门体下落**：与双翼上飞在点击同帧开始；门根 `DOMoveY(groundY, 0.8s).SetEase(Ease.InQuart)`，不等待双翼完成。只写世界 Y，因此 X 始终为 26.9；`floatPivot` 本地 Y 同时归零，避免悬停偏移进入最终落点。
- **进门门禁**：`Initialize` 即关闭 `doorTrigger`，起飞、悬停、翅膀飞离和门体下落期间持续关闭；只有落地 Tween 完成且 Domain 接受 `NotifyDoorLanded` 后恢复。旧拖门、错误高度回悬、吸附归位与收翼流程已废止。
- **悬停浮动节点**：`hoverFloat.target` 指向 `floatPivot`（门体视觉根子节点），飞行与落门移动的是门根节点；点击后先停止浮动，下落时把 `floatPivot` 本地 Y 同步归零。

## 场景精简

- 场景星星：2026-08-03 曾按用户要求移除原坑上 `Postcard_Star` 与地面 `Postcard_Star (1)` 两个实例；2026-08-06 用户确认恢复摆放两颗。`Postcard_Star`（index=1）继续与陷坑中心对齐并位于 `(16.745, -2)`；`Postcard_Star (1)`（index=2）改为与入口假坑透明承托的中心对齐，位于 `(-1.665, -2)`，保持 Y=-2；不修改星星资源、配置或既有通关结算链。
- 同日后续确认删除 `Plantform_mid` 诱导平台；终局区域只保留高位飞门与点击落门交互。

## 两屏连续场景与相机跟随（2026-08-07 用户确认）

场景采用与 Level 105 一致的第一屏加连续第二屏结构。2026-08-08 起，`CameraFollowRange` 与 Level 105 完全对齐为边界 X∈[-13,31.5]；`GameCamera` 位置与正交尺寸保持 `(0,0,-10)` / `5.9`，`1920×1080`、`2340×1080` 的初始相机中心均为 X=0，最右锁定视野边界仍为 X=31.5。除随后确认整体左移 0.3 的入口假坑、本次整体右移 4 的陷坑及坑中心星星、对齐 Level 105 第二扇真门 X=26.9 的飞门悬停/回落点，以及本轮恢复到 X=29.4 的狗起点外，其余场景节点世界坐标保持不变；本轮只调整尖刺、飞门与小狗的触发节奏。

- **相机组件**：`CameraFollowArea` 使用死区中心 (0.5,-3)、尺寸 19×10000、`strickY=1`、平滑 0.2/0.5；纵向范围在本关 Prefab 内扩为大范围，使上游共享组件只按水平死区判断，主角越过右缘 X=10 后镜头才仅沿 X 轴跟随，不修改共享 `CameraFollowArea.cs`。`CameraFollowRange` 使用 Level 105 同款边界中心 (9.25,-2)、尺寸 44.5×20，对应边界 `[-13,31.5]`；宽屏初始位不再被向右夹移，Y 轴构图不变。
- **地面**：第二屏地面禁止拉伸中段，改为 `Ground_long` 原位复制拼接。`Ground_right`（`Ground_short` 原生尺寸，body scaleX=60.4、顶/底 scaleX=1）碰撞约 `[17.99,25.61]`，其后接 `Ground_long (1)` 原生尺寸碰撞约 `[24.51,53.24]`（接缝交叠约 1.1），连续覆盖至场景右边界 32.2 及以上；`Ground_mid` 右沿 15.5、坑口 `[15.5,17.99]` 与 FallArea 合同不变。`Ground_mid` 中段顶/底线仍向右补到升降地块视觉左沿 `X=15.430121`；右岸坑沿叶仍由 `Ground_right/Group_left` 解析，伪装隐藏端帽时的像素接缝未运行验证。
- **背景与装饰**：白纱改用 Level 105 同款覆盖（local X=20.886、scaleX=64.1555，世界覆盖约 `[-15.66,48.5]`）；新增 `CommonBg (1)`（X=50）、远景 `Sp_vision_01_1 (1)/(2)`（X=12.598/36.006）及 5 个复用自本关现有素材的第二屏中景/植物实例，保持各自 Y、缩放、材质和父层级与 Level 105 连续拼接方案一致。
- **右 FallArea**：`FallArea (1)` 中心调整为 X=37，10×10 触发盒左缘 X=32，与地面碰撞右沿 32.2 保持 0.2 重叠，且位于相机右缘 31.5 之外；左侧 FallArea 不动。
- **行为边界**：镜头只扩大横向行程，不改变坑、离屏、刺石、飞门点击落门、失败或通关规则。实际平滑跟随、地面接缝、远景/装饰连续性和宽屏观感没有本次运行证据，保持 `uncertain`。

- 订阅事件：`PlayerJumpEvent`（跳跃即死）、`OnTriggerTrapEvent` / `OnTriggerFailAreaEvent`（失败确认同帧冻结陷坑升降）、`GameResetEvent`（失败重开清理）；小狗冲锋不再订阅左右移动事件，`Update` 按角色世界 X 与 `dogRoot.position.x - dogTriggerDistance` 的关系触发。移动/跳跃动作本身仍由 `BaseLevelCtrl` 既有处理驱动。
- 触发轮询：`Update` 按 Domain 阶段做阈值/区域判定，每个 Notify/Try 入口由 Domain 保证一次性，重复帧调用幂等。
- `GameResetEvent` / `OnDisable` / `OnDestroy`：Kill 本关小狗 bark 后蓄力/冲锋、尖刺破土与陷坑 Tween、复位事件订阅；失败重开由既有流程销毁重建关卡，无需复用清理。

## Prefab 接线

根级 `LevelTitlePlaceholder` 是共享 `Assets/Game/Prefabs/LevelItems/Tips.prefab`（GUID `49d63fd22a39a044bab1c28dbf961bcb`）的独立实例。它沿用 Level 083 的原始文本 `Well... \nShow some respect.`、`m_margin.z=-0.40812898`、I2 `S_LevelTips_Des_level_83`、中心 `(0.84,3.06)`、共享尺寸 `7.5473×2.2407` 与继承的 `SceneLayer/100`；没有组件增删、碰撞、Animator 或关卡脚本接线。`Tips.prefab`、`I2Languages.asset`、C#、玩法和配置均不修改；该实例仅为未来正式标题占位。

根节点使用 `Level103Ctrl` 组件（不替换根节点 `BaseLevelCtrl`）；右侧 `door_0` 外套 `FlyingDoor` 根节点挂 `FlyingDoorCtrl` + `MoveFloatHandle`（autoStart=false）+ `DragEventsListener`（默认 enableDrag 会被 Awake 关闭，只在陷坑完成且门悬停到位后作为按下命中开放），`FloatPivot` 上的 Animator 只引用 `BrainPunk/Assets/Game/ArtSrc/Animations/Level_103/` 内的本地 controller/clip。陷坑的 `pitGroundSegment` 绑定 `Ground_pit_top`，其根 `SortingGroup` 为 `SceneLayer/22`：运动根只包含 `Sp_ground_1_07` 顶面、`Sp_ground_1_01` 实心主体与 `Sp_ground_1_06` 底边三层结构，不再携带 `Sp_em_1_02` 表面装饰。两个同素材小凸起统一挂在静止 `Frontscene/zhiwu` 下，Prefab 局部坐标为 `(11.18,-1.90)` 与 `(14.36,-2.92)`，对应世界中心 `(11.65,-2.40)` 与 `(14.83,-3.42)`；两者保持原 Sprite、尺寸和无 Collider 状态，半落与回升时不移动。三层结构材质/颜色/Sorting Layer 一致，主体渲染在上下边线之后；不包含随动左右竖边，根 Collider 仍为约 `2.49×0.2` 的薄型承托。2026-08-06 地形拆分：原 `Ground_long` 缩短并改名 `Ground_left`（左沿 -14.20 不变；2026-08-08 入口假坑整体左移 0.3 后右沿收至 -2.91，Group_right 坑壁/底边保持常显），现有陷坑左岸为新增 `Ground_short` 实例 `Ground_mid`（入口假坑左移后碰撞范围为 `[-0.42,15.5]`，并保持 `Group_mid_1` 中段平铺、Group_left 坑壁/底边常显、Group_right 坑壁/底边触发前隐藏）。`pitLeftTopEdge` / `pitLeftWall` / `pitLeftBottom` 运行时精确解析为 `Ground_mid/Group_right` 下的 `Sp_ground_1_03` / `Sp_ground_1_09` / `Sp_ground_1_05`;`pitRightTopEdge` / `pitRightWall` / `pitRightBottom` 精确解析为 `Ground_right/Group_left` 下的 `Sp_ground_1_02` / `Sp_ground_1_08` / `Sp_ground_1_04`。伪装状态隐藏左右顶部端帽时，左右岸中段顶/底线分别静态延伸到升降地块视觉边界 `X=15.430121 / 18.060121`，不会留下端帽宽度的缺块。控制器不启停父 Group:六个叶节点触发前全部隐藏,下沉开始同帧共同显示,半落等待与回升途中保持,到位同帧共同隐藏;每侧必须由顶部端帽段、纵壁和底边连续组成完整竖线。入口假坑由 `Ground_left` 右沿、`Ground_mid` 左沿、隐形承托 `Ground_airbridge`（BoxCollider2D 2.59×0.2、顶面 Y=-2.576、无渲染）与坑底兜底 `Ground_safe (1)`（`Y=-10`，与 `Ground_safe` 同构纯 Collider）组成。离屏边界优先取 Actor 根节点上 enabled、非 Trigger、activeInHierarchy 的主 `Collider2D`（标准 Actor 为 `PolygonCollider2D`）；仅根节点不可用时遍历同样有效的子级候选，默认 inactive 的 `pushCollider` 不参与判定，完全找不到时不伪造离屏状态。`Ground_safe` 位于约 `Y=-10`，只保留薄型 Collider、无 Sprite 或 SortingGroup。场景中的 `Plantform_short` 实例 `Plantform_drop` 开局固定摆放；在此前右移 7、上移 1 的基础上，用户最新要求再向右平移 3，当前 Prefab 根坐标为 `(22.635,-0.125)`、名义视觉范围约 `[20.638,24.285]`、右沿为 X=24.285；顶面仍为悬停门底 - 2 ≈0.195，实心承托碰撞体保留。两颗 `Postcard_Star` 当前位于 `(16.745,-2)` 与 `(-1.665,-2)`，第一颗继续与陷坑中心对齐，第二颗与入口假坑透明承托中心对齐。根节点下另有 `FloorSpikes` 节点：其下只保留一个 Level 029 同源的 `Caltrop_mid.prefab` 实例；实例朝上，并在实例层移除 `SortingGroup`、`BoxCollider2D` 与陷阱脚本，仅保留纯视觉 SpriteRenderer（尺寸 1.71×0.71）。按用户确认，该实例在上一版右沿对齐位置上整体向左移动 `0.5` 后，本地坐标为 `(-0.677,0.048899)`；`FloorSpikes` 根保持 `(3.885,-2.3167)`，名义世界左右沿约为 `2.353 / 4.063`。两种素材高度同为 `0.71` 且 Sprite pivot 均居中；Level 103 的 `Ground_mid` 地表边界为 `-3+1.27/2=-2.365`，本地 Y 偏移使 mid 向上的根沿为 `-2.622801`，名义遮根继续为 `0.257801`。`CaltropGround` 不恢复；`Ground_mid` 与 `FloorSpikes` 都直接挂在 Level 103 根节点，后者根 `SortingGroup` 调整为 `SceneLayer/20`，前者根 `SortingGroup` 覆盖为 `SceneLayer/21`。两个根级 `CommonBg` 实例仅对嵌套纸张纹理 Renderer（`Mask.prefab`，共享源 order 20）增加本关 `m_SortingOrder=23` 覆盖，使纸张纹理继续合成在 `Ground_mid/21` 与 `Ground_pit_top/22` 之上。其余既有场景素材全部保留原排序：`Plantform_drop` 与两颗 `Postcard_Star`=50、`Midscene`=51、双翼=99、`StartDoor` 与飞门 `door_0`=100、`SpikeStoneImpact_103` 两级粒子=105/210（实例层覆盖子节点 `Knock 1` 为 inactive，去掉 `KNOCK!` 字眼、保留根星爆；不修改共享 `Knock_Explosion.prefab`）、Actor=200、`Frontscene`=201、`DogBait`=202、`SpikeStone`=300；共享粒子源 Prefab、纸张素材与材质均不修改。`FloorSpikes` 无 Collider 与陷阱脚本；`Level103Ctrl.floorSpikes` 绑定与 0.71 高度读取不变，当前触发常量为 -4.25、Tween 时长为 0.3s，scaleY/Y 同步 OutBack 结构保持不变。静态结构可证明名义边界和数值排序关系；地板纸张质感是否连续、各 SortingGroup 与嵌套粒子的实际合成、素材不规则边缘、接缝与 OutBack 过程观感仍为 `uncertain`。

## 当前标定值

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| dogExitX / dogRunSpeed / dogBarkTriggerDistance / dogTriggerDistance / dogPostBarkHold | -12 / 8 / 8.9 / 7 / 0.4s | `DogBait` 初始 X=29.4；主角到达 X=20.5 时先原地播放约 0.6333s 的 bark，成功完成后保持 0.4s；原 X=22.4 继续作为 run 门槛，两条件同时满足才开始 41.4 单位、约 5.175s 的 run；持续右移时从 bark 到起跑约1.033s，从 bark 到隐藏约6.21s。 |
| Actor moveSpeed（PrefabInstance 覆盖） | 4.5 | 仅本关 Box 移速：2026-08-06 用户确认由共享源 `Actor.prefab` 的 5 覆盖为 4.5（`Level_103.prefab` Actor 实例属性覆盖，不改共享 Prefab，其他关不受影响） |
| dogSfxTag / dogSfxVolume / dogBarkEffect | bark / 1 / `Noise/Bark.prefab` | bark Spine 动画开始同帧以本关局部最大音量播放 `l17/bark.ogg` 并显示声波闪电特效；特效贯穿 0.4s 最终姿态停留，切换 idle/run 或清理时隐藏；共享默认音量 0.4 与其它关卡不变 |
| pitDropDistance / pitDropDuration | 4.8 / 0.3s | 完整下落距离基准与下坠/回升时长（2026-08-06 经 0.2s 试调后改回 0.3s）；实际半落下坠距离为完整距离 ×0.5，不再完全离屏或隐藏 |
| pitRiseDelay | 3.8s | 正常未失败时，半落下坠完成到自动回升的等待时长；不做坑道占用检测 |
| pitCollapseSfxTag | l89StonePillarDrop | 保持既有 SoundsGood 接线 |
| pitCenterX / pitHalfWidth / pitEnterDetectY | 16.745 / 1.245 / -3.5 | 坑口中心（当前整体右移 4，坑口 [15.5,17.99]）、半宽、掉入判定线（坑口地面线以下约 1 单位，2026-08-05 从 -1 下调，修掉站坑沿误判掉入） |
| pitRimY / pitBounceFloorY | -2.5 / -8.5 | 坑口地面线（站稳高于此值即脱困）、坑底起跳线（三条件之一，只有落到离屏承托面才满足） |
| pitExitMargin / pitBounceVelocity | 0.2 / 28 | 出坑余量、深坑自动上跳初速度（上升量 ≈8.88 = 落差 7.0 + 拱顶余量 ≈1.9，一次弹跳漂出坑口） |
| Ground_safe 根 Y | -10 | 纯 Collider 离屏承托面 |
| DoorApproachTriggerActorX | 0.8（代码常量） | 玩家通过入口假坑后启动飞门；保留 0.3s 长翼、1.2s 竖直飞行和 1.0s 水平飞行，名义到位时角色约在 X=12.05，距主坑左沿约 0.77s 行进时间 |
| FloorSpikesTriggerActorX | -4.25（代码常量） | 尖刺破土继续与坑口判定解耦；配合 0.3s 弹出，在主角进入入口假坑左沿附近完成 |
| hoverX / hoverFlyDuration | 26.9 / 1.0s | 飞门悬停点世界 X 对齐 Level 105 第二扇真门（2026-08-08 用户确认）与水平飞行段时长（OutCubic） |
| floorSpikesEmergeDuration | 0.3s | 地面尖刺破土弹出时长；floorSpikesEmergeSfxTag 默认空 |
| hoverHeight / flyDuration / wingGrowDuration | 3.545 / 1.2s / 0.3s | 固定世界悬停高度、飞起（2026-08-06 由 1s 调整为 1.2s）、长翅膀 |
| wingFlyTargetY / wingFlyDuration | 10 / 1s | 两翼保持各自世界 X，同帧直接沿世界 Y 轴上飞，到时同时隐藏 |
| doorFallDuration / Ease | 0.8s / InQuart | 与双翼点击同帧开始；门体保持 X=26.9，沿世界 Y 轴落至根 Y=-1.25 |
| dropPlatformRightX / dropPlatformBelowDoorBottom | 24.285 / 3 | 悬空平台（`Plantform_short`）视觉右沿世界 X、顶面相对悬停门底下沉量；当前 Prefab 根 `(22.635,-1.125)`，名义视觉范围约 `[20.638,24.285]`。2026-08-09 用户要求平台下移 1 世界单位，下沉量由 2 恢复为 3（顶面约由 0.195 降至 -0.805）；X、实心承托 Collider 与开局固定摆放逻辑不变 |
| spikeHeadOffset | 2.0 | 刺石固定悬停点相对起跳时刻主角中心的垂直距离（刺石 scale 0.7、trigger 世界 1.603×1.575（与贴图 Sp_l103_dj_01 实际尺寸一致）；2026-08-06 经 1 试调后由 3.0 定为 2.0，大于双方半高和约 1.38 且小于跳跃顶点约 3.67，主角上升一段距离后真实撞刺；离屏结算按固定 0.5 重叠放置） |
| CameraFollowArea 死区 / 平滑 | (0.5,-3) 19×10000 / 0.2s·0.5s | 本关 Prefab 纵向扩为大范围且 `strickY=1`，只按 X∈[-9,10] 判断并仅跟随 X；不修改共享相机脚本 |
| CameraFollowRange 边界 | (9.25,-2) 44.5×20 | 与 Level 105 一致，边界 `[-13,31.5]`；1920×1080 / 2340×1080 初始中心均为 X=0，主角 X>10 后仅 X 轴跟随 |
| 地面碰撞链 | Ground_left [-14.2,-2.91] / Ground_mid [-0.42,15.5] / Ground_right [17.99,25.61] / Ground_long (1) [24.51,53.24] | 入口假坑 `[-2.91,-0.42]`；陷坑坑口 [15.5,17.99]；第二屏原位拼接、禁止拉伸中段；接缝交叠约 1.1，覆盖 ≥32.2 |
| FallArea (1) x | 37 | 10×10 盒左缘 32 与地面右边界合同 32.2 保持 0.2 交叠，并位于窗口右缘 31.5 之外 |

`dogExitX`、`dogRunSpeed`、`dogTriggerDistance`、`dogPostBarkHold`、`pitCenterX` 与 `pitHalfWidth` 为当前精确合同；小狗初始根坐标为 `(29.4,-1.89)`，主角到达 `dogRoot.position.x - dogBarkTriggerDistance`（初始 X=20.5）时开始 bark；成功完成后保持0.4s，且主角到达 `dogRoot.position.x - dogTriggerDistance`（初始 X=22.4）后才冲锋。小狗冲锋/隐藏只更新自身一次性状态，不再作为陷坑塌落的前置；主角 pivot 首次进入实时 `pitGroundSegment` Collider 坑口边界即触发。当前节奏合同为：尖刺触发 `-4.25`、弹出 `0.3s`、飞门触发 `0.8`、坑等待 `3.8s`；当前布局合同为：悬停点 X=26.9、坑口 `[15.5,17.99]`、假坑 `[-2.91,-0.42]`、狗起点 `(29.4,-1.89)`、平台根 `(22.635,-1.125)` / 右沿 `24.285`、星星 `(16.745,-2)/(-1.665,-2)`。陷坑完成且门物理到位后开放点击，点击门体或任意翅膀后双翼垂直上飞与门体垂直下落同帧开始，门落稳后开放进门。

## 标题占位验证与回滚

- 静态证据确认 `LevelTitlePlaceholder` 的 PrefabInstance、stripped RectTransform 与根 child 各唯一一处，引用闭合且新增 YAML anchor 无重复；占位文本、key、位置、margin 和排序继承关系符合当前合同。
- 未启动 Unity 或 PlayMode；I2 实际刷新、TMP 字形/换行、不同画幅遮挡和最终构图均为 `uncertain`。本次不执行编译、测试或探针。
- 仅回退标题占位时，移除 `Level_103.prefab` 的 `LevelTitlePlaceholder` PrefabInstance、stripped RectTransform 和根 child，并同步恢复 Context、PRD 与本目录两份说明；不得修改共享 `Tips.prefab`、`I2Languages.asset`、Level 083 或其它 Level 103 玩法。

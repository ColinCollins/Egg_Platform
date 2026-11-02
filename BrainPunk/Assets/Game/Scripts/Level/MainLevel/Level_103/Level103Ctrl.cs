using Bear.EventSystem;
using Bear.Logger;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Level103;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level 103「不要跳」本地玩法编排器（组合形态，Level 083 先例）：
/// 通过序列化字段引用 Prefab 根节点的 <see cref="BaseLevelCtrl"/>，根节点既有接线保持不动。
/// 纯规则判定（跳跃即死、事件编排顺序、飞门点击落地状态机）全部收敛到 Domain 状态机
/// <see cref="Level103DontJumpRule"/>；本组件只做编排与表现适配——
/// 玩家起跳输入走 <see cref="Level103DontJumpRule.NotifyPlayerJumpInput"/>（唯一违规登记入口，判负由 SpikeStoneTrapHandle 碰撞收口），
/// 陷坑自动上跳的脚本位移走 <see cref="Level103DontJumpRule.NotifyScriptDrivenLift"/>（永不判负），
/// 两条通路在代码结构上严格分离。
/// </summary>
public sealed class Level103Ctrl : MonoBehaviour, IDebuger, IEventSender
{
    #region Inspector

    /// <summary>承载标准输入开关、门触发通关链与失败重开流程的关卡基础控制器（Prefab 根节点）。</summary>
    [SerializeField] private BaseLevelCtrl levelCtrl;

    /// <summary>从左侧入场门出场的主角 Box；为空时回退取 levelCtrl.Actor。</summary>
    [SerializeField] private ActorCtrl actor;

    [Header("跳跃即死 · 头顶刺石")]
    /// <summary>头顶刺石（Sp_l103_dj_01 实例，带 SpikeStoneTrapHandle 碰撞判负）。平时完全不可见；起跳瞬间固定悬于起跳点上空 spikeHeadOffset 处不再跟随，主角上升、头部真实撞击刺石 trigger 才判负。</summary>
    [SerializeField] private GameObject spikeStone;

    /// <summary>刺石固定悬停点相对起跳时刻主角中心的垂直距离（世界单位）。2026-08-06 经 1 试调后定为 2：大于刺石与主角的半高和（约 1.38）且小于跳跃顶点（约 3.67），主角上升一段距离后真实撞刺；离屏违规重入结算仍用固定 0.5 重叠放置。</summary>
    [SerializeField] private float spikeHeadOffset = 2f;

    /// <summary>刺石瞬现待碰撞收口的起始时间（Time.time）；&lt; 0 表示无待结算。超过 1 秒未接触走兜底死亡链，防软锁。</summary>
    private float headSpikePendingSince = -1f;

    [Header("诱饵小狗")]
    /// <summary>诱饵小狗根节点（sp_smalldog，含 ActorSpineCtrl，无碰撞体，前层渲染）；开局可见并循环 idle。</summary>
    [SerializeField] private Transform dogRoot;

    /// <summary>小狗 Spine 动画控制器（挂在 dogRoot 或其子节点上的 ActorSpineCtrl）。</summary>
    [SerializeField] private ActorSpineCtrl dogSpine;

    /// <summary>小狗直线狂奔的终点世界 X（左侧屏外）。</summary>
    [SerializeField] private float dogExitX = -12f;

    /// <summary>小狗狂奔速度（世界单位/秒）；2026-08-08 用户确认由 12 调整为 8。</summary>
    [SerializeField] private float dogRunSpeed = 8f;

    /// <summary>bark 触发时主角位于小狗世界 X 左侧的距离；当前 8.9 对应初始玩家 X=20.5。</summary>
    [SerializeField] private float dogBarkTriggerDistance = 8.9f;

    /// <summary>run 门槛保持为小狗世界 X 左侧 7；当前对应初始玩家 X=22.4。</summary>
    [SerializeField] private float dogTriggerDistance = 7f;

    /// <summary>成功播放 bark 后保持最终叫喊姿态的时长；结束后等待 run 门槛。</summary>
    [SerializeField] private float dogPostBarkHold = 0.4f;

    /// <summary>复用 Noise/Bark.prefab 的叫喊声波特效；bark 与蓄力停顿期间显示，切换 run 时隐藏。</summary>
    [SerializeField] private GameObject dogBarkEffect;

    /// <summary>小狗叫喊动画开始时同步播放的音效 tag（SoundsGood 已配置 bark → l17/bark.ogg）。</summary>
    [SerializeField] private string dogSfxTag = "bark";

    /// <summary>本关狗叫音量；1 为 SoundsGood 单次音效最大音量，只影响本关这次 bark 播放。</summary>
    [SerializeField, Range(0f, 1f)] private float dogSfxVolume = 1f;

    [Header("陷坑 · 坑前塌陷")]
    /// <summary>坑位宽度内的完整塌落地块；顶部、实心主体与底边整体下坠，左右岸与离屏承托面保持静止。
    /// 2026-08-06 起为半落回升：碰撞不再关闭，主角随块下沉，当前停 3.8 秒后升回原位并恢复平整地面（一次性）。</summary>
    [SerializeField] private Transform pitGroundSegment;

    /// <summary>左岸坑口顶部端帽段 Sp_ground_1_03；与纵壁、底边共同组成下沉期间显示的完整左坑沿。</summary>
    [SerializeField] private Transform pitLeftTopEdge;

    /// <summary>左岸朝向坑口的纵壁 Sp_ground_1_09；下沉开始至回升完成前持续显示。</summary>
    [SerializeField] private Transform pitLeftWall;

    /// <summary>左岸向外延伸的底边 Sp_ground_1_05；与顶部端帽段、纵壁共同显隐。</summary>
    [SerializeField] private Transform pitLeftBottom;

    /// <summary>右岸坑口顶部端帽段 Sp_ground_1_02；与纵壁、底边共同组成下沉期间显示的完整右坑沿。</summary>
    [SerializeField] private Transform pitRightTopEdge;

    /// <summary>右岸朝向坑口的纵壁 Sp_ground_1_08；下沉开始至回升完成前持续显示。</summary>
    [SerializeField] private Transform pitRightWall;

    /// <summary>右岸向外延伸的底边 Sp_ground_1_04；与顶部端帽段、纵壁共同显隐。</summary>
    [SerializeField] private Transform pitRightBottom;

    /// <summary>关卡相机；用于按实际下边界判断塌落地块全部可见部分与角色主体碰撞体是否完整离屏。</summary>
    [SerializeField] private Camera sceneCamera;

    /// <summary>完整下落距离基准；运行时至少取 4.8，并按全部 Renderer 的相机下边界继续补足。实际半落下坠距离为该完整距离的一半。</summary>
    [SerializeField] private float pitDropDistance = 4.8f;

    /// <summary>半落下坠完成后到自动回升的等待时长（秒）；当前节奏标定为 3.8 秒，不做坑道占用检测。</summary>
    [SerializeField] private float pitRiseDelay = 3.8f;

    /// <summary>塌落地块快速下坠时长（秒）；2026-08-06 用户确认经 0.2 试调后改回 0.3，半落回升复用同一时长。</summary>
    [SerializeField] private float pitDropDuration = 0.3f;

    /// <summary>地面塌落音效 tag（复用既有 l89StonePillarDrop 接线，不新增音效）。</summary>
    [SerializeField] private string pitCollapseSfxTag = "l89StonePillarDrop";

    [Header("陷坑 · 自动上跳")]
    /// <summary>坑口中心世界 X 的回退值；正常情况下以 pitGroundSegment 的 Collider2D 世界边界为准，避免关卡移动后逻辑与实际开口脱节。</summary>
    [SerializeField] private float pitCenterX = 16.745f;

    /// <summary>坑口半宽的回退值；仅在 pitGroundSegment 未绑定有效 Collider2D 时用于掉入与移出坑沿判定。</summary>
    [SerializeField] private float pitHalfWidth = 1.245f;

    /// <summary>掉入判定线：主角世界 Y 低于该值且水平位于坑口内时记为掉入陷坑（不致死）。
    /// 必须低于站立 pivot（坑口地面线约 -2.58 + 碰撞体底偏移 ≈ -2.10）；原 -1 高于站立线，
    /// 站坑沿会被误判掉入并触发自动弹跳，2026-08-05 起取坑口地面线以下约 1 单位。</summary>
    [SerializeField] private float pitEnterDetectY = -3.5f;

    /// <summary>坑口地面线（坑沿可站面）世界 Y，约 -2.58；站稳且 pivotY 高于该值（岸上或坑沿墙顶）即脱困。</summary>
    [SerializeField] private float pitRimY = -2.5f;

    /// <summary>坑底起跳线：站稳且水平在坑口内且 pivotY 低于该值才施加自动上跳（Level 105 坑底跳三条件先例）；
    /// 取离屏承托面顶（约 -9.58，对应 pivot ≈ -9.10）上方约 0.6 单位，实际只有落到离屏承托面才满足。</summary>
    [SerializeField] private float pitBounceFloorY = -8.5f;

    /// <summary>移出坑沿判定的水平余量：主角站稳且水平超出 pitHalfWidth + 本余量时记为脱困。</summary>
    [SerializeField] private float pitExitMargin = 0.2f;

    /// <summary>自动上跳的竖直初速度（脚本驱动位移，非玩家起跳输入）。28 = 上升量 v²/(2×9.81×4.5) ≈ 8.88：
    /// 坑底承托顶 -9.58 → 坑口地面线 -2.58 落差 7.0，拱顶余量约 1.9，box 主体完整越过坑沿后仍有富余做水平漂移，
    /// 一次弹跳即可上岸，不再依赖擦碰坑壁后站上坑沿再弹一次的二段路径（2026-08-05 标定，Level 105 注释风格）。</summary>
    [SerializeField] private float pitBounceVelocity = 28f;

    [Header("飞门")]
    /// <summary>飞门编排器（右侧通关门根节点上的 FlyingDoorCtrl）。</summary>
    [SerializeField] private FlyingDoorCtrl flyingDoor;

    /// <summary>飞门触发的固定主角世界 X；当前在入口假坑后方 X=0.8 启动，仍使用不占线性阶段的一次性表现门禁。</summary>
    private const float DoorApproachTriggerActorX = 0.8f;

    /// <summary>门前地面尖刺破土的固定主角世界 X；当前在 X=-4.25 提前启动，与坑口判定保持解耦。</summary>
    private const float FloorSpikesTriggerActorX = -4.25f;

    [Header("门前地面尖刺 · 破土弹出")]
    /// <summary>门前地面尖刺根节点（FloorSpikes，子级只保留一个复用 Level 029 的 Caltrop_mid Prefab 实例；实例层移除 SortingGroup、BoxCollider2D 与陷阱脚本，只保留纯视觉 SpriteRenderer）。Prefab 中保存已长出姿态；初始化时把 FloorSpikes 参考底线埋回地面（scaleY=0），该实例保留用于对齐替换前遮根高度的少量本地 Y 偏移，触发后一次性破土弹出。</summary>
    [SerializeField] private Transform floorSpikes;

    /// <summary>尖刺破土弹出时长（秒），当前节奏标定为 0.3。</summary>
    [SerializeField] private float floorSpikesEmergeDuration = 0.3f;

    /// <summary>尖刺破土瞬间播放的音效 tag；默认空表示不播放，需要时在 Inspector 配置既有 SoundsGood 接线。</summary>
    [SerializeField] private string floorSpikesEmergeSfxTag = "";

    [Header("悬空平台 · 开局固定摆放")]
    /// <summary>悬空平台根节点（仓库既有平台素材实例，实心顶面承托、玩家可站立）。2026-08-07 起开局即固定摆放，
    /// 不再有门飞同帧的天降演出与落地反馈（用户确认按仓库既有实心平台处理）。</summary>
    [SerializeField] private Transform dropPlatform;

    /// <summary>平台视觉右沿对齐的世界 X；当前为 24.285，在上一版 21.285 的基础上向右移动 3 个世界单位。</summary>
    [SerializeField] private float dropPlatformRightX = 24.285f;

    /// <summary>平台顶面相对悬停门体底部的下沉量（世界单位）；曾由 3 调为 2 使顶面上移 1，本轮按用户要求恢复为 3，顶面随之下移 1。</summary>
    [SerializeField] private float dropPlatformBelowDoorBottom = 3f;

    #endregion

    #region Runtime State

    private const string DogIdleAnimationName = "idle";
    private const string DogBarkAnimationName = "bark";
    private const string DogRunAnimationName = "run";

    private readonly Level103DontJumpRule rule = new Level103DontJumpRule();
    private EventSubscriber subscriber;
    private Spine.TrackEntry dogBarkEntry;
    private Tween dogChargeTween;
    private bool dogRunTriggerReached;
    private bool dogBarkPresentationCompleted;
    private bool dogRunStarted;
    private Tween pitTween;
    private Tween floorSpikesTween;
    private float floorSpikesEmergedY;
    private float floorSpikesBuriedY;
    private Renderer[] pitGroundRenderers;
    /// <summary>塌落地块触发前的原始世界 Y；半落下坠以此为起点、回升以此为终点。</summary>
    private float pitOriginalY;
    /// <summary>塌落地块的实际承托碰撞体；其世界 X 边界是坑口触发、掉入与脱困判定的唯一正常数据源。</summary>
    private Collider2D pitGroundCollider;
    private Collider2D actorBodyCollider;
    /// <summary>陷坑自动上跳的离地标记：离地帧置位、落地起弹帧消费；保证每次落地只起弹一次、只播一次跳跃音效。</summary>
    private bool pitBounceAirborne;
    /// <summary>本局失败确认后锁住升降地块；只终止升降，不改地块位置、碰撞或坑沿显隐。</summary>
    private bool pitMovementFrozen;

    #endregion

    #region Unity Messages

    private void Start()
    {
        ResolveReferences();
        InitializeGameplay();
        BindEvents();
    }

    private void Update()
    {
        if (actor != null && actor.IsDied)
            FreezePitMovement();

        UpdatePendingHeadSpike();

        if (actor == null || rule.Result != Level103Result.None)
            return;

        float actorX = actor.transform.position.x;
        UpdateDogSequenceAtActorPosition(actorX);
        // 尖刺破土与坑口判定解耦；当前提前到 X=-4.25，使破土完成与进入入口假坑形成连续节拍。
        if (actorX >= FloorSpikesTriggerActorX && rule.TryEmergeFloorSpikes())
            PlayFloorSpikesEmerge();
        // 飞门：主角通过入口假坑后到达 X=0.8 立即起飞；一次性表现门禁不占线性阶段。
        if (actorX >= DoorApproachTriggerActorX && rule.TryLaunchDoorFlight())
            flyingDoor?.Play();
        // 缺口打开后任何阶段（含脱困后折返、门流程中途折返）再次掉入陷坑都重新进入自动上跳，掉入不致死；
        // Domain 记录掉入前阶段，脱困后原路返回，飞门剧情不重复触发；塌落完成前的掉入由 Domain 门禁拒绝。
        // 随半落地块下沉/回升不算掉入（IsActorRidingPitGround 排除），只有从块沿主动走下坑道才登记掉入。
        if (rule.Phase != Level103Phase.InPitAutoBounce && IsActorInsidePit() && !IsActorRidingPitGround())
            rule.NotifyFellIntoPit();

        // 陷坑与小狗解耦：主角 pivot 首次进入实时坑口 Collider 即触发，不等待小狗启动、冲锋或隐藏。
        // Domain 记录一次性门禁，小狗完成回调不会覆盖 GroundDropping/GapOpen 等坑洞阶段。
        if (IsActorPivotInsidePitOpening() && rule.NotifyPitApproached())
            PlayGroundCollapse();

        switch (rule.Phase)
        {
            case Level103Phase.InPitAutoBounce:
                UpdatePitAutoBounce();
                break;
            case Level103Phase.ApproachingDoor:
                // 陷坑流程完成：门通常早已提前起飞并到位悬停；物理到位后推进 Domain 门阶段（→DoorHovering）并开放点击。
                // 极端时序下门尚未到位时逐帧等待 EnterHover 置位，不会丢失推进。
                if (flyingDoor != null && flyingDoor.HasReachedHover && rule.NotifyHoveringDoorReadyForClick())
                    flyingDoor.OpenClickAtHover();
                break;
            case Level103Phase.DoorGrounded:
                // 沿既有门触发链通关：BaseLevelCtrl 的门触发把 IsActorEnterDoor 置位后，
                // Domain 侧登记胜利；翅膀飞离且门垂直落稳前的一切进门尝试不被 Domain 承认。
                if (levelCtrl != null && levelCtrl.IsActorEnterDoor)
                    rule.TryEnterDoor();
                break;
        }
    }

    private void LateUpdate()
    {
        if (actor == null || rule.Result != Level103Result.None ||
            rule.Phase != Level103Phase.InPitAutoBounce || !rule.PitActorIsFullyOffscreen)
            return;

        Rigidbody2D body = actor.RB;
        if (body == null)
            return;

        // ActorCtrl 每帧仍可读取并保留 XInput；这里只在物理步进前收口实际水平速度。
        body.linearVelocity = new Vector2(0f, body.linearVelocityY);

        // 若共享跳跃事件在离屏落地帧先写入 jumpForce，以本关自动反弹速度覆盖，避免违规输入改变循环。
        if (actor.IsGround && rule.NotifyScriptDrivenLift() && body.linearVelocityY < EffectivePitBounceVelocity)
            body.linearVelocity = new Vector2(0f, EffectivePitBounceVelocity);
    }

    private void OnDisable() => CleanupRuntimeState();

    private void OnDestroy() => CleanupRuntimeState();

    private void CleanupRuntimeState()
    {
        KillTweens();
        EventsUtils.ResetEvents(ref subscriber);
    }

    #endregion

    #region Initialization

    private void ResolveReferences()
    {
        levelCtrl ??= GetComponent<BaseLevelCtrl>();
        actor ??= levelCtrl != null ? levelCtrl.Actor : null;
        if (flyingDoor == null)
            flyingDoor = GetComponentInChildren<FlyingDoorCtrl>(true);
        if (dogSpine == null && dogRoot != null)
            dogSpine = dogRoot.GetComponentInChildren<ActorSpineCtrl>(true);

        // 2026-08-06 地形拆分：现有陷坑左岸为 Ground_mid（入口假坑右沿段），右岸仍为 Ground_right。
        // 每侧由顶部端帽段、纵壁和底边三个叶节点拼成完整坑沿，父 Group 始终保持 active。
        pitLeftTopEdge = ResolvePitNode(pitLeftTopEdge, "Ground_mid", "Group_right", "Sp_ground_1_03");
        pitLeftWall = ResolvePitNode(pitLeftWall, "Ground_mid", "Group_right", "Sp_ground_1_09");
        pitLeftBottom = ResolvePitNode(pitLeftBottom, "Ground_mid", "Group_right", "Sp_ground_1_05");
        pitRightTopEdge = ResolvePitNode(pitRightTopEdge, "Ground_right", "Group_left", "Sp_ground_1_02");
        pitRightWall = ResolvePitNode(pitRightWall, "Ground_right", "Group_left", "Sp_ground_1_08");
        pitRightBottom = ResolvePitNode(pitRightBottom, "Ground_right", "Group_left", "Sp_ground_1_04");
        sceneCamera ??= GetComponentInChildren<Camera>(true);
        sceneCamera ??= Camera.main;

        if (pitGroundSegment != null)
            pitGroundRenderers = pitGroundSegment.GetComponentsInChildren<Renderer>(true);

        pitGroundCollider = ResolvePitGroundCollider();
        actorBodyCollider = ResolveActorBodyCollider();
    }

    private Collider2D ResolvePitGroundCollider()
    {
        if (pitGroundSegment == null)
            return null;

        Collider2D rootCollider = pitGroundSegment.GetComponent<Collider2D>();
        if (IsUsablePitGroundCollider(rootCollider))
            return rootCollider;

        Collider2D[] candidates = pitGroundSegment.GetComponentsInChildren<Collider2D>(true);
        for (int index = 0; index < candidates.Length; index++)
        {
            Collider2D candidate = candidates[index];
            if (IsUsablePitGroundCollider(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsUsablePitGroundCollider(Collider2D candidate)
    {
        return candidate != null &&
               candidate.enabled &&
               !candidate.isTrigger &&
               candidate.gameObject.activeInHierarchy;
    }

    private Collider2D ResolveActorBodyCollider()
    {
        if (actor == null)
            return null;

        // 标准 Actor 优先使用根节点 PolygonCollider2D；回退候选也必须是启用、非 Trigger 且实际激活的主体碰撞体，
        // 因此默认 inactive 的 pushCollider 不会被误选为角色离屏边界。
        Collider2D rootCollider = actor.GetComponent<Collider2D>();
        if (IsUsableActorBodyCollider(rootCollider))
            return rootCollider;

        Collider2D[] candidates = actor.GetComponentsInChildren<Collider2D>(true);
        for (int index = 0; index < candidates.Length; index++)
        {
            Collider2D candidate = candidates[index];
            if (IsUsableActorBodyCollider(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsUsableActorBodyCollider(Collider2D candidate)
    {
        return candidate != null &&
               candidate.enabled &&
               !candidate.isTrigger &&
               candidate.gameObject.activeInHierarchy;
    }

    private Transform ResolvePitNode(
        Transform currentReference,
        string groundName,
        string groupName,
        string nodeName)
    {
        // 先按真实层级逐段查找，避免同名 Sprite 在另一侧岸体中被误绑；旧 Prefab 若仍把字段指向 Group，
        // 最后的 currentReference 后代查找也只取目标叶节点，绝不把整个 Group 交给显隐逻辑。
        Transform groundRoot = FindDescendantByName(transform, groundName);
        Transform groupRoot = FindDescendantByName(groundRoot, groupName);
        Transform resolved = FindDescendantByName(groupRoot, nodeName);
        resolved ??= FindDescendantByName(currentReference, nodeName);
        return resolved;
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;
        if (root.name == targetName)
            return root;

        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
        {
            Transform resolved = FindDescendantByName(root.GetChild(childIndex), targetName);
            if (resolved != null)
                return resolved;
        }

        return null;
    }

    private void BindEvents()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
        subscriber.Subscribe<OnTriggerTrapEvent>(OnTriggerTrap);
        subscriber.Subscribe<OnTriggerFailAreaEvent>(OnTriggerFailArea);
        subscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private void InitializeGameplay()
    {
        KillTweens();
        dogRunTriggerReached = false;
        dogBarkPresentationCompleted = false;
        dogRunStarted = false;
        pitMovementFrozen = false;
        headSpikePendingSince = -1f;
        // 刺石平时完全不可见；仅玩家起跳输入瞬现。
        if (spikeStone != null)
            spikeStone.SetActive(false);
        // 小狗开局即作为可见诱饵待机；bark 与 run 使用两个独立的位置门槛。
        if (dogRoot != null)
            dogRoot.gameObject.SetActive(true);
        SetDogBarkEffectVisible(false);
        if (dogSpine != null)
            dogSpine.PlayAnimation(DogIdleAnimationName, true, false);
        // 六个坑沿叶节点在触发前隐藏；下沉开始后共同显示，回升完成后再共同隐藏。
        SetPitEdgesVisible(false);
        BuryFloorSpikes();
        flyingDoor?.Initialize(rule);
        SetupDropPlatform();
    }

    private void KillTweens()
    {
        KillDogChargeTween();
        KillFloorSpikesTween();
        KillPitTween();
    }

    private void KillPitTween()
    {
        if (pitTween != null && pitTween.IsActive())
            pitTween.Kill();
        pitTween = null;
    }

    private void KillFloorSpikesTween()
    {
        if (floorSpikesTween != null && floorSpikesTween.IsActive())
            floorSpikesTween.Kill();
        floorSpikesTween = null;
    }

    private void KillDogChargeTween()
    {
        ClearDogBarkCompletion();
        SetDogBarkEffectVisible(false);
        if (dogChargeTween != null && dogChargeTween.IsActive())
            dogChargeTween.Kill();
        dogChargeTween = null;
    }

    private void ClearDogBarkCompletion()
    {
        if (dogBarkEntry != null)
            dogBarkEntry.Complete -= OnDogBarkCompleted;
        dogBarkEntry = null;
    }

    #endregion

    #region 小狗位置触发

    /// <summary>bark 先按较远门槛触发；run 仅在原位置门槛与完整 bark 表现都完成后启动。</summary>
    private void UpdateDogSequenceAtActorPosition(float actorX)
    {
        if (levelCtrl == null || !levelCtrl.ActorCtrlable || dogRoot == null)
            return;

        float barkTriggerActorX = dogRoot.position.x - Mathf.Max(0f, dogBarkTriggerDistance);
        if (!rule.DogHasCharged && actorX >= barkTriggerActorX && rule.TryStartDogCharge())
            PlayDogBark();

        float runTriggerActorX = dogRoot.position.x - Mathf.Max(0f, dogTriggerDistance);
        if (actorX >= runTriggerActorX)
            dogRunTriggerReached = true;

        TryStartDogRunWhenReady();
    }

    #endregion

    #region 跳跃即死（玩家起跳输入通路）

    /// <summary>
    /// 玩家按下上跳键：Domain 唯一违规登记入口；可见阶段刺石瞬现头顶并跟随，判负以主角头部
    /// 真实撞击刺石 trigger（SpikeStoneTrapHandle）为准；完全离屏时只记录待重入结算的违规。
    /// 与陷坑自动上跳的脚本位移通路（<see cref="UpdatePitAutoBounce"/>）严格分离。
    /// </summary>
    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        if (!rule.NotifyPlayerJumpInput())
            return;

        ShowHeadSpike(false);
    }

    /// <summary>
    /// 刺石瞬现并固定于起跳点上空 spikeHeadOffset 处（不再跟随主角），登记待碰撞结算；
    /// 判负由 SpikeStoneTrapHandle 在主角主体真实接触刺石 trigger 时收口（播放 failed 音效 + OnTriggerTrapEvent 死亡链）。
    /// immediateContact 用于离屏违规重入的延迟结算：按固定 0.5 重叠放置，保证下一物理步必接触。
    /// </summary>
    private void ShowHeadSpike(bool immediateContact)
    {
        if (spikeStone == null || actor == null)
        {
            this.LogWarning("[Level103Ctrl] 头顶刺石或主角未绑定，跳过刺石表现，直接走死亡链。");
            AudioManager.PlaySound("failed");
            this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
            return;
        }

        float offset = immediateContact ? 0.5f : spikeHeadOffset;
        spikeStone.transform.position = actor.transform.position + Vector3.up * offset;
        spikeStone.SetActive(true);
        headSpikePendingSince = Time.time;
    }

    /// <summary>
    /// 刺石待碰撞期间固定悬停不再跟随，由主角上升真实撞刺收口；
    /// 超过 1 秒未接触说明物理通路异常，按同一死亡链兜底，防软锁。
    /// </summary>
    private void UpdatePendingHeadSpike()
    {
        if (spikeStone == null || !spikeStone.activeSelf || headSpikePendingSince < 0f)
            return;

        if (actor == null || actor.IsDied)
        {
            headSpikePendingSince = -1f;
            return;
        }

        if (Time.time - headSpikePendingSince > 1f)
        {
            headSpikePendingSince = -1f;
            this.LogWarning("[Level103Ctrl] 头顶刺石超过 1 秒未与主角接触，按死亡链兜底收口。");
            AudioManager.PlaySound("failed");
            this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
            return;
        }

        // 刺石固定悬停于起跳点上空，不再每帧贴合主角头顶。
    }

    #endregion

    #region 诱饵小狗

    /// <summary>
    /// 开局待机的小狗在主角达到较远的 dogBarkTriggerDistance 时先原地播放一次 bark；
    /// bark 完成后保持最终姿态 dogPostBarkHold 秒。只有原 dogTriggerDistance 门槛也已到达才切 run，
    /// 再通知 Domain 记录小狗完成；小狗完成不再推进或阻塞陷坑阶段。
    /// </summary>
    private void PlayDogBark()
    {
        if (dogRoot == null)
        {
            // 极端引用失效时只跳过小狗表现；陷坑仍按独立位置门禁运行，不伪造小狗完成信号。
            this.LogWarning("[Level103Ctrl] 诱饵小狗未绑定，跳过小狗表现并保持当前关卡阶段。");
            return;
        }

        KillDogChargeTween();
        dogRoot.gameObject.SetActive(true);
        SetDogBarkEffectVisible(true);
        Spine.TrackEntry barkEntry = dogSpine?.PlayAnimation(DogBarkAnimationName, false, false);
        if (!string.IsNullOrEmpty(dogSfxTag))
            AudioManager.PlaySound(dogSfxTag, Mathf.Clamp01(dogSfxVolume));

        if (barkEntry == null)
        {
            CompleteDogBarkPresentation();
            return;
        }

        dogBarkEntry = barkEntry;
        dogBarkEntry.Complete += OnDogBarkCompleted;
    }

    private void OnDogBarkCompleted(Spine.TrackEntry completedEntry)
    {
        if (dogBarkEntry != completedEntry)
            return;

        ClearDogBarkCompletion();
        if (dogRoot == null || rule.Result != Level103Result.None)
        {
            SetDogBarkEffectVisible(false);
            return;
        }

        StartDogRunAfterBarkHold();
    }

    private void StartDogRunAfterBarkHold()
    {
        float holdDuration = Mathf.Max(0f, dogPostBarkHold);
        if (holdDuration <= 0f)
        {
            CompleteDogBarkPresentation();
            return;
        }

        dogChargeTween = DOVirtual.DelayedCall(holdDuration, () =>
        {
            dogChargeTween = null;
            if (dogRoot == null || rule.Result != Level103Result.None)
            {
                SetDogBarkEffectVisible(false);
                return;
            }

            CompleteDogBarkPresentation();
        }).SetLink(gameObject);
    }

    private void CompleteDogBarkPresentation()
    {
        if (dogRoot == null || rule.Result != Level103Result.None)
        {
            SetDogBarkEffectVisible(false);
            return;
        }

        SetDogBarkEffectVisible(false);
        dogBarkPresentationCompleted = true;
        if (!dogRunTriggerReached)
            dogSpine?.PlayAnimation(DogIdleAnimationName, true, false);
        TryStartDogRunWhenReady();
    }

    private void TryStartDogRunWhenReady()
    {
        if (dogRunStarted || !dogRunTriggerReached || !dogBarkPresentationCompleted
            || dogRoot == null || rule.Result != Level103Result.None)
            return;

        dogRunStarted = true;
        StartDogRun();
    }

    private void StartDogRun()
    {
        if (dogRoot == null)
            return;

        SetDogBarkEffectVisible(false);
        dogSpine?.PlayAnimation(DogRunAnimationName, true, false);

        // 入口假坑上方有与地面线齐平的隐形承托（Ground_airbridge），小狗无需上跳；
        // 保持最初的贴地直线冲锋（2026-08-06 用户确认撤销跨坑抛物线，恢复原始逻辑）。
        float distance = Mathf.Abs(dogExitX - dogRoot.position.x);
        float duration = Mathf.Max(0.01f, distance / Mathf.Max(0.01f, dogRunSpeed));
        dogChargeTween = dogRoot.DOMoveX(dogExitX, duration)
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                dogChargeTween = null;
                if (dogRoot == null)
                    return;

                dogRoot.gameObject.SetActive(false);
                rule.NotifyDogPassCompleted();
            });
    }

    private void SetDogBarkEffectVisible(bool isVisible)
    {
        if (dogBarkEffect == null)
            return;

        if (isVisible)
        {
            // 关闭再开启，让循环 Animator 每次都从第一帧开始。
            dogBarkEffect.SetActive(false);
            dogBarkEffect.SetActive(true);
            return;
        }

        dogBarkEffect.SetActive(false);
    }

    #endregion

    #region 门前地面尖刺 · 破土弹出 / 悬空平台 · 固定摆放

    /// <summary>
    /// 初始化时把尖刺埋回地面线：记录 Prefab 保存的已长出根姿态，scaleY 收为 0 并把
    /// FloorSpikes 参考底线固定在原高度；子实例的遮根对齐 Y 偏移随父级缩放，过程位于地板遮挡后。
    /// 失败重开由既有流程销毁重建关卡，Start 会再次执行本方法。
    /// </summary>
    private void BuryFloorSpikes()
    {
        if (floorSpikes == null)
            return;

        float stripHeight = 0.48f;
        SpriteRenderer stripRenderer = floorSpikes.GetComponentInChildren<SpriteRenderer>(true);
        if (stripRenderer != null && stripRenderer.size.y > 0.001f)
            stripHeight = stripRenderer.size.y;

        floorSpikesEmergedY = floorSpikes.position.y;
        floorSpikesBuriedY = floorSpikesEmergedY - stripHeight * 0.5f;

        Vector3 buriedScale = floorSpikes.localScale;
        buriedScale.y = 0f;
        floorSpikes.localScale = buriedScale;
        Vector3 buriedPosition = floorSpikes.position;
        buriedPosition.y = floorSpikesBuriedY;
        floorSpikes.position = buriedPosition;
    }

    /// <summary>
    /// 尖刺破土：scaleY 0→1 与 Y 同步、同一 OutBack 缓动的双 Tween，FloorSpikes 参考底线保持固定；
    /// 子实例用于对齐 Level 029 遮根高度的少量 Y 偏移始终位于地板后方，刺尖过冲后回弹。
    /// 音效仅在配置了既有 SoundsGood tag 时播放。
    /// </summary>
    private void PlayFloorSpikesEmerge()
    {
        if (floorSpikes == null)
        {
            this.LogWarning("[Level103Ctrl] 门前地面尖刺未绑定，跳过破土演出。");
            return;
        }

        KillFloorSpikesTween();
        if (!string.IsNullOrEmpty(floorSpikesEmergeSfxTag))
            AudioManager.PlaySound(floorSpikesEmergeSfxTag);

        float duration = Mathf.Max(0.05f, floorSpikesEmergeDuration);
        floorSpikesTween = DOTween.Sequence()
            .Append(floorSpikes.DOScaleY(1f, duration).SetEase(Ease.OutBack).SetLink(gameObject))
            .Join(floorSpikes.DOMoveY(floorSpikesEmergedY, duration).SetEase(Ease.OutBack).SetLink(gameObject))
            .SetLink(gameObject)
            .OnComplete(() => floorSpikesTween = null);
    }

    /// <summary>
    /// 初始化悬空平台：用渲染包围盒把平台视觉右沿对齐 dropPlatformRightX、顶面高度取
    /// 悬停门体底部以下 dropPlatformBelowDoorBottom 个世界单位，开局即固定摆放到该目标位
    /// （2026-08-07 起不再有屏顶停放与从天而降的演出，落地特效/音效随之移除）；素材更换后对齐关系仍成立。
    /// </summary>
    private void SetupDropPlatform()
    {
        if (dropPlatform == null)
            return;

        Renderer[] renderers = dropPlatform.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            this.LogWarning("[Level103Ctrl] 悬空平台无渲染器，跳过定位。");
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float rootX = dropPlatformRightX - (bounds.max.x - dropPlatform.position.x);
        float hoverDoorBottomY = flyingDoor != null ? flyingDoor.HoverBottomY : 0f;
        float restY = hoverDoorBottomY - dropPlatformBelowDoorBottom - (bounds.max.y - dropPlatform.position.y);
        dropPlatform.position = new Vector3(rootX, restY, dropPlatform.position.z);
    }

    #endregion

    #region 陷坑

    /// <summary>
    /// 坑前半落：触发同帧显现两侧完整固定坑沿（顶部端帽段、纵壁与底边）并播放既有音效，碰撞保持开启；
    /// 随后顶部、实心主体与底边按完整下落距离的一半刚性垂直下坠（0.3s，2026-08-06 经 0.2s 试调后改回），站在块上的主角随块下沉；
    /// 下坠完成后在玩家尚未失败时等待 pitRiseDelay（当前 3.8s）再自动回升原位；失败后保持当时位置与坑沿状态。全程一次性。
    /// </summary>
    private void PlayGroundCollapse()
    {
        if (pitMovementFrozen)
            return;

        KillPitTween();

        SetPitEdgesVisible(true);

        if (!string.IsNullOrEmpty(pitCollapseSfxTag))
            AudioManager.PlaySound(pitCollapseSfxTag);

        if (pitGroundSegment == null)
        {
            this.LogWarning("[Level103Ctrl] 完整塌落地块未绑定，跳过半落演出。");
            rule.NotifyGroundCollapseCompleted();
            pitTween = DOVirtual.DelayedCall(pitRiseDelay, StartPitRise).SetLink(gameObject);
            return;
        }

        pitOriginalY = pitGroundSegment.position.y;
        float dropDistance = CalculatePitDropDistance() * 0.5f;
        pitTween = pitGroundSegment
            .DOMoveY(pitOriginalY - dropDistance, EffectivePitDropDuration)
            .SetEase(Ease.InQuad)
            .SetLink(gameObject)
            .OnComplete(OnPitHalfDropCompleted);
    }

    /// <summary>半落下坠完成：通知 Domain 进入等待期（GapOpen），并启动当前 3.8 秒回升倒计时。</summary>
    private void OnPitHalfDropCompleted()
    {
        pitTween = null;
        if (pitMovementFrozen)
            return;

        rule.NotifyGroundCollapseCompleted();
        pitTween = DOVirtual.DelayedCall(pitRiseDelay, StartPitRise).SetLink(gameObject);
    }

    /// <summary>
    /// 正常等待结束：无论主角在块上、坑内弹跳还是已脱困都按时回升（不做坑道占用检测）；
    /// 回升复用同一下坠时长与同一既有音效，到位同帧隐藏坑壁与底边，地形恢复平整完整可见。
    /// </summary>
    private void StartPitRise()
    {
        pitTween = null;
        if (pitMovementFrozen)
            return;

        if (pitGroundSegment == null)
        {
            SetPitEdgesVisible(false);
            rule.NotifyPitRestored();
            return;
        }

        rule.NotifyPitRiseStarted();
        if (!string.IsNullOrEmpty(pitCollapseSfxTag))
            AudioManager.PlaySound(pitCollapseSfxTag);

        pitTween = pitGroundSegment
            .DOMoveY(pitOriginalY, EffectivePitDropDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                pitTween = null;
                if (pitMovementFrozen)
                    return;

                SetPitEdgesVisible(false);
                rule.NotifyPitRestored();
            });
    }

    private float CalculatePitDropDistance()
    {
        float dropDistance = Mathf.Max(4.8f, pitDropDistance);
        if (TryGetPitGroundExitDistance(out float requiredDistance))
            dropDistance = Mathf.Max(dropDistance, requiredDistance);

        return dropDistance;
    }

    private bool TryGetPitGroundExitDistance(out float distance)
    {
        distance = 0f;
        if (!TryGetCameraBottomY(pitGroundSegment != null ? pitGroundSegment.position.z : transform.position.z,
                out float cameraBottomY) ||
            !TryGetPitGroundRendererMaxY(out float rendererMaxY))
            return false;

        const float hideMargin = 0.05f;
        distance = Mathf.Max(0f, rendererMaxY - cameraBottomY + hideMargin);
        return true;
    }

    private bool TryGetPitGroundRendererMaxY(out float maxY)
    {
        maxY = float.NegativeInfinity;
        bool found = false;
        if (pitGroundRenderers == null && pitGroundSegment != null)
            pitGroundRenderers = pitGroundSegment.GetComponentsInChildren<Renderer>(true);

        if (pitGroundRenderers == null)
            return false;

        for (int index = 0; index < pitGroundRenderers.Length; index++)
        {
            Renderer targetRenderer = pitGroundRenderers[index];
            if (targetRenderer == null || !targetRenderer.enabled || !targetRenderer.gameObject.activeInHierarchy)
                continue;

            maxY = Mathf.Max(maxY, targetRenderer.bounds.max.y);
            found = true;
        }

        return found;
    }

    /// <summary>同步显隐左右完整坑沿；每侧三个叶节点必须共同切换，父 Group 始终保持 active。</summary>
    private void SetPitEdgesVisible(bool isVisible)
    {
        if (pitLeftTopEdge != null)
            pitLeftTopEdge.gameObject.SetActive(isVisible);
        if (pitLeftWall != null)
            pitLeftWall.gameObject.SetActive(isVisible);
        if (pitLeftBottom != null)
            pitLeftBottom.gameObject.SetActive(isVisible);
        if (pitRightTopEdge != null)
            pitRightTopEdge.gameObject.SetActive(isVisible);
        if (pitRightWall != null)
            pitRightWall.gameObject.SetActive(isVisible);
        if (pitRightBottom != null)
            pitRightBottom.gameObject.SetActive(isVisible);
    }

    /// <summary>
    /// 返回当前陷坑的世界 X 开口边界。正常路径直接读取塌落地块承托 Collider2D 的 bounds，
    /// 所以 Prefab 中重定位坑位后，触发、掉入和自动上跳判定会随之同步；旧序列化值只在绑定异常时保底。
    /// </summary>
    private void GetPitOpeningEdges(out float leftEdge, out float rightEdge)
    {
        if (!IsUsablePitGroundCollider(pitGroundCollider))
            pitGroundCollider = ResolvePitGroundCollider();

        if (IsUsablePitGroundCollider(pitGroundCollider))
        {
            Bounds bounds = pitGroundCollider.bounds;
            if (bounds.size.x > Mathf.Epsilon)
            {
                leftEdge = bounds.min.x;
                rightEdge = bounds.max.x;
                return;
            }
        }

        leftEdge = pitCenterX - pitHalfWidth;
        rightEdge = pitCenterX + pitHalfWidth;
    }

    /// <summary>
    /// 主角 pivot 是否进入当前陷坑开口。坑口边界来自塌落地块 Collider2D 的实时世界 bounds，
    /// 因而 Prefab 重定位坑位后不需要手动同步固定触发坐标。
    /// </summary>
    private bool IsActorPivotInsidePitOpening()
    {
        GetPitOpeningEdges(out float leftEdge, out float rightEdge);
        float pivotX = actor.transform.position.x;
        return pivotX >= leftEdge && pivotX <= rightEdge;
    }

    private bool IsActorInsidePit()
    {
        Vector3 position = actor.transform.position;
        GetPitOpeningEdges(out float leftEdge, out float rightEdge);
        return position.x >= leftEdge && position.x <= rightEdge && position.y <= pitEnterDetectY;
    }

    /// <summary>
    /// 主角是否正站在半落地块上随其下沉/回升：站稳、水平在坑口内、且 pivot 与块顶站立面贴合。
    /// 随块位移不属于掉入陷坑，不进入自动上跳；只有从块沿主动走下坑道（离开块面下落）才登记掉入。
    /// </summary>
    private bool IsActorRidingPitGround()
    {
        if (pitGroundSegment == null || actor == null || !actor.IsGround)
            return false;

        Vector3 position = actor.transform.position;
        GetPitOpeningEdges(out float leftEdge, out float rightEdge);
        if (position.x < leftEdge || position.x > rightEdge)
            return false;

        // 块顶站立面 pivotY = 块根 Y + 碰撞体顶偏移 0.4245（offset.y 0.3245 + size.y/2 0.1）+ 站立 pivot 偏移约 0.48。
        float ridingY = pitGroundSegment.position.y + 0.9045f;
        return Mathf.Abs(position.y - ridingY) < 0.5f;
    }

    /// <summary>
    /// 陷坑自动上跳：脚本驱动位移，必须走 Domain 的非玩家输入入口
    /// <see cref="Level103DontJumpRule.NotifyScriptDrivenLift"/>（永不判负、不引来刺石），
    /// 与玩家起跳输入通路（<see cref="OnPlayerJump"/>）在代码结构上严格分离。
    /// 玩家全程保留左右水平控制；弹跳拱顶按坑口地面线 + 安全余量标定，一次弹跳即可漂过坑沿上岸。
    /// </summary>
    private void UpdatePitAutoBounce()
    {
        if (UpdatePitActorScreenState())
            return;

        Vector3 position = actor.transform.position;
        GetPitOpeningEdges(out float leftEdge, out float rightEdge);
        bool isHorizontallyInsidePit = position.x >= leftEdge && position.x <= rightEdge;

        if (!actor.IsGround)
        {
            pitBounceAirborne = true;
            return;
        }

        // 脱困（站稳帧）：水平移出余量带，或已站回坑口地面线以上（岸上或坑沿墙顶）均立即脱困；
        // 完全离屏阶段由 Domain 门禁与 LateUpdate 水平锁共同保护，不登记脱困。
        if (!rule.PitActorIsFullyOffscreen &&
            (position.x < leftEdge - pitExitMargin ||
             position.x > rightEdge + pitExitMargin ||
             position.y > pitRimY))
        {
            rule.NotifyPitEdgeExited();
            return;
        }

        // 起跳三条件（Level 105 坑底跳先例）：站稳、水平在坑口内、pivotY 低于坑底起跳线；
        // 坑沿墙顶与岸上 pivotY ≈ -2.10 远高于本线，天然排除，实际只有落到离屏承托面才弹。
        // 每次落地只起弹一次（消费 pitBounceAirborne），站稳帧不重复施加速度、不连播音效。
        if (!pitBounceAirborne || !isHorizontallyInsidePit || position.y > pitBounceFloorY ||
            !rule.NotifyScriptDrivenLift())
            return;

        pitBounceAirborne = false;
        Rigidbody2D body = actor.RB;
        if (body == null)
            return;

        // 与 ActorCtrl.Jump 同源的全项目通用跳跃音效；每次脚本起弹（含完全离屏时）都播。
        AudioManager.PlaySound("jump");
        body.linearVelocity = new Vector2(
            rule.PitActorIsFullyOffscreen ? 0f : body.linearVelocityX,
            EffectivePitBounceVelocity);
    }

    /// <summary>
    /// 以角色主 Collider2D（主体碰撞体）的世界 bounds 判断：上沿低于相机下边界才算完整离屏；
    /// 上沿重新触及画面时立即恢复水平控制，并结算离屏期间记录的主动跳跃违规。
    /// </summary>
    /// <returns>本帧是否已触发延迟死亡。</returns>
    private bool UpdatePitActorScreenState()
    {
        actorBodyCollider = ResolveActorBodyCollider();
        if (actorBodyCollider == null ||
            !TryGetCameraBottomY(actorBodyCollider.bounds.center.z, out float cameraBottomY))
            return false;

        bool isFullyOffscreen = actorBodyCollider.bounds.max.y < cameraBottomY;
        if (!rule.PitActorIsFullyOffscreen)
        {
            if (isFullyOffscreen)
                rule.NotifyPitActorFullyOffscreen();
            return false;
        }

        if (isFullyOffscreen)
            return false;

        if (!rule.NotifyPitActorReenteredView())
            return false;

        ShowHeadSpike(true);
        return true;
    }

    private bool TryGetCameraBottomY(float worldZ, out float cameraBottomY)
    {
        if (sceneCamera == null)
            sceneCamera = Camera.main;
        if (sceneCamera == null)
            sceneCamera = GetComponentInChildren<Camera>(true);

        if (sceneCamera == null)
        {
            cameraBottomY = 0f;
            return false;
        }

        float cameraDistance = Mathf.Abs(sceneCamera.transform.position.z - worldZ);
        cameraBottomY = sceneCamera.ViewportToWorldPoint(new Vector3(0.5f, 0f, cameraDistance)).y;
        return true;
    }

    private float EffectivePitDropDuration =>
        pitDropDuration >= 0.2f && pitDropDuration <= 0.4f ? pitDropDuration : 0.3f;

    // 下限只做非负保护（Level 105 先例）：标定值本身已含拱顶余量，不再设 26 的静默抬升下限。
    private float EffectivePitBounceVelocity => Mathf.Max(0f, pitBounceVelocity);

    #endregion

    #region Reset

    private void OnTriggerTrap(OnTriggerTrapEvent evt)
    {
        FreezePitMovement();
    }

    private void OnTriggerFailArea(OnTriggerFailAreaEvent evt)
    {
        FreezePitMovement();
    }

    private void FreezePitMovement()
    {
        if (pitMovementFrozen)
            return;

        pitMovementFrozen = true;
        KillPitTween();
    }

    private void OnGameReset(GameResetEvent evt)
    {
        // 失败收口：升降地块保持失败瞬间状态；其它本关 Tween 沿用既有重置清理。
        FreezePitMovement();
        KillTweens();
    }

    #endregion
}

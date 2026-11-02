using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Level104;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level 104「不要回头」本地玩法编排器（组合形态，Level 103 先例）：
/// 纯规则判定收敛到 <see cref="Level104DontLookBackRule"/>，本组件只负责输入、碰撞、Tween、
/// 音画表现与既有失败/通关链适配。玩家左移输入与脚本绕屏位移始终走两条独立通路。
/// </summary>
public sealed class Level104Ctrl : MonoBehaviour, IDebuger, IEventSender
{
    #region Constants

    private const string DogIdleAnimationName = "idle";
    private const string DogBarkAnimationName = "bark";
    private const string DogRunAnimationName = "run";
    private const string DogJumpDownAnimationName = "jump_down";

    /// <summary>玩家从左侧首次到达最左轨左侧 5 个世界单位的世界 X 门槛；当前为 -5.18，不再等于覆盖区左界。</summary>
    private const float ArrowVolleyTriggerActorX = -5.18f;

    /// <summary>
    /// 固定十条世界落箭轨道；不得按玩家实时 X 平移，也不得把第一轨写成门槛常量。
    /// 2026-08-13：以旧五轨最左 `-5.18` 为原点按间距 1 向右扩到 12 支，再整组静态右移 5；同日再删最右两轨，保留 `-0.18…8.82`。
    /// 门槛是最左轨左侧 5 个世界单位（-5.18），不等于覆盖区左界 -0.18。
    /// 落箭顺序保持从右往左；当前标定单支下落 0.45s / Ease.InQuad，落地 0.15s 渐隐。
    /// </summary>
    private static readonly float[] ArrowWorldXs =
    {
        -0.18f,
        0.82f,
        1.82f,
        2.82f,
        3.82f,
        4.82f,
        5.82f,
        6.82f,
        7.82f,
        8.82f
    };

    /// <summary>
    /// arrowWhoosh.ogg 自带约 0.54s 前导静音（078 实测发声段 0.54–0.74s）。
    /// 104 单箭下落仅 0.45s，经 AudioManager 从头播放时落地后才出声，听感上像没加破空音。
    /// </summary>
    private const float ArrowWhooshSfxStartSeconds = 0.54f;

    /// <summary>破空音以 1.0 播放；AudioManager 默认单次音量 0.4 会把本就短的发声段再压一层。</summary>
    private const float ArrowWhooshSfxVolume = 1f;

    #endregion

    #region Inspector

    /// <summary>承载标准输入开关、门触发通关链与失败重开流程的关卡基础控制器。</summary>
    [SerializeField] private BaseLevelCtrl levelCtrl;

    /// <summary>主角 Box；为空时回退取 levelCtrl.Actor。</summary>
    [SerializeField] private ActorCtrl actor;

    [Header("回头即死 · 执法刺")]
    /// <summary>平时隐藏，玩家按下左移键时固定于违规时刻主角左侧 spikeFrontOffset 处不再跟随；主角继续左移、面部真实撞击刺石 trigger（SpikeStoneTrapHandle）才判负。</summary>
    [SerializeField] private GameObject spikeStone;

    /// <summary>执法刺固定点相对违规时刻主角中心的水平距离（世界单位）。2026-08-06 经 1、2 试调后定为 1.5：略大于刺石与主角的半宽和（约 1.32），主角继续左移极短距离后真实撞刺。</summary>
    [SerializeField] private float spikeFrontOffset = 1.5f;

    /// <summary>执法刺瞬现待碰撞收口的起始时间（Time.time）；&lt; 0 表示无待结算。超过 1 秒未接触走兜底死亡链，防软锁。</summary>
    private float frontSpikePendingSince = -1f;

    [Header("诱饵小狗")]
    /// <summary>开局位于右门左侧、可见并播放 idle 的小狗根节点。</summary>
    [SerializeField] private Transform dogRoot;

    /// <summary>小狗 Spine 动画控制器。</summary>
    [SerializeField] private ActorSpineCtrl dogSpine;

    /// <summary>bark 触发距离：当前狗 X=34.5、距离 8，对应玩家 X=26.5。</summary>
    [SerializeField] private float dogBarkTriggerDistance = 8f;

    /// <summary>run 门槛保持原距离 6；当前狗 X=34.5，对应玩家 X=28.5。</summary>
    [SerializeField] private float dogChargeTriggerDistance = 6f;

    /// <summary>小狗冲锋速度；当前用户确认为 6 世界单位/秒。</summary>
    [SerializeField] private float dogRunSpeed = 6f;

    /// <summary>固定致死坑右缘回退值；运行时优先读 Ground_pit_right 碰撞左缘。</summary>
    [SerializeField] private float dogPitDropX = 21.52f;

    /// <summary>狗踩空后落到该世界 Y，低于相机底缘后隐藏。</summary>
    [SerializeField] private float dogPitFallY = -10f;

    /// <summary>狗从坑口右缘下落到 dogPitFallY 的时长；InQuad。</summary>
    [SerializeField] private float dogPitFallDuration = 0.6f;

    /// <summary>bark 完成后保持最终叫喊姿态的时长；结束后等待原 run 门槛。</summary>
    [SerializeField, Min(0f)] private float dogPostBarkHold = 0.4f;

    /// <summary>复用 Noise/Bark.prefab 的叫喊声波特效；bark 与蓄力停顿期间显示，切换 run 时隐藏。</summary>
    [SerializeField] private GameObject dogBarkEffect;

    /// <summary>小狗冲锋前播放 bark 动画时同步播放的狗叫音效。</summary>
    [SerializeField] private string dogSfxTag = "bark";

    /// <summary>本关狗叫音量；1 为 SoundsGood 单次音效最大音量，只影响本关这次 bark 播放。</summary>
    [SerializeField, Range(0f, 1f)] private float dogSfxVolume = 1f;

    /// <summary>小狗首次命中后，在首次接触点播放的 Knock_Explosion 粒子。</summary>
    [SerializeField] private ParticleSystem dogHitEffect;

    /// <summary>小狗撞击音效；与 Level 083 冲撞链保持 trap。</summary>
    [SerializeField] private string dogHitSfxTag = "trap";

    /// <summary>首次接触后到粒子、音效与击飞生效前的等待。</summary>
    [SerializeField] private float dogHitDelay = 0.1f;

    /// <summary>主角向左上方击飞方向。</summary>
    [SerializeField] private Vector2 dogKnockbackDirection = new Vector2(-0.5f, 0.5f);

    /// <summary>主角击飞力度。</summary>
    [SerializeField] private float dogKnockbackForce = 50f;

    /// <summary>主角每次完整翻滚的时长。</summary>
    [SerializeField] private float dogRotationDuration = 0.5f;

    [Header("固定坑平台 · 左岸按钮")]
    /// <summary>固定致死坑中央的 Plantform_pit；按钮冒出不再读取该平台落地。</summary>
    [SerializeField] private Transform pitPlatform;

    /// <summary>复用 SceneCommonButton 的左岸按钮；初始隐藏，玩家中心到达绝对世界 X=4.5 后只冒出一次。</summary>
    [SerializeField] private Transform pitPlatformButton;

    /// <summary>按钮冒出的绝对世界 X 门槛；玩家中心首次到达或越过该点时触发。</summary>
    [SerializeField] private float pitPlatformButtonRevealActorX = 4.5f;

    /// <summary>按钮从终态地表位置下方开始冒出的距离（世界单位）。</summary>
    [SerializeField] private float pitPlatformButtonRiseDistance = 0.6f;

    /// <summary>按钮上升并恢复完整纵向缩放的时长。</summary>
    [SerializeField] private float pitPlatformButtonRevealDuration = 0.3f;

    [Header("升降陷坑 · 刺墙联动")]
    /// <summary>坑口宽度内的完整升降地块；顶面、实心主体、底边和表面小石块共同半落回升。</summary>
    [SerializeField] private Transform liftPitGroundSegment;

    /// <summary>左岸顶部端帽、纵壁和底边；下沉开始至回升完成前共同显示。</summary>
    [SerializeField] private Transform liftPitLeftTopEdge;
    [SerializeField] private Transform liftPitLeftWall;
    [SerializeField] private Transform liftPitLeftBottom;

    /// <summary>右岸顶部端帽、纵壁和底边；下沉开始至回升完成前共同显示。</summary>
    [SerializeField] private Transform liftPitRightTopEdge;
    [SerializeField] private Transform liftPitRightWall;
    [SerializeField] private Transform liftPitRightBottom;

    /// <summary>完整下落距离基准；实际半落距离取运行时完整距离的一半。</summary>
    [SerializeField] private float liftPitDropDistance = 4.8f;

    /// <summary>半落完成后等待回升的秒数。</summary>
    [SerializeField] private float liftPitRiseDelay = 5f;

    /// <summary>半落和回升各自的时长；当前合同为 0.25 秒。</summary>
    [SerializeField] private float liftPitDropDuration = 0.25f;

    [SerializeField] private string liftPitCollapseSfxTag = "l89StonePillarDrop";

    /// <summary>Collider 异常时使用的坑口中心与半宽回退值。</summary>
    [SerializeField] private float liftPitCenterX = 37.5f;
    [SerializeField] private float liftPitHalfWidth = 1.245f;

    /// <summary>刺墙启动前，Actor 主碰撞体左缘必须越过坑口右缘的最小余量。</summary>
    [SerializeField, Min(0f)] private float liftPitActorClearance = 0.15f;

    /// <summary>
    /// 刺墙有效触发 Collider 左缘与 Actor 主体 Collider 右缘的水平间距门槛；
    /// 刺墙已启动逼近且尚未反弹时，间距 ≤ 该值才揭坑。Collider 无法解析时不得用中心距离猜测提前触发。
    /// </summary>
    [SerializeField, Min(0f)] private float liftPitRevealWallGap = 2f;

    [SerializeField] private float liftPitEnterDetectY = -3.5f;
    [SerializeField] private float liftPitRimY = -2.5f;
    [SerializeField] private float liftPitBounceFloorY = -8.5f;
    [SerializeField] private float liftPitExitMargin = 0.2f;
    [SerializeField] private float liftPitBounceVelocity = 28f;

    [Header("追逐箭阵")]
    /// <summary>十支箭，数组顺序必须为从左到右；运行时强制使用固定世界 X。</summary>
    [SerializeField] private Transform[] arrows;

    /// <summary>相邻箭启动间隔；运行时收紧为严格小于单箭下落时长，当前为 0.05 秒。</summary>
    [SerializeField] private float arrowStaggerDelay = 0.05f;

    /// <summary>单支箭从出生高度到落地高度的时长；下落 Tween 固定使用 Ease.InQuad，当前为 0.45 秒。</summary>
    [SerializeField] private float arrowFallDuration = 0.45f;

    /// <summary>与 Level 078 相同的暖白箭矢拖尾材质；未绑定时只关闭拖尾，不阻断箭雨。</summary>
    [SerializeField] private Material arrowTrailMaterial;

    /// <summary>停止生成后拖尾自然残留的时长；当前恢复为 0.1 秒。</summary>
    [SerializeField, Min(0f)] private float arrowTrailTime = 0.1f;

    /// <summary>箭尾处拖尾起始宽度；104 专属放大，当前为 0.2 并沿宽度曲线收窄到零。</summary>
    [SerializeField, Min(0f)] private float arrowTrailWidth = 0.2f;

    /// <summary>箭命中主角时在首次接触点播放的共享 Knock_Explosion 粒子。</summary>
    [SerializeField] private ParticleSystem arrowHitEffect;

    /// <summary>箭命中的撞击音效；与本关其它真实撞击保持 trap。</summary>
    [SerializeField] private string arrowHitSfxTag = "trap";

    /// <summary>箭接地、关闭碰撞后的渐隐时长；当前为 0.15 秒。</summary>
    [SerializeField] private float arrowFadeDuration = 0.15f;

    /// <summary>箭出生世界 Y。</summary>
    [SerializeField] private float arrowSpawnY = 7f;

    /// <summary>箭接地世界 Y。</summary>
    [SerializeField] private float arrowGroundY = -1.6f;

    /// <summary>箭矢破空音；资源自带约 0.54s 前导静音，由本关自带 AudioSource 跳过静音起播。</summary>
    [SerializeField] private AudioClip arrowWhooshClip;

    [Header("竖刺墙")]
    /// <summary>竖刺墙根节点。</summary>
    [SerializeField] private Transform spikeWallRoot;

    /// <summary>刺墙候选启动点；当前 104 为 X=41，且 Actor 主碰撞体必须已完整越过升降坑右缘。</summary>
    [SerializeField] private float spikeWallStartActorX = 41f;

    /// <summary>竖刺墙向左逼近速度；当前玩法合同固定为 2.5 世界单位/秒。</summary>
    [SerializeField] private float spikeWallAdvanceSpeed = 2.5f;

    /// <summary>竖刺墙碰到玩家后向右反弹速度；保持 8。</summary>
    [SerializeField] private float spikeWallBounceSpeed = 8f;

    /// <summary>竖刺墙反弹终点世界 X，到位后隐藏。三屏场景锁定视野右缘为 52.5，终点 53.1 保留 0.6 世界单位的屏外余量。</summary>
    [SerializeField] private float spikeWallExitX = 53.1f;

    [Header("飞门")]
    /// <summary>本关飞门可见折线编排器：上升、向左横飞、落地并收翅。</summary>
    [SerializeField] private Level104FlyingDoorCtrl flyingDoor;

    [Header("循环绕屏")]
    /// <summary>主角达到该世界 X 后绕回左屏。三屏场景锁定视野右缘为 52.5，绕屏点 53.1 位于屏外且距地板碰撞右沿 53.7 仍有 0.6 世界单位承托余量。</summary>
    [SerializeField] private float wrapRightX = 53.1f;

    /// <summary>脚本绕屏传送落点世界 X。</summary>
    [SerializeField] private float wrapLeftX = -11.3f;

    #endregion

    #region Runtime State

    private readonly Level104DontLookBackRule rule = new Level104DontLookBackRule();
    private readonly List<Tween> arrowTweens = new List<Tween>();
    private readonly Dictionary<Transform, TrailRenderer> arrowTrails = new Dictionary<Transform, TrailRenderer>();
    private readonly List<AudioSource> arrowWhooshSources = new List<AudioSource>();

    private EventSubscriber subscriber;
    private Spine.TrackEntry dogBarkEntry;
    private Tween dogChargeTween;
    private bool dogRunTriggerReached;
    private bool dogBarkPresentationCompleted;
    private bool dogRunStarted;
    private Tween spikeWallTween;
    private Tween dogHitDelayTween;
    private Tween dogActorRotationTween;
    private Level104TrapTrigger dogTrapTrigger;
    private bool hasPendingSpikeWallTouch;
    private float pendingSpikeWallActorX;
    private bool isTeardownRequested;
    private Camera levelCamera;
    private Vector3 levelCameraInitialPosition;
    private bool hasLevelCameraInitialPosition;
    private CameraFollowRange cameraFollowRange;

    private Tween pitPlatformButtonRevealTween;
    private Collider2D[] pitPlatformButtonColliders;
    private Renderer[] pitPlatformButtonRenderers;
    private Vector3 pitPlatformButtonVisiblePosition;
    private Vector3 pitPlatformButtonVisibleScale;
    private bool pitPlatformButtonHasRevealed;

    private Tween liftPitTween;
    private Renderer[] liftPitGroundRenderers;
    private float liftPitOriginalY;
    private Collider2D liftPitGroundCollider;
    private Collider2D liftPitActorBodyCollider;
    /// <summary>刺墙有效触发 Collider（挂有 Level104SpikeWallTrigger 的 Trigger）；用于揭坑间距判定。</summary>
    private Collider2D spikeWallCollider;
    private bool liftPitHasTriggered;
    private bool liftPitCanRegisterFall;
    private bool liftPitActorInAutoBounce;
    private bool liftPitActorFullyOffscreen;
    private bool liftPitBounceAirborne;
    /// <summary>本局失败确认后锁住升降地块；只终止升降，不改地块位置、碰撞或坑沿显隐。</summary>
    private bool liftPitMovementFrozen;

    #endregion

    #region Unity Messages

    private void Start()
    {
        ResolveReferences();
        CacheLevelCamera();
        BindEvents();
        InitializeGameplay();
    }

    private void Update()
    {
        if (actor != null && actor.IsDied)
            FreezeLiftPitMovement();

        UpdatePendingFrontSpike();

        if (isTeardownRequested || actor == null || rule.Result != Level104Result.None || rule.IsGameplayFrozen)
            return;

        float actorX = actor.transform.position.x;
        UpdatePitPlatformButton();
        UpdateLiftPitGameplay();

        // 箭阵只看玩家首次到达最左轨左侧 5 个世界单位，不读取小狗冲锋、命中或离场状态。
        if (!rule.ArrowVolleyHasFired
            && actorX >= ArrowVolleyTriggerActorX
            && rule.TryStartArrowVolley())
        {
            PlayArrowVolley();
        }

        // bark 先按较远门槛触发；原距离继续作为 run 门槛，箭阵阶段不参与这两个门禁。
        if (dogRoot == null)
        {
            if (rule.TryStartDogCharge())
                OnDogChargeCompleted();
        }
        else
        {
            float barkTriggerActorX = dogRoot.position.x - Mathf.Max(0f, dogBarkTriggerDistance);
            if (!rule.DogHasCharged && actorX >= barkTriggerActorX && rule.TryStartDogCharge())
                PlayDogBark();

            float runTriggerActorX = dogRoot.position.x - Mathf.Max(0f, dogChargeTriggerDistance);
            if (actorX >= runTriggerActorX)
                dogRunTriggerReached = true;

            TryStartDogRunWhenReady();
        }

        // 刺墙启动仍要求 Actor 主碰撞体完整越过坑右缘；升降坑改由逼近间距独立揭开。
        if (!rule.SpikeWallAdvanceHasStarted && spikeWallRoot != null
            && actorX >= spikeWallStartActorX && HasActorClearedLiftPit()
            && rule.TryStartSpikeWallAdvance())
        {
            PlaySpikeWallAdvance();
        }

        UpdateLiftPitRevealFromSpikeWall();

        // 飞门只读取玩家中心绝对世界 X；不依赖刺墙是否已实际反弹。
        if (!rule.DoorFlyHasStarted
            && actorX >= Level104DontLookBackRule.DoorFlyStartActorX
            && flyingDoor != null && flyingDoor.isActiveAndEnabled
            && rule.TryStartDoorFly(actorX))
        {
            flyingDoor.Play();
        }

        // 循环是全程世界规则；脚本传送不经过玩家左移判负入口。
        if (actorX >= wrapRightX && rule.NotifyScriptDrivenWrap())
        {
            WrapActorToLeftEdge();
            return;
        }

        if (rule.Phase == Level104Phase.DoorLanded && levelCtrl != null && levelCtrl.IsActorEnterDoor)
            rule.TryEnterDoor();
    }

    /// <summary>
    /// 刺墙触人信号延迟到 LateUpdate 收口：同一物理帧内箭触发器先立即登记 Failure，
    /// 随后本入口会因终态被拒绝，从而稳定保证箭命中优先。
    /// </summary>
    private void LateUpdate()
    {
        if (isTeardownRequested)
            return;

        UpdateLiftPitOffscreenVelocity();

        if (!hasPendingSpikeWallTouch)
            return;

        float actorX = pendingSpikeWallActorX;
        hasPendingSpikeWallTouch = false;

        if (rule.Result != Level104Result.None || rule.IsGameplayFrozen)
            return;

        PlaySpikeWallBounce(actorX);
    }

    private void OnDisable() => CleanupRuntimeState();

    private void OnDestroy() => CleanupRuntimeState();

    private void CleanupRuntimeState()
    {
        TeardownGameplay();
        EventsUtils.ResetEvents(ref subscriber);
    }

    #endregion

    #region Initialization

    private void ResolveReferences()
    {
        levelCtrl ??= GetComponent<BaseLevelCtrl>();
        actor ??= levelCtrl != null ? levelCtrl.Actor : null;
        if (flyingDoor == null)
            flyingDoor = GetComponentInChildren<Level104FlyingDoorCtrl>(true);

        if (dogRoot != null)
            dogTrapTrigger = dogRoot.GetComponent<Level104TrapTrigger>();

        pitPlatform ??= FindDescendantByName(transform, "Plantform_pit");
        pitPlatformButton ??= FindDescendantByName(transform, "PitPlatformRevealButton");

        liftPitGroundSegment ??= FindDescendantByName(transform, "Ground_lift_pit_top");
        liftPitLeftTopEdge = ResolveLiftPitNode(liftPitLeftTopEdge, "Ground_lift_pit_left", "Group_right", "Sp_ground_1_03");
        liftPitLeftWall = ResolveLiftPitNode(liftPitLeftWall, "Ground_lift_pit_left", "Group_right", "Sp_ground_1_09");
        liftPitLeftBottom = ResolveLiftPitNode(liftPitLeftBottom, "Ground_lift_pit_left", "Group_right", "Sp_ground_1_05");
        liftPitRightTopEdge = ResolveLiftPitNode(liftPitRightTopEdge, "Ground_lift_pit_right", "Group_left", "Sp_ground_1_02");
        liftPitRightWall = ResolveLiftPitNode(liftPitRightWall, "Ground_lift_pit_right", "Group_left", "Sp_ground_1_08");
        liftPitRightBottom = ResolveLiftPitNode(liftPitRightBottom, "Ground_lift_pit_right", "Group_left", "Sp_ground_1_04");

        if (liftPitGroundSegment != null)
            liftPitGroundRenderers = liftPitGroundSegment.GetComponentsInChildren<Renderer>(true);
        liftPitGroundCollider = ResolveLiftPitGroundCollider();
        liftPitActorBodyCollider = ResolveLiftPitActorBodyCollider();
        spikeWallCollider = ResolveSpikeWallTriggerCollider();
    }

    private Transform ResolveLiftPitNode(
        Transform currentReference,
        string groundName,
        string groupName,
        string nodeName)
    {
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

    private static Collider2D ResolveSolidCollider(Transform root)
    {
        if (root == null)
            return null;

        Collider2D rootCollider = root.GetComponent<Collider2D>();
        if (IsUsableLiftPitCollider(rootCollider))
            return rootCollider;

        Collider2D[] candidates = root.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D candidate in candidates)
        {
            if (IsUsableLiftPitCollider(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>缓存关卡相机与其初始位置，供绕屏同帧瞬移复位；查找链与 CameraFollowArea 保持一致。</summary>
    private void CacheLevelCamera()
    {
        levelCamera = Camera.main;
        if (levelCamera == null)
            levelCamera = FindFirstObjectByType<Camera>();
        if (levelCamera == null)
            return;

        levelCameraInitialPosition = levelCamera.transform.position;
        hasLevelCameraInitialPosition = true;
        cameraFollowRange = GetComponent<CameraFollowRange>();
    }

    private void BindEvents()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);
        subscriber.Subscribe<OnTriggerTrapEvent>(OnTriggerTrap);
        subscriber.Subscribe<OnTriggerFailAreaEvent>(OnTriggerFailArea);
        subscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private void InitializeGameplay()
    {
        isTeardownRequested = false;
        KillTweens();
        dogRunTriggerReached = false;
        dogBarkPresentationCompleted = false;
        dogRunStarted = false;
        hasPendingSpikeWallTouch = false;
        frontSpikePendingSince = -1f;
        InitializePitPlatformButton();
        InitializeLiftPit();

        if (spikeStone != null)
            spikeStone.SetActive(false);

        // 小狗开局可见 idle，碰撞门禁保持关闭；bark 与 run 使用两个独立的位置门槛。
        if (dogRoot != null)
        {
            ConfigureDogTrap(false);
            dogRoot.gameObject.SetActive(true);
            SetDogBarkEffectVisible(false);
            dogSpine?.PlayAnimation(DogIdleAnimationName, true);
        }

        InitializeArrowHazards();
        flyingDoor?.Initialize(rule);
    }

    private void InitializeArrowHazards()
    {
        if (arrows == null)
            return;

        foreach (Transform arrow in arrows)
        {
            if (arrow == null)
                continue;

            SetArrowTrapArmed(arrow, false);
            SetArrowCollidersEnabled(arrow, true);
            StopAndClearArrowTrail(arrow);
            ResetArrowAlpha(arrow);
            arrow.gameObject.SetActive(false);
        }
    }

    private void KillTweens()
    {
        KillDogChargeTween();
        KillDogHitPresentationTweens();
        KillSpikeWallTween();
        KillPitPlatformButtonRevealTween();
        KillLiftPitTween();
        KillArrowTweens();
    }

    #endregion

    #region Player Input

    /// <summary>玩家左移输入是唯一回头违规登记入口；脚本绕屏永不调用本方法。判负以主角面部真实撞击执法刺 trigger（SpikeStoneTrapHandle）为准。</summary>
    private void OnPlayerLeftMove(PlayerLeftMoveEvent evt)
    {
        if (isTeardownRequested || !rule.NotifyPlayerLeftMoveInput())
            return;

        StopGameplayTweensForTerminalOutcome();
        ShowFrontSpike();
    }

    /// <summary>
    /// 执法刺瞬现并固定于违规时刻主角左侧 spikeFrontOffset 处（不再跟随），登记待碰撞结算；
    /// 判负由 SpikeStoneTrapHandle 在主角主体真实接触刺石 trigger 时收口（播放 failed 音效 + OnTriggerTrapEvent 死亡链）。
    /// </summary>
    private void ShowFrontSpike()
    {
        if (spikeStone == null || actor == null)
        {
            this.LogWarning("[Level104Ctrl] 执法刺或主角未绑定，跳过刺石表现，直接走死亡链。");
            AudioManager.PlaySound("failed");
            this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
            return;
        }

        spikeStone.transform.position = actor.transform.position + Vector3.left * spikeFrontOffset;
        spikeStone.SetActive(true);
        frontSpikePendingSince = Time.time;
    }

    /// <summary>
    /// 执法刺待碰撞期间固定悬停不再跟随，由主角继续左移真实撞刺收口；
    /// 超过 1 秒未接触说明物理通路异常，按同一死亡链兜底，防软锁。
    /// </summary>
    private void UpdatePendingFrontSpike()
    {
        if (spikeStone == null || !spikeStone.activeSelf || frontSpikePendingSince < 0f)
            return;

        if (actor == null || actor.IsDied)
        {
            frontSpikePendingSince = -1f;
            return;
        }

        if (Time.time - frontSpikePendingSince > 1f)
        {
            frontSpikePendingSince = -1f;
            this.LogWarning("[Level104Ctrl] 执法刺超过 1 秒未与主角接触，按死亡链兜底收口。");
            AudioManager.PlaySound("failed");
            this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
            return;
        }

        // 执法刺固定于违规点左侧，不再每帧贴合主角面前。
    }

    #endregion

    #region Dog Charge And Knockback

    private void PlayDogBark()
    {
        if (dogRoot == null)
        {
            this.LogWarning("[Level104Ctrl] 诱饵小狗未绑定，跳过冲锋演出并只登记离场。");
            OnDogChargeCompleted();
            return;
        }

        KillDogChargeTween();
        ConfigureDogTrap(false);
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
        if (isTeardownRequested || dogRoot == null
            || rule.Result != Level104Result.None || rule.IsGameplayFrozen)
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
            if (isTeardownRequested || dogRoot == null
                || rule.Result != Level104Result.None || rule.IsGameplayFrozen)
            {
                SetDogBarkEffectVisible(false);
                return;
            }

            CompleteDogBarkPresentation();
        }).SetLink(gameObject);
    }

    private void CompleteDogBarkPresentation()
    {
        if (isTeardownRequested || dogRoot == null
            || rule.Result != Level104Result.None || rule.IsGameplayFrozen)
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
            || isTeardownRequested || dogRoot == null
            || rule.Result != Level104Result.None || rule.IsGameplayFrozen)
            return;

        dogRunStarted = true;
        StartDogRun();
    }

    private void StartDogRun()
    {
        if (dogRoot == null)
            return;

        SetDogBarkEffectVisible(false);
        ConfigureDogTrap(true);
        dogSpine?.PlayAnimation(DogRunAnimationName, true, false);

        float dropX = ResolveDogPitDropX();
        float startX = dogRoot.position.x;
        if (startX <= dropX)
        {
            StartDogPitFall();
            return;
        }

        float duration = Mathf.Max(0.01f, (startX - dropX) / Mathf.Max(0.01f, dogRunSpeed));
        dogChargeTween = dogRoot.DOMoveX(dropX, duration)
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(StartDogPitFall);
    }

    /// <summary>
    /// 狗跑到固定致死坑右缘后不再锁 Y 平穿：切 jump_down，保持原水平速度落入坑中，
    /// 落到相机下后隐藏并登记离场。命中玩家后这条下落仍继续，不改撞飞链。
    /// </summary>
    private void StartDogPitFall()
    {
        if (isTeardownRequested || dogRoot == null)
            return;

        dogSpine?.PlayAnimation(DogJumpDownAnimationName, false, false);

        float fallDuration = Mathf.Max(0.01f, dogPitFallDuration);
        float startX = dogRoot.position.x;
        float targetX = startX - dogRunSpeed * fallDuration;
        float targetY = dogPitFallY;

        Sequence fallSequence = DOTween.Sequence().SetLink(gameObject);
        fallSequence.Join(dogRoot.DOMoveX(targetX, fallDuration).SetEase(Ease.Linear));
        fallSequence.Join(dogRoot.DOMoveY(targetY, fallDuration).SetEase(Ease.InQuad));
        fallSequence.OnComplete(() =>
        {
            dogChargeTween = null;
            if (dogRoot == null)
                return;

            dogRoot.gameObject.SetActive(false);
            OnDogChargeCompleted();
        });
        dogChargeTween = fallSequence;
    }

    private float ResolveDogPitDropX()
    {
        Transform rightBank = FindDescendantByName(transform, "Ground_pit_right");
        Collider2D rightBankCollider = ResolveSolidCollider(rightBank);
        if (rightBankCollider != null)
            return rightBankCollider.bounds.min.x;

        return dogPitDropX;
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

    /// <summary>本关 Tween 已把狗落入固定致死坑并隐藏；只登记小狗离场，不参与箭阵启动。</summary>
    private void OnDogChargeCompleted()
    {
        ConfigureDogTrap(false);
        if (!isTeardownRequested)
            rule.NotifyDogChargeCompleted();
    }

    /// <summary>
    /// 由狗类型 <see cref="Level104TrapTrigger"/> 调用。Domain 先进入 DogKnockback 冻结态，
    /// 再立即锁操作并停止 Actor；狗自身 Tween 不停止，仍继续跑到坑口并落入坑中后隐藏。
    /// </summary>
    public bool NotifyDogHit(ActorCtrl targetActor, Vector3 hitPosition)
    {
        if (isTeardownRequested || targetActor == null || !rule.NotifyDogHit())
            return false;

        if (actor == null)
            actor = targetActor;

        ConfigureDogTrap(false);
        hasPendingSpikeWallTouch = false;
        levelCtrl?.SetCtrlable(false);
        targetActor.StopMoving();
        targetActor.SetMoveInput(0f);

        // 冻结除狗冲锋外的一切玩法表现；狗必须继续跑完，所以不能杀死 dogChargeTween。
        StopNonDogGameplayTweens();
        ScheduleDogActorKnockback(targetActor, hitPosition);
        return true;
    }

    private void ScheduleDogActorKnockback(ActorCtrl targetActor, Vector3 hitPosition)
    {
        KillDogHitPresentationTweens();
        float delay = Mathf.Max(0f, dogHitDelay);
        if (delay <= 0f)
        {
            ApplyDogActorKnockback(targetActor, hitPosition);
            return;
        }

        dogHitDelayTween = DOVirtual.DelayedCall(delay, () =>
            {
                dogHitDelayTween = null;
                ApplyDogActorKnockback(targetActor, hitPosition);
            })
            .SetLink(gameObject);
    }

    private void ApplyDogActorKnockback(ActorCtrl targetActor, Vector3 hitPosition)
    {
        if (isTeardownRequested || targetActor == null
            || rule.Phase != Level104Phase.DogKnockback || rule.Result != Level104Result.None)
            return;

        if (dogHitEffect != null)
        {
            dogHitEffect.transform.position = hitPosition;
            dogHitEffect.Play();
        }

        if (!string.IsNullOrEmpty(dogHitSfxTag))
            AudioManager.PlaySound(dogHitSfxTag);

        targetActor.Knockback(dogKnockbackDirection, dogKnockbackForce).Forget();
        dogActorRotationTween = targetActor.transform
            .DOLocalRotate(
                new Vector3(0f, 0f, 360f),
                Mathf.Max(0.01f, dogRotationDuration),
                RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1)
            .SetLink(targetActor.gameObject);
    }

    private void KillDogHitPresentationTweens()
    {
        if (dogHitDelayTween != null && dogHitDelayTween.IsActive())
            dogHitDelayTween.Kill();
        dogHitDelayTween = null;

        if (dogActorRotationTween != null && dogActorRotationTween.IsActive())
            dogActorRotationTween.Kill();
        dogActorRotationTween = null;
    }

    private void ConfigureDogTrap(bool armed)
    {
        if (dogRoot == null)
            return;

        dogTrapTrigger ??= dogRoot.GetComponent<Level104TrapTrigger>();
        if (dogTrapTrigger == null)
            return;

        dogTrapTrigger.Configure(this, Level104TrapKind.Dog);
        dogTrapTrigger.SetArmed(armed);
    }

    #endregion

    #region Pit Platform Button

    private void InitializePitPlatformButton()
    {
        KillPitPlatformButtonRevealTween();
        pitPlatformButtonHasRevealed = false;

        if (pitPlatformButton == null)
            return;

        pitPlatformButtonVisiblePosition = pitPlatformButton.position;
        pitPlatformButtonVisibleScale = pitPlatformButton.localScale;
        pitPlatformButtonRenderers = pitPlatformButton.GetComponentsInChildren<Renderer>(true);
        pitPlatformButtonColliders = pitPlatformButton.GetComponentsInChildren<Collider2D>(true);
        SetPitPlatformButtonRenderersEnabled(false);
        SetPitPlatformButtonCollidersEnabled(false);
    }

    private void UpdatePitPlatformButton()
    {
        if (pitPlatformButtonHasRevealed || actor == null)
            return;

        if (actor.transform.position.x < pitPlatformButtonRevealActorX)
            return;

        RevealPitPlatformButton();
    }

    private void RevealPitPlatformButton()
    {
        pitPlatformButtonHasRevealed = true;
        if (pitPlatformButton == null)
            return;

        KillPitPlatformButtonRevealTween();
        SetPitPlatformButtonCollidersEnabled(false);

        Vector3 startPosition = pitPlatformButtonVisiblePosition;
        startPosition.y -= Mathf.Max(0f, pitPlatformButtonRiseDistance);
        pitPlatformButton.position = startPosition;

        Vector3 startScale = pitPlatformButtonVisibleScale;
        startScale.y *= 0.05f;
        pitPlatformButton.localScale = startScale;
        SetPitPlatformButtonRenderersEnabled(true);

        float duration = Mathf.Max(0f, pitPlatformButtonRevealDuration);
        if (duration <= 0f)
        {
            CompletePitPlatformButtonReveal();
            return;
        }

        Sequence revealSequence = DOTween.Sequence().SetLink(gameObject);
        revealSequence
            .Append(pitPlatformButton.DOMoveY(pitPlatformButtonVisiblePosition.y, duration).SetEase(Ease.OutBack))
            .Join(pitPlatformButton.DOScale(pitPlatformButtonVisibleScale, duration).SetEase(Ease.OutBack))
            .OnComplete(CompletePitPlatformButtonReveal);
        pitPlatformButtonRevealTween = revealSequence;
    }

    private void CompletePitPlatformButtonReveal()
    {
        pitPlatformButtonRevealTween = null;
        if (pitPlatformButton == null)
            return;

        pitPlatformButton.position = pitPlatformButtonVisiblePosition;
        pitPlatformButton.localScale = pitPlatformButtonVisibleScale;
        SetPitPlatformButtonRenderersEnabled(true);
        SetPitPlatformButtonCollidersEnabled(true);
    }

    private void SetPitPlatformButtonRenderersEnabled(bool isEnabled)
    {
        if (pitPlatformButtonRenderers == null && pitPlatformButton != null)
            pitPlatformButtonRenderers = pitPlatformButton.GetComponentsInChildren<Renderer>(true);
        if (pitPlatformButtonRenderers == null)
            return;

        foreach (Renderer targetRenderer in pitPlatformButtonRenderers)
        {
            if (targetRenderer != null)
                targetRenderer.enabled = isEnabled;
        }
    }

    private void SetPitPlatformButtonCollidersEnabled(bool isEnabled)
    {
        if (pitPlatformButtonColliders == null && pitPlatformButton != null)
            pitPlatformButtonColliders = pitPlatformButton.GetComponentsInChildren<Collider2D>(true);
        if (pitPlatformButtonColliders == null)
            return;

        foreach (Collider2D targetCollider in pitPlatformButtonColliders)
        {
            if (targetCollider != null)
                targetCollider.enabled = isEnabled;
        }
    }

    private void KillPitPlatformButtonRevealTween()
    {
        if (pitPlatformButtonRevealTween != null && pitPlatformButtonRevealTween.IsActive())
            pitPlatformButtonRevealTween.Kill();
        pitPlatformButtonRevealTween = null;
    }

    #endregion

    #region Lift Pit

    private void InitializeLiftPit()
    {
        KillLiftPitTween();
        liftPitHasTriggered = false;
        liftPitCanRegisterFall = false;
        liftPitActorInAutoBounce = false;
        liftPitActorFullyOffscreen = false;
        liftPitBounceAirborne = false;
        liftPitMovementFrozen = false;

        if (liftPitGroundSegment != null)
        {
            liftPitOriginalY = liftPitGroundSegment.position.y;
            liftPitGroundRenderers = liftPitGroundSegment.GetComponentsInChildren<Renderer>(true);
        }

        liftPitGroundCollider = ResolveLiftPitGroundCollider();
        liftPitActorBodyCollider = ResolveLiftPitActorBodyCollider();
        spikeWallCollider = ResolveSpikeWallTriggerCollider();
        SetLiftPitEdgesVisible(false);
    }

    /// <summary>
    /// 刺墙已启动逼近且尚未反弹时，按刺墙有效 Trigger 左缘与 Actor 主体右缘实时水平间距揭坑；
    /// Collider 无法解析时不提前触发，由实际接触回调兜底。
    /// </summary>
    private void UpdateLiftPitRevealFromSpikeWall()
    {
        if (liftPitHasTriggered || liftPitMovementFrozen
            || !rule.SpikeWallAdvanceHasStarted || rule.SpikeWallHasBounced
            || rule.Result != Level104Result.None || rule.IsGameplayFrozen)
            return;

        if (!TryGetSpikeWallToActorHorizontalGap(out float gap))
            return;

        if (gap <= Mathf.Max(0f, liftPitRevealWallGap))
            PlayLiftPitCollapse();
    }

    private bool TryGetSpikeWallToActorHorizontalGap(out float gap)
    {
        gap = float.PositiveInfinity;

        if (!IsUsableSpikeWallTriggerCollider(spikeWallCollider))
            spikeWallCollider = ResolveSpikeWallTriggerCollider();
        if (!IsUsableSpikeWallTriggerCollider(spikeWallCollider))
            return false;

        if (!IsUsableLiftPitCollider(liftPitActorBodyCollider))
            liftPitActorBodyCollider = ResolveLiftPitActorBodyCollider();
        if (!IsUsableLiftPitCollider(liftPitActorBodyCollider))
            return false;

        gap = spikeWallCollider.bounds.min.x - liftPitActorBodyCollider.bounds.max.x;
        return true;
    }

    private Collider2D ResolveSpikeWallTriggerCollider()
    {
        if (spikeWallRoot == null)
            return null;

        Level104SpikeWallTrigger[] triggers =
            spikeWallRoot.GetComponentsInChildren<Level104SpikeWallTrigger>(true);
        foreach (Level104SpikeWallTrigger trigger in triggers)
        {
            if (trigger == null)
                continue;

            Collider2D[] candidates = trigger.GetComponents<Collider2D>();
            foreach (Collider2D candidate in candidates)
            {
                if (IsUsableSpikeWallTriggerCollider(candidate))
                    return candidate;
            }
        }

        // 兼容 Trigger 与脚本分挂：在 spikeWallRoot 子树中取第一个可用 Trigger Collider。
        Collider2D[] rootCandidates = spikeWallRoot.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D candidate in rootCandidates)
        {
            if (IsUsableSpikeWallTriggerCollider(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsUsableSpikeWallTriggerCollider(Collider2D candidate)
    {
        return candidate != null
            && candidate.enabled
            && candidate.isTrigger
            && candidate.gameObject.activeInHierarchy;
    }

    private void UpdateLiftPitGameplay()
    {
        if (liftPitCanRegisterFall && !liftPitActorInAutoBounce &&
            IsActorInsideLiftPit() && !IsActorRidingLiftPitGround())
        {
            liftPitActorInAutoBounce = true;
            liftPitActorFullyOffscreen = false;
            liftPitBounceAirborne = !actor.IsGround;
        }

        if (liftPitActorInAutoBounce)
            UpdateLiftPitAutoBounce();
    }

    private void PlayLiftPitCollapse()
    {
        if (liftPitHasTriggered || liftPitMovementFrozen)
            return;

        liftPitHasTriggered = true;
        KillLiftPitTween();
        SetLiftPitEdgesVisible(true);

        if (!string.IsNullOrEmpty(liftPitCollapseSfxTag))
            AudioManager.PlaySound(liftPitCollapseSfxTag);

        if (liftPitGroundSegment == null)
        {
            this.LogWarning("[Level104Ctrl] 升降陷坑地块未绑定，跳过半落演出。");
            OnLiftPitHalfDropCompleted();
            return;
        }

        liftPitOriginalY = liftPitGroundSegment.position.y;
        float dropDistance = CalculateLiftPitDropDistance() * 0.5f;
        liftPitTween = liftPitGroundSegment
            .DOMoveY(liftPitOriginalY - dropDistance, EffectiveLiftPitDropDuration)
            .SetEase(Ease.InQuad)
            .SetLink(gameObject)
            .OnComplete(OnLiftPitHalfDropCompleted);
    }

    private void OnLiftPitHalfDropCompleted()
    {
        liftPitTween = null;
        if (liftPitMovementFrozen)
            return;

        liftPitCanRegisterFall = true;
        liftPitTween = DOVirtual
            .DelayedCall(Mathf.Max(0f, liftPitRiseDelay), StartLiftPitRise)
            .SetLink(gameObject);
    }

    private void StartLiftPitRise()
    {
        liftPitTween = null;
        if (liftPitMovementFrozen)
            return;

        if (liftPitGroundSegment == null)
        {
            CompleteLiftPitRestore();
            return;
        }

        if (!string.IsNullOrEmpty(liftPitCollapseSfxTag))
            AudioManager.PlaySound(liftPitCollapseSfxTag);

        liftPitTween = liftPitGroundSegment
            .DOMoveY(liftPitOriginalY, EffectiveLiftPitDropDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject)
            .OnComplete(CompleteLiftPitRestore);
    }

    private void CompleteLiftPitRestore()
    {
        liftPitTween = null;
        if (liftPitMovementFrozen)
            return;

        SetLiftPitEdgesVisible(false);
    }

    private float CalculateLiftPitDropDistance()
    {
        float dropDistance = Mathf.Max(4.8f, liftPitDropDistance);
        if (TryGetLiftPitGroundExitDistance(out float requiredDistance))
            dropDistance = Mathf.Max(dropDistance, requiredDistance);
        return dropDistance;
    }

    private bool TryGetLiftPitGroundExitDistance(out float distance)
    {
        distance = 0f;
        if (!TryGetLiftPitCameraBottomY(
                liftPitGroundSegment != null ? liftPitGroundSegment.position.z : transform.position.z,
                out float cameraBottomY) ||
            !TryGetLiftPitGroundRendererMaxY(out float rendererMaxY))
            return false;

        const float hideMargin = 0.05f;
        distance = Mathf.Max(0f, rendererMaxY - cameraBottomY + hideMargin);
        return true;
    }

    private bool TryGetLiftPitGroundRendererMaxY(out float maxY)
    {
        maxY = float.NegativeInfinity;
        bool found = false;
        if (liftPitGroundRenderers == null && liftPitGroundSegment != null)
            liftPitGroundRenderers = liftPitGroundSegment.GetComponentsInChildren<Renderer>(true);
        if (liftPitGroundRenderers == null)
            return false;

        foreach (Renderer targetRenderer in liftPitGroundRenderers)
        {
            if (targetRenderer == null || !targetRenderer.enabled || !targetRenderer.gameObject.activeInHierarchy)
                continue;

            maxY = Mathf.Max(maxY, targetRenderer.bounds.max.y);
            found = true;
        }

        return found;
    }

    private void SetLiftPitEdgesVisible(bool isVisible)
    {
        SetLiftPitNodeVisible(liftPitLeftTopEdge, isVisible);
        SetLiftPitNodeVisible(liftPitLeftWall, isVisible);
        SetLiftPitNodeVisible(liftPitLeftBottom, isVisible);
        SetLiftPitNodeVisible(liftPitRightTopEdge, isVisible);
        SetLiftPitNodeVisible(liftPitRightWall, isVisible);
        SetLiftPitNodeVisible(liftPitRightBottom, isVisible);
    }

    private static void SetLiftPitNodeVisible(Transform target, bool isVisible)
    {
        if (target != null)
            target.gameObject.SetActive(isVisible);
    }

    private Collider2D ResolveLiftPitGroundCollider()
    {
        if (liftPitGroundSegment == null)
            return null;

        Collider2D rootCollider = liftPitGroundSegment.GetComponent<Collider2D>();
        if (IsUsableLiftPitCollider(rootCollider))
            return rootCollider;

        Collider2D[] candidates = liftPitGroundSegment.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D candidate in candidates)
        {
            if (IsUsableLiftPitCollider(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsUsableLiftPitCollider(Collider2D candidate)
    {
        return candidate != null && candidate.enabled && !candidate.isTrigger && candidate.gameObject.activeInHierarchy;
    }

    private Collider2D ResolveLiftPitActorBodyCollider()
    {
        if (actor == null)
            return null;

        Collider2D rootCollider = actor.GetComponent<Collider2D>();
        if (IsUsableLiftPitCollider(rootCollider))
            return rootCollider;

        Collider2D[] candidates = actor.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D candidate in candidates)
        {
            if (IsUsableLiftPitCollider(candidate))
                return candidate;
        }

        return null;
    }

    private void GetLiftPitOpeningEdges(out float leftEdge, out float rightEdge)
    {
        if (!IsUsableLiftPitCollider(liftPitGroundCollider))
            liftPitGroundCollider = ResolveLiftPitGroundCollider();

        if (IsUsableLiftPitCollider(liftPitGroundCollider))
        {
            Bounds bounds = liftPitGroundCollider.bounds;
            if (bounds.size.x > Mathf.Epsilon)
            {
                leftEdge = bounds.min.x;
                rightEdge = bounds.max.x;
                return;
            }
        }

        leftEdge = liftPitCenterX - liftPitHalfWidth;
        rightEdge = liftPitCenterX + liftPitHalfWidth;
    }

    private bool HasActorClearedLiftPit()
    {
        if (actor == null)
            return false;

        GetLiftPitOpeningEdges(out _, out float rightEdge);
        if (!IsUsableLiftPitCollider(liftPitActorBodyCollider))
            liftPitActorBodyCollider = ResolveLiftPitActorBodyCollider();
        if (!IsUsableLiftPitCollider(liftPitActorBodyCollider))
            return false;

        return liftPitActorBodyCollider.bounds.min.x
            >= rightEdge + Mathf.Max(0f, liftPitActorClearance);
    }

    private bool IsActorInsideLiftPit()
    {
        Vector3 position = actor.transform.position;
        GetLiftPitOpeningEdges(out float leftEdge, out float rightEdge);
        return position.x >= leftEdge && position.x <= rightEdge && position.y <= liftPitEnterDetectY;
    }

    private bool IsActorRidingLiftPitGround()
    {
        if (liftPitGroundSegment == null || actor == null || !actor.IsGround)
            return false;

        Vector3 position = actor.transform.position;
        GetLiftPitOpeningEdges(out float leftEdge, out float rightEdge);
        if (position.x < leftEdge || position.x > rightEdge)
            return false;

        float ridingY = liftPitGroundSegment.position.y + 0.9045f;
        return Mathf.Abs(position.y - ridingY) < 0.5f;
    }

    private void UpdateLiftPitAutoBounce()
    {
        UpdateLiftPitActorScreenState();

        Vector3 position = actor.transform.position;
        GetLiftPitOpeningEdges(out float leftEdge, out float rightEdge);
        bool isHorizontallyInsidePit = position.x >= leftEdge && position.x <= rightEdge;

        if (!actor.IsGround)
        {
            liftPitBounceAirborne = true;
            return;
        }

        if (!liftPitActorFullyOffscreen &&
            (position.x < leftEdge - liftPitExitMargin ||
             position.x > rightEdge + liftPitExitMargin ||
             position.y > liftPitRimY))
        {
            liftPitActorInAutoBounce = false;
            liftPitActorFullyOffscreen = false;
            return;
        }

        if (!liftPitBounceAirborne || !isHorizontallyInsidePit || position.y > liftPitBounceFloorY)
            return;

        ApplyLiftPitBounce();
    }

    private void ApplyLiftPitBounce()
    {
        Rigidbody2D body = actor != null ? actor.RB : null;
        if (body == null)
            return;

        liftPitBounceAirborne = false;
        AudioManager.PlaySound("jump");
        body.linearVelocity = new Vector2(
            liftPitActorFullyOffscreen ? 0f : body.linearVelocityX,
            EffectiveLiftPitBounceVelocity);
    }

    private void UpdateLiftPitActorScreenState()
    {
        liftPitActorBodyCollider = ResolveLiftPitActorBodyCollider();
        if (liftPitActorBodyCollider == null ||
            !TryGetLiftPitCameraBottomY(liftPitActorBodyCollider.bounds.center.z, out float cameraBottomY))
            return;

        bool isFullyOffscreen = liftPitActorBodyCollider.bounds.max.y < cameraBottomY;
        if (!liftPitActorFullyOffscreen)
        {
            if (isFullyOffscreen)
                liftPitActorFullyOffscreen = true;
            return;
        }

        if (!isFullyOffscreen)
            liftPitActorFullyOffscreen = false;
    }

    private void UpdateLiftPitOffscreenVelocity()
    {
        if (!liftPitActorInAutoBounce || !liftPitActorFullyOffscreen || actor == null ||
            rule.Result != Level104Result.None || rule.IsGameplayFrozen)
            return;

        Rigidbody2D body = actor.RB;
        if (body == null)
            return;

        body.linearVelocity = new Vector2(0f, body.linearVelocityY);
        if (actor.IsGround && liftPitBounceAirborne && body.linearVelocityY < EffectiveLiftPitBounceVelocity)
            ApplyLiftPitBounce();
    }

    private bool TryGetLiftPitCameraBottomY(float worldZ, out float cameraBottomY)
    {
        if (levelCamera == null)
            levelCamera = Camera.main;
        if (levelCamera == null)
            levelCamera = FindFirstObjectByType<Camera>();

        if (levelCamera == null)
        {
            cameraBottomY = 0f;
            return false;
        }

        float cameraDistance = Mathf.Abs(levelCamera.transform.position.z - worldZ);
        cameraBottomY = levelCamera.ViewportToWorldPoint(new Vector3(0.5f, 0f, cameraDistance)).y;
        return true;
    }

    private void KillLiftPitTween()
    {
        if (liftPitTween != null && liftPitTween.IsActive())
            liftPitTween.Kill();
        liftPitTween = null;
    }

    private float EffectiveLiftPitDropDuration =>
        liftPitDropDuration >= 0.2f && liftPitDropDuration <= 0.4f ? liftPitDropDuration : 0.25f;

    private float EffectiveLiftPitBounceVelocity => Mathf.Max(0f, liftPitBounceVelocity);

    #endregion

    #region Arrow Volley

    private void PlayArrowVolley()
    {
        int arrowCount = arrows != null ? Mathf.Min(arrows.Length, ArrowWorldXs.Length) : 0;
        int lastValidIndex = -1;
        for (int index = arrowCount - 1; index >= 0; index--)
        {
            if (arrows[index] != null)
            {
                lastValidIndex = index;
                break;
            }
        }

        int firstValidIndex = -1;
        for (int index = 0; index < arrowCount; index++)
        {
            if (arrows[index] != null)
            {
                firstValidIndex = index;
                break;
            }
        }

        if (lastValidIndex < 0 || firstValidIndex < 0)
        {
            this.LogWarning("[Level104Ctrl] 追逐箭阵未绑定，跳过落箭演出。");
            rule.NotifyArrowVolleyCompleted();
            return;
        }

        if (arrows.Length != ArrowWorldXs.Length)
            this.LogWarning($"[Level104Ctrl] 箭阵应绑定 10 支箭，当前为 {arrows.Length}；仅处理固定轨道范围内的有效项。");

        KillArrowTweens();
        float fallDuration = Mathf.Max(0.01f, arrowFallDuration);
        float staggerDelay = Mathf.Min(Mathf.Max(0f, arrowStaggerDelay), fallDuration * 0.99f);

        for (int index = 0; index <= lastValidIndex; index++)
        {
            Transform arrow = arrows[index];
            if (arrow == null)
                continue;

            // 2026-08-09 右先落反转后，延迟最大的是最左（首个有效）箭，它最后落地，由它登记箭阵完成。
            bool isLastArrow = index == firstValidIndex;
            Vector3 position = arrow.position;
            arrow.position = new Vector3(ArrowWorldXs[index], arrowSpawnY, position.z);
            ResetArrowAlpha(arrow);
            SetArrowCollidersEnabled(arrow, true);
            SetArrowTrapArmed(arrow, false);
            arrow.gameObject.SetActive(true);

            // 2026-08-09：从右往左落——数组从左到右排列，故延迟按 (末位 - index) 反排，最右箭零延迟先落。
            Tween fallTween = arrow.DOMoveY(arrowGroundY, fallDuration)
                .SetEase(Ease.InQuad)
                .SetDelay((lastValidIndex - index) * staggerDelay)
                .SetLink(gameObject)
                .OnStart(() =>
                {
                    StartArrowTrail(arrow);
                    SetArrowTrapArmed(arrow, true);
                    PlayArrowWhooshSound();
                })
                .OnComplete(() => CompleteArrowFall(arrow, isLastArrow));

            arrowTweens.Add(fallTween);
        }
    }

    /// <summary>接地同帧先关闭危险，再登记最后一支完成；0.3 秒渐隐只属于视觉收口。</summary>
    private void CompleteArrowFall(Transform arrow, bool isLastArrow)
    {
        StopArrowTrail(arrow);
        SetArrowTrapArmed(arrow, false);
        SetArrowCollidersEnabled(arrow, false);

        if (isLastArrow)
            rule.NotifyArrowVolleyCompleted();

        FadeAndHideArrow(arrow);
    }

    private void FadeAndHideArrow(Transform arrow)
    {
        if (arrow == null)
            return;

        SpriteRenderer[] renderers = arrow.GetComponentsInChildren<SpriteRenderer>(true);
        float fadeDuration = Mathf.Max(0f, arrowFadeDuration);
        if (renderers.Length == 0 || fadeDuration <= 0f)
        {
            arrow.gameObject.SetActive(false);
            return;
        }

        Sequence fadeSequence = DOTween.Sequence().SetLink(gameObject);
        bool hasTween = false;
        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer == null)
                continue;

            Tween fadeTween = spriteRenderer.DOFade(0f, fadeDuration).SetEase(Ease.Linear);
            if (!hasTween)
            {
                fadeSequence.Append(fadeTween);
                hasTween = true;
            }
            else
            {
                fadeSequence.Join(fadeTween);
            }
        }

        if (!hasTween)
        {
            fadeSequence.Kill();
            arrow.gameObject.SetActive(false);
            return;
        }

        fadeSequence.OnComplete(() =>
        {
            if (arrow != null)
                arrow.gameObject.SetActive(false);
        });
        arrowTweens.Add(fadeSequence);
    }

    /// <summary>
    /// 由箭类型 <see cref="Level104TrapTrigger"/> 调用：先登记 Domain Failure，再停止刺墙/飞门等玩法 Tween
    /// 并解除全部箭的武装与碰撞；剩余箭的下落与落地渐隐 Tween 保留。
    /// 在首次接触点播放一次 Knock_Explosion，并同帧播放 trap 撞击音与 failed 死亡音，
    /// 最后沿既有 OnTriggerTrapEvent 死亡链收口（WaitPlayDie 自身不播音效，不会重复）。
    /// </summary>
    public bool NotifyArrowHit(Vector3 hitPosition)
    {
        if (isTeardownRequested || !rule.NotifyArrowHit())
            return false;

        hasPendingSpikeWallTouch = false;
        StopGameplayTweensForTerminalOutcome();

        if (arrowHitEffect != null)
        {
            arrowHitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            arrowHitEffect.transform.position = hitPosition;
            arrowHitEffect.Play(true);
        }

        if (!string.IsNullOrEmpty(arrowHitSfxTag))
            AudioManager.PlaySound(arrowHitSfxTag);
        AudioManager.PlaySound("failed");
        this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
        return true;
    }

    private void SetArrowTrapArmed(Transform arrow, bool armed)
    {
        if (arrow == null)
            return;

        Level104TrapTrigger[] triggers = arrow.GetComponentsInChildren<Level104TrapTrigger>(true);
        foreach (Level104TrapTrigger trigger in triggers)
        {
            if (trigger == null)
                continue;

            trigger.Configure(this, Level104TrapKind.Arrow);
            trigger.SetArmed(armed);
        }
    }

    private static void SetArrowCollidersEnabled(Transform arrow, bool isEnabled)
    {
        if (arrow == null)
            return;

        Collider2D[] colliders = arrow.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D arrowCollider in colliders)
        {
            if (arrowCollider != null)
                arrowCollider.enabled = isEnabled;
        }
    }

    private static void ResetArrowAlpha(Transform arrow)
    {
        if (arrow == null)
            return;

        SpriteRenderer[] renderers = arrow.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer == null)
                continue;

            Color color = spriteRenderer.color;
            color.a = 1f;
            spriteRenderer.color = color;
        }
    }

    private void StartArrowTrail(Transform arrow)
    {
        if (arrow == null || arrowTrailMaterial == null || arrowTrailTime <= 0f || arrowTrailWidth <= 0f)
            return;

        if (!arrowTrails.TryGetValue(arrow, out TrailRenderer trail) || trail == null)
        {
            SpriteRenderer arrowRenderer = arrow.GetComponentInChildren<SpriteRenderer>(true);
            var trailObject = new GameObject("Level104_ArrowRainTrail");
            trailObject.transform.SetParent(arrow, false);
            float arrowHalfLength = arrowRenderer?.sprite != null
                ? arrowRenderer.sprite.bounds.extents.x
                : 0f;
            trailObject.transform.localPosition = Vector3.left * arrowHalfLength;

            trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = arrowTrailMaterial;
            trail.time = arrowTrailTime;
            trail.widthMultiplier = arrowTrailWidth;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.colorGradient = CreateArrowTrailGradient();
            trail.minVertexDistance = 0.04f;
            trail.numCapVertices = 2;
            trail.numCornerVertices = 2;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.autodestruct = false;
            trail.sortingLayerID = arrowRenderer != null ? arrowRenderer.sortingLayerID : 0;
            trail.sortingOrder = arrowRenderer != null ? arrowRenderer.sortingOrder - 1 : 0;
            arrowTrails[arrow] = trail;
        }

        trail.Clear();
        trail.emitting = true;
    }

    private static Gradient CreateArrowTrailGradient()
    {
        var gradient = new Gradient();
        var warmWhite = new Color(1f, 0.94f, 0.82f);
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(warmWhite, 0f),
                new GradientColorKey(warmWhite, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.65f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private void StopArrowTrail(Transform arrow)
    {
        if (arrow != null && arrowTrails.TryGetValue(arrow, out TrailRenderer trail) && trail != null)
            trail.emitting = false;
    }

    private void StopAndClearArrowTrail(Transform arrow)
    {
        if (arrow == null || !arrowTrails.TryGetValue(arrow, out TrailRenderer trail) || trail == null)
            return;

        trail.emitting = false;
        trail.Clear();
    }

    private void StopAndClearAllArrowTrails()
    {
        foreach (TrailRenderer trail in arrowTrails.Values)
        {
            if (trail == null)
                continue;

            trail.emitting = false;
            trail.Clear();
        }
    }

    private void DisarmAllArrowTraps()
    {
        if (arrows == null)
            return;

        foreach (Transform arrow in arrows)
            SetArrowTrapArmed(arrow, false);
    }

    /// <summary>
    /// 终态收口只解除箭的危险性：关闭全部箭的武装与碰撞体，但保留下落 Tween，
    /// 使已在空中与待错峰启动的箭继续落地并照常渐隐；迟到的命中与完成回调由 Domain 终态门禁拒绝。
    /// </summary>
    private void DisarmArrowHazardsForTerminalOutcome()
    {
        if (arrows == null)
            return;

        foreach (Transform arrow in arrows)
        {
            SetArrowTrapArmed(arrow, false);
            SetArrowCollidersEnabled(arrow, false);
        }
    }

    private void KillArrowTweens()
    {
        foreach (Tween tween in arrowTweens)
        {
            if (tween != null && tween.IsActive())
                tween.Kill();
        }

        arrowTweens.Clear();
        StopAndClearAllArrowTrails();
        StopArrowWhooshSounds();
    }

    /// <summary>
    /// 播放箭矢破空音。共享 AudioManager 无法跳过 arrowWhoosh.ogg 的前导静音，
    /// 因此按 078 同款由本关 AudioSource 从发声点起播、音量 1.0，并受 Sfx 开关控制。
    /// 十支箭 0.05s 错峰，使用小池避免单源互相掐断。
    /// </summary>
    private void PlayArrowWhooshSound()
    {
        if (!DB.GameSetting.SfxOn || arrowWhooshClip == null)
            return;

        AudioSource source = null;
        for (int i = 0; i < arrowWhooshSources.Count; i++)
        {
            AudioSource candidate = arrowWhooshSources[i];
            if (candidate != null && !candidate.isPlaying)
            {
                source = candidate;
                break;
            }
        }

        if (source == null)
        {
            var sourceObject = new GameObject("Level104_ArrowWhooshSource");
            sourceObject.transform.SetParent(transform, false);
            source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            arrowWhooshSources.Add(source);
        }

        source.clip = arrowWhooshClip;
        source.volume = ArrowWhooshSfxVolume;
        source.time = Mathf.Clamp(ArrowWhooshSfxStartSeconds, 0f, arrowWhooshClip.length);
        source.Play();
    }

    private void StopArrowWhooshSounds()
    {
        for (int i = 0; i < arrowWhooshSources.Count; i++)
        {
            AudioSource source = arrowWhooshSources[i];
            if (source != null && source.isPlaying)
                source.Stop();
        }
    }

    #endregion

    #region Spike Wall And Door Gate

    /// <summary>刺墙开始向左逼近；升降坑改由逼近间距或实际接触兜底揭开，不再同帧启动。</summary>
    private void PlaySpikeWallAdvance()
    {
        if (spikeWallRoot == null)
            return;

        // 启动逼近时刷新一次刺墙 Trigger 缓存，供后续间距揭坑使用。
        spikeWallCollider = ResolveSpikeWallTriggerCollider();
        KillSpikeWallTween();
        float targetX = wrapLeftX - 2f;
        float distance = Mathf.Abs(targetX - spikeWallRoot.position.x);
        float duration = Mathf.Max(0.01f, distance / Mathf.Max(0.01f, spikeWallAdvanceSpeed));
        spikeWallTween = spikeWallRoot.DOMoveX(targetX, duration)
            .SetEase(Ease.Linear)
            .SetLink(gameObject);
    }

    /// <summary>
    /// 由 <see cref="Level104SpikeWallTrigger"/> 传入实际碰撞时 Actor 中心 X；这里只排队，
    /// LateUpdate 才尝试登记反弹，以保证同一物理帧箭命中优先。
    /// 接受信号时若坑尚未触发，先以接触作为最晚兜底揭坑，避免低帧率跨过间距门槛后永不揭坑。
    /// </summary>
    public void NotifySpikeWallTouched(float actorX)
    {
        if (isTeardownRequested || hasPendingSpikeWallTouch
            || rule.Result != Level104Result.None || rule.IsGameplayFrozen
            || float.IsNaN(actorX) || float.IsInfinity(actorX))
            return;

        if (!liftPitHasTriggered && !liftPitMovementFrozen)
            PlayLiftPitCollapse();

        hasPendingSpikeWallTouch = true;
        pendingSpikeWallActorX = actorX;
    }

    private void PlaySpikeWallBounce(float actorX)
    {
        if (!rule.NotifySpikeWallBounced(actorX))
            return;

        if (spikeWallRoot == null)
        {
            this.LogWarning("[Level104Ctrl] 竖刺墙未绑定，跳过反弹演出。");
            return;
        }

        KillSpikeWallTween();
        float distance = Mathf.Abs(spikeWallExitX - spikeWallRoot.position.x);
        float duration = Mathf.Max(0.01f, distance / Mathf.Max(0.01f, spikeWallBounceSpeed));
        spikeWallTween = spikeWallRoot.DOMoveX(spikeWallExitX, duration)
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                if (spikeWallRoot != null)
                    spikeWallRoot.gameObject.SetActive(false);
            });
    }

    private void KillSpikeWallTween()
    {
        if (spikeWallTween != null && spikeWallTween.IsActive())
            spikeWallTween.Kill();
        spikeWallTween = null;
    }

    #endregion

    #region Gameplay Freeze And Terminal Cleanup

    private void StopNonDogGameplayTweens()
    {
        KillSpikeWallTween();
        KillPitPlatformButtonRevealTween();
        FreezeLiftPitMovement();
        DisarmArrowHazardsForTerminalOutcome();
        StopFlyingDoorTween();
    }

    private void StopGameplayTweensForTerminalOutcome()
    {
        KillDogChargeTween();
        StopNonDogGameplayTweens();
    }

    private void StopFlyingDoorTween()
    {
        // Level104FlyingDoorCtrl.OnDisable 会杀死其内部 Sequence；失败后本实例无需恢复。
        if (flyingDoor != null && flyingDoor.enabled)
            flyingDoor.enabled = false;
    }

    private void TeardownGameplay()
    {
        isTeardownRequested = true;
        hasPendingSpikeWallTouch = false;
        FreezeLiftPitMovement();
        ConfigureDogTrap(false);
        DisarmAllArrowTraps();
        KillTweens();
        StopFlyingDoorTween();

        if (dogHitEffect != null)
            dogHitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (arrowHitEffect != null)
            arrowHitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (actor != null)
        {
            actor.StopKnock();
            actor.StopMoving();
            actor.SetMoveInput(0f);
        }
    }

    #endregion

    #region Script Driven Wrap

    private void WrapActorToLeftEdge()
    {
        Vector3 position = actor.transform.position;
        Vector3 wrapped = new Vector3(wrapLeftX, position.y, position.z);
        actor.transform.position = wrapped;

        Rigidbody2D body = actor.RB;
        if (body != null)
            body.position = new Vector2(wrapped.x, wrapped.y);

        SnapCameraToInitialPosition();
    }

    /// <summary>绕屏同帧把跟随相机从右侧锁定位瞬移回初始位，避免保留上一屏位置并横摇揭示中段；
    /// 随后的相机变化仍由 CameraFollowArea 根据演员位置、死区与 CameraFollowRange 正常处理。</summary>
    private void SnapCameraToInitialPosition()
    {
        if (!hasLevelCameraInitialPosition || levelCamera == null)
            return;

        // CameraFollowRange 与 Level 105 同值；正常初始位在 1920×1080、2340×1080 下均无需夹移。
        // 仍统一经过 ClampOrthographic，确保其他比例和后续边界调整下瞬移目标与 LateUpdate 一致。
        Vector3 target = levelCameraInitialPosition;
        if (cameraFollowRange != null)
            target = cameraFollowRange.ClampOrthographic(levelCamera, target);
        levelCamera.transform.position = target;
    }

    #endregion

    #region Reset

    private void OnTriggerTrap(OnTriggerTrapEvent evt)
    {
        FreezeLiftPitMovement();
    }

    private void OnTriggerFailArea(OnTriggerFailAreaEvent evt)
    {
        FreezeLiftPitMovement();
    }

    private void FreezeLiftPitMovement()
    {
        if (liftPitMovementFrozen)
            return;

        liftPitMovementFrozen = true;
        KillLiftPitTween();
    }

    private void OnGameReset(GameResetEvent evt)
    {
        TeardownGameplay();
    }

    #endregion
}

using System;
using System.Collections;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.SaveModule;
using DG.Tweening;
using Game.Events;
using Game.Level078;
using Game.Play;
using Game.Scripts.Common;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Level 129 local boss encounter. Scene objects, input adaptation, attacks and
/// result arbitration remain local to this level and do not modify shared play code.
/// </summary>
public sealed class Level129Ctrl : MonoBehaviour, IEventSender
{
    private enum BossSkill
    {
        GroundSpikes,
        Fireball,
        FanSpikes,
        MeteorWave,
        RollingStones,
    }

    private enum ArrowTarget
    {
        BossBody,
        HealthBar,
    }

    private static readonly BossSkill[] RandomSkillPool =
    {
        BossSkill.GroundSpikes,
        BossSkill.Fireball,
        BossSkill.FanSpikes,
        BossSkill.MeteorWave,
        BossSkill.RollingStones,
    };

    private const int MaxConsecutiveSameSkill = 2;
    private const string SceneSortingLayer = "SceneLayer";
    private const string BossIdleAnimation = "idle";
    private const string BossWarningAnimation = "step";
    private const string AimButtonPath = "Root/aim_btn";
    private const string JumpButtonPath = "Root/jump_btn";
    private const string BattleSoundTag = "l69battle";
    private const string FireballBurnSoundTag = "buildInFire";
    private const string CommonHitSoundTag = "crash";
    private const string HealthBarBreakSoundTag = "crashBroke";
    private const string GroundSpikeEmergeSoundTag = "frictional";
    private const string BossStompSoundTag = "level64Block";
    private const float BossFirstStompTime = 0.4667f;
    private const float BossSecondStompTime = 1.2667f;
    private const float ArrowWhooshSfxStartSeconds = 0.54f;
    private const float ArrowActionSfxVolume = 1f;
    private const string WarningAlphaTestKeyword = "_ALPHATEST_ON";
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");
    private static readonly int RendererColorPropertyId = Shader.PropertyToID("_RendererColor");
    private static readonly int AlphaClipPropertyId = Shader.PropertyToID("_AlphaClip");

    [Header("Scene References")]
    [SerializeField] private Level129BaseLevelCtrl levelCtrl;
    [SerializeField] private Level129ActorCtrl actor;
    [SerializeField] private Transform exitDoor;
    [SerializeField] private Transform exitDoorLandingPoint;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Transform gameplayLeftBound;
    [SerializeField] private Transform gameplayRightBound;

    [Header("Authored Boss")]
    [SerializeField] private Transform bossRoot;
    [SerializeField] private SkeletonAnimation bossSkeleton;
    [SerializeField] private SortingGroup bossSortingGroup;
    [SerializeField] private Transform healthBarRoot;
    [SerializeField] private SpriteRenderer healthBackgroundRenderer;
    [SerializeField] private SpriteRenderer healthFillRenderer;
    [SerializeField] private TextMeshPro hpText;
    [SerializeField] private Transform bossBodyHitPoint;
    [SerializeField] private Transform healthBarHitPoint;
    [SerializeField] private Transform fanSpikeSpawnPoint;
    [SerializeField] private Transform fireballSpawnPoint;
    [SerializeField] private Sprite firstCrackSprite;
    [SerializeField] private Sprite secondCrackSprite;
    [SerializeField] private TMP_FontAsset hpFont;

    [Header("Authored Bow Pickup")]
    [SerializeField] private Transform bowPickupRoot;
    [SerializeField] private Transform pickupBubbleVisual;
    [SerializeField] private Transform bowVisual;
    [SerializeField] private BoxCollider2D bowPickupCollider;
    [SerializeField] private Transform bowHandBone;
    [SerializeField] private Transform bowCarryAnchor;

    [Header("Bow And Arrow Assets")]
    [SerializeField] private Sprite arrowSprite;
    [SerializeField] private Sprite reticleSprite;
    [SerializeField] private Material arrowTrailMaterial;
    [SerializeField] private AudioClip arrowWhooshClip;

    [Header("Hit And Break Effects")]
    [Tooltip("陨石落地时播放的 Level127_BrickBroken 碎裂粒子 Prefab。")]
    [SerializeField] private ParticleSystem meteorBreakEffectPrefab;
    [Tooltip("任一攻击首次有效命中玩家、箭命中 Boss 或血条时复用的项目通用 Knock_Explosion 粒子 Prefab。")]
    [SerializeField] private ParticleSystem commonHitEffectPrefab;

    [Header("Attack Assets")]
    [SerializeField] private Sprite groundSpikeSprite;
    [SerializeField] private Material warningLineMaterial;
    [Tooltip("预警虚线的短划贴图变体；复用画线玩法 DrawGame_TipsLine 的三张贴图（斜杠/横杠/反斜杠），按种子随机交替且不连续重复。空列表回退到材质自带贴图。")]
    [SerializeField] private Texture2D[] warningSegmentTextures = Array.Empty<Texture2D>();
    [Tooltip("预警虚线贴图变体的随机种子；与画线玩法一致使用 xorshift，保证同关卡内观感可复现。")]
    [SerializeField] private int warningSegmentRandomSeed = 1;
    [SerializeField] private Sprite fireballSprite;
    [SerializeField] private RuntimeAnimatorController fireballAnimatorController;
    [SerializeField] private Sprite fanSpikeSprite;
    [SerializeField] private Sprite[] meteorSprites = Array.Empty<Sprite>();
    [SerializeField] private Sprite meteorShadowSprite;
    [SerializeField] private Sprite rollingStoneSprite;
    [SerializeField] private Sprite[] healthBarFragmentSprites = Array.Empty<Sprite>();

    [Header("World Layout")]
    [SerializeField] private float groundSurfaceY = -2.48f;
    [SerializeField] private int hazardSortingOrder = 210;
    [Tooltip("地刺排根 SortingGroup 的渲染序；固定低于地板（SceneLayer 0）、高于远景（-2），让地刺像从地里刺出。其余攻击物仍用 hazardSortingOrder。")]
    [SerializeField] private int groundSpikeSortingOrder = -1;

    [Header("Boss Health")]
    [SerializeField, Min(1)] private int bossMaxHp = 10000;
    [SerializeField, Min(1)] private int bodyArrowDamage = 5;
    [SerializeField, Min(1)] private int healthBarDurability = 3;

    [Header("Bow Pickup")]
    [SerializeField, Min(0f)] private float pickupFloatRange = 0.15f;
    [SerializeField, Min(0.01f)] private float pickupFloatOneWayDuration = 1.2f;
    [SerializeField, Min(0f)] private float pickupDismissDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float pickupFlyDuration = 0.45f;
    [SerializeField, Min(0f)] private float pickupFlyJumpPower = 0.5f;
    [SerializeField] private int bowCarriedSortingOrder = 300;

    [Header("HUD Aim")]
    [SerializeField] private Vector2 aimButtonAnchoredPosition = new Vector2(-601f, -403.5f);
    [SerializeField] private Vector2 aimButtonSize = new Vector2(290f, 153f);
    [SerializeField, Min(0.05f)] private float aimLongPressSeconds = 0.35f;
    [SerializeField, Min(1f)] private float healthBarSnapRadiusPixels = 110f;
    [SerializeField] private Vector2 reticleSize = new Vector2(108f, 108f);
    [SerializeField, Min(0f)] private float reticleFadeOutDuration = 0.2f;

    [Header("Arrow Flight: Level 078")]
    [SerializeField, Range(0f, 1f)] private float arrowReleaseNormalizedTime = 0.55f;
    [SerializeField, Min(0f)] private float shotRecoveryDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float arrowHorizontalSpeed = 27.65625f;
    [SerializeField] private Vector3 arrowReleaseOffset = new Vector3(0.75f, 0.8f, -0.1f);
    [SerializeField] private Vector3 arrowVisualScale = Vector3.one;
    [SerializeField, Min(0f)] private float arrowTrailTime = 0.1f;
    [SerializeField, Min(0f)] private float arrowTrailWidth = 0.1f;
    [SerializeField, Min(0f)] private float arrowImpactHoldDuration = 0.12f;
    [SerializeField, Min(0f)] private float arrowImpactFadeDuration = 0.08f;
    [Tooltip("箭头相对命中目标最高渲染序的增量；至少为 2，以便低一层的拖尾仍高于目标。")]
    [SerializeField, Min(2)] private int arrowSortingOffsetFromTarget = 2;

    [Header("Skill Timing")]
    [SerializeField, Min(0f)] private float skillCooldown = 0.65f;
    [SerializeField, Min(0.01f)] private float bossWarningFallbackDuration = 1.6f;
    [SerializeField, Min(0f)] private float bossWarningExtraDuration = 0.4f;

    [Header("Ground Spike Skill")]
    [SerializeField, Min(1)] private int groundSpikeCount = 10;
    [SerializeField] private Vector3 groundSpikeScale = Vector3.one;
    [SerializeField, Range(0.5f, 1f)] private float groundSpikeColliderFit = 0.94f;
    [SerializeField, Min(0f)] private float warningFadeInDuration = 0.35f;
    [SerializeField, Min(0f)] private float warningVisibleDuration = 0.45f;
    [SerializeField, Min(0f)] private float warningFadeOutDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float groundSpikeEmergeDuration = 0.22f;
    [SerializeField, Min(0f)] private float groundSpikeHoldDuration = 0.8f;
    [SerializeField, Min(0.01f)] private float groundSpikeRetractDuration = 0.2f;
    [SerializeField, Min(0f)] private float groundSpikeHiddenDepth = 0.65f;
    [SerializeField, Min(0.01f)] private float warningLineWidth = 0.12f;
    [SerializeField, Min(0.01f)] private float warningDashTextureRepeat = 3f;
    [SerializeField, Min(0f)] private float warningDashGap = 0.08f;

    [Header("Fireball Skill")]
    [SerializeField] private Vector3 fireballScale = Vector3.one * 1.2f;
    [SerializeField, Range(0.5f, 1f)] private float fireballColliderFit = 0.9f;
    [SerializeField, Min(0.01f)] private float fireballSpeed = 8f;
    [SerializeField] private float fireballRotationZ = -90f;

    [Header("Fan Spike Skill")]
    [SerializeField, Min(1)] private int fanSpikeCount = 7;
    [SerializeField] private Vector2 fanAngleRange = new Vector2(100f, 220f);
    [SerializeField] private Vector3 fanSpikeScale = Vector3.one * 0.6f;
    [SerializeField, Range(0.5f, 1f)] private float fanSpikeColliderFit = 0.92f;
    [SerializeField, Min(0.01f)] private float fanSpikeSpeed = 5.333333f;
    [SerializeField, Min(0.1f)] private float fanSpikeTravelDistance = 22f;

    [Header("Meteor Skill")]
    [SerializeField] private Vector2Int meteorGroupCountRange = new Vector2Int(2, 4);
    [SerializeField] private Vector2Int meteorCountPerGroupRange = new Vector2Int(2, 5);
    [SerializeField, Min(3)] private int meteorLaneCount = 7;
    [SerializeField] private Vector2 meteorStartDelayRange = new Vector2(0.05f, 0.2f);
    [SerializeField] private Vector2 meteorStaggerRange = new Vector2(0.16f, 0.42f);
    [SerializeField] private Vector2 meteorFallDurationRange = new Vector2(0.8f, 1.35f);
    [SerializeField, Min(0f)] private float meteorGroupInterval = 0.35f;
    [SerializeField, Min(0.1f)] private float meteorTargetHeight = 1.4f;
    [SerializeField, Range(0.5f, 1f)] private float meteorColliderFit = 0.9f;
    [SerializeField, Min(0f)] private float meteorSpawnAboveCamera = 1.5f;
    [SerializeField, Min(0f)] private float meteorImpactBelowGround = 0.8f;
    [SerializeField] private Vector3 meteorShadowScale = new Vector3(1.2f, 0.28f, 1f);
    [SerializeField] private Color meteorShadowStartColor = new Color(0f, 0f, 0f, 0.18f);
    [SerializeField] private Color meteorShadowEndColor = new Color(0f, 0f, 0f, 0.58f);

    [Header("Rolling Stone Skill")]
    [SerializeField] private Vector3 rollingStoneScale = Vector3.one * 0.5f;
    [SerializeField, Range(0.5f, 1f)] private float rollingStoneColliderFit = 0.94f;
    [SerializeField, Min(0.01f)] private float rollingStoneSpeed = 5.85f;
    [SerializeField, Min(0f)] private float rollingStoneSpawnMargin = 1f;
    [SerializeField, Min(0f)] private float rollingStonePostSpawnDelay = 0.3f;
    [Tooltip("第二颗滚石相对第一颗生成时刻的最小间隔（对局时钟锚点，暂停不计时）；当前 5.85u/s × 2.65s 形成 15.5025 世界单位名义间距。")]
    [SerializeField, Min(0f)] private float rollingStoneSpawnInterval = 2.65f;

    [Header("Boss Defeat And Door")]
    [SerializeField, Min(0f)] private float bossFadeDuration = 0.8f;
    [Tooltip("血条碎裂后，Boss 开始渐隐前的短暂保留时间；碎片仍按 fragmentLifetime 完整播放。")]
    [SerializeField, Min(0f)] private float bossFadeLeadAfterHealthBarShatter = 0.4f;
    [SerializeField, Min(0.01f)] private float exitDoorDropDuration = 0.8f;
    [SerializeField, Min(0f)] private float minimumDeathPresentationSeconds = 1.5f;

    [Header("Boss Defeated Banner (Presentation Only)")]
    [SerializeField] private Sprite missionBannerSprite;
    [SerializeField] private Sprite completeBannerSprite;
    [SerializeField, Min(0.01f)] private float bannerPopDuration = 0.3f;
    [SerializeField, Min(0f)] private float bannerHoldDuration = 1f;
    [SerializeField, Min(0.01f)] private float bannerFadeDuration = 0.3f;

    [Header("Health Bar Fragment Burst")]
    [SerializeField] private Vector3 fragmentScale = Vector3.one;
    [SerializeField] private Vector2 fragmentHorizontalSpeedRange = new Vector2(1.5f, 4.5f);
    [SerializeField] private Vector2 fragmentVerticalSpeedRange = new Vector2(2.5f, 5.5f);
    [SerializeField, Min(0f)] private float fragmentGravity = 9f;
    [SerializeField] private Vector2 fragmentAngularSpeedRange = new Vector2(-420f, 420f);
    [SerializeField, Min(0.01f)] private float fragmentLifetime = 1.5f;
    [SerializeField, Min(0f)] private float fragmentFadeDuration = 0.45f;

    private EventSubscriber subscriber;
    private readonly List<GameObject> runtimeObjects = new();
    private readonly List<GameObject> activeSkillObjects = new();
    private readonly List<Collider2D> hazardColliders = new();

    private Sprite baseHealthFillSprite;
    private Sprite activeHealthFillSprite;

    private GamePlayPanel gamePlayPanel;
    private CustomButton aimButton;
    private CustomButton jumpButton;
    private Level129AimDragHandle aimDragHandle;
    private RectTransform reticleRect;
    private Image reticleImage;
    private RectTransform hudRootRect;
    private Coroutine reticleFadeCoroutine;
    private RectTransform defeatedBannerRect;
    private CanvasGroup defeatedBannerGroup;
    private float defeatedBannerStartClock;

    private AudioSource arrowWhooshSource;
    private AudioSource fanSpikeWhooshSource;
    private MelenitasDev.SoundsGood.Sound battleSound;
    private GameObject activeImpactArrow;
    private Coroutine bossSkillLoop;
    private DG.Tweening.Sequence bowRecoverySequence;
    private Material[] warningLineMaterialInstances = Array.Empty<Material>();
    private Mesh warningRectangleMesh;

    /// <summary>对局时钟：仅在非暂停时累计，用作双滚石内部两颗滚石的固定间隔锚点。</summary>
    private float gameplayClock;
    private float rollingStoneSpawnAnchorClock;

    private Vector2 gameplayWorldXRange = new Vector2(-7.11f, 6.25f);
    private Vector3 bowPickupBaseLocalPosition;
    private Transform bowInitialParent;
    private Vector3 bowInitialLocalPosition;
    private Quaternion bowInitialLocalRotation;
    private Vector3 bowInitialLocalScale;
    private readonly Dictionary<SpriteRenderer, int> bowInitialSortingOrders = new();
    private readonly Dictionary<SpriteRenderer, Color> bowInitialColors = new();
    private int bowInitialSortingOrderBase;
    private Vector3 exitDoorLandingPosition;
    private Collider2D[] exitDoorColliders = Array.Empty<Collider2D>();
    private Color healthBackgroundInitialColor = Color.white;
    private Color healthFillInitialColor = Color.white;
    private Color hpTextInitialColor = Color.white;

    private int bossHp;
    private int healthBarHitCount;
    private BossSkill? lastSelectedSkill;
    private int consecutiveSelectedSkillCount;
    private int arrowsInFlight;
    private bool bowCollecting;
    private bool bowEquipped;
    private bool bossDefeated;
    private bool waitingForHealthBarShatter;
    private bool bossFadeCompleted;
    private bool exitDoorLanded;
    private bool playerDeathCommitted;
    private bool failureDeferred;
    private float failurePresentationElapsed;
    private bool isPaused;
    private bool attemptEnding;
    private bool cleanedUp;
    private bool aimDragging;
    private bool reticleSnapped;
    private bool battleSoundStarted;
    private bool battleMusicPaused;

    public bool HasPlayerDeathCommitted => playerDeathCommitted;

    public bool CanFinalizeFailure => !failureDeferred;

    internal bool IsRuntimeSuspended => isPaused || cleanedUp;

    private void Awake()
    {
        ResolveSceneReferences();
        InitializeAuthoredScene();
        BindEvents();
    }

    private void Start()
    {
        ResolveHud();
    }

    private void Update()
    {
        if (cleanedUp)
            return;

        if (!isPaused)
            gameplayClock += Time.deltaTime;

        ResolveHud();
        UpdateSnappedReticlePosition();
        DetectExternalActorDeath();
        if (playerDeathCommitted && !isPaused)
            failurePresentationElapsed += Time.deltaTime;
        TryFinalizeDeferredFailure();
        DetectAcceptedSuccess();
        UpdateDefeatedBanner();
    }

    private void OnDisable()
    {
        CleanupRuntime();
    }

    private void OnDestroy()
    {
        CleanupRuntime();
    }

    #region Initialization

    private void ResolveSceneReferences()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponent<Level129BaseLevelCtrl>();

        if (actor == null)
            actor = transform.Find("Actor")?.GetComponent<Level129ActorCtrl>();

        if (exitDoor == null)
            exitDoor = transform.Find("door_0");

        if (worldCamera == null)
            worldCamera = GetComponentInChildren<Camera>(true);
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (levelCtrl != null && actor != null)
            levelCtrl.SetActor(actor);

        actor?.SetBowCarried(false);
        bossHp = Mathf.Max(1, bossMaxHp);
    }

    private void InitializeAuthoredScene()
    {
        InitializeGameplayBounds();
        InitializeExitDoor();
        InitializeBoss();
        InitializeBowPickup();
    }

    private void InitializeGameplayBounds()
    {
        if (gameplayLeftBound == null || gameplayRightBound == null)
        {
            Debug.LogError("[Level129Ctrl] Authored gameplay bounds are missing.", this);
            return;
        }

        gameplayWorldXRange = new Vector2(
            Mathf.Min(gameplayLeftBound.position.x, gameplayRightBound.position.x),
            Mathf.Max(gameplayLeftBound.position.x, gameplayRightBound.position.x));
    }

    private void InitializeExitDoor()
    {
        if (exitDoor == null || exitDoorLandingPoint == null)
        {
            Debug.LogError("[Level129Ctrl] Authored exit door or landing point is missing.", this);
            return;
        }

        exitDoorLandingPosition = exitDoorLandingPoint.position;
        exitDoor.gameObject.SetActive(true);
        exitDoorColliders = exitDoor.GetComponentsInChildren<Collider2D>(true);
        SetExitDoorCollidersEnabled(false);
    }

    private void InitializeBoss()
    {
        if (bossRoot == null || bossSkeleton == null)
        {
            Debug.LogError("[Level129Ctrl] Authored big dog references are missing.", this);
            return;
        }

        bossRoot.gameObject.SetActive(true);
        bossSkeleton.Initialize(false);
        bossSkeleton.AnimationState?.SetAnimation(0, BossIdleAnimation, true);

        if (bossSortingGroup == null)
            Debug.LogError("[Level129Ctrl] Authored boss SortingGroup is missing.", this);

        if (healthBarRoot == null ||
            healthBackgroundRenderer == null ||
            healthFillRenderer == null ||
            hpText == null)
        {
            Debug.LogError("[Level129Ctrl] Authored boss health bar references are missing.", this);
            return;
        }

        healthBarRoot.gameObject.SetActive(true);
        baseHealthFillSprite = healthFillRenderer.sprite;
        activeHealthFillSprite = baseHealthFillSprite;
        healthBackgroundInitialColor = healthBackgroundRenderer.color;
        healthFillInitialColor = healthFillRenderer.color;
        hpTextInitialColor = hpText.color;
        UpdateHealthBarVisual();
    }

    private void InitializeBowPickup()
    {
        if (bowPickupRoot == null ||
            pickupBubbleVisual == null ||
            bowVisual == null ||
            bowPickupCollider == null ||
            bowHandBone == null ||
            bowCarryAnchor == null)
        {
            Debug.LogError("[Level129Ctrl] Authored bow pickup references are missing.", this);
            return;
        }

        bowPickupBaseLocalPosition = bowPickupRoot.localPosition;
        bowInitialParent = bowVisual.parent;
        bowInitialLocalPosition = bowVisual.localPosition;
        bowInitialLocalRotation = bowVisual.localRotation;
        bowInitialLocalScale = bowVisual.localScale;
        CacheBowSortingOrders();
        RestoreBowAuthoredState();
        bowPickupRoot.gameObject.SetActive(true);
        pickupBubbleVisual.gameObject.SetActive(true);
        bowVisual.gameObject.SetActive(true);
        bowHandBone.gameObject.SetActive(true);
        bowCarryAnchor.gameObject.SetActive(true);
        bowPickupCollider.isTrigger = true;
        ApplyActorOnlyFilter(bowPickupCollider, ResolveActorMask());
        Level129BowPickupTrigger trigger = bowPickupRoot.GetComponent<Level129BowPickupTrigger>();
        if (trigger == null)
            trigger = bowPickupRoot.gameObject.AddComponent<Level129BowPickupTrigger>();
        trigger.Initialize(this);

        bowPickupRoot.DOLocalMoveY(
                bowPickupBaseLocalPosition.y + pickupFloatRange,
                pickupFloatOneWayDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetTarget(bowPickupRoot.gameObject)
            .SetLink(bowPickupRoot.gameObject);
    }

    #endregion

    #region HUD And Aim

    private void ResolveHud()
    {
        if (gamePlayPanel != null)
            return;

        gamePlayPanel = PlayCtrl.Instance?.CurrentGamePlayPanel;
        if (gamePlayPanel == null)
            return;

        Transform aimTransform = gamePlayPanel.transform.Find(AimButtonPath);
        Transform jumpTransform = gamePlayPanel.transform.Find(JumpButtonPath);
        aimButton = aimTransform != null ? aimTransform.GetComponent<CustomButton>() : null;
        jumpButton = jumpTransform != null ? jumpTransform.GetComponent<CustomButton>() : null;
        hudRootRect = aimTransform != null ? aimTransform.parent as RectTransform : null;

        if (aimTransform != null)
        {
            RectTransform aimRect = aimTransform as RectTransform;
            if (aimRect != null)
            {
                aimRect.anchoredPosition = aimButtonAnchoredPosition;
                aimRect.sizeDelta = aimButtonSize;
                aimRect.localScale = Vector3.one;
            }

            aimDragHandle = aimTransform.GetComponent<Level129AimDragHandle>();
            if (aimDragHandle == null)
                aimDragHandle = aimTransform.gameObject.AddComponent<Level129AimDragHandle>();
            aimDragHandle.Initialize(this);
        }

        if (jumpButton != null)
            jumpButton.gameObject.SetActive(true);

        CreateHudReticle();
        SetAimButtonVisible(
            bowEquipped &&
            !bossDefeated &&
            !playerDeathCommitted &&
            !attemptEnding);
    }

    private void CreateHudReticle()
    {
        if (reticleRect != null || hudRootRect == null || reticleSprite == null)
            return;

        GameObject reticleObject = new GameObject(
            "Level129AimReticle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        reticleObject.layer = LayerMask.NameToLayer("UI");
        reticleRect = reticleObject.GetComponent<RectTransform>();
        reticleRect.SetParent(hudRootRect, false);
        reticleRect.anchorMin = new Vector2(0.5f, 0.5f);
        reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
        reticleRect.pivot = new Vector2(0.5f, 0.5f);
        reticleRect.sizeDelta = reticleSize;
        reticleImage = reticleObject.GetComponent<Image>();
        reticleImage.sprite = reticleSprite;
        reticleImage.preserveAspect = true;
        reticleImage.raycastTarget = false;
        SetReticleAlpha(1f);
        reticleObject.SetActive(false);
    }

    private bool CanShowDefeatedBanner => !cleanedUp && !attemptEnding &&
        !playerDeathCommitted && actor != null && !actor.IsDied &&
        levelCtrl != null && !levelCtrl.IsActorEnterDoor;

    private void ShowDefeatedBanner()
    {
        if (!CanShowDefeatedBanner || defeatedBannerRect != null)
            return;

        ResolveHud();
        if (gamePlayPanel == null || missionBannerSprite == null || completeBannerSprite == null)
        {
            Debug.LogWarning("[Level129Ctrl] Boss defeated banner HUD or sprites are missing.", this);
            return; // Presentation must never block the existing door flow.
        }

        GameObject root = new GameObject("Level129BossDefeatedBanner", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = LayerMask.NameToLayer("UI");
        defeatedBannerRect = root.GetComponent<RectTransform>();
        defeatedBannerRect.SetParent(gamePlayPanel.transform, false);
        defeatedBannerRect.anchorMin = defeatedBannerRect.anchorMax = new Vector2(0.5f, 0.5f);
        defeatedBannerRect.pivot = new Vector2(0.5f, 0.5f);
        defeatedBannerRect.anchoredPosition = Vector2.zero;
        // Shared source-pixel scale preserves both images' relative lettering size.
        float gap = 28f;
        float height = missionBannerSprite.rect.height + gap + completeBannerSprite.rect.height;
        defeatedBannerRect.sizeDelta = new Vector2(completeBannerSprite.rect.width, height);
        CreateDefeatedBannerLine("Mission", missionBannerSprite,
            (height - missionBannerSprite.rect.height) * 0.5f);
        CreateDefeatedBannerLine("Complete", completeBannerSprite,
            -(height - completeBannerSprite.rect.height) * 0.5f);
        defeatedBannerGroup = root.GetComponent<CanvasGroup>();
        defeatedBannerGroup.interactable = false;
        defeatedBannerGroup.blocksRaycasts = false;
        defeatedBannerStartClock = gameplayClock;
        UpdateDefeatedBanner();
        AudioManager.PlaySound("success");
    }

    private void CreateDefeatedBannerLine(string objectName, Sprite sprite, float y)
    {
        GameObject line = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        line.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.SetParent(defeatedBannerRect, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = sprite.rect.size;
        Image image = line.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void UpdateDefeatedBanner()
    {
        if (defeatedBannerRect == null)
            return;
        float elapsed = gameplayClock - defeatedBannerStartClock;
        float pop = Mathf.Max(0.01f, bannerPopDuration);
        float hold = Mathf.Max(0f, bannerHoldDuration);
        float fade = Mathf.Max(0.01f, bannerFadeDuration);
        if (!CanShowDefeatedBanner || elapsed >= pop + hold + fade)
        {
            HideDefeatedBanner();
            return;
        }

        RectTransform parent = defeatedBannerRect.parent as RectTransform;
        if (parent == null)
            return;
        float fit = Mathf.Min(parent.rect.width * 0.74f / defeatedBannerRect.sizeDelta.x,
            parent.rect.height * 0.40f / defeatedBannerRect.sizeDelta.y);
        float t = Mathf.Clamp01(elapsed / pop) - 1f;
        // Ease-out-back: grow continuously from zero, gently overshoot, settle at one.
        float scale = 1f + 2.70158f * t * t * t + 1.70158f * t * t;
        defeatedBannerRect.localScale = Vector3.one * (fit * Mathf.Max(0f, scale));
        float fadeProgress = Mathf.Clamp01((elapsed - pop - hold) / fade);
        defeatedBannerGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, fadeProgress);
    }

    private void HideDefeatedBanner()
    {
        if (defeatedBannerRect != null)
        {
            defeatedBannerRect.gameObject.SetActive(false);
            Destroy(defeatedBannerRect.gameObject);
        }
        defeatedBannerRect = null;
        defeatedBannerGroup = null;
    }

    internal bool CanBeginAimPointer()
    {
        return bowEquipped &&
               !bowCollecting &&
               !bossDefeated &&
               !playerDeathCommitted &&
               !attemptEnding &&
               !isPaused &&
               actor != null &&
               !actor.IsDied;
    }

    internal void HandleAimPointerDown(Vector2 screenPosition)
    {
        aimDragging = false;
    }

    internal void HandleAimPointerHold(Vector2 screenPosition, float heldSeconds)
    {
        if (!CanBeginAimPointer())
            return;

        if (reticleSnapped || heldSeconds < aimLongPressSeconds)
            return;

        aimDragging = true;
        SetReticleScreenPosition(screenPosition);
    }

    internal void HandleAimPointerMoved(Vector2 screenPosition, float heldSeconds)
    {
        if (!CanBeginAimPointer())
            return;

        if (reticleSnapped)
            return;

        if (heldSeconds >= aimLongPressSeconds)
            aimDragging = true;

        if (aimDragging)
            SetReticleScreenPosition(screenPosition);
    }

    internal void HandleAimPointerReleased(
        Vector2 screenPosition,
        float heldSeconds,
        bool cancelled)
    {
        if (!CanBeginAimPointer())
        {
            if (!reticleSnapped)
                HideFreeReticle();
            return;
        }

        if (cancelled)
        {
            if (!reticleSnapped)
                HideFreeReticle();
            return;
        }

        if (reticleSnapped)
        {
            TryStartPlayerShot(ArrowTarget.HealthBar);
            return;
        }

        bool wasLongPress = aimDragging || heldSeconds >= aimLongPressSeconds;
        if (!wasLongPress)
        {
            TryStartPlayerShot(ArrowTarget.BossBody);
            return;
        }

        SetReticleScreenPosition(screenPosition);
        if (IsScreenPositionNearHealthBar(screenPosition))
        {
            reticleSnapped = true;
            UpdateSnappedReticlePosition();
        }
        else
        {
            HideFreeReticle();
        }

        aimDragging = false;
    }

    private void SetReticleScreenPosition(Vector2 screenPosition)
    {
        if (bossDefeated || playerDeathCommitted || attemptEnding || cleanedUp)
            return;

        CreateHudReticle();
        if (reticleRect == null || hudRootRect == null)
            return;

        StopReticleFade();
        SetReticleAlpha(1f);
        reticleRect.gameObject.SetActive(true);
        Camera eventCamera = gamePlayPanel != null && gamePlayPanel.GetComponentInParent<Canvas>()?.renderMode != RenderMode.ScreenSpaceOverlay
            ? worldCamera
            : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                hudRootRect,
                screenPosition,
                eventCamera,
                out Vector2 localPoint))
        {
            reticleRect.anchoredPosition = localPoint;
        }
    }

    private void UpdateSnappedReticlePosition()
    {
        if (!reticleSnapped ||
            bossDefeated ||
            playerDeathCommitted ||
            attemptEnding ||
            cleanedUp ||
            healthBarHitPoint == null ||
            worldCamera == null)
            return;

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(healthBarHitPoint.position);
        SetReticleScreenPosition(screenPoint);
    }

    private bool IsScreenPositionNearHealthBar(Vector2 screenPosition)
    {
        if (healthBarHitPoint == null || worldCamera == null)
            return false;

        Vector2 healthScreen = worldCamera.WorldToScreenPoint(healthBarHitPoint.position);
        return Vector2.Distance(screenPosition, healthScreen) <= healthBarSnapRadiusPixels;
    }

    private void HideFreeReticle()
    {
        aimDragging = false;
        if (!reticleSnapped)
            HideReticleImmediately(false);
    }

    private void FadeOutReticle()
    {
        aimDragging = false;
        reticleSnapped = false;
        StopReticleFade();
        if (reticleRect == null || reticleImage == null || !reticleRect.gameObject.activeSelf)
        {
            HideReticleImmediately(false);
            return;
        }

        if (reticleFadeOutDuration <= 0f)
        {
            HideReticleImmediately(false);
            return;
        }

        reticleFadeCoroutine = StartCoroutine(PlayReticleFadeOut());
    }

    private IEnumerator PlayReticleFadeOut()
    {
        float elapsed = 0f;
        float startAlpha = reticleImage != null ? reticleImage.color.a : 1f;
        while (elapsed < reticleFadeOutDuration && !cleanedUp)
        {
            if (!isPaused)
            {
                elapsed += Time.deltaTime;
                SetReticleAlpha(Mathf.Lerp(
                    startAlpha,
                    0f,
                    Mathf.Clamp01(elapsed / reticleFadeOutDuration)));
            }
            yield return null;
        }

        reticleFadeCoroutine = null;
        if (reticleRect != null)
            reticleRect.gameObject.SetActive(false);
        SetReticleAlpha(1f);
    }

    private void HideReticleImmediately(bool clearSnappedState)
    {
        StopReticleFade();
        aimDragging = false;
        if (clearSnappedState)
            reticleSnapped = false;
        if (reticleRect != null)
            reticleRect.gameObject.SetActive(false);
        SetReticleAlpha(1f);
    }

    private void StopReticleFade()
    {
        if (reticleFadeCoroutine == null)
            return;

        StopCoroutine(reticleFadeCoroutine);
        reticleFadeCoroutine = null;
    }

    private void SetReticleAlpha(float alpha)
    {
        if (reticleImage == null)
            return;

        Color color = reticleImage.color;
        color.a = Mathf.Clamp01(alpha);
        reticleImage.color = color;
    }

    private void SetAimButtonVisible(bool visible)
    {
        if (aimButton == null)
            return;

        aimButton.gameObject.SetActive(visible);
        aimButton.Interactable = visible;
        if (visible)
            aimButton.transform.localScale = Vector3.one;
    }

    #endregion

    #region Bow Pickup And Shooting

    internal void TryCollectBow(ActorCtrl hitActor)
    {
        if (bowCollecting || bowEquipped || actor == null || hitActor != actor || actor.IsDied ||
            bowPickupRoot == null || bowVisual == null || bowCarryAnchor == null)
            return;

        bowCollecting = true;
        if (bowPickupCollider != null)
            bowPickupCollider.enabled = false;
        if (bowPickupRoot != null)
            DOTween.Kill(bowPickupRoot.gameObject);

        levelCtrl?.SetCtrlable(false);
        actor.SetMoveInput(0f);
        actor.StopMoving();

        DG.Tweening.Sequence sequence = DOTween.Sequence().SetTarget(gameObject).SetLink(gameObject);
        if (pickupBubbleVisual != null)
        {
            sequence.Join(
                pickupBubbleVisual.DOScale(Vector3.zero, pickupDismissDuration)
                    .SetEase(Ease.InBack));
        }

        if (bowVisual != null && bowCarryAnchor != null)
        {
            // Level 083 instrument pattern: parent the bow to the carry anchor
            // first, then fly a small parabola toward the anchor's live pose so
            // hand movement during the flight cannot end in a landing snap.
            bowVisual.SetParent(bowCarryAnchor, true);
            Vector3 flightStartPosition = bowVisual.position;
            Quaternion flightStartRotation = bowVisual.rotation;
            Vector3 flightStartScale = bowVisual.localScale;
            sequence.Join(
                DOTween.To(
                        () => 0f,
                        progress =>
                        {
                            if (bowVisual == null || bowCarryAnchor == null)
                                return;

                            Vector3 position = Vector3.Lerp(
                                flightStartPosition,
                                bowCarryAnchor.position,
                                progress);
                            position.y += Mathf.Sin(progress * Mathf.PI) * pickupFlyJumpPower;
                            bowVisual.position = position;
                            bowVisual.rotation = Quaternion.Slerp(
                                flightStartRotation,
                                bowCarryAnchor.rotation,
                                progress);
                            bowVisual.localScale = Vector3.Lerp(
                                flightStartScale,
                                Vector3.Scale(bowInitialLocalScale, bowCarryAnchor.lossyScale),
                                progress);
                        },
                        1f,
                        pickupFlyDuration)
                    .SetEase(Ease.Linear));
        }

        sequence.OnComplete(CompleteBowPickup);
    }

    private void CompleteBowPickup()
    {
        if (cleanedUp)
            return;

        if (bowVisual != null && bowCarryAnchor != null)
        {
            bowVisual.SetParent(bowCarryAnchor, false);
            bowVisual.localPosition = Vector3.zero;
            bowVisual.localRotation = Quaternion.identity;
            bowVisual.localScale = bowInitialLocalScale;
            SetBowCarriedSortingOrder();
            bowVisual.gameObject.SetActive(true);
        }

        if (bowPickupRoot != null)
            bowPickupRoot.gameObject.SetActive(false);

        bowCollecting = false;
        bowEquipped = true;
        actor?.SetBowCarried(true);
        actor?.ResumeMoving();
        levelCtrl?.SetCtrlable(true);
        StartBattleSoundOnce();
        SetAimButtonVisible(true);

        if (bossSkillLoop == null)
            bossSkillLoop = StartCoroutine(RunBossSkillLoop());
    }

    private void TryStartPlayerShot(ArrowTarget target)
    {
        if (actor == null || !actor.CanStartArcheryShot || bossRoot == null || bossDefeated)
            return;

        actor.SetFaceDir(true);
        HideBowForShot();

        bool started = actor.TryPlayArcheryShot(
            arrowReleaseNormalizedTime,
            shotRecoveryDuration,
            () => LaunchPlayerArrow(target),
            BeginBowRecovery,
            CompleteBowRecovery);

        if (!started)
            ShowBowImmediately();
    }

    private void LaunchPlayerArrow(ArrowTarget target)
    {
        if (actor == null || arrowSprite == null || bossRoot == null)
            return;

        PlayArrowWhooshSound();
        Vector3 releasePoint = actor.transform.TransformPoint(arrowReleaseOffset);
        Transform hitTarget = target == ArrowTarget.HealthBar
            ? healthBarHitPoint
            : bossBodyHitPoint;
        if (hitTarget == null)
            return;
        Vector3 hitPoint = hitTarget.position;

        float horizontalDistance = Mathf.Abs(hitPoint.x - releasePoint.x);
        if (horizontalDistance <= 0.0001f)
            return;

        GameObject arrowRootObject = new GameObject($"Level129Arrow_{target}");
        Transform arrowRoot = arrowRootObject.transform;
        arrowRoot.SetParent(transform, false);
        RegisterRuntimeObject(arrowRootObject);

        GetArrowTargetTopSortingSettings(target, out int targetSortingLayerId, out int targetSortingOrder);
        int arrowSortingOrder = targetSortingOrder + Mathf.Max(2, arrowSortingOffsetFromTarget);

        SpriteRenderer arrowRenderer = CreateSpriteRenderer(
            "Visual",
            arrowRoot,
            arrowSprite,
            arrowSortingOrder,
            Vector3.zero,
            arrowVisualScale);
        arrowRenderer.sortingLayerID = targetSortingLayerId;
        Vector3 localTip = Vector3.right * arrowSprite.bounds.extents.x;
        arrowRenderer.transform.localPosition = -localTip;
        TrailRenderer trail = CreateArrowTrail(arrowRenderer.transform, arrowRenderer);

        Level78ArrowTrajectory trajectory = Level78ArrowTrajectory.Create(
            new Level78ArrowTrajectoryPoint(releasePoint.x, releasePoint.y),
            new Level78ArrowTrajectoryPoint(hitPoint.x, hitPoint.y));
        float flightDuration = Level78ArrowFlightTiming.CalculateDuration(
            releasePoint.x,
            hitPoint.x,
            arrowHorizontalSpeed);
        ApplyArrowTrajectory(arrowRoot, trajectory, releasePoint.z, 0f);
        if (trail != null)
        {
            // 箭先落到真实发射点，再清除初始化位置；只记录随后飞行产生的拖尾。
            trail.Clear();
            trail.emitting = true;
        }
        arrowsInFlight++;

        DOVirtual.Float(
                0f,
                1f,
                flightDuration,
                progress =>
                {
                    if (arrowRoot != null)
                        ApplyArrowTrajectory(arrowRoot, trajectory, releasePoint.z, progress);
                })
            .SetEase(Ease.Linear)
            .SetTarget(arrowRootObject)
            .SetLink(arrowRootObject)
            .OnComplete(() => CompletePlayerArrowImpact(
                arrowRootObject,
                arrowRenderer,
                trail,
                target,
                hitPoint));
    }

    private void CompletePlayerArrowImpact(
        GameObject arrowRootObject,
        SpriteRenderer arrowRenderer,
        TrailRenderer trail,
        ArrowTarget target,
        Vector3 hitPoint)
    {
        arrowsInFlight = Mathf.Max(0, arrowsInFlight - 1);
        if (trail != null)
            trail.emitting = false;

        if (!bossDefeated)
        {
            GetArrowTargetTopSortingSettings(target, out int targetSortingLayerId, out int targetSortingOrder);
            PlayCommonHitEffect(
                hitPoint,
                targetSortingLayerId,
                targetSortingOrder,
                "Level129BossHitEffect");
        }
        AudioManager.PlaySound(CommonHitSoundTag, ArrowActionSfxVolume);
        bool shattered = target == ArrowTarget.HealthBar
            ? ApplyHealthBarArrowDamage()
            : ApplyBossBodyArrowDamage();

        if (shattered || arrowRootObject == null || arrowRenderer == null)
        {
            DestroyRuntimeObject(arrowRootObject);
        }
        else
        {
            PlayShortImpactArrow(arrowRootObject, arrowRenderer);
        }

        RefreshFailureDeferralAfterArrowResolution();
    }

    private bool ApplyBossBodyArrowDamage()
    {
        if (bossDefeated)
            return false;

        bossHp = Mathf.Max(0, bossHp - Mathf.Max(1, bodyArrowDamage));
        UpdateHealthBarVisual();
        if (bossHp <= 0)
            BeginBossDefeat(false);
        return false;
    }

    private bool ApplyHealthBarArrowDamage()
    {
        if (bossDefeated)
            return false;

        healthBarHitCount++;
        if (healthBarHitCount == 1 && firstCrackSprite != null)
            SetHealthFillSprite(firstCrackSprite);
        else if (healthBarHitCount == 2 && secondCrackSprite != null)
            SetHealthFillSprite(secondCrackSprite);

        if (healthBarHitCount < Mathf.Max(1, healthBarDurability))
            return false;

        AudioManager.PlaySound(HealthBarBreakSoundTag);
        SpawnHealthBarFragments();
        if (healthBarRoot != null)
            healthBarRoot.gameObject.SetActive(false);
        BeginBossDefeat(true);
        return true;
    }

    private void PlayShortImpactArrow(GameObject arrowRootObject, SpriteRenderer arrowRenderer)
    {
        if (activeImpactArrow != null && activeImpactArrow != arrowRootObject)
            DestroyRuntimeObject(activeImpactArrow);

        activeImpactArrow = arrowRootObject;
        DG.Tweening.Sequence sequence = DOTween.Sequence()
            .SetTarget(arrowRootObject)
            .SetLink(arrowRootObject)
            .AppendInterval(arrowImpactHoldDuration);
        if (arrowImpactFadeDuration > 0f)
            sequence.Append(arrowRenderer.DOFade(0f, arrowImpactFadeDuration));
        sequence.OnComplete(() =>
        {
            if (activeImpactArrow == arrowRootObject)
                activeImpactArrow = null;
            DestroyRuntimeObject(arrowRootObject);
        });
    }

    private void StartBattleSoundOnce()
    {
        if (battleSoundStarted)
            return;

        battleSoundStarted = true;
        // Match Level 069's battle overlay, but loop explicitly because this
        // encounter is not bounded by Level 069's 20-second session.
        battleSound = AudioManager.PlaySound(BattleSoundTag, loop: true);
        if (battleSound == null)
            return;

        AudioManager.PauseMusic();
        battleMusicPaused = true;
    }

    private void StopBattleSound(bool resumeMusic)
    {
        battleSound?.Stop();
        battleSound = null;
        battleSoundStarted = false;

        if (resumeMusic && battleMusicPaused)
            AudioManager.ResumeMusic();

        battleMusicPaused = false;
    }

    private void PlayCommonHitEffect(
        Vector3 worldPosition,
        int targetSortingLayerId,
        int targetSortingOrder,
        string instanceName)
    {
        PlayParticleEffect(
            commonHitEffectPrefab,
            worldPosition,
            targetSortingLayerId,
            targetSortingOrder + 1,
            1f,
            instanceName);
    }

    private void PlayMeteorBreakEffect(Vector3 worldPosition)
    {
        PlayParticleEffect(
            meteorBreakEffectPrefab,
            worldPosition,
            SortingLayer.NameToID(SceneSortingLayer),
            hazardSortingOrder + 1,
            0.75f,
            "Level129MeteorBreakEffect");
    }

    private void PlayParticleEffect(
        ParticleSystem effectPrefab,
        Vector3 worldPosition,
        int sortingLayerId,
        int sortingOrder,
        float scaleMultiplier,
        string instanceName)
    {
        if (effectPrefab == null)
            return;

        ParticleSystem instance = Instantiate(
            effectPrefab,
            worldPosition,
            effectPrefab.transform.rotation,
            transform);
        instance.name = instanceName;
        instance.transform.localScale *= Mathf.Max(0f, scaleMultiplier);
        RegisterRuntimeObject(instance.gameObject);

        ParticleSystemRenderer sourceRootRenderer =
            effectPrefab.GetComponent<ParticleSystemRenderer>();
        int sourceRootSortingOrder = sourceRootRenderer != null
            ? sourceRootRenderer.sortingOrder
            : 0;
        Transform knockText = instance.transform.Find("Knock 1");
        if (knockText != null)
            knockText.gameObject.SetActive(false);

        foreach (ParticleSystemRenderer particleRenderer in
                 instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (particleRenderer == null)
                continue;

            particleRenderer.sortingLayerID = sortingLayerId;
            particleRenderer.sortingOrder = sortingOrder +
                particleRenderer.sortingOrder - sourceRootSortingOrder;
        }

        instance.gameObject.SetActive(true);
        instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        instance.Play(true);
        Destroy(instance.gameObject, 3f);
    }

    private void PlayArrowWhooshSound()
    {
        PlayWhooshSound(ref arrowWhooshSource, "Level129ArrowWhooshSource");
    }

    private void PlayFanSpikeWhooshSound()
    {
        PlayWhooshSound(ref fanSpikeWhooshSource, "Level129FanSpikeWhooshSource");
    }

    private void PlayWhooshSound(ref AudioSource source, string sourceName)
    {
        if (!DB.GameSetting.SfxOn || arrowWhooshClip == null)
            return;

        if (source == null)
        {
            GameObject sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        source.clip = arrowWhooshClip;
        source.volume = ArrowActionSfxVolume;
        source.time = Mathf.Clamp(
            ArrowWhooshSfxStartSeconds,
            0f,
            arrowWhooshClip.length);
        source.Play();
    }

    private TrailRenderer CreateArrowTrail(
        Transform arrowTransform,
        SpriteRenderer arrowRenderer)
    {
        if (arrowTrailMaterial == null || arrowTrailTime <= 0f || arrowTrailWidth <= 0f)
            return null;

        GameObject trailObject = new GameObject("Level129ArrowTrail");
        trailObject.transform.SetParent(arrowTransform, false);
        trailObject.transform.localPosition = Vector3.left * arrowSprite.bounds.extents.x;
        TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
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
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.sortingLayerID = arrowRenderer.sortingLayerID;
        trail.sortingOrder = arrowRenderer.sortingOrder - 1;
        trail.Clear();
        trail.emitting = false;
        return trail;
    }

    private static Gradient CreateArrowTrailGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.94f, 0.78f), 0f),
                new GradientColorKey(new Color(1f, 0.85f, 0.55f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private static void ApplyArrowTrajectory(
        Transform arrowRoot,
        Level78ArrowTrajectory trajectory,
        float worldZ,
        float progress)
    {
        Level78ArrowTrajectoryPoint point = trajectory.EvaluatePosition(progress);
        float angle = trajectory.EvaluateTangentAngleDegrees(progress);
        arrowRoot.position = new Vector3(point.X, point.Y, worldZ);
        arrowRoot.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    #endregion

    #region Boss Skill Loop

    private IEnumerator RunBossSkillLoop()
    {
        lastSelectedSkill = null;
        consecutiveSelectedSkillCount = 0;
        while (!cleanedUp && !bossDefeated && !playerDeathCommitted && !attemptEnding)
        {
            BossSkill skill = SelectNextRandomSkill();
            yield return ExecuteSkill(skill);

            if (bossDefeated || playerDeathCommitted || attemptEnding || cleanedUp)
                break;

            yield return WaitGameplaySeconds(skillCooldown);
        }

        bossSkillLoop = null;
    }

    private BossSkill SelectNextRandomSkill()
    {
        bool mustChangeSkill =
            lastSelectedSkill.HasValue &&
            consecutiveSelectedSkillCount >= MaxConsecutiveSameSkill &&
            RandomSkillPool.Length > 1;

        int selectedIndex;
        if (mustChangeSkill)
        {
            int blockedIndex = Array.IndexOf(RandomSkillPool, lastSelectedSkill.Value);
            selectedIndex = UnityEngine.Random.Range(0, RandomSkillPool.Length - 1);
            if (selectedIndex >= blockedIndex)
                selectedIndex++;
        }
        else
        {
            selectedIndex = UnityEngine.Random.Range(0, RandomSkillPool.Length);
        }

        BossSkill selectedSkill = RandomSkillPool[selectedIndex];
        if (lastSelectedSkill.HasValue && lastSelectedSkill.Value == selectedSkill)
        {
            consecutiveSelectedSkillCount++;
        }
        else
        {
            lastSelectedSkill = selectedSkill;
            consecutiveSelectedSkillCount = 1;
        }

        return selectedSkill;
    }

    private IEnumerator ExecuteSkill(BossSkill skill)
    {
        yield return PlayBossWarningToCompletion();
        if (cleanedUp || attemptEnding)
            yield break;

        switch (skill)
        {
            case BossSkill.GroundSpikes:
                yield return ExecuteGroundSpikeSkill();
                break;
            case BossSkill.Fireball:
                yield return ExecuteFireballSkill();
                break;
            case BossSkill.FanSpikes:
                yield return ExecuteFanSpikeSkill();
                break;
            case BossSkill.MeteorWave:
                yield return ExecuteMeteorWaveSkill();
                break;
            case BossSkill.RollingStones:
                yield return ExecuteRollingStonePairSkill();
                break;
        }
    }

    private IEnumerator ExecuteRollingStonePairSkill()
    {
        yield return ExecuteRollingStoneSkill(isFirstStone: true, waitUntilCleared: false);
        if (cleanedUp || attemptEnding || bossDefeated)
            yield break;

        // 双滚石是随机池中的一个完整技能。第一颗之后保留原冷却与第二次完整踏步预警；
        // Actor 死亡不会取消这项已选技能，但 Boss 败北或尝试重置仍会阻止第二颗生成。
        yield return WaitGameplaySeconds(skillCooldown);
        if (cleanedUp || attemptEnding || bossDefeated)
            yield break;

        yield return PlayBossWarningToCompletion();
        if (cleanedUp || attemptEnding || bossDefeated)
            yield break;

        yield return WaitRollingStoneInterval();
        if (cleanedUp || attemptEnding || bossDefeated)
            yield break;

        yield return ExecuteRollingStoneSkill(isFirstStone: false, waitUntilCleared: true);
    }

    /// <summary>
    /// 等到对局时钟到达“石 1 生成时刻 + rollingStoneSpawnInterval”。
    /// 用时间锚点而不是各段延迟叠加，配合滚石 1 的动态等待和两石连续
    /// 调度维持名义空间间距；暂停期间时钟与滚石同步冻结。
    /// </summary>
    private IEnumerator WaitRollingStoneInterval()
    {
        float targetClock = rollingStoneSpawnAnchorClock +
                            Mathf.Max(0f, rollingStoneSpawnInterval);
        while (gameplayClock < targetClock &&
               !cleanedUp && !bossDefeated && !attemptEnding)
        {
            yield return null;
        }
    }

    private float GetBossWarningNominalDuration()
    {
        Spine.Animation warningAnimation =
            bossSkeleton?.AnimationState?.Data?.SkeletonData?.FindAnimation(
                BossWarningAnimation);
        return warningAnimation != null
            ? Mathf.Max(0f, warningAnimation.Duration) + GetBossWarningExtraDuration()
            : Mathf.Max(0f, bossWarningFallbackDuration) + GetBossWarningExtraDuration();
    }

    private float GetBossWarningExtraDuration()
    {
        return Mathf.Max(0f, bossWarningExtraDuration);
    }

    private float GetRollingStonePostSpawnWait(bool isFirstStone)
    {
        if (!isFirstStone)
            return rollingStonePostSpawnDelay;

        // Rolling stone two must not spawn before stone one's anchored interval.
        // Keep the full cooldown and warning, and consume only the portion of
        // stone one's post-spawn wait that fits before those two phases. With
        // the current 2.65-second interval, cooldown + warning consume it all.
        float availableBeforeSecondWarning =
            Mathf.Max(0f, rollingStoneSpawnInterval) -
            Mathf.Max(0f, skillCooldown) -
            GetBossWarningNominalDuration();
        return Mathf.Min(
            Mathf.Max(0f, rollingStonePostSpawnDelay),
            Mathf.Max(0f, availableBeforeSecondWarning));
    }

    private IEnumerator PlayBossWarningToCompletion()
    {
        Spine.AnimationState animationState = bossSkeleton?.AnimationState;
        Spine.Animation warningAnimation =
            animationState?.Data?.SkeletonData?.FindAnimation(BossWarningAnimation);
        if (animationState == null || warningAnimation == null)
        {
            yield return WaitGameplaySeconds(
                Mathf.Max(0f, bossWarningFallbackDuration) +
                GetBossWarningExtraDuration());
            yield break;
        }

        bool completed = false;
        bool firstStompPlayed = false;
        bool secondStompPlayed = false;
        TrackEntry entry = animationState.SetAnimation(0, warningAnimation, false);
        if (entry == null)
        {
            yield return WaitGameplaySeconds(
                Mathf.Max(0f, bossWarningFallbackDuration) +
                GetBossWarningExtraDuration());
            yield break;
        }

        entry.Complete += _ => completed = true;
        while (!completed && !cleanedUp)
        {
            PlayBossStompSoundsAtTrackTime(
                entry.TrackTime,
                ref firstStompPlayed,
                ref secondStompPlayed);
            yield return null;
        }

        if (!cleanedUp)
        {
            PlayBossStompSoundsAtTrackTime(
                entry.TrackTime,
                ref firstStompPlayed,
                ref secondStompPlayed);
        }

        if ((!bossDefeated || waitingForHealthBarShatter) &&
            bossSkeleton?.AnimationState != null)
            bossSkeleton.AnimationState.SetAnimation(0, BossIdleAnimation, true);

        if (!cleanedUp)
            yield return WaitGameplaySeconds(GetBossWarningExtraDuration());
    }

    private static void PlayBossStompSoundsAtTrackTime(
        float trackTime,
        ref bool firstStompPlayed,
        ref bool secondStompPlayed)
    {
        if (!firstStompPlayed && trackTime >= BossFirstStompTime)
        {
            firstStompPlayed = true;
            AudioManager.PlaySound(BossStompSoundTag);
        }

        if (!secondStompPlayed && trackTime >= BossSecondStompTime)
        {
            secondStompPlayed = true;
            AudioManager.PlaySound(BossStompSoundTag);
        }
    }

    #endregion

    #region Ground Spikes

    private IEnumerator ExecuteGroundSpikeSkill()
    {
        if (groundSpikeSprite == null)
            yield break;

        float spikeWidth = groundSpikeSprite.bounds.size.x * Mathf.Abs(groundSpikeScale.x);
        float rowWidth = spikeWidth * Mathf.Max(1, groundSpikeCount);
        float minCenter = gameplayWorldXRange.x + rowWidth * 0.5f;
        float maxCenter = gameplayWorldXRange.y - rowWidth * 0.5f;
        float centerX = minCenter <= maxCenter
            ? UnityEngine.Random.Range(minCenter, maxCenter)
            : (gameplayWorldXRange.x + gameplayWorldXRange.y) * 0.5f;

        yield return PlayGroundSpikeWarning(centerX, rowWidth);

        GameObject rowObject = new GameObject("Level129GroundSpikeRow");
        rowObject.transform.SetParent(transform, false);
        RegisterSkillObject(rowObject);
        // Level 130 pattern: the whole spike row shares one root SortingGroup on
        // SceneLayer instead of per-sprite sorting. Ground spikes deliberately
        // render BELOW the floor slab (SceneLayer 0) and above the back scene
        // (-2) so the emerged spikes read as stabbing out of the ground; all
        // other hazards keep the higher hazardSortingOrder.
        SortingGroup rowSortingGroup = rowObject.AddComponent<SortingGroup>();
        rowSortingGroup.sortingLayerName = SceneSortingLayer;
        rowSortingGroup.sortingOrder = groundSpikeSortingOrder;
        List<Transform> spikes = new();
        float visibleY = groundSurfaceY - groundSpikeSprite.bounds.min.y * groundSpikeScale.y;
        float hiddenY = visibleY - groundSpikeHiddenDepth;
        int actorMask = ResolveActorMask();

        for (int index = 0; index < groundSpikeCount; index++)
        {
            float x = centerX - rowWidth * 0.5f + spikeWidth * (index + 0.5f);
            GameObject spikeObject = new GameObject($"Spike_{index + 1:00}");
            spikeObject.transform.SetParent(rowObject.transform, false);
            spikeObject.transform.position = new Vector3(x, hiddenY, 0f);
            spikeObject.transform.localScale = groundSpikeScale;
            SpriteRenderer renderer = spikeObject.AddComponent<SpriteRenderer>();
            ConfigureRenderer(renderer, groundSpikeSprite, hazardSortingOrder);
            Collider2D collider = AddPolygonActorTrigger(
                spikeObject,
                groundSpikeColliderFit,
                actorMask);
            Level129HazardHitbox hitbox = spikeObject.AddComponent<Level129HazardHitbox>();
            hitbox.Initialize(this);
            hazardColliders.Add(collider);
            spikes.Add(spikeObject.transform);
        }

        AudioManager.PlaySound(GroundSpikeEmergeSoundTag);
        DG.Tweening.Sequence emerge = DOTween.Sequence().SetTarget(rowObject).SetLink(rowObject);
        foreach (Transform spike in spikes)
            emerge.Join(spike.DOMoveY(visibleY, groundSpikeEmergeDuration).SetEase(Ease.OutBack));
        yield return WaitGameplaySeconds(groundSpikeEmergeDuration + groundSpikeHoldDuration);

        DG.Tweening.Sequence retract = DOTween.Sequence().SetTarget(rowObject).SetLink(rowObject);
        foreach (Transform spike in spikes)
            retract.Join(spike.DOMoveY(hiddenY, groundSpikeRetractDuration).SetEase(Ease.InQuad));
        yield return WaitGameplaySeconds(groundSpikeRetractDuration);
        DestroyRuntimeObject(rowObject);
    }

    private IEnumerator PlayGroundSpikeWarning(float centerX, float rowWidth)
    {
        GameObject warningObject = new GameObject("Level129GroundSpikeWarning");
        warningObject.transform.SetParent(transform, false);
        RegisterSkillObject(warningObject);

        // 预警框与尖刺实际视觉范围对齐：宽 = 行宽（rowWidth），高 = 尖刺露出地表的完整高度；
        // 仅保留底部 0.02 世界单位，避免底线与地面线完全重合。
        float halfWidth = rowWidth * 0.5f;
        float bottomY = groundSurfaceY + 0.02f;
        float topY = groundSurfaceY + groundSpikeSprite.bounds.size.y *
            Mathf.Abs(groundSpikeScale.y);

        // 画线玩法 DrawGame_TipsLine 配方：周长累计的轴对齐短划 Mesh，
        // 每个完整短划随机选用一张贴图变体，逐变体拆子网格并绑定运行时材质实例。
        BuildWarningRectangleMesh(
            warningObject,
            centerX - halfWidth,
            centerX + halfWidth,
            bottomY,
            topY);

        TextMeshPro warningText = CreateWorldText(
            "WarningExclamation",
            warningObject.transform,
            "!",
            new Vector3(centerX, (bottomY + topY) * 0.5f, 0f),
            Color.black,
            5f,
            Vector3.one,
            hazardSortingOrder + 2,
            true);

        SetWarningAlpha(warningText, 0f);
        yield return FadeWarning(warningText, 0f, 1f, warningFadeInDuration);
        yield return WaitGameplaySeconds(warningVisibleDuration);
        yield return FadeWarning(warningText, 1f, 0f, warningFadeOutDuration);
        DestroyRuntimeObject(warningObject);
    }

    private IEnumerator FadeWarning(
        TextMeshPro text,
        float from,
        float to,
        float duration)
    {
        if (duration <= 0f)
        {
            SetWarningAlpha(text, to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration && !cleanedUp)
        {
            if (!isPaused)
            {
                elapsed += Time.deltaTime;
                SetWarningAlpha(text, Mathf.Lerp(from, to, elapsed / duration));
            }
            yield return null;
        }

        SetWarningAlpha(text, to);
    }

    private void SetWarningAlpha(TextMeshPro text, float alpha)
    {
        Color color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        Material[] lineMaterials = EnsureWarningLineMaterialInstances();
        for (int index = 0; index < lineMaterials.Length; index++)
        {
            Material lineMaterial = lineMaterials[index];
            if (lineMaterial == null)
                continue;

            if (lineMaterial.HasProperty(BaseColorPropertyId))
                lineMaterial.SetColor(BaseColorPropertyId, color);
            if (lineMaterial.HasProperty(ColorPropertyId))
                lineMaterial.SetColor(ColorPropertyId, color);
            if (lineMaterial.HasProperty(RendererColorPropertyId))
                lineMaterial.SetColor(RendererColorPropertyId, color);
        }

        if (text != null)
            text.color = color;
    }

    /// <summary>
    /// 画线玩法 DrawGame_TipsLine 的材质配方：每个贴图变体各复制一份运行时材质实例并
    /// 换绑各自贴图，不修改共享材质资产；无变体时回退为单实例（材质自带贴图）。
    /// 实例统一关闭 alpha clip，让淡入淡出保持平滑透明混合。
    /// </summary>
    private Material[] EnsureWarningLineMaterialInstances()
    {
        if (warningLineMaterialInstances.Length > 0 || warningLineMaterial == null)
            return warningLineMaterialInstances;

        List<Texture2D> variants = new();
        for (int index = 0; index < warningSegmentTextures.Length; index++)
        {
            Texture2D texture = warningSegmentTextures[index];
            if (texture != null && !variants.Contains(texture))
                variants.Add(texture);
        }

        int instanceCount = Mathf.Max(1, variants.Count);
        warningLineMaterialInstances = new Material[instanceCount];
        for (int index = 0; index < instanceCount; index++)
        {
            Material instance = new Material(warningLineMaterial)
            {
                name = $"Level129WarningLineMaterial_{index}",
                hideFlags = HideFlags.DontSave
            };
            // Mirror DrawTipsLineObjectRenderer.ConfigureSmoothAlpha: disable
            // alpha clipping so the fade stays a smooth transparent blend.
            if (instance.HasProperty(AlphaClipPropertyId))
                instance.SetFloat(AlphaClipPropertyId, 0f);
            instance.DisableKeyword(WarningAlphaTestKeyword);

            if (index < variants.Count)
            {
                Texture2D texture = variants[index];
                if (instance.HasProperty(BaseMapPropertyId))
                    instance.SetTexture(BaseMapPropertyId, texture);
                if (instance.HasProperty(MainTexPropertyId))
                    instance.SetTexture(MainTexPropertyId, texture);
            }

            warningLineMaterialInstances[index] = instance;
        }

        return warningLineMaterialInstances;
    }

    private void BuildWarningRectangleMesh(
        GameObject hostObject,
        float leftX,
        float rightX,
        float bottomY,
        float topY)
    {
        Material[] lineMaterials = EnsureWarningLineMaterialInstances();
        if (lineMaterials.Length == 0)
            return;

        if (warningRectangleMesh != null)
            Destroy(warningRectangleMesh);
        warningRectangleMesh = new Mesh
        {
            name = "Level129WarningRectangleMesh",
            hideFlags = HideFlags.DontSave
        };

        Vector3[] corners =
        {
            new Vector3(leftX, bottomY, 0f),
            new Vector3(leftX, topY, 0f),
            new Vector3(rightX, topY, 0f),
            new Vector3(rightX, bottomY, 0f),
            new Vector3(leftX, bottomY, 0f),
        };

        float halfWidth = warningLineWidth * 0.5f;
        float dashLength = 1f / Mathf.Max(0.01f, warningDashTextureRepeat);
        float cycleLength = dashLength + Mathf.Max(0f, warningDashGap);
        List<Vector3> vertices = new();
        List<Vector2> uvs = new();
        List<Color32> colors = new();
        int variantCount = lineMaterials.Length;
        List<int>[] variantTriangles = new List<int>[variantCount];
        for (int variantIndex = 0; variantIndex < variantCount; variantIndex++)
            variantTriangles[variantIndex] = new List<int>();
        Color32 vertexColor = Color.white;
        float accumulatedLength = 0f;
        uint randomState = CreateWarningRandomState(warningSegmentRandomSeed);
        int selectedCycleIndex = -1;
        int selectedTextureIndex = -1;

        for (int edgeIndex = 0; edgeIndex < corners.Length - 1; edgeIndex++)
        {
            Vector3 edgeStart = corners[edgeIndex];
            Vector3 edgeDelta = corners[edgeIndex + 1] - edgeStart;
            float edgeLength = edgeDelta.magnitude;
            if (edgeLength <= 0.0001f)
                continue;

            Vector3 direction = edgeDelta / edgeLength;
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f) * halfWidth;
            float pathStart = accumulatedLength;
            float pathEnd = pathStart + edgeLength;
            // 画线玩法 BuildDiscreteRepeatedSegments：虚线相位沿周长累计并在折角处保持连续。
            int cycleIndex = Mathf.FloorToInt(pathStart / cycleLength);
            float dashPathStart = cycleIndex * cycleLength;
            float dashPathEnd = dashPathStart + dashLength;

            if (dashPathEnd <= pathStart + 0.0001f)
            {
                dashPathStart += cycleLength;
                dashPathEnd += cycleLength;
                cycleIndex++;
            }

            while (dashPathStart < pathEnd)
            {
                float piecePathStart = Mathf.Max(pathStart, dashPathStart);
                float piecePathEnd = Mathf.Min(pathEnd, dashPathEnd);
                if (piecePathEnd - piecePathStart > 0.0001f)
                {
                    // 裁剪出的可见片段按其在整段短划中的比例映射 UV，
                    // 不把整张贴图拉伸进片段；每个完整短划用种子随机挑一个贴图变体。
                    float startU = Mathf.Clamp01((piecePathStart - dashPathStart) / dashLength);
                    float endU = Mathf.Clamp01((piecePathEnd - dashPathStart) / dashLength);
                    int textureIndex = SelectWarningTextureIndexForCycle(
                        cycleIndex,
                        variantCount,
                        ref randomState,
                        ref selectedCycleIndex,
                        ref selectedTextureIndex);
                    AddWarningDashQuad(
                        edgeStart + direction * (piecePathStart - pathStart),
                        edgeStart + direction * (piecePathEnd - pathStart),
                        perpendicular,
                        startU,
                        endU,
                        vertices,
                        uvs,
                        colors,
                        variantTriangles[textureIndex],
                        vertexColor);
                }

                dashPathStart += cycleLength;
                dashPathEnd += cycleLength;
                cycleIndex++;
            }

            accumulatedLength = pathEnd;
        }

        warningRectangleMesh.SetVertices(vertices);
        warningRectangleMesh.SetUVs(0, uvs);
        warningRectangleMesh.SetColors(colors);
        warningRectangleMesh.subMeshCount = variantCount;
        for (int subMeshIndex = 0; subMeshIndex < variantCount; subMeshIndex++)
            warningRectangleMesh.SetTriangles(variantTriangles[subMeshIndex], subMeshIndex, false);
        warningRectangleMesh.RecalculateBounds();

        MeshFilter meshFilter = hostObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = warningRectangleMesh;
        MeshRenderer meshRenderer = hostObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = lineMaterials;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.allowOcclusionWhenDynamic = false;
        meshRenderer.sortingLayerName = SceneSortingLayer;
        meshRenderer.sortingOrder = hazardSortingOrder + 1;
    }

    /// <summary>画线玩法同款 xorshift 种子；保证同 seed 下虚线变体序列可复现。</summary>
    private static uint CreateWarningRandomState(int seed)
    {
        uint state = unchecked((uint)seed) ^ 0x9E3779B9u;
        return state != 0u ? state : 0x6D2B79F5u;
    }

    private static int SelectWarningTextureIndexForCycle(
        int cycleIndex,
        int textureCount,
        ref uint randomState,
        ref int selectedCycleIndex,
        ref int selectedTextureIndex)
    {
        while (selectedCycleIndex < cycleIndex)
        {
            selectedTextureIndex = SelectNextWarningTextureIndex(
                textureCount,
                selectedTextureIndex,
                ref randomState);
            selectedCycleIndex++;
        }

        return selectedTextureIndex;
    }

    private static int SelectNextWarningTextureIndex(
        int textureCount,
        int previousTextureIndex,
        ref uint randomState)
    {
        if (textureCount <= 1)
            return 0;

        if (previousTextureIndex < 0)
            return NextWarningRandomIndex(textureCount, ref randomState);

        int textureIndex = NextWarningRandomIndex(textureCount - 1, ref randomState);
        return textureIndex >= previousTextureIndex
            ? textureIndex + 1
            : textureIndex;
    }

    private static int NextWarningRandomIndex(int exclusiveMaximum, ref uint randomState)
    {
        randomState ^= randomState << 13;
        randomState ^= randomState >> 17;
        randomState ^= randomState << 5;
        return (int)(((ulong)randomState * (uint)exclusiveMaximum) >> 32);
    }

    private static void AddWarningDashQuad(
        Vector3 start,
        Vector3 end,
        Vector3 perpendicular,
        float startU,
        float endU,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<Color32> colors,
        List<int> triangles,
        Color32 vertexColor)
    {
        int baseIndex = vertices.Count;
        vertices.Add(start - perpendicular);
        vertices.Add(start + perpendicular);
        vertices.Add(end + perpendicular);
        vertices.Add(end - perpendicular);
        uvs.Add(new Vector2(startU, 0f));
        uvs.Add(new Vector2(startU, 1f));
        uvs.Add(new Vector2(endU, 1f));
        uvs.Add(new Vector2(endU, 0f));
        colors.Add(vertexColor);
        colors.Add(vertexColor);
        colors.Add(vertexColor);
        colors.Add(vertexColor);
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 3);
    }

    #endregion

    #region Projectile Skills

    private IEnumerator ExecuteFireballSkill()
    {
        if (fireballSprite == null || fireballSpawnPoint == null)
            yield break;

        Vector3 spawnPosition = fireballSpawnPoint.position;
        float startX = spawnPosition.x;
        float y = spawnPosition.y;
        float endX = GetCameraLeftWorldX() - 2f;
        GameObject fireball = new GameObject("Level129Fireball");
        fireball.transform.SetParent(transform, false);
        fireball.transform.position = new Vector3(startX, y, spawnPosition.z);
        fireball.transform.localScale = fireballScale;
        fireball.transform.rotation = Quaternion.Euler(0f, 0f, fireballRotationZ);
        RegisterSkillObject(fireball);

        SpriteRenderer renderer = fireball.AddComponent<SpriteRenderer>();
        ConfigureRenderer(renderer, fireballSprite, hazardSortingOrder);
        if (fireballAnimatorController != null)
        {
            Animator animator = fireball.AddComponent<Animator>();
            animator.runtimeAnimatorController = fireballAnimatorController;
        }

        Collider2D collider = AddPolygonActorTrigger(
            fireball,
            fireballColliderFit,
            ResolveActorMask());
        Level129HazardHitbox hitbox = fireball.AddComponent<Level129HazardHitbox>();
        hitbox.Initialize(this);
        hazardColliders.Add(collider);

        float duration = Mathf.Abs(startX - endX) / Mathf.Max(0.01f, fireballSpeed);
        AudioManager.PlaySound(FireballBurnSoundTag);
        fireball.transform.DOMoveX(endX, duration)
            .SetEase(Ease.Linear)
            .SetTarget(fireball)
            .SetLink(fireball)
            .OnComplete(() => DestroyRuntimeObject(fireball));
        yield return WaitGameplaySeconds(duration);
    }

    private IEnumerator ExecuteFanSpikeSkill()
    {
        if (fanSpikeSprite == null || fanSpikeSpawnPoint == null)
            yield break;

        GameObject rootObject = new GameObject("Level129FanSpikes");
        rootObject.transform.SetParent(transform, false);
        RegisterSkillObject(rootObject);
        int count = Mathf.Max(1, fanSpikeCount);
        int actorMask = ResolveActorMask();
        float maxDuration = fanSpikeTravelDistance / Mathf.Max(0.01f, fanSpikeSpeed);
        Vector3 origin = fanSpikeSpawnPoint.position;
        PlayFanSpikeWhooshSound();

        for (int index = 0; index < count; index++)
        {
            float t = count == 1 ? 0.5f : index / (float)(count - 1);
            float angle = Mathf.Lerp(fanAngleRange.x, fanAngleRange.y, t);
            Vector2 direction = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));

            GameObject spike = new GameObject($"FanSpike_{index + 1:00}");
            spike.transform.SetParent(rootObject.transform, false);
            spike.transform.position = origin;
            spike.transform.localScale = fanSpikeScale;
            spike.transform.rotation = Quaternion.Euler(0f, 0f, angle + 90f);
            SpriteRenderer renderer = spike.AddComponent<SpriteRenderer>();
            ConfigureRenderer(renderer, fanSpikeSprite, hazardSortingOrder);
            Collider2D collider = AddPolygonActorTrigger(
                spike,
                fanSpikeColliderFit,
                actorMask);
            Level129HazardHitbox hitbox = spike.AddComponent<Level129HazardHitbox>();
            hitbox.Initialize(this);
            hazardColliders.Add(collider);

            Vector3 end = origin + (Vector3)(direction * fanSpikeTravelDistance);
            spike.transform.DOMove(end, maxDuration)
                .SetEase(Ease.Linear)
                .SetTarget(spike)
                .SetLink(spike)
                .OnComplete(() => DestroyRuntimeObject(spike));
        }

        yield return WaitGameplaySeconds(maxDuration);
        DestroyRuntimeObject(rootObject);
    }

    private IEnumerator ExecuteRollingStoneSkill(bool isFirstStone, bool waitUntilCleared)
    {
        if (rollingStoneSprite == null)
            yield break;

        float radius = Mathf.Min(
            rollingStoneSprite.bounds.extents.x * Mathf.Abs(rollingStoneScale.x),
            rollingStoneSprite.bounds.extents.y * Mathf.Abs(rollingStoneScale.y));
        float startX = GetCameraRightWorldX() + radius + rollingStoneSpawnMargin;
        float y = groundSurfaceY + radius;
        float recycleX = GetCameraLeftWorldX() - radius - rollingStoneSpawnMargin;

        GameObject stone = new GameObject("Level129RollingStone");
        stone.transform.SetParent(transform, false);
        stone.transform.position = new Vector3(startX, y, 0f);
        stone.transform.localScale = rollingStoneScale;
        RegisterSkillObject(stone);
        if (isFirstStone)
            rollingStoneSpawnAnchorClock = gameplayClock;
        SpriteRenderer renderer = stone.AddComponent<SpriteRenderer>();
        ConfigureRenderer(renderer, rollingStoneSprite, hazardSortingOrder);

        CircleCollider2D collider = stone.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = radius / Mathf.Max(0.0001f, Mathf.Abs(rollingStoneScale.x)) *
                          rollingStoneColliderFit;
        ApplyActorOnlyFilter(collider, ResolveActorMask());
        AddKinematicBody(stone);
        Level129HazardHitbox hitbox = stone.AddComponent<Level129HazardHitbox>();
        hitbox.Initialize(this);
        hazardColliders.Add(collider);

        Level129RollingStoneMotion motion = stone.AddComponent<Level129RollingStoneMotion>();
        motion.Initialize(this, rollingStoneSpeed, radius, recycleX);
        yield return WaitGameplaySeconds(GetRollingStonePostSpawnWait(isFirstStone));

        // 技能门控：滚石完全滚出屏幕被 Level129RollingStoneMotion 回收销毁后，
        // 本技能才算完全释放完，循环才进入冷却与下一个技能。
        if (waitUntilCleared)
        {
            while (stone != null &&
                   !cleanedUp && !bossDefeated && !attemptEnding)
            {
                yield return null;
            }
        }
    }

    #endregion

    #region Meteor Skill

    private IEnumerator ExecuteMeteorWaveSkill()
    {
        if (meteorSprites == null || meteorSprites.Length == 0)
            yield break;

        int groupCount = RandomInclusive(meteorGroupCountRange.x, meteorGroupCountRange.y);
        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            yield return ExecuteMeteorGroup(groupIndex);
            if (groupIndex < groupCount - 1)
                yield return WaitGameplaySeconds(meteorGroupInterval);
        }
    }

    private IEnumerator ExecuteMeteorGroup(int groupIndex)
    {
        int laneCount = Mathf.Max(3, meteorLaneCount);
        List<int> lanes = new();
        for (int index = 0; index < laneCount; index++)
            lanes.Add(index);
        Shuffle(lanes);

        int safeLaneCount = UnityEngine.Random.Range(1, 3);
        safeLaneCount = Mathf.Min(safeLaneCount, laneCount - 1);
        HashSet<int> safeLanes = new();
        for (int index = 0; index < safeLaneCount; index++)
            safeLanes.Add(lanes[index]);

        List<int> attackLanes = new();
        for (int index = 0; index < laneCount; index++)
        {
            if (!safeLanes.Contains(index))
                attackLanes.Add(index);
        }
        Shuffle(attackLanes);

        int meteorCount = Mathf.Clamp(
            RandomInclusive(meteorCountPerGroupRange.x, meteorCountPerGroupRange.y),
            1,
            attackLanes.Count);
        float cumulativeDelay = UnityEngine.Random.Range(
            Mathf.Min(meteorStartDelayRange.x, meteorStartDelayRange.y),
            Mathf.Max(meteorStartDelayRange.x, meteorStartDelayRange.y));
        float groupDuration = 0f;

        for (int index = 0; index < meteorCount; index++)
        {
            int lane = attackLanes[index];
            float laneT = laneCount == 1 ? 0.5f : lane / (float)(laneCount - 1);
            float x = Mathf.Lerp(gameplayWorldXRange.x, gameplayWorldXRange.y, laneT);
            float fallDuration = UnityEngine.Random.Range(
                Mathf.Min(meteorFallDurationRange.x, meteorFallDurationRange.y),
                Mathf.Max(meteorFallDurationRange.x, meteorFallDurationRange.y));
            StartCoroutine(SpawnMeteorWithShadow(x, cumulativeDelay, fallDuration, groupIndex, index));
            groupDuration = Mathf.Max(groupDuration, cumulativeDelay + fallDuration);
            cumulativeDelay += UnityEngine.Random.Range(
                Mathf.Min(meteorStaggerRange.x, meteorStaggerRange.y),
                Mathf.Max(meteorStaggerRange.x, meteorStaggerRange.y));
        }

        yield return WaitGameplaySeconds(groupDuration);
    }

    private IEnumerator SpawnMeteorWithShadow(
        float worldX,
        float spawnDelay,
        float fallDuration,
        int groupIndex,
        int meteorIndex)
    {
        Sprite sprite = meteorSprites[UnityEngine.Random.Range(0, meteorSprites.Length)];
        if (sprite == null)
            yield break;

        GameObject rootObject = new GameObject(
            $"Level129Meteor_{groupIndex + 1:00}_{meteorIndex + 1:00}");
        rootObject.transform.SetParent(transform, false);
        RegisterSkillObject(rootObject);

        GameObject shadowObject = new GameObject("Shadow");
        shadowObject.transform.SetParent(rootObject.transform, false);
        shadowObject.transform.position = new Vector3(worldX, groundSurfaceY + 0.02f, 0f);
        shadowObject.transform.localScale = meteorShadowScale;
        SpriteRenderer shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        ConfigureRenderer(shadowRenderer, meteorShadowSprite, hazardSortingOrder - 1);
        shadowRenderer.color = meteorShadowStartColor;

        GameObject meteorObject = new GameObject("Meteor");
        meteorObject.transform.SetParent(rootObject.transform, false);
        float cameraTop = GetCameraTopWorldY();
        float startY = cameraTop + meteorSpawnAboveCamera;
        float endY = groundSurfaceY - meteorImpactBelowGround;
        meteorObject.transform.position = new Vector3(worldX, startY, 0f);
        float normalizedScale = meteorTargetHeight /
                                Mathf.Max(0.01f, sprite.bounds.size.y);
        meteorObject.transform.localScale = Vector3.one * normalizedScale;
        SpriteRenderer meteorRenderer = meteorObject.AddComponent<SpriteRenderer>();
        ConfigureRenderer(meteorRenderer, sprite, hazardSortingOrder);
        meteorRenderer.enabled = false;
        Collider2D collider = AddPolygonActorTrigger(
            meteorObject,
            meteorColliderFit,
            ResolveActorMask());
        collider.enabled = false;
        Level129HazardHitbox hitbox = meteorObject.AddComponent<Level129HazardHitbox>();
        hitbox.Initialize(this);
        hazardColliders.Add(collider);

        float waitElapsed = 0f;
        while (waitElapsed < spawnDelay && !cleanedUp && rootObject != null)
        {
            if (!isPaused)
                waitElapsed += Time.deltaTime;
            UpdateMeteorShadow(shadowRenderer, waitElapsed / Mathf.Max(0.01f, spawnDelay + fallDuration));
            yield return null;
        }

        if (meteorObject == null)
            yield break;
        meteorRenderer.enabled = true;
        collider.enabled = true;

        float fallElapsed = 0f;
        while (fallElapsed < fallDuration && !cleanedUp && meteorObject != null)
        {
            if (!isPaused)
            {
                fallElapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(fallElapsed / Mathf.Max(0.01f, fallDuration));
                Vector3 position = meteorObject.transform.position;
                position.y = Mathf.Lerp(startY, endY, progress);
                meteorObject.transform.position = position;
                meteorObject.transform.Rotate(0f, 0f, 180f * Time.deltaTime);
                UpdateMeteorShadow(
                    shadowRenderer,
                    (spawnDelay + fallElapsed) / Mathf.Max(0.01f, spawnDelay + fallDuration));
            }
            yield return null;
        }

        if (rootObject != null)
        {
            PlayMeteorBreakEffect(new Vector3(worldX, groundSurfaceY, 0f));
            AudioManager.PlaySound("flyRocketExplosion");
            DestroyRuntimeObject(rootObject);
        }
    }

    private void UpdateMeteorShadow(SpriteRenderer renderer, float progress)
    {
        if (renderer == null)
            return;

        float t = Mathf.Clamp01(progress);
        renderer.color = Color.Lerp(meteorShadowStartColor, meteorShadowEndColor, t);
        renderer.transform.localScale = Vector3.Lerp(
            meteorShadowScale * 0.75f,
            meteorShadowScale,
            t);
    }

    #endregion

    #region Damage, Defeat And Door

    internal bool TryCommitHazardHit(ActorCtrl hitActor, Vector3 hitPosition)
    {
        if (cleanedUp || attemptEnding || actor == null || hitActor != actor || playerDeathCommitted || actor.IsDied)
            return false;

        playerDeathCommitted = true;
        HideDefeatedBanner();
        failurePresentationElapsed = 0f;
        failureDeferred = arrowsInFlight > 0 || (bossDefeated && !exitDoorLanded);
        SetAimButtonVisible(false);
        HideReticleImmediately(true);
        StopBattleSound(false);

        GetTopSortingSettings(
            actor.transform,
            SortingLayer.NameToID(SceneSortingLayer),
            200,
            out int actorSortingLayerId,
            out int actorSortingOrder);
        PlayCommonHitEffect(
            hitPosition,
            actorSortingLayerId,
            actorSortingOrder,
            "Level129PlayerHitEffect");

        AudioManager.PlaySound("trap");
        AudioManager.PlaySound("failed");
        this.DispatchEvent(Witness<OnTriggerTrapEvent>._);

        return true;
    }

    private void DetectExternalActorDeath()
    {
        if (actor == null || !actor.IsDied || playerDeathCommitted)
            return;

        playerDeathCommitted = true;
        HideDefeatedBanner();
        failurePresentationElapsed = 0f;
        failureDeferred = arrowsInFlight > 0 || (bossDefeated && !exitDoorLanded);
        SetAimButtonVisible(false);
        HideReticleImmediately(true);
        StopBattleSound(false);
    }

    private void RefreshFailureDeferralAfterArrowResolution()
    {
        if (!playerDeathCommitted)
            return;

        // Keep the gate armed until Update explicitly wakes BaseLevelCtrl.
        // Its original delayed CheckFinished may already have run while this
        // arrow was still in flight.
        failureDeferred = true;
    }

    private void TryFinalizeDeferredFailure()
    {
        if (!playerDeathCommitted || !failureDeferred)
            return;

        bool presentationReady = failurePresentationElapsed >= minimumDeathPresentationSeconds;
        bool arrowsResolved = arrowsInFlight <= 0;
        bool doorReady = !bossDefeated || exitDoorLanded;
        if (!presentationReady || !arrowsResolved || !doorReady)
            return;

        failureDeferred = false;
        levelCtrl?.NotifyDeferredFailureReady();
    }

    private void BeginBossDefeat(bool healthBarShattered)
    {
        if (bossDefeated)
            return;

        bossDefeated = true;
        waitingForHealthBarShatter = healthBarShattered;
        bossFadeCompleted = false;
        StopBattleSound(!playerDeathCommitted);
        SetAimButtonVisible(false);
        FadeOutReticle();
        if (playerDeathCommitted)
            failureDeferred = true;
        StartCoroutine(BossDefeatFlow(healthBarShattered));
    }

    private IEnumerator BossDefeatFlow(bool healthBarShattered)
    {
        if (healthBarShattered)
        {
            float fragmentDuration = Mathf.Max(0f, fragmentLifetime);
            float fadeLead = Mathf.Clamp(
                Mathf.Max(0f, bossFadeLeadAfterHealthBarShatter),
                0f,
                fragmentDuration);
            yield return WaitGameplaySeconds(fadeLead);
            if (cleanedUp)
                yield break;

            StartCoroutine(FadeBossToCompletion());
            yield return WaitGameplaySeconds(fragmentDuration - fadeLead);

            TrackEntry currentBossEntry = bossSkeleton?.AnimationState?.GetCurrent(0);
            if (currentBossEntry?.Animation?.Name != BossWarningAnimation &&
                bossSkeleton?.AnimationState != null)
                bossSkeleton.AnimationState.SetAnimation(0, BossIdleAnimation, true);
            waitingForHealthBarShatter = false;
        }
        else
        {
            yield return FadeBossToCompletion();
        }

        while (!bossFadeCompleted && !cleanedUp)
            yield return null;

        if (cleanedUp)
            yield break;

        ClearActiveSkillObjects();
        ShowDefeatedBanner();
        yield return DropExitDoor();
    }

    private IEnumerator FadeBossToCompletion()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, bossFadeDuration);
        while (elapsed < duration && !cleanedUp)
        {
            if (!isPaused)
            {
                elapsed += Time.deltaTime;
                ApplyBossFade(1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
            }
            yield return null;
        }

        if (cleanedUp)
            yield break;

        ApplyBossFade(0f);
        bossFadeCompleted = true;
        if (healthBarRoot != null)
            healthBarRoot.gameObject.SetActive(false);
    }

    private void ApplyBossFade(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        if (bossSkeleton?.Skeleton != null)
            bossSkeleton.Skeleton.A = alpha;

        if (healthBackgroundRenderer != null)
        {
            Color color = healthBackgroundInitialColor;
            color.a *= alpha;
            healthBackgroundRenderer.color = color;
        }

        if (healthFillRenderer != null)
        {
            Color color = healthFillInitialColor;
            color.a *= alpha;
            healthFillRenderer.color = color;
        }

        if (hpText != null)
        {
            Color color = hpTextInitialColor;
            color.a *= alpha;
            hpText.color = color;
        }
    }

    private IEnumerator DropExitDoor()
    {
        if (exitDoor == null)
            yield break;

        float elapsed = 0f;
        float startY = exitDoor.position.y;
        float duration = Mathf.Max(0.01f, exitDoorDropDuration);
        while (elapsed < duration && !cleanedUp)
        {
            if (!isPaused)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = progress * progress;
                Vector3 position = exitDoor.position;
                position.y = Mathf.Lerp(startY, exitDoorLandingPosition.y, eased);
                exitDoor.position = position;
            }
            yield return null;
        }

        exitDoor.position = exitDoorLandingPosition;
        exitDoorLanded = true;
        AudioManager.PlaySound("blockByDoor");
        SetExitDoorCollidersEnabled(true);
        TryFinalizeDeferredFailure();
    }

    private void DetectAcceptedSuccess()
    {
        if (attemptEnding || playerDeathCommitted || actor == null || actor.IsDied || levelCtrl == null)
            return;

        if (!levelCtrl.IsActorEnterDoor)
            return;

        attemptEnding = true;
        StopBattleSound(true);
        HideDefeatedBanner();
        SetAimButtonVisible(false);
        HideReticleImmediately(true);
        SetHazardCollidersEnabled(false);
    }

    private void SetExitDoorCollidersEnabled(bool enabled)
    {
        for (int index = 0; index < exitDoorColliders.Length; index++)
        {
            if (exitDoorColliders[index] != null)
                exitDoorColliders[index].enabled = enabled;
        }
    }

    #endregion

    #region Health Bar And Fragments

    private void SetHealthFillSprite(Sprite sprite)
    {
        if (sprite == null || healthFillRenderer == null)
            return;

        activeHealthFillSprite = sprite;
        healthFillRenderer.sprite = sprite;
        UpdateHealthBarVisual();
    }

    private void UpdateHealthBarVisual()
    {
        if (healthFillRenderer == null || baseHealthFillSprite == null || activeHealthFillSprite == null)
            return;

        float fillAmount = Mathf.Clamp01(bossHp / (float)Mathf.Max(1, bossMaxHp));
        float baseWidth = baseHealthFillSprite.bounds.size.x;
        float baseHeight = baseHealthFillSprite.bounds.size.y;
        float spriteWidth = Mathf.Max(0.0001f, activeHealthFillSprite.bounds.size.x);
        float spriteHeight = Mathf.Max(0.0001f, activeHealthFillSprite.bounds.size.y);
        float fitX = baseWidth / spriteWidth;
        float fitY = baseHeight / spriteHeight;
        healthFillRenderer.transform.localScale = new Vector3(
            fitX * fillAmount,
            fitY,
            1f);
        healthFillRenderer.transform.localPosition = new Vector3(
            -baseWidth * 0.5f + spriteWidth * fitX * fillAmount * 0.5f,
            0f,
            -0.01f);

        if (hpText != null)
            hpText.text = $"HP: {bossHp}";
    }

    private void SpawnHealthBarFragments()
    {
        if (healthBarFragmentSprites == null || healthBarRoot == null)
            return;

        Vector3 origin = healthBarHitPoint != null
            ? healthBarHitPoint.position
            : healthBarRoot.position;
        for (int index = 0; index < healthBarFragmentSprites.Length; index++)
        {
            Sprite sprite = healthBarFragmentSprites[index];
            if (sprite == null)
                continue;

            GameObject piece = new GameObject($"Level129HealthBarFragment_{index + 1:00}");
            piece.transform.SetParent(transform, false);
            piece.transform.position = origin;
            piece.transform.localScale = fragmentScale;
            RegisterRuntimeObject(piece);
            SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();
            ConfigureRenderer(renderer, sprite, GetHealthFragmentSortingOrder());

            float horizontal = UnityEngine.Random.Range(
                Mathf.Min(fragmentHorizontalSpeedRange.x, fragmentHorizontalSpeedRange.y),
                Mathf.Max(fragmentHorizontalSpeedRange.x, fragmentHorizontalSpeedRange.y));
            horizontal *= UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float vertical = UnityEngine.Random.Range(
                Mathf.Min(fragmentVerticalSpeedRange.x, fragmentVerticalSpeedRange.y),
                Mathf.Max(fragmentVerticalSpeedRange.x, fragmentVerticalSpeedRange.y));
            float angular = UnityEngine.Random.Range(
                Mathf.Min(fragmentAngularSpeedRange.x, fragmentAngularSpeedRange.y),
                Mathf.Max(fragmentAngularSpeedRange.x, fragmentAngularSpeedRange.y));

            Level129FragmentPiece fragment = piece.AddComponent<Level129FragmentPiece>();
            fragment.Initialize(
                this,
                renderer,
                new Vector2(horizontal, vertical),
                fragmentGravity,
                angular,
                fragmentLifetime,
                fragmentFadeDuration);
        }
    }

    #endregion

    #region Runtime Helpers

    internal void DestroyRuntimeObject(GameObject runtimeObject)
    {
        if (runtimeObject == null)
            return;

        DOTween.Kill(runtimeObject);
        runtimeObjects.Remove(runtimeObject);
        activeSkillObjects.Remove(runtimeObject);
        if (activeImpactArrow == runtimeObject)
            activeImpactArrow = null;
        Destroy(runtimeObject);
    }

    private void RegisterRuntimeObject(GameObject runtimeObject)
    {
        if (runtimeObject != null && !runtimeObjects.Contains(runtimeObject))
            runtimeObjects.Add(runtimeObject);
    }

    private void RegisterSkillObject(GameObject skillObject)
    {
        RegisterRuntimeObject(skillObject);
        if (skillObject != null && !activeSkillObjects.Contains(skillObject))
            activeSkillObjects.Add(skillObject);
    }

    private void ClearActiveSkillObjects()
    {
        if (bossSkillLoop != null)
        {
            StopCoroutine(bossSkillLoop);
            bossSkillLoop = null;
        }

        SetHazardCollidersEnabled(false);
        for (int index = activeSkillObjects.Count - 1; index >= 0; index--)
        {
            GameObject skillObject = activeSkillObjects[index];
            if (skillObject == null)
                continue;

            // Destroy 延迟到帧末，先隐藏对象，避免 Mission Complete 当帧仍残留技能画面。
            skillObject.SetActive(false);
            DestroyRuntimeObject(skillObject);
        }

        activeSkillObjects.Clear();
        hazardColliders.Clear();
    }

    private void GetArrowTargetTopSortingSettings(
        ArrowTarget target,
        out int sortingLayerId,
        out int sortingOrder)
    {
        Transform targetRoot = target == ArrowTarget.HealthBar && healthBarRoot != null
            ? healthBarRoot
            : bossRoot;
        int fallbackLayerId = bossSortingGroup != null
            ? bossSortingGroup.sortingLayerID
            : SortingLayer.NameToID(SceneSortingLayer);
        int fallbackOrder = bossSortingGroup != null ? bossSortingGroup.sortingOrder : 205;
        GetTopSortingSettings(
            targetRoot,
            fallbackLayerId,
            fallbackOrder,
            out sortingLayerId,
            out sortingOrder);
    }

    private static void GetTopSortingSettings(
        Transform targetRoot,
        int fallbackLayerId,
        int fallbackOrder,
        out int sortingLayerId,
        out int sortingOrder)
    {
        sortingLayerId = fallbackLayerId;
        sortingOrder = fallbackOrder;
        int topLayerValue = SortingLayer.GetLayerValueFromID(sortingLayerId);
        if (targetRoot == null)
            return;

        SortingGroup[] sortingGroups = targetRoot.GetComponentsInChildren<SortingGroup>(true);
        for (int index = 0; index < sortingGroups.Length; index++)
        {
            SortingGroup group = sortingGroups[index];
            if (group == null)
                continue;

            IncludeSortingCandidate(
                group.sortingLayerID,
                group.sortingOrder,
                ref sortingLayerId,
                ref sortingOrder,
                ref topLayerValue);
        }

        Renderer[] renderers = targetRoot.GetComponentsInChildren<Renderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null)
                continue;

            IncludeSortingCandidate(
                renderer.sortingLayerID,
                renderer.sortingOrder,
                ref sortingLayerId,
                ref sortingOrder,
                ref topLayerValue);
        }
    }

    private static void IncludeSortingCandidate(
        int candidateLayerId,
        int candidateOrder,
        ref int sortingLayerId,
        ref int sortingOrder,
        ref int topLayerValue)
    {
        int candidateLayerValue = SortingLayer.GetLayerValueFromID(candidateLayerId);
        if (candidateLayerValue < topLayerValue)
            return;

        if (candidateLayerValue > topLayerValue)
        {
            sortingLayerId = candidateLayerId;
            sortingOrder = candidateOrder;
            topLayerValue = candidateLayerValue;
            return;
        }

        sortingOrder = Mathf.Max(sortingOrder, candidateOrder);
    }

    private int GetHealthFragmentSortingOrder()
    {
        return healthFillRenderer != null ? healthFillRenderer.sortingOrder + 2 : 223;
    }

    private void CacheBowSortingOrders()
    {
        bowInitialSortingOrders.Clear();
        bowInitialColors.Clear();
        bowInitialSortingOrderBase = int.MaxValue;
        if (bowVisual == null)
            return;

        SpriteRenderer[] renderers = bowVisual.GetComponentsInChildren<SpriteRenderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];
            if (renderer == null)
                continue;

            bowInitialSortingOrders[renderer] = renderer.sortingOrder;
            bowInitialColors[renderer] = renderer.color;
            bowInitialSortingOrderBase = Mathf.Min(
                bowInitialSortingOrderBase,
                renderer.sortingOrder);
        }

        if (bowInitialSortingOrderBase == int.MaxValue)
            bowInitialSortingOrderBase = bowCarriedSortingOrder;
    }

    private void SetBowCarriedSortingOrder()
    {
        foreach (KeyValuePair<SpriteRenderer, int> pair in bowInitialSortingOrders)
        {
            if (pair.Key != null)
                pair.Key.sortingOrder = bowCarriedSortingOrder +
                    pair.Value - bowInitialSortingOrderBase;
        }
    }

    private void RestoreBowAuthoredState()
    {
        if (bowVisual == null)
            return;

        if (bowInitialParent != null)
            bowVisual.SetParent(bowInitialParent, false);
        bowVisual.localPosition = bowInitialLocalPosition;
        bowVisual.localRotation = bowInitialLocalRotation;
        bowVisual.localScale = bowInitialLocalScale;
        foreach (KeyValuePair<SpriteRenderer, int> pair in bowInitialSortingOrders)
        {
            if (pair.Key != null)
                pair.Key.sortingOrder = pair.Value;
        }
        RestoreBowColors();
    }

    private void HideBowForShot()
    {
        KillBowRecoverySequence();
        RestoreBowColors();
        if (bowVisual != null)
            bowVisual.gameObject.SetActive(false);
    }

    private void BeginBowRecovery()
    {
        if (playerDeathCommitted || attemptEnding || bowVisual == null)
            return;

        KillBowRecoverySequence();
        bowVisual.gameObject.SetActive(true);
        if (shotRecoveryDuration <= 0f || bowInitialColors.Count == 0)
        {
            RestoreBowColors();
            return;
        }

        DG.Tweening.Sequence sequence = DOTween.Sequence().SetLink(bowVisual.gameObject);
        bowRecoverySequence = sequence;
        foreach (KeyValuePair<SpriteRenderer, Color> pair in bowInitialColors)
        {
            if (pair.Key == null)
                continue;

            Color transparent = pair.Value;
            transparent.a = 0f;
            pair.Key.color = transparent;
            sequence.Join(pair.Key.DOFade(pair.Value.a, shotRecoveryDuration));
        }

        sequence.OnComplete(() =>
        {
            if (bowRecoverySequence != sequence)
                return;
            bowRecoverySequence = null;
            RestoreBowColors();
        });
    }

    private void CompleteBowRecovery()
    {
        if (playerDeathCommitted || attemptEnding || bowVisual == null)
            return;

        KillBowRecoverySequence();
        bowVisual.gameObject.SetActive(true);
        RestoreBowColors();
    }

    private void ShowBowImmediately()
    {
        KillBowRecoverySequence();
        if (bowVisual != null)
            bowVisual.gameObject.SetActive(true);
        RestoreBowColors();
    }

    private void RestoreBowColors()
    {
        foreach (KeyValuePair<SpriteRenderer, Color> pair in bowInitialColors)
        {
            if (pair.Key != null)
                pair.Key.color = pair.Value;
        }
    }

    private void KillBowRecoverySequence()
    {
        if (bowRecoverySequence == null)
            return;

        bowRecoverySequence.Kill();
        bowRecoverySequence = null;
    }

    private SpriteRenderer CreateSpriteRenderer(
        string objectName,
        Transform parent,
        Sprite sprite,
        int sortingOrder,
        Vector3 localPosition,
        Vector3 localScale)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = localScale;
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        ConfigureRenderer(renderer, sprite, sortingOrder);
        return renderer;
    }

    private static void ConfigureRenderer(
        SpriteRenderer renderer,
        Sprite sprite,
        int sortingOrder)
    {
        renderer.sprite = sprite;
        renderer.sortingLayerName = SceneSortingLayer;
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.white;
    }

    private TextMeshPro CreateWorldText(
        string objectName,
        Transform parent,
        string content,
        Vector3 worldPosition,
        Color color,
        float fontSize,
        Vector3 localScale,
        int sortingOrder,
        bool useWorldPosition)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        if (useWorldPosition)
            textObject.transform.position = worldPosition;
        else
            textObject.transform.localPosition = worldPosition;
        textObject.transform.localScale = localScale;
        TextMeshPro text = textObject.AddComponent<TextMeshPro>();
        text.font = hpFont;
        text.fontSize = fontSize;
        text.color = color;
        text.text = content;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.rectTransform.sizeDelta = new Vector2(100f, 100f);
        Renderer textRenderer = text.GetComponent<Renderer>();
        if (textRenderer != null)
        {
            textRenderer.sortingLayerName = SceneSortingLayer;
            textRenderer.sortingOrder = sortingOrder;
        }
        return text;
    }

    private Collider2D AddPolygonActorTrigger(
        GameObject target,
        float fit,
        int actorMask)
    {
        PolygonCollider2D collider = target.AddComponent<PolygonCollider2D>();
        collider.isTrigger = true;
        fit = Mathf.Clamp(fit, 0.5f, 1f);
        for (int pathIndex = 0; pathIndex < collider.pathCount; pathIndex++)
        {
            Vector2[] points = collider.GetPath(pathIndex);
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
                points[pointIndex] *= fit;
            collider.SetPath(pathIndex, points);
        }

        ApplyActorOnlyFilter(collider, actorMask);
        AddKinematicBody(target);
        return collider;
    }

    private static void AddKinematicBody(GameObject target)
    {
        if (target.GetComponent<Rigidbody2D>() != null)
            return;

        Rigidbody2D body = target.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private static void ApplyActorOnlyFilter(Collider2D collider, int actorMask)
    {
        if (collider == null || actorMask == 0)
            return;

        collider.includeLayers = actorMask;
        collider.excludeLayers = ~actorMask;
    }

    private int ResolveActorMask()
    {
        int actorLayer = LayerMask.NameToLayer("Actor");
        return actorLayer >= 0 ? 1 << actorLayer : 0;
    }

    private float GetCameraLeftWorldX()
    {
        if (worldCamera == null || !worldCamera.orthographic)
            return -10.5f;
        return worldCamera.transform.position.x -
               worldCamera.orthographicSize * worldCamera.aspect;
    }

    private float GetCameraRightWorldX()
    {
        if (worldCamera == null || !worldCamera.orthographic)
            return 10.5f;
        return worldCamera.transform.position.x +
               worldCamera.orthographicSize * worldCamera.aspect;
    }

    private float GetCameraTopWorldY()
    {
        if (worldCamera == null || !worldCamera.orthographic)
            return 5.9f;
        return worldCamera.transform.position.y + worldCamera.orthographicSize;
    }

    private IEnumerator WaitGameplaySeconds(float seconds)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, seconds);
        while (elapsed < duration && !cleanedUp)
        {
            if (!isPaused)
                elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static int RandomInclusive(int first, int second)
    {
        int min = Mathf.Min(first, second);
        int max = Mathf.Max(first, second);
        return UnityEngine.Random.Range(min, max + 1);
    }

    private static void Shuffle<T>(IList<T> values)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int swap = UnityEngine.Random.Range(0, index + 1);
            (values[index], values[swap]) = (values[swap], values[index]);
        }
    }

    private void SetHazardCollidersEnabled(bool enabled)
    {
        for (int index = 0; index < hazardColliders.Count; index++)
        {
            if (hazardColliders[index] != null)
                hazardColliders[index].enabled = enabled;
        }
    }

    #endregion

    #region Events And Cleanup

    private void BindEvents()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        subscriber.Subscribe<GameResumeEvent>(OnGameResume);
        subscriber.Subscribe<GameResetEvent>(OnGameReset);
        subscriber.Subscribe<OnTriggerTrapEvent>(OnExternalTrapSignal);
        subscriber.Subscribe<OnTriggerFailAreaEvent>(OnExternalFailAreaSignal);
    }

    private void OnExternalTrapSignal(OnTriggerTrapEvent _)
    {
        MarkExternalFailureSignal();
    }

    private void OnExternalFailAreaSignal(OnTriggerFailAreaEvent _)
    {
        MarkExternalFailureSignal();
    }

    private void MarkExternalFailureSignal()
    {
        if (playerDeathCommitted)
            return;

        playerDeathCommitted = true;
        HideDefeatedBanner();
        failurePresentationElapsed = 0f;
        failureDeferred = arrowsInFlight > 0 || (bossDefeated && !exitDoorLanded);
        SetAimButtonVisible(false);
        HideReticleImmediately(true);
        StopBattleSound(false);
    }

    private void OnGamePause(GamePauseEvent _)
    {
        isPaused = true;
        actor?.SetArcheryPaused(true);
        if (bossSkeleton != null)
            bossSkeleton.timeScale = 0f;
        SetAimButtonVisible(false);
        HideReticleImmediately(false);
        bowRecoverySequence?.Pause();
        PauseRuntimeTweens(true);
    }

    private void OnGameResume(GameResumeEvent _)
    {
        isPaused = false;
        actor?.SetArcheryPaused(false);
        if (bossSkeleton != null)
            bossSkeleton.timeScale = 1f;
        PauseRuntimeTweens(false);
        bowRecoverySequence?.Play();
        SetAimButtonVisible(bowEquipped && !bossDefeated && !playerDeathCommitted && !attemptEnding);
        if (reticleSnapped)
            UpdateSnappedReticlePosition();
    }

    private void OnGameReset(GameResetEvent _)
    {
        StopBattleSound(true);
        attemptEnding = true;
        HideDefeatedBanner();
        SetAimButtonVisible(false);
        HideReticleImmediately(true);
        SetHazardCollidersEnabled(false);
    }

    private void PauseRuntimeTweens(bool pause)
    {
        if (pause)
            DOTween.Pause(gameObject);
        else
            DOTween.Play(gameObject);

        if (bowPickupRoot != null)
        {
            if (pause)
                DOTween.Pause(bowPickupRoot.gameObject);
            else
                DOTween.Play(bowPickupRoot.gameObject);
        }

        for (int index = 0; index < runtimeObjects.Count; index++)
        {
            GameObject runtimeObject = runtimeObjects[index];
            if (runtimeObject == null)
                continue;
            if (pause)
                DOTween.Pause(runtimeObject);
            else
                DOTween.Play(runtimeObject);

            Animator[] animators = runtimeObject.GetComponentsInChildren<Animator>(true);
            for (int animatorIndex = 0; animatorIndex < animators.Length; animatorIndex++)
            {
                if (animators[animatorIndex] != null)
                    animators[animatorIndex].speed = pause ? 0f : 1f;
            }
        }
    }

    private void CleanupRuntime()
    {
        if (cleanedUp)
            return;

        cleanedUp = true;
        HideDefeatedBanner();
        EventsUtils.ResetEvents(ref subscriber);
        StopBattleSound(!playerDeathCommitted);
        StopReticleFade();
        StopAllCoroutines();
        DOTween.Kill(gameObject);
        if (bowPickupRoot != null)
            DOTween.Kill(bowPickupRoot.gameObject);
        KillBowRecoverySequence();
        aimDragHandle?.Detach();
        actor?.CancelLevel129Action(false);
        if (arrowWhooshSource != null)
            arrowWhooshSource.Stop();
        if (fanSpikeWhooshSource != null)
            fanSpikeWhooshSource.Stop();

        for (int index = runtimeObjects.Count - 1; index >= 0; index--)
        {
            GameObject runtimeObject = runtimeObjects[index];
            if (runtimeObject == null)
                continue;
            DOTween.Kill(runtimeObject);
            Destroy(runtimeObject);
        }
        runtimeObjects.Clear();
        activeSkillObjects.Clear();
        hazardColliders.Clear();
        if (warningRectangleMesh != null)
            Destroy(warningRectangleMesh);
        for (int index = 0; index < warningLineMaterialInstances.Length; index++)
        {
            if (warningLineMaterialInstances[index] != null)
                Destroy(warningLineMaterialInstances[index]);
        }
        warningLineMaterialInstances = Array.Empty<Material>();
    }

    #endregion
}

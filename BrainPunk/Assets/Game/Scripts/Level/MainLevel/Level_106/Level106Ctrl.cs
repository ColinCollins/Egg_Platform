using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using I2.Loc;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

/// <summary>
/// Level 106 UFO 送层编排器。
/// 玩家在真实地面上到达视觉地毯中心后自动开始送层；第 5 层固定为钥匙小球，
/// 其余层按重复洗牌后的素材顺序送出。钥匙解锁右移，玩家登上 UFO 后停止送层并升顶。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level106Ctrl : MonoBehaviour, IEventSender
{
    private const string RemainingLayersParameter = "remainingLayers";
    private const string RemainingLayersFallbackPrefix = "\u8FD8\u5DEE";
    private const string RemainingLayersFallbackSuffix = "\u5C42\u767B\u9876";
    private const int TotalLayers = 999;
    private const int KeyLayerNumber = 5;
    private const int AccelerationStartLayer = 5;
    private const float StackOverlapY = 0.4f;
    private const string UfoPassengerSortingLayerName = "Default";
    private const int UfoPassengerSortingOrder = 4;
    private static readonly Vector2 DeliveryHitKnockbackDirection = new Vector2(-0.5f, 0.5f);
    private const float DeliveryHitKnockbackForce = 50f;
    private const float DeliveryHitRotationDuration = 0.5f;
    private static readonly Vector2 InitialCameraFollowAreaCenter = new Vector2(-0.5f, -0.351f);
    private static readonly Vector2 InitialCameraFollowAreaSize = new Vector2(17f, 0.01f);
    private static readonly Vector2 BoardingCameraFollowAreaCenter = new Vector2(-3.5f, -0.351f);
    private static readonly Vector2 BoardingCameraFollowAreaSize = new Vector2(11f, 0.01f);

    private enum MoveDirection
    {
        None,
        Left,
        Right
    }

    [Serializable]
    private sealed class LayerVisualProfile
    {
        public Sprite sprite;
        [Min(0.01f)] public float scale = 1f;
        public Vector3 carryOffset;
        public float rotationZ;
        public Vector3 stackOffset;
        [Min(1f)] public float colliderWidthMultiplier = 1f;
    }

    [Header("关卡引用")]
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private Transform ufoRoot;
    [SerializeField] private Collider2D ufoBodyCollider;
    [SerializeField] private Level106UfoCollision ufoCollision;
    [SerializeField] private Transform carryAnchor;
    [SerializeField] private Transform triggerStage;
    [SerializeField] private Transform stageSupport;
    [SerializeField] private Collider2D exitPlatformCollider;
    [SerializeField] private Transform inducedExitPlatform;
    [SerializeField] private SuccessAnimCtrl inducedExitDoor;
    [SerializeField] private SuccessAnimCtrl ufoExitDoor;
    [SerializeField] private Level106ClimbLoop climbLoop;
    [SerializeField] private Transform stackRoot;
    [SerializeField] private TMP_Text remainingLayersText;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Level106MoveButtonLock moveButtonLockPrefab;
    [Tooltip("移动键锁 Spine 可视内容相对按钮中心的 UI 补偿。")]
    [SerializeField] private Vector2 moveButtonLockAnchoredOffset = new Vector2(0f, -118f);
    [SerializeField] private Level106BoxBadCtrl boxBadCtrl;

    [Header("自动真实提示弹窗")]
    private Level106RealTipsPopup realTipsPopup;
    private bool realTipsPopupTriggered;
    private bool realTipsInputLock;

    [Header("送层素材")]
    [Tooltip("除第 5 层外的随机素材池；数组中的重复项保留为独立洗牌项。")]
    [SerializeField] private Sprite[] randomLayerSprites;
    [SerializeField] private Sprite keyBallSprite;
    [Tooltip("第 5 层小球内部可单独拖拽的移动钥匙视觉。")]
    [SerializeField] private Sprite moveKeySprite;
    [SerializeField] private Vector3 moveKeyLocalPosition = new Vector3(0f, 0.1f, -0.02f);
    [SerializeField, Min(0.01f)] private float moveKeyScale = 0.5f;
    [SerializeField] private float moveKeyRotation;
    [Tooltip("Sp_mcqh_1_03 使用时横向摆放并缩小。")]
    [SerializeField] private Sprite horizontalLayerSprite;
    [SerializeField, Min(0.01f)] private float horizontalLayerScale = 0.6f;

    [Header("送层节奏")]
    [Tooltip("UFO 将物件向左送到舞台中心的基准时长。")]
    [SerializeField, Min(0.01f)] private float ufoMoveDuration = 1.5f;
    [Tooltip("玩家踩下物件后，UFO 向右返回待机位置的基准时长。")]
    [SerializeField, Min(0.01f)] private float ufoReturnDuration = 1.6f;
    [Tooltip("玩家踩下送层物件后，物件从当前踩踏位置下压到堆叠位置的时长。")]
    [SerializeField, Min(0.01f)] private float itemTravelDuration = 0.12f;
    [SerializeField, Min(0f)] private float firstLayerInterval = 0.8f;
    [SerializeField, Min(0f)] private float finalLayerInterval = 0.4f;
    [Tooltip("第 6 层起，每层送层速度倍率增加的数值。")]
    [SerializeField, Min(0f)] private float layerSpeedIncrement = 0.4f;
    [Tooltip("第 6 层起送层速度倍率的上限。")]
    [SerializeField, Min(1f)] private float maxLayerSpeedMultiplier = 10f;
    [Header("舞台中心锁定")]
    [Tooltip("角色从地毯左侧沿真实地面接近视觉中心时锁住横向输入，并自动开始首层送物。")]
    [SerializeField, Min(0f)] private float stageCenterLockTolerance = 0.08f;
    [Tooltip("每个送层素材独立的大小、夹持偏移、旋转和落定偏移；未配置素材使用默认值。")]
    [SerializeField] private LayerVisualProfile[] layerVisualProfiles;
    [SerializeField] private string carrySortingLayerName = "Default";
    [SerializeField] private int carrySortingOrder = 12;
    [SerializeField] private string stackedSortingLayerName = "SceneLayer";
    [SerializeField, Min(0)] private int layerSortingOrder = 202;

    [Header("UFO 登乘与上升")]
    [SerializeField, Min(0.01f)] private float ascentSpeed = 2.7f;
    [Tooltip("Actor 主碰撞体中心与 UFOBody Collider 水平中心的最大登乘距离；命中后校正到精确中心。")]
    [SerializeField, Min(0f)] private float boardingCenterTolerance = 0.1f;
    [SerializeField, Min(0.01f)] private float boardingHoldDuration = 1f;
    [SerializeField, Min(0.01f)] private float finalAscentDuration = 5f;
    [SerializeField, Min(1)] private int remainingLayersPulseCount = 12;

    private EventSubscriber eventSubscriber;
    private Sequence activeSequence;
    private Tween remainingLayersCountTween;
    private DOTweenTMPAnimator remainingLayersAnimator;
    private LocalizationParamsManager remainingLayersParamsManager;
    private readonly List<GameObject> stackedObjects = new List<GameObject>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private List<Sprite> layerSequence;
    private Level106MoveKeyDrag moveKeyDrag;
    private RectTransform leftMoveButtonRect;
    private RectTransform rightMoveButtonRect;
    private Level106MoveButtonLock leftMoveLock;
    private Level106MoveButtonLock rightMoveLock;
    private Collider2D actorCollider;
    private Rigidbody2D actorBody;
    private Level106ActorCtrl boardingActor;
    private Level106CameraFollowArea cameraFollowArea;
    private Tween actorDeliveryRotationTween;
    private Quaternion actorDeliveryKnockbackStartRotation;
    private bool actorDeliveryKnockbackActive;
    private GameObject actorShadow;
    private ShadowController actorShadowController;
    private bool actorShadowInitialActive;
    private bool actorShadowControllerInitialEnabled;
    private bool actorShadowSuppressed;
    private readonly Dictionary<SpriteRenderer, bool> actorShadowRendererStates = new Dictionary<SpriteRenderer, bool>();
    private Transform actorInitialParent;
    private RigidbodyType2D actorInitialBodyType;
    private float actorInitialGravityScale;
    private bool actorInitialSimulated;
    private RigidbodyInterpolation2D actorInitialInterpolation;
    private SortingGroup actorSortingGroup;
    private int actorSortingLayerIdBeforeUfo;
    private int actorSortingOrderBeforeUfo;
    private bool actorSortingCapturedForUfo;
    private bool actorAttachedToUfo;
    private bool actorUfoPhysicsCaptured;
    private Collider2D ufoRodCollider;
    private Collider2D stageSupportCollider;
    private Vector3 initialUfoPosition;
    private Vector3 deliveryRestPosition;
    private Vector3 lastUfoPosition;
    private float stackTopY;
    private float ascentTargetY;
    private float finalAscentRootY;
    private float deliveryWorkY;
    private float lastSettledLayerSceneHeight;
    private int completedLayers;
    private bool remainingLayersReachedTop;
    private MoveDirection unlockedMoveDirection;
    private bool leftMoveUnlocked;
    private bool boardingCameraFollowActivated;
    private bool hasStarted;
    private bool boardingReady;
    private bool waitingForUfoBoarding;
    private bool stageCenterLocked;
    private bool deliveryMovementActive;
    private bool deathTriggered;
    private bool deliveryPausedForBoarding;
    private bool currentLayerWaitingForStomp;
    private bool currentLayerSettling;
    private bool boardingHoldActive;
    private bool boardingCommitted;
    private float boardingHoldElapsed;
    private float boxBadImpactPauseRemaining;
    private bool isAscending;
    private bool gameplayActive;

    private bool IsBoxBadImpactPaused => boxBadImpactPauseRemaining > 0f;

    private GameObject currentLayerObject;
    private Transform currentLayerParent;
    private Vector3 currentLayerTargetPosition;
    private float currentLayerHeight;
    private float currentLayerReturnDuration;
    private float currentLayerObjectDuration;
    private Vector3 lastLayerPosition;
    private float stackCenterX;
    private bool stackCenterInitialized;
    private bool moveKeyUsed;
    private bool ufoExitSelected;
    private bool finalUfoExitCompleted;
    private bool fullClimbUnlockStarted;
    private float ufoExitSupportY;
    private float inducedExitSupportY;
    private float finalUfoExitDuration;
    private double[] remainingLayerRise;
    private Collider2D[] inducedDoorColliders;
    private bool[] inducedDoorColliderStates;
    private SuccessAnimCtrl activeExitAnimation;

    private void Awake()
    {
        if (levelCtrl == null)
        {
            levelCtrl = GetComponent<BaseLevelCtrl>();
        }

        if (actor == null && levelCtrl != null)
        {
            actor = levelCtrl.Actor;
        }

        if (actor != null)
        {
            actorShadow = actor.transform.Find("Shadow")?.gameObject;
            actorShadowController = actorShadow != null ? actorShadow.GetComponent<ShadowController>() : null;
            actorShadowInitialActive = actorShadow != null && actorShadow.activeSelf;
            actorShadowControllerInitialEnabled = actorShadowController != null && actorShadowController.enabled;
        }

        if (ufoRoot == null)
        {
            ufoRoot = transform.Find("UFO_Ascending");
        }

        if (ufoBodyCollider == null && ufoRoot != null)
        {
            Transform body = ufoRoot.Find("UFOBody");
            if (body != null)
            {
                ufoBodyCollider = body.GetComponent<Collider2D>();
            }
        }

        if (ufoCollision == null && ufoRoot != null)
        {
            ufoCollision = ufoRoot.GetComponent<Level106UfoCollision>();
        }

        if (ufoRodCollider == null && ufoRoot != null)
        {
            ufoRodCollider = ufoRoot.GetComponent<Collider2D>();
        }

        if (carryAnchor == null && ufoRoot != null)
        {
            carryAnchor = ufoRoot.Find("UFO_CarryAnchor");
        }

        if (triggerStage == null)
        {
            triggerStage = transform.Find("UFO_TriggerStage");
        }

        if (stageSupport == null)
        {
            stageSupport = transform.Find("Ground_long");
        }

        stageSupportCollider = stageSupport != null
            ? stageSupport.GetComponent<Collider2D>()
            : null;

        if (stackRoot == null)
        {
            stackRoot = transform.Find("UFO_StackedLayers");
        }

        remainingLayersParamsManager = remainingLayersText != null
            ? remainingLayersText.GetComponent<LocalizationParamsManager>()
            : null;

        if (boxBadCtrl == null)
        {
            boxBadCtrl = GetComponentInChildren<Level106BoxBadCtrl>(true);
        }

        if (exitPlatformCollider == null)
        {
            Transform exitPlatform = transform.Find("TopExitPlatform");
            if (exitPlatform != null)
            {
                exitPlatformCollider = exitPlatform.GetComponent<Collider2D>();
            }
        }

        if (inducedExitPlatform == null)
            inducedExitPlatform = transform.Find("TopExitPlatform (1)");
        if (inducedExitDoor == null)
            inducedExitDoor = transform.Find("door_0 (1)")?.GetComponent<SuccessAnimCtrl>();
        if (ufoExitDoor == null)
            ufoExitDoor = transform.Find("door_0")?.GetComponent<SuccessAnimCtrl>();
        if (climbLoop == null)
            climbLoop = GetComponent<Level106ClimbLoop>();

        ufoExitSupportY = exitPlatformCollider != null ? exitPlatformCollider.bounds.max.y : 194f;
        Collider2D inducedSupport = inducedExitPlatform != null
            ? inducedExitPlatform.GetComponent<Collider2D>()
            : null;
        inducedExitSupportY = inducedSupport != null ? inducedSupport.bounds.max.y : ufoExitSupportY;
        inducedDoorColliders = inducedExitDoor != null
            ? inducedExitDoor.GetComponentsInChildren<Collider2D>(true)
            : Array.Empty<Collider2D>();
        inducedDoorColliderStates = new bool[inducedDoorColliders.Length];
        for (int i = 0; i < inducedDoorColliders.Length; i++)
            inducedDoorColliderStates[i] = inducedDoorColliders[i].enabled;

        if (actor != null)
        {
            actorCollider = actor.GetComponent<Collider2D>();
            boardingActor = actor as Level106ActorCtrl;
            if (boardingActor == null)
                Debug.LogError("Level 106 玩家必须使用 Level106ActorCtrl 承载登乘 idle。", this);
            actorBody = actor.GetComponent<Rigidbody2D>();
            actorSortingGroup = actor.GetComponent<SortingGroup>();
            actorInitialParent = actor.transform.parent;
            if (actorBody != null)
            {
                actorInitialBodyType = actorBody.bodyType;
                actorInitialGravityScale = actorBody.gravityScale;
                actorInitialSimulated = actorBody.simulated;
            }
        }

        if (ufoRoot != null)
        {
            initialUfoPosition = ufoRoot.position;
            deliveryRestPosition = initialUfoPosition;
            lastUfoPosition = initialUfoPosition;
            ascentTargetY = initialUfoPosition.y;
            deliveryWorkY = initialUfoPosition.y;
            finalAscentRootY = ResolveFinalAscentRootY();
        }

        stackCenterX = ResolveStageCenterX();
        stackCenterInitialized = true;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (ufoRoot != null)
        {
            deliveryRestPosition = initialUfoPosition;
        }

        cameraFollowArea = GetComponent<Level106CameraFollowArea>();
        ApplyInitialCameraProfile();
        climbLoop?.Initialize(targetCamera, cameraFollowArea, inducedExitSupportY);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
        eventSubscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerRightMove);
        eventSubscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);
        eventSubscriber.Subscribe<PlayerMoveCancelEvent>(OnPlayerMoveCancel);
        eventSubscriber.Subscribe<GameResetEvent>(OnGameReset);
        eventSubscriber.Subscribe<OnTriggerTrapEvent>(OnTriggerTrap);
        eventSubscriber.Subscribe<OnTriggerFailAreaEvent>(OnTriggerFailArea);
        eventSubscriber.Subscribe<GamePauseEvent>(OnGamePause);
        eventSubscriber.Subscribe<GameResumeEvent>(OnGameResume);
        eventSubscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);

        gameplayActive = PlayCtrl.Instance == null || PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
        InitializeRuntimeState();
    }

    private void Update()
    {
        UpdateUfoCollisionState();
        ResolveMoveButtons();
        UpdateStageCenterLock();

        if (!gameplayActive || actor == null)
        {
            return;
        }

        UpdateBoxBadImpactPause();
        if (IsBoxBadImpactPaused || boardingHoldActive || boardingCommitted || isAscending)
        {
            LockActorControls();
        }

        if (!waitingForUfoBoarding)
        {
            CancelBoardingHold();
            return;
        }

        if (!actor.IsGround)
        {
            CancelBoardingHold();
            return;
        }

        if (!deliveryPausedForBoarding && IsActorSupportedBy(ufoRodCollider))
        {
            PauseDeliveryForBoarding();
        }

        if (!deliveryPausedForBoarding || boardingCommitted || !IsActorCenteredAndSupportedBy(ufoBodyCollider)
            || boxBadCtrl == null || !boxBadCtrl.HasKnockedDown)
        {
            CancelBoardingHold();
            return;
        }

        if (!boardingHoldActive)
        {
            CenterActorColliderOn(ufoBodyCollider);
            BeginBoardingHold();
        }

        boardingHoldElapsed += Time.deltaTime;
        if (boardingHoldElapsed >= boardingHoldDuration)
        {
            BeginAscent();
        }
    }

    private void LateUpdate()
    {
        UpdateExitVisibility();
    }

    private void OnDisable()
    {
        BeginTeardown();
        leftMoveButtonRect = null;
        rightMoveButtonRect = null;
    }

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        if (TryShowRealTipsPopupImmediately())
        {
            return;
        }

        if (realTipsPopup != null || realTipsInputLock)
        {
            LockActorControls();
            return;
        }

        if (IsBoxBadImpactPaused || boardingHoldActive || boardingCommitted || isAscending)
        {
            LockActorControls();
        }
    }

    private void OnPlayerRightMove(PlayerRightMoveEvent evt)
    {
        if (TryShowRealTipsPopupImmediately())
        {
            return;
        }

        if (realTipsPopup != null || realTipsInputLock)
        {
            LockActorControls();
            return;
        }

        if (unlockedMoveDirection != MoveDirection.Right
            || actor == null
            || !gameplayActive
            || deathTriggered
            || IsBoxBadImpactPaused
            || boardingHoldActive
            || isAscending
            || boardingCommitted)
        {
            return;
        }

        ActivateBoardingCameraFollow();
        actor.SetMoveInput(1f);
    }

    private void OnPlayerLeftMove(PlayerLeftMoveEvent evt)
    {
        if (TryShowRealTipsPopupImmediately())
        {
            return;
        }

        if (realTipsPopup != null || realTipsInputLock)
        {
            LockActorControls();
            return;
        }

        if (!leftMoveUnlocked
            || actor == null
            || IsBoxBadImpactPaused
            || boardingHoldActive
            || isAscending
            || boardingCommitted)
        {
            return;
        }

        actor.SetMoveInput(-1f);
    }

    private void OnPlayerMoveCancel(PlayerMoveCancelEvent evt)
    {
        if (unlockedMoveDirection == MoveDirection.None)
        {
            return;
        }

        StopActorHorizontalMovement();
    }

    private bool TryShowRealTipsPopupImmediately()
    {
        if (realTipsPopupTriggered || realTipsPopup != null || !isActiveAndEnabled)
        {
            return false;
        }

        levelCtrl?.SetCtrlable(false);
        LockActorControls();
        Level106RealTipsPopup popup = Level106RealTipsPopup.Create();
        if (popup == null)
        {
            levelCtrl?.SetCtrlable(!hasStarted);
            UnlockActorControls();
            return false;
        }

        realTipsPopupTriggered = true;
        realTipsInputLock = true;
        realTipsPopup = popup;
        return true;
    }

    private void CloseRealTipsPopup()
    {
        if (realTipsPopup != null)
        {
            realTipsPopup.CloseFromOwner();
            realTipsPopup = null;
        }

        realTipsPopupTriggered = false;
        realTipsInputLock = false;
    }

    private void OnGameReset(GameResetEvent evt)
    {
        BeginTeardown();
    }

    private void OnTriggerTrap(OnTriggerTrapEvent evt)
    {
        deathTriggered = true;
        FreezeDeliveryMechanisms();
    }

    private void OnTriggerFailArea(OnTriggerFailAreaEvent evt)
    {
        deathTriggered = true;
        FreezeDeliveryMechanisms();
    }

    private void SetDeliveryMovementActive(bool isActive)
    {
        deliveryMovementActive = isActive;
        UpdateUfoCollisionState();
    }

    private void UpdateUfoCollisionState()
    {
        if (ufoCollision == null)
        {
            return;
        }

        bool platformActive = gameplayActive
            && !finalUfoExitCompleted
            && (hasStarted || waitingForUfoBoarding || isAscending);
        ufoCollision.SetPlatformActive(platformActive);
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        gameplayActive = false;
        climbLoop?.SetSuspended(true);
        activeSequence?.Pause();
        remainingLayersCountTween?.Pause();
        UpdateUfoCollisionState();
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        gameplayActive = !deathTriggered;
        climbLoop?.SetSuspended(!gameplayActive);
        UpdateUfoCollisionState();
        if (gameplayActive)
        {
            activeSequence?.Play();
            remainingLayersCountTween?.Play();
        }
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        gameplayActive = !deathTriggered
            && evt != null
            && evt.NewState == GamePlayStateName.PLAYING;
        climbLoop?.SetSuspended(!gameplayActive);
        UpdateUfoCollisionState();
        if (gameplayActive)
        {
            activeSequence?.Play();
            remainingLayersCountTween?.Play();
            if (realTipsInputLock)
            {
                realTipsInputLock = false;
                realTipsPopup = null;
                actor?.ResumeMoving();
                if (!hasStarted)
                {
                    levelCtrl?.SetCtrlable(true);
                }
            }
        }
        else
        {
            activeSequence?.Pause();
        }
    }

    private void BeginTeardown()
    {
        deathTriggered = true;
        gameplayActive = false;
        climbLoop?.SetSuspended(true);
        SetInducedDoorCollision(false);
        CloseRealTipsPopup();
        EventsUtils.ResetEvents(ref eventSubscriber);
        activeSequence?.Kill(false);
        activeSequence = null;
        StopRemainingLayersCountEffect(false);
        ufoRoot?.DOKill(false);
        ReleaseBoardingPresentation(resumePhysics: false, updateAnimation: false);
        boardingHoldActive = false;
        boardingCommitted = false;
        isAscending = false;
        actorDeliveryRotationTween?.Kill(false);
        actorDeliveryRotationTween = null;
        actorDeliveryKnockbackActive = false;
        deliveryMovementActive = false;
        boxBadImpactPauseRemaining = 0f;
        boxBadCtrl?.SuspendRuntime();
        UpdateUfoCollisionState();
        DestroyMoveLocks();
        levelCtrl?.SetCtrlable(false);
        StopActorHorizontalMovement();
    }

    private void InitializeRuntimeState()
    {
        activeSequence?.Kill();
        activeSequence = null;
        StopRemainingLayersCountEffect(false);

        for (int i = 0; i < stackedObjects.Count; i++)
        {
            if (stackedObjects[i] != null)
            {
                Destroy(stackedObjects[i]);
            }
        }

        stackedObjects.Clear();
        if (currentLayerObject != null)
        {
            Destroy(currentLayerObject);
        }

        currentLayerObject = null;
        currentLayerParent = null;
        currentLayerWaitingForStomp = false;
        currentLayerSettling = false;
        layerSequence = null;
        moveKeyDrag = null;
        completedLayers = 0;
        moveKeyUsed = false;
        ufoExitSelected = false;
        finalUfoExitCompleted = false;
        fullClimbUnlockStarted = false;
        remainingLayerRise = null;
        remainingLayersReachedTop = false;
        hasStarted = false;
        unlockedMoveDirection = MoveDirection.None;
        leftMoveUnlocked = false;
        boardingReady = false;
        waitingForUfoBoarding = false;
        stageCenterLocked = false;
        deliveryMovementActive = false;
        deliveryPausedForBoarding = false;
        boardingHoldActive = false;
        boardingHoldElapsed = 0f;
        boxBadImpactPauseRemaining = 0f;
        boardingCommitted = false;
        deathTriggered = false;
        isAscending = false;
        gameplayActive = PlayCtrl.Instance == null
            || PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);

        ReleaseBoardingPresentation(resumePhysics: true, updateAnimation: false);
        ResetActorDeliveryKnockback();
        SetActorShadowVisible(true);
        if (ufoRoot != null)
        {
            ufoRoot.position = initialUfoPosition;
            deliveryRestPosition = initialUfoPosition;
            lastUfoPosition = initialUfoPosition;
            ascentTargetY = initialUfoPosition.y;
            deliveryWorkY = initialUfoPosition.y;
            finalAscentRootY = ResolveFinalAscentRootY();
        }

        lastSettledLayerSceneHeight = 0f;
        UnlockActorControls();
        ApplyInitialCameraProfile();
        cameraFollowArea?.SetCanFollow(true);
        levelCtrl?.SetCtrlable(true);
        boxBadCtrl?.InitializeRuntimeState();
        ufoCollision?.InitializeRuntimeState();
        DestroyMoveLocks();
        stackTopY = stageSupportCollider != null ? stageSupportCollider.bounds.max.y : -1.44f;
        stackCenterX = ResolveStageCenterX();
        stackCenterInitialized = true;
        UpdateRemainingLayersText();
        UpdateExitVisibility();
    }

    private void StartDeliverySequence()
    {
        if (hasStarted || !gameplayActive || actor == null)
        {
            return;
        }

        if (keyBallSprite == null
            || moveKeySprite == null
            || randomLayerSprites == null
            || randomLayerSprites.Length == 0)
        {
            Debug.LogWarning("[Level106Ctrl] Send-layer sprites are not configured.");
            return;
        }

        hasStarted = true;
        unlockedMoveDirection = MoveDirection.None;
        levelCtrl?.SetCtrlable(false);
        ResolveMoveButtons();
        SetMoveLocksVisible(true);
        StopActorHorizontalMovement();
        layerSequence = BuildLayerSequence();
        BuildRemainingLayerRise();
        SendNextLayer();
    }

    private void BuildRemainingLayerRise()
    {
        remainingLayerRise = new double[layerSequence.Count + 1];
        for (int i = layerSequence.Count - 1; i >= 0; i--)
        {
            Sprite sprite = layerSequence[i];
            LayerVisualProfile profile = FindVisualProfile(sprite);
            float scale = profile != null ? profile.scale : sprite == horizontalLayerSprite ? horizontalLayerScale : 1f;
            float rotation = profile != null ? profile.rotationZ : sprite == horizontalLayerSprite ? 90f : 0f;
            float widthMultiplier = profile != null ? Mathf.Max(1f, profile.colliderWidthMultiplier) : 1f;
            float radians = rotation * Mathf.Deg2Rad;
            float height = sprite == null ? 0f : scale * (
                Mathf.Abs(Mathf.Cos(radians)) * sprite.bounds.size.y * Mathf.Abs(transform.lossyScale.y)
                + Mathf.Abs(Mathf.Sin(radians)) * sprite.bounds.size.x * widthMultiplier * Mathf.Abs(transform.lossyScale.x));
            float rise = height - (i == 0 ? 0f : StackOverlapY) + (profile != null ? profile.stackOffset.y : 0f);
            remainingLayerRise[i] = remainingLayerRise[i + 1] + Mathf.Max(0f, rise);
        }
    }

    private List<Sprite> BuildLayerSequence()
    {
        var result = new List<Sprite>(TotalLayers);
        var shuffledPool = new List<Sprite>();
        int poolIndex = 0;
        Sprite previousSprite = null;

        for (int layerNumber = 1; layerNumber <= TotalLayers; layerNumber++)
        {
            if (layerNumber == KeyLayerNumber)
            {
                result.Add(keyBallSprite);
                previousSprite = keyBallSprite;
                continue;
            }

            if (poolIndex >= shuffledPool.Count)
            {
                shuffledPool = BuildNonRepeatingPool(previousSprite);
                poolIndex = 0;
            }

            Sprite nextSprite = shuffledPool[poolIndex++];
            result.Add(nextSprite);
            previousSprite = nextSprite;
        }

        return result;
    }

    private List<Sprite> BuildNonRepeatingPool(Sprite previousSprite)
    {
        var remaining = new Dictionary<Sprite, int>();
        for (int i = 0; i < randomLayerSprites.Length; i++)
        {
            Sprite sprite = randomLayerSprites[i];
            if (sprite == null)
            {
                continue;
            }

            remaining.TryGetValue(sprite, out int count);
            remaining[sprite] = count + 1;
        }

        var result = new List<Sprite>(randomLayerSprites.Length);
        while (remaining.Count > 0)
        {
            int maxCount = 0;
            foreach (KeyValuePair<Sprite, int> pair in remaining)
            {
                if (pair.Key != previousSprite && pair.Value > maxCount)
                {
                    maxCount = pair.Value;
                }
            }

            var candidates = new List<Sprite>();
            foreach (KeyValuePair<Sprite, int> pair in remaining)
            {
                if (pair.Key != previousSprite && pair.Value == maxCount)
                {
                    candidates.Add(pair.Key);
                }
            }

            if (candidates.Count == 0)
            {
                Debug.LogWarning("[Level106Ctrl] Unable to build a non-repeating layer pool.");
                break;
            }

            Sprite selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            result.Add(selected);
            previousSprite = selected;
            if (remaining[selected] <= 1)
            {
                remaining.Remove(selected);
            }
            else
            {
                remaining[selected]--;
            }
        }

        return result;
    }

    private void SendNextLayer()
    {
        if (!gameplayActive
            || layerSequence == null
            || completedLayers >= TotalLayers
            || deliveryPausedForBoarding
            || currentLayerObject != null
            || ufoRoot == null)
        {
            return;
        }

        Sprite sprite = layerSequence[completedLayers];
        int layerNumber = completedLayers + 1;
        GameObject layerObject = CreateLayerObject(sprite, layerNumber);
        if (layerObject == null)
        {
            return;
        }

        LayerVisualProfile visualProfile = FindVisualProfile(sprite);
        Transform parent = stackRoot != null ? stackRoot : transform;
        BoxCollider2D layerCollider = layerObject.GetComponent<BoxCollider2D>();
        float layerHeight = ResolveColliderWorldHeight(layerCollider);
        float colliderCenterOffsetY = ResolveColliderWorldCenterOffsetY(layerCollider);
        float supportTopY = ResolveCurrentSupportTopY();
        float overlap = ResolveStackOverlap();
        Vector3 targetPosition = new Vector3(
            ResolveLayerPivotXForStackCenter(layerObject),
            supportTopY - overlap - colliderCenterOffsetY + layerHeight * 0.5f,
            -0.1f);
        if (visualProfile != null)
        {
            targetPosition.y += visualProfile.stackOffset.y;
            targetPosition.z += visualProfile.stackOffset.z;
        }
        targetPosition.x = ResolveLayerPivotXForStackCenter(layerObject);

        layerObject.transform.SetParent(ufoRoot, false);
        Vector3 carryPosition = carryAnchor != null
            ? carryAnchor.localPosition
            : new Vector3(-3.85f, -0.7f, -0.1f);
        if (visualProfile != null)
        {
            carryPosition += visualProfile.carryOffset;
        }

        layerObject.transform.localPosition = carryPosition;

        float deliveryTargetX = ResolveUfoDeliveryX();
        float ufoTargetY = completedLayers == 0
            ? ufoRoot.position.y
            : deliveryWorkY + lastSettledLayerSceneHeight - StackOverlapY;
        float speedMultiplier = ResolveLayerSpeedMultiplier(layerNumber);
        float verticalDuration = Mathf.Abs(ufoTargetY - ufoRoot.position.y)
            / (Mathf.Max(0.01f, ascentSpeed) * speedMultiplier);
        float leftMoveDuration = Mathf.Max(0.01f, ufoMoveDuration / speedMultiplier);
        float rightMoveDuration = Mathf.Max(0.01f, ufoReturnDuration / speedMultiplier);

        currentLayerObject = layerObject;
        currentLayerParent = parent;
        currentLayerTargetPosition = targetPosition;
        currentLayerHeight = layerHeight;
        currentLayerReturnDuration = rightMoveDuration;
        currentLayerObjectDuration = Mathf.Max(0.01f, itemTravelDuration);
        currentLayerWaitingForStomp = false;
        currentLayerSettling = false;

        activeSequence = DOTween.Sequence()
            .AppendCallback(() => SetDeliveryMovementActive(true))
            .Append(ufoRoot.DOMoveY(ufoTargetY, Mathf.Max(0.01f, verticalDuration))
                .SetEase(Ease.InOutSine))
            .Append(ufoRoot.DOMoveX(deliveryTargetX, leftMoveDuration)
                .SetEase(Ease.Linear)
                .OnComplete(() => SetDeliveryMovementActive(false)))
            .OnComplete(() =>
            {
                SetDeliveryMovementActive(false);
                currentLayerWaitingForStomp = true;
                activeSequence = null;
            });

        if (!gameplayActive)
        {
            activeSequence.Pause();
        }
    }

    private float ResolveLayerSpeedMultiplier(int layerNumber)
    {
        if (layerNumber <= AccelerationStartLayer)
        {
            return 1f;
        }

        float layersAfterAccelerationStart = layerNumber - AccelerationStartLayer;
        return Mathf.Min(
            maxLayerSpeedMultiplier,
            1f + layersAfterAccelerationStart * Mathf.Max(0f, layerSpeedIncrement));
    }

    private void MarkBoardingReady()
    {
        boardingReady = true;
        deliveryRestPosition = initialUfoPosition;
        finalAscentRootY = ResolveFinalAscentRootY();
        waitingForUfoBoarding = moveKeyUsed && unlockedMoveDirection == MoveDirection.Right;
    }

    private void HandleLayerCollision(GameObject layerObject, Collision2D collision)
    {
        if (layerObject == null
            || collision == null
            || currentLayerObject != layerObject
            || currentLayerSettling
            || actor == null
            || actorCollider == null)
        {
            return;
        }

        ActorCtrl collidedActor = collision.collider != null
            ? collision.collider.GetComponentInParent<ActorCtrl>()
            : null;
        if (collidedActor != actor || !collidedActor.CompareTag("Player"))
        {
            return;
        }

        BoxCollider2D layerCollider = layerObject.GetComponent<BoxCollider2D>();
        if (IsActorDescendingOnto(actorCollider, layerCollider))
        {
            TryStompLayer(layerObject);
            return;
        }

        TryHandleDeliveryLayerSideCollision(collision);
    }

    private void TryHandleDeliveryLayerSideCollision(Collision2D collision)
    {
        if (boardingReady
            && moveKeyUsed
            && unlockedMoveDirection == MoveDirection.Right
            && IsActorSupportedBy(ufoRodCollider))
        {
            PauseDeliveryForBoarding();
            return;
        }

        if (!gameplayActive
            || deliveryPausedForBoarding
            || deathTriggered
            || actor == null
            || actor.IsDied)
        {
            return;
        }

        deathTriggered = true;
        Vector3 impactPosition = collision.contactCount > 0
            ? collision.GetContact(0).point
            : actorCollider != null ? actorCollider.bounds.center : transform.position;
        FreezeDeliveryMechanisms();
        ufoCollision?.PlayKnock(impactPosition);
        AudioManager.PlaySound("trap");
        PlayActorDeliveryKnockback();
    }

    private void PlayActorDeliveryKnockback()
    {
        if (actor == null)
        {
            return;
        }

        actor.StopMoving();
        cameraFollowArea?.SetCanFollow(false);
        actorDeliveryRotationTween?.Kill(false);
        actorDeliveryKnockbackStartRotation = actor.transform.localRotation;
        actorDeliveryKnockbackActive = true;
        actor.Knockback(DeliveryHitKnockbackDirection, DeliveryHitKnockbackForce).Forget();
        actorDeliveryRotationTween = actor.transform
            .DOLocalRotate(
                new Vector3(0f, 0f, 360f),
                DeliveryHitRotationDuration,
                RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1)
            .SetLink(actor.gameObject);
    }

    private void ResetActorDeliveryKnockback()
    {
        actorDeliveryRotationTween?.Kill(false);
        actorDeliveryRotationTween = null;
        if (actor != null)
        {
            actor.StopKnock();
            if (actorDeliveryKnockbackActive)
            {
                actor.transform.localRotation = actorDeliveryKnockbackStartRotation;
            }
        }

        if (actorBody != null)
        {
            actorBody.linearVelocity = Vector2.zero;
            actorBody.angularVelocity = 0f;
        }

        actorDeliveryKnockbackActive = false;
    }

    private bool IsActorDescendingOnto(Collider2D playerCollider, Collider2D layerCollider)
    {
        if (playerCollider == null || layerCollider == null)
        {
            return false;
        }

        Bounds playerBounds = playerCollider.bounds;
        Bounds layerBounds = layerCollider.bounds;
        bool horizontalOverlap = playerBounds.max.x > layerBounds.min.x
            && playerBounds.min.x < layerBounds.max.x;
        bool fromAbove = playerBounds.min.y >= layerBounds.max.y - 0.2f;
        bool movingDown = actorBody == null || actorBody.linearVelocity.y <= 0.5f;
        return horizontalOverlap && fromAbove && movingDown;
    }

    private void TryStompLayer(GameObject layerObject)
    {
        if (!gameplayActive
            || deliveryPausedForBoarding
            || layerObject == null
            || currentLayerObject != layerObject
            || currentLayerSettling)
        {
            return;
        }

        currentLayerSettling = true;
        currentLayerWaitingForStomp = false;
        activeSequence?.Kill(false);
        activeSequence = null;
        ufoRoot?.DOKill(false);
        SetDeliveryMovementActive(false);

        float stompCenterX = layerObject.GetComponent<BoxCollider2D>()?.bounds.center.x
            ?? layerObject.transform.position.x;
        Vector3 targetPosition = ResolveLayerTargetAtCurrentPosition(layerObject, stompCenterX);
        currentLayerTargetPosition = targetPosition;
        lastLayerPosition = layerObject.transform.position;
        ReleaseLayerObject(layerObject, currentLayerParent, completedLayers + 1);

        bool isFinalLayer = completedLayers + 1 == TotalLayers;
        activeSequence = DOTween.Sequence()
            .Append(layerObject.transform.DOMoveY(targetPosition.y, currentLayerObjectDuration)
                .SetEase(Ease.OutQuad)
                .OnUpdate(() => SyncActorWithLayer(layerObject)))
            .AppendCallback(() => CompleteLayer(layerObject, currentLayerHeight, targetPosition));
        if (isFinalLayer)
        {
            activeSequence.OnComplete(BeginFinalUfoExit);
        }
        else
        {
            activeSequence
                .AppendCallback(() => SetDeliveryMovementActive(true))
                .Append(ufoRoot.DOMoveX(deliveryRestPosition.x, currentLayerReturnDuration)
                    .SetEase(Ease.Linear)
                    .OnComplete(() => SetDeliveryMovementActive(false)))
                .AppendInterval(Mathf.Lerp(
                    firstLayerInterval,
                    finalLayerInterval,
                    Mathf.Clamp01(completedLayers / (float)Mathf.Max(1, TotalLayers - 1))))
                .OnComplete(FinishLayerCycle);
        }

        if (!gameplayActive)
        {
            activeSequence.Pause();
        }
    }

    private Vector3 ResolveLayerTargetAtCurrentPosition(GameObject layerObject, float worldCenterX)
    {
        BoxCollider2D collider = layerObject != null
            ? layerObject.GetComponent<BoxCollider2D>()
            : null;
        float layerHeight = ResolveColliderWorldHeight(collider);
        float colliderCenterOffsetY = ResolveColliderWorldCenterOffsetY(collider);
        float supportTopY = ResolveCurrentSupportTopY();
        float overlap = ResolveStackOverlap();
        float pivotOffsetX = ResolveLayerRendererCenterOffsetX(layerObject);
        LayerVisualProfile visualProfile = FindVisualProfile(layerObject != null
            ? layerObject.GetComponent<SpriteRenderer>()?.sprite
            : null);
        Vector3 targetPosition = new Vector3(
            worldCenterX - pivotOffsetX,
            supportTopY - overlap - colliderCenterOffsetY + layerHeight * 0.5f,
            -0.1f);
        if (visualProfile != null)
        {
            targetPosition.y += visualProfile.stackOffset.y;
            targetPosition.z += visualProfile.stackOffset.z;
        }

        return targetPosition;
    }

    private void SyncActorWithLayer(GameObject layerObject)
    {
        if (layerObject == null)
        {
            return;
        }

        Vector3 layerDelta = layerObject.transform.position - lastLayerPosition;
        if (actorBody != null && IsActorSupportedBy(layerObject.GetComponent<Collider2D>()))
        {
            actorBody.MovePosition(actorBody.position + (Vector2)layerDelta);
        }

        lastLayerPosition = layerObject.transform.position;
    }

    private void FinishLayerCycle()
    {
        SetDeliveryMovementActive(false);
        activeSequence = null;
        currentLayerObject = null;
        currentLayerParent = null;
        currentLayerWaitingForStomp = false;
        currentLayerSettling = false;

        if (completedLayers < TotalLayers && !deliveryPausedForBoarding)
        {
            RebaseClimbBetweenLayers();
            SendNextLayer();
        }
    }

    private void RebaseClimbBetweenLayers()
    {
        if (climbLoop == null || remainingLayerRise == null)
            return;

        float supportTopY = ResolveCurrentSupportTopY();
        float predictedFinalTopY = supportTopY + (float)remainingLayerRise[completedLayers];
        float deltaY = climbLoop.RebaseBetweenLayers(supportTopY, predictedFinalTopY);
        if (deltaY == 0f)
            return;

        deliveryWorkY += deltaY;
        deliveryRestPosition.y += deltaY;
        lastUfoPosition.y += deltaY;
        stackTopY += deltaY;
        currentLayerTargetPosition.y += deltaY;
        lastLayerPosition.y += deltaY;
        moveKeyDrag?.ApplyWorldShift(Vector3.up * deltaY);
    }

    private void BeginFinalUfoExit()
    {
        activeSequence = null;
        currentLayerObject = null;
        currentLayerParent = null;
        currentLayerWaitingForStomp = false;
        currentLayerSettling = false;
        if (deliveryPausedForBoarding || deathTriggered || ufoRoot == null)
            return;

        float deliveryX = ResolveUfoDeliveryX();
        float normalDistance = Mathf.Max(0.01f, Mathf.Abs(deliveryRestPosition.x - deliveryX));
        float speed = normalDistance / Mathf.Max(0.01f, currentLayerReturnDuration) * 2f;
        CameraFollowRange range = GetComponent<CameraFollowRange>();
        float rightEdge = range != null ? range.GetBoundaryRect().xMax
            : targetCamera.transform.position.x + targetCamera.orthographicSize * targetCamera.aspect;
        float leftEdge = ufoRoot.position.x;
        bool hasBounds = false;
        foreach (Renderer renderer in ufoRoot.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled)
                continue;
            leftEdge = hasBounds ? Mathf.Min(leftEdge, renderer.bounds.min.x) : renderer.bounds.min.x;
            hasBounds = true;
        }

        float distance = Mathf.Max(0f, rightEdge + 0.1f - leftEdge);
        finalUfoExitDuration = Mathf.Max(0.01f, distance / speed);
        SetDeliveryMovementActive(true);
        activeSequence = DOTween.Sequence()
            .Append(ufoRoot.DOMoveX(ufoRoot.position.x + distance, finalUfoExitDuration).SetEase(Ease.Linear))
            .OnComplete(() =>
            {
                activeSequence = null;
                finalUfoExitCompleted = true;
                waitingForUfoBoarding = false;
                SetDeliveryMovementActive(false);
                ufoRoot.gameObject.SetActive(false);
                BeginFullClimbUnlock();
            });
        if (!gameplayActive)
            activeSequence.Pause();
    }

    private void BeginFullClimbUnlock()
    {
        if (fullClimbUnlockStarted || deathTriggered || deliveryPausedForBoarding)
            return;

        fullClimbUnlockStarted = true;
        ResolveMoveButtons();
        if (!leftMoveUnlocked)
        {
            if (leftMoveLock != null)
                leftMoveLock.PlayUnlockAndDestroy(CompleteFullClimbLeftUnlock);
            else
                CompleteFullClimbLeftUnlock();
        }

        // A key already being consumed owns its existing right-lock completion callback.
        if (unlockedMoveDirection != MoveDirection.Right && !moveKeyUsed)
        {
            if (rightMoveLock != null)
                rightMoveLock.PlayUnlockAndDestroy(CompleteFullClimbRightUnlock);
            else
                CompleteFullClimbRightUnlock();
        }
    }

    private void CompleteFullClimbLeftUnlock()
    {
        if (deathTriggered || !isActiveAndEnabled)
            return;
        leftMoveUnlocked = true;
        leftMoveLock = null;
    }

    private void CompleteFullClimbRightUnlock()
    {
        if (deathTriggered || !isActiveAndEnabled)
            return;
        unlockedMoveDirection = MoveDirection.Right;
        rightMoveLock = null;
    }

    private void UpdateExitVisibility()
    {
        bool showInduced = !ufoExitSelected && (completedLayers >= TotalLayers
            || (climbLoop != null && climbLoop.IsFinalApproach));
        SetExitActive(inducedExitPlatform, showInduced);
        SetExitActive(inducedExitDoor != null ? inducedExitDoor.transform : null, showInduced);
        SetExitActive(exitPlatformCollider != null ? exitPlatformCollider.transform : null, ufoExitSelected);
        SetExitActive(ufoExitDoor != null ? ufoExitDoor.transform : null, ufoExitSelected);
        SetInducedDoorCollision(showInduced && completedLayers == TotalLayers && !deathTriggered);
        SuccessAnimCtrl animation = ufoExitSelected ? ufoExitDoor : showInduced ? inducedExitDoor : null;
        if (animation != null && animation != activeExitAnimation)
        {
            activeExitAnimation = animation;
            levelCtrl?.SetSuccessAnim(animation);
        }
    }

    private static void SetExitActive(Transform root, bool value)
    {
        if (root != null && root.gameObject.activeSelf != value)
            root.gameObject.SetActive(value);
    }

    private void SetInducedDoorCollision(bool enabled)
    {
        if (inducedDoorColliders == null)
            return;
        for (int i = 0; i < inducedDoorColliders.Length; i++)
        {
            if (inducedDoorColliders[i] != null)
                inducedDoorColliders[i].enabled = enabled && inducedDoorColliderStates[i];
        }
    }

    private GameObject CreateLayerObject(Sprite sprite, int layerNumber)
    {
        if (sprite == null)
        {
            Debug.LogWarning($"[Level106Ctrl] Layer {layerNumber} has no sprite.");
            return null;
        }

        var layerObject = new GameObject($"UFO_Layer_{layerNumber:000}");
        layerObject.layer = triggerStage != null ? triggerStage.gameObject.layer : 6;
        var renderer = layerObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        ApplyLayerVisualProfile(layerObject, sprite);
        SetLayerRendererSorting(layerObject, true, layerNumber);

        var collider = layerObject.AddComponent<BoxCollider2D>();
        LayerVisualProfile visualProfile = FindVisualProfile(sprite);
        float colliderWidthMultiplier = visualProfile != null
            ? Mathf.Max(1f, visualProfile.colliderWidthMultiplier)
            : 1f;
        collider.size = new Vector2(
            sprite.bounds.size.x * colliderWidthMultiplier,
            sprite.bounds.size.y);
        collider.offset = sprite.bounds.center;
        collider.enabled = true;

        Rigidbody2D layerBody = layerObject.AddComponent<Rigidbody2D>();
        layerBody.bodyType = RigidbodyType2D.Kinematic;
        layerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        layerBody.useFullKinematicContacts = true;
        layerBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        layerBody.interpolation = RigidbodyInterpolation2D.Interpolate;

        DeliveryLayerCollision collisionHandler = layerObject.AddComponent<DeliveryLayerCollision>();
        collisionHandler.Initialize(this);

        if (layerNumber == KeyLayerNumber && sprite == keyBallSprite)
        {
            var moveKeyObject = new GameObject("Level106_MoveKey");
            moveKeyObject.layer = layerObject.layer;
            moveKeyObject.transform.SetParent(layerObject.transform, false);
            moveKeyObject.transform.localPosition = moveKeyLocalPosition;
            moveKeyObject.transform.localRotation = Quaternion.Euler(0f, 0f, moveKeyRotation);
            moveKeyObject.transform.localScale = Vector3.one * moveKeyScale;

            var keyRenderer = moveKeyObject.AddComponent<SpriteRenderer>();
            keyRenderer.sprite = moveKeySprite;
            keyRenderer.sortingLayerName = carrySortingLayerName;
            keyRenderer.sortingOrder = carrySortingOrder - 1;

            var keyCollider = moveKeyObject.AddComponent<BoxCollider2D>();
            keyCollider.size = moveKeySprite.bounds.size;
            keyCollider.offset = moveKeySprite.bounds.center;
            keyCollider.enabled = false;

            moveKeyDrag = moveKeyObject.AddComponent<Level106MoveKeyDrag>();
            moveKeyDrag.Initialize(this, targetCamera, keyRenderer, keyCollider);
        }

        return layerObject;
    }

    private void ReleaseLayerObject(GameObject layerObject, Transform parent, int layerNumber)
    {
        if (layerObject == null)
        {
            return;
        }

        layerObject.transform.SetParent(parent, true);
        SetLayerRendererSorting(layerObject, false, layerNumber);
    }

    private sealed class DeliveryLayerCollision : MonoBehaviour
    {
        private Level106Ctrl owner;

        internal void Initialize(Level106Ctrl newOwner)
        {
            owner = newOwner;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            owner?.HandleLayerCollision(gameObject, collision);
        }
    }

    private void ApplyLayerVisualProfile(GameObject layerObject, Sprite sprite)
    {
        LayerVisualProfile profile = FindVisualProfile(sprite);
        if (profile != null)
        {
            layerObject.transform.localScale = Vector3.one * profile.scale;
            layerObject.transform.localRotation = Quaternion.Euler(0f, 0f, profile.rotationZ);
            return;
        }

        if (sprite == horizontalLayerSprite)
        {
            layerObject.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            layerObject.transform.localScale = Vector3.one * horizontalLayerScale;
        }
    }

    private LayerVisualProfile FindVisualProfile(Sprite sprite)
    {
        if (sprite == null || layerVisualProfiles == null)
        {
            return null;
        }

        for (int i = 0; i < layerVisualProfiles.Length; i++)
        {
            LayerVisualProfile profile = layerVisualProfiles[i];
            if (profile != null && profile.sprite == sprite)
            {
                return profile;
            }
        }

        return null;
    }

    private void SetLayerRendererSorting(GameObject layerObject, bool isCarried, int layerNumber)
    {
        if (layerObject == null)
        {
            return;
        }

        SpriteRenderer[] renderers = layerObject.GetComponentsInChildren<SpriteRenderer>(true);
        string sortingLayerName = isCarried ? carrySortingLayerName : stackedSortingLayerName;
        int sortingOrder = isCarried
            ? carrySortingOrder
            : layerSortingOrder + Mathf.Max(1, layerNumber);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = renderer.transform == layerObject.transform
                ? sortingOrder
                : sortingOrder - 1;
        }
    }

    private void CompleteLayer(GameObject layerObject, float layerHeight, Vector3 targetPosition)
    {
        if (layerObject == null)
        {
            return;
        }

        layerObject.transform.position = targetPosition;
        BoxCollider2D collider = layerObject.GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            collider.enabled = true;
        }

        int layerNumber = completedLayers + 1;
        SetLayerRendererSorting(layerObject, false, layerNumber);

        SpriteRenderer renderer = layerObject.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            stackCenterX = renderer.bounds.center.x;
            stackCenterInitialized = true;
            lastSettledLayerSceneHeight = renderer.bounds.size.y;
        }

        if (ufoRoot != null)
        {
            deliveryWorkY = ufoRoot.position.y;
        }

        stackedObjects.Add(layerObject);
        stackTopY = ResolveCurrentSupportTopY();
        completedLayers++;

        if (layerNumber == KeyLayerNumber)
        {
            MarkBoardingReady();
            moveKeyDrag?.EnableInteraction();
        }

        UpdateRemainingLayersText();
        UpdateExitVisibility();
    }

    private void FreezeDeliveryMechanisms()
    {
        climbLoop?.SetSuspended(true);
        SetInducedDoorCollision(false);
        ReleaseBoardingPresentation(resumePhysics: true, updateAnimation: false);
        SetDeliveryMovementActive(false);
        activeSequence?.Kill(false);
        activeSequence = null;
        StopRemainingLayersCountEffect(true);
        ufoRoot?.DOKill(false);
        waitingForUfoBoarding = false;
        boardingHoldActive = false;
        boardingCommitted = false;
        boardingHoldElapsed = 0f;
        isAscending = false;
        boxBadImpactPauseRemaining = 0f;
        gameplayActive = false;
        levelCtrl?.SetCtrlable(false);
    }

    private void SyncActorWithUfo()
    {
        // 保留根位置记录；乘员只随父级移动，不再用独立刚体 MovePosition 追赶。
        if (ufoRoot != null)
            lastUfoPosition = ufoRoot.position;
    }

    internal bool TryDropMoveKey(Level106MoveKeyDrag keyDrag, Vector2 screenPosition)
    {
        ResolveMoveButtons();
        if (unlockedMoveDirection != MoveDirection.None || keyDrag == null || keyDrag != moveKeyDrag)
        {
            return false;
        }

        MoveDirection selectedDirection = ResolveMoveDirection(screenPosition);
        if (selectedDirection != MoveDirection.Right)
        {
            return false;
        }

        moveKeyUsed = true;
        moveKeyDrag = null;
        Level106MoveButtonLock selectedLock = selectedDirection == MoveDirection.Left
            ? leftMoveLock
            : rightMoveLock;
        if (selectedLock != null)
        {
            selectedLock.PlayUnlockAndDestroy(() => CompleteMoveUnlock(selectedDirection));
        }
        else
        {
            CompleteMoveUnlock(selectedDirection);
        }

        StopActorHorizontalMovement();
        return true;
    }

    private void CompleteMoveUnlock(MoveDirection selectedDirection)
    {
        if (deathTriggered || !isActiveAndEnabled || unlockedMoveDirection != MoveDirection.None)
        {
            return;
        }

        unlockedMoveDirection = selectedDirection;
        waitingForUfoBoarding = boardingReady && moveKeyUsed && !finalUfoExitCompleted;
        deliveryPausedForBoarding = false;
        if (selectedDirection == MoveDirection.Right)
        {
            boxBadCtrl?.Arm(transform);
            SendNextLayer();
        }
    }

    private void ApplyInitialCameraProfile()
    {
        boardingCameraFollowActivated = false;
        cameraFollowArea?.SetFollowArea(
            InitialCameraFollowAreaCenter,
            InitialCameraFollowAreaSize);
    }

    private void ActivateBoardingCameraFollow()
    {
        if (boardingCameraFollowActivated)
        {
            return;
        }

        boardingCameraFollowActivated = true;
        cameraFollowArea?.SetFollowArea(
            BoardingCameraFollowAreaCenter,
            BoardingCameraFollowAreaSize);
    }

    internal void NotifyBoxBadHit(float impactPauseDuration)
    {
        if (!boardingReady || !moveKeyUsed || unlockedMoveDirection != MoveDirection.Right)
        {
            return;
        }

        UnlockLeftMovement();
        PauseDeliveryForBoarding();
        BeginBoxBadImpactPause(impactPauseDuration);
    }

    private void BeginBoxBadImpactPause(float duration)
    {
        boxBadImpactPauseRemaining = Mathf.Max(0f, duration);
        if (IsBoxBadImpactPaused)
        {
            LockActorControls();
        }
    }

    private void UpdateBoxBadImpactPause()
    {
        if (!IsBoxBadImpactPaused)
        {
            return;
        }

        boxBadImpactPauseRemaining = Mathf.Max(0f, boxBadImpactPauseRemaining - Time.deltaTime);
        if (IsBoxBadImpactPaused)
        {
            return;
        }

        if (!boardingHoldActive && !boardingCommitted && !isAscending && !realTipsInputLock)
        {
            UnlockActorControls();
        }
    }

    private void PauseDeliveryForBoarding()
    {
        if (deliveryPausedForBoarding || !boardingReady || !moveKeyUsed
            || finalUfoExitCompleted || unlockedMoveDirection != MoveDirection.Right)
        {
            return;
        }

        deliveryPausedForBoarding = true;
        currentLayerWaitingForStomp = false;
        activeSequence?.Kill(false);
        activeSequence = null;
        ufoRoot?.DOKill(false);
        SetDeliveryMovementActive(false);
        boardingHoldActive = false;
        boardingHoldElapsed = 0f;
        StopActorHorizontalMovement();
    }

    private void BeginBoardingHold()
    {
        boardingHoldActive = true;
        boardingHoldElapsed = 0f;
        boardingActor?.BeginBoardingIdle();
        LockActorControls();
    }

    private void CancelBoardingHold()
    {
        if (!boardingHoldActive || boardingCommitted || isAscending)
        {
            return;
        }

        boardingHoldActive = false;
        boardingHoldElapsed = 0f;
        boardingActor?.EndBoardingIdle();
        if (!IsBoxBadImpactPaused)
        {
            UnlockActorControls();
        }
    }

    private void UnlockLeftMovement()
    {
        if (leftMoveUnlocked)
        {
            return;
        }

        leftMoveUnlocked = true;
        if (leftMoveLock != null)
        {
            leftMoveLock.PlayUnlockAndDestroy(null);
            leftMoveLock = null;
        }
    }

    private void LockActorControls()
    {
        if (actor == null)
        {
            return;
        }

        actor.SetMoveInput(0f);
        actor.StopMoving();
        actor.UpdateAnimation();
    }

    private void UnlockActorControls()
    {
        actor?.ResumeMoving();
    }

    private void ResolveMoveButtons()
    {
        if (leftMoveButtonRect != null
            && rightMoveButtonRect != null
            && (leftMoveUnlocked || unlockedMoveDirection == MoveDirection.Left || leftMoveLock != null)
            && (unlockedMoveDirection == MoveDirection.Right || rightMoveLock != null))
        {
            SetMoveLocksVisible(hasStarted || stageCenterLocked);
            return;
        }

        GamePlayPanel panel = FindFirstObjectByType<GamePlayPanel>();
        if (panel == null)
        {
            return;
        }

        if (leftMoveButtonRect == null || rightMoveButtonRect == null)
        {
            RectTransform[] rectTransforms = panel.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rectTransforms.Length; i++)
            {
                if (rectTransforms[i].name == "leftMove_btn")
                {
                    leftMoveButtonRect = rectTransforms[i];
                }
                else if (rectTransforms[i].name == "rightMove_btn")
                {
                    rightMoveButtonRect = rectTransforms[i];
                }
            }
        }

        if (!leftMoveUnlocked && unlockedMoveDirection != MoveDirection.Left && leftMoveLock == null)
        {
            leftMoveLock = CreateMoveButtonLock(leftMoveButtonRect, "Level106_LeftMoveLock", MoveDirection.Left);
        }

        if (unlockedMoveDirection != MoveDirection.Right && rightMoveLock == null)
        {
            rightMoveLock = CreateMoveButtonLock(rightMoveButtonRect, "Level106_RightMoveLock", MoveDirection.Right);
        }

        SetMoveLocksVisible(hasStarted || stageCenterLocked);
    }

    private Level106MoveButtonLock CreateMoveButtonLock(
        RectTransform buttonRect,
        string lockName,
        MoveDirection lockDirection)
    {
        if (buttonRect == null || moveButtonLockPrefab == null)
        {
            return null;
        }

        Level106MoveButtonLock moveLock = Instantiate(moveButtonLockPrefab, buttonRect, false);
        moveLock.name = lockName;
        moveLock.gameObject.layer = buttonRect.gameObject.layer;

        RectTransform lockRect = moveLock.transform as RectTransform;
        if (lockRect != null)
        {
            lockRect.anchorMin = new Vector2(0.5f, 0.5f);
            lockRect.anchorMax = new Vector2(0.5f, 0.5f);
            lockRect.pivot = new Vector2(0.5f, 0.5f);
            lockRect.anchoredPosition = moveButtonLockAnchoredOffset;
            lockRect.SetAsLastSibling();
        }

        moveLock.gameObject.SetActive(
            (hasStarted || stageCenterLocked) && unlockedMoveDirection != lockDirection);
        return moveLock;
    }

    private void SetMoveLocksVisible(bool isVisible)
    {
        if (!leftMoveUnlocked && leftMoveLock != null && unlockedMoveDirection != MoveDirection.Left)
        {
            leftMoveLock.gameObject.SetActive(isVisible);
        }

        if (rightMoveLock != null && unlockedMoveDirection != MoveDirection.Right)
        {
            rightMoveLock.gameObject.SetActive(isVisible);
        }
    }

    private void DestroyMoveLocks()
    {
        DestroyMoveLock(ref leftMoveLock);
        DestroyMoveLock(ref rightMoveLock);
    }

    private void DestroyMoveLock(ref Level106MoveButtonLock moveLock)
    {
        if (moveLock != null)
        {
            Destroy(moveLock.gameObject);
            moveLock = null;
        }
    }

    private MoveDirection ResolveMoveDirection(Vector2 screenPosition)
    {
        if (EventSystem.current != null)
        {
            var pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
            uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(pointerData, uiRaycastResults);
            for (int i = 0; i < uiRaycastResults.Count; i++)
            {
                Transform hitTransform = uiRaycastResults[i].gameObject.transform;
                if (IsButtonOrChild(hitTransform, leftMoveButtonRect))
                {
                    return MoveDirection.Left;
                }

                if (IsButtonOrChild(hitTransform, rightMoveButtonRect))
                {
                    return MoveDirection.Right;
                }
            }
        }

        if (RectangleContainsScreenPoint(leftMoveButtonRect, screenPosition))
        {
            return MoveDirection.Left;
        }

        return RectangleContainsScreenPoint(rightMoveButtonRect, screenPosition)
            ? MoveDirection.Right
            : MoveDirection.None;
    }

    private static bool IsButtonOrChild(Transform hitTransform, RectTransform buttonRect)
    {
        return buttonRect != null
            && (hitTransform == buttonRect || hitTransform.IsChildOf(buttonRect));
    }

    private static bool RectangleContainsScreenPoint(RectTransform buttonRect, Vector2 screenPosition)
    {
        if (buttonRect == null)
        {
            return false;
        }

        Canvas canvas = buttonRect.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(buttonRect, screenPosition, eventCamera);
    }

    private float ResolveUfoDeliveryX()
    {
        if (ufoRoot == null)
        {
            return ResolveStageCenterX();
        }

        Bounds clawBounds = default;
        bool hasClawBounds = false;
        AppendRendererBounds(ufoRoot.Find("UFO_ClawUpper"), ref clawBounds, ref hasClawBounds);
        AppendRendererBounds(ufoRoot.Find("UFO_ClawLower"), ref clawBounds, ref hasClawBounds);
        if (!hasClawBounds)
        {
            return ufoRoot.position.x;
        }

        float clawLeftOffsetX = clawBounds.min.x - ufoRoot.position.x;
        return ResolveStageCenterX() - clawLeftOffsetX;
    }

    private static void AppendRendererBounds(
        Transform target,
        ref Bounds bounds,
        ref bool hasBounds)
    {
        SpriteRenderer renderer = target != null
            ? target.GetComponent<SpriteRenderer>()
            : null;
        if (renderer == null)
        {
            return;
        }

        if (!hasBounds)
        {
            bounds = renderer.bounds;
            hasBounds = true;
        }
        else
        {
            bounds.Encapsulate(renderer.bounds);
        }
    }

    private float ResolveCurrentSupportTopY()
    {
        float supportTopY = stageSupportCollider != null
            ? stageSupportCollider.bounds.max.y
            : stackTopY;

        for (int i = 0; i < stackedObjects.Count; i++)
        {
            GameObject stackedObject = stackedObjects[i];
            Collider2D collider = stackedObject != null
                ? stackedObject.GetComponent<Collider2D>()
                : null;
            if (collider != null && collider.enabled)
            {
                supportTopY = Mathf.Max(supportTopY, collider.bounds.max.y);
            }
        }

        return supportTopY;
    }

    private float ResolveStageCenterX()
    {
        if (triggerStage == null)
        {
            return transform.position.x;
        }

        SpriteRenderer stageRenderer = triggerStage.GetComponent<SpriteRenderer>();
        return stageRenderer != null
            ? stageRenderer.bounds.center.x
            : triggerStage.position.x;
    }

    private float ResolveStackCenterX()
    {
        return stackCenterInitialized ? stackCenterX : ResolveStageCenterX();
    }

    private static float ResolveLayerRendererCenterOffsetX(GameObject layerObject)
    {
        if (layerObject == null)
        {
            return 0f;
        }

        SpriteRenderer renderer = layerObject.GetComponent<SpriteRenderer>();
        return renderer != null ? renderer.bounds.center.x - layerObject.transform.position.x : 0f;
    }

    private float ResolveLayerPivotXForStackCenter(GameObject layerObject)
    {
        return ResolveStackCenterX() - ResolveLayerRendererCenterOffsetX(layerObject);
    }

    private void UpdateStageCenterLock()
    {
        if (!gameplayActive || hasStarted || stageCenterLocked || actor == null
            || triggerStage == null || stageSupportCollider == null
            || !actor.IsGround || !IsActorSupportedBy(stageSupportCollider))
        {
            return;
        }

        float centerX = ResolveStackCenterX();
        float actorCenterX = actorCollider != null ? actorCollider.bounds.center.x : actor.transform.position.x;
        if (actorCenterX < centerX - stageCenterLockTolerance)
        {
            return;
        }

        if (actorBody != null)
        {
            float colliderCenterOffsetX = actorCollider.bounds.center.x - actorBody.position.x;
            Vector2 position = actorBody.position;
            position.x = centerX - colliderCenterOffsetX;
            actorBody.position = position;
            actorBody.linearVelocity = new Vector2(0f, actorBody.linearVelocity.y);
        }
        else
        {
            float colliderCenterOffsetX = actorCollider != null
                ? actorCollider.bounds.center.x - actor.transform.position.x
                : 0f;
            Vector3 position = actor.transform.position;
            position.x = centerX - colliderCenterOffsetX;
            actor.transform.position = position;
        }

        stageCenterLocked = true;
        SetActorShadowVisible(false);
        ResolveMoveButtons();
        SetMoveLocksVisible(true);
        levelCtrl?.SetCtrlable(false);
        StopActorHorizontalMovement();
        StartDeliverySequence();
    }

    private void SetActorShadowVisible(bool visible)
    {
        if (actorShadow == null)
        {
            return;
        }

        if (!visible)
        {
            if (!actorShadowSuppressed)
            {
                actorShadowRendererStates.Clear();
                SpriteRenderer[] renderers = actorShadow.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    SpriteRenderer renderer = renderers[i];
                    if (renderer != null)
                    {
                        actorShadowRendererStates[renderer] = renderer.enabled;
                    }
                }

                actorShadowSuppressed = true;
            }

            if (actorShadowController != null)
            {
                actorShadowController.enabled = false;
            }

            foreach (SpriteRenderer renderer in actorShadowRendererStates.Keys)
            {
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }

            actorShadow.SetActive(false);
            return;
        }

        actorShadow.SetActive(actorShadowInitialActive);
        foreach (KeyValuePair<SpriteRenderer, bool> state in actorShadowRendererStates)
        {
            if (state.Key != null)
            {
                state.Key.enabled = state.Value;
            }
        }

        if (actorShadowController != null)
        {
            actorShadowController.enabled = actorShadowControllerInitialEnabled;
        }

        actorShadowRendererStates.Clear();
        actorShadowSuppressed = false;
    }

    private float ResolveStackOverlap()
    {
        return completedLayers <= 0 ? 0f : StackOverlapY;
    }

    private static float ResolveColliderWorldCenterOffsetY(BoxCollider2D collider)
    {
        if (collider == null)
        {
            return 0f;
        }

        return collider.transform.TransformPoint(collider.offset).y
            - collider.transform.position.y;
    }

    private static float ResolveColliderWorldHeight(BoxCollider2D collider)
    {
        if (collider == null)
        {
            return 0f;
        }

        Vector3 localSize = new Vector3(collider.size.x, collider.size.y, 0f);
        Vector3 worldSize = Vector3.Scale(localSize, collider.transform.lossyScale);
        float radians = collider.transform.eulerAngles.z * Mathf.Deg2Rad;
        return Mathf.Abs(Mathf.Cos(radians)) * Mathf.Abs(worldSize.y)
            + Mathf.Abs(Mathf.Sin(radians)) * Mathf.Abs(worldSize.x);
    }

    private float ResolveFinalAscentRootY()
    {
        if (ufoRoot == null || ufoBodyCollider == null || exitPlatformCollider == null)
        {
            Debug.LogWarning("[Level106Ctrl] Exit platform alignment references are missing.");
            return deliveryRestPosition.y;
        }

        return ufoRoot.position.y
            + ufoExitSupportY
            - ufoBodyCollider.bounds.max.y;
    }

    private void BeginAscent()
    {
        if (boardingCommitted
            || ufoRoot == null
            || actor == null
            || !actor.IsGround
            || !deliveryPausedForBoarding
            || boxBadCtrl == null
            || !boxBadCtrl.HasKnockedDown
            || !IsActorCenteredAndSupportedBy(ufoBodyCollider))
        {
            return;
        }

        CenterActorColliderOn(ufoBodyCollider);
        ufoExitSelected = true;
        climbLoop?.BeginUfoAscent();
        UpdateExitVisibility();
        boardingCommitted = true;
        boardingHoldActive = false;
        boardingHoldElapsed = 0f;
        waitingForUfoBoarding = false;
        isAscending = true;
        deliveryPausedForBoarding = true;
        activeSequence?.Kill(false);
        activeSequence = null;
        ufoRoot.DOKill(false);
        SetDeliveryMovementActive(false);
        finalAscentRootY = ResolveFinalAscentRootY();
        lastUfoPosition = ufoRoot.position;
        ascentTargetY = finalAscentRootY;
        boardingActor?.BeginBoardingIdle();
        LockActorControls();
        AttachActorToUfo();
        cameraFollowArea?.BeginAscentFollow(ufoRoot);
        levelCtrl?.SetCtrlable(false);
        StartRemainingLayersCountEffect();

        activeSequence = DOTween.Sequence()
            .Append(ufoRoot.DOMoveY(ascentTargetY, finalAscentDuration)
                .SetEase(Ease.Linear)
                .OnUpdate(SyncActorWithUfo))
            .OnComplete(() =>
            {
                activeSequence = null;
                isAscending = false;
                boardingCommitted = false;
                boardingHoldActive = false;
                boardingHoldElapsed = 0f;
                remainingLayersReachedTop = true;
                CompleteRemainingLayersCountEffect();
                ReleaseBoardingPresentation(resumePhysics: true, updateAnimation: true, syncFinalCamera: true);
                UnlockActorControls();
                levelCtrl?.SetCtrlable(true);
                StopActorHorizontalMovement();
            });
    }

    private bool IsActorSupportedBy(Collider2D supportCollider)
    {
        if (supportCollider == null || actorCollider == null)
        {
            return false;
        }

        Bounds actorBounds = actorCollider.bounds;
        Bounds supportBounds = supportCollider.bounds;
        return actorBounds.max.x > supportBounds.min.x
            && actorBounds.min.x < supportBounds.max.x
            && actorBounds.min.y >= supportBounds.max.y - 0.25f
            && actorBounds.min.y <= supportBounds.max.y + 0.4f;
    }

    private bool IsActorCenteredAndSupportedBy(Collider2D supportCollider)
    {
        if (supportCollider == null || actorCollider == null)
        {
            return false;
        }

        Bounds actorBounds = actorCollider.bounds;
        Bounds supportBounds = supportCollider.bounds;
        float horizontalCenterDistance = Mathf.Abs(actorBounds.center.x - supportBounds.center.x);
        return horizontalCenterDistance <= Mathf.Max(0f, boardingCenterTolerance)
            && actorBounds.min.y >= supportBounds.max.y - 0.12f
            && actorBounds.min.y <= supportBounds.max.y + 0.2f;
    }

    private void CenterActorColliderOn(Collider2D supportCollider)
    {
        if (supportCollider == null || actorCollider == null || actor == null)
        {
            return;
        }

        float targetCenterX = supportCollider.bounds.center.x;
        if (actorBody != null)
        {
            float colliderCenterOffsetX = actorCollider.bounds.center.x - actorBody.position.x;
            Vector2 position = actorBody.position;
            position.x = targetCenterX - colliderCenterOffsetX;
            actorBody.position = position;
            actorBody.linearVelocity = new Vector2(0f, actorBody.linearVelocity.y);
            return;
        }

        float transformCenterOffsetX = actorCollider.bounds.center.x - actor.transform.position.x;
        Vector3 transformPosition = actor.transform.position;
        transformPosition.x = targetCenterX - transformCenterOffsetX;
        actor.transform.position = transformPosition;
    }

    private void AttachActorToUfo()
    {
        if (actorAttachedToUfo || actor == null || ufoRoot == null)
        {
            return;
        }

        actorInitialParent = actor.transform.parent;
        if (actorBody != null)
        {
            actorInitialBodyType = actorBody.bodyType;
            actorInitialGravityScale = actorBody.gravityScale;
            actorInitialSimulated = actorBody.simulated;
            actorInitialInterpolation = actorBody.interpolation;
            actorUfoPhysicsCaptured = true;
            // 父级 Tween 是飞升唯一位移来源。暂停独立物理与插值，避免刚体旧姿态拉回乘员。
            actorBody.simulated = false;
            actorBody.interpolation = RigidbodyInterpolation2D.None;
            actorBody.bodyType = RigidbodyType2D.Kinematic;
            actorBody.gravityScale = 0f;
            actorBody.linearVelocity = Vector2.zero;
            actorBody.angularVelocity = 0f;
        }

        actor.transform.SetParent(ufoRoot, true);
        ApplyActorUfoPassengerSorting();
        actorAttachedToUfo = true;
    }

    private void ReleaseBoardingPresentation(bool resumePhysics, bool updateAnimation, bool syncFinalCamera = false)
    {
        cameraFollowArea?.EndAscentFollow(syncFinalCamera);
        RestoreActorFromUfo(resumePhysics);
        boardingActor?.EndBoardingIdle(updateAnimation);
    }

    private void RestoreActorFromUfo(bool resumePhysics = true)
    {
        if ((!actorAttachedToUfo && !actorUfoPhysicsCaptured) || actor == null)
        {
            return;
        }

        Vector3 worldPosition = actor.transform.position;
        if (actorAttachedToUfo)
            actor.transform.SetParent(actorInitialParent, true);

        if (actorBody != null)
        {
            actorBody.position = worldPosition;
            actorBody.linearVelocity = Vector2.zero;
            actorBody.angularVelocity = 0f;
            if (resumePhysics)
            {
                actorBody.bodyType = actorInitialBodyType;
                actorBody.gravityScale = actorInitialGravityScale;
                actorBody.interpolation = actorInitialInterpolation;
                actorBody.simulated = actorInitialSimulated;
                actorUfoPhysicsCaptured = false;
            }
            // teardown 的旧实例只解除所有权并原地停机，不在销毁等待期恢复物理或操作。
        }

        RestoreActorSortingFromUfo();
        actorAttachedToUfo = false;
    }

    private void ApplyActorUfoPassengerSorting()
    {
        if (actorSortingGroup == null && actor != null)
        {
            actorSortingGroup = actor.GetComponent<SortingGroup>();
        }

        if (actorSortingGroup == null)
        {
            return;
        }

        actorSortingLayerIdBeforeUfo = actorSortingGroup.sortingLayerID;
        actorSortingOrderBeforeUfo = actorSortingGroup.sortingOrder;
        actorSortingCapturedForUfo = true;
        actorSortingGroup.sortingLayerName = UfoPassengerSortingLayerName;
        actorSortingGroup.sortingOrder = UfoPassengerSortingOrder;
    }

    private void RestoreActorSortingFromUfo()
    {
        if (!actorSortingCapturedForUfo)
        {
            return;
        }

        if (actorSortingGroup == null && actor != null)
        {
            actorSortingGroup = actor.GetComponent<SortingGroup>();
        }

        if (actorSortingGroup != null)
        {
            actorSortingGroup.sortingLayerID = actorSortingLayerIdBeforeUfo;
            actorSortingGroup.sortingOrder = actorSortingOrderBeforeUfo;
        }

        actorSortingCapturedForUfo = false;
    }

    private void StopActorHorizontalMovement()
    {
        if (actor == null)
        {
            return;
        }

        actor.SetMoveInput(0f);
        actor.UpdateAnimation();
    }

    private void StartRemainingLayersCountEffect()
    {
        StopRemainingLayersCountEffect(false);
        if (remainingLayersText == null)
        {
            return;
        }

        int startValue = Mathf.Max(0, TotalLayers - completedLayers);
        int pulseCount = Mathf.Max(1, remainingLayersPulseCount);
        float progress = 0f;
        remainingLayersAnimator = new DOTweenTMPAnimator(remainingLayersText);
        remainingLayersCountTween = DOTween.To(
                () => progress,
                value =>
                {
                    progress = value;
                    int displayValue = Mathf.RoundToInt(Mathf.Lerp(startValue, 0f, value));
                    float pulse = Mathf.Abs(Mathf.Sin(value * Mathf.PI * pulseCount));
                    ApplyRemainingLayersText(displayValue, pulse);
                },
                1f,
                finalAscentDuration)
            .SetEase(Ease.Linear)
            .SetLink(gameObject);

        if (!gameplayActive)
        {
            remainingLayersCountTween.Pause();
        }
    }

    private void CompleteRemainingLayersCountEffect()
    {
        remainingLayersCountTween?.Complete();
        remainingLayersCountTween = null;
        DisposeRemainingLayersAnimator();
        ApplyRemainingLayersText(0);
    }

    private void StopRemainingLayersCountEffect(bool restoreLogicalValue)
    {
        remainingLayersCountTween?.Kill(false);
        remainingLayersCountTween = null;
        DisposeRemainingLayersAnimator();
        if (restoreLogicalValue)
        {
            UpdateRemainingLayersText();
        }
    }

    private void DisposeRemainingLayersAnimator()
    {
        if (remainingLayersAnimator == null)
        {
            return;
        }

        remainingLayersAnimator.Reset();
        remainingLayersAnimator.Dispose();
        remainingLayersAnimator = null;
    }

    private void UpdateRemainingLayersText()
    {
        int remainingLayers = remainingLayersReachedTop
            ? 0
            : Mathf.Max(0, TotalLayers - completedLayers);
        ApplyRemainingLayersText(remainingLayers);
    }

    private void ApplyRemainingLayersText(int remainingLayers, float digitPulse = 0f)
    {
        if (remainingLayersText == null)
        {
            return;
        }

        string number = remainingLayers.ToString();
        if (remainingLayersParamsManager != null)
        {
            remainingLayersParamsManager.SetParameterValue(RemainingLayersParameter, number);
        }

        if (remainingLayersText.text.IndexOf(number, StringComparison.Ordinal) < 0)
        {
            remainingLayersText.text = RemainingLayersFallbackPrefix + number + RemainingLayersFallbackSuffix;
        }

        remainingLayersText.ForceMeshUpdate(true, true);
        int numberStartIndex = remainingLayersText.text.IndexOf(number, StringComparison.Ordinal);

        if (remainingLayersAnimator == null || numberStartIndex < 0)
        {
            return;
        }

        remainingLayersAnimator.Refresh();
        float pulse = Mathf.Clamp01(digitPulse);
        Vector3 scale = Vector3.one * Mathf.Lerp(1f, 1.15f, pulse);
        Color32 baseColor = remainingLayersText.color;
        Color32 digitColor = Color32.Lerp(baseColor, new Color32(255, 255, 255, 255), pulse);
        for (int i = 0; i < number.Length; i++)
        {
            int charIndex = numberStartIndex + i;
            remainingLayersAnimator.SetCharScale(charIndex, scale);
            remainingLayersAnimator.SetCharColor(charIndex, digitColor);
        }
    }
}

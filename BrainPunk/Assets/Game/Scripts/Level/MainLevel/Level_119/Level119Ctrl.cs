using System;
using System.Collections;
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

/// <summary>
/// Level 119 UFO 送层编排器。玩家在真实地面上到达视觉地毯中心后持续锁定横向位置，
/// 每件物件必须从上方踩下落定；保留弹窗捷径，真实满层后开放场景诱导出口。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level119Ctrl : MonoBehaviour, IEventSender
{
    private const string RemainingLayersTerm = "S_LevelTips_Des_level_106_1";
    private const int TotalLayers = 999;
    private const int AccelerationStartLayer = 5;
    private const float StackOverlapY = 0.4f;
    private static readonly Vector2 DeliveryHitKnockbackDirection = new Vector2(-0.5f, 0.5f);
    private const float DeliveryHitKnockbackForce = 50f;
    private const float DeliveryHitRotationDuration = 0.5f;
    private static readonly Vector2 PopupCameraFollowAreaCenter = new Vector2(0.5f, 0f);
    private static readonly Vector2 PopupCameraFollowAreaSize = new Vector2(19f, 10.63f);
    private static readonly Vector2 DeliveryCameraFollowAreaCenter = new Vector2(0.5f, -0.351f);
    private static readonly Vector2 DeliveryCameraFollowAreaSize = new Vector2(19f, 0.01f);
    private const string LeftMoveButtonPath = "Root/leftMove_btn";
    private const string RightMoveButtonPath = "Root/rightMove_btn";

    private enum PlayerInputKind
    {
        None,
        Left,
        Right,
        Jump
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
    [SerializeField] private Collider2D ufoRodCollider;
    [SerializeField] private Transform carryAnchor;
    [SerializeField] private Transform triggerStage;
    [SerializeField] private Transform stageSupport;
    [SerializeField] private Transform stackRoot;
    [SerializeField] private Transform inducedExitPlatform;
    [SerializeField] private SuccessAnimCtrl inducedExitDoor;
    [SerializeField] private Level119ClimbLoop climbLoop;
    [SerializeField] private TMP_Text remainingLayersText;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private ParticleSystem knockEffect;

    [Header("送层素材")]
    [Tooltip("全部送层使用的随机素材池；小球只是普通池项。")]
    [SerializeField] private Sprite[] randomLayerSprites;
    [Tooltip("Sp_mcqh_1_03 使用时横向摆放并缩小。")]
    [SerializeField] private Sprite horizontalLayerSprite;
    [SerializeField, Min(0.01f)] private float horizontalLayerScale = 0.6f;

    [Header("送层节奏")]
    [SerializeField, Min(0.01f)] private float ufoMoveDuration = 1.5f;
    [SerializeField, Min(0.01f)] private float ufoReturnDuration = 1.6f;
    [SerializeField, Min(0.01f)] private float itemTravelDuration = 0.12f;
    [SerializeField, Min(0f)] private float firstLayerInterval = 0.8f;
    [SerializeField, Min(0f)] private float finalLayerInterval = 0.4f;
    [SerializeField, Min(0f)] private float layerSpeedIncrement = 0.4f;
    [SerializeField, Min(1f)] private float maxLayerSpeedMultiplier = 10f;
    [SerializeField, Min(0.01f)] private float ascentSpeed = 2.7f;

    [Header("舞台中心锁定")]
    [SerializeField, Min(0f)] private float stageCenterLockTolerance = 0.08f;
    [SerializeField] private LayerVisualProfile[] layerVisualProfiles;
    [SerializeField] private string carrySortingLayerName = "Default";
    [SerializeField] private int carrySortingOrder = 12;
    [SerializeField] private string stackedSortingLayerName = "SceneLayer";
    [SerializeField, Min(0)] private int layerSortingOrder = 202;

    private EventSubscriber eventSubscriber;
    private Coroutine resumeStateCoroutine;
    private Sequence activeSequence;
    private Level119FakeTipsPopup fakeTipsPopup;
    private Level119FakeTipsPopup shadowSuppressingPopup;
    private readonly List<GameObject> stackedObjects = new List<GameObject>();
    private List<Sprite> layerSequence;
    private double[] remainingLayerRise;
    private Collider2D[] inducedDoorColliders;
    private bool[] inducedDoorColliderStates;
    private Collider2D actorCollider;
    private Collider2D stageSupportCollider;
    private Rigidbody2D actorBody;
    private GameObject actorShadow;
    private ShadowController actorShadowController;
    private bool actorShadowInitialActive;
    private bool actorShadowControllerInitialEnabled;
    private bool actorShadowSuppressed;
    private readonly Dictionary<SpriteRenderer, bool> actorShadowRendererStates = new Dictionary<SpriteRenderer, bool>();
    private Level119CameraFollowArea cameraFollowArea;
    private Tween actorDeliveryRotationTween;
    private Quaternion actorDeliveryKnockbackStartRotation;
    private bool actorDeliveryKnockbackActive;
    private Vector3 initialUfoPosition;
    private Vector3 deliveryRestPosition;
    private int lastDisplayedRemainingLayers = -1;
    private float stackTopY;
    private float deliveryWorkY;
    private float lastSettledLayerSceneHeight;
    private float stackCenterX;
    private int completedLayers;
    private bool hasStarted;
    private bool stageCenterLocked;
    private bool currentLayerSettling;
    private bool deathTriggered;
    private bool gameplayActive;
    private bool gamePaused;
    private bool applicationPaused;
    private bool applicationUnfocused;
    private bool statePlaying;
    private bool popupClosed;
    private bool popupDoorCommitted;
    private bool popupSuccessCommitted;
    private bool inducedDoorCommitted;
    private bool fullClimbMovementUnlocked;
    private bool popupTriggerPending;
    private bool popupOpening;
    private PlayerInputKind pendingPopupTriggerInput;
    private int popupInputCutFrame = -1;
    private bool leftInputBlockedUntilRelease;
    private bool rightInputBlockedUntilRelease;
    private GamePlayPanel trackedInputPanel;
    private CustomButton trackedLeftMoveButton;
    private CustomButton trackedRightMoveButton;
    private bool uiLeftHeld;
    private bool uiRightHeld;
    private bool keyboardLeftHeld;
    private bool keyboardRightHeld;
    private bool baseInputFrameSuppressed;
    private ActorCtrl suppressedBaseActor;
    private bool suppressedBaseEnabled;
    private bool suppressedActorVelocityCaptured;
    private bool clearSuppressedActorHorizontalVelocity;
    private Vector2 suppressedActorVelocity;
    private float suppressedActorAngularVelocity;

    private GameObject currentLayerObject;
    private Transform currentLayerParent;
    private float currentLayerReturnDuration;
    private float currentLayerObjectDuration;
    private Vector3 lastLayerPosition;

    private bool IsDoorCommitted => popupDoorCommitted || inducedDoorCommitted;

    private void Awake()
    {
        levelCtrl ??= GetComponent<BaseLevelCtrl>();
        actor ??= levelCtrl != null ? levelCtrl.Actor : null;
        ufoRoot ??= transform.Find("UFO_Ascending");
        carryAnchor ??= ufoRoot != null ? ufoRoot.Find("UFO_CarryAnchor") : null;
        ufoRodCollider ??= ufoRoot != null ? ufoRoot.GetComponent<Collider2D>() : null;
        triggerStage ??= transform.Find("UFO_TriggerStage");
        stageSupport ??= transform.Find("Ground_long");
        stageSupportCollider = stageSupport != null ? stageSupport.GetComponent<Collider2D>() : null;
        stackRoot ??= transform.Find("UFO_StackedLayers");
        inducedExitPlatform ??= transform.Find("TopExitPlatform");
        inducedExitDoor ??= transform.Find("door_0")?.GetComponent<SuccessAnimCtrl>();
        climbLoop ??= GetComponent<Level119ClimbLoop>();
        Collider2D inducedSupport = inducedExitPlatform != null
            ? inducedExitPlatform.GetComponent<Collider2D>()
            : null;
        float inducedExitSupportY = inducedSupport != null ? inducedSupport.bounds.max.y : 194f;
        inducedDoorColliders = inducedExitDoor != null
            ? inducedExitDoor.GetComponentsInChildren<Collider2D>(true)
            : Array.Empty<Collider2D>();
        inducedDoorColliderStates = new bool[inducedDoorColliders.Length];
        for (int i = 0; i < inducedDoorColliders.Length; i++)
        {
            inducedDoorColliderStates[i] = inducedDoorColliders[i].enabled;
        }

        if (actor != null)
        {
            actorCollider = actor.GetComponent<Collider2D>();
            actorBody = actor.GetComponent<Rigidbody2D>();
            actorShadow = actor.transform.Find("Shadow")?.gameObject;
            actorShadowController = actorShadow != null ? actorShadow.GetComponent<ShadowController>() : null;
            actorShadowInitialActive = actorShadow != null && actorShadow.activeSelf;
            actorShadowControllerInitialEnabled = actorShadowController != null && actorShadowController.enabled;
        }

        if (ufoRoot != null)
        {
            initialUfoPosition = ufoRoot.position;
            deliveryRestPosition = initialUfoPosition;
            deliveryWorkY = initialUfoPosition.y;
        }

        stackCenterX = ResolveStageCenterX();
        targetCamera ??= Camera.main;
        cameraFollowArea = GetComponent<Level119CameraFollowArea>();
        ApplyPopupCameraProfile();
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

        // Keep the level-specific interception ahead of the shared input handlers.
        levelCtrl?.AddListener();

        LocalizationManager.OnLocalizeEvent += OnRemainingLayersLanguageChanged;
        gameplayActive = IsGameplayPlaying();
        InitializeRuntimeState();
    }

    private void Update()
    {
        RefreshPlayerInputSources();
        ReleaseNeutralInputBlocks();
        ApplyPopupDirectionalInput();
        TryOpenPendingFakeTipsPopup();
        UpdateStageCenterLock();
        MaintainStageCenterLock();
    }

    private void LateUpdate()
    {
        RestoreBaseInputAfterCut();
        UpdateExitVisibility();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLocalizeEvent -= OnRemainingLayersLanguageChanged;
        BeginTeardown();
    }

    private void OnGameReset(GameResetEvent evt)
    {
        BeginTeardown();
    }

    private void BeginTeardown()
    {
        RestoreBaseInputAfterCut();
        statePlaying = false;
        gameplayActive = false;
        hasStarted = false;
        climbLoop?.SetSuspended(true);
        SetInducedDoorCollision(false);
        CancelPendingPopupTrigger();
        CloseFakeTipsPopup();
        ResetInterruptedInputState(false, true);
        UnbindTrackedInputPanel();
        shadowSuppressingPopup = null;
        SetActorShadowVisible(true);
        EventsUtils.ResetEvents(ref eventSubscriber);
        if (resumeStateCoroutine != null)
        {
            StopCoroutine(resumeStateCoroutine);
            resumeStateCoroutine = null;
        }
        KillDeliveryMotion();
        actorDeliveryRotationTween?.Kill(false);
        actorDeliveryRotationTween = null;
        actorDeliveryKnockbackActive = false;
        SetUfoRodActive(false);
        StopKnockEffect();
        levelCtrl?.SetCtrlable(false);
        StopActorHorizontalMovement();
    }

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        HandlePlayerInput(PlayerInputKind.Jump);
    }

    private void OnPlayerRightMove(PlayerRightMoveEvent evt)
    {
        HandlePlayerInput(PlayerInputKind.Right);
    }

    private void OnPlayerLeftMove(PlayerLeftMoveEvent evt)
    {
        HandlePlayerInput(PlayerInputKind.Left);
    }

    private void OnPlayerMoveCancel(PlayerMoveCancelEvent evt)
    {
        RefreshPlayerInputSources();
        ReleaseNeutralInputBlocks();
        if (TryResolvePopupDirectionalInput(out float input) && !Mathf.Approximately(input, 0f))
        {
            SuppressBaseInputForCurrentFrame(clearHorizontalVelocity: false);
        }

        ApplyPopupDirectionalInput();
    }

    private void OnTriggerTrap(OnTriggerTrapEvent evt)
    {
        RestoreBaseInputAfterCut();
        if (IsDoorCommitted)
        {
            return;
        }

        deathTriggered = true;
        CancelPendingPopupTrigger();
        ResetInterruptedInputState(true, true);
        FreezeDeliveryForFailure();
        fakeTipsPopup?.CancelForFailure();
        CloseFakeTipsPopup();
    }

    private void OnTriggerFailArea(OnTriggerFailAreaEvent evt)
    {
        OnTriggerTrap(null);
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        RestoreBaseInputAfterCut();
        gamePaused = true;
        CutHeldGameplayInputsForLifecycle();
        RefreshRuntimeSuspension();
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        RestoreBaseInputAfterCut();
        gamePaused = false;
        if (resumeStateCoroutine != null)
            StopCoroutine(resumeStateCoroutine);
        resumeStateCoroutine = StartCoroutine(RefreshPlayingStateAfterResume());
    }

    private IEnumerator RefreshPlayingStateAfterResume()
    {
        yield return null;
        resumeStateCoroutine = null;
        if (!isActiveAndEnabled)
            yield break;

        statePlaying = IsGameplayPlaying();
        RefreshRuntimeSuspension();
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt != null
            && (evt.NewState == GamePlayStateName.SUCCESS || evt.NewState == GamePlayStateName.FAILED))
        {
            CancelPendingPopupTrigger();
            ResetInterruptedInputState(evt.NewState == GamePlayStateName.FAILED, true);
            CloseFakeTipsPopup();
        }
        else if (evt != null && evt.NewState != GamePlayStateName.PLAYING)
        {
            CutHeldGameplayInputsForLifecycle();
        }

        statePlaying = evt != null && evt.NewState == GamePlayStateName.PLAYING;
        RefreshRuntimeSuspension();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        applicationUnfocused = !hasFocus;
        if (applicationUnfocused)
        {
            CutHeldGameplayInputsForLifecycle();
        }
        RefreshRuntimeSuspension();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        applicationPaused = pauseStatus;
        if (applicationPaused)
        {
            CutHeldGameplayInputsForLifecycle();
        }
        RefreshRuntimeSuspension();
    }

    private void RefreshRuntimeSuspension()
    {
        bool suspended = gamePaused || applicationPaused || applicationUnfocused || !statePlaying;
        fakeTipsPopup?.SetSuspended(suspended);
        climbLoop?.SetSuspended(suspended || !hasStarted || deathTriggered || IsDoorCommitted);
        if (suspended)
        {
            gameplayActive = false;
            SetInducedDoorCollision(false);
            activeSequence?.Pause();
            actor?.StopMoving();
            return;
        }

        if (deathTriggered || IsDoorCommitted || popupSuccessCommitted)
        {
            return;
        }

        gameplayActive = true;
        activeSequence?.Play();
        actor?.ResumeMoving();
        levelCtrl?.SetCtrlable(!stageCenterLocked);
        TryOpenPendingFakeTipsPopup();
    }

    private void InitializeRuntimeState()
    {
        RestoreBaseInputAfterCut();
        RefreshPlayerInputSources();
        ResetInterruptedInputState(true, true);
        KillDeliveryMotion();

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
        currentLayerSettling = false;
        layerSequence = null;
        remainingLayerRise = null;
        completedLayers = 0;
        hasStarted = false;
        stageCenterLocked = false;
        deathTriggered = false;
        gamePaused = false;
        applicationPaused = false;
        applicationUnfocused = false;
        statePlaying = IsGameplayPlaying();
        popupClosed = false;
        popupDoorCommitted = false;
        popupSuccessCommitted = false;
        inducedDoorCommitted = false;
        fullClimbMovementUnlocked = false;
        popupTriggerPending = false;
        popupOpening = false;
        pendingPopupTriggerInput = PlayerInputKind.None;
        shadowSuppressingPopup = null;
        ResetActorDeliveryKnockback();
        SetActorShadowVisible(true);
        lastDisplayedRemainingLayers = -1;
        gameplayActive = statePlaying;

        if (ufoRoot != null)
        {
            ufoRoot.position = initialUfoPosition;
            deliveryRestPosition = initialUfoPosition;
            deliveryWorkY = initialUfoPosition.y;
        }

        lastSettledLayerSceneHeight = 0f;
        stackTopY = stageSupportCollider != null ? stageSupportCollider.bounds.max.y : -1.44f;
        stackCenterX = ResolveStageCenterX();
        SetUfoRodActive(false);
        StopKnockEffect();
        ApplyPopupCameraProfile();
        cameraFollowArea?.SetCanFollow(true);
        levelCtrl?.SetSuccessAnim(null);
        levelCtrl?.SetCtrlable(true);
        actor?.ResumeMoving();
        UpdateRemainingLayersText();
        climbLoop?.SetSuspended(true);
        UpdateExitVisibility();
    }

    private bool IsGameplayPlaying()
    {
        return PlayCtrl.Instance == null || PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
    }

    private void HandlePlayerInput(PlayerInputKind inputKind)
    {
        RefreshPlayerInputSources();
        ReleaseNeutralInputBlocks();
        if (ShouldCutPlayerInput(inputKind))
        {
            SuppressBaseInputForCurrentFrame(clearHorizontalVelocity: true);
            return;
        }

        TryShowFakeTipsPopup(inputKind);
    }

    private void TryShowFakeTipsPopup(PlayerInputKind inputKind)
    {
        if (!isActiveAndEnabled
            || popupClosed
            || fakeTipsPopup != null
            || popupTriggerPending
            || popupOpening
            || IsDoorCommitted
            || popupSuccessCommitted
            || deathTriggered)
        {
            return;
        }

        popupTriggerPending = true;
        pendingPopupTriggerInput = inputKind;
        TryOpenPendingFakeTipsPopup();
    }

    private void TryOpenPendingFakeTipsPopup()
    {
        if (!popupTriggerPending || popupOpening)
        {
            return;
        }

        if (!isActiveAndEnabled
            || popupClosed
            || fakeTipsPopup != null
            || IsDoorCommitted
            || popupSuccessCommitted
            || deathTriggered)
        {
            CancelPendingPopupTrigger();
            return;
        }

        if (!gameplayActive || !statePlaying)
        {
            return;
        }

        popupOpening = true;
        CloseFakeTipsPopup();
        Level119FakeTipsPopup createdPopup = Level119FakeTipsPopup.Create();
        if (createdPopup == null)
        {
            CancelPendingPopupTrigger();
            return;
        }

        PlayerInputKind triggerInput = pendingPopupTriggerInput;
        bool popupConfigured;
        try
        {
            popupConfigured = createdPopup.Configure(this, levelCtrl, actor, targetCamera);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, createdPopup);
            popupConfigured = false;
        }

        if (!popupConfigured)
        {
            createdPopup.CloseFromOwner();
            CancelPendingPopupTrigger();
            return;
        }

        fakeTipsPopup = createdPopup;
        popupTriggerPending = false;
        popupOpening = false;
        pendingPopupTriggerInput = PlayerInputKind.None;
        shadowSuppressingPopup = fakeTipsPopup;
        RefreshActorShadowVisibility();
        ApplyPopupCameraProfile();
        fakeTipsPopup.SetSuspended(!gameplayActive);
        BeginFirstOperationCut(triggerInput);
    }

    private void CancelPendingPopupTrigger()
    {
        popupTriggerPending = false;
        popupOpening = false;
        pendingPopupTriggerInput = PlayerInputKind.None;
    }

    private void BeginFirstOperationCut(PlayerInputKind triggerInput)
    {
        RefreshPlayerInputSources();
        leftInputBlockedUntilRelease |= triggerInput == PlayerInputKind.Left || IsLeftInputHeld();
        rightInputBlockedUntilRelease |= triggerInput == PlayerInputKind.Right || IsRightInputHeld();
        trackedInputPanel?.ResetState();
        actor?.SetMoveInput(0f);
        popupInputCutFrame = Time.frameCount;
        SuppressBaseInputForCurrentFrame(clearHorizontalVelocity: true);
    }

    private bool ShouldCutPlayerInput(PlayerInputKind inputKind)
    {
        if (Time.frameCount == popupInputCutFrame)
        {
            return true;
        }

        return inputKind switch
        {
            PlayerInputKind.Left => leftInputBlockedUntilRelease,
            PlayerInputKind.Right => rightInputBlockedUntilRelease,
            _ => false,
        };
    }

    private void RefreshPlayerInputSources()
    {
        GamePlayPanel currentPanel = PlayCtrl.Instance != null
            ? PlayCtrl.Instance.CurrentGamePlayPanel
            : null;
        if (currentPanel != trackedInputPanel)
        {
            BindTrackedInputPanel(currentPanel);
        }

        float horizontalAxis = Input.GetAxisRaw("Horizontal");
        keyboardRightHeld = Input.GetKey(KeyCode.RightArrow)
            || Input.GetKey(KeyCode.D)
            || horizontalAxis > 0.5f;
        keyboardLeftHeld = Input.GetKey(KeyCode.LeftArrow)
            || Input.GetKey(KeyCode.A)
            || horizontalAxis < -0.5f;
    }

    private void BindTrackedInputPanel(GamePlayPanel panel)
    {
        UnbindTrackedInputPanel();
        trackedInputPanel = panel;
        if (trackedInputPanel == null)
        {
            return;
        }

        trackedLeftMoveButton = ResolveTrackedButton(trackedInputPanel.transform, LeftMoveButtonPath);
        trackedRightMoveButton = ResolveTrackedButton(trackedInputPanel.transform, RightMoveButtonPath);
        BindDirectionButton(
            trackedLeftMoveButton,
            OnLeftInputPressed,
            OnLeftInputReleased);
        BindDirectionButton(
            trackedRightMoveButton,
            OnRightInputPressed,
            OnRightInputReleased);
    }

    private void UnbindTrackedInputPanel()
    {
        UnbindDirectionButton(
            trackedLeftMoveButton,
            OnLeftInputPressed,
            OnLeftInputReleased);
        UnbindDirectionButton(
            trackedRightMoveButton,
            OnRightInputPressed,
            OnRightInputReleased);
        trackedLeftMoveButton = null;
        trackedRightMoveButton = null;
        trackedInputPanel = null;
        uiLeftHeld = false;
        uiRightHeld = false;
    }

    private static CustomButton ResolveTrackedButton(Transform panelRoot, string path)
    {
        Transform buttonTransform = panelRoot != null ? panelRoot.Find(path) : null;
        return buttonTransform != null ? buttonTransform.GetComponent<CustomButton>() : null;
    }

    private static void BindDirectionButton(
        CustomButton button,
        CustomButtonClick pressed,
        CustomButtonClick released)
    {
        if (button == null)
        {
            return;
        }

        button.OnClickDown -= pressed;
        button.OnClickEnter -= pressed;
        button.OnClickUp -= released;
        button.OnClickExit -= released;
        button.OnClickDown += pressed;
        button.OnClickEnter += pressed;
        button.OnClickUp += released;
        button.OnClickExit += released;
    }

    private static void UnbindDirectionButton(
        CustomButton button,
        CustomButtonClick pressed,
        CustomButtonClick released)
    {
        if (button == null)
        {
            return;
        }

        button.OnClickDown -= pressed;
        button.OnClickEnter -= pressed;
        button.OnClickUp -= released;
        button.OnClickExit -= released;
    }

    private void OnLeftInputPressed(CustomButton _)
    {
        uiLeftHeld = true;
        ApplyPopupDirectionalInput();
    }

    private void OnLeftInputReleased(CustomButton _)
    {
        uiLeftHeld = false;
        ReleaseNeutralInputBlocks();
        ApplyPopupDirectionalInput();
    }

    private void OnRightInputPressed(CustomButton _)
    {
        uiRightHeld = true;
        ApplyPopupDirectionalInput();
    }

    private void OnRightInputReleased(CustomButton _)
    {
        uiRightHeld = false;
        ReleaseNeutralInputBlocks();
        ApplyPopupDirectionalInput();
    }

    private bool IsLeftInputHeld()
    {
        return uiLeftHeld || keyboardLeftHeld;
    }

    private bool IsRightInputHeld()
    {
        return uiRightHeld || keyboardRightHeld;
    }

    private void ReleaseNeutralInputBlocks()
    {
        if (Time.frameCount == popupInputCutFrame)
        {
            return;
        }

        if (!IsLeftInputHeld())
        {
            leftInputBlockedUntilRelease = false;
        }

        if (!IsRightInputHeld())
        {
            rightInputBlockedUntilRelease = false;
        }
    }

    private void ApplyPopupDirectionalInput()
    {
        if (!TryResolvePopupDirectionalInput(out float input))
        {
            return;
        }

        actor.SetMoveInput(input);
    }

    private bool TryResolvePopupDirectionalInput(out float input)
    {
        input = 0f;
        if (fakeTipsPopup == null
            || actor == null
            || !gameplayActive
            || deathTriggered
            || IsDoorCommitted
            || popupSuccessCommitted
            || Time.frameCount == popupInputCutFrame)
        {
            return false;
        }

        if (IsRightInputHeld() && !rightInputBlockedUntilRelease)
        {
            input = 1f;
            return true;
        }

        if (IsLeftInputHeld() && !leftInputBlockedUntilRelease)
        {
            input = -1f;
        }

        return true;
    }

    private void CutHeldGameplayInputsForLifecycle()
    {
        RefreshPlayerInputSources();
        leftInputBlockedUntilRelease |= IsLeftInputHeld();
        rightInputBlockedUntilRelease |= IsRightInputHeld();
        trackedInputPanel?.ResetState();
        actor?.SetMoveInput(0f);
    }

    private void ResetInterruptedInputState(bool blockCurrentlyHeld, bool resetPanel)
    {
        RefreshPlayerInputSources();
        if (resetPanel)
        {
            trackedInputPanel?.ResetState();
        }

        leftInputBlockedUntilRelease = blockCurrentlyHeld && IsLeftInputHeld();
        rightInputBlockedUntilRelease = blockCurrentlyHeld && IsRightInputHeld();
        popupInputCutFrame = -1;
        actor?.SetMoveInput(0f);
    }

    private void SuppressBaseInputForCurrentFrame(bool clearHorizontalVelocity)
    {
        actor?.SetMoveInput(0f);
        clearSuppressedActorHorizontalVelocity |= clearHorizontalVelocity;
        if (baseInputFrameSuppressed || levelCtrl == null)
        {
            return;
        }

        baseInputFrameSuppressed = true;
        suppressedBaseActor = levelCtrl.Actor;
        suppressedBaseEnabled = levelCtrl.enabled;
        suppressedActorVelocityCaptured = actorBody != null;
        if (suppressedActorVelocityCaptured)
        {
            suppressedActorVelocity = actorBody.linearVelocity;
            suppressedActorAngularVelocity = actorBody.angularVelocity;
        }

        levelCtrl.SetActor(null);
        levelCtrl.enabled = false;
    }

    private void RestoreBaseInputAfterCut()
    {
        if (!baseInputFrameSuppressed)
        {
            return;
        }

        if (levelCtrl != null)
        {
            if (levelCtrl.Actor == null)
            {
                levelCtrl.SetActor(suppressedBaseActor);
            }

            levelCtrl.enabled = suppressedBaseEnabled;
        }

        if (suppressedActorVelocityCaptured
            && actorBody != null
            && actorBody.simulated
            && !deathTriggered
            && !IsDoorCommitted
            && !popupSuccessCommitted
            && !gamePaused
            && !applicationPaused
            && !applicationUnfocused)
        {
            Vector2 restoredVelocity = suppressedActorVelocity;
            if (clearSuppressedActorHorizontalVelocity)
            {
                restoredVelocity.x = 0f;
            }

            actorBody.linearVelocity = restoredVelocity;
            actorBody.angularVelocity = suppressedActorAngularVelocity;
        }

        baseInputFrameSuppressed = false;
        suppressedBaseActor = null;
        suppressedActorVelocityCaptured = false;
        clearSuppressedActorHorizontalVelocity = false;
    }

    private void ApplyPopupCameraProfile()
    {
        cameraFollowArea?.SetFollowArea(PopupCameraFollowAreaCenter, PopupCameraFollowAreaSize);
    }

    private void ApplyDeliveryCameraProfile()
    {
        cameraFollowArea?.SetFollowArea(DeliveryCameraFollowAreaCenter, DeliveryCameraFollowAreaSize);
        cameraFollowArea?.SetCanFollow(true);
    }

    private void StartDeliverySequence()
    {
        if (hasStarted || fakeTipsPopup != null || !gameplayActive || actor == null)
        {
            return;
        }

        if (randomLayerSprites == null || randomLayerSprites.Length == 0)
        {
            Debug.LogWarning("[Level119Ctrl] Send-layer sprites are not configured.");
            return;
        }

        hasStarted = true;
        levelCtrl?.SetCtrlable(false);
        StopActorHorizontalMovement();
        SetUfoRodActive(true);
        layerSequence = BuildLayerSequence();
        BuildRemainingLayerRise();
        climbLoop?.SetSuspended(false);
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
            if (poolIndex >= shuffledPool.Count)
            {
                shuffledPool = BuildNonRepeatingPool(previousSprite);
                poolIndex = 0;
            }

            if (shuffledPool.Count == 0)
            {
                break;
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
                Debug.LogWarning("[Level119Ctrl] Unable to build a non-repeating layer pool.");
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
            || fakeTipsPopup != null
            || IsDoorCommitted
            || deathTriggered
            || layerSequence == null
            || completedLayers >= TotalLayers
            || completedLayers >= layerSequence.Count
            || currentLayerObject != null
            || ufoRoot == null)
        {
            return;
        }

        Sprite sprite = layerSequence[completedLayers];
        GameObject layerObject = CreateLayerObject(sprite, completedLayers + 1);
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

        layerObject.transform.SetParent(ufoRoot, false);
        Vector3 carryPosition = carryAnchor != null
            ? carryAnchor.localPosition
            : new Vector3(-3.85f, -0.7f, -0.1f);
        if (visualProfile != null)
        {
            carryPosition += visualProfile.carryOffset;
        }

        layerObject.transform.localPosition = carryPosition;

        float ufoTargetY = completedLayers == 0
            ? ufoRoot.position.y
            : deliveryWorkY + lastSettledLayerSceneHeight - StackOverlapY;
        float speedMultiplier = ResolveLayerSpeedMultiplier(completedLayers + 1);
        float verticalDuration = Mathf.Abs(ufoTargetY - ufoRoot.position.y)
            / (Mathf.Max(0.01f, ascentSpeed) * speedMultiplier);
        float leftMoveDuration = Mathf.Max(0.01f, ufoMoveDuration / speedMultiplier);

        currentLayerObject = layerObject;
        currentLayerParent = parent;
        currentLayerReturnDuration = Mathf.Max(0.01f, ufoReturnDuration / speedMultiplier);
        currentLayerObjectDuration = Mathf.Max(0.01f, itemTravelDuration);
        currentLayerSettling = false;

        activeSequence = DOTween.Sequence()
            .Append(ufoRoot.DOMoveY(ufoTargetY, Mathf.Max(0.01f, verticalDuration))
                .SetEase(Ease.InOutSine))
            .Append(ufoRoot.DOMoveX(ResolveUfoDeliveryX(), leftMoveDuration)
                .SetEase(Ease.Linear))
            .OnComplete(() =>
            {
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

    private void HandleLayerCollision(GameObject layerObject, Collision2D collision)
    {
        if (layerObject == null
            || collision == null
            || currentLayerObject != layerObject
            || currentLayerSettling
            || IsDoorCommitted
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
        if (!gameplayActive
            || IsDoorCommitted
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
        FreezeDeliveryForFailure();
        PlayKnock(impactPosition);
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
            || IsDoorCommitted
            || layerObject == null
            || currentLayerObject != layerObject
            || currentLayerSettling)
        {
            return;
        }

        currentLayerSettling = true;
        activeSequence?.Kill(false);
        activeSequence = null;
        ufoRoot?.DOKill(false);

        float stompCenterX = layerObject.GetComponent<BoxCollider2D>()?.bounds.center.x
            ?? layerObject.transform.position.x;
        Vector3 targetPosition = ResolveLayerTargetAtCurrentPosition(layerObject, stompCenterX);
        lastLayerPosition = layerObject.transform.position;
        ReleaseLayerObject(layerObject, currentLayerParent, completedLayers + 1);

        bool isFinalLayer = completedLayers + 1 == TotalLayers;
        activeSequence = DOTween.Sequence()
            .Append(layerObject.transform.DOMoveY(targetPosition.y, currentLayerObjectDuration)
                .SetEase(Ease.OutQuad)
                .OnUpdate(() => SyncActorWithLayer(layerObject)))
            .AppendCallback(() => CompleteLayer(layerObject, targetPosition));
        if (isFinalLayer)
        {
            activeSequence.OnComplete(BeginFinalUfoExit);
        }
        else
        {
            activeSequence
                .Append(ufoRoot.DOMoveX(deliveryRestPosition.x, currentLayerReturnDuration)
                    .SetEase(Ease.Linear))
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
        float pivotOffsetX = ResolveLayerRendererCenterOffsetX(layerObject);
        LayerVisualProfile visualProfile = FindVisualProfile(layerObject != null
            ? layerObject.GetComponent<SpriteRenderer>()?.sprite
            : null);
        Vector3 targetPosition = new Vector3(
            worldCenterX - pivotOffsetX,
            supportTopY - ResolveStackOverlap() - colliderCenterOffsetY + layerHeight * 0.5f,
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

    private void CompleteLayer(GameObject layerObject, Vector3 targetPosition)
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
            lastSettledLayerSceneHeight = renderer.bounds.size.y;
        }

        if (ufoRoot != null)
        {
            deliveryWorkY = ufoRoot.position.y;
        }

        stackedObjects.Add(layerObject);
        stackTopY = ResolveCurrentSupportTopY();
        completedLayers++;
        UpdateRemainingLayersText();
    }

    private void FinishLayerCycle()
    {
        activeSequence = null;
        currentLayerObject = null;
        currentLayerParent = null;
        currentLayerSettling = false;

        if (completedLayers < TotalLayers && !IsDoorCommitted && !deathTriggered)
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
        stackTopY += deltaY;
        lastLayerPosition.y += deltaY;
    }

    private void BeginFinalUfoExit()
    {
        activeSequence = null;
        currentLayerObject = null;
        currentLayerParent = null;
        currentLayerSettling = false;
        if (!isActiveAndEnabled || deathTriggered || IsDoorCommitted || ufoRoot == null)
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
        float duration = Mathf.Max(0.01f, distance / speed);
        activeSequence = DOTween.Sequence()
            .Append(ufoRoot.DOMoveX(ufoRoot.position.x + distance, duration).SetEase(Ease.Linear))
            .OnComplete(() =>
            {
                activeSequence = null;
                if (!isActiveAndEnabled || deathTriggered || IsDoorCommitted)
                    return;

                ufoRoot.gameObject.SetActive(false);
                fullClimbMovementUnlocked = true;
                stageCenterLocked = false;
                levelCtrl?.SetCtrlable(gameplayActive);
                RefreshActorShadowVisibility();
                UpdateExitVisibility();
            });
        if (!gameplayActive)
            activeSequence.Pause();
    }

    private void UpdateExitVisibility()
    {
        bool showExit = isActiveAndEnabled && hasStarted && !popupDoorCommitted && !popupSuccessCommitted
            && (completedLayers >= TotalLayers || (climbLoop != null && climbLoop.IsFinalApproach));
        if (inducedExitPlatform != null && inducedExitPlatform.gameObject.activeSelf != showExit)
            inducedExitPlatform.gameObject.SetActive(showExit);
        if (inducedExitDoor != null && inducedExitDoor.gameObject.activeSelf != showExit)
            inducedExitDoor.gameObject.SetActive(showExit);
        SetInducedDoorCollision(showExit && fullClimbMovementUnlocked && completedLayers == TotalLayers
            && gameplayActive && !deathTriggered && !IsDoorCommitted && actor != null && !actor.IsDied);
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

    public void OnInducedExitEntered(Collider2D collider)
    {
        if (!isActiveAndEnabled || !gameplayActive || !fullClimbMovementUnlocked
            || completedLayers != TotalLayers || IsDoorCommitted || deathTriggered
            || fakeTipsPopup != null || actor == null || actor.IsDied || collider != actorCollider
            || levelCtrl == null || inducedExitDoor == null || !inducedExitDoor.gameObject.activeInHierarchy)
        {
            return;
        }

        RestoreBaseInputAfterCut();
        ResetInterruptedInputState(false, true);
        inducedDoorCommitted = true;
        RefreshActorShadowVisibility();
        FreezeDeliveryForDoor();
        levelCtrl.SetSuccessAnim(inducedExitDoor);
        levelCtrl.OnActorTrigger2D(collider);
    }

    private GameObject CreateLayerObject(Sprite sprite, int layerNumber)
    {
        if (sprite == null)
        {
            Debug.LogWarning($"[Level119Ctrl] Layer {layerNumber} has no sprite.");
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
        private Level119Ctrl owner;

        internal void Initialize(Level119Ctrl newOwner)
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
        SpriteRenderer renderer = target != null ? target.GetComponent<SpriteRenderer>() : null;
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
            Collider2D collider = stackedObjects[i] != null
                ? stackedObjects[i].GetComponent<Collider2D>()
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

    private static float ResolveLayerRendererCenterOffsetX(GameObject layerObject)
    {
        if (layerObject == null)
        {
            return 0f;
        }

        SpriteRenderer renderer = layerObject.GetComponent<SpriteRenderer>();
        return renderer != null
            ? renderer.bounds.center.x - layerObject.transform.position.x
            : 0f;
    }

    private float ResolveLayerPivotXForStackCenter(GameObject layerObject)
    {
        return stackCenterX - ResolveLayerRendererCenterOffsetX(layerObject);
    }

    private void UpdateStageCenterLock()
    {
        if (!gameplayActive
            || fakeTipsPopup != null
            || IsDoorCommitted
            || deathTriggered
            || hasStarted
            || stageCenterLocked
            || actor == null
            || triggerStage == null
            || stageSupportCollider == null
            || !actor.IsGround
            || !IsActorSupportedBy(stageSupportCollider))
        {
            return;
        }

        float actorCenterX = actorCollider != null
            ? actorCollider.bounds.center.x
            : actor.transform.position.x;
        if (actorCenterX < stackCenterX - stageCenterLockTolerance)
        {
            return;
        }

        AlignActorToStageCenter();
        stageCenterLocked = true;
        RefreshActorShadowVisibility();
        levelCtrl?.SetCtrlable(false);
        StartDeliverySequence();
    }

    private void RefreshActorShadowVisibility()
    {
        bool shouldHide = shadowSuppressingPopup != null
            || stageCenterLocked
            || fullClimbMovementUnlocked
            || IsDoorCommitted
            || popupSuccessCommitted;
        SetActorShadowVisible(!shouldHide);
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
                actorShadowInitialActive = actorShadow.activeSelf;
                actorShadowControllerInitialEnabled = actorShadowController != null
                    && actorShadowController.enabled;
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

    private void MaintainStageCenterLock()
    {
        if (!gameplayActive
            || fakeTipsPopup != null
            || !stageCenterLocked
            || IsDoorCommitted
            || deathTriggered
            || actor == null)
        {
            return;
        }

        AlignActorToStageCenter();
        levelCtrl?.SetCtrlable(false);
        StopActorHorizontalMovement();
    }

    private void AlignActorToStageCenter()
    {
        float colliderCenterOffsetX = actorCollider != null
            ? actorCollider.bounds.center.x - actor.transform.position.x
            : 0f;
        float targetRootX = stackCenterX - colliderCenterOffsetX;

        if (actorBody != null)
        {
            Vector2 position = actorBody.position;
            position.x = targetRootX;
            actorBody.position = position;
            actorBody.linearVelocity = new Vector2(0f, actorBody.linearVelocity.y);
        }
        else
        {
            Vector3 position = actor.transform.position;
            position.x = targetRootX;
            actor.transform.position = position;
        }
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

    private void CloseFakeTipsPopup()
    {
        Level119FakeTipsPopup activePopup = fakeTipsPopup;
        fakeTipsPopup = null;
        activePopup?.CloseFromOwner();

        Level119FakeTipsPopup[] stalePopups = FindObjectsByType<Level119FakeTipsPopup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < stalePopups.Length; i++)
        {
            Level119FakeTipsPopup popup = stalePopups[i];
            if (popup != null && popup != activePopup)
            {
                popup.CloseFromOwner();
            }
        }
    }

    internal bool TryCommitPopupDoor(Level119FakeTipsPopup popup)
    {
        if (popup == null
            || popup != fakeTipsPopup
            || popupClosed
            || IsDoorCommitted
            || popupSuccessCommitted
            || deathTriggered
            || actor == null
            || actor.IsDied
            || !gameplayActive)
        {
            return false;
        }

        RestoreBaseInputAfterCut();
        ResetInterruptedInputState(false, true);
        popupDoorCommitted = true;
        RefreshActorShadowVisibility();
        FreezeDeliveryForDoor();
        return true;
    }

    internal void NotifyPopupVisualHidden(Level119FakeTipsPopup popup)
    {
        if (popup == null || popup != shadowSuppressingPopup)
        {
            return;
        }

        shadowSuppressingPopup = null;
        RefreshActorShadowVisibility();
    }

    internal void NotifyPopupClosed(Level119FakeTipsPopup popup)
    {
        if (popup == null || popup != fakeTipsPopup || popupDoorCommitted)
        {
            return;
        }

        fakeTipsPopup = null;
        popupClosed = true;
        ApplyDeliveryCameraProfile();
    }

    internal void CompletePopupDoorSuccess(Level119FakeTipsPopup popup)
    {
        if (popup == null
            || popup != fakeTipsPopup
            || !popupDoorCommitted
            || popupSuccessCommitted
            || deathTriggered)
        {
            return;
        }

        popupSuccessCommitted = true;
        levelCtrl?.SetSuccessAnim(null);
        CloseFakeTipsPopup();
        levelCtrl?.OnActorTrigger2D(actorCollider);
    }

    private void FreezeDeliveryForDoor()
    {
        gameplayActive = false;
        climbLoop?.SetSuspended(true);
        SetInducedDoorCollision(false);
        KillDeliveryMotion();
        SetUfoRodActive(false);
        levelCtrl?.SetCtrlable(false);
        StopActorHorizontalMovement();
        cameraFollowArea?.SetCanFollow(false);
    }

    private void FreezeDeliveryForFailure()
    {
        gameplayActive = false;
        climbLoop?.SetSuspended(true);
        SetInducedDoorCollision(false);
        KillDeliveryMotion();
        SetUfoRodActive(false);
        levelCtrl?.SetCtrlable(false);
    }

    private void KillDeliveryMotion()
    {
        activeSequence?.Kill(false);
        activeSequence = null;
        ufoRoot?.DOKill(false);
        currentLayerObject?.transform.DOKill(false);
    }

    private void SetUfoRodActive(bool isActive)
    {
        if (ufoRodCollider != null)
        {
            ufoRodCollider.enabled = isActive;
        }
    }

    private void PlayKnock(Vector3 worldPosition)
    {
        if (knockEffect == null)
        {
            return;
        }

        knockEffect.transform.position = worldPosition;
        knockEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        knockEffect.Play(true);
    }

    private void StopKnockEffect()
    {
        knockEffect?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

    #region 剩余层数文本

    // I2 完成语言刷新后重绘当前层数，避免层数未变化时被数字缓存跳过。
    private void OnRemainingLayersLanguageChanged()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        lastDisplayedRemainingLayers = -1;
        UpdateRemainingLayersText();
    }

    private void UpdateRemainingLayersText()
    {
        int remainingLayers = Mathf.Max(0, TotalLayers - completedLayers);
        if (remainingLayersText == null || remainingLayers == lastDisplayedRemainingLayers)
        {
            return;
        }

        // 场景已显式绑定 TMP 文本；复用现有 I2 词条的 {0}，不依赖额外 Localize 组件。
        string template = LocalizationManager.GetTranslation(RemainingLayersTerm, FixForRTL: false);
        remainingLayersText.text = string.Format(template, remainingLayers);

        remainingLayersText.ForceMeshUpdate(true, true);
        lastDisplayedRemainingLayers = remainingLayers;
    }

    #endregion
}

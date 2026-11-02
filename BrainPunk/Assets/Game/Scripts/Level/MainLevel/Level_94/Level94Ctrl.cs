using System.Collections;
using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 管理 Level094 的假地面揭盖、弹簧蓄力和月球发射流程。
/// 逻辑仅作用于本关 Prefab，不能改写共享 Actor、地面或 Level032 资源。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level94Ctrl : MonoBehaviour
{
    private const int MousePointerId = -1;
    private const int NoPointerId = int.MinValue;
    private const float PassiveRearmHorizontalClearance = 0.05f;
    private const string PlayerLaunchSfxTag = "l93PlayerLaunch";

    private enum PointerMode
    {
        None,
        CoverSwipe,
        ButtonDrag
    }

    #region Inspector references

    // 以下引用只绑定 Level_094.prefab 内的既有对象，不允许运行时替换共享资源。
    [Header("Level references")]
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private Camera inputCamera;

    [Header("Cover")]
    [SerializeField] private SpriteRenderer fakeGroundCover;
    [SerializeField] private Collider2D fakeGroundCoverCollider;
    [SerializeField] private GameObject revealedGround;

    [Header("Spring")]
    [SerializeField] private Transform springRoot;
    [SerializeField] private Transform springButton;
    [SerializeField] private Collider2D springButtonCollider;
    [SerializeField] private Rigidbody2D springButtonBody;
    [SerializeField] private SpringJoint2D legacySpringJoint;
    [SerializeField] private Transform springVisual;

    [Header("Success")]
    [SerializeField] private Transform moonTarget;

    [Header("Optional floor override")]
    [Tooltip("可选：覆盖弹簧洞口的 Ground 层 BoxCollider2D。当前 Prefab 已预拆地面，应保持为空。")]
    [SerializeField] private BoxCollider2D floorCollider;

    #endregion

    #region Tuning

    // 手势距离使用屏幕像素；只用于假盖板上的单指横滑。
    [Header("Cover swipe")]
    [SerializeField, Min(1f)] private float coverSwipeDistancePixels = 55f;
    [SerializeField, Min(1f)] private float coverSwipeHorizontalBias = 1.15f;
    [SerializeField, Min(0.01f)] private float coverFadeDuration = 0.22f;
    [SerializeField, Min(0f)] private float coverSlideDistance = 1.4f;

    // 压缩行程使用世界单位；成功阈值按 0~1 的归一化压缩比例判断。
    [Header("Spring drag")]
    [SerializeField, Min(1f)] private float buttonDragActivationPixels = 14f;
    [SerializeField, Min(0.1f)] private float maximumCompressionDistance = 4.2f;
    [SerializeField, Range(0.05f, 0.95f)] private float successCompressionRatio = 0.5f;
    [SerializeField, Range(0.01f, 0.5f)] private float fullyCompressedSpringScale = 0.12f;
    [SerializeField, Min(0.01f)] private float standingTolerance = 0.42f;
    [SerializeField, Min(0.01f)] private float buttonReturnDuration = 0.24f;

    // 普通踩踏只负责小幅弹性反馈，不参与成功判定。
    [Header("Passive spring bounce")]
    [SerializeField, Min(0f)] private float passiveBounceDelay = 0.03f;
    [SerializeField, Range(0.01f, 0.35f)] private float passiveCompressionRatio = 0.08f;
    [SerializeField, Range(0.01f, 0.5f)] private float passiveExtensionRatio = 0.405f;
    [SerializeField, Min(0.01f)] private float passiveCompressionDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float passiveReturnDuration = 0.16f;
    [SerializeField, Min(0f)] private float passiveBounceVelocity = 10f;
    [SerializeField, Min(0f)] private float passiveBounceCooldown = 0.12f;

    [Header("Spring release presentation")]
    [SerializeField, Range(0.01f, 0.5f)] private float launchExtensionRatio = 0.18f;

    // 欠压发射沿 Actor 当前朝向侧抛，超时仅用于防止控制权软锁。
    [Header("Insufficient launch")]
    [SerializeField] private Vector2 insufficientLaunchVelocity = new Vector2(8.5f, 19f);
    [SerializeField, Range(0.1f, 1f)] private float minimumInsufficientLaunchStrength = 0.65f;
    [SerializeField, Min(0.1f)] private float insufficientLaunchControlTimeout = 2.8f;

    // 成功轨迹由本关直接驱动 Transform，飞行末段才渐隐并缩小。
    [Header("Moon flight")]
    [SerializeField, Min(0.01f)] private float moonFlightDuration = 2.45f;
    [SerializeField, Min(0f)] private float moonFlightArcHeight = 18f;
    [SerializeField, Range(0.5f, 0.95f)] private float moonFlightFadeStart = 0.67f;
    [SerializeField, Range(0f, 0.5f)] private float moonFlightFinalScale = 0.12f;
    [SerializeField] private Vector3 moonTargetOffset;

    // 仅供未预拆地面的兼容回退使用；不能修改共享 Ground_mid.prefab。
    [Header("Floor opening")]
    [SerializeField, Min(0f)] private float floorGapPadding = 0.04f;
    [SerializeField, Min(0.01f)] private float floorSurfaceTopTolerance = 0.35f;

    #endregion

    #region Runtime state

    private PointerMode pointerMode;
    private int activePointerId = NoPointerId;
    private Vector2 pointerStartScreenPosition;
    private float buttonPressStartWorldY;
    private bool buttonDragActivated;
    private float dragStartPointerWorldY;
    private float dragStartCompressionRatio;
    private float currentCompressionRatio;

    private Collider2D actorCollider;
    private Rigidbody2D actorBody;
    private SpriteRenderer springButtonRenderer;
    private SpriteRenderer springVisualRenderer;
    private SpriteRendererGroupAlpha actorRendererGroup;

    private Vector3 buttonRestLocalPosition;
    private Vector3 springRestLocalPosition;
    private Vector3 springRestLocalScale;
    private float springRestBottomWorldY;
    private bool hasCachedSpringPose;

    private Color coverRestColor;
    private bool hasCachedCoverColor;
    private Vector3 coverRestLocalPosition;
    private bool hasCachedCoverPose;
    private bool isCoverRemoved;
    private bool isCoverFading;

    private bool actorInsideSpringTrigger;
    private bool actorWasStanding;
    private bool passiveBounceArmed;
    private bool passiveBouncePending;
    private bool passiveBouncePlaying;
    private bool passiveReturnStarted;
    private float passiveBounceAt;
    private float nextPassiveBounceAt;
    private bool passiveActorLaunchActive;
    private bool passiveActorLaunchBecameAirborne;
    private Collider2D passiveIgnoredActorCollider;
    private Collider2D passiveIgnoredButtonCollider;
    private bool passiveButtonCollisionWasIgnored;
    private bool passiveButtonCollisionBypassActive;

    private bool actorOwnedBySpring;
    private bool actorWasCtrlable;
    private bool actorBodyWasSimulated;
    private bool isCarryingActor;
    private Vector3 carriedActorOffset;

    private bool isInsufficientLaunch;
    private bool insufficientLaunchBecameAirborne;
    private float insufficientLaunchElapsed;
    private bool springLaunchPaused;
    private bool springLaunchWasSimulated;
    private Vector2 cachedSpringLaunchVelocity;

    private bool isMoonFlight;
    private bool hasSucceeded;
    private Vector3 moonFlightInitialScale;
    private float actorRendererInitialAlpha = 1f;

    private Tween coverTween;
    private Tween buttonTween;
    private Tween moonFlightTween;
    private Coroutine resumeSpringActorCoroutine;
    private EventSubscriber eventSubscriber;
    private bool isGameplayActive = true;

    private Vector2 floorOriginalSize;
    private Vector2 floorOriginalOffset;
    private bool floorWasSplit;
    private GameObject runtimeRightFloor;

    private bool initialized;
    private bool needsResetAfterEnable;
    private bool hasLoggedMissingReferences;

    #endregion

    #region Unity lifecycle

    private void Awake()
    {
        ResolveReferences();
        CacheInitialState();
        PrepareAttempt();
        initialized = true;
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            EventsUtils.ResetEvents(ref eventSubscriber);
            eventSubscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
            eventSubscriber.Subscribe<GamePauseEvent>(OnGamePause);
            eventSubscriber.Subscribe<GameResumeEvent>(OnGameResume);
        }

        if (initialized && needsResetAfterEnable)
        {
            needsResetAfterEnable = false;
            ResolveReferences();
            PrepareAttempt();
        }

        if (Application.isPlaying)
        {
            SetGameplayActive(PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING));
        }
    }

    private void Start()
    {
        ResolveReferences();
        PrepareButtonBody();
    }

    private void Update()
    {
        if (!ResolveReferences())
        {
            return;
        }

        if (!isGameplayActive || Time.timeScale <= 0f)
        {
            CancelActivePointer();
            return;
        }

        if (actor.IsDied)
        {
            CancelActivePointer();
            isInsufficientLaunch = false;
            passiveActorLaunchActive = false;
            passiveActorLaunchBecameAirborne = false;
            ResetPassiveSpringPresentation();
            isCarryingActor = false;
            moonFlightTween?.Kill();
            moonFlightTween = null;
            isMoonFlight = false;
            RestorePausedSpringLaunch(false);
            ReleaseActorOwnership(false);
            RestoreActorVisualState();
            return;
        }

        if (hasSucceeded)
        {
            return;
        }

        if (levelCtrl != null && levelCtrl.IsSuccess())
        {
            CancelActivePointer();
            isInsufficientLaunch = false;
            passiveActorLaunchActive = false;
            passiveActorLaunchBecameAirborne = false;
            ResetPassiveSpringPresentation();
            isCarryingActor = false;
            RestorePausedSpringLaunch(false);
            ReleaseActorOwnership(false);
            return;
        }

        UpdatePassiveActorLaunch();
        UpdateInsufficientLaunch();
        UpdateSpringStandingState();
        UpdatePointerInput();
    }

    private void LateUpdate()
    {
        ApplyCarriedActorPose();

        if (isGameplayActive
            && !springLaunchPaused
            && actorBody != null
            && actorBody.simulated
            && (isInsufficientLaunch || passiveActorLaunchActive)
            && actorBody.linearVelocity.sqrMagnitude > 0.0001f)
        {
            cachedSpringLaunchVelocity = actorBody.linearVelocity;
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            CancelActivePointer();
        }
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
        needsResetAfterEnable = true;
    }

    private void OnDestroy() => CleanupRuntimeState();

    private void CleanupRuntimeState()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        CleanupInterruptedAttempt();
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        SetGameplayActive(evt.NewState.Equals(GamePlayStateName.PLAYING));
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        SetGameplayActive(false);
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        StartCoroutine(ReconcileGameplayStateAfterResume());
    }

    private IEnumerator ReconcileGameplayStateAfterResume()
    {
        // GameResumeEvent 由状态节点退出时派发，延后一帧可避开事件订阅顺序差异。
        yield return null;
        if (isActiveAndEnabled)
        {
            SetGameplayActive(PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING));
        }
    }

    private void SetGameplayActive(bool isActive)
    {
        if (isGameplayActive == isActive)
        {
            return;
        }

        isGameplayActive = isActive;
        if (!isGameplayActive)
        {
            CancelActivePointer();
            coverTween?.Pause();
            buttonTween?.Pause();
            moonFlightTween?.Pause();
            PauseSpringLaunch();
            return;
        }

        coverTween?.Play();
        buttonTween?.Play();
        moonFlightTween?.Play();
        if ((actorOwnedBySpring || springLaunchPaused) && resumeSpringActorCoroutine == null)
        {
            resumeSpringActorCoroutine = StartCoroutine(ResumeSpringActorAfterGameplayResume());
        }
    }

    private void PauseSpringLaunch()
    {
        if (springLaunchPaused
            || actorBody == null
            || (!isInsufficientLaunch && !passiveActorLaunchActive))
        {
            return;
        }

        springLaunchWasSimulated = actorBody.simulated;
        if (actorBody.linearVelocity.sqrMagnitude > 0.0001f)
        {
            cachedSpringLaunchVelocity = actorBody.linearVelocity;
        }

        actorBody.simulated = false;
        springLaunchPaused = true;
    }

    private IEnumerator ResumeSpringActorAfterGameplayResume()
    {
        yield return null;
        resumeSpringActorCoroutine = null;

        if (!isGameplayActive || actor == null || actor.IsDied)
        {
            yield break;
        }

        if (actorOwnedBySpring)
        {
            actor.StopMoving();
        }

        RestorePausedSpringLaunch(true);
    }

    private void RestorePausedSpringLaunch(bool restoreVelocity)
    {
        if (!springLaunchPaused)
        {
            return;
        }

        if (actorBody != null)
        {
            Physics2D.SyncTransforms();
            actorBody.simulated = springLaunchWasSimulated;
            if (restoreVelocity
                && springLaunchWasSimulated
                && actor != null
                && !actor.IsDied)
            {
                actorBody.linearVelocity = cachedSpringLaunchVelocity;
            }
        }

        springLaunchPaused = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        coverSwipeDistancePixels = Mathf.Max(1f, coverSwipeDistancePixels);
        coverSwipeHorizontalBias = Mathf.Max(1f, coverSwipeHorizontalBias);
        coverFadeDuration = Mathf.Max(0.01f, coverFadeDuration);
        coverSlideDistance = Mathf.Max(0f, coverSlideDistance);
        buttonDragActivationPixels = Mathf.Max(1f, buttonDragActivationPixels);
        maximumCompressionDistance = Mathf.Max(0.1f, maximumCompressionDistance);
        standingTolerance = Mathf.Max(0.01f, standingTolerance);
        buttonReturnDuration = Mathf.Max(0.01f, buttonReturnDuration);
        passiveExtensionRatio = Mathf.Clamp(passiveExtensionRatio, 0.01f, 0.5f);
        launchExtensionRatio = Mathf.Clamp(launchExtensionRatio, 0.01f, 0.5f);
        floorSurfaceTopTolerance = Mathf.Max(0.01f, floorSurfaceTopTolerance);
        moonFlightDuration = Mathf.Max(0.01f, moonFlightDuration);
        successCompressionRatio = Mathf.Clamp(successCompressionRatio, 0.05f, 0.95f);
        moonFlightFadeStart = Mathf.Clamp(moonFlightFadeStart, 0.5f, 0.95f);
    }
#endif

    #endregion

    #region Public spring callbacks

    /// <summary>
    /// 供本关弹簧范围的 OnTrigger2DHandle.onEnter 绑定；只接受 Actor 碰撞体。
    /// </summary>
    public void OnActorEnterSpring(Collider2D other)
    {
        if (!IsActorCollider(other))
        {
            return;
        }

        actorInsideSpringTrigger = true;
        TrySchedulePassiveBounce();
    }

    /// <summary>
    /// 供本关弹簧范围的 OnTrigger2DHandle.onExit 绑定；只清理 Actor 的范围状态。
    /// </summary>
    public void OnActorExitSpring(Collider2D other)
    {
        if (!IsActorCollider(other))
        {
            return;
        }

        actorInsideSpringTrigger = false;
    }

    /// <summary>
    /// Level032 复制 Prefab 的兼容入口；新接线应优先使用 <see cref="OnActorEnterSpring"/>。
    /// </summary>
    public void TriggerSpring(Collider2D other)
    {
        OnActorEnterSpring(other);
    }

    #endregion

    #region Pointer input

    private void UpdatePointerInput()
    {
        if (pointerMode == PointerMode.None)
        {
            TryBeginPointerFromTouch();

            if (pointerMode == PointerMode.None && Input.touchCount == 0 && Input.GetMouseButtonDown(0))
            {
                TryBeginPointer(Input.mousePosition, MousePointerId);
            }

            return;
        }

        if (!TryReadActivePointer(out Vector2 screenPosition, out bool ended, out bool canceled))
        {
            CancelActivePointer();
            return;
        }

        if (canceled)
        {
            CancelActivePointer();
            return;
        }

        if (pointerMode == PointerMode.CoverSwipe)
        {
            UpdateCoverSwipe(screenPosition);
        }
        else if (pointerMode == PointerMode.ButtonDrag)
        {
            UpdateButtonDrag(screenPosition);
        }

        if (!ended)
        {
            return;
        }

        if (pointerMode == PointerMode.ButtonDrag)
        {
            FinishButtonDrag();
        }
        else
        {
            ClearPointerState();
        }
    }

    private void TryBeginPointerFromTouch()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began)
            {
                continue;
            }

            if (TryBeginPointer(touch.position, touch.fingerId))
            {
                return;
            }
        }
    }

    private bool TryBeginPointer(Vector2 screenPosition, int pointerId)
    {
        if (!CanAcceptNewInteraction() || InputUtils.IsPointerOverUI(screenPosition))
        {
            return false;
        }

        if (!isCoverRemoved)
        {
            if (!IsCoverHitPoint(screenPosition))
            {
                return false;
            }

            pointerMode = PointerMode.CoverSwipe;
            activePointerId = pointerId;
            pointerStartScreenPosition = screenPosition;
            return true;
        }

        if (!TryGetWorldPoint(screenPosition, out Vector2 worldPoint)
            || !IsSpringButtonHitPoint(worldPoint)
            || !IsActorStandingOnButton())
        {
            return false;
        }

        // 候选按下阶段就接管角色，避免 14px 起拖阈值前从按钮边缘滑入洞口。
        TakeActorOwnership();
        SuppressPassiveBounceForButtonPress();
        pointerMode = PointerMode.ButtonDrag;
        activePointerId = pointerId;
        pointerStartScreenPosition = screenPosition;
        buttonPressStartWorldY = worldPoint.y;
        buttonDragActivated = false;
        return true;
    }

    private bool TryReadActivePointer(out Vector2 screenPosition, out bool ended, out bool canceled)
    {
        screenPosition = default;
        ended = false;
        canceled = false;

        if (activePointerId == MousePointerId)
        {
            screenPosition = Input.mousePosition;
            ended = Input.GetMouseButtonUp(0);
            canceled = !ended && !Input.GetMouseButton(0);
            return true;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != activePointerId)
            {
                continue;
            }

            screenPosition = touch.position;
            ended = touch.phase == TouchPhase.Ended;
            canceled = touch.phase == TouchPhase.Canceled;
            return true;
        }

        return false;
    }

    private void UpdateCoverSwipe(Vector2 screenPosition)
    {
        Vector2 delta = screenPosition - pointerStartScreenPosition;
        if (Mathf.Abs(delta.x) < coverSwipeDistancePixels
            || Mathf.Abs(delta.x) < Mathf.Abs(delta.y) * coverSwipeHorizontalBias)
        {
            return;
        }

        ClearPointerState();
        RevealFakeGround(Mathf.Sign(delta.x));
    }

    private void UpdateButtonDrag(Vector2 screenPosition)
    {
        if (!buttonDragActivated)
        {
            float downwardPixels = pointerStartScreenPosition.y - screenPosition.y;
            if (downwardPixels < buttonDragActivationPixels)
            {
                return;
            }

            if (!actorOwnedBySpring || actor == null || actor.IsDied)
            {
                CancelButtonDrag();
                return;
            }

            buttonDragActivated = true;
            BeginButtonDrag(buttonPressStartWorldY);
        }

        if (!TryGetWorldPoint(screenPosition, out Vector2 worldPoint))
        {
            return;
        }

        float draggedDistance = dragStartPointerWorldY - worldPoint.y;
        float ratio = dragStartCompressionRatio + draggedDistance / Mathf.Max(0.1f, maximumCompressionDistance);
        SetCompressionRatio(Mathf.Clamp01(ratio));
    }

    private void CancelActivePointer()
    {
        if (pointerMode == PointerMode.ButtonDrag)
        {
            CancelButtonDrag();
            return;
        }

        ClearPointerState();
    }

    private void ClearPointerState()
    {
        pointerMode = PointerMode.None;
        activePointerId = NoPointerId;
        buttonDragActivated = false;
    }

    private bool CanAcceptNewInteraction()
    {
        return isGameplayActive
               && Time.timeScale > 0f
               && !isCoverFading
               && !isInsufficientLaunch
               && !passiveActorLaunchActive
               && !IsPassiveButtonCollisionBypassActive()
               && !isMoonFlight
               && !hasSucceeded
               && actor != null
               && !actor.IsDied
               && (levelCtrl == null || (levelCtrl.ActorCtrlable && !levelCtrl.IsSuccess()));
    }

    #endregion

    #region Cover and floor opening

    private void RevealFakeGround(float swipeDirection)
    {
        if (isCoverRemoved || fakeGroundCover == null)
        {
            return;
        }

        isCoverRemoved = true;
        isCoverFading = true;
        revealedGround?.SetActive(true);
        if (fakeGroundCoverCollider != null)
        {
            fakeGroundCoverCollider.enabled = false;
        }

        SplitFloorAroundSpring();

        coverTween?.Kill();
        float direction = swipeDirection < 0f ? -1f : 1f;
        float duration = Mathf.Max(0.01f, coverFadeDuration);
        float alpha = fakeGroundCover.color.a;
        Transform coverTransform = fakeGroundCover.transform;
        Vector3 startLocalPosition = coverTransform.localPosition;
        Vector3 targetLocalPosition = (hasCachedCoverPose
                ? coverRestLocalPosition
                : startLocalPosition)
            + Vector3.right * (direction * Mathf.Max(0f, coverSlideDistance));

        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Append(DOVirtual.Float(
            alpha,
            0f,
            duration,
            value =>
            {
                alpha = value;
                SetRendererAlpha(fakeGroundCover, value);
            }).SetEase(Ease.OutQuad));
        sequence.Join(DOVirtual.Vector3(
            startLocalPosition,
            targetLocalPosition,
            duration,
            value =>
            {
                if (fakeGroundCover != null)
                {
                    fakeGroundCover.transform.localPosition = value;
                }
            }).SetEase(Ease.OutQuad));
        sequence.OnComplete(() =>
        {
            coverTween = null;
            isCoverFading = false;
            if (fakeGroundCover != null)
            {
                fakeGroundCover.gameObject.SetActive(false);
            }
        });
        coverTween = sequence;
    }

    private void SplitFloorAroundSpring()
    {
        if (floorWasSplit || springButtonCollider == null)
        {
            return;
        }

        Physics2D.SyncTransforms();
        if (floorCollider == null)
        {
            floorCollider = FindFloorColliderCoveringSpring();
        }

        if (floorCollider == null)
        {
            return;
        }

        Bounds buttonBounds = springButtonCollider.bounds;
        if (Mathf.Abs(floorCollider.bounds.max.y - buttonBounds.max.y) > floorSurfaceTopTolerance)
        {
            return;
        }

        float gapWorldMin = buttonBounds.min.x - floorGapPadding;
        float gapWorldMax = buttonBounds.max.x + floorGapPadding;
        Vector3 floorCenter = floorCollider.bounds.center;
        float localGapA = floorCollider.transform.InverseTransformPoint(
            new Vector3(gapWorldMin, floorCenter.y, floorCenter.z)).x;
        float localGapB = floorCollider.transform.InverseTransformPoint(
            new Vector3(gapWorldMax, floorCenter.y, floorCenter.z)).x;
        float localGapMin = Mathf.Min(localGapA, localGapB);
        float localGapMax = Mathf.Max(localGapA, localGapB);

        floorOriginalSize = floorCollider.size;
        floorOriginalOffset = floorCollider.offset;
        float floorMin = floorOriginalOffset.x - floorOriginalSize.x * 0.5f;
        float floorMax = floorOriginalOffset.x + floorOriginalSize.x * 0.5f;

        if (localGapMin <= floorMin + 0.01f || localGapMax >= floorMax - 0.01f)
        {
            // A prefab with an already-split floor does not need a runtime piece.
            return;
        }

        float leftWidth = localGapMin - floorMin;
        float rightWidth = floorMax - localGapMax;
        if (leftWidth <= 0.01f || rightWidth <= 0.01f)
        {
            Debug.LogWarning("[Level94Ctrl] The configured floor collider cannot be split around the spring.", this);
            return;
        }

        floorCollider.size = new Vector2(leftWidth, floorOriginalSize.y);
        floorCollider.offset = new Vector2(floorMin + leftWidth * 0.5f, floorOriginalOffset.y);

        runtimeRightFloor = new GameObject("Level094_RightGround");
        runtimeRightFloor.layer = floorCollider.gameObject.layer;
        runtimeRightFloor.transform.SetParent(floorCollider.transform, false);
        BoxCollider2D rightCollider = runtimeRightFloor.AddComponent<BoxCollider2D>();
        rightCollider.size = new Vector2(rightWidth, floorOriginalSize.y);
        rightCollider.offset = new Vector2(localGapMax + rightWidth * 0.5f, floorOriginalOffset.y);
        rightCollider.sharedMaterial = floorCollider.sharedMaterial;
        rightCollider.isTrigger = false;
        rightCollider.enabled = floorCollider.enabled;

        floorWasSplit = true;
        Physics2D.SyncTransforms();
    }

    private BoxCollider2D FindFloorColliderCoveringSpring()
    {
        BoxCollider2D[] candidates = GetComponentsInChildren<BoxCollider2D>(true);
        BoxCollider2D best = null;
        float bestTopDistance = float.PositiveInfinity;
        int groundLayer = LayerMask.NameToLayer("Ground");
        Bounds buttonBounds = springButtonCollider.bounds;

        for (int i = 0; i < candidates.Length; i++)
        {
            BoxCollider2D candidate = candidates[i];
            if (candidate == null
                || candidate == springButtonCollider
                || candidate == fakeGroundCoverCollider
                || !candidate.enabled
                || candidate.isTrigger
                || (groundLayer >= 0 && candidate.gameObject.layer != groundLayer)
                || candidate.bounds.min.x > buttonBounds.center.x
                || candidate.bounds.max.x < buttonBounds.center.x
                || candidate.bounds.size.x <= buttonBounds.size.x + floorGapPadding * 2f)
            {
                continue;
            }

            float topDistance = Mathf.Abs(candidate.bounds.max.y - buttonBounds.max.y);
            if (topDistance > floorSurfaceTopTolerance || topDistance >= bestTopDistance)
            {
                continue;
            }

            best = candidate;
            bestTopDistance = topDistance;
        }

        return best;
    }

    private void RestoreFloorCollider()
    {
        if (floorWasSplit && floorCollider != null)
        {
            floorCollider.size = floorOriginalSize;
            floorCollider.offset = floorOriginalOffset;
        }

        floorWasSplit = false;
        if (runtimeRightFloor == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeRightFloor);
        }
        else
        {
            DestroyImmediate(runtimeRightFloor);
        }

        runtimeRightFloor = null;
    }

    #endregion

    #region Spring drag and launch

    private void BeginButtonDrag(float pointerWorldY)
    {
        TakeActorOwnership();
        ResetPassiveSpringPresentation();
        passiveBouncePending = false;
        passiveBouncePlaying = false;
        passiveBounceArmed = false;
        actorWasStanding = false;

        dragStartPointerWorldY = pointerWorldY;
        dragStartCompressionRatio = currentCompressionRatio;
    }

    private void FinishButtonDrag()
    {
        bool wasActivated = buttonDragActivated;
        float releasedRatio = currentCompressionRatio;
        ClearPointerState();
        isCarryingActor = false;

        if (!wasActivated)
        {
            ReleaseActorOwnership(true);
            return;
        }

        if (!actorOwnedBySpring || actor == null || actor.IsDied)
        {
            ReleaseActorOwnership(true);
            AnimateButtonHome();
            return;
        }

        if (releasedRatio >= successCompressionRatio && BeginMoonFlight())
        {
            AnimateButtonHome();
            return;
        }

        BeginInsufficientLaunch(releasedRatio);
        AnimateButtonHome();
    }

    private void CancelButtonDrag()
    {
        bool wasActivated = buttonDragActivated;
        ClearPointerState();
        if (!wasActivated)
        {
            ReleaseActorOwnership(isGameplayActive, !isGameplayActive);
            return;
        }

        // 取消不是一次松手发射。角色仍由按钮携带时直接回到休止位，再恢复物理，
        // 避免回位中的 Kinematic 按钮从下方把角色意外顶飞。
        ResetPassiveSpringPresentation();
        isCarryingActor = false;
        // 暂停事件会由 BaseLevelCtrl 统一恢复移动；刚体模拟也要延后到游戏恢复，避免暂停中下落。
        ReleaseActorOwnership(isGameplayActive, !isGameplayActive);
    }

    private void TakeActorOwnership()
    {
        if (actorOwnedBySpring || actor == null)
        {
            return;
        }

        actorOwnedBySpring = true;
        actorWasCtrlable = levelCtrl == null || levelCtrl.ActorCtrlable;
        levelCtrl?.SetCtrlable(false);
        actor.SetMoveInput(0f);
        actor.StopMoving();

        actorBody ??= actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>();
        if (actorBody != null)
        {
            actorBodyWasSimulated = actorBody.simulated;
            actorBody.linearVelocity = Vector2.zero;
            actorBody.simulated = false;
        }

        carriedActorOffset = actor.transform.position - springButton.position;
        isCarryingActor = true;
    }

    private void BeginInsufficientLaunch(float releasedRatio)
    {
        isInsufficientLaunch = true;
        insufficientLaunchBecameAirborne = false;
        insufficientLaunchElapsed = 0f;
        passiveActorLaunchActive = false;
        passiveActorLaunchBecameAirborne = false;

        if (actorBody == null || actor == null)
        {
            isInsufficientLaunch = false;
            ReleaseActorOwnership(true);
            return;
        }

        Physics2D.SyncTransforms();
        actorBody.simulated = actorBodyWasSimulated;
        float threshold = Mathf.Max(0.01f, successCompressionRatio);
        float strength = Mathf.Lerp(
            minimumInsufficientLaunchStrength,
            1f,
            Mathf.Clamp01(releasedRatio / threshold));
        float horizontalDirection = actor.FaingRight ? 1f : -1f;
        actorBody.linearVelocity = new Vector2(
            Mathf.Abs(insufficientLaunchVelocity.x) * horizontalDirection * strength,
            Mathf.Abs(insufficientLaunchVelocity.y) * strength);
        cachedSpringLaunchVelocity = actorBody.linearVelocity;

        AudioManager.PlaySound("spring");
        AudioManager.PlaySound(PlayerLaunchSfxTag);
    }

    private void UpdateInsufficientLaunch()
    {
        // 恢复协程会在下一帧还原刚体和缓存速度；在此之前不能先判定落地或超时。
        if (!isInsufficientLaunch || springLaunchPaused || resumeSpringActorCoroutine != null)
        {
            return;
        }

        if (actor == null || actor.IsDied || actorBody == null)
        {
            isInsufficientLaunch = false;
            ReleaseActorOwnership(false);
            return;
        }

        insufficientLaunchElapsed += Time.deltaTime;
        if (insufficientLaunchElapsed > 0.06f
            && (!actor.IsGround || actorBody.linearVelocity.y > 0.1f))
        {
            insufficientLaunchBecameAirborne = true;
        }

        bool landed = insufficientLaunchBecameAirborne
                      && actor.IsGround
                      && actorBody.linearVelocity.y <= 0.05f;
        bool timedOut = insufficientLaunchElapsed >= insufficientLaunchControlTimeout;
        if (!landed && !timedOut)
        {
            return;
        }

        isInsufficientLaunch = false;
        ReleaseActorOwnership(true);
    }

    private bool BeginMoonFlight()
    {
        if (moonTarget == null || actor == null)
        {
            Debug.LogWarning("[Level94Ctrl] Moon target is missing; using the insufficient launch fallback.", this);
            return false;
        }

        isMoonFlight = true;
        isCarryingActor = false;
        moonFlightInitialScale = actor.transform.localScale;
        CacheActorRendererGroup();
        SetActorVisualAlpha(1f);

        Vector3 start = actor.transform.position;
        Vector3 target = moonTarget.position + moonTargetOffset;
        target.z = start.z;

        moonFlightTween?.Kill();
        moonFlightTween = DOVirtual
            .Float(0f, 1f, Mathf.Max(0.01f, moonFlightDuration), progress =>
            {
                if (actor == null)
                {
                    return;
                }

                Vector3 position = Vector3.LerpUnclamped(start, target, progress);
                position.y += 4f * moonFlightArcHeight * progress * (1f - progress);
                actor.transform.position = position;

                float fadeProgress = Mathf.InverseLerp(moonFlightFadeStart, 1f, progress);
                if (progress < moonFlightFadeStart)
                {
                    fadeProgress = 0f;
                }

                SetActorVisualAlpha(1f - fadeProgress);
                actor.transform.localScale = Vector3.LerpUnclamped(
                    moonFlightInitialScale,
                    moonFlightInitialScale * moonFlightFinalScale,
                    fadeProgress);
            })
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(CompleteMoonFlight);

        AudioManager.PlaySound("spring");
        AudioManager.PlaySound(PlayerLaunchSfxTag);
        return true;
    }

    private void CompleteMoonFlight()
    {
        moonFlightTween = null;
        isMoonFlight = false;
        hasSucceeded = true;

        if (actor != null && moonTarget != null)
        {
            Vector3 target = moonTarget.position + moonTargetOffset;
            target.z = actor.transform.position.z;
            actor.transform.position = target;
            actor.transform.localScale = moonFlightInitialScale * moonFlightFinalScale;
            SetActorVisualAlpha(0f);
        }

        if (levelCtrl != null)
        {
            levelCtrl.SetSuccessAnim(null);
            levelCtrl.SuccessStrightly();
        }
    }

    private void ReleaseActorOwnership(bool resumeMovement, bool deferBodyRestore = false)
    {
        if (!actorOwnedBySpring)
        {
            return;
        }

        actorOwnedBySpring = false;
        isCarryingActor = false;

        if (actorBody != null)
        {
            if (deferBodyRestore)
            {
                springLaunchWasSimulated = actorBodyWasSimulated;
                cachedSpringLaunchVelocity = Vector2.zero;
                springLaunchPaused = true;
            }
            else
            {
                Physics2D.SyncTransforms();
                actorBody.simulated = actorBodyWasSimulated;
                if (!isInsufficientLaunch)
                {
                    actorBody.linearVelocity = Vector2.zero;
                }
            }
        }

        if (levelCtrl != null && !levelCtrl.IsSuccess())
        {
            levelCtrl.SetCtrlable(actorWasCtrlable);
        }

        if (!resumeMovement
            || !actorWasCtrlable
            || actor == null
            || actor.IsDied
            || (levelCtrl != null && levelCtrl.IsSuccess()))
        {
            return;
        }

        actor.SetMoveInput(0f);
        actor.ResumeMoving();
    }

    private void ApplyCarriedActorPose()
    {
        if (!isCarryingActor || actor == null || springButton == null)
        {
            return;
        }

        Vector3 carriedPosition = springButton.position + carriedActorOffset;
        actor.transform.position = carriedPosition;
        if (actorBody != null && !actorBody.simulated)
        {
            actorBody.position = carriedPosition;
        }
    }

    #endregion

    #region Passive bounce and spring presentation

    private void UpdateSpringStandingState()
    {
        if (isCoverFading
            || pointerMode == PointerMode.ButtonDrag
            || isInsufficientLaunch
            || isMoonFlight
            || hasSucceeded)
        {
            passiveBouncePending = false;
            actorWasStanding = false;
            return;
        }

        TryRearmPassiveBounce();
        bool standing = IsActorStandingOnButton();
        if (!IsActorHorizontallyOverButton() && passiveBouncePlaying && !passiveActorLaunchActive)
        {
            ResetPassiveSpringPresentation();
        }

        if (standing && !actorWasStanding)
        {
            TrySchedulePassiveBounce();
        }
        else if (!standing)
        {
            passiveBouncePending = false;
        }

        actorWasStanding = standing;
        if (passiveBouncePending
            && standing
            && Time.time >= passiveBounceAt
            && Time.time >= nextPassiveBounceAt
            && pointerMode == PointerMode.None)
        {
            PlayPassiveBounce();
        }
    }

    private void TrySchedulePassiveBounce()
    {
        if (!passiveBounceArmed
            || isCoverFading
            || passiveBouncePlaying
            || passiveBouncePending
            || Time.time < nextPassiveBounceAt
            || (!actorInsideSpringTrigger && !IsActorStandingOnButton()))
        {
            return;
        }

        passiveBouncePending = true;
        passiveBounceAt = Time.time + passiveBounceDelay;
    }

    private void PlayPassiveBounce()
    {
        passiveBouncePending = false;
        passiveBounceArmed = false;
        nextPassiveBounceAt = Time.time + passiveBounceCooldown;
        ResetPassiveSpringPresentation();
        passiveBouncePlaying = true;
        passiveReturnStarted = false;

        float startRatio = currentCompressionRatio;
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Append(DOVirtual.Float(
            startRatio,
            Mathf.Max(startRatio, passiveCompressionRatio),
            Mathf.Max(0.01f, passiveCompressionDuration),
            SetCompressionRatio).SetEase(Ease.OutQuad));
        sequence.AppendCallback(LaunchPassiveBounce);
        sequence.Append(DOVirtual.Float(
            Mathf.Max(startRatio, passiveCompressionRatio),
            -passiveExtensionRatio,
            Mathf.Max(0.01f, passiveReturnDuration),
            SetCompressionRatio).SetEase(Ease.OutQuad));
        sequence.OnComplete(() =>
        {
            buttonTween = null;
            if (!passiveActorLaunchActive)
            {
                ResetPassiveSpringPresentation();
            }
        });
        buttonTween = sequence;
    }

    private void SuppressPassiveBounceForButtonPress()
    {
        passiveBouncePending = false;
        if (!passiveBouncePlaying || passiveActorLaunchActive)
        {
            return;
        }

        ResetPassiveSpringPresentation();
        passiveBounceArmed = true;
    }

    private void TryRearmPassiveBounce()
    {
        if (passiveBounceArmed
            || passiveBouncePlaying
            || passiveActorLaunchActive
            || IsPassiveButtonCollisionBypassActive()
            || isInsufficientLaunch
            || actorOwnedBySpring
            || actor == null
            || actorCollider == null
            || actorBody == null
            || springButtonCollider == null
            || !actor.IsGround
            || Mathf.Abs(actorBody.linearVelocity.y) > 0.1f)
        {
            return;
        }

        Bounds actorBounds = actorCollider.bounds;
        Bounds buttonBounds = springButtonCollider.bounds;
        bool clearOnLeft = actorBounds.max.x
                           <= buttonBounds.min.x - PassiveRearmHorizontalClearance;
        bool clearOnRight = actorBounds.min.x
                            >= buttonBounds.max.x + PassiveRearmHorizontalClearance;
        passiveBounceArmed = clearOnLeft || clearOnRight;
    }

    private void LaunchPassiveBounce()
    {
        if (actor == null
            || actor.IsDied
            || actorBody == null
            || !actorBody.simulated
            || !IsActorHorizontallyOverButton())
        {
            return;
        }

        Vector2 velocity = actorBody.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, passiveBounceVelocity);
        BeginPassiveButtonCollisionBypass();
        actorBody.linearVelocity = velocity;
        cachedSpringLaunchVelocity = velocity;
        passiveActorLaunchActive = true;
        passiveActorLaunchBecameAirborne = false;
        AudioManager.PlaySound("spring");
    }

    private void UpdatePassiveActorLaunch()
    {
        if (!passiveActorLaunchActive || springLaunchPaused)
        {
            return;
        }

        if (actor == null || actor.IsDied || actorBody == null)
        {
            passiveActorLaunchActive = false;
            passiveActorLaunchBecameAirborne = false;
            ResetPassiveSpringPresentation();
            return;
        }

        if (!actor.IsGround || actorBody.linearVelocity.y > 0.1f)
        {
            passiveActorLaunchBecameAirborne = true;
        }

        if (passiveActorLaunchBecameAirborne
            && actorBody.linearVelocity.y <= 0f
            && buttonTween == null)
        {
            StartPassiveSpringReturn();
        }

        if (passiveActorLaunchBecameAirborne
            && actor.IsGround
            && actorBody.linearVelocity.y <= 0.05f
            && !IsPassiveButtonCollisionBypassActive())
        {
            passiveActorLaunchActive = false;
            passiveActorLaunchBecameAirborne = false;
        }
    }

    private void StartPassiveSpringReturn()
    {
        if (passiveReturnStarted || !passiveBouncePlaying)
        {
            return;
        }

        passiveReturnStarted = true;
        float startRatio = currentCompressionRatio;
        if (startRatio >= -0.0001f)
        {
            CompletePassiveSpringReturn();
            return;
        }

        buttonTween = DOVirtual.Float(
                startRatio,
                0f,
                Mathf.Max(0.01f, passiveReturnDuration),
                SetCompressionRatio)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject)
            .OnComplete(CompletePassiveSpringReturn);
    }

    private void CompletePassiveSpringReturn()
    {
        buttonTween = null;
        passiveReturnStarted = false;
        passiveBouncePlaying = false;
        SetCompressionRatio(0f);
        Physics2D.SyncTransforms();
        EndPassiveButtonCollisionBypass();
    }

    private void ResetPassiveSpringPresentation()
    {
        buttonTween?.Kill();
        buttonTween = null;
        passiveReturnStarted = false;
        passiveBouncePlaying = false;
        SetCompressionRatio(0f);
        Physics2D.SyncTransforms();
        EndPassiveButtonCollisionBypass();
    }

    private void BeginPassiveButtonCollisionBypass()
    {
        if (passiveButtonCollisionBypassActive)
        {
            SetCompressionRatio(0f);
            Physics2D.SyncTransforms();
        }

        EndPassiveButtonCollisionBypass();
        if (actorCollider == null || springButtonCollider == null)
        {
            return;
        }

        passiveIgnoredActorCollider = actorCollider;
        passiveIgnoredButtonCollider = springButtonCollider;
        passiveButtonCollisionWasIgnored = Physics2D.GetIgnoreCollision(
            passiveIgnoredActorCollider,
            passiveIgnoredButtonCollider);
        Physics2D.IgnoreCollision(
            passiveIgnoredActorCollider,
            passiveIgnoredButtonCollider,
            true);
        passiveButtonCollisionBypassActive = true;
    }

    private void EndPassiveButtonCollisionBypass()
    {
        if (passiveIgnoredActorCollider != null && passiveIgnoredButtonCollider != null)
        {
            Physics2D.IgnoreCollision(
                passiveIgnoredActorCollider,
                passiveIgnoredButtonCollider,
                passiveButtonCollisionWasIgnored);
        }

        passiveIgnoredActorCollider = null;
        passiveIgnoredButtonCollider = null;
        passiveButtonCollisionWasIgnored = false;
        passiveButtonCollisionBypassActive = false;
    }

    private bool IsPassiveButtonCollisionBypassActive()
    {
        return passiveButtonCollisionBypassActive;
    }

    private void AnimateButtonHome()
    {
        KillButtonTween();
        float startRatio = currentCompressionRatio;
        if (startRatio <= 0.0001f)
        {
            SetCompressionRatio(0f);
            Physics2D.SyncTransforms();
            EndPassiveButtonCollisionBypass();
            return;
        }

        float extensionRatio = Mathf.Max(0.01f, launchExtensionRatio);
        float duration = Mathf.Max(0.02f, buttonReturnDuration);
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Append(DOVirtual.Float(
            startRatio,
            -extensionRatio,
            duration * 0.65f,
            SetCompressionRatio).SetEase(Ease.OutQuad));
        sequence.Append(DOVirtual.Float(
            -extensionRatio,
            0f,
            duration * 0.35f,
            SetCompressionRatio).SetEase(Ease.OutQuad));
        sequence.OnComplete(() =>
        {
            buttonTween = null;
            SetCompressionRatio(0f);
            Physics2D.SyncTransforms();
            EndPassiveButtonCollisionBypass();
        });
        buttonTween = sequence;
    }

    private void SetCompressionRatio(float ratio)
    {
        float maximumExtension = Mathf.Max(passiveExtensionRatio, launchExtensionRatio);
        currentCompressionRatio = Mathf.Clamp(ratio, -maximumExtension, 1f);
        if (!hasCachedSpringPose)
        {
            return;
        }

        if (springButton != null)
        {
            Vector3 position = buttonRestLocalPosition;
            position.y -= currentCompressionRatio * maximumCompressionDistance;
            springButton.localPosition = position;
        }

        if (springVisual != null)
        {
            springVisual.localPosition = springRestLocalPosition;
            Vector3 scale = springRestLocalScale;
            scale.y = Mathf.LerpUnclamped(
                springRestLocalScale.y,
                springRestLocalScale.y * fullyCompressedSpringScale,
                currentCompressionRatio);
            springVisual.localScale = scale;

            if (springVisualRenderer != null)
            {
                float bottomDelta = springRestBottomWorldY - springVisualRenderer.bounds.min.y;
                springVisual.position += Vector3.up * bottomDelta;
            }
        }

        ApplyCarriedActorPose();
    }

    private void KillButtonTween()
    {
        buttonTween?.Kill();
        buttonTween = null;
        passiveReturnStarted = false;
        passiveBouncePlaying = false;
    }

    #endregion

    #region Contact and coordinate helpers

    private bool IsSpringButtonHitPoint(Vector2 worldPoint)
    {
        Physics2D.SyncTransforms();
        if (springButtonCollider != null
            && springButtonCollider.enabled
            && springButtonCollider.OverlapPoint(worldPoint))
        {
            return true;
        }

        if (springButtonRenderer == null
            || !springButtonRenderer.enabled
            || !springButtonRenderer.gameObject.activeInHierarchy
            || springButtonRenderer.sprite == null)
        {
            return false;
        }

        Bounds bounds = springButtonRenderer.bounds;
        return worldPoint.x >= bounds.min.x
               && worldPoint.x <= bounds.max.x
               && worldPoint.y >= bounds.min.y
               && worldPoint.y <= bounds.max.y;
    }

    private bool IsActorStandingOnButton()
    {
        if (actor == null
            || actor.IsDied
            || actorCollider == null
            || springButtonCollider == null
            || !actorCollider.enabled
            || !springButtonCollider.enabled)
        {
            return false;
        }

        Bounds actorBounds = actorCollider.bounds;
        Bounds buttonBounds = springButtonCollider.bounds;
        bool horizontalOverlap = actorBounds.max.x >= buttonBounds.min.x
                                 && actorBounds.min.x <= buttonBounds.max.x;
        float verticalGap = actorBounds.min.y - buttonBounds.max.y;
        bool actorAbove = actorBounds.center.y >= buttonBounds.center.y;
        bool movingDownOrStill = actor.IsGround
                                 || actorBody == null
                                 || actorBody.linearVelocity.y <= 0.5f;

        return horizontalOverlap
               && actorAbove
               && movingDownOrStill
               && verticalGap >= -standingTolerance
               && verticalGap <= standingTolerance;
    }

    private bool IsActorHorizontallyOverButton()
    {
        if (actorCollider == null
            || springButtonCollider == null
            || !actorCollider.enabled
            || !springButtonCollider.enabled)
        {
            return false;
        }

        Bounds actorBounds = actorCollider.bounds;
        Bounds buttonBounds = springButtonCollider.bounds;
        return actorBounds.max.x >= buttonBounds.min.x
               && actorBounds.min.x <= buttonBounds.max.x;
    }

    private bool IsActorCollider(Collider2D other)
    {
        if (other == null || actor == null)
        {
            return false;
        }

        return other.transform == actor.transform
               || other.transform.IsChildOf(actor.transform)
               || LayerMask.LayerToName(other.gameObject.layer) == "Actor";
    }

    private bool TryGetWorldPoint(Vector2 screenPosition, out Vector2 worldPoint)
    {
        if (inputCamera == null)
        {
            inputCamera = Camera.main;
            if (inputCamera == null)
            {
                inputCamera = FindFirstObjectByType<Camera>();
            }
        }

        if (inputCamera == null)
        {
            worldPoint = default;
            return false;
        }

        float planeZ = springButton != null ? springButton.position.z : transform.position.z;
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));
        if (!plane.Raycast(ray, out float distance))
        {
            worldPoint = default;
            return false;
        }

        worldPoint = ray.GetPoint(distance);
        return true;
    }

    private bool IsCoverHitPoint(Vector2 screenPosition)
    {
        if (!TryGetWorldPoint(screenPosition, out Vector2 worldPoint))
        {
            return false;
        }

        if (fakeGroundCoverCollider != null && fakeGroundCoverCollider.enabled)
        {
            return fakeGroundCoverCollider.OverlapPoint(worldPoint);
        }

        if (fakeGroundCover == null || !fakeGroundCover.enabled)
        {
            return false;
        }

        Bounds bounds = fakeGroundCover.bounds;
        return worldPoint.x >= bounds.min.x
               && worldPoint.x <= bounds.max.x
               && worldPoint.y >= bounds.min.y
               && worldPoint.y <= bounds.max.y;
    }

    #endregion

    #region Reference setup and cleanup

    private bool ResolveReferences()
    {
        levelCtrl ??= GetComponent<BaseLevelCtrl>() ?? GetComponentInParent<BaseLevelCtrl>();
        if (actor == null && levelCtrl != null)
        {
            actor = levelCtrl.Actor;
        }

        if (springRoot == null)
        {
            springRoot = FindNamedChild(transform, "Spring");
        }

        if (springButton == null && springButtonCollider != null)
        {
            springButton = springButtonCollider.transform;
        }

        if (springButton == null && springRoot != null)
        {
            springButton = FindNamedChild(springRoot, "Sp_button_1_02");
        }

        springButtonCollider ??= springButton != null ? springButton.GetComponent<Collider2D>() : null;
        springButtonBody ??= springButton != null ? springButton.GetComponent<Rigidbody2D>() : null;
        legacySpringJoint ??= springButton != null ? springButton.GetComponent<SpringJoint2D>() : null;
        springButtonRenderer ??= springButton != null ? springButton.GetComponent<SpriteRenderer>() : null;

        if (springVisual == null && springRoot != null)
        {
            springVisual = FindNamedChild(springRoot, "Sp_spring_1_01");
        }

        springVisualRenderer ??= springVisual != null ? springVisual.GetComponent<SpriteRenderer>() : null;

        if (fakeGroundCover == null && springRoot != null)
        {
            Transform cover = FindNamedChild(springRoot, "Sp_l94_dm_01");
            fakeGroundCover = cover != null ? cover.GetComponent<SpriteRenderer>() : null;
        }

        fakeGroundCoverCollider ??= fakeGroundCover != null
            ? fakeGroundCover.GetComponent<Collider2D>()
            : null;

        if (revealedGround == null && springRoot != null)
        {
            Transform revealed = FindNamedChild(springRoot, "Sp_l94_dm_02");
            revealedGround = revealed != null ? revealed.gameObject : null;
        }

        if (actor != null)
        {
            actorCollider ??= actor.GetComponent<Collider2D>();
            actorBody ??= actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>();
        }

        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }

        bool valid = levelCtrl != null
                     && actor != null
                     && actorCollider != null
                     && actorBody != null
                     && springButton != null
                     && springButtonCollider != null
                     && fakeGroundCover != null;
        if (!valid && !hasLoggedMissingReferences)
        {
            hasLoggedMissingReferences = true;
            Debug.LogWarning("[Level94Ctrl] Required level, actor, cover, or spring button reference is missing.", this);
        }

        return valid;
    }

    private void CacheInitialState()
    {
        if (fakeGroundCover != null && !hasCachedCoverColor)
        {
            coverRestColor = fakeGroundCover.color;
            coverRestColor.a = 1f;
            hasCachedCoverColor = true;
        }

        if (fakeGroundCover != null && !hasCachedCoverPose)
        {
            coverRestLocalPosition = fakeGroundCover.transform.localPosition;
            hasCachedCoverPose = true;
        }

        if (springButton == null || springVisual == null || hasCachedSpringPose)
        {
            return;
        }

        buttonRestLocalPosition = springButton.localPosition;
        springRestLocalPosition = springVisual.localPosition;
        springRestLocalScale = springVisual.localScale;
        springVisualRenderer ??= springVisual.GetComponent<SpriteRenderer>();
        springRestBottomWorldY = springVisualRenderer != null
            ? springVisualRenderer.bounds.min.y
            : springVisual.position.y;
        hasCachedSpringPose = true;
    }

    private void PrepareAttempt()
    {
        KillButtonTween();
        if (resumeSpringActorCoroutine != null)
        {
            StopCoroutine(resumeSpringActorCoroutine);
            resumeSpringActorCoroutine = null;
        }

        RestorePausedSpringLaunch(false);
        if (actorOwnedBySpring && actorBody != null)
        {
            actorBody.simulated = actorBodyWasSimulated;
            actorBody.linearVelocity = Vector2.zero;
        }

        hasSucceeded = false;
        isMoonFlight = false;
        isInsufficientLaunch = false;
        insufficientLaunchElapsed = 0f;
        actorOwnedBySpring = false;
        isCarryingActor = false;
        actorInsideSpringTrigger = false;
        actorWasStanding = false;
        passiveBounceArmed = true;
        passiveBouncePending = false;
        passiveBouncePlaying = false;
        passiveReturnStarted = false;
        passiveBounceAt = 0f;
        nextPassiveBounceAt = 0f;
        passiveActorLaunchActive = false;
        passiveActorLaunchBecameAirborne = false;
        springLaunchPaused = false;
        isCoverRemoved = false;
        isCoverFading = false;
        currentCompressionRatio = 0f;
        ClearPointerState();

        CacheInitialState();
        PrepareButtonBody();
        SetCompressionRatio(0f);
        Physics2D.SyncTransforms();
        EndPassiveButtonCollisionBypass();
        RestoreFloorCollider();

        if (revealedGround != null)
        {
            revealedGround.SetActive(true);
        }

        if (fakeGroundCover != null)
        {
            fakeGroundCover.gameObject.SetActive(true);
            if (hasCachedCoverPose)
            {
                fakeGroundCover.transform.localPosition = coverRestLocalPosition;
            }

            fakeGroundCover.color = hasCachedCoverColor ? coverRestColor : Color.white;
        }

        if (fakeGroundCoverCollider != null)
        {
            fakeGroundCoverCollider.enabled = true;
        }

        RestoreActorVisualState();
    }

    private void PrepareButtonBody()
    {
        if (springButtonBody != null)
        {
            springButtonBody.simulated = true;
            springButtonBody.bodyType = RigidbodyType2D.Kinematic;
            springButtonBody.gravityScale = 0f;
            springButtonBody.linearVelocity = Vector2.zero;
            springButtonBody.angularVelocity = 0f;
        }

        if (legacySpringJoint != null)
        {
            legacySpringJoint.enabled = false;
        }
    }

    private void CleanupInterruptedAttempt()
    {
        bool clearPassiveLaunchVelocity = passiveActorLaunchActive && !actorOwnedBySpring;
        coverTween?.Kill();
        coverTween = null;
        KillButtonTween();
        moonFlightTween?.Kill();
        moonFlightTween = null;
        if (resumeSpringActorCoroutine != null)
        {
            StopCoroutine(resumeSpringActorCoroutine);
            resumeSpringActorCoroutine = null;
        }

        ClearPointerState();
        passiveBounceArmed = false;
        passiveBouncePending = false;
        passiveActorLaunchActive = false;
        passiveActorLaunchBecameAirborne = false;
        isCoverFading = false;
        isInsufficientLaunch = false;

        // 中断主动拖拽时先携带角色回到按钮休止位，再恢复刚体，避免角色留在井内。
        if (isCarryingActor && actorOwnedBySpring)
        {
            SetCompressionRatio(0f);
            ApplyCarriedActorPose();
            Physics2D.SyncTransforms();
        }

        isCarryingActor = false;
        RestorePausedSpringLaunch(false);
        ReleaseActorOwnership(!hasSucceeded);
        if (clearPassiveLaunchVelocity && actorBody != null)
        {
            actorBody.linearVelocity = Vector2.zero;
        }

        if (!hasSucceeded)
        {
            isMoonFlight = false;
            RestoreActorVisualState();
            RestoreFloorCollider();
        }

        SetCompressionRatio(0f);
        Physics2D.SyncTransforms();
        EndPassiveButtonCollisionBypass();
    }

    private void CacheActorRendererGroup()
    {
        if (actor == null)
        {
            return;
        }

        actorRendererGroup ??= actor.GetComponentInChildren<SpriteRendererGroupAlpha>(true);
        if (actorRendererGroup == null)
        {
            actorRendererInitialAlpha = 1f;
            return;
        }

        actorRendererGroup.CollectRenderers();
        actorRendererInitialAlpha = actorRendererGroup.GetAlpha();
    }

    private void SetActorVisualAlpha(float alpha)
    {
        if (actor == null)
        {
            return;
        }

        float clampedAlpha = Mathf.Clamp01(alpha);
        actor.SetSpineAlpha(clampedAlpha);
        if (actorRendererGroup != null)
        {
            actorRendererGroup.SetAlpha(actorRendererInitialAlpha * clampedAlpha);
        }
    }

    private void RestoreActorVisualState()
    {
        if (actor == null || (moonFlightInitialScale == Vector3.zero && actorRendererGroup == null))
        {
            return;
        }

        actor.SetSpineAlpha(1f);
        if (actorRendererGroup != null)
        {
            actorRendererGroup.SetAlpha(actorRendererInitialAlpha);
        }

        if (moonFlightInitialScale != Vector3.zero)
        {
            actor.transform.localScale = moonFlightInitialScale;
        }
    }

    private static void SetRendererAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null)
        {
            return;
        }

        Color color = renderer.color;
        color.a = Mathf.Clamp01(alpha);
        renderer.color = color;
    }

    private static Transform FindNamedChild(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamedChild(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    #endregion
}

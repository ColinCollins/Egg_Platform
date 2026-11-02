using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Scripts.Common;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Level133Ctrl : MonoBehaviour, IEventSender
{
    private const string CountdownSfxTag = "normalScore";
    private const float CountdownPulseScale = 1.35f;
    private const float CountdownPulseGrowDuration = 0.12f;
    private const float CountdownPulseReturnDuration = 0.18f;
    private const float ActorLandingYTolerance = 0.03f;
    private const float ActorLandingVelocityTolerance = 0.1f;
    private const float ActorInputEpsilon = 0.001f;
    private const int ActorCastHitCapacity = 16;

    [Header("Mechanism")]
    [SerializeField] private Rigidbody2D pillarBody;
    [SerializeField] private Rigidbody2D buttonBody;
    [SerializeField] private TMP_Text countdownText;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float cycleDuration = 8f;
    [SerializeField, Min(0.01f)] private float raiseDuration = 1f;
    [SerializeField, Min(0.01f)] private float lowerDuration = 1f;
    [Tooltip("活动按钮按下与回弹共用的单段过渡时长；中间保持 cycleDuration。")]
    [SerializeField, Min(0f)] private float buttonPressDuration = 0.15f;

    [Header("Motion")]
    [SerializeField] private float pillarLiftDistance = 4f;
    [SerializeField] private float buttonPressDistance = 0.15f;

    [Header("Actor Entry")]
    [Tooltip("实体承托层。延长后的 Plantform_short 与主地面均使用 Ground。")]
    [SerializeField] private LayerMask actorLandingLayers = 1 << 6;
    [SerializeField, Min(0.1f)] private float actorLandingCastDistance = 20f;
    [SerializeField, Range(0f, 1f)] private float actorLandingNormalThreshold = 0.25f;

    private readonly RaycastHit2D[] actorCastHits = new RaycastHit2D[ActorCastHitCapacity];

    private EventSubscriber subscriber;
    private BaseLevelCtrl levelCtrl;
    private ActorCtrl actor;
    private Rigidbody2D actorBody;
    private Collider2D actorCollider;
    private DragableItem actorDrag;
    private Collider2D buttonCollider;

    private Vector2 pillarRestPosition;
    private Vector2 pillarHighPosition;
    private Vector2 buttonRestPosition;
    private Vector2 buttonPressedPosition;
    private Vector2 desiredPillarPosition;
    private Vector2 desiredButtonPosition;
    private Vector3 countdownBaseScale = Vector3.one;
    private Vector3 actorAirPosition;
    private Vector3 actorGroundPosition;
    private Tween countdownTween;
    private float elapsed;
    private int lastDisplayedSeconds = -1;
    private bool initialized;
    private bool cycleActive;
    private bool paused;
    private bool actorStartupPending;
    private bool actorIntroActive;
    private bool actorEntryReady;
    private bool actorEntryBlockDispatched;
    private bool actorGroundPositionResolved;
    private bool actorPhysicsCaptured;
    private RigidbodyType2D actorOriginalBodyType;
    private RigidbodyConstraints2D actorOriginalConstraints;
    private bool actorOriginalSimulated;
    private float actorOriginalGravityScale;

    private void Awake()
    {
        ResolveReferences();
        CaptureInitialPose();
        PrepareActorStartup();
    }

    private void Start()
    {
        // ActorCtrl.Start must finish before its movement lock is applied.
        actorStartupPending = true;
    }

    private void OnEnable()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        subscriber.Subscribe<GameResumeEvent>(OnGameResume);

        if (initialized)
            ResetMechanism();
    }

    private void Update()
    {
        if (actorStartupPending)
        {
            actorStartupPending = false;
            BeginActorEntry();
        }

        if (!cycleActive || paused)
            return;

        float totalTimelineDuration = GetTotalTimelineDuration();
        elapsed = Mathf.Min(totalTimelineDuration, elapsed + Time.deltaTime);
        UpdateDesiredPositions();
        RefreshCountdownDisplay();

        if (elapsed >= totalTimelineDuration)
            CompleteCycle();
    }

    private void LateUpdate()
    {
        if (actorIntroActive && !paused && HasActorReachedLanding())
            CompleteActorIntro();

        if (!actorIntroActive && actorEntryReady)
            SuppressButtonPushAnimation();
    }

    private void FixedUpdate()
    {
        if (actorIntroActive && actorBody != null)
        {
            if (paused)
            {
                actorBody.linearVelocity = Vector2.zero;
            }
            else
            {
                actorBody.linearVelocity = new Vector2(0f, actorBody.linearVelocityY);
            }
        }

        if (!initialized)
            return;

        pillarBody.MovePosition(desiredPillarPosition);
        buttonBody.MovePosition(desiredButtonPosition);
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref subscriber);
        countdownTween?.Kill();
        countdownTween = null;
        CancelActorIntro();
        SetActorEntryBlock(false);
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref subscriber);
        countdownTween?.Kill();
        CancelActorIntro();
    }

    public void OnButtonPressed(Collider2D collider)
    {
        if (!initialized || !actorEntryReady || cycleActive || paused ||
            collider == null || !collider.CompareTag("Player"))
            return;

        if (actor == null || actor.IsDied)
            return;

        cycleActive = true;
        elapsed = 0f;
        lastDisplayedSeconds = -1;
        desiredPillarPosition = pillarRestPosition;
        desiredButtonPosition = buttonRestPosition;
        HideCountdown();
        AudioManager.PlaySound("boardTrigger");
    }

    private void ResolveReferences()
    {
        levelCtrl = GetComponentInParent<BaseLevelCtrl>(true);
        actor = levelCtrl != null ? levelCtrl.Actor : null;
        actor ??= GetComponentInChildren<ActorCtrl>(true);

        pillarBody ??= transform.Find("Level133_SpikePillar")?.GetComponent<Rigidbody2D>();
        buttonBody ??= transform.Find("Level133_SideButton/Sp_l133_an_01")?.GetComponent<Rigidbody2D>();
        countdownText ??= GetComponentInChildren<TMP_Text>(true);

        if (actor != null)
        {
            actorBody ??= actor.GetComponent<Rigidbody2D>();
            actorCollider ??= actor.GetComponent<Collider2D>();
            actorDrag ??= actor.GetComponent<DragableItem>();
        }

        buttonCollider ??= buttonBody != null ? buttonBody.GetComponent<Collider2D>() : null;
    }

    private void CaptureInitialPose()
    {
        if (pillarBody == null || buttonBody == null || countdownText == null)
        {
            Debug.LogError("[Level133Ctrl] Pillar, button, or countdown reference is missing.", this);
            return;
        }

        pillarRestPosition = pillarBody.position;
        pillarHighPosition = pillarRestPosition + Vector2.up * pillarLiftDistance;
        buttonRestPosition = buttonBody.position;
        buttonPressedPosition = buttonRestPosition + Vector2.right * buttonPressDistance;
        desiredPillarPosition = pillarRestPosition;
        desiredButtonPosition = buttonRestPosition;
        countdownBaseScale = countdownText.transform.localScale;
        initialized = true;
    }

    private void PrepareActorStartup()
    {
        ResolveReferences();
        if (actor == null || actorBody == null)
        {
            actorEntryReady = true;
            return;
        }

        actorEntryReady = false;
        actorAirPosition = actor.transform.position;

        if (!actorPhysicsCaptured)
        {
            actorOriginalBodyType = actorBody.bodyType;
            actorOriginalConstraints = actorBody.constraints;
            actorOriginalSimulated = actorBody.simulated;
            actorOriginalGravityScale = actorBody.gravityScale;
            actorPhysicsCaptured = true;
        }

        actorBody.linearVelocity = Vector2.zero;
        actorBody.angularVelocity = 0f;
        actorBody.simulated = false;
        Physics2D.SyncTransforms();

        actorGroundPositionResolved = TryResolveActorGroundPosition(
            actorAirPosition,
            out actorGroundPosition);

        actorDrag?.SetEnableDrag(false);
    }

    private void BeginActorEntry()
    {
        ResolveReferences();
        if (actor == null || actorBody == null)
        {
            actorEntryReady = true;
            return;
        }

        if (!actorPhysicsCaptured)
            PrepareActorStartup();

        if (ShouldPlayActorIntro())
        {
            StartActorIntro();
        }
        else
        {
            PlaceActorAtGround();
        }
    }

    private bool ShouldPlayActorIntro()
    {
        LevelRuntimeState runtimeState = PlayCtrl.Instance?.Level?.CurrentLevelState;
        return runtimeState != null && runtimeState.ResetCount <= 0 && runtimeState.FailCount <= 0;
    }

    private void StartActorIntro()
    {
        actorEntryReady = false;
        actorIntroActive = true;
        actor.transform.position = actorAirPosition;
        actorBody.bodyType = RigidbodyType2D.Dynamic;
        actorBody.simulated = true;
        actorBody.gravityScale = Mathf.Max(0.01f, actorOriginalGravityScale);
        actorBody.constraints = actorOriginalConstraints | RigidbodyConstraints2D.FreezePositionX;
        actorBody.linearVelocity = Vector2.zero;
        actorBody.angularVelocity = 0f;
        Physics2D.SyncTransforms();

        actor.SetMoveInput(0f);
        actor.StopMoving();
        actorDrag?.SetEnableDrag(false);
        levelCtrl?.SetCtrlable(false);
        SetActorEntryBlock(true);
    }

    private void PlaceActorAtGround()
    {
        actorIntroActive = false;

        if (!actorGroundPositionResolved)
        {
            actorGroundPositionResolved = TryResolveActorGroundPosition(
                actorAirPosition,
                out actorGroundPosition);
        }

        if (actorGroundPositionResolved)
        {
            actor.transform.position = actorGroundPosition;
        }
        else
        {
            Debug.LogWarning("[Level133Ctrl] Actor ground support could not be resolved.", this);
            actor.transform.position = actorAirPosition;
        }

        RestoreActorPhysics();
        ApplyActorReadyState();
        actor.UpdateAnimation();
        actorEntryReady = true;
        SetActorEntryBlock(false);
        Physics2D.SyncTransforms();
    }

    private bool HasActorReachedLanding()
    {
        if (actor == null || actorBody == null)
            return false;

        if (actorBody.linearVelocityY > ActorLandingVelocityTolerance)
            return false;

        if (actorGroundPositionResolved)
        {
            return actor.IsGround &&
                   actorBody.position.y <= actorGroundPosition.y + ActorLandingYTolerance &&
                   Mathf.Abs(actorBody.linearVelocityY) <= ActorLandingVelocityTolerance;
        }

        return actor.IsGround;
    }

    private void CompleteActorIntro()
    {
        if (!actorIntroActive)
            return;

        actorIntroActive = false;

        if (actorGroundPositionResolved)
            actor.transform.position = actorGroundPosition;

        RestoreActorPhysics();
        ApplyActorReadyState();
        actor.UpdateAnimation();
        actorEntryReady = true;
        SetActorEntryBlock(false);
        Physics2D.SyncTransforms();
    }

    private void CancelActorIntro()
    {
        actorIntroActive = false;
        RestoreActorPhysics();
    }

    private void ApplyActorReadyState()
    {
        if (actor == null)
            return;

        actor.SetMoveInput(0f);
        if (paused)
        {
            actor.StopMoving();
            actorDrag?.SetEnableDrag(false);
            levelCtrl?.SetCtrlable(false);
            return;
        }

        actor.ResumeMoving();
        actorDrag?.SetEnableDrag(true);
        levelCtrl?.SetCtrlable(true);
    }

    private void RestoreActorPhysics()
    {
        if (!actorPhysicsCaptured || actorBody == null)
            return;

        actorBody.bodyType = actorOriginalBodyType;
        actorBody.constraints = actorOriginalConstraints;
        actorBody.simulated = actorOriginalSimulated;
        actorBody.gravityScale = actorOriginalGravityScale;
        actorBody.linearVelocity = Vector2.zero;
        actorBody.angularVelocity = 0f;
        actorPhysicsCaptured = false;
    }

    private void SetActorEntryBlock(bool blocked)
    {
        if (actorEntryBlockDispatched == blocked)
            return;

        actorEntryBlockDispatched = blocked;
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, blocked);
    }

    private bool TryResolveActorGroundPosition(Vector3 origin, out Vector3 supportPosition)
    {
        supportPosition = origin;
        if (actorCollider == null)
            return false;

        if (actorLandingLayers.value == 0)
            actorLandingLayers = LayerMask.GetMask("Ground");

        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = false
        };
        filter.SetLayerMask(actorLandingLayers);

        int hitCount = actorCollider.Cast(
            Vector2.down,
            filter,
            actorCastHits,
            Mathf.Max(0.1f, actorLandingCastDistance));

        RaycastHit2D nearestHit = default;
        float nearestDistance = float.PositiveInfinity;
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit2D hit = actorCastHits[index];
            if (hit.collider == null || hit.collider.isTrigger || hit.distance < 0f)
                continue;

            if (hit.collider.transform == actor.transform || hit.collider.transform.IsChildOf(actor.transform))
                continue;

            if (hit.normal.y < actorLandingNormalThreshold || hit.distance >= nearestDistance)
                continue;

            nearestHit = hit;
            nearestDistance = hit.distance;
        }

        if (nearestHit.collider == null)
            return false;

        supportPosition = origin + Vector3.down * Mathf.Max(0f, nearestHit.distance);
        return true;
    }

    private void SuppressButtonPushAnimation()
    {
        if (actor == null || actorCollider == null || buttonCollider == null ||
            actor.IsDied || !actor.IsGround || Mathf.Abs(actor.XInput) <= ActorInputEpsilon)
            return;

        if (!buttonCollider.IsTouching(actorCollider) &&
            !buttonCollider.bounds.Intersects(actorCollider.bounds))
            return;

        actor.SpineCtrl?.PlayAnimation("walk", true);
    }

    private void UpdateDesiredPositions()
    {
        float safeCycleDuration = Mathf.Max(0.01f, cycleDuration);
        float safeButtonTransitionDuration = GetSafeButtonTransitionDuration();
        float mechanismElapsed = Mathf.Clamp(
            elapsed - safeButtonTransitionDuration,
            0f,
            safeCycleDuration);
        float safeRaiseDuration = Mathf.Min(Mathf.Max(0.01f, raiseDuration), safeCycleDuration);
        float safeLowerDuration = Mathf.Min(Mathf.Max(0.01f, lowerDuration), safeCycleDuration);
        float lowerStart = Mathf.Max(safeRaiseDuration, safeCycleDuration - safeLowerDuration);

        if (mechanismElapsed <= safeRaiseDuration)
        {
            desiredPillarPosition = Vector2.Lerp(
                pillarRestPosition,
                pillarHighPosition,
                mechanismElapsed / safeRaiseDuration);
        }
        else if (mechanismElapsed < lowerStart)
        {
            desiredPillarPosition = pillarHighPosition;
        }
        else
        {
            desiredPillarPosition = Vector2.Lerp(
                pillarHighPosition,
                pillarRestPosition,
                Mathf.Clamp01((mechanismElapsed - lowerStart) / safeLowerDuration));
        }

        if (elapsed < safeButtonTransitionDuration)
        {
            float pressProgress = Mathf.Clamp01(elapsed / safeButtonTransitionDuration);
            pressProgress = 1f - (1f - pressProgress) * (1f - pressProgress);
            desiredButtonPosition = Vector2.Lerp(
                buttonRestPosition,
                buttonPressedPosition,
                pressProgress);
            return;
        }

        float reboundStart = safeButtonTransitionDuration + safeCycleDuration;
        if (elapsed < reboundStart)
        {
            desiredButtonPosition = buttonPressedPosition;
            return;
        }

        float reboundProgress = Mathf.Clamp01(
            (elapsed - reboundStart) / safeButtonTransitionDuration);
        reboundProgress = 1f - (1f - reboundProgress) * (1f - reboundProgress);
        desiredButtonPosition = Vector2.Lerp(
            buttonPressedPosition,
            buttonRestPosition,
            reboundProgress);
    }

    private void RefreshCountdownDisplay()
    {
        float safeCycleDuration = Mathf.Max(0.01f, cycleDuration);
        float safeButtonTransitionDuration = GetSafeButtonTransitionDuration();
        float mechanismElapsed = elapsed - safeButtonTransitionDuration;
        if (mechanismElapsed < 0f || mechanismElapsed >= safeCycleDuration)
        {
            HideCountdown();
            return;
        }

        float remaining = safeCycleDuration - mechanismElapsed;
        int displayedSeconds = Mathf.CeilToInt(remaining);
        if (displayedSeconds <= 0)
        {
            HideCountdown();
            return;
        }

        if (countdownText.gameObject.activeSelf && displayedSeconds == lastDisplayedSeconds)
            return;

        lastDisplayedSeconds = displayedSeconds;
        countdownTween?.Kill();
        countdownText.text = displayedSeconds.ToString();
        AudioManager.PlaySound(CountdownSfxTag);
        countdownText.transform.localScale = countdownBaseScale;
        countdownText.gameObject.SetActive(true);

        countdownTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(countdownText.gameObject)
            .Append(countdownText.transform
                .DOScale(countdownBaseScale * CountdownPulseScale, CountdownPulseGrowDuration)
                .SetEase(Ease.OutQuad))
            .Append(countdownText.transform
                .DOScale(countdownBaseScale, CountdownPulseReturnDuration)
                .SetEase(Ease.OutBack));
    }

    private void CompleteCycle()
    {
        cycleActive = false;
        elapsed = GetTotalTimelineDuration();
        desiredPillarPosition = pillarRestPosition;
        desiredButtonPosition = buttonRestPosition;
        pillarBody.position = pillarRestPosition;
        buttonBody.position = buttonRestPosition;
        HideCountdown();
    }

    private float GetSafeButtonTransitionDuration()
    {
        return Mathf.Max(0.01f, buttonPressDuration);
    }

    private float GetTotalTimelineDuration()
    {
        return Mathf.Max(0.01f, cycleDuration) + 2f * GetSafeButtonTransitionDuration();
    }

    private void ResetMechanism()
    {
        cycleActive = false;
        paused = false;
        elapsed = 0f;
        lastDisplayedSeconds = -1;
        desiredPillarPosition = pillarRestPosition;
        desiredButtonPosition = buttonRestPosition;
        pillarBody.position = pillarRestPosition;
        buttonBody.position = buttonRestPosition;
        HideCountdown();
    }

    private void HideCountdown()
    {
        countdownTween?.Kill();
        countdownTween = null;
        lastDisplayedSeconds = -1;

        if (countdownText == null)
            return;

        countdownText.transform.localScale = countdownBaseScale;
        countdownText.gameObject.SetActive(false);
    }

    private void OnGamePause(GamePauseEvent _)
    {
        paused = true;
        if (actorIntroActive && actorBody != null)
            actorBody.linearVelocity = Vector2.zero;
        else if (actorEntryReady)
            ApplyActorReadyState();
    }

    private void OnGameResume(GameResumeEvent _)
    {
        paused = false;
        if (actorEntryReady && !actorIntroActive)
            ApplyActorReadyState();
    }

}

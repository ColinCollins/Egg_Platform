using System;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Level.Actor;
using Game.Scripts.Common;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Level124 专用逻辑：横置的 8 表示无限循环，转正并锁定后才开始有限次数倒计数。
/// </summary>
public sealed class Level124Ctrl : MonoBehaviour, IEventSender
{
    private const int InitialDoorCount = 8;
    private const string DoorIdleAnimation = "door_idle";
    private const string DoorOpenAnimation = "door_open";
    private const string DoorCloseAnimation = "dose_close";
    private const float StartDoorOpenDelay = 0.4f;
    private const float StartDoorActorExitDuration = 0.2f;
    private const float StartDoorCloseDelay = 0.2f;
    private const float StartDoorCloseSfxDelay = 0.25f;

    [Header("Level References")]
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private Transform exitDoorRoot;
    [SerializeField] private ActorSpineCtrl exitDoorSpine;
    [SerializeField] private Collider2D exitDoorTrigger;
    [SerializeField] private Transform startDoorRoot;
    [SerializeField] private ActorSpineCtrl startDoorSpine;
    [SerializeField] private RectTransform counterRoot;
    [SerializeField] private SpriteRenderer actorShadow;
    [SerializeField] private SpriteRendererGroupAlpha actorRendererGroup;

    [Header("Counter Rotation")]
    [Tooltip("横置数字距离 0 或 180 度小于该角度时吸附为正向 8，并永久锁定。")]
    [SerializeField, Range(1f, 20f)] private float uprightLockAngle = 7.5f;
    [Tooltip("数字渲染边界以外仍可开始旋转的世界坐标余量。")]
    [SerializeField, Min(0f)] private float rotationHitPadding = 0.35f;

    [Header("Loop Door Flow")]
    [Tooltip("仅影响本关普通循环的门动画与角色吸入时序；1.2 表示速度提升 20%。")]
    [SerializeField, Min(0.1f)] private float flowSpeedMultiplier = 1.2f;
    [SerializeField, Min(0f)] private float doorOpenDelay = 0.4f;
    [SerializeField, Min(0.05f)] private float actorEnterDuration = 0.5f;
    [SerializeField] private Vector3 actorEnterOffset = new(0f, 1f, 0f);
    [SerializeField, Min(0f)] private float doorCloseDelay = 0.15f;
    [SerializeField, Min(0f)] private float closeDoorSfxDelay = 0.25f;
    [Tooltip("出口门开始关门后，到初始门开始开门之间的实际停顿秒数。")]
    [SerializeField, Min(0f)] private float exitCloseToEntranceOpenDelay = 0.22f;

    [Header("Minus One Feedback")]
    [SerializeField] private Vector2 minusOneStartOffset = new(0f, 0.18f);
    [SerializeField, Min(0f)] private float minusOneRiseDistance = 0.72f;
    [SerializeField, Min(0.1f)] private float minusOneDuration = 0.55f;
    [SerializeField, Range(0.1f, 2f)] private float minusOneScale = 0.82f;

    private TMP_Text counterText;
    private Transform exitDoorTarget;
    private Rigidbody2D actorBody;
    private Camera mainCamera;
    private EventSubscriber eventSubscriber;
    private bool teardownStarted;
    private ActorSkinBinding actorSkinBinding;
    private Sequence actorEnterSequence;
    private Sequence actorStartDoorExitSequence;
    private Sequence minusOneSequence;
    private Spine.TrackEntry exitDoorTrackEntry;
    private Spine.TrackEntry startDoorTrackEntry;
    private Spine.TrackEntry actorFlowTrackEntry;
    private TextMeshPro minusOneText;
    private RectTransform minusOneRect;

    private Vector3 actorInitialLocalPosition;
    private Vector3 actorInitialLocalScale;
    private Quaternion actorInitialLocalRotation;
    private bool actorInitialSimulated;
    private bool hasCachedActorState;
    private float initialCounterAngle;
    private float actorFlowOriginalTimeScale = 1f;
    private float currentActorAlpha = 1f;
    private int remainingDoorCount = InitialDoorCount;
    private int doorFlowVersion;
    private bool isFiniteCounter;
    private bool isDoorFlowPlaying;
    private bool isFinalSuccessAccepted;
    private bool isPaused;
    private bool isPanelBlocked;
    private bool isActorLeavingStartDoor;

    private bool isCounterDragging;
    private float dragStartPointerAngle;
    private float dragStartCounterAngle;

    private void Awake()
    {
        ResolveReferences();
        CacheInitialState();
        InitializeCounter();
        CreateMinusOneFeedback();
    }

    private void OnEnable()
    {
        teardownStarted = false;
        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<GameResetEvent>(OnGameReset);
        eventSubscriber.Subscribe<GameReplayEvent>(OnGameReplay);
        eventSubscriber.Subscribe<GamePauseEvent>(OnGamePause);
        eventSubscriber.Subscribe<GameResumeEvent>(OnGameResume);
        BindActorSkinCallbacks();
    }

    private void Update()
    {
        UpdateCounterRotationInput();
    }

    private void OnDisable()
    {
        BeginTeardown();
    }

    private void OnDestroy() => BeginTeardown();

    private void UnbindRuntimeCallbacks()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        UnbindActorSkinCallbacks();
    }

    /// <summary>
    /// 仅供 Level124 出口门的 OnTrigger2DHandle 绑定。
    /// </summary>
    public void OnActorEnterDoor(Collider2D collider)
    {
        if (!Application.isPlaying || !IsValidActorCollider(collider))
            return;

        if (isPaused || isDoorFlowPlaying || isFinalSuccessAccepted)
            return;

        if (isFiniteCounter && remainingDoorCount <= 1)
        {
            isFinalSuccessAccepted = true;
            isCounterDragging = false;
            levelCtrl.OnActorTrigger2D(collider);
            return;
        }

        PlayLoopDoorFlowAsync(++doorFlowVersion).Forget();
    }

    #region Counter Rotation

    private void UpdateCounterRotationInput()
    {
        if (!Application.isPlaying || isFiniteCounter || isDoorFlowPlaying || isFinalSuccessAccepted || isPaused)
        {
            isCounterDragging = false;
            return;
        }

        if (!TryReadPointer(out Vector2 pointerPosition, out bool pressed, out bool held, out bool released))
            return;

        if (pressed)
        {
            if (InputUtils.IsPointerOverUI(pointerPosition) || !IsPointerOverCounter(pointerPosition))
                return;

            Vector2 pivotScreenPosition = mainCamera.WorldToScreenPoint(counterRoot.position);
            dragStartPointerAngle = CalculatePointerAngle(pivotScreenPosition, pointerPosition);
            dragStartCounterAngle = NormalizeAngle(counterRoot.localEulerAngles.z);
            isCounterDragging = true;
        }

        if (isCounterDragging && held)
        {
            Vector2 pivotScreenPosition = mainCamera.WorldToScreenPoint(counterRoot.position);
            float pointerAngle = CalculatePointerAngle(pivotScreenPosition, pointerPosition);
            float angleDelta = Mathf.DeltaAngle(dragStartPointerAngle, pointerAngle);
            float newAngle = dragStartCounterAngle + angleDelta;
            counterRoot.localRotation = Quaternion.Euler(0f, 0f, newAngle);
            TryLockFiniteCounter(newAngle);
        }

        if (released)
            isCounterDragging = false;
    }

    private bool TryReadPointer(
        out Vector2 pointerPosition,
        out bool pressed,
        out bool held,
        out bool released)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            pointerPosition = touch.position;
            pressed = touch.phase == TouchPhase.Began;
            held = touch.phase == TouchPhase.Began ||
                   touch.phase == TouchPhase.Moved ||
                   touch.phase == TouchPhase.Stationary;
            released = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            return true;
        }

        pointerPosition = Input.mousePosition;
        pressed = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        released = Input.GetMouseButtonUp(0);
        return pressed || held || released;
    }

    private bool IsPointerOverCounter(Vector2 screenPosition)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null || counterText == null)
            return false;

        counterText.ForceMeshUpdate();
        Renderer counterRenderer = counterText.GetComponent<Renderer>();
        if (counterRenderer == null)
            return false;

        Bounds hitBounds = counterRenderer.bounds;
        hitBounds.Expand(Mathf.Max(0f, rotationHitPadding) * 2f);
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
        worldPosition.z = hitBounds.center.z;
        return hitBounds.Contains(worldPosition);
    }

    private void TryLockFiniteCounter(float angle)
    {
        float distanceToZero = Mathf.Abs(Mathf.DeltaAngle(angle, 0f));
        float distanceToHalfTurn = Mathf.Abs(Mathf.DeltaAngle(angle, 180f));
        if (Mathf.Min(distanceToZero, distanceToHalfTurn) > Mathf.Max(1f, uprightLockAngle))
            return;

        isFiniteCounter = true;
        isCounterDragging = false;
        counterRoot.localRotation = Quaternion.identity;
        remainingDoorCount = InitialDoorCount;
        UpdateCounterText();
    }

    private static float CalculatePointerAngle(Vector2 center, Vector2 pointer)
    {
        return Mathf.Atan2(pointer.y - center.y, pointer.x - center.x) * Mathf.Rad2Deg;
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.DeltaAngle(0f, angle);
    }

    #endregion

    #region Door Loop

    private async UniTaskVoid PlayLoopDoorFlowAsync(int flowVersion)
    {
        isDoorFlowPlaying = true;
        isCounterDragging = false;
        levelCtrl.IsActorEnterDoor = false;
        levelCtrl.SetCtrlable(false);
        actor.SetMoveInput(0f);
        actor.StopMoving();
        SetPanelBlocked(true);

        if (exitDoorTrigger != null)
            exitDoorTrigger.enabled = false;

        try
        {
            PlayExitDoorOpen();
            if (!await WaitForFlowSeconds(ScaleFlowDuration(doorOpenDelay), flowVersion))
                return;

            if (!PrepareActorForExitDoor())
                return;

            float alpha = 1f;
            float duration = Mathf.Max(0.05f, ScaleFlowDuration(actorEnterDuration));
            actorEnterSequence = DOTween.Sequence().SetLink(gameObject);
            actorEnterSequence.Append(
                actor.transform.DOMove(exitDoorTarget.position + actorEnterOffset, duration)
                    .SetEase(Ease.Linear));
            actorEnterSequence.Join(
                DOTween.To(() => alpha, value =>
                {
                    alpha = value;
                    SetActorAlpha(value);
                }, 0f, duration).SetEase(Ease.InQuad));

            await actorEnterSequence.AsyncWaitForCompletion();
            actorEnterSequence = null;
            if (!IsCurrentFlow(flowVersion))
                return;

            actor.SetMoveInput(0f);
            actor.StopMoving();
            RestoreActorFlowAnimationSpeed();
            SetActorAlpha(0f);

            if (!await WaitForFlowSeconds(ScaleFlowDuration(doorCloseDelay), flowVersion))
                return;

            PlayDoorAnimation(exitDoorSpine, DoorCloseAnimation);
            PlayCloseDoorSoundAfterDelayAsync(
                ScaleFlowDuration(closeDoorSfxDelay),
                flowVersion).Forget();

            if (!await WaitForFlowSeconds(exitCloseToEntranceOpenDelay, flowVersion))
                return;

            if (!await PlayStartDoorReturnAsync(flowVersion))
                return;

            RestoreActorVisuals();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (IsCurrentFlow(flowVersion))
            {
                isDoorFlowPlaying = false;
                levelCtrl.IsActorEnterDoor = false;
                actor.SetMoveInput(0f);
                levelCtrl.SetCtrlable(!isPaused);
                if (isPaused)
                    actor.StopMoving();
                else
                    actor.ResumeMoving();

                if (exitDoorTrigger != null)
                    exitDoorTrigger.enabled = true;

                ClearDoorTrackEntries(false);
                SetPanelBlocked(false);
            }
        }
    }

    private bool PrepareActorForExitDoor()
    {
        if (actor == null || exitDoorTarget == null)
            return false;

        actor.SetFaceDir(true);
        actor.ResumeMoving();
        actor.SetMoveInput(1f);
        actor.UpdateAnimation();
        ApplyActorFlowAnimationSpeed();
        actorRendererGroup?.CollectRenderers();

        if (actorBody != null)
        {
            actorBody.linearVelocity = Vector2.zero;
            actorBody.angularVelocity = 0f;
            actorBody.simulated = false;
        }

        AudioManager.PlaySound("enterDoor");
        return true;
    }

    private void PlayExitDoorOpen()
    {
        if (exitDoorSpine == null)
            return;

        if (!exitDoorSpine.IsPlayingAnimation(DoorOpenAnimation))
        {
            AudioManager.PlaySound("openDoor");
            PlayDoorAnimation(exitDoorSpine, DoorOpenAnimation);
        }
    }

    private async UniTask<bool> PlayStartDoorReturnAsync(int flowVersion)
    {
        if (actor == null || startDoorRoot == null || startDoorSpine == null)
        {
            Debug.LogWarning("[Level124Ctrl] Start door references are incomplete.", this);
            RestoreActorForStartDoor(true);
            RestoreActorVisuals();
            return false;
        }

        RestoreActorForStartDoor();
        actor.transform.position = startDoorRoot.position;
        PrepareActorHiddenForStartDoor();

        AudioManager.PlaySound("openDoor");
        PlayDoorAnimation(startDoorSpine, DoorOpenAnimation);
        if (!await WaitForFlowSeconds(ScaleFlowDuration(StartDoorOpenDelay), flowVersion))
            return false;

        actor.SetFaceDir(true);
        actor.ResumeMoving();
        actor.SetMoveInput(1f);
        actor.UpdateAnimation();
        isActorLeavingStartDoor = true;

        // 扣减反馈与角色真正从初始门开始走出的时刻对齐。
        if (isFiniteCounter)
        {
            remainingDoorCount--;
            UpdateCounterText();
        }

        PlayMinusOneFeedback();

        float alpha = 0f;
        // 走出入口门沿用共享出生流程的 0.2 秒，避免 1.2 倍门/吸入节奏缩短角色落点。
        float duration = Mathf.Max(0.05f, StartDoorActorExitDuration);
        actorStartDoorExitSequence = DOTween.Sequence().SetLink(gameObject);
        actorStartDoorExitSequence.Append(
            DOTween.To(() => alpha, value =>
            {
                alpha = value;
                SetActorAlpha(value);
            }, 1f, duration).SetEase(Ease.OutQuad));

        await actorStartDoorExitSequence.AsyncWaitForCompletion();
        actorStartDoorExitSequence = null;
        if (!IsCurrentFlow(flowVersion))
            return false;

        isActorLeavingStartDoor = false;
        RestoreActorFlowAnimationSpeed();
        actor.SetMoveInput(0f);
        actor.UpdateAnimation();
        actor.StopMoving();
        RestoreActorVisuals();

        if (!await WaitForFlowSeconds(ScaleFlowDuration(StartDoorCloseDelay), flowVersion))
            return false;

        PlayDoorAnimation(startDoorSpine, DoorCloseAnimation);
        if (!await WaitForFlowSeconds(ScaleFlowDuration(StartDoorCloseSfxDelay), flowVersion))
            return false;

        AudioManager.PlaySound("closeDoor");
        return IsCurrentFlow(flowVersion);
    }

    private async UniTaskVoid PlayCloseDoorSoundAfterDelayAsync(float delay, int flowVersion)
    {
        if (await WaitForFlowSeconds(delay, flowVersion))
            AudioManager.PlaySound("closeDoor");
    }

    private void PlayDoorAnimation(ActorSpineCtrl spine, string animationName)
    {
        if (spine == null)
            return;

        Spine.TrackEntry entry = spine.PlayAnimation(animationName, false, false);
        if (entry == null)
            return;

        entry.TimeScale = isPaused ? 0f : Mathf.Max(0.1f, flowSpeedMultiplier);
        if (spine == exitDoorSpine)
            exitDoorTrackEntry = entry;
        else if (spine == startDoorSpine)
            startDoorTrackEntry = entry;
    }

    private float ScaleFlowDuration(float duration)
    {
        return Mathf.Max(0f, duration) / Mathf.Max(0.1f, flowSpeedMultiplier);
    }

    private void ApplyActorFlowAnimationSpeed()
    {
        RestoreActorFlowAnimationSpeed();
        actorFlowTrackEntry = actor?.SpineCtrl?.AnimationState?.GetCurrent(0);
        if (actorFlowTrackEntry == null)
            return;

        actorFlowOriginalTimeScale = actorFlowTrackEntry.TimeScale;
        actorFlowTrackEntry.TimeScale = isPaused ? 0f : Mathf.Max(0.1f, flowSpeedMultiplier);
    }

    private void RestoreActorFlowAnimationSpeed()
    {
        if (actorFlowTrackEntry != null)
            actorFlowTrackEntry.TimeScale = actorFlowOriginalTimeScale;

        actorFlowTrackEntry = null;
        actorFlowOriginalTimeScale = 1f;
    }

    private void SetFlowAnimationPaused(bool paused)
    {
        float timeScale = paused ? 0f : Mathf.Max(0.1f, flowSpeedMultiplier);
        if (exitDoorTrackEntry != null)
            exitDoorTrackEntry.TimeScale = timeScale;

        if (startDoorTrackEntry != null)
            startDoorTrackEntry.TimeScale = timeScale;

        if (actorFlowTrackEntry != null)
            actorFlowTrackEntry.TimeScale = paused ? 0f : Mathf.Max(0.1f, flowSpeedMultiplier);
    }

    private void ClearDoorTrackEntries(bool restoreDefaultSpeed)
    {
        if (restoreDefaultSpeed)
        {
            if (exitDoorTrackEntry != null)
                exitDoorTrackEntry.TimeScale = 1f;

            if (startDoorTrackEntry != null)
                startDoorTrackEntry.TimeScale = 1f;
        }

        exitDoorTrackEntry = null;
        startDoorTrackEntry = null;
    }

    private async UniTask<bool> WaitForFlowSeconds(float seconds, int flowVersion)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, seconds);
        while (elapsed < duration)
        {
            await UniTask.Yield(cancellationToken: this.destroyCancellationToken);
            if (!IsCurrentFlow(flowVersion))
                return false;

            if (!isPaused)
                elapsed += Time.deltaTime;
        }

        return IsCurrentFlow(flowVersion);
    }

    private bool IsCurrentFlow(int flowVersion)
    {
        return flowVersion == doorFlowVersion && isDoorFlowPlaying && !isFinalSuccessAccepted;
    }

    #endregion

    #region Minus One Feedback

    private void CreateMinusOneFeedback()
    {
        if (counterText == null || counterRoot == null || minusOneText != null)
            return;

        GameObject popupObject = new("Level124_MinusOne", typeof(RectTransform));
        popupObject.layer = counterText.gameObject.layer;
        popupObject.SetActive(false);

        minusOneRect = popupObject.GetComponent<RectTransform>();
        minusOneRect.SetParent(counterRoot.parent, false);
        minusOneRect.anchorMin = counterRoot.anchorMin;
        minusOneRect.anchorMax = counterRoot.anchorMax;
        minusOneRect.pivot = counterRoot.pivot;
        minusOneRect.sizeDelta = counterRoot.sizeDelta;

        minusOneText = popupObject.AddComponent<TextMeshPro>();
        minusOneText.font = counterText.font;
        minusOneText.fontSharedMaterial = counterText.fontSharedMaterial;
        minusOneText.fontSize = counterText.fontSize;
        minusOneText.fontStyle = counterText.fontStyle;
        minusOneText.alignment = counterText.alignment;
        minusOneText.characterSpacing = counterText.characterSpacing;
        minusOneText.wordSpacing = counterText.wordSpacing;
        minusOneText.margin = counterText.margin;
        minusOneText.enableAutoSizing = false;
        minusOneText.overflowMode = TextOverflowModes.Overflow;
        minusOneText.raycastTarget = false;
        minusOneText.text = "-1";
        minusOneText.color = counterText.color;

        SortingGroup sourceSortingGroup = counterRoot.GetComponent<SortingGroup>();
        Renderer sourceRenderer = counterText.GetComponent<Renderer>();
        SortingGroup popupSortingGroup = popupObject.AddComponent<SortingGroup>();
        popupSortingGroup.sortingLayerID = sourceSortingGroup != null
            ? sourceSortingGroup.sortingLayerID
            : sourceRenderer != null ? sourceRenderer.sortingLayerID : 0;
        popupSortingGroup.sortingOrder = sourceSortingGroup != null
            ? sourceSortingGroup.sortingOrder + 1
            : sourceRenderer != null ? sourceRenderer.sortingOrder + 1 : 1;
    }

    private void PlayMinusOneFeedback()
    {
        if (minusOneText == null || minusOneRect == null)
            CreateMinusOneFeedback();

        if (minusOneText == null || minusOneRect == null || counterRoot == null)
            return;

        minusOneSequence?.Kill(false);
        minusOneText.gameObject.SetActive(true);
        minusOneText.text = "-1";
        Color visibleColor = counterText != null ? counterText.color : Color.white;
        visibleColor.a = 1f;
        minusOneText.color = visibleColor;

        Vector3 startPosition = counterRoot.localPosition +
                                new Vector3(minusOneStartOffset.x, minusOneStartOffset.y, -0.01f);
        minusOneRect.localPosition = startPosition;
        minusOneRect.localRotation = Quaternion.identity;
        minusOneRect.localScale = counterRoot.localScale * Mathf.Max(0.1f, minusOneScale);

        float duration = Mathf.Max(0.1f, minusOneDuration);
        float fadeStart = duration * 0.15f;
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        minusOneSequence = sequence;
        sequence.Insert(
            0f,
            minusOneRect.DOLocalMoveY(startPosition.y + Mathf.Max(0f, minusOneRiseDistance), duration)
                .SetEase(Ease.OutQuad));
        sequence.Insert(
            fadeStart,
            minusOneText.DOFade(0f, Mathf.Max(0.01f, duration - fadeStart)).SetEase(Ease.InQuad));
        sequence.OnComplete(() =>
        {
            if (minusOneSequence != sequence)
                return;

            minusOneSequence = null;
            if (minusOneText != null)
                minusOneText.gameObject.SetActive(false);
        });

        if (isPaused)
            sequence.Pause();
    }

    #endregion

    #region State And References

    private void ResolveReferences()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponent<BaseLevelCtrl>() ?? GetComponentInParent<BaseLevelCtrl>();

        if (actor == null && levelCtrl != null)
            actor = levelCtrl.Actor;

        if (exitDoorRoot != null)
            exitDoorTarget = exitDoorRoot.Find("door") ?? exitDoorRoot;

        if (exitDoorSpine == null && exitDoorRoot != null)
            exitDoorSpine = exitDoorRoot.GetComponentInChildren<ActorSpineCtrl>(true);

        if (exitDoorTrigger == null && exitDoorRoot != null)
            exitDoorTrigger = exitDoorRoot.GetComponent<Collider2D>();

        if (startDoorRoot == null && levelCtrl != null)
        {
            EnterDoorAnimCtrl startDoorAnim = levelCtrl.GetComponentInChildren<EnterDoorAnimCtrl>(true);
            if (startDoorAnim != null)
                startDoorRoot = startDoorAnim.transform;
        }

        if (startDoorSpine == null && startDoorRoot != null)
            startDoorSpine = startDoorRoot.GetComponent<ActorSpineCtrl>() ??
                             startDoorRoot.GetComponentInChildren<ActorSpineCtrl>(true);

        if (counterRoot != null)
            counterText = counterRoot.GetComponentInChildren<TMP_Text>(true);

        if (actor != null)
        {
            actorBody = actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>();
            actorRendererGroup ??= actor.GetComponentInChildren<SpriteRendererGroupAlpha>(true);
            actorShadow ??= actor.transform.Find("Shadow")?.GetComponent<SpriteRenderer>();
        }

        mainCamera = Camera.main;

        if (levelCtrl == null || actor == null || exitDoorRoot == null || exitDoorSpine == null ||
            exitDoorTrigger == null || startDoorRoot == null || startDoorSpine == null ||
            counterRoot == null || counterText == null)
        {
            Debug.LogWarning("[Level124Ctrl] Required Level124 references are incomplete.", this);
        }
    }

    private void CacheInitialState()
    {
        if (counterRoot != null)
            initialCounterAngle = NormalizeAngle(counterRoot.localEulerAngles.z);

        if (actor == null)
            return;

        actorInitialLocalPosition = actor.transform.localPosition;
        actorInitialLocalScale = actor.transform.localScale;
        actorInitialLocalRotation = actor.transform.localRotation;
        actorInitialSimulated = actorBody == null || actorBody.simulated;
        hasCachedActorState = true;
    }

    private void InitializeCounter()
    {
        DisableCounterLocalization();
        remainingDoorCount = InitialDoorCount;
        isFiniteCounter = false;
        isFinalSuccessAccepted = false;
        isCounterDragging = false;

        if (counterRoot != null)
            counterRoot.localRotation = Quaternion.Euler(0f, 0f, initialCounterAngle);

        UpdateCounterText();
    }

    private void DisableCounterLocalization()
    {
        if (counterRoot == null)
            return;

        MonoBehaviour[] components = counterRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < components.Length; i++)
        {
            MonoBehaviour component = components[i];
            if (component != null && component.GetType().FullName == "I2.Loc.Localize")
                component.enabled = false;
        }
    }

    private void UpdateCounterText()
    {
        if (counterText != null)
            counterText.text = Mathf.Max(1, remainingDoorCount).ToString();
    }

    private bool IsValidActorCollider(Collider2D collider)
    {
        if (collider == null || levelCtrl == null || actor == null || actor.IsDied)
            return false;

        return collider.GetComponentInParent<ActorCtrl>() == actor ||
               LayerMask.LayerToName(collider.gameObject.layer) == "Actor";
    }

    private void OnGameReset(GameResetEvent evt)
    {
        BeginTeardown();
    }

    private void OnGameReplay(GameReplayEvent evt)
    {
        BeginTeardown();
    }

    private void BeginTeardown()
    {
        if (teardownStarted)
            return;

        teardownStarted = true;
        isPaused = true;
        isFinalSuccessAccepted = true;
        isCounterDragging = false;
        doorFlowVersion++;
        isDoorFlowPlaying = false;
        isActorLeavingStartDoor = false;
        ClearDoorTrackEntries(false);
        KillTweens();
        UnbindRuntimeCallbacks();

        if (exitDoorTrigger != null)
            exitDoorTrigger.enabled = false;

        levelCtrl?.SetCtrlable(false);
        actor?.SetMoveInput(0f);
        actor?.StopMoving();
        SetPanelBlocked(false);
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        isPaused = true;
        isCounterDragging = false;
        actorEnterSequence?.Pause();
        actorStartDoorExitSequence?.Pause();
        minusOneSequence?.Pause();
        SetFlowAnimationPaused(true);

        if (isActorLeavingStartDoor && actor != null)
        {
            actor.SetMoveInput(0f);
            actor.StopMoving();
        }
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        isPaused = false;
        actorEnterSequence?.Play();
        actorStartDoorExitSequence?.Play();
        minusOneSequence?.Play();
        SetFlowAnimationPaused(false);

        if (isDoorFlowPlaying && isActorLeavingStartDoor && actor != null && !actor.IsDied)
        {
            actor.ResumeMoving();
            actor.SetMoveInput(1f);
            actor.UpdateAnimation();
        }

        if (!isDoorFlowPlaying && !isFinalSuccessAccepted && levelCtrl != null && actor != null && !actor.IsDied)
        {
            levelCtrl.IsActorEnterDoor = false;
            levelCtrl.SetCtrlable(true);
            actor.SetMoveInput(0f);
            actor.ResumeMoving();
        }
    }

    private void CancelDoorFlow(bool restoreActor)
    {
        doorFlowVersion++;
        isDoorFlowPlaying = false;
        isActorLeavingStartDoor = false;
        RestoreActorFlowAnimationSpeed();
        ClearDoorTrackEntries(true);
        KillTweens();

        if (restoreActor)
        {
            RestoreActorForStartDoor(true);
            RestoreActorVisuals();

            if (!isPaused && !isFinalSuccessAccepted && levelCtrl != null && actor != null && !actor.IsDied)
            {
                levelCtrl.IsActorEnterDoor = false;
                levelCtrl.SetCtrlable(true);
                actor.SetMoveInput(0f);
                actor.ResumeMoving();
            }
        }

        if (exitDoorTrigger != null && !isFinalSuccessAccepted)
            exitDoorTrigger.enabled = true;

        SetPanelBlocked(false);
    }

    private void KillTweens()
    {
        actorEnterSequence?.Kill(false);
        actorEnterSequence = null;
        actorStartDoorExitSequence?.Kill(false);
        actorStartDoorExitSequence = null;
        minusOneSequence?.Kill(false);
        minusOneSequence = null;

        if (minusOneText != null)
            minusOneText.gameObject.SetActive(false);
    }

    private void RestoreActorForStartDoor(bool restoreInitialPosition = false)
    {
        if (actor == null || !hasCachedActorState)
            return;

        actor.SetMoveInput(0f);
        actor.StopMoving();
        if (restoreInitialPosition)
            actor.transform.localPosition = actorInitialLocalPosition;

        actor.transform.localScale = actorInitialLocalScale;
        actor.transform.localRotation = actorInitialLocalRotation;

        if (actorBody != null)
        {
            actorBody.linearVelocity = Vector2.zero;
            actorBody.angularVelocity = 0f;
            actorBody.simulated = actorInitialSimulated;
        }
    }

    private void PrepareActorHiddenForStartDoor()
    {
        SetActorAlpha(0f);
        actorRendererGroup?.CollectRenderers();
        actorRendererGroup?.SetAlpha(0f);
    }

    private void RestoreActorVisuals()
    {
        SetActorAlpha(1f);
        actorRendererGroup?.CollectRenderers();
        actorRendererGroup?.SetAlpha(1f);
    }

    private void SetActorAlpha(float alpha)
    {
        float safeAlpha = Mathf.Clamp01(alpha);
        currentActorAlpha = safeAlpha;
        actor?.SetSpineAlpha(safeAlpha);
        actorRendererGroup?.SetAlpha(safeAlpha);

        if (actorShadow != null)
            actorShadow.gameObject.SetActive(safeAlpha > 0.001f);
    }

    private void BindActorSkinCallbacks()
    {
        UnbindActorSkinCallbacks();
        if (actor == null)
            return;

        actorSkinBinding = actor.GetComponentInChildren<ActorSkinBinding>(true);
        if (actorSkinBinding != null)
            actorSkinBinding.CurrentSelectionApplied += OnActorSkinApplied;
    }

    private void UnbindActorSkinCallbacks()
    {
        if (actorSkinBinding != null)
            actorSkinBinding.CurrentSelectionApplied -= OnActorSkinApplied;

        actorSkinBinding = null;
    }

    private void OnActorSkinApplied(ActorSkinBinding binding)
    {
        if (binding != actorSkinBinding || actorRendererGroup == null)
            return;

        actorRendererGroup.CollectRenderers();
        actorRendererGroup.SetAlpha(currentActorAlpha);
    }

    private void SetPanelBlocked(bool blocked)
    {
        if (isPanelBlocked == blocked)
            return;

        isPanelBlocked = blocked;
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, blocked);
    }

    #endregion
}

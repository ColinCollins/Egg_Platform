using System.Collections.Generic;
using Bear.EventSystem;
using DG.Tweening;
using Game.Common;
using Game.Events;
using Game.ItemEvent;
using Game.Level;
using Game.Play;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Level099 私有关卡逻辑：持续推动石柱，或反复顶问号块累计金币。
/// </summary>
public sealed class Level99Ctrl : MonoBehaviour
{
    private const float PushColliderContactTolerance = 0.02f;

    #region Serialized References And Parameters

    private enum PillarPhase
    {
        WaitingForPush,
        Falling,
        Fading,
        Hidden
    }

    /// <summary>本关角色；用于核对输入、受控状态和与石柱的真实接触。</summary>
    [Header("Level Refs")]
    [SerializeField] private ActorCtrl actor;
    /// <summary>需要被推动并在金币路线中渐隐的岩石柱根节点。</summary>
    [SerializeField] private Transform pillar;
    /// <summary>岩石柱刚体；等待推动时为 Kinematic，离开平台后切为 Dynamic。</summary>
    [SerializeField] private Rigidbody2D pillarBody;
    /// <summary>岩石柱主碰撞体；用于接触和离开平台边缘判定。</summary>
    [SerializeField] private Collider2D pillarCollider;
    /// <summary>岩石柱所在的平台；其碰撞体右边界作为坠落释放线。</summary>
    [SerializeField] private Transform pushGround;
    /// <summary>右侧剩余金币数量的 Tips 根节点。</summary>
    [SerializeField] private RectTransform numberRoot;
    /// <summary>剩余金币数量左侧的金币图标。</summary>
    [SerializeField] private SpriteRenderer coinCounterIcon;
    /// <summary>问号块的既有射线事件执行器。</summary>
    [SerializeField] private Raycast2DExecutor questionBlockExecutor;
    /// <summary>本关私有的问号块顶击动画，不依赖 MMF。</summary>
    [SerializeField] private Level99QuestionBlockFeedback questionBlockFeedback;

    /// <summary>连续推动期间石柱完全锁定不动的时长。</summary>
    [Header("Continuous Push")]
    [Tooltip("连续推动达到该时长前，石柱保持完全不动。")]
    [SerializeField, Min(0f)] private float lockedPushDuration = 5f;
    /// <summary>石柱从首次可见移动加速到最大推动速度的时长。</summary>
    [Tooltip("石柱从首次可见移动加速到最大推动速度所需的时间。")]
    [SerializeField, Min(0.01f)] private float pushAccelerationDuration = 4f;
    /// <summary>解除五秒锁定后石柱的初始推动速度。</summary>
    [SerializeField, Min(0f)] private float minimumPushSpeed = 0.03f;
    /// <summary>持续推动达到最大加速进度后的速度。</summary>
    [SerializeField, Min(0f)] private float maximumPushSpeed = 0.45f;
    /// <summary>容忍物理接触短暂抖动的连续推动宽限时间。</summary>
    [SerializeField, Min(0f)] private float pushContactGraceDuration = 0.1f;
    /// <summary>石柱左边缘完全越过平台右边界后额外保留的释放间距。</summary>
    [SerializeField, Min(0f)] private float edgeReleaseOffset = 0.02f;

    /// <summary>石柱离开平台并切为 Dynamic 后使用的质量。</summary>
    [Header("Pillar Fall")]
    [SerializeField, Min(0.01f)] private float fallingMass = 8f;
    /// <summary>石柱离开平台后的重力倍率。</summary>
    [SerializeField, Min(0f)] private float fallingGravityScale = 2f;

    /// <summary>问号块路线需要累计的金币数量，策划默认值为 9999。</summary>
    [Header("Question Block Route")]
    [Tooltip("让金币计数和石柱消失所需的问号块金币数量，默认 9999。")]
    [SerializeField, Min(1)] private int requiredCoinCount = 9999;
    /// <summary>金币图标、数字和石柱同步渐隐的时长。</summary>
    [SerializeField, Min(0.01f)] private float completionFadeDuration = 1.5f;

    /// <summary>允许同时显示的减一飞字数量。</summary>
    [Header("Counter Feedback")]
    [SerializeField, Range(1, 8)] private int minusOnePoolSize = 4;
    /// <summary>减一飞字相对 Number 的起始偏移。</summary>
    [SerializeField] private Vector2 minusOneStartOffset = new(0f, 0.18f);
    /// <summary>减一飞字向上移动的距离。</summary>
    [SerializeField, Min(0f)] private float minusOneRiseDistance = 0.72f;
    /// <summary>减一飞字完整播放时长。</summary>
    [SerializeField, Min(0.1f)] private float minusOneDuration = 0.55f;
    /// <summary>减一飞字相对主数字的显示倍率。</summary>
    [SerializeField, Range(0.1f, 2f)] private float minusOneScale = 0.82f;
    #endregion

    #region Runtime State

    private BaseLevelCtrl baseLevelCtrl;
    private Collider2D actorCollider;
    private Collider2D actorPushCollider;
    private Collider2D pushGroundCollider;
    private TMP_Text numberText;
    private SpriteRenderer[] pillarRenderers;
    private EventSubscriber subscriber;
    private Sequence completionFade;
    private readonly List<MinusOneSlot> minusOneSlots = new();

    private PillarPhase pillarPhase;
    private float continuousPushTime;
    private float contactGraceRemaining;
    private int earnedCoinCount;
    private bool runtimeReady;
    private bool isPaused;
    private bool isResetting;
    private bool resumePending;
    private bool coinRouteComplete;
    private float pillarUprightRotation;
    private Vector2 pausedPillarVelocity;
    private float pausedPillarAngularVelocity;
    private int nextMinusOneSlot;

    private sealed class MinusOneSlot
    {
        public TextMeshPro Text;
        public RectTransform RectTransform;
        public Sequence Sequence;
        public Color VisibleColor;
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        InitializeRuntime();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        BindEvents();
        AlignRuntimeWithCurrentState();
    }

    private void FixedUpdate()
    {
        TryResumeWhenPlaying();

        if (!runtimeReady || isPaused || isResetting || coinRouteComplete ||
            pillarPhase != PillarPhase.WaitingForPush || !IsPlayingState())
        {
            return;
        }

        UpdateContinuousPush();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref subscriber);
        resumePending = false;
        isPaused = true;
        PauseRuntimeObjects();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref subscriber);
        completionFade?.Kill();
        completionFade = null;
        StopMinusOnePopups();
    }

    #endregion

    #region Initialization

    private void ResolveReferences()
    {
        baseLevelCtrl ??= GetComponent<BaseLevelCtrl>();
        actor ??= baseLevelCtrl != null ? baseLevelCtrl.Actor : null;
        actor ??= GetComponentInChildren<ActorCtrl>(true);
        pillar ??= transform.Find("stone");
        pushGround ??= transform.Find("Ground_long");
        numberRoot ??= transform.Find("Number") as RectTransform;
        coinCounterIcon ??= transform.Find("Sp_goldcoin_01-1")?.GetComponent<SpriteRenderer>();
        questionBlockExecutor ??= transform.Find("Box_1")?.GetComponent<Raycast2DExecutor>();
        questionBlockFeedback ??= questionBlockExecutor != null
            ? questionBlockExecutor.GetComponent<Level99QuestionBlockFeedback>()
            : null;

        actorCollider ??= actor != null ? actor.GetComponent<Collider2D>() : null;
        actorPushCollider ??= actor != null
            ? actor.transform.Find("pushCollider")?.GetComponent<Collider2D>()
            : null;
        pillarCollider ??= pillar != null ? pillar.GetComponent<Collider2D>() : null;
        pillarBody ??= pillar != null ? pillar.GetComponent<Rigidbody2D>() : null;
        pushGroundCollider ??= pushGround != null ? pushGround.GetComponent<Collider2D>() : null;
        numberText ??= numberRoot != null ? numberRoot.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private void InitializeRuntime()
    {
        SetQuestionBlockEnabled(false);
        DisableNumberLocalization();
        UpdateRemainingCoinText();

        if (actor == null || actorCollider == null || actorPushCollider == null ||
            pillar == null || pillarCollider == null ||
            pushGroundCollider == null || numberText == null || coinCounterIcon == null ||
            questionBlockExecutor == null || questionBlockFeedback == null)
        {
            Debug.LogWarning("[Level99Ctrl] Required Level099 references are incomplete; private gameplay is disabled.", this);
            return;
        }

        if (pillarBody == null)
            pillarBody = pillar.gameObject.AddComponent<Rigidbody2D>();

        int stuffLayer = LayerMask.NameToLayer("Stuff");
        if (stuffLayer >= 0)
            pillar.gameObject.layer = stuffLayer;

        pillarUprightRotation = pillarBody.rotation;
        pillarBody.simulated = false;
        pillarBody.bodyType = RigidbodyType2D.Kinematic;
        pillarBody.gravityScale = 0f;
        pillarBody.mass = Mathf.Max(0.01f, fallingMass);
        pillarBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        pillarBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        pillarBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        pillarBody.linearVelocity = Vector2.zero;
        pillarBody.angularVelocity = 0f;
        pillarBody.simulated = true;

        pillarRenderers = pillar.GetComponentsInChildren<SpriteRenderer>(true);
        questionBlockFeedback.ResetVisual();
        CreateMinusOnePool();
        pillarPhase = PillarPhase.WaitingForPush;
        runtimeReady = true;
    }

    private void DisableNumberLocalization()
    {
        if (numberRoot == null)
            return;

        MonoBehaviour[] components = numberRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < components.Length; i++)
        {
            MonoBehaviour component = components[i];
            if (component != null && component.GetType().FullName == "I2.Loc.Localize")
                component.enabled = false;
        }
    }

    private void AlignRuntimeWithCurrentState()
    {
        if (!runtimeReady || isResetting)
            return;

        if (IsPlayingState())
        {
            ResumeRuntimeObjects();
            return;
        }

        resumePending = false;
        isPaused = true;
        PauseRuntimeObjects();
    }

    #endregion

    #region Game State Events

    private void BindEvents()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        subscriber.Subscribe<GameResumeEvent>(OnGameResume);
        subscriber.Subscribe<GameResetEvent>(OnGameReset);
        subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        resumePending = false;
        isPaused = true;
        PauseRuntimeObjects();
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        if (isResetting)
            return;

        resumePending = true;
        TryResumeWhenPlaying();
    }

    private void OnGameReset(GameResetEvent evt)
    {
        isResetting = true;
        resumePending = false;
        isPaused = true;
        SetQuestionBlockEnabled(false);
        questionBlockFeedback?.ResetVisual();
        StopMinusOnePopups();
        completionFade?.Kill();
        completionFade = null;

        if (pillarBody != null)
        {
            pillarBody.linearVelocity = Vector2.zero;
            pillarBody.angularVelocity = 0f;
            pillarBody.simulated = false;
        }
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt == null || isResetting)
            return;

        if (evt.NewState == GamePlayStateName.PLAYING)
        {
            resumePending = true;
            TryResumeWhenPlaying();
            return;
        }

        resumePending = false;
        isPaused = true;
        PauseRuntimeObjects();
    }

    private void PauseRuntimeObjects()
    {
        SetQuestionBlockEnabled(false);
        questionBlockFeedback?.PauseAnimation();
        PauseMinusOnePopups();
        if (completionFade != null && completionFade.IsActive())
            completionFade.Pause();

        if (pillarBody == null || !pillarBody.simulated)
            return;

        pausedPillarVelocity = pillarBody.linearVelocity;
        pausedPillarAngularVelocity = pillarBody.angularVelocity;
        pillarBody.linearVelocity = Vector2.zero;
        pillarBody.angularVelocity = 0f;
        pillarBody.simulated = false;
    }

    private void TryResumeWhenPlaying()
    {
        if (!resumePending || isResetting || !runtimeReady || !IsPlayingState())
            return;

        ResumeRuntimeObjects();
    }

    private void ResumeRuntimeObjects()
    {
        resumePending = false;
        isPaused = false;

        if (completionFade != null && completionFade.IsActive())
            completionFade.Play();

        if (!coinRouteComplete && pillarBody != null && pillarPhase != PillarPhase.Hidden)
        {
            pillarBody.simulated = true;
            if (pillarBody.bodyType == RigidbodyType2D.Dynamic)
            {
                pillarBody.linearVelocity = pausedPillarVelocity;
                pillarBody.angularVelocity = pausedPillarAngularVelocity;
            }
        }

        questionBlockFeedback?.ResumeAnimation();
        ResumeMinusOnePopups();
        SetQuestionBlockEnabled(!coinRouteComplete);
    }

    #endregion

    #region Pillar Route

    private void UpdateContinuousPush()
    {
        bool isValidPush = IsActorContinuouslyPushing();
        if (!isValidPush)
        {
            if (contactGraceRemaining > 0f)
            {
                contactGraceRemaining = Mathf.Max(0f, contactGraceRemaining - Time.fixedDeltaTime);
                return;
            }

            continuousPushTime = 0f;
            return;
        }

        contactGraceRemaining = Mathf.Max(0f, pushContactGraceDuration);
        continuousPushTime += Time.fixedDeltaTime;
        if (continuousPushTime <= Mathf.Max(0f, lockedPushDuration))
            return;

        float accelerationDuration = Mathf.Max(0.01f, pushAccelerationDuration);
        float unlockedTime = continuousPushTime - Mathf.Max(0f, lockedPushDuration);
        float progress = Mathf.Clamp01(unlockedTime / accelerationDuration);
        float easedProgress = progress * progress * (3f - 2f * progress);
        float speed = Mathf.Lerp(
            Mathf.Max(0f, minimumPushSpeed),
            Mathf.Max(minimumPushSpeed, maximumPushSpeed),
            easedProgress);

        Vector2 nextPosition = pillarBody.position + Vector2.right * (speed * Time.fixedDeltaTime);
        float nextColliderMinX = pillarCollider.bounds.min.x + (nextPosition.x - pillarBody.position.x);
        float releaseX = pushGroundCollider.bounds.max.x + Mathf.Max(0f, edgeReleaseOffset);
        if (nextColliderMinX >= releaseX)
        {
            BeginPillarFall(nextPosition);
            return;
        }

        pillarBody.MovePosition(nextPosition);
    }

    private bool IsActorContinuouslyPushing()
    {
        if (actor == null || actorCollider == null || pillarCollider == null ||
            actor.IsDied || actor.IsKnocked || actor.XInput < 0.5f ||
            (baseLevelCtrl != null && !baseLevelCtrl.ActorCtrlable))
        {
            return false;
        }

        Bounds actorBounds = actorCollider.bounds;
        Bounds pillarBounds = pillarCollider.bounds;
        if (actorBounds.center.x >= pillarBounds.center.x ||
            actorBounds.max.y < pillarBounds.min.y || actorBounds.min.y > pillarBounds.max.y)
        {
            return false;
        }

        if (!actor.IsPushStuff || !actorPushCollider.isActiveAndEnabled)
            return false;

        ColliderDistance2D pushDistance = actorPushCollider.Distance(pillarCollider);
        return pushDistance.isValid &&
               (pushDistance.isOverlapped || pushDistance.distance <= PushColliderContactTolerance);
    }

    private void BeginPillarFall(Vector2 releasePosition)
    {
        pillarPhase = PillarPhase.Falling;
        pillarBody.position = releasePosition;
        pillarBody.rotation = pillarUprightRotation;
        pillarBody.bodyType = RigidbodyType2D.Dynamic;
        pillarBody.mass = Mathf.Max(0.01f, fallingMass);
        pillarBody.gravityScale = Mathf.Max(0f, fallingGravityScale);
        pillarBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        pillarBody.linearVelocity = Vector2.zero;
        pillarBody.angularVelocity = 0f;
    }

    #endregion

    #region Question Block Route

    /// <summary>
    /// 登记一次已完成金币反馈链的有效顶块。只供本关的
    /// <see cref="Level99QuestionBlockReward"/> 调用，不会写入玩家持久金币。
    /// </summary>
    internal bool TryRegisterQuestionBlockCoin()
    {
        if (!runtimeReady || isPaused || isResetting || coinRouteComplete || !IsPlayingState())
            return false;

        int targetCount = Mathf.Max(1, requiredCoinCount);
        earnedCoinCount = Mathf.Min(targetCount, earnedCoinCount + 1);
        PlayMinusOnePopup();
        UpdateRemainingCoinText();
        if (earnedCoinCount >= targetCount)
            BeginCoinRouteCompletion();

        return true;
    }

    private void UpdateRemainingCoinText()
    {
        if (numberText == null && numberRoot != null)
            numberText = numberRoot.GetComponentInChildren<TMP_Text>(true);

        if (numberText != null)
            numberText.text = Mathf.Max(0, Mathf.Max(1, requiredCoinCount) - earnedCoinCount).ToString();
    }

    private void CreateMinusOnePool()
    {
        if (numberText == null || numberRoot == null || minusOneSlots.Count > 0)
            return;

        SortingGroup sourceSortingGroup = numberRoot.GetComponent<SortingGroup>();
        Renderer sourceRenderer = numberText.GetComponent<Renderer>();
        int sortingLayerId = sourceSortingGroup != null
            ? sourceSortingGroup.sortingLayerID
            : sourceRenderer != null ? sourceRenderer.sortingLayerID : 0;
        int sortingOrder = sourceSortingGroup != null
            ? sourceSortingGroup.sortingOrder + 1
            : sourceRenderer != null ? sourceRenderer.sortingOrder + 1 : 1;

        int poolSize = Mathf.Clamp(minusOnePoolSize, 1, 8);
        for (int i = 0; i < poolSize; i++)
        {
            GameObject popupObject = new($"Level99_MinusOne_{i}", typeof(RectTransform));
            popupObject.SetActive(false);
            popupObject.layer = numberText.gameObject.layer;

            RectTransform popupRect = popupObject.GetComponent<RectTransform>();
            popupRect.SetParent(numberRoot.parent, false);
            popupRect.anchorMin = numberRoot.anchorMin;
            popupRect.anchorMax = numberRoot.anchorMax;
            popupRect.pivot = numberRoot.pivot;
            popupRect.sizeDelta = numberRoot.sizeDelta;
            popupRect.localRotation = numberRoot.localRotation;

            TextMeshPro popupText = popupObject.AddComponent<TextMeshPro>();
            popupText.font = numberText.font;
            popupText.fontSharedMaterial = numberText.fontSharedMaterial;
            popupText.fontSize = numberText.fontSize;
            popupText.fontStyle = numberText.fontStyle;
            popupText.alignment = numberText.alignment;
            popupText.characterSpacing = numberText.characterSpacing;
            popupText.wordSpacing = numberText.wordSpacing;
            popupText.margin = numberText.margin;
            popupText.overflowMode = TextOverflowModes.Overflow;
            popupText.raycastTarget = false;
            popupText.text = "-1";

            SortingGroup sortingGroup = popupObject.AddComponent<SortingGroup>();
            sortingGroup.sortingLayerID = sortingLayerId;
            sortingGroup.sortingOrder = sortingOrder;

            Color visibleColor = numberText.color;
            visibleColor.a = 1f;
            popupText.color = visibleColor;
            minusOneSlots.Add(new MinusOneSlot
            {
                Text = popupText,
                RectTransform = popupRect,
                VisibleColor = visibleColor
            });
        }
    }

    private void PlayMinusOnePopup()
    {
        if (minusOneSlots.Count == 0)
            CreateMinusOnePool();

        if (minusOneSlots.Count == 0 || numberRoot == null)
            return;

        MinusOneSlot slot = minusOneSlots[nextMinusOneSlot % minusOneSlots.Count];
        nextMinusOneSlot = (nextMinusOneSlot + 1) % minusOneSlots.Count;
        slot.Sequence?.Kill(false);
        slot.Sequence = null;

        RectTransform popupRect = slot.RectTransform;
        TextMeshPro popupText = slot.Text;
        popupText.gameObject.SetActive(true);
        popupText.text = "-1";
        popupText.color = slot.VisibleColor;

        Vector3 startPosition = numberRoot.localPosition +
                                new Vector3(minusOneStartOffset.x, minusOneStartOffset.y, -0.01f);
        Vector3 targetScale = numberRoot.localScale * Mathf.Max(0.1f, minusOneScale);
        popupRect.localPosition = startPosition;
        popupRect.localRotation = numberRoot.localRotation;
        popupRect.localScale = targetScale;

        float duration = Mathf.Max(0.1f, minusOneDuration);
        float fadeStart = duration * 0.15f;

        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        slot.Sequence = sequence;
        sequence.Insert(
            0f,
            popupRect.DOLocalMoveY(startPosition.y + Mathf.Max(0f, minusOneRiseDistance), duration)
                .SetEase(Ease.OutQuad));
        sequence.Insert(
            fadeStart,
            popupText.DOFade(0f, Mathf.Max(0.01f, duration - fadeStart)).SetEase(Ease.InQuad));
        sequence.OnComplete(() =>
        {
            if (slot.Sequence != sequence)
                return;

            slot.Sequence = null;
            popupText.gameObject.SetActive(false);
        });

        if (isPaused)
            sequence.Pause();
    }

    private void PauseMinusOnePopups()
    {
        for (int i = 0; i < minusOneSlots.Count; i++)
        {
            Sequence sequence = minusOneSlots[i].Sequence;
            if (sequence != null && sequence.IsActive())
                sequence.Pause();
        }
    }

    private void ResumeMinusOnePopups()
    {
        for (int i = 0; i < minusOneSlots.Count; i++)
        {
            Sequence sequence = minusOneSlots[i].Sequence;
            if (sequence != null && sequence.IsActive())
                sequence.Play();
        }
    }

    private void StopMinusOnePopups()
    {
        for (int i = 0; i < minusOneSlots.Count; i++)
        {
            MinusOneSlot slot = minusOneSlots[i];
            slot.Sequence?.Kill(false);
            slot.Sequence = null;
            if (slot.Text != null)
                slot.Text.gameObject.SetActive(false);
        }
    }

    private void BeginCoinRouteCompletion()
    {
        if (coinRouteComplete)
            return;

        coinRouteComplete = true;
        pillarPhase = PillarPhase.Fading;
        SetQuestionBlockEnabled(false);
        questionBlockFeedback?.ShowCompletedVisual();

        if (pillarBody != null)
        {
            pillarBody.linearVelocity = Vector2.zero;
            pillarBody.angularVelocity = 0f;
            pillarBody.simulated = false;
        }

        completionFade?.Kill();
        completionFade = DOTween.Sequence().SetLink(gameObject);
        float duration = Mathf.Max(0.01f, completionFadeDuration);
        bool hasTween = false;

        if (pillarRenderers != null)
        {
            for (int i = 0; i < pillarRenderers.Length; i++)
                AddFadeTween(pillarRenderers[i]?.DOFade(0f, duration).SetEase(Ease.InOutSine), ref hasTween);
        }

        AddFadeTween(coinCounterIcon?.DOFade(0f, duration).SetEase(Ease.InOutSine), ref hasTween);
        AddFadeTween(numberText?.DOFade(0f, duration).SetEase(Ease.InOutSine), ref hasTween);

        if (!hasTween)
        {
            CompleteCoinRouteFade();
            return;
        }

        completionFade.OnComplete(CompleteCoinRouteFade);
    }

    private void AddFadeTween(Tween tween, ref bool hasTween)
    {
        if (tween == null || completionFade == null)
            return;

        if (hasTween)
            completionFade.Join(tween);
        else
        {
            completionFade.Append(tween);
            hasTween = true;
        }
    }

    private void CompleteCoinRouteFade()
    {
        completionFade = null;
        pillarPhase = PillarPhase.Hidden;
        if (pillarCollider != null)
            pillarCollider.enabled = false;

        pillar?.gameObject.SetActive(false);
        coinCounterIcon?.gameObject.SetActive(false);
        numberRoot?.gameObject.SetActive(false);
    }

    private void SetQuestionBlockEnabled(bool isEnabled)
    {
        if (questionBlockExecutor != null)
            questionBlockExecutor.enabled = isEnabled;
    }

    private static bool IsPlayingState()
    {
        if (!Singleton<PlayCtrl>.IsInitialized)
            return true;

        PlayCtrl playCtrl = PlayCtrl.Instance;
        return !playCtrl.IsInitialized || playCtrl.CheckState(GamePlayStateName.PLAYING);
    }

    #endregion
}

using System.Collections.Generic;
using DG.Tweening;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// Level120 关卡控制器：驱动可重复踩踏弹簧、红色机关后的隐藏入口，
/// 并实现进入下层前后的分段相机跟随。所有弹簧长度和尖刺布局均由 Prefab 决定。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level120Ctrl : MonoBehaviour
{
    [Header("关卡引用")]
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private CameraFollowRange followRange;
    [SerializeField] private CameraFollowArea legacyCameraFollowArea;
    [SerializeField] private Transform hiddenEntranceBlockerRoot;
    [SerializeField] private Transform decoyDoorRoot;
    [SerializeField] private TrapEventHandlle trapHandler;

    [Header("弹簧表现")]
    [SerializeField, Range(0.05f, 0.3f)] private float compressionRatio = 0.12f;
    [SerializeField, Min(0.05f)] private float minimumCompressionDistance = 0.32f;
    [SerializeField, Min(0.05f)] private float maximumCompressionDistance = 0.78f;
    [SerializeField, Min(0.01f)] private float compressionDuration = 0.11f;
    [SerializeField, Min(0.01f)] private float returnDuration = 0.16f;
    [SerializeField, Min(0f)] private float shortestSpringBounceVelocity = 18.5f;
    [SerializeField, Min(0f)] private float longestSpringBounceVelocity = 34f;
    [SerializeField, Min(0f)] private float redSpringBounceVelocity = 26f;
    [SerializeField, Min(0f)] private float redSpringRepeatBounceVelocity = 16.5f;
    [SerializeField, Min(0f)] private float redSpringRepeatCompressionDistance = 1.65f;
    [SerializeField, Min(0.01f)] private float redSpringRepeatCompressionDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float redSpringRepeatReturnDuration = 0.24f;
    [SerializeField, Min(0f)] private float redSpringRevealDistance = 1.9f;
    [SerializeField, Min(0.01f)] private float redSpringRevealDuration = 0.22f;
    [SerializeField, Min(0f)] private float springActivationMaxGap = 0.18f;
    [SerializeField, Min(0f)] private float springActivationPenetration = 0.3f;
    [SerializeField, Min(0f)] private float springRearmClearance = 0.35f;

    [Header("隐藏入口遮挡")]
    [SerializeField] private int actorPassageSortingOrder = 12;

    [Header("分段相机")]
    [SerializeField] private float horizontalFollowCenterX = -4.62f;
    [SerializeField, Min(0.01f)] private float horizontalFollowWidth = 12f;
    [SerializeField] private Vector2 cameraOffset = Vector2.zero;
    [SerializeField, Min(0f)] private float followSmoothTime = 0.2f;
    [SerializeField, Min(0f)] private float returnSmoothTime = 0.5f;
    [SerializeField] private float downwardFollowUnlockY = -6.5f;
    [SerializeField, Min(0f)] private float lockedCameraTopPadding = 0.35f;

    private sealed class SpringState
    {
        public Transform Root;
        public Transform[] ButtonParts;
        public Vector3[] ButtonPartRestLocalPositions;
        public BoxCollider2D ButtonCollider;
        public GameObject RedWhiteBase;
        public BoxCollider2D RedBodyCollider;
        public BoxCollider2D Trigger;
        public Vector2 TriggerRestOffset;
        public SpriteRenderer Visual;
        public OnTrigger2DHandle TriggerHandle;
        public UnityAction<Collider2D> EnterListener;
        public UnityAction<Collider2D> ExitListener;
        public Vector3 VisualRestLocalPosition;
        public Vector3 VisualRestLocalScale;
        public float VisualRestBottomWorldY;
        public float VisualWorldHeight;
        public bool IsRed;
        public bool Armed;
        public bool ActorInside;
        public bool Busy;
        public Tween Tween;
    }

    private readonly List<SpringState> springs = new List<SpringState>();
    private readonly List<OnTrigger2DHandle> spikeHandles = new List<OnTrigger2DHandle>();
    private BoxCollider2D hiddenEntranceBlockerCollider;
    private BoxCollider2D upperGroundCollider;
    private Collider2D actorCollider;
    private Renderer[] actorRenderers;
    private SortingGroup actorSortingGroup;
    private int actorInitialSortingLayerId;
    private int actorInitialSortingOrder;
    private bool actorSortingCached;
    private bool actorPassageSortingActive;
    private Vector3 cameraInitialPosition;
    private Vector3 cameraVelocity;
    private float shortestVisualHeight;
    private float longestVisualHeight;
    private bool hiddenEntranceOpened;
    private bool downwardFollowUnlocked;
    private bool cameraInitialized;

    private void Awake()
    {
        ResolveReferences();
        CacheSpringsAndBindTriggers();
        BindSpikeTriggers();
        DisableDecoyDoorTrigger();
        InitializeCamera();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResetAttempt();
    }

    private void Update()
    {
        if (!Application.isPlaying || actor == null)
            return;

        RearmReleasedSprings();
        RetryOccupiedSprings();
        if (hiddenEntranceOpened
            && !downwardFollowUnlocked
            && actor.transform.position.y <= downwardFollowUnlockY)
        {
            downwardFollowUnlocked = true;
        }

        UpdateActorPassageSorting();
    }

    private void LateUpdate()
    {
        if (Application.isPlaying)
            UpdateCameraPosition();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        KillSpringTweens();
        RestoreActorSorting();
    }

    private void OnDestroy()
    {
        KillSpringTweens();

        for (int index = 0; index < springs.Count; index++)
        {
            SpringState spring = springs[index];
            if (spring.TriggerHandle == null)
                continue;

            spring.TriggerHandle.OnEnter.RemoveListener(spring.EnterListener);
            spring.TriggerHandle.OnExit.RemoveListener(spring.ExitListener);
        }

        for (int index = 0; index < spikeHandles.Count; index++)
        {
            if (spikeHandles[index] != null)
                spikeHandles[index].OnEnter.RemoveListener(OnSpikeEnter);
        }
    }

    private void ResolveReferences()
    {
        if (actor == null)
            actor = GetComponent<BaseLevelCtrl>()?.Actor;

        actorCollider = actor != null ? actor.GetComponent<Collider2D>() : null;
        actorRenderers = actor != null
            ? actor.GetComponentsInChildren<Renderer>(true)
            : null;
        actorSortingGroup = actor != null ? actor.GetComponent<SortingGroup>() : null;
        if (actorSortingGroup != null)
        {
            actorInitialSortingLayerId = actorSortingGroup.sortingLayerID;
            actorInitialSortingOrder = actorSortingGroup.sortingOrder;
            actorSortingCached = true;
        }

        if (targetCamera == null)
            targetCamera = GetComponentInChildren<Camera>(true);

        if (followRange == null)
            followRange = GetComponent<CameraFollowRange>();

        if (legacyCameraFollowArea == null)
            legacyCameraFollowArea = GetComponent<CameraFollowArea>();

        if (hiddenEntranceBlockerRoot == null)
            hiddenEntranceBlockerRoot = transform.Find("HiddenEntranceBlocker");

        hiddenEntranceBlockerCollider = hiddenEntranceBlockerRoot != null
            ? hiddenEntranceBlockerRoot.GetComponent<BoxCollider2D>()
            : null;

        Transform upperGroundRoot = transform.Find("Ground_mid");
        upperGroundCollider = upperGroundRoot != null
            ? upperGroundRoot.GetComponent<BoxCollider2D>()
            : null;

        if (decoyDoorRoot == null)
            decoyDoorRoot = transform.Find("door_0");

        if (trapHandler == null)
            trapHandler = GetComponent<TrapEventHandlle>();
    }

    private void CacheSpringsAndBindTriggers()
    {
        springs.Clear();
        shortestVisualHeight = float.PositiveInfinity;
        longestVisualHeight = float.NegativeInfinity;

        for (int index = 0; index < transform.childCount; index++)
        {
            Transform child = transform.GetChild(index);
            if (!IsSpringRootName(child.name))
                continue;

            SpringState state = CreateSpringState(child);
            if (state == null)
                continue;

            SpringState capturedState = state;
            state.EnterListener = other => OnSpringEnter(capturedState, other);
            state.ExitListener = other => OnSpringExit(capturedState, other);
            state.TriggerHandle.OnEnter.AddListener(state.EnterListener);
            state.TriggerHandle.OnExit.AddListener(state.ExitListener);
            springs.Add(state);

            if (!state.IsRed)
            {
                shortestVisualHeight = Mathf.Min(shortestVisualHeight, state.VisualWorldHeight);
                longestVisualHeight = Mathf.Max(longestVisualHeight, state.VisualWorldHeight);
            }
        }

        if (float.IsInfinity(shortestVisualHeight))
        {
            shortestVisualHeight = 0f;
            longestVisualHeight = 1f;
        }
    }

    private SpringState CreateSpringState(Transform root)
    {
        bool isRed = root.name == "Spring";
        Transform button = null;
        GameObject redWhiteBase = null;
        BoxCollider2D redBodyCollider = null;
        SpriteRenderer visual = null;
        List<Transform> buttonParts = new List<Transform>();

        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);
            if (child.name == "Sp_spring_1_01")
                visual = child.GetComponent<SpriteRenderer>();

            if (isRed && child.name == "Sp_l94_dm_01")
                redBodyCollider = child.GetComponent<BoxCollider2D>();

            if (child.name != "Sp_button_1_02")
                continue;

            BoxCollider2D childCollider = child.GetComponent<BoxCollider2D>();
            if (childCollider != null)
            {
                buttonParts.Add(child);
                button = child;
            }
            else if (isRed)
            {
                redWhiteBase = child.gameObject;
            }
            else if (child.gameObject.activeSelf)
            {
                buttonParts.Add(child);
            }
        }

        OnTrigger2DHandle triggerHandle = root.GetComponent<OnTrigger2DHandle>();
        BoxCollider2D trigger = root.GetComponent<BoxCollider2D>();
        BoxCollider2D buttonCollider = button != null
            ? button.GetComponent<BoxCollider2D>()
            : null;

        if (button == null || buttonCollider == null || visual == null
            || trigger == null || triggerHandle == null)
        {
            Debug.LogError($"[Level120Ctrl] 弹簧 {root.name} 引用不完整。", this);
            return null;
        }

        Transform[] cachedButtonParts = buttonParts.ToArray();
        Vector3[] cachedButtonPartPositions = new Vector3[cachedButtonParts.Length];
        for (int index = 0; index < cachedButtonParts.Length; index++)
            cachedButtonPartPositions[index] = cachedButtonParts[index].localPosition;

        SpringState state = new SpringState
        {
            Root = root,
            ButtonParts = cachedButtonParts,
            ButtonPartRestLocalPositions = cachedButtonPartPositions,
            ButtonCollider = buttonCollider,
            RedWhiteBase = redWhiteBase,
            RedBodyCollider = redBodyCollider,
            Trigger = trigger,
            TriggerRestOffset = trigger.offset,
            Visual = visual,
            TriggerHandle = triggerHandle,
            VisualRestLocalPosition = visual.transform.localPosition,
            VisualRestLocalScale = visual.transform.localScale,
            VisualRestBottomWorldY = visual.bounds.min.y,
            VisualWorldHeight = visual.size.y * Mathf.Abs(visual.transform.lossyScale.y),
            IsRed = isRed,
            Armed = true
        };

        return state;
    }

    private static bool IsSpringRootName(string objectName)
    {
        return objectName == "Spring" || objectName.StartsWith("Spring (");
    }

    private void BindSpikeTriggers()
    {
        spikeHandles.Clear();
        if (trapHandler == null)
            return;

        for (int index = 0; index < transform.childCount; index++)
        {
            Transform child = transform.GetChild(index);
            if (!child.name.StartsWith("SpikeStrip_")
                && !child.name.StartsWith("Sp_caltrop_"))
            {
                continue;
            }

            OnTrigger2DHandle handle = child.GetComponent<OnTrigger2DHandle>();
            if (handle == null)
                continue;

            handle.OnEnter.AddListener(OnSpikeEnter);
            spikeHandles.Add(handle);
        }
    }

    private void OnSpikeEnter(Collider2D other)
    {
        if (trapHandler != null && other != null && other.CompareTag("Player"))
            trapHandler.PlayKnock(other);
    }

    private void DisableDecoyDoorTrigger()
    {
        if (decoyDoorRoot == null)
            return;

        OnTrigger2DHandle[] handles = decoyDoorRoot.GetComponentsInChildren<OnTrigger2DHandle>(true);
        for (int index = 0; index < handles.Length; index++)
            handles[index].enabled = false;

        Collider2D[] colliders = decoyDoorRoot.GetComponentsInChildren<Collider2D>(true);
        for (int index = 0; index < colliders.Length; index++)
        {
            if (colliders[index].isTrigger)
                colliders[index].enabled = false;
        }
    }

    private void InitializeCamera()
    {
        if (legacyCameraFollowArea != null)
            legacyCameraFollowArea.enabled = false;

        if (targetCamera == null)
            return;

        cameraInitialPosition = targetCamera.transform.position;
        cameraVelocity = Vector3.zero;
        cameraInitialized = true;
    }

    private void ResetAttempt()
    {
        hiddenEntranceOpened = false;
        downwardFollowUnlocked = false;
        cameraVelocity = Vector3.zero;

        if (hiddenEntranceBlockerCollider != null)
            hiddenEntranceBlockerCollider.enabled = true;
        RestoreActorSorting();

        DisableDecoyDoorTrigger();
        for (int index = 0; index < springs.Count; index++)
        {
            SpringState spring = springs[index];
            spring.Tween?.Kill();
            spring.Tween = null;
            if (spring.RedWhiteBase != null)
                spring.RedWhiteBase.SetActive(true);
            if (spring.RedBodyCollider != null)
                spring.RedBodyCollider.enabled = true;
            RestoreButtonParts(spring);
            if (spring.Trigger != null)
                spring.Trigger.offset = spring.TriggerRestOffset;
            spring.Visual.transform.localPosition = spring.VisualRestLocalPosition;
            spring.Visual.transform.localScale = spring.VisualRestLocalScale;
            spring.Armed = true;
            spring.ActorInside = false;
            spring.Busy = false;
        }
    }

    private void OnSpringEnter(SpringState spring, Collider2D other)
    {
        if (spring == null || other == null || !other.CompareTag("Player"))
            return;

        spring.ActorInside = true;
        TryPlaySpringBounce(spring);
    }

    private void TryPlaySpringBounce(SpringState spring)
    {
        if (!spring.Armed || spring.Busy || actor == null || actor.RB == null
            || actorCollider == null || actor.IsDied || actor.IsKnocked
            || actor.RB.linearVelocityY > 0.5f)
        {
            return;
        }

        Bounds actorBounds = actorCollider.bounds;
        Bounds buttonBounds = spring.ButtonCollider.bounds;
        float verticalGap = actorBounds.min.y - buttonBounds.max.y;
        bool overlapsButtonWidth = actorBounds.max.x > buttonBounds.min.x
                                   && actorBounds.min.x < buttonBounds.max.x;
        if (!overlapsButtonWidth
            || verticalGap > Mathf.Max(0f, springActivationMaxGap)
            || verticalGap < -Mathf.Max(0f, springActivationPenetration))
        {
            return;
        }

        PlaySpringBounce(spring);
    }

    private void RetryOccupiedSprings()
    {
        if (actorCollider == null)
            return;

        for (int index = 0; index < springs.Count; index++)
        {
            SpringState spring = springs[index];
            if (!spring.Armed || spring.Busy || spring.Trigger == null)
                continue;

            bool touchingTrigger = spring.ActorInside
                                   || spring.Trigger.bounds.Intersects(actorCollider.bounds);
            if (touchingTrigger)
                TryPlaySpringBounce(spring);
        }
    }

    private static void OnSpringExit(SpringState spring, Collider2D other)
    {
        if (spring != null && other != null && other.CompareTag("Player"))
            spring.ActorInside = false;
    }

    private void PlaySpringBounce(SpringState spring)
    {
        spring.Armed = false;
        spring.Busy = true;
        spring.Tween?.Kill();

        bool isFirstRedActivation = spring.IsRed && !hiddenEntranceOpened;
        float baseRevealDistance = spring.IsRed && hiddenEntranceOpened
            ? Mathf.Max(0f, redSpringRevealDistance)
            : 0f;
        float compressionDistance = spring.IsRed && hiddenEntranceOpened
            ? Mathf.Max(0f, redSpringRepeatCompressionDistance)
            : Mathf.Clamp(
                spring.VisualWorldHeight * compressionRatio,
                minimumCompressionDistance,
                maximumCompressionDistance);
        bool isRepeatRedActivation = spring.IsRed && hiddenEntranceOpened;
        float activeCompressionDuration = isRepeatRedActivation
            ? redSpringRepeatCompressionDuration
            : compressionDuration;
        float activeReturnDuration = isRepeatRedActivation
            ? redSpringRepeatReturnDuration
            : returnDuration;
        Ease activeCompressionEase = isRepeatRedActivation
            ? Ease.InOutSine
            : Ease.OutQuad;
        Ease activeReturnEase = isRepeatRedActivation
            ? Ease.OutCubic
            : Ease.OutQuad;

        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Append(DOVirtual.Float(
            0f,
            compressionDistance,
            Mathf.Max(0.01f, activeCompressionDuration),
            value => ApplyCompression(spring, value, baseRevealDistance)).SetEase(activeCompressionEase));
        sequence.AppendCallback(() => LaunchActorFromSpring(spring));
        sequence.Append(DOVirtual.Float(
            compressionDistance,
            0f,
            Mathf.Max(0.01f, activeReturnDuration),
            value => ApplyCompression(spring, value, baseRevealDistance)).SetEase(activeReturnEase));
        if (isFirstRedActivation)
        {
            sequence.Append(DOVirtual.Float(
                0f,
                Mathf.Max(0f, redSpringRevealDistance),
                Mathf.Max(0.01f, redSpringRevealDuration),
                value =>
                {
                    ApplyCompression(spring, 0f, 0f);
                    ApplyRevealOffset(spring, value);
                }).SetEase(Ease.OutBack));
        }
        sequence.OnComplete(() =>
        {
            spring.Tween = null;
            ApplyCompression(spring, 0f, baseRevealDistance);
            if (isFirstRedActivation)
                ApplyRevealOffset(spring, Mathf.Max(0f, redSpringRevealDistance));
            spring.Busy = false;
            if (isFirstRedActivation)
                OpenHiddenEntrance();
        });
        spring.Tween = sequence;
    }

    private static void ApplyCompression(
        SpringState spring,
        float worldDistance,
        float revealWorldDistance)
    {
        float rootScaleY = Mathf.Max(0.0001f, Mathf.Abs(spring.Root.lossyScale.y));
        float buttonLocalDistance = worldDistance / rootScaleY;
        float revealLocalDistance = revealWorldDistance / rootScaleY;

        if (spring.Trigger != null)
        {
            spring.Trigger.offset = spring.TriggerRestOffset
                + Vector2.up * revealLocalDistance
                + Vector2.down * buttonLocalDistance;
        }

        for (int index = 0; index < spring.ButtonParts.Length; index++)
        {
            spring.ButtonParts[index].localPosition =
                spring.ButtonPartRestLocalPositions[index]
                + Vector3.up * revealLocalDistance
                + Vector3.down * buttonLocalDistance;
        }
        spring.Visual.transform.localPosition =
            spring.VisualRestLocalPosition + Vector3.up * revealLocalDistance;
        float remainingHeightRatio = Mathf.Clamp(
            1f - worldDistance / Mathf.Max(0.0001f, spring.VisualWorldHeight),
            0.05f,
            1f);
        spring.Visual.transform.localScale = new Vector3(
            spring.VisualRestLocalScale.x,
            spring.VisualRestLocalScale.y * remainingHeightRatio,
            spring.VisualRestLocalScale.z);
        float targetBottomWorldY = spring.VisualRestBottomWorldY + revealWorldDistance;
        float bottomDelta = targetBottomWorldY - spring.Visual.bounds.min.y;
        spring.Visual.transform.position += Vector3.up * bottomDelta;
    }

    private static void ApplyRevealOffset(SpringState spring, float worldDistance)
    {
        float rootScaleY = Mathf.Max(0.0001f, Mathf.Abs(spring.Root.lossyScale.y));
        float localDistance = worldDistance / rootScaleY;

        if (spring.Trigger != null)
            spring.Trigger.offset = spring.TriggerRestOffset + Vector2.up * localDistance;

        for (int index = 0; index < spring.ButtonParts.Length; index++)
        {
            spring.ButtonParts[index].localPosition =
                spring.ButtonPartRestLocalPositions[index] + Vector3.up * localDistance;
        }

        spring.Visual.transform.localPosition =
            spring.VisualRestLocalPosition + Vector3.up * localDistance;
    }

    private void LaunchActorFromSpring(SpringState spring)
    {
        if (actor == null || actor.RB == null || actor.IsDied)
            return;

        float bounceVelocity = spring.IsRed
            ? (hiddenEntranceOpened
                ? redSpringRepeatBounceVelocity
                : redSpringBounceVelocity)
            : Mathf.Lerp(
                shortestSpringBounceVelocity,
                longestSpringBounceVelocity,
                Mathf.InverseLerp(
                    shortestVisualHeight,
                    longestVisualHeight,
                    spring.VisualWorldHeight));
        bounceVelocity = ClampBounceVelocityToLockedCamera(bounceVelocity);

        Vector2 velocity = actor.RB.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, bounceVelocity);
        actor.RB.linearVelocity = velocity;
        AudioManager.PlaySound("spring");
    }

    private float ClampBounceVelocityToLockedCamera(float desiredVelocity)
    {
        if (downwardFollowUnlocked || targetCamera == null || !targetCamera.orthographic
            || actor == null || actor.RB == null)
        {
            return desiredVelocity;
        }

        float visualTop = GetActorVisualTop();
        float cameraTop = cameraInitialPosition.y + targetCamera.orthographicSize;
        float availableRise = cameraTop - Mathf.Max(0f, lockedCameraTopPadding) - visualTop;
        float gravity = Mathf.Abs(Physics2D.gravity.y * actor.RB.gravityScale);
        if (availableRise <= 0f || gravity <= 0.0001f)
            return availableRise <= 0f ? 0f : desiredVelocity;

        float safeVelocity = Mathf.Sqrt(2f * gravity * availableRise);
        return Mathf.Min(desiredVelocity, safeVelocity);
    }

    private float GetActorVisualTop()
    {
        float visualTop = actorCollider != null
            ? actorCollider.bounds.max.y
            : actor != null
                ? actor.transform.position.y
                : float.NegativeInfinity;
        if (actorRenderers != null)
        {
            for (int index = 0; index < actorRenderers.Length; index++)
            {
                Renderer renderer = actorRenderers[index];
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    visualTop = Mathf.Max(visualTop, renderer.bounds.max.y);
            }
        }
        return visualTop;
    }

    private void OpenHiddenEntrance()
    {
        if (hiddenEntranceOpened)
            return;

        hiddenEntranceOpened = true;
        if (hiddenEntranceBlockerCollider != null)
            hiddenEntranceBlockerCollider.enabled = false;

        for (int index = 0; index < springs.Count; index++)
        {
            SpringState spring = springs[index];
            if (spring.IsRed && spring.RedBodyCollider != null)
                spring.RedBodyCollider.enabled = false;
        }
    }

    private void BeginActorPassageSorting()
    {
        if (!actorSortingCached || actorSortingGroup == null)
            return;

        actorSortingGroup.sortingLayerID = actorInitialSortingLayerId;
        actorSortingGroup.sortingOrder = actorPassageSortingOrder;
        actorPassageSortingActive = true;
    }

    private void UpdateActorPassageSorting()
    {
        if (!hiddenEntranceOpened || actorCollider == null || upperGroundCollider == null)
            return;

        Bounds actorBounds = actorCollider.bounds;
        Bounds groundBounds = upperGroundCollider.bounds;
        if (actorPassageSortingActive)
        {
            if (GetActorVisualTop() <= groundBounds.min.y)
                RestoreActorSorting();
            return;
        }

        if (actor != null && actor.RB != null && actor.RB.linearVelocityY > 0f)
            return;

        bool overlapsPlatformThickness = actorBounds.min.y < groundBounds.max.y
                                         && actorBounds.max.y > groundBounds.min.y;
        bool overlapsEntranceWidth = hiddenEntranceBlockerCollider == null
                                     || (actorBounds.max.x > hiddenEntranceBlockerCollider.bounds.min.x
                                         && actorBounds.min.x < hiddenEntranceBlockerCollider.bounds.max.x);
        if (overlapsPlatformThickness && overlapsEntranceWidth)
        {
            BeginActorPassageSorting();
            downwardFollowUnlocked = true;
        }
    }

    private void RestoreActorSorting()
    {
        actorPassageSortingActive = false;
        if (!actorSortingCached || actorSortingGroup == null)
            return;

        actorSortingGroup.sortingLayerID = actorInitialSortingLayerId;
        actorSortingGroup.sortingOrder = actorInitialSortingOrder;
    }

    private void RearmReleasedSprings()
    {
        if (actorCollider == null)
            return;

        for (int index = 0; index < springs.Count; index++)
        {
            SpringState spring = springs[index];
            if (spring.Armed || spring.Busy)
                continue;

            bool touching = spring.Trigger != null
                            && spring.Trigger.bounds.Intersects(actorCollider.bounds);
            bool clearAboveButton = spring.ButtonCollider != null
                                    && actorCollider.bounds.min.y
                                    >= spring.ButtonCollider.bounds.max.y
                                    + Mathf.Max(0f, springRearmClearance);
            if ((!spring.ActorInside && !touching) || clearAboveButton)
                spring.Armed = true;
        }
    }

    private void UpdateCameraPosition()
    {
        if (!cameraInitialized || targetCamera == null || actor == null)
            return;

        float halfWidth = Mathf.Max(0.01f, horizontalFollowWidth) * 0.5f;
        float actorX = actor.transform.position.x;
        bool followX = downwardFollowUnlocked
                       || actorX < horizontalFollowCenterX - halfWidth
                       || actorX > horizontalFollowCenterX + halfWidth;

        float requestedY = actor.transform.position.y + cameraOffset.y;
        float targetY = downwardFollowUnlocked
            ? requestedY
            : cameraInitialPosition.y;

        Vector3 targetPosition = new Vector3(
            followX ? actorX + cameraOffset.x : cameraInitialPosition.x,
            targetY,
            cameraInitialPosition.z);

        if (followRange != null)
            targetPosition = followRange.ClampOrthographic(targetCamera, targetPosition);

        bool following = followX || !Mathf.Approximately(targetY, cameraInitialPosition.y);
        float smoothTime = following
            ? Mathf.Max(0f, followSmoothTime)
            : Mathf.Max(0f, returnSmoothTime);

        if (smoothTime > 0f)
        {
            targetCamera.transform.position = Vector3.SmoothDamp(
                targetCamera.transform.position,
                targetPosition,
                ref cameraVelocity,
                smoothTime);
        }
        else
        {
            targetCamera.transform.position = targetPosition;
        }

        if (followRange != null)
        {
            targetCamera.transform.position = followRange.ClampOrthographic(
                targetCamera,
                targetCamera.transform.position);
        }
    }

    private void KillSpringTweens()
    {
        for (int index = 0; index < springs.Count; index++)
        {
            SpringState spring = springs[index];
            spring.Tween?.Kill();
            spring.Tween = null;
            if (spring.RedWhiteBase != null)
                spring.RedWhiteBase.SetActive(true);
            if (spring.RedBodyCollider != null)
                spring.RedBodyCollider.enabled = true;
            RestoreButtonParts(spring);
            if (spring.Trigger != null)
                spring.Trigger.offset = spring.TriggerRestOffset;
            if (spring.Visual != null)
            {
                spring.Visual.transform.localPosition = spring.VisualRestLocalPosition;
                spring.Visual.transform.localScale = spring.VisualRestLocalScale;
            }
            spring.Busy = false;
        }
    }

    private static void RestoreButtonParts(SpringState spring)
    {
        if (spring?.ButtonParts == null || spring.ButtonPartRestLocalPositions == null)
            return;

        int count = Mathf.Min(
            spring.ButtonParts.Length,
            spring.ButtonPartRestLocalPositions.Length);
        for (int index = 0; index < count; index++)
        {
            if (spring.ButtonParts[index] != null)
            {
                spring.ButtonParts[index].localPosition =
                    spring.ButtonPartRestLocalPositions[index];
            }
        }
    }
}

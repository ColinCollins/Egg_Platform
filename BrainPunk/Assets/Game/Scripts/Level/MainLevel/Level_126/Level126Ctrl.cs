using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.ItemEvent;
using Game.Play;
using Game.Scripts.Common;
using I2.Loc;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Level126Ctrl : MonoBehaviour, IEventSender
{
    [Serializable]
    private struct MatchSlot
    {
        public Vector3 localPosition;
        public float localAngle;
    }

    private sealed class MatchRuntime
    {
        public Transform Transform;
        public SpriteRenderer Renderer;
        public Vector3 InitialLocalPosition;
        public Quaternion InitialLocalRotation;
        public Vector3 InitialLocalScale;
        public Color InitialColor;
        public int InitialSortingOrder;
        public SpriteRenderer BurnedRenderer;
        public GameObject FireObject;
        public Animator FireAnimator;
        public GameObject SmokeObject;
        public ParticleSystem[] SmokeSystems = Array.Empty<ParticleSystem>();
        public bool SmokeTailScheduled;
        public bool Ignited;
        public bool BurnStarted;
    }

    private sealed class FrictionSparkRuntime
    {
        public GameObject Object;
        public ParticleSystem[] Systems = Array.Empty<ParticleSystem>();
        public float TailDuration;
    }

    private enum FlowState
    {
        Waiting,
        Settling,
        Failure,
        Burning,
        Unlocking,
        Unlocked,
        Completed
    }

    private const string SceneSortingLayer = "SceneLayer";
    private const string MatchNamePrefix = "Sp_l126_hc_01";
    private const string FireAnimationState = "Fire";
    private const string FrictionGlowName = "Glow";
    private const float FrictionSparkParticleSize = 0.06f;
    private const float FrictionSparkParticleSpeed = 8f;
    private const float FrictionSparkParticleLifetime = 0.25f;
    private const float FrictionGlowSize = 0.45f;
    private const float FireVerticalWorldOffset = 0.1f;
    private const int FailureExplosionSortingOrder = 250;
    private static readonly Quaternion DragLocalRotation = Quaternion.Euler(0f, 0f, 90f);

    [Header("题面")]
    [SerializeField] private Transform matchesRoot;
    [SerializeField] private int expectedMatchCount = 18;
    [SerializeField] private MatchSlot[] targetSlots;

    [Header("素材")]
    [SerializeField] private Sprite hintSprite;
    [SerializeField] private Sprite burnedMatchSprite;
    [SerializeField] private Sprite fireSprite;
    [SerializeField] private RuntimeAnimatorController fireController;
    [SerializeField] private GameObject frictionSparkPrefab;
    [SerializeField] private GameObject smokePrefab;

    [Header("门禁")]
    [SerializeField] private EventHandleListener doorLockUnlockHandle;
    [SerializeField] private Collider2D exitDoorTrigger;
    [SerializeField, Min(0f)] private float doorColliderEnableDelay = 1f;

    [Header("输入与吸附")]
    [SerializeField, Min(1f)] private float dragStartDistancePixels = 10f;
    [SerializeField, Min(0.01f)] private float snapDistance = 0.6f;
    [SerializeField, Min(1f)] private float selectedScaleMultiplier = 1.1f;
    [SerializeField, Min(0f)] private float settleMoveDuration = 0.2f;
    [SerializeField, Min(0f)] private float settleScaleDuration = 0.15f;

    [Header("浮现提示")]
    [SerializeField, Range(0f, 1f)] private float hintPeakAlpha = 0.65f;
    [SerializeField, Min(0.01f)] private float hintHalfCycleDuration = 0.6f;

    [Header("摩擦点火")]
    [SerializeField] private Transform blackboardGroup;
    [SerializeField] private Collider2D frictionSurface;
    [SerializeField, Min(1f)] private float frictionDistancePixels = 1000f;
    [SerializeField, Min(0f)] private float frictionContactTolerance = 0.08f;
    [SerializeField, Min(0.01f)] private float frictionSparkSpacing = 0.15f;
    [SerializeField, Min(1)] private int frictionSparkBurstCount = 8;
    [SerializeField] private int frictionSparkSortingOrder = 250;
    [SerializeField] private float matchHeadLocalOffset = 0.62f;
    [SerializeField] private string frictionSfxTag = "frictional";
    [SerializeField] private string ignitionSfxTag = "buildInFire";

    [Header("整式燃烧")]
    [SerializeField, Min(0f)] private float fireTransferDistance = 0.2f;
    [SerializeField, Min(0f)] private float burnSpreadInterval = 0.2f;
    [SerializeField, Min(0.01f)] private float burnFadeDuration = 0.8f;
    [SerializeField, Min(0f)] private float fireScaleDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float fireFadeDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float fireTargetScale = 0.9f;
    [SerializeField, Min(0f)] private float smokeTailDuration = 1.7f;

    [Header("失败")]
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private GameObject failureExplosionPrefab;
    [SerializeField] private string failureTipKey = "S_LevelTips_Des_level_126_1";
    [SerializeField] private string failureTipSfxTag = "passwordError";
    [SerializeField] private string failureExplosionSfxTag = "rocketExplosion";
    [SerializeField, Min(0f)] private float failureExplosionDelay = 1f;
    [SerializeField] private Vector2 failureKnockbackDirection = new Vector2(-0.5f, 0.7f);
    [SerializeField, Min(0f)] private float failureKnockbackForce = 50f;
    [SerializeField, Min(0.01f)] private float failureSpinDuration = 0.5f;
    [SerializeField, Min(0f)] private float failureResetDelay = 2f;

    [Header("成功")]
    [SerializeField] private string successTipKey = "S_LevelTips_Des_level_126_2";
    [SerializeField] private string successSfxTag = "passwordCorrect";

    private readonly List<MatchRuntime> matches = new List<MatchRuntime>();
    private readonly List<SpriteRenderer> staticHintRenderers = new List<SpriteRenderer>();
    private readonly List<FrictionSparkRuntime> frictionSparkRuntimes = new List<FrictionSparkRuntime>();

    private Camera worldCamera;
    private EventSubscriber eventSubscriber;
    private SpriteRenderer sourceHintRenderer;
    private Tween hintPulseTween;
    private Tween dragRotationTween;
    private MelenitasDev.SoundsGood.Sound frictionSound;
    private FrictionSparkRuntime activeFrictionSpark;
    private Collider2D activeFrictionSurface;
    private GameObject failureExplosionInstance;

    private FlowState flowState = FlowState.Waiting;
    private int selectedMatchIndex = -1;
    private int selectedBeforePress = -1;
    private int pressedMatchIndex = -1;
    private int pressedSlotIndex = -1;
    private bool pointerActive;
    private bool dragging;
    private bool dragRotationReady;
    private int trackingFingerId = -1;
    private Vector2 pointerDownScreen;
    private Vector2 lastPointerScreen;
    private Vector3 dragGrabOffset;
    private float dragWorldZ;
    private Vector3 dragSourceLocalPosition;
    private Quaternion dragSourceLocalRotation;
    private float frictionTravelPixels;
    private bool hasLastFrictionHeadPosition;
    private Vector3 lastFrictionHeadWorldPosition;
    private float frictionSparkDistanceToNextBurst;
    private int burnedMatchCount;
    private bool formulaBurnStarted;
    private bool burningSourcePlacementCompleted;
    private bool burnCompletionPending;
    private bool burnSuccessFeedbackShown;
    private bool gameplayPaused;
    private bool teardownStarted;
    private bool runtimeObjectsDestroyed;
    private bool configurationValid;

    private void Awake()
    {
        ResolveCamera();
        CollectMatches();
        CreateHintRenderers();
        ResolveFrictionSurface();
        RegisterEvents();

        if (exitDoorTrigger != null)
            exitDoorTrigger.enabled = false;

        configurationValid = ValidateConfiguration();
    }

    private void Update()
    {
        if (teardownStarted || !configurationValid || gameplayPaused || !IsGameplayPlaying())
            return;

        if (flowState != FlowState.Waiting)
            return;

        HandlePointerInput();
    }

    private void LateUpdate()
    {
        for (int i = 0; i < matches.Count; i++)
            UpdateFirePosition(matches[i]);
    }

    private void RegisterEvents()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        eventSubscriber.Subscribe<GameResetEvent>(OnGameReset);
        eventSubscriber.Subscribe<GameReplayEvent>(OnGameReplay);
    }

    private void ResolveCamera()
    {
        worldCamera = Camera.main;
        if (worldCamera == null)
            worldCamera = FindFirstObjectByType<Camera>();
    }

    private void CollectMatches()
    {
        matches.Clear();
        if (matchesRoot == null)
            return;

        for (int i = 0; i < matchesRoot.childCount; i++)
        {
            Transform child = matchesRoot.GetChild(i);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null || !child.name.StartsWith(MatchNamePrefix, StringComparison.Ordinal))
                continue;

            matches.Add(new MatchRuntime
            {
                Transform = child,
                Renderer = renderer,
                InitialLocalPosition = child.localPosition,
                InitialLocalRotation = child.localRotation,
                InitialLocalScale = child.localScale,
                InitialColor = renderer.color,
                InitialSortingOrder = renderer.sortingOrder
            });
        }
    }

    private bool ValidateConfiguration()
    {
        bool valid = true;
        if (matchesRoot == null || matches.Count != expectedMatchCount)
        {
            Debug.LogError($"[Level126Ctrl] Expected {expectedMatchCount} matches but found {matches.Count}.", this);
            valid = false;
        }

        if (targetSlots == null || targetSlots.Length != 7)
        {
            Debug.LogError("[Level126Ctrl] Exactly seven static target slots are required.", this);
            valid = false;
        }

        if (hintSprite == null || burnedMatchSprite == null || fireSprite == null ||
            fireController == null || frictionSparkPrefab == null || smokePrefab == null ||
            failureExplosionPrefab == null)
        {
            Debug.LogError("[Level126Ctrl] One or more match presentation assets are missing.", this);
            valid = false;
        }

        if (actor == null)
        {
            Debug.LogError("[Level126Ctrl] The Actor binding is missing.", this);
            valid = false;
        }

        if (frictionSurface == null)
        {
            Debug.LogError("[Level126Ctrl] The blackboard friction surface is missing.", this);
            valid = false;
        }

        if (doorLockUnlockHandle == null || exitDoorTrigger == null)
        {
            Debug.LogError("[Level126Ctrl] DoorLock handle or exit trigger is missing.", this);
            valid = false;
        }

        return valid;
    }

    private void ResolveFrictionSurface()
    {
        if (frictionSurface != null || blackboardGroup == null)
            return;

        frictionSurface = blackboardGroup.GetComponentInChildren<Collider2D>(true);
    }

    private void CreateHintRenderers()
    {
        if (matchesRoot == null || hintSprite == null || targetSlots == null)
            return;

        Vector3 hintScale = matches.Count > 0 ? matches[0].InitialLocalScale : Vector3.one;
        for (int i = 0; i < targetSlots.Length; i++)
        {
            SpriteRenderer renderer = CreateHintRenderer($"Level126TargetHint_{i + 1}", hintScale);
            renderer.transform.localPosition = targetSlots[i].localPosition;
            renderer.transform.localRotation = Quaternion.Euler(0f, 0f, targetSlots[i].localAngle);
            staticHintRenderers.Add(renderer);
        }

        sourceHintRenderer = CreateHintRenderer("Level126SourceHint", hintScale);
    }

    private SpriteRenderer CreateHintRenderer(string objectName, Vector3 localScale)
    {
        GameObject hintObject = new GameObject(objectName);
        hintObject.transform.SetParent(matchesRoot, false);
        hintObject.transform.localScale = localScale;

        SpriteRenderer renderer = hintObject.AddComponent<SpriteRenderer>();
        renderer.sprite = hintSprite;
        renderer.sortingOrder = 10;
        renderer.color = new Color(1f, 1f, 1f, 0f);
        renderer.enabled = false;
        return renderer;
    }

    private void HandlePointerInput()
    {
        if (!pointerActive)
        {
            if (TryGetPointerDown(out int fingerId, out Vector2 screenPosition))
                BeginPointer(fingerId, screenPosition);
            return;
        }

        if (!TryGetTrackedPointer(out Vector2 currentPosition, out bool released, out bool canceled))
        {
            EndPointer(lastPointerScreen, true);
            return;
        }

        if (released || canceled)
        {
            EndPointer(currentPosition, canceled);
            return;
        }

        UpdatePointer(currentPosition);
    }

    private void BeginPointer(int fingerId, Vector2 screenPosition)
    {
        pointerActive = true;
        trackingFingerId = fingerId;
        pointerDownScreen = screenPosition;
        lastPointerScreen = screenPosition;
        selectedBeforePress = selectedMatchIndex;
        pressedMatchIndex = FindMatchAt(screenPosition);
        pressedSlotIndex = -1;

        if (pressedMatchIndex >= 0)
        {
            SelectMatch(pressedMatchIndex);
            MatchRuntime match = matches[pressedMatchIndex];
            Vector3 pointerWorld = ScreenToWorld(screenPosition, match.Transform.position.z);
            dragGrabOffset = match.Transform.position - pointerWorld;
            dragWorldZ = match.Transform.position.z;
            return;
        }

        if (selectedMatchIndex >= 0)
            pressedSlotIndex = FindTargetSlotAt(screenPosition);
    }

    private void UpdatePointer(Vector2 screenPosition)
    {
        if (pressedMatchIndex >= 0 && !dragging &&
            Vector2.Distance(pointerDownScreen, screenPosition) >= dragStartDistancePixels)
        {
            BeginDrag(pressedMatchIndex);
        }

        if (dragging)
            UpdateDraggedMatch(screenPosition);

        lastPointerScreen = screenPosition;
    }

    private void EndPointer(Vector2 screenPosition, bool canceled)
    {
        if (dragging)
        {
            if (!canceled)
                UpdateDraggedMatch(screenPosition);

            if (canceled)
                ResolveCanceledDrag();
            else
                ResolveDrag();
        }
        else if (canceled)
        {
            CancelSelection();
        }
        else if (pressedMatchIndex >= 0)
        {
            if (selectedBeforePress == pressedMatchIndex)
                CancelSelection();
        }
        else if (pressedSlotIndex >= 0 && selectedMatchIndex >= 0)
        {
            CommitClickedPlacement(pressedSlotIndex);
        }
        else
        {
            CancelSelection();
        }

        ResetPointerState();
    }

    private void ResetPointerState()
    {
        pointerActive = false;
        dragging = false;
        trackingFingerId = -1;
        pressedMatchIndex = -1;
        pressedSlotIndex = -1;
        selectedBeforePress = -1;
    }

    private void SelectMatch(int matchIndex)
    {
        if (matchIndex < 0 || matchIndex >= matches.Count)
            return;

        if (selectedMatchIndex >= 0 && selectedMatchIndex != matchIndex)
            RestoreMatchSelectionVisual(matches[selectedMatchIndex]);

        selectedMatchIndex = matchIndex;
        MatchRuntime match = matches[matchIndex];
        match.Transform.localScale = match.InitialLocalScale * selectedScaleMultiplier;
        match.Renderer.sortingOrder = match.InitialSortingOrder + 100;
        ShowHints(false);
    }

    private void CancelSelection()
    {
        StopFrictionFeedback(true);
        if (selectedMatchIndex >= 0 && selectedMatchIndex < matches.Count)
            RestoreMatchSelectionVisual(matches[selectedMatchIndex]);

        selectedMatchIndex = -1;
        HideHints();
    }

    private static void RestoreMatchSelectionVisual(MatchRuntime match)
    {
        if (match == null || match.Transform == null || match.Renderer == null)
            return;

        match.Transform.localScale = match.InitialLocalScale;
        match.Renderer.sortingOrder = match.InitialSortingOrder;
    }

    private void BeginDrag(int matchIndex)
    {
        if (matchIndex < 0 || matchIndex >= matches.Count)
            return;

        dragging = true;
        MatchRuntime match = matches[matchIndex];
        dragSourceLocalPosition = match.Transform.localPosition;
        dragSourceLocalRotation = match.Transform.localRotation;
        ResetFrictionTracking();
        dragRotationTween?.Kill();
        dragRotationTween = null;
        dragRotationReady = Quaternion.Angle(match.Transform.localRotation, DragLocalRotation) <= 0.1f;
        if (dragRotationReady)
        {
            match.Transform.localRotation = DragLocalRotation;
        }
        else
        {
            dragRotationTween = match.Transform
                .DOLocalRotateQuaternion(DragLocalRotation, settleMoveDuration)
                .SetEase(Ease.OutQuad)
                .SetId(this)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    dragRotationTween = null;
                    dragRotationReady = true;
                });
        }

        sourceHintRenderer.transform.localPosition = dragSourceLocalPosition;
        sourceHintRenderer.transform.localRotation = dragSourceLocalRotation;
        sourceHintRenderer.transform.localScale = match.InitialLocalScale;
        ShowHints(true);
    }

    private void UpdateDraggedMatch(Vector2 screenPosition)
    {
        if (selectedMatchIndex < 0 || selectedMatchIndex >= matches.Count)
            return;

        MatchRuntime match = matches[selectedMatchIndex];
        Vector3 pointerWorld = ScreenToWorld(screenPosition, dragWorldZ);
        Vector3 targetWorld = pointerWorld + dragGrabOffset;
        targetWorld.z = dragWorldZ;
        match.Transform.position = targetWorld;

        if (dragRotationReady)
            UpdateFriction(match, screenPosition - lastPointerScreen);
        UpdateFirePosition(match);
        if (match.Ignited)
            TryIgniteNearbyMatches(match);
    }

    private void ResolveDrag()
    {
        if (selectedMatchIndex < 0 || selectedMatchIndex >= matches.Count)
            return;

        MatchRuntime match = matches[selectedMatchIndex];
        StopFrictionFeedback(true);

        int targetSlot = FindNearestTargetSlot(match.Transform.position, out float targetDistance);
        Vector3 sourceWorld = matchesRoot.TransformPoint(dragSourceLocalPosition);
        float sourceDistance = Vector2.Distance(match.Transform.position, sourceWorld);
        bool sourceIsNearest = sourceDistance <= snapDistance && sourceDistance <= targetDistance;

        if (match.Ignited)
        {
            if (targetSlot >= 0 && !sourceIsNearest)
                SettleMatch(match, targetSlots[targetSlot].localPosition,
                    Quaternion.Euler(0f, 0f, targetSlots[targetSlot].localAngle),
                    CompleteIgnitedPlacement);
            else
                SettleMatch(match, dragSourceLocalPosition, dragSourceLocalRotation,
                    CompleteIgnitedPlacement);
            return;
        }

        if (targetSlot >= 0 && !sourceIsNearest)
        {
            SettleMatch(match, targetSlots[targetSlot].localPosition,
                Quaternion.Euler(0f, 0f, targetSlots[targetSlot].localAngle), BeginFailure);
            return;
        }

        SettleMatch(match, dragSourceLocalPosition, dragSourceLocalRotation, ReturnToWaiting);
    }

    private void ResolveCanceledDrag()
    {
        if (selectedMatchIndex < 0 || selectedMatchIndex >= matches.Count)
            return;

        MatchRuntime match = matches[selectedMatchIndex];
        StopFrictionFeedback(true);
        Action<MatchRuntime> completion = match.Ignited ? CompleteIgnitedPlacement : ReturnToWaiting;
        SettleMatch(match, dragSourceLocalPosition, dragSourceLocalRotation, completion);
    }

    private void CommitClickedPlacement(int slotIndex)
    {
        if (selectedMatchIndex < 0 || selectedMatchIndex >= matches.Count ||
            slotIndex < 0 || slotIndex >= targetSlots.Length)
            return;

        MatchRuntime match = matches[selectedMatchIndex];
        SettleMatch(match, targetSlots[slotIndex].localPosition,
            Quaternion.Euler(0f, 0f, targetSlots[slotIndex].localAngle), BeginFailure);
    }

    private void SettleMatch(MatchRuntime match, Vector3 localPosition, Quaternion localRotation,
        Action<MatchRuntime> onComplete)
    {
        dragRotationTween?.Kill();
        dragRotationTween = null;
        dragRotationReady = false;
        flowState = FlowState.Settling;
        Sequence sequence = DOTween.Sequence()
            .SetId(this)
            .SetLink(gameObject);

        sequence.Join(match.Transform.DOLocalMove(localPosition, settleMoveDuration).SetEase(Ease.OutQuad));
        sequence.Join(match.Transform.DOLocalRotateQuaternion(localRotation, settleMoveDuration).SetEase(Ease.OutQuad));
        sequence.Append(match.Transform.DOScale(match.InitialLocalScale, settleScaleDuration).SetEase(Ease.OutQuad));
        sequence.OnComplete(() =>
        {
            match.Renderer.sortingOrder = match.InitialSortingOrder;
            if (match.BurnedRenderer != null)
                match.BurnedRenderer.sortingOrder = match.InitialSortingOrder + 1;
            onComplete?.Invoke(match);
        });
    }

    private void ReturnToWaiting(MatchRuntime match)
    {
        match.Ignited = false;
        selectedMatchIndex = -1;
        HideHints();
        flowState = FlowState.Waiting;
    }

    private void BeginFailure(MatchRuntime match)
    {
        selectedMatchIndex = -1;
        HideHints();
        StopFrictionFeedback(true);
        flowState = FlowState.Failure;

        string message = LocalizationManager.GetTranslation(failureTipKey);
        if (string.IsNullOrWhiteSpace(message) ||
            string.Equals(message, failureTipKey, StringComparison.Ordinal))
        {
            message = "你答错了";
        }
        SystemTips.Show(message);
        AudioManager.PlaySound(failureTipSfxTag);

        DOVirtual.DelayedCall(failureExplosionDelay, PlayFailureExplosion)
            .SetId(this)
            .SetLink(gameObject);
    }

    private void PlayFailureExplosion()
    {
        if (teardownStarted || flowState != FlowState.Failure)
            return;

        Vector3 explosionPosition = actor != null ? actor.transform.position : transform.position;
        if (failureExplosionInstance != null)
            Destroy(failureExplosionInstance);

        failureExplosionInstance = Instantiate(
            failureExplosionPrefab,
            explosionPosition,
            failureExplosionPrefab.transform.rotation,
            transform);
        failureExplosionInstance.name = "Level126FailureExplosion";

        ParticleSystem[] systems = failureExplosionInstance.GetComponentsInChildren<ParticleSystem>(true);
        ParticleSystemRenderer[] renderers =
            failureExplosionInstance.GetComponentsInChildren<ParticleSystemRenderer>(true);
        int sortingLayerId = SortingLayer.NameToID(SceneSortingLayer);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingLayerID = sortingLayerId;
            renderers[i].sortingOrder = FailureExplosionSortingOrder + i;
        }

        float destroyDelay = 0.5f;
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem particle = systems[i];
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particle.main;
            destroyDelay = Mathf.Max(
                destroyDelay,
                main.duration + main.startLifetime.constantMax + main.startDelay.constantMax);
            particle.Play(false);
        }
        Destroy(failureExplosionInstance, destroyDelay + 0.2f);

        AudioManager.PlaySound(failureExplosionSfxTag);
        if (actor != null)
        {
            actor.Die();
            actor.Knockback(failureKnockbackDirection, failureKnockbackForce).Forget();
            if (actor.Body != null)
            {
                actor.Body.DOKill();
                actor.Body
                    .DOLocalRotate(new Vector3(0f, 0f, 360f), failureSpinDuration,
                        RotateMode.FastBeyond360)
                    .SetLoops(-1)
                    .SetId(this)
                    .SetLink(gameObject);
            }
        }

        DOVirtual.DelayedCall(failureResetDelay, () =>
            this.DispatchEvent(Witness<GameResetEvent>._, GameResetType.Failed))
            .SetId(this)
            .SetLink(gameObject);
    }

    private void UpdateFriction(MatchRuntime match, Vector2 screenDelta)
    {
        if (match.Ignited)
            return;

        Vector3 headPosition = GetMatchHeadWorldPosition(match);
        Collider2D surface = FindFrictionSurfaceAt(headPosition);
        if (surface == null)
        {
            StopFrictionFeedback(true);
            return;
        }

        if (surface != activeFrictionSurface)
        {
            StopFrictionFeedback(true);
            activeFrictionSurface = surface;
            hasLastFrictionHeadPosition = true;
            lastFrictionHeadWorldPosition = headPosition;
            return;
        }

        Vector3 previousHeadPosition = hasLastFrictionHeadPosition
            ? lastFrictionHeadWorldPosition
            : headPosition;
        lastFrictionHeadWorldPosition = headPosition;
        hasLastFrictionHeadPosition = true;

        float screenMovement = screenDelta.magnitude;
        if (screenMovement <= 0.01f)
            return;

        StartFrictionFeedback(previousHeadPosition);
        float worldMovement = Vector2.Distance(previousHeadPosition, headPosition);
        if (worldMovement > 0.0001f)
            EmitFrictionSparks(previousHeadPosition, headPosition, worldMovement);

        frictionTravelPixels += screenMovement;
        if (frictionTravelPixels >= frictionDistancePixels)
            IgniteMatch(match);
    }

    private Collider2D FindFrictionSurfaceAt(Vector3 worldPosition)
    {
        return frictionSurface != null && IsPointOnCollider(frictionSurface, worldPosition)
            ? frictionSurface
            : null;
    }

    private bool IsPointOnCollider(Collider2D collider, Vector3 worldPosition)
    {
        if (collider is BoxCollider2D box)
        {
            Vector3 localPoint = box.transform.InverseTransformPoint(worldPosition);
            Vector3 scale = box.transform.lossyScale;
            float toleranceX = frictionContactTolerance / Mathf.Max(0.0001f, Mathf.Abs(scale.x));
            float toleranceY = frictionContactTolerance / Mathf.Max(0.0001f, Mathf.Abs(scale.y));
            Vector2 delta = (Vector2)localPoint - box.offset;
            Vector2 halfSize = box.size * 0.5f;
            return Mathf.Abs(delta.x) <= halfSize.x + toleranceX &&
                   Mathf.Abs(delta.y) <= halfSize.y + toleranceY;
        }

        Vector2 point = worldPosition;
        if (collider.enabled && collider.OverlapPoint(point))
            return true;

        if (!collider.enabled)
            return false;

        Vector2 closest = collider.ClosestPoint(point);
        return Vector2.Distance(closest, point) <= frictionContactTolerance;
    }

    private void StartFrictionFeedback(Vector3 worldPosition)
    {
        if (frictionSound == null)
            frictionSound = AudioManager.PlaySound(frictionSfxTag, loop: true);

        if (activeFrictionSpark != null && activeFrictionSpark.Object != null)
        {
            activeFrictionSpark.Object.transform.position = worldPosition;
            return;
        }

        GameObject sparkObject = Instantiate(
            frictionSparkPrefab,
            worldPosition,
            frictionSparkPrefab.transform.rotation,
            transform);
        sparkObject.name = "Level126FrictionSpark";

        FrictionSparkRuntime runtime = new FrictionSparkRuntime
        {
            Object = sparkObject,
            Systems = sparkObject.GetComponentsInChildren<ParticleSystem>(true),
            TailDuration = 0.1f
        };

        int sortingLayerId = SortingLayer.NameToID(SceneSortingLayer);
        for (int i = 0; i < runtime.Systems.Length; i++)
        {
            ParticleSystem particle = runtime.Systems[i];
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.EmissionModule emission = particle.emission;
            emission.enabled = false;

            ParticleSystem.MainModule main = particle.main;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            if (particle.transform == sparkObject.transform)
            {
                main.startSize = FrictionSparkParticleSize;
                main.startSpeed = FrictionSparkParticleSpeed;
                main.startLifetime = FrictionSparkParticleLifetime;
            }
            else if (string.Equals(particle.gameObject.name, FrictionGlowName, StringComparison.Ordinal))
            {
                main.startSize = FrictionGlowSize;
            }

            runtime.TailDuration = Mathf.Max(runtime.TailDuration, main.startLifetime.constantMax + 0.1f);

            ParticleSystemRenderer renderer = particle.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = frictionSparkSortingOrder;
            }

            particle.Play(false);
        }

        frictionSparkRuntimes.Add(runtime);
        activeFrictionSpark = runtime;
    }

    private void EmitFrictionSparks(Vector3 fromWorldPosition, Vector3 toWorldPosition, float movement)
    {
        if (activeFrictionSpark == null || activeFrictionSpark.Object == null || movement <= 0f)
            return;

        float spacing = Mathf.Max(0.01f, frictionSparkSpacing);
        Vector3 direction = (toWorldPosition - fromWorldPosition) / movement;
        float traversed = 0f;
        float remaining = movement;

        while (remaining >= frictionSparkDistanceToNextBurst)
        {
            float step = frictionSparkDistanceToNextBurst;
            traversed += step;
            remaining -= step;
            EmitFrictionSparkBurst(activeFrictionSpark, fromWorldPosition + direction * traversed);
            frictionSparkDistanceToNextBurst = spacing;
        }

        frictionSparkDistanceToNextBurst = Mathf.Max(0f, frictionSparkDistanceToNextBurst - remaining);
        activeFrictionSpark.Object.transform.position = toWorldPosition;
    }

    private void EmitFrictionSparkBurst(FrictionSparkRuntime runtime, Vector3 worldPosition)
    {
        if (runtime == null || runtime.Object == null)
            return;

        runtime.Object.transform.position = worldPosition;
        for (int i = 0; i < runtime.Systems.Length; i++)
        {
            ParticleSystem particle = runtime.Systems[i];
            if (particle == null)
                continue;

            int count = particle.transform == runtime.Object.transform
                ? Mathf.Max(1, frictionSparkBurstCount)
                : 1;
            particle.Emit(count);
        }
    }

    private void RetireActiveFrictionSpark()
    {
        if (activeFrictionSpark == null)
            return;

        FrictionSparkRuntime runtime = activeFrictionSpark;
        activeFrictionSpark = null;
        for (int i = 0; i < runtime.Systems.Length; i++)
        {
            if (runtime.Systems[i] != null)
                runtime.Systems[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        DOVirtual.DelayedCall(runtime.TailDuration, () => DestroyFrictionSparkRuntime(runtime))
            .SetId(this)
            .SetLink(gameObject);
    }

    private void DestroyFrictionSparkRuntime(FrictionSparkRuntime runtime)
    {
        if (runtime == null)
            return;

        frictionSparkRuntimes.Remove(runtime);
        if (runtime.Object != null)
            Destroy(runtime.Object);
    }

    private void ResetFrictionTracking()
    {
        activeFrictionSurface = null;
        frictionTravelPixels = 0f;
        hasLastFrictionHeadPosition = false;
        lastFrictionHeadWorldPosition = Vector3.zero;
        frictionSparkDistanceToNextBurst = 0f;
    }

    private void StopFrictionFeedback(bool clearSurface)
    {
        if (frictionSound != null)
        {
            frictionSound.Stop();
            frictionSound = null;
        }

        RetireActiveFrictionSpark();
        if (clearSurface)
            ResetFrictionTracking();
    }

    private void IgniteMatch(MatchRuntime match)
    {
        if (match.Ignited)
            return;

        match.Ignited = true;
        StopFrictionFeedback(true);
        AudioManager.PlaySound(ignitionSfxTag);
        EnsureFire(match);
    }

    private void TryIgniteNearbyMatches(MatchRuntime sourceMatch)
    {
        if (sourceMatch == null || !sourceMatch.Ignited || sourceMatch.Transform == null ||
            sourceMatch.FireObject == null)
        {
            return;
        }

        Vector3 fireRootPosition = GetMatchHeadWorldPosition(sourceMatch);
        float transferDistance = Mathf.Max(0f, fireTransferDistance);
        float transferDistanceSqr = transferDistance * transferDistance;
        List<MatchRuntime> newlyIgnitedMatches = null;

        for (int i = 0; i < matches.Count; i++)
        {
            MatchRuntime targetMatch = matches[i];
            if (ReferenceEquals(targetMatch, sourceMatch) || targetMatch.Ignited ||
                targetMatch.BurnStarted || targetMatch.Renderer == null)
            {
                continue;
            }

            if (targetMatch.Renderer.bounds.SqrDistance(fireRootPosition) > transferDistanceSqr)
                continue;

            targetMatch.Ignited = true;
            EnsureFire(targetMatch);
            if (newlyIgnitedMatches == null)
                newlyIgnitedMatches = new List<MatchRuntime>();
            newlyIgnitedMatches.Add(targetMatch);
        }

        if (newlyIgnitedMatches == null || newlyIgnitedMatches.Count == 0)
            return;

        AudioManager.PlaySound(ignitionSfxTag);
        if (!formulaBurnStarted)
        {
            StartFormulaBurn(sourceMatch, newlyIgnitedMatches);
            return;
        }

        for (int i = 0; i < newlyIgnitedMatches.Count; i++)
            BurnMatch(newlyIgnitedMatches[i]);
    }

    private void EnsureFire(MatchRuntime match)
    {
        if (match.FireObject == null)
        {
            GameObject fireObject = new GameObject($"Level126Fire_{matches.IndexOf(match) + 1}");
            fireObject.transform.SetParent(transform, false);
            fireObject.transform.position =
                GetMatchHeadWorldPosition(match) + Vector3.down * FireVerticalWorldOffset;
            fireObject.transform.rotation = Quaternion.identity;
            fireObject.transform.localScale = Vector3.zero;

            SpriteRenderer renderer = fireObject.AddComponent<SpriteRenderer>();
            renderer.sprite = fireSprite;
            renderer.sortingLayerID = SortingLayer.NameToID(SceneSortingLayer);
            renderer.sortingOrder = 205;

            Animator animator = fireObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = fireController;
            animator.Rebind();
            animator.Play(FireAnimationState, 0, 0f);
            animator.Update(0f);
            animator.speed = gameplayPaused ? 0f : 1f;

            match.FireObject = fireObject;
            match.FireAnimator = animator;
            fireObject.transform.DOScale(Vector3.one * fireTargetScale, fireScaleDuration)
                .SetEase(Ease.OutBack, 3f)
                .SetId(this)
                .SetLink(gameObject);
        }

        EnsureSmoke(match);
    }

    private void EnsureSmoke(MatchRuntime match)
    {
        if (match.SmokeObject != null)
            return;

        GameObject smokeObject = Instantiate(
            smokePrefab,
            GetMatchHeadWorldPosition(match),
            smokePrefab.transform.rotation,
            transform);
        smokeObject.name = $"Level126Smoke_{matches.IndexOf(match) + 1}";

        ParticleSystemRenderer[] renderers = smokeObject.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingLayerID = SortingLayer.NameToID(SceneSortingLayer);
            renderers[i].sortingOrder = 204;
        }

        match.SmokeObject = smokeObject;
        match.SmokeSystems = smokeObject.GetComponentsInChildren<ParticleSystem>(true);
        match.SmokeTailScheduled = false;
        for (int i = 0; i < match.SmokeSystems.Length; i++)
            match.SmokeSystems[i].Play(true);
    }

    private void UpdateFirePosition(MatchRuntime match)
    {
        Vector3 headPosition = GetMatchHeadWorldPosition(match);
        if (match.FireObject != null)
        {
            match.FireObject.transform.position = headPosition + Vector3.down * FireVerticalWorldOffset;
            match.FireObject.transform.rotation = Quaternion.identity;
        }

        if (match.SmokeObject != null)
        {
            match.SmokeObject.transform.position = headPosition;
            match.SmokeObject.transform.rotation = smokePrefab.transform.rotation;
        }
    }

    private Vector3 GetMatchHeadWorldPosition(MatchRuntime match)
    {
        return match.Transform.TransformPoint(Vector3.right * matchHeadLocalOffset);
    }

    private void CompleteIgnitedPlacement(MatchRuntime ignitionMatch)
    {
        selectedMatchIndex = -1;
        HideHints();
        burningSourcePlacementCompleted = true;

        if (!formulaBurnStarted)
        {
            StartFormulaBurn(ignitionMatch, null);
            return;
        }

        flowState = FlowState.Burning;
        if (burnCompletionPending)
            CompleteFormulaBurn();
    }

    private void StartFormulaBurn(MatchRuntime ignitionMatch, List<MatchRuntime> contactMatches)
    {
        if (formulaBurnStarted || ignitionMatch == null)
            return;

        formulaBurnStarted = true;
        burnedMatchCount = 0;
        burnCompletionPending = false;

        bool startedWhileDragging = contactMatches != null && contactMatches.Count > 0;
        burningSourcePlacementCompleted = !startedWhileDragging;
        if (!startedWhileDragging)
        {
            selectedMatchIndex = -1;
            HideHints();
            flowState = FlowState.Burning;
        }

        List<MatchRuntime> immediateMatches = new List<MatchRuntime> { ignitionMatch };
        if (contactMatches != null)
        {
            for (int i = 0; i < contactMatches.Count; i++)
            {
                MatchRuntime contactMatch = contactMatches[i];
                if (contactMatch != null && !immediateMatches.Contains(contactMatch))
                    immediateMatches.Add(contactMatch);
            }
        }

        for (int i = 0; i < immediateMatches.Count; i++)
            BurnMatch(immediateMatches[i]);

        MatchRuntime spreadOrigin = immediateMatches.Count > 1
            ? immediateMatches[1]
            : ignitionMatch;
        Vector3 originPosition = spreadOrigin.Transform.position;
        List<MatchRuntime> ordered = new List<MatchRuntime>(matches);
        for (int i = 0; i < immediateMatches.Count; i++)
            ordered.Remove(immediateMatches[i]);
        ordered.Sort((left, right) =>
            Vector2.Distance(left.Transform.position, originPosition)
                .CompareTo(Vector2.Distance(right.Transform.position, originPosition)));

        for (int i = 0; i < ordered.Count; i++)
        {
            MatchRuntime match = ordered[i];
            float delay = (i + 1) * burnSpreadInterval;
            DOVirtual.DelayedCall(delay, () => BurnMatch(match))
                .SetId(this)
                .SetLink(gameObject);
        }
    }

    private void BurnMatch(MatchRuntime match)
    {
        if (match == null || match.BurnStarted || !formulaBurnStarted || teardownStarted ||
            flowState == FlowState.Failure || flowState == FlowState.Completed)
        {
            return;
        }

        match.Ignited = true;
        match.BurnStarted = true;
        EnsureFire(match);
        SpriteRenderer burnedRenderer = GetOrCreateBurnedRenderer(match);

        Color burnedColor = match.InitialColor;
        burnedColor.a = 0f;
        burnedRenderer.color = burnedColor;
        burnedRenderer.enabled = true;

        Sequence sequence = DOTween.Sequence()
            .SetId(this)
            .SetLink(gameObject);
        sequence.Join(match.Renderer.DOFade(0f, burnFadeDuration));
        sequence.Join(burnedRenderer.DOFade(match.InitialColor.a, burnFadeDuration));
        sequence.OnComplete(() => CompleteMatchBurn(match));
    }

    private SpriteRenderer GetOrCreateBurnedRenderer(MatchRuntime match)
    {
        if (match.BurnedRenderer != null)
            return match.BurnedRenderer;

        GameObject burnedObject = new GameObject("BurnedMatch");
        burnedObject.transform.SetParent(match.Transform, false);
        burnedObject.transform.localPosition = Vector3.zero;
        burnedObject.transform.localRotation = Quaternion.identity;
        burnedObject.transform.localScale = Vector3.one;

        SpriteRenderer renderer = burnedObject.AddComponent<SpriteRenderer>();
        renderer.sprite = burnedMatchSprite;
        renderer.sharedMaterial = match.Renderer.sharedMaterial;
        renderer.sortingLayerID = match.Renderer.sortingLayerID;
        renderer.sortingOrder = match.Renderer.sortingOrder + 1;
        match.BurnedRenderer = renderer;
        return renderer;
    }

    private void CompleteMatchBurn(MatchRuntime match)
    {
        FadeOutFire(match, () =>
        {
            StopSmoke(match, true);
            burnedMatchCount++;
            if (burnedMatchCount < matches.Count)
                return;

            CompleteFormulaBurn();
        });
    }

    private void CompleteFormulaBurn()
    {
        if (!burningSourcePlacementCompleted)
        {
            burnCompletionPending = true;
            return;
        }

        burnCompletionPending = false;
        flowState = FlowState.Burning;
        ShowBurnSuccessFeedback();
        UnlockExitDoor();
    }

    private void ShowBurnSuccessFeedback()
    {
        if (burnSuccessFeedbackShown)
            return;

        burnSuccessFeedbackShown = true;
        AudioManager.PlaySound(successSfxTag);
        string message = LocalizationManager.GetTranslation(successTipKey);
        if (string.IsNullOrWhiteSpace(message) ||
            string.Equals(message, successTipKey, StringComparison.Ordinal))
        {
            message = "算式成立";
        }
        SystemTips.Show(message);
    }

    private void FadeOutFire(MatchRuntime match, Action onComplete)
    {
        if (match.FireObject == null)
        {
            match.FireAnimator = null;
            onComplete?.Invoke();
            return;
        }

        GameObject fireObject = match.FireObject;
        SpriteRenderer fireRenderer = fireObject.GetComponent<SpriteRenderer>();
        float duration = Mathf.Max(0f, fireFadeDuration);
        if (duration <= 0f)
        {
            Destroy(fireObject);
            match.FireObject = null;
            match.FireAnimator = null;
            onComplete?.Invoke();
            return;
        }

        Sequence sequence = DOTween.Sequence()
            .SetId(this)
            .SetLink(gameObject);
        sequence.Join(fireObject.transform.DOScale(Vector3.zero, duration).SetEase(Ease.InQuad));
        if (fireRenderer != null)
            sequence.Join(fireRenderer.DOFade(0f, duration).SetEase(Ease.InQuad));
        sequence.OnComplete(() =>
        {
            if (match.FireObject == fireObject)
            {
                Destroy(fireObject);
                match.FireObject = null;
                match.FireAnimator = null;
            }
            onComplete?.Invoke();
        });
    }

    private void StopFire(MatchRuntime match, bool preserveSmokeTail = true)
    {
        if (match.FireObject != null)
            Destroy(match.FireObject);

        match.FireObject = null;
        match.FireAnimator = null;
        StopSmoke(match, preserveSmokeTail);
    }

    private void StopSmoke(MatchRuntime match, bool preserveSmokeTail)
    {
        if (match.SmokeObject == null)
            return;

        if (!preserveSmokeTail)
        {
            DestroySmoke(match);
            return;
        }

        for (int i = 0; i < match.SmokeSystems.Length; i++)
        {
            if (match.SmokeSystems[i] != null)
                match.SmokeSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (match.SmokeTailScheduled)
            return;

        match.SmokeTailScheduled = true;
        DOVirtual.DelayedCall(smokeTailDuration, () => DestroySmoke(match))
            .SetId(this)
            .SetLink(gameObject);
    }

    private void DestroySmoke(MatchRuntime match)
    {
        if (match.SmokeObject != null)
            Destroy(match.SmokeObject);

        match.SmokeObject = null;
        match.SmokeSystems = Array.Empty<ParticleSystem>();
        match.SmokeTailScheduled = false;
    }

    private void UnlockExitDoor()
    {
        if (flowState != FlowState.Burning)
            return;

        flowState = FlowState.Unlocking;
        if (doorLockUnlockHandle == null || exitDoorTrigger == null)
        {
            Debug.LogError("[Level126Ctrl] Cannot unlock the exit because the DoorLock binding is missing.", this);
            return;
        }

        doorLockUnlockHandle.Execute();
        DOVirtual.DelayedCall(doorColliderEnableDelay, () =>
        {
            if (exitDoorTrigger != null)
                exitDoorTrigger.enabled = true;
            flowState = FlowState.Unlocked;
        })
            .SetId(this)
            .SetLink(gameObject);
    }

    private void ShowHints(bool includeSource)
    {
        for (int i = 0; i < staticHintRenderers.Count; i++)
            staticHintRenderers[i].enabled = true;

        if (sourceHintRenderer != null)
            sourceHintRenderer.enabled = includeSource;

        hintPulseTween?.Kill();
        ApplyHintAlpha(0f);
        hintPulseTween = DOVirtual.Float(0f, hintPeakAlpha, hintHalfCycleDuration, ApplyHintAlpha)
            .SetLoops(-1, LoopType.Yoyo)
            .SetId(this)
            .SetLink(gameObject);
    }

    private void HideHints()
    {
        hintPulseTween?.Kill();
        hintPulseTween = null;
        ApplyHintAlpha(0f);

        for (int i = 0; i < staticHintRenderers.Count; i++)
            staticHintRenderers[i].enabled = false;

        if (sourceHintRenderer != null)
            sourceHintRenderer.enabled = false;
    }

    private void ApplyHintAlpha(float alpha)
    {
        for (int i = 0; i < staticHintRenderers.Count; i++)
            SetRendererAlpha(staticHintRenderers[i], alpha);

        if (sourceHintRenderer != null && sourceHintRenderer.enabled)
            SetRendererAlpha(sourceHintRenderer, alpha);
    }

    private static void SetRendererAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null)
            return;

        Color color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }

    private int FindMatchAt(Vector2 screenPosition)
    {
        Vector3 worldPosition = ScreenToWorld(screenPosition,
            matchesRoot != null ? matchesRoot.position.z : 0f);
        int bestIndex = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < matches.Count; i++)
        {
            Bounds bounds = matches[i].Renderer.bounds;
            if (worldPosition.x < bounds.min.x || worldPosition.x > bounds.max.x ||
                worldPosition.y < bounds.min.y || worldPosition.y > bounds.max.y)
                continue;

            float distance = Vector2.Distance(worldPosition, matches[i].Transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestIndex = i;
        }

        return bestIndex;
    }

    private int FindTargetSlotAt(Vector2 screenPosition)
    {
        Vector3 worldPosition = ScreenToWorld(screenPosition,
            matchesRoot != null ? matchesRoot.position.z : 0f);
        return FindNearestTargetSlot(worldPosition, out _);
    }

    private int FindNearestTargetSlot(Vector3 worldPosition, out float bestDistance)
    {
        int bestIndex = -1;
        bestDistance = snapDistance;
        if (matchesRoot == null || targetSlots == null)
            return bestIndex;

        for (int i = 0; i < targetSlots.Length; i++)
        {
            Vector3 slotWorld = matchesRoot.TransformPoint(targetSlots[i].localPosition);
            float distance = Vector2.Distance(worldPosition, slotWorld);
            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            bestIndex = i;
        }

        return bestIndex;
    }

    private Vector3 ScreenToWorld(Vector2 screenPosition, float worldZ)
    {
        if (worldCamera == null)
            ResolveCamera();
        if (worldCamera == null)
            return Vector3.zero;

        float zDistance = Mathf.Abs(worldCamera.transform.position.z - worldZ);
        return worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, zDistance));
    }

    private bool TryGetPointerDown(out int fingerId, out Vector2 screenPosition)
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began)
                continue;

            fingerId = touch.fingerId;
            screenPosition = touch.position;
            return true;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            fingerId = -1;
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        fingerId = -1;
        screenPosition = Vector2.zero;
        return false;
    }

    private bool TryGetTrackedPointer(out Vector2 screenPosition, out bool released, out bool canceled)
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != trackingFingerId)
                continue;

            screenPosition = touch.position;
            released = touch.phase == TouchPhase.Ended;
            canceled = touch.phase == TouchPhase.Canceled;
            return true;
        }

        screenPosition = lastPointerScreen;
        released = false;
        canceled = true;
        return false;
#else
        screenPosition = Input.mousePosition;
        released = Input.GetMouseButtonUp(0);
        canceled = !released && !Input.GetMouseButton(0);
        return true;
#endif
    }

    private static bool IsGameplayPlaying()
    {
        PlayCtrl playCtrl = PlayCtrl.Instance;
        return playCtrl != null && playCtrl.IsInitialized && playCtrl.IsActive &&
               playCtrl.CheckState(GamePlayStateName.PLAYING);
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt == null)
            return;

        gameplayPaused = evt.NewState != GamePlayStateName.PLAYING;
        SetPresentationPaused(gameplayPaused);

        if (evt.NewState != GamePlayStateName.SUCCESS && evt.NewState != GamePlayStateName.FAILED)
            return;

        flowState = FlowState.Completed;
        ResetPointerState();
        StopFrictionFeedback(true);
        HideHints();
    }

    private void SetPresentationPaused(bool paused)
    {
        if (paused)
            DOTween.Pause(this);
        else
            DOTween.Play(this);

        for (int i = 0; i < matches.Count; i++)
        {
            if (matches[i].FireAnimator != null)
                matches[i].FireAnimator.speed = paused ? 0f : 1f;

            for (int j = 0; j < matches[i].SmokeSystems.Length; j++)
            {
                ParticleSystem smoke = matches[i].SmokeSystems[j];
                if (smoke == null)
                    continue;
                if (paused)
                    smoke.Pause(true);
                else if (smoke.isPaused)
                    smoke.Play(true);
            }
        }

        for (int i = frictionSparkRuntimes.Count - 1; i >= 0; i--)
        {
            FrictionSparkRuntime runtime = frictionSparkRuntimes[i];
            if (runtime == null || runtime.Object == null)
            {
                frictionSparkRuntimes.RemoveAt(i);
                continue;
            }

            for (int j = 0; j < runtime.Systems.Length; j++)
            {
                ParticleSystem particle = runtime.Systems[j];
                if (particle == null)
                    continue;
                if (paused)
                    particle.Pause(false);
                else if (particle.isPaused)
                    particle.Play(false);
            }
        }

        if (paused && frictionSound != null)
        {
            frictionSound.Stop();
            frictionSound = null;
        }
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
        flowState = FlowState.Completed;
        DOTween.Kill(this);
        dragRotationTween = null;
        dragRotationReady = false;
        StopFrictionFeedback(true);
        for (int i = 0; i < matches.Count; i++)
            StopFire(matches[i], false);
        HideHints();
        ResetPointerState();
        EventsUtils.ResetEvents(ref eventSubscriber);

        selectedMatchIndex = -1;
    }

    private void DestroyRuntimeObjects()
    {
        if (runtimeObjectsDestroyed)
            return;
        runtimeObjectsDestroyed = true;

        for (int i = frictionSparkRuntimes.Count - 1; i >= 0; i--)
        {
            FrictionSparkRuntime runtime = frictionSparkRuntimes[i];
            if (runtime != null && runtime.Object != null)
                Destroy(runtime.Object);
        }
        frictionSparkRuntimes.Clear();
        activeFrictionSpark = null;
        ResetFrictionTracking();

        if (failureExplosionInstance != null)
            Destroy(failureExplosionInstance);
        failureExplosionInstance = null;

        for (int i = 0; i < matches.Count; i++)
        {
            MatchRuntime match = matches[i];
            if (match.FireObject != null)
                Destroy(match.FireObject);
            if (match.SmokeObject != null)
                Destroy(match.SmokeObject);
            if (match.BurnedRenderer != null)
                Destroy(match.BurnedRenderer.gameObject);
        }

        for (int i = 0; i < staticHintRenderers.Count; i++)
        {
            if (staticHintRenderers[i] != null)
                Destroy(staticHintRenderers[i].gameObject);
        }

        if (sourceHintRenderer != null)
            Destroy(sourceHintRenderer.gameObject);
    }

    private void OnDisable()
    {
        BeginTeardown();
        DestroyRuntimeObjects();
    }

    private void OnDestroy()
    {
        BeginTeardown();
        DestroyRuntimeObjects();
    }
}

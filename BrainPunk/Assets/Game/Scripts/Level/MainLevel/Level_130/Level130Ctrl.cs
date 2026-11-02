using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Level 130 input routing and door-drop orchestration.
/// </summary>
[DisallowMultipleComponent]
public sealed class Level130Ctrl : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private Level130SpikeRow[] spikeRows;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Transform doorRoot;
    [SerializeField] private Collider2D doorTrigger;
    [SerializeField] private Level130StarDropOnPlatformBreak doorSupportStar;
    [SerializeField] private Level130SpikeRow doorLandingDisabledSpikeRow;
    [SerializeField] private ParticleSystem commonHitEffectPrefab;

    [Header("Door Drop")]
    [SerializeField] private float doorLandingY = -1.2f;
    [SerializeField, Min(0f)] private float doorLandingInset = 0.22716625f;
    [SerializeField] private LayerMask doorSupportLayers = 1 << 6;
    [SerializeField, Min(0.01f)] private float doorFallDuration = 0.8f;
    [SerializeField] private Ease doorFallEase = Ease.InQuart;

    private EventSubscriber eventSubscriber;
    private Tween doorFallTween;
    private Transform doorOriginalParent;
    private Level130BreakablePlatform currentDoorSupport;
    private float doorInitialY;
    private float doorBottomOffset;
    private float doorBoundsCenterOffsetX;
    private float doorBoundsWidth;
    private readonly Plane[] gameplayViewPlanes = new Plane[6];
    private bool isGameplayActive = true;
    private bool doorFallStarted;
    private bool doorIsFalling;
    private bool doorGeometryCached;
    private bool spikeTrapCommitted;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
        eventSubscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        eventSubscriber.Subscribe<GamePauseEvent>(OnGamePause);
        eventSubscriber.Subscribe<GameResumeEvent>(OnGameResume);

        bool playing = PlayCtrl.Instance == null || PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
        SetGameplayActive(playing);
    }

    private void FixedUpdate()
    {
        RemoveFallenSpikeRows();
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        KillDoorFallTween();
        SetSpikeRowsGameplayActive(false);
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        KillDoorFallTween();
    }

    public void NotifyPlatformBroken(Level130BreakablePlatform platform)
    {
        if (platform == null)
            return;

        if (platform.Role == Level130BreakablePlatform.PlatformRole.DoorSupport)
        {
            doorSupportStar?.NotifyPlatformBroken(platform);
            if (!doorFallStarted)
                BeginDoorFall();
            return;
        }

        if (platform == currentDoorSupport)
            BeginDoorFall();
    }

    public bool TryCommitSpikeTrap(Vector3 hitPosition)
    {
        if (spikeTrapCommitted)
            return false;

        spikeTrapCommitted = true;
        if (levelCtrl != null)
        {
            levelCtrl.IsActorEnterDoor = false;
            if (levelCtrl.Actor != null)
                PlayCommonHitEffect(hitPosition, levelCtrl.Actor.transform);
        }

        return true;
    }

    public void OnDoorActorTrigger2D(Collider2D collider)
    {
        ResolveReferences();
        if (spikeRows != null)
        {
            for (int i = 0; i < spikeRows.Length; i++)
            {
                Level130SpikeRow row = spikeRows[i];
                if (row != null && row.TryCommitActorTrapIfTouching(collider))
                    return;
            }
        }

        if (collider == null || levelCtrl == null || spikeTrapCommitted ||
            levelCtrl.Actor == null || levelCtrl.Actor.IsDied || levelCtrl.IsFail())
        {
            return;
        }

        levelCtrl.OnActorTrigger2D(collider);
    }

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        if (!isGameplayActive || levelCtrl == null || levelCtrl.Actor == null ||
            levelCtrl.Actor.IsDied || levelCtrl.IsSuccess())
        {
            return;
        }

        ResolveReferences();
        RemoveFallenSpikeRows();
        bool hasSynchronizedParticipant = false;
        for (int i = 0; i < spikeRows.Length; i++)
        {
            Level130SpikeRow row = spikeRows[i];
            if (row == null || !row.IsSynchronizedParticipant)
                continue;

            hasSynchronizedParticipant = true;
            if (!row.CanStartJump())
                return;
        }

        if (!hasSynchronizedParticipant || gameplayCamera == null || !gameplayCamera.isActiveAndEnabled)
            return;

        // Visibility filters the jump, not the all-active-rows grounded gate above.
        GeometryUtility.CalculateFrustumPlanes(gameplayCamera, gameplayViewPlanes);
        bool jumped = false;
        for (int i = 0; i < spikeRows.Length; i++)
        {
            Level130SpikeRow row = spikeRows[i];
            if (row == null || !row.IsSynchronizedParticipant ||
                !row.IsVisibleIn(gameplayCamera, gameplayViewPlanes))
                continue;

            row.StartJump(false);
            jumped = true;
        }

        if (jumped)
            AudioManager.PlaySound("jump");
    }

    private void RemoveFallenSpikeRows()
    {
        if (!isGameplayActive)
            return;

        if (gameplayCamera == null || spikeRows == null)
            ResolveReferences();
        if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled ||
            !gameplayCamera.orthographic || spikeRows == null)
        {
            return;
        }

        // This level uses an unrotated camera with fixed Y. Horizontal scrolling never removes a row.
        float cameraBottomY = gameplayCamera.transform.position.y - gameplayCamera.orthographicSize;
        for (int i = 0; i < spikeRows.Length; i++)
            spikeRows[i]?.TryRemoveIfFallenBelow(cameraBottomY);
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
        SetGameplayActive(true);
    }

    private void SetGameplayActive(bool active)
    {
        isGameplayActive = active;
        SetSpikeRowsGameplayActive(active);

        if (doorFallTween == null || !doorFallTween.IsActive())
            return;

        if (active)
            doorFallTween.Play();
        else
            doorFallTween.Pause();
    }

    private void BeginDoorFall()
    {
        ResolveReferences();
        if (doorRoot == null)
            return;

        doorFallStarted = true;
        DetachDoorFromSupport();
        KillDoorFallTween();

        float startY = doorRoot.position.y;
        if (startY <= doorLandingY)
        {
            SetDoorWorldY(doorLandingY);
            CompleteDoorLanding(null);
            return;
        }

        float referenceDistance = Mathf.Max(0.01f, Mathf.Abs(doorInitialY - doorLandingY));
        float referenceSpeed = referenceDistance / Mathf.Max(0.01f, doorFallDuration);
        float duration = Mathf.Max(0.01f, (startY - doorLandingY) / referenceSpeed);
        doorIsFalling = true;

        doorFallTween = DOVirtual
            .Float(startY, doorLandingY, duration, ApplyDoorFallPosition)
            .SetEase(doorFallEase)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                // The final update can already have landed on a platform. Never
                // replace that support with the fallback parent on completion.
                if (!doorIsFalling)
                    return;

                ApplyDoorFallPosition(doorLandingY);
                if (!doorIsFalling)
                    return;

                doorFallTween = null;
                doorIsFalling = false;
                SetDoorWorldY(doorLandingY);
                CompleteDoorLanding(null);
            });

        if (!isGameplayActive)
            doorFallTween.Pause();
    }

    private void ApplyDoorFallPosition(float requestedY)
    {
        if (!doorIsFalling || doorRoot == null)
            return;

        float currentY = doorRoot.position.y;
        float nextY = Mathf.Min(currentY, requestedY);
        if (!TryResolveDoorSupportLanding(
                currentY,
                nextY,
                out float landingY,
                out Level130BreakablePlatform support,
                out Transform supportRoot))
        {
            SetDoorWorldY(nextY);
            return;
        }

        Tween activeTween = doorFallTween;
        doorFallTween = null;
        doorIsFalling = false;
        if (activeTween != null && activeTween.IsActive())
            activeTween.Kill(false);

        SetDoorWorldY(landingY);
        CompleteDoorLanding(support, supportRoot);
    }

    private bool TryResolveDoorSupportLanding(
        float currentY,
        float nextY,
        out float landingY,
        out Level130BreakablePlatform support,
        out Transform supportRoot)
    {
        landingY = 0f;
        support = null;
        supportRoot = null;
        if (doorTrigger == null)
            return false;

        const float overlapEpsilon = 0.0001f;
        float dropDistance = Mathf.Max(0f, currentY - nextY);
        float doorCenterX = doorRoot.position.x + doorBoundsCenterOffsetX;
        float doorMinX = doorCenterX - doorBoundsWidth * 0.5f;
        float doorMaxX = doorCenterX + doorBoundsWidth * 0.5f;
        Vector2 probeSize = new Vector2(Mathf.Max(0.01f, doorBoundsWidth), 0.02f);
        float safeInset = Mathf.Max(0f, doorLandingInset);
        // Collider bounds can still describe the previous physics pose after a
        // Transform tween update. Sweep from our current logical door position.
        Vector2 probeOrigin = new Vector2(
            doorCenterX,
            currentY + doorBottomOffset + safeInset + probeSize.y * 0.5f);
        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            probeOrigin,
            probeSize,
            0f,
            Vector2.down,
            dropDistance + probeSize.y + overlapEpsilon,
            doorSupportLayers);

        bool found = false;
        float highestLandingY = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D supportCollider = hits[i].collider;
            if (supportCollider == null || !supportCollider.enabled || supportCollider.isTrigger)
                continue;

            Transform supportTransform = supportCollider.transform;
            if (supportTransform == doorRoot || supportTransform.IsChildOf(doorRoot))
                continue;

            Level130BreakablePlatform platform =
                supportCollider.GetComponentInParent<Level130BreakablePlatform>();
            if (platform != null &&
                (platform.IsBroken || platform.Role != Level130BreakablePlatform.PlatformRole.Movable))
            {
                continue;
            }

            Bounds supportBounds = supportCollider.bounds;
            bool overlapsHorizontally =
                doorMaxX >= supportBounds.min.x - overlapEpsilon &&
                doorMinX <= supportBounds.max.x + overlapEpsilon;
            if (!overlapsHorizontally)
                continue;

            float candidateLandingY =
                supportBounds.max.y - doorBottomOffset - safeInset;
            if (candidateLandingY > currentY + overlapEpsilon ||
                candidateLandingY < nextY - overlapEpsilon ||
                candidateLandingY <= highestLandingY)
            {
                continue;
            }

            found = true;
            highestLandingY = candidateLandingY;
            support = platform;
            Rigidbody2D supportBody = supportCollider.attachedRigidbody;
            supportRoot = platform != null
                ? platform.transform
                : (supportBody != null && supportBody.bodyType == RigidbodyType2D.Kinematic
                    ? supportBody.transform
                    : null);
        }

        if (!found)
            return false;

        landingY = highestLandingY;
        return true;
    }

    private void CompleteDoorLanding(
        Level130BreakablePlatform support,
        Transform supportRoot = null)
    {
        currentDoorSupport = support;
        if (doorRoot == null)
            return;

        Transform targetParent = supportRoot != null && (support == null || !support.IsBroken)
            ? supportRoot
            : doorOriginalParent;
        if (targetParent != null && doorRoot.parent != targetParent)
            doorRoot.SetParent(targetParent, true);

        doorLandingDisabledSpikeRow?.DisableCollisionAfterDoorLanding();
    }

    private void DetachDoorFromSupport()
    {
        currentDoorSupport = null;
        if (doorRoot != null && doorOriginalParent != null && doorRoot.parent != doorOriginalParent)
            doorRoot.SetParent(doorOriginalParent, true);
    }

    private void SetDoorWorldY(float worldY)
    {
        if (doorRoot == null)
            return;

        Vector3 position = doorRoot.position;
        position.y = worldY;
        doorRoot.position = position;
    }

    private void PlayCommonHitEffect(Vector3 worldPosition, Transform targetRoot)
    {
        if (commonHitEffectPrefab == null)
            return;

        GetTopSortingSettings(targetRoot, 0, 0, out int sortingLayerId, out int sortingOrder);
        int topLayerValue = SortingLayer.GetLayerValueFromID(sortingLayerId);
        for (int i = 0; spikeRows != null && i < spikeRows.Length; i++)
        {
            Level130SpikeRow row = spikeRows[i];
            if (row != null && row.gameObject.activeInHierarchy)
            {
                IncludeTopSortingSettings(
                    row.transform,
                    ref sortingLayerId,
                    ref sortingOrder,
                    ref topLayerValue);
            }
        }

        ParticleSystem instance = Instantiate(
            commonHitEffectPrefab,
            worldPosition,
            commonHitEffectPrefab.transform.rotation,
            transform);
        instance.name = "Level130PlayerHitEffect";

        ParticleSystemRenderer[] sourceRenderers =
            commonHitEffectPrefab.GetComponentsInChildren<ParticleSystemRenderer>(true);
        int minimumSourceSortingOrder = 0;
        if (sourceRenderers.Length > 0)
        {
            minimumSourceSortingOrder = sourceRenderers[0].sortingOrder;
            for (int i = 1; i < sourceRenderers.Length; i++)
            {
                minimumSourceSortingOrder = Mathf.Min(
                    minimumSourceSortingOrder,
                    sourceRenderers[i].sortingOrder);
            }
        }

        Transform knockText = instance.transform.Find("Knock 1");
        if (knockText != null)
            knockText.gameObject.SetActive(false);

        foreach (ParticleSystemRenderer particleRenderer in
                 instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (particleRenderer == null)
                continue;

            particleRenderer.sortingLayerID = sortingLayerId;
            particleRenderer.sortingOrder = sortingOrder + 1 +
                particleRenderer.sortingOrder - minimumSourceSortingOrder;
        }

        instance.gameObject.SetActive(true);
        instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        instance.Play(true);
        Destroy(instance.gameObject, 3f);
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
        for (int i = 0; i < sortingGroups.Length; i++)
        {
            SortingGroup group = sortingGroups[i];
            if (group != null)
                IncludeSortingCandidate(
                    group.sortingLayerID,
                    group.sortingOrder,
                    ref sortingLayerId,
                    ref sortingOrder,
                    ref topLayerValue);
        }

        Renderer[] renderers = targetRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null)
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

    private static void IncludeTopSortingSettings(
        Transform targetRoot,
        ref int sortingLayerId,
        ref int sortingOrder,
        ref int topLayerValue)
    {
        if (targetRoot == null)
            return;

        SortingGroup[] sortingGroups = targetRoot.GetComponentsInChildren<SortingGroup>(true);
        for (int i = 0; i < sortingGroups.Length; i++)
        {
            SortingGroup group = sortingGroups[i];
            if (group != null)
            {
                IncludeSortingCandidate(
                    group.sortingLayerID,
                    group.sortingOrder,
                    ref sortingLayerId,
                    ref sortingOrder,
                    ref topLayerValue);
            }
        }

        Renderer[] renderers = targetRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null)
            {
                IncludeSortingCandidate(
                    renderer.sortingLayerID,
                    renderer.sortingOrder,
                    ref sortingLayerId,
                    ref sortingOrder,
                    ref topLayerValue);
            }
        }
    }

    private void ResolveReferences()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponent<BaseLevelCtrl>();
        if (spikeRows == null || spikeRows.Length == 0)
            spikeRows = GetComponentsInChildren<Level130SpikeRow>(true);
        if (gameplayCamera == null)
        {
            Transform cameraRoot = transform.Find("GameCamera");
            if (cameraRoot != null)
                gameplayCamera = cameraRoot.GetComponent<Camera>();
        }
        if (doorRoot == null)
            doorRoot = transform.Find("door_0");
        if (doorTrigger == null && doorRoot != null)
            doorTrigger = doorRoot.GetComponentInChildren<Collider2D>(true);

        if (!doorGeometryCached && doorRoot != null && doorTrigger != null)
        {
            doorOriginalParent = doorRoot.parent;
            doorInitialY = doorRoot.position.y;
            Bounds bounds = doorTrigger.bounds;
            doorBottomOffset = bounds.min.y - doorRoot.position.y;
            doorBoundsCenterOffsetX = bounds.center.x - doorRoot.position.x;
            doorBoundsWidth = bounds.size.x;
            doorGeometryCached = true;
        }
    }

    private void SetSpikeRowsGameplayActive(bool active)
    {
        if (spikeRows == null)
            return;

        for (int i = 0; i < spikeRows.Length; i++)
            spikeRows[i]?.SetGameplayActive(active);
    }

    private void KillDoorFallTween()
    {
        if (doorFallTween != null && doorFallTween.IsActive())
            doorFallTween.Kill();
        doorFallTween = null;
        doorIsFalling = false;
    }
}

using Bear.EventSystem;
using Game.Events;
using Game.Level;
using Game.Play;
using UnityEngine;

/// <summary>
/// Level 130 automatic horizontal stone platform. It carries a standing Actor,
/// can continuously drive an authored spike row, and carries any configured
/// spike rows that are actually standing on it. Landed stars receive the same
/// physics-step horizontal destination as the platform.
/// </summary>
[DisallowMultipleComponent]
public sealed class Level130AutomaticPlatform : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform movingTarget;
    [SerializeField] private Rigidbody2D movingBody;
    [SerializeField] private Collider2D[] platformColliders;
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private Level130SpikeRow synchronizedSpikeRow;
    [SerializeField] private Level130SpikeRow[] supportedSpikeRows;

    [Header("Visibility Start")]
    [SerializeField] private bool startWhenVisible;
    [SerializeField] private Camera gameplayCamera;

    [Header("Horizontal Motion")]
    [SerializeField] private float minimumLocalX = -0.413f;
    [SerializeField] private float maximumLocalX = 3.862f;
    [SerializeField, Min(0.01f)] private float moveSpeed = 1.2f;
    [SerializeField, Min(0f)] private float waitAtEndSeconds = 0.8f;
    [SerializeField, Min(0.01f)] private float standingTolerance = 0.25f;

    private EventSubscriber eventSubscriber;
    private bool movingRight = true;
    private bool isGameplayActive = true;
    private float waitUntil;
    private SpriteRenderer[] platformVisuals;
    private bool hasEnteredView;
    private readonly Plane[] gameplayViewPlanes = new Plane[6];

    // Kinematic passengers must receive the same physics-step destination as the
    // platform; parenting a separate Rigidbody2D alone is not a movement driver.
    public event System.Action<float> HorizontalPositionRequested;
    public float WorldX => movingBody != null ? movingBody.position.x : transform.position.x;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        eventSubscriber.Subscribe<GamePauseEvent>(OnGamePause);
        eventSubscriber.Subscribe<GameResumeEvent>(OnGameResume);
        ResolveReferences();
        isGameplayActive = PlayCtrl.Instance == null || PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
        movingRight = true;
        waitUntil = 0f;
    }

    private void FixedUpdate()
    {
        ResolveReferences();
        if (!isGameplayActive || movingTarget == null || movingBody == null || Time.timeScale <= 0f)
        {
            StopMotion();
            return;
        }

        // Latch once per instance, not once per visit to the camera view.
        // Pause/resume and subsequent horizontal scrolling must not re-arm it.
        if (startWhenVisible && !hasEnteredView)
        {
            if (!IsVisibleInGameplayCamera())
            {
                StopMotion();
                return;
            }

            hasEnteredView = true;
        }

        if (Time.fixedTime < waitUntil)
        {
            StopMotion();
            return;
        }

        float minimum = Mathf.Min(minimumLocalX, maximumLocalX);
        float maximum = Mathf.Max(minimumLocalX, maximumLocalX);
        Vector3 currentLocal = GetCurrentLocalPosition();
        float destinationX = movingRight ? maximum : minimum;
        float nextX = Mathf.MoveTowards(
            currentLocal.x,
            destinationX,
            Mathf.Max(0.01f, moveSpeed) * Mathf.Max(Time.fixedDeltaTime, 0.0001f));
        Vector3 nextLocal = new Vector3(nextX, currentLocal.y, currentLocal.z);
        Vector3 nextWorld = movingTarget.parent != null
            ? movingTarget.parent.TransformPoint(nextLocal)
            : nextLocal;
        Vector2 movementDelta = (Vector2)nextWorld - movingBody.position;
        movementDelta.y = 0f;
        float fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float maximumDistance = Mathf.Max(0.01f, Physics2D.maxTranslationSpeed) * fixedDeltaTime;
        movementDelta = Vector2.ClampMagnitude(movementDelta, maximumDistance);

        Collider2D actorCollider = actor != null ? actor.GetComponent<Collider2D>() : null;
        Rigidbody2D actorBody = actor != null
            ? (actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>())
            : null;
        bool carriesActor = IsSupported(actorCollider);

        Vector2 nextBodyPosition = movingBody.position + movementDelta;
        movingBody.MovePosition(nextBodyPosition);
        HorizontalPositionRequested?.Invoke(nextBodyPosition.x);
        float velocityX = movementDelta.x / fixedDeltaTime;
        if (carriesActor && actorBody != null && actorBody.simulated && actor != null && !actor.IsDied)
        {
            actorBody.linearVelocity = new Vector2(
                velocityX + actor.XInput * actor.MoveSpeed,
                actorBody.linearVelocityY);
        }

        ApplySpikeVelocities(velocityX);
        if (Mathf.Abs(nextX - destinationX) <= 0.0001f)
        {
            movingRight = !movingRight;
            waitUntil = Time.fixedTime + Mathf.Max(0f, waitAtEndSeconds);
        }
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        StopMotion();
    }

    public bool OwnsPlatformCollider(Collider2D candidate)
    {
        ResolveReferences();
        if (candidate == null || platformColliders == null)
            return false;

        for (int i = 0; i < platformColliders.Length; i++)
        {
            if (platformColliders[i] == candidate)
                return true;
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (movingTarget == null)
            movingTarget = transform;
        if (movingBody == null && movingTarget != null)
            movingBody = movingTarget.GetComponent<Rigidbody2D>();
        if ((platformColliders == null || platformColliders.Length == 0) && movingTarget != null)
            platformColliders = movingTarget.GetComponentsInChildren<Collider2D>(true);
        if (platformVisuals == null && movingTarget != null)
            platformVisuals = movingTarget.GetComponentsInChildren<SpriteRenderer>(true);
        if (actor == null || (startWhenVisible && gameplayCamera == null))
        {
            BaseLevelCtrl owner = GetComponentInParent<BaseLevelCtrl>();
            if (owner != null)
            {
                if (actor == null)
                    actor = owner.Actor;
                if (startWhenVisible && gameplayCamera == null)
                {
                    Transform cameraRoot = owner.transform.Find("GameCamera");
                    if (cameraRoot != null)
                        gameplayCamera = cameraRoot.GetComponent<Camera>();
                }
            }
        }
    }

    private bool IsVisibleInGameplayCamera()
    {
        if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled)
            return false;

        GeometryUtility.CalculateFrustumPlanes(gameplayCamera, gameplayViewPlanes);
        // Only the platform's authored visuals count; a door parented here after
        // landing must not become a visibility trigger for the platform.
        for (int i = 0; platformVisuals != null && i < platformVisuals.Length; i++)
        {
            SpriteRenderer visual = platformVisuals[i];
            if (visual == null || !visual.enabled || !visual.gameObject.activeInHierarchy ||
                visual.sprite == null || visual.color.a <= 0f ||
                (gameplayCamera.cullingMask & (1 << visual.gameObject.layer)) == 0)
            {
                continue;
            }

            if (GeometryUtility.TestPlanesAABB(gameplayViewPlanes, visual.bounds))
                return true;
        }

        return false;
    }

    private Vector3 GetCurrentLocalPosition()
    {
        Vector3 worldPosition = movingTarget.position;
        worldPosition.x = movingBody.position.x;
        worldPosition.y = movingBody.position.y;
        return movingTarget.parent != null
            ? movingTarget.parent.InverseTransformPoint(worldPosition)
            : worldPosition;
    }

    private bool IsSupported(Collider2D riderCollider)
    {
        return TryGetSupportingCollider(riderCollider, out _);
    }

    public bool TryGetStandingVisualTop(Collider2D riderCollider, out float worldY)
    {
        worldY = 0f;
        if (!TryGetSupportingCollider(riderCollider, out Collider2D surface))
            return false;

        worldY = surface.bounds.max.y;
        float visualTop = float.NegativeInfinity;
        Bounds riderBounds = riderCollider.bounds;
        for (int i = 0; platformVisuals != null && i < platformVisuals.Length; i++)
        {
            SpriteRenderer visual = platformVisuals[i];
            if (visual == null || !visual.enabled || !visual.gameObject.activeInHierarchy)
                continue;

            Bounds bounds = visual.bounds;
            if (bounds.max.x >= riderBounds.min.x && bounds.min.x <= riderBounds.max.x)
                visualTop = Mathf.Max(visualTop, bounds.max.y);
        }

        if (!float.IsNegativeInfinity(visualTop))
            worldY = visualTop;
        return true;
    }

    private bool TryGetSupportingCollider(Collider2D riderCollider, out Collider2D surface)
    {
        surface = null;
        if (riderCollider == null || !riderCollider.enabled || platformColliders == null)
            return false;

        Bounds riderBounds = riderCollider.bounds;
        float tolerance = Mathf.Max(0.01f, standingTolerance);
        for (int i = 0; i < platformColliders.Length; i++)
        {
            Collider2D platformCollider = platformColliders[i];
            if (platformCollider == null || !platformCollider.enabled || platformCollider.isTrigger)
                continue;

            Bounds platformBounds = platformCollider.bounds;
            bool horizontalOverlap = riderBounds.max.x >= platformBounds.min.x &&
                                     riderBounds.min.x <= platformBounds.max.x;
            float verticalGap = riderBounds.min.y - platformBounds.max.y;
            if (!horizontalOverlap || riderBounds.center.y < platformBounds.center.y ||
                verticalGap < -tolerance || verticalGap > tolerance)
            {
                continue;
            }

            if (riderCollider.IsTouching(platformCollider))
            {
                surface = platformCollider;
                return true;
            }

            ColliderDistance2D distance = riderCollider.Distance(platformCollider);
            float contactDistance = Mathf.Max(0.02f, Physics2D.defaultContactOffset * 2f);
            if (distance.isValid && distance.distance <= contactDistance)
            {
                surface = platformCollider;
                return true;
            }
        }

        return false;
    }

    private void ApplySpikeVelocities(float velocityX)
    {
        ApplySpikeVelocity(synchronizedSpikeRow, velocityX, false);

        for (int i = 0; supportedSpikeRows != null && i < supportedSpikeRows.Length; i++)
        {
            Level130SpikeRow row = supportedSpikeRows[i];
            if (row == synchronizedSpikeRow)
                continue;

            ApplySpikeVelocity(row, velocityX, true);
        }
    }

    private void ApplySpikeVelocity(
        Level130SpikeRow row,
        float velocityX,
        bool requirePlatformSupport)
    {
        if (row == null || !row.IsSynchronizedParticipant ||
            (requirePlatformSupport && (row.IsAscending || !IsSupported(row.SupportCollider))))
        {
            return;
        }

        Rigidbody2D spikeBody = row.Body;
        if (spikeBody != null && spikeBody.simulated)
            spikeBody.linearVelocity = new Vector2(velocityX, spikeBody.linearVelocityY);
    }

    private void StopMotion()
    {
        if (movingBody != null)
        {
            movingBody.linearVelocity = Vector2.zero;
            movingBody.angularVelocity = 0f;
        }

        HorizontalPositionRequested?.Invoke(WorldX);

        Collider2D actorCollider = actor != null ? actor.GetComponent<Collider2D>() : null;
        Rigidbody2D actorBody = actor != null
            ? (actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>())
            : null;
        if (IsSupported(actorCollider) && actorBody != null && actorBody.simulated && actor != null && !actor.IsDied)
            actorBody.linearVelocity = new Vector2(actor.XInput * actor.MoveSpeed, actorBody.linearVelocityY);

        ApplySpikeVelocities(0f);
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        isGameplayActive = evt.NewState.Equals(GamePlayStateName.PLAYING);
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        isGameplayActive = false;
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        isGameplayActive = true;
    }
}

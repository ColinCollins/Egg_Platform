using Bear.EventSystem;
using Game.Events;
using Game.Play;
using UnityEngine;

/// <summary>
/// Horizontal-only Kinematic platform drag for Level 130. The platform is the
/// movement authority and composes its velocity into supported Actor/spike bodies.
/// </summary>
[DisallowMultipleComponent]
public sealed class Level130HorizontalPlatformDrag : MonoBehaviour
{
    private const int MousePointerId = -1;

    [Header("References")]
    [SerializeField] private Transform movingTarget;
    [SerializeField] private Rigidbody2D movingBody;
    [SerializeField] private Collider2D[] dragColliders;
    [SerializeField] private Camera inputCamera;
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private Level130SpikeRow[] spikeRows;

    [Header("Horizontal Range")]
    [SerializeField] private float minimumLocalX = -0.1f;
    [SerializeField] private float maximumLocalX = 7f;
    [SerializeField] private bool clampPositionOnEnable;

    [Header("Rider Detection")]
    [SerializeField, Min(0.01f)] private float standingTolerance = 0.25f;

    private EventSubscriber eventSubscriber;
    private int activePointerId = MousePointerId;
    private float pointerToTargetLocalXOffset;
    private float lockedLocalY;
    private float lockedLocalZ;
    private Vector3 desiredLocalPosition;
    private bool hasDesiredPosition;
    private bool isDragging;
    private bool isGameplayActive = true;

    private void Awake()
    {
        ResolveReferences();
        SyncDesiredPosition();
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
        if (clampPositionOnEnable && movingTarget != null)
        {
            Vector3 position = movingTarget.localPosition;
            position.x = ClampLocalX(position.x);
            movingTarget.localPosition = position;
            if (movingBody != null)
                movingBody.position = movingTarget.position;
        }

        SyncDesiredPosition();
        bool playing = PlayCtrl.Instance == null || PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
        SetGameplayActive(playing);
    }

    private void Update()
    {
        if (!isGameplayActive || movingTarget == null || inputCamera == null || Time.timeScale <= 0f)
        {
            FinishDrag();
            return;
        }

        if (Input.touchCount > 0)
        {
            HandleTouchInput();
            return;
        }

        HandleMouseInput();
    }

    private void FixedUpdate()
    {
        if (!isGameplayActive || !hasDesiredPosition || movingTarget == null || movingBody == null)
            return;

        Vector3 targetWorld = movingTarget.parent != null
            ? movingTarget.parent.TransformPoint(desiredLocalPosition)
            : desiredLocalPosition;
        Vector2 previousPosition = movingBody.position;
        Vector2 movementDelta = (Vector2)targetWorld - previousPosition;
        movementDelta.y = 0f;

        float fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float maxDistance = Mathf.Max(0.01f, Physics2D.maxTranslationSpeed) * fixedDeltaTime;
        movementDelta = Vector2.ClampMagnitude(movementDelta, maxDistance);

        Collider2D actorCollider = actor != null ? actor.GetComponent<Collider2D>() : null;
        Rigidbody2D actorBody = actor != null
            ? (actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>())
            : null;
        bool carriesActor = IsSupported(actorCollider);

        if (movementDelta.sqrMagnitude > 0.0000001f)
            movingBody.MovePosition(previousPosition + movementDelta);

        float platformVelocityX = movementDelta.x / fixedDeltaTime;
        if (carriesActor && actorBody != null && actorBody.simulated && actor != null && !actor.IsDied)
        {
            float actorVelocityX = actor.XInput * actor.MoveSpeed;
            actorBody.linearVelocity = new Vector2(platformVelocityX + actorVelocityX, actorBody.linearVelocityY);
        }

        for (int i = 0; spikeRows != null && i < spikeRows.Length; i++)
        {
            Level130SpikeRow row = spikeRows[i];
            Rigidbody2D spikeBody = row != null ? row.Body : null;
            if (row != null && row.IsSynchronizedParticipant && spikeBody != null && spikeBody.simulated &&
                !row.IsAscending && IsSupported(row.SupportCollider))
            {
                spikeBody.linearVelocity = new Vector2(platformVelocityX, spikeBody.linearVelocityY);
            }
        }
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        FinishDrag();
    }

    public bool OwnsPlatformCollider(Collider2D candidate)
    {
        ResolveReferences();
        if (candidate == null || dragColliders == null)
            return false;

        for (int i = 0; i < dragColliders.Length; i++)
        {
            if (dragColliders[i] == candidate)
                return true;
        }

        return false;
    }

    public void PrepareForPlatformBreak()
    {
        ResolveReferences();

        Collider2D actorCollider = actor != null ? actor.GetComponent<Collider2D>() : null;
        Rigidbody2D actorBody = actor != null
            ? (actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>())
            : null;
        if (IsSupported(actorCollider) && actorBody != null && actorBody.simulated)
            actorBody.linearVelocity = new Vector2(0f, actorBody.linearVelocityY);

        for (int i = 0; spikeRows != null && i < spikeRows.Length; i++)
        {
            Level130SpikeRow row = spikeRows[i];
            if (row != null && IsSupported(row.SupportCollider))
                row.StopHorizontalMotion();
        }

        FinishDrag();
        isGameplayActive = false;
        if (movingBody != null)
            movingBody.linearVelocity = Vector2.zero;
        enabled = false;
    }

    private void HandleTouchInput()
    {
        if (!isDragging)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began && TryBeginDrag(touch.position, touch.fingerId))
                    return;
            }

            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != activePointerId)
                continue;

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                FinishDrag();
            else
                ContinueDrag(touch.position);
            return;
        }

        FinishDrag();
    }

    private void HandleMouseInput()
    {
        if (!isDragging)
        {
            if (Input.GetMouseButtonDown(0))
                TryBeginDrag(Input.mousePosition, MousePointerId);
            return;
        }

        if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
        {
            FinishDrag();
            return;
        }

        ContinueDrag(Input.mousePosition);
    }

    private bool TryBeginDrag(Vector2 screenPosition, int pointerId)
    {
        if (!TryGetWorldPoint(screenPosition, out Vector3 worldPoint) || !IsPointOnPlatform(worldPoint))
            return false;

        Vector3 pointerLocal = WorldToParentLocal(worldPoint);
        Vector3 targetLocal = GetCurrentLocalPosition();
        pointerToTargetLocalXOffset = targetLocal.x - pointerLocal.x;
        lockedLocalY = targetLocal.y;
        lockedLocalZ = targetLocal.z;
        activePointerId = pointerId;
        isDragging = true;
        return true;
    }

    private void ContinueDrag(Vector2 screenPosition)
    {
        if (!TryGetWorldPoint(screenPosition, out Vector3 worldPoint))
            return;

        Vector3 pointerLocal = WorldToParentLocal(worldPoint);
        desiredLocalPosition = new Vector3(
            ClampLocalX(pointerLocal.x + pointerToTargetLocalXOffset),
            lockedLocalY,
            lockedLocalZ);
        hasDesiredPosition = true;
    }

    private void FinishDrag()
    {
        isDragging = false;
        activePointerId = MousePointerId;
    }

    private bool IsPointOnPlatform(Vector3 worldPoint)
    {
        if (dragColliders == null)
            return false;

        for (int i = 0; i < dragColliders.Length; i++)
        {
            Collider2D candidate = dragColliders[i];
            if (candidate != null && candidate.enabled && candidate.OverlapPoint(worldPoint))
                return true;
        }

        return false;
    }

    private bool IsSupported(Collider2D riderCollider)
    {
        if (riderCollider == null || !riderCollider.enabled || dragColliders == null)
            return false;

        Bounds riderBounds = riderCollider.bounds;
        float tolerance = Mathf.Max(0.01f, standingTolerance);
        for (int i = 0; i < dragColliders.Length; i++)
        {
            Collider2D platformCollider = dragColliders[i];
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
                return true;

            ColliderDistance2D distance = riderCollider.Distance(platformCollider);
            float contactDistance = Mathf.Max(0.02f, Physics2D.defaultContactOffset * 2f);
            if (distance.isValid && distance.distance <= contactDistance)
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
        if (dragColliders == null || dragColliders.Length == 0)
            dragColliders = movingTarget.GetComponentsInChildren<Collider2D>(true);
        if (inputCamera == null)
        {
            inputCamera = Camera.main;
            if (inputCamera == null)
                inputCamera = FindFirstObjectByType<Camera>();
        }
        if (actor == null)
        {
            Game.Level.BaseLevelCtrl baseLevel = GetComponentInParent<Game.Level.BaseLevelCtrl>();
            if (baseLevel != null)
                actor = baseLevel.Actor;
        }
        if (spikeRows == null || spikeRows.Length == 0)
        {
            Level130Ctrl owner = GetComponentInParent<Level130Ctrl>();
            if (owner != null)
                spikeRows = owner.GetComponentsInChildren<Level130SpikeRow>(true);
        }
    }

    private void SyncDesiredPosition()
    {
        if (movingTarget == null)
            return;

        desiredLocalPosition = GetCurrentLocalPosition();
        hasDesiredPosition = true;
    }

    private Vector3 GetCurrentLocalPosition()
    {
        if (movingBody == null)
            return movingTarget.localPosition;

        Vector3 worldPosition = movingTarget.position;
        worldPosition.x = movingBody.position.x;
        worldPosition.y = movingBody.position.y;
        return movingTarget.parent != null
            ? movingTarget.parent.InverseTransformPoint(worldPosition)
            : worldPosition;
    }

    private bool TryGetWorldPoint(Vector2 screenPosition, out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        if (inputCamera == null || movingTarget == null)
            return false;

        float zDistance = Mathf.Abs(inputCamera.transform.position.z - movingTarget.position.z);
        worldPoint = inputCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, zDistance));
        return true;
    }

    private Vector3 WorldToParentLocal(Vector3 worldPoint)
    {
        return movingTarget.parent != null
            ? movingTarget.parent.InverseTransformPoint(worldPoint)
            : worldPoint;
    }

    private float ClampLocalX(float value)
    {
        return Mathf.Clamp(value, Mathf.Min(minimumLocalX, maximumLocalX), Mathf.Max(minimumLocalX, maximumLocalX));
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
        if (!active)
            FinishDrag();
    }

    private void OnDrawGizmosSelected()
    {
        Transform target = movingTarget != null ? movingTarget : transform;
        Transform parent = target.parent;
        float minX = Mathf.Min(minimumLocalX, maximumLocalX);
        float maxX = Mathf.Max(minimumLocalX, maximumLocalX);
        Vector3 localPosition = target.localPosition;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = parent != null ? parent.localToWorldMatrix : Matrix4x4.identity;
        Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.9f);
        Gizmos.DrawLine(
            new Vector3(minX, localPosition.y, localPosition.z),
            new Vector3(maxX, localPosition.y, localPosition.z));
        Gizmos.DrawWireSphere(new Vector3(minX, localPosition.y, localPosition.z), 0.1f);
        Gizmos.DrawWireSphere(new Vector3(maxX, localPosition.y, localPosition.z), 0.1f);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}

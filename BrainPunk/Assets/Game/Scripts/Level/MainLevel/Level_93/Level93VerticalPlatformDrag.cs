using Bear.EventSystem;
using Game.Events;
using Game.Level;
using Game.Play;
using UnityEngine;

/// <summary>
/// Level093 轨道平台专用的纵向拖拽组件。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level93VerticalPlatformDrag : MonoBehaviour
{
    #region Inspector 配置

    /// <summary>
    /// 实际移动的平台；为空时移动当前组件所在物体。
    /// </summary>
    [SerializeField] private Transform movingTarget;

    /// <summary>
    /// 可响应按下的碰撞体；为空时在 movingTarget 子级中自动查找，移动端会逐个精确命中。
    /// </summary>
    [SerializeField] private Collider2D[] dragColliders;

    /// <summary>
    /// 输入坐标换算使用的相机；为空时仅在运行时查找主相机。
    /// </summary>
    [SerializeField] private Camera inputCamera;

    /// <summary>
    /// 平台中心允许到达的最小本地 Y；与初始位置是否居中无关。
    /// </summary>
    [SerializeField] private float minimumLocalY = -1.2f;

    /// <summary>
    /// 平台中心允许到达的最大本地 Y；小于 minimumLocalY 时运行时会自动交换上下限。
    /// </summary>
    [SerializeField] private float maximumLocalY = 0.9f;

    /// <summary>
    /// 启用组件时是否立即把初始位置限制到区间内；关闭时只在真正拖拽后限制。
    /// </summary>
    [SerializeField] private bool clampPositionOnEnable;

    /// <summary>
    /// Scene 视图中拖拽范围框的宽度，仅影响 Gizmo 显示。
    /// </summary>
    [SerializeField, Min(0.01f)] private float gizmoWidth = 0.35f;

    /// <summary>
    /// Scene 视图中拖拽范围的颜色，仅影响 Gizmo 显示。
    /// </summary>
    [SerializeField] private Color gizmoColor = new Color(0.1f, 0.85f, 1f, 0.9f);

    /// <summary>
    /// 角色站在平台上时是否随拖拽平台一起移动，关闭后仅移动平台自身。
    /// </summary>
    [SerializeField] private bool carryStandingActor = true;

    /// <summary>
    /// 判定角色仍站在平台表面时允许的竖直误差，单位为世界坐标。
    /// </summary>
    [SerializeField, Min(0.01f)] private float riderStandingTolerance = 0.25f;

    #endregion

    #region 运行时状态

    private const int MousePointerId = -1;

    private int activePointerId = MousePointerId;
    private float pointerToTargetLocalYOffset;
    private float lockedLocalX;
    private float lockedLocalZ;
    private Vector3 desiredLocalPosition;
    private bool isDragging;
    private bool hasDesiredPosition;
    private bool isRiderAttached;
    private float riderSuppressedUntil;
    private bool riderNeedsSettle;
    private Vector2 lastAppliedPlatformVelocity;
    private bool pendingJumpRelease;
    private float velocityBeforeJumpRequest;
    private BaseLevelCtrl levelCtrl;
    private ActorCtrl actor;
    private Collider2D actorCollider;
    private Rigidbody2D actorBody;
    private Rigidbody2D movingBody;
    private EventSubscriber eventSubscriber;
    private bool isGameplayActive = true;
    private bool hasLoggedMissingMovingBody;

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        ResolveRuntimeReferences();
        SyncDesiredPosition();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        eventSubscriber.Subscribe<GamePauseEvent>(OnGamePause);
        eventSubscriber.Subscribe<GameResumeEvent>(OnGameResume);
        eventSubscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
        SetGameplayActive(PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING));
        ResolveRuntimeReferences();
        FinishDrag();

        if (clampPositionOnEnable && movingTarget != null)
        {
            Vector3 localPosition = movingTarget.localPosition;
            localPosition.y = ClampLocalY(localPosition.y);
            movingTarget.localPosition = localPosition;
            if (movingBody != null)
            {
                movingBody.position = movingTarget.position;
            }
        }

        SyncDesiredPosition();
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
        if (!isGameplayActive || movingTarget == null || Time.timeScale <= 0f)
        {
            return;
        }

        ApplyDesiredPosition();
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        FinishDrag();
        ReleaseRider(true);
        StopMovingBody();
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

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        if (!isRiderAttached || actorBody == null)
        {
            return;
        }

        if (actor != null && actor.IsGround)
        {
            ReleaseRiderForJump();
            return;
        }

        velocityBeforeJumpRequest = actorBody.linearVelocity.y;
        pendingJumpRelease = true;
    }

    private void LateUpdate()
    {
        if (!pendingJumpRelease)
        {
            return;
        }

        pendingJumpRelease = false;
        if (!isRiderAttached
            || actor == null
            || actorBody == null
            || actor.IsDied
            || actor.IsKnocked)
        {
            return;
        }

        if (Mathf.Abs(actorBody.linearVelocity.y - velocityBeforeJumpRequest) > 0.1f)
        {
            ReleaseRiderForJump();
        }
    }

    private void SetGameplayActive(bool isActive)
    {
        isGameplayActive = isActive;
        if (!isGameplayActive)
        {
            FinishDrag();
            ReleaseRider(true);
            StopMovingBody();
            SyncDesiredPosition();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform target = movingTarget != null ? movingTarget : transform;
        Transform parent = target.parent;
        float minY = Mathf.Min(minimumLocalY, maximumLocalY);
        float maxY = Mathf.Max(minimumLocalY, maximumLocalY);
        Vector3 localPosition = target.localPosition;
        Vector3 localCenter = new Vector3(localPosition.x, (minY + maxY) * 0.5f, localPosition.z);
        Vector3 localSize = new Vector3(Mathf.Max(0.01f, gizmoWidth), Mathf.Max(0.01f, maxY - minY), 0.05f);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = parent != null ? parent.localToWorldMatrix : Matrix4x4.identity;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(localCenter, localSize);
        Gizmos.DrawLine(
            new Vector3(localPosition.x, minY, localPosition.z),
            new Vector3(localPosition.x, maxY, localPosition.z));
        Gizmos.DrawSphere(new Vector3(localPosition.x, minY, localPosition.z), 0.08f);
        Gizmos.DrawSphere(new Vector3(localPosition.x, maxY, localPosition.z), 0.08f);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    #endregion

    #region 输入处理

    private void HandleTouchInput()
    {
        if (!isDragging)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began && TryBeginDrag(touch.position, touch.fingerId))
                {
                    return;
                }
            }

            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != activePointerId)
            {
                continue;
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                FinishDrag();
            }
            else
            {
                ContinueDrag(touch.position);
            }

            return;
        }

        FinishDrag();
    }

    private void HandleMouseInput()
    {
        if (!isDragging)
        {
            if (Input.GetMouseButtonDown(0))
            {
                TryBeginDrag(Input.mousePosition, MousePointerId);
            }

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
        {
            return false;
        }

        Vector3 pointerLocalPosition = WorldToParentLocal(worldPoint);
        Vector3 targetLocalPosition = GetCurrentLocalPosition();
        pointerToTargetLocalYOffset = targetLocalPosition.y - pointerLocalPosition.y;
        lockedLocalX = targetLocalPosition.x;
        lockedLocalZ = targetLocalPosition.z;
        activePointerId = pointerId;
        isDragging = true;
        return true;
    }

    private void ContinueDrag(Vector2 screenPosition)
    {
        if (!TryGetWorldPoint(screenPosition, out Vector3 worldPoint))
        {
            return;
        }

        Vector3 pointerLocalPosition = WorldToParentLocal(worldPoint);
        desiredLocalPosition = new Vector3(
            lockedLocalX,
            ClampLocalY(pointerLocalPosition.y + pointerToTargetLocalYOffset),
            lockedLocalZ);
        hasDesiredPosition = true;
    }

    private void FinishDrag()
    {
        isDragging = false;
        activePointerId = MousePointerId;
    }

    #endregion

    #region 引用与坐标换算

    private void ResolveRuntimeReferences()
    {
        if (movingTarget == null)
        {
            movingTarget = transform;
        }

        if (movingBody == null && movingTarget != null)
        {
            movingBody = movingTarget.GetComponent<Rigidbody2D>();
        }

        if (dragColliders == null || dragColliders.Length == 0)
        {
            dragColliders = movingTarget.GetComponentsInChildren<Collider2D>(true);
        }

        if (inputCamera == null)
        {
            inputCamera = Camera.main;
            if (inputCamera == null)
            {
                inputCamera = FindFirstObjectByType<Camera>();
            }
        }

        if (levelCtrl == null)
        {
            levelCtrl = GetComponentInParent<BaseLevelCtrl>();
        }

        if (actor == null && levelCtrl != null)
        {
            actor = levelCtrl.Actor;
        }

        if (actor != null)
        {
            actorCollider ??= actor.GetComponent<Collider2D>();
            actorBody ??= actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>();
        }

        if (Application.isPlaying && movingBody == null && !hasLoggedMissingMovingBody)
        {
            hasLoggedMissingMovingBody = true;
            Debug.LogWarning("[Level93VerticalPlatformDrag] Moving target requires a Kinematic Rigidbody2D.", this);
        }
    }

    private void SyncDesiredPosition()
    {
        if (movingTarget == null)
        {
            return;
        }

        desiredLocalPosition = GetCurrentLocalPosition();
        hasDesiredPosition = true;
    }

    private Vector3 GetCurrentLocalPosition()
    {
        if (movingBody == null)
        {
            return movingTarget.localPosition;
        }

        Vector3 worldPosition = movingTarget.position;
        worldPosition.x = movingBody.position.x;
        worldPosition.y = movingBody.position.y;
        return movingTarget.parent != null
            ? movingTarget.parent.InverseTransformPoint(worldPosition)
            : worldPosition;
    }

    private void ApplyDesiredPosition()
    {
        ResolveRuntimeReferences();
        if (!hasDesiredPosition || movingTarget == null)
        {
            return;
        }

        Vector3 targetWorldPosition = movingTarget.parent != null
            ? movingTarget.parent.TransformPoint(desiredLocalPosition)
            : desiredLocalPosition;
        Vector2 previousWorldPosition = movingBody != null
            ? movingBody.position
            : (Vector2)movingTarget.position;
        Vector2 movementDelta = (Vector2)targetWorldPosition - previousWorldPosition;
        float fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float maximumMoveDistance = Mathf.Max(0.01f, Physics2D.maxTranslationSpeed) * fixedDeltaTime;
        movementDelta = Vector2.ClampMagnitude(movementDelta, maximumMoveDistance);
        if (movementDelta.sqrMagnitude <= 0.0000001f)
        {
            StopMovingBody();
            MaintainAttachedRiderAtRest();
            return;
        }

        RefreshRiderAttachment(movementDelta);
        if (isRiderAttached && actorBody != null)
        {
            MoveAttachedRider(movementDelta);
        }

        if (movingBody != null)
        {
            movingBody.MovePosition(previousWorldPosition + movementDelta);
        }
        else
        {
            movingTarget.localPosition = desiredLocalPosition;
        }
    }

    private void RefreshRiderAttachment(Vector2 movementDelta)
    {
        if (isRiderAttached)
        {
            if (!CanContinueCarryingRider(movementDelta))
            {
                bool removePlatformVelocity = actor != null && !actor.IsKnocked && !actor.IsDied;
                ReleaseRider(removePlatformVelocity);
            }

            return;
        }

        if (Time.time >= riderSuppressedUntil && CanAcquireRider())
        {
            isRiderAttached = true;
            riderNeedsSettle = true;
        }
    }

    private bool CanAcquireRider()
    {
        if (!carryStandingActor
            || actor == null
            || actorCollider == null
            || actorBody == null
            || actor.IsDied
            || actor.IsKnocked
            || !actorBody.simulated
            || dragColliders == null)
        {
            return false;
        }

        return IsActorSupportedByPlatform(Mathf.Max(0.01f, riderStandingTolerance));
    }

    private bool CanContinueCarryingRider(Vector2 movementDelta)
    {
        if (!carryStandingActor
            || actor == null
            || actorCollider == null
            || actorBody == null
            || actor.IsDied
            || actor.IsKnocked
            || !actorBody.simulated
            || !actorCollider.enabled
            || dragColliders == null)
        {
            return false;
        }

        float releaseTolerance = Mathf.Max(
            Mathf.Max(0.6f, riderStandingTolerance * 3f),
            Mathf.Abs(movementDelta.y) + riderStandingTolerance);
        return IsActorOverPlatform(releaseTolerance);
    }

    private bool IsActorSupportedByPlatform(float tolerance)
    {
        if (actorCollider == null)
        {
            return false;
        }

        Bounds actorBounds = actorCollider.bounds;
        for (int i = 0; i < dragColliders.Length; i++)
        {
            Collider2D platformCollider = dragColliders[i];
            if (platformCollider == null
                || !platformCollider.enabled
                || platformCollider.isTrigger
                || !platformCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds platformBounds = platformCollider.bounds;
            bool overlapsHorizontally = actorBounds.max.x >= platformBounds.min.x
                                        && actorBounds.min.x <= platformBounds.max.x;
            bool actorIsAbovePlatform = actorBounds.center.y >= platformBounds.center.y;
            if (!overlapsHorizontally || !actorIsAbovePlatform)
            {
                continue;
            }

            float verticalGap = actorBounds.min.y - platformBounds.max.y;
            if (verticalGap < -tolerance || verticalGap > tolerance)
            {
                continue;
            }

            if (actorCollider.IsTouching(platformCollider))
            {
                return true;
            }

            ColliderDistance2D distance = actorCollider.Distance(platformCollider);
            float contactDistance = Mathf.Max(0.02f, Physics2D.defaultContactOffset * 2f);
            if (distance.isValid && distance.distance <= contactDistance)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsActorOverPlatform(float tolerance)
    {
        if (actorCollider == null)
        {
            return false;
        }

        Bounds actorBounds = actorCollider.bounds;
        for (int i = 0; i < dragColliders.Length; i++)
        {
            Collider2D platformCollider = dragColliders[i];
            if (platformCollider == null
                || !platformCollider.enabled
                || platformCollider.isTrigger
                || !platformCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds platformBounds = platformCollider.bounds;
            bool overlapsHorizontally = actorBounds.max.x >= platformBounds.min.x
                                        && actorBounds.min.x <= platformBounds.max.x;
            float verticalGap = actorBounds.min.y - platformBounds.max.y;
            if (overlapsHorizontally
                && actorBounds.center.y >= platformBounds.center.y
                && verticalGap >= -tolerance
                && verticalGap <= tolerance)
            {
                return true;
            }
        }

        return false;
    }

    private void ReleaseRider(bool removePlatformVelocity)
    {
        bool wasAttached = isRiderAttached;
        if (removePlatformVelocity && wasAttached && actorBody != null && actorBody.simulated)
        {
            Vector2 velocity = actorBody.linearVelocity;
            velocity.x = GetRiderHorizontalVelocity();
            velocity.y = RemoveInheritedVerticalVelocity(velocity.y);
            if (Mathf.Abs(velocity.y) <= 0.01f)
            {
                velocity.y = 0f;
            }

            actorBody.linearVelocity = velocity;
        }

        isRiderAttached = false;
        riderNeedsSettle = false;
        lastAppliedPlatformVelocity = Vector2.zero;
        pendingJumpRelease = false;
    }

    private void ReleaseRiderForJump()
    {
        ReleaseRider(false);
        riderSuppressedUntil = Time.time + 0.15f;
    }

    private float RemoveInheritedVerticalVelocity(float currentVelocityY)
    {
        float platformVelocityY = lastAppliedPlatformVelocity.y;
        if (Mathf.Abs(platformVelocityY) <= 0.01f
            || Mathf.Abs(currentVelocityY) <= 0.01f
            || Mathf.Sign(currentVelocityY) != Mathf.Sign(platformVelocityY))
        {
            return currentVelocityY;
        }

        float removableSpeed = Mathf.Min(Mathf.Abs(currentVelocityY), Mathf.Abs(platformVelocityY));
        return currentVelocityY - Mathf.Sign(platformVelocityY) * removableSpeed;
    }

    private void MoveAttachedRider(Vector2 movementDelta)
    {
        float fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float relativeHorizontalVelocity = GetRiderHorizontalVelocity();
        Vector2 platformVelocity = movementDelta / fixedDeltaTime;
        actorBody.linearVelocity = new Vector2(
            platformVelocity.x + relativeHorizontalVelocity,
            platformVelocity.y);
        lastAppliedPlatformVelocity = platformVelocity;
        riderNeedsSettle = movementDelta.sqrMagnitude > 0.0000001f;
    }

    private float GetRiderHorizontalVelocity()
    {
        if (actor == null
            || actor.IsKnocked
            || actor.IsWallDetected
            || levelCtrl == null
            || !levelCtrl.ActorCtrlable)
        {
            return 0f;
        }

        return actor.XInput * actor.MoveSpeed;
    }

    private void MaintainAttachedRiderAtRest()
    {
        RefreshRiderAttachment(Vector2.zero);
        if (isRiderAttached && actorBody != null && riderNeedsSettle)
        {
            MoveAttachedRider(Vector2.zero);
        }
    }

    private void StopMovingBody()
    {
        if (movingBody == null)
        {
            return;
        }

        movingBody.linearVelocity = Vector2.zero;
        movingBody.angularVelocity = 0f;
    }

    private bool TryGetWorldPoint(Vector2 screenPosition, out Vector3 worldPoint)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        Plane movementPlane = new Plane(Vector3.forward, movingTarget.position);
        if (!movementPlane.Raycast(ray, out float distance))
        {
            worldPoint = default;
            return false;
        }

        worldPoint = ray.GetPoint(distance);
        return true;
    }

    private bool IsPointOnPlatform(Vector2 worldPoint)
    {
        if (dragColliders == null)
        {
            return false;
        }

        for (int i = 0; i < dragColliders.Length; i++)
        {
            Collider2D dragCollider = dragColliders[i];
            if (dragCollider != null
                && dragCollider.enabled
                && dragCollider.gameObject.activeInHierarchy
                && dragCollider.OverlapPoint(worldPoint))
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 WorldToParentLocal(Vector3 worldPoint)
    {
        return movingTarget.parent != null
            ? movingTarget.parent.InverseTransformPoint(worldPoint)
            : worldPoint;
    }

    private float ClampLocalY(float value)
    {
        return Mathf.Clamp(value, Mathf.Min(minimumLocalY, maximumLocalY), Mathf.Max(minimumLocalY, maximumLocalY));
    }

    #endregion
}

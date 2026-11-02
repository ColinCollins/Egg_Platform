using Bear.EventSystem;
using Game.Events;
using Game.Level;
using Game.Play;
using UnityEngine;

/// <summary>
/// Level093 平台专用的两点往复移动组件，可在 Scene 视图中编辑本地轨迹。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level93PlatformPingPong : MonoBehaviour
{
    #region Inspector 配置

    /// <summary>
    /// 实际移动的平台；为空时移动当前组件所在物体，不应与拖拽组件同时驱动同一 Transform。
    /// </summary>
    [SerializeField] private Transform movingTarget;

    /// <summary>
    /// 相对初始本地位置的第一个端点偏移。
    /// </summary>
    [SerializeField] private Vector2 firstPointOffset = Vector2.zero;

    /// <summary>
    /// 相对初始本地位置的第二个端点偏移，可同时包含水平和竖直分量。
    /// </summary>
    [SerializeField] private Vector2 secondPointOffset = new Vector2(3f, 0f);

    /// <summary>
    /// 平台沿本地轨迹移动的速度，单位为本地坐标单位每秒。
    /// </summary>
    [SerializeField, Min(0.01f)] private float moveSpeed = 1.5f;

    /// <summary>
    /// 平台到达每个端点后的停留时间，单位为秒。
    /// </summary>
    [SerializeField, Min(0f)] private float waitAtEndSeconds = 0.2f;

    /// <summary>
    /// 组件启用后是否自动开始往复；关闭时只显示 Gizmo，不移动平台。
    /// </summary>
    [SerializeField] private bool playOnEnable = true;

    /// <summary>
    /// Scene 视图中运动轨迹的颜色，仅影响 Gizmo 显示。
    /// </summary>
    [SerializeField] private Color gizmoColor = new Color(1f, 0.75f, 0.1f, 0.9f);

    /// <summary>
    /// 用作角色站立判定的平台碰撞体；为空时从 movingTarget 子级自动读取。
    /// </summary>
    [SerializeField] private Collider2D[] platformColliders;

    /// <summary>
    /// 角色站在平台上时是否随往复平台一起移动，关闭后仅移动平台自身。
    /// </summary>
    [SerializeField] private bool carryStandingActor = true;

    /// <summary>
    /// 判定角色仍站在平台表面时允许的竖直误差，单位为世界坐标。
    /// </summary>
    [SerializeField, Min(0.01f)] private float riderStandingTolerance = 0.25f;

    #endregion

    #region 运行时状态

    private Vector3 initialLocalPosition;
    private float waitUntil;
    private bool hasCachedInitialPosition;
    private bool movingTowardSecondPoint = true;
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
        ResolveTarget();
        CacheInitialPosition();
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
        ResolveTarget();
        ResolveRuntimeReferences();
        CacheInitialPosition();
        movingTowardSecondPoint = true;
        waitUntil = 0f;
    }

    private void FixedUpdate()
    {
        if (!playOnEnable
            || !isGameplayActive
            || movingTarget == null
            || Time.timeScale <= 0f)
        {
            return;
        }

        if (Time.fixedTime < waitUntil)
        {
            StopMovingBody();
            MaintainAttachedRiderAtRest();
            return;
        }

        Vector3 destination = GetEndpointLocalPosition(movingTowardSecondPoint);
        float fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float maximumMoveDistance = Mathf.Max(0.01f, Physics2D.maxTranslationSpeed) * fixedDeltaTime;
        Vector3 nextPosition = Vector3.MoveTowards(
            GetCurrentLocalPosition(),
            destination,
            Mathf.Min(Mathf.Max(0.01f, moveSpeed) * fixedDeltaTime, maximumMoveDistance));
        MoveTargetToLocalPosition(nextPosition);

        if ((nextPosition - destination).sqrMagnitude > 0.0001f)
        {
            return;
        }

        movingTowardSecondPoint = !movingTowardSecondPoint;
        waitUntil = Time.fixedTime + Mathf.Max(0f, waitAtEndSeconds);
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
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
            ReleaseRider(true);
            StopMovingBody();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform target = movingTarget != null ? movingTarget : transform;
        Vector3 anchor = Application.isPlaying && hasCachedInitialPosition
            ? initialLocalPosition
            : target.localPosition;
        Vector3 firstPoint = anchor + new Vector3(firstPointOffset.x, firstPointOffset.y, 0f);
        Vector3 secondPoint = anchor + new Vector3(secondPointOffset.x, secondPointOffset.y, 0f);
        Transform parent = target.parent;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = parent != null ? parent.localToWorldMatrix : Matrix4x4.identity;
        Gizmos.color = gizmoColor;
        Gizmos.DrawLine(firstPoint, secondPoint);
        Gizmos.DrawWireSphere(firstPoint, 0.12f);
        Gizmos.DrawWireSphere(secondPoint, 0.12f);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    #endregion

    #region 轨迹计算

    private void ResolveTarget()
    {
        if (movingTarget == null)
        {
            movingTarget = transform;
        }
    }

    private void ResolveRuntimeReferences()
    {
        if (movingBody == null && movingTarget != null)
        {
            movingBody = movingTarget.GetComponent<Rigidbody2D>();
        }

        if (movingTarget != null && (platformColliders == null || platformColliders.Length == 0))
        {
            platformColliders = movingTarget.GetComponentsInChildren<Collider2D>(true);
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
            Debug.LogWarning("[Level93PlatformPingPong] Moving target requires a Kinematic Rigidbody2D.", this);
        }
    }

    private void CacheInitialPosition()
    {
        if (hasCachedInitialPosition || movingTarget == null)
        {
            return;
        }

        initialLocalPosition = movingTarget.localPosition;
        hasCachedInitialPosition = true;
    }

    private Vector3 GetEndpointLocalPosition(bool secondPoint)
    {
        Vector2 offset = secondPoint ? secondPointOffset : firstPointOffset;
        return initialLocalPosition + new Vector3(offset.x, offset.y, 0f);
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

    private void MoveTargetToLocalPosition(Vector3 targetLocalPosition)
    {
        ResolveRuntimeReferences();
        Vector3 targetWorldPosition = movingTarget.parent != null
            ? movingTarget.parent.TransformPoint(targetLocalPosition)
            : targetLocalPosition;
        Vector2 previousWorldPosition = movingBody != null
            ? movingBody.position
            : (Vector2)movingTarget.position;
        Vector2 movementDelta = (Vector2)targetWorldPosition - previousWorldPosition;

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
            movingTarget.localPosition = targetLocalPosition;
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
            || platformColliders == null)
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
            || platformColliders == null)
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
        for (int i = 0; i < platformColliders.Length; i++)
        {
            Collider2D platformCollider = platformColliders[i];
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
        for (int i = 0; i < platformColliders.Length; i++)
        {
            Collider2D platformCollider = platformColliders[i];
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
        ResolveRuntimeReferences();
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

    #endregion
}

using UnityEngine;

/// <summary>
/// Level 131 加速方块的水平运动权威。实现沿用 Level 130 的核心关系：
/// 方块使用 Kinematic MovePosition，并把本帧实际水平速度传给站在方块上的 Actor。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level131PlatformMover : MonoBehaviour
{
    private const float StandingToleranceBand = 0.45f;

    private Rigidbody2D movingBody;
    private ActorCtrl actor;
    private Collider2D[] platformColliders;
    private bool initialized;
    private bool isRunning;
    private bool carryActor;
    private float speed;

    public float CurrentSpeed => speed;

    public Rigidbody2D Body => movingBody;

    public bool IsRunning => isRunning;

    public void Configure(ActorCtrl targetActor)
    {
        actor = targetActor;
        movingBody = GetComponent<Rigidbody2D>() ?? gameObject.AddComponent<Rigidbody2D>();
        movingBody.bodyType = RigidbodyType2D.Kinematic;
        movingBody.gravityScale = 0f;
        movingBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        movingBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        movingBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        platformColliders = GetComponentsInChildren<Collider2D>(true);
        initialized = true;
    }

    public void SetSpeed(float targetSpeed)
    {
        speed = Mathf.Max(0f, targetSpeed);
        isRunning = speed > 0.0001f;
    }

    public void SetCarryActor(bool enabled)
    {
        carryActor = enabled;
    }

    public void SetActorCollisionIgnored(bool ignored)
    {
        if (actor == null || platformColliders == null)
            return;

        Collider2D actorCollider = actor.GetComponent<Collider2D>();
        if (actorCollider == null)
            return;

        foreach (Collider2D platformCollider in platformColliders)
        {
            if (platformCollider != null && !platformCollider.isTrigger)
                Physics2D.IgnoreCollision(actorCollider, platformCollider, ignored);
        }
    }

    public void Stop()
    {
        speed = 0f;
        isRunning = false;
        if (movingBody != null)
            movingBody.linearVelocity = Vector2.zero;
    }

    public bool TryGetSolidRightEdge(out float rightEdge)
    {
        rightEdge = float.NegativeInfinity;
        if (platformColliders == null || movingBody == null)
            return false;

        foreach (Collider2D collider in platformColliders)
        {
            if (collider == null || !collider.enabled || collider.isTrigger ||
                !collider.gameObject.activeInHierarchy || collider.attachedRigidbody != movingBody)
                continue;
            rightEdge = Mathf.Max(rightEdge, collider.bounds.max.x);
        }
        return !float.IsNegativeInfinity(rightEdge);
    }

    public void MoveStep(float remainingWorldDistance)
    {
        if (!initialized || !isRunning || movingBody == null || Time.timeScale <= 0f)
            return;

        float deltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float maxDistance = Mathf.Max(0.01f, Physics2D.maxTranslationSpeed) * deltaTime;
        float distance = Mathf.Min(Mathf.Min(speed * deltaTime, maxDistance), Mathf.Max(0f, remainingWorldDistance));
        Vector2 previousPosition = movingBody.position;
        Vector2 nextPosition = previousPosition + Vector2.right * distance;
        movingBody.MovePosition(nextPosition);

        if (!carryActor || actor == null || actor.IsDied || actor.RB == null)
            return;

        Collider2D actorCollider = actor.GetComponent<Collider2D>();
        if (actorCollider == null)
            return;

        float actualVelocity = distance / deltaTime;
        // 沿用 130 的站立携带合同：只有仍受方块支撑（IsSupported）
        // 或处于方块顶部容差带内（覆盖高速档的接触抖动）才合成水平速度；
        // 乘员真正离开方块后不再携带，交给水下 FallArea 的正常失败链。
        bool supported = IsSupported(actorCollider);
        bool nearPlatformTop = Mathf.Abs(actorCollider.bounds.min.y - GetPlatformTop()) <= StandingToleranceBand;
        if (supported || nearPlatformTop)
            actor.RB.linearVelocity = new Vector2(actualVelocity, actor.RB.linearVelocityY);
    }

    private float GetPlatformTop()
    {
        if (platformColliders == null)
            return transform.position.y;

        float top = float.NegativeInfinity;
        foreach (Collider2D platformCollider in platformColliders)
        {
            if (platformCollider == null || !platformCollider.enabled || platformCollider.isTrigger)
                continue;
            top = Mathf.Max(top, platformCollider.bounds.max.y);
        }

        return float.IsNegativeInfinity(top) ? transform.position.y : top;
    }

    private bool IsSupported(Collider2D riderCollider)
    {
        if (riderCollider == null || !riderCollider.enabled || platformColliders == null)
            return false;

        Bounds riderBounds = riderCollider.bounds;
        const float standingTolerance = 0.25f;
        foreach (Collider2D platformCollider in platformColliders)
        {
            if (platformCollider == null || !platformCollider.enabled || platformCollider.isTrigger)
                continue;

            Bounds platformBounds = platformCollider.bounds;
            bool horizontalOverlap = riderBounds.max.x >= platformBounds.min.x &&
                                     riderBounds.min.x <= platformBounds.max.x;
            float verticalGap = riderBounds.min.y - platformBounds.max.y;
            if (!horizontalOverlap || riderBounds.center.y < platformBounds.center.y ||
                verticalGap < -standingTolerance || verticalGap > standingTolerance)
            {
                continue;
            }

            if (riderCollider.IsTouching(platformCollider))
                return true;

            ColliderDistance2D distance = riderCollider.Distance(platformCollider);
            if (distance.isValid && distance.distance <= Mathf.Max(0.02f, Physics2D.defaultContactOffset * 2f))
                return true;
        }

        return false;
    }
}

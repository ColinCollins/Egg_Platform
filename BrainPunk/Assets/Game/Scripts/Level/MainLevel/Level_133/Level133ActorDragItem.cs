using UnityEngine;

/// <summary>
/// Level 133 的主角拖拽适配器。
/// 复用 DragableItem 的点击、触摸和世界坐标换算，只对其直接写入的 Transform 做实体碰撞约束。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class Level133ActorDragItem : DragableItem
{
    private const int CastHitCapacity = 16;
    private const int MaxSlideIterations = 2;
    private const float DirectionEpsilon = 0.001f;

    [Header("Level 133 碰撞约束")]
    [Tooltip("拖拽时阻挡主角的实体层。Level 133 的地面、墙体、平台和刺柱实体均使用 Ground 层。")]
    [SerializeField] private LayerMask blockingLayers = 1 << 6;

    [Tooltip("主角与实体碰撞面保持的最小间隙（世界单位）。")]
    [SerializeField, Min(0f)] private float collisionSkin = 0.01f;

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[CastHitCapacity];

    private Rigidbody2D actorRigidbody;
    private Collider2D actorCollider;
    private ActorCtrl actorController;
    private ContactFilter2D blockingFilter;
    private Vector3 lastAllowedPosition;
    private bool referencesResolved;
    private bool physicsCaptured;
    private RigidbodyType2D originalBodyType;
    private bool originalSimulated;

    protected override void OnDragStart()
    {
        ResolveReferences();
        if (!referencesResolved)
        {
            Debug.LogError("[Level133ActorDragItem] Actor Rigidbody2D or Collider2D is missing.", this);
            SetEnableDrag(false);
            return;
        }

        CapturePhysicsState();
        lastAllowedPosition = transform.position;
        Physics2D.SyncTransforms();
        base.OnDragStart();
    }

    protected override void OnDragUpdate()
    {
        if (physicsCaptured)
            ResolveDraggedPosition();

        base.OnDragUpdate();
    }

    protected override void OnDragEnd()
    {
        base.OnDragEnd();
        RestorePhysicsState();
    }

    private void OnDisable()
    {
        if (IsDragging())
            SetEnableDrag(false);

        RestorePhysicsState();
    }

    private void OnDestroy()
    {
        RestorePhysicsState();
    }

    private void ResolveReferences()
    {
        if (actorRigidbody == null)
            actorRigidbody = GetComponent<Rigidbody2D>();
        if (actorCollider == null)
            actorCollider = GetComponent<Collider2D>();
        if (actorController == null)
            actorController = GetComponent<ActorCtrl>();

        if (blockingLayers.value == 0)
            blockingLayers = LayerMask.GetMask("Ground");

        blockingFilter = new ContactFilter2D
        {
            useTriggers = false
        };
        blockingFilter.SetLayerMask(blockingLayers);
        referencesResolved = actorRigidbody != null && actorCollider != null;
    }

    private void CapturePhysicsState()
    {
        if (physicsCaptured || actorRigidbody == null)
            return;

        originalBodyType = actorRigidbody.bodyType;
        originalSimulated = actorRigidbody.simulated;
        physicsCaptured = true;

        actorController?.SetMoveInput(0f);
        actorRigidbody.bodyType = RigidbodyType2D.Kinematic;
        actorRigidbody.linearVelocity = Vector2.zero;
        actorRigidbody.angularVelocity = 0f;
    }

    private void RestorePhysicsState()
    {
        if (!physicsCaptured || actorRigidbody == null)
            return;

        actorRigidbody.bodyType = originalBodyType;
        actorRigidbody.simulated = originalSimulated;
        actorRigidbody.linearVelocity = Vector2.zero;
        actorRigidbody.angularVelocity = 0f;
        actorController?.SetMoveInput(0f);
        physicsCaptured = false;
    }

    private void ResolveDraggedPosition()
    {
        if (actorCollider == null || !actorCollider.enabled || !actorCollider.gameObject.activeInHierarchy)
        {
            transform.position = lastAllowedPosition;
            Physics2D.SyncTransforms();
            return;
        }

        Vector3 proposedPosition = transform.position;
        Vector2 remaining = proposedPosition - lastAllowedPosition;
        if (remaining.sqrMagnitude <= Mathf.Epsilon)
            return;

        Vector3 resolvedPosition = lastAllowedPosition;
        transform.position = resolvedPosition;
        Physics2D.SyncTransforms();

        for (int iteration = 0; iteration < MaxSlideIterations; iteration++)
        {
            float requestedDistance = remaining.magnitude;
            if (requestedDistance <= Mathf.Epsilon)
                break;

            Vector2 direction = remaining / requestedDistance;
            int hitCount = actorCollider.Cast(
                direction,
                blockingFilter,
                castHits,
                requestedDistance + Mathf.Max(0f, collisionSkin));

            if (!TryFindBlockingHit(direction, hitCount, out RaycastHit2D hit))
            {
                resolvedPosition += (Vector3)remaining;
                remaining = Vector2.zero;
                break;
            }

            float allowedDistance = Mathf.Clamp(
                hit.distance - Mathf.Max(0f, collisionSkin),
                0f,
                requestedDistance);
            resolvedPosition += (Vector3)(direction * allowedDistance);

            Vector2 leftover = remaining - direction * allowedDistance;
            if (leftover.sqrMagnitude <= Mathf.Epsilon || hit.normal.sqrMagnitude <= Mathf.Epsilon)
            {
                remaining = Vector2.zero;
                break;
            }

            // 移除朝向实体内部的分量，让剩余位移沿碰撞面滑动。
            float normalComponent = Vector2.Dot(leftover, hit.normal);
            if (normalComponent < 0f)
                leftover -= hit.normal * normalComponent;

            remaining = leftover;
            transform.position = resolvedPosition;
            Physics2D.SyncTransforms();
        }

        resolvedPosition.z = proposedPosition.z;
        transform.position = resolvedPosition;
        Physics2D.SyncTransforms();
        lastAllowedPosition = resolvedPosition;
    }

    private bool TryFindBlockingHit(Vector2 direction, int hitCount, out RaycastHit2D nearestHit)
    {
        nearestHit = default;
        float nearestDistance = float.PositiveInfinity;

        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit2D hit = castHits[index];
            Collider2D hitCollider = hit.collider;
            if (hitCollider == null || hitCollider.isTrigger || IsActorCollider(hitCollider) || hit.distance < 0f)
                continue;

            // Cast 只在移动方向压向表面时构成阻挡；贴着地面横向移动不应被当前地面接触截断。
            if (Vector2.Dot(direction, hit.normal) >= -DirectionEpsilon)
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
            }
        }

        return nearestHit.collider != null;
    }

    private bool IsActorCollider(Collider2D candidate)
    {
        return candidate == actorCollider ||
               candidate.transform == transform ||
               candidate.transform.IsChildOf(transform);
    }

    private void OnValidate()
    {
        collisionSkin = Mathf.Max(0f, collisionSkin);
    }
}

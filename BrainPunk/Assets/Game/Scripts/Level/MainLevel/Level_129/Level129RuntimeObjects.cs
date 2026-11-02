using UnityEngine;

[DisallowMultipleComponent]
public sealed class Level129HazardHitbox : MonoBehaviour
{
    private Level129Ctrl owner;

    internal void Initialize(Level129Ctrl newOwner)
    {
        owner = newOwner;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ActorCtrl hitActor = other != null ? other.GetComponentInParent<ActorCtrl>() : null;
        if (owner == null || hitActor == null)
            return;

        Vector3 hitPosition = transform.position;
        Collider2D hazardCollider = GetComponent<Collider2D>();
        if (hazardCollider != null && other != null)
        {
            Vector2 hazardContact = hazardCollider.ClosestPoint(other.bounds.center);
            Vector2 actorContact = other.ClosestPoint(transform.position);
            hitPosition = (hazardContact + actorContact) * 0.5f;
        }

        owner.TryCommitHazardHit(hitActor, hitPosition);
    }
}

[DisallowMultipleComponent]
public sealed class Level129BowPickupTrigger : MonoBehaviour
{
    private Level129Ctrl owner;

    internal void Initialize(Level129Ctrl newOwner)
    {
        owner = newOwner;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ActorCtrl hitActor = other != null ? other.GetComponentInParent<ActorCtrl>() : null;
        if (hitActor != null)
            owner?.TryCollectBow(hitActor);
    }
}

[DisallowMultipleComponent]
public sealed class Level129RollingStoneMotion : MonoBehaviour
{
    private Level129Ctrl owner;
    private float moveSpeed;
    private float angularSpeed;
    private float recycleWorldX;

    internal void Initialize(
        Level129Ctrl newOwner,
        float configuredMoveSpeed,
        float radius,
        float configuredRecycleWorldX)
    {
        owner = newOwner;
        moveSpeed = Mathf.Max(0f, configuredMoveSpeed);
        angularSpeed = radius > 0.0001f
            ? moveSpeed / radius * Mathf.Rad2Deg
            : 0f;
        recycleWorldX = configuredRecycleWorldX;
    }

    private void Update()
    {
        if (owner == null || owner.IsRuntimeSuspended)
            return;

        float deltaTime = Time.deltaTime;
        transform.position += Vector3.left * (moveSpeed * deltaTime);
        transform.Rotate(0f, 0f, angularSpeed * deltaTime);
        if (transform.position.x <= recycleWorldX)
            owner.DestroyRuntimeObject(gameObject);
    }
}

[DisallowMultipleComponent]
public sealed class Level129FragmentPiece : MonoBehaviour
{
    private Level129Ctrl owner;
    private SpriteRenderer spriteRenderer;
    private Vector2 velocity;
    private float gravity;
    private float angularSpeed;
    private float lifetime;
    private float fadeDuration;
    private float elapsed;
    private Color initialColor;

    internal void Initialize(
        Level129Ctrl newOwner,
        SpriteRenderer renderer,
        Vector2 initialVelocity,
        float configuredGravity,
        float configuredAngularSpeed,
        float configuredLifetime,
        float configuredFadeDuration)
    {
        owner = newOwner;
        spriteRenderer = renderer;
        velocity = initialVelocity;
        gravity = configuredGravity;
        angularSpeed = configuredAngularSpeed;
        lifetime = Mathf.Max(0.01f, configuredLifetime);
        fadeDuration = Mathf.Clamp(configuredFadeDuration, 0f, lifetime);
        initialColor = renderer != null ? renderer.color : Color.white;
    }

    private void Update()
    {
        if (owner == null || owner.IsRuntimeSuspended)
            return;

        float deltaTime = Time.deltaTime;
        elapsed += deltaTime;
        velocity.y -= gravity * deltaTime;
        transform.position += (Vector3)(velocity * deltaTime);
        transform.Rotate(0f, 0f, angularSpeed * deltaTime);

        if (spriteRenderer != null && fadeDuration > 0f)
        {
            float fadeStart = lifetime - fadeDuration;
            if (elapsed >= fadeStart)
            {
                Color color = initialColor;
                color.a = Mathf.Clamp01((lifetime - elapsed) / fadeDuration);
                spriteRenderer.color = color;
            }
        }

        if (elapsed >= lifetime)
            owner.DestroyRuntimeObject(gameObject);
    }
}

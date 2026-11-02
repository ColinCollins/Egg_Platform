using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// Level 130 专用的星星掉落逻辑：仅当配置的承托平台被击碎后，
/// 将标准 Postcard_Star 切换为带实体碰撞体的动态物体并向下掉落。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Postcard_Star))]
public sealed class Level130StarDropOnPlatformBreak : MonoBehaviour
{
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D supportCollider;
    [SerializeField, Min(0.01f)] private float fallGravityScale = 1f;
    [SerializeField] private Vector2 solidColliderSize = new Vector2(0.8f, 0.8f);
    [SerializeField] private Vector2 solidColliderOffset = new Vector2(0f, -0.4f);

    private MoveFloatHandle floatHandle;
    private bool hasDropped;
    private bool isFalling;
    private Level130AutomaticPlatform carryingPlatform;
    private float platformOffsetX;

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        ConfigureAttachedPhysics();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        DetachFromPlatform();
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.simulated = false;
    }

    private void FixedUpdate()
    {
        if (!isFalling || body == null || !body.simulated)
            return;

        body.linearVelocity = new Vector2(0f, body.linearVelocityY);
        body.angularVelocity = 0f;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryFinishFall(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryFinishFall(collision);
    }

    /// <summary>
    /// 由 Level130Ctrl 在平台完成击碎后调用；其它平台不会触发本星星掉落。
    /// </summary>
    public void NotifyPlatformBroken(Level130BreakablePlatform platform)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || hasDropped || platform == null)
            return;

        if (platform.Role != Level130BreakablePlatform.PlatformRole.DoorSupport)
            return;

        BeginFall();
    }

    private void BeginFall()
    {
        ResolveReferences();
        if (body == null || supportCollider == null)
            return;

        hasDropped = true;
        isFalling = true;
        DetachFromPlatform();

        if (floatHandle != null)
        {
            floatHandle.StopMove();
            floatHandle.enabled = false;
        }

        supportCollider.enabled = true;

        // 平台击碎瞬间不继承平台的横向运动，按用户确认保持水平速度为 0。
        Physics2D.SyncTransforms();
        body.simulated = false;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = Mathf.Max(0.01f, fallGravityScale);
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.position = transform.position;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.simulated = true;
    }

    private void TryFinishFall(Collision2D collision)
    {
        if (!isActiveAndEnabled || !isFalling || body == null || !body.simulated ||
            !IsValidSupportCollision(collision))
            return;

        isFalling = false;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.gravityScale = 0f;
        body.bodyType = RigidbodyType2D.Kinematic;

        Level130AutomaticPlatform platform =
            collision.collider.GetComponentInParent<Level130AutomaticPlatform>();
        if (platform != null && platform.isActiveAndEnabled &&
            platform.OwnsPlatformCollider(collision.collider))
        {
            carryingPlatform = platform;
            platformOffsetX = body.position.x - platform.WorldX;
            platform.HorizontalPositionRequested += FollowPlatform;
        }
    }

    private void FollowPlatform(float nextPlatformWorldX)
    {
        if (!isActiveAndEnabled || isFalling || body == null || !body.simulated)
            return;

        body.MovePosition(new Vector2(nextPlatformWorldX + platformOffsetX, body.position.y));
    }

    private void DetachFromPlatform()
    {
        if (carryingPlatform != null)
            carryingPlatform.HorizontalPositionRequested -= FollowPlatform;
        carryingPlatform = null;
    }

    private bool IsValidSupportCollision(Collision2D collision)
    {
        if (collision == null)
            return false;

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0 && collision.gameObject.layer != groundLayer)
            return false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.25f)
                return true;
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody2D>();

        if (supportCollider == null)
        {
            Collider2D[] colliders = GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && !colliders[i].isTrigger)
                {
                    supportCollider = colliders[i];
                    break;
                }
            }
        }

        if (supportCollider == null)
            supportCollider = gameObject.AddComponent<BoxCollider2D>();

        if (floatHandle == null)
            floatHandle = GetComponent<MoveFloatHandle>();
    }

    private void ConfigureAttachedPhysics()
    {
        if (body == null || supportCollider == null)
            return;

        if (supportCollider is BoxCollider2D boxCollider)
        {
            boxCollider.size = solidColliderSize;
            boxCollider.offset = solidColliderOffset;
        }

        supportCollider.isTrigger = false;
        int groundLayer = LayerMask.NameToLayer("Ground");
        int groundMask = groundLayer >= 0 ? 1 << groundLayer : 0;
        supportCollider.includeLayers = groundMask;
        supportCollider.excludeLayers = groundLayer >= 0 ? ~groundMask : 0;
        supportCollider.enabled = true;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.simulated = true;
    }
}

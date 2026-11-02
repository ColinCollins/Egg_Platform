using System;
using UnityEngine;

/// <summary>
/// D084/D085/D086 弹性线的有界弹射与球速上限，避免多线接触在求解器内部连续增能。
/// 挂在球刚体所在对象并显式绑定；线材质使用零弹性，发射增能每物理步最多执行一次。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 84 Ball Speed Limiter")]
public sealed class DLevel84BallSpeedLimiter : MonoBehaviour
{
    #region 序列化配置

    /// <summary>同一球对象上的刚体；由本系列各关 Prefab 显式引用，不在运行时查找。</summary>
    [SerializeField] private Rigidbody2D ballRigidbody;

    /// <summary>球的最大线速度，单位为世界单位/秒；窄框内按活动间距进一步降低，保持速度方向。</summary>
    [Tooltip("高弹球的最大线速度；窄框内会按活动间距进一步限速，普通单面弹射与滚动保持。")]
    [SerializeField, Min(1f)] private float maxLinearSpeed = 30f;

    /// <summary>本系列的零弹性线材质，用于识别可发射球的接触；必须显式绑定，地面不参与增能。</summary>
    [SerializeField] private PhysicsMaterial2D elasticLineMaterial;

    /// <summary>球碰线后目标法向分离速度与入射速度之比；只在真实撞击时增能，随后立即限速。</summary>
    [SerializeField, Min(1f)] private float lineLaunchRestitution = 2f;

    #endregion

    #region 局部物理查询

    // 查询只用于限速，不查找或绑定场景对象；复用数组避免每步产生垃圾。
    private readonly RaycastHit2D[] _castHits = new RaycastHit2D[8];

    // 一根线可能包含多个重叠 Collider，同一步只能补一次弹射，不能按回调次数叠加。
    private bool _hasLaunchedThisStep;

    #endregion

    #region 物理回调

    private void FixedUpdate()
    {
        _hasLaunchedThisStep = false;
        LimitLinearSpeed();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryLaunchFromLine(collision);
        // 弹射只在求解后的回调中增能，并立即约束下一物理步的速度。
        LimitLinearSpeed();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryLaunchFromLine(collision);
        LimitLinearSpeed();
    }

    #endregion

    #region 球碰线的有界弹射

    private void TryLaunchFromLine(Collision2D collision)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || ballRigidbody == null ||
            !ballRigidbody.simulated || ballRigidbody.bodyType != RigidbodyType2D.Dynamic ||
            _hasLaunchedThisStep || elasticLineMaterial == null || collision == null)
            return;

        Collider2D surface = collision.collider;
        Rigidbody2D surfaceBody = collision.rigidbody;
        PhysicsMaterial2D material = surface.sharedMaterial;
        if (material == null && surfaceBody != null)
            material = surfaceBody.sharedMaterial;
        if (material != elasticLineMaterial)
            return;

        float impactSpeed = Physics2D.bounceThreshold;
        ContactPoint2D impact = default;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            // 新线可能生成在运动中的球内，深度重叠只交给无增能的物理求解分离。
            if (contact.separation < -0.05f)
                continue;

            // Unity 2D 回调中的相对速度为对方相对本球的入射速度，法线朝本球。
            float incoming = Vector2.Dot(contact.relativeVelocity, contact.normal);
            if (incoming <= impactSpeed)
                continue;

            impactSpeed = incoming;
            impact = contact;
        }
        if (impactSpeed <= Physics2D.bounceThreshold)
            return;

        Vector2 surfaceVelocity = surfaceBody != null
            ? surfaceBody.GetPointVelocity(impact.point)
            : Vector2.zero;
        float outgoing = Vector2.Dot(
            ballRigidbody.GetPointVelocity(impact.point) - surfaceVelocity, impact.normal);
        float boost = Mathf.Max(0f, impactSpeed * lineLaunchRestitution - outgoing);
        if (boost <= 0f)
            return;

        _hasLaunchedThisStep = true;
        ballRigidbody.linearVelocity += impact.normal * boost;
    }

    #endregion

    #region 速度约束

    private void LimitLinearSpeed()
    {
        // Unity 可能向禁用的 MonoBehaviour 分发碰撞消息，因此同时检查组件状态。
        if (!Application.isPlaying || !isActiveAndEnabled || ballRigidbody == null ||
            !ballRigidbody.simulated || ballRigidbody.bodyType != RigidbodyType2D.Dynamic)
            return;

        Vector2 velocity = ballRigidbody.linearVelocity;
        if (float.IsNaN(velocity.x) || float.IsInfinity(velocity.x) ||
            float.IsNaN(velocity.y) || float.IsInfinity(velocity.y))
        {
            // 异常速度不传入下一物理步；不移动球或触发新的失败/重开流程。
            ballRigidbody.linearVelocity = Vector2.zero;
            return;
        }

        float limit = Mathf.Max(1f, maxLinearSpeed);
        // 使用 double 求长度，避免极端有限速度在平方时溢出。
        double speedSquared = (double)velocity.x * velocity.x + (double)velocity.y * velocity.y;
        if (speedSquared > 0.0001)
        {
            Vector2 direction = velocity * (float)(1.0 / Math.Sqrt(speedSquared));
            limit = GetConfinedSpeedLimit(direction, limit);
        }
        if (speedSquared <= (double)limit * limit)
            return;

        float scale = (float)(limit / Math.Sqrt(speedSquared));
        ballRigidbody.linearVelocity = velocity * scale;
    }

    private float GetConfinedSpeedLimit(Vector2 direction, float limit)
    {
        float step = Mathf.Max(0.0001f, Time.fixedDeltaTime);
        float lookAhead = limit * step * 4f;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(ballRigidbody.gameObject.layer));
        if (!TryGetNearestHit(direction, filter, lookAhead, out RaycastHit2D forward))
            return limit;

        // 沿前方表面的法线反向寻找相对的表面；普通单面地板没有这组相对约束。
        Vector2 awayFromSurface = forward.normal;
        if (!TryGetNearestHit(awayFromSurface, filter, lookAhead, out RaycastHit2D opposite) ||
            Vector2.Dot(awayFromSurface, opposite.normal) > -0.9f)
            return limit;

        float clearance = forward.distance * Mathf.Abs(Vector2.Dot(direction, forward.normal)) + opposite.distance;
        // 窄框需留出多个物理步完成往返，避免连续碰撞求解在单步内反复放大球和线框的速度。
        return Mathf.Clamp(clearance / (4f * step), 0.1f, limit);
    }

    private bool TryGetNearestHit(Vector2 direction, ContactFilter2D filter, float distance, out RaycastHit2D nearest)
    {
        int count = ballRigidbody.Cast(direction, filter, _castHits, distance);
        nearest = default;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (_castHits[i].distance >= nearestDistance)
                continue;

            nearest = _castHits[i];
            nearestDistance = nearest.distance;
        }

        return count > 0;
    }

    #endregion
}

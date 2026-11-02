using UnityEngine;

/// <summary>
/// DLevel53 单只蜜蜂的两帧动画、玩家追踪、碰撞施力与反冲行为。
/// 蜜蜂之间的碰撞由 DLevel53Hive 在生成时忽略；本组件只处理玩家和场景实体碰撞。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
[AddComponentMenu("Game/Draw/Level 53 Bee")]
public sealed class DLevel53Bee : MonoBehaviour
{
    #region Inspector References

    /// <summary>
    /// 蜜蜂自身动态刚体。必须保持零重力、连续检测和旋转冻结，以避免高速穿线或撞击乱转。
    /// </summary>
    [Header("运行时引用")]
    [Tooltip("蜜蜂自身的 Dynamic Rigidbody2D。")]
    [SerializeField] private Rigidbody2D body;

    /// <summary>
    /// 蜜蜂唯一的非 Trigger 实体碰撞体，用于场景碰撞和玩家命中判定。
    /// </summary>
    [Tooltip("蜜蜂自身的非 Trigger Collider2D。")]
    [SerializeField] private Collider2D hitCollider;

    /// <summary>
    /// 承载两帧蜜蜂贴图的 SpriteRenderer。
    /// </summary>
    [Tooltip("播放两帧蜜蜂动画的 SpriteRenderer。")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    #endregion

    #region Animation Settings

    /// <summary>
    /// 蜜蜂循环播放的图片帧。当前 Prefab 绑定 Sp_hz_mf_01 与 Sp_hz_mf_02。
    /// </summary>
    [Header("两帧动画")]
    [Tooltip("按顺序循环播放的蜜蜂 Sprite 帧。")]
    [SerializeField] private Sprite[] animationFrames;

    /// <summary>
    /// 两张图片之间的切换间隔，单位为秒；仅在蜂群实际暂停时停止推进。
    /// </summary>
    [Tooltip("蜜蜂每一帧图片保持的秒数。")]
    [SerializeField, Min(0.01f)] private float frameInterval = 0.1f;

    /// <summary>
    /// 蜜蜂生成后从透明到正常不透明度的渐显时长；暂停期间不会推进。
    /// </summary>
    [Header("出生渐显")]
    [Tooltip("蜜蜂生成后渐显到正常不透明度所需的秒数；设为 0 可关闭渐显。")]
    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;

    /// <summary>
    /// 原始图片是否朝右，用于根据玩家所在方向翻转图片。
    /// </summary>
    [Tooltip("原始蜜蜂 Sprite 是否面向右侧。")]
    [SerializeField] private bool spriteFacesRight;

    #endregion

    #region Tracking Settings

    /// <summary>
    /// 蜜蜂正常追踪玩家时的目标线速度。
    /// </summary>
    [Header("追踪")]
    [Tooltip("蜜蜂追踪玩家时的目标速度。")]
    [SerializeField, Min(0f)] private float trackingSpeed = 5.5f;

    /// <summary>
    /// 当前线速度追向目标速度的最大加速度，值越高转向越迅速。
    /// </summary>
    [Tooltip("蜜蜂追踪和转向时的加速度。")]
    [SerializeField, Min(0f)] private float trackingAcceleration = 22f;

    /// <summary>
    /// 飞行方向上的横向摆动比例，使蜂群不会形成完全笔直的轨迹。
    /// </summary>
    [Tooltip("追踪方向附加的横向摆动幅度。")]
    [SerializeField, Min(0f)] private float wobbleAmplitude = 0.16f;

    /// <summary>
    /// 横向摆动频率，单位为每秒循环次数。
    /// </summary>
    [Tooltip("蜜蜂横向摆动的每秒循环次数。")]
    [SerializeField, Min(0f)] private float wobbleFrequency = 2.2f;

    /// <summary>
    /// 蜜蜂距离玩家小于该值时，开始把固定攻击通道偏角平滑收束到玩家中心。
    /// </summary>
    [Header("攻击路线")]
    [Tooltip("距离玩家小于该值时开始收束攻击通道；远于该值时保持完整偏角。")]
    [SerializeField, Min(0f)]
    private float attackRouteConvergeStartDistance = 1.2f;

    /// <summary>
    /// 蜜蜂距离玩家小于等于该值时，完全取消攻击通道偏角并直扑玩家。
    /// </summary>
    [Tooltip("距离玩家小于等于该值时完全直扑玩家；应小于开始收束距离。")]
    [SerializeField, Min(0f)]
    private float attackRouteDirectDistance = 0.45f;

    #endregion

    #region Impact Settings

    /// <summary>
    /// 撞到带 Dynamic Rigidbody2D 的画线或场景实体时，在接触点围绕飞行方向施加主冲量。
    /// 静态或 Kinematic 障碍仍会触发反冲，但不会被该冲量移动。
    /// </summary>
    [Header("碰撞推力（推力调这里）")]
    [Tooltip("推力调这里：蜜蜂撞击画线或其它动态刚体时施加的接触点冲量。")]
    [SerializeField, Min(0f)] private float impactImpulse = 350f;

    /// <summary>
    /// 每次撞击时允许主冲量相对入射方向随机偏转的最大角度，避免所有冲量完全一致。
    /// </summary>
    [Tooltip("主冲量相对蜜蜂入射方向的随机偏角范围（正负该角度）；建议保持较小。")]
    [SerializeField, Range(0f, 30f)]
    private float impactDirectionRandomAngleDegrees = 8f;

    /// <summary>
    /// 在主冲量之外额外施加的水平冲量；左右符号自动沿蜜蜂飞行或玩家所在方向。
    /// 该值用于让高质量画线获得足够明显的水平速度，不会替代沿飞行方向的主冲量。
    /// </summary>
    [Tooltip("额外水平冲量：质量较大的画线水平方向推不动时调这里。方向会自动沿蜜蜂飞行或玩家方向。")]
    [SerializeField, Min(0f)] private float horizontalImpactImpulse = 400f;

    /// <summary>
    /// 每次实体碰撞后，蜜蜂沿入射反方向需要退回的距离。
    /// </summary>
    [Tooltip("蜜蜂碰撞后沿反方向退回的距离。")]
    [SerializeField, Min(0f)] private float recoilDistance = 0.35f;

    /// <summary>
    /// 蜜蜂执行反冲时的线速度。
    /// </summary>
    [Tooltip("蜜蜂碰撞后退回时的速度。")]
    [SerializeField, Min(0f)] private float recoilSpeed = 9f;

    /// <summary>
    /// 同一只蜜蜂两次施力和反冲之间的最短间隔，避免持续接触每个物理帧重复施力。
    /// </summary>
    [Tooltip("蜜蜂连续碰撞可再次施力前的冷却时间。")]
    [SerializeField, Min(0f)] private float impactCooldown = 0.18f;

    #endregion

    #region Runtime State

    private DLevel53Hive _owner;
    private ActorCtrl _targetActor;
    private Transform _target;
    private Collider2D _targetCollider;
    private Collider2D _recoilCollider;
    private Vector2 _lastFlightDirection;
    private Vector2 _recoilDirection;
    private Vector2 _recoilStartPosition;
    private Vector2 _suspendedVelocity;
    private float _remainingRecoilDistance;
    private float _recoilElapsed;
    private float _recoilDurationLimit;
    private float _nextImpactTime;
    private float _animationElapsed;
    private float _fadeInElapsed;
    private float _wobblePhase;
    private float _attackAngleDegrees;
    private Color _visibleSpriteColor = Color.white;
    private int _animationFrameIndex;
    private bool _initialized;
    private bool _facesRight;
    private bool _isFadingIn;
    private bool _isRecoiling;
    private bool _isSuspended;

    /// <summary>
    /// 供 DLevel53Hive 建立蜂间忽略碰撞关系的唯一实体碰撞体。
    /// 只读访问，不允许外部替换 Prefab 上的 Collider2D。
    /// </summary>
    internal Collider2D HitCollider => hitCollider;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDestroy()
    {
        RestoreRecoilCollision();

        if (Application.isPlaying && _owner != null)
            _owner.UnregisterBee(this);
    }

    private void OnDisable()
    {
        RestoreRecoilCollision();
        _isRecoiling = false;
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void Update()
    {
        if (!_initialized || _isSuspended ||
            (_owner != null && _owner.IsBeeSimulationSuspended))
        {
            return;
        }

        UpdateFadeIn();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        if (!_initialized || body == null || !body.simulated)
            return;

        if (_isSuspended ||
            _owner == null ||
            _owner.IsBeeSimulationSuspended ||
            _target == null ||
            _targetActor == null)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        if (_isRecoiling)
        {
            UpdateRecoil();
            return;
        }

        UpdateTracking();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleCollision(collision);
    }

    #endregion

    #region Runtime API

    /// <summary>
    /// 由 DLevel53Hive 在实例化后绑定本次尝试的拥有者、一个随机玩家目标和固定攻击通道偏角。
    /// 未调用前蜜蜂不推进动画、追踪或碰撞结果。
    /// attackAngleDegrees 只在初始化时写入；正值逆时针偏转，负值顺时针偏转。
    /// </summary>
    internal void Initialize(
        DLevel53Hive owner,
        ActorCtrl targetActor,
        float attackAngleDegrees)
    {
        ResolveReferences();
        _owner = owner;
        _targetActor = targetActor;
        _target = targetActor != null ? targetActor.transform : null;
        _targetCollider = ResolveTargetCollider(targetActor);
        _attackAngleDegrees = attackAngleDegrees;
        _wobblePhase = Random.Range(0f, Mathf.PI * 2f);
        _animationElapsed = Random.Range(0f, Mathf.Max(0.01f, frameInterval));
        _initialized = owner != null && targetActor != null;

        if (spriteRenderer != null)
        {
            _visibleSpriteColor = spriteRenderer.color;
            _fadeInElapsed = 0f;
            _isFadingIn = fadeInDuration > 0f && _visibleSpriteColor.a > 0f;
            SetSpriteAlpha(_isFadingIn ? 0f : _visibleSpriteColor.a);
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.WakeUp();
        }

        RefreshFacing();
    }

    /// <summary>
    /// 暂停或恢复单只蜜蜂的物理模拟与两帧动画。
    /// 暂停时缓存速度并关闭 Rigidbody2D.simulated，避免全局 Physics2D 仍推进碰撞。
    /// </summary>
    internal void SetSuspended(bool suspended)
    {
        if (_isSuspended == suspended || body == null)
            return;

        _isSuspended = suspended;
        if (suspended)
        {
            _suspendedVelocity = body.linearVelocity;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
            return;
        }

        body.simulated = true;
        body.linearVelocity = _suspendedVelocity;
        body.WakeUp();
    }

    #endregion

    #region Animation And Tracking

    private void UpdateFadeIn()
    {
        if (!_isFadingIn || spriteRenderer == null)
            return;

        float duration = Mathf.Max(0f, fadeInDuration);
        if (duration <= 0f)
        {
            SetSpriteAlpha(_visibleSpriteColor.a);
            _isFadingIn = false;
            return;
        }

        _fadeInElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_fadeInElapsed / duration);
        SetSpriteAlpha(
            _visibleSpriteColor.a * Mathf.SmoothStep(0f, 1f, progress));
        _isFadingIn = progress < 1f;
    }

    private void SetSpriteAlpha(float alpha)
    {
        if (spriteRenderer == null)
            return;

        Color color = _visibleSpriteColor;
        color.a = alpha;
        spriteRenderer.color = color;
    }

    private void UpdateAnimation()
    {
        if (spriteRenderer == null ||
            animationFrames == null ||
            animationFrames.Length == 0)
        {
            return;
        }

        _animationElapsed += Time.deltaTime;
        float interval = Mathf.Max(0.01f, frameInterval);
        while (_animationElapsed >= interval)
        {
            _animationElapsed -= interval;
            _animationFrameIndex =
                (_animationFrameIndex + 1) % animationFrames.Length;
            Sprite frame = animationFrames[_animationFrameIndex];
            if (frame != null)
                spriteRenderer.sprite = frame;
        }
    }

    private void UpdateTracking()
    {
        Vector2 toTarget = (Vector2)_target.position - body.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            body.linearVelocity = Vector2.MoveTowards(
                body.linearVelocity,
                Vector2.zero,
                trackingAcceleration * Time.fixedDeltaTime);
            return;
        }

        float targetDistance = toTarget.magnitude;
        Vector2 direction = toTarget / targetDistance;
        float convergeStartDistance = Mathf.Max(
            attackRouteConvergeStartDistance,
            attackRouteDirectDistance);
        float directDistance = Mathf.Min(
            attackRouteConvergeStartDistance,
            attackRouteDirectDistance);
        float attackRouteWeight =
            convergeStartDistance - directDistance > 0.0001f
                ? Mathf.InverseLerp(
                    directDistance,
                    convergeStartDistance,
                    targetDistance)
                : targetDistance > convergeStartDistance ? 1f : 0f;
        attackRouteWeight = Mathf.SmoothStep(0f, 1f, attackRouteWeight);
        float attackAngle =
            _attackAngleDegrees * attackRouteWeight * Mathf.Deg2Rad;
        float attackAngleCos = Mathf.Cos(attackAngle);
        float attackAngleSin = Mathf.Sin(attackAngle);
        direction = new Vector2(
            direction.x * attackAngleCos - direction.y * attackAngleSin,
            direction.x * attackAngleSin + direction.y * attackAngleCos);

        Vector2 perpendicular = new(-direction.y, direction.x);
        float wobble = Mathf.Sin(
            (Time.time * wobbleFrequency * Mathf.PI * 2f) + _wobblePhase);
        direction = (direction + perpendicular * (wobble * wobbleAmplitude)).normalized;
        _lastFlightDirection = direction;
        Vector2 targetVelocity = direction * trackingSpeed;
        body.linearVelocity = Vector2.MoveTowards(
            body.linearVelocity,
            targetVelocity,
            trackingAcceleration * Time.fixedDeltaTime);
        RefreshFacing();
    }

    private void RefreshFacing()
    {
        if (spriteRenderer == null || body == null || _target == null)
            return;

        Vector2 aimDirection = ResolveAimPoint() - body.position;
        if (aimDirection.sqrMagnitude <= 0.0001f)
            return;

        aimDirection.Normalize();
        if (aimDirection.x > 0.02f)
            _facesRight = true;
        else if (aimDirection.x < -0.02f)
            _facesRight = false;

        spriteRenderer.flipX = spriteFacesRight
            ? !_facesRight
            : _facesRight;

        Vector2 spriteHeadAxis = _facesRight ? Vector2.right : Vector2.left;
        float facingAngle = Vector2.SignedAngle(spriteHeadAxis, aimDirection);
        body.angularVelocity = 0f;
        body.SetRotation(facingAngle);
    }

    #endregion

    #region Collision And Recoil

    private void HandleCollision(Collision2D collision)
    {
        if (!_initialized ||
            _isSuspended ||
            _owner == null ||
            _owner.IsBeeSimulationSuspended ||
            collision == null)
        {
            return;
        }

        Collider2D otherCollider = ResolveOtherCollider(collision);
        if (otherCollider == null || otherCollider.isTrigger)
            return;

        if (_owner.IsCurrentPlayerCollider(otherCollider))
        {
            _owner.TryHandlePlayerHit(this, collision, otherCollider);
            return;
        }

        if (_isRecoiling || Time.time < _nextImpactTime)
            return;

        // Physics2D 可能在回调前已经改变当前速度；使用本次碰撞前缓存的追踪方向，
        // 让碰撞物的主冲量只在入射方向附近随机，反冲仍稳定取原入射反向。
        Vector2 flightDirection = _lastFlightDirection;
        if (flightDirection.sqrMagnitude <= 0.0001f && _target != null)
        {
            flightDirection =
                ((Vector2)_target.position - (Vector2)transform.position).normalized;
        }

        if (flightDirection.sqrMagnitude <= 0.0001f)
            return;

        flightDirection.Normalize();
        float randomImpactAngle = Random.Range(
            -Mathf.Max(0f, impactDirectionRandomAngleDegrees),
            Mathf.Max(0f, impactDirectionRandomAngleDegrees));
        Vector2 impactDirection =
            RotateDirection(flightDirection, randomImpactAngle);
        float horizontalDirection = ResolveHorizontalImpactDirection(
            flightDirection);
        Vector2 impactForce =
            impactDirection * Mathf.Max(0f, impactImpulse) +
            Vector2.right * (horizontalDirection *
                Mathf.Max(0f, horizontalImpactImpulse));

        Vector2 contactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : otherCollider.ClosestPoint(transform.position);
        Rigidbody2D impactedBody = otherCollider.attachedRigidbody;
        if (impactedBody != null &&
            impactedBody != body &&
            impactedBody.simulated &&
            impactedBody.bodyType == RigidbodyType2D.Dynamic)
        {
            impactedBody.AddForceAtPosition(
                impactForce,
                contactPoint,
                ForceMode2D.Impulse);
            impactedBody.WakeUp();
        }

        _nextImpactTime = Time.time + Mathf.Max(0f, impactCooldown);
        _recoilDirection = -flightDirection;
        _recoilStartPosition = body.position;
        _remainingRecoilDistance = Mathf.Max(0f, recoilDistance);
        _recoilElapsed = 0f;
        _recoilDurationLimit = recoilSpeed > 0f
            ? _remainingRecoilDistance / recoilSpeed + Time.fixedDeltaTime * 2f
            : 0f;
        _isRecoiling = _remainingRecoilDistance > 0f && recoilSpeed > 0f;
        BeginRecoilCollisionIgnore(otherCollider);
        body.linearVelocity = _isRecoiling
            ? _recoilDirection * recoilSpeed
            : Vector2.zero;

        if (!_isRecoiling)
            RestoreRecoilCollision();
    }

    private float ResolveHorizontalImpactDirection(Vector2 flightDirection)
    {
        if (Mathf.Abs(flightDirection.x) > 0.01f)
            return Mathf.Sign(flightDirection.x);

        if (_target != null)
        {
            float targetOffsetX = _target.position.x - transform.position.x;
            if (Mathf.Abs(targetOffsetX) > 0.01f)
                return Mathf.Sign(targetOffsetX);
        }

        return spriteRenderer != null && spriteRenderer.flipX
            ? (spriteFacesRight ? -1f : 1f)
            : (spriteFacesRight ? 1f : -1f);
    }

    private static Vector2 RotateDirection(Vector2 direction, float angleDegrees)
    {
        float angle = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos).normalized;
    }

    private void UpdateRecoil()
    {
        RefreshFacing();
        _recoilElapsed += Time.fixedDeltaTime;
        float traveledDistance = Mathf.Max(
            0f,
            Vector2.Dot(body.position - _recoilStartPosition, _recoilDirection));
        _remainingRecoilDistance = Mathf.Max(0f, recoilDistance - traveledDistance);
        body.linearVelocity = _recoilDirection * recoilSpeed;

        if (_remainingRecoilDistance > 0f && _recoilElapsed < _recoilDurationLimit)
            return;

        _isRecoiling = false;
        body.linearVelocity = Vector2.zero;
        RestoreRecoilCollision();
    }

    private void BeginRecoilCollisionIgnore(Collider2D otherCollider)
    {
        RestoreRecoilCollision();
        if (hitCollider == null || otherCollider == null ||
            Physics2D.GetIgnoreCollision(hitCollider, otherCollider))
        {
            return;
        }

        _recoilCollider = otherCollider;
        Physics2D.IgnoreCollision(hitCollider, _recoilCollider, true);
    }

    private void RestoreRecoilCollision()
    {
        if (hitCollider != null && _recoilCollider != null)
            Physics2D.IgnoreCollision(hitCollider, _recoilCollider, false);

        _recoilCollider = null;
    }

    private Collider2D ResolveOtherCollider(Collision2D collision)
    {
        if (collision.collider != null && collision.collider != hitCollider)
            return collision.collider;

        if (collision.otherCollider != null &&
            collision.otherCollider != hitCollider)
        {
            return collision.otherCollider;
        }

        return null;
    }

    #endregion

    #region References

    private Vector2 ResolveAimPoint()
    {
        if (_targetCollider != null &&
            _targetCollider.enabled &&
            _targetCollider.gameObject.activeInHierarchy)
        {
            return _targetCollider.bounds.center;
        }

        return _target != null ? (Vector2)_target.position : body.position;
    }

    private static Collider2D ResolveTargetCollider(ActorCtrl targetActor)
    {
        if (targetActor == null)
            return null;

        Collider2D rootCollider = targetActor.GetComponent<Collider2D>();
        if (IsUsableTargetCollider(rootCollider))
            return rootCollider;

        Rigidbody2D targetBody = targetActor.GetComponent<Rigidbody2D>();
        Collider2D[] colliders =
            targetActor.GetComponentsInChildren<Collider2D>(true);
        Collider2D largestCollider = null;
        float largestArea = -1f;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (!IsUsableTargetCollider(candidate) ||
                (targetBody != null && candidate.attachedRigidbody != targetBody))
            {
                continue;
            }

            Vector2 size = candidate.bounds.size;
            float area = size.x * size.y;
            if (area <= largestArea)
                continue;

            largestArea = area;
            largestCollider = candidate;
        }

        return largestCollider;
    }

    private static bool IsUsableTargetCollider(Collider2D collider)
    {
        return collider != null &&
               collider.enabled &&
               !collider.isTrigger &&
               collider.gameObject.activeInHierarchy;
    }

    private void ResolveReferences()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (hitCollider == null)
            hitCollider = GetComponent<Collider2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    #endregion
}

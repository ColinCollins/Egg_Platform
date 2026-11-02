using DG.Tweening;
using UnityEngine;

/// <summary>
/// IWanner 模式角色控制器，将共享 ActorCtrl 的基础移动状态映射到 Qboxy Spine 的本地动作。
/// 调用边界：仅用于使用 sp_qboxy_001/Qboxy 骨骼资源的 IWanner 角色；移动和死亡继续复用 ActorCtrl，跳跃默认增加接地门禁并可显式开启无限跳跃，
/// 站在带 Rigidbody2D 的 Ground 上时合成支撑刚体水平速度，支持局部机关叠加衰减的外部水平速度，
/// 为标记为 SingleSidePlatform 的单向平台补充重力方向接触确认，并可按实例开启贴墙下落限速与连续蹬墙跳。
/// </summary>
public sealed class Actor_IWanner : ActorCtrl
{
    #region Constants

    private const int MaxGroundContactCount = 8;
    private const float MinimumSupportNormalY = 0.5f;
    private const float DeathScaleDuration = 0.2f;
    private const float DeathScaleMultiplier = 1.2f;
    private const string SingleSidePlatformTag = "SingleSidePlatform";
    private const float SingleSidePlatformProbeDistance = 0.05f;
    private const float SingleSidePlatformProbeHeight = 0.02f;
    private const float SingleSidePlatformProbeWidthRatio = 0.8f;
    private const float SingleSidePlatformSurfaceTolerance = 0.03f;
    private const float MinimumWallNormalAlignment = 0.8f;

    #endregion

    #region Spine Animation Names

    private const string IdleAnimationName = "idle";
    private const string WalkAnimationName = "walk";
    private const string JumpAnimationName = "jump";
    private const string DistortionAnimationName = "distortion";
    private const string DistortionReverseAnimationName = "distortion_reverse";

    #endregion

    #region Runtime State

    private bool _hasJumpedSinceLastLanding;
    private Spine.TrackEntry _distortionTrackEntry;
    private readonly ContactPoint2D[] _groundContacts = new ContactPoint2D[MaxGroundContactCount];
    private ContactFilter2D _groundContactFilter;
    private bool _isGroundContactFilterReady;
    private Sequence _deathScaleSequence;
    private Collider2D _actorCollider;
    private int _groundLayerMask;
    private bool _isGroundCheckDirectionLocked;
    private Vector2 _lockedGroundCheckDirection = Vector2.down;
    private float _externalHorizontalVelocity;

    #endregion

    #region IWanner Physics Settings

    /// <summary>
    /// 用途：限制 IWanner 角色沿当前重力方向的最大坠落速度，支持 AreaEffector2D 提供的世界方向。
    /// 调用边界：由当前 Actor_IWanner 的 FixedUpdate 根据 GroundCheckDirection 应用，只移除超出上限的重力方向速度分量。
    /// 注意事项：仍按小于或等于 0 的值配置；大于 0 时按 0 处理，默认值为 -5，其绝对值作为沿重力方向的速度上限。
    /// </summary>
    [Header("IWanner 物理设置")]
    [Tooltip("角色沿当前 Ground/重力方向允许的最大坠落速度，应填写小于或等于 0 的值；默认 -5。")]
    [SerializeField] private float maximumFallSpeed = -5f;

    /// <summary>
    /// 用途：控制当前 IWanner 角色实例是否启用贴墙下落限速与连续蹬墙跳。
    /// 调用边界：由 Actor_IWanner prefab 或关卡实例在 Inspector 配置，只在与 Ground Layer 侧面产生真实物理接触时生效。
    /// 注意事项：默认关闭以保持既有关卡行为；每次离散跳跃输入都可在仍贴墙时重新起跳，不额外施加水平反冲。
    /// </summary>
    [Tooltip("开启后，角色贴 Ground Layer 墙面时限制下落速度，并允许每次离散输入继续蹬墙跳")]
    [SerializeField] private bool isFrictionToWall;

    /// <summary>
    /// 用途：配置贴墙时沿当前重力方向允许的最大下落速度。
    /// 调用边界：只在 isFrictionToWall 开启且确认侧面接触时由 FixedUpdate 应用。
    /// 注意事项：按小于或等于 0 的值配置，默认 -2；0 表示贴墙时不允许继续沿重力方向下落。
    /// </summary>
    [Tooltip("贴墙时沿当前 Ground/重力方向的最大下落速度，应小于或等于 0；默认 -2")]
    [SerializeField] private float frictionToWallFallSpeed = -2f;

    /// <summary>
    /// 用途：配置机关叠加给 IWanner Actor 的外部水平速度每秒衰减量。
    /// 调用边界：只影响 AddDecayingHorizontalVelocity() 累积的速度，不改变玩家输入、移动平台或共享 moveSpeed。
    /// 注意事项：单位为世界单位/秒²；默认 80，配置为 0 时外部速度在下一次 Actor 更新后清除。
    /// </summary>
    [Tooltip("外部水平速度的衰减量（世界单位/秒²）；默认 80")]
    [Min(0f)]
    [SerializeField] private float externalHorizontalVelocityDeceleration = 80f;

    #endregion

    #region External Horizontal Velocity

    /// <summary>
    /// 用途：为当前 IWanner Actor 叠加一段会自动衰减的外部水平速度。
    /// 调用边界：仅供 Spring 等 IWanner 局部机关在运行时调用；传入值是速度增量，不是 Force 或 Impulse。
    /// 注意事项：多次调用会累加；玩家输入与移动平台速度仍由基础流程计算，死亡、禁用或触墙会清除此附加量。
    /// </summary>
    /// <param name="velocityDelta">要叠加的世界空间 X 速度，正值向右、负值向左。</param>
    internal void AddDecayingHorizontalVelocity(float velocityDelta)
    {
        if (!Application.isPlaying
            || RB == null
            || !RB.simulated
            || RB.bodyType != RigidbodyType2D.Dynamic
            || Mathf.Approximately(velocityDelta, 0f))
        {
            return;
        }

        _externalHorizontalVelocity += velocityDelta;
        RB.linearVelocityX += velocityDelta;
    }

    private void ApplyDecayingHorizontalVelocity()
    {
        if (Mathf.Approximately(_externalHorizontalVelocity, 0f))
            return;

        if (RB == null || !RB.simulated || IsDied || IsKnocked || IsSit)
        {
            ClearDecayingHorizontalVelocity();
            return;
        }

        if (IsWallDetected)
        {
            ClearDecayingHorizontalVelocity();
            RB.linearVelocityX = 0f;
            return;
        }

        RB.linearVelocityX += _externalHorizontalVelocity;

        float effectiveDeceleration = Mathf.Max(0f, externalHorizontalVelocityDeceleration);
        _externalHorizontalVelocity = effectiveDeceleration > 0f
            ? Mathf.MoveTowards(
                _externalHorizontalVelocity,
                0f,
                effectiveDeceleration * Time.deltaTime)
            : 0f;
    }

    private void ClearDecayingHorizontalVelocity()
    {
        _externalHorizontalVelocity = 0f;
    }

    #endregion

    #region Collision Control

    /// <summary>
    /// 用途：运行时修改当前 IWanner 角色实例向下 BoxCast 的地面检测框尺寸。
    /// 调用边界：供 IWanner 关卡本地逻辑或 UnityEvent 调用；编辑态调用不会修改 prefab 序列化值。
    /// 注意事项：X/Y 负值分别按 0 处理；只影响当前 Actor_IWanner 实例，后续 HandleCollision 从下一次检测开始使用新值。
    /// </summary>
    /// <param name="size">新的地面检测框尺寸，单位为 Unity 世界单位。</param>
    public void SetGroundCheckSize(Vector2 size)
    {
        if (!Application.isPlaying)
            return;

        groundCheckSize = new Vector2(
            Mathf.Max(0f, size.x),
            Mathf.Max(0f, size.y));
    }

    /// <summary>
    /// 用途：读取当前 IWanner 角色实例正在使用的地面检测框尺寸。
    /// 调用边界：仅供同程序集内的 IWanner 局部 Listener 计算平滑变化起点，不作为 UnityEvent 调用入口。
    /// 注意事项：返回运行时当前值；读取本身不触发碰撞检测、存档或资源链路。
    /// </summary>
    internal Vector2 GetGroundCheckSize()
    {
        return groundCheckSize;
    }

    /// <summary>
    /// 用途：运行时修改当前 IWanner 角色实例向下 BoxCast 的地面检测距离。
    /// 调用边界：供 IWanner 关卡本地逻辑或 UnityEvent 调用；编辑态调用不会修改 prefab 序列化值。
    /// 注意事项：负值按 0 处理；只影响当前 Actor_IWanner 实例，后续 HandleCollision 从下一次检测开始使用新值。
    /// </summary>
    /// <param name="distance">新的地面检测距离，单位为 Unity 世界单位。</param>
    public void SetGroundCheckDistance(float distance)
    {
        if (!Application.isPlaying)
            return;

        GroundCheckDistance = Mathf.Max(0f, distance);
    }

    /// <summary>
    /// 用途：读取当前 IWanner 角色实例正在使用的地面检测距离。
    /// 调用边界：仅供同程序集内的 IWanner 局部 Listener 计算平滑变化起点，不作为 UnityEvent 调用入口。
    /// 注意事项：返回运行时当前值；读取本身不触发碰撞检测、存档或资源链路。
    /// </summary>
    internal float GetGroundCheckDistance()
    {
        return GroundCheckDistance;
    }

    /// <summary>
    /// 用途：锁定当前 IWanner 角色正在使用的 Ground 检测方向，使后续重力归零时仍保留最后的承载面方向。
    /// 调用边界：仅供需要从正/反重力切换到无重力阶段的 IWanner 关卡运行时逻辑调用。
    /// 注意事项：锁定的是世界向上或向下方向，不随角色 Transform 旋转改变；当前 Actor 实例销毁前持续有效。
    /// </summary>
    public void PreserveCurrentGroundCheckDirection()
    {
        if (!Application.isPlaying)
            return;

        SetGroundCheckDirection(GroundCheckDirection);
    }

    /// <summary>
    /// 用途：显式设置当前 IWanner 角色的世界重力与 Ground 检测方向。
    /// 调用边界：供 AreaEffector2D 等不使用 Rigidbody2D.gravityScale 的 IWanner 局部重力控制器在运行时同步方向。
    /// 注意事项：零向量会被忽略；设置后覆盖 gravityScale 推断，最大坠落速度也沿该方向限制。
    /// </summary>
    /// <param name="gravityDirection">归一化前的世界重力方向，同时也是 Ground BoxCast 查找承载面的方向。</param>
    public void SetGroundCheckDirection(Vector2 gravityDirection)
    {
        if (!Application.isPlaying || gravityDirection.sqrMagnitude <= 0.0001f)
            return;

        _lockedGroundCheckDirection = gravityDirection.normalized;
        _isGroundCheckDirectionLocked = true;
    }

    #endregion

    #region Jump Control

    /// <summary>
    /// 用途：控制当前 IWanner 角色是否允许在离地后继续响应每次跳跃请求。
    /// 调用边界：由 Actor_IWanner prefab 或关卡实例在 Inspector 中配置；只影响当前组件实例。
    /// 注意事项：默认关闭以保持每次落地周期只跳一次；开启后仍遵守角色坐下、StopMoving 和 Rigidbody2D 模拟状态门禁。
    /// </summary>
    [Header("IWanner 跳跃设置")]
    [Tooltip("开启后允许角色在空中重复跳跃；默认关闭，保持每次落地周期只跳一次")]
    [SerializeField] private bool enableInfiniteJump;

    /// <summary>
    /// 用途：按当前实例配置执行一次 IWanner 跳跃；默认仅允许接地跳，开启无限跳或正在有效摩擦墙面上时允许空中重复跳跃。
    /// 调用边界：由 IWannerLevelCtrl 收到离散 PlayerJumpEvent 后调用；其它玩法不得依赖本地规则。
    /// 注意事项：无限跳与贴墙条件均不成立时，每次接触承载面周期只接受一次；墙跳不额外施加水平反冲。
    /// </summary>
    public void TriggerGroundedJump()
    {
        if (enableInfiniteJump)
        {
            TriggerAirJump();
            return;
        }

        if (IsTouchingFrictionWall(GroundCheckDirection.normalized))
        {
            TriggerAirJump();
            return;
        }

        bool isStandingOnSingleSidePlatform = IsStandingOnSingleSidePlatform();
        if (isStandingOnSingleSidePlatform && _hasJumpedSinceLastLanding)
            HandleLanding();

        if ((!IsGround && !isStandingOnSingleSidePlatform) || _hasJumpedSinceLastLanding)
            return;

        base.JumpButton();
        _hasJumpedSinceLastLanding = RB != null && RB.linearVelocityY > 0f;
    }

    private void TriggerAirJump()
    {
        if (IsSit || !CanMove || RB == null || !RB.simulated)
            return;

        Jump();
        _hasJumpedSinceLastLanding = true;
    }

    #endregion

    #region Action Control

    /// <summary>
    /// 用途：触发 IWanner 角色的 distortion 一次性动作，并在动作完成后恢复移动状态动画。
    /// 调用边界：仅供运行时 IWanner 玩法调用；不改变移动、输入、碰撞或死亡状态。
    /// 注意事项：重复调用会从头重播 distortion；死亡状态或 Spine 引用缺失时不执行。
    /// </summary>
    public void PlayDistortion()
    {
        PlayDistortionTracked();
    }

    /// <summary>
    /// 用途：播放消失用 distortion 或出现用 distortion_reverse，并返回本次 Spine 轨道，供 IWanner 局部流程等待完整完成或识别中断。
    /// 调用边界：仅供同程序集内需要串联动作时序的 IWanner 逻辑调用；普通调用继续使用 PlayDistortion()。
    /// 注意事项：两个动画资源都按正向播放；播放条件不满足或 Spine 未返回有效轨道时返回 null。
    /// </summary>
    /// <param name="useReverseAnimation">为 true 时播放 distortion_reverse，否则播放 distortion。</param>
    internal Spine.TrackEntry PlayDistortionTracked(bool useReverseAnimation = false)
    {
        if (!Application.isPlaying || IsDied || SpineCtrl == null)
            return null;

        ClearDistortionTracking();
        string animationName = useReverseAnimation
            ? DistortionReverseAnimationName
            : DistortionAnimationName;
        var trackEntry = SpineCtrl.PlayAnimation(animationName, false, false);
        if (trackEntry == null)
            return null;

        _distortionTrackEntry = trackEntry;
        trackEntry.Complete += HandleDistortionFinished;
        trackEntry.Interrupt += HandleDistortionFinished;
        return trackEntry;
    }

    private void HandleDistortionFinished(Spine.TrackEntry trackEntry)
    {
        if (_distortionTrackEntry != trackEntry)
            return;

        ClearDistortionTracking();
    }

    private void ClearDistortionTracking()
    {
        if (_distortionTrackEntry == null)
            return;

        _distortionTrackEntry.Complete -= HandleDistortionFinished;
        _distortionTrackEntry.Interrupt -= HandleDistortionFinished;
        _distortionTrackEntry = null;
    }

    #endregion

    #region ActorCtrl Overrides

    /// <summary>
    /// 用途：让 IWanner 角色的共享 Ground BoxCast 沿当前重力方向或显式同步方向检测承载面。
    /// 调用边界：由 ActorCtrl 的碰撞检测与 Gizmo 绘制读取；只影响当前 Actor_IWanner 实例。
    /// 注意事项：显式设置后优先返回保存方向；未设置时负重力返回向上，其余情况保持向下。
    /// </summary>
    protected override Vector2 GroundCheckDirection =>
        _isGroundCheckDirectionLocked
            ? _lockedGroundCheckDirection
            : RB != null && RB.gravityScale < 0f
                ? Vector2.up
                : Vector2.down;

    /// <summary>
    /// 完成共享 Actor 初始化后，在运行时缓存重力方向单向平台检测所需的 Collider 与 Ground LayerMask。
    /// </summary>
    protected override void Start()
    {
        base.Start();
        _actorCollider = GetComponent<Collider2D>();
        _groundLayerMask = LayerMask.GetMask("Ground");
    }

    private void OnDisable()
    {
        ClearDecayingHorizontalVelocity();
    }

    /// <summary>
    /// 执行共享 Actor 更新后，先合成移动 Ground 速度，再叠加并衰减机关提供的外部水平速度。
    /// 调用边界：由 IWannerLevelCtrl 逐帧调用，仅影响 Actor_IWanner；静态 Ground、离地、死亡和击退状态不合成速度。
    /// 注意事项：Ground 必须位于 Ground Layer；外部速度在基础输入与平台速度之后叠加，触墙时清除。
    /// </summary>
    public override void OnUpdate()
    {
        base.OnUpdate();
        RefreshSingleSidePlatformLanding();
        ComposeMovingGroundVelocity();
        ApplyDecayingHorizontalVelocity();
    }

    private void FixedUpdate()
    {
        ClampFallSpeed();
    }

    /// <summary>
    /// IWanner 角色死亡时沿用共享死亡表现，关闭角色根节点的 Collider2D 检测，并播放一次 0.2 秒的缩放反馈。
    /// 调用边界：所有经 ActorCtrl 引用触发的死亡都会多态进入本实现；当前关卡实例销毁前不恢复碰撞器。
    /// 注意事项：缩放反馈从死亡当帧尺寸放大到 1.2 倍后恢复，不改变死亡结算、Spine 动画或物理状态。
    /// </summary>
    public override void Die()
    {
        ClearDistortionTracking();
        ClearDecayingHorizontalVelocity();
        base.Die();

        Collider2D actorCollider = GetComponent<Collider2D>();
        if (actorCollider != null)
            actorCollider.enabled = false;

        PlayDeathScaleAnimation();
    }

    private void PlayDeathScaleAnimation()
    {
        _deathScaleSequence?.Kill();

        Vector3 originalScale = transform.localScale;
        float halfDuration = DeathScaleDuration * 0.5f;
        _deathScaleSequence = DOTween.Sequence()
            .Append(transform.DOScale(originalScale * DeathScaleMultiplier, halfDuration).SetEase(Ease.OutQuad))
            .Append(transform.DOScale(originalScale, halfDuration).SetEase(Ease.InQuad))
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnKill(() => _deathScaleSequence = null);
    }

    /// <summary>
    /// 落地时开放下一次 IWanner 地面跳跃；共享落地状态与可选能力仍由 ActorCtrl 维护。
    /// </summary>
    protected override void HandleLanding()
    {
        base.HandleLanding();
        _hasJumpedSinceLastLanding = false;
    }

    /// <summary>
    /// 根据 IWanner 角色的移动与落地状态切换待机、走动和跳跃动画；死亡动画由 ActorCtrl.Die() 播放 Qboxy 的 die。
    /// </summary>
    protected override void HandleAnimators()
    {
        if (IsDied || IsKicking || IsSit || _distortionTrackEntry != null || RB == null || SpineCtrl == null)
            return;

        if (!IsGround)
        {
            SpineCtrl.PlayAnimation(JumpAnimationName, false);
            return;
        }

        SpineCtrl.PlayAnimation(XInput != 0f ? WalkAnimationName : IdleAnimationName, true);
    }

    #endregion

    #region Single Side Platform

    private void RefreshSingleSidePlatformLanding()
    {
        if (!_hasJumpedSinceLastLanding || !IsStandingOnSingleSidePlatform())
            return;

        HandleLanding();
    }

    private bool IsStandingOnSingleSidePlatform()
    {
        Vector2 groundDirection = GroundCheckDirection;
        if (!Application.isPlaying
            || RB == null
            || !RB.simulated
            || Vector2.Dot(RB.linearVelocity, groundDirection) < -0.01f
            || _groundLayerMask == 0)
        {
            return false;
        }

        if (_actorCollider == null)
            _actorCollider = GetComponent<Collider2D>();
        if (_actorCollider == null || !_actorCollider.enabled)
            return false;

        Bounds actorBounds = _actorCollider.bounds;
        Vector2 probeSize = new Vector2(
            Mathf.Max(0.01f, actorBounds.size.x * SingleSidePlatformProbeWidthRatio),
            SingleSidePlatformProbeHeight);
        bool isReverseGravity = groundDirection.y > 0f;
        Vector2 probeOrigin = new Vector2(
            actorBounds.center.x,
            isReverseGravity
                ? actorBounds.max.y - SingleSidePlatformProbeHeight * 0.5f
                : actorBounds.min.y + SingleSidePlatformProbeHeight * 0.5f);
        RaycastHit2D platformHit = Physics2D.BoxCast(
            probeOrigin,
            probeSize,
            0f,
            groundDirection,
            SingleSidePlatformProbeDistance,
            _groundLayerMask);

        if (platformHit.collider == null)
            return false;

        float distanceToPlatformSurface = isReverseGravity
            ? platformHit.collider.bounds.min.y - actorBounds.max.y
            : actorBounds.min.y - platformHit.collider.bounds.max.y;
        return distanceToPlatformSurface >= -SingleSidePlatformSurfaceTolerance
            && distanceToPlatformSurface <= SingleSidePlatformSurfaceTolerance;
    }

    #endregion

    #region Fall Speed

    private void ClampFallSpeed()
    {
        if (RB == null || !RB.simulated)
            return;

        Vector2 gravityDirection = GroundCheckDirection.normalized;
        float configuredFallSpeed = IsTouchingFrictionWall(gravityDirection)
            ? frictionToWallFallSpeed
            : maximumFallSpeed;
        float maximumSpeedAlongGravity = -Mathf.Min(0f, configuredFallSpeed);
        float speedAlongGravity = Vector2.Dot(RB.linearVelocity, gravityDirection);
        if (speedAlongGravity <= maximumSpeedAlongGravity)
            return;

        RB.linearVelocity -= gravityDirection * (speedAlongGravity - maximumSpeedAlongGravity);
    }

    private bool IsTouchingFrictionWall(Vector2 gravityDirection)
    {
        if (!isFrictionToWall
            || RB == null
            || !RB.simulated
            || IsDied
            || IsKnocked
            || IsSit)
        {
            return false;
        }

        EnsureGroundContactFilter();
        int contactCount = RB.GetContacts(_groundContactFilter, _groundContacts);
        Vector2 wallAxis = Vector2.Perpendicular(gravityDirection);
        for (int index = 0; index < contactCount; index++)
        {
            Vector2 actorFacingNormal = GetActorFacingNormal(_groundContacts[index]);
            if (actorFacingNormal.sqrMagnitude <= 0.0001f)
                continue;

            float wallAlignment = Mathf.Abs(Vector2.Dot(actorFacingNormal.normalized, wallAxis));
            if (wallAlignment >= MinimumWallNormalAlignment)
                return true;
        }

        return false;
    }

    #endregion

    #region Moving Ground

    private void ComposeMovingGroundVelocity()
    {
        if (!Application.isPlaying
            || RB == null
            || !RB.simulated
            || !IsGround
            || IsDied
            || IsKnocked
            || IsSit
            || RB.linearVelocityY > 0.01f)
        {
            return;
        }

        Rigidbody2D groundBody = FindSupportingGroundBody();
        if (groundBody == null
            || !groundBody.simulated
            || groundBody.bodyType == RigidbodyType2D.Static)
        {
            return;
        }

        float relativeVelocityX = IsWallDetected ? 0f : XInput * MoveSpeed;
        RB.linearVelocity = new Vector2(
            relativeVelocityX + groundBody.linearVelocityX,
            RB.linearVelocityY);
    }

    private Rigidbody2D FindSupportingGroundBody()
    {
        EnsureGroundContactFilter();
        int contactCount = RB.GetContacts(_groundContactFilter, _groundContacts);
        Rigidbody2D bestGroundBody = null;
        float bestSupportNormalY = MinimumSupportNormalY;

        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = _groundContacts[i];
            Vector2 actorFacingNormal = GetActorFacingNormal(contact);
            if (actorFacingNormal.y < bestSupportNormalY)
                continue;

            Collider2D supportCollider = GetSupportCollider(contact);
            Rigidbody2D supportBody = supportCollider != null ? supportCollider.attachedRigidbody : null;
            if (supportBody == null
                || supportBody == RB
                || supportBody.bodyType == RigidbodyType2D.Static)
            {
                continue;
            }

            bestGroundBody = supportBody;
            bestSupportNormalY = actorFacingNormal.y;
        }

        return bestGroundBody;
    }

    private void EnsureGroundContactFilter()
    {
        if (_isGroundContactFilterReady)
            return;

        _groundContactFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true,
            layerMask = LayerMask.GetMask("Ground")
        };
        _isGroundContactFilterReady = true;
    }

    private Vector2 GetActorFacingNormal(ContactPoint2D contact)
    {
        Vector2 normal = contact.normal;
        bool colliderBelongsToActor = contact.collider != null && contact.collider.attachedRigidbody == RB;
        bool otherColliderBelongsToActor = contact.otherCollider != null && contact.otherCollider.attachedRigidbody == RB;

        if (colliderBelongsToActor && !otherColliderBelongsToActor)
            normal = -normal;

        return normal;
    }

    private Collider2D GetSupportCollider(ContactPoint2D contact)
    {
        bool colliderBelongsToActor = contact.collider != null && contact.collider.attachedRigidbody == RB;
        bool otherColliderBelongsToActor = contact.otherCollider != null && contact.otherCollider.attachedRigidbody == RB;

        if (colliderBelongsToActor == otherColliderBelongsToActor)
            return null;

        return colliderBelongsToActor ? contact.otherCollider : contact.collider;
    }

    #endregion
}

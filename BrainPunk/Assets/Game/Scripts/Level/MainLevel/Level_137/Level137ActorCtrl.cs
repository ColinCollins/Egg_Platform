using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>入场保留共享Actor；上车后脚本接管，狗打败时例外恢复真实物理击飞与FallArea触发。</summary>
public sealed class Level137ActorCtrl : ActorCtrl
{
    // 仅用于把真实玩家的跳跃入口转交给本关总控；未完成绑定时不允许进入脚本接管阶段。
    [SerializeField] private Level137Ctrl gameplay;
    // 仅用于识别本关车辆的推挤对象，不改变共享 ActorCtrl 的碰撞层配置。
    [SerializeField] private BoxCollider2D carCollider;
    // 与本实例继承的 WallCheckDistance 一致；仅识别 Car，不改变共享碰撞层。
    [SerializeField, Min(0f)] private float carContactDistance = 0.9f;
    private PolygonCollider2D bodyCollider;
    private Bounds localBodyBounds;
    private bool scripted;
    private GameObject shadow;
    private bool defeatFlying;
    private bool flightSimulatedBefore;
    private bool ownsFlightPause;
    private bool pausedFlightSimulated;
    private Vector2 pausedFlightVelocity;
    private float pausedFlightAngularVelocity;

    /// <summary>表示本关是否已读取玩家碰撞轮廓并可以接受总控调用。</summary>
    public bool Initialized { get; private set; }

    /// <summary>表示玩家是否已经由共享 Actor 更新切换为本关脚本接管。</summary>
    public bool IsScripted => scripted;

    /// <summary>返回共享星星收集链需要的真实玩家碰撞体；初始化完成前可能为空。</summary>
    public Collider2D CollectorCollider => bodyCollider;

    /// <summary>返回玩家本地碰撞轮廓转换到当前世界姿态后的包围盒。</summary>
    public Bounds BodyBounds => Level137Geometry.TransformBounds(transform, localBodyBounds);

    /// <summary>返回当前玩家脚底相对 Transform 原点的世界高度偏移。</summary>
    public float FootOffset => -localBodyBounds.min.y * Mathf.Abs(transform.lossyScale.y);

    protected override void Start()
    {
        base.Start();
        bodyCollider = GetComponent<PolygonCollider2D>();
        Vector2[] points = bodyCollider.points;
        localBodyBounds = new Bounds(points[0] + bodyCollider.offset, Vector3.zero);
        foreach (Vector2 point in points) localBodyBounds.Encapsulate(point + bodyCollider.offset);
        shadow = transform.Find("Shadow")?.gameObject;
        Initialized = true;
    }

    /// <summary>在玩家尚未锁定到车辆前保留共享 ActorCtrl 的物理、输入和动画更新。</summary>
    public override void OnUpdate()
    {
        // 上车前与普通关卡一样由共享 Actor 更新物理/动画；出生位置由 Prefab 决定。
        if (!Initialized || scripted) return;
        base.OnUpdate();
    }

    /// <summary>未接管时调用共享跳跃；接管后把原跳键转交给 Level137Ctrl.RequestShot。</summary>
    protected override void JumpButton()
    {
        if (scripted)
        {
            gameplay?.RequestShot();
            return;
        }
        base.JumpButton();
    }

    /// <summary>仅将面前对象为本关车辆时的 push 表现替换为 walk，其余动画仍由共享逻辑处理。</summary>
    protected override void HandleAnimators()
    {
        if (scripted) return;
        // 只替换“面前最近墙体是本关 Car”的 push；其它墙、推物和空中动画仍走共享逻辑。
        if (!IsDied && !IsKicking && !IsSit && !IsKnocked && RB != null &&
            IsGround && XInput != 0f && IsWallDetected && !IsPushStuff &&
            carCollider != null && carCollider.enabled &&
            Physics2D.Raycast(transform.position, FaingRight ? Vector2.right : Vector2.left,
                carContactDistance, 1 << carCollider.gameObject.layer).collider == carCollider)
        {
            SpineCtrl.PlayAnimation("walk", true);
            return;
        }
        base.HandleAnimators();
    }

    /// <summary>
    /// 在玩家靠近车辆、开始自动上车时停止共享移动，关闭刚体模拟并交给总控更新。
    /// 只能在 <see cref="Initialized"/> 为 true 时调用。
    /// </summary>
    public void TakeScriptedControl()
    {
        if (!Initialized) return;
        SetMoveInput(0f);
        StopMoving();
        SetActorFaceDirection(true);
        scripted = true;
        RB.angularVelocity = 0f;
        RB.simulated = false;
        if (shadow != null) shadow.SetActive(false);
        SpineCtrl.PlayAnimation("idle", true);
    }

    /// <summary>
    /// 写入脚本接管期间玩家 Transform 与 Rigidbody2D 的位置，避免两者在重定位时分离。
    /// 调用方负责确保玩家已经进入本关脚本控制状态。
    /// </summary>
    public void SetPosition(Vector3 position)
    {
        transform.position = position;
        if (RB != null) RB.position = position;
    }

    /// <summary>
    /// 开始狗夺车后的真实物理击飞，恢复刚体模拟并交给共享 Knockback/FallArea 失败链。
    /// 本方法只负责启动运动，不按时间或屏幕坐标提交失败。
    /// </summary>
    public void BeginDefeatFlight(float force)
    {
        if (!Initialized || IsDied || defeatFlying) return;
        SetMoveInput(0f);
        StopMoving();
        flightSimulatedBefore = RB.simulated;
        defeatFlying = true;
        ownsFlightPause = false;
        RB.simulated = true;
        // 对齐dev138的LaunchPlayer：45度是初始速度方向，保留重力，不改成直线Tween。
        Knockback(new Vector2(-0.5f, 0.5f), force).Forget();
    }

    /// <summary>
    /// 更新击飞期间的 Spine 翻滚表现；实际位移和失败判定仍由 Rigidbody2D 与共享区域负责。
    /// </summary>
    public void TickDefeatFlight(float dt)
    {
        if (!defeatFlying || ownsFlightPause || IsDied) return;
        // 只转Spine表现，不转物理根节点/碰撞体；与138每0.5秒一圈的表现一致。
        SpineCtrl.transform.Rotate(0f, 0f, 720f * dt, Space.Self);
    }

    /// <summary>
    /// 暂停或恢复本关拥有的击飞物理快照，不影响普通入场和未击飞阶段的共享暂停行为。
    /// </summary>
    public void SetDefeatFlightPaused(bool value)
    {
        if (!defeatFlying || RB == null) return;
        if (value)
        {
            if (ownsFlightPause) return;
            pausedFlightVelocity = RB.linearVelocity;
            pausedFlightAngularVelocity = RB.angularVelocity;
            pausedFlightSimulated = RB.simulated;
            ownsFlightPause = true;
            RB.simulated = false;
        }
        else if (ownsFlightPause)
        {
            ownsFlightPause = false;
            RB.simulated = pausedFlightSimulated;
            RB.linearVelocity = pausedFlightVelocity;
            RB.angularVelocity = pausedFlightAngularVelocity;
        }
    }

    /// <summary>
    /// 停止本关拥有的击飞异步/物理状态并恢复击飞前的 simulated 标志；不回写旧初始位置。
    /// </summary>
    public void StopDefeatFlight()
    {
        if (!defeatFlying) return;
        defeatFlying = false;
        ownsFlightPause = false;
        StopKnock();
        StopMoving();
        if (RB != null) RB.simulated = flightSimulatedBefore;
        // 不回写初始位置或旋转，避免旧关卡在重启退场前突然恢复站姿。
    }

    private void OnDisable() { StopDefeatFlight(); }

    /// <summary>
    /// 返回包含玩家当前可见附件的运动包围盒，用于限制挂钩在参考视野内的行程。
    /// </summary>
    public Bounds TravelBounds()
    {
        Bounds bounds = BodyBounds;
        // 可换装角色的可见附件也不得越出基准框；阴影在挂载时已隐藏。
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
            bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }
}

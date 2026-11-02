using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 为 DLevel_019 的杠杆装置建立真实的 2D 刚体、碰撞体与铰链关系。
/// 长杆绕固定支点转动；左侧小碗跟随杆端移动，但冻结世界旋转并始终保持水平。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/DLevel 019 Lever Physics")]
public sealed class DLevel19LeverPhysics :
    MonoBehaviour,
    IDrawStrokeCompletedHandler,
    IDrawLevelIntroHandler,
    IDrawLevelRetryIntroStateHandler
{
    [Header("Bindings")]
    [Tooltip("固定不动的中间支座。")]
    [SerializeField] private Transform pivot;

    [Tooltip("可绕支点旋转的长杆。")]
    [SerializeField] private Transform beam;

    [Tooltip("跟随长杆左端移动、但始终保持水平的小碗。")]
    [SerializeField] private Transform bowl;

    [Tooltip("复用本关外星人放球装置；仅用于 Retry 时把球恢复到碗内。")]
    [SerializeField] private DrawAlienClipBallRigView rig;

    [Tooltip("Retry 时球刚体原点相对小碗中心的落座位置。")]
    [SerializeField] private Vector2 retryBallSeatLocalOffset = new(0f, 0.62f);

    [Header("Ball Flight")]
    [Tooltip("首次放球演出完成后使用的球重力倍率。只影响可玩态，不改变 DLevel10Ctrl 的入场落球节奏。")]
    [SerializeField, Range(0.5f, 5f)] private float playableBallGravityScale = 2.2f;

    [Tooltip("首次放球演出完成后使用的球线性阻尼。较低值可保留更多水平速度。")]
    [SerializeField, Min(0f)] private float playableBallLinearDamping = 0.05f;

    [Header("Beam")]
    [Tooltip("长杆质量。画线的刚体会通过碰撞真实地向长杆传递冲量。")]
    [SerializeField, Min(0.01f)] private float beamMass = 25f;

    [Tooltip("长杆线性阻尼。")]
    [SerializeField, Min(0f)] private float beamLinearDamping = 0.15f;

    [Tooltip("长杆角阻尼。数值越大，撞击后的摆动越快稳定。")]
    [SerializeField, Min(0f)] private float beamAngularDamping = 1f;

    [Tooltip("长杆重力倍率。")]
    [SerializeField, Min(0f)] private float beamGravityScale = 1f;

    [Tooltip("相对初始角度允许左端下压的少量反向余量。")]
    [SerializeField, Range(-45f, 0f)] private float beamLowerAngle = -2f;

    [Tooltip("相对初始角度允许右端下压、左端抬升的最大角度。角度越大，球离碗时的切线越偏向右侧；HingeJoint2D 在本连接方向下顺时针为正角度。")]
    [SerializeField, Range(0f, 120f)] private float beamUpperAngle = 78f;

    [Tooltip("长杆碰撞盒尺寸；Y 沿美术长杆方向。")]
    [SerializeField] private Vector2 beamColliderSize = new(0.27f, 5.9f);

    [Tooltip("长杆碰撞盒相对图片中心的偏移。负 Y 会保留右侧受压区，同时让左端避开碗和球。")]
    [SerializeField] private Vector2 beamColliderOffset = new(0f, -0.35f);

    [Tooltip("长杆接触画线时使用的物理材质；建议高摩擦、零弹性，避免重线沿斜杆滑回支点。")]
    [SerializeField] private PhysicsMaterial2D beamMaterial;

    [Header("Bowl")]
    [Tooltip("小碗质量。球的重量会通过小碗关节传递到长杆左端。")]
    [SerializeField, Min(0.01f)] private float bowlMass = 1.5f;

    [Tooltip("小碗线性阻尼。")]
    [SerializeField, Min(0f)] private float bowlLinearDamping = 0.2f;

    [Tooltip("小碗重力倍率。小碗自身和碗内球体都会对杠杆产生真实力矩。")]
    [SerializeField, Min(0f)] private float bowlGravityScale = 1f;

    [Tooltip("小碗 U 形边缘碰撞体的圆角半径。")]
    [SerializeField, Min(0f)] private float bowlEdgeRadius = 0.055f;

    [Tooltip("小碗局部坐标中的 U 形碰撞边缘。顶部保持敞开，球可被正常弹出。")]
    [SerializeField] private Vector2[] bowlEdgePoints =
    {
        new(-0.96f, 0.32f),
        new(-0.72f, -0.08f),
        new(-0.42f, -0.24f),
        new(0f, -0.3f),
        new(0.42f, -0.24f),
        new(0.72f, -0.08f),
        new(0.96f, 0.05f),
    };

    [Tooltip("碗体使用的物理材质；建议无弹性并带适量摩擦，避免放球时自行弹走。")]
    [SerializeField] private PhysicsMaterial2D bowlMaterial;

    private Rigidbody2D _pivotBody;
    private Rigidbody2D _beamBody;
    private Rigidbody2D _bowlBody;
    private HingeJoint2D _beamJoint;
    private HingeJoint2D _bowlJoint;

    private Vector2 _pivotInitialPosition;
    private float _pivotInitialRotation;
    private Vector2 _beamInitialPosition;
    private float _beamInitialRotation;
    private Vector2 _bowlInitialPosition;
    private float _bowlInitialRotation;
    private bool _initialized;
    private bool _hasReachedPlayableState;
    private bool _isLeverPhysicsReleased;
    private bool _hasCapturedInitialBallPhysics;
    private float _initialBallGravityScale;
    private float _initialBallLinearDamping;

    private static readonly Vector2[] DefaultBowlEdgePoints =
    {
        new(-0.96f, 0.32f),
        new(-0.72f, -0.08f),
        new(-0.42f, -0.24f),
        new(0f, -0.3f),
        new(0.42f, -0.24f),
        new(0.72f, -0.08f),
        new(0.96f, 0.05f),
    };

    private void Awake()
    {
        ResolveReferences();
        if (pivot == null || beam == null || bowl == null)
        {
            Debug.LogError(
                "[DLevel19LeverPhysics] 缺少 pivot、beam 或 bowl 绑定，杠杆物理未启用。",
                this);
            enabled = false;
            return;
        }

        CaptureInitialState();
        BuildPhysicsComponents();
        ResolveBallBody();
        _initialized = true;
        HoldLeverAtInitialPose();
    }

    private void OnEnable()
    {
        if (!_initialized)
            return;

        if (_hasReachedPlayableState)
        {
            ResetLeverPhysics();
            ReleaseLeverPhysics();
            return;
        }

        HoldLeverAtInitialPose();
    }

    private void OnDisable()
    {
        if (_initialized)
        {
            HoldLeverAtInitialPose();
            RestoreInitialBallPhysics();
        }
    }

    private void OnValidate()
    {
        playableBallGravityScale = Mathf.Clamp(playableBallGravityScale, 0.5f, 5f);
        playableBallLinearDamping = Mathf.Max(0f, playableBallLinearDamping);
        beamMass = Mathf.Max(0.01f, beamMass);
        beamLinearDamping = Mathf.Max(0f, beamLinearDamping);
        beamAngularDamping = Mathf.Max(0f, beamAngularDamping);
        beamGravityScale = Mathf.Max(0f, beamGravityScale);
        beamLowerAngle = Mathf.Min(0f, beamLowerAngle);
        beamUpperAngle = Mathf.Max(0f, beamUpperAngle);
        beamColliderSize.x = Mathf.Max(0.01f, beamColliderSize.x);
        beamColliderSize.y = Mathf.Max(0.01f, beamColliderSize.y);

        bowlMass = Mathf.Max(0.01f, bowlMass);
        bowlLinearDamping = Mathf.Max(0f, bowlLinearDamping);
        bowlGravityScale = Mathf.Max(0f, bowlGravityScale);
        bowlEdgeRadius = Mathf.Max(0f, bowlEdgeRadius);
    }

    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        // 只负责防止异常流程误锁；画线仍只通过真实碰撞向杠杆传力。
        ReleaseLeverPhysics();
    }

    public void OnDrawAttemptReset()
    {
        PreparePlayableRetryState();
    }

    public UniTask PlayDrawIntroAsync()
    {
        // 根节点 DLevel10Ctrl 已在此前等待放球落碗和双弹演出；此时才开放杠杆物理。
        ReleaseLeverPhysics();
        return UniTask.CompletedTask;
    }

    public void ApplyDrawRetryIntroState()
    {
        PreparePlayableRetryState();
    }

    /// <summary>
    /// 恢复支点、长杆和小碗的初始位姿，并清除本轮残留速度。
    /// </summary>
    [ContextMenu("Reset Lever Physics")]
    public void ResetLeverPhysics()
    {
        if (!_initialized)
            return;

        RestoreBody(_pivotBody, _pivotInitialPosition, _pivotInitialRotation);
        RestoreBody(_beamBody, _beamInitialPosition, _beamInitialRotation);
        RestoreBody(_bowlBody, _bowlInitialPosition, _bowlInitialRotation);
        Physics2D.SyncTransforms();
    }

    private void ResolveReferences()
    {
        pivot ??= transform.Find("Sp_hx_gg_01");
        beam ??= transform.Find("Sp_hx_bz_01");
        bowl ??= transform.Find("Sp_hx_w_01");
    }

    private void CaptureInitialState()
    {
        _pivotInitialPosition = pivot.position;
        _pivotInitialRotation = pivot.eulerAngles.z;
        _beamInitialPosition = beam.position;
        _beamInitialRotation = beam.eulerAngles.z;
        _bowlInitialPosition = bowl.position;
        _bowlInitialRotation = bowl.eulerAngles.z;
    }

    private void PreparePlayableRetryState()
    {
        HoldLeverAtInitialPose();
        Rigidbody2D ballBody = SeatReleasedBallForRetry();
        ReleaseLeverPhysics();
        ReleaseSeatedBall(ballBody);
    }

    private Rigidbody2D SeatReleasedBallForRetry()
    {
        Rigidbody2D ballBody = ResolveBallBody();
        if (ballBody == null || bowl == null)
            return null;

        ballBody.bodyType = RigidbodyType2D.Kinematic;
        ballBody.linearVelocity = Vector2.zero;
        ballBody.angularVelocity = 0f;
        ballBody.position = bowl.TransformPoint(retryBallSeatLocalOffset);
        Physics2D.SyncTransforms();

        return ballBody;
    }

    private static void ReleaseSeatedBall(Rigidbody2D ballBody)
    {
        if (ballBody == null)
            return;

        ballBody.bodyType = RigidbodyType2D.Dynamic;
        ballBody.linearVelocity = Vector2.zero;
        ballBody.angularVelocity = 0f;
        ballBody.Sleep();
    }

    private void HoldLeverAtInitialPose()
    {
        if (!_initialized)
            return;

        _isLeverPhysicsReleased = false;
        HoldBody(_bowlBody);
        HoldBody(_beamBody);
        RestoreBody(_pivotBody, _pivotInitialPosition, _pivotInitialRotation);
        RestoreBody(_beamBody, _beamInitialPosition, _beamInitialRotation);
        RestoreBody(_bowlBody, _bowlInitialPosition, _bowlInitialRotation);
        Physics2D.SyncTransforms();
        _beamBody?.Sleep();
        _bowlBody?.Sleep();
    }

    private void ReleaseLeverPhysics()
    {
        if (!_initialized)
            return;

        ApplyPlayableBallPhysics();
        if (_isLeverPhysicsReleased)
            return;

        ActivateDynamicBody(_beamBody);
        ActivateDynamicBody(_bowlBody);
        Physics2D.SyncTransforms();
        _beamBody?.WakeUp();
        _bowlBody?.WakeUp();
        _hasReachedPlayableState = true;
        _isLeverPhysicsReleased = true;
    }

    private Rigidbody2D ResolveBallBody()
    {
        rig?.ResolveReferences();
        Rigidbody2D ballBody = rig?.YellowBallRigidbody;
        if (ballBody == null || _hasCapturedInitialBallPhysics)
            return ballBody;

        _initialBallGravityScale = ballBody.gravityScale;
        _initialBallLinearDamping = ballBody.linearDamping;
        _hasCapturedInitialBallPhysics = true;
        return ballBody;
    }

    private void ApplyPlayableBallPhysics()
    {
        Rigidbody2D ballBody = ResolveBallBody();
        if (ballBody == null)
            return;

        ballBody.gravityScale = playableBallGravityScale;
        ballBody.linearDamping = playableBallLinearDamping;
    }

    private void RestoreInitialBallPhysics()
    {
        if (!_hasCapturedInitialBallPhysics)
            return;

        Rigidbody2D ballBody = rig?.YellowBallRigidbody;
        if (ballBody == null)
            return;

        ballBody.gravityScale = _initialBallGravityScale;
        ballBody.linearDamping = _initialBallLinearDamping;
    }

    private static void HoldBody(Rigidbody2D body)
    {
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private static void ActivateDynamicBody(Rigidbody2D body)
    {
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Dynamic;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void BuildPhysicsComponents()
    {
        _pivotBody = GetOrAdd<Rigidbody2D>(pivot.gameObject);
        ConfigurePivotBody(_pivotBody);

        _beamBody = GetOrAdd<Rigidbody2D>(beam.gameObject);
        ConfigureBeamBody(_beamBody);

        _bowlBody = GetOrAdd<Rigidbody2D>(bowl.gameObject);
        ConfigureBowlBody(_bowlBody);

        ConfigureBeamCollider(GetOrAdd<BoxCollider2D>(beam.gameObject));
        ConfigureBowlCollider(GetOrAdd<EdgeCollider2D>(bowl.gameObject));

        ConfigureBeamJoint();
        ConfigureBowlJoint();
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void ConfigurePivotBody(Rigidbody2D body)
    {
        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;
    }

    private void ConfigureBeamBody(Rigidbody2D body)
    {
        body.bodyType = RigidbodyType2D.Dynamic;
        body.simulated = true;
        body.useAutoMass = false;
        body.mass = beamMass;
        body.linearDamping = beamLinearDamping;
        body.angularDamping = beamAngularDamping;
        body.gravityScale = beamGravityScale;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.constraints = RigidbodyConstraints2D.None;
    }

    private void ConfigureBowlBody(Rigidbody2D body)
    {
        body.bodyType = RigidbodyType2D.Dynamic;
        body.simulated = true;
        body.useAutoMass = false;
        body.mass = bowlMass;
        body.linearDamping = bowlLinearDamping;
        body.angularDamping = 0f;
        body.gravityScale = bowlGravityScale;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void ConfigureBeamCollider(BoxCollider2D collider)
    {
        collider.enabled = true;
        collider.isTrigger = false;
        collider.size = beamColliderSize;
        collider.offset = beamColliderOffset;
        collider.sharedMaterial = beamMaterial;
    }

    private void ConfigureBowlCollider(EdgeCollider2D collider)
    {
        collider.enabled = true;
        collider.isTrigger = false;
        collider.edgeRadius = bowlEdgeRadius;
        collider.sharedMaterial = bowlMaterial;
        collider.points = bowlEdgePoints is { Length: >= 2 }
            ? bowlEdgePoints
            : DefaultBowlEdgePoints;
    }

    private void ConfigureBeamJoint()
    {
        _beamJoint = GetOrAdd<HingeJoint2D>(beam.gameObject);
        _beamJoint.enabled = true;
        _beamJoint.connectedBody = _pivotBody;
        _beamJoint.enableCollision = false;
        _beamJoint.autoConfigureConnectedAnchor = false;
        _beamJoint.useConnectedAnchor = true;
        _beamJoint.anchor = Vector2.zero;
        _beamJoint.connectedAnchor = pivot.InverseTransformPoint(beam.position);
        _beamJoint.useMotor = false;
        _beamJoint.useLimits = true;
        _beamJoint.limits = new JointAngleLimits2D
        {
            min = beamLowerAngle,
            max = beamUpperAngle,
        };
        _beamJoint.breakForce = Mathf.Infinity;
        _beamJoint.breakTorque = Mathf.Infinity;
    }

    private void ConfigureBowlJoint()
    {
        _bowlJoint = GetOrAdd<HingeJoint2D>(bowl.gameObject);
        _bowlJoint.enabled = true;
        _bowlJoint.connectedBody = _beamBody;
        _bowlJoint.enableCollision = false;
        _bowlJoint.autoConfigureConnectedAnchor = false;
        _bowlJoint.useConnectedAnchor = true;
        _bowlJoint.anchor = Vector2.zero;
        _bowlJoint.connectedAnchor = beam.InverseTransformPoint(bowl.position);
        _bowlJoint.useMotor = false;
        _bowlJoint.useLimits = false;
        _bowlJoint.breakForce = Mathf.Infinity;
        _bowlJoint.breakTorque = Mathf.Infinity;
    }

    private static void RestoreBody(Rigidbody2D body, Vector2 position, float rotation)
    {
        if (body == null)
            return;

        body.position = position;
        body.rotation = rotation;
        if (body.bodyType == RigidbodyType2D.Static)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.WakeUp();
    }
}

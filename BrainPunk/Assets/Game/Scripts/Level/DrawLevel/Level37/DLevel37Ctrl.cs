using UnityEngine;

/// <summary>
/// 管理 DLevel_037 的中心铰链物理机关。
/// 第一笔有效画线完成前固定平台与 Actor；释放后由真实重力和碰撞驱动平台绕齿轮转动。
/// 本组件不负责成功条件，五秒后直接成功由通用 DrawLevelCountdownCtrl 处理。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 37 Physics Ctrl")]
public sealed class DLevel37Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler
{
    #region Inspector References

    /// <summary>
    /// 中央齿轮上的静态刚体，作为平台铰链的固定连接体。
    /// </summary>
    [Header("Bindings")]
    [Tooltip("中央齿轮 Sp_l93_cl_01 上的 Static Rigidbody2D。")]
    [SerializeField] private Rigidbody2D gearRigidbody;

    /// <summary>
    /// 长平台根节点上的刚体；运行时只允许绕中央齿轮旋转。
    /// </summary>
    [Tooltip("长平台 platform_Stone 根节点上的 Rigidbody2D。")]
    [SerializeField] private Rigidbody2D platformRigidbody;

    /// <summary>
    /// 将长平台连接到中央齿轮的 2D 铰链。
    /// </summary>
    [Tooltip("长平台根节点上的 HingeJoint2D。")]
    [SerializeField] private HingeJoint2D platformHinge;

    /// <summary>
    /// 本关 Actor 的根刚体；第一笔完成后与平台同时释放，使 Actor 的质量真实作用于平台。
    /// </summary>
    [Tooltip("DLevel_037 内 Actor 根节点上的 Rigidbody2D。")]
    [SerializeField] private Rigidbody2D actorRigidbody;

    #endregion

    #region Physics Parameters

    /// <summary>
    /// 长平台质量。平台重心与齿轮基本重合，质量主要影响受 Actor 和画线冲击后的转动响应。
    /// </summary>
    [Header("Platform Physics")]
    [Tooltip("长平台质量；越大越不容易被短促碰撞打飞。")]
    [SerializeField, Min(0.01f)] private float platformMass = 10f;

    /// <summary>长平台线性阻尼。</summary>
    [Tooltip("长平台线性阻尼。铰链会限制平移，该值主要用于抑制接头数值抖动。")]
    [SerializeField, Min(0f)] private float platformLinearDamping = 0.05f;

    /// <summary>长平台角阻尼，用于让来回摆动逐步收敛。</summary>
    [Tooltip("长平台角阻尼；数值越大，左右摆动越快稳定。")]
    [SerializeField, Min(0f)] private float platformAngularDamping = 1.5f;

    /// <summary>长平台重力倍率。</summary>
    [Tooltip("长平台重力倍率。平台重心位于支点附近，自重不会主动偏向一侧。")]
    [SerializeField, Min(0f)] private float platformGravityScale = 1f;

    #endregion

    #region Runtime State

    private Vector2 _platformInitialPosition;
    private float _platformInitialRotation;
    private Vector2 _actorInitialPosition;
    private float _actorInitialRotation;
    private bool _isInitialized;
    private bool _isReleased;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        CaptureInitialState();
        ConfigurePhysics();
        _isInitialized = true;
        RestoreWaitingState();
    }

    private void OnValidate()
    {
        platformMass = Mathf.Max(0.01f, platformMass);
        platformLinearDamping = Mathf.Max(0f, platformLinearDamping);
        platformAngularDamping = Mathf.Max(0f, platformAngularDamping);
        platformGravityScale = Mathf.Max(0f, platformGravityScale);
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 第一笔有效画线完成后同时释放 Actor 与平台。
    /// 后续笔画仍可作为配重，但不会重复重置平台状态；通关倒计时由通用组件独立启动。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying ||
            !_isInitialized ||
            _isReleased ||
            !result.IsFirstStroke)
        {
            return;
        }

        _isReleased = true;
        ReleaseBody(platformRigidbody);
        ReleaseBody(actorRigidbody);
    }

    /// <summary>
    /// 清线或重置尝试时恢复平台和 Actor 的初始位姿，并重新等待第一笔有效画线。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying || !_isInitialized)
            return;

        RestoreWaitingState();
    }

    #endregion

    #region Physics Setup

    private void ResolveReferences()
    {
        Transform platformGroup = transform.Find("pingtai");
        Transform gearTransform = platformGroup != null
            ? platformGroup.Find("Sp_l93_cl_01")
            : null;
        if (gearRigidbody == null && gearTransform != null)
            gearRigidbody = gearTransform.GetComponent<Rigidbody2D>();

        if (platformRigidbody == null && platformGroup != null)
        {
            Rigidbody2D[] bodies = platformGroup.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody2D candidate = bodies[i];
                if (candidate != null && candidate != gearRigidbody)
                {
                    platformRigidbody = candidate;
                    break;
                }
            }
        }

        if (platformHinge == null && platformRigidbody != null)
            platformHinge = platformRigidbody.GetComponent<HingeJoint2D>();

        if (actorRigidbody == null)
        {
            ActorCtrl actor = GetComponentInChildren<ActorCtrl>(true);
            if (actor != null)
                actorRigidbody = actor.GetComponent<Rigidbody2D>();
        }
    }

    private bool ValidateReferences()
    {
        bool isValid =
            gearRigidbody != null &&
            platformRigidbody != null &&
            platformHinge != null &&
            actorRigidbody != null;

        if (!isValid)
        {
            Debug.LogError(
                "[DLevel37Ctrl] 缺少齿轮、平台、铰链或 Actor 刚体绑定，中心铰链物理机关已停用。",
                this);
        }

        return isValid;
    }

    private void CaptureInitialState()
    {
        _platformInitialPosition = platformRigidbody.position;
        _platformInitialRotation = platformRigidbody.rotation;
        _actorInitialPosition = actorRigidbody.position;
        _actorInitialRotation = actorRigidbody.rotation;
    }

    private void ConfigurePhysics()
    {
        gearRigidbody.bodyType = RigidbodyType2D.Static;
        gearRigidbody.simulated = true;

        platformRigidbody.simulated = true;
        platformRigidbody.useAutoMass = false;
        platformRigidbody.mass = platformMass;
        platformRigidbody.linearDamping = platformLinearDamping;
        platformRigidbody.angularDamping = platformAngularDamping;
        platformRigidbody.gravityScale = platformGravityScale;
        platformRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        platformRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        platformRigidbody.constraints = RigidbodyConstraints2D.None;

        platformHinge.enabled = true;
        platformHinge.connectedBody = gearRigidbody;
        platformHinge.enableCollision = false;
        platformHinge.autoConfigureConnectedAnchor = false;
        platformHinge.useConnectedAnchor = true;
        platformHinge.anchor = platformRigidbody.transform.InverseTransformPoint(
            gearRigidbody.transform.position);
        platformHinge.connectedAnchor = gearRigidbody.transform.InverseTransformPoint(
            gearRigidbody.transform.position);
        platformHinge.useMotor = false;
        platformHinge.useLimits = false;
        platformHinge.breakForce = Mathf.Infinity;
        platformHinge.breakTorque = Mathf.Infinity;
    }

    private void RestoreWaitingState()
    {
        _isReleased = false;
        HoldBody(platformRigidbody, _platformInitialPosition, _platformInitialRotation);
        HoldBody(actorRigidbody, _actorInitialPosition, _actorInitialRotation);
        Physics2D.SyncTransforms();
    }

    private static void HoldBody(Rigidbody2D body, Vector2 position, float rotation)
    {
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.position = position;
        body.rotation = rotation;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private static void ReleaseBody(Rigidbody2D body)
    {
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Dynamic;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.WakeUp();
    }

    #endregion
}

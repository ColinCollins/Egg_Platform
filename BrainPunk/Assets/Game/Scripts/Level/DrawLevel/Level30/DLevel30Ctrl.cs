using Bear.Logger;
using UnityEngine;

/// <summary>
/// DLevel_030 偏重平台控制器。
/// 将平台的世界中心固定在初始位置，仅保留绕中心旋转的动态物理，
/// 并用等效重心力矩表现长边更重，避免铰链与位置约束共同求解产生抖动。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 30 Ctrl")]
public sealed class DLevel30Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Inspector References

    /// <summary>
    /// 需要固定中心且保留旋转物理的 platform_Stone 刚体。
    /// 调用边界：必须绑定本关平台根节点上的 Dynamic Rigidbody2D。
    /// </summary>
    [Header("平台引用")]
    [Tooltip("platform_Stone 根节点上的 Rigidbody2D。中心位置会固定，只允许旋转。")]
    [SerializeField] private Rigidbody2D platformRigidbody;

    /// <summary>
    /// 本关 DrawLevelCtrl，用于跟随暂停、结算和尝试重置生命周期。
    /// 调用边界：仅引用 DLevel_030 根节点上的控制器。
    /// </summary>
    [Tooltip("DLevel_030 根节点上的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    #endregion

    #region Physics Configuration

    /// <summary>
    /// 平台用于碰撞与转动惯量计算的质量；偏重方向由等效重心偏移单独控制。
    /// </summary>
    [Header("偏重物理")]
    [Tooltip("平台质量。不会改变固定中心，只影响碰撞和转动惯量。")]
    [Min(0.01f)]
    [SerializeField] private float platformMass = 10f;

    /// <summary>
    /// 相对平台中心的等效重心偏移；X 为负表示左侧长边更重。
    /// 注意：该值只生成重力力矩，不会向平台施加向下的平移力。
    /// </summary>
    [Tooltip("等效重心相对中心的局部偏移。X 为负表示左侧长边更重。")]
    [SerializeField] private Vector2 longSideCenterOfMassOffset = new(-1.25f, 0f);

    /// <summary>
    /// 等效重力倍率，仅用于偏重力矩；平台 Rigidbody2D 的原生重力始终关闭。
    /// </summary>
    [Tooltip("偏重力矩使用的重力倍率，不会让平台整体下坠。")]
    [Min(0f)]
    [SerializeField] private float effectiveGravityScale = 1f;

    /// <summary>
    /// 平台旋转阻尼，用于吸收碰撞后的高频角速度，减少视觉抖动。
    /// </summary>
    [Tooltip("平台旋转阻尼。值越大越稳定，但旋转响应越慢。")]
    [Min(0f)]
    [SerializeField] private float angularDamping = 1.5f;

    #endregion

    #region Runtime State

    private Vector2 _initialPosition;
    private float _initialRotation;
    private bool _hasInitialState;
    private bool _isResolved;
    private DrawLevelCtrl _subscribedDrawLevelCtrl;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (platformRigidbody == null)
        {
            this.LogError("[DLevel30Ctrl] platform_Stone Rigidbody2D 未绑定，平台稳定控制已停用。");
            enabled = false;
            return;
        }

        CacheInitialState();
        ConfigureStablePlatform();
        SubscribeToLevelResolved();
        _isResolved = false;
    }

    private void FixedUpdate()
    {
        if (_isResolved || platformRigidbody == null ||
            (drawLevelCtrl != null && drawLevelCtrl.IsPaused))
        {
            return;
        }

        ApplyLongSideGravityTorque();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        UnsubscribeFromLevelResolved();
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 平台从开场即参与物理，本回调只用于接入统一尝试重置生命周期。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
    }

    /// <summary>
    /// 当前画线尝试重置时恢复平台初始中心、角度和稳定物理配置。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying || platformRigidbody == null || !_hasInitialState)
            return;

        _isResolved = false;
        RestoreInitialState();
    }

    #endregion

    #region Platform Physics

    private void ApplyLongSideGravityTorque()
    {
        Vector2 worldOffset = platformRigidbody.transform.TransformVector(longSideCenterOfMassOffset);
        Vector2 equivalentGravityForce =
            platformRigidbody.mass * effectiveGravityScale * Physics2D.gravity;
        float torque =
            worldOffset.x * equivalentGravityForce.y -
            worldOffset.y * equivalentGravityForce.x;

        platformRigidbody.AddTorque(torque, ForceMode2D.Force);
    }

    private void ConfigureStablePlatform()
    {
        platformRigidbody.bodyType = RigidbodyType2D.Dynamic;
        platformRigidbody.simulated = true;
        platformRigidbody.useAutoMass = false;
        platformRigidbody.mass = Mathf.Max(0.01f, platformMass);
        platformRigidbody.gravityScale = 0f;
        platformRigidbody.angularDamping = Mathf.Max(0f, angularDamping);
        platformRigidbody.centerOfMass = Vector2.zero;
        platformRigidbody.constraints =
            RigidbodyConstraints2D.FreezePositionX |
            RigidbodyConstraints2D.FreezePositionY;
        platformRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        platformRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        platformRigidbody.WakeUp();
    }

    private void CacheInitialState()
    {
        if (_hasInitialState)
            return;

        _initialPosition = platformRigidbody.position;
        _initialRotation = platformRigidbody.rotation;
        _hasInitialState = true;
    }

    private void RestoreInitialState()
    {
        platformRigidbody.simulated = false;
        platformRigidbody.position = _initialPosition;
        platformRigidbody.rotation = _initialRotation;
        platformRigidbody.linearVelocity = Vector2.zero;
        platformRigidbody.angularVelocity = 0f;
        ConfigureStablePlatform();
        Physics2D.SyncTransforms();
    }

    private void HoldResolvedState()
    {
        if (platformRigidbody == null)
            return;

        platformRigidbody.linearVelocity = Vector2.zero;
        platformRigidbody.angularVelocity = 0f;
        platformRigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
        platformRigidbody.Sleep();
    }

    #endregion

    #region References And Level Lifecycle

    private void ResolveReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponent<DrawLevelCtrl>();
    }

    private void SubscribeToLevelResolved()
    {
        if (_subscribedDrawLevelCtrl == drawLevelCtrl)
            return;

        UnsubscribeFromLevelResolved();
        _subscribedDrawLevelCtrl = drawLevelCtrl;
        if (_subscribedDrawLevelCtrl != null)
            _subscribedDrawLevelCtrl.Resolved += OnLevelResolved;
    }

    private void UnsubscribeFromLevelResolved()
    {
        if (_subscribedDrawLevelCtrl == null)
            return;

        _subscribedDrawLevelCtrl.Resolved -= OnLevelResolved;
        _subscribedDrawLevelCtrl = null;
    }

    private void OnLevelResolved(DrawLevelResult result)
    {
        _isResolved = true;
        HoldResolvedState();
    }

    #endregion
}

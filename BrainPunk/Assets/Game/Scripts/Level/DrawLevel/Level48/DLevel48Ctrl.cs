using Bear.Logger;
using UnityEngine;

/// <summary>
/// DLevel_048 固定支点的受力旋转杆控制器。
/// 调用边界：只管理本关绑定的杆刚体，通过真实碰撞转动、角阻尼和回正扭矩实现回弹。
/// 注意：旋转中心使用刚体局部坐标；偏离原点时，物体原点会随转动移动，但世界支点保持固定。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 48 Ctrl")]
public sealed class DLevel48Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Inspector References

    /// <summary>
    /// 本关中心杆的刚体，必须在 Prefab 中显式绑定。
    /// 注意：本组件负责配置 Dynamic、固定平移和开放旋转，不与位置关节共同控制该刚体。
    /// </summary>
    [Header("杆子引用")]
    [Tooltip("中心杆根节点上的 Rigidbody2D。必须在 Prefab 中显式绑定。")]
    [SerializeField] private Rigidbody2D platformRigidbody;

    /// <summary>
    /// 本关主控，用于读取暂停状态、订阅结算并接入现有尝试重置流程。
    /// </summary>
    [Tooltip("DLevel_048 根节点上的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    #endregion

    #region Physics Configuration

    /// <summary>
    /// 杆刚体局部坐标中的旋转中心，启用或重置时写入物理质心。
    /// 注意：原点即齿轮支点时使用零；调整此值后需重置本次尝试。
    /// </summary>
    [Header("旋转与回弹")]
    [Tooltip("杆刚体局部坐标中的支点。原点就是齿轮中心时填 (0,0)，调整后重置生效。")]
    [SerializeField] private Vector2 localRotationCenter = Vector2.zero;

    /// <summary>
    /// 杆的质量，用于真实碰撞和转动惯量计算，启用或重置时生效。
    /// </summary>
    [Tooltip("杆质量，影响碰撞后的转动响应；启用或重置时生效。")]
    [Min(0.01f)]
    [SerializeField] private float platformMass = 10f;

    /// <summary>
    /// 旋转阻力，值越大角速度衰减越快；运行时调整会在下一物理步生效。
    /// </summary>
    [Tooltip("旋转阻力。值越大摆动衰减越快；可在运行时调节。")]
    [Min(0f)]
    [SerializeField] private float angularDamping = 1.5f;

    /// <summary>
    /// 每弧度偏转对应的回正扭矩强度，使杆向开场角度回弹。
    /// 注意：零表示关闭自动回弹；质量和阻力同时影响回弹快慢及是否往复摆动。
    /// </summary>
    [Tooltip("回弹强度，单位为每弧度偏转的扭矩。越大回正越有力，0 表示不自动回弹。")]
    [Min(0f)]
    [SerializeField] private float springStrength = 80f;

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

        if (platformRigidbody == null || drawLevelCtrl == null)
        {
            this.LogError("[DLevel48Ctrl] 杆 Rigidbody2D 或 DrawLevelCtrl 未绑定，旋转控制已停用。");
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
            drawLevelCtrl.IsPaused || drawLevelCtrl.IsOutcomeLocked)
        {
            return;
        }

        float damping = Mathf.Max(0f, angularDamping);
        if (!Mathf.Approximately(platformRigidbody.angularDamping, damping))
            platformRigidbody.angularDamping = damping;

        float angleOffset = Mathf.DeltaAngle(platformRigidbody.rotation, _initialRotation);
        float torque = angleOffset * Mathf.Deg2Rad * Mathf.Max(0f, springStrength);
        platformRigidbody.AddTorque(torque, ForceMode2D.Force);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        UnsubscribeFromLevelResolved();
    }

    #endregion

    #region Editor Visualization

#if UNITY_EDITOR
    /// <summary>
    /// 在 Scene 与 Prefab 视图标出 Inspector 配置的杆局部旋转中心。
    /// 调用边界：仅编辑器 Gizmos 绘制，使用显式绑定的杆 Transform 转换世界位置。
    /// 注意：不修改刚体；运行中调整中心后，实际物理支点仍需启用或重置才会更新。
    /// </summary>
    private void OnDrawGizmos()
    {
        if (platformRigidbody == null)
            return;

        Vector3 center = platformRigidbody.transform.TransformPoint(
            new Vector3(localRotationCenter.x, localRotationCenter.y, 0f));
        Vector3 horizontal = Vector3.right * 0.22f;
        Vector3 vertical = Vector3.up * 0.22f;
        Color previousColor = Gizmos.color;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(center, 0.05f);
        Gizmos.DrawWireSphere(center, 0.15f);
        Gizmos.DrawLine(center - horizontal, center + horizontal);
        Gizmos.DrawLine(center - vertical, center + vertical);
        Gizmos.color = previousColor;

        UnityEditor.Handles.Label(center + Vector3.up * 0.28f, "Local Rotation Center");
    }
#endif

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 杆从开场参与真实碰撞，画线完成不额外注入旋转速度。
    /// 调用边界：通过现有接口接入本关尝试重置，不承担首笔放球职责。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
    }

    /// <summary>
    /// 本次尝试重置时恢复杆的初始位置、角度、零速度和当前物理参数。
    /// 调用边界：由 DrawLevelCtrl 的现有局部 Handler 流程调用，仅运行时有效。
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

    private void ConfigureStablePlatform()
    {
        platformRigidbody.bodyType = RigidbodyType2D.Dynamic;
        platformRigidbody.simulated = true;
        platformRigidbody.useAutoMass = false;
        platformRigidbody.mass = Mathf.Max(0.01f, platformMass);
        platformRigidbody.gravityScale = 0f;
        platformRigidbody.angularDamping = Mathf.Max(0f, angularDamping);
        platformRigidbody.centerOfMass = localRotationCenter;
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
        // 暂停模拟期间同步Transform，避免重新启用模拟时旧显示姿态覆盖刚体重置值。
        Vector3 initialPosition = new Vector3(
            _initialPosition.x, _initialPosition.y, platformRigidbody.transform.position.z);
        platformRigidbody.transform.SetPositionAndRotation(
            initialPosition, Quaternion.Euler(0f, 0f, _initialRotation));
        platformRigidbody.linearVelocity = Vector2.zero;
        platformRigidbody.angularVelocity = 0f;
        ConfigureStablePlatform();
        Physics2D.SyncTransforms();
    }

    private void HoldResolvedState()
    {
        platformRigidbody.linearVelocity = Vector2.zero;
        platformRigidbody.angularVelocity = 0f;
        platformRigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
        platformRigidbody.Sleep();
    }

    #endregion

    #region Level Lifecycle

    private void SubscribeToLevelResolved()
    {
        if (_subscribedDrawLevelCtrl == drawLevelCtrl)
            return;

        UnsubscribeFromLevelResolved();
        _subscribedDrawLevelCtrl = drawLevelCtrl;
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

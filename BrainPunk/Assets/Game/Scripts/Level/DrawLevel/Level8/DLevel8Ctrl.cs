using Bear.Logger;
using UnityEngine;

/// <summary>
/// 给场景中的 weapon 施加一个力，力的方向通过 Gizmos 展示一下，然后力的大小方向都暴露给我调整。
/// 第一笔有效画线完成后，先把 weapon 刚体切换为 Dynamic，再施加一次冲量。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 8 Ctrl")]
public sealed class DLevel8Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Constants

    private const float GizmoLineLength = 1.5f;
    private const float GizmoArrowHeadLength = 0.35f;
    private const float GizmoArrowHeadAngle = 30f;

    #endregion

    #region Inspector References

    /// <summary>
    /// 接收启动冲量的 weapon 刚体。
    /// 调用边界：只绑定 DLevel_008 prefab 内 level8/weapon 的 Rigidbody2D。
    /// 注意事项：未绑定时运行时只按固定层级做一次局部回退查找。
    /// </summary>
    [Header("Weapon")]
    [Tooltip("level8/weapon 节点上的 Rigidbody2D。")]
    [SerializeField] private Rigidbody2D weaponRigidbody;

    /// <summary>
    /// 启动冲量的世界坐标方向。
    /// 注意事项：运行时会归一化，向量长度不会参与最终冲量大小计算。
    /// </summary>
    [Tooltip("Weapon 启动冲量的世界坐标方向；Gizmos 使用同一方向显示。")]
    [SerializeField] private Vector2 forceDirection = Vector2.right;

    /// <summary>
    /// 启动冲量大小。
    /// 注意事项：负值会按 0 处理，实际速度还会受到 weapon 质量影响。
    /// </summary>
    [Tooltip("Weapon 启动冲量大小，使用 ForceMode2D.Impulse。")]
    [Min(0f)]
    [SerializeField] private float forceMagnitude = 200f;

    /// <summary>
    /// 首笔后给 weapon 的旋转冲量；正值逆时针，负值顺时针。
    /// 注意事项：使用刚体自身的惯量计算角速度，建议保持小幅值以保留挥刀姿态。
    /// </summary>
    [Tooltip("Weapon 首笔后的旋转冲量；正值逆时针，负值顺时针。")]
    [SerializeField] private float rotationImpulse;

    #endregion

    #region Runtime State

    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;
    private bool _hasCachedInitialState;
    private bool _hasLaunched;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        CacheInitialState();
        RestoreInitialState();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = weaponRigidbody != null
            ? weaponRigidbody.worldCenterOfMass
            : transform.position;
        Vector2 normalizedDirection = forceDirection.normalized;
        if (normalizedDirection == Vector2.zero)
            return;

        Vector3 direction = normalizedDirection;
        Vector3 end = origin + direction * GizmoLineLength;
        Vector3 firstArrowHeadDirection =
            Quaternion.Euler(0f, 0f, GizmoArrowHeadAngle) * -direction;
        Vector3 secondArrowHeadDirection =
            Quaternion.Euler(0f, 0f, -GizmoArrowHeadAngle) * -direction;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawLine(
            end,
            end + firstArrowHeadDirection * GizmoArrowHeadLength);
        Gizmos.DrawLine(
            end,
            end + secondArrowHeadDirection * GizmoArrowHeadLength);
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 第一笔有效画线完成后，把 weapon 刚体切换为 Dynamic 并施加一次配置冲量。
    /// 后续笔画或重复回调不会再次发射 weapon。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !result.IsFirstStroke || _hasLaunched)
            return;

        ResolveReferences();
        if (weaponRigidbody == null)
        {
            this.LogError("[DLevel8Ctrl] level8/weapon 缺少 Rigidbody2D，无法发射 weapon。");
            return;
        }

        _hasLaunched = true;
        weaponRigidbody.bodyType = RigidbodyType2D.Dynamic;
        weaponRigidbody.linearVelocity = Vector2.zero;
        weaponRigidbody.angularVelocity = 0f;
        if (ApplyForce())
            ApplyRotationImpulse();
    }

    /// <summary>
    /// 当前尝试重置时恢复 weapon 的初始相对位姿、Kinematic 状态和零速度。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        RestoreInitialState();
    }

    #endregion

    #region Force Control

    private bool ApplyForce()
    {
        if (weaponRigidbody == null)
        {
            this.LogError("[DLevel8Ctrl] level8/weapon 缺少 Rigidbody2D，无法施加启动冲量。");
            return false;
        }

        Vector2 normalizedDirection = forceDirection.normalized;
        float appliedMagnitude = Mathf.Max(0f, forceMagnitude);
        if (normalizedDirection == Vector2.zero || appliedMagnitude <= 0f)
        {
            this.LogWarning("[DLevel8Ctrl] 启动冲量方向为零或大小不大于零，本次不施力。");
            return false;
        }

        weaponRigidbody.AddForce(
            normalizedDirection * appliedMagnitude,
            ForceMode2D.Impulse);
        weaponRigidbody.WakeUp();
        return true;
    }

    private void ApplyRotationImpulse()
    {
        if (weaponRigidbody == null || Mathf.Approximately(rotationImpulse, 0f))
            return;

        weaponRigidbody.AddTorque(rotationImpulse, ForceMode2D.Impulse);
        weaponRigidbody.WakeUp();
    }

    #endregion

    #region State Control

    private void CacheInitialState()
    {
        if (_hasCachedInitialState || weaponRigidbody == null)
            return;

        _initialLocalPosition = weaponRigidbody.transform.localPosition;
        _initialLocalRotation = weaponRigidbody.transform.localRotation;
        _hasCachedInitialState = true;
    }

    private void RestoreInitialState()
    {
        _hasLaunched = false;
        if (weaponRigidbody == null)
            return;

        weaponRigidbody.bodyType = RigidbodyType2D.Kinematic;
        weaponRigidbody.linearVelocity = Vector2.zero;
        weaponRigidbody.angularVelocity = 0f;

        if (!_hasCachedInitialState)
            return;

        weaponRigidbody.transform.localPosition = _initialLocalPosition;
        weaponRigidbody.transform.localRotation = _initialLocalRotation;
    }

    #endregion

    #region Reference Resolution

    private void ResolveReferences()
    {
        if (weaponRigidbody != null)
            return;

        Transform levelRoot = transform.Find("level8");
        weaponRigidbody = levelRoot?.Find("weapon")?.GetComponent<Rigidbody2D>();
    }

    #endregion
}

using Bear.Logger;
using UnityEngine;

/// <summary>
/// IWanner 第 037 关 Portal 控制器：按触发次数启动或停止 Portal 的水平移动。
/// </summary>
[DisallowMultipleComponent]
public sealed class ILevel037Ctrl : MonoBehaviour, IDebuger
{
    #region Constants

    private const float EndPointSwitchDelaySeconds = 1f;

    #endregion

    #region Serialized Fields

    /// <summary>
    /// 用途：指定第一次触发时需要切换终点并向右移动的同一个 Portal。
    /// 调用边界：第一次有效触发会预约传送完成 1 秒后切换终点，并立即修改其世界坐标。
    /// 注意事项：应绑定本关唯一受控的 Portal 实例。
    /// </summary>
    [Header("Portal Movement")]
    [Tooltip("第一次触发后持续向右移动的 Portal")]
    [SerializeField] private IWannerGame_Portal portal;

    /// <summary>
    /// 用途：指定该 Portal 第一次触发后使用的新终点。
    /// 调用边界：作为 portal.SetEndPointAfterTeleport 的参数；可以绑定在需要随 Portal 移动的层级下。
    /// 注意事项：引用为空时第一次触发不会启动移动，也不会累计触发次数。
    /// </summary>
    [Tooltip("该 Portal 第一次触发后切换到的新 endPoint")]
    [SerializeField] private Transform newEndPoint;

    /// <summary>
    /// 用途：配置 Portal 向世界坐标右侧移动的速度。
    /// 调用边界：第一次有效触发后生效，第二次触发时停止；运行时按非负值处理。
    /// 注意事项：单位为 Unity 世界单位/秒，必须大于 0 才能启动移动。
    /// </summary>
    [Tooltip("Portal 向右移动的速度（世界单位/秒）")]
    [Min(0f)]
    [SerializeField] private float portalMoveSpeed = 1f;

    #endregion

    #region Runtime State

    private bool _hasStartedPortalMovement;
    private bool _isPortalMoving;

    #endregion

    #region Public API

    /// <summary>
    /// 用途：接收本关 Portal 机关的依次触发。
    /// 调用边界：第一次有效调用会预约同一个 Portal 在传送完成 1 秒后切换 endPoint，并立即启动向右移动；第二次调用只停止移动，不修改 endPoint。
    /// 注意事项：缺少引用或速度无效时第一次调用失败且不累计次数；完成第二次调用后，后续调用不再改变状态。
    /// </summary>
    public void TriggerPortalMovement()
    {
        if (!Application.isPlaying)
        {
            this.LogWarning(
                "[ILevel037Ctrl] TriggerPortalMovement is only available in PlayMode.");
            return;
        }

        if (_hasStartedPortalMovement)
        {
            // 同一个 Trigger 第二次调用该 API 时立即停止，不再执行任何终点相关逻辑。
            _isPortalMoving = false;
            return;
        }

        if (!TryStartPortalMovement())
            return;

        _hasStartedPortalMovement = true;
    }

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (!_isPortalMoving)
            return;

        if (portal == null)
        {
            _isPortalMoving = false;
            this.LogWarning(
                "[ILevel037Ctrl] Moving Portal was lost at runtime; movement has stopped.");
            return;
        }

        float effectiveSpeed = Mathf.Max(0f, portalMoveSpeed);
        portal.transform.position += Vector3.right * (effectiveSpeed * Time.deltaTime);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        _isPortalMoving = false;
    }

    #endregion

    #region Runtime Helpers

    private bool TryStartPortalMovement()
    {
        if (portal == null || newEndPoint == null)
        {
            this.LogWarning(
                "[ILevel037Ctrl] Portal references are incomplete; first trigger was ignored.");
            return false;
        }

        if (portalMoveSpeed <= 0f)
        {
            this.LogWarning(
                "[ILevel037Ctrl] Portal move speed must be greater than 0; first trigger was ignored.");
            return false;
        }

        portal.SetEndPointAfterTeleport(newEndPoint, EndPointSwitchDelaySeconds);
        _isPortalMoving = true;
        return true;
    }

    #endregion
}

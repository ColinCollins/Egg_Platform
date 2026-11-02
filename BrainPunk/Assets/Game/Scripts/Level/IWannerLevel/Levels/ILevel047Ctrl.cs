using Bear.Logger;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// IWanner 第 047 关左右互斥触发控制器。
/// 每个实例只接受首次触发，通过对应的本地回调驱动 Inspector 绑定的 Executor。
/// </summary>
[DisallowMultipleComponent]
public sealed class ILevel047Ctrl : MonoBehaviour, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：持有本关成功门配置的接收控制器；触发时调用其门配置 API。
    /// 注意事项：必须在 Inspector 显式绑定本关实例，不进行运行时查找。
    /// </summary>
    [Header("Level Controller")]
    [SerializeField] private IWannerLevelCtrl levelCtrl;

    /// <summary>
    /// 用途：右侧触发时选用的左门成功触发器。
    /// 注意事项：必须显式绑定，并与左门动画组件属于同一扇门。
    /// </summary>
    [Header("Left Door")]
    [SerializeField] private OnTrigger2DHandle leftSuccessDoorTrigger;

    /// <summary>
    /// 用途：右侧触发时选用的左门动画及定位组件。
    /// 注意事项：必须显式绑定；只配置引用，不在选择时播放动画。
    /// </summary>
    [SerializeField] private IWannerGame_EnterDoorAnimCtrl leftEnterDoorAnimCtrl;

    /// <summary>
    /// 用途：左侧触发时选用的右门成功触发器。
    /// 注意事项：必须显式绑定，并与右门动画组件属于同一扇门。
    /// </summary>
    [Header("Right Door")]
    [SerializeField] private OnTrigger2DHandle rightSuccessDoorTrigger;

    /// <summary>
    /// 用途：左侧触发时选用的右门动画及定位组件。
    /// 注意事项：必须显式绑定；只配置引用，不在选择时播放动画。
    /// </summary>
    [SerializeField] private IWannerGame_EnterDoorAnimCtrl rightEnterDoorAnimCtrl;

    /// <summary>
    /// 用途：左侧首次触发后执行的回调，用于在 Inspector 绑定本关 Executor。
    /// 调用边界：仅运行态且左侧先触发时调用一次；执行前已锁定左右入口。
    /// 注意事项：需要显式绑定 Executor 的执行方法，空回调也会消耗本次选择。
    /// </summary>
    [Header("Trigger Callbacks")]
    [SerializeField] private UnityEvent onTriggerLeft = new();

    /// <summary>
    /// 用途：右侧首次触发后执行的回调，用于在 Inspector 绑定本关 Executor。
    /// 调用边界：仅运行态且右侧先触发时调用一次；执行前已锁定左右入口。
    /// 注意事项：需要显式绑定 Executor 的执行方法，空回调也会消耗本次选择。
    /// </summary>
    [SerializeField] private UnityEvent onTriggerRight = new();

    #endregion

    #region Runtime State

    private bool _hasTriggered;

    #endregion

    #region Public API

    /// <summary>
    /// 用途：供本关左侧 Trigger 的 UnityEvent 调用，执行左侧回调。
    /// 调用边界：仅 PlayMode 有效，左右入口共享一次触发机会。
    /// 注意事项：任一侧已触发后忽略调用；禁用再启用组件不会恢复触发机会。
    /// </summary>
    public void TriggerLeft()
    {
        if (!Application.isPlaying || _hasTriggered)
            return;

        if (!ConfigureDoor(rightSuccessDoorTrigger, rightEnterDoorAnimCtrl))
            return;

        _hasTriggered = true;
        onTriggerLeft?.Invoke();
    }

    /// <summary>
    /// 用途：供本关右侧 Trigger 的 UnityEvent 调用，执行右侧回调。
    /// 调用边界：仅 PlayMode 有效，左右入口共享一次触发机会。
    /// 注意事项：任一侧已触发后忽略调用；禁用再启用组件不会恢复触发机会。
    /// </summary>
    public void TriggerRight()
    {
        if (!Application.isPlaying || _hasTriggered)
            return;

        if (!ConfigureDoor(leftSuccessDoorTrigger, leftEnterDoorAnimCtrl))
            return;

        _hasTriggered = true;
        onTriggerRight?.Invoke();
    }

    #endregion

    #region Door Configuration

    private bool ConfigureDoor(OnTrigger2DHandle doorTrigger, IWannerGame_EnterDoorAnimCtrl doorAnimCtrl)
    {
        if (levelCtrl == null || doorTrigger == null || doorAnimCtrl == null)
        {
            this.LogError("[ILevel047Ctrl] 请在 Inspector 绑定本关控制器和对侧门的 Trigger、AnimCtrl。");
            return false;
        }

        levelCtrl.SetEnterDoorAnimCtrl(doorAnimCtrl);
        levelCtrl.SetSuccessDoorTrigger(doorTrigger);
        return true;
    }

    #endregion
}

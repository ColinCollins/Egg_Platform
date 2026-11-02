using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 第一笔有效画线完成后触发一次 Inspector 中绑定的回调。
/// 调用边界：挂在同一 DrawLevelCtrl 的关卡根节点或子节点，由现有画线接口分发通知。
/// 注意：尝试重置后重新等待第一笔；组件禁用期间不触发，重新启用不补发已错过的通知。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/First Stroke Completed Handle")]
public sealed class DrawFirstStrokeCompletedHandle : MonoBehaviour, IDrawStrokeCompletedHandler
{
    #region Inspector

    /// <summary>
    /// 本次尝试第一笔有效画线完成后执行的本地回调。
    /// 调用边界：在 Inspector 显式绑定目标对象和方法；同步调用，不等待异步演出结束。
    /// 注意：回调只通知完成，不负责初始化或恢复目标对象的状态。
    /// </summary>
    [Tooltip("本次尝试第一笔有效画线完成后触发一次；尝试重置后可再次触发。")]
    [SerializeField] private UnityEvent onFirstStrokeCompleted = new UnityEvent();

    #endregion

    #region Runtime State

    private bool _hasTriggered;

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 接收现有关卡主控的画线完成通知，仅在运行中且组件启用时处理第一笔。
    /// 注意：调用回调前先锁定本次尝试，避免回调同步重入造成重复触发。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !result.IsFirstStroke || _hasTriggered)
            return;

        _hasTriggered = true;
        onFirstStrokeCompleted?.Invoke();
    }

    /// <summary>
    /// 接收现有关卡主控的尝试重置通知，重新允许下一次尝试的首笔回调。
    /// 注意：禁用组件仍需恢复此门禁；目标对象自身的重置由其所属控制器负责。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying)
            return;

        _hasTriggered = false;
    }

    #endregion
}

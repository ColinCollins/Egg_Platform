using DG.Tweening;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// IWanner Transform Tween 停止器。
/// 由 TriggerExecutor/BaseItemExecutor 触发后，停止所有以指定 Transform 注册为 target 的 DOTween。
/// </summary>
public sealed class IWannerGame_StopAllDOTween : BaseItemEventHandle
{
    #region Serialized Fields

    /// <summary>
    /// 用途：指定需要停止 DOTween 表现的 Transform。
    /// 调用边界：仅在 Execute 时读取；未配置时使用当前组件所在 Transform。
    /// 注意事项：只匹配以该 Transform 注册为 target 的 Tween，不递归处理子节点或其它 Component target。
    /// </summary>
    [Header("Target")]
    [Tooltip("需要停止全部关联 DOTween 的 Transform；留空时使用当前 Transform")]
    [SerializeField] private Transform target;

    #endregion

    #region BaseItemEventHandle

    /// <summary>
    /// 用途：立即停止目标 Transform 当前关联的全部 DOTween，不把 Tween 推进到终点。
    /// 调用边界：由 TriggerExecutor/BaseItemExecutor、UnityEvent 或明确的 IWanner 关卡本地逻辑调用。
    /// 注意事项：DOTween 的 OnKill 回调仍会执行；未以目标 Transform 注册的 Tween 不受影响。
    /// </summary>
    public override void Execute()
    {
        if (!Application.isPlaying)
            return;

        IsRunning = true;
        IsDone = false;

        Transform targetTransform = target != null ? target : transform;
        targetTransform.DOKill(false);

        IsRunning = false;
        IsDone = true;
    }

    #endregion
}

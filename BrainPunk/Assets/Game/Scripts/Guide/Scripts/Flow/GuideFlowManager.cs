using System;
using System.Collections.Generic;
using System.Threading;
using Bear.Logger;
using Cysharp.Threading.Tasks;

/// <summary>
/// 引导流程集中管理器，保证所有 GuideFlow 按提交顺序串行执行。
/// </summary>
public sealed class GuideFlowManager : IDebuger
{
    #region Pending Flow

    private sealed class PendingFlow
    {
        internal GuideFlowBase Flow { get; }
        internal UniTaskCompletionSource<bool> CompletionSource { get; } = new();
        internal LinkedListNode<PendingFlow> Node { get; set; }
        internal bool IsCancelled { get; set; }

        internal PendingFlow(GuideFlowBase flow)
        {
            Flow = flow;
        }
    }

    #endregion

    #region Runtime State

    private readonly LinkedList<PendingFlow> _pendingFlows = new();

    #endregion

    #region Singleton

    /// <summary>
    /// 用途：提供当前进程内唯一的引导流程管理入口。
    /// 调用边界：仅由 Unity 主线程上的 GuideFlowBase 和只读状态查询使用。
    /// 注意事项：本实例不依赖场景对象，不负责 UI、存档或 PlayCtrl 状态切换。
    /// </summary>
    public static GuideFlowManager Instance { get; } = new();

    private GuideFlowManager()
    {
    }

    #endregion

    #region Public Query

    /// <summary>
    /// 用途：获取当前已经取得执行权的引导流程。
    /// 调用边界：仅用于运行态只读判断和调试展示。
    /// 注意事项：排队等待中的流程不会作为 CurrentFlow 返回。
    /// </summary>
    public GuideFlowBase CurrentFlow { get; private set; }

    /// <summary>
    /// 用途：判断当前是否存在正在执行的引导流程。
    /// 调用边界：仅用于运行态只读判断，不应用它替代具体 Flow 的业务资格检查。
    /// 注意事项：只有排队项、没有活动项的瞬时交接状态不会对外暴露。
    /// </summary>
    public bool HasActiveFlow => CurrentFlow != null;

    /// <summary>
    /// 用途：获取仍在等待执行且尚未取消的引导流程数量。
    /// 调用边界：用于低频状态判断和调试，不应在每帧调用。
    /// 注意事项：已经取消但尚未轮到清理的队列占位不会计入结果。
    /// </summary>
    public int PendingCount => _pendingFlows.Count;

    /// <summary>
    /// 用途：判断活动项或等待队列中是否已经存在指定类型的引导流程。
    /// 调用边界：由业务入口在创建同类流程前做防重复判断；最终资格仍由具体 Flow 校验。
    /// 注意事项：已取消的等待项会被忽略，派生类型会按泛型类型匹配。
    /// </summary>
    /// <typeparam name="T">需要查询的 GuideFlowBase 类型。</typeparam>
    /// <returns>活动项或有效等待项中存在该类型时返回 true。</returns>
    public bool Contains<T>() where T : GuideFlowBase
    {
        if (CurrentFlow is T)
            return true;

        foreach (PendingFlow pendingFlow in _pendingFlows)
        {
            if (!pendingFlow.IsCancelled && pendingFlow.Flow is T)
                return true;
        }

        return false;
    }

    #endregion

    #region GuideFlowBase Boundary

    /// <summary>
    /// 用途：让 Flow 取得唯一执行权；已有 Flow 运行时按 FIFO 顺序等待。
    /// 调用边界：仅由 GuideFlowBase.RunAsync 调用，调用前必须已经建立该 Flow 的取消令牌。
    /// 注意事项：等待期间取消会结束当前等待，但不会中断正在执行的其它 Flow。
    /// </summary>
    /// <param name="flow">请求执行权的引导流程。</param>
    /// <param name="cancellationToken">该 Flow 当前唯一的运行时取消令牌。</param>
    internal async UniTask EnterAsync(
        GuideFlowBase flow,
        CancellationToken cancellationToken)
    {
        if (flow == null)
            throw new ArgumentNullException(nameof(flow));

        cancellationToken.ThrowIfCancellationRequested();

        if (CurrentFlow == null)
        {
            CurrentFlow = flow;
            this.Log($"[GuideFlowManager] Enter: {flow.GetType().Name}");
            return;
        }

        var pendingFlow = new PendingFlow(flow);
        pendingFlow.Node = _pendingFlows.AddLast(pendingFlow);
        this.Log($"[GuideFlowManager] Enqueue: {flow.GetType().Name}, pending:{PendingCount}");

        using (cancellationToken.Register(() => CancelPending(pendingFlow, cancellationToken)))
        {
            await pendingFlow.CompletionSource.Task;
        }
    }

    /// <summary>
    /// 用途：释放当前 Flow 的执行权，并唤醒队列中下一条未取消的 Flow。
    /// 调用边界：仅由 GuideFlowBase.RunAsync 的最终收尾调用，且应晚于 OnFlowStopped。
    /// 注意事项：非当前 Flow 的释放请求会被拒绝，避免旧回调误释放新流程。
    /// </summary>
    /// <param name="flow">正在结束的引导流程。</param>
    internal void Exit(GuideFlowBase flow)
    {
        if (!ReferenceEquals(CurrentFlow, flow))
        {
            this.LogWarning(
                $"[GuideFlowManager] Exit ignored. current:{CurrentFlow?.GetType().Name ?? "null"}, request:{flow?.GetType().Name ?? "null"}");
            return;
        }

        this.Log($"[GuideFlowManager] Exit: {flow.GetType().Name}");
        CurrentFlow = null;
        PromoteNext();
    }

    #endregion

    #region Internal Helpers

    private void CancelPending(
        PendingFlow pendingFlow,
        CancellationToken cancellationToken)
    {
        if (pendingFlow == null || pendingFlow.IsCancelled || pendingFlow.Node == null)
            return;

        _pendingFlows.Remove(pendingFlow.Node);
        pendingFlow.Node = null;
        pendingFlow.IsCancelled = true;
        pendingFlow.CompletionSource.TrySetCanceled(cancellationToken);
    }

    private void PromoteNext()
    {
        while (_pendingFlows.Count > 0)
        {
            PendingFlow pendingFlow = _pendingFlows.First.Value;
            _pendingFlows.RemoveFirst();
            pendingFlow.Node = null;
            if (pendingFlow == null || pendingFlow.IsCancelled)
                continue;

            GuideFlowBase promotedFlow = pendingFlow.Flow;
            CurrentFlow = promotedFlow;
            if (pendingFlow.CompletionSource.TrySetResult(true))
            {
                this.Log(
                    $"[GuideFlowManager] Enter queued: {promotedFlow.GetType().Name}, pending:{PendingCount}");
                return;
            }

            CurrentFlow = null;
        }
    }

    #endregion
}

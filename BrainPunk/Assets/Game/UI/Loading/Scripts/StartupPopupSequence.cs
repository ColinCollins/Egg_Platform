using System;
using System.Collections.Generic;
using System.Threading;
using Bear.Logger;
using Cysharp.Threading.Tasks;

/// <summary>
/// 启动弹窗专用的一次性顺序执行器。
/// 调用边界：只由启动收尾流程组装和执行，不得作为游戏内通用 Popup 队列。
/// 注意事项：每个实例只能执行一次；弹窗步骤必须在真实 OnClose 边界调用完成回调。
/// </summary>
internal sealed class StartupPopupSequence : IDebuger
{
    #region 步骤定义

    private sealed class SequenceStep
    {
        public SequenceStep(string key, Func<CancellationToken, UniTask> executeAsync)
        {
            Key = key;
            ExecuteAsync = executeAsync;
        }

        public string Key { get; }
        public Func<CancellationToken, UniTask> ExecuteAsync { get; }
    }

    private readonly Queue<SequenceStep> _pendingSteps = new();

    #endregion

    #region 运行状态

    /// <summary>当前是否正在执行启动弹窗序列。</summary>
    internal bool IsRunning { get; private set; }

    /// <summary>当前实例是否已经启动过；无论成功或取消都不允许重用。</summary>
    internal bool HasRun { get; private set; }

    /// <summary>尚未开始执行的步骤数，不包含当前正在等待关闭的弹窗。</summary>
    internal int PendingCount => _pendingSteps.Count;

    #endregion

    #region 队列组装

    /// <summary>
    /// 向启动序列追加一个异步步骤。
    /// 调用边界：只能在 RunAsync 之前调用，步骤内部负责自身资格判断和 UI 生命周期。
    /// 注意事项：普通异常会被序列记录并隔离，不阻断后续步骤；取消会终止整个序列。
    /// </summary>
    /// <param name="stepKey">用于日志和定位的稳定步骤名称。</param>
    /// <param name="executeAsync">当轮到该步骤时执行的异步逻辑。</param>
    internal void Enqueue(string stepKey, Func<CancellationToken, UniTask> executeAsync)
    {
        EnsureCanEnqueue();

        if (string.IsNullOrWhiteSpace(stepKey))
            throw new ArgumentException("Startup popup step key cannot be empty.", nameof(stepKey));
        if (executeAsync == null)
            throw new ArgumentNullException(nameof(executeAsync));

        _pendingSteps.Enqueue(new SequenceStep(stepKey, executeAsync));
    }

    /// <summary>
    /// 追加一个“成功打开后等待真实关闭”的弹窗步骤。
    /// 调用边界：`tryOpen` 只做当前步骤的资格判断和弹窗创建；返回 false 表示跳过且立即继续后续步骤。
    /// 注意事项：弹窗打开成功后必须将 `onClosed` 保存到实例，并在 OnClose 中至少安全调用一次；重复调用不会重复完成等待。
    /// </summary>
    /// <param name="stepKey">用于日志和定位的稳定弹窗步骤名称。</param>
    /// <param name="tryOpen">接收关闭完成回调并返回是否真实打开弹窗。</param>
    internal void EnqueuePopup(string stepKey, Func<Action, bool> tryOpen)
    {
        if (tryOpen == null)
            throw new ArgumentNullException(nameof(tryOpen));

        Enqueue(stepKey, cancellationToken =>
            WaitForPopupCloseAsync(stepKey, tryOpen, cancellationToken));
    }

    private void EnsureCanEnqueue()
    {
        if (IsRunning || HasRun)
        {
            throw new InvalidOperationException(
                "Startup popup steps cannot be added after the sequence has started.");
        }
    }

    #endregion

    #region 顺序执行

    /// <summary>
    /// 按入队顺序执行所有启动弹窗步骤。
    /// 调用边界：必须使用跨场景存活对象的取消令牌，不得绑定会在 Loading 场景切换时销毁的 LoadManager。
    /// 注意事项：该实例为一次性；运行后会清空未完成步骤，如需重试必须新建序列。
    /// </summary>
    /// <param name="cancellationToken">应绑定应用或常驻 UI 生命周期的取消令牌。</param>
    internal async UniTask RunAsync(CancellationToken cancellationToken)
    {
        if (IsRunning || HasRun)
            throw new InvalidOperationException("Startup popup sequence can only run once.");

        IsRunning = true;
        HasRun = true;

        try
        {
            while (_pendingSteps.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SequenceStep step = _pendingSteps.Dequeue();

                try
                {
                    this.Log($"[StartupPopupSequence] Step start. key={step.Key}");
                    await step.ExecuteAsync(cancellationToken);
                    this.Log($"[StartupPopupSequence] Step complete. key={step.Key}");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    this.LogError(
                        $"[StartupPopupSequence] Step failed and was skipped. key={step.Key}, exception={exception}");
                }
            }
        }
        finally
        {
            _pendingSteps.Clear();
            IsRunning = false;
        }
    }

    private async UniTask WaitForPopupCloseAsync(
        string stepKey,
        Func<Action, bool> tryOpen,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var closeCompletionSource = new UniTaskCompletionSource();
        bool opened = tryOpen(() => closeCompletionSource.TrySetResult());
        if (!opened)
        {
            this.Log($"[StartupPopupSequence] Popup step skipped. key={stepKey}");
            return;
        }

        await closeCompletionSource.Task.AttachExternalCancellation(cancellationToken);
        // OnClose 由 UIManager 清理栈与资源的过程中同步调用，跨一帧后再开下一窗可避免重入修改同一 UI 栈。
        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
    }

    #endregion
}

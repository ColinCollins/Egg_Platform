using System;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 引导流程的通用执行结果。
/// </summary>
public enum GuideFlowResult
{
    /// <summary>引导流程正常完成。</summary>
    Completed,

    /// <summary>引导流程被外部或持有者主动取消。</summary>
    Cancelled,

    /// <summary>同一流程实例已经处于运行状态，本次启动未执行。</summary>
    AlreadyRunning,

    /// <summary>引导流程识别到可预期的业务失败并主动结束。</summary>
    Failed,
}

/// <summary>
/// 引导流程抽象基类，统一处理单实例防重入、集中排队、取消令牌和运行结束清理。
/// 子类只负责具体引导步骤，不应在表现组件内继续编排后续流程。
/// </summary>
public abstract class GuideFlowBase
{
    #region Runtime State

    private CancellationTokenSource _flowCancellationSource;

    #endregion

    #region Public API

    /// <summary>
    /// 用途：标识当前流程实例是否正在执行。
    /// 调用边界：仅用于主线程上的流程启动和 UI 交互判断，不提供跨线程同步保证。
    /// 注意事项：执行结束、取消或抛出异常后都会恢复为 false。
    /// </summary>
    public bool IsRunning => _flowCancellationSource != null;

    /// <summary>
    /// 用途：启动一次引导流程，把外部取消令牌与流程自身的取消入口合并，并自动进入集中管理队列。
    /// 调用边界：由具体业务入口在 Unity 主线程调用；同一实例运行期间重复调用会返回 AlreadyRunning。
    /// 注意事项：同一时刻只有一个 GuideFlow 执行；排队期间也可取消，其他异常继续向调用方传播。
    /// </summary>
    /// <param name="cancellationToken">外部生命周期取消令牌；不需要外部取消时可使用默认值。</param>
    /// <returns>具体流程返回的执行结果，或基类生成的 AlreadyRunning / Cancelled。</returns>
    public async UniTask<GuideFlowResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return GuideFlowResult.AlreadyRunning;

        CancellationTokenSource runtimeCancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _flowCancellationSource = runtimeCancellationSource;
        bool hasEnteredManager = false;

        try
        {
            runtimeCancellationSource.Token.ThrowIfCancellationRequested();
            await GuideFlowManager.Instance.EnterAsync(
                this,
                runtimeCancellationSource.Token);
            hasEnteredManager = true;
            return await ExecuteAsync(runtimeCancellationSource.Token);
        }
        catch (OperationCanceledException) when (runtimeCancellationSource.IsCancellationRequested)
        {
            return GuideFlowResult.Cancelled;
        }
        finally
        {
            if (ReferenceEquals(_flowCancellationSource, runtimeCancellationSource))
                _flowCancellationSource = null;

            runtimeCancellationSource.Dispose();

            try
            {
                OnFlowStopped();
            }
            finally
            {
                if (hasEnteredManager)
                    GuideFlowManager.Instance.Exit(this);
            }
        }
    }

    /// <summary>
    /// 用途：请求取消当前正在执行的引导流程。
    /// 调用边界：由流程宿主在关闭、销毁、切换页面或主动中止时于 Unity 主线程调用。
    /// 注意事项：本方法只发出取消信号；子类的异步等待必须使用 ExecuteAsync 收到的令牌才能及时停止。
    /// </summary>
    public void Cancel()
    {
        CancellationTokenSource cancellationSource = _flowCancellationSource;
        if (cancellationSource == null || cancellationSource.IsCancellationRequested)
            return;

        cancellationSource.Cancel();
    }

    #endregion

    #region Subclass Hooks

    /// <summary>
    /// 用途：执行具体业务的引导步骤。
    /// 调用边界：只由 RunAsync 调用；子类不得缓存或替换传入的取消令牌。
    /// 注意事项：所有延迟和等待都必须传递该令牌；可预期业务失败应返回 Failed，非预期异常应继续抛出。
    /// </summary>
    /// <param name="cancellationToken">当前流程唯一的运行时取消令牌。</param>
    /// <returns>具体引导流程的执行结果。</returns>
    protected abstract UniTask<GuideFlowResult> ExecuteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 用途：在流程完成、取消或异常退出后执行同步收尾。
    /// 调用边界：由 RunAsync 的 finally 保证调用，执行时 IsRunning 已恢复为 false。
    /// 注意事项：这里只做同步解绑和状态恢复，不应启动新的异步流程或抛出异常。
    /// </summary>
    protected virtual void OnFlowStopped()
    {
    }

    #endregion
}

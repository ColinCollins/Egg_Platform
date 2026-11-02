using System;
using System.Threading;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 明信片首次引导的页面内流程，负责从拼图引导完成一直编排到 Stage Four 收尾。
/// 流程依次为：
/// 锁定 Popup 交互并启动 Postcard_GuidePart；
/// 等待 GuidePart 与当前页完成表现、Chest、奖励链全部结束；
/// 等待 1.5 秒后播放 Stage Three；
/// 等待下一页 GuideFinger 点击并执行引导切页；
/// 等待 1.3 秒后播放 Stage Four。
/// 流程完成、失败、取消或异常退出时，统一清理等待状态并恢复 Popup 交互。
/// 当前由 PostcardPopup 持有并通过局部依赖委托复用既有 GuidePart、切页与交互锁逻辑。
/// </summary>
public sealed class PostcardGuideFlow2 : GuideFlowBase, IDebuger
{
    #region Constants

    private const float StageThreeDelay = 1.5f;
    private const float StageFourDelay = 1.3f;

    #endregion

    #region Dependencies

    private readonly RectTransform _nextPageGuideTarget;
    private readonly RectTransform _guideRoot;
    private readonly Func<Action, bool> _tryStartGuidePart;
    private readonly Func<bool> _tryGoNextPageForGuide;
    private readonly Action<bool> _setGuideInteractionLocked;

    #endregion

    #region Runtime State

    private UniTaskCompletionSource<GuideFlowResult> _guidePartCompletionSource;
    private UniTaskCompletionSource<GuideFlowResult> _pageCompleteCompletionSource;

    #endregion

    #region Public API

    /// <summary>
    /// 用途：通知 Flow2，当前 Postcard_GuidePart 的所有目标已经完成。
    /// 调用边界：由 Popup 现有 GuidePart 完成回调在 Unity 主线程调用；每次 Flow2 运行只消费一次。
    /// 注意事项：Flow2 尚未运行、已结束或已经消费该信号时返回 false，不会启动后续流程副本。
    /// </summary>
    /// <returns>本次完成信号被当前 Flow2 接受时返回 true。</returns>
    public bool NotifyGuidePartCompleted()
    {
        return _guidePartCompletionSource?.TrySetResult(GuideFlowResult.Completed) ?? false;
    }

    /// <summary>
    /// 用途：通知 Flow2，当前页正常完成表现、Chest 和奖励链已经结束，可以进入 Stage Three。
    /// 调用边界：由 Popup 页面完成流程的 finally 收尾节点在 Unity 主线程调用；允许早于 GuidePart 完成信号到达。
    /// 注意事项：Flow2 尚未运行、已结束或已经消费该信号时返回 false。
    /// </summary>
    /// <returns>本次页面完成信号被当前 Flow2 接受时返回 true。</returns>
    public bool NotifyPageComplete()
    {
        return _pageCompleteCompletionSource?.TrySetResult(GuideFlowResult.Completed) ?? false;
    }

    /// <summary>
    /// 用途：通知 Flow2，GuidePart 或页面完成阶段发生无法继续的业务失败。
    /// 调用边界：由 Popup 在 Stage Three 开始前的既有降级失败点于 Unity 主线程调用。
    /// 注意事项：只结束仍在等待的 GuidePart/页面完成阶段；进入 Stage Three 后如需中止应调用 Cancel()。
    /// </summary>
    /// <returns>至少一个当前等待阶段接受失败信号时返回 true。</returns>
    public bool NotifyFailed()
    {
        bool guidePartAccepted =
            _guidePartCompletionSource?.TrySetResult(GuideFlowResult.Failed) ?? false;
        bool pageCompleteAccepted =
            _pageCompleteCompletionSource?.TrySetResult(GuideFlowResult.Failed) ?? false;
        return guidePartAccepted || pageCompleteAccepted;
    }

    #endregion

    #region Construction

    /// <summary>
    /// 用途：创建一次由指定页面依赖承载的明信片页面内引导流程。
    /// 调用边界：仅在 Unity 主线程创建；构造不会启动 GuidePart、修改存档或打开引导弹窗，需显式调用 RunAsync(...)。
    /// 注意事项：依赖缺失时运行返回 Failed；委托应复用 Popup 既有逻辑，不得在 Flow2 外复制存档或奖励流程。
    /// </summary>
    /// <param name="nextPageGuideTarget">Stage Three 结束后的下一页 GuideFinger 目标。</param>
    /// <param name="guideRoot">创建下一页 GuideFinger 使用的 UI 父节点。</param>
    /// <param name="tryStartGuidePart">同步启动既有 GuidePart 的局部入口；Flow2 会传入一次性完成回调。</param>
    /// <param name="tryGoNextPageForGuide">复用 Popup 原切页、刷新和保存链的引导切页入口。</param>
    /// <param name="setGuideInteractionLocked">切换 Popup 本地关闭与非引导翻页交互锁的入口。</param>
    public PostcardGuideFlow2(
        RectTransform nextPageGuideTarget,
        RectTransform guideRoot,
        Func<Action, bool> tryStartGuidePart,
        Func<bool> tryGoNextPageForGuide,
        Action<bool> setGuideInteractionLocked)
    {
        _nextPageGuideTarget = nextPageGuideTarget;
        _guideRoot = guideRoot;
        _tryStartGuidePart = tryStartGuidePart;
        _tryGoNextPageForGuide = tryGoNextPageForGuide;
        _setGuideInteractionLocked = setGuideInteractionLocked;
    }

    #endregion

    #region GuideFlowBase

    /// <summary>
    /// 用途：顺序执行页面内拼图完成、Stage Three、下一页 Finger 和 Stage Four。
    /// 调用边界：只由 GuideFlowBase.RunAsync(...) 调用；调用前应由 Flow1 完成 StartGuding() 的既有存档交接。
    /// 注意事项：所有等待沿用同一取消令牌；任一阶段未完成都会立即返回对应结果，并在统一收尾中恢复 Popup 交互。
    /// </summary>
    protected override async UniTask<GuideFlowResult> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        if (!Application.isPlaying ||
            _tryStartGuidePart == null ||
            _tryGoNextPageForGuide == null ||
            _setGuideInteractionLocked == null)
            return GuideFlowResult.Failed;

        if (!TrySetGuideInteractionLocked(true))
            return GuideFlowResult.Failed;

        UniTaskCompletionSource<GuideFlowResult> guidePartCompletionSource =
            new UniTaskCompletionSource<GuideFlowResult>();
        UniTaskCompletionSource<GuideFlowResult> pageCompleteCompletionSource =
            new UniTaskCompletionSource<GuideFlowResult>();
        _guidePartCompletionSource = guidePartCompletionSource;
        _pageCompleteCompletionSource = pageCompleteCompletionSource;

        if (!_tryStartGuidePart(() => NotifyGuidePartCompleted()))
            return GuideFlowResult.Failed;

        GuideFlowResult pageCompleteResult;
        using (cancellationToken.Register(() =>
               {
                   guidePartCompletionSource.TrySetResult(GuideFlowResult.Cancelled);
                   pageCompleteCompletionSource.TrySetResult(GuideFlowResult.Cancelled);
               }))
        {
            GuideFlowResult guidePartResult = await guidePartCompletionSource.Task;
            if (guidePartResult != GuideFlowResult.Completed)
                return guidePartResult;

            pageCompleteResult = await pageCompleteCompletionSource.Task;
        }

        if (pageCompleteResult != GuideFlowResult.Completed)
            return pageCompleteResult;

        await UniTask.WaitForSeconds(
            StageThreeDelay,
            cancellationToken: cancellationToken);

        GuideFlowResult stageThreeResult = await NormalGuidePopup.ShowAsync(
            PostcardStageStep.Three,
            cancellationToken);
        if (stageThreeResult != GuideFlowResult.Completed)
            return stageThreeResult;

        if (_nextPageGuideTarget == null ||
            !_nextPageGuideTarget.gameObject.activeInHierarchy ||
            _guideRoot == null)
            return GuideFlowResult.Failed;

        GuideFlowResult fingerResult = await GuideFinger.WaitForClickAsync(
            _nextPageGuideTarget,
            _guideRoot,
            cancellationToken);
        if (fingerResult != GuideFlowResult.Completed)
            return fingerResult;

        if (!_tryGoNextPageForGuide())
            return GuideFlowResult.Failed;

        await UniTask.WaitForSeconds(
            StageFourDelay,
            cancellationToken: cancellationToken);

        return await NormalGuidePopup.ShowAsync(
            PostcardStageStep.Four,
            cancellationToken);
    }

    /// <summary>
    /// 流程完成、失败、取消或异常退出时恢复 Popup 的普通交互状态。
    /// </summary>
    protected override void OnFlowStopped()
    {
        _guidePartCompletionSource = null;
        _pageCompleteCompletionSource = null;
        TrySetGuideInteractionLocked(false);
    }

    #endregion

    #region Internal Helpers

    private bool TrySetGuideInteractionLocked(bool isLocked)
    {
        if (_setGuideInteractionLocked == null)
            return false;

        try
        {
            _setGuideInteractionLocked(isLocked);
            return true;
        }
        catch (Exception exception)
        {
            this.LogError($"[PostcardGuideFlow2] Set guide interaction lock failed. isLocked:{isLocked}, error:{exception}");
            return false;
        }
    }

    #endregion
}

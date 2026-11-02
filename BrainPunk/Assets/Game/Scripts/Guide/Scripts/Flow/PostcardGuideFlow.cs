using System;
using System.Threading;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Game.ConfigModule;
using UnityEngine;

/// <summary>
/// 明信片首次引导的入口流程，负责串联胜利页入口、前两阶段通用弹窗和页内引导交接。
/// 当前由 GameVictoryPanel 持有入口流程，并在 Stage Two 后把页面内阶段交接给 PostcardPopup 持有的 Flow2。
/// </summary>
public sealed class PostcardGuideFlow : GuideFlowBase, IDebuger
{
    #region Constants

    private const float StageOneDelay = 3f;
    private const float StageTwoDelay = 1.5f;

    #endregion

    #region Dependencies

    private readonly PostcardManager _postcardManager;
    private readonly PostcardEnter _postcardEntryTarget;
    private readonly RectTransform _fingerRoot;
    private readonly Action<bool> _setInteractionBlocked;

    #endregion

    #region Runtime State

    private PostcardPopup _postcardPopup;
    private bool _hasHandedOffPostcardGuide;

    #endregion

    #region Construction

    /// <summary>
    /// 用途：创建一次明信片首次引导入口流程，并注入当前胜利页和明信片模块所需依赖。
    /// 调用边界：仅在 Unity 主线程创建；构造不会打开 UI、修改存档或启动异步任务，需显式调用 RunAsync(...)。
    /// 注意事项：postcardManager、postcardEntryTarget 或 fingerRoot 缺失时，运行会返回 Failed；交互锁回调可为空。
    /// </summary>
    /// <param name="postcardManager">现有明信片管理器，用于补充引导星星和写入页内引导状态。</param>
    /// <param name="postcardEntryTarget">胜利页的明信片入口按钮，同时作为第一枚 GuideFinger 的目标。</param>
    /// <param name="fingerRoot">第一枚 GuideFinger 的 UI 父节点。</param>
    /// <param name="setInteractionBlocked">切换胜利页交互遮罩的局部回调；true 表示阻止其它点击。</param>
    public PostcardGuideFlow(
        PostcardManager postcardManager,
        PostcardEnter postcardEntryTarget,
        RectTransform fingerRoot,
        Action<bool> setInteractionBlocked)
    {
        _postcardManager = postcardManager;
        _postcardEntryTarget = postcardEntryTarget;
        _fingerRoot = fingerRoot;
        _setInteractionBlocked = setInteractionBlocked;
    }

    #endregion

    #region GuideFlowBase

    /// <summary>
    /// 用途：判断当前关卡是否满足首次明信片引导的只读展示门槛。
    /// 调用边界：仅运行态的关卡完成流程调用；本方法不负责创建弹窗。
    /// 注意事项：只读取 RemoteConfig 关卡门槛和 `DB.Postcard.HasGuide`，不修改或保存任何数据。
    /// </summary>
    /// <param name="levelSortId">当前完成关卡的排序 id。</param>
    /// <returns>满足只读门槛时返回 true。</returns>
    public static bool IsShow(int levelSortId)
    {
        if (!Application.isPlaying)
            return false;

        if (ConfigManager.RemoteConfig.GetPostcardGuideLevel() > levelSortId)
            return false;

        if (HasGuide())
            return false;

        return true;
    }

    private static bool HasGuide()
    {
        var postcardData = DB.Postcard;
        return postcardData == null || postcardData.HasGuide;
    }

    /// <summary>
    /// 用途：执行首次明信片引导的入口阶段，并在 Stage Two 关闭后交接给现有 PostcardPopup 页内引导。
    /// 调用边界：只由 GuideFlowBase.RunAsync(...) 调用；不负责 IsShowNormalGuidePopup(...) 的展示资格判断。
    /// 注意事项：Stage Three/Four 继续由 PostcardPopup 页面完成流程处理；所有等待沿用统一取消令牌。
    /// </summary>
    protected override async UniTask<GuideFlowResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!Application.isPlaying ||
            _postcardManager == null ||
            _postcardEntryTarget == null ||
            _fingerRoot == null)
        {
            return GuideFlowResult.Failed;
        }

        // 补充 star 数量
        _postcardManager.FullNeededStarCountForGuide();
        _postcardEntryTarget.ForceShow();
        if (!TrySetInteractionBlocked(true))
            return GuideFlowResult.Failed;

        // 等待 星星表现
        await UniTask.WaitForSeconds(StageOneDelay, cancellationToken: cancellationToken);

        // postcard guide step1
        GuideFlowResult stageOneResult = await NormalGuidePopup.ShowAsync(
            PostcardStageStep.One,
            cancellationToken);
        if (stageOneResult != GuideFlowResult.Completed)
            return stageOneResult;

        // Stage One 结束后释放 VictoryPanel 的引导阻挡，允许入口 Finger 接收点击。
        if (!TrySetInteractionBlocked(false))
            return GuideFlowResult.Failed;

        //  手指引导点击 enter
        GuideFlowResult fingerResult = await GuideFinger.WaitForClickAsync(
            _postcardEntryTarget.transform as RectTransform,
            _fingerRoot,
            cancellationToken);
        if (fingerResult != GuideFlowResult.Completed)
            return fingerResult;

        // Popup 创建前写入 HasGuide/IsGuiding，确保 Popup 打开首帧即可启用自身 ClickBlock。
        _postcardManager.StartGuding();

        // 明信片弹出
        _postcardPopup = PostcardPopup.Create();
        if (_postcardPopup == null)
            return GuideFlowResult.Failed;

        _postcardPopup.SetPostcardGuideInteractionLocked(true);

        await UniTask.WaitForSeconds(StageTwoDelay, cancellationToken: cancellationToken);

        // 展示 step2
        GuideFlowResult stageTwoResult = await NormalGuidePopup.ShowAsync(
            PostcardStageStep.Two,
            cancellationToken);
        if (stageTwoResult == GuideFlowResult.Failed)
            return StartPendingPostcardGuide();

        if (stageTwoResult != GuideFlowResult.Completed)
            return stageTwoResult;

        return StartPendingPostcardGuide();
    }

    /// <summary>
    /// 流程结束时恢复胜利页交互锁，并释放本流程持有的 PostcardPopup 引用。
    /// </summary>
    protected override void OnFlowStopped()
    {
        TrySetInteractionBlocked(false);

        if (!_hasHandedOffPostcardGuide && _postcardPopup != null)
            _postcardPopup.SetPostcardGuideInteractionLocked(false);

        _postcardPopup = null;
        _hasHandedOffPostcardGuide = false;
    }

    #endregion

    #region Internal Helpers

    private GuideFlowResult StartPendingPostcardGuide()
    {
        if (_postcardManager == null || _postcardPopup == null)
            return GuideFlowResult.Failed;

        if (!_postcardPopup.StartPendingPostcardGuide())
            return GuideFlowResult.Failed;

        _hasHandedOffPostcardGuide = true;
        return GuideFlowResult.Completed;
    }

    private bool TrySetInteractionBlocked(bool isBlocked)
    {
        try
        {
            _setInteractionBlocked?.Invoke(isBlocked);
            return true;
        }
        catch (Exception exception)
        {
            this.LogError($"[PostcardGuideFlow] Set interaction block failed. isBlocked:{isBlocked}, error:{exception}");
            return false;
        }
    }

    #endregion
}

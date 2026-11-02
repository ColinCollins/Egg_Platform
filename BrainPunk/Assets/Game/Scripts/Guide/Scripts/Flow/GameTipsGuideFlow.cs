using System;
using System.Threading;
using Bear.Logger;
using Config.Game;
using Cysharp.Threading.Tasks;
using Game.ConfigModule;
using Game.Play;
using UnityEngine;



// 加载 NormalGuidePopup 功能
/// <summary>
/// 首次提示剧情引导使用的通用引导弹窗阶段；枚举值对应同编号的 NormalGuidePopup 资源。
/// </summary>
public enum GameTipsGuideStep
{
    One = 20001
}

/// <summary>
/// 首次提示剧情引导的单关配置快照，避免更新阶段重复解析 RemoteConfig JSON。
/// </summary>
internal readonly struct GameTipsGuideTriggerSettings
{
    internal int MinimumLevel { get; }
    internal int FailedCount { get; }
    internal float WaitingTimeSeconds { get; }

    internal GameTipsGuideTriggerSettings(int minimumLevel, int failedCount, float waitingTimeSeconds)
    {
        MinimumLevel = minimumLevel;
        FailedCount = failedCount;
        WaitingTimeSeconds = waitingTimeSeconds;
    }
}

/// <summary>
/// 首次提示剧情引导，负责串联头像对话、提示入口手指、首次免费提示与关闭后的收尾对话。
/// </summary>
public sealed class GameTipsGuideFlow : GuideFlowBase, IDebuger
{
    #region Constants

    private const int MaximumGuideLevelId = 10;
    private const float PopupDialogueDelaySeconds = 0.5f;
    private const string TipsToolKey = nameof(GameProps.Tips);

    #endregion

    #region Dependencies

    private readonly LevelRuntimeState _runtimeState;
    private readonly GameTipsGuideTriggerSettings _triggerSettings;
    private readonly RectTransform _tipsButtonTarget;
    private readonly RectTransform _gamePanelRoot;
    private readonly Func<GameTipsPopup> _openFirstFreeTipsPopup;

    #endregion

    #region Runtime State

    private GameTipsPopup _normalTipsPopup;

    #endregion

    #region Construction

    /// <summary>
    /// 用途：创建一次首次提示剧情引导并注入当前关卡、提示入口和弹窗创建边界。
    /// 调用边界：仅由 PlayCtrl 在 Unity 主线程创建，构造本身不修改存档或打开 UI。
    /// 注意事项：触发关卡、重试次数和等待时间由 Playing 状态进入时读取的配置快照提供。
    /// </summary>
    /// <param name="runtimeState">当前关卡运行时状态，用于校验提示使用、累计停留和重试次数。</param>
    /// <param name="triggerSettings">当前 Playing 状态进入时读取的引导配置快照。</param>
    /// <param name="tipsButtonTarget">GamePlayPanel 的提示按钮目标。</param>
    /// <param name="gamePanelRoot">第一枚 GuideFinger 的 UI 父节点。</param>
    /// <param name="openFirstFreeTipsPopup">复用 GamePlayPanel 暂停与提示弹窗入口的局部回调。</param>
    internal GameTipsGuideFlow(
        LevelRuntimeState runtimeState,
        GameTipsGuideTriggerSettings triggerSettings,
        RectTransform tipsButtonTarget,
        RectTransform gamePanelRoot,
        Func<GameTipsPopup> openFirstFreeTipsPopup)
    {
        _runtimeState = runtimeState;
        _triggerSettings = triggerSettings;
        _tipsButtonTarget = tipsButtonTarget;
        _gamePanelRoot = gamePanelRoot;
        _openFirstFreeTipsPopup = openFirstFreeTipsPopup;
    }

    #endregion

    #region Eligibility

    /// <summary>
    /// 用途：从 RemoteConfig API 读取首次提示剧情引导的单关配置快照。
    /// 调用边界：由 PlayCtrl_Playing 进入状态时调用一次，后续更新与流程校验复用返回值。
    /// 注意事项：RemoteConfig API 已包含 GlobalConst 回退，调用方不应再次读取或覆盖单项默认值。
    /// </summary>
    internal static GameTipsGuideTriggerSettings LoadTriggerSettings()
    {
        return new GameTipsGuideTriggerSettings(
            ConfigManager.RemoteConfig.GetGameTipsGuideLevel(),
            ConfigManager.RemoteConfig.GetGameTipsGuideFailedCount(),
            ConfigManager.RemoteConfig.GetGameTipsGuideWaitingTime());
    }

    /// <summary>
    /// 用途：判断当前关卡是否位于前 10 关，并达到首次提示引导配置的关卡、停留时间或重试次数门槛。
    /// 调用边界：由 PlayCtrl_Playing 的既有更新节点调用；本方法只读运行时状态，不访问或写入存档。
    /// 注意事项：第 10 关包含在触发范围内；重试门槛小于等于 0 时只允许停留时间条件触发。
    /// </summary>
    /// <param name="runtimeState">当前关卡运行时状态。</param>
    /// <param name="settings">当前 Playing 状态进入时读取的引导配置快照。</param>
    /// <returns>达到时间或重试任一门槛时返回 true。</returns>
    internal static bool HasReachedTrigger(
        LevelRuntimeState runtimeState,
        GameTipsGuideTriggerSettings settings)
    {
        if (!Application.isPlaying || runtimeState == null)
            return false;

        if (runtimeState.LevelId < settings.MinimumLevel ||
            runtimeState.LevelId > MaximumGuideLevelId)
            return false;

        // 等待时间
        bool hasStayedLongEnough = runtimeState.CurrentAttemptTimeSeconds >= settings.WaitingTimeSeconds;

        // 重试次数
        bool hasRetriedEnough = settings.FailedCount > 0 &&
                                runtimeState.ResetCount + runtimeState.FailCount >= settings.FailedCount;
        return hasStayedLongEnough || hasRetriedEnough;
    }

    /// <summary>
    /// 用途：判断玩家是否仍具备首次提示引导资格。
    /// 调用边界：只在 HasReachedTrigger(...) 成立后、正式创建流程前于运行态调用。
    /// 注意事项：只读取 GameData 和当前关卡提示状态，不消费资格也不保存数据。
    /// </summary>
    /// <returns>玩家没有 Tips 消耗记录且从未触发本引导时返回 true。</returns>
    public static bool CanStart(LevelRuntimeState runtimeState)
    {
        if (!Application.isPlaying || runtimeState == null || runtimeState.HasTips || runtimeState.IsAttemptTransitioning)
            return false;

        var gameData = DB.GameData;
        return gameData != null &&
               !HasUsedTips() &&
               !gameData.HasTriggeredGameTipsGuide;
    }

    #endregion

    #region GuideFlowBase

    /// <summary>
    /// 用途：按需求顺序执行首次提示剧情、两次手指点击、免费提示和可选收尾剧情。
    /// 调用边界：只由 GuideFlowBase.RunAsync(...) 调用，所有等待共用同一个取消令牌。
    /// 注意事项：最后一句仅在 PassLevel 提示弹窗由 CloseBtn（X）关闭时播放。
    /// </summary>
    protected override async UniTask<GuideFlowResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!Application.isPlaying ||
            PlayCtrl.Instance?.CheckState(GamePlayStateName.PLAYING) != true ||
            _runtimeState == null ||
            _tipsButtonTarget == null ||
            _gamePanelRoot == null ||
            _openFirstFreeTipsPopup == null ||
            !HasReachedTrigger(_runtimeState, _triggerSettings) ||
            !CanStart(_runtimeState))
        {
            return GuideFlowResult.Failed;
        }

        if (!TryMarkGuideTriggered())
            return GuideFlowResult.Failed;

        GuideFlowResult openingResult = await NormalGuidePopup.ShowAsync(
            GameTipsGuideStep.One,
            cancellationToken,
            skinName: null);
        if (openingResult != GuideFlowResult.Completed)
            return openingResult;

        GuideFlowResult entryFingerResult = await GuideFinger.WaitForClickAsync(
            _tipsButtonTarget,
            _gamePanelRoot,
            cancellationToken);
        if (entryFingerResult != GuideFlowResult.Completed)
            return entryFingerResult;

        _normalTipsPopup = _openFirstFreeTipsPopup.Invoke();
        if (_normalTipsPopup == null)
            return GuideFlowResult.Failed;

        if (!_normalTipsPopup.SetClickBlocked(true))
            return GuideFlowResult.Failed;

        await UniTask.WaitForSeconds(
            PopupDialogueDelaySeconds,
            cancellationToken: cancellationToken);

        GuideFlowResult freeDialogueResult = await GuideDialogue.ShowAsync(
            GuideDialogueVariant.GameTipsFree,
            cancellationToken);
        if (freeDialogueResult != GuideFlowResult.Completed)
            return freeDialogueResult;

        if (!_normalTipsPopup.EnableFirstGuideFreeButton())
            return GuideFlowResult.Failed;

        RectTransform freeButtonTarget = _normalTipsPopup.FirstGuideFreeButtonTarget;
        RectTransform popupRoot = _normalTipsPopup.transform as RectTransform;
        GuideFlowResult freeFingerResult = await GuideFinger.WaitForClickAsync(
            freeButtonTarget,
            popupRoot,
            cancellationToken);
        if (freeFingerResult != GuideFlowResult.Completed)
            return freeFingerResult;

        GameTipsPopup passLevelPopup = _normalTipsPopup.UseFirstGuideFreeHint();
        if (passLevelPopup == null)
            return GuideFlowResult.Failed;

        bool closedByX = await passLevelPopup.WaitForFirstGuideCloseByXAsync(cancellationToken);
        if (!closedByX)
            return GuideFlowResult.Completed;

        return await GuideDialogue.ShowAsync(
            GuideDialogueVariant.GameTipsClosing,
            cancellationToken);
    }

    /// <summary>
    /// 流程结束时解除未消费的首次免费锁，避免取消或失败后留下不可点击的提示弹窗。
    /// </summary>
    protected override void OnFlowStopped()
    {
        if (_normalTipsPopup != null)
        {
            _normalTipsPopup.SetClickBlocked(false);
            if (_normalTipsPopup.IsOpen)
                _normalTipsPopup.CancelFirstGuideFreeMode();
        }

        _normalTipsPopup = null;
    }

    #endregion

    #region Internal Helpers

    private bool TryMarkGuideTriggered()
    {
        var gameData = DB.GameData;
        if (gameData == null || HasUsedTips() || gameData.HasTriggeredGameTipsGuide)
            return false;

        gameData.HasTriggeredGameTipsGuide = true;
        if (!DB.SaveGameData())
            this.LogWarning("[GameTipsGuideFlow] Failed to save the triggered state; the current session remains consumed.");

        return true;
    }

    private static bool HasUsedTips()
    {
        var toolConsumesCache = DB.GameData?.ToolConsumesCache;
        return toolConsumesCache != null &&
               toolConsumesCache.TryGetValue(TipsToolKey, out int tipsUseCount) &&
               tipsUseCount > 0;
    }

    #endregion
}

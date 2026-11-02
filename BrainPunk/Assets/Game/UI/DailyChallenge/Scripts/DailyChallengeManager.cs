using System;
using System.Collections.Generic;
using Bear.Logger;
using Bear.SaveModule;
using Config.Game;
using Game.ConfigModule;
using GameCommon;
using UnityEngine;

/// <summary>
/// 每日挑战入口需要的只读状态快照。
/// 快照中的阶段进度按阶段独立计算，第二阶段不会继续显示累计总进度。
/// </summary>
public sealed class DailyChallengeViewState
{
    #region Public Properties

    /// <summary>当前入口是否应该显示。</summary>
    public bool IsVisible { get; }

    /// <summary>当日累计进度，仅用于存档和跨阶段位置计算。</summary>
    public int TotalProgress { get; }

    /// <summary>当前阶段 id，第一阶段为 1，第二阶段为 2。</summary>
    public int CurrentStageId { get; }

    /// <summary>当前阶段内的进度。</summary>
    public int CurrentStageProgress { get; }

    /// <summary>当前阶段需要完成的目标数量。</summary>
    public int CurrentStageTarget { get; }

    /// <summary>第一阶段需要完成的目标数量。</summary>
    public int FirstStageTarget { get; }

    /// <summary>第二阶段的累计总目标，对应每日挑战 goals[1] 的原始配置值。</summary>
    public int SecondStageTarget { get; }

    /// <summary>第一阶段是否已经达成。</summary>
    public bool IsFirstStageReached { get; }

    /// <summary>第二阶段是否已经达成。</summary>
    public bool IsSecondStageReached { get; }

    /// <summary>第一阶段奖励是否已经领取。</summary>
    public bool IsFirstRewardClaimed { get; }

    /// <summary>第二阶段奖励是否已经领取。</summary>
    public bool IsSecondRewardClaimed { get; }

    /// <summary>当前阶段的进度比例，供两张阶段独立进度图显示。</summary>
    public float CurrentStageNormalizedProgress { get; }

    /// <summary>累计进度按两个阶段分别切分后的比例，供 Btn 收起进度条与 Popup 游标兜底使用。</summary>
    public float ExpandedNormalizedProgress { get; }

    /// <summary>当日两个阶段是否全部完成。</summary>
    public bool IsCompleted => IsSecondStageReached;

    #endregion

    #region Lifecycle

    /// <summary>
    /// 创建每日挑战入口的只读状态。
    /// 调用边界：仅由 DailyChallengeManager 根据配置和存档构建，UI 不应自行拼装状态。
    /// 注意事项：阶段目标必须已经完成有效性检查。
    /// </summary>
    public DailyChallengeViewState(
        bool isVisible,
        int totalProgress,
        int currentStageId,
        int currentStageProgress,
        int currentStageTarget,
        int firstStageTarget,
        int secondStageTarget,
        bool isFirstStageReached,
        bool isSecondStageReached,
        bool isFirstRewardClaimed,
        bool isSecondRewardClaimed,
        float currentStageNormalizedProgress,
        float expandedNormalizedProgress)
    {
        IsVisible = isVisible;
        TotalProgress = totalProgress;
        CurrentStageId = currentStageId;
        CurrentStageProgress = currentStageProgress;
        CurrentStageTarget = currentStageTarget;
        FirstStageTarget = firstStageTarget;
        SecondStageTarget = secondStageTarget;
        IsFirstStageReached = isFirstStageReached;
        IsSecondStageReached = isSecondStageReached;
        IsFirstRewardClaimed = isFirstRewardClaimed;
        IsSecondRewardClaimed = isSecondRewardClaimed;
        CurrentStageNormalizedProgress = currentStageNormalizedProgress;
        ExpandedNormalizedProgress = expandedNormalizedProgress;
    }

    #endregion
}

/// <summary>
/// 单次胜利产生的每日挑战结算快照。
/// 保存动画前后状态和当前可领取阶段，避免 Popup 再次读取并拼装业务数据。
/// </summary>
public sealed class DailyChallengeSettlement
{
    #region Public Properties

    /// <summary>本次胜利记入前的每日挑战状态。</summary>
    public DailyChallengeViewState BeforeState { get; }

    /// <summary>本次胜利记入后的每日挑战状态。</summary>
    public DailyChallengeViewState AfterState { get; }

    /// <summary>当前已经达成但尚未发放的阶段 id。</summary>
    public IReadOnlyList<int> PendingRewardStageIds { get; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// 创建一次胜利结算快照。
    /// 调用边界：仅由 DailyChallengeManager 在成功流程中创建。
    /// 注意事项：传入列表会被复制，Popup 不能修改 Manager 内部状态。
    /// </summary>
    public DailyChallengeSettlement(
        DailyChallengeViewState beforeState,
        DailyChallengeViewState afterState,
        IReadOnlyList<int> pendingRewardStageIds)
    {
        BeforeState = beforeState;
        AfterState = afterState;
        PendingRewardStageIds = pendingRewardStageIds == null
            ? Array.Empty<int>()
            : new List<int>(pendingRewardStageIds);
    }

    #endregion
}

/// <summary>
/// 交给 CommonGetRewardPopup 的每日挑战奖励数据。
/// Manager 负责选择并发放普通奖励，通用奖励弹窗只展示该份数据并继续处理广告额外奖励。
/// </summary>
public sealed class DailyChallengeRewardData
{
    #region Public Properties

    /// <summary>奖励所属阶段 id。</summary>
    public int StageId { get; }

    /// <summary>通用奖励弹窗可直接解析的奖励字典。</summary>
    public Dictionary<GameProps, object> Rewards { get; }

    /// <summary>通用奖励弹窗的广告倍率；Skin 不参与倍率。</summary>
    public int Multiple { get; }

    /// <summary>本次是否包含随机 Skin。</summary>
    public bool HasSkinReward { get; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// 创建阶段奖励展示数据。
    /// 调用边界：仅由 DailyChallengeManager 在阶段已达成且未领取时创建。
    /// 注意事项：Rewards 会同时用于 Manager 普通发奖和 CommonGetRewardPopup 展示，创建后不应再修改。
    /// </summary>
    public DailyChallengeRewardData(
        int stageId,
        Dictionary<GameProps, object> rewards,
        int multiple,
        bool hasSkinReward)
    {
        StageId = stageId;
        Rewards = rewards ?? new Dictionary<GameProps, object>();
        Multiple = Mathf.Max(1, multiple);
        HasSkinReward = hasSkinReward;
    }

    #endregion
}

/// <summary>
/// 每日挑战数据中心。
/// 由 GameManager 持有，负责配置、存档和每日重置边界，UI 只读取状态快照。
/// </summary>
public sealed class DailyChallengeManager : IScheduleRefreshable, IDisposable, IDebuger
{
    #region Constants

    private const int FirstStageId = 1;
    private const int SecondStageId = 2;
    private const int StageCount = 2;
    private const float StageNormalizedSpan = 1f / StageCount;

    #endregion

    #region Public Properties

    /// <summary>供 DailyScheduleModule 日志和注册查询使用的模块标识。</summary>
    public string RefreshKey => nameof(DailyChallengeManager);

    #endregion

    #region Public API

    /// <summary>
    /// 获取每日挑战入口的只读状态。
    /// 调用边界：仅供运行时 UI 刷新使用；编辑器预览不会访问 DB 或初始化运行时单例。
    /// 注意事项：配置、存档或解锁条件无效时返回隐藏状态。
    /// </summary>
    public DailyChallengeViewState GetViewState()
    {
        if (!TryGetRuntimeContext(out int firstStageTarget, out int secondStageTarget))
            return CreateHiddenState();

        return CreateViewState(firstStageTarget, secondStageTarget);
    }

    /// <summary>
    /// 检测本次成功通关是否达到每日挑战解锁关卡，并持久化一次性解锁标记。
    /// 调用边界：仅由 PlayCtrl_Success 在 SUCCESS 状态入口、记录每日挑战进度前调用。
    /// 注意事项：已解锁时直接返回 true；保存失败会回滚内存标记，等待后续成功通关重试。
    /// </summary>
    /// <param name="completedLevel">本次成功通关的关卡序号。</param>
    /// <returns>解锁标记已经有效并成功持久化时返回 true。</returns>
    public bool TryUnlock(int completedLevel)
    {
        if (!Application.isPlaying || DB.GameData == null)
            return false;

        if (DB.GameData.HasUnlockDailyChallenge)
            return true;

        int unlockLevel = ConfigManager.RemoteConfig.GetDailyChallengeUnlockLevel();
        if (completedLevel < unlockLevel)
            return false;

        DB.GameData.HasUnlockDailyChallenge = true;
        if (DB.SaveGameData())
            return true;

        DB.GameData.HasUnlockDailyChallenge = false;
        this.LogWarning(
            $"[DailyChallengeManager] Unlock state save failed and was rolled back. completedLevel:{completedLevel}, unlockLevel:{unlockLevel}");
        return false;
    }

    /// <summary>
    /// 记录一次成功通关，并返回供 DailyChallengePopup 使用的动画前后快照。
    /// 调用边界：由主线 PlayCtrl_Success 或 DrawPlayCtrl 在一次成功结算入口调用一次。
    /// 注意事项：同一胜利去重键每天只累加一次，达到最终进度时不再累加；仍有未领奖阶段时会继续返回结算，用于补发奖励。
    /// </summary>
    /// <param name="victoryKey">本次完成记录的日内去重键；主线使用正关卡序号，Draw 使用 20001 起的独立正业务 ID。</param>
    /// <param name="settlement">需要展示 Popup 时返回的结算快照。</param>
    /// <returns>是否需要在当前玩法的胜利面板前打开 DailyChallengePopup。</returns>
    public bool TryRecordVictory(int victoryKey, out DailyChallengeSettlement settlement)
    {
        settlement = null;
        if (!TryGetRuntimeContext(out int firstStageTarget, out int secondStageTarget))
            return false;

        DailyChallengeViewState beforeState = CreateViewState(firstStageTarget, secondStageTarget);
        int finalTarget = firstStageTarget + secondStageTarget;
        int beforeProgress = beforeState.TotalProgress;
        List<int> dailyPassedLevels = DB.GameSchedule.DailyChallengePassedLevel;
        bool isFirstDailyLevelVictory = !dailyPassedLevels.Contains(victoryKey);
        if (isFirstDailyLevelVictory)
        {
            dailyPassedLevels.Add(victoryKey);
            if (beforeProgress < finalTarget)
                DB.GameSchedule.DailyChallengeCurrentProcess = beforeProgress + 1;

            if (!DB.SaveGameSchedule())
            {
                dailyPassedLevels.Remove(victoryKey);
                DB.GameSchedule.DailyChallengeCurrentProcess = beforeProgress;
                this.LogWarning("[DailyChallengeManager] Victory daily state save failed; progress and daily passed level were rolled back.");
                return false;
            }
        }

        DailyChallengeViewState afterState = CreateViewState(firstStageTarget, secondStageTarget);
        List<int> pendingRewardStageIds = GetPendingRewardStageIds(afterState);
        bool hasProgressChange = afterState.TotalProgress > beforeState.TotalProgress;
        if (!hasProgressChange && pendingRewardStageIds.Count == 0)
            return false;

        settlement = new DailyChallengeSettlement(beforeState, afterState, pendingRewardStageIds);
        return true;
    }

    /// <summary>
    /// 创建已达成阶段对应的通用奖励弹窗数据。
    /// 调用边界：由 TryClaimRewardOnPopupOpen 在 DailyChallengePopup 初始化领奖数据时调用。
    /// 注意事项：本方法只选择奖励，不发放、不写领奖状态；所有阶段使用统一配置的数量奖励广告倍率，Skin 仍只发放一次。
    /// </summary>
    public bool TryCreateRewardData(int stageId, out DailyChallengeRewardData rewardData)
    {
        rewardData = null;
        if (!Application.isPlaying
            || DB.GameSchedule == null
            || !TryGetStageTargets(out int firstStageTarget, out int secondStageTarget)
            || !IsStageReached(stageId, firstStageTarget, secondStageTarget)
            || ContainsRewardId(DB.GameSchedule.DailyChallengeHasRewardId, stageId))
        {
            return false;
        }

        int coinReward = stageId == FirstStageId
            ? ConfigManager.RemoteConfig.GetDailyChallengeStep1CoinReward()
            : ConfigManager.RemoteConfig.GetDailyChallengeStep2CoinReward();

        var rewards = new Dictionary<GameProps, object>();
        if (coinReward > 0)
            rewards[GameProps.Coin] = coinReward;

        Config.Skin skinReward = null;
        int randomSkinPoolId = stageId == SecondStageId
            ? ConfigManager.RemoteConfig.GetDailyChallengeRandomSkinPoolId()
            : 0;
        bool hasSkinReward = stageId == SecondStageId
                             && randomSkinPoolId > 0
                             && SkinUtils.TryGetRandomUnownedRewardSkin(
                                 randomSkinPoolId.ToString(),
                                 out skinReward);
        if (hasSkinReward)
            rewards[GameProps.Skin] = skinReward.Id;

        if (rewards.Count == 0)
        {
            this.LogWarning($"[DailyChallengeManager] Stage reward is empty. stageId:{stageId}");
            return false;
        }

        rewardData = new DailyChallengeRewardData(
            stageId,
            rewards,
            ConfigManager.RemoteConfig.GetDailyChallengeRewardMultiple(),
            hasSkinReward);
        return true;
    }

    /// <summary>
    /// 用途：DailyChallengePopup 打开时同步选择并发放指定阶段的普通奖励，同时写入阶段领取状态。
    /// 调用边界：仅供 DailyChallengePopup 初始化本次待领奖数据时调用；动画结束后的展示弹窗不得再次调用。
    /// 注意事项：成功返回的 rewardData 已完成 normal 奖励入包，后续 CommonGetRewardPopup 必须传 grantNormalReward:false。
    /// </summary>
    /// <param name="stageId">当前待领取阶段 id。</param>
    /// <param name="rewardData">成功时返回已经发放、可直接用于展示和广告额外奖励的数据。</param>
    /// <returns>普通奖励已写入背包且阶段领取状态已进入当前内存时返回 true。</returns>
    public bool TryClaimRewardOnPopupOpen(int stageId, out DailyChallengeRewardData rewardData)
    {
        rewardData = null;
        if (!TryCreateRewardData(stageId, out DailyChallengeRewardData createdRewardData)
            || !TryGrantNormalReward(createdRewardData))
        {
            return false;
        }

        TryMarkRewardClaimed(stageId);
        if (!ContainsRewardId(DB.GameSchedule?.DailyChallengeHasRewardId, stageId))
        {
            this.LogError(
                $"[DailyChallengeManager] Reward was granted but claimed state was not recorded. stageId:{stageId}");
            return false;
        }

        rewardData = createdRewardData;
        return true;
    }

    /// <summary>
    /// 标记阶段奖励已由 DailyChallengeManager 完成普通发放。
    /// 调用边界：只能在 TryClaimRewardOnPopupOpen 完成普通奖励入包后调用。
    /// 注意事项：保存失败时保留当前会话内的领奖 id，降低同一会话重复发奖风险，并记录警告。
    /// </summary>
    public bool TryMarkRewardClaimed(int stageId)
    {
        if (!Application.isPlaying || DB.GameSchedule == null)
            return false;

        List<int> claimedRewardIds = DB.GameSchedule.DailyChallengeHasRewardId;
        if (ContainsRewardId(claimedRewardIds, stageId))
            return true;

        if (!TryGetStageTargets(out int firstStageTarget, out int secondStageTarget)
            || !IsStageReached(stageId, firstStageTarget, secondStageTarget))
        {
            return false;
        }

        claimedRewardIds.Add(stageId);
        bool saved = DB.SaveGameSchedule();
        if (!saved)
            this.LogWarning($"[DailyChallengeManager] Reward claimed state save failed. stageId:{stageId}");

        return saved;
    }

    /// <summary>
    /// 每日调度触发时清空当日进度、已领取奖励和成功通关关卡。
    /// 调用边界：只能由 DailyScheduleModule 调用，不应由 UI 或关卡直接重置。
    /// 注意事项：重置后会立即保存 GameSchedule；存档不可用时安全跳过。
    /// </summary>
    public void OnScheduleRefresh()
    {
        if (!Application.isPlaying || DB.GameSchedule == null)
            return;

        DB.GameSchedule.DailyChallengeCurrentProcess = 0;
        DB.GameSchedule.DailyChallengeHasRewardId.Clear();
        DB.GameSchedule.DailyChallengePassedLevel.Clear();

        if (!DB.SaveGameSchedule())
            this.LogWarning("[DailyChallengeManager] Daily reset save failed.");
    }

    /// <summary>
    /// 释放 DailyScheduleModule 对当前管理器的引用。
    /// 调用边界：仅由 GameManager 销毁流程调用。
    /// 注意事项：不会修改每日挑战存档。
    /// </summary>
    public void Dispose()
    {
        ScheduleSystem.Instance.GetModule<DailyScheduleModule>()?.Unregister(this);
    }

    #endregion

    #region Internal Helpers

    private static bool TryGetRuntimeContext(
        out int firstStageTarget,
        out int secondStageTarget)
    {
        firstStageTarget = 0;
        secondStageTarget = 0;

        return Application.isPlaying
               && ConfigManager.RemoteConfig.GetDailyChallengeIsOpen()
               && TryGetStageTargets(out firstStageTarget, out secondStageTarget)
               && DB.GameSchedule != null
               && DB.GameData?.HasUnlockDailyChallenge == true;
    }

    private bool TryGrantNormalReward(DailyChallengeRewardData rewardData)
    {
        if (rewardData?.Rewards == null || rewardData.Rewards.Count == 0)
            return false;

        var bag = GameManager.Instance?.Bag;
        if (bag == null || DB.GameData == null)
        {
            this.LogWarning(
                $"[DailyChallengeManager] Normal reward grant skipped because Bag or GameData is unavailable. stageId:{rewardData.StageId}");
            return false;
        }

        var quantityRewards = new Dictionary<GameProps, int>();
        string skinId = null;
        foreach (KeyValuePair<GameProps, object> reward in rewardData.Rewards)
        {
            switch (reward.Key)
            {
                case GameProps.Coin:
                case GameProps.Star:
                    if (!BaseParamUtils.TryGetInt(reward.Value, out int count) || count <= 0)
                    {
                        this.LogWarning(
                            $"[DailyChallengeManager] Quantity reward is invalid. stageId:{rewardData.StageId}, prop:{reward.Key}");
                        return false;
                    }

                    quantityRewards[reward.Key] = count;
                    break;
                case GameProps.Skin:
                    if (!BaseParamUtils.TryGetString(reward.Value, out skinId)
                        || string.IsNullOrEmpty(skinId))
                    {
                        this.LogWarning(
                            $"[DailyChallengeManager] Skin reward is invalid. stageId:{rewardData.StageId}");
                        return false;
                    }

                    break;
                default:
                    this.LogWarning(
                        $"[DailyChallengeManager] Unsupported normal reward. stageId:{rewardData.StageId}, prop:{reward.Key}");
                    return false;
            }
        }

        if (!string.IsNullOrEmpty(skinId)
            && !bag.GetNewSkin(skinId, GetSkinWayType.Reward))
        {
            this.LogWarning(
                $"[DailyChallengeManager] Skin reward grant failed. stageId:{rewardData.StageId}, skinId:{skinId}");
            return false;
        }

        bag.GrantRewards(quantityRewards, RewardType.Other);

        return true;
    }

    private static DailyChallengeViewState CreateViewState(int firstStageTarget, int secondStageTarget)
    {
        int finalTarget = firstStageTarget + secondStageTarget;
        int totalProgress = Mathf.Clamp(DB.GameSchedule.DailyChallengeCurrentProcess, 0, finalTarget);
        bool isFirstStageReached = totalProgress >= firstStageTarget;
        bool isSecondStageReached = totalProgress >= finalTarget;

        int currentStageId;
        int currentStageProgress;
        int currentStageTarget;
        if (!isFirstStageReached)
        {
            currentStageId = FirstStageId;
            currentStageProgress = totalProgress;
            currentStageTarget = firstStageTarget;
        }
        else
        {
            currentStageId = SecondStageId;
            currentStageProgress = Mathf.Clamp(totalProgress - firstStageTarget, 0, secondStageTarget);
            currentStageTarget = secondStageTarget;
        }

        float currentStageNormalizedProgress = GetNormalizedProgress(currentStageProgress, currentStageTarget);
        IReadOnlyList<int> claimedRewardIds = DB.GameSchedule.DailyChallengeHasRewardId;
        return new DailyChallengeViewState(
            true,
            totalProgress,
            currentStageId,
            currentStageProgress,
            currentStageTarget,
            firstStageTarget,
            finalTarget,
            isFirstStageReached,
            isSecondStageReached,
            ContainsRewardId(claimedRewardIds, FirstStageId),
            ContainsRewardId(claimedRewardIds, SecondStageId),
            currentStageNormalizedProgress,
            GetSegmentedNormalizedProgress(currentStageId, currentStageNormalizedProgress));
    }

    private static List<int> GetPendingRewardStageIds(DailyChallengeViewState state)
    {
        var result = new List<int>(StageCount);
        if (state == null)
            return result;

        if (state.IsFirstStageReached && !state.IsFirstRewardClaimed)
            result.Add(FirstStageId);

        if (state.IsSecondStageReached && !state.IsSecondRewardClaimed)
            result.Add(SecondStageId);

        return result;
    }

    private static bool IsStageReached(int stageId, int firstStageTarget, int secondStageTarget)
    {
        int progress = DB.GameSchedule?.DailyChallengeCurrentProcess ?? 0;
        return stageId switch
        {
            FirstStageId => progress >= firstStageTarget,
            SecondStageId => progress >= firstStageTarget + secondStageTarget,
            _ => false,
        };
    }

    private static bool TryGetStageTargets(out int firstStageTarget, out int secondStageTarget)
    {
        firstStageTarget = 0;
        secondStageTarget = 0;

        int[] steps = ConfigManager.RemoteConfig.GetDailyChallengeSteps();
        if (steps == null || steps.Length < StageCount || steps[0] <= 0 || steps[1] <= steps[0])
            return false;

        firstStageTarget = steps[0];
        secondStageTarget = steps[1] - steps[0];
        return secondStageTarget > 0;
    }

    private static bool ContainsRewardId(IReadOnlyList<int> rewardIds, int rewardId)
    {
        if (rewardIds == null)
            return false;

        for (int i = 0; i < rewardIds.Count; i++)
        {
            if (rewardIds[i] == rewardId)
                return true;
        }

        return false;
    }

    private static float GetNormalizedProgress(int progress, int target)
    {
        return target > 0 ? Mathf.Clamp01((float)progress / target) : 0f;
    }

    private static float GetSegmentedNormalizedProgress(
        int currentStageId,
        float currentStageNormalizedProgress)
    {
        float completedStageProgress = currentStageId == SecondStageId
            ? StageNormalizedSpan
            : 0f;
        return Mathf.Clamp01(
            completedStageProgress + currentStageNormalizedProgress * StageNormalizedSpan);
    }

    private static DailyChallengeViewState CreateHiddenState()
    {
        return new DailyChallengeViewState(
            false,
            0,
            FirstStageId,
            0,
            0,
            0,
            0,
            false,
            false,
            false,
            false,
            0f,
            0f);
    }

    #endregion
}

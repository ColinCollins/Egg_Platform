using System;
using Bear.Logger;

/// <summary>
/// IWanner 小关运行统计，统一维护尝试次数、成功次数、重试次数和历史累计时长。
/// </summary>
internal sealed class IWannerLevelRuntimeStatistics : IDebuger
{
    #region Runtime State

    private string _activeLevelDataId;
    private string _activeLevelPath;
    private string _levelContextPath;
    private float _attemptDurationSeconds;
    private int _levelReviveCount;
    private bool _isAttemptActive;

    #endregion

    #region Attempt Lifecycle

    /// <summary>
    /// 用途：开始一次可玩的 IWanner 小关尝试，并增加该 Path 的历史游玩次数。
    /// 调用边界：只能在关卡资源和运行时初始化完成、即将开放输入前调用一次。
    /// 注意事项：levelDataId 仅用于成功派生事件；TBN 可传空。若上一次尝试异常未结束，会先按退出口径保存其时长。
    /// </summary>
    public void BeginAttempt(string levelDataId, string levelPath)
    {
        if (_isAttemptActive)
        {
            this.LogWarning(
                $"[IWannerLevelRuntimeStatistics] Previous attempt was still active. levelPath:{_activeLevelPath}");
            CompleteAttempt(isSuccess: false, isRetry: false);
        }

        if (!string.Equals(_levelContextPath, levelPath, StringComparison.Ordinal))
        {
            _levelContextPath = levelPath;
            _levelReviveCount = 0;
        }

        _activeLevelDataId = levelDataId;
        _activeLevelPath = levelPath;
        _attemptDurationSeconds = 0f;
        _isAttemptActive = !string.IsNullOrEmpty(levelPath);
        if (!_isAttemptActive)
        {
            this.LogWarning(
                "[IWannerLevelRuntimeStatistics] Cannot begin an attempt because level Path is empty.");
            return;
        }

        var saveData = DB.IWannerGame;
        int playCount = 0;
        int startType = GameSDKService.LevelStartTypeNewGame;
        if (saveData == null)
        {
            this.LogWarning(
                $"[IWannerLevelRuntimeStatistics] Cannot record play count. levelPath:{levelPath}, save data is unavailable.");
        }
        else
        {
            saveData.EnsureValidCollections();
            saveData.LevelSuccessCount.TryGetValue(levelPath, out int successCount);
            startType = successCount > 0
                ? GameSDKService.LevelStartTypeReplay
                : GameSDKService.LevelStartTypeNewGame;
            saveData.LevelPlayCount.TryGetValue(levelPath, out playCount);
            saveData.LevelPlayCount[levelPath] = ++playCount;
            SaveWithWarning(
                "begin attempt",
                $"levelPath:{levelPath}, playCount:{playCount}");
        }

        GameSDKService sdk = GameSDKService.Instance;
        if (sdk == null)
        {
            this.LogWarning(
                $"[IWannerLevelRuntimeStatistics] level_start skipped because GameSDKService is unavailable. levelPath:{levelPath}");
            return;
        }

        sdk.IWannerLevelStartEvent(levelPath, playCount, startType);
    }

    /// <summary>
    /// 用途：累计当前尝试的有效游玩时长。
    /// 调用边界：只由活动 IWanner 控制器在未暂停、未加载、未结算时逐帧调用。
    /// 注意事项：传入非正数或当前没有活动尝试时不会修改数据。
    /// </summary>
    public void Tick(float deltaSeconds)
    {
        if (_isAttemptActive && deltaSeconds > 0f)
            _attemptDurationSeconds += deltaSeconds;
    }

    /// <summary>
    /// 用途：结束当前尝试，并按结果一次性写入成功、重试和历史累计时长。
    /// 调用边界：Reset、Fail、成功、Skip 成功或退出时调用；同一尝试重复调用不会重复计数。
    /// 注意事项：成功与重试互不隐式推导，由调用方按真实业务结果显式传入。
    /// </summary>
    public void CompleteAttempt(bool isSuccess, bool isRetry)
    {
        if (!_isAttemptActive)
            return;

        string levelDataId = _activeLevelDataId;
        string levelPath = _activeLevelPath;
        long durationMilliseconds = Math.Max(
            0L,
            (long)(_attemptDurationSeconds * 1000f));
        ClearActiveAttempt();

        if (isRetry)
            _levelReviveCount++;

        var saveData = DB.IWannerGame;
        long totalDuration = durationMilliseconds;
        if (saveData == null || string.IsNullOrEmpty(levelPath))
        {
            this.LogWarning(
                $"[IWannerLevelRuntimeStatistics] Cannot complete attempt. levelPath:{levelPath}, saveAvailable:{saveData != null}");
        }
        else
        {
            saveData.EnsureValidCollections();
            if (isSuccess)
            {
                saveData.LevelSuccessCount.TryGetValue(levelPath, out int successCount);
                saveData.LevelSuccessCount[levelPath] = successCount + 1;
            }

            if (isRetry)
            {
                saveData.LevelReviveCount.TryGetValue(levelPath, out int reviveCount);
                saveData.LevelReviveCount[levelPath] = reviveCount + 1;
            }

            saveData.LevelPlayDuration.TryGetValue(levelPath, out long historicalDuration);
            totalDuration = historicalDuration + durationMilliseconds;
            saveData.LevelPlayDuration[levelPath] = totalDuration;
            SaveWithWarning(
                "complete attempt",
                $"levelPath:{levelPath}, duration:{durationMilliseconds}, success:{isSuccess}, retry:{isRetry}");
        }

        int levelEndType = isSuccess
            ? GameSDKService.LevelEndTypeSuccess
            : isRetry
                ? GameSDKService.LevelEndTypeFailed
                : GameSDKService.LevelEndTypeExit;
        GameSDKService sdk = GameSDKService.Instance;
        if (sdk == null)
        {
            this.LogWarning(
                $"[IWannerLevelRuntimeStatistics] level_end skipped because GameSDKService is unavailable. levelPath:{levelPath}, type:{levelEndType}");
        }
        else
        {
            sdk.IWannerLevelEndEvent(
                levelDataId,
                levelPath,
                durationMilliseconds,
                totalDuration,
                _levelReviveCount,
                levelEndType);
        }

        if (isSuccess || !isRetry)
            ResetLevelContext();
    }

    /// <summary>
    /// 用途：丢弃当前统计上下文且不写入存档。
    /// 调用边界：仅供 GM 无统计切关和控制器最终清理使用。
    /// 注意事项：正式玩法退出必须先调用 CompleteAttempt 保存时长，不能直接丢弃。
    /// </summary>
    public void DiscardAttempt()
    {
        ClearActiveAttempt();
        ResetLevelContext();
    }

    private void ClearActiveAttempt()
    {
        _activeLevelDataId = null;
        _activeLevelPath = null;
        _attemptDurationSeconds = 0f;
        _isAttemptActive = false;
    }

    private void ResetLevelContext()
    {
        _levelContextPath = null;
        _levelReviveCount = 0;
    }

    #endregion

    #region Save

    private void SaveWithWarning(string operation, string context)
    {
        if (!DB.SaveIWannerGame())
        {
            this.LogWarning(
                $"[IWannerLevelRuntimeStatistics] Failed to save {operation}. {context}");
        }
    }

    #endregion
}

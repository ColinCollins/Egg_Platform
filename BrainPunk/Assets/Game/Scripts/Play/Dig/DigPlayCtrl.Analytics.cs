using Bear.Logger;
using Bear.SaveModule;
using Config;

public sealed partial class DigPlayCtrl
{
    #region 进入时固定的统计上下文
    private string _analyticsLevelDataId, _analyticsLevelPath;
    private bool _isAnalyticsAttemptActive;
    private float _analyticsCurrentLevelTimeSeconds;
    private int _analyticsReviveCount;
    private bool TryGetCurrentLevelData(out DigLevelData data) { data = _data; return data != null; }
    #endregion
    #region Level Analytics

    private void StartNewLevelAnalytics()
    {
        if (_isAnalyticsAttemptActive)
            ReportLevelEndAnalytics(GameSDKService.LevelEndTypeExit);

        ResetLevelAnalyticsContext();
        if (!TryGetCurrentLevelData(out DigLevelData currentLevelData) ||
            string.IsNullOrEmpty(currentLevelData.Id))
        {
            this.LogWarning(
                $"[DigPlayCtrl] Dig level_start skipped because DigLevelData is missing. sortId:{_levelId}");
            return;
        }

        _analyticsLevelDataId = currentLevelData.Id;
        _analyticsLevelPath = currentLevelData.Path;
        _isAnalyticsAttemptActive = true;

        DigGame saveData = DB.DigGame;
        int playCount = 0;
        if (saveData != null)
        {
            saveData.EnsureValidCollections();
            saveData.LevelPlayCount.TryGetValue(_analyticsLevelDataId, out playCount);
            saveData.LevelPlayCount[_analyticsLevelDataId] = ++playCount;
            SaveDigAnalytics("level start");
        }
        else
        {
            this.LogWarning("[DigPlayCtrl] DigGame save data is missing during level_start.");
        }

        GameSDKService.Instance.DigLevelStartEvent(
            _analyticsLevelPath,
            playCount,
            GetCurrentLevelStartType(saveData));
    }

    private void StartResetAnalytics()
    {
        if (string.IsNullOrEmpty(_analyticsLevelDataId))
        {
            this.LogWarning(
                $"[DigPlayCtrl] Dig reset level_start skipped because analytics context is missing. sortId:{_levelId}");
            return;
        }

        _analyticsCurrentLevelTimeSeconds = 0f;
        _isAnalyticsAttemptActive = true;

        DigGame saveData = DB.DigGame;
        int playCount = 0;
        if (saveData != null)
        {
            saveData.EnsureValidCollections();
            saveData.LevelPlayCount.TryGetValue(_analyticsLevelDataId, out playCount);
            saveData.LevelPlayCount[_analyticsLevelDataId] = ++playCount;
            SaveDigAnalytics("level reset");
        }

        GameSDKService.Instance.DigLevelStartEvent(
            _analyticsLevelPath,
            playCount,
            GetCurrentLevelStartType(saveData));
    }

    private void StartReplayAnalytics()
    {
        if (string.IsNullOrEmpty(_analyticsLevelDataId))
        {
            this.LogWarning(
                $"[DigPlayCtrl] Dig replay level_start skipped because analytics context is missing. sortId:{_levelId}");
            return;
        }

        _analyticsCurrentLevelTimeSeconds = 0f;
        _analyticsReviveCount = 0;
        _isAnalyticsAttemptActive = true;

        DigGame saveData = DB.DigGame;
        int playCount = 0;
        if (saveData != null)
        {
            saveData.EnsureValidCollections();
            saveData.LevelPlayCount.TryGetValue(_analyticsLevelDataId, out playCount);
            saveData.LevelPlayCount[_analyticsLevelDataId] = ++playCount;
            SaveDigAnalytics("level replay");
        }

        GameSDKService.Instance.DigLevelStartEvent(
            _analyticsLevelPath,
            playCount,
            GetCurrentLevelStartType(saveData));
    }

    private int GetCurrentLevelStartType(DigGame saveData)
    {
        return saveData?.PassedLevels?.Contains(_levelId) == true
            ? GameSDKService.LevelStartTypeReplay
            : GameSDKService.LevelStartTypeNewGame;
    }

    private void TickLevelAnalytics(float deltaSeconds)
    {
        if (!_isAnalyticsAttemptActive ||
            _runtime == null ||
            _resolved ||
            deltaSeconds <= 0f)
        {
            return;
        }

        _analyticsCurrentLevelTimeSeconds += deltaSeconds;
    }

    private void RecordLevelRetryAnalytics()
    {
        _analyticsReviveCount++;

        DigGame saveData = DB.DigGame;
        if (saveData == null || string.IsNullOrEmpty(_analyticsLevelDataId))
            return;

        saveData.EnsureValidCollections();
        saveData.LevelReviveCount.TryGetValue(_analyticsLevelDataId, out int reviveCount);
        saveData.LevelReviveCount[_analyticsLevelDataId] = reviveCount + 1;
        SaveDigAnalytics("level retry");
    }

    private void RecordLevelSuccessAnalytics()
    {
        DigGame saveData = DB.DigGame;
        if (saveData == null || string.IsNullOrEmpty(_analyticsLevelDataId))
            return;

        saveData.EnsureValidCollections();
        saveData.LevelSuccessCount.TryGetValue(_analyticsLevelDataId, out int successCount);
        saveData.LevelSuccessCount[_analyticsLevelDataId] = successCount + 1;
        SaveDigAnalytics("level success");
    }

    private void ReportLevelEndAnalytics(int levelEndType)
    {
        if (!_isAnalyticsAttemptActive || string.IsNullOrEmpty(_analyticsLevelDataId))
            return;

        long levelDuration = ToAnalyticsMilliseconds(_analyticsCurrentLevelTimeSeconds);
        long totalDuration = RecordLevelDurationAnalytics(levelDuration);
        int reviveCount = _analyticsReviveCount;

        GameSDKService.Instance.DigLevelEndEvent(
            _analyticsLevelDataId,
            _analyticsLevelPath,
            levelDuration,
            totalDuration,
            reviveCount,
            levelEndType);
        _isAnalyticsAttemptActive = false;
    }

    private long RecordLevelDurationAnalytics(long levelDuration)
    {
        DigGame saveData = DB.DigGame;
        if (saveData == null || string.IsNullOrEmpty(_analyticsLevelPath))
        {
            this.LogWarning(
                $"[DigPlayCtrl] Dig level duration save skipped because analytics context is missing. levelDataId:{_analyticsLevelDataId}");
            return levelDuration;
        }

        saveData.EnsureValidCollections();
        saveData.LevelPlayDuration.TryAdd(_analyticsLevelPath, 0L);
        saveData.LevelPlayDuration[_analyticsLevelPath] += levelDuration;
        SaveDigAnalytics("level duration");
        return saveData.LevelPlayDuration[_analyticsLevelPath];
    }

    private void ResetCompletedLevelAnalyticsCounters()
    {
        _analyticsCurrentLevelTimeSeconds = 0f;
        _analyticsReviveCount = 0;
    }

    private void ResetLevelAnalyticsContext()
    {
        _analyticsLevelDataId = null;
        _analyticsLevelPath = null;
        _analyticsCurrentLevelTimeSeconds = 0f;
        _analyticsReviveCount = 0;
        _isAnalyticsAttemptActive = false;
    }

    private void SaveDigAnalytics(string operation)
    {
        if (!DB.SaveDigGame())
        {
            this.LogWarning(
                $"[DigPlayCtrl] Failed to save Dig analytics. operation:{operation}, levelDataId:{_analyticsLevelDataId}");
        }
    }

    private static long ToAnalyticsMilliseconds(float seconds)
    {
        return seconds <= 0f ? 0L : (long)(seconds * 1000f);
    }

    #endregion
}

using System.Collections.Generic;

public partial class GameSDKService
{
    #region Dig 关卡统计
    private const string DigLevelItemCategory = "dig";
    /// <summary>
    /// 上报 Dig 关卡启动事件。
    /// 调用边界：仅由 DigPlayCtrl 在新关卡进入、Reset 或 VictoryPanel Replay 已确认后调用。
    /// 注意事项：level 使用主线、DrawGame、IWannerGame 与 DigGame 的累计通关次数生成；start_type 由当前关卡是否已通关决定，不修改主线关卡的 _curLevelId。
    /// </summary>
    /// <param name="levelPath">DigLevelData.Path。</param>
    /// <param name="playCount">当前关卡累计进入与重试次数。</param>
    /// <param name="levelStartType">关卡启动类型。</param>
    public void DigLevelStartEvent(
        string levelPath,
        int playCount,
        int levelStartType = 0)
    {
        RecordLastStartedLevelPath(levelPath);
        SetScreen(ScreenGameView);

        int level = GetCombinedLevelPassedCount() + 1;
        var data = new Dictionary<string, object>()
        {
            ["level"] = level,
            ["level_name"] = levelPath,
            ["item_category"] = DigLevelItemCategory,
            ["play_count"] = playCount
        };

        var startType = GetLevelStartType(levelStartType);
        if (!string.IsNullOrEmpty(startType))
            data["start_type"] = startType;

        LogEvent("level_start", data);
    }

    /// <summary>
    /// 上报 Dig 关卡结束事件。
    /// 调用边界：仅由 DigPlayCtrl 在 Reset、成功结算或离开当前玩法时调用。
    /// 注意事项：level 使用主线、DrawGame、IWannerGame 与 DigGame 的累计通关次数生成；成功时继续发送 level_end_success_N 买量事件。
    /// </summary>
    /// <param name="levelDataId">当前 DigLevelData.Id；仅成功时用作 level_end_success_N 的实际关卡 ID。</param>
    /// <param name="levelPath">DigLevelData.Path。</param>
    /// <param name="duration">当前进入周期内该关累计游玩时长，单位毫秒。</param>
    /// <param name="totalDuration">该关历史累计游玩时长，单位毫秒。</param>
    /// <param name="reviveCount">当前关卡运行周期内的主动重开与失败次数之和。</param>
    /// <param name="levelEndType">关卡结束类型。</param>
    public void DigLevelEndEvent(
        string levelDataId,
        string levelPath,
        long duration,
        long totalDuration,
        int reviveCount,
        int levelEndType = 0)
    {
        int combinedPassedCount = GetCombinedLevelPassedCount();
        int level = levelEndType == LevelEndTypeSuccess
            ? combinedPassedCount
            : combinedPassedCount + 1;
        var data = new Dictionary<string, object>()
        {
            ["level"] = level,
            ["level_name"] = levelPath,
            ["item_category"] = DigLevelItemCategory,
            ["duration"] = duration,
            ["level_duration"] = duration,
            ["revive"] = reviveCount,
            ["total_duration"] = totalDuration
        };

        var levelResult = GetLevelResult(levelEndType);
        if (!string.IsNullOrEmpty(levelResult))
            data["result"] = levelResult;

        LogEvent("level_end", data);

        if (levelEndType == LevelEndTypeSuccess)
        {
            UpdateBPlay();

            if (int.TryParse(levelDataId, out int digLevelId) && digLevelId > 0)
            {
                LevelEndSuccessEvent(combinedPassedCount, digLevelId);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    $"Dig level_end_success skipped because DigLevelData.Id is invalid: {levelDataId}");
            }
        }
    }

    #endregion
}

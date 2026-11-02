using Bear.Logger;

/// <summary>
/// 记录当前关卡的一些运行时状态：
/// - 当前关卡 Id
/// - 当前关卡游玩总时长（所有尝试累计的秒数）
/// - 当前关卡本局游玩时长（当前尝试内的秒数）
/// - 当前关卡的重置次数
/// - 当前关卡的失败次数
/// 
/// 负责运行时数据存储和简单累加；历史统计方法仍会写入 DB，由外部（如 PlayCtrl）控制调用时机。
/// </summary>
public class LevelRuntimeState : IDebuger
{
    #region Runtime State

    /// <summary>当前关卡 Id。</summary>
    public int LevelId { get; private set; }

    /// <summary>当前关卡 name。</summary>
    public string LevelPath { get; private set; }

    /// <summary>当前关卡游玩总时长（单位：秒）；历史字段，目前与 CurrentLevelTimeSeconds 同步累计。</summary>
    public float TotalPlayTimeSeconds { get; private set; }

    /// <summary>当前尝试游玩时长（单位：秒），重开、失败重试或 replay 时重置为 0。</summary>
    public float CurrentAttemptTimeSeconds { get; private set; }

    /// <summary>当前关卡累计游玩时长（单位：秒），同一关卡 replay 时不重置。</summary>
    public float CurrentLevelTimeSeconds { get; private set; }

    /// <summary>当前关卡的重置次数（主动重开次数）。</summary>
    public int ResetCount { get; private set; }

    /// <summary>当前关卡的失败次数（不含主动重开，视业务需要外部调用）。</summary>
    public int FailCount { get; private set; }

    /// <summary>
    /// 当前关卡会话是否已经获得提示；同关 Reset/失败重试保留，Replay、离关或重新进入关卡时清理。
    /// </summary>
    public bool HasTips { get; private set; }

    /// <summary>
    /// 当前提示入口气泡是否正在展示；展示期间用于阻止重复派发显示事件。
    /// </summary>
    public bool HasClickTips { get; private set; }

    /// <summary>当前尝试是否已经展示过提示入口气泡。</summary>
    public bool HasShownTipsWarning { get; private set; }

    /// <summary>当前尝试内，上一次提示入口气泡完全消失时的累计秒数。</summary>
    public float TipsWarningHiddenAtSeconds { get; private set; }

    /// <summary>
    /// 当前尝试是否已经弹出过关卡提示气泡。
    /// </summary>
    public bool HasShownGuideBubble { get; private set; }

    /// <summary>
    /// 当前是否处于重开、失败重试或胜利后重播的关卡尝试切换阶段。
    /// 切换期间旧场景仍可能短暂执行 Playing 更新，因此提示气泡等依赖新场景的逻辑必须暂停。
    /// </summary>
    public bool IsAttemptTransitioning { get; private set; }

    #endregion

    #region Level And Attempt Lifecycle

    /// <summary>
    /// 切换到一个新的关卡时调用，重置所有统计数据。
    /// 新关卡在重新进入 Playing 前保持尝试切换状态，避免旧场景触发依赖新场景的提示逻辑。
    /// </summary>
    public void StartLevel(int levelId, string levelPath)
    {
        LevelId = levelId;
        LevelPath = levelPath;

        TotalPlayTimeSeconds = 0f;
        CurrentAttemptTimeSeconds = 0f;
        CurrentLevelTimeSeconds = 0f;
        ResetCount = 0;
        FailCount = 0;

        HasTips = false;
        HasClickTips = false;
        HasShownTipsWarning = false;
        TipsWarningHiddenAtSeconds = 0f;
        HasShownGuideBubble = false;

        IsAttemptTransitioning = true;

        TryToAddPlayCount();
    }

    /// <summary>
    /// 用途：标记当前关卡尝试开始切换，阻止旧场景在异步重建期间再次触发提示气泡。
    /// 调用边界：仅在普通重开、失败重试或胜利后重播已经确定、场景开始重建前调用。
    /// 注意事项：本方法不重置计时、不增加重试次数，也不写入存档。
    /// </summary>
    public void BeginAttemptTransition()
    {
        IsAttemptTransitioning = true;
    }

    /// <summary>
    /// 用途：标记当前关卡尝试切换完成，允许新场景继续执行依赖场景就绪的提示逻辑。
    /// 调用边界：仅在 PlayCtrl 已重新进入 Playing 状态后调用；暂停后恢复也可安全重复调用。
    /// 注意事项：本方法不重置尝试计时，避免暂停恢复时丢失本局累计时间。
    /// </summary>
    public void CompleteAttemptTransition()
    {
        IsAttemptTransitioning = false;
    }

    /// <summary>
    /// 开始一局新的尝试前调用，重置当前尝试时间和本次提示气泡显示状态。
    /// </summary>
    public void ResetAttemptPlayTime()
    {
        CurrentAttemptTimeSeconds = 0f;
        HasClickTips = false;
        HasShownTipsWarning = false;
        TipsWarningHiddenAtSeconds = 0f;
        HasShownGuideBubble = false;
    }

    /// <summary>
    /// 每帧或固定时间间隔调用，用于累加当前关卡的总时长和本局时长。
    /// deltaSeconds 通常为 Time.deltaTime 或 Time.unscaledDeltaTime。
    /// </summary>
    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
            return;

        TotalPlayTimeSeconds += deltaSeconds;
        CurrentAttemptTimeSeconds += deltaSeconds;
        CurrentLevelTimeSeconds += deltaSeconds;
    }

    #endregion

    #region Statistics And Hint State


    /// <summary>
    /// 对应关卡游戏时长
    /// </summary>
    /// <param name="duration"></param>
    private void TryToAddPlayDuration(long duration)
    {
        DB.GameData.LevelPlayDuration.TryAdd(LevelPath, 0);
        DB.GameData.LevelPlayDuration[LevelPath] += duration;
        DB.GameData.Save();
    }

    /// <summary>
    /// 把当前尝试时长记录到当前关卡的持久化累计时长中。
    /// </summary>
    public void RecordLevelDuration()
    {
        TryToAddPlayDuration(GetCurrentAttemptTimeSeconds());
    }

    public long GetCurrentAttemptTimeSeconds()
    {
        // 历史命名保留不变，返回值单位为毫秒。
        if (CurrentAttemptTimeSeconds <= 0f)
            return 0L;

        return (long)(CurrentAttemptTimeSeconds * 1000f);
    }

    public long GetCurrentLevelTimeSeconds()
    {
        // 历史命名保留不变，返回值单位为毫秒。
        if (CurrentLevelTimeSeconds <= 0f)
            return 0L;

        return (long)(CurrentLevelTimeSeconds * 1000f);
    }


    /// <summary>
    /// 记录一次主动重开当前关卡。
    /// </summary>
    public void RecordReset()
    {
        ResetCount++;

        TryToAddReviveCount();
    }

    /// <summary>
    /// 记录一次当前关卡的失败（不含主动重开，是否包括由外部业务决定）。
    /// </summary>
    public void RecordFail()
    {
        FailCount++;

        TryToAddReviveCount();
    }

    public void SwitchTips(bool has)
    {
        HasTips = has;
    }

    public void SwitchClickTips(bool hasClick)
    {
        if (HasClickTips == hasClick)
            return;

        HasClickTips = hasClick;
        if (hasClick)
        {
            HasShownTipsWarning = true;
            return;
        }

        if (HasShownTipsWarning)
            TipsWarningHiddenAtSeconds = CurrentAttemptTimeSeconds;
    }

    public void SwitchGuideBubble(bool hasShown)
    {
        HasShownGuideBubble = hasShown;
    }

    /// <summary>
    /// 重置当前关卡的统计与尝试级提示显示状态，但保留 LevelId 和当前关卡会话提示资格。
    /// </summary>
    public void ResetData()
    {
        ResetDataPreservingRevive();
        ResetCount = 0;
        FailCount = 0;
    }

    /// <summary>
    /// 重置当前尝试的计时与提示显示状态，同时保留当前关提示资格及累计的主动重开与失败次数。
    /// 调用边界：仅供 Reset 在失败结束打点完成、开启下一次尝试前调用；成功结算与 Replay 继续使用 ResetData。
    /// </summary>
    public void ResetDataPreservingRevive()
    {
        TotalPlayTimeSeconds = 0f;
        CurrentLevelTimeSeconds = 0;
        CurrentAttemptTimeSeconds = 0f;

        HasClickTips = false;
        HasShownTipsWarning = false;
        TipsWarningHiddenAtSeconds = 0f;
        HasShownGuideBubble = false;
        this.Log("Reset state data");
    }

    /// <summary>
    /// 记录当前关卡进入次数。
    /// </summary>
    public void TryToAddPlayCount()
    {
        DB.GameData.LevelPlayCount.TryAdd(LevelPath, 0);
        DB.GameData.LevelPlayCount[LevelPath]++;
        DB.GameData.Save();
    }

    /// <summary>
    /// 记录当前关卡成功次数，并同步增加插屏关卡胜利计数。
    /// </summary>
    public void TryToAddSuccessCount()
    {
        DB.GameData.LevelSuccessCount.TryAdd(LevelPath, 0);
        DB.GameData.LevelSuccessCount[LevelPath]++;

        // 插屏关卡胜利次数记录
        DB.GameData.InterstitialLevelSuccessCount++;

        DB.GameData.Save();
    }

    /// <summary>
    /// 记录当前关卡重开或失败重试次数。
    /// </summary>
    private void TryToAddReviveCount()
    {
        DB.GameData.LevelReviveCount.TryAdd(LevelPath, 0);
        DB.GameData.LevelReviveCount[LevelPath]++;
        DB.GameData.Save();
    }

    #endregion
}


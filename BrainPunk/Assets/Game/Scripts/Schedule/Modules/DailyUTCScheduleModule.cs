using System;

/// <summary>
/// UTC 每日调度模块：按 UTC 00:00 判断是否需要刷新。
/// UTC 业务必须显式注册到本模块，不会执行 DailyScheduleModule 的当地自然日业务。
/// 刷新时间使用独立的 DailyUTC Key 保存，避免与当地 Daily 日界线互相覆盖。
/// </summary>
public sealed class DailyUTCScheduleModule : ScheduleModuleBase
{
    #region ScheduleModuleBase

    /// <summary>
    /// UTC 每日调度的独立存档 Key。
    /// </summary>
    public override string ModuleKey => "DailyUTC";

    protected override bool ShouldRefresh()
    {
        long today = GetTodayStartTimestamp();
        long lastDate = DB.GameSchedule.GetScheduleLastRefreshTime(ModuleKey);
        return lastDate < today;
    }

    protected override void OnRefreshed()
    {
        DB.GameSchedule.SetScheduleLastRefreshTime(ModuleKey, GetTodayStartTimestamp());
        DB.SaveGameSchedule();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 获取今天 UTC 00:00 对应的 Unix 秒时间戳。
    /// 供 UTC Daily 调度和需要同一 UTC 日界线口径的运行时业务使用。
    /// </summary>
    public static long GetTodayStartTimestamp()
    {
        return GetDayStartTimestamp(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// 计算指定时刻所属 UTC 自然日 00:00 对应的 Unix 秒时间戳。
    /// 调用边界：传入任意带偏移的 DateTimeOffset，方法会先统一换算为 UTC。
    /// </summary>
    public static long GetDayStartTimestamp(DateTimeOffset currentTime)
    {
        DateTimeOffset utcTime = currentTime.ToUniversalTime();
        return new DateTimeOffset(
            utcTime.Year,
            utcTime.Month,
            utcTime.Day,
            0,
            0,
            0,
            TimeSpan.Zero).ToUnixTimeSeconds();
    }

    #endregion
}

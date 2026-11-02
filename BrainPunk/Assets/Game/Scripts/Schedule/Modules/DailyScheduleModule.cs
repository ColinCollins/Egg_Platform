using System;
using Bear.Logger;
using Bear.SaveModule;

/// <summary>
/// 每日调度模块：按设备当地自然日 00:00 判断是否需要刷新。
/// ScheduleSystem 的内置模块之一。
/// 持有并管理所有 Daily 刷新相关的业务子模块实例。
/// </summary>
public class DailyScheduleModule : ScheduleModuleBase
{
    public override string ModuleKey => "Daily";

    /// <summary>
    /// 按设备当地自然日累计游玩天数的刷新模块。
    /// 仅由 Daily 调度触发，外部如需读取实例也可通过 GetRefreshable 获取。
    /// </summary>
    public PlayDaysRefreshModule PlayDaysModule { get; }

    public InterstitialNOAdPopupAutoShowModule InterstitialNOAdPopupModule { get; }
    public ShopAdRewardTipAutoShowModule ShopAdRewardTipModule { get; }

    /// <summary>
    /// 礼物弹窗统一管理器；Daily 刷新由其分发给包含 ChestGift 在内的各礼物类型控制器。
    /// </summary>
    public GiftManager GiftModule { get; }

    public DailyScheduleModule()
    {
        PlayDaysModule = new PlayDaysRefreshModule();
        this.Register(PlayDaysModule);

        InterstitialNOAdPopupModule = new InterstitialNOAdPopupAutoShowModule();
        this.Register(InterstitialNOAdPopupModule);

        ShopAdRewardTipModule = new ShopAdRewardTipAutoShowModule();
        this.Register(ShopAdRewardTipModule);

        GiftModule = GiftManager.Instance;
        this.Register(GiftModule);

        DailyChallengeManager dailyChallengeManager = GameManager.Instance?.DailyChallenge;
        if (dailyChallengeManager != null)
            this.Register(dailyChallengeManager);
    }

    /// <summary>
    /// 按类型获取已注册的 Daily 刷新业务模块实例。
    /// </summary>
    public T GetRefreshable<T>() where T : class, IScheduleRefreshable
    {
        foreach (var r in _refreshables)
        {
            if (r is T t)
                return t;
        }
        return null;
    }

    protected override bool ShouldRefresh()
    {
        long lastDate = DB.GameSchedule.GetScheduleLastRefreshTime(ModuleKey);
        if (lastDate <= 0)
        {
            return true;
        }

        var timeZone = TimeZoneInfo.Local;
        DateTime today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).Date;
        DateTime lastRefreshDate = TimeZoneInfo.ConvertTime(
            DateTimeOffset.FromUnixTimeSeconds(lastDate), timeZone).Date;
        return lastRefreshDate < today;
    }

    protected override void OnRefreshed()
    {
        long today = GetTodayStartTimestamp();
        DB.GameSchedule.SetScheduleLastRefreshTime(ModuleKey, today);
        DB.SaveGameSchedule();
    }

    /// <summary>
    /// 获取设备当地时区今天 00:00 对应的 Unix 秒时间戳。
    /// 供 Daily 调度和需要同一日界线口径的运行时业务使用。
    /// 注意：玩家修改系统时区后，日界线会随设备当地时区变化。
    /// </summary>
    public static long GetTodayStartTimestamp()
    {
        return GetDayStartTimestamp(DateTimeOffset.UtcNow, TimeZoneInfo.Local);
    }

    /// <summary>
    /// 计算指定时刻在指定时区内所属自然日 00:00 对应的 Unix 秒时间戳。
    /// 调用方可用于固定时区业务或自动化测试；运行时默认入口请使用 GetTodayStartTimestamp。
    /// 注意：timeZone 不能为空，夏令时偏移按该自然日的当地 00:00 计算。
    /// </summary>
    public static long GetDayStartTimestamp(DateTimeOffset currentTime, TimeZoneInfo timeZone)
    {
        if (timeZone == null)
        {
            throw new ArgumentNullException(nameof(timeZone));
        }

        DateTimeOffset localTime = TimeZoneInfo.ConvertTime(currentTime, timeZone);
        DateTime localDayStart = new DateTime(
            localTime.Year,
            localTime.Month,
            localTime.Day,
            0,
            0,
            0,
            DateTimeKind.Unspecified);
        TimeSpan offset = timeZone.GetUtcOffset(localDayStart);
        return new DateTimeOffset(localDayStart, offset).ToUnixTimeSeconds();
    }
}

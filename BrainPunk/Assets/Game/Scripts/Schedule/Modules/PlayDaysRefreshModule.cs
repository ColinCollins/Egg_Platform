using System;
using Bear.Logger;

/// <summary>
/// 按设备当地自然日累计游玩天数。
/// 仅由 DailyScheduleModule 在跨日刷新时调用，不应由界面或关卡重复触发。
/// 每次累计后立即保存 GameData；日界线和重复触发保护由 DailyScheduleModule 负责。
/// </summary>
public sealed class PlayDaysRefreshModule : IScheduleRefreshable, IDebuger
{
    #region IScheduleRefreshable

    /// <summary>
    /// 调度日志标识。
    /// </summary>
    public string RefreshKey => "PlayDays";

    /// <summary>
    /// 本进程内是否已经成功完成过一次 PlayDays 递增与保存。
    /// 供登录日打点判断当天首次 Daily 刷新；重新启动进程后会恢复为 false。
    /// </summary>
    public bool WasIncrementedThisSession { get; private set; }

    /// <summary>
    /// Daily 跨日刷新回调：累计一个当地自然日并立即保存 GameData。
    /// 注意：本方法不自行判断日期，只能通过 DailyScheduleModule 调用。
    /// </summary>
    public void OnScheduleRefresh()
    {
        DB.GameData.PlayDays++;

        if (!DB.SaveGameData())
        {
            throw new InvalidOperationException(
                $"[{RefreshKey}] Save failed. Current={DB.GameData.PlayDays}");
        }

        WasIncrementedThisSession = true;
        this.Log($"[{RefreshKey}] Daily count increased. Current={DB.GameData.PlayDays}");
    }

    #endregion
}

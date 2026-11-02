/// <summary>
/// 需要被周期调度的业务模块接口。
/// 实现类向具体的 ScheduleModule（如 DailyScheduleModule）注册自己。
/// </summary>
public interface IScheduleRefreshable
{
    /// <summary>模块标识，用于日志追踪</summary>
    string RefreshKey { get; }

    /// <summary>周期刷新触发时执行</summary>
    void OnScheduleRefresh();
}

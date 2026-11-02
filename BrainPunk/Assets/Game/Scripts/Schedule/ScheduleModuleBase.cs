using System;
using System.Collections.Generic;
using Bear.Logger;

/// <summary>
/// 调度模块抽象基类。封装公共的注册列表、遍历执行、异常防护。
/// 子类只需实现「是否需要刷新」的判断逻辑。
/// </summary>
public abstract class ScheduleModuleBase : IDebuger
{
    protected readonly List<IScheduleRefreshable> _refreshables = new();

    /// <summary>模块唯一 Key，用于存储区分（如 "Daily" / "Weekly"）</summary>
    public abstract string ModuleKey { get; }

    /// <summary>依赖注入入口：各业务模块在 Init() 中调用</summary>
    public void Register(IScheduleRefreshable refreshable)
    {
        if (refreshable == null)
        {
            this.LogWarning($"[Schedule] [{ModuleKey}] Register ignored. refreshable is null.");
            return;
        }

        if (_refreshables.Contains(refreshable))
        {
            this.LogWarning($"[Schedule] [{ModuleKey}] Register ignored. Already registered: {refreshable.RefreshKey}");
            return;
        }

        _refreshables.Add(refreshable);
        this.Log($"[Schedule] [{ModuleKey}] Registered: {refreshable.RefreshKey}");
    }

    /// <summary>注销依赖：业务模块在 Dispose 时调用</summary>
    public void Unregister(IScheduleRefreshable refreshable)
    {
        if (refreshable == null)
        {
            return;
        }

        _refreshables.Remove(refreshable);
        this.Log($"[Schedule] [{ModuleKey}] Unregistered: {refreshable.RefreshKey}");
    }

    /// <summary>
    /// 由 ScheduleSystem 调用。判断是否满足刷新条件，
    /// 若满足则执行所有注册的业务模块，并持久化刷新时间。
    /// </summary>
    public void CheckAndRefresh()
    {
        if (!ShouldRefresh())
        {
            this.Log($"[Schedule] [{ModuleKey}] Skip.");
            return;
        }

        this.Log($"[Schedule] [{ModuleKey}] Trigger. Count={_refreshables.Count}");

        foreach (var r in _refreshables)
        {
            try
            {
                r.OnScheduleRefresh();
                this.Log($"[Schedule] [{ModuleKey}] {r.RefreshKey} OK");
            }
            catch (Exception e)
            {
                this.LogError($"[Schedule] [{ModuleKey}] {r.RefreshKey} failed: {e}");
            }
        }

        OnRefreshed();
        this.Log($"[Schedule] [{ModuleKey}] Finished.");
    }

    /// <summary>判断当前是否满足刷新条件</summary>
    protected abstract bool ShouldRefresh();

    /// <summary>刷新完成后回调，用于持久化当前周期时间</summary>
    protected abstract void OnRefreshed();
}

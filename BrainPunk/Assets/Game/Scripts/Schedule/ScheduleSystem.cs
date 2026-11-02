using System;
using System.Collections.Generic;
using Bear.Logger;
using Game.Common;
using UnityEngine;

/// <summary>
/// 周期调度系统中央控制器。
/// 负责管理所有 ScheduleModule，并在启动、日界线和应用恢复时统一触发刷新检查。
/// </summary>
public class ScheduleSystem : Singleton<ScheduleSystem>, IDebuger
{
    private const float ClockCalibrationInterval = 60f;
    private const float MinimumBoundaryDelay = 0.1f;

    private readonly Dictionary<string, ScheduleModuleBase> _modules = new();
    private float _nextBoundaryCheckTime;
    private float _nextClockCalibrationTime;
    private DateTime _observedLocalDate;
    private DateTime _observedUtcDate;
    private bool _hasObservedDates;

    /// <summary>注册调度模块。通常在 ScheduleSystem 初始化时完成。</summary>
    public void RegisterModule(ScheduleModuleBase module)
    {
        if (module == null)
        {
            this.LogWarning("[ScheduleSystem] RegisterModule ignored. module is null.");
            return;
        }

        _modules[module.ModuleKey] = module;
        this.Log($"[ScheduleSystem] Module registered: {module.ModuleKey}");
    }

    /// <summary>获取已注册的指定类型模块。</summary>
    public T GetModule<T>() where T : ScheduleModuleBase
    {
        foreach (var module in _modules.Values)
        {
            if (module is T t)
                return t;
        }
        return null;
    }

    /// <summary>
    /// 遍历所有已注册的调度模块，分别检查并执行刷新。
    /// 调用边界：用于启动检查、GM 调试和运行时检测到当地或 UTC 日期变化后的补刷新。
    /// </summary>
    public void CheckAllModules()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CheckAllModules(now);
        ScheduleRuntimeChecks(now, Time.unscaledTime);
    }

    /// <summary>
    /// 运行时周期检查入口，由 GameManager.Update 持续调用。
    /// 普通帧只比较非缩放时间；到达预约日界线或校准点时才读取系统日期。
    /// </summary>
    public void Tick()
    {
        float runtimeNow = Time.unscaledTime;
        if (_hasObservedDates &&
            runtimeNow < _nextBoundaryCheckTime &&
            runtimeNow < _nextClockCalibrationTime)
        {
            return;
        }

        CheckRuntimeBoundary(DateTimeOffset.UtcNow, runtimeNow);
    }

    /// <summary>
    /// 立即校准当前当地与 UTC 日期，并重新预约最近日界线。
    /// 调用边界：由应用恢复前台回调使用；日期未变化时不会遍历模块或读写存档。
    /// </summary>
    public void CheckRuntimeBoundary()
    {
        CheckRuntimeBoundary(DateTimeOffset.UtcNow, Time.unscaledTime);
    }

    private void CheckRuntimeBoundary(DateTimeOffset now, float runtimeNow)
    {
        DateTime localDate = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local).Date;
        DateTime utcDate = now.UtcDateTime.Date;
        if (!_hasObservedDates)
        {
            CaptureObservedDates(localDate, utcDate);
        }
        else if (localDate != _observedLocalDate || utcDate != _observedUtcDate)
        {
            CheckAllModules(now);
        }

        ScheduleRuntimeChecks(now, runtimeNow);
    }

    private void CheckAllModules(DateTimeOffset now)
    {
        this.Log($"[ScheduleSystem] CheckAllModules. Count={_modules.Count}");

        foreach (var module in _modules.Values)
        {
            try
            {
                module.CheckAndRefresh();
            }
            catch (Exception e)
            {
                this.LogError($"[ScheduleSystem] Module {module.ModuleKey} CheckAndRefresh failed: {e}");
            }
        }

        CaptureObservedDates(
            TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local).Date,
            now.UtcDateTime.Date);
    }

    private void CaptureObservedDates(DateTime localDate, DateTime utcDate)
    {
        _observedLocalDate = localDate;
        _observedUtcDate = utcDate;
        _hasObservedDates = true;
    }

    private void ScheduleRuntimeChecks(DateTimeOffset now, float runtimeNow)
    {
        TimeZoneInfo localTimeZone = TimeZoneInfo.Local;
        DateTime localDate = TimeZoneInfo.ConvertTime(now, localTimeZone).Date;
        DateTime nextLocalDate = DateTime.SpecifyKind(localDate.AddDays(1), DateTimeKind.Unspecified);
        DateTimeOffset nextLocalBoundary = new(
            nextLocalDate,
            localTimeZone.GetUtcOffset(nextLocalDate));
        DateTimeOffset nextUtcBoundary = new(
            now.UtcDateTime.Date.AddDays(1),
            TimeSpan.Zero);
        DateTimeOffset nextBoundary = nextLocalBoundary < nextUtcBoundary
            ? nextLocalBoundary
            : nextUtcBoundary;
        float boundaryDelay = (float)Math.Max(
            MinimumBoundaryDelay,
            (nextBoundary - now).TotalSeconds);

        _nextBoundaryCheckTime = runtimeNow + boundaryDelay;
        _nextClockCalibrationTime = runtimeNow + ClockCalibrationInterval;
    }
}

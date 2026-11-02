using System;
using System.Collections.Generic;
using Bear.SaveModule;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Utils.Analytics;
using Guru.SDK.Framework.Utils.Log;
using UnityEngine;

public partial class GameSDKService
{
    private const string LoginDayEventPrefix = "login_day_";
    private const string LoginDayLogTag = "LoginDay";
    private static readonly TimeSpan LoginDayReadyTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 登录打点天
    /// </summary>
    private readonly List<int> LoginCheckDays = new List<int>()
    {
        2
    };

    /// <summary>
    /// GuruSDK 初始化完成后启动登录天数检测。
    /// 调用边界：仅限运行态；方法不会阻塞 SDK Ready 回调，会等待 DB 和当地 Daily 刷新就绪后再读取 PlayDays。
    /// 注意：当前命中事件只发送到 Facebook，事件名格式为 login_day_{天数}。
    /// </summary>
    public void LoginDay()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        LoginDayAsync().Forget();
    }

    private async UniTask LoginDayAsync()
    {
        bool didTimeout = await UniTask.WaitUntil(IsLoginDayDataReady)
            .TimeoutWithoutException(LoginDayReadyTimeout, DelayType.UnscaledDeltaTime);
        if (didTimeout)
        {
            Log.W("Login day check skipped: DB or local Daily schedule was not ready before timeout.", LoginDayLogTag);
            return;
        }

        int playDays = DB.GameData.PlayDays;
        if (!LoginCheckDays.Contains(playDays))
        {
            Log.D($"Login day check skipped: PlayDays={playDays}.", LoginDayLogTag);
            return;
        }

        var dailyModule = ScheduleSystem.Instance.GetModule<DailyScheduleModule>();
        if (dailyModule?.PlayDaysModule == null ||
            !dailyModule.PlayDaysModule.WasIncrementedThisSession)
        {
            Log.D($"Login day check skipped: PlayDays={playDays} was not increased in this session.", LoginDayLogTag);
            return;
        }

        string eventName = $"{LoginDayEventPrefix}{playDays}";
        var parameters = new Dictionary<string, object>
        {
            ["login_day"] = playDays
        };

        LogEvent(
            eventName,
            parameters,
            AnalyticsCapabilities.FacebookCapabilities);
        Log.I($"Facebook login day event queued: {eventName}.", LoginDayLogTag);
    }

    private static bool IsLoginDayDataReady()
    {
        var dbManager = DBManager.Instance;
        if (dbManager == null || !dbManager.IsInitialized)
        {
            return false;
        }

        if (ScheduleSystem.Instance.GetModule<DailyScheduleModule>() == null)
        {
            return false;
        }

        var gameSchedule = DB.GameSchedule;
        if (gameSchedule == null)
        {
            return false;
        }

        long lastDailyRefreshTime = gameSchedule.GetScheduleLastRefreshTime("Daily");
        return lastDailyRefreshTime == DailyScheduleModule.GetTodayStartTimestamp();
    }
}

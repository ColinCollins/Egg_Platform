using Bear.SaveModule;
using Game.ConfigModule;
using UnityEngine;

/// <summary>
/// 每日免费金币红点判断器。
/// 调用边界：仅由 RedDotManager 在运行时刷新时调用，只读本地配置与 GameSchedule。
/// 注意事项：不会创建运行时单例，不写存档、不发奖、不打开 UI。
/// </summary>
public sealed class DailyGiftCoinRedDotChecker : IRedDotChecker
{
    #region IRedDotChecker

    /// <summary>
    /// 当前奖励配置有效且已跨过上次领取后的下一设备当地自然日时显示红点。
    /// </summary>
    /// <returns>true 表示每日金币可领取。</returns>
    public bool IsOn()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        DBManager dbManager = UnityEngine.Object.FindAnyObjectByType<DBManager>();
        ConfigManager configManager = UnityEngine.Object.FindAnyObjectByType<ConfigManager>();
        if (dbManager == null
            || !dbManager.IsInitialized
            || configManager == null
            || !configManager.IsInitialized)
        {
            return false;
        }

        GameSchedule schedule = dbManager.Get<GameSchedule>();
        long currentTimestamp = DailyGiftCoinClockGuard.GetCurrentTimestamp(out _);
        return DailyGiftCoinRules.TryGetState(
                   configManager,
                   schedule,
                   currentTimestamp,
                   out _,
                   out bool isAvailable,
                   out _)
               && isAvailable;
    }

    #endregion
}

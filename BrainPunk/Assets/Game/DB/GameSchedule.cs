using System.Collections.Generic;
using UnityEngine;

namespace Bear.SaveModule
{
    [CreateAssetMenu(fileName = "GameSchedule", menuName = "Save Data/GameSchedule")]
    public partial class GameSchedule : BaseSaveDataSO
    {
        public static StorageType StorageType = StorageType.PlayerPrefs;

        /// <summary>各调度模块的最后刷新时间 &lt;ModuleKey, Unix秒时间戳&gt;</summary>
        [SerializeField] private Dictionary<string, long> scheduleLastRefreshTimes = new();


        // 插屏后的每日免广告弹窗提醒弹出次数
        [SerializeField] private int daily_interstitialNoAdPopupShowCount = 0;
        // 商店关闭后免费金币提醒弹出次数
        [SerializeField] private int daily_shopFreeCoinShowCount = 0;

        /// <summary>
        /// ShopPanel 每日免费金币的上次成功领取时间，使用 Unix 秒时间戳。
        /// 仅在领取资格持久化成功后更新，用于跨会话防止同一当地自然日重复领取。
        /// </summary>
        [SerializeField] private long dailyFreeCoinLastClaimTime = 0;

        /// <summary>
        /// ShopPanel 每日免费金币流程已观察到的最大有效 Unix 秒时间戳。
        /// 仅允许递增，用于在后续会话发现设备时间回拨并阻止重复领取。
        /// </summary>
        [SerializeField] private long dailyFreeCoinLastObservedTime = 0;

        // GiftData 迁移兼容字段；新业务不再写入，至少保留一个兼容版本。
        [SerializeField] private int chestGiftDailyRefreshCount = 0;

        public long GetScheduleLastRefreshTime(string moduleKey)
        {
            if (scheduleLastRefreshTimes != null &&
                scheduleLastRefreshTimes.TryGetValue(moduleKey, out var value))
            {
                return value;
            }
            return 0;
        }

        public void SetScheduleLastRefreshTime(string moduleKey, long timestamp)
        {
            if (scheduleLastRefreshTimes == null)
                scheduleLastRefreshTimes = new Dictionary<string, long>();

            scheduleLastRefreshTimes[moduleKey] = timestamp;
        }


#region DailyChallenge

        /// <summary>
        /// 当前进度
        /// </summary>
        [SerializeField] private int dailyChallengeCurrentProcess = 0;

        /// <summary>
        /// 每日挑战获取奖励
        /// </summary>
        [SerializeField] private List<int> dailyChallengeHasRewardId = new List<int>();

        /// <summary>
        /// 每日挑战有效期间当日成功通关的关卡。
        /// </summary>
        [SerializeField] private List<int> dailyChallengePassedLevel = new List<int>();

#endregion
        
    }
}

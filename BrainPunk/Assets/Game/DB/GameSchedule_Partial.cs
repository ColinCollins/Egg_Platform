using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bear.SaveModule
{
    public partial class GameSchedule
    {
        /// <summary>
        /// 初始化数据（设置默认值）
        /// </summary>
        public override void Init()
        {
            scheduleLastRefreshTimes = new();
            daily_interstitialNoAdPopupShowCount = 0;
            daily_shopFreeCoinShowCount = 0;
            dailyFreeCoinLastClaimTime = 0;
            dailyFreeCoinLastObservedTime = 0;
            chestGiftDailyRefreshCount = 0;
            dailyChallengeCurrentProcess = 0;
            dailyChallengeHasRewardId = new List<int>();
            dailyChallengePassedLevel = new List<int>();
        }

        public Dictionary<string, long> ScheduleLastRefreshTimes
        {
            get => scheduleLastRefreshTimes;
            set => scheduleLastRefreshTimes = value;
        }

        public int Daily_interstitialNoAdPopupShowCount
        {
            get => daily_interstitialNoAdPopupShowCount;
            set => daily_interstitialNoAdPopupShowCount = value;
        }

        public int Daily_shopFreeCoinShowCount
        {
            get => daily_shopFreeCoinShowCount;
            set => daily_shopFreeCoinShowCount = value;
        }

        /// <summary>
        /// ShopPanel 每日免费金币上次成功领取的 Unix 秒时间戳。
        /// 调用边界：仅供每日免费金币领取流程读取和更新；写入后必须同步保存 GameSchedule。
        /// 注意事项：资格判断按设备当地自然日计算，未来时间戳会继续保持冷却以避免重复领取。
        /// </summary>
        public long DailyFreeCoinLastClaimTime
        {
            get => dailyFreeCoinLastClaimTime;
            set => dailyFreeCoinLastClaimTime = value;
        }

        /// <summary>
        /// 每日免费金币流程已观察到的最大有效 Unix 秒时间戳。
        /// 调用边界：只能写入不小于当前值的时间；用于识别跨会话设备时间回拨。
        /// 注意事项：本地高水位只能防回拨，不能把应用关闭期间的未来时间与真实离线时长区分开。
        /// </summary>
        public long DailyFreeCoinLastObservedTime
        {
            get => dailyFreeCoinLastObservedTime;
            set => dailyFreeCoinLastObservedTime = value;
        }

        /// <summary>
        /// GiftData 迁移兼容的历史 ChestGift 刷新次数；新业务不得继续写入。
        /// </summary>
        public int ChestGiftDailyRefreshCount
        {
            get => chestGiftDailyRefreshCount;
            set => chestGiftDailyRefreshCount = value;
        }

        public int DailyChallengeCurrentProcess
        {
            get => dailyChallengeCurrentProcess;
            set => dailyChallengeCurrentProcess = value;
        }

        public List<int> DailyChallengeHasRewardId
        {
            get => dailyChallengeHasRewardId;
            set => dailyChallengeHasRewardId = value;
        }

        public List<int> DailyChallengePassedLevel
        {
            get => dailyChallengePassedLevel;
            set => dailyChallengePassedLevel = value;
        }

    }
}

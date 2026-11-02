using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bear.SaveModule
{
    public partial class DigGame
    {
        /// <summary>
        /// 初始化数据（设置默认值）
        /// </summary>
        public override void Init()
        {
            currentLevel = 1;
            choiceLevelPanelPageIndex = 0;
            levelPlayCount = new Dictionary<string, int>();
            levelSuccessCount = new Dictionary<string, int>();
            levelReviveCount = new Dictionary<string, int>();
            levelPlayDuration = new Dictionary<string, long>();
            lastInterstitialDate = 0;
            lastRewardAdDate = 0;
            interstitialFailCount = 0;
            interstitialLevelSuccessCount = 0;
            dailyInterstitialNoAdPopupShowCount = 0;

            maxLevel = 0;
            unlockLevels = new List<int>();
            passedLevels = new List<int>();
            sharedLevelIds = new List<string>();
            hasReactionLevel = new Dictionary<string, int>();
            levelCollectedStar = new Dictionary<string, int>();
            levelWaitingAnimStar = new Dictionary<string, int>();
        }

        public int CurrentLevel
        {
            get => currentLevel;
            set => currentLevel = value;
        }

        public int MaxLevel
        {
            get => maxLevel;
            set => maxLevel = value;
        }

        public List<int> UnlockLevels
        {
            get => unlockLevels;
            set => unlockLevels = value;
        }

        public List<int> PassedLevels
        {
            get => passedLevels;
            set => passedLevels = value;
        }

        public List<string> SharedLevelIds
        {
            get => sharedLevelIds;
            set => sharedLevelIds = value;
        }

        public Dictionary<string, int> HasReactionLevel
        {
            get => hasReactionLevel;
            set => hasReactionLevel = value;
        }

        public Dictionary<string, int> LevelCollectedStar
        {
            get => levelCollectedStar;
            set => levelCollectedStar = value;
        }

        public Dictionary<string, int> LevelWaitingAnimStar
        {
            get => levelWaitingAnimStar;
            set => levelWaitingAnimStar = value;
        }

        public int ChoiceLevelPanelPageIndex
        {
            get => choiceLevelPanelPageIndex;
            set => choiceLevelPanelPageIndex = value;
        }

        public Dictionary<string, int> LevelPlayCount
        {
            get => levelPlayCount;
            set => levelPlayCount = value;
        }

        public Dictionary<string, int> LevelSuccessCount
        {
            get => levelSuccessCount;
            set => levelSuccessCount = value;
        }

        public Dictionary<string, int> LevelReviveCount
        {
            get => levelReviveCount;
            set => levelReviveCount = value;
        }

        public Dictionary<string, long> LevelPlayDuration
        {
            get => levelPlayDuration;
            set => levelPlayDuration = value;
        }

        public long LastInterstitialDate
        {
            get => lastInterstitialDate;
            set => lastInterstitialDate = value;
        }

        public long LastRewardAdDate
        {
            get => lastRewardAdDate;
            set => lastRewardAdDate = value;
        }

        public int InterstitialFailCount
        {
            get => interstitialFailCount;
            set => interstitialFailCount = value;
        }

        public int InterstitialLevelSuccessCount
        {
            get => interstitialLevelSuccessCount;
            set => interstitialLevelSuccessCount = value;
        }

        public int DailyInterstitialNoAdPopupShowCount
        {
            get => dailyInterstitialNoAdPopupShowCount;
            set => dailyInterstitialNoAdPopupShowCount = value;
        }

    }
}

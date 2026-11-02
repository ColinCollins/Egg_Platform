using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bear.SaveModule
{
    public partial class DrawGame
    {
        /// <summary>
        /// 初始化数据（设置默认值）
        /// </summary>
        public override void Init()
        {
            currentLevel = 1;
            maxLevel = 0;
            unlockLevels = new List<int>();
            passedLevels = new List<int>();
            chioceLevelPanelPageIndex = 0;
            levelPlayCount = new Dictionary<string, int>();
            levelSuccessCount = new Dictionary<string, int>();
            levelReviveCount = new Dictionary<string, int>();
            levelPlayDuration = new Dictionary<string, long>();
            lastInterstitialDate = 0L;
            lastRewardAdDate = 0L;
            interstitialFailCount = 0;
            interstitialLevelSuccessCount = 0;
            dailyInterstitialNoAdPopupShowCount = 0;
            currentCollectedPuzzledId = new List<int>();
            levelCollectedStar = new Dictionary<string, int>();
            levelWaitingAnimStar = new Dictionary<string, int>();
            sharedLevelIds = new List<string>();
            hasReactionLevel = new Dictionary<string, int>();
            hasGuide = false;
            isGuiding = false;
            ownedSkin = new List<string>();
            currentSkinId = "";
            waitingPreviewSkinId = new List<string>();
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

        public int ChioceLevelPanelPageIndex
        {
            get => chioceLevelPanelPageIndex;
            set => chioceLevelPanelPageIndex = value;
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

        public List<int> CurrentCollectedPuzzledId
        {
            get => currentCollectedPuzzledId;
            set => currentCollectedPuzzledId = value;
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

        public bool HasGuide
        {
            get => hasGuide;
            set => hasGuide = value;
        }

        public bool IsGuiding
        {
            get => isGuiding;
            set => isGuiding = value;
        }

        public List<string> OwnedSkin
        {
            get => ownedSkin;
            set => ownedSkin = value;
        }

        public string CurrentSkinId
        {
            get => currentSkinId;
            set => currentSkinId = value;
        }

        public List<string> WaitingPreviewSkinId
        {
            get => waitingPreviewSkinId;
            set => waitingPreviewSkinId = value;
        }

    }
}

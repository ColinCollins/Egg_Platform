using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bear.SaveModule
{
    public partial class IWannerGame
    {
        /// <summary>
        /// 初始化数据（设置默认值）
        /// </summary>
        public override void Init()
        {
            currentLevel = 0;
            currentGroupID = -1;
            maxGroupId = 0;
            unlockGroups = new List<int>();
            passedLevels = new List<int>();
            sharedGroupIds = new List<int>();
            reactionGroupResults = new Dictionary<int, int>();
            chioceLevelPanelPageIndex = 0;
            lastMapIndex = 0;
            levelPlayCount = new Dictionary<string, int>();
            levelSuccessCount = new Dictionary<string, int>();
            levelReviveCount = new Dictionary<string, int>();
            levelPlayDuration = new Dictionary<string, long>();
            lastInterstitialDate = 0L;
            lastRewardAdDate = 0L;
            interstitialFailCount = 0;
            interstitialLevelSuccessCount = 0;
            interstitialPassedLevelCount = 0;
            dailyInterstitialNoAdPopupShowCount = 0;
        }

        public int CurrentLevel
        {
            get => currentLevel;
            set => currentLevel = value;
        }

        public int CurrentGroupID
        {
            get => currentGroupID;
            set => currentGroupID = value;
        }

        public int MaxGroupId
        {
            get => maxGroupId;
            set => maxGroupId = value;
        }

        public List<int> UnlockGroups
        {
            get => unlockGroups;
            set => unlockGroups = value;
        }

        public List<int> PassedLevels
        {
            get => passedLevels;
            set => passedLevels = value;
        }

        /// <summary>
        /// 用途：保存 IWanner 各 Group 的一次性分享奖励领取状态。
        /// 调用边界：仅由 IWanner 胜利面板读写，并在变更后通过 DB.SaveIWannerGame 持久化。
        /// 注意事项：GroupId 与主线 LevelId 命名空间不同，禁止复用 GameData.HasSharedLevel。
        /// </summary>
        public List<int> SharedGroupIds
        {
            get => sharedGroupIds;
            set => sharedGroupIds = value;
        }

        /// <summary>
        /// 用途：保存 IWanner Reward Victory 的喜欢/不喜欢结果。
        /// 调用边界：仅由带 IWanner 上下文的 GameVictoryPanel_Share 读写。
        /// 注意事项：字典值 1 表示喜欢、0 表示不喜欢，每个 Group 只写入一次。
        /// </summary>
        public Dictionary<int, int> ReactionGroupResults
        {
            get => reactionGroupResults;
            set => reactionGroupResults = value;
        }

        public int ChioceLevelPanelPageIndex
        {
            get => chioceLevelPanelPageIndex;
            set => chioceLevelPanelPageIndex = value;
        }

        /// <summary>
        /// 用途：保存 IWanner 玩家上一次成功进入关卡所在的 Map Index。
        /// 调用边界：仅由 IWannerGame_ChoiceLevelPopup 在进入 Group/TBN 成功后写入，并在弹窗显示时读取。
        /// 注意事项：读取时仍需按 PageView 当前页数夹取，不得借用 ChioceLevelPanelPageIndex。
        /// </summary>
        public int LastMapIndex
        {
            get => lastMapIndex;
            set => lastMapIndex = value;
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

        /// <summary>
        /// IWannerGame 自上次胜利插屏成功展示后的普通小关通过累计次数。
        /// 调用边界：仅供 IWanner 插屏策略读写，禁止与 Group 胜利次数混用。
        /// </summary>
        public int InterstitialPassedLevelCount
        {
            get => interstitialPassedLevelCount;
            set => interstitialPassedLevelCount = value;
        }

        public int DailyInterstitialNoAdPopupShowCount
        {
            get => dailyInterstitialNoAdPopupShowCount;
            set => dailyInterstitialNoAdPopupShowCount = value;
        }

    }
}

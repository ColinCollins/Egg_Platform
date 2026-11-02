using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bear.SaveModule
{
    public partial class GameData
    {
        /// <summary>
        /// 初始化数据（设置默认值）
        /// </summary>
        public override void Init()
        {
            isEnterByOneLink = false;
            oneinlinkEnterType = new List<Config.Level.GameType>();
            oneLinkClaimedRewardIds = new List<string>();
            currentLevel = 1;
            maxLevel = 0;
            unlockLevels = new List<int>();
            passedLevels = new List<int>();
            chioceLevelPanelPageIndex = 0;
            playCount = 0;
            playDays = 0;
            gameTools = CreateDefaultGameTools();
            purchaseCache = new Dictionary<string, int>();
            giftEntryStateVersion = 1;
            hasReachedGiftHome = false;
            lastChestGiftPurchaseLocalDay = 0;
            toolConsumesCache = new Dictionary<string, int>();
            lastInterstitialDate = 0;
            lastRewardAdDate = 0;
            rewardADCount = 0;
            interstitialFailCount = 0;
            interstitialLevelSuccessCount = 0;
            hasRatingPopup = false;
            hasShowDailyChallenge = false;
            hasUnlockDailyChallenge = false;
            levelPlayCount = new Dictionary<string, int>();
            levelSuccessCount = new Dictionary<string, int>();
            levelReviveCount = new Dictionary<string, int>();
            levelPlayDuration = new Dictionary<string, long>();
            remoteConfigCache = new Dictionary<string, string>();
            adGetTools = new Dictionary<string, int>();
            purchaseGetTools = new Dictionary<string, int>();
            hasComics = false;
            hasBegineTutorial = false;
            hasDrawGameGuidePopup = false;
            hasIWannerGameGuidePopup = false;
            currentAppVersion = "";
            hasSharedLevel = new List<int>();
            hasReactionLevel = new Dictionary<string, int>();
            couldPopupBulletinBoard = false;
            hasEnterLevel61 = false;
            hasGetLevel61Key = false;
            ownedSkin = new List<string>();
            currentBackPart = "";
            currentFacePart = "";
            currentBodyPart = "";
            currentSkinId = "";
            waitingPreviewSkinId = new List<string>();
            hasTriggeredGameTipsGuide = false;
            hasShopToSkinGuide = false;
        }

        public bool IsEnterByOneLink
        {
            get => isEnterByOneLink;
            set => isEnterByOneLink = value;
        }

        public List<Config.Level.GameType> OneinlinkEnterType
        {
            get => oneinlinkEnterType;
            set => oneinlinkEnterType = value;
        }

        public List<string> OneLinkClaimedRewardIds
        {
            get => oneLinkClaimedRewardIds;
            set => oneLinkClaimedRewardIds = value;
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

        public int PlayCount
        {
            get => playCount;
            set => playCount = value;
        }

        public int PlayDays
        {
            get => playDays;
            set => playDays = value;
        }

        public Dictionary<string, int> GameTools
        {
            get => gameTools;
            set => gameTools = value;
        }

        public Dictionary<string, int> PurchaseCache
        {
            get => purchaseCache;
            set => purchaseCache = value;
        }

        /// <summary>
        /// GiftData 迁移兼容版本；新业务不得继续写入。
        /// </summary>
        public int GiftEntryStateVersion
        {
            get => giftEntryStateVersion;
            set => giftEntryStateVersion = value;
        }

        /// <summary>
        /// GiftData 迁移兼容的历史主页到达状态；新业务不得继续写入。
        /// </summary>
        public bool HasReachedGiftHome
        {
            get => hasReachedGiftHome;
            set => hasReachedGiftHome = value;
        }

        /// <summary>
        /// GiftData 迁移兼容的历史 ChestGift 购买当地日期；新业务不得继续写入。
        /// </summary>
        public long LastChestGiftPurchaseLocalDay
        {
            get => lastChestGiftPurchaseLocalDay;
            set => lastChestGiftPurchaseLocalDay = value;
        }

        public Dictionary<string, int> ToolConsumesCache
        {
            get => toolConsumesCache;
            set => toolConsumesCache = value;
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

        /// <summary>
        /// 累计实际开始展示的激励视频次数。
        /// 调用边界：仅由激励广告展示生命周期回调累加；写入后必须立即保存 GameData。
        /// </summary>
        public int RewardADCount
        {
            get => rewardADCount;
            set => rewardADCount = value;
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

        public bool HasRatingPopup
        {
            get => hasRatingPopup;
            set => hasRatingPopup = value;
        }

        public bool HasShowDailyChallenge
        {
            get => hasShowDailyChallenge;
            set => hasShowDailyChallenge = value;
        }

        public bool HasUnlockDailyChallenge
        {
            get => hasUnlockDailyChallenge;
            set => hasUnlockDailyChallenge = value;
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

        public Dictionary<string, string> RemoteConfigCache
        {
            get => remoteConfigCache;
            set => remoteConfigCache = value;
        }

        public Dictionary<string, int> AdGetTools
        {
            get => adGetTools;
            set => adGetTools = value;
        }

        public Dictionary<string, int> PurchaseGetTools
        {
            get => purchaseGetTools;
            set => purchaseGetTools = value;
        }

        public bool HasComics
        {
            get => hasComics;
            set => hasComics = value;
        }

        public bool HasBegineTutorial
        {
            get => hasBegineTutorial;
            set => hasBegineTutorial = value;
        }

        /// <summary>
        /// Draw 操作引导已展示记录；仅在有效弹窗创建后置为 true，写入后须保存 GameData。
        /// </summary>
        public bool HasDrawGameGuidePopup
        {
            get => hasDrawGameGuidePopup;
            set => hasDrawGameGuidePopup = value;
        }

        /// <summary>
        /// IWanner 操作引导已展示记录；与 Draw 独立，清档后允许再次展示。
        /// </summary>
        public bool HasIWannerGameGuidePopup
        {
            get => hasIWannerGameGuidePopup;
            set => hasIWannerGameGuidePopup = value;
        }

        public string CurrentAppVersion
        {
            get => currentAppVersion;
            set => currentAppVersion = value;
        }

        public List<int> HasSharedLevel
        {
            get => hasSharedLevel;
            set => hasSharedLevel = value;
        }

        public Dictionary<string, int> HasReactionLevel
        {
            get => hasReactionLevel;
            set => hasReactionLevel = value;
        }

        public bool CouldPopupBulletinBoard
        {
            get => couldPopupBulletinBoard;
            set => couldPopupBulletinBoard = value;
        }

        public bool HasEnterLevel61
        {
            get => hasEnterLevel61;
            set => hasEnterLevel61 = value;
        }

        public bool HasGetLevel61Key
        {
            get => hasGetLevel61Key;
            set => hasGetLevel61Key = value;
        }

        public List<string> OwnedSkin
        {
            get => ownedSkin;
            set => ownedSkin = value;
        }

        public string CurrentBackPart
        {
            get => currentBackPart;
            set => currentBackPart = value;
        }

        public string CurrentFacePart
        {
            get => currentFacePart;
            set => currentFacePart = value;
        }

        public string CurrentBodyPart
        {
            get => currentBodyPart;
            set => currentBodyPart = value;
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

        public bool HasTriggeredGameTipsGuide
        {
            get => hasTriggeredGameTipsGuide;
            set => hasTriggeredGameTipsGuide = value;
        }

        public bool HasShopToSkinGuide
        {
            get => hasShopToSkinGuide;
            set => hasShopToSkinGuide = value;
        }

    }
}

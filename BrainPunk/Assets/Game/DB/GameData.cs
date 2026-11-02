using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Bear.SaveModule
{
    // [CreateAssetMenu(fileName = "GameData", menuName = "Save Data/GameData")]
    public partial class GameData : BaseSaveDataSO
    {
        public static StorageType StorageType = StorageType.PlayerPrefs;

        #region OneLink

        /// <summary>本轮是否由 OneLink 进入；由 OneLinkManager 在启动、普通进关和退出时清除，供胜利面板判断重玩显示。</summary>
        [SerializeField] private bool isEnterByOneLink = false;

        /// <summary>已成功通过 OneLink 进入的玩法；每种玩法仅记录一次，不记录关卡或伪造通关进度。</summary>
        [SerializeField] private List<Config.Level.GameType> oneinlinkEnterType = new List<Config.Level.GameType>();

        /// <summary>已领取的 OneLink 奖励唯一标识；getCoin/getSkin 共用，随奖励同份保存，勿在普通启动时清空。</summary>
        [SerializeField] private List<string> oneLinkClaimedRewardIds = new List<string>();

        #endregion

        private static Dictionary<string, int> CreateDefaultGameTools()
        {
            return new Dictionary<string, int>
            {
                { "Coin", -999 }
            };
        }

        // 当前关卡
        [SerializeField] private int currentLevel = 1;

        // 最大通关关卡
        [SerializeField] private int maxLevel = 0;

        // 已解锁关卡
        [SerializeField] private List<int> unlockLevels = new List<int>();

        // 已通关关卡
        [SerializeField] private List<int> passedLevels = new List<int>();

        // ChoiceLevelPanel 页数记录
        [SerializeField] private int chioceLevelPanelPageIndex = 0;
        // 开启游戏次数
        [SerializeField] private int playCount = 0;
        // 游玩天数
        [SerializeField] private int playDays = 0;

        // 背包道具内容
        [SerializeField] private Dictionary<string, int> gameTools = CreateDefaultGameTools();

        // 内购数据缓存 <product_id, count>
        [SerializeField] private Dictionary<string, int> purchaseCache = new Dictionary<string, int>();

        #region Gift Entry Legacy Migration

        /// <summary>
        /// GiftData 迁移兼容字段。旧存档缺少此字段时为 0，用于识别升级用户。
        /// 注意事项：新业务不得继续写入；至少保留一个兼容版本，避免历史 PlayerPrefs 无法迁移。
        /// </summary>
        [SerializeField] private int giftEntryStateVersion = 0;

        /// <summary>
        /// GiftData 迁移兼容字段：历史版本记录的主页到达状态。
        /// 注意事项：新业务不得继续写入。
        /// </summary>
        [SerializeField] private bool hasReachedGiftHome = false;

        /// <summary>
        /// GiftData 迁移兼容字段：历史版本记录的 ChestGift 最近购买当地日期。
        /// 注意事项：新业务不得继续写入。
        /// </summary>
        [SerializeField] private long lastChestGiftPurchaseLocalDay = 0;

        #endregion

        // 道具使用情况记录
        [SerializeField] private Dictionary<string, int> toolConsumesCache = new Dictionary<string, int>();

        // 上一次播放插屏时间
        [SerializeField] private long lastInterstitialDate = 0;

        // 上一次播放激励时间
        [SerializeField] private long lastRewardAdDate = 0;

        /// <summary>
        /// 用途：累计实际开始展示的激励视频次数。
        /// 注意事项：展示失败不计入；开始展示后即计入，不等待奖励结果。
        /// </summary>
        [SerializeField] private int rewardADCount = 0;

        // 用于插屏计算失败次数
        [SerializeField] private int interstitialFailCount = 0;

        // 用于插屏计算成功通关次数，包含广告通关
        [SerializeField] private int interstitialLevelSuccessCount = 0;

        // 评分弹窗已经弹出
        [SerializeField] private bool hasRatingPopup = false;

        /// <summary>
        /// 用途：记录 DailyChallenge 结算弹窗是否已经成功创建过。
        /// 调用边界：仅由 DailyChallengePopup 在有效结算弹窗创建成功后写入。
        /// 注意事项：旧存档缺少该字段时保持 false，由首次展示流程负责保存。
        /// </summary>
        [SerializeField] private bool hasShowDailyChallenge = false;

        /// <summary>
        /// 用途：记录 DailyChallenge 是否已经达到关卡解锁条件。
        /// 调用边界：仅由 DailyChallengeManager 在成功通关入口写入。
        /// 注意事项：一旦解锁不随每日刷新重置，写入后必须立即保存 GameData。
        /// </summary>
        [SerializeField] private bool hasUnlockDailyChallenge = false;

        // 对应关卡游玩次数, <levelPath, count>
        [SerializeField] private Dictionary<string, int> levelPlayCount = new Dictionary<string, int>();
        // 对应关卡胜利次数, <levelPath, count>
        [SerializeField] private Dictionary<string, int> levelSuccessCount = new Dictionary<string, int>();

        // 对应关卡复活次数, <levelPath, count>
        [SerializeField] private Dictionary<string, int> levelReviveCount = new Dictionary<string, int>();

        // 对应关卡游玩时长 mm , <levelPath, duration>
        [SerializeField] private Dictionary<string, long> levelPlayDuration = new Dictionary<string, long>();

        [SerializeField] private Dictionary<string, string> remoteConfigCache = new Dictionary<string, string>();


        // 打点需要

        #region 打点

        [SerializeField] private Dictionary<string, int> adGetTools = new Dictionary<string, int>();
        [SerializeField] private Dictionary<string, int> purchaseGetTools = new Dictionary<string, int>();

        #endregion


        #region 1.2.0

        // 开场漫画
        [SerializeField] private bool hasComics = false;

        // 新手引导
        [SerializeField] private bool hasBegineTutorial = false;

        #endregion

        #region 玩法操作引导

        /// <summary>
        /// 记录 Draw 操作引导是否已经展示；有效弹窗创建后写入并立即保存，旧存档默认为 false。
        /// </summary>
        [SerializeField] private bool hasDrawGameGuidePopup = false;

        /// <summary>
        /// 记录 IWanner 操作引导是否已经展示；与主线、Draw 独立，有效弹窗创建后写入并立即保存。
        /// </summary>
        [SerializeField] private bool hasIWannerGameGuidePopup = false;

        #endregion

        #region 1.6.0
        // 记录用户上一个版本数据信息
        [SerializeField] private string currentAppVersion = "";

        // 已经分享过的关卡
        [SerializeField] private List<int> hasSharedLevel = new List<int>();

        /// <summary>
        /// 评价过 like | dislike. 1 | 0
        /// </summary>
        [SerializeField] private Dictionary<string, int> hasReactionLevel = new Dictionary<string, int>();
        #endregion


        #region 1.7.0
        /// <summary>
        /// 是否可以弹出更新公告弹窗，在 currentAppVersion 更新时候刷新
        /// </summary>
        [SerializeField] private bool couldPopupBulletinBoard = false;

        // 进入 61 关后才会触发
        [SerializeField] private bool hasEnterLevel61 = false;
        // 获取 ChoicePanel 的钥匙后才会触发
        [SerializeField] private bool hasGetLevel61Key = false;
        #endregion

        #region  1.8.0 skin part
        // 已经获取皮肤
        [SerializeField] private List<string> ownedSkin = new List<string>();
        // 当前背部展示
        [SerializeField] private string currentBackPart = "";
        [SerializeField] private string currentFacePart = "";
        [SerializeField] private string currentBodyPart = "";

        // 当前 spine 切换角色皮肤
        [SerializeField] private string currentSkinId = "";

        // 1.9.0, 用于红点系统，判断是否有新的皮肤还没有 preview 过。
        [SerializeField] private List<string> waitingPreviewSkinId = new List<string>();
        #endregion

        #region Game Tips Guide

        // 是否已经触发过首次提示引导；触发时立即保存，避免跨关卡或重进游戏后重复播放。
        [SerializeField] private bool hasTriggeredGameTipsGuide = false;

        /// <summary>
        /// 是否触发过 ShopPanel 跳转 SkinPart 的引导。
        /// </summary>
        [SerializeField] private bool hasShopToSkinGuide = false;

        #endregion
    }
}

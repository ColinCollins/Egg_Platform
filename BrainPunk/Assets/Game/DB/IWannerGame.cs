using System.Collections.Generic;
using UnityEngine;

namespace Bear.SaveModule
{
    public partial class IWannerGame : BaseSaveDataSO
    {
        public static StorageType StorageType = StorageType.PlayerPrefs;

        // 当前关卡
        [SerializeField] private int currentLevel = 0;

        // 当前所在 groupId
        [SerializeField] private int currentGroupID = -1;

        // 最大通关Group
        [SerializeField] private int maxGroupId = 0;

        // 已解锁关卡
        [SerializeField] private List<int> unlockGroups = new List<int>();

        // 已通关关卡
        [SerializeField] private List<int> passedLevels = new List<int>();

        /// <summary>
        /// IWanner 已领取分享奖励的 GroupId。
        /// 注意事项：仅供 IWanner Victory 分享流程使用，不得与主线或 Draw 的分享记录混用。
        /// </summary>
        [SerializeField] private List<int> sharedGroupIds = new List<int>();

        /// <summary>
        /// IWanner 已提交胜利评价的 GroupId 与评价值，1 表示喜欢、0 表示不喜欢。
        /// 注意事项：仅供 Reward Victory 的评价按钮使用，每个 Group 只记录首次选择。
        /// </summary>
        [SerializeField] private Dictionary<int, int> reactionGroupResults = new Dictionary<int, int>();

        // ChoiceLevelPanel 页数记录
        [SerializeField] private int chioceLevelPanelPageIndex = 0;

        /// <summary>
        /// IWanner 玩家上一次成功进入关卡所在的 Map Index。
        /// 注意事项：仅供 IWannerGame_ChoiceLevelPopup 读写，不得与历史 ChoiceLevelPanel 页码字段混用。
        /// </summary>
        [SerializeField] private int lastMapIndex = 0;

        // 对应 IWanner 小关游玩次数, <IWannerLevelData.Path, count>
        [SerializeField] private Dictionary<string, int> levelPlayCount = new Dictionary<string, int>();
        // 对应 IWanner 小关胜利次数, <IWannerLevelData.Path, count>
        [SerializeField] private Dictionary<string, int> levelSuccessCount = new Dictionary<string, int>();

        // 对应 IWanner 小关重试次数, <IWannerLevelData.Path, count>
        [SerializeField] private Dictionary<string, int> levelReviveCount = new Dictionary<string, int>();

        // 对应 IWanner 小关历史累计游玩时长, <IWannerLevelData.Path, duration>，单位毫秒
        [SerializeField] private Dictionary<string, long> levelPlayDuration = new Dictionary<string, long>();

        #region 插屏广告独立状态

        /// <summary>
        /// IWannerGame 上一次成功展示插屏的 UTC 秒级时间戳。
        /// 注意事项：仅供 IWanner 插屏策略使用，不得与主线或 Draw 时间戳混用。
        /// </summary>
        [SerializeField] private long lastInterstitialDate;

        /// <summary>
        /// IWannerGame 上一次成功播放激励视频的 UTC 秒级时间戳。
        /// 注意事项：仅用于 IWanner 激励视频后的插屏冷却。
        /// </summary>
        [SerializeField] private long lastRewardAdDate;

        /// <summary>
        /// IWannerGame 自上次失败插屏成功展示后的失败或手动 Reset 累计次数。
        /// </summary>
        [SerializeField] private int interstitialFailCount;

        /// <summary>
        /// IWannerGame 自上次胜利插屏成功展示后的 Group 胜利累计次数。
        /// </summary>
        [SerializeField] private int interstitialLevelSuccessCount;

        /// <summary>
        /// IWannerGame 自上次胜利插屏成功展示后的普通小关通过累计次数。
        /// 注意事项：TBN 不计入；正常通关与激励视频 Skip 均视为通过小关。
        /// </summary>
        [SerializeField] private int interstitialPassedLevelCount;

        /// <summary>
        /// IWannerGame 当地自然日内已自动展示免广告弹窗的次数。
        /// 注意事项：只由每日刷新模块和 IWannerPlayCtrl 读写，不得与主线或 Draw 计数混用。
        /// </summary>
        [SerializeField] private int dailyInterstitialNoAdPopupShowCount;

        #endregion

        #region Runtime API

        /// <summary>
        /// 用途：修复旧档或字段缺失存档中的空集合。
        /// 调用边界：运行时读取 IWannerGame 集合前调用；本方法只补空值，不改变已有进度。
        /// 注意事项：返回 true 时调用方应在本次业务写入完成后保存。
        /// </summary>
        /// <returns>至少一个集合被补齐时返回 true。</returns>
        public bool EnsureValidCollections()
        {
            bool changed = false;
            changed |= EnsureCollection(ref unlockGroups);
            changed |= EnsureCollection(ref passedLevels);
            changed |= EnsureCollection(ref sharedGroupIds);
            changed |= EnsureCollection(ref reactionGroupResults);
            changed |= EnsureCollection(ref levelPlayCount);
            changed |= EnsureCollection(ref levelSuccessCount);
            changed |= EnsureCollection(ref levelReviveCount);
            changed |= EnsureCollection(ref levelPlayDuration);
            return changed;
        }

        private static bool EnsureCollection<T>(ref List<T> collection)
        {
            if (collection != null)
                return false;

            collection = new List<T>();
            return true;
        }

        private static bool EnsureCollection<TKey, TValue>(ref Dictionary<TKey, TValue> collection)
        {
            if (collection != null)
                return false;

            collection = new Dictionary<TKey, TValue>();
            return true;
        }

        #endregion
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Bear.SaveModule
{
    /// <summary>Dig 独立进度；不保存关内大小、地形或主线/Draw 统计。</summary>
    public partial class DigGame : BaseSaveDataSO
    {
        #region 存档字段
        /// <summary>使用既有 PlayerPrefs 存储，与其它玩法各自独立。</summary>
        public static StorageType StorageType = StorageType.PlayerPrefs;
        /// <summary>当前排序关卡 Id，不是 DigLevelData.Id。</summary>
        [SerializeField] private int currentLevel = 1;
        /// <summary>已通关的最大排序 Id；初始没有通关。</summary>
        [SerializeField] private int maxLevel = 0;
        /// <summary>已解锁的排序 Id；配置初始化补首关，不清理历史值。</summary>
        [SerializeField] private List<int> unlockLevels = new List<int>();
        /// <summary>已通关排序 Id，用于区分首次/重复胜利。</summary>
        [SerializeField] private List<int> passedLevels = new List<int>();
        /// <summary>已领取分享奖励的 DigLevelData.Id。</summary>
        [SerializeField] private List<string> sharedLevelIds = new List<string>();
        /// <summary>以 DigLevelData.Id 为键的点赞 1/点踩 0 记录。</summary>
        [SerializeField] private Dictionary<string, int> hasReactionLevel = new Dictionary<string, int>();
        /// <summary>以 DigLevelData.Id 为键的历史最高墨水星数；普通通关只增不减，TBC 不写入。</summary>
        [SerializeField] private Dictionary<string, int> levelCollectedStar = new Dictionary<string, int>();
        /// <summary>以 DigLevelData.Id 为键的新增待播星数；胜利面板读取后一次性消费，不作为选关永久星数。</summary>
        [SerializeField] private Dictionary<string, int> levelWaitingAnimStar = new Dictionary<string, int>();
        #endregion


        #region 选关、统计与广告独立字段
        /// <summary>选关分页索引，只在 Dig 面板读写。</summary>
        [SerializeField] private int choiceLevelPanelPageIndex = 0;
        /// <summary>真实 DigLevelData.Id 的进入次数，含 Reset/Replay。</summary>
        [SerializeField] private Dictionary<string, int> levelPlayCount = new Dictionary<string, int>();
        /// <summary>真实 DigLevelData.Id 的成功次数，参与四玩法累计。</summary>
        [SerializeField] private Dictionary<string, int> levelSuccessCount = new Dictionary<string, int>();
        /// <summary>真实 DigLevelData.Id 的失败与主动 Reset 次数。</summary>
        [SerializeField] private Dictionary<string, int> levelReviveCount = new Dictionary<string, int>();
        /// <summary>DigLevelData.Path 的累计游玩毫秒数。</summary>
        [SerializeField] private Dictionary<string, long> levelPlayDuration = new Dictionary<string, long>();
        /// <summary>Dig 成功插屏的 UTC 秒数。</summary>
        [SerializeField] private long lastInterstitialDate = 0;
        /// <summary>Dig 成功激励视频的 UTC 秒数。</summary>
        [SerializeField] private long lastRewardAdDate = 0;
        /// <summary>Dig 自上次失败插屏后的失败/重试次数。</summary>
        [SerializeField] private int interstitialFailCount = 0;
        /// <summary>Dig 自上次胜利插屏后的成功次数。</summary>
        [SerializeField] private int interstitialLevelSuccessCount = 0;
        /// <summary>当地自然日的 Dig 免广告自动弹窗次数。</summary>
        [SerializeField] private int dailyInterstitialNoAdPopupShowCount = 0;
        #endregion

        #region 运行时校验
        /// <summary>仅补缺失集合，保留未知配置的历史进度；调用方决定是否保存。</summary>
        public bool EnsureValidCollections()
        {
            bool changed = levelPlayCount == null || levelSuccessCount == null || levelReviveCount == null || levelPlayDuration == null || unlockLevels == null || passedLevels == null || sharedLevelIds == null || hasReactionLevel == null || levelCollectedStar == null || levelWaitingAnimStar == null;
            unlockLevels ??= new List<int>();
            passedLevels ??= new List<int>();
            sharedLevelIds ??= new List<string>();
            hasReactionLevel ??= new Dictionary<string, int>();
            levelCollectedStar ??= new Dictionary<string, int>();
            levelWaitingAnimStar ??= new Dictionary<string, int>();
            levelPlayCount ??= new Dictionary<string, int>();
            levelSuccessCount ??= new Dictionary<string, int>();
            levelReviveCount ??= new Dictionary<string, int>();
            levelPlayDuration ??= new Dictionary<string, long>();
            return changed;
        }
        #endregion
    }
}

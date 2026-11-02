using System.Collections.Generic;
using UnityEngine;

namespace Bear.SaveModule
{
    public partial class DrawGame : BaseSaveDataSO
    {
        public static StorageType StorageType = StorageType.PlayerPrefs;

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

        // 对应关卡游玩次数, <DrawLevelData.Id, count>
        [SerializeField] private Dictionary<string, int> levelPlayCount = new Dictionary<string, int>();
        // 对应关卡胜利次数, <DrawLevelData.Id, count>
        [SerializeField] private Dictionary<string, int> levelSuccessCount = new Dictionary<string, int>();

        // 对应关卡重试次数, <DrawLevelData.Id, count>
        [SerializeField] private Dictionary<string, int> levelReviveCount = new Dictionary<string, int>();

        // 对应关卡历史累计游玩时长, <DrawLevelData.Path, duration>，单位毫秒
        [SerializeField] private Dictionary<string, long> levelPlayDuration = new Dictionary<string, long>();

        #region 插屏广告独立状态

        /// <summary>
        /// DrawGame 上一次成功展示插屏的 UTC 秒级时间戳。
        /// 注意事项：仅供 Draw 插屏策略使用，不得与主线 DB.GameData 时间戳混用。
        /// </summary>
        [SerializeField] private long lastInterstitialDate;

        /// <summary>
        /// DrawGame 上一次成功播放激励视频的 UTC 秒级时间戳。
        /// 注意事项：仅用于 Draw 激励视频后的插屏冷却。
        /// </summary>
        [SerializeField] private long lastRewardAdDate;

        /// <summary>
        /// DrawGame 自上次失败插屏成功展示后的失败或重试累计次数。
        /// </summary>
        [SerializeField] private int interstitialFailCount;

        /// <summary>
        /// DrawGame 自上次胜利插屏成功展示后的胜利累计次数。
        /// </summary>
        [SerializeField] private int interstitialLevelSuccessCount;

        /// <summary>
        /// DrawGame 当地自然日内已自动展示免广告弹窗的次数。
        /// 注意事项：只由 Draw 每日刷新模块和 DrawPlayCtrl 读写，不得与主线 GameSchedule 计数混用。
        /// </summary>
        [SerializeField] private int dailyInterstitialNoAdPopupShowCount;

        #endregion

        #region 用于 victory 动画表现 star
        /// <summary>
        /// 当前 postcard 已经解锁的部件 id，根据这个 count 判断 chest 的 progress
        /// </summary>
        [SerializeField] private List<int> currentCollectedPuzzledId = new List<int>();

        /// <summary>
        /// Draw 关卡历史最高星数，键使用 DrawLevelData.Id，值限制为 0~3。
        /// 该数据由 Draw 选关和胜利表现读取，不得与主线 Postcard 星记录混用。
        /// </summary>
        [SerializeField] private Dictionary<string, int> levelCollectedStar = new Dictionary<string, int>();

        /// <summary>
        /// Draw 关卡本轮新增且等待胜利面板播放动画的星数，键使用 DrawLevelData.Id。
        /// 该数据由 Draw VictoryPanel 一次性消费，不得作为选关永久星数读取。
        /// </summary>
        [SerializeField] private Dictionary<string, int> levelWaitingAnimStar = new Dictionary<string, int>();

        /// <summary>
        /// Draw 关卡已领取分享奖励的配置 Id。
        /// 注意事项：仅供 Draw 胜利面板使用，不得与主线 GameData.HasSharedLevel 混用。
        /// </summary>
        [SerializeField] private List<string> sharedLevelIds = new List<string>();

        /// <summary>
        /// Draw 关卡评价记录，键为 DrawLevelData.Id，值为点赞 1 或点踩 0。
        /// 注意事项：每关只允许写入一次，不与主线 GameData.HasReactionLevel 混用。
        /// </summary>
        [SerializeField] private Dictionary<string, int> hasReactionLevel = new Dictionary<string, int>();

        /// <summary>
        /// 是否有引导
        /// </summary>
        [SerializeField] private bool hasGuide;
        /// <summary>
        /// 是否正在引导流程中
        /// </summary>
        [SerializeField] private bool isGuiding;

        #endregion

        #region pencil skin
        // 已经获取皮肤
        [SerializeField] private List<string> ownedSkin = new List<string>();
        // 当前 spine 切换角色皮肤
        [SerializeField] private string currentSkinId = "";

        // 1.9.0, 用于红点系统，判断是否有新的皮肤还没有 preview 过。
        [SerializeField] private List<string> waitingPreviewSkinId = new List<string>();
        #endregion

        #region Runtime API

        /// <summary>
        /// 修复旧档或缺字段存档中的空集合。
        /// 调用边界：运行时读取 DrawGame 数据前调用；本方法只补空值，不改变已有进度。
        /// </summary>
        /// <returns>数据被修复时返回 true，调用方应按需保存。</returns>
        public bool EnsureValidCollections()
        {
            bool changed = false;
            changed |= EnsureCollection(ref unlockLevels);
            changed |= EnsureCollection(ref passedLevels);
            changed |= EnsureCollection(ref levelPlayCount);
            changed |= EnsureCollection(ref levelSuccessCount);
            changed |= EnsureCollection(ref levelReviveCount);
            changed |= EnsureCollection(ref levelPlayDuration);
            changed |= EnsureCollection(ref currentCollectedPuzzledId);
            changed |= EnsureCollection(ref levelCollectedStar);
            changed |= EnsureCollection(ref levelWaitingAnimStar);
            changed |= EnsureCollection(ref sharedLevelIds);
            changed |= EnsureCollection(ref hasReactionLevel);
            changed |= EnsureCollection(ref ownedSkin);
            changed |= EnsureCollection(ref waitingPreviewSkinId);
            return changed;
        }

        /// <summary>
        /// Draw 选关面板上次停留的分页索引。
        /// 注意事项：底层序列化字段保留历史拼写，业务代码统一使用本属性。
        /// </summary>
        public int ChoiceLevelPanelPageIndex
        {
            get => chioceLevelPanelPageIndex;
            set => chioceLevelPanelPageIndex = value;
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

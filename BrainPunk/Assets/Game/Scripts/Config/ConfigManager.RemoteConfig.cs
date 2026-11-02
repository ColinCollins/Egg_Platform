using System.Collections.Generic;
using Config.LubanConfig;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.ConfigModule
{
    /// <summary>
    /// ConfigManager 的 RemoteConfig 扩展：
    /// 用于存储并解析从 RemoteConfigService 拉取到的配置 JSON。
    /// </summary>
    public partial class ConfigManager
    {
        public static class RemoteConfig
        {
            public const string LevelConfigKey = "level_config";
            public const string DrawGameConfigKey = "drawgame_config";

            /// <summary>Dig 独立配置顶层 key；不读取 Draw 的云控值。</summary>
            public const string DigGameConfigKey = "diggame_config";

            /// <summary>IWannerGame 独立 RemoteConfig 的顶层 key。</summary>
            public const string IWannerGameConfigKey = "iwannergame_config";

            public const string EnabledFieldKey = "enabled";
            public const string LevelSortKey = "level_sort";
            public const string ShowTipsTimeLimitFieldKey = "showTipsTimeLimit";
            public const string ShowTipsFailCountFieldKey = "showTipsFailCount";
            public const string ShowRatingLevelKey = "showRatingLevel";

            public const string OpenGAPS = "open_gaps";

            public const string AdInterstitialCDFieldKey = "ad_interstitialCD";
            public const string AdRewardResetInterstitialCDFieldKey = "ad_rewardResetInterstitialCD";
            public const string AdInterstitialShowMinLevelFieldKey = "ad_interstitialShowMinLevel";
            public const string AdInterstitialShowIntervalCountFieldKey = "ad_interstitialShowIntervalCount";
            public const string AdInterstitialShowFailedCountFieldKey = "ad_interstitialShowFailedCount";

            /// <summary>主线插屏关卡区间上界列表的 RemoteConfig 字段名。</summary>
            public const string MainGameInterstitialAdLevelRangeFieldKey = "mainGame_interstitalAdLevelRange";

            /// <summary>主线各关卡区间胜利插屏间隔数的 RemoteConfig 字段名。</summary>
            public const string MainGameInterstitialAdRangeLevelCountFieldKey = "mainGame_interstitalAdRangeLevelCount";

            /// <summary>主线各关卡区间失败插屏门槛的 RemoteConfig 字段名。</summary>
            public const string MainGameInterstitialAdRangeFailedCountFieldKey = "mianGame_interstititalAdRangeFailedCount";

            /// <summary>DrawGame 功能开关的 RemoteConfig 字段名。</summary>
            public const string DrawGameIsOpenFieldKey = "drawGameIsOpen";

            /// <summary>DrawGame 失败提示次数的 RemoteConfig 字段名。</summary>
            public const string DrawGameShowTipsFailCountFieldKey = "drawGameShowTipsFailCount";

            /// <summary>DrawGame 提示等待时间的 RemoteConfig 字段名。</summary>
            public const string DrawGameShowTipsTimeLimitFieldKey = "drawGameShowTipsTimeLimit";

            /// <summary>DrawGame 插屏冷却的 RemoteConfig 字段名。</summary>
            public const string DrawGameInterstitialCDFieldKey = "drawGame_interstitialCD";

            /// <summary>DrawGame 激励视频后插屏冷却的 RemoteConfig 字段名。</summary>
            public const string DrawGameRewardResetInterstitialCDFieldKey = "drawGame_rewardResetInterstitialCD";

            /// <summary>DrawGame 插屏最小关卡的 RemoteConfig 字段名。</summary>
            public const string DrawGameInterstitialShowMinLevelFieldKey = "drawGame_interstitialShowMinLevel";

            /// <summary>DrawGame 胜利插屏间隔次数的 RemoteConfig 字段名。</summary>
            public const string DrawGameInterstitialShowIntervalCountFieldKey = "drawGame_interstitialShowIntervalCount";

            /// <summary>DrawGame 失败插屏触发次数的 RemoteConfig 字段名。</summary>
            public const string DrawGameInterstitialShowFailedCountFieldKey = "drawGame_interstitialShowFailedCount";

            /// <summary>DrawGame 插屏关卡区间上界列表的 RemoteConfig 字段名。</summary>
            public const string DrawGameInterstitialAdLevelRangeFieldKey = "drawGame_interstitalAdLevelRange";

            /// <summary>DrawGame 各关卡区间胜利插屏间隔数的 RemoteConfig 字段名。</summary>
            public const string DrawGameInterstitialAdRangeLevelCountFieldKey = "drawGame_interstitalAdRangeLevelCount";

            /// <summary>DrawGame 各关卡区间失败插屏门槛的 RemoteConfig 字段名。</summary>
            public const string DrawGameInterstitialAdRangeFailedCountFieldKey = "drawGame_interstititalAdRangeFailedCount";

            /// <summary>DrawGame 每日免广告弹窗次数的 RemoteConfig 字段名。</summary>
            public const string DrawGameDailyNOAdPopupAutoShowCountFieldKey = "drawGame_dailyNOAdPopupAutoShowCount";

            /// <summary>DrawGame 获取场景提示消耗金币数量的 RemoteConfig 字段名。</summary>
            public const string DrawGameGameTipsPopupCoinCostFieldKey = "drawGame_gameTipsPopupCoinCost";

            /// <summary>DrawGame 提示后跳关消耗金币数量的 RemoteConfig 字段名。</summary>
            public const string DrawGameGameTipsPopupPassLevelCoinCostFieldKey = "drawGame_gameTipsPopupPassLevelCoinCost";

            /// <summary>DigGame 功能开关的 RemoteConfig 字段名。</summary>
            public const string DigGameIsOpenFieldKey = "digGameIsOpen";

            /// <summary>DigGame 失败提示次数的 RemoteConfig 字段名。</summary>
            public const string DigGameShowTipsFailCountFieldKey = "digGameShowTipsFailCount";

            /// <summary>DigGame 提示等待时间的 RemoteConfig 字段名。</summary>
            public const string DigGameShowTipsTimeLimitFieldKey = "digGameShowTipsTimeLimit";

            /// <summary>DigGame 插屏冷却的 RemoteConfig 字段名。</summary>
            public const string DigGameInterstitialCDFieldKey = "digGame_interstitialCD";

            /// <summary>DigGame 激励视频后插屏冷却的 RemoteConfig 字段名。</summary>
            public const string DigGameRewardResetInterstitialCDFieldKey = "digGame_rewardResetInterstitialCD";

            /// <summary>DigGame 插屏最小关卡的 RemoteConfig 字段名。</summary>
            public const string DigGameInterstitialShowMinLevelFieldKey = "digGame_interstitialShowMinLevel";

            /// <summary>DigGame 胜利插屏间隔次数的 RemoteConfig 字段名。</summary>
            public const string DigGameInterstitialShowIntervalCountFieldKey = "digGame_interstitialShowIntervalCount";

            /// <summary>DigGame 失败插屏触发次数的 RemoteConfig 字段名。</summary>
            public const string DigGameInterstitialShowFailedCountFieldKey = "digGame_interstitialShowFailedCount";

            /// <summary>DigGame 插屏关卡区间上界列表的 RemoteConfig 字段名。</summary>
            public const string DigGameInterstitialAdLevelRangeFieldKey = "digGame_interstitalAdLevelRange";

            /// <summary>DigGame 各关卡区间胜利插屏间隔数的 RemoteConfig 字段名。</summary>
            public const string DigGameInterstitialAdRangeLevelCountFieldKey = "digGame_interstitalAdRangeLevelCount";

            /// <summary>DigGame 各关卡区间失败插屏门槛的 RemoteConfig 字段名。</summary>
            public const string DigGameInterstitialAdRangeFailedCountFieldKey = "digGame_interstititalAdRangeFailedCount";

            /// <summary>DigGame 每日免广告弹窗次数的 RemoteConfig 字段名。</summary>
            public const string DigGameDailyNOAdPopupAutoShowCountFieldKey = "digGame_dailyNOAdPopupAutoShowCount";

            /// <summary>DigGame 获取场景提示消耗金币数量的 RemoteConfig 字段名。</summary>
            public const string DigGameGameTipsPopupCoinCostFieldKey = "digGame_gameTipsPopupCoinCost";

            /// <summary>DigGame 提示后跳关消耗金币数量的 RemoteConfig 字段名。</summary>
            public const string DigGameGameTipsPopupPassLevelCoinCostFieldKey = "digGame_gameTipsPopupPassLevelCoinCost";

            /// <summary>IWannerGame 入口开关的 RemoteConfig 字段名。</summary>
            public const string IWannerGameIsOpenFieldKey = "iWannerGame_IsOpen";

            /// <summary>IWannerGame 失败后显示 Skip 的次数字段名。</summary>
            public const string IWannerGameShowSkipFailCountFieldKey = "iWannerGame_showSkipFailCount";

            /// <summary>IWannerGame 插屏冷却的 RemoteConfig 字段名。</summary>
            public const string IWannerGameInterstitialCDFieldKey = "iWannerGame_interstitialCD";

            /// <summary>IWannerGame 激励视频后插屏冷却的 RemoteConfig 字段名。</summary>
            public const string IWannerGameRewardResetInterstitialCDFieldKey = "iWannerGame_rewardResetInterstitialCD";

            /// <summary>IWannerGame 插屏最小显示 Group 序号的 RemoteConfig 字段名。</summary>
            public const string IWannerGameInterstitialShowMinGroupFieldKey = "iWannerGame_interstitialShowMinGroup";

            /// <summary>IWannerGame 插屏显示间隔数的 RemoteConfig 字段名。</summary>
            public const string IWannerGameInterstitialShowIntervalCountFieldKey = "iWannerGame_interstitialShowIntervalCount";

            /// <summary>IWannerGame 失败次数触发插屏的 RemoteConfig 字段名。</summary>
            public const string IWannerGameInterstitialShowFailedCountFieldKey = "iWannerGame_interstitialShowFailedCount";

            /// <summary>IWannerGame 插屏关卡区间上界列表的 RemoteConfig 字段名。</summary>
            public const string IWannerGameInterstitialAdLevelRangeFieldKey = "iwannarGame_interstitalAdLevelRange";

            /// <summary>IWannerGame 各关卡区间胜利插屏间隔数的 RemoteConfig 字段名。</summary>
            public const string IWannerGameInterstitialAdRangeLevelCountFieldKey = "iwannarGame_interstitalAdRangeLevelCount";

            /// <summary>IWannerGame 各关卡区间失败插屏门槛的 RemoteConfig 字段名。</summary>
            public const string IWannerGameInterstitialAdRangeFailedCountFieldKey = "iwannarGame_interstititalAdRangeFailedCount";

            /// <summary>IWannerGame 普通小关通过次数触发插屏的 RemoteConfig 字段名。</summary>
            public const string IWannerGameInterstitialShowPassedLevelCountFieldKey = "iWannerGame_interstitialShowPassedLevelCount";

            /// <summary>IWannerGame 每日免广告弹窗次数的 RemoteConfig 字段名。</summary>
            public const string IWannerGameDailyNOAdPopupAutoShowCountFieldKey = "iWannerGame_dailyNOAdPopupAutoShowCount";
            public const string VictoryHorizantalRewardsFieldKey = "victoryHorizantalRewards";
            public const string GameTipsPopupCoinCostFieldKey = "gameTipsPopupCoinCost";
            public const string GameTipsPopupPassLevelCoinCostFieldKey = "gameTipsPopupPassLevelCoinCost";
            public const string AdDailyNOAdPopupAutoShowCountFieldKey = "ad_dailyNOAdPopupAutoShowCount";
            public const string ShopFreeCoinShowCountFieldKey = "shop_adfreeCoinAutoShowCount";

            /// <summary>ShopPanel 每日免费金币数量的 RemoteConfig 字段名。</summary>
            public const string DailyFreeCoinCountFieldKey = "dailyFreeCoinCount";

            public const string InitGameCoinRewardKey = "initGameCoinReward";

            public const string AutoPopupBulleinBoardKey = "AutoPopupBulleinBoardKey";

            public const string GetFreeSkinLevelKey = "getFreeSkinLevel";
            public const string FreeSkinIdKey = "freeSkinId";
            public const string PostcardGuideLevelKey = "postcardGuideLevel";
            public const string GameTipsGuideLevelKey = "gameTipsGuideLevel";
            public const string GameTipsGuideFailedCountKey = "gameTipsGuideFailedCount";
            public const string GameTipsGuideWaitingTimeKey = "gameTipsGuideWaitingTime";
            public const string DailyChallengeIsOpenKey = "dailyChallengeIsOpen";
            public const string DailyChallengeStep1CoinRewardKey = "dailyChallengeStep1CoinReward";
            public const string DailyChallengeStep2CoinRewardKey = "dailyChallengeStep2CoinReward";
            public const string DailyChallengeStepsKey = "dailyChallengeSteps";
            public const string DailyChallengeUnlockLevelKey = "dailyChallengeUnlockLevel";
            public const string DailyChallengeRandomSkinPoolIdKey = "dailyChallengeRandomSkinPoolId";
            public const string DailyChallengeRewardMultipleKey = "dailyChallengeRewardMultiple";
            /// <summary>每日宝箱入口与弹窗的云控开关字段名。</summary>
            public const string IsOpenDailyChestFieldKey = "isOpenDailyChest";
            /// <summary>新玩家首日首次通关后自动弹出每日宝箱的主线关卡序号字段名。</summary>
            public const string AutoShowChestGiftLevelFieldKey = "autoShowChestGiftLevel";
            private const int DailyChallengeRewardMultipleFallback = 2;

            // 是否展示更新公告
            public const string ShowVersionBoardKey = "showVersionBoard";

            #region Activity
            /// <summary>
            /// 是否开启 Postcard 活动
            /// </summary>
            public const string IsOpenPostcardKey = "isOpenPostcard";

            #endregion

            private static GlobalConst globalConst;

            /// <summary>
            /// 记录某个 remote key 对应的原始 JSON。
            /// 同步写入 DB.GameData.RemoteConfigCache 并保存，便于下次启动使用。
            /// </summary>
            public static void SetRaw(string remoteKey, string json)
            {
                if (string.IsNullOrEmpty(remoteKey))
                {
                    return;
                }

                if (string.IsNullOrEmpty(json))
                {
                    PersistRemoteConfigCache(remoteKey, remove: true);
                    return;
                }

                PersistRemoteConfigCache(remoteKey, json);
            }
            // Batch write remote JSON entries and persist once.
            public static void SetRawBatch(IReadOnlyDictionary<string, string> entries)
            {
                TrySetRawBatch(entries);
            }

            /// <summary>
            /// 在主线程批量提交配置并返回保存结果；相同内容不重复保存。
            /// 保存失败恢复旧缓存引用，仅回滚配置，不改动其它 GameData 字段。
            /// </summary>
            public static bool TrySetRawBatch(IReadOnlyDictionary<string, string> entries)
            {
                if (entries == null || entries.Count == 0)
                {
                    return true;
                }

                if (DB.GameData == null)
                {
                    Debug.LogWarning("[RemoteConfig] Cache commit skipped: GameData is not initialized.");
                    return false;
                }

                var gameData = DB.GameData;
                var previousCache = gameData.RemoteConfigCache;
                var cache = previousCache == null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(previousCache);

                var hasChanges = false;
                foreach (var entry in entries)
                {
                    var remoteKey = entry.Key;
                    if (string.IsNullOrEmpty(remoteKey))
                    {
                        continue;
                    }

                    var json = entry.Value;
                    if (string.IsNullOrEmpty(json))
                    {
                        hasChanges |= cache.Remove(remoteKey);
                        continue;
                    }

                    if (!cache.TryGetValue(remoteKey, out var existingJson) || existingJson != json)
                    {
                        cache[remoteKey] = json;
                        hasChanges = true;
                    }
                }

                if (!hasChanges)
                {
                    return true;
                }

                gameData.RemoteConfigCache = cache;
                try
                {
                    if (gameData.Save()) return true;
                    Debug.LogError("[RemoteConfig] Cache save returned false; previous cache retained.");
                }
                catch (System.Exception exception)
                {
                    Debug.LogError($"[RemoteConfig] Cache save failed: {exception.Message}");
                }

                gameData.RemoteConfigCache = previousCache;
                return false;
            }

            private static void PersistRemoteConfigCache(string remoteKey, string json = null, bool remove = false)
            {
                if (DB.GameData == null)
                {
                    return;
                }

                var cache = DB.GameData.RemoteConfigCache;
                if (cache == null)
                {
                    cache = new Dictionary<string, string>();
                    DB.GameData.RemoteConfigCache = cache;
                }

                if (remove)
                {
                    cache.Remove(remoteKey);
                }
                else
                {
                    cache[remoteKey] = json;
                }

                DB.GameData.Save();
            }

            /// <summary>
            /// 获取某个 remote key 的原始 JSON（来自 DB.GameData.RemoteConfigCache）。
            /// </summary>
            public static bool TryGetRaw(string remoteKey, out string json)
            {
                json = null;
                if (string.IsNullOrEmpty(remoteKey))
                {
                    return false;
                }

                var cache = DB.GameData?.RemoteConfigCache;
                if (cache == null || !cache.TryGetValue(remoteKey, out json) || string.IsNullOrEmpty(json))
                {
                    json = null;
                    return false;
                }

                return true;
            }

            /// <summary>
            /// 使用 JsonUtils 从指定的 remote key JSON 中解析字段。
            /// </summary>
            public static bool TryGetValue<T>(string remoteKey, string fieldKey, out T value)
            {
                value = default;

                if (string.IsNullOrEmpty(fieldKey))
                {
                    return false;
                }

                if (!TryGetRaw(remoteKey, out var json))
                {
                    return false;
                }

                return JsonUtils.TryGetValue<T>(json, fieldKey, out value);
            }

            private static bool TryGetGlobalConst(out GlobalConst value)
            {
                if (globalConst != null)
                {
                    value = globalConst;
                    return true;
                }

                var manager = ConfigManager.Instance;
                if (manager == null || !manager.IsInitialized || manager.Tables == null)
                {
                    value = null;
                    return false;
                }

                var list = manager.Tables.TbGlobalConst?.DataList;
                if (list == null || list.Count == 0)
                {
                    value = null;
                    return false;
                }

                globalConst = list[0];
                value = globalConst;
                return true;
            }

            /// <summary>
            /// 从指定玩法 RemoteConfig 的关卡区间配置中解析当前关卡对应值。
            /// 调用边界：仅允许运行态广告配置入口调用，字段缺失或数组非法时返回 false。
            /// 注意事项：LevelRange 按升序的包含上界解释，超过最后上界时沿用最后一档。
            /// </summary>
            private static bool TryGetRemoteLevelRangeValue(
                string remoteKey,
                string levelRangeFieldKey,
                string rangeValueFieldKey,
                int currentLevel,
                out int value)
            {
                value = 0;
                return TryGetValue<int[]>(remoteKey, levelRangeFieldKey, out int[] levelRanges)
                    && TryGetValue<int[]>(remoteKey, rangeValueFieldKey, out int[] rangeValues)
                    && TryResolveLevelRangeValue(currentLevel, levelRanges, rangeValues, out value);
            }

            /// <summary>
            /// 根据关卡区间上界解析同索引配置值。
            /// 调用边界：供主线、DrawGame、IWannerGame 插屏区间配置共同使用。
            /// 注意事项：两数组必须非空、等长且上界严格递增，否则拒绝该组配置。
            /// </summary>
            private static bool TryResolveLevelRangeValue(
                int currentLevel,
                IReadOnlyList<int> levelRanges,
                IReadOnlyList<int> rangeValues,
                out int value)
            {
                value = 0;
                if (levelRanges == null
                    || rangeValues == null
                    || levelRanges.Count == 0
                    || levelRanges.Count != rangeValues.Count)
                {
                    return false;
                }

                for (int index = 0; index < levelRanges.Count; index++)
                {
                    if (index > 0 && levelRanges[index] <= levelRanges[index - 1])
                        return false;

                    if (currentLevel <= levelRanges[index])
                    {
                        value = rangeValues[index];
                        return true;
                    }
                }

                value = rangeValues[rangeValues.Count - 1];
                return true;
            }

            public static string GetGlobalTest()
            {
                return TryGetGlobalConst(out var value) ? value.Test : string.Empty;
            }

            /*          
                        // 1.5.0 obsoleted
                        public static int GetGlobalTipCostCount()
                        {
                            return TryGetGlobalConst(out var value) ? value.TipCostCount : 0;
                        } */

            public static int GetShowTipsFailCount()
            {
                if (TryGetValue<int>(LevelConfigKey, ShowTipsFailCountFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.ShowTipsFailCount : 0;
            }

            public static float GetShowTipsTimeLimit()
            {
                if (TryGetValue<float>(LevelConfigKey, ShowTipsTimeLimitFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.ShowTipsTimeLimit : 0f;
            }

            public static int GetAdInterstitialCD()
            {
                if (TryGetValue<int>(LevelConfigKey, AdInterstitialCDFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.AdInterstitialCD : 0;
            }

            public static int GetAdRewardResetInterstitialCD()
            {
                if (TryGetValue<int>(LevelConfigKey, AdRewardResetInterstitialCDFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.AdRewardResetInterstitialCD : 0;
            }

            public static int GetAdInterstitialShowMinLevel()
            {
                if (TryGetValue<int>(LevelConfigKey, AdInterstitialShowMinLevelFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.AdInterstitialShowMinLevel : 0;
            }

            /// <summary>
            /// 用途：获取主线当前关卡对应区间的胜利插屏间隔数。
            /// 调用边界：仅供运行时主线插屏资格判断读取。
            /// 注意事项：依次读取远端区间、远端旧固定值、本地区间和本地旧固定值，保证新旧配置双向兼容。
            /// </summary>
            public static int GetAdInterstitialShowIntervalCount(int currentLevel)
            {
                if (!Application.isPlaying)
                    return 0;

                if (TryGetRemoteLevelRangeValue(
                        LevelConfigKey,
                        MainGameInterstitialAdLevelRangeFieldKey,
                        MainGameInterstitialAdRangeLevelCountFieldKey,
                        currentLevel,
                        out int rangeValue))
                {
                    return rangeValue;
                }

                if (TryGetValue<int>(LevelConfigKey, AdInterstitialShowIntervalCountFieldKey, out int remoteValue))
                    return remoteValue;

                bool hasGlobalConst = TryGetGlobalConst(out GlobalConst value);
                if (hasGlobalConst
                    && TryResolveLevelRangeValue(
                        currentLevel,
                        value.MainGameInterstitalAdLevelRange,
                        value.MainGameInterstitalAdRangeLevelCount,
                        out rangeValue))
                {
                    return rangeValue;
                }

                return hasGlobalConst ? value.AdInterstitialShowIntervalCount : 0;
            }

            /// <summary>
            /// 用途：获取主线当前关卡对应区间的失败或重置插屏门槛。
            /// 调用边界：仅供运行时主线失败插屏资格判断读取。
            /// 注意事项：依次读取远端区间、远端旧固定值、本地区间和本地旧固定值，保证新旧配置双向兼容。
            /// </summary>
            public static int GetAdInterstitialShowFailedCount(int currentLevel)
            {
                if (!Application.isPlaying)
                    return 0;

                if (TryGetRemoteLevelRangeValue(
                        LevelConfigKey,
                        MainGameInterstitialAdLevelRangeFieldKey,
                        MainGameInterstitialAdRangeFailedCountFieldKey,
                        currentLevel,
                        out int rangeValue))
                {
                    return rangeValue;
                }

                if (TryGetValue<int>(LevelConfigKey, AdInterstitialShowFailedCountFieldKey, out int remoteValue))
                    return remoteValue;

                bool hasGlobalConst = TryGetGlobalConst(out GlobalConst value);
                if (hasGlobalConst
                    && TryResolveLevelRangeValue(
                        currentLevel,
                        value.MainGameInterstitalAdLevelRange,
                        value.MianGameInterstititalAdRangeFailedCount,
                        out rangeValue))
                {
                    return rangeValue;
                }

                return hasGlobalConst ? value.AdInterstitialShowFailedCount : 0;
            }

            /// <summary>
            /// 获取 DrawGame 功能开关。
            /// 调用边界：运行时优先读取 drawgame_config，缺失时回退到 GlobalConst。
            /// </summary>
            public static bool GetDrawGameIsOpen()
            {
                if (TryGetValue<bool>(DrawGameConfigKey, DrawGameIsOpenFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) && value.DrawGameIsOpen;
            }

            /// <summary>
            /// 获取 DrawGame 失败后展示提示的累计次数。
            /// RemoteConfig 缺失时回退到 GlobalConst。
            /// </summary>
            public static int GetDrawGameShowTipsFailCount()
            {
                if (TryGetValue<int>(DrawGameConfigKey, DrawGameShowTipsFailCountFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DrawGameShowTipsFailCount : 0;
            }

            /// <summary>
            /// 获取 DrawGame 单次等待后展示提示的秒数。
            /// RemoteConfig 缺失时回退到 GlobalConst。
            /// </summary>
            public static float GetDrawGameShowTipsTimeLimit()
            {
                if (TryGetValue<float>(DrawGameConfigKey, DrawGameShowTipsTimeLimitFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DrawGameShowTipsTimeLimit : 0f;
            }

            /// <summary>
            /// 获取 DrawGame 插屏展示冷却秒数。
            /// RemoteConfig 缺失时回退到 GlobalConst。
            /// </summary>
            public static int GetDrawGameInterstitialCD()
            {
                if (TryGetValue<int>(DrawGameConfigKey, DrawGameInterstitialCDFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DrawGameInterstitialCD : 0;
            }

            /// <summary>
            /// 获取 DrawGame 激励视频后的插屏冷却秒数。
            /// RemoteConfig 缺失时回退到 GlobalConst。
            /// </summary>
            public static int GetDrawGameRewardResetInterstitialCD()
            {
                if (TryGetValue<int>(DrawGameConfigKey, DrawGameRewardResetInterstitialCDFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DrawGameRewardResetInterstitialCD : 0;
            }

            /// <summary>
            /// 获取 DrawGame 允许展示插屏的最小关卡序号。
            /// RemoteConfig 缺失时回退到 GlobalConst。
            /// </summary>
            public static int GetDrawGameInterstitialShowMinLevel()
            {
                if (TryGetValue<int>(DrawGameConfigKey, DrawGameInterstitialShowMinLevelFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DrawGameInterstitialShowMinLevel : 0;
            }

            /// <summary>
            /// 用途：获取 DrawGame 当前关卡对应区间的胜利插屏间隔数。
            /// 调用边界：仅供运行时 DrawGame 插屏资格判断读取。
            /// 注意事项：依次读取远端区间、远端旧固定值、本地区间和本地旧固定值，保证新旧配置双向兼容。
            /// </summary>
            public static int GetDrawGameInterstitialShowIntervalCount(int currentLevel)
            {
                if (!Application.isPlaying)
                    return 0;

                if (TryGetRemoteLevelRangeValue(
                        DrawGameConfigKey,
                        DrawGameInterstitialAdLevelRangeFieldKey,
                        DrawGameInterstitialAdRangeLevelCountFieldKey,
                        currentLevel,
                        out int rangeValue))
                {
                    return rangeValue;
                }

                if (TryGetValue<int>(
                        DrawGameConfigKey,
                        DrawGameInterstitialShowIntervalCountFieldKey,
                        out int remoteValue))
                {
                    return remoteValue;
                }

                bool hasGlobalConst = TryGetGlobalConst(out GlobalConst value);
                if (hasGlobalConst
                    && TryResolveLevelRangeValue(
                        currentLevel,
                        value.DrawGameInterstitalAdLevelRange,
                        value.DrawGameInterstitalAdRangeLevelCount,
                        out rangeValue))
                {
                    return rangeValue;
                }

                return hasGlobalConst ? value.DrawGameInterstitialShowIntervalCount : 0;
            }

            /// <summary>
            /// 用途：获取 DrawGame 当前关卡对应区间的失败或重置插屏门槛。
            /// 调用边界：仅供运行时 DrawGame 失败插屏资格判断读取。
            /// 注意事项：依次读取远端区间、远端旧固定值、本地区间和本地旧固定值，保证新旧配置双向兼容。
            /// </summary>
            public static int GetDrawGameInterstitialShowFailedCount(int currentLevel)
            {
                if (!Application.isPlaying)
                    return 0;

                if (TryGetRemoteLevelRangeValue(
                        DrawGameConfigKey,
                        DrawGameInterstitialAdLevelRangeFieldKey,
                        DrawGameInterstitialAdRangeFailedCountFieldKey,
                        currentLevel,
                        out int rangeValue))
                {
                    return rangeValue;
                }

                if (TryGetValue<int>(
                        DrawGameConfigKey,
                        DrawGameInterstitialShowFailedCountFieldKey,
                        out int remoteValue))
                {
                    return remoteValue;
                }

                bool hasGlobalConst = TryGetGlobalConst(out GlobalConst value);
                if (hasGlobalConst
                    && TryResolveLevelRangeValue(
                        currentLevel,
                        value.DrawGameInterstitalAdLevelRange,
                        value.DrawGameInterstititalAdRangeFailedCount,
                        out rangeValue))
                {
                    return rangeValue;
                }

                return hasGlobalConst ? value.DrawGameInterstitialShowFailedCount : 0;
            }

            /// <summary>
            /// 获取 DrawGame 每日主动展示免广告弹窗的次数上限。
            /// RemoteConfig 缺失时回退到 GlobalConst。
            /// </summary>
            public static int GetDrawGameDailyNOAdPopupAutoShowCount()
            {
                if (TryGetValue<int>(DrawGameConfigKey, DrawGameDailyNOAdPopupAutoShowCountFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value)
                    ? value.DrawGameDailyNOAdPopupAutoShowCount
                    : 0;
            }

            /// <summary>
            /// 获取 DrawGame 首次获取场景提示的金币价格。
            /// 调用边界：仅供 Draw 专用提示弹窗读取；远端字段缺失时回退 GlobalConst。
            /// </summary>
            public static int GetDrawGameGameTipsPopupCoinCost()
            {
                if (TryGetValue<int>(
                        DrawGameConfigKey,
                        DrawGameGameTipsPopupCoinCostFieldKey,
                        out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value)
                    ? value.DrawGameGameTipsPopupCoinCost
                    : 0;
            }

            /// <summary>
            /// 获取 DrawGame 已显示提示后直接跳关的金币价格。
            /// 调用边界：仅供 Draw 专用跳关弹窗读取；远端字段缺失时回退 GlobalConst。
            /// </summary>
            public static int GetDrawGameGameTipsPopupPassLevelCoinCost()
            {
                if (TryGetValue<int>(
                        DrawGameConfigKey,
                        DrawGameGameTipsPopupPassLevelCoinCostFieldKey,
                        out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value)
                    ? value.DrawGameGameTipsPopupPassLevelCoinCost
                    : 0;
            }

            /// <summary>
            /// 获取 DigGame 功能开关。
            /// 调用边界：运行时优先读取 diggame_config，缺失时回退到 GlobalConst。
            /// </summary>
            public static bool GetDigGameIsOpen()
            {
                if (TryGetValue<bool>(DigGameConfigKey, DigGameIsOpenFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) && value.DigGameIsOpen;
            }

            /// <summary>
            /// 获取 DigGame 失败后展示提示的累计次数。
            /// RemoteConfig 缺失时回退到 GlobalConst。
            /// </summary>
            public static int GetDigGameShowTipsFailCount()
            {
                if (TryGetValue<int>(DigGameConfigKey, DigGameShowTipsFailCountFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DigGameShowTipsFailCount : 0;
            }

            /// <summary>
            /// 获取 DigGame 单次等待后展示提示的秒数。
            /// RemoteConfig 缺失时回退到 GlobalConst。
            /// </summary>
            public static float GetDigGameShowTipsTimeLimit()
            {
                if (TryGetValue<float>(DigGameConfigKey, DigGameShowTipsTimeLimitFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DigGameShowTipsTimeLimit : 0f;
            }

            /// <summary>
            /// 获取 DigGame 插屏展示冷却秒数。
            /// RemoteConfig 缺失时回退到 GlobalConst。
            /// </summary>
            public static int GetDigGameInterstitialCD()
            {
                if (TryGetValue<int>(DigGameConfigKey, DigGameInterstitialCDFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DigGameInterstitialCD : 0;
            }

            /// <summary>
            /// 获取 DigGame 激励视频后的插屏冷却秒数。
            /// RemoteConfig 缺失时回退到 GlobalConst。
            /// </summary>
            public static int GetDigGameRewardResetInterstitialCD()
            {
                if (TryGetValue<int>(DigGameConfigKey, DigGameRewardResetInterstitialCDFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DigGameRewardResetInterstitialCD : 0;
            }

            /// <summary>
            /// 获取 DigGame 允许展示插屏的最小关卡序号。
            /// RemoteConfig 缺失时回退到 GlobalConst。
            /// </summary>
            public static int GetDigGameInterstitialShowMinLevel()
            {
                if (TryGetValue<int>(DigGameConfigKey, DigGameInterstitialShowMinLevelFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DigGameInterstitialShowMinLevel : 0;
            }

            /// <summary>
            /// 用途：获取 DigGame 当前关卡对应区间的胜利插屏间隔数。
            /// 调用边界：仅供运行时 DigGame 插屏资格判断读取。
            /// 注意事项：依次读取远端区间、远端旧固定值、本地区间和本地旧固定值，保证新旧配置双向兼容。
            /// </summary>
            public static int GetDigGameInterstitialShowIntervalCount(int currentLevel)
            {
                if (!Application.isPlaying)
                    return 0;

                if (TryGetRemoteLevelRangeValue(
                        DigGameConfigKey,
                        DigGameInterstitialAdLevelRangeFieldKey,
                        DigGameInterstitialAdRangeLevelCountFieldKey,
                        currentLevel,
                        out int rangeValue))
                {
                    return rangeValue;
                }

                if (TryGetValue<int>(
                        DigGameConfigKey,
                        DigGameInterstitialShowIntervalCountFieldKey,
                        out int remoteValue))
                {
                    return remoteValue;
                }

                bool hasGlobalConst = TryGetGlobalConst(out GlobalConst value);
                if (hasGlobalConst
                    && TryResolveLevelRangeValue(
                        currentLevel,
                        value.DigGameInterstitalAdLevelRange,
                        value.DigGameInterstitalAdRangeLevelCount,
                        out rangeValue))
                {
                    return rangeValue;
                }

                return hasGlobalConst ? value.DigGameInterstitialShowIntervalCount : 0;
            }

            /// <summary>
            /// 用途：获取 DigGame 当前关卡对应区间的失败或重置插屏门槛。
            /// 调用边界：仅供运行时 DigGame 失败插屏资格判断读取。
            /// 注意事项：依次读取远端区间、远端旧固定值、本地区间和本地旧固定值，保证新旧配置双向兼容。
            /// </summary>
            public static int GetDigGameInterstitialShowFailedCount(int currentLevel)
            {
                if (!Application.isPlaying)
                    return 0;

                if (TryGetRemoteLevelRangeValue(
                        DigGameConfigKey,
                        DigGameInterstitialAdLevelRangeFieldKey,
                        DigGameInterstitialAdRangeFailedCountFieldKey,
                        currentLevel,
                        out int rangeValue))
                {
                    return rangeValue;
                }

                if (TryGetValue<int>(
                        DigGameConfigKey,
                        DigGameInterstitialShowFailedCountFieldKey,
                        out int remoteValue))
                {
                    return remoteValue;
                }

                bool hasGlobalConst = TryGetGlobalConst(out GlobalConst value);
                if (hasGlobalConst
                    && TryResolveLevelRangeValue(
                        currentLevel,
                        value.DigGameInterstitalAdLevelRange,
                        value.DigGameInterstititalAdRangeFailedCount,
                        out rangeValue))
                {
                    return rangeValue;
                }

                return hasGlobalConst ? value.DigGameInterstitialShowFailedCount : 0;
            }

            /// <summary>
            /// 获取 DigGame 每日主动展示免广告弹窗的次数上限。
            /// RemoteConfig 缺失时回退到 GlobalConst。
            /// </summary>
            public static int GetDigGameDailyNOAdPopupAutoShowCount()
            {
                if (TryGetValue<int>(DigGameConfigKey, DigGameDailyNOAdPopupAutoShowCountFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value)
                    ? value.DigGameDailyNOAdPopupAutoShowCount
                    : 0;
            }

            /// <summary>
            /// 获取 DigGame 首次获取场景提示的金币价格。
            /// 调用边界：仅供 Dig 专用提示弹窗读取；远端字段缺失时回退 GlobalConst。
            /// </summary>
            public static int GetDigGameGameTipsPopupCoinCost()
            {
                if (TryGetValue<int>(
                        DigGameConfigKey,
                        DigGameGameTipsPopupCoinCostFieldKey,
                        out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value)
                    ? value.DigGameGameTipsPopupCoinCost
                    : 0;
            }

            /// <summary>
            /// 获取 DigGame 已显示提示后直接跳关的金币价格。
            /// 调用边界：仅供 Dig 专用跳关弹窗读取；远端字段缺失时回退 GlobalConst。
            /// </summary>
            public static int GetDigGameGameTipsPopupPassLevelCoinCost()
            {
                if (TryGetValue<int>(
                        DigGameConfigKey,
                        DigGameGameTipsPopupPassLevelCoinCostFieldKey,
                        out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value)
                    ? value.DigGameGameTipsPopupPassLevelCoinCost
                    : 0;
            }

            /// <summary>
            /// 用途：获取 IWannerGame 入口是否开启。
            /// 调用边界：仅供运行时 IWanner 入口与玩法流程读取。
            /// 注意事项：优先读取 iwannergame_config，缺失时回退 GlobalConst，配置均不可用时返回 false。
            /// </summary>
            public static bool GetIWannerGameIsOpen()
            {
                if (Application.isPlaying
                    && TryGetValue<bool>(IWannerGameConfigKey, IWannerGameIsOpenFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) && value.IWannerGameIsOpen;
            }

            /// <summary>
            /// 用途：获取 IWannerGame 失败后显示 Skip 的累计次数。
            /// 调用边界：仅供运行时 IWanner 失败与 Skip 流程读取。
            /// 注意事项：优先读取 iwannergame_config，缺失时回退 GlobalConst，配置均不可用时返回 0。
            /// </summary>
            public static int GetIWannerGameShowSkipFailCount()
            {
                if (Application.isPlaying
                    && TryGetValue<int>(IWannerGameConfigKey, IWannerGameShowSkipFailCountFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.IWannerGameShowSkipFailCount : 0;
            }

            /// <summary>
            /// 用途：获取 IWannerGame 插屏展示冷却秒数。
            /// 调用边界：仅供运行时 IWanner 广告流程读取。
            /// 注意事项：优先读取 iwannergame_config，缺失时回退 GlobalConst，配置均不可用时返回 0。
            /// </summary>
            public static int GetIWannerGameInterstitialCD()
            {
                if (Application.isPlaying
                    && TryGetValue<int>(IWannerGameConfigKey, IWannerGameInterstitialCDFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.IWannerGameInterstitialCD : 0;
            }

            /// <summary>
            /// 用途：获取 IWannerGame 激励视频后的插屏冷却秒数。
            /// 调用边界：仅供运行时 IWanner 广告流程读取。
            /// 注意事项：优先读取 iwannergame_config，缺失时回退 GlobalConst，配置均不可用时返回 0。
            /// </summary>
            public static int GetIWannerGameRewardResetInterstitialCD()
            {
                if (Application.isPlaying
                    && TryGetValue<int>(IWannerGameConfigKey, IWannerGameRewardResetInterstitialCDFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.IWannerGameRewardResetInterstitialCD : 0;
            }

            /// <summary>
            /// 用途：获取 IWannerGame 允许展示插屏的最小 Group 序号。
            /// 调用边界：仅供运行时 IWanner 广告资格判断读取。
            /// 注意事项：优先读取 iwannergame_config，缺失时回退 GlobalConst，配置均不可用时返回 0。
            /// </summary>
            public static int GetIWannerGameInterstitialShowMinGroup()
            {
                if (Application.isPlaying
                    && TryGetValue<int>(IWannerGameConfigKey, IWannerGameInterstitialShowMinGroupFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.IWannerGameInterstitialShowMinGroup : 0;
            }

            /// <summary>
            /// 用途：获取 IWannerGame 当前 Group 序号对应区间的胜利插屏间隔数。
            /// 调用边界：仅供运行时 IWanner 插屏资格判断读取。
            /// 注意事项：依次读取远端区间、远端旧固定值、本地区间和本地旧固定值，保证新旧配置双向兼容。
            /// </summary>
            public static int GetIWannerGameInterstitialShowIntervalCount(int currentLevel)
            {
                if (!Application.isPlaying)
                    return 0;

                if (TryGetRemoteLevelRangeValue(
                        IWannerGameConfigKey,
                        IWannerGameInterstitialAdLevelRangeFieldKey,
                        IWannerGameInterstitialAdRangeLevelCountFieldKey,
                        currentLevel,
                        out int rangeValue))
                {
                    return rangeValue;
                }

                if (TryGetValue<int>(
                        IWannerGameConfigKey,
                        IWannerGameInterstitialShowIntervalCountFieldKey,
                        out int remoteValue))
                {
                    return remoteValue;
                }

                bool hasGlobalConst = TryGetGlobalConst(out GlobalConst value);
                if (hasGlobalConst
                    && TryResolveLevelRangeValue(
                        currentLevel,
                        value.IwannarGameInterstitalAdLevelRange,
                        value.IwannarGameInterstitalAdRangeLevelCount,
                        out rangeValue))
                {
                    return rangeValue;
                }

                return hasGlobalConst ? value.IWannerGameInterstitialShowIntervalCount : 0;
            }

            /// <summary>
            /// 用途：获取 IWannerGame 当前 Group 序号对应区间的失败或重置插屏门槛。
            /// 调用边界：仅供运行时 IWanner 失败插屏资格判断读取。
            /// 注意事项：依次读取远端区间、远端旧固定值、本地区间和本地旧固定值，保证新旧配置双向兼容。
            /// </summary>
            public static int GetIWannerGameInterstitialShowFailedCount(int currentLevel)
            {
                if (!Application.isPlaying)
                    return 0;

                if (TryGetRemoteLevelRangeValue(
                        IWannerGameConfigKey,
                        IWannerGameInterstitialAdLevelRangeFieldKey,
                        IWannerGameInterstitialAdRangeFailedCountFieldKey,
                        currentLevel,
                        out int rangeValue))
                {
                    return rangeValue;
                }

                if (TryGetValue<int>(
                        IWannerGameConfigKey,
                        IWannerGameInterstitialShowFailedCountFieldKey,
                        out int remoteValue))
                {
                    return remoteValue;
                }

                bool hasGlobalConst = TryGetGlobalConst(out GlobalConst value);
                if (hasGlobalConst
                    && TryResolveLevelRangeValue(
                        currentLevel,
                        value.IwannarGameInterstitalAdLevelRange,
                        value.IwannarGameInterstititalAdRangeFailedCount,
                        out rangeValue))
                {
                    return rangeValue;
                }

                return hasGlobalConst ? value.IWannerGameInterstitialShowFailedCount : 0;
            }

            /// <summary>
            /// 用途：获取 IWannerGame 普通小关通过次数触发插屏的门槛。
            /// 调用边界：仅供运行时 IWanner 胜利插屏资格判断读取。
            /// 注意事项：优先读取 iwannergame_config，缺失时回退 GlobalConst；-1 表示关闭该条件。
            /// </summary>
            public static int GetIWannerGameInterstitialShowPassedLevelCount()
            {
                if (Application.isPlaying
                    && TryGetValue<int>(
                        IWannerGameConfigKey,
                        IWannerGameInterstitialShowPassedLevelCountFieldKey,
                        out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value)
                    ? value.IWannerGameInterstitialShowPassedLevelCount
                    : -1;
            }

            /// <summary>
            /// 用途：获取 IWannerGame 每日主动展示免广告弹窗的次数上限。
            /// 调用边界：仅供运行时 IWanner 广告流程读取。
            /// 注意事项：优先读取 iwannergame_config，缺失时回退 GlobalConst，配置均不可用时返回 0。
            /// </summary>
            public static int GetIWannerGameDailyNOAdPopupAutoShowCount()
            {
                if (Application.isPlaying
                    && TryGetValue<int>(IWannerGameConfigKey, IWannerGameDailyNOAdPopupAutoShowCountFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value)
                    ? value.IWannerGameDailyNOAdPopupAutoShowCount
                    : 0;
            }

            public static int GetShowRatingLevel()
            {
                if (TryGetValue<int>(LevelConfigKey, ShowRatingLevelKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.ShowRatingLevel : 0;
            }

            /// <summary>
            /// 获取每日挑战功能开关。
            /// 调用边界：运行时优先使用 RemoteConfig，无可用远程值时回退 GlobalConst。
            /// </summary>
            public static bool GetDailyChallengeIsOpen()
            {
                if (Application.isPlaying
                    && TryGetValue<bool>(LevelConfigKey, DailyChallengeIsOpenKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) && value.DailyChallengeIsOpen;
            }

            /// <summary>
            /// 获取每日宝箱开关。
            /// 调用边界：编辑态不访问运行时配置；运行时优先读取 level_config，缺字段或类型不符时回退 Luban。
            /// 注意事项：Luban 配置未就绪时保持原有开启行为。
            /// </summary>
            public static bool GetIsOpenDailyChest()
            {
                if (!Application.isPlaying)
                    return true;

                if (TryGetValue<JToken>(LevelConfigKey, IsOpenDailyChestFieldKey, out var remoteValue)
                    && remoteValue?.Type == JTokenType.Boolean)
                {
                    return remoteValue.Value<bool>();
                }

                return !TryGetGlobalConst(out var value) || value.IsOpenDailyChest;
            }

            /// <summary>
            /// 获取新玩家首日自动弹出每日宝箱的主线关卡序号。
            /// 调用边界：运行时优先读取 level_config，缺字段或解析失败时回退 Luban。
            /// 注意事项：业务消费者将小于等于 0 的值视为关闭自动弹出。
            /// </summary>
            public static int GetAutoShowChestGiftLevel()
            {
                if (Application.isPlaying
                    && TryGetValue<JToken>(LevelConfigKey, AutoShowChestGiftLevelFieldKey, out var remoteToken)
                    && remoteToken?.Type == JTokenType.Integer
                    && TryGetValue<int>(LevelConfigKey, AutoShowChestGiftLevelFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.AutoShowChestGiftLevel : -1;
            }

            /// <summary>
            /// 获取每日挑战累计奖励节点。
            /// 调用边界：运行时优先使用 RemoteConfig，UI 不应直接读取该数组。
            /// 注意事项：返回新数组，Manager 仍需校验节点数量和递增关系。
            /// </summary>
            public static int[] GetDailyChallengeSteps()
            {
                if (Application.isPlaying
                    && TryGetValue<int[]>(LevelConfigKey, DailyChallengeStepsKey, out var remoteValue)
                    && remoteValue != null
                    && remoteValue.Length > 0)
                {
                    return remoteValue;
                }

                if (TryGetGlobalConst(out var value)
                    && value.DailyChallengeSteps != null
                    && value.DailyChallengeSteps.Count > 0)
                {
                    return value.DailyChallengeSteps.ToArray();
                }

                return new int[0];
            }

            /// <summary>
            /// 获取每日挑战的解锁关卡。
            /// 调用边界：运行时优先使用 RemoteConfig，由 DailyChallengeManager 统一判断解锁。
            /// </summary>
            public static int GetDailyChallengeUnlockLevel()
            {
                if (Application.isPlaying
                    && TryGetValue<int>(LevelConfigKey, DailyChallengeUnlockLevelKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DailyChallengeUnlockLevel : int.MaxValue;
            }

            /// <summary>
            /// 获取每日挑战最终阶段的随机 Skin 奖励池 id。
            /// 调用边界：由 DailyChallengeManager 在最终阶段领奖时读取。
            /// 注意事项：运行时优先使用 RemoteConfig，缺失时回退 GlobalConst；非正数表示不提供随机 Skin。
            /// </summary>
            public static int GetDailyChallengeRandomSkinPoolId()
            {
                if (Application.isPlaying
                    && TryGetValue<int>(LevelConfigKey, DailyChallengeRandomSkinPoolIdKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DailyChallengeRandomSkinPoolId : 0;
            }

            /// <summary>
            /// 获取每日挑战数量奖励的广告倍率。
            /// 调用边界：由 DailyChallengeManager 创建阶段奖励数据时读取。
            /// 注意事项：运行时优先使用 RemoteConfig，缺失时回退 GlobalConst；小于 1 的配置按 1 处理，配置尚未加载时回退为 2。
            /// </summary>
            public static int GetDailyChallengeRewardMultiple()
            {
                if (Application.isPlaying
                    && TryGetValue<int>(LevelConfigKey, DailyChallengeRewardMultipleKey, out var remoteValue))
                {
                    return Mathf.Max(1, remoteValue);
                }

                return TryGetGlobalConst(out var value)
                    ? Mathf.Max(1, value.DailyChallengeRewardMultiple)
                    : DailyChallengeRewardMultipleFallback;
            }

            /// <summary>
            /// 获取每日挑战第一阶段金币奖励。
            /// 调用边界：由 DailyChallengeManager 在第一阶段领奖时读取。
            /// 注意事项：运行时优先使用 RemoteConfig，缺失时回退 GlobalConst。
            /// </summary>
            public static int GetDailyChallengeStep1CoinReward()
            {
                if (Application.isPlaying
                    && TryGetValue<int>(LevelConfigKey, DailyChallengeStep1CoinRewardKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DailyChallengeStep1CoinReward : 0;
            }

            /// <summary>
            /// 获取每日挑战第二阶段金币奖励。
            /// 调用边界：由 DailyChallengeManager 在第二阶段领奖时读取。
            /// 注意事项：运行时优先使用 RemoteConfig，缺失时回退 GlobalConst。
            /// </summary>
            public static int GetDailyChallengeStep2CoinReward()
            {
                if (Application.isPlaying
                    && TryGetValue<int>(LevelConfigKey, DailyChallengeStep2CoinRewardKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DailyChallengeStep2CoinReward : 0;
            }

            public static bool GetOpenGaps()
            {
                if (TryGetValue<bool>(LevelConfigKey, OpenGAPS, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) && value.OpenGaps;
            }

            public static int[] GetVictoryHorizantalRewards()
            {
                // 编辑器非运行态不要读 DB.GameData.RemoteConfigCache。
                // 否则 prefab 预览 / OnValidate / PreviewImporter 会把 DBManager 在 edit mode 顶出来。
                if (Application.isPlaying
                    && TryGetValue<int[]>(LevelConfigKey, VictoryHorizantalRewardsFieldKey, out var remoteValue)
                    && remoteValue != null
                    && remoteValue.Length > 0)
                {
                    return remoteValue;
                }

                if (TryGetGlobalConst(out var value)
                    && value.VictoryHorizantalRewards != null
                    && value.VictoryHorizantalRewards.Count > 0)
                {
                    return value.VictoryHorizantalRewards.ToArray();
                }

                return new[] { 1 };
            }

            public static int GetGameTipsPopupCoinCost()
            {
                if (TryGetValue<int>(LevelConfigKey, GameTipsPopupCoinCostFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.GameTipsPopupCoinCost : 0;
            }

            public static int GetGameTipsPopupPassLevelCoinCost()
            {
                if (TryGetValue<int>(LevelConfigKey, GameTipsPopupPassLevelCoinCostFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.GameTipsPopupPassLevelCoinCost : 0;
            }

            public static int GetAdDailyNOAdPopupAutoShowCount()
            {
                if (TryGetValue<int>(LevelConfigKey, AdDailyNOAdPopupAutoShowCountFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.AdDailyNOAdPopupAutoShowCount : 0;
            }

            public static int GetShopFreeCoinShowCount()
            {
                if (TryGetValue<int>(LevelConfigKey, ShopFreeCoinShowCountFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.ShopAdfreeCoinAutoShowCount : 0;
            }

            /// <summary>
            /// 获取 ShopPanel 每日免费金币数量。
            /// 调用边界：由每日金币卡片与红点共享规则在配置初始化后读取。
            /// 注意事项：优先读取 level_config，字段缺失或解析失败时回退 GlobalConst，配置均不可用时返回 0。
            /// </summary>
            public static int GetDailyFreeCoinCount()
            {
                if (TryGetValue<int>(LevelConfigKey, DailyFreeCoinCountFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DailyFreeCoinCount : 0;
            }

            public static int GetInitGameCoinReward()
            {
                if (TryGetValue<int>(LevelConfigKey, InitGameCoinRewardKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.InitGameCoinReward : 0;
            }


            /// <summary>
            /// 是否展示更新公告
            /// </summary>
            /// <returns></returns>
            public static bool GetVersionBoardEnabled()
            {
                if (TryGetValue<bool>(LevelConfigKey, ShowVersionBoardKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.ShowVersionBoard : false;
            }


            // 免费皮肤关卡
            public static int GetFreeSkinLevel()
            {
                if (TryGetValue<int>(LevelConfigKey, GetFreeSkinLevelKey, out var remoteValue))
                {
                    return remoteValue;
                }
                return TryGetGlobalConst(out var value) ? value.GetFreeSkinLevel : -1;
            }

            // 免费皮肤 id
            public static string GetFreeSkinId()
            {

                if (TryGetValue<string>(LevelConfigKey, FreeSkinIdKey, out var remoteValue))
                {
                    return remoteValue;
                }
                return TryGetGlobalConst(out var value) ? value.FreeSkinId : "";
            }



            public static int GetMediaShareCoinReward()
            {
                return TryGetGlobalConst(out var value) ? value.MediaShareRewardCoinCount : 5;
            }

            // 胜利分享
            public static int GetLevelVictoryShareCoinReward()
            {
                return TryGetGlobalConst(out var value) ? value.VictoryShareRewardCoinCount : 5;
            }

            // 是否自动弹出更新公告
            // 默认不弹出
            public static bool GetAutoPopupBulleinBoard()
            {
                if (TryGetValue<bool>(LevelConfigKey, AutoPopupBulleinBoardKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return false;
            }

            /// <summary>
            /// 是否开启明信片活动
            /// </summary>
            /// <returns></returns>
            public static bool IsOpenPostcard()
            {
                if (TryGetValue<bool>(LevelConfigKey, IsOpenPostcardKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.IsOpenPostcard : false;
            }

            /// <summary>
            /// 获取明信片引导功能开启关卡
            /// </summary>
            /// <returns></returns>
            public static int GetPostcardGuideLevel()
            {
                if (TryGetValue<int>(LevelConfigKey, PostcardGuideLevelKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.PostcardGuideLevel : 0;
            }

            /// <summary>
            /// 用途：获取首次提示剧情引导允许触发的最低关卡。
            /// 调用边界：由 GameTipsGuideFlow 在运行态资格判断时读取。
            /// 注意事项：优先读取 level_config，缺失时回退 GlobalConst，配置均不可用时返回 0。
            /// </summary>
            public static int GetGameTipsGuideLevel()
            {
                if (TryGetValue<int>(LevelConfigKey, GameTipsGuideLevelKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.GameTipsGuideLevel : 0;
            }

            /// <summary>
            /// 用途：获取首次提示剧情引导的重试次数门槛。
            /// 调用边界：由 GameTipsGuideFlow 在运行态资格判断时读取。
            /// 注意事项：优先读取 level_config，缺失时回退 GlobalConst，配置均不可用时返回 0。
            /// </summary>
            public static int GetGameTipsGuideFailedCount()
            {
                if (TryGetValue<int>(LevelConfigKey, GameTipsGuideFailedCountKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.GameTipsGuideFailedCount : 0;
            }

            /// <summary>
            /// 用途：获取首次提示剧情引导的关卡停留时间门槛，单位为秒。
            /// 调用边界：由 GameTipsGuideFlow 在运行态资格判断时读取。
            /// 注意事项：优先读取 level_config，缺失时回退 GlobalConst，配置均不可用时返回 0。
            /// </summary>
            public static float GetGameTipsGuideWaitingTime()
            {
                if (TryGetValue<float>(LevelConfigKey, GameTipsGuideWaitingTimeKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.GameTipsGuideWaitingTime : 0f;
            }
        }
    }
}


using System;using System.Collections.Generic;using UnityEngine;using Config.LubanConfig;namespace Game.ConfigModule { public partial class ConfigManager {public static partial class RemoteConfig {private static GlobalConst globalConst;            public const string DigGameConfigKey = "diggame_config";
            public const string DigGameIsOpenFieldKey = "digGameIsOpen";
            public const string DigGameShowTipsFailCountFieldKey = "digGameShowTipsFailCount";
            public const string DigGameShowTipsTimeLimitFieldKey = "digGameShowTipsTimeLimit";
            public const string DigGameInterstitialCDFieldKey = "digGame_interstitialCD";
            public const string DigGameRewardResetInterstitialCDFieldKey = "digGame_rewardResetInterstitialCD";
            public const string DigGameInterstitialShowMinLevelFieldKey = "digGame_interstitialShowMinLevel";
            public const string DigGameInterstitialShowIntervalCountFieldKey = "digGame_interstitialShowIntervalCount";
            public const string DigGameInterstitialShowFailedCountFieldKey = "digGame_interstitialShowFailedCount";
            public const string DigGameInterstitialAdLevelRangeFieldKey = "digGame_interstitalAdLevelRange";
            public const string DigGameInterstitialAdRangeLevelCountFieldKey = "digGame_interstitalAdRangeLevelCount";
            public const string DigGameInterstitialAdRangeFailedCountFieldKey = "digGame_interstititalAdRangeFailedCount";
            public const string DigGameDailyNOAdPopupAutoShowCountFieldKey = "digGame_dailyNOAdPopupAutoShowCount";
            public const string DigGameGameTipsPopupCoinCostFieldKey = "digGame_gameTipsPopupCoinCost";
            public const string DigGameGameTipsPopupPassLevelCoinCostFieldKey = "digGame_gameTipsPopupPassLevelCoinCost";
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
            public static bool GetDigGameIsOpen()
            {
                if (TryGetValue<bool>(DigGameConfigKey, DigGameIsOpenFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) && value.DigGameIsOpen;
            }
            public static int GetDigGameShowTipsFailCount()
            {
                if (TryGetValue<int>(DigGameConfigKey, DigGameShowTipsFailCountFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DigGameShowTipsFailCount : 0;
            }
            public static float GetDigGameShowTipsTimeLimit()
            {
                if (TryGetValue<float>(DigGameConfigKey, DigGameShowTipsTimeLimitFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DigGameShowTipsTimeLimit : 0f;
            }
            public static int GetDigGameInterstitialCD()
            {
                if (TryGetValue<int>(DigGameConfigKey, DigGameInterstitialCDFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DigGameInterstitialCD : 0;
            }
            public static int GetDigGameRewardResetInterstitialCD()
            {
                if (TryGetValue<int>(DigGameConfigKey, DigGameRewardResetInterstitialCDFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DigGameRewardResetInterstitialCD : 0;
            }
            public static int GetDigGameInterstitialShowMinLevel()
            {
                if (TryGetValue<int>(DigGameConfigKey, DigGameInterstitialShowMinLevelFieldKey, out var remoteValue))
                {
                    return remoteValue;
                }

                return TryGetGlobalConst(out var value) ? value.DigGameInterstitialShowMinLevel : 0;
            }
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
}}}
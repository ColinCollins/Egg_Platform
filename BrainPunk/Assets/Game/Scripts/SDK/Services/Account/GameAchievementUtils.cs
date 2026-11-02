#nullable enable
using System;
using System.Collections.Generic;
using Config;
using Config.Game;
using Cysharp.Threading.Tasks;
using Game.ConfigModule;
using Guru.SDK.Framework.Core;
using Guru.SDK.Framework.GameService.Core.Achievement;
using Guru.SDK.Framework.GameService.Core.Package;
using UnityEngine;

namespace Game
{
    public enum AchievementType
    {
        // 指定关卡
        Level = 0,
        // 关卡数量
        LevelCount = 1,
        // 道具消耗
        ToolConsumes = 2,

        // 其他
        Other = -1
    }

    /// <summary>
    /// 游戏成就工具类
    /// 封装 Guru SDK GameServicePackage 的常用成就操作
    /// SDK 内部做了风险处理和数据统计。在不登陆或者没有网络的情况下仍然可以调用 Unlock 和 Increment 接口用于数据记录存储。
    /// TODO: 等待 guru_spec.yaml 配置 achievements 并重新生成 GuruAllSpecs.G.cs 后开放使用
    /// </summary>
    public static class GameAchievementUtils
    {
        private const string TAG = "GameAchievementUtils";

        // 26.4.0 bug 暂时不用
        // 成就状态缓存，避免重复调用 LoadAchievements
        private static List<AchievementItem>? _cachedAchievements;

        private static GameServicePackage? _gameService;

        // private static int cachePassedCount = 0;

        public static void Init()
        {
            var root = GuruSdk.Instance.GuruRoot;
            if (root == null)
            {
                Debug.LogWarning("GuruRoot is null, cannot obtain GameServicePackage.");
                return;
            }

            // cachePassedCount = DB.GameData.PassedLevels.Count;
            _gameService = root.ObtainPackage<GameServicePackage>();

            if (_gameService == null)
            {
                Debug.LogError("GameAchievementUtils package is null");
            }
        }

        public static void TryToGetPackage()
        {
            _gameService = GuruSdk.Instance.GuruRoot.ObtainPackage<GameServicePackage>();
            if (_gameService == null)
            {
                Debug.LogError("GameAchievementUtils package is null");
            }
        }

        /// <summary>
        /// 预加载成就状态缓存
        /// </summary>
        public static async UniTask LoadAchievementCacheAsync()
        {
            if (!ConfigManager.RemoteConfig.GetOpenGaps())
            {
                Debug.Log("[GameAchievementUtils] LoadAchievementCacheAsync skipped: OpenGaps is false.");
                return;
            }

            if (_gameService == null)
                TryToGetPackage();

            var package = _gameService;
            if (package == null)
            {
                Debug.LogWarning("LoadAchievementCacheAsync failed: GameServicePackage is null.");
                _cachedAchievements = null;
                return;
            }

            /*             _cachedAchievements = await TryLoadAchievementsFromSdk(package);
                        if (_cachedAchievements == null)
                        {
                            Debug.LogWarning("LoadAchievementCacheAsync failed: unable to load achievements from SDK.");
                            return;
                        }

                        Debug.Log($"Achievement cache loaded, count: {_cachedAchievements.Count}"); */
        }

        /// <summary>
        /// 清空成就状态缓存
        /// </summary>
        public static void ClearAchievementCache()
        {
            Debug.Log("Achievement cache cleared.");
            _cachedAchievements = null;
        }

        public static bool IsGameServiceReady()
        {
            if (_gameService == null)
                TryToGetPackage();

            return _gameService?.IsGameServiceReady() ?? false;
        }

        public static async UniTask<bool> UnlockAchievement(string achievementId)
        {
            if (!ConfigManager.RemoteConfig.GetOpenGaps())
            {
                Debug.Log($"[GameAchievementUtils] UnlockAchievement [{achievementId}] skipped: OpenGaps is false.");
                return false;
            }

            if (_gameService == null)
                TryToGetPackage();

            var package = _gameService;
            if (package == null)
            {
                Debug.LogWarning($"UnlockAchievement [{achievementId}] failed: GameServicePackage is null.");
                return false;
            }

            var achievement = Achievements.GetAchievement(achievementId);
            if (achievement == null)
            {
                Debug.LogWarning($"UnlockAchievement [{achievementId}] failed: achievement not found.");
                return false;
            }

            Debug.Log($"UnlockAchievement [{achievementId}] requesting...");
            var result = await package.Unlock(achievement);
            package.Reveal(achievement).Forget();
            Debug.Log($"UnlockAchievement [{achievementId}] result: {result}");

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="achievementId"></param>
        /// <param name="steps"> 传入的是通关数量，应该是用于检测判断的值，>= 满足判断条件就可以</param>
        public static void IncrementAchievement(string achievementId, int steps = 1)
        {
            if (!ConfigManager.RemoteConfig.GetOpenGaps())
            {
                Debug.Log($"[GameAchievementUtils] IncrementAchievement [{achievementId}] skipped: OpenGaps is false.");
                return;
            }

            if (_gameService == null)
                TryToGetPackage();

            var package = _gameService;
            if (package == null)
            {
                Debug.LogWarning($"IncrementAchievement [{achievementId}] failed: GameServicePackage is null.");
                return;
            }

            var achievement = Achievements.GetAchievement(achievementId);
            if (achievement == null)
            {
                Debug.LogWarning($"IncrementAchievement [{achievementId}] failed: achievement not found.");
                return;
            }

            Debug.Log($"IncrementAchievement [{achievementId}] steps: {steps}");
            package.Increment(achievement, steps).Forget();
        }

        #region TryToAchievement

        /// <summary>
        /// 公共入口：尝试触发指定类型的成就
        /// 根据 AchievementType 分发到对应的独立 API
        /// </summary>
        public static async UniTask TryToAchievement(AchievementType type)
        {
            if (!ConfigManager.RemoteConfig.GetOpenGaps())
            {
                Debug.Log($"[GameAchievementUtils] TryToAchievement [{type}] skipped: OpenGaps is false.");
                return;
            }

            Debug.Log($"TryToAchievement started, type: {type}");

            switch (type)
            {
                case AchievementType.Level:
                    await TryToAchievement_Level();
                    break;
                case AchievementType.LevelCount:
                    await TryToAchievement_LevelCount();
                    break;
                case AchievementType.ToolConsumes:
                    await TryToAchievement_ToolConsumes();
                    break;
                case AchievementType.Other:
                default:
                    Debug.LogWarning($"TryToAchievement unsupported type: {type}");
                    break;
            }
        }

        /// <summary>
        /// 尝试触发 Level 类型成就
        /// 检测 DB.GameData.LevelSuccessCount 是否包含指定 key 且 count > 0
        /// </summary>
        private static async UniTask TryToAchievement_Level()
        {
            Debug.Log("TryToAchievement_Level started");

            var gaps = ConfigManager.Instance?.Tables?.TbGAPs?.DataList;
            if (gaps == null)
            {
                Debug.LogWarning("TryToAchievement_Level failed: TbGAPs config is null.");
                return;
            }

            int triggeredCount = 0;
            foreach (var gap in gaps)
            {
                if (gap.Type != (int)AchievementType.Level) continue;

                var achievement = Achievements.GetAchievement(gap.Name);
                if (achievement == null)
                {
                    Debug.LogWarning($"GAPs config [{gap.Name}] not found in Achievements.");
                    continue;
                }

                if (string.IsNullOrEmpty(gap.Value))
                {
                    Debug.LogWarning($"Achievement [{gap.Name}] Level type but value is empty.");
                    continue;
                }

                var levelData = PlayCtrl.Instance.Level.GetLevelDataById(gap.Value);
                if (levelData == null)
                {
                    Debug.LogWarning($"Achievement [{gap.Name}] Level config invalid: cannot find level by id [{gap.Value}].");
                    continue;
                }

                if (DB.GameData.LevelSuccessCount != null &&
                    DB.GameData.LevelSuccessCount.TryGetValue(levelData.Path, out var levelCount) &&
                    levelCount > 0)
                {
                    Debug.Log($"Achievement [{gap.Name}] Level condition met, key: {gap.Value}, count: {levelCount}");
                    UnlockAchievement(gap.Name).Forget();
                    triggeredCount++;
                }
                else
                {
                    Debug.Log($"Achievement [{gap.Name}] Level condition not met, key: {gap.Value}");
                }
            }

            Debug.Log($"TryToAchievement_Level finished, triggered: {triggeredCount}");
        }

        /// <summary>
        /// 尝试触发 LevelCount 类型成就
        /// 检测 DB.GameData.PassedLevels.Count >= target
        /// </summary>
        private static async UniTask TryToAchievement_LevelCount()
        {
            Debug.Log("TryToAchievement_LevelCount started");

            // if (!await EnsureReadyAsync()) return;
            // var achievementItems = _cachedAchievements!;

            var gaps = ConfigManager.Instance?.Tables?.TbGAPs?.DataList;
            if (gaps == null)
            {
                Debug.LogWarning("TryToAchievement_LevelCount failed: TbGAPs config is null.");
                return;
            }

            int triggeredCount = 0;
            foreach (var gap in gaps)
            {
                if (gap.Type != (int)AchievementType.LevelCount) continue;

                var achievement = Achievements.GetAchievement(gap.Name);
                if (achievement == null)
                {
                    Debug.LogWarning($"GAPs config [{gap.Name}] not found in Achievements.");
                    continue;
                }

                if (!int.TryParse(gap.Value, out var targetLevelCount))
                {
                    Debug.LogWarning($"Achievement [{gap.Name}] LevelCount type but value parse failed: {gap.Value}");
                    continue;
                }

                int newCount = DB.GameData.PassedLevels?.Count ?? 0;

                // 超过最大关卡数，解锁对应成就
                if (newCount >= targetLevelCount)
                {
                    IncrementAchievement(gap.Name, targetLevelCount);
                }
                else
                {
                    // 因为没有 Load 所以不知道确切的 increment  值，先这样等下个 sdk 版本 API 修好了再说。
                    IncrementAchievement(gap.Name, 1);

                    Debug.Log($"Achievement [{gap.Name}] LevelCount condition not met, passed: {newCount}, target: {targetLevelCount}");
                }
            }

            Debug.Log($"TryToAchievement_LevelCount finished, triggered: {triggeredCount}");
        }

        /// <summary>
        /// 尝试触发 ToolConsumes 类型成就
        /// 检测 DB.GameData.ToolConsumesCache["Tips"] > target
        /// </summary>
        private static async UniTask TryToAchievement_ToolConsumes()
        {
            Debug.Log("TryToAchievement_ToolConsumes started");

            // if (!await EnsureReadyAsync()) return;
            // var achievementItems = _cachedAchievements!;

            var gaps = ConfigManager.Instance?.Tables?.TbGAPs?.DataList;
            if (gaps == null)
            {
                Debug.LogWarning("TryToAchievement_ToolConsumes failed: TbGAPs config is null.");
                return;
            }

            int triggeredCount = 0;
            foreach (var gap in gaps)
            {
                if (gap.Type != (int)AchievementType.ToolConsumes) continue;

                var achievement = Achievements.GetAchievement(gap.Name);
                if (achievement == null)
                {
                    Debug.LogWarning($"GAPs config [{gap.Name}] not found in Achievements.");
                    continue;
                }

                /*                 if (IsAchievementUnlocked(achievementItems, achievement.Id))
                                {
                                    Debug.Log($"Achievement [{gap.Name}] already unlocked, skip.");
                                    continue;
                                } */

                if (!int.TryParse(gap.Value, out var targetConsumeCount))
                {
                    Debug.LogWarning($"Achievement [{gap.Name}] ToolConsumes type but value parse failed: {gap.Value}");
                    continue;
                }

                if (DB.GameData.ToolConsumesCache != null &&
                    DB.GameData.ToolConsumesCache.TryGetValue(GameProps.Tips.ToString(), out var tipsCount) &&
                    tipsCount >= targetConsumeCount)
                {
                    Debug.Log($"Achievement [{gap.Name}] ToolConsumes condition met, tipsCount: {tipsCount}, target: {targetConsumeCount}");
                    UnlockAchievement(gap.Name).Forget();
                    triggeredCount++;
                }
                else
                {
                    Debug.Log($"Achievement [{gap.Name}] ToolConsumes condition not met, target: {targetConsumeCount}");
                }
            }

            Debug.Log($"TryToAchievement_ToolConsumes finished, triggered: {triggeredCount}");
        }

        #endregion

        public static void ShowAchievementUI()
        {
            if (!ConfigManager.RemoteConfig.GetOpenGaps())
            {
                Debug.Log("[GameAchievementUtils] ShowAchievementUI skipped: OpenGaps is false.");
                return;
            }

            Debug.Log("ShowAchievementUI");
            if (_gameService == null)
                TryToGetPackage();
            _gameService?.ShowUI().Forget();
        }

        #region Private Helpers

        private static async UniTask<List<AchievementItem>?> TryLoadAchievementsFromSdk(GameServicePackage package)
        {
            try
            {
                var items = await package.LoadAchievements();
                return new List<AchievementItem>(items);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                // Most commonly caused by invalid provider achievement metadata
                // (e.g. incremental target/steps <= 0 on platform console).
                Debug.LogError(
                    $"[{TAG}] LoadAchievements interrupted by invalid achievement metadata. " +
                    $"Please verify platform achievement config (Google Play/Game Center), especially incremental target/steps > 0. Error: {ex}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{TAG}] LoadAchievements failed: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 判断指定成就是否已解锁
        /// </summary>
        private static bool IsAchievementUnlocked(List<AchievementItem> achievementItems, string achievementId)
        {
            if (achievementItems == null)
                return false;

            foreach (var item in achievementItems)
            {
                if (item.AchievementId == achievementId && item.Unlocked)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        /// <summary>
        /// 重置成就（仅iOS支持）
        /// </summary>
        public static void ResetAchievements()
        {
#if UNITY_IOS
     Apple.GameKit.GKAchievement.ResetAchievements();
#endif
        }

        // 打开成就UI按钮
        public static void OpenDebugAchievementShowUI()
        {
            ShowAchievementUI();
        }
    }
}

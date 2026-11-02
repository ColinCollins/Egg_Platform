using System.Collections.Generic;
using Bear.SaveModule;
using Config.Game;
using Game.Common;
using Game.ConfigModule;
using UnityEngine;

/// <summary>
/// 明信片红点判断器。按 Sort 升序推进的最近未完成普通明信片配置存在尚未解锁且星星数量满足 unlockProgress 的部件时显示红点。
/// 调用边界：仅由 RedDotManager 在运行时刷新红点时调用；不要在编辑器态、OnValidate 或 prefab 预览中直接调用。
/// 注意事项：本类只读取 DB.Postcard、配置表和背包 Star 数量，不写存档、不发奖、不打开 UI、不加载资源。
/// </summary>
public sealed class PostcardRedDotChecker : IRedDotChecker
{
    #region IRedDotChecker

    /// <summary>
    /// 计算按 Sort 升序推进的最近未完成明信片配置是否存在可消除的 postcardItem。
    /// </summary>
    /// <returns>true 表示显示明信片红点；false 表示隐藏。</returns>
    public bool IsOn()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        if (!TryGetPostcardData(out Postcard postcardData)
            || !TryGetNearestIncompletePostcardConfig(postcardData, out Config.PostcardConfig postcardConfig)
            || !TryGetAvailableStarCount(out int starCount))
        {
            return false;
        }

        return HasUnlockableProgress(postcardConfig, postcardData.CurrentCollectedPuzzledId, starCount);
    }

    #endregion

    #region Data Access

    private static bool TryGetPostcardData(out Postcard postcardData)
    {
        postcardData = null;

        DBManager dbManager = Object.FindAnyObjectByType<DBManager>();
        if (dbManager == null || !dbManager.IsInitialized)
        {
            return false;
        }

        postcardData = dbManager.Get<Postcard>();
        return postcardData != null;
    }

    private static bool TryGetNearestIncompletePostcardConfig(Postcard postcardData, out Config.PostcardConfig postcardConfig)
    {
        postcardConfig = null;
        if (postcardData == null)
        {
            return false;
        }

        ConfigManager configManager = Object.FindAnyObjectByType<ConfigManager>();
        List<Config.PostcardConfig> postcardConfigs = configManager?.Tables?.TbPostcardConfig?.DataList;
        if (configManager == null || !configManager.IsInitialized || postcardConfigs == null || postcardConfigs.Count <= 0)
        {
            return false;
        }

        for (int i = 0; i < postcardConfigs.Count; i++)
        {
            Config.PostcardConfig candidate = postcardConfigs[i];
            if (candidate == null
                || string.IsNullOrWhiteSpace(candidate.Id)
                || IsUnlockConfig(candidate)
                || IsPostcardCompleted(postcardData, candidate.Id))
            {
                continue;
            }

            if (postcardConfig == null
                || candidate.Sort < postcardConfig.Sort
                || (candidate.Sort == postcardConfig.Sort
                    && string.Compare(candidate.Id, postcardConfig.Id, System.StringComparison.Ordinal) < 0))
            {
                postcardConfig = candidate;
            }
        }

        return postcardConfig != null;
    }

    private static bool TryGetAvailableStarCount(out int starCount)
    {
        starCount = 0;
        if (!Singleton<PlayCtrl>.IsInitialized)
        {
            return false;
        }

        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            return false;
        }

        starCount = Mathf.Max(0, bag.GetToolCount(GameProps.Star));
        return true;
    }

    #endregion

    #region Rules

    private static bool IsPostcardCompleted(Postcard postcardData, string postcardId)
    {
        return !string.IsNullOrWhiteSpace(postcardId)
            && postcardData?.CompletedId != null
            && postcardData.CompletedId.Contains(postcardId);
    }

    private static bool IsUnlockConfig(Config.PostcardConfig postcardConfig)
    {
        return postcardConfig != null
            && (postcardConfig.UnlockProgress == null || postcardConfig.UnlockProgress.Count <= 0)
            && (postcardConfig.Rewards == null || postcardConfig.Rewards.Count <= 0)
            && postcardConfig.RewardMultiple <= 0;
    }

    private static bool HasUnlockableProgress(
        Config.PostcardConfig postcardConfig,
        List<int> collectedPuzzleIds,
        int starCount)
    {
        if (postcardConfig.UnlockProgress == null || postcardConfig.UnlockProgress.Count <= 0)
        {
            return false;
        }

        foreach (KeyValuePair<int, int> progress in postcardConfig.UnlockProgress)
        {
            int puzzleId = progress.Key;
            int requiredStarCount = Mathf.Max(0, progress.Value);
            if (IsPuzzleCollected(collectedPuzzleIds, puzzleId))
            {
                continue;
            }

            if (starCount >= requiredStarCount)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPuzzleCollected(List<int> collectedPuzzleIds, int puzzleId)
    {
        return collectedPuzzleIds != null
            && collectedPuzzleIds.Contains(puzzleId);
    }

    #endregion
}

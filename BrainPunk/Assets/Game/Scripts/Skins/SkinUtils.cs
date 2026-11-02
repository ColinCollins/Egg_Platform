using System;
using System.Collections.Generic;
using System.Threading;
using Bear.ResourceSystem;
using Bear.SaveModule;
using Config;
using Config.Game;
using Config.LubanConfig;
using Cysharp.Threading.Tasks;
using Game.ConfigModule;
using UnityEngine;

/// <summary>
/// 提供 Skin 配置查询、拥有状态、随机奖励候选，以及图标与部件 prefab 的资源读取入口。
/// 调用方仍需自行处理奖励发放、保存、UI 回写、实例化、缓存、取消和版本保护。
/// </summary>
public static class SkinUtils
{
    #region Resource Constants

    /// <summary>
    /// Skin UI 图标资源根目录；仅用于 TextureResources 为短名时拼接候选路径。
    /// </summary>
    public const string SkinIconResourceRoot = "Assets/Game/ArtSrc/Skins/UI";

    /// <summary>
    /// Skin 部件 prefab 资源根目录；仅用于 PrefabResource 为短名时拼接候选路径。
    /// </summary>
    public const string SkinPrefabResourceRoot = "Assets/Game/Prefabs/Skins";

    #endregion

    #region Config And Ownership

    /// <summary>
    /// 按 skinId 获取 Skin 配置。
    /// 调用边界：仅在 ConfigManager 已完成初始化后返回有效配置；不会触发配置初始化。
    /// 注意事项：skinId 为空、配置未加载或表中不存在时返回 false。
    /// </summary>
    public static bool TryGetSkin(string skinId, out Skin skin)
    {
        skin = null;
        if (!Application.isPlaying || string.IsNullOrEmpty(skinId))
            return false;

        skin = ConfigManager.Instance?.Tables?.TbSkin?.GetOrDefault(skinId);
        return skin != null && !string.IsNullOrEmpty(skin.Id);
    }

    /// <summary>
    /// 按保存值与部位解析 Skin 配置；兼容 Id、PrefabResource 和 SpineResource 三种历史保存值。
    /// 调用边界：只读取已初始化的 Skin 配置表，不触发配置初始化或写入存档。
    /// 注意事项：保存值为空、配置未加载或没有同部位匹配项时返回 null。
    /// </summary>
    public static Skin ResolveSavedSkin(string savedValue, SkinPart part)
    {
        if (!Application.isPlaying || string.IsNullOrEmpty(savedValue))
            return null;

        var skins = ConfigManager.Instance?.Tables?.TbSkin?.DataList;
        if (skins == null)
            return null;

        foreach (Skin skin in skins)
        {
            if (skin != null
                && skin.SkinPart == part
                && SavedValueMatchesSkin(savedValue, skin))
                return skin;
        }

        return null;
    }

    /// <summary>
    /// 把 FullBody 历史保存值解析为 Spine skin 名称。
    /// 调用边界：供 Actor 和 ShopPanel 预览读取现有选择；不会应用皮肤或修改存档。
    /// 注意事项：空保存值返回空字符串，未匹配配置时原样返回保存值。
    /// </summary>
    public static string ResolveFullBodySkinName(string savedValue)
    {
        if (string.IsNullOrEmpty(savedValue))
            return string.Empty;

        Skin skin = ResolveSavedSkin(savedValue, SkinPart.FullBody);
        if (skin == null)
            return savedValue;

        return string.IsNullOrEmpty(skin.SpineResource) ? skin.Id : skin.SpineResource;
    }

    /// <summary>
    /// 检查现有保存值是否匹配指定 Skin 的 Id、PrefabResource 或 SpineResource。
    /// 调用边界：仅用于兼容 Skin 历史保存格式，不读取或写入存档。
    /// </summary>
    public static bool SavedValueMatchesSkin(string savedValue, Skin skin)
    {
        if (string.IsNullOrEmpty(savedValue) || skin == null)
            return false;

        return string.Equals(savedValue, skin.Id, StringComparison.Ordinal)
               || string.Equals(savedValue, skin.PrefabResource, StringComparison.Ordinal)
               || string.Equals(savedValue, skin.SpineResource, StringComparison.Ordinal);
    }

    /// <summary>
    /// 检测指定 skinId 是否存在于当前 Skin 配置表。
    /// 调用边界：供奖励、商店和外观流程做轻量配置校验。
    /// 注意事项：只检查配置是否存在，不检查玩家是否拥有或资源是否可加载。
    /// </summary>
    public static bool SkinExists(string skinId)
    {
        return Application.isPlaying && TryGetSkin(skinId, out _);
    }

    /// <summary>
    /// 检测玩家当前是否已经拥有指定 Skin。
    /// 调用边界：仅供运行时奖励候选过滤和外观状态查询。
    /// 注意事项：GameData 未就绪或 skinId 为空时返回 false，不触发存档初始化。
    /// </summary>
    public static bool IsSkinOwned(string skinId)
    {
        if (!Application.isPlaying)
            return false;

        ICollection<string> ownedSkinIds = DB.GameData?.OwnedSkin;
        if (ownedSkinIds == null || string.IsNullOrEmpty(skinId))
            return false;

        foreach (string ownedSkinId in ownedSkinIds)
        {
            if (string.Equals(ownedSkinId, skinId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 检测指定 Skin 是否可作为随机奖励发放。
    /// 调用边界：供 RandomSkinRewardConfig 候选过滤使用。
    /// 注意事项：要求配置存在、尚未拥有，且不是默认或不可用占位 Skin。
    /// </summary>
    public static bool CanGrantRandomRewardSkin(string skinId, out Skin skin)
    {
        skin = null;
        return Application.isPlaying
               && TryGetSkin(skinId, out skin)
               && !IsSkinOwned(skinId)
               && skin.UnlockType != SkinUnlockType.Default
               && skin.UnlockType != SkinUnlockType.Unavailable;
    }

    /// <summary>
    /// 根据 RandomSkinRewardConfig 表 id 按权重抽取一件未拥有的可发放 Skin。
    /// 调用边界：供每日挑战等配置驱动奖励流程使用，不直接授予 Skin 或写存档。
    /// 注意事项：奖励池不存在、候选全部已拥有、权重无效或配置未初始化时返回 false。
    /// </summary>
    public static bool TryGetRandomUnownedRewardSkin(string rewardConfigId, out Skin selectedSkin)
    {
        selectedSkin = null;
        if (!Application.isPlaying || string.IsNullOrEmpty(rewardConfigId))
            return false;

        RandomSkinRewardConfig rewardConfig = ConfigManager.Instance?
            .Tables?
            .TbRandomSkinRewardConfig?
            .GetOrDefault(rewardConfigId);
        return TryGetRandomUnownedRewardSkin(rewardConfig, out selectedSkin);
    }

    /// <summary>
    /// 从给定 RandomSkinRewardConfig 中按权重抽取一件未拥有的可发放 Skin。
    /// 调用边界：调用方已经取得配置对象时可复用，避免再次按 id 查表。
    /// 注意事项：只返回候选，不调用 Bag.GetNewSkin；同一次业务结算应缓存返回结果，避免重复抽取。
    /// </summary>
    public static bool TryGetRandomUnownedRewardSkin(
        RandomSkinRewardConfig rewardConfig,
        out Skin selectedSkin)
    {
        selectedSkin = null;
        if (!Application.isPlaying
            || rewardConfig?.Rewards == null
            || rewardConfig.Rewards.Count == 0)
            return false;

        var candidates = new List<KeyValuePair<Skin, int>>(rewardConfig.Rewards.Count);
        long totalWeight = 0L;
        foreach (KeyValuePair<string, int> reward in rewardConfig.Rewards)
        {
            if (reward.Value <= 0 || !CanGrantRandomRewardSkin(reward.Key, out Skin skin))
                continue;

            candidates.Add(new KeyValuePair<Skin, int>(skin, reward.Value));
            totalWeight += reward.Value;
        }

        if (candidates.Count == 0 || totalWeight <= 0L)
            return false;

        double randomWeight = UnityEngine.Random.value * totalWeight;
        long accumulatedWeight = 0L;
        for (int index = 0; index < candidates.Count; index++)
        {
            accumulatedWeight += candidates[index].Value;
            if (randomWeight < accumulatedWeight)
            {
                selectedSkin = candidates[index].Key;
                return true;
            }
        }

        selectedSkin = candidates[candidates.Count - 1].Key;
        return true;
    }

    #endregion

    #region Location Resolution

    /// <summary>
    /// 根据 Skin.TextureResources 解析可加载的 UI 图标路径；完整 Assets 路径会原样返回。
    /// 返回空字符串表示配置为空或当前 ResourceManager 无法加载候选资源。
    /// </summary>
    public static string ResolveIconLocation(Skin skin)
    {
        string textureResource = skin?.TextureResources;
        if (string.IsNullOrEmpty(textureResource))
        {
            return string.Empty;
        }

        if (textureResource.StartsWith("Assets/", StringComparison.Ordinal))
        {
            return textureResource;
        }

        string candidate = $"{SkinIconResourceRoot}/{textureResource}.png";
        return CanLoadIconLocation(candidate) ? candidate : string.Empty;
    }

    /// <summary>
    /// 根据 Skin.PrefabResource 解析部件 prefab 路径；完整 Assets 路径会原样返回。
    /// 该方法只负责路径规则，不校验资源是否存在。
    /// </summary>
    public static string ResolvePrefabLocation(string prefabResource)
    {
        if (string.IsNullOrEmpty(prefabResource))
        {
            return string.Empty;
        }

        if (prefabResource.StartsWith("Assets/", StringComparison.Ordinal))
        {
            return prefabResource;
        }

        return $"{SkinPrefabResourceRoot}/{prefabResource}.prefab";
    }

    #endregion

    #region Resource Loading

    /// <summary>
    /// 检查已解析的 Skin 图标路径是否能由当前 Bear ResourceManager 加载为 Sprite。
    /// 调用边界：传入值应来自 ResolveIconLocation 或等价的完整资源路径。
    /// </summary>
    public static bool CanLoadIconLocation(string location)
    {
        return ResourceManager.Instance.CanLoad<Sprite>(location);
    }

    /// <summary>
    /// 异步加载已解析的 Skin 图标 Sprite；调用方负责捕获异常并处理过期 UI 回写。
    /// </summary>
    public static UniTask<Sprite> LoadIconAsync(string location, CancellationToken cancellationToken = default)
    {
        return ResourceManager.Instance.LoadAsync<Sprite>(location, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 检查已解析的 Skin 部件 prefab 路径是否能由当前 Bear ResourceManager 加载为 GameObject。
    /// 调用边界：传入值应来自 ResolvePrefabLocation 或等价的完整资源路径。
    /// </summary>
    public static bool CanLoadPrefabLocation(string location)
    {
        return ResourceManager.Instance.CanLoad<GameObject>(location);
    }

    /// <summary>
    /// 异步加载已解析的 Skin 部件 prefab；调用方负责实例化、缓存、取消和版本保护。
    /// </summary>
    public static UniTask<GameObject> LoadPrefabAsync(string location, CancellationToken cancellationToken = default)
    {
        return ResourceManager.Instance.LoadAsync<GameObject>(location, cancellationToken: cancellationToken);
    }

    #endregion
}

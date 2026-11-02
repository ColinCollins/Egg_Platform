using System.Collections.Generic;
using Bear.Logger;
using Config;
using Game.ConfigModule;

/// <summary>
/// IWanner 关卡配置解析与查询控制器。
/// 由 Loading 在 Luban 表完成加载后初始化，统一校验 Group 排序行及其小关引用。
/// </summary>
public sealed class IWannerGameLevelCtrl : IDebuger
{
    #region Runtime Data

    private readonly List<IWannerLevelSort> _levelGroups = new List<IWannerLevelSort>();
    private TbIWannerLevelData _levelDataTable;

    /// <summary>
    /// 用途：标识 IWanner 本地配置是否已完成一次初始化。
    /// 调用边界：入口和玩法控制器只有在该值为 true 后才能请求 Group。
    /// 注意事项：true 仅表示表已加载；具体 Group 仍会在使用前执行完整引用校验。
    /// </summary>
    public bool IsReady { get; private set; }

    /// <summary>
    /// 用途：提供按配置顺序冻结的 IWanner Group 只读列表。
    /// 调用边界：仅供运行时入口、选组和玩法推进逻辑读取，调用方不得修改生成配置对象。
    /// </summary>
    public IReadOnlyList<IWannerLevelSort> LevelGroups => _levelGroups;

    #endregion

    #region Initialization

    /// <summary>
    /// 用途：读取当前 Loading 周期已经加载完成的 IWanner Luban 表。
    /// 调用边界：仅由 LoadManager 在 ConfigManager 初始化完成后调用；重复调用保持幂等。
    /// 注意事项：本方法不修改 Excel、JSON 或远端配置，也不自动修复无效 Group 数据。
    /// </summary>
    /// <param name="error">初始化失败原因；成功时为空。</param>
    /// <returns>两张 IWanner 表均存在且排序表至少包含一行时返回 true。</returns>
    public bool Init(out string error)
    {
        if (IsReady)
        {
            error = null;
            return true;
        }

        error = null;
        if (ConfigManager.Instance == null ||
            !ConfigManager.Instance.IsInitialized ||
            ConfigManager.Instance.Tables?.TbIWannerLevelData == null ||
            ConfigManager.Instance.Tables.TbIWannerLevelSort == null)
        {
            error = "IWanner config tables are not initialized.";
            return false;
        }

        _levelDataTable = ConfigManager.Instance.Tables.TbIWannerLevelData;
        List<IWannerLevelSort> configuredGroups =
            ConfigManager.Instance.Tables.TbIWannerLevelSort.DataList;
        if (configuredGroups == null || configuredGroups.Count == 0)
        {
            error = "TbIWannerLevelSort contains no Group rows.";
            _levelDataTable = null;
            return false;
        }

        _levelGroups.Clear();
        for (int i = 0; i < configuredGroups.Count; i++)
        {
            IWannerLevelSort group = configuredGroups[i];
            if (group != null)
                _levelGroups.Add(group);
        }

        if (_levelGroups.Count == 0)
        {
            error = "TbIWannerLevelSort contains no non-null Group rows.";
            _levelDataTable = null;
            return false;
        }

        IsReady = true;
        return true;
    }

    #endregion

    #region Group Queries

    /// <summary>
    /// 用途：解析配置顺序中的第一个 Group，供当前临时 IWannerGameEnter 直接进入。
    /// 调用边界：入口点击后调用；未来接入选组界面时应改由选组结果调用 TryGetGroup。
    /// 注意事项：不会跳过首行寻找后续可用 Group，首行无效时会明确失败。
    /// </summary>
    public bool TryGetFirstGroup(out IWannerLevelSort group, out string error)
    {
        group = null;
        if (!IsReady || _levelGroups.Count == 0)
        {
            error = "IWanner level controller is not ready.";
            return false;
        }

        return TryValidateGroup(_levelGroups[0], out group, out error);
    }

    /// <summary>
    /// 用途：按 IWannerLevelSort.Id 查询并校验一个 Group。
    /// 调用边界：恢复进度、选组和 Group 切换逻辑使用；Id 是当前唯一 Group 主键。
    /// 注意事项：IWannerLevelSort.Group 仅保留为配置分类字段，不作为唯一键。
    /// </summary>
    public bool TryGetGroup(int groupId, out IWannerLevelSort group, out string error)
    {
        group = null;
        if (!IsReady)
        {
            error = "IWanner level controller is not ready.";
            return false;
        }

        IWannerLevelSort configuredGroup =
            _levelGroups.Find(item => item != null && item.Id == groupId);
        if (configuredGroup == null)
        {
            error = $"IWanner Group was not found. groupId:{groupId}.";
            return false;
        }

        return TryValidateGroup(configuredGroup, out group, out error);
    }

    /// <summary>
    /// 用途：按配置顺序合并存档中的当前 Group 与历史通关记录，解析当前最高可进入的 Group Id。
    /// 调用边界：供选组界面初始化、Group 入口校验和通关进度写入使用。
    /// 注意事项：本方法只计算结果，不修改或保存 IWanner 存档；已通关最后一组时返回最后一组 Id。
    /// </summary>
    /// <param name="savedMaxGroupId">存档当前记录的最高可进入 Group Id。</param>
    /// <param name="passedGroupIds">存档中的历史已通关 Group Id 集合，可为空。</param>
    /// <returns>解析后的当前 Group Id；配置未就绪时返回 0。</returns>
    public int ResolveCurrentGroupId(int savedMaxGroupId, ICollection<int> passedGroupIds)
    {
        if (!IsReady || _levelGroups.Count == 0)
            return 0;

        int resolvedIndex = -1;
        int highestPassedIndex = -1;
        for (int i = 0; i < _levelGroups.Count; i++)
        {
            int groupId = _levelGroups[i].Id;
            if (groupId == savedMaxGroupId)
                resolvedIndex = i;

            if (passedGroupIds?.Contains(groupId) == true)
                highestPassedIndex = i;
        }

        if (highestPassedIndex >= 0)
        {
            int nextPlayableIndex = highestPassedIndex < _levelGroups.Count - 1
                ? highestPassedIndex + 1
                : highestPassedIndex;
            if (nextPlayableIndex > resolvedIndex)
                resolvedIndex = nextPlayableIndex;
        }

        if (resolvedIndex < 0)
            resolvedIndex = 0;

        return _levelGroups[resolvedIndex].Id;
    }

    /// <summary>
    /// 用途：按配置顺序把每个已通关 Group 的直接下一组补入永久解锁列表。
    /// 调用边界：供通关写入、选组旧档归一和实际进组校验共用；只修改传入存档，调用方负责保存。
    /// 注意事项：不依赖最高进度或 Id 连续性，不递归解锁，不撤销已有解锁；最后一组与 TBN 不新增记录。
    /// </summary>
    /// <param name="saveData">已加载的 IWanner 独立存档；为空或配置未就绪时不修改。</param>
    /// <returns>补齐集合或至少新增一个下一组的解锁记录时返回 true。</returns>
    public bool RestorePassedGroupUnlocks(Bear.SaveModule.IWannerGame saveData)
    {
        if (!IsReady || saveData == null)
            return false;

        bool changed = saveData.EnsureValidCollections();
        for (int i = 1; i < _levelGroups.Count; i++)
        {
            int passedGroupId = _levelGroups[i - 1].Id;
            int nextGroupId = _levelGroups[i].Id;
            if (passedGroupId <= 0 || nextGroupId <= 0 ||
                !saveData.PassedLevels.Contains(passedGroupId) ||
                saveData.UnlockGroups.Contains(nextGroupId))
            {
                continue;
            }

            saveData.UnlockGroups.Add(nextGroupId);
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// 用途：批量解锁当前配置中的全部 IWanner Group，并在全部写入后统一保存一次。
    /// 调用边界：仅供运行态 GM/调试功能使用；不会写入通关记录、组内进度或奖励状态。
    /// 注意事项：只补充 UnlockGroups，配置或独立存档不可用时不会执行部分写入。
    /// </summary>
    /// <param name="addedCount">本次实际新增的解锁 Group 数量。</param>
    /// <returns>控制器与 IWannerGame 存档可用且保存成功时返回 true。</returns>
    public bool TryUnlockAllGroups(out int addedCount)
    {
        addedCount = 0;
        if (!IsReady || _levelGroups.Count == 0)
        {
            this.LogWarning(
                "[IWannerGameLevelCtrl] Unlock all Groups skipped because the controller is not ready.");
            return false;
        }

        Bear.SaveModule.IWannerGame saveData = DB.IWannerGame;
        if (saveData == null)
        {
            this.LogWarning(
                "[IWannerGameLevelCtrl] Unlock all Groups skipped because DB.IWannerGame is unavailable.");
            return false;
        }

        bool changed = saveData.EnsureValidCollections();
        for (int i = 0; i < _levelGroups.Count; i++)
        {
            IWannerLevelSort group = _levelGroups[i];
            if (group == null ||
                group.Id <= 0 ||
                saveData.UnlockGroups.Contains(group.Id))
            {
                continue;
            }

            saveData.UnlockGroups.Add(group.Id);
            addedCount++;
            changed = true;
        }

        if (!changed || DB.SaveIWannerGame())
            return true;

        this.LogWarning("[IWannerGameLevelCtrl] Failed to save unlocked IWanner Groups.");
        return false;
    }

    /// <summary>
    /// 用途：判定 LevelSort 之外的 TBN Item 是否已由最后一个有效 Group 解锁。
    /// 调用边界：供选组表现与 IWanner TBN 权威入口共同使用，不写入或保存存档。
    /// 注意事项：只认最后一条有效配置的通关记录，不读取 LockType，也不允许广告解锁。
    /// </summary>
    /// <param name="passedGroupIds">IWanner 历史已通关 Group Id 集合，可为空。</param>
    /// <returns>配置已就绪且最后一个 Group 已通关时返回 true。</returns>
    public bool IsTbnUnlocked(ICollection<int> passedGroupIds)
    {
        if (!IsReady || _levelGroups.Count == 0)
            return false;

        IWannerLevelSort lastGroup = _levelGroups[_levelGroups.Count - 1];
        return lastGroup != null && passedGroupIds?.Contains(lastGroup.Id) == true;
    }

    /// <summary>
    /// 用途：解析 Group 内指定索引对应的小关数据。
    /// 调用边界：IWannerPlayCtrl 在首次进入、Reset 和后续小关推进时调用。
    /// 注意事项：索引采用从 0 开始的配置位置，配置重排后仍按位置解析。
    /// </summary>
    public bool TryGetLevelData(
        int groupId,
        int levelIndex,
        out IWannerLevelData levelData,
        out string error)
    {
        levelData = null;
        if (!TryGetGroup(groupId, out IWannerLevelSort group, out error))
            return false;

        if (levelIndex < 0 || levelIndex >= group.Path.Length)
        {
            error =
                $"IWanner level index is out of range. groupId:{groupId}, index:{levelIndex}, count:{group.Path.Length}.";
            return false;
        }

        string levelId = group.Path[levelIndex];
        levelData = _levelDataTable?.GetOrDefault(levelId);
        if (levelData != null)
        {
            error = null;
            return true;
        }

        error =
            $"IWanner LevelData was not found. groupId:{groupId}, index:{levelIndex}, levelId:{levelId}.";
        return false;
    }

    private bool TryValidateGroup(
        IWannerLevelSort configuredGroup,
        out IWannerLevelSort group,
        out string error)
    {
        group = null;
        if (configuredGroup == null)
        {
            error = "IWanner Group row is null.";
            return false;
        }

        if (configuredGroup.Id <= 0)
        {
            error = $"IWanner Group id is invalid: {configuredGroup.Id}.";
            return false;
        }

        string[] levelIds = configuredGroup.Path;
        if (levelIds == null || levelIds.Length < 1 || levelIds.Length > 5)
        {
            int count = levelIds?.Length ?? 0;
            error =
                $"IWanner Group level count must be between 1 and 5. groupId:{configuredGroup.Id}, count:{count}.";
            return false;
        }

        var uniqueLevelIds = new HashSet<string>();
        for (int i = 0; i < levelIds.Length; i++)
        {
            string levelId = levelIds[i];
            if (string.IsNullOrWhiteSpace(levelId))
            {
                error =
                    $"IWanner Group contains an empty level id. groupId:{configuredGroup.Id}, index:{i}.";
                return false;
            }

            if (!uniqueLevelIds.Add(levelId))
            {
                error =
                    $"IWanner Group contains a duplicate level id. groupId:{configuredGroup.Id}, levelId:{levelId}.";
                return false;
            }

            if (_levelDataTable?.GetOrDefault(levelId) == null)
            {
                error =
                    $"IWanner Group references missing LevelData. groupId:{configuredGroup.Id}, index:{i}, levelId:{levelId}.";
                return false;
            }
        }

        group = configuredGroup;
        error = null;
        return true;
    }

    #endregion
}

using System;
using System.Collections.Generic;
using System.Linq;
using Bear.Logger;
using Config;
using Game;
using Game.ConfigModule;
using SimpleJSON;

/// <summary>
/// 用于处理一些 Level 相关的数据
/// </summary>
public class LevelCtrl : IDebuger
{
    private const string WaitingForNextLevelPath = "10999";

    private bool isReady = false;

    private List<LevelData> leveldatas;
    private List<LevelSort> levelsort;

    private List<LevelSort> _cacheLevelSort;


    // 关卡顺序数据，只能从这里获取
    public List<LevelSort> LevelSorts { get => levelsort; }

    /// <summary>
    /// LevelSortId!!! 名字是历史遗留问题
    /// </summary>
    public int CurrentLevel
    {
        get
        {
            return DB.GameData.CurrentLevel;
        }
    }

    public int MaxLevel
    {
        get
        {
            return DB.GameData.MaxLevel;
        }
    }

    public bool IsLastLevel
    {
        get
        {
            if (levelsort == null || levelsort.Count == 0)
            {
                return true;
            }

            return CurrentLevel >= levelsort.Last().Id;
        }
    }

    /// <summary>
    /// 当前关卡数据（根据当前关卡 Id 映射到对应的 LevelData）。
    /// </summary>
    public LevelData CurrentLevelData
    {
        get
        {
            if (leveldatas == null || levelsort == null || levelsort.Count == 0)
            {
                this.LogError("[LevelCtrl] CurrentLevelData: level data not initialized.");
                return null;
            }

            // 使用关卡排序表的 Id 范围进行一次安全夹紧
            int minId = levelsort.First().Id;
            int maxId = levelsort.Last().Id;
            int currentId = Math.Clamp(CurrentLevel, minId, maxId);

            var sort = levelsort.Find(d => d.Id == currentId);
            if (sort == null)
            {
                this.LogError($"[LevelCtrl] CurrentLevelData: LevelSort not found for id = {currentId}, fallback to last.");
                sort = levelsort.Last();
            }

            var data = GetLevelDataById(sort.Path);
            if (data == null)
            {
                this.LogError($"[LevelCtrl] CurrentLevelData: LevelData not found for path = {sort.Path}.");
            }

            return data;
        }
    }

    /// <summary>
    /// 当前关卡对应的 LevelSort（根据当前关卡 Id）。
    /// </summary>
    public LevelSort CurrentLevelSort
    {
        get
        {
            if (levelsort == null || levelsort.Count == 0)
            {
                this.LogError("[LevelCtrl] CurrentLevelSort: levelsort not initialized.");
                return null;
            }

            int minId = levelsort.First().Id;
            int maxId = levelsort.Last().Id;
            int currentId = Math.Clamp(CurrentLevel, minId, maxId);

            var sort = levelsort.Find(d => d.Id == currentId);
            if (sort == null)
            {
                this.LogError($"[LevelCtrl] CurrentLevelSort: LevelSort not found for id = {currentId}, fallback to last.");
                sort = levelsort.Last();
            }

            return sort;
        }
    }

    /// <summary>
    /// 运行时关卡数据记录，不做缓存
    /// </summary>
    public LevelRuntimeState CurrentLevelState;

    public void Init()
    {
        if (isReady)
            return;

        // 数据加载配置
        leveldatas = ConfigManager.Instance.Tables.TbLevelData.DataList;
        levelsort = ConfigManager.Instance.Tables.TbLevelSort.DataList;
        CurrentLevelState = new LevelRuntimeState();

        InitUnlockLevel();

        SyncRemoteConfig();
        AddWaitingForNextLevel();
        RefreshLevel();

        isReady = true;
    }

    // 默认第一关
    private void InitUnlockLevel()
    {
        if (!DB.GameData.UnlockLevels.Contains(1))
            DB.GameData.UnlockLevels.Add(1);
    }

    /// <summary>
    /// 增加未完待续关卡
    /// </summary>
    private void AddWaitingForNextLevel()
    {
        if (levelsort.Any(data => data != null && data.Path == WaitingForNextLevelPath))
            return;

        var json = new JSONObject
        {
            ["id"] = levelsort.Count + 1,
            ["path"] = WaitingForNextLevelPath,
        };

        var levelData = new LevelSort(json);
        levelsort.Add(levelData);
    }

    public void Victory()
    {
        int nextLevel = DB.GameData.CurrentLevel + 1;

        if (!IsPassed(CurrentLevel))
        {
            DB.GameData.PassedLevels.Add(CurrentLevel);

            // 暂时没啥用，因为可以跳关
            DB.GameData.MaxLevel = Math.Max(DB.GameData.MaxLevel, nextLevel);

            // 打点需要，记录最好关卡数据
            GameAttribute.Instance.SetLevelId(DB.GameData.CurrentLevel, true);
        }

        // 通关当前关卡，解锁下一关
        if (!DB.GameData.UnlockLevels.Contains(nextLevel))
        {
            DB.GameData.UnlockLevels.Add(nextLevel);
        }

        if (nextLevel <= levelsort.Last().Id)
            DB.GameData.CurrentLevel = nextLevel;

        DB.GameData.Save();

        this.Log("----- Victroy currentLevel" + DB.GameData.CurrentLevel);

        RefreshLevel();
    }

    // 根据 pass 和 unlock 确定当前的 最小 level
    public void RefreshLevel()
    {
        // check current config and DB 数据对比
        // _currentLevel = 1
        LevelSort sortData;
        LevelData data = null;
        var gameData = DB.GameData;

        string levelId = "";
        for (int i = 0; i < levelsort.Count; i++)
        {
            sortData = levelsort[i];
            levelId = sortData.Path;
            data = GetLevelDataById(levelId);
            if (IsPassed(sortData.Id) || data == null)
            {
                continue;
            }

            if (!IsUnlock(sortData.Id))
            {
                if (data.LockType == Config.Level.LevelLockType.Unlock)
                    gameData.UnlockLevels.Add(sortData.Id);
                else
                    continue;
            }

        }

        DB.GameData.Save();
    }

    /// <summary>
    /// 同步 RemoteConfig 中的关卡配置。
    /// 默认使用 remote key: level_config。
    /// - 如果 enabled 没变化：不做任何事
    /// - 如果 enabled 变为 true：todo
    /// </summary>
    public void SyncRemoteConfig()
    {
        if (ConfigManager.Instance == null || !ConfigManager.Instance.IsInitialized)
        {
            return;
        }

        if (!ConfigManager.RemoteConfig.TryGetValue<bool>(
                ConfigManager.RemoteConfig.LevelConfigKey,
                ConfigManager.RemoteConfig.EnabledFieldKey,
                out var remoteEnabled))
        {
            return;
        }

        if (!remoteEnabled)
            return;
#if UNITY_EDITOR
        return;
#endif

        // TODO: enabled == true 时的逻辑
        if (ConfigManager.RemoteConfig.TryGetValue<List<string>>(
                ConfigManager.RemoteConfig.LevelConfigKey,
                ConfigManager.RemoteConfig.LevelSortKey,
                out var newPaths))
        {
            if (newPaths == null || newPaths.Count == 0)
            {
                return;
            }

            _cacheLevelSort = new List<LevelSort>(levelsort.Count);
            int id = 1;
            for (int i = 0; i < newPaths.Count; i++)
            {
                string path = newPaths[i];

                // 去除错误数据，以及levelData 不存在数据
                if (string.IsNullOrEmpty(path) || GetLevelDataById(path) == null)
                {
                    continue;
                }

                var json = new JSONObject();
                json["id"] = id++;
                json["path"] = path;

                var replaced = LevelSort.DeserializeLevelSort(json);
                _cacheLevelSort.Add(replaced);
            }

            if (_cacheLevelSort.Count > 0)
            {
                levelsort = _cacheLevelSort;
                this.Log("Replace levelsort success: " + levelsort.Count);
            }
            else
            {
                this.LogError("[LevelCtrl] SyncRemoteConfig: all remote paths invalid, keep original levelsort.");
            }
        }
    }

    public LevelData GetLevelDataById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            this.LogError("[LevelCtrl] GetLevelDataById: id is null or empty.");
            return null;
        }

        if (leveldatas == null || leveldatas.Count == 0)
        {
            this.LogError("[LevelCtrl] GetLevelDataById: leveldatas is not initialized.");
            return null;
        }

        // 关卡表中同时存在 Id(string) 和 Path(string)，这里优先按 Path 匹配，找不到再按 Id 兜底
        var data = leveldatas.Find(d => d.Path == id);
        if (data == null)
        {
            data = leveldatas.Find(d => d.Id == id);
        }

        if (data == null)
        {
            this.LogError($"[LevelCtrl] GetLevelDataById: LevelData not found, id/path = {id}.");
        }

        return data;
    }

    public LevelSort GetLevelSortById(int id)
    {
        if (levelsort == null || levelsort.Count == 0)
        {
            this.LogError("[LevelCtrl] GetLevelSortById: levelsort is not initialized.");
            return null;
        }

        var sort = levelsort.Find(d => d.Id == id);
        if (sort == null)
        {
            this.LogError($"[LevelCtrl] GetLevelSortById: LevelSort not found, id = {id}.");
        }

        return sort;
    }


    /// <summary>
    /// 是否已解锁
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool IsUnlock(int id)
    {
        return DB.GameData.UnlockLevels.Contains(id);
    }

    /// <summary>
    /// 是否已通关
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool IsPassed(int id)
    {
        return DB.GameData.PassedLevels.Contains(id);
    }

    /// <summary>
    /// 获取当前最终主线排序中序号最小且尚未通关的有效关卡；全部通关时返回 Level_999。
    /// 调用边界：供跨玩法入口选择可继续游玩的主线关卡；只查询配置和存档，不修改当前关卡或解锁状态。
    /// 注意事项：未通关候选不受解锁集合限制；仅在候选全部通关时使用固定的 Level_999 配置兜底。
    /// </summary>
    /// <param name="sortData">满足条件且 LevelSort.Id 最小的排序项，或全部通关时的 Level_999 排序项。</param>
    /// <param name="levelData">排序项对应的有效主线关卡配置。</param>
    /// <param name="error">查询失败原因；成功时为空。</param>
    /// <returns>找到未通关的最小序列主线关卡，或找到有效 Level_999 兜底时返回 true。</returns>
    public bool TryGetLowestUnpassedOrLastLevel(
        out LevelSort sortData,
        out LevelData levelData,
        out string error)
    {
        sortData = null;
        levelData = null;

        if (levelsort == null || levelsort.Count == 0 || DB.GameData == null)
        {
            error = "Main level sequence or save data is unavailable.";
            return false;
        }

        List<int> passedLevels = DB.GameData.PassedLevels;
        if (passedLevels == null)
        {
            error = "Main level passed data is unavailable.";
            return false;
        }

        for (int i = 0; i < levelsort.Count; i++)
        {
            LevelSort candidate = levelsort[i];
            if (candidate == null ||
                candidate.Id <= 0 ||
                passedLevels.Contains(candidate.Id) ||
                (sortData != null && candidate.Id >= sortData.Id))
            {
                continue;
            }

            LevelData candidateData = GetLevelDataById(candidate.Path);
            if (candidateData == null)
                continue;

            sortData = candidate;
            levelData = candidateData;
        }

        if (sortData != null)
        {
            error = null;
            return true;
        }

        LevelSort lastLevelSort = levelsort.Find(candidate =>
            candidate != null && candidate.Path == WaitingForNextLevelPath);
        LevelData lastLevelData = lastLevelSort == null
            ? null
            : GetLevelDataById(lastLevelSort.Path);
        if (lastLevelSort != null && lastLevelData != null)
        {
            sortData = lastLevelSort;
            levelData = lastLevelData;
            error = null;
            return true;
        }

        error = "No unpassed main level or valid Level_999 fallback is available.";
        return false;
    }


    /// <summary>
    /// 解锁关卡
    /// </summary>
    /// <param name="id"></param>
    public void UnlockLevel(int id)
    {
        if (IsUnlock(id))
            return;

        DB.GameData.UnlockLevels.Add(id);
        DB.GameData.Save();
    }

    /// <summary>
    /// 批量解锁当前最终有效排序中的全部主线关卡，并在全部写入后统一保存一次。
    /// 调用边界：仅供运行态 GM/调试功能使用；不会写入通关记录或修改当前关卡。
    /// </summary>
    /// <param name="addedCount">本次实际新增的解锁关卡数量。</param>
    /// <returns>关卡排序与主线存档均可用时返回 true。</returns>
    public bool TryUnlockAllLevels(out int addedCount)
    {
        addedCount = 0;
        if (levelsort == null || levelsort.Count == 0 || DB.GameData == null)
        {
            this.LogWarning("[LevelCtrl] Unlock all levels skipped because level data is unavailable.");
            return false;
        }

        List<int> unlockLevels = DB.GameData.UnlockLevels;
        if (unlockLevels == null)
        {
            unlockLevels = new List<int>();
            DB.GameData.UnlockLevels = unlockLevels;
        }

        for (int i = 0; i < levelsort.Count; i++)
        {
            LevelSort sortData = levelsort[i];
            if (sortData == null || sortData.Id <= 0 || unlockLevels.Contains(sortData.Id))
                continue;

            unlockLevels.Add(sortData.Id);
            addedCount++;
        }

        if (addedCount > 0)
            DB.GameData.Save();

        return true;
    }

    /// <summary>
    /// For Test
    /// </summary>
    /// <param name="id"></param>
    public void SetCurrentLevel(int id)
    {
        if (levelsort == null || levelsort.Count == 0)
        {
            DB.GameData.CurrentLevel = id;
        }
        else
        {
            int minId = levelsort.First().Id;
            int maxId = levelsort.Last().Id;
            DB.GameData.CurrentLevel = Math.Clamp(id, minId, maxId);
        }

        DB.GameData.Save();
    }
}

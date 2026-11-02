using System;
using System.Collections.Generic;
using Bear.Logger;
using Config;
using Game.ConfigModule;
using Newtonsoft.Json.Linq;
using SimpleJSON;
using UnityEngine;

/// <summary>
/// DigGame 关卡配置控制器。
/// 由 LoadManager 在 RemoteConfig 与 Luban 表加载完成后初始化一次，统一提供本地/远端排序和关卡数据查询。
/// </summary>
public sealed class DigGameLevelCtrl : IDebuger
{
    #region Constants

    /// <summary>未完待续使用实际配置 ID；排序 ID 随有效普通关数量变化。</summary>
    public const string TbcDataId = "40999";
    private const string WaitingForNextLevelPath = TbcDataId;

    #endregion

    #region Fields And Properties

    private readonly List<DigLevelSort> _levelSorts = new();
    private TbDigLevelData _levelDataTable;

    /// <summary>
    /// DigGame 关卡配置是否已完成一次性初始化。
    /// </summary>
    public bool IsReady { get; private set; }

    /// <summary>
    /// 当前有效的 DigGame 关卡顺序。
    /// 调用边界：仅在 IsReady 为 true 后读取；调用方不得修改返回集合或其中的生成配置对象。
    /// </summary>
    public IReadOnlyList<DigLevelSort> LevelSorts => _levelSorts;

    /// <summary>当前有效排序的运行时版本；用于拒绝刷新前的旧选关请求，不写入存档。</summary>
    public int ConfigGeneration { get; private set; }

    #endregion

    #region Initialization

    /// <summary>
    /// 从 Luban 表构建 DigGame 关卡顺序，按需应用 diggame_config 远端排序，并在末尾补充 DigLevel_999。
    /// 注意事项：方法幂等，同一实例完成初始化后重复调用不会重建序列。
    /// </summary>
    /// <param name="error">初始化失败原因；成功时为空。</param>
    /// <returns>关卡数据与排序均可用时返回 true。</returns>
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
            ConfigManager.Instance.Tables?.TbDigLevelSort == null ||
            ConfigManager.Instance.Tables.TbDigLevelData == null)
        {
            error = "Dig config tables are not initialized.";
            return false;
        }

        _levelDataTable = ConfigManager.Instance.Tables.TbDigLevelData;
        if (!TryBuildLocalSort(out List<DigLevelSort> localCandidate, out error))
        {
            _levelDataTable = null;
            return false;
        }

        _levelSorts.Clear();
        _levelSorts.AddRange(localCandidate);

        SyncRemoteConfig();
        if (_levelSorts.Count == 0)
        {
            error = "Dig level sequence is empty after initialization.";
            _levelDataTable = null;
            return false;
        }

        IsReady = true;
        ConfigGeneration = 1;
        Guru.SDK.Framework.Utils.Log.Log.I($"[DigRemoteConfig] stage=initial_sequence, generation={ConfigGeneration}, count={_levelSorts.Count}, paths=[{string.Join(",", _levelSorts.ConvertAll(item => item.Path))}].");
        return true;
    }

    private void SyncRemoteConfig()
    {
        if (!Application.isPlaying)
            return;

        if (!ConfigManager.RemoteConfig.TryGetRaw(
                ConfigManager.RemoteConfig.DigGameConfigKey,
                out string json))
            return;

        if (!TryBuildRemoteConfigCandidate(json, out List<DigLevelSort> candidate, out string error))
        {
            Guru.SDK.Framework.Utils.Log.Log.W(
                $"[DigRemoteConfig] stage=initial_remote_rejected, reason={error}; local sequence retained.");
            return;
        }

        _levelSorts.Clear();
        _levelSorts.AddRange(candidate);
        this.Log($"[DigGameLevelCtrl] Replace Dig level sort success: {_levelSorts.Count}");
    }

    private bool TryBuildLocalSort(out List<DigLevelSort> candidate, out string error)
    {
        candidate = null;
        error = null;
        List<DigLevelSort> localSorts = ConfigManager.Instance?.Tables?.TbDigLevelSort?.DataList;
        if (localSorts == null || localSorts.Count == 0)
        {
            error = "TbDigLevelSort contains no rows.";
            return false;
        }

        var paths = new List<string>(localSorts.Count);
        for (int i = 0; i < localSorts.Count; i++)
        {
            paths.Add(localSorts[i]?.Path);
        }

        return TryBuildLevelSort(paths, out candidate, out error);
    }

    #endregion

    #region 运行时远端排序提交

    /// <summary>
    /// 从候选 JSON 构建排序；仅在初次初始化完成后调用，不改缓存、当前序列或存档。
    /// 无效字段、重复/未知关卡或空资源路径拒绝整份候选；关闭远端排序时恢复本地顺序。
    /// </summary>
    internal bool TryPrepareRemoteConfig(string json, out List<DigLevelSort> candidate, out string error)
    {
        candidate = null;
        error = null;
        if (!IsReady)
        {
            error = "Dig level controller is not ready.";
            return false;
        }

        return TryBuildRemoteConfigCandidate(json, out candidate, out error);
    }

    private bool TryBuildRemoteConfigCandidate(
        string json,
        out List<DigLevelSort> candidate,
        out string error)
    {
        candidate = null;
        error = null;
        try
        {
            JObject config = JObject.Parse(json);
            JToken enabled = config[ConfigManager.RemoteConfig.EnabledFieldKey];
            if (enabled != null && enabled.Type != JTokenType.Boolean)
            {
                error = "Dig enabled must be a boolean.";
                return false;
            }

            var paths = new List<string>();
            if (enabled?.Value<bool>() == true)
            {
                if (config[ConfigManager.RemoteConfig.LevelSortKey] is not JArray remotePaths || remotePaths.Count == 0)
                {
                    error = "Enabled Dig level_sort must be a nonempty array.";
                    return false;
                }
                foreach (JToken path in remotePaths)
                {
                    if (path.Type != JTokenType.String && path.Type != JTokenType.Integer)
                    {
                        error = "Dig level_sort must contain integer or string IDs.";
                        return false;
                    }
                    paths.Add(path.Value<string>());
                }
            }
            else
            {
                return TryBuildLocalSort(out candidate, out error);
            }

            return TryBuildLevelSort(paths, out candidate, out error);
        }
        catch (Exception exception)
        {
            error = $"Invalid Dig configuration: {exception.Message}";
            return false;
        }
    }

    private bool TryBuildLevelSort(IReadOnlyList<string> paths, out List<DigLevelSort> candidate, out string error)
    {
        candidate = new List<DigLevelSort>();
        error = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in paths)
        {
            DigLevelData data = GetLevelDataById(path);
            if (string.IsNullOrWhiteSpace(path) || !seen.Add(path) || data == null || string.IsNullOrWhiteSpace(data.Path))
            {
                error = $"Dig level_sort contains an invalid or duplicate ID: {path}.";
                candidate = null;
                return false;
            }
            if (path == WaitingForNextLevelPath) continue;

            var item = new JSONObject { ["id"] = candidate.Count + 1, ["path"] = path };
            candidate.Add(DigLevelSort.DeserializeDigLevelSort(item));
        }

        DigLevelData waiting = GetLevelDataById(WaitingForNextLevelPath);
        if (candidate.Count == 0 || waiting == null || string.IsNullOrWhiteSpace(waiting.Path))
        {
            error = "Dig sequence has no playable levels or no valid waiting level.";
            candidate = null;
            return false;
        }
        var last = new JSONObject { ["id"] = candidate.Count + 1, ["path"] = WaitingForNextLevelPath };
        candidate.Add(DigLevelSort.DeserializeDigLevelSort(last));
        return true;
    }

    /// <summary>比较候选序列的 ID 与真实内容；仅用于提交判断，不修改候选或有效序列。</summary>
    internal bool HasSameLevelSort(IReadOnlyList<DigLevelSort> candidate)
    {
        if (candidate == null || candidate.Count != _levelSorts.Count) return false;
        for (int i = 0; i < candidate.Count; i++)
        {
            if (candidate[i].Id != _levelSorts[i].Id || candidate[i].Path != _levelSorts[i].Path) return false;
        }
        return true;
    }

    /// <summary>
    /// 缓存保存成功后同步提交已验证候选；仅由 RemoteConfigService 在安全边界调用。
    /// 不重建正在玩的关卡，不修改通关/解锁进度；同序列保持版本不变。
    /// </summary>
    internal void ApplyRemoteConfig(List<DigLevelSort> candidate)
    {
        if (HasSameLevelSort(candidate))
        {
            Guru.SDK.Framework.Utils.Log.Log.I($"[DigRemoteConfig] stage=sequence_unchanged, generation={ConfigGeneration}, count={_levelSorts.Count}.");
            return;
        }
        _levelSorts.Clear();
        _levelSorts.AddRange(candidate);
        ConfigGeneration++;
        Guru.SDK.Framework.Utils.Log.Log.I($"[DigRemoteConfig] stage=sequence_applied, generation={ConfigGeneration}, count={_levelSorts.Count}, paths=[{string.Join(",", _levelSorts.ConvertAll(item => item.Path))}].");
        this.Log($"[DigGameLevelCtrl] Applied remote sort. generation:{ConfigGeneration}, count:{_levelSorts.Count}");
    }

    /// <summary>校验选关数据仍属于当前版本及真实关卡；旧 UI/广告请求不得借新序号进入其它内容。</summary>
    internal bool IsCurrentSelection(int generation, DigLevelSort sort, DigLevelData data)
    {
        return IsReady && generation == ConfigGeneration && sort != null && data != null &&
               GetLevelSortById(sort.Id)?.Path == sort.Path && sort.Path == data.Id &&
               ReferenceEquals(GetLevelDataById(data.Id), data);
    }

    #endregion

    #region Dig 进度查询
    /// <summary>与旧入口兼容的只读序列；包括末尾 TBC。</summary>
    public IReadOnlyList<DigLevelSort> Levels => LevelSorts;
    /// <summary>按当前有效排序取得数据；不能用于重新解释正在运行的关卡。</summary>
    public bool TryGetLevel(int id, out DigLevelData data)
    {
        data = GetLevelDataById(GetLevelSortById(id)?.Path);
        return data != null;
    }
    /// <summary>OneLink 无匹配路径时选择第一个未通关排序项；普通关全通后进入 TBC。</summary>
    public int ResolveLowestUnpassed()
    {
        foreach (var sort in _levelSorts)
            if (sort.Path == TbcDataId || !DB.DigGame.PassedLevels.Contains(sort.Id)) return sort.Id;
        return 1;
    }
    /// <summary>仅查询当前配置中仍有效的存档关号，失效时回退首项。</summary>
    public int ResolveCurrentLevel() => GetLevelSortById(DB.DigGame.CurrentLevel)?.Id ?? _levelSorts[0].Id;
    /// <summary>只读判断当前排序是否首胜，TBC 不参与普通通关。</summary>
    public bool IsFirstVictory(int id) => !DB.DigGame.PassedLevels.Contains(id);
    /// <summary>保存排序通关与后继解锁进度；星级已由 DigPlayCtrl 在胜利面板创建前独立保存，TBC 永远不写入。</summary>
    public bool RecordVictory(int id, DigLevelData data)
    {
        if (data == null || data.Id == TbcDataId) return false;
        var save = DB.DigGame;
        save.EnsureValidCollections();
        bool first = !save.PassedLevels.Contains(id);
        if (first) save.PassedLevels.Add(id);
        save.MaxLevel = Math.Max(save.MaxLevel, id);
        save.CurrentLevel = id;
        // 当前排序仍映射原内容时，才顺序解锁下一关，避免云控重排后解锁错误内容。
        int index = _levelSorts.FindIndex(item => item.Id == id && item.Path == data.Id);
        if (index >= 0 && index + 1 < _levelSorts.Count)
        {
            int next = _levelSorts[index + 1].Id;
            if (!save.UnlockLevels.Contains(next)) save.UnlockLevels.Add(next);
            save.CurrentLevel = next;
        }
        if (!DB.SaveDigGame()) this.LogError("[Dig] 通关进度保存失败。");
        return first;
    }
    #endregion

    #region Queries

    /// <summary>
    /// 按 DigLevelData.Id 查询关卡配置。
    /// 调用边界：选关、进关等运行时流程在控制器完成初始化后使用。
    /// </summary>
    public DigLevelData GetLevelDataById(string id)
    {
        if (!IsReady && _levelDataTable == null)
            return null;

        if (string.IsNullOrWhiteSpace(id))
            return null;

        return _levelDataTable?.GetOrDefault(id);
    }

    /// <summary>
    /// 按玩家可见的 DigLevelSort.Id 查询当前排序项。
    /// 调用边界：查询当前有效序列，找不到时返回 null；运行中的关卡不得据此反推进入时的真实身份。
    /// </summary>
    public DigLevelSort GetLevelSortById(int id)
    {
        return _levelSorts.Find(data => data != null && data.Id == id);
    }

    /// <summary>
    /// 获取未完待续关卡及其在当前有效排序中的最后一个前置关卡。
    /// 调用边界：仅用于运行时判定 DigLevel_999 的顺序解锁条件；不读取存档，也不修改排序。
    /// </summary>
    /// <param name="lastPlayableSort">DigLevel_999 前面的最后一个有效关卡。</param>
    /// <param name="waitingSort">未完待续关卡 DigLevel_999。</param>
    /// <returns>同时找到两个有效排序项时返回 true。</returns>
    public bool TryGetWaitingLevelUnlockPair(
        out DigLevelSort lastPlayableSort,
        out DigLevelSort waitingSort)
    {
        lastPlayableSort = null;
        waitingSort = null;

        if (!IsReady)
            return false;

        int waitingIndex = _levelSorts.FindIndex(data =>
            data != null && data.Path == WaitingForNextLevelPath);
        if (waitingIndex <= 0)
            return false;

        waitingSort = _levelSorts[waitingIndex];
        for (int i = waitingIndex - 1; i >= 0; i--)
        {
            if (_levelSorts[i] == null)
                continue;

            lastPlayableSort = _levelSorts[i];
            return true;
        }

        waitingSort = null;
        return false;
    }

    /// <summary>
    /// 批量解锁当前最终有效排序中的全部 DigGame 关卡，并在全部写入后统一保存一次。
    /// 调用边界：仅供运行态 GM/调试功能使用；不会写入通关记录、星级或修改当前关卡。
    /// </summary>
    /// <param name="addedCount">本次实际新增的解锁关卡数量。</param>
    /// <returns>控制器与 DigGame 存档可用且保存成功时返回 true。</returns>
    public bool TryUnlockAllLevels(out int addedCount)
    {
        addedCount = 0;
        if (!IsReady || _levelSorts.Count == 0)
        {
            this.LogWarning(
                "[DigGameLevelCtrl] Unlock all levels skipped because the controller is not ready.");
            return false;
        }

        Bear.SaveModule.DigGame saveData = DB.DigGame;
        if (saveData == null)
        {
            this.LogWarning(
                "[DigGameLevelCtrl] Unlock all levels skipped because DB.DigGame is unavailable.");
            return false;
        }

        bool changed = saveData.EnsureValidCollections();
        for (int i = 0; i < _levelSorts.Count; i++)
        {
            DigLevelSort sortData = _levelSorts[i];
            if (sortData == null ||
                sortData.Id <= 0 ||
                saveData.UnlockLevels.Contains(sortData.Id))
            {
                continue;
            }

            saveData.UnlockLevels.Add(sortData.Id);
            addedCount++;
            changed = true;
        }

        if (!changed || DB.SaveDigGame())
            return true;

        this.LogWarning("[DigGameLevelCtrl] Failed to save unlocked DigGame levels.");
        return false;
    }

    #endregion
}

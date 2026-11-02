using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Game.Events;
using UnityEngine;

/// <summary>
/// 管理 postcard 数据的中心类
/// 
/// verison 1.0 监听 PostcardGetStarEvent,DB.Postcard.LevelWaitingAnimStar 记录对应关卡新获得的 star 数量。
/// Victory 弹出的时候清空对应关卡数据。
/// 需要 Get，Clear，ClearAll，Cache 等内容 API
/// 
/// </summary>
public class PostcardManager : IDisposable, IDebuger
{
    #region Runtime State

    private EventSubscriber subscriber;

    #endregion

    #region Lifecycle

    /// <summary>
    /// 用途：创建明信片数据管理器并订阅明信片星星收集完成事件。
    /// 调用边界：仅应由游戏全局管理流程在运行时初始化阶段创建，避免重复创建导致事件重复订阅。
    /// 注意事项：构造函数不读取或写入存档数据，实际数据写入只在事件触发或外部调用 API 时发生。
    /// </summary>
    public PostcardManager()
    {
        AddListener();
    }

    /// <summary>
    /// 用途：释放明信片管理器持有的事件订阅，避免全局事件回调残留。
    /// 调用边界：游戏全局管理器销毁、重建或明确不再使用 PostcardManager 时调用。
    /// 注意事项：该方法只清理订阅，不会清空 DB.Postcard.LevelWaitingAnimStar 中已经缓存的星星数量。
    /// </summary>
    public void Dispose()
    {
        subscriber?.Dispose();
        subscriber = null;
    }

    #endregion

    #region Public API

    /// <summary>
    /// 用途：判断当前关卡配置是否允许收集明信片星星。
    /// 调用边界：仅供运行时关卡流程在进入星星收集逻辑前调用。
    /// 注意事项：非运行态、当前关卡数据或存档无效、有效星星上限小于等于 0、已收集数量达到上限时返回 false；该方法仅读取永久收集记录，不写入存档。
    /// </summary>
    public static bool CanCurrentLevelCollectStar()
    {
        if (!Application.isPlaying)
            return false;

        Config.LevelData currentLevelData = PlayCtrl.Instance?.Level?.CurrentLevelData;
        if (currentLevelData == null
            || !IsValidLevelId(currentLevelData.Id))
        {
            return false;
        }

        int maxStarCount = Mathf.Clamp(currentLevelData.LevelSumStarCount, 0, 3);
        if (maxStarCount <= 0)
            return false;

        PostcardManager postcardManager = GameManager.Instance?.Postcard;
        if (postcardManager == null
            || !postcardManager.TryGetLevelCollectedStar(out _))
        {
            return false;
        }

        int collectedStarCount = postcardManager.GetLevelCollectedStarIds(currentLevelData.Id).Count;
        return collectedStarCount < maxStarCount;
    }

    /// <summary>
    /// 用途：获取指定关卡在本轮流程中新获得的明信片星星数量。
    /// 调用边界：胜利面板、调试工具或后续明信片表现逻辑需要读取临时计数时调用。
    /// 注意事项：levelId 为空或当前没有缓存数据时返回 0；该方法不会清空数据，也不会触发保存。
    /// </summary>
    public int GetCollectedStarCount(string levelId)
    {
        if (!IsValidLevelId(levelId))
            return 0;

        if (!TryGetLevelWaitingAnimStar(out Dictionary<string, int> levelCollectedStar))
            return 0;

        return levelCollectedStar.TryGetValue(levelId, out int count) ? Mathf.Max(0, count) : 0;
    }

    /// <summary>
    /// 用途：缓存指定关卡在本轮流程中新获得的明信片星星数量。
    /// 调用边界：PostcardManager 内部事件处理、调试工具或明确需要覆盖临时计数的流程可调用。
    /// 注意事项：starCount 小于等于 0 时会移除该关卡缓存；成功写入或移除后会保存 Postcard 数据。
    /// </summary>
    public bool CacheCollectedStarCount(string levelId, int starCount)
    {
        if (!IsValidLevelId(levelId))
            return false;

        if (!TryGetLevelWaitingAnimStar(out Dictionary<string, int> levelCollectedStar))
            return false;

        int normalizedCount = Mathf.Max(0, starCount);
        bool changed;
        if (normalizedCount <= 0)
        {
            changed = levelCollectedStar.Remove(levelId);
        }
        else
        {
            changed = !levelCollectedStar.TryGetValue(levelId, out int currentCount)
                || currentCount != normalizedCount;
            levelCollectedStar[levelId] = normalizedCount;
        }

        return !changed || SavePostcardData("CacheCollectedStarCount");
    }

    /// <summary>
    /// 用途：读取指定关卡已经永久收集的明信片星星 id 列表。
    /// 调用边界：胜利面板刷新星星展示、关卡内星星初始化状态时调用。
    /// 注意事项：返回的是新列表，调用方修改返回值不会直接改动存档；levelId 无效或存档不可用时返回空列表。
    /// </summary>
    public List<int> GetLevelCollectedStarIds(string levelId)
    {
        List<int> result = new List<int>();
        if (!IsValidLevelId(levelId))
            return result;

        if (!TryGetLevelCollectedStar(out Dictionary<string, List<int>> levelCollectedStar))
            return result;

        if (!levelCollectedStar.TryGetValue(levelId, out List<int> collectedStars) || collectedStars == null)
            return result;

        HashSet<int> addedStarIds = new HashSet<int>();
        for (int i = 0; i < collectedStars.Count; i++)
        {
            int starId = collectedStars[i];
            if (IsValidStarId(starId) && addedStarIds.Add(starId))
                result.Add(starId);
        }

        return result;
    }

    /// <summary>
    /// 用途：判断指定关卡的单个明信片星星是否已经永久收集。
    /// 调用边界：关卡内 Postcard_Star 初始化或触发收集前做状态检查时调用。
    /// 注意事项：只读取收集状态，不写入等待动画计数，也不会发放背包道具。
    /// </summary>
    public bool IsLevelStarCollected(string levelId, int starId)
    {
        if (!IsValidLevelId(levelId) || !IsValidStarId(starId))
            return false;

        if (!TryGetLevelCollectedStar(out Dictionary<string, List<int>> levelCollectedStar))
            return false;

        return levelCollectedStar.TryGetValue(levelId, out List<int> collectedStars)
            && collectedStars != null
            && collectedStars.Contains(starId);
    }

    /// <summary>
    /// 用途：写入指定关卡单个明信片星星的永久收集记录。
    /// 调用边界：仅由关卡内 Postcard_Star 在确认玩家触发收集后调用；调用方负责后续背包奖励和事件派发。
    /// 注意事项：已有星星会返回成功且 isNewCollection 为 false；保存失败时回滚本次新增 id。
    /// </summary>
    public bool TryCollectLevelStar(string levelId, int starId, out bool isNewCollection)
    {
        isNewCollection = false;
        if (!IsValidLevelId(levelId) || !IsValidStarId(starId))
            return false;

        if (!TryGetLevelCollectedStar(out Dictionary<string, List<int>> levelCollectedStar))
            return false;

        bool createdCollectedEntry = false;
        if (!levelCollectedStar.TryGetValue(levelId, out List<int> collectedStars)
            || collectedStars == null)
        {
            collectedStars = new List<int>();
            levelCollectedStar[levelId] = collectedStars;
            createdCollectedEntry = true;
        }

        if (collectedStars.Contains(starId))
            return true;

        collectedStars.Add(starId);
        isNewCollection = true;

        if (SavePostcardData("TryCollectLevelStar"))
            return true;

        collectedStars.Remove(starId);
        if (createdCollectedEntry && collectedStars.Count <= 0)
            levelCollectedStar.Remove(levelId);

        isNewCollection = false;
        return false;
    }

    /// <summary>
    /// 用途：为指定关卡补充配置的明信片胜利默认星、增加同等数量的背包 Star，并缓存胜利面板需要播放的新增星星数量。
    /// 调用边界：由首次通关流程在胜利面板创建前调用，或由 LoadManager 启动后的历史通关补录流程通过 TryGetAllCompleteLevelStar 调用。
    /// 注意事项：会同时读取 LevelVictoryStarCount 与 LevelSumStarCount，并将发放数量限制在 0 到 3；胜利默认星 id 从 3 递减到 1，已有 id 不重复写入或发奖；Postcard 保存失败时会回滚本次新增数据。
    /// </summary>
    public bool TryGrantVictoryStars(Config.LevelData levelData)
    {
        if (levelData == null || !IsValidLevelId(levelData.Id))
            return false;

        string levelId = levelData.Id;
        int normalizedMaxCount = Mathf.Clamp(levelData.LevelSumStarCount, 0, 3);
        int normalizedCount = Mathf.Clamp(levelData.LevelVictoryStarCount, 0, normalizedMaxCount);
        if (normalizedCount <= 0)
            return true;

        if (!TryGetLevelCollectedStar(out Dictionary<string, List<int>> levelCollectedStar)
            || !TryGetLevelWaitingAnimStar(out Dictionary<string, int> levelWaitingAnimStar))
        {
            return false;
        }

        var bag = GameManager.Instance?.Bag;
        if (bag == null || DB.GameData == null)
        {
            this.LogWarning($"[PostcardManager] Grant victory stars skipped because Bag or GameData is missing. levelId:{levelId}");
            return false;
        }

        bool createdCollectedEntry = false;
        if (!levelCollectedStar.TryGetValue(levelId, out List<int> collectedStars)
            || collectedStars == null)
        {
            collectedStars = new List<int>();
            levelCollectedStar[levelId] = collectedStars;
            createdCollectedEntry = true;
        }

        List<int> addedStarIds = new List<int>();
        for (int starId = 3; starId > 3 - normalizedCount; starId--)
        {
            if (collectedStars.Contains(starId))
                continue;

            collectedStars.Add(starId);
            addedStarIds.Add(starId);
        }

        if (addedStarIds.Count <= 0)
        {
            if (createdCollectedEntry && collectedStars.Count <= 0)
                levelCollectedStar.Remove(levelId);

            return true;
        }

        bool hadWaitingCount = levelWaitingAnimStar.TryGetValue(levelId, out int originalWaitingCount);
        levelWaitingAnimStar[levelId] = Mathf.Max(0, originalWaitingCount) + addedStarIds.Count;

        if (SavePostcardData("TryGrantVictoryStars"))
        {
            bag.AddTool(
                Config.Game.GameProps.Star,
                addedStarIds.Count,
                GameCommon.RewardType.Other);
            return true;
        }

        for (int i = 0; i < addedStarIds.Count; i++)
            collectedStars.Remove(addedStarIds[i]);

        if (createdCollectedEntry && collectedStars.Count <= 0)
            levelCollectedStar.Remove(levelId);

        if (hadWaitingCount)
            levelWaitingAnimStar[levelId] = originalWaitingCount;
        else
            levelWaitingAnimStar.Remove(levelId);

        return false;
    }

    /// <summary>
    /// 用途：清空指定关卡在本轮流程中新获得的明信片星星数量。
    /// 调用边界：胜利面板消费完当前关卡星星表现数据后调用，也可用于调试清理单关临时数据。
    /// 注意事项：只移除 DB.Postcard.LevelWaitingAnimStar 的对应 levelId，不会影响永久星星收集记录 LevelCollectedStar。
    /// </summary>
    public bool ClearCollectedStarCount(string levelId)
    {
        if (!IsValidLevelId(levelId))
            return false;

        if (!TryGetLevelWaitingAnimStar(out Dictionary<string, int> levelCollectedStar))
            return false;

        if (!levelCollectedStar.Remove(levelId))
            return true;

        return SavePostcardData("ClearCollectedStarCount");
    }

    /// <summary>
    /// 用途：清空所有关卡在本轮流程中新获得的明信片星星数量缓存。
    /// 调用边界：调试重置、活动状态重置或明确需要丢弃全部临时星星表现数据时调用。
    /// 注意事项：只清空 LevelWaitingAnimStar 临时字典，不会影响明信片完成状态、章节进度或 LevelCollectedStar 永久星星收集记录。
    /// </summary>
    public bool ClearAllCollectedStarCounts()
    {
        if (!TryGetLevelWaitingAnimStar(out Dictionary<string, int> levelCollectedStar))
            return false;

        if (levelCollectedStar.Count <= 0)
            return true;

        levelCollectedStar.Clear();
        return SavePostcardData("ClearAllCollectedStarCounts");
    }

    /// <summary>
    /// 用途：读取并清空指定关卡在本轮流程中新获得的明信片星星数量，供胜利流程一次性消费。
    /// 调用边界：Victory 面板弹出或胜利表现初始化时调用，调用方应保存返回值用于本次表现。
    /// 注意事项：返回值为读取到的数量；清空保存失败时会记录警告，但仍返回本次读取值，避免阻断胜利流程。
    /// </summary>
    public int ConsumeCollectedStarCount(string levelId)
    {
        int count = GetCollectedStarCount(levelId);
        if (count <= 0)
            return 0;

        if (!ClearCollectedStarCount(levelId))
            this.LogWarning($"[PostcardManager] ConsumeCollectedStarCount clear failed. levelId:{levelId}, count:{count}");

        return count;
    }

    
    private const int Postcard_NeededStarCount = 9;
    /// <summary>
    /// 补充 star
    /// </summary>
    public void FullNeededStarCountForGuide()
    {
        var bag = GameManager.Instance.Bag;
        var count = bag.GetToolCount(Config.Game.GameProps.Star);
        var dt = count - Postcard_NeededStarCount;
        if (dt < 0)
        {
            bag.AddTool(Config.Game.GameProps.Star, Math.Abs(dt), GameCommon.RewardType.Other);
        }
    }

    /// <summary>
    /// 用途：在游戏加载完成后，为历史已通关关卡补录配置的胜利默认星和对应 Bag Star。
    /// 调用边界：由 LoadManager.OnPostProcess 在 PlayCtrl 与配置初始化完成后调用。
    /// </summary>
    /// <remarks>
    /// 仅补录 LevelSumStarCount 大于 0 的已通关关卡；新增星星会通过 TryGrantVictoryStars 按总星数上限同步累加到 LevelWaitingAnimStar 和 Bag Star。
    /// </remarks>
    public void TryGetAllCompleteLevelStar()
    {
        // 每次加载都会检查历史通关记录；已有星 id 由 TryGrantVictoryStars 去重。
        if (!Application.isPlaying)
            return;

        var passedLevels = DB.GameData?.PassedLevels;
        if (passedLevels == null || passedLevels.Count <= 0)
            return;

        LevelCtrl levelCtrl = PlayCtrl.Instance?.Level;
        PostcardManager postcardManager = GameManager.Instance?.Postcard;
        if (levelCtrl == null || postcardManager == null)
        {
            this.LogWarning("[PostcardGuidePopup] Complete level stars skipped because LevelCtrl or PostcardManager is missing.");
            return;
        }

        HashSet<int> handledLevelSortIds = new HashSet<int>();
        for (int i = 0; i < passedLevels.Count; i++)
        {
            int levelSortId = passedLevels[i];
            if (!handledLevelSortIds.Add(levelSortId))
                continue;

            Config.LevelSort levelSort = levelCtrl.GetLevelSortById(levelSortId);
            if (levelSort == null || string.IsNullOrWhiteSpace(levelSort.Path))
            {
                this.LogWarning($"[PostcardGuidePopup] Complete level star skipped because LevelSort is missing. sortId:{levelSortId}");
                continue;
            }

            Config.LevelData levelData = levelCtrl.GetLevelDataById(levelSort.Path);
            if (levelData == null || string.IsNullOrWhiteSpace(levelData.Id))
            {
                this.LogWarning($"[PostcardGuidePopup] Complete level star skipped because LevelData is missing. sortId:{levelSortId}, path:{levelSort.Path}");
                continue;
            }

            if (!postcardManager.TryGrantVictoryStars(levelData))
            {
                this.LogWarning($"[PostcardGuidePopup] Grant complete level stars failed. levelId:{levelData.Id}, count:{levelData.LevelVictoryStarCount}, maxCount:{levelData.LevelSumStarCount}");
            }
        }
    }

    /// <summary>
    /// 用途：记录首次明信片引导即将进入 PostcardPopup，并设置 Popup 待消费的引导状态。
    /// 调用边界：仅在创建引导用 PostcardPopup 之前调用，确保 Popup 打开首帧即可开启本地交互遮罩。
    /// 注意事项：会写入并保存 HasGuide/IsGuiding；IsGuiding 随后在第二阶段关闭后由 PostcardPopup 启动 GuidePart 时消费。
    /// </summary>
    public void StartGuding()
    {
        DB.Postcard.HasGuide = true;
        DB.Postcard.IsGuiding = true;
        DB.Postcard.Save();
    }

    /// <summary>
    /// 用途：清除首次明信片引导的完成标记与待启动状态，供显式重置或中止流程使用。
    /// 调用边界：仅由确认需要重新开放完整引导的管理流程调用；正常 Popup 启动不调用本方法。
    /// 注意事项：会立即保存 DB，误用会让首次引导重新满足展示条件。
    /// </summary>
    public void CloseGuding()
    {
        DB.Postcard.HasGuide = false;
        DB.Postcard.IsGuiding = false;
        DB.Postcard.Save();
    }

    #endregion

    #region Event Handling

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<PostcardGetStarEvent>(OnPostcardGetStar);
    }

    private void OnPostcardGetStar(PostcardGetStarEvent evt)
    {
        if (!TryGetCurrentLevelId(out string levelId))
        {
            this.LogWarning("[PostcardManager] PostcardGetStarEvent ignored because current level id is missing.");
            return;
        }

        int nextCount = GetCollectedStarCount(levelId) + 1;
        if (!CacheCollectedStarCount(levelId, nextCount))
            this.LogWarning($"[PostcardManager] Cache collected star failed. levelId:{levelId}, count:{nextCount}");
    }

    #endregion

    #region Helpers

    private static bool IsValidLevelId(string levelId)
    {
        return !string.IsNullOrWhiteSpace(levelId);
    }

    private static bool IsValidStarId(int starId)
    {
        return starId >= 1 && starId <= 3;
    }

    private static bool TryGetCurrentLevelId(out string levelId)
    {
        levelId = string.Empty;
        PlayCtrl playCtrl = PlayCtrl.Instance;
        if (playCtrl == null || playCtrl.Level == null)
            return false;

        Config.LevelData currentLevelData = playCtrl.Level.CurrentLevelData;
        if (currentLevelData == null || string.IsNullOrWhiteSpace(currentLevelData.Id))
            return false;

        levelId = currentLevelData.Id;
        return true;
    }

    private bool TryGetLevelCollectedStar(out Dictionary<string, List<int>> levelCollectedStar)
    {
        levelCollectedStar = null;
        var postcardData = DB.Postcard;
        if (postcardData == null)
        {
            this.LogWarning("[PostcardManager] DB.Postcard is null.");
            return false;
        }

        if (postcardData.LevelCollectedStar == null)
            postcardData.LevelCollectedStar = new Dictionary<string, List<int>>();

        levelCollectedStar = postcardData.LevelCollectedStar;
        return true;
    }

    private bool TryGetLevelWaitingAnimStar(out Dictionary<string, int> levelCollectedStar)
    {
        levelCollectedStar = null;
        var postcardData = DB.Postcard;
        if (postcardData == null)
        {
            this.LogWarning("[PostcardManager] DB.Postcard is null.");
            return false;
        }

        if (postcardData.LevelWaitingAnimStar == null)
            postcardData.LevelWaitingAnimStar = new Dictionary<string, int>();

        levelCollectedStar = postcardData.LevelWaitingAnimStar;
        return true;
    }

    private bool SavePostcardData(string reason)
    {
        if (DB.SavePostcard())
            return true;

        this.LogWarning($"[PostcardManager] SavePostcard failed. reason:{reason}");
        return false;
    }

    #endregion
}

using System;
using System.Collections.Generic;
using Bear.Logger;
using Bear.SaveModule;
using Bear.UI;
using Config;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Dig 玩法选关面板。
/// 分页表现参考 ChoiceLevelPanel，但配置与进度分别读取 DigLevelSort、DigLevelData 和 DB.DigGame。
/// </summary>
public partial class DigGame_ChoiceLevelPanel : BaseUIView, IDebuger
{
    #region Serialized References

    /// <summary>
    /// 单页最多显示的 Dig 关卡数量；必须大于 0。
    /// </summary>
    [SerializeField, Min(1)] private int maxCount = 10;

    /// <summary>
    /// Dig 关卡项运行时容器，仅在 PlayMode 创建缓存项。
    /// </summary>
    [SerializeField] private RectTransform content;

    /// <summary>
    /// Dig 专用选关项 prefab；组件、配置、进度和星星均与主线选关项隔离。
    /// 可在 Inspector 中替换样式，但新 prefab 需完整绑定 DigGame_LevelChoiceItem 的显式引用。
    /// </summary>
    [FormerlySerializedAs("itemPrefab")]
    [SerializeField] private DigGame_LevelChoiceItem choiceLevelItem;

    /// <summary>
    /// 当前 Dig 选关项 prefab 的只读访问口。
    /// 调用边界：供编辑器工具或样式检查读取，运行时不得替换引用。
    /// </summary>
    public DigGame_LevelChoiceItem ChoiceLevelItem => choiceLevelItem;

    /// <summary>
    /// Dig 关卡项布局组件，分页刷新后用于重建布局。
    /// </summary>
    [SerializeField] private RowFirstLayoutGroup gridLayout;

    /// <summary>
    /// 上一页按钮，只切换 DigLevelSort 分页。
    /// </summary>
    [SerializeField] private CustomButton lastBtn;

    /// <summary>
    /// 下一页按钮，只切换 DigLevelSort 分页。
    /// </summary>
    [SerializeField] private CustomButton nextBtn;

    /// <summary>
    /// 关闭按钮，退出当前 Dig 控制器并恢复 StartPanel。
    /// </summary>
    [SerializeField] private CustomButton closeBtn;

    /// <summary>
    /// 主线与 Dig 共用的快速分页拖动组件；拖动跨页时请求立即刷新，不读取 Dig 配置或存档。
    /// </summary>
    [SerializeField] private LevelChoicePanel_QuickTurn quickTurnComp;

    #endregion

    #region Runtime State

    private readonly List<DigGame_LevelChoiceItem> _itemCache = new();
    private List<DigLevelSort> _sortData = new();
    private DigGameLevelCtrl _levelCtrl;
    private int _pageIndex;
    private bool _isReady;
    private int _configGeneration;
    private bool _isRewardAdPending;

    /// <summary>面板当前显示的数据版本；选关项绑定及广告回调使用此版本校验身份。</summary>
    internal int ConfigGeneration => _configGeneration;

    /// <summary>选关奖励尚未返回时保护旧数据和页码；回调完整结束后才允许云控提交。</summary>
    internal bool IsRewardAdPending => _isRewardAdPending;

    #endregion

    #region Lifecycle

    public override void OnOpen()
    {
        base.OnOpen();

        if (!_isReady)
            return;

        if (_configGeneration != _levelCtrl.ConfigGeneration)
            RefreshRemoteConfig();
        TryUnlockWaitingLevelAndSave();
        RefreshInitialPageIndex();
        RefreshQuickTurn();
        RefreshButtons();
        RefreshItems();
    }

    public override void OnCreate()
    {
        base.OnCreate();

        if (!TryInitializeData())
            return;

        InitItems();
        InitButtons();
        RefreshQuickTurn();
        _isReady = true;
    }

    #endregion

    #region Initialization

    private bool TryInitializeData()
    {
        _levelCtrl = GameManager.Instance?.DigGameLevel;
        if (_levelCtrl == null || !_levelCtrl.IsReady)
        {
            this.LogError("[DigGame_ChoiceLevelPanel] DigGameLevelCtrl is not initialized.");
            return false;
        }

        if (content == null ||
            choiceLevelItem == null ||
            lastBtn == null ||
            nextBtn == null ||
            closeBtn == null ||
            quickTurnComp == null)
        {
            this.LogError("[DigGame_ChoiceLevelPanel] Required prefab references are missing.");
            return false;
        }

        maxCount = Mathf.Max(1, maxCount);
        _sortData = new List<DigLevelSort>(_levelCtrl.LevelSorts);
        _configGeneration = _levelCtrl.ConfigGeneration;

        DigGame saveData = DB.DigGame;
        if (saveData == null)
        {
            this.LogError("[DigGame_ChoiceLevelPanel] DB.DigGame is unavailable.");
            return false;
        }

        bool saveChanged = saveData.EnsureValidCollections();
        if (_sortData.Count > 0)
        {
            int firstSortId = _sortData[0].Id;
            if (!saveData.UnlockLevels.Contains(firstSortId))
            {
                saveData.UnlockLevels.Add(firstSortId);
                saveChanged = true;
            }

        }

        saveChanged |= TryUnlockWaitingLevel(saveData);

        if (saveChanged && !DB.SaveDigGame())
            this.LogWarning("[DigGame_ChoiceLevelPanel] Failed to save repaired DigGame data.");

        return true;
    }

    private void InitItems()
    {
        for (int i = _itemCache.Count; i < maxCount; i++)
        {
            DigGame_LevelChoiceItem digItem = Instantiate(choiceLevelItem, content);
            digItem.Init(this);
            _itemCache.Add(digItem);
        }

        RefreshInitialPageIndex();
        RefreshItems();
    }

    private void InitButtons()
    {
        lastBtn.OnClick += LastPage;
        nextBtn.OnClick += NextPage;
        closeBtn.OnClick += ClosePanel;
        RefreshButtons();
    }

    private void RefreshQuickTurn()
    {
        quickTurnComp?.Initialize(
            _sortData.Count,
            maxCount,
            _pageIndex,
            GetQuickTurnCurrentPageIndex,
            OnQuickTurnPageChanged,
            OnQuickTurnPageSelected);
        quickTurnComp?.SetInteractable(!_isRewardAdPending);
    }

    private int GetQuickTurnCurrentPageIndex() => _pageIndex;

    private void ClosePanel(CustomButton button)
    {
        if (_isRewardAdPending) return;
        if (!GameManager.Instance.TryExitDigLevelSelection(out string error))
            this.LogError($"[DigGame_ChoiceLevelPanel] Exit level selection failed. error:{error}");
    }

    #endregion

    #region 远端更新与广告边界

    /// <summary>
    /// 配置成功提交后重取有效排序并刷新页码、按钮、关卡与星星；不重建面板或发放奖励。
    /// 未打开的面板在下次 OnOpen 更新；广告期间由提交方延后，保留原请求身份。
    /// </summary>
    internal void RefreshRemoteConfig()
    {
        if (!Application.isPlaying) return;
        if (!_isReady || !IsOpen || _isRewardAdPending)
        {
            Guru.SDK.Framework.Utils.Log.Log.I($"[DigRemoteConfigUI] stage=choice_skipped, ready={_isReady}, open={IsOpen}, rewardAd={_isRewardAdPending}, viewGeneration={_configGeneration}.");
            return;
        }
        _sortData = new List<DigLevelSort>(_levelCtrl.LevelSorts);
        _configGeneration = _levelCtrl.ConfigGeneration;
        _pageIndex = Math.Clamp(_pageIndex, 0, GetMaxPageIndex());
        RefreshQuickTurn();
        TryUnlockWaitingLevelAndSave();
        RefreshButtons();
        RefreshItems();
        Guru.SDK.Framework.Utils.Log.Log.I($"[DigRemoteConfigUI] stage=choice_refreshed, generation={_configGeneration}, count={_sortData.Count}, page={_pageIndex}.");
    }

    /// <summary>开始选关广告前验证当前显示版本；成功后冻结分页/关闭，直到原广告结果处理完毕。</summary>
    internal bool TryBeginRewardAd(int generation, DigLevelSort sort, DigLevelData data)
    {
        if (_isRewardAdPending || !IsValidSelection(generation, sort, data)) return false;
        _isRewardAdPending = true;
        quickTurnComp?.SetInteractable(false);
        return true;
    }

    /// <summary>仅由选关广告的 finally 调用，释放排序提交门禁；不取消或补发任何奖励。</summary>
    internal void EndRewardAd()
    {
        _isRewardAdPending = false;
        quickTurnComp?.SetInteractable(true);
    }

    private bool IsValidSelection(int generation, DigLevelSort sort, DigLevelData data)
    {
        if (!_isReady || !IsOpen || generation != _configGeneration ||
            !_levelCtrl.IsCurrentSelection(generation, sort, data))
        {
            this.LogWarning("[DigGame_ChoiceLevelPanel] Stale or invalid Dig selection rejected.");
            return false;
        }
        return true;
    }

    #endregion

    #region Pagination

    /// <summary>打开时优先显示当前有效关卡页，否则夹取保存的进关页；浏览与关闭不覆盖该记录。</summary>
    private void RefreshInitialPageIndex()
    {
        int maxPageIndex = GetMaxPageIndex();
        DigGame saveData = DB.DigGame;
        int currentSortIndex =
            saveData == null
                ? -1
                : _sortData.FindIndex(item => item.Id == saveData.CurrentLevel);

        _pageIndex = currentSortIndex >= 0 &&
                     _levelCtrl.GetLevelDataById(_sortData[currentSortIndex].Path) != null
            ? Math.Clamp(currentSortIndex / maxCount, 0, maxPageIndex)
            : Math.Clamp(saveData?.ChoiceLevelPanelPageIndex ?? 0, 0, maxPageIndex);
    }

    private int GetMaxPageIndex()
    {
        return _sortData.Count <= 0 ? 0 : (_sortData.Count - 1) / maxCount;
    }

    private void LastPage(CustomButton button)
    {
        ChangePage(Math.Max(0, _pageIndex - 1));
    }

    private void NextPage(CustomButton button)
    {
        ChangePage(Math.Min(GetMaxPageIndex(), _pageIndex + 1));
    }

    private void OnQuickTurnPageChanged(int targetPageIndex)
    {
        int nextPageIndex = Math.Clamp(targetPageIndex, 0, GetMaxPageIndex());
        if (_isRewardAdPending || nextPageIndex == _pageIndex)
            return;

        _pageIndex = nextPageIndex;
        RefreshButtons();
        RefreshItems();
    }

    private void OnQuickTurnPageSelected(int targetPageIndex)
    {
        quickTurnComp?.SetCurrentPage(_pageIndex);
    }

    private bool ChangePage(int nextPageIndex)
    {
        nextPageIndex = Math.Clamp(nextPageIndex, 0, GetMaxPageIndex());
        if (_isRewardAdPending || nextPageIndex == _pageIndex)
        {
            quickTurnComp?.SetCurrentPage(_pageIndex);
            return false;
        }

        for (int i = 0; i < _itemCache.Count; i++)
            _itemCache[i].SetCanvasGroupAlpha(0f);
        quickTurnComp?.SetCanvasGroupAlpha(0f);

        _pageIndex = nextPageIndex;
        quickTurnComp?.SetCurrentPage(_pageIndex);
        RefreshButtons();
        RefreshItems();

        for (int i = 0; i < _itemCache.Count; i++)
            _itemCache[i].SetCanvasGroupAlpha(1f, 0.2f);
        quickTurnComp?.SetCanvasGroupAlpha(1f, 0.2f);

        return true;
    }

    private void RefreshButtons()
    {
        lastBtn.gameObject.SetActive(_pageIndex > 0);
        nextBtn.gameObject.SetActive(_pageIndex < GetMaxPageIndex());
    }

    private void RefreshItems()
    {
        for (int i = 0; i < _itemCache.Count; i++)
        {
            int dataIndex = _pageIndex * maxCount + i;
            DigLevelSort sortData =
                dataIndex < _sortData.Count ? _sortData[dataIndex] : null;
            DigLevelData levelData =
                sortData == null ? null : _levelCtrl.GetLevelDataById(sortData.Path);

            if (sortData != null && levelData == null)
            {
                this.LogError(
                    $"[DigGame_ChoiceLevelPanel] DigLevelData not found. sortId:{sortData.Id}, dataId:{sortData.Path}");
            }

            _itemCache[i].SetData(sortData, levelData);
        }

        gridLayout?.RebuildLayout();
    }

    #endregion

    #region Item State

    private void TryUnlockWaitingLevelAndSave()
    {
        DigGame saveData = DB.DigGame;
        if (!TryUnlockWaitingLevel(saveData))
            return;

        if (!DB.SaveDigGame())
        {
            this.LogWarning(
                "[DigGame_ChoiceLevelPanel] Failed to save unlocked waiting level.");
        }
    }

    private bool TryUnlockWaitingLevel(DigGame saveData)
    {
        if (saveData == null ||
            saveData.PassedLevels == null ||
            saveData.UnlockLevels == null ||
            _levelCtrl == null ||
            !_levelCtrl.TryGetWaitingLevelUnlockPair(
                out DigLevelSort lastPlayableSort,
                out DigLevelSort waitingSort))
        {
            return false;
        }

        if (!saveData.PassedLevels.Contains(lastPlayableSort.Id) ||
            saveData.UnlockLevels.Contains(waitingSort.Id))
        {
            return false;
        }

        saveData.UnlockLevels.Add(waitingSort.Id);
        return true;
    }

    internal bool IsUnlocked(int sortId)
    {
        return DB.DigGame?.UnlockLevels?.Contains(sortId) == true;
    }

    internal bool IsPassed(int sortId)
    {
        return DB.DigGame?.PassedLevels?.Contains(sortId) == true;
    }

    internal bool IsCurrent(int sortId)
    {
        return DB.DigGame?.MaxLevel == sortId;
    }

    /// <summary>
    /// 广告解锁 Dig 关卡后立即进入。
    /// 调用边界：仅由当前面板持有的 DigGame_LevelChoiceItem 在奖励广告成功回调中调用。
    /// generation 必须是广告发起时的绑定版本，过期结果不得改写其它内容的解锁记录。
    /// </summary>
    public void UnlockAndEnter(DigLevelSort sortData, DigLevelData levelData, int generation)
    {
        if (!IsValidSelection(generation, sortData, levelData))
            return;

        DigGame saveData = DB.DigGame;
        if (saveData == null)
            return;

        if (!saveData.UnlockLevels.Contains(sortData.Id))
            saveData.UnlockLevels.Add(sortData.Id);

        if (!DB.SaveDigGame())
            this.LogWarning($"[DigGame_ChoiceLevelPanel] Failed to save unlocked level. sortId:{sortData.Id}");

        EnterLevel(sortData, levelData, generation);
    }

    /// <summary>
    /// 选择一条 DigLevelSort，并用其 Path 索引到的 DigLevelData 进入对应挖洞关卡。
    /// 注意事项：先校验绑定版本，排序 ID 写入 DigGame 进度，资源加载使用 DigLevelData.Path。
    /// </summary>
    public void EnterLevel(DigLevelSort sortData, DigLevelData levelData, int generation)
    {
        if (!TryEnterSelectedLevel(sortData, levelData, generation, out string error))
            this.LogError($"[DigGame_ChoiceLevelPanel] Enter Dig level failed. sortId:{sortData?.Id}, error:{error}");
    }

    /// <summary>OneLink 激活 Dig 后复用正常选关的存档、版本校验和进关链，返回同步受理结果。</summary>
    internal bool TryEnterOneLinkLevel(DigLevelSort sortData, DigLevelData levelData, out string error)
    {
        return TryEnterSelectedLevel(sortData, levelData, _configGeneration, out error);
    }

    private bool TryEnterSelectedLevel(DigLevelSort sortData, DigLevelData levelData, int generation, out string error)
    {
        error = null;
        if (!IsValidSelection(generation, sortData, levelData))
        {
            error = "Dig selection is stale or unavailable.";
            return false;
        }

        DigGame saveData = DB.DigGame;
        if (saveData == null)
        {
            this.LogError("[DigGame_ChoiceLevelPanel] Cannot enter level because DB.DigGame is unavailable.");
            error = "Dig save data is unavailable.";
            return false;
        }

        saveData.CurrentLevel = sortData.Id;
        saveData.ChoiceLevelPanelPageIndex = _pageIndex;
        if (!saveData.UnlockLevels.Contains(sortData.Id))
            saveData.UnlockLevels.Add(sortData.Id);

        if (!DB.SaveDigGame())
            this.LogWarning($"[DigGame_ChoiceLevelPanel] Failed to save selected level. sortId:{sortData.Id}");

        return GameManager.Instance.TryEnterDigLevel(sortData, levelData, out error);
    }

    #endregion

    #region Factory

    public static DigGame_ChoiceLevelPanel Create()
    {
        return UIManager.Instance.OpenUI<DigGame_ChoiceLevelPanel>(
            $"{typeof(DigGame_ChoiceLevelPanel).Name}",
            UILayer.Popup);
    }

    #endregion
}

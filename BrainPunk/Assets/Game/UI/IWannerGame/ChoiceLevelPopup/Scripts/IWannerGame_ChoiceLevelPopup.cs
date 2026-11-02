using Bear.Logger;
using Bear.SaveModule;
using Bear.UI;
using Config;
using I2.Loc;
using TMPro;
using UnityEngine;

/// <summary>
/// IWanner Group 选择弹窗。
/// 创建时按当前已加载的 IWanner LevelSort 顺序生成普通或 Boss 选项，并在排序末尾额外生成一个 TBN 占位项。
/// 可玩项直接进入，广告锁定项在激励视频成功后写入永久解锁；TBN 不绑定 LevelSort，只在末组通关后进入 ILevel_999。
/// </summary>
public sealed class IWannerGame_ChoiceLevelPopup : BaseUIView, IDebuger
{
    #region References

    /// <summary>
    /// 用途：按 PageView 页序提供 Group Id 同名定位节点容器，运行时生成的 Item 会挂到对应节点下并归零局部位置。
    /// 注意事项：每个定位节点必须是对应 Points 容器的直接子节点，名称必须与 IWannerLevelSort.Id 完全一致。
    /// </summary>
    [SerializeField] private RectTransform[] mapPages;

    /// <summary>
    /// 用途：恢复上次进入的地图页；第一页末组通关后优先显示第二页。
    /// 调用边界：仅在 Popup 显示后刷新并定位；玩家单纯翻页不改写存档。
    /// 注意事项：必须绑定承载 Map1、Map2 的 PageView，不得直接操作其底层 ScrollRect。
    /// </summary>
    [SerializeField] private PageView pageView;

    /// <summary>
    /// 用途：作为非 Boss Group 的运行时复制源；只允许绑定 IWanner 普通选组 Item prefab 资源。
    /// </summary>
    [SerializeField] private IWannerGame_ChoiceLevelItem itemPrefab;

    /// <summary>
    /// 用途：作为 Boss Group 的运行时复制源，仅在 IWannerLevelSort.IsBoss 为 true 时使用。
    /// 注意事项：必须保留 IWannerGame_ChoiceLevelItem 组件，并与普通 Item 使用相同的四态绑定契约。
    /// </summary>
    [SerializeField] private IWannerGame_ChoiceLevelItem bossItemPrefab;

    /// <summary>
    /// 用途：在全部 IWannerLevelSort Item 之后额外生成一个未完待续占位项。
    /// 调用边界：不绑定 IWannerLevelSort；只按最后一个 Group 的通关状态切换 unlock/lock 并提交独立 TBN 入口。
    /// 注意事项：锁定态只能显示未完待续提示，不得复用普通 Group 的广告解锁回调。
    /// </summary>
    [SerializeField] private IWannerGame_ChoiceLevelItem tbnItemPrefab;

    /// <summary>
    /// 用途：显示当前玩家在 IWannerGame 各关历史重试次数的总和。
    /// 注意事项：只读取 IWannerGame.LevelReviveCount，不参与写入或修改重试生命周期。
    /// </summary>
    [SerializeField] private TMP_Text reviveTxt;

    /// <summary>
    /// 用途：关闭当前选组弹窗；不改变玩法控制器状态，并取消弹窗持有的未完成解锁广告请求。
    /// </summary>
    [SerializeField] private CustomButton closeButton;

    #endregion

    #region Runtime State

    private bool _isEnteringGroup;
    private bool _isUnlockRewardAdRequesting;
    private IWannerLevelSort _pendingUnlockGroup;

    #endregion

    #region UI Lifecycle

    public override void OnCreate()
    {
        base.OnCreate();

        BindButtons();
        RefreshTotalReviveCount();
        CreateLevelItems();
    }

    public override void OnShow()
    {
        base.OnShow();

        if (pageView != null)
            pageView.RefreshPages();

        RestoreMapPage();
    }

    public override void OnClose()
    {
        if (_isUnlockRewardAdRequesting)
            RewardAdHelper.CancelCurrent();

        _isUnlockRewardAdRequesting = false;
        _pendingUnlockGroup = null;
        if (closeButton != null)
            closeButton.OnClick -= OnCloseButtonClicked;

        base.OnClose();
    }

    #endregion

    #region Map Page State

    private void RestoreMapPage()
    {
        if (pageView == null)
        {
            this.LogWarning("[IWannerGame_ChoiceLevelPopup] PageView is missing; map page cannot be restored.");
            return;
        }

        if (pageView.PageCount <= 0)
        {
            this.LogWarning("[IWannerGame_ChoiceLevelPopup] PageView has no pages; map page cannot be restored.");
            return;
        }

        IWannerGame saveData = DB.IWannerGame;
        int savedPageIndex = saveData?.LastMapIndex ?? 0;
        int normalizedPageIndex = Mathf.Clamp(savedPageIndex, 0, pageView.PageCount - 1);
        int targetPageIndex = normalizedPageIndex;
        if (targetPageIndex == 0 && pageView.PageCount > 1 && IsFirstMapLastGroupPassed(saveData))
            targetPageIndex = 1;

        pageView.SetPage(targetPageIndex, false);

        if (saveData == null || savedPageIndex == normalizedPageIndex)
            return;

        saveData.LastMapIndex = normalizedPageIndex;
        if (!DB.SaveIWannerGame())
        {
            this.LogWarning(
                $"[IWannerGame_ChoiceLevelPopup] Failed to save normalized map page index. pageIndex:{normalizedPageIndex}");
        }
    }

    /// <summary>
    /// 按现有配置顺序和地图归属判断第一页末组是否已通关，不把预留点位或 TBN 当作 Group。
    /// 只在弹窗恢复页码时读取进度，不写入上次进入页，也不改变玩家手动翻页。
    /// </summary>
    private bool IsFirstMapLastGroupPassed(IWannerGame saveData)
    {
        if (saveData?.PassedLevels == null || mapPages == null ||
            mapPages.Length < 2 || mapPages[0] == null || mapPages[1] == null)
            return false;

        IWannerGameLevelCtrl levelCtrl = GameManager.Instance?.IWannerGameLevel;
        if (levelCtrl == null || !levelCtrl.IsReady)
            return false;

        for (int index = levelCtrl.LevelGroups.Count - 1; index >= 0; index--)
        {
            IWannerLevelSort group = levelCtrl.LevelGroups[index];
            if (group != null && GetMapIndex(group.Id) == 0)
                return saveData.PassedLevels.Contains(group.Id);
        }

        return false;
    }

    #endregion

    #region Item Creation

    private void CreateLevelItems()
    {
        if (mapPages == null || mapPages.Length == 0 || itemPrefab == null)
        {
            this.LogError("[IWannerGame_ChoiceLevelPopup] Map pages or item prefab is missing.");
            return;
        }

        IWannerGameLevelCtrl levelCtrl = GameManager.Instance?.IWannerGameLevel;
        if (levelCtrl == null || !levelCtrl.IsReady)
        {
            this.LogError("[IWannerGame_ChoiceLevelPopup] IWanner level data is not ready.");
            return;
        }

        int currentGroupId = ResolveAndSaveCurrentGroupId(levelCtrl);

        for (int i = 0; i < levelCtrl.LevelGroups.Count; i++)
        {
            IWannerLevelSort levelSort = levelCtrl.LevelGroups[i];
            if (levelSort == null)
                continue;

            IWannerGame_ChoiceLevelItem sourcePrefab = levelSort.IsBoss
                ? bossItemPrefab
                : itemPrefab;
            if (sourcePrefab == null)
            {
                this.LogError(
                    $"[IWannerGame_ChoiceLevelPopup] Item prefab is missing. groupId:{levelSort.Id}, isBoss:{levelSort.IsBoss}");
                continue;
            }

            RectTransform itemPosition = GetMapPosition(levelSort.Id);
            if (itemPosition == null)
                continue;

            IWannerGame_ChoiceLevelItem item = Instantiate(sourcePrefab, itemPosition, false);
            NormalizeItemTransform(item.transform as RectTransform);

            item.SetData(
                levelSort,
                currentGroupId,
                OnLevelItemClicked,
                OnLockedLevelItemClicked);
            item.gameObject.SetActive(true);
        }

        CreateTbnItem(levelCtrl.LevelGroups.Count + 1);
    }

    private void CreateTbnItem(int positionId)
    {
        if (tbnItemPrefab == null)
        {
            this.LogError("[IWannerGame_ChoiceLevelPopup] TBN item prefab is missing.");
            return;
        }

        RectTransform itemPosition = GetMapPosition(positionId);
        if (itemPosition == null)
            return;

        IWannerGame_ChoiceLevelItem item = Instantiate(tbnItemPrefab, itemPosition, false);
        NormalizeItemTransform(item.transform as RectTransform);
        item.SetTbnData(
            GameManager.Instance?.IWannerGameLevel?.IsTbnUnlocked(
                DB.IWannerGame?.PassedLevels) == true,
            OnTbnItemClicked,
            OnLockedTbnItemClicked);
        item.gameObject.SetActive(true);
    }

    private RectTransform GetMapPosition(int positionId)
    {
        for (int i = 0; i < mapPages.Length; i++)
        {
            RectTransform mapPage = mapPages[i];
            if (mapPage == null)
                continue;

            RectTransform itemPosition = mapPage.Find(positionId.ToString()) as RectTransform;
            if (itemPosition != null && itemPosition.parent == mapPage)
                return itemPosition;
        }

        this.LogWarning(
            $"[IWannerGame_ChoiceLevelPopup] Map page position is missing. positionId:{positionId}");
        return null;
    }

    private int GetMapIndex(int positionId)
    {
        if (mapPages == null)
            return -1;

        for (int i = 0; i < mapPages.Length; i++)
        {
            RectTransform mapPage = mapPages[i];
            if (mapPage == null)
                continue;

            RectTransform itemPosition = mapPage.Find(positionId.ToString()) as RectTransform;
            if (itemPosition != null && itemPosition.parent == mapPage)
                return i;
        }

        return -1;
    }

    private static void NormalizeItemTransform(RectTransform itemRectTransform)
    {
        if (itemRectTransform == null)
            return;

        itemRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        itemRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        itemRectTransform.anchoredPosition = Vector2.zero;
        itemRectTransform.localRotation = Quaternion.identity;
        itemRectTransform.localScale = Vector3.one;
    }

    private int ResolveAndSaveCurrentGroupId(IWannerGameLevelCtrl levelCtrl)
    {
        var saveData = DB.IWannerGame;
        if (saveData == null)
        {
            this.LogWarning("[IWannerGame_ChoiceLevelPopup] IWanner save data is unavailable.");
            return levelCtrl.ResolveCurrentGroupId(0, null);
        }

        bool changed = saveData.EnsureValidCollections();
        changed |= levelCtrl.RestorePassedGroupUnlocks(saveData);
        int progressionGroupId = levelCtrl.ResolveCurrentGroupId(
            saveData.MaxGroupId,
            saveData.PassedLevels);
        if (progressionGroupId > 0 && saveData.MaxGroupId != progressionGroupId)
        {
            saveData.MaxGroupId = progressionGroupId;
            changed = true;
        }

        if (changed && !DB.SaveIWannerGame())
        {
            this.LogWarning(
                $"[IWannerGame_ChoiceLevelPopup] Failed to save normalized Group progress. progressionGroupId:{progressionGroupId}");
        }

        return ResolveDisplayedCurrentGroupId(levelCtrl, saveData, progressionGroupId);
    }

    private static int ResolveDisplayedCurrentGroupId(
        IWannerGameLevelCtrl levelCtrl,
        IWannerGame saveData,
        int progressionGroupId)
    {
        int savedCurrentGroupId = saveData.CurrentGroupID;
        if (savedCurrentGroupId <= 0 ||
            saveData.PassedLevels.Contains(savedCurrentGroupId) ||
            !levelCtrl.TryGetGroup(savedCurrentGroupId, out IWannerLevelSort savedCurrentGroup, out _))
        {
            return progressionGroupId;
        }

        bool canEnterSavedCurrentGroup =
            savedCurrentGroup.Id == progressionGroupId ||
            savedCurrentGroup.LockType == Config.Level.LevelLockType.Unlock ||
            saveData.UnlockGroups.Contains(savedCurrentGroupId);
        return canEnterSavedCurrentGroup ? savedCurrentGroupId : progressionGroupId;
    }

    private void RefreshTotalReviveCount()
    {
        if (reviveTxt == null)
        {
            this.LogWarning("[IWannerGame_ChoiceLevelPopup] Revive text is missing.");
            return;
        }

        long totalReviveCount = 0;
        var levelReviveCount = DB.IWannerGame?.LevelReviveCount;
        if (levelReviveCount != null)
        {
            foreach (int reviveCount in levelReviveCount.Values)
                totalReviveCount += Mathf.Max(0, reviveCount);
        }

        reviveTxt.text = totalReviveCount.ToString();
    }

    #endregion

    #region Interaction

    private void BindButtons()
    {
        if (closeButton != null)
            closeButton.OnClick += OnCloseButtonClicked;
    }

    private void OnCloseButtonClicked(CustomButton _)
    {
        UIManager.Instance.DestroyUI(this);
    }

    private void OnLevelItemClicked(IWannerLevelSort levelSort)
    {
        if (_isEnteringGroup || _isUnlockRewardAdRequesting || levelSort == null)
            return;

        TryEnterGroupAndClose(levelSort);
    }

    private void OnLockedLevelItemClicked(IWannerLevelSort levelSort)
    {
        if (_isEnteringGroup || _isUnlockRewardAdRequesting || levelSort == null)
            return;

        if (levelSort.LockType != Config.Level.LevelLockType.Ad)
        {
            SystemTips.Show(LocalizationManager.GetTranslation("U_HurryTips_TBC"));
            return;
        }

        _isUnlockRewardAdRequesting = true;
        _pendingUnlockGroup = levelSort;
        if (closeButton != null)
            closeButton.Interactable = false;

        try
        {
            RewardAdHelper.TryToShowRewardAd(
                Config.Game.RewardPlacement.OutGameUnlockLevel.ToString(),
                success: "U_HurryTips_RewardVideo_succeed",
                failed: "U_HurryTips_RewardVideo_failed",
                onResult: OnUnlockRewardAdResult);
        }
        catch (System.Exception exception)
        {
            _isUnlockRewardAdRequesting = false;
            _pendingUnlockGroup = null;
            if (closeButton != null)
                closeButton.Interactable = true;
            this.LogError(
                $"[IWannerGame_ChoiceLevelPopup] Unlock reward ad request failed. groupId:{levelSort.Id}, error:{exception}");
        }
    }

    private void OnTbnItemClicked()
    {
        if (_isEnteringGroup || _isUnlockRewardAdRequesting)
            return;

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            this.LogError("[IWannerGame_ChoiceLevelPopup] GameManager is unavailable.");
            return;
        }

        _isEnteringGroup = true;
        if (!gameManager.TryEnterIWannerTbn(out string error))
        {
            _isEnteringGroup = false;
            this.LogError(
                $"[IWannerGame_ChoiceLevelPopup] Enter IWanner TBN failed. error:{error}");
            return;
        }

        IWannerGameLevelCtrl levelCtrl = gameManager.IWannerGameLevel;
        if (levelCtrl != null)
            SaveLastEnteredMapIndex(levelCtrl.LevelGroups.Count + 1);

        UIManager.Instance.DestroyUI(this);
    }

    private static void OnLockedTbnItemClicked()
    {
        SystemTips.Show(LocalizationManager.GetTranslation("U_HurryTips_TBC"));
    }

    private void OnUnlockRewardAdResult(string _, bool isSuccess)
    {
        if (this == null || !IsOpen || !_isUnlockRewardAdRequesting)
            return;

        IWannerLevelSort levelSort = _pendingUnlockGroup;
        _isUnlockRewardAdRequesting = false;
        _pendingUnlockGroup = null;
        if (closeButton != null)
            closeButton.Interactable = true;

        if (!isSuccess || levelSort == null)
            return;

        var saveData = DB.IWannerGame;
        if (saveData == null)
        {
            this.LogError(
                $"[IWannerGame_ChoiceLevelPopup] Cannot unlock Group because IWanner save data is unavailable. groupId:{levelSort.Id}");
            return;
        }

        saveData.EnsureValidCollections();
        if (!saveData.UnlockGroups.Contains(levelSort.Id))
            saveData.UnlockGroups.Add(levelSort.Id);

        if (!DB.SaveIWannerGame())
        {
            this.LogWarning(
                $"[IWannerGame_ChoiceLevelPopup] Failed to save unlocked Group. groupId:{levelSort.Id}");
        }

        TryEnterGroupAndClose(levelSort);
    }

    private void TryEnterGroupAndClose(IWannerLevelSort levelSort)
    {
        if (_isEnteringGroup || levelSort == null)
            return;

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            this.LogError("[IWannerGame_ChoiceLevelPopup] GameManager is unavailable.");
            return;
        }

        _isEnteringGroup = true;
        if (!gameManager.TryEnterIWannerGroup(levelSort.Id, out string error))
        {
            _isEnteringGroup = false;
            this.LogError(
                $"[IWannerGame_ChoiceLevelPopup] Enter IWanner Group failed. groupId:{levelSort.Id}, error:{error}");
            return;
        }

        SaveLastEnteredMapIndex(levelSort.Id);

        UIManager.Instance.DestroyUI(this);
    }

    private void SaveLastEnteredMapIndex(int positionId)
    {
        int mapIndex = GetMapIndex(positionId);
        if (mapIndex < 0)
        {
            this.LogWarning(
                $"[IWannerGame_ChoiceLevelPopup] Map index is unavailable; last entered map was not saved. positionId:{positionId}");
            return;
        }

        IWannerGame saveData = DB.IWannerGame;
        if (saveData == null)
        {
            this.LogWarning("[IWannerGame_ChoiceLevelPopup] IWanner save data is unavailable; last entered map was not saved.");
            return;
        }

        if (saveData.LastMapIndex == mapIndex)
            return;

        saveData.LastMapIndex = mapIndex;
        if (!DB.SaveIWannerGame())
        {
            this.LogWarning(
                $"[IWannerGame_ChoiceLevelPopup] Failed to save last entered map index. mapIndex:{mapIndex}");
        }
    }

    #endregion

    #region Creation

    /// <summary>
    /// 用途：在 Popup 层创建 IWanner Group 选择弹窗。
    /// 调用边界：仅在 IWanner 配置已由 Loading 初始化完成后调用。
    /// 注意事项：创建 Item 前会按配置顺序补齐旧存档的 MaxGroupId；广告解锁只在 Lock Item 奖励成功后写入。
    /// </summary>
    public static IWannerGame_ChoiceLevelPopup Create()
    {
        return UIManager.Instance.OpenUI<IWannerGame_ChoiceLevelPopup>(
            nameof(IWannerGame_ChoiceLevelPopup),
            UILayer.Popup);
    }

    #endregion
}

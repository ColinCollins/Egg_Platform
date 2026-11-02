using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.UI;
using Config;
using Cysharp.Threading.Tasks;
using Game.Scripts.Common;
using Game.Events;
using Config.Game;
using UnityEngine;
using Bear.Logger;
using System.Threading.Tasks;
using DG.Tweening;
using Game;
using Game.ConfigModule;
#if UNITY_IOS
using Guru.UniKit.Apple.GameKit;
using R3;
#endif

public partial class ChoiceLevelPanel : BaseUIView, IEventSender, IDebuger
{
    [SerializeField] private int MaxCount;
    [SerializeField] private RectTransform Content;
    [SerializeField] private LevelChoiceItem itemPrefab;
    [SerializeField] private RowFirstLayoutGroup gridLayout;

    [SerializeField] private UISpineCtrl bgCtrl;
    [SerializeField] private UISpineCtrl boxCtrl;

    [SerializeField] private Level61ChoicePanelKeyCtrl level61KeyCtrl;

    [SerializeField] private PostcardEnter enterBtn;

    /// <summary>NoAd 点击区域；整体显隐必须操作其父节点，避免残留同组美术。</summary>
    [SerializeField] private CustomButton NoAdBtn;

    /// <summary>
    /// 用途：拖动跨页时立即刷新当前关卡区间，松手时只对齐最终页状态。
    /// 调用边界：必须在 ChoiceLevelPanel prefab 中显式绑定嵌套的 QuickTurn 组件。
    /// </summary>
    [SerializeField] private LevelChoicePanel_QuickTurn quickTurnComp;

    private List<LevelSort> sortDatas;
    private EventSubscriber _subscriber;
    private int pageIndex = 0;
    private List<LevelChoiceItem> _itemCache;
    private bool _isPageAnimating;
#if UNITY_IOS && !UNITY_EDITOR
    private IDisposable _gameCenterAuthStateSubscription;
#endif

    public override void OnCreate()
    {
        base.OnCreate();

        sortDatas = PlayCtrl.Instance.Level.LevelSorts;

        InitItems();
        InitButtons();
        InitQuickTurn();
    }

    private async Task PlayOpenAnim()
    {
        bgCtrl.gameObject.SetActive(false);
        boxCtrl.gameObject.SetActive(false);
        quickTurnComp?.SetCanvasGroupAlpha(0f);

        for (int i = 0; i < _itemCache.Count; i++)
        {
            _itemCache[i].SetCanvasGroupAlpha(0);
        }

        await UniTask.WaitForSeconds(.5f);

        bgCtrl.gameObject.SetActive(true);
        boxCtrl.gameObject.SetActive(true);
        quickTurnComp?.SetCanvasGroupAlpha(1f, 0.2f);

        for (int i = 0; i < _itemCache.Count; i++)
        {
            _itemCache[i].transform.DOPunchScale(Vector3.up * 0.3f, 0.3f, 1, 1).SetDelay(i * 0.05f);
            _itemCache[i].SetCanvasGroupAlpha(1, 0.2f, delay: i * 0.05f);
        }

        bgCtrl.PlayAnimation("book_in", false).Complete += (entry) =>
        {
            bgCtrl.PlayAnimation("book_idle", true);
        };

        boxCtrl.PlayAnimation("box_in", false).Complete += (entry) =>
        {
            boxCtrl.PlayAnimation("box_idle", true);
        };
    }

    void Start()
    {
        gridLayout.RebuildLayout();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        RefreshPageIndex();
        quickTurnComp?.SetCurrentPage(pageIndex);
        quickTurnComp?.SetInteractable(true);
        RefreshBtn();
        SubscribeGameCenterAuthState();
        RefreshGpsButtonVisibility();
        RefreshItems();

        AddListener();

        if (level61KeyCtrl != null)
            level61KeyCtrl.TryShow();

        if (!AudioManager.IsCurrentMusicTag("musicOutGame"))
        {
            AudioManager.PlayMusic("musicOutGame", fadeInSeconds: 8f);
        }

        enterBtn.Refresh();
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<UpdatePropEvent>(OnPropUpdate);
        _subscriber.Subscribe<OnRestoreSuccessEvent>(OnRestoreSuccess);
    }

    private void OnPropUpdate(UpdatePropEvent evt)
    {
        if (evt.Prop != GameProps.NoAds)
            return;

        RefreshBtn();
    }

    private void OnRestoreSuccess(OnRestoreSuccessEvent evt)
    {
        RefreshBtn();
        RefreshItems();
        gridLayout.RebuildLayout();
    }

    private void InitItems()
    {
        if (_itemCache == null)
            _itemCache = new List<LevelChoiceItem>();

        if (_itemCache.Count <= MaxCount)
        {
            for (int i = _itemCache.Count; i < MaxCount; i++)
            {
                var item = Instantiate(itemPrefab, Content);
                item.Init(this);
                _itemCache.Add(item);
            }
        }

        int maxIndex = sortDatas.Count / MaxCount;
        if (sortDatas.Count % MaxCount == 0)
            maxIndex -= 1;

        pageIndex = GetInitialPageIndex(maxIndex);
        RefreshItems();
    }

    /// <summary>
    /// 初始化关卡选择页索引。
    /// 优先根据玩家当前所在关卡计算对应分页；如果当前关卡数据不存在，则回退到存档记录的分页索引。
    /// </summary>
    /// <param name="maxIndex">允许的最大分页索引。</param>
    /// <returns>最终使用的初始分页索引。</returns>
    private int GetInitialPageIndex(int maxIndex)
    {
        var levelCtrl = PlayCtrl.Instance?.Level;
        if (levelCtrl?.CurrentLevelData != null)
        {
            int currentSortIndex = sortDatas.FindIndex(data => data.Id == levelCtrl.CurrentLevel);
            if (currentSortIndex >= 0)
            {
                return Math.Clamp(currentSortIndex / MaxCount, 0, maxIndex);
            }
        }

        return Math.Clamp(DB.GameData.ChioceLevelPanelPageIndex, 0, maxIndex);
    }

    /// <summary>
    /// 刷新当前分页索引，确保每次打开面板时都能对齐玩家当前关卡。
    /// </summary>
    private void RefreshPageIndex()
    {
        int maxIndex = sortDatas.Count / MaxCount;
        if (sortDatas.Count % MaxCount == 0)
            maxIndex -= 1;

        pageIndex = GetInitialPageIndex(maxIndex);
    }

    private void InitButtons()
    {
        BackBtn.OnClick += ReturnToStartPanel;
        LastBtn.OnClick += LastPage;
        NextBtn.OnClick += NextPage;

        SettingBtn.OnClick += ShowSetting;
        ShopBtn.OnClick += ShowShop;
        NoAdBtn.OnClick += ShowNoAd;

        // 成就系统按钮
        if (GpsBtn)
        {
            GpsBtn.OnClick += ShowGps;
        }

        RefreshBtn();
    }

    private void InitQuickTurn()
    {
        if (quickTurnComp == null)
        {
            this.LogError("[ChoiceLevelPanel] QuickTurn component is missing.");
            return;
        }

        quickTurnComp.Initialize(
            sortDatas.Count,
            MaxCount,
            pageIndex,
            GetQuickTurnCurrentPageIndex,
            OnQuickTurnPageChanged,
            OnQuickTurnPageSelected);
    }

    private int GetQuickTurnCurrentPageIndex() => pageIndex;

    private void ReturnToStartPanel(CustomButton btn)
    {
        if (!GameManager.Instance.ReturnToStartPanel(out string error))
            this.LogError($"[ChoiceLevelPanel] Return to StartPanel failed. error:{error}");
    }

    private bool _isGpsProcessing;

    private async void ShowGps(CustomButton btn)
    {
        if (_isGpsProcessing) return;

        try
        {
            _isGpsProcessing = true;

#if UNITY_IOS && !UNITY_EDITOR
            if (!AppleGameCenterAuthAgent.Instance.IsAuthenticated)
            {
                RefreshGpsButtonVisibility();
                return;
            }
#endif

            // 成就系统强依赖游戏服务登录，先检查是否已登录
            if (!GameAccountUtils.IsGameLoggedIn)
            {
                // 检查当前设备/平台是否支持游戏登录
                if (!GameAccountUtils.IsGameLoginSupported())
                {
                    Debug.Log("[ChoiceLevelPanel] Game login not supported on this device.");
                    return;
                }

                // 尝试触发 Google Play Games / Game Center 登录
                // Android 平台内部会优先走 Google Play Games 手动认证，确保登录框能弹出
                var loginSuccess = await GameAccountUtils.LoginWithGame(true);
                if (!loginSuccess)
                {
                    Debug.Log("[ChoiceLevelPanel] Game login failed, cannot show achievements.");
                    return;
                }
            }

            // 显示平台原生成就界面（Google Play Games 或 Game Center）
            GameAchievementUtils.ShowAchievementUI();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChoiceLevelPanel] ShowGps failed: {ex}");
        }
        finally
        {
            _isGpsProcessing = false;
        }
    }

    private void ShowNoAd(CustomButton btn)
    {
        NoAdsPopup.Create();
    }

    private void ShowShop(CustomButton btn)
    {
        ShopPanel.Create(false, GetType().Name);

        // 内购体系修改，兼容旧功能。弹出转换界面，避免回复内购功能跟不上。这里手动调用一下
        if (TransformPopup.HasTransformableTips())
            TransformPopup.Create();
    }

    private void ShowSetting(CustomButton btn)
    {
        GameSettingPopup.Create();
    }

    private void LastPage(CustomButton btn)
    {
        TryTurnPage(pageIndex - 1);
    }

    private void NextPage(CustomButton btn)
    {
        TryTurnPage(pageIndex + 1);
    }

    private void OnQuickTurnPageChanged(int targetPageIndex)
    {
        if (_isPageAnimating)
            return;

        int maxPageIndex = sortDatas.Count <= 0 ? 0 : (sortDatas.Count - 1) / MaxCount;
        int newPageIndex = Math.Clamp(targetPageIndex, 0, maxPageIndex);
        if (newPageIndex == pageIndex)
            return;

        pageIndex = newPageIndex;
        RefreshBtn();
        RefreshItems();
    }

    private void OnQuickTurnPageSelected(int targetPageIndex)
    {
        quickTurnComp?.SetCurrentPage(pageIndex);
    }

    private bool TryTurnPage(int targetPageIndex)
    {
        if (_isPageAnimating)
            return false;

        int maxPageIndex = sortDatas.Count <= 0 ? 0 : (sortDatas.Count - 1) / MaxCount;
        int newPageIndex = Math.Clamp(targetPageIndex, 0, maxPageIndex);
        if (newPageIndex == pageIndex)
            return false;

        bool isReverse = newPageIndex < pageIndex;
        _isPageAnimating = true;
        pageIndex = newPageIndex;
        quickTurnComp?.SetCurrentPage(pageIndex);
        quickTurnComp?.SetInteractable(false);
        quickTurnComp?.SetCanvasGroupAlpha(0f, 0.2f);

        for (int i = 0; i < _itemCache.Count; i++)
        {
            _itemCache[i].SetCanvasGroupAlpha(0, 0.2f);
        }

        AudioManager.PlaySound("FlipPage");

        string animationName = isReverse ? "book_turn-the-page_reverse" : "book_turn-the-page";
        bgCtrl.PlayAnimation(animationName, false).Complete += (entry) =>
        {
            if (isReverse)
                bgCtrl.PlayAnimation("book_idle", true);

            RefreshBtn();
            RefreshItems();
            if (!isReverse)
                bgCtrl.PlayAnimation("book_idle", true);

            quickTurnComp?.SetCanvasGroupAlpha(1f, 0.2f);
            for (int i = 0; i < _itemCache.Count; i++)
            {
                _itemCache[i].SetCanvasGroupAlpha(1, 0.2f, i * 0.05f);
            }

            _isPageAnimating = false;
            quickTurnComp?.SetInteractable(true);
        };

        return true;
    }

    private void RefreshBtn()
    {
        LastBtn.gameObject.SetActive(pageIndex > 0);
        NextBtn.gameObject.SetActive((pageIndex + 1) * MaxCount < sortDatas.Count);

        // noAd 按钮隐藏
        bool shouldShowNoAd = GameManager.Instance.Bag.GetToolCount(Config.Game.GameProps.NoAds) <= 0;
        NoAdBtn.transform.parent.gameObject.SetActive(shouldShowNoAd);
    }

    private void RefreshGpsButtonVisibility()
    {
        if (!GpsBtn)
            return;

        bool isVisible = ConfigManager.RemoteConfig.GetOpenGaps();
#if UNITY_IOS && !UNITY_EDITOR
        isVisible &= AppleGameCenterAuthAgent.Instance.IsAuthenticated;
#endif
        GpsBtn.gameObject.SetActive(isVisible);
    }

    private void SubscribeGameCenterAuthState()
    {
#if UNITY_IOS && !UNITY_EDITOR
        UnsubscribeGameCenterAuthState();
        _gameCenterAuthStateSubscription = AppleGameCenterAuthAgent.Instance.AuthStateUpdated
            .Where(state => state.OldState != null || state.NewState != null)
            .Subscribe(_ => RefreshGpsButtonVisibility());
#endif
    }

    private void UnsubscribeGameCenterAuthState()
    {
#if UNITY_IOS && !UNITY_EDITOR
        _gameCenterAuthStateSubscription?.Dispose();
        _gameCenterAuthStateSubscription = null;
#endif
    }

    private void RefreshItems()
    {
        if (_itemCache.Count <= 0)
            return;

        int index = -1;
        for (int i = 0; i < _itemCache.Count; i++)
        {
            index = pageIndex * MaxCount + i;

            var sortData = index >= sortDatas.Count ? null : sortDatas[index];
            _itemCache[i].SetData(sortData);
        }
    }

    public void EnterLevel(LevelSort sort)
    {
        this.Log("--------------------- enter level");

        this.DispatchEvent(Witness<Game.Events.EnterLevelEvent>._, sort);
        WaitToClose().Forget();

        DB.GameData.ChioceLevelPanelPageIndex = pageIndex;
        DB.GameData.Save();
    }

    private async UniTask WaitToClose()
    {
        await UniTask.WaitForSeconds(1f);

        UIManager.Instance.CloseUI(this);
    }

    public static ChoiceLevelPanel Create()
    {
        var panel = UIManager.Instance.OpenUI<ChoiceLevelPanel>($"{typeof(ChoiceLevelPanel).Name}", UILayer.Normal);
        _ = panel.PlayOpenAnim();
        return panel;
    }

    void OnDisable()
    {
        _isPageAnimating = false;
        quickTurnComp?.SetInteractable(true);
    }

    private void OnDestroy()
    {
        UnsubscribeGameCenterAuthState();
    }

    public override void OnClose()
    {
        base.OnClose();
        EventsUtils.ResetEvents(ref _subscriber);
        UnsubscribeGameCenterAuthState();
    }
}

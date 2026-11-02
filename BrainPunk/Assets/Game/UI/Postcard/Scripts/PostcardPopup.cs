using Bear.Logger;
using Bear.SaveModule;
using Bear.UI;
using Config.Game;
using Cysharp.Threading.Tasks;
using Game.Scripts.Common;
using I2.Loc;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using BearResourceManager = Bear.ResourceSystem.ResourceManager;
using GameConfigManager = Game.ConfigModule.ConfigManager;

/// <summary>
/// 展示明信片功能
/// version 1.0
/// 先读取 postcardConfig 配置。并尝试初始化 Postcard.CurrentId。从 config 第一个数据开始记录。
/// CurrenId 支持 lastBtn 功能。若是当前 page 未完成 点击 NextBtn 显示 Postcard_Unlock page。然后 nextBtn 隐藏。
/// 若当前Page 为最后一个 Page 则 NextBtn 隐藏。
/// 若当前 Page 为第一个 Page，则 LastBtn 隐藏。
///
/// 补充一下 OnPageComplete 回调以及 OnPagePlayAnim。
/// OnPagePlayAnim 期间，不允许关闭和点击。
/// OnPageComplete 暂留一个 API 先不做具体实现。
///
/// 补充 chest 脚本控制。切换不同的 config page 的时候记得刷新 chest
///
/// OnPageComplete 补充逻辑 Anim 播放完成后，调用 CommonGetRewardPopup 获取 Chest 的奖励，并且隐藏 Chest。
/// 关闭 CommonGetRewardPopup 后，自动切换下一页。若存在下一页的话。可以用 UniTask 在每个操作补充一个 0.5f 的小等待。
/// 将 Popup 开启操作的逻辑放到这些逻辑之后吧。
///
/// </summary>
public partial class PostcardPopup : BaseUIView, IDebuger
{
    private const string ContainerName = "Container";
    private const string TitleNodeName = "title";
    private const string ChestNodeName = "Postcard_Chest";
    private const string PostcardPageAssetRoot = "Assets/Game/UI/Postcard/Prefabs/Pages";
    private const string PrefabExtension = ".prefab";

    /// <summary>
    /// 用途：承载当前明信片页或锁定占位页实例的本地挂点。
    /// 调用边界：优先由 prefab 序列化绑定；未绑定时运行时按子节点名查找。
    /// 注意事项：只作为 UI 实例父节点，不负责资源加载、存档或页面状态判断。
    /// </summary>
    [SerializeField] private RectTransform container;

    /// <summary>
    /// 用途：显示当前明信片主题标题的文本控件。
    /// 调用边界：优先由 prefab 序列化绑定；未绑定时运行时按 title 节点查找子级 TMP_Text。
    /// 注意事项：文本内容由当前 PostcardConfig.ThemeName 多语言 key 刷新，不写入存档。
    /// </summary>
    [SerializeField] private TMP_Text title;

    /// <summary>
    /// 用途：控制明信片宝箱奖励预览和当前页收集进度显示。
    /// 调用边界：优先由 prefab 序列化绑定；未绑定时运行时按 Postcard_Chest 节点或子组件查找。
    /// 注意事项：只刷新 UI，不发奖、不写存档、不读取资源。
    /// </summary>
    [SerializeField] private Postcard_Chest chest;

    [SerializeField] private Postcard_GuidePart guidePart;

    /// <summary>
    /// 用途：明信片 item 解锁时复用播放的本地粒子列表。
    /// 调用边界：仅由 `PlayUnlockEffectsAsync` 移动到真实 PageItem 目标位置后播放。
    /// 注意事项：未绑定或存在空项时静默跳过，不阻断解锁、存档或引导流程。
    /// </summary>
    [SerializeField] private List<ParticleSystem> collectEffects;

    /// <summary>
    /// 用途：在完整明信片引导或页面切换表现期间拦截 Popup 原生 UI 射线，避免玩家直接操作页面、关闭或翻页按钮。
    /// 调用边界：随当前 PostcardPopup 的本地 `_isGuiding` 或 `IsPagePlayingAnim` 状态开启或关闭。
    /// 注意事项：遮罩必须位于普通 Popup 内容之上、页内引导组件之下，并使用不生成可见网格的射线目标组件。
    /// </summary>
    [SerializeField] private GameObject clickBlock;

    /// <summary>
    /// 用途：显示当前 Coin 数量，并在页面完成奖励展示结束后由 PostcardPopup 手动刷新。
    /// 调用边界：目标 PropBlock 必须配置为 Coin 且关闭 IsAutoUpdate；未绑定时运行时从当前 Popup 子级查找。
    /// 注意事项：前置发奖期间保持旧显示，避免 Coin 数字早于奖励展示跳变。
    /// </summary>
    [SerializeField] private PropBlock coinPropBlock;

    private Postcard_PageItem _currentPage;
    private readonly List<Config.PostcardConfig> _postcardConfigs = new List<Config.PostcardConfig>();
    private Config.PostcardConfig _unlockConfig;
    private int _currentConfigIndex = -1;
    private bool _isShowingUnlockPage;
    private bool _refreshCoinAfterRewardPopup;
    private PostcardPopupTipsBubble _tipsBubble;
    private string _currentPageResourcePath;

    /// <summary>
    /// 用途：标记当前明信片页是否正在播放解锁或完成表现，供页面 item 和弹窗按钮判断是否允许点击。
    /// 调用边界：由 Postcard_PageItem 解锁动画前后读取；外部只读，不应直接改写。
    /// 注意事项：该状态只影响当前 PostcardPopup 实例的交互锁，不写 DB、不暂停游戏流程。
    /// </summary>
    public bool IsPagePlayingAnim { get; private set; }

    /// <summary>
    /// 当前是 guiding 阶段
    /// </summary>
    private bool _isGuiding = false;

    /// <summary>
    /// 当前 Popup 持有的页面内明信片引导流程；只在 Stage Two 交接后创建，关闭 Popup 时主动取消。
    /// </summary>
    private PostcardGuideFlow2 _postcardGuideFlow2;

    public override void OnOpen()
    {
        base.OnOpen();
        CancelPostcardGuideFlow2();
        BindButtons();
        _refreshCoinAfterRewardPopup = false;
        ResolveCoinPropBlock()?.UpdateData();
        Postcard postcardData = null;
        bool hasPostcardData = Application.isPlaying && TryGetPostcardData(out postcardData);
        bool isGuidingOnOpen = hasPostcardData && postcardData.IsGuiding;
        SetGuidingState(isGuidingOnOpen);
        StopPostcardGuide();
        LoadInitialPage();
        RefreshTipsBubble(postcardData);
    }

    public override void OnCreate()
    {
        base.OnCreate();
        ResolveClickBlock();
        ResolveCoinPropBlock();
    }

    /// <summary>
    /// 用途：Popup 恢复显示时，在奖励弹窗已结束的边界刷新 Coin PropBlock。
    /// 调用边界：由 Bear UI 生命周期调用；普通首次显示不会触发额外刷新。
    /// 注意事项：只有存在待处理奖励刷新标记时才读取 Bag，避免覆盖前置发奖期间保留的旧显示。
    /// </summary>
    public override void OnShow()
    {
        base.OnShow();
        RefreshCoinAfterRewardPopupIfPending();
    }

    public override void OnClose()
    {
        CancelPostcardGuideFlow2();
        HideTipsBubbleImmediately();
        ClearCurrentPage();
        UnbindButtons();
        SetPagePlayingAnimationState(false);
        SetGuidingState(false);
        _postcardConfigs.Clear();
        _unlockConfig = null;
        _currentConfigIndex = -1;
        _isShowingUnlockPage = false;
        _isHandlingPageComplete = false;
        _refreshCoinAfterRewardPopup = false;
        base.OnClose();
    }

    #region Public API

    /// <summary>
    /// 用途：供 GM 单张解锁在写入前确认弹窗可安全刷新。
    /// 调用边界：只读当前实例状态；页面动画、领奖或引导期间禁止外部改写进度。
    /// </summary>
    internal bool CanApplyGmUnlock => Application.isPlaying && IsOpen &&
        !IsPagePlayingAnim && !_isHandlingPageComplete && !_isGuiding;

    /// <summary>
    /// 用途：GM 单张解锁保存成功后，立即显示本次完成的明信片及其完成态。
    /// 调用边界：仅供 GM 在 CanApplyGmUnlock 允许时调用；复用页面加载、宝箱和翻页按钮刷新。
    /// 注意事项：不另存 CurrentId，不调用正常完成领奖流程；动画或引导中拒绝重建页面。
    /// </summary>
    internal bool RefreshAfterGmUnlock(string postcardId)
    {
        if (!CanApplyGmUnlock)
            return false;

        int configIndex = FindConfigIndex(postcardId);
        if (configIndex < 0 || !IsConfigCompleted(_postcardConfigs[configIndex]))
            return false;

        Postcard_PageItem previousPage = _currentPage;
        ShowConfigPage(configIndex, false);
        return _currentPage != null && _currentPage != previousPage &&
            !_isShowingUnlockPage && _currentConfigIndex == configIndex;
    }

    /// <summary>
    /// 用途：接收当前页面动画播放状态，并同步全屏 ClickBlock 及关闭、翻页按钮的交互锁。
    /// 调用边界：仅由当前 Postcard_PageItem 在解锁动画开始和结束时调用。
    /// 注意事项：传入 true 后玩家不能关闭或翻页；传入 false 时仅刷新未完成页的 Chest 进度，完成页的 Chest 显隐由 PageComplete 流程或页面加载流程负责。
    /// </summary>
    /// <param name="isPlaying">true 表示页面表现进行中；false 表示页面表现结束。</param>
    public void OnPagePlayAnim(bool isPlaying)
    {
        SetPagePlayingAnimationState(isPlaying);
        if (!isPlaying && !IsConfigCompleted(GetVisibleConfig()))
            RefreshCurrentChest(false);

        RefreshButtonStates();
    }

    /// <summary>
    /// 用途：兼容旧的收集动效入口，当前转调到 item 解锁粒子播放。
    /// 调用边界：外部已有收集动效调用可继续传入目标世界坐标。
    /// 注意事项：具体播放逻辑统一由 `PlayUnlockEffectsAsync` 处理。
    /// </summary>
    /// <param name="pos">目标世界坐标。</param>
    public void PlayCollectEffect(Vector3 pos)
    {
        PlayUnlockEffectsAsync(pos).Forget();
    }

    /// <summary>
    /// 用途：把已绑定粒子移动到真实明信片 PageItem 的解锁 item 位置并播放。
    /// 调用边界：由 `Postcard_PageItem` 在解锁校验和保存成功后调用。
    /// 注意事项：延迟期间随 Popup 销毁取消；只复用现有粒子对象，不实例化、不写存档、不影响解锁回调。
    /// </summary>
    /// <param name="worldPosition">目标 item 的世界坐标位置。</param>
    public async UniTask PlayUnlockEffectsAsync(Vector3 worldPosition)
    {
        if (!Application.isPlaying || collectEffects == null || collectEffects.Count <= 0)
            return;

        bool isCanceled = await UniTask.WaitForSeconds(.5f, cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        if (isCanceled || this == null || !gameObject.activeInHierarchy || collectEffects == null)
            return;

        AudioManager.PlaySound("postcardUnlockProgressItem");
        for (int i = 0; i < collectEffects.Count; i++)
        {
            ParticleSystem particle = collectEffects[i];
            if (particle == null)
                continue;

            particle.transform.position = worldPosition;
            if (!particle.gameObject.activeSelf)
                particle.gameObject.SetActive(true);

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(true);
        }
    }

    /// <summary>
    /// 用途：在第二阶段引导弹窗关闭后，启动当前 Popup 持有的 PostcardGuideFlow2 页面内引导流程。
    /// 调用边界：仅由持有当前 Popup 实例的明信片引导流程在 `PostcardStageStep.Two` 关闭回调中调用。
    /// 注意事项：Flow2 会通过局部委托启动 GuidePart 并消费既有 `DB.Postcard.IsGuiding`；重复调用不会并行启动第二份流程。
    /// </summary>
    /// <returns>页面内引导已启动或已有同一流程运行时返回 true；当前页面不可用时返回 false。</returns>
    public bool StartPendingPostcardGuide()
    {
        if (!Application.isPlaying)
            return false;

        if (_currentPage == null || _isShowingUnlockPage)
        {
            this.LogWarning("[PostcardPopup] Pending postcard guide could not start because the current page is unavailable.");
            return false;
        }

        if (_postcardGuideFlow2?.IsRunning == true)
            return true;

        RectTransform nextPageGuideTarget = NextBtn != null
            ? NextBtn.transform as RectTransform
            : null;
        PostcardGuideFlow2 guideFlow = new PostcardGuideFlow2(
            nextPageGuideTarget,
            transform as RectTransform,
            TryStartPostcardGuidePart,
            TryGoNextPageForGuide,
            SetPostcardGuideInteractionLocked);
        _postcardGuideFlow2 = guideFlow;
        RunPostcardGuideFlow2Async(guideFlow).Forget();
        return true;
    }

    #endregion

    #region Button Binding

    private void BindButtons()
    {
        UnbindButtons();

        if (CloseBtn == null)
        {
            this.LogWarning("[PostcardPopup] CloseBtn is null.");
        }
        else
        {
            CloseBtn.OnClick += OnCloseBtnClick;
        }

        if (LastBtn != null)
            LastBtn.OnClick += OnLastBtnClick;

        if (NextBtn != null)
            NextBtn.OnClick += OnNextBtnClick;
    }

    private void UnbindButtons()
    {
        if (CloseBtn != null)
            CloseBtn.OnClick -= OnCloseBtnClick;

        if (LastBtn != null)
            LastBtn.OnClick -= OnLastBtnClick;

        if (NextBtn != null)
            NextBtn.OnClick -= OnNextBtnClick;
    }

    /// <summary>
    /// 关闭按钮点击时销毁当前明信片弹窗并由 OnClose 释放当前页面资源，不修改明信片数据或存档。
    /// </summary>
    private void OnCloseBtnClick(CustomButton _)
    {
        if (_isGuiding || IsPagePlayingAnim)
            return;

        UIManager.Instance?.DestroyUI(this);
    }

    private void OnLastBtnClick(CustomButton _)
    {
        if (_isGuiding || IsPagePlayingAnim)
            return;

        if (_isShowingUnlockPage)
        {
            ShowConfigPage(_currentConfigIndex, false);
            return;
        }

        if (_currentConfigIndex <= 0)
            return;

        ShowConfigPage(_currentConfigIndex - 1, true);
    }

    private void OnNextBtnClick(CustomButton _)
    {
        TryGoNextPage(false);
    }

    private void TryGoNextPage(bool allowWhileGuiding)
    {
        if ((_isGuiding && !allowWhileGuiding) || IsPagePlayingAnim || !HasValidCurrentConfig())
            return;

        if (_isShowingUnlockPage)
            return;

        Config.PostcardConfig currentConfig = _postcardConfigs[_currentConfigIndex];
        bool isLastConfig = _currentConfigIndex >= _postcardConfigs.Count - 1;
        if (!IsConfigCompleted(currentConfig))
        {
            if (!isLastConfig)
                ShowUnlockPage();
            return;
        }

        if (isLastConfig)
        {
            if (!AreAllPostcardPagesCompleted())
                ShowUnlockPage();
            return;
        }

        ShowConfigPage(_currentConfigIndex + 1, true);
    }

    #endregion

    #region Page Loading

    private void LoadInitialPage()
    {
        if (!TryLoadPostcardConfigs())
        {
            RefreshButtonStates();
            return;
        }

        if (!TryResolveInitialIndex(out int initialIndex))
        {
            RefreshButtonStates();
            return;
        }

        ShowConfigPage(initialIndex, false);
    }

    private bool TryLoadPostcardConfigs()
    {
        _postcardConfigs.Clear();

        GameConfigManager configManager = Object.FindAnyObjectByType<GameConfigManager>();
        var configList = configManager?.Tables?.TbPostcardConfig?.DataList;
        if (configManager == null || !configManager.IsInitialized || configList == null || configList.Count <= 0)
        {
            this.LogWarning("[PostcardPopup] Postcard config is missing or ConfigManager is not initialized.");
            return false;
        }

        for (int i = 0; i < configList.Count; i++)
        {
            Config.PostcardConfig postcardConfig = configList[i];
            if (postcardConfig == null)
                continue;

            if (IsUnlockConfig(postcardConfig))
            {
                if (_unlockConfig == null || ComparePostcardConfigSort(postcardConfig, _unlockConfig) < 0)
                    _unlockConfig = postcardConfig;
                continue;
            }

            _postcardConfigs.Add(postcardConfig);
        }

        _postcardConfigs.Sort(ComparePostcardConfigSort);
        return _postcardConfigs.Count > 0;
    }

    private bool TryResolveInitialIndex(out int initialIndex)
    {
        initialIndex = -1;

        if (!TryGetPostcardData(out Postcard postcardData))
            return false;

        initialIndex = FindConfigIndex(postcardData.CurrentId);
        if (initialIndex >= 0)
        {
            if (!postcardData.CompletedId.Contains(_postcardConfigs[initialIndex].Id))
                return true;

            for (int i = 0; i < _postcardConfigs.Count; i++)
            {
                Config.PostcardConfig postcardConfig = _postcardConfigs[i];
                if (postcardConfig == null || postcardData.CompletedId.Contains(postcardConfig.Id))
                    continue;

                if (SaveCurrentId(postcardConfig.Id))
                    initialIndex = i;
                return true;
            }

            return true;
        }

        initialIndex = 0;
        return SaveCurrentId(_postcardConfigs[initialIndex].Id);
    }

    private void ShowConfigPage(int configIndex, bool saveCurrentId)
    {
        if (configIndex < 0 || configIndex >= _postcardConfigs.Count)
            return;

        Config.PostcardConfig postcardConfig = _postcardConfigs[configIndex];
        if (postcardConfig == null)
            return;

        Postcard_PageItem prefab = LoadPostcardPagePrefab(postcardConfig, out string resourcePath);
        if (prefab == null)
        {
            this.LogWarning($"[PostcardPopup] Postcard page item prefab is missing. id:{postcardConfig.Id}, theme:{postcardConfig.ThemeName}, path:{postcardConfig.PrefabPath}");
            RecoverGuideAfterPageLoadFailure();
            RefreshButtonStates();
            return;
        }

        AudioManager.PlaySound("FlipPage");

        if (saveCurrentId && !SaveCurrentId(postcardConfig.Id))
        {
            ReleaseLoadedPageResource(resourcePath);
            RefreshButtonStates();
            return;
        }

        ClearCurrentPage(resourcePath);
        _currentConfigIndex = configIndex;
        _isShowingUnlockPage = false;

        if (!ShowPageItem(prefab, postcardConfig, false, resourcePath))
        {
            ReleaseLoadedPageResource(resourcePath);
            RecoverGuideAfterPageLoadFailure();
        }
        RefreshButtonStates();
    }

    private void ShowUnlockPage()
    {
        if (_unlockConfig == null)
        {
            this.LogWarning("[PostcardPopup] Unlock page config is missing.");
            RefreshButtonStates();
            return;
        }

        Postcard_PageItem prefab = LoadPostcardPagePrefab(_unlockConfig, out string resourcePath);
        if (prefab == null)
        {
            this.LogWarning("[PostcardPopup] Unlock page prefab is missing.");
            RecoverGuideAfterPageLoadFailure();
            RefreshButtonStates();
            return;
        }

        ClearCurrentPage(resourcePath);
        _isShowingUnlockPage = true;
        if (!ShowPageItem(prefab, _unlockConfig, true, resourcePath))
        {
            ReleaseLoadedPageResource(resourcePath);
            RecoverGuideAfterPageLoadFailure();
        }
        RefreshButtonStates();
    }

    private void ClearCurrentPage(string retainedResourcePath = null)
    {
        StopPostcardGuide();
        if (_currentPage != null)
        {
            _currentPage.OnExit();
            Destroy(_currentPage.gameObject);
            _currentPage = null;
        }

        string resourcePath = _currentPageResourcePath;
        _currentPageResourcePath = null;
        if (!string.IsNullOrWhiteSpace(resourcePath)
            && !string.Equals(resourcePath, retainedResourcePath, System.StringComparison.Ordinal))
        {
            BearResourceManager.Instance.Release(resourcePath);
        }
    }

    private bool ShowPageItem(
        Postcard_PageItem prefab,
        Config.PostcardConfig postcardConfig,
        bool isUnlockPage,
        string resourcePath)
    {
        Transform pageParent = ResolvePageContainer();
        if (prefab == null || pageParent == null)
        {
            this.LogWarning("[PostcardPopup] Page item prefab or container is missing.");
            return false;
        }

        _currentPage = Instantiate(prefab, pageParent, false);
        if (_currentPage == null)
        {
            this.LogWarning("[PostcardPopup] Page item prefab instantiate failed.");
            return false;
        }

        _currentPageResourcePath = resourcePath;
        _currentPage.SetData(postcardConfig, this, isUnlockPage ? null : OnPageComplete);
        RefreshTitle(postcardConfig);
        RefreshChest(postcardConfig, true);
        _currentPage.OnShow();
        return true;
    }

    private Postcard_PageItem LoadPostcardPagePrefab(
        Config.PostcardConfig postcardConfig,
        out string resourcePath)
    {
        resourcePath = null;
        if (postcardConfig == null)
            return null;

        resourcePath = BuildPostcardPageResourcePath(postcardConfig.PrefabPath);
        return LoadResourcePrefab(resourcePath);
    }

    private Postcard_PageItem LoadResourcePrefab(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        GameObject prefab = BearResourceManager.Instance.Load<GameObject>(resourcePath);
        if (prefab == null)
            return null;

        Postcard_PageItem pageItem = prefab.GetComponent<Postcard_PageItem>();
        if (pageItem != null)
            return pageItem;

        this.LogWarning($"[PostcardPopup] YooAsset prefab does not contain Postcard_PageItem. path:{resourcePath}");
        BearResourceManager.Instance.Release(resourcePath);
        return null;
    }

    private static string BuildPostcardPageResourcePath(string prefabPath)
    {
        return string.IsNullOrWhiteSpace(prefabPath)
            ? null
            : $"{PostcardPageAssetRoot}/{prefabPath}{PrefabExtension}";
    }

    private void ReleaseLoadedPageResource(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath)
            || string.Equals(resourcePath, _currentPageResourcePath, System.StringComparison.Ordinal))
        {
            return;
        }

        BearResourceManager.Instance.Release(resourcePath);
    }

    private void RecoverGuideAfterPageLoadFailure()
    {
        if (!_isGuiding)
            return;

        CancelPostcardGuideFlow2();
        StopPostcardGuide();
        if (TryGetPostcardData(out Postcard postcardData))
            ConsumePendingGuideState(postcardData);

        SetGuidingState(false);
    }

    private Transform ResolvePageContainer()
    {
        if (container != null)
            return container;

        Transform found = FindChildByName(transform, ContainerName);
        container = found as RectTransform;
        return container != null ? container : transform;
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                return child;

            Transform result = FindChildByName(child, targetName);
            if (result != null)
                return result;
        }

        return null;
    }

    private void RefreshTitle(Config.PostcardConfig postcardConfig)
    {
        TMP_Text titleText = ResolveTitleText();
        if (titleText == null)
            return;

        string themeName = postcardConfig?.ThemeName;
        if (string.IsNullOrEmpty(themeName))
        {
            titleText.text = string.Empty;
            return;
        }

        string translated = LocalizationManager.GetTranslation(themeName);
        titleText.text = string.IsNullOrEmpty(translated) ? themeName : translated;
    }

    private TMP_Text ResolveTitleText()
    {
        if (title != null)
            return title;

        if (UiMingxinpian01Txt != null)
        {
            title = UiMingxinpian01Txt;
            return title;
        }

        Transform titleRoot = FindChildByName(transform, TitleNodeName);
        title = titleRoot != null ? titleRoot.GetComponentInChildren<TMP_Text>(true) : null;
        return title;
    }

    private void RefreshCurrentChest(bool isImmediate)
    {
        RefreshChest(GetVisibleConfig(), isImmediate);
    }

    private void RefreshChest(Config.PostcardConfig postcardConfig, bool isImmediate)
    {
        Postcard_Chest chestView = ResolveChest();
        if (chestView == null)
            return;

        if (IsConfigCompleted(postcardConfig) || IsUnlockConfig(postcardConfig))
        {
            chestView.Hide();
            return;
        }

        chestView.Show();
        chestView.RefreshItems(BuildChestRewards(postcardConfig));

        int maxProgress = GetChestMaxProgress(postcardConfig);
        int currentProgress = GetChestCurrentProgress(postcardConfig, maxProgress);
        chestView.RefreshProgress(currentProgress, maxProgress, isImmediate);
    }

    private Postcard_Chest ResolveChest()
    {
        if (chest != null)
            return chest;

        Transform chestRoot = FindChildByName(transform, ChestNodeName);
        chest = chestRoot != null ? chestRoot.GetComponent<Postcard_Chest>() : null;
        if (chest == null)
            chest = GetComponentInChildren<Postcard_Chest>(true);

        return chest;
    }

    #endregion

    #region Postcard Guide

    private bool TryStartPostcardGuidePart(System.Action onCompleted)
    {
        if (!Application.isPlaying)
            return false;

        if (!TryGetPostcardData(out Postcard postcardData) || !postcardData.IsGuiding)
        {
            StopPostcardGuide();
            return false;
        }

        Postcard_GuidePart guide = ResolveGuidePart();
        if (guide == null)
        {
            this.LogWarning("[PostcardPopup] Pending postcard guide skipped because guidePart is missing.");
            ConsumePendingGuideState(postcardData);
            return false;
        }

        bool started = _currentPage != null && guide.TryStartGuide(
            _currentPage,
            () =>
            {
                if (this == null)
                    return;

                RefreshButtonStates();
                onCompleted?.Invoke();
            });
        if (!started)
            this.LogWarning("[PostcardPopup] Pending postcard guide was consumed without a valid target.");

        ConsumePendingGuideState(postcardData);
        return started;
    }

    private bool TryGoNextPageForGuide()
    {
        int previousConfigIndex = _currentConfigIndex;
        bool wasShowingUnlockPage = _isShowingUnlockPage;
        TryGoNextPage(true);
        return previousConfigIndex != _currentConfigIndex ||
               wasShowingUnlockPage != _isShowingUnlockPage;
    }

    /// <summary>
    /// 用途：切换当前明信片弹窗的引导交互锁，锁定时禁止关闭和非引导翻页。
    /// 调用边界：由 PostcardGuideFlow 在 Popup 打开后立即锁定，并由 PostcardGuideFlow2 接管后续状态。
    /// 注意事项：仅刷新当前 Popup 按钮和 ClickBlock 状态，不写 DB、不创建流程；流程取消或失败时必须由当前持有者释放。
    /// </summary>
    /// <param name="isLocked">true 表示进入引导态并禁用普通按钮；false 表示恢复普通交互。</param>
    internal void SetPostcardGuideInteractionLocked(bool isLocked)
    {
        if (this == null)
            return;

        SetGuidingState(isLocked);
        RefreshButtonStates();
    }

    private async UniTask RunPostcardGuideFlow2Async(PostcardGuideFlow2 guideFlow)
    {
        try
        {
            GuideFlowResult result = await guideFlow.RunAsync(this.GetCancellationTokenOnDestroy());
            if (this != null && result == GuideFlowResult.Failed)
                this.LogWarning("[PostcardPopup] PostcardGuideFlow2 ended with Failed.");
        }
        catch (System.Exception exception)
        {
            if (this != null)
                this.LogError($"[PostcardPopup] PostcardGuideFlow2 threw an exception. error:{exception}");
        }
        finally
        {
            if (this != null && object.ReferenceEquals(_postcardGuideFlow2, guideFlow))
                _postcardGuideFlow2 = null;
        }
    }

    private void CancelPostcardGuideFlow2()
    {
        PostcardGuideFlow2 guideFlow = _postcardGuideFlow2;
        _postcardGuideFlow2 = null;
        guideFlow?.Cancel();
    }

    private void StopPostcardGuide()
    {
        Postcard_GuidePart guide = ResolveGuidePart();
        if (guide != null)
            guide.StopGuide();
    }

    private void SetGuidingState(bool isGuiding)
    {
        _isGuiding = isGuiding;
        RefreshClickBlock();
    }

    private void SetPagePlayingAnimationState(bool isPlaying)
    {
        IsPagePlayingAnim = isPlaying;
        RefreshClickBlock();
    }

    private void RefreshClickBlock()
    {
        GameObject currentClickBlock = ResolveClickBlock();
        if (currentClickBlock == null)
            return;

        bool shouldBlock = _isGuiding || IsPagePlayingAnim;
        if (currentClickBlock.activeSelf != shouldBlock)
            currentClickBlock.SetActive(shouldBlock);
    }

    /// <summary>
    /// 用途：获取当前 Popup 的点击遮罩，确保 prefab 引用意外丢失时仍能在 OnOpen 首帧按引导状态展开。
    /// 调用边界：仅由当前 PostcardPopup 生命周期和本地交互锁刷新调用。
    /// 注意事项：只查找当前 Popup 层级内名为 ClickBlock 的既有节点，不创建新对象或修改层级。
    /// </summary>
    private GameObject ResolveClickBlock()
    {
        if (clickBlock != null)
            return clickBlock;

        Transform clickBlockTransform = FindChildByName(transform, "ClickBlock");
        clickBlock = clickBlockTransform != null ? clickBlockTransform.gameObject : null;
        return clickBlock;
    }

    private PropBlock ResolveCoinPropBlock()
    {
        if (coinPropBlock != null)
            return coinPropBlock;

        coinPropBlock = GetComponentInChildren<PropBlock>(true);
        return coinPropBlock;
    }

    private void RefreshCoinAfterRewardPopupIfPending()
    {
        if (!_refreshCoinAfterRewardPopup)
            return;

        PropBlock propBlock = ResolveCoinPropBlock();
        if (propBlock == null)
            return;

        propBlock.UpdateData();
        _refreshCoinAfterRewardPopup = false;
    }

    private Postcard_GuidePart ResolveGuidePart()
    {
        if (guidePart != null)
            return guidePart;

        guidePart = GetComponentInChildren<Postcard_GuidePart>(true);
        return guidePart;
    }

    private bool ConsumePendingGuideState(Postcard postcardData)
    {
        if (postcardData == null || !postcardData.IsGuiding)
            return true;

        postcardData.IsGuiding = false;
        bool isSaved = DB.SavePostcard();
        if (!isSaved)
            this.LogWarning("[PostcardPopup] SavePostcard failed while consuming postcard guide state.");

        return isSaved;
    }

    #endregion

    #region Postcard Tips Bubble

    private void RefreshTipsBubble(Postcard postcardData)
    {
        PostcardPopupTipsBubble tipsBubble = ResolveTipsBubble();
        tipsBubble?.HideImmediately();
        tipsBubble?.SetReplayEnabled(postcardData != null && postcardData.HasShowTipsBubble);

        if (!Application.isPlaying ||
            postcardData == null ||
            !postcardData.HasGuide ||
            postcardData.IsGuiding ||
            postcardData.HasShowTipsBubble)
            return;

        if (tipsBubble == null)
        {
            this.LogWarning("[PostcardPopup] Tips bubble was not consumed because PostcardPopupTipsBubble is missing.");
            return;
        }

        if (!tipsBubble.TryShow())
        {
            this.LogWarning("[PostcardPopup] Tips bubble was not consumed because its display could not start.");
            return;
        }

        postcardData.HasShowTipsBubble = true;
        tipsBubble.SetReplayEnabled(true);
        if (!DB.SavePostcard())
            this.LogWarning("[PostcardPopup] SavePostcard failed while recording the tips bubble display state.");
    }

    private PostcardPopupTipsBubble ResolveTipsBubble()
    {
        if (_tipsBubble != null)
            return _tipsBubble;

        _tipsBubble = GetComponentInChildren<PostcardPopupTipsBubble>(true);
        return _tipsBubble;
    }

    private void HideTipsBubbleImmediately()
    {
        ResolveTipsBubble()?.HideImmediately();
    }

    #endregion

    #region Data Helpers

    private bool TryGetPostcardData(out Postcard postcardData)
    {
        postcardData = null;

        DBManager dbManager = Object.FindAnyObjectByType<DBManager>();
        if (dbManager == null || !dbManager.IsInitialized)
        {
            this.LogWarning("[PostcardPopup] DBManager is not initialized.");
            return false;
        }

        postcardData = dbManager.Get<Postcard>();
        if (postcardData == null)
        {
            this.LogWarning("[PostcardPopup] DB.Postcard is null.");
            return false;
        }

        postcardData.CompletedId ??= new List<string>();
        postcardData.CurrentCollectedPuzzledId ??= new List<int>();
        return true;
    }

    private bool HasValidCurrentConfig()
    {
        return _currentConfigIndex >= 0 && _currentConfigIndex < _postcardConfigs.Count;
    }

    private static bool IsUnlockConfig(Config.PostcardConfig postcardConfig)
    {
        return postcardConfig != null
            && (postcardConfig.UnlockProgress == null || postcardConfig.UnlockProgress.Count <= 0)
            && (postcardConfig.Rewards == null || postcardConfig.Rewards.Count <= 0)
            && postcardConfig.RewardMultiple <= 0;
    }

    private static int ComparePostcardConfigSort(Config.PostcardConfig left, Config.PostcardConfig right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;

        int sortCompare = left.Sort.CompareTo(right.Sort);
        if (sortCompare != 0)
            return sortCompare;

        return string.Compare(left.Id, right.Id, System.StringComparison.Ordinal);
    }

    private Config.PostcardConfig GetVisibleConfig()
    {
        if (_isShowingUnlockPage)
            return _unlockConfig;

        return HasValidCurrentConfig() ? _postcardConfigs[_currentConfigIndex] : null;
    }

    private static Dictionary<GameProps, object> BuildChestRewards(Config.PostcardConfig postcardConfig)
    {
        if (postcardConfig?.Rewards == null || postcardConfig.Rewards.Count <= 0)
            return null;

        Dictionary<GameProps, object> rewards = new Dictionary<GameProps, object>();
        foreach (var reward in postcardConfig.Rewards)
        {
            if (reward.Value != null)
                rewards[reward.Key] = reward.Value;
        }

        return rewards.Count > 0 ? rewards : null;
    }

    private static int GetChestMaxProgress(Config.PostcardConfig postcardConfig)
    {
        return postcardConfig?.UnlockProgress == null
            ? 0
            : postcardConfig.UnlockProgress.Count;
    }

    private int GetChestCurrentProgress(Config.PostcardConfig postcardConfig, int maxProgress)
    {
        if (postcardConfig == null || maxProgress <= 0)
            return 0;

        if (!TryGetPostcardData(out Postcard postcardData))
            return 0;

        if (postcardData.CompletedId.Contains(postcardConfig.Id))
            return maxProgress;

        int currentProgress = 0;
        foreach (int puzzleId in postcardConfig.UnlockProgress.Keys)
        {
            if (postcardData.CurrentCollectedPuzzledId.Contains(puzzleId))
                currentProgress++;
        }

        return currentProgress;
    }

    private int FindConfigIndex(string postcardId)
    {
        if (string.IsNullOrEmpty(postcardId))
            return -1;

        for (int i = 0; i < _postcardConfigs.Count; i++)
        {
            if (_postcardConfigs[i] != null && _postcardConfigs[i].Id == postcardId)
                return i;
        }

        return -1;
    }

    private bool IsConfigCompleted(Config.PostcardConfig postcardConfig)
    {
        return postcardConfig != null
            && TryGetPostcardData(out Postcard postcardData)
            && postcardData.CompletedId.Contains(postcardConfig.Id);
    }

    private bool AreAllPostcardPagesCompleted()
    {
        if (_postcardConfigs.Count <= 0 || !TryGetPostcardData(out Postcard postcardData))
            return false;

        for (int i = 0; i < _postcardConfigs.Count; i++)
        {
            Config.PostcardConfig postcardConfig = _postcardConfigs[i];
            if (postcardConfig == null || !postcardData.CompletedId.Contains(postcardConfig.Id))
                return false;
        }

        return true;
    }

    private bool SaveCurrentId(string postcardId)
    {
        if (string.IsNullOrEmpty(postcardId))
            return false;

        if (!TryGetPostcardData(out Postcard postcardData))
            return false;

        if (postcardData.CurrentId == postcardId)
            return true;

        string originalId = postcardData.CurrentId;
        postcardData.CurrentId = postcardId;
        if (DB.SavePostcard())
            return true;

        postcardData.CurrentId = originalId;
        this.LogWarning($"[PostcardPopup] SaveCurrentId failed. postcardId:{postcardId}");
        return false;
    }

    private void RefreshButtonStates()
    {
        bool hasCurrentConfig = HasValidCurrentConfig();
        bool canGoLast = hasCurrentConfig && (_isShowingUnlockPage || _currentConfigIndex > 0);
        bool canGoNext = false;
        if (hasCurrentConfig && !_isShowingUnlockPage)
        {
            bool hasNextConfig = _currentConfigIndex < _postcardConfigs.Count - 1;
            bool canShowTailUnlockPage = _unlockConfig != null
                && _currentConfigIndex == _postcardConfigs.Count - 1
                && IsConfigCompleted(_postcardConfigs[_currentConfigIndex])
                && !AreAllPostcardPagesCompleted();
            canGoNext = hasNextConfig || canShowTailUnlockPage;
        }
        bool interactable = !IsPagePlayingAnim && !_isGuiding;

        if (CloseBtn != null)
            CloseBtn.Interactable = interactable;

        if (LastBtn != null)
        {
            LastBtn.gameObject.SetActive(canGoLast);
            LastBtn.Interactable = interactable && canGoLast;
        }

        if (NextBtn != null)
        {
            NextBtn.gameObject.SetActive(canGoNext);
            NextBtn.Interactable = interactable && canGoNext;
        }
    }

    #endregion

    public static PostcardPopup Create()
    {
        var panel = UIManager.Instance.OpenUI<PostcardPopup>($"{typeof(PostcardPopup).Name}", UILayer.Popup);
        return panel;
    }
}

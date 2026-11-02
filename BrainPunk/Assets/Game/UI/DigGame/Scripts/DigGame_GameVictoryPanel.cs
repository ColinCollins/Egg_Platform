using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Config;
using DG.Tweening;
using Game.Events;
using Game.Scripts.Common;
using GameCommon;
using UnityEngine;
using UnityEngine.Serialization;
using static Products;

/// <summary>
/// Dig 玩法专用胜利面板。
/// 复用 GameVictoryPanel 的界面资源与进场节奏，但导航、重玩和配置数据全部由 DigPlayCtrl 注入。
/// </summary>
public class DigGame_GameVictoryPanel : BaseUIView, IEventSender, IDebuger
{
    #region Constants

    private const float NextButtonFadeDuration = 0.2f;
    private const float NextButtonFadeDelay = 0.15f;
    private const float SharePartShowDelay = 2f;
    private const string NoAdProductName = ProductName.NewnoAds;

    #endregion

    #region Inspector

    /// <summary>
    /// 用途：控制 Dig 胜利面板的入场与待机 Spine 动画。
    /// 调用边界：必须由 DigGame_GameVictoryPanel prefab 显式绑定。
    /// 注意事项：缺失时会跳过 Spine 表现并直接推进后续流程。
    /// </summary>
    [SerializeField] private UISpineCtrl spineCtrl;

    /// <summary>
    /// 用途：承载下一关按钮的透明度渐显表现。
    /// 调用边界：必须绑定下一关按钮所在节点的 CanvasGroup。
    /// 注意事项：缺失时下一关按钮仍会直接进入可用状态，但没有渐显效果。
    /// </summary>
    [FormerlySerializedAs("nextBtnGroup")]
    [SerializeField] private CanvasGroup nextButtonGroup;

    /// <summary>
    /// 用途：处理 Dig 胜利后的下一关操作。
    /// 调用边界：必须由 DigGame_GameVictoryPanel prefab 显式绑定。
    /// 注意事项：按钮只会在入场与星星动画都完成后开放。
    /// </summary>
    [FormerlySerializedAs("NextBtn")]
    [SerializeField] private CustomButton nextButton;

    /// <summary>
    /// 用途：处理 Dig 胜利后的当前关卡重玩操作。
    /// 调用边界：必须由 DigGame_GameVictoryPanel prefab 显式绑定。
    /// 注意事项：导航触发后会立即关闭交互，避免重复派发事件。
    /// </summary>
    [FormerlySerializedAs("resetBtn")]
    [SerializeField] private CustomButton replayButton;

    /// <summary>
    /// 用途：播放 Dig 胜利面板打开时的粒子表现。
    /// 调用边界：由 prefab 显式维护需要重播的粒子列表。
    /// 注意事项：允许为空，不会阻断面板流程。
    /// </summary>
    [SerializeField] private ParticleSystem[] openEffects;


    /// <summary>
    /// 用途：控制 Dig 胜利面板的评价与分享按钮组。
    /// 调用边界：普通版与奖励版均通过 prefab 绑定并启用；Configure 会按当前 Dig 关卡刷新数据。
    /// 注意事项：Configure 会注入 Dig 关卡上下文，禁止回退到主线关卡分享存档。
    /// </summary>
    [SerializeField] private GameVictoryPanel_Share sharePart;

    /// <summary>
    /// 用途：处理 Dig 胜利面板的免广告购买入口。
    /// 调用边界：普通版与奖励版 prefab 均需显式绑定，购买流程由基类统一处理。
    /// 注意事项：购买中会锁定胜利面板导航，已拥有免广告权益时自动隐藏。
    /// </summary>
    [SerializeField] private CustomButton NoAdBtn;
    /// <summary>复用纯表现的 Draw 星组；只展示 Configure 从独立 Dig 档缓存的最高星及待播星，不读取 Draw 存档。</summary>
    [SerializeField] private DrawGame_VictoryStarGroup victoryStarGroup;

    #endregion

    #region Runtime State

    private Tween _nextButtonFadeTween;
    private bool _isNextButtonReady;
    private bool _isNavigationTriggered;
    private bool _isEntranceAnimationComplete;
    private bool _isPresentationReady;
    private bool _isConfigured;
    private bool _hasHidden;
    private bool _isNoAdsPurchasing;
    private int _collectedStars, _newStars;
    private bool _starsStarted;
    /// <summary>当前打开生命周期版本；原生广告/购买回调不得改写复用后的面板。</summary>
    protected int PresentationVersion { get; private set; }

    /// <summary>
    /// 用途：供派生胜利面板在刷新其它原生覆盖层入口时读取免广告购买状态。
    /// 调用边界：仅用于交互门禁，不允许派生类改写购买生命周期。
    /// </summary>
    protected bool IsNoAdsPurchasing => _isNoAdsPurchasing;

    #endregion

    #region UI Lifecycle

    public override void OnCreate()
    {
        base.OnCreate();

        ValidateInspectorReferences();
        if (nextButton != null)
            nextButton.OnClick += OnNextLevel;
        if (replayButton != null)
            replayButton.OnClick += OnReplay;
        if (NoAdBtn != null)
            NoAdBtn.OnClick += PurchaseNoAd;
        BindButtonsExtra();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        PresentationVersion++;

        ResetOpenState();
        PlayOpenEffects();
        PlayOpenAnimation();
        PrepareSharePart();
        OnOpenExtra();
        AudioManager.PlaySound("success");
    }

    public override void OnShow()
    {
        base.OnShow();
        if (_hasHidden)
        {
            ShowSharePart();
            _hasHidden = false;
        }

        OnShowExtra();
    }

    public override void OnHide()
    {
        _hasHidden = true;
        OnHideExtra();
        base.OnHide();
    }

    public override void OnClose()
    {
        PresentationVersion++;
        StopTransientAnimations();
        UnbindSpineStateCallbacks();
        CancelInvoke(nameof(ShowSharePart));
        _isConfigured = false;
        _hasHidden = false;
        OnCloseExtra();
        base.OnClose();
    }

    private void Update()
    {
        UpdateExtra();
    }

    #endregion

    #region Configuration

    /// <summary>
    /// 注入本次 Dig 胜利展示所需的数据。
    /// 调用边界：仅由 DigPlayCtrl 在 Create 返回后立即调用。
    /// 注意事项：控制器已保存星级，面板读取并消费 Dig 待播记录；导航操作通过既有玩法事件派发。
    /// </summary>
    /// <param name="levelData">当前胜利关卡的 Dig 配置。</param>
    public void Configure(DigLevelData levelData)
    {
        CacheAndConsumeVictoryStars(levelData);
        if (SupportsSharePart && sharePart != null)
            sharePart.RefreshDigData(levelData);

        _isConfigured = true;

        if (_isEntranceAnimationComplete)
        {
            RefreshPresentation();
        }
    }

    /// <summary>沿用 Draw 的消费时机：配置面板时缓存最高星／待播星，删除当前 Dig 关卡等待项并保存。</summary>
    private void CacheAndConsumeVictoryStars(DigLevelData levelData)
    {
        _collectedStars = 0;
        _newStars = 0;
        if (levelData == null || string.IsNullOrEmpty(levelData.Id))
            return;

        var saveData = DB.DigGame;
        if (saveData == null)
        {
            this.LogWarning($"[DigGame_GameVictoryPanel] 无法读取 Dig 星级存档。levelId:{levelData.Id}");
            return;
        }

        bool changed = saveData.EnsureValidCollections();
        if (saveData.LevelCollectedStar.TryGetValue(levelData.Id, out int collectedStars))
            _collectedStars = Mathf.Clamp(collectedStars, 0, 3);
        if (saveData.LevelWaitingAnimStar.TryGetValue(levelData.Id, out int waitingStars))
        {
            _newStars = Mathf.Clamp(waitingStars, 0, _collectedStars);
            saveData.LevelWaitingAnimStar.Remove(levelData.Id);
            changed = true;
        }
        if (changed && !DB.SaveDigGame())
            this.LogWarning($"[DigGame_GameVictoryPanel] 消费 Dig 待播星保存失败。levelId:{levelData.Id}");
    }



    #endregion

    #region Button Handlers

    private void OnNextLevel(CustomButton button)
    {
        if (_isNavigationTriggered || !_isNextButtonReady || !_isPresentationReady || _isNoAdsPurchasing || HasExtraBusyState())
            return;

        _isNavigationTriggered = true;
        RefreshButtonStates();
        if (UIManager.Instance == null)
        {
            this.LogError("[DigGame_GameVictoryPanel] Next level failed. UIManager is null.");
            return;
        }

        UIManager.Instance.DestroyUI(this);
        this.DispatchEvent(Witness<EnterNextLevelEvent>._);
    }

    private void OnReplay(CustomButton button)
    {
        if (_isNavigationTriggered || _isNoAdsPurchasing || HasExtraBusyState())
            return;

        _isNavigationTriggered = true;
        RefreshButtonStates();
        if (UIManager.Instance == null)
        {
            this.LogError("[DigGame_GameVictoryPanel] Replay failed. UIManager is null.");
            return;
        }

        UIManager.Instance.DestroyUI(this);
        this.DispatchEvent(Witness<GameReplayEvent>._);
    }

    private void PurchaseNoAd(CustomButton button)
    {
        if (NoAdBtn == null)
            return;

        if (_isNoAdsPurchasing || HasExtraBusyState())
        {
            this.LogWarning(
                "[DigGame_GameVictoryPanel] NoAdBtn ignored. purchase already in progress.");
            return;
        }

        var purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager == null)
        {
            this.LogError(
                "[DigGame_GameVictoryPanel] NoAdBtn failed. PurchaseManager is null.");
            return;
        }

        if (HasNoAds())
        {
            this.LogWarning(
                "[DigGame_GameVictoryPanel] NoAdBtn ignored. NoAds already purchased.");
            RefreshButtonStates();
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            purchaseManager.OnPurchaseFailed();
            return;
        }

        var data = Products.GetProductId(NoAdProductName);
        if (data == null)
        {
            this.LogError(
                "[DigGame_GameVictoryPanel] NoAdBtn failed. product data is null.");
            purchaseManager.OnPurchaseFailed();
            return;
        }

        _isNoAdsPurchasing = true;
        RefreshButtonStates();
        GameSDKService.Instance?.IAP_CLK(data.Sku);
        int version = PresentationVersion;
        purchaseManager.Purchase(
            NoAdProductName,
            nameof(DigGame_GameVictoryPanel),
            (productName, success) => OnPurchaseNoAdCallback(productName, success, version));
    }

    private void OnPurchaseNoAdCallback(string productName, bool isSuccess, int version)
    {

        var purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager == null)
        {
            this.LogError(
                $"[DigGame_GameVictoryPanel] NoAdBtn callback failed. PurchaseManager is null. productName:{productName}, isSuccess:{isSuccess}");
            if (this != null && IsOpen && version == PresentationVersion)
            {
                _isNoAdsPurchasing = false;
                RefreshButtonStates();
            }
            return;
        }

        if (isSuccess)
            purchaseManager.OnPurchaseSuccess(productName);
        else
            purchaseManager.OnPurchaseFailed();

        if (this == null || !IsOpen || version != PresentationVersion)
            return;

        _isNoAdsPurchasing = false;
        RefreshButtonStates();
    }

    #endregion

    #region Presentation


    /// <summary>
    /// 用途：指定当前 Dig 胜利面板的 Spine 入场动画名。
    /// 调用边界：普通新版界面使用 2_appear01；动画命名不同的派生面板必须覆写。
    /// </summary>
    protected virtual string EntranceAnimationName => "2_appear01";

    /// <summary>
    /// 用途：指定当前 Dig 胜利面板入场完成后的循环待机动画名。
    /// 调用边界：普通新版界面使用 2_idle；动画命名不同的派生面板必须与入场动画一起覆写。
    /// </summary>
    protected virtual string IdleAnimationName => "2_idle";

    /// <summary>
    /// 用途：声明当前 Dig 胜利面板是否展示评价与分享区域。
    /// 调用边界：普通版保持 true；不支持 Dig 分享上下文的派生资源必须覆写为 false。
    /// </summary>
    protected virtual bool SupportsSharePart => true;

    private void ValidateInspectorReferences()
    {
        if (spineCtrl == null ||
            nextButton == null ||
            replayButton == null)
        {
            this.LogError(
                "[DigGame_GameVictoryPanel] Required Inspector references are missing.");
        }
    }

    private void ResetOpenState()
    {
        StopTransientAnimations();
        UnbindSpineStateCallbacks();
        _isNextButtonReady = false;
        _isNavigationTriggered = false;
        _isEntranceAnimationComplete = false;
        _isPresentationReady = false;
        _isConfigured = false;
        _isNoAdsPurchasing = false;
        _starsStarted = false;
        _collectedStars = _newStars = 0;
        ResetRuntimeStateExtra();

        if (nextButtonGroup != null)
            nextButtonGroup.alpha = 0f;

        ResetOpenStateExtra();
        RefreshButtonStates();
    }

    private void PlayOpenEffects()
    {
        if (openEffects == null)
            return;

        for (int i = 0; i < openEffects.Length; i++)
        {
            ParticleSystem effect = openEffects[i];
            if (effect == null)
                continue;

            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Play(true);
        }
    }

    private void PlayOpenAnimation()
    {
        UnbindSpineStateCallbacks();
        if (spineCtrl == null)
        {
            OnEntranceAnimationComplete(null);
            return;
        }

        Spine.TrackEntry track = spineCtrl.PlayAnimation(EntranceAnimationName, false);
        Spine.AnimationState state = spineCtrl.GetAnimationState();
        if (track == null || state == null)
        {
            OnEntranceAnimationComplete(null);
            return;
        }

        state.Complete += OnEntranceAnimationComplete;
    }

    private void OnEntranceAnimationComplete(Spine.TrackEntry track)
    {
        if (_isEntranceAnimationComplete)
            return;

        _isEntranceAnimationComplete = true;
        RevealNextButton();
        UnlockNextButton();
        OnEntranceAnimationCompleteExtra();
        RefreshPresentation();

        if (spineCtrl != null)
            spineCtrl.PlayAnimation(IdleAnimationName, true);

        UnbindSpineStateCallbacks();
    }

    private void RefreshPresentation()
    {
        if (!_isConfigured || !_isEntranceAnimationComplete || _starsStarted) return;
        _starsStarted = true;
        int version = PresentationVersion;
        void CompleteStars()
        {
            if (this == null || !IsOpen || version != PresentationVersion) return;
            _isPresentationReady = true;
            RefreshButtonStates();
        }
        if (!victoryStarGroup || !victoryStarGroup.Refresh(_collectedStars, _newStars, CompleteStars)) CompleteStars();
    }







    private void PrepareSharePart()
    {
        CancelInvoke(nameof(ShowSharePart));
        if (sharePart == null)
            return;

        sharePart.gameObject.SetActive(SupportsSharePart);
        if (SupportsSharePart)
            Invoke(nameof(ShowSharePart), SharePartShowDelay);
    }

    private void ShowSharePart()
    {
        if (!SupportsSharePart || sharePart == null || !_isConfigured)
            return;

        sharePart.Show();
    }

    private void RevealNextButton()
    {
        if (nextButtonGroup == null)
        {
            if (nextButton != null)
                nextButton.gameObject.SetActive(true);
            return;
        }

        _nextButtonFadeTween?.Kill();
        _nextButtonFadeTween = nextButtonGroup
            .DOFade(1f, NextButtonFadeDuration)
            .SetDelay(NextButtonFadeDelay)
            .SetUpdate(true);
    }

    private void UnlockNextButton()
    {
        _isNextButtonReady = true;
        RefreshButtonStates();
    }

    protected virtual void RefreshButtonStates()
    {
        bool hasExtraBusyState = HasExtraBusyState();
        bool hasNoAds = HasNoAds();
        if (nextButton != null)
        {
            nextButton.Interactable =
                !_isNavigationTriggered &&
                _isNextButtonReady &&
                _isPresentationReady &&
                !_isNoAdsPurchasing &&
                !hasExtraBusyState;
        }

        if (replayButton != null)
        {
            replayButton.gameObject.SetActive(true);
            replayButton.Interactable =
                !_isNavigationTriggered &&
                !_isNoAdsPurchasing &&
                !hasExtraBusyState;
        }

        if (NoAdBtn != null)
        {
            NoAdBtn.Interactable =
                !hasNoAds &&
                !_isNoAdsPurchasing &&
                !hasExtraBusyState;
            NoAdBtn.gameObject.SetActive(!hasNoAds);
        }
    }

    private bool HasNoAds()
    {
        var bag = GameManager.Instance?.Bag;
        if (bag != null && bag.GetToolCount(Config.Game.GameProps.NoAds) > 0)
            return true;

        var purchaseManager = GameManager.Instance?.Purchase;
        return purchaseManager != null &&
               (purchaseManager.IsPurchased(NoAdProductName) ||
                purchaseManager.IsPurchased(ProductName.NoAds) ||
                purchaseManager.IsPurchased(ProductName.NoadBundle) ||
                purchaseManager.IsPurchased(ProductName.NewnoadBundle));
    }

    private void StopTransientAnimations()
    {
        _nextButtonFadeTween?.Kill();
        _nextButtonFadeTween = null;
    }

    private void UnbindSpineStateCallbacks()
    {
        Spine.AnimationState state = spineCtrl?.GetAnimationState();
        if (state != null)
            state.Complete -= OnEntranceAnimationComplete;
    }

    protected virtual void BindButtonsExtra()
    {
    }

    protected virtual void OnOpenExtra()
    {
    }

    protected virtual void OnShowExtra()
    {
    }

    protected virtual void OnHideExtra()
    {
    }

    protected virtual void OnCloseExtra()
    {
    }

    protected virtual void UpdateExtra()
    {
    }

    protected virtual void ResetOpenStateExtra()
    {
    }

    protected virtual void ResetRuntimeStateExtra()
    {
    }

    protected virtual void OnEntranceAnimationCompleteExtra()
    {
    }

    protected virtual bool HasExtraBusyState()
    {
        return false;
    }

    #endregion

    #region Factory

    /// <summary>
    /// 打开 Dig 玩法专用胜利面板并注入本次结算数据。
    /// 调用边界：仅由活动中的 DigPlayCtrl 在收到 Success 结算后调用。
    /// </summary>
    public static DigGame_GameVictoryPanel Create(DigLevelData levelData)
    {
        if (levelData == null || UIManager.Instance == null)
            return null;

        DigGame_GameVictoryPanel panel =
            UIManager.Instance.OpenUI<DigGame_GameVictoryPanel>(
                nameof(DigGame_GameVictoryPanel),
                UILayer.Popup);
        panel?.Configure(levelData);
        return panel;
    }

    #endregion
}

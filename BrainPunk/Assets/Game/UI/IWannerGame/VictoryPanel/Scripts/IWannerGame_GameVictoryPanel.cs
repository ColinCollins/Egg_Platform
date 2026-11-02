using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.ConfigModule;
using Game.Events;
using Game.Scripts.Common;
using GameCommon;
using I2.Loc;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using static Products;

/// <summary>
/// IWanner Group 通关后的普通胜利面板。
/// 复用主线 Victory 的入场、提示、分享与导航节奏，并使用 IWanner 独立进度保存分享奖励状态。
/// </summary>
public class IWannerGame_GameVictoryPanel : BaseUIView, IEventSender, IDebuger
{
    #region Constants

    private const float NextButtonFadeDuration = 0.2f;
    private const float NextButtonFadeDelay = 0.15f;
    private const float TipsTypewriterDuration = 1f;
    private const float TipsFallbackDelay = 2f;
    private const string NoAdProductName = ProductName.NewnoAds;

    #endregion

    #region Inspector

    /// <summary>
    /// 用途：控制当前 IWanner 胜利布局中唯一的 Spine 入场与待机动画。
    /// 调用边界：由普通版和首次通关版 prefab 根组件显式绑定。
    /// 注意事项：缺失或动画不存在时会跳过 Spine 表现并继续提示文案流程。
    /// </summary>
    [SerializeField] private UISpineCtrl spineCtrl;

    /// <summary>
    /// 用途：显示当前 IWanner Group 配置的通关提示文案。
    /// 调用边界：由普通版和首次通关版 prefab 的 tips_txt 显式绑定。
    /// 注意事项：缺失时跳过打字机表现并直接开放导航，不阻断结算流程。
    /// </summary>
    [SerializeField] private TextMeshProUGUI tipsTxt;

    /// <summary>
    /// 用途：承载下一步按钮区域的透明度渐显表现。
    /// 调用边界：必须绑定包含下一步按钮的 CanvasGroup。
    /// 注意事项：缺失时按钮仍会在入场完成后开放，只是不播放渐显。
    /// </summary>
    [FormerlySerializedAs("nextBtnGroup")]
    [SerializeField] private CanvasGroup nextButtonGroup;

    /// <summary>
    /// 用途：结束当前 Group 结算并返回 IWanner 选组界面。
    /// 调用边界：必须绑定当前胜利 prefab 的 next_btn。
    /// 注意事项：入场完成前与任一导航触发后不可交互。
    /// </summary>
    [FormerlySerializedAs("NextBtn")]
    [SerializeField] private CustomButton nextButton;

    /// <summary>
    /// 用途：从当前 Group 第 0 小关重新开始。
    /// 调用边界：必须绑定当前胜利 prefab 的 replay_btn。
    /// 注意事项：只派发共享 GameReplayEvent，具体 Group Replay 由活动 IWannerPlayCtrl 处理。
    /// </summary>
    [FormerlySerializedAs("resetBtn")]
    [SerializeField] private CustomButton replayButton;

    /// <summary>
    /// 用途：播放胜利面板每次打开时的粒子表现。
    /// 调用边界：由 prefab 维护需要重播的粒子列表。
    /// 注意事项：允许为空，不影响导航流程。
    /// </summary>
    [SerializeField] private ParticleSystem[] openEffects;

    /// <summary>
    /// 用途：控制普通版 IWanner Victory 的分享按钮区域。
    /// 调用边界：普通版运行时保持显示；Reward 版由 GameVictoryPanel_Share 接管并覆写关闭此入口。
    /// 注意事项：分享领取状态只写入 DB.IWannerGame.SharedGroupIds。
    /// </summary>
    [SerializeField] private GameObject sharePart;

    /// <summary>
    /// 用途：分享当前应用商店链接并触发首次分享奖励。
    /// 调用边界：仅供普通版 Victory 使用，Reward 版保持为空并使用独立分享组件。
    /// 注意事项：按钮始终可分享，但每个 IWanner Group 只发放一次金币。
    /// </summary>
    [SerializeField] private CustomButton shareButton;

    /// <summary>
    /// 用途：显示远端配置中的胜利分享金币数量。
    /// 调用边界：未领取当前 Group 分享奖励时显示，领取后隐藏。
    /// 注意事项：只负责展示，不作为实际发奖数值来源。
    /// </summary>
    [SerializeField] private TextMeshProUGUI shareRewardTxt;

    /// <summary>
    /// 用途：普通版分享发奖后播放金币飞入动画。
    /// 调用边界：prefab 存在公共奖励动画控制器时绑定；缺失只跳过表现，不影响到账和存档。
    /// 注意事项：动画起点使用 shareButton，终点由控制器自身配置。
    /// </summary>
    [SerializeField] private CommonUIRewardAnimCtrl shareRewardAnimCtrl;

    /// <summary>
    /// 用途：提供与主线 Victory 一致的免广告购买入口。
    /// 调用边界：普通版和 Reward 版 prefab 都绑定按钮根节点，OnCreate 会从根节点解析 CustomButton。
    /// 注意事项：已拥有免广告权益时隐藏；购买期间会锁定导航与 Reward 广告入口。
    /// </summary>
    [FormerlySerializedAs("NoAdBtn")]
    [SerializeField] private GameObject noAdPart;

    #endregion

    #region Runtime State

    private Tween _nextButtonFadeTween;
    private Tween _tipsTween;
    private Tween _tipsFallbackTween;
    private bool _isNavigationReady;
    private bool _isNavigationTriggered;
    private bool _hideOneLinkReplay;
    private bool _isEntranceAnimationComplete;
    private bool _isNoAdsPurchasing;
    private CustomButton _noAdButton;
    private string _tipsKey;
    private int _currentGroupId = -1;
    private string _currentLevelPath = "no_start";

    /// <summary>
    /// 用途：供首次通关面板判断是否已经完成入场。
    /// 调用边界：仅允许派生类控制本地展示恢复，不得据此修改 Group 进度。
    /// </summary>
    protected bool IsEntranceAnimationComplete => _isEntranceAnimationComplete;

    /// <summary>
    /// 用途：向 Reward 派生面板提供当前结算 GroupId。
    /// 调用边界：只允许用于胜利评价/分享上下文，不得据此推进关卡进度。
    /// </summary>
    protected int CurrentGroupId => _currentGroupId;

    /// <summary>
    /// 用途：向 Reward 派生面板提供当前结算小关 Path。
    /// 调用边界：仅用于评价打点上下文，不参与资源加载或导航。
    /// </summary>
    protected string CurrentLevelPath => _currentLevelPath;

    /// <summary>
    /// 用途：供 Reward 派生面板在刷新广告入口时读取免广告购买状态。
    /// 调用边界：仅用于交互门禁，不允许派生类改写购买生命周期。
    /// </summary>
    protected bool IsNoAdsPurchasing => _isNoAdsPurchasing;

    #endregion

    #region UI Lifecycle

    public override void OnCreate()
    {
        base.OnCreate();

        _noAdButton = noAdPart != null ? noAdPart.GetComponent<CustomButton>() : null;
        ValidateInspectorReferences();
        if (nextButton != null)
        {
            nextButton.OnClick -= OnNextLevel;
            nextButton.OnClick += OnNextLevel;
        }

        if (replayButton != null)
        {
            replayButton.OnClick -= OnReplay;
            replayButton.OnClick += OnReplay;
        }

        if (shareButton != null)
        {
            shareButton.OnClick -= OnClickShare;
            shareButton.OnClick += OnClickShare;
        }

        if (_noAdButton != null)
        {
            _noAdButton.OnClick -= PurchaseNoAd;
            _noAdButton.OnClick += PurchaseNoAd;
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();

        _hideOneLinkReplay = Game.OneLinkManager.Instance.IsCurrentEntry(Config.Level.GameType.IWanner);
        InitializeVictoryContext();
        ResetOpenState();
        ConfigureSupplementaryParts();
        PlayOpenEffects();
        PlayOpenAnimation();
        OnOpenExtra();
        AudioManager.PlaySound("success");
    }

    public override void OnShow()
    {
        base.OnShow();
        RefreshStandaloneShareView();
        OnShowExtra();
    }

    public override void OnHide()
    {
        OnHideExtra();
        base.OnHide();
    }

    public override void OnClose()
    {
        StopTransientAnimations();
        UnbindSpineStateCallbacks();
        CancelInvoke();
        OnCloseExtra();
        base.OnClose();
    }

    private void Update()
    {
        UpdateExtra();
    }

    #endregion

    #region Button Handlers

    private void OnNextLevel(CustomButton _)
    {
        if (_isNavigationTriggered || !_isNavigationReady || _isNoAdsPurchasing || HasExtraBusyState())
            return;

        _isNavigationTriggered = true;
        RefreshButtonStates();
        if (UIManager.Instance == null)
        {
            this.LogError("[IWannerGame_GameVictoryPanel] Return to level selection failed. UIManager is null.");
            return;
        }

        UIManager.Instance.DestroyUI(this);
        this.DispatchEvent(Witness<EnterNextLevelEvent>._);
    }

    private void OnReplay(CustomButton _)
    {
        if (_hideOneLinkReplay || _isNavigationTriggered || !_isNavigationReady || _isNoAdsPurchasing || HasExtraBusyState())
            return;

        _isNavigationTriggered = true;
        RefreshButtonStates();
        if (UIManager.Instance == null)
        {
            this.LogError("[IWannerGame_GameVictoryPanel] Replay failed. UIManager is null.");
            return;
        }

        UIManager.Instance.DestroyUI(this);
        this.DispatchEvent(Witness<GameReplayEvent>._);
    }

    private void OnClickShare(CustomButton _)
    {
        ShareStoreUrl();

        if (shareRewardTxt != null)
            shareRewardTxt.gameObject.SetActive(false);

        if (HasSharedCurrentGroup())
            return;

        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            this.LogError("[IWannerGame_GameVictoryPanel] Share reward grant failed. Bag is null.");
            return;
        }

        if (_currentGroupId <= 0)
        {
            this.LogError("[IWannerGame_GameVictoryPanel] Share reward grant failed. GroupId is invalid.");
            return;
        }

        int coins = ConfigManager.RemoteConfig.GetLevelVictoryShareCoinReward();
        bag.AddTool(Config.Game.GameProps.Coin, coins, RewardType.Media);
        MarkCurrentGroupShared();

        if (shareRewardAnimCtrl != null)
            shareRewardAnimCtrl.PlayFrom(shareButton != null ? shareButton.transform : transform, 1).Forget();
    }

    private void PurchaseNoAd(CustomButton _)
    {
        if (_noAdButton == null)
            return;

        if (_isNoAdsPurchasing || HasExtraBusyState())
        {
            this.LogWarning(
                "[IWannerGame_GameVictoryPanel] NoAdBtn ignored. purchase already in progress or reward flow is busy.");
            return;
        }

        var purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager == null)
        {
            this.LogError(
                "[IWannerGame_GameVictoryPanel] NoAdBtn failed. PurchaseManager is null.");
            return;
        }

        if (HasNoAds())
        {
            this.LogWarning(
                "[IWannerGame_GameVictoryPanel] NoAdBtn ignored. NoAds already purchased.");
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
                "[IWannerGame_GameVictoryPanel] NoAdBtn failed. product data is null.");
            purchaseManager.OnPurchaseFailed();
            return;
        }

        _isNoAdsPurchasing = true;
        RefreshButtonStates();
        GameSDKService.Instance?.IAP_CLK(data.Sku);
        purchaseManager.Purchase(
            NoAdProductName,
            nameof(IWannerGame_GameVictoryPanel),
            OnPurchaseNoAdCallback);
    }

    private void OnPurchaseNoAdCallback(string productName, bool isSuccess)
    {
        _isNoAdsPurchasing = false;

        var purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager == null)
        {
            this.LogError(
                $"[IWannerGame_GameVictoryPanel] NoAdBtn callback failed. PurchaseManager is null. productName:{productName}, isSuccess:{isSuccess}");
            RefreshButtonStates();
            return;
        }

        if (isSuccess)
            purchaseManager.OnPurchaseSuccess(productName);
        else
            purchaseManager.OnPurchaseFailed();

        if (this == null || !isActiveAndEnabled)
            return;

        RefreshButtonStates();
    }

    #endregion

    #region Presentation

    /// <summary>
    /// 用途：指定普通 IWanner 胜利面板当前 Spine 资源的入场动画。
    /// 调用边界：动画命名不同的派生面板必须与待机动画一起覆写。
    /// </summary>
    protected virtual string EntranceAnimationName => "in";

    /// <summary>
    /// 用途：指定普通 IWanner 胜利面板当前 Spine 资源的循环待机动画。
    /// 调用边界：动画命名不同的派生面板必须与入场动画一起覆写。
    /// </summary>
    protected virtual string IdleAnimationName => "idle";

    /// <summary>
    /// 用途：声明是否由基类播放下一步按钮区域渐显。
    /// 调用边界：首次通关版会覆写并由自己的整组入场动画接管。
    /// </summary>
    protected virtual bool RevealNavigationWithBase => true;

    /// <summary>
    /// 用途：声明是否使用基类的单分享按钮逻辑。
    /// 调用边界：普通版返回 true；Reward 版必须返回 false 并由 GameVictoryPanel_Share 接管。
    /// </summary>
    protected virtual bool UsesStandaloneShareButton => true;

    private void ValidateInspectorReferences()
    {
        if (spineCtrl == null || tipsTxt == null || nextButton == null || replayButton == null)
        {
            this.LogError(
                "[IWannerGame_GameVictoryPanel] Required spine, tips, next or replay reference is missing.");
        }

        if (UsesStandaloneShareButton &&
            (sharePart == null || shareButton == null || shareRewardTxt == null))
        {
            this.LogError(
                "[IWannerGame_GameVictoryPanel] Required standalone share reference is missing.");
        }

        if (noAdPart == null || _noAdButton == null)
        {
            this.LogError(
                "[IWannerGame_GameVictoryPanel] Required NoAd button reference is missing.");
        }
    }

    private void ResetOpenState()
    {
        StopTransientAnimations();
        UnbindSpineStateCallbacks();
        _isNavigationReady = false;
        _isNavigationTriggered = false;
        _isEntranceAnimationComplete = false;
        _isNoAdsPurchasing = false;

        if (nextButtonGroup != null)
            nextButtonGroup.alpha = 0f;
        if (tipsTxt != null)
            tipsTxt.text = string.Empty;

        ResetOpenStateExtra();
        RefreshButtonStates();
    }

    private void ConfigureSupplementaryParts()
    {
        if (sharePart != null)
            sharePart.SetActive(UsesStandaloneShareButton);
    }

    private void InitializeVictoryContext()
    {
        _currentGroupId = DB.IWannerGame?.CurrentGroupID ?? -1;
        _currentLevelPath = "no_start";
    }

    private void RefreshStandaloneShareView()
    {
        if (!UsesStandaloneShareButton)
            return;

        if (sharePart != null)
            sharePart.SetActive(true);

        if (shareButton != null)
            shareButton.gameObject.SetActive(true);

        if (shareRewardTxt == null)
            return;

        shareRewardTxt.gameObject.SetActive(!HasSharedCurrentGroup());
        shareRewardTxt.text = "+" + ConfigManager.RemoteConfig.GetLevelVictoryShareCoinReward();
    }

    private bool HasSharedCurrentGroup()
    {
        if (_currentGroupId <= 0)
            return false;

        List<int> sharedGroupIds = DB.IWannerGame?.SharedGroupIds;
        return sharedGroupIds != null && sharedGroupIds.Contains(_currentGroupId);
    }

    private void MarkCurrentGroupShared()
    {
        var saveData = DB.IWannerGame;
        if (saveData == null)
        {
            this.LogError("[IWannerGame_GameVictoryPanel] Share reward state save failed. IWannerGame is null.");
            return;
        }

        saveData.EnsureValidCollections();
        if (saveData.SharedGroupIds.Contains(_currentGroupId))
            return;

        saveData.SharedGroupIds.Add(_currentGroupId);
        if (!DB.SaveIWannerGame())
            this.LogError("[IWannerGame_GameVictoryPanel] Share reward state save failed.");
    }

    private static void ShareStoreUrl()
    {
        string url = "https://link.brainpunk.studio/store/install";
#if UNITY_ANDROID
        AndroidShare.ShareText(url.Trim());
#elif UNITY_IOS
        IosShare.ShareText(url.Trim());
#endif
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

    private void OnEntranceAnimationComplete(Spine.TrackEntry _)
    {
        if (_isEntranceAnimationComplete)
            return;

        _isEntranceAnimationComplete = true;
        if (RevealNavigationWithBase)
            RevealNextButton();

        OnEntranceAnimationCompleteExtra();

        if (spineCtrl != null)
            spineCtrl.PlayAnimation(IdleAnimationName, true);

        UnbindSpineStateCallbacks();
        RefreshTips();
    }

    private void RefreshTips()
    {
        if (string.IsNullOrEmpty(_tipsKey))
        {
            this.LogWarning("[IWannerGame_GameVictoryPanel] CongraktTips key is null or empty.");
            UnlockNavigation();
            return;
        }

        string translated = LocalizationManager.GetTranslation(_tipsKey, false);
        if (string.IsNullOrEmpty(translated))
        {
            this.LogError($"[IWannerGame_GameVictoryPanel] Translate key lost. key:{_tipsKey}");
            UnlockNavigation();
            return;
        }

        if (tipsTxt == null)
        {
            this.LogWarning("[IWannerGame_GameVictoryPanel] tips_txt reference is missing.");
            UnlockNavigation();
            return;
        }

        tipsTxt.text = translated;
        tipsTxt.UpdateFontAsset();
        tipsTxt.ForceMeshUpdate(true, true);
        tipsTxt.text = string.Empty;

        _tipsTween?.Kill();
        _tipsFallbackTween?.Kill();
        _tipsTween = tipsTxt
            .DOText(translated, TipsTypewriterDuration)
            .SetUpdate(true)
            .OnComplete(UnlockNavigation);
        _tipsFallbackTween = DOVirtual
            .DelayedCall(TipsFallbackDelay, UnlockNavigation)
            .SetUpdate(true);
    }

    private void UnlockNavigation()
    {
        if (_isNavigationReady)
            return;

        _isNavigationReady = true;
        RefreshButtonStates();
    }

    private void RevealNextButton()
    {
        if (nextButtonGroup == null)
            return;

        _nextButtonFadeTween?.Kill();
        _nextButtonFadeTween = nextButtonGroup
            .DOFade(1f, NextButtonFadeDuration)
            .SetDelay(NextButtonFadeDelay)
            .SetUpdate(true)
            .OnComplete(() => _nextButtonFadeTween = null);
    }

    /// <summary>
    /// 用途：统一刷新下一步与 Replay 的交互门禁。
    /// 调用边界：基类生命周期和派生面板局部状态变化后调用。
    /// 注意事项：派生类覆写时必须先调用 base，避免绕过重复导航保护。
    /// </summary>
    protected virtual void RefreshButtonStates()
    {
        bool hasExtraBusyState = HasExtraBusyState();
        bool hasNoAds = HasNoAds();
        bool navigationInteractable =
            _isNavigationReady &&
            !_isNavigationTriggered &&
            !_isNoAdsPurchasing &&
            !hasExtraBusyState;

        if (nextButton != null)
            nextButton.Interactable = navigationInteractable;
        if (replayButton != null)
        {
            replayButton.gameObject.SetActive(!_hideOneLinkReplay);
            replayButton.Interactable = !_hideOneLinkReplay && navigationInteractable;
        }

        if (noAdPart != null)
            noAdPart.SetActive(_noAdButton != null && !hasNoAds);
        if (_noAdButton != null)
        {
            _noAdButton.Interactable =
                !hasNoAds &&
                !_isNoAdsPurchasing &&
                !hasExtraBusyState;
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
        _tipsTween?.Kill();
        _tipsTween = null;
        _tipsFallbackTween?.Kill();
        _tipsFallbackTween = null;
    }

    private void UnbindSpineStateCallbacks()
    {
        Spine.AnimationState state = spineCtrl?.GetAnimationState();
        if (state != null)
            state.Complete -= OnEntranceAnimationComplete;
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

    protected virtual void OnEntranceAnimationCompleteExtra()
    {
    }

    /// <summary>
    /// 用途：当前 Group 分享/评价上下文变化后的派生类刷新入口。
    /// 调用边界：面板工厂注入结算上下文后调用；普通版无需覆写。
    /// </summary>
    protected virtual void OnVictoryContextChanged()
    {
    }

    protected virtual bool HasExtraBusyState()
    {
        return false;
    }

    /// <summary>
    /// 用途：在面板工厂完成 Resources 实例创建后写入当前 Group 的分享、评价与通关文案上下文。
    /// 调用边界：仅供普通版与 Reward 版工厂在分享组显示、入场动画完成前调用。
    /// 注意事项：GroupId 必须使用 IWannerLevelSort.Id；空提示键会安全降级并直接开放导航。
    /// </summary>
    protected void ConfigureVictoryContext(int groupId, string currentLevelPath, string congraktTips)
    {
        _currentGroupId = groupId;
        _currentLevelPath = string.IsNullOrEmpty(currentLevelPath) ? "no_start" : currentLevelPath;
        _tipsKey = congraktTips;
        RefreshStandaloneShareView();
        OnVictoryContextChanged();
    }

    #endregion

    #region Factory

    /// <summary>
    /// 用途：打开 IWanner Group 复通使用的普通胜利面板。
    /// 调用边界：仅由活动 IWannerPlayCtrl 在整个 Group 完成后调用。
    /// 注意事项：资源根节点必须绑定 IWannerGame_GameVictoryPanel，而不是 DrawGame 组件。
    /// </summary>
    /// <param name="groupId">当前完成的 IWanner GroupId。</param>
    /// <param name="currentLevelPath">当前 Group 最后一小关的配置 Path。</param>
    /// <param name="congraktTips">当前 IWanner Group 配置的通关文案本地化键。</param>
    public static IWannerGame_GameVictoryPanel Create(
        int groupId,
        string currentLevelPath,
        string congraktTips)
    {
        if (UIManager.Instance == null)
            return null;

        IWannerGame_GameVictoryPanel panel = UIManager.Instance.OpenUI<IWannerGame_GameVictoryPanel>(
            nameof(IWannerGame_GameVictoryPanel),
            UILayer.Popup);
        if (panel != null)
            panel.ConfigureVictoryContext(groupId, currentLevelPath, congraktTips);

        return panel;
    }

    #endregion
}

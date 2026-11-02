using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Config;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.ConfigModule;
using Game.Events;
using Game.ItemEvent;
using Game.Scripts.Common;
using GameCommon;
using I2.Loc;
using TMPro;
using UnityEngine;
using static Products;

/// <summary>
/// Common victory panel flow shared by both the regular and reward variants.
/// Reward-only behavior lives in <see cref="GameVictoryPanel_Reawrd"/>.
/// </summary>
public partial class GameVictoryPanel : BaseUIView, IEventSender, IDebuger
{
    protected const string NoAdProductName = ProductName.NewnoAds;
    protected const float CloseBoneDelay = 2f;
    private const float NextButtonFadeDuration = 0.2f;
    private const float NextButtonFadeDelay = 0.15f;
    private const float TipsTypewriterDuration = 1f;
    private const float TipsFallbackDelay = 2f;

    [Header("Common")]
    [SerializeField] private UISpineCtrl spineCtrl;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI tips;
    [SerializeField] private CanvasGroup nextBtnGroup;
    [SerializeField] protected CustomButton NextBtn;
    [SerializeField] private CustomButton shareBtn;
    [SerializeField] private TextMeshProUGUI shareTxt;
    [SerializeField] private BreathingScaleHandle breathingScale;
    [SerializeField] protected CustomButton NoAdBtn;
    [SerializeField] protected CommonUIRewardAnimCtrl rewardAnimCtrl;

    [Header("Postcard")]
    [SerializeField] private Postcard_VictoryStarGroupCtrl postcard_starGroup;

    [Header("OnOpen")]
    // 胜利面板每次打开时播放一次；粒子需在 prefab 中关闭循环和 Play On Awake。
    [SerializeField] private List<ParticleSystem> openEffects;

    // 重玩当前关卡
    [SerializeField] private CustomButton resetBtn;

    [SerializeField] private GameObject clickBlock;
    [SerializeField] private PostcardEnter postcardEnterBtn;

    protected LevelData Data;
    protected string TipsKey;

    private bool _isNoAdsPurchasing;
    private bool _isNextButtonReady;
    private bool _isNextLevelTriggered;
    private bool _isPostcardGuideClickBlocked;
    private bool _isPostcardStarAnimationClickBlocked;
    private PostcardGuideFlow _postcardGuideFlow;

    private bool _isReplayCurrentLevel;
    private bool _hideOneLinkReplay;

    private Tween _tipsTween;
    private Tween _tipsFallbackTween;
    private Tween _nextButtonFadeTween;
    private int _currentLevel;

    protected bool IsNoAdsPurchasing => _isNoAdsPurchasing;
    protected bool IsNextLevelTriggered => _isNextLevelTriggered;
    protected bool IsReplayCurrentLevel => _isReplayCurrentLevel;
    public override void OnCreate()
    {
        base.OnCreate();
        BindButtons();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        _hideOneLinkReplay = Game.OneLinkManager.Instance.IsCurrentEntry(Config.Level.GameType.Default);
        _currentLevel = GetCurrentLevel();
        ResetOpenState();
        PlayOpenEffects();
        BindReplayButton();
        OpenAnimation();
        TryToBlockClick();
        OnOpenExtra();

        AudioManager.PlaySound("success");
    }

    private void PlayOpenEffects()
    {
        if (openEffects == null)
            return;

        foreach (ParticleSystem effect in openEffects)
        {
            if (effect == null)
                continue;

            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Play(true);
        }
    }

    public override void OnShow()
    {
        base.OnShow();
        OnShowExtra();

        if (shareTxt != null)
        {
            shareTxt.gameObject.SetActive(!HasSharedCurrentLevel());
            int coins = ConfigManager.RemoteConfig.GetLevelVictoryShareCoinReward();
            shareTxt.text = "+" + coins;
        }

        if (breathingScale)
        {
            breathingScale.SetStartScale(Vector3.one);
            breathingScale.StartScale();
        }
    }

    public override void OnHide()
    {
        OnHideExtra();
        base.OnHide();
    }

    public override void OnClose()
    {
        base.OnClose();
        CancelInvoke();
        StopTransientAnimations();
        UnbindSpineStateCallbacks();
        ResetRuntimeState();
        OnCloseExtra();

        _postcardGuideFlow?.Cancel();
        _postcardGuideFlow = null;
    }

    private void Update()
    {
        UpdateExtra();
    }

    private void BindButtons()
    {
        if (NextBtn != null)
            NextBtn.OnClick += OnNextLevel;

        if (shareBtn != null)
            shareBtn.OnClick += OnClickShare;

        if (NoAdBtn != null)
            NoAdBtn.OnClick += PurchaseNoAd;

        BindButtonsExtra();
    }

    private void BindReplayButton()
    {
        if (resetBtn == null)
            return;

        resetBtn.OnClick -= ReplayCurrentLevel;
        resetBtn.OnClick += ReplayCurrentLevel;
    }

    private void ResetOpenState()
    {
        StopTransientAnimations();
        ResetRuntimeState();
        ResetClickBlockState();
        _isNextButtonReady = false;

        if (tips != null)
            tips.text = string.Empty;

        if (title != null)
            title.color = Color.white;

        if (nextBtnGroup != null)
            nextBtnGroup.alpha = 0f;
        else if (NextBtn != null)
            NextBtn.gameObject.SetActive(true);

        if (shareTxt)
        {
            // Share reward text follows the current remote-config reward amount.
            int coins = ConfigManager.RemoteConfig.GetLevelVictoryShareCoinReward();
            shareTxt.text = "+" + coins;
        }


        RefreshShareButton();
        ResetOpenStateExtra();
        RefreshButtonStates();
    }


    private void ReplayCurrentLevel(CustomButton btn)
    {
        if (_hideOneLinkReplay || _isNextLevelTriggered || IsReplayCurrentLevel)
        {
            this.LogWarning("[GameVictoryPanel] Replay ignored. already triggered.");
            return;
        }

        _isNextLevelTriggered = true;
        _isReplayCurrentLevel = true;

        if (UIManager.Instance == null)
        {
            this.LogError("[GameVictoryPanel] Replay failed. UIManager is null.");
            return;
        }

        UIManager.Instance.DestroyUI(this);
        this.DispatchEvent(Witness<GameReplayEvent>._);
    }

    private void StopTransientAnimations()
    {
        _tipsTween?.Kill();
        _tipsTween = null;

        _tipsFallbackTween?.Kill();
        _tipsFallbackTween = null;

        _nextButtonFadeTween?.Kill();
        _nextButtonFadeTween = null;
    }

    private void ResetRuntimeState()
    {
        _isNoAdsPurchasing = false;
        _isNextLevelTriggered = false;
        _isReplayCurrentLevel = false;
        ResetRuntimeStateExtra();
    }

    private void PurchaseNoAd(CustomButton btn)
    {
        if (NoAdBtn == null)
            return;

        if (_isNoAdsPurchasing || HasExtraBusyState())
        {
            this.LogWarning("[GameVictoryPanel] NoAdBtn ignored. purchase already in progress.");
            return;
        }

        var purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager == null)
        {
            this.LogError("[GameVictoryPanel] NoAdBtn failed. PurchaseManager is null.");
            return;
        }

        if (HasNoAds())
        {
            this.LogWarning("[GameVictoryPanel] NoAdBtn ignored. NoAds already purchased.");
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
            this.LogError("[GameVictoryPanel] NoAdBtn failed. product data is null.");
            purchaseManager.OnPurchaseFailed();
            return;
        }

        _isNoAdsPurchasing = true;
        RefreshButtonStates();
        GameSDKService.Instance?.IAP_CLK(data.Sku);
        purchaseManager.Purchase(NoAdProductName, nameof(GameVictoryPanel), OnPurchaseNoAdCallback);
    }

    private void OnPurchaseNoAdCallback(string productName, bool isSuc)
    {
        _isNoAdsPurchasing = false;

        var purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager == null)
        {
            this.LogError($"[GameVictoryPanel] NoAdBtn callback failed. PurchaseManager is null. productName:{productName}, isSuc:{isSuc}");
            RefreshButtonStates();
            return;
        }

        if (isSuc)
            purchaseManager.OnPurchaseSuccess(productName);
        else
            purchaseManager.OnPurchaseFailed();

        if (this == null || !isActiveAndEnabled)
            return;

        RefreshButtonStates();
    }

    private void OnClickShare(CustomButton btn)
    {
        ShareStoreUrl();

        if (shareTxt != null)
            shareTxt.gameObject.SetActive(false);

        if (HasSharedCurrentLevel())
            return;

        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            this.LogError("[GameVictoryPanel] Share reward grant failed. Bag is null.");
            return;
        }

        if (_currentLevel <= 0)
        {
            this.LogError("[GameVictoryPanel] Share reward grant failed. current level is invalid.");
            return;
        }

        int coins = ConfigManager.RemoteConfig.GetLevelVictoryShareCoinReward();
        bag.AddTool(Config.Game.GameProps.Coin, coins, RewardType.Media);

        MarkCurrentLevelShared(_currentLevel);

        if (rewardAnimCtrl != null)
            rewardAnimCtrl.PlayFrom(shareBtn != null ? shareBtn.transform : transform, 1).Forget();
    }

    protected void ShareStoreUrl()
    {
        string url = "https://link.brainpunk.studio/store/install";
#if UNITY_ANDROID
        // url = "https://play.google.com/store/apps/details?id=com.brain.tricky.puzzle.games.test.quest";
        AndroidShare.ShareText(url.Trim());
#elif UNITY_IOS
        // url = "https://apps.apple.com/us/app/brain-riddle-tricky-box/id6757749388";
        IosShare.ShareText(url.Trim());
#endif
    }

    protected virtual void RefreshButtonStates()
    {
        bool hasNoAds = HasNoAds();

        if (resetBtn != null)
            resetBtn.gameObject.SetActive(!_hideOneLinkReplay);

        if (NextBtn != null)
            NextBtn.Interactable = _isNextButtonReady && !_isNoAdsPurchasing && !HasExtraBusyState();

        if (NoAdBtn != null)
        {
            NoAdBtn.Interactable = !hasNoAds && !_isNoAdsPurchasing && !HasExtraBusyState();
            NoAdBtn.gameObject.SetActive(!hasNoAds);
        }
    }

    private void RefreshShareButton()
    {
        if (shareBtn == null)
            return;

        shareBtn.gameObject.SetActive(true);
    }

    private bool HasSharedCurrentLevel()
    {
        if (_currentLevel <= 0)
            return false;

        List<int> sharedLevels = DB.GameData?.HasSharedLevel;
        return sharedLevels != null && sharedLevels.Contains(_currentLevel);
    }

    private int GetCurrentLevel()
    {
        return PlayCtrl.Instance?.Level != null
            ? PlayCtrl.Instance.Level.CurrentLevel
            : DB.GameData?.CurrentLevel ?? 0;
    }

    private void MarkCurrentLevelShared(int currentLevel)
    {
        var gameData = DB.GameData;
        if (gameData == null)
        {
            this.LogError("[GameVictoryPanel] Share reward state save failed. GameData is null.");
            return;
        }

        if (gameData.HasSharedLevel == null)
            gameData.HasSharedLevel = new List<int>();

        if (gameData.HasSharedLevel.Contains(currentLevel))
            return;

        gameData.HasSharedLevel.Add(currentLevel);
        gameData.Save();
    }

    protected bool HasNoAds()
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

    private void OpenAnimation()
    {
        UnbindSpineStateCallbacks();

        if (spineCtrl == null)
        {
            OnInAnimationComplete(null);
            return;
        }

        var track = spineCtrl.PlayAnimation("in", false);
        if (track == null)
        {
            OnInAnimationComplete(null);
            return;
        }

        var state = spineCtrl.GetAnimationState();
        if (state == null)
        {
            OnInAnimationComplete(null);
            return;
        }

        if (NoAdBtn)
        {
            NoAdBtn.transform.localScale = Vector3.zero;
            NoAdBtn.transform.DOScale(Vector3.one, .3f).SetEase(Ease.OutBack).SetDelay(1.5f).OnComplete(() =>
            {
                if (breathingScale)
                {
                    breathingScale.SetStartScale(Vector3.one);
                    breathingScale.StartScale();
                }
            });
        }

        state.Event += OnSpineEvent;
        state.Complete += OnInAnimationComplete;
    }

    private void UnbindSpineStateCallbacks()
    {
        var state = spineCtrl != null ? spineCtrl.GetAnimationState() : null;
        if (state == null)
            return;

        state.Event -= OnSpineEvent;
        state.Complete -= OnInAnimationComplete;
    }

    private void OnInAnimationComplete(Spine.TrackEntry track)
    {
        RevealNextButton();
        RefreshTips();

        if (spineCtrl == null)
            return;

        spineCtrl.PlayAnimation("idle", true);
        UnbindSpineStateCallbacks();
        OnInAnimationCompleteExtra();

        TryToPostcardGuide().Forget();
    }

    private void TryToBlockClick()
    {
        _isPostcardGuideClickBlocked = PostcardGuideFlow.IsShow(_currentLevel);
        RefreshClickBlock();
    }

    private void ResetClickBlockState()
    {
        _isPostcardGuideClickBlocked = false;
        _isPostcardStarAnimationClickBlocked = false;
        RefreshClickBlock();
    }

    private void SetPostcardStarAnimationClickBlocked(bool isBlocked)
    {
        _isPostcardStarAnimationClickBlocked = isBlocked;
        RefreshClickBlock();
    }

    private void SetPostcardGuideClickBlocked(bool isBlocked)
    {
        _isPostcardGuideClickBlocked = isBlocked;
        RefreshClickBlock();
    }

    private void RefreshClickBlock()
    {
        if (clickBlock == null)
            return;

        bool shouldBlock = _isPostcardGuideClickBlocked || _isPostcardStarAnimationClickBlocked;
        if (clickBlock.activeSelf != shouldBlock)
            clickBlock.SetActive(shouldBlock);
    }

    private async UniTask TryToPostcardGuide()
    {
        /// animation 播放完成的时候弹出 RatingPopupPol
        if (!PostcardGuideFlow.IsShow(_currentLevel))
            return;

        _postcardGuideFlow = new PostcardGuideFlow(
            GameManager.Instance?.Postcard,
            postcardEnterBtn,
            transform as RectTransform,
            SetPostcardGuideClickBlocked);

        GuideFlowResult result = await _postcardGuideFlow.RunAsync();
        if (result == GuideFlowResult.Failed)
            this.LogWarning("[GameVictoryPanel] Postcard guide flow failed.");
    }

    private void RevealNextButton()
    {
        if (nextBtnGroup == null)
        {
            if (NextBtn != null)
                NextBtn.gameObject.SetActive(true);

            return;
        }

        _nextButtonFadeTween?.Kill();
        _nextButtonFadeTween = nextBtnGroup
            .DOFade(1f, NextButtonFadeDuration)
            .SetDelay(NextButtonFadeDelay)
            .SetUpdate(true);
    }

    private void OnSpineEvent(Spine.TrackEntry trackEntry, Spine.Event e)
    {
        if (trackEntry?.Animation == null || e == null)
            return;

        if (!string.Equals(trackEntry.Animation.Name, "in", StringComparison.OrdinalIgnoreCase))
            return;

        if (e.Data.Name == "NextLevel")
        {
            RevealNextButton();
            this.Log("[GameVictoryPanel] Spine event: NextLevel");
        }
    }

    public void RefreshTips()
    {
        string key = TipsKey;
        if (string.IsNullOrEmpty(key))
        {
            this.LogWarning("[GameVictoryPanel] RefreshTips: CongraktTips key is null or empty.");
            UnlockNextButton();
            return;
        }

        string translated = LocalizationManager.GetTranslation(key, false);
        if (string.IsNullOrEmpty(translated))
        {
            this.LogError($"Translate key: {key} lost!");
            UnlockNextButton();
            return;
        }

        if (TipsTxt == null)
        {
            this.LogWarning("[GameVictoryPanel] RefreshTips: TipsTxt is null.");
            UnlockNextButton();
            return;
        }

        TipsTxt.text = translated;
        TipsTxt.UpdateFontAsset();
        TipsTxt.ForceMeshUpdate(true, true);
        TipsTxt.text = string.Empty;

        _tipsTween?.Kill();
        _tipsFallbackTween?.Kill();

        _tipsTween = TipsTxt
            .DOText(translated, TipsTypewriterDuration)
            .SetUpdate(true)
            .OnComplete(UnlockNextButton);

        _tipsFallbackTween = DOVirtual
            .DelayedCall(TipsFallbackDelay, () =>
            {
                if (this == null)
                    return;

                UnlockNextButton();
            })
            .SetUpdate(true);

        this.Log($"[GameVictoryPanel] RefreshTips: key={key}, text=\"{translated}\"");
    }

    private void UnlockNextButton()
    {
        _isNextButtonReady = true;
        RefreshButtonStates();
    }

    protected void OnNextLevel(CustomButton btn)
    {
        if (_isNextLevelTriggered)
        {
            this.LogWarning("[GameVictoryPanel] OnNextLevel ignored. already triggered.");
            return;
        }

        _isNextLevelTriggered = true;

        if (UIManager.Instance == null)
        {
            this.LogError("[GameVictoryPanel] OnNextLevel failed. UIManager is null.");
            return;
        }

        UIManager.Instance.DestroyUI(this);
        this.DispatchEvent(Witness<EnterNextLevelEvent>._);
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

    protected virtual void OnInAnimationCompleteExtra()
    {
    }

    protected virtual bool HasExtraBusyState()
    {
        return false;
    }

    public static GameVictoryPanel Create(LevelData data, bool hasReward = false)
    {
        if (data == null)
        {
            Debug.LogError("[GameVictoryPanel] Create failed. LevelData is null.");
            return null;
        }

        if (UIManager.Instance == null)
        {
            Debug.LogError("[GameVictoryPanel] Create failed. UIManager is null.");
            return null;
        }

        GameVictoryPanel panel;
        if (hasReward)
        {
            panel = UIManager.Instance.OpenUI<GameVictoryPanel_Reawrd>($"{nameof(GameVictoryPanel)}_Reward", UILayer.Popup);
        }
        else
        {
            panel = UIManager.Instance.OpenUI<GameVictoryPanel>(nameof(GameVictoryPanel), UILayer.Popup);
        }

        if (panel == null)
        {
            Debug.LogError($"[GameVictoryPanel] Create failed. OpenUI returned null. hasReward:{hasReward}");
            return null;
        }

        panel.Data = data;
        panel.TipsKey = data.CongraktTips;
        panel.RefreshPostcardVictoryStars(data);
        return panel;
    }

    private void RefreshPostcardVictoryStars(LevelData data)
    {
        if (postcard_starGroup == null)
            postcard_starGroup = GetComponentInChildren<Postcard_VictoryStarGroupCtrl>(true);

        if (postcard_starGroup != null)
        {
            bool isAnimationPlaying = postcard_starGroup.Refresh(
                data,
                () =>
                {
                    SetPostcardStarAnimationClickBlocked(false);
                    TryExpandDailyChallengeBubble();
                });
            SetPostcardStarAnimationClickBlocked(isAnimationPlaying);
            if (!isAnimationPlaying)
                TryExpandDailyChallengeBubble();
            return;
        }

        SetPostcardStarAnimationClickBlocked(false);
        TryExpandDailyChallengeBubble();

        if (data == null || string.IsNullOrEmpty(data.Id))
            return;

        GameManager.Instance?.Postcard?.ConsumeCollectedStarCount(data.Id);
    }

    private void TryExpandDailyChallengeBubble()
    {
        if (!Application.isPlaying || this == null || !isActiveAndEnabled || !IsOpen)
            return;

        DailyChallengeBtn dailyChallengeBtn = GetComponentInChildren<DailyChallengeBtn>(true);
        dailyChallengeBtn?.TryExpandIfIncomplete();
    }
}

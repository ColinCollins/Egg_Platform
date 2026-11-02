using Bear.EventSystem;
using Bear.UI;
using Config.Game;
using DG.Tweening;
using Game.Events;
using Game.Play;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class DrawGame_GamePlayPanel : BaseUIView, IEventSender
{
    #region Constants

    private const float RewardButtonHideDuration = 0.2f;
    private const float CountdownPulseScale = 1.35f;
    private const float CountdownPulseGrowDuration = 0.12f;
    private const float CountdownPulseReturnDuration = 0.18f;

    #endregion

    #region Inspector References

    [Header("画线运行时")]

    /// <summary>
    /// 墨水进度显示控制器；负责填充、星级节点和激励奖励宽度动画。
    /// </summary>
    [SerializeField] private DrawGame_InkProcessHandle inkProcessHandle;

    /// <summary>
    /// 激励广告入口的完整根节点，包含翻倍按钮和视频图标。
    /// 调用边界：缩放隐藏与重新显示必须作用于该节点，按钮交互仍由 UIYouxizhong2X201Btn 控制。
    /// </summary>
    [SerializeField] private RectTransform rewardAdButtonRoot;

    /// <summary>
    /// Draw Reset 使用的全屏黑幕。
    /// 调用边界：必须由 DrawGame_GamePlayPanel prefab 绑定，Reset 期间负责遮挡重载画面和输入。
    /// </summary>
    [SerializeField] private Image blackMask;

    /// <summary>
    /// Draw GameplayPanel 的全屏点击拦截节点。
    /// 调用边界：通过 GamePlayPanelSwitchBlockEvent 成对开启或关闭，与主线 GamePlayPanel 保持一致。
    /// </summary>
    [SerializeField] private GameObject clickBlock;

    /// <summary>
    /// Draw 关卡倒计时文本。
    /// 调用边界：仅响应 DrawGameCountdownChangedEvent；倒计时未进入最后五秒时保持隐藏。
    /// </summary>
    [SerializeField] private TextMeshProUGUI countdownText;

    #endregion

    #region Runtime State

    private EventSubscriber _subscriber;
    private bool _isRewardAdRequesting;

    /// <summary>供当前 Draw 控制器延后云控排序提交，直到本面板的激励回调处理结束。</summary>
    internal bool IsRewardAdRequesting => _isRewardAdRequesting;
    private bool _animateNextInkChange;
    private int _rewardAdRequestVersion;
    private Vector3 _rewardButtonBaseScale = Vector3.one;
    private bool _hasRewardButtonBaseScale;
    private Vector3 _countdownTextBaseScale = Vector3.one;
    private bool _hasCountdownTextBaseScale;

    #endregion

    #region UI Lifecycle

    public override void OnOpen()
    {
        base.OnOpen();
        if (clickBlock != null)
            clickBlock.SetActive(false);
        SetCountdownVisible(false, 0);
        _rewardAdRequestVersion++;
        _isRewardAdRequesting = false;
        _animateNextInkChange = false;
        ShowRewardButtonImmediately();
        SetRewardButtonInteractable(true);
        inkProcessHandle?.ResetPresentation();
        AddListener();
    }

    public override void OnCreate()
    {
        base.OnCreate();
        if (PauseBtn != null)
            PauseBtn.OnClick += OnClickPause;
        if (PencilShopBtn != null)
            PencilShopBtn.OnClick += OnClickPencilShop;
        if (ResetBtn != null)
            ResetBtn.OnClick += OnClickReset;
        if (ShopBtn != null)
            ShopBtn.OnClick += OnClickShop;
        if (TipsBtn != null)
            TipsBtn.OnClick += OnClickTips;
        if (UIYouxizhong2X201Btn != null)
            UIYouxizhong2X201Btn.OnClick += OnClickRewardAd;
    }

    public override void OnClose()
    {
        _rewardAdRequestVersion++;
        EventsUtils.ResetEvents(ref _subscriber);
        SetCountdownVisible(false, 0);
        blackMask?.DOKill();
        GetRewardButtonRootTransform()?.DOKill();
        inkProcessHandle?.Release();
        base.OnClose();
    }

    #endregion

    #region Events

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<DrawInkStateChangedEvent>(OnDrawInkStateChanged);
        _subscriber.Subscribe<DrawGameCountdownChangedEvent>(OnDrawGameCountdownChanged);
        _subscriber.Subscribe<GamePlayPanelFadeInEvent>(OnGamePlayFadeIn);
        _subscriber.Subscribe<GamePlayPanelSwitchBlockEvent>(OnSwitchBlock);
    }

    /// <summary>
    /// 开始已被 DrawPlayCtrl 接受的 Reset/Replay 黑幕过渡。
    /// 调用边界：仅由当前活动 DrawPlayCtrl 在通过运行时门禁后调用，HUD 不再直接响应导航事件。
    /// 注意事项：后续淡出仍由新 HUD 接收 GamePlayPanelFadeInEvent 完成。
    /// </summary>
    internal void BeginResetTransition()
    {
        _rewardAdRequestVersion++;
        _isRewardAdRequesting = false;
        SetRewardButtonInteractable(false);
        PlayResetAnim();
    }

    private void OnDrawInkStateChanged(DrawInkStateChangedEvent evt)
    {
        bool animateReward = _animateNextInkChange;
        _animateNextInkChange = false;
        inkProcessHandle?.SetInkState(evt.InkState, animateReward);
        if (evt.InkState.UsedInk > 0f)
            HideRewardButtonImmediately();
    }

    private void OnDrawGameCountdownChanged(DrawGameCountdownChangedEvent evt)
    {
        SetCountdownVisible(evt.IsVisible, evt.Seconds);
    }

    private void OnSwitchBlock(GamePlayPanelSwitchBlockEvent evt)
    {
        if (clickBlock != null)
            clickBlock.SetActive(evt.IsShow);
    }

    private void PlayResetAnim()
    {
        if (blackMask == null)
            return;

        blackMask.DOKill();
        blackMask.gameObject.SetActive(true);
        blackMask.color = new Color(0f, 0f, 0f, 0f);
        blackMask.DOFade(1f, 0.4f);
    }

    private void OnGamePlayFadeIn(GamePlayPanelFadeInEvent evt)
    {
        if (blackMask == null)
            return;

        ShowBlackMaskImmediately();
        blackMask.DOFade(0f, 0.4f).SetUpdate(true).OnComplete(() =>
        {
            if (blackMask != null)
                blackMask.gameObject.SetActive(false);
        });
    }

    #endregion

    #region Button Handlers

    private void OnClickPause(CustomButton btn)
    {
        IPlayPauseHost pauseHost = GameManager.Instance?.ControllerManager?.ActivePauseHost;
        if (pauseHost == null)
            return;

        pauseHost.Pause();
        GameSettingPopup.Create(true, pauseHost);
    }

    private void OnClickReset(CustomButton btn)
    {
        this.DispatchEvent(Witness<DrawGameResetEvent>._, GameResetType.Manually);
    }

    private void OnClickPencilShop(CustomButton btn)
    {
        PencilSkinPopup.Create();
    }

    private void OnClickShop(CustomButton btn)
    {
        IPlayPauseHost pauseHost = GameManager.Instance?.ControllerManager?.ActivePauseHost;
        if (pauseHost == null)
            return;

        pauseHost.Pause();
        ShopPanel shopPanel = ShopPanel.Create(true, GetType().Name, pauseHost);
        if (shopPanel == null)
            pauseHost.Resume();
    }

    private void OnClickTips(CustomButton btn)
    {
        this.DispatchEvent(Witness<GamePlayPanelSwitchTipsEvent>._, false);
        DrawGame_GameTipsPopup.Create();
    }

    private void OnClickRewardAd(CustomButton btn)
    {
        DrawPlayCtrl drawPlayCtrl =
            GameManager.Instance?.ControllerManager?.ActiveController as DrawPlayCtrl;
        if (_isRewardAdRequesting ||
            drawPlayCtrl == null ||
            drawPlayCtrl.IsRewardInkGranted)
        {
            return;
        }

        _isRewardAdRequesting = true;
        SetRewardButtonInteractable(false);
        int requestVersion = ++_rewardAdRequestVersion;
        RewardAdHelper.TryToShowRewardAd(
            RewardPlacement.InGameTips.ToString(),
            onResult: (placement, isSuccess) =>
                OnRewardAdResult(requestVersion, placement, isSuccess));
    }

    private void OnRewardAdResult(
        int requestVersion,
        string placement,
        bool isSuccess)
    {
        if (this == null ||
            !isActiveAndEnabled ||
            requestVersion != _rewardAdRequestVersion)
        {
            return;
        }

        _isRewardAdRequesting = false;
        if (!isSuccess)
        {
            DrawPlayCtrl activeDrawPlayCtrl =
                GameManager.Instance?.ControllerManager?.ActiveController as DrawPlayCtrl;
            if (activeDrawPlayCtrl?.IsRewardInkGranted == true)
                SetRewardInkGranted(true);
            else
                SetRewardButtonInteractable(true);
            return;
        }

        DrawPlayCtrl drawPlayCtrl =
            GameManager.Instance?.ControllerManager?.ActiveController as DrawPlayCtrl;
        _animateNextInkChange = true;
        if (drawPlayCtrl == null ||
            !drawPlayCtrl.TryGrantRewardInk(out DrawInkState rewardInkState))
        {
            _animateNextInkChange = false;
            if (drawPlayCtrl?.IsRewardInkGranted == true)
                SetRewardInkGranted(true);
            else
                SetRewardButtonInteractable(true);
            return;
        }

        if (_animateNextInkChange)
        {
            _animateNextInkChange = false;
            inkProcessHandle?.SetInkState(rewardInkState, true);
        }

        SetRewardButtonInteractable(false);
        HideRewardButtonAnimated();
    }

    #endregion

    #region Draw Runtime Binding

    /// <summary>
    /// 刷新当前画线关卡显示编号。
    /// 调用边界：仅由 DrawPlayCtrl 在 HUD 创建成功后调用。
    /// </summary>
    internal void SetLevel(int level)
    {
        if (LevelTxt != null)
            LevelTxt.text = level.ToString();
    }

    /// <summary>
    /// 设置当前 Draw 关卡是否展示 Tips 按钮。
    /// 调用边界：仅由 DrawPlayCtrl 根据当前关卡资源身份在 HUD 创建后调用；隐藏按钮时其 warning 子节点也不可见。
    /// </summary>
    internal void SetTipsButtonVisible(bool isVisible)
    {
        if (TipsBtn != null)
            TipsBtn.gameObject.SetActive(isVisible);
    }

    /// <summary>
    /// 同步当前 Draw 关卡进入周期内的奖励墨水领取状态。
    /// 调用边界：由 DrawPlayCtrl 在 HUD 创建后调用；广告回调只在确认已领取时复用隐藏分支。
    /// 注意事项：true 时立即隐藏入口；false 时恢复入口，供进入新关或返回菜单后的新周期重新领取。
    /// </summary>
    internal void SetRewardInkGranted(bool isGranted)
    {
        inkProcessHandle?.SetRewardPresentationExpanded(isGranted);
        if (isGranted)
        {
            SetRewardButtonInteractable(false);
            HideRewardButtonImmediately();
            return;
        }

        ShowRewardButtonImmediately();
        SetRewardButtonInteractable(true);
    }

    /// <summary>
    /// 根据当前墨水快照刷新 HUD 进度条和星级达标状态。
    /// 注意事项：激励视频奖励快照会同时播放宽度扩展和 DOFillAmount 动画。
    /// </summary>
    internal void SetInkState(DrawInkState inkState)
    {
        inkProcessHandle?.ConfigureStages(inkState);
        inkProcessHandle?.SetInkState(inkState, false);
    }

    /// <summary>
    /// 立即显示全黑遮罩，为旧 HUD 销毁到新 HUD 完成加载之间提供连续遮挡。
    /// 调用边界：仅由 DrawPlayCtrl 在 Reset/Replay 创建全新 HUD 后、首次异步加载前调用。
    /// 注意事项：本方法只接管新 HUD 的过渡表现，不恢复旧 HUD 的任何运行时状态。
    /// </summary>
    internal void ShowBlackMaskImmediately()
    {
        if (blackMask == null)
            return;

        blackMask.DOKill();
        blackMask.gameObject.SetActive(true);
        blackMask.color = new Color(0f, 0f, 0f, 1f);
    }

    private void SetCountdownVisible(bool visible, int seconds)
    {
        if (countdownText == null)
            return;

        Transform countdownTransform = countdownText.transform;
        if (!_hasCountdownTextBaseScale)
        {
            _countdownTextBaseScale = countdownTransform.localScale;
            _hasCountdownTextBaseScale = true;
        }

        countdownTransform.DOKill();
        countdownTransform.localScale = _countdownTextBaseScale;
        countdownText.gameObject.SetActive(visible);
        if (!visible)
            return;

        countdownText.text = Mathf.Max(0, seconds).ToString();
        DOTween.Sequence()
            .SetTarget(countdownTransform)
            .SetUpdate(true)
            .SetLink(countdownText.gameObject)
            .Append(countdownTransform
                .DOScale(_countdownTextBaseScale * CountdownPulseScale, CountdownPulseGrowDuration)
                .SetEase(Ease.OutQuad))
            .Append(countdownTransform
                .DOScale(_countdownTextBaseScale, CountdownPulseReturnDuration)
                .SetEase(Ease.OutBack));
    }

    private void SetRewardButtonInteractable(bool interactable)
    {
        if (UIYouxizhong2X201Btn != null)
            UIYouxizhong2X201Btn.Interactable = interactable;
    }

    private void ShowRewardButtonImmediately()
    {
        Transform buttonRootTransform = GetRewardButtonRootTransform();
        if (buttonRootTransform == null)
            return;

        if (!_hasRewardButtonBaseScale)
        {
            _rewardButtonBaseScale = buttonRootTransform.localScale;
            _hasRewardButtonBaseScale = true;
        }

        buttonRootTransform.DOKill();
        buttonRootTransform.localScale = _rewardButtonBaseScale;
        buttonRootTransform.gameObject.SetActive(true);
    }

    private void HideRewardButtonAnimated()
    {
        Transform buttonRootTransform = GetRewardButtonRootTransform();
        if (buttonRootTransform == null ||
            !buttonRootTransform.gameObject.activeSelf)
        {
            return;
        }

        SetRewardButtonInteractable(false);
        buttonRootTransform.DOKill();
        buttonRootTransform
            .DOScale(Vector3.zero, RewardButtonHideDuration)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                if (buttonRootTransform != null)
                    buttonRootTransform.gameObject.SetActive(false);
            });
    }

    private void HideRewardButtonImmediately()
    {
        Transform buttonRootTransform = GetRewardButtonRootTransform();
        if (buttonRootTransform == null ||
            !buttonRootTransform.gameObject.activeSelf)
        {
            return;
        }

        SetRewardButtonInteractable(false);
        buttonRootTransform.DOKill();
        buttonRootTransform.gameObject.SetActive(false);
    }

    private Transform GetRewardButtonRootTransform()
    {
        if (rewardAdButtonRoot != null)
            return rewardAdButtonRoot;

        return UIYouxizhong2X201Btn != null
            ? UIYouxizhong2X201Btn.transform
            : null;
    }

    #endregion

    #region Factory

    public static DrawGame_GamePlayPanel Create()
    {
        var panel = UIManager.Instance.OpenUI<DrawGame_GamePlayPanel>($"{typeof(DrawGame_GamePlayPanel).Name}", UILayer.Normal);
        return panel;
    }

    #endregion
}

using System;
using System.Threading;
using Bear.EventSystem;
using Bear.UI;
using Config.Game;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Play;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>复制 Draw HUD 的独立 Dig 面板；只接当前 Dig 控制器，不订阅主线/Draw 的玩法事件。</summary>
public sealed partial class DigGame_GamePlayPanel : BaseUIView, IEventSender
{
    #region 倒计时表现常量
    private const float CountdownPulseScale = 1.35f;
    private const float CountdownPulseGrowDuration = 0.12f;
    private const float CountdownPulseReturnDuration = 0.18f;
    #endregion

    #region Prefab 显式绑定
    /// <summary>复用 Draw 的纯墨水/星级显示组件；输入为 Dig 世界长度快照，不负责广告或存档。</summary>
    [SerializeField] private DrawGame_InkProcessHandle inkProcessHandle;
    /// <summary>完整广告按钮根，领取或耗墨后一起隐藏视频图标与按钮。</summary>
    [SerializeField] private RectTransform rewardAdButtonRoot;
    /// <summary>本关重建黑幕；不响应其它玩法的遮罩事件。</summary>
    [SerializeField] private Image blackMask;
    /// <summary>HUD 点击遮罩，加载、暂停、结算及广告期间由本面板持有。</summary>
    [SerializeField] private GameObject clickBlock;
    /// <summary>末关隐藏的墨水条根；显式绑定副本 process 节点。</summary>
    [SerializeField] private GameObject inkRoot;
    /// <summary>本面板已存在的cd_txt文本；Prefab显式绑定，只显示当前Dig关卡的最后五秒。</summary>
    [SerializeField] private TextMeshProUGUI countdownText;
    /// <summary>同关重建的淡入/淡出时长，单位秒。</summary>
    [SerializeField, Min(0f)] private float maskDuration = 0.2f;
    #endregion

    #region 生命周期
    private DigPlayCtrl _controller;
    private Func<bool> _canInteract;
    private bool _interactionEnabled, _requestingReward, _hasHint, _hasInk;
    private int _requestVersion;
    private int _rewardLevelVersion;
    private Tween _maskTween;
    private CancellationTokenSource _openCts;
    private Vector3 _countdownTextBaseScale = Vector3.one;
    private bool _hasCountdownTextBaseScale;
    private int _displayedCountdownSeconds;

    /// <summary>广告会话尚未结束时阻止 Dig 云控提交。</summary>
    internal bool IsRewardAdRequesting => _requestingReward;

    /// <inheritdoc />
    public override void OnCreate()
    {
        base.OnCreate();
        ResetBtn.OnClick += OnDigReset;
        PauseBtn.OnClick += OnDigPause;
        TipsBtn.OnClick += OnDigTips;
        ShopBtn.OnClick += OnShop;
        UIYouxizhong2X201Btn.OnClick += OnReward;
    }

    /// <inheritdoc />
    public override void OnOpen()
    {
        base.OnOpen();
        _openCts = new CancellationTokenSource();
        _requestVersion++;
        _requestingReward = false;
        _canInteract = null;
        _controller = null;
        inkProcessHandle.ResetPresentation();
        SetDigInteraction(false);
        SetDigBlackMask(false);
        SetCountdownSeconds(0);
    }

    /// <inheritdoc />
    public override void OnClose()
    {
        StopOpenSession();
        _canInteract = null;
        _controller = null;
        SetDigInteraction(false);
        base.OnClose();
    }

    private void StopOpenSession()
    {
        SetCountdownSeconds(0);
        _requestVersion++;
        if (_requestingReward && _controller?.LevelVersion == _rewardLevelVersion) _controller.Resume();
        _requestingReward = false;
        _openCts?.Cancel();
        _openCts?.Dispose();
        _openCts = null;
        _maskTween?.Kill();
        rewardAdButtonRoot.DOKill();
        inkProcessHandle.Release();
    }

    private void OnDestroy()
    {
        StopOpenSession();
        ResetBtn.OnClick -= OnDigReset;
        PauseBtn.OnClick -= OnDigPause;
        TipsBtn.OnClick -= OnDigTips;
        ShopBtn.OnClick -= OnShop;
        UIYouxizhong2X201Btn.OnClick -= OnReward;
    }
    #endregion

    #region 运行时绑定与操作
    /// <summary>控制器创建后注入关卡号和实时门禁；基础绑定由同层级 Draw Generated 副本提供。</summary>
    public void ConfigureForDig(int displayLevel, Func<bool> canInteract)
    {
        _controller = GameManager.Instance.ControllerManager.ActiveController as DigPlayCtrl;
        _canInteract = canInteract;
        LevelTxt.text = displayLevel.ToString();
        SetDigInteraction(false);
    }

    /// <summary>新运行时初始化后恢复本关墨水、广告资格和提示入口；TBC 隐藏墨水与提示。</summary>
    internal void BindRuntime(DrawInkState state, bool rewarded, bool hasHint, bool hasInk)
    {
        SetCountdownSeconds(0);
        _hasHint = hasHint;
        _hasInk = hasInk;
        TipsBtn.gameObject.SetActive(hasHint);
        inkRoot.SetActive(hasInk);
        inkProcessHandle.ConfigureStages(state);
        inkProcessHandle.SetRewardPresentationExpanded(rewarded);
        SetInkState(state);
        rewardAdButtonRoot.gameObject.SetActive(hasInk && !rewarded && state.UsedInk <= 0f);
    }

    /// <summary>关卡耗墨时直接刷新 HUD；奖励宽度动画由有效广告回调单独触发。</summary>
    internal void SetInkState(DrawInkState state)
    {
        inkProcessHandle.SetInkState(state, false);
        if (state.UsedInk > 0f) rewardAdButtonRoot.gameObject.SetActive(false);
    }

    /// <summary>控制器转发本关整数快照；0或关闭时隐藏，数字变化使用Draw同款脉冲，不重复启动相同数字动画。</summary>
    internal void SetCountdownSeconds(int seconds)
    {
        if (!countdownText) return;
        int next = IsOpen ? Mathf.Clamp(seconds, 0, 5) : 0;
        if (next > 0 && next == _displayedCountdownSeconds) return;
        _displayedCountdownSeconds = next;
        Transform textTransform = countdownText.transform;
        if (!_hasCountdownTextBaseScale)
        {
            _countdownTextBaseScale = textTransform.localScale;
            _hasCountdownTextBaseScale = true;
        }
        textTransform.DOKill();
        textTransform.localScale = _countdownTextBaseScale;
        countdownText.gameObject.SetActive(next > 0);
        if (next == 0) return;
        countdownText.text = next.ToString();
        DOTween.Sequence()
            .SetTarget(textTransform)
            .SetUpdate(true)
            .SetLink(countdownText.gameObject)
            .Append(textTransform.DOScale(_countdownTextBaseScale * CountdownPulseScale, CountdownPulseGrowDuration).SetEase(Ease.OutQuad))
            .Append(textTransform.DOScale(_countdownTextBaseScale, CountdownPulseReturnDuration).SetEase(Ease.OutBack));
    }

    /// <summary>控制器每帧同步可操作状态；点击时再次检查物理结果门禁。</summary>
    public void SetDigInteraction(bool enabled)
    {
        _interactionEnabled = IsOpen && enabled && !_requestingReward;
        clickBlock.SetActive(!_interactionEnabled);
        ResetBtn.Interactable = PauseBtn.Interactable = ShopBtn.Interactable = _interactionEnabled;
        TipsBtn.Interactable = _interactionEnabled && _hasHint;
        UIYouxizhong2X201Btn.Interactable = _interactionEnabled && _hasInk && _controller?.IsRewardInkGranted == false;
    }

    private bool CanOperate() => IsOpen && _interactionEnabled && _canInteract?.Invoke() == true;
    private void OnDigReset(CustomButton button)
    {
        if (!CanOperate()) return;
        SetDigInteraction(false);
        this.DispatchEvent(Witness<DigGameResetEvent>._, GameResetType.Manually);
    }
    private void OnDigPause(CustomButton button)
    {
        if (!CanOperate()) return;
        _controller.Pause();
        if (GameSettingPopup.Create(true, _controller) == null) _controller.Resume();
    }
    private void OnDigTips(CustomButton button)
    {
        if (CanOperate())
        {
            this.DispatchEvent(Witness<GamePlayPanelSwitchTipsEvent>._, false);
            DigGame_GameTipsPopup.Create();
        }
    }
    private void OnShop(CustomButton button)
    {
        if (!CanOperate()) return;
        _controller.Pause();
        if (ShopPanel.Create(true, GetType().Name, _controller) == null) _controller.Resume();
    }
    #endregion

    #region 广告暂停与会话回调
    private void OnReward(CustomButton button)
    {
        if (!CanOperate() || !_controller.TryBeginRewardInk()) return;
        _requestingReward = true;
        SetDigInteraction(false);
        DigPlayCtrl owner = _controller;
        int levelVersion = owner.LevelVersion;
        _rewardLevelVersion = levelVersion;
        int requestVersion = ++_requestVersion;
        try
        {
            RewardAdHelper.TryToShowRewardAd(RewardPlacement.InGameTips.ToString(), onResult: (_, success) =>
            {
                if (this == null || !IsOpen || requestVersion != _requestVersion || owner != _controller ||
                    GameManager.Instance.ControllerManager.ActiveController != owner || owner.LevelVersion != levelVersion) return;
                _requestingReward = false;
                if (success && owner.TryGrantRewardInk())
                {
                    inkProcessHandle.SetInkState(owner.CurrentInkState, true);
                    rewardAdButtonRoot.gameObject.SetActive(false);
                }
                owner.Resume();
            });
        }
        catch
        {
            _requestingReward = false;
            owner.Resume();
            throw;
        }
    }

    #endregion

    #region 黑幕重建
    /// <summary>加载前立即遮黑；只操作本次 Dig HUD 的序列化图片。</summary>
    public void SetDigBlackMask(bool opaque)
    {
        _maskTween?.Kill();
        blackMask.color = new Color(0, 0, 0, opaque ? 1 : 0);
        blackMask.gameObject.SetActive(opaque);
    }

    /// <summary>同关重建的遮黑/揭幕；关闭和销毁取消旧等待，防止旧会话隐藏新黑幕。</summary>
    public async UniTask FadeDigMaskAsync(bool opaque)
    {
        if (!IsOpen || _openCts == null) throw new OperationCanceledException();
        CancellationToken token = _openCts.Token;
        _maskTween?.Kill();
        blackMask.gameObject.SetActive(true);
        _maskTween = blackMask.DOFade(opaque ? 1 : 0, maskDuration).SetUpdate(true);
        await UniTask.Delay(TimeSpan.FromSeconds(maskDuration), ignoreTimeScale: true, cancellationToken: token);
        token.ThrowIfCancellationRequested();
        if (!opaque) blackMask.gameObject.SetActive(false);
    }

    /// <summary>仅由 DigPlayCtrl 打开独立 Resources HUD；保持与 Draw 相同 Normal 层资源入口。</summary>
    public static DigGame_GamePlayPanel Create(string panelName = "DigGame_GamePlayPanel")
        => UIManager.Instance.OpenUI<DigGame_GamePlayPanel>(panelName, UILayer.Normal);
    #endregion
}

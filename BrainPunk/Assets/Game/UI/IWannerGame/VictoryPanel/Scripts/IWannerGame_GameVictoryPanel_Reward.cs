using System.Threading;
using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using GameCommon;
using Spine.Unity;
using TMPro;
using UnityEngine;

/// <summary>
/// IWanner Group 首次通关使用的奖励版胜利面板。
/// 导航与重玩沿用 IWanner 胜利面板，奖励广告、金币发放和按钮表现与 DrawGame 奖励面板一致。
/// </summary>
public sealed class IWannerGame_GameVictoryPanel_Reward : IWannerGame_GameVictoryPanel, IDebuger
{
    #region Constants

    private const float CloseBoneDelay = 2f;
    private const float BtnGroupShowDuration = 0.35f;
    private const float BtnGroupEnterOffsetY = 160f;

    #endregion

    #region Inspector

    /// <summary>
    /// 用途：发起首次通关的金币奖励广告。
    /// 调用边界：仅在奖励未领取且广告/领奖流程空闲时可交互。
    /// 注意事项：成功结果只允许消费一次，失败或取消不得发放金币。
    /// </summary>
    [SerializeField] private CustomButton AdRewardBtn;

    /// <summary>
    /// 用途：保留 DrawGame 奖励 prefab 的广告后下一步节点。
    /// 调用边界：当前与 DrawGame 一致，仅负责打开状态复位并保持隐藏。
    /// </summary>
    [SerializeField] private CustomButton AfterAdNextBtn;

    /// <summary>
    /// 用途：在短暂入场后停止指针骨骼持续跟随。
    /// 调用边界：仅控制当前 Reward prefab 的局部展示。
    /// </summary>
    [SerializeField] private BoneFollowerGraphic arrowBone;

    /// <summary>
    /// 用途：驱动奖励数值随机展示并提供最终金币数量。
    /// 调用边界：必须由 Reward prefab 绑定，成功广告回调后读取最终值。
    /// </summary>
    [SerializeField] private HorizontalRandomAdRewardCtrl adRewardCtrl;

    /// <summary>
    /// 用途：实时显示奖励转盘当前指向的金币数量。
    /// 调用边界：只展示当前值，实际发放仍以成功广告回调后的最终值为准。
    /// </summary>
    [SerializeField] private TextMeshProUGUI rewardTxt;

    /// <summary>
    /// 用途：金币收集动画完成后刷新顶部道具显示。
    /// 调用边界：只刷新显示，金币写入由 Bag 统一处理。
    /// </summary>
    [SerializeField] private PropBlock propBlock;

    /// <summary>
    /// 用途：播放金币飞入背包的既有通用动画。
    /// 调用边界：金币成功写入 Bag 后播放；动画缺失不回滚已经到账的奖励。
    /// </summary>
    [SerializeField] private CommonUIRewardAnimCtrl rewardAnimCtrl;

    /// <summary>
    /// 用途：兼容复制资源中的引导点击遮罩。
    /// 调用边界：IWanner Victory 打开时强制关闭，避免阻塞按钮。
    /// </summary>
    [SerializeField] private GameObject clickBlock;

    /// <summary>
    /// 用途：承载 Reward 版按钮组的局部入场动画。
    /// 调用边界：必须绑定同时包含 next/replay 的 BtnGroups CanvasGroup。
    /// </summary>
    [SerializeField] private CanvasGroup btnGroup;

    /// <summary>
    /// 用途：奖励领取成功后置灰广告按钮。
    /// 调用边界：仅负责视觉表现，重复领奖由运行时状态门禁阻止。
    /// </summary>
    [SerializeField] private CustomGrayRender grayRender;

    /// <summary>
    /// 用途：控制 Reward 版的喜欢、不喜欢与分享按钮组。
    /// 调用边界：打开时注入当前 IWanner Group 上下文，并在入场延迟后调用 Show。
    /// 注意事项：组件必须使用 IWanner 存档上下文，禁止回退读取主线或 Draw 当前关卡。
    /// </summary>
    [SerializeField] private GameVictoryPanel_Share shareFeedbackPart;

    #endregion

    #region Runtime State

    private RectTransform _btnGroupRect;
    private Vector2 _btnGroupTargetPosition;
    private bool _hasBtnGroupTargetPosition;
    private Tween _btnGroupTween;
    private bool _isRewardAdRequesting;
    private bool _isAdRewardPlaying;
    private bool _isRewardSelectorActive;
    private bool _resumeRewardSelectOnEnable;
    private bool _isAdRewardComplete;
    private bool _hasHidden;
    private CancellationTokenSource _rewardFlowCts;

    #endregion

    #region UI Lifecycle

    public override void OnCreate()
    {
        base.OnCreate();

        if (AdRewardBtn != null)
        {
            AdRewardBtn.OnClick -= OnClickAdReward;
            AdRewardBtn.OnClick += OnClickAdReward;
        }
    }

    #endregion

    #region Victory Hooks

    /// <summary>用途：指定首次通关 Reward prefab 的 Spine 入场动画。</summary>
    protected override string EntranceAnimationName => "in";

    /// <summary>用途：指定首次通关 Reward prefab 的 Spine 待机动画。</summary>
    protected override string IdleAnimationName => "idle";

    /// <summary>用途：由 Reward 版整组动画接管 next/replay 区域入场，避免两条 Tween 同时写同一 CanvasGroup。</summary>
    protected override bool RevealNavigationWithBase => false;

    /// <summary>用途：Reward 版由 GameVictoryPanel_Share 接管分享按钮和一次性奖励。</summary>
    protected override bool UsesStandaloneShareButton => false;

    protected override void OnOpenExtra()
    {
        if (clickBlock != null)
            clickBlock.SetActive(false);

        RefreshShareContext();

        CancelInvoke(nameof(CloseBone));
        CancelInvoke(nameof(ShowSharePart));
        Invoke(nameof(CloseBone), CloseBoneDelay);
        Invoke(nameof(ShowSharePart), CloseBoneDelay);
    }

    protected override void OnShowExtra()
    {
        TryResumeRewardSelect();
        if (!_hasHidden)
            return;

        ShowSharePart();
        ShowBtnGroup();
        _hasHidden = false;
    }

    protected override void OnHideExtra()
    {
        _hasHidden = true;
        PrepareRewardSelectResume();
        KillBtnGroupTween();
    }

    protected override void OnCloseExtra()
    {
        CancelInvoke(nameof(CloseBone));
        CancelInvoke(nameof(ShowSharePart));
        _rewardFlowCts?.Cancel();
        _rewardFlowCts?.Dispose();
        _rewardFlowCts = null;
        KillBtnGroupTween();
        _hasHidden = false;
    }

    protected override void UpdateExtra()
    {
        RefreshRewardPreviewText();
    }

    protected override void ResetOpenStateExtra()
    {
        _isRewardAdRequesting = false;
        _isAdRewardPlaying = false;
        _isRewardSelectorActive = false;
        _resumeRewardSelectOnEnable = false;
        _isAdRewardComplete = false;

        if (arrowBone != null)
            arrowBone.enabled = true;

        if (AdRewardBtn != null)
            AdRewardBtn.gameObject.SetActive(true);

        if (AfterAdNextBtn != null)
            AfterAdNextBtn.gameObject.SetActive(false);

        grayRender?.SetGray(false);
        ResetBtnGroupView();
        RefreshRewardPreviewText();
    }

    protected override void OnEntranceAnimationCompleteExtra()
    {
        ShowBtnGroup();
        PlayRewardSelect();
    }

    protected override void OnVictoryContextChanged()
    {
        RefreshShareContext();
    }

    protected override bool HasExtraBusyState()
    {
        return _isRewardAdRequesting || _isAdRewardPlaying;
    }

    protected override void RefreshButtonStates()
    {
        base.RefreshButtonStates();

        bool isRewardSelecting = adRewardCtrl != null && adRewardCtrl.IsPlaying;
        if (AdRewardBtn != null)
        {
            AdRewardBtn.Interactable =
                IsEntranceAnimationComplete &&
                !_isAdRewardComplete &&
                !IsNoAdsPurchasing &&
                !_isRewardAdRequesting &&
                (!_isAdRewardPlaying || isRewardSelecting);
        }
    }

    #endregion

    #region Reward Flow

    private void PrepareRewardSelectResume()
    {
        if (!IsOpen ||
            adRewardCtrl == null ||
            !_isRewardSelectorActive ||
            _isRewardAdRequesting ||
            _isAdRewardPlaying)
        {
            return;
        }

        _resumeRewardSelectOnEnable = true;
        adRewardCtrl.PreserveCurrentStateOnNextEnable();
    }

    private void TryResumeRewardSelect()
    {
        if (!_resumeRewardSelectOnEnable ||
            !IsOpen ||
            adRewardCtrl == null ||
            _isRewardAdRequesting ||
            _isAdRewardPlaying)
        {
            return;
        }

        _resumeRewardSelectOnEnable = false;
        PlayRewardSelect();
    }

    private void OnClickAdReward(CustomButton _)
    {
        if (_isAdRewardComplete)
        {
            this.LogWarning(
                "[IWannerGame_GameVictoryPanel_Reward] Reward button ignored. reward completed.");
            RefreshButtonStates();
            return;
        }

        if (_isRewardAdRequesting || _isAdRewardPlaying)
            return;

        _isRewardAdRequesting = true;
        RefreshButtonStates();

        if (adRewardCtrl != null)
            adRewardCtrl.Stop(snapToRewardBand: false).Forget();

        RewardAdHelper.TryToShowRewardAd(
            Config.Game.RewardPlacement.GameVictory.ToString(),
            success: "U_HurryTips_RewardVideo_succeed",
            failed: "U_HurryTips_RewardVideo_failed",
            onResult: OnRewardAdCallback);
    }

    private void OnRewardAdCallback(string _, bool isSuccess)
    {
        _isRewardAdRequesting = false;
        if (this == null || !isActiveAndEnabled)
            return;

        if (!isSuccess)
        {
            PlayRewardSelect();
            RefreshButtonStates();
            return;
        }

        _isAdRewardComplete = true;
        grayRender?.SetGray(true);
        PlayRewardFlowAsync().Forget();
        RefreshButtonStates();
    }

    private async UniTask PlayRewardFlowAsync()
    {
        if (_isAdRewardPlaying)
            return;

        _rewardFlowCts?.Cancel();
        _rewardFlowCts?.Dispose();
        _rewardFlowCts = new CancellationTokenSource();
        CancellationToken token = _rewardFlowCts.Token;

        _isAdRewardPlaying = true;
        _isRewardSelectorActive = false;
        RefreshButtonStates();

        try
        {
            if (adRewardCtrl == null)
            {
                this.LogError(
                    "[IWannerGame_GameVictoryPanel_Reward] Reward flow aborted. selector is null.");
                return;
            }

            await adRewardCtrl.Stop(snapToRewardBand: false);
            if (this == null || !isActiveAndEnabled || token.IsCancellationRequested)
                return;

            GrantRewardCoins();
            await PlayRewardCollectAsync(token);
        }
        finally
        {
            _isRewardAdRequesting = false;
            _isAdRewardPlaying = false;
            if (this != null && isActiveAndEnabled)
                RefreshButtonStates();
        }
    }

    private void PlayRewardSelect()
    {
        if (adRewardCtrl == null)
        {
            this.LogWarning(
                "[IWannerGame_GameVictoryPanel_Reward] Reward selector is missing.");
            return;
        }

        _isRewardSelectorActive = true;
        adRewardCtrl.Play().Forget();
        RefreshRewardPreviewText();
        RefreshButtonStates();
    }

    private async UniTask PlayRewardCollectAsync(CancellationToken token)
    {
        if (rewardAnimCtrl == null)
        {
            this.LogWarning(
                "[IWannerGame_GameVictoryPanel_Reward] Reward animation skipped. controller is null.");
            return;
        }

        Transform rewardStartTransform =
            adRewardCtrl != null ? adRewardCtrl.GetPointerTransform() : null;
        CommonUIRewardPlayResult result;
        if (rewardStartTransform != null)
            result = await rewardAnimCtrl.PlayFrom(rewardStartTransform);
        else
            result = await rewardAnimCtrl.Play();

        if (result == CommonUIRewardPlayResult.Completed)
            this.Log("[IWannerGame_GameVictoryPanel_Reward] Reward animation completed.");
        else
            this.LogWarning(
                $"[IWannerGame_GameVictoryPanel_Reward] Reward animation did not complete. result:{result}");

        AudioManager.PlaySound("shopGetReward");
        token.ThrowIfCancellationRequested();
        propBlock?.UpdateData();
    }

    private void GrantRewardCoins()
    {
        if (GameManager.Instance?.Bag == null)
        {
            this.LogError(
                "[IWannerGame_GameVictoryPanel_Reward] Reward grant failed. Bag is null.");
            return;
        }

        int coins = adRewardCtrl != null ? adRewardCtrl.GetCurrentReward() : 0;
        this.Log($"[IWannerGame_GameVictoryPanel_Reward] Reward coins: {coins}");
        GameManager.Instance.Bag.AddTool(
            Config.Game.GameProps.Coin,
            coins,
            RewardType.Ad);
    }

    private void RefreshRewardPreviewText()
    {
        if (rewardTxt == null || adRewardCtrl == null)
            return;

        rewardTxt.text = $"+ {adRewardCtrl.GetCurrentReward()}";
    }

    private void CloseBone()
    {
        if (arrowBone != null)
            arrowBone.enabled = false;
    }

    private void RefreshShareContext()
    {
        if (shareFeedbackPart == null)
        {
            this.LogWarning(
                "[IWannerGame_GameVictoryPanel_Reward] Share feedback part is missing.");
            return;
        }

        if (!shareFeedbackPart.gameObject.activeSelf)
            shareFeedbackPart.gameObject.SetActive(true);

        shareFeedbackPart.RefreshIWannerData(CurrentGroupId, CurrentLevelPath);
    }

    private void ShowSharePart()
    {
        if (shareFeedbackPart == null)
            return;

        shareFeedbackPart.Show();
    }

    #endregion

    #region Button Group Animation

    private void ShowBtnGroup()
    {
        if (btnGroup == null)
            return;

        CacheBtnGroupTargetPosition();
        KillBtnGroupTween();
        btnGroup.alpha = 0f;
        btnGroup.interactable = false;
        btnGroup.blocksRaycasts = false;

        if (_btnGroupRect != null)
            _btnGroupRect.anchoredPosition = GetBtnGroupEnterPosition();

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        _btnGroupTween = sequence;
        if (_btnGroupRect != null)
        {
            sequence.Join(
                _btnGroupRect
                    .DOAnchorPos(_btnGroupTargetPosition, BtnGroupShowDuration)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true));
        }

        sequence
            .Join(btnGroup.DOFade(1f, BtnGroupShowDuration).SetUpdate(true))
            .OnComplete(() =>
            {
                if (btnGroup == null)
                    return;

                btnGroup.alpha = 1f;
                btnGroup.interactable = true;
                btnGroup.blocksRaycasts = true;
                _btnGroupTween = null;
            });
    }

    private void ResetBtnGroupView()
    {
        if (btnGroup == null)
            return;

        CacheBtnGroupTargetPosition();
        KillBtnGroupTween();
        btnGroup.alpha = 0f;
        btnGroup.interactable = false;
        btnGroup.blocksRaycasts = false;

        if (_btnGroupRect != null)
            _btnGroupRect.anchoredPosition = GetBtnGroupEnterPosition();
    }

    private void CacheBtnGroupTargetPosition()
    {
        if (_hasBtnGroupTargetPosition || btnGroup == null)
            return;

        _btnGroupRect = btnGroup.transform as RectTransform;
        if (_btnGroupRect != null)
            _btnGroupTargetPosition = _btnGroupRect.anchoredPosition;

        _hasBtnGroupTargetPosition = true;
    }

    private Vector2 GetBtnGroupEnterPosition()
    {
        return new Vector2(
            _btnGroupTargetPosition.x,
            _btnGroupTargetPosition.y - BtnGroupEnterOffsetY);
    }

    private void KillBtnGroupTween()
    {
        _btnGroupTween?.Kill();
        _btnGroupTween = null;
    }

    #endregion

    #region Factory

    /// <summary>
    /// 用途：打开 IWanner Group 首次通关使用的 Reward 胜利面板。
    /// 调用边界：仅由活动 IWannerPlayCtrl 在确认当前 Group 为首次通关后调用。
    /// 注意事项：奖励广告成功后按转盘最终值发放金币，导航仍由 IWanner 胜利事件处理。
    /// </summary>
    /// <param name="groupId">当前完成的 IWanner GroupId。</param>
    /// <param name="currentLevelPath">当前 Group 最后一小关的配置 Path。</param>
    /// <param name="congraktTips">当前 IWanner Group 配置的通关文案本地化键。</param>
    public new static IWannerGame_GameVictoryPanel_Reward Create(
        int groupId,
        string currentLevelPath,
        string congraktTips)
    {
        if (UIManager.Instance == null)
            return null;

        IWannerGame_GameVictoryPanel_Reward panel = UIManager.Instance.OpenUI<IWannerGame_GameVictoryPanel_Reward>(
            nameof(IWannerGame_GameVictoryPanel_Reward),
            UILayer.Popup);
        if (panel != null)
            panel.ConfigureVictoryContext(groupId, currentLevelPath, congraktTips);

        return panel;
    }

    #endregion
}

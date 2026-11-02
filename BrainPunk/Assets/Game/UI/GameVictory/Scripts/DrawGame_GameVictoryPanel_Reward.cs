using System.Threading;
using Bear.Logger;
using Bear.UI;
using Config;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using GameCommon;
using Spine.Unity;
using TMPro;
using UnityEngine;

/// <summary>
/// Draw 玩法首次通关专用奖励胜利面板。
/// 导航与重玩沿用 Draw 胜利面板，奖励广告、金币发放和按钮表现与主线奖励面板一致。
/// </summary>
public sealed class DrawGame_GameVictoryPanel_Reward : DrawGame_GameVictoryPanel
{
    #region Constants

    private const float CloseBoneDelay = 2f;
    private const float BtnGroupShowDuration = 0.35f;
    private const float BtnGroupEnterOffsetY = 160f;

    #endregion

    #region Inspector

    /// <summary>用途：发起首次通关的金币奖励广告；仅在奖励未领取且流程空闲时可交互。</summary>
    [SerializeField] private CustomButton AdRewardBtn;

    /// <summary>用途：保留主线奖励 prefab 的广告后下一关节点；当前只负责打开状态复位。</summary>
    [SerializeField] private CustomButton AfterAdNextBtn;

    /// <summary>用途：奖励转盘结束后关闭指针骨骼跟随，避免空闲期持续更新。</summary>
    [SerializeField] private BoneFollowerGraphic arrowBone;

    /// <summary>用途：驱动奖励数值随机展示并提供最终金币数量；必须由 Reward prefab 绑定。</summary>
    [SerializeField] private HorizontalRandomAdRewardCtrl adRewardCtrl;

    /// <summary>用途：实时展示当前奖励转盘指向的金币数量。</summary>
    [SerializeField] private TextMeshProUGUI rewardTxt;

    /// <summary>用途：奖励到账后刷新顶部道具显示。</summary>
    [SerializeField] private PropBlock propBlock;

    /// <summary>用途：播放金币飞入背包的既有通用动画。</summary>
    [SerializeField] private CommonUIRewardAnimCtrl rewardAnimCtrl;

    /// <summary>用途：标识主线 Postcard 引导遮罩；Draw 奖励版打开时强制关闭，避免阻塞交互。</summary>
    [SerializeField] private GameObject clickBlock;

    /// <summary>用途：承载奖励按钮组的延迟进场动画。</summary>
    [SerializeField] private CanvasGroup btnGroup;

    /// <summary>用途：奖励领取完成后置灰广告按钮，仅负责视觉表现。</summary>
    [SerializeField] private CustomGrayRender grayRender;

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

    #region Draw Victory Hooks

    /// <summary>奖励版使用 1_appear01 作为新版 Spine 入场动画。</summary>
    protected override string EntranceAnimationName => "1_appear01";

    /// <summary>奖励版在 1_appear01 完成后循环播放 1_idle。</summary>
    protected override string IdleAnimationName => "1_idle";

    protected override void BindButtonsExtra()
    {
        if (AdRewardBtn != null)
            AdRewardBtn.OnClick += OnClickAdReward;
    }

    protected override void OnOpenExtra()
    {
        if (clickBlock != null)
            clickBlock.SetActive(false);

        Invoke(nameof(CloseBone), CloseBoneDelay);
    }

    protected override void OnShowExtra()
    {
        TryResumeRewardSelect();
        if (!_hasHidden)
            return;

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
        CancelInvoke();
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
        if (arrowBone != null)
            arrowBone.enabled = true;

        if (AdRewardBtn != null)
            AdRewardBtn.gameObject.SetActive(true);

        grayRender?.SetGray(false);

        if (AfterAdNextBtn != null)
            AfterAdNextBtn.gameObject.SetActive(false);

        ResetBtnGroupView();
        RefreshRewardPreviewText();
    }

    protected override void ResetRuntimeStateExtra()
    {
        _isRewardAdRequesting = false;
        _isAdRewardPlaying = false;
        _isRewardSelectorActive = false;
        _resumeRewardSelectOnEnable = false;
        _isAdRewardComplete = false;
    }

    protected override void OnEntranceAnimationCompleteExtra()
    {
        ShowBtnGroup();
        PlayRewardSelect();
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

    private void OnClickAdReward(CustomButton button)
    {
        if (_isAdRewardComplete)
        {
            this.LogWarning(
                "[DrawGame_GameVictoryPanel_Reward] Reward button ignored. reward completed.");
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

    private void OnRewardAdCallback(string placement, bool isSuccess)
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
                    "[DrawGame_GameVictoryPanel_Reward] Reward flow aborted. selector is null.");
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
                "[DrawGame_GameVictoryPanel_Reward] Reward selector is missing.");
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
                "[DrawGame_GameVictoryPanel_Reward] Reward animation skipped. controller is null.");
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
            this.Log("[DrawGame_GameVictoryPanel_Reward] Reward animation completed.");
        else
            this.LogWarning(
                $"[DrawGame_GameVictoryPanel_Reward] Reward animation did not complete. result:{result}");

        AudioManager.PlaySound("shopGetReward");
        token.ThrowIfCancellationRequested();
        propBlock?.UpdateData();
    }

    private void GrantRewardCoins()
    {
        if (GameManager.Instance?.Bag == null)
        {
            this.LogError(
                "[DrawGame_GameVictoryPanel_Reward] Reward grant failed. Bag is null.");
            return;
        }

        int coins = adRewardCtrl != null ? adRewardCtrl.GetCurrentReward() : 0;
        this.Log($"[DrawGame_GameVictoryPanel_Reward] Reward coins: {coins}");
        GameManager.Instance.Bag.AddTool(Config.Game.GameProps.Coin, coins, RewardType.Ad);
    }

    private void RefreshRewardPreviewText()
    {
        if (rewardTxt != null && adRewardCtrl != null)
            rewardTxt.text = $"+ {adRewardCtrl.GetCurrentReward()}";
    }

    private void CloseBone()
    {
        if (arrowBone != null)
            arrowBone.enabled = false;
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
    /// 打开 Draw 玩法首次通关奖励面板并注入当前关卡数据。
    /// 调用边界：仅由活动中的 DrawPlayCtrl 在写入本次 Draw 星级记录后调用。
    /// 注意事项：资源必须使用 DrawGame_GameVictoryPanel_Reward 名称并绑定本组件。
    /// </summary>
    public new static DrawGame_GameVictoryPanel_Reward Create(DrawLevelData levelData)
    {
        if (levelData == null || UIManager.Instance == null)
            return null;

        DrawGame_GameVictoryPanel_Reward panel =
            UIManager.Instance.OpenUI<DrawGame_GameVictoryPanel_Reward>(
                nameof(DrawGame_GameVictoryPanel_Reward),
                UILayer.Popup);
        panel?.Configure(levelData);
        return panel;
    }

    #endregion
}

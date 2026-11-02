using System.Threading;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using GameCommon;
using Spine.Unity;
using TMPro;
using UnityEngine;

public class GameVictoryPanel_Reawrd : GameVictoryPanel
{
    private const float BtnGroupShowDuration = 0.35f;
    private const float BtnGroupEnterOffsetY = 160f;

    [Header("Reward")]
    [SerializeField] private CustomButton AdRewardBtn;
    [SerializeField] private CustomButton AfterAdNextBtn;
    [SerializeField] private BoneFollowerGraphic arrowBone;
    [SerializeField] private HorizontalRandomAdRewardCtrl adRewardCtrl;
    [SerializeField] private TextMeshProUGUI rewardTxt;
    [SerializeField] private PropBlock propBlock;
    [SerializeField] private GameVictoryPanel_Share sharePart;

    // 按钮组进场动画根节点；仅用于奖励版胜利面板延迟显示，需在 prefab 上绑定带 RectTransform 的 CanvasGroup。
    [SerializeField] private CanvasGroup btnGroup;

    // AdReward 完成后置灰奖励按钮；仅影响按钮视觉，不负责点击禁用状态。
    [SerializeField] private CustomGrayRender grayRender;

    private RectTransform _btnGroupRect;
    private Vector2 _btnGroupTargetPosition;
    private bool _hasBtnGroupTargetPosition;
    private Tween _btnGroupTween;

    private bool _isRewardAdRequesting;
    private bool _isAdRewardPlaying;
    private bool _isRewardSelectorActive;
    private bool _resumeRewardSelectOnEnable;
    private bool _isAdRewardComplete;
    private CancellationTokenSource _rewardFlowCts;

    private bool hasHide = false;

    protected override void BindButtonsExtra()
    {
        if (AdRewardBtn != null)
            AdRewardBtn.OnClick += OnClickAdReward;
    }

    protected override void OnOpenExtra()
    {
        sharePart.RefreshData();
        Invoke(nameof(CloseBone), CloseBoneDelay);
        Invoke(nameof(ShowSharePart), CloseBoneDelay);
        Invoke(nameof(ShowBtnGroup), CloseBoneDelay);
    }

    protected override void OnShowExtra()
    {
        TryResumeRewardSelect();

        if (hasHide)
        {
            ShowSharePart();
            ShowBtnGroup();
            hasHide = false;
        }

    }

    protected override void OnHideExtra()
    {
        hasHide = true;
        PrepareRewardSelectResume();
        KillBtnGroupTween();
    }

    protected override void OnCloseExtra()
    {
        _rewardFlowCts?.Cancel();
        _rewardFlowCts?.Dispose();
        _rewardFlowCts = null;
        KillBtnGroupTween();
        hasHide = false;
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

    protected override void OnInAnimationCompleteExtra()
    {
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
            AdRewardBtn.Interactable = !_isAdRewardComplete && !IsNoAdsPurchasing && !_isRewardAdRequesting && (!_isAdRewardPlaying || isRewardSelecting);
    }

    /// <summary>
    /// Capture selector resume intent before the panel is hidden by the UI stack.
    /// The child selector may be disabled during the same hide pass, so the panel
    /// keeps its own high-level "selection is active" flag instead of re-reading child state.
    /// </summary>
    private void PrepareRewardSelectResume()
    {
        if (!CanPrepareRewardSelectResume())
            return;

        _resumeRewardSelectOnEnable = true;
        adRewardCtrl.PreserveCurrentStateOnNextEnable();
    }

    /// <summary>
    /// Restarts selector presentation when the panel is shown again after a temporary hide.
    /// </summary>
    private void TryResumeRewardSelect()
    {
        if (!CanResumeRewardSelect())
            return;

        _resumeRewardSelectOnEnable = false;
        PlayRewardSelect();
    }

    private bool CanPrepareRewardSelectResume()
    {
        if (!IsOpen || adRewardCtrl == null || !_isRewardSelectorActive)
            return false;

        return !_isRewardAdRequesting && !_isAdRewardPlaying && !IsNextLevelTriggered;
    }

    private bool CanResumeRewardSelect()
    {
        if (!_resumeRewardSelectOnEnable || !IsOpen || adRewardCtrl == null)
            return false;

        return !_isRewardAdRequesting && !_isAdRewardPlaying && !IsNextLevelTriggered;
    }

    private void OnClickAdReward(CustomButton btn)
    {
        if (_isAdRewardComplete)
        {
            this.LogWarning("[GameVictoryPanel] AdRewardBtn ignored. reward already completed.");
            RefreshButtonStates();
            return;
        }

        if (_isRewardAdRequesting || _isAdRewardPlaying)
        {
            this.LogWarning("[GameVictoryPanel] AdRewardBtn ignored. reward flow already in progress.");
            return;
        }

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

    private void OnRewardAdCallback(string placement, bool isSuc)
    {
        _isRewardAdRequesting = false;

        if (this == null || !isActiveAndEnabled)
            return;

        if (!isSuc)
        {
            PlayRewardSelect();
            RefreshButtonStates();
            return;
        }

        PlayRewardFlowAsync().Forget();
        _isAdRewardComplete = true;
        grayRender?.SetGray(true);
        RefreshButtonStates();
        /*
                if (AdRewardBtn != null)
                    AdRewardBtn.gameObject.SetActive(false);

                if (NextBtn != null)
                    NextBtn.gameObject.SetActive(false); */
    }

    private async UniTask PlayRewardFlowAsync()
    {
        if (_isAdRewardPlaying)
            return;

        _rewardFlowCts?.Cancel();
        _rewardFlowCts?.Dispose();
        _rewardFlowCts = new CancellationTokenSource();
        var token = _rewardFlowCts.Token;

        _isAdRewardPlaying = true;
        _isRewardSelectorActive = false;
        RefreshButtonStates();

        try
        {
            if (adRewardCtrl == null)
            {
                this.LogError("[GameVictoryPanel] Reward flow aborted. adRewardCtrl is null.");
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
            this.LogWarning("[GameVictoryPanel] Reward selector skipped. adRewardCtrl is null.");
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
            this.LogWarning("[GameVictoryPanel] Reward animation skipped. controller is null.");
            return;
        }

        Transform rewardStartTransform = adRewardCtrl != null ? adRewardCtrl.GetPointerTransform() : null;
        CommonUIRewardPlayResult result;
        if (rewardStartTransform != null)
            result = await rewardAnimCtrl.PlayFrom(rewardStartTransform);
        else
            result = await rewardAnimCtrl.Play();

        if (result == CommonUIRewardPlayResult.Completed)
            this.Log("[GameVictoryPanel] Reward animation completed.");
        else
            this.LogWarning($"[GameVictoryPanel] Reward animation did not complete. result:{result}");

        AudioManager.PlaySound("shopGetReward");

        token.ThrowIfCancellationRequested();

        propBlock?.UpdateData();

        /*         await UniTask.WaitForSeconds(1, ignoreTimeScale: true, cancellationToken: token);

                if (!token.IsCancellationRequested)
                    OnNextLevel(null); */
    }

    private void GrantRewardCoins()
    {
        if (GameManager.Instance?.Bag == null)
        {
            this.LogError("[GameVictoryPanel] Reward grant failed. Bag is null.");
            return;
        }

        int coins = adRewardCtrl != null ? adRewardCtrl.GetCurrentReward() : 0;
        this.Log($"[GameVictoryPanel] Reward coins: {coins}");
        GameManager.Instance.Bag.AddTool(Config.Game.GameProps.Coin, coins, RewardType.Ad);
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

    private void ShowSharePart()
    {
        sharePart.Show();
    }

    #region BtnGroup 进场动画

    private void ShowBtnGroup()
    {
        // 从界面下方进入到初始位置，canvasGroup 从 0 - 1
        if (btnGroup == null)
        {
            this.LogWarning("[GameVictoryPanel] ShowBtnGroup skipped. btnGroup is null.");
            return;
        }

        CacheBtnGroupTargetPosition();
        KillBtnGroupTween();

        btnGroup.alpha = 0f;
        btnGroup.interactable = false;
        btnGroup.blocksRaycasts = false;

        if (_btnGroupRect != null)
            _btnGroupRect.anchoredPosition = GetBtnGroupEnterPosition();

        var sequence = DOTween.Sequence().SetUpdate(true);
        _btnGroupTween = sequence;

        if (_btnGroupRect != null)
        {
            sequence.Join(_btnGroupRect
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
        if (_hasBtnGroupTargetPosition)
            return;

        if (btnGroup == null)
            return;

        _btnGroupRect = btnGroup.transform as RectTransform;
        if (_btnGroupRect != null)
            _btnGroupTargetPosition = _btnGroupRect.anchoredPosition;

        _hasBtnGroupTargetPosition = true;
    }

    private Vector2 GetBtnGroupEnterPosition()
    {
        return new Vector2(_btnGroupTargetPosition.x, _btnGroupTargetPosition.y - BtnGroupEnterOffsetY);
    }

    private void KillBtnGroupTween()
    {
        _btnGroupTween?.Kill();
        _btnGroupTween = null;
    }

    #endregion
}

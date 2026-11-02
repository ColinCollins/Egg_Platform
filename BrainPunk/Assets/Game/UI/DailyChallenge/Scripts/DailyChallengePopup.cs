using System;
using System.Collections.Generic;
using System.Threading;
using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.ConfigModule;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 胜利后的每日挑战进度结算弹窗。
/// 负责在打开时请求 Manager 发放待领取奖励，并展示前后快照、阶段达成动画和通用奖励弹窗。
/// </summary>
public partial class DailyChallengePopup : BaseUIView, IDebuger
{
    #region Animation Constants

    private const float PreviousProgressHoldDuration = 1f;
    private const float ProgressDuration = 0.45f;
    private const float RewardClaimAnimationDuration = 1.5f;

    #endregion

    #region Serialized Fields

    /// <summary>
    /// 第一阶段独立进度前景；仅显示第一阶段内进度，达到第一阶段后保持填满。
    /// </summary>
    [SerializeField, Tooltip("第一阶段独立进度前景；必须在 DailyChallengePopup prefab 中绑定。")]
    private Image firstStageProgressFill;

    /// <summary>
    /// 第二阶段独立进度前景；进入第二阶段前保持为空。
    /// </summary>
    [SerializeField, Tooltip("第二阶段独立进度前景；必须在 DailyChallengePopup prefab 中绑定。")]
    private Image secondStageProgressFill;

    [SerializeField, Tooltip("累计进度条 RectTransform；必须在 DailyChallengePopup prefab 中绑定。")]
    private RectTransform processBar;

    [SerializeField, Tooltip("第一阶段奖励节点；为空时按横向位置自动识别。")]
    private DailyChallengeStepItem firstStageItem;

    [SerializeField, Tooltip("第二阶段奖励节点；为空时按横向位置自动识别。")]
    private DailyChallengeStepItem secondStageItem;

    [SerializeField, Tooltip("进度游标；横向位置跟随当前阶段进度图，必须在 DailyChallengePopup prefab 中绑定。")]
    private RectTransform cursor;

    /// <summary>
    /// 进度数字开始回弹时的缩放倍数；仅在数字变化动画中使用，1 表示不放大。
    /// </summary>
    [SerializeField, Min(0f), Tooltip("进度数字开始回弹时的缩放倍数；1 表示不放大。")]
    private float numberStartScale = 1.2f;

    /// <summary>
    /// 进度数字从起始缩放回弹到原始大小的持续秒数；设为 0 时立即恢复。
    /// </summary>
    [SerializeField, Min(0f), Tooltip("进度数字缩放回弹动画的持续秒数；设为 0 时立即恢复。")]
    private float numberScaleDuration = 0.18f;

    /// <summary>
    /// 正常结算流程完成后的自动关闭等待秒数；仅影响本弹窗，设为 0 时立即关闭。
    /// </summary>
    [SerializeField, Min(0f), Tooltip("正常结算流程完成后的自动关闭等待秒数；设为 0 时立即关闭。")]
    private float autoCloseDelay = 1f;

    /// <summary>
    /// 领奖时在对应 mainIcon 位置播放的粒子集合；必须在 DailyChallengePopup prefab 中直接绑定。
    /// </summary>
    [SerializeField, Tooltip("领奖粒子集合；领奖时统一移动到对应 mainIcon 位置并重播。")]
    private List<ParticleSystem> rewardEffects;

    /// <summary>
    /// 用途：显示当前 Coin 数量，并在每个阶段奖励展示完成后由 DailyChallengePopup 手动刷新。
    /// 调用边界：目标 PropBlock 必须配置为 Coin 且关闭 IsAutoUpdate；未绑定时运行时从当前 Popup 子级查找。
    /// 注意事项：多个阶段在打开时已前置发奖，刷新时会扣除尚未展示阶段的基础 Coin，避免数字提前跳到最终值。
    /// </summary>
    [SerializeField] private PropBlock coinPropBlock;

    #endregion

    #region Runtime State

    private DailyChallengeSettlement _settlement;
    private UniTaskCompletionSource _closeCompletionSource;
    private Sequence _progressSequence;
    private Tween _numberScaleTween;
    private bool _isClosing;
    private bool _isWaitingForRewardPopup;
    private bool _closeAfterInterruption;
    private bool _hasLoggedMissingReferences;
    private bool _refreshCoinAfterRewardPopup;
    private int _remainingBaseCoinAfterRewardPopup;
    private readonly Dictionary<int, DailyChallengeRewardData> _claimedRewardDataByStageId = new();

    #endregion

    #region UIView Lifecycle

    public override void OnCreate()
    {
        base.OnCreate();
        ResolveReferences();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        _isClosing = false;
        _isWaitingForRewardPopup = false;
        _closeAfterInterruption = false;
        _refreshCoinAfterRewardPopup = false;
        _remainingBaseCoinAfterRewardPopup = 0;
        _claimedRewardDataByStageId.Clear();
        StopRewardEffects();
        ResolveReferences();
        ResolveCoinPropBlock()?.UpdateData();

        AudioManager.PlaySound("DC_PopIn");
    }

    public override void OnShow()
    {
        RefreshCoinAfterRewardPopupIfPending();
        if (_closeAfterInterruption && !_isWaitingForRewardPopup)
        {
            _closeAfterInterruption = false;
            CompleteAndClose();
            return;
        }

        base.OnShow();
    }

    public override void OnHide()
    {
        if (IsOpen && !_isClosing && !_isWaitingForRewardPopup)
            _closeAfterInterruption = true;

        base.OnHide();
    }

    public override void OnClose()
    {
        KillTweens();
        StopRewardEffects();
        _settlement = null;
        _claimedRewardDataByStageId.Clear();
        _isClosing = false;
        _isWaitingForRewardPopup = false;
        _closeAfterInterruption = false;
        _refreshCoinAfterRewardPopup = false;
        _remainingBaseCoinAfterRewardPopup = 0;
        base.OnClose();
        CompleteCloseWait();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 打开每日挑战进度结算弹窗并等待内部奖励流程与关闭生命周期完成。
    /// 调用边界：仅由 PlayCtrl_Success 在 RatingPopup 关闭后调用。
    /// 注意事项：创建失败时直接完成；外部取消会关闭仍打开的弹窗并继续抛出取消。
    /// </summary>
    /// <param name="settlement">Manager 已保存的本次胜利结算快照。</param>
    /// <param name="cancellationToken">SUCCESS 状态流程的生命周期取消令牌。</param>
    public static async UniTask ShowAndWaitAsync(
        DailyChallengeSettlement settlement,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DailyChallengePopup panel = Create(settlement);
        if (panel == null)
            return;

        try
        {
            await panel.WaitForCloseAsync(cancellationToken);
        }
        catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (panel != null && panel.IsOpen)
                UIManager.Instance?.DestroyUI(panel);

            throw;
        }
    }

    private static DailyChallengePopup Create(DailyChallengeSettlement settlement)
    {
        if (settlement?.BeforeState == null || settlement.AfterState == null)
            return null;

        DailyChallengePopup panel = UIManager.Instance?.OpenUI<DailyChallengePopup>(
            nameof(DailyChallengePopup),
            UILayer.Popup);
        if (panel != null)
        {
            panel.SetData(settlement);
            MarkShown();
        }

        return panel;
    }

    private static void MarkShown()
    {
        if (DB.GameData.HasShowDailyChallenge)
            return;

        DB.GameData.HasShowDailyChallenge = true;
        DB.GameData.Save();
    }

    #endregion

    #region Flow

    private void SetData(DailyChallengeSettlement settlement)
    {
        CompleteCloseWait();
        _closeCompletionSource = new UniTaskCompletionSource();
        _settlement = settlement;
        ClaimPendingRewardsOnOpen();
        RunSettlementFlowAsync().Forget();
    }

    private void ClaimPendingRewardsOnOpen()
    {
        _claimedRewardDataByStageId.Clear();
        IReadOnlyList<int> pendingStageIds = _settlement?.PendingRewardStageIds;
        if (pendingStageIds == null || pendingStageIds.Count == 0)
            return;

        DailyChallengeManager manager = GameManager.Instance?.DailyChallenge;
        if (manager == null)
        {
            this.LogWarning("[DailyChallengePopup] Pending rewards cannot be claimed because manager is unavailable.");
            return;
        }

        for (int index = 0; index < pendingStageIds.Count; index++)
        {
            int stageId = pendingStageIds[index];
            if (!manager.TryClaimRewardOnPopupOpen(stageId, out DailyChallengeRewardData rewardData))
            {
                this.LogWarning($"[DailyChallengePopup] Reward claim failed on popup open. stageId:{stageId}");
                break;
            }

            _claimedRewardDataByStageId[stageId] = rewardData;
        }
    }

    private async UniTask WaitForCloseAsync(CancellationToken cancellationToken)
    {
        UniTaskCompletionSource completionSource = _closeCompletionSource;
        if (completionSource == null || !IsOpen)
            return;

        await completionSource.Task.AttachExternalCancellation(cancellationToken);
    }

    private async UniTaskVoid RunSettlementFlowAsync()
    {
        if (_settlement?.BeforeState == null || _settlement.AfterState == null)
        {
            CompleteAndClose();
            return;
        }

        ResolveReferences();
        ApplyState(_settlement.BeforeState);
        if (!await PlayProgressAnimationAsync(_settlement.BeforeState, _settlement.AfterState))
            return;

        IReadOnlyList<int> pendingStageIds = _settlement.PendingRewardStageIds;
        if (pendingStageIds == null || pendingStageIds.Count == 0)
        {
            if (!await WaitBeforeAutoCloseAsync())
                return;

            CompleteAndClose();
            return;
        }

        for (int index = 0; index < pendingStageIds.Count; index++)
        {
            int stageId = pendingStageIds[index];
            if (!await PlayClaimedAnimationAsync(stageId))
                return;

            if (!_claimedRewardDataByStageId.TryGetValue(stageId, out DailyChallengeRewardData rewardData))
            {
                this.LogWarning($"[DailyChallengePopup] Claimed reward display data is unavailable. stageId:{stageId}");
                CompleteAndClose();
                return;
            }

            int remainingBaseCoin = GetRemainingClaimedBaseCoin(pendingStageIds, index + 1);
            if (!await OpenRewardPopupAsync(rewardData, remainingBaseCoin))
                return;
        }

        if (!await WaitBeforeAutoCloseAsync())
            return;

        CompleteAndClose();
    }

    private async UniTask<bool> WaitBeforeAutoCloseAsync()
    {
        if (this == null || !isActiveAndEnabled)
            return false;

        float delaySeconds = Mathf.Max(0f, autoCloseDelay);
        if (delaySeconds <= 0f)
            return true;

        bool isCanceled = await UniTask.Delay(
                TimeSpan.FromSeconds(delaySeconds),
                ignoreTimeScale: true,
                cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        return !isCanceled && this != null && isActiveAndEnabled;
    }

    private async UniTask<bool> PlayProgressAnimationAsync(
        DailyChallengeViewState beforeState,
        DailyChallengeViewState afterState)
    {
        float firstStageTargetProgress = afterState.IsFirstStageReached
            ? 1f
            : afterState.CurrentStageNormalizedProgress;
        float secondStageTargetProgress = afterState.IsFirstStageReached
            ? afterState.CurrentStageNormalizedProgress
            : 0f;
        bool hasProgressChange = afterState.TotalProgress != beforeState.TotalProgress;
        if (!hasProgressChange)
        {
            ApplyState(afterState);
            return this != null && isActiveAndEnabled;
        }

        bool isHoldCanceled = await UniTask.Delay(
                TimeSpan.FromSeconds(PreviousProgressHoldDuration),
                ignoreTimeScale: true,
                cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        if (isHoldCanceled || this == null || !isActiveAndEnabled)
            return false;

        SetProgressNumber(afterState.TotalProgress, true);
        if (_numberScaleTween != null)
            await _numberScaleTween.AsyncWaitForCompletion();
        if (this == null || !isActiveAndEnabled)
            return false;

        KillProgressTween();
        _progressSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);

        bool hasProgressFill = firstStageProgressFill != null || secondStageProgressFill != null;
        if (hasProgressFill)
            AudioManager.PlaySound("DC_process");

        if (firstStageProgressFill != null)
        {
            ConfigureProgressFill(firstStageProgressFill);
            _progressSequence.Join(firstStageProgressFill
                .DOFillAmount(firstStageTargetProgress, ProgressDuration)
                .SetEase(Ease.OutCubic));
        }

        if (secondStageProgressFill != null)
        {
            ConfigureProgressFill(secondStageProgressFill);
            _progressSequence.Join(secondStageProgressFill
                .DOFillAmount(secondStageTargetProgress, ProgressDuration)
                .SetEase(Ease.OutCubic));
        }

        if (cursor != null)
        {
            float cursorX = GetCursorAnchoredPositionX(afterState);
            _progressSequence.Join(cursor.DOAnchorPosX(cursorX, ProgressDuration).SetEase(Ease.OutCubic));
        }

        await _progressSequence.AsyncWaitForCompletion();
        return this != null && isActiveAndEnabled;
    }

    private async UniTask<bool> PlayClaimedAnimationAsync(int stageId)
    {
        DailyChallengeStepItem item = stageId == 1 ? firstStageItem : secondStageItem;
        if (item != null)
            PlayRewardEffects(item.RewardEffectWorldPosition);
        
        AudioManager.PlaySound("DC_OpenChest");
        Tween claimedTween = item?.PlayClaimedAnimation(RewardClaimAnimationDuration);
        if (claimedTween != null)
            await claimedTween.AsyncWaitForCompletion();

        return this != null && isActiveAndEnabled;
    }

    private void PlayRewardEffects(Vector3 worldPosition)
    {
        if (rewardEffects == null)
            return;

        for (int index = 0; index < rewardEffects.Count; index++)
        {
            ParticleSystem effect = rewardEffects[index];
            if (effect == null)
                continue;

            effect.transform.position = worldPosition;
            if (!effect.gameObject.activeSelf)
                effect.gameObject.SetActive(true);

            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Play(true);
        }
    }

    private async UniTask<bool> OpenRewardPopupAsync(
        DailyChallengeRewardData rewardData,
        int remainingBaseCoin)
    {
        _isWaitingForRewardPopup = true;
        _refreshCoinAfterRewardPopup = true;
        _remainingBaseCoinAfterRewardPopup = Mathf.Max(0, remainingBaseCoin);
        try
        {
            var closeSource = new UniTaskCompletionSource();
            CommonGetRewardPopup rewardPopup = CommonGetRewardPopup.Create(
                rewardData.Rewards,
                rewardData.Multiple,
                () => closeSource.TrySetResult(),
                grantNormalReward: false,
                remainingHiddenCoinAfterReward: remainingBaseCoin);
            if (rewardPopup == null)
            {
                _refreshCoinAfterRewardPopup = false;
                _remainingBaseCoinAfterRewardPopup = 0;
                this.LogWarning($"[DailyChallengePopup] CommonGetRewardPopup create failed. stageId:{rewardData.StageId}");
                CompleteAndClose();
                return false;
            }

            bool isCanceled = await closeSource.Task
                .AttachExternalCancellation(this.GetCancellationTokenOnDestroy())
                .SuppressCancellationThrow();
            if (!isCanceled && this != null && isActiveAndEnabled)
                RefreshCoinAfterRewardPopupIfPending();

            return !isCanceled && this != null && isActiveAndEnabled;
        }
        finally
        {
            if (this != null)
            {
                _isWaitingForRewardPopup = false;
                if (IsOpen && !isActiveAndEnabled)
                    _closeAfterInterruption = true;
            }
        }
    }

    private void CompleteAndClose()
    {
        if (_isClosing || this == null)
            return;

        _isClosing = true;
        UIManager.Instance?.DestroyUI(this);
    }

    #endregion

    #region View

    private void ApplyState(DailyChallengeViewState state)
    {
        if (state == null)
            return;

        SetProgressVisual(state);
        SetProgressNumber(state.TotalProgress, false);
        firstStageItem?.Refresh(state.FirstStageTarget, state.IsFirstRewardClaimed);
        secondStageItem?.Refresh(state.SecondStageTarget, state.IsSecondRewardClaimed);
    }

    private void SetProgressVisual(DailyChallengeViewState state)
    {
        if (state == null)
            return;

        float firstStageProgress = state.IsFirstStageReached
            ? 1f
            : state.CurrentStageNormalizedProgress;
        float secondStageProgress = state.IsFirstStageReached
            ? state.CurrentStageNormalizedProgress
            : 0f;
        SetProgressFill(firstStageProgressFill, firstStageProgress);
        SetProgressFill(secondStageProgressFill, secondStageProgress);

        if (cursor != null)
        {
            Vector2 cursorPosition = cursor.anchoredPosition;
            cursorPosition.x = GetCursorAnchoredPositionX(state);
            cursor.anchoredPosition = cursorPosition;
        }
    }

    private static void SetProgressFill(Image image, float normalizedProgress)
    {
        if (image == null)
            return;

        ConfigureProgressFill(image);
        image.fillAmount = Mathf.Clamp01(normalizedProgress);
    }

    private static void ConfigureProgressFill(Image image)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = 0;
    }

    private void SetProgressNumber(int progress, bool playScale)
    {
        if (UiDaily03Txt == null)
            return;

        UiDaily03Txt.SetText(Mathf.Max(0, progress).ToString());
        _numberScaleTween?.Kill();
        _numberScaleTween = null;
        UiDaily03Txt.transform.localScale = Vector3.one;
        if (!playScale)
            return;

        AudioManager.PlaySound("DC_number");
        _numberScaleTween = UiDaily03Txt.transform
            .DOScale(Vector3.one * numberStartScale, Mathf.Max(0f, numberScaleDuration) / 2f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(gameObject)
            .SetLoops(2, LoopType.Yoyo);
    }

    private float GetCursorAnchoredPositionX(DailyChallengeViewState state)
    {
        if (state == null || cursor == null || cursor.parent == null)
            return cursor != null ? cursor.anchoredPosition.x : 0f;

        if (TryGetReachedStageItemAnchoredPositionX(state, out float reachedStageItemX))
            return reachedStageItemX;

        Image activeProgressFill = state.IsFirstStageReached
            ? secondStageProgressFill
            : firstStageProgressFill;
        float activeStageProgress = state.CurrentStageNormalizedProgress;
        RectTransform progressTrack = activeProgressFill != null
            ? activeProgressFill.rectTransform
            : processBar;
        float normalizedProgress = activeProgressFill != null
            ? activeStageProgress
            : state.ExpandedNormalizedProgress;
        if (progressTrack == null)
            return cursor.anchoredPosition.x;

        float progressX = progressTrack.rect.xMin
            + Mathf.Clamp01(normalizedProgress) * progressTrack.rect.width;
        Vector3 worldPoint = progressTrack.TransformPoint(
            new Vector3(progressX, progressTrack.rect.center.y, 0f));
        Vector3 localPoint = cursor.parent.InverseTransformPoint(worldPoint);
        return localPoint.x;
    }

    private bool TryGetReachedStageItemAnchoredPositionX(
        DailyChallengeViewState state,
        out float anchoredPositionX)
    {
        anchoredPositionX = 0f;
        DailyChallengeStepItem reachedStageItem = state.IsSecondStageReached
            ? secondStageItem
            : state.IsFirstStageReached && state.TotalProgress == state.FirstStageTarget
                ? firstStageItem
                : null;
        if (reachedStageItem == null || cursor?.parent == null)
            return false;

        Vector3 localPoint = cursor.parent.InverseTransformPoint(reachedStageItem.transform.position);
        anchoredPositionX = localPoint.x;
        return true;
    }

    #endregion

    #region Reference Resolution

    private void ResolveReferences()
    {
        ResolveStepItems();
        ResolveCoinPropBlock();

        if (!_hasLoggedMissingReferences
            && (firstStageProgressFill == null
                || secondStageProgressFill == null
                || processBar == null
                || cursor == null
                || UiDaily03Txt == null
                || firstStageItem == null
                || secondStageItem == null
                || coinPropBlock == null
                || rewardEffects == null
                || rewardEffects.Count == 0))
        {
            _hasLoggedMissingReferences = true;
            this.LogWarning(
                $"[DailyChallengePopup] Required references are incomplete. "
                + $"firstStageProgressFill:{firstStageProgressFill != null}, "
                + $"secondStageProgressFill:{secondStageProgressFill != null}, "
                + $"processBar:{processBar != null}, "
                + $"cursor:{cursor != null}, progressText:{UiDaily03Txt != null}, "
                + $"firstStage:{firstStageItem != null}, secondStage:{secondStageItem != null}, "
                + $"coinPropBlock:{coinPropBlock != null}, "
                + $"rewardEffects:{rewardEffects != null && rewardEffects.Count > 0}");
        }
    }

    private void ResolveStepItems()
    {
        if (firstStageItem != null && secondStageItem != null)
            return;

        DailyChallengeStepItem[] items = GetComponentsInChildren<DailyChallengeStepItem>(true);
        if (items.Length < 2)
            return;

        DailyChallengeStepItem leftItem = items[0];
        DailyChallengeStepItem rightItem = items[0];
        for (int index = 1; index < items.Length; index++)
        {
            DailyChallengeStepItem item = items[index];
            if (item.transform.localPosition.x < leftItem.transform.localPosition.x)
                leftItem = item;
            if (item.transform.localPosition.x >= rightItem.transform.localPosition.x)
                rightItem = item;
        }

        firstStageItem = leftItem;
        secondStageItem = rightItem == leftItem ? items[1] : rightItem;
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
        var bag = GameManager.Instance?.Bag;
        if (propBlock == null || bag == null)
            return;

        int actualCoinCount = bag.GetToolCount(Config.Game.GameProps.Coin);
        int displayCoinCount = Mathf.Max(0, actualCoinCount - _remainingBaseCoinAfterRewardPopup);
        propBlock.BindData(displayCoinCount);
        _refreshCoinAfterRewardPopup = false;
        _remainingBaseCoinAfterRewardPopup = 0;
    }

    private int GetRemainingClaimedBaseCoin(IReadOnlyList<int> pendingStageIds, int startIndex)
    {
        if (pendingStageIds == null || startIndex >= pendingStageIds.Count)
            return 0;

        int totalCoin = 0;
        for (int index = Mathf.Max(0, startIndex); index < pendingStageIds.Count; index++)
        {
            int stageId = pendingStageIds[index];
            if (!_claimedRewardDataByStageId.TryGetValue(stageId, out DailyChallengeRewardData rewardData)
                || rewardData?.Rewards == null
                || !rewardData.Rewards.TryGetValue(Config.Game.GameProps.Coin, out object coinValue)
                || !BaseParamUtils.TryGetInt(coinValue, out int coinCount)
                || coinCount <= 0)
            {
                continue;
            }

            totalCoin = coinCount > int.MaxValue - totalCoin
                ? int.MaxValue
                : totalCoin + coinCount;
        }

        return totalCoin;
    }

    #endregion

    #region Cleanup

    private void KillTweens()
    {
        KillProgressTween();
        _numberScaleTween?.Kill();
        _numberScaleTween = null;
    }

    private void StopRewardEffects()
    {
        if (rewardEffects == null)
            return;

        for (int index = 0; index < rewardEffects.Count; index++)
        {
            ParticleSystem effect = rewardEffects[index];
            if (effect != null)
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void KillProgressTween()
    {
        _progressSequence?.Kill();
        _progressSequence = null;
    }

    private void CompleteCloseWait()
    {
        UniTaskCompletionSource completionSource = _closeCompletionSource;
        _closeCompletionSource = null;
        completionSource?.TrySetResult();
    }

    #endregion
}

using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draw HUD 墨水进度显示控制器。
/// 调用边界：由 DrawGame_GamePlayPanel 转发运行时墨水快照和激励奖励表现。
/// 注意事项：150%/165% 仅改变 UI 宽度，墨水容量与奖励数值由 Draw 运行时管理。
/// </summary>
public sealed class DrawGame_InkProcessHandle : MonoBehaviour
{
    #region Constants

    private const float RewardBackgroundWidthMultiplier = 1.55f;
    private const float RewardProcessWidthMultiplier = 1.65f;

    #endregion

    #region Inspector References

    /// <summary>
    /// 激励加墨时进度条宽度与填充量的同步增长动画时长，单位为秒。
    /// 注意事项：设为 0 时动画会在下一次奖励加墨刷新时立即完成。
    /// </summary>
    [SerializeField, Min(0f)] private float inkProcessGrowthDuration = 0.5f;

    /// <summary>
    /// 当前墨水填充图；其 RectTransform 宽度作为星级阈值位置的 100% 基准。
    /// </summary>
    [SerializeField] private Image process;

    /// <summary>
    /// 墨水进度条外层背板；激励视频成功后扩展到基础宽度的 150%。
    /// </summary>
    [SerializeField] private RectTransform processBackground;

    /// <summary>
    /// 墨水进度条底图；激励视频成功后与填充图一起扩展到基础宽度的 165%。
    /// </summary>
    [SerializeField] private RectTransform processBarBackground;

    /// <summary>
    /// 星级阈值节点模板；运行时按当前关卡非零 InkStarStage 数量复用或复制。
    /// </summary>
    [SerializeField] private DrawGame_ProcessStageItem processStageItem;

    #endregion

    #region Runtime State

    private readonly List<DrawGame_ProcessStageItem> _processStageItems = new(3);
    private readonly List<float> _processStageRemainingInkThresholds = new(3);
    private Sequence _rewardInkSequence;
    private DrawInkState _currentInkState;
    private bool _hasInkState;
    private float _baseBackgroundWidth;
    private float _baseProcessBarBackgroundWidth;
    private float _baseProcessWidth;

    #endregion

    #region Public API

    /// <summary>
    /// 开始一次新的 HUD 显示周期，恢复基础宽度并清理旧关卡星级节点。
    /// 调用边界：由 DrawGame_GamePlayPanel.OnOpen 调用一次。
    /// </summary>
    public void ResetPresentation()
    {
        _currentInkState = default;
        _hasInkState = false;
        CacheBaseWidths();
        RestoreBaseWidths();
        ClearStageItems();
    }

    /// <summary>
    /// 立即同步当前选关进入周期内的奖励墨水宽度表现。
    /// 调用边界：由 DrawGame_GamePlayPanel 在 HUD 创建后按 DrawPlayCtrl 的领取状态调用。
    /// 注意事项：只恢复背板与进度条宽度，不播放奖励动画，也不修改运行时墨水容量。
    /// </summary>
    public void SetRewardPresentationExpanded(bool isExpanded)
    {
        if (!isExpanded)
        {
            RestoreBaseWidths();
            return;
        }

        CacheBaseWidths();
        KillTweens();
        SetWidth(
            processBackground,
            _baseBackgroundWidth * RewardBackgroundWidthMultiplier);
        SetWidth(
            processBarBackground,
            _baseProcessBarBackgroundWidth * RewardProcessWidthMultiplier);
        SetWidth(
            process != null ? process.rectTransform : null,
            _baseProcessWidth * RewardProcessWidthMultiplier);
    }

    /// <summary>
    /// 结束当前 HUD 显示周期，停止动画并释放动态星级节点。
    /// 调用边界：由 DrawGame_GamePlayPanel.OnClose 调用；不会修改运行时墨水状态。
    /// </summary>
    public void Release()
    {
        KillTweens();
        ClearStageItems();
    }

    /// <summary>
    /// 按 DrawLineFeature 最终采用的 Stage 剩余墨水量布置星级阈值节点。
    /// 调用边界：节点位置按 Stage / MaxInk 换算；0 基础星和 MaxInk 右端节点不生成。
    /// 注意事项：RewardAD 拉伸 process 时节点保持该基础坐标，不随长状态移动。
    /// </summary>
    public void ConfigureStages(DrawInkState inkState)
    {
        ClearStageItems();
        if (processStageItem == null || process == null)
            return;

        Vector3 stage = inkState.Stage;
        float[] stages =
        {
            stage.x,
            stage.y,
            stage.z,
        };
        CacheBaseWidths();
        float maxInkLength = Mathf.Max(0f, inkState.MaxInk);
        if (maxInkLength <= 0f)
            return;

        float processWidth = Mathf.Max(0f, _baseProcessWidth);
        float currentRemainingInk = Mathf.Max(0f, inkState.RemainingInk);

        for (int i = 0; i < stages.Length; i++)
        {
            float stageRemainingInk = Mathf.Clamp(stages[i], 0f, maxInkLength);
            float normalizedStage = stageRemainingInk / maxInkLength;
            if (normalizedStage <= 0f || normalizedStage >= 1f)
                continue;

            DrawGame_ProcessStageItem stageItem = _processStageItems.Count == 0
                ? processStageItem
                : Instantiate(processStageItem, process.rectTransform);
            stageItem.gameObject.SetActive(true);
            stageItem.Configure(
                process.rectTransform,
                normalizedStage,
                processWidth,
                currentRemainingInk >= stageRemainingInk);
            _processStageItems.Add(stageItem);
            _processStageRemainingInkThresholds.Add(stageRemainingInk);
        }

        if (_processStageItems.Count == 0)
            processStageItem.gameObject.SetActive(false);
    }

    /// <summary>
    /// 根据当前墨水快照刷新填充量和星级达标状态。
    /// 调用边界：animateReward 仅在激励视频成功后传 true，以同步播放宽度和填充动画。
    /// </summary>
    public void SetInkState(DrawInkState inkState, bool animateReward)
    {
        _currentInkState = inkState;
        _hasInkState = true;
        if (process == null)
            return;

        float fillNormalized = Mathf.Clamp01(inkState.RemainingNormalized);
        float remainingInk = Mathf.Max(0f, inkState.RemainingInk);
        process.type = Image.Type.Filled;
        process.fillMethod = Image.FillMethod.Horizontal;
        process.fillOrigin = 0;

        if (animateReward)
        {
            PlayRewardAnimation(fillNormalized, remainingInk);
            return;
        }

        process.DOKill();
        process.fillAmount = fillNormalized;
        RefreshStageStates(remainingInk);
    }

    #endregion

    #region Reward Animation

    private void PlayRewardAnimation(
        float targetFillAmount,
        float targetRemainingInk)
    {
        CacheBaseWidths();
        KillTweens();

        float targetBackgroundWidth =
            _baseBackgroundWidth * RewardBackgroundWidthMultiplier;
        float targetProcessBarBackgroundWidth =
            _baseProcessBarBackgroundWidth * RewardProcessWidthMultiplier;
        float targetProcessWidth =
            _baseProcessWidth * RewardProcessWidthMultiplier;

        _rewardInkSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);

        if (processBackground != null)
        {
            Vector2 targetSize = processBackground.sizeDelta;
            targetSize.x = targetBackgroundWidth;
            _rewardInkSequence.Join(
                processBackground
                    .DOSizeDelta(targetSize, inkProcessGrowthDuration)
                    .SetEase(Ease.OutCubic));
        }

        if (processBarBackground != null)
        {
            Vector2 targetSize = processBarBackground.sizeDelta;
            targetSize.x = targetProcessBarBackgroundWidth;
            _rewardInkSequence.Join(
                processBarBackground
                    .DOSizeDelta(targetSize, inkProcessGrowthDuration)
                    .SetEase(Ease.OutCubic));
        }

        RectTransform processRect = process.rectTransform;
        Vector2 processTargetSize = processRect.sizeDelta;
        processTargetSize.x = targetProcessWidth;
        _rewardInkSequence.Join(
            processRect
                .DOSizeDelta(processTargetSize, inkProcessGrowthDuration)
                .SetEase(Ease.OutCubic));
        _rewardInkSequence.Join(
            process
                .DOFillAmount(targetFillAmount, inkProcessGrowthDuration)
                .SetEase(Ease.OutCubic));

        _rewardInkSequence.OnComplete(() =>
        {
            _rewardInkSequence = null;
            RefreshStageStates(targetRemainingInk);
        });
    }

    #endregion

    #region Stage State

    private void RefreshStageStates(float remainingInk)
    {
        int count = Mathf.Min(
            _processStageItems.Count,
            _processStageRemainingInkThresholds.Count);
        for (int i = 0; i < count; i++)
        {
            _processStageItems[i]?.SetReached(
                remainingInk >= _processStageRemainingInkThresholds[i]);
        }
    }

    private void ClearStageItems()
    {
        for (int i = _processStageItems.Count - 1; i >= 0; i--)
        {
            DrawGame_ProcessStageItem stageItem = _processStageItems[i];
            if (stageItem == null || stageItem == processStageItem)
                continue;

            stageItem.transform.DOKill();
            stageItem.gameObject.SetActive(false);
            Destroy(stageItem.gameObject);
        }

        _processStageItems.Clear();
        _processStageRemainingInkThresholds.Clear();
        if (processStageItem != null)
        {
            processStageItem.transform.DOKill();
            processStageItem.gameObject.SetActive(false);
        }
    }

    #endregion

    #region Width And Tween Lifecycle

    private void CacheBaseWidths()
    {
        if (_baseBackgroundWidth <= 0f && processBackground != null)
            _baseBackgroundWidth = processBackground.sizeDelta.x;
        if (_baseProcessBarBackgroundWidth <= 0f && processBarBackground != null)
            _baseProcessBarBackgroundWidth = processBarBackground.sizeDelta.x;
        if (_baseProcessWidth <= 0f && process != null)
            _baseProcessWidth = process.rectTransform.sizeDelta.x;
    }

    private void RestoreBaseWidths()
    {
        KillTweens();
        SetWidth(processBackground, _baseBackgroundWidth);
        SetWidth(processBarBackground, _baseProcessBarBackgroundWidth);
        SetWidth(process != null ? process.rectTransform : null, _baseProcessWidth);
    }

    private static void SetWidth(RectTransform target, float width)
    {
        if (target == null || width <= 0f)
            return;

        Vector2 size = target.sizeDelta;
        size.x = width;
        target.sizeDelta = size;
    }

    private void KillTweens()
    {
        _rewardInkSequence?.Kill();
        _rewardInkSequence = null;
        process?.DOKill();
        process?.rectTransform.DOKill();
        processBackground?.DOKill();
        processBarBackground?.DOKill();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    #endregion
}

using Bear.Logger;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 每日挑战复用入口组件。
/// 入口只展示 Manager 提供的状态并控制进度气泡，不负责奖励发放或打开 DailyChallengePopup。
/// </summary>
public partial class DailyChallengeBtn : MonoBehaviour, IDebuger
{
    #region Constants

    private const float BubbleShowDuration = 0.2f;

    #endregion

    #region Serialized Fields

    [SerializeField, Tooltip("每日挑战入口的可见内容根节点；功能关闭或未解锁时仅隐藏该节点。")]
    private GameObject contentRoot;

    [SerializeField, Tooltip("点击入口后展开或收起的阶段进度气泡。")]
    private GameObject processBubble;

    [SerializeField, Tooltip("入口收起状态下显示双阶段分段累计进度的前景图片。")]
    private Image compactProgressFill;

    /// <summary>
    /// 气泡展开后第一阶段的独立进度前景；仅显示第一阶段内进度，达到第一阶段后保持填满。
    /// </summary>
    [SerializeField, Tooltip("气泡展开后的第一阶段独立进度前景；必须在 DailyChallengeBtn prefab 中绑定。")]
    private Image firstStageProgressFill;

    /// <summary>
    /// 气泡展开后第二阶段的独立进度前景；进入第二阶段前保持为空。
    /// </summary>
    [SerializeField, Tooltip("气泡展开后的第二阶段独立进度前景；必须在 DailyChallengeBtn prefab 中绑定。")]
    private Image secondStageProgressFill;

    [SerializeField, Tooltip("第一阶段奖励的复用展示节点；未绑定时会从进度气泡内按横向位置自动识别。")]
    private DailyChallengeStepItem firstStageItem;

    [SerializeField, Tooltip("第二阶段奖励的复用展示节点；未绑定时会从进度气泡内按横向位置自动识别。")]
    private DailyChallengeStepItem secondStageItem;

    #endregion

    #region Runtime State

    private bool _isExpanded;
    private bool _hasLoggedMissingStepItems;
    private Tween _bubbleScaleTween;

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        BindButton();
        SetExpanded(false);
        Refresh();
    }

    private void OnDisable()
    {
        KillBubbleScaleTween();
        UnbindButton();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 刷新每日挑战入口显示。
    /// 调用边界：宿主界面重新显示、每日挑战进度或领奖状态变化后调用。
    /// 注意事项：编辑器非运行态不会读取 Manager、配置或 DB。
    /// </summary>
    public void Refresh()
    {
        if (!Application.isPlaying)
            return;

        DailyChallengeManager manager = GameManager.Instance?.DailyChallenge;
        if (manager == null)
        {
            SetContentVisible(false);
            this.LogWarning("[DailyChallengeBtn] Refresh skipped because DailyChallengeManager is missing.");
            return;
        }

        ApplyViewState(manager.GetViewState());
    }

    /// <summary>
    /// 尝试在宿主指定的表现时机展开每日挑战气泡。
    /// 调用边界：仅供 VictoryPanel 在明信片星星动画结束后调用；普通点击仍由入口自身切换展开状态。
    /// 注意事项：功能不可见或当天挑战已全部完成时保持收起，不读取编辑器非运行态数据。
    /// </summary>
    /// <returns>本次是否满足条件并展开气泡。</returns>
    public bool TryExpandIfIncomplete()
    {
        if (!Application.isPlaying)
            return false;

        DailyChallengeManager manager = GameManager.Instance?.DailyChallenge;
        if (manager == null)
        {
            SetContentVisible(false);
            this.LogWarning("[DailyChallengeBtn] Auto expand skipped because DailyChallengeManager is missing.");
            return false;
        }

        DailyChallengeViewState state = manager.GetViewState();
        ApplyViewState(state);

        bool shouldExpand = state != null && state.IsVisible && !state.IsCompleted;
        SetExpanded(shouldExpand);
        return shouldExpand;
    }

    #endregion

    #region View Refresh

    private void SetExpanded(bool expanded)
    {
        _isExpanded = expanded;
        if (processBubble == null)
            return;

        KillBubbleScaleTween();
        if (!expanded)
        {
            processBubble.SetActive(false);
            return;
        }

        processBubble.SetActive(true);
        Transform bubbleTransform = processBubble.transform;
        Vector3 bubbleScale = bubbleTransform.localScale;
        bubbleScale.x = 0f;
        bubbleTransform.localScale = bubbleScale;
        _bubbleScaleTween = bubbleTransform
            .DOScaleX(1f, BubbleShowDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(processBubble);
    }

    private void ApplyViewState(DailyChallengeViewState state)
    {
        bool isVisible = state != null && state.IsVisible;
        SetContentVisible(isVisible);
        if (!isVisible)
            return;

        UiShengli211Txt?.SetText(state.TotalProgress.ToString());
        ResolveStepItems();
        firstStageItem?.Refresh(state.FirstStageTarget, state.IsFirstRewardClaimed);
        secondStageItem?.Refresh(state.SecondStageTarget, state.IsSecondRewardClaimed);

        SetProgress(compactProgressFill, state.ExpandedNormalizedProgress);
        SetProgress(
            firstStageProgressFill,
            state.IsFirstStageReached ? 1f : state.CurrentStageNormalizedProgress);
        SetProgress(
            secondStageProgressFill,
            state.IsFirstStageReached ? state.CurrentStageNormalizedProgress : 0f);

        if (UIDailyGou012Img != null)
            UIDailyGou012Img.gameObject.SetActive(state.IsCompleted);
    }

    private void SetContentVisible(bool visible)
    {
        if (contentRoot != null)
            contentRoot.SetActive(visible);

        if (!visible)
            SetExpanded(false);
    }

    private static void SetProgress(Image image, float progress)
    {
        if (image == null)
            return;

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = 0;
        image.fillAmount = Mathf.Clamp01(progress);
    }

    private void ResolveStepItems()
    {
        if (firstStageItem != null && secondStageItem != null)
            return;

        DailyChallengeStepItem[] items = processBubble != null
            ? processBubble.GetComponentsInChildren<DailyChallengeStepItem>(true)
            : GetComponentsInChildren<DailyChallengeStepItem>(true);

        if (items.Length >= 2)
        {
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
            return;
        }

        if (!_hasLoggedMissingStepItems)
        {
            _hasLoggedMissingStepItems = true;
            this.LogWarning("[DailyChallengeBtn] Two DailyChallengeStepItem children are required.");
        }
    }

    private void KillBubbleScaleTween()
    {
        _bubbleScaleTween?.Kill();
        _bubbleScaleTween = null;
        if (processBubble != null)
        {
            Transform bubbleTransform = processBubble.transform;
            Vector3 bubbleScale = bubbleTransform.localScale;
            bubbleScale.x = 1f;
            bubbleTransform.localScale = bubbleScale;
        }
    }

    #endregion

    #region Button Binding

    private void BindButton()
    {
        if (UIXuanguanFang011Btn == null)
        {
            this.LogWarning("[DailyChallengeBtn] Click area is not bound.");
            return;
        }

        UIXuanguanFang011Btn.OnClick -= OnEntryClick;
        UIXuanguanFang011Btn.OnClick += OnEntryClick;
    }

    private void UnbindButton()
    {
        if (UIXuanguanFang011Btn != null)
            UIXuanguanFang011Btn.OnClick -= OnEntryClick;
    }

    private void OnEntryClick(CustomButton _)
    {
        SetExpanded(!_isExpanded);
    }

    #endregion
}

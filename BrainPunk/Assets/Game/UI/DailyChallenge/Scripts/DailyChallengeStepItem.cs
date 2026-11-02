using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 每日挑战阶段奖励的复用展示组件。
/// 只负责刷新阶段目标数字和已领奖状态，不读取配置、存档或 Manager。
/// </summary>
[DisallowMultipleComponent]
public sealed class DailyChallengeStepItem : MonoBehaviour
{
    #region Constants

    private const float RewardScaleMultiplier = 1.25f;
    private const float RewardReboundScaleMultiplier = 0.92f;
    private const float RewardShakeRotation = 12f;
    private const int RewardShakeVibrato = 20;
    private const float RewardShakeRandomness = 45f;
    private const float ClaimedIconGrayValue = 0.7f;

    #endregion

    #region Serialized Fields

    [SerializeField, Tooltip("阶段完成所需的挑战次数文本。")]
    private TMP_Text targetCountText;

    [SerializeField, Tooltip("阶段目标区域底板；包含目标次数文本，刷新时始终保持显示。")]
    private GameObject claimedBackground;

    [SerializeField, Tooltip("阶段奖励已领取时显示的勾选节点。")]
    private GameObject claimedCheck;

    /// <summary>
    /// 领奖时执行抖动和弹性缩放的主图标；必须在 DailyChallengeStepItem prefab 中直接绑定。
    /// </summary>
    [SerializeField, Tooltip("领奖动画目标；必须在 DailyChallengeStepItem prefab 中直接绑定。")]
    private RectTransform mainIcon;

    #endregion

    #region Runtime State

    private Tween _claimedTween;
    private Image _mainIconImage;
    private Color _mainIconDefaultColor;
    private bool _hasMainIconDefaultColor;

    #endregion

    #region Public API

    /// <summary>
    /// 刷新单个阶段奖励节点。
    /// 调用边界：由 DailyChallengeBtn、DailyChallengePopup 等宿主拿到阶段状态后调用。
    /// 注意事项：传入目标次数小于零时按零显示；该组件不判断是否达到领取条件。
    /// </summary>
    public void Refresh(int targetCount, bool isRewardClaimed)
    {
        KillClaimedTween();
        SetActive(targetCountText != null ? targetCountText.gameObject : null, true);
        targetCountText?.SetText(Mathf.Max(0, targetCount).ToString());
        SetActive(claimedBackground, true);
        SetActive(claimedCheck, isRewardClaimed);
        ApplyClaimedIconColor(isRewardClaimed);
        ResetClaimedTransform();
    }

    /// <summary>
    /// 获取领奖特效应播放的世界坐标。
    /// 调用边界：仅供持有该阶段节点的弹窗在播放领奖特效前读取。
    /// 注意事项：mainIcon 未绑定时回退到阶段节点坐标，避免中断奖励弹窗流程。
    /// </summary>
    public Vector3 RewardEffectWorldPosition => mainIcon != null ? mainIcon.position : transform.position;

    /// <summary>
    /// 显示阶段已领取状态，并让 mainIcon 同时播放旋转抖动与弹性放大缩小动画。
    /// 调用边界：仅由宿主在本次新达成阶段时调用；普通状态恢复继续使用 Refresh。
    /// 注意事项：动画使用 unscaled time，结束或中断时恢复动画开始前的旋转和缩放。
    /// </summary>
    /// <param name="duration">缩放动画时长，负值按零处理。</param>
    /// <returns>可供宿主等待完成的 Tween；mainIcon 未绑定时返回 null。</returns>
    public Tween PlayClaimedAnimation(float duration)
    {
        KillClaimedTween();
        SetActive(targetCountText != null ? targetCountText.gameObject : null, true);
        SetActive(claimedBackground, true);
        SetActive(claimedCheck, true);
        ApplyClaimedIconColor(true);
        if (mainIcon == null)
            return null;

        float safeDuration = Mathf.Max(0.01f, duration);
        Vector3 originalScale = mainIcon.localScale;
        Quaternion originalRotation = mainIcon.localRotation;
        Vector3 overScale = originalScale * RewardScaleMultiplier;
        Vector3 reboundScale = originalScale * RewardReboundScaleMultiplier;

        Sequence scaleSequence = DOTween.Sequence()
            .Append(mainIcon.DOScale(overScale, safeDuration * 0.42f).SetEase(Ease.OutBack))
            .Append(mainIcon.DOScale(reboundScale, safeDuration * 0.23f).SetEase(Ease.InOutSine))
            .Append(mainIcon.DOScale(originalScale, safeDuration * 0.35f).SetEase(Ease.OutBack));

        Sequence sequence = DOTween.Sequence()
            .Join(mainIcon.DOShakeRotation(
                safeDuration,
                new Vector3(0f, 0f, RewardShakeRotation),
                RewardShakeVibrato,
                RewardShakeRandomness,
                true))
            .Join(scaleSequence)
            .SetUpdate(true)
            .SetLink(gameObject);

        sequence.OnKill(() =>
        {
            if (mainIcon != null)
            {
                mainIcon.localScale = originalScale;
                mainIcon.localRotation = originalRotation;
            }

            if (_claimedTween == sequence)
                _claimedTween = null;
        });
        _claimedTween = sequence;
        return sequence;
    }

    #endregion

    #region Lifecycle

    private void OnDisable()
    {
        KillClaimedTween();
        ResetClaimedTransform();
    }

    #endregion

    #region View Helpers

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    private void ApplyClaimedIconColor(bool isRewardClaimed)
    {
        if (mainIcon == null)
            return;

        if (_mainIconImage == null)
            _mainIconImage = mainIcon.GetComponent<Image>();
        if (_mainIconImage == null)
            return;

        if (!_hasMainIconDefaultColor)
        {
            _mainIconDefaultColor = _mainIconImage.color;
            _hasMainIconDefaultColor = true;
        }

        _mainIconImage.color = isRewardClaimed
            ? new Color(ClaimedIconGrayValue, ClaimedIconGrayValue, ClaimedIconGrayValue, _mainIconDefaultColor.a)
            : _mainIconDefaultColor;
    }

    private void ResetClaimedTransform()
    {
        if (mainIcon != null)
        {
            mainIcon.localScale = Vector3.one;
            mainIcon.localRotation = Quaternion.identity;
            return;
        }

        if (claimedCheck != null)
            claimedCheck.transform.localScale = Vector3.one;
    }

    private void KillClaimedTween()
    {
        _claimedTween?.Kill();
        _claimedTween = null;
    }

    #endregion
}

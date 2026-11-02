using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Graphic 延迟显示组件，仅负责按配置延迟后渐显目标 Graphic。
/// </summary>
[RequireComponent(typeof(Graphic))]
public class DelayDisplay : MonoBehaviour
{
    #region Serialized Fields

    /// <summary>
    /// 显示目标 Graphic；为空时使用当前节点上的 Graphic，不负责子节点或 CanvasGroup。
    /// </summary>
    [Header("显示设置")]
    [Tooltip("显示目标 Graphic；为空时使用当前节点上的 Graphic，不负责子节点或 CanvasGroup。")]
    [SerializeField] private Graphic targetGraphic;

    /// <summary>
    /// 调用 Show 后等待多久开始渐显，单位秒；重复调用会重新计时。
    /// </summary>
    [Tooltip("调用 Show 后等待多久开始渐显，单位秒；重复调用会重新计时。")]
    [SerializeField, Min(0f)] private float displayDelay = 2f;

    /// <summary>
    /// 从透明渐显到目标透明度的时长，单位秒；为 0 时立即显示。
    /// </summary>
    [Tooltip("从透明渐显到目标透明度的时长，单位秒；为 0 时立即显示。")]
    [SerializeField, Min(0f)] private float fadeInDuration = 1f;

    /// <summary>
    /// 渐显完成后的目标透明度；默认 1 表示完全显示。
    /// </summary>
    [Tooltip("渐显完成后的目标透明度；默认 1 表示完全显示。")]
    [SerializeField, Range(0f, 1f)] private float displayAlpha = 1f;

    /// <summary>
    /// 启用组件时是否立即隐藏目标；用于避免打开 UI 的首帧露出。
    /// </summary>
    [Tooltip("启用组件时是否立即隐藏目标；用于避免打开 UI 的首帧露出。")]
    [SerializeField] private bool hideOnEnable = true;

    /// <summary>
    /// 是否使用不受 Time.timeScale 影响的 UI 时间；暂停界面或弹窗显示建议保持开启。
    /// </summary>
    [Tooltip("是否使用不受 Time.timeScale 影响的 UI 时间；暂停界面或弹窗显示建议保持开启。")]
    [SerializeField] private bool ignoreTimeScale = true;

    [SerializeField] private bool IsAutoPlay = true;

    #endregion

    #region Runtime State

    private Sequence showSequence;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();

        if (hideOnEnable)
            HideImmediate();
        if (IsAutoPlay)
            Show();
    }

    private void OnDisable()
    {
        StopShowTween();
    }

    private void OnDestroy()
    {
        StopShowTween();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 按配置延迟后淡入显示目标 Graphic；调用方需保证目标 GameObject 所在层级处于 active 状态。
    /// </summary>
    public void Show()
    {
        StopShowTween();
        CacheReferences();

        if (targetGraphic == null)
        {
            Debug.LogWarning("[DelayDisplay] Show skipped: target Graphic is missing.", this);
            return;
        }

        SetTargetVisible(false);
        SetTargetAlpha(0f);

        showSequence = DOTween.Sequence()
            .SetUpdate(ignoreTimeScale)
            .SetLink(gameObject);

        float delay = Mathf.Max(0f, displayDelay);
        if (delay > 0f)
            showSequence.AppendInterval(delay);

        showSequence.AppendCallback(() =>
        {
            if (targetGraphic == null)
                return;

            SetTargetVisible(true);
            SetTargetAlpha(0f);
        });

        float duration = Mathf.Max(0f, fadeInDuration);
        float targetAlpha = Mathf.Clamp01(displayAlpha);
        if (duration > 0f)
        {
            showSequence.Append(targetGraphic
                .DOFade(targetAlpha, duration)
                .SetEase(Ease.Linear));
        }
        else
        {
            showSequence.AppendCallback(() => SetTargetAlpha(targetAlpha));
        }

        showSequence.OnComplete(() =>
        {
            showSequence = null;

            if (targetGraphic == null)
                return;

            SetTargetVisible(true);
            SetTargetAlpha(targetAlpha);
        });
    }

    /// <summary>
    /// 立即中断延迟/渐显流程并隐藏目标 Graphic；不会修改目标 GameObject 的 active 状态。
    /// </summary>
    public void HideImmediate()
    {
        StopShowTween();
        CacheReferences();

        if (targetGraphic == null)
            return;

        SetTargetAlpha(0f);
        SetTargetVisible(false);
    }

    #endregion

    #region Reference And Visual Helpers

    private void CacheReferences()
    {
        if (targetGraphic == null)
            targetGraphic = GetComponent<Graphic>();
    }

    private void SetTargetAlpha(float alpha)
    {
        if (targetGraphic == null)
            return;

        Color color = targetGraphic.color;
        color.a = Mathf.Clamp01(alpha);
        targetGraphic.color = color;
    }

    private void SetTargetVisible(bool visible)
    {
        if (targetGraphic == null)
            return;

        targetGraphic.enabled = visible;
    }

    private void StopShowTween()
    {
        showSequence?.Kill();
        showSequence = null;
    }

    #endregion
}

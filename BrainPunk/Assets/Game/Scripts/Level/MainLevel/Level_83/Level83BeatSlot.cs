using System;
using DG.Tweening;
using Game.Level083;
using UnityEngine;

/// <summary>
/// Level 083 单个预置谱面槽，拥有方向、白/绿/红语义和局部反馈 Tween，不在轮次中创建或销毁对象。
/// </summary>
public sealed class Level83BeatSlot : MonoBehaviour
{
    private const float ShowDuration = 0.3f;
    private const float AwaitingScaleMultiplier = 1.2f;
    private const float AwaitingScaleDuration = 0.4f;
    private const float CorrectPeakScaleMultiplier = 1.2f;
    private const float CorrectUndershootScaleMultiplier = 0.96f;
    private const float IncorrectScaleMultiplier = 0.92f;
    private const float IncorrectFeedbackDuration = 0.3f;
    private const float IncorrectOffsetWidthRatio = 0.08f;
    private const int IncorrectShakeCount = 3;

    #region Inspector

    /// <summary>显示当前方向与状态的 SpriteRenderer。</summary>
    [SerializeField] private SpriteRenderer iconRenderer;

    /// <summary>未输入状态使用的白色箭头。</summary>
    [SerializeField] private Sprite idleSprite;

    /// <summary>正确状态使用的绿色箭头。</summary>
    [SerializeField] private Sprite correctSprite;

    /// <summary>错误状态使用的红色箭头。</summary>
    [SerializeField] private Sprite incorrectSprite;

    #endregion

    #region Runtime State

    private Vector3 originalLocalScale;
    private Vector3 originalLocalPosition;
    private Quaternion originalIconLocalRotation;
    private Tween feedbackTween;
    private bool hasCapturedOriginalTransform;

    #endregion

    #region Unity Messages

    private void Awake()
    {
        CaptureOriginalTransform();
    }

    private void OnDisable()
    {
        RestoreOriginalTransform();
    }

    private void OnDestroy()
    {
        RestoreOriginalTransform();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 设置该槽位的输入方向，并恢复为白色未输入状态。
    /// </summary>
    public void Configure(Level83PerformanceInput input)
    {
        if (iconRenderer == null)
            return;

        iconRenderer.flipX = false;
        iconRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, GetRotation(input));
        SetIdle();
    }

    /// <summary>
    /// 从缩放 0 以 OutBack 弹出到原始缩放；延迟由编排器用于从左到右逐格错峰，
    /// 全部弹出完成前谱面输入不生效。
    /// </summary>
    public void PlayShow(float delay, Action onCompleted = null)
    {
        CaptureOriginalTransform();
        feedbackTween?.Kill();
        feedbackTween = null;
        transform.localPosition = originalLocalPosition;
        transform.localScale = Vector3.zero;
        feedbackTween = transform
            .DOScale(originalLocalScale, ShowDuration)
            .SetEase(Ease.OutBack)
            .SetDelay(delay)
            .OnComplete(() =>
            {
                feedbackTween = null;
                onCompleted?.Invoke();
            });
    }

    /// <summary>
    /// PlayShow 的倒放镜像：保持当前方向与 Sprite 原样（完成时为全绿），以 InBack 缩小至 0；
    /// 延迟由编排器用于从右到左反向错峰。不退回、不复白、不重置槽内箭头。
    /// </summary>
    public void PlayHide(float delay, Action onCompleted = null)
    {
        CaptureOriginalTransform();
        feedbackTween?.Kill();
        feedbackTween = null;
        transform.localPosition = originalLocalPosition;
        transform.localScale = originalLocalScale;
        feedbackTween = transform
            .DOScale(Vector3.zero, ShowDuration)
            .SetEase(Ease.InBack)
            .SetDelay(delay)
            .OnComplete(() =>
            {
                feedbackTween = null;
                onCompleted?.Invoke();
            });
    }

    /// <summary>把该槽位恢复为白色未输入状态。</summary>
    public void SetIdle()
    {
        StopFeedback();
        SetSprite(idleSprite);
    }

    /// <summary>把该槽位切换为白色待输入状态，并循环播放轻微呼吸缩放。</summary>
    public void SetAwaiting()
    {
        StopFeedback();
        SetSprite(idleSprite);
        feedbackTween = transform
            .DOScale(originalLocalScale * AwaitingScaleMultiplier, AwaitingScaleDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    /// <summary>停止当前槽位反馈，并恢复原始缩放。</summary>
    public void StopFeedback()
    {
        CaptureOriginalTransform();
        feedbackTween?.Kill();
        feedbackTween = null;
        transform.localPosition = originalLocalPosition;
        transform.localScale = originalLocalScale;
    }

    /// <summary>停止当前反馈，并恢复该槽位及图标的 Prefab 原始变换。</summary>
    public void RestoreOriginalTransform()
    {
        StopFeedback();
        if (iconRenderer != null)
            iconRenderer.transform.localRotation = originalIconLocalRotation;
    }

    /// <summary>把该槽位切换为绿色正确状态。</summary>
    public void SetCorrect()
    {
        StopFeedback();
        SetSprite(correctSprite);
        feedbackTween = DOTween.Sequence()
            .Append(transform.DOScale(originalLocalScale * CorrectPeakScaleMultiplier, 0.08f)
                .SetEase(Ease.OutQuad))
            .Append(transform.DOScale(originalLocalScale * CorrectUndershootScaleMultiplier, 0.06f)
                .SetEase(Ease.InOutSine))
            .Append(transform.DOScale(originalLocalScale, 0.08f)
                .SetEase(Ease.OutQuad));
    }

    /// <summary>把该槽位切换为红色错误状态。</summary>
    public void SetIncorrect()
    {
        StopFeedback();
        SetSprite(incorrectSprite);

        float iconWidth = iconRenderer != null && iconRenderer.sprite != null
            ? iconRenderer.sprite.bounds.size.x * Mathf.Abs(originalLocalScale.x)
            : 1f;
        float horizontalOffset = iconWidth * IncorrectOffsetWidthRatio;
        float shakeSegmentDuration = IncorrectFeedbackDuration / (IncorrectShakeCount * 3f);
        Sequence shakeSequence = DOTween.Sequence();
        for (int index = 0; index < IncorrectShakeCount; index++)
        {
            shakeSequence
                .Append(transform.DOLocalMoveX(originalLocalPosition.x + horizontalOffset, shakeSegmentDuration)
                    .SetEase(Ease.InOutSine))
                .Append(transform.DOLocalMoveX(originalLocalPosition.x - horizontalOffset, shakeSegmentDuration)
                    .SetEase(Ease.InOutSine))
                .Append(transform.DOLocalMoveX(originalLocalPosition.x, shakeSegmentDuration)
                    .SetEase(Ease.InOutSine));
        }

        Sequence scaleSequence = DOTween.Sequence()
            .Append(transform.DOScale(originalLocalScale * IncorrectScaleMultiplier, IncorrectFeedbackDuration * 0.5f)
                .SetEase(Ease.InOutSine))
            .Append(transform.DOScale(originalLocalScale, IncorrectFeedbackDuration * 0.5f)
                .SetEase(Ease.InOutSine));
        feedbackTween = DOTween.Sequence()
            .Append(shakeSequence)
            .Join(scaleSequence);
    }

    #endregion

    #region Helpers

    private void CaptureOriginalTransform()
    {
        if (hasCapturedOriginalTransform)
            return;

        originalLocalScale = transform.localScale;
        originalLocalPosition = transform.localPosition;
        originalIconLocalRotation = iconRenderer != null
            ? iconRenderer.transform.localRotation
            : Quaternion.identity;
        hasCapturedOriginalTransform = true;
    }

    private void SetSprite(Sprite sprite)
    {
        if (iconRenderer != null && sprite != null)
            iconRenderer.sprite = sprite;
    }

    private static float GetRotation(Level83PerformanceInput input)
    {
        return input switch
        {
            Level83PerformanceInput.Left => 90f,
            Level83PerformanceInput.Right => -90f,
            _ => 0f
        };
    }

    #endregion
}

using System;
using UnityEngine;
using DG.Tweening;
using Bear.UI;

/// <summary>
/// UI 淡入淡出动画组件。
/// 使用 CanvasGroup.alpha，需挂载在有 CanvasGroup 的物体上或指定目标。
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class UIFadeAnimation : MonoBehaviour, IUIAnimation
{
    [SerializeField] private CanvasGroup _targetCanvasGroup;

    [Header("动画配置")]
    [SerializeField] private float _duration = 0.3f;
    [SerializeField][Range(0f, 1f)] private float _alphaFrom = 0f;
    [SerializeField] private Ease _easeType = Ease.OutQuad;

    [SerializeField] private bool _playOpenAnim = true;
    [SerializeField] private bool _playCloseAnim = true;

    [SerializeField] private bool _ignoreTimeScale = true;

    private Tween _currentTween;
    private float _originalAlpha = 1f;

    /// <summary>
    /// 动画目标 CanvasGroup
    /// </summary>
    public CanvasGroup TargetCanvasGroup
    {
        get => _targetCanvasGroup;
        set
        {
            _targetCanvasGroup = value;
            if (_targetCanvasGroup != null)
                _originalAlpha = _targetCanvasGroup.alpha;
        }
    }

    public float Duration { get => _duration; set => _duration = value; }
    public float AlphaFrom { get => _alphaFrom; set => _alphaFrom = value; }
    public Ease EaseType { get => _easeType; set => _easeType = value; }

    private void Awake()
    {
        InitializeTarget();
    }

    private void InitializeTarget()
    {
        if (_targetCanvasGroup == null)
            _targetCanvasGroup = GetComponent<CanvasGroup>();

        if (_targetCanvasGroup != null)
            _originalAlpha = _targetCanvasGroup.alpha;
    }

    /// <inheritdoc />
    public bool PlayOpenAnimation(Action onComplete = null)
    {
        if (_targetCanvasGroup == null || !_playOpenAnim)
        {
            _targetCanvasGroup.alpha = _originalAlpha;
            onComplete?.Invoke();
            return true;
        }

        KillCurrentTween();

        _targetCanvasGroup.alpha = _alphaFrom;
        _currentTween = _targetCanvasGroup.DOFade(_originalAlpha, _duration)
            .SetEase(_easeType)
            .SetUpdate(_ignoreTimeScale)
            .OnComplete(() => onComplete?.Invoke());

        return false;
    }

    /// <inheritdoc />
    public bool PlayCloseAnimation(Action onComplete = null)
    {
        if (_targetCanvasGroup == null || !_playCloseAnim)
        {
            onComplete?.Invoke();
            return true;
        }

        KillCurrentTween();

        _currentTween = _targetCanvasGroup.DOFade(_alphaFrom, _duration)
            .SetEase(_easeType)
            .SetUpdate(_ignoreTimeScale)
            .OnComplete(() => onComplete?.Invoke());

        return false;
    }

    private void KillCurrentTween()
    {
        if (_currentTween != null && _currentTween.IsActive())
        {
            _currentTween.Kill();
            _currentTween = null;
        }
    }

    /// <summary>
    /// 立即完成当前动画
    /// </summary>
    public void CompleteAnimation()
    {
        if (_currentTween != null && _currentTween.IsActive())
        {
            _currentTween.Complete();
        }

        if (_targetCanvasGroup != null)
        {
            _targetCanvasGroup.alpha = _originalAlpha;
        }
    }

    /// <summary>
    /// 停止当前动画
    /// </summary>
    public void StopAnimation()
    {
        KillCurrentTween();
    }

    private void OnDestroy()
    {
        KillCurrentTween();
    }

    public void ResetAnim(Action onComplete = null)
    {
        StopAnimation();
    }
}

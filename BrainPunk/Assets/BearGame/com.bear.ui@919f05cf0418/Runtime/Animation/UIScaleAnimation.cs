using System;
using UnityEngine;
using DG.Tweening;

namespace Bear.UI
{
    /// <summary>
    /// UI 缩放动画组件
    /// 继承 MonoBehaviour，可在 Inspector 中配置
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIScaleAnimation : MonoBehaviour, IUIAnimation
    {
        [SerializeField] private RectTransform _targetRectTransform;

        [Header("动画配置")]
        [SerializeField] private float _duration = 0.3f;
        [SerializeField] private Vector3 _scaleFrom = new Vector3(0.8f, 0.8f, 1f);
        [SerializeField] private DG.Tweening.Ease _easeType = DG.Tweening.Ease.OutBack;

        [SerializeField] private bool _playOpenAnim = true;
        [SerializeField] private bool _playCloseAnim = true;

        [SerializeField] private bool _ignoreTimeScale = true;


        private Tween _currentTween;
        private Vector3 _originalScale = Vector3.one;

        /// <summary>
        /// 动画目标 RectTransform
        /// </summary>
        public RectTransform TargetRectTransform
        {
            get => _targetRectTransform;
            set => _targetRectTransform = value;
        }

        /// <summary>
        /// 动画持续时间
        /// </summary>
        public float Duration
        {
            get => _duration;
            set => _duration = value;
        }

        /// <summary>
        /// 缩放起始值
        /// </summary>
        public Vector3 ScaleFrom
        {
            get => _scaleFrom;
            set => _scaleFrom = value;
        }

        /// <summary>
        /// 缓动类型
        /// </summary>
        public DG.Tweening.Ease EaseType
        {
            get => _easeType;
            set => _easeType = value;
        }

        private void Awake()
        {
            InitializeTarget();
        }

        /// <summary>
        /// 初始化动画目标
        /// </summary>
        private void InitializeTarget()
        {
            // 如果没有指定目标 RectTransform，使用当前对象的
            if (_targetRectTransform == null)
            {
                _targetRectTransform = GetComponent<RectTransform>();
            }

            if (_targetRectTransform != null)
            {
                _originalScale = _targetRectTransform.localScale;
            }
        }

        /// <summary>
        /// 播放打开动画
        /// </summary>
        /// <param name="onComplete">动画完成回调</param>
        /// <returns>动画是否立即完成</returns>
        public bool PlayOpenAnimation(Action onComplete = null)
        {
            if (_targetRectTransform == null || !_playOpenAnim)
            {
                onComplete?.Invoke();
                return true;
            }

            KillCurrentTween();

            _targetRectTransform.localScale = _scaleFrom;
            _currentTween = _targetRectTransform.DOScale(_originalScale, _duration)
                .SetEase(_easeType)
                .SetUpdate(_ignoreTimeScale)
                .OnComplete(() => onComplete?.Invoke());

            return false;
        }

        /// <summary>
        /// 播放关闭动画
        /// </summary>
        /// <param name="onComplete">动画完成回调</param>
        /// <returns>动画是否立即完成</returns>
        public bool PlayCloseAnimation(Action onComplete = null)
        {
            if (_targetRectTransform == null || !_playCloseAnim)
            {
                onComplete?.Invoke();
                return true;
            }

            KillCurrentTween();

            _currentTween = _targetRectTransform.DOScale(_scaleFrom, _duration)
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

            _targetRectTransform.localScale = _originalScale;
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
            KillCurrentTween();
        }
    }
}


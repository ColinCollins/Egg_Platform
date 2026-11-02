using UnityEngine;
using UnityEngine.UI;

namespace Bear.UI
{
    /// <summary>
    /// UI 遮罩管理
    /// </summary>
    public class UIMask : MonoBehaviour
    {
        private Image _maskImage;
        private Button _maskButton;
        private System.Action _onMaskClick;

        private bool _isFading;
        private bool _isShowing;
        private float _fadeTime;
        private float _fadeDuration;
        private float _targetAlpha;
        private Color _baseColor;

        /// <summary>
        /// 当前遮罩是否处于显示状态（包括淡入中 / 已完全显示）
        /// </summary>
        public bool IsVisible => gameObject.activeSelf && _isShowing;

        private void Awake()
        {
            _maskImage = GetComponent<Image>();
            if (_maskImage == null)
            {
                _maskImage = gameObject.AddComponent<Image>();
            }

            _maskButton = GetComponent<Button>();
            if (_maskButton == null)
            {
                _maskButton = gameObject.AddComponent<Button>();
            }

            _maskButton.onClick.AddListener(OnMaskClicked);
        }

        private void OnEnable()
        {
            // 仅在外部直接 SetActive(true) 且当前未处于显示状态时，自动触发一次 Show。
            // 通过 Show() 内部调用 SetActive(true) 时，_isShowing 已经被置为 true，不会重复触发。
            if (_maskImage != null && !_isShowing)
            {
                Color current = _maskImage.color;
                float alpha = current.a > 0f ? current.a : (UIManager.Instance != null ? UIManager.Instance.MaskAlpha : 0.5f);
                Show(current, alpha);
            }
        }

        private void Update()
        {
            if (!_isFading || _maskImage == null)
            {
                return;
            }

            _fadeTime += Time.unscaledDeltaTime;
            float duration = _fadeDuration > 0f ? _fadeDuration : 0.0001f;
            float t = Mathf.Clamp01(_fadeTime / duration);

            float fromAlpha = _isShowing ? 0f : _targetAlpha;
            float toAlpha = _isShowing ? _targetAlpha : 0f;
            float alpha = Mathf.Lerp(fromAlpha, toAlpha, t);

            Color c = _baseColor;
            c.a = alpha;
            _maskImage.color = c;

            if (t >= 1f)
            {
                _isFading = false;
                if (!_isShowing)
                {
                    gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 显示遮罩
        /// </summary>
        /// <param name="color">遮罩颜色</param>
        /// <param name="alpha">透明度（0-1）</param>
        public void Show(Color color, float alpha = 0.5f)
        {
            if (_maskImage == null)
            {
                return;
            }

            _baseColor = color;
            _targetAlpha = alpha;

            float duration = UIManager.Instance != null ? UIManager.Instance.MaskFadeDuration : 0f;
            _fadeDuration = duration;
            _fadeTime = 0f;
            _isShowing = true;

            gameObject.SetActive(true);

            if (duration <= 0f)
            {
                color.a = alpha;
                _maskImage.color = color;
                _isFading = false;
            }
            else
            {
                color.a = 0f;
                _maskImage.color = color;
                _isFading = true;
            }
        }

        /// <summary>
        /// 隐藏遮罩
        /// </summary>
        public void Hide()
        {
            if (_maskImage == null)
            {
                gameObject.SetActive(false);
                return;
            }

            float duration = UIManager.Instance != null ? UIManager.Instance.MaskFadeDuration : 0f;
            _fadeDuration = duration;
            _fadeTime = 0f;
            _isShowing = false;

            if (duration <= 0f)
            {
                gameObject.SetActive(false);
                _isFading = false;
                return;
            }

            _baseColor = _maskImage.color;
            _targetAlpha = _baseColor.a;
            _isFading = true;
        }

        /// <summary>
        /// 设置是否可点击关闭
        /// </summary>
        /// <param name="clickable">是否可点击</param>
        /// <param name="onClick">点击回调</param>
        public void SetClickable(bool clickable, System.Action onClick = null)
        {
            if (_maskButton != null)
            {
                _maskButton.interactable = clickable;
                _onMaskClick = onClick;
            }
        }

        private void OnMaskClicked()
        {
            _onMaskClick?.Invoke();
        }
    }
}


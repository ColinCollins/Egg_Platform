using DG.Tweening;
using UnityEngine;

namespace Game.ItemEvent
{
    /// <summary>
    /// 对 SpriteRenderer 执行透明度渐变。
    /// </summary>
    public class SpriteRenderFadeListener : BaseItemEventHandle
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private float startAlpha = 1f;
        [SerializeField] private float endAlpha = 0f;
        [SerializeField] private float delay = 0f;
        [SerializeField] private float duration = 0.3f;
        [SerializeField] private Ease ease = Ease.Linear;
        [SerializeField] private bool useCurrentAsStart = true;
        [SerializeField] private bool isWaiting = true;

        private Tween _fadeTween;

        public override void Execute()
        {
            if (target == null)
                target = GetComponent<SpriteRenderer>();

            if (target == null)
            {
                IsRunning = false;
                IsDone = true;
                return;
            }

            IsRunning = true;
            IsDone = false;

            if (_fadeTween != null && _fadeTween.IsActive())
                _fadeTween.Kill();

            Color color = target.color;
            if (!useCurrentAsStart)
            {
                color.a = Mathf.Clamp01(startAlpha);
                target.color = color;
            }

            _fadeTween = target
                .DOFade(Mathf.Clamp01(endAlpha), Mathf.Max(0f, duration))
                .SetDelay(Mathf.Max(0f, delay))
                .SetEase(ease)
                .OnComplete(() =>
                {
                    IsRunning = false;
                    IsDone = true;
                });

            if (!isWaiting)
            {
                IsRunning = false;
                IsDone = true;
            }
        }

        private void OnDestroy()
        {
            if (_fadeTween != null && _fadeTween.IsActive())
                _fadeTween.Kill();
        }
    }
}

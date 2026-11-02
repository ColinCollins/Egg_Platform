using System;
using DG.Tweening;
using UnityEngine;

namespace GF
{
    [DisallowMultipleComponent]
    public class UIAnimationBase: MonoBehaviour
    {
        private UIBase _base;
        public Transform Root => _base.Root;

        public Action OnOpenAnimComplete;
        public Action OnCloseAnimComplete;
        
        protected void Start()
        {
            _base = GetComponent<UIBase>();
            if (_base == null)
            {
                return;
            }
            
            PlayEnterAnimation();
        }

        private void OnEnable()
        {
            if (_base == null)
            {
                return;
            }
            
            PlayEnterAnimation();
        }

        /// <summary>
        /// 入场动画
        /// </summary>
        /// <param name="callback"></param>
        public virtual void PlayEnterAnimation(Action callback = null)
        {
            _base.Root.DOKill(true);
            _base.Root.localScale = Vector3.one;
            Sequence sq = DOTween.Sequence();
            sq.Append(_base.Root.DOScale(1.01f, 0.06f));
            sq.Append(_base.Root.DOScale(0.98f, 0.08f));
            sq.Append(_base.Root.DOScale(1f, 0.04f));
            sq.OnComplete(delegate
            {
                callback?.Invoke();
                OnOpenAnimComplete?.Invoke();
            });
        }

        /// <summary>
        /// 退场动画
        /// </summary>
        /// <param name="callback"></param>
        public virtual void PlayExitAnimation(Action callback = null)
        {
            _base.Root.DOKill(true);
            _base.Root.DOScale(Vector3.one * 0.8f, 0.2f).SetEase(Ease.InBack).OnComplete(delegate
            {
                callback?.Invoke();
                OnCloseAnimComplete?.Invoke();
            });
        }
    }
}
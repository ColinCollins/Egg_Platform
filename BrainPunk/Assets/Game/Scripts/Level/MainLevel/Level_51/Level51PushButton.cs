using UnityEngine;
using System;
using System.Collections.Generic;
using Bear.Logger;

namespace Game.Turntable
{
    /// <summary>
    /// override  OnTriggerEnter2D he OnTriggerExit2D 功能
    /// 
    ///  新增Enter 和 Exit 的 Action<Collider2D> 回调
    /// 
    ///  提供 public 的 ManuallyExit 功能，用户根据自己的需求触发，让按钮回到 Exit 状态
    /// </summary>
    public class Level51PushButton : PressButtonBase
    {
        [SerializeField] private LayerMask ignoreLayer;

        private readonly HashSet<Collider2D> _currentColliders = new HashSet<Collider2D>();
        private Action<Collider2D> _onEnterCollider;
        private Action<Collider2D> _onExitCollider;

        protected override void Start()
        {
            base.Start();
            if (ignoreLayer == 0)
                ignoreLayer = LayerMask.GetMask("Ground");
        }

        public void SetEnter(Action<Collider2D> callback)
        {
            _onEnterCollider = callback;
        }

        public void SetExit(Action<Collider2D> callback)
        {
            _onExitCollider = callback;
        }

        /// <summary>
        /// 手动触发 Exit，让按钮回到未按压状态
        /// </summary>
        public void ManuallyExit(Collider2D collider = null)
        {
            if (_currentColliders.Count == 0 && !_isPressed)
                return;

            SetPressed(false);
            _onExit?.Invoke();
            _onExitCollider?.Invoke(collider);
            _currentColliders.Clear();
        }

        protected override void OnTriggerEnter2D(Collider2D other)
        {
            if ((ignoreLayer & (1 << other.gameObject.layer)) != 0)
                return;

            bool wasEmpty = _currentColliders.Count == 0;
            _currentColliders.Add(other);

            this.Log($" OnEnter ColliderCount: {_currentColliders.Count}");
            if (wasEmpty)
            {
                SetPressed(true);
                _onEnter?.Invoke();
                _onEnterCollider?.Invoke(other);
            }
        }

        protected override void OnTriggerExit2D(Collider2D other)
        {
            if ((ignoreLayer & (1 << other.gameObject.layer)) != 0)
                return;

            _currentColliders.Remove(other);

             this.Log($" OnExit ColliderCount: {_currentColliders.Count}");
            if (_currentColliders.Count <= 0)
            {
                SetPressed(false);
                _onExit?.Invoke();
                _onExitCollider?.Invoke(other);
            }
        }

        protected virtual void OnDisable()
        {
            _currentColliders.Clear();
            if (_isPressed)
            {
                SetPressedInstant(false);
            }
        }
    }
}

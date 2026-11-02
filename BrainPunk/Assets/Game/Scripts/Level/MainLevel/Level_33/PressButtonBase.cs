using UnityEngine;
using Game.Scripts.Common;
using System;
using Bear.Logger;

namespace Game.Turntable
{
    public abstract class PressButtonBase : MonoBehaviour, IDebuger
    {
        [SerializeField] private Transform button;
        [SerializeField] private Collider2D collider;

        [Header("Press Settings")]
        [SerializeField] private float offsetY = 0.2f;

        [SerializeField] private float tweenDuration = 0.08f;

        protected Vector3 _buttonOriginPos;
        protected Vector2 _colliderOriginOffset;
        protected bool _isPressed;
        protected Action _onEnter;
        protected Action _onExit;

        protected float _currentState01;
        protected float _targetState01;

        protected virtual void Start()
        {
            _isPressed = false;
            _buttonOriginPos = button != null ? button.position : transform.position;
            _colliderOriginOffset = collider != null ? collider.offset : Vector2.zero;
            _currentState01 = 0f;
            _targetState01 = 0f;
        }

        protected virtual void Update()
        {
            if (!Mathf.Approximately(_currentState01, _targetState01))
            {
                _currentState01 = Mathf.MoveTowards(_currentState01, _targetState01, Time.deltaTime / tweenDuration);
                ApplyState(_currentState01);
            }
        }

        public void SetEnter(Action callback)
        {
            _onEnter = callback;
        }

        public void SetExit(Action callback)
        {
            _onExit = callback;
        } 

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                SetPressed(true);
                _onEnter?.Invoke();
            }
        }

        protected virtual void OnTriggerExit2D(Collider2D other)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                SetPressed(false);
                _onExit?.Invoke();
            }
        }

        public void SetPressed(bool pressed)
        {
            this.Log("isPress" + _isPressed);
            if (_isPressed == pressed)
                return;

            _isPressed = pressed;
            _targetState01 = pressed ? 1f : 0f;
            AudioManager.PlaySound("boardTrigger");
        }

        public void SetPressedInstant(bool pressed)
        {
            _isPressed = pressed;
            _currentState01 = _targetState01 = pressed ? 1f : 0f;
            ApplyState(_currentState01);
        }

        public Vector3 GetReleasedButtonPosition() => _buttonOriginPos;

        public Vector3 GetPressedButtonPosition()
        {
            var p = _buttonOriginPos;
            p.y -= offsetY;
            return p;
        }

        public Vector2 GetReleasedColliderOffset() => _colliderOriginOffset;

        public Vector2 GetPressedColliderOffset()
        {
            var o = _colliderOriginOffset;
            o.y -= offsetY;
            return o;
        }

        protected float GetCurrentState01()
        {
            return _currentState01;
        }

        protected void ApplyState(float state01)
        {
            state01 = Mathf.Clamp01(state01);

            if (button != null)
            {
                Vector3 p = transform.position;
                p.y = Mathf.Lerp(_buttonOriginPos.y, _buttonOriginPos.y - offsetY, state01);
                button.position = p;
            }

            if (collider != null)
            {
                Vector2 o = _colliderOriginOffset;
                o.y = Mathf.Lerp(_colliderOriginOffset.y, _colliderOriginOffset.y - offsetY, state01);
                collider.offset = o;
            }
        }
    }
}

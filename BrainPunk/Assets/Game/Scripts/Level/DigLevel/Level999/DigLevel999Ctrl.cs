using System;
using Bear.Logger;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Dig
{
    /// <summary>Dig 未完待续占位关；只提供暂停/重建与返回主线传送门，不允许通关、领墨或写普通进度。</summary>
    public sealed class DigLevel999Ctrl : MonoBehaviour, IDigLevelRuntime, IDebuger
    {
        #region 运行时契约
        private bool _inputEnabled, _paused = true, _enteringMain;
        /// <inheritdoc />
        public bool IsRuntimeReady { get; private set; }
        /// <inheritdoc />
        public bool IsResultLocked => _enteringMain;
        /// <inheritdoc />
        public DrawInkState InkState => default;
        /// <inheritdoc />
        public bool HasHint => false;
        /// <inheritdoc />
        public event Action<DigLevelResult> Resolved { add { } remove { } }
        /// <inheritdoc />
        public event Action<DrawInkState> InkChanged { add { } remove { } }
        /// <inheritdoc />
        public int CountdownSeconds => 0;
        /// <inheritdoc />
        public event Action<int> CountdownChanged { add { } remove { } }
        /// <inheritdoc />
        public void InitializeRuntime()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("TBC 只允许运行态初始化。");
            IsRuntimeReady = true;
        }
        /// <inheritdoc />
        public void SetInputEnabled(bool enabled) => _inputEnabled = enabled;
        /// <inheritdoc />
        public void SetPaused(bool paused) => _paused = paused;
        /// <inheritdoc />
        public void AddRewardInk() { }
        /// <inheritdoc />
        public void ShowHint(bool visible) { }
        /// <inheritdoc />
        public bool TryCompleteByHint() => false;
        #endregion

        #region 传送门
        /// <summary>仅供本关传送门世界点击 UnityEvent；翻页/暂停/重复点击时不发起跳转。</summary>
        public void EnterMainLevelByClick()
        {
            if (!Application.isPlaying || !IsRuntimeReady || !_inputEnabled || _paused || _enteringMain) return;
            int pointer = Input.touchCount > 0 ? Input.GetTouch(0).fingerId : -1;
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject(pointer)) return;
            _enteringMain = true;
            if (GameManager.Instance.TryEnterMainLevel(out string error))
                GameSDKService.Instance.EnterTBCPortal((int)Game.Play.PlayModeType.Dig);
            else
            {
                _enteringMain = false;
                this.LogError($"[Dig TBC] 返回主线失败：{error}");
            }
        }
        #endregion
    }
}

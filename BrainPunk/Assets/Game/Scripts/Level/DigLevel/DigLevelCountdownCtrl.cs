using UnityEngine;

namespace Game.Dig
{
    /// <summary>可选的挖掘后超时失败条件；参考Draw首笔完成后启动的结构，停止和结果由本关生命周期控制。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game/Dig/Dig Level Countdown Ctrl")]
    public sealed class DigLevelCountdownCtrl : MonoBehaviour
    {
        #region Inspector 绑定
        /// <summary>当前关卡根控制器，必须在Inspector显式绑定；不通过父层级或节点名查找。</summary>
        [SerializeField] private DigLevelCtrl level;
        /// <summary>第一笔有效挖掘正常结束后的游戏秒数；默认20秒，0为首笔完成即失败，暂停／后台不扣时。</summary>
        [SerializeField, Min(0f)] private float cdTime = 20f;
        #endregion

        #region 运行状态
        private float _remainingTime;
        private bool _started, _counting;
        #endregion

        #region Unity 生命周期
        private void OnEnable()
        {
            if (!Application.isPlaying || !level) return;
            level.StrokeCompleted += OnStrokeCompleted;
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            if (level) level.StrokeCompleted -= OnStrokeCompleted;
            _started = _counting = false;
            _remainingTime = 0f;
            if (level) level.SetCountdownSeconds(0);
        }

        private void Update()
        {
            if (!Application.isPlaying || !_counting || !level) return;
            if (!level.isActiveAndEnabled || level.IsResultLocked)
            {
                _counting = false;
                level.SetCountdownSeconds(0);
                return;
            }
            if (level.IsPaused) return;
            _remainingTime = Mathf.Max(0f, _remainingTime - Time.deltaTime);
            if (_remainingTime <= 0f) CompleteCountdown();
            else RefreshCountdown();
        }
        #endregion

        #region 超时结算
        private void OnStrokeCompleted()
        {
            if (_started || !isActiveAndEnabled || !level || !level.IsRuntimeReady ||
                level.IsPaused || level.IsResultLocked || float.IsNaN(cdTime) || float.IsInfinity(cdTime)) return;
            _started = _counting = true;
            _remainingTime = Mathf.Max(0f, cdTime);
            if (_remainingTime <= 0f) CompleteCountdown();
            else RefreshCountdown();
        }

        /// <summary>仅最后五秒显示向上取整数字；整数去重由关卡快照负责，不逐帧刷新HUD或创建Tween。</summary>
        private void RefreshCountdown()
        {
            int seconds = Mathf.CeilToInt(_remainingTime);
            level.SetCountdownSeconds(seconds <= 5 ? seconds : 0);
        }

        /// <summary>停止计时后上报一次失败；胜利或其它失败已占用结果时不会覆盖，也不重复发送重开事件。</summary>
        private void CompleteCountdown()
        {
            _counting = false;
            level.SetCountdownSeconds(0);
            level.TryFail();
        }
        #endregion
    }
}

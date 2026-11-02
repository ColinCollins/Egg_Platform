using DG.Tweening;
using UnityEngine;

namespace Game.ItemEvent
{
    /// <summary>
    /// 旋转抖动控制器：按固定间隔重复播放目标的本地旋转抖动表现。
    /// </summary>
    public class ShakeRotateHandle : MonoBehaviour
    {
        #region Serialized Fields

        /// <summary>
        /// 旋转抖动目标；未绑定时使用当前组件所在的 Transform。
        /// </summary>
        [Header("Target Settings")]
        [Tooltip("旋转抖动目标；未绑定时使用当前组件所在的 Transform。")]
        [SerializeField] private Transform target;

        /// <summary>
        /// 首次播放旋转抖动前的等待时间，单位秒；循环时不会重复计算此延迟。
        /// </summary>
        [Header("Rotation Settings")]
        [Tooltip("首次播放旋转抖动前的等待时间，单位秒；循环时不会重复计算此延迟。")]
        [SerializeField, Min(0f)] private float delay;

        /// <summary>
        /// 单次旋转抖动持续时间，单位秒。
        /// </summary>
        [Tooltip("单次旋转抖动持续时间，单位秒。")]
        [SerializeField, Min(0.01f)] private float duration = 0.3f;

        /// <summary>
        /// 各本地轴的最大旋转抖动强度，单位为度；2D 表现通常只设置 Z 轴。
        /// </summary>
        [Tooltip("各本地轴的最大旋转抖动强度，单位为度；2D 表现通常只设置 Z 轴。")]
        [SerializeField] private Vector3 strength = new Vector3(0f, 0f, 18f);

        /// <summary>
        /// 单次表现中的振动次数；数值越大，抖动频率越高。
        /// </summary>
        [Tooltip("单次表现中的振动次数；数值越大，抖动频率越高。")]
        [SerializeField, Min(1)] private int vibrato = 5;

        /// <summary>
        /// 抖动方向随机度，范围为 0 到 180 度。
        /// </summary>
        [Tooltip("抖动方向随机度，范围为 0 到 180 度。")]
        [SerializeField, Range(0f, 180f)] private float randomness = 10f;

        /// <summary>
        /// 单次表现结束前是否逐渐减弱抖动幅度。
        /// </summary>
        [Tooltip("单次表现结束前是否逐渐减弱抖动幅度。")]
        [SerializeField] private bool fadeOut = true;

        /// <summary>
        /// 旋转抖动使用的缓动类型。
        /// </summary>
        [Tooltip("旋转抖动使用的缓动类型。")]
        [SerializeField] private Ease easeType = Ease.Linear;

        /// <summary>
        /// 每轮连续执行的旋转摇晃次数；感叹号默认连续摇晃两次。
        /// </summary>
        [Tooltip("每轮连续执行的旋转摇晃次数。")]
        [SerializeField, Min(1)] private int shakeCount = 2;

        /// <summary>
        /// 每组旋转抖动表现结束后的固定等待时间，单位秒。
        /// </summary>
        [Header("Loop Settings")]
        [Tooltip("每组旋转抖动表现结束后的固定等待时间，单位秒。")]
        [SerializeField, Min(0f)] private float interval = 0.7f;

        /// <summary>
        /// 播放次数设置：-1 为无限循环，0 为只播放一次，正数沿用 DOTween 的循环次数语义。
        /// </summary>
        [Tooltip("播放次数设置：-1 为无限循环，0 为只播放一次，正数沿用 DOTween 的循环次数语义。")]
        [SerializeField] private int loops = -1;

        /// <summary>
        /// 是否在运行态 OnEnable 时自动开始或重新开始播放。
        /// </summary>
        [Header("Auto Start")]
        [Tooltip("是否在运行态 OnEnable 时自动开始或重新开始播放。")]
        [SerializeField] private bool autoStart = true;

        /// <summary>
        /// 是否忽略 Time.timeScale；UI 或暂停状态下仍需播放时可开启。
        /// </summary>
        [Tooltip("是否忽略 Time.timeScale；UI 或暂停状态下仍需播放时可开启。")]
        [SerializeField] private bool ignoreTimeScale;

        #endregion

        #region Runtime State

        private Sequence rotateSequence;
        private Vector3 startEulerAngles;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (target == null)
                target = transform;

            startEulerAngles = target.localEulerAngles;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || !autoStart)
                return;

            StartRotate();
        }

        private void OnDisable()
        {
            StopRotate();
        }

        private void OnDestroy()
        {
            StopRotate();
        }

        #endregion

        #region Public API

        /// <summary>
        /// 按当前配置重新开始旋转抖动；重复调用会终止上一段序列并从起始角度重新播放。
        /// </summary>
        public void StartRotate()
        {
            StopRotate();

            if (target == null)
                return;

            target.localEulerAngles = startEulerAngles;

            Tweener shakeTweener = target
                .DOShakeRotation(
                    Mathf.Max(0.01f, duration),
                    strength,
                    Mathf.Max(1, vibrato),
                    Mathf.Clamp(randomness, 0f, 180f),
                    fadeOut)
                .SetEase(easeType)
                .SetLoops(Mathf.Max(1, shakeCount), LoopType.Restart);

            rotateSequence = DOTween.Sequence()
                .Append(shakeTweener)
                .AppendInterval(Mathf.Max(0f, interval))
                .SetDelay(Mathf.Max(0f, delay), false)
                .SetUpdate(ignoreTimeScale)
                .SetAutoKill(false);

            if (loops != 0)
                rotateSequence.SetLoops(loops, LoopType.Restart);
        }

        /// <summary>
        /// 停止当前旋转抖动和等待流程；不会主动恢复起始角度，需要恢复时调用 ResetToStart。
        /// </summary>
        public void StopRotate()
        {
            if (rotateSequence != null && rotateSequence.IsActive())
                rotateSequence.Kill();

            rotateSequence = null;
        }

        /// <summary>
        /// 暂停当前旋转抖动或间隔计时；未启动时调用不会产生效果。
        /// </summary>
        public void PauseRotate()
        {
            if (rotateSequence != null && rotateSequence.IsActive())
                rotateSequence.Pause();
        }

        /// <summary>
        /// 恢复已暂停的旋转抖动或间隔计时；已停止的序列需要重新调用 StartRotate。
        /// </summary>
        public void ResumeRotate()
        {
            if (rotateSequence != null && rotateSequence.IsActive())
                rotateSequence.Play();
        }

        /// <summary>
        /// 停止当前流程并恢复组件启动时记录的本地欧拉角。
        /// </summary>
        public void ResetToStart()
        {
            StopRotate();

            if (target != null)
                target.localEulerAngles = startEulerAngles;
        }

        #endregion
    }
}

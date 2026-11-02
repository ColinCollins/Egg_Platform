using DG.Tweening;
using UnityEngine;

namespace Game.ItemEvent
{
    /// <summary>
    /// 呼吸缩放脚本：使用 DOTween 实现物体的呼吸式缩放效果
    /// </summary>
    public class BreathingScaleHandle : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("目标 Transform，为空则使用自身")]
        [SerializeField] private Transform target;

        /// <summary>
        /// 用途：指定呼吸动画的起始缩放，避免目标初始状态被其他动画覆盖后使用错误基准。
        /// 调用边界：由 prefab 显式配置，或通过 SetStartScale 在运行时覆盖。
        /// 注意事项：未配置时默认为 Vector3.zero，会导致呼吸动画不可见。
        /// </summary>
        [SerializeField] private Vector3 startScale;
        [Header("Scale Settings")]
        [Tooltip("缩放范围（相对于原始大小的缩放倍数，例如 1.2 表示放大到 120%）")]
        [SerializeField] private float scaleRange = 1.2f;

        [Tooltip("单次缩放时长（秒）")]
        [SerializeField] private float duration = 2f;

        [Tooltip("缓动类型")]
        [SerializeField] private Ease easeType = Ease.InOutSine;

        [Header("Loop Settings")]
        [Tooltip("循环次数：-1=无限循环，0=不循环，>0=指定次数")]
        [SerializeField] private int loops = -1;

        [Tooltip("是否使用 Yoyo 模式（来回缩放）")]
        [SerializeField] private bool useYoyo = true;

        [Header("Auto Start")]
        [Tooltip("是否在 Start 时自动开始缩放")]
        [SerializeField] private bool autoStart = true;

        private Tweener scaleTweener;
        // private Vector3 startScale;

        private void Awake()
        {
            if (target == null)
                target = transform;
        }

        private void Start()
        {
            // startScale = target.localScale;

            if (autoStart)
                StartScale();
        }

        public void SetStartScale(Vector3 scale)
        {
            startScale = scale;
        }

        /// <summary>
        /// 开始缩放
        /// </summary>
        public void StartScale()
        {
            StopScale();

            Vector3 endScale = startScale * scaleRange;

            scaleTweener = target.DOScale(endScale, duration)
                .SetEase(easeType);

            if (loops != 0)
            {
                LoopType loopType = useYoyo ? LoopType.Yoyo : LoopType.Restart;
                scaleTweener.SetLoops(loops, loopType);
            }

            scaleTweener.SetAutoKill(false);
        }

        /// <summary>
        /// 停止缩放
        /// </summary>
        public void StopScale()
        {
            if (scaleTweener != null && scaleTweener.IsActive())
                scaleTweener.Kill();
        }

        /// <summary>
        /// 暂停缩放
        /// </summary>
        public void PauseScale()
        {
            if (scaleTweener != null && scaleTweener.IsActive())
                scaleTweener.Pause();
        }

        /// <summary>
        /// 恢复缩放
        /// </summary>
        public void ResumeScale()
        {
            if (scaleTweener != null && scaleTweener.IsActive())
                scaleTweener.Play();
        }

        /// <summary>
        /// 重置到起始缩放
        /// </summary>
        public void ResetToStart()
        {
            StopScale();
            if (target != null)
                target.localScale = startScale;
        }

        private void OnDestroy()
        {
            StopScale();
        }

        void OnDisable()
        {
            StopScale();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (target == null)
                target = transform;

            Vector3 currentLocalScale = Application.isPlaying ? startScale : target.localScale;
            Vector3 maxLocalScale = currentLocalScale * scaleRange;
            Vector3 minLocalScale = currentLocalScale;

            // 绘制缩放范围的 Gizmos
            Vector3 worldPos = target.position;
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(worldPos, minLocalScale);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(worldPos, maxLocalScale);
        }
#endif
    }
}

using DG.Tweening;
using UnityEngine;

namespace Game.ItemEvent
{
    /// <summary>
    /// 缓动移动脚本：使用 DOTween 实现物体在竖直或水平方向上的来回移动
    /// </summary>
    public class MoveFloatHandle : MonoBehaviour
    {
        public enum MoveMode
        {
            [Tooltip("竖直方向（Y 轴）")]
            Vertical,
            [Tooltip("横向（X 轴）")]
            Horizontal
        }

        [Header("Target Settings")]
        [Tooltip("目标 Transform，为空则使用自身")]
        [SerializeField] private Transform target;

        [Header("Movement Settings")]
        [Tooltip("移动模式：竖直（上下）或横向（左右）")]
        [SerializeField] private MoveMode moveMode = MoveMode.Vertical;

        [Tooltip("移动范围（单位：Unity 单位），竖直时为正上/正下，横向时为正右/正左")]
        [SerializeField] private float moveRange = 1f;

        [Tooltip("单次移动时长（秒）")]
        [SerializeField] private float duration = 2f;

        [Tooltip("缓动类型")]
        [SerializeField] private Ease easeType = Ease.InOutSine;

        [Header("Loop Settings")]
        [Tooltip("循环次数：-1=无限循环，0=不循环，>0=指定次数")]
        [SerializeField] private int loops = -1;

        [Tooltip("是否使用 Yoyo 模式（来回移动）")]
        [SerializeField] private bool useYoyo = true;

        [Header("Auto Start")]
        [Tooltip("是否在 Start 时自动开始移动")]
        [SerializeField] private bool autoStart = true;

        private Tweener moveTweener;
        private Vector3 startPosition;

        private void Awake()
        {
            if (target == null)
                target = transform;
        }

        private void Start()
        {
            startPosition = target.localPosition;

            if (autoStart)
                StartMove();
        }

        void OnEnable()
        {
            ResumeMove();
        }

        /// <summary>
        /// 开始移动
        /// </summary>
        public void StartMove()
        {
            StopMove();

            Vector3 direction = moveMode == MoveMode.Vertical ? Vector3.up : Vector3.right;
            Vector3 endPosition = startPosition + direction * moveRange;

            if (moveMode == MoveMode.Vertical)
                moveTweener = target.DOLocalMoveY(endPosition.y, duration).SetEase(easeType);
            else
                moveTweener = target.DOLocalMoveX(endPosition.x, duration).SetEase(easeType);

            if (loops != 0)
            {
                LoopType loopType = useYoyo ? LoopType.Yoyo : LoopType.Restart;
                moveTweener.SetLoops(loops, loopType);
            }

            moveTweener.SetAutoKill(false);
        }

        /// <summary>
        /// 停止移动
        /// </summary>
        public void StopMove()
        {
            if (moveTweener != null && moveTweener.IsActive())
                moveTweener.Kill();
        }

        /// <summary>
        /// 暂停移动
        /// </summary>
        public void PauseMove()
        {
            if (moveTweener != null && moveTweener.IsActive())
                moveTweener.Pause();
        }

        /// <summary>
        /// 恢复移动
        /// </summary>
        public void ResumeMove()
        {
            if (moveTweener != null && moveTweener.IsActive())
                moveTweener.Play();
        }

        /// <summary>
        /// 重置到起始位置
        /// </summary>
        public void ResetToStart()
        {
            StopMove();
            if (target != null)
                target.localPosition = startPosition;
        }

        private void OnDestroy()
        {
            StopMove();
        }

        void OnDisable()
        {
            PauseMove();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (target == null)
                target = transform;

            Vector3 direction = moveMode == MoveMode.Vertical ? Vector3.up : Vector3.right;
            Vector3 currentLocalPos = Application.isPlaying ? startPosition : target.localPosition;
            Vector3 startLocalPos = currentLocalPos;
            Vector3 endLocalPos = currentLocalPos + direction * moveRange;

            Transform parent = target.parent;
            Vector3 startWorldPos = parent != null ? parent.TransformPoint(startLocalPos) : startLocalPos;
            Vector3 endWorldPos = parent != null ? parent.TransformPoint(endLocalPos) : endLocalPos;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(startWorldPos, 0.1f);
            Gizmos.DrawWireSphere(endWorldPos, 0.1f);
            Gizmos.DrawLine(startWorldPos, endWorldPos);
        }
#endif
    }
}

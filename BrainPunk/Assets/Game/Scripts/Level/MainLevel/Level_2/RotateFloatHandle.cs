using DG.Tweening;
using UnityEngine;

namespace Game.ItemEvent
{
    /// <summary>
    /// 旋转缓动脚本：使用 DOTween 实现物体绕指定轴的来回旋转
    /// </summary>
    public class RotateFloatHandle : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("目标 Transform，为空则使用自身")]
        [SerializeField] private Transform target;

        [Header("Rotation Settings")]
        [Tooltip("旋转轴（本地空间）：1=X, 2=Y, 3=Z")]
        [SerializeField] private Axis axis = Axis.Z;

        [Tooltip("单侧旋转角度（度），Yoyo 时在 0 与此角度之间来回")]
        [SerializeField] private float rotationRange = 30f;

        [Tooltip("单次旋转时长（秒）")]
        [SerializeField] private float duration = 2f;

        [Tooltip("缓动类型")]
        [SerializeField] private Ease easeType = Ease.InOutSine;

        [Header("Loop Settings")]
        [Tooltip("循环次数：-1=无限循环，0=不循环，>0=指定次数")]
        [SerializeField] private int loops = -1;

        [Tooltip("是否使用 Yoyo 模式（来回旋转）")]
        [SerializeField] private bool useYoyo = true;

        [Header("Auto Start")]
        [Tooltip("是否在 Start 时自动开始旋转")]
        [SerializeField] private bool autoStart = true;

        [SerializeField] private bool isIgnoreScaleTime = true;


        private Tweener rotateTweener;
        private Vector3 startEuler;

        private enum Axis { X = 0, Y = 1, Z = 2 }

        private void Awake()
        {
            if (target == null)
                target = transform;
        }

        private void Start()
        {
            startEuler = target.localEulerAngles;

            if (autoStart)
                StartRotate();
        }

        /// <summary>
        /// 开始旋转
        /// </summary>
        public void StartRotate()
        {
            StopRotate();

            Vector3 endEuler = startEuler + GetAxisVector() * rotationRange;
            target.transform.localEulerAngles = startEuler;
            rotateTweener = target.DOLocalRotate(endEuler, duration, RotateMode.FastBeyond360)
                .SetUpdate(isIgnoreScaleTime)
                .SetEase(easeType);


            if (loops != 0)
            {
                LoopType loopType = useYoyo ? LoopType.Yoyo : LoopType.Restart;
                rotateTweener.SetLoops(loops, loopType);
            }

            rotateTweener.SetAutoKill(false);
        }

        /// <summary>
        /// 停止旋转
        /// </summary>
        public void StopRotate()
        {
            if (rotateTweener != null && rotateTweener.IsActive())
                rotateTweener.Kill();
        }

        /// <summary>
        /// 暂停旋转
        /// </summary>
        public void PauseRotate()
        {
            if (rotateTweener != null && rotateTweener.IsActive())
                rotateTweener.Pause();
        }

        /// <summary>
        /// 恢复旋转
        /// </summary>
        public void ResumeRotate()
        {
            if (rotateTweener != null && rotateTweener.IsActive())
                rotateTweener.Play();
        }

        /// <summary>
        /// 重置到起始角度
        /// </summary>
        public void ResetToStart()
        {
            StopRotate();
            if (target != null)
                target.localEulerAngles = startEuler;
        }

        private Vector3 GetAxisVector()
        {
            switch (axis)
            {
                case Axis.X: return Vector3.right;
                case Axis.Y: return Vector3.up;
                case Axis.Z: return Vector3.forward;
                default: return Vector3.forward;
            }
        }

        private void OnDestroy()
        {
            StopRotate();
        }

        private void OnDisable()
        {
            StopRotate();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (target == null)
                target = transform;

            Vector3 worldAxis = GetAxisVector();
            if (target.parent != null)
                worldAxis = target.parent.TransformDirection(worldAxis);

            Vector3 center = target.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(center, worldAxis * 0.5f);
            Gizmos.DrawWireSphere(center + worldAxis * 0.5f, 0.05f);
        }
#endif
    }
}

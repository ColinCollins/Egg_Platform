using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.ItemEvent
{
    /// <summary>
    /// 物体跳跃到目标监听器（2D 抛物线）
    /// 从 Target 位置以抛物线轨迹跳到 EndTarget，Duration 时长，JumpPower 控制弧高。
    /// isWaiting 为 true 时，Execute 后需等跳跃结束才置 IsDone；为 false 则立即置 IsDone，跳跃在后台播放。
    /// </summary>
    public class ItemJumpToListener : BaseItemEventHandle
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [Tooltip("落点 Transform；为空时使用 End Position")]
        [SerializeField] private Transform endTarget;
        [Tooltip("endTarget 为空时使用的世界坐标落点")]
        [SerializeField] private Vector3 endPosition;

        [Header("Jump Settings")]
        [SerializeField] private float duration = 1f;
        [SerializeField] private float jumpPower = 2f;

        [SerializeField] private bool isWaiting = true;

        private Tweener _jumpTweener;

        private void Awake()
        {
            if (target == null)
                target = transform;
        }

        public override void Execute()
        {
            if (target == null)
            {
                Debug.LogWarning("[ItemJumpToListener] Target is null!");
                IsDone = true;
                return;
            }

            Vector3 endPos = endTarget != null ? endTarget.position : endPosition;

            if (_jumpTweener != null && _jumpTweener.IsActive())
                _jumpTweener.Kill();

            Vector3 startPos = target.position;

            IsRunning = true;
            if (!isWaiting)
                IsDone = true;

            float progress = 0f;
            _jumpTweener = DOTween.To(() => progress, x => progress = x, 1f, duration)
                .SetEase(Ease.Linear)
                .SetAutoKill(true)
                .OnUpdate(() =>
                {
                    if (target == null) return;
                    float t = progress;
                    float arc = 4f * jumpPower * t * (1f - t);
                    target.position = new Vector3(
                        Mathf.Lerp(startPos.x, endPos.x, t),
                        Mathf.Lerp(startPos.y, endPos.y, t) + arc,
                        Mathf.Lerp(startPos.z, endPos.z, t)
                    );
                })
                .OnComplete(() =>
                {
                    if (target != null)
                        target.position = endPos;
                    IsRunning = false;
                    if (isWaiting)
                        IsDone = true;
                })
                .OnKill(() =>
                {
                    IsRunning = false;
                    if (isWaiting)
                        IsDone = true;
                });
        }

        public void Stop()
        {
            if (_jumpTweener != null && _jumpTweener.IsActive())
                _jumpTweener.Kill();
            IsRunning = false;
            IsDone = true;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetEndTarget(Transform newEndTarget)
        {
            endTarget = newEndTarget;
        }

        public void SetEndPosition(Vector3 worldPosition)
        {
            endPosition = worldPosition;
        }

#if UNITY_EDITOR
        [Button("Preview Jump")]
        private void PreviewJump()
        {
            Execute();
        }
#endif
    }
}

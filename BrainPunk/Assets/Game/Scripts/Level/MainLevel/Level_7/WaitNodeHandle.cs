using System.Collections;
using UnityEngine;

namespace Game.ItemEvent
{
    /// <summary>
    /// 等待节点：在事件执行链中等待指定时间后继续执行下一个节点
    /// </summary>
    public class WaitNodeHandle : BaseItemEventHandle
    {
        [Header("Wait Settings")]
        [Tooltip("等待时长（秒）")]
        [SerializeField] private float waitDuration = 1f;

        private Coroutine waitCoroutine;

        public override void Execute()
        {
            // 如果已经有等待协程在运行，先停止它
            if (waitCoroutine != null)
            {
                StopCoroutine(waitCoroutine);
            }

            IsRunning = true;
            IsDone = false;

            waitCoroutine = StartCoroutine(WaitRoutine());
        }

        private IEnumerator WaitRoutine()
        {
            yield return new WaitForSeconds(waitDuration);

            IsRunning = false;
            IsDone = true;
            waitCoroutine = null;
        }

        public override void ResetState()
        {
            base.ResetState();

            // 停止等待协程
            if (waitCoroutine != null)
            {
                StopCoroutine(waitCoroutine);
                waitCoroutine = null;
            }
        }

        private void OnDestroy()
        {
            if (waitCoroutine != null)
            {
                StopCoroutine(waitCoroutine);
            }
        }

        private void OnDisable()
        {
            if (waitCoroutine != null)
            {
                StopCoroutine(waitCoroutine);
            }
        }
    }
}

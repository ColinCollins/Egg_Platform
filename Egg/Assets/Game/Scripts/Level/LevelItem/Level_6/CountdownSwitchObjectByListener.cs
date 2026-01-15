using System.Threading.Tasks;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.ItemEvent
{
    public class CountdownSwitchObjectByListener : BaseItemEventHandle, IDebuger
    {
        [SerializeField] private GameObject target;

        // 执行次数
        [SerializeField] private int Countdown;
        [SerializeField] private GameObject newObj;

        [Tooltip("用于重置 isDone 状态，避免连续触发")]
        [SerializeField] private float delayRefreshInterval;

        private int currentCount = 0;

        void Awake()
        {
            currentCount = 0;
        }

        public override void Execute()
        {
            IsDone = true;
            currentCount++;
            if (currentCount >= Countdown)
            {
                target.SetActive(false);
                newObj.SetActive(true);
            }

            this.Log($"Trigger count: {currentCount}");

            WaitRefreshState();
        }

        /// <summary>
        /// 用于重置 isDone 状态，避免连续触发
        /// </summary>
        /// <returns></returns>
        private async Task WaitRefreshState()
        {
            await UniTask.WaitForSeconds(delayRefreshInterval);
            IsDone = false;
        }
    }

}

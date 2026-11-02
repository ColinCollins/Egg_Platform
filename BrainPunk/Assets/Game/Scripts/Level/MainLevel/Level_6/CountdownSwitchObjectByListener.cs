using Bear.Logger;
using UnityEngine;

namespace Game.ItemEvent
{
    public class CountdownSwitchObjectByListener : BaseItemEventHandle, IDebuger
    {
        [SerializeField] private GameObject target;

        [SerializeField] private GameObject newObj;

        // 执行次数
        [SerializeField] private int Countdown;
        private int currentCount = 0;

        private bool isFinished = false;

        void Awake()
        {
            currentCount = 0;
        }

        public override void Execute()
        {
            IsDone = true;
            currentCount++;
            if (currentCount >= Countdown && !isFinished)
            {
                if (target)
                    target.SetActive(false);
                
                if (newObj)
                    newObj.SetActive(true);

                isFinished = true;
            }

            this.Log($"Trigger count: {currentCount}");
        }
    }

}

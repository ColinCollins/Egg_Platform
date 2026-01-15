using System.Threading.Tasks;
using Bear.Logger;
using UnityEngine;

namespace Game.ItemEvent
{
    public class CountdownSwitchComponentByListener : BaseItemEventHandle, IDebuger
    {
        [SerializeField] private MonoBehaviour target;

        // 执行次数
        [SerializeField] private int Countdown;
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
                target.enabled = !target.enabled;
            }

            this.Log($"Trigger count: {currentCount}");
        }
    }

}

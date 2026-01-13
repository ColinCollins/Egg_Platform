using Bear.Logger;
using DG.Tweening;
using UnityEngine;

namespace Game.ItemEvent
{

    /// <summary>
    /// 销毁自身
    /// </summary>
    public class DestroyListener : BaseItemEventHandle, IDebuger
    {   
        [SerializeField] private GameObject target;
        [SerializeField] private float Delay = 0.1f;

        private float _time = 0;

        public override void Execute()
        {
            _time = 0;
            IsRunning = true;
            IsDone = false;
        }

        void Update()
        {
            if (!IsRunning || IsDone)
                return;

            _time += Time.deltaTime;
            if (_time > Delay)
            {
                Destroy(target);
                IsDone = true;
                this.LogError("------------ destroy!");
            }
        }

        void OnDestroy()
        {
            if (transform != null)
                transform.DOKill();
        }
    }

}

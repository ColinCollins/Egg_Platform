using UnityEngine;
using UnityEngine.Events;

namespace Game.ItemEvent
{
    public class EventHandleListener : BaseItemEventHandle
    {
        [SerializeField] private UnityEvent Handle;

        public override void Execute()
        {
            IsRunning = true;
            Handle?.Invoke();
            IsDone = true;
            IsRunning = false;
        }
    }
}

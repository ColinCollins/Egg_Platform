using UnityEngine;


namespace Game.ItemEvent
{
    public abstract class BaseItemEventHandle : MonoBehaviour, IItemEventHandle
    {
        public bool IsRunning { get; protected set; }

        public bool IsDone { get; protected set; }

        public abstract void Execute();

        public virtual void ResetState()
        {
            IsRunning = false;
            IsDone = false;
        }
    }
}
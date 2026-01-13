using UnityEngine;
namespace Game.ItemEvent
{
    [SerializeField]
    public interface IItemEventHandle
    {
        public bool IsRunning { get; }

        public bool IsDone { get; }
        public void Execute();
    }
}
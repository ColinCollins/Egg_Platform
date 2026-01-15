using UnityEngine;
namespace Game.ItemEvent
{
    public class TriggerExecutor : BaseItemExecutor
    {
        public void OnTriggerExecute(Collider2D collider)
        {
            Execute();
        }
    }
}
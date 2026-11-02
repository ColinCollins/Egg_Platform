using Bear.Logger;
using Game.ItemEvent;
using UnityEngine;

public class Level54Ctrl : MonoBehaviour, IDebuger
{
    [SerializeField] private ActorCtrl actor;
    

    public void OnKeyTrigger(Collider2D trigger)
    {
        if (trigger == null)
        {
            this.LogWarning("[Level54Ctrl] OnKeyTrigger failed: trigger is null.");
            return;
        }

        var targetObject = trigger.gameObject;
        if (targetObject == null)
        {
            this.LogWarning("[Level54Ctrl] OnKeyTrigger failed: target object is null.");
            return;
        }

        var listener = trigger.GetComponentInChildren<EventHandleListener>(true);
        if (listener == null)
        {
            this.LogWarning($"[Level54Ctrl] OnKeyTrigger failed: EventHandleListener missing on '{targetObject.name}'.");
            return;
        }

        if (listener.IsRunning || listener.IsDone)
        {
            return;
        }

        var name = targetObject.name;
        if (name.Equals("leftBtn") || name.Equals("rightBtn"))
        {
            var dragableItem = trigger.GetComponent<DragableItem>();
            if (dragableItem == null)
            {
                this.LogWarning($"[Level54Ctrl] OnKeyTrigger failed: DragableItem missing on '{name}'.");
                return;
            }

            dragableItem.enabled = true;
        }
        else
        {
            var clickItemEventHandle = trigger.GetComponent<ClickItemEventHandle>();
            if (clickItemEventHandle == null)
            {
                this.LogWarning($"[Level54Ctrl] OnKeyTrigger failed: ClickItemEventHandle missing on '{name}'.");
                return;
            }

            clickItemEventHandle.enabled = true;
        }

        listener.Execute();
    }
}

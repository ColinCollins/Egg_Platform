using Bear.EventSystem;
using Game.Events;
using UnityEngine;

public class SwitchGamePanelUseBtnBoard : MonoBehaviour, IEventSender
{
    public void OnEnterTrigger(Collider2D collider)
    {
        this.DispatchEvent(Witness<SwitchObjActiveEvent>._, true);
    }

    public void OnExitTrigger(Collider2D collider)
    {
        this.DispatchEvent(Witness<SwitchObjActiveEvent>._, false);
    }

}

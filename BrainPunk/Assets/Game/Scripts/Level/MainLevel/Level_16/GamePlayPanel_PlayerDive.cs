using Bear.EventSystem;
using Game.Events;
using UnityEngine;

public class GamePlayPanel_PlayerDive : MonoBehaviour, IEventSender
{
    [SerializeField] private CustomButton btn;

    private void Start()
    {
        btn.OnClickDown -= DiveButton;
        btn.OnClickDown += DiveButton;


        btn.OnClickUp -= CancelDiveButton;
        btn.OnClickUp += CancelDiveButton;
    }

    public void DiveButton(CustomButton btn)
    {
        Debug.Log("--------------");
        this.DispatchEvent(Witness<PlayerDiveEvent>._, true);
    }

    public void CancelDiveButton(CustomButton btn)
    {
         this.DispatchEvent(Witness<PlayerDiveEvent>._, false);
    }
}

using Game.Scripts.Common;
using UnityEngine;

public class Level20Ctrl : MonoBehaviour
{
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private DoorCtrl door;

    [SerializeField] private Transform standupPoint;
    [SerializeField] private Transform triggerPoint; 
    
    private bool isStandup = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isStandup = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (door != null)
        {
            TriggerDoor();
        }

    }

    private void TriggerDoor()
    {
        if (!isStandup && actor.transform.localPosition.x > standupPoint.position.x)
        {
            isStandup = true;
            door.enabled = true;
            door.StandUp();
            AudioManager.PlaySound("doorStandup");
        }

        if (actor.transform.localPosition.x > triggerPoint.position.x)
        {
            door.StartMove();
        }
    }
}

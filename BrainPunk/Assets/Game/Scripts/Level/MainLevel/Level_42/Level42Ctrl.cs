using System;
using System.Linq;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using Game.Events;
using UnityEngine;

public class Level42Ctrl : MonoBehaviour
{
    [SerializeField] private Rigidbody2D door;

    [SerializeField] private Rigidbody2D[] ballons;

    [SerializeField] private ParticleSystem knock;

    [SerializeField] private Camera mainCamera;
    private EventSubscriber _subscriber;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<OnTiggerItemEvent>(OnEventTrigger);

        SwitchBallonsSimulated(false);
        WaitingForPlay().Forget();
    }

    private async UniTask WaitingForPlay()
    {
        await UniTask.WaitForSeconds(1.2f, ignoreTimeScale: true, cancellationToken: this.destroyCancellationToken);
        SwitchBallonsSimulated(true);
    }

    private void OnEventTrigger(OnTiggerItemEvent @event)
    {
        if (@event.EventId != 1)
            return;

        SwitchBallonsSimulated(false);

        Destroy(door.GetComponent<Collider2D>());
        Destroy(door);
    }

    private void SwitchBallonsSimulated(bool isOpen)
    {
        for (int i = 0; i < ballons.Length; i++)
        {
            if (ballons[i] == null)
                break;

            ballons[i].simulated = isOpen;
        }

        if (door)
            door.simulated = isOpen;
    }

    public void PlayKnock()
    {
        if (Input.touchCount > 0)
        {
            var _ = ballons.First();
            var inputPosition = Input.GetTouch(0).position;
            var worldPosition = Camera.main.ScreenToWorldPoint(inputPosition);
            if (Input.touchCount > 1)
                for (int i = 1; i < Input.touchCount; i++)
                {
                    var pos = Input.GetTouch(i).position;
                    var ws = Camera.main.ScreenToWorldPoint(pos);
                    var dis1 = Vector3.Distance(ws, _.position);
                    var dis2 = Vector3.Distance(worldPosition, _.position);
                    if (dis1 < dis2)
                    {
                        worldPosition = ws;
                    }
                }
            worldPosition.z = 0;

            var obj = Instantiate(knock, transform);
            obj.gameObject.SetActive(true);
            obj.transform.position = worldPosition;

            Debug.Log("--------------- Play knock");
        }

    }


    void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
    }
}

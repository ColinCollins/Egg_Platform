using Bear.EventSystem;
using Bear.UI;
using Game.Events;
using UnityEngine;

public class Level6GamePanelFeature : MonoBehaviour
{
    private EventSubscriber _subscriber;

    public EventSubscriber Subscriber => _subscriber;

    public CustomButton Use_btn;

    void Awake()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchObjActiveEvent>(OnSwitchObj);

        Use_btn.OnClick += OnShowPasswordPopup;
    }

    private void OnSwitchObj(SwitchObjActiveEvent evt)
    {
        Use_btn.gameObject.SetActive(evt.isShow);
    }

    private void OnShowPasswordPopup(CustomButton btn)
    {
        PasswordPopup.Create();
    }

    void OnDestroy()
    {
         Use_btn.OnClick -= OnShowPasswordPopup;
        EventsUtils.ResetEvents(ref _subscriber);
    }
}

using System;
using Bear.EventSystem;
using Bear.UI;
using DG.Tweening;
using Game.Events;
using UnityEngine;

public class Level44GamePanelFeature : MonoBehaviour, IEventSender
{
    private EventSubscriber _subscriber;

    public EventSubscriber Subscriber => _subscriber;

    public CustomButton Use_btn;

    void Awake()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchObjActiveEvent>(OnSwitchObj);

        Use_btn.OnClick += OnTriggerClick;
    }

    private void OnTriggerClick(CustomButton btn)
    {
        this.DispatchEvent(Witness<OnTiggerItemEvent>._, 1);
    }

    private void OnTiggerItem(OnTiggerItemEvent evt)
    {
        if (evt.EventId == 1)
        {
            var trans = Use_btn.transform;
            trans.DOKill();
            trans.localScale = Vector3.one;
            trans.DOScale(Vector3.zero, 0.1f);
        }
    }

    private void OnSwitchObj(SwitchObjActiveEvent evt)
    {
        var trans = Use_btn.transform;
        if (evt.isShow)
        {
            Use_btn.gameObject.SetActive(evt.isShow);

            trans.DOKill();
            trans.localScale = Vector3.one * 0.5f;
            trans.DOScale(Vector3.one, 0.1f);
        }
        else
        {
            trans.DOKill();
            trans.localScale = Vector3.one;
            trans.DOScale(Vector3.zero, 0.1f);
        }
    }

    void OnDestroy()
    {
        Use_btn.OnClick -= OnTriggerClick;
        EventsUtils.ResetEvents(ref _subscriber);

        if (Use_btn)
        {
            Use_btn.transform.DOKill();
        }
    }
}

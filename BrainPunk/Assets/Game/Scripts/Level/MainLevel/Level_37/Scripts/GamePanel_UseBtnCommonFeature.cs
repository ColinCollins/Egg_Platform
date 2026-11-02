using Bear.EventSystem;
using Bear.UI;
using DG.Tweening;
using Game.Events;
using UnityEngine;

/// <summary>
/// 从 level_6 衍生出来的脚本，用于 GamePlayPanel UseBtn 的复用
/// </summary>
public class GamePanel_UseBtnCommonFeature : MonoBehaviour
{
    private EventSubscriber _subscriber;

    public EventSubscriber Subscriber => _subscriber;

    public CustomButton Use_btn;

    void Awake()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchObjActiveEvent>(OnSwitchObj);
    }

    private void OnSwitchObj(SwitchObjActiveEvent evt)
    {
        SwitchObj(evt.isShow);
    }

    private void SwitchObj(bool isShow)
    {
        var trans = Use_btn.transform;
        if (isShow)
        {
            Use_btn.gameObject.SetActive(isShow);

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

    public void StopListener()
    {
        SwitchObj(false);
        EventsUtils.ResetEvents(ref _subscriber);
    }


    void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);

        if (Use_btn)
        {
            Use_btn.transform.DOKill();
        }
    }
}

using System;
using System.Threading.Tasks;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

public class Level16Dive : MonoBehaviour
{
    [SerializeField] private ActorCtrl actor;

    private bool isFirstEnter = false;

    private Transform downBtn;

    private EventSubscriber _subscriber;

    void Awake()
    {
        AddListener();
    }

    void Start()
    {
        downBtn = PlayCtrl.Instance.CurrentGamePlayPanel.transform.Find("Root/down_btn");

        ShowDiveButton().Forget();
    }

    public virtual void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerDiveEvent>(OnPlayerDive);
    }

    private async UniTask ShowDiveButton()
    {
        downBtn.gameObject.SetActive(false);

        await UniTask.WaitForSeconds(2f, ignoreTimeScale: true);

        downBtn.gameObject.SetActive(true);
    }

    private void OnPlayerDive(PlayerDiveEvent @evt)
    {
        actor.SwitchDiveState(evt.isDown);
    }

    public void PlayAnim(Collider2D collider)
    {
        if (isFirstEnter)
            return;

        isFirstEnter = true;
        actor.StopMoving();
        actor.transform.DOLocalMoveY(-6, 1f).SetEase(Ease.OutQuad).SetLoops(2, LoopType.Yoyo).OnComplete(() =>
        {
            actor.ResumeMoving();
        });

        AudioManager.PlaySound("jumpToWater");
    }

    void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
    }
}

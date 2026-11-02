using System;
using Bear.EventSystem;
using Bear.UI;
using DG.Tweening;
using Game.Events;
using Game.Scripts.Common;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// Level43 关卡控制器，负责创建和管理 Level43LoadingPopup 窗口
/// 倒计时结束后显示钥匙，玩家获取钥匙通关
/// </summary>
public class Level43Ctrl : MonoBehaviour
{
    [SerializeField] private Transform door;
    [SerializeField] private MMF_Player shake;
    [SerializeField] private ActorCtrl actor;

    private Level43LoadingPopup loadingPopup;
    private EventSubscriber _subscriber;

    private bool isStop = false;

    void Awake()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<OnTiggerItemEvent>(OnEventTrigger);

        if (actor != null)
            actor.StopMoving();
    }

    void Start()
    {
        // 创建加载弹窗
        InitLoadingPopup();
    }

    private void OnEventTrigger(OnTiggerItemEvent @event)
    {
        isStop = true;
        if (actor != null)
            actor.StopMoving();
    }

    void Update()
    {
        if (isStop && actor != null)
            actor.StopMoving();
    }

    /// <summary>
    /// 显示加载弹窗并开始倒计时
    /// </summary>
    public void InitLoadingPopup()
    {
        loadingPopup = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponentInChildren<Level43LoadingPopup>(true);
        loadingPopup.SetCompleteCallback(OnCountdownComplete);
    }

    /// <summary>
    /// 关闭加载弹窗
    /// </summary>
    public void CloseLoadingPopup()
    {
        if (loadingPopup != null)
        {
            UIManager.Instance.DestroyUI(loadingPopup);
            loadingPopup = null;
        }
    }

    /// <summary>
    /// 倒计时结束回调
    /// </summary>
    public void OnCountdownComplete()
    {
        PlayComplete();
        // 关闭弹窗
        CloseLoadingPopup();

        isStop = false;
        actor.ResumeMoving();
    }

    void PlayComplete()
    {
        door.transform.DOMoveY(-1.27f, 0.5f).SetEase(Ease.InCubic).OnComplete(() =>
        {
            shake.PlayFeedbacks();
            AudioManager.PlaySound("crash");
        });
    }

    void OnDestroy()
    {
        // 确保清理
        CloseLoadingPopup();
        EventsUtils.ResetEvents(ref _subscriber);
    }
}

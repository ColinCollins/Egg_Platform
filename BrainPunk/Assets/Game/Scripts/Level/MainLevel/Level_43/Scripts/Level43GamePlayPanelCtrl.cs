using System;
using Bear.EventSystem;
using Bear.UI;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// ClosePart 在倒计时完成时关闭的内容
/// ShowPart 在倒计时完成时显示的内容
/// </summary>
public class Level43GamePlayPanelCtrl : MonoBehaviour, IEventSender
{
    [SerializeField] private GamePlayPanel playPanel;
    [Header("组件引用")]
    [SerializeField] private Level43ErrorPopup errorPopup;
    [SerializeField] private Level43LoadingPopup loadingPopup;

    [Header("倒计时完成时的切换")]
    [Tooltip("倒计时完成时要关闭的 GameObject")]
    [SerializeField] private GameObject[] ClosePart;

    [Tooltip("倒计时完成时要显示的 GameObject")]
    [SerializeField] private GameObject[] ShowPart;

    private UIScaleAnimation scaleAnimation;
    private bool isCompleted = false;
    private bool isStart = false;

    private EventSubscriber _subscriber;

    void Start()
    {
        // 初始化组件
        if (!ValidateSetup())
        {
            Debug.LogError("[Level43GamePlayPanelCtrl] 初始化失败，配置有误！", this);
            enabled = false;
            return;
        }

        SetObjectsActive(ClosePart, false);
        SetObjectsActive(ShowPart, true);

        // Level43ErrorPopup.Create(ShowLoadingPopup);

        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayRightMove);

        _subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayLeftMove);
    }

    private void OnPlayLeftMove(PlayerLeftMoveEvent @event)
    {
        EventsUtils.ResetEvents(ref _subscriber);
        ShowErrorPopup();
    }

    private void OnPlayRightMove(PlayerRightMoveEvent @event)
    {
        EventsUtils.ResetEvents(ref _subscriber);
        ShowErrorPopup();
    }

    private void ShowErrorPopup()
    {
        errorPopup.gameObject.SetActive(true);
        errorPopup.OnCreate();
        errorPopup.OnOpen();
        errorPopup.SetCallback(ShowLoadingPopup);

        SetObjectsActive(ClosePart, true);
        SetObjectsActive(ShowPart, false);

        AudioManager.StopMusic();

        playPanel.ResetState();
        this.DispatchEvent(Witness<OnTiggerItemEvent>._, 1);
    }

    void ShowLoadingPopup()
    {
        isStart = true;

        loadingPopup.gameObject.SetActive(true);
        // 获取动画组件
        scaleAnimation = loadingPopup.GetComponent<UIScaleAnimation>();
        if (scaleAnimation != null)
        {
            scaleAnimation.PlayOpenAnimation();
        }

        // 初始化弹窗
        loadingPopup.OnCreate();
        loadingPopup.OnOpen();

        // 注册倒计时完成回调
        // 注意：如果 Level43LoadingPopup 有完成回调机制，使用它
        // 这里假设通过 Level43Ctrl 或 Update 检测
    }

    void Update()
    {
        if (!isStart)
            return;
        // 检测倒计时是否完成
        if (!isCompleted && loadingPopup != null)
        {
            // 如果 loadingPopup 有完成状态检测
            if (IsCountdownFinished())
            {
                OnCountdownComplete();
            }
        }
    }

    /// <summary>
    /// 验证配置是否正确
    /// </summary>
    bool ValidateSetup()
    {
        if (loadingPopup == null)
        {
            Debug.LogError("[Level43GamePlayPanelCtrl] loadingPopup 未赋值！", this);
            return false;
        }
        return true;
    }

    /// <summary>
    /// 检查倒计时是否完成
    /// </summary>
    bool IsCountdownFinished()
    {
        // 通过剩余时间判断是否完成
        return loadingPopup.GetRemainTime() <= 0 && loadingPopup.isClickOK;
    }

    /// <summary>
    /// 倒计时完成回调
    /// </summary>
    public void OnCountdownComplete()
    {
        if (isCompleted) return;
        isCompleted = true;

        Debug.Log("[Level43GamePlayPanelCtrl] 倒计时完成，切换界面状态");

        // 关闭 ClosePart
        SetObjectsActive(ClosePart, false);

        // 显示 ShowPart
        SetObjectsActive(ShowPart, true);
    }

    /// <summary>
    /// 设置 GameObject 数组的激活状态
    /// </summary>
    void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;

        foreach (var obj in objects)
        {
            if (obj != null)
            {
                obj.SetActive(active);
            }
        }
    }


    /// <summary>
    /// 强制完成倒计时（供外部调用，如看完广告后）
    /// </summary>
    public void ForceComplete()
    {
        if (loadingPopup != null)
        {
            loadingPopup.SetRemainTime(0);
        }
        OnCountdownComplete();
    }

    void OnDestroy()
    {
        if (loadingPopup != null)
        {
            loadingPopup.OnClose();
        }

        EventsUtils.ResetEvents(ref _subscriber);
    }
}

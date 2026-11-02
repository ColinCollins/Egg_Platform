using System;
using Bear.EventSystem;
using Bear.UI;
using Game.Events;
using Game.Play;
using UnityEngine;

/// <summary>
/// Level 132 real Tips popup.
/// The dedicated Prefab pauses gameplay while the modal prompt is visible.
/// </summary>
public sealed class Level132RealTipsPopup : BaseUIView, IEventSender
{
    private CustomButton _closeButton;
    private bool _closedSilently;
    private bool _closedByButton;
    public event Action<Level132RealTipsPopup> Closed;

    public override void OnCreate()
    {
        base.OnCreate();
        _closeButton = GetComponentInChildren<CustomButton>(true);
        if (_closeButton == null)
            return;

        _closeButton.OnClick -= OnCloseButtonClicked;
        _closeButton.OnClick += OnCloseButtonClicked;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        _closedSilently = false;
        _closedByButton = false;
    }

    public override void OnShow()
    {
        base.OnShow();
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PAUSE);
    }

    public override void OnClose()
    {
        // ResetGame 的 CloseAllUI 可能先于本关 Reset 监听执行；只有叉号才恢复玩法。
        bool resume = IsOpen && !_closedSilently && _closedByButton;
        _closedByButton = false;
        var closed = Closed;
        Closed = null;
        base.OnClose();
        if (!resume) return;
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
        closed?.Invoke(this);
    }

    private void OnCloseButtonClicked(CustomButton _)
    {
        _closedByButton = true;
        UIManager.Instance?.CloseUI<Level132RealTipsPopup>();
    }

    public void CloseFromOwner()
    {
        _closedSilently = true;
        UIManager.Instance?.DestroyUI(this);
    }

    public static Level132RealTipsPopup Create()
    {
        if (UIManager.Instance == null)
            return null;

        return UIManager.Instance.OpenUI<Level132RealTipsPopup>(
            nameof(Level132RealTipsPopup),
            UILayer.Popup,
            isShowMask: true,
            isCloseByMask: false);
    }
}

using Bear.EventSystem;
using Bear.UI;
using Game.Events;
using Game.Play;
using UnityEngine;

/// <summary>
/// Level 106 real Tips popup.
/// The generated Prefab pauses gameplay while the modal prompt is visible.
/// </summary>
public sealed class Level106RealTipsPopup : BaseUIView, IEventSender
{
    private CustomButton _closeButton;
    private bool _closedSilently;

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
    }

    public override void OnShow()
    {
        base.OnShow();
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PAUSE);
    }

    public override void OnClose()
    {
        if (!_closedSilently)
            this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);

        base.OnClose();
    }

    private void OnCloseButtonClicked(CustomButton _)
    {
        UIManager.Instance?.CloseUI<Level106RealTipsPopup>();
    }

    public void CloseFromOwner()
    {
        _closedSilently = true;
        UIManager.Instance?.DestroyUI(this);
    }

    public static Level106RealTipsPopup Create()
    {
        if (UIManager.Instance == null)
            return null;

        return UIManager.Instance.OpenUI<Level106RealTipsPopup>(
            nameof(Level106RealTipsPopup),
            UILayer.Popup,
            isShowMask: true,
            isCloseByMask: false);
    }
}

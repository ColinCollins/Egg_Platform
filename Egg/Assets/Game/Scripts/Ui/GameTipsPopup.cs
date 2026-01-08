using Bear.EventSystem;
using Bear.UI;
using Game.Events;
using Game.Play;
using UnityEngine;

public partial class GameTipsPopup : BaseUIView, IEventSender
{
    public override void OnOpen()
    {
        MakesureBtn.OnClick += Close;
    }

    private void Close(CustomButton btn)
    {
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
        UIManager.Instance.CloseUI(this);
    }

    public static GameTipsPopup Create()
    {
        var panel = UIManager.Instance.OpenUI<GameTipsPopup>($"{typeof(GameTipsPopup).Name}", UILayer.Popup);
        return panel;
    }
}

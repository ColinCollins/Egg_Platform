using System.Diagnostics;
using Bear.EventSystem;
using Bear.UI;
using Game.Events;
using Game.Play;
using UnityEngine;

public partial class GameSettingPopup : BaseUIView, IEventSender
{
    public override void OnOpen()
    {
        CloseBtn.OnClick += Close;
        ExitBtn.OnClick += ExitLevel;
    }

    private void Close(CustomButton btn)
    {
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
        UIManager.Instance.CloseUI(this);
    }

    private void ExitLevel(CustomButton btn)
    {
        // this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.START);
        ChoiceLevelPanel.Create();
        UIManager.Instance.CloseUI(this);
    }

    public static GameSettingPopup Create()
    {
        var panel = UIManager.Instance.OpenUI<GameSettingPopup>($"{typeof(GameSettingPopup).Name}", UILayer.Popup);
        return panel;
    }
}

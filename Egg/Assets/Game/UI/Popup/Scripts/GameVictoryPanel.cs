using Bear.EventSystem;
using Bear.UI;
using Game.Events;
using UnityEngine;

public partial class GameVictoryPanel : BaseUIView, IEventSender
{
    public override void OnOpen()
    {
        MakesureBtn.OnClick += OnNextLevel;
    }

    private void OnNextLevel(CustomButton btn)
    {
        this.DispatchEvent(Witness<GameResetEvent>._);
        UIManager.Instance.CloseUI(this);
    }

    public static GameVictoryPanel Create()
    {
        var panel = UIManager.Instance.OpenUI<GameVictoryPanel>($"{typeof(GameVictoryPanel).Name}", UILayer.Popup);
        return panel;
    }
}

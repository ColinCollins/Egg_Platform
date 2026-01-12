using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Game.Events;
using Game.Play;

public partial class StartPanel : BaseUIView, IDebuger, IEventSender
{
    // public CustomButton PlayBtn;

    public override void OnOpen()
    {
        base.OnOpen();
        PlayBtn.OnClick += OnClickPlay;
    }

    /// <summary>
    /// 直接进入游戏
    /// </summary>
    /// <param name="btn"></param>
    private void OnClickPlay(CustomButton btn)
    {   
        this.DispatchEvent(Witness<EnterLevelEvent>._, PlayCtrl.Instance.Level.CurrentLevelData);
        // this.Log("Play Game");
        UIManager.Instance.CloseUI(this);
    }

    public static StartPanel Create()
    {
        var panel = UIManager.Instance.OpenUI<StartPanel>($"{typeof(StartPanel).Name}", UILayer.Normal);
        return panel;
    }
}

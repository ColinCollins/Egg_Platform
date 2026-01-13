using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Game.Events;

public partial class StartPanel : BaseUIView, IDebuger, IEventSender
{

    public override void OnOpen()
    {
        base.OnOpen();
        PlayBtn.OnClick += OnClickPlay;
        ChoiceLevelBtn.OnClick += OnShowChoiceLevel;

        TestBtn.OnClick += TestTips;
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

    private void OnShowChoiceLevel(CustomButton btn)
    {
        ChoiceLevelPanel.Create();
    }

    private void TestTips(CustomButton btn)
    {
        SystemTips.Show(transform.parent, "test - luoweiming");
    }

    public static StartPanel Create()
    {
        var panel = UIManager.Instance.OpenUI<StartPanel>($"{typeof(StartPanel).Name}", UILayer.Normal);
        return panel;
    }
}

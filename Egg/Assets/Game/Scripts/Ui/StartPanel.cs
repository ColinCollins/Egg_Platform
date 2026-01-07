using Bear.Logger;
using Bear.UI;
using UnityEngine;
using UnityEngine.UI;

public class StartPanel : BaseUIView, IDebuger
{
    public CustomButton PlayBtn;

    public override void OnOpen()
    {
        base.OnOpen();
        PlayBtn.OnClick += OnClickPlay;
        
    }

    private void OnClickPlay(CustomButton btn)
    {
        this.Log("Play Game");
        UIManager.Instance.CloseUI(this); 

    }

    public static StartPanel Create()
    {
        var panel = UIManager.Instance.OpenUI<StartPanel>($"{typeof(StartPanel).Name}", UILayer.Normal);
        return panel;
    }
}

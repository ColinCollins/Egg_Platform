using Bear.Logger;
using Bear.UI;
using UnityEngine;
using UnityEngine.UI;

public class StartPanel : BaseUIView, IDebuger
{
    public Button PlayBtn;

    public override void OnOpen()
    {
        base.OnOpen();
        PlayBtn.onClick.AddListener(OnClickPlay);
        
    }

    private void OnClickPlay()
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

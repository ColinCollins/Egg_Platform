using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Game.Events;
using UnityEngine;

public class GamePlayPanel : BaseUIView, IDebuger, IEventSender
{
    [SerializeField] private CustomButton ResetBtn;

    [SerializeField] private CustomButton PauseBtn;

    [SerializeField] private CustomButton TipsBtn;

    #region Player ctrl


    [SerializeField] private CustomButton JumpBtn;

    [SerializeField] private CustomButton RightBtn;
    [SerializeField] private CustomButton LeftBtn;

    #endregion

    public override void OnOpen()
    {
        base.OnOpen();
        ResetBtn.OnClick += OnClickReset;
        PauseBtn.OnClick += OnClickPause;
        TipsBtn.OnClick += OnClickTips;

        JumpBtn.OnClick += OnClickJump;
        RightBtn.OnClick += OnClickRight;
        LeftBtn.OnClick += OnClickLeft;

    }

    private void OnClickReset(CustomButton btn)
    {
        this.Log("Play Game");
        this.DispatchEvent(Witness<GameReset>._);
    }

    private void OnClickPause(CustomButton btn)
    {
        this.Log("Pause Game");
        this.DispatchEvent(Witness<GamePause>._);
    } 

    private void OnClickTips(CustomButton btn)
    {
        this.Log("Tips Panel");
        this.DispatchEvent(Witness<GameTips>._);
    } 

    private void OnClickJump(CustomButton btn)
    {
        this.Log("Jump");
        this.DispatchEvent(Witness<PlayerJump>._);
    } 

    private void OnClickRight(CustomButton btn)
    {
        this.Log("Right");
        this.DispatchEvent(Witness<PlayerJump>._);
    } 

    private void OnClickLeft(CustomButton btn)
    {
        this.Log("Left");
    } 
    public static GamePlayPanel Create()
    {
        var panel = UIManager.Instance.OpenUI<GamePlayPanel>($"{typeof(GamePlayPanel).Name}", UILayer.Normal);
        return panel;
    }
}

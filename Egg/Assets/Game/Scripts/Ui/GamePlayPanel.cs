using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Game.Events;
using UnityEngine;

public partial class GamePlayPanel : BaseUIView, IDebuger, IEventSender
{
    #region System
    private bool isPause = false;

    private EventSubscriber _subscriber;

    #endregion 

    #region Player ctrl

    private bool isRightDown = false;

    private bool isLeftDown = false;

    #endregion

    public override void OnOpen()
    {
        base.OnOpen();
        ResetBtn.OnClick += OnClickReset;
        PauseBtn.OnClick += OnClickSetting;
        TipsBtn.OnClick += OnClickTips;

        JumpBtn.OnClickDown += OnClickJump;
        RightMoveBtn.OnClickDown += OnClickDownRight;
        LeftMoveBtn.OnClickDown += OnClickDownLeft;

        RightMoveBtn.OnClickUp += OnClickUpRight;
        LeftMoveBtn.OnClickUp += OnClickUpLeft;

        isPause = false;
        isRightDown = false;
        isLeftDown = false;

        AddListener();
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<ResumeGamePanelEvent>(OnGamePanelResume);
    }

    private void OnClickDownRight(CustomButton btn)
    {
        this.Log("Right Down");
        isRightDown = true;
    }

    private void OnClickDownLeft(CustomButton btn)
    {
        this.Log("Left Down");
        isLeftDown = true;
    }

    private void OnClickUpRight(CustomButton btn)
    {
        this.Log("Right Up");
        isRightDown = false;
    }

    private void OnClickUpLeft(CustomButton btn)
    {
        this.Log("Left Up");
        isLeftDown = false;
    }

    private void OnClickJump(CustomButton btn)
    {
        this.Log("Jump");
        this.DispatchEvent(Witness<PlayerJumpEvent>._);
    }

    void Update()
    {
        if (isPause)
            return;

        if (isRightDown)
            this.DispatchEvent(Witness<PlayerRightMoveEvent>._);
        else if (isLeftDown)
            this.DispatchEvent(Witness<PlayerLeftMoveEvent>._);
    }

    private void OnClickReset(CustomButton btn)
    {
        this.Log("Play Game");
        isPause = true;
        this.DispatchEvent(Witness<GameResetEvent>._);
    }

    private void OnClickSetting(CustomButton btn)
    {
        this.Log("Pause Game");
        isPause = true;
        this.DispatchEvent(Witness<GameSettingEvent>._);
    }

    private void OnClickTips(CustomButton btn)
    {
        this.Log("Tips Panel");
        isPause = true;
        this.DispatchEvent(Witness<GameTipsEvent>._);
    }

    private void OnGamePanelResume(ResumeGamePanelEvent evt)
    {
        isPause = false;   
    }

    public override void OnClose()
    {
        base.OnClose();
        EventsUtils.ResetEvents(ref _subscriber);
    }

    public static GamePlayPanel Create()
    {
        var panel = UIManager.Instance.OpenUI<GamePlayPanel>($"{typeof(GamePlayPanel).Name}", UILayer.Normal);
        return panel;
    }
}

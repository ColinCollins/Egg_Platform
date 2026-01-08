using System.Diagnostics;
using Bear.EventSystem;
using Bear.Fsm;
using Bear.Logger;
using Game.Common;
using Game.Events;
using Game.Play;

/* public enum GamePlayState
{
    // 默认。没什么功能设计
    Start = 0,
    // 开始游戏
    Playing, 
    // 暂停游戏
    Pause,
    // 游戏成功
    Success,
    // 游戏失败
    Failed
} */

/// <summary>
/// 
/// </summary>
public class PlayCtrl : Singleton<PlayCtrl>, IBearMachineOwner, IDebuger, IEventSender
{
    private StateMachine _machine;

    private EventSubscriber _subscriber;

    public EventSubscriber Subscriber => _subscriber;


    public void Init()
    {
        _machine = new StateMachine(this);
        _machine.Inject(typeof(PlayCtrl_Start),
        typeof(PlayCtrl_Playing),
        typeof(PlayCtrl_Pause),
        typeof(PlayCtrl_Success),
        typeof(PlayCtrl_Failed));

        _machine.Apply(GetType());
        _machine.Enter(GamePlayStateName.START);

        AddListener();
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchState);
        _subscriber.Subscribe<GameSettingEvent>(OnGameSettingEvent);
        _subscriber.Subscribe<GameTipsEvent>(OnGameTipsEvent);
    }

    private void OnSwitchState(SwitchGameStateEvent evt)
    {
        _machine.Enter(evt.NewState);
        this.Log(evt.NewState);
    }

    private void OnGameSettingEvent(GameSettingEvent evt)
    {
        this.Log("show Settings");
        _machine.Enter(GamePlayStateName.PAUSE);
    }

    private void OnGameTipsEvent(GameTipsEvent evt)
    {
        this.Log("show Tips");
        _machine.Enter(GamePlayStateName.PAUSE);
    }

    private void Update()
    {
        _machine?.Update();
    }

    private void OnDestroy()
    {
        _machine?.Dispose();
    }
}

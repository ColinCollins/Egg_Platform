using Bear.Fsm;
using Game.Common;
using Game.Play;
using UnityEngine;

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
public class PlayCtrl : Singleton<PlayCtrl>, IBearMachineOwner
{
    private StateMachine _machine;

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

using Bear.EventSystem;
using Bear.Fsm;
using Game.Events;
using UnityEngine;

namespace Game.Play
{
    /// <summary>
    /// 游戏暂停状态节点
    /// </summary>
    [StateMachineNode(typeof(PlayCtrl), GamePlayStateName.PAUSE, false)]
    public class PlayCtrl_Pause : StateNode, IEventSender
    {
        public override void OnEnter()
        {
            Debug.Log($"{nameof(PlayCtrl_Pause)} Enter");
            this.DispatchEvent(Witness<GamePauseEvent>._);
        }

        public override void OnExecute()
        {
            Debug.Log($"{nameof(PlayCtrl_Pause)} Execute");
        }

        public override void OnUpdate()
        {
            Debug.Log($"{nameof(PlayCtrl_Pause)} Update");
        }

        public override void OnExit()
        {
            Debug.Log($"{nameof(PlayCtrl_Pause)} Exit");
            this.DispatchEvent(Witness<GameResumeEvent>._);
        }
    }
}


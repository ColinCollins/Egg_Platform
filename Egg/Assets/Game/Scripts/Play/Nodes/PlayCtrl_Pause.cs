using Bear.Fsm;
using UnityEngine;

namespace Game.Play
{
    /// <summary>
    /// 游戏暂停状态节点
    /// </summary>
    [StateMachineNode(typeof(PlayCtrl), GamePlayStateName.PAUSE, false)]
    public class PlayCtrl_Pause : StateNode
    {
        public override void OnEnter()
        {
            Debug.Log($"{nameof(PlayCtrl_Pause)} Enter");
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
        }
    }
}


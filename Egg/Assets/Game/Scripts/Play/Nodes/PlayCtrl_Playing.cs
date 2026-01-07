using Bear.Fsm;
using UnityEngine;

namespace Game.Play
{
    /// <summary>
    /// 游戏进行中状态节点
    /// </summary>
    [StateMachineNode(typeof(PlayCtrl), GamePlayStateName.PLAYING, false)]
    public class PlayCtrl_Playing : StateNode
    {
        public override void OnEnter()
        {
            Debug.Log($"{nameof(PlayCtrl_Playing)} Enter");
        }

        public override void OnExecute()
        {
            Debug.Log($"{nameof(PlayCtrl_Playing)} Execute");
        }

        public override void OnUpdate()
        {
            Debug.Log($"{nameof(PlayCtrl_Playing)} Update");
        }

        public override void OnExit()
        {
            Debug.Log($"{nameof(PlayCtrl_Playing)} Exit");
        }
    }
}


using Bear.Fsm;
using Game.Scripts.Common;
using UnityEngine;

namespace Game.Play
{
    /// <summary>
    /// 游戏开始状态节点
    /// </summary>
    [StateMachineNode(typeof(PlayCtrl), GamePlayStateName.START, true)]
    public class PlayCtrl_Start : StateNode
    {
        public override void OnEnter()
        {
            Debug.Log($"{nameof(PlayCtrl_Start)} Enter");

            if (!AudioManager.IsCurrentMusicTag("musicOutGame"))
                AudioManager.PlayMusic("musicOutGame", fadeInSeconds: 8f);

            (_owner as PlayCtrl).DestroyLevel();
        }

        public override void OnExecute()
        {
            Debug.Log($"{nameof(PlayCtrl_Start)} Execute");
        }

        public override void OnUpdate()
        {
            // Debug.Log($"{nameof(PlayCtrl_Start)} Update");
        }

        public override void OnExit()
        {
            Debug.Log($"{nameof(PlayCtrl_Start)} Exit");
        }
    }
}


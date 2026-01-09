using Bear.Fsm;
using UnityEngine;

namespace Game.Play
{
    /// <summary>
    /// 游戏成功状态节点
    /// </summary>
    [StateMachineNode(typeof(PlayCtrl), GamePlayStateName.SUCCESS, false)]
    public class PlayCtrl_Success : StateNode
    {
        private PlayCtrl owner;

        private GameVictoryPanel _gameVictoryPanel;

        public override void OnEnter()
        {
            Debug.Log($"{nameof(PlayCtrl_Success)} Enter");

            owner = _owner as PlayCtrl;

            // 处理等级

            owner.Level.Victory();
            _gameVictoryPanel = GameVictoryPanel.Create();
        }

        public override void OnExecute()
        {
            Debug.Log($"{nameof(PlayCtrl_Success)} Execute");
        }

        public override void OnUpdate()
        {
            Debug.Log($"{nameof(PlayCtrl_Success)} Update");
        }

        public override void OnExit()
        {
            Debug.Log($"{nameof(PlayCtrl_Success)} Exit");
        }
    }
}


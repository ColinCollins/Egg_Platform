using Bear.Fsm;
using Bear.UI;
using UnityEngine;

namespace Game.Play
{
    /// <summary>
    /// 游戏开始状态节点
    /// </summary>
    [StateMachineNode(typeof(PlayCtrl), GamePlayStateName.START, true)]
    public class PlayCtrl_Start : StateNode
    {
        private StartPanel startPanel;
        public override void OnEnter()
        {
            Debug.Log($"{nameof(PlayCtrl_Start)} Enter");

            // 创建主界面
            startPanel = StartPanel.Create();
            GameManager.Instance.AssisCamera.enabled = true;
        }

        public override void OnExecute()
        {
            Debug.Log($"{nameof(PlayCtrl_Start)} Execute");
        }

        public override void OnUpdate()
        {
            Debug.Log($"{nameof(PlayCtrl_Start)} Update");
        }

        public override void OnExit()
        {
            Debug.Log($"{nameof(PlayCtrl_Start)} Exit");
            startPanel = null;
        }
    }
}


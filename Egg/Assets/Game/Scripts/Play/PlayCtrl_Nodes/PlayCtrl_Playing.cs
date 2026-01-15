using Bear.EventSystem;
using Bear.Fsm;
using Bear.Logger;
using Game.Events;
using Game.Level;
using UnityEngine;

namespace Game.Play
{
    /// <summary>
    /// 游戏进行中状态节点
    /// </summary>
    [StateMachineNode(typeof(PlayCtrl), GamePlayStateName.PLAYING, false)]
    public class PlayCtrl_Playing : StateNode, IDebuger, IEventSender
    {
        private PlayCtrl owner;

        public override void OnEnter()
        {
            Debug.Log($"{nameof(PlayCtrl_Playing)} Enter");

            owner = _owner as PlayCtrl;

            if (owner.SceneRoot == null)
                owner.SceneRoot = GameObject.Find("Scene").transform;

/*             if (owner.GamePlayPanel == null)
                owner.GamePlayPanel = GamePlayPanel.Create();

            owner.GamePlayPanel.SetData(owner.Level.CurrentLevel); */
            // 测试用关卡
            // owner.CreateLevel(owner.Level.CurrentLevel.ToString("000"));

            GameManager.Instance.AssisCamera.enabled = false;
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


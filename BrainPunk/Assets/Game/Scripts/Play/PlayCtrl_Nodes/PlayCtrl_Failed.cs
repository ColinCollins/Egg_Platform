using Bear.Fsm;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

namespace Game.Play
{
    /// <summary>
    /// 游戏失败状态节点
    /// </summary>
    [StateMachineNode(typeof(PlayCtrl), GamePlayStateName.FAILED, false)]
    public class PlayCtrl_Failed : StateNode
    {
        public override void OnEnter()
        {
            Debug.Log($"{nameof(PlayCtrl_Failed)} Enter");

            var owner = _owner as PlayCtrl;
            if (owner == null) return;

            if (!AudioManager.IsCurrentMusicTag("musicOutGame"))
                AudioManager.PlayMusic("musicOutGame", fadeInSeconds: 8f);
            
            owner.DestroyLevel();
            // 退出关卡
            GameSDKService.Instance.LevelEndEvent(GameSDKService.LevelEndTypeExit);
        }

        public override void OnExecute()
        {
            Debug.Log($"{nameof(PlayCtrl_Failed)} Execute");
        }

        public override void OnUpdate()
        {
            // Debug.Log($"{nameof(PlayCtrl_Failed)} Update");
        }

        public override void OnExit()
        {
            Debug.Log($"{nameof(PlayCtrl_Failed)} Exit");
        }
    }
}


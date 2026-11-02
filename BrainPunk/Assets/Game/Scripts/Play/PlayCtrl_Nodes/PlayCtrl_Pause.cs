using Bear.EventSystem;
using Bear.Fsm;
using Game.Events;
using UnityEngine;

namespace Game.Play
{
    /// <summary>
    /// 游戏暂停状态节点, Time.timeScale 只允许在这里修改
    /// </summary>
    [StateMachineNode(typeof(PlayCtrl), GamePlayStateName.PAUSE, false)]
    public class PlayCtrl_Pause : StateNode, IEventSender
    {
        public override void OnEnter()
        {
            Debug.Log($"{nameof(PlayCtrl_Pause)} Enter");
            this.DispatchEvent(Witness<GamePauseEvent>._);
            // 临时移除，确保广告能够加载到
            // Time.timeScale = 0;
        }

        public override void OnExecute()
        {
            Debug.Log($"{nameof(PlayCtrl_Pause)} Execute");
        }

        public override void OnUpdate()
        {
            // Debug.Log($"{nameof(PlayCtrl_Pause)} Update");
        }

        public override void OnExit()
        {   
            // 继续游戏打点======================================================== 
            // GameSDKService.Instance.LevelStartEvent(GameSDKService.LevelStartTypeContinue);
            
            this.DispatchEvent(Witness<GameResumeEvent>._);
            // Time.timeScale = 1;
            Debug.Log($"{nameof(PlayCtrl_Pause)} Exit, TimeScale: {Time.timeScale}");
        }
    }
}


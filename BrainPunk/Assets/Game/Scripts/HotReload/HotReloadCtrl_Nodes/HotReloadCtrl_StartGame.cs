using Bear.EventSystem;
using Bear.Fsm;
using I2.Loc;
using UnityEngine;

namespace Game.HotReload
{
    /// <summary>
    /// 开始游戏状态节点
    /// </summary>
    [StateMachineNode(typeof(HotReloadCtrl), HotReloadStateName.START_GAME, false)]
    public class HotReloadCtrl_StartGame : StateNode, IEventSender
    {
        private HotReloadCtrl owner;

        public override void OnEnter()
        {
            Debug.Log($"[HotReload] {nameof(HotReloadCtrl_StartGame)} Enter");
            owner = _owner as HotReloadCtrl;
            this.DispatchEvent(Witness<HotReloadEvents.PatchStepsChange>._, LocalizationManager.GetTranslation("U_Loading_Des_Step01"));
            owner.SetFinish();
        }

        public override void OnExecute()
        {
        }

        public override void OnUpdate()
        {
        }

        public override void OnExit()
        {
            Debug.Log($"[HotReload] {nameof(HotReloadCtrl_StartGame)} Exit");
        }
    }
}

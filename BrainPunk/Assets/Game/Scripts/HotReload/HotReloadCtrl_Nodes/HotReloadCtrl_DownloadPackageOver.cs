using Bear.EventSystem;
using Bear.Fsm;
using I2.Loc;
using UnityEngine;

namespace Game.HotReload
{
    /// <summary>
    /// 资源文件下载完毕状态节点
    /// </summary>
    [StateMachineNode(typeof(HotReloadCtrl), HotReloadStateName.DOWNLOAD_PACKAGE_OVER, false)]
    public class HotReloadCtrl_DownloadPackageOver : StateNode, IEventSender
    {
        private HotReloadCtrl owner;
        public override void OnEnter()
        {
            owner = _owner as HotReloadCtrl;
            Debug.Log($"[HotReload] {nameof(HotReloadCtrl_DownloadPackageOver)} Enter");
            this.DispatchEvent(Witness<HotReloadEvents.PatchStepsChange>._, LocalizationManager.GetTranslation("U_Loading_Des_Step06"));
            owner.Machine.Enter(HotReloadStateName.CLEAR_CACHE_BUNDLE);
        }

        public override void OnExecute()
        {
        }

        public override void OnUpdate()
        {
        }

        public override void OnExit()
        {
            Debug.Log($"[HotReload] {nameof(HotReloadCtrl_DownloadPackageOver)} Exit");
        }
    }
}

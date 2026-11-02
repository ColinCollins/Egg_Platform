using Bear.EventSystem;
using Bear.Fsm;
using I2.Loc;
using UnityEngine;
using YooAsset;

namespace Game.HotReload
{
    /// <summary>
    /// 清理未使用的缓存文件状态节点
    /// </summary>
    [StateMachineNode(typeof(HotReloadCtrl), HotReloadStateName.CLEAR_CACHE_BUNDLE, false)]
    public class HotReloadCtrl_ClearCacheBundle : StateNode, IEventSender
    {
        private HotReloadCtrl owner;

        public override void OnEnter()
        {
            Debug.Log($"[HotReload] {nameof(HotReloadCtrl_ClearCacheBundle)} Enter");
            owner = _owner as HotReloadCtrl;
            this.DispatchEvent(Witness<HotReloadEvents.PatchStepsChange>._, LocalizationManager.GetTranslation("U_Loading_Des_Step07"));
            var packageName = owner.PackageName;
            var package = YooAssets.GetPackage(packageName);
            // var operation = package.ClearCacheFilesAsync(EFileClearMode.ClearUnusedBundleFiles);
            var operation = package.ClearUnusedCacheFilesAsync();
            operation.Completed += Operation_Completed;
        }

        public override void OnExecute()
        {
        }

        public override void OnUpdate()
        {
        }

        public override void OnExit()
        {
            Debug.Log($"[HotReload] {nameof(HotReloadCtrl_ClearCacheBundle)} Exit");
        }

        private void Operation_Completed(YooAsset.AsyncOperationBase obj)
        {
            owner.Machine.Enter(HotReloadStateName.START_GAME);
        }
    }
}

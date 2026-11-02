using System.Collections;
using Bear.EventSystem;
using Bear.Fsm;
using I2.Loc;
using UnityEngine;
using YooAsset;

namespace Game.HotReload
{
    /// <summary>
    /// 更新资源清单状态节点
    /// </summary>
    [StateMachineNode(typeof(HotReloadCtrl), HotReloadStateName.UPDATE_PACKAGE_MANIFEST, false)]
    public class HotReloadCtrl_UpdatePackageManifest : StateNode, IEventSender
    {
        private HotReloadCtrl owner;

        public override void OnEnter()
        {
            Debug.Log($"[HotReload] {nameof(HotReloadCtrl_UpdatePackageManifest)} Enter");
            owner = _owner as HotReloadCtrl;
            this.DispatchEvent(Witness<HotReloadEvents.PatchStepsChange>._, LocalizationManager.GetTranslation("U_Loading_Des_Step03"));
            owner.StartCoroutine(UpdateManifest());
        }

        public override void OnExecute()
        {
        }

        public override void OnUpdate()
        {
        }

        public override void OnExit()
        {
            Debug.Log($"[HotReload] {nameof(HotReloadCtrl_UpdatePackageManifest)} Exit");
        }

        private IEnumerator UpdateManifest()
        {
            var packageName = owner.PackageName;
            var packageVersion = owner.PackageVersion;
            var package = YooAssets.GetPackage(packageName);
            var operation = package.UpdatePackageManifestAsync(packageVersion);
            yield return operation;

            if (operation.Status != EOperationStatus.Succeed)
            {
                Debug.LogWarning($"[HotReload] Update package manifest failed: {operation.Error}");
                this.DispatchEvent(Witness<HotReloadEvents.PackageManifestUpdateFailed>._);
                owner.SetFailed();
                yield break;
            }
            else
            {
                owner.Machine.Enter(HotReloadStateName.CREATE_DOWNLOADER);
            }
        }
    }
}

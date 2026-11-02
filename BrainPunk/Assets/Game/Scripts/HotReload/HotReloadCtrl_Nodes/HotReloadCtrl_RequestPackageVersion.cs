using System.Collections;
using Bear.EventSystem;
using Bear.Fsm;
using I2.Loc;
using UnityEngine;
using YooAsset;

namespace Game.HotReload
{
    /// <summary>
    /// 请求资源包版本状态节点
    /// </summary>
    [StateMachineNode(typeof(HotReloadCtrl), HotReloadStateName.REQUEST_PACKAGE_VERSION, false)]
    public class HotReloadCtrl_RequestPackageVersion : StateNode, IEventSender
    {
        private HotReloadCtrl owner;

        public override void OnEnter()
        {
            Debug.Log($"[HotReload] {nameof(HotReloadCtrl_RequestPackageVersion)} Enter");
            owner = _owner as HotReloadCtrl;
            this.DispatchEvent(Witness<HotReloadEvents.PatchStepsChange>._, LocalizationManager.GetTranslation("U_Loading_Des_Step02"));
            owner.StartCoroutine(UpdatePackageVersion());
        }

        public override void OnExecute()
        {
        }

        public override void OnUpdate()
        {
        }

        public override void OnExit()
        {
            Debug.Log($"[HotReload] {nameof(HotReloadCtrl_RequestPackageVersion)} Exit");
        }

        private IEnumerator UpdatePackageVersion()
        {
            var packageName = owner.PackageName;
            var package = YooAssets.GetPackage(packageName);

            // Offline / EditorSimulate 模式不依赖远端版本文件，直接读取本地已激活清单版本。
            if (owner.PlayMode == EPlayMode.OfflinePlayMode || owner.PlayMode == EPlayMode.EditorSimulateMode)
            {
                var localVersion = package.GetPackageVersion();
                if (string.IsNullOrEmpty(localVersion))
                {
                    Debug.LogWarning("[HotReload] Local package version is empty in offline/editor mode.");
                    this.DispatchEvent(Witness<HotReloadEvents.PackageVersionRequestFailed>._);
                    owner.SetFailed();
                    yield break;
                }

                owner.PackageVersion = localVersion;
                Debug.Log($"[HotReload] Use local package version : {owner.PackageVersion}");
                owner.Machine.Enter(HotReloadStateName.UPDATE_PACKAGE_MANIFEST);
                yield break;
            }

            var operation = package.UpdatePackageVersionAsync();
            yield return operation;

            if (operation.Status != EOperationStatus.Succeed)
            {
                Debug.LogWarning($"[HotReload] Request package version failed: {operation.Error}");
                this.DispatchEvent(Witness<HotReloadEvents.PackageVersionRequestFailed>._);
                owner.SetFailed();
                yield break;
            }
            else
            {
                if (string.IsNullOrEmpty(operation.PackageVersion))
                {
                    Debug.LogWarning("[HotReload] Request package version succeed but version is empty.");
                    this.DispatchEvent(Witness<HotReloadEvents.PackageVersionRequestFailed>._);
                    owner.SetFailed();
                    yield break;
                }

                Debug.Log($"[HotReload] Request package version : {operation.PackageVersion}");
                owner.PackageVersion = operation.PackageVersion;
                owner.Machine.Enter(HotReloadStateName.UPDATE_PACKAGE_MANIFEST);
            }
        }
    }
}

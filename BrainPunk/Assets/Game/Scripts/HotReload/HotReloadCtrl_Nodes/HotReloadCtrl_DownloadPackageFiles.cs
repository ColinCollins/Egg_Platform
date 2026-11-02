using System.Collections;
using Bear.EventSystem;
using Bear.Fsm;
using I2.Loc;
using UnityEngine;
using YooAsset;

namespace Game.HotReload
{
    /// <summary>
    /// 下载资源文件状态节点
    /// </summary>
    [StateMachineNode(typeof(HotReloadCtrl), HotReloadStateName.DOWNLOAD_PACKAGE_FILES, false)]
    public class HotReloadCtrl_DownloadPackageFiles : StateNode, IEventSender
    {
        private HotReloadCtrl owner;

        public override void OnEnter()
        {
            Debug.Log($"[HotReload] {nameof(HotReloadCtrl_DownloadPackageFiles)} Enter");
            owner = _owner as HotReloadCtrl;
            this.DispatchEvent(Witness<HotReloadEvents.PatchStepsChange>._, LocalizationManager.GetTranslation("U_Loading_Des_Step05"));
            owner.StartCoroutine(BeginDownload());
        }

        public override void OnExecute()
        {
        }

        public override void OnUpdate()
        {
        }

        public override void OnExit()
        {
            Debug.Log($"[HotReload] {nameof(HotReloadCtrl_DownloadPackageFiles)} Exit");
        }

        private IEnumerator BeginDownload()
        {
            var downloader = owner.Downloader;
            downloader.OnDownloadErrorCallback = (filename, errorData) =>
            {
                this.DispatchEvent(Witness<HotReloadEvents.WebFileDownloadFailed>._, filename, errorData);
            };
            downloader.OnDownloadProgressCallback = (totalDonwloadCount, currentDownloadCount, totalDownloadBytes, currentDownloadBytes) =>
            {
                this.DispatchEvent(Witness<HotReloadEvents.DownloadUpdate>._, 
                    totalDonwloadCount, 
                    currentDownloadCount, 
                    totalDownloadBytes, 
                    currentDownloadBytes);
            };
            downloader.BeginDownload();
            yield return downloader;

            // 检测下载结果
            if (downloader.Status != EOperationStatus.Succeed)
            {
                Debug.LogWarning($"[HotReload] Download failed: {downloader.Error}");
                this.DispatchEvent(Witness<HotReloadEvents.WebFileDownloadFailed>._, string.Empty, downloader.Error);
                owner.SetFailed();
                yield break;
            }

            owner.Machine.Enter(HotReloadStateName.DOWNLOAD_PACKAGE_OVER);
        }
    }
}

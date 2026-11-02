using Bear.EventSystem;
using Bear.Fsm;
using I2.Loc;
using UnityEngine;
using YooAsset;

namespace Game.HotReload
{
    /// <summary>
    /// 创建资源下载器状态节点
    /// </summary>
    [StateMachineNode(typeof(HotReloadCtrl), HotReloadStateName.CREATE_DOWNLOADER, false)]
    public class HotReloadCtrl_CreateDownloader : StateNode, IEventSender
    {
        private HotReloadCtrl owner;

        public override void OnEnter()
        {
            Debug.Log($"[HotReload] {nameof(HotReloadCtrl_CreateDownloader)} Enter");
            owner = _owner as HotReloadCtrl;
            this.DispatchEvent(Witness<HotReloadEvents.PatchStepsChange>._, LocalizationManager.GetTranslation("U_Loading_Des_Step04"));
            CreateDownloader();
        }

        public override void OnExecute()
        {
        }

        public override void OnUpdate()
        {
        }

        public override void OnExit()
        {
            Debug.Log($"[HotReload] {nameof(HotReloadCtrl_CreateDownloader)} Exit");
        }

        void CreateDownloader()
        {
            var packageName = owner.PackageName;
            var package = YooAssets.GetPackage(packageName);
            int downloadingMaxNum = 10;
            int failedTryAgain = 3;
            var downloader = package.CreateResourceDownloader(downloadingMaxNum, failedTryAgain);
            owner.Downloader = downloader;

            if (downloader.TotalDownloadCount == 0)
            {
                Debug.Log("[HotReload] Not found any download files !");
                owner.Machine.Enter(HotReloadStateName.START_GAME);
            }
            else
            {
                int totalDownloadCount = downloader.TotalDownloadCount;
                long totalDownloadBytes = downloader.TotalDownloadBytes;
                Debug.Log($"[HotReload] Found update files: {totalDownloadCount} files, {totalDownloadBytes} bytes");
                this.DispatchEvent(Witness<HotReloadEvents.FoundUpdateFiles>._, totalDownloadCount, totalDownloadBytes);
                owner.Machine.Enter(HotReloadStateName.DOWNLOAD_PACKAGE_FILES);
            }
        }
    }
}

using Cysharp.Threading.Tasks;
using YooAsset;

namespace GF
{
    public class ProcedureHotUpdate: FsmState<App>
    {
        private string _remotePackageVersion;
        private string _updatePackageName;
        private ResourceDownloaderOperation _downloader;
        public override void OnEnter(params object[] args)
        {
            base.OnEnter(args);
            _updatePackageName = args[0].ToString();
            StartUp().Forget();
        }

        private async UniTask StartUp()
        {
            bool updateVersionSuccess = await UpdateVersion();
            if (!updateVersionSuccess)
            {
                return;
            }
            bool updateManifestSuccess = await UpdateManifest();
            if (!updateManifestSuccess)
            {
                return;
            }

            bool hasDownload = await CreateDownloader();
            if (!hasDownload)
            {
                //没有需要更新的文件，直接进入热更层
                App.ChangeProcedure<ProcedureGame>();
                return;
            }

            bool downloadSuccess = await BeginDownload();
            if (!downloadSuccess)
            {
                return;
            }

            await ClearCache().ToUniTask();
            
            App.ChangeProcedure<ProcedureGame>();
        }
        
        //更新版本号
        private async UniTask<bool> UpdateVersion()
        {
            LogKit.E("更新Package Version...");
            await UniTask.Delay(1000);
            
            var package = App.Res.GetPackage(_updatePackageName);
            var operation = package.UpdatePackageVersionAsync();
            await operation.ToUniTask();
            if (operation.Status == EOperationStatus.Succeed)
            {
                _remotePackageVersion = operation.PackageVersion;
                return true;
            }
            else
            {
                LogKit.E(operation.Error);
                return false;
            }
        }

        //更新资源清单文件
        private async UniTask<bool> UpdateManifest()
        {
            LogKit.E("更新资源清单...");
            await UniTask.Delay(1000);

            bool savePackageVersion = true;
            var package = App.Res.GetPackage(_updatePackageName);
            var operation = package.UpdatePackageManifestAsync(_remotePackageVersion, savePackageVersion);
            await operation.ToUniTask();

            if(operation.Status == EOperationStatus.Succeed)
            {
                return true;
            }
            else
            {
                LogKit.E(operation.Error);
                return false;
            }
        }
        
        //创建下载器
        private async UniTask<bool> CreateDownloader()
        {
            LogKit.E("创建补丁下载器...");
            await UniTask.Delay(1000);

            int downloadingMaxNum = 10;
            int failedTryAgain = 3;
            var package = App.Res.GetPackage(_updatePackageName);
            _downloader = package.CreateResourceDownloader(downloadingMaxNum, failedTryAgain);

            if (_downloader.TotalDownloadCount == 0)
            {
                LogKit.E("没有下载文件");
                return false;
            }
            else
            {
                LogKit.E($"需要下载的文件数量：{_downloader.TotalDownloadCount}");

                // 发现新更新文件后，挂起流程系统
                // 注意：开发者需要在下载前检测磁盘空间不足
                // 弹出弹窗提示玩家需要下载的大小和数量，点击确定后继续流程，这里直接跳过
                int totalDownloadCount = _downloader.TotalDownloadCount;
                long totalDownloadBytes = _downloader.TotalDownloadBytes;
                return true;
            }
        }
        
        //下载文件
        private async UniTask<bool> BeginDownload()
        {
            LogKit.E("开始下载补丁文件...");
            await UniTask.Delay(1000);
            _downloader.OnDownloadProgressCallback=
                delegate(int count, int downloadCount, long bytes, long downloadBytes)
                {
                    //todo:派发下载进度事件
                };
            _downloader.BeginDownload();
            await _downloader.ToUniTask();
            

            // 检测下载结果
            if (_downloader.Status != EOperationStatus.Succeed)
            {
                LogKit.E(_downloader.Error);
                return false;
            }
            else
            {
                return true;
            }
        }
        
        //清除无用的缓存文件
        private ClearUnusedCacheFilesOperation ClearCache()
        {
            var package = App.Res.GetPackage(_updatePackageName);
            ClearUnusedCacheFilesOperation operation = package.ClearUnusedCacheFilesAsync();
            return operation;
        }
    }
}
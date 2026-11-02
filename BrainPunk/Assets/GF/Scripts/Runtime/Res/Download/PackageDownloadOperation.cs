using System;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace GF
{
    /// <summary>
    /// 包下载操作
    /// </summary>
    public class PackageDownloadOperation: GameAsyncOperation
    {
        private enum ESteps
        {
            None,
            TryInitPackage,         // 尝试初始化包
            PackageInitializing,    // 包初始化中
            UpdateVersion,          // 更新版本号
            UpdateManifest,         // 更新清单
            CreateLoader,           // 创建下载器
            BeginDownload,          // 开始下载
            Downloading,            // 下载中
            ClearCache,             // 清理缓存
            Done,                   // 完成
        }
        
        private string _packageName;    // 下载包名
        private string _downloadGeneration;    // 下载版本号
        private ResourcePackage _resourcePackage;   // 资源包
        private ESteps _step = ESteps.None;     // 当前步骤
        private string _remotePackageVersion;   // 远程版本号
        private InitializationOperation _initializationOperation;   // 初始化操作
        private UpdatePackageVersionOperation _updatePackageVersionOperation;   // 更新版本号操作
        private UpdatePackageManifestOperation _updatePackageManifestOperation;  // 更新清单操作
        private ResourceDownloaderOperation _downloaderOperation;   // 下载器操作
        private ClearUnusedCacheFilesOperation _clearUnusedCacheFilesOperation;  // 清理缓存操作
        private Action<int, int, long, long> _onProgress;

        /// <summary>
        /// 下载进度事件
        /// </summary>
        public Action<int, int, long, long> OnProgress
        {
            get => _onProgress;
            set => _onProgress = value;
        }
        
        public PackageDownloadOperation(string packageName, string generation, Action<int, int, long, long> onProgress)
        {
            _packageName = packageName;
            _downloadGeneration = generation;
            _onProgress = onProgress;
        }
        
        protected override void OnStart()
        {
            _step = ESteps.TryInitPackage;
            _remotePackageVersion = null;
            _downloaderOperation = null;
        }

        protected override void OnUpdate()
        {
            if (_step == ESteps.None || _step == ESteps.Done)
            {
                return;
            }

            //初始化资源包
            if (_step == ESteps.TryInitPackage)
            {
                var package = App.Res.TryGetPackage(_packageName);
                if (package == null)
                {

                    _initializationOperation = App.Res.InitHostPackage(_packageName, _downloadGeneration);
                    _step = ESteps.PackageInitializing;
                }
                else
                {
                    _resourcePackage = package;
                    _step = ESteps.UpdateVersion;
                }
            }

            //等待初始化完成
            if (_step == ESteps.PackageInitializing)
            {
                if (!_initializationOperation.IsDone)
                {
                    return;
                }

                if (_initializationOperation.Status == EOperationStatus.Succeed)
                {
                    _step = ESteps.UpdateVersion;
                }
                else if (_initializationOperation.Status == EOperationStatus.Failed)
                {
                    //初始化失败销毁package
                    App.Res.DestroyPackage(_packageName);
                    _step = ESteps.Done;
                    Status = EOperationStatus.Failed;
                }
            }

            //更新版本号
            if (_step == ESteps.UpdateVersion)
            {
                if (_updatePackageVersionOperation == null)
                {
                    LogKit.I("更新Package Version...");
                    _updatePackageVersionOperation = _resourcePackage.UpdatePackageVersionAsync(false);
                }

                if (!_updatePackageVersionOperation.IsDone)
                {
                    return;
                }
                
                if(_updatePackageVersionOperation.Status == EOperationStatus.Succeed)
                {
                    _remotePackageVersion = _updatePackageVersionOperation.PackageVersion;
                    _step = ESteps.UpdateManifest;
                }
                else
                {
                    LogKit.E(_updatePackageVersionOperation.Error);
                    _step = ESteps.Done;
                    Status = EOperationStatus.Failed;
                    Error = _updatePackageVersionOperation.Error;
                }
            }

            //更新资源清单文件
            if (_step == ESteps.UpdateManifest)
            {
                if (_updatePackageManifestOperation == null)
                {
                    LogKit.I("更新资源清单...");
                    bool savePackageVersion = true;     //是否保存版本号
                    _updatePackageManifestOperation = _resourcePackage.UpdatePackageManifestAsync(_remotePackageVersion, savePackageVersion);
                }
                if (!_updatePackageManifestOperation.IsDone)
                {
                    return;
                }
                
                if(_updatePackageManifestOperation.Status == EOperationStatus.Succeed)
                {
                    _step = ESteps.CreateLoader;
                }
                else
                {
                    LogKit.E(_updatePackageManifestOperation.Error);
                    _step = ESteps.Done;
                    Status = EOperationStatus.Failed;
                    Error = _updatePackageManifestOperation.Error;
                }
            }
            
            //创建下载器
            if(_step == ESteps.CreateLoader)
            {
                LogKit.I("创建补丁下载器...");
                int downloadingMaxNum = 10;
                int failedTryAgain = 3;
                _downloaderOperation = _resourcePackage.CreateResourceDownloader(downloadingMaxNum, failedTryAgain);

                if (_downloaderOperation.TotalDownloadCount == 0)
                {
                    LogKit.I("没有下载文件");
                    _step = ESteps.None;
                    Status = EOperationStatus.Succeed;
                }
                else
                {
                    LogKit.I($"需要下载的文件数量：{_downloaderOperation.TotalDownloadCount}");

                    // 发现新更新文件后，挂起流程系统
                    // 注意：开发者需要在下载前检测磁盘空间不足
                    // 弹出弹窗提示玩家需要下载的大小和数量，点击确定后继续流程，这里直接跳过
                    int totalDownloadCount = _downloaderOperation.TotalDownloadCount;
                    long totalDownloadBytes = _downloaderOperation.TotalDownloadBytes;

                    _step = ESteps.BeginDownload;
                }
            }
            
            //开始下载
            if(_step == ESteps.BeginDownload)
            {
                LogKit.I("开始下载补丁文件...");
                _downloaderOperation.OnDownloadProgressCallback =
                    delegate(int count, int downloadCount, long bytes, long downloadBytes)
                    {
                        _onProgress?.Invoke(count, downloadCount, bytes, downloadBytes);
                    };
                _downloaderOperation.BeginDownload();

                _step = ESteps.Downloading;
            }
            
            //等待下载完成
            if(_step == ESteps.Downloading)
            {
                if (!_downloaderOperation.IsDone)
                {
                    return;
                }
                
                if (_downloaderOperation.Status == EOperationStatus.Succeed)
                {
                    LogKit.I("下载完成");
                    _step = ESteps.ClearCache;
                    
                }
                else if (_downloaderOperation.Status == EOperationStatus.Failed)
                {
                    LogKit.E(_downloaderOperation.Error);
                    _step = ESteps.Done;
                    Status = EOperationStatus.Failed;
                    Error = _downloaderOperation.Error;
                }
            }

            //清除无用的缓存文件
            if (_step == ESteps.ClearCache)
            {
                if (_clearUnusedCacheFilesOperation == null)
                {
                    _clearUnusedCacheFilesOperation = _resourcePackage.ClearUnusedCacheFilesAsync();
                }

                if (!_clearUnusedCacheFilesOperation.IsDone)
                {
                    return;
                }
                
                if(_clearUnusedCacheFilesOperation.Status == EOperationStatus.Succeed)
                {
                    _step = ESteps.Done;
                    Status = EOperationStatus.Succeed;
                }
                else
                {
                    //这里不会走到，但是还是写一下，但是还是标记成功，因为已经下载完成
                    LogKit.E(_clearUnusedCacheFilesOperation.Error);
                    _step = ESteps.Done;
                    Status = EOperationStatus.Succeed;
                }
            }
        }

        protected override void OnAbort()
        {
            if (_step != ESteps.Done)
            {
                Status = EOperationStatus.Failed;
            }

            if (_downloaderOperation != null && !_downloaderOperation.IsDone)
            {
                _downloaderOperation.CancelDownload();
            }
        }
        
        /// <summary>
        /// 获取当前下载包版本号
        /// </summary>
        /// <returns></returns>
        public string GetVersion()
        {
            return _downloadGeneration;
        }

        /// <summary>
        /// 中止下载
        /// </summary>
        public void Abort()
        {
            if (_step != ESteps.Done)
            {
                Status = EOperationStatus.Failed;
            }

            if (_downloaderOperation != null && !_downloaderOperation.IsDone)
            {
                _downloaderOperation.CancelDownload();
            }
        }
    }
}
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace GF
{
    public class PackageDownloadMgr : Singleton<PackageDownloadMgr>
    {
        private Dictionary<string, PackageDownloadOperation> _downloadOperations = new();
        
        private Dictionary<string, InitializationOperation> _downloadInitializationOperation = new Dictionary<string, InitializationOperation>();

        
        /// <summary>
        /// 当前包是否已经被缓存 需要比对version
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="generation"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public bool IsCachedPackage(string packageName, string generation, string version)
        {
            ResourcePackage package = App.Res.TryGetPackage(packageName);
            if (null == package || package.InitializeStatus != EOperationStatus.Succeed)
            {
                return false;
            }

            if (null != version && version.Equals(""))
            {
                version = App.LocalStorage.GetData($"{Define.LocalStorage.GENERATION_TO_YOOASSETS}{generation}", "");
            }

            return package.IsAllCache(version);
        }


        /// <summary>
        /// 通过generation和version下载资源包
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="generation">资源包generation</param>
        /// <param name="version">判断缓存的version，version为空字符串会下载最新包，如果为null会检查使用cache的，如果没有全部cache完成就去下载</param>
        /// <param name="onSuccess"></param>
        /// <param name="onFail"></param>
        /// <param name="onProgress"></param>
        public async UniTask DownLoadPackage(string packageName, string generation,string version, System.Action onSuccess,
            System.Action onFail, System.Action<float> onProgress)
        {
            // 是否为本地资源模式
            BuiltinPackageElement element = GameSettingData.Setting.GetDefaultPackageElement(packageName);
            bool isSimulateMode = element != null && element.playMode == EPlayMode.EditorSimulateMode;

            // 本地资源模式，无需验证
            if (!isSimulateMode)
            {
                if (string.IsNullOrEmpty(generation))
                {
                    LogKit.E("generation is null");
                    onFail?.Invoke();
                    return;
                }
                if (string.IsNullOrEmpty(version))
                {
                    string originVersion = version;
                    version = App.LocalStorage.GetData($"{Define.LocalStorage.GENERATION_TO_YOOASSETS}{generation}", "");
                    if (originVersion == null)
                    {
                        version = null;
                    }
                    LogKit.I($"获取本地版本号：{version}, {generation}, {packageName}");
                }
            }

            ResourcePackage package = App.Res.TryGetPackage(packageName);
            if (package == null)
            {
                if (isSimulateMode)
                {
                    //Editor模式，使用本地资源
                    InitializationOperation initializationOperation = App.Res.InitSimulatePackage(packageName);
                    await initializationOperation.ToUniTask();
                }
                else
                {
                    InitializationOperation initializationOperation = App.Res.InitHostPackage(packageName, generation);
                    _downloadInitializationOperation.Add(packageName, initializationOperation);
                    await initializationOperation.ToUniTask();
                }
                package = App.Res.TryGetPackage(packageName);
            }
            else if(package.InitializeStatus != EOperationStatus.Succeed)
            {
                if (_downloadInitializationOperation.TryGetValue(packageName, out InitializationOperation initializationOperation))
                {
                    await initializationOperation.ToUniTask();
                }
            }

            if (isSimulateMode)
            {
                onProgress?.Invoke(1);
                onSuccess?.Invoke();
                return;
            }

            // 防止多次下载同一个包，并使用了不同的Generation，此处对最新的Generation进行保存
            ((RemoteServices)((HostPlayModeImpl)package.PlayModeImpl).RemoteServices).SetGeneration(generation);

            //初始化成功后，移除初始化操作
            _downloadInitializationOperation.Remove(packageName);

            if (package.IsAllCache(version))
            {
                onSuccess?.Invoke();
                return;
            }

            UniTaskCompletionSource taskCompletionSource = new UniTaskCompletionSource();
            DownLoadPackage(packageName, generation, delegate(AsyncOperationBase op)
            {
                if (op.Status == EOperationStatus.Succeed)
                {
                    LogKit.I($"设置版本号：{package.GetPackageVersion()}, {generation}");
                    App.LocalStorage.SetData($"{Define.LocalStorage.GENERATION_TO_YOOASSETS}{generation}", package.GetPackageVersion());
                    onSuccess?.Invoke();
                    taskCompletionSource.TrySetResult();
                }
                else
                {
                    onFail?.Invoke();
                    taskCompletionSource.TrySetException(new Exception(op.Error ?? "Fail"));
                }
            }, delegate(int i, int i1, long arg3, long arg4)
            {
                onProgress?.Invoke(arg4 / (float) arg3);
            });

            await taskCompletionSource.Task;
        }

        /// <summary>
        /// 下载资源包
        /// </summary>
        /// <param name="packageName">下载包名</param>
        /// <param name="generation">下载版本号</param>
        /// <param name="yooVersion">下载版本号</param>
        /// <param name="downloadMode"></param>
        /// <param name="onCompleted">下载完成回调</param>
        /// <param name="onProgress">下载进度回调，参数1,2,3,4，分别是总文件数量，已下载数量，总文件大小(bytes)，已下载大小(bytes)</param>
        public void DownLoadPackage(string packageName, string generation, Action<AsyncOperationBase> onCompleted = null, Action<int, int, long, long> onProgress = null)
        {
            if (_downloadOperations.TryGetValue(packageName, out PackageDownloadOperation packageDownloadOperation))
            {
                LogKit.E($"已经存在下载任务：packageName{packageName}, downloading version{packageDownloadOperation.GetVersion()}, new version{generation}");
                packageDownloadOperation.Completed += (op) =>
                {
                    onCompleted?.Invoke(op);
                };

                if (onProgress != null)
                {
                    packageDownloadOperation.OnProgress += onProgress;
                }

                return;
            }
            
            var operation = new PackageDownloadOperation(packageName, generation, onProgress);
            operation.Completed += (op) =>
            {
                _downloadOperations.Remove(packageName);
                onCompleted?.Invoke(op);
            };

            YooAssets.StartOperation(operation);
            _downloadOperations.Add(packageName, operation);
        }
        
        /// <summary>
        /// 中止下载
        /// </summary>
        /// <param name="packageName"></param>
        public void AbortDownload(string packageName)
        {
            if (_downloadOperations.TryGetValue(packageName, out var operation))
            {
                operation.Abort();
                _downloadOperations.Remove(packageName);
            }
        }
        
        /// <summary>
        /// 中止所有下载
        /// </summary>
        public void AbortAllDownload()
        {
            foreach (var operation in _downloadOperations.Values)
            {
                operation.Abort();
            }
            _downloadOperations.Clear();
        }
    }
}
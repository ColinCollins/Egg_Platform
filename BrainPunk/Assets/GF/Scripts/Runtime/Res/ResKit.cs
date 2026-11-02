using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using YooAsset;
using Object = UnityEngine.Object;
using Path = System.IO.Path;

namespace GF
{
    /// <summary>
    /// 资源管理工具
    /// </summary>
    public class ResKit
    {
        private ResourcePackage _defaultPackage;
        private Dictionary<string, Dictionary<int, AssetHandle>> _assetCache;
        private Dictionary<string, Dictionary<int, RawFileHandle>> _rawFileCache;

        public void Init(YooAsset.ILogger iLogger)
        {
            YooAssets.Initialize(iLogger);
            _assetCache = new Dictionary<string, Dictionary<int, AssetHandle>>();
            _rawFileCache = new Dictionary<string, Dictionary<int, RawFileHandle>>();
        }

        /// <summary>
        /// 初始化Host模式资源包
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="generation"></param>
        /// <param name="cdn"></param>
        /// <param name="cdnFallback"></param>
        /// <returns></returns>
        public InitializationOperation InitHostPackage(string packageName, string generation)

        {
            ResourcePackage resourcePackage = App.Res.TryGetPackage(packageName);
            if (resourcePackage != null)
            {
                return null;
            }
            
            ResourcePackage package = App.Res.CreatePackage(packageName);
            var initParameters = new HostPlayModeParameters();
            initParameters.BuildinQueryServices = new BuildinQueryServices();
            initParameters.DecryptionServices = new DecryptoHTXOR();
            
            initParameters.RemoteServices = new RemoteServices(GameSettingData.Setting.GetCdnUrl(), GameSettingData.Setting.GetCdnUrlFallback(), generation, package.PackageName);
            initParameters.BreakpointResumeFileSize = 1024 * 1024;      //超过1M启用断点续传下载
            InitializationOperation initializationOperation = package.InitializeAsync(initParameters);
            return initializationOperation;
        }

        /// <summary>
        ///  初始化模拟模式资源包
        /// </summary>
        /// <param name="packageName"></param>
        /// <returns></returns>
        public InitializationOperation InitSimulatePackage(string packageName)
        {
            ResourcePackage resourcePackage = App.Res.TryGetPackage(packageName);
            if (resourcePackage != null)
            {
                return null;
            }
            
            ResourcePackage package = App.Res.CreatePackage(packageName);

            EditorSimulateModeParameters createParameters = new EditorSimulateModeParameters();
            createParameters.SimulateManifestFilePath = EditorSimulateModeHelper.SimulateBuild(GameSettingData.Setting.buildPipeline, package.PackageName);
            InitializationOperation initializationOperation = package.InitializeAsync(createParameters);
            return initializationOperation;
        }

        /// <summary>
        /// 初始化离线模式资源包
        /// </summary>
        /// <param name="packageName"></param>
        /// <returns></returns>
        public InitializationOperation InitOfflinePackage(string packageName)
        {
            ResourcePackage resourcePackage = App.Res.TryGetPackage(packageName);
            if (resourcePackage != null)
            {
                return null;
            }
            
            ResourcePackage package = App.Res.CreatePackage(packageName);

            OfflinePlayModeParameters createParameters = new OfflinePlayModeParameters();
            createParameters.DecryptionServices = new DecryptoHTXOR();
            InitializationOperation initializationOperation = package.InitializeAsync(createParameters);
            
            return initializationOperation;
        }

        /// <summary>
        /// 设置默认的资源包
        /// </summary>
        public void SetDefaultPackage(ResourcePackage package)
        {
            _defaultPackage = package;
        }
        
        /// <summary>
        /// 创建资源包
        /// </summary>
        /// <param name="packageName">包名</param>
        /// <returns>返回资源包</returns>
        public ResourcePackage CreatePackage(string packageName)
        {
            return YooAssets.CreatePackage(packageName);
        }

        /// <summary>
        /// 获得资源包
        /// </summary>
        /// <param name="packageName">包名</param>
        /// <returns>返回资源包</returns>
        public ResourcePackage GetPackage(string packageName)
        {
            return YooAssets.GetPackage(packageName);
        }

        /// <summary>
        /// 尝试获得资源包
        /// </summary>
        /// <param name="packageName">包名</param>
        /// <returns>返回资源包</returns>
        public ResourcePackage TryGetPackage(string packageName)
        {
            return YooAssets.TryGetPackage(packageName);
        }

        /// <summary>
        /// 销毁资源包
        /// </summary>
        /// <param name="packageName">资源包名称</param>
        /// <param name="isDeleteCacheFile">是否删除缓存文件, 默认不删除</param>
        public void DestroyPackage(string packageName, bool isDeleteCacheFile = false)
        {
            if (isDeleteCacheFile)
            {
                ClearPackageSandbox(packageName);
            }
            YooAssets.DestroyPackage(packageName);
        }
        
        /// <summary>
        /// 添加Cache
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="handle"></param>
        /// <typeparam name="T"></typeparam>
        private void AddCache<T>(string tag, T handle) where T: HandleBase
        {
            if (handle == null)
            {
                LogKit.E("handle is null");
                return;
            }
            
            if (handle is AssetHandle assetHandle)
            {
                if (!_assetCache.TryGetValue(tag, out var dic))
                {
                    dic = new Dictionary<int, AssetHandle>();
                    _assetCache[tag] = dic;
                }

                int code = handle.GetHashCode();
                if (!dic.ContainsKey(code))
                {
                    dic.Add(code, assetHandle);
                }
            }
            else if (handle is RawFileHandle rawFileHandle)
            {
                if (!_rawFileCache.TryGetValue(tag, out var dic))
                {
                    dic = new Dictionary<int, RawFileHandle>();
                    _rawFileCache[tag] = dic;
                }

                int code = handle.GetHashCode();
                if (!dic.ContainsKey(code))
                {
                    dic.Add(code, rawFileHandle);
                }
            }
        }

        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="tag">标签</param>
        /// <param name="packageName">资源包名</param>
        /// <typeparam name="T">加载类型</typeparam>
        /// <returns>返回加载资源</returns>
        public T LoadAsset<T>(string path, string tag, string packageName = null) where T : Object
        {
            ResourcePackage resourcePackage = null;
            if (!string.IsNullOrEmpty(packageName))
            {
                resourcePackage = TryGetPackage(packageName);
            }

            return LoadAsset<T>(path, tag, resourcePackage);
        }

        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="tag">标签</param>
        /// <param name="resourcePackage">资源包</param>
        /// <typeparam name="T">加载类型</typeparam>
        /// <returns>返回加载资源</returns>
        private T LoadAsset<T>(string path, string tag,ResourcePackage resourcePackage = null) where T: Object
        {
            resourcePackage = PreCheck(resourcePackage);
            AssetHandle handle = resourcePackage?.LoadAssetSync<T>(path);
            AddCache(tag, handle);
            return handle?.GetAssetObject<T>();
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="tag">标签</param>
        /// <param name="packageName">资源包名</param>
        /// <typeparam name="T">加载类型</typeparam>
        /// <returns>返回加载资源</returns>
        public async UniTask<T> LoadAssetAsync<T>(string path, string tag, string packageName = null)
            where T : Object
        {
            ResourcePackage resourcePackage = null;
            if (!string.IsNullOrEmpty(packageName))
            {
                resourcePackage = TryGetPackage(packageName);
            }

            return await LoadAssetAsync<T>(path, tag, resourcePackage);
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="tag">标签</param>
        /// <param name="resourcePackage">资源包</param>
        /// <typeparam name="T">加载类型</typeparam>
        /// <returns>返回加载资源</returns>
        private async UniTask<T> LoadAssetAsync<T>(string path, string tag, ResourcePackage resourcePackage = null) where T: Object
        {
            resourcePackage = PreCheck(resourcePackage);
            AssetHandle handle = resourcePackage?.LoadAssetAsync<T>(path);
            AddCache(tag, handle);
            await handle.ToUniTask();
            return handle?.GetAssetObject<T>();
        }

        /// <summary>
        /// 同步加载原生文件字节数组
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="tag">标签</param>
        /// <param name="resourcePackage">资源包</param>
        /// <returns>返回资源</returns>
        public byte[] LoadRawData(string path, string tag, ResourcePackage resourcePackage = null)
        {
            RawFileHandle handle = LoadRawInner(path, tag, resourcePackage);
            return handle?.GetRawFileData();
        }
        
        /// <summary>
        /// 同步加载原生文件文本
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="tag">标签</param>
        /// <param name="resourcePackage">资源包</param>
        /// <returns>返回资源</returns>
        public string LoadRawText(string path, string tag, ResourcePackage resourcePackage = null)
        {
            RawFileHandle handle = LoadRawInner(path, tag, resourcePackage);
            return handle?.GetRawFileText();
        }

        /// <summary>
        /// 异步加载原生文件字节数组
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="tag">标签</param>
        /// <param name="resourcePackage">资源包</param>
        /// <returns>返回资源</returns>
        public async UniTask<byte[]> LoadRawDataAsync(string path, string tag,
            ResourcePackage resourcePackage = null)
        {
            RawFileHandle handle = await LoadRawAsyncInner(path, tag, resourcePackage);
            return handle.GetRawFileData();
        }
        
        /// <summary>
        /// 异步加载原生文件文本
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="tag">标签</param>
        /// <param name="resourcePackage">资源包</param>
        /// <returns>返回资源</returns>
        public async UniTask<string> LoadRawTextAsync(string path, string tag,
            ResourcePackage resourcePackage = null)
        {
            RawFileHandle handle = await LoadRawAsyncInner(path, tag, resourcePackage);
            return handle.GetRawFileText();
        }

        /// <summary>
        /// 内部原生资源加载
        /// </summary>
        /// <param name="path"></param>
        /// <param name="tag"></param>
        /// <param name="resourcePackage"></param>
        /// <returns></returns>
        private async UniTask<RawFileHandle> LoadRawAsyncInner(string path, string tag,
            ResourcePackage resourcePackage = null)
        {
            resourcePackage = PreCheck(resourcePackage);
            RawFileHandle handle = resourcePackage?.LoadRawFileAsync(path);
            AddCache(tag, handle);
            await handle.ToUniTask();
            return handle;
        }

        /// <summary>
        /// 内部原生加载
        /// </summary>
        /// <param name="path"></param>
        /// <param name="tag"></param>
        /// <param name="resourcePackage"></param>
        /// <returns></returns>
        private RawFileHandle LoadRawInner(string path, string tag, ResourcePackage resourcePackage = null)
        {
            resourcePackage = PreCheck(resourcePackage);
            RawFileHandle handle = resourcePackage.LoadRawFileSync(path);
            AddCache(tag, handle);
            return handle;
        }

        /// <summary>
        /// 加载场景
        /// </summary>
        /// <param name="path">场景路径</param>
        /// <param name="loadSceneMode">加载模式</param>
        /// <param name="suspendLoad">是否自动加载完成挂起</param>
        /// <param name="priority">优先级</param>
        /// <param name="resourcePackage">资源包</param>
        /// <returns>返回加载场景</returns>
        public async UniTask<Scene> LoadSceneAsync(string path, LoadSceneMode loadSceneMode = LoadSceneMode.Single, bool suspendLoad = false, uint priority = 100, ResourcePackage resourcePackage = null)
        {
            resourcePackage = PreCheck(resourcePackage);
            SceneHandle handle = resourcePackage?.LoadSceneAsync(path, loadSceneMode, suspendLoad, priority: priority);
            await handle.ToUniTask();
            return handle.SceneObject;
        }

        /// <summary>
        /// 根据tag释放资源
        /// </summary>
        /// <param name="tag">标签</param>
        public void ReleaseAsset(string tag, string packageName = null)
        {
            if (_assetCache.TryGetValue(tag, out var dic))
            {
                using var enumerator = dic.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    enumerator.Current.Value?.Release();
                }

                _assetCache.Remove(tag);
            }
            
            if (_rawFileCache.TryGetValue(tag, out var rawDic))
            {
                using var enumerator = rawDic.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    enumerator.Current.Value?.Release();
                }

                _rawFileCache.Remove(tag);
            }

            if (!string.IsNullOrEmpty(packageName))
            {
                ResourcePackage package = TryGetPackage(packageName);
                if (package != null)
                {
                    UnloadUnusedAssets(package);
                }
            }
            else
            {
                UnloadUnusedAssets();
            }
        }

        /// <summary>
        /// 真正卸载资源
        /// </summary>
        /// <param name="resourcePackage"></param>
        private void UnloadUnusedAssets(ResourcePackage resourcePackage = null)
        {
            resourcePackage = PreCheck(resourcePackage);
            resourcePackage?.UnloadUnusedAssets();
        }

        /// <summary>
        /// 删除所有缓存文件 by 包名
        /// </summary>
        /// <param name="packageName"></param>
        private void ClearPackageSandbox(string packageName)
        {
            ResourcePackage resourcePackage = TryGetPackage(packageName);
            if (resourcePackage != null)
            {
                ClearPackageSandbox(resourcePackage);
            }
        }

        /// <summary>
        /// 删除所有缓存文件 by ResourcePackage
        /// </summary>
        /// <param name="resourcePackage"></param>
        private void ClearPackageSandbox(ResourcePackage resourcePackage)
        {
            try
            {
                resourcePackage.ClearPackageSandbox();
            }
            catch (Exception e)
            {
                LogKit.E(e);
            }
        }
        
        /// <summary>
        /// 获取YooAsset的存储根目录
        /// </summary>
        /// <returns></returns>
        private static string GetDefaultSandboxRoot()
        {
#if UNITY_EDITOR
            // 注意：为了方便调试查看，编辑器下把存储目录放到项目里。
            string projectPath = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            projectPath = PathUtility.RegularPath(projectPath);
            return PathUtility.Combine(projectPath, YooAssetSettingsData.Setting.DefaultYooFolderName);
#elif UNITY_STANDALONE
            return PathUtility.Combine(UnityEngine.Application.dataPath, YooAssetSettingsData.Setting.DefaultYooFolderName);
#else
            return PathUtility.Combine(UnityEngine.Application.persistentDataPath, YooAssetSettingsData.Setting.DefaultYooFolderName);	
#endif
        }
        
        /// <summary>
        /// 清理YooAsset所有缓存
        /// </summary>
        /// <param name="packagePrefix">需要删除的Package前缀</param>
        /// <returns></returns>
        public bool ClearAllCache(string packagePrefix)
        {
            try
            {
                string yooAssetCacheRoot = GetDefaultSandboxRoot();
                LogKit.I($"yooAssetCacheRoot: {yooAssetCacheRoot}");
                // 获取yooAssetCacheRoot文件夹内的所有文件夹
                if (Directory.Exists(yooAssetCacheRoot))
                {
                    string[] dirs = Directory.GetDirectories(yooAssetCacheRoot);
                    foreach (string dir in dirs)
                    {
                        // 获取文件夹的名称
                        string dirName = Path.GetFileName(dir);
                        if (dirName != null && dirName.StartsWith(packagePrefix))
                        {
                            LogKit.I($"Delete: {dirName}");
                            Directory.Delete(dir, true);
                        }
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                LogKit.E(e);
            }

            return false;
        }
        
        /// <summary>
        /// 预先检查
        /// </summary>
        /// <param name="resourcePackage"></param>
        /// <returns></returns>
        private ResourcePackage PreCheck(ResourcePackage resourcePackage)
        {
            resourcePackage ??= _defaultPackage;

            if (resourcePackage == null)
            {
                LogKit.E("需要设置默认ResourcePackage，或者传入ResourcePackage");
            }

            return resourcePackage;
        }

        /// <summary>
        /// 释放资源工具
        /// </summary>
        public void Destroy()
        {
            _assetCache.Clear();
            _rawFileCache.Clear();
            YooAssets.Destroy();
        }
    }
}
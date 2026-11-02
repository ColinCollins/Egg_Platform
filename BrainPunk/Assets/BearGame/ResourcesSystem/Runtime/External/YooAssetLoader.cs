#if YOOASSET || YOOASSET_ENABLED

using System;
using System.Collections.Concurrent;
using System.Linq;
using UnityEngine;
using YooAsset;

namespace Bear.ResourceSystem.External
{
    /// <summary>
    /// YooAsset 加载策略
    /// 放在 External 文件夹，表示对外部库的适配
    /// </summary>
    public partial class YooAssetLoader : IResourceLoader
    {
        #region Handle Types

        private readonly struct YooAssetHandleKey : IEquatable<YooAssetHandleKey>
        {
            public readonly string Path;
            public readonly Type AssetType;

            public YooAssetHandleKey(string path, Type assetType)
            {
                Path = path;
                AssetType = assetType;
            }

            public bool Equals(YooAssetHandleKey other)
            {
                return string.Equals(Path, other.Path, StringComparison.Ordinal)
                    && AssetType == other.AssetType;
            }

            public override bool Equals(object obj)
            {
                return obj is YooAssetHandleKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Path != null ? Path.GetHashCode() : 0) * 397)
                        ^ (AssetType != null ? AssetType.GetHashCode() : 0);
                }
            }
        }

        #endregion

        #region Runtime State

        public string LoaderName => "YooAssetLoader";
        public int Priority { get; set; } = 10;
        public bool IsAvailable
        {
            get
            {
                ResourcePackage package = GetPackage();
                return package != null
                    && package.InitializeStatus == EOperationStatus.Succeed;
            }
        }

        private readonly string _packageName;
        private ResourcePackage _package;
        private readonly ConcurrentDictionary<YooAssetHandleKey, AssetHandle> _handleCache = new();

        private string EffectivePackageName =>
            string.IsNullOrEmpty(_packageName) ? "DefaultPackage" : _packageName;

        #endregion

        /// <summary>
        /// 用途：创建指定 YooAsset Package 的 Bear 资源加载器。
        /// 调用边界：Package 应已由项目启动流程创建并初始化成功。
        /// 注意事项：本适配器不负责初始化、下载或更新 Package。
        /// </summary>
        public YooAssetLoader(string packageName = null)
        {
            _packageName = packageName;
        }

        #region Package

        private ResourcePackage GetPackage()
        {
            if (!YooAssets.Initialized)
                return null;

            ResourcePackage currentPackage = YooAssets.TryGetPackage(EffectivePackageName);
            if (!ReferenceEquals(_package, currentPackage))
                _package = currentPackage;

            return _package;
        }

        #endregion

        #region 同步加载

        /// <summary>
        /// 用途：确认已初始化的 Package 清单中是否存在指定资源位置。
        /// 调用边界：只负责查询，不触发下载、加载或缓存清理。
        /// 注意事项：Package 未初始化成功时始终返回 false。
        /// </summary>
        public bool CanLoad<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path) || !IsAvailable)
                return false;

            try
            {
                return GetPackage().CheckLocationValid(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[YooAssetLoader] Failed to check location. path:{path}, package:{EffectivePackageName}, error:{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 用途：同步加载资源，并持有 AssetHandle 直到 BearResources 主动释放。
        /// 调用边界：应在 Unity 主线程使用；调用前 Package 必须初始化成功。
        /// 注意事项：成功加载后需通过 Release(path) 或 ReleaseAll() 释放。
        /// </summary>
        public T Load<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var package = GetPackage();
            if (package == null || package.InitializeStatus != EOperationStatus.Succeed)
            {
                Debug.LogError($"[YooAssetLoader] Package is not ready: {EffectivePackageName}");
                return null;
            }

            var key = new YooAssetHandleKey(path, typeof(T));
            if (TryGetCompletedAsset(key, out T cachedAsset))
                return cachedAsset;

            if (TryGetValidHandle(key, out AssetHandle loadingHandle))
            {
                loadingHandle.WaitForAsyncComplete();
                if (loadingHandle.IsValid
                    && loadingHandle.Status == EOperationStatus.Succeed)
                {
                    return loadingHandle.GetAssetObject<T>();
                }

                if (TryRemoveHandle(key, loadingHandle))
                    ReleaseHandleSafely(loadingHandle);
            }

            AssetHandle handle = package.LoadAssetSync<T>(path);
            if (handle != null
                && handle.IsValid
                && handle.Status == EOperationStatus.Succeed)
            {
                T asset = handle.GetAssetObject<T>();
                if (asset != null)
                {
                    if (_handleCache.TryAdd(key, handle))
                        return asset;

                    ReleaseHandleSafely(handle);
                    if (_handleCache.TryGetValue(key, out AssetHandle existingHandle)
                        && existingHandle != null
                        && existingHandle.IsValid)
                    {
                        if (!existingHandle.IsDone)
                            existingHandle.WaitForAsyncComplete();

                        if (existingHandle.IsValid
                            && existingHandle.Status == EOperationStatus.Succeed)
                        {
                            return existingHandle.GetAssetObject<T>();
                        }

                        if (TryRemoveHandle(key, existingHandle))
                            ReleaseHandleSafely(existingHandle);
                    }

                    return null;
                }
            }

            string error = handle?.LastError ?? "AssetHandle is null.";
            Debug.LogError(
                $"[YooAssetLoader] Failed to load asset. path:{path}, type:{typeof(T).Name}, package:{EffectivePackageName}, error:{error}");
            ReleaseHandleSafely(handle);
            return null;
        }

        #endregion

        #region 资源释放

        /// <summary>
        /// 用途：释放指定路径下由本加载器持有的全部类型 Handle，并尝试回收无引用 AB。
        /// 调用边界：调用方需先确保该资源不再被业务对象使用。
        /// 注意事项：同路径不同类型的 Handle 会一起释放。
        /// </summary>
        public void Release(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            YooAssetHandleKey[] keys = _handleCache.Keys
                .Where(key => string.Equals(key.Path, path, StringComparison.Ordinal))
                .ToArray();

            for (int i = 0; i < keys.Length; i++)
            {
                if (_handleCache.TryRemove(keys[i], out AssetHandle handle))
                    ReleaseHandleSafely(handle);
            }

            ResourcePackage package = GetPackage();
            if (package == null || package.InitializeStatus != EOperationStatus.Succeed)
                return;

            try
            {
                if (package.CheckLocationValid(path))
                    package.TryUnloadUnusedAsset(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[YooAssetLoader] Failed to unload unused asset. path:{path}, package:{EffectivePackageName}, error:{ex.Message}");
            }
        }

        /// <summary>
        /// 用途：释放本加载器持有的全部 Handle，并回收已无引用的 AB。
        /// 调用边界：场景切换、Loader 切换或资源系统销毁时调用。
        /// 注意事项：不会强制卸载仍被其他 YooAsset Handle 引用的资源。
        /// </summary>
        public void ReleaseAll()
        {
            foreach (var pair in _handleCache.ToArray())
            {
                if (_handleCache.TryRemove(pair.Key, out AssetHandle handle))
                    ReleaseHandleSafely(handle);
            }

            ResourcePackage package = GetPackage();
            if (package == null || package.InitializeStatus != EOperationStatus.Succeed)
                return;

            try
            {
                package.UnloadUnusedAssets();
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[YooAssetLoader] Failed to unload unused assets. package:{EffectivePackageName}, error:{ex.Message}");
            }
        }

        #endregion

        #region Handle Helpers

        private bool TryGetCompletedAsset<T>(YooAssetHandleKey key, out T asset)
            where T : UnityEngine.Object
        {
            asset = null;
            if (!TryGetValidHandle(key, out AssetHandle handle))
                return false;

            if (!handle.IsDone)
                return false;

            if (handle.Status == EOperationStatus.Succeed)
            {
                asset = handle.GetAssetObject<T>();
                if (asset != null)
                    return true;
            }

            if (TryRemoveHandle(key, handle))
                ReleaseHandleSafely(handle);

            return false;
        }

        private bool TryGetValidHandle(YooAssetHandleKey key, out AssetHandle handle)
        {
            if (!_handleCache.TryGetValue(key, out handle))
                return false;

            if (handle != null && handle.IsValid)
                return true;

            _handleCache.TryRemove(key, out _);
            return false;
        }

        private bool TryRemoveHandle(YooAssetHandleKey key, AssetHandle expectedHandle)
        {
            if (!_handleCache.TryGetValue(key, out AssetHandle currentHandle)
                || !ReferenceEquals(currentHandle, expectedHandle))
            {
                return false;
            }

            return _handleCache.TryRemove(key, out _);
        }

        private static void ReleaseHandleSafely(AssetHandle handle)
        {
            if (handle != null && handle.IsValid)
                handle.Release();
        }

        #endregion
    }
}

#endif // YOOASSET || YOOASSET_ENABLED

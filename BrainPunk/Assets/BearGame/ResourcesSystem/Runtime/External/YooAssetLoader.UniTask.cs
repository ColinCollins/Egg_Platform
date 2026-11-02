#if (YOOASSET || YOOASSET_ENABLED) && UNITASK

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace Bear.ResourceSystem.External
{
    /// <summary>
    /// YooAsset 加载策略 - UniTask 扩展
    /// </summary>
    public partial class YooAssetLoader : IResourceLoader
    {
        #region 异步加载

        /// <summary>
        /// 用途：异步加载资源，并复用相同路径与类型的 YooAsset Handle。
        /// 调用边界：应在 Unity 主线程使用；调用前 Package 必须初始化成功。
        /// 注意事项：取消仅终止本次等待；共享 Handle 的最终释放由 Release 接口负责。
        /// </summary>
        public async UniTask<T> LoadAsync<T>(string path, Action<float> onProgress = null, CancellationToken cancellationToken = default) where T : UnityEngine.Object
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
            {
                onProgress?.Invoke(1f);
                return cachedAsset;
            }

            AssetHandle handle;
            if (!TryGetValidHandle(key, out handle))
            {
                handle = package.LoadAssetAsync<T>(path);
                if (handle == null)
                {
                    Debug.LogError(
                        $"[YooAssetLoader] YooAsset returned a null handle. path:{path}, type:{typeof(T).Name}, package:{EffectivePackageName}");
                    return null;
                }

                if (!_handleCache.TryAdd(key, handle))
                {
                    ReleaseHandleSafely(handle);
                    if (!TryGetValidHandle(key, out handle))
                        return await LoadAsync<T>(path, onProgress, cancellationToken);
                }
            }

            while (handle != null && handle.IsValid && !handle.IsDone)
            {
                if (cancellationToken.IsCancellationRequested)
                    return null;

                onProgress?.Invoke(handle.Progress);
                await UniTask.Yield();
            }

            if (cancellationToken.IsCancellationRequested)
                return null;

            if (handle == null || !handle.IsValid)
            {
                TryRemoveHandle(key, handle);
                return null;
            }

            if (handle.Status == EOperationStatus.Succeed)
            {
                T asset = handle.GetAssetObject<T>();
                if (asset == null)
                {
                    RemoveAndReleaseHandle(key, handle);
                    Debug.LogError(
                        $"[YooAssetLoader] Loaded handle has no matching asset. path:{path}, type:{typeof(T).Name}, package:{EffectivePackageName}");
                    return null;
                }

                onProgress?.Invoke(1f);
                return asset;
            }

            string error = handle.LastError;
            RemoveAndReleaseHandle(key, handle);
            Debug.LogError(
                $"[YooAssetLoader] Failed to load asset. path:{path}, type:{typeof(T).Name}, package:{EffectivePackageName}, error:{error}");
            return null;
        }

        /// <summary>
        /// 用途：预加载资源并保留 Handle，供后续 Load/LoadAsync 直接复用。
        /// 调用边界：业务仍需在不再使用资源时调用 Release(path)。
        /// 注意事项：失败时会清理失败 Handle，不会清空本地 YooAsset 缓存文件。
        /// </summary>
        public async UniTask<bool> PreloadAsync<T>(string path) where T : UnityEngine.Object
        {
            return await LoadAsync<T>(path) != null;
        }

        private void RemoveAndReleaseHandle(YooAssetHandleKey key, AssetHandle handle)
        {
            if (TryRemoveHandle(key, handle))
                ReleaseHandleSafely(handle);
        }

        #endregion
    }
}

#endif

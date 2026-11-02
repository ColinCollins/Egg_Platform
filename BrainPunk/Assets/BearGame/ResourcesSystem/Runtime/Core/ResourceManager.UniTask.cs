#if UNITASK

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Bear.ResourceSystem
{
    /// <summary>
    /// ResourceManager UniTask 扩展 - 提供异步加载功能
    /// 需要安装 UniTask 包才能使用
    /// </summary>
    public partial class ResourceManager
    {
        /// <summary>
        /// 加载中的任务（路径 + 类型 -> TaskCompletionSource）
        /// 用于防止重复加载同一资源，同时避免不同资源类型错误复用同一个结果。
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<ResourceCacheKey, UniTaskCompletionSource<UnityEngine.Object>> _loadingTasks = new();

        #region 异步加载

        /// <summary>
        /// 用途：异步加载资源，并按路径与类型复用同一底层加载任务。
        /// 调用边界：应从 Unity 主线程调用；单个调用方取消只终止自己的等待，不取消其他调用方共享的底层加载。
        /// 注意事项：成功结果进入 ResourceManager 缓存，业务结束后仍需调用 Release(path)。
        /// </summary>
        public async UniTask<T> LoadAsync<T>(string path, Action<float> onProgress = null, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (TryGetCachedAsset(path, out T cached))
            {
                onProgress?.Invoke(1f);
                return cached;
            }

            var key = new ResourceCacheKey(path, typeof(T));
            if (_loadingTasks.TryGetValue(key, out UniTaskCompletionSource<UnityEngine.Object> existingTcs))
            {
                onProgress?.Invoke(0.5f);
                UnityEngine.Object existingAsset = await existingTcs.Task.AttachExternalCancellation(cancellationToken);
                onProgress?.Invoke(1f);
                return existingAsset as T;
            }

            var tcs = new UniTaskCompletionSource<UnityEngine.Object>();
            if (!_loadingTasks.TryAdd(key, tcs))
            {
                tcs = _loadingTasks[key];
                UnityEngine.Object asset = await tcs.Task.AttachExternalCancellation(cancellationToken);
                onProgress?.Invoke(1f);
                return asset as T;
            }

            int cacheGeneration = GetCacheGeneration();
            int pathGeneration = GetPathGeneration(path);
            RunSharedLoadAsync<T>(
                key,
                path,
                cacheGeneration,
                pathGeneration,
                tcs,
                onProgress).Forget();

            UnityEngine.Object loadedAsset = await tcs.Task.AttachExternalCancellation(cancellationToken);
            return loadedAsset as T;
        }

        /// <summary>
        /// 用途：执行不受单个等待者取消影响的共享底层加载，并统一写入缓存或传播失败。
        /// 调用边界：只由 LoadAsync 在成功注册首个路径任务时调用。
        /// 注意事项：若 ReleaseAll 或 Loader 切换使缓存代次变化，结果会被释放且不会重新写回缓存。
        /// </summary>
        private async UniTaskVoid RunSharedLoadAsync<T>(
            ResourceCacheKey key,
            string path,
            int cacheGeneration,
            int pathGeneration,
            UniTaskCompletionSource<UnityEngine.Object> tcs,
            Action<float> onProgress)
            where T : UnityEngine.Object
        {
            IResourceLoader loader = null;
            try
            {
                loader = ResolveLoader<T>(path);
                if (loader == null)
                    throw new InvalidOperationException($"No loader can load asset. path:{path}, type:{typeof(T).Name}");

                T loadedAsset = await loader.LoadAsync<T>(
                    path,
                    onProgress,
                    CancellationToken.None);
                if (loadedAsset == null)
                    throw new InvalidOperationException(
                        $"Loader '{loader.LoaderName}' returned null. path:{path}, type:{typeof(T).Name}");

                if (!CacheAsset(
                    path,
                    loadedAsset,
                    loader,
                    cacheGeneration,
                    pathGeneration))
                {
                    loader.Release(path);
                    throw new OperationCanceledException(
                        $"Resource load result became stale before caching. path:{path}, type:{typeof(T).Name}");
                }

                tcs.TrySetResult(loadedAsset);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                _loadingTasks.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// 异步加载并实例化（确保在主线程实例化）
        /// </summary>
        public async UniTask<T> LoadAndInstantiateAsync<T>(string path, Transform parent = null, Action<float> onProgress = null, CancellationToken cancellationToken = default) where T : Component
        {
            // 加载预制体
            var prefab = await LoadAsync<GameObject>(path, onProgress, cancellationToken);
            if (prefab == null) return null;

            // 确保在主线程实例化
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);

            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            // 实例化
            var instance = Instantiate(prefab, parent);
            var component = instance.GetComponent<T>();
            if (component == null)
            {
                Destroy(instance);
                Debug.LogError(
                    $"[ResourceManager] Instantiated prefab does not contain component. path:{path}, component:{typeof(T).Name}");
                return null;
            }

            // 同时追踪根对象和返回组件，确保任意一种释放方式都会销毁完整实例。
            var instanceRecord = new InstanceRecord(instance, component);
            _instanceRecords.Add(instance, instanceRecord);
            _instanceRecords.Add(component, instanceRecord);

            return component;
        }

        #endregion

        #region 预加载

        /// <summary>
        /// 预加载资源
        /// </summary>
        public async UniTask<bool> PreloadAsync<T>(string path) where T : UnityEngine.Object
        {
            var asset = await LoadAsync<T>(path);
            return asset != null;
        }

        /// <summary>
        /// 批量预加载
        /// </summary>
        public async UniTask PreloadBatchAsync<T>(IEnumerable<string> paths, Action<float> onProgress = null) where T : UnityEngine.Object
        {
            var pathList = paths.ToList();
            int total = pathList.Count;
            int completed = 0;

            // 使用 WhenAll 并行加载
            var tasks = pathList.Select(async path =>
            {
                await PreloadAsync<T>(path);
                Interlocked.Increment(ref completed);
                onProgress?.Invoke((float)completed / total);
            });

            await UniTask.WhenAll(tasks);
        }

        #endregion
    }
}

#endif // UNITASK

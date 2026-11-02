using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace Bear.ResourceSystem
{
    /// <summary>
    /// 资源管理器 - 统一管理所有资源加载策略
    /// 缓存容器支持并发访问；Unity 资源加载、实例化与销毁入口仍应在主线程调用。
    /// </summary>
    public partial class ResourceManager : MonoBehaviour
    {
        #region Cache Types

        private readonly struct ResourceCacheKey : IEquatable<ResourceCacheKey>
        {
            public readonly string Path;
            public readonly Type AssetType;

            public ResourceCacheKey(string path, Type assetType)
            {
                Path = path;
                AssetType = assetType;
            }

            public bool Equals(ResourceCacheKey other)
            {
                return string.Equals(Path, other.Path, StringComparison.Ordinal)
                    && AssetType == other.AssetType;
            }

            public override bool Equals(object obj)
            {
                return obj is ResourceCacheKey other && Equals(other);
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

        private sealed class ResourceCacheEntry
        {
            public readonly UnityEngine.Object Asset;
            public readonly IResourceLoader OwnerLoader;

            public ResourceCacheEntry(UnityEngine.Object asset, IResourceLoader ownerLoader)
            {
                Asset = asset;
                OwnerLoader = ownerLoader;
            }
        }

        private sealed class InstanceRecord
        {
            public readonly GameObject Root;
            public readonly Component ReturnedComponent;

            public InstanceRecord(GameObject root, Component returnedComponent)
            {
                Root = root;
                ReturnedComponent = returnedComponent;
            }
        }

        #endregion

        #region Singleton

        private static ResourceManager _instance;
        public static ResourceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("ResourceManager");
                    _instance = go.AddComponent<ResourceManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        #endregion

        #region Runtime State

        /// <summary>
        /// 加载器列表（按优先级排序）
        /// </summary>
        private readonly List<IResourceLoader> _loaders = new();
        private readonly object _loaderLock = new();

        /// <summary>
        /// 资源缓存（路径 + 类型 -> 资源对象与实际 Loader）
        /// 使用线程安全的 ConcurrentDictionary
        /// </summary>
        private readonly ConcurrentDictionary<ResourceCacheKey, ResourceCacheEntry> _assetCache = new();

        /// <summary>
        /// 实例化对象与返回组件到根 GameObject 的映射（用于完整销毁实例）
        /// </summary>
        private readonly ConditionalWeakTable<UnityEngine.Object, InstanceRecord> _instanceRecords = new();

        /// <summary>
        /// 用途：使 ReleaseAll 或 Loader 切换前发起的异步加载结果失效，避免旧任务重新写回缓存。
        /// 调用边界：只由 ResourceManager 内部读取和递增。
        /// 注意事项：它不取消底层 Unity/YooAsset 操作，只阻止过期结果进入 Bear 缓存。
        /// </summary>
        private int _cacheGeneration;

        /// <summary>
        /// 用途：使指定路径 Release 前发起的异步加载结果失效。
        /// 调用边界：由 Release(path) 递增，由异步加载入口捕获并在写缓存前校验。
        /// 注意事项：避免单资源释放时粗暴取消其他路径的并行加载。
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _pathGenerations = new(StringComparer.Ordinal);

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }

        #endregion

        #region 加载器管理

        /// <summary>
        /// 注册加载器
        /// </summary>
        public void RegisterLoader(IResourceLoader loader)
        {
            if (loader == null) return;

            lock (_loaderLock)
            {
                if (_loaders.Any(l => l.LoaderName == loader.LoaderName))
                {
                    Debug.LogWarning($"[ResourceManager] Loader '{loader.LoaderName}' already registered.");
                    return;
                }

                _loaders.Add(loader);
                _loaders.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }

            Debug.Log($"[ResourceManager] Registered loader: {loader.LoaderName} (Priority: {loader.Priority})");
        }

        /// <summary>
        /// 注销加载器
        /// </summary>
        public void UnregisterLoader(string loaderName)
        {
            IResourceLoader loader = null;
            lock (_loaderLock)
            {
                loader = _loaders.FirstOrDefault(l => l.LoaderName == loaderName);
                if (loader != null)
                {
                    _loaders.Remove(loader);
                }
            }

            if (loader == null)
                return;

            Interlocked.Increment(ref _cacheGeneration);
            RemoveCacheEntriesOwnedBy(loader);
            loader.ReleaseAll();
            Debug.Log($"[ResourceManager] Unregistered loader: {loaderName}");
        }

        /// <summary>
        /// 用途：按优先级查找真正能够加载指定路径与类型的 Loader。
        /// 调用边界：同步/异步加载与 CanLoad 查询统一使用本入口。
        /// 注意事项：单个 Loader 的校验异常会记录 warning 并继续尝试后续 Loader。
        /// </summary>
        private IResourceLoader ResolveLoader<T>(string path) where T : UnityEngine.Object
        {
            IResourceLoader[] loaderSnapshot;
            lock (_loaderLock)
            {
                loaderSnapshot = _loaders.ToArray();
            }

            for (int i = 0; i < loaderSnapshot.Length; i++)
            {
                IResourceLoader loader = loaderSnapshot[i];
                if (loader == null || !loader.IsAvailable)
                    continue;

                try
                {
                    if (loader.CanLoad<T>(path))
                        return loader;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[ResourceManager] Loader '{loader.LoaderName}' failed CanLoad check. path:{path}, type:{typeof(T).Name}, error:{ex.Message}");
                }
            }

            return null;
        }

        #endregion

        #region 同步加载

        /// <summary>
        /// 同步加载资源
        /// </summary>
        public T Load<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (TryGetCachedAsset(path, out T cached))
                return cached;

            IResourceLoader loader = ResolveLoader<T>(path);
            if (loader == null)
            {
                Debug.LogError($"[ResourceManager] No loader can load asset. path:{path}, type:{typeof(T).Name}");
                return null;
            }

            try
            {
                T asset = loader.Load<T>(path);
                if (asset != null)
                    CacheAsset(path, asset, loader);

                return asset;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[ResourceManager] Loader '{loader.LoaderName}' failed to load asset. path:{path}, type:{typeof(T).Name}, error:{ex.Message}");
                return null;
            }
        }

        #endregion

        #region 资源释放

        /// <summary>
        /// 通过路径释放资源
        /// </summary>
        public void Release(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            _pathGenerations.AddOrUpdate(path, 1, (_, generation) => unchecked(generation + 1));

            var ownerLoaders = new HashSet<IResourceLoader>();
            ResourceCacheKey[] keys = _assetCache.Keys
                .Where(key => string.Equals(key.Path, path, StringComparison.Ordinal))
                .ToArray();

            for (int i = 0; i < keys.Length; i++)
            {
                if (_assetCache.TryRemove(keys[i], out ResourceCacheEntry entry)
                    && entry?.OwnerLoader != null)
                {
                    ownerLoaders.Add(entry.OwnerLoader);
                }
            }

            foreach (IResourceLoader ownerLoader in ownerLoaders)
            {
                ownerLoader.Release(path);
            }
        }

        /// <summary>
        /// 通过实例释放资源
        /// </summary>
        public void ReleaseInstance(UnityEngine.Object instance)
        {
            if (instance == null) return;

            if (_instanceRecords.TryGetValue(instance, out InstanceRecord record))
            {
                _instanceRecords.Remove(instance);
                if (record.Root != null)
                    _instanceRecords.Remove(record.Root);
                if (record.ReturnedComponent != null)
                    _instanceRecords.Remove(record.ReturnedComponent);

                if (record.Root != null)
                    Destroy(record.Root);

                // 当前兼容 API 仍由调用方决定何时 Release(path)，避免一个实例销毁时提前释放其他实例共用的 prefab。
            }
            else
            {
                // 未跟踪的实例直接销毁
                Destroy(instance);
            }
        }

        /// <summary>
        /// 释放所有资源
        /// </summary>
        public void ReleaseAll()
        {
            Interlocked.Increment(ref _cacheGeneration);
            _assetCache.Clear();
            _pathGenerations.Clear();

            IResourceLoader[] loaderSnapshot;
            lock (_loaderLock)
            {
                loaderSnapshot = _loaders.ToArray();
            }

            for (int i = 0; i < loaderSnapshot.Length; i++)
            {
                loaderSnapshot[i]?.ReleaseAll();
            }
        }

        #endregion

        #region 查询接口

        /// <summary>
        /// 检查资源是否已加载
        /// </summary>
        public bool IsLoaded(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return _assetCache.Any(pair =>
                string.Equals(pair.Key.Path, path, StringComparison.Ordinal)
                && pair.Value?.Asset != null);
        }

        public bool CanLoad<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (TryGetCachedAsset(path, out T _))
                return true;

            return ResolveLoader<T>(path) != null;
        }

        /// <summary>
        /// 获取已加载资源列表
        /// </summary>
        public IReadOnlyCollection<string> GetLoadedAssets()
        {
            return _assetCache.Keys
                .Select(key => key.Path)
                .Distinct(StringComparer.Ordinal)
                .ToList()
                .AsReadOnly();
        }

        #endregion

        #region Cache Helpers

        private bool TryGetCachedAsset<T>(string path, out T asset) where T : UnityEngine.Object
        {
            asset = null;
            var key = new ResourceCacheKey(path, typeof(T));
            if (!_assetCache.TryGetValue(key, out ResourceCacheEntry entry)
                || entry?.Asset == null)
            {
                return false;
            }

            asset = entry.Asset as T;
            return asset != null;
        }

        private bool CacheAsset<T>(
            string path,
            T asset,
            IResourceLoader ownerLoader,
            int expectedGeneration = -1,
            int expectedPathGeneration = -1)
            where T : UnityEngine.Object
        {
            if (asset == null || ownerLoader == null)
                return false;

            if (expectedGeneration >= 0 && expectedGeneration != Volatile.Read(ref _cacheGeneration))
                return false;

            if (expectedPathGeneration >= 0
                && expectedPathGeneration != GetPathGeneration(path))
            {
                return false;
            }

            var key = new ResourceCacheKey(path, typeof(T));
            _assetCache[key] = new ResourceCacheEntry(asset, ownerLoader);
            return true;
        }

        private int GetCacheGeneration()
        {
            return Volatile.Read(ref _cacheGeneration);
        }

        private int GetPathGeneration(string path)
        {
            return _pathGenerations.TryGetValue(path, out int generation)
                ? generation
                : 0;
        }

        private void RemoveCacheEntriesOwnedBy(IResourceLoader ownerLoader)
        {
            if (ownerLoader == null)
                return;

            foreach (var pair in _assetCache.ToArray())
            {
                if (ReferenceEquals(pair.Value?.OwnerLoader, ownerLoader))
                    _assetCache.TryRemove(pair.Key, out _);
            }
        }

        #endregion
    }
}

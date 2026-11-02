using System;
using System.Collections.Generic;
using Bear.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

/// <summary>
/// YooAsset UI 加载器（预留实现）。
/// 这里先把接口/入口留好，具体 YooAsset 管理（路径规则、缓存、依赖、异步、卸载策略）后续再补。
/// </summary>
public class YooAssetUILoader : IUILoader
{
    /// <summary>
    /// 缓存已加载但未释放的 AssetHandle
    /// </summary>
    private readonly Dictionary<string, AssetHandle> _handleCache = new Dictionary<string, AssetHandle>();

    public YooAssetUILoader()
    {
    }

    public GameObject Load(string path)
    {
        if (!YooAssets.Initialized)
        {
            return null;
        }

        // 检查缓存
        if (_handleCache.TryGetValue(path, out AssetHandle cachedHandle))
        {
            if (cachedHandle.IsValid)
            {
                // 如果已加载完成，直接返回
                if (cachedHandle.IsDone)
                {
                    return cachedHandle.GetAssetObject<GameObject>();
                }
                // 如果正在加载中，等待完成
                cachedHandle.WaitForAsyncComplete();
                return cachedHandle.GetAssetObject<GameObject>();
            }
            else
            {
                // 缓存中的 handle 已失效，移除
                _handleCache.Remove(path);
            }
        }

        // 同步加载（默认包）
        AssetHandle handle = YooAssets.LoadAssetSync<GameObject>(path);
        if (handle != null && handle.IsValid)
        {
            // 存入缓存
            _handleCache[path] = handle;
            return handle.GetAssetObject<GameObject>();
        }

        return null;
    }

    public void LoadAsync(string assetKey, Action<GameObject> onLoaded)
    {
        if (!YooAssets.Initialized)
        {
            onLoaded?.Invoke(null);
            return;
        }

        // 检查缓存
        if (_handleCache.TryGetValue(assetKey, out AssetHandle cachedHandle))
        {
            if (cachedHandle.IsValid)
            {
                // 如果已加载完成，直接返回
                if (cachedHandle.IsDone)
                {
                    GameObject obj = cachedHandle.GetAssetObject<GameObject>();
                    onLoaded?.Invoke(obj);
                    return;
                }
                // 如果正在加载中，等待完成
                LoadAsyncWaitForCached(cachedHandle, onLoaded).Forget();
                return;
            }
            else
            {
                // 缓存中的 handle 已失效，移除
                _handleCache.Remove(assetKey);
            }
        }

        // 使用 UniTask 配合 YooAsset 异步加载
        LoadAsyncInternal(assetKey, onLoaded).Forget();
    }

    private async UniTaskVoid LoadAsyncWaitForCached(AssetHandle handle, Action<GameObject> onLoaded)
    {
        try
        {
            // 等待已存在的 handle 加载完成
            await handle.ToUniTask();

            // 获取资源对象
            GameObject obj = handle.GetAssetObject<GameObject>();

            // 调用回调
            onLoaded?.Invoke(obj);
        }
        catch (Exception e)
        {
            Debug.LogError($"[YooAssetUILoader] LoadAsyncWaitForCached failed. error={e.Message}");
            onLoaded?.Invoke(null);
        }
    }

    private async UniTaskVoid LoadAsyncInternal(string assetKey, Action<GameObject> onLoaded)
    {
        try
        {
            // 使用 YooAsset 加载资源
            AssetHandle handle = YooAssets.LoadAssetAsync<GameObject>(assetKey);

            // 存入缓存
            _handleCache[assetKey] = handle;

            // 使用 ToUniTask() 等待加载完成
            await handle.ToUniTask();

            // 获取资源对象
            GameObject obj = handle.GetAssetObject<GameObject>();

            // 调用回调
            onLoaded?.Invoke(obj);
        }
        catch (Exception e)
        {
            Debug.LogError($"[YooAssetUILoader] LoadAsync failed. assetKey={assetKey}, error={e.Message}");
            // 加载失败时移除缓存
            if (_handleCache.ContainsKey(assetKey))
            {
                _handleCache.Remove(assetKey);
            }
            onLoaded?.Invoke(null);
        }
    }

    public void Release(string assetKey)
    {
        if (_handleCache.TryGetValue(assetKey, out AssetHandle handle))
        {
            if (handle.IsValid)
            {
                handle.Release();
            }
            _handleCache.Remove(assetKey);
        }
    }

    public void ReleaseAll()
    {
        foreach (var kvp in _handleCache)
        {
            if (kvp.Value.IsValid)
            {
                kvp.Value.Release();
            }
        }
        _handleCache.Clear();
    }

    public void Unload(string path)
    {
        Release(path);
    }
}

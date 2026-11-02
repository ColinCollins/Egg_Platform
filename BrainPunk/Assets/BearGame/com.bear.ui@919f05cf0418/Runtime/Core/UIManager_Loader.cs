using System;
using System.Collections.Generic;
using UnityEngine;
using Bear.ResourceSystem;

namespace Bear.UI
{
    /// <summary>
    /// UIManager Loader 相关功能
    /// </summary>
    public partial class UIManager
    {
        private List<LoaderInfo> _loaders;
        private IUILoader _defaultLoader;

        /// <summary>
        /// Loader 信息结构
        /// </summary>
        private class LoaderInfo
        {
            public IUILoader Loader;
            public int Priority;

            public LoaderInfo(IUILoader loader, int priority)
            {
                Loader = loader;
                Priority = priority;
            }
        }

        /// <summary>
        /// 初始化 Loader 系统
        /// </summary>
        private void InitializeLoaders()
        {
            // 初始化 Loader 列表
            _loaders = new List<LoaderInfo>();

            // 默认使用 ResourceSystemUILoader（优先级 0）
            RegisterLoader(new ResourceSystemUILoader(), 0);
        }

        /// <summary>
        /// 设置默认加载器（如果不注册任何加载器，将使用此加载器）
        /// </summary>
        /// <param name="loader">默认加载器</param>
        public void SetDefaultLoader(IUILoader loader)
        {
            _defaultLoader = loader;
        }

        /// <summary>
        /// 注册 UI 加载器（带优先级）
        /// </summary>
        /// <param name="loader">加载器实例</param>
        /// <param name="priority">优先级，数字越小优先级越高，默认为 0</param>
        public void RegisterLoader(IUILoader loader, int priority = 0)
        {
            if (loader == null)
            {
                Debug.LogWarning("[UIManager] Cannot register null loader");
                return;
            }

            if (_loaders == null)
            {
                _loaders = new List<LoaderInfo>();
            }

            // 检查是否已存在相同的 loader
            for (int i = 0; i < _loaders.Count; i++)
            {
                if (_loaders[i].Loader == loader)
                {
                    // 更新优先级
                    _loaders[i].Priority = priority;
                    SortLoaders();
                    Debug.Log($"[UIManager] Loader priority updated - {loader.GetType().Name} (Priority: {priority})");
                    return;
                }
            }

            // 添加新的 loader
            _loaders.Add(new LoaderInfo(loader, priority));
            SortLoaders();
            Debug.Log($"[UIManager] Loader registered - {loader.GetType().Name} (Priority: {priority})");
        }

        /// <summary>
        /// 取消注册 UI 加载器
        /// </summary>
        /// <param name="loader">加载器实例</param>
        public void UnregisterLoader(IUILoader loader)
        {
            if (loader == null || _loaders == null)
            {
                return;
            }

            for (int i = _loaders.Count - 1; i >= 0; i--)
            {
                if (_loaders[i].Loader == loader)
                {
                    _loaders.RemoveAt(i);
                    Debug.Log($"[UIManager] Loader unregistered - {loader.GetType().Name}");
                    return;
                }
            }
        }

        /// <summary>
        /// 按优先级排序 Loader 列表
        /// </summary>
        private void SortLoaders()
        {
            if (_loaders == null || _loaders.Count <= 1)
            {
                return;
            }

            _loaders.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        /// <summary>
        /// 使用已注册的 Loader 加载 UI
        /// </summary>
        /// <param name="path">UI 资源路径</param>
        /// <returns>加载的 GameObject，如果加载失败返回 null</returns>
        private GameObject LoadUIWithLoaders(string path)
        {
            // 如果没有注册加载器，尝试使用默认加载器或 ResourceSystem
            if (_loaders == null || _loaders.Count == 0)
            {
                if (_defaultLoader != null)
                {
                    return _defaultLoader.Load(path);
                }
                
                // 使用 ResourceSystem 直接加载
                return LoadUIWithResourceSystem(path);
            }

            // 按优先级顺序尝试加载
            GameObject uiPrefab = null;
            string lastError = string.Empty;

            foreach (var loaderInfo in _loaders)
            {
                if (loaderInfo.Loader == null)
                {
                    continue;
                }

                try
                {
                    uiPrefab = loaderInfo.Loader.Load(path);
                    if (uiPrefab != null)
                    {
                        Debug.Log($"[UIManager] Successfully loaded UI '{path}' using loader {loaderInfo.Loader.GetType().Name} (Priority: {loaderInfo.Priority})");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    Debug.LogWarning($"[UIManager] Loader {loaderInfo.Loader.GetType().Name} failed to load '{path}': {ex.Message}");
                }
            }

            if (uiPrefab == null)
            {
                Debug.LogError($"[UIManager] Failed to load UI prefab at path '{path}' using all registered loaders. Last error: {lastError}");
            }

            return uiPrefab;
        }

        /// <summary>
        /// 使用 ResourceSystem 直接加载 UI
        /// </summary>
        /// <param name="path">UI 资源路径</param>
        /// <returns>加载的 GameObject，如果加载失败返回 null</returns>
        private GameObject LoadUIWithResourceSystem(string path)
        {
            try
            {
                GameObject prefab = ResourceManager.Instance.Load<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogError($"[UIManager] Failed to load UI at path '{path}' via ResourceSystem");
                    return null;
                }
                return GameObject.Instantiate(prefab);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIManager] Exception when loading UI '{path}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 使用已注册的 Loader 卸载 UI
        /// </summary>
        /// <param name="path">UI 资源路径</param>
        private void UnloadUIWithLoaders(string path)
        {
            if (_loaders == null || _loaders.Count == 0)
            {
                // 使用 ResourceSystem 直接释放
                ResourceManager.Instance.Release(path);
                return;
            }

            // 调用所有 Loader 的 Unload 方法
            foreach (var loaderInfo in _loaders)
            {
                if (loaderInfo.Loader != null)
                {
                    try
                    {
                        loaderInfo.Loader.Unload(path);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[UIManager] Loader {loaderInfo.Loader.GetType().Name} failed to unload '{path}': {ex.Message}");
                    }
                }
            }
        }
    }
}

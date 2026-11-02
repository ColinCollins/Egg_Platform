#if UNITASK

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Bear.ResourceSystem;

namespace Bear.UI
{
    /// <summary>
    /// 基于 ResourceSystem 的 UI 加载器 - UniTask 扩展
    /// </summary>
    public partial class ResourceSystemUILoader
    {
        /// <summary>
        /// 异步加载 UI Prefab（使用 UniTask）
        /// </summary>
        public void LoadAsync(string path, Action<GameObject> onComplete)
        {
            LoadAsyncInternal(path, onComplete).Forget();
        }

        private async UniTask LoadAsyncInternal(string path, Action<GameObject> onComplete)
        {
            GameObject prefab = await ResourceManager.Instance.LoadAsync<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[ResourceSystemUILoader] Failed to load UI at path '{path}'");
                onComplete?.Invoke(null);
            }
            else
            {
                GameObject instance = GameObject.Instantiate(prefab);
                onComplete?.Invoke(instance);
            }
        }
    }
}

#else

using System;
using UnityEngine;

namespace Bear.UI
{
    /// <summary>
    /// 基于 ResourceSystem 的 UI 加载器 - 非 UniTask 版本
    /// </summary>
    public partial class ResourceSystemUILoader
    {
        /// <summary>
        /// 异步加载 UI Prefab（回退到同步加载）
        /// </summary>
        public void LoadAsync(string path, Action<GameObject> onComplete)
        {
            // 回退到同步加载
            GameObject result = Load(path);
            onComplete?.Invoke(result);
        }
    }
}

#endif

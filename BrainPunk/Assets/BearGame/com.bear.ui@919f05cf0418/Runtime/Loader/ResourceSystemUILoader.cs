using UnityEngine;
using Bear.ResourceSystem;

namespace Bear.UI
{
    /// <summary>
    /// 基于 ResourceSystem 的 UI 加载器
    /// 支持多种加载策略（Resources、YooAsset 等）
    /// </summary>
    public partial class ResourceSystemUILoader : IUILoader
    {
        /// <summary>
        /// 同步加载 UI Prefab
        /// </summary>
        public GameObject Load(string path)
        {
            GameObject prefab = ResourceManager.Instance.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[ResourceSystemUILoader] Failed to load UI at path '{path}'");
                return null;
            }
            return Object.Instantiate(prefab);
        }

        /// <summary>
        /// 卸载 UI 资源
        /// </summary>
        public void Unload(string path)
        {
            // 通过 ResourceSystem 释放资源
            ResourceManager.Instance.Release(path);
        }
    }
}

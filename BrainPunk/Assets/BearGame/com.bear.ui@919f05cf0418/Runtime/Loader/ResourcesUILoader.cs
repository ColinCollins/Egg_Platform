using UnityEngine;

namespace Bear.UI
{
    /// <summary>
    /// 基于 Resources 的 UI 加载器
    /// </summary>
    public class ResourcesUILoader : IUILoader
    {
        private string _basePath = "UI/";

        public ResourcesUILoader(string basePath = "UI/")
        {
            _basePath = basePath;
        }

        public GameObject Load(string path)
        {
            string fullPath = _basePath + path;
            GameObject prefab = Resources.Load<GameObject>(fullPath);
            if (prefab == null)
            {
                Debug.LogError($"ResourcesUILoader: Failed to load UI at path '{fullPath}'");
                return null;
            }
            return Object.Instantiate(prefab);
        }

        public void LoadAsync(string path, System.Action<GameObject> onComplete)
        {
            string fullPath = _basePath + path;
            ResourceRequest request = Resources.LoadAsync<GameObject>(fullPath);
            request.completed += (op) =>
            {
                GameObject prefab = request.asset as GameObject;
                if (prefab == null)
                {
                    Debug.LogError($"ResourcesUILoader: Failed to load UI at path '{fullPath}'");
                    onComplete?.Invoke(null);
                }
                else
                {
                    GameObject instance = Object.Instantiate(prefab);
                    onComplete?.Invoke(instance);
                }
            };
        }

        public void Unload(string path)
        {
            // Resources.UnloadAsset 只能用于纹理、网格等独立资源，不能用于 GameObject/Component。
            // 通过 Resources 加载的 Prefab 无法单独卸载，需在适当时机调用 Resources.UnloadUnusedAssets() 释放未引用资源。
        }
    }
}


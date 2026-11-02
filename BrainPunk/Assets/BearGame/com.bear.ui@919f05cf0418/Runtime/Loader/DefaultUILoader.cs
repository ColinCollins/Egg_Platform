using UnityEngine;

namespace Bear.UI
{
    /// <summary>
    /// 默认 UI 加载器（直接创建 GameObject，不加载资源）
    /// 用于向后兼容，当没有指定路径时使用
    /// </summary>
    public class DefaultUILoader : IUILoader
    {
        public GameObject Load(string path)
        {
            Debug.LogWarning($"DefaultUILoader: Cannot load from path '{path}'. This loader only creates empty GameObjects.");
            return new GameObject("EmptyUI");
        }

        public void LoadAsync(string path, System.Action<GameObject> onComplete)
        {
            Debug.LogWarning($"DefaultUILoader: Cannot load from path '{path}'. This loader only creates empty GameObjects.");
            GameObject go = new GameObject("EmptyUI");
            onComplete?.Invoke(go);
        }

        public void Unload(string path)
        {
            // 默认加载器不需要卸载
        }
    }
}


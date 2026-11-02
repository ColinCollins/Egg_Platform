using UnityEngine;

namespace Bear.UI
{
    /// <summary>
    /// UI 加载器接口
    /// 支持多种加载方式：Resources、Addressables、AssetBundle 等
    /// </summary>
    public interface IUILoader
    {
        /// <summary>
        /// 同步加载 UI Prefab
        /// </summary>
        /// <param name="path">UI 资源路径</param>
        /// <returns>加载的 GameObject，如果加载失败返回 null</returns>
        GameObject Load(string path);

        /// <summary>
        /// 异步加载 UI Prefab
        /// </summary>
        /// <param name="path">UI 资源路径</param>
        /// <param name="onComplete">加载完成回调</param>
        void LoadAsync(string path, System.Action<GameObject> onComplete);

        /// <summary>
        /// 卸载 UI 资源
        /// </summary>
        /// <param name="path">UI 资源路径</param>
        void Unload(string path);
    }
}


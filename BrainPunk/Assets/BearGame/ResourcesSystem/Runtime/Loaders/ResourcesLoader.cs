using System;
using UnityEngine;

namespace Bear.ResourceSystem.Loaders
{
    /// <summary>
    /// Resources 加载策略
    /// </summary>
    public partial class ResourcesLoader : IResourceLoader
    {
        public string LoaderName => "ResourcesLoader";
        public int Priority { get; set; } = 100;
        public bool IsAvailable => true;

        private readonly string _basePath;

        public ResourcesLoader(string basePath = "")
        {
            _basePath = basePath;
        }

        private string GetFullPath(string path)
        {
            return string.IsNullOrEmpty(_basePath) ? path : $"{_basePath}/{path}";
        }

        #region 同步加载

        public bool CanLoad<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string fullPath = GetFullPath(path);
            return Resources.Load<T>(fullPath) != null;
        }

        public T Load<T>(string path) where T : UnityEngine.Object
        {
            string fullPath = GetFullPath(path);
            return Resources.Load<T>(fullPath);
        }

        #endregion

        #region 资源释放

        public void Release(string path)
        {
            // Resources 加载的资源无法单独卸载
            // 需要通过 Resources.UnloadUnusedAssets() 全局释放
        }

        public void ReleaseAll()
        {
            Resources.UnloadUnusedAssets();
        }

        #endregion
    }
}

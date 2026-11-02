#if UNITASK

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Bear.ResourceSystem.Loaders
{
    /// <summary>
    /// Resources 加载策略 - UniTask 扩展
    /// </summary>
    public partial class ResourcesLoader : IResourceLoader
    {
        /// <summary>
        /// 异步加载资源
        /// </summary>
        public async UniTask<T> LoadAsync<T>(string path, Action<float> onProgress = null, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            string fullPath = GetFullPath(path);
            
            ResourceRequest request = Resources.LoadAsync<T>(fullPath);
            
            while (!request.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }
                
                onProgress?.Invoke(request.progress);
                await UniTask.Yield();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            onProgress?.Invoke(1f);
            return request.asset as T;
        }

        /// <summary>
        /// 预加载资源
        /// </summary>
        public async UniTask<bool> PreloadAsync<T>(string path) where T : UnityEngine.Object
        {
            var asset = await LoadAsync<T>(path);
            return asset != null;
        }
    }
}

#endif

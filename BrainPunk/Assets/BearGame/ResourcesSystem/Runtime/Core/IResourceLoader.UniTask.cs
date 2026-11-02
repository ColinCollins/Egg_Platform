#if UNITASK

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Bear.ResourceSystem
{
    /// <summary>
    /// 资源加载策略接口 - UniTask 扩展
    /// </summary>
    public partial interface IResourceLoader
    {
        /// <summary>
        /// 异步加载资源
        /// </summary>
        UniTask<T> LoadAsync<T>(string path, Action<float> onProgress = null, CancellationToken cancellationToken = default) where T : UnityEngine.Object;

        /// <summary>
        /// 预加载资源
        /// </summary>
        UniTask<bool> PreloadAsync<T>(string path) where T : UnityEngine.Object;
    }
}

#endif

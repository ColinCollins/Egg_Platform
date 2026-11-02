using System;
using UnityEngine;

namespace Bear.ResourceSystem
{
    /// <summary>
    /// 资源加载策略接口
    /// </summary>
    public partial interface IResourceLoader
    {
        /// <summary>
        /// 加载器名称
        /// </summary>
        string LoaderName { get; }
        
        /// <summary>
        /// 加载器优先级（数字越小优先级越高）
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// 是否可用
        /// </summary>
        bool IsAvailable { get; }

        bool CanLoad<T>(string path) where T : UnityEngine.Object;

        /// <summary>
        /// 同步加载资源
        /// </summary>
        T Load<T>(string path) where T : UnityEngine.Object;

        /// <summary>
        /// 释放指定资源
        /// </summary>
        void Release(string path);

        /// <summary>
        /// 释放所有资源
        /// </summary>
        void ReleaseAll();
    }
}

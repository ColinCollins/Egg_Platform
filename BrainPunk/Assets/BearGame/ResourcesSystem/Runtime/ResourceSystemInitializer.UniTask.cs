#if UNITASK && (YOOASSET || YOOASSET_ENABLED)

using Bear.ResourceSystem.External;
using Bear.ResourceSystem.Loaders;
using UnityEngine;
using YooAsset;

namespace Bear.ResourceSystem
{
    /// <summary>
    /// 资源系统初始化 - UniTask + YooAsset 扩展
    /// </summary>
    public static partial class ResourceSystemInitializer
    {
        /// <summary>
        /// 切换为纯 Resources 模式
        /// </summary>
        public static void SwitchToResourcesMode()
        {
            var manager = ResourceManager.Instance;
            
            // 注销 YooAssetLoader
            manager.UnregisterLoader("YooAssetLoader");
            
            // 确保 ResourcesLoader 存在
            var resLoader = new ResourcesLoader();
            manager.RegisterLoader(resLoader);
            
            Debug.Log("[ResourceSystem] Switched to Resources mode");
        }
    }
}

#endif

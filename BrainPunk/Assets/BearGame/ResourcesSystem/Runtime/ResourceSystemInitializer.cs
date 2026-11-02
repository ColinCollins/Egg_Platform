using Bear.ResourceSystem.Loaders;
using UnityEngine;

#if YOOASSET || YOOASSET_ENABLED
using Bear.ResourceSystem.External;
using YooAsset;
#endif

namespace Bear.ResourceSystem
{
    /// <summary>
    /// 资源系统初始化
    /// </summary>
    public static partial class ResourceSystemInitializer
    {
        /// <summary>
        /// 初始化资源系统
        /// </summary>
        /// <param name="useYooAsset">是否优先使用 YooAsset（需要定义 YOOASSET 或 YOOASSET_ENABLED）</param>
        public static void Initialize(bool useYooAsset = true)
        {
            var manager = ResourceManager.Instance;

#if YOOASSET || YOOASSET_ENABLED
            // 1. 注册 YooAssetLoader（优先级高）
            if (useYooAsset && YooAssets.Initialized)
            {
                var yooLoader = new YooAssetLoader("GamePackage");
                manager.RegisterLoader(yooLoader);
                Debug.Log("[ResourceSystem] YooAssetLoader registered");
            }
#endif

            // 2. 注册 ResourcesLoader（兜底）
            var resLoader = new ResourcesLoader();
            manager.RegisterLoader(resLoader);

            Debug.Log("[ResourceSystem] Initialized");
        }
    }
}

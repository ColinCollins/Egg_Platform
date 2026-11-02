using YooAsset;

namespace GF
{
    public static class ResourcePackageExtension
    {
        /// <summary>
        /// 判断资源包是否全部缓存
        /// </summary>
        /// <param name="self"></param>
        /// <param name="version">需要判断的版本号，不需要版本号不填</param>
        /// <returns></returns>
        public static bool IsAllCache(this ResourcePackage self, string version = null)
        {
            if (self.PlayModeImpl == null || self.PlayModeImpl.ActiveManifest == null)
            {
                LogKit.E($"不存在资源包 {self.PackageName}");
                return false;
            }

            if (version != null && self.GetPackageVersion() != version)
            {
                LogKit.E("版本号不同");
                return false;
            }

            var bundles = self.PlayModeImpl.ActiveManifest.BundleList;

            HostPlayModeImpl hostPlayModeImpl = self.PlayModeImpl as HostPlayModeImpl;
            if (hostPlayModeImpl == null)
            {
                LogKit.E("IsAllCache只能在Host模式使用");
                return false;
            }

            bool isCachePersistent = true;
            bool isCacheBuiltin = true;
            for (int i = 0; i < bundles.Count; i++)
            {
                var bundle = bundles[i];
                if (isCachePersistent && !hostPlayModeImpl.Cache.IsCached(bundle.CacheGUID))
                {
                    LogKit.I("持久化目录未全部缓存");
                    isCachePersistent = false;
                }
                
                if (isCacheBuiltin && !hostPlayModeImpl.IsBuildinPackageBundle(bundle))
                {
                    LogKit.I("streaming未全部缓存");
                    isCacheBuiltin = false;
                }
            }

            return isCachePersistent || isCacheBuiltin;
        }
    }
}
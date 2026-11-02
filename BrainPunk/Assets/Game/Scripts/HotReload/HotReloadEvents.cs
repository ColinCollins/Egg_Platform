using Bear.EventSystem;
using UnityEngine;
using YooAsset;

namespace Game.HotReload
{
    /// <summary>
    /// 热更新相关事件
    /// </summary>
    public class HotReloadEvents
    {
        #region Patch Events (补丁流程事件)
        /// <summary>
        /// 补丁流程步骤改变
        /// </summary>
        public class PatchStepsChange : EventBase<string>
        {
            public string Tips => Field1;
        }

        /// <summary>
        /// 补丁包初始化失败
        /// </summary>
        public class InitializeFailed : EventBase { }

        /// <summary>
        /// 资源版本请求失败
        /// </summary>
        public class PackageVersionRequestFailed : EventBase { }

        /// <summary>
        /// 资源清单更新失败
        /// </summary>
        public class PackageManifestUpdateFailed : EventBase { }

        /// <summary>
        /// 发现更新文件
        /// </summary>
        public class FoundUpdateFiles : EventBase<int, long>
        {
            public int TotalCount => Field1;
            public long TotalSizeBytes => Field2;
        }

        /// <summary>
        /// 下载进度更新
        /// </summary>
        public class DownloadUpdate : EventBase<int, int, long, long>
        {
            public int TotalDownloadCount => Field1;
            public int CurrentDownloadCount => Field2;
            public long TotalDownloadSizeBytes => Field3;
            public long CurrentDownloadSizeBytes => Field4;
        }

        /// <summary>
        /// 网络文件下载失败
        /// </summary>
        public class WebFileDownloadFailed : EventBase<string, string>
        {
            public string FileName => Field1;
            public string Error => Field2;
        }
        #endregion
    }
}

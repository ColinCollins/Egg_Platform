using Bear.Fsm;
using Game.Common;
using UnityEngine;
using YooAsset;

namespace Game.HotReload
{
    /// <summary>
    /// 热更新控制器
    /// </summary>
    public class HotReloadCtrl : MonoSingleton<HotReloadCtrl>, IBearMachineOwner
    {
        private StateMachine _machine;

        public StateMachine Machine => _machine;

        #region HotReload Data
        /// <summary>
        /// 资源包名称
        /// </summary>
        public string PackageName { get; set; }

        /// <summary>
        /// 资源包版本
        /// </summary>
        public string PackageVersion { get; set; }

        /// <summary>
        /// 运行模式
        /// </summary>
        public EPlayMode PlayMode { get; set; }

        /// <summary>
        /// 资源下载器
        /// </summary>
        public ResourceDownloaderOperation Downloader { get; set; }

        /// <summary>
        /// 是否完成
        /// </summary>
        public bool IsFinish { get; private set; }

        /// <summary>
        /// 是否失败
        /// </summary>
        public bool IsFailed { get; private set; }
        #endregion

        public void Init(string packageName, EPlayMode playMode)
        {
            PackageName = packageName;
            PlayMode = playMode;
            IsFinish = false;
            IsFailed = false;

            _machine = new StateMachine(this);
            _machine.Inject(
                typeof(HotReloadCtrl_InitializePackage),
                typeof(HotReloadCtrl_RequestPackageVersion),
                typeof(HotReloadCtrl_UpdatePackageManifest),
                typeof(HotReloadCtrl_CreateDownloader),
                typeof(HotReloadCtrl_DownloadPackageFiles),
                typeof(HotReloadCtrl_DownloadPackageOver),
                typeof(HotReloadCtrl_ClearCacheBundle),
                typeof(HotReloadCtrl_StartGame)
            );

            _machine.Apply(GetType());
            _machine.Enter(HotReloadStateName.INITIALIZE_PACKAGE);
        }

        private void Update()
        {
            _machine?.Update();
        }

        private void OnDestroy()
        {
            _machine?.Dispose();
        }

        /// <summary>
        /// 设置完成状态
        /// </summary>
        public void SetFinish()
        {
            IsFinish = true;
        }

        /// <summary>
        /// 设置失败状态
        /// </summary>
        public void SetFailed()
        {
            IsFailed = true;
        }
    }
}

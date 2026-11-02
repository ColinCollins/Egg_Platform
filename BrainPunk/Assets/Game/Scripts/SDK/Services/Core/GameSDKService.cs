#nullable enable
using System;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Core;
using Guru.SDK.Framework.Core.Spec;
using Guru.SDK.Framework.Utils.Log;
using Game;
using Guru.SDK.Framework.Utils.Package;
using System.Collections.Generic;

/// <summary>
/// GuruSDK 服务总封装
/// SDK 相关的服务建议都封装到这个类下
/// </summary>
public partial class GameSDKService
    {
        // 单例
        private static readonly Lazy<GameSDKService> LazyInstance = new Lazy<GameSDKService>(() => new GameSDKService());

        public static GameSDKService Instance => LazyInstance.Value;

        public AppSpec MainAppSpec { get; }
        public bool IsInitialized { get; private set; }
        
        // 各管理器
        private GameAdsManager _adsManager;
        
        public GameSDKService()
        {
            var mainFlavor = "main";
            MainAppSpec = GuruSpecFactory.Create(mainFlavor);
        }
        
        
        /// <summary>
        /// 初始化 GuruSDK 主接口
        /// </summary>
        /// <param name="onInitCallback"></param>
        /// <param name="onFirebaseReady"></param>
        public async UniTask InitService(Action? onInitCallback= null, Action? onFirebaseReady = null)
        {
            OneLinkManager.Instance.Initialize();

            // 初始化本地服务

            // 初始化环境依赖, 如 Firebase
            await GuruSdk.PrepareEnv();
            onFirebaseReady?.Invoke(); // Firebase 初始化完成

            // 初始化各管理器
            PrepareManger();

            // SDK 普通拉取开始前接好业务缓存回调；订阅由应用生命周期管理。
            SDK.Remote.RemoteConfigService.RegisterRemoteConfigCallback();
            SDK.Remote.RemoteConfigService.BeginSdkInitializationTracking();
            
            // 初始化 SDK 
            await GuruSdk.Initialize(new AppEnv(
                appSpec: MainAppSpec,
                protocol: new GameProtocol(),
                root: new GameRoot(new List<GuruPackage>())));
            SDK.Remote.RemoteConfigService.NotifySdkInitializationDispatched();

            LoginDay();
            onInitCallback?.Invoke();
        }
        
        
        // 初始化各管理器
        private void PrepareManger()
        {
            _adsManager = new GameAdsManager();
        }

        
        


    }

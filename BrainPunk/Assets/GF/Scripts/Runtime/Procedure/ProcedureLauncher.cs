using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using YooAsset;

namespace GF
{
    public class ProcedureLauncher: FsmState<App>
    {
        public override void OnEnter(params object[] args)
        {
            base.OnEnter(args);
            
            // 派发进入启动流程事件，以便其他模块监听，走相应的Loading逻辑
            App.Event.DispatchEvent(Define.Event.EnterLauncherProcedure);

            InitAsync().Forget();
        }

        private async UniTask InitAsync()
        {
            // Guru Utils 启动逻辑
            
            Debug.Log($"=========== GURU UTILS INIT ============");
            
            
            string packageName = GameSettingData.Setting.defaultPackageName;
            BuiltinPackageElement packageElement = GameSettingData.Setting.GetDefaultPackageElement(packageName);
            EPlayMode playMode = packageElement.playMode;
            //获取时间戳，毫秒
            long timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;

            InitializationOperation initializationOperation = null;
            if (playMode == EPlayMode.OfflinePlayMode)
            {
                initializationOperation = App.Res.InitOfflinePackage(packageName);
            }
            else if (playMode == EPlayMode.EditorSimulateMode)
            {
                initializationOperation = App.Res.InitSimulatePackage(packageName);
            }
            else if(playMode == EPlayMode.HostPlayMode)
            {
                // //请求网关
                // string gatewayUrl = GameSettingData.GetGatewayUrl();
                // string mainPackageVersion = null;
                // if (!string.IsNullOrEmpty(gatewayUrl))
                // {
                //     JObject json = await App.Http.GetJsonWithDecrypto(gatewayUrl,new Dictionary<string, string>()
                //     {
                //         ["device_id"] = App.LocalStorage.GetDeviceId(),
                //         ["app_version"] = "1.0.0"
                //     });
                //     LogKit.I($"GetDeviceId = {App.LocalStorage.GetDeviceId()}");
                //     if (json != null)
                //     {
                //         JToken json2 = json["data"];
                //         Gateway gateway = Gateway.GetGateway(json2.ToString());
                //         gateway.AddJwtTokenTimer().Forget();
                //
                //         mainPackageVersion = gateway.max_gamepackage_version;
                //         CDNConfig cdnConfig = CDNConfig.GetInstance();
                //         cdnConfig.SetCDNFromServer(gateway.cdn_url);
                //         cdnConfig.SetCDNFallbackFromServer(gateway.cdn_fallback_url);
                //     }
                // }
                // else
                // {
                //     //网关未配置使用cdn配置版本号
                //     mainPackageVersion = await RemoteVersionHelper.RequestRemoteVersion(packageName);
                // }
                //
                // if (string.IsNullOrEmpty(mainPackageVersion))
                // {
                //     //获取版本号失败
                //     LogKit.E($"{packageName} 获取远端版本号失败");
                //     return;
                // }
                //
                // LogKit.I($"{packageName} 远端版本号: {mainPackageVersion}");
                // initializationOperation = App.Res.InitHostPackage(packageName, mainPackageVersion);
            }
            else
            {
                LogKit.E("暂不支持...");
                return;
            }

            await initializationOperation.ToUniTask();
            //计划完成时，使用时间，毫秒
            long timestamp2 = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
            LogKit.I($"初始化完成，耗时：{timestamp2 - timestamp}ms");
            
            ResourcePackage package = App.Res.TryGetPackage(packageName);
            App.Res.SetDefaultPackage(package);
            
            // 派发退出启动流程事件，以便其他模块监听，走相应的Loading逻辑
            App.Event.DispatchEvent(Define.Event.ExitLauncherProcedure);

            if (playMode == EPlayMode.OfflinePlayMode || playMode == EPlayMode.EditorSimulateMode)
            {
               //进入热更层
               App.ChangeProcedure<ProcedureGame>();
            }
            else if (playMode == EPlayMode.HostPlayMode)
            {
                // App.ChangeProcedure<ProcedureHotUpdate>(_package.PackageName);
                App.ChangeProcedure<ProcedureGame>();
            }

            //流程：打开热更ui，初始化yooasset package，请求远端最新版本号，对比版本号并请求远端manifest，
             
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using YooAsset;

namespace GF
{
    [Serializable]
    public class PlatformSetting
    {
        [Header("包名")]
        public string package = "com.guru.xyz";
        [Header("公司名")]
        public string company = "guru";
        [Header("游戏名")]
        public string productName = "GuruGame";
        [Header("Android签名文件路径,Assets目录的相对路径")]
        public string keyStoreFilePath = "../keystore/guru.jks";
        [Header("Android签名密码")]
        public string keyStorePass = "guru0622";
        [Header("Android签名别名")]
        public string keyaliasName = "guru";
        [Header("Android签名别名密码")]
        public string keyaliasPass = "guru0622";
        [Header("苹果开发者ID")]
        public string appleDeveloperTeamID = "39253T242A";
    }

    [Serializable]
    public class AppInfo
    {
        [Header("应用版本号")]
        public string appVersion;
        [Header("应用构建号")]
        public string appBuildCode;
    }
    
    [Serializable]
    public class BuiltinInfo
    {
        [Header("键")]
        public string key;
        [Header("值")]
        public string value;
    }
    
    [CreateAssetMenu(fileName = "GameSetting", menuName = "GF/Create GameSetting")]
    public class GameSetting: ScriptableObject
    {
        [Header("Android/iOS配置")]
        public PlatformSetting platformSetting;
        
        [Header("应用信息"), HideInInspector]
        public AppInfo appInfo;

        [Header("YooAsset 构建管线")]
        public EDefaultBuildPipeline buildPipeline;
        [Header("包内package对应的PlayMode\n!!! HostMode必须填写Generation和YooVersion\n!!! EditorSimulateMode不需要填写Generation和YooVersion")]
        public List<BuiltinPackageElement> builtinPackageList; //包内package对应的PlayMode
        public string defaultPackageName;
        [Header("内置信息")]
        public List<BuiltinInfo> builtinInfo;
        [HideInInspector]
        public string hostUrl;
        public string cdnUrl;
        public string cdnUrlFallback;
        
        [Header("动态链接配置")]
        public string[] AssociateDomains;
        
        /// <summary>
        /// 资源下载地址
        /// </summary>
        public string GetCdnUrl()
        {
            return App.LocalStorage.GetData(Define.LocalStorage.STORAGE_CDN, cdnUrl);
        }

        public void SetCdnUrl(string cdn)
        {
            App.LocalStorage.SetData(Define.LocalStorage.STORAGE_CDN, cdnUrl);
            cdnUrl = cdn;
        }
        
        /// <summary>
        /// 资源下载备用地址
        /// </summary>
        public string GetCdnUrlFallback()
        {
            return App.LocalStorage.GetData(Define.LocalStorage.STORAGE_CDN_FALLBACK, cdnUrlFallback);
        }
    
        public void SetCdnUrlFallback(string cdnFallback)
        {
            cdnUrlFallback = cdnFallback;
            App.LocalStorage.SetData(Define.LocalStorage.STORAGE_CDN_FALLBACK, cdnUrlFallback);
        }

        /// <summary>
        /// 是否存在设定的运行模式
        /// </summary>
        /// <param name="packageName"></param>
        /// <returns></returns>
        public bool HasDefaultPlayMode(string packageName)
        {
            foreach (BuiltinPackageElement packageElement in builtinPackageList)
            {
                if (packageElement.packageName == packageName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取默认的YooVersion
        /// </summary>
        /// <param name="packageName"></param>
        /// <returns></returns>
        public BuiltinPackageElement GetDefaultPackageElement(string packageName = null)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                packageName = defaultPackageName;
            }

            foreach (BuiltinPackageElement packageElement in builtinPackageList)
            {
                if (packageElement.packageName == packageName)
                {
                    return packageElement;
                }
            }

            LogKit.I($"不存在该包名 :{packageName}");
            return null;
        }

        /// <summary>
        /// 设置PackageName默认的运行模式
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="playMode"></param>
        /// <param name="generation"></param>
        /// <param name="yooVersion"></param>
        public void SetDefaultPackagePlayMode(string packageName, EPlayMode playMode, string generation, string yooVersion)
        {
            bool found = false;
            foreach (BuiltinPackageElement packageElement in builtinPackageList)
            {
                if (packageElement.packageName == packageName)
                {
                    packageElement.playMode = playMode;
                    packageElement.generation = generation;
                    packageElement.yooVersion = yooVersion;
                    found = true;
                    break;
                }
            }

            //若不存在则添加
            if (!found)
            {
                BuiltinPackageElement element = new BuiltinPackageElement();
                element.packageName = packageName;
                element.playMode = playMode;
                element.generation = generation;
                element.yooVersion = yooVersion;
                
                builtinPackageList.Add(element);
            }
        }
        
        /// <summary>
        /// 清理默认的运行模式
        /// </summary>
        public void ClearDefaultPlayMode()
        {
            builtinPackageList.Clear();
        }
        
        /// <summary>
        /// 获取内置信息
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetBuiltInInfo(string key)
        {
            foreach (BuiltinInfo info in builtinInfo)
            {
                if (info.key == key)
                {
                    return info.value;
                }
            }

            return null;
        }
        
        /// <summary>
        /// 清理内置信息
        /// </summary>
        public void ClearBuiltInInfo()
        {
            builtinInfo.Clear();
        }
    }

    [Serializable]
    public class BuiltinPackageElement
    {
        //包名
        public string packageName;
        //运行模式
        public EPlayMode playMode;
        //运控generation
        public string generation;
        //包体内的版本号
        public string yooVersion;
    }
}
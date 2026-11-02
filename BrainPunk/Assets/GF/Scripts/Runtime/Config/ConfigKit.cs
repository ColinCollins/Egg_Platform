using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace GF
{
    public class ConfigKit
    {
        private Dictionary<int, TableBase> _tableDic = new Dictionary<int, TableBase>();
        
        //YooAsset包名
        public static string PackageName = "ConfigPackage";

        private bool _isInitialized;

        /// <summary>
        /// 加载配置的Package
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="generation"></param>
        /// <param name="successCallback"></param>
        /// <param name="failCallback"></param>
        /// <param name="progressCallback"></param>
        public async UniTask LoadPackage(string packageName, string generation, Action successCallback, Action failCallback, Action<float> progressCallback)
        {
            string yooVerison = "";
            if (!string.IsNullOrEmpty(generation))
            {
                yooVerison = App.LocalStorage.GetData($"{Define.LocalStorage.GENERATION_TO_YOOASSETS}{generation}", "");
            }

            BuiltinPackageElement element = GameSettingData.Setting.GetDefaultPackageElement(packageName);
            if (element != null && element.generation == generation && yooVerison == "")
            {
                yooVerison = element.yooVersion;
            }

            if (element != null && element.playMode == EPlayMode.EditorSimulateMode)
            {
                //Editor模式，使用本地资源
                InitializationOperation initializationOperation = App.Res.InitSimulatePackage(PackageName);
                await initializationOperation.ToUniTask();
            }
            else
            {
                //其他模式，使用Host模式
                UniTaskCompletionSource taskCompletionSource = new UniTaskCompletionSource();
                DownLoadTaskQueueMgr.Instance.StartDownload(packageName, generation, yooVerison, EDownloadTaskPriority.VeryHigh,
                    (pkgName) =>
                    {
                        _isInitialized = true;
                        LogKit.I("加载成功");
                        successCallback?.Invoke();
                        taskCompletionSource.TrySetResult();
                    }, (pkgName) =>
                    {
                        failCallback?.Invoke();
                        taskCompletionSource.TrySetException(new Exception("Fail"));
                    }, (pkgName, progress) =>
                    {
                        progressCallback?.Invoke(progress);
                    });
                
                await taskCompletionSource.Task;
            }
        }

        /// <summary>
        /// 是否初始化完成
        /// </summary>
        /// <returns></returns>
        public bool IsInitialized()
        {
            return _isInitialized;
        }
        
        public async UniTask LoadConfig<T>(string path) where T: TableBase,new()
        {
            TextAsset ta = await App.Res.LoadAssetAsync<TextAsset>(path, path, PackageName);
            T tableBase = new T();
            tableBase.Deserialize(ta.text);
            _tableDic[typeof(T).GetHashCode()] = tableBase;
            App.Res.ReleaseAsset(path);
        }

        public T GetTable<T>() where T: TableBase
        {
            int code = typeof(T).GetHashCode();
            if (_tableDic.TryGetValue(code, out TableBase table))
            {
                return table as T;
            }

            LogKit.E("不存在配置：" + typeof(T));
            return null;
        }

        public T2 GetTableItem<T1, T2>(int id) where T1: TableBase where T2: TableItemBase
        {
            T1 table = GetTable<T1>();
            if (table != null)
            {
                return table.GetItem<T2>(id);
            }

            return null;
        }

        public void Destroy()
        {
            _tableDic.Clear();
            _tableDic = null;
        }
    }
}
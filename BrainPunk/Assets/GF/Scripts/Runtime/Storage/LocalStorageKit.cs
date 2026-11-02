using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GF
{
    [Serializable]
    public class StorageData
    {
        public Dictionary<string, string> stringData;
        public Dictionary<string, int> intData;
        public Dictionary<string, float> floatData;
        public Dictionary<string, bool> boolData;
    }

    [Serializable]
    public class RemoteStorageData
    {
        public int storage_version;
        public StorageData data;
    }

    /// <summary>
    /// 本地存储
    /// </summary>
    public class LocalStorageKit
    {
        enum TypeEnum
        {
            Int,
            Float,
            String,
            Bool
        }

        private string _playerID;
        private readonly string STORAGE_ALL_KEYS_KEY = "STORAGE_ALL_KEYS_KEY";
        private readonly string STORAGE_VERSION_KEY = "STORAGE_VERSION_KEY";
        private Dictionary<string, TypeEnum> _key2Type;

        //是否开启云存储
        public bool IsCloudStorage { get; set; } = true;

        public LocalStorageKit()
        {
            string keyString = PlayerPrefs.GetString(STORAGE_ALL_KEYS_KEY, "");
            if (string.IsNullOrEmpty(keyString))
            {
                _key2Type = new Dictionary<string, TypeEnum>();
            }
            else
            {
                _key2Type = Utility.Json.Deserialize<Dictionary<string, TypeEnum>>(keyString);
            }
        }

        /// <summary>
        /// 设置PlayerID
        /// </summary>
        /// <param name="id"></param>
        public void SetPlayerID(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                LogKit.E("id 为空");
                return;
            }

            _playerID = id;
        }

        public int GetStorageVersion()
        {
            return GetData(STORAGE_VERSION_KEY, 0);
        }
        
        public string GetDeviceId()
        {
            return SystemInfo.deviceUniqueIdentifier;
        }

        public bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            
            key = AppendID(key);
            return PlayerPrefs.HasKey(key);
        }

        /// <summary>
        /// 保存int型数据
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetData(string key, int value)
        {
            if (string.IsNullOrEmpty(key))
            {
                LogKit.E("key 为空");
                return;
            }

            AddKey(key, TypeEnum.Int);
            key = AppendID(key);
            PlayerPrefs.SetInt(key, value);
        }

        /// <summary>
        /// 保存float型数据
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetData(string key, float value)
        {
            if (string.IsNullOrEmpty(key))
            {
                LogKit.E("key 为空");
                return;
            }

            AddKey(key, TypeEnum.Float);
            key = AppendID(key);
            PlayerPrefs.SetFloat(key, value);
        }

        /// <summary>
        /// 保存string型数据
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetData(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                LogKit.E("key 为空");
                return;
            }

            AddKey(key, TypeEnum.String);
            key = AppendID(key);
            PlayerPrefs.SetString(key, value);
        }
        
        /// <summary>
        /// 保存bool型数据
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetData(string key, bool value)
        {
            if (string.IsNullOrEmpty(key))
            {
                LogKit.E("key 为空");
                return;
            }

            AddKey(key, TypeEnum.Bool);
            key = AppendID(key);
            PlayerPrefs.SetInt(key, value ? 1 : 0);
        }

        /// <summary>
        /// 获取int型数据
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultVal"></param>
        /// <returns></returns>
        public int GetData(string key, int defaultVal)
        {
            if (string.IsNullOrEmpty(key))
            {
                LogKit.E("key 为空");
                return defaultVal;
            }

            key = AppendID(key);
            return PlayerPrefs.GetInt(key, defaultVal);
        }

        /// <summary>
        /// 获取float型数据
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultVal"></param>
        /// <returns></returns>
        public float GetData(string key, float defaultVal)
        {
            if (string.IsNullOrEmpty(key))
            {
                LogKit.E("key 为空");
                return defaultVal;
            }

            key = AppendID(key);
            return PlayerPrefs.GetFloat(key, defaultVal);
        }

        /// <summary>
        /// 获取string型数据
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultVal"></param>
        /// <returns></returns>
        public string GetData(string key, string defaultVal)
        {
            if (string.IsNullOrEmpty(key))
            {
                LogKit.E("key 为空");
                return defaultVal;
            }

            key = AppendID(key);
            if (!PlayerPrefs.HasKey(key))
            {
                return defaultVal;
            }
            return PlayerPrefs.GetString(key, defaultVal);
        }
        
        /// <summary>
        /// 获取bool型数据
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultVal"></param>
        /// <returns></returns>
        public bool GetData(string key, bool defaultVal)
        {
            if (string.IsNullOrEmpty(key))
            {
                LogKit.E("key 为空");
                return defaultVal;
            }

            key = AppendID(key);
            return PlayerPrefs.GetInt(key, defaultVal ? 1 : 0) == 1;
        }
        
        /// <summary>
        /// 删除key
        /// </summary>
        /// <param name="key"></param>
        public void DeleteKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                LogKit.E("key 为空");
                return;
            }

            key = AppendID(key);
            PlayerPrefs.DeleteKey(key);
            RemoveKey(key);
        }

        /// <summary>
        /// 保存到本地文件
        /// </summary>
        public void Save()
        {
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// 检测更新Storage
        /// </summary>
        public async UniTask CheckRemoteStorage()
        {
            // int remoteStorageVersion = Gateway.GetGateway().storage_version;
            // LogKit.I($"服务器Storage版本号：{remoteStorageVersion}，本地Storage版本号：{GetStorageVersion()}");
            // if (remoteStorageVersion > GetStorageVersion())
            // {
            //     //下载服务器storage
            //     await DownloadStorage();
            // }
        }
        
        public async UniTask DownloadStorage()
        {
            string url = GameSettingData.GetHost() + "/idle/storage/storage";
            JObject jObject =
                await App.Http.GetJsonWithDecrypto(url);

            string errorCode = (string) jObject["err_code"];
            if (errorCode == "0")
            {
                string deStr = jObject?["data"]?.ToString();
                RemoteStorageData remoteStorageData = Utility.Json.Deserialize<RemoteStorageData>(deStr);
                //更新本地数据
                Dictionary<string, TypeEnum> tmpDic = new Dictionary<string, TypeEnum>();
                foreach (var kv in remoteStorageData.data.intData)
                {
                    SetData(kv.Key, kv.Value);
                    tmpDic[kv.Key] = TypeEnum.Int;
                }
                
                foreach (var kv in remoteStorageData.data.floatData)
                {
                    SetData(kv.Key, kv.Value);
                    tmpDic[kv.Key] = TypeEnum.Float;
                }
                
                foreach (var kv in remoteStorageData.data.stringData)
                {
                    SetData(kv.Key, kv.Value);
                    tmpDic[kv.Key] = TypeEnum.String;
                }
                
                foreach (var kv in remoteStorageData.data.boolData)
                {
                    SetData(kv.Key, kv.Value);
                    tmpDic[kv.Key] = TypeEnum.Bool;
                }
                
                //更新all key
                _key2Type = tmpDic;
                string jsonData = Utility.Json.Serialize(_key2Type);
                PlayerPrefs.SetString(STORAGE_ALL_KEYS_KEY, jsonData);
                
                SetStorageVersion(remoteStorageData.storage_version);
                LogKit.I("本地更新Storage完成");
                
                //派发更新事件
                App.Event.DispatchEvent(BuiltinEventDefine.Storage.UpdateComplete);
            }
            else
            {
                LogKit.E(errorCode);
            }
        }

        // """错误码定义"""
        // #正常
        // ERR_OK = 0
        // #参数错误
        // ERR_PARAM_ERROR = 1
        // #token无效
        // ERR_TOKEN_INVALID = 2
        // #token过期
        // ERR_TOKEN_EXPIRED = 3
        // #存在更高版本，设置失败
        // ERR_EXIST_HIGHER_VERSION = 4
        // #Authorization 不存在
        // ERR_AUTHORIZATION_NOT_FOUND = 5
        /// <summary>
        /// 上传Storage
        /// </summary>
        public async UniTask UploadStorage()
        {
            RemoteStorageData data = GetRemoteData();

            string url=GameSettingData.GetHost() + "/idle/storage/storage";
            JObject json = await App.Http.PostJsonWithDecrypto(url, new Dictionary<string, string>()
            {
                ["data"]=Utility.Json.Serialize(data)
            });

            string errorCode = (string) json["err_code"];
            if (errorCode == "0")
            {
                // string dataJson = CryptoAES.Decrypt((string) json["data"]);
                // JObject dataJ = Utility.Json.Deserialize<JObject>(dataJson);
                // int remoteVersion = (int) dataJ["storage_version"];
                int remoteVersion = (int) json["data"]?["storage_version"];
                SetStorageVersion(remoteVersion);
            }
            else if (errorCode == "1")
            {
                LogKit.E("参数错误");
            }
            else if (errorCode == "2" || errorCode == "3")
            {
                LogKit.E("token 失效或过期，重新获取token");
                //token失效，需要重新获取
                App.Event.DispatchEvent(BuiltinEventDefine.Gateway.TokenInvalid);
            }
            else if (errorCode == "4")
            {
                LogKit.E("存在更高版本，设置失败");
            }
            else if (errorCode == "5")
            {
                LogKit.E("Authorization 不存在");
            }
        }
        
        public void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                Save();
            }
        }

        public void Destroy()
        {
            Save();
        }

        #region Private

        private void AddKey(string key, TypeEnum type)
        {
            _key2Type[key] = type;
            string jsonData = Utility.Json.Serialize(_key2Type);
            PlayerPrefs.SetString(STORAGE_ALL_KEYS_KEY, jsonData);
        }
        
        private void RemoveKey(string key)
        {
            if (_key2Type.ContainsKey(key))
            {
                _key2Type.Remove(key);
                string jsonData = Utility.Json.Serialize(_key2Type);
                PlayerPrefs.SetString(STORAGE_ALL_KEYS_KEY, jsonData);
            }
        }

        private string GetPlayerID()
        {
            if (_playerID == null)
            {
                _playerID = "";
            }

            return _playerID;
        }

        private string AppendID(string key)
        {
            return $"{GetPlayerID()}_{key}";
        }

        private RemoteStorageData GetRemoteData()
        {
            RemoteStorageData remoteStorageData = new RemoteStorageData();
            StorageData storageData = new StorageData();
            storageData.intData = new Dictionary<string, int>();
            storageData.floatData = new Dictionary<string, float>();
            storageData.stringData = new Dictionary<string, string>();
            storageData.boolData = new Dictionary<string, bool>();

            foreach (var kv in _key2Type)
            {
                if (kv.Value == TypeEnum.Int)
                {
                    storageData.intData[kv.Key] = PlayerPrefs.GetInt(AppendID(kv.Key));
                }
                else if (kv.Value == TypeEnum.Float)
                {
                    storageData.floatData[kv.Key] = PlayerPrefs.GetFloat(AppendID(kv.Key));
                }
                else if (kv.Value == TypeEnum.String)
                {
                    storageData.stringData[kv.Key] = PlayerPrefs.GetString(AppendID(kv.Key));
                }
                else if (kv.Value == TypeEnum.Bool)
                {
                    storageData.boolData[kv.Key] = PlayerPrefs.GetInt(AppendID(kv.Key)) == 1;
                }
            }

            remoteStorageData.data = storageData;
            remoteStorageData.storage_version = GetStorageVersion();
            return remoteStorageData;
        }
        
        private void SetStorageVersion(int version)
        {
            // Gateway.GetGateway().storage_version = version;
            SetData(STORAGE_VERSION_KEY, version);
            LogKit.E("更新Storage Version：" + version);
        }

        #endregion
    }
}
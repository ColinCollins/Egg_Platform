using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Bear.SaveModule
{
    /// <summary>
    /// 统一存储管理器
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        private static SaveManager _instance;
        public static SaveManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("SaveManager");
                    _instance = go.AddComponent<SaveManager>();
                    if (Application.isPlaying)
                        DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        
        private Dictionary<StorageType, ISaveProvider> _providers = new Dictionary<StorageType, ISaveProvider>();
        private IServerSyncProvider _serverSyncProvider;
        private bool _initialized = false;
        
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// 初始化存储管理器
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }
            
            _providers[StorageType.PlayerPrefs] = new PlayerPrefsSaveProvider();
            _providers[StorageType.Json] = new JsonSaveProvider();
            
            _initialized = true;
        }
        
        /// <summary>
        /// 设置服务器同步提供者
        /// </summary>
        public void SetServerSyncProvider(IServerSyncProvider provider)
        {
            _serverSyncProvider = provider;
        }
        
        /// <summary>
        /// 获取存储提供者
        /// </summary>
        private ISaveProvider GetProvider(StorageType storageType)
        {
            if (storageType == StorageType.Auto)
            {
                storageType = StorageType.Json;
            }
            
            if (_providers.TryGetValue(storageType, out var provider))
            {
                return provider;
            }
            
            Debug.LogWarning($"Provider for {storageType} not found, using Json");
            return _providers[StorageType.Json];
        }
        
        /// <summary>
        /// 保存数据
        /// </summary>
        public bool Save<T>(string key, T data, StorageType storageType = StorageType.Auto) where T : class
        {
            if (!_initialized)
            {
                Initialize();
            }
            
            try
            {
                string json = JsonConvert.SerializeObject(data);
                var provider = GetProvider(storageType);
                return provider.Save(key, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Save failed for key {key}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 异步保存数据
        /// </summary>
        public async Task<bool> SaveAsync<T>(string key, T data, StorageType storageType = StorageType.Auto) where T : class
        {
            if (!_initialized)
            {
                Initialize();
            }
            
            try
            {
                string json = JsonConvert.SerializeObject(data);
                var provider = GetProvider(storageType);
                return await provider.SaveAsync(key, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveAsync failed for key {key}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 读取数据
        /// </summary>
        public T Load<T>(string key, StorageType storageType = StorageType.Auto) where T : class
        {
            if (!_initialized)
            {
                Initialize();
            }
            
            try
            {
                var provider = GetProvider(storageType);
                string json = provider.Load(key);
                
                if (string.IsNullOrEmpty(json))
                {
                    return null;
                }
                
                T data = JsonConvert.DeserializeObject<T>(json);
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Load failed for key {key}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 异步读取数据
        /// </summary>
        public async Task<T> LoadAsync<T>(string key, StorageType storageType = StorageType.Auto) where T : class
        {
            if (!_initialized)
            {
                Initialize();
            }
            
            try
            {
                var provider = GetProvider(storageType);
                string json = await provider.LoadAsync(key);
                
                if (string.IsNullOrEmpty(json))
                {
                    return null;
                }
                
                T data = JsonConvert.DeserializeObject<T>(json);
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"LoadAsync failed for key {key}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 检查数据是否存在
        /// </summary>
        public bool HasKey(string key, StorageType storageType = StorageType.Auto)
        {
            if (!_initialized)
            {
                Initialize();
            }
            
            var provider = GetProvider(storageType);
            return provider.HasKey(key);
        }
        
        /// <summary>
        /// 删除数据
        /// </summary>
        public bool Delete(string key, StorageType storageType = StorageType.Auto)
        {
            if (!_initialized)
            {
                Initialize();
            }
            
            var provider = GetProvider(storageType);
            return provider.Delete(key);
        }
        
        /// <summary>
        /// 同步到服务器
        /// </summary>
        public async Task<bool> SyncToServer(string key, StorageType storageType = StorageType.Auto)
        {
            if (_serverSyncProvider == null)
            {
                Debug.LogWarning("ServerSyncProvider is not set");
                return false;
            }
            
            try
            {
                var provider = GetProvider(storageType);
                string data = provider.Load(key);
                
                if (string.IsNullOrEmpty(data))
                {
                    Debug.LogWarning($"No data found for key {key}");
                    return false;
                }
                
                return await _serverSyncProvider.UploadAsync(key, data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SyncToServer failed for key {key}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 从服务器同步
        /// </summary>
        public async Task<bool> SyncFromServer(string key, StorageType storageType = StorageType.Auto)
        {
            if (_serverSyncProvider == null)
            {
                Debug.LogWarning("ServerSyncProvider is not set");
                return false;
            }
            
            try
            {
                string data = await _serverSyncProvider.DownloadAsync(key);
                
                if (string.IsNullOrEmpty(data))
                {
                    Debug.LogWarning($"No data found on server for key {key}");
                    return false;
                }
                
                var provider = GetProvider(storageType);
                return await provider.SaveAsync(key, data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SyncFromServer failed for key {key}: {ex.Message}");
                return false;
            }
        }
    }
}


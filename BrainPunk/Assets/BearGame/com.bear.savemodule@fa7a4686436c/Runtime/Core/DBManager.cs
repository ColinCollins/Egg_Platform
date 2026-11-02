using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Jobs;
using UnityEngine;

namespace Bear.SaveModule
{
    /// <summary>
    /// 数据管理器 - 统一管理所有数据类的静态实例
    /// </summary>
    public class DBManager : MonoBehaviour
    {
        private static DBManager _instance;
        public static DBManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("DBManager");
                    _instance = go.AddComponent<DBManager>();
                    if (Application.isPlaying)
                        DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private Dictionary<Type, BaseSaveDataSO> _dataInstances = new Dictionary<Type, BaseSaveDataSO>();
        private DBSetting _dbSetting;
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
        /// 初始化数据管理器
        /// </summary>
        /// <param name="dbSetting">数据库设置（可选，如果不提供则自动扫描）</param>
        public void Initialize(DBSetting dbSetting = null)
        {
            if (_initialized)
            {
                Debug.LogWarning("DBManager: Already initialized");
                return;
            }

            // 确保 SaveManager 已初始化
            SaveManager.Instance.Initialize();

            _dbSetting = dbSetting;
            _dataInstances = new Dictionary<Type, BaseSaveDataSO>();

            // 如果没有提供 DBSetting，尝试从资源中加载
            if (_dbSetting == null)
            {
                _dbSetting = Resources.FindObjectsOfTypeAll<DBSetting>().FirstOrDefault();
                if (_dbSetting == null)
                {
                    Debug.LogWarning("DBManager: DBSetting not found. Will scan all BaseSaveDataSO classes.");
                }
            }

            // 扫描并初始化所有数据类
            ScanAndInitializeDataClasses();

            _initialized = true;
            Debug.Log($"DBManager: Initialized with {_dataInstances.Count} data classes");
        }

        /// <summary>
        /// 扫描并初始化所有数据类
        /// </summary>
        private void ScanAndInitializeDataClasses()
        {
            Type baseSaveDataSOType = typeof(BaseSaveDataSO);
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (Assembly assembly in assemblies)
            {
                try
                {
                    Type[] types = assembly.GetTypes();
                    foreach (Type type in types)
                    {
                        if (!type.IsClass || type.IsAbstract)
                        {
                            continue;
                        }

                        if (!baseSaveDataSOType.IsAssignableFrom(type) || type == baseSaveDataSOType)
                        {
                            continue;
                        }

                        // 创建实例
                        BaseSaveDataSO instance = CreateScriptableObjectInstance(type);
                        if (instance != null)
                        {
                            _dataInstances[type] = instance;

                            // 根据存储方式加载数据
                            LoadDataForInstance(instance, type);
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Debug.LogWarning($"DBManager: Failed to load types from assembly {assembly.FullName}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"DBManager: Error scanning assembly {assembly.FullName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 创建 ScriptableObject 实例
        /// </summary>
        private BaseSaveDataSO CreateScriptableObjectInstance(Type type)
        {
            try
            {
                BaseSaveDataSO instance = ScriptableObject.CreateInstance(type) as BaseSaveDataSO;
                if (instance == null)
                {
                    Debug.LogError($"DBManager: Failed to create instance of type {type.Name}");
                    return null;
                }
                return instance;
            }
            catch (Exception ex)
            {
                Debug.LogError($"DBManager: Error creating instance of {type.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 为实例加载数据
        /// </summary>
        private void LoadDataForInstance(BaseSaveDataSO instance, Type type)
        {
            try
            {
                string key = type.Name;
                StorageType storageType = type.GetStorageType();

                // 确定实际的存储类型
                if (storageType == StorageType.Auto)
                {
                    storageType = StorageType.Json;
                }

                // 创建对应的 provider
                ISaveProvider provider = null;
                switch (storageType)
                {
                    case StorageType.PlayerPrefs:
                        provider = new PlayerPrefsSaveProvider();
                        break;
                    case StorageType.Json:
                        provider = new JsonSaveProvider();
                        break;
                    default:
                        provider = new JsonSaveProvider();
                        break;
                }

                if (provider == null)
                {
                    Debug.LogWarning($"DBManager: Provider not found for {type.Name} with storage type {storageType}");
                    return;
                }

                string json = provider.Load(key);

                if (!string.IsNullOrEmpty(json))
                {
                    // 使用 FromJson 方法填充实例
                    instance.FromJson(json);
                    Debug.Log($"DBManager: Loaded data for {type.Name} from {storageType}");
                }
                else
                {
                    // 没有缓存数据，调用 Init() 初始化默认值并保存
                    Debug.Log($"DBManager: No saved data found for {type.Name}, initializing with default values");
                    instance.Init();
                    
                    // 保存初始化后的数据
                    try
                    {
                        instance.Save();
                        Debug.Log($"DBManager: Initialized and saved default data for {type.Name}");
                    }
                    catch (Exception saveEx)
                    {
                        Debug.LogError($"DBManager: Failed to save initialized data for {type.Name}: {saveEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"DBManager: Error loading data for {type.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定类型的数据实例
        /// </summary>
        public T Get<T>() where T : BaseSaveDataSO
        {
            Type type = typeof(T);
            if (_dataInstances.TryGetValue(type, out BaseSaveDataSO instance))
            {
                return instance as T;
            }

            Debug.LogWarning($"DBManager: Data instance for type {type.Name} not found. Make sure Initialize() has been called.");
            return null;
        }

        /// <summary>
        /// 保存指定类型的数据
        /// </summary>
        public bool Save<T>() where T : BaseSaveDataSO
        {
            T instance = Get<T>();
            if (instance != null)
            {
                return instance.Save();
            }
            return false;
        }

        /// <summary>
        /// 保存所有数据
        /// </summary>
        public void SaveAll()
        {
            foreach (var kvp in _dataInstances)
            {
                try
                {
                    kvp.Value.Save();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"DBManager: Error saving {kvp.Key.Name}: {ex.Message}");
                }
            }
            Debug.Log("DBManager: All data saved");
        }

        /// <summary>
        /// 检查是否已初始化
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// 获取所有已注册的数据类型
        /// </summary>
        public IEnumerable<Type> GetRegisteredTypes()
        {
            return _dataInstances.Keys;
        }

        /// <summary>
        /// 清除所有缓存的数据实例
        /// </summary>
        /// <param name="destroyInstances">是否销毁 ScriptableObject 实例（默认 true）</param>
        public void ClearCache(bool destroyInstances = true)
        {
            int count = _dataInstances.Count;

            if (destroyInstances)
            {
                // 销毁所有 ScriptableObject 实例
                foreach (var kvp in _dataInstances)
                {
                    if (kvp.Value != null)
                    {
                        if (Application.isPlaying)
                            GameObject.Destroy(kvp.Value);
                        else
                        {
#if UNITY_EDITOR
                            GameObject.DestroyImmediate(kvp.Value);
#endif
                        }
                    }
                }
            }

            // 清空字典
            _dataInstances.Clear();

            Debug.Log($"DBManager: Cleared cache ({count} data instances cleared)");
        }

        /// <summary>
        /// 清除所有 PlayerPrefs 缓存数据（只清除 SaveModule 保存的数据，带 "SaveData_" 前缀）
        /// </summary>
        /// <param name="onlySaveModuleData">是否只清除 SaveModule 保存的数据（默认 true，只清除带 "SaveData_" 前缀的键）</param>
        public void ClearPlayerPrefsCache(bool onlySaveModuleData = true)
        {
            // PlayerPrefs 在所有平台都支持，包括移动端
            if (!Application.isPlaying)
            {
                Debug.LogWarning("DBManager: ClearPlayerPrefsCache should only be called at runtime");
                return;
            }

            int deletedCount = 0;
            
            try
            {
                if (onlySaveModuleData)
                {
                    // 只清除 SaveModule 保存的数据（带 "SaveData_" 前缀）
                    if (_dbSetting != null && _dbSetting.DataClasses != null)
                    {
                        foreach (var dataClass in _dbSetting.DataClasses)
                        {
                            if (dataClass.storageType == StorageType.PlayerPrefs)
                            {
                                string key = "SaveData_" + dataClass.className;
                                try
                                {
                                    if (UnityEngine.PlayerPrefs.HasKey(key))
                                    {
                                        UnityEngine.PlayerPrefs.DeleteKey(key);
                                        deletedCount++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogError($"DBManager: Failed to delete PlayerPrefs key {key}: {ex.Message}");
                                }
                            }
                        }
                    }
                    
                    // 如果没有 DBSetting，尝试扫描所有 BaseSaveDataSO 类
                    if (deletedCount == 0)
                    {
                        Type baseSaveDataSOType = typeof(BaseSaveDataSO);
                        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                        
                        foreach (Assembly assembly in assemblies)
                        {
                            try
                            {
                                Type[] types = assembly.GetTypes();
                                foreach (Type type in types)
                                {
                                    if (type.IsClass && !type.IsAbstract && baseSaveDataSOType.IsAssignableFrom(type) && type != baseSaveDataSOType)
                                    {
                                        StorageType storageType = type.GetStorageType();
                                        if (storageType == StorageType.PlayerPrefs)
                                        {
                                            string key = "SaveData_" + type.Name;
                                            try
                                            {
                                                if (UnityEngine.PlayerPrefs.HasKey(key))
                                                {
                                                    UnityEngine.PlayerPrefs.DeleteKey(key);
                                                    deletedCount++;
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Debug.LogError($"DBManager: Failed to delete PlayerPrefs key {key}: {ex.Message}");
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"DBManager: Error scanning assembly {assembly.FullName} for PlayerPrefs keys: {ex.Message}");
                            }
                        }
                    }
                    
                    UnityEngine.PlayerPrefs.Save();
                    Debug.Log($"DBManager: Cleared PlayerPrefs cache ({deletedCount} SaveModule keys deleted)");
                }
                else
                {
                    // 清除所有 PlayerPrefs 数据（危险操作）
                    try
                    {
                        UnityEngine.PlayerPrefs.DeleteAll();
                        UnityEngine.PlayerPrefs.Save();
                        Debug.LogWarning("DBManager: Cleared ALL PlayerPrefs data (including Unity system data)");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"DBManager: Failed to clear all PlayerPrefs: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"DBManager: Error clearing PlayerPrefs cache: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除所有 Json 文件缓存数据（只清除 SaveModule 保存的数据，文件名格式为 "类名.json"）
        /// </summary>
        /// <param name="onlySaveModuleData">是否只清除 SaveModule 保存的数据（默认 true，只清除 SaveModule 管理的 Json 文件）</param>
        public void ClearJsonCache(bool onlySaveModuleData = true)
        {
            // 检查是否在运行时（文件操作需要在运行时执行）
            if (!Application.isPlaying)
            {
                Debug.LogWarning("DBManager: ClearJsonCache should only be called at runtime");
                return;
            }

            // 检查 persistentDataPath 是否可用（移动端可能在某些情况下不可用）
            if (string.IsNullOrEmpty(Application.persistentDataPath))
            {
                Debug.LogError("DBManager: Application.persistentDataPath is not available on this platform");
                return;
            }

            int deletedCount = 0;
            string saveDirectory = Path.Combine(Application.persistentDataPath, "SaveData");
            
            try
            {
                // 检查目录是否存在
                if (!Directory.Exists(saveDirectory))
                {
                    Debug.LogWarning($"DBManager: Save directory does not exist: {saveDirectory}");
                    return;
                }

                if (onlySaveModuleData)
                {
                    // 只清除 SaveModule 保存的数据（文件名格式为 "类名.json"）
                    if (_dbSetting != null && _dbSetting.DataClasses != null)
                    {
                        foreach (var dataClass in _dbSetting.DataClasses)
                        {
                            if (dataClass.storageType == StorageType.Json || dataClass.storageType == StorageType.Auto)
                            {
                                string fileName = dataClass.className + ".json";
                                string filePath = Path.Combine(saveDirectory, fileName);
                                
                                try
                                {
                                    if (File.Exists(filePath))
                                    {
                                        File.Delete(filePath);
                                        deletedCount++;
                                    }
                                }
                                catch (UnauthorizedAccessException ex)
                                {
                                    Debug.LogError($"DBManager: Access denied when deleting Json file {filePath}. Platform may not support file deletion: {ex.Message}");
                                }
                                catch (IOException ex)
                                {
                                    Debug.LogError($"DBManager: IO error when deleting Json file {filePath}: {ex.Message}");
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogError($"DBManager: Failed to delete Json file {filePath}: {ex.Message}");
                                }
                            }
                        }
                    }
                    
                    // 如果没有 DBSetting，尝试扫描所有 BaseSaveDataSO 类
                    if (deletedCount == 0)
                    {
                        Type baseSaveDataSOType = typeof(BaseSaveDataSO);
                        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                        
                        foreach (Assembly assembly in assemblies)
                        {
                            try
                            {
                                Type[] types = assembly.GetTypes();
                                foreach (Type type in types)
                                {
                                    if (type.IsClass && !type.IsAbstract && baseSaveDataSOType.IsAssignableFrom(type) && type != baseSaveDataSOType)
                                    {
                                        StorageType storageType = type.GetStorageType();
                                        if (storageType == StorageType.Json || storageType == StorageType.Auto)
                                        {
                                            string fileName = type.Name + ".json";
                                            string filePath = Path.Combine(saveDirectory, fileName);
                                            
                                            try
                                            {
                                                if (File.Exists(filePath))
                                                {
                                                    File.Delete(filePath);
                                                    deletedCount++;
                                                }
                                            }
                                            catch (UnauthorizedAccessException ex)
                                            {
                                                Debug.LogError($"DBManager: Access denied when deleting Json file {filePath}. Platform may not support file deletion: {ex.Message}");
                                            }
                                            catch (IOException ex)
                                            {
                                                Debug.LogError($"DBManager: IO error when deleting Json file {filePath}: {ex.Message}");
                                            }
                                            catch (Exception ex)
                                            {
                                                Debug.LogError($"DBManager: Failed to delete Json file {filePath}: {ex.Message}");
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"DBManager: Error scanning assembly {assembly.FullName} for Json files: {ex.Message}");
                            }
                        }
                    }
                    
                    Debug.Log($"DBManager: Cleared Json cache ({deletedCount} SaveModule files deleted)");
                }
                else
                {
                    // 清除所有 Json 文件（危险操作）
                    try
                    {
                        string[] jsonFiles = Directory.GetFiles(saveDirectory, "*.json");
                        foreach (var filePath in jsonFiles)
                        {
                            try
                            {
                                File.Delete(filePath);
                                deletedCount++;
                            }
                            catch (UnauthorizedAccessException ex)
                            {
                                Debug.LogError($"DBManager: Access denied when deleting Json file {filePath}. Platform may not support file deletion: {ex.Message}");
                            }
                            catch (IOException ex)
                            {
                                Debug.LogError($"DBManager: IO error when deleting Json file {filePath}: {ex.Message}");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"DBManager: Failed to delete Json file {filePath}: {ex.Message}");
                            }
                        }
                        Debug.LogWarning($"DBManager: Cleared ALL Json files ({deletedCount} files deleted)");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"DBManager: Failed to clear Json cache: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"DBManager: Error clearing Json cache: {ex.Message}");
            }
        }
    }
}


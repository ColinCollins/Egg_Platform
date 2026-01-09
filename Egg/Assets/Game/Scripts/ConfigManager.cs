using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Common;
using SimpleJSON;
using UnityEngine;

namespace Game.ConfigModule
{
    /// <summary>
    /// 配置管理器
    /// 负责加载和管理 Luban 生成的配置表
    /// </summary>
    public class ConfigManager : MonoSingleton<ConfigManager>
    {
        /// <summary>
        /// 配置表实例
        /// </summary>
        public Config.Tables Tables { get; private set; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 配置加载进度 (0-1)
        /// </summary>
        public float LoadProgress { get; private set; }

        /// <summary>
        /// 配置文件根目录（相对于 StreamingAssets）
        /// </summary>
        [SerializeField] private string configRootPath = "Configs";

        /// <summary>
        /// 配置文件扩展名
        /// </summary>
        private const string CONFIG_EXTENSION = ".json";

        /// <summary>
        /// 已加载的配置数据缓存
        /// </summary>
        private Dictionary<string, JSONNode> _configCache = new Dictionary<string, JSONNode>();

        /// <summary>
        /// 同步初始化配置表
        /// </summary>
        /// <returns>配置表实例，如果加载失败返回 null</returns>
        public Config.Tables Initialize()
        {
            if (IsInitialized && Tables != null)
            {
                Debug.LogWarning("[ConfigManager] Config tables already initialized.");
                return Tables;
            }

            LoadProgress = 0f;
            _configCache.Clear();

            // 获取所有需要加载的配置文件名
            string[] configFileNames = GetConfigFileNames();
            int totalFiles = configFileNames.Length;
            int loadedFiles = 0;

            Debug.Log($"[ConfigManager] Start loading {totalFiles} config files synchronously...");

            // 加载所有配置文件
            foreach (string fileName in configFileNames)
            {
                JSONNode jsonNode = LoadConfigFile(fileName);
                if (jsonNode != null)
                {
                    _configCache[fileName] = jsonNode;
                    loadedFiles++;
                    LoadProgress = (float)loadedFiles / totalFiles;
                    Debug.Log($"[ConfigManager] Loaded: {fileName} ({loadedFiles}/{totalFiles})");
                }
                else
                {
                    Debug.LogError($"[ConfigManager] Failed to load config file: {fileName}");
                }
            }

            // 创建 Tables 实例
            if (_configCache.Count > 0)
            {
                Tables = new Config.Tables(LoadConfigData);
                IsInitialized = true;
                LoadProgress = 1f;
                Debug.Log("[ConfigManager] Config tables initialized successfully.");
                return Tables;
            }
            else
            {
                Debug.LogError("[ConfigManager] No config files loaded!");
                return null;
            }
        }

        /// <summary>
        /// 异步初始化配置表
        /// </summary>
        /// <param name="onComplete">加载完成回调</param>
        /// <returns>协程</returns>
        public Coroutine InitializeAsync(System.Action onComplete = null)
        {
            return StartCoroutine(LoadConfigTablesCoroutine(onComplete));
        }

        /// <summary>
        /// 加载配置表的协程
        /// </summary>
        private IEnumerator LoadConfigTablesCoroutine(System.Action onComplete)
        {
            if (IsInitialized)
            {
                Debug.LogWarning("[ConfigManager] Config tables already initialized.");
                onComplete?.Invoke();
                yield break;
            }

            LoadProgress = 0f;
            _configCache.Clear();

            // 获取所有需要加载的配置文件名
            // 根据 Tables.cs 构造函数中的文件名列表
            string[] configFileNames = GetConfigFileNames();
            int totalFiles = configFileNames.Length;
            int loadedFiles = 0;

            Debug.Log($"[ConfigManager] Start loading {totalFiles} config files...");

            // 加载所有配置文件
            foreach (string fileName in configFileNames)
            {
                JSONNode jsonNode = LoadConfigFile(fileName);
                if (jsonNode != null)
                {
                    _configCache[fileName] = jsonNode;
                    loadedFiles++;
                    LoadProgress = (float)loadedFiles / totalFiles;
                    Debug.Log($"[ConfigManager] Loaded: {fileName} ({loadedFiles}/{totalFiles})");
                }
                else
                {
                    Debug.LogError($"[ConfigManager] Failed to load config file: {fileName}");
                }

                yield return null; // 每加载一个文件后等待一帧
            }

            // 创建 Tables 实例
            if (_configCache.Count > 0)
            {
                Tables = new Config.Tables(LoadConfigData);
                IsInitialized = true;
                LoadProgress = 1f;
                Debug.Log("[ConfigManager] Config tables initialized successfully.");
            }
            else
            {
                Debug.LogError("[ConfigManager] No config files loaded!");
            }

            onComplete?.Invoke();
        }

        /// <summary>
        /// 获取所有配置文件名列表
        /// </summary>
        private string[] GetConfigFileNames()
        {
            return ConfigFileNames.GetFileNames();
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        private JSONNode LoadConfigFile(string fileName)
        {
            string filePath = GetConfigFilePath(fileName);

            // 优先从 Resources 加载
            TextAsset textAsset = Resources.Load<TextAsset>(filePath);
            if (textAsset != null)
            {
                return JSON.Parse(textAsset.text);
            }

            // 如果 Resources 中没有，尝试从 StreamingAssets 加载
            string streamingPath = Path.Combine(Application.streamingAssetsPath, configRootPath, fileName + CONFIG_EXTENSION);
            if (File.Exists(streamingPath))
            {
                try
                {
                    string jsonText = File.ReadAllText(streamingPath);
                    return JSON.Parse(jsonText);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ConfigManager] Failed to read file {streamingPath}: {e.Message}");
                    return null;
                }
            }

            Debug.LogWarning($"[ConfigManager] Config file not found: {fileName}");
            return null;
        }

        /// <summary>
        /// 获取配置文件路径（相对于 Resources）
        /// </summary>
        private string GetConfigFilePath(string fileName)
        {
            if (string.IsNullOrEmpty(configRootPath))
            {
                return fileName;
            }
            return $"{configRootPath}/{fileName}";
        }

        /// <summary>
        /// 配置数据加载器（供 Tables 构造函数使用）
        /// </summary>
        private JSONNode LoadConfigData(string fileName)
        {
            if (_configCache.TryGetValue(fileName, out JSONNode node))
            {
                return node;
            }

            Debug.LogWarning($"[ConfigManager] Config data not found in cache: {fileName}");
            return null;
        }

        /// <summary>
        /// 同步重新加载配置表
        /// </summary>
        /// <returns>配置表实例，如果加载失败返回 null</returns>
        public Config.Tables Reload()
        {
            IsInitialized = false;
            Tables = null;
            _configCache.Clear();
            return Initialize();
        }

        /// <summary>
        /// 异步重新加载配置表
        /// </summary>
        public Coroutine ReloadAsync(System.Action onComplete = null)
        {
            IsInitialized = false;
            Tables = null;
            _configCache.Clear();
            return InitializeAsync(onComplete);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _configCache?.Clear();
        }
    }
}

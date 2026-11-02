using UnityEngine;
using System.Threading.Tasks;
using System.Linq;

namespace Bear.SaveModule.Examples
{
    /// <summary>
    /// 存储系统使用示例
    /// 展示如何使用 SaveManager 进行数据存储和加载
    /// </summary>
    public class SaveSystemUsageExample : MonoBehaviour
    {
        [Header("测试配置")]
        [SerializeField] private string testKey = "PlayerData";
        [SerializeField] private StorageType testStorageType = StorageType.Json;

        private void Start()
        {
            // 初始化存储系统
            SaveManager.Instance.Initialize();

            // 示例1: 基本保存和加载
            Example1_BasicSaveAndLoad();

            // 示例2: 使用 ScriptableObject
            Example2_ScriptableObjectUsage();

            // 示例3: 异步操作
            Example3_AsyncOperations();
        }

        /// <summary>
        /// 示例1: 基本保存和加载
        /// </summary>
        private void Example1_BasicSaveAndLoad()
        {
            Debug.Log("=== 示例1: 基本保存和加载 ===");

            // 创建测试数据
            var testData = new SimpleData
            {
                Score = 100,
                PlayerName = "TestPlayer",
                Level = 5
            };

            // 保存数据
            bool saveResult = SaveManager.Instance.Save(testKey, testData, testStorageType);
            Debug.Log($"保存结果: {saveResult}");

            // 加载数据
            var loadedData = SaveManager.Instance.Load<SimpleData>(testKey, testStorageType);
            if (loadedData != null)
            {
                Debug.Log($"加载成功 - Score: {loadedData.Score}, Name: {loadedData.PlayerName}, Level: {loadedData.Level}");
            }

            // 检查数据是否存在
            bool exists = SaveManager.Instance.HasKey(testKey, testStorageType);
            Debug.Log($"数据是否存在: {exists}");
        }

        /// <summary>
        /// 示例2: 使用 ScriptableObject 和 DBManager
        /// </summary>
        private void Example2_ScriptableObjectUsage()
        {
            Debug.Log("=== 示例2: 使用 ScriptableObject 和 DBManager ===");

            // 初始化 DBManager（会自动初始化 SaveManager）
            DBManager.Instance.Initialize();

            // 方式1: 使用 DBManager 获取数据实例（推荐）
            var playerData = DBManager.Instance.Get<PlayerDataExample>();
            if (playerData != null)
            {
                playerData.Level = 10;
                playerData.PlayerName = "ExamplePlayer";
                playerData.Experience = 5000f;
                playerData.Gold = 1000;
                playerData.IsVip = true;

                // 保存数据（使用基类的 Save() 方法）
                bool saveResult = playerData.Save();
                Debug.Log($"ScriptableObject 保存结果: {saveResult}");

                // 重新获取数据验证
                var loadedSO = DBManager.Instance.Get<PlayerDataExample>();
                if (loadedSO != null)
                {
                    Debug.Log($"加载成功 - Level: {loadedSO.Level}, Name: {loadedSO.PlayerName}, " +
                             $"Exp: {loadedSO.Experience}, Gold: {loadedSO.Gold}, VIP: {loadedSO.IsVip}");
                }
            }
            else
            {
                Debug.LogWarning("PlayerDataExample 实例未找到，请确保已通过 DBSetting 扫描并初始化");
            }

            // 方式2: 使用静态 Instance 属性（如果已生成 Partial 类并创建了资源文件）
            // var playerData2 = PlayerDataExample.Instance;
            // if (playerData2 != null)
            // {
            //     playerData2.Level = 20;
            //     playerData2.Save();
            // }

            // 方式3: 创建 ScriptableObject 实例（如果未生成资源文件）
            // var playerData3 = ScriptableObject.CreateInstance<PlayerDataExample>();
            // playerData3.Level = 30;
            // PlayerDataExample.StorageType = StorageType.Json;
            // playerData3.Save();
        }

        /// <summary>
        /// 示例3: 异步操作
        /// </summary>
        private async void Example3_AsyncOperations()
        {
            Debug.Log("=== 示例4: 异步操作 ===");

            var testData = new SimpleData
            {
                Score = 200,
                PlayerName = "AsyncPlayer",
                Level = 10
            };

            // 异步保存
            string asyncKey = "AsyncData";
            bool saveResult = await SaveManager.Instance.SaveAsync(asyncKey, testData, StorageType.Json);
            Debug.Log($"异步保存结果: {saveResult}");

            // 异步加载
            var loadedData = await SaveManager.Instance.LoadAsync<SimpleData>(asyncKey, StorageType.Json);
            if (loadedData != null)
            {
                Debug.Log($"异步加载成功 - Score: {loadedData.Score}");
            }
        }

        /// <summary>
        /// 示例: 服务器同步
        /// </summary>
        [ContextMenu("测试服务器同步")]
        private async void TestServerSync()
        {
            Debug.Log("=== 测试服务器同步 ===");

            // 设置服务器同步提供者（需要实际的服务器地址）
            string serverUrl = "https://your-server.com/api";
            var serverProvider = new ServerSyncProvider(serverUrl);
            SaveManager.Instance.SetServerSyncProvider(serverProvider);

            // 同步到服务器
            bool uploadResult = await SaveManager.Instance.SyncToServer(testKey, testStorageType);
            Debug.Log($"上传到服务器结果: {uploadResult}");

            // 从服务器同步
            bool downloadResult = await SaveManager.Instance.SyncFromServer(testKey, testStorageType);
            Debug.Log($"从服务器下载结果: {downloadResult}");
        }

        /// <summary>
        /// 示例: 删除数据
        /// </summary>
        [ContextMenu("删除测试数据")]
        private void DeleteTestData()
        {
            bool deleted = SaveManager.Instance.Delete(testKey, testStorageType);
            Debug.Log($"删除数据结果: {deleted}");
        }

        /// <summary>
        /// 示例: 清除 PlayerPrefs 缓存数据
        /// </summary>
        [ContextMenu("测试清除 PlayerPrefs 缓存")]
        private void TestClearPlayerPrefsCache()
        {
            Debug.Log("=== 测试清除 PlayerPrefs 缓存数据 ===");

            // 初始化 DBManager
            DBManager.Instance.Initialize();
            
            // 获取使用 PlayerPrefs 存储的数据并修改
            var exampleData = DBManager.Instance.Get<ExampleData>();
            if (exampleData != null && ExampleData.StorageType == StorageType.PlayerPrefs)
            {
                exampleData.Number = 888;
                exampleData.Save();
                Debug.Log($"修改数据 - ExampleData.Number = {exampleData.Number} (已保存到 PlayerPrefs)");

                // 验证数据已保存
                string key = "SaveData_" + typeof(ExampleData).Name;
                bool exists = UnityEngine.PlayerPrefs.HasKey(key);
                Debug.Log($"PlayerPrefs 中是否存在 ExampleData: {exists}");

                // 清除 PlayerPrefs 缓存（只清除 SaveModule 数据）
                DBManager.Instance.ClearPlayerPrefsCache(onlySaveModuleData: true);
                Debug.Log("已清除 PlayerPrefs 缓存（只清除 SaveModule 数据）");

                // 验证数据已被清除
                bool existsAfter = UnityEngine.PlayerPrefs.HasKey(key);
                Debug.Log($"清除后 PlayerPrefs 中是否存在 ExampleData: {existsAfter} (应该为 false)");

                // 重新初始化并验证数据已重置
                DBManager.Instance.Initialize();
                var reloadedData = DBManager.Instance.Get<ExampleData>();
                if (reloadedData != null)
                {
                    Debug.Log($"重新加载后 - ExampleData.Number = {reloadedData.Number} (应该是默认值，因为 PlayerPrefs 数据已被清除)");
                }
            }
            else
            {
                Debug.LogWarning("ExampleData 不存在或不是使用 PlayerPrefs 存储，无法测试");
            }
        }

        /// <summary>
        /// 示例: 清除 Json 文件缓存数据
        /// </summary>
        [ContextMenu("测试清除 Json 文件缓存")]
        private void TestClearJsonCache()
        {
            Debug.Log("=== 测试清除 Json 文件缓存数据 ===");

            // 初始化 DBManager
            DBManager.Instance.Initialize();
            
            // 获取使用 Json 存储的数据并修改
            var playerData = DBManager.Instance.Get<PlayerDataExample>();
            if (playerData != null && PlayerDataExample.StorageType == StorageType.Json)
            {
                playerData.Level = 777;
                playerData.PlayerName = "JsonCache测试";
                playerData.Save();
                Debug.Log($"修改数据 - Level: {playerData.Level}, Name: {playerData.PlayerName} (已保存到 Json)");

                // 验证文件已保存
                string saveDirectory = System.IO.Path.Combine(Application.persistentDataPath, "SaveData");
                string fileName = typeof(PlayerDataExample).Name + ".json";
                string filePath = System.IO.Path.Combine(saveDirectory, fileName);
                bool exists = System.IO.File.Exists(filePath);
                Debug.Log($"Json 文件中是否存在 PlayerDataExample: {exists}");

                // 清除 Json 文件缓存（只清除 SaveModule 数据）
                DBManager.Instance.ClearJsonCache(onlySaveModuleData: true);
                Debug.Log("已清除 Json 文件缓存（只清除 SaveModule 数据）");

                // 验证文件已被删除
                bool existsAfter = System.IO.File.Exists(filePath);
                Debug.Log($"清除后 Json 文件中是否存在 PlayerDataExample: {existsAfter} (应该为 false)");

                // 重新初始化并验证数据已重置
                DBManager.Instance.Initialize();
                var reloadedData = DBManager.Instance.Get<PlayerDataExample>();
                if (reloadedData != null)
                {
                    Debug.Log($"重新加载后 - Level: {reloadedData.Level}, Name: {reloadedData.PlayerName} (应该是默认值，因为 Json 文件已被删除)");
                }
            }
            else
            {
                Debug.LogWarning("PlayerDataExample 不存在或不是使用 Json 存储，无法测试");
            }
        }
    }

    /// <summary>
    /// 简单测试数据类
    /// </summary>
    [System.Serializable]
    public class SimpleData
    {
        public int Score { get; set; }
        public string PlayerName { get; set; }
        public int Level { get; set; }
    }
}


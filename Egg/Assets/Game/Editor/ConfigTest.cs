#if UNITY_EDITOR
using Game.ConfigModule;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 配置表测试工具（仅 Editor 模式）
    /// </summary>
    public class ConfigTest : EditorWindow
    {
        [MenuItem("Tools/Config/Test Config Tables")]
        public static void ShowWindow()
        {
            GetWindow<ConfigTest>("Config Test");
        }

        private Vector2 scrollPosition;
        private bool isInitialized = false;
        private string logText = "";

        private void OnEnable()
        {
            // 只在运行时（Play Mode）使用
            if (!Application.isPlaying)
            {
                return;
            }

            // 检查 ConfigManager 是否已初始化
            UpdateInitializedState();
        }

        private void UpdateInitializedState()
        {
            if (!Application.isPlaying)
            {
                isInitialized = false;
                return;
            }

            bool wasInitialized = isInitialized;
            isInitialized = ConfigManager.Instance != null && ConfigManager.Instance.Tables != null && ConfigManager.Instance.IsInitialized;
            
            if (isInitialized && !wasInitialized)
            {
                RefreshLog();
            }
        }

        private void OnGUI()
        {
            // 检查是否在运行时
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("请在 Play Mode 下使用此工具。", MessageType.Warning);
                if (GUILayout.Button("进入 Play Mode"))
                {
                    EditorApplication.isPlaying = true;
                }
                return;
            }

            // 更新初始化状态
            UpdateInitializedState();

            GUILayout.Label("Config Tables Test", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 初始化按钮
            if (!isInitialized)
            {
                if (GUILayout.Button("Initialize Config Manager", GUILayout.Height(30)))
                {
                    InitializeConfig();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Config Manager is initialized.", MessageType.Info);
                
                if (GUILayout.Button("Reload Config", GUILayout.Height(30)))
                {
                    ReloadConfig();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // 测试按钮
            if (isInitialized)
            {
                EditorGUILayout.LabelField("Test Actions:", EditorStyles.boldLabel);

                if (GUILayout.Button("Test GlobalConst Table"))
                {
                    TestGlobalConst();
                }

                if (GUILayout.Button("Test LevelConfig Table"))
                {
                    TestLevelConfig();
                }

                if (GUILayout.Button("Print All Tables Info"))
                {
                    PrintAllTablesInfo();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // 日志显示
            EditorGUILayout.LabelField("Log:", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
            EditorGUILayout.TextArea(logText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Clear Log"))
            {
                logText = "";
            }
        }

        private void InitializeConfig()
        {
            if (!Application.isPlaying)
            {
                AddLog("Error: Must be in Play Mode!");
                return;
            }

            if (ConfigManager.Instance == null)
            {
                AddLog("Error: ConfigManager instance is null! Please ensure ConfigManager is initialized in the scene.");
                return;
            }

            AddLog("Initializing Config Manager...");
            ConfigManager.Instance.InitializeAsync(() =>
            {
                isInitialized = ConfigManager.Instance.Tables != null && ConfigManager.Instance.IsInitialized;
                if (isInitialized)
                {
                    AddLog("Config Manager initialized successfully!");
                    RefreshLog();
                }
                else
                {
                    AddLog("Error: Failed to initialize Config Manager!");
                }
                Repaint(); // 刷新 UI
            });
        }

        private void ReloadConfig()
        {
            if (!Application.isPlaying)
            {
                AddLog("Error: Must be in Play Mode!");
                return;
            }

            if (ConfigManager.Instance == null)
            {
                AddLog("Error: ConfigManager instance is null! Please ensure ConfigManager is initialized in the scene.");
                return;
            }

            AddLog("Reloading Config Manager...");
            ConfigManager.Instance.ReloadAsync(() =>
            {
                isInitialized = ConfigManager.Instance.IsInitialized && ConfigManager.Instance.Tables != null;
                if (isInitialized)
                {
                    AddLog("Config Manager reloaded successfully!");
                    RefreshLog();
                }
                else
                {
                    AddLog("Error: Failed to reload Config Manager!");
                }
                Repaint(); // 刷新 UI
            });
        }

        private void TestGlobalConst()
        {
            if (ConfigManager.Instance == null || !ConfigManager.Instance.IsInitialized || ConfigManager.Instance.Tables == null)
            {
                AddLog("Error: Config tables not initialized!");
                return;
            }

            var globalConst = ConfigManager.Instance.Tables.TbGlobalConst;
            if (globalConst == null)
            {
                AddLog("Error: TbGlobalConst is null!");
                return;
            }

            AddLog("=== TbGlobalConst Table ===");
            AddLog($"DataMap Count: {globalConst.DataMap.Count}");
            AddLog($"DataList Count: {globalConst.DataList.Count}");

            if (globalConst.DataList.Count > 0)
            {
                AddLog("\nFirst Item:");
                var firstItem = globalConst.DataList[0];
                AddLog($"  - X1: {firstItem.X1}");
                AddLog($"  - X2: {firstItem.X2}");
                AddLog($"  - X3: {firstItem.X3}");
                AddLog($"  - X4: {firstItem.X4}");
                AddLog($"  - X5: {firstItem.X5}");
                AddLog($"  - X6: {firstItem.X6}");
                AddLog($"  - X7: [{string.Join(", ", firstItem.X7)}]");
            }

            AddLog("");
        }

        private void TestLevelConfig()
        {
            if (ConfigManager.Instance == null || !ConfigManager.Instance.IsInitialized || ConfigManager.Instance.Tables == null)
            {
                AddLog("Error: Config tables not initialized!");
                return;
            }

            var levelConfig = ConfigManager.Instance.Tables.TbLevelData;
            if (levelConfig == null)
            {
                AddLog("Error: TbLevelConfig is null!");
                return;
            }

            AddLog("=== TbLevelConfig Table ===");
            AddLog($"DataMap Count: {levelConfig.DataMap.Count}");
            AddLog($"DataList Count: {levelConfig.DataList.Count}");

            if (levelConfig.DataList.Count > 0)
            {
                AddLog("\nFirst 5 Items:");
                int count = Mathf.Min(5, levelConfig.DataList.Count);
                for (int i = 0; i < count; i++)
                {
                    var item = levelConfig.DataList[i];
                    AddLog($"  [{i}] Id: {item.Id}, Level: {item.Path}, LevelType: {item.LevelType}, LockType: {item.LockType}");
                }
            }

            // 测试通过 ID 获取
            if (levelConfig.DataMap.Count > 0)
            {
                var firstId = levelConfig.DataList[0].Id;
                var itemById = levelConfig.Get(firstId);
                if (itemById != null)
                {
                    AddLog($"\nGet by ID ({firstId}):");
                    AddLog($"  Id: {itemById.Id}, Level: {itemById.Path}, LevelType: {itemById.LevelType}, LockType: {itemById.LockType}");
                }
            }

            AddLog("");
        }

        private void PrintAllTablesInfo()
        {
            if (ConfigManager.Instance == null || !ConfigManager.Instance.IsInitialized || ConfigManager.Instance.Tables == null)
            {
                AddLog("Error: Config tables not initialized!");
                return;
            }

            var tables = ConfigManager.Instance.Tables;
            AddLog("=== All Tables Info ===");
            AddLog($"TbGlobalConst: {(tables.TbGlobalConst != null ? "Loaded" : "Null")}");
            AddLog($"TbLevelConfig: {(tables.TbLevelData != null ? "Loaded" : "Null")}");
            AddLog("");
        }

        private void RefreshLog()
        {
            if (ConfigManager.Instance != null && ConfigManager.Instance.IsInitialized && ConfigManager.Instance.Tables != null)
            {
                var tables = ConfigManager.Instance.Tables;
                AddLog("=== Config Manager Status ===");
                AddLog($"Initialized: {ConfigManager.Instance.IsInitialized}");
                AddLog($"Load Progress: {ConfigManager.Instance.LoadProgress * 100:F1}%");
                AddLog($"TbGlobalConst: {tables.TbGlobalConst?.DataList?.Count ?? 0} items");
                AddLog($"TbLevelConfig: {tables.TbLevelData?.DataList?.Count ?? 0} items");
                AddLog("");
            }
        }

        private void AddLog(string message)
        {
            logText += $"{message}\n";
            Debug.Log($"[ConfigTest] {message}");
        }
    }
}
#endif

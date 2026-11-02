using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bear.Logger
{
    /// <summary>
    /// 基础配置
    /// </summary>
    public class DebugSetting : ScriptableObject, IDebuger
    {
        public const string Name = "DebugSetting";
        #region Setup
    
#if UNITY_EDITOR
        [MenuItem("Tools/Debug/Setup", priority = 1)]
        public static void Setup()
        {
            var instance = ScriptableObject.CreateInstance<DebugSetting>();
            AssetDatabase.CreateAsset(instance, "Assets/Resources/DebugSetting.asset");
            AssetDatabase.SaveAssets();
            RefreshInstance();
            instance.Log("Debug mode setup finished");
        }
#endif
    
        #endregion

        /// <summary>
        /// 设置
        /// </summary>
        public enum DebugMode
        {
            None,
            SpecialPriority,
            RangePriority
        }

        public DebugMode Mode = DebugMode.None;
        public int SpecialPriority = -1;
        public Vector2Int RangePriority = new Vector2Int(-1, -1);

        private static DebugSetting _instance;
        public static DebugSetting Instance
        {
            get
            {
                if (_instance == null)
                    RefreshInstance();
                return _instance;
            }
        }

        private static void RefreshInstance()
        {
            _instance = Resources.Load<DebugSetting>(Name);
        }

        // 存在性能损耗
        public bool isShowTime = true;
        public enum SaveMode
        {
            None,
            Local_All,
            Local_Error_Only
        }

        /// <summary>
        /// 是否保存到本地
        /// </summary>
        public SaveMode Save = SaveMode.None;

        private string DebugLogFilePath = "Assets/Debug.Log";

        private DateTime Today = DateTime.Today;
        private string FileNameWithDate = "";
        private string CompletLocalFilePath = "";
        public string SaveFilePath => GetFilePathWithDate();
        
        /// <summary>
        /// 开启一天算一次
        /// </summary>
        /// <returns></returns>
        private string GetFilePathWithDate()
        {
            if (Today != DateTime.Today || string.IsNullOrEmpty(FileNameWithDate))
            {
                var fileName = Path.GetFileNameWithoutExtension(DebugLogFilePath);
                FileNameWithDate = string.Format("{0}_{1}", fileName, DateTime.Today.ToString("yy-MM-dd"));
                var path = DebugLogFilePath.Replace(fileName, FileNameWithDate);
                
                // 跨平台路径处理：使用 persistentDataPath 而不是 dataPath（移动端更安全）
                if (path.StartsWith("Assets"))
                {
#if UNITY_EDITOR
                    // 编辑器模式下使用 Assets 目录
                    CompletLocalFilePath = path.Replace("Assets", Application.dataPath);
#else
                    // 运行时使用 persistentDataPath（移动端支持）
                    string directory = Path.Combine(Application.persistentDataPath, "DebugLogs");
                    if (!Directory.Exists(directory))
                    {
                        try
                        {
                            Directory.CreateDirectory(directory);
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogError($"Failed to create debug log directory: {ex.Message}");
                            // 如果无法创建目录，回退到 dataPath
                            CompletLocalFilePath = Path.Combine(Application.dataPath, FileNameWithDate + ".log");
                            return CompletLocalFilePath;
                        }
                    }
                    CompletLocalFilePath = Path.Combine(directory, FileNameWithDate + ".log");
#endif
                }
                else
                {
                    // 如果路径不是 Assets 开头，直接使用
                    CompletLocalFilePath = path;
                }
            }
         
            return CompletLocalFilePath;
        }
    }   
}

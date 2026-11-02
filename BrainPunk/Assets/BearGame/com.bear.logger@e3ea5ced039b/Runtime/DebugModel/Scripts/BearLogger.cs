using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Bear.Logger
{
    public interface IDebuger
    {
        
    }
    
    /// <summary>
    /// 依赖 Define 控制开关
    /// </summary>
    
    public static class BearLogger
    {
        public enum Color
        {
            green,
            black,
            blue,
            cyan,
            white,
            yellow,
            red
        }

        public enum LogState
        {
            Log, 
            Warning,
            Error
        }

        /// <summary>
        /// 直接输出彩色日志（不需要传 this / obj）
        /// </summary>
        [Conditional("DEBUG_MODE")]
        public static void LogColor(string msg, Color color)
        {
            var log = string.Format("<color={0}>{1}</color>", color.ToString(), msg);
            Debug.Log(log);
            LogSaveToLocal(log);
        }

        [Conditional("DEBUG_MODE")]
        public static void LogColor(string msg, Color color, object obj)
        {
            var log = string.Format("<color={0}>[{1}]-{2}</color>", color.ToString(), obj, msg);
            Debug.Log(log);
            LogSaveToLocal(log);
        }
        
        [Conditional("DEBUG_MODE")]
        public static void LogColor(this IDebuger obj, string msg, Color color)
        {
            var log = string.Format("<color={0}>[{1}]-{2}</color>", color.ToString(), obj, msg);
            Debug.Log(log);
            LogSaveToLocal(log);
        }
        
        [Conditional("DEBUG_MODE")]
        public static void Log(string msg, object obj, int priority = -1)
        {
            if (priority >= 0)
            {
                if (!LogCouldShow(priority))
                    return;
            }

            string log = string.Format("[{0}]-{1}", obj, msg);
            Debug.Log(log);
            LogSaveToLocal(log);
        }

        /// <summary>
        /// 直接输出普通日志（不需要传 this / obj）
        /// </summary>
        [Conditional("DEBUG_MODE")]
        public static void Log(string msg, int priority = -1)
        {
            if (priority >= 0)
            {
                if (!LogCouldShow(priority))
                    return;
            }

            string log = msg;
            Debug.Log(log);
            LogSaveToLocal(log);
        }
        
        [Conditional("DEBUG_MODE")]
        public static void Log(this IDebuger obj, string msg, int priority = -1)
        {
            if (priority >= 0)
            {
                if (!LogCouldShow(priority))
                    return;
            }
            
            string log = string.Format("[{0}]-{1}", obj, msg);
            Debug.Log(log);
            LogSaveToLocal(log);
        }
        
        [Conditional("DEBUG_MODE")]
        public static void LogWarning(string msg, object obj)
        {
            string log = string.Format("[{0}]-{1}", obj, msg);
            Debug.LogWarning(log);
            LogSaveToLocal(log, LogState.Warning);
        }

        /// <summary>
        /// 直接输出警告日志（不需要传 this / obj）
        /// </summary>
        [Conditional("DEBUG_MODE")]
        public static void LogWarning(string msg)
        {
            string log = msg;
            Debug.LogWarning(log);
            LogSaveToLocal(log, LogState.Warning);
        }
        
        [Conditional("DEBUG_MODE")]
        public static void LogWarning(this IDebuger obj, string msg)
        {
            string log = string.Format("[{0}]-{1}", obj, msg);
            Debug.LogWarning(log);
            LogSaveToLocal(log, LogState.Warning);
        }
        
        [Conditional("DEBUG_MODE")]
        public static void LogError(string msg, object obj)
        {
            string log = string.Format("[{0}]-{1}", obj, msg);
            Debug.LogError(log);
            LogSaveToLocal(log, LogState.Error);
        }

        /// <summary>
        /// 直接输出错误日志（不需要传 this / obj）
        /// </summary>
        [Conditional("DEBUG_MODE")]
        public static void LogError(string msg)
        {
            string log = msg;
            Debug.LogError(log);
            LogSaveToLocal(log, LogState.Error);
        }
        
        [Conditional("DEBUG_MODE")]
        public static void LogError(this IDebuger obj, string msg)
        {
            string log = string.Format("[{0}]-{1}", obj, msg);
            Debug.LogError(log);
            LogSaveToLocal(log, LogState.Error);
        }

        private static bool LogCouldShow(int priority)
        {
            var setting = DebugSetting.Instance;
            if (setting == null)
                return true;
            if (setting.Mode == DebugSetting.DebugMode.SpecialPriority)
                return setting.SpecialPriority == priority;
            else if (setting.Mode == DebugSetting.DebugMode.RangePriority)
                return priority >= setting.RangePriority.x && priority <= setting.RangePriority.y;
            else
                return true;
        }

        /// <summary>
        /// 存储到本地
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="state"></param>
        public static void LogSaveToLocal(string msg, LogState state = LogState.Log)
        {
            var setting = DebugSetting.Instance;
            if (setting == null || 
                string.IsNullOrEmpty(setting.SaveFilePath) ||
                setting.Save == DebugSetting.SaveMode.None ||  
                (state == LogState.Error && 
                 setting.Save == DebugSetting.SaveMode.Local_Error_Only))
                return;

            // 检查是否在运行时（文件操作需要在运行时执行）
            if (!Application.isPlaying)
            {
                return;
            }

            var path = setting.SaveFilePath;
            
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"BearLogger: Failed to create directory {directory}: {ex.Message}");
                        return;
                    }
                }

                // 创建文件（如果不存在）
                if (!File.Exists(path))
                {
                    try
                    {
                        var f = File.Create(path);
                        f.Close();
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Debug.LogError($"BearLogger: Access denied when creating log file {path}. Platform may not support file creation: {ex.Message}");
                        return;
                    }
                    catch (IOException ex)
                    {
                        Debug.LogError($"BearLogger: IO error when creating log file {path}: {ex.Message}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"BearLogger: Failed to create log file {path}: {ex.Message}");
                        return;
                    }
                }

                // 写入日志
                using (FileStream files = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    using (StreamWriter writer = new StreamWriter(files, Encoding.UTF8))
                    {
                        if (setting.isShowTime)
                        {
                            // Write to local file as txt
                            writer.WriteLine("[{0}][{2}] {1}", state.ToString(), msg, DateTime.Now.ToString("yy-MMM-dd hh:mm:ss"));
                        }
                        else
                        {
                            writer.WriteLine("[{0}] {1}", state.ToString(), msg);
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogError($"BearLogger: Access denied when writing to log file {path}. Platform may not support file writing: {ex.Message}");
            }
            catch (IOException ex)
            {
                Debug.LogError($"BearLogger: IO error when writing to log file {path}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"BearLogger: Failed to save log to file {path}: {ex.Message}");
            }
        }

        #region Menu Command

#if UNITY_EDITOR

        [MenuItem("Tools/Debug/Open")]
        public static void LogOpen()
        {
            LogAddScriptingDefineSymbol(BuildTargetGroup.Standalone,"DEBUG_MODE");
            LogAddScriptingDefineSymbol(BuildTargetGroup.WebGL, "DEBUG_MODE");
            LogAddScriptingDefineSymbol(BuildTargetGroup.Android,"DEBUG_MODE");
            LogAddScriptingDefineSymbol(BuildTargetGroup.iOS,"DEBUG_MODE");
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Debug/Close")]
        public static void LogClose()
        {
            LogRemoveScriptingDefineSymbol(BuildTargetGroup.Standalone,"DEBUG_MODE");
            LogRemoveScriptingDefineSymbol(BuildTargetGroup.WebGL, "DEBUG_MODE");
            LogRemoveScriptingDefineSymbol(BuildTargetGroup.Android,"DEBUG_MODE");
            LogRemoveScriptingDefineSymbol(BuildTargetGroup.iOS,"DEBUG_MODE");
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Debug/Open Current Platform Only")]
        public static void LogOpenCurrent()
        {
            BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            
            // 如果无法确定 BuildTargetGroup，尝试根据 BuildTarget 推断
            if (targetGroup == BuildTargetGroup.Unknown)
            {
                switch (activeTarget)
                {
                    case BuildTarget.Android:
                        targetGroup = BuildTargetGroup.Android;
                        break;
                    case BuildTarget.iOS:
                        targetGroup = BuildTargetGroup.iOS;
                        break;
                    case BuildTarget.WebGL:
                        targetGroup = BuildTargetGroup.WebGL;
                        break;
                    case BuildTarget.StandaloneWindows:
                    case BuildTarget.StandaloneWindows64:
                    case BuildTarget.StandaloneLinux64:
                    case BuildTarget.StandaloneOSX:
                    default:
                        targetGroup = BuildTargetGroup.Standalone;
                        break;
                }
            }
            
            LogAddScriptingDefineSymbol(targetGroup, "DEBUG_MODE");
            AssetDatabase.Refresh();
        }
        
        [MenuItem("Tools/Debug/Close Current Platform Only")]
        public static void LogCloseCurrent()
        {
            BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            
            // 如果无法确定 BuildTargetGroup，尝试根据 BuildTarget 推断
            if (targetGroup == BuildTargetGroup.Unknown)
            {
                switch (activeTarget)
                {
                    case BuildTarget.Android:
                        targetGroup = BuildTargetGroup.Android;
                        break;
                    case BuildTarget.iOS:
                        targetGroup = BuildTargetGroup.iOS;
                        break;
                    case BuildTarget.WebGL:
                        targetGroup = BuildTargetGroup.WebGL;
                        break;
                    case BuildTarget.StandaloneWindows:
                    case BuildTarget.StandaloneWindows64:
                    case BuildTarget.StandaloneLinux64:
                    case BuildTarget.StandaloneOSX:
                    default:
                        targetGroup = BuildTargetGroup.Standalone;
                        break;
                }
            }
            
            LogRemoveScriptingDefineSymbol(targetGroup, "DEBUG_MODE");
            AssetDatabase.Refresh();
        }

        private static void LogAddScriptingDefineSymbol(BuildTargetGroup targetGroup, string symbol)
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            var index = symbols.IndexOf(symbol, StringComparison.Ordinal);
            if (index >= 0)
                return;
            symbols += ";" + symbol;
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, symbols);
        }
        
        private static void LogRemoveScriptingDefineSymbol(BuildTargetGroup targetGroup, string symbol)
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            var index = symbols.IndexOf(symbol, StringComparison.Ordinal);
            if (index < 0)
                return;
            symbols = symbols.Remove(index, symbol.Length);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, symbols);
        }

#endif
        
#endregion 
    }

}

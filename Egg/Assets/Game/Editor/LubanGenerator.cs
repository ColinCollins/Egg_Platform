#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Luban 配置生成工具
    /// 用于调用 Luban 的 gen.bat 生成配置代码和数据
    /// </summary>
    public static class LubanGenerator
    {
        private const string MENU_PATH = "Tools/Luban/Generate Config";
        private const string GEN_BAT_NAME = "gen.bat";
        
        [MenuItem(MENU_PATH, false, 1)]
        public static void GenerateConfig()
        {
            string genBatPath = GetGenBatPath();
            
            if (string.IsNullOrEmpty(genBatPath))
            {
                EditorUtility.DisplayDialog("Error", 
                    $"找不到 {GEN_BAT_NAME} 文件！\n\n请确保文件存在于：\nLuban/Configs/gen.bat", 
                    "OK");
                return;
            }

            if (!File.Exists(genBatPath))
            {
                EditorUtility.DisplayDialog("Error", 
                    $"文件不存在：\n{genBatPath}", 
                    "OK");
                return;
            }

            // 确认生成
            if (!EditorUtility.DisplayDialog("Generate Config", 
                $"即将执行配置生成：\n\n{genBatPath}\n\n这可能会覆盖现有的配置代码文件。", 
                "确定", "取消"))
            {
                return;
            }

            ExecuteGenBat(genBatPath);
        }

        /// <summary>
        /// 获取 gen.bat 文件路径
        /// </summary>
        private static string GetGenBatPath()
        {
            // 从 Assets 目录向上查找 Luban/Configs/gen.bat
            string assetsPath = Application.dataPath.Replace('\\', '/');
            string projectRoot = Directory.GetParent(assetsPath)?.FullName.Replace('\\', '/');
            
            if (string.IsNullOrEmpty(projectRoot))
            {
                return null;
            }

            string genBatPath = Path.Combine(projectRoot, "Luban", "Configs", GEN_BAT_NAME).Replace('\\', '/');
            return genBatPath;
        }

        /// <summary>
        /// 执行 gen.bat 文件
        /// </summary>
        private static void ExecuteGenBat(string batPath)
        {
            string workingDirectory = Path.GetDirectoryName(batPath).Replace('\\', '/');
            
            UnityEngine.Debug.Log($"[LubanGenerator] 开始执行配置生成...");
            UnityEngine.Debug.Log($"[LubanGenerator] 工作目录: {workingDirectory}");
            UnityEngine.Debug.Log($"[LubanGenerator] 批处理文件: {batPath}");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = batPath,
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
                CreateNoWindow = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            try
            {
                Process process = Process.Start(startInfo);
                
                if (process != null)
                {
                    UnityEngine.Debug.Log($"[LubanGenerator] 进程已启动 (PID: {process.Id})");
                    
                    // 等待进程完成（可选，如果需要同步等待）
                    // process.WaitForExit();
                    
                    // 刷新资源数据库
                    EditorApplication.delayCall += () =>
                    {
                        AssetDatabase.Refresh();
                        UnityEngine.Debug.Log("[LubanGenerator] 配置生成完成，已刷新资源数据库。");
                    };
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "无法启动生成进程！", "OK");
                }
            }
            catch (System.Exception e)
            {
                string errorMsg = $"执行配置生成时发生错误：\n\n{e.Message}";
                UnityEngine.Debug.LogError($"[LubanGenerator] {errorMsg}");
                EditorUtility.DisplayDialog("Error", errorMsg, "OK");
            }
        }

        /// <summary>
        /// 验证 gen.bat 文件是否存在
        /// </summary>
        [MenuItem(MENU_PATH + " (Validate)", false, 2)]
        public static void ValidateGenBat()
        {
            string genBatPath = GetGenBatPath();
            
            if (string.IsNullOrEmpty(genBatPath))
            {
                EditorUtility.DisplayDialog("Validation", 
                    $"❌ 找不到 {GEN_BAT_NAME} 文件！", 
                    "OK");
                return;
            }

            if (File.Exists(genBatPath))
            {
                EditorUtility.DisplayDialog("Validation", 
                    $"✅ 找到 gen.bat 文件：\n\n{genBatPath}", 
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Validation", 
                    $"❌ 文件不存在：\n\n{genBatPath}", 
                    "OK");
            }
        }
    }
}
#endif

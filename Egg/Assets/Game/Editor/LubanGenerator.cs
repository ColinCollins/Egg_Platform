#if UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        private const string TABLES_CS_PATH = "Assets/Configs/Code/Tables.cs";
        private const string CONFIG_FILE_NAMES_PATH = "Assets/Game/Scripts/ConfigFileNames.cs";
        
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
                    
                    // 等待进程完成后更新配置文件名
                    EditorApplication.delayCall += () =>
                    {
                        // 延迟执行，确保文件写入完成
                        EditorApplication.delayCall += () =>
                        {
                            AssetDatabase.Refresh();
                            UpdateConfigFileNames();
                            UnityEngine.Debug.Log("[LubanGenerator] 配置生成完成，已刷新资源数据库并更新配置文件名。");
                        };
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

        /// <summary>
        /// 从 Tables.cs 中提取配置文件名
        /// </summary>
        private static List<string> ExtractConfigFileNamesFromTables()
        {
            List<string> fileNames = new List<string>();
            
            string tablesPath = TABLES_CS_PATH.Replace('/', Path.DirectorySeparatorChar);
            if (!File.Exists(tablesPath))
            {
                UnityEngine.Debug.LogWarning($"[LubanGenerator] Tables.cs 文件不存在: {tablesPath}");
                return fileNames;
            }

            try
            {
                string content = File.ReadAllText(tablesPath);
                
                // 使用正则表达式匹配 loader("filename") 模式
                Regex regex = new Regex(@"loader\s*\(\s*""([^""]+)""\s*\)", RegexOptions.Multiline);
                MatchCollection matches = regex.Matches(content);
                
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        string fileName = match.Groups[1].Value;
                        if (!string.IsNullOrEmpty(fileName) && !fileNames.Contains(fileName))
                        {
                            fileNames.Add(fileName);
                        }
                    }
                }
                
                UnityEngine.Debug.Log($"[LubanGenerator] 从 Tables.cs 中提取到 {fileNames.Count} 个配置文件名");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[LubanGenerator] 解析 Tables.cs 时发生错误: {e.Message}");
            }
            
            return fileNames;
        }

        /// <summary>
        /// 更新 ConfigFileNames.cs 文件
        /// </summary>
        private static void UpdateConfigFileNames()
        {
            List<string> configFileNames = ExtractConfigFileNamesFromTables();
            
            if (configFileNames.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[LubanGenerator] 未找到配置文件名，跳过更新。");
                return;
            }

            string configFileNamesPath = CONFIG_FILE_NAMES_PATH.Replace('/', Path.DirectorySeparatorChar);
            if (!File.Exists(configFileNamesPath))
            {
                UnityEngine.Debug.LogWarning($"[LubanGenerator] ConfigFileNames.cs 文件不存在: {configFileNamesPath}");
                return;
            }

            try
            {
                string content = File.ReadAllText(configFileNamesPath);
                string originalContent = content;

                // 构建新的配置文件名数组代码
                string newArrayCode = "return new string[]\n            {\n";
                foreach (string fileName in configFileNames)
                {
                    newArrayCode += $"                \"{fileName}\",\n";
                }
                newArrayCode = newArrayCode.TrimEnd(',', '\n') + "\n            };";

                // 使用正则表达式匹配 GetFileNames 方法中的 return 语句
                Regex regex = new Regex(
                    @"(public\s+static\s+string\[\]\s+GetFileNames\(\)\s*\{[^}]*return\s+new\s+string\[\]\s*\{[^}]*\};)",
                    RegexOptions.Singleline | RegexOptions.Multiline
                );

                Match match = regex.Match(content);
                if (match.Success)
                {
                    // 找到方法体，替换 return 语句部分
                    string methodBody = match.Groups[1].Value;
                    Regex returnRegex = new Regex(@"return\s+new\s+string\[\]\s*\{[^}]*\};", RegexOptions.Singleline);
                    string newMethodBody = returnRegex.Replace(methodBody, newArrayCode);
                    content = content.Replace(methodBody, newMethodBody);
                }
                else
                {
                    // 如果没找到完整方法，尝试只替换 return 语句
                    Regex returnRegex = new Regex(@"return\s+new\s+string\[\]\s*\{[^}]*\};", RegexOptions.Singleline);
                    content = returnRegex.Replace(content, newArrayCode);
                }

                // 如果内容有变化，写入文件
                if (content != originalContent)
                {
                    File.WriteAllText(configFileNamesPath, content);
                    AssetDatabase.ImportAsset(CONFIG_FILE_NAMES_PATH);
                    UnityEngine.Debug.Log($"[LubanGenerator] 已更新 ConfigFileNames.cs 中的配置文件名列表:\n{string.Join("\n", configFileNames.Select(f => $"  - {f}"))}");
                }
                else
                {
                    UnityEngine.Debug.Log("[LubanGenerator] ConfigFileNames.cs 中的配置文件名列表无需更新。");
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[LubanGenerator] 更新 ConfigFileNames.cs 时发生错误: {e.Message}");
            }
        }

        /// <summary>
        /// 手动更新配置文件名（用于测试）
        /// </summary>
        [MenuItem(MENU_PATH + " (Update File Names)", false, 3)]
        public static void ManualUpdateConfigFileNames()
        {
            UpdateConfigFileNames();
            EditorUtility.DisplayDialog("Update Complete", "配置文件名已更新！", "OK");
        }
    }
}
#endif

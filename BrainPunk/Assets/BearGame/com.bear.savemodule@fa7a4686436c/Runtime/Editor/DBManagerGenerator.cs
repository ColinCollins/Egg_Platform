using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Bear.SaveModule.Editor
{
    /// <summary>
    /// DBManager 代码生成器
    /// 根据 DBSetting 的扫描结果，生成静态数据访问代码
    /// </summary>
    public class DBManagerGenerator
    {
        private const string GENERATED_FILE_NAME = "DBManager_Generated.cs";
        private const string GENERATED_FILE_PATH = "Runtime/Core/DBManager_Generated.cs";

        /// <summary>
        /// 根据 DBSetting 生成代码
        /// </summary>
        [MenuItem("Tools/Save Module/Generate DBManager Code", false, 5)]
        public static void GenerateDBManagerCode()
        {
            // 查找 DBSetting
            DBSetting dbSetting = FindDBSetting();
            if (dbSetting == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "DBSetting not found. Please create a DBSetting asset first and scan data classes.", 
                    "OK");
                return;
            }

            // 如果数据类列表为空，先扫描
            if (dbSetting.DataClasses == null || dbSetting.DataClasses.Count == 0)
            {
                dbSetting.ScanDataClasses(GetScriptFilePath);
                EditorUtility.SetDirty(dbSetting);
                AssetDatabase.SaveAssets();
            }

            // 使用公共方法生成并保存
            GenerateAndSave(dbSetting, true);
        }

        /// <summary>
        /// 根据 DBSetting 生成代码并保存到文件
        /// </summary>
        /// <param name="dbSetting">数据库设置</param>
        /// <param name="showDialog">是否显示对话框</param>
        /// <returns>是否成功生成</returns>
        public static bool GenerateAndSave(DBSetting dbSetting, bool showDialog = false)
        {
            if (dbSetting == null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Error", 
                        "DBSetting is null.", 
                        "OK");
                }
                return false;
            }

            // 如果数据类列表为空，先扫描
            if (dbSetting.DataClasses == null || dbSetting.DataClasses.Count == 0)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Info", 
                        "No data classes found. Please scan data classes first.", 
                        "OK");
                }
                return false;
            }

            // 生成代码
            string generatedCode = GenerateCode(dbSetting);
            
            // 保存文件（使用 DBSetting 所在路径）
            string savePath = GetSavePath(dbSetting);
            if (string.IsNullOrEmpty(savePath))
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Error", "Cannot determine save path for generated file.", "OK");
                }
                return false;
            }

            // 确保目录存在
            string directory = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(savePath, generatedCode);
            AssetDatabase.Refresh();

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Success", 
                    $"DBManager code generated successfully!\n\nFile: {savePath}\n\nData classes: {dbSetting.DataClasses.Count}", 
                    "OK");
            }

            return true;
        }

        /// <summary>
        /// 查找 DBSetting 资源
        /// </summary>
        private static DBSetting FindDBSetting()
        {
            string[] guids = AssetDatabase.FindAssets("t:DBSetting");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<DBSetting>(path);
            }
            return null;
        }

        /// <summary>
        /// 获取脚本文件路径（用于编辑器扫描）
        /// </summary>
        private static string GetScriptFilePath(Type type)
        {
            MonoScript[] scripts = Resources.FindObjectsOfTypeAll<MonoScript>();
            foreach (MonoScript script in scripts)
            {
                if (script.GetClass() == type)
                {
                    return AssetDatabase.GetAssetPath(script);
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// 生成代码
        /// </summary>
        public static string GenerateCode(DBSetting dbSetting)
        {
            StringBuilder sb = new StringBuilder();
            
            // 文件头注释
            sb.AppendLine("// =========================================");
            sb.AppendLine("// 此文件由 DBManagerGenerator 自动生成");
            sb.AppendLine("// 请勿手动修改此文件");
            sb.AppendLine("// 如需重新生成，请使用菜单: Tools/Save Module/Generate DBManager Code");
            sb.AppendLine($"// 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("// =========================================");
            sb.AppendLine();
            
            // Using 语句
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using Bear.SaveModule;");
            sb.AppendLine();
            
            // 不包含命名空间，直接定义类
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// DBManager 生成的静态数据访问类");
            sb.AppendLine("/// 此文件由 DBManagerGenerator 自动生成");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public static partial class DB");
            sb.AppendLine("{");
            sb.AppendLine();
            
            // 为每个数据类生成静态属性
            var validClasses = dbSetting.DataClasses.Where(c => c.isValid).OrderBy(c => c.className);
            
            foreach (var dataClass in validClasses)
            {
                string className = dataClass.className;
                string propertyName = className; // 使用类名作为属性名
                
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// 获取 {className} 数据实例");
                sb.AppendLine("    /// </summary>");
                sb.AppendLine($"    public static {className} {propertyName}");
                sb.AppendLine("    {");
                sb.AppendLine("        get");
                sb.AppendLine("        {");
                sb.AppendLine($"            return DBManager.Instance.Get<{className}>();");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
            }
            
            // 生成保存方法
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 保存指定类型的数据");
            sb.AppendLine("    /// </summary>");
            foreach (var dataClass in validClasses)
            {
                string className = dataClass.className;
                sb.AppendLine($"    public static bool Save{className}()");
                sb.AppendLine("    {");
                sb.AppendLine($"        return DBManager.Instance.Save<{className}>();");
                sb.AppendLine("    }");
                sb.AppendLine();
            }
            
            sb.AppendLine("}");
            
            return sb.ToString();
        }

        /// <summary>
        /// 获取保存路径（确保在主工程的 Assets 目录下，不在 UPM 包内）
        /// </summary>
        /// <param name="dbSetting">数据库设置</param>
        /// <returns>保存路径</returns>
        public static string GetSavePath(DBSetting dbSetting)
        {
            if (dbSetting == null)
            {
                return null;
            }

            // 获取 DBSetting 资源文件路径
            string dbSettingPath = AssetDatabase.GetAssetPath(dbSetting);
            if (string.IsNullOrEmpty(dbSettingPath))
            {
                // 如果无法获取路径，尝试查找
                string[] guids = AssetDatabase.FindAssets("t:DBSetting");
                if (guids.Length > 0)
                {
                    dbSettingPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                }
                else
                {
                    return null;
                }
            }

            // 检查路径是否在 UPM 包内（Packages/ 目录）
            if (dbSettingPath.StartsWith("Packages/"))
            {
                // 如果在包内，生成到主工程的 Assets/Generated 目录
                string generatedDir = "Assets/Generated";
                if (!Directory.Exists(generatedDir))
                {
                    // 创建目录（如果不存在）
                    string fullGeneratedDir = Path.Combine(Application.dataPath, "Generated");
                    if (!Directory.Exists(fullGeneratedDir))
                    {
                        Directory.CreateDirectory(fullGeneratedDir);
                        AssetDatabase.Refresh();
                    }
                }
                string fullPath = Path.Combine(generatedDir, GENERATED_FILE_NAME).Replace('\\', '/');
                return fullPath;
            }

            // 如果不在包内，检查是否在 Assets 目录下
            if (dbSettingPath.StartsWith("Assets/"))
            {
                // 获取 DBSetting 所在目录
                string directory = Path.GetDirectoryName(dbSettingPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    // 在同目录下生成文件
                    string fullPath = Path.Combine(directory, GENERATED_FILE_NAME).Replace('\\', '/');
                    return fullPath;
                }
            }

            // 如果路径不在 Assets 或 Packages 下，使用默认的 Assets/Generated 目录
            string defaultDir = "Assets/Generated";
            string defaultFullDir = Path.Combine(Application.dataPath, "Generated");
            if (!Directory.Exists(defaultFullDir))
            {
                Directory.CreateDirectory(defaultFullDir);
                AssetDatabase.Refresh();
            }
            string defaultPath = Path.Combine(defaultDir, GENERATED_FILE_NAME).Replace('\\', '/');
            return defaultPath;
        }
        
        /// <summary>
        /// 获取保存路径（兼容旧版本，使用默认路径）
        /// </summary>
        [System.Obsolete("Use GetSavePath(DBSetting) instead")]
        public static string GetSavePath()
        {
            // 查找 SaveModule 模块路径
            string[] guids = AssetDatabase.FindAssets("DBManager t:Script");
            if (guids.Length > 0)
            {
                string dbManagerPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                string directory = Path.GetDirectoryName(dbManagerPath);
                string fullPath = Path.Combine(directory, GENERATED_FILE_NAME).Replace('\\', '/');
                return fullPath;
            }
            
            // 如果找不到，使用默认路径
            string defaultPath = Path.Combine("Assets/Modules/SaveModule", GENERATED_FILE_PATH).Replace('\\', '/');
            if (defaultPath.StartsWith("Assets/"))
            {
                string fullPath = Path.Combine(Application.dataPath, "..", defaultPath).Replace('\\', '/');
                // 标准化路径
                fullPath = Path.GetFullPath(fullPath).Replace('\\', '/');
                return fullPath;
            }
            
            return null;
        }
    }
}


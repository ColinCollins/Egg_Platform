using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Bear.SaveModule
{
    /// <summary>
    /// 数据库设置 - 统一管理所有数据类的存储方式
    /// </summary>
    // [CreateAssetMenu(fileName = "DBSetting", menuName = "Save Data/DB Setting")]
    public class DBSetting : ScriptableObject
    {
        [SerializeField]
        private List<DataClassInfo> dataClasses = new List<DataClassInfo>();

        /// <summary>
        /// 数据类信息列表
        /// </summary>
        public List<DataClassInfo> DataClasses
        {
            get => dataClasses;
            set => dataClasses = value;
        }

        /// <summary>
        /// 扫描所有继承自 BaseSaveDataSO 的数据类
        /// </summary>
        /// <param name="filePathResolver">文件路径解析器（编辑器模式下使用，可为 null）</param>
        /// <returns>扫描到的数据类列表</returns>
        public List<DataClassInfo> ScanDataClasses(Func<Type, string> filePathResolver = null)
        {
            List<DataClassInfo> dataClasses = new List<DataClassInfo>();

            // 获取 BaseSaveDataSO 类型（用于检查继承关系）
            Type baseSaveDataSOType = typeof(BaseSaveDataSO);

            // 扫描所有程序集中的类型
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (Assembly assembly in assemblies)
            {
                try
                {
                    Type[] types = assembly.GetTypes();

                    foreach (Type type in types)
                    {
                        // 检查是否是类且不是抽象类
                        if (!type.IsClass || type.IsAbstract)
                        {
                            continue;
                        }

                        // 检查是否继承自 BaseSaveDataSO
                        if (!baseSaveDataSOType.IsAssignableFrom(type) || type == baseSaveDataSOType)
                        {
                            continue;
                        }

                        // 获取文件路径
                        string filePath = null;
                        if (filePathResolver != null)
                        {
                            filePath = filePathResolver(type);
                        }

                        // 查找静态 StorageType 字段
                        StorageType storageType = StorageType.Auto;
                        bool hasStaticStorageType = false;

                        FieldInfo storageTypeField = type.GetField("StorageType",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                        if (storageTypeField != null && storageTypeField.FieldType == typeof(StorageType))
                        {
                            storageType = (StorageType)storageTypeField.GetValue(null);
                            hasStaticStorageType = true;
                        }

                        dataClasses.Add(new DataClassInfo(
                            type.Name,
                            filePath ?? string.Empty,
                            storageType,
                            hasStaticStorageType));
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // 忽略无法加载的类型
                    Debug.LogWarning($"Failed to load types from assembly {assembly.FullName}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error scanning assembly {assembly.FullName}: {ex.Message}");
                }
            }

            this.dataClasses = dataClasses.OrderBy(c => c.className).ToList();
            return this.dataClasses;
        }

        /// <summary>
        /// 数据类信息
        /// </summary>
        [System.Serializable]
        public class DataClassInfo
        {
            public string className;
            public string filePath;
            public StorageType storageType;
            public bool isValid;

            public DataClassInfo(string className, string filePath, StorageType storageType, bool isValid)
            {
                this.className = className;
                this.filePath = filePath;
                this.storageType = storageType;
                this.isValid = isValid;
            }
        }
    }
}


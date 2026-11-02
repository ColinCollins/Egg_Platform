using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Bear.SaveModule
{
    /// <summary>
    /// 数据基类扩展方法
    /// 提供 ScriptableObject 的扩展方法，用于保存数据
    /// </summary>
    public static class BaseSaveDataSOExtensions
    {
        /// <summary>
        /// 获取存储类型（从静态字段获取）
        /// </summary>
        public static StorageType GetStorageType(this System.Type type)
        {
            var storageTypeField = type.GetField("StorageType", 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Static);
            
            if (storageTypeField != null && storageTypeField.FieldType == typeof(StorageType))
            {
                return (StorageType)storageTypeField.GetValue(null);
            }
            
            return StorageType.Auto;
        }
        
        /// <summary>
        /// 保存数据到存储系统
        /// </summary>
        public static bool SaveData(this ScriptableObject data)
        {
            string key = data.GetType().Name;
            StorageType storageType = data.GetType().GetStorageType();
            return SaveManager.Instance.Save(key, data, storageType);
        }
        
        /// <summary>
        /// 异步保存数据到存储系统
        /// </summary>
        public static async Task<bool> SaveDataAsync(this ScriptableObject data)
        {
            string key = data.GetType().Name;
            StorageType storageType = data.GetType().GetStorageType();
            return await SaveManager.Instance.SaveAsync(key, data, storageType);
        }
    }
    
    /// <summary>
    /// ScriptableObject 基类
    /// 数据类可以继承此类，自动获得保存功能
    /// 注意：继承此类的数据类需要定义静态 StorageType 字段来指定存储方式
    /// </summary>
    public abstract class BaseSaveDataSO : ScriptableObject
    {
        /// <summary>
        /// 初始化数据（设置默认值）
        /// 子类应在 Partial 类中实现此方法，初始化所有字段的默认值
        /// </summary>
        public virtual void Init()
        {
            // 子类实现
        }
        
        /// <summary>
        /// 序列化为 Json 字符串
        /// </summary>
        public virtual string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
        
        /// <summary>
        /// 从 Json 字符串反序列化
        /// </summary>
        public virtual void FromJson(string json)
        {
            JsonConvert.PopulateObject(json, this);
        }
        
        /// <summary>
        /// 获取存储类型（从静态字段获取）
        /// </summary>
        protected virtual StorageType GetStorageType()
        {
            return GetType().GetStorageType();
        }
        
        /// <summary>
        /// 保存数据到存储系统
        /// </summary>
        /// <returns>保存是否成功</returns>
        public bool Save()
        {
            string key = GetType().Name;
            return SaveManager.Instance.Save(key, this, GetStorageType());
        }
        
        /// <summary>
        /// 异步保存数据到存储系统
        /// </summary>
        /// <returns>保存是否成功</returns>
        public async Task<bool> SaveAsync()
        {
            string key = GetType().Name;
            return await SaveManager.Instance.SaveAsync(key, this, GetStorageType());
        }
    }
}

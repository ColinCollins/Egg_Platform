using System;
using System.Threading.Tasks;

namespace Bear.SaveModule
{
    /// <summary>
    /// 存储提供者接口
    /// </summary>
    public interface ISaveProvider
    {
        /// <summary>
        /// 存储类型
        /// </summary>
        StorageType StorageType { get; }
        
        /// <summary>
        /// 保存数据
        /// </summary>
        bool Save(string key, string data);
        
        /// <summary>
        /// 异步保存数据
        /// </summary>
        Task<bool> SaveAsync(string key, string data);
        
        /// <summary>
        /// 读取数据
        /// </summary>
        string Load(string key);
        
        /// <summary>
        /// 异步读取数据
        /// </summary>
        Task<string> LoadAsync(string key);
        
        /// <summary>
        /// 检查数据是否存在
        /// </summary>
        bool HasKey(string key);
        
        /// <summary>
        /// 删除数据
        /// </summary>
        bool Delete(string key);
        
        /// <summary>
        /// 删除所有数据
        /// </summary>
        bool DeleteAll();
    }
}


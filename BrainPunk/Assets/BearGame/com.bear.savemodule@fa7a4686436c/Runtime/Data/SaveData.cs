using System;

namespace Bear.SaveModule
{
    /// <summary>
    /// 基础保存数据结构
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>
        /// 数据键名
        /// </summary>
        public string Key { get; set; }
        
        /// <summary>
        /// 数据内容（Json 字符串）
        /// </summary>
        public string Data { get; set; }
        
        /// <summary>
        /// 创建时间戳
        /// </summary>
        public long Timestamp { get; set; }
        
        public SaveData()
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        
        public SaveData(string key, string data) : this()
        {
            Key = key;
            Data = data;
        }
    }
}


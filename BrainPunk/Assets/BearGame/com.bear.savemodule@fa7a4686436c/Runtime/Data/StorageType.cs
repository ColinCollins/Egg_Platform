namespace Bear.SaveModule
{
    /// <summary>
    /// 数据存储方式枚举
    /// </summary>
    public enum StorageType
    {
        /// <summary>
        /// 使用 PlayerPrefs 存储
        /// </summary>
        PlayerPrefs,
        
        /// <summary>
        /// 使用 Json 文件存储
        /// </summary>
        Json,
        
        /// <summary>
        /// 自动选择（根据数据类型）
        /// </summary>
        Auto
    }
}


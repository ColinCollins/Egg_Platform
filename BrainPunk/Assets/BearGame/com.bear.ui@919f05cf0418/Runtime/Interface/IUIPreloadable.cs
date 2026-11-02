namespace Bear.UI
{
    /// <summary>
    /// UI 预加载接口（预留）
    /// 用于实现 UI 预加载和缓存机制
    /// </summary>
    public interface IUIPreloadable
    {
        /// <summary>
        /// 预加载 UI 资源
        /// </summary>
        void Preload();

        /// <summary>
        /// 是否已预加载
        /// </summary>
        bool IsPreloaded { get; }

        // TODO: 后续实现预加载和缓存逻辑
        // 示例：
        // - 资源预加载
        // - UI 实例缓存
        // - 资源释放管理
        // - 内存优化等
    }
}


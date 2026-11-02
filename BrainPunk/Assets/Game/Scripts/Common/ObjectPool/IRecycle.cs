namespace Game.Common
{
    /// <summary>
    /// 对象回收接口
    /// 实现此接口的对象可以在对象池中自动回收和重置
    /// </summary>
    public interface IRecycle
    {
        /// <summary>
        /// 对象被回收时调用，用于重置对象状态
        /// </summary>
        void OnRecycle();

        /// <summary>
        /// 对象被取出时调用，用于初始化对象状态
        /// </summary>
        void OnSpawn();
    }
}

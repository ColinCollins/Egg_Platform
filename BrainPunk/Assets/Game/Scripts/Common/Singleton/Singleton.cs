using System;

namespace Game.Common
{
    /// <summary>
    /// 泛型单例基类（非 MonoBehaviour）
    /// 线程安全的单例实现
    /// </summary>
    /// <typeparam name="T">单例类型</typeparam>
    public class Singleton<T> where T : class, new()
    {
        private static T _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// 单例实例
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new T();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 检查单例是否已初始化
        /// </summary>
        public static bool IsInitialized => _instance != null;

        /// <summary>
        /// 销毁单例实例
        /// </summary>
        public static void Destroy()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 受保护的构造函数，防止外部实例化
        /// </summary>
        protected Singleton()
        {
        }
    }
}


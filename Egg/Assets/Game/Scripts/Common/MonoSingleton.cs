using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// MonoBehaviour 泛型单例基类
    /// 自动创建 GameObject 并附加组件，支持 DontDestroyOnLoad
    /// </summary>
    /// <typeparam name="T">继承自 MonoBehaviour 的单例类型</typeparam>
    public class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _applicationQuitting = false;

        /// <summary>
        /// 单例实例
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_applicationQuitting)
                {
                    Debug.LogWarning($"[MonoSingleton] Instance '{typeof(T)}' already destroyed on application quit. Won't create again - returning null.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<T>();

                        if (_instance == null)
                        {
                            GameObject singletonObject = new GameObject();
                            singletonObject.name = typeof(T).ToString();
                            _instance = singletonObject.AddComponent<T>();

                            // 设置为 DontDestroyOnLoad
                            DontDestroyOnLoad(singletonObject);

                            Debug.Log($"[MonoSingleton] An instance of {typeof(T)} was created.");
                        }
                        else
                        {
                            Debug.Log($"[MonoSingleton] Using instance already created: {_instance.name}");
                        }
                    }

                    return _instance;
                }
            }
        }

        /// <summary>
        /// 检查单例是否已初始化
        /// </summary>
        public static bool IsInitialized => _instance != null;

        /// <summary>
        /// 虚拟的 Awake 方法，子类可以重写
        /// </summary>
        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"[MonoSingleton] Instance already exists, destroying duplicate: {gameObject.name}");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 虚拟的 OnDestroy 方法，子类可以重写
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 虚拟的 OnApplicationQuit 方法
        /// </summary>
        protected virtual void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        /// <summary>
        /// 销毁单例实例
        /// </summary>
        public static void Destroy()
        {
            lock (_lock)
            {
                if (_instance != null)
                {
                    Destroy(_instance.gameObject);
                    _instance = null;
                }
            }
        }
    }
}


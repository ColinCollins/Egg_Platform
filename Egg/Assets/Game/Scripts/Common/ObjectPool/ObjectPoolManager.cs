using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// 对象池管理器
    /// 支持注册不同类型的对象池，每个对象池使用自定义创建函数
    /// </summary>
    public class ObjectPoolManager : MonoSingleton<ObjectPoolManager>
    {
        /// <summary>
        /// 对象池字典，key 为类型名称，value 为对应的对象池
        /// </summary>
        private Dictionary<string, IObjectPool> _pools = new Dictionary<string, IObjectPool>();

        /// <summary>
        /// 对象池根节点，用于存放所有回收的对象
        /// </summary>
        private Transform _poolRoot;

        protected virtual void Awake()
        {
            base.Awake();
            
            // 创建对象池根节点
            GameObject poolRootObj = new GameObject("ObjectPoolRoot");
            poolRootObj.transform.SetParent(transform);
            _poolRoot = poolRootObj.transform;
            _poolRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// 注册对象池
        /// </summary>
        /// <typeparam name="T">对象类型，必须实现 IRecycle 接口</typeparam>
        /// <param name="createFunc">创建对象的函数</param>
        /// <param name="initialSize">初始池大小（可选）</param>
        /// <param name="maxSize">最大池大小，0 表示无限制（可选）</param>
        public void RegisterPool<T>(Func<T> createFunc, int initialSize = 0, int maxSize = 0) where T : class, IRecycle
        {
            string typeName = typeof(T).Name;
            
            if (_pools.ContainsKey(typeName))
            {
                Debug.LogWarning($"[ObjectPoolManager] Pool for type '{typeName}' already exists. Overwriting...");
            }

            ObjectPool<T> pool = new ObjectPool<T>(createFunc, initialSize, maxSize, _poolRoot);
            _pools[typeName] = pool;

            Debug.Log($"[ObjectPoolManager] Registered pool for type '{typeName}' with initial size: {initialSize}, max size: {(maxSize > 0 ? maxSize.ToString() : "unlimited")}");
        }

        /// <summary>
        /// 从对象池获取对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <returns>对象实例，如果对象池未注册则返回 null</returns>
        public T Get<T>() where T : class, IRecycle
        {
            string typeName = typeof(T).Name;
            
            if (!_pools.TryGetValue(typeName, out IObjectPool pool))
            {
                Debug.LogError($"[ObjectPoolManager] Pool for type '{typeName}' not registered!");
                return null;
            }

            ObjectPool<T> typedPool = pool as ObjectPool<T>;
            if (typedPool == null)
            {
                Debug.LogError($"[ObjectPoolManager] Pool type mismatch for '{typeName}'!");
                return null;
            }

            T obj = typedPool.Get();
            obj?.OnSpawn();
            return obj;
        }

        /// <summary>
        /// 回收对象到对象池
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">要回收的对象</param>
        public void Recycle<T>(T obj) where T : class, IRecycle
        {
            if (obj == null)
            {
                Debug.LogWarning("[ObjectPoolManager] Trying to recycle null object!");
                return;
            }

            string typeName = typeof(T).Name;
            
            if (!_pools.TryGetValue(typeName, out IObjectPool pool))
            {
                Debug.LogError($"[ObjectPoolManager] Pool for type '{typeName}' not registered!");
                return;
            }

            ObjectPool<T> typedPool = pool as ObjectPool<T>;
            if (typedPool == null)
            {
                Debug.LogError($"[ObjectPoolManager] Pool type mismatch for '{typeName}'!");
                return;
            }

            obj.OnRecycle();
            typedPool.Recycle(obj);
        }

        /// <summary>
        /// 清空指定类型的对象池
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        public void ClearPool<T>() where T : class, IRecycle
        {
            string typeName = typeof(T).Name;
            
            if (_pools.TryGetValue(typeName, out IObjectPool pool))
            {
                pool.Clear();
                Debug.Log($"[ObjectPoolManager] Cleared pool for type '{typeName}'");
            }
            else
            {
                Debug.LogWarning($"[ObjectPoolManager] Pool for type '{typeName}' not found!");
            }
        }

        /// <summary>
        /// 清空所有对象池
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                pool.Clear();
            }
            
            _pools.Clear();
            Debug.Log("[ObjectPoolManager] Cleared all pools");
        }

        /// <summary>
        /// 获取对象池中可用对象的数量
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <returns>可用对象数量</returns>
        public int GetAvailableCount<T>() where T : class, IRecycle
        {
            string typeName = typeof(T).Name;
            
            if (_pools.TryGetValue(typeName, out IObjectPool pool))
            {
                return pool.GetAvailableCount();
            }

            return 0;
        }

        /// <summary>
        /// 检查对象池是否已注册
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <returns>是否已注册</returns>
        public bool IsPoolRegistered<T>() where T : class, IRecycle
        {
            return _pools.ContainsKey(typeof(T).Name);
        }
    }

    /// <summary>
    /// 对象池接口
    /// </summary>
    internal interface IObjectPool
    {
        void Clear();
        int GetAvailableCount();
    }

    /// <summary>
    /// 泛型对象池实现
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    internal class ObjectPool<T> : IObjectPool where T : class, IRecycle
    {
        private readonly Func<T> _createFunc;
        private readonly Queue<T> _pool = new Queue<T>();
        private readonly int _maxSize;
        private readonly Transform _poolRoot;
        private int _totalCreated = 0;

        public ObjectPool(Func<T> createFunc, int initialSize, int maxSize, Transform poolRoot)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            _maxSize = maxSize;
            _poolRoot = poolRoot;

            // 预创建初始对象
            for (int i = 0; i < initialSize; i++)
            {
                T obj = _createFunc();
                if (obj is MonoBehaviour mb)
                {
                    mb.gameObject.SetActive(false);
                    mb.transform.SetParent(_poolRoot);
                }
                _pool.Enqueue(obj);
                _totalCreated++;
            }
        }

        /// <summary>
        /// 从对象池获取对象
        /// </summary>
        public T Get()
        {
            T obj;
            
            if (_pool.Count > 0)
            {
                obj = _pool.Dequeue();
            }
            else
            {
                obj = _createFunc();
                _totalCreated++;
            }

            // 如果是 MonoBehaviour，激活并移除父节点
            if (obj is MonoBehaviour mb)
            {
                mb.gameObject.SetActive(true);
                mb.transform.SetParent(null);
            }

            return obj;
        }

        /// <summary>
        /// 回收对象到对象池
        /// </summary>
        public void Recycle(T obj)
        {
            if (obj == null)
                return;

            // 如果设置了最大大小且池已满，直接销毁对象
            if (_maxSize > 0 && _pool.Count >= _maxSize)
            {
                if (obj is MonoBehaviour mb)
                {
                    UnityEngine.Object.Destroy(mb.gameObject);
                }
                return;
            }

            // 如果是 MonoBehaviour，禁用并设置父节点
            if (obj is MonoBehaviour mb2)
            {
                mb2.gameObject.SetActive(false);
                mb2.transform.SetParent(_poolRoot);
            }

            _pool.Enqueue(obj);
        }

        /// <summary>
        /// 清空对象池
        /// </summary>
        public void Clear()
        {
            while (_pool.Count > 0)
            {
                T obj = _pool.Dequeue();
                if (obj is MonoBehaviour mb)
                {
                    UnityEngine.Object.Destroy(mb.gameObject);
                }
            }
            
            _pool.Clear();
            _totalCreated = 0;
        }

        /// <summary>
        /// 获取可用对象数量
        /// </summary>
        public int GetAvailableCount()
        {
            return _pool.Count;
        }
    }
}

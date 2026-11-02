using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GF
{
    /// <summary>
    /// 对象池工具
    /// </summary>
    public class PoolKit
    {
        #region 类对象池

        //类对象池
        private ClassObjectPool _classObjectPool = new ();
        
        /// <summary>
        /// 从池子获取一个类实例
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <returns>返回实例</returns>
        public T SpawnClassObject<T>() where T : class, new()
        {
            return _classObjectPool.Spawn<T>();
        }

        /// <summary>
        /// 回收类实例
        /// </summary>
        /// <param name="obj">类示例</param>
        /// <returns>是否成功回收</returns>
        public bool RecycleClassObject(object obj)
        {
            return _classObjectPool.Recycle(obj);
        }

        /// <summary>
        /// 设置对应类型的最大容量
        /// </summary>
        /// <param name="capacity">容量</param>
        /// <typeparam name="T">类型</typeparam>
        public void SetCapacityClass<T>(int capacity) where T : class
        {
            _classObjectPool.SetCapacity<T>(capacity);
        }

        /// <summary>
        /// 生成类对象回调
        /// </summary>
        /// <param name="action"></param>
        public void OnSpawnClassObject(Action<object> action)
        {
            _classObjectPool.OnSpawn(action);
        }
        
        /// <summary>
        /// 回收类对象回调
        /// </summary>
        /// <param name="action"></param>
        public void OnRecycleClassObject(Action<object> action)
        {
            _classObjectPool.OnRecycle(action);
        }

        /// <summary>
        /// 释放对应类型的对象池
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void DestroyClassObjectPool<T>() where T : class
        {
            _classObjectPool.DestroyClassObjectPool<T>();
        }
        
        /// <summary>
        /// 释放所有类对象池
        /// </summary>
        public void DestroyAllClassObjectPool()
        {
            _classObjectPool.Destroy();
        }

        #endregion

        #region 游戏物体对象池

        private Dictionary<string, GameObjectPool> _name2GameObjectPool = new();
        
        /// <summary>
        /// 创建游戏物体对象池
        /// </summary>
        /// <param name="poolName">池子名</param>
        /// <param name="prefab">预制体</param>
        /// <param name="capacity">最大容量</param>
        /// <param name="isPreLoad">是否预加载</param>
        /// <returns></returns>
        public GameObjectPool CreateGameObjectPool(string poolName, GameObject prefab, int capacity = 10, bool isPreLoad = false)
        {
            if (_name2GameObjectPool.ContainsKey(poolName))
            {
                LogKit.E($"已存在对象池: {poolName}");
                return null;
            }

            GameObjectPool pool = GameObjectPool.CreatePool(poolName, prefab, capacity, isPreLoad);
            if(pool!= null)
            {
                _name2GameObjectPool[poolName] = pool;
            }

            return pool;
        }
        
        /// <summary>
        /// 销毁游戏物体对象池
        /// </summary>
        /// <param name="poolName"></param>
        public void DestroyGameObjectPool(string poolName)
        {
            if (_name2GameObjectPool.TryGetValue(poolName, out var pool))
            {
                pool.Destroy();
                _name2GameObjectPool.Remove(poolName);
            }
        }

        /// <summary>
        /// 获取游戏物体
        /// </summary>
        /// <param name="poolName">池子名</param>
        /// <param name="parent">父物体</param>
        /// <param name="worldPositionStays"></param>
        /// <returns></returns>
        public GameObject SpawnGameObject(string poolName, Transform parent = null, bool worldPositionStays = true)
        {
            if (!_name2GameObjectPool.TryGetValue(poolName, out var pool))
            {
                LogKit.E($"不存在对象池: {poolName}");
                return null;
            }

            return pool.Spawn(parent, worldPositionStays);
        }
        
        /// <summary>
        /// 回收游戏物体
        /// </summary>
        /// <param name="poolName">池子名</param>
        /// <param name="obj">游戏物体</param>
        /// <returns></returns>
        public bool RecycleGameObject(string poolName, GameObject obj)
        {
            if (!_name2GameObjectPool.TryGetValue(poolName, out var pool))
            {
                LogKit.E("不存在对象池: {poolName}");
                Object.Destroy(obj);
                return false;
            }

            return pool.Recycle(obj);
        }
        
        /// <summary>
        /// 回收从该池子中生成的所有游戏物体
        /// </summary>
        /// <param name="poolName">池子名</param>
        public void RecycleAllGameObject(string poolName)
        {
            if (!_name2GameObjectPool.TryGetValue(poolName, out var pool))
            {
                LogKit.E("不存在对象池: {poolName}");
                return;
            }

            pool.RecycleAll();
        }
        
        /// <summary>
        /// 创建游戏物体回调
        /// </summary>
        /// <param name="poolName">池子名</param>
        /// <param name="action"></param>
        public void OnSpawnGameObject(string poolName, System.Action<GameObject> action)
        {
            if (!_name2GameObjectPool.TryGetValue(poolName, out var pool))
            {
                LogKit.E("不存在对象池: {poolName}");
                return;
            }

            pool.OnSpawn(action);
        }
        
        /// <summary>
        /// 回收游戏物体回调
        /// </summary>
        /// <param name="poolName"></param>
        /// <param name="action"></param>
        public void OnRecycleGameObject(string poolName, System.Action<GameObject> action)
        {
            if (!_name2GameObjectPool.TryGetValue(poolName, out var pool))
            {
                LogKit.E("不存在对象池: {poolName}");
                return;
            }

            pool.OnRecycle(action);
        }

        #endregion
        

        /// <summary>
        /// 释放池
        /// </summary>
        public void Destroy()
        {
            _classObjectPool.Destroy();
            foreach (var kv in _name2GameObjectPool)
            {
                kv.Value.Destroy();
            }
            _name2GameObjectPool.Clear();
        }
    }
}
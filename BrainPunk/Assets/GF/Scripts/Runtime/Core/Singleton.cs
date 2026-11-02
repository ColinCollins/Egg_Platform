using System;
using UnityEngine;

namespace GF
{
    public abstract class Singleton<T> where T : class, new()//使用关键字 new() 限定，必须含有无参构造函数的单例 
    {
        // 用于lock块的对象,使用 双重锁确保单例在多线程初始化时的线程安全性
        private static readonly object _synclock = new object();
        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_synclock)
                    {
                        _instance = Activator.CreateInstance<T>();//new T();
                    }
                }
                return _instance;
            }
        }
        
        public virtual void Initialize()
        {
            
        }

        public virtual void Dispose()
        {
            
        }
    }
    public abstract class MonoSingleton<T> : MonoBehaviour where T : Component
    {
        protected static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType(typeof(T)) as T;
                    if (_instance == null)
                    {
                        var obj = new GameObject(typeof(T).Name);
                        _instance = obj.AddComponent<T>();
                    }
                }
                return _instance;
            }
        }
        protected virtual void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (_instance != null) DestroyImmediate(gameObject); else _instance = this as T;
        }
    }
}
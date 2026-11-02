using System;
using System.Collections.Generic;
using System.Linq;

namespace GF
{
	/// <summary>
	/// 类对象池
	/// </summary>
	public class ClassObjectPool
	{
		private Dictionary<int, int> _type2Capacity;
		private Dictionary<int, Dictionary<int, object>> _classObjectPoolDic;
		private int _defaultCapacity;
		private Action<object> _onSpawn;
		private Action<object> _onRecycle;

		public ClassObjectPool(int defaultCapacity = 50)
		{
			_defaultCapacity = defaultCapacity;
			_type2Capacity = new Dictionary<int, int>();
			_classObjectPoolDic = new Dictionary<int, Dictionary<int, object>>();
		}

		/// <summary>
		/// 设置对应类型的最大容量
		/// </summary>
		/// <param name="count">容量</param>
		/// <typeparam name="T">类型</typeparam>
		public void SetCapacity<T>(int count) where T : class
		{
			int key = typeof(T).GetHashCode();
			_type2Capacity[key] = count;
		}

		/// <summary>
		/// 从池子获取一个类实例
		/// </summary>
		/// <typeparam name="T">类型</typeparam>
		/// <returns>返回实例</returns>
		public T Spawn<T>() where T : class, new()
		{
			lock (_classObjectPoolDic)
			{
				int key = typeof(T).GetHashCode();

				if (!_classObjectPoolDic.TryGetValue(key, out var dic))
				{
					dic = new Dictionary<int, object>();
					_classObjectPoolDic[key] = dic;
				}
				if (dic.Count > 0)
				{
					var kv = dic.First();
					object obj = kv.Value;
					if (obj == null)
					{
						obj = new T();
					}
					dic.Remove(kv.Key);
					
					_onSpawn?.Invoke(obj);
					return (T)obj;
				}
				
				//注意：此处是使用反射创建实例，所以需要注意性能问题
				T instance = new T();
				_onSpawn?.Invoke(instance);
				return instance;
			}
		}

		/// <summary>
		/// 回收类实例
		/// </summary>
		/// <param name="obj">类示例</param>
		/// <returns>是否成功回收</returns>
		public bool Recycle(object obj)
		{
			lock (_classObjectPoolDic)
			{
				if (obj == null)
				{
					LogKit.E("回收的对象为空");
					return false;
				}
				int key = obj.GetType().GetHashCode();

				if (_classObjectPoolDic.TryGetValue(key, out var dic))
				{
					int capacity = _defaultCapacity;
					if (_type2Capacity.TryGetValue(key, out int val))
					{
						capacity = val;
					}

					int instanceId = obj.GetHashCode();
					if (dic.Count < capacity && !dic.ContainsKey(instanceId))
					{
						_onRecycle?.Invoke(obj);
						dic[instanceId] = obj;
						return true;
					}
				}

				return false;
			}
		}
		
		public void OnSpawn(Action<object> action)
		{
			_onSpawn = action;
		}
        
		public void OnRecycle(Action<object> action)
		{
			_onRecycle = action;
		}
		
		/// <summary>
		/// 释放对应类型的类对象池
		/// </summary>
		/// <typeparam name="T"></typeparam>
		public void DestroyClassObjectPool<T>() where T : class
		{
			lock (_classObjectPoolDic)
			{
				int key = typeof(T).GetHashCode();
				if (_classObjectPoolDic.TryGetValue(key, out var dic))
				{
					dic.Clear();
					_classObjectPoolDic.Remove(key);
				}
			}
		}

		/// <summary>
		/// 释放类对象池
		/// </summary>
		public void Destroy()
		{
			lock (_classObjectPoolDic)
			{
				foreach (var kv in _classObjectPoolDic)
				{
					kv.Value.Clear();
				}
				_classObjectPoolDic.Clear();
			}
			_type2Capacity.Clear();
		}
	}
}
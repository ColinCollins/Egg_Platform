using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GF
{
    public class GameObjectPool
    {
        private int _capacity = 10;
        private string _poolName;
        private GameObject _prefab;
        private Transform _root;
        private Dictionary<int, GameObject> _cacheDic;
        private Dictionary<int, GameObject> _activeObj;
        private List<GameObject> _tmpList;
        private Action<GameObject> _onSpawn;
        private Action<GameObject> _onRecycle;

        public static GameObjectPool CreatePool(string poolName, GameObject prefab, int capacity = 10, bool isPreLoad = false)
        {
            if (string.IsNullOrEmpty(poolName) || prefab == null)
            {
                LogKit.E("poolName is null");
                return null;
            }
            GameObjectPool pool = new GameObjectPool();
            pool._poolName = poolName;
            pool._prefab = prefab;
            pool._capacity = capacity;
            pool._cacheDic = new Dictionary<int, GameObject>(capacity);
            pool._tmpList = new List<GameObject>();
            pool._activeObj = new Dictionary<int, GameObject>();
            pool._root = new GameObject(poolName + " [Pool]").transform;
            Object.DontDestroyOnLoad(pool._root.gameObject);
            pool._root.gameObject.SetActive(false);
            if (isPreLoad)
            {
                PreLoad(pool);
            }
            return pool;
        }

        private static void PreLoad(GameObjectPool pool, bool worldPositionStays = true)
        {
            for (int i = 0; i < pool._capacity; i++)
            {
                GameObject obj = Object.Instantiate(pool._prefab);
                obj.transform.SetParent(pool._root, worldPositionStays);
                pool._cacheDic[obj.GetHashCode()] = obj;
            }
        }

        private GameObjectPool() {}

        public void OnSpawn(Action<GameObject> action)
        {
            _onSpawn = action;
        }
        
        public void OnRecycle(Action<GameObject> action)
        {
            _onRecycle = action;
        }
        
        public GameObject Spawn(Transform parent = null, bool worldPositionStays = true)
        {
            GameObject obj = null;
            
            if (_cacheDic.Count<=0)
            {
                obj = Object.Instantiate(_prefab);
            }
            else
            {
                KeyValuePair<int, GameObject> kv = _cacheDic.First();
                if (kv.Value == null)
                {
                    _cacheDic.Remove(kv.Key);
                    obj = Object.Instantiate(_prefab);
                }else
                {
                    obj = kv.Value;

                    int hash = obj.GetHashCode();
                    if (_cacheDic.ContainsKey(hash))
                    {
                        _cacheDic.Remove(hash);
                    }
                }
            }

            _activeObj[obj.GetHashCode()] = obj;

            obj.transform.SetParent(parent, worldPositionStays);
            if (!obj.activeSelf)
            {
                obj.SetActive(true);
            }
            obj.transform.localScale=Vector3.one;
            _onSpawn?.Invoke(obj);
            return obj;
        }

        public bool Recycle(GameObject obj)
        {
            if (obj == null)
            {
                return false;
            }

            int hash = obj.GetHashCode();
            if (_activeObj.ContainsKey(hash))
            {
                _activeObj.Remove(hash);
            }
            if (_cacheDic.Count < _capacity)
            {
                _onRecycle?.Invoke(obj);
                _cacheDic[hash] = obj;
                obj.transform.SetParent(_root);
                return true;
            }

            if (!_cacheDic.ContainsKey(hash))
            {
                Object.Destroy(obj);
            }

            return false;
        }

        public void RecycleAll()
        {
            foreach (var kv in _activeObj)
            {
                _tmpList.Add(kv.Value);
            }

            foreach (GameObject obj in _tmpList)
            {
                Recycle(obj);
            }
            
            _activeObj.Clear();
            _tmpList.Clear();
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }
            
            _cacheDic?.Clear();
            _activeObj?.Clear();
            _tmpList?.Clear();
            _onSpawn = null;
            _onRecycle = null;
        }
    }
}
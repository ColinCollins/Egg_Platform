using System.Collections.Generic;
using Game.Common;
using UnityEngine;

/// <summary>
/// Shit 对象池控制器，负责注册对象池并提供生成接口
/// </summary>
public class ShitPoolCtrl : MonoBehaviour
{
    [Header("Shit Prefabs")]
    [Tooltip("Shit Prefab 列表，需自带 ShitItem 脚本")]
    [SerializeField] private List<GameObject> shitPrefabs = new List<GameObject>();

    [Header("Pool Settings")]
    [SerializeField] private int initialPoolSize = 5;
    [SerializeField] private int maxPoolSize = 50;

    private bool isPoolRegistered;
    private float _goldChance = -1f;
    private readonly HashSet<ShitItem> _spawnedItems = new HashSet<ShitItem>();

    /// <summary>
    /// 外部设置 prefab 列表（用于代码动态初始化）
    /// </summary>
    public void SetupPrefabs(List<GameObject> prefabs, float goldChance = -1f)
    {
        if (prefabs == null || prefabs.Count == 0)
            return;

        shitPrefabs.Clear();
        shitPrefabs.AddRange(prefabs);
        _goldChance = goldChance;
    }

    private void OnDestroy()
    {
        DestroySpawnedAndUnregisterPool();
    }

    /// <summary>
    /// 注册对象池
    /// </summary>
    public void RegisterPool()
    {
        if (ObjectPoolManager.Instance == null)
        {
            Debug.LogError("[ShitPoolCtrl] ObjectPoolManager is not initialized!");
            return;
        }

        if (ObjectPoolManager.Instance.IsPoolRegistered<ShitItem>())
            return;

        if (shitPrefabs.Count == 0)
        {
            Debug.LogWarning("[ShitPoolCtrl] Shit prefabs list is empty!");
            return;
        }

        ObjectPoolManager.Instance.RegisterPool<ShitItem>(() =>
        {
            GameObject prefab;
            if (shitPrefabs.Count >= 2 && _goldChance >= 0f)
            {
                prefab = Random.value < _goldChance ? shitPrefabs[1] : shitPrefabs[0];
            }
            else
            {
                int index = Random.Range(0, shitPrefabs.Count);
                prefab = shitPrefabs[index];
            }
            var go = Instantiate(prefab);
            var shit = go.GetComponent<ShitItem>();
            if (shit == null)
            {
                Debug.LogError("[ShitPoolCtrl] Prefab missing ShitItem component!");
                Destroy(go);
                return null;
            }
            return shit;
        }, initialSize: initialPoolSize, maxSize: maxPoolSize);

        isPoolRegistered = true;
    }

    /// <summary>
    /// 从对象池生成一个 Shit
    /// </summary>
    public ShitItem Spawn(Vector3 position, Transform parent = null)
    {
        if (!isPoolRegistered)
        {
            RegisterPool();
        }

        if (ObjectPoolManager.Instance == null)
            return null;

        var shit = ObjectPoolManager.Instance.Get<ShitItem>();
        if (shit == null)
            return null;

        _spawnedItems.Add(shit);

        var t = shit.transform;
        if (parent != null)
            t.SetParent(parent);
        else
            t.SetParent(null);

        t.position = position;
        return shit;
    }

    public void DestroySpawnedAndUnregisterPool()
    {
        foreach (ShitItem item in _spawnedItems)
        {
            if (item == null || !item.gameObject.activeInHierarchy)
                continue;

            Destroy(item.gameObject);
        }

        _spawnedItems.Clear();

        if (ObjectPoolManager.Instance != null && ObjectPoolManager.Instance.IsPoolRegistered<ShitItem>())
        {
            ObjectPoolManager.Instance.UnregisterPool<ShitItem>();
        }

        isPoolRegistered = false;
    }
}

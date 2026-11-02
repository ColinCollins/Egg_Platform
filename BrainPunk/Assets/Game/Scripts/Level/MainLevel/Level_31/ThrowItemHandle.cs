using DG.Tweening;
using Game.Common;
using UnityEngine;

/// <summary>
/// 投掷物体处理器：从对象池生成物体，按抛物线 JumpTo 落到随机 X、指定 Y 的位置。
/// </summary>
public class ThrowItemHandle : MonoBehaviour
{
    [Header("Prefab Settings")]
    [SerializeField] private ThrowableItem itemPrefab;

    [Header("Control Settings")]
    [Tooltip("是否启用生成")]
    [SerializeField] private bool enableSpawn = true;

    [Tooltip("是否自动开始生成")]
    [SerializeField] private bool autoStart = true;

    [Tooltip("生成时间间隔（秒）")]
    [SerializeField] private float spawnInterval = 1f;

    [Header("Scale Settings")]
    [Tooltip("初始 scale 的最小值（x 和 y 保持一致）")]
    [SerializeField] private float minScale = 1f;

    [Tooltip("初始 scale 的最大值（x 和 y 保持一致）")]
    [SerializeField] private float maxScale = 1f;

    [Header("Jump To Settings")]
    [Tooltip("跳跃时长（秒）")]
    [SerializeField] private float duration = 1f;

    [Tooltip("抛物线弧高")]
    [SerializeField] private float jumpPower = 2f;

    [Tooltip("落点 X 相对生成位置的随机范围（min, max）")]
    [SerializeField] private Vector2 randomXRange = new Vector2(-2f, 2f);

    [Tooltip("落点 Y（世界坐标）")]
    [SerializeField] private float endPositionY = 0f;

    [Header("Rotation Settings")]
    [Tooltip("是否跟随当前物体的旋转角度计算落点偏移")]
    [SerializeField] private bool followRotate = false;

    [Header("Pool Settings")]
    [Tooltip("对象池初始大小")]
    [SerializeField] private int poolInitialSize = 5;

    [Tooltip("对象池最大大小（0 表示无限制）")]
    [SerializeField] private int poolMaxSize = 20;

    [Header("Spawn Settings")]
    [Tooltip("生成位置（为空时使用当前 transform 位置）")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Item 的父节点（为空时保持原 parent）")]
    [SerializeField] private Transform root;

    private bool _poolRegistered = false;
    private float _nextSpawnTime = 0f; // 下次生成的时间

    void Start()
    {
        RegisterPool();
        
        if (autoStart && enableSpawn)
        {
            _nextSpawnTime = Time.time + spawnInterval;
        }
    }

    /// <summary>
    /// 注册对象池
    /// </summary>
    private void RegisterPool()
    {
        if (itemPrefab == null)
        {
            Debug.LogWarning("[ThrowItemHandle] Item prefab is not assigned!");
            return;
        }

        if (ObjectPoolManager.Instance != null && !_poolRegistered)
        {
            ObjectPoolManager.Instance.RegisterPool(
                () => Instantiate(itemPrefab),
                initialSize: poolInitialSize,
                maxSize: poolMaxSize
            );
            _poolRegistered = true;
        }
    }

    /// <summary>
    /// 生成一个物体
    /// </summary>
    public ThrowableItem SpawnItem()
    {
        return SpawnItem(GetSpawnPosition());
    }

    /// <summary>
    /// 在指定位置生成物体
    /// </summary>
    /// <param name="spawnPosition">生成位置</param>
    public ThrowableItem SpawnItem(Vector3 spawnPosition)
    {
        if (itemPrefab == null || ObjectPoolManager.Instance == null)
        {
            Debug.LogWarning("[ThrowItemHandle] Prefab or ObjectPoolManager is null!");
            return null;
        }

        ThrowableItem item = ObjectPoolManager.Instance.Get<ThrowableItem>();
        if (item == null)
        {
            Debug.LogWarning("[ThrowItemHandle] Failed to get item from pool!");
            return null;
        }

        // 设置 parent
        if (root != null)
        {
            item.transform.SetParent(root);
        }

        item.transform.position = spawnPosition;

        float randomScaleValue = Random.Range(minScale, maxScale);
        item.transform.localScale = new Vector3(randomScaleValue, randomScaleValue, 1f);

        float randomOffset = Random.Range(randomXRange.x, randomXRange.y);
        Vector3 endPos;
        if (followRotate)
        {
            Vector3 worldOffset = transform.right * randomOffset;
            endPos = spawnPosition + worldOffset;
            endPos.y = endPositionY;
        }
        else
        {
            endPos = new Vector3(spawnPosition.x + randomOffset, endPositionY, spawnPosition.z);
        }

        JumpTo(item.transform, spawnPosition, endPos, duration, jumpPower);

        return item;
    }

    private static void JumpTo(Transform target, Vector3 startPos, Vector3 endPos, float duration, float jumpPower)
    {
        float progress = 0f;
        DOTween.To(() => progress, x => progress = x, 1f, duration)
            .SetEase(Ease.Linear)
            .SetAutoKill(true)
            .OnUpdate(() =>
            {
                if (target == null) return;
                float t = progress;
                float arc = 4f * jumpPower * t * (1f - t);
                target.position = new Vector3(
                    Mathf.Lerp(startPos.x, endPos.x, t),
                    Mathf.Lerp(startPos.y, endPos.y, t) + arc,
                    Mathf.Lerp(startPos.z, endPos.z, t)
                );
            })
            .OnComplete(() =>
            {
                if (target != null)
                    target.position = endPos;
            });
    }

    /// <summary>
    /// 获取生成位置
    /// </summary>
    private Vector3 GetSpawnPosition()
    {
        if (spawnPoint != null)
            return spawnPoint.position;
        return transform.position;
    }

    /// <summary>
    /// 开始自动生成
    /// </summary>
    public void StartSpawning()
    {
        enableSpawn = true;
        _nextSpawnTime = Time.time + spawnInterval;
    }

    /// <summary>
    /// 停止自动生成
    /// </summary>
    public void StopSpawning()
    {
        enableSpawn = false;
        _nextSpawnTime = 0f;
    }

    /// <summary>
    /// 设置是否启用生成
    /// </summary>
    public void SetEnableSpawn(bool enable)
    {
        enableSpawn = enable;
        
        if (!enable)
        {
            StopSpawning();
        }
        else if (autoStart)
        {
            StartSpawning();
        }
    }

    /// <summary>
    /// 设置生成间隔
    /// </summary>
    public void SetSpawnInterval(float interval)
    {
        spawnInterval = Mathf.Max(0f, interval);
    }

    void Update()
    {
        if (!enableSpawn)
            return;

        if (Time.time >= _nextSpawnTime)
        {
            SpawnItem();
            _nextSpawnTime = Time.time + spawnInterval;
        }
    }

    void OnDestroy()
    {
        // 可选：清理对象池
        if (_poolRegistered && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.ClearPool<ThrowableItem>();
        }
    }
}

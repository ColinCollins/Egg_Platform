using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Level121 金币雨生成线：在横向线段上随机选点，并通过既有金币池垂直向下投放。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level121VerticalCoinRain : MonoBehaviour
{
    #region Inspector

    [Tooltip("复用 Level31 金币对象池和落地回收链路的投放器。")]
    [SerializeField] private ThrowItemHandle throwHandle;

    [Tooltip("金币生成横线的左端点。")]
    [SerializeField] private Transform lineStart;

    [Tooltip("金币生成横线的右端点。")]
    [SerializeField] private Transform lineEnd;

    [Tooltip("相邻两枚金币的生成间隔。")]
    [SerializeField, Min(0.01f)] private float spawnInterval = 0.3f;

    [Tooltip("停雨后延迟回收隐藏金币的时间；需覆盖 ThrowItemHandle 当前 2.5 秒下落 Tween。")]
    [SerializeField, Min(0f)] private float clearRecycleDelay = 2.6f;

    #endregion

    #region Runtime State

    private readonly List<ThrowableItem> _spawnedCoins = new List<ThrowableItem>();
    private readonly List<ThrowableItem> _pendingRecycleCoins = new List<ThrowableItem>();
    private float _nextSpawnTime;
    private float _pendingRecycleTime;
    private Coroutine _recycleCoroutine;
    private bool _isRunning;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        ResumePendingRecycle();
        StartRain();
    }

    private void OnDisable()
    {
        _isRunning = false;

        if (_recycleCoroutine != null)
        {
            StopCoroutine(_recycleCoroutine);
            _recycleCoroutine = null;
        }
    }

    private void Update()
    {
        if (!_isRunning || Time.time < _nextSpawnTime)
        {
            return;
        }

        SpawnCoinOnLine();
        _nextSpawnTime = Time.time + Mathf.Max(0.01f, spawnInterval);
    }

    private void OnDrawGizmos()
    {
        if (lineStart == null || lineEnd == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(lineStart.position, lineEnd.position);
        Gizmos.DrawWireSphere(lineStart.position, 0.12f);
        Gizmos.DrawWireSphere(lineEnd.position, 0.12f);
    }

    #endregion

    #region Coin Spawn

    /// <summary>
    /// 开始或重新开始本关金币雨。
    /// </summary>
    public void StartRain()
    {
        _isRunning = true;
        _nextSpawnTime = Time.time + Mathf.Max(0.01f, spawnInterval);
    }

    /// <summary>
    /// 停止继续生成，并清除仍带有效碰撞体的下落金币；已经被拾取、正在飞向气泡的金币保留原流程。
    /// </summary>
    public void StopAndClearFallingCoins()
    {
        _isRunning = false;
        _nextSpawnTime = 0f;

        for (int i = _spawnedCoins.Count - 1; i >= 0; i--)
        {
            ThrowableItem item = _spawnedCoins[i];
            if (item == null || !item.gameObject.activeSelf)
            {
                continue;
            }

            Collider2D coinCollider = item.GetComponent<Collider2D>();
            if (coinCollider != null && !coinCollider.enabled)
            {
                continue;
            }

            SpriteRenderer coinRenderer = item.GetComponent<SpriteRenderer>();
            coinRenderer?.DOKill();
            item.gameObject.SetActive(false);
            _pendingRecycleCoins.Add(item);
        }

        _spawnedCoins.Clear();
        SchedulePendingRecycle();
    }

    private void SpawnCoinOnLine()
    {
        if (throwHandle == null || lineStart == null || lineEnd == null)
        {
            return;
        }

        float minX = Mathf.Min(lineStart.position.x, lineEnd.position.x);
        float maxX = Mathf.Max(lineStart.position.x, lineEnd.position.x);
        float lineY = (lineStart.position.y + lineEnd.position.y) * 0.5f;
        float lineZ = (lineStart.position.z + lineEnd.position.z) * 0.5f;
        Vector3 spawnPosition = new Vector3(Random.Range(minX, maxX), lineY, lineZ);
        PruneInactiveCoins();
        ThrowableItem item = throwHandle.SpawnItem(spawnPosition);
        if (item != null)
        {
            _spawnedCoins.Add(item);
        }
    }

    private void PruneInactiveCoins()
    {
        for (int i = _spawnedCoins.Count - 1; i >= 0; i--)
        {
            ThrowableItem item = _spawnedCoins[i];
            if (item == null || !item.gameObject.activeSelf)
            {
                _spawnedCoins.RemoveAt(i);
            }
        }
    }

    private void SchedulePendingRecycle()
    {
        if (_pendingRecycleCoins.Count == 0)
        {
            return;
        }

        if (_recycleCoroutine != null)
        {
            StopCoroutine(_recycleCoroutine);
        }

        _pendingRecycleTime = Time.time + Mathf.Max(0f, clearRecycleDelay);
        _recycleCoroutine = StartCoroutine(RecyclePendingCoinsAfterDelay());
    }

    private void ResumePendingRecycle()
    {
        if (_pendingRecycleCoins.Count == 0)
        {
            return;
        }

        _recycleCoroutine = StartCoroutine(RecyclePendingCoinsAfterDelay());
    }

    private IEnumerator RecyclePendingCoinsAfterDelay()
    {
        float waitTime = Mathf.Max(0f, _pendingRecycleTime - Time.time);
        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }

        for (int i = _pendingRecycleCoins.Count - 1; i >= 0; i--)
        {
            ThrowableItem item = _pendingRecycleCoins[i];
            if (item != null)
            {
                item.RecycleSelf();
            }
        }

        _pendingRecycleCoins.Clear();
        _recycleCoroutine = null;
    }

    #endregion
}

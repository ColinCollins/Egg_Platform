using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level50 子游戏，控制生成陨石 prefab 不断的从 200 的位置生成，对准 Actor 当前的位置以及随机生成一些其他位置。
/// 调用 StartPlay 开始调用
/// 提供固定的游戏时间，默认 5 分钟，5 分钟内没死亡就触发成功回调。
/// 提供一个 Prefab List 用于注册 ObjectPool。
/// </summary>
public class Level50FlyingGameCtrl : MonoBehaviour
{
    [Header("Actor")]
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private ParticleSystem knock;

    [Header("Meteorite Prefabs")]
    [Tooltip("陨石 Prefab 列表，用于注册 ObjectPool，需自带 Level50Meteorite 脚本")]
    [SerializeField] private List<GameObject> meteoritePrefabs = new List<GameObject>();

    [Header("Spawn Settings")]
    [SerializeField] private float spawnHeight = 200f;
    [Tooltip("生成间隔范围，X=最小间隔，Y=最大间隔。随着游戏进行，间隔会从 Y 逐渐减小到 X")]
    [SerializeField] private Vector2 spawnInterval = new Vector2(0.3f, 1.5f);
    [SerializeField] private float spawnRandomRange = 10f;
    [SerializeField] private float meteoriteFallSpeed = 10f;
    [SerializeField] private float meteoriteAutoRecycleY = -50f;

    [Header("Protection")]
    [Tooltip("游戏开始后 Actor 的初始无敌时间（秒），期间不会被陨石击杀")]
    [SerializeField] private float protectDuration = 2f;
    [Tooltip("Actor 正上方安全区域半宽，陨石不会直接生成在该范围内")]
    [SerializeField] private float safeZoneHalfWidth = 2f;

    [Header("Game Settings")]
    [SerializeField] private float gameDuration = 300f;

    public float Duration
    {
        get => gameDuration;
    }

    private bool isPlaying;
    private float elapsedTime;
    private float spawnTimer;
    private bool isFinished;
    private float protectTimer;

    /// <summary>
    /// Actor 是否处于防护/无敌状态
    /// </summary>
    public bool IsInvincible => isPlaying && protectTimer > 0f;

    /// <summary>
    /// 游戏成功回调（5 分钟内未死亡）
    /// </summary>
    public Action OnSuccess;

    /// <summary>
    /// 游戏失败回调（Actor 死亡）
    /// </summary>
    public Action OnFail;

    private void Update()
    {
        if (!isPlaying || isFinished)
            return;

        elapsedTime += Time.deltaTime;
        spawnTimer += Time.deltaTime;

        if (protectTimer > 0f)
        {
            protectTimer -= Time.deltaTime;
        }

        // 检查 Actor 是否死亡
        if (actor != null && actor.IsDied)
        {
            FinishGame(false);
            return;
        }

        // 检查是否到达游戏时间
        if (elapsedTime >= gameDuration)
        {
            FinishGame(true);
            return;
        }

        // 生成陨石（间隔随 duration 降低而变小）
        float currentInterval = GetCurrentSpawnInterval();
        if (spawnTimer >= currentInterval)
        {
            spawnTimer = 0f;
            SpawnMeteorite();
        }
    }

    /// <summary>
    /// 开始游戏，注册对象池并启动陨石生成与计时
    /// </summary>
    public void StartPlay()
    {
        if (isPlaying)
            return;

        if (actor == null)
        {
            actor = FindFirstObjectByType<ActorCtrl>();
        }

        if (actor == null)
        {
            Debug.LogError("[Level50FlyingGameCtrl] Actor not found!");
            return;
        }

        if (meteoritePrefabs.Count == 0)
        {
            Debug.LogWarning("[Level50FlyingGameCtrl] Meteorite prefabs list is empty!");
            return;
        }

        RegisterObjectPool();

        isPlaying = true;
        isFinished = false;
        elapsedTime = 0f;
        protectTimer = protectDuration;
        spawnTimer = GetCurrentSpawnInterval(); // 立即生成第一颗
    }

    /// <summary>
    /// 停止游戏并清理对象池
    /// </summary>
    public void StopPlay()
    {
        isPlaying = false;
        isFinished = false;
        ClearObjectPool();
    }

    private void RegisterObjectPool()
    {
        if (ObjectPoolManager.Instance == null)
        {
            Debug.LogError("[Level50FlyingGameCtrl] ObjectPoolManager is not initialized!");
            return;
        }

        if (ObjectPoolManager.Instance.IsPoolRegistered<Level50Meteorite>())
            return;

        ObjectPoolManager.Instance.RegisterPool<Level50Meteorite>(() =>
        {
            int index = UnityEngine.Random.Range(0, meteoritePrefabs.Count);
            var prefab = meteoritePrefabs[index];
            var go = Instantiate(prefab);
            var meteorite = go.GetComponent<Level50Meteorite>();
            meteorite.SetAutoRecycleY(meteoriteAutoRecycleY);
            return meteorite;
        }, initialSize: 5, maxSize: 50);
    }

    private void ClearObjectPool()
    {
        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.ClearPool<Level50Meteorite>();
        }
    }

    /// <summary>
    /// 根据已进行的游戏时间，动态计算当前生成间隔
    /// </summary>
    private float GetCurrentSpawnInterval()
    {
        if (gameDuration <= 0f)
            return spawnInterval.y;

        float t = Mathf.Clamp01(elapsedTime / gameDuration);
        // 随着 elapsedTime 增加（剩余时间减少），间隔从 max 线性减小到 min
        return Mathf.Lerp(spawnInterval.y, spawnInterval.x, t);
    }

    private void SpawnMeteorite()
    {
        if (actor == null || ObjectPoolManager.Instance == null)
            return;

        var meteorite = ObjectPoolManager.Instance.Get<Level50Meteorite>();
        if (meteorite == null)
            return;

        // 50% 概率瞄准 Actor，50% 概率随机偏移
        float targetX = actor.transform.position.x;
        if (UnityEngine.Random.value >= 0.5f)
        {
            targetX += UnityEngine.Random.Range(-spawnRandomRange, spawnRandomRange);
        }

        // 确保不会直接生成在 Actor 正上方（安全区内），给玩家反应时间
        float diffX = targetX - actor.transform.position.x;
        if (Mathf.Abs(diffX) < safeZoneHalfWidth)
        {
            float offset = safeZoneHalfWidth * Mathf.Sign(diffX);
            if (offset == 0f) offset = safeZoneHalfWidth;
            targetX = actor.transform.position.x + offset;
        }

        Vector3 spawnPos = new Vector3(targetX, spawnHeight, actor.transform.position.z);

        // prefab 自带 trap 脚本，获取 component 后直接赋值即可
        meteorite.Init(spawnPos, meteoriteFallSpeed, actor, knock);
    }

    private void FinishGame(bool isSuccess)
    {
        if (isFinished)
            return;

        isFinished = true;
        isPlaying = false;

        if (isSuccess)
        {
            Debug.Log("[Level50FlyingGameCtrl] Game Success!");
            OnSuccess?.Invoke();
        }
        else
        {
            Debug.Log("[Level50FlyingGameCtrl] Game Failed!");
            OnFail?.Invoke();
        }
    }

    private void OnDestroy()
    {
        isPlaying = false;
        isFinished = true;

        // 清理对象池，防止残留陨石在切换场景后仍然活跃
        ClearObjectPool();

        // 清空事件，防止外部对已销毁对象的引用导致 MissingReferenceException
        OnSuccess = null;
        OnFail = null;
    }
}

using System;
using System.Collections.Generic;
using Game.Common;
using UnityEngine;

public class Level56FlipBirdGameCtrl : MonoBehaviour
{
    [Serializable]
    private class ScrollTargetData
    {
        // The transform to scroll during the mini game.
        public Transform target;
        // Leftward movement speed in units per second.
        public float moveSpeed = 2f;
        // Reset when the target moves past this X position.
        public float resetThresholdX = -20f;
        // Amount added to X after reset to loop the target.
        public float resetOffsetX = 40f;
        // Use localPosition for child loops, world position otherwise.
        public bool useLocalPosition = true;

        // Cached at startup so each run can restore the original position.
        [NonSerialized] public Vector3 initialPosition;
    }

    [Header("Actor")]
    [SerializeField] private ActorCtrl actor;

    [Header("Scroll Targets")]
    [SerializeField] private List<ScrollTargetData> scrollTargets = new List<ScrollTargetData>();

    [Header("Pipe Prefabs")]
    [SerializeField] private List<GameObject> pipePrefabs = new List<GameObject>();
    [SerializeField] private Transform spawnRoot;
    // World-space offset applied to the base spawn point before each pipe is initialized.
    [SerializeField] private Vector3 pipeSpawnPositionOffset = Vector3.zero;
    [SerializeField] private Vector2 pipeSpawnYOffsetRange = Vector2.zero;

    [Header("Game Settings")]
    // Total mini game duration in seconds before success is triggered.
    [SerializeField] private float gameDuration = 60f;
    // Time interval between pipe spawns.
    [SerializeField] private float spawnInterval = 2f;
    // Leftward movement speed applied to active pipes.
    [SerializeField] private float pipeMoveSpeed = 5f;
    // Pipes are recycled once they move past this world-space X position.
    [SerializeField] private float pipeRecycleX = -20f;
    // Initial number of pooled pipe instances to create.
    [SerializeField] private int poolInitialSize = 4;
    // Maximum number of pooled pipe instances allowed.
    [SerializeField] private int poolMaxSize = 20;

    private readonly List<Level56PipeObstacle> activePipes = new List<Level56PipeObstacle>();
    private readonly List<GameObject> fallbackPipeSources = new List<GameObject>();

    private bool isPlaying;
    private bool isFinished;
    private bool isStoppingAfterPlay;
    private bool poolRegisteredBySelf;
    private float elapsedTime;
    private float spawnTimer;

    public float Duration => gameDuration;

    public Action OnSuccess;
    public Action OnFail;

    private void Awake()
    {
        ResolveSpawnRoot();
        CacheFallbackPipeSources();
        CacheScrollTargetPositions();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (isStoppingAfterPlay)
        {
            UpdateActivePipes(deltaTime);

            if (activePipes.Count == 0)
            {
                CompleteStopPlay();
            }

            return;
        }

        if (!isPlaying || isFinished)
        {
            return;
        }

        elapsedTime += deltaTime;
        spawnTimer += deltaTime;

        UpdateScrollTargets(deltaTime);
        UpdateActivePipes(deltaTime);

        if (elapsedTime >= gameDuration)
        {
            FinishGame(true);
            return;
        }

        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            SpawnPipe();
        }
    }

    public void SetActor(ActorCtrl targetActor)
    {
        if (targetActor != null)
        {
            actor = targetActor;
        }
    }

    public void StartPlay()
    {
        if (isPlaying)
        {
            return;
        }

        if (actor == null)
        {
            actor = FindFirstObjectByType<ActorCtrl>();
        }

        ResolveSpawnRoot();
        CacheFallbackPipeSources();
        CacheScrollTargetPositions();
        ResetScrollTargets();
        RecycleAllActivePipes();
        RegisterObjectPool();

        isPlaying = true;
        isFinished = false;
        isStoppingAfterPlay = false;
        elapsedTime = 0f;
        spawnTimer = Mathf.Max(spawnInterval, 0f);
    }

    public void StopPlay()
    {
        isPlaying = false;

        if (activePipes.Count == 0)
        {
            CompleteStopPlay();
            return;
        }

        isStoppingAfterPlay = true;
    }

    private void OnDestroy()
    {
        isPlaying = false;
        isFinished = true;
        isStoppingAfterPlay = false;

        RecycleAllActivePipes();
        ClearObjectPool();

        OnSuccess = null;
        OnFail = null;
    }

    private void CacheScrollTargetPositions()
    {
        for (int i = 0; i < scrollTargets.Count; i++)
        {
            ScrollTargetData data = scrollTargets[i];
            if (data?.target == null)
            {
                continue;
            }

            data.initialPosition = data.useLocalPosition ? data.target.localPosition : data.target.position;
        }
    }

    private void ResetScrollTargets()
    {
        for (int i = 0; i < scrollTargets.Count; i++)
        {
            ScrollTargetData data = scrollTargets[i];
            if (data?.target == null)
            {
                continue;
            }

            if (data.useLocalPosition)
            {
                data.target.localPosition = data.initialPosition;
            }
            else
            {
                data.target.position = data.initialPosition;
            }
        }
    }

    private void UpdateScrollTargets(float deltaTime)
    {
        for (int i = 0; i < scrollTargets.Count; i++)
        {
            ScrollTargetData data = scrollTargets[i];
            if (data?.target == null)
            {
                continue;
            }

            Vector3 position = data.useLocalPosition ? data.target.localPosition : data.target.position;
            position.x -= data.moveSpeed * deltaTime;

            if (position.x <= data.resetThresholdX)
            {
                position.x += data.resetOffsetX;
            }

            if (data.useLocalPosition)
            {
                data.target.localPosition = position;
            }
            else
            {
                data.target.position = position;
            }
        }
    }

    private void UpdateActivePipes(float deltaTime)
    {
        for (int i = activePipes.Count - 1; i >= 0; i--)
        {
            Level56PipeObstacle pipe = activePipes[i];
            if (pipe == null)
            {
                activePipes.RemoveAt(i);
                continue;
            }

            pipe.Move(Vector3.left * (pipeMoveSpeed * deltaTime));
            if (pipe.transform.position.x <= pipeRecycleX)
            {
                RecyclePipe(pipe);
                activePipes.RemoveAt(i);
            }
        }
    }

    private void SpawnPipe()
    {
        Level56PipeObstacle pipe = GetPipeInstance();
        if (pipe == null)
        {
            return;
        }

        Vector3 spawnPosition = spawnRoot != null ? spawnRoot.position : transform.position;
        spawnPosition += pipeSpawnPositionOffset;
        spawnPosition.y += UnityEngine.Random.Range(pipeSpawnYOffsetRange.x, pipeSpawnYOffsetRange.y);

        pipe.Init(spawnPosition, spawnRoot);
        activePipes.Add(pipe);
    }

    private Level56PipeObstacle GetPipeInstance()
    {
        if (GetPipeSourceCount() == 0)
        {
            return null;
        }

        if (ObjectPoolManager.Instance != null && ObjectPoolManager.Instance.IsPoolRegistered<Level56PipeObstacle>())
        {
            Level56PipeObstacle pipeFromPool = ObjectPoolManager.Instance.Get<Level56PipeObstacle>();
            return pipeFromPool;
        }

        return CreatePipeInstance();
    }

    private Level56PipeObstacle CreatePipeInstance()
    {
        GameObject prefab = GetRandomPipeSource();
        if (prefab == null)
        {
            return null;
        }

        Transform parent = spawnRoot != null ? spawnRoot : transform;
        GameObject instance = Instantiate(prefab, parent);
        Level56PipeObstacle pipe = instance.GetComponent<Level56PipeObstacle>();
        if (pipe == null)
        {
            pipe = instance.AddComponent<Level56PipeObstacle>();
        }

        return pipe;
    }

    private void RegisterObjectPool()
    {
        if (GetPipeSourceCount() == 0 || ObjectPoolManager.Instance == null)
        {
            return;
        }

        if (ObjectPoolManager.Instance.IsPoolRegistered<Level56PipeObstacle>())
        {
            ObjectPoolManager.Instance.UnregisterPool<Level56PipeObstacle>();
        }

        ObjectPoolManager.Instance.RegisterPool(CreatePipeInstance, poolInitialSize, poolMaxSize);
        poolRegisteredBySelf = true;
    }

    private void ClearObjectPool()
    {
        if (!poolRegisteredBySelf || ObjectPoolManager.Instance == null)
        {
            return;
        }

        ObjectPoolManager.Instance.UnregisterPool<Level56PipeObstacle>();
        poolRegisteredBySelf = false;
    }

    private void CompleteStopPlay()
    {
        isPlaying = false;
        isFinished = false;
        isStoppingAfterPlay = false;
        ClearObjectPool();
    }

    private void ResolveSpawnRoot()
    {
        if (spawnRoot != null)
        {
            return;
        }

        spawnRoot = FindChildRecursive(transform, "Pipes");
    }

    private void CacheFallbackPipeSources()
    {
        if (pipePrefabs.Count > 0)
        {
            return;
        }

        fallbackPipeSources.Clear();
        if (spawnRoot == null)
        {
            return;
        }

        for (int i = 0; i < spawnRoot.childCount; i++)
        {
            Transform child = spawnRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            fallbackPipeSources.Add(child.gameObject);
            child.gameObject.SetActive(false);
        }
    }

    private int GetPipeSourceCount()
    {
        if (pipePrefabs.Count > 0)
        {
            return pipePrefabs.Count;
        }

        return fallbackPipeSources.Count;
    }

    private GameObject GetRandomPipeSource()
    {
        if (pipePrefabs.Count > 0)
        {
            int prefabIndex = UnityEngine.Random.Range(0, pipePrefabs.Count);
            return pipePrefabs[prefabIndex];
        }

        if (fallbackPipeSources.Count == 0)
        {
            return null;
        }

        int fallbackIndex = UnityEngine.Random.Range(0, fallbackPipeSources.Count);
        return fallbackPipeSources[fallbackIndex];
    }

    private void RecycleAllActivePipes()
    {
        for (int i = activePipes.Count - 1; i >= 0; i--)
        {
            RecyclePipe(activePipes[i]);
        }

        activePipes.Clear();
    }

    private void RecyclePipe(Level56PipeObstacle pipe)
    {
        if (pipe == null)
        {
            return;
        }

        pipe.RecycleSelf();
    }

    private void FinishGame(bool isSuccess)
    {
        if (isFinished)
        {
            return;
        }

        isFinished = true;
        isPlaying = false;

        if (isSuccess)
        {
            OnSuccess?.Invoke();
        }
        else
        {
            OnFail?.Invoke();
        }
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform nestedChild = FindChildRecursive(child, childName);
            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }
}

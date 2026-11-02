using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;

/// <summary>
/// 公共奖励飞行动画的实际播放结果；只有 Completed 表示整批奖励自然播放完成。
/// </summary>
public enum CommonUIRewardPlayResult
{
    Completed,
    Replaced,
    ControllerDisabled,
    ControllerDestroyed,
    InvalidRequest,
    NoItemsSpawned,
    Interrupted
}

public class CommonUIRewardAnimCtrl : MonoBehaviour
{
    private sealed class RewardPlaySession
    {
        public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
        public CommonUIRewardPlayResult StopResult = CommonUIRewardPlayResult.Interrupted;
    }

    private const string RewardItemResourcePath = "CommonUIRewardItem";

    [Header("Template")]
    [SerializeField] private CommonUIRewardItem rewardItem;
    [SerializeField] private RectTransform flyRoot;
    [SerializeField] private Sprite defaultRewardSprite;

    [Header("Path")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private int rewardCount = 8;
    [SerializeField] private float spawnInterval = 0.04f;
    [SerializeField] private float scatterRadiusMin = 40f;
    [SerializeField] private float scatterRadiusMax = 110f;
    [SerializeField] private float scatterUpBias = 40f;
    [SerializeField] private float curveHeightMin = 100f;
    [SerializeField] private float curveHeightMax = 180f;

    [Header("Timing")]
    [SerializeField] private float scatterDuration = 0.18f;
    [SerializeField] private float flyDurationMin = 0.55f;
    [SerializeField] private float flyDurationMax = 0.8f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Scale")]
    [SerializeField] private float startScale = 0.65f;
    [SerializeField] private float scatterScale = 1f;
    [SerializeField] private float endScale = 0.35f;

    [Header("Pool")]
    [SerializeField] private int initialPoolSize = 8;
    [SerializeField] private int maxPoolSize = 24;

    private readonly HashSet<CommonUIRewardItem> _activeItems = new HashSet<CommonUIRewardItem>();

    private RectTransform _flyRoot;
    private Canvas _canvas;
    private int _playVersion;
    private bool _isRegisteringPool;
    private RewardPlaySession _activeSession;
    private bool _isDestroyed;

    private void Awake()
    {
        ResolveRefs();
        EnsureRewardItemTemplate();
        RegisterPoolIfNeeded();

        if (rewardItem != null)
            rewardItem.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        StopAndRecycleAll(CommonUIRewardPlayResult.ControllerDisabled);
    }

    private void OnDestroy()
    {
        _isDestroyed = true;
        StopAndRecycleAll(CommonUIRewardPlayResult.ControllerDestroyed);

        if (TryGetPoolManager(out ObjectPoolManager poolManager))
            poolManager.UnregisterPool<CommonUIRewardItem>();
    }

    private void OnValidate()
    {
        rewardCount = Mathf.Max(1, rewardCount);
        spawnInterval = Mathf.Max(0f, spawnInterval);
        scatterRadiusMin = Mathf.Max(0f, scatterRadiusMin);
        scatterRadiusMax = Mathf.Max(scatterRadiusMin, scatterRadiusMax);
        curveHeightMin = Mathf.Max(0f, curveHeightMin);
        curveHeightMax = Mathf.Max(curveHeightMin, curveHeightMax);
        scatterDuration = Mathf.Max(0.01f, scatterDuration);
        flyDurationMin = Mathf.Max(0.01f, flyDurationMin);
        flyDurationMax = Mathf.Max(flyDurationMin, flyDurationMax);
        startScale = Mathf.Max(0.01f, startScale);
        scatterScale = Mathf.Max(0.01f, scatterScale);
        endScale = Mathf.Max(0.01f, endScale);
        initialPoolSize = Mathf.Max(0, initialPoolSize);
        maxPoolSize = Mathf.Max(initialPoolSize, maxPoolSize);

        ResolveRefs();
    }

    public UniTask<CommonUIRewardPlayResult> Play()
    {
        return Play(defaultRewardSprite, startPoint, endPoint, rewardCount);
    }

    public UniTask<CommonUIRewardPlayResult> PlayFrom(Transform from, int count = -1)
    {
        return Play(defaultRewardSprite, from, endPoint, count);
    }

    public UniTask<CommonUIRewardPlayResult> PlayFrom(Sprite rewardSprite, Transform from, int count = -1)
    {
        return Play(rewardSprite, from, endPoint, count);
    }

    public async UniTask<CommonUIRewardPlayResult> Play(Sprite rewardSprite, Transform from, Transform to, int count = -1)
    {
        // A new play request always replaces the previous one on this controller.
        // This prevents overlapping batches from mixing paths and pooled items.
        StopAndRecycleAll(CommonUIRewardPlayResult.Replaced);
        ResolveRefs();
        RegisterPoolIfNeeded();

        // Device IAP callbacks may return after the source UI is hidden or destroyed.
        // Log each prerequisite separately so we can see the exact skip reason on device.
        if (_isDestroyed)
        {
            Debug.Log("[CommonUIRewardAnimCtrl] Play skipped. Controller already destroyed.", this);
            return CommonUIRewardPlayResult.ControllerDestroyed;
        }

        if (!isActiveAndEnabled)
        {
            Debug.LogWarning("[CommonUIRewardAnimCtrl] Play skipped. Controller is inactive or disabled.", this);
            return CommonUIRewardPlayResult.ControllerDisabled;
        }

        if (rewardItem == null)
        {
            Debug.LogError("[CommonUIRewardAnimCtrl] Play skipped. Reward item template is missing.", this);
            return CommonUIRewardPlayResult.InvalidRequest;
        }

        if (_flyRoot == null)
        {
            Debug.LogError("[CommonUIRewardAnimCtrl] Play skipped. Fly root is missing.", this);
            return CommonUIRewardPlayResult.InvalidRequest;
        }

        if (from == null)
        {
            Debug.LogError("[CommonUIRewardAnimCtrl] Play skipped. Start transform is null.", this);
            return CommonUIRewardPlayResult.InvalidRequest;
        }

        if (to == null)
        {
            Debug.LogError("[CommonUIRewardAnimCtrl] Play skipped. End transform is null.", this);
            return CommonUIRewardPlayResult.InvalidRequest;
        }

        // Convert both endpoints to flyRoot local coordinates before spawning items.
        // If this conversion fails, the effect has no stable path to play on.
        if (!TryGetAnchoredPosition(from, out Vector2 startAnchoredPosition))
        {
            Debug.LogError(
                $"[CommonUIRewardAnimCtrl] Play skipped. Failed to resolve start anchored position. from:{from.name}, flyRoot:{_flyRoot.name}",
                this);
            return CommonUIRewardPlayResult.InvalidRequest;
        }

        if (!TryGetAnchoredPosition(to, out Vector2 endAnchoredPosition))
        {
            Debug.LogError(
                $"[CommonUIRewardAnimCtrl] Play skipped. Failed to resolve end anchored position. to:{to.name}, flyRoot:{_flyRoot.name}",
                this);
            return CommonUIRewardPlayResult.InvalidRequest;
        }

        // Version and cancellation token together guarantee that a later Play(),
        // OnDisable(), or OnDestroy() can stop this batch cleanly.
        int version = ++_playVersion;
        int resolvedCount = Mathf.Max(1, count > 0 ? count : rewardCount);
        List<UniTask<bool>> tasks = new List<UniTask<bool>>(resolvedCount);
        RewardPlaySession session = new RewardPlaySession();
        _activeSession = session;
        CancellationToken token = session.Cancellation.Token;
        CommonUIRewardPlayResult result = CommonUIRewardPlayResult.Interrupted;

        Debug.Log(
            $"[CommonUIRewardAnimCtrl] Play started. version:{version}, count:{resolvedCount}, from:{from.name}, to:{to.name}",
            this);

        try
        {
            for (int i = 0; i < resolvedCount; i++)
            {
                token.ThrowIfCancellationRequested();

                if (version != _playVersion)
                {
                    result = session.StopResult;
                    break;
                }

                if (this == null || !isActiveAndEnabled)
                {
                    result = CommonUIRewardPlayResult.ControllerDisabled;
                    break;
                }

                CommonUIRewardItem item = SpawnRewardItem(rewardSprite);
                if (item != null)
                {
                    tasks.Add(PlaySingleItemAndRecycle(item, startAnchoredPosition, endAnchoredPosition));
                }
                else
                {
                    Debug.LogError("[CommonUIRewardAnimCtrl] Play skipped one reward item. SpawnRewardItem returned null.", this);
                }

                if (spawnInterval > 0f && i < resolvedCount - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(spawnInterval), ignoreTimeScale: useUnscaledTime, cancellationToken: token);
                }
            }

            if (tasks.Count > 0)
            {
                bool[] itemResults = await UniTask.WhenAll(tasks);
                if (token.IsCancellationRequested || version != _playVersion)
                {
                    result = session.StopResult;
                }
                else if (tasks.Count != resolvedCount || Array.Exists(itemResults, completed => !completed))
                {
                    result = CommonUIRewardPlayResult.Interrupted;
                }
                else
                {
                    result = CommonUIRewardPlayResult.Completed;
                }
            }
            else
            {
                result = CommonUIRewardPlayResult.NoItemsSpawned;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            result = session.StopResult;
        }
        finally
        {
            if (ReferenceEquals(_activeSession, session))
                _activeSession = null;

            session.Cancellation.Dispose();
        }

        LogPlayResult(version, resolvedCount, tasks.Count, result);
        return result;
    }

    private async UniTask<bool> PlaySingleItemAndRecycle(CommonUIRewardItem item, Vector2 startAnchoredPosition, Vector2 endAnchoredPosition)
    {
        try
        {
            Vector2 scatterAnchoredPosition = startAnchoredPosition + GetScatterOffset();
            Vector2 controlAnchoredPosition = Vector2.Lerp(scatterAnchoredPosition, endAnchoredPosition, 0.5f)
                + Vector2.up * UnityEngine.Random.Range(curveHeightMin, curveHeightMax);

            return await item.PlayAsync(
                startAnchoredPosition,
                scatterAnchoredPosition,
                controlAnchoredPosition,
                endAnchoredPosition,
                scatterDuration,
                UnityEngine.Random.Range(flyDurationMin, flyDurationMax),
                startScale,
                scatterScale,
                endScale,
                useUnscaledTime);
        }
        finally
        {
            RecycleItem(item);
        }
    }

    private CommonUIRewardItem SpawnRewardItem(Sprite rewardSprite)
    {
        CommonUIRewardItem item = null;
        if (TryGetPoolManager(out ObjectPoolManager poolManager)
            && poolManager.IsPoolRegistered<CommonUIRewardItem>())
        {
            item = poolManager.Get<CommonUIRewardItem>();
        }

        if (item == null && rewardItem != null)
        {
            item = Instantiate(rewardItem, _flyRoot);
            item.gameObject.SetActive(true);
        }

        if (item == null)
            return null;

        item.transform.SetParent(_flyRoot, false);
        item.transform.SetAsLastSibling();
        item.SetIcon(rewardSprite != null ? rewardSprite : defaultRewardSprite);
        _activeItems.Add(item);
        return item;
    }

    private void RecycleItem(CommonUIRewardItem item)
    {
        if (item == null || !_activeItems.Remove(item))
            return;

        if (TryGetPoolManager(out ObjectPoolManager poolManager)
            && poolManager.IsPoolRegistered<CommonUIRewardItem>())
        {
            poolManager.Recycle(item);
        }
        else
        {
            Destroy(item.gameObject);
        }
    }

    private void StopAndRecycleAll(CommonUIRewardPlayResult stopResult)
    {
        CancelActivePlay(stopResult);
        _playVersion++;
        if (_activeItems.Count == 0)
            return;

        CommonUIRewardItem[] items = new CommonUIRewardItem[_activeItems.Count];
        _activeItems.CopyTo(items);
        _activeItems.Clear();

        for (int i = 0; i < items.Length; i++)
        {
            CommonUIRewardItem item = items[i];
            if (item == null)
                continue;

            if (TryGetPoolManager(out ObjectPoolManager poolManager)
                && poolManager.IsPoolRegistered<CommonUIRewardItem>())
            {
                poolManager.Recycle(item);
            }
            else
            {
                Destroy(item.gameObject);
            }
        }
    }

    private void ResolveRefs()
    {
        if (_flyRoot == null)
            _flyRoot = flyRoot != null ? flyRoot : transform as RectTransform;

        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();

        if (rewardItem == null)
            rewardItem = GetComponentInChildren<CommonUIRewardItem>(true);
    }

    private void EnsureRewardItemTemplate()
    {
        if (rewardItem != null)
            return;

        GameObject rewardItemPrefab = Resources.Load<GameObject>(RewardItemResourcePath);
        if (rewardItemPrefab == null)
        {
            Debug.LogError($"[CommonUIRewardAnimCtrl] Failed to load reward item prefab from Resources/{RewardItemResourcePath}.", this);
            return;
        }

        rewardItem = rewardItemPrefab.GetComponent<CommonUIRewardItem>();
        if (rewardItem == null)
        {
            Debug.LogError($"[CommonUIRewardAnimCtrl] Loaded prefab from Resources/{RewardItemResourcePath} but CommonUIRewardItem is missing.", this);
        }
    }

    private void RegisterPoolIfNeeded()
    {
        if (_isRegisteringPool || rewardItem == null)
            return;

        if (!TryGetPoolManager(out ObjectPoolManager poolManager))
            return;

        if (poolManager.IsPoolRegistered<CommonUIRewardItem>())
            return;

        _isRegisteringPool = true;
        try
        {
            poolManager.RegisterPool(
                () =>
                {
                    CommonUIRewardItem instance = Instantiate(rewardItem, _flyRoot);
                    instance.gameObject.name = rewardItem.gameObject.name;
                    return instance;
                },
                initialPoolSize,
                maxPoolSize);
        }
        finally
        {
            _isRegisteringPool = false;
        }
    }

    private Vector2 GetScatterOffset()
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float radius = UnityEngine.Random.Range(scatterRadiusMin, scatterRadiusMax);
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        offset.y += scatterUpBias;
        return offset;
    }

    private bool TryGetAnchoredPosition(Transform point, out Vector2 anchoredPosition)
    {
        anchoredPosition = default;
        if (point == null || _flyRoot == null)
            return false;

        Camera camera = null;
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            camera = _canvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, point.position);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(_flyRoot, screenPoint, camera, out anchoredPosition);
    }

    private void CancelActivePlay(CommonUIRewardPlayResult stopResult)
    {
        if (_activeSession == null)
            return;

        _activeSession.StopResult = stopResult;
        if (_activeSession.Cancellation.IsCancellationRequested)
            return;

        _activeSession.Cancellation.Cancel();
    }

    private void LogPlayResult(
        int version,
        int requestedCount,
        int spawnedCount,
        CommonUIRewardPlayResult result)
    {
        string message =
            $"[CommonUIRewardAnimCtrl] Play finished. version:{version}, result:{result}, requested:{requestedCount}, spawned:{spawnedCount}";
        if (result == CommonUIRewardPlayResult.Completed)
            Debug.Log(message, this);
        else
            Debug.LogWarning(message, this);
    }

    private static bool TryGetPoolManager(out ObjectPoolManager poolManager)
    {
        if (!ObjectPoolManager.IsInitialized)
        {
            poolManager = null;
            return false;
        }

        poolManager = ObjectPoolManager.Instance;
        return poolManager != null;
    }
}

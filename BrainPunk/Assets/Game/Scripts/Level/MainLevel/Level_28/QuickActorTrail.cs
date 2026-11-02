using Game.Common;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// 角色 Spine 残影拖尾：按间隔生成残影实例，同步当前动画帧并做透明度渐变，使用对象池优化。
/// </summary>
public class QuickActorTrail : MonoBehaviour
{
    [Header("源")]
    [SerializeField] private Transform sourceTransform;
    [SerializeField] private SkeletonAnimation sourceSkeleton;

    [Header("残影预制体")]
    [Tooltip("需挂 QuickActorTrailItem，且 Skeleton 与源一致")]
    [SerializeField] private GameObject trailPrefab;

    [Header("生成")]
    [Tooltip("每隔多少秒生成一条残影")]
    [SerializeField] private float spawnInterval = 0.06f;
    [Tooltip("残影从 1 到 0 的渐变时长")]
    [SerializeField] private float fadeDuration = 0.25f;
    [Tooltip("最小位移阈值（小于该距离视为未移动，不生成拖尾）")]
    [SerializeField] private float minMoveDistance = 0.01f;

    [Header("对象池（可选）")]
    [Tooltip("场景内需有 ObjectPoolManager 才生效")]
    [SerializeField] private int poolInitialSize = 8;
    [SerializeField] private int poolMaxSize = 24;

    [Header("开关")]
    [SerializeField] private bool trailActive = true;

    private Transform _trailContainer;
    private float _nextSpawnTime;
    private bool _poolRegistered;
    private Vector3 _lastSourcePos;

    private void Awake()
    {
        if (sourceTransform == null)
            sourceTransform = transform;
        if (sourceSkeleton == null)
            sourceSkeleton = GetComponentInChildren<SkeletonAnimation>();
        _lastSourcePos = sourceTransform != null ? sourceTransform.position : Vector3.zero;

        GameObject container = new GameObject("TrailContainer");
        container.transform.SetParent(transform);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        container.transform.localScale = Vector3.one;
        _trailContainer = container.transform;

        if (trailPrefab != null && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.RegisterPool(CreateTrailItem, poolInitialSize, poolMaxSize);
            _poolRegistered = true;
        }
    }

    private QuickActorTrailItem CreateTrailItem()
    {
        if (trailPrefab == null) return null;
        GameObject go = Instantiate(trailPrefab, _trailContainer);
        return go.GetComponent<QuickActorTrailItem>();
    }

    private void FixedUpdate()
    {
        if (!trailActive || sourceTransform == null || sourceSkeleton == null || trailPrefab == null)
            return;

        Vector3 currentPos = sourceTransform.position;
        float minMoveSqr = minMoveDistance * minMoveDistance;
        if ((currentPos - _lastSourcePos).sqrMagnitude < minMoveSqr)
            return;
        _lastSourcePos = currentPos;

        if (Time.time < _nextSpawnTime)
            return;

        _nextSpawnTime = Time.time + spawnInterval;

        string animName = GetSourceAnimName();
        float trackTime = GetSourceTrackTime();
        bool isLoop = GetSourceIsLoop();

        QuickActorTrailItem item = null;
        if (_poolRegistered && ObjectPoolManager.Instance != null)
            item = ObjectPoolManager.Instance.Get<QuickActorTrailItem>();

        if (item == null)
        {
            item = CreateTrailItem();
            if (item == null) return;
            item.transform.SetParent(_trailContainer);
        }
        else
        {
            item.transform.SetParent(_trailContainer);
        }

        item.Setup(sourceTransform.position, sourceTransform.rotation, animName, trackTime, isLoop, fadeDuration);
    }

    private string GetSourceAnimName()
    {
        if (sourceSkeleton == null || sourceSkeleton.AnimationState == null)
            return "";
        Spine.TrackEntry entry = sourceSkeleton.AnimationState.GetCurrent(0);
        return entry?.Animation?.Name ?? "";
    }

    private float GetSourceTrackTime()
    {
        if (sourceSkeleton == null || sourceSkeleton.AnimationState == null)
            return 0f;
        Spine.TrackEntry entry = sourceSkeleton.AnimationState.GetCurrent(0);
        return entry?.TrackTime ?? 0f;
    }

    private bool GetSourceIsLoop()
    {
        if (sourceSkeleton == null || sourceSkeleton.AnimationState == null)
            return false;
        Spine.TrackEntry entry = sourceSkeleton.AnimationState.GetCurrent(0);
        return entry != null && entry.Loop;
    }

    /// <summary>
    /// 开启/关闭残影生成
    /// </summary>
    public void SetTrailActive(bool active)
    {
        trailActive = active;
    }

    private void OnDestroy()
    {
        if (_trailContainer != null)
        {
            QuickActorTrailItem[] items = _trailContainer.GetComponentsInChildren<QuickActorTrailItem>();
            foreach (var item in items)
            {
                if (item != null)
                {
                    item.OnRecycle();
                    Destroy(item.gameObject);
                }
            }
        }

        if (_poolRegistered && ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.ClearPool<QuickActorTrailItem>();
    }
}

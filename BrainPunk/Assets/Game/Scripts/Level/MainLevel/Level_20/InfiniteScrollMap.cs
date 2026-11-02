using UnityEngine;

/// <summary>
/// 无限滚动地图：根据跟随物体位置，用 currentTile / cacheTile 两格循环复用
/// Tile Array 至少 2 个；物体 1 初始在 direction(0,0)，物体 2 初始隐藏；超出 DetectRange * tileSize 时 cacheTile 移到边界并交换
/// </summary>
public class InfiniteScrollMap : MonoBehaviour
{
    public enum ScrollDirection
    {
        Horizontal,
        Vertical
    }

    [Header("方向")]
    [SerializeField] private ScrollDirection direction = ScrollDirection.Horizontal;

    [Header("Tile 配置")]
    [Tooltip("单格地图长度（世界单位）")]
    [SerializeField] private float tileSize = 10f;

    [Tooltip("检测距离总长 = DetectRange * tileSize，以当前格中心为基准左右各一半，超出则移格")]
    [SerializeField] private float detectRange = 0.5f;

    [Header("Tile 数组")]
    [Tooltip("至少 2 个：索引 0 为初始 currentTile，索引 1 为 cacheTile")]
    [SerializeField] private Transform[] tileArray = new Transform[2];

    [Header("跟随物体")]
    [Tooltip("用该物体在方向上的位置决定何时移格")]
    [SerializeField] private Transform followObject;

    [SerializeField] private bool ShowGizmos;

    /// <summary> 当前所在格（跟随物体所在的 tile） </summary>
    private Transform currentTile;

    /// <summary> 可移动的缓存格（移出视界时被挪到另一侧） </summary>
    private Transform cacheTile;

    /// <summary> 当前格在滚动轴上的起点位置（世界坐标） </summary>
    private float currentTilePosition;

    private int _currentIndex;
    private int _cacheIndex;

    void Awake()
    {
        ValidateAndInitTiles();
    }

    void Start()
    {
        InitTilePositions();
    }

    void LateUpdate()
    {
        if (followObject == null || currentTile == null || cacheTile == null) return;
        if (tileSize <= 0f) return;

        float followPos = GetAxisPosition(followObject.position);
        float tileCenter = currentTilePosition;
        float halfDetect = (detectRange * tileSize) * 0.5f; // 总长 detectRange*tileSize，左右各一半

        // 以当前格中心为基准：向右超出 center + halfDetect 则移到下一格
        if (followPos >= tileCenter + halfDetect)
        {
            MoveCacheToPosition(currentTilePosition + tileSize);
            currentTilePosition += tileSize;
            SwapCurrentAndCache();
        }
        // 向左超出 center - halfDetect 则移到上一格
        else if (followPos < tileCenter - halfDetect)
        {
            MoveCacheToPosition(currentTilePosition - tileSize);
            currentTilePosition -= tileSize;
            SwapCurrentAndCache();
        }
    }

    private void ValidateAndInitTiles()
    {
        if (tileArray == null || tileArray.Length < 2)
        {
            Debug.LogWarning("[InfiniteScrollMap] tileArray 至少需要 2 个物体。");
            return;
        }

        if (tileArray[0] == null || tileArray[1] == null)
        {
            Debug.LogWarning("[InfiniteScrollMap] tileArray 中不能有空引用。");
            return;
        }

        currentTile = tileArray[0];
        cacheTile = tileArray[1];
        _currentIndex = 0;
        _cacheIndex = 1;
        currentTilePosition = 0f;
    }

    /// <summary>
    /// 初始化：物体 1 在 direction(0,0)，物体 2 隐藏
    /// </summary>
    private void InitTilePositions()
    {
        if (currentTile == null || cacheTile == null) return;

        SetAxisPosition(currentTile, 0f);
        currentTilePosition = 0f;
        currentTile.gameObject.SetActive(true);
        cacheTile.gameObject.SetActive(false);
    }

    private float GetAxisPosition(Vector3 worldPos)
    {
        return direction == ScrollDirection.Horizontal ? worldPos.x : worldPos.y;
    }

    private void SetAxisPosition(Transform t, float axisValue)
    {
        Vector3 p = t.position;
        if (direction == ScrollDirection.Horizontal)
            p.x = axisValue;
        else
            p.y = axisValue;
        t.position = p;
    }

    /// <summary>
    /// 将 cacheTile 移到指定轴位置（只改滚动轴）
    /// </summary>
    private void MoveCacheToPosition(float axisPosition)
    {
        cacheTile.gameObject.SetActive(true);
        SetAxisPosition(cacheTile, axisPosition);
    }

    /// <summary>
    /// 交换 currentTile 与 cacheTile 的引用
    /// </summary>
    private void SwapCurrentAndCache()
    {
        Transform t = currentTile;
        currentTile = cacheTile;
        cacheTile = t;

        int i = _currentIndex;
        _currentIndex = _cacheIndex;
        _cacheIndex = i;
    }

    public void SetDirection(ScrollDirection newDirection)
    {
        direction = newDirection;
    }

    public void SetFollowObject(Transform follow)
    {
        followObject = follow;
    }

    public void SetTileSize(float size)
    {
        tileSize = size;
    }

    public void SetDetectRange(float range)
    {
        detectRange = range;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (tileSize <= 0f || !ShowGizmos) return;

        // basePos = currentTile 的位置（未运行时用 tileArray[0]）
        Transform tile = currentTile != null ? currentTile : 
        (tileArray != null && tileArray.Length > 0 ? tileArray[0] : null);
        if (tile == null) return;

        Vector3 basePos = tile.position;
        float halfTile = tileSize * 0.5f;
        float detectTotal = detectRange * tileSize; // 检测距离总长，以格中心左右各一半

        // 格中心 = currentTile 位置 + 半格
        Vector3 centerWorld = basePos;

        Gizmos.color = Color.yellow;
        Vector3 size = direction == ScrollDirection.Horizontal
            ? new Vector3(detectTotal, 0.1f, 0.1f)
            : new Vector3(0.1f, detectTotal, 0.1f);
        Gizmos.DrawWireCube(centerWorld, size);
    }
#endif
}

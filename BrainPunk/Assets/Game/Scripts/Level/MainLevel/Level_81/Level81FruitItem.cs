using UnityEngine;

/// <summary>
/// Level081 单个抛掷物：负责水果/炸弹飞行、火花帧和线段命中判定。
/// </summary>
public sealed class Level81FruitItem : MonoBehaviour
{
    #region Runtime State

    private SpriteRenderer itemRenderer;
    private SpriteRenderer sparkRenderer;
    private Rigidbody2D itemBody;
    private Collider2D itemCollider;
    private Sprite[] sparkFrames;
    private float sparkFrameDuration;
    private float nextSparkFrameTime;
    private float killWorldY;
    private float hitRadiusScale;
    private float lifeRemaining;
    private int sparkFrameIndex;
    private bool simulationPaused;
    private Vector2 pausedVelocity;
    private float pausedAngularVelocity;

    #endregion

    #region Public State

    public bool IsBomb { get; private set; }
    public bool IsResolved { get; private set; }
    public Color JuiceColor { get; private set; }
    public SpriteRenderer Renderer => itemRenderer;
    public Rigidbody2D Body => itemBody;

    #endregion

    #region Initialization

    /// <summary>
    /// 初始化本轮抛掷物；所有参数仅作用于当前运行时实例。
    /// </summary>
    public void Initialize(
        Sprite sprite,
        Color juiceColor,
        bool isBomb,
        Sprite[] bombSparkFrames,
        Vector2 velocity,
        float gravityScale,
        float angularVelocity,
        float uniformScale,
        float worldKillY,
        float segmentHitRadiusScale,
        float bombSparkFramesPerSecond,
        int sortingOrder)
    {
        IsBomb = isBomb;
        IsResolved = false;
        JuiceColor = juiceColor;
        killWorldY = worldKillY;
        hitRadiusScale = Mathf.Clamp(segmentHitRadiusScale, 0.2f, 1.5f);
        lifeRemaining = 8f;
        transform.localScale = Vector3.one * Mathf.Max(0.01f, uniformScale);

        itemRenderer = gameObject.AddComponent<SpriteRenderer>();
        itemRenderer.sprite = sprite;
        // LevelItem 不是项目中的 SortingLayer；抛掷物应与场景美术保持在 SceneLayer。
        itemRenderer.sortingLayerID = SortingLayer.NameToID("SceneLayer");
        itemRenderer.sortingOrder = sortingOrder;

        itemCollider = gameObject.AddComponent<CircleCollider2D>();
        itemCollider.isTrigger = true;

        itemBody = gameObject.AddComponent<Rigidbody2D>();
        itemBody.gravityScale = Mathf.Max(0f, gravityScale);
        itemBody.linearVelocity = velocity;
        itemBody.angularVelocity = angularVelocity;
        itemBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        itemBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        sparkFrames = bombSparkFrames;
        sparkFrameDuration = 1f / Mathf.Max(1f, bombSparkFramesPerSecond);
        nextSparkFrameTime = Time.unscaledTime + sparkFrameDuration;
        if (IsBomb)
            CreateBombSpark(sortingOrder + 1);
    }

    private void CreateBombSpark(int sortingOrder)
    {
        if (sparkFrames == null || sparkFrames.Length == 0)
            return;

        GameObject sparkObject = new GameObject("FuseSpark");
        sparkObject.transform.SetParent(transform, false);
        sparkObject.transform.localPosition = new Vector3(0.82f, 0.76f, 0f);
        sparkObject.transform.localScale = Vector3.one * 0.78f;
        sparkRenderer = sparkObject.AddComponent<SpriteRenderer>();
        sparkRenderer.sprite = sparkFrames[0];
        sparkRenderer.sortingLayerID = itemRenderer.sortingLayerID;
        sparkRenderer.sortingOrder = sortingOrder;
    }

    #endregion

    #region Slice Detection

    /// <summary>
    /// 判断世界空间滑动线段是否进入当前物体的候选命中范围。
    /// 真正切片成功后由调用方通过 ConfirmSlice 封锁二次命中。
    /// </summary>
    public bool TryGetSliceHit(
        Vector2 worldStart,
        Vector2 worldEnd,
        float bladeRadius,
        out Vector2 hitPosition)
    {
        hitPosition = transform.position;
        if (IsResolved || itemRenderer == null || !itemRenderer.enabled)
            return false;

        Bounds bounds = itemRenderer.bounds;
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.y) * hitRadiusScale +
                       Mathf.Max(0f, bladeRadius);
        Vector2 center = bounds.center;
        hitPosition = ClosestPointOnSegment(center, worldStart, worldEnd);
        if ((hitPosition - center).sqrMagnitude > radius * radius)
            return false;

        return true;
    }

    /// <summary>
    /// 在可见切片已成功创建后确认本物体被切中。
    /// </summary>
    public bool ConfirmSlice()
    {
        if (IsResolved)
            return false;

        Resolve();
        return true;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= Mathf.Epsilon)
            return start;

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
        return start + segment * t;
    }

    private void Resolve()
    {
        IsResolved = true;
        if (itemCollider != null)
            itemCollider.enabled = false;
        if (itemBody != null)
            itemBody.simulated = false;
    }

    #endregion

    #region Pause And Cleanup

    public void SetSimulationPaused(bool paused)
    {
        if (itemBody == null || IsResolved || simulationPaused == paused)
            return;

        simulationPaused = paused;
        if (paused)
        {
            pausedVelocity = itemBody.linearVelocity;
            pausedAngularVelocity = itemBody.angularVelocity;
            itemBody.simulated = false;
        }
        else
        {
            itemBody.simulated = true;
            itemBody.linearVelocity = pausedVelocity;
            itemBody.angularVelocity = pausedAngularVelocity;
        }
    }

    public void Discard()
    {
        if (!IsResolved)
            Resolve();

        Destroy(gameObject);
    }

    #endregion

    #region Lifecycle

    private void Update()
    {
        if (IsResolved || simulationPaused)
            return;

        lifeRemaining -= Time.deltaTime;
        if ((transform.position.y <= killWorldY &&
             itemBody != null &&
             itemBody.linearVelocityY < 0f) ||
            lifeRemaining <= 0f)
        {
            Resolve();
            Destroy(gameObject);
            return;
        }

        if (sparkRenderer == null || sparkFrames == null || sparkFrames.Length == 0)
            return;

        if (Time.unscaledTime < nextSparkFrameTime)
            return;

        sparkFrameIndex = (sparkFrameIndex + 1) % sparkFrames.Length;
        sparkRenderer.sprite = sparkFrames[sparkFrameIndex];
        nextSparkFrameTime = Time.unscaledTime + sparkFrameDuration;
    }

    #endregion
}

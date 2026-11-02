using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum Level131ObstacleDirection
{
    Bottom = 0,
    Top = 1,
}

/// <summary>
/// Level 131 的一根固定宽度纸面障碍柱。
/// 使用主关卡 Prefab 中预先裁切的完整 Ground_short 1 视觉模板，保留两侧撕边与原始 Sprite 尺寸；
/// 以当前 Sprite 物理轮廓建立 Trigger，旧外层盒子仅作禁用的布局包络。
/// 不修改已序列化的素材位置、尺寸或间距。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level131ObstacleColumn : MonoBehaviour
{
    private readonly List<Renderer> visualRenderers = new();
    private readonly List<PolygonCollider2D> shapeColliders = new();
    private readonly List<Vector2> shapePoints = new();

    [SerializeField] private Level131ObstacleDirection direction;
    [Tooltip("历史序列化兼容值；碰撞和拼接范围现在由素材轮廓生成，不使用此宽度。")]
    [SerializeField, Min(0.01f)] private float serializedTargetWidth = 1.5f;
    [Tooltip("历史序列化兼容值；不再用统一尖刺高度生成伤害盒。")]
    [SerializeField, Min(0f)] private float serializedSpikeHeight = 0.34f;
    [SerializeField] private int serializedSortingOrder = 180;

    private Level131Ctrl owner;
    private BoxCollider2D hitCollider;
    private Transform groundShortTemplate;
    private bool isBroken;

    public Level131ObstacleDirection Direction => direction;

    public bool IsBottom => direction == Level131ObstacleDirection.Bottom;

    public bool IsActive => !isBroken && gameObject.activeInHierarchy && shapeColliders.Count > 0;

    public Vector3 Center => WorldBounds.center;

    public Bounds WorldBounds => GetLayoutBounds(null);

    public void ResolveDebrisSorting(out int sortingLayerId, out int sortingOrder)
    {
        // 初始化将柱体、尖刺及纸面组统一到 SceneLayer；使用本实例缓存的实际 Renderer 排序。
        sortingLayerId = SortingLayer.NameToID("SceneLayer");
        sortingOrder = serializedSortingOrder;
        foreach (Renderer renderer in visualRenderers)
        {
            if (renderer != null && renderer.sortingLayerID == sortingLayerId)
                sortingOrder = Mathf.Max(sortingOrder, renderer.sortingOrder);
        }
        // 与 127 相同：碎片放在被破坏对象的前两级，不改柱体自身排序。
        sortingOrder += 2;
    }

    public void InitializeSerialized(Level131Ctrl levelOwner)
    {
        owner = levelOwner;
        isBroken = false;

        int triggerLayer = LayerMask.NameToLayer("TriggerItem");
        if (triggerLayer >= 0)
            gameObject.layer = triggerLayer;

        groundShortTemplate = FindChildRecursive(transform, "GroundShort1Cropped") ??
                               FindChildRecursive(transform, "Ground_short 1");
        visualRenderers.Clear();

        if (groundShortTemplate != null)
        {
            foreach (Collider2D collider in groundShortTemplate.GetComponentsInChildren<Collider2D>(true))
            {
                if (collider != null)
                    collider.enabled = false;
            }

            SortingGroup sortingGroup = groundShortTemplate.GetComponent<SortingGroup>();
            if (sortingGroup != null)
            {
                sortingGroup.sortingLayerID = SortingLayer.NameToID("SceneLayer");
                sortingGroup.sortingOrder = serializedSortingOrder;
            }
        }

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || visualRenderers.Contains(renderer))
                continue;

            renderer.sortingLayerID = SortingLayer.NameToID("SceneLayer");
            renderer.sortingOrder = renderer.transform.name.StartsWith("Spike_")
                ? serializedSortingOrder + 1
                : serializedSortingOrder;
            visualRenderers.Add(renderer);
        }

        hitCollider = GetComponent<BoxCollider2D>();
        if (hitCollider == null)
            hitCollider = gameObject.AddComponent<BoxCollider2D>();

        // 不能再用统一大盒子产生 Trigger；它曾比 05/07 的素材多伸出 1.11 单位。
        hitCollider.isTrigger = true;
        hitCollider.enabled = false;
        BuildSpriteColliders();
        gameObject.SetActive(true);
    }

    private void BuildSpriteColliders()
    {
        // 完整模板会复制这些组件；按同一 Renderer 顺序复用，避免每次实例化叠加碰撞体。
        PolygonCollider2D[] existing = GetComponents<PolygonCollider2D>();
        foreach (PolygonCollider2D collider in existing)
            collider.enabled = false;
        shapeColliders.Clear();
        Bounds layoutBounds = default;
        bool hasBounds = false;

        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sprite == null || !renderer.enabled || !IsLocallyActive(renderer.transform))
                continue;

            // 本关柱体全部为 Simple Sprite；不把未知平铺图静默当作未缩放的原图碰撞。
            if (renderer.drawMode != SpriteDrawMode.Simple)
            {
                Debug.LogError($"Level 131 刺柱素材需要 Simple Sprite 碰撞轮廓：{renderer.name}", this);
                continue;
            }

            Sprite sprite = renderer.sprite;
            int shapeCount = sprite.GetPhysicsShapeCount();
            Vector2[] vertices = shapeCount == 0 ? sprite.vertices : null;
            ushort[] triangles = shapeCount == 0 ? sprite.triangles : null;
            int pathCount = shapeCount > 0 ? shapeCount : triangles.Length / 3;
            if (pathCount == 0)
            {
                Debug.LogError($"Level 131 刺柱素材缺少物理轮廓和网格：{renderer.name}", this);
                continue;
            }

            int colliderIndex = shapeColliders.Count;
            PolygonCollider2D polygon = colliderIndex < existing.Length
                ? existing[colliderIndex]
                : gameObject.AddComponent<PolygonCollider2D>();
            polygon.enabled = false;
            polygon.offset = Vector2.zero;
            polygon.isTrigger = true;
            polygon.pathCount = pathCount;
            Matrix4x4 toColumn = transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;

            for (int path = 0; path < pathCount; path++)
            {
                shapePoints.Clear();
                if (shapeCount > 0)
                    sprite.GetPhysicsShape(path, shapePoints);
                else
                {
                    // 无自定义轮廓时使用实际 Sprite 网格三角形，不退回外接大矩形。
                    for (int corner = 0; corner < 3; corner++)
                        shapePoints.Add(vertices[triangles[path * 3 + corner]]);
                }

                for (int pointIndex = 0; pointIndex < shapePoints.Count; pointIndex++)
                {
                    Vector2 point = shapePoints[pointIndex];
                    if (renderer.flipX) point.x = -point.x;
                    if (renderer.flipY) point.y = -point.y;
                    point = toColumn.MultiplyPoint3x4(point);
                    shapePoints[pointIndex] = point;
                    if (!hasBounds)
                    {
                        layoutBounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                        layoutBounds.Encapsulate(point);
                }
                polygon.SetPath(path, shapePoints);
            }
            polygon.enabled = true;
            shapeColliders.Add(polygon);
        }

        if (hasBounds)
        {
            hitCollider.offset = layoutBounds.center;
            hitCollider.size = layoutBounds.size;
        }
    }

    private bool IsLocallyActive(Transform child)
    {
        // 模板父根是 inactive，但模板内部原本隐藏的 Sprite 仍不应生成碰撞。
        for (Transform current = child; current != null && current != transform; current = current.parent)
        {
            if (!current.gameObject.activeSelf)
                return false;
        }
        return true;
    }

    public bool Overlaps(Collider2D other)
    {
        return TryGetHitPoint(other, out _);
    }

    public bool IsCollisionShape(Collider2D collider)
    {
        return collider is PolygonCollider2D polygon && shapeColliders.Contains(polygon);
    }

    public bool TryGetHitPoint(Collider2D other, out Vector3 hitPoint)
    {
        hitPoint = transform.position;
        if (!IsActive || other == null || !other.enabled || !other.gameObject.activeInHierarchy)
            return false;

        Bounds bounds = WorldBounds;
        Bounds otherBounds = other.bounds;
        // 包络仅作 XY 粗筛；2D 碰撞不应被视觉 Z 分层影响，也不能仅凭包络相交判死。
        if (bounds.max.x < otherBounds.min.x || bounds.min.x > otherBounds.max.x ||
            bounds.max.y < otherBounds.min.y || bounds.min.y > otherBounds.max.y)
            return false;

        foreach (PolygonCollider2D collider in shapeColliders)
        {
            if (collider == null || !collider.enabled)
                continue;
            ColliderDistance2D separation = collider.Distance(other);
            if (separation.isValid && separation.distance <= 0f)
            {
                Vector2 contact = (separation.pointA + separation.pointB) * 0.5f;
                hitPoint = new Vector3(contact.x, contact.y, other.transform.position.z);
                return true;
            }
        }
        return false;
    }

    public Bounds GetLayoutBounds(Transform relativeTo)
    {
        BoxCollider2D collider = hitCollider != null ? hitCollider : GetComponent<BoxCollider2D>();
        if (collider == null)
            return new Bounds(ToRelativePoint(relativeTo, transform.position), Vector3.zero);

        // 初始化已用素材轮廓更新这个禁用的包络；不依赖 inactive 模板的物理 bounds。
        Vector2 halfSize = collider.size * 0.5f;
        Vector2 center = collider.offset;
        Bounds bounds = new(ToRelativePoint(relativeTo,
            collider.transform.TransformPoint(new Vector3(center.x, center.y, 0f))), Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                Vector3 corner = new(center.x + halfSize.x * x, center.y + halfSize.y * y, 0f);
                bounds.Encapsulate(ToRelativePoint(relativeTo, collider.transform.TransformPoint(corner)));
            }
        }

        return bounds;
    }

    private static Vector3 ToRelativePoint(Transform relativeTo, Vector3 worldPoint)
    {
        return relativeTo != null ? relativeTo.InverseTransformPoint(worldPoint) : worldPoint;
    }

    public void SetGameplayVisible(bool visible)
    {
        if (isBroken && visible)
            return;

        gameObject.SetActive(visible);
        foreach (PolygonCollider2D collider in shapeColliders)
            collider.enabled = visible && !isBroken;
    }

    public bool TryMarkBroken()
    {
        if (!IsActive)
            return false;

        isBroken = true;
        // 一次停用整棵子树即可停止显示和碰撞，不再逐组件禁用后又重复停用。
        // 保留完整实例；owner 仅在整批离开镜头和运动主体后，移动到前方并恢复。
        gameObject.SetActive(false);
        return true;
    }

    /// <summary>
    /// 仅供本关两批循环器在离屏并完成前移后调用；恢复破坏状态和已缓存的碰撞体。
    /// 不重建 Sprite 轮廓、不添加组件，也不能用于屏幕内复活已破坏的柱体。
    /// </summary>
    public void RestoreForLoop()
    {
        isBroken = false;
        SetGameplayVisible(true);
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;
        if (root.name == childName)
            return root;

        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindChildRecursive(root.GetChild(index), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsActive || other == null || owner == null)
            return;

        Level131PlatformSpike breaker = other.GetComponentInParent<Level131PlatformSpike>();
        if (breaker != null)
        {
            owner.NotifyPlatformSpikeHit(this);
            return;
        }

        ActorCtrl actor = other.GetComponentInParent<ActorCtrl>();
        if (actor != null && actor == owner.Actor)
            owner.NotifyObstacleHit(this);
    }
}

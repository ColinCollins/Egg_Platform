using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// D088 局部反重力区域。接触范围内的动态刚体反转原重力，离开最后一个区域时恢复。
/// 不修改速度、质量、阻力和物理材质；多个子碰撞体或重叠区域只反转一次。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 88 Gravity Zone")]
public sealed class DLevel88GravityZone : MonoBehaviour
{
    #region 本地配置
    /// <summary>圆形使用 64 边轮廓，保证可见填色与触发边界一致。</summary>
    public enum ZoneShape { [InspectorName("矩形")] Rectangle, [InspectorName("圆形")] Circle, [InspectorName("多边形")] Polygon }

    /// <summary>区域轮廓；多边形顶点为本地坐标，按顺/逆时针排列且不能自交。</summary>
    [SerializeField] private ZoneShape shape;
    /// <summary>矩形宽高，世界尺寸还会乘以 Transform 缩放。</summary>
    [SerializeField] private Vector2 rectangleSize = new Vector2(6f, 3f);
    /// <summary>圆形本地半径；非等比缩放会形成椭圆。</summary>
    [SerializeField, Min(0.05f)] private float circleRadius = 2f;
    /// <summary>多边形本地顶点，3～64 个；可通过 Scene 视图拖动编辑。</summary>
    [SerializeField] private Vector2[] polygonPoints = { new Vector2(-3,-1.5f), new Vector2(3,-1.5f), new Vector2(3,1.5f), new Vector2(-3,1.5f) };
    /// <summary>区域填色，Alpha 控制透明度。</summary>
    [SerializeField] private Color regionColor = new Color(152f / 255f, 160f / 255f, 250f / 255f, 0.5f);
    /// <summary>内部手绘纹理每张图对应的本地尺寸；默认按 233×201 像素、100 PPU 平铺。</summary>
    [SerializeField] private Vector2 patternTileSize = new Vector2(2.33f, 2.01f);
    /// <summary>内部纹理强度；原图最大 Alpha 约 13%，默认放大4倍，0恢复纯色，不改变区域透明度。</summary>
    [SerializeField, Range(0f, 8f)] private float patternStrength = 4f;
    /// <summary>描边宽度；默认按素材 11 像素、100 PPU 配置，不扩大触发范围。</summary>
    [SerializeField, Min(0.01f)] private float outlineWidth = 0.11f;
    /// <summary>沿边界重复一整张素材的长度；默认 200 像素对应 2 单位，不随总周长拉伸。</summary>
    [SerializeField, Min(0.01f)] private float outlineRepeatLength = 2f;
    /// <summary>可影响的碰撞体层，默认包括球与画线；静态、运动学和 Trigger 不受影响。</summary>
    [SerializeField] private LayerMask affectedLayers = ~0;
    /// <summary>与本脚本同一对象的触发器，必须在 Prefab 显式绑定。</summary>
    [SerializeField] private PolygonCollider2D regionCollider;
    /// <summary>同一对象的网格过滤器；仅运行时创建本实例网格。</summary>
    [SerializeField] private MeshFilter regionMesh;
    /// <summary>同一对象的填色渲染器，材质使用本关区域着色器。</summary>
    [SerializeField] private MeshRenderer regionRenderer;
    /// <summary>预置描边子对象的 LineRenderer，必须显式绑定；没有碰撞体，只负责重复贴图。</summary>
    [SerializeField] private LineRenderer regionOutline;

    /// <summary>编辑器读取的当前形状；不访问配置、存档或运行时单例。</summary>
    public ZoneShape Shape => shape;
    /// <summary>编辑器本地颜色预览。</summary>
    public Color RegionColor => regionColor;
    /// <summary>编辑器辅助描边使用的真实宽度；不读取渲染器旧值。</summary>
    public float OutlineWidth => outlineWidth;
    /// <summary>编辑器撤销、绑定核对使用的触发器引用。</summary>
    public PolygonCollider2D RegionCollider => regionCollider;
    #endregion

    #region 运行时所有权
    private sealed class GravityLease { public float Original; public int Owners; }
    private static readonly Dictionary<Rigidbody2D, GravityLease> Leases = new Dictionary<Rigidbody2D, GravityLease>();
    private readonly List<Collider2D> _overlaps = new List<Collider2D>(64);
    private HashSet<Rigidbody2D> _inside = new HashSet<Rigidbody2D>();
    private HashSet<Rigidbody2D> _current = new HashSet<Rigidbody2D>();
    private Mesh _runtimeMesh;
    private MaterialPropertyBlock _colorBlock;
    private bool _dirty;
    private bool _valid;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOwnership()
    {
        foreach (var pair in Leases) Restore(pair.Key, pair.Value);
        Leases.Clear();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        RefreshRuntimeShape();
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying) return;
        if (_dirty) RefreshRuntimeShape();
        if (!_valid || !regionCollider.enabled) { ReleaseAll(); return; }

        _current.Clear();
        var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = affectedLayers };
        regionCollider.Overlap(filter, _overlaps);
        foreach (var hit in _overlaps)
        {
            if (hit == null || !hit.enabled) continue;
            var body = hit.attachedRigidbody;
            if (body == null || !body.simulated || body.bodyType != RigidbodyType2D.Dynamic) continue;
            _current.Add(body);
        }
        foreach (var body in _inside)
            if (!_current.Contains(body)) Release(body);
        foreach (var body in _current)
            if (!_inside.Contains(body)) Acquire(body);
        var swap = _inside; _inside = _current; _current = swap;
    }

    private static void Acquire(Rigidbody2D body)
    {
        if (!Leases.TryGetValue(body, out var lease))
        {
            lease = new GravityLease { Original = body.gravityScale };
            Leases.Add(body, lease);
            body.gravityScale = -lease.Original;
            body.WakeUp();
        }
        lease.Owners++;
    }

    private static void Release(Rigidbody2D body)
    {
        if (!Leases.TryGetValue(body, out var lease)) return;
        if (--lease.Owners > 0) return;
        Restore(body, lease);
        Leases.Remove(body);
    }

    private static void Restore(Rigidbody2D body, GravityLease lease)
    {
        // 若其它机制已经接管重力，不在退出时覆写它的新值。
        if (body != null && Mathf.Approximately(body.gravityScale, -lease.Original))
        {
            body.gravityScale = lease.Original;
            body.WakeUp();
        }
    }

    private void ReleaseAll()
    {
        foreach (var body in _inside) Release(body);
        _inside.Clear(); _current.Clear(); _overlaps.Clear();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        ReleaseAll();
        if (regionRenderer != null) regionRenderer.enabled = false;
        if (regionOutline != null) regionOutline.enabled = false;
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying) return;
        ReleaseAll();
        if (_runtimeMesh != null) Destroy(_runtimeMesh);
    }
    #endregion

    #region 形状与本地预览
    private void OnValidate()
    {
        rectangleSize.x = Mathf.Max(0.05f, rectangleSize.x);
        rectangleSize.y = Mathf.Max(0.05f, rectangleSize.y);
        circleRadius = Mathf.Max(0.05f, circleRadius);
        patternTileSize.x = Mathf.Max(0.01f, patternTileSize.x);
        patternTileSize.y = Mathf.Max(0.01f, patternTileSize.y);
        patternStrength = Mathf.Clamp(patternStrength, 0f, 8f);
        outlineWidth = Mathf.Max(0.01f, outlineWidth);
        outlineRepeatLength = Mathf.Max(0.01f, outlineRepeatLength);
        // 编辑器只规范字段。物理和 Mesh 更新留到运行时主线程，Scene 预览由 Editor Handles 绘制。
        _dirty = true;
    }

    /// <summary>生成本地轮廓和填充索引供运行时/Scene 预览共用；只分配托管数组，不创建 Unity 实例。</summary>
    public bool TryBuildShape(out Vector2[] points, out int[] triangles)
    {
        if (shape == ZoneShape.Rectangle)
        {
            Vector2 h = rectangleSize * 0.5f;
            points = new[] { new Vector2(-h.x,-h.y), new Vector2(h.x,-h.y), h, new Vector2(-h.x,h.y) };
        }
        else if (shape == ZoneShape.Circle)
        {
            points = new Vector2[64];
            for (int i=0;i<points.Length;i++)
            {
                float a = i * Mathf.PI * 2f / points.Length;
                points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * circleRadius;
            }
        }
        else points = polygonPoints == null ? new Vector2[0] : (Vector2[])polygonPoints.Clone();
        return DLevel88ZoneGeometry.TryTriangulate(points, out triangles);
    }

    /// <summary>编辑器明确修改 Inspector 时同步已绑定碰撞体；不生成 Mesh/对象、不进入任何运行时数据链。</summary>
    public void ApplyEditorCollider()
    {
        if (Application.isPlaying || regionCollider == null) return;
        bool valid = TryBuildShape(out var points, out _);
        regionCollider.enabled = valid;
        regionCollider.isTrigger = true;
        regionCollider.offset = Vector2.zero;
        if (valid) { regionCollider.pathCount = 1; regionCollider.SetPath(0, points); }
    }

    /// <summary>Inspector修改运行中的区域后立即刷新显示；暂停时也生效，不驱动物理模拟或生成编辑态实例。</summary>
    public void RefreshRuntimePreview()
    {
        if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled) return;
        RefreshRuntimeShape();
    }

    private void RefreshRuntimeShape()
    {
        _dirty = false;
        _valid = regionCollider != null && regionMesh != null && regionRenderer != null;
        if (!_valid) { Debug.LogError("D088 反重力区域缺少显式绑定，请从区域 Prefab 创建。", this); return; }
        _valid = TryBuildShape(out var points, out var triangles);
        regionCollider.enabled = _valid;
        regionRenderer.enabled = _valid;
        if (regionOutline != null) regionOutline.enabled = _valid;
        if (!_valid) { ReleaseAll(); Debug.LogWarning("D088 区域顶点必须为 3～64 个、不能重复或自交。", this); return; }
        regionCollider.isTrigger = true;
        regionCollider.offset = Vector2.zero;
        regionCollider.pathCount = 1;
        regionCollider.SetPath(0, points);
        if (_runtimeMesh == null) { _runtimeMesh = new Mesh { name = "D088 区域实例网格" }; regionMesh.sharedMesh = _runtimeMesh; }
        var vertices = new Vector3[points.Length];
        for (int i=0;i<points.Length;i++) vertices[i] = points[i];
        _runtimeMesh.Clear(); _runtimeMesh.vertices = vertices; _runtimeMesh.triangles = triangles; _runtimeMesh.RecalculateBounds();
        if (_colorBlock == null) _colorBlock = new MaterialPropertyBlock();
        _colorBlock.SetColor("_RegionColor", regionColor);
        _colorBlock.SetVector("_PatternTileSize", new Vector4(patternTileSize.x, patternTileSize.y, 0f, 0f));
        _colorBlock.SetFloat("_PatternStrength", patternStrength);
        regionRenderer.SetPropertyBlock(_colorBlock);
        RefreshOutline(vertices);
    }
    #endregion

    #region 同源轮廓重复描边
    private void RefreshOutline(Vector3[] vertices)
    {
        if (regionOutline == null) return;
        regionOutline.useWorldSpace = false;
        regionOutline.loop = true;
        regionOutline.alignment = LineAlignment.TransformZ;
        regionOutline.textureMode = LineTextureMode.Tile;
        regionOutline.textureScale = new Vector2(1f / outlineRepeatLength, 1f);
        regionOutline.startWidth = outlineWidth;
        regionOutline.endWidth = outlineWidth;
        regionOutline.startColor = Color.white;
        regionOutline.endColor = Color.white;
        regionOutline.numCornerVertices = 3;
        regionOutline.numCapVertices = 0;
        regionOutline.sortingLayerID = regionRenderer.sortingLayerID;
        regionOutline.sortingOrder = regionRenderer.sortingOrder + 1;
        regionOutline.positionCount = vertices.Length;
        regionOutline.SetPositions(vertices);
    }
    #endregion
}

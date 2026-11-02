using System.Collections.Generic;
using UnityEngine;

/// <summary>第 9 关岩浆表面的鼓泡、爆裂和熔岩滴；纯表现，不改变水面或浮力。</summary>
[DisallowMultipleComponent]
public sealed class Level9LavaBubbles : MonoBehaviour
{
    #region Inspector 绑定与表现参数
    // 由 Level_009 Prefab 显式绑定，禁止通过名称或层级查找。
    [Header("水面引用（已绑定）")]
    [SerializeField, Tooltip("只读获取水面宽度及上排顶点数量。")]
    private InteractableWater water;
    [SerializeField, Tooltip("读取水面运行时网格，跟随交互波浪。")]
    private MeshFilter waterMesh;
    [SerializeField, Tooltip("读取当前水面材质的波浪参数，不改动材质。")]
    private MeshRenderer waterRenderer;
    [SerializeField, Tooltip("本节点专用网格输出。")]
    private MeshFilter bubbleMesh;

    [Header("循环节奏")]
    [SerializeField, Range(1, 24), Tooltip("同时循环的泡泡槽数；修改后重新运行生效。")]
    private int bubbleCount = 12;
    [SerializeField, Tooltip("每次鼓起前的随机等待秒数，X 最短，Y 最长。")]
    private Vector2 idleSeconds = new Vector2(0.4f, 1.8f);
    [SerializeField, Tooltip("从水面鼓起到爆裂的随机秒数。")]
    private Vector2 growSeconds = new Vector2(1f, 1.4f);
    [SerializeField, Tooltip("爆裂飞溅、回落和消散的随机秒数。")]
    private Vector2 burstSeconds = new Vector2(0.48f, 0.72f);

    [Header("大小和颜色")]
    [SerializeField, Tooltip("泡泡直径的世界单位范围；小泡较多，中泡次之，大泡少量，不受 Water 横向缩放拉伸。")]
    private Vector2 diameter = new Vector2(0.35f, 0.9f);
    [SerializeField, Min(0f), Tooltip("两侧边缘留白，单位为世界距离。")]
    private float edgeMargin = 0.2f;
    [SerializeField, Tooltip("泡泡根部相对可见水面的高度，负值使根部稍微沉入岩浆。")]
    private float surfaceOffset = -0.015f;
    [SerializeField, Tooltip("爆裂小滴的明亮橙色轮廓；完整泡体颜色由本关泡泡材质控制。")]
    private Color rimColor = new Color(1f, 0.5f, 0.04f, 1f);
    [SerializeField, Tooltip("爆裂破口和熔岩滴的主体颜色，不影响完整泡体。")]
    private Color lavaColor = new Color(1f, 0.38f, 0.035f, 1f);
    [SerializeField, Tooltip("爆裂涟漪和小滴内侧的高光颜色。")]
    private Color highlightColor = new Color(1f, 0.79f, 0.2f, 1f);
    [SerializeField, Tooltip("单张贴图内泡体的 UV 范围，用于去除透明留白，不是序列帧。")]
    private Rect bubbleTextureRect = new Rect(0.02f, 0.15f, 0.96f, 0.7f);
    #endregion

    #region 运行时缓存
    private struct Bubble
    {
        internal float X, Radius, Age, Grow, Burst, Shape, Seed;
        internal int DropStart, DropCount;
    }

    // 每轮只抽样一次，再按缓存推进连续飞溅；不在绘制时重新随机。
    private struct Drop
    {
        internal float StartX, StartY, TravelX, Lift, Size, Aspect, Delay, Duration;
    }

    private const int MaxDrops = 6;

    private static readonly int WaveSpeed = Shader.PropertyToID("_WaveSpeed");
    private static readonly int WaveDirection = Shader.PropertyToID("_WaveFlowDirection");
    private static readonly int WaveCount = Shader.PropertyToID("_WaveCount");
    private static readonly int WaveSize = Shader.PropertyToID("_WaveSize");
    private readonly List<Vector3> _surface = new List<Vector3>(1000);
    private readonly List<Vector3> _vertices = new List<Vector3>(8192);
    private readonly List<Color> _colors = new List<Color>(8192);
    private readonly List<Vector2> _uv = new List<Vector2>(8192);
    private readonly List<int> _indices = new List<int>(24576);
    private Bubble[] _bubbles;
    private Drop[] _drops;
    private System.Random _random;
    private Mesh _mesh;
    private Matrix4x4 _worldToLocal;
    private float _wavePhase, _waveCount, _waveSize;
    #endregion

    #region 生命周期
    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (water == null || waterMesh == null || waterRenderer == null || bubbleMesh == null)
        {
            Debug.LogError("[Level9LavaBubbles] 请在 Prefab 绑定水面和网格引用。", this);
            enabled = false;
            return;
        }

        // Rect 序列化缺失版本或裁剪范围无效时，避免整张泡体只采到透明角点。
        if (!(bubbleTextureRect.width > 0.01f && bubbleTextureRect.height > 0.01f &&
              bubbleTextureRect.xMin >= 0f && bubbleTextureRect.yMin >= 0f &&
              bubbleTextureRect.xMax <= 1f && bubbleTextureRect.yMax <= 1f))
        {
            bubbleTextureRect = new Rect(0.02f, 0.15f, 0.96f, 0.7f);
            Debug.LogWarning("[Level9LavaBubbles] 泡体 UV 范围无效，本次运行使用素材默认范围；请检查 Prefab。", this);
        }

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "Level009_LavaBubbles_Runtime" };
            _mesh.MarkDynamic();
        }
        bubbleMesh.sharedMesh = _mesh;
        _random = new System.Random(GetInstanceID()); // 不消费全局随机数，避免影响关卡随机逻辑。
        _bubbles = new Bubble[Mathf.Clamp(bubbleCount, 1, 24)];
        _drops = new Drop[_bubbles.Length * MaxDrops];
        for (int i = 0; i < _bubbles.Length; i++)
        {
            RestartBubble(i);
            // 首轮只错开等待，不跳过鼓起阶段，避免刚出现就爆裂。
            _bubbles[i].Age = Range(-1.5f, 0f);
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || _mesh == null || _bubbles == null) return;
        // 水面在 Start 建网格、FixedUpdate 更新顶点；这里只读，不触发 Splash。
        Mesh source = waterMesh.sharedMesh;
        if (source == null || source.vertexCount != water.NumOfVertices * 2) return;
        source.GetVertices(_surface);
        Material material = waterRenderer.sharedMaterial;
        _wavePhase = Time.time * material.GetFloat(WaveSpeed) * material.GetFloat(WaveDirection);
        _waveCount = material.GetFloat(WaveCount);
        _waveSize = material.GetFloat(WaveSize);
        _worldToLocal = transform.worldToLocalMatrix;
        _vertices.Clear();
        _colors.Clear();
        _uv.Clear();
        _indices.Clear();

        for (int i = 0; i < _bubbles.Length; i++)
        {
            _bubbles[i].Age += Time.deltaTime;
            if (_bubbles[i].Age >= _bubbles[i].Grow + _bubbles[i].Burst) RestartBubble(i);
            Bubble bubble = _bubbles[i];
            if (bubble.Age < 0f) continue;
            Vector3 anchor = SurfacePoint(bubble.X);
            anchor.y += surfaceOffset;
            anchor.z -= 0.01f;
            if (bubble.Age < bubble.Grow) DrawGrowing(bubble, anchor);
            else DrawBurst(bubble, anchor);
        }

        _mesh.Clear();
        _mesh.SetVertices(_vertices);
        _mesh.SetColors(_colors);
        _mesh.SetUVs(0, _uv);
        _mesh.SetTriangles(_indices, 0);
        _mesh.RecalculateBounds();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        if (_mesh != null) _mesh.Clear();
    }

    private void OnDestroy()
    {
        // 只释放本组件创建的网格；共享材质、水面网格均不归本组件所有。
        if (_mesh != null) Destroy(_mesh);
    }
    #endregion

    #region 周期与水面采样
    private float Range(float min, float max) => Mathf.Lerp(min, max, (float)_random.NextDouble());
    private float PositiveRange(Vector2 range) => Range(Mathf.Max(0.05f, range.x), Mathf.Max(0.05f, Mathf.Max(range.x, range.y)));

    private void RestartBubble(int index)
    {
        // 拉开三档尺寸差异，最大值仍受 Inspector 上限约束，不再放大最大泡。
        float sizeRoll = Range(0f, 1f);
        float sizeT = sizeRoll < 0.55f ? Range(0f, 0.3f) : sizeRoll < 0.9f ? Range(0.45f, 0.7f) : Range(0.85f, 1f);
        float radius = Mathf.Lerp(Mathf.Max(0.05f, diameter.x), Mathf.Max(0.05f, Mathf.Max(diameter.x, diameter.y)), sizeT) * 0.5f;
        float scaleX = water.transform.TransformVector(Vector3.right).magnitude;
        // 覆盖随机小滴最远位移及自身半径，也覆盖涟漪；每个槽分区随机。
        float halfRange = Mathf.Max(0f, water.Width * 0.5f - (radius * 2.9f + edgeMargin) / Mathf.Max(0.001f, scaleX));
        float position = (index + Range(0.05f, 0.95f)) / _bubbles.Length;
        _bubbles[index] = new Bubble
        {
            X = Mathf.Lerp(-halfRange, halfRange, position), Radius = radius,
            Age = -PositiveRange(idleSeconds), Grow = PositiveRange(growSeconds),
            Burst = PositiveRange(burstSeconds), Shape = Range(0.85f, 1.05f), Seed = Range(0f, 6.28f),
            DropStart = index * MaxDrops, DropCount = _random.Next(2, MaxDrops + 1)
        };
        for (int i = 0; i < _bubbles[index].DropCount; i++)
        {
            float angle = Range(0.2f, Mathf.PI - 0.2f);
            _drops[index * MaxDrops + i] = new Drop
            {
                StartX = Mathf.Cos(angle) * Range(0.3f, 0.7f),
                StartY = Mathf.Sin(angle) * _bubbles[index].Shape * Range(0.45f, 0.85f),
                TravelX = Mathf.Cos(angle) * Range(0.7f, 1.6f) + Range(-0.3f, 0.3f),
                Lift = Range(1.4f, 3.8f), Size = Range(0.1f, 0.24f), Aspect = Range(0.85f, 1.6f),
                Delay = i == 0 ? 0f : Range(0f, 0.08f), Duration = Range(0.65f, 0.92f)
            };
        }
    }

    private Vector3 SurfacePoint(float localX)
    {
        int count = water.NumOfVertices;
        float position = Mathf.Clamp01((localX + water.Width * 0.5f) / water.Width) * (count - 1);
        int left = Mathf.Min(Mathf.FloorToInt(position), count - 2);
        Vector3 a = _surface[count + left];
        Vector3 b = _surface[count + left + 1];
        // 与本关 Level9LavaSurface 上排顶点（uv.y=1）的位移一致，再按网格边线插值。
        a.y += Mathf.Sin((a.x + _wavePhase) * _waveCount) * _waveSize;
        b.y += Mathf.Sin((b.x + _wavePhase) * _waveCount) * _waveSize;
        return water.transform.TransformPoint(Vector3.Lerp(a, b, position - left));
    }
    #endregion

    #region 鼓泡与爆裂绘制
    private void DrawGrowing(Bubble bubble, Vector3 anchor)
    {
        float t = bubble.Age / bubble.Grow;
        // 全程持续膨胀，末段仍有增长速度，达到最大值后立即转入爆裂。
        float growth = t * (0.65f + 0.35f * t);
        float width = bubble.Radius * Mathf.Lerp(0.45f, 1f, growth);
        float height = bubble.Radius * Mathf.Lerp(0.12f, 1f, growth) * bubble.Shape;
        height *= 1f + Mathf.Sin(t * 7f + bubble.Seed) * 0.015f * t * (1f - t);
        float alpha = Mathf.Clamp01(t * 8f);
        float localHalfWidth = width / Mathf.Max(0.001f, water.transform.TransformVector(Vector3.right).magnitude);
        Vector3 leftBase = anchor + Vector3.left * width;
        Vector3 rightBase = anchor + Vector3.right * width;
        // 放大泡体后分别贴合左右根部，避免波浪斜坡上出现水平底边悬空。
        leftBase.y = SurfacePoint(bubble.X - localHalfWidth).y + surfaceOffset;
        rightBase.y = SurfacePoint(bubble.X + localHalfWidth).y + surfaceOffset;
        // 单张泡体随原算法连续鼓起；爆裂仍由下方短弧、小滴和涟漪绘制。
        Color color = new Color(1f, 1f, 1f, alpha);
        int first = Vertex(leftBase, color);
        Vertex(rightBase, color);
        Vertex(leftBase + Vector3.up * height, color);
        Vertex(rightBase + Vector3.up * height, color);
        _uv[first] = new Vector2(bubbleTextureRect.xMin, bubbleTextureRect.yMin);
        _uv[first + 1] = new Vector2(bubbleTextureRect.xMax, bubbleTextureRect.yMin);
        _uv[first + 2] = new Vector2(bubbleTextureRect.xMin, bubbleTextureRect.yMax);
        _uv[first + 3] = new Vector2(bubbleTextureRect.xMax, bubbleTextureRect.yMax);
        Triangle(first, first + 2, first + 1);
        Triangle(first + 1, first + 2, first + 3);
    }

    private void DrawBurst(Bubble bubble, Vector3 anchor)
    {
        float t = (bubble.Age - bubble.Grow) / bubble.Burst;
        float fade = 1f - Mathf.SmoothStep(0.15f, 1f, t);
        float radius = bubble.Radius;
        // 破口只保留两侧短弧，随后化为小滴，避免直接把完整泡泡缩没。
        if (t < 0.24f)
        {
            float shell = 1f - t / 0.24f;
            Arc(anchor, radius * (1f + t), radius * bubble.Shape, 0f, shell * 0.9f,
                radius * 0.09f, Fade(lavaColor, shell), 7);
            Arc(anchor, radius * (1f + t), radius * bubble.Shape, Mathf.PI - shell * 0.9f, Mathf.PI,
                radius * 0.09f, Fade(lavaColor, shell), 7);
        }
        Arc(anchor, radius * (0.65f + t * 1.1f), radius * 0.13f,
            0f, Mathf.PI * 2f, radius * 0.07f * (1f - t), Fade(highlightColor, fade * 0.75f), 24);

        for (int i = 0; i < bubble.DropCount; i++)
        {
            Drop drop = _drops[bubble.DropStart + i];
            float progress = (t - drop.Delay) / drop.Duration;
            if (progress < 0f || progress >= 1f) continue;
            float height = radius * (drop.StartY * (1f - progress) + drop.Lift * progress * (1f - progress));
            Vector3 center = anchor + new Vector3(radius * (drop.StartX + drop.TravelX * progress), height, 0f);
            float size = radius * drop.Size * (1f - progress * 0.55f);
            float dropFade = 1f - Mathf.SmoothStep(0.45f, 1f, progress);
            Disc(center, size, size * drop.Aspect, 0f, Mathf.PI * 2f,
                Fade(highlightColor, dropFade), Fade(rimColor, dropFade), 10);
        }
    }

    private static Color Fade(Color color, float alpha)
    {
        color.a *= alpha;
        return color;
    }
    #endregion

    #region 共用网格（世界尺寸转回本节点坐标，不逐帧创建对象）
    private int Vertex(Vector3 world, Color color)
    {
        int index = _vertices.Count;
        _vertices.Add(_worldToLocal.MultiplyPoint3x4(world));
        _colors.Add(color);
        _uv.Add(new Vector2(-1f, -1f)); // 负 UV 表示纯顶点色飞溅几何。
        return index;
    }

    private void Triangle(int a, int b, int c)
    {
        _indices.Add(a); _indices.Add(b); _indices.Add(c);
    }

    private void Disc(Vector3 center, float width, float height, float from, float to, Color inner, Color outer, int segments)
    {
        int first = Vertex(center, inner);
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(from, to, (float)i / segments);
            Vertex(center + new Vector3(Mathf.Cos(angle) * width, Mathf.Sin(angle) * height, 0f), outer);
            if (i > 0) Triangle(first, first + i, first + i + 1);
        }
    }

    private void Arc(Vector3 center, float width, float height, float from, float to, float thickness, Color color, int segments)
    {
        int first = _vertices.Count;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(from, to, (float)i / segments);
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * width, Mathf.Sin(angle) * height, 0f);
            Vector3 normal = new Vector3(Mathf.Cos(angle) * height, Mathf.Sin(angle) * width, 0f).normalized * thickness * 0.5f;
            Vertex(point - normal, color);
            Vertex(point + normal, color);
            if (i == 0) continue;
            int current = first + i * 2;
            Triangle(current - 2, current - 1, current);
            Triangle(current, current - 1, current + 1);
        }
    }
    #endregion
}

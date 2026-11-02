using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>沿原 Sprite 的世界矩形细分曲面，保留原纹理跨度，不把整张绳纹压进每一小段。</summary>
internal sealed class Level132RopeVisual : IDisposable
{
    private readonly Transform _root;
    private readonly GameObject _object;
    private readonly Mesh _mesh;
    private readonly Vector3[] _rest;
    private readonly Vector3[] _vertices;
    private readonly Vector3 _a;
    private readonly Vector3 _b;
    private readonly Vector3 _halfWidth;
    public SpriteRenderer Source { get; }
    public bool OriginalVisible { get; }
    public float LeftX => Mathf.Min(_a.x, _b.x);
    public float RightX => Mathf.Max(_a.x, _b.x);
    public Vector3 RightEnd => _a.x > _b.x ? _a : _b;
    public float Width => _halfWidth.magnitude * 2f;
    public bool HasDrawn { get; private set; }
    public Bounds WorldBounds { get; private set; }

    public Level132RopeVisual(SpriteRenderer source, Transform parent, Material curveMaterial,
        bool extension = false, float anchorX = float.NaN)
    {
        Source = source;
        OriginalVisible = source.enabled;
        _root = parent;
        Bounds bounds = source.sprite.bounds;
        _a = source.transform.TransformPoint(new Vector3(bounds.center.x, bounds.min.y, 0f));
        _b = source.transform.TransformPoint(new Vector3(bounds.center.x, bounds.max.y, 0f));
        _halfWidth = source.transform.TransformVector(Vector3.right * bounds.extents.x);
        if (extension)
        {
            Vector3 right = RightEnd;
            float length = Mathf.Max(0.1f, RightX - LeftX);
            float width = Width;
            bool pointsRight = _b.x >= _a.x;
            // 只由原最右端水平续接；不会回到锚点 Y 或在每次复用时产生斜率断层。
            _a = pointsRight ? right : right + Vector3.right * length;
            _b = pointsRight ? right + Vector3.right * length : right;
            _halfWidth = (pointsRight ? Vector3.down : Vector3.up) * (width * 0.5f);
        }
        int segments = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(_a, _b) / 0.18f));
        var samples = new List<float>();
        for (int i = 0; i <= segments; i++) samples.Add((float)i / segments);
        if (!float.IsNaN(anchorX) && Mathf.Abs(_b.x - _a.x) > 0.00001f)
        {
            float anchorT = (anchorX - _a.x) / (_b.x - _a.x);
            if (anchorT > 0f && anchorT < 1f && !samples.Contains(anchorT)) samples.Add(anchorT);
            samples.Sort();
        }
        segments = samples.Count - 1;
        _rest = new Vector3[samples.Count * 2];
        _vertices = new Vector3[_rest.Length];
        var uv = new Vector2[_rest.Length];
        var colors = new Color[_rest.Length];
        var triangles = new int[segments * 6];

        // Sprite 顶点到 UV 的仿射映射，同时保留 pivot、翻转和图集旋转后的采样方向。
        Vector2[] spriteVertices = source.sprite.vertices;
        Vector2[] spriteUv = source.sprite.uv;
        Vector2 origin = spriteVertices[0];
        Vector2 axisA = spriteVertices[1] - origin;
        int third = 2;
        while (third < spriteVertices.Length &&
            Mathf.Abs(Cross(axisA, spriteVertices[third] - origin)) < 0.000001f) third++;
        if (third == spriteVertices.Length)
            throw new InvalidOperationException("[Level132] Rope sprite UV basis is degenerate.");
        Vector2 axisB = spriteVertices[third] - origin;
        float determinant = Cross(axisA, axisB);
        for (int i = 0; i <= segments; i++)
        {
            float t = samples[i];
            Vector3 center = Vector3.Lerp(_a, _b, t);
            for (int side = 0; side < 2; side++)
            {
                int index = i * 2 + side;
                _rest[index] = center + _halfWidth * (side == 0 ? -1f : 1f);
                Vector2 local = new Vector2(side == 0 ? bounds.min.x : bounds.max.x,
                    Mathf.Lerp(bounds.min.y, bounds.max.y, t));
                if (source.flipX) local.x = bounds.min.x + bounds.max.x - local.x;
                if (source.flipY) local.y = bounds.min.y + bounds.max.y - local.y;
                Vector2 relative = local - origin;
                uv[index] = spriteUv[0] + (spriteUv[1] - spriteUv[0]) * (Cross(relative, axisB) / determinant) +
                    (spriteUv[third] - spriteUv[0]) * (Cross(axisA, relative) / determinant);
                colors[index] = source.color;
            }
            if (i == segments) continue;
            int v = i * 2;
            int tri = i * 6;
            triangles[tri] = v; triangles[tri + 1] = v + 2; triangles[tri + 2] = v + 1;
            triangles[tri + 3] = v + 1; triangles[tri + 4] = v + 2; triangles[tri + 5] = v + 3;
        }
        _object = new GameObject("Level132RopeCurve");
        _object.layer = source.gameObject.layer;
        _object.SetActive(false);
        _object.transform.SetParent(parent, false);
        _mesh = new Mesh { name = "Level132RopeCurve" };
        _mesh.MarkDynamic();
        _mesh.vertices = _vertices;
        _mesh.uv = uv;
        _mesh.colors = colors;
        _mesh.triangles = triangles;
        _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
        var renderer = _object.AddComponent<MeshRenderer>();
        // 普通 MeshRenderer 不沿用依赖 unity_SpriteProps/unity_SpriteColor 的 Sprite-Lit 材质。
        renderer.sharedMaterial = curveMaterial;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder;
        var properties = new MaterialPropertyBlock();
        properties.SetTexture("_MainTex", source.sprite.texture);
        Texture2D alpha = source.sprite.associatedAlphaSplitTexture;
        properties.SetTexture("_AlphaTex", alpha != null ? alpha : Texture2D.whiteTexture);
        properties.SetFloat("_UseExternalAlpha", alpha != null ? 1f : 0f);
        renderer.SetPropertyBlock(properties);
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    public float SurfaceY(float x, bool below = false)
    {
        float t = Mathf.Abs(_b.x - _a.x) < 0.00001f ? 0f : Mathf.Clamp01((x - _a.x) / (_b.x - _a.x));
        return Mathf.Lerp(_a.y, _b.y, t) + Mathf.Abs(_halfWidth.y) * (below ? -1f : 1f);
    }

    public void Draw(float offsetX, Func<float, float> displacement)
    {
        Bounds worldBounds = default;
        for (int i = 0; i < _rest.Length; i++)
        {
            Vector3 point = _rest[i] + Vector3.right * offsetX;
            point.y += displacement(point.x);
            if (i == 0) worldBounds = new Bounds(point, Vector3.zero);
            else worldBounds.Encapsulate(point);
            _vertices[i] = _root.InverseTransformPoint(point);
        }
        _mesh.vertices = _vertices;
        _mesh.RecalculateBounds();
        WorldBounds = worldBounds;
        HasDrawn = true;
        _object.SetActive(true); // 网格、UV、纹理与包围盒就绪后才显示。
    }

    public void Hide() => _object.SetActive(false);

    public void Dispose()
    {
        if (_mesh != null) UnityEngine.Object.Destroy(_mesh);
        if (_object != null) UnityEngine.Object.Destroy(_object);
    }
}

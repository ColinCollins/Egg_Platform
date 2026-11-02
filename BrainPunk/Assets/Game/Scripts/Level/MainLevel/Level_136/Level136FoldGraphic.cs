using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>Level136 专用纸片/墨迹网格。仅绘制局部多边形，不覆盖整屏，不生成材质或截图。</summary>
public sealed class Level136FoldGraphic : MaskableGraphic, ICanvasRaycastFilter,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IPointerUpHandler, IEndDragHandler
{
    #region 层数据
    private Level136PaperFold _owner;
    private Sprite _sprite;
    private int _layer;
    private Vector2 _corner;
    private readonly List<Vector2> _paper = new List<Vector2>(5);
    private readonly List<UIVertex> _polygon = new List<UIVertex>(12);
    private readonly List<UIVertex> _scratch = new List<UIVertex>(12);

    /// <summary>墨迹层直接采样原始 Sprite；纸面层使用 UGUI 白纹理。</summary>
    public override Texture mainTexture => _sprite != null ? _sprite.texture : Texture2D.whiteTexture;

    /// <summary>由本关控制器创建时调用，只有最后一层参与局部射线命中。</summary>
    public void Initialize(Level136PaperFold owner, int layer, Sprite sprite)
    {
        _owner = owner;
        _layer = layer;
        _sprite = sprite;
        raycastTarget = layer == 3;
        SetAllDirty();
    }

    /// <summary>二维纸角变化仅标记网格变化；不新建纹理、材质或临时 GameObject。</summary>
    public void SetCorner(Vector2 corner)
    {
        _corner = corner;
        SetVerticesDirty();
    }
    #endregion

    #region 网格绘制
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f || _owner == null)
            return;
        Level136FoldGeometry shape = new Level136FoldGeometry(rect.size, _corner);
        BuildPaperPolygon(shape, rect.size);
        if (_layer == 3)
        {
            // 射线不依赖数字是否露出；即使纸角几乎合拢，仍能重新抓住它。
            Color hitColor = new Color(1f, 1f, 1f, 0.001f);
            Triangle(vh, Vector2.zero, new Vector2(rect.width, 0f), rect.size, hitColor);
            Triangle(vh, Vector2.zero, rect.size, new Vector2(0f, rect.height), hitColor);
        }
        else if (shape.IsClosed)
            return;
        else if (_layer == 0)
        {
            Paper(vh, shape, false);
            RuledLines(vh, shape);
        }
        else if (_layer == 1)
        {
            // 仅沿自由边产生窄阴影，避免旧版的大圆柱形灰幕。
            Vector2 center = FoldedCenter(shape);
            for (int i = 0; i < _paper.Count; i++)
            {
                Vector2 a = _paper[i];
                Vector2 b = _paper[(i + 1) % _paper.Count];
                if (shape.FoldSide(a) < 0.01f && shape.FoldSide(b) < 0.01f)
                    continue;
                EdgeShadow(vh, shape.FoldPoint(a), shape.FoldPoint(b), center, rect.width * 0.004f);
            }
            Paper(vh, shape, true);
        }
        else if (_sprite != null && _owner.HasInkLayout)
            Ink(vh, shape);
    }

    private void BuildPaperPolygon(Level136FoldGeometry shape, Vector2 size)
    {
        _paper.Clear();
        Vector2 previous = new Vector2(0f, size.y);
        float previousDistance = shape.FoldSide(previous);
        for (int i = 0; i < 4; i++)
        {
            Vector2 current = i == 0 ? Vector2.zero : i == 1 ? new Vector2(size.x, 0f)
                : i == 2 ? size : new Vector2(0f, size.y);
            float distance = shape.FoldSide(current);
            if ((distance >= 0f) != (previousDistance >= 0f))
                _paper.Add(Vector2.LerpUnclamped(previous, current, previousDistance / (previousDistance - distance)));
            if (distance >= 0f)
                _paper.Add(current);
            previous = current;
            previousDistance = distance;
        }
    }

    private Vector2 FoldedCenter(Level136FoldGeometry shape)
    {
        Vector2 center = Vector2.zero;
        foreach (Vector2 point in _paper)
            center += shape.FoldPoint(point);
        return center / Mathf.Max(1, _paper.Count);
    }

    private void Paper(VertexHelper vh, Level136FoldGeometry shape, bool back)
    {
        int start = vh.currentVertCount;
        foreach (Vector2 point in _paper)
            vh.AddVert(Vertex(back ? shape.FoldPoint(point) : point, Vector2.zero,
                back ? _owner.Appearance.foldColor : _owner.Appearance.underColor));
        for (int i = 1; i + 1 < _paper.Count; i++)
            vh.AddTriangle(start, start + i, start + i + 1);
    }

    private void RuledLines(VertexHelper vh, Level136FoldGeometry shape)
    {
        Level136PaperAppearance style = _owner.Appearance;
        Rect rect = rectTransform.rect;
        float scale = rect.height / 1080f;
        float spacing = Mathf.Max(12f, style.lineSpacing) * scale;
        float width = Mathf.Clamp(style.lineWidth, 0.1f, Mathf.Max(0.1f, style.lineSpacing * 0.5f)) * scale;
        float offset = Mathf.Repeat(style.lineOffset * scale, spacing);
        if (style.lineColor.a <= 0f)
            return;
        for (float y = offset; y < rect.height; y += spacing)
        {
            float bottom = Mathf.Min(y + width, rect.height);
            _polygon.Clear();
            _polygon.Add(Vertex(new Vector2(0f, y), Vector2.zero, style.lineColor));
            _polygon.Add(Vertex(new Vector2(rect.width, y), Vector2.zero, style.lineColor));
            _polygon.Add(Vertex(new Vector2(rect.width, bottom), Vector2.zero, style.lineColor));
            _polygon.Add(Vertex(new Vector2(0f, bottom), Vector2.zero, style.lineColor));
            // 线条只出现在揭开的底纸内，不浮到正面关卡或纸背上。
            Clip(shape.Top, shape.Right, shape.Corner);
            AddPolygon(vh);
        }
    }

    private UIVertex Vertex(Vector2 point, Vector2 uv, Color tint)
    {
        Rect rect = rectTransform.rect;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = new Vector3(rect.xMin + point.x, rect.yMax - point.y);
        vertex.uv0 = uv;
        vertex.color = tint;
        return vertex;
    }

    private void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(Vertex(a, Vector2.zero, tint));
        vh.AddVert(Vertex(b, Vector2.zero, tint));
        vh.AddVert(Vertex(c, Vector2.zero, tint));
        vh.AddTriangle(start, start + 1, start + 2);
    }

    private void EdgeShadow(VertexHelper vh, Vector2 a, Vector2 b, Vector2 inside, float width)
    {
        Vector2 direction = (b - a).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x);
        if (Vector2.Dot(inside - a, normal) > 0f)
            normal = -normal;
        Color dark = new Color(0.22f, 0.20f, 0.18f, 0.18f);
        Color clear = new Color(dark.r, dark.g, dark.b, 0f);
        int start = vh.currentVertCount;
        vh.AddVert(Vertex(a, Vector2.zero, dark));
        vh.AddVert(Vertex(b, Vector2.zero, dark));
        vh.AddVert(Vertex(b + normal * width, Vector2.zero, clear));
        vh.AddVert(Vertex(a + normal * width, Vector2.zero, clear));
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private void Ink(VertexHelper vh, Level136FoldGeometry shape)
    {
        Vector2 offset = Level136FoldGeometry.BackInkOffset;
        Vector2 size = _sprite.rect.size;
        Vector4 uv = DataUtility.GetOuterUV(_sprite);
        _polygon.Clear();
        AddInkVertex(shape, offset, new Vector2(uv.x, uv.w));
        AddInkVertex(shape, offset + new Vector2(size.x, 0f), new Vector2(uv.z, uv.w));
        AddInkVertex(shape, offset + size, new Vector2(uv.z, uv.y));
        AddInkVertex(shape, offset + new Vector2(0f, size.y), new Vector2(uv.x, uv.y));
        Vector2 center = FoldedCenter(shape);
        for (int i = 0; i < _paper.Count; i++)
            Clip(shape.FoldPoint(_paper[i]), shape.FoldPoint(_paper[(i + 1) % _paper.Count]), center);
        AddPolygon(vh);
    }

    private void AddPolygon(VertexHelper vh)
    {
        int start = vh.currentVertCount;
        foreach (UIVertex vertex in _polygon)
            vh.AddVert(vertex);
        for (int i = 1; i + 1 < _polygon.Count; i++)
            vh.AddTriangle(start, start + i, start + i + 1);
    }

    private void AddInkVertex(Level136FoldGeometry shape, Vector2 source, Vector2 uv)
    {
        _polygon.Add(Vertex(_owner.BackInkPoint(shape, source), uv, _owner.InkColor));
    }

    private Vector2 PagePoint(UIVertex vertex)
    {
        Rect rect = rectTransform.rect;
        return new Vector2(vertex.position.x - rect.xMin, rect.yMax - vertex.position.y);
    }

    private void Clip(Vector2 a, Vector2 b, Vector2 inside)
    {
        _scratch.Clear();
        if (_polygon.Count == 0)
            return;
        float sign = Mathf.Sign(Level136FoldGeometry.Cross(b - a, inside - a));
        UIVertex previous = _polygon[_polygon.Count - 1];
        float previousDistance = sign * Level136FoldGeometry.Cross(b - a, PagePoint(previous) - a);
        foreach (UIVertex current in _polygon)
        {
            float distance = sign * Level136FoldGeometry.Cross(b - a, PagePoint(current) - a);
            if ((distance >= 0f) != (previousDistance >= 0f))
            {
                float t = previousDistance / (previousDistance - distance);
                UIVertex crossing = previous;
                crossing.position = Vector3.LerpUnclamped(previous.position, current.position, t);
                crossing.uv0 = Vector4.LerpUnclamped(previous.uv0, current.uv0, t);
                _scratch.Add(crossing);
            }
            if (distance >= 0f)
                _scratch.Add(current);
            previous = current;
            previousDistance = distance;
        }
        _polygon.Clear();
        _polygon.AddRange(_scratch);
    }
    #endregion

    #region 射线与事件转发
    /// <summary>整屏 Rect 只在纸角命中，页面其它区域可正常操作角色。</summary>
    public bool IsRaycastLocationValid(Vector2 point, Camera eventCamera) => _owner != null && _owner.HitTest(point, eventCamera);
    /// <summary>将按下事件交给本关的单指所有权处理。</summary>
    public void OnPointerDown(PointerEventData data) { _owner.BeginPointer(data); }
    /// <summary>保留 UGUI 拖动目标，起点已在 PointerDown 中记录。</summary>
    public void OnBeginDrag(PointerEventData data) { }
    /// <summary>移动纸角而非数字 Image 的 Transform。</summary>
    public void OnDrag(PointerEventData data) { _owner.DragPointer(data); }
    /// <summary>抬起时保留折页量并释放指针。</summary>
    public void OnPointerUp(PointerEventData data) { _owner.EndPointer(data); }
    /// <summary>拖动结束时补充清理指针。</summary>
    public void OnEndDrag(PointerEventData data) { _owner.EndPointer(data); }
    #endregion
}

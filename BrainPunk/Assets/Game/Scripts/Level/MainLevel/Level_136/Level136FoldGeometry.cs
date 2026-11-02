using UnityEngine;

/// <summary>本关折角的纯几何数据；坐标从页面左上开始，Y 向下，不访问运行时单例。</summary>
public readonly struct Level136FoldGeometry
{
    #region 页面三角
    /// <summary>原始右上角、上沿折点、右沿折点、翻回的自由角。</summary>
    public readonly Vector2 Corner, Top, Right, Tip;
    /// <summary>完全铺平状态，没有折面、底页或阴影；仍可从右上区域开始拖动。</summary>
    public readonly bool IsClosed;
    private readonly Vector2 _size;
    private readonly Vector2 _normal;
    private readonly Vector2 _midpoint;
    private readonly Vector2 _foldDirection;
    // 仅校准折线方向以兼容固定的正面接缝；不是纸背缩放倍率，变换必须保持面积为 1:1。
    private const float FoldDirectionRatio = 5.2f;

    /// <summary>由自由角二维位置反算折线；折线可跨过页面任意边，不把拖拽限制成一条轨迹。</summary>
    public Level136FoldGeometry(Vector2 size, Vector2 normalizedCorner)
    {
        _size = size;
        Corner = new Vector2(size.x, 0f);
        Tip = Vector2.Scale(size, ClampCorner(normalizedCorner));
        IsClosed = Tip == Corner;
        if (IsClosed)
        {
            _normal = Vector2.zero;
            _foldDirection = Vector2.zero;
            _midpoint = Top = Right = Corner;
            return;
        }
        Vector2 displacement = Corner - Tip;
        _normal = new Vector2(displacement.x / FoldDirectionRatio, displacement.y).normalized;
        _midpoint = (Corner + Tip) * 0.5f;
        // n·direction 必须为 1：同一块纸翻回后面积不变，不能用额外的横向放大去拼数字。
        _foldDirection = displacement / Vector2.Dot(_normal, displacement);
        float distance = Vector2.Dot(Corner - _midpoint, _normal);
        Top = Corner - new Vector2(distance / _normal.x, 0f);
        Right = Corner - new Vector2(0f, distance / _normal.y);
    }

    /// <summary>右上极小区域回到完全铺平；其它边沿保留防退化余量，不限制拖动路线。</summary>
    public static Vector2 ClampCorner(Vector2 corner)
    {
        if (corner.x >= 0.997f && corner.y <= 0.003f)
            return new Vector2(1f, 0f);
        return new Vector2(Mathf.Clamp(corner.x, 0f, 0.9995f), Mathf.Clamp(corner.y, 0.0005f, 1f));
    }

    /// <summary>从场景 29 的斜切边反算可拼合的纸角；只计算，不移动数字，不吸附玩家输入。</summary>
    public static bool TryReferenceCorner(Vector2 size, Vector2 seamStart, Vector2 seamEnd, out Vector2 corner)
    {
        corner = Vector2.zero;
        Vector2 direction = seamEnd - seamStart;
        if (size.x <= 0f || size.y <= 0f || Mathf.Abs(direction.x) < 0.0001f)
            return false;
        float slope = direction.y / direction.x;
        float rightY = seamStart.y + (size.x - seamStart.x) * slope;
        float root = Mathf.Sqrt(slope * slope + 1f / FoldDirectionRatio);
        float t = slope >= 0f ? 1f / (root + slope) : FoldDirectionRatio * (root - slope);
        float divisor = FoldDirectionRatio + t * t;
        corner = new Vector2(1f - 2f * FoldDirectionRatio * t * rightY / (divisor * size.x),
            2f * t * t * rightY / (divisor * size.y));
        if (rightY <= 0f || corner.x < 0f || corner.x >= 0.997f || corner.y <= 0.003f || corner.y > 1f)
            return false;
        // 无限延长线重合不代表能拼合：接缝的两端都必须位于实际自由纸边上。
        Level136FoldGeometry reference = new Level136FoldGeometry(size, corner);
        Vector2 edge = reference.Right - reference.Tip;
        float startT = Vector2.Dot(seamStart - reference.Tip, edge) / edge.sqrMagnitude;
        float endT = Vector2.Dot(seamEnd - reference.Tip, edge) / edge.sqrMagnitude;
        return startT >= 0f && startT <= 1f && endT >= 0f && endT <= 1f;
    }

    /// <summary>保留参考图的印刷配准姿态；该位置不用于输入限制、自动移动或胜利判定。</summary>
    public static Vector2 ReferenceCorner(Vector2 size)
    {
        Vector2 corner = new Vector2(size.x, 0f);
        Vector2 top = new Vector2(size.x * 0.704f, 0f);
        Vector2 right = new Vector2(size.x, size.y * 0.6873f);
        Vector2 edge = (right - top).normalized;
        Vector2 normal = new Vector2(edge.y, -edge.x);
        Vector2 direction = new Vector2(normal.x * FoldDirectionRatio, normal.y);
        direction /= Vector2.Dot(normal, direction);
        Vector2 tip = corner - 2f * Vector2.Dot(corner - top, normal) * direction;
        return new Vector2(tip.x / size.x, tip.y / size.y);
    }

    /// <summary>判断点是否在当前纸背内，供局部命中和纹理裁切共用。</summary>
    public bool Contains(Vector2 point)
    {
        if (IsClosed)
            return false;
        Vector2 source = UnfoldPoint(point);
        return source.x >= 0f && source.x <= _size.x && source.y >= 0f && source.y <= _size.y
            && FoldSide(source) >= -0.01f;
    }

    /// <summary>把固定的纸背印刷点从对齐姿态映射到当前纸背，纸角和墨迹始终共用变换。</summary>
    public Vector2 MoveBackInk(Vector2 referencePoint)
    {
        return MoveBackInk(referencePoint, ReferenceCorner(_size));
    }

    /// <summary>采用场景中正面数字推导的参考纸角，保持两份素材的拼接关系。</summary>
    public Vector2 MoveBackInk(Vector2 referencePoint, Vector2 referenceCorner)
    {
        Level136FoldGeometry target = new Level136FoldGeometry(_size, referenceCorner);
        return FoldPoint(target.UnfoldPoint(referencePoint));
    }

    /// <summary>折线正侧为被翻走的原纸，用于原页矩形的多边形裁切。</summary>
    public float FoldSide(Vector2 point) { return IsClosed ? -1f : Vector2.Dot(point - _midpoint, _normal); }

    /// <summary>纸背和印刷共用同一个保面积折叠变换；自由角与手指保持二维对应。</summary>
    public Vector2 FoldPoint(Vector2 source)
    {
        return source - 2f * FoldSide(source) * _foldDirection;
    }

    private Vector2 UnfoldPoint(Vector2 folded)
    {
        // 保面积的反折变换自逆；纸背墨迹与纸面使用同一套配准，不另加尺寸补偿。
        return FoldPoint(folded);
    }
    #endregion

    #region 原始素材配准
    /// <summary>正式素材 Sp_l136_sz_01 的斜切边起点，使用左上原点像素坐标。</summary>
    public static readonly Vector2 InkSeamStart = new Vector2(202.5f, 0f);
    /// <summary>正式素材斜切边的另一端；由 Alpha 切边配准，不使用旧占位图尺寸。</summary>
    public static readonly Vector2 InkSeamEnd = new Vector2(593f, 307f);
    /// <summary>Sp_l136_sz_02 左上角相对 Sp_l136_sz_01 的像素偏移；不能按图片中心对齐。</summary>
    public static readonly Vector2 BackInkOffset = new Vector2(231f, -59f);

    /// <summary>正式素材共用像素坐标转参考页面坐标；仅供参考姿态与几何验证使用。</summary>
    public Vector2 InkPoint(Vector2 sourcePoint)
    {
        Level136FoldGeometry target = new Level136FoldGeometry(_size, ReferenceCorner(_size));
        Vector2 freeEdge = target.Right - target.Tip;
        Vector2 seam = InkSeamEnd - InkSeamStart;
        float angle = Mathf.Atan2(freeEdge.y, freeEdge.x) - Mathf.Atan2(seam.y, seam.x);
        float scale = _size.x * 0.31f / 909f;
        Vector2 delta = (sourcePoint - InkSeamStart) * scale;
        Vector2 rotated = new Vector2(delta.x * Mathf.Cos(angle) - delta.y * Mathf.Sin(angle),
            delta.x * Mathf.Sin(angle) + delta.y * Mathf.Cos(angle));
        return Vector2.Lerp(target.Tip, target.Right, 0.28f) + rotated;
    }

    /// <summary>二维叉积，供纸面和裁切代码使用。</summary>
    public static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
    #endregion
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>仅用于下拉/回弹：长直线配 V 底/右侧相切小圆弧，恒定法向宽度与连续纹理。下坠仍用原曲面。</summary>
internal sealed class Level132TautRopeVisual : IDisposable
{
    private readonly struct Vertex
    {
        public readonly Vector3 Position;
        public readonly float Distance;
        public readonly float Across;

        public Vertex(Vector3 position, float distance, float across)
        {
            Position = position;
            Distance = distance;
            Across = across;
        }
    }

    private readonly Transform _root;
    private readonly GameObject _object;
    private readonly Mesh _mesh;
    private readonly float _leftX;
    private readonly float _rightX;
    private readonly float _restY;
    private readonly float _restZ;
    private readonly float _width;
    private readonly float _textureLength;
    private readonly Vector2 _uvOrigin;
    private readonly Vector2 _uvAcross;
    private readonly Vector2 _uvAlong;
    private readonly Color _color;
    private readonly List<float> _cuts = new List<float>(96);
    private readonly Vertex[] _clipA = new Vertex[8];
    private readonly Vertex[] _clipB = new Vertex[8];
    private readonly List<Vector3> _vertices = new List<Vector3>(96);
    private readonly List<Vector2> _uv = new List<Vector2>(96);
    private readonly List<Color> _colors = new List<Color>(96);
    private readonly List<int> _triangles = new List<int>(192);
    private float _fixedLeftX;
    private float _pullX;
    private float _rightJoinX;
    private float _depth;
    private float _pullSign;
    private float _leftSpan;
    private float _rightSpan;
    private float _radius;
    private float _leftAngle;
    private float _rightAngle;
    private float _leftTangentX;
    private float _bottomRightX;
    private float _rightTangentX;
    private float _leftLineLength;
    private float _bottomArcLength;
    private float _middleLineLength;
    private float _rightArcLength;
    private Vector3 _leftDirection;
    private Vector3 _rightDirection;
    private const float MaxCornerStepDegrees = 4f;

    public Level132TautRopeVisual(SpriteRenderer source, Transform parent, Material material,
        bool extension = false, float extensionOffsetX = 0f)
    {
        _root = parent;
        Bounds bounds = source.sprite.bounds;
        Vector3 a = source.transform.TransformPoint(new Vector3(bounds.center.x, bounds.min.y, 0f));
        Vector3 b = source.transform.TransformPoint(new Vector3(bounds.center.x, bounds.max.y, 0f));
        Vector3 across = source.transform.TransformVector(Vector3.right * bounds.size.x);
        if (extension)
        {
            bool pointsRight = b.x >= a.x;
            Vector3 right = pointsRight ? b : a;
            float length = Mathf.Max(0.1f, Mathf.Abs(b.x - a.x));
            float width = across.magnitude;
            Vector3 start = right + Vector3.right * extensionOffsetX;
            a = pointsRight ? start : start + Vector3.right * length;
            b = pointsRight ? start + Vector3.right * length : start;
            across = (pointsRight ? Vector3.down : Vector3.up) * width;
        }
        if (Mathf.Abs(a.y - b.y) > 0.001f || Mathf.Abs(a.z - b.z) > 0.001f || Mathf.Abs(a.x - b.x) < 0.1f)
            throw new InvalidOperationException("[Level132] Taut pull requires horizontal authored rope pieces; their transforms were retained.");
        _leftX = Mathf.Min(a.x, b.x);
        _rightX = Mathf.Max(a.x, b.x);
        _restY = (a.y + b.y) * 0.5f;
        _restZ = a.z;
        _width = across.magnitude;
        _textureLength = Vector3.Distance(a, b);
        _color = source.color;
        if (_width < 0.0001f)
            throw new InvalidOperationException("[Level132] Taut rope needs a positive source width.");

        // 由 Sprite 顶点/UV 建立映射；不改源图 WrapMode，也不把图集整张纹理 Repeat。
        Vector2[] positions = source.sprite.vertices;
        Vector2[] textureUv = source.sprite.uv;
        Vector2 origin = positions[0];
        Vector2 axisA = positions[1] - origin;
        int third = 2;
        while (third < positions.Length && Mathf.Abs(Cross(axisA, positions[third] - origin)) < 0.000001f) third++;
        if (third == positions.Length)
            throw new InvalidOperationException("[Level132] Taut rope sprite UV basis is degenerate.");
        Vector2 axisB = positions[third] - origin;
        float determinant = Cross(axisA, axisB);
        float lowerX = across.y >= 0f ? bounds.min.x : bounds.max.x;
        float upperX = across.y >= 0f ? bounds.max.x : bounds.min.x;
        float leftY = b.x >= a.x ? bounds.min.y : bounds.max.y;
        float rightY = b.x >= a.x ? bounds.max.y : bounds.min.y;

        Vector2 Map(float x, float y)
        {
            if (source.flipX) x = bounds.min.x + bounds.max.x - x;
            if (source.flipY) y = bounds.min.y + bounds.max.y - y;
            Vector2 relative = new Vector2(x, y) - origin;
            return textureUv[0] + (textureUv[1] - textureUv[0]) * (Cross(relative, axisB) / determinant) +
                (textureUv[third] - textureUv[0]) * (Cross(axisA, relative) / determinant);
        }

        _uvOrigin = Map(lowerX, leftY);
        _uvAcross = Map(upperX, leftY) - _uvOrigin;
        _uvAlong = Map(lowerX, rightY) - _uvOrigin;
        try
        {
            string name = extension ? "Level132TautRopeExtension" : "Level132TautRope";
            _mesh = new Mesh { name = name };
            _mesh.MarkDynamic();
            _object = new GameObject(name);
            _object.SetActive(false);
            _object.layer = source.gameObject.layer;
            _object.transform.SetParent(parent, false);
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = _object.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder;
            var properties = new MaterialPropertyBlock();
            properties.SetTexture("_MainTex", source.sprite.texture);
            Texture2D alpha = source.sprite.associatedAlphaSplitTexture;
            properties.SetTexture("_AlphaTex", alpha != null ? alpha : Texture2D.whiteTexture);
            properties.SetFloat("_UseExternalAlpha", alpha != null ? 1f : 0f);
            renderer.SetPropertyBlock(properties);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Draw(float fixedLeftX, float pullX, float pull, float rightSpan, float cornerRadius)
    {
        _fixedLeftX = fixedLeftX;
        _pullX = pullX;
        _depth = Mathf.Abs(pull);
        _pullSign = pull < 0f ? -1f : 1f;
        _leftSpan = Mathf.Max(0.1f, pullX - fixedLeftX);
        _rightSpan = Mathf.Max(0.1f, rightSpan);
        _rightJoinX = pullX + _rightSpan;
        ConfigureCorners(cornerRadius);

        _vertices.Clear();
        _uv.Clear();
        _colors.Clear();
        _triangles.Clear();
        _cuts.Clear();
        _cuts.Add(_leftX);
        AddCut(_fixedLeftX);
        // 所有首摆共享同一组世界圆弧采样，相邻 Sprite 边界只裁切，不各自生成不同圆角。
        AddCornerCuts(_pullX, _leftAngle, 0f, true);
        AddCornerCuts(_pullX, 0f, _rightAngle, true);
        AddCornerCuts(_rightJoinX, _rightAngle, 0f, false);
        _cuts.Add(_rightX);
        _cuts.Sort();
        for (int i = 0; i < _cuts.Count - 1; i++)
            if (_cuts[i + 1] > _cuts[i]) AddStrip(_cuts[i], _cuts[i + 1]);

        // 同一个 Mesh 连续裁切，不按长度阈值创建/激活整截 Sprite。
        _mesh.Clear();
        _mesh.SetVertices(_vertices);
        _mesh.SetUVs(0, _uv);
        _mesh.SetColors(_colors);
        _mesh.SetTriangles(_triangles, 0);
        _mesh.RecalculateBounds();
        _object.SetActive(true);
    }

    private void ConfigureCorners(float requestedRadius)
    {
        float minimumRadius = _width * 0.5f + 0.01f;
        float maximumRadius = Mathf.Min(_leftSpan, _rightSpan) * 0.45f;
        if (minimumRadius > maximumRadius)
            throw new InvalidOperationException("[Level132] Pull spans are too short for a rounded rope of this width.");
        _radius = Mathf.Clamp(requestedRadius, minimumRadius, maximumRadius);

        // 正拉量的底部圆心为 (P.x, P.y+R)，因此圆弧最低点恰好是原 P，不削高脚下接触点。
        // 从固定 A 求该圆的切线；回弹负拉量仅镜像 Y，Actor/拉点的 X 不参与修正。
        float leftCenterY = _radius - _depth;
        float leftDistance = new Vector2(_leftSpan, leftCenterY).magnitude;
        _leftAngle = -Mathf.Max(0f, Mathf.Asin(Mathf.Clamp01(_radius / leftDistance)) -
            Mathf.Atan2(leftCenterY, _leftSpan));

        // 右圆心为 (B.x, 原绳面-R)，顶点 B 保持原位且切向水平；中间直线取两圆的内公切线。
        float centerDeltaY = _depth - 2f * _radius;
        float centerDistance = new Vector2(_rightSpan, centerDeltaY).magnitude;
        _rightAngle = Mathf.Max(0f, Mathf.Atan2(centerDeltaY, _rightSpan) +
            Mathf.Asin(Mathf.Clamp01(2f * _radius / centerDistance)));
        _leftDirection = AngleDirection(_leftAngle);
        _rightDirection = AngleDirection(_rightAngle);
        _leftTangentX = _pullX + _radius * Mathf.Sin(_leftAngle);
        _bottomRightX = _pullX + _radius * Mathf.Sin(_rightAngle);
        _rightTangentX = _rightJoinX - _radius * Mathf.Sin(_rightAngle);
        _leftLineLength = Mathf.Sqrt(Mathf.Max(0f, leftDistance * leftDistance - _radius * _radius));
        _bottomArcLength = _radius * (_rightAngle - _leftAngle);
        _middleLineLength = Mathf.Sqrt(Mathf.Max(0f, centerDistance * centerDistance - 4f * _radius * _radius));
        _rightArcLength = _radius * _rightAngle;
    }

    private void AddCut(float x)
    {
        if (x > _leftX && x < _rightX) _cuts.Add(x);
    }

    private void AddCornerCuts(float centerX, float fromAngle, float toAngle, bool bottom)
    {
        int segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(toAngle - fromAngle) * Mathf.Rad2Deg / MaxCornerStepDegrees));
        for (int i = 0; i <= segments; i++)
        {
            float angle = i == 0 ? fromAngle : i == segments ? toAngle : Mathf.Lerp(fromAngle, toAngle, (float)i / segments);
            AddCut(centerX + (bottom ? 1f : -1f) * _radius * Mathf.Sin(angle));
        }
    }

    private float BottomAngle(float x) => Mathf.Asin(Mathf.Clamp((x - _pullX) / _radius, -1f, 1f));
    private float RightAngle(float x) => Mathf.Asin(Mathf.Clamp((_rightJoinX - x) / _radius, -1f, 1f));
    private Vector3 AngleDirection(float angle) => new Vector3(Mathf.Cos(angle), _pullSign * Mathf.Sin(angle), 0f);

    private Vector3 Center(float x)
    {
        float height;
        if (x <= _fixedLeftX || x >= _rightJoinX) height = 0f;
        else if (x < _leftTangentX) height = (x - _fixedLeftX) * _leftDirection.y / _leftDirection.x;
        else if (x < _bottomRightX)
            height = _pullSign * (-_depth + _radius * (1f - Mathf.Cos(BottomAngle(x))));
        else if (x < _rightTangentX)
            height = _pullSign * (-_depth + _radius * (1f - _rightDirection.x)) +
                (x - _bottomRightX) * _rightDirection.y / _rightDirection.x;
        else height = _pullSign * _radius * (Mathf.Cos(RightAngle(x)) - 1f);
        return new Vector3(x, _restY + height, _restZ);
    }

    private Vector3 Direction(float x)
    {
        if (x <= _fixedLeftX || x >= _rightJoinX) return Vector3.right;
        if (x < _leftTangentX) return _leftDirection;
        if (x < _bottomRightX) return AngleDirection(BottomAngle(x));
        if (x < _rightTangentX) return _rightDirection;
        return AngleDirection(RightAngle(x));
    }

    private bool OnCorner(float x)
    {
        return (x > _leftTangentX && x < _bottomRightX) || (x > _rightTangentX && x < _rightJoinX);
    }

    private static Vector3 Normal(Vector3 direction) => new Vector3(-direction.y, direction.x, 0f);

    private Vector3 JoinOffset(float x)
    {
        float halfWidth = _width * 0.5f;
        // 固定左端沿原 X 截断，不让旋转后的带宽向左伸进柱子；法向粗细仍保持原值。
        if (x == _fixedLeftX) return Vector3.up * (halfWidth / _leftDirection.x);
        return Normal(Direction(x)) * halfWidth;
    }

    // 从固定左端累计真实路径长度，保证左端纹理相位不随拉动重置。
    private float PathDistance(float x)
    {
        if (x <= _fixedLeftX) return x - _fixedLeftX;
        if (x < _leftTangentX) return (x - _fixedLeftX) / _leftDirection.x;
        if (x < _bottomRightX) return _leftLineLength + _radius * (BottomAngle(x) - _leftAngle);
        float throughBottom = _leftLineLength + _bottomArcLength;
        if (x < _rightTangentX) return throughBottom + (x - _bottomRightX) / _rightDirection.x;
        if (x < _rightJoinX) return throughBottom + _middleLineLength + _radius * (_rightAngle - RightAngle(x));
        return throughBottom + _middleLineLength + _rightArcLength + x - _rightJoinX;
    }

    private void AddStrip(float fromX, float toX)
    {
        Vector3 from = Center(fromX);
        Vector3 to = Center(toX);
        Vector3 direction = Direction((fromX + toX) * 0.5f);
        Vector3 normal = Normal(direction);
        Vector3 startOffset = JoinOffset(fromX);
        Vector3 endOffset = JoinOffset(toX);
        // 保留每张首摆的原相位/方向。pull=0 时映射回该 Sprite，而非换成全新居中平铺。
        float distance = PathDistance(fromX) - (_leftX - _fixedLeftX);
        float endDistance = PathDistance(toX) - (_leftX - _fixedLeftX);
        bool curved = OnCorner((fromX + toX) * 0.5f);
        Vertex Make(Vector3 p) => new Vertex(p, distance + Vector3.Dot(p - from, direction),
            0.5f + Vector3.Dot(p - from, normal) / _width);
        // 圆角按真实中心线弧长连续采样，允许局部纹理弯转；长直线仍使用原正交投影，不拉长整段纹理。
        Vertex a = curved ? new Vertex(from - startOffset, distance, 0f) : Make(from - startOffset);
        Vertex b = curved ? new Vertex(to - endOffset, endDistance, 0f) : Make(to - endOffset);
        Vertex c = curved ? new Vertex(to + endOffset, endDistance, 1f) : Make(to + endOffset);
        Vertex d = curved ? new Vertex(from + startOffset, distance, 1f) : Make(from + startOffset);
        float minimum = Mathf.Min(Mathf.Min(a.Distance, b.Distance), Mathf.Min(c.Distance, d.Distance));
        float maximum = Mathf.Max(Mathf.Max(a.Distance, b.Distance), Mathf.Max(c.Distance, d.Distance));
        int first = Mathf.FloorToInt(minimum / _textureLength);
        int last = Mathf.CeilToInt(maximum / _textureLength) - 1;
        for (int tile = first; tile <= last; tile++)
        {
            _clipA[0] = a; _clipA[1] = b; _clipA[2] = c; _clipA[3] = d;
            int count = Clip(_clipA, 4, _clipB, tile * _textureLength, true);
            count = Clip(_clipB, count, _clipA, (tile + 1) * _textureLength, false);
            if (count < 3) continue;
            int start = _vertices.Count;
            for (int i = 0; i < count; i++)
            {
                Vertex vertex = _clipA[i];
                float along = Mathf.Clamp01((vertex.Distance - tile * _textureLength) / _textureLength);
                _vertices.Add(_root.InverseTransformPoint(vertex.Position));
                _uv.Add(_uvOrigin + _uvAcross * Mathf.Clamp01(vertex.Across) + _uvAlong * along);
                _colors.Add(_color);
            }
            for (int i = 1; i < count - 1; i++)
            {
                _triangles.Add(start); _triangles.Add(start + i); _triangles.Add(start + i + 1);
            }
        }
    }

    private static int Clip(Vertex[] input, int count, Vertex[] output, float edge, bool keepHigher)
    {
        if (count == 0) return 0;
        int result = 0;
        Vertex previous = input[count - 1];
        bool previousInside = keepHigher ? previous.Distance >= edge : previous.Distance <= edge;
        for (int i = 0; i < count; i++)
        {
            Vertex current = input[i];
            bool inside = keepHigher ? current.Distance >= edge : current.Distance <= edge;
            if (inside != previousInside)
            {
                float t = (edge - previous.Distance) / (current.Distance - previous.Distance);
                output[result++] = new Vertex(Vector3.LerpUnclamped(previous.Position, current.Position, t), edge,
                    Mathf.LerpUnclamped(previous.Across, current.Across, t));
            }
            if (inside) output[result++] = current;
            previous = current;
            previousInside = inside;
        }
        return result;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    public void Hide() => _object.SetActive(false);

    public void Dispose()
    {
        if (_mesh != null) UnityEngine.Object.Destroy(_mesh);
        if (_object != null) UnityEngine.Object.Destroy(_object);
    }
}

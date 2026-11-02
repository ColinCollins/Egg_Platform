using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>保留绳子首摆；消费本关渐进组合，前方补齐石刺与绳段，身后复用。</summary>
internal sealed class Level132Track : IDisposable
{
    private sealed class AuthoredRope
    {
        public readonly SpriteRenderer Source;
        public readonly bool OriginalVisible;
        public readonly Bounds RestBounds;
        private readonly Vector3 _a;
        private readonly Vector3 _b;
        private readonly Vector3 _halfWidth;
        public Level132RopeVisual Visual;
        public Level132TautRopeVisual TautVisual;
        public bool Retired;

        public float LeftX => Mathf.Min(_a.x, _b.x);
        public float RightX => Mathf.Max(_a.x, _b.x);
        public float HalfWidth => _halfWidth.magnitude;
        public bool Horizontal => RightX - LeftX > 0.1f && Mathf.Abs(_a.y - _b.y) <= 0.02f;

        public AuthoredRope(SpriteRenderer source)
        {
            Source = source;
            OriginalVisible = source.enabled;
            RestBounds = source.bounds;
            Bounds bounds = source.sprite.bounds;
            _a = source.transform.TransformPoint(new Vector3(bounds.center.x, bounds.min.y, 0f));
            _b = source.transform.TransformPoint(new Vector3(bounds.center.x, bounds.max.y, 0f));
            _halfWidth = source.transform.TransformVector(Vector3.right * bounds.extents.x);
        }

        public float SurfaceY(float x, bool below = false)
        {
            float t = Mathf.Abs(_b.x - _a.x) < 0.00001f ? 0f : Mathf.Clamp01((x - _a.x) / (_b.x - _a.x));
            return Mathf.Lerp(_a.y, _b.y, t) + Mathf.Abs(_halfWidth.y) * (below ? -1f : 1f);
        }
    }

    private sealed class RopePiece
    {
        public Level132RopeVisual Visual;
        public float OffsetX;
        public float LeftX => Visual.LeftX + OffsetX;
        public float RightX => Visual.RightX + OffsetX;
    }

    private const int OpeningGroupCount = 3;
    private Level132RunCourse _course;
    private double _runMeters;

    private readonly Transform _root;
    private readonly GameObject _runtime;
    private readonly Transform _templatesRoot;
    private readonly Func<float, float> _displacement;
    private readonly Material _curveMaterial;
    private readonly Plane[] _viewPlanes = new Plane[6];
    private readonly List<AuthoredRope> _authoredRopes = new List<AuthoredRope>();
    private readonly List<RopePiece> _activeRopes = new List<RopePiece>();
    private readonly List<RopePiece> _allStreamRopes = new List<RopePiece>();
    private readonly Stack<RopePiece> _ropePool = new Stack<RopePiece>();
    private readonly List<EdgeCollider2D> _approachSupport = new List<EdgeCollider2D>();
    private readonly List<Level132TautRopeVisual> _pullExtensions = new List<Level132TautRopeVisual>();
    private int _pullExtensionCount;
    private readonly List<SpriteRenderer> _activeObstacles = new List<SpriteRenderer>();
    private readonly Stack<SpriteRenderer> _obstaclePool = new Stack<SpriteRenderer>();
    private SpriteRenderer _obstacleTemplate;
    private float _obstacleWidth;
    private bool _courseConfigured;
    private AuthoredRope _rightmost;
    private AuthoredRope _preparationRope;
    private SpriteRenderer _ropeTemplate;
    private float _ropeStartX;
    private float _ropeLength;
    private int _nextRopeIndex;
    private bool _obstaclesComplete;
    private bool _running;
    private bool _disposed;
    private float _runOriginX;
    private float _goal;
    private float _pullCenterX;
    private float _pullLeftX;
    private float _pull;
    private float _pullSpan;
    private float _fallFront;
    private bool _chasing;
    private const float RecycleMargin = 3f;
    private const float FallenRopeRetentionSeconds = 4f;
    private const float ObstacleRopeClearance = 0.08f;
    public float MaximumRopeHalfWidth { get; private set; }

    public Level132Track(Transform parent, Transform layout, Material curveMaterial, SpriteRenderer obstacleVisual)
    {
        _root = parent;
        _curveMaterial = curveMaterial;
        _runtime = new GameObject("Level132RuntimeTrack");
        _runtime.transform.SetParent(parent, false);
        _templatesRoot = new GameObject("Level132TrackTemplates").transform;
        _templatesRoot.SetParent(_runtime.transform, false);
        _templatesRoot.gameObject.SetActive(false);
        _displacement = RopeDisplacement;
        try
        {
            if (_curveMaterial == null || _curveMaterial.shader == null || !_curveMaterial.shader.isSupported)
                throw new InvalidOperationException("[Level132] A supported rope-curve mesh material must be assigned.");
            if (obstacleVisual == null || obstacleVisual.sprite == null || obstacleVisual.drawMode != SpriteDrawMode.Simple)
                throw new InvalidOperationException("[Level132] Assign the original simple obstacle SpriteRenderer prefab.");
            _obstacleTemplate = CopySpriteTemplate(obstacleVisual);
            _obstacleWidth = SpriteWorldBounds(_obstacleTemplate).size.x;
            if (_obstacleWidth <= 0.01f)
                throw new InvalidOperationException("[Level132] Obstacle template must have a positive world width.");
            // 绳子只读缓存；障碍由准备点绑定后的固定开局负责，不混入旧的散块首摆。
            foreach (var sprite in layout.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!sprite.enabled || !sprite.gameObject.activeInHierarchy || sprite.sprite == null) continue;
                if (sprite.name.StartsWith("Rope_", StringComparison.Ordinal))
                    CaptureRope(sprite);
                else if (sprite.name.StartsWith("Obstacle_", StringComparison.Ordinal))
                    throw new InvalidOperationException("[Level132] Remove legacy Obstacle_ instances before using the grouped course.");
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private AuthoredRope CaptureRope(SpriteRenderer source)
    {
        var rope = _authoredRopes.Find(item => item.Source == source);
        if (rope != null) return rope;
        rope = new AuthoredRope(source);
        _authoredRopes.Add(rope);
        MaximumRopeHalfWidth = Mathf.Max(MaximumRopeHalfWidth, rope.HalfWidth);
        if (_rightmost == null || rope.RightX > _rightmost.RightX) _rightmost = rope;
        return rope;
    }

    // 仅由独立的准备点绑定成功后调用；不影响路线游标，不触发任何前方追加。
    public void ConfigurePreparation(SpriteRenderer source, SpriteRenderer leftSource, float fixedLeftX)
    {
        _preparationRope = CaptureRope(source); // 显式准备绳即使改名也保留身份。
        CaptureRope(leftSource);
        _pullLeftX = fixedLeftX;
        foreach (var edge in _approachSupport) UnityEngine.Object.Destroy(edge.gameObject);
        _approachSupport.Clear();
        // 入场跳跃可能越过锁点；所有已有水平绳段均须提供完整承托。
        foreach (var rope in _authoredRopes)
            if (rope.Horizontal)
                AddApproachSupport(rope);
    }

    private void AddApproachSupport(AuthoredRope rope)
    {
        var go = new GameObject("Level132ApproachSupport");
        go.layer = 3;
        go.transform.SetParent(_runtime.transform, false);
        var effector = go.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        var edge = go.AddComponent<EdgeCollider2D>();
        float right = rope.RightX;
        edge.points = new Vector2[]
        {
            go.transform.InverseTransformPoint(new Vector3(rope.LeftX, rope.SurfaceY(rope.LeftX), _root.position.z)),
            go.transform.InverseTransformPoint(new Vector3(right, rope.SurfaceY(right), _root.position.z))
        };
        edge.usedByEffector = true;
        edge.enabled = false;
        _approachSupport.Add(edge);
    }

    public void SetApproachSupport(bool enabled)
    {
        foreach (var edge in _approachSupport) edge.enabled = enabled;
    }

    public bool IsStandingOnApproachRope(Collider2D actorCollider)
    {
        if (_disposed || actorCollider == null || !actorCollider.enabled) return false;
        foreach (var edge in _approachSupport)
        {
            if (edge != null && edge.enabled && edge.gameObject.activeInHierarchy &&
                actorCollider.IsTouching(edge) &&
                Mathf.Abs(actorCollider.bounds.min.y - edge.bounds.max.y) < 0.18f)
                return true;
        }
        return false;
    }

    // 只为屏外下拉衔接补纯视觉副本；不启动跑绳，不触碰路线游标、Collider、障碍组或下坠池。
    public void ConfigurePullView(float rightJoinX)
    {
        if (_disposed || _running || _rightmost == null || _rightmost.RightX - _rightmost.LeftX < 0.1f)
            throw new InvalidOperationException("[Level132] Configure the pull view before running, with a valid authored rope tail.");
        float length = _rightmost.RightX - _rightmost.LeftX;
        int required = Mathf.Max(0, Mathf.CeilToInt((rightJoinX + 1f - _rightmost.RightX) / length));
        while (_pullExtensions.Count < required)
        {
            var visual = new Level132TautRopeVisual(_rightmost.Source, _runtime.transform, _curveMaterial,
                true, _pullExtensions.Count * length);
            _pullExtensions.Add(visual);
        }
        _pullExtensionCount = required;
    }

    public float SurfaceY(float x, bool below = false)
    {
        if (_preparationRope != null && x >= _preparationRope.LeftX && x <= _preparationRope.RightX)
            return _preparationRope.SurfaceY(x, below);
        foreach (var rope in _authoredRopes)
            if (x >= rope.LeftX && x <= rope.RightX) return rope.SurfaceY(x, below);
        return _rightmost != null ? _rightmost.SurfaceY(_rightmost.RightX, below) : _root.position.y;
    }

    /// <summary>准备点绑定后预摆三组教学；组合与速度共用 course，不挪动任何人工绳段。</summary>
    public void ConfigureObstacleCourse(float originX, float goal, Level132RunCourse course)
    {
        if (_courseConfigured) return;
        if (_disposed || _preparationRope == null)
            throw new InvalidOperationException("[Level132] Bind the preparation rope before placing obstacle groups.");
        _runOriginX = originX;
        _course = course ?? throw new ArgumentNullException(nameof(course));
        _goal = goal;
        for (int i = 0; i < OpeningGroupCount; i++)
            if (!AppendObstacleGroup()) break;
        _courseConfigured = true;
    }

    /// <summary>每次跑动小步提交真实里程，供加速后的绳尾下坠按实际经过时间计算。</summary>
    public void SetRunMeters(double meters) => _runMeters = meters;

    public void BeginRun()
    {
        if (_running) return;
        if (_disposed || _rightmost == null || _rightmost.RightX - _rightmost.LeftX < 0.1f)
            throw new InvalidOperationException("[Level132] Forward loop needs an authored rope with a valid right edge.");
        if (!_courseConfigured)
            throw new InvalidOperationException("[Level132] Configure the fixed obstacle opening before running.");

        _ropeTemplate = CopySpriteTemplate(_rightmost.Source);
        _ropeStartX = _rightmost.RightX;
        _ropeLength = _rightmost.RightX - _rightmost.LeftX;
        _nextRopeIndex = 0;
        _running = true;
    }

    // 调用者在移动/扫掠之前提供所需覆盖右边界；绳和障碍使用互不依赖的追加游标。
    public void EnsureAhead(float requiredRightX)
    {
        if (!_running || _disposed) return;
        while (_ropeStartX + _nextRopeIndex * _ropeLength < requiredRightX)
        {
            RopePiece piece;
            if (_ropePool.Count > 0) piece = _ropePool.Pop();
            else
            {
                piece = new RopePiece { Visual = new Level132RopeVisual(_ropeTemplate, _runtime.transform, _curveMaterial, true) };
                _allStreamRopes.Add(piece);
            }
            piece.OffsetX = _nextRopeIndex * _ropeLength;
            _nextRopeIndex++;
            _activeRopes.Add(piece); // 取出池时不展示旧网格；LateUpdate 先重绘再显示。
        }

        while (!_obstaclesComplete && _runOriginX + _course.NextLeft <= requiredRightX)
        {
            if (!AppendObstacleGroup()) break;
        }
    }

    private bool AppendObstacleGroup()
    {
        // 整组预检，不在终点前留下半组，也不把任何刺块伸进最后十二米。
        if (!_course.TryPlanNext(_obstacleWidth, _goal))
        {
            _obstaclesComplete = true;
            return false;
        }
        foreach (var obstacle in _course.Group)
            PlaceObstacle(_runOriginX + obstacle.Left, obstacle.Upper);
        return true;
    }

    private void PlaceObstacle(float leftX, bool upper)
    {
        var sprite = _obstaclePool.Count > 0 ? _obstaclePool.Pop() :
            UnityEngine.Object.Instantiate(_obstacleTemplate, _runtime.transform, false);
        sprite.name = $"Obstacle_Group{_course.GroupNumber:D4}_{(upper ? "Upper" : "Lower")}";
        Bounds bounds = SpriteWorldBounds(sprite);
        float edgeY = SurfaceY(leftX + bounds.extents.x, !upper) + (upper ? ObstacleRopeClearance : -ObstacleRopeClearance);
        // 上下均保持同一素材朝向和 Scale，只平移；以下沿/上沿分别贴近绳子两侧。
        sprite.transform.position += new Vector3(leftX - bounds.min.x,
            edgeY - (upper ? bounds.min.y : bounds.max.y), 0f);
        sprite.gameObject.SetActive(true);
        _activeObstacles.Add(sprite);
    }

    // 模板和池中对象未启用时也能计算，不依赖 Renderer 的上一帧缓存包围盒。
    private static Bounds SpriteWorldBounds(SpriteRenderer sprite)
    {
        Bounds local = sprite.sprite.bounds;
        Vector3 center = local.center;
        if (sprite.flipX) center.x = -center.x;
        if (sprite.flipY) center.y = -center.y;
        Vector3 x = sprite.transform.TransformVector(Vector3.right * local.extents.x);
        Vector3 y = sprite.transform.TransformVector(Vector3.up * local.extents.y);
        return new Bounds(sprite.transform.TransformPoint(center), 2f * new Vector3(
            Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y), Mathf.Abs(x.z) + Mathf.Abs(y.z)));
    }

    // 只在本次运动完成扫掠之后调用。使用实际相机左缘，不用预测镜头回收仍可见对象。
    public void RecycleBehind(Camera camera, float actorX, float fallFrontX)
    {
        if (!_running || _disposed || camera == null) return;
        float cameraLeftX = camera.transform.position.x - camera.orthographicSize * camera.aspect;
        GeometryUtility.CalculateFrustumPlanes(camera, _viewPlanes);
        float behind = Mathf.Min(cameraLeftX - RecycleMargin, actorX - 4f);
        for (int i = _activeObstacles.Count - 1; i >= 0; i--)
        {
            var obstacle = _activeObstacles[i];
            if (obstacle.bounds.max.x >= behind) continue;
            obstacle.gameObject.SetActive(false);
            _obstaclePool.Push(obstacle);
            _activeObstacles.RemoveAt(i);
        }
        float ropeBehind = Mathf.Min(behind, fallFrontX - _course.MaximumSpeed * FallenRopeRetentionSeconds);
        for (int i = _activeRopes.Count - 1; i >= 0; i--)
        {
            var piece = _activeRopes[i];
            if (piece.RightX >= ropeBehind) continue;
            if (!piece.Visual.HasDrawn || GeometryUtility.TestPlanesAABB(_viewPlanes, piece.Visual.WorldBounds)) continue;
            piece.Visual.Hide();
            _ropePool.Push(piece);
            _activeRopes.RemoveAt(i);
        }
        // 首摆有限对象只隐藏保留以便清理还原，新增绳段则在池中复用；不搬动首摆弯曲尾段。
        foreach (var rope in _authoredRopes)
        {
            if (rope.Retired || rope.RightX >= ropeBehind) continue;
            if (GeometryUtility.TestPlanesAABB(_viewPlanes, rope.RestBounds) ||
                (rope.Visual != null && rope.Visual.HasDrawn && GeometryUtility.TestPlanesAABB(_viewPlanes, rope.Visual.WorldBounds))) continue;
            rope.Retired = true;
            rope.Source.enabled = false;
            rope.Visual?.Hide();
        }
    }

    public bool HitsObstacle(Vector2 from, Vector2 to, Vector2 actorHalfSize,
        out Vector2 impactPoint, out SpriteRenderer hitObstacle)
    {
        impactPoint = default;
        hitObstacle = null;
        foreach (var obstacle in _activeObstacles)
        {
            if (!obstacle.gameObject.activeInHierarchy) continue;
            Bounds bounds = obstacle.bounds;
            Vector2 min = (Vector2)bounds.min - actorHalfSize;
            Vector2 max = (Vector2)bounds.max + actorHalfSize;
            float enter = 0f;
            float exit = 1f;
            if (Clip(from.x, to.x - from.x, min.x, max.x, ref enter, ref exit) &&
                Clip(from.y, to.y - from.y, min.y, max.y, ref enter, ref exit))
            {
                // 只补视觉反馈数据；保留原命中顺序、包围盒膨胀及扫掠判定。
                Vector2 centerAtImpact = Vector2.Lerp(from, to, enter);
                impactPoint = bounds.ClosestPoint(new Vector3(centerAtImpact.x, centerAtImpact.y, bounds.center.z));
                hitObstacle = obstacle;
                return true;
            }
        }
        return false;
    }

    private static bool Clip(float origin, float delta, float min, float max, ref float enter, ref float exit)
    {
        if (Mathf.Abs(delta) < 0.000001f) return origin >= min && origin <= max;
        float a = (min - origin) / delta;
        float b = (max - origin) / delta;
        enter = Mathf.Max(enter, Mathf.Min(a, b));
        exit = Mathf.Min(exit, Mathf.Max(a, b));
        return enter <= exit;
    }

    // 绘制只更新已有对象；屏外下拉副本由 ConfigurePullView 配置，不在本方法生成路线内容。
    public void DrawRope(float pullCenterX, float pull, float pullSpan, bool chasing, float fallFrontX, float cornerRadius)
    {
        _pullCenterX = pullCenterX;
        _pull = pull;
        _pullSpan = pullSpan;
        _chasing = chasing;
        _fallFront = fallFrontX;
        for (int i = 0; i < _pullExtensions.Count; i++)
        {
            if (!chasing && i < _pullExtensionCount)
                _pullExtensions[i].Draw(_pullLeftX, pullCenterX, pull, pullSpan, cornerRadius);
            else
                _pullExtensions[i].Hide();
        }
        foreach (var rope in _authoredRopes)
        {
            if (rope.Retired) continue;
            bool deform = (Mathf.Abs(pull) > 0.0001f && rope.RightX > _pullLeftX && rope.LeftX < pullCenterX + pullSpan) ||
                (chasing && rope.LeftX < fallFrontX);
            if (deform)
            {
                try
                {
                    if (!chasing)
                    {
                        if (rope.TautVisual == null) rope.TautVisual = new Level132TautRopeVisual(rope.Source, _runtime.transform, _curveMaterial);
                        rope.TautVisual.Draw(_pullLeftX, pullCenterX, pull, pullSpan, cornerRadius);
                        rope.Visual?.Hide();
                    }
                    else
                    {
                        // 跑绳下坠保留原曲面、原位移函数与 UV，不使用准备期的直线绘制。
                        if (rope.Visual == null) rope.Visual = new Level132RopeVisual(rope.Source, _runtime.transform, _curveMaterial, false, pullCenterX);
                        rope.Visual.Draw(0f, _displacement);
                        rope.TautVisual?.Hide();
                    }
                    rope.Source.enabled = false; // 先提交可绘制网格，再交接原 Sprite。
                }
                catch
                {
                    rope.Source.enabled = rope.OriginalVisible;
                    rope.Visual?.Hide();
                    rope.TautVisual?.Hide();
                    throw;
                }
            }
            else
            {
                rope.Source.enabled = rope.OriginalVisible;
                rope.Visual?.Hide();
                rope.TautVisual?.Hide();
            }
        }
        // 活动绳交给 Unity 正常裁剪，不再用未变形端点手动关闭渲染。
        foreach (var piece in _activeRopes)
            piece.Visual.Draw(piece.OffsetX, _displacement);
    }

    private float RopeDisplacement(float x)
    {
        float weight = 0f;
        if (x > _pullLeftX && x <= _pullCenterX)
        {
            float t = Mathf.Clamp01((x - _pullLeftX) / Mathf.Max(0.1f, _pullCenterX - _pullLeftX));
            weight = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t);
        }
        else if (x > _pullCenterX)
        {
            float t = Mathf.Clamp01((x - _pullCenterX) / Mathf.Max(0.1f, _pullSpan));
            weight = 0.5f + 0.5f * Mathf.Cos(Mathf.PI * t);
        }
        // 下拉及回弹共用权重：固定左端为 0，准备点为 1；不限制跑绳后的下坠项。
        float y = -_pull * weight;
        if (_chasing && x < _fallFront)
        {
            float age = (float)(_course.TimeAtDistance(_runMeters) -
                _course.TimeAtDistance(_runMeters - (_fallFront - x)));
            float settle = 1f - Mathf.Exp(-4f * age * age);
            y -= 30f * (1f - Mathf.Exp(-0.15f * age * age));
            y += 0.12f * settle * Mathf.Exp(-1.5f * age) * Mathf.Sin(8f * age);
        }
        return y;
    }

    private SpriteRenderer CopySpriteTemplate(SpriteRenderer source)
    {
        var go = new GameObject(source.name + "_Template");
        go.SetActive(false);
        go.layer = source.gameObject.layer;
        go.transform.SetParent(source.transform.parent, false);
        go.transform.localPosition = source.transform.localPosition;
        go.transform.localRotation = source.transform.localRotation;
        go.transform.localScale = source.transform.localScale;
        go.transform.SetParent(_templatesRoot, true);
        var sprite = go.AddComponent<SpriteRenderer>();
        sprite.sprite = source.sprite;
        sprite.sharedMaterials = source.sharedMaterials;
        sprite.color = source.color;
        sprite.flipX = source.flipX;
        sprite.flipY = source.flipY;
        sprite.drawMode = source.drawMode;
        sprite.size = source.size;
        sprite.tileMode = source.tileMode;
        sprite.adaptiveModeThreshold = source.adaptiveModeThreshold;
        sprite.maskInteraction = source.maskInteraction;
        sprite.spriteSortPoint = source.spriteSortPoint;
        sprite.sortingLayerID = source.sortingLayerID;
        sprite.sortingOrder = source.sortingOrder;
        var properties = new MaterialPropertyBlock();
        source.GetPropertyBlock(properties);
        sprite.SetPropertyBlock(properties);
        return sprite;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var rope in _authoredRopes)
        {
            if (rope.Source != null) rope.Source.enabled = rope.OriginalVisible;
            rope.Visual?.Dispose();
            rope.TautVisual?.Dispose();
        }
        foreach (var piece in _allStreamRopes) piece.Visual.Dispose();
        foreach (var visual in _pullExtensions) visual.Dispose();
        if (_runtime != null) UnityEngine.Object.Destroy(_runtime);
    }
}

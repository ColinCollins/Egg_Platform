using UnityEngine;

/// <summary>137 私有的二维扫掠与视野计算；不查询物理世界，不让地面或绳子参与钩取。</summary>
internal static class Level137Geometry
{
    /// <summary>读取 SpriteRenderer 的未变换局部包围盒，并考虑 DrawMode 与翻转方向。</summary>
    /// <param name="renderer">本关已绑定的 SpriteRenderer。</param>
    /// <returns>可供本关扫掠计算使用的局部包围盒。</returns>
    public static Bounds SpriteLocalBounds(SpriteRenderer renderer)
    {
        Bounds local = renderer.sprite.bounds;
        if (renderer.drawMode != SpriteDrawMode.Simple) local.size = renderer.size;
        Vector3 center = local.center;
        if (renderer.flipX) center.x = -center.x;
        if (renderer.flipY) center.y = -center.y;
        local.center = center;
        return local;
    }

    /// <summary>
    /// 读取 Spine 当前姿态的局部包围盒；提供 slot 前缀时只统计指定槽位。
    /// 不修改共享 SkeletonDataAsset，只读取当前实例姿态。
    /// </summary>
    /// <param name="animation">已经完成 Skeleton 姿态更新的实例。</param>
    /// <param name="slotPrefix">可选的槽位名前缀；为空时统计整个骨架。</param>
    /// <returns>当前 Spine 姿态的局部包围盒。</returns>
    public static Bounds SkeletonLocalBounds(Spine.Unity.SkeletonAnimation animation, string slotPrefix = null)
    {
        float[] vertices = null;
        animation.Skeleton.UpdateWorldTransform(Spine.Skeleton.Physics.Pose);
        if (slotPrefix != null)
        {
            // 门资源还包含雾、星光和吸入特效；不能把整份 Skeleton 的区域当作关门尺寸。
            bool found = false;
            Bounds bounds = new Bounds();
            foreach (Spine.Slot slot in animation.Skeleton.DrawOrder)
            {
                if (!slot.Bone.Active || slot.A <= 0f ||
                    !slot.Data.Name.StartsWith(slotPrefix, System.StringComparison.Ordinal)) continue;
                int count;
                if (slot.Attachment is Spine.RegionAttachment region && region.A > 0f)
                {
                    count = 8;
                    if (vertices == null || vertices.Length < count) vertices = new float[count];
                    region.ComputeWorldVertices(slot, vertices, 0, 2);
                }
                else if (slot.Attachment is Spine.MeshAttachment mesh && mesh.A > 0f)
                {
                    count = mesh.WorldVerticesLength;
                    if (vertices == null || vertices.Length < count) vertices = new float[count];
                    mesh.ComputeWorldVertices(slot, 0, count, vertices, 0, 2);
                }
                else continue;
                for (int i = 0; i < count; i += 2)
                {
                    Vector3 point = new Vector3(vertices[i], vertices[i + 1], 0f);
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }
        animation.Skeleton.GetBounds(out float x, out float y, out float width, out float height,
            ref vertices, new Spine.SkeletonClipping());
        return new Bounds(new Vector3(x + width * 0.5f, y + height * 0.5f, 0f), new Vector3(width, height, 0f));
    }

    // 连续 SAT：目标保持素材的实际旋转/缩放，普通钩尖 halfSize 为零。
    // exit 不截断到本帧终点，用于计算小目标内部可深入的长度。
    /// <summary>
    /// 对保持旋转的二维矩形目标执行连续 SAT 线段扫掠，返回进入和离开目标的归一化比例。
    /// 不查询 Physics2D，也不改变任何 Collider 状态。
    /// </summary>
    /// <param name="from">本帧扫掠起点。</param>
    /// <param name="to">本帧扫掠终点。</param>
    /// <param name="halfSize">扫掠主体在世界轴向的半尺寸；钩尖点应传零。</param>
    /// <param name="frame">目标局部包围盒所属 Transform。</param>
    /// <param name="local">目标在 frame 局部坐标中的包围盒。</param>
    /// <param name="enter">返回进入目标的线段比例。</param>
    /// <param name="exit">返回离开目标的线段比例，可超过当前 [0,1] 范围。</param>
    /// <returns>线段与目标相交时返回 true。</returns>
    public static bool SweepShape(Vector2 from, Vector2 to, Vector2 halfSize,
        Transform frame, Bounds local, out float enter, out float exit)
    {
        Vector2 center = frame.TransformPoint(local.center);
        Vector2 x = frame.TransformVector(Vector3.right * local.extents.x);
        Vector2 y = frame.TransformVector(Vector3.up * local.extents.y);
        enter = 0f;
        exit = float.PositiveInfinity;
        return ShapeAxis(Vector2.right, from, to, halfSize, center, x, y, ref enter, ref exit) &&
               ShapeAxis(Vector2.up, from, to, halfSize, center, x, y, ref enter, ref exit) &&
               ShapeAxis(new Vector2(-x.y, x.x).normalized, from, to, halfSize, center, x, y, ref enter, ref exit) &&
               ShapeAxis(new Vector2(-y.y, y.x).normalized, from, to, halfSize, center, x, y, ref enter, ref exit) &&
               enter <= 1f && exit >= 0f;
    }

    private static bool ShapeAxis(Vector2 axis, Vector2 from, Vector2 to, Vector2 halfSize,
        Vector2 center, Vector2 x, Vector2 y, ref float enter, ref float exit)
    {
        float radius = Mathf.Abs(Vector2.Dot(axis, x)) + Mathf.Abs(Vector2.Dot(axis, y)) +
                       Mathf.Abs(axis.x) * halfSize.x + Mathf.Abs(axis.y) * halfSize.y;
        float middle = Vector2.Dot(axis, center);
        return Slab(Vector2.Dot(axis, from), Vector2.Dot(axis, to - from),
            middle - radius, middle + radius, ref enter, ref exit);
    }

    /// <summary>将局部包围盒按 Transform 的位置、旋转和缩放转换为世界轴对齐包围盒。</summary>
    /// <param name="root">局部包围盒所属 Transform。</param>
    /// <param name="local">root 局部坐标中的包围盒。</param>
    /// <returns>转换后的世界轴对齐包围盒。</returns>
    public static Bounds TransformBounds(Transform root, Bounds local)
    {
        Vector3 x = root.TransformVector(Vector3.right * local.extents.x);
        Vector3 y = root.TransformVector(Vector3.up * local.extents.y);
        return new Bounds(root.TransformPoint(local.center), new Vector3(
            2f * (Mathf.Abs(x.x) + Mathf.Abs(y.x)),
            2f * (Mathf.Abs(x.y) + Mathf.Abs(y.y)), 0f));
    }

    /// <summary>返回 SpriteRenderer 当前姿态的世界轴对齐包围盒。</summary>
    /// <param name="renderer">目标 SpriteRenderer。</param>
    /// <returns>Sprite 的世界轴对齐包围盒。</returns>
    public static Bounds SpriteBounds(SpriteRenderer renderer)
    {
        return TransformBounds(renderer.transform, SpriteLocalBounds(renderer));
    }

    /// <summary>返回 BoxCollider2D 当前姿态的世界轴对齐包围盒。</summary>
    /// <param name="collider">目标 BoxCollider2D。</param>
    /// <returns>Collider 的世界轴对齐包围盒。</returns>
    public static Bounds BoxBounds(BoxCollider2D collider)
    {
        return TransformBounds(collider.transform, new Bounds(collider.offset, collider.size));
    }

    /// <summary>按二维 X/Y 范围判断两个世界包围盒是否相交。</summary>
    /// <param name="a">第一个世界包围盒。</param>
    /// <param name="b">第二个世界包围盒。</param>
    /// <returns>两个包围盒在 X/Y 平面相交时返回 true。</returns>
    public static bool Overlaps(Bounds a, Bounds b)
    {
        return a.min.x <= b.max.x && a.max.x >= b.min.x &&
               a.min.y <= b.max.y && a.max.y >= b.min.y;
    }

    // 返回线段进入扩展 AABB 的第一个比例，避免低帧率跨过小钥匙/星星。
    /// <summary>
    /// 对世界轴对齐包围盒执行线段扫掠；保留给局部几何检查使用，不参与 Unity 物理回调。
    /// </summary>
    /// <param name="from">扫掠起点。</param>
    /// <param name="to">扫掠终点。</param>
    /// <param name="halfSize">扫掠主体半尺寸。</param>
    /// <param name="target">目标世界包围盒。</param>
    /// <param name="fraction">返回首次进入目标的线段比例。</param>
    /// <returns>扫掠主体与目标相交时返回 true。</returns>
    public static bool Sweep(Vector2 from, Vector2 to, Vector2 halfSize, Bounds target, out float fraction)
    {
        Vector2 min = (Vector2)target.min - halfSize;
        Vector2 max = (Vector2)target.max + halfSize;
        Vector2 delta = to - from;
        float enter = 0f;
        float exit = 1f;
        bool hit = Slab(from.x, delta.x, min.x, max.x, ref enter, ref exit) &&
                   Slab(from.y, delta.y, min.y, max.y, ref enter, ref exit);
        fraction = enter;
        return hit;
    }

    private static bool Slab(float origin, float delta, float min, float max, ref float enter, ref float exit)
    {
        if (Mathf.Abs(delta) < 0.000001f) return origin >= min && origin <= max;
        float a = (min - origin) / delta;
        float b = (max - origin) / delta;
        enter = Mathf.Max(enter, Mathf.Min(a, b));
        exit = Mathf.Min(exit, Mathf.Max(a, b));
        return enter <= exit;
    }

    /// <summary>与 CameraGizmoDrawer 的 1920×1080 黑框一致，不读取长屏 Camera.aspect。</summary>
    /// <summary>按项目固定 1920×1080 比例计算本关使用的参考视野矩形。</summary>
    /// <param name="camera">提供正交尺寸和世界中心的相机。</param>
    /// <returns>不读取长屏 aspect 的 16:9 参考视野。</returns>
    public static Rect ReferenceView(Camera camera)
    {
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * (1920f / 1080f);
        Vector3 center = camera.transform.position;
        return Rect.MinMaxRect(center.x - halfWidth, center.y - halfHeight,
            center.x + halfWidth, center.y + halfHeight);
    }

    /// <summary>
    /// 计算包围盒沿指定方向仍能在参考视野内移动的最大距离。
    /// </summary>
    /// <param name="view">本关固定比例参考视野。</param>
    /// <param name="body">待移动主体的世界包围盒。</param>
    /// <param name="direction">单位或非单位移动方向。</param>
    /// <param name="margin">距离视野边界保留的安全边距。</param>
    /// <returns>不越出视野时允许的最大非负距离。</returns>
    public static float TravelInside(Rect view, Bounds body, Vector2 direction, float margin)
    {
        float distance = float.PositiveInfinity;
        if (direction.x > 0.000001f)
            distance = Mathf.Min(distance, (view.xMax - margin - body.max.x) / direction.x);
        else if (direction.x < -0.000001f)
            distance = Mathf.Min(distance, (view.xMin + margin - body.min.x) / direction.x);
        if (direction.y > 0.000001f)
            distance = Mathf.Min(distance, (view.yMax - margin - body.max.y) / direction.y);
        else if (direction.y < -0.000001f)
            distance = Mathf.Min(distance, (view.yMin + margin - body.min.y) / direction.y);
        return Mathf.Max(0f, distance);
    }

    /// <summary>计算带额外 SmoothStep 的兼容弧线；当前主流程优先使用已缓动的 ArcAtProgress。</summary>
    /// <param name="from">弧线起点。</param>
    /// <param name="to">弧线终点。</param>
    /// <param name="progress">未缓动的 0 到 1 进度。</param>
    /// <param name="height">弧线最高点的世界高度。</param>
    /// <returns>当前进度对应的世界位置。</returns>
    public static Vector3 Arc(Vector3 from, Vector3 to, float progress, float height)
    {
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        return ArcAtProgress(from, to, t, height);
    }

    // 进度已由Tween缓动；这里只求轨迹，避免重复缓动改变节奏。
    /// <summary>按已由调用方决定的进度计算抛物线位置，不重复施加时间缓动。</summary>
    /// <param name="from">弧线起点。</param>
    /// <param name="to">弧线终点。</param>
    /// <param name="progress">已经缓动的 0 到 1 进度。</param>
    /// <param name="height">弧线最高点的世界高度。</param>
    /// <returns>当前进度对应的世界位置。</returns>
    public static Vector3 ArcAtProgress(Vector3 from, Vector3 to, float progress, float height)
    {
        float t = Mathf.Clamp01(progress);
        return Vector3.LerpUnclamped(from, to, t) + Vector3.up * (4f * height * t * (1f - t));
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Level 131 的纯视觉环境循环。只复制 Transform、SpriteRenderer 和 SortingGroup，
/// 不克隆玩法组件、碰撞体、门或拾取物；装饰复用有限副本，纯色蒙版单张连续延展。
/// 由关卡控制器在相机更新之后 Tick，所有位置使用关卡局部坐标以兼容世界重定位。
/// </summary>
internal sealed class Level131BackgroundLoop
{
    private readonly Transform levelRoot;
    private readonly Camera camera;
    private readonly Transform loopRoot;
    private readonly List<Layer> layers = new();
    private readonly float initialCameraX;
    private SpriteRenderer continuousMask;
    private Vector3 maskOrigin;
    private Vector3 maskScale;
    private Bounds maskBounds;
    private float maskRight;
    private float maskBottom;

    private sealed class Layer
    {
        public Transform Source;
        public Transform Template;
        public Vector3 Origin;
        public Bounds Bounds;
        public bool FollowCamera;
        public SpriteRenderer[] CoverRenderers;
        public readonly List<Transform> Tiles = new();
    }

    public Level131BackgroundLoop(Transform root, Camera worldCamera)
    {
        levelRoot = root;
        camera = worldCamera;
        if (camera == null || !camera.orthographic)
            return;

        initialCameraX = levelRoot.InverseTransformPoint(camera.transform.position).x;
        loopRoot = new GameObject("Level131BackgroundLoop").transform;
        loopRoot.SetParent(levelRoot, false);
        // 底色只补足视口；纸张属于世界环境，不能随相机锁在屏幕上。
        AddLayer("CommonBg/Bg", true);
        AddLayer("CommonBg/Mask (1)", false);
        AddLayer("GameBg (1)", false);
        AddLayer("Backscene", false);
        AddLayer("Midscene", false);
        // Frontscene 不属于本关循环范围，不恢复人工删除的前景。
        Tick();
    }

    private void AddLayer(string name, bool followCamera)
    {
        Transform source = levelRoot.Find(name);
        if (source == null || !source.gameObject.activeInHierarchy)
            return;

        Bounds bounds = default;
        bool hasBounds = false;
        foreach (SpriteRenderer renderer in source.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!renderer.enabled || renderer.sprite == null)
                continue;
            Bounds local = renderer.localBounds;
            Matrix4x4 matrix = levelRoot.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(local.center +
                        new Vector3(local.extents.x * x, local.extents.y * y, 0f));
                    if (!hasBounds)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                        bounds.Encapsulate(point);
                }
            }
        }
        if (!hasBounds || bounds.size.x < 0.01f)
            return;

        // 先保留源节点的父级变换，再转入关卡循环根；支持 CommonBg 的嵌套子层。
        SpriteRenderer sourceMask = name == "Backscene"
            ? source.Find("bjmengban")?.GetComponent<SpriteRenderer>()
            : null;
        bool hasMask = sourceMask != null && sourceMask.enabled &&
            sourceMask.gameObject.activeInHierarchy && sourceMask.sprite != null;
        // 节距仍取原整层包络，但蒙版不进入重复模板，避免留下隐藏的重复蒙版。
        Transform template = CopyVisualTree(source, source.parent,
            hasMask ? sourceMask.transform : null);
        template.SetParent(loopRoot, true);
        template.name = source.name + "_Template";
        template.gameObject.SetActive(false);
        if (hasMask)
        {
            // 单张蒙版和所有树木段共用原 Backscene 的排序组，保留蒙版 order=1
            // 覆盖树木 order=0 的关系，避免独立排序组按距离交换前后层。
            Transform group = CopyVisualTree(source, source.parent, copyChildren: false);
            group.name = "Backscene_ContinuousGroup";
            group.SetParent(loopRoot, true);
            template.SetParent(group, true);
            SortingGroup templateGroup = template.GetComponent<SortingGroup>();
            if (templateGroup != null)
                templateGroup.enabled = false;
            continuousMask = CopyVisualTree(sourceMask.transform, group).GetComponent<SpriteRenderer>();
            continuousMask.name = "bjmengban_Continuous";
            maskOrigin = continuousMask.transform.localPosition;
            maskScale = continuousMask.transform.localScale;
            maskBounds = GetLevelBounds(continuousMask);
            maskRight = maskBounds.max.x;
            maskBottom = maskBounds.min.y;
        }
        Layer layer = new()
        {
            Source = source,
            Template = template,
            Origin = levelRoot.InverseTransformPoint(template.position),
            Bounds = bounds,
            FollowCamera = followCamera,
        };
        layers.Add(layer);
        // 原始布局仍保存在本关实例中；由纯视觉副本接管，避免旧视差/扩图脚本争写位置。
        source.gameObject.SetActive(false);
    }

    public void Tick()
    {
        if (camera == null || loopRoot == null)
            return;

        float depth = Mathf.Abs(Vector3.Dot(levelRoot.position - camera.transform.position, camera.transform.forward));
        float left = levelRoot.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth))).x;
        float right = levelRoot.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth))).x;
        float minX = Mathf.Min(left, right);
        float width = Mathf.Abs(right - left);
        float centerX = (left + right) * 0.5f;
        float bottom = levelRoot.InverseTransformPoint(
            camera.ViewportToWorldPoint(new Vector3(0.5f, 0f, depth))).y - 1f;

        foreach (Layer layer in layers)
        {
            if (layer.FollowCamera)
            {
                if (layer.Tiles.Count == 0)
                {
                    CreateTile(layer);
                    layer.CoverRenderers = layer.Tiles[0].GetComponentsInChildren<SpriteRenderer>(true);
                }
                Vector3 coverPosition = layer.Origin;
                coverPosition.x += centerX - initialCameraX;
                layer.Tiles[0].position = levelRoot.TransformPoint(coverPosition);
                CoverViewport(layer.CoverRenderers, width, centerX);
                continue;
            }

            float period = layer.Bounds.size.x;
            int count = Mathf.CeilToInt(width / period) + 3;
            int first = Mathf.FloorToInt((minX - layer.Bounds.min.x) / period) - 1;
            // 纸张与装饰恢复原来的视口数量、节距索引及副本重排逻辑。
            while (layer.Tiles.Count < count)
                CreateTile(layer);
            for (int index = 0; index < layer.Tiles.Count; index++)
            {
                Transform tile = layer.Tiles[index];
                tile.gameObject.SetActive(index < count);
                if (index >= count)
                    continue;
                Vector3 position = layer.Origin;
                position.x += (first + index) * period;
                tile.position = levelRoot.TransformPoint(position);
            }
        }
        // 实际镜头右缘之外提前一整屏，始终修改同一张蒙版，不生成新蒙版。
        FitContinuousMask(Mathf.Max(left, right) + width, bottom);
    }

    private void CreateTile(Layer layer)
    {
        Transform tile = Object.Instantiate(layer.Template, layer.Template.parent, false);
        tile.name = layer.Source.name + "_Loop_" + layer.Tiles.Count;
        layer.Tiles.Add(tile);
        if (layer.FollowCamera)
            tile.gameObject.SetActive(true);
    }

    private void FitContinuousMask(float right, float bottom)
    {
        if (continuousMask == null || maskBounds.size.x < 0.01f || maskBounds.size.y < 0.01f)
            return;

        // 缓存最大覆盖：右边只增长，底部只向下延伸，折返和视口缩窄不收缩。
        // 原始左边界和顶部不动，始终从原尺寸重算，避免累积缩放误差。
        maskRight = Mathf.Max(maskRight, right);
        maskBottom = Mathf.Min(maskBottom, bottom);
        Transform target = continuousMask.transform;
        target.localPosition = maskOrigin;
        target.localScale = new Vector3(
            maskScale.x * (maskRight - maskBounds.min.x) / maskBounds.size.x,
            maskScale.y * (maskBounds.max.y - maskBottom) / maskBounds.size.y,
            maskScale.z);
        Bounds resized = GetLevelBounds(continuousMask);
        Vector3 correction = new(
            maskBounds.min.x - resized.min.x,
            maskBounds.max.y - resized.max.y,
            0f);
        target.position += levelRoot.TransformVector(correction);
    }

    private Bounds GetLevelBounds(SpriteRenderer renderer)
    {
        Bounds local = renderer.localBounds;
        Matrix4x4 matrix = levelRoot.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
        Bounds result = new(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
                result.Encapsulate(matrix.MultiplyPoint3x4(local.center +
                    new Vector3(local.extents.x * x, local.extents.y * y, 0f)));
        }
        return result;
    }

    private void CoverViewport(SpriteRenderer[] renderers, float width, float centerX)
    {
        // 只扩展跟随视口的纯底色，纸张保持模板尺寸与世界拼接基准。
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.drawMode != SpriteDrawMode.Tiled)
                continue;
            float relativeScaleX = Mathf.Abs(renderer.transform.lossyScale.x / levelRoot.lossyScale.x);
            if (relativeScaleX < 0.0001f)
                continue;
            float offset = Mathf.Abs(levelRoot.InverseTransformPoint(renderer.bounds.center).x - centerX);
            Vector2 size = renderer.size;
            size.x = Mathf.Max(size.x, (width + offset * 2f + 2f) / relativeScaleX);
            renderer.size = size;
        }
    }

    private static Transform CopyVisualTree(Transform source, Transform parent,
        Transform excluded = null, bool copyChildren = true)
    {
        GameObject copy = new(source.name);
        copy.layer = source.gameObject.layer;
        Transform target = copy.transform;
        target.SetParent(parent, false);
        target.localPosition = source.localPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;

        SortingGroup sourceGroup = source.GetComponent<SortingGroup>();
        if (sourceGroup != null)
        {
            SortingGroup group = copy.AddComponent<SortingGroup>();
            group.sortingLayerID = sourceGroup.sortingLayerID;
            group.sortingOrder = sourceGroup.sortingOrder;
            group.sortAtRoot = sourceGroup.sortAtRoot;
            group.enabled = sourceGroup.enabled;
        }

        SpriteRenderer sourceRenderer = source.GetComponent<SpriteRenderer>();
        if (sourceRenderer != null)
        {
            SpriteRenderer renderer = copy.AddComponent<SpriteRenderer>();
            renderer.sprite = sourceRenderer.sprite;
            renderer.sharedMaterials = sourceRenderer.sharedMaterials;
            renderer.color = sourceRenderer.color;
            renderer.flipX = sourceRenderer.flipX;
            renderer.flipY = sourceRenderer.flipY;
            renderer.drawMode = sourceRenderer.drawMode;
            renderer.size = sourceRenderer.size;
            renderer.tileMode = sourceRenderer.tileMode;
            renderer.adaptiveModeThreshold = sourceRenderer.adaptiveModeThreshold;
            renderer.maskInteraction = sourceRenderer.maskInteraction;
            renderer.spriteSortPoint = sourceRenderer.spriteSortPoint;
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder;
            renderer.enabled = sourceRenderer.enabled;
        }

        if (copyChildren)
        {
            for (int index = 0; index < source.childCount; index++)
            {
                Transform child = source.GetChild(index);
                if (child != excluded)
                    CopyVisualTree(child, target, excluded);
            }
        }
        copy.SetActive(source.gameObject.activeSelf);
        return target;
    }

    public void Dispose()
    {
        foreach (Layer layer in layers)
        {
            if (layer.Source != null)
                layer.Source.gameObject.SetActive(true);
        }
        layers.Clear();
        if (loopRoot != null)
            Object.Destroy(loopRoot.gameObject);
    }
}

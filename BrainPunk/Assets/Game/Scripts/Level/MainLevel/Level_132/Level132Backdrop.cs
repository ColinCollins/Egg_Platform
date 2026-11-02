using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>参考 131 的纯视觉段复用与单张蒙版延展；不接管 Midscene / Frontscene。</summary>
internal sealed class Level132Backdrop : IDisposable
{
    private sealed class Layer
    {
        public Transform Source;
        public bool OriginalActive;
        public Transform Template;
        public Vector3 Origin;
        public Bounds Bounds;
        public bool FollowCamera;
        public SpriteRenderer[] CoverRenderers;
        public readonly List<Transform> Tiles = new List<Transform>();
    }

    private readonly Transform _levelRoot;
    private readonly Camera _camera;
    private readonly Transform _loopRoot;
    private readonly Vector3 _cameraStart;
    private readonly List<Layer> _layers = new List<Layer>();
    private SpriteRenderer _continuousMask;
    private Vector3 _maskOrigin;
    private Vector3 _maskScale;
    private Bounds _maskBounds;
    private float _maskRight;
    private float _maskBottom;
    private bool _disposed;

    public Level132Backdrop(Transform level, Camera camera)
    {
        if (level == null || camera == null || !camera.orthographic)
            throw new ArgumentException("[Level132] Background coverage requires a level root and an orthographic camera.");
        _levelRoot = level;
        _camera = camera;
        _cameraStart = level.InverseTransformPoint(camera.transform.position);
        _loopRoot = new GameObject("Level132BackgroundLoop").transform;
        _loopRoot.SetParent(level, false);
        try
        {
            AddLayer("CommonBg/Bg", true);
            AddLayer("CommonBg/Mask (1)", false); // 与 131 一致：有纸张子层时按世界节距复用，不拉伸纹理。
            AddLayer("GameBg (1)", false); // 当前 Prefab 中的真实名字，不再查找不存在的 FarBg。
            AddLayer("Backscene", false); // 与 131 一致：树林按世界节距循环，不再叠加视差。
            // Midscene / Frontscene 保留人工首摆，不复制、不移动，也不关闭源对象。
            Step();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void AddLayer(string path, bool followCamera)
    {
        Transform source = _levelRoot.Find(path);
        if (source == null || !source.gameObject.activeInHierarchy) return;
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (var renderer in source.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!renderer.enabled || renderer.sprite == null) continue;
            Bounds local = GetLevelBounds(renderer);
            if (!hasBounds)
            {
                bounds = local;
                hasBounds = true;
            }
            else bounds.Encapsulate(local);
        }
        if (!hasBounds || bounds.size.x < 0.01f) return;

        SpriteRenderer sourceMask = path == "Backscene"
            ? source.Find("bjmengban")?.GetComponent<SpriteRenderer>() : null;
        bool hasMask = sourceMask != null && sourceMask.enabled &&
            sourceMask.gameObject.activeInHierarchy && sourceMask.sprite != null;
        Transform template = CopyVisualTree(source, source.parent, hasMask ? sourceMask.transform : null);
        template.SetParent(_loopRoot, true);
        template.name = source.name + "_Template";
        template.gameObject.SetActive(false);
        if (hasMask)
        {
            // 原整层包络仍决定节距；模板不含蒙版。树木和唯一蒙版共用原排序组。
            Transform group = CopyVisualTree(source, source.parent, copyChildren: false);
            group.name = "Backscene_ContinuousGroup";
            group.SetParent(_loopRoot, true);
            template.SetParent(group, true);
            SortingGroup templateGroup = template.GetComponent<SortingGroup>();
            if (templateGroup != null) templateGroup.enabled = false;
            _continuousMask = CopyVisualTree(sourceMask.transform, group).GetComponent<SpriteRenderer>();
            _continuousMask.name = "bjmengban_Continuous";
            _maskOrigin = _continuousMask.transform.localPosition;
            _maskScale = _continuousMask.transform.localScale;
            _maskBounds = GetLevelBounds(_continuousMask);
            _maskRight = _maskBounds.max.x;
            _maskBottom = _maskBounds.min.y;
        }
        _layers.Add(new Layer
        {
            Source = source,
            OriginalActive = source.gameObject.activeSelf,
            Template = template,
            Origin = _levelRoot.InverseTransformPoint(template.position),
            Bounds = bounds,
            FollowCamera = followCamera
        });
        // 只让纯视觉副本接管，源节点及其原扩图/视差脚本留在实例中供清理还原。
        source.gameObject.SetActive(false);
    }

    public void Step()
    {
        if (_disposed || _camera == null || _loopRoot == null) return;
        float depth = Mathf.Abs(Vector3.Dot(_levelRoot.position - _camera.transform.position, _camera.transform.forward));
        float left = _levelRoot.InverseTransformPoint(_camera.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth))).x;
        float right = _levelRoot.InverseTransformPoint(_camera.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth))).x;
        Vector3 bottomWorld = _camera.ViewportToWorldPoint(new Vector3(0.5f, 0f, depth));
        Vector3 topWorld = _camera.ViewportToWorldPoint(new Vector3(0.5f, 1f, depth));
        float bottom = _levelRoot.InverseTransformPoint(bottomWorld).y;
        float top = _levelRoot.InverseTransformPoint(topWorld).y;
        float minX = Mathf.Min(left, right);
        float width = Mathf.Abs(right - left);
        float height = Mathf.Abs(top - bottom);
        float centerX = (left + right) * 0.5f;
        float centerY = (bottom + top) * 0.5f;

        foreach (Layer layer in _layers)
        {
            if (layer.FollowCamera)
            {
                if (layer.Tiles.Count == 0)
                {
                    CreateTile(layer);
                    layer.CoverRenderers = layer.Tiles[0].GetComponentsInChildren<SpriteRenderer>(true);
                }
                // 131 只横向走；132 还会高空弹射，因此纯底色保留原有的纵向相机跟随。
                Vector3 coverPosition = layer.Origin + new Vector3(centerX - _cameraStart.x, centerY - _cameraStart.y, 0f);
                layer.Tiles[0].position = _levelRoot.TransformPoint(coverPosition);
                CoverViewport(layer.CoverRenderers, width, height, centerX, centerY);
                continue;
            }

            float period = layer.Bounds.size.x;
            int count = Mathf.CeilToInt(width / period) + 3;
            int first = Mathf.FloorToInt((minX - layer.Bounds.min.x) / period) - 1;
            while (layer.Tiles.Count < count) CreateTile(layer);
            for (int index = 0; index < layer.Tiles.Count; index++)
            {
                Transform tile = layer.Tiles[index];
                tile.gameObject.SetActive(index < count);
                if (index >= count) continue;
                Vector3 position = layer.Origin;
                position.x += (first + index) * period;
                tile.position = _levelRoot.TransformPoint(position);
            }
        }
        // 与 131 相同：右侧预留一整屏、下沿超出实际相机底部一单位；不整体下移蒙版。
        FitContinuousMask(Mathf.Max(left, right) + width, Mathf.Min(bottom, top) - 1f);
    }

    private void CreateTile(Layer layer)
    {
        Transform tile = UnityEngine.Object.Instantiate(layer.Template, layer.Template.parent, false);
        tile.name = layer.Source.name + "_Loop_" + layer.Tiles.Count;
        layer.Tiles.Add(tile);
        if (layer.FollowCamera) tile.gameObject.SetActive(true);
    }

    private void FitContinuousMask(float right, float bottom)
    {
        if (_continuousMask == null || _maskBounds.size.x < 0.01f || _maskBounds.size.y < 0.01f) return;
        _maskRight = Mathf.Max(_maskRight, right);
        _maskBottom = Mathf.Min(_maskBottom, bottom);
        Transform target = _continuousMask.transform;
        target.localPosition = _maskOrigin;
        target.localScale = new Vector3(
            _maskScale.x * (_maskRight - _maskBounds.min.x) / _maskBounds.size.x,
            _maskScale.y * (_maskBounds.max.y - _maskBottom) / _maskBounds.size.y,
            _maskScale.z);
        Bounds resized = GetLevelBounds(_continuousMask);
        Vector3 correction = new Vector3(_maskBounds.min.x - resized.min.x,
            _maskBounds.max.y - resized.max.y, 0f);
        target.position += _levelRoot.TransformVector(correction);
    }

    private Bounds GetLevelBounds(SpriteRenderer renderer)
    {
        Bounds local = renderer.localBounds;
        Matrix4x4 matrix = _levelRoot.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
        Bounds result = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                result.Encapsulate(matrix.MultiplyPoint3x4(local.center +
                    new Vector3(local.extents.x * x, local.extents.y * y, 0f)));
        return result;
    }

    private void CoverViewport(SpriteRenderer[] renderers, float width, float height, float centerX, float centerY)
    {
        foreach (var renderer in renderers)
        {
            if (renderer.drawMode != SpriteDrawMode.Tiled) continue;
            float scaleX = Mathf.Abs(renderer.transform.lossyScale.x / _levelRoot.lossyScale.x);
            float scaleY = Mathf.Abs(renderer.transform.lossyScale.y / _levelRoot.lossyScale.y);
            if (scaleX < 0.0001f || scaleY < 0.0001f) continue;
            Vector3 center = _levelRoot.InverseTransformPoint(renderer.bounds.center);
            Vector2 size = renderer.size;
            size.x = Mathf.Max(size.x, (width + Mathf.Abs(center.x - centerX) * 2f + 2f) / scaleX);
            size.y = Mathf.Max(size.y, (height + Mathf.Abs(center.y - centerY) * 2f + 2f) / scaleY);
            renderer.size = size;
        }
    }

    private static Transform CopyVisualTree(Transform source, Transform parent,
        Transform excluded = null, bool copyChildren = true)
    {
        GameObject copy = new GameObject(source.name);
        try
        {
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
                for (int index = 0; index < source.childCount; index++)
                {
                    Transform child = source.GetChild(index);
                    if (child != excluded) CopyVisualTree(child, target, excluded);
                }
            copy.SetActive(source.gameObject.activeSelf);
            return target;
        }
        catch
        {
            copy.SetActive(false);
            UnityEngine.Object.Destroy(copy);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_loopRoot != null) _loopRoot.gameObject.SetActive(false);
        foreach (Layer layer in _layers)
            if (layer.Source != null) layer.Source.gameObject.SetActive(layer.OriginalActive);
        _layers.Clear();
        if (_loopRoot != null) UnityEngine.Object.Destroy(_loopRoot.gameObject);
    }
}

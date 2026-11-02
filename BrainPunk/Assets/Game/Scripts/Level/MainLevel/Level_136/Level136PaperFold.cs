using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>仅供 Level136 使用的连续折角输入；不截图、不使用全屏卷页材质。</summary>
[DisallowMultipleComponent]
public sealed class Level136PaperFold : MonoBehaviour
{
    #region 私有状态
    private RectTransform _rect;
    private Func<bool> _canDrag;
    private Level136FoldGraphic[] _layers;
    private Vector2 _corner = new Vector2(1f, 0f);
    private SpriteRenderer _front29;
    private Camera _sceneCamera;
    private Level136Ctrl _settings;
    private Vector2 _inkOrigin, _inkX, _inkY, _referenceCorner;
    private Color _inkColor;
    private Vector2 _layoutSize;
    private bool _hasInkSample;
    private bool _hasInkLayout;
    private bool _warnedLayout;
    private Vector2 _grabOffset;
    private int? _pointer;
    #endregion

    #region 构建与刷新
    /// <summary>仅创建三层折页视觉和独立命中层；正面 29 始终由场景 SpriteRenderer 绘制。</summary>
    public void Initialize(SpriteRenderer front29, Sprite back986, Level136Ctrl settings, Func<bool> canDrag)
    {
        if (!Application.isPlaying || _layers != null)
            return;
        _rect = (RectTransform)transform;
        _canDrag = canDrag;
        _front29 = front29;
        _settings = settings;
        _layers = new Level136FoldGraphic[4];
        for (int i = 0; i < _layers.Length; i++)
        {
            GameObject layer = new GameObject(new[] { "ExposedPaper", "FoldedBack", "BackInk_986", "PaperInput" }[i],
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Level136FoldGraphic));
            layer.layer = gameObject.layer;
            layer.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)layer.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _layers[i] = layer.GetComponent<Level136FoldGraphic>();
            _layers[i].Initialize(this, i, i == 2 ? back986 : null);
        }
        Refresh();
    }

    private void Update()
    {
        if (_pointer.HasValue && !CanInteract())
            _pointer = null;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || _front29 == null || _front29.sprite == null || _rect == null)
            return;
        if (_sceneCamera == null)
            _sceneCamera = Camera.main;
        if (_sceneCamera == null || _rect.rect.width <= 0f || _rect.rect.height <= 0f)
            return;
        Vector2 origin = SceneInkPoint(Vector2.zero);
        Vector2 x = (SceneInkPoint(new Vector2(100f, 0f)) - origin) / 100f;
        Vector2 y = (SceneInkPoint(new Vector2(0f, 100f)) - origin) / 100f;
        Color tint = _front29.color;
        if (_hasInkSample && origin == _inkOrigin && x == _inkX && y == _inkY && tint == _inkColor
            && _layoutSize == _rect.rect.size)
            return;
        _hasInkSample = true;
        _layoutSize = _rect.rect.size;
        _inkOrigin = origin;
        _inkX = x;
        _inkY = y;
        _inkColor = tint;
        _hasInkLayout = Level136FoldGeometry.TryReferenceCorner(_rect.rect.size,
            PrintedPoint(Level136FoldGeometry.InkSeamStart), PrintedPoint(Level136FoldGeometry.InkSeamEnd),
            out _referenceCorner);
        if (!_hasInkLayout && !_warnedLayout)
        {
            Debug.LogWarning("[Level136] 当前纸背无法覆盖 FrontDigits_29 的完整接缝；请校准本关折页侧参数，保留场景数字的摆放。", _front29);
            _warnedLayout = true;
        }
        if (_hasInkLayout)
            _warnedLayout = false;
        Refresh();
    }

    private Vector2 SceneInkPoint(Vector2 sourcePixel)
    {
        Sprite sprite = _front29.sprite;
        Vector2 local = new Vector2(sourcePixel.x - sprite.pivot.x,
            sprite.rect.height - sourcePixel.y - sprite.pivot.y) / sprite.pixelsPerUnit;
        if (_front29.flipX) local.x = -local.x;
        if (_front29.flipY) local.y = -local.y;
        Vector3 world = _front29.transform.TransformPoint(local);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(_sceneCamera, world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screen, null, out Vector2 point);
        return new Vector2(point.x - _rect.rect.xMin, _rect.rect.yMax - point.y);
    }

    private Vector2 PrintedPoint(Vector2 source) { return _inkOrigin + _inkX * source.x + _inkY * source.y; }

    /// <summary>当前 Inspector 纸张外观，仅供本关网格使用。</summary>
    public Level136PaperAppearance Appearance => _settings.PaperAppearance;
    /// <summary>背面墨迹继承场景 29 的颜色，不改 SpriteRenderer。</summary>
    public Color InkColor => _inkColor;
    /// <summary>场景 29 能否在页面内形成有效拼接姿态。</summary>
    public bool HasInkLayout => _hasInkLayout;
    /// <summary>把原始素材像素映射到折回纸背，参考姿态随场景 29 的摆放计算。</summary>
    public Vector2 BackInkPoint(Level136FoldGeometry shape, Vector2 source)
    {
        return shape.MoveBackInk(PrintedPoint(source), _referenceCorner);
    }
    /// <summary>Inspector 外观更新后请求重绘，不移动场景数字或创建额外资源。</summary>
    public void RefreshAppearance() { if (_layers != null) Refresh(); }
    private void OnDisable() { _pointer = null; }
    private void OnApplicationFocus(bool focused) { if (!focused) _pointer = null; }
    private void OnApplicationPause(bool paused) { if (paused) _pointer = null; }
    private bool CanInteract() { return isActiveAndEnabled && _canDrag != null && _canDrag(); }
    private void Refresh()
    {
        foreach (Level136FoldGraphic layer in _layers)
            layer.SetCorner(_corner);
    }
    #endregion

    #region 单指拖动
    /// <summary>只允许右上角或已折回的纸片接收输入，HUD 的更高排序保持优先。</summary>
    public bool HitTest(Vector2 screenPoint, Camera eventCamera)
    {
        if (!CanInteract() || _rect == null)
            return false;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenPoint, eventCamera, out Vector2 local);
        Rect rect = _rect.rect;
        Vector2 point = new Vector2(local.x - rect.xMin, rect.yMax - local.y);
        Level136FoldGeometry shape = new Level136FoldGeometry(rect.size, _corner);
        bool corner = point.x >= rect.width * 0.84f && point.y <= rect.height * 0.2f;
        bool nearTip = !shape.IsClosed && Vector2.Distance(point, shape.Tip) <= rect.height * 0.05f;
        return rect.Contains(local) && (corner || nearTip || shape.Contains(point));
    }

    /// <summary>记录按下位置与指针所有权，拒绝第二根手指接管正在调整的纸角。</summary>
    public void BeginPointer(PointerEventData data)
    {
        if (_pointer.HasValue || data.button != PointerEventData.InputButton.Left || !CanInteract())
            return;
        _pointer = data.pointerId;
        _grabOffset = _corner - PointerPosition(data);
    }

    /// <summary>二维拖动直接移动自由纸角；保留抓取偏移，松手保持，不走预设路径或吸附答案。</summary>
    public void DragPointer(PointerEventData data)
    {
        if (_pointer != data.pointerId || !CanInteract())
            return;
        _corner = Level136FoldGeometry.ClampCorner(PointerPosition(data) + _grabOffset);
        Refresh();
    }

    private Vector2 PointerPosition(PointerEventData data)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, data.position,
            data.pressEventCamera, out Vector2 local);
        Rect rect = _rect.rect;
        return new Vector2((local.x - rect.xMin) / Mathf.Max(1f, rect.width),
            (rect.yMax - local.y) / Mathf.Max(1f, rect.height));
    }

    /// <summary>只释放发起本次拖动的指针；暂停、失焦、退出也会清理所有权。</summary>
    public void EndPointer(PointerEventData data)
    {
        if (_pointer == data.pointerId)
            _pointer = null;
    }
    #endregion
}

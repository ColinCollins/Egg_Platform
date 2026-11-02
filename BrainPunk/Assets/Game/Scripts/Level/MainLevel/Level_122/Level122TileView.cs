using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>牌堆中单个元素的点击转发器；只要当前画面能点到，就可以直接选择。</summary>
[DisallowMultipleComponent]
public sealed class Level122TileView : MonoBehaviour, IPointerClickHandler, ICanvasRaycastFilter
{
    private Level122MatchPanel _owner;
    private int _tileId;
    private CanvasGroup _canvasGroup;
    private UnityEngine.UI.Image _image;
    private Vector2[] _spriteVertices;
    private ushort[] _spriteTriangles;

    public RectTransform RectTransform { get; private set; }

    public void Configure(Level122MatchPanel owner, int tileId)
    {
        _owner = owner;
        _tileId = tileId;
        RectTransform = transform as RectTransform;
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _image = GetComponent<UnityEngine.UI.Image>();
        if (_image != null && _image.sprite != null)
        {
            _spriteVertices = _image.sprite.vertices;
            _spriteTriangles = _image.sprite.triangles;
        }
    }

    public void SetInteractive(bool interactive)
    {
        if (_canvasGroup == null)
            return;

        _canvasGroup.blocksRaycasts = interactive;
        _canvasGroup.interactable = interactive;
        _canvasGroup.alpha = 1f;
    }

    /// <summary>牌进入暂存区后保持完整亮度，但不再接受牌堆点击。</summary>
    public void SetTrayVisual()
    {
        if (_canvasGroup == null)
            return;

        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        _canvasGroup.alpha = 1f;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
            _owner?.HandleTileClicked(_tileId);
    }

    /// <summary>
    /// 使用 Sprite 的 Tight Mesh 做射线轮廓，透明矩形区域不再抢走后方可见道具的点击。
    /// 不依赖 Texture Read/Write，因此不会修改共享 Level033 素材的导入设置。
    /// </summary>
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (_image == null || _image.sprite == null || RectTransform == null ||
            _spriteVertices == null || _spriteVertices.Length < 3 ||
            _spriteTriangles == null || _spriteTriangles.Length < 3)
        {
            return true;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                RectTransform,
                screenPoint,
                eventCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = RectTransform.rect;
        Sprite sprite = _image.sprite;
        float spriteWidth = Mathf.Max(0.0001f, sprite.rect.width);
        float spriteHeight = Mathf.Max(0.0001f, sprite.rect.height);
        float scale = Mathf.Min(rect.width / spriteWidth, rect.height / spriteHeight);
        Vector2 drawnSize = new Vector2(spriteWidth * scale, spriteHeight * scale);
        Rect drawnRect = new Rect(
            rect.center.x - drawnSize.x * 0.5f,
            rect.center.y - drawnSize.y * 0.5f,
            drawnSize.x,
            drawnSize.y);
        if (!drawnRect.Contains(localPoint))
            return false;

        float normalizedX = Mathf.InverseLerp(drawnRect.xMin, drawnRect.xMax, localPoint.x);
        float normalizedY = Mathf.InverseLerp(drawnRect.yMin, drawnRect.yMax, localPoint.y);
        Vector2 spritePoint = new Vector2(
            (normalizedX * spriteWidth - sprite.pivot.x) / sprite.pixelsPerUnit,
            (normalizedY * spriteHeight - sprite.pivot.y) / sprite.pixelsPerUnit);

        for (int i = 0; i <= _spriteTriangles.Length - 3; i += 3)
        {
            Vector2 a = _spriteVertices[_spriteTriangles[i]];
            Vector2 b = _spriteVertices[_spriteTriangles[i + 1]];
            Vector2 c = _spriteVertices[_spriteTriangles[i + 2]];
            if (IsPointInTriangle(spritePoint, a, b, c))
                return true;
        }

        return false;
    }

    private static bool IsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float sign1 = Cross(point - b, a - b);
        float sign2 = Cross(point - c, b - c);
        float sign3 = Cross(point - a, c - a);
        bool hasNegative = sign1 < 0f || sign2 < 0f || sign3 < 0f;
        bool hasPositive = sign1 > 0f || sign2 > 0f || sign3 > 0f;
        return !(hasNegative && hasPositive);
    }

    private static float Cross(Vector2 left, Vector2 right)
    {
        return left.x * right.y - left.y * right.x;
    }

    private void OnDestroy()
    {
        _owner = null;
        _image = null;
        _spriteVertices = null;
        _spriteTriangles = null;
    }
}

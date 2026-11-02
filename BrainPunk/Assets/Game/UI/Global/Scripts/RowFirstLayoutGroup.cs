using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 按行填充的 UI 布局：先填满一行，再换下一行。不每帧刷新，需主动调用 RebuildLayout()。
/// 参考 GridLayoutGroup，仅支持“先横后竖”的排列方式。
/// </summary>
[ExecuteAlways]
public class RowFirstLayoutGroup : MonoBehaviour
{
    [Header("Cell & Spacing")]
    [SerializeField] private Vector2 _cellSize = new Vector2(100f, 100f);
    [SerializeField] private Vector2 _spacing = new Vector2(10f, 10f);

    [Header("Padding (L R T B)")]
    [SerializeField] private float _paddingLeft;
    [SerializeField] private float _paddingRight;
    [SerializeField] private float _paddingTop;
    [SerializeField] private float _paddingBottom;

    [Header("Options")]
    [Tooltip("为 true 时强制子物体大小为 cellSize；否则只改位置不改大小")]
    [SerializeField] private bool _fitCellSize = true;
    [Tooltip("为 true 时仅排列激活的子物体；为 false 时 inactive 也参与排列")]
    [SerializeField] private bool _ignoreInactive = false;

    private RectTransform _rect;
    private DrivenRectTransformTracker _tracker;

    private RectTransform Rect
    {
        get
        {
            if (_rect == null) _rect = transform as RectTransform;
            return _rect;
        }
    }

    /// <summary>
    /// 重新计算并应用子物体布局（按行填充：先填满一行再下一行）。不每帧调用。
    /// </summary>
    [ContextMenu("Rebuild Layout")]
    public void RebuildLayout()
    {
        if (Rect == null) return;

        _tracker.Clear();
        float width = Rect.rect.width - _paddingLeft - _paddingRight;
        if (width <= 0f) width = Rect.rect.width;

        float cellW = _cellSize.x + _spacing.x;
        int columnCount = Mathf.Max(1, Mathf.FloorToInt((width + _spacing.x) / cellW));

        int index = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i) as RectTransform;
            if (child == null) continue;
            if (_ignoreInactive && !child.gameObject.activeSelf) continue;

            int row = index / columnCount;
            int col = index % columnCount;
            index++;

            float x = _paddingLeft + col * (_cellSize.x + _spacing.x) + _cellSize.x * 0.5f;
            float y = -_paddingTop - row * (_cellSize.y + _spacing.y) - _cellSize.y * 0.5f;

            child.pivot = new Vector2(0.5f, 0.5f);
            child.anchorMin = new Vector2(0f, 1f);
            child.anchorMax = new Vector2(0f, 1f);
            child.anchoredPosition = new Vector2(x, y);

            if (_fitCellSize)
            {
                child.sizeDelta = _cellSize;
                _tracker.Add(this, child, DrivenTransformProperties.Anchors | DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta);
            }
            else
            {
                _tracker.Add(this, child, DrivenTransformProperties.Anchors | DrivenTransformProperties.AnchoredPosition);
            }
        }
    }

    private void OnDisable()
    {
        _tracker.Clear();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _cellSize.x = Mathf.Max(1f, _cellSize.x);
        _cellSize.y = Mathf.Max(1f, _cellSize.y);
        _spacing.x = Mathf.Max(0f, _spacing.x);
        _spacing.y = Mathf.Max(0f, _spacing.y);
    }
#endif
}

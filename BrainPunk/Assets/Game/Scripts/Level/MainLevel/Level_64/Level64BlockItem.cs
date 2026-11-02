using Game.Common;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Level64 BlockItem，用于对象池回收与 Sprite 切换
/// </summary>
public class Level64BlockItem : MonoBehaviour, IRecycle
{
    private SpriteRenderer _sr;
    private SortingGroup _sortingGroup;
    private int _defaultSortingOrder;

    /// <summary>在排内的 X 索引</summary>
    public int GridX { get; set; }
    /// <summary>在排内的 Y 索引（行号）</summary>
    public int GridY { get; set; }
    /// <summary>前后排层索引（0=前排，1=后排）</summary>
    public int LayerIndex { get; set; }

    private void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr == null)
            _sr = gameObject.AddComponent<SpriteRenderer>();

        _sortingGroup = GetComponent<SortingGroup>();
        if (_sortingGroup == null)
            _sortingGroup = GetComponentInChildren<SortingGroup>();

        if (_sortingGroup != null)
            _defaultSortingOrder = _sortingGroup.sortingOrder;
    }

    public void SetSprite(Sprite sprite)
    {
        if (_sr != null)
            _sr.sprite = sprite;
    }

    public void OnSpawn()
    {
        gameObject.SetActive(true);
        RestoreSortingGroupOrder();
    }

    public void OnRecycle()
    {
        gameObject.SetActive(false);
        GridX = 0;
        GridY = 0;
        LayerIndex = 0;
        transform.SetParent(null);
        RestoreSortingGroupOrder();
    }

    public void OffsetSortingGroupOrder(int delta)
    {
        if (_sortingGroup == null)
            return;

        _sortingGroup.sortingOrder = _defaultSortingOrder + delta;
    }

    public void RestoreSortingGroupOrder()
    {
        if (_sortingGroup == null)
            return;

        _sortingGroup.sortingOrder = _defaultSortingOrder;
    }
}

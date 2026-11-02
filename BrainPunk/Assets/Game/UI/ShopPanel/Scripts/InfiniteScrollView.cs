using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum InfiniteScrollMode
{
    /// <summary>有限滚动，到第一项和最后一项时停止。</summary>
    Finite,

    /// <summary>循环滚动，仅在目录大于可见槽位容量时生效；小目录仍按有限列表显示。</summary>
    Cyclic
}

/// <summary>
/// Bounded-slot ScrollRect adapter for ShopPanel skin browsing.
/// </summary>
public class InfiniteScrollView : MonoBehaviour
{
    public delegate void BindSlotHandler(SkinItem itemView, int itemIndex);

    /// <summary>
    /// 绑定通用 MonoBehaviour 槽位；供不继承 SkinItem、但需要复用相同虚拟列表生命周期的 Item 使用。
    /// </summary>
    public delegate void BindComponentSlotHandler(MonoBehaviour itemView, int itemIndex);

    /// <summary>
    /// 清理通用 MonoBehaviour 槽位的旧数据；槽位换绑、隐藏或列表清空时调用。
    /// </summary>
    public delegate void ClearComponentSlotHandler(MonoBehaviour itemView);

    /// <summary>
    /// 根据数据索引选择槽位预制体；返回 null 时回退到默认 itemPrefab。
    /// </summary>
    public delegate SkinItem SelectPrefabHandler(int itemIndex);

    private sealed class Slot
    {
        public readonly List<MonoBehaviour> ItemViews = new();
        public readonly List<MonoBehaviour> Prefabs = new();
        public MonoBehaviour ItemView;
        public RectTransform RectTransform;
        public MonoBehaviour Prefab;
        public int BoundIndex = -1;

        public MonoBehaviour FindItem(MonoBehaviour prefab)
        {
            for (int i = 0; i < Prefabs.Count; i++)
            {
                if (ReferenceEquals(Prefabs[i], prefab))
                {
                    return ItemViews[i];
                }
            }

            return null;
        }

        public void AddItem(MonoBehaviour prefab, MonoBehaviour item)
        {
            Prefabs.Add(prefab);
            ItemViews.Add(item);
        }
    }

    [Header("引用")]
    [Tooltip("接收拖拽输入的 ScrollRect，同时持有 viewport/content。为空时会从子节点自动查找。")]
    [SerializeField] private ScrollRect scrollRect;

    [Tooltip("可见裁剪区域，用于计算需要多少个可复用槽位。默认使用 ScrollRect.viewport。")]
    [SerializeField] private RectTransform viewport;

    [Tooltip("承载可复用 SkinItem 槽位的 Content 节点。默认使用 ScrollRect.content。")]
    [SerializeField] private RectTransform content;

    [Tooltip("可复用槽位不足时实例化的 SkinItem 预制体。为空时可使用 Content 下已有 SkinItem 作为兜底。")]
    [SerializeField] private SkinItem itemPrefab;

    [Header("滚动行为")]
    [Tooltip("期望滚动模式。Cyclic 只有在目录数量大于可见槽位容量时才会循环。")]
    [SerializeField] private InfiniteScrollMode scrollMode = InfiniteScrollMode.Finite;

    [Tooltip("单个皮肤槽位的宽高。需要与预制体视觉尺寸一致，避免定位偏移。")]
    [SerializeField] private Vector2 itemSize = new(190f, 239f);

    [Tooltip("槽位之间的横向/纵向间距。纵向间距会参与滚动行高计算。")]
    [SerializeField] private Vector2 spacing = new(20f, 40f);

    [Tooltip("Content 内边距，决定第一个槽位距离内容边界的位置。")]
    [SerializeField] private RectOffset padding = new();

    [Tooltip("皮肤网格列数。1 表示单列竖向列表，2 及以上表示网格。")]
    [SerializeField] private int constraintCount = 2;

    [Tooltip("可见区域外额外保留的复用行数，用于降低快速滚动时的换绑露出风险。")]
    [SerializeField] private int bufferRows = 1;

    private readonly List<Slot> _slots = new();
    private MonoBehaviour _componentItemPrefab;
    private BindComponentSlotHandler _bindComponentSlot;
    private ClearComponentSlotHandler _clearComponentSlot;
    private SelectPrefabHandler _selectItemPrefab;
    private Func<int, MonoBehaviour> _selectComponentItemPrefab;
    private int _itemCount;
    private int _slotCapacity;
    private int _visibleSlotCapacity;
    private int _lastFirstIndex = -1;
    private bool _isListenerBound;
    private bool _isRefreshing;

    public int ItemCount => _itemCount;

    public InfiniteScrollMode Mode
    {
        get => scrollMode;
        set
        {
            scrollMode = value;
            RefreshSlots(true);
        }
    }

    public InfiniteScrollMode EffectiveMode =>
        scrollMode == InfiniteScrollMode.Cyclic && _itemCount > _visibleSlotCapacity
            ? InfiniteScrollMode.Cyclic
            : InfiniteScrollMode.Finite;

    private float RowHeight => Mathf.Max(1f, itemSize.y + spacing.y);
    private float ColumnWidth => Mathf.Max(1f, itemSize.x + spacing.x);

    private void Awake()
    {
        ResolveReferences();
        ApplyScrollRectSettings();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindScrollListener();
        RefreshSlots(false);
    }

    private void OnDisable()
    {
        UnbindScrollListener();
        if (scrollRect != null)
        {
            scrollRect.StopMovement();
        }
    }

    private void OnDestroy()
    {
        UnbindScrollListener();
    }

    private void OnValidate()
    {
        itemSize.x = Mathf.Max(1f, itemSize.x);
        itemSize.y = Mathf.Max(1f, itemSize.y);
        spacing.x = Mathf.Max(0f, spacing.x);
        spacing.y = Mathf.Max(0f, spacing.y);
        constraintCount = Mathf.Max(1, constraintCount);
        bufferRows = Mathf.Max(0, bufferRows);
    }

    public void SetItemPrefab(SkinItem prefab)
    {
        if (prefab != null)
        {
            itemPrefab = prefab;
            _componentItemPrefab = prefab;
            _clearComponentSlot = ClearSkinItem;
        }
    }

    /// <summary>
    /// 设置按数据索引选择槽位预制体的回调；用于 Restore 等特殊行，普通行可返回默认 itemPrefab。
    /// </summary>
    public void SetItemPrefabSelector(SelectPrefabHandler selector)
    {
        _selectItemPrefab = selector;
        _selectComponentItemPrefab = selector == null
            ? null
            : itemIndex => selector(itemIndex);
        InvalidateSlotBindings();
    }

    public void SetData(int itemCount, BindSlotHandler bindSlot, bool resetToBeginning = true)
    {
        ResolveReferences();
        _componentItemPrefab = itemPrefab;
        _clearComponentSlot = ClearSkinItem;
        _selectComponentItemPrefab = _selectItemPrefab == null
            ? null
            : itemIndex => _selectItemPrefab(itemIndex);
        _bindComponentSlot = bindSlot == null
            ? null
            : (itemView, itemIndex) => bindSlot(itemView as SkinItem, itemIndex);
        _itemCount = Mathf.Max(0, itemCount);
        RefreshSlots(resetToBeginning);
    }

    /// <summary>
    /// 设置非 SkinItem 槽位使用的预制体和清理回调。
    /// 调用边界：必须在 SetComponentData(...) 前由运行时 UI 控制器调用。
    /// 注意事项：clearSlot 只负责清空 Item 绑定状态，不应销毁槽位对象。
    /// </summary>
    public void SetComponentItemPrefab(
        MonoBehaviour prefab,
        ClearComponentSlotHandler clearSlot)
    {
        if (prefab == null)
        {
            return;
        }

        _componentItemPrefab = prefab;
        _clearComponentSlot = clearSlot;
        _selectItemPrefab = null;
        _selectComponentItemPrefab = null;
        InvalidateSlotBindings();
    }

    /// <summary>
    /// 设置非 SkinItem 列表的数据量和槽位绑定回调。
    /// 调用边界：先通过 SetComponentItemPrefab(...) 指定 Item 预制体后再调用。
    /// 注意事项：Item 会被虚拟列表复用，绑定回调必须完整覆盖旧显示状态。
    /// </summary>
    public void SetComponentData(
        int itemCount,
        BindComponentSlotHandler bindSlot,
        bool resetToBeginning = true)
    {
        _itemCount = Mathf.Max(0, itemCount);
        _bindComponentSlot = bindSlot;
        RefreshSlots(resetToBeginning);
    }

    public void Refresh(bool resetToBeginning = true)
    {
        RefreshSlots(resetToBeginning);
    }

    public void ResetToBeginning()
    {
        if (content != null)
        {
            content.anchoredPosition = Vector2.zero;
        }

        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            scrollRect.normalizedPosition = new Vector2(0f, 1f);
        }

        _lastFirstIndex = -1;
        RebindVisibleSlots();
    }

    /// <summary>
    /// 用途：把指定数据索引滚动到当前可见槽位，并返回该索引此刻绑定的 SkinItem。
    /// 调用边界：仅在 SetData(...) 已完成后由 ShopPanel 皮肤引导于 Unity 主线程调用。
    /// 注意事项：返回的槽位会被虚拟列表复用，调用方不得跨滚动或刷新长期缓存。
    /// </summary>
    internal bool TryFocusItem(int itemIndex, out SkinItem itemView)
    {
        itemView = null;
        if (itemIndex < 0 || itemIndex >= _itemCount)
            return false;

        ResolveReferences();
        ApplyScrollRectSettings();
        RecalculateCapacity();
        EnsureSlotCount();
        UpdateContentSize();
        if (content == null)
            return false;

        int targetRow = Mathf.FloorToInt(itemIndex / (float)Mathf.Max(1, constraintCount));
        float targetOffset = targetRow <= 0
            ? 0f
            : padding.top + targetRow * RowHeight;
        float viewportHeight = viewport != null ? viewport.rect.height : 0f;
        float maxOffset = Mathf.Max(0f, content.rect.height - viewportHeight);
        content.anchoredPosition = new Vector2(
            content.anchoredPosition.x,
            Mathf.Clamp(targetOffset, 0f, maxOffset));

        scrollRect?.StopMovement();
        _lastFirstIndex = -1;
        RebindVisibleSlots();
        return TryGetBoundItem(itemIndex, out itemView);
    }

    /// <summary>
    /// 用途：查询当前可复用槽位中是否存在指定数据索引的绑定项。
    /// 调用边界：仅供 ShopPanel 皮肤引导在目标定位后、消费点击前即时校验。
    /// 注意事项：本方法不会滚动或重绑列表，索引不在当前槽位范围时返回 false。
    /// </summary>
    internal bool TryGetBoundItem(int itemIndex, out SkinItem itemView)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            Slot slot = _slots[i];
            if (slot != null && slot.BoundIndex == itemIndex && slot.ItemView != null)
            {
                itemView = slot.ItemView as SkinItem;
                return itemView != null;
            }
        }

        itemView = null;
        return false;
    }

    private void RefreshSlots(bool resetToBeginning)
    {
        ResolveReferences();
        ApplyScrollRectSettings();
        RecalculateCapacity();
        EnsureSlotCount();
        UpdateContentSize();

        if (resetToBeginning)
        {
            InvalidateSlotBindings();
            ResetToBeginning();
        }
        else
        {
            InvalidateSlotBindings();
            _lastFirstIndex = -1;
            RebindVisibleSlots();
        }
    }

    private void BindScrollListener()
    {
        if (_isListenerBound || scrollRect == null)
        {
            return;
        }

        scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        _isListenerBound = true;
    }

    private void UnbindScrollListener()
    {
        if (!_isListenerBound || scrollRect == null)
        {
            _isListenerBound = false;
            return;
        }

        scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
        _isListenerBound = false;
    }

    private void OnScrollValueChanged(Vector2 _)
    {
        if (!_isRefreshing)
        {
            RebindVisibleSlots();
        }
    }

    private void RecalculateCapacity()
    {
        float viewportHeight = viewport != null ? viewport.rect.height : 0f;
        int visibleRows = Mathf.Max(1, Mathf.CeilToInt((viewportHeight - padding.top - padding.bottom) / RowHeight) + 1);

        _visibleSlotCapacity = Mathf.Max(1, visibleRows * constraintCount);
        _slotCapacity = Mathf.Max(_visibleSlotCapacity, (visibleRows + bufferRows) * constraintCount);
    }

    private void EnsureSlotCount()
    {
        int targetCount = Mathf.Min(_itemCount, Mathf.Max(1, _slotCapacity));

        while (_slots.Count < targetCount)
        {
            MonoBehaviour prefab = _componentItemPrefab;
            MonoBehaviour item = CreateSlotItem(prefab);
            if (item == null)
            {
                break;
            }

            var slot = new Slot
            {
                ItemView = item,
                RectTransform = item.transform as RectTransform,
                Prefab = prefab
            };
            slot.AddItem(prefab, item);
            _slots.Add(slot);
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            bool active = i < targetCount;
            if (_slots[i].ItemView != null && _slots[i].ItemView.gameObject.activeSelf != active)
            {
                _slots[i].ItemView.gameObject.SetActive(active);
            }

            if (!active)
            {
                _slots[i].BoundIndex = -1;
                ClearSlotItems(_slots[i]);
                SetSlotItemsActive(_slots[i], false);
            }
        }
    }

    private MonoBehaviour CreateSlotItem(MonoBehaviour prefab)
    {
        if (content == null)
        {
            return null;
        }

        MonoBehaviour item = null;
        prefab ??= _componentItemPrefab;
        if (prefab != null)
        {
            item = Instantiate(prefab, content);
        }

        if (item == null && _componentItemPrefab == null)
        {
            item = content.GetComponentInChildren<SkinItem>(true);
            if (item != null && item.transform.parent != content)
            {
                item = null;
            }
        }

        if (item == null)
        {
            return null;
        }

        ConfigureSlotRect(item.transform as RectTransform);

        item.gameObject.SetActive(true);
        return item;
    }

    private void UpdateContentSize()
    {
        if (content == null)
        {
            return;
        }

        int rows = _itemCount <= 0 ? 0 : Mathf.CeilToInt(_itemCount / (float)constraintCount);
        float width = padding.left + padding.right + constraintCount * itemSize.x + (constraintCount - 1) * spacing.x;
        float height = padding.top + padding.bottom + rows * itemSize.y + Mathf.Max(0, rows - 1) * spacing.y;

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
    }

    private void RebindVisibleSlots()
    {
        if (_slots.Count == 0)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            if (_itemCount <= 0)
            {
                ClearSlots();
                return;
            }

            int firstIndex = GetFirstVisibleIndex();
            if (firstIndex == _lastFirstIndex && AllActiveSlotsBound())
            {
                return;
            }

            _lastFirstIndex = firstIndex;

            for (int i = 0; i < _slots.Count; i++)
            {
                int visualIndex = firstIndex + i;
                int itemIndex = ResolveItemIndex(visualIndex);
                BindSlot(_slots[i], itemIndex, visualIndex);
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private int GetFirstVisibleIndex()
    {
        if (content == null)
        {
            return 0;
        }

        float rawOffset = content.anchoredPosition.y - padding.top;
        int firstRow = EffectiveMode == InfiniteScrollMode.Cyclic
            ? Mathf.FloorToInt(rawOffset / RowHeight)
            : Mathf.FloorToInt(Mathf.Max(0f, rawOffset) / RowHeight);
        int firstIndex = firstRow * constraintCount;

        if (EffectiveMode == InfiniteScrollMode.Cyclic)
        {
            return firstIndex;
        }

        int maxFirst = Mathf.Max(0, _itemCount - _slots.Count);
        return Mathf.Clamp(firstIndex, 0, maxFirst);
    }

    private int ResolveItemIndex(int rawIndex)
    {
        if (_itemCount <= 0)
        {
            return -1;
        }

        if (EffectiveMode == InfiniteScrollMode.Cyclic)
        {
            return WrapIndex(rawIndex);
        }

        return rawIndex >= 0 && rawIndex < _itemCount ? rawIndex : -1;
    }

    private int WrapIndex(int index)
    {
        if (_itemCount <= 0)
        {
            return -1;
        }

        int wrapped = index % _itemCount;
        return wrapped < 0 ? wrapped + _itemCount : wrapped;
    }

    private void BindSlot(Slot slot, int itemIndex, int visualIndex)
    {
        if (slot?.ItemView == null)
        {
            return;
        }

        bool hasItem = itemIndex >= 0 && itemIndex < _itemCount;
        if (!hasItem)
        {
            slot.BoundIndex = -1;
            ClearSlotItems(slot);
            SetSlotItemsActive(slot, false);
            return;
        }

        if (!EnsureSlotItem(slot, itemIndex))
        {
            slot.BoundIndex = -1;
            return;
        }

        if (slot.ItemView.gameObject.activeSelf != hasItem)
        {
            slot.ItemView.gameObject.SetActive(hasItem);
        }

        PositionSlot(slot.RectTransform, visualIndex);

        if (slot.BoundIndex == itemIndex)
        {
            return;
        }

        slot.BoundIndex = itemIndex;
        _bindComponentSlot?.Invoke(slot.ItemView, itemIndex);
    }

    private bool EnsureSlotItem(Slot slot, int itemIndex)
    {
        var prefab = ResolveItemPrefab(itemIndex);
        if (slot.ItemView != null && ReferenceEquals(slot.Prefab, prefab))
        {
            return true;
        }

        var item = slot.FindItem(prefab);
        if (item == null)
        {
            item = CreateSlotItem(prefab);
            if (item == null)
            {
                return false;
            }

            slot.AddItem(prefab, item);
        }

        if (slot.ItemView != null)
        {
            ClearItemView(slot.ItemView);
            slot.ItemView.gameObject.SetActive(false);
        }

        slot.ItemView = item;
        slot.RectTransform = item.transform as RectTransform;
        slot.Prefab = prefab;
        slot.BoundIndex = -1;
        ConfigureSlotRect(slot.RectTransform);
        return true;
    }

    private MonoBehaviour ResolveItemPrefab(int itemIndex)
    {
        MonoBehaviour prefab = _selectComponentItemPrefab?.Invoke(itemIndex);
        return prefab != null ? prefab : _componentItemPrefab;
    }

    private void ConfigureSlotRect(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = itemSize;
    }

    private void PositionSlot(RectTransform rect, int visualIndex)
    {
        if (rect == null)
        {
            return;
        }

        int row = Mathf.FloorToInt(visualIndex / (float)constraintCount);
        int column = visualIndex - row * constraintCount;
        rect.anchoredPosition = new Vector2(
            padding.left + column * ColumnWidth,
            -(padding.top + row * RowHeight));
    }

    private bool AllActiveSlotsBound()
    {
        foreach (var slot in _slots)
        {
            if (slot.ItemView != null && slot.ItemView.gameObject.activeSelf && slot.BoundIndex < 0)
            {
                return false;
            }
        }

        return true;
    }

    private void ClearSlots()
    {
        foreach (var slot in _slots)
        {
            slot.BoundIndex = -1;
            ClearSlotItems(slot);
            SetSlotItemsActive(slot, false);
        }
    }

    private void InvalidateSlotBindings()
    {
        foreach (var slot in _slots)
        {
            slot.BoundIndex = -1;
        }
    }

    private void ClearSlotItems(Slot slot)
    {
        if (slot == null)
        {
            return;
        }

        for (int i = 0; i < slot.ItemViews.Count; i++)
        {
            ClearItemView(slot.ItemViews[i]);
        }
    }

    private void ClearItemView(MonoBehaviour itemView)
    {
        if (itemView == null)
        {
            return;
        }

        if (itemView is SkinItem skinItem)
        {
            skinItem.Clear();
            return;
        }

        _clearComponentSlot?.Invoke(itemView);
    }

    private static void ClearSkinItem(MonoBehaviour itemView)
    {
        (itemView as SkinItem)?.Clear();
    }

    private static void SetSlotItemsActive(Slot slot, bool active)
    {
        if (slot == null)
        {
            return;
        }

        for (int i = 0; i < slot.ItemViews.Count; i++)
        {
            var item = slot.ItemViews[i];
            if (item != null && item.gameObject.activeSelf != active)
            {
                item.gameObject.SetActive(active);
            }
        }
    }

    private void ResolveReferences()
    {
        scrollRect ??= GetComponentInChildren<ScrollRect>(true);
        viewport ??= scrollRect != null ? scrollRect.viewport : null;
        content ??= scrollRect != null ? scrollRect.content : null;
        itemPrefab ??= content != null ? content.GetComponentInChildren<SkinItem>(true) : null;
        if (_componentItemPrefab == null && itemPrefab != null)
        {
            _componentItemPrefab = itemPrefab;
            _clearComponentSlot = ClearSkinItem;
        }
    }

    private void ApplyScrollRectSettings()
    {
        if (scrollRect == null)
        {
            return;
        }

        DisableContentLayoutComponents();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = EffectiveMode == InfiniteScrollMode.Cyclic
            ? ScrollRect.MovementType.Unrestricted
            : ScrollRect.MovementType.Clamped;
    }

    private void DisableContentLayoutComponents()
    {
        if (content == null)
        {
            return;
        }

        var gridLayout = content.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
        {
            gridLayout.enabled = false;
        }

        var contentSizeFitter = content.GetComponent<ContentSizeFitter>();
        if (contentSizeFitter != null)
        {
            contentSizeFitter.enabled = false;
        }
    }
}

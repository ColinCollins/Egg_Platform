using System.Collections.Generic;
using Bear.Logger;
using Game.Common;
using UnityEngine;

/// <summary>
/// 用于控制 block 堆的展示
/// 通过 pool 生成 blockItem, blockItem 在 sprites[] 中选择一个切换皮肤。
/// blockItem 生成数量通过 Size(Vector) 表现展示数量，预生产指定 size 数量的根据 x,y 按照 gridlayout 的形式进行放置。并进行持有。
/// 玩家通过 OutputTriggerEnter 获取一个上层 item，触发 InputTRiggerEnter 的时候，item 进入 inputBlocks 的cache中。
/// blockItem 通过 getInputAvaliablePosition 获取当前指定位置。
/// blockItem 的生成和放置位置根据 outputBlock 以及 inputBlock 的位置为中心计算获得。
/// 默认 outputCache 要生成前后两排，用于第一排用完后，生成新的一排。原先的第二排变为第一排。
/// </summary>
public class Level64BlockCtrl : MonoBehaviour, IDebuger
{
    private const int InputFrontLayer = 0;
    private const int InputBackLayer = 1;

    [Header("Block Containers")]
    [SerializeField] private Transform outputBlocks;
    [SerializeField] private Transform inputBlocks;

    [Header("Block Item")]
    [Tooltip("BlockItem 预制体，为空时自动生成带 SpriteRenderer 的默认对象")]
    [SerializeField] private GameObject blockItemPrefab;
    [Tooltip("砖块皮肤数组，生成时随机选取")]
    [SerializeField] private Sprite[] sprites;
    [Tooltip("每排网格尺寸 (x=列数, y=行数)")]
    [SerializeField] private Vector2Int size = new Vector2Int(3, 3);
    [Tooltip("格子间距 (x=横向, y=纵向)")]
    [SerializeField] private Vector2 blockSpacing = new Vector2(0.5f, 0.5f);

    [Header("Output Cache")]
    [Tooltip("Output 前后排数（默认2排，深度方向）。第一排用完后，第二排前移并生成新排。设为1则关闭循环补充。")]
    [SerializeField] private int outputCacheRows = 2;
    [Tooltip("前后排在 x 方向的层偏移")]
    [SerializeField] private float layerXOffset = 0.2f;
    [Tooltip("前后排间距（z 方向深度）")]
    [SerializeField] private float layerSpacing = 0.1f;

    [Header("Pool Settings")]
    [SerializeField] private int initialPoolSize = 10;
    [SerializeField] private int maxPoolSize = 50;

    private readonly List<Level64BlockItem> _outputBlockItems = new();
    private readonly List<Level64BlockItem> _inputBlockItems = new();
    private readonly HashSet<Level64BlockItem> _activeBlockItems = new();
    private bool _isPoolRegistered;
    private bool _isOver;
    private bool _isInitialized;

    private void OnDestroy()
    {
        ClearAllItems();
    }

    #region Pool

    public void ConfigurePool(int configuredInitialPoolSize, int configuredMaxPoolSize)
    {
        if (_isInitialized)
            return;

        initialPoolSize = Mathf.Max(0, configuredInitialPoolSize);
        maxPoolSize = configuredMaxPoolSize > 0 ? Mathf.Max(1, configuredMaxPoolSize) : 0;
    }

    public void Initialize()
    {
        if (_isInitialized)
            return;

        RegisterPool();
        GenerateOutputBlocks();
        _isInitialized = true;
    }

    private void RegisterPool()
    {
        if (ObjectPoolManager.Instance == null)
        {
            Debug.LogError("[Level64BlockCtrl] ObjectPoolManager is not initialized!");
            return;
        }

        if (ObjectPoolManager.Instance.IsPoolRegistered<Level64BlockItem>())
        {
            Debug.LogWarning(
                $"[Level64BlockCtrl] Detected stale Level64BlockItem pool before registration. " +
                $"AvailableCount={ObjectPoolManager.Instance.GetAvailableCount<Level64BlockItem>()}. " +
                $"Forcing pool rebuild.");
            ObjectPoolManager.Instance.UnregisterPool<Level64BlockItem>();
        }

        ObjectPoolManager.Instance.RegisterPool<Level64BlockItem>(() =>
        {
            GameObject go;
            if (blockItemPrefab != null)
            {
                go = Instantiate(blockItemPrefab);
            }
            else
            {
                go = new GameObject("BlockItem");
                go.AddComponent<SpriteRenderer>();
            }

            var item = go.GetComponent<Level64BlockItem>();
            if (item == null)
                item = go.AddComponent<Level64BlockItem>();

            return item;
        }, initialSize: initialPoolSize, maxSize: maxPoolSize);

        _isPoolRegistered = true;
    }

    private void UnregisterPool()
    {
        if (ObjectPoolManager.Instance != null && _isPoolRegistered)
        {
            ObjectPoolManager.Instance.UnregisterPool<Level64BlockItem>();
        }
        _isPoolRegistered = false;
    }

    #endregion

    #region Generate

    private void GenerateOutputBlocks()
    {
        if (outputBlocks == null)
            return;

        int remainingCapacity = GetRemainingOutputGenerationCapacity();
        if (remainingCapacity == 0)
            return;

        int layers = Mathf.Max(1, outputCacheRows);
        float startX = GetCenterStartX(size.x);
        float startY = GetCenterStartY(size.y);

        for (int layer = 0; layer < layers; layer++)
        {
            float layerOffsetX = GetOutputLocalX(layer);
            float z = GetOutputLocalZ(layer);
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    if (remainingCapacity == 0)
                        return;

                    var item = SpawnBlockItem(outputBlocks, startX + x * blockSpacing.x + layerOffsetX, startY + y * blockSpacing.y, z);
                    if (item != null)
                    {
                        item.GridX = x;
                        item.GridY = y;
                        item.LayerIndex = layer;
                        _outputBlockItems.Add(item);
                        remainingCapacity--;
                    }
                }
            }
        }
    }

    private void GenerateOutputLayer(int layer)
    {
        if (outputBlocks == null || _isOver)
            return;

        int remainingCapacity = GetRemainingOutputGenerationCapacity();
        if (remainingCapacity == 0)
            return;

        float startX = GetCenterStartX(size.x);
        float startY = GetCenterStartY(size.y);
        float layerOffsetX = GetOutputLocalX(layer);
        float z = GetOutputLocalZ(layer);

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (remainingCapacity == 0)
                    return;

                var item = SpawnBlockItem(outputBlocks, startX + x * blockSpacing.x + layerOffsetX, startY + y * blockSpacing.y, z);
                if (item != null)
                {
                    item.GridX = x;
                    item.GridY = y;
                    item.LayerIndex = layer;
                    _outputBlockItems.Add(item);
                    remainingCapacity--;
                }
            }
        }
    }

    private Level64BlockItem SpawnBlockItem(Transform parent, float localX, float localY, float localZ)
    {
        if (ObjectPoolManager.Instance == null)
            return null;

        var item = ObjectPoolManager.Instance.Get<Level64BlockItem>();
        if (item == null)
        {
            Debug.LogError(
                $"[Level64BlockCtrl] Failed to get Level64BlockItem from pool. " +
                $"ActiveCount={_activeBlockItems.Count}, " +
                $"OutputCount={GetOutputCacheCount()}, " +
                $"InputCount={_inputBlockItems.Count}, " +
                $"InitialPoolSize={initialPoolSize}, " +
                $"MaxPoolSize={maxPoolSize}");
            return null;
        }

        item.transform.SetParent(parent);
        item.transform.localPosition = new Vector3(localX, localY, localZ);
        item.transform.localRotation = Quaternion.identity;
        item.transform.localScale = Vector3.one;

        if (sprites != null && sprites.Length > 0)
        {
            Sprite sprite = sprites[Random.Range(0, sprites.Length)];
            item.SetSprite(sprite);
        }

        _activeBlockItems.Add(item);

        return item;
    }

    #endregion

    #region Public API

    /// <summary>
    /// 从 Output 堆中取出一个上层 blockItem（前排最上层）。
    /// 若前排排空且开启了多排缓存，则先前移补充再取。
    /// 返回取出的 blockItem，无可用时返回 null。由外部（Level64Ctrl）决定如何交给 player。
    /// </summary>
    public Level64BlockItem OutputTriggerEnter()
    {
        int frontLayer = 0;

        // 若前排排空，尝试将后排前移补充
        if (!HasItemsInLayer(frontLayer) && outputCacheRows > 1)
        {
            PromoteOutputLayers();
        }

        var item = GetTopItemFromLayer(frontLayer);
        if (item == null)
            return null;

        _outputBlockItems.Remove(item);
        return item;
    }

    /// <summary>
    /// 将一个 blockItem 放入 Input 堆。
    /// 由外部（Level64Ctrl）传入 player 持有的 blockItem。
    /// </summary>
    public void InputTriggerEnter(Level64BlockItem item)
    {
        if (item == null)
            return;

        _activeBlockItems.Add(item);
        CommitInputLayerCycleIfNeeded();
        GetNextInputPlacementPreview(out Vector3 localPosition, out int layer, out int gridX, out int gridY);
        item.transform.SetParent(inputBlocks);
        item.transform.localPosition = localPosition;
        item.transform.localRotation = Quaternion.identity;
        item.GridX = gridX;
        item.GridY = gridY;
        item.LayerIndex = layer;

        _inputBlockItems.Add(item);
    }

    /// <summary>
    /// 获取 inputBlocks 中下一个可用位置（按 grid 从最左下角开始，左到右、下到上排列）
    /// </summary>
    public Vector3 GetInputAvailablePosition()
    {
        GetNextInputPlacementPreview(out Vector3 localPosition, out _, out _, out _);

        if (inputBlocks == null)
            return localPosition;

        return inputBlocks.TransformPoint(localPosition);
    }

    public int GetOutputCacheCount()
    {
        int count = 0;
        foreach (var outputItem in _outputBlockItems)
        {
            if (outputItem != null)
                count++;
        }

        return count;
    }

    public int GetInitialOutputCapacity()
    {
        return Mathf.Max(1, outputCacheRows) * Mathf.Max(1, size.x) * Mathf.Max(1, size.y);
    }

    public int GetRecommendedPoolCapacity(int extraCapacity = 0)
    {
        int inputDisplayCapacity = GetInputLayerCapacity() * 2;
        return GetInitialOutputCapacity() + inputDisplayCapacity + Mathf.Max(0, extraCapacity);
    }

    public Transform GetOutputBlocksRoot()
    {
        return outputBlocks;
    }

    public Transform GetInputBlocksRoot()
    {
        return inputBlocks;
    }

    /// <summary>
    /// 回收指定的 blockItem 到对象池
    /// </summary>
    public void RecycleBlockItem(Level64BlockItem item)
    {
        if (item == null)
            return;

        _activeBlockItems.Remove(item);
        ObjectPoolManager.Instance?.Recycle(item);
    }

    public void SetOver(bool isOver)
    {
        _isOver = isOver;
    }

    #endregion

    #region Helpers

    private float GetCenterStartX(int columns)
    {
        return -(columns - 1) * blockSpacing.x * 0.5f;
    }

    private float GetCenterStartY(int rows)
    {
        return -(rows - 1) * blockSpacing.y * 0.5f;
    }

    private static float GetGridBottomLeftStartX(int columns, float spacingX)
    {
        return -(columns - 1) * spacingX * 0.5f;
    }

    private static float GetGridBottomLeftStartY(int rows, float spacingY)
    {
        return -(rows - 1) * spacingY * 0.5f;
    }

    private float GetInputLocalZ(int layer)
    {
        return layer == InputBackLayer ? layerSpacing * 0.5f : -layerSpacing * 0.5f;
    }

    private float GetInputLocalX(int layer)
    {
        return layer == InputBackLayer ? layerXOffset * 0.5f : -layerXOffset * 0.5f;
    }

    private float GetOutputLocalZ(int layer)
    {
        int layers = Mathf.Max(1, outputCacheRows);
        float totalDepth = (layers - 1) * layerSpacing;
        float startZ = -totalDepth * 0.5f;
        return startZ + layer * layerSpacing;
    }

    private float GetOutputLocalX(int layer)
    {
        int layers = Mathf.Max(1, outputCacheRows);
        float totalOffset = (layers - 1) * layerXOffset;
        float startX = -totalOffset * 0.5f;
        return startX + layer * layerXOffset;
    }

    private bool HasItemsInLayer(int layer)
    {
        foreach (var item in _outputBlockItems)
        {
            if (item == null)
                continue;
            if (item.LayerIndex == layer)
                return true;
        }
        return false;
    }

    private void PromoteOutputLayers()
    {
        // 从前往后逐层前移：layer+1 的砖块移动到 layer
        for (int layer = 0; layer < outputCacheRows - 1; layer++)
        {
            float targetX = GetOutputLocalX(layer);
            float targetZ = GetOutputLocalZ(layer);
            foreach (var item in _outputBlockItems)
            {
                if (item == null || item.LayerIndex != layer + 1)
                    continue;

                item.LayerIndex = layer;
                var pos = item.transform.localPosition;
                pos.x = GetCenterStartX(size.x) + item.GridX * blockSpacing.x + targetX;
                pos.z = targetZ;
                item.transform.localPosition = pos;
            }
        }

        // 生成新的最后层
        GenerateOutputLayer(outputCacheRows - 1);
    }

    private Level64BlockItem GetTopItemFromLayer(int layer)
    {
        Level64BlockItem target = null;
        float maxY = float.MinValue;
        float maxX = float.MinValue;

        foreach (var item in _outputBlockItems)
        {
            if (item == null || item.LayerIndex != layer)
                continue;

            Vector3 localPos = item.transform.localPosition;
            if (localPos.y > maxY || (Mathf.Approximately(localPos.y, maxY) && localPos.x > maxX))
            {
                maxY = localPos.y;
                maxX = localPos.x;
                target = item;
            }
        }

        return target;
    }

    private void GetNextInputPlacementPreview(out Vector3 localPosition, out int layer, out int gridX, out int gridY)
    {
        int columns = Mathf.Max(1, size.x);
        int rows = Mathf.Max(1, size.y);
        int layerCapacity = GetInputLayerCapacity();
        int backCount = GetInputLayerItemCount(InputBackLayer);
        int frontCount = GetInputLayerItemCount(InputFrontLayer);

        if (backCount >= layerCapacity && frontCount >= layerCapacity)
        {
            backCount = layerCapacity;
            frontCount = 0;
        }

        layer = backCount < layerCapacity ? InputBackLayer : InputFrontLayer;
        int countInLayer = layer == InputBackLayer ? backCount : frontCount;
        gridX = countInLayer % columns;
        gridY = countInLayer / columns;

        float startX = GetGridBottomLeftStartX(columns, blockSpacing.x);
        float startY = GetGridBottomLeftStartY(rows, blockSpacing.y);
        localPosition = new Vector3(
            startX + gridX * blockSpacing.x + GetInputLocalX(layer),
            startY + gridY * blockSpacing.y,
            GetInputLocalZ(layer));
    }

    private void CommitInputLayerCycleIfNeeded()
    {
        int layerCapacity = GetInputLayerCapacity();
        if (layerCapacity <= 0)
            return;

        if (GetInputLayerItemCount(InputBackLayer) < layerCapacity)
            return;

        if (GetInputLayerItemCount(InputFrontLayer) < layerCapacity)
            return;

        RecycleInputLayer(InputBackLayer);
        PromoteInputFrontLayerToBack();
    }

    private int GetInputLayerCapacity()
    {
        return Mathf.Max(1, size.x) * Mathf.Max(1, size.y);
    }

    private int GetInputLayerItemCount(int layer)
    {
        int count = 0;
        foreach (var inputItem in _inputBlockItems)
        {
            if (inputItem == null || inputItem.LayerIndex != layer)
                continue;

            count++;
        }

        return count;
    }

    private void RecycleInputLayer(int layer)
    {
        for (int i = _inputBlockItems.Count - 1; i >= 0; i--)
        {
            Level64BlockItem inputItem = _inputBlockItems[i];
            if (inputItem == null)
            {
                _inputBlockItems.RemoveAt(i);
                continue;
            }

            if (inputItem.LayerIndex != layer)
                continue;

            _activeBlockItems.Remove(inputItem);
            ObjectPoolManager.Instance?.Recycle(inputItem);
            _inputBlockItems.RemoveAt(i);
        }
    }

    private void PromoteInputFrontLayerToBack()
    {
        float targetX = GetInputLocalX(InputBackLayer);
        float targetZ = GetInputLocalZ(InputBackLayer);
        foreach (var inputItem in _inputBlockItems)
        {
            if (inputItem == null || inputItem.LayerIndex != InputFrontLayer)
                continue;

            inputItem.LayerIndex = InputBackLayer;
            Vector3 localPosition = inputItem.transform.localPosition;
            localPosition.x = GetGridBottomLeftStartX(Mathf.Max(1, size.x), blockSpacing.x) + inputItem.GridX * blockSpacing.x + targetX;
            localPosition.z = targetZ;
            inputItem.transform.localPosition = localPosition;
        }
    }

    private void ClearAllItems()
    {
        var activeItems = new List<Level64BlockItem>(_activeBlockItems);
        foreach (var item in activeItems)
        {
            if (item != null)
                RecycleBlockItem(item);
        }

        _activeBlockItems.Clear();
        _outputBlockItems.Clear();
        _inputBlockItems.Clear();

        UnregisterPool();
    }

    private int GetRemainingOutputGenerationCapacity()
    {
        if (maxPoolSize <= 0)
            return int.MaxValue;

        return Mathf.Max(0, maxPoolSize - GetOutputCacheCount());
    }

    #endregion
}

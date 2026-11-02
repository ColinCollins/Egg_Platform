using UnityEngine;

/// <summary>117 关太阳伞素材的本关误导：横向拖动并始终投影，遮门仅稍微褪红，不改变真实温度或角色受热。</summary>
public sealed class Level144UmbrellaDecoy : MonoBehaviour
{
    #region 显式引用与移动范围
    [Tooltip("本关太阳伞图像，复用 117 的原图，不附带旧关卡遮阳免疫脚本。")]
    [SerializeField] private SpriteRenderer canopy;
    [Tooltip("本关地面阴影，默认显示并始终跟随太阳伞横向移动。")]
    [SerializeField] private SpriteRenderer groundShadow;
    [Tooltip("本关出口区域，用于判断伞盖是否遮到门。")]
    [SerializeField] private Collider2D doorArea;
    [Tooltip("太阳伞中心允许的世界 X 范围，配合本关镜头拖动范围。")]
    [SerializeField] private Vector2 horizontalRange = new Vector2(-8f, 12f);
    [Tooltip("伞完全遮住门时稍微褪红的比例，仅影响门显示，不改变通关和角色受热。")]
    [SerializeField, Range(0f, 0.4f)] private float coolingStrength = 0.2f;
    #endregion

    #region 指针状态
    private bool initialized;
    private bool dragging;
    private float pointerOffsetX;
    private float canopyHeight;
    private float shadowGroundHeight;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        canopyHeight = canopy.transform.position.y;
        shadowGroundHeight = groundShadow.transform.position.y;
        initialized = true;
        RefreshShadow();
    }

    private void OnDisable()
    {
        dragging = false;
    }

    /// <summary>由关卡统一输入认领指针；调用前已处理 UI、风扇及可游玩状态。</summary>
    public bool TryBeginDrag(Vector2 worldPoint)
    {
        if (!Application.isPlaying || !initialized || !isActiveAndEnabled || dragging) return false;
        Bounds bounds = canopy.bounds;
        if (worldPoint.x < bounds.min.x || worldPoint.x > bounds.max.x || worldPoint.y < bounds.min.y || worldPoint.y > bounds.max.y) return false;
        pointerOffsetX = canopy.transform.position.x - worldPoint.x;
        dragging = true;
        return true;
    }

    /// <summary>只横向拖动伞图并刷新投影，忽略手指上下偏移，始终保留编辑时高度。</summary>
    public void MoveDrag(Vector2 worldPoint)
    {
        if (!Application.isPlaying || !initialized || !dragging) return;
        canopy.transform.position = new Vector3(Mathf.Clamp(worldPoint.x + pointerOffsetX, horizontalRange.x, horizontalRange.y),
            canopyHeight, canopy.transform.position.z);
        RefreshShadow();
    }

    /// <summary>松手/暂停/失焦时结束当前拖动，伞留在玩家放置的位置。</summary>
    public void EndDrag()
    {
        dragging = false;
    }
    #endregion

    #region 阴影与误导褪红
    /// <summary>按伞与门的横向重叠比例稍微褪红；不修改真实热度、角色受热或风扇进度。</summary>
    public float CoolingAmount
    {
        get
        {
            if (!Application.isPlaying || !initialized || !isActiveAndEnabled) return 0f;
            Bounds umbrella = canopy.bounds;
            Bounds door = doorArea.bounds;
            float overlap = Mathf.Min(umbrella.max.x, door.max.x) - Mathf.Max(umbrella.min.x, door.min.x);
            return coolingStrength * Mathf.Clamp01(overlap / Mathf.Max(0.01f, door.size.x));
        }
    }

    /// <summary>默认保留地面阴影，跟随伞的 X 位置，不随遮门程度改变阴影可见性。</summary>
    public void RefreshShadow()
    {
        if (!Application.isPlaying || !initialized || !isActiveAndEnabled) return;
        groundShadow.enabled = true;
        // 门触发盒低于实际地面，阴影沿编辑时摆好的地面高度移动。
        groundShadow.transform.position = new Vector3(canopy.bounds.center.x, shadowGroundHeight, groundShadow.transform.position.z);
    }
    #endregion
}

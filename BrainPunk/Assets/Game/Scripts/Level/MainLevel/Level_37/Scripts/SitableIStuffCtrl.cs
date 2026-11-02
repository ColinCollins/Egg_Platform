using DG.Tweening;
using UnityEngine;

/// <summary>
/// 可坐下机关在世界空间中的平移：入座时沿配置方向推一段、离座时从当前位置收回（与 Level37 椅子一致）。
/// 挂在椅子等物体上，或使用 <see cref="stuffRoot"/> 指定要动的 Transform。
/// </summary>
public class SitableIStuffCtrl : MonoBehaviour
{
    [SerializeField] private Transform stuffRoot;

    [Tooltip("入座时相对当前世界坐标的目标位移")]
    [SerializeField] private Vector3 worldShiftOnSit = new Vector3(0.5f, 0f, 0f);

    [SerializeField] private float shiftDuration = 0.5f;

    /// <summary> 椅子等需要盖在角色前的部位；入座时整体 <see cref="sitSortingOrderDelta"/> 叠加到 sortingOrder。 </summary>
    [SerializeField] private SpriteRenderer[] frontParts;

    [Tooltip("入座时相对缓存的原始 sortingOrder 的增量（通常为正直，把前挡片提到角色之上）")]
    [SerializeField] private int sitSortingOrderDelta = 50;

    [SerializeField] private bool ignoreTimeScale = true;

    private Transform _root;
    private bool _shiftApplied;
    private int[] _frontPartOriginalSortingOrders;

    private void Awake()
    {
        _root = stuffRoot != null ? stuffRoot : transform;
        CacheFrontPartSortingOrders();
    }

    /// <summary> 入座阶段：相对当前世界位置推入 <see cref="worldShiftOnSit"/>。重复调用无效。 </summary>
    public void SitIn()
    {
        EnsureRoot();
        if (_shiftApplied)
            return;

        _shiftApplied = true;
        ApplyFrontPartsSitSorting(true);
        var tween = _root.DOMove(_root.position + worldShiftOnSit, shiftDuration);
        if (ignoreTimeScale)
            tween.SetUpdate(true);
    }

    /// <summary> 离座阶段：相对当前世界位置反向平移（与入座同长度）。未入座过则无效。 </summary>
    public void SitOut()
    {
        EnsureRoot();
        if (!_shiftApplied)
            return;

        _shiftApplied = false;
        ApplyFrontPartsSitSorting(false);
        var tween = _root.DOMove(_root.position - worldShiftOnSit, shiftDuration);
        if (ignoreTimeScale)
            tween.SetUpdate(true);
    }

    private void CacheFrontPartSortingOrders()
    {
        if (frontParts == null || frontParts.Length == 0)
        {
            _frontPartOriginalSortingOrders = null;
            return;
        }

        _frontPartOriginalSortingOrders = new int[frontParts.Length];
        for (int i = 0; i < frontParts.Length; i++)
        {
            SpriteRenderer r = frontParts[i];
            _frontPartOriginalSortingOrders[i] = r != null ? r.sortingOrder : 0;
        }
    }

    /// <summary> 入座 true：原始 order + delta；离座 false：恢复 Awake 时缓存。 </summary>
    private void ApplyFrontPartsSitSorting(bool sitState)
    {
        if (frontParts == null || frontParts.Length == 0)
            return;

        if (_frontPartOriginalSortingOrders == null || _frontPartOriginalSortingOrders.Length != frontParts.Length)
            CacheFrontPartSortingOrders();

        for (int i = 0; i < frontParts.Length; i++)
        {
            SpriteRenderer r = frontParts[i];
            if (r == null)
                continue;

            int baseline = _frontPartOriginalSortingOrders[i];
            r.sortingOrder = sitState ? baseline + sitSortingOrderDelta : baseline;
        }
    }

    private void EnsureRoot()
    {
        if (_root == null)
            Awake();
    }

    private void OnDestroy()
    {
        if (_root != null)
            _root.DOKill();
    }
}

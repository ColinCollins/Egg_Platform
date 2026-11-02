using Bear.Logger;
using UnityEngine;

/// <summary>
/// IWanner 对象回收区域。
/// 指定 Layer 的 Collider2D 进入 Trigger 后，按配置销毁或隐藏其所在 GameObject。
/// </summary>
[DisallowMultipleComponent]
public sealed class IWannerGame_RecycleBackArea : MonoBehaviour, IDebuger
{
    #region Types

    private enum RecycleMode
    {
        Destroy,
        Hide
    }

    #endregion

    #region Inspector

    /// <summary>
    /// 用途：限制允许当前区域处理的 Collider2D Layer。
    /// 调用边界：只检查进入 Trigger 的 Collider 所在 GameObject，不向父级或 Rigidbody 根节点扩散。
    /// 注意事项：默认 Nothing，必须在 Inspector 明确选择目标 Layer 后才会生效。
    /// </summary>
    [SerializeField] private LayerMask targetLayers;

    /// <summary>
    /// 用途：选择命中对象的回收方式。
    /// 调用边界：Destroy 用于不再复用的实例；Hide 只执行 SetActive(false)，恢复由外部逻辑负责。
    /// </summary>
    [SerializeField] private RecycleMode recycleMode = RecycleMode.Hide;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Collider2D areaCollider = GetComponent<Collider2D>();
        if (areaCollider == null)
        {
            this.LogWarning(
                "[IWannerGame_RecycleBackArea] Collider2D is missing; recycle trigger cannot be entered.");
            return;
        }

        if (!areaCollider.isTrigger)
        {
            this.LogWarning(
                "[IWannerGame_RecycleBackArea] Collider2D is not marked as Trigger; OnTriggerEnter2D will not run as expected.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !ContainsLayer(other.gameObject.layer))
            return;

        GameObject target = other.gameObject;
        if (target == gameObject)
            return;

        if (recycleMode == RecycleMode.Destroy)
        {
            Destroy(target);
            return;
        }

        target.SetActive(false);
    }

    #endregion

    #region Internal

    private bool ContainsLayer(int layer)
    {
        return (targetLayers.value & (1 << layer)) != 0;
    }

    #endregion
}

using UnityEngine;

/// <summary>
/// Level 104 竖刺墙非致死触发器。碰到主角时把「碰撞当下 Actor 中心 X」传给 Ctrl；
/// Ctrl 延迟到 LateUpdate 才登记 Domain 反弹，以便同一物理帧的箭命中先结算 Failure。
/// </summary>
public sealed class Level104SpikeWallTrigger : MonoBehaviour
{
    /// <summary>本关玩法编排器；Prefab 显式绑定，缺失时从父层级解析。</summary>
    [SerializeField] private Level104Ctrl ctrl;

    private void Awake()
    {
        ctrl ??= GetComponentInParent<Level104Ctrl>();
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider == null)
            return;

        string layerName = LayerMask.LayerToName(collider.gameObject.layer);
        if (!layerName.Equals("Actor") || !collider.CompareTag("Player"))
            return;

        ActorCtrl targetActor = collider.GetComponent<ActorCtrl>();
        targetActor ??= collider.GetComponentInParent<ActorCtrl>();
        if (targetActor == null)
            return;

        ctrl ??= GetComponentInParent<Level104Ctrl>();
        if (ctrl != null)
            ctrl.NotifySpikeWallTouched(targetActor.transform.position.x);
    }
}

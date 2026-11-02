using UnityEngine;

/// <summary>
/// Level 125 吊环触碰转发器：挂在吊环碰撞体所在物体上，
/// 把 OnTriggerEnter2D 转发给 Level125GadgetCtrl，由其统一执行状态机判定。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class Level125RingContact : MonoBehaviour
{
    [SerializeField] private Level125GadgetCtrl gadget;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (gadget != null)
            gadget.HandleRingTouch(other);
    }
}

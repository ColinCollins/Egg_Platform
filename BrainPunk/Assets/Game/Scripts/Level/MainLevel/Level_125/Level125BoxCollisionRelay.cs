using UnityEngine;

/// <summary>
/// Level 125 箱子专属物理碰撞转发器。
/// 只挂在本关 Box 实例上，不修改共享 Box Prefab；命中石块由碰撞双方和顶面接触交给机关控制器判断。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class Level125BoxCollisionRelay : MonoBehaviour
{
    [SerializeField] private Level125GadgetCtrl gadget;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        gadget?.HandleRightPitBoxCollision(collision);
    }
}

using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// DrawGame 第 4 关建立的碰撞陷阱处理器，当前由 DLevel_004 与 DLevel_016 复用。
/// 按宿主绑定的 Actor 层级识别目标，命中特效使用本次 2D 物理碰撞的接触点。
/// </summary>
public sealed class DLevel4RockTrapHandle : MonoBehaviour, IEventSender
{
    #region Inspector References

    /// <summary>
    /// 当前宿主 DLevel 中被危险物击中的玩家 Actor；用于限定目标并复用既有死亡和击退流程。
    /// </summary>
    [SerializeField] private ActorCtrl actor;

    /// <summary>
    /// 当前宿主 DLevel 已有的命中特效实例；每次命中时移动到物理接触点后播放。
    /// </summary>
    [SerializeField] private ParticleSystem knock;

    /// <summary>
    /// 保持宿主 prefab 的序列化配置；为 true 时仅响应 Collision2D。
    /// </summary>
    [SerializeField] private bool isCollision = true;

    #endregion

    private bool isDead;

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (isCollision || !IsPlayerCollider(collider))
            return;

        PlayKnock(actor.transform.position);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isCollision)
            return;

        Collider2D collider = collision.collider;
        if (!IsPlayerCollider(collider))
            return;

        PlayKnock(GetContactPosition(collision, collider));
    }

    private bool IsPlayerCollider(Collider2D collider)
    {
        if (collider == null || actor == null)
            return false;

        // Actor may expose untagged child colliders (for example pushCollider).
        // Resolve the owning Actor instead of requiring every child to repeat the Player tag.
        ActorCtrl collidedActor = collider.GetComponentInParent<ActorCtrl>();
        return collidedActor == actor;
    }

    private Vector3 GetContactPosition(Collision2D collision, Collider2D playerCollider)
    {
        Vector2 contactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : playerCollider.ClosestPoint(transform.position);

        return new Vector3(contactPoint.x, contactPoint.y, knock.transform.position.z);
    }

    private void PlayKnock(Vector3 impactPosition)
    {
        if (isDead || actor.IsDied)
            return;

        isDead = true;
        knock.transform.position = impactPosition;
        knock.Play();

        actor.Die();
        actor.Knockback(Vector2.up, 10f).Forget();

        AudioManager.PlaySound("trap");

        TriggerTrapFailed().Forget();
    }

    private async UniTaskVoid TriggerTrapFailed()
    {
        bool isCanceled = await UniTask.WaitForSeconds(
                0.5f,
                cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        if (isCanceled || this == null)
            return;

        this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
    }
}

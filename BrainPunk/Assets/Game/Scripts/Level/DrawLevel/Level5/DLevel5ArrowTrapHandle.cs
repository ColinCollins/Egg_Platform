using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// DLevel_005 箭矢命中玩家时的关卡局部陷阱处理器。
/// 命中特效使用箭头与玩家的真实接触点，并且不会向玩家施加击退。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 5 Arrow Trap Handle")]
public sealed class DLevel5ArrowTrapHandle : MonoBehaviour, IEventSender
{
    #region Inspector References

    /// <summary>
    /// DLevel_005 中被箭击中的玩家 Actor。
    /// </summary>
    [SerializeField] private ActorCtrl actor;

    /// <summary>
    /// DLevel_005 已有的命中特效实例；命中时移动到真实接触点后播放。
    /// </summary>
    [SerializeField] private ParticleSystem knock;

    /// <summary>
    /// 保持箭模板现有序列化配置；为 true 时仅响应 Collision2D。
    /// </summary>
    [SerializeField] private bool isCollision = true;

    #endregion

    #region Runtime State

    private bool _isDead;

    #endregion

    #region Unity Lifecycle

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (isCollision || !IsPlayerCollider(collider))
            return;

        PlayKnock(GetClosestImpactPosition(collider));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isCollision || collision == null)
            return;

        Collider2D playerCollider = ResolvePlayerCollider(collision);
        if (playerCollider == null)
            return;

        PlayKnock(GetContactPosition(collision, playerCollider));
    }

    #endregion

    #region Impact Handling

    private static Collider2D ResolvePlayerCollider(Collision2D collision)
    {
        if (IsPlayerCollider(collision.collider))
            return collision.collider;

        return IsPlayerCollider(collision.otherCollider)
            ? collision.otherCollider
            : null;
    }

    private static bool IsPlayerCollider(Collider2D collider)
    {
        return collider != null &&
               collider.gameObject.layer == LayerMask.NameToLayer("Actor") &&
               collider.CompareTag("Player");
    }

    private Vector3 GetContactPosition(
        Collision2D collision,
        Collider2D playerCollider)
    {
        Vector2 contactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : playerCollider.ClosestPoint(transform.position);
        return PreserveEffectDepth(contactPoint);
    }

    private Vector3 GetClosestImpactPosition(Collider2D playerCollider)
    {
        Vector2 contactPoint = playerCollider.ClosestPoint(transform.position);
        return PreserveEffectDepth(contactPoint);
    }

    private Vector3 PreserveEffectDepth(Vector2 impactPoint)
    {
        float effectDepth = knock != null
            ? knock.transform.position.z
            : transform.position.z;
        return new Vector3(impactPoint.x, impactPoint.y, effectDepth);
    }

    private void PlayKnock(Vector3 impactPosition)
    {
        if (_isDead || actor == null || actor.IsDied)
            return;

        _isDead = true;
        if (knock != null)
        {
            knock.transform.position = impactPosition;
            knock.Play();
        }

        actor.Die();
        StopActorPhysics();

        AudioManager.PlaySound("trap");
        TriggerTrapFailed().Forget();
    }

    private void StopActorPhysics()
    {
        Rigidbody2D actorRigidbody = actor.RB;
        if (actorRigidbody == null)
            return;

        actorRigidbody.linearVelocity = Vector2.zero;
        actorRigidbody.angularVelocity = 0f;
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

    #endregion
}

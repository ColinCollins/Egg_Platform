using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 用于陷阱触发
/// </summary>
public class OnTriggerTrapHandle : MonoBehaviour, IEventSender
{
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private ParticleSystem knock;

    [SerializeField] private float knockForce = 10f;

    [SerializeField] private float trapDelay = 0.5f;

    /// <summary>
    /// 是否使用 Collision2D 入口；默认 false，保持原有 Trigger2D 行为。
    /// </summary>
    [SerializeField] private bool isCollision = false;
    private bool isDead = false;

    void OnTriggerEnter2D(Collider2D collider)
    {
        if (isCollision)
            return;

        TryPlayKnock(collider);
        // this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isCollision)
            return;

        TryPlayKnock(collision.collider);
    }

    private void TryPlayKnock(Collider2D collider)
    {
        if (collider == null)
            return;

        var tag = collider.gameObject.tag;
        var layerName = LayerMask.LayerToName(collider.gameObject.layer);
        if (layerName.Equals("Actor") && tag.Equals("Player"))
        {
            PlayKnock(collider);
        }
    }

    public void ManuallySet(ActorCtrl _actor, ParticleSystem _knock)
    {
        actor = _actor;
        knock = _knock;
    }

    public void PlayKnock(Collider2D collider)
    {
        if (isDead || actor.IsDied)
            return;

        isDead = true;
        knock.transform.position = actor.transform.position;
        knock.Play();

        actor.Die();
        actor.Knockback(Vector2.up, knockForce).Forget();

        AudioManager.PlaySound("trap");

        TriggerTrapFailed().Forget();
    }

    private async UniTaskVoid TriggerTrapFailed()
    {
        // 保存本次命中的 Actor，避免等待期间重新绑定后误放行旧事件。
        var hitActor = actor;

        if (trapDelay > 0)
        {
            bool isCanceled = await UniTask.WaitForSeconds(
        trapDelay,
        cancellationToken: this.GetCancellationTokenOnDestroy())
    .SuppressCancellationThrow();
            if (isCanceled || this == null)
                return;
        }

        // 旧关卡失活后 Destroy 要到帧末执行，派发前先拦截已退出现场的 Actor。
        if (this == null || hitActor == null || !hitActor.gameObject.activeInHierarchy)
            return;

        this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
    }
}

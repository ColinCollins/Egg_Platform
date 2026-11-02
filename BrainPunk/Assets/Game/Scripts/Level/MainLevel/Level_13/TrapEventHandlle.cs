using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 用于 地刺陷阱触发表现
/// </summary>
public class TrapEventHandlle : MonoBehaviour, IEventSender
{
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private ParticleSystem knock;
    [SerializeField] private float knockForce = 10f;
    private bool isDead = false;

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
        await UniTask.WaitForSeconds(0.5f);
        this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
    }
}

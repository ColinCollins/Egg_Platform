using Bear.EventSystem;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

public class FailAreaTriggerHandle : MonoBehaviour, IEventSender
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        ActorCtrl actor = ResolveActor(collision);
        if (actor == null || actor.IsDied)
            return;

        this.DispatchEvent(Witness<OnTriggerFailAreaEvent>._);
    }

    /// <summary>
    /// 从普通玩家 Collider 或承载玩家的复合 Rigidbody2D 根 Collider 中解析 Actor。
    /// 调用边界：普通角色仍校验 Player Tag；复合载体则只接受归属同一刚体的 Player Actor。
    /// </summary>
    private static ActorCtrl ResolveActor(Collider2D collision)
    {
        if (collision == null)
            return null;

        ActorCtrl actor = collision.GetComponentInParent<ActorCtrl>();
        if (actor != null)
        {
            // 普通角色仍保留 Player Tag 边界，避免继承 ActorCtrl 的敌人落入区域时误判玩家失败。
            // 复合球中的 Actor 子 Collider 交由刚体根 Collider 统一触发，避免同一刚体重复派发。
            Rigidbody2D actorRigidbody = actor.GetComponent<Rigidbody2D>();
            if (actorRigidbody == null && collision.attachedRigidbody != null)
                return null;

            return actor.CompareTag("Player") ? actor : null;
        }

        Rigidbody2D attachedRigidbody = collision.attachedRigidbody;
        if (attachedRigidbody == null || collision.transform != attachedRigidbody.transform)
            return null;

        // 球根可能因为皮肤或玩法使用 tools 等 Tag，因此以子 Actor 的 Player Tag 识别玩家身份。
        // 同时校验最近的父级刚体，避免把其它嵌套载体中的 Actor 错认成当前碰撞体的玩家。
        ActorCtrl[] actors = attachedRigidbody.GetComponentsInChildren<ActorCtrl>(true);
        foreach (ActorCtrl candidate in actors)
        {
            if (candidate.CompareTag("Player") &&
                candidate.GetComponentInParent<Rigidbody2D>() == attachedRigidbody)
            {
                return candidate;
            }
        }

        return null;
    }
}

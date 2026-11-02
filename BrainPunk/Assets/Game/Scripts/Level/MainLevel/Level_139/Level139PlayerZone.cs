using System.Collections.Generic;
using UnityEngine;

/// <summary>006 的 Player 触发判定语义，额外限定为本关玩家并去重多个身体 Collider。</summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class Level139PlayerZone : MonoBehaviour
{
    private readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();
    private ActorCtrl player;
    private BoxCollider2D zone;
    private bool requireFullBodyWidth;
    private Collider2D[] bodyColliders;

    public bool HasPlayer
    {
        get
        {
            contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            if (contacts.Count == 0 || player == null)
                return false;
            if (!requireFullBodyWidth)
                return true;

            if (!TryGetPlayerBodyBounds(out Bounds bodyBounds))
                return false;
            Bounds interval = zone.bounds;
            return bodyBounds.min.x >= interval.min.x && bodyBounds.max.x <= interval.max.x
                && bodyBounds.max.y >= interval.min.y && bodyBounds.min.y <= interval.max.y;
        }
    }

    public bool TryGetPlayerBodyBounds(out Bounds bodyBounds)
    {
        // 登梯及中心就位共用同一主体范围，不纳入推物子 Collider、阴影或饰品。
        bodyBounds = default;
        if (player == null || bodyColliders == null)
            return false;
        bool hasBody = false;
        foreach (Collider2D body in bodyColliders)
        {
            if (body == null || !body.enabled || body.isTrigger || !body.gameObject.activeInHierarchy)
                continue;
            if (!hasBody)
                bodyBounds = body.bounds;
            else
                bodyBounds.Encapsulate(body.bounds);
            hasBody = true;
        }
        return hasBody;
    }

    public void Initialize(ActorCtrl target, Vector2 size, bool requirePlayerFullWidth = false)
    {
        player = target;
        requireFullBodyWidth = requirePlayerFullWidth;
        bodyColliders = target != null ? target.GetComponents<Collider2D>() : new Collider2D[0];
        zone = GetComponent<BoxCollider2D>();
        zone.isTrigger = true;
        zone.size = size;
    }

    public void ClearContacts() => contacts.Clear();

    private void OnTriggerEnter2D(Collider2D other) => Track(other);
    private void OnTriggerStay2D(Collider2D other) => Track(other);
    private void OnTriggerExit2D(Collider2D other) => contacts.Remove(other);
    private void OnDisable() => contacts.Clear();

    private void Track(Collider2D other)
    {
        if (player != null && other.GetComponentInParent<ActorCtrl>() == player
            && (other.CompareTag("Player") || player.CompareTag("Player")))
            contacts.Add(other);
    }
}

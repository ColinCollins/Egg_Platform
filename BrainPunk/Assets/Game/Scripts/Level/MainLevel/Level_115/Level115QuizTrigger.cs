using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level 115 课桌测验入口 Trigger。只接受当前关卡的玩家主角，并保持目标世界范围不受课桌缩放影响。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class Level115QuizTrigger : MonoBehaviour
{
    [SerializeField] private Level115Ctrl owner;
    [SerializeField] private Vector2 worldSize = new Vector2(5.28f, 1.78f);

    private readonly HashSet<Collider2D> _actorContacts = new HashSet<Collider2D>();
    private BoxCollider2D _trigger;

    private void Awake()
    {
        _trigger = GetComponent<BoxCollider2D>();
        owner ??= GetComponentInParent<Level115Ctrl>();
        ApplyWorldSize();
    }

    private void OnEnable()
    {
        ApplyWorldSize();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        RegisterActorContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        RegisterActorContact(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || !_actorContacts.Remove(other))
            return;

        if (_actorContacts.Count == 0 && owner != null)
            owner.SetActorInQuizRange(false);
    }

    private void OnDisable()
    {
        _actorContacts.Clear();
        if (owner != null)
            owner.SetActorInQuizRange(false);
    }

    private void RegisterActorContact(Collider2D other)
    {
        if (!IsCurrentActor(other) || !_actorContacts.Add(other))
            return;

        if (owner != null)
            owner.SetActorInQuizRange(true);
    }

    private bool IsCurrentActor(Collider2D other)
    {
        if (other == null || !other.CompareTag("Player") || owner == null || owner.Actor == null)
            return false;

        ActorCtrl actor = other.GetComponent<ActorCtrl>();
        actor ??= other.GetComponentInParent<ActorCtrl>();
        return actor == owner.Actor;
    }

    private void ApplyWorldSize()
    {
        _trigger ??= GetComponent<BoxCollider2D>();
        if (_trigger == null)
            return;

        Vector3 scale = transform.lossyScale;
        float scaleX = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(scale.y));

        _trigger.isTrigger = true;
        _trigger.offset = Vector2.zero;
        _trigger.size = new Vector2(worldSize.x / scaleX, worldSize.y / scaleY);
    }
}

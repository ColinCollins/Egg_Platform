using UnityEngine;

/// <summary>
/// Level113 场景物触发中继；把既有 Collider2D 的接触交给本关局部状态机。
/// </summary>
public sealed class Level113TriggerRelay : MonoBehaviour
{
    public enum TriggerKind
    {
        Key,
        Chest
    }

    private Level113Ctrl owner;
    private TriggerKind kind;

    /// <summary>
    /// 配置触发归属和类型。
    /// 调用边界：仅由同一 Level113 实例的总控在 Awake 阶段调用。
    /// </summary>
    public void Configure(Level113Ctrl levelOwner, TriggerKind triggerKind)
    {
        owner = levelOwner;
        kind = triggerKind;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner == null || other == null)
            return;

        if (kind == TriggerKind.Key)
            owner.OnKeyContact(transform, other);
        else
            owner.OnChestContact(transform, other);
    }
}

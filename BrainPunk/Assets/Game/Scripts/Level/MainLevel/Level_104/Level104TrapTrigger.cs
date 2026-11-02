using UnityEngine;

/// <summary>Level 104 陷阱触发器类型；狗进入撞飞链，箭进入立即致死链。</summary>
public enum Level104TrapKind
{
    Dog,
    Arrow
}

/// <summary>
/// Level 104 专属狗/箭触发器。运行时由 <see cref="Level104Ctrl"/> 配置威胁类型并控制 armed 窗口：
/// 狗仅在 run 冲锋阶段开放一次，箭仅在自身开始下落到接地前开放；所有结算都回到 Ctrl 登记 Domain。
/// </summary>
public sealed class Level104TrapTrigger : MonoBehaviour
{
    /// <summary>本关玩法编排器；为空时尝试从父层级解析。</summary>
    [SerializeField] private Level104Ctrl ctrl;

    /// <summary>当前触发器代表狗还是箭；Ctrl 初始化时会按所属节点再次确定。</summary>
    [SerializeField] private Level104TrapKind trapKind;

    /// <summary>是否只接受本局第一次有效命中。</summary>
    [SerializeField] private bool oneShot = true;

    /// <summary>当前是否开放碰撞结算；待机狗和未开始/已接地箭均保持 false。</summary>
    [SerializeField] private bool armed;

    private bool hasFired;
    private Collider2D sourceCollider;

    /// <summary>供运行态与探针读取当前门禁。</summary>
    public bool IsArmed => armed;

    private void Awake()
    {
        sourceCollider = GetComponent<Collider2D>();
        ctrl ??= GetComponentInParent<Level104Ctrl>();
    }

    /// <summary>由 Level104Ctrl 确定归属与威胁类型，避免依赖对象命名猜测。</summary>
    public void Configure(Level104Ctrl owner, Level104TrapKind kind)
    {
        if (owner != null)
            ctrl = owner;
        trapKind = kind;
    }

    /// <summary>开启或关闭本次碰撞窗口；oneShot 已成功触发后不可再次武装。</summary>
    public void SetArmed(bool isArmed)
    {
        if (isArmed && oneShot && hasFired)
            return;

        armed = isArmed;
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (!armed || collider == null || (oneShot && hasFired))
            return;

        string layerName = LayerMask.LayerToName(collider.gameObject.layer);
        if (!layerName.Equals("Actor") || !collider.CompareTag("Player"))
            return;

        ActorCtrl targetActor = collider.GetComponent<ActorCtrl>();
        targetActor ??= collider.GetComponentInParent<ActorCtrl>();
        if (targetActor == null)
            return;

        ctrl ??= GetComponentInParent<Level104Ctrl>();
        if (ctrl == null)
            return;

        Vector3 contactPoint = CalculateContactPoint(collider);
        bool accepted = trapKind == Level104TrapKind.Dog
            ? ctrl.NotifyDogHit(targetActor, contactPoint)
            : ctrl.NotifyArrowHit(contactPoint);
        if (!accepted)
            return;

        armed = false;
        if (oneShot)
            hasFired = true;
    }

    /// <summary>用双方 Collider 的最近点中点近似首次接触点，供狗与箭的命中特效定位。</summary>
    private Vector3 CalculateContactPoint(Collider2D targetCollider)
    {
        sourceCollider ??= GetComponent<Collider2D>();

        Vector2 sourceCenter = sourceCollider != null
            ? sourceCollider.bounds.center
            : (Vector2)transform.position;
        Vector2 targetPoint = targetCollider.ClosestPoint(sourceCenter);
        Vector2 sourcePoint = sourceCollider != null
            ? sourceCollider.ClosestPoint(targetPoint)
            : sourceCenter;
        Vector2 contactPoint = (sourcePoint + targetPoint) * 0.5f;
        return new Vector3(contactPoint.x, contactPoint.y, transform.position.z);
    }
}

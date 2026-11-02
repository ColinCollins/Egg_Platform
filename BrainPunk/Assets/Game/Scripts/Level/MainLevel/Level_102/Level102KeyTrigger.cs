using UnityEngine;

/// <summary>
/// Level102 真钥匙的本关触发适配器。
/// 只识别当前玩家并通知 Level102Ctrl；钥匙的显示、收集演出和解锁时序由控制器负责。
/// </summary>
public sealed class Level102KeyTrigger : MonoBehaviour
{
    #region Inspector

    [Header("关卡引用")]
    [Tooltip("Level102 局部控制器；为空时从父级查找。")]
    [SerializeField] private Level102Ctrl levelCtrl;

    [Header("触发设置")]
    [Tooltip("钥匙收集后关闭的触发碰撞体；为空时自动收集当前节点及子节点。")]
    [SerializeField] private Collider2D keyCollider;

    [Tooltip("是否要求碰撞体属于 ActorCtrl；关闭后仅使用 Player 标签判断。")]
    [SerializeField] private bool requireActorComponent = true;

    #endregion

    #region Runtime State

    private bool collected;

    /// <summary>本次尝试是否已经通知过控制器。</summary>
    public bool IsCollected => collected;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHandlePlayerContact(other);
    }

    /// <summary>
    /// 补偿关卡初始化时角色已经与钥匙重叠的情况；控制器未开放输入时不会锁存，
    /// 后续物理帧仍可再次尝试。与 Enter 共用同一原子收集入口，避免重复演出。
    /// </summary>
    private void OnTriggerStay2D(Collider2D other)
    {
        TryHandlePlayerContact(other);
    }

    private void TryHandlePlayerContact(Collider2D other)
    {
        if (collected || !Application.isPlaying)
            return;

        ResolveReferences();
        if (!IsPlayerCollider(other))
            return;

        if (levelCtrl == null)
        {
            Debug.LogWarning("[Level102KeyTrigger] Level102Ctrl 引用缺失，无法通知钥匙收集。", this);
            return;
        }

        // 由控制器原子确认游戏仍在可交互状态后再锁存，避免暂停竞争帧丢钥匙。
        if (!levelCtrl.TryCollectKey(this))
            return;

        collected = true;
        DisableKeyTrigger();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 重置当前尝试的钥匙触发状态。
    /// 调用边界：仅由 Level102Ctrl 在同一实例复位时调用；标准重建流程无需调用。
    /// </summary>
    public void ResetForAttempt()
    {
        collected = false;
        ResolveReferences();
        SetKeyCollidersEnabled(true);
    }

    #endregion

    #region References And Validation

    private void ResolveReferences()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponentInParent<Level102Ctrl>();

        if (keyCollider == null)
            keyCollider = GetComponent<Collider2D>();

        if (keyCollider == null)
            keyCollider = GetComponentInChildren<Collider2D>(true);
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
            return false;

        ActorCtrl candidateActor = other.GetComponentInParent<ActorCtrl>();
        if (candidateActor == null)
            return !requireActorComponent && other.CompareTag("Player");
        if (candidateActor.IsDied)
            return false;

        // 优先按 Level102 实际绑定的角色实例判断。复制关卡中可能存在其它
        // ActorCtrl（敌人/装饰角色），仅凭 Actor 层不能把它们当成玩家。
        if (levelCtrl != null && levelCtrl.CurrentActor != null)
            return candidateActor == levelCtrl.CurrentActor;

        // 兼容项目中 Player 标签挂在角色子碰撞体或根节点两种情况。
        bool taggedPlayer = other.CompareTag("Player") ||
                            candidateActor.CompareTag("Player");
        if (taggedPlayer)
            return true;

        if (!requireActorComponent)
            return false;

        // 某些复制关卡只在角色根节点保留 Actor 层，子碰撞体的 Tag 可能为空。
        // 仅凭 ActorCtrl 类型不能认定是玩家，因此这里再用项目约定的 Actor 层兜底。
        int actorLayer = LayerMask.NameToLayer("Actor");
        if (actorLayer < 0)
            return false;

        return (other.gameObject.layer == actorLayer) ||
               (candidateActor.gameObject.layer == actorLayer);
    }

    private void DisableKeyTrigger()
    {
        SetKeyCollidersEnabled(false);
    }

    private void SetKeyCollidersEnabled(bool enabled)
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int index = 0; index < colliders.Length; index++)
        {
            if (colliders[index] != null)
                colliders[index].enabled = enabled;
        }

        // 即使显式指定的碰撞体位于组件层级之外，也要同步覆盖它的启停状态。
        if (keyCollider != null)
            keyCollider.enabled = enabled;
    }

    #endregion
}

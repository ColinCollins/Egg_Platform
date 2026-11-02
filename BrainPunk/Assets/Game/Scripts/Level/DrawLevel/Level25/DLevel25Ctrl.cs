using UnityEngine;

/// <summary>
/// 管理 DLevel_025 成功门与石柱的本地物理生命周期。
/// 开场和重置时锁为 Kinematic；任意一笔有效画线完成后，门与石柱同步进入 Dynamic 物理。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 25 Ctrl")]
public sealed class DLevel25Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler
{
    #region Inspector References

    [Header("Bindings")]
    [Tooltip("DrawGame_SuccessDoor 根节点上的 Rigidbody2D。")]
    [SerializeField] private Rigidbody2D doorRigidbody;

    [Tooltip("成功门新增的非 Trigger 实体碰撞盒；不要绑定原成功 Trigger。")]
    [SerializeField] private Collider2D doorSolidCollider;

    [Tooltip("石柱根节点 shizhu 上的 Rigidbody2D。")]
    [SerializeField] private Rigidbody2D pillarRigidbody;

    [Tooltip("石柱根节点 shizhu 上的非 Trigger Collider2D。")]
    [SerializeField] private Collider2D pillarCollider;

    #endregion

    #region Collision Safety

    /// <summary>
    /// 仅应用到门的实体碰撞盒。运行时始终额外排除 Actor，避免物理门推挤角色；
    /// 原成功 Trigger 不使用该掩码，因此仍能检测玩家进门。
    /// </summary>
    [Header("Collision Safety")]
    [Tooltip("门实体碰撞盒额外排除的 Layer；运行时始终包含 Actor。")]
    [SerializeField] private LayerMask doorSolidExcludeLayers = 1 << 11;

    #endregion

    #region Runtime State

    private struct RigidbodyInitialState
    {
        public Rigidbody2D Rigidbody;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
    }

    private RigidbodyInitialState _doorInitialState;
    private RigidbodyInitialState _pillarInitialState;
    private bool _isInitialized;
    private bool _isReleased;
    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        _doorInitialState = CaptureInitialState(doorRigidbody);
        _pillarInitialState = CaptureInitialState(pillarRigidbody);
        ApplyDoorCollisionSafety();
        _isInitialized = true;
        RestoreInitialState();
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 任意一笔有效画线完成后立即释放门柱；不要求画线先接触机关。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !_isInitialized || _isReleased)
            return;

        ReleaseMechanism();
    }

    /// <summary>
    /// 清线或重置当前尝试时，恢复门柱的初始位姿、Kinematic 和零速度。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying || !_isInitialized)
            return;

        RestoreInitialState();
    }

    #endregion

    #region Reference Resolution

    private void ResolveReferences()
    {
        Transform pillarTransform = transform.Find("shizhu");
        if (pillarRigidbody == null && pillarTransform != null)
            pillarRigidbody = pillarTransform.GetComponent<Rigidbody2D>();
        if (pillarCollider == null && pillarTransform != null)
            pillarCollider = pillarTransform.GetComponent<Collider2D>();

        if (doorRigidbody == null)
        {
            Rigidbody2D[] rigidbodies = GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody2D candidate = rigidbodies[i];
                if (candidate != null && candidate.gameObject.name == "DrawGame_SuccessDoor")
                {
                    doorRigidbody = candidate;
                    break;
                }
            }
        }

        if (doorSolidCollider == null && doorRigidbody != null)
        {
            Collider2D[] colliders = doorRigidbody.GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D candidate = colliders[i];
                if (candidate != null && !candidate.isTrigger)
                {
                    doorSolidCollider = candidate;
                    break;
                }
            }
        }
    }

    private bool ValidateReferences()
    {
        if (doorRigidbody != null && doorSolidCollider != null &&
            pillarRigidbody != null && pillarCollider != null)
        {
            return true;
        }

        Debug.LogError(
            "[DLevel25Ctrl] 缺少门/石柱的 Rigidbody2D/实体 Collider2D 绑定，D025 物理未启用。",
            this);
        return false;
    }

    private void ApplyDoorCollisionSafety()
    {
        doorSolidCollider.isTrigger = false;
        doorSolidCollider.excludeLayers = GetDoorSolidExcludeLayers();
    }

    private LayerMask GetDoorSolidExcludeLayers()
    {
        int excludeBits = doorSolidExcludeLayers.value;
        int actorLayer = LayerMask.NameToLayer("Actor");
        if (actorLayer >= 0)
            excludeBits |= 1 << actorLayer;

        return excludeBits;
    }

    #endregion

    #region State Control

    private static RigidbodyInitialState CaptureInitialState(Rigidbody2D target)
    {
        return new RigidbodyInitialState
        {
            Rigidbody = target,
            LocalPosition = target.transform.localPosition,
            LocalRotation = target.transform.localRotation
        };
    }

    private void RestoreInitialState()
    {
        _isReleased = false;

        HoldAndRestoreBody(_doorInitialState);
        HoldAndRestoreBody(_pillarInitialState);
        ActivateHeldBody(_doorInitialState.Rigidbody);
        ActivateHeldBody(_pillarInitialState.Rigidbody);
        Physics2D.SyncTransforms();
    }

    private static void HoldAndRestoreBody(RigidbodyInitialState state)
    {
        Rigidbody2D target = state.Rigidbody;
        if (target == null)
            return;

        target.simulated = false;
        target.bodyType = RigidbodyType2D.Kinematic;
        target.linearVelocity = Vector2.zero;
        target.angularVelocity = 0f;
        target.transform.localPosition = state.LocalPosition;
        target.transform.localRotation = state.LocalRotation;
    }

    private static void ActivateHeldBody(Rigidbody2D target)
    {
        if (target == null)
            return;

        target.bodyType = RigidbodyType2D.Kinematic;
        target.simulated = true;
        target.linearVelocity = Vector2.zero;
        target.angularVelocity = 0f;
    }

    private void ReleaseMechanism()
    {
        if (_isReleased)
            return;

        _isReleased = true;
        ReleaseBody(pillarRigidbody);
        ReleaseBody(doorRigidbody);
    }

    private static void ReleaseBody(Rigidbody2D target)
    {
        if (target == null)
            return;

        target.bodyType = RigidbodyType2D.Dynamic;
        target.linearVelocity = Vector2.zero;
        target.angularVelocity = 0f;
        target.WakeUp();
    }

    #endregion
}

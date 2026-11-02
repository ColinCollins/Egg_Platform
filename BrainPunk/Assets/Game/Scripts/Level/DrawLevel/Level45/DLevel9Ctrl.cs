using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 控制所有 ballon 子节点 Nail 与门的物理状态，第一笔画完之后同步变为 Dynamic。
/// 仅处理本关气球吊门编队的释放、浮力与完成线/门碰撞隔离，不负责画线输入、倒计时或关卡结算。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 9 Ctrl")]
public sealed class DLevel9Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler
{
    #region Inspector References

    /// <summary>
    /// 第一笔有效画线完成后切换为 Dynamic 的 Nail 刚体。
    /// 调用边界：只绑定 DLevel_009 prefab 内各 ballon 直接子节点的 Nail。
    /// 注意事项：Nail 的初始 Kinematic 状态由 DLevel_009 prefab 保证。
    /// </summary>
    [Tooltip("各 ballon 子节点下需要在第一笔完成后释放的 Nail Rigidbody2D。")]
    [SerializeField] private List<Rigidbody2D> nailRigidbodies = new();

    /// <summary>
    /// 第一笔有效画线完成后与三个 Nail 同步释放的门刚体。
    /// 调用边界：只绑定 DLevel_009 prefab 内 DoorOutside 的 Rigidbody2D。
    /// 注意事项：画线前由本控制器维持 Kinematic，避免关节提前改变编辑器摆位。
    /// </summary>
    [Tooltip("与三个气球同步释放的 DoorOutside Rigidbody2D。")]
    [SerializeField] private Rigidbody2D doorRigidbody;

    [Header("气球物理")]

    /// <summary>
    /// 三个气球向上的浮力倍率。
    /// 调用边界：由本关控制器统一写入每个 Nail 的 Rigidbody2D.gravityScale。
    /// 注意事项：使用正值配置；运行时会写入对应的负 gravityScale，0 表示关闭浮力。
    /// </summary>
    [Tooltip("气球向上的浮力倍率；0.6 会写入 Nail Rigidbody2D.gravityScale = -0.6。")]
    [SerializeField, Min(0f)] private float balloonBuoyancy = 0.6f;

    #endregion

    #region Runtime State

    private struct RigidbodyInitialState
    {
        public Rigidbody2D Rigidbody;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
    }

    private readonly List<RigidbodyInitialState> _initialStates = new();
    private readonly List<Collider2D> _completedStrokeColliders = new();
    private DrawLineFeature _drawLineFeature;
    private bool _hasCachedInitialState;
    private bool _hasReleased;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        _drawLineFeature = GetComponent<DrawLineFeature>();
        CacheInitialState();
        RestoreInitialState();
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 每笔有效画线完成后，先忽略该笔与门实体盒的碰撞，避免门面形成不可见挂点；
    /// 第一笔再将门与所有已绑定 Nail 同步切换为 Dynamic 并唤醒。
    /// 后续笔画不会再次修改气球吊门编队的物理状态，但仍会登记门/线忽略碰撞。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying)
            return;

        IgnoreDoorCollisionWithCompletedStroke(result.StrokeIndex);

        if (!result.IsFirstStroke || _hasReleased)
            return;

        _hasReleased = true;
        ApplyBalloonBuoyancy();
        ReleaseRigidbody(doorRigidbody);

        for (int i = 0; i < nailRigidbodies.Count; i++)
            ReleaseRigidbody(nailRigidbodies[i]);
    }

    /// <summary>
    /// 当前尝试重置时恢复门与三个 Nail 的初始相对位姿、Kinematic 状态和零速度。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        RestoreInitialState();
    }

    #endregion

    #region State Control

    /// <summary>
    /// 仅忽略 DoorOutside 实体盒与指定完成笔画的碰撞，让线条只通过三个气球传递负载。
    /// 调用边界：必须在 DrawLineFeature 已登记完成笔画后调用；地面、角色与门的碰撞不受影响。
    /// </summary>
    private void IgnoreDoorCollisionWithCompletedStroke(int strokeIndex)
    {
        Collider2D doorCollider = doorRigidbody != null
            ? doorRigidbody.GetComponent<Collider2D>()
            : null;
        if (doorCollider == null)
            return;

        _drawLineFeature ??= GetComponent<DrawLineFeature>();
        if (_drawLineFeature == null ||
            !_drawLineFeature.TryGetCompletedStrokeObject(strokeIndex, out GameObject strokeObject))
        {
            return;
        }

        _completedStrokeColliders.Clear();
        strokeObject.GetComponentsInChildren(true, _completedStrokeColliders);
        for (int i = 0; i < _completedStrokeColliders.Count; i++)
        {
            Collider2D strokeCollider = _completedStrokeColliders[i];
            if (strokeCollider != null && strokeCollider != doorCollider)
                Physics2D.IgnoreCollision(doorCollider, strokeCollider, true);
        }

        _completedStrokeColliders.Clear();
    }

    private void CacheInitialState()
    {
        if (_hasCachedInitialState)
            return;

        _initialStates.Clear();
        CacheRigidbodyInitialState(doorRigidbody);
        for (int i = 0; i < nailRigidbodies.Count; i++)
            CacheRigidbodyInitialState(nailRigidbodies[i]);

        _hasCachedInitialState = true;
    }

    private void CacheRigidbodyInitialState(Rigidbody2D targetRigidbody)
    {
        if (targetRigidbody == null)
            return;

        _initialStates.Add(new RigidbodyInitialState
        {
            Rigidbody = targetRigidbody,
            LocalPosition = targetRigidbody.transform.localPosition,
            LocalRotation = targetRigidbody.transform.localRotation
        });
    }

    private void RestoreInitialState()
    {
        _hasReleased = false;
        if (!_hasCachedInitialState)
            CacheInitialState();

        RestoreBalloonCollisions();
        ApplyBalloonBuoyancy();
        for (int i = 0; i < _initialStates.Count; i++)
        {
            RigidbodyInitialState initialState = _initialStates[i];
            Rigidbody2D targetRigidbody = initialState.Rigidbody;
            if (targetRigidbody == null)
                continue;

            targetRigidbody.bodyType = RigidbodyType2D.Kinematic;
            targetRigidbody.linearVelocity = Vector2.zero;
            targetRigidbody.angularVelocity = 0f;
            targetRigidbody.transform.localPosition = initialState.LocalPosition;
            targetRigidbody.transform.localRotation = initialState.LocalRotation;
        }
    }

    private static void ReleaseRigidbody(Rigidbody2D targetRigidbody)
    {
        if (targetRigidbody == null)
            return;

        targetRigidbody.bodyType = RigidbodyType2D.Dynamic;
        targetRigidbody.linearVelocity = Vector2.zero;
        targetRigidbody.angularVelocity = 0f;
        targetRigidbody.WakeUp();
    }

    private void ApplyBalloonBuoyancy()
    {
        float gravityScale = -Mathf.Max(0f, balloonBuoyancy);
        for (int i = 0; i < nailRigidbodies.Count; i++)
        {
            Rigidbody2D nailRigidbody = nailRigidbodies[i];
            if (nailRigidbody == null)
                continue;

            nailRigidbody.gravityScale = gravityScale;
        }
    }

    private void RestoreBalloonCollisions()
    {
        for (int firstIndex = 0; firstIndex < nailRigidbodies.Count; firstIndex++)
        {
            Rigidbody2D firstRigidbody = nailRigidbodies[firstIndex];
            Collider2D firstCollider = firstRigidbody != null
                ? firstRigidbody.GetComponent<Collider2D>()
                : null;
            if (firstCollider == null)
                continue;

            for (int secondIndex = firstIndex + 1;
                 secondIndex < nailRigidbodies.Count;
                 secondIndex++)
            {
                Rigidbody2D secondRigidbody = nailRigidbodies[secondIndex];
                Collider2D secondCollider = secondRigidbody != null
                    ? secondRigidbody.GetComponent<Collider2D>()
                    : null;
                if (secondCollider == null || secondCollider == firstCollider)
                    continue;

                Physics2D.IgnoreCollision(firstCollider, secondCollider, false);
            }
        }
    }

    #endregion
}

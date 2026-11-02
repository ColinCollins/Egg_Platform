using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// D085 顶部尖刺的致死接触入口，复用既有 Knock 表现与 Draw 失败演出窗口。
/// 球内 Actor 没有独立刚体，受击运动只作用于显式绑定的球载体。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 85 Spike Trap Handle")]
public sealed class DLevel85SpikeTrapHandle : MonoBehaviour, IDrawStrokeCompletedHandler
{
    #region Inspector

    /// <summary>当前 D085 根控制器，负责玩家身份及失败重建；必须显式绑定本关实例。</summary>
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>本关 yellow_ball 刚体，同时用于接触身份过滤和受击运动。</summary>
    [SerializeField] private Rigidbody2D playerBall;

    /// <summary>顶部尖刺碰撞体；局部 up 为刺尖方向，用于接触点和击退方向。</summary>
    [SerializeField] private Collider2D spikeCollider;

    /// <summary>本关内 D003 同款 Knock 实例，须移除源 Prefab 的敲门子表现。</summary>
    [SerializeField] private ParticleSystem knockEffect;

    /// <summary>命中后保留演出的非缩放秒数，结束后提交标准 Draw 失败。</summary>
    [SerializeField, Min(0f)] private float failDelay = 0.5f;

    /// <summary>仅死亡表现时沿刺尖方向设置球的速度；不改变正常接触弹性。</summary>
    [SerializeField, Min(0f)] private float knockbackSpeed = 10f;

    #endregion

    #region Failure Performance

    private bool _ownsFailurePerformance;
    private float _remainingTime;

    /// <summary>
    /// 由本关 SpikeStrip_00 的 OnTrigger2DHandle 显式调用；只接受本关球的复合碰撞体。
    /// 先锁定失败演出，防止 Actor 死亡被通用 Tick 提前结算或被重复触发。
    /// </summary>
    public void OnSpikeTriggered(Collider2D collider)
    {
        if (!Application.isPlaying || !isActiveAndEnabled ||
            _ownsFailurePerformance || collider == null ||
            drawLevelCtrl == null || playerBall == null ||
            spikeCollider == null || knockEffect == null ||
            collider.attachedRigidbody != playerBall || drawLevelCtrl.IsOutcomeLocked)
            return;

        ActorCtrl actor = drawLevelCtrl.Actor;
        if (actor == null || actor.IsDied || !drawLevelCtrl.TryBeginFailurePerformance())
            return;

        _ownsFailurePerformance = true;
        _remainingTime = Mathf.Max(0f, failDelay);

        Vector2 point = spikeCollider.ClosestPoint(playerBall.worldCenterOfMass);
        knockEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        knockEffect.transform.position = new Vector3(point.x, point.y, knockEffect.transform.position.z);
        knockEffect.Play(true);
        actor.Die();
        if (knockbackSpeed > 0f)
            playerBall.linearVelocity = (Vector2)spikeCollider.transform.up * knockbackSpeed;
        AudioManager.PlaySound("trap");
    }

    private void Update()
    {
        if (!_ownsFailurePerformance)
            return;

        _remainingTime -= Time.unscaledDeltaTime;
        if (_remainingTime > 0f)
            return;

        _ownsFailurePerformance = false;
        if (drawLevelCtrl != null)
            drawLevelCtrl.CompleteFailurePerformance(nameof(DLevel85SpikeTrapHandle));
    }

    #endregion

    #region Cleanup And Draw Lifecycle

    private void OnDisable()
    {
        if (Application.isPlaying)
            ClearFailurePerformance();
    }

    /// <inheritdoc />
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
    }

    /// <summary>尝试重置只取消本组件未提交的演出；关卡重建由 DrawPlayCtrl 负责。</summary>
    public void OnDrawAttemptReset()
    {
        if (Application.isPlaying)
            ClearFailurePerformance();
    }

    private void ClearFailurePerformance()
    {
        if (_ownsFailurePerformance)
        {
            _ownsFailurePerformance = false;
            if (drawLevelCtrl != null)
                drawLevelCtrl.CancelFailurePerformance();
        }

        _remainingTime = 0f;
        if (knockEffect != null)
            knockEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    #endregion
}

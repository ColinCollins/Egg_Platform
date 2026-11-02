using Bear.Logger;
using UnityEngine;

/// <summary>
/// 第 70 关碰撞成功入口：Actor 完整播放 eat 后进入标准 Draw 胜利结算。
/// </summary>
[DisallowMultipleComponent]
public sealed class DLevel70Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Inspector References

    /// <summary>
    /// 当前 DLevel_070 的结算控制器；必须在 Prefab Inspector 显式绑定。
    /// 只复用成功演出门禁与标准结算，不直接操作 UI、存档或打点。
    /// </summary>
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 当前关卡玩家的专用 Actor 子类；必须显式绑定，不能绑定其它关的 Actor。
    /// </summary>
    [SerializeField] private DLevel70ActorCtrl actorCtrl;

    #endregion

    #region Runtime State

    private bool _isEating;

    #endregion

    #region Public API

    /// <summary>
    /// CollisionEnter 动态事件入口；只有碰撞到已绑定玩家刚体才触发 eat。
    /// 调用边界：由本关 fruit 的 LayerCollisionDetector.OnEnter 绑定，不接收 TriggerEnter。
    /// </summary>
    public void OnCollisionEnter(Collision2D collision)
    {
        if (!Application.isPlaying || collision == null || actorCtrl == null || actorCtrl.RB == null)
            return;

        if (collision.rigidbody == actorCtrl.RB)
            PlayEatAndWin();
    }

    /// <summary>
    /// 无参数成功 API，也可绑定 Inspector 事件；启动一次 eat，完成后提交胜利。
    /// 调用边界：关卡初始化后且未暂停、失败或结算时有效；重复触发不会重播或重复结算。
    /// </summary>
    public void PlayEatAndWin()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || _isEating ||
            drawLevelCtrl == null || !drawLevelCtrl.IsRuntimeReady || drawLevelCtrl.IsPaused ||
            actorCtrl == null || !actorCtrl.isActiveAndEnabled || actorCtrl.IsDied)
        {
            return;
        }

        if (!drawLevelCtrl.TryBeginSuccessPerformance())
            return;

        _isEating = true;
        if (!actorCtrl.PlayEat(OnEatComplete, OnEatCancelled))
        {
            OnEatCancelled();
            this.LogWarning("[DLevel70Ctrl] eat 动画不可用，本次成功演出已取消。");
        }
    }

    #endregion

    #region Draw Lifecycle

    /// <summary>
    /// 本组件只消费现有 Handler 的 Reset 通知；画线完成不直接触发 eat。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
    }

    /// <summary>
    /// 同实例尝试重置时解除 eat 回调；通常 Reset/Replay 仍由共享流程整关重建。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        if (Application.isPlaying)
            CancelEatFlow();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            CancelEatFlow();
    }

    private void CancelEatFlow()
    {
        if (!_isEating)
            return;

        actorCtrl?.CancelEat();
        OnEatCancelled();
    }

    private void OnEatComplete()
    {
        if (!_isEating || !isActiveAndEnabled)
            return;

        _isEating = false;
        drawLevelCtrl.CompleteSuccessPerformance(nameof(DLevel70Ctrl));
    }

    private void OnEatCancelled()
    {
        if (!_isEating)
            return;

        _isEating = false;
        drawLevelCtrl?.CancelSuccessPerformance();
    }

    #endregion
}

using Bear.Logger;
using Spine;
using UnityEngine;

/// <summary>
/// 第 75 关局部控制器：首笔后驱动 enemy 向右，失败门开门完成后提交标准 Draw 失败。
/// </summary>
[DisallowMultipleComponent]
public sealed class DLevel75Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Inspector References

    /// <summary>
    /// 本关 Draw 控制器；必须在 Prefab Inspector 显式绑定，用于暂停、结果门禁和标准失败结算。
    /// </summary>
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 本关 enemy 根节点上的 ActorCtrl；必须显式绑定，首笔后驱动右移，开门时隐藏其 GameObject。
    /// </summary>
    [SerializeField] private ActorCtrl enemy;

    /// <summary>
    /// 本关 FailedDoor 的门 Spine；必须显式绑定，只播放 door_open，不使用成功门彩纸流程。
    /// </summary>
    [SerializeField] private ActorSpineCtrl failedDoorSpine;

    #endregion

    #region Runtime State

    private bool _moveRight;
    private bool _moveInputApplied;
    private TrackEntry _openEntry;
    private bool _enemyWasActive;

    #endregion

    #region Public API

    /// <summary>
    /// 播放 FailedDoor 的非循环开门动画，同时隐藏 enemy；动画完成后发送标准 Draw 失败事件。
    /// 调用边界：可直接调用或绑定无参数 UnityEvent；本关初始化后且未暂停、未结算时有效。
    /// 重复调用不会重播或重复结算；缺少动画时不隐藏 enemy，不占用失败门禁。
    /// </summary>
    public void PlayFailedDoorAndFail()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || _openEntry != null ||
            drawLevelCtrl == null || !drawLevelCtrl.IsRuntimeReady || drawLevelCtrl.IsPaused ||
            enemy == null || failedDoorSpine == null || !failedDoorSpine.isActiveAndEnabled)
        {
            return;
        }

        Spine.AnimationState animationState = failedDoorSpine.AnimationState;
        if (animationState == null || animationState.Data.SkeletonData.FindAnimation("door_open") == null)
        {
            this.LogError("[DLevel75Ctrl] FailedDoor 的 door_open 动画不可用，未触发失败。");
            return;
        }

        if (!drawLevelCtrl.TryBeginFailurePerformance())
            return;

        _openEntry = failedDoorSpine.PlayAnimation("door_open", false, false);
        if (_openEntry == null)
        {
            drawLevelCtrl.CancelFailurePerformance();
            return;
        }

        _openEntry.Complete += OnDoorOpenComplete;
        _openEntry.Interrupt += OnDoorOpenInterrupted;
        _openEntry.End += OnDoorOpenInterrupted;
        ClearMoveInput();
        _enemyWasActive = enemy.gameObject.activeSelf;
        enemy.gameObject.SetActive(false);
    }

    #endregion

    #region Draw Lifecycle

    /// <summary>
    /// 接收本关有效笔画完成通知；仅第一笔启动 enemy 持续右移，不附加延迟。
    /// 调用边界：由 DrawLevelCtrl 分发，暂停、死亡或结果锁定期间不启动移动。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !result.IsFirstStroke ||
            drawLevelCtrl == null || !drawLevelCtrl.IsRuntimeReady || drawLevelCtrl.IsPaused ||
            drawLevelCtrl.IsOutcomeLocked || enemy == null || !enemy.isActiveAndEnabled || enemy.IsDied)
        {
            return;
        }

        _moveRight = true;
    }

    /// <summary>
    /// 同实例尝试重置时停止 enemy，解除开门回调并恢复被本流程隐藏的 enemy。
    /// 调用边界：由 DrawLevelCtrl 分发；正常失败、Reset、Replay 仍由标准流程重建整关。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying)
            return;

        StopMovingRight();
        CancelDoorOpen();
    }

    private void OnEnable()
    {
        if (Application.isPlaying && drawLevelCtrl != null)
            drawLevelCtrl.Resolved += OnLevelResolved;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved -= OnLevelResolved;

        StopMovingRight();
        CancelDoorOpen();
    }

    private void Update()
    {
        if (!Application.isPlaying || !_moveRight || enemy == null)
            return;

        if (drawLevelCtrl == null || drawLevelCtrl.IsPaused || drawLevelCtrl.IsOutcomeLocked ||
            !enemy.isActiveAndEnabled || enemy.IsDied)
        {
            ClearMoveInput();
            return;
        }

        enemy.SetMoveInput(1f);
        _moveInputApplied = true;
        enemy.OnUpdate();
    }

    private void OnLevelResolved(DrawLevelResult result)
    {
        StopMovingRight();
    }

    #endregion

    #region Local Helpers

    private void StopMovingRight()
    {
        _moveRight = false;
        ClearMoveInput();
    }

    private void ClearMoveInput()
    {
        if (!_moveInputApplied || enemy == null)
            return;

        enemy.SetMoveInput(0f);
        enemy.UpdateAnimation();
        _moveInputApplied = false;
    }

    private void OnDoorOpenComplete(TrackEntry entry)
    {
        if (!ReferenceEquals(entry, _openEntry) || !isActiveAndEnabled)
            return;

        DetachDoorOpen();
        drawLevelCtrl.CompleteFailurePerformance(nameof(DLevel75Ctrl));
    }

    private void OnDoorOpenInterrupted(TrackEntry entry)
    {
        if (ReferenceEquals(entry, _openEntry))
            CancelDoorOpen();
    }

    private void CancelDoorOpen()
    {
        if (_openEntry == null)
            return;

        TrackEntry entry = _openEntry;
        DetachDoorOpen();
        if (failedDoorSpine != null && failedDoorSpine.AnimationState?.GetCurrent(0) == entry)
            failedDoorSpine.AnimationState.ClearTrack(0);

        if (enemy != null)
            enemy.gameObject.SetActive(_enemyWasActive);

        drawLevelCtrl?.CancelFailurePerformance();
    }

    private void DetachDoorOpen()
    {
        _openEntry.Complete -= OnDoorOpenComplete;
        _openEntry.Interrupt -= OnDoorOpenInterrupted;
        _openEntry.End -= OnDoorOpenInterrupted;
        _openEntry = null;
    }

    #endregion
}

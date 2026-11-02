using Bear.Logger;
using Spine;
using UnityEngine;

/// <summary>
/// 第 76 关双角色进门控制：首笔后 CuteActor 右移，门在同一轮尝试中只打开一次，两个角色进门后才能通关。
/// </summary>
[DisallowMultipleComponent]
public sealed class DLevel76Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Inspector References

    /// <summary>
    /// 本关共享 Draw 控制器；必须显式绑定，用于暂停、结果门禁及标准胜利结算。
    /// </summary>
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 本关玩家 Actor；必须显式绑定，原有 DLevel001Ctrl 继续负责其首笔右移。
    /// </summary>
    [SerializeField] private ActorCtrl actor;

    /// <summary>
    /// 本关第二角色 CuteActor；必须显式绑定，首笔完成后由本脚本驱动其向右移动。
    /// </summary>
    [SerializeField] private ActorCtrl cuteActor;

    /// <summary>
    /// 本关共用门的 Spine 控制器；必须显式绑定，首个进门角色触发一次非循环 door_open。
    /// </summary>
    [SerializeField] private ActorSpineCtrl doorSpine;

    #endregion

    #region Runtime State

    private bool _moveCuteRight;
    private bool _cuteMoveInputApplied;
    private bool _actorEntered;
    private bool _cuteActorEntered;
    private bool _doorOpened;
    private bool _successPending;
    private TrackEntry _openEntry;
    private ActorCtrl _openingActor;

    #endregion

    #region Public API

    /// <summary>
    /// 门 TriggerEnter 的动态 Collider2D 入口；只接受已绑定 Actor 或 CuteActor 的刚体。
    /// 首个角色进入时开门并隐藏；第二个角色复用进行中的开门动画，或在门已打开时直接进入结算。
    /// 调用边界：关卡初始化后且未暂停/结算时有效，同一角色的重复碰撞不会重复计数。
    /// </summary>
    public void OnDoorEnter(Collider2D collider)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || collider == null ||
            drawLevelCtrl == null || !drawLevelCtrl.IsRuntimeReady || drawLevelCtrl.IsPaused ||
            drawLevelCtrl.IsOutcomeLocked || actor == null || cuteActor == null || actor == cuteActor ||
            doorSpine == null || !doorSpine.isActiveAndEnabled)
        {
            return;
        }

        Rigidbody2D body = collider.attachedRigidbody;
        if (body == null)
            return;

        ActorCtrl enteringActor;
        if (body == actor.RB && !_actorEntered)
            enteringActor = actor;
        else if (body == cuteActor.RB && !_cuteActorEntered)
            enteringActor = cuteActor;
        else
            return;

        if (!enteringActor.isActiveAndEnabled || enteringActor.IsDied)
            return;

        bool isSecondActor = _actorEntered || _cuteActorEntered;
        if (isSecondActor && !drawLevelCtrl.TryBeginSuccessPerformance())
            return;

        _successPending = isSecondActor;

        if (_doorOpened)
        {
            AcceptActorEntry(enteringActor);
            TryCompleteSuccess();
            return;
        }

        if (_openEntry != null)
        {
            AcceptActorEntry(enteringActor);
            return;
        }

        Spine.AnimationState state = doorSpine.AnimationState;
        if (state == null || state.Data.SkeletonData.FindAnimation("door_open") == null)
        {
            CancelPendingSuccess();
            this.LogError("[DLevel76Ctrl] door_open 动画不可用，未隐藏角色或结算。");
            return;
        }

        _openEntry = doorSpine.PlayAnimation("door_open", false, false);
        if (_openEntry == null)
        {
            CancelPendingSuccess();
            return;
        }

        _openingActor = enteringActor;
        _openEntry.Complete += OnDoorOpenComplete;
        _openEntry.Interrupt += OnDoorOpenInterrupted;
        _openEntry.End += OnDoorOpenInterrupted;
        AcceptActorEntry(enteringActor);
    }

    private void AcceptActorEntry(ActorCtrl enteringActor)
    {
        enteringActor.SetMoveInput(0f);
        enteringActor.StopMoving();
        if (enteringActor == actor)
            _actorEntered = true;
        else
        {
            _cuteActorEntered = true;
            _cuteMoveInputApplied = false;
        }
        enteringActor.gameObject.SetActive(false);
    }

    #endregion

    #region Draw Lifecycle

    /// <summary>
    /// 接收有效笔画通知；第一笔完成后启动 CuteActor 持续右移，无附加延迟。
    /// 调用边界：由同关 DrawLevelCtrl 分发，后续笔画不改变已进门角色的状态。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !result.IsFirstStroke ||
            drawLevelCtrl == null || !drawLevelCtrl.IsRuntimeReady || drawLevelCtrl.IsPaused ||
            drawLevelCtrl.IsOutcomeLocked || cuteActor == null || !cuteActor.isActiveAndEnabled ||
            cuteActor.IsDied || _cuteActorEntered)
        {
            return;
        }

        _moveCuteRight = true;
    }

    /// <summary>
    /// 同实例尝试重置时停止 CuteActor，取消未完成演出并恢复两个角色进门前的可见/可移动状态。
    /// 调用边界：由 DrawLevelCtrl 分发；正常 Reset/Replay 仍使用标准整关重建。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying)
            return;

        StopCuteMoving();
        CancelDoorOpen();
        RestoreEnteredActors();
        _doorOpened = false;
        if (doorSpine != null && doorSpine.AnimationState != null)
            doorSpine.PlayAnimation("door_idle", false, false);
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
        StopCuteMoving();
        CancelDoorOpen();
        RestoreEnteredActors();
        _doorOpened = false;
    }

    private void Update()
    {
        if (!Application.isPlaying || !_moveCuteRight || cuteActor == null)
            return;

        if (drawLevelCtrl == null || drawLevelCtrl.IsPaused || drawLevelCtrl.IsOutcomeLocked ||
            _cuteActorEntered || !cuteActor.isActiveAndEnabled || cuteActor.IsDied)
        {
            ClearCuteMoveInput();
            return;
        }

        cuteActor.SetMoveInput(1f);
        _cuteMoveInputApplied = true;
        cuteActor.OnUpdate();
    }

    private void OnLevelResolved(DrawLevelResult result)
    {
        StopCuteMoving();
        DetachDoorOpen();
    }

    #endregion

    #region Local Helpers

    private void StopCuteMoving()
    {
        _moveCuteRight = false;
        ClearCuteMoveInput();
    }

    private void ClearCuteMoveInput()
    {
        if (!_cuteMoveInputApplied || cuteActor == null)
            return;

        cuteActor.SetMoveInput(0f);
        cuteActor.UpdateAnimation();
        _cuteMoveInputApplied = false;
    }

    private void OnDoorOpenComplete(TrackEntry entry)
    {
        if (!ReferenceEquals(entry, _openEntry) || !isActiveAndEnabled)
            return;

        _doorOpened = true;
        DetachDoorOpen();
        TryCompleteSuccess();
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
        ActorCtrl openingActor = _openingActor;
        DetachDoorOpen();
        if (doorSpine != null && doorSpine.AnimationState?.GetCurrent(0) == entry)
            doorSpine.AnimationState.ClearTrack(0);
        if (openingActor == actor)
            RestoreEnteredActor(actor, ref _actorEntered);
        else if (openingActor == cuteActor)
            RestoreEnteredActor(cuteActor, ref _cuteActorEntered);
        if (_successPending)
            drawLevelCtrl?.CancelSuccessPerformance();
        _successPending = false;
    }

    private void CancelPendingSuccess()
    {
        if (_successPending)
            drawLevelCtrl?.CancelSuccessPerformance();
        _successPending = false;
    }

    private void TryCompleteSuccess()
    {
        if (!_successPending || !_doorOpened || !_actorEntered || !_cuteActorEntered)
            return;

        _successPending = false;
        drawLevelCtrl.CompleteSuccessPerformance(nameof(DLevel76Ctrl));
    }

    private void RestoreEnteredActors()
    {
        RestoreEnteredActor(actor, ref _actorEntered);
        RestoreEnteredActor(cuteActor, ref _cuteActorEntered);
    }

    private void RestoreEnteredActor(ActorCtrl target, ref bool entered)
    {
        if (!entered)
            return;

        entered = false;
        if (target == null)
            return;
        target.ResumeMoving();
        target.gameObject.SetActive(true);
    }

    private void DetachDoorOpen()
    {
        if (_openEntry == null)
            return;

        _openEntry.Complete -= OnDoorOpenComplete;
        _openEntry.Interrupt -= OnDoorOpenInterrupted;
        _openEntry.End -= OnDoorOpenInterrupted;
        _openEntry = null;
        _openingActor = null;
    }

    #endregion
}

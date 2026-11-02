using UnityEngine;

/// <summary>只在成功乘梯期间接管本关玩家；共享 Actor 和其它关卡不变。</summary>
public sealed class Level139ActorCtrl : ActorCtrl
{
    private bool elevatorLocked;
    private bool savedSimulated;
    private bool portalTransit;
    private bool portalWalkOut;

    public void SetPortalTransit(bool active, bool allowAutomaticWalkOut)
    {
        portalTransit = active;
        portalWalkOut = active && allowAutomaticWalkOut;
        if (active && !portalWalkOut)
        {
            SetMoveInput(0f);
            StopMoving();
        }
    }

    public void LockForElevator()
    {
        if (elevatorLocked)
            return;
        elevatorLocked = true;
        SetMoveInput(0f);
        StopMoving();
        if (RB != null)
        {
            savedSimulated = RB.simulated;
            RB.linearVelocity = Vector2.zero;
            RB.angularVelocity = 0f;
            RB.simulated = false;
        }
        SpineCtrl?.PlayAnimation("idle", true, false);
    }

    public void ReleaseElevatorLock()
    {
        if (!elevatorLocked)
            return;
        elevatorLocked = false;
        if (RB != null)
            RB.simulated = savedSimulated;
        ResumeMoving();
        SetMoveInput(0f);
    }

    public override void OnUpdate()
    {
        // BaseLevelCtrl 的 Jump 事件不受 SetCtrlable 门禁控制，所以本关同时门禁
        // JumpButton 和 OnUpdate；暂停恢复事件不能解除此锁，也不会调用落地缓冲跳。
        if (!elevatorLocked && (!portalTransit || portalWalkOut))
            base.OnUpdate();
    }

    protected override void HandleInput()
    {
        // 本关出门演出通过 SetMoveInput 自动行走；这期间不接收编辑器键盘覆盖。
        if (!portalTransit)
            base.HandleInput();
    }

    protected override void JumpButton()
    {
        if (!elevatorLocked && !portalTransit)
            base.JumpButton();
    }
}

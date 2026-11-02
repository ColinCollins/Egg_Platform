using Bear.Logger;
using UnityEngine;

/// <summary>
/// 操作手柄动画 controlhandle.
/// 进入 isControlHandle 状态，播放 controlhandle 动画。不能进行其他操作。
/// </summary>
public class Level70ActorCtrl : ActorCtrl, IDebuger
{
    private const string ControlHandleAnimName = "controlhandle";

    private bool isControlHandle = false;
    private bool isLevel70HookLocked = false;
    private int controlHandleFlowVersion = 0;

    public override void OnUpdate()
    {
        if (isLevel70HookLocked)
        {
            UpdateLevel70HookLockedState();
            return;
        }

        if (isControlHandle)
        {
            UpdateControlHandleState();
            return;
        }

        base.OnUpdate();
    }

    protected override void HandleAnimators()
    {
        if (isControlHandle || isLevel70HookLocked)
            return;

        base.HandleAnimators();
    }

    /// <summary>
    /// 进入 Level70 抓钩控制锁定；调用边界是 Level70Ctrl 的抓钩入口，注意不要从共享关卡或普通触发器调用。
    /// </summary>
    public void EnterLevel70HookControl()
    {
        isLevel70HookLocked = true;
        PlayControlHandleOnce();
        StopMoving();
        StopAllMotion();
    }

    /// <summary>
    /// 通过 Level70 恢复 Trigger2D 恢复 actor 普通移动；controlhandle 动画状态下会拒绝恢复，调用方必须检查返回值。
    /// </summary>
    public bool RestoreLevel70NormalControl()
    {
        if (isControlHandle)
        {
            // LockLevel70MoveInput();
            StopHorizontalMotion();
            return false;
        }

        isLevel70HookLocked = false;
        StopControlHandle();
        SetMoveInput(0f);

        if (!IsDied)
            ResumeMoving();

        return true;
    }

    /// <summary>
    /// Level70 失败、重开、退出或对象禁用时的兜底清理；用于避免 actor 残留在抓钩锁定状态。
    /// </summary>
    public void ResetLevel70HookStateForCleanup()
    {
        isLevel70HookLocked = false;
        StopControlHandle();
        SetMoveInput(0f);

        if (!IsDied)
            ResumeMoving();
    }

    private void PlayControlHandleOnce()
    {
        if (IsDied || !HasControlHandleAnimation())
        {
            return;
        }

        isControlHandle = true;
        int flowVersion = ++controlHandleFlowVersion;
        LockLevel70MoveInput();
        StopHorizontalMotion();

        Spine.TrackEntry trackEntry = SpineCtrl.PlayAnimation(ControlHandleAnimName, false, false);
        if (trackEntry == null)
        {
            isControlHandle = false;
            UpdateAnimation();
            return;
        }

        trackEntry.Complete += (_) => CompleteControlHandle(flowVersion);
    }

    private void StopControlHandle()
    {
        if (!isControlHandle)
            return;

        isControlHandle = false;
        controlHandleFlowVersion++;

        if (!IsDied)
        {
            UpdateAnimation();
        }
    }

    private void CompleteControlHandle(int flowVersion)
    {
        if (!isActiveAndEnabled || flowVersion != controlHandleFlowVersion || IsDied || !isControlHandle)
            return;

        isControlHandle = false;

        if (!isLevel70HookLocked)
        {
            UpdateAnimation();
        }
    }

    private void UpdateControlHandleState()
    {
        if (!IsReady)
            return;

        LockLevel70MoveInput();
        UpdateAirbornStatus();
        StopHorizontalMotion();

        HandleCollision();
    }

    private void UpdateLevel70HookLockedState()
    {
        if (!IsReady)
            return;

        // LockLevel70MoveInput();
        UpdateAirbornStatus();
        StopAllMotion();

        HandleCollision();
    }

    private void StopAllMotion()
    {
        SetMoveInput(0f);
    }

    private void LockLevel70MoveInput()
    {
        // StopMoving();
    }

    private void StopHorizontalMotion()
    {
        SetMoveInput(0f);

        if (RB != null)
        {
            RB.linearVelocity = new Vector2(0f, RB.linearVelocityY);
        }
    }

    private bool HasControlHandleAnimation()
    {
        if (SpineCtrl == null || SpineCtrl.AnimationState == null)
        {
            this.LogWarning("[Level70ActorCtrl] SpineCtrl or AnimationState is missing.");
            return false;
        }

        var skeletonData = SpineCtrl.AnimationState.Data?.SkeletonData;
        if (skeletonData == null)
        {
            this.LogWarning("[Level70ActorCtrl] SkeletonData is missing.");
            return false;
        }

        if (skeletonData.FindAnimation(ControlHandleAnimName) != null)
        {
            return true;
        }

        this.LogWarning($"[Level70ActorCtrl] Animation not found: {ControlHandleAnimName}");
        return false;
    }

    private void OnDisable()
    {
        isControlHandle = false;
        isLevel70HookLocked = false;
        controlHandleFlowVersion++;
    }
}

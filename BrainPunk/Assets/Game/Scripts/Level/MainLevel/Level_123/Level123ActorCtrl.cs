using UnityEngine;

/// <summary>
/// Level123 专用角色扩展。纸片状态保留水平移动惯性，但屏蔽跳跃。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level123ActorCtrl : ActorCtrl
{
    private bool jumpLocked;

    /// <summary>
    /// 设置本关纸片状态的跳跃锁。
    /// </summary>
    public void SetJumpLocked(bool locked)
    {
        jumpLocked = locked;
    }

    public override void OnUpdate()
    {
        if (!jumpLocked)
        {
            base.OnUpdate();
            return;
        }

        // 保留共享碰撞和动画更新；水平速度交给 Level123Ctrl，纸片外观保持固定朝向。
        if (!IsReady)
            return;

        UpdateAirbornStatus();
        if (IsSit)
            return;

        if (IsKnocked)
        {
            HandleCollision();
            HandleAnimators();
            return;
        }

        HandleInput();
        HandleCollision();
        HandleAnimators();
    }

    protected override void JumpButton()
    {
        if (jumpLocked)
            return;

        base.JumpButton();
    }

    protected override void HandleLanding()
    {
        // 纸片状态不消费落地时的跳跃缓冲。
        if (jumpLocked)
            return;

        base.HandleLanding();
    }
}

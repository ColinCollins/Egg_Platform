using Game.Scripts.Common;
using UnityEngine;

/// <summary>左侧楼层门的手动开关状态；只在完全打开后放行渐隐换层。</summary>
public sealed class Level139PortalDoor
{
    private ActorSpineCtrl spine;
    private Level139PortalVisual visual;
    private bool wantsOpen;
    private bool ownsPose;
    private Spine.TrackEntry motion;
    public bool IsOpen => wantsOpen && motion != null && motion.IsComplete
        && spine != null && spine.AnimationState?.GetCurrent(0) == motion;

    public void Initialize(Transform door)
    {
        spine = door.GetComponent<ActorSpineCtrl>();
        visual = door.GetComponent<Level139PortalVisual>();
        if (spine == null || visual == null)
            throw new System.InvalidOperationException("Level139 楼层门缺少 ActorSpineCtrl 或手动门视觉组件。");
        wantsOpen = ownsPose = false;
        motion = null;
        // 不抢初始共享 StartDoor 出场演出；第一次玩家点击后才接管门姿态。
    }

    public void Toggle()
    {
        // 仅手动点击进入黑洞模式，初始出生和共享出门从不调用此入口。
        if (!visual.TryUseBlackInterior())
            return;
        wantsOpen = !wantsOpen;
        ownsPose = true;
        motion = spine.PlayAnimation(wantsOpen ? "door_open" : "dose_close", false, false);
        if (motion == null || motion.Animation == null)
        {
            wantsOpen = false;
            visual.UseSharedVisuals();
            Debug.LogError("[Level139] 楼层门动画不可用，不放行换层。");
            return;
        }
        AudioManager.PlaySound(wantsOpen ? "openDoor" : "closeDoor");
    }

    public void SetPaused(bool paused)
    {
        if (motion != null && spine != null && spine.AnimationState?.GetCurrent(0) == motion)
        {
            motion.TimeScale = paused ? 0f : 1f;
            if (!wantsOpen && motion.IsComplete)
                visual.UseSharedVisuals();
        }
    }

    public void Consume()
    {
        SetPaused(false);
        motion = null;
        wantsOpen = false;
    }

    public void ResetClosed()
    {
        Consume();
        if (spine != null && ownsPose)
            spine.PlayAnimation("door_idle", true, false);
        visual?.UseSharedVisuals();
        ownsPose = false;
    }
}

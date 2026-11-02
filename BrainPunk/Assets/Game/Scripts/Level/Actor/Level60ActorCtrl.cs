using UnityEngine;

public class Level60ActorCtrl : ActorCtrl
{
    private const string DreamIdleInAnimationName = "idle_random_in";

    private const string DreamIdleAnimationName = "idle_random_idle";
    private const string NormalIdleAnimationName = "idle";

    private bool isDreamIdle;

    public bool IsDreamIdle => isDreamIdle;

    public override void OnUpdate()
    {
        if (isDreamIdle)
        {
            UpdateDreamIdleState();
            return;
        }

        base.OnUpdate();
    }

    protected override void HandleAnimators()
    {
        if (isDreamIdle)
            return;

        base.HandleAnimators();
    }

    public void EnterDreamIdle()
    {
        if (IsDied || SpineCtrl == null)
            return;

        isDreamIdle = true;
        StopHorizontalMotion();

        var track = SpineCtrl?.PlayAnimation(DreamIdleInAnimationName, false, false);
        track.Complete += (tracy) =>
        {
            SpineCtrl?.PlayAnimation(DreamIdleAnimationName, true, false);
        };

    }

    public void ExitDreamIdle()
    {
        if (!isDreamIdle)
            return;

        isDreamIdle = false;

        if (!IsDied && SpineCtrl != null)
        {
            SpineCtrl.PlayAnimation(NormalIdleAnimationName, true, false);

            isDreamIdle = false;
            UpdateAnimation();
        }
    }

    private void UpdateDreamIdleState()
    {
        if (!IsReady)
            return;

        UpdateAirbornStatus();
        StopHorizontalMotion();
        HandleCollision();
    }

    private void StopHorizontalMotion()
    {
        SetMoveInput(0f);

        if (RB != null)
        {
            RB.linearVelocity = new Vector2(0f, RB.linearVelocityY);
        }
    }

    private void OnDisable()
    {
        isDreamIdle = false;
    }
}

using Bear.Logger;
using UnityEngine;

public class DoorEnemyCtrl : ActorCtrl, IDebuger
{
    private bool isStand = false;
    private bool isPressed = false;

    protected override void HandleAnimators()
    {
        if (isPressed)
        {
            SpineCtrl.PlayAnimation("pressed", true);
            return;
        }

        if (IsGround)
        {
            if (XInput != 0)
            {
                SpineCtrl.PlayAnimation("walk", true);
            }
            else
            {
                SpineCtrl.PlayAnimation(isStand ? "idle01" : "idle", true);
            }
        }
        else
        {
            if (RB.linearVelocityY > 0)
            {
                SpineCtrl.PlayAnimation("jump_up", false);
            }
            else
            {
                SpineCtrl.PlayAnimation("jump_down", false);
            }
        }
    }

    public void StandUp()
    {
        isStand = true;
    }

    public void PressDoor()
    {
        isPressed = true;
    }

    public void CancelPress()
    {
        isPressed = false;
    }
}

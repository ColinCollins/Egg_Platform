using Bear.Logger;
using Spine;
using UnityEngine;

public class Level21ActorCtrl : ActorCtrl, IDebuger
{
    private bool stopAnim = false;
    private bool isInNoise = false;

    public override void OnUpdate()
    {
        if (!IsReady)
            return;

        UpdateAirbornStatus();

        if (IsKnocked)
            return;

        HandleInput();
        if (isInNoise)
        {
            if (XInput > 0)
                SetMoveInput(0);
        }

        HandleFlip();
        HandleMovement();
        HandleCollision();
        HandleAnimators();
    }

    protected override void HandleAnimators()
    {
        if (stopAnim)
            return;

        // 死亡后不切动画
        if (IsDied)
            return;

        if (IsGround)
        {
            // this.Log("-----------" + XInput);
            if (XInput != 0)
            {
                SpineCtrl.PlayAnimation("walk", true);
            }
            else
            {
                if (isInNoise)
                {
                    SpineCtrl.PlayAnimation("cover_ears01".ToString(), true);
                }
                else
                {
                    SpineCtrl.PlayAnimation("idle".ToString(), true);
                }
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

    public void TriggerCoverEar()
    {
        isInNoise = true;
        PlayCoverEar();
    }

    private void PlayCoverEar()
    {
        stopAnim = true;
        var track = SpineCtrl.PlayAnimation("cover_ears", false);
        track.Complete += ResumePlayAnimHandle;
    }

    private void ResumePlayAnimHandle(TrackEntry entry)
    {
        stopAnim = false;
    }

    public void StopCoverEar()
    {
        isInNoise = false;
    }
}

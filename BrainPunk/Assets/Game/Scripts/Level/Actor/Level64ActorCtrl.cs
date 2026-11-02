using System;
using Bear.Logger;
using Spine;
using UnityEngine;

/// <summary>
/// 举东西动画表现，同时保存运输状态。
/// Actor 自己不执行 AI 决策，只提供状态读写接口。
/// </summary>
public class Level64ActorCtrl : ActorCtrl, IDebuger
{
    public enum TransportState
    {
        ToOutput,
        ToInput
    }

    private TransportState _transportState = TransportState.ToOutput;
    private bool isLift;

    public override void OnUpdate()
    {
        base.OnUpdate();
    }

    protected override void HandleAnimators()
    {
        if (IsDied || IsSit)
            return;

        if (IsDied || IsKicking || IsSit || RB == null)
            return;

        if (IsGround)
        {
            if (isLift)
            {
                if (XInput != 0)
                {
                    SpineCtrl.PlayAnimation("lift", true);
                }
                else
                {
                    SpineCtrl.PlayAnimation("lift01_idle", true);
                }

                return;
            }

            if (XInput != 0)
            {
                if (IsWallDetected || IsPushStuff)
                {
                    SpineCtrl.PlayAnimation(ActorSpineState.push.ToString(), true);
                }
                else
                {
                    SpineCtrl.PlayAnimation(ActorSpineState.walk.ToString(), true);
                }
            }
            else
            {
                SpineCtrl.PlayAnimation(ActorSpineState.idle.ToString(), true);
            }
        }
        else
        {
            if (isLift)
            {
                if (RB.linearVelocityY > 0)
                {
                    SpineCtrl.PlayAnimation("lift_jump_up", false);
                }
                else
                {
                    SpineCtrl.PlayAnimation("lift_jump_down", false);
                }

                return;
            }

            if (RB.linearVelocityY > 0)
            {
                SpineCtrl.PlayAnimation(ActorSpineState.jump_up.ToString(), false);
            }
            else
            {
                SpineCtrl.PlayAnimation(ActorSpineState.jump_down.ToString(), false);
            }
        }
    }

    /// <summary>
    /// 变为 lift 状态
    /// </summary>
    /// <param name="isLift"></param>
    public void SwitchLiftState(bool isLift)
    {
        this.isLift = isLift;
        SwitchTransportState(isLift);
    }

    public TransportState GetTransportState()
    {
        return _transportState;
    }

    public void SetTransportState(TransportState state)
    {
        _transportState = state;
    }

    public void SwitchTransportState(bool isLift)
    {
        _transportState = isLift
            ? TransportState.ToInput
            : TransportState.ToOutput;
    }

    public bool IsTransportToOutput()
    {
        return _transportState == TransportState.ToOutput;
    }

    public bool IsTransportToInput()
    {
        return _transportState == TransportState.ToInput;
    }
}

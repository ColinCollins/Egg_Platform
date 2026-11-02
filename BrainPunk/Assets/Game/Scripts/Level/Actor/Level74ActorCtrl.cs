using System;
using Bear.Logger;
using Game.Scripts.Common;
using UnityEngine;

public class Level74ActorCtrl : ActorCtrl, IDebuger
{
    public enum ToolState
    {
        None,
        Using_Eradicator,
        Using_Mow,
        Using_Hoe
    }

    private const string EradicatorAnimName = "weeding_car";
    private const string MowAnimName = "weeding_mowing";
    private const string HoeAnimName = "weeding_hoeing";

    private ToolState toolState = ToolState.None;
    private bool isExecutingTool;
    private Action executeComplete;

    public ToolState CurrentToolState => toolState;
    public bool IsExecutingTool => isExecutingTool;
    public bool CanStartLevel74Action => !IsDied && IsGround && !isExecutingTool;

    private MelenitasDev.SoundsGood.Sound eradicatorSound;

    public override void OnUpdate()
    {
        if (isExecutingTool)
        {
            UpdateExecutingToolState();
            return;
        }

        base.OnUpdate();
    }

    public void SetToolState(ToolState state)
    {
        if (isExecutingTool || toolState == state)
            return;

        toolState = state;
        UpdateAnimation();
        // StopSound();
        PlayToolSound();
    }

    public bool ExecuteCurrentTool(Action onComplete)
    {
        string animName = GetExecuteAnimationName(toolState);
        if (string.IsNullOrEmpty(animName) || !CanStartLevel74Action)
            return false;

        isExecutingTool = true;
        executeComplete = onComplete;
        SetMoveInput(0f);
        StopMoving();

        var entry = SpineCtrl != null ? SpineCtrl.PlayAnimation(animName, false, false) : null;
        if (entry == null)
        {
            this.LogWarning($"[Level74ActorCtrl] Animation missing or failed: {animName}");
            CompleteExecute();
            return true;
        }

        PlayToolSound();

        entry.Complete += _ => CompleteExecute();
        return true;
    }

    public void CancelLevel74Action()
    {
        isExecutingTool = false;
        executeComplete = null;

        if (!IsDied)
        {
            ResumeMoving();
            UpdateAnimation();
        }
    }

    protected override void JumpButton()
    {
        if (toolState == ToolState.Using_Eradicator || isExecutingTool)
            return;

        base.JumpButton();
    }

    protected override void HandleAnimators()
    {
        if (isExecutingTool)
            return;

        if (toolState == ToolState.Using_Eradicator && SpineCtrl != null && IsGround && !IsDied)
        {
            SpineCtrl.PlayAnimation(EradicatorAnimName, true, true);
            return;
        }

        base.HandleAnimators();
    }

    private void UpdateExecutingToolState()
    {
        UpdateAirbornStatus();
        SetMoveInput(0f);

        if (RB != null)
            RB.linearVelocity = Vector2.zero;

        HandleCollision();
    }

    private void CompleteExecute()
    {
        if (!isExecutingTool)
            return;

        isExecutingTool = false;
        Action callback = executeComplete;
        executeComplete = null;
        callback?.Invoke();
    }

    public void FinishLevel74Operation()
    {
        if (IsDied)
            return;

        ResumeMoving();
        UpdateAnimation();
    }

    private string GetExecuteAnimationName(ToolState state)
    {
        switch (state)
        {
            case ToolState.Using_Mow:
                return MowAnimName;
            case ToolState.Using_Hoe:
                return HoeAnimName;
            default:
                return string.Empty;
        }
    }

    private void OnDisable()
    {
        CancelLevel74Action();
        toolState = ToolState.None;

        StopSound();
    }

    private void PlayToolSound()
    {
        StopSound();

        switch (toolState)
        {
            case ToolState.Using_Mow:
                AudioManager.PlaySound("sickle");
                break;
            case ToolState.Using_Eradicator:
                eradicatorSound = AudioManager.PlaySound("eradicator", loop: true);
                break;
            case ToolState.Using_Hoe:
                AudioManager.PlaySound("level74dig");
                break;
            default:
                break;
        }
    }

    private void StopSound()
    {
        if (eradicatorSound != null)
            eradicatorSound.Stop();
    }
}

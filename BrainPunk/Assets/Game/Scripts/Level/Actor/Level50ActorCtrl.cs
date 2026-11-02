using System;
using Bear.Logger;
using Spine;
using UnityEngine;

/// <summary>
/// 飞向太空 2，火箭动画
/// </summary>
public class Level50ActorCtrl : ActorCtrl, IDebuger
{
    [SerializeField] private ActorSpineCtrl rocketSpineCtrl;
    private bool stopAnim = false;

    public override void OnUpdate()
    {
        if (!IsReady)
            return;

        UpdateAirbornStatus();

        if (IsKnocked)
            return;

        HandleInput();

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

        base.HandleAnimators();
    }

    public void SwitchRocket(bool isShow)
    {
        rocketSpineCtrl.gameObject.SetActive(isShow);
    }

    public void StartRocketFly()
    {
        stopAnim = true;
        rocketSpineCtrl.PlayAnimation("rocket01_fly", true);
        SpineCtrl.PlayAnimation("rocket_fly", true);
        
        this.Log("Rocket flying");
    }

    public void StopRocket()
    {
        stopAnim = false;
    }

    private void ResumePlayAnimHandle(TrackEntry entry)
    {
        stopAnim = false;
    }
}

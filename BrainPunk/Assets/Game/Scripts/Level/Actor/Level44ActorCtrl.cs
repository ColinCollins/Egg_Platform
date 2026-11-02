using System;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using MelenitasDev.SoundsGood;
using Spine;
using UnityEngine;
using AudioManager = Game.Scripts.Common.AudioManager;

/// <summary>
/// 称重。丢东西
/// </summary>
public class Level44ActorCtrl : ActorCtrl, IDebuger
{
    private bool stopAnim = false;
    private Sound findItems;

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


    public async UniTask PlayThrow()
    {
        stopAnim = true;
        SpineCtrl.PlayAnimation("touching", true);

        if (findItems == null || !findItems.Playing)
            findItems = AudioManager.PlaySound("findItems", loop: true);

        await UniTask.WaitForSeconds(1.5f);

        if (findItems != null)
        {
            findItems.Stop();
            findItems = null;
        }

        SpineCtrl.PlayAnimation("throwing", false);

        await UniTask.WaitForSeconds(.3f);
        AudioManager.PlaySound("throwItem");
        await UniTask.WaitForSeconds(.2f);

        this.Log("Rocket flying");

        stopAnim = false;
    }

    private void ResumePlayAnimHandle(TrackEntry entry)
    {
        stopAnim = false;
    }

    private void OnDestroy()
    {
        if (findItems != null)
        {
            findItems.Stop();
        }
    }
}

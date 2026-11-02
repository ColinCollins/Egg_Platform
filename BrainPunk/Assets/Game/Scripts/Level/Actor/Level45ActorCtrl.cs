using System;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Spine;
using UnityEngine;
using AudioManager = Game.Scripts.Common.AudioManager;

/// <summary>
/// 篮球互动
/// </summary>
public class Level45ActorCtrl : ActorCtrl, IDebuger
{
    private bool stopAnim = false;
    public override void OnUpdate()
    {
        base.OnUpdate();
    }

    protected override void HandleAnimators()
    {
        if (stopAnim)
            return;

        // 死亡后不切动画
        if (IsDied || IsSit)
            return;

        base.HandleAnimators();
    }

    public void PlayLift()
    {
        stopAnim = true;
        SpineCtrl.PlayAnimation("lift", false);
    }

    public async UniTask PlayThrow()
    {
        SpineCtrl.PlayAnimation("throwing", false);

        await UniTask.WaitForSeconds(.3f);
        AudioManager.PlaySound("throwItem");
        await UniTask.WaitForSeconds(.2f);

        stopAnim = false;
    }

    private void ResumePlayAnimHandle(TrackEntry entry)
    {
        stopAnim = false;
    }

}

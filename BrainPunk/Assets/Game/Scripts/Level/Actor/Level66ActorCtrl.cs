using System;
using Bear.Logger;
using Game.Scripts.Common;
using Spine;
using UnityEngine;

/// <summary>
/// 播放一个动画
/// </summary>
public class Level66ActorCtrl : ActorCtrl, IDebuger
{
    public override void OnUpdate()
    {
        base.OnUpdate();
    }

    public void PlayMagic()
    {
        var track = SpineCtrl.PlayAnimation("magic".ToString(), false);
        track.Complete += (track) =>
        {
            AudioManager.PlaySound("level66Magic");
            SpineCtrl.PlayAnimation("magic01".ToString(), false).Complete += (t) =>
            {
                SpineCtrl.PlayAnimation("idle".ToString(), false);
            };
        };
    }
}

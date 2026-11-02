using System;
using UnityEngine;

/// <summary>145 专用角色：托管 113 的持物/挥击和 044 的翻找动作，不修改共享 Actor。</summary>
public sealed class Level145ActorCtrl : ActorCtrl
{
    #region 动作所有权
    private bool actionPlaying;
    private int actionVersion;

    /// <summary>开始本关动作锁；也用于没有独立 Spine 动画的首次晃动，防止暂停恢复重新开放跳跃。</summary>
    public void BeginLevelAction()
    {
        actionPlaying = true;
        StopMoving();
    }

    /// <summary>由本关控制器在锁住操作后调用；动作期间阻止通用移动动画覆盖。</summary>
    public void PlayLevelAction(string animationName, bool loop, Action completed = null)
    {
        BeginLevelAction();
        int version = ++actionVersion;
        Spine.TrackEntry entry = SpineCtrl.PlayAnimation(animationName, loop, false);
        if (entry == null) { completed?.Invoke(); return; }
        if (!loop && entry != null)
            entry.Complete += _ => { if (version == actionVersion) completed?.Invoke(); };
    }

    /// <summary>仅本关动作完成时恢复通用动画；输入恢复由关卡控制器负责。</summary>
    public void FinishLevelAction()
    {
        actionVersion++;
        actionPlaying = false;
        if (!IsDied) UpdateAnimation();
    }

    protected override void HandleAnimators()
    {
        if (!actionPlaying || IsDied) base.HandleAnimators();
    }

    /// <summary>通用关卡恢复事件会 ResumeMoving；局部动作未结束时仍由本角色维持移动锁。</summary>
    public override void OnUpdate()
    {
        if (actionPlaying && !IsDied && CanMove) StopMoving();
        base.OnUpdate();
    }

    protected override void JumpButton()
    {
        if (!actionPlaying) base.JumpButton();
    }

    private void OnDisable()
    {
        actionVersion++;
        actionPlaying = false;
    }
    #endregion
}

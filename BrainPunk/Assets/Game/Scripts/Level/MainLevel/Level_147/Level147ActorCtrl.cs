using Spine;
using UnityEngine;

/// <summary>147 专用角色：跑向车、上下车、骑行和踢门期间由本关接管动作。</summary>
public sealed class Level147ActorCtrl : ActorCtrl
{
    #region 本关动作控制
    /// <summary>本角色已有的影子根节点；骑行时一并隐藏动态子影子，下车后恢复原投影。</summary>
    [SerializeField] private GameObject groundShadow;
    private bool scripted;
    private RigidbodyType2D originalBodyType;
    private float originalGravity;

    /// <summary>保持原角色朝向规则，避免将动画倒放误当作整车倒退。</summary>
    public void FaceRight(bool right) => SetActorFaceDirection(right);

    /// <summary>接管位置且保留玩家的碰撞体，使自动骑行仍能触发星星收集。</summary>
    public void BeginScriptedMotion()
    {
        if (scripted) return;
        originalBodyType = RB.bodyType;
        originalGravity = RB.gravityScale;
        SetMoveInput(0);
        StopMoving();
        SetSitState(true);
        RB.bodyType = RigidbodyType2D.Kinematic;
        RB.gravityScale = 0;
        scripted = true;
    }

    /// <summary>只在本关动作内使用现有 Spine 动画，不扩展共享 ActorCtrl。</summary>
    public TrackEntry PlayRaceAnimation(string name, bool loop = true)
    {
        BeginScriptedMotion();
        groundShadow.SetActive(name != "bike_ride");
        TrackEntry entry = SpineCtrl.PlayAnimation(name, loop);
        if (entry != null) entry.TimeScale = 1f;
        return entry;
    }

    /// <summary>按真实车速设置蹬踏频率；倒退时仍正向播放 bike_ride。</summary>
    public void SetPedalSpeed(float cyclesPerSecond)
    {
        TrackEntry entry = SpineCtrl.PlayAnimation("bike_ride", true);
        if (entry != null) entry.TimeScale = Mathf.Abs(cyclesPerSecond) * entry.Animation.Duration;
    }

    /// <summary>仅暂停本关接管的演出，不覆盖下车后原有行走或成功进门动画。</summary>
    public void PauseRaceAnimation(bool pause)
    {
        if (scripted && SpineCtrl.AnimationState != null)
            SpineCtrl.AnimationState.TimeScale = pause ? 0f : 1f;
    }

    /// <summary>下车后归还原刚体与移动操作；上车前缓存的移动输入不会继续生效。</summary>
    public void ResumeWalking()
    {
        groundShadow.SetActive(true);
        RB.bodyType = originalBodyType;
        RB.gravityScale = originalGravity;
        RB.linearVelocity = Vector2.zero;
        scripted = false;
        SetSitState(false);
        SetMoveInput(0);
        SpineCtrl.AnimationState.TimeScale = 1f;
        UpdateAnimation();
    }

    /// <summary>演出接管期间不执行原走跳更新；下车后恢复基类处理。</summary>
    public override void OnUpdate()
    {
        if (!scripted) base.OnUpdate();
    }

    protected override void HandleAnimators()
    {
        if (!scripted) base.HandleAnimators();
    }

    protected override void JumpButton()
    {
        if (!scripted) base.JumpButton();
    }
    #endregion
}

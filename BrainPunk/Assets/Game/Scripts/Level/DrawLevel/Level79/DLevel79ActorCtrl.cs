using Spine;
using UnityEngine;

/// <summary>
/// 第 79 关 Actor：保留本 prefab 原有陡坡限制，隔离 Level84 的两次足球蓄力与踢球动作。
/// 不修改共享 ActorCtrl 或已验收关卡的动画实现。
/// </summary>
[DisallowMultipleComponent]
public sealed class DLevel79ActorCtrl : ActorCtrl
{
    #region Inspector

    /// <summary>本关 Actor 可向上行走的最大坡度；沿用原 prefab 的 50 度阈值。</summary>
    [Header("坡度限制")]
    [SerializeField, Range(0f, 89f)] private float maxWalkableSlopeAngle = 50f;

    /// <summary>参与陡坡限制的层；本关显式配置 Draw 层，不改变普通地面行为。</summary>
    [SerializeField] private LayerMask slopeCheckLayers;

    #endregion

    #region Runtime State

    private const string ReadyAnimation = "football_shoot_ready";
    private const string ShootAnimation = "football_shoot";
    private TrackEntry _shotEntry;
    private int _shotPhase;
    private bool _entryCompleted;
    private readonly ContactPoint2D[] _slopeContacts = new ContactPoint2D[8];
    private ContactFilter2D _slopeContactFilter;
    private bool _isSlopeContactFilterReady;

    #endregion

    #region Public API

    /// <summary>是否持有本关足球动作；只供本关 Ctrl 判断完成，不代表胜负结果。</summary>
    public bool IsFootballShootPlaying => _shotEntry != null;

    /// <summary>设置本关角色朝向；重复设置同一方向不会再次翻转，运行时由本关 Ctrl 调用。</summary>
    public void SetFacingRight(bool isRight)
    {
        if (Application.isPlaying)
            SetActorFaceDirection(isRight);
    }

    /// <summary>
    /// 开始两次非循环 football_shoot_ready，再播放 football_shoot。
    /// 调用边界：角色存活、可移动且动作未开始；动画缺失时返回 false，不使用定时兜底踢球。
    /// </summary>
    public bool PlayFootballShoot()
    {
        var state = SpineCtrl != null ? SpineCtrl.AnimationState : null;
        if (!Application.isPlaying || !isActiveAndEnabled || IsDied || !CanMove ||
            _shotEntry != null || state == null ||
            state.Data.SkeletonData.FindAnimation(ReadyAnimation) == null ||
            state.Data.SkeletonData.FindAnimation(ShootAnimation) == null)
            return false;

        _shotPhase = 0;
        SetMoveInput(0f);
        StopMoving();
        PlayShotEntry(ReadyAnimation);
        return true;
    }

    /// <summary>
    /// 本关 Ctrl 每帧推进动作并同步暂停；只有开始踢球的这一帧返回 true，供 Ctrl 施力一次。
    /// Complete 回调只记录完成，阶段切换在未暂停的此入口执行，避免暂停期间推进或施力。
    /// </summary>
    public bool TickFootballShoot(bool paused)
    {
        if (!Application.isPlaying || _shotEntry == null)
            return false;
        _shotEntry.TimeScale = paused ? 0f : 1f;
        if (paused || !_entryCompleted)
            return false;

        DetachShotEntry();
        _shotPhase++;
        if (_shotPhase < 2)
            PlayShotEntry(ReadyAnimation);
        else if (_shotPhase == 2)
        {
            PlayShotEntry(ShootAnimation);
            return true;
        }
        else
            UpdateAnimation();
        return false;
    }

    /// <summary>
    /// 取消本关动作并解绑旧轨道回调，用于 Reset、结算、禁用和动画中断。
    /// 只清除自身仍占用的轨道；不恢复移动门禁，Reset 由 Ctrl 显式 ResumeMoving。
    /// </summary>
    public void CancelFootballShoot()
    {
        if (_shotEntry == null)
            return;
        TrackEntry entry = _shotEntry;
        DetachShotEntry();
        if (SpineCtrl != null && SpineCtrl.AnimationState?.GetCurrent(0) == entry)
            SpineCtrl.AnimationState.ClearTrack(0);
    }

    #endregion

    #region Actor Lifecycle

    /// <summary>足球动作期间保留专用动画与接触检测；其余时间沿用共享行走及原陡坡限制。</summary>
    public override void OnUpdate()
    {
        if (_shotEntry != null)
        {
            if (RB != null)
                RB.linearVelocity = new Vector2(0f, RB.linearVelocity.y);
            HandleCollision();
            return;
        }
        if (!ShouldBlockUphillMovement())
        {
            base.OnUpdate();
            return;
        }

        float originalMoveSpeed = MoveSpeed;
        MoveSpeed = 0f;
        try { base.OnUpdate(); }
        finally { MoveSpeed = originalMoveSpeed; }
    }

    /// <summary>阻止共享待机、行走动画覆盖正在播放的本关蓄力或踢球动作。</summary>
    protected override void HandleAnimators()
    {
        if (_shotEntry == null)
            base.HandleAnimators();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            CancelFootballShoot();
    }

    #endregion

    #region Animation Helpers

    private void PlayShotEntry(string animationName)
    {
        _entryCompleted = false;
        _shotEntry = SpineCtrl.PlayAnimation(animationName, false, false);
        _shotEntry.Complete += OnShotEntryComplete;
        _shotEntry.Interrupt += OnShotEntryInterrupted;
        _shotEntry.End += OnShotEntryInterrupted;
    }

    private void OnShotEntryComplete(TrackEntry entry)
    {
        if (ReferenceEquals(entry, _shotEntry))
            _entryCompleted = true;
    }

    private void OnShotEntryInterrupted(TrackEntry entry)
    {
        if (ReferenceEquals(entry, _shotEntry))
            CancelFootballShoot();
    }

    private void DetachShotEntry()
    {
        _shotEntry.Complete -= OnShotEntryComplete;
        _shotEntry.Interrupt -= OnShotEntryInterrupted;
        _shotEntry.End -= OnShotEntryInterrupted;
        _shotEntry = null;
        _entryCompleted = false;
    }

    #endregion

    #region Slope Detection

    private bool ShouldBlockUphillMovement()
    {
        if (!Application.isPlaying || RB == null || Mathf.Approximately(XInput, 0f))
            return false;
        if (!_isSlopeContactFilterReady)
        {
            _slopeContactFilter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = slopeCheckLayers
            };
            _isSlopeContactFilterReady = true;
        }

        int count = RB.GetContacts(_slopeContactFilter, _slopeContacts);
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = _slopeContacts[i];
            Vector2 normal = contact.normal;
            bool ownsCollider = contact.collider != null && contact.collider.attachedRigidbody == RB;
            bool ownsOtherCollider = contact.otherCollider != null && contact.otherCollider.attachedRigidbody == RB;
            if (ownsCollider && !ownsOtherCollider)
                normal = -normal;
            if (normal.y > 0f && XInput * -normal.x > 0f &&
                Vector2.Angle(normal, Vector2.up) > maxWalkableSlopeAngle)
                return true;
        }
        return false;
    }

    #endregion
}

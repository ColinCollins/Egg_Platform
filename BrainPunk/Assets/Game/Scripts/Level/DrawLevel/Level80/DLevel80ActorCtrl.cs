using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// 第 80 关角色动作：enemy 蓄力踢球，Actor 用真实刚体跳跃扑救，落地保留水平位置。
/// 本关 Ctrl 负责施力与胜负，本类不修改共享 Actor 动画或足球物理。
/// </summary>
[DisallowMultipleComponent]
public sealed class DLevel80ActorCtrl : ActorCtrl
{
    #region Inspector

    /// <summary>
    /// 玩家扑救使用的 Spine 动画组件，必须显式绑定到 SpineCtrl 使用的同一组件；enemy 可留空。
    /// 开场可将其 timeScale 设为 0 定格；跳起恢复为 1，落地/取消恢复正常播放倍率，避免 idle 再冻结。
    /// </summary>
    [SerializeField] private SkeletonAnimation saveSkeletonAnimation;

    #endregion

    #region Runtime State

    private enum ActionKind { None, FootballShoot, FakeSave }
    private ActionKind _action;
    private TrackEntry _entry;
    private bool _completed;
    private int _shootPhase;
    private bool _saveDescending;
    private float _initialSkeletonTimeScale;
    private bool _savePaused;
    private Vector2 _savePausedVelocity;
    private RigidbodyConstraints2D _saveInitialConstraints;

    #endregion

    #region Public API

    /// <summary>
    /// 刷新共享接触检测及空中/落地状态；由本关 Ctrl 在首笔前和自有动作期间调用。
    /// 仅运行时且刚体初始化后有效；不读取移动输入，不切换动画，保留开场定格和扑救轨道。
    /// </summary>
    public void RefreshContactState()
    {
        if (!Application.isPlaying || RB == null)
            return;
        HandleCollision();
        UpdateAirbornStatus();
    }

    /// <summary>
    /// 播放两次 football_shoot_ready 后再踢球，沿用当前足球动作顺序。
    /// 调用边界：仅运行时、角色存活且没有自有动作；缺动画时返回 false，不使用定时兜底。
    /// </summary>
    public bool PlayFootballShoot()
    {
        if (!CanStartAction("football_shoot_ready") ||
            SpineCtrl.AnimationState.Data.SkeletonData.FindAnimation("football_shoot") == null)
            return false;
        _action = ActionKind.FootballShoot;
        _shootPhase = 0;
        SetMoveInput(0f);
        StopMoving();
        PlayEntry("football_shoot_ready");
        return true;
    }

    /// <summary>
    /// 以真实刚体跳跃播放 jump_up / jump_down；offset.y 是目标跳高，offset.x 是同高度落地时的水平位移。
    /// 按当前向下重力计算初速度，实际落点受地形/碰撞影响；不拉回原位，足球仍沿用排除 Actor 层的碰撞配置。
    /// 调用边界：本关踢球开始时调用一次；需 Dynamic 刚体、正跳高/向下重力、Spine 引用及两段跳跃动画。
    /// 落地由持续接地检测判定，恢复 idle；开场倍率为 0 时后续改为 1，其它正倍率恢复原值。
    /// </summary>
    public bool PlayFakeSave(Vector2 offset)
    {
        if (RB == null || RB.bodyType != RigidbodyType2D.Dynamic || offset.y <= 0f ||
            saveSkeletonAnimation == null || !CanStartAction("jump_up") ||
            SpineCtrl.AnimationState.Data.SkeletonData.FindAnimation("jump_down") == null)
            return false;
        float gravity = -Physics2D.gravity.y * RB.gravityScale;
        if (gravity <= 0f)
            return false;
        float jumpSpeed = Mathf.Sqrt(2f * gravity * offset.y);
        float flightTime = 2f * jumpSpeed / gravity;
        _action = ActionKind.FakeSave;
        _saveDescending = false;
        _initialSkeletonTimeScale = saveSkeletonAnimation.timeScale;
        saveSkeletonAnimation.timeScale = 1f;
        SetMoveInput(0f);
        StopMoving();
        RB.linearVelocity = new Vector2(offset.x / flightTime, jumpSpeed);
        PlayEntry("jump_up");
        return true;
    }

    /// <summary>
    /// 由本关 Ctrl 每帧推进，暂停时冻结自有 Spine 轨道；只在踢球开始的这一帧返回 true。
    /// 踢球按 Complete 推进；扑救按真实速度切下降、接地结束，未暂停时才消费状态。
    /// </summary>
    public bool TickAction(bool paused)
    {
        if (!Application.isPlaying || _entry == null)
            return false;
        if (_action == ActionKind.FakeSave)
            SetSavePaused(paused);
        _entry.TimeScale = paused ? 0f : 1f;
        if (paused)
            return false;

        if (_action == ActionKind.FakeSave)
        {
            if (!_saveDescending && RB.linearVelocity.y <= 0f)
            {
                DetachEntry();
                _saveDescending = true;
                PlayEntry("jump_down");
            }
            if (_saveDescending && IsGround && RB.linearVelocity.y <= 0f)
            {
                DetachEntry();
                RestoreSaveClock();
                _action = ActionKind.None;
                RB.linearVelocityX = 0f;
                ResumeMoving();
                UpdateAnimation();
            }
            return false;
        }
        if (!_completed)
            return false;

        DetachEntry();
        if (_action == ActionKind.FootballShoot && ++_shootPhase <= 2)
        {
            PlayEntry(_shootPhase == 1 ? "football_shoot_ready" : "football_shoot");
            return _shootPhase == 2;
        }
        _action = ActionKind.None;
        UpdateAnimation();
        return false;
    }

    /// <summary>
    /// 取消本关动作并解绑回调；跳跃只停止自有水平速度、恢复正常 Spine 播放，不拉回位置或清外部动画。
    /// 用于本关 Reset、结算、禁用及外部动画中断；Reset 的刚体复位由本关 Ctrl 负责。
    /// </summary>
    public void CancelAction()
    {
        if (_entry == null)
            return;
        TrackEntry oldEntry = _entry;
        DetachEntry();
        if (_action == ActionKind.FakeSave)
        {
            ReleaseSavePause(false);
            RestoreSaveClock();
            if (RB != null)
                RB.linearVelocityX = 0f;
        }
        _action = ActionKind.None;
        if (SpineCtrl != null && SpineCtrl.AnimationState?.GetCurrent(0) == oldEntry)
            SpineCtrl.AnimationState.ClearTrack(0);
    }

    #endregion

    #region Actor Lifecycle

    /// <summary>动作期间保留本关动画和接触检测，其余时间沿用共享角色更新。</summary>
    public override void OnUpdate()
    {
        if (_entry == null)
            base.OnUpdate();
        else
            RefreshContactState();
    }

    /// <summary>保护本关蓄力、踢球和 jump，防止共享待机动画提前覆盖。</summary>
    protected override void HandleAnimators()
    {
        if (_entry == null)
            base.HandleAnimators();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            CancelAction();
    }

    #endregion

    #region Local Helpers

    private bool CanStartAction(string animationName) => Application.isPlaying &&
        isActiveAndEnabled && !IsDied && _entry == null && SpineCtrl != null &&
        SpineCtrl.AnimationState != null &&
        SpineCtrl.AnimationState.Data.SkeletonData.FindAnimation(animationName) != null;

    private void PlayEntry(string animationName)
    {
        _completed = false;
        _entry = SpineCtrl.PlayAnimation(animationName, false, false);
        _entry.Complete += OnComplete;
        _entry.Interrupt += OnInterrupted;
        _entry.End += OnInterrupted;
    }

    private void OnComplete(TrackEntry entry)
    {
        if (ReferenceEquals(entry, _entry))
            _completed = true;
    }

    private void OnInterrupted(TrackEntry entry)
    {
        if (ReferenceEquals(entry, _entry))
            CancelAction();
    }

    private void RestoreSaveClock()
    {
        if (saveSkeletonAnimation != null)
            saveSkeletonAnimation.timeScale = _initialSkeletonTimeScale > 0f ? _initialSkeletonTimeScale : 1f;
    }

    // 共享暂停不修改全局 timeScale；本关只冻结自有跳跃刚体，保持 Collider 和接地查询可用。
    private void SetSavePaused(bool paused)
    {
        if (!paused)
        {
            ReleaseSavePause(true);
            return;
        }
        if (_savePaused)
            return;
        _savePaused = true;
        _savePausedVelocity = RB.linearVelocity;
        _saveInitialConstraints = RB.constraints;
        RB.constraints = RigidbodyConstraints2D.FreezeAll;
        RB.linearVelocity = Vector2.zero;
    }

    private void ReleaseSavePause(bool restoreVelocity)
    {
        if (!_savePaused)
            return;
        _savePaused = false;
        if (RB == null)
            return;
        RB.constraints = _saveInitialConstraints;
        if (restoreVelocity)
            RB.linearVelocity = _savePausedVelocity;
    }

    private void DetachEntry()
    {
        _entry.Complete -= OnComplete;
        _entry.Interrupt -= OnInterrupted;
        _entry.End -= OnInterrupted;
        _entry = null;
        _completed = false;
    }

    #endregion
}

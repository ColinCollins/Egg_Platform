using Cysharp.Threading.Tasks;
using Spine;
using UnityEngine;

/// <summary>
/// Level081 专用角色控制器：在水果玩法期间独占剑术动画，防止通用移动动画覆盖。
/// </summary>
public sealed class Level81ActorCtrl : ActorCtrl
{
    #region Constants

    private const string SwordIdleAnimation = "sword_idle";

    private static readonly string[] SwingAnimations =
    {
        "sword_swing",
        "sword_swing01",
        "sword_swing02"
    };

    #endregion

    #region Inspector Tuning

    [Tooltip("挥刀播完后保持最后一帧的时间；新手势可以立即打断该等待。")]
    [SerializeField, Min(0f)] private float swingHoldDuration = 1.5f;

    #endregion

    #region Runtime State

    private TrackEntry activeSwingEntry;
    private int lastSwingIndex = -1;
    private int animationVersion;
    private float animationStateTimeScaleBeforePause = 1f;
    private bool swordModeActive;
    private bool swordAnimationPaused;
    private bool hasSwordModeConstraints;
    private RigidbodyConstraints2D swordModeOriginalConstraints;

    #endregion

    #region ActorCtrl Overrides

    public override void OnUpdate()
    {
        if (swordModeActive)
            return;

        base.OnUpdate();
    }

    protected override void HandleAnimators()
    {
        if (swordModeActive)
            return;

        base.HandleAnimators();
    }

    #endregion

    #region Level081 API

    /// <summary>
    /// 锁住通用移动并立即切到无混合的 sword_idle。
    /// </summary>
    public bool EnterSwordMode()
    {
        SetSwordAnimationPaused(false);
        swordModeActive = true;
        animationVersion++;
        DetachActiveSwing();
        SetMoveInput(0f);
        StopMoving();
        LockHorizontalPosition();
        return PlaySwordIdle();
    }

    /// <summary>
    /// 播放一次不与上次重复的挥剑动画，完成后无混合返回 sword_idle。
    /// </summary>
    public bool PlayRandomSwordSwing()
    {
        if (!swordModeActive || SpineCtrl == null || SwingAnimations.Length == 0)
            return false;

        int selectedIndex = SelectNextSwingIndex();
        lastSwingIndex = selectedIndex;
        animationVersion++;

        DetachActiveSwing();
        TrackEntry entry = SpineCtrl.PlayAnimation(
            SwingAnimations[selectedIndex],
            false,
            false);
        if (entry == null)
            return PlaySwordIdle();

        entry.MixDuration = 0f;
        activeSwingEntry = entry;
        activeSwingEntry.Complete += HandleSwingComplete;
        return true;
    }

    /// <summary>
    /// 退出剑术状态并恢复通用移动动画；仅在本关出口已开放时调用。
    /// </summary>
    public void ExitSwordMode()
    {
        SetSwordAnimationPaused(false);
        swordModeActive = false;
        animationVersion++;
        DetachActiveSwing();
        RestoreHorizontalPosition();
        SetMoveInput(0f);
        ResumeMoving();
        UpdateAnimation();
    }

    /// <summary>
    /// 炸弹失败前取消挥剑回调，死亡动画随后由 ActorCtrl.Die 独占。
    /// </summary>
    public void PrepareForExplosion()
    {
        swordModeActive = true;
        animationVersion++;
        DetachActiveSwing();
        RestoreHorizontalPosition();
        SetMoveInput(0f);
        StopMoving();
    }

    /// <summary>
    /// 仅冻结本关剑术表现；物理和游戏状态暂停仍由 Level81Ctrl 与共享 PlayCtrl
    /// 负责，避免本关子类越权修改公共暂停流程。
    /// </summary>
    public void SetSwordAnimationPaused(bool paused)
    {
        Spine.AnimationState animationState = SpineCtrl != null
            ? SpineCtrl.AnimationState
            : null;
        if (paused)
        {
            if (swordAnimationPaused || !swordModeActive || animationState == null)
                return;

            animationStateTimeScaleBeforePause = animationState.TimeScale;
            animationState.TimeScale = 0f;
            swordAnimationPaused = true;
            return;
        }

        if (!swordAnimationPaused)
            return;

        if (animationState != null)
            animationState.TimeScale = animationStateTimeScaleBeforePause;
        animationStateTimeScaleBeforePause = 1f;
        swordAnimationPaused = false;
    }

    #endregion

    #region Animation Helpers

    private int SelectNextSwingIndex()
    {
        if (SwingAnimations.Length <= 1)
            return 0;

        if (lastSwingIndex < 0 || lastSwingIndex >= SwingAnimations.Length)
            return Random.Range(0, SwingAnimations.Length);

        int selectedIndex = Random.Range(0, SwingAnimations.Length - 1);
        if (selectedIndex >= lastSwingIndex)
            selectedIndex++;

        return selectedIndex;
    }

    private bool PlaySwordIdle()
    {
        if (SpineCtrl == null)
            return false;

        TrackEntry idleEntry = SpineCtrl.PlayAnimation(
            SwordIdleAnimation,
            true,
            false);
        if (idleEntry == null)
            return false;

        idleEntry.MixDuration = 0f;
        return true;
    }

    private void DetachActiveSwing()
    {
        if (activeSwingEntry == null)
            return;

        activeSwingEntry.Complete -= HandleSwingComplete;
        activeSwingEntry = null;
    }

    private void HandleSwingComplete(TrackEntry completedEntry)
    {
        if (!ReferenceEquals(activeSwingEntry, completedEntry))
            return;

        DetachActiveSwing();
        int completedVersion = animationVersion;
        HoldSwingPoseThenIdleAsync(completedVersion).Forget();
    }

    private async UniTaskVoid HoldSwingPoseThenIdleAsync(int completedVersion)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, swingHoldDuration);
        while (elapsed < duration)
        {
            await UniTask.Yield(
                PlayerLoopTiming.Update,
                this.GetCancellationTokenOnDestroy());
            if (!swordModeActive || completedVersion != animationVersion)
                return;

            if (!swordAnimationPaused)
                elapsed += Time.unscaledDeltaTime;
        }

        if (swordModeActive && completedVersion == animationVersion)
            PlaySwordIdle();
    }

    private void LockHorizontalPosition()
    {
        Rigidbody2D body = RB;
        if (body == null)
            return;

        if (!hasSwordModeConstraints)
        {
            swordModeOriginalConstraints = body.constraints;
            hasSwordModeConstraints = true;
        }

        body.linearVelocity = new Vector2(0f, body.linearVelocityY);
        body.constraints = swordModeOriginalConstraints | RigidbodyConstraints2D.FreezePositionX;
    }

    private void RestoreHorizontalPosition()
    {
        if (!hasSwordModeConstraints)
            return;

        if (RB != null)
            RB.constraints = swordModeOriginalConstraints;
        hasSwordModeConstraints = false;
    }

    #endregion

    #region Lifecycle

    private void OnDisable()
    {
        SetSwordAnimationPaused(false);
        swordModeActive = false;
        animationVersion++;
        DetachActiveSwing();
        RestoreHorizontalPosition();
    }

    #endregion
}

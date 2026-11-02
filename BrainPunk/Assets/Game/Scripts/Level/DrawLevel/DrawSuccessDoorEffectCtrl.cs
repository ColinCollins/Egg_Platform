using System;
using System.Threading;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Draw 标准成功门的局部表现控制器。
/// 负责开关门 Spine、音效、ribbon 和胜利彩纸时序，不持有关卡结算、Actor 或 HUD 职责。
/// </summary>
[DisallowMultipleComponent]
public sealed class DrawSuccessDoorEffectCtrl : MonoBehaviour, IDebuger
{
    #region Constants

    private const float RibbonHoldSeconds = 1.6f;
    private const float CloseDelaySeconds = 0.2f;
    private const float CloseSoundDelaySeconds = 0.25f;
    private const float ConfettiSettleSeconds = 0.8f;
    private const string OpenAnimationName = "door_open";
    private const string CloseAnimationName = "dose_close";
    private const string OpenSoundName = "openDoor";
    private const string CloseSoundName = "closeDoor";

    #endregion

    #region Inspector References

    /// <summary>
    /// 当前标准成功门的 Spine 控制器。
    /// 调用边界：只绑定本组件同一成功门根节点下的 door，不得绑定其它关卡或 effect/ribbon Spine。
    /// </summary>
    [SerializeField] private ActorSpineCtrl doorSpine;

    /// <summary>
    /// 开门期间显示的 ribbon 根对象。
    /// 调用边界：只控制当前成功门的局部视觉对象；缺失时保留门动画和音效并记录错误。
    /// </summary>
    [SerializeField] private GameObject ribbon;

    /// <summary>
    /// 关门音效触发时播放的 ConfettiBlastRainbow 根粒子。
    /// 调用边界：只绑定当前成功门下的彩纸根 ParticleSystem；Reset 时会连同子粒子停止并清空。
    /// </summary>
    [SerializeField] private ParticleSystem confettiBlastRainbow;

    #endregion

    #region Runtime State

    private CancellationTokenSource _playCancellationSource;

    #endregion

    #region Public API

    /// <summary>
    /// 播放一次 Draw 成功门完整表现：开门与 ribbon、关门动画、开关门音效及胜利彩纸。
    /// 调用边界：只由持有当前 DLevel 结果锁的 DrawLevelCtrl 调用；取消或禁用时不会触发关卡结算。
    /// </summary>
    /// <param name="cancellationToken">当前 DLevel 销毁或成功流程取消时使用的令牌。</param>
    public async UniTask PlayAsync(CancellationToken cancellationToken)
    {
        CancelActiveSequence();
        var playCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            this.GetCancellationTokenOnDestroy());
        _playCancellationSource = playCancellationSource;
        CancellationToken playToken = playCancellationSource.Token;

        try
        {
            Spine.TrackEntry openTrackEntry = TryPlayDoorAnimation(OpenAnimationName);
            if (openTrackEntry?.Animation == null)
                return;

            AudioManager.PlaySound(OpenSoundName);
            SetRibbonActive(true);

            await UniTask.WaitForSeconds(
                Mathf.Max(RibbonHoldSeconds, openTrackEntry.Animation.Duration),
                cancellationToken: playToken);

            SetRibbonActive(false);
            await UniTask.WaitForSeconds(CloseDelaySeconds, cancellationToken: playToken);

            Spine.TrackEntry closeTrackEntry = TryPlayDoorAnimation(CloseAnimationName);
            if (closeTrackEntry?.Animation == null)
                return;

            await UniTask.WaitForSeconds(CloseSoundDelaySeconds, cancellationToken: playToken);
            AudioManager.PlaySound(CloseSoundName);
            PlayConfetti();

            float remainingCloseSeconds = Mathf.Max(
                0f,
                closeTrackEntry.Animation.Duration - CloseSoundDelaySeconds);
            await UniTask.WhenAll(
                UniTask.WaitForSeconds(
                    remainingCloseSeconds,
                    cancellationToken: playToken),
                UniTask.WaitForSeconds(
                    ConfettiSettleSeconds,
                    cancellationToken: playToken));
        }
        catch (OperationCanceledException) when (playCancellationSource.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_playCancellationSource, playCancellationSource))
                _playCancellationSource = null;

            playCancellationSource.Dispose();
        }
    }

    /// <summary>
    /// 取消仍在播放的成功门时序，隐藏 ribbon，并停止清空胜利彩纸。
    /// 调用边界：由 Draw 尝试重置或本组件禁用时调用；不主动播放关门动画或修改关卡结果。
    /// </summary>
    public void ResetVisuals()
    {
        CancelActiveSequence();
        SetRibbonActive(false);
        StopConfetti();
    }

    #endregion

    #region Internal Helpers

    private Spine.TrackEntry TryPlayDoorAnimation(string animationName)
    {
        if (doorSpine == null)
        {
            this.LogError("[DrawSuccessDoorEffectCtrl] doorSpine 未绑定，跳过成功门表现。");
            return null;
        }

        try
        {
            Spine.TrackEntry trackEntry = doorSpine.PlayAnimation(animationName, false, false);
            if (trackEntry?.Animation == null)
            {
                this.LogError(
                    $"[DrawSuccessDoorEffectCtrl] 成功门无法播放动画：{animationName}。");
            }

            return trackEntry;
        }
        catch (ArgumentException exception)
        {
            this.LogError(
                $"[DrawSuccessDoorEffectCtrl] 成功门动画配置错误：{animationName}，" +
                $"error:{exception.Message}");
            return null;
        }
    }

    private void SetRibbonActive(bool active)
    {
        if (ribbon != null)
        {
            ribbon.SetActive(active);
            return;
        }

        if (active)
            this.LogError("[DrawSuccessDoorEffectCtrl] ribbon 未绑定，跳过 ribbon 表现。");
    }

    private void PlayConfetti()
    {
        if (confettiBlastRainbow == null)
        {
            this.LogError(
                "[DrawSuccessDoorEffectCtrl] confettiBlastRainbow 未绑定，跳过胜利彩纸表现。");
            return;
        }

        confettiBlastRainbow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        confettiBlastRainbow.Play(true);
    }

    private void StopConfetti()
    {
        if (confettiBlastRainbow != null)
        {
            confettiBlastRainbow.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void CancelActiveSequence()
    {
        CancellationTokenSource cancellationSource = _playCancellationSource;
        _playCancellationSource = null;
        cancellationSource?.Cancel();
    }

    #endregion

    #region Unity Lifecycle

    private void OnDisable()
    {
        ResetVisuals();
    }

    #endregion
}

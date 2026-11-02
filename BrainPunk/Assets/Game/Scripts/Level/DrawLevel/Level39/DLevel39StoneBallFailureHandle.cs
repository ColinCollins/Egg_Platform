using Cysharp.Threading.Tasks;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// DLevel_039 巨石命中指定球体后的失败处理器。
/// 只消费本关 Stone 的真实 Collision2D，在接触点播放 DLevel_003 同款特效后提交失败。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 39 Stone Ball Failure Handle")]
public sealed class DLevel39StoneBallFailureHandle : MonoBehaviour,
    IDrawStrokeCompletedHandler
{
    #region Inspector References

    /// <summary>
    /// 当前 DLevel_039 根节点的结果控制器；仅用于提交本次尝试失败。
    /// </summary>
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 本关猫夹装置内的目标球刚体；碰撞时按实例精确匹配，避免误伤其它 Actor 层物体。
    /// </summary>
    [SerializeField] private Rigidbody2D targetBallRigidbody;

    /// <summary>
    /// DLevel_039 内复用的 DLevel_003 Knock_Explosion 根粒子实例。
    /// </summary>
    [SerializeField] private ParticleSystem knockEffect;

    #endregion

    #region Settings

    /// <summary>
    /// 命中特效开始后到提交关卡失败之间的等待时间。
    /// </summary>
    [Header("失败表现")]
    [SerializeField, Min(0f)] private float failDelay = 0.5f;

    #endregion

    #region Runtime State

    private int _attemptVersion;
    private bool _isArmed;
    private bool _isFailing;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponentInParent<DrawLevelCtrl>();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        ResetFailureState();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!Application.isPlaying ||
            !_isArmed ||
            _isFailing ||
            collision == null ||
            targetBallRigidbody == null ||
            drawLevelCtrl == null ||
            drawLevelCtrl.IsOutcomeLocked)
        {
            return;
        }

        Collider2D ballCollider = collision.collider;
        if (ballCollider == null ||
            ballCollider.attachedRigidbody != targetBallRigidbody)
        {
            return;
        }

        _isFailing = true;
        int version = ++_attemptVersion;

        PlayKnock(GetImpactPosition(collision, ballCollider));
        AudioManager.PlaySound("trap");
        TriggerFailureAsync(version).Forget();
    }

    #endregion

    #region Draw Level Handlers

    /// <summary>第一笔有效画线完成后才允许巨石碰球触发失败。</summary>
    void IDrawStrokeCompletedHandler.OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (Application.isPlaying && result.IsFirstStroke)
            _isArmed = true;
    }

    /// <summary>清线或尝试重置时取消旧的延迟失败并清空命中特效。</summary>
    void IDrawStrokeCompletedHandler.OnDrawAttemptReset()
    {
        if (!Application.isPlaying)
            return;

        ResetFailureState();
    }

    #endregion

    #region Failure Flow

    private Vector3 GetImpactPosition(
        Collision2D collision,
        Collider2D ballCollider)
    {
        Vector2 impactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : ballCollider.ClosestPoint(transform.position);
        float effectDepth = knockEffect != null
            ? knockEffect.transform.position.z
            : transform.position.z;
        return new Vector3(impactPoint.x, impactPoint.y, effectDepth);
    }

    private void PlayKnock(Vector3 impactPosition)
    {
        if (knockEffect == null)
            return;

        knockEffect.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
        knockEffect.transform.position = impactPosition;
        knockEffect.Play(true);
    }

    private async UniTaskVoid TriggerFailureAsync(int version)
    {
        bool isCanceled = await UniTask.WaitForSeconds(
                Mathf.Max(0f, failDelay),
                cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        if (isCanceled ||
            this == null ||
            version != _attemptVersion ||
            !_isArmed ||
            !_isFailing)
        {
            return;
        }

        drawLevelCtrl.TryFailAndReset(
            nameof(DLevel39StoneBallFailureHandle));
    }

    private void ResetFailureState()
    {
        _attemptVersion++;
        _isArmed = false;
        _isFailing = false;

        if (knockEffect != null)
        {
            knockEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    #endregion
}

using Bear.Logger;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// IWanner 弹簧触发控制器：指定 Tag 的 Collider2D 进入后，播放弹簧局部位移动画并向其刚体施加指定方向的冲量。
/// </summary>
[DisallowMultipleComponent]
public sealed class IWannerGame_SpingTriggerCtrl : MonoBehaviour, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：控制当前组件在一次启用周期内是否允许重复响应有效触发。
    /// 调用边界：关闭时只接受第一个符合 Tag 的 Collider2D；重新启用组件后可再次触发。
    /// 注意事项：默认允许多次触发；重复触发会重启弹簧动画并再次施加冲量。
    /// </summary>
    [Header("Trigger Settings")]
    [Tooltip("是否允许多次触发；关闭后，每次启用期间只响应第一次有效触发")]
    [SerializeField] private bool canTriggerMultipleTimes = true;

    /// <summary>
    /// 用途：限制允许触发弹簧的 Collider2D Tag。
    /// 调用边界：只比较进入 Trigger 的 Collider2D 所在 GameObject。
    /// 注意事项：默认 Player；配置为空时不接受任何触发。
    /// </summary>
    [Tooltip("允许触发弹簧的 Collider2D Tag，默认 Player")]
    [SerializeField] private string triggerTag = "Player";

    /// <summary>
    /// 用途：指定播放局部位移动画的弹簧对象。
    /// 调用边界：运行时启用组件时缓存其原始 localPosition；为空时使用当前组件所在 Transform。
    /// 注意事项：动画结束或组件禁用时会返回缓存的原始局部位置。
    /// </summary>
    [Header("Spring Movement")]
    [Tooltip("播放位移动画的弹簧对象；留空时使用当前对象")]
    [SerializeField] private Transform spring;

    /// <summary>
    /// 用途：配置弹簧相对原始局部位置的移动偏移。
    /// 调用边界：只使用 X/Y 分量，Z 保持原始值。
    /// 注意事项：偏移不累计，每次触发都以缓存的原始局部位置为基准。
    /// </summary>
    [Tooltip("弹簧相对原始 localPosition 的移动偏移")]
    [SerializeField] private Vector2 offset = new(0f, -0.2f);

    /// <summary>
    /// 用途：配置弹簧从当前位置快速移动到原始位置加 offset 的时间。
    /// 调用边界：每次有效触发创建的 DOTween 动画读取该值。
    /// 注意事项：单位为秒；运行时按非负值处理。
    /// </summary>
    [Tooltip("弹簧移动到偏移位置的时间（秒）")]
    [Min(0f)]
    [SerializeField] private float moveDuration = 0.08f;

    /// <summary>
    /// 用途：配置弹簧从偏移位置返回原始局部位置的时间。
    /// 调用边界：紧接移动阶段执行，不额外停留。
    /// 注意事项：单位为秒；运行时按非负值处理。
    /// </summary>
    [Tooltip("弹簧返回原始位置的时间（秒）")]
    [Min(0f)]
    [SerializeField] private float returnDuration = 0.12f;

    /// <summary>
    /// 用途：配置有效触发时施加给进入者 Rigidbody2D 的世界空间受力方向。
    /// 调用边界：运行时会归一化后与 force 相乘，不改变 force 表示的冲量大小。
    /// 注意事项：默认向上以保持既有行为；配置为零向量时同样安全回退为向上。
    /// </summary>
    [Header("Actor Force")]
    [Tooltip("世界空间受力方向；默认向上，运行时会归一化，零向量回退为向上")]
    [SerializeField] private Vector2 forceDirection = Vector2.up;

    /// <summary>
    /// 用途：配置有效触发时施加给进入者 Rigidbody2D 的冲量大小。
    /// 调用边界：只对启用模拟且非 Static 的 attachedRigidbody 生效。
    /// 注意事项：使用 ForceMode2D.Impulse；普通刚体会先清除受力轴旧速度，IWanner Actor 的 X 分量改为可衰减速度增量。
    /// </summary>
    [Tooltip("施加给触发 Actor Rigidbody2D 的冲量大小")]
    [Min(0f)]
    [SerializeField] private float force = 30f;

    #endregion

    #region Runtime State

    private Transform _runtimeSpring;
    private Vector3 _restLocalPosition;
    private Sequence _springSequence;
    private bool _hasTriggered;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        _hasTriggered = false;
        _runtimeSpring = spring != null ? spring : transform;
        _restLocalPosition = _runtimeSpring.localPosition;

        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider == null)
        {
            this.LogWarning(
                "[IWannerGame_SpingTriggerCtrl] Collider2D is missing; spring trigger cannot be entered.");
            return;
        }

        if (!triggerCollider.isTrigger)
        {
            this.LogWarning(
                "[IWannerGame_SpingTriggerCtrl] Collider2D is not marked as Trigger; OnTriggerEnter2D will not run as expected.");
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        StopSpringAnimation(true);
    }

    private void OnDestroy()
    {
        StopSpringAnimation(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Application.isPlaying || other == null || string.IsNullOrWhiteSpace(triggerTag))
            return;

        if (!other.CompareTag(triggerTag) || (!canTriggerMultipleTimes && _hasTriggered))
            return;

        _hasTriggered = true;
        PlaySpringAnimation();
        ApplyConfiguredForce(other);
    }

    #endregion

    #region Spring Animation

    private void PlaySpringAnimation()
    {
        if (_runtimeSpring == null)
            return;

        StopSpringAnimation(false);

        Vector3 offsetLocalPosition = _restLocalPosition + new Vector3(offset.x, offset.y, 0f);
        float effectiveMoveDuration = Mathf.Max(0f, moveDuration);
        float effectiveReturnDuration = Mathf.Max(0f, returnDuration);

        _springSequence = DOTween.Sequence();
        _springSequence.Append(
            _runtimeSpring.DOLocalMove(offsetLocalPosition, effectiveMoveDuration)
                .SetEase(Ease.OutQuad));
        _springSequence.Append(
            _runtimeSpring.DOLocalMove(_restLocalPosition, effectiveReturnDuration)
                .SetEase(Ease.OutQuad));
        _springSequence.OnComplete(CompleteSpringAnimation);
    }

    private void CompleteSpringAnimation()
    {
        _springSequence = null;
        if (_runtimeSpring != null)
            _runtimeSpring.localPosition = _restLocalPosition;
    }

    private void StopSpringAnimation(bool restorePosition)
    {
        if (_springSequence != null && _springSequence.IsActive())
            _springSequence.Kill(false);
        _springSequence = null;

        if (restorePosition && _runtimeSpring != null)
            _runtimeSpring.localPosition = _restLocalPosition;
    }

    #endregion

    #region Actor Force

    private void ApplyConfiguredForce(Collider2D actorCollider)
    {
        Rigidbody2D actorRigidbody = actorCollider.attachedRigidbody;
        if (actorRigidbody == null)
        {
            this.LogWarning(
                "[IWannerGame_SpingTriggerCtrl] Triggered Collider2D has no attached Rigidbody2D; configured force was skipped.");
            return;
        }

        if (!actorRigidbody.simulated || actorRigidbody.bodyType == RigidbodyType2D.Static)
        {
            this.LogWarning(
                "[IWannerGame_SpingTriggerCtrl] Triggered Rigidbody2D is not simulated or is Static; configured force was skipped.");
            return;
        }

        Vector2 effectiveForceDirection = GetEffectiveForceDirection();

        float effectiveForce = Mathf.Max(0f, force);
        if (effectiveForce <= 0f)
            return;

        Vector2 impulse = effectiveForceDirection * effectiveForce;
        Actor_IWanner iwannerActor = actorRigidbody.GetComponent<Actor_IWanner>();
        if (actorRigidbody.bodyType == RigidbodyType2D.Dynamic
            && iwannerActor != null
            && Mathf.Abs(impulse.x) > Mathf.Epsilon)
        {
            // IWanner Actor 的 X 由自身移动更新合成；横向冲量换算为外部速度交给 Actor 衰减，避免下一帧被输入速度覆盖。
            if (Mathf.Abs(impulse.y) > Mathf.Epsilon)
            {
                actorRigidbody.linearVelocityY = 0f;
                actorRigidbody.AddForce(new Vector2(0f, impulse.y), ForceMode2D.Impulse);
            }

            float effectiveMass = Mathf.Max(actorRigidbody.mass, Mathf.Epsilon);
            iwannerActor.AddDecayingHorizontalVelocity(impulse.x / effectiveMass);
            return;
        }

        // 默认纯竖向和普通刚体继续清除受力轴旧速度，再由 Physics2D 处理完整 Impulse。
        Vector2 currentVelocity = actorRigidbody.linearVelocity;
        actorRigidbody.linearVelocity = currentVelocity
                                        - Vector2.Dot(currentVelocity, effectiveForceDirection)
                                        * effectiveForceDirection;
        actorRigidbody.AddForce(impulse, ForceMode2D.Impulse);
    }

    private Vector2 GetEffectiveForceDirection()
    {
        return forceDirection.sqrMagnitude > Mathf.Epsilon
            ? forceDirection.normalized
            : Vector2.up;
    }

    #endregion

    #region Gizmos

    /// <summary>
    /// 用途：在编辑器中预览弹簧局部位移方向和 Actor 的世界空间受力方向。
    /// 调用边界：只读取当前 Transform 与序列化配置，不运行 Tween 或物理逻辑。
    /// 注意事项：黄色表示弹簧位移；青色单位长度线表示归一化受力方向，不表示 force 大小。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Transform previewSpring = spring != null ? spring : transform;
        Vector3 startWorldPosition = previewSpring.position;
        Vector3 endLocalPosition = previewSpring.localPosition + new Vector3(offset.x, offset.y, 0f);
        Vector3 endWorldPosition = previewSpring.parent != null
            ? previewSpring.parent.TransformPoint(endLocalPosition)
            : endLocalPosition;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(startWorldPosition, endWorldPosition);
        Gizmos.DrawWireSphere(endWorldPosition, 0.1f);

        Vector2 previewForceDirection = GetEffectiveForceDirection();
        Vector3 forceStartWorldPosition = transform.position;
        Vector3 forceEndWorldPosition = forceStartWorldPosition
                                        + new Vector3(previewForceDirection.x, previewForceDirection.y, 0f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(forceStartWorldPosition, forceEndWorldPosition);
        Gizmos.DrawWireSphere(forceEndWorldPosition, 0.1f);
    }

    #endregion
}

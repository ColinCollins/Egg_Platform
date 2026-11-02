using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using DG.Tweening;
using Game.Events;
using UnityEngine;

/// <summary>
/// 控制 GameObjects List 水平移动，可以通过设置 rigidbody2D 得 velocity。velocity 暴露给我配置。
/// 设置 range position。单个 gameObject 移动到指定位置后从 end 移动回到 start。通过 Transform 配置 start 和 end。
/// </summary>
[DisallowMultipleComponent]
public sealed class ILevel027Ctrl : MonoBehaviour, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：配置需要沿世界 X 轴循环移动的对象。
    /// 调用边界：对象必须在同一 GameObject 上挂有非 Static 的 Rigidbody2D；每次控制器启用时缓存一次引用。
    /// 注意事项：列表中的空对象、缺少刚体的对象和 Static 刚体会被忽略并记录警告。
    /// </summary>
    [Tooltip("需要水平循环移动的对象；对象自身必须挂有非 Static 的 Rigidbody2D")]
    [SerializeField] private List<GameObject> movingObjects = new();

    /// <summary>
    /// 用途：配置移动对象从起点前往终点的水平速度大小，单位为 Unity 单位/秒。
    /// 调用边界：仅写入 Rigidbody2D.linearVelocity 的 X 分量，Y 分量保持不变。
    /// 注意事项：方向由 startPoint 到 endPoint 的世界 X 坐标决定，运行时使用该值的绝对值。
    /// </summary>
    [Tooltip("水平移动速度大小；方向由 Start 到 End 决定")]
    [Min(0f)]
    [SerializeField] private float horizontalVelocity = 3f;

    /// <summary>
    /// 用途：配置 ReverseVelocity 触发后移动对象相对初始 X 缩放的倍率。
    /// 调用边界：只修改有效移动对象的 localScale.x，Y/Z 缩放保持触发时的当前值。
    /// 注意事项：倍率限制为 0～1；重复触发始终以初始 X 缩放计算，不会累计缩小。
    /// </summary>
    [Tooltip("Trigger 反转后对象的 X 缩放倍率；0.5 表示缩小为初始宽度的一半")]
    [Range(0f, 1f)]
    [SerializeField] private float triggeredScaleXMultiplier = 0.5f;

    /// <summary>
    /// 用途：配置 ReverseVelocity 触发后移动对象缩放到目标宽度所需的时间，单位为秒。
    /// 调用边界：所有有效移动对象同时开始缩放，使用物理步进更新；重复触发会从当前缩放重新过渡到同一目标值。
    /// 注意事项：0 表示立即完成缩放，负值会按 0 处理；暂停时 Tween 不推进。
    /// </summary>
    [Tooltip("Trigger 反转后缩放到目标宽度所需时间；0 表示立即完成")]
    [Min(0f)]
    [SerializeField] private float triggeredScaleDuration = 0.5f;

    /// <summary>
    /// 用途：配置对象循环重置时使用的世界 X 坐标。
    /// 调用边界：对象到达终点后只把 Rigidbody2D.position.x 重置到该点，保留自身 Y 坐标。
    /// 注意事项：必须与 endPoint 具有不同的世界 X 坐标。
    /// </summary>
    [Tooltip("循环移动的起点；只使用世界 X 坐标")]
    [SerializeField] private Transform startPoint;

    /// <summary>
    /// 用途：配置对象本轮移动结束的世界 X 坐标。
    /// 调用边界：每个对象独立检测是否到达该点，到达后立即回到 startPoint。
    /// 注意事项：必须与 startPoint 具有不同的世界 X 坐标。
    /// </summary>
    [Tooltip("循环移动的终点；只使用世界 X 坐标")]
    [SerializeField] private Transform endPoint;

    #endregion

    #region Runtime State

    private readonly List<Rigidbody2D> _movingBodies = new();
    private readonly Dictionary<Rigidbody2D, float> _initialScaleXByBody = new();
    private readonly Dictionary<Rigidbody2D, Tween> _scaleTweenByBody = new();
    private EventSubscriber _subscriber;
    private bool _isRuntimeReady;
    private bool _hasMovementStarted;
    private float _movementDirectionMultiplier = 1f;

    #endregion

    #region Public API

    /// <summary>
    /// 反转当前水平移动方向，把所有有效移动对象的 X 缩放设置为配置倍率，并在已启动时立即更新刚体速度。
    /// 可由本关 TriggerExecutor 的 UnityEvent 或其它明确的本关运行时逻辑重复调用。
    /// 每调用一次切换一次方向；缩放始终基于对象初始值，不会随重复触发累计缩小。
    /// </summary>
    public void ReverseVelocity()
    {
        if (!Application.isPlaying)
        {
            this.LogWarning("[ILevel027Ctrl] ReverseVelocity is only available in PlayMode.");
            return;
        }

        if (!_isRuntimeReady || startPoint == null || endPoint == null)
        {
            this.LogWarning("[ILevel027Ctrl] Runtime movement is not ready; velocity cannot be reversed.");
            return;
        }

        float rangeDeltaX = endPoint.position.x - startPoint.position.x;
        if (Mathf.Approximately(rangeDeltaX, 0f))
        {
            StopHorizontalMovement();
            _isRuntimeReady = false;
            this.LogWarning("[ILevel027Ctrl] StartPoint and EndPoint have the same world X; velocity cannot be reversed.");
            return;
        }

        _movementDirectionMultiplier *= -1f;
        AnimateTriggeredScaleX();
        if (_hasMovementStarted)
            SetHorizontalVelocity(GetConfiguredVelocity(rangeDeltaX));
    }

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        _movementDirectionMultiplier = 1f;
        _hasMovementStarted = false;
        _isRuntimeReady = TryBuildRuntimeState();
        StopHorizontalMovement();

        EventsUtils.ResetEvents(ref _subscriber);
        if (!_isRuntimeReady)
            return;

        _subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerRightMove);
        _subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);
    }

    private void FixedUpdate()
    {
        if (!_isRuntimeReady || !_hasMovementStarted)
            return;

        if (startPoint == null || endPoint == null)
        {
            StopHorizontalMovement();
            _isRuntimeReady = false;
            this.LogWarning("[ILevel027Ctrl] StartPoint or EndPoint was lost at runtime; movement has stopped.");
            return;
        }

        float rangeDeltaX = endPoint.position.x - startPoint.position.x;
        if (Mathf.Approximately(rangeDeltaX, 0f))
        {
            StopHorizontalMovement();
            _isRuntimeReady = false;
            this.LogWarning("[ILevel027Ctrl] StartPoint and EndPoint have the same world X; movement has stopped.");
            return;
        }

        float speed = Mathf.Abs(horizontalVelocity);
        float movementDirection = Mathf.Sign(rangeDeltaX) * _movementDirectionMultiplier;
        float signedVelocity = movementDirection * speed;
        float targetX = _movementDirectionMultiplier > 0f
            ? endPoint.position.x
            : startPoint.position.x;
        float resetX = _movementDirectionMultiplier > 0f
            ? startPoint.position.x
            : endPoint.position.x;

        for (int i = 0; i < _movingBodies.Count; i++)
        {
            Rigidbody2D body = _movingBodies[i];
            if (body == null || body.bodyType == RigidbodyType2D.Static)
                continue;

            if (speed > 0f && HasReachedTarget(body.position.x, targetX, movementDirection))
            {
                Vector2 resetPosition = body.position;
                resetPosition.x = resetX;
                body.position = resetPosition;
            }

            body.linearVelocity = new Vector2(signedVelocity, body.linearVelocityY);
        }

    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref _subscriber);
        KillScaleTweens();
        StopHorizontalMovement();
        _isRuntimeReady = false;
        _hasMovementStarted = false;
    }

    #endregion

    #region Player Input Events

    private void OnPlayerRightMove(PlayerRightMoveEvent evt)
    {
        StartMovement();
    }

    private void OnPlayerLeftMove(PlayerLeftMoveEvent evt)
    {
        StartMovement();
    }

    private void StartMovement()
    {
        if (!_isRuntimeReady || _hasMovementStarted || startPoint == null || endPoint == null)
            return;

        float rangeDeltaX = endPoint.position.x - startPoint.position.x;
        if (Mathf.Approximately(rangeDeltaX, 0f))
        {
            StopHorizontalMovement();
            _isRuntimeReady = false;
            this.LogWarning("[ILevel027Ctrl] StartPoint and EndPoint have the same world X; movement cannot start.");
            return;
        }

        _hasMovementStarted = true;
        SetHorizontalVelocity(GetConfiguredVelocity(rangeDeltaX));
    }

    #endregion

    #region Runtime Helpers

    private bool TryBuildRuntimeState()
    {
        _movingBodies.Clear();

        if (startPoint == null || endPoint == null)
        {
            this.LogWarning("[ILevel027Ctrl] StartPoint or EndPoint is missing; movement will not start.");
            return false;
        }

        if (Mathf.Approximately(startPoint.position.x, endPoint.position.x))
        {
            this.LogWarning("[ILevel027Ctrl] StartPoint and EndPoint must have different world X positions.");
            return false;
        }

        if (movingObjects == null || movingObjects.Count == 0)
        {
            this.LogWarning("[ILevel027Ctrl] MovingObjects is empty; movement will not start.");
            return false;
        }

        for (int i = 0; i < movingObjects.Count; i++)
        {
            GameObject movingObject = movingObjects[i];
            if (movingObject == null)
            {
                this.LogWarning($"[ILevel027Ctrl] MovingObjects[{i}] is missing and will be ignored.");
                continue;
            }

            if (!movingObject.TryGetComponent(out Rigidbody2D body))
            {
                this.LogWarning($"[ILevel027Ctrl] {movingObject.name} has no Rigidbody2D and will be ignored.");
                continue;
            }

            if (body.bodyType == RigidbodyType2D.Static)
            {
                this.LogWarning($"[ILevel027Ctrl] {movingObject.name} uses a Static Rigidbody2D and will be ignored.");
                continue;
            }

            if (!_movingBodies.Contains(body))
            {
                _movingBodies.Add(body);
                _initialScaleXByBody.TryAdd(body, movingObject.transform.localScale.x);
            }
        }

        if (_movingBodies.Count > 0)
            return true;

        this.LogWarning("[ILevel027Ctrl] No movable Rigidbody2D was found; movement will not start.");
        return false;
    }

    private static bool HasReachedTarget(float currentX, float targetX, float movementDirection)
    {
        return movementDirection > 0f
            ? currentX >= targetX
            : currentX <= targetX;
    }

    private void StopHorizontalMovement()
    {
        SetHorizontalVelocity(0f);
    }

    private float GetConfiguredVelocity(float rangeDeltaX)
    {
        return Mathf.Sign(rangeDeltaX)
               * _movementDirectionMultiplier
               * Mathf.Abs(horizontalVelocity);
    }

    private void SetHorizontalVelocity(float signedVelocity)
    {
        for (int i = 0; i < _movingBodies.Count; i++)
        {
            Rigidbody2D body = _movingBodies[i];
            if (body == null || body.bodyType == RigidbodyType2D.Static)
                continue;

            body.linearVelocity = new Vector2(signedVelocity, body.linearVelocityY);
        }
    }

    private void AnimateTriggeredScaleX()
    {
        float multiplier = Mathf.Clamp01(triggeredScaleXMultiplier);
        float duration = Mathf.Max(0f, triggeredScaleDuration);

        for (int i = 0; i < _movingBodies.Count; i++)
        {
            Rigidbody2D body = _movingBodies[i];
            if (body == null || !_initialScaleXByBody.TryGetValue(body, out float initialScaleX))
                continue;

            KillScaleTween(body);

            float targetScaleX = initialScaleX * multiplier;
            if (duration <= 0f)
            {
                Vector3 localScale = body.transform.localScale;
                localScale.x = targetScaleX;
                body.transform.localScale = localScale;
                continue;
            }

            Tween scaleTween = body.transform
                .DOScaleX(targetScaleX, duration)
                .SetEase(Ease.InOutSine)
                .SetUpdate(UpdateType.Fixed)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnKill(() => _scaleTweenByBody.Remove(body));
            _scaleTweenByBody[body] = scaleTween;
        }
    }

    private void KillScaleTween(Rigidbody2D body)
    {
        if (body == null || !_scaleTweenByBody.TryGetValue(body, out Tween scaleTween))
            return;

        _scaleTweenByBody.Remove(body);
        scaleTween.Kill();
    }

    private void KillScaleTweens()
    {
        if (_scaleTweenByBody.Count == 0)
            return;

        Tween[] tweens = new Tween[_scaleTweenByBody.Count];
        _scaleTweenByBody.Values.CopyTo(tweens, 0);
        _scaleTweenByBody.Clear();

        for (int i = 0; i < tweens.Length; i++)
            tweens[i]?.Kill();
    }

    #endregion
}

using System;
using Bear.Logger;
using UnityEngine;

/// <summary>
/// 控制 ILevel_026 的三块移动平台：统一水平移动、按各自延迟掉落，并显式携带站在平台上的 Actor。
/// 调用边界：仅绑定到 ILevel_026，由本关 Trigger 的 UnityEvent 调用 StartMovingPlatforms。
/// </summary>
[DisallowMultipleComponent]
public sealed class ILevel026Ctrl : MonoBehaviour, IDebuger
{
    #region Serialized Types

    [Serializable]
    private sealed class PlatformSettings
    {
        /// <summary>
        /// 用途：绑定本关需要水平移动并可能掉落的平台刚体。
        /// 调用边界：必须为 Kinematic Rigidbody2D，Collider2D 应与刚体位于同一对象。
        /// 注意事项：空引用或 Static 刚体会在运行时被忽略并记录警告。
        /// </summary>
        [Tooltip("平台的 Kinematic Rigidbody2D")]
        [SerializeField] private Rigidbody2D body;

        /// <summary>
        /// 用途：配置从 StartMovingPlatforms 被调用到该平台切换为 Dynamic 的等待秒数。
        /// 调用边界：在 FixedUpdate 中计时，到点后由重力接管该刚体。
        /// 注意事项：小于 0 表示该平台不掉落。
        /// </summary>
        [Tooltip("触发后多少秒掉落；小于 0 表示不掉落")]
        [SerializeField] private float dropDelay = -1f;

        [NonSerialized] internal Collider2D Collider;
        [NonSerialized] internal bool HasDropped;

        internal Rigidbody2D Body => body;
        internal float DropDelay => dropDelay;
    }

    #endregion

    #region Serialized Fields

    /// <summary>
    /// 用途：配置三块平台在掉落前的统一世界 X 轴移动速度，单位为 Unity 单位/秒。
    /// 调用边界：StartMovingPlatforms 后写入仍为 Kinematic 的平台刚体。
    /// 注意事项：正数向右、负数向左，0 表示仅等待掉落。
    /// </summary>
    [Tooltip("三块平台掉落前的水平速度；正数向右，负数向左")]
    [SerializeField] private float horizontalMoveSpeed = 2.46f;

    /// <summary>
    /// 用途：限制未掉落平台持续水平移动的时间。
    /// 调用边界：计时结束后只停止仍为 Kinematic 的平台，不阻止尚未到时的掉落。
    /// 注意事项：小于 0 表示持续移动，直到平台掉落或关卡被销毁。
    /// </summary>
    [Tooltip("水平移动持续时间；小于 0 表示持续移动")]
    [SerializeField] private float movementDuration = 5f;

    /// <summary>
    /// 用途：配置本关三块平台及各自掉落时机。
    /// 调用边界：建议保持三个元素，触发时同时开始移动。
    /// 注意事项：重复引用同一刚体只会采用列表中首次出现的配置。
    /// </summary>
    [Tooltip("三块平台及各自掉落延迟")]
    [SerializeField] private PlatformSettings[] platforms = new PlatformSettings[3];

    /// <summary>
    /// 用途：绑定需要被移动平台携带的本关 Actor。
    /// 调用边界：仅在 Actor 站在尚未掉落的平台上时叠加平台水平速度。
    /// 注意事项：缺失时平台仍移动和掉落，但不会执行乘客携带。
    /// </summary>
    [Tooltip("站在平台上时需要被携带的 ILevel_026 Actor")]
    [SerializeField] private ActorCtrl actor;

    /// <summary>
    /// 用途：允许 Actor 脚底与平台顶部之间存在少量物理接触误差。
    /// 调用边界：仅参与本关乘客判定，不改变 Collider 或全局 Physics2D 设置。
    /// 注意事项：值过大会让临近但未站立的 Actor 被误判为乘客。
    /// </summary>
    [Tooltip("Actor 脚底与平台顶部的站立判定容差")]
    [Min(0.01f)]
    [SerializeField] private float riderStandingTolerance = 0.08f;

    #endregion

    #region Runtime State

    private Rigidbody2D _actorBody;
    private Collider2D _actorCollider;
    private bool _isRuntimeReady;
    private bool _isSequenceRunning;
    private bool _hasTriggered;
    private float _elapsedTime;

    #endregion

    #region Public API

    /// <summary>
    /// 让三块有效平台同时按配置速度开始水平移动，并启动各自的掉落计时。
    /// 调用边界：由 ILevel_026 Trigger 的 UnityEvent 在 PlayMode 中调用；单次关卡实例只响应第一次调用。
    /// 注意事项：平台掉落会切换为 Dynamic，后续由各自重力参数和 Physics2D 接管。
    /// </summary>
    public void StartMovingPlatforms()
    {
        if (!Application.isPlaying)
        {
            this.LogWarning("[ILevel026Ctrl] StartMovingPlatforms is only available in PlayMode.");
            return;
        }

        if (_hasTriggered)
            return;

        if (!_isRuntimeReady)
        {
            this.LogWarning("[ILevel026Ctrl] Runtime platform configuration is invalid; movement cannot start.");
            return;
        }

        _hasTriggered = true;
        _isSequenceRunning = true;
        _elapsedTime = 0f;

        SetKinematicPlatformVelocity(horizontalMoveSpeed);
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        _isRuntimeReady = TryBuildRuntimeState();
        SetKinematicPlatformVelocity(0f);
    }

    private void FixedUpdate()
    {
        if (!_isSequenceRunning || !_isRuntimeReady)
            return;

        _elapsedTime += Time.fixedDeltaTime;
        DropDuePlatforms();

        bool isHorizontalMovementActive = movementDuration < 0f || _elapsedTime < movementDuration;
        float platformVelocityX = isHorizontalMovementActive ? horizontalMoveSpeed : 0f;
        SetKinematicPlatformVelocity(platformVelocityX);
        CarryStandingActor(platformVelocityX);

        if (!isHorizontalMovementActive && !HasPendingDrop())
            _isSequenceRunning = false;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        SetKinematicPlatformVelocity(0f);
        _isSequenceRunning = false;
    }

    #endregion

    #region Runtime Setup

    private bool TryBuildRuntimeState()
    {
        _actorBody = actor != null
            ? (actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>())
            : null;
        _actorCollider = actor != null ? actor.GetComponent<Collider2D>() : null;

        if (actor == null || _actorBody == null || _actorCollider == null)
            this.LogWarning("[ILevel026Ctrl] Actor, Actor Rigidbody2D or Actor Collider2D is missing; passenger carrying is disabled.");

        if (platforms == null || platforms.Length == 0)
        {
            this.LogWarning("[ILevel026Ctrl] Platforms is empty; movement cannot start.");
            return false;
        }

        bool hasValidPlatform = false;
        for (int i = 0; i < platforms.Length; i++)
        {
            PlatformSettings settings = platforms[i];
            if (settings == null || settings.Body == null)
            {
                this.LogWarning($"[ILevel026Ctrl] Platforms[{i}] is missing and will be ignored.");
                continue;
            }

            if (settings.Body.bodyType == RigidbodyType2D.Static)
            {
                this.LogWarning($"[ILevel026Ctrl] {settings.Body.name} uses a Static Rigidbody2D and will be ignored.");
                continue;
            }

            if (ContainsBodyBeforeIndex(settings.Body, i))
            {
                this.LogWarning($"[ILevel026Ctrl] {settings.Body.name} is configured more than once; the duplicate entry will be ignored.");
                continue;
            }

            settings.Collider = settings.Body.GetComponent<Collider2D>();
            settings.HasDropped = false;
            settings.Body.bodyType = RigidbodyType2D.Kinematic;
            settings.Body.linearVelocity = Vector2.zero;
            hasValidPlatform = true;

            if (settings.Collider == null)
                this.LogWarning($"[ILevel026Ctrl] {settings.Body.name} has no Collider2D; it can move and drop but cannot carry Actor.");
        }

        if (hasValidPlatform)
            return true;

        this.LogWarning("[ILevel026Ctrl] No valid platform Rigidbody2D was found; movement cannot start.");
        return false;
    }

    private bool ContainsBodyBeforeIndex(Rigidbody2D body, int currentIndex)
    {
        for (int i = 0; i < currentIndex; i++)
        {
            PlatformSettings previous = platforms[i];
            if (previous != null && previous.Body == body)
                return true;
        }

        return false;
    }

    #endregion

    #region Platform Motion

    private void DropDuePlatforms()
    {
        for (int i = 0; i < platforms.Length; i++)
        {
            PlatformSettings settings = platforms[i];
            if (!IsControllablePlatform(settings)
                || settings.DropDelay < 0f
                || _elapsedTime < settings.DropDelay)
            {
                continue;
            }

            Rigidbody2D body = settings.Body;
            settings.HasDropped = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.linearVelocity = new Vector2(horizontalMoveSpeed, body.linearVelocityY);
            body.WakeUp();
        }
    }

    private void SetKinematicPlatformVelocity(float velocityX)
    {
        if (platforms == null)
            return;

        for (int i = 0; i < platforms.Length; i++)
        {
            PlatformSettings settings = platforms[i];
            if (!IsControllablePlatform(settings))
                continue;

            Rigidbody2D body = settings.Body;
            body.linearVelocity = new Vector2(velocityX, 0f);
        }
    }

    private bool HasPendingDrop()
    {
        for (int i = 0; i < platforms.Length; i++)
        {
            PlatformSettings settings = platforms[i];
            if (IsControllablePlatform(settings) && settings.DropDelay >= 0f)
                return true;
        }

        return false;
    }

    private static bool IsControllablePlatform(PlatformSettings settings)
    {
        return settings != null
               && settings.Body != null
               && !settings.HasDropped
               && settings.Body.bodyType == RigidbodyType2D.Kinematic;
    }

    #endregion

    #region Actor Passenger

    private void CarryStandingActor(float platformVelocityX)
    {
        if (actor == null
            || _actorBody == null
            || _actorCollider == null
            || !_actorBody.simulated
            || !_actorCollider.enabled
            || actor.IsDied
            || actor.IsKnocked)
        {
            return;
        }

        if (!IsActorSupportedByMovingPlatform())
            return;

        float relativeVelocityX = actor.IsWallDetected ? 0f : actor.XInput * actor.MoveSpeed;
        _actorBody.linearVelocity = new Vector2(
            platformVelocityX + relativeVelocityX,
            _actorBody.linearVelocityY);
    }

    private bool IsActorSupportedByMovingPlatform()
    {
        Bounds actorBounds = _actorCollider.bounds;
        float tolerance = Mathf.Max(0.01f, riderStandingTolerance);

        for (int i = 0; i < platforms.Length; i++)
        {
            PlatformSettings settings = platforms[i];
            Collider2D platformCollider = settings != null ? settings.Collider : null;
            if (!IsControllablePlatform(settings)
                || platformCollider == null
                || !platformCollider.enabled
                || platformCollider.isTrigger
                || !platformCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds platformBounds = platformCollider.bounds;
            bool overlapsHorizontally = actorBounds.max.x >= platformBounds.min.x
                                        && actorBounds.min.x <= platformBounds.max.x;
            bool actorIsAbovePlatform = actorBounds.center.y >= platformBounds.center.y;
            float verticalGap = actorBounds.min.y - platformBounds.max.y;
            if (!overlapsHorizontally
                || !actorIsAbovePlatform
                || verticalGap < -tolerance
                || verticalGap > tolerance)
            {
                continue;
            }

            if (_actorCollider.IsTouching(platformCollider))
                return true;

            ColliderDistance2D distance = _actorCollider.Distance(platformCollider);
            float contactDistance = Mathf.Max(0.02f, Physics2D.defaultContactOffset * 2f);
            if (distance.isValid && distance.distance <= contactDistance)
                return true;
        }

        return false;
    }

    #endregion
}

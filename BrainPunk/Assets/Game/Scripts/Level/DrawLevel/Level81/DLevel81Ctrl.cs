using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// DrawGame 第 81 关循环陷阱控制器。
/// 第一笔有效画线完成后，三个发射器按 Inspector 顺序循环执行“闪烁预警、直线连发、等待”。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 81 Ctrl")]
public sealed class DLevel81Ctrl : MonoBehaviour,
    IDrawStrokeCompletedHandler,
    IEventSender,
    IDebuger
{
    #region Types

    [Serializable]
    private sealed class LauncherBinding
    {
        /// <summary>
        /// 发射器朝向根节点；陷阱沿该节点的局部下方向发射。
        /// 调用边界：必须绑定当前 DLevel_081 内对应的 L116ObstacleVisualTemplates 节点。
        /// </summary>
        [Tooltip("发射器朝向根节点；运行时沿它的局部下方向发射。")]
        [SerializeField] private Transform directionRoot;

        /// <summary>
        /// 发射器下的红色陷阱模板，同时承担发射前闪烁提示。
        /// 调用边界：必须绑定对应 Sp_l116_jc_01_RedDownTemplate 的 SpriteRenderer。
        /// </summary>
        [Tooltip("红色陷阱模板；运行时作为闪烁提示和对象池克隆来源。")]
        [SerializeField] private SpriteRenderer warningTemplate;

        internal Transform DirectionRoot => directionRoot;
        internal SpriteRenderer WarningTemplate => warningTemplate;
        internal bool IsValid => directionRoot != null && warningTemplate != null;
    }

    private enum LaunchPhase
    {
        Idle,
        Warning,
        Burst,
        LauncherDelay
    }

    #endregion

    #region Constants

    private const int RequiredLauncherCount = 3;
    private const int WarningFlashCount = 2;
    private const int MaximumPhaseTransitionsPerFrame = 32;
    private const float MinimumTimedPhaseDuration = 0.0001f;

    #endregion

    #region Inspector

    /// <summary>
    /// 当前 DLevel 根节点上的通用状态控制器。
    /// 调用边界：只读取当前关卡的暂停、结算与生命周期状态。
    /// </summary>
    [Header("关卡引用")]
    [Tooltip("当前 DLevel_081 根节点上的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 当前关卡的玩家 Actor；只有该 Actor 的碰撞体能触发陷阱失败。
    /// 调用边界：必须绑定当前 DLevel_081 内的 ActorCtrl。
    /// </summary>
    [Tooltip("陷阱唯一可命中的当前关卡 Actor。")]
    [SerializeField] private ActorCtrl actor;

    /// <summary>
    /// 陷阱命中时播放的局部粒子。
    /// 调用边界：允许为空；为空时仍会提交标准陷阱失败。
    /// </summary>
    [Tooltip("陷阱命中 Actor 时播放的粒子，可为空。")]
    [SerializeField] private ParticleSystem knock;

    /// <summary>
    /// 三个发射器的固定循环顺序。
    /// 注意事项：数组顺序就是运行时触发顺序，必须恰好配置三个有效元素。
    /// </summary>
    [Tooltip("三个发射器的循环顺序；数组顺序即触发顺序。")]
    [SerializeField] private LauncherBinding[] launchers;

    /// <summary>
    /// 每个发射器在一轮中连续发射的陷阱数量。
    /// 调用边界：运行时至少按 1 个处理。
    /// </summary>
    [Header("发射配置")]
    [Tooltip("每轮连续发射的陷阱数量。")]
    [Min(1)]
    [SerializeField] private int projectilesPerBurst = 3;

    /// <summary>
    /// 同一发射器连续两枚陷阱之间的间隔，单位为秒。
    /// 调用边界：仅影响当前一轮内部的发射节奏。
    /// </summary>
    [Tooltip("同一轮内相邻陷阱的发射间隔（秒）。")]
    [Min(0f)]
    [SerializeField] private float projectileInterval = 0.2f;

    /// <summary>
    /// 陷阱沿发射器朝向直线移动的速度，单位为世界单位/秒。
    /// </summary>
    [Tooltip("陷阱直线移动速度（世界单位/秒）。")]
    [Min(0f)]
    [SerializeField] private float launchSpeed = 10f;

    /// <summary>
    /// 当前发射器最后一枚陷阱发出后，到下一个发射器开始闪烁前的等待时间。
    /// </summary>
    [Tooltip("当前轮发射结束后，到下一个发射器开始预警的间隔（秒）。")]
    [Min(0f)]
    [SerializeField] private float launcherInterval = 1f;

    /// <summary>
    /// 红色模板每次亮起或熄灭持续的时间；每轮固定亮起两次后发射。
    /// </summary>
    [Header("预警与回收")]
    [Tooltip("闪烁单个亮/灭阶段的时间（秒）；每轮固定闪烁两次。")]
    [Min(0.01f)]
    [SerializeField] private float warningFlashInterval = 0.15f;

    /// <summary>
    /// 单个陷阱累计直线移动达到该距离后回收到对象池。
    /// 注意事项：只控制离场回收，不改变发射速度和命中判定。
    /// </summary>
    [Tooltip("陷阱累计移动到该距离后回收，避免离场对象长期占用池容量。")]
    [Min(0.1f)]
    [SerializeField] private float recycleDistance = 25f;

    /// <summary>
    /// Actor 命中后的向上击退力度；保持原陷阱模板的默认值。
    /// </summary>
    [Header("命中反馈")]
    [Tooltip("陷阱命中 Actor 后的向上击退力度。")]
    [SerializeField] private float knockForce = 10f;

    /// <summary>
    /// 命中表现开始到提交共享陷阱失败事件的等待时间。
    /// </summary>
    [Tooltip("命中后提交标准陷阱失败事件前的等待时间（秒）。")]
    [Min(0f)]
    [SerializeField] private float trapDelay = 0.5f;

    #endregion

    #region Runtime State

    private readonly List<DLevel81RuntimeTrap> _activeTraps = new();
    private LaunchPhase _phase;
    private float _phaseRemainingTime;
    private int _launcherIndex;
    private int _warningPhasesRemaining;
    private int _shotsRemaining;
    private int _templateCursor;
    private int _attemptVersion;
    private bool _isRunning;
    private bool _trapHitCommitted;
    private bool _ownsPoolRegistration;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        PrepareLauncherTemplates();
        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved += OnLevelResolved;
        EnsurePoolRegistered();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved -= OnLevelResolved;

        _attemptVersion++;
        StopLaunchFlow();
        RecycleAllActiveTraps();
        UnregisterOwnedPool();
    }

    private void Update()
    {
        if (TryStopForOutcome())
            return;

        if (!CanAdvanceRuntime())
            return;

        AdvanceLaunchFlow(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (!CanAdvanceRuntime())
            return;

        float deltaTime = Time.fixedDeltaTime;
        for (int i = _activeTraps.Count - 1; i >= 0; i--)
        {
            DLevel81RuntimeTrap trap = _activeTraps[i];
            if (trap == null || trap.Advance(deltaTime))
                RecycleTrapAt(i);
        }
    }

    #endregion

    #region Draw Lifecycle

    /// <summary>
    /// 第一笔有效画线完成后，从数组中的第一个发射器开始循环攻击。
    /// 调用边界：仅响应当前尝试的首笔，暂停或结算期间不会启动。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying ||
            !isActiveAndEnabled ||
            !result.IsFirstStroke ||
            _isRunning ||
            drawLevelCtrl == null ||
            drawLevelCtrl.IsPaused ||
            drawLevelCtrl.IsOutcomeLocked)
        {
            return;
        }

        if (!ValidateRuntimeReferences() || !EnsurePoolRegistered())
            return;

        _attemptVersion++;
        _trapHitCommitted = false;
        _isRunning = true;
        _launcherIndex = 0;
        BeginWarning();
    }

    /// <summary>
    /// 重置当前画线尝试时停止发射、隐藏预警，并回收本次尝试中的全部陷阱。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying)
            return;

        _attemptVersion++;
        _trapHitCommitted = false;
        StopLaunchFlow();
        RecycleAllActiveTraps();
    }

    private void OnLevelResolved(DrawLevelResult result)
    {
        _attemptVersion++;
        StopLaunchFlow();
        RecycleAllActiveTraps();
    }

    #endregion

    #region Launch Flow

    private void AdvanceLaunchFlow(float deltaTime)
    {
        float remainingDelta = Mathf.Max(0f, deltaTime);
        int transitionCount = 0;

        while (_isRunning && transitionCount++ < MaximumPhaseTransitionsPerFrame)
        {
            if (_phaseRemainingTime > remainingDelta)
            {
                _phaseRemainingTime -= remainingDelta;
                return;
            }

            remainingDelta -= Mathf.Max(0f, _phaseRemainingTime);
            AdvanceCurrentPhase();
            if (remainingDelta <= 0f && _phaseRemainingTime > 0f)
                return;
        }
    }

    private void AdvanceCurrentPhase()
    {
        switch (_phase)
        {
            case LaunchPhase.Warning:
                AdvanceWarning();
                break;
            case LaunchPhase.Burst:
                FireAndScheduleNextShot();
                break;
            case LaunchPhase.LauncherDelay:
                _launcherIndex = (_launcherIndex + 1) % RequiredLauncherCount;
                BeginWarning();
                break;
            default:
                StopLaunchFlow();
                break;
        }
    }

    private void BeginWarning()
    {
        HideAllWarnings();
        LauncherBinding launcher = GetCurrentLauncher();
        if (launcher == null)
        {
            this.LogError("[DLevel81Ctrl] 当前发射器绑定无效，循环陷阱已停止。");
            StopLaunchFlow();
            return;
        }

        _phase = LaunchPhase.Warning;
        _warningPhasesRemaining = WarningFlashCount * 2;
        launcher.WarningTemplate.enabled = true;
        _phaseRemainingTime = GetTimedDuration(warningFlashInterval);
    }

    private void AdvanceWarning()
    {
        LauncherBinding launcher = GetCurrentLauncher();
        if (launcher == null)
        {
            StopLaunchFlow();
            return;
        }

        _warningPhasesRemaining--;
        if (_warningPhasesRemaining <= 0)
        {
            launcher.WarningTemplate.enabled = false;
            BeginBurst();
            return;
        }

        launcher.WarningTemplate.enabled = !launcher.WarningTemplate.enabled;
        _phaseRemainingTime = GetTimedDuration(warningFlashInterval);
    }

    private void BeginBurst()
    {
        _phase = LaunchPhase.Burst;
        _shotsRemaining = Mathf.Max(1, projectilesPerBurst);
        FireAndScheduleNextShot();
    }

    private void FireAndScheduleNextShot()
    {
        SpawnTrapFromCurrentLauncher();
        _shotsRemaining--;
        if (_shotsRemaining > 0)
        {
            _phase = LaunchPhase.Burst;
            _phaseRemainingTime = GetTimedDuration(projectileInterval);
            return;
        }

        _phase = LaunchPhase.LauncherDelay;
        _phaseRemainingTime = GetTimedDuration(launcherInterval);
    }

    private void SpawnTrapFromCurrentLauncher()
    {
        LauncherBinding launcher = GetCurrentLauncher();
        ObjectPoolManager poolManager = ObjectPoolManager.Instance;
        if (launcher == null || poolManager == null || !_ownsPoolRegistration)
            return;

        DLevel81RuntimeTrap trap = poolManager.Get<DLevel81RuntimeTrap>();
        if (trap == null)
        {
            this.LogError("[DLevel81Ctrl] 对象池没有可用陷阱，本次发射已跳过。");
            return;
        }

        Transform templateTransform = launcher.WarningTemplate.transform;
        Vector2 direction = -(Vector2)launcher.DirectionRoot.up;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            direction = Vector2.down;

        trap.Launch(
            transform,
            templateTransform.position,
            templateTransform.rotation,
            templateTransform.lossyScale,
            direction.normalized,
            Mathf.Max(0f, launchSpeed),
            Mathf.Max(0.1f, recycleDistance));
        _activeTraps.Add(trap);
    }

    private void StopLaunchFlow()
    {
        _isRunning = false;
        _phase = LaunchPhase.Idle;
        _phaseRemainingTime = 0f;
        _warningPhasesRemaining = 0;
        _shotsRemaining = 0;
        HideAllWarnings();
    }

    private bool CanAdvanceRuntime()
    {
        return _isRunning &&
               drawLevelCtrl != null &&
               !drawLevelCtrl.IsPaused &&
               !drawLevelCtrl.IsOutcomeLocked;
    }

    private bool TryStopForOutcome()
    {
        if (!_isRunning || drawLevelCtrl == null || !drawLevelCtrl.IsOutcomeLocked)
            return false;

        StopLaunchFlow();
        RecycleAllActiveTraps();
        return true;
    }

    private LauncherBinding GetCurrentLauncher()
    {
        if (launchers == null ||
            _launcherIndex < 0 ||
            _launcherIndex >= launchers.Length ||
            launchers[_launcherIndex] == null ||
            !launchers[_launcherIndex].IsValid)
        {
            return null;
        }

        return launchers[_launcherIndex];
    }

    private static float GetTimedDuration(float duration)
    {
        return Mathf.Max(MinimumTimedPhaseDuration, duration);
    }

    #endregion

    #region Trap Hit

    /// <summary>
    /// 运行时陷阱命中回调入口，只接受当前关卡 Actor，且每次尝试最多提交一次失败。
    /// 调用边界：仅供 DLevel81RuntimeTrap 使用；绳子和其它碰撞体会在物理过滤层被排除。
    /// </summary>
    internal void TryHandleTrapHit(DLevel81RuntimeTrap trap, Collider2D other)
    {
        if (!_isRunning ||
            _trapHitCommitted ||
            trap == null ||
            other == null ||
            actor == null ||
            actor.IsDied ||
            drawLevelCtrl == null ||
            drawLevelCtrl.IsOutcomeLocked ||
            other.GetComponentInParent<ActorCtrl>() != actor)
        {
            return;
        }

        _trapHitCommitted = true;
        int currentAttempt = _attemptVersion;
        StopLaunchFlow();
        RecycleAllActiveTraps();

        if (knock != null)
        {
            knock.transform.position = actor.transform.position;
            knock.Play();
        }

        actor.Die();
        actor.Knockback(Vector2.up, knockForce).Forget();
        AudioManager.PlaySound("trap");
        DispatchTrapFailureAsync(currentAttempt).Forget();
    }

    private async UniTaskVoid DispatchTrapFailureAsync(int currentAttempt)
    {
        if (trapDelay > 0f)
        {
            bool isCanceled = await UniTask.WaitForSeconds(
                    trapDelay,
                    cancellationToken: this.GetCancellationTokenOnDestroy())
                .SuppressCancellationThrow();
            if (isCanceled)
                return;
        }

        if (this == null ||
            !isActiveAndEnabled ||
            !_trapHitCommitted ||
            currentAttempt != _attemptVersion ||
            actor == null ||
            !actor.gameObject.activeInHierarchy)
        {
            return;
        }

        this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
    }

    #endregion

    #region Pool Lifecycle

    private bool EnsurePoolRegistered()
    {
        if (_ownsPoolRegistration)
            return true;

        if (!ValidateRuntimeReferences())
            return false;

        ObjectPoolManager poolManager = ObjectPoolManager.Instance;
        if (poolManager == null)
        {
            this.LogError("[DLevel81Ctrl] ObjectPoolManager 缺失，无法创建陷阱对象池。");
            return false;
        }

        if (poolManager.IsPoolRegistered<DLevel81RuntimeTrap>())
        {
            this.LogError("[DLevel81Ctrl] DLevel81RuntimeTrap 对象池已被其它控制器占用。");
            return false;
        }

        int initialSize = RequiredLauncherCount * Mathf.Max(1, projectilesPerBurst);
        int maximumSize = initialSize * 2;
        _templateCursor = 0;
        poolManager.RegisterPool(
            CreatePooledTrap,
            initialSize,
            maximumSize);
        _ownsPoolRegistration = true;
        return true;
    }

    private DLevel81RuntimeTrap CreatePooledTrap()
    {
        LauncherBinding sourceLauncher = launchers[_templateCursor % RequiredLauncherCount];
        _templateCursor++;
        GameObject source = sourceLauncher.WarningTemplate.gameObject;
        GameObject instance = Instantiate(source);
        instance.name = "DLevel81RuntimeTrap";

        OnTriggerTrapHandle sharedTrapHandle = instance.GetComponent<OnTriggerTrapHandle>();
        if (sharedTrapHandle != null)
            sharedTrapHandle.enabled = false;

        DLevel81RuntimeTrap runtimeTrap = instance.GetComponent<DLevel81RuntimeTrap>();
        if (runtimeTrap == null)
            runtimeTrap = instance.AddComponent<DLevel81RuntimeTrap>();
        runtimeTrap.Bind(this);
        return runtimeTrap;
    }

    private void UnregisterOwnedPool()
    {
        if (!_ownsPoolRegistration)
            return;

        ObjectPoolManager poolManager = ObjectPoolManager.Instance;
        if (poolManager != null && poolManager.IsPoolRegistered<DLevel81RuntimeTrap>())
            poolManager.UnregisterPool<DLevel81RuntimeTrap>();
        _ownsPoolRegistration = false;
    }

    private void RecycleAllActiveTraps()
    {
        for (int i = _activeTraps.Count - 1; i >= 0; i--)
            RecycleTrapAt(i);
        _activeTraps.Clear();
    }

    private void RecycleTrapAt(int index)
    {
        if (index < 0 || index >= _activeTraps.Count)
            return;

        DLevel81RuntimeTrap trap = _activeTraps[index];
        _activeTraps.RemoveAt(index);
        if (trap == null)
            return;

        ObjectPoolManager poolManager = ObjectPoolManager.Instance;
        if (_ownsPoolRegistration &&
            poolManager != null &&
            poolManager.IsPoolRegistered<DLevel81RuntimeTrap>())
        {
            poolManager.Recycle(trap);
            return;
        }

        Destroy(trap.gameObject);
    }

    #endregion

    #region Template Setup And Validation

    private void PrepareLauncherTemplates()
    {
        if (launchers == null)
            return;

        foreach (LauncherBinding launcher in launchers)
        {
            if (launcher == null || !launcher.IsValid)
                continue;

            GameObject templateObject = launcher.WarningTemplate.gameObject;
            OnTriggerTrapHandle sharedTrapHandle =
                templateObject.GetComponent<OnTriggerTrapHandle>();
            if (sharedTrapHandle != null)
                sharedTrapHandle.enabled = false;

            Collider2D templateCollider = templateObject.GetComponent<Collider2D>();
            if (templateCollider != null)
                templateCollider.enabled = false;

            Rigidbody2D templateBody = templateObject.GetComponent<Rigidbody2D>();
            if (templateBody != null)
            {
                templateBody.linearVelocity = Vector2.zero;
                templateBody.angularVelocity = 0f;
                templateBody.simulated = false;
            }

            launcher.WarningTemplate.enabled = false;
            templateObject.SetActive(true);
        }
    }

    private void HideAllWarnings()
    {
        if (launchers == null)
            return;

        foreach (LauncherBinding launcher in launchers)
        {
            if (launcher != null && launcher.WarningTemplate != null)
                launcher.WarningTemplate.enabled = false;
        }
    }

    private bool ValidateRuntimeReferences()
    {
        if (drawLevelCtrl == null || actor == null)
        {
            this.LogError("[DLevel81Ctrl] DrawLevelCtrl 或 Actor 未绑定，无法启动循环陷阱。");
            return false;
        }

        if (launchers == null || launchers.Length != RequiredLauncherCount)
        {
            this.LogError("[DLevel81Ctrl] 必须按顺序绑定恰好三个发射器。");
            return false;
        }

        for (int i = 0; i < launchers.Length; i++)
        {
            if (launchers[i] == null || !launchers[i].IsValid)
            {
                this.LogError($"[DLevel81Ctrl] 第 {i + 1} 个发射器绑定不完整。");
                return false;
            }
        }

        if (LayerMask.NameToLayer("Actor") < 0)
        {
            this.LogError("[DLevel81Ctrl] 项目缺少 Actor Layer，陷阱无法安全过滤绳子碰撞。");
            return false;
        }

        return true;
    }

    #endregion
}

/// <summary>
/// DLevel81 对象池中的单个运行时陷阱。
/// 只使用 Kinematic Trigger 命中 Actor，不参与绳子或其它关卡物体的实体碰撞。
/// </summary>
internal sealed class DLevel81RuntimeTrap : MonoBehaviour, IRecycle
{
    #region Runtime State

    private DLevel81Ctrl _owner;
    private SpriteRenderer _renderer;
    private Collider2D _collider;
    private Rigidbody2D _body;
    private Vector2 _direction;
    private float _speed;
    private float _travelledDistance;
    private float _maximumDistance;
    private bool _isLaunched;

    #endregion

    #region Pool API

    /// <summary>
    /// 绑定创建该对象池实例的关卡控制器，并配置仅命中 Actor 的物理过滤。
    /// 调用边界：只由 DLevel81Ctrl 的对象池创建函数调用。
    /// </summary>
    internal void Bind(DLevel81Ctrl owner)
    {
        _owner = owner;
        CacheComponents();
        ConfigureActorOnlyTrigger();
    }

    /// <summary>
    /// 从对象池取出时先保持不可见且不参与物理，等待同帧 Launch 完成配置。
    /// </summary>
    public void OnSpawn()
    {
        CacheComponents();
        _isLaunched = false;
        if (_renderer != null)
            _renderer.enabled = false;
        if (_collider != null)
            _collider.enabled = false;
        if (_body != null)
        {
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.simulated = false;
        }
    }

    /// <summary>
    /// 回收到对象池时清空移动状态并关闭渲染与物理。
    /// </summary>
    public void OnRecycle()
    {
        _isLaunched = false;
        _travelledDistance = 0f;
        if (_renderer != null)
            _renderer.enabled = false;
        if (_collider != null)
            _collider.enabled = false;
        if (_body != null)
        {
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.simulated = false;
        }
    }

    #endregion

    #region Launch And Movement

    /// <summary>
    /// 以给定世界姿态和方向启动一次直线移动。
    /// 调用边界：每次 OnSpawn 后只能由 DLevel81Ctrl 调用一次。
    /// </summary>
    internal void Launch(
        Transform runtimeParent,
        Vector3 position,
        Quaternion rotation,
        Vector3 worldScale,
        Vector2 direction,
        float speed,
        float maximumDistance)
    {
        CacheComponents();
        transform.SetParent(runtimeParent, true);
        transform.SetPositionAndRotation(position, rotation);
        transform.localScale = worldScale;
        _direction = direction;
        _speed = speed;
        _maximumDistance = maximumDistance;
        _travelledDistance = 0f;
        _isLaunched = true;

        if (_renderer != null)
            _renderer.enabled = true;
        if (_collider != null)
            _collider.enabled = true;
        if (_body != null)
        {
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.gravityScale = 0f;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.constraints = RigidbodyConstraints2D.FreezeRotation;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.simulated = true;
            _body.WakeUp();
        }
    }

    /// <summary>
    /// 按 FixedUpdate 时间推进直线位移；达到回收距离时返回 true。
    /// </summary>
    internal bool Advance(float deltaTime)
    {
        if (!_isLaunched || _body == null)
            return true;

        float distance = Mathf.Max(0f, _speed) * Mathf.Max(0f, deltaTime);
        _body.MovePosition(_body.position + _direction * distance);
        _travelledDistance += distance;
        return _travelledDistance >= _maximumDistance;
    }

    #endregion

    #region Collision

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isLaunched)
            _owner?.TryHandleTrapHit(this, other);
    }

    private void CacheComponents()
    {
        if (_renderer == null)
            _renderer = GetComponent<SpriteRenderer>();
        if (_collider == null)
            _collider = GetComponent<Collider2D>();
        if (_body == null)
            _body = GetComponent<Rigidbody2D>();
    }

    private void ConfigureActorOnlyTrigger()
    {
        if (_collider == null)
            return;

        int actorLayer = LayerMask.NameToLayer("Actor");
        int actorMask = actorLayer >= 0 ? 1 << actorLayer : 0;
        _collider.isTrigger = true;
        _collider.includeLayers = actorMask;
        _collider.excludeLayers = ~actorMask;
    }

    #endregion
}

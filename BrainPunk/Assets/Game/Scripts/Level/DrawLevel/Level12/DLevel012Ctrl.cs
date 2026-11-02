using Bear.Logger;
using UnityEngine;

/// <summary>
/// DLevel_012 的逃跑门控制器。
/// 当玩家与 DoorEnemy 的水平距离不大于阈值时，驱动 DoorEnemy 向右跳出；
/// DoorEnemy 首次碰到 Draw 层后停止水平移动。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/DLevel 012 Ctrl")]
public sealed class DLevel012Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Inspector References

    /// <summary>
    /// 本关通用 Draw 生命周期控制器。
    /// 调用边界：必须绑定同一 DLevel_012 根节点上的 DrawLevelCtrl，用于暂停、结算和 Attempt Reset 边界。
    /// </summary>
    [Header("Level References")]
    [Tooltip("DLevel_012 根节点上的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 触发 DoorEnemy 跳出的玩家 Actor。
    /// 调用边界：必须绑定本关 Player，不能绑定 DoorEnemy 自身。
    /// </summary>
    [Tooltip("靠近 DoorEnemy 后触发其跳出的玩家 Actor。")]
    [SerializeField] private ActorCtrl actor;

    /// <summary>
    /// 本关需要被驱动的 DoorEnemy。
    /// 注意事项：该 Actor 没有独立 Update，必须由本控制器逐帧调用 OnUpdate。
    /// </summary>
    [Tooltip("向右跳出并在碰到 Draw 后停止的 DoorEnemy。")]
    [SerializeField] private DoorEnemyCtrl doorEnemy;

    /// <summary>
    /// DoorEnemy 上仅检测 Draw 层的碰撞监听器。
    /// 调用边界：不得复用 DoorEnemy 原有的 Actor 层监听器，否则会改变原门业务执行链。
    /// </summary>
    [Tooltip("DoorEnemy 上目标层仅为 Draw 的 LayerCollisionDetector。")]
    [SerializeField] private LayerCollisionDetector drawCollisionDetector;

    #endregion

    #region Inspector Settings

    /// <summary>
    /// 玩家与 DoorEnemy 的水平触发距离，按世界坐标 X 轴绝对距离计算。
    /// </summary>
    [Header("Escape Settings")]
    [Tooltip("玩家与 DoorEnemy 的 X 轴距离不大于该值时触发跳跃。")]
    [SerializeField, Min(0f)] private float triggerHorizontalDistance = 2f;

    #endregion

    #region Runtime State

    private Rigidbody2D _doorRigidbody;
    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;
    private Vector2 _initialVelocity;
    private float _initialAngularVelocity;
    private bool _hasCapturedInitialState;
    private bool _escapeStarted;
    private bool _stoppedByDraw;
    private bool _levelResolved;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (!ResolveAndValidateReferences())
            return;

        CaptureInitialState();
        drawCollisionDetector.OnEnter.RemoveListener(OnDoorEnteredDraw);
        drawCollisionDetector.OnEnter.AddListener(OnDoorEnteredDraw);
        drawLevelCtrl.Resolved -= OnLevelResolved;
        drawLevelCtrl.Resolved += OnLevelResolved;
    }

    private void Update()
    {
        if (!Application.isPlaying || doorEnemy == null || _levelResolved)
            return;

        if (drawLevelCtrl == null || drawLevelCtrl.IsPaused || doorEnemy.IsDied)
        {
            ClearDoorMoveInput();
            return;
        }

        if (_stoppedByDraw)
        {
            ClearDoorMoveInput();
            doorEnemy.OnUpdate();
            return;
        }

        if (_escapeStarted)
        {
            doorEnemy.SetMoveInput(1f);
            doorEnemy.OnUpdate();
            return;
        }

        // DoorEnemy 没有独立 Update；触发前也要刷新落地状态，保证 TriggerJump 能识别当前地面。
        ClearDoorMoveInput();
        doorEnemy.OnUpdate();

        if (actor == null ||
            Mathf.Abs(actor.transform.position.x - doorEnemy.transform.position.x) > triggerHorizontalDistance)
        {
            return;
        }

        StartDoorEscape();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (drawCollisionDetector != null)
            drawCollisionDetector.OnEnter.RemoveListener(OnDoorEnteredDraw);

        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved -= OnLevelResolved;

        ClearDoorMoveInput();
    }

    #endregion

    #region Draw Lifecycle

    /// <inheritdoc />
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        // 本关由玩家距离触发 DoorEnemy，不消费 StrokeCompleted。
    }

    /// <inheritdoc />
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying)
            return;

        ResolveAndValidateReferences();
        RestoreInitialState();
    }

    #endregion

    #region Collision And Result Callbacks

    private void OnDoorEnteredDraw(Collision2D collision)
    {
        if (!_escapeStarted || _stoppedByDraw || _levelResolved)
            return;

        _stoppedByDraw = true;
        ClearDoorMoveInput();

        if (_doorRigidbody != null)
            _doorRigidbody.linearVelocityX = 0f;

        doorEnemy?.UpdateAnimation();
    }

    private void OnLevelResolved(DrawLevelResult result)
    {
        _levelResolved = true;
        ClearDoorMoveInput();
    }

    #endregion

    #region Internal Helpers

    private void StartDoorEscape()
    {
        _escapeStarted = true;
        doorEnemy.SetMoveInput(1f);
        doorEnemy.TriggerJump();
        doorEnemy.OnUpdate();
    }

    private void ClearDoorMoveInput()
    {
        if (doorEnemy != null)
            doorEnemy.SetMoveInput(0f);
    }

    private bool ResolveAndValidateReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponent<DrawLevelCtrl>();

        if (doorEnemy == null)
            doorEnemy = GetComponentInChildren<DoorEnemyCtrl>(true);

        if (actor == null)
        {
            ActorCtrl[] actors = GetComponentsInChildren<ActorCtrl>(true);
            for (int i = 0; i < actors.Length; i++)
            {
                ActorCtrl candidate = actors[i];
                if (candidate != null && candidate != doorEnemy && candidate.CompareTag("Player"))
                {
                    actor = candidate;
                    break;
                }
            }
        }

        if (drawCollisionDetector == null && doorEnemy != null)
        {
            LayerCollisionDetector[] detectors = doorEnemy.GetComponents<LayerCollisionDetector>();
            if (detectors.Length > 1)
                drawCollisionDetector = detectors[detectors.Length - 1];
        }

        if (doorEnemy != null)
            _doorRigidbody = doorEnemy.GetComponent<Rigidbody2D>();

        bool isValid = true;
        if (drawLevelCtrl == null)
        {
            this.LogError("[DLevel012Ctrl] DrawLevelCtrl 未绑定，无法处理暂停、结算和重置边界。");
            isValid = false;
        }

        if (actor == null)
        {
            this.LogError("[DLevel012Ctrl] Player Actor 未绑定，无法检测触发距离。");
            isValid = false;
        }

        if (doorEnemy == null || _doorRigidbody == null)
        {
            this.LogError("[DLevel012Ctrl] DoorEnemyCtrl 或 Rigidbody2D 缺失，无法执行跳跃移动。");
            isValid = false;
        }

        if (drawCollisionDetector == null)
        {
            this.LogError("[DLevel012Ctrl] Draw LayerCollisionDetector 未绑定，DoorEnemy 碰到画线后无法停止。");
            isValid = false;
        }

        return isValid;
    }

    private void CaptureInitialState()
    {
        if (_hasCapturedInitialState || doorEnemy == null || _doorRigidbody == null)
            return;

        _initialLocalPosition = doorEnemy.transform.localPosition;
        _initialLocalRotation = doorEnemy.transform.localRotation;
        _initialVelocity = _doorRigidbody.linearVelocity;
        _initialAngularVelocity = _doorRigidbody.angularVelocity;
        _hasCapturedInitialState = true;
    }

    private void RestoreInitialState()
    {
        _escapeStarted = false;
        _stoppedByDraw = false;
        _levelResolved = false;
        ClearDoorMoveInput();

        if (!_hasCapturedInitialState || doorEnemy == null || _doorRigidbody == null)
            return;

        doorEnemy.transform.SetLocalPositionAndRotation(_initialLocalPosition, _initialLocalRotation);
        _doorRigidbody.linearVelocity = _initialVelocity;
        _doorRigidbody.angularVelocity = _initialAngularVelocity;
        _doorRigidbody.WakeUp();
        doorEnemy.UpdateAnimation();
    }

    #endregion
}

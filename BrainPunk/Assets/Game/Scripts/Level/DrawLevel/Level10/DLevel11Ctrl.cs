using Bear.Logger;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// DLevel_011 的小偷与钥匙控制器。
/// 第一笔有效画线完成后等待指定时间，再持续驱动小偷向右移动；
/// 钥匙触发时区分玩家取钥匙与小偷偷钥匙，小偷命中只启用钥匙跟随。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/DLevel 011 Ctrl")]
public sealed class DLevel11Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Inspector References

    /// <summary>
    /// 本关共享 Draw 状态控制器。
    /// 调用边界：必须绑定 DLevel_011 根节点上的 DrawLevelCtrl，不允许跨关卡引用。
    /// </summary>
    [Header("Runtime")]
    [Tooltip("DLevel_011 根节点上的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 第一笔完成后自动向右移动的小偷。
    /// 调用边界：必须绑定 DLevel_011 内 Tag 为 enemy 的 ActorCtrl。
    /// </summary>
    [Tooltip("第一笔完成并等待后持续向右移动的小偷 ActorCtrl。")]
    [SerializeField] private ActorCtrl enemy;

    /// <summary>
    /// 第一笔完成到小偷开始移动之间的等待时间，单位为秒。
    /// 注意事项：暂停和结算期间不累计等待时间。
    /// </summary>
    [Tooltip("第一笔完成后，小偷开始向右移动前的等待秒数。")]
    [SerializeField, Min(0f)] private float enemyMoveDelay = 0.5f;

    /// <summary>
    /// 钥匙上的跟随组件，初始应保持禁用。
    /// 调用边界：由 Key 的 OnTrigger2DHandle 把触发对象传给 OnKeyTriggerEntered；
    /// 小偷命中后本控制器把跟随目标设为 enemy 并启用该组件。
    /// </summary>
    [Header("Key")]
    [Tooltip("Key 上初始禁用、由小偷命中后启用的 FollowTarget。")]
    [SerializeField] private FollowTarget keyFollower;

    #endregion

    #region Runtime State

    private MoveFloatHandle _keyIdleMotion;
    private Transform _initialKeyFollowTarget;
    private Vector3 _initialKeyLocalPosition;
    private Quaternion _initialKeyLocalRotation;
    private bool _initialKeyFollowerEnabled;
    private bool _hasCapturedKeyState;
    private bool _isMovePending;
    private bool _isMovingRight;
    private bool _moveInputApplied;
    private bool _keyClaimed;
    private float _remainingMoveDelay;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        CaptureKeyState();

        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved += OnLevelResolved;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved -= OnLevelResolved;

        StopEnemyMovement();
    }

    private void Update()
    {
        if (!Application.isPlaying || enemy == null)
            return;

        if (drawLevelCtrl == null || drawLevelCtrl.IsPaused || drawLevelCtrl.IsOutcomeLocked || enemy.IsDied)
        {
            ClearEnemyMoveInput();
            return;
        }

        if (_isMovePending)
        {
            _remainingMoveDelay -= Time.deltaTime;
            if (_remainingMoveDelay > 0f)
                return;

            _isMovePending = false;
            _isMovingRight = true;
        }

        if (!_isMovingRight)
            return;

        enemy.SetMoveInput(1f);
        _moveInputApplied = true;
        enemy.OnUpdate();
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 第一笔有效画线完成后启动一次小偷移动倒计时；后续笔画不会重复计时。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !result.IsFirstStroke || _isMovePending || _isMovingRight)
            return;

        ResolveReferences();
        if (enemy == null)
        {
            this.LogError("[DLevel11Ctrl] Enemy ActorCtrl 缺失，第一笔完成后无法启动小偷移动。");
            return;
        }

        _remainingMoveDelay = Mathf.Max(0f, enemyMoveDelay);
        _isMovePending = _remainingMoveDelay > 0f;
        _isMovingRight = !_isMovePending;
    }

    /// <summary>
    /// 当前尝试重置时停止小偷移动，并恢复钥匙的初始跟随和悬浮状态。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        StopEnemyMovement();
        RestoreKeyState();
    }

    #endregion

    #region Key Trigger

    /// <summary>
    /// 接收本关 Key 额外挂载的 enemy 专用 Trigger 触发对象。
    /// 调用边界：仅允许由 DLevel_011 Key 实例新增的 OnTrigger2DHandle UnityEvent 调用；
    /// Key 原有 Player Trigger 与 TriggerExecutor 不经过本方法，也不会被本关脚本改写。
    /// </summary>
    /// <param name="other">进入 Key Trigger 的 2D Collider。</param>
    public void OnEnemyEnteredKeyTrigger(Collider2D other)
    {
        if (!Application.isPlaying || other == null || _keyClaimed)
            return;

        ResolveReferences();
        ActorCtrl touchedActor = other.GetComponentInParent<ActorCtrl>();
        if (touchedActor == null)
            return;

        if (touchedActor != enemy)
            return;

        if (keyFollower == null)
        {
            this.LogError("[DLevel11Ctrl] Key FollowTarget 缺失，小偷无法带走钥匙。");
            return;
        }

        _keyClaimed = true;
        _keyIdleMotion?.StopMove();
        keyFollower.SetTarget(enemy.transform);
        keyFollower.enabled = true;
    }

    #endregion

    #region Internal State

    private void OnLevelResolved(DrawLevelResult result)
    {
        StopEnemyMovement();
    }

    private void StopEnemyMovement()
    {
        _isMovePending = false;
        _isMovingRight = false;
        _remainingMoveDelay = 0f;
        ClearEnemyMoveInput();
    }

    private void ClearEnemyMoveInput()
    {
        if (!_moveInputApplied || enemy == null)
            return;

        enemy.SetMoveInput(0f);
        if (enemy.RB != null)
            enemy.RB.linearVelocityX = 0f;
        enemy.UpdateAnimation();
        _moveInputApplied = false;
    }

    private void ResolveReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponentInParent<DrawLevelCtrl>(true);

        if (enemy == null)
        {
            ActorCtrl[] actors = GetComponentsInChildren<ActorCtrl>(true);
            for (int i = 0; i < actors.Length; i++)
            {
                ActorCtrl actor = actors[i];
                if (actor != null && actor.CompareTag("enemy"))
                {
                    enemy = actor;
                    break;
                }
            }
        }

        if (keyFollower == null)
            keyFollower = GetComponentInChildren<FollowTarget>(true);

        if (keyFollower == null)
            return;

        _keyIdleMotion ??= keyFollower.GetComponent<MoveFloatHandle>();
    }

    private void CaptureKeyState()
    {
        if (_hasCapturedKeyState || keyFollower == null)
            return;

        _initialKeyFollowTarget = keyFollower.Target;
        _initialKeyLocalPosition = keyFollower.transform.localPosition;
        _initialKeyLocalRotation = keyFollower.transform.localRotation;
        _initialKeyFollowerEnabled = keyFollower.enabled;
        _hasCapturedKeyState = true;
    }

    private void RestoreKeyState()
    {
        ResolveReferences();
        CaptureKeyState();
        _keyClaimed = false;

        if (!_hasCapturedKeyState || keyFollower == null)
            return;

        keyFollower.enabled = false;
        keyFollower.SetTarget(_initialKeyFollowTarget);
        keyFollower.transform.localPosition = _initialKeyLocalPosition;
        keyFollower.transform.localRotation = _initialKeyLocalRotation;
        keyFollower.enabled = _initialKeyFollowerEnabled;

        if (_keyIdleMotion != null)
        {
            _keyIdleMotion.ResetToStart();
            if (_keyIdleMotion.enabled)
                _keyIdleMotion.StartMove();
        }
    }

    #endregion
}

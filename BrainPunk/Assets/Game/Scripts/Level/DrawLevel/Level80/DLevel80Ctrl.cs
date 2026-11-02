using Bear.Logger;
using UnityEngine;

/// <summary>第 80 关首笔蓄力踢球及玩家假扑救；只施一次足球冲量，结算沿用共享 Draw 流程。</summary>
[DisallowMultipleComponent]
public sealed class DLevel80Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Inspector

    /// <summary>同关 Draw 控制器，必须显式绑定，用于暂停、初始化与结果门禁。</summary>
    [Header("本关引用")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>玩家跳跃扑救角色，必须显式绑定本关 Actor 子类及其 Spine 组件。</summary>
    [SerializeField] private DLevel80ActorCtrl actor;

    /// <summary>首笔后原地蓄力踢球的 enemy，必须显式绑定本关 Actor 子类。</summary>
    [SerializeField] private DLevel80ActorCtrl enemy;

    /// <summary>足球刚体，必须显式绑定，踢球开始时转为 Dynamic 并施加一次冲量。</summary>
    [SerializeField] private Rigidbody2D football;

    /// <summary>足球冲量世界坐标方向，使用归一化向量；零向量表示不增加冲量。</summary>
    [Header("踢球与假扑救")]
    [Tooltip("世界方向：(1,0) 右，(0,1) 上，(-1,0) 左；长度不影响力度。仅踢球瞬间读取当前实例，Prefab 改参后需重开关卡。")]
    [SerializeField] private Vector2 kickDirection = Vector2.right;

    /// <summary>单次踢球冲量大小；球的质量、重力和阻尼仍使用其 Rigidbody2D 配置。</summary>
    [Min(0f)]
    [SerializeField] private float kickImpulse = 80f;

    /// <summary>扑救世界偏移：X 为同高度落地时的水平位移，Y 为正跳高；真实刚体跳跃，落地不复位。</summary>
    [Tooltip("X 为水平位移、Y 为跳高（需大于 0）；例如 (-0.5,1) 向左跳，实际落点受地形影响。")]
    [SerializeField] private Vector2 jumpOffset = new Vector2(-0.5f, 1f);

    #endregion

    #region Runtime State

    private bool _started;
    private bool _kicked;
    private bool _hasInitialState;
    private Vector2 _initialActorPosition;
    private Vector2 _initialEnemyPosition;
    private Vector2 _initialFootballPosition;
    private float _initialFootballRotation;
    private RigidbodyType2D _initialFootballType;
    private Vector2 _initialFootballVelocity;
    private float _initialFootballAngularVelocity;

    #endregion

    #region Draw Lifecycle

    /// <summary>首笔有效画线完成后开始 enemy 蓄力；每次尝试只启动一次，后续笔画不重播。</summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !result.IsFirstStroke || _started ||
            !CanRun() || drawLevelCtrl.IsPaused || football == null || actor.RB == null || enemy.RB == null)
            return;
        CaptureInitialState();
        if (!enemy.PlayFootballShoot())
        {
            this.LogError("[DLevel80Ctrl] enemy 蓄力/踢球动画不可用，未启动踢球。");
            return;
        }
        _started = true;
    }

    /// <summary>同实例重置取消旧动作并恢复角色/球初态；正常 Reset/Replay 仍走共享整关重建。</summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying)
            return;
        StopActions();
        if (_hasInitialState && actor != null && enemy != null && football != null)
        {
            RestoreActor(actor, _initialActorPosition);
            RestoreActor(enemy, _initialEnemyPosition);
            football.bodyType = _initialFootballType;
            football.position = _initialFootballPosition;
            football.rotation = _initialFootballRotation;
            football.linearVelocity = _initialFootballVelocity;
            football.angularVelocity = _initialFootballAngularVelocity;
        }
        _started = false;
        _kicked = false;
    }

    private void OnEnable()
    {
        if (Application.isPlaying && drawLevelCtrl != null)
            drawLevelCtrl.Resolved += OnResolved;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;
        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved -= OnResolved;
        StopActions();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;
        if (!CanRun())
        {
            StopActions();
            return;
        }

        bool paused = drawLevelCtrl.IsPaused;
        if (!_started)
        {
            if (!paused)
            {
                enemy.RefreshContactState();
                actor.RefreshContactState();
            }
            return;
        }
        if (enemy.TickAction(paused) && !_kicked)
        {
            _kicked = true;
            if (football != null)
            {
                football.bodyType = RigidbodyType2D.Dynamic;
                // 每次射门从零线速度施加冲量，避免旧动量混入用户填写的发射方向。
                football.linearVelocity = Vector2.zero;
                Vector2 impulse = kickDirection.normalized * Mathf.Max(0f, kickImpulse);
                football.AddForce(impulse, ForceMode2D.Impulse);
                this.Log($"[DLevel80Ctrl] 实例 {GetInstanceID()} 踢球：世界方向={kickDirection}，冲量={impulse}，初速度={football.linearVelocity}，质量={football.mass}，重力={football.gravityScale}，阻尼={football.linearDamping}");
            }
            if (!actor.PlayFakeSave(jumpOffset))
                this.LogError("[DLevel80Ctrl] Actor 的动画、Spine 引用、Dynamic 刚体或正跳高/向下重力不可用，未播放扑救。");
        }
        actor.TickAction(paused);
        if (!paused)
        {
            enemy.OnUpdate();
            if (_kicked)
                actor.OnUpdate();
            else
                actor.RefreshContactState();
        }
    }

    private void OnResolved(DrawLevelResult result) => StopActions();

    #endregion

    #region Local Helpers

    private bool CanRun() => drawLevelCtrl != null && drawLevelCtrl.IsRuntimeReady &&
        !drawLevelCtrl.IsOutcomeLocked && actor != null && enemy != null && actor != enemy &&
        actor.isActiveAndEnabled && enemy.isActiveAndEnabled && !actor.IsDied && !enemy.IsDied;

    private void StopActions()
    {
        actor?.CancelAction();
        enemy?.CancelAction();
    }

    private void CaptureInitialState()
    {
        if (_hasInitialState)
            return;
        _hasInitialState = true;
        _initialActorPosition = actor.RB.position;
        _initialEnemyPosition = enemy.RB.position;
        _initialFootballPosition = football.position;
        _initialFootballRotation = football.rotation;
        _initialFootballType = football.bodyType;
        _initialFootballVelocity = football.linearVelocity;
        _initialFootballAngularVelocity = football.angularVelocity;
    }

    private static void RestoreActor(DLevel80ActorCtrl target, Vector2 position)
    {
        if (target.RB != null)
        {
            target.RB.position = position;
            target.RB.linearVelocity = Vector2.zero;
        }
        target.SetMoveInput(0f);
        target.ResumeMoving();
        target.UpdateAnimation();
    }

    #endregion

    #region 本地编辑器预览

    // 只读取当前序列化引用和方向，选中本关 Ctrl 时显示世界方向，不初始化运行时系统。
    private void OnDrawGizmosSelected()
    {
        if (football == null || kickDirection.sqrMagnitude == 0f)
            return;
        Vector3 direction = kickDirection.normalized;
        Vector3 start = football.transform.position;
        Vector3 end = start + direction * 3f;
        Vector3 side = new Vector3(-direction.y, direction.x, 0f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawLine(end, end - direction * 0.4f + side * 0.2f);
        Gizmos.DrawLine(end, end - direction * 0.4f - side * 0.2f);
    }

    #endregion
}

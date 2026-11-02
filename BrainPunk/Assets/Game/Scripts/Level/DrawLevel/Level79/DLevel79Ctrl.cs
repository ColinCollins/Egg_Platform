using System.Collections;
using Bear.Logger;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 第 79 关首笔完成后的足球流程：右移指定距离、朝左蓄力两次，再踢球施加一次冲量。
/// 只管理本关动作；胜负和倒计时仍由既有 Draw 控制器负责。
/// </summary>
[DisallowMultipleComponent]
public sealed class DLevel79Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Inspector

    /// <summary>同关 Draw 控制器，必须显式绑定；用于初始化、暂停及结果门禁。</summary>
    [Header("本关引用")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>本关专用 Actor，必须显式绑定；负责行走、朝向和足球动作。</summary>
    [SerializeField] private DLevel79ActorCtrl actor;

    /// <summary>足球刚体，必须显式绑定；踢球时切换为 Dynamic 后施加冲量。</summary>
    [SerializeField] private Rigidbody2D football;

    /// <summary>首笔完成后向世界坐标右侧行走的 X 距离；0 表示原地转身踢球。</summary>
    [Header("移动与踢球")]
    [Min(0f)]
    [SerializeField] private float moveRightDistance = 2f;

    /// <summary>踢球冲量的世界坐标方向；使用归一化后的方向，零向量表示不施力。</summary>
    [Tooltip("世界坐标方向：(-1, 0) 向左；(-1, 1) 向左上。长度不影响力度。")]
    [SerializeField] private Vector2 kickDirection = Vector2.left;

    /// <summary>单次踢球冲量大小；球质量、重力、阻尼仍使用足球刚体上的配置。</summary>
    [Min(0f)]
    [SerializeField] private float kickImpulse = 20f;

    /// <summary>进球后展示的 FIFA Goal 界面；必须绑定本关 Prefab 内已有且默认隐藏的实例。</summary>
    [Header("进球结算表现")]
    [SerializeField] private GameObject fifaGoal;

    /// <summary>显示进球界面并播放吹哨音效后，等待进入标准胜利结算的时间。</summary>
    [Min(0f)]
    [SerializeField] private float goalVictoryDelay = 0.5f;

    #endregion

    #region Runtime State

    private enum ShotStage { Waiting, Moving, Acting, Finished }
    private ShotStage _stage;
    private float _targetX;
    private bool _hasInitialState;
    private Vector2 _initialActorPosition;
    private bool _initialFacingRight;
    private Vector2 _initialFootballPosition;
    private float _initialFootballRotation;
    private RigidbodyType2D _initialFootballBodyType;
    private Vector2 _initialFootballVelocity;
    private float _initialFootballAngularVelocity;
    private Coroutine _goalSuccessCoroutine;
    private bool _ownsSuccessPerformance;

    #endregion

    #region Draw Lifecycle

    /// <summary>
    /// 本关足球进入球门后的成功入口：锁住其它结算，展示 FIFA Goal 并播放吹哨，
    /// 等待配置时间后再提交标准 Draw 胜利。只接受已绑定足球刚体所属的碰撞体。
    /// </summary>
    public void OnFootballEnteredGoal(Collider2D collider)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || _ownsSuccessPerformance ||
            drawLevelCtrl == null || football == null || collider == null ||
            collider.attachedRigidbody != football || !drawLevelCtrl.TryBeginSuccessPerformance())
        {
            return;
        }

        _ownsSuccessPerformance = true;
        StopSequence();
        SetGoalUIVisible(true);
        AudioManager.PlaySound("whistle");
        _goalSuccessCoroutine = StartCoroutine(CompleteGoalSuccessAfterDelay());
    }

    /// <summary>
    /// 第一笔有效画线完成后启动一次动作序列；后续笔画和重复通知不重新启动。
    /// 调用边界：关卡已初始化、未暂停或结算，且 Actor 与足球均已绑定时有效。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !result.IsFirstStroke ||
            _stage != ShotStage.Waiting || !CanAdvance() || actor.RB == null || football == null)
            return;

        CaptureInitialState();
        _targetX = actor.RB.position.x + Mathf.Max(0f, moveRightDistance);
        _stage = ShotStage.Moving;
    }

    /// <summary>
    /// 同实例重置时取消旧动画并恢复首笔前的角色及足球物理状态，等待新首笔。
    /// 正常 Reset/Replay 继续采用共享 Draw 的整关重建，不新增事件或重建入口。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying)
            return;

        CancelGoalSuccess();
        StopSequence();
        if (_hasInitialState && actor != null && actor.RB != null && football != null)
        {
            actor.RB.position = _initialActorPosition;
            actor.RB.linearVelocity = Vector2.zero;
            actor.SetFacingRight(_initialFacingRight);
            actor.ResumeMoving();
            actor.UpdateAnimation();
            football.bodyType = _initialFootballBodyType;
            football.position = _initialFootballPosition;
            football.rotation = _initialFootballRotation;
            football.linearVelocity = _initialFootballVelocity;
            football.angularVelocity = _initialFootballAngularVelocity;
        }
        _stage = ShotStage.Waiting;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        SetGoalUIVisible(false);
        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved += OnLevelResolved;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;
        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved -= OnLevelResolved;
        CancelGoalSuccess();
        StopSequence();
    }

    private void Update()
    {
        if (!Application.isPlaying || _stage == ShotStage.Waiting)
            return;

        if (drawLevelCtrl == null || drawLevelCtrl.IsOutcomeLocked || actor == null ||
            !actor.isActiveAndEnabled || actor.IsDied)
        {
            StopSequence();
            return;
        }

        if (_stage == ShotStage.Acting)
        {
            bool kickStarted = actor.TickFootballShoot(drawLevelCtrl.IsPaused);
            if (kickStarted && CanAdvance())
                KickFootball();
            if (!actor.IsFootballShootPlaying)
                _stage = ShotStage.Finished;
        }

        if (drawLevelCtrl.IsPaused)
        {
            ClearHorizontalMotion();
            return;
        }

        if (_stage == ShotStage.Moving)
        {
            actor.SetMoveInput(1f);
            actor.OnUpdate();
            if (actor.RB.position.x >= _targetX)
            {
                // 只校正已越过的终点 X，保留当前高度；受阻时继续真实物理行走，不穿过障碍。
                actor.RB.position = new Vector2(_targetX, actor.RB.position.y);
                ClearHorizontalMotion();
                actor.SetFacingRight(false);
                _stage = actor.PlayFootballShoot() ? ShotStage.Acting : ShotStage.Finished;
                if (_stage == ShotStage.Finished)
                    this.LogError("[DLevel79Ctrl] 蓄力或踢球动画不可用，未给足球施力。");
            }
        }
        else
            actor.OnUpdate();
    }

    private void OnLevelResolved(DrawLevelResult result)
    {
        _goalSuccessCoroutine = null;
        _ownsSuccessPerformance = false;
        SetGoalUIVisible(false);
        StopSequence();
    }

    #endregion

    #region Local Helpers

    private bool CanAdvance() => drawLevelCtrl != null && drawLevelCtrl.IsRuntimeReady &&
        !drawLevelCtrl.IsPaused && !drawLevelCtrl.IsOutcomeLocked && actor != null &&
        actor.isActiveAndEnabled && !actor.IsDied;

    private void KickFootball()
    {
        if (football == null)
            return;
        football.bodyType = RigidbodyType2D.Dynamic;
        football.AddForce(kickDirection.normalized * Mathf.Max(0f, kickImpulse), ForceMode2D.Impulse);
    }

    private IEnumerator CompleteGoalSuccessAfterDelay()
    {
        float elapsed = 0f;
        float delay = Mathf.Max(0f, goalVictoryDelay);
        while (elapsed < delay)
        {
            if (!_ownsSuccessPerformance || drawLevelCtrl == null)
                yield break;

            if (!drawLevelCtrl.IsPaused)
                elapsed += Time.deltaTime;
            yield return null;
        }

        _goalSuccessCoroutine = null;
        if (!_ownsSuccessPerformance || drawLevelCtrl == null)
            yield break;

        if (!drawLevelCtrl.CompleteSuccessPerformance(nameof(DLevel79Ctrl)))
            CancelGoalSuccess();
    }

    private void CancelGoalSuccess()
    {
        if (_goalSuccessCoroutine != null)
        {
            StopCoroutine(_goalSuccessCoroutine);
            _goalSuccessCoroutine = null;
        }

        SetGoalUIVisible(false);
        if (!_ownsSuccessPerformance)
            return;

        _ownsSuccessPerformance = false;
        drawLevelCtrl?.CancelSuccessPerformance();
    }

    private void SetGoalUIVisible(bool visible)
    {
        if (fifaGoal != null)
            fifaGoal.SetActive(visible);
    }

    private void ClearHorizontalMotion()
    {
        if (actor == null)
            return;
        actor.SetMoveInput(0f);
        if (actor.RB != null)
            actor.RB.linearVelocity = new Vector2(0f, actor.RB.linearVelocity.y);
    }

    private void StopSequence()
    {
        _stage = ShotStage.Finished;
        if (actor != null)
            actor.CancelFootballShoot();
        ClearHorizontalMotion();
    }

    private void CaptureInitialState()
    {
        if (_hasInitialState)
            return;
        _hasInitialState = true;
        _initialActorPosition = actor.RB.position;
        _initialFacingRight = actor.FaingRight;
        _initialFootballPosition = football.position;
        _initialFootballRotation = football.rotation;
        _initialFootballBodyType = football.bodyType;
        _initialFootballVelocity = football.linearVelocity;
        _initialFootballAngularVelocity = football.angularVelocity;
    }

    #endregion
}

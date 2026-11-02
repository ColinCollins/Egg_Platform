using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// version 1.0.0
/// 修改 football 下落逻辑，不用 rigibody2D 了，改用 DoTween，控制 football 移动到 endTarget 就可以了
/// </summary>
public class Level84Ctrl : MonoBehaviour, IEventSender, IDebuger
{
    private const float MissingActorShootFallbackDuration = 0.8f;
    private const float MissingActorSaveFallbackDuration = 0.35f;

    #region 序列化引用

    [Tooltip("Level84 本地足球选择面板，只在进入足球流程并完成准备表现后显示。")]
    [SerializeField] private GameObject Canvas;
    [Tooltip("Level84 选择面板内容根节点，用于入场完成后播放本地缩放展示；为空时只显示 Canvas，不播放缩放。")]
    [SerializeField] private Transform uiRoot;
    [Tooltip("高射按钮。点击后播放高射表现，随后进入 enemy 阻挡和失败重置。")]
    [SerializeField] private CustomButton highBtn;
    [Tooltip("直射按钮。点击后播放直射表现，随后进入 enemy 阻挡和失败重置。")]
    [SerializeField] private CustomButton strightBtn;

    [Tooltip("Level84 玩家角色引用；为空时会从 BaseLevelCtrl.Actor 自动解析。")]
    [SerializeField] private ActorCtrl actor;
    [Tooltip("Level84 配置好的阻挡敌人。缺失时只允许走防崩溃 fallback，不能视为验收通过。")]
    [SerializeField] private ActorCtrl enemy;
    [Tooltip("Level84 玩家角色足球表现控制器；未手动绑定时会从 actor 所在物体自动解析。")]
    [SerializeField] private Level84ActorCtrl actorSoccerCtrl;
    [Tooltip("Level84 敌人扑救表现控制器；未手动绑定时会从 enemy 所在物体自动解析。")]
    [SerializeField] private Level84ActorCtrl enemySoccerCtrl;
    [Tooltip("Level84 足球 Transform，用于入场、直射、高射和缺失 enemy fallback 表现。")]
    [SerializeField] private Transform football;

    [Header("Trigger Areas")]
    [Tooltip("Level84 足球流程进入触发区；为空时会从本地子物体 EnterTrigger 自动解析。")]
    [SerializeField] private TriggerAreaHandle enterTrigger;
    [Tooltip("Level84 足球成功区域；足球进入后优先等待一段时间再触发既有成功结算。")]
    [SerializeField] private TriggerAreaHandle successArea;

    [Header("Football")]
    [SerializeField] private ParticleSystem knock;
    // 足球刚体
    [SerializeField] private Rigidbody2D fbRigidbody;
    [Tooltip("足球入场前相对初始位置的下落起点偏移。")]
    [SerializeField] private Vector3 footballDropOffset = new Vector3(0f, 2.2f, 0f);
    [Tooltip("足球下落/弹跳到初始位置的持续时间。")]
    [SerializeField] private float footballDropDuration = 0.8f;
    [Tooltip("足球移动持续时间。")]
    [SerializeField] private float shotDuration = 0.8f;
    [Tooltip("高射足球跳跃高度。")]
    [SerializeField] private float highShotHeight = 2f;
    [Tooltip("直射表现中足球相对当前位置的目标偏移。")]
    [SerializeField] private Transform shotEndPoint;

    [SerializeField] private Vector2 blockForceDir = new Vector2(-.5f, .4f);
    [SerializeField] private float blockForce = 300f;

    [Header("Failure")]
    [Tooltip("enemy 进入阻挡位置的持续时间。")]
    [SerializeField] private float enemyEnterDuration = 0.35f;
    [Tooltip("enemy 完成阻挡动作后额外保持可见的时间，用于确保失败重置前能看清阻挡。")]
    [SerializeField] private float enemyBlockVisibleDuration = 0.35f;
    [Tooltip("enemy 阻挡完成到派发失败重置事件之间的等待时间。")]
    [SerializeField] private float failureResetDelay = 2f;

    [Header("Success")]
    [Tooltip("足球进入 SuccessArea 后等待多久触发本关的既有成功结算。")]
    [SerializeField] private float successDelay = 2f;

    [SerializeField] private GameObject successUI;

    #endregion

    #region 运行时状态

    private BaseLevelCtrl levelCtrl;
    private Vector3 footballInitialPosition;
    private Quaternion footballInitialRotation;
    private Vector3 footballInitialScale;
    private float footballInitialGravityScale;
    private Vector3 enemyInitialPosition;
    private Quaternion enemyInitialRotation;
    private bool enemyInitialActive;
    private int sessionId;
    private bool hasEntered;
    private bool isResolvingShot;
    private bool isResolvingSuccess;
    private bool isCompleted;

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        ResolveReferences();
        CacheInitialState();
        ResetPresentationState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheInitialState();
        ResetPresentationState();
        AddListeners();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        AddListeners();
    }

    // Update is called once per frame
    private void Update()
    {
        if (!Application.isPlaying || isCompleted)
        {
            return;
        }

        if (TryStartSuccessFlow())
        {
            return;
        }

        if (enemySoccerCtrl != null && enemySoccerCtrl.gameObject.activeInHierarchy)
        {
            enemySoccerCtrl.OnUpdate();
        }

        if (hasEntered || isResolvingShot || isResolvingSuccess)
        {
            return;
        }

        if (IsActorInsideEnterTrigger())
        {
            EnterSoccerFlow().Forget();
        }
    }

    private void OnDisable()
    {
        RemoveListeners();
        ResetPresentationState();
        SetActorInputLocked(false);
    }

    private void OnDestroy()
    {
        RemoveListeners();

        if (football != null)
        {
            football.DOKill();
        }

        if (enemy != null)
        {
            enemy.transform.DOKill();
        }
    }

    #endregion

    #region 对外入口

    /// <summary>
    /// 由 Level84 本地触发器或 UnityEvent 调用的足球流程入口；仅在运行时且当前尝试未结算时生效。
    /// </summary>
    public void Enter()
    {
        if (!Application.isPlaying || hasEntered || isResolvingShot || isCompleted)
        {
            return;
        }

        EnterSoccerFlow().Forget();
    }

    #endregion

    #region 事件绑定

    private void AddListeners()
    {
        if (highBtn != null)
        {
            highBtn.OnClick -= OnHighShotClick;
            highBtn.OnClick += OnHighShotClick;
        }

        if (strightBtn != null)
        {
            strightBtn.OnClick -= OnStraightShotClick;
            strightBtn.OnClick += OnStraightShotClick;
        }
    }

    private void RemoveListeners()
    {
        if (highBtn != null)
        {
            highBtn.OnClick -= OnHighShotClick;
        }

        if (strightBtn != null)
        {
            strightBtn.OnClick -= OnStraightShotClick;
        }
    }

    #endregion

    #region 足球流程

    public void ResetFootballPosition(Transform trans)
    {
        if (isCompleted || isResolvingSuccess)
            return;

        football.position = footballInitialPosition + footballDropOffset;
    }

    private void SwitchFootballState(bool isDynamic = false)
    {
        if (fbRigidbody == null)
        {
            return;
        }

        fbRigidbody.linearVelocity = Vector2.zero;
        fbRigidbody.angularVelocity = 0f;
        fbRigidbody.gravityScale = isDynamic ? footballInitialGravityScale : 0f;
        fbRigidbody.bodyType = isDynamic ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
    }

    private async UniTaskVoid EnterSoccerFlow()
    {
        if (hasEntered || isResolvingShot || isCompleted)
        {
            return;
        }

        hasEntered = true;
        int currentSession = ++sessionId;

        SetActorInputLocked(true);
        AudioManager.PlaySound("whistle");

        if (football != null)
        {
            football.DOKill();
            SwitchFootballState(false);
            football.position = footballInitialPosition;
            football.rotation = footballInitialRotation;
            football.localScale = footballInitialScale;
            football.gameObject.SetActive(true);

            await football
                   .DOMove(footballInitialPosition + footballDropOffset, Mathf.Max(0.01f, footballDropDuration))
                   .SetEase(Ease.OutBounce)
                   .SetLink(gameObject)
                   .AsyncWaitForCompletion();
        }

        if (!IsCurrentSession(currentSession))
        {
            return;
        }

        await UniTask.WaitForSeconds(.5f, cancellationToken: this.GetCancellationTokenOnDestroy());

        if (!IsCurrentSession(currentSession))
        {
            return;
        }

        AudioManager.PlaySound("grassCut");
        ShowChoicePanel(true);
        PlayChoicePanelShowAnimation();
    }

    private void OnHighShotClick(CustomButton _)
    {
        ResolveShot(false).Forget();
    }

    private void OnStraightShotClick(CustomButton _)
    {
        ResolveShot(true).Forget();
    }

    private async UniTaskVoid ResolveShot(bool isStraightShot)
    {
        if (!hasEntered || isResolvingShot || isCompleted)
        {
            return;
        }

        isResolvingShot = true;
        int currentSession = ++sessionId;
        ShowChoicePanel(false);
        SwitchFootballState(false);

        await PlayActorFootballShoot();

        if (!IsCurrentSession(currentSession))
        {
            return;
        }

        await PlayBlockedShot(currentSession, isStraightShot);
    }

    private async UniTask PlayBlockedShot(int currentSession, bool isStraightShot)
    {
        if (isStraightShot)
        {
            PlayStraightShotPresentation(currentSession).Forget();
        }
        else
        {
            PlayHighShotPresentation(currentSession).Forget();
        }

        if (!IsCurrentSession(currentSession))
        {
            return;
        }

        if (TryStartSuccessFlow())
        {
            return;
        }

        AudioManager.PlaySound("kickFootball");
        await UniTask.WaitForSeconds(shotDuration / 2f);


        await PlayEnemyBlock(currentSession);

        if (!IsCurrentSession(currentSession))
        {
            return;
        }

        if (TryStartSuccessFlow())
        {
            return;
        }

        if (IsCurrentSession(currentSession))
        {
            await UniTask.WaitForSeconds(Mathf.Max(0f, failureResetDelay), cancellationToken: this.GetCancellationTokenOnDestroy());
        }

        if (TryStartSuccessFlow())
        {
            return;
        }

        if (IsCurrentSession(currentSession))
        {
            CompleteFailure();
        }
    }

    /// <summary>
    /// 供 Level84 prefab 上的足球碰撞/触发回调调用，只播放本地足球落地音效；不改变胜负、存档或物理状态。
    /// </summary>
    public void PlayFootballTouchGround()
    {
        AudioManager.PlaySound("footballTouchGround");
    }

    #endregion

    #region 射门表现

    private async UniTask PlayStraightShotPresentation(int currentSession)
    {
        if (football == null)
        {
            return;
        }

        ResetFootballPosition(null);
        Vector3 target = GetShotTargetPosition();
        await football
            .DOMove(target, Mathf.Max(0.01f, shotDuration))
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .AsyncWaitForCompletion();
    }

    private async UniTask PlayHighShotPresentation(int currentSession)
    {
        if (football == null)
        {
            return;
        }

        ResetFootballPosition(null);
        Vector3 target = GetShotTargetPosition();
        await football
            .DOJump(target, Mathf.Max(0.01f, highShotHeight), 1, Mathf.Max(0.01f, shotDuration))
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .AsyncWaitForCompletion();
    }

    #endregion

    #region enemy 阻挡表现

    private async UniTask PlayEnemyBlock(int currentSession)
    {
        if (enemy == null)
        {
            return;
        }

        enemy.gameObject.SetActive(true);
        enemy.transform.DOKill();
        Vector3 targetPosition = GetEnemySavePosition();
        Vector3 startPosition = enemyInitialPosition;
        startPosition.x = targetPosition.x + 3f;
        enemy.transform.position = startPosition;
        enemy.transform.rotation = enemyInitialRotation;

        PlayEnemyGoalkeeperSave().Forget();

        await enemy.transform
            .DOMove(targetPosition, Mathf.Max(0.01f, enemyEnterDuration))
            .SetEase(Ease.OutQuad)
            .SetLink(enemy.gameObject)
            .AsyncWaitForCompletion();

        // 补充一个 knock 给 football
        if (fbRigidbody)
        {
            if (football != null)
            {
                football.DOKill();
            }

            SwitchFootballState(true);
            fbRigidbody.AddForce(blockForceDir * blockForce, ForceMode2D.Impulse);

            AudioManager.PlaySound("footballOut");
        }

        if (knock && football != null)
        {
            knock.transform.position = football.transform.position + new Vector3(-0.2f, 0, 0);
            knock.Play();

            AudioManager.PlaySound("trap");
        }

        if (!IsCurrentSession(currentSession))
        {
            return;
        }

        if (IsCurrentSession(currentSession) && enemyBlockVisibleDuration > 0f)
        {
            await UniTask.WaitForSeconds(Mathf.Max(0f, enemyBlockVisibleDuration), cancellationToken: this.GetCancellationTokenOnDestroy());
        }

        /* if (IsCurrentSession(currentSession))
        {
            RestoreEnemyPresentationState();
        } */
    }

    #endregion

    #region 角色动画

    private async UniTask PlayActorFootballShoot()
    {
        ResolveReferences();

        if (actorSoccerCtrl != null)
        {
            actorSoccerCtrl.PlayFootballShoot().Forget();
            await UniTask.WaitForSeconds(2f);
            if (knock)
            {
                knock.transform.position = actorSoccerCtrl.transform.position + new Vector3(0.5f, 0, 0);
                knock.Play();
            }

            return;
        }

        await PlayMissingActorControllerFallback(
            actor,
            MissingActorShootFallbackDuration,
            "[Level84Ctrl] Missing Level84ActorCtrl on actor; using crash-safety shoot fallback only.");
    }

    private async UniTask PlayEnemyGoalkeeperSave()
    {
        ResolveReferences();

        if (enemySoccerCtrl != null)
        {
            await enemySoccerCtrl.PlayGoalkeeperSave();
            return;
        }

        await PlayMissingActorControllerFallback(
            enemy,
            MissingActorSaveFallbackDuration,
            "[Level84Ctrl] Missing Level84ActorCtrl on enemy; using crash-safety save fallback only.");
    }

    private async UniTask PlayMissingActorControllerFallback(ActorCtrl target, float fallbackDuration, string warning)
    {
        if (target == null || target.IsDied)
        {
            return;
        }

        this.LogWarning(warning);
        StopActorMotionFallback(target);

        await UniTask.WaitForSeconds(Mathf.Max(0f, fallbackDuration), cancellationToken: this.GetCancellationTokenOnDestroy());

        if (!target.IsDied)
        {
            target.UpdateAnimation();
        }
    }

    private void StopActorMotionFallback(ActorCtrl target)
    {
        if (target == null)
        {
            return;
        }

        target.SetMoveInput(0f);
        target.StopMoving();

        if (target.RB != null)
        {
            target.RB.linearVelocity = Vector2.zero;
        }
    }

    private void StopLevel84SoccerAction(Level84ActorCtrl soccerCtrl, ActorCtrl fallbackTarget)
    {
        ActorCtrl target = soccerCtrl != null ? soccerCtrl : fallbackTarget;
        if (target == null || target.IsDied)
        {
            return;
        }

        target.SetMoveInput(0f);

        if (soccerCtrl != null)
        {
            bool wasPlaying = soccerCtrl.IsSoccerActionPlaying;
            soccerCtrl.StopSoccerAction();
            if (!wasPlaying)
            {
                soccerCtrl.UpdateAnimation();
            }
            return;
        }

        target.UpdateAnimation();
    }

    /// <summary>
    /// 重置 enemy 状态
    /// </summary>
    private void RestoreEnemyPresentationState()
    {
        if (enemy == null)
        {
            return;
        }

        enemy.transform.DOKill();
        StopLevel84SoccerAction(enemySoccerCtrl, enemy);
        enemy.transform.position = enemyInitialPosition;
        enemy.transform.rotation = enemyInitialRotation;
        enemy.gameObject.SetActive(enemyInitialActive);
    }

    #endregion

    #region 结算与状态判断

    public void EnterSuccessArea()
    {
        StartSuccessFlow().Forget();
    }

    public bool TryStartSuccessFlow()
    {
        if (!hasEntered || isResolvingSuccess || isCompleted || !IsFootballInsideSuccessArea())
        {
            return false;
        }

        StartSuccessFlow().Forget();
        return true;
    }

    private async UniTaskVoid StartSuccessFlow()
    {
        if (isResolvingSuccess || isCompleted || !IsFootballInsideSuccessArea())
        {
            return;
        }

        isResolvingSuccess = true;
        isResolvingShot = false;
        int currentSession = ++sessionId;
        ShowChoicePanel(false);

        if (football != null)
        {
            football.DOKill();
            SwitchFootballState(false);
        }

        if (enemy != null)
        {
            enemy.transform.DOKill();
            StopLevel84SoccerAction(enemySoccerCtrl, enemy);
        }

        successUI.gameObject.SetActive(true);
        AudioManager.PlaySound("footballAudienceCelebrate");

        await UniTask.WaitForSeconds(Mathf.Max(0f, successDelay), cancellationToken: this.GetCancellationTokenOnDestroy());

        if (IsCurrentSession(currentSession))
        {
            CompleteSuccess();
        }
    }

    private void CompleteSuccess()
    {
        if (isCompleted)
        {
            return;
        }

        isCompleted = true;
        isResolvingShot = false;
        isResolvingSuccess = false;
        ShowChoicePanel(false);
        ResolveReferences();
        levelCtrl?.SuccessStrightly();

        successUI.gameObject.SetActive(false);
    }

    private void CompleteFailure()
    {
        if (isCompleted)
        {
            return;
        }

        isCompleted = true;
        isResolvingShot = false;
        isResolvingSuccess = false;
        ShowChoicePanel(false);
        AudioManager.PlaySound("failed");
        this.DispatchEvent(Witness<GameResetEvent>._, GameResetType.Failed);
    }

    private bool IsActorInsideEnterTrigger()
    {
        if (enterTrigger == null || actor == null)
        {
            return false;
        }

        return IsInsideRect(enterTrigger.GetBoundaryRect(), actor.transform.position);
    }

    private bool IsFootballInsideSuccessArea()
    {
        if (successArea == null || football == null)
        {
            return false;
        }

        return IsInsideRect(successArea.GetBoundaryRect(), football.position);
    }

    private Vector3 GetEnemySavePosition()
    {
        if (shotEndPoint != null)
        {
            return shotEndPoint.position;
        }

        if (football != null)
        {
            return football.position;
        }

        return enemyInitialPosition;
    }

    private Vector3 GetShotTargetPosition()
    {
        if (shotEndPoint != null)
        {
            return shotEndPoint.position;
        }

        if (football != null)
        {
            this.LogWarning("[Level84Ctrl] Missing shotEndPoint; using current football position as crash-safety fallback.");
            return football.position;
        }

        this.LogWarning("[Level84Ctrl] Missing shotEndPoint and football; using level object position as crash-safety fallback.");
        return transform.position;
    }

    private bool IsCurrentSession(int currentSession)
    {
        return isActiveAndEnabled && sessionId == currentSession && !isCompleted;
    }

    private static bool IsInsideRect(Rect rect, Vector3 position)
    {
        return position.x >= rect.xMin && position.x <= rect.xMax &&
               position.y >= rect.yMin && position.y <= rect.yMax;
    }

    #endregion

    #region 本地引用与状态重置

    private void ShowChoicePanel(bool visible)
    {
        if (Canvas != null)
        {
            Canvas.SetActive(visible);
        }
    }

    private void PlayChoicePanelShowAnimation()
    {
        if (uiRoot == null)
        {
            return;
        }

        uiRoot.DOKill();
        uiRoot.localScale = Vector3.zero;
        uiRoot
            .DOScale(Vector3.one, 0.2f)
            .SetLink(gameObject);
    }

    private void SetActorInputLocked(bool locked)
    {
        ResolveReferences();

        if (levelCtrl != null)
        {
            levelCtrl.SetCtrlable(!locked);
        }

        if (actor != null)
        {
            actor.SetMoveInput(0f);
            if (locked)
            {
                actor.StopMoving();
            }
            else
            {
                actor.ResumeMoving();
            }
        }
    }

    private void CacheInitialState()
    {
        if (football != null)
        {
            footballInitialPosition = football.position;
            footballInitialRotation = football.rotation;
            footballInitialScale = football.localScale;
        }

        if (fbRigidbody != null)
        {
            footballInitialGravityScale = fbRigidbody.gravityScale;
        }

        if (enemy != null)
        {
            enemyInitialPosition = enemy.transform.position;
            enemyInitialRotation = enemy.transform.rotation;
            enemyInitialActive = enemy.gameObject.activeSelf;
        }
    }

    private void ResetPresentationState()
    {
        sessionId++;
        hasEntered = false;
        isResolvingShot = false;
        isResolvingSuccess = false;
        isCompleted = false;

        ShowChoicePanel(false);
        if (uiRoot != null)
        {
            uiRoot.DOKill();
            uiRoot.localScale = Vector3.one;
        }

        if (football != null)
        {
            football.DOKill();
            SwitchFootballState(false);
            football.position = footballInitialPosition;
            football.rotation = footballInitialRotation;
            football.localScale = footballInitialScale;
            football.gameObject.SetActive(true);
        }

        RestoreEnemyPresentationState();

        StopLevel84SoccerAction(actorSoccerCtrl, actor);
    }

    private void ResolveReferences()
    {
        if (levelCtrl == null)
        {
            levelCtrl = GetComponent<BaseLevelCtrl>();
            if (levelCtrl == null)
            {
                levelCtrl = GetComponentInParent<BaseLevelCtrl>();
            }
        }

        if (actor == null && levelCtrl != null)
        {
            actor = levelCtrl.Actor;
        }

        if (actor == null && actorSoccerCtrl != null)
        {
            actor = actorSoccerCtrl;
        }

        if (enemy == null && enemySoccerCtrl != null)
        {
            enemy = enemySoccerCtrl;
        }

        actorSoccerCtrl = ResolveLevel84ActorCtrl(actorSoccerCtrl, actor);
        enemySoccerCtrl = ResolveLevel84ActorCtrl(enemySoccerCtrl, enemy);

        if (enterTrigger == null)
        {
            enterTrigger = FindNamedChild<TriggerAreaHandle>("EnterTrigger");
        }

        if (successArea == null)
        {
            successArea = FindNamedChild<TriggerAreaHandle>("SuccessArea");
        }

        if (fbRigidbody == null && football != null)
        {
            fbRigidbody = football.GetComponent<Rigidbody2D>();
        }
    }

    private Level84ActorCtrl ResolveLevel84ActorCtrl(Level84ActorCtrl current, ActorCtrl source)
    {
        if (source == null)
        {
            return current;
        }

        if (current != null && current.gameObject == source.gameObject)
        {
            return current;
        }

        Level84ActorCtrl direct = source as Level84ActorCtrl;
        if (direct != null)
        {
            return direct;
        }

        return source.GetComponent<Level84ActorCtrl>();
    }

    private T FindNamedChild<T>(string childName) where T : Component
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
            {
                return children[i].GetComponent<T>();
            }
        }

        return null;
    }

    #endregion
}

using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// version 1.0.0
/// 扫描当前脚本，查看已有逻辑部分
/// 修改或者补充现有逻辑，EnterArea 后，SwitchObjActiveEvent 发送 true，显示 useBtn.useBtn 参考之前关卡看如何获取的。
/// 获取之后绑定功能，点击后进入踢球逻辑。
/// 
/// 进入 EnterArea 后，展示 Canvas，提供一个 arrow 在 arrowRotateRange 角度范围内进行 loop yoyo 的转动。
/// 根据角度转动，触发踢球时候 heightRange 范围，给 football 一个对应 new Vector2(1，height) 角度的方向的 Force
/// enemy 继续进入 block 逻辑，正常情况下，都是拦截成功，走正常拦截逻辑，等待游戏失败逻辑。
/// 
/// 存在一个 topObj 可以在 Y 轴拖拽，拖到超过指定位置 topTriggerPositionY 触发动画 OnTriggerTop, 移动到指定位置 topEndPosition2, 移动到指定位置后不可以再拖拽。
/// 对应的两个 sliderObj 的 height 需要根据 top 的位置计算增加 offset。offset 必须 > 0;
/// 
/// 场景中存在两个 ArrowRange，，触发 OnTriggerTop 的时候，同时播放效果 ArrowRange_green 的 fillAmount 变为 1.
/// 
/// 此时点击踢球按钮，触发踢球动画，给球一个指定方向的 Force，enemy 拦截失败，球触碰 collider2D 触发 enterCollision2D 等待 2s 游戏胜利。
/// 
/// version 1.1
/// 补充逻辑，触发踢球动画后，arrow 停止移动，保留当前 range 映射 height 角度。
/// 
/// version 2.0
/// PlayForceShot 改为用 DoTween 操作，利用 shotDuration 来记录移动时间。整理一下现有代码，无效逻辑移除，相关逻辑属性整合
/// sliderObj修改 height，不要改 scale
/// </summary>
public class Level87Ctrl : MonoBehaviour, IEventSender, IDebuger
{
    private const float MissingActorShootFallbackDuration = 0.8f;
    private const float MissingActorSaveFallbackDuration = 0.35f;
    private const float NormalShotHighForceMultiplier = 2f;
    private const float TopTriggeredShotForceMultiplier = 15f;
    private static readonly Vector2 DefaultShotHeightRange = new Vector2(0.8f, -0.45f);

    #region 序列化引用

    [Tooltip("Level87 Process 控制面板")]
    [SerializeField] private GameObject Canvas;
    [SerializeField] private RectTransform arror;
    [SerializeField] private Vector2 arrowRotateRange = new Vector2(70f, -50f);
    [Tooltip("Level87 GamePlayPanel use_btn 兜底引用；优先运行时从 CurrentGamePlayPanel 的 GamePanel_UseBtnCommonFeature 获取。")]
    [SerializeField] private CustomButton useBtn;
    [Tooltip("Level87 足球角度箭头；为空时按子物体名 Arrow_1 自动解析。")]
    [SerializeField] private Transform arrowTransform;
    [Tooltip("Level87 球框顶部可拖拽物；为空时按子物体名 top 自动解析。")]
    [SerializeField] private Transform topObj;
    [Tooltip("Level87 topObj 上的拖拽组件；用于触发到位后禁止继续拖拽。")]
    [SerializeField] private DragableItem topDrag;
    [Tooltip("Level87 topObj 本地 Y 轴触发阈值；超过该值后调用 OnTriggerTop，不使用旧目标或偏移兜底。")]
    [SerializeField] private float topTriggerPositionY = 3f;
    [Tooltip("Level87 topObj 触发后吸附到的显式 Transform；使用 localPosition，未绑定时不执行旧 fallback。")]
    [SerializeField] private Transform topEndPosition2;
    [Tooltip("Level87 topObj 到位后吸附到指定位置的时间。")]
    [SerializeField] private float topSnapDuration = 0.25f;
    [Tooltip("Level87 随 topObj 抬高而增加 SpriteRenderer.size.y 的两个 slider 对象；要求 SpriteRenderer 支持 Size，且不修改 Transform scale。")]
    [SerializeField] private SpriteRenderer[] sliderObjs;
    [Tooltip("Level87 球框网格 SpriteRenderer 数组；随 topObj 上移从 size.y = 0 开始增长，未绑定时按子物体名 grid / grid (*) 自动解析。")]
    [SerializeField] private SpriteRenderer[] grids;
    [Tooltip("Level87 slider 高度随 topObj Y 轴正向偏移增加的倍率，最终偏移会强制不小于 0。")]
    [SerializeField] private float sliderHeightOffsetScale = 1f;
    [Tooltip("Level87 绿色角度范围图；触发 OnTriggerTop 后 fillAmount 变为 1。")]
    [SerializeField] private Image arrowRangeGreen;
    [Tooltip("Level87 成功射门目标点；仅作为视觉/调试参考，不参与成功判定。")]
    [SerializeField] private Transform successTarget;
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

    [Header("Football")]
    // 足球射击角度
    [SerializeField] private Vector2 shotHeightRange;
    [Tooltip("Level87 use_btn 触发踢球时 football 沿冻结方向移动的距离/强度；v2.0 主射门不再作为刚体冲量使用。")]
    [SerializeField] private float shotForce = 18f;
    [Tooltip("Level87 top 未触发的普通射门 height 超过该值时，本次 football 移动距离为 shotForce * 2，并跳过 enemyBlock 表现。")]
    [SerializeField] private float normalShotSkipEnemyBlockHeight = 0.25f;
    [Tooltip("Level87 箭头在 arrowRotateRange 内单向转动时间，循环使用 yoyo。")]
    [SerializeField] private float arrowRotateDuration = 0.8f;
    [SerializeField] private ParticleSystem knock;
    // 足球刚体
    [SerializeField] private Rigidbody2D fbRigidbody;
    [Tooltip("足球入场前相对初始位置的下落起点偏移。")]
    [SerializeField] private Vector3 footballDropOffset = new Vector3(0f, 2.2f, 0f);
    [Tooltip("足球下落/弹跳到初始位置的持续时间。")]
    [SerializeField] private float footballDropDuration = 0.8f;
    [Tooltip("足球移动持续时间。")]
    [SerializeField] private float shotDuration = 0.8f;

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
    [Tooltip("Level87 top 触发后的成功射门方向；为零时继续使用点击瞬间冻结的箭头方向。")]
    [SerializeField] private Vector2 topTriggeredShotDirection = Vector2.zero;
    [Tooltip("Level87 top 触发成功后 enemy blockFailure 表现移动到的终点；为空时按子物体名 endPoint 自动解析。")]
    [SerializeField] private Transform endPoint;
    [Tooltip("足球进入 SuccessArea 后等待多久触发本关的既有成功结算。")]
    [SerializeField] private float successDelay = 2f;
    [Tooltip("Level87 成功等待阶段显示的本地成功 UI/Canvas；只在成功流程启动后显示，重置和失败路径会隐藏。")]
    [SerializeField] private GameObject successUI;

    #endregion

    #region 运行时状态

    private BaseLevelCtrl levelCtrl;
    private Vector3 footballInitialPosition;
    private Quaternion footballInitialRotation;
    private Vector3 footballInitialScale;
    private Vector3 enemyInitialPosition;
    private Quaternion enemyInitialRotation;
    private Vector3 topInitialLocalPosition;
    private Vector3 topTargetLocalPosition;
    private float[] sliderInitialHeights;
    private GamePanel_UseBtnCommonFeature commonFeature;
    private CustomButton resolvedUseButton;
    private Tween arrowTween;
    private Tween topTween;
    private int sessionId;
    private bool hasEntered;
    private bool isResolvingShot;
    private bool isResolvingSuccess;
    private bool isCompleted;
    private bool hasStarted;
    private bool useButtonSubscribed;
    private bool hasTopInitialState;
    private bool topTriggered;
    private bool isSuccessShotActive;
    private bool successAreaEntered;
    private bool hasFrozenShotDirection;
    private Vector2 frozenShotDirection = Vector2.right;
    private float frozenShotHeight;

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
        StartArrowLoop();
        if (hasStarted)
        {
            RegisterUseButton(false);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        hasStarted = true;
        RegisterUseButton();
        StartArrowLoop();
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

        if (enemySoccerCtrl)
        {
            enemySoccerCtrl.OnUpdate();
        }

        OnTopDragUpdate();

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
        UnregisterUseButton();
        StopArrowLoop();
        topTween?.Kill();
        topTween = null;
        ResetPresentationState();
        SetActorInputLocked(false);
    }

    private void OnDestroy()
    {
        UnregisterUseButton();
        StopArrowLoop();
        topTween?.Kill();
        topTween = null;

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

    private void RegisterUseButton(bool hideButton = true)
    {
        UnregisterUseButton();
        ResolveReferences();

        resolvedUseButton = ResolveUseButton();
        if (resolvedUseButton == null)
        {
            this.LogWarning("[Level87Ctrl] Use button binding skipped: GamePanel_UseBtnCommonFeature.Use_btn is missing.");
            return;
        }

        resolvedUseButton.OnClick += OnUseButtonClick;
        resolvedUseButton.Interactable = !isCompleted;
        useButtonSubscribed = true;

        if (hideButton)
        {
            HideUseButton();
        }
    }

    private void UnregisterUseButton()
    {
        if (useButtonSubscribed && resolvedUseButton != null)
        {
            resolvedUseButton.OnClick -= OnUseButtonClick;
        }

        useButtonSubscribed = false;
        resolvedUseButton = null;
        commonFeature = null;
    }

    private CustomButton ResolveUseButton()
    {
        if (PlayCtrl.Instance != null && PlayCtrl.Instance.CurrentGamePlayPanel != null)
        {
            commonFeature = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<GamePanel_UseBtnCommonFeature>();
            if (commonFeature != null && commonFeature.Use_btn != null)
            {
                return commonFeature.Use_btn;
            }
        }

        return useBtn;
    }

    private void OnUseButtonClick(CustomButton _)
    {
        ResolveShot().Forget();
    }


    #endregion

    #region 足球流程

    private void SwitchFootballState(bool isDynamic = false)
    {
        if (fbRigidbody == null)
        {
            return;
        }

        fbRigidbody.linearVelocity = Vector2.zero;
        fbRigidbody.angularVelocity = 0f;
        // fbRigidbody.bodyType = isDynamic ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
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
        fbRigidbody.bodyType = RigidbodyType2D.Dynamic;
        
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

        AudioManager.PlaySound("grassCut");
        ShowChoicePanel(true);
        ShowUseButton();
    }

    private async UniTaskVoid ResolveShot()
    {
        if (!hasEntered || isResolvingShot || isCompleted)
        {
            return;
        }

        isResolvingShot = true;
        int currentSession = ++sessionId;
        HideUseButton();
        ShowChoicePanel(true);
        SwitchFootballState(false);
        FreezeShotDirection();

        await PlayActorFootballShoot();

        if (!IsCurrentSession(currentSession))
        {
            return;
        }

        if (topTriggered)
        {
            await PlayUnlockedShot(currentSession);
            return;
        }

        await PlayBlockedShot(currentSession);
    }

    private async UniTask PlayBlockedShot(int currentSession)
    {
        bool skipEnemyBlock = ShouldSkipEnemyBlockForNormalShot();
        PlayForceShot(skipEnemyBlock ? NormalShotHighForceMultiplier : 1f);

        if (!IsCurrentSession(currentSession))
        {
            return;
        }

        if (TryStartSuccessFlow())
        {
            return;
        }

        if (skipEnemyBlock)
        {
            await WaitNormalShotFailure(currentSession, shotDuration);
            return;
        }

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

    private async UniTask WaitNormalShotFailure(int currentSession, float extraDelay)
    {
        if (IsCurrentSession(currentSession))
        {
            await UniTask.WaitForSeconds(Mathf.Max(0f, extraDelay), cancellationToken: this.GetCancellationTokenOnDestroy());
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

    private async UniTask PlayUnlockedShot(int currentSession)
    {
        isSuccessShotActive = true;
        successAreaEntered = false;
        PlayTopTriggeredForceShot();
        PlayEnemyBlockFailure(currentSession).Forget();

        while (IsCurrentSession(currentSession) && isSuccessShotActive && !successAreaEntered)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
        }

        if (IsCurrentSession(currentSession) && successAreaEntered)
        {
            StartSuccessFlow().Forget();
        }
    }

    /// <summary>
    /// 供 Level87 prefab 上的足球触地触发器调用，只播放本地足球落地音效；不改变胜负、存档、事件或物理状态。
    /// </summary>
    public void PlayFootballTouchGround()
    {
        AudioManager.PlaySound("footballTouchGround");
    }

    #endregion

    #region 射门表现

    private Vector3 footballTargetPosition;

    private void PlayForceShot()
    {
        PlayForceShot(1f);
    }

    private void PlayForceShot(float forceMultiplier)
    {
        if (football == null)
        {
            return;
        }

        AudioManager.PlaySound("kickFootball");
        football.DOKill();
        Destroy(football.GetComponent<LayerCollisionDetector>());
        SwitchFootballState(false);

        Vector2 shotDirection = GetShotDirection();
        Vector3 moveDirection = new Vector3(shotDirection.x, shotDirection.y, 0f);
        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            moveDirection = Vector3.right;
        }

        float shotDistance = Mathf.Max(0f, shotForce) * Mathf.Max(0f, forceMultiplier);
        footballTargetPosition = football.position + moveDirection.normalized * shotDistance;
        football
            .DOMove(footballTargetPosition, Mathf.Max(0.01f, shotDuration))
            .SetEase(Ease.Linear)
            .SetLink(gameObject);
    }

    private void PlayTopTriggeredForceShot()
    {
        if (football == null)
        {
            return;
        }

        AudioManager.PlaySound("kickFootball");
        football.DOKill();

        Vector2 shotDirection = GetShotDirection();
        Vector2 forceDirection = shotDirection.sqrMagnitude <= 0.0001f ? Vector2.right : shotDirection.normalized;
        float force = Mathf.Max(0f, shotForce) * TopTriggeredShotForceMultiplier;
        footballTargetPosition = football.position + new Vector3(forceDirection.x, forceDirection.y, 0f) * force;

        if (fbRigidbody == null)
        {
            this.LogWarning("[Level87Ctrl] Missing football Rigidbody2D; top triggered AddForce shot skipped.");
            return;
        }

        SwitchFootballState(true);
        fbRigidbody.AddForce(forceDirection * force, ForceMode2D.Impulse);
    }

    #endregion

    #region enemy 阻挡表现

    private async UniTaskVoid PlayEnemyBlockFailure(int currentSession)
    {
        if (enemy == null)
        {
            return;
        }

        enemy.gameObject.SetActive(true);
        enemy.transform.DOKill();
        Vector3 targetPosition = GetEnemyBlockFailurePosition();
        Vector3 startPosition = enemyInitialPosition;
        startPosition.x = targetPosition.x + 3f;
        enemy.transform.position = startPosition;
        enemy.transform.rotation = enemyInitialRotation;

        PlayEnemyBlockFailureAnimation().Forget();

        await enemy.transform
            .DOMove(targetPosition, Mathf.Max(0.01f, enemyEnterDuration))
            .SetEase(Ease.OutQuad)
            .SetLink(enemy.gameObject)
            .AsyncWaitForCompletion();

        if (!IsCurrentSession(currentSession))
        {
            return;
        }

        if (enemyBlockVisibleDuration > 0f)
        {
            await UniTask.WaitForSeconds(Mathf.Max(0f, enemyBlockVisibleDuration), cancellationToken: this.GetCancellationTokenOnDestroy());
        }
    }

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

        if (IsCurrentSession(currentSession))
        {
            RestoreEnemyPresentationState();
        }
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
            "[Level87Ctrl] Missing Level84ActorCtrl on actor; using crash-safety shoot fallback only.");
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
            "[Level87Ctrl] Missing Level84ActorCtrl on enemy; using crash-safety save fallback only.");
    }

    private async UniTask PlayEnemyBlockFailureAnimation()
    {
        ResolveReferences();

        if (enemySoccerCtrl != null)
        {
            await enemySoccerCtrl.PlayBlockFailure();
            return;
        }

        await PlayMissingActorControllerFallback(
            enemy,
            MissingActorSaveFallbackDuration,
            "[Level87Ctrl] Missing Level84ActorCtrl on enemy; using crash-safety block failure fallback only.");
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

    private void StopSoccerAction(Level84ActorCtrl soccerCtrl, ActorCtrl fallbackTarget)
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
        StopSoccerAction(enemySoccerCtrl, enemy);
    }

    #endregion

    #region 结算与状态判断

    /// <summary>
    /// Level87 成功碰撞入口；仅在 topObj 已到位且本次射门处于成功候选阶段时生效。
    /// </summary>
    public void EnterCollision2D(Collider2D collider)
    {
        if (!CanAcceptSuccessCollision(collider))
        {
            return;
        }

        successAreaEntered = true;
        TryStartSuccessFlow();
    }

    /// <summary>
    /// Level87 碰撞成功入口兼容方法；用于匹配注释里小写 enterCollision2D 的 UnityEvent 绑定。
    /// </summary>
    public void enterCollision2D(Collider2D collider)
    {
        EnterCollision2D(collider);
    }

    /// <summary>
    /// Level87 物理碰撞成功入口；用于 LayerCollisionDetector 传入 Collision2D 时转接到统一成功标记。
    /// </summary>
    public void EnterCollision2D(Collision2D collision)
    {
        if (collision == null)
        {
            EnterCollision2D((Collider2D)null);
            return;
        }

        if (CanAcceptSuccessCollision(collision.collider) ||
            CanAcceptSuccessCollision(collision.otherCollider))
        {
            successAreaEntered = true;
        }
    }

    /// <summary>
    /// Level87 物理碰撞成功入口兼容方法；用于匹配注释里小写 enterCollision2D 的 Collision2D 绑定。
    /// </summary>
    public void enterCollision2D(Collision2D collision)
    {
        EnterCollision2D(collision);
    }

    /// <summary>
    /// 兼容已有 TriggerAreaHandle / UnityEvent 的成功入口；只作为成功射门阶段的碰撞标记使用。
    /// </summary>
    public void EnterSuccessArea()
    {
        if (!CanAcceptSuccessCollision(null))
        {
            return;
        }

        successAreaEntered = true;
    }

    public bool TryStartSuccessFlow()
    {
        if (!hasEntered || isResolvingSuccess || isCompleted || !topTriggered || !isSuccessShotActive)
        {
            return false;
        }

        if (!successAreaEntered)
        {
            return false;
        }

        successAreaEntered = true;
        StartSuccessFlow().Forget();
        return true;
    }

    private async UniTaskVoid StartSuccessFlow()
    {
        if (isResolvingSuccess || isCompleted || !topTriggered)
        {
            return;
        }

        isResolvingSuccess = true;
        isResolvingShot = false;
        isSuccessShotActive = false;
        int currentSession = ++sessionId;
        ShowChoicePanel(false);
        HideUseButton();

        if (football != null)
        {
            football.DOKill();
            SwitchFootballState(false);
        }

        if (enemy != null)
        {
            enemy.transform.DOKill();
            StopSoccerAction(enemySoccerCtrl, enemy);
        }

        SetSuccessUIVisible(true);
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
        isSuccessShotActive = false;
        ShowChoicePanel(false);
        HideUseButton();
        ResolveReferences();
        levelCtrl?.SuccessStrightly();

        SetSuccessUIVisible(false);
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
        isSuccessShotActive = false;
        ShowChoicePanel(false);
        HideUseButton();
        SetSuccessUIVisible(false);

        if (football != null)
        {
            football.DOKill();
            SwitchFootballState(false);
        }

        if (enemy != null)
        {
            enemy.transform.DOKill();
            StopSoccerAction(enemySoccerCtrl, enemy);
        }

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

    private Vector3 GetEnemySavePosition()
    {
        return footballTargetPosition;
    }

    private Vector3 GetEnemyBlockFailurePosition()
    {
        ResolveReferences();

        if (endPoint != null)
        {
            return endPoint.position;
        }

        this.LogWarning("[Level87Ctrl] Missing endPoint; using football target position as block failure fallback.");
        return footballTargetPosition;
    }

    private bool CanAcceptSuccessCollision(Collider2D collider)
    {
        if (!topTriggered || !isSuccessShotActive || isResolvingSuccess || isCompleted)
        {
            return false;
        }

        if (collider == null || football == null)
        {
            return true;
        }

        return collider.transform == football || collider.transform.IsChildOf(football);
    }

    private Vector2 GetCurrentShotDirection()
    {
        float height = GetCurrentShotHeight();
        Vector2 dir = new Vector2(1f, height);
        if (dir.sqrMagnitude <= 0.0001f)
        {
            return Vector2.right;
        }

        return dir.normalized;
    }

    private float GetCurrentShotHeight()
    {
        Transform arrow = arrowTransform != null ? arrowTransform : arror;
        float angle = arrow != null ? arrow.eulerAngles.z : 0f;
        float normalized = Mathf.InverseLerp(arrowRotateRange.x, arrowRotateRange.y, NormalizeAngle180(angle));
        Vector2 heightRange = GetEffectiveShotHeightRange();
        return Mathf.Lerp(heightRange.x, heightRange.y, normalized);
    }

    private Vector2 GetShotDirection()
    {
        if (topTriggered && topTriggeredShotDirection.sqrMagnitude > 0.0001f)
        {
            return topTriggeredShotDirection.normalized;
        }

        return hasFrozenShotDirection ? frozenShotDirection : GetCurrentShotDirection();
    }

    private void FreezeShotDirection()
    {
        frozenShotHeight = GetCurrentShotHeight();
        Vector2 dir = new Vector2(1f, frozenShotHeight);
        frozenShotDirection = dir.sqrMagnitude <= 0.0001f ? Vector2.right : dir.normalized;
        hasFrozenShotDirection = true;
        StopArrowLoop();
    }

    private void ClearFrozenShotDirection()
    {
        hasFrozenShotDirection = false;
        frozenShotDirection = Vector2.right;
        frozenShotHeight = 0f;
    }

    private bool ShouldSkipEnemyBlockForNormalShot()
    {
        if (topTriggered)
        {
            return false;
        }

        float shotHeight = hasFrozenShotDirection ? frozenShotHeight : GetCurrentShotHeight();
        return shotHeight > normalShotSkipEnemyBlockHeight;
    }

    private Vector2 GetEffectiveShotHeightRange()
    {
        return Mathf.Approximately(shotHeightRange.x, 0f) && Mathf.Approximately(shotHeightRange.y, 0f)
            ? DefaultShotHeightRange
            : shotHeightRange;
    }

    private static float NormalizeAngle180(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }

        if (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
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

    private void ShowUseButton()
    {
        this.DispatchEvent(Witness<SwitchObjActiveEvent>._, true);
        CustomButton target = resolvedUseButton != null ? resolvedUseButton : ResolveUseButton();
        if (target == null)
        {
            RegisterUseButton(false);
            target = resolvedUseButton;
        }

        if (target == null)
        {
            return;
        }

        target.Interactable = !isCompleted;
        target.gameObject.SetActive(true);
        target.transform.DOKill();
        target.transform.localScale = Vector3.one * 0.5f;
        target.transform.DOScale(Vector3.one, 0.1f).SetLink(target.gameObject);
    }

    private void HideUseButton()
    {
        this.DispatchEvent(Witness<SwitchObjActiveEvent>._, false);
        CustomButton target = resolvedUseButton != null ? resolvedUseButton : ResolveUseButton();
        if (target == null)
        {
            return;
        }

        target.Interactable = false;
        target.transform.DOKill();
        target.transform.localScale = Vector3.one;
        if (!target.gameObject.activeSelf)
        {
            target.transform.localScale = Vector3.zero;
            return;
        }

        target.transform.DOScale(Vector3.zero, 0.1f)
            .SetLink(target.gameObject)
            .OnComplete(() =>
            {
                if (target != null)
                {
                    target.gameObject.SetActive(false);
                }
            });
    }

    private void StartArrowLoop()
    {
        Transform arrow = arrowTransform != null ? arrowTransform : arror;
        if (arrow == null)
        {
            return;
        }

        arrowTween?.Kill();
        arrow.localRotation = Quaternion.Euler(0f, 0f, arrowRotateRange.x);
        arrowTween = arrow
            .DOLocalRotate(new Vector3(0f, 0f, arrowRotateRange.y), Mathf.Max(0.01f, arrowRotateDuration), RotateMode.Fast)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetLink(gameObject);
    }

    private void StopArrowLoop()
    {
        arrowTween?.Kill();
        arrowTween = null;
    }

    private void OnTopDragUpdate()
    {
        if (topTriggered || topObj == null)
        {
            return;
        }

        float offset = Mathf.Max(0f, topObj.localPosition.y - topInitialLocalPosition.y);
        ApplySliderOffset(offset);

        if (topObj.localPosition.y >= topTriggerPositionY)
        {
            OnTriggerTop();
        }
    }

    /// <summary>
    /// Level87 topObj 到位入口；允许 DragableItem 事件或脚本检测调用，调用后会锁定 topObj 并解锁成功射门。
    /// </summary>
    public void OnTriggerTop()
    {
        if (topTriggered || topObj == null)
        {
            return;
        }

        if (topEndPosition2 == null)
        {
            this.LogWarning("[Level87Ctrl] topEndPosition2 is missing; top trigger ignored because no explicit snap target is configured.");
            return;
        }

        topTargetLocalPosition = topEndPosition2.localPosition;
        topTriggered = true;
        topTween?.Kill();
        topTween = topObj
            .DOLocalMove(topTargetLocalPosition, Mathf.Max(0.01f, topSnapDuration))
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject)
            .OnUpdate(() =>
            {
                float offset = Mathf.Max(0f, topObj.localPosition.y - topInitialLocalPosition.y);
                ApplySliderOffset(offset);
            })
            .OnComplete(() =>
            {
                topObj.localPosition = topTargetLocalPosition;
                float offset = Mathf.Max(0f, topObj.localPosition.y - topInitialLocalPosition.y);
                ApplySliderOffset(offset);
            });

        SetTopDragEnabled(false);
        SetArrowRangeGreenFill(1f);
    }

    private void ApplySliderOffset(float offset)
    {
        float scaledOffset = Mathf.Max(0f, offset * Mathf.Max(0f, sliderHeightOffsetScale));
        if (sliderObjs != null && sliderInitialHeights != null)
        {
            for (int i = 0; i < sliderObjs.Length; i++)
            {
                if (sliderObjs[i] == null || i >= sliderInitialHeights.Length)
                {
                    continue;
                }

                Vector2 size = sliderObjs[i].size;
                size.y = Mathf.Max(0.01f, sliderInitialHeights[i] + scaledOffset);
                sliderObjs[i].size = size;
            }
        }

        var oh = scaledOffset > 0.1f ? 0.3f : 0;
        SetGridHeight(scaledOffset + oh);
    }

    private void SetTopDragEnabled(bool enabled)
    {
        if (topDrag != null)
        {
            topDrag.SetEnableDrag(enabled);
        }
    }

    private void SetArrowRangeGreenFill(float value)
    {
        if (arrowRangeGreen != null)
        {
            arrowRangeGreen.fillAmount = Mathf.Clamp01(value);
        }
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

        if (enemy != null)
        {
            enemyInitialPosition = enemy.transform.position;
            enemyInitialRotation = enemy.transform.rotation;
        }

        if (topObj != null && !hasTopInitialState)
        {
            topInitialLocalPosition = topObj.localPosition;
            topTargetLocalPosition = topEndPosition2 != null ? topEndPosition2.localPosition : topInitialLocalPosition;
            hasTopInitialState = true;
        }

        CacheSliderInitialState();
    }

    private void ResetPresentationState()
    {
        sessionId++;
        hasEntered = false;
        isResolvingShot = false;
        isResolvingSuccess = false;
        isCompleted = false;
        isSuccessShotActive = false;
        successAreaEntered = false;
        topTriggered = false;
        ClearFrozenShotDirection();

        ShowChoicePanel(false);
        HideUseButton();
        SetSuccessUIVisible(false);
        SetArrowRangeGreenFill(0f);

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

        StopSoccerAction(actorSoccerCtrl, actor);

        if (topObj != null && hasTopInitialState)
        {
            topTween?.Kill();
            topObj.localPosition = topInitialLocalPosition;
            SetTopDragEnabled(true);
        }

        RestoreSliderInitialState();
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

        if (arrowTransform == null)
        {
            arrowTransform = FindNamedChild<Transform>("Arrow_1");
        }

        if (arror == null && arrowTransform != null)
        {
            arror = arrowTransform as RectTransform;
        }

        if (topObj == null)
        {
            topObj = FindNamedChild<Transform>("top");
        }

        if (topDrag == null && topObj != null)
        {
            topDrag = topObj.GetComponent<DragableItem>();
        }

        if (arrowRangeGreen == null)
        {
            arrowRangeGreen = FindNamedChild<Image>("ArrowRange_green");
        }

        if (successUI == null)
        {
            Transform successUITransform =
                FindNamedChild<Transform>("successUI") ??
                FindNamedChild<Transform>("SuccessUI") ??
                FindNamedChild<Transform>("successCanvas") ??
                FindNamedChild<Transform>("SuccessCanvas");
            if (successUITransform != null)
            {
                successUI = successUITransform.gameObject;
            }
        }

        ResolveDefaultGrids();

        if (football == null)
        {
            football = FindNamedChild<Transform>("football");
        }

        if (successTarget == null)
        {
            successTarget = FindNamedChild<Transform>("qiukuang");
        }

        if (endPoint == null)
        {
            endPoint = FindNamedChild<Transform>("endPoint");
        }

        actorSoccerCtrl = ResolveLevel84ActorCtrl(actorSoccerCtrl, actor);
        enemySoccerCtrl = ResolveLevel84ActorCtrl(enemySoccerCtrl, enemy);

        if (enterTrigger == null)
        {
            enterTrigger = FindNamedChild<TriggerAreaHandle>("EnterTrigger");
        }

        if (fbRigidbody == null && football != null)
        {
            fbRigidbody = football.GetComponent<Rigidbody2D>();
        }

        ResolveDefaultSliders();
    }

    private void SetSuccessUIVisible(bool visible)
    {
        if (successUI != null)
        {
            successUI.SetActive(visible);
        }
    }

    private void CacheSliderInitialState()
    {
        if (sliderObjs == null || sliderObjs.Length <= 0 || sliderInitialHeights != null)
        {
            return;
        }

        sliderInitialHeights = new float[sliderObjs.Length];
        for (int i = 0; i < sliderObjs.Length; i++)
        {
            sliderInitialHeights[i] = sliderObjs[i] != null ? sliderObjs[i].size.y : 0f;
        }
    }

    private void RestoreSliderInitialState()
    {
        if (sliderObjs != null && sliderInitialHeights != null)
        {
            for (int i = 0; i < sliderObjs.Length && i < sliderInitialHeights.Length; i++)
            {
                if (sliderObjs[i] != null)
                {
                    Vector2 size = sliderObjs[i].size;
                    size.y = Mathf.Max(0.01f, sliderInitialHeights[i]);
                    sliderObjs[i].size = size;
                }
            }
        }

        SetGridHeight(0f);
    }

    private void SetGridHeight(float height)
    {
        if (grids == null || grids.Length <= 0)
        {
            return;
        }

        float safeHeight = Mathf.Max(0f, height);
        for (int i = 0; i < grids.Length; i++)
        {
            if (grids[i] == null)
            {
                continue;
            }

            Vector2 size = grids[i].size;
            size.y = safeHeight;
            grids[i].size = size;
        }
    }

    private void ResolveDefaultGrids()
    {
        if (grids != null && grids.Length > 0)
        {
            return;
        }

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        int matchCount = 0;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (IsGridRenderer(spriteRenderers[i]))
            {
                matchCount++;
            }
        }

        if (matchCount <= 0)
        {
            return;
        }

        grids = new SpriteRenderer[matchCount];
        int writeIndex = 0;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (IsGridRenderer(spriteRenderers[i]))
            {
                grids[writeIndex++] = spriteRenderers[i];
            }
        }
    }

    private static bool IsGridRenderer(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null)
        {
            return false;
        }

        string objectName = spriteRenderer.name;
        return objectName == "grid" || objectName.StartsWith("grid (");
    }

    private void ResolveDefaultSliders()
    {
        if (sliderObjs != null && sliderObjs.Length > 0)
        {
            return;
        }

        SpriteRenderer left = FindNamedChild<SpriteRenderer>("1");
        SpriteRenderer right = FindNamedChild<SpriteRenderer>("1 (1)");
        if (left == null && right == null)
        {
            return;
        }

        sliderObjs = left != null && right != null
            ? new[] { left, right }
            : new[] { left != null ? left : right };
    }

    private static Vector3 ToLocalPosition(Transform parent, Vector3 worldPosition)
    {
        return parent != null ? parent.InverseTransformPoint(worldPosition) : worldPosition;
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

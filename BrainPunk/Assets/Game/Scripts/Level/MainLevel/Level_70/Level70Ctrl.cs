using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///  version 1.0
/// 控制两个部分
/// 1. actor，提供一个 API Trigger2D 触发，触发后，Actor 进入 control 状态不能移动。变为控制 catch 运动
/// 2. catch 控制，方向左右控制 catch 移动。Jump 按钮切换为 Catch 按钮。catch 是抓娃娃机的一个钩子。
/// 缓慢下落，catchConllider 触碰到 triggerItem 或者 Player 的时候触发抓捕逻辑。item 自动进入 area position。然后跟随 cacth 上移到 dropPoint.position.y 高度，然后平移到指定 dropPoint位置。
/// isOpen 打开钩子。对应物品自然下落。
/// 旧版设计曾使用 playing / complete 两阶段切换抓钩范围；version 3.0 起不再依赖 complete 状态，抓钩直接使用完整行程。
/// 提供一个心得 Trigger2D API，触发后， actor 可以切换 controlhanlde 模式，恢复移动。
///
/// version 3.0
/// catch 水平移动改为按完整行程计算的加速度模式；左边界超过速度阈值时破墙，其他边界碰撞播放对应失败效果。
/// </summary>
public class Level70Ctrl : MonoBehaviour, IDebuger
{
    #region Inspector

    // Level70 专用 actor 引用；仅用于本关抓钩控制锁定、搬运和恢复，不修改共享 ActorCtrl。
    [SerializeField] private Level70ActorCtrl actor;

    // 当前关卡根控制器引用；为空时运行时从本对象兜底获取，用于本关临时关闭共享左右移动输入。
    [SerializeField] private BaseLevelCtrl levelCtrl;

    // Level70 本关 catch 机械控制器；只接收本控制器委托的输入，不订阅全局事件。
    [SerializeField] private Level70HookCtrl hookCtrl;

    // 抓钩控制入口触发器；为空时运行时查找 controller 节点，只在本关注册 EnterHookControl 回调。
    [SerializeField] private OnTrigger2DHandle hookControlTrigger;

    // Level70 ball 初始化目标列表；仅在进入/重置时按 itemSprites 索引刷新，运行中不逐帧改写。
    [SerializeField] private List<Level70BallItemCtrl> balls;

    // Level70 ball 内容 sprite 列表；多余项会忽略，缺失或空 sprite 会让对应 ball 保持空内容。
    [SerializeField] private List<Sprite> itemSprites;

    [Header("wallCollision")]
    // 左边界高速撞击破碎前的完整墙体显示；仅由 Level70 墙体破碎表现切换显隐。
    [SerializeField] private SpriteRenderer crushWall1;

    // 左边界高速撞击破碎后的动画墙体；播放时原地淡出，新 Level 实例初始化时恢复初始颜色和显隐。
    [SerializeField] private SpriteRenderer crushWall2;

    [SerializeField] private float WallCrushAnimDuration = 0.5f;

    // 左墙高速撞击成功后播放的震屏 Feedback；由 Level70 prefab 绑定 MMF_Player，未绑定时不影响破墙主流程。
    [SerializeField] private MMF_Player crashSuccessFeedback;

    #endregion

    #region Runtime

    private const string ActorTag = "Player";
    private const string ToolsTag = "tools";

    private EventSubscriber subscriber;
    private Sequence wallCrushSequence;
    private Color initialCrushWall2Color;
    private bool hasInitialWallCrushState;
    private bool initialCrushWall1Active;
    private bool initialCrushWall2Active;
    private bool hookControlTriggerBound;
    private bool isLevel70CatchButtonMode;
    private CustomButton useBtn;
    private bool wallCrushAnimPlayed;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        CacheInitialState();
        InitializeRuntime();
    }

    private void OnEnable()
    {
        BindRuntimeEvents();
        BindTriggerEvents();
    }

    private void Start()
    {
        ResolveReferences();
        CacheInitialState();
    }

    private void OnDisable()
    {
        UnbindUseButton();
        UnbindTriggerEvents();
        EventsUtils.ResetEvents(ref subscriber);
    }

    private void OnDestroy()
    {
        UnbindUseButton();
        UnbindTriggerEvents();
        EventsUtils.ResetEvents(ref subscriber);
    }

    #endregion

    #region UnityEvent Entrypoints

    /// <summary>
    /// 抓钩控制入口，仅供 Level70 的 Trigger2D/UnityEvent 调用；重复触发会被忽略，不能用于其它关卡。
    /// </summary>
    /// <param name="other">触发入口的碰撞体，允许为空；本方法不依赖该对象做跨系统状态变更。</param>
    public void EnterHookControl(Collider2D other)
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (hookCtrl == null)
        {
            this.LogWarning("[Level70Ctrl] EnterHookControl skipped: Level70HookCtrl is not assigned.");
            return;
        }

        if (hookCtrl.IsControlActive)
            return;

        if (other != null && !IsActorCollider(other))
            return;

        if (hookControlTrigger != null)
            hookControlTrigger.gameObject.SetActive(false);

        SetActorInputLocked(true);
        hookCtrl.ActivateControl();
        SetLevel70CatchButtonMode(true);

        AudioManager.PlaySound("l70PlayGame");
    }

    /// <summary>
    /// tools 触发 Level70BallItem 打开和内部 sprite item 弹出；仅供 Level70 ball 的 OnTrigger2DHandle 绑定。
    /// </summary>
    /// <param name="other">tag 必须为 tools 的 Level70BallItem 相关碰撞体。</param>
    public void OpenBallItemByTools(Collider2D other)
    {
        if (!Application.isPlaying || other == null || !HasTag(other, ToolsTag))
            return;

        Level70BallItemCtrl item = other.GetComponentInParent<Level70BallItemCtrl>();
        if (item == null)
            item = other.GetComponentInChildren<Level70BallItemCtrl>(true);

        item?.OpenAndPlayJumpOut();
    }

    /// <summary>
    /// 播放左侧墙体破碎表现；仅供 Level70HookCtrl 在 range1Min 速度超过配置阈值后调用。
    /// </summary>
    /// <returns>开始播放破墙表现时返回 true；非 PlayMode、已播放或引用缺失时返回 false。</returns>
    public bool PlayLeftWallCrash()
    {
        if (!Application.isPlaying || wallCrushAnimPlayed)
            return false;

        if (!TryPrepareWallCrushAnim())
            return false;

        wallCrushAnimPlayed = true;
        crushWall1.gameObject.SetActive(false);
        crushWall2.gameObject.SetActive(true);

        AudioManager.PlaySound("l70Crash");
        crashSuccessFeedback?.PlayFeedbacks();

        wallCrushSequence = DOTween.Sequence().SetLink(gameObject);
        wallCrushSequence.Append(crushWall2.DOFade(0f, WallCrushAnimDuration));
        wallCrushSequence.OnComplete(() =>
        {
            if (crushWall2 != null)
                crushWall2.gameObject.SetActive(false);
        });
        wallCrushSequence.OnKill(() => wallCrushSequence = null);
        return true;
    }

    /// <summary>
    /// actor 恢复普通控制入口，只供独立恢复 Trigger2D 调用；抓钩释放或到达 dropPoint 不会调用它。
    /// </summary>
    /// <param name="other">触发恢复入口的碰撞体，允许为空；为空时也可由 Inspector 按钮测试恢复。</param>
    public void RestoreActorControl(Collider2D other)
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (other != null && !IsActorCollider(other))
            return;

        if (RestoreActorControl())
            ModifyActorCollider();
    }

    /// <summary>
    /// 执行 actor 普通控制恢复；只由带 Collider2D 的恢复入口在校验通过后调用。
    /// </summary>
    private bool RestoreActorControl()
    {
        ResolveReferences();
        bool actorRestored = actor == null || actor.RestoreLevel70NormalControl();
        if (!actorRestored)
        {
            hookCtrl?.SetMoveInput(0f);
            return false;
        }

        hookCtrl?.DeactivateControl();
        SetLevel70CatchButtonMode(false);

        if (levelCtrl != null)
            levelCtrl.SetCtrlable(true);

        return true;
    }

    private void ModifyActorCollider()
    {
        if (actor == null)
        {
            this.LogWarning("[Level70Ctrl] ModifyActorCollider skipped: actor is not assigned.");
            return;
        }

        Collider2D actorCollider = actor.GetComponent<Collider2D>();
        if (actorCollider == null)
        {
            this.LogWarning("[Level70Ctrl] ModifyActorCollider skipped: actor collider is missing.");
            return;
        }

        int stuffLayer = LayerMask.NameToLayer("Stuff");
        if (stuffLayer < 0)
        {
            this.LogWarning("[Level70Ctrl] ModifyActorCollider skipped: Stuff layer is missing.");
            return;
        }

        actorCollider.excludeLayers = 1 << stuffLayer;
        actor.SwitchWhatIsStuff(default);
    }

    #endregion

    #region Input

    private void BindRuntimeEvents()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerRightMove);
        subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);
        subscriber.Subscribe<PlayerMoveCancelEvent>(OnPlayerMoveCancel);
    }

    private void BindTriggerEvents()
    {
        ResolveReferences();
        if (hookControlTrigger == null || hookControlTriggerBound)
            return;

        hookControlTrigger.OnEnter.RemoveListener(EnterHookControl);
        hookControlTrigger.OnEnter.AddListener(EnterHookControl);
        hookControlTriggerBound = true;
    }

    private void UnbindTriggerEvents()
    {
        if (hookControlTrigger == null || !hookControlTriggerBound)
            return;

        hookControlTrigger.OnEnter.RemoveListener(EnterHookControl);
        hookControlTriggerBound = false;
    }

    private void OnPlayerRightMove(PlayerRightMoveEvent evt)
    {
        hookCtrl?.SetMoveInput(1f);
    }

    private void OnPlayerLeftMove(PlayerLeftMoveEvent evt)
    {
        hookCtrl?.SetMoveInput(-1f);
    }

    private void OnPlayerMoveCancel(PlayerMoveCancelEvent evt)
    {
        if (hookCtrl != null && hookCtrl.IsControlActive)
            hookCtrl.SetMoveInput(0f);
    }

    private void BindUseButton(CustomButton button)
    {
        if (useBtn == button)
            return;

        UnbindUseButton();
        useBtn = button;

        if (useBtn != null)
            useBtn.OnClickDown += OnClickUse;
    }

    private void UnbindUseButton()
    {
        if (useBtn != null)
            useBtn.OnClickDown -= OnClickUse;

        useBtn = null;
    }

    private void OnClickUse(CustomButton button)
    {
        if (button == null || button != useBtn || !button.isActiveAndEnabled || !button.Interactable)
            return;

        if (hookCtrl != null && hookCtrl.IsControlActive)
            hookCtrl.TryStartCatch();
    }

    #endregion

    #region Runtime Initialization

    private void InitializeRuntime()
    {
        SetLevel70CatchButtonMode(false);
        hookCtrl?.ResetRuntime();

        if (Application.isPlaying)
            ResetWallCrushAnim();

        InitializeBallsFromSprites();
    }

    #endregion

    #region Resolve & Utility

    private void ResolveReferences()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponent<BaseLevelCtrl>();

        if (actor == null)
            actor = GetComponentInChildren<Level70ActorCtrl>(true);

        if (hookCtrl == null)
            hookCtrl = GetComponent<Level70HookCtrl>();

        if (hookControlTrigger == null)
            hookControlTrigger = transform.Find("controller")?.GetComponent<OnTrigger2DHandle>();
    }

    private void CacheInitialState()
    {
        hookCtrl?.CacheInitialState();
        CacheInitialWallCrushState();
    }

    private void CacheInitialWallCrushState()
    {
        if (hasInitialWallCrushState || crushWall2 == null)
            return;

        initialCrushWall1Active = crushWall1 != null && crushWall1.gameObject.activeSelf;
        initialCrushWall2Active = crushWall2.gameObject.activeSelf;
        initialCrushWall2Color = crushWall2.color;
        hasInitialWallCrushState = true;
    }

    private bool TryPrepareWallCrushAnim()
    {
        CacheInitialWallCrushState();

        if (crushWall1 == null || crushWall2 == null)
        {
            this.LogWarning("[Level70Ctrl] PlayLeftWallCrash skipped: crush wall renderers are not assigned.");
            return false;
        }

        KillWallCrushAnim(false);
        if (hasInitialWallCrushState)
            crushWall2.color = initialCrushWall2Color;

        return true;
    }

    private void ResetWallCrushAnim()
    {
        KillWallCrushAnim(false);
        wallCrushAnimPlayed = false;

        if (!hasInitialWallCrushState)
            CacheInitialWallCrushState();

        if (!hasInitialWallCrushState)
            return;

        if (crushWall1 != null)
            crushWall1.gameObject.SetActive(initialCrushWall1Active);

        if (crushWall2 == null)
            return;

        crushWall2.color = initialCrushWall2Color;
        crushWall2.gameObject.SetActive(initialCrushWall2Active);
    }

    private void KillWallCrushAnim(bool complete)
    {
        wallCrushSequence?.Kill(complete);
        wallCrushSequence = null;

        if (crushWall2 == null)
            return;

        DOTween.Kill(crushWall2, complete);
    }

    private void InitializeBallsFromSprites()
    {
        if (!Application.isPlaying || balls == null || balls.Count == 0)
            return;

        for (int i = 0; i < balls.Count; i++)
        {
            Level70BallItemCtrl ball = balls[i];
            if (ball == null)
                continue;

            Sprite contentSprite = itemSprites != null && i < itemSprites.Count ? itemSprites[i] : null;
            ball.InitializeContent(contentSprite);
        }
    }

    private void SetActorInputLocked(bool locked)
    {
        if (levelCtrl != null)
            levelCtrl.SetCtrlable(!locked);

        if (actor == null)
            return;

        if (locked)
            actor.EnterLevel70HookControl();
    }

    private void SetLevel70CatchButtonMode(bool isCatchMode)
    {
        if (!Application.isPlaying || isLevel70CatchButtonMode == isCatchMode)
            return;

        if (!isCatchMode)
            UnbindUseButton();

        Level70GamePlayPanel level70Panel = PlayCtrl.Instance?.CurrentGamePlayPanel?.GetComponent<Level70GamePlayPanel>();
        if (level70Panel == null)
        {
            this.LogWarning("[Level70Ctrl] Level70GamePlayPanel not found on CurrentGamePlayPanel.");
            return;
        }

        CustomButton nextUseBtn = level70Panel.SetLevel70CatchButtonVisible(isCatchMode);
        isLevel70CatchButtonMode = isCatchMode;

        if (isCatchMode)
            BindUseButton(nextUseBtn);
    }

    private bool IsActorCollider(Collider2D other)
    {
        if (other == null)
            return false;

        Level70ActorCtrl targetActor = other.GetComponentInParent<Level70ActorCtrl>();
        if (targetActor != null)
            return actor == null || targetActor == actor;

        return HasTag(other, ActorTag);
    }

    private bool HasTag(Collider2D target, string tagName)
    {
        return target != null && target.gameObject != null && target.gameObject.tag == tagName;
    }

    #endregion
}

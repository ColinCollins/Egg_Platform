using System;
using System.Collections;
using System.Collections.Generic;
using Bear.EventSystem;
using DG.Tweening;
using Game.Common;
using Game.ConfigModule;
using Game.Events;
using Game.ItemEvent;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using Spine;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Level102 的局部状态机。
///
/// 本关沿用 Level101 的锁拖动/重新上锁表现，但箱子改由
/// Level102BoxBreakOnActor 处理，钥匙收集后启用 DoorLock Spine 解锁链。
/// 提示气泡沿用 PlayCtrl_Playing 的全局提示门槛（showTipsTimeLimit /
/// showTipsFailCount）；本控制器接管同一提示事件驱动的 GamePlayPanel warningBubble，
/// 使这个实际 UI 提示对象在需要时成为可拖动的垫脚物，并用屏幕到关卡世界的
/// 投影代理提供 Physics2D 碰撞，不再创建角色头顶 HintBubble。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level102Ctrl : MonoBehaviour
{
    private enum LockState
    {
        LockedIdle,
        Relocking,
        Unlocking,
        DoorUnlocked
    }

    private const float MinimumAnimationPollStep = 0.02f;
    private const float UnlockExecutorCompletionGrace = 0.25f;
    private const float WarningBubbleParentReadyTimeout = 2f;
    private const float WarningBubbleParentReadyScale = 0.98f;

    // 本关已获得答案后仍需要物理气泡；首次补偿时缓存门槛，避免逐帧读取配置。
    private bool hasLoadedPhysicalBubbleThresholds;
    private float physicalBubbleTimeLimit;
    private int physicalBubbleFailCount;

    #region Inspector

    [Header("基础引用")]
    [Tooltip("标准关卡控制器；为空时从当前节点或子节点查找。")]
    [SerializeField] private BaseLevelCtrl baseLevelCtrl;

    [Tooltip("当前关卡角色；为空时使用 BaseLevelCtrl.Actor 或子节点查找。")]
    [SerializeField] private ActorCtrl actor;

    [Tooltip("门根节点；为空时查找名为 door_0 的子节点。")]
    [SerializeField] private Transform doorRoot;

    [Tooltip("门的成功入口 Collider2D。解锁完成前保持关闭，完成后重新开启交给 BaseLevelCtrl。")]
    [SerializeField] private Collider2D doorCollider;

    [Tooltip("门上的 OnTrigger2DHandle；解锁完成前关闭，避免提前触发成功。")]
    [SerializeField] private OnTrigger2DHandle doorTriggerHandle;

    [Tooltip("运行时禁用复制 101 时残留的 Level101Ctrl，防止两套状态机同时改写锁和箱子。")]
    [SerializeField] private bool disableLegacyLevel101Controller = true;

    [Header("箱子")]
    [Tooltip("完整箱子上的 Level102BoxBreakOnActor；为空时自动查找。")]
    [SerializeField] private Level102BoxBreakOnActor boxBreak;

    [Tooltip("进入 PlayMode 时是否复位箱子和残骸。")]
    [SerializeField] private bool resetBoxOnStart = true;

    [Header("钥匙")]
    [Tooltip("场景中的真实 Key 预制体实例；为空时查找名为 Key 的子节点。")]
    [SerializeField] private GameObject keyObject;

    [Tooltip("真实钥匙上的 Level102KeyTrigger；为空时自动查找。")]
    [SerializeField] private Level102KeyTrigger keyTrigger;

    [Tooltip("钥匙触发碰撞体；为空时从 Level102KeyTrigger 或 Key 根节点查找。")]
    [SerializeField] private Collider2D keyCollider;

    [Tooltip("Key 预制体原有的拾取执行器；用于播放 getKey、渐隐和销毁链。")]
    [SerializeField] private TriggerExecutor keyPickupExecutor;

    [Header("钥匙拾取兜底")]
    [Tooltip("找不到标准 Key 执行器时才使用局部拾取兜底。关闭时复用 Key 预制体的标准 ItemJumpToListener，让钥匙飞向门的 LockPoint。")]
    [SerializeField] private bool useLocalKeyPickupFallback = false;

    [Tooltip("局部拾取演出开始后隐藏钥匙的延迟；应覆盖缩放和渐隐动画时长。")]
    [SerializeField, Min(0f)] private float localKeyPickupHideDelay = 1.05f;

    [Tooltip("找不到 Key 自带缩放监听器时使用的目标缩放倍率。")]
    [SerializeField, Min(0f)] private float localKeyScaleFactor = 0.5f;

    [Tooltip("找不到 Key 自带缩放监听器时使用的缩放时长。")]
    [SerializeField, Min(0f)] private float localKeyScaleDuration = 0.8f;

    [Tooltip("找不到 Key 自带渐隐监听器时使用的渐隐延迟。")]
    [SerializeField, Min(0f)] private float localKeyFadeDelay = 0.4f;

    [Tooltip("找不到 Key 自带渐隐监听器时使用的渐隐时长。")]
    [SerializeField, Min(0f)] private float localKeyFadeDuration = 0.5f;

    [Tooltip("是否关闭 Key 预制体原有 OnTrigger2DHandle，由 Level102KeyTrigger 统一判定角色。")]
    [SerializeField] private bool disableDefaultKeyTrigger = true;

    [Header("可见锁与重新上锁")]
    [Tooltip("门上可拖动的图片锁。")]
    [SerializeField] private Level102LockDragItem lockDrag;

    [Tooltip("图片锁的 SpriteRenderer，用于移走时渐隐。")]
    [SerializeField] private SpriteRenderer lockRenderer;

    [Tooltip("门体上的重新上锁 Spine；通常是 door_0/body。")]
    [SerializeField] private ActorSpineCtrl relockSpine;

    [Tooltip("标准门表现；通常是 door_0/door。吃到钥匙时先在假门上层显示。")]
    [SerializeField] private GameObject standardDoorVisual;

    [Tooltip("正常门与假门 body 主轮廓对齐后的局部位置；复用 Level101 的 idle 网格边界校准值。")]
    [SerializeField] private Vector3 normalDoorAlignedLocalPosition = new Vector3(0.1058f, -1.5815f, 0f);

    [Tooltip("正常门与假门 body 主轮廓对齐后的局部缩放。")]
    [SerializeField] private Vector3 normalDoorAlignedLocalScale = new Vector3(1.0283f, 1.0165f, 1f);

    [Tooltip("正常门内的循环漩涡特效；与标准门同步显示并使用相同的对齐补偿。")]
    [SerializeField] private GameObject legacyDoorEffectVisual;

    [Tooltip("重新上锁动画名称。")]
    [SerializeField] private string relockAnimationName = "lock";

    [Tooltip("重新上锁完成后的待机动画名称。")]
    [SerializeField] private string relockIdleAnimationName = "idle";

    [Tooltip("图片锁移走时的渐隐时长。")]
    [SerializeField, Min(0f)] private float lockFadeDuration = 0.2f;

    [Tooltip("找不到重新上锁动画时使用的兜底时长。")]
    [SerializeField, Min(0.1f)] private float relockFallbackDuration = 1.9f;

    [Header("Spine 解锁锁")]
    [Tooltip("DoorLock Spine 预制体实例；通常在场景中初始关闭。")]
    [SerializeField] private GameObject doorLockObject;

    [Tooltip("DoorLock 上的 ActorSpineCtrl；为空时从 DoorLock 子节点查找。")]
    [SerializeField] private ActorSpineCtrl doorLockSpine;

    [Tooltip("DoorLock 上的标准执行器；用于复用 unlock、音效和延迟销毁链。")]
    [SerializeField] private BaseItemExecutor doorLockExecutor;

    [Tooltip("DoorLock 的解锁动画名称。")]
    [SerializeField] private string unlockAnimationName = "unlock";

    [Tooltip("正常锁在钥匙飞到之前显示的待机动画。")]
    [SerializeField] private string doorLockIdleAnimationName = "lock_idle";

    [Tooltip("DoorLock 激活后等待 Spine 数据就绪的最长时间；用于覆盖复制实例在首帧的初始化延迟。")]
    [SerializeField, Min(0.1f)] private float doorLockInitializationTimeout = 0.75f;

    [Tooltip("解锁动画缺失或实例被提前销毁时的兜底时长。")]
    [SerializeField, Min(0.1f)] private float unlockFallbackDuration = 1.2f;

    [Tooltip("是否调用 DoorLock 预制体上的 BaseItemExecutor。关闭时只直接播放 Spine 动画。Level102 默认关闭，避免短动画与执行器等待回调竞态。")]
    [SerializeField] private bool invokeDoorLockExecutor = false;

    [Tooltip("仅在解锁动画/销毁链超时且确认过配置有效时强制开放门。默认关闭，避免异常配置绕过真实钥匙解锁。")]
    [SerializeField] private bool allowUnlockTimeoutFallback = false;

    [Header("提示气泡")]
    [Tooltip("提示气泡实例的父节点；为空时挂在关卡根节点。")]
    [SerializeField] private Transform hintBubbleParent;

    [Tooltip("可选的 HintBubble 预制体；为空时从 Resources/HintBubble 加载。")]
    [SerializeField] private HintBubble hintBubblePrefab;

    [Tooltip("气泡根节点的碰撞体；为空时在运行时创建 BoxCollider2D。")]
    [SerializeField] private Collider2D hintBubbleCollider;

    [Tooltip("气泡根节点的刚体；为空时在运行时创建 Rigidbody2D。")]
    [SerializeField] private Rigidbody2D hintBubbleRigidbody;

    [Tooltip("气泡根节点的 Level102HintBubbleDragItem；为空时运行时添加。")]
    [SerializeField] private Level102HintBubbleDragItem hintBubbleDrag;

    [Tooltip("自动创建气泡 BoxCollider2D 时的本地偏移。")]
    [SerializeField] private Vector2 hintBubbleColliderOffset = new Vector2(0.19f, 1.39f);

    [Tooltip("自动创建气泡 BoxCollider2D 时的本地尺寸。")]
    [SerializeField] private Vector2 hintBubbleColliderSize = new Vector2(1.15f, 1.05f);

    [Tooltip("生成气泡后是否根据气泡背景和文字的实际渲染边界自动扩大碰撞体。关闭时使用上面的固定尺寸。")]
    [SerializeField] private bool fitHintBubbleColliderToVisual = true;

    [Tooltip("等待气泡 show 动画完成后再按渲染边界适配碰撞体，避免初始缩放动画把碰撞体测得过小。")]
    [SerializeField, Min(0f)] private float hintBubbleColliderFitDelay = 0.5f;

    [Tooltip("自动适配碰撞体的最长等待时间；超时后保留上面的固定尺寸。")]
    [SerializeField, Min(0.1f)] private float hintBubbleColliderFitTimeout = 1f;

    [Tooltip("气泡根节点缩放达到该比例后才开始按可视边界适配。")]
    [SerializeField, Range(0f, 1f)] private float hintBubbleColliderFitMinimumRootScale = 0.98f;

    [Tooltip("自动适配气泡碰撞体时在四周增加的边距（每一侧）。")]
    [SerializeField] private Vector2 hintBubbleColliderPadding = new Vector2(0.08f, 0.08f);

    [Tooltip("气泡刚体质量；气泡是可移动的运动学垫脚物。")]
    [SerializeField, Min(0.01f)] private float hintBubbleMass = 1f;

    [Tooltip("没有预置物理组件时是否自动补齐气泡碰撞体、刚体和拖拽组件。")]
    [SerializeField] private bool addHintBubblePhysicsComponents = true;

    [Tooltip("运行时气泡及其碰撞体使用的物理层；默认 Stuff，需保留在 Actor.whatIsGround 中才能作为垫脚物。")]
    [SerializeField] private LayerMask hintBubblePhysicsLayer = 1 << 8;

    [Tooltip("在本关实例上关闭提示气泡 Animator 的根运动，避免 show/close 动画把可拖动气泡带离角色附近。")]
    [SerializeField] private bool disableHintBubbleRootMotion = true;

    [Tooltip("全局提示门槛满足时使用的气泡本地化 Key；默认与顶部提示气泡一致。")]
    [SerializeField] private string globalTipsBubbleDescription = "U_Tips_Pop_Des_01";

    [Tooltip("物理气泡未被拖动时的显示时长；默认与 GamePlayPanelTipsSwitcher 的 displayDuration 一致。")]
    [SerializeField, Min(0f)] private float globalTipsBubbleDisappearTime = 3f;

    [Header("warningBubble 物理接管")]
    [Tooltip("GamePlayPanel 上提示按钮的 GamePlayPanelTipsSwitcher；为空时从当前面板查找。")]
    [SerializeField] private GamePlayPanelTipsSwitcher warningTipsSwitcher;

    [Tooltip("真正显示给玩家的 warningBubble 实例；为空时从 GamePlayPanelTipsSwitcher.TargetObject 查找。")]
    [SerializeField] private GameObject warningBubbleObject;

    [Tooltip("warningBubble 的 RectTransform；为空时从 warningBubbleObject 获取。")]
    [SerializeField] private RectTransform warningBubbleRect;

    [Tooltip("warningBubble 的真实可视区域；通常绑定 Image 子节点，仅供拖拽命中。")]
    [SerializeField] private RectTransform warningBubbleInteractionRect;

    [Tooltip("warningBubble 的物理范围；为空时优先查找 msg/title 文字矩形，匹配策划标注的红框范围。")]
    [SerializeField] private RectTransform warningBubbleColliderRect;

    [Tooltip("warningBubble 对应的场景世界碰撞体；为空且允许自动补齐时运行时创建独立代理。不要直接绑定 UI 节点上的 Collider。")]
    [SerializeField] private Collider2D warningBubblePhysicsCollider;

    [Tooltip("warningBubble 对应的场景世界刚体；为空且允许自动补齐时运行时创建独立代理。不要直接绑定 UI 节点上的 Rigidbody2D。")]
    [SerializeField] private Rigidbody2D warningBubblePhysicsRigidbody;

    [Tooltip("warningBubble 的拖拽适配器；为空且允许自动补齐时运行时添加。")]
    [SerializeField] private Level102HintBubbleDragItem warningBubbleDrag;

    [Tooltip("是否允许本关在 warningBubble 实例上补齐 2D 物理和拖拽组件。")]
    [SerializeField] private bool addWarningBubblePhysicsComponents = true;

    [Tooltip("warningBubble 文字矩形四周增加的本地 UI 单位边距；Y=20 用于覆盖策划标注的红框高度，但不包含外侧气泡轮廓和尾巴。")]
    [SerializeField] private Vector2 warningBubbleColliderPadding = new Vector2(0f, 20f);

    [Tooltip("是否按 warningBubble 及其 UI 子节点的实际矩形边界适配 Collider；关闭时使用根节点尺寸。")]
    [SerializeField] private bool fitWarningBubbleColliderToVisual = true;

    [Tooltip("角色站在 warningBubble 世界代理顶部时是否隐藏角色阴影。")]
    [SerializeField] private bool hideActorShadowOnWarningBubble = true;

    [Tooltip("判定角色脚底位于 warningBubble 顶部时允许的世界坐标误差。")]
    [SerializeField, Min(0f)] private float warningBubbleTopContactTolerance = 0.15f;

    [Tooltip("warningBubble 的刚体质量；当前为 Kinematic，仅用于保持物理接触设置一致。")]
    [SerializeField, Min(0.01f)] private float warningBubbleMass = 1f;

    [Tooltip("warningBubble 未被拖动时是否使用提示开关 Inspector 中的展示时长。")]
    [SerializeField] private bool useWarningSwitcherDisplayDuration = true;

    [Tooltip("warningBubble 每次显示时从零缩放展开的时长；动画完成后才开放拖拽。")]
    [SerializeField, Min(0f)] private float warningBubbleExpandDuration = 0.3f;

    [Tooltip("warningBubble 展开动画的缓动曲线。默认与通用提示一致。")]
    [SerializeField] private Ease warningBubbleExpandEase = Ease.OutQuad;

    [Tooltip("warningBubble 自动消失时回缩到 0 的时长。")]
    [SerializeField, Min(0f)] private float warningBubbleCollapseDuration = 0.2f;

    [Tooltip("warningBubble 自动消失时的回缩缓动曲线。默认与通用提示一致。")]
    [SerializeField] private Ease warningBubbleCollapseEase = Ease.OutQuad;

    #endregion

    #region Runtime State

    private LockState state = LockState.LockedIdle;
    private int flowVersion;
    private bool runtimeInitialized;
    private bool levelStarted;
    // PlayCtrl 可能在本控制器 Start 前派发首次提示事件；只缓存这一个初始化窗口，
    // 重开/Teardown 后不再接收旧事件，避免新尝试开场误弹气泡。
    private bool hasStartedPlayLifecycle;
    private bool hasPendingHintBubble;
    private string pendingHintDescription;
    private float pendingHintDisappearTime;
    private bool localInputPaused;
    private bool lockDragStarted;
    private bool keyCollected;
    private bool bubbleDragged;

    private EventSubscriber eventSubscriber;
    private Tween lockFadeTween;
    private Coroutine relockRoutine;
    private Coroutine unlockRoutine;
    private Coroutine doorLockActivationRoutine;
    private Coroutine hintBubbleRoutine;
    private Coroutine keyPickupRoutine;
    private Coroutine keyArrivalRoutine;
    private Coroutine keyBobStartRoutine;
    private Coroutine doorReplacementRoutine;

    // 钥匙收集帧先显示正常门 + DoorLock，下一帧再退役假门。
    // 两个布尔值分开表达重叠帧，避免单一 LockState 把两扇门同帧互斥。
    private bool replacementDoorVisible;
    private bool fakeDoorRetired;

    private Vector3 lockInitialLocalPosition;
    private Quaternion lockInitialLocalRotation;
    private Vector3 lockInitialLocalScale;
    private Color lockInitialColor = Color.white;
    private bool hasLockPose;

    private Spine.TrackEntry relockTrackEntry;
    private Spine.AnimationState.TrackEntryDelegate relockCompleteHandler;
    private Spine.TrackEntry unlockTrackEntry;
    private Spine.AnimationState.TrackEntryDelegate unlockCompleteHandler;

    private bool unlockActivationStarted;
    private bool unlockAnimationObserved;
    // 执行器模式下，Spine 动画完成并不等于 DoorLock 执行器已经完成其 SFX/销毁链。
    // 该标记把动画完成与执行器生命周期拆开，避免在执行器后续项执行前隐藏根节点。
    private bool unlockAnimationCompleted;
    private bool unlockWaitsForExecutorLifecycle;
    private float pausedDoorLockTimeScale = 1f;
    private bool doorLockAnimationPaused;

    private bool hasDoorColliderPose;
    private bool doorColliderInitialEnabled;
    private bool doorColliderInitialIsTrigger;

    private HintBubble activeHintBubble;
    private Level102HintBubbleDragItem activeHintBubbleDrag;
    private Collider2D activeHintBubbleCollider;
    private Rigidbody2D activeHintBubbleRigidbody;
    private Coroutine hintBubbleColliderFitRoutine;

    private GameObject activeWarningBubbleObject;
    private RectTransform activeWarningBubbleRect;
    private GamePlayPanelTipsSwitcher activeWarningTipsSwitcher;
    private GameObject warningBubblePhysicsProxy;
    private Collider2D activeWarningBubbleCollider;
    private Rigidbody2D activeWarningBubbleRigidbody;
    private Level102HintBubbleDragItem activeWarningBubbleDrag;
    private Coroutine warningBubbleColliderFitRoutine;
    private bool warningBubbleExternalControl;
    private bool warningBubbleWasShown;
    private bool warningBubbleDragReady;
    private Tween warningBubbleShowTween;
    private Tween warningBubbleHideTween;
    private Vector3 warningBubbleInitialLocalPosition;
    private Vector2 warningBubbleInitialAnchoredPosition;
    private Quaternion warningBubbleInitialLocalRotation;
    private Vector3 warningBubbleInitialLocalScale = Vector3.one;
    private bool hasWarningBubblePose;
    private Collider2D actorCollider;
    private GameObject actorShadowObject;
    private bool actorShadowActiveBeforeBubble;
    private bool actorShadowSuppressedByBubble;

    private OnTrigger2DHandle[] doorTriggerHandles = Array.Empty<OnTrigger2DHandle>();
    private OnTrigger2DHandle[] keyTriggerHandles = Array.Empty<OnTrigger2DHandle>();
    // DoorLock 自带的触发器会把任意进入者转发给共享 BaseItemExecutor。
    // Level102 的解锁由真实钥匙状态机显式驱动，不能让角色/画线碰撞重复启动它。
    private OnTrigger2DHandle[] doorLockTriggerHandles = Array.Empty<OnTrigger2DHandle>();

    private MoveFloatHandle keyBobHandle;
    private ItemScaleListener keyScaleListener;
    private PlaySfxListener keySfxListener;
    private SpriteRenderFadeListener keyFadeListener;
    private ItemJumpToListener keyJumpToListener;
    private Transform keyVisual;
    private SpriteRenderer keyVisualRenderer;
    private SortingGroup keySortingGroup;
    private Vector3 keyInitialLocalPosition;
    private Quaternion keyInitialLocalRotation;
    private Vector3 keyInitialLocalScale;
    private Vector3 keyVisualInitialLocalScale;
    private Color keyVisualInitialColor = Color.white;
    private int keyInitialSortingLayerId;
    private int keyInitialSortingOrder;
    private bool hasKeySortingPose;
    private bool hasKeyPickupPose;
    private bool keyPickupStarted;

    /// <summary>当前是否已经收集真实钥匙。</summary>
    public bool IsKeyCollected => keyCollected;

    /// <summary>当前是否已经完成 Spine 解锁并开放门入口。</summary>
    public bool IsDoorUnlocked => state == LockState.DoorUnlocked;

    /// <summary>
    /// 当前帧是否允许本关的局部交互接收输入。
    /// 入口统一经过这里，避免暂停/结算事件与 Unity 物理回调竞态时先锁存状态。
    /// </summary>
    public bool CanAcceptGameplayInput =>
        runtimeInitialized && levelStarted && IsGameplayInputOpen();

    /// <summary>当前关卡实际控制的玩家角色。</summary>
    public ActorCtrl CurrentActor => actor;

    /// <summary>当前由本关接管的物理提示气泡；没有显示时为空。</summary>
    public HintBubble ActiveHintBubble => activeHintBubble;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        CaptureLockPose();
        CaptureKeyPickupPose();
        CaptureDoorColliderPose();

        // 让拖拽适配器在同一帧收到输入前就有明确的初始状态。
        state = LockState.LockedIdle;
        levelStarted = false;
        if (!Application.isPlaying)
            return;

        runtimeInitialized = true;
        DisableLegacyController();
        ApplyDoorVisualState();
        // 本关不使用角色头顶的通用 HintBubble；提示表现统一由 GamePlayPanel
        // 的 warningBubble 接管，避免两个气泡同时出现或误拖动角色头顶气泡。
        baseLevelCtrl?.SetHintBubbleEnabled(false);
        DestroyOtherHintBubbles();
        SetDoorLockTriggerHandlers(false);

        // DoorLock 的源预制体默认是 Trigger。Level102 在拿到钥匙前必须保持实体门，
        // 因此在第一个物理步前先关闭成功入口并切换为实体碰撞；Start 会再次校正。
        SetDoorGate(false);
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        CaptureKeyPickupPose();
        CaptureDoorColliderPose();
        if (resetBoxOnStart)
            boxBreak?.ResetRuntimeState();

        // 所有对象的 Awake 已经完成后再复位钥匙。ItemScaleListener 在自身
        // Awake 中记录初始缩放，过早复位会在跨对象 Awake 顺序变化时把钥匙
        // 错误地恢复为零缩放。
        PrepareInitialState();
        // GamePlayPanel 在创建时会先启动默认 Tips 展开流程。提前在关卡
        // Start 阶段接管共享 warningBubble，避免默认动画抢先显示一帧，导致
        // 首次物理气泡没有本关展开动画或尚未接上拖拽输入。
        TryBindWarningBubble();
        baseLevelCtrl?.SetHintBubbleEnabled(false);
        DestroyOtherHintBubbles();
        levelStarted = true;
        hasStartedPlayLifecycle = true;
        ConsumePendingHintBubble();
        ScheduleKeyBobStart();
        RefreshInputAvailability();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<GamePlayPanelSwitchTipsEvent>(OnGamePlayPanelSwitchTips);
        eventSubscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        eventSubscriber.Subscribe<GamePauseEvent>(OnGamePause);
        eventSubscriber.Subscribe<GameResumeEvent>(OnGameResume);
        eventSubscriber.Subscribe<GameResetEvent>(OnGameReset);
        eventSubscriber.Subscribe<GameReplayEvent>(OnGameReplay);
        baseLevelCtrl?.SetHintBubbleEnabled(false);
        DestroyOtherHintBubbles();
        RefreshInputAvailability();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        RefreshInputAvailability();

        // GamePlayPanel 在 PlayCtrl 中晚于关卡对象动态创建；面板出现后再绑定
        // warningBubble，并把默认 Tips 动画交给本关局部物理流程。
        TryBindWarningBubble();

        UpdatePhysicalBubbleAvailability();

        // PlayCtrl 可能在 Start 后仍处于暂停/初始化态。缓存的首次提示事件
        // 必须等局部输入真正开放后再消费，不能因为一次过早调用而丢失。
        if (hasPendingHintBubble && CanAcceptGameplayInput)
            ConsumePendingHintBubble();

        // 某些复制门上的 SwitchComponentListener 可能在退出接触时改写入口状态；
        // 每帧只在状态不一致时纠正，锁定态必须是实体碰撞，解锁态必须是 Trigger。
        SetDoorGate(state == LockState.DoorUnlocked);
        SetDoorLockTriggerHandlers(false);

        ApplyDoorVisualState();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !warningBubbleWasShown || !warningBubbleDragReady)
            return;

        // warningBubble 属于屏幕 UI，物理代理属于关卡世界。拖拽、CanvasScaler、
        // 安全区或相机移动后都要重新投影，不能让 UI Transform 直接参与 Physics2D。
        SyncWarningBubbleWorldCollider();
        RefreshActorShadowForWarningBubble();
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying || !warningBubbleWasShown || !warningBubbleDragReady)
            return;

        // 在物理步前再同步一次，保证角色落脚时使用最新的气泡世界位置。
        SyncWarningBubbleWorldCollider();
    }

    private void OnDisable()
    {
        // 编辑器停留/预制体导入阶段不应改写碰撞体、销毁气泡或重置事件。
        // 运行态禁用（包括关卡重建）仍需走统一清理路径。
        if (!Application.isPlaying)
            return;

        BeginTeardown();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
            return;

        BeginTeardown();
        if (baseLevelCtrl != null)
            baseLevelCtrl.SetHintBubbleEnabled(true);
    }

    #endregion

    #region Lock Flow

    /// <summary>
    /// 由 Level102LockDragItem 在开始拖动图片锁时调用。
    /// </summary>
    public bool TryBeginLockDrag()
    {
        if (state != LockState.LockedIdle || keyCollected || lockDragStarted ||
            !CanAcceptGameplayInput)
            return false;

        lockDragStarted = true;
        return true;
    }

    /// <summary>
    /// 由 Level102LockDragItem 在结束拖动图片锁时调用。
    /// 第一次及后续移锁都复用 Level101 的“渐隐 + 重新上锁 + 生成锁”逻辑。
    /// </summary>
    public void EndLockDrag()
    {
        if (!lockDragStarted || state != LockState.LockedIdle)
            return;

        lockDragStarted = false;
        if (!CanAcceptGameplayInput)
        {
            RestoreLockVisual();
            return;
        }

        state = LockState.Relocking;
        int version = ++flowVersion;
        lockDrag?.SetEnableDrag(false);

        lockFadeTween?.Kill();
        lockFadeTween = null;
        if (lockRenderer != null && lockFadeDuration > 0f)
        {
            lockFadeTween = lockRenderer
                .DOFade(0f, lockFadeDuration)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        PlayRelockAnimation(version);
    }

    private void PlayRelockAnimation(int version)
    {
        ClearRelockTrackEntry();
        if (relockRoutine != null)
            StopCoroutine(relockRoutine);

        relockRoutine = null;
        if (relockSpine != null && !string.IsNullOrWhiteSpace(relockAnimationName))
        {
            relockTrackEntry = relockSpine.PlayAnimation(relockAnimationName, false, false);
            if (relockTrackEntry != null)
            {
                relockCompleteHandler = _ => FinishRelock(version);
                relockTrackEntry.Complete += relockCompleteHandler;
            }
        }

        relockRoutine = StartCoroutine(RelockTimeout(version));
    }

    private IEnumerator RelockTimeout(int version)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, relockFallbackDuration);
        while (elapsed < duration)
        {
            if (version != flowVersion || state != LockState.Relocking)
                yield break;

            elapsed += Mathf.Max(MinimumAnimationPollStep, Time.deltaTime);
            yield return null;
        }

        relockRoutine = null;
        FinishRelock(version);
    }

    private void FinishRelock(int version)
    {
        if (version != flowVersion || state != LockState.Relocking)
            return;

        ClearRelockTrackEntry();
        if (relockRoutine != null)
        {
            StopCoroutine(relockRoutine);
            relockRoutine = null;
        }

        lockFadeTween?.Kill();
        lockFadeTween = null;
        RestoreLockVisual();
        relockSpine?.PlayAnimation(relockIdleAnimationName, true, false);
        state = LockState.LockedIdle;
        RefreshInputAvailability();
    }

    private void RestoreLockVisual()
    {
        if (lockDrag != null)
        {
            lockDrag.gameObject.SetActive(true);
            if (hasLockPose)
            {
                lockDrag.transform.localPosition = lockInitialLocalPosition;
                lockDrag.transform.localRotation = lockInitialLocalRotation;
                lockDrag.transform.localScale = lockInitialLocalScale;
            }
        }

        if (lockRenderer != null)
            lockRenderer.color = lockInitialColor;
    }

    private void HideLockVisual()
    {
        lockFadeTween?.Kill();
        lockFadeTween = null;
        if (lockDrag != null)
        {
            lockDrag.SetEnableDrag(false);
            lockDrag.gameObject.SetActive(false);
        }
    }

    private void BeginUnlockSequence()
    {
        if (state == LockState.Unlocking || state == LockState.DoorUnlocked)
            return;

        state = LockState.Unlocking;
        int version = ++flowVersion;
        unlockActivationStarted = false;
        unlockAnimationObserved = false;
        unlockAnimationCompleted = false;
        unlockWaitsForExecutorLifecycle = false;
        StopDoorLockActivationRoutine();
        lockDragStarted = false;
        ClearRelockTrackEntry();
        if (relockRoutine != null)
        {
            StopCoroutine(relockRoutine);
            relockRoutine = null;
        }

        lockDrag?.SetEnableDrag(false);
        lockFadeTween?.Kill();
        lockFadeTween = null;
        if (fakeDoorRetired)
        {
            ActivateDoorLock(version);
            return;
        }

        if (lockRenderer != null && lockFadeDuration > 0f)
        {
            lockFadeTween = lockRenderer
                .DOFade(0f, lockFadeDuration)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnComplete(() => ActivateDoorLock(version));
        }
        else
        {
            ActivateDoorLock(version);
        }
    }

    private void ActivateDoorLock(int version)
    {
        if (version != flowVersion || state != LockState.Unlocking || unlockActivationStarted)
            return;

        // 渐隐完成回调和兜底调用可能在同一帧汇合；只允许第一个调用者启动
        // DoorLock 链，避免重复重播 Spine 或重复执行执行器事件。
        unlockActivationStarted = true;

        HideLockVisual();
        // 进入 Unlocking 时关闭当前锁定门，同时打开正常过关门作为 DoorLock
        // 解锁动画的门体背景，避免只剩一把锁悬空播放。
        ApplyDoorVisualState();
        if (doorLockObject == null)
        {
            AbortUnlock(version, "DoorLock 对象缺失，保持门锁定。", null);
            return;
        }

        try
        {
            doorLockObject.SetActive(true);
            // SetActive 会在后续物理步重新建立其 Trigger 接触对；保持 DoorLock
            // 自带的共享触发链关闭，解锁只能由本关显式路径推进。
            SetDoorLockTriggerHandlers(false);
        }
        catch (Exception exception)
        {
            doorLockObject = null;
            AbortUnlock(version, "DoorLock 无法激活，保持门锁定。", exception);
            return;
        }

        // SkeletonAnimation 的 SkeletonData 可能在 DoorLock 激活后的首帧才完成
        // 初始化。不要在 SetActive(true) 同一帧把“数据尚未就绪”误判为配置错误。
        if (doorLockActivationRoutine != null)
            StopCoroutine(doorLockActivationRoutine);
        doorLockActivationRoutine = StartCoroutine(WaitForDoorLockReady(version));
    }

    private IEnumerator WaitForDoorLockReady(int version)
    {
        // 至少让激活链经过一帧，覆盖 OnEnable/Start 中的 SkeletonAnimation 初始化。
        yield return null;

        float elapsed = 0f;
        float timeout = Mathf.Max(0.1f, doorLockInitializationTimeout);
        while (version == flowVersion && state == LockState.Unlocking)
        {
            ResolveDoorLockReferences();

            string validationError;
            if (TryValidateDoorLockUnlockAnimation(out validationError))
            {
                doorLockActivationRoutine = null;
                StartDoorLockUnlock(version);
                yield break;
            }

            // 只有组件/Spine 数据尚未准备好时才继续重试；动画缺失等确定性配置
            // 错误应立即 fail-closed，避免无意义地延长钥匙收集后的等待。
            if (!IsDoorLockInitializationPending())
            {
                doorLockActivationRoutine = null;
                AbortUnlock(version, validationError, null);
                yield break;
            }

            if (!localInputPaused && Time.timeScale > 0.0001f)
            {
                elapsed += Mathf.Max(MinimumAnimationPollStep, Time.deltaTime);
                if (elapsed >= timeout)
                {
                    doorLockActivationRoutine = null;
                    AbortUnlock(version,
                        "DoorLock Spine 初始化超时，保持门锁定。请检查 DoorLock 预制体和 SkeletonAnimation。",
                        null);
                    yield break;
                }
            }

            yield return null;
        }

        doorLockActivationRoutine = null;
    }

    private bool IsDoorLockInitializationPending()
    {
        if (doorLockObject == null)
            return false;

        if (doorLockSpine == null)
            return true;

        try
        {
            Spine.AnimationState animationState = doorLockSpine.AnimationState;
            return animationState == null || animationState.Data == null ||
                   animationState.Data.SkeletonData == null;
        }
        catch (Exception)
        {
            // 资源初始化期间读取 Spine 属性可能暂时抛异常，按 pending 处理，
            // 让短重试窗口自行恢复；超时后仍由上层 fail-closed。
            return true;
        }
    }

    private void StartDoorLockUnlock(int version)
    {
        if (version != flowVersion || state != LockState.Unlocking)
            return;

        ClearUnlockTrackEntry();

        string validationError;
        if (!TryValidateDoorLockUnlockAnimation(out validationError))
        {
            AbortUnlock(version, validationError, null);
            return;
        }

        bool executorWillPlayUnlock = invokeDoorLockExecutor && doorLockExecutor != null;
        unlockWaitsForExecutorLifecycle = executorWillPlayUnlock;
        unlockAnimationCompleted = false;

        // 先由本关拿到真实 TrackEntry 并绑定完成回调，再启动标准执行器。DoorLock
        // 的 PlaySpineListener 使用 checkCurrent=true，因此下一帧执行器会复用这条
        // 已经开始的轨道，而不是重播。这样即使 unlock 很短，也不会因为执行器的
        // 顺序 Update 尚未推进到 PlaySpineListener 而错过完成事件。
        try
        {
            unlockTrackEntry = doorLockSpine.PlayAnimation(unlockAnimationName, false, false);
            if (unlockTrackEntry != null)
                unlockTrackEntry.MixDuration = 0f;
        }
        catch (Exception exception)
        {
            AbortUnlock(version, "DoorLock 的 unlock Spine 启动失败，保持门锁定。", exception);
            return;
        }

        if (unlockTrackEntry == null)
        {
            AbortUnlock(version, "DoorLock 没有返回 unlock Spine 轨道，保持门锁定。", null);
            return;
        }

        unlockAnimationObserved = true;
        float expectedDuration = GetDoorLockUnlockTimeout(unlockTrackEntry, executorWillPlayUnlock);
        unlockCompleteHandler = _ => HandleUnlockAnimationComplete(version);
        unlockTrackEntry.Complete += unlockCompleteHandler;

        if (executorWillPlayUnlock)
        {
            try
            {
                doorLockExecutor.enabled = true;
                doorLockExecutor.Execute();
            }
            catch (Exception exception)
            {
                AbortUnlock(version, "DoorLock 执行器启动失败，保持门锁定。", exception);
                return;
            }
        }
        else
        {
            // Level102 默认不走 DoorLock 的顺序执行器：其 PlaySpineListener 可能在
            // 下一帧才取得轨道，短 unlock 动画会因此错过 Complete。这里复用已验证
            // 的局部解锁路径，音效与动画都以当前已取得的 TrackEntry 为准。
            AudioManager.PlaySound("unlock");
        }

        if (unlockRoutine != null)
            StopCoroutine(unlockRoutine);
        // TrackEntry 是本关已经取得的权威完成信号；执行器只负责复用动画后的
        // 音效和销毁链，不再作为“是否播放过动画”的唯一观察入口。
        unlockRoutine = StartCoroutine(UnlockTimeout(
            version,
            expectedDuration,
            executorWillPlayUnlock));
    }

    private IEnumerator UnlockTimeout(int version, float duration, bool waitForDoorLockLifecycle)
    {
        float elapsed = 0f;
        float timeout = Mathf.Max(0.1f, duration);
        bool timedOut = false;
        bool lifecycleCompleted = false;
        while (true)
        {
            if (version != flowVersion || state != LockState.Unlocking)
                yield break;

            // Spine 完成回调只代表动画结束。执行器模式还必须等待它的 SFX/Destroy
            // 后续项结束，才能关闭 DoorLock 根节点；否则 BaseItemExecutor 会在同一
            // 帧被禁用，后续项永远不会执行。
            bool animationCompleted = unlockAnimationCompleted;
            if (!animationCompleted && waitForDoorLockLifecycle)
                animationCompleted = IsDoorLockUnlockAnimationComplete();

            if (!animationCompleted && !waitForDoorLockLifecycle && unlockTrackEntry != null)
            {
                animationCompleted = unlockTrackEntry.TrackTime >=
                                      unlockTrackEntry.AnimationEnd - 0.01f;
                if (animationCompleted)
                    unlockAnimationCompleted = true;
            }

            if (animationCompleted &&
                (!waitForDoorLockLifecycle || doorLockObject == null ||
                 !doorLockObject.activeInHierarchy))
            {
                lifecycleCompleted = true;
                break;
            }

            // 关卡暂停和 Time.timeScale=0 都不应消耗兜底时长；DoorLock 的
            // Spine/DestroyListener 同样按缩放时间推进，故这里使用缩放后的帧间隔。
            if (CanAcceptGameplayInput && Time.timeScale > 0.0001f)
            {
                elapsed += Mathf.Max(0f, Time.deltaTime);
                if (elapsed >= timeout)
                {
                    timedOut = true;
                    break;
                }
            }

            yield return null;
        }

        unlockRoutine = null;
        if (timedOut)
        {
            if (allowUnlockTimeoutFallback && unlockAnimationObserved)
            {
                Debug.LogWarning(
                    "[Level102Ctrl] DoorLock 解锁链超过兜底时长，按 Inspector 开关开放门入口。",
                    this);
                FinishUnlock(version);
            }
            else
            {
                AbortUnlock(version,
                    "DoorLock 解锁链超时且未确认完成，保持门锁定。请检查 DoorLock 执行器和 Spine 配置。",
                    null);
            }

            yield break;
        }

        if (!lifecycleCompleted)
        {
            AbortUnlock(version, "DoorLock 生命周期异常，保持门锁定。", null);
            yield break;
        }

        FinishUnlock(version);
    }

    /// <summary>
    /// 在启动 DoorLock 链前确认目标 Spine 和 unlock 动画确实存在。
    /// 预制体复制时若引用丢失，不能让门静默地走到成功状态。
    /// </summary>
    private bool TryValidateDoorLockUnlockAnimation(out string error)
    {
        error = null;
        if (doorLockSpine == null)
        {
            error = "DoorLock 的 ActorSpineCtrl 缺失，保持门锁定。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(unlockAnimationName))
        {
            error = "DoorLock 的 unlock 动画名称为空，保持门锁定。";
            return false;
        }

        try
        {
            Spine.AnimationState animationState = doorLockSpine.AnimationState;
            var skeletonData = animationState?.Data?.SkeletonData;
            if (skeletonData == null)
            {
                error = "DoorLock 的 Spine 数据尚未初始化，保持门锁定。";
                return false;
            }

            if (skeletonData.FindAnimation(unlockAnimationName) == null)
            {
                error = $"DoorLock 找不到 Spine 动画 '{unlockAnimationName}'，保持门锁定。";
                return false;
            }
        }
        catch (Exception exception)
        {
            error = $"DoorLock 的 Spine 配置读取失败，保持门锁定：{exception.Message}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 解锁链配置错误时的 fail-closed 收尾。钥匙已被消耗，需重开关卡重新尝试；
    /// 这里绝不开放门入口。
    /// </summary>
    private void AbortUnlock(int version, string reason, Exception exception)
    {
        if (version != flowVersion || state != LockState.Unlocking)
            return;

        flowVersion++;
        StopDoorLockActivationRoutine();
        ClearUnlockTrackEntry();
        if (unlockRoutine != null)
        {
            StopCoroutine(unlockRoutine);
            unlockRoutine = null;
        }

        unlockActivationStarted = false;
        unlockAnimationObserved = false;
        unlockAnimationCompleted = false;
        unlockWaitsForExecutorLifecycle = false;
        if (doorLockObject != null)
        {
            try
            {
                SetDoorLockTriggerHandlers(false);
                doorLockObject.SetActive(false);
            }
            catch (Exception disableException)
            {
                Debug.LogWarning(
                    $"[Level102Ctrl] DoorLock 收尾时无法隐藏实例：{disableException.Message}",
                    this);
            }
        }

        StopDoorReplacementRoutine();
        replacementDoorVisible = false;
        fakeDoorRetired = false;
        state = LockState.LockedIdle;
        RestoreLockVisual();
        ApplyNormalDoorAlignment();
        ApplyDoorVisualState();
        SetDoorGate(false);
        RefreshInputAvailability();

        if (exception == null)
            Debug.LogError($"[Level102Ctrl] {reason}", this);
        else
            Debug.LogError($"[Level102Ctrl] {reason} {exception.GetType().Name}: {exception.Message}", this);
    }

    /// <summary>
    /// 检查执行器刚启动的 DoorLock 是否已经播放完指定解锁动画。
    /// 仅用于执行器路径；首次观察到轨道时也绑定 TrackEntry.Complete，
    /// 这样即使执行器随后进入延迟销毁阶段，解锁收尾仍不依赖轮询时机。
    /// </summary>
    private bool IsDoorLockUnlockAnimationComplete()
    {
        if (doorLockSpine == null || string.IsNullOrWhiteSpace(unlockAnimationName))
            return false;

        try
        {
            Spine.AnimationState animationState = doorLockSpine.AnimationState;
            Spine.TrackEntry current = animationState != null ? animationState.GetCurrent(0) : null;
            if (current == null || current.Animation == null ||
                !string.Equals(current.Animation.Name, unlockAnimationName, StringComparison.OrdinalIgnoreCase))
                return false;

            // 执行器内部的 PlaySpineListener 在本控制器之后才创建/持有该轨道。
            // 第一次轮询观察到它时立即保留引用并监听 Complete，避免短动画在下一次
            // 轮询前完成、随后 DestroyListener 销毁 DoorLock，导致状态被误判为失败。
            if (unlockTrackEntry != current || unlockCompleteHandler == null)
            {
                ClearUnlockTrackEntry();
                unlockTrackEntry = current;
                int version = flowVersion;
                unlockCompleteHandler = _ => HandleUnlockAnimationComplete(version);
                unlockTrackEntry.Complete += unlockCompleteHandler;
            }

            unlockAnimationObserved = true;
            bool completed = current.TrackTime >= current.AnimationEnd - 0.01f;
            if (completed)
                unlockAnimationCompleted = true;
            return completed;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[Level102Ctrl] 读取 DoorLock unlock 播放状态失败：{exception.Message}",
                this);
            return false;
        }
    }

    private float GetDoorLockUnlockAnimationDuration()
    {
        if (doorLockSpine == null || string.IsNullOrWhiteSpace(unlockAnimationName))
            return 0f;

        try
        {
            var animationState = doorLockSpine.AnimationState;
            var animation = animationState?.Data?.SkeletonData?.FindAnimation(unlockAnimationName);
            return animation != null ? Mathf.Max(0f, animation.Duration) : 0f;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[Level102Ctrl] 读取 DoorLock unlock 时长失败：{exception.Message}",
                this);
            return 0f;
        }
    }

    private float GetDoorLockUnlockTimeout(Spine.TrackEntry entry, bool includesExecutor)
    {
        float timeout = Mathf.Max(0.1f, unlockFallbackDuration);
        float animationDuration = entry != null ? Mathf.Max(0f, entry.AnimationEnd) : 0f;
        if (animationDuration <= 0f)
            animationDuration = GetDoorLockUnlockAnimationDuration();

        float playbackSpeed = 1f;
        try
        {
            float entryScale = entry != null ? Mathf.Abs(entry.TimeScale) : 1f;
            float stateScale = doorLockSpine != null && doorLockSpine.AnimationState != null
                ? Mathf.Abs(doorLockSpine.AnimationState.TimeScale)
                : 1f;
            // TrackEntry/AnimationState 的倍率都会影响实际动画时间；按有效倍率
            // 放大 watchdog，避免合法的慢速解锁被误判为超时。倍率为 0 时保留
            // 一个很小的下限，仍让显式兜底开关决定是否 fail-open。
            playbackSpeed = Mathf.Max(0.01f, entryScale * stateScale);
        }
        catch (Exception)
        {
            playbackSpeed = 1f;
        }

        timeout = Mathf.Max(timeout, animationDuration / playbackSpeed);
        if (includesExecutor)
            timeout += UnlockExecutorCompletionGrace;

        return timeout;
    }

    private void HandleUnlockAnimationComplete(int version)
    {
        if (version != flowVersion || state != LockState.Unlocking)
            return;

        unlockAnimationCompleted = true;
        if (!unlockWaitsForExecutorLifecycle)
            FinishUnlock(version);
    }

    private void FinishUnlock(int version)
    {
        if (version != flowVersion || state != LockState.Unlocking)
            return;

        ClearUnlockTrackEntry();
        if (unlockRoutine != null)
        {
            StopCoroutine(unlockRoutine);
            unlockRoutine = null;
        }

        HideLockVisual();
        if (doorLockObject != null)
        {
            SetDoorLockTriggerHandlers(false);
            doorLockObject.SetActive(false);
        }
        unlockAnimationCompleted = true;
        unlockWaitsForExecutorLifecycle = false;
        state = LockState.DoorUnlocked;
        ApplyDoorVisualState();
        SetDoorGate(true);
        RefreshInputAvailability();
    }

    #endregion

    #region Key Flow

    /// <summary>
    /// 原子尝试收集钥匙。触发器只有在这里返回成功后才锁存自身状态，
    /// 因此暂停或状态切换同一帧不会出现“钥匙已标记但没有解锁”的半完成状态。
    /// </summary>
    public bool TryCollectKey(Level102KeyTrigger source)
    {
        ResolveReferences();
        if (!CanAcceptGameplayInput || keyCollected)
            return false;

        if (source != null && keyTrigger != null && source != keyTrigger)
            return false;

        NotifyKeyCollected();
        return keyCollected;
    }

    /// <summary>
    /// 由 Level102KeyTrigger 调用。钥匙只计数一次，随后播放原 Key 拾取链并启动锁解锁。
    /// </summary>
    public void NotifyKeyCollected()
    {
        if (!CanAcceptGameplayInput || keyCollected)
            return;

        keyCollected = true;
        PrepareDoorReplacementVisuals();
        EnsureFlyingKeyAboveDoorLock();
        if (keyTrigger != null)
            keyTrigger.enabled = false;
        DisableKeyColliders();

        // 拾取演出属于表现层，不能阻断真实钥匙已经收集后的解锁状态机。
        // 但默认必须复用标准 Key TriggerExecutor；执行器缺失时保留钥匙并报警，
        // 不能用“直接隐藏”伪造拾取完成，避免玩家看不到飞向锁的标准表现。
        bool standardPickupStarted = false;
        try
        {
            ConfigureKeyPickupTarget();
            if (useLocalKeyPickupFallback)
            {
                PlayLocalKeyPickup();
                BeginUnlockSequence();
            }
            else
            {
                ResolveReferences();
                if (keyObject != null)
                {
                    // 运行时重新取当前实例上的标准执行器，避免复制关卡的
                    // 序列化引用指向旧对象或在重试后持有已销毁实例。
                    TriggerExecutor standardExecutor = keyPickupExecutor;
                    if (standardExecutor == null ||
                        !IsInHierarchy(standardExecutor.transform, keyObject))
                    {
                        standardExecutor = keyObject.GetComponent<TriggerExecutor>() ??
                                           keyObject.GetComponentInChildren<TriggerExecutor>(true);
                    }

                    keyPickupExecutor = standardExecutor;
                    keyBobHandle?.StopMove();
                    if (standardExecutor != null)
                    {
                        standardExecutor.enabled = true;
                        standardExecutor.Execute();
                        standardPickupStarted = keyJumpToListener != null;
                    }
                    else
                    {
                        Debug.LogError(
                            "[Level102Ctrl] Key 预制体缺少标准 TriggerExecutor，已跳过隐藏钥匙；请检查 Key.prefab/实例。",
                            this);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[Level102Ctrl] 钥匙拾取演出失败，保持门锁定。{exception.GetType().Name}: {exception.Message}",
                this);
        }

        if (standardPickupStarted)
            StartKeyArrivalWait();
    }

    private void StartKeyArrivalWait()
    {
        StopKeyArrivalWait();
        keyArrivalRoutine = StartCoroutine(WaitForKeyArrival());
    }

    private IEnumerator WaitForKeyArrival()
    {
        // TriggerExecutor 在自己的 Update 中逐项推进。至少等待一帧，让标准
        // ItemJumpToListener 真正开始，再以它的完成状态作为开锁时机。
        yield return null;

        while (Application.isPlaying && runtimeInitialized && levelStarted && keyCollected)
        {
            if (keyJumpToListener == null)
            {
                keyArrivalRoutine = null;
                Debug.LogError("[Level102Ctrl] Key 缺少 ItemJumpToListener，无法确认钥匙已飞到锁。", this);
                yield break;
            }

            if (keyJumpToListener.IsDone)
                break;

            yield return null;
        }

        keyArrivalRoutine = null;
        if (!Application.isPlaying || !runtimeInitialized || !levelStarted || !keyCollected)
            yield break;

        BeginUnlockSequence();
    }

    private void StopKeyArrivalWait()
    {
        if (keyArrivalRoutine != null)
            StopCoroutine(keyArrivalRoutine);
        keyArrivalRoutine = null;
    }

    private void DisableKeyColliders()
    {
        if (keyObject == null)
            return;

        Collider2D[] colliders = keyObject.GetComponentsInChildren<Collider2D>(true);
        for (int index = 0; index < colliders.Length; index++)
        {
            if (colliders[index] != null)
                colliders[index].enabled = false;
        }
    }

    private void DisableDefaultKeyTriggerChain()
    {
        if (!disableDefaultKeyTrigger || keyObject == null)
            return;

        keyTriggerHandles = keyObject.GetComponentsInChildren<OnTrigger2DHandle>(true);
        for (int index = 0; index < keyTriggerHandles.Length; index++)
        {
            if (keyTriggerHandles[index] != null)
                keyTriggerHandles[index].enabled = false;
        }
    }

    /// <summary>
    /// 只播放共享 Key 拾取链中明确的表现部分。复制来的 Prefab 可能带有本关为空的
    /// 场景专用 Switch/Jump 目标；直接执行 TriggerExecutor 可能在销毁前卡住，或把
    /// 钥匙移动到序列化的兜底位置（世界原点）。
    /// </summary>
    private void PlayLocalKeyPickup()
    {
        if (keyObject == null)
        {
            AudioManager.PlaySound("getKey");
            return;
        }

        ResolveKeyPickupReferences();
        StopLocalKeyPickup();
        keyPickupStarted = true;

        // 保持钥匙在收集时的世界位置；拾取演出期间停止共享上下浮动 Tween，
        // 避免两个动画同时写入 Transform。
        keyBobHandle?.StopMove();
        if (keyPickupExecutor != null)
            keyPickupExecutor.enabled = false;

        if (keySfxListener != null)
            keySfxListener.Execute();
        else
            AudioManager.PlaySound("getKey");

        if (keyScaleListener != null)
            keyScaleListener.Execute();
        else if (keyVisual != null)
        {
            keyVisual.DOKill();
            keyVisual
                .DOScale(keyVisualInitialLocalScale * Mathf.Max(0f, localKeyScaleFactor),
                    Mathf.Max(0f, localKeyScaleDuration))
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        if (keyFadeListener != null)
            keyFadeListener.Execute();
        else if (keyVisualRenderer != null)
        {
            keyVisualRenderer.DOKill();
            keyVisualRenderer
                .DOFade(0f, Mathf.Max(0f, localKeyFadeDuration))
                .SetDelay(Mathf.Max(0f, localKeyFadeDelay))
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        keyPickupRoutine = StartCoroutine(HideLocalKeyAfterDelay());
    }

    private IEnumerator HideLocalKeyAfterDelay()
    {
        float delay = Mathf.Max(0f, localKeyPickupHideDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        else
            yield return null;

        keyPickupRoutine = null;
        if (!Application.isPlaying || !keyPickupStarted)
            yield break;

        keyPickupStarted = false;
        if (keyObject != null)
            keyObject.SetActive(false);
    }

    private void StopLocalKeyPickup()
    {
        if (keyPickupRoutine != null)
            StopCoroutine(keyPickupRoutine);
        keyPickupRoutine = null;
        keyPickupStarted = false;

        // UnityEngine.Object 被 Destroy 后，托管引用仍非 CLR null，`?.` 会继续
        // 访问其 transform 并抛 MissingReferenceException。必须使用 Unity 的
        // 重载空值判断，保证 Reset/OnDisable 清理能够继续完成并退订事件。
        if (keyObject != null)
            keyObject.transform.DOKill();
        if (keyVisual != null)
            keyVisual.DOKill();
        if (keyVisualRenderer != null)
            keyVisualRenderer.DOKill();
    }

    /// <summary>
    /// 等所有对象的 Start 都完成后再启动钥匙的浮动 Tween。
    /// Level102 的初始复位会清理 Key 根节点上的 Tween，而 MoveFloatHandle 只有在
    /// 自身 Start 中记录 startPosition 后才能安全接受 StartMove。
    /// </summary>
    private void ScheduleKeyBobStart()
    {
        if (keyBobStartRoutine != null)
            StopCoroutine(keyBobStartRoutine);

        keyBobStartRoutine = StartCoroutine(StartKeyBobNextFrame());
    }

    private IEnumerator StartKeyBobNextFrame()
    {
        yield return null;
        keyBobStartRoutine = null;

        // PlayCtrl 在复制关卡中可能要到几帧后才切入 PLAYING。不要把这段
        // 状态窗口当成失败，否则 PrepareInitialState 已经杀掉原始 Tween 后，
        // 钥匙会永久停在初始位置。只要关卡仍在运行且钥匙未被收集，就等待
        // 输入门打开；暂停/切场景时 levelStarted 会被 teardown 置为 false。
        while (Application.isPlaying && runtimeInitialized && levelStarted && !keyCollected)
        {
            if (CanAcceptGameplayInput && keyBobHandle != null &&
                keyBobHandle.isActiveAndEnabled)
            {
                keyBobHandle.StartMove();
                yield break;
            }

            yield return null;
        }
    }

    private void CaptureKeyPickupPose()
    {
        ResolveKeyPickupReferences();
        if (keyObject == null || hasKeyPickupPose)
            return;

        keyInitialLocalPosition = keyObject.transform.localPosition;
        keyInitialLocalRotation = keyObject.transform.localRotation;
        keyInitialLocalScale = keyObject.transform.localScale;
        if (keyVisual != null)
            keyVisualInitialLocalScale = keyVisual.localScale;
        if (keyVisualRenderer != null)
            keyVisualInitialColor = keyVisualRenderer.color;
        if (keySortingGroup != null)
        {
            keyInitialSortingLayerId = keySortingGroup.sortingLayerID;
            keyInitialSortingOrder = keySortingGroup.sortingOrder;
            hasKeySortingPose = true;
        }
        hasKeyPickupPose = true;
    }

    private void ResetKeyPickupVisualState()
    {
        StopLocalKeyPickup();
        if (keyObject == null)
            return;

        // 重启 MoveFloatHandle 前先重新激活对象，让 Unity 重建 Trigger 状态，
        // 这样重试时才能重新创建上下浮动 Tween。
        keyObject.SetActive(true);
        ResolveKeyPickupReferences();

        if (hasKeySortingPose && keySortingGroup != null)
        {
            keySortingGroup.sortingLayerID = keyInitialSortingLayerId;
            keySortingGroup.sortingOrder = keyInitialSortingOrder;
        }

        keyScaleListener?.ResetToInitial();
        if (hasKeyPickupPose)
        {
            keyObject.transform.localPosition = keyInitialLocalPosition;
            keyObject.transform.localRotation = keyInitialLocalRotation;
            keyObject.transform.localScale = keyInitialLocalScale;
            if (keyVisual != null)
                keyVisual.localScale = keyVisualInitialLocalScale;
            if (keyVisualRenderer != null)
                keyVisualRenderer.color = keyVisualInitialColor;
        }

        if (keyVisual != null)
            keyVisual.gameObject.SetActive(true);
        if (levelStarted && keyBobHandle != null && keyBobHandle.isActiveAndEnabled)
            keyBobHandle.StartMove();
    }

    private void ResolveKeyPickupReferences()
    {
        if (keyObject == null)
            return;

        if (keyBobHandle == null)
            keyBobHandle = keyObject.GetComponent<MoveFloatHandle>();
        if (keyScaleListener == null)
            keyScaleListener = keyObject.GetComponent<ItemScaleListener>();
        if (keySfxListener == null)
            keySfxListener = keyObject.GetComponentInChildren<PlaySfxListener>(true);
        if (keyFadeListener == null)
            keyFadeListener = keyObject.GetComponentInChildren<SpriteRenderFadeListener>(true);
        if (keyJumpToListener == null)
            keyJumpToListener = keyObject.GetComponent<ItemJumpToListener>();
        if (keyVisual == null)
            keyVisual = keyObject.transform.Find("Square");
        if (keyVisual == null)
        {
            SpriteRenderer childRenderer = keyObject.GetComponentInChildren<SpriteRenderer>(true);
            if (childRenderer != null)
                keyVisual = childRenderer.transform;
        }
        if (keyVisualRenderer == null && keyVisual != null)
            keyVisualRenderer = keyVisual.GetComponent<SpriteRenderer>();
        if (keyVisualRenderer == null)
            keyVisualRenderer = keyObject.GetComponentInChildren<SpriteRenderer>(true);
        if (keySortingGroup == null)
            keySortingGroup = keyObject.GetComponent<SortingGroup>() ??
                              keyObject.GetComponentInChildren<SortingGroup>(true);
    }

    /// <summary>
    /// Level102 把场景内钥匙的初始排序覆盖为 100，而正常锁使用 101。
    /// 只在钥匙飞向 LockPoint 的阶段把整个 Key SortingGroup 提到锁之上；
    /// 重试时 ResetKeyPickupVisualState 会恢复资源原值，避免影响待拾取状态。
    /// </summary>
    private void EnsureFlyingKeyAboveDoorLock()
    {
        ResolveKeyPickupReferences();
        if (keySortingGroup == null || doorLockObject == null)
            return;

        int lockLayerId;
        int lockOrder;
        SortingGroup lockSortingGroup = doorLockObject.GetComponent<SortingGroup>();
        if (lockSortingGroup != null)
        {
            lockLayerId = lockSortingGroup.sortingLayerID;
            lockOrder = lockSortingGroup.sortingOrder;
        }
        else
        {
            Renderer[] lockRenderers = doorLockObject.GetComponentsInChildren<Renderer>(true);
            Renderer topRenderer = null;
            for (int index = 0; index < lockRenderers.Length; index++)
            {
                Renderer candidate = lockRenderers[index];
                if (candidate == null)
                    continue;

                if (topRenderer == null || IsSortingAbove(
                        candidate.sortingLayerID, candidate.sortingOrder,
                        topRenderer.sortingLayerID, topRenderer.sortingOrder))
                    topRenderer = candidate;
            }

            if (topRenderer == null)
                return;

            lockLayerId = topRenderer.sortingLayerID;
            lockOrder = topRenderer.sortingOrder;
        }

        if (IsSortingAbove(keySortingGroup.sortingLayerID, keySortingGroup.sortingOrder,
                lockLayerId, lockOrder))
            return;

        keySortingGroup.sortingLayerID = lockLayerId;
        keySortingGroup.sortingOrder = lockOrder + 1;
    }

    /// <summary>
    /// The shared Key prefab leaves ItemJumpToListener.endTarget empty so each
    /// level can choose its own door. Bind this instance to the existing
    /// door_0/LockPoint before the standard TriggerExecutor runs.
    /// </summary>
    private void ConfigureKeyPickupTarget()
    {
        ResolveKeyPickupReferences();
        if (keyObject == null)
            return;

        Transform jumpTarget = doorRoot != null
            ? doorRoot.Find("LockPoint")
            : null;
        if (keyJumpToListener != null)
            keyJumpToListener.SetEndTarget(jumpTarget != null ? jumpTarget : doorRoot);
    }

    #endregion

    #region Hint Bubble Flow

    /// <summary>
    /// 仅为 Level102 补偿已获得答案时被公共逻辑关闭的气泡入口。
    /// 沿用公共等待/重试门槛和本关显示管线，不清除提示资格；拖动后不再补弹。
    /// </summary>
    private void UpdatePhysicalBubbleAvailability()
    {
        if (!CanAcceptGameplayInput || bubbleDragged || warningBubbleWasShown ||
            hasPendingHintBubble || hintBubbleRoutine != null || warningBubbleHideTween != null ||
            !Singleton<PlayCtrl>.IsInitialized)
            return;

        LevelRuntimeState attempt = PlayCtrl.Instance.Level?.CurrentLevelState;
        if (attempt == null || !attempt.HasTips || attempt.IsAttemptTransitioning)
            return;

        if (!hasLoadedPhysicalBubbleThresholds)
        {
            physicalBubbleTimeLimit = ConfigManager.RemoteConfig.GetShowTipsTimeLimit();
            physicalBubbleFailCount = ConfigManager.RemoteConfig.GetShowTipsFailCount();
            hasLoadedPhysicalBubbleThresholds = true;
        }

        float waitingTime = attempt.HasShownTipsWarning
            ? attempt.CurrentAttemptTimeSeconds - attempt.TipsWarningHiddenAtSeconds
            : attempt.CurrentAttemptTimeSeconds;
        bool retryThresholdReached = !attempt.HasShownTipsWarning &&
            attempt.FailCount + attempt.ResetCount >= physicalBubbleFailCount;
        if (!retryThresholdReached && waitingTime <= physicalBubbleTimeLimit)
            return;

        // 复用隐藏完成通知的计时闭环，仅排队本关气泡，不向其它 UI 派发显示事件。
        attempt.SwitchClickTips(true);
        hasPendingHintBubble = true;
        pendingHintDescription = string.IsNullOrWhiteSpace(globalTipsBubbleDescription)
            ? "U_Tips_Pop_Des_01"
            : globalTipsBubbleDescription;
        pendingHintDisappearTime = Mathf.Max(0f, globalTipsBubbleDisappearTime);
    }

    private void OnGamePlayPanelSwitchTips(GamePlayPanelSwitchTipsEvent evt)
    {
        if (!Application.isPlaying || evt == null)
            return;

        if (!evt.IsShow)
        {
            // 关闭事件用于清理 UI 的普通提示；未被拖动的物理泡也应随之结束。
            // 已被玩家接管的气泡属于本次尝试的垫脚物，继续保留到 Reset/Replay。
            hasPendingHintBubble = false;
            pendingHintDescription = null;
            pendingHintDisappearTime = 0f;
            ScheduleHintBubbleCleanup(bubbleDragged);
            return;
        }

        string description = string.IsNullOrWhiteSpace(globalTipsBubbleDescription)
            ? "U_Tips_Pop_Des_01"
            : globalTipsBubbleDescription;
        float disappearTime = Mathf.Max(0f, globalTipsBubbleDisappearTime);

        // 只处理首次 Start 前的初始化竞态。Teardown 会清回 false，因此同一
        // 控制器被重建/重新启用时不会把下一次尝试的首次提示事件丢掉。
        if (!hasStartedPlayLifecycle)
        {
            hasPendingHintBubble = true;
            pendingHintDescription = description;
            pendingHintDisappearTime = disappearTime;
            return;
        }

        // 玩家接管本关气泡后，保留物理实例，同时清理同一提示流程可能留下的
        // 普通气泡（订阅顺序不保证）。这个判断必须位于输入门禁之前。
        if (bubbleDragged)
        {
            ScheduleHintBubbleCleanup();
            return;
        }

        if (!CanAcceptGameplayInput)
        {
            // 每一轮门槛只派发一次显示事件；若事件恰好落在暂停或局部初始化窗口，
            // 保留 pending，待输入恢复后再生成物理泡，避免当前轮次丢失。
            hasPendingHintBubble = true;
            pendingHintDescription = description;
            pendingHintDisappearTime = disappearTime;
            return;
        }

        if (hintBubbleRoutine != null)
            StopCoroutine(hintBubbleRoutine);

        hintBubbleRoutine = StartCoroutine(ReplaceHintBubbleNextFrame(
            description,
            disappearTime));
    }

    private void ConsumePendingHintBubble()
    {
        if (!hasPendingHintBubble)
            return;

        // 保留 pending 数据，直到关卡进入可交互状态；Start/Update 会在
        // 后续帧再次尝试。Teardown 仍会显式清空，避免旧尝试污染重开。
        if (!CanAcceptGameplayInput)
            return;

        // GamePlayPanel 是由 PlayCtrl 异步创建的，可能还没有把共享
        // warningBubble 实例挂入层级。绑定失败时不要消费掉唯一的全局 Tips
        // 事件，等后续 Update 在面板就绪后再次尝试。
        if (!TryBindWarningBubble())
            return;

        string description = pendingHintDescription;
        float disappearTime = pendingHintDisappearTime;
        hasPendingHintBubble = false;
        pendingHintDescription = null;
        pendingHintDisappearTime = 0f;

        if (string.IsNullOrEmpty(description))
            return;

        if (hintBubbleRoutine != null)
            StopCoroutine(hintBubbleRoutine);

        hintBubbleRoutine = StartCoroutine(ReplaceHintBubbleNextFrame(
            description,
            Mathf.Max(0f, disappearTime)));
    }

    private void ScheduleHintBubbleCleanup(bool preserveActiveBubble = true)
    {
        if (hintBubbleRoutine != null)
            StopCoroutine(hintBubbleRoutine);

        hintBubbleRoutine = StartCoroutine(CleanupHintBubbleNextFrame(preserveActiveBubble));
    }

    private IEnumerator CleanupHintBubbleNextFrame(bool preserveActiveBubble)
    {
        // 先让其它提示处理器完成回调；订阅顺序不是约定，因此要等所有处理器返回后
        // 再清理普通气泡和 warningBubble。
        yield return null;
        hintBubbleRoutine = null;

        if (!Application.isPlaying)
            yield break;

        HideWarningBubble(!preserveActiveBubble);
        DestroyOtherHintBubbles();
    }

    private IEnumerator ReplaceHintBubbleNextFrame(string description, float disappearTime)
    {
        // 延迟一帧后清掉提示流程可能创建的普通跟随气泡，再接管 GamePlayPanel
        // 已有的 warningBubble 实例；不再实例化 Resources/HintBubble。
        yield return null;

        if (!Application.isPlaying)
        {
            hintBubbleRoutine = null;
            yield break;
        }

        if (!CanAcceptGameplayInput || bubbleDragged)
        {
            // 事件帧后可能立刻暂停，或玩家已接管当前气泡；两种情况都不能留下
            // BaseLevelCtrl 创建的普通实例。已拖动的 warningBubble 继续保留。
            HideWarningBubble(false);
            DestroyOtherHintBubbles();
            hintBubbleRoutine = null;
            yield break;
        }

        DestroyOtherHintBubbles();
        if (!TryBindWarningBubble())
        {
            // 面板还未就绪时保留 pending。PlayCtrl 已经把当前轮次置为展示中，
            // 因此必须由本关在后续帧补偿这次事件，不能等下一轮 Tips。
            hasPendingHintBubble = true;
            pendingHintDescription = description;
            pendingHintDisappearTime = Mathf.Max(0f, disappearTime);
            hintBubbleRoutine = null;
            yield break;
        }

        // Reset 后的失败次数门槛会在重新进入 PLAYING 的第一帧立即满足，而
        // GamePlayPanel.PlayShowPanelAnim 此时刚开始依次恢复按钮缩放。warningBubble
        // 是 tips_btn 的子节点；等父按钮恢复 authored 姿态再显示，避免达到次数门槛的重试
        // 的气泡继承 0/中间缩放而发生可见位置偏移。
        float parentWaitElapsed = 0f;
        while (!IsWarningBubbleParentReady() &&
               parentWaitElapsed < WarningBubbleParentReadyTimeout)
        {
            yield return null;
            parentWaitElapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 120f);

            if (!Application.isPlaying || !CanAcceptGameplayInput || bubbleDragged ||
                activeWarningBubbleObject == null)
            {
                hasPendingHintBubble = !bubbleDragged && Application.isPlaying;
                pendingHintDescription = hasPendingHintBubble ? description : null;
                pendingHintDisappearTime = hasPendingHintBubble
                    ? Mathf.Max(0f, disappearTime)
                    : 0f;
                hintBubbleRoutine = null;
                yield break;
            }
        }

        Canvas.ForceUpdateCanvases();
        ConfigureWarningBubblePhysics();
        ShowWarningBubble(disappearTime);
        hintBubbleRoutine = null;
    }

    private bool IsWarningBubbleParentReady()
    {
        if (activeWarningBubbleObject == null)
            return false;

        CustomButton tipsButton = activeWarningBubbleObject.GetComponentInParent<CustomButton>(true);
        if (tipsButton == null)
            return true;
        if (!tipsButton.gameObject.activeInHierarchy)
            return false;

        Vector3 scale = tipsButton.transform.localScale;
        return Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y)) >= WarningBubbleParentReadyScale;
    }

    /// <summary>
    /// 由 Level102HintBubbleDragItem 在首次拖动时调用。
    /// 物理接管会停止跟随角色和自动关闭；锁存后后续提示事件不会生成新气泡。
    /// </summary>
    public bool TryBeginBubbleDrag(Level102HintBubbleDragItem source)
    {
        if (!CanAcceptGameplayInput || source == null ||
            activeWarningBubbleObject == null || activeWarningBubbleDrag != source ||
            !warningBubbleDragReady ||
            !activeWarningBubbleObject.activeInHierarchy)
            return false;

        // 第一次有效拖动把气泡交给物理模式并抑制后续自动替换；同一气泡后续仍可
        // 重复拖动，方便玩家反复调整这个临时垫脚物。
        if (!bubbleDragged)
        {
            bubbleDragged = true;
            warningBubbleWasShown = true;
        }

        return true;
    }

    /// <summary>
    /// 兼容场景 UnityEvent/旧接线的无参入口；新的拖拽适配器使用
    /// TryBeginBubbleDrag 以便在锁存前完成输入门禁。
    /// </summary>
    public void OnBubbleDragged()
    {
        TryBeginBubbleDrag(activeWarningBubbleDrag);
    }

    private void ShowWarningBubble(float disappearTime)
    {
        if (activeWarningBubbleObject == null)
            return;

        warningBubbleShowTween?.Kill();
        warningBubbleShowTween = null;
        warningBubbleHideTween?.Kill();
        warningBubbleHideTween = null;
        warningBubbleDragReady = false;
        warningBubbleWasShown = true;
        RestoreWarningBubblePose();
        activeWarningBubbleObject.SetActive(true);
        Transform bubbleTransform = activeWarningBubbleObject.transform;
        Vector3 targetScale = hasWarningBubblePose
            ? warningBubbleInitialLocalScale
            : Vector3.one;
        bubbleTransform.localScale = Vector3.zero;

        if (activeWarningBubbleDrag != null)
            activeWarningBubbleDrag.ResetForAttempt(false);

        float safeExpandDuration = Mathf.Max(0f, warningBubbleExpandDuration);
        if (safeExpandDuration <= 0f)
        {
            bubbleTransform.localScale = targetScale;
            OnWarningBubbleExpandComplete(disappearTime);
        }
        else
        {
            warningBubbleShowTween = bubbleTransform
                .DOScale(targetScale, safeExpandDuration)
                .SetEase(warningBubbleExpandEase)
                .SetUpdate(true)
                .SetLink(activeWarningBubbleObject)
                .OnComplete(() =>
                {
                    warningBubbleShowTween = null;
                    OnWarningBubbleExpandComplete(disappearTime);
                });
        }

        RefreshInputAvailability();
    }

    private void OnWarningBubbleExpandComplete(float disappearTime)
    {
        if (!Application.isPlaying || activeWarningBubbleObject == null ||
            !activeWarningBubbleObject.activeInHierarchy || !warningBubbleWasShown)
            return;

        warningBubbleDragReady = true;
        if (activeWarningBubbleDrag != null)
            activeWarningBubbleDrag.EnableBubbleDrag();
        SyncWarningBubbleWorldCollider();

        // GamePlayPanel 入场动画可能在本关接管时仍把 warningBubble 的子 Image
        // 缩放为 0。展开完成后重试一次可视边界拟合，避免仅保留根 RectTransform
        // 的回退碰撞体，导致视觉可见区域无法点中/拖动。
        ScheduleWarningBubbleColliderFit();

        // Keep the same visible duration as the shared switcher. The countdown starts
        // after expansion so the animation does not consume the configured display time.
        if (!bubbleDragged)
        {
            float duration = useWarningSwitcherDisplayDuration && activeWarningTipsSwitcher != null
                ? activeWarningTipsSwitcher.DisplayDuration
                : disappearTime;
            ScheduleWarningBubbleAutoHide(Mathf.Max(0f, duration));
        }

        RefreshInputAvailability();
    }

    private Coroutine warningBubbleAutoHideRoutine;

    private void ScheduleWarningBubbleAutoHide(float duration)
    {
        if (warningBubbleAutoHideRoutine != null)
            StopCoroutine(warningBubbleAutoHideRoutine);

        warningBubbleAutoHideRoutine = StartCoroutine(HideWarningBubbleAfterDelay(duration));
    }

    private void ScheduleWarningBubbleColliderFit()
    {
        if (!fitWarningBubbleColliderToVisual ||
            !(activeWarningBubbleCollider is BoxCollider2D boxCollider))
            return;

        if (warningBubbleColliderFitRoutine != null)
            StopCoroutine(warningBubbleColliderFitRoutine);

        warningBubbleColliderFitRoutine = StartCoroutine(FitWarningBubbleColliderNextFrames(boxCollider));
    }

    private IEnumerator FitWarningBubbleColliderNextFrames(BoxCollider2D boxCollider)
    {
        const float timeout = 1f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            yield return null;
            elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 120f);

            if (!Application.isPlaying || boxCollider == null ||
                activeWarningBubbleObject == null ||
                !activeWarningBubbleObject.activeInHierarchy ||
                activeWarningBubbleCollider != boxCollider)
            {
                warningBubbleColliderFitRoutine = null;
                yield break;
            }

            if (FitWarningBubbleColliderToVisual(boxCollider))
            {
                warningBubbleColliderFitRoutine = null;
                yield break;
            }
        }

        // Keep the fixed root-size fallback when the UI has no valid visual bounds.
        warningBubbleColliderFitRoutine = null;
    }

    private IEnumerator HideWarningBubbleAfterDelay(float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0f, duration);
        while (elapsed < safeDuration)
        {
            yield return null;
            elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 120f);
            if (bubbleDragged || !warningBubbleWasShown)
            {
                warningBubbleAutoHideRoutine = null;
                yield break;
            }
        }

        warningBubbleAutoHideRoutine = null;
        HideWarningBubble(true, notifyDisplayEnded: true);
    }

    private void HideWarningBubble(
        bool clearShownState,
        bool animate = true,
        bool notifyDisplayEnded = false)
    {
        if (warningBubbleAutoHideRoutine != null)
        {
            StopCoroutine(warningBubbleAutoHideRoutine);
            warningBubbleAutoHideRoutine = null;
        }

        bool shouldHide = activeWarningBubbleObject != null && (!bubbleDragged || clearShownState);
        if (shouldHide)
        {
            warningBubbleShowTween?.Kill();
            warningBubbleShowTween = null;
            warningBubbleHideTween?.Kill();
            warningBubbleHideTween = null;
            warningBubbleDragReady = false;
            if (activeWarningBubbleDrag != null)
                activeWarningBubbleDrag.DisableBubbleDrag();
            SetWarningBubblePhysicsActive(false);

            GameObject bubbleObject = activeWarningBubbleObject;
            Transform bubbleTransform = bubbleObject.transform;
            if (animate && bubbleObject.activeInHierarchy &&
                warningBubbleCollapseDuration > 0f)
            {
                warningBubbleHideTween = bubbleTransform
                    .DOScale(Vector3.zero, warningBubbleCollapseDuration)
                    .SetEase(warningBubbleCollapseEase)
                    .SetUpdate(true)
                    .SetLink(bubbleObject, LinkBehaviour.KillOnDisable)
                    .OnComplete(() =>
                    {
                        warningBubbleHideTween = null;
                        if (bubbleObject != null)
                            bubbleObject.SetActive(false);
                        if (notifyDisplayEnded)
                            activeWarningTipsSwitcher?.NotifyExternalDisplayEnded();
                    });
            }
            else
            {
                bubbleObject.SetActive(false);
                if (notifyDisplayEnded)
                    activeWarningTipsSwitcher?.NotifyExternalDisplayEnded();
            }
        }

        if (clearShownState)
        {
            warningBubbleWasShown = false;
            warningBubbleDragReady = false;
        }

        RefreshInputAvailability();
    }

    private bool TryBindWarningBubble()
    {
        GamePlayPanel panel = PlayCtrl.Instance != null ? PlayCtrl.Instance.CurrentGamePlayPanel : null;
        if (panel == null)
            return false;

        GamePlayPanelTipsSwitcher resolvedSwitcher = warningTipsSwitcher;
        if (resolvedSwitcher == null ||
            (resolvedSwitcher.transform != panel.transform && !resolvedSwitcher.transform.IsChildOf(panel.transform)))
        {
            resolvedSwitcher = panel.GetComponentInChildren<GamePlayPanelTipsSwitcher>(true);
        }

        GameObject resolvedTarget = warningBubbleObject;
        if (resolvedTarget == null ||
            (resolvedTarget.transform != panel.transform && !resolvedTarget.transform.IsChildOf(panel.transform)))
        {
            resolvedTarget = resolvedSwitcher != null ? resolvedSwitcher.TargetObject : null;
        }

        if (resolvedTarget == null)
        {
            Transform namedTarget = FindDescendantByName(panel.transform, "warningBubble");
            resolvedTarget = namedTarget != null ? namedTarget.gameObject : null;
        }

        if (resolvedSwitcher == null || resolvedTarget == null)
            return false;

        bool changedTarget = activeWarningBubbleObject != resolvedTarget;
        if (changedTarget && activeWarningBubbleObject != null)
        {
            if (warningBubbleExternalControl && activeWarningTipsSwitcher != null)
                activeWarningTipsSwitcher.SetExternalControl(false);
            warningBubbleExternalControl = false;
            ReleaseWarningBubbleBinding();
        }

        warningTipsSwitcher = resolvedSwitcher;
        warningBubbleObject = resolvedTarget;
        warningBubbleRect = warningBubbleObject.GetComponent<RectTransform>();
        warningBubbleInteractionRect = FindDescendantByName(warningBubbleObject.transform, "Image") as RectTransform;
        warningBubbleColliderRect = ResolveWarningBubbleColliderRect(warningBubbleObject);
        activeWarningTipsSwitcher = warningTipsSwitcher;
        activeWarningBubbleObject = warningBubbleObject;
        activeWarningBubbleRect = warningBubbleRect;

        if (!warningBubbleExternalControl || changedTarget)
        {
            // GamePlayPanel survives a level retry. Restore its persistent authored
            // pose before this new controller captures a local baseline; otherwise
            // the previous attempt's dragged position becomes the next attempt's
            // starting point.
            warningTipsSwitcher.ResetTargetPose();
            warningTipsSwitcher.SetExternalControl(true);
            warningBubbleExternalControl = true;
            CaptureWarningBubblePose();
        }

        if (changedTarget || activeWarningBubbleDrag == null || activeWarningBubbleCollider == null)
            ConfigureWarningBubblePhysics();
        return true;
    }

    private void ConfigureWarningBubblePhysics()
    {
        if (activeWarningBubbleObject == null)
            return;

        if (!addWarningBubblePhysicsComponents)
            return;

        EnsureWarningBubblePhysicsProxy();

        GameObject dragHost = warningBubbleInteractionRect != null
            ? warningBubbleInteractionRect.gameObject
            : activeWarningBubbleObject;
        if (warningBubbleDrag == null || warningBubbleDrag.gameObject != dragHost)
            warningBubbleDrag = dragHost.GetComponent<Level102HintBubbleDragItem>();
        if (warningBubbleDrag == null)
        {
            // Put the pointer handler on the Graphic that actually wins the UI
            // raycast. It still moves the warningBubble root via BindDragTarget.
            warningBubbleDrag = dragHost.AddComponent<Level102HintBubbleDragItem>();
        }

        activeWarningBubbleCollider = warningBubblePhysicsCollider;
        activeWarningBubbleRigidbody = warningBubblePhysicsRigidbody;
        activeWarningBubbleDrag = warningBubbleDrag;
        activeWarningBubbleDrag.BindController(this);
        Canvas warningBubbleCanvas = activeWarningBubbleObject.GetComponentInParent<Canvas>();
        activeWarningBubbleDrag.BindDragTarget(activeWarningBubbleRect);
        activeWarningBubbleDrag.BindInteractionRect(warningBubbleInteractionRect);
        activeWarningBubbleDrag.BindDragPlane(
            warningBubbleCanvas != null ? warningBubbleCanvas.transform as RectTransform : null);
        activeWarningBubbleDrag.enabled = true;
        warningBubbleDragReady = false;
        activeWarningBubbleDrag.ResetForAttempt(false);
        SetWarningBubblePhysicsActive(false);
    }

    /// <summary>
    /// 为屏幕 UI 气泡创建独立的场景世界碰撞代理。
    /// 调用边界：仅在 Level102 已绑定当前 GamePlayPanel warningBubble 后调用；
    /// 代理不得挂到 UI 层级，否则 Canvas 坐标仍会污染 Physics2D 世界坐标。
    /// </summary>
    private void EnsureWarningBubblePhysicsProxy()
    {
        if (warningBubblePhysicsProxy != null &&
            warningBubblePhysicsCollider != null &&
            warningBubblePhysicsRigidbody != null)
            return;

        DestroyWarningBubblePhysicsProxy();

        warningBubblePhysicsProxy = new GameObject("Level102_WarningBubblePhysicsProxy");
        warningBubblePhysicsProxy.transform.SetPositionAndRotation(
            new Vector3(0f, 0f, ResolveWarningBubblePhysicsPlaneZ()),
            Quaternion.identity);
        warningBubblePhysicsProxy.transform.localScale = Vector3.one;
        ApplyHintBubblePhysicsLayer(warningBubblePhysicsProxy);

        BoxCollider2D boxCollider = warningBubblePhysicsProxy.AddComponent<BoxCollider2D>();
        boxCollider.isTrigger = false;
        boxCollider.offset = Vector2.zero;
        boxCollider.size = Vector2.one;
        boxCollider.enabled = false;

        Rigidbody2D rigidbody = warningBubblePhysicsProxy.AddComponent<Rigidbody2D>();
        rigidbody.bodyType = RigidbodyType2D.Kinematic;
        rigidbody.simulated = false;
        rigidbody.gravityScale = 0f;
        rigidbody.mass = Mathf.Max(0.01f, warningBubbleMass);
        rigidbody.linearVelocity = Vector2.zero;
        rigidbody.angularVelocity = 0f;
        rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;

        warningBubblePhysicsCollider = boxCollider;
        warningBubblePhysicsRigidbody = rigidbody;
    }

    private void CaptureWarningBubblePose()
    {
        if (activeWarningBubbleObject == null)
            return;

        Transform bubbleTransform = activeWarningBubbleObject.transform;
        warningBubbleInitialLocalPosition = bubbleTransform.localPosition;
        if (bubbleTransform is RectTransform rectTransform)
            warningBubbleInitialAnchoredPosition = rectTransform.anchoredPosition;
        warningBubbleInitialLocalRotation = bubbleTransform.localRotation;

        Vector3 scale = bubbleTransform.localScale;
        // SetExternalControl normalizes the root to one. If a legacy switcher animation
        // left a zero scale before binding, keep one as the authored target scale.
        warningBubbleInitialLocalScale = scale.sqrMagnitude > 0.0001f ? scale : Vector3.one;
        hasWarningBubblePose = true;
    }

    private void RestoreWarningBubblePose()
    {
        if (!hasWarningBubblePose || activeWarningBubbleObject == null)
            return;

        Transform bubbleTransform = activeWarningBubbleObject.transform;
        if (bubbleTransform is RectTransform rectTransform)
        {
            rectTransform.anchoredPosition = warningBubbleInitialAnchoredPosition;
            Vector3 localPosition = rectTransform.localPosition;
            localPosition.z = warningBubbleInitialLocalPosition.z;
            rectTransform.localPosition = localPosition;
        }
        else
        {
            bubbleTransform.localPosition = warningBubbleInitialLocalPosition;
        }
        bubbleTransform.localRotation = warningBubbleInitialLocalRotation;
        bubbleTransform.localScale = warningBubbleInitialLocalScale;

        // 场景世界代理由 SyncWarningBubbleWorldCollider 从 UI 屏幕矩形重新投影；
        // 这里不能把 UI Transform.position 直接写入 Physics2D 坐标。
        SetWarningBubblePhysicsActive(false);
    }

    private void ReleaseWarningBubbleBinding()
    {
        warningBubbleShowTween?.Kill();
        warningBubbleShowTween = null;
        warningBubbleHideTween?.Kill();
        warningBubbleHideTween = null;
        if (activeWarningBubbleDrag != null)
            activeWarningBubbleDrag.DisableBubbleDrag();
        RestoreWarningBubblePose();

        // UI 拖拽组件位于面板实例上，销毁会延迟到帧末；这里仅关闭它。场景世界
        // 碰撞代理不属于面板，必须在本关退出时立即停用并销毁，避免跨尝试残留。
        if (activeWarningBubbleDrag != null)
            activeWarningBubbleDrag.enabled = false;
        DestroyWarningBubblePhysicsProxy();

        warningBubbleDrag = null;
        warningTipsSwitcher = null;
        warningBubbleObject = null;
        warningBubbleRect = null;
        warningBubbleInteractionRect = null;
        warningBubbleColliderRect = null;
        hasWarningBubblePose = false;
        ClearActiveWarningBubbleReferences();
    }

    private bool FitWarningBubbleColliderToVisual(BoxCollider2D boxCollider)
    {
        if (boxCollider == null || warningBubblePhysicsProxy == null)
            return false;

        RectTransform boundsRoot = fitWarningBubbleColliderToVisual &&
                                   warningBubbleColliderRect != null
            ? warningBubbleColliderRect
            : activeWarningBubbleRect;
        if (boundsRoot == null)
            return false;

        Camera worldCamera = ResolveWarningBubbleWorldCamera();
        if (worldCamera == null)
            return false;

        Canvas canvas = activeWarningBubbleObject != null
            ? activeWarningBubbleObject.GetComponentInParent<Canvas>()
            : null;
        Canvas rootCanvas = canvas != null && canvas.rootCanvas != null
            ? canvas.rootCanvas
            : canvas;
        Camera uiCamera = rootCanvas != null &&
                          rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        // 自动拟合使用 msg/title 文字矩形加配置边距，对齐策划红框，不包含气泡
        // 外轮廓和尾巴；显式关闭时保留根矩形回退。角点先转屏幕再投影到
        // 关卡 XY 平面，CanvasScaler、SafeArea 和父级按钮缩放不会混入物理坐标。
        RectTransform[] rectTransforms = fitWarningBubbleColliderToVisual
            ? boundsRoot.GetComponentsInChildren<RectTransform>(true)
            : new[] { boundsRoot };
        if (rectTransforms == null || rectTransforms.Length == 0)
            return false;

        Vector2 minimumScreen = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximumScreen = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        Vector3[] corners = new Vector3[4];
        bool hasBounds = false;

        for (int index = 0; index < rectTransforms.Length; index++)
        {
            RectTransform rectTransform = rectTransforms[index];
            if (rectTransform == null || !rectTransform.gameObject.activeSelf)
                continue;

            rectTransform.GetWorldCorners(corners);
            for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[cornerIndex]);
                minimumScreen = Vector2.Min(minimumScreen, screenPoint);
                maximumScreen = Vector2.Max(maximumScreen, screenPoint);
                hasBounds = true;
            }
        }

        if (!hasBounds)
            return false;

        float canvasScale = rootCanvas != null
            ? Mathf.Max(0.0001f, rootCanvas.scaleFactor)
            : 1f;
        Vector2 screenPadding = new Vector2(
            Mathf.Max(0f, warningBubbleColliderPadding.x) * canvasScale,
            Mathf.Max(0f, warningBubbleColliderPadding.y) * canvasScale);
        minimumScreen -= screenPadding;
        maximumScreen += screenPadding;

        float planeZ = ResolveWarningBubblePhysicsPlaneZ();
        if (!TryProjectScreenPointToLevelPlane(worldCamera, minimumScreen, planeZ, out Vector2 minimumWorld) ||
            !TryProjectScreenPointToLevelPlane(worldCamera, maximumScreen, planeZ, out Vector2 maximumWorld))
            return false;

        Vector2 minimum = Vector2.Min(minimumWorld, maximumWorld);
        Vector2 maximum = Vector2.Max(minimumWorld, maximumWorld);
        Vector2 visualSize = maximum - minimum;
        if (visualSize.x <= 0.01f || visualSize.y <= 0.01f ||
            float.IsInfinity(visualSize.x) || float.IsInfinity(visualSize.y) ||
            float.IsNaN(visualSize.x) || float.IsNaN(visualSize.y))
            return false;

        Vector2 center = (minimum + maximum) * 0.5f;
        warningBubblePhysicsProxy.transform.SetPositionAndRotation(
            new Vector3(center.x, center.y, planeZ),
            Quaternion.identity);
        warningBubblePhysicsProxy.transform.localScale = Vector3.one;
        boxCollider.offset = Vector2.zero;
        boxCollider.size = visualSize;

        if (warningBubblePhysicsRigidbody != null)
        {
            warningBubblePhysicsRigidbody.position = center;
            warningBubblePhysicsRigidbody.SetRotation(0f);
            warningBubblePhysicsRigidbody.linearVelocity = Vector2.zero;
            warningBubblePhysicsRigidbody.angularVelocity = 0f;
        }

        return true;
    }

    private bool SyncWarningBubbleWorldCollider()
    {
        if (!(activeWarningBubbleCollider is BoxCollider2D boxCollider) ||
            activeWarningBubbleRigidbody == null ||
            activeWarningBubbleObject == null ||
            !activeWarningBubbleObject.activeInHierarchy)
        {
            SetWarningBubblePhysicsActive(false);
            return false;
        }

        bool synced = FitWarningBubbleColliderToVisual(boxCollider);
        SetWarningBubblePhysicsActive(synced && warningBubbleWasShown && warningBubbleDragReady);
        return synced;
    }

    private void SetWarningBubblePhysicsActive(bool active)
    {
        if (activeWarningBubbleRigidbody != null)
        {
            activeWarningBubbleRigidbody.linearVelocity = Vector2.zero;
            activeWarningBubbleRigidbody.angularVelocity = 0f;
            activeWarningBubbleRigidbody.simulated = active;
        }

        if (activeWarningBubbleCollider != null)
            activeWarningBubbleCollider.enabled = active;

        if (!active)
            SetActorShadowSuppressedByWarningBubble(false);
    }

    private RectTransform ResolveWarningBubbleColliderRect(GameObject bubbleObject)
    {
        if (bubbleObject == null)
            return null;

        if (warningBubbleColliderRect != null &&
            IsInHierarchy(warningBubbleColliderRect.transform, bubbleObject))
            return warningBubbleColliderRect;

        // GamePlayPanel 将源 Prefab 的 title 重命名为 msg；兼容未覆写名称的
        // warningBubble 资源，最后才退回整张背景 Image。
        return FindDescendantByName(bubbleObject.transform, "msg") as RectTransform ??
               FindDescendantByName(bubbleObject.transform, "title") as RectTransform ??
               warningBubbleInteractionRect;
    }

    private void RefreshActorShadowForWarningBubble()
    {
        SetActorShadowSuppressedByWarningBubble(IsActorStandingOnWarningBubble());
    }

    private bool IsActorStandingOnWarningBubble()
    {
        if (!hideActorShadowOnWarningBubble || actorCollider == null ||
            activeWarningBubbleCollider == null || !activeWarningBubbleCollider.enabled ||
            activeWarningBubbleRigidbody == null || !activeWarningBubbleRigidbody.simulated)
            return false;

        if (!actorCollider.IsTouching(activeWarningBubbleCollider))
            return false;

        Bounds actorBounds = actorCollider.bounds;
        Bounds bubbleBounds = activeWarningBubbleCollider.bounds;
        float horizontalOverlap = Mathf.Min(actorBounds.max.x, bubbleBounds.max.x) -
                                  Mathf.Max(actorBounds.min.x, bubbleBounds.min.x);
        float topDelta = actorBounds.min.y - bubbleBounds.max.y;
        float tolerance = Mathf.Max(0f, warningBubbleTopContactTolerance);
        return horizontalOverlap > 0.01f &&
               actorBounds.center.y >= bubbleBounds.center.y &&
               topDelta >= -tolerance && topDelta <= tolerance;
    }

    private void SetActorShadowSuppressedByWarningBubble(bool suppressed)
    {
        if (actorShadowObject == null)
        {
            actorShadowSuppressedByBubble = false;
            return;
        }

        if (suppressed)
        {
            if (!actorShadowSuppressedByBubble)
                actorShadowActiveBeforeBubble = actorShadowObject.activeSelf;

            actorShadowSuppressedByBubble = true;
            if (actorShadowObject.activeSelf)
                actorShadowObject.SetActive(false);
            return;
        }

        if (!actorShadowSuppressedByBubble)
            return;

        actorShadowObject.SetActive(actorShadowActiveBeforeBubble);
        actorShadowSuppressedByBubble = false;
    }

    private Camera ResolveWarningBubbleWorldCamera()
    {
        Transform cameraTransform = baseLevelCtrl != null
            ? baseLevelCtrl.transform.Find("GameCamera")
            : null;
        Camera worldCamera = cameraTransform != null
            ? cameraTransform.GetComponent<Camera>()
            : null;
        if (worldCamera == null && baseLevelCtrl != null)
            worldCamera = baseLevelCtrl.GetComponentInChildren<Camera>(true);
        if (worldCamera == null)
            worldCamera = Camera.main;
        return worldCamera;
    }

    private float ResolveWarningBubblePhysicsPlaneZ()
    {
        if (actor != null)
            return actor.transform.position.z;

        return transform.position.z;
    }

    private static bool TryProjectScreenPointToLevelPlane(
        Camera worldCamera,
        Vector2 screenPoint,
        float planeZ,
        out Vector2 worldPoint)
    {
        worldPoint = default;
        if (worldCamera == null)
            return false;

        Ray ray = worldCamera.ScreenPointToRay(screenPoint);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));
        if (!plane.Raycast(ray, out float distance))
            return false;

        Vector3 hit = ray.GetPoint(distance);
        worldPoint = new Vector2(hit.x, hit.y);
        return true;
    }

    private void DestroyWarningBubblePhysicsProxy()
    {
        SetWarningBubblePhysicsActive(false);
        if (warningBubblePhysicsProxy != null)
        {
            warningBubblePhysicsProxy.SetActive(false);
            Destroy(warningBubblePhysicsProxy);
        }

        warningBubblePhysicsProxy = null;
        warningBubblePhysicsCollider = null;
        warningBubblePhysicsRigidbody = null;
        activeWarningBubbleCollider = null;
        activeWarningBubbleRigidbody = null;
    }

    private static Transform FindDescendantByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < children.Length; index++)
        {
            Transform child = children[index];
            if (child != null && string.Equals(child.name, objectName, StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    private void ConfigureHintBubblePhysics(HintBubble bubble)
    {
        if (bubble == null)
            return;

        GameObject bubbleObject = bubble.gameObject;
        DisableHintBubbleRootMotion(bubbleObject);
        Collider2D instanceCollider = bubbleObject.GetComponent<Collider2D>() ??
                                      bubbleObject.GetComponentInChildren<Collider2D>(true);
        // Inspector 引用可能指向旧场景实例或 Prefab 资源，不能把它们直接复用到
        // 新实例的气泡上。
        hintBubbleCollider = instanceCollider != null
            ? instanceCollider
            : (hintBubbleCollider != null && IsInHierarchy(hintBubbleCollider.transform, bubbleObject)
                ? hintBubbleCollider
                : null);
        if (hintBubbleCollider == null && addHintBubblePhysicsComponents)
            hintBubbleCollider = bubbleObject.AddComponent<BoxCollider2D>();

        if (hintBubbleCollider is BoxCollider2D boxCollider)
        {
            boxCollider.isTrigger = false;
            boxCollider.offset = hintBubbleColliderOffset;
            boxCollider.size = hintBubbleColliderSize;
            ScheduleHintBubbleColliderFit(bubble, boxCollider);
        }
        else if (hintBubbleCollider != null)
        {
            hintBubbleCollider.isTrigger = false;
        }

        Rigidbody2D instanceRigidbody = bubbleObject.GetComponent<Rigidbody2D>();
        hintBubbleRigidbody = instanceRigidbody != null
            ? instanceRigidbody
            : (hintBubbleRigidbody != null && IsInHierarchy(hintBubbleRigidbody.transform, bubbleObject)
                ? hintBubbleRigidbody
                : null);
        if (hintBubbleRigidbody == null && addHintBubblePhysicsComponents)
            hintBubbleRigidbody = bubbleObject.AddComponent<Rigidbody2D>();

        if (hintBubbleRigidbody != null)
        {
            hintBubbleRigidbody.bodyType = RigidbodyType2D.Kinematic;
            hintBubbleRigidbody.simulated = true;
            hintBubbleRigidbody.gravityScale = 0f;
            hintBubbleRigidbody.mass = Mathf.Max(0.01f, hintBubbleMass);
            hintBubbleRigidbody.linearVelocity = Vector2.zero;
            hintBubbleRigidbody.angularVelocity = 0f;
            hintBubbleRigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        hintBubbleDrag = bubbleObject.GetComponent<Level102HintBubbleDragItem>();
        if (hintBubbleDrag == null && addHintBubblePhysicsComponents)
            hintBubbleDrag = bubbleObject.AddComponent<Level102HintBubbleDragItem>();

        ApplyHintBubblePhysicsLayer(bubbleObject);

        activeHintBubbleCollider = hintBubbleCollider;
        activeHintBubbleRigidbody = hintBubbleRigidbody;
        activeHintBubbleDrag = hintBubbleDrag;
        if (activeHintBubbleDrag != null)
        {
            activeHintBubbleDrag.enabled = true;
            activeHintBubbleDrag.BindController(this);
            activeHintBubbleDrag.ResetForAttempt(CanAcceptGameplayInput);
        }

        RefreshInputAvailability();
    }

    /// <summary>
    /// 把运行时创建的气泡根节点和实际碰撞体切到同一物理层。
    /// 调用边界：仅在本关生成/配置提示气泡后调用；不修改共享 HintBubble 预制体。
    /// </summary>
    private void ApplyHintBubblePhysicsLayer(GameObject bubbleObject)
    {
        if (bubbleObject == null)
            return;

        int layer = ResolveHintBubblePhysicsLayer();
        if (layer < 0)
        {
            Debug.LogWarning("[Level102Ctrl] 气泡物理层无效，角色可能无法把气泡识别为地面。", this);
            return;
        }

        // 根节点承载运行时补齐的 Collider/Rigidbody；预制体若把 Collider 放在子节点，
        // 也必须同步设置子节点层，否则 Physics2D 的层矩阵仍会按 Default 过滤。
        bubbleObject.layer = layer;
        Collider2D[] colliders = bubbleObject.GetComponentsInChildren<Collider2D>(true);
        for (int index = 0; index < colliders.Length; index++)
        {
            if (colliders[index] != null)
                colliders[index].gameObject.layer = layer;
        }
    }

    private int ResolveHintBubblePhysicsLayer()
    {
        int configuredMask = hintBubblePhysicsLayer.value;
        int stuffLayer = LayerMask.NameToLayer("Stuff");

        // 老 Prefab 没有该序列化字段时可能读到 0；优先回退到项目约定的 Stuff。
        if (configuredMask == 0)
            configuredMask = stuffLayer >= 0 ? 1 << stuffLayer : 0;

        // Stuff 是角色默认 whatIsGround 的成员。即使 Inspector 误选了多个层，
        // 仍优先使用 Stuff，避免气泡退回 Default 后失去垫脚功能。
        if (stuffLayer >= 0 && (configuredMask & (1 << stuffLayer)) != 0)
            return stuffLayer;

        for (int layer = 0; layer < 32; layer++)
        {
            if ((configuredMask & (1 << layer)) != 0)
                return layer;
        }

        return -1;
    }

    private void DisableHintBubbleRootMotion(GameObject bubbleObject)
    {
        if (!disableHintBubbleRootMotion || bubbleObject == null)
            return;

        Animator[] animators = bubbleObject.GetComponentsInChildren<Animator>(true);
        for (int index = 0; index < animators.Length; index++)
        {
            if (animators[index] != null)
                animators[index].applyRootMotion = false;
        }
    }

    /// <summary>
    /// show 动画会先把气泡根节点缩放到 0。等待动画完成（或达到超时）再测量，
    /// 若动画没有产出有效边界就保留 Inspector 的固定碰撞体，避免生成只有 padding
    /// 大小的气泡。
    /// </summary>
    private void ScheduleHintBubbleColliderFit(HintBubble bubble, BoxCollider2D boxCollider)
    {
        if (!fitHintBubbleColliderToVisual || bubble == null || boxCollider == null)
            return;

        if (hintBubbleColliderFitRoutine != null)
            StopCoroutine(hintBubbleColliderFitRoutine);

        hintBubbleColliderFitRoutine = StartCoroutine(FitHintBubbleColliderNextFrames(bubble, boxCollider));
    }

    private IEnumerator FitHintBubbleColliderNextFrames(HintBubble bubble, BoxCollider2D boxCollider)
    {
        float delay = Mathf.Max(0f, hintBubbleColliderFitDelay);
        float timeout = Mathf.Max(delay, hintBubbleColliderFitTimeout);
        float elapsed = 0f;

        // Animator 的 show 状态约 0.4 秒完成。按非缩放时间计时，避免暂停时
        // 协程因 Time.deltaTime=0 永远等待；暂停期间仍保留固定碰撞体作为安全回退。
        while (elapsed < timeout)
        {
            yield return null;
            elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 120f);

            if (!Application.isPlaying || bubble == null || boxCollider == null ||
                activeHintBubble != bubble || !bubble.gameObject.activeInHierarchy)
            {
                hintBubbleColliderFitRoutine = null;
                yield break;
            }

            if (elapsed < delay || !IsHintBubbleVisualReady(bubble))
                continue;

            if (FitHintBubbleColliderToVisual(bubble, boxCollider))
            {
                hintBubbleColliderFitRoutine = null;
                yield break;
            }
        }

        // 未能读取有效渲染边界时不覆盖固定尺寸；它是可编辑且可工作的安全回退。
        hintBubbleColliderFitRoutine = null;
    }

    private bool IsHintBubbleVisualReady(HintBubble bubble)
    {
        if (bubble == null)
            return false;

        Vector3 scale = bubble.transform.localScale;
        // 气泡是 2D 物体，只用 XY 判断 show 动画完成；Z 轴可能被项目的
        // 2D 层级约定设为 0，不应因此阻止碰撞体适配。
        float minimumScale = Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        return minimumScale >= Mathf.Clamp01(hintBubbleColliderFitMinimumRootScale);
    }

    private bool IsInHierarchy(Transform candidate, GameObject root)
    {
        if (candidate == null || root == null)
            return false;

        Transform rootTransform = root.transform;
        return candidate == rootTransform || candidate.IsChildOf(rootTransform);
    }

    private bool FitHintBubbleColliderToVisual(HintBubble bubble, BoxCollider2D boxCollider)
    {
        if (!fitHintBubbleColliderToVisual || bubble == null || boxCollider == null)
            return false;

        Renderer[] renderers = bubble.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return false;

        Transform colliderTransform = boxCollider.transform;
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool hasBounds = false;

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            Bounds bounds = renderer.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };

            for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
            {
                Vector3 local = colliderTransform.InverseTransformPoint(corners[cornerIndex]);
                minimum = Vector2.Min(minimum, new Vector2(local.x, local.y));
                maximum = Vector2.Max(maximum, new Vector2(local.x, local.y));
                hasBounds = true;
            }
        }

        if (!hasBounds)
            return false;

        Vector2 visualSize = maximum - minimum;
        // 根 Animator 处于 scale=0 或尚未完成初始化时，Renderer.bounds 只会给出
        // 退化矩形。此时保留固定碰撞体，而不是把 padding 当成真实气泡大小。
        if (visualSize.x <= 0.01f || visualSize.y <= 0.01f ||
            float.IsInfinity(visualSize.x) || float.IsInfinity(visualSize.y) ||
            float.IsNaN(visualSize.x) || float.IsNaN(visualSize.y))
            return false;

        Vector2 padding = new Vector2(
            Mathf.Max(0f, hintBubbleColliderPadding.x),
            Mathf.Max(0f, hintBubbleColliderPadding.y));
        Vector2 size = maximum - minimum + padding * 2f;
        if (size.x <= 0f || size.y <= 0f ||
            float.IsInfinity(size.x) || float.IsInfinity(size.y) ||
            float.IsNaN(size.x) || float.IsNaN(size.y))
            return false;

        boxCollider.offset = (minimum + maximum) * 0.5f;
        boxCollider.size = size;
        return true;
    }

    private void DestroyOtherHintBubbles(bool preserveActiveBubble = false)
    {
        HintBubble activeToDestroy = activeHintBubble;
        if (!preserveActiveBubble && hintBubbleColliderFitRoutine != null)
        {
            StopCoroutine(hintBubbleColliderFitRoutine);
            hintBubbleColliderFitRoutine = null;
        }

        if (!preserveActiveBubble)
            ClearActiveHintBubbleReferences();

        var bubbles = new HashSet<HintBubble>();
        CollectHintBubbles(transform, bubbles);
        CollectHintBubbles(baseLevelCtrl != null ? baseLevelCtrl.transform : null, bubbles);
        CollectHintBubbles(hintBubbleParent, bubbles);

        foreach (HintBubble candidate in bubbles)
        {
            if (candidate == null || (preserveActiveBubble && candidate == activeToDestroy))
                continue;

            candidate.gameObject.SetActive(false);
            Destroy(candidate.gameObject);
        }

        if (!preserveActiveBubble && activeToDestroy != null && !bubbles.Contains(activeToDestroy))
        {
            activeToDestroy.gameObject.SetActive(false);
            Destroy(activeToDestroy.gameObject);
        }
    }

    private void CollectHintBubbles(Transform scope, HashSet<HintBubble> result)
    {
        if (scope == null || result == null)
            return;

        HintBubble[] bubbles = scope.GetComponentsInChildren<HintBubble>(true);
        for (int index = 0; index < bubbles.Length; index++)
        {
            if (bubbles[index] != null)
                result.Add(bubbles[index]);
        }
    }

    private void ClearActiveHintBubbleReferences()
    {
        if (hintBubbleColliderFitRoutine != null)
        {
            StopCoroutine(hintBubbleColliderFitRoutine);
            hintBubbleColliderFitRoutine = null;
        }

        activeHintBubble = null;
        activeHintBubbleDrag = null;
        activeHintBubbleCollider = null;
        activeHintBubbleRigidbody = null;
    }

    private void ClearActiveWarningBubbleReferences()
    {
        warningBubbleShowTween?.Kill();
        warningBubbleShowTween = null;
        warningBubbleHideTween?.Kill();
        warningBubbleHideTween = null;

        if (warningBubbleColliderFitRoutine != null)
        {
            StopCoroutine(warningBubbleColliderFitRoutine);
            warningBubbleColliderFitRoutine = null;
        }

        if (warningBubbleAutoHideRoutine != null)
        {
            StopCoroutine(warningBubbleAutoHideRoutine);
            warningBubbleAutoHideRoutine = null;
        }

        activeWarningBubbleObject = null;
        activeWarningBubbleRect = null;
        warningBubbleInteractionRect = null;
        activeWarningTipsSwitcher = null;
        activeWarningBubbleCollider = null;
        activeWarningBubbleRigidbody = null;
        activeWarningBubbleDrag = null;
        warningBubbleDragReady = false;
    }

    #endregion

    #region Pause, Reset And Teardown

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt == null)
            return;

        if (evt.NewState == GamePlayStateName.PLAYING)
        {
            localInputPaused = false;
            ResumeUnlockPresentation();
            RefreshInputAvailability();
            return;
        }

        PauseLocalInput();
    }

    private void OnGamePause(GamePauseEvent _)
    {
        PauseLocalInput();
    }

    private void OnGameResume(GameResumeEvent _)
    {
        localInputPaused = false;
        ResumeUnlockPresentation();
        RefreshInputAvailability();
    }

    private void OnGameReset(GameResetEvent _)
    {
        BeginTeardown();
    }

    private void OnGameReplay(GameReplayEvent _)
    {
        BeginTeardown();
    }

    private void PauseLocalInput()
    {
        localInputPaused = true;
        bool wasLockDragging = lockDragStarted ||
                               (lockDrag != null && lockDrag.IsDragging());

        // PlayCtrl_Pause 不修改 Time.timeScale；解锁渐隐和 DoorLock Spine 需要显式
        // 暂停，否则暂停面板打开期间仍会推进并可能提前放开门入口。
        if (state == LockState.Unlocking)
            PauseUnlockPresentation();

        if (state == LockState.Relocking)
        {
            flowVersion++;
            state = LockState.LockedIdle;
            ClearRelockTrackEntry();
            if (relockRoutine != null)
                StopCoroutine(relockRoutine);
            relockRoutine = null;
            lockFadeTween?.Kill();
            lockFadeTween = null;
            RestoreLockVisual();
            relockSpine?.PlayAnimation(relockIdleAnimationName, true, false);
        }

        // SetEnableDrag(false) 会同步触发 DragableItem.EndDrag；保留标记直到该回调
        // 返回，避免暂停事件先到本控制器时丢掉锁的复位路径。
        lockDrag?.SetEnableDrag(false);
        lockDragStarted = false;
        if (wasLockDragging)
            RestoreLockVisual();
    }

    private void PauseUnlockPresentation()
    {
        if (lockFadeTween != null && lockFadeTween.IsActive() && lockFadeTween.IsPlaying())
            lockFadeTween.Pause();

        if (doorLockSpine == null || doorLockSpine.AnimationState == null ||
            doorLockAnimationPaused)
            return;

        pausedDoorLockTimeScale = doorLockSpine.AnimationState.TimeScale;
        doorLockSpine.AnimationState.TimeScale = 0f;
        doorLockAnimationPaused = true;
    }

    private void ResumeUnlockPresentation()
    {
        if (doorLockAnimationPaused)
        {
            if (doorLockSpine != null && doorLockSpine.AnimationState != null)
                doorLockSpine.AnimationState.TimeScale = pausedDoorLockTimeScale;
            doorLockAnimationPaused = false;
        }

        if (lockFadeTween != null && lockFadeTween.IsActive())
            lockFadeTween.Play();
    }

    private void BeginTeardown()
    {
        // 必须先退订。Reset 期间任一资源清理若遇到 Unity 假 null 并抛异常，
        // 也不能让已销毁的 Level102Ctrl 继续留在全局事件列表中。
        EventsUtils.ResetEvents(ref eventSubscriber);
        SetActorShadowSuppressedByWarningBubble(false);
        StopDoorReplacementRoutine();
        levelStarted = false;
        localInputPaused = true;
        hasPendingHintBubble = false;
        pendingHintDescription = null;
        pendingHintDisappearTime = 0f;
        flowVersion++;
        unlockActivationStarted = false;
        unlockAnimationObserved = false;
        unlockAnimationCompleted = false;
        unlockWaitsForExecutorLifecycle = false;
        lockDragStarted = false;
        ClearRelockTrackEntry();
        ClearUnlockTrackEntry();
        StopDoorLockActivationRoutine();

        // 关卡实例销毁/重建前恢复被本关暂存的 Spine 时间倍率，避免复用对象
        // 在下一次尝试中保持 0 倍速。
        if (doorLockAnimationPaused)
        {
            if (doorLockSpine != null && doorLockSpine.AnimationState != null)
                doorLockSpine.AnimationState.TimeScale = pausedDoorLockTimeScale;
            doorLockAnimationPaused = false;
        }

        lockFadeTween?.Kill();
        lockFadeTween = null;
        if (relockRoutine != null)
            StopCoroutine(relockRoutine);
        if (unlockRoutine != null)
            StopCoroutine(unlockRoutine);
        if (hintBubbleRoutine != null)
            StopCoroutine(hintBubbleRoutine);
        if (hintBubbleColliderFitRoutine != null)
            StopCoroutine(hintBubbleColliderFitRoutine);
        if (warningBubbleColliderFitRoutine != null)
            StopCoroutine(warningBubbleColliderFitRoutine);
        if (warningBubbleAutoHideRoutine != null)
            StopCoroutine(warningBubbleAutoHideRoutine);
        warningBubbleShowTween?.Kill();
        warningBubbleShowTween = null;
        warningBubbleHideTween?.Kill();
        warningBubbleHideTween = null;
        if (keyBobStartRoutine != null)
            StopCoroutine(keyBobStartRoutine);
        StopKeyArrivalWait();
        StopLocalKeyPickup();
        relockRoutine = null;
        unlockRoutine = null;
        hintBubbleRoutine = null;
        hintBubbleColliderFitRoutine = null;
        warningBubbleColliderFitRoutine = null;
        warningBubbleAutoHideRoutine = null;
        keyBobStartRoutine = null;

        // 其它提示路径可能在本关层级下创建普通跟随气泡；重开时清理本关全部气泡，
        // 不能只清理本控制器替换过的实例。
        DestroyOtherHintBubbles();
        HideWarningBubble(true, false);
        if (warningBubbleExternalControl && warningTipsSwitcher != null)
            warningTipsSwitcher.SetExternalControl(false);
        warningBubbleExternalControl = false;
        hasStartedPlayLifecycle = false;
        ReleaseWarningBubbleBinding();

        if (lockDrag != null)
            lockDrag.SetEnableDrag(false);
        ApplyDoorVisualState();
        SetDoorLockTriggerHandlers(false);
        SetDoorGate(false);
    }

    private void ClearRelockTrackEntry()
    {
        if (relockTrackEntry != null && relockCompleteHandler != null)
            relockTrackEntry.Complete -= relockCompleteHandler;

        relockTrackEntry = null;
        relockCompleteHandler = null;
    }

    private void ClearUnlockTrackEntry()
    {
        if (unlockTrackEntry != null && unlockCompleteHandler != null)
            unlockTrackEntry.Complete -= unlockCompleteHandler;

        unlockTrackEntry = null;
        unlockCompleteHandler = null;
    }

    private void StopDoorLockActivationRoutine()
    {
        if (doorLockActivationRoutine != null)
            StopCoroutine(doorLockActivationRoutine);

        doorLockActivationRoutine = null;
    }

    #endregion

    #region Setup And Collision Gate

    private void ResolveReferences()
    {
        if (baseLevelCtrl == null)
            baseLevelCtrl = GetComponent<BaseLevelCtrl>() ?? GetComponentInParent<BaseLevelCtrl>();

        if (actor == null)
            actor = baseLevelCtrl != null ? baseLevelCtrl.Actor : null;
        if (actor == null)
            actor = GetComponentInChildren<ActorCtrl>(true);

        if (actor != null)
        {
            if (actorCollider == null)
                actorCollider = actor.GetComponent<Collider2D>();
            if (actorShadowObject == null)
            {
                Transform shadowTransform = actor.transform.Find("Shadow");
                actorShadowObject = shadowTransform != null ? shadowTransform.gameObject : null;
            }
        }

        if (doorRoot == null)
            doorRoot = transform.Find("door_0") ?? transform;
        if (doorCollider == null && doorRoot != null)
            doorCollider = doorRoot.GetComponent<Collider2D>();
        if (doorTriggerHandle == null && doorRoot != null)
            doorTriggerHandle = doorRoot.GetComponent<OnTrigger2DHandle>();

        if (lockDrag == null && doorRoot != null)
            lockDrag = doorRoot.GetComponentInChildren<Level102LockDragItem>(true);
        if (lockDrag == null)
            lockDrag = GetComponentInChildren<Level102LockDragItem>(true);
        if (lockRenderer == null && lockDrag != null)
            lockRenderer = lockDrag.GetComponent<SpriteRenderer>();
        if (relockSpine == null && doorRoot != null)
            relockSpine = doorRoot.Find("body")?.GetComponent<ActorSpineCtrl>();
        if (standardDoorVisual == null && doorRoot != null)
            standardDoorVisual = doorRoot.Find("door")?.gameObject;
        if (legacyDoorEffectVisual == null && doorRoot != null)
            legacyDoorEffectVisual = doorRoot.Find("effect")?.gameObject;

        if (doorLockObject == null)
            doorLockObject = FindChildObjectByName("DoorLock");
        ResolveDoorLockReferences();

        if (keyObject == null)
            keyObject = FindChildObjectByName("Key");
        if (keyTrigger == null && keyObject != null)
            keyTrigger = keyObject.GetComponentInChildren<Level102KeyTrigger>(true);
        if (keyCollider == null && keyTrigger != null)
            keyCollider = keyTrigger.GetComponent<Collider2D>();
        if (keyCollider == null && keyObject != null)
            keyCollider = keyObject.GetComponentInChildren<Collider2D>(true);
        if (keyPickupExecutor == null && keyObject != null)
            keyPickupExecutor = keyObject.GetComponent<TriggerExecutor>();

        if (boxBreak == null)
        {
            Level102BoxBreakOnActor[] candidates = GetComponentsInChildren<Level102BoxBreakOnActor>(true);
            for (int index = 0; index < candidates.Length; index++)
            {
                Level102BoxBreakOnActor candidate = candidates[index];
                if (candidate != null &&
                    !string.Equals(candidate.gameObject.name, "Box_2", StringComparison.OrdinalIgnoreCase))
                {
                    boxBreak = candidate;
                    break;
                }
            }
        }

        if (hintBubbleParent == null)
            hintBubbleParent = transform;
        if (doorRoot != null)
            doorTriggerHandles = doorRoot.GetComponentsInChildren<OnTrigger2DHandle>(true);

        CaptureDoorColliderPose();
    }

    private void ResolveDoorLockReferences()
    {
        if (doorLockObject == null)
            return;

        if (doorLockSpine == null)
            doorLockSpine = doorLockObject.GetComponentInChildren<ActorSpineCtrl>(true);
        if (doorLockExecutor == null)
            doorLockExecutor = doorLockObject.GetComponent<BaseItemExecutor>();

        doorLockTriggerHandles = doorLockObject.GetComponentsInChildren<OnTrigger2DHandle>(true);
        SetDoorLockTriggerHandlers(false);
    }

    /// <summary>
    /// DoorLock 预制体根节点自带 OnTrigger2DHandle，并把进入碰撞体转发给
    /// BaseItemExecutor。Level102 只允许真实钥匙显式启动解锁，因此其整个子树的
    /// 触发转发器在本关生命周期内始终关闭；invokeDoorLockExecutor 不受影响。
    /// </summary>
    private void SetDoorLockTriggerHandlers(bool enabled)
    {
        if (doorLockTriggerHandles == null)
            return;

        for (int index = 0; index < doorLockTriggerHandles.Length; index++)
        {
            if (doorLockTriggerHandles[index] != null)
                doorLockTriggerHandles[index].enabled = enabled;
        }
    }

    private GameObject FindChildObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < children.Length; index++)
        {
            Transform candidate = children[index];
            if (candidate != null && string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                return candidate.gameObject;
        }

        return null;
    }

    private void CaptureLockPose()
    {
        if (lockDrag == null)
            return;

        lockInitialLocalPosition = lockDrag.transform.localPosition;
        lockInitialLocalRotation = lockDrag.transform.localRotation;
        lockInitialLocalScale = lockDrag.transform.localScale;
        if (lockRenderer != null)
            lockInitialColor = lockRenderer.color;
        hasLockPose = true;
    }

    private void CaptureDoorColliderPose()
    {
        if (hasDoorColliderPose || doorCollider == null)
            return;

        // 保留资源中记录的状态用于诊断和销毁收尾，但锁定玩法不直接沿用资源的
        // Trigger 值：Level102 初始必须是实体门，避免角色未拿钥匙就穿门。
        doorColliderInitialEnabled = doorCollider.enabled;
        doorColliderInitialIsTrigger = doorCollider.isTrigger;
        hasDoorColliderPose = true;
    }

    private void PrepareInitialState()
    {
        state = LockState.LockedIdle;
        flowVersion++;
        lockDragStarted = false;
        keyCollected = false;
        replacementDoorVisible = false;
        fakeDoorRetired = false;
        StopDoorReplacementRoutine();
        bubbleDragged = false;
        warningBubbleWasShown = false;
        HideWarningBubble(true, false);
        unlockActivationStarted = false;
        unlockAnimationObserved = false;
        unlockAnimationCompleted = false;
        unlockWaitsForExecutorLifecycle = false;

        ClearRelockTrackEntry();
        ClearUnlockTrackEntry();
        StopDoorLockActivationRoutine();
        lockFadeTween?.Kill();
        lockFadeTween = null;

        if (relockRoutine != null)
            StopCoroutine(relockRoutine);
        if (unlockRoutine != null)
            StopCoroutine(unlockRoutine);
        StopKeyArrivalWait();
        relockRoutine = null;
        unlockRoutine = null;

        DestroyOtherHintBubbles();
        RestoreLockVisual();
        ApplyNormalDoorAlignment();
        ApplyDoorVisualState();
        relockSpine?.PlayAnimation(relockIdleAnimationName, true, false);

        if (doorLockObject != null)
        {
            SetDoorLockTriggerHandlers(false);
            doorLockObject.SetActive(false);
        }

        ResetKeyPickupVisualState();
        if (keyPickupExecutor != null)
            keyPickupExecutor.enabled = !useLocalKeyPickupFallback;
        if (keyTrigger != null)
        {
            keyTrigger.enabled = true;
            keyTrigger.ResetForAttempt();
        }
        DisableDefaultKeyTriggerChain();

        SetDoorGate(false);
        RefreshInputAvailability();
    }

    private void SetDoorGate(bool unlocked)
    {
        if (doorTriggerHandles == null || doorTriggerHandles.Length == 0)
        {
            if (doorTriggerHandle != null)
                doorTriggerHandle.enabled = unlocked;
        }
        else
        {
            for (int index = 0; index < doorTriggerHandles.Length; index++)
            {
                if (doorTriggerHandles[index] != null)
                    doorTriggerHandles[index].enabled = unlocked;
            }
        }

        if (doorCollider == null)
            return;

        CaptureDoorColliderPose();

        // 只有复制 Prefab 或其它监听器实际改写门状态时才重建接触对；每帧重新启用
        // 会产生重复 Trigger 回调，并让角色在门边抖动。
        bool desiredTrigger = unlocked;
        if (doorCollider.enabled && doorCollider.isTrigger == desiredTrigger)
            return;

        // 修改 isTrigger 前先禁用，让 Unity 按正确语义重建接触对。锁定和解锁状态
        // 都保持 Collider 启用：前者是实体阻挡，后者是 BaseLevelCtrl 的成功触发器。
        doorCollider.enabled = false;
        doorCollider.isTrigger = desiredTrigger;
        doorCollider.enabled = true;
    }

    private void RefreshInputAvailability()
    {
        if (lockDrag != null && !lockDragStarted)
        {
            bool canDrag = CanAcceptGameplayInput &&
                           state == LockState.LockedIdle && !keyCollected;
            lockDrag.SetEnableDrag(canDrag);
        }

        if (activeWarningBubbleDrag != null)
        {
            bool canDragBubble = CanAcceptGameplayInput &&
                                 warningBubbleDragReady &&
                                 activeWarningBubbleObject != null &&
                                 activeWarningBubbleObject.activeInHierarchy;
            activeWarningBubbleDrag.SetBubbleDragEnabled(canDragBubble);
        }
    }

    private void ApplyDoorVisualState()
    {
        bool showStandardDoor = replacementDoorVisible ||
                                state == LockState.Unlocking ||
                                state == LockState.DoorUnlocked;
        bool showLockedDoor = !fakeDoorRetired && state != LockState.DoorUnlocked;
        GameObject lockedDoorVisual = relockSpine != null ? relockSpine.gameObject : null;
        if (lockedDoorVisual != null && lockedDoorVisual.activeSelf != showLockedDoor)
            lockedDoorVisual.SetActive(showLockedDoor);
        if (standardDoorVisual != null && standardDoorVisual.activeSelf != showStandardDoor)
            standardDoorVisual.SetActive(showStandardDoor);
        if (legacyDoorEffectVisual != null && legacyDoorEffectVisual.activeSelf != showStandardDoor)
            legacyDoorEffectVisual.SetActive(showStandardDoor);
    }

    /// <summary>
    /// 吃到钥匙的当帧先把正常门和正常 DoorLock 放到假门上层，
    /// 保留一个完整渲染帧后才隐藏假门/假锁。这个顺序避免任何空门帧。
    /// </summary>
    private void PrepareDoorReplacementVisuals()
    {
        ResolveReferences();
        replacementDoorVisible = true;
        fakeDoorRetired = false;

        ApplyNormalDoorAlignment();
        if (standardDoorVisual != null)
            standardDoorVisual.SetActive(true);

        if (doorLockObject != null)
        {
            doorLockObject.SetActive(true);
            ResolveDoorLockReferences();
            SetDoorLockTriggerHandlers(false);
            PlayDoorLockIdleWithoutMix();
        }

        EnsureReplacementVisualsAboveFakeDoor();
        ApplyDoorVisualState();

        StopDoorReplacementRoutine();
        doorReplacementRoutine = StartCoroutine(RetireFakeDoorAfterReplacementFrame());
    }

    private IEnumerator RetireFakeDoorAfterReplacementFrame()
    {
        // yield null 使当前帧先渲染“正常门/锁在上，假门在下”，
        // 再在下一帧退役假表现。
        yield return null;
        doorReplacementRoutine = null;
        if (!Application.isPlaying || !runtimeInitialized || !levelStarted ||
            !keyCollected || !replacementDoorVisible)
            yield break;

        fakeDoorRetired = true;
        HideLockVisual();
        ApplyDoorVisualState();

        // 局部拾取兜底可能在重叠帧内就进入 Unlocking。
        // 退役假锁会 Kill 其渐隐 Tween，因此需要在这里接续真实解锁链。
        if (state == LockState.Unlocking && !unlockActivationStarted)
            ActivateDoorLock(flowVersion);
    }

    private void StopDoorReplacementRoutine()
    {
        if (doorReplacementRoutine != null)
            StopCoroutine(doorReplacementRoutine);
        doorReplacementRoutine = null;
    }

    private void ApplyNormalDoorAlignment()
    {
        ApplyNormalDoorAlignment(standardDoorVisual != null ? standardDoorVisual.transform : null);
        ApplyNormalDoorAlignment(legacyDoorEffectVisual != null ? legacyDoorEffectVisual.transform : null);
    }

    private void ApplyNormalDoorAlignment(Transform visual)
    {
        if (visual == null)
            return;

        visual.localPosition = normalDoorAlignedLocalPosition;
        visual.localScale = normalDoorAlignedLocalScale;
    }

    private void PlayDoorLockIdleWithoutMix()
    {
        if (doorLockSpine == null || string.IsNullOrWhiteSpace(doorLockIdleAnimationName))
            return;

        try
        {
            Spine.TrackEntry idleTrack = doorLockSpine.PlayAnimation(
                doorLockIdleAnimationName, true, false);
            if (idleTrack != null)
                idleTrack.MixDuration = 0f;
        }
        catch (Exception exception)
        {
            // 首次激活的 Spine 可能要到下一帧才完成数据初始化；
            // DoorLock 自身 Awake 仍会播放默认 lock_idle，真实 unlock 链也有就绪等待。
            Debug.LogWarning(
                $"[Level102Ctrl] DoorLock 待机动画暂未就绪：{exception.Message}", this);
        }
    }

    private void EnsureReplacementVisualsAboveFakeDoor()
    {
        Renderer fakeDoorRenderer = relockSpine != null
            ? relockSpine.GetComponentInChildren<Renderer>(true)
            : null;
        Renderer normalDoorRenderer = standardDoorVisual != null
            ? standardDoorVisual.GetComponentInChildren<Renderer>(true)
            : null;
        EnsureRendererAbove(normalDoorRenderer, fakeDoorRenderer);

        SortingGroup fakeDoorGroup = doorRoot != null
            ? doorRoot.GetComponent<SortingGroup>()
            : null;
        SortingGroup normalLockGroup = doorLockObject != null
            ? doorLockObject.GetComponent<SortingGroup>()
            : null;
        if (fakeDoorGroup == null || normalLockGroup == null ||
            IsSortingAbove(normalLockGroup.sortingLayerID, normalLockGroup.sortingOrder,
                fakeDoorGroup.sortingLayerID, fakeDoorGroup.sortingOrder))
            return;

        normalLockGroup.sortingLayerID = fakeDoorGroup.sortingLayerID;
        normalLockGroup.sortingOrder = fakeDoorGroup.sortingOrder + 1;
    }

    private static void EnsureRendererAbove(Renderer upper, Renderer lower)
    {
        if (upper == null || lower == null ||
            IsSortingAbove(upper.sortingLayerID, upper.sortingOrder,
                lower.sortingLayerID, lower.sortingOrder))
            return;

        upper.sortingLayerID = lower.sortingLayerID;
        upper.sortingOrder = lower.sortingOrder + 1;
    }

    private static bool IsSortingAbove(int upperLayerId, int upperOrder,
        int lowerLayerId, int lowerOrder)
    {
        int upperLayerValue = SortingLayer.GetLayerValueFromID(upperLayerId);
        int lowerLayerValue = SortingLayer.GetLayerValueFromID(lowerLayerId);
        return upperLayerValue > lowerLayerValue ||
               upperLayerValue == lowerLayerValue && upperOrder > lowerOrder;
    }

    private void DisableLegacyController()
    {
        if (!disableLegacyLevel101Controller)
            return;

        Level101Ctrl legacy = GetComponent<Level101Ctrl>();
        if (legacy != null && legacy.enabled)
            legacy.enabled = false;

        Level101LockDragItem[] oldLocks = GetComponentsInChildren<Level101LockDragItem>(true);
        for (int index = 0; index < oldLocks.Length; index++)
        {
            if (oldLocks[index] != null)
            {
                oldLocks[index].SetEnableDrag(false);
                oldLocks[index].enabled = false;
            }
        }

        Level101BoxDragItem[] oldBoxes = GetComponentsInChildren<Level101BoxDragItem>(true);
        for (int index = 0; index < oldBoxes.Length; index++)
        {
            if (oldBoxes[index] != null)
            {
                oldBoxes[index].SetEnableDrag(false);
                oldBoxes[index].enabled = false;
            }
        }
    }

    private bool IsGameplayInputOpen()
    {
        if (!Application.isPlaying || localInputPaused)
            return false;

        if (!Singleton<PlayCtrl>.IsInitialized)
            return true;

        PlayCtrl playCtrl = PlayCtrl.Instance;
        return !playCtrl.IsInitialized || playCtrl.CheckState(GamePlayStateName.PLAYING);
    }

    #endregion
}

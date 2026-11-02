using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.ItemEvent;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level081「水果忍者」私有控制器：管理泡泡入场、切水果、炸弹失败和两条开锁路线。
/// </summary>
public sealed class Level81Ctrl : MonoBehaviour, IEventSender
{
    #region Types

    [Serializable]
    private struct FruitDefinition
    {
        [Tooltip("水果完整贴图。")]
        [SerializeField] private Sprite sprite;

        [Tooltip("切开该水果时粒子喷溅使用的颜色。")]
        [SerializeField] private Color juiceColor;

        public Sprite Sprite => sprite;
        public Color JuiceColor => juiceColor;
    }

    private sealed class RuntimeEffect
    {
        private readonly bool[] resumeAfterPause;

        public RuntimeEffect(GameObject root, ParticleSystem[] systems, float lifetime)
        {
            Root = root;
            Systems = systems ?? Array.Empty<ParticleSystem>();
            RemainingLifetime = Mathf.Max(0f, lifetime);
            resumeAfterPause = new bool[Systems.Length];
        }

        public GameObject Root { get; }
        public ParticleSystem[] Systems { get; }
        public float RemainingLifetime { get; set; }
        public bool PausedByLevel { get; private set; }

        public void SetPaused(bool paused)
        {
            if (PausedByLevel == paused)
                return;

            PausedByLevel = paused;
            for (int index = 0; index < Systems.Length; index++)
            {
                ParticleSystem system = Systems[index];
                if (system == null)
                    continue;

                if (paused)
                {
                    resumeAfterPause[index] = system.isPlaying;
                    if (resumeAfterPause[index])
                        system.Pause(false);
                }
                else
                {
                    if (resumeAfterPause[index] && system.isPaused)
                        system.Play(false);
                    resumeAfterPause[index] = false;
                }
            }
        }
    }

    private enum PointerMode
    {
        None,
        Touch,
        EditorMouse
    }

    #endregion

    #region Inspector References

    [Header("Level References")]
    [Tooltip("关卡基础控制器，用于锁定和恢复玩家操作。")]
    [SerializeField] private BaseLevelCtrl baseLevelCtrl;

    [Tooltip("替换通用 ActorCtrl 的 Level081 专用角色控制器。")]
    [SerializeField] private Level81ActorCtrl actor;

    [Tooltip("图1中的泡泡根节点；玩法开始后整体隐藏。")]
    [SerializeField] private Transform bubbleRoot;

    [Tooltip("图1木台的实体碰撞体；角色必须真正站到该台面上才开始玩法。")]
    [SerializeField] private Collider2D bubblePlatformCollider;

    [Tooltip("玩法期间可直接切开的图片锁。")]
    [SerializeField] private SpriteRenderer imageLockRenderer;

    [Tooltip("目标分达成后激活的标准 Spine DoorLock 根节点。")]
    [SerializeField] private GameObject doorLockRoot;

    [Tooltip("目标分达成后弹出的现有标准 Key 根节点。")]
    [SerializeField] private Transform keyRoot;

    [Tooltip("钥匙飞行终点，建议绑定出口门 door_0/LockPoint；为空时运行时按该路径自动查找。")]
    [SerializeField] private Transform keyDoorTarget;

    [Tooltip("Key 子节点上的 SpriteRenderer；用于在演出和重开时复位钥匙透明度。")]
    [SerializeField] private SpriteRenderer keyRenderer;

    [Tooltip("标准 Key 的触发碰撞体；脚本演出期间保持关闭。")]
    [SerializeField] private Collider2D keyCollider;

    [Tooltip("door_0 的成功触发碰撞体，开锁前保持关闭。")]
    [SerializeField] private Collider2D exitDoorCollider;

    [Tooltip("切水果输入与抛物线换算使用的世界相机。")]
    [SerializeField] private Camera worldCamera;

    [Header("Fruit And Bomb Art")]
    [Tooltip("可随机抛出的水果及各自喷溅颜色。")]
    [SerializeField] private FruitDefinition[] fruits;

    [Tooltip("炸弹主体贴图。")]
    [SerializeField] private Sprite bombSprite;

    [Tooltip("炸弹引线火花两帧，按数组顺序循环。")]
    [SerializeField] private Sprite[] bombSparkFrames = new Sprite[2];

    [Tooltip("从用户 unitypackage 导入的 CoconutExplosion 特效。")]
    [SerializeField] private GameObject fruitSliceEffectPrefab;

    [Tooltip("Level067 同款角色爆破特效 Prefab。")]
    [SerializeField] private GameObject actorExplosionPrefab;

    #endregion

    #region Inspector Tuning

    [Header("Start Trigger")]
    [Tooltip("木台碰撞体缺失时使用的水平触发范围，仅作为安全回退。")]
    [SerializeField, Min(0.1f)] private float bubbleTriggerHalfWidth = 0.85f;

    [Tooltip("木台碰撞体缺失时使用的垂直触发范围，仅作为安全回退。")]
    [SerializeField, Min(0.1f)] private float bubbleTriggerVerticalRange = 2.25f;

    [Tooltip("角色碰撞体脚底与木台顶面的最大允许误差。")]
    [SerializeField, Min(0.01f)] private float bubblePlatformFootTolerance = 0.22f;

    [Tooltip("泡泡待机时上下浮动的单边幅度。")]
    [SerializeField, Min(0f)] private float bubbleFloatAmplitude = 0.12f;

    [Tooltip("泡泡完成一次上下往返的时长。")]
    [SerializeField, Min(0.1f)] private float bubbleFloatCycleDuration = 1.4f;

    [Header("Score")]
    [Tooltip("正常玩法需要达到的目标分数。")]
    [SerializeField, Min(1)] private int targetScore = 30000;

    [Tooltip("每切到一个水果获得的分数。")]
    [SerializeField, Min(0)] private int fruitScore = 5;

    [Tooltip("每轮切到总数减二时的额外分数。")]
    [SerializeField, Min(0)] private int wellBonus = 10;

    [Tooltip("每轮切到总数减一时的额外分数。")]
    [SerializeField, Min(0)] private int goodBonus = 20;

    [Tooltip("每轮全部切到时的额外分数。")]
    [SerializeField, Min(0)] private int perfectBonus = 40;

    [Header("Rounds")]
    [Tooltip("每轮最少抛出的水果数量。")]
    [SerializeField, Min(1)] private int minimumFruitCount = 3;

    [Tooltip("每轮最多抛出的水果数量。")]
    [SerializeField, Min(1)] private int maximumFruitCount = 5;

    [Tooltip("每轮额外出现一枚炸弹的概率。")]
    [SerializeField, Range(0f, 1f)] private float bombRoundChance = 0.1f;

    [Tooltip("泡泡消失后等待首轮抛出的时间。")]
    [SerializeField, Min(0f)] private float firstRoundDelay = 0.35f;

    [Tooltip("一轮全部落下后等待下一轮的时间。")]
    [SerializeField, Min(0f)] private float betweenRoundDelay = 0.45f;

    [Header("Launch")]
    [Tooltip("红框下方左侧抛物起点带的最小屏幕横向比例，右侧起点会自动镜像。")]
    [SerializeField, Range(0f, 0.5f)] private float spawnViewportMinX = 0.34f;

    [Tooltip("红框下方左侧抛物起点带的最大屏幕横向比例，右侧起点会自动镜像。")]
    [SerializeField, Range(0f, 0.5f)] private float spawnViewportMaxX = 0.40f;

    [Tooltip("水果向上抛出后回落的画面中部区域最小屏幕横向比例。")]
    [SerializeField, Range(0f, 0.5f)] private float landingViewportMinX = 0.44f;

    [Tooltip("水果向上抛出后回落的画面中部区域最大屏幕横向比例。")]
    [SerializeField, Range(0.5f, 1f)] private float landingViewportMaxX = 0.56f;

    [Tooltip("水果从屏幕底边外、截图红框竖直下方的该 viewport 比例处抛出。")]
    [SerializeField] private float spawnViewportY = -0.08f;

    [Tooltip("水果落到屏幕底部以下该比例后回收。")]
    [SerializeField] private float killViewportY = -0.2f;

    [Tooltip("单次完整抛物线飞行时间范围。")]
    [SerializeField] private Vector2 flightDurationRange = new Vector2(2.4f, 3.05f);

    [Tooltip("水果和炸弹使用的重力倍率。")]
    [SerializeField, Min(0.05f)] private float launchGravityScale = 1f;

    [Tooltip("水果显示缩放。")]
    [SerializeField, Min(0.05f)] private float fruitScale = 0.66f;

    [Tooltip("炸弹显示缩放。")]
    [SerializeField, Min(0.05f)] private float bombScale = 0.58f;

    [Tooltip("抛出物的随机旋转速度范围。")]
    [SerializeField] private Vector2 angularVelocityRange = new Vector2(-150f, 150f);

    [Tooltip("滑动线段命中半径相对 Sprite 包围盒的倍率。")]
    [SerializeField, Range(0.2f, 1.5f)] private float sliceHitRadiusScale = 0.72f;

    [Tooltip("刀锋在世界空间中的额外半径，同时用于水果、半片和锁体判定。")]
    [SerializeField, Min(0f)] private float sliceBladeRadius = 0.15f;

    [Tooltip("炸弹火花每秒切换的帧数。")]
    [SerializeField, Min(1f)] private float bombSparkFramesPerSecond = 10f;

    [Tooltip("水果、炸弹在场景中的排序值。")]
    [SerializeField] private int itemSortingOrder = 260;

    [Header("Slice")]
    [Tooltip("相邻滑动采样至少相隔的屏幕像素，避免零长度切割。")]
    [SerializeField, Min(1f)] private float minimumSliceStepPixels = 4f;

    [Tooltip("滑动拖尾保留时间。")]
    [SerializeField, Min(0.02f)] private float trailLifetime = 0.18f;

    [Tooltip("滑动拖尾起点宽度。")]
    [SerializeField, Min(0.01f)] private float trailWidth = 0.3f;

    [Tooltip("切开后较小半片至少占原可见面积的比例，避免只削掉一点。")]
    [SerializeField, Range(0f, 0.45f)] private float minimumSliceAreaRatio = 0.18f;

    [Tooltip("每个首切半片允许继续切开的层数；二次切割不再计分。")]
    [SerializeField, Range(0, 2)] private int additionalFruitSliceDepth = 1;

    [Tooltip("水果切开后半片开始渐隐前的停留时间。")]
    [SerializeField, Min(0f)] private float fruitHalfFadeDelay = 1.05f;

    [Tooltip("水果切开半片渐隐时长。")]
    [SerializeField, Min(0.01f)] private float fruitHalfFadeDuration = 0.38f;

    [Tooltip("图片锁可切判定框相对锁图片中心的本地偏移，只覆盖锁体红框而不覆盖整段锁链。")]
    [SerializeField] private Vector2 lockSliceAreaLocalOffset = new Vector2(0.08f, -0.39f);

    [Tooltip("图片锁可切判定框的本地尺寸。")]
    [SerializeField] private Vector2 lockSliceAreaSize = new Vector2(1.05f, 1.15f);

    [Tooltip("图片锁切开后开始渐隐前的停留时间。")]
    [SerializeField, Min(0f)] private float lockHalfFadeDelay = 0.75f;

    [Tooltip("图片锁切开半片渐隐时长。")]
    [SerializeField, Min(0.01f)] private float lockHalfFadeDuration = 0.8f;

    [Tooltip("水果喷溅特效的统一缩放。")]
    [SerializeField, Min(0.05f)] private float fruitSliceEffectScale = 0.8f;

    [Tooltip("水果喷溅特效在 SceneLayer 中的基础排序值。")]
    [SerializeField] private int fruitSliceEffectSortingOrder = 320;

    [Header("Key And Unlock")]
    [Tooltip("钥匙达标后相对其 authored 初始位置的显示偏移，默认保持策划摆放位置。")]
    [SerializeField] private Vector2 keyRevealOffset = Vector2.zero;

    [Tooltip("钥匙出现后镜头缩放到原正交尺寸的比例；值越小特写越近。")]
    [SerializeField, Range(0.25f, 1f)] private float keyFocusZoomRatio = 0.7f;

    [Tooltip("镜头从原位置移动到钥匙中心的时长。")]
    [SerializeField, Min(0.01f)] private float keyFocusMoveDuration = 0.35f;

    [Tooltip("镜头聚焦钥匙后停留的时长。")]
    [SerializeField, Min(0f)] private float keyFocusHoldDuration = 0.6f;

    [Tooltip("钥匙从特写开始飞向门时，镜头恢复原位置和尺寸的时长。")]
    [SerializeField, Min(0.01f)] private float keyCameraRestoreDuration = 0.3f;

    [Tooltip("钥匙从出现位置飞到 door_0/LockPoint 的时长。")]
    [SerializeField, Min(0.05f)] private float keyFlightToDoorDuration = 0.75f;

    [Tooltip("钥匙飞门弧线相对直线的最大高度。")]
    [SerializeField, Min(0f)] private float keyFlightToDoorArcHeight = 0.35f;

    [Tooltip("镜头聚焦中心相对钥匙位置的世界坐标偏移，用于策划微调特写构图。")]
    [SerializeField] private Vector2 keyFocusCameraOffset = Vector2.zero;

    [Tooltip("等待标准 Spine 锁解锁动画的最长时间。")]
    [SerializeField, Min(0.5f)] private float unlockTimeout = 3f;

    [Header("Bomb Failure")]
    [Tooltip("命中炸弹后镜头缩放到原尺寸的倍率。")]
    [SerializeField, Range(0.6f, 1f)] private float bombFocusZoomRatio = 0.84f;

    [Tooltip("命中炸弹后的镜头聚焦时间。")]
    [SerializeField, Min(0.01f)] private float bombFocusDuration = 0.15f;

    [Tooltip("聚焦完成到爆破角色之间的停顿。")]
    [SerializeField, Min(0f)] private float bombFreezeDelay = 1f;

    [Tooltip("角色被炸飞的方向。")]
    [SerializeField] private Vector2 bombKnockDirection = new Vector2(-0.5f, 0.7f);

    [Tooltip("角色被炸飞的力度。")]
    [SerializeField, Min(0.1f)] private float bombKnockForce = 50f;

    #endregion

    #region Runtime State

    private readonly List<Level81FruitItem> roundItems = new();
    private readonly List<Level81SliceMesh.SliceResult> activeSlices = new();
    // 每次手势只允许切它开始前已存在的半片，避免同一刀递归切碎刚生成的碎片。
    private readonly List<Level81SliceMesh.SliceResult> gestureResliceCandidates = new();
    private readonly List<RuntimeEffect> activeEffects = new();
    private readonly List<Collider2D> bubbleTriggerActorColliders = new();
    private EventSubscriber subscriber;
    private Level81GameplayHud hud;
    private MoveFloatHandle keyFloatHandle;
    private ActorSpineCtrl doorLockSpineCtrl;
    private TrailRenderer sliceTrail;
    private Material sliceTrailMaterial;
    private Tween cameraFocusTween;
    private Tween actorSpinTween;
    private PointerMode pointerMode;
    private int activeFingerId = -1;
    private Vector2 latestScreenPosition;
    private Vector2 latestWorldPosition;
    private int currentScore;
    private int roundFruitTotal;
    private int roundFruitSliced;
    private int sessionVersion;
    private float roundDelayRemaining;
    private float originalCameraSize;
    private Vector3 originalCameraPosition;
    private float doorLockSpineTimeScaleBeforePause = 1f;
    private bool hasOriginalCameraSize;
    private bool hasOriginalCameraPosition;
    private bool actorWasCtrlable;
    private bool actorLocked;
    private bool miniGameActive;
    private bool roundActive;
    private bool swingPlayedForGesture;
    private bool completionStarted;
    private bool doorOpened;
    private bool isPaused;
    private bool isFailing;
    private bool cleanupComplete;
    // GameReset 会先于 PlayCtrl 销毁旧关卡实例到达；在销毁前保持旧实例惰性，
    // 防止延迟异步续体重新打开泡泡或重复派发失败事件。
    private bool resetOrReplayReceived;
    private bool bombFailureEventDispatched;
    private bool reinforceActorLockNextUpdate;
    private bool restoreActorControlOnResume;
    private bool gameplayPanelBlockedByBomb;
    private bool doorLockSpinePaused;
    private bool actorPhysicsPausedByLevel;
    private bool actorPhysicsFrozenForFocus;
    private bool bombFocusPresentationFrozen;
    private bool sliceTrailPausedByLevel;

    private Vector2 pausedActorVelocity;
    private float pausedActorAngularVelocity;
    private bool pausedActorWasSimulated;
    private Vector2 focusActorVelocity;
    private float focusActorAngularVelocity;
    private bool focusActorWasSimulated;
    private Vector2 lastFailingActorVelocity;
    private float lastFailingActorAngularVelocity;
    private bool hasLastFailingActorVelocity;

    private Vector3 initialKeyLocalPosition;
    private Quaternion initialKeyLocalRotation;
    private Vector3 initialKeyLocalScale = Vector3.one;
    private Color initialKeyRendererColor = Color.white;
    private bool hasInitialKeyTransform;
    private bool hasInitialKeyRendererColor;
    private Vector3 initialBubbleLocalPosition;
    private float bubbleFloatPhase;
    private bool hasInitialBubbleTransform;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        CacheCameraState();
        CacheInitialPresentationState();
        InitializePresentation();
        ValidateConfiguration();
    }

    private void OnEnable()
    {
        ResolveReferences();

        // 工具或场景转场可能临时禁用再启用关卡根节点；只有复用实例时才重建
        // 本地表现，首次 OnEnable 已经由 Awake 完成初始化。
        if (cleanupComplete || resetOrReplayReceived)
        {
            if (!hasInitialKeyTransform)
                CacheInitialPresentationState();
            InitializePresentation();
            ResetRuntimeFlags();
        }

        cleanupComplete = false;
        resetOrReplayReceived = false;
        RegisterEvents();
        isPaused = !IsGameplayPlaying();
        ClearPointer();
    }

    private void Update()
    {
        if (resetOrReplayReceived)
            return;

        // 共享 GameplayPanel 出现后立即绑定预先摆好的 HUD；泡泡被消费前只隐藏
        // 运行时表现，不创建新的 HUD 实例。
        EnsureHud();

        PruneInactiveSlices();

        bool gameplayPlaying = IsGameplayPlaying();
        UpdateRuntimeEffects(gameplayPlaying && !isPaused ? Time.unscaledDeltaTime : 0f);
        if (gameplayPlaying && !isPaused)
            CacheFailingActorVelocity();

        if (reinforceActorLockNextUpdate)
        {
            reinforceActorLockNextUpdate = false;
            ReinforceActorLock();
        }

        // PlayCtrl 可能晚于本组件 OnEnable 完成初始化；每帧重新对齐状态，避免
        // 早期读取到 false 后让关卡永久停在暂停态。
        if (!gameplayPlaying)
        {
            isPaused = true;
            ClearPointer();
            return;
        }

        if (isPaused)
        {
            isPaused = false;
            SetPresentationPaused(false);
            ReinforceActorLock();
        }

        UpdateBubbleFloat(Time.unscaledDeltaTime);

        if (!miniGameActive && !completionStarted && !isFailing)
        {
            TryConsumeBubble();
            return;
        }

        if (!miniGameActive || completionStarted || isFailing)
            return;

        EnsureHud();
        HandlePointerInput();
        UpdateRoundFlow();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            ClearPointer();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            ClearPointer();
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref subscriber);
        CleanupRuntime(!resetOrReplayReceived);
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref subscriber);
        CleanupRuntime(false);
    }

    #endregion

    #region Start And Round Flow

    private void TryConsumeBubble()
    {
        if (bubbleRoot == null || actor == null || !bubbleRoot.gameObject.activeInHierarchy)
            return;

        if (bubblePlatformCollider != null)
        {
            if (!IsActorStandingOnBubblePlatform())
                return;
        }
        else
        {
            Vector3 delta = actor.transform.position - bubbleRoot.position;
            if (!actor.IsGround ||
                Mathf.Abs(delta.x) > bubbleTriggerHalfWidth ||
                Mathf.Abs(delta.y) > bubbleTriggerVerticalRange)
            {
                return;
            }
        }

        bubbleRoot.gameObject.SetActive(false);
        LockActorForMiniGame();
        actor.EnterSwordMode();
        currentScore = 0;
        roundDelayRemaining = firstRoundDelay;
        miniGameActive = true;
        EnsureHud();
        if (hud != null)
        {
            hud.SetTarget(targetScore);
            hud.SetScore(0, false);
            hud.Show();
        }
    }

    private void UpdateBubbleFloat(float deltaTime)
    {
        if (!hasInitialBubbleTransform ||
            bubbleRoot == null ||
            !bubbleRoot.gameObject.activeInHierarchy)
        {
            return;
        }

        float cycleDuration = Mathf.Max(0.1f, bubbleFloatCycleDuration);
        bubbleFloatPhase = Mathf.Repeat(
            bubbleFloatPhase + Mathf.Max(0f, deltaTime) / cycleDuration,
            1f);
        Vector3 position = initialBubbleLocalPosition;
        position.y += Mathf.Sin(bubbleFloatPhase * Mathf.PI * 2f) *
                      Mathf.Max(0f, bubbleFloatAmplitude);
        bubbleRoot.localPosition = position;
    }

    private bool IsActorStandingOnBubblePlatform()
    {
        if (actor == null ||
            bubblePlatformCollider == null ||
            !actor.IsGround ||
            !bubblePlatformCollider.enabled ||
            !bubblePlatformCollider.gameObject.activeInHierarchy)
        {
            return false;
        }

        bubbleTriggerActorColliders.Clear();
        actor.GetComponentsInChildren(false, bubbleTriggerActorColliders);
        bool isTouchingPlatform = false;
        bool hasActorBounds = false;
        Bounds actorBounds = default;

        foreach (Collider2D actorCollider in bubbleTriggerActorColliders)
        {
            if (actorCollider == null ||
                actorCollider == bubblePlatformCollider ||
                !actorCollider.enabled ||
                actorCollider.isTrigger ||
                !actorCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!isTouchingPlatform && actorCollider.IsTouching(bubblePlatformCollider))
                isTouchingPlatform = true;

            if (!hasActorBounds)
            {
                actorBounds = actorCollider.bounds;
                hasActorBounds = true;
            }
            else
            {
                actorBounds.Encapsulate(actorCollider.bounds);
            }
        }

        if (!isTouchingPlatform || !hasActorBounds)
            return false;

        float footDistance = Mathf.Abs(
            actorBounds.min.y - bubblePlatformCollider.bounds.max.y);
        return footDistance <= bubblePlatformFootTolerance;
    }

    private void UpdateRoundFlow()
    {
        if (!roundActive)
        {
            roundDelayRemaining -= Time.deltaTime;
            if (roundDelayRemaining <= 0f)
                SpawnRound();
            return;
        }

        for (int index = roundItems.Count - 1; index >= 0; index--)
        {
            Level81FruitItem item = roundItems[index];
            if (item == null || item.IsResolved)
                continue;

            return;
        }

        ResolveRound();
    }

    private void SpawnRound()
    {
        roundItems.Clear();
        roundFruitSliced = 0;
        roundFruitTotal = 0;

        int minimum = Mathf.Max(1, minimumFruitCount);
        int maximum = Mathf.Max(minimum, maximumFruitCount);
        int requestedFruitCount = UnityEngine.Random.Range(minimum, maximum + 1);
        bool launchFromLeft = UnityEngine.Random.value < 0.5f;
        for (int index = 0; index < requestedFruitCount; index++)
        {
            if (TrySpawnItem(false, launchFromLeft) != null)
            {
                roundFruitTotal++;
                launchFromLeft = !launchFromLeft;
            }
        }

        if (UnityEngine.Random.value < bombRoundChance)
            TrySpawnItem(true, launchFromLeft);

        roundActive = roundItems.Count > 0;
        if (!roundActive)
            roundDelayRemaining = betweenRoundDelay;
    }

    private Level81FruitItem TrySpawnItem(bool isBomb, bool launchFromLeft)
    {
        Sprite sprite;
        Color juiceColor;
        if (isBomb)
        {
            sprite = bombSprite;
            juiceColor = new Color(1f, 0.36f, 0.08f, 1f);
        }
        else
        {
            if (fruits == null || fruits.Length == 0)
                return null;

            FruitDefinition definition = fruits[UnityEngine.Random.Range(0, fruits.Length)];
            sprite = definition.Sprite;
            juiceColor = definition.JuiceColor;
        }

        if (sprite == null || worldCamera == null)
            return null;

        float leftStartMinX = Mathf.Clamp(
            Mathf.Min(spawnViewportMinX, spawnViewportMaxX),
            0f,
            0.5f);
        float leftStartMaxX = Mathf.Clamp(
            Mathf.Max(spawnViewportMinX, spawnViewportMaxX),
            leftStartMinX,
            0.5f);
        float leftStartX = UnityEngine.Random.Range(leftStartMinX, leftStartMaxX);
        float startX = launchFromLeft ? leftStartX : 1f - leftStartX;
        float landingX = UnityEngine.Random.Range(
            Mathf.Clamp(landingViewportMinX, 0f, 0.5f),
            Mathf.Clamp(landingViewportMaxX, 0.5f, 1f));
        landingX = launchFromLeft
            ? Mathf.Max(landingX, startX)
            : Mathf.Min(landingX, startX);
        float duration = UnityEngine.Random.Range(
            Mathf.Min(flightDurationRange.x, flightDurationRange.y),
            Mathf.Max(flightDurationRange.x, flightDurationRange.y));
        duration = Mathf.Max(0.25f, duration);

        Vector2 startWorld = ViewportToWorld(startX, spawnViewportY);
        Vector2 endWorld = ViewportToWorld(landingX, spawnViewportY);
        float gravityY = Physics2D.gravity.y * Mathf.Max(0.05f, launchGravityScale);
        Vector2 velocity = new Vector2(
            (endWorld.x - startWorld.x) / duration,
            -0.5f * gravityY * duration);

        GameObject itemObject = new GameObject(isBomb ? "Level081_Bomb" : "Level081_Fruit");
        itemObject.transform.SetParent(transform, true);
        itemObject.transform.position = new Vector3(startWorld.x, startWorld.y, 0f);
        Level81FruitItem item = itemObject.AddComponent<Level81FruitItem>();
        item.Initialize(
            sprite,
            juiceColor,
            isBomb,
            bombSparkFrames,
            velocity,
            launchGravityScale,
            UnityEngine.Random.Range(angularVelocityRange.x, angularVelocityRange.y),
            isBomb ? bombScale : fruitScale,
            ViewportToWorld(0.5f, killViewportY).y,
            sliceHitRadiusScale,
            bombSparkFramesPerSecond,
            itemSortingOrder + roundItems.Count);
        roundItems.Add(item);
        return item;
    }

    private void ResolveRound()
    {
        roundActive = false;
        roundItems.Clear();

        if (roundFruitTotal <= 0)
        {
            roundDelayRemaining = betweenRoundDelay;
            return;
        }

        if (roundFruitSliced >= roundFruitTotal)
        {
            if (hud != null)
                hud.ShowRating(Level81GameplayHud.Rating.Perfect);
            AddScore(perfectBonus);
        }
        else if (roundFruitSliced == roundFruitTotal - 1)
        {
            if (hud != null)
                hud.ShowRating(Level81GameplayHud.Rating.Good);
            AddScore(goodBonus);
        }
        else if (roundFruitSliced == roundFruitTotal - 2)
        {
            if (hud != null)
                hud.ShowRating(Level81GameplayHud.Rating.Well);
            AddScore(wellBonus);
        }

        if (!completionStarted)
            roundDelayRemaining = betweenRoundDelay;
    }

    private void AddScore(int amount)
    {
        if (amount <= 0 || completionStarted || isFailing)
            return;

        currentScore += amount;
        if (hud != null)
            hud.SetScore(currentScore);
        if (currentScore >= Mathf.Max(1, targetScore))
            BeginScoreCompletion();
    }

    #endregion

    #region Pointer And Slicing

    private void HandlePointerInput()
    {
        if (Input.touchCount > 0)
        {
            HandleTouchInput();
            return;
        }

        if (pointerMode == PointerMode.Touch)
        {
            EndPointer(latestScreenPosition, true);
            return;
        }

#if UNITY_EDITOR
        HandleEditorMouse();
#endif
    }

    private void HandleTouchInput()
    {
        if (pointerMode == PointerMode.Touch)
        {
            if (!TryGetActiveTouch(out Touch activeTouch))
            {
                EndPointer(latestScreenPosition, true);
                return;
            }

            if (activeTouch.phase == TouchPhase.Ended || activeTouch.phase == TouchPhase.Canceled)
                EndPointer(activeTouch.position, activeTouch.phase == TouchPhase.Canceled);
            else
                UpdatePointer(activeTouch.position);

            return;
        }

        if (pointerMode != PointerMode.None)
            return;

        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (touch.phase != TouchPhase.Began)
                continue;

            BeginPointer(touch.position, touch.fingerId, PointerMode.Touch);
            if (pointerMode != PointerMode.None)
                break;
        }
    }

#if UNITY_EDITOR
    private void HandleEditorMouse()
    {
        if (Input.GetMouseButtonDown(0))
            BeginPointer(Input.mousePosition, -1, PointerMode.EditorMouse);
        else if (pointerMode == PointerMode.EditorMouse && Input.GetMouseButton(0))
            UpdatePointer(Input.mousePosition);
        else if (pointerMode == PointerMode.EditorMouse && Input.GetMouseButtonUp(0))
            EndPointer(Input.mousePosition, false);
    }
#endif

    private void BeginPointer(Vector2 screenPosition, int fingerId, PointerMode mode)
    {
        if (!CanReceiveSliceInput() || IsPointerOverUi(screenPosition, fingerId))
            return;

        pointerMode = mode;
        activeFingerId = fingerId;
        latestScreenPosition = screenPosition;
        latestWorldPosition = ScreenToWorld(screenPosition);
        swingPlayedForGesture = false;
        gestureResliceCandidates.Clear();
        for (int index = 0; index < activeSlices.Count; index++)
        {
            Level81SliceMesh.SliceResult result = activeSlices[index];
            if (result != null && result.IsAlive)
                gestureResliceCandidates.Add(result);
        }

        EnsureSliceTrail();
        if (sliceTrail != null)
        {
            sliceTrail.emitting = false;
            sliceTrail.transform.position = ToTrailPosition(latestWorldPosition);
            sliceTrail.Clear();
            sliceTrail.AddPosition(ToTrailPosition(latestWorldPosition));
            sliceTrail.emitting = true;
        }
    }

    private void UpdatePointer(Vector2 screenPosition)
    {
        if (pointerMode == PointerMode.None || !CanReceiveSliceInput())
        {
            ClearPointer();
            return;
        }

        Vector2 screenDelta = screenPosition - latestScreenPosition;
        if (screenDelta.sqrMagnitude < minimumSliceStepPixels * minimumSliceStepPixels)
            return;

        Vector2 worldPosition = ScreenToWorld(screenPosition);
        if (!swingPlayedForGesture)
        {
            swingPlayedForGesture = true;
            if (actor != null)
                actor.PlayRandomSwordSwing();
        }

        ProcessSliceSegment(latestWorldPosition, worldPosition);
        latestScreenPosition = screenPosition;
        latestWorldPosition = worldPosition;
        if (sliceTrail != null)
            sliceTrail.transform.position = ToTrailPosition(worldPosition);
    }

    private void EndPointer(Vector2 screenPosition, bool canceled)
    {
        if (!canceled && pointerMode != PointerMode.None)
            UpdatePointer(screenPosition);

        if (sliceTrail != null)
            sliceTrail.emitting = false;

        pointerMode = PointerMode.None;
        activeFingerId = -1;
        latestScreenPosition = Vector2.zero;
        latestWorldPosition = Vector2.zero;
        swingPlayedForGesture = false;
        gestureResliceCandidates.Clear();
    }

    private void ProcessSliceSegment(Vector2 worldStart, Vector2 worldEnd)
    {
        if (CanCutImageLock() && SegmentIntersectsBounds(worldStart, worldEnd, GetLockSliceBounds()))
        {
            if (CutImageLock(worldStart, worldEnd))
                return;
        }

        for (int index = roundItems.Count - 1; index >= 0; index--)
        {
            Level81FruitItem item = roundItems[index];
            if (item == null || item.IsResolved ||
                !item.TryGetSliceHit(
                    worldStart,
                    worldEnd,
                    sliceBladeRadius,
                    out _))
            {
                continue;
            }

            if (item.IsBomb)
            {
                HandleBombSlice(item);
                return;
            }

            HandleFruitSlice(item, worldStart, worldEnd);
            if (completionStarted || isFailing || !miniGameActive)
                return;
        }

        ProcessExistingFruitSlices(worldStart, worldEnd);
    }

    private bool HandleFruitSlice(
        Level81FruitItem item,
        Vector2 worldStart,
        Vector2 worldEnd)
    {
        if (item == null)
            return false;

        Vector2 baseVelocity = item.Body != null ? item.Body.linearVelocity : Vector2.zero;
        float baseAngularVelocity = item.Body != null ? item.Body.angularVelocity : 0f;
        Level81SliceMesh.SliceSettings settings = Level81SliceMesh.SliceSettings.Default;
        settings.AddPolygonCollider = false;
        settings.BaseVelocity = baseVelocity * 0.48f;
        settings.BaseAngularVelocity = baseAngularVelocity;
        settings.SeparationSpeed = 1.25f;
        settings.DownwardSpeed = 0.2f;
        settings.GravityScale = 1.25f;
        settings.FadeDelay = fruitHalfFadeDelay;
        settings.FadeDuration = fruitHalfFadeDuration;
        settings.MinimumAreaRatio = minimumSliceAreaRatio;
        settings.RemainingReslices = additionalFruitSliceDepth;
        settings.EffectColor = item.JuiceColor;
        settings.NamePrefix = "Level081FruitHalf";

        if (!Level81SliceMesh.TrySlice(
                 item.Renderer,
                 worldStart,
                 worldEnd,
                 settings,
                 out Level81SliceMesh.SliceResult result) ||
            result == null || result.PositiveHalf == null || result.NegativeHalf == null)
        {
            result?.Dispose();
            return false;
        }

        if (!item.ConfirmSlice())
        {
            result.Dispose();
            return false;
        }

        item.Renderer.enabled = false;
        activeSlices.Add(result);
        SpawnFruitSliceEffect(result.CutWorldPosition, item.JuiceColor);
        roundFruitSliced++;
        AddScore(fruitScore);
        Destroy(item.gameObject);
        return true;
    }

    private void ProcessExistingFruitSlices(
        Vector2 worldStart,
        Vector2 worldEnd)
    {
        if (gestureResliceCandidates.Count <= 0)
            return;

        List<Level81SliceMesh.SliceResult> pendingResults = null;
        for (int index = 0; index < gestureResliceCandidates.Count; index++)
        {
            Level81SliceMesh.SliceResult owner = gestureResliceCandidates[index];
            if (owner == null || !owner.IsAlive)
                continue;

            TryResliceFruitHalf(owner, owner.PositiveHalf, worldStart, worldEnd, ref pendingResults);
            TryResliceFruitHalf(owner, owner.NegativeHalf, worldStart, worldEnd, ref pendingResults);
        }

        if (pendingResults != null)
            activeSlices.AddRange(pendingResults);
    }

    private bool TryResliceFruitHalf(
        Level81SliceMesh.SliceResult owner,
        GameObject halfObject,
        Vector2 worldStart,
        Vector2 worldEnd,
        ref List<Level81SliceMesh.SliceResult> pendingResults)
    {
        if (owner == null || halfObject == null)
            return false;

        Level81SliceMesh half = halfObject.GetComponent<Level81SliceMesh>();
        if (half == null || !half.CanReslice)
            return false;

        Bounds hitBounds = half.RenderBounds;
        float bladeDiameter = Mathf.Max(0f, sliceBladeRadius) * 2f;
        hitBounds.Expand(new Vector3(bladeDiameter, bladeDiameter, 0f));
        if (!SegmentIntersectsBounds(worldStart, worldEnd, hitBounds))
            return false;

        Rigidbody2D body = halfObject.GetComponent<Rigidbody2D>();
        Level81SliceMesh.SliceSettings settings = Level81SliceMesh.SliceSettings.Default;
        settings.AddPolygonCollider = false;
        settings.BaseVelocity = body != null ? body.linearVelocity * 0.48f : Vector2.zero;
        settings.BaseAngularVelocity = body != null ? body.angularVelocity : 0f;
        settings.SeparationSpeed = 1.15f;
        settings.DownwardSpeed = 0.16f;
        settings.GravityScale = 1.25f;
        settings.FadeDelay = fruitHalfFadeDelay;
        settings.FadeDuration = fruitHalfFadeDuration;
        settings.MinimumAreaRatio = minimumSliceAreaRatio;
        settings.NamePrefix = "Level081FruitFragment";

        if (!half.TryReslice(
                worldStart,
                worldEnd,
                settings,
                out Level81SliceMesh.SliceResult result) ||
            result == null || result.PositiveHalf == null || result.NegativeHalf == null)
        {
            result?.Dispose();
            return false;
        }

        if (!owner.DetachHalf(halfObject))
        {
            result.Dispose();
            return false;
        }

        Color effectColor = half.EffectColor;
        Vector2 effectPosition = result.CutWorldPosition;
        half.ConsumeAfterReslice();
        pendingResults ??= new List<Level81SliceMesh.SliceResult>();
        pendingResults.Add(result);
        SpawnFruitSliceEffect(effectPosition, effectColor);
        return true;
    }

    private void HandleBombSlice(Level81FruitItem item)
    {
        if (isFailing || completionStarted)
            return;

        if (item == null)
            return;

        Vector2 bombPosition = item.transform.position;
        if (!item.ConfirmSlice())
            return;

        // ConfirmSlice 会冻结刚体和引线当前帧；失败特写期间保持炸弹完整可见且
        // 停在命中位置。
        BeginBombFailureAsync(bombPosition, sessionVersion).Forget();
    }

    private bool CanCutImageLock()
    {
        return miniGameActive &&
               !completionStarted &&
               !isFailing &&
               imageLockRenderer != null &&
               imageLockRenderer.enabled &&
               imageLockRenderer.gameObject.activeInHierarchy;
    }

    private bool CutImageLock(Vector2 worldStart, Vector2 worldEnd)
    {
        if (!CanCutImageLock())
            return false;

        Level81SliceMesh.SliceSettings settings = Level81SliceMesh.SliceSettings.Default;
        settings.AddPolygonCollider = true;
        settings.ColliderIsTrigger = false;
        settings.SeparationSpeed = 1.5f;
        settings.DownwardSpeed = 0f;
        settings.AngularSeparationSpeed = 52f;
        settings.GravityScale = 0.6f;
        settings.Mass = 0.4f;
        settings.FadeDelay = lockHalfFadeDelay;
        settings.FadeDuration = lockHalfFadeDuration;
        settings.MinimumAreaRatio = Mathf.Min(0.05f, minimumSliceAreaRatio);
        settings.EffectColor = Color.white;
        settings.NamePrefix = "Level081LockHalf";

        if (!Level81SliceMesh.TrySlice(
                 imageLockRenderer,
                 worldStart,
                 worldEnd,
                 settings,
                 out Level81SliceMesh.SliceResult result) ||
            result == null || result.PositiveHalf == null || result.NegativeHalf == null)
        {
            result?.Dispose();
            return false;
        }

        completionStarted = true;
        miniGameActive = false;
        ClearPointer();
        DiscardRoundItems();
        if (hud != null)
            hud.Hide();
        activeSlices.Add(result);
        imageLockRenderer.enabled = false;
        OpenExitAndRestoreActor();
        return true;
    }

    private Bounds GetLockSliceBounds()
    {
        if (imageLockRenderer == null)
            return default;

        Transform lockTransform = imageLockRenderer.transform;
        Vector2 halfSize = new Vector2(
            Mathf.Max(0.01f, Mathf.Abs(lockSliceAreaSize.x)) * 0.5f,
            Mathf.Max(0.01f, Mathf.Abs(lockSliceAreaSize.y)) * 0.5f);
        Vector2[] corners =
        {
            lockSliceAreaLocalOffset + new Vector2(-halfSize.x, -halfSize.y),
            lockSliceAreaLocalOffset + new Vector2(-halfSize.x, halfSize.y),
            lockSliceAreaLocalOffset + new Vector2(halfSize.x, halfSize.y),
            lockSliceAreaLocalOffset + new Vector2(halfSize.x, -halfSize.y)
        };

        Bounds bounds = new Bounds(lockTransform.TransformPoint(corners[0]), Vector3.zero);
        for (int index = 1; index < corners.Length; index++)
            bounds.Encapsulate(lockTransform.TransformPoint(corners[index]));

        float bladeDiameter = Mathf.Max(0f, sliceBladeRadius) * 2f;
        bounds.Expand(new Vector3(bladeDiameter, bladeDiameter, 0f));
        return bounds;
    }

    #endregion

    #region Score Completion And Unlock

    private void BeginScoreCompletion()
    {
        if (completionStarted || isFailing)
            return;

        completionStarted = true;
        miniGameActive = false;
        ClearPointer();
        DiscardRoundItems();
        if (hud != null)
            hud.Hide();
        PlayNormalUnlockAsync(++sessionVersion).Forget();
    }

    private async UniTaskVoid PlayNormalUnlockAsync(int currentSession)
    {
        try
        {
            if (!IsCurrentSession(currentSession))
                return;

            if (imageLockRenderer != null)
                imageLockRenderer.enabled = false;
            if (doorLockRoot != null)
                doorLockRoot.SetActive(true);

            if (keyRoot == null || actor == null)
            {
                await PlayDoorUnlockAsync(currentSession);
                if (IsCurrentSession(currentSession))
                    OpenExitAndRestoreActor();
                return;
            }

            PrepareKeyForLevelSequence();
            GameObject keyObject = keyRoot.gameObject;
            keyObject.SetActive(true);
            if (keyCollider != null)
                keyCollider.enabled = false;

            RestoreKeyRendererColor();
            if (hasInitialKeyTransform)
            {
                keyRoot.localPosition = initialKeyLocalPosition;
                keyRoot.localRotation = initialKeyLocalRotation;
                keyRoot.localScale = initialKeyLocalScale;
            }

            Vector3 revealPosition = keyRoot.position + (Vector3)keyRevealOffset;
            keyRoot.position = revealPosition;
            AudioManager.PlaySound("getKey");

            if (!await FocusKeyCameraAsync(revealPosition, currentSession))
                return;

            if (!await WaitGameplayDelayAsync(keyFocusHoldDuration, currentSession))
                return;

            Transform doorTarget = ResolveKeyDoorTarget();
            if (doorTarget == null)
            {
                Debug.LogWarning("[Level81Ctrl] Key door target is missing; DoorLock root will be used as the fallback target.", this);
                doorTarget = doorLockRoot != null ? doorLockRoot.transform : null;
            }

            if (doorTarget != null &&
                !await FlyKeyToDoorAndRestoreCameraAsync(
                    keyRoot,
                    revealPosition,
                    doorTarget.position,
                    currentSession))
            {
                return;
            }

            if (doorTarget == null && !await RestoreKeyCameraAsync(currentSession))
                return;

            if (!IsCurrentSession(currentSession) || keyRoot == null)
                return;

            if (doorTarget != null)
                keyRoot.position = doorTarget.position;
            RestoreKeyRendererColor();
            keyObject.SetActive(false);
            await PlayDoorUnlockAsync(currentSession);
            if (IsCurrentSession(currentSession))
                OpenExitAndRestoreActor();
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// 将相机移动到钥匙中心并缩放，所有时序只在 PLAYING 状态累计，暂停时保持当前画面。
    /// </summary>
    private async UniTask<bool> FocusKeyCameraAsync(Vector3 keyPosition, int currentSession)
    {
        if (worldCamera == null || !hasOriginalCameraSize || !hasOriginalCameraPosition)
            return IsCurrentSession(currentSession);

        Vector3 startPosition = worldCamera.transform.position;
        Vector3 focusPosition = new Vector3(
            keyPosition.x + keyFocusCameraOffset.x,
            keyPosition.y + keyFocusCameraOffset.y,
            startPosition.z);
        float targetSize = originalCameraSize * Mathf.Clamp(keyFocusZoomRatio, 0.25f, 1f);
        return await AnimateCameraAsync(
            startPosition,
            focusPosition,
            worldCamera.orthographicSize,
            targetSize,
            keyFocusMoveDuration,
            currentSession);
    }

    /// <summary>
    /// 没有可用门锁目标时仍恢复相机，避免异常绑定让关卡停留在特写视野。
    /// </summary>
    private async UniTask<bool> RestoreKeyCameraAsync(int currentSession)
    {
        if (worldCamera == null || !hasOriginalCameraSize || !hasOriginalCameraPosition)
            return IsCurrentSession(currentSession);

        return await AnimateCameraAsync(
            worldCamera.transform.position,
            originalCameraPosition,
            worldCamera.orthographicSize,
            originalCameraSize,
            keyCameraRestoreDuration,
            currentSession);
    }

    /// <summary>
    /// 在钥匙飞向门的同时将相机恢复到关卡初始状态，确保整段飞行仍保持在可见构图内。
    /// </summary>
    private async UniTask<bool> FlyKeyToDoorAndRestoreCameraAsync(
        Transform key,
        Vector3 startPosition,
        Vector3 endPosition,
        int currentSession)
    {
        float flightDuration = Mathf.Max(0.05f, keyFlightToDoorDuration);
        float restoreDuration = Mathf.Max(0.01f, keyCameraRestoreDuration);
        float flightElapsed = 0f;
        float restoreElapsed = 0f;
        Vector3 cameraStartPosition = worldCamera != null
            ? worldCamera.transform.position
            : Vector3.zero;
        float cameraStartSize = worldCamera != null
            ? worldCamera.orthographicSize
            : 0f;
        bool restoreCamera = worldCamera != null &&
                             hasOriginalCameraPosition &&
                             hasOriginalCameraSize;

        while (flightElapsed < flightDuration || (restoreCamera && restoreElapsed < restoreDuration))
        {
            try
            {
                await UniTask.Yield(
                    PlayerLoopTiming.Update,
                    this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            if (!IsCurrentSession(currentSession) || key == null)
                return false;
            if (isPaused || !IsGameplayPlaying())
                continue;

            float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
            if (flightElapsed < flightDuration)
            {
                flightElapsed += deltaTime;
                float normalized = Mathf.Clamp01(flightElapsed / flightDuration);
                float eased = Mathf.SmoothStep(0f, 1f, normalized);
                Vector3 position = Vector3.LerpUnclamped(startPosition, endPosition, eased);
                position.y += 4f * eased * (1f - eased) * Mathf.Max(0f, keyFlightToDoorArcHeight);
                key.position = position;
            }

            if (restoreCamera && restoreElapsed < restoreDuration)
            {
                restoreElapsed += deltaTime;
                float normalized = Mathf.Clamp01(restoreElapsed / restoreDuration);
                float eased = Mathf.SmoothStep(0f, 1f, normalized);
                worldCamera.transform.position = Vector3.LerpUnclamped(
                    cameraStartPosition,
                    originalCameraPosition,
                    eased);
                worldCamera.orthographicSize = Mathf.Lerp(
                    cameraStartSize,
                    originalCameraSize,
                    eased);
            }
        }

        if (key != null)
            key.position = endPosition;
        if (restoreCamera && worldCamera != null)
        {
            worldCamera.transform.position = originalCameraPosition;
            worldCamera.orthographicSize = originalCameraSize;
        }
        return IsCurrentSession(currentSession);
    }

    /// <summary>
    /// 以非缩放时间推进相机位置与正交尺寸；暂停面板打开时不消耗演出时长。
    /// </summary>
    private async UniTask<bool> AnimateCameraAsync(
        Vector3 startPosition,
        Vector3 endPosition,
        float startSize,
        float endSize,
        float duration,
        int currentSession)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            try
            {
                await UniTask.Yield(
                    PlayerLoopTiming.Update,
                    this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            if (!IsCurrentSession(currentSession))
                return false;
            if (isPaused || !IsGameplayPlaying())
                continue;

            elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
            float normalized = Mathf.Clamp01(elapsed / safeDuration);
            float eased = Mathf.SmoothStep(0f, 1f, normalized);
            if (worldCamera != null)
            {
                worldCamera.transform.position = Vector3.LerpUnclamped(
                    startPosition,
                    endPosition,
                    eased);
                worldCamera.orthographicSize = Mathf.Lerp(startSize, endSize, eased);
            }
        }

        if (worldCamera != null)
        {
            worldCamera.transform.position = endPosition;
            worldCamera.orthographicSize = endSize;
        }
        return IsCurrentSession(currentSession);
    }

    private Transform ResolveKeyDoorTarget()
    {
        if (keyDoorTarget != null)
            return keyDoorTarget;

        Transform doorRoot = transform.Find("door_0");
        Transform target = doorRoot != null ? doorRoot.Find("LockPoint") : null;
        if (target != null)
            return target;

        target = exitDoorCollider != null
            ? exitDoorCollider.transform.Find("LockPoint")
            : null;
        return target != null ? target : doorLockRoot?.transform;
    }

    private async UniTask PlayDoorUnlockAsync(int currentSession)
    {
        if (!IsCurrentSession(currentSession) || doorLockRoot == null)
            return;

        doorLockRoot.SetActive(true);
        doorLockSpineCtrl ??= doorLockRoot.GetComponent<ActorSpineCtrl>();
        Spine.TrackEntry unlockEntry = doorLockSpineCtrl != null
            ? doorLockSpineCtrl.PlayAnimation("unlock", false, false)
            : null;
        if (unlockEntry != null)
            unlockEntry.MixDuration = 0f;
        AudioManager.PlaySound("unlock");

        bool animationCompleted = unlockEntry == null;
        void HandleUnlockComplete(Spine.TrackEntry _) => animationCompleted = true;
        if (unlockEntry != null)
            unlockEntry.Complete += HandleUnlockComplete;

        float elapsed = 0f;
        try
        {
            while (!animationCompleted && elapsed < Mathf.Max(0.5f, unlockTimeout))
            {
                await UniTask.Yield(
                    PlayerLoopTiming.Update,
                    this.GetCancellationTokenOnDestroy());
                if (!IsCurrentSession(currentSession))
                    return;

                bool shouldPause = isPaused || !IsGameplayPlaying();
                SetDoorLockAnimationPaused(shouldPause);
                if (!shouldPause)
                    elapsed += Time.unscaledDeltaTime;
            }

            while (isPaused || !IsGameplayPlaying())
            {
                await UniTask.Yield(
                    PlayerLoopTiming.Update,
                    this.GetCancellationTokenOnDestroy());
                if (!IsCurrentSession(currentSession))
                    return;
            }
        }
        finally
        {
            if (unlockEntry != null)
                unlockEntry.Complete -= HandleUnlockComplete;
            SetDoorLockAnimationPaused(false);
        }

        if (IsCurrentSession(currentSession) && doorLockRoot != null)
            doorLockRoot.SetActive(false);
    }

    private void OpenExitAndRestoreActor()
    {
        if (doorOpened || isFailing)
            return;

        doorOpened = true;
        if (exitDoorCollider != null)
            exitDoorCollider.enabled = true;

        if (actor != null)
            actor.ExitSwordMode();
        bool canRestoreControlNow = !isPaused &&
                                    isActiveAndEnabled &&
                                    IsGameplayPlaying();
        restoreActorControlOnResume = actorWasCtrlable && !canRestoreControlNow;
        if (actorWasCtrlable && canRestoreControlNow)
        {
            if (baseLevelCtrl != null)
                baseLevelCtrl.SetCtrlable(true);
        }
        else if (actor != null && restoreActorControlOnResume)
        {
            actor.StopMoving();
        }

        actorLocked = false;
        if (!restoreActorControlOnResume)
            actorWasCtrlable = false;
    }

    #endregion

    #region Bomb Failure

    private async UniTaskVoid BeginBombFailureAsync(Vector2 bombPosition, int currentSession)
    {
        if (isFailing || !IsCurrentSession(currentSession))
            return;

        isFailing = true;
        miniGameActive = false;
        completionStarted = true;
        SetGameplayPanelBlockedByBomb(true);
        ClearPointer();
        FreezeRoundItems();
        FreezeActiveSlices();
        if (hud != null)
            hud.SetPaused(true);
        if (actor != null)
        {
            actor.PrepareForExplosion();
            FreezeActorForBombFocus();
            actor.SetSwordAnimationPaused(true);
        }

        if (worldCamera != null)
        {
            cameraFocusTween?.Kill();
            Vector3 focusPosition = new Vector3(
                bombPosition.x,
                bombPosition.y,
                worldCamera.transform.position.z);
            Sequence focusSequence = DOTween.Sequence().SetUpdate(true);
            focusSequence.Join(worldCamera
                .DOOrthoSize(
                    originalCameraSize * Mathf.Clamp(bombFocusZoomRatio, 0.6f, 1f),
                    bombFocusDuration)
                .SetEase(Ease.OutQuad));
            focusSequence.Join(worldCamera.transform
                .DOMove(focusPosition, bombFocusDuration)
                .SetEase(Ease.OutQuad));
            focusSequence.SetLink(worldCamera.gameObject, LinkBehaviour.KillOnDestroy);
            cameraFocusTween = focusSequence;
        }

        FreezeBombFocusPresentation();
        float focusWait = worldCamera != null ? Mathf.Max(0f, bombFocusDuration) : 0f;
        if (!await WaitGameplayDelayAsync(focusWait, currentSession))
            return;

        if (cameraFocusTween != null && cameraFocusTween.IsActive())
            cameraFocusTween.Complete();
        if (!await WaitGameplayDelayAsync(bombFreezeDelay, currentSession))
            return;

        RestoreBombFocusPresentation();
        RestoreActorAfterBombFocus();
        if (!IsCurrentSession(currentSession) || actor == null || actor.IsDied)
            return;

        SpawnActorExplosion();
        AudioManager.PlaySound("rocketExplosion");

        // 先派发事件：BaseLevelCtrl 会同步进入标准陷阱链并调用 Die；本地兜底只
        // 覆盖缺少监听器的异常配置。
        if (!bombFailureEventDispatched)
        {
            bombFailureEventDispatched = true;
            this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
        }
        if (!actor.IsDied)
            actor.Die();

        actor.Knockback(bombKnockDirection, bombKnockForce).Forget();
        CacheFailingActorVelocity();
        if (actor.Body != null)
        {
            actorSpinTween?.Kill();
            actorSpinTween = actor.Body
                .DOLocalRotate(new Vector3(0f, 0f, 360f), 0.5f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1)
                .SetLink(actor.gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    /// <summary>
    /// 仅在游戏处于 PLAYING 时推进本关演出等待。演出使用非缩放时间，但打开
    /// 暂停面板时不能在面板背后偷偷调用 Die 或派发完成事件。
    /// </summary>
    private async UniTask<bool> WaitGameplayDelayAsync(float duration, int currentSession)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0f, duration);
        while (elapsed < safeDuration)
        {
            try
            {
                await UniTask.Yield(
                    PlayerLoopTiming.Update,
                    this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            if (!IsCurrentSession(currentSession))
                return false;

            if (isPaused || !IsGameplayPlaying())
                continue;

            elapsed += Time.unscaledDeltaTime;
        }

        // 如果暂停切换恰好落在等待结束帧，保留续体并在恢复后继续演出，避免
        // 因状态切换丢失后续流程。
        while (isPaused || !IsGameplayPlaying())
        {
            try
            {
                await UniTask.Yield(
                    PlayerLoopTiming.Update,
                    this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            if (!IsCurrentSession(currentSession))
                return false;
        }

        return IsCurrentSession(currentSession);
    }

    private void FreezeRoundItems()
    {
        for (int index = 0; index < roundItems.Count; index++)
        {
            Level81FruitItem item = roundItems[index];
            if (item != null)
                item.SetSimulationPaused(true);
        }
    }

    private void FreezeActorForBombFocus()
    {
        if (actorPhysicsFrozenForFocus || actor == null || actor.RB == null)
            return;

        Rigidbody2D body = actor.RB;
        focusActorVelocity = body.linearVelocity;
        focusActorAngularVelocity = body.angularVelocity;
        focusActorWasSimulated = body.simulated;
        body.simulated = false;
        actorPhysicsFrozenForFocus = true;
    }

    private void RestoreActorAfterBombFocus()
    {
        if (!actorPhysicsFrozenForFocus)
            return;

        if (actor != null && actor.RB != null)
        {
            Rigidbody2D body = actor.RB;
            body.simulated = focusActorWasSimulated;
            body.linearVelocity = focusActorVelocity;
            body.angularVelocity = focusActorAngularVelocity;
        }

        actorPhysicsFrozenForFocus = false;
        focusActorVelocity = Vector2.zero;
        focusActorAngularVelocity = 0f;
        focusActorWasSimulated = false;
        if (actor != null)
            actor.SetSwordAnimationPaused(isPaused);
    }

    private void FreezeActiveSlices()
    {
        for (int index = 0; index < activeSlices.Count; index++)
            activeSlices[index]?.SetPaused(true);
    }

    private void FreezeBombFocusPresentation()
    {
        bombFocusPresentationFrozen = true;
        SetRuntimeEffectsPaused(true);
        SetSliceTrailPaused(true);
    }

    private void RestoreBombFocusPresentation()
    {
        if (!bombFocusPresentationFrozen)
            return;

        bombFocusPresentationFrozen = false;
        SetRuntimeEffectsPaused(isPaused);
        SetSliceTrailPaused(isPaused);
    }

    private void SetGameplayPanelBlockedByBomb(bool blocked)
    {
        if (gameplayPanelBlockedByBomb == blocked)
            return;

        gameplayPanelBlockedByBomb = blocked;
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, blocked);
    }

    private void SpawnActorExplosion()
    {
        if (actorExplosionPrefab == null || actor == null)
            return;

        GameObject explosion = Instantiate(
            actorExplosionPrefab,
            actor.transform.position,
            Quaternion.identity,
            transform);
        ParticleSystem[] systems = explosion.GetComponentsInChildren<ParticleSystem>(true);
        for (int index = 0; index < systems.Length; index++)
            systems[index].Play(true);
        RegisterRuntimeEffect(explosion, systems, 2f);
    }

    #endregion

    #region Effects And Trail

    private void SpawnFruitSliceEffect(Vector2 position, Color juiceColor)
    {
        if (fruitSliceEffectPrefab == null)
            return;

        GameObject effect = Instantiate(
            fruitSliceEffectPrefab,
            new Vector3(position.x, position.y, -0.5f),
            fruitSliceEffectPrefab.transform.rotation,
            transform);
        effect.transform.localScale *= fruitSliceEffectScale;

        // 导入的视觉 Prefab 带有 Play On Awake 演示音效，会绕过游戏的
        // AudioManager；Level081 这里只复用其中的粒子。
        AudioSource[] effectAudioSources = effect.GetComponentsInChildren<AudioSource>(true);
        for (int index = 0; index < effectAudioSources.Length; index++)
        {
            AudioSource source = effectAudioSources[index];
            source.Stop();
            source.playOnAwake = false;
            source.enabled = false;
        }

        ParticleSystem[] systems = effect.GetComponentsInChildren<ParticleSystem>(true);
        for (int index = 0; index < systems.Length; index++)
        {
            ParticleSystem system = systems[index];
            if (system.name.Equals("Gibs", StringComparison.OrdinalIgnoreCase))
            {
                system.gameObject.SetActive(false);
                continue;
            }

            ParticleSystem.MainModule main = system.main;
            main.startColor = system.name.Equals("Cloud", StringComparison.OrdinalIgnoreCase)
                ? new Color(1f, 1f, 1f, 0.72f)
                : juiceColor;

            ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null)
            {
                int relativeOrder = particleRenderer.sortingOrder;
                particleRenderer.sortingLayerID = SortingLayer.NameToID("SceneLayer");
                particleRenderer.sortingOrder = fruitSliceEffectSortingOrder + relativeOrder;
            }
            system.Play(true);
        }

        RegisterRuntimeEffect(effect, systems, 2.5f);
    }

    private void RegisterRuntimeEffect(
        GameObject root,
        ParticleSystem[] systems,
        float lifetime)
    {
        if (root == null)
            return;

        RuntimeEffect effect = new RuntimeEffect(root, systems, lifetime);
        activeEffects.Add(effect);
        if (isPaused || bombFocusPresentationFrozen)
            effect.SetPaused(true);
    }

    private void UpdateRuntimeEffects(float deltaTime)
    {
        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        for (int index = activeEffects.Count - 1; index >= 0; index--)
        {
            RuntimeEffect effect = activeEffects[index];
            if (effect == null || effect.Root == null)
            {
                activeEffects.RemoveAt(index);
                continue;
            }

            if (effect.PausedByLevel || safeDeltaTime <= 0f)
                continue;

            effect.RemainingLifetime -= safeDeltaTime;
            if (effect.RemainingLifetime > 0f)
                continue;

            Destroy(effect.Root);
            activeEffects.RemoveAt(index);
        }
    }

    private void SetRuntimeEffectsPaused(bool paused)
    {
        for (int index = activeEffects.Count - 1; index >= 0; index--)
        {
            RuntimeEffect effect = activeEffects[index];
            if (effect == null || effect.Root == null)
            {
                activeEffects.RemoveAt(index);
                continue;
            }

            effect.SetPaused(paused);
        }
    }

    private void SetSliceTrailPaused(bool paused)
    {
        sliceTrailPausedByLevel = paused;
        if (sliceTrail == null)
            return;

        sliceTrail.emitting = false;
        sliceTrail.time = paused ? float.MaxValue : trailLifetime;
    }

    private void CleanupRuntimeEffects()
    {
        for (int index = 0; index < activeEffects.Count; index++)
        {
            RuntimeEffect effect = activeEffects[index];
            if (effect != null && effect.Root != null)
                Destroy(effect.Root);
        }
        activeEffects.Clear();
    }

    private void EnsureSliceTrail()
    {
        if (sliceTrail != null)
            return;

        GameObject trailObject = new GameObject("Level081SliceTrail");
        trailObject.transform.SetParent(transform, true);
        sliceTrail = trailObject.AddComponent<TrailRenderer>();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            sliceTrailMaterial = new Material(shader)
            {
                name = "Level081SliceTrailMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
            sliceTrail.material = sliceTrailMaterial;
        }

        sliceTrail.time = sliceTrailPausedByLevel ? float.MaxValue : trailLifetime;
        sliceTrail.minVertexDistance = 0.035f;
        sliceTrail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f));
        sliceTrail.widthMultiplier = trailWidth;
        sliceTrail.textureMode = LineTextureMode.Stretch;
        sliceTrail.alignment = LineAlignment.View;
        sliceTrail.numCapVertices = 3;
        sliceTrail.numCornerVertices = 2;
        sliceTrail.sortingLayerID = SortingLayer.NameToID("SceneLayer");
        sliceTrail.sortingOrder = 1000;
        sliceTrail.emitting = false;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.85f, 0.96f, 1f), 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        sliceTrail.colorGradient = gradient;
    }

    private Vector3 ToTrailPosition(Vector2 worldPosition)
    {
        return new Vector3(worldPosition.x, worldPosition.y, -0.6f);
    }

    #endregion

    #region Events And Setup

    private void RegisterEvents()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        subscriber.Subscribe<GameResumeEvent>(OnGameResume);
        subscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private void OnGamePause(GamePauseEvent _)
    {
        if (resetOrReplayReceived)
            return;

        isPaused = true;
        ClearPointer();
        SetPresentationPaused(true);
    }

    private void OnGameResume(GameResumeEvent _)
    {
        if (resetOrReplayReceived)
            return;

        isPaused = false;
        SetPresentationPaused(false);
        RestoreActorControlAfterPausedUnlock();
        ReinforceActorLock();
        reinforceActorLockNextUpdate = true;
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt == null || resetOrReplayReceived)
            return;

        bool shouldPause = !evt.NewState.Equals(GamePlayStateName.PLAYING);
        isPaused = shouldPause;
        ClearPointer();
        SetPresentationPaused(shouldPause);
        if (!shouldPause)
        {
            RestoreActorControlAfterPausedUnlock();
            ReinforceActorLock();
            reinforceActorLockNextUpdate = true;
        }
    }

    private void OnGameReset(GameResetEvent _)
    {
        if (resetOrReplayReceived)
            return;

        resetOrReplayReceived = true;
        isPaused = true;
        // GameReset 派发时失败关卡还会存活一个短暂转场窗口；使本地异步工作
        // 全部失效，并在角色仍存活时恢复手动 Reset/Replay 所需状态，不触碰
        // 共享关卡控制器。
        CleanupRuntime(true);
    }

    private void SetPresentationPaused(bool paused)
    {
        bool freezeGameplayObjects = paused || isFailing;
        for (int index = 0; index < roundItems.Count; index++)
        {
            Level81FruitItem item = roundItems[index];
            if (item != null)
                item.SetSimulationPaused(freezeGameplayObjects);
        }

        for (int index = 0; index < activeSlices.Count; index++)
            activeSlices[index]?.SetPaused(freezeGameplayObjects);

        bool freezeBombPresentation = paused || bombFocusPresentationFrozen;
        SetRuntimeEffectsPaused(freezeBombPresentation);
        SetSliceTrailPaused(freezeBombPresentation);
        SetFailingActorPhysicsPaused(paused);
        if (actorLocked && actor != null)
            actor.SetSwordAnimationPaused(paused || actorPhysicsFrozenForFocus);
        SetDoorLockAnimationPaused(paused);

        if (hud != null)
            hud.SetPaused(freezeGameplayObjects);

        if (paused)
            cameraFocusTween?.Pause();
        else
            cameraFocusTween?.Play();

        if (actorSpinTween != null && actorSpinTween.IsActive())
        {
            if (paused)
                actorSpinTween.Pause();
            else
                actorSpinTween.Play();
        }
    }

    private void SetFailingActorPhysicsPaused(bool paused)
    {
        if (!paused)
        {
            RestoreFailingActorPhysics();
            return;
        }

        if (actorPhysicsPausedByLevel ||
            actorPhysicsFrozenForFocus ||
            !isFailing ||
            actor == null ||
            actor.RB == null)
        {
            return;
        }

        Rigidbody2D body = actor.RB;
        bool velocityWasCleared = body.linearVelocity.sqrMagnitude <= 0.0001f &&
                                  hasLastFailingActorVelocity;
        pausedActorVelocity = velocityWasCleared
            ? lastFailingActorVelocity
            : body.linearVelocity;
        pausedActorAngularVelocity = velocityWasCleared
            ? lastFailingActorAngularVelocity
            : body.angularVelocity;
        pausedActorWasSimulated = body.simulated;
        body.simulated = false;
        actorPhysicsPausedByLevel = true;
    }

    private void CacheFailingActorVelocity()
    {
        if (!isFailing ||
            actorPhysicsPausedByLevel ||
            actorPhysicsFrozenForFocus ||
            actor == null ||
            actor.RB == null ||
            !actor.RB.simulated)
        {
            return;
        }

        Vector2 velocity = actor.RB.linearVelocity;
        if (velocity.sqrMagnitude <= 0.0001f)
            return;

        lastFailingActorVelocity = velocity;
        lastFailingActorAngularVelocity = actor.RB.angularVelocity;
        hasLastFailingActorVelocity = true;
    }

    private void RestoreFailingActorPhysics()
    {
        if (!actorPhysicsPausedByLevel)
            return;

        if (actor != null && actor.RB != null)
        {
            Rigidbody2D body = actor.RB;
            body.simulated = pausedActorWasSimulated;
            body.linearVelocity = pausedActorVelocity;
            body.angularVelocity = pausedActorAngularVelocity;
        }

        actorPhysicsPausedByLevel = false;
        pausedActorVelocity = Vector2.zero;
        pausedActorAngularVelocity = 0f;
        pausedActorWasSimulated = false;
        actorPhysicsFrozenForFocus = false;
        focusActorVelocity = Vector2.zero;
        focusActorAngularVelocity = 0f;
        focusActorWasSimulated = false;
        lastFailingActorVelocity = Vector2.zero;
        lastFailingActorAngularVelocity = 0f;
        hasLastFailingActorVelocity = false;
    }

    private void ResolveReferences()
    {
        baseLevelCtrl ??= GetComponent<BaseLevelCtrl>();
        actor ??= baseLevelCtrl != null
            ? baseLevelCtrl.Actor as Level81ActorCtrl
            : GetComponentInChildren<Level81ActorCtrl>(true);
        worldCamera ??= Camera.main;
        bubbleRoot ??= transform.Find("bubble");
        imageLockRenderer ??= transform.Find("lock")?.GetComponent<SpriteRenderer>();
        keyRoot ??= transform.Find("Key");
        keyCollider ??= keyRoot != null ? keyRoot.GetComponent<Collider2D>() : null;
        keyRenderer ??= keyRoot != null
            ? keyRoot.GetComponentInChildren<SpriteRenderer>(true)
            : null;
        doorLockRoot ??= transform.Find("DoorLock")?.gameObject;
        keyDoorTarget ??= transform.Find("door_0/LockPoint");
        bubblePlatformCollider ??= transform.Find("Sp_l83_tz_01")?.GetComponent<Collider2D>();
        keyFloatHandle ??= keyRoot != null ? keyRoot.GetComponent<MoveFloatHandle>() : null;
        doorLockSpineCtrl ??= doorLockRoot != null
            ? doorLockRoot.GetComponent<ActorSpineCtrl>()
            : null;
    }

    private void CacheCameraState()
    {
        if (worldCamera == null)
            return;

        originalCameraSize = worldCamera.orthographicSize;
        originalCameraPosition = worldCamera.transform.position;
        hasOriginalCameraSize = true;
        hasOriginalCameraPosition = true;
    }

    private void CacheInitialPresentationState()
    {
        if (bubbleRoot != null)
        {
            initialBubbleLocalPosition = bubbleRoot.localPosition;
            hasInitialBubbleTransform = true;
        }

        if (keyRoot == null)
            return;

        initialKeyLocalPosition = keyRoot.localPosition;
        initialKeyLocalRotation = keyRoot.localRotation;
        initialKeyLocalScale = keyRoot.localScale;
        hasInitialKeyTransform = true;

        keyRenderer ??= keyRoot.GetComponentInChildren<SpriteRenderer>(true);
        if (keyRenderer != null)
        {
            initialKeyRendererColor = keyRenderer.color;
            hasInitialKeyRendererColor = true;
        }
    }

    private void InitializePresentation()
    {
        if (bubbleRoot != null)
        {
            if (hasInitialBubbleTransform)
                bubbleRoot.localPosition = initialBubbleLocalPosition;
            bubbleFloatPhase = 0f;
            bubbleRoot.gameObject.SetActive(true);
        }
        if (exitDoorCollider != null)
            exitDoorCollider.enabled = false;
        if (imageLockRenderer != null)
            imageLockRenderer.enabled = true;
        if (doorLockRoot != null)
        {
            SetDoorLockAnimationPaused(false);
            doorLockRoot.SetActive(false);
        }
        if (keyRoot != null)
        {
            PrepareKeyForLevelSequence();
            keyRoot.gameObject.SetActive(false);
            if (hasInitialKeyTransform)
            {
                keyRoot.localPosition = initialKeyLocalPosition;
                keyRoot.localRotation = initialKeyLocalRotation;
                keyRoot.localScale = initialKeyLocalScale;
            }
            RestoreKeyRendererColor();
        }
        if (keyCollider != null)
            keyCollider.enabled = false;
    }

    private void ResetRuntimeFlags()
    {
        currentScore = 0;
        roundFruitTotal = 0;
        roundFruitSliced = 0;
        roundDelayRemaining = 0f;
        actorWasCtrlable = false;
        actorLocked = false;
        miniGameActive = false;
        roundActive = false;
        swingPlayedForGesture = false;
        completionStarted = false;
        doorOpened = false;
        isFailing = false;
        bombFailureEventDispatched = false;
        reinforceActorLockNextUpdate = false;
        restoreActorControlOnResume = false;
        gameplayPanelBlockedByBomb = false;
        doorLockSpinePaused = false;
        doorLockSpineTimeScaleBeforePause = 1f;
        actorPhysicsPausedByLevel = false;
        pausedActorVelocity = Vector2.zero;
        pausedActorAngularVelocity = 0f;
        pausedActorWasSimulated = false;
        bombFocusPresentationFrozen = false;
        sliceTrailPausedByLevel = false;
        bubbleFloatPhase = 0f;
    }

    private void PrepareKeyForLevelSequence()
    {
        if (keyRoot == null)
            return;

        keyFloatHandle ??= keyRoot.GetComponent<MoveFloatHandle>();
        if (keyFloatHandle == null)
            return;

        keyFloatHandle.StopMove();
        keyFloatHandle.enabled = false;
    }

    private void RestoreKeyRendererColor()
    {
        if (keyRenderer == null && keyRoot != null)
            keyRenderer = keyRoot.GetComponentInChildren<SpriteRenderer>(true);
        if (keyRenderer == null)
            return;

        if (!hasInitialKeyRendererColor)
        {
            initialKeyRendererColor = keyRenderer.color;
            hasInitialKeyRendererColor = true;
        }

        keyRenderer.color = initialKeyRendererColor;
    }

    private void SetDoorLockAnimationPaused(bool paused)
    {
        doorLockSpineCtrl ??= doorLockRoot != null
            ? doorLockRoot.GetComponent<ActorSpineCtrl>()
            : null;
        Spine.AnimationState animationState = doorLockSpineCtrl != null
            ? doorLockSpineCtrl.AnimationState
            : null;
        if (paused)
        {
            if (doorLockSpinePaused || animationState == null)
                return;

            doorLockSpineTimeScaleBeforePause = animationState.TimeScale;
            animationState.TimeScale = 0f;
            doorLockSpinePaused = true;
            return;
        }

        if (!doorLockSpinePaused)
            return;

        if (animationState != null)
            animationState.TimeScale = doorLockSpineTimeScaleBeforePause;
        doorLockSpineTimeScaleBeforePause = 1f;
        doorLockSpinePaused = false;
    }

    private void RestoreActorControlAfterPausedUnlock()
    {
        if (!restoreActorControlOnResume ||
            isPaused ||
            !isActiveAndEnabled ||
            !IsGameplayPlaying())
        {
            return;
        }

        restoreActorControlOnResume = false;
        if (actorWasCtrlable)
        {
            actor?.ResumeMoving();
            baseLevelCtrl?.SetCtrlable(true);
        }
        actorWasCtrlable = false;
    }

    private void RestoreActorForReuse()
    {
        if (actor == null ||
            actor.IsDied ||
            (!actorLocked && !restoreActorControlOnResume))
            return;

        actor.ExitSwordMode();
        actor.SetMoveInput(0f);
        if (actorWasCtrlable && baseLevelCtrl != null)
        {
            actor.ResumeMoving();
            baseLevelCtrl.SetCtrlable(true);
        }
        else if (!actorWasCtrlable)
        {
            actor.StopMoving();
        }
    }

    private void EnsureHud()
    {
        if (hud != null)
            return;

        PlayCtrl playCtrl = PlayCtrl.Instance;
        GamePlayPanel gamePlayPanel = playCtrl != null
            ? playCtrl.CurrentGamePlayPanel
            : null;
        if (gamePlayPanel == null)
            return;

        hud = gamePlayPanel.GetComponentInChildren<Level81GameplayHud>(true);
        if (hud == null)
            return;

        hud.InitializeForLevel(GetKeySpriteForHud());
        hud.SetTarget(targetScore);
        hud.SetScore(currentScore, false);
        if (miniGameActive && !completionStarted && !isFailing)
            hud.Show();
    }

    private Sprite GetKeySpriteForHud()
    {
        if (keyRoot == null)
            return null;

        SpriteRenderer keyRenderer = keyRoot.GetComponentInChildren<SpriteRenderer>(true);
        return keyRenderer != null ? keyRenderer.sprite : null;
    }

    private void ValidateConfiguration()
    {
        if (baseLevelCtrl == null || actor == null)
            Debug.LogError("[Level81Ctrl] BaseLevelCtrl or Level81ActorCtrl is missing.", this);
        if (bubbleRoot == null || bubblePlatformCollider == null || imageLockRenderer == null)
            Debug.LogError("[Level81Ctrl] Bubble, bubble platform, or image lock binding is missing.", this);
        if (exitDoorCollider == null || keyRoot == null || doorLockRoot == null)
            Debug.LogError("[Level81Ctrl] Key, DoorLock, or exit collider binding is missing.", this);
        if (fruits == null || fruits.Length == 0 || bombSprite == null)
            Debug.LogError("[Level81Ctrl] Fruit or bomb art is missing.", this);
    }

    private void LockActorForMiniGame()
    {
        if (actorLocked)
            return;

        actorWasCtrlable = baseLevelCtrl == null || baseLevelCtrl.ActorCtrlable;
        if (baseLevelCtrl != null)
            baseLevelCtrl.SetCtrlable(false);
        if (actor != null)
        {
            actor.SetMoveInput(0f);
            actor.StopMoving();
        }
        actorLocked = true;
    }

    private void ReinforceActorLock()
    {
        if (!actorLocked || doorOpened || actor == null)
            return;

        // 暂停面板关闭时 BaseLevelCtrl 会恢复 Actor 移动；在任一开锁路线成功
        // 前，持续压住本关剑术阶段的移动。
        if (isFailing && actor.IsDied && actor.RB != null)
        {
            Vector2 knockVelocity = actor.RB.linearVelocity;
            actor.SetMoveInput(0f);
            actor.StopMoving();
            actor.RB.linearVelocity = knockVelocity;
            return;
        }

        actor.SetMoveInput(0f);
        actor.StopMoving();
    }

    #endregion

    #region Input Helpers

    private bool TryGetActiveTouch(out Touch activeTouch)
    {
        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (touch.fingerId != activeFingerId)
                continue;

            activeTouch = touch;
            return true;
        }

        activeTouch = default;
        return false;
    }

    private bool CanReceiveSliceInput()
    {
        return miniGameActive &&
               !completionStarted &&
               !isFailing &&
               !isPaused &&
               IsGameplayPlaying();
    }

    private static bool IsPointerOverUi(Vector2 screenPosition, int fingerId)
    {
        return fingerId >= 0
            ? InputUtils.IsPointerOverUI(fingerId)
            : InputUtils.IsPointerOverUI(screenPosition);
    }

    private Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        if (worldCamera == null)
            return screenPosition;

        float distance = Mathf.Abs(worldCamera.transform.position.z - transform.position.z);
        Vector3 world = worldCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, distance));
        return world;
    }

    private Vector2 ViewportToWorld(float viewportX, float viewportY)
    {
        if (worldCamera == null)
            return Vector2.zero;

        float distance = Mathf.Abs(worldCamera.transform.position.z - transform.position.z);
        Vector3 world = worldCamera.ViewportToWorldPoint(
            new Vector3(viewportX, viewportY, distance));
        return world;
    }

    private static bool SegmentIntersectsBounds(Vector2 start, Vector2 end, Bounds bounds)
    {
        Rect rect = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        if (rect.Contains(start) || rect.Contains(end))
            return true;

        Vector2 min = rect.min;
        Vector2 max = rect.max;
        return SegmentsIntersect(start, end, new Vector2(min.x, min.y), new Vector2(max.x, min.y)) ||
               SegmentsIntersect(start, end, new Vector2(max.x, min.y), new Vector2(max.x, max.y)) ||
               SegmentsIntersect(start, end, new Vector2(max.x, max.y), new Vector2(min.x, max.y)) ||
               SegmentsIntersect(start, end, new Vector2(min.x, max.y), new Vector2(min.x, min.y));
    }

    private static bool SegmentsIntersect(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
    {
        Vector2 firstDelta = firstEnd - firstStart;
        Vector2 secondDelta = secondEnd - secondStart;
        float denominator = firstDelta.x * secondDelta.y - firstDelta.y * secondDelta.x;
        if (Mathf.Abs(denominator) <= 0.00001f)
            return false;

        Vector2 offset = secondStart - firstStart;
        float firstT = (offset.x * secondDelta.y - offset.y * secondDelta.x) / denominator;
        float secondT = (offset.x * firstDelta.y - offset.y * firstDelta.x) / denominator;
        return firstT >= 0f && firstT <= 1f && secondT >= 0f && secondT <= 1f;
    }

    private void ClearPointer()
    {
        if (sliceTrail != null)
            sliceTrail.emitting = false;
        pointerMode = PointerMode.None;
        activeFingerId = -1;
        latestScreenPosition = Vector2.zero;
        latestWorldPosition = Vector2.zero;
        swingPlayedForGesture = false;
        gestureResliceCandidates.Clear();
    }

    private static bool IsGameplayPlaying()
    {
        PlayCtrl playCtrl = PlayCtrl.Instance;
        return playCtrl != null &&
               playCtrl.IsInitialized &&
               playCtrl.IsActive &&
               playCtrl.CheckState(GamePlayStateName.PLAYING);
    }

    private bool IsCurrentSession(int currentSession)
    {
        return currentSession == sessionVersion &&
               isActiveAndEnabled &&
               !resetOrReplayReceived;
    }

    #endregion

    #region Cleanup

    private void DiscardRoundItems()
    {
        for (int index = 0; index < roundItems.Count; index++)
        {
            Level81FruitItem item = roundItems[index];
            if (item != null)
                item.Discard();
        }
        roundItems.Clear();
        roundActive = false;
    }

    private void PruneInactiveSlices()
    {
        for (int index = activeSlices.Count - 1; index >= 0; index--)
        {
            Level81SliceMesh.SliceResult result = activeSlices[index];
            if (result != null && result.IsAlive)
                continue;

            result?.Dispose();
            activeSlices.RemoveAt(index);
        }
    }

    private void CleanupRuntime(bool restoreActor)
    {
        if (cleanupComplete)
            return;

        // Reset/Disable 到 PlayCtrl 销毁旧实例前可能会有短暂过渡帧；立即恢复预置表现，
        // 避免钥匙飞行或已解锁的 DoorLock 泄漏到下一次转场。已经打开出口的关卡保留
        // 完成态表现，直到对象真正销毁。
        bool resetAuthoredPresentation = resetOrReplayReceived || !doorOpened;
        cleanupComplete = true;
        sessionVersion++;
        ClearPointer();
        cameraFocusTween?.Kill();
        cameraFocusTween = null;
        actorSpinTween?.Kill();
        actorSpinTween = null;
        SetGameplayPanelBlockedByBomb(false);
        SetDoorLockAnimationPaused(false);
        if (keyFloatHandle != null)
            keyFloatHandle.StopMove();
        RestoreFailingActorPhysics();
        RestoreBombFocusPresentation();
        RestoreActorAfterBombFocus();
        DiscardRoundItems();
        CleanupRuntimeEffects();

        for (int index = 0; index < activeSlices.Count; index++)
            activeSlices[index]?.Dispose();
        activeSlices.Clear();

        if (hud != null)
            hud.InitializeForLevel(GetKeySpriteForHud());
        hud = null;

        if (sliceTrail != null)
            Destroy(sliceTrail.gameObject);
        sliceTrail = null;
        if (sliceTrailMaterial != null)
            Destroy(sliceTrailMaterial);
        sliceTrailMaterial = null;

        if (worldCamera != null && hasOriginalCameraSize)
            worldCamera.orthographicSize = originalCameraSize;
        if (worldCamera != null && hasOriginalCameraPosition)
            worldCamera.transform.position = originalCameraPosition;

        if (resetAuthoredPresentation)
            InitializePresentation();

        if (restoreActor && actorLocked && actor != null && !actor.IsDied)
        {
            RestoreActorForReuse();
        }

        ResetRuntimeFlags();
        isPaused = true;
    }

    #endregion
}

using Bear.EventSystem;
using Game.Events;
using Game.Level;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level 109 私有关卡控制器：分别记录玩家从出生点向左、向右到达过的最远距离，
/// 达到路线阈值后，在下一循环显示对应出口门。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level109Ctrl : MonoBehaviour
{
    #region Inspector

    [Header("场景引用")]
    [Tooltip("本关基础关卡控制器。")]
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [Tooltip("本关玩家；入口门演出结束且恢复控制后，才记录它的世界 X 作为循环起点。")]
    [SerializeField] private ActorCtrl actor;
    [Tooltip("左侧长平台路线的出口门。")]
    [SerializeField] private Transform leftExitDoor;
    [Tooltip("左侧出口门的标准成功动画。")]
    [SerializeField] private SuccessAnimCtrl leftExitDoorSuccessAnim;
    [Tooltip("右侧四平台路线的出口门。")]
    [SerializeField] private Transform rightExitDoor;
    [Tooltip("右侧出口门的标准成功动画。")]
    [SerializeField] private SuccessAnimCtrl rightExitDoorSuccessAnim;

    [Header("路线循环")]
    [Tooltip("Level023 遗留的混合循环器。本关会禁用它，避免右侧高台被搬到左侧。")]
    [SerializeField] private InfiniteScrollMap mixedGameplayLoop;
    [Tooltip("左侧单个循环块的父节点；地面和需要一起循环的装饰都放在这里。")]
    [SerializeField] private Transform leftCycleRoot;
    [Tooltip("右侧单个循环块的父节点；四个平台和需要一起循环的装饰都放在这里。")]
    [SerializeField] private Transform rightCycleRoot;
    [Tooltip("左侧循环边界只读取这个地形节点，避免装饰 Collider 改变回收时机。")]
    [SerializeField] private Transform leftTerrainSource;
    [Tooltip("右侧循环边界只读取这个平台组，避免装饰 Collider 改变回收时机。")]
    [SerializeField] private Transform rightTerrainSource;

    [Header("循环阈值")]
    [Tooltip("向左完成多少次循环后，在左侧下一循环显示门。")]
    [SerializeField, Min(1)] private int leftCyclesToUnlock = 4;
    [Tooltip("向右完成多少次循环后，在右侧下一循环显示门。")]
    [SerializeField, Min(1)] private int rightCyclesToUnlock = 6;
    [Tooltip("左侧 Ground_long 地面每次循环的世界宽度。")]
    [SerializeField, Min(0.01f)] private float leftCycleWidth = 28f;
    [Tooltip("右侧四平台组每次循环的世界宽度。")]
    [SerializeField, Min(0.01f)] private float rightCycleWidth = 25f;

    [Header("出口门显示")]
    [Tooltip("达到阈值后，将两扇模板门分别沿路线方向移动到下一循环。")]
    [SerializeField] private bool moveDoorsToUnlockLoop = true;
    [Tooltip("两扇出口门在达到各自条件前保持隐藏。")]
    [SerializeField] private bool hideDoorsUntilUnlocked = true;
    [Tooltip("距离计算误差容限，避免浮点误差导致刚好走满一格时漏记。")]
    [SerializeField, Min(0f)] private float cyclePositionTolerance = 0.05f;
    [Tooltip("开始记录角色位置前的最短等待时间，用于避开入口门演出的初始帧。")]
    [SerializeField, Min(0f)] private float trackingStartDelaySeconds = 0.1f;

    #endregion

    #region Runtime state

    private const int RouteTilePoolSize = 3;

    private EventSubscriber resetSubscriber;
    private OnTrigger2DHandle leftRuntimeDoorTrigger;
    private OnTrigger2DHandle rightRuntimeDoorTrigger;
    private Vector3 leftDoorInitialPosition;
    private Vector3 rightDoorInitialPosition;
    private Vector3 leftRouteAnchorInitialPosition;
    private Vector3 rightRouteAnchorInitialPosition;
    private Vector3 leftDoorOffsetFromRouteAnchor;
    private Vector3 rightDoorOffsetFromRouteAnchor;
    private readonly Transform[] leftRouteTiles = new Transform[RouteTilePoolSize];
    private readonly Transform[] rightRouteTiles = new Transform[RouteTilePoolSize];
    private readonly int[] leftRouteTileIndices = new int[RouteTilePoolSize];
    private readonly int[] rightRouteTileIndices = new int[RouteTilePoolSize];
    private readonly GameObject[] runtimeRouteClones = new GameObject[RouteTilePoolSize + 1];
    private int runtimeRouteCloneCount;
    private float leftRouteCoverageStartX;
    private float rightRouteCoverageStartX;
    private float actorInitialX;
    private float leftMinReachedX;
    private float rightMaxReachedX;
    private float trackingCanBeginAt;
    private int leftReachedCycles;
    private int rightReachedCycles;
    private bool progressTrackingInitialized;
    private bool routePoolsReady;
    private bool leftDoorUnlocked;
    private bool rightDoorUnlocked;
    private bool successRequested;
    private bool runtimeReady;
    private bool isShuttingDown;
    private bool resetOrReplayReceived;

    #endregion

    #region Unity lifecycle

    private void Awake()
    {
        isShuttingDown = false;
        resetOrReplayReceived = false;
        ResolveReferences();
        PrepareLegacyLevel23Flow();
        PrepareExitDoors();
        PrepareRouteLoops();
        runtimeReady = runtimeReady && routePoolsReady;
        trackingCanBeginAt = Time.unscaledTime + Mathf.Max(0f, trackingStartDelaySeconds);
    }

    private void OnEnable()
    {
        if (!resetOrReplayReceived)
        {
            isShuttingDown = false;
            // 关卡根节点可能被临时禁用再启用；恢复本关动态触发器，
            // 但 Reset/Replay 已进入 teardown 的旧实例不允许重新接管碰撞。
            if (runtimeReady && !successRequested)
                SetRuntimeDoorTriggersEnabled(true);
        }
        EventsUtils.ResetEvents(ref resetSubscriber);
        resetSubscriber.Subscribe<GameResetEvent>(OnGameReset);
        resetSubscriber.Subscribe<GameReplayEvent>(OnGameReplay);
    }

    private void Update()
    {
        if (!runtimeReady || isShuttingDown || successRequested)
            return;

        UpdateRouteTilePools();

        if (!progressTrackingInitialized)
        {
            InitializeProgressTracking();
            if (!progressTrackingInitialized)
                return;
        }

        UpdateRouteProgress();
        TryUnlockReachedDoors();
    }

    private void OnDisable() => SuspendRuntime();

    private void OnDestroy()
    {
        SuspendRuntime();

        if (leftRuntimeDoorTrigger != null)
            leftRuntimeDoorTrigger.OnEnter.RemoveListener(OnLeftDoorTrigger);
        if (rightRuntimeDoorTrigger != null)
            rightRuntimeDoorTrigger.OnEnter.RemoveListener(OnRightDoorTrigger);

        CleanupRuntimeRouteClones();
    }

    private void SuspendRuntime()
    {
        isShuttingDown = true;
        SetRuntimeDoorTriggersEnabled(false);
        EventsUtils.ResetEvents(ref resetSubscriber);
    }

    #endregion

    #region Initialization

    private void ResolveReferences()
    {
        levelCtrl ??= GetComponent<BaseLevelCtrl>();
        actor ??= levelCtrl != null ? levelCtrl.Actor : null;
        actor ??= GetComponentInChildren<ActorCtrl>(true);

        if (mixedGameplayLoop == null)
        {
            InfiniteScrollMap[] loops = GetComponents<InfiniteScrollMap>();
            if (loops.Length > 0)
                mixedGameplayLoop = loops[0];
        }

        leftExitDoor ??= transform.Find("door_0");
        rightExitDoor ??= transform.Find("door_0 (1)");
        leftCycleRoot ??= transform.Find("LeftGroundLoopItem");
        rightCycleRoot ??= transform.Find("RightPlatformLoopItem");
        leftTerrainSource ??= leftCycleRoot != null
            ? leftCycleRoot.Find("Ground_long")
            : null;
        rightTerrainSource ??= rightCycleRoot != null
            ? rightCycleRoot.Find("Platsform")
            : null;

        if (leftExitDoorSuccessAnim == null && leftExitDoor != null)
            leftExitDoorSuccessAnim = leftExitDoor.GetComponent<SuccessAnimCtrl>();
        if (rightExitDoorSuccessAnim == null && rightExitDoor != null)
            rightExitDoorSuccessAnim = rightExitDoor.GetComponent<SuccessAnimCtrl>();

        runtimeReady = levelCtrl != null && actor != null &&
                       leftExitDoor != null && rightExitDoor != null &&
                       leftExitDoorSuccessAnim != null && rightExitDoorSuccessAnim != null &&
                       mixedGameplayLoop != null && leftCycleRoot != null &&
                       rightCycleRoot != null && leftTerrainSource != null &&
                       rightTerrainSource != null;

        if (!runtimeReady)
        {
            Debug.LogWarning(
                "[Level109Ctrl] 本关引用不完整，左右循环计数与出口门逻辑不会运行。",
                this);
        }
    }

    private void PrepareLegacyLevel23Flow()
    {
        // Level 109 不使用 Level 023 的点击直通通关区域。
        Transform clickArea = transform.Find("ClickArea");
        if (clickArea != null)
            clickArea.gameObject.SetActive(false);

        Level23Ctrl legacyController = GetComponent<Level23Ctrl>();
        if (legacyController != null)
            legacyController.enabled = false;
    }

    private void PrepareExitDoors()
    {
        if (leftExitDoor != null)
        {
            leftDoorInitialPosition = leftExitDoor.position;
            DisableLegacyDoorTriggers(leftExitDoor);
            leftRuntimeDoorTrigger = leftExitDoor.gameObject.AddComponent<OnTrigger2DHandle>();
            leftRuntimeDoorTrigger.SetTargetTag(string.Empty);
            leftRuntimeDoorTrigger.enabled = true;
            leftRuntimeDoorTrigger.OnEnter.AddListener(OnLeftDoorTrigger);

            if (hideDoorsUntilUnlocked)
                leftExitDoor.gameObject.SetActive(false);
        }

        if (rightExitDoor != null)
        {
            rightDoorInitialPosition = rightExitDoor.position;
            DisableLegacyDoorTriggers(rightExitDoor);
            rightRuntimeDoorTrigger = rightExitDoor.gameObject.AddComponent<OnTrigger2DHandle>();
            rightRuntimeDoorTrigger.SetTargetTag(string.Empty);
            rightRuntimeDoorTrigger.enabled = true;
            rightRuntimeDoorTrigger.OnEnter.AddListener(OnRightDoorTrigger);

            if (hideDoorsUntilUnlocked)
                rightExitDoor.gameObject.SetActive(false);
        }
    }

    private void PrepareRouteLoops()
    {
        routePoolsReady = false;
        if (!runtimeReady)
            return;

        // Level023 的共享循环器会把同一组高台同时铺向左右，不符合本关两条独立路线。
        mixedGameplayLoop.enabled = false;

        float safeLeftWidth = Mathf.Max(0.01f, leftCycleWidth);
        float safeRightWidth = Mathf.Max(0.01f, rightCycleWidth);

        leftRouteAnchorInitialPosition = leftCycleRoot.position;
        rightRouteAnchorInitialPosition = rightCycleRoot.position;
        leftDoorOffsetFromRouteAnchor = leftDoorInitialPosition - leftRouteAnchorInitialPosition;
        rightDoorOffsetFromRouteAnchor = rightDoorInitialPosition - rightRouteAnchorInitialPosition;
        leftRouteCoverageStartX = GetSolidColliderMinX(
            leftTerrainSource,
            leftRouteAnchorInitialPosition.x - safeLeftWidth * 0.5f);
        rightRouteCoverageStartX = GetSolidColliderMinX(
            rightTerrainSource,
            rightRouteAnchorInitialPosition.x - safeRightWidth * 0.5f);

        leftRouteTiles[0] = CreateRuntimeRouteClone(leftCycleRoot, "LeftLoopItem Runtime -2");
        leftRouteTiles[1] = CreateRuntimeRouteClone(leftCycleRoot, "LeftLoopItem Runtime -1");
        leftRouteTiles[2] = leftCycleRoot;
        InitializeRoutePool(
            leftRouteTiles,
            leftRouteTileIndices,
            leftRouteAnchorInitialPosition,
            safeLeftWidth,
            -2);

        rightRouteTiles[0] = rightCycleRoot;
        rightRouteTiles[1] = CreateRuntimeRouteClone(rightCycleRoot, "RightLoopItem Runtime 1");
        rightRouteTiles[2] = CreateRuntimeRouteClone(rightCycleRoot, "RightLoopItem Runtime 2");
        InitializeRoutePool(
            rightRouteTiles,
            rightRouteTileIndices,
            rightRouteAnchorInitialPosition,
            safeRightWidth,
            0);

        routePoolsReady = true;
    }

    private Transform CreateRuntimeRouteClone(Transform template, string cloneName)
    {
        GameObject clone = Instantiate(template.gameObject, template.parent);
        clone.name = cloneName;
        runtimeRouteClones[runtimeRouteCloneCount++] = clone;
        return clone.transform;
    }

    private static float GetSolidColliderMinX(Transform tile, float fallback)
    {
        float minX = float.PositiveInfinity;
        Collider2D[] colliders = tile.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;

            minX = Mathf.Min(minX, collider.bounds.min.x);
        }

        return float.IsPositiveInfinity(minX) ? fallback : minX;
    }

    private static void InitializeRoutePool(
        Transform[] tiles,
        int[] tileIndices,
        Vector3 anchor,
        float cycleWidth,
        int firstIndex)
    {
        for (int i = 0; i < RouteTilePoolSize; i++)
        {
            int tileIndex = firstIndex + i;
            tileIndices[i] = tileIndex;
            PositionRouteTile(tiles[i], anchor, cycleWidth, tileIndex);
        }
    }

    private static OnTrigger2DHandle[] DisableLegacyDoorTriggers(Transform door)
    {
        OnTrigger2DHandle[] triggers = door.GetComponents<OnTrigger2DHandle>();
        for (int i = 0; i < triggers.Length; i++)
        {
            if (triggers[i] != null)
                triggers[i].enabled = false;
        }

        return triggers;
    }

    private void InitializeProgressTracking()
    {
        // BaseLevelCtrl 在首次进关时会先播放入口门演出；不能把这段自动位移计入循环距离。
        if (levelCtrl == null || !levelCtrl.ActorCtrlable || actor == null ||
            Time.unscaledTime < trackingCanBeginAt)
            return;

        actorInitialX = actor.transform.position.x;
        leftMinReachedX = actorInitialX;
        rightMaxReachedX = actorInitialX;
        leftReachedCycles = 0;
        rightReachedCycles = 0;
        progressTrackingInitialized = true;
    }

    #endregion

    #region Route tracking

    private void UpdateRouteTilePools()
    {
        if (!routePoolsReady || actor == null)
            return;

        float actorX = actor.transform.position.x;
        float safeLeftWidth = Mathf.Max(0.01f, leftCycleWidth);
        float safeRightWidth = Mathf.Max(0.01f, rightCycleWidth);

        int leftCurrentIndex = Mathf.Min(
            0,
            Mathf.FloorToInt((actorX - leftRouteCoverageStartX) / safeLeftWidth));
        int leftHighestIndex = Mathf.Min(0, leftCurrentIndex + 1);
        EnsureRoutePoolRange(
            leftRouteTiles,
            leftRouteTileIndices,
            leftRouteAnchorInitialPosition,
            safeLeftWidth,
            leftHighestIndex - (RouteTilePoolSize - 1));

        int rightCurrentIndex = Mathf.Max(
            0,
            Mathf.FloorToInt((actorX - rightRouteCoverageStartX) / safeRightWidth));
        int rightFirstIndex = Mathf.Max(0, rightCurrentIndex - 1);
        EnsureRoutePoolRange(
            rightRouteTiles,
            rightRouteTileIndices,
            rightRouteAnchorInitialPosition,
            safeRightWidth,
            rightFirstIndex);
    }

    private static void EnsureRoutePoolRange(
        Transform[] tiles,
        int[] tileIndices,
        Vector3 anchor,
        float cycleWidth,
        int firstIndex)
    {
        int lastIndex = firstIndex + RouteTilePoolSize - 1;
        for (int desiredIndex = firstIndex; desiredIndex <= lastIndex; desiredIndex++)
        {
            bool alreadyAssigned = false;
            for (int i = 0; i < RouteTilePoolSize; i++)
            {
                if (tileIndices[i] == desiredIndex)
                {
                    alreadyAssigned = true;
                    break;
                }
            }

            if (alreadyAssigned)
                continue;

            for (int i = 0; i < RouteTilePoolSize; i++)
            {
                if (tileIndices[i] >= firstIndex && tileIndices[i] <= lastIndex)
                    continue;

                tileIndices[i] = desiredIndex;
                PositionRouteTile(tiles[i], anchor, cycleWidth, desiredIndex);
                break;
            }
        }
    }

    private static void PositionRouteTile(
        Transform tile,
        Vector3 anchor,
        float cycleWidth,
        int tileIndex)
    {
        if (tile == null)
            return;

        Vector3 position = anchor;
        position.x += tileIndex * cycleWidth;
        tile.position = position;
        tile.gameObject.SetActive(true);
    }

    private void UpdateRouteProgress()
    {
        if (actor == null || actor.IsDied)
            return;

        float actorX = actor.transform.position.x;
        leftMinReachedX = Mathf.Min(leftMinReachedX, actorX);
        rightMaxReachedX = Mathf.Max(rightMaxReachedX, actorX);

        float leftDistance = actorInitialX - leftMinReachedX;
        float rightDistance = rightMaxReachedX - actorInitialX;
        leftReachedCycles = Mathf.Max(
            leftReachedCycles,
            GetReachedCycles(leftDistance, leftCycleWidth));
        rightReachedCycles = Mathf.Max(
            rightReachedCycles,
            GetReachedCycles(rightDistance, rightCycleWidth));
    }

    private int GetReachedCycles(float travelledDistance, float cycleWidth)
    {
        if (travelledDistance <= 0f)
            return 0;

        float safeWidth = Mathf.Max(0.01f, cycleWidth);
        float normalized =
            (travelledDistance + Mathf.Max(0f, cyclePositionTolerance)) / safeWidth;
        return Mathf.Max(0, Mathf.FloorToInt(normalized));
    }

    private void TryUnlockReachedDoors()
    {
        if (!leftDoorUnlocked && leftReachedCycles >= Mathf.Max(1, leftCyclesToUnlock))
            UnlockLeftDoor();

        if (!rightDoorUnlocked && rightReachedCycles >= Mathf.Max(1, rightCyclesToUnlock))
            UnlockRightDoor();
    }

    private void UnlockLeftDoor()
    {
        if (leftExitDoor == null)
            return;

        if (moveDoorsToUnlockLoop)
        {
            Vector3 position = leftRouteAnchorInitialPosition + leftDoorOffsetFromRouteAnchor;
            position.x -= Mathf.Max(1, leftCyclesToUnlock) * Mathf.Max(0.01f, leftCycleWidth);
            leftExitDoor.position = position;
        }

        leftDoorUnlocked = true;
        leftExitDoor.gameObject.SetActive(true);
        SetSuccessDoor(leftExitDoor, ref leftExitDoorSuccessAnim);
    }

    private void UnlockRightDoor()
    {
        if (rightExitDoor == null)
            return;

        if (moveDoorsToUnlockLoop)
        {
            Vector3 position = rightRouteAnchorInitialPosition + rightDoorOffsetFromRouteAnchor;
            position.x += Mathf.Max(1, rightCyclesToUnlock) * Mathf.Max(0.01f, rightCycleWidth);
            rightExitDoor.position = position;
        }

        rightDoorUnlocked = true;
        rightExitDoor.gameObject.SetActive(true);
        SetSuccessDoor(rightExitDoor, ref rightExitDoorSuccessAnim);
    }

    private void SetSuccessDoor(Transform door, ref SuccessAnimCtrl successAnim)
    {
        if (successAnim == null && door != null)
            successAnim = door.GetComponent<SuccessAnimCtrl>();

        if (levelCtrl != null && successAnim != null)
            levelCtrl.SetSuccessAnim(successAnim);
    }

    #endregion

    #region Door trigger and reset

    private void OnLeftDoorTrigger(Collider2D collider)
    {
        TryEnterDoor(
            leftDoorUnlocked,
            leftExitDoor,
            ref leftExitDoorSuccessAnim,
            collider);
    }

    private void OnRightDoorTrigger(Collider2D collider)
    {
        TryEnterDoor(
            rightDoorUnlocked,
            rightExitDoor,
            ref rightExitDoorSuccessAnim,
            collider);
    }

    private void TryEnterDoor(
        bool doorUnlocked,
        Transform door,
        ref SuccessAnimCtrl successAnim,
        Collider2D collider)
    {
        if (!doorUnlocked || successRequested || isShuttingDown ||
            collider == null || levelCtrl == null)
        {
            return;
        }

        ActorCtrl expectedActor = levelCtrl.Actor != null ? levelCtrl.Actor : actor;
        if (expectedActor == null || expectedActor.IsDied || expectedActor.IsKnocked)
            return;

        ActorCtrl colliderActor = collider.GetComponentInParent<ActorCtrl>();
        if (colliderActor != expectedActor)
            return;

        SetSuccessDoor(door, ref successAnim);
        successRequested = true;
        levelCtrl.SuccessStrightly();
    }

    private void OnGameReset(GameResetEvent evt)
    {
        // Reset 后旧关卡实例仍会短暂存活；门禁可阻止它在销毁前重新建立进度或再次通关。
        BeginTeardown();
    }

    private void OnGameReplay(GameReplayEvent evt)
    {
        // Replay 与失败重开共享同一个旧实例销毁窗口。
        BeginTeardown();
    }

    private void BeginTeardown()
    {
        resetOrReplayReceived = true;
        SuspendRuntime();
    }

    private void SetRuntimeDoorTriggersEnabled(bool enabled)
    {
        if (leftRuntimeDoorTrigger != null)
            leftRuntimeDoorTrigger.enabled = enabled;
        if (rightRuntimeDoorTrigger != null)
            rightRuntimeDoorTrigger.enabled = enabled;
    }

    private void CleanupRuntimeRouteClones()
    {
        routePoolsReady = false;
        for (int i = 0; i < runtimeRouteCloneCount; i++)
        {
            if (runtimeRouteClones[i] != null)
                Destroy(runtimeRouteClones[i]);
            runtimeRouteClones[i] = null;
        }

        runtimeRouteCloneCount = 0;
    }

    #endregion
}

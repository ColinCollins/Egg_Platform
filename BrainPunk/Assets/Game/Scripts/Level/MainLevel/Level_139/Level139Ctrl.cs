using System;
using System.Collections;
using System.Collections.Generic;
using Bear.EventSystem;
using Game.Events;
using Game.Level;
using Game.Play;
using I2.Loc;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

/// <summary>Level139 私有编排：上层满员、地下空梯、手动楼层门与成功关门。</summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class Level139Ctrl : MonoBehaviour
{
    [SerializeField] private BaseLevelCtrl level;
    [SerializeField] private Level139Elevator upper = new Level139Elevator();
    [SerializeField] private Level139Elevator parking = new Level139Elevator();
    [SerializeField] private Transform upperStartDoor;
    [SerializeField] private Transform parkingStartDoor;
    [SerializeField] private Camera levelCamera;
    [SerializeField] private Material doorMaterial;
    [SerializeField] private Sprite normalLamp;
    [SerializeField] private Sprite yellowLamp;
    [SerializeField] private Sprite greenLamp;
    [SerializeField] private Sprite overloadLamp;
    [SerializeField] private SkeletonDataAsset passengerSkeleton;
    [SerializeField] private GameObject[] faceAccessories;
    [SerializeField] private GameObject[] bodyAccessories;
    [SerializeField] private GameObject[] backAccessories;
    [SerializeField] private SuccessAnimCtrl legacySuccess;
    [FormerlySerializedAs("legacyBoxPink")]
    [SerializeField] private GameObject receptionist;
    [SerializeField, Min(0f)] private float receptionistGreetingDistance = 2f;
    private const string ReceptionistGreetingKey = "S_LevelTips_Des_level_139_1";
    private const float ReceptionistGreetingDuration = 3f;
    [SerializeField, Min(0.01f)] private float floorCameraTravelSeconds = 0.8f;
    [SerializeField, Min(0f)] private float parkingCentreTolerance = 0.15f;
    [SerializeField, Min(0f)] private float boardingCloseWaitSeconds = 2f;

    // 扩展地面的右边界（本关局部X），用于按实际视野宽度限制镜头；楼层过场仍只控制Y。
    [SerializeField] private float horizontalCameraRightEdge = 25.007555f;

    private sealed class Passenger
    {
        public Transform Root;
        public SortingGroup Sorting;
        public SkeletonAnimation Visual;
        public MeshRenderer Renderer;
        public HintBubble Bubble;
        public GameObject ClothesAnchor;
        public GameObject ExtraAnchor;
        public int ClothesIndex = -1;
    }

    private const int PassengerCount = 6;
    private const string PassengerSkin = "box_original";
    private const string OverloadTipKey = "S_LevelTips_Des_level_139_6";
    private static readonly string[] PassengerSpeechKeys =
    {
        "S_LevelTips_Des_level_139_2", "S_LevelTips_Des_level_139_3",
        "S_LevelTips_Des_level_139_4", "S_LevelTips_Des_level_139_5"
    };
    private static readonly Vector3 PassengerVisualOffset = new Vector3(0f, -0.48f, 0f);
    private readonly Passenger[] passengers = new Passenger[PassengerCount];
    private readonly HashSet<int> occupiedClothes = new HashSet<int>();
    private readonly HashSet<int> occupiedExtras = new HashSet<int>();
    private HintBubble bubbleTemplate;
    private Level139ActorCtrl player;
    private Level139Hud hud;
    private Level139PlayerZone upperPortal, parkingPortal;
    private readonly Level139PortalDoor upperPortalDoor = new Level139PortalDoor();
    private readonly Level139PortalDoor parkingPortalDoor = new Level139PortalDoor();
    private GameObject portalRoot;
    private GameObject playerShadow;
    private bool shadowHiddenForElevator;
    private bool shadowActiveBeforeEntry;
    private SortingGroup playerSorting;
    private Transform playerParentBeforeBoarding;
    private int playerLayerBeforeBoarding, playerOrderBeforeBoarding;
    private bool playerDepthChanged;
    private float successCloseWaitRemaining;
    private bool successCloseStarted;
    private CameraFollowArea existingFollow;
    private bool originalFollowEnabled, originalLegacyActive, originalReceptionistActive;
    private bool receptionistGreeted;
    private HintBubble receptionistBubble;
    private bool initialized, winning, completed;
    private bool eventPaused;
    private bool failed, controlsBeforeLock;
    private EventSubscriber subscriber;
    private int floor;
    private Vector3 initialCameraPosition;
    private float horizontalCameraVelocity;
    private Level139PortalTransit portalTransit;
    private Coroutine setup;
    private GameObject passengerRoot;

    private Level139Elevator CurrentLift => floor == 0 ? upper : parking;
    private bool IsPlaying => !eventPaused && PlayCtrl.Instance != null
        && PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
    private bool CanInteract => initialized && isActiveAndEnabled && IsPlaying && !winning
        && !completed && !failed && player != null && !player.IsDied && level.ActorCtrlable
        && (portalTransit == null || !portalTransit.IsBusy);

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;
        // 先完成两组门的可见闭合姿态，之后才允许协程等待 Actor/HUD。
        // Prefab 中的打开姿态只在此缓存一次，不作为第一帧运行画面。
        try
        {
            upper.PrepareClosedVisuals(doorMaterial, normalLamp, yellowLamp, greenLamp, overloadLamp);
            parking.PrepareClosedVisuals(doorMaterial, normalLamp, yellowLamp, greenLamp, overloadLamp);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            upper.Restore();
            parking.Restore();
            enabled = false;
            return;
        }
        EventsUtils.ResetEvents(ref subscriber);
        eventPaused = false;
        subscriber.Subscribe<GamePauseEvent>(_ =>
        {
            eventPaused = true;
            portalTransit?.SetPaused(true);
        });
        subscriber.Subscribe<GameResumeEvent>(_ =>
        {
            eventPaused = false;
            portalTransit?.SetPaused(false);
        });
        subscriber.Subscribe<OnTriggerTrapEvent>(_ => failed = true);
        subscriber.Subscribe<OnTriggerFailAreaEvent>(_ => failed = true);
        subscriber.Subscribe<GameResetEvent>(_ => failed = true);
        subscriber.Subscribe<GameReplayEvent>(_ => failed = true);
        setup = StartCoroutine(Setup());
    }

    private IEnumerator Setup()
    {
        // 等 Actor.Start 和本关 HUD 创建；不取得上一关的面板。
        yield return null;
        player = level != null ? level.Actor as Level139ActorCtrl : null;
        playerSorting = player != null ? player.GetComponent<SortingGroup>() : null;
        if (player == null || playerSorting == null || !upper.HasReferences || !parking.HasReferences
            || !upperStartDoor || !parkingStartDoor || !levelCamera || !doorMaterial
            || !normalLamp || !yellowLamp || !greenLamp || !overloadLamp || !passengerSkeleton || !receptionist)
        {
            Debug.LogError("[Level139] 缺少本关专属 Actor、电梯、前台、入口、相机或资源接线，停止初始化。", this);
            enabled = false;
            yield break;
        }
        Exception initializationError = null;
        try
        {
            InitializeLevel();
        }
        catch (Exception exception)
        {
            initializationError = exception;
        }
        if (initializationError != null)
        {
            Debug.LogException(initializationError, this);
            enabled = false;
            yield break;
        }
        while (isActiveAndEnabled)
        {
            var panel = PlayCtrl.Instance != null ? PlayCtrl.Instance.CurrentGamePlayPanel : null;
            Level139Hud candidate = panel != null ? panel.GetComponent<Level139Hud>() : null;
            if (candidate != null)
            {
                hud = candidate;
                hud.Bind(RequestOpen);
                break;
            }
            yield return null;
        }
        setup = null;
    }

    private void InitializeLevel()
    {
        playerShadow = player.transform.Find("Shadow")?.gameObject;
        shadowHiddenForElevator = false;
        initialCameraPosition = levelCamera.transform.position;
        existingFollow = levelCamera.GetComponent<CameraFollowArea>();
        originalFollowEnabled = existingFollow != null && existingFollow.enabled;
        if (existingFollow != null)
            existingFollow.enabled = false;
        originalLegacyActive = legacySuccess != null && legacySuccess.gameObject.activeSelf;
        originalReceptionistActive = receptionist.activeSelf;
        // 只隐藏旧通关占位门；场景 BoxPink 是前台管理员，不属于电梯乘客。
        if (legacySuccess != null)
            legacySuccess.gameObject.SetActive(false);
        level.SetSuccessAnim(null);
        initialized = true; // 后续资源创建异常也由同一 OnDisable 恢复路径收口。
        receptionist.SetActive(true);
        receptionist.GetComponentInChildren<ActorSpineCtrl>(true)?.PlayAnimation("idle", true, false);
        receptionistGreeted = false;
        floor = 0;
        winning = completed = false;
        successCloseStarted = playerDepthChanged = false;
        successCloseWaitRemaining = 0f;
        failed = false;
        bubbleTemplate = Resources.Load<HintBubble>("HintBubble");
        if (bubbleTemplate == null)
            throw new InvalidOperationException("Level139 缺少共享 HintBubble。");
        ValidateAppearancePalette();
        horizontalCameraVelocity = 0f;

        upper.Initialize(player, RefreshPassengerAppearances);
        parking.Initialize(player);
        portalRoot = new GameObject("Level139_FloorPortals");
        portalRoot.transform.SetParent(transform, false);
        upperPortal = CreatePortal("UpperReturnZone", upperStartDoor);
        parkingPortal = CreatePortal("ParkingReturnZone", parkingStartDoor);
        portalTransit = portalRoot.AddComponent<Level139PortalTransit>();
        portalTransit.Initialize(level, player,
            () => this != null && isActiveAndEnabled && initialized && !failed && player != null && !player.IsDied);
        upperPortalDoor.Initialize(upperStartDoor);
        parkingPortalDoor.Initialize(parkingStartDoor);
        BindPortalClick(upperPortal, upperStartDoor, upperPortalDoor, 0);
        BindPortalClick(parkingPortal, parkingStartDoor, parkingPortalDoor, 1);
        passengerRoot = new GameObject("Level139_PassengerOwnership");
        passengerRoot.transform.SetParent(transform, false);
        for (int i = 0; i < passengers.Length; i++)
            SpawnPassenger(i);
    }

    private Level139PlayerZone CreatePortal(string name, Transform door)
    {
        var go = new GameObject(name, typeof(BoxCollider2D), typeof(Level139PlayerZone));
        go.transform.SetParent(portalRoot.transform, false);
        go.transform.position = door.position + Vector3.up;
        var zone = go.GetComponent<Level139PlayerZone>();
        zone.Initialize(player, new Vector2(1f, 2f));
        return zone;
    }

    private void SpawnPassenger(int index)
    {
        // 只复用 Spine 和饰品，不复制 ActorCtrl、Player 标签、实体碰撞或存档绑定。
        var data = passengerSkeleton.GetSkeletonData(false);
        if (data == null || data.FindSkin(PassengerSkin) == null || data.FindAnimation("idle") == null)
            throw new InvalidOperationException("Level139 乘客缺少 box_original 或 idle。");
        // 先登记所有权再初始化 Spine，保证导入/皮肤初始化异常时也可回收临时对象。
        var go = new GameObject("Level139_Passenger_" + index);
        go.SetActive(false);
        go.transform.SetParent(passengerRoot.transform, false);
        var passenger = new Passenger
        {
            Root = go.transform
        };
        passengers[index] = passenger;
        passenger.Root.SetParent(upper.root, false);
        passenger.Root.position = upper.GetPassengerStandPosition(index, passengers.Length)
            - upper.root.TransformVector(PassengerVisualOffset);
        passenger.Sorting = go.AddComponent<SortingGroup>();
        passenger.Sorting.sortingLayerID = upper.leftDoor.sortingLayerID;
        passenger.Sorting.sortingOrder = -12 + index;

        var visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(passenger.Root, false);
        visualObject.transform.localPosition = PassengerVisualOffset;
        SkeletonAnimation visual = visualObject.AddComponent<SkeletonAnimation>();
        passenger.Visual = visual;
        visual.skeletonDataAsset = passengerSkeleton;
        visual.initialSkinName = PassengerSkin;
        visual.Initialize(false);
        visual.transform.localScale = Vector3.one * 0.4f;
        visual.Skeleton.SetSkin(PassengerSkin);
        visual.Skeleton.SetSlotsToSetupPose();
        visual.Skeleton.UpdateWorldTransform(Spine.Skeleton.Physics.Update);
        visual.AnimationState.SetAnimation(0, "idle", true);
        passenger.Renderer = visual.GetComponent<MeshRenderer>();
        passenger.Renderer.sortingLayerID = upper.leftDoor.sortingLayerID;
        passenger.Renderer.sortingOrder = 0;

        go.SetActive(true);
    }

    private void RefreshPassengerAppearances()
    {
        // 门真正开始打开时刷新；打开后的续期点击不换装，不改变乘客根、站位或气泡跟随点。
        ClosePassengerBubbles();
        occupiedClothes.Clear();
        occupiedExtras.Clear();
        foreach (Passenger passenger in passengers)
        {
            ClearPassengerAccessory(passenger.ClothesAnchor);
            ClearPassengerAccessory(passenger.ExtraAnchor);
            passenger.ClothesAnchor = passenger.ExtraAnchor = null;
            passenger.ClothesIndex = ChooseUnused(bodyAccessories.Length, occupiedClothes, false,
                passenger.ClothesIndex);
            passenger.ClothesAnchor = AttachAccessory(passenger,
                bodyAccessories[passenger.ClothesIndex], "point02");
            int extra = ChooseUnused(faceAccessories.Length + backAccessories.Length, occupiedExtras, true);
            if (extra >= 0 && extra < faceAccessories.Length)
                passenger.ExtraAnchor = AttachAccessory(passenger, faceAccessories[extra], "point01");
            else if (extra >= faceAccessories.Length)
                passenger.ExtraAnchor = AttachAccessory(passenger,
                    backAccessories[extra - faceAccessories.Length], "point");
            // 只镜像 Visual 和其饰品；气泡和站位不随朝向翻转。
            passenger.Visual.transform.localScale = new Vector3(
                UnityEngine.Random.Range(0, 2) == 0 ? -0.4f : 0.4f, 0.4f, 0.4f);
        }
    }

    private void ClearPassengerAccessory(GameObject anchor)
    {
        if (anchor == null)
            return;
        anchor.SetActive(false);
        Destroy(anchor);
    }

    private void ValidateAppearancePalette()
    {
        if (bodyAccessories == null || bodyAccessories.Length <= PassengerCount
            || faceAccessories == null || backAccessories == null)
            throw new InvalidOperationException("Level139 随机装扮池不完整。");
        var distinct = new HashSet<GameObject>();
        foreach (GameObject[] pool in new[] { bodyAccessories, faceAccessories, backAccessories })
            foreach (GameObject part in pool)
                if (part == null || !distinct.Add(part))
                    throw new InvalidOperationException("Level139 装扮池有空引用或重复资源。");
    }

    private static int ChooseUnused(int count, HashSet<int> used, bool allowNone, int excluded = -1)
    {
        int chosen = -1;
        int available = allowNone ? 1 : 0;
        for (int i = 0; i < count; i++)
            if (i != excluded && !used.Contains(i) && UnityEngine.Random.Range(0, ++available) == 0)
                chosen = i;
        if (chosen >= 0)
            used.Add(chosen);
        return chosen;
    }

    private GameObject AttachAccessory(Passenger passenger, GameObject prefab, string boneName)
    {
        if (passenger.Visual.Skeleton.FindBone(boneName) == null)
            throw new InvalidOperationException("Level139 饰品挂点缺失：" + boneName);
        var anchor = new GameObject(boneName);
        // 与 138/共享 Actor 相同，挂点是 Spine 的直接子节点，饰品继承 0.4 缩放。
        anchor.transform.SetParent(passenger.Visual.transform, false);
        var sorting = anchor.AddComponent<SortingGroup>();
        sorting.sortingLayerID = passenger.Renderer.sortingLayerID;
        sorting.sortingOrder = boneName == "point" ? -1 : (boneName == "point01" ? 10 : 2);
        var follower = anchor.AddComponent<BoneFollower>();
        follower.skeletonRenderer = passenger.Visual;
        follower.boneName = boneName;
        follower.followBoneRotation = true;
        follower.Initialize();
        follower.LateUpdate();
        if (!follower.valid)
            throw new InvalidOperationException("Level139 无效饰品挂点：" + boneName);
        GameObject part = Instantiate(prefab, anchor.transform, false);
        part.transform.localPosition = Vector3.zero;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = Vector3.one;
        foreach (SpriteRenderer sprite in part.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sprite.sortingLayerID = passenger.Renderer.sortingLayerID;
            if (boneName == "point") sprite.sortingOrder = -1;
        }
        part.SetActive(true);
        return anchor;
    }

    private void ShowRandomPassengerBubble()
    {
        if (!CanInteract || floor != 0 || !upper.IsOpen)
            return;
        Passenger passenger = passengers[UnityEngine.Random.Range(0, passengers.Length)];
        if (passenger == null || passenger.Root == null)
            return;
        ClosePassengerBubbles();
        passenger.Bubble = Instantiate(bubbleTemplate, transform);
        passenger.Bubble.Show(PassengerSpeechKeys[UnityEngine.Random.Range(0, PassengerSpeechKeys.Length)],
            passenger.Root, 3f);
    }

    private void ClosePassengerBubbles()
    {
        foreach (Passenger passenger in passengers)
        {
            if (passenger?.Bubble != null)
                passenger.Bubble.CloseStraightly();
            if (passenger != null)
                passenger.Bubble = null;
        }
    }

    private void BindPortalClick(Level139PlayerZone zone, Transform door, Level139PortalDoor state, int targetFloor)
    {
        // 命中可见门体，不要求玩家站进触发区；吸入仍由独立的门口 Trigger 判定。
        Renderer renderer = door.GetComponentInChildren<MeshRenderer>(true);
        if (renderer == null)
            throw new InvalidOperationException("Level139 楼层门缺少可点击 Renderer。");
        zone.gameObject.AddComponent<Level139ClickTarget>().Bind(levelCamera,
            () => CanInteract && floor == targetFloor && renderer != null && renderer.gameObject.activeInHierarchy,
            () => renderer.bounds, state.Toggle, 200);
    }

    private void Update()
    {
        if (!initialized || player == null)
            return;
        UpdatePlayerShadow();
        bool playing = IsPlaying && !player.IsDied && !failed && !completed;
        SetPassengerAnimationEnabled(playing);
        portalTransit?.SetPaused(!playing);
        upperPortalDoor.SetPaused(!playing);
        parkingPortalDoor.SetPaused(!playing);
        if (!playing)
        {
            if (failed || player.IsDied)
            {
                portalTransit?.Cancel();
                CloseReceptionistGreeting();
                ClosePassengerBubbles();
            }
            hud?.SetEntry(false, false);
            return;
        }
        if (portalTransit != null && portalTransit.IsBusy)
        {
            hud?.SetEntry(false, false);
            return;
        }
        if (winning)
        {
            if (!successCloseStarted)
            {
                // 锁定后刚体停止模拟会产生 Trigger 退出；此阶段使用独立计时，
                // 不让普通“无人时关门”逻辑抢先关闭，也不因退出回调取消成功就位。
                successCloseWaitRemaining = Mathf.Max(0f, successCloseWaitRemaining - Time.deltaTime);
                if (successCloseWaitRemaining > 0f)
                    return;
                successCloseStarted = true;
                parking.BeginSuccessClose();
                return;
            }
            parking.Tick(Time.deltaTime);
            if (parking.State == Level139Elevator.DoorState.Closed)
            {
                completed = true;
                // 不调用旧门演出；仍经过 BaseLevelCtrl 标准成功/第三星收口。
                level.SuccessStrightly();
            }
            return;
        }
        // 初始出门演出期间不允许按 HUD、楼层转移或成功锁定。
        if (!level.ActorCtrlable)
        {
            hud?.SetEntry(false, false);
            return;
        }

        TickPortals();
        if (portalTransit != null && portalTransit.IsBusy)
        {
            hud?.SetEntry(false, false);
            return;
        }
        TickReceptionistGreeting();
        bool upperWasOpen = upper.IsOpen;
        upper.Tick(Time.deltaTime);
        parking.Tick(Time.deltaTime);
        // 只在完全打开的边沿自动发言；保持打开或按钮续期不重复弹出。
        if (!upperWasOpen && upper.IsOpen)
            ShowRandomPassengerBubble();
        if (upper.SetOverload(floor == 0 && upper.IsOpen && upper.HasPlayer))
            SystemTips.Show(LocalizationManager.GetTranslation(OverloadTipKey));
        if (!upper.IsOpen)
            ClosePassengerBubbles();
        if (floor == 1 && parking.IsOpen && IsPlayerAtParkingCentre())
        {
            winning = true;
            hud?.SetEntry(false, false);
            controlsBeforeLock = level.ActorCtrlable;
            level.SetCtrlable(false);
            player.LockForElevator();
            SetPlayerBoardingDepth();
            successCloseStarted = false;
            successCloseWaitRemaining = Mathf.Max(0f, boardingCloseWaitSeconds);
            return;
        }
        var lift = CurrentLift;
        bool nearButton = lift.ButtonZone.HasPlayer;
        hud?.SetEntry(nearButton, lift.CanRequestOpen);
    }

    private void LateUpdate()
    {
        if (!initialized || player == null || !IsPlaying || player.IsDied || failed || completed
            || winning || !level.ActorCtrlable || (portalTransit != null && portalTransit.IsBusy))
            return;
        // 只跟随扩展场景的 X；不与楼层过场争用 Y，不因跳跃改变高度。
        float halfWidth = levelCamera.orthographicSize * levelCamera.aspect;
        // 镜头终点取地面右边界，主角走过电梯后仍保持可见，并避免露出背景之外。
        float rightEdge = transform.TransformPoint(new Vector3(horizontalCameraRightEdge, 0f, 0f)).x;
        float maximumX = Mathf.Max(initialCameraPosition.x, rightEdge - halfWidth);
        float targetX = Mathf.Clamp(player.transform.position.x - halfWidth * 0.35f,
            initialCameraPosition.x, maximumX);
        Vector3 position = levelCamera.transform.position;
        position.x = Mathf.SmoothDamp(position.x, targetX, ref horizontalCameraVelocity, 0.2f);
        levelCamera.transform.position = position;
    }

    private bool IsPlayerAtParkingCentre()
    {
        return playerSorting != null && player.IsGround && parking.HasPlayer
            && parking.CabinZone.TryGetPlayerBodyBounds(out Bounds body)
            && Mathf.Abs(body.center.x - parking.CabinBounds.center.x)
                <= Mathf.Max(0f, parkingCentreTolerance);
    }

    private void SetPlayerBoardingDepth()
    {
        // 仅中心成功就位时进入舱内层；普通走入/走出仍保持原前景层级。
        playerParentBeforeBoarding = player.transform.parent;
        playerLayerBeforeBoarding = playerSorting.sortingLayerID;
        playerOrderBeforeBoarding = playerSorting.sortingOrder;
        playerDepthChanged = true;
        player.transform.SetParent(parking.root, true);
        playerSorting.sortingLayerID = parking.leftDoor.sortingLayerID;
        playerSorting.sortingOrder = -5; // 背景 -20 之上、门扇 0 之后。
    }

    private void RestorePlayerBoardingDepth()
    {
        if (!playerDepthChanged)
            return;
        playerDepthChanged = false;
        if (player != null && playerParentBeforeBoarding != null)
            player.transform.SetParent(playerParentBeforeBoarding, true);
        if (playerSorting != null)
        {
            playerSorting.sortingLayerID = playerLayerBeforeBoarding;
            playerSorting.sortingOrder = playerOrderBeforeBoarding;
        }
        playerParentBeforeBoarding = null;
    }

    private void RequestOpen()
    {
        if (!initialized || winning || completed || failed || !IsPlaying || player.IsDied || !level.ActorCtrlable
            || (portalTransit != null && portalTransit.IsBusy))
            return;
        Level139Elevator lift = CurrentLift;
        // 点击时重新检查触发区和状态，防止离开 HUD、旧事件或重复点击越权开门。
        if (lift.ButtonZone.HasPlayer)
            lift.RequestOpen();
    }

    private void TickReceptionistGreeting()
    {
        if (floor != 0 || receptionistGreeted || receptionist == null
            || !receptionist.activeInHierarchy)
            return;
        Vector2 offset = player.transform.position - receptionist.transform.position;
        if (offset.sqrMagnitude > receptionistGreetingDistance * receptionistGreetingDistance)
            return;

        // 每局一次；UI、自动关闭、文字动画和语言刷新全部交给共享 HintBubble。
        receptionistGreeted = true;
        HintBubble prefab = Resources.Load<HintBubble>("HintBubble");
        if (prefab == null)
        {
            Debug.LogError("[Level139] 找不到共享 HintBubble，无法显示前台欢迎语。", this);
            return;
        }
        receptionistBubble = Instantiate(prefab, transform);
        receptionistBubble.Show(ReceptionistGreetingKey, receptionist.transform, ReceptionistGreetingDuration);
    }

    private void CloseReceptionistGreeting()
    {
        if (receptionistBubble != null)
            receptionistBubble.CloseStraightly();
        receptionistBubble = null;
    }

    private void TickPortals()
    {
        Level139PlayerZone portal = floor == 0 ? upperPortal : parkingPortal;
        Level139PortalDoor doorState = floor == 0 ? upperPortalDoor : parkingPortalDoor;
        if (!doorState.IsOpen || !portal.HasPlayer)
            return;
        int sourceFloor = floor;
        Transform source = floor == 0 ? upperStartDoor : parkingStartDoor;
        Transform target = floor == 0 ? parkingStartDoor : upperStartDoor;
        int targetFloor = 1 - sourceFloor;
        float targetCameraY = initialCameraPosition.y
            + (targetFloor == 0 ? 0f : parkingStartDoor.position.y - upperStartDoor.position.y);
        if (portalTransit == null || !portalTransit.TryPlay(source, target,
            levelCamera, targetCameraY, floorCameraTravelSeconds,
            () => ApplyFloorState(targetFloor), () => ApplyFloorState(sourceFloor)))
            return;
        doorState.Consume();
        CloseReceptionistGreeting();
        ClosePassengerBubbles();
        RestorePlayerShadow();
    }

    private void ApplyFloorState(int targetFloor)
    {
        floor = targetFloor;
        Transform destination = floor == 0 ? upperStartDoor : parkingStartDoor;
        // 相机抵达后才切换楼层状态；此刻玩家仍隐藏。相机位置由过场单独管理，
        // 此处不再直接跳相机。之后的渐显和自动走出交给本关黑门洞出场流程。
        Vector3 target = destination.position;
        target.z = player.transform.position.z;
        player.SetMoveInput(0f);
        player.RB.linearVelocity = Vector2.zero;
        player.RB.position = target;
        player.transform.position = target;
        upper.ClearContacts();
        parking.ClearContacts();
        upperPortal.ClearContacts();
        parkingPortal.ClearContacts();
        upperPortalDoor.ResetClosed();
        parkingPortalDoor.ResetClosed();
        upper.SetOverload(false);
        Physics2D.SyncTransforms();
    }

    private void DestroyPassenger(Passenger passenger)
    {
        if (passenger.Root != null)
        {
            passenger.Root.gameObject.SetActive(false);
            Destroy(passenger.Root.gameObject);
        }
        passenger.Root = null;
        passenger.Visual = null;
        passenger.Renderer = null;
        passenger.Sorting = null;
    }

    private void SetPassengerAnimationEnabled(bool enabledAnimation)
    {
        foreach (Passenger passenger in passengers)
            if (passenger != null && passenger.Visual != null)
                passenger.Visual.timeScale = enabledAnimation ? 1f : 0f;
    }

    private void UpdatePlayerShadow()
    {
        if (playerShadow == null)
            return;
        // 成功锁定会停止刚体模拟并导致 Trigger 退出，不能因此在关门时恢复阴影。
        if (winning || CurrentLift.HasPlayer)
        {
            if (!shadowHiddenForElevator)
            {
                // 出生门演出也会开关 Shadow，必须在实际入舱时保存，不采集出生淡入的临时状态。
                shadowActiveBeforeEntry = playerShadow.activeSelf;
                shadowHiddenForElevator = true;
            }
            // ShadowController 会生成子阴影，仅关根 Renderer 无法隐藏这些动态对象。
            if (playerShadow.activeSelf)
                playerShadow.SetActive(false);
        }
        else
            RestorePlayerShadow();
    }

    private void RestorePlayerShadow()
    {
        if (!shadowHiddenForElevator)
            return;
        shadowHiddenForElevator = false;
        if (playerShadow != null)
            playerShadow.SetActive(shadowActiveBeforeEntry);
    }

    private void OnDisable()
    {
        if (setup != null)
            StopCoroutine(setup);
        setup = null;
        EventsUtils.ResetEvents(ref subscriber);
        if (portalTransit != null)
            portalTransit.Cancel();
        CloseReceptionistGreeting();
        ClosePassengerBubbles();
        upperPortalDoor.ResetClosed();
        parkingPortalDoor.ResetClosed();
        if (hud != null)
            hud.Bind(null);
        hud = null;
        if (!initialized)
        {
            // 包括同步闭合成功但 setup 尚未完成、就被停用的路径。
            upper.Restore();
            parking.Restore();
            return;
        }
        initialized = false;
        RestorePlayerBoardingDepth();
        RestorePlayerShadow();
        if (player != null)
            player.ReleaseElevatorLock();
        if (level != null)
        {
            if (winning)
                level.SetCtrlable(controlsBeforeLock);
            level.SetSuccessAnim(legacySuccess);
        }
        foreach (Passenger passenger in passengers)
            if (passenger != null)
                DestroyPassenger(passenger);
        Array.Clear(passengers, 0, passengers.Length);
        if (passengerRoot != null)
        {
            passengerRoot.SetActive(false);
            Destroy(passengerRoot);
        }
        upper.Restore();
        parking.Restore();
        if (portalRoot != null)
        {
            portalRoot.SetActive(false);
            Destroy(portalRoot);
        }
        if (levelCamera != null)
            levelCamera.transform.position = initialCameraPosition;
        if (existingFollow != null)
            existingFollow.enabled = originalFollowEnabled;
        if (legacySuccess != null)
            legacySuccess.gameObject.SetActive(originalLegacyActive);
        if (receptionist != null)
            receptionist.SetActive(originalReceptionistActive);
    }
}

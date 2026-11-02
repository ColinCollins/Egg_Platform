using System.Collections;
using System.Collections.Generic;
using Game.ItemEvent;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level113 总控：管理彩色箱钥匙链、台面钥匙拖入跳跃按钮、point_hit 挂载和门锁打击通关。
/// </summary>
public sealed class Level113Ctrl : MonoBehaviour
{
    private const string ToolBoneName = "point_hit";
    private const string ChestOpenAnimationName = "box-open";
    private const float OriginalConsumedKeyHideDelay = 0.1f;

    private static readonly string[] KeyNames =
    {
        "Sp_key_1_05", // 红
        "Sp_key_1_02", // 蓝
        "Sp_key_1_04", // 橙
        "Sp_key_1_03", // 绿
        "Sp_key_1_06"  // 粉
    };

    private static readonly string[] ChestNames =
    {
        "Chest_red",
        "Chest_blue",
        "Chest_orange",
        "Chest_green"
    };

    #region Inspector

    [Header("核心引用")]
    [Tooltip("公共关卡控制器；只复用角色输入和标准进门成功链。")]
    [SerializeField] private BaseLevelCtrl levelCtrl;

    [Tooltip("Level113 专用角色控制器。")]
    [SerializeField] private Level113ActorCtrl actor;

    [Header("箱钥匙表现")]
    [Tooltip("保持原关卡：box-open 开始后等待 0.5 秒，再同时播放闪光并显示下一把钥匙。")]
    [SerializeField, Min(0f)] private float chestKeyAppearDelay = 0.5f;

    [Header("按钮道具")]
    [Tooltip("角色使用钥匙打击门锁的最大世界距离。")]
    [SerializeField, Min(0.1f)] private float unlockUseDistance = 2.6f;

    [Tooltip("门锁或 hit 动画回调异常时的兜底等待时长。")]
    [SerializeField, Min(0.2f)] private float unlockTimeout = 3.5f;

    [Tooltip("hit 动画开始后，Knock_Explosion 对齐到门锁并播放的延迟。")]
    [SerializeField, Min(0f)] private float doorHitEffectDelay = 0.18f;

    [Tooltip("纯击打粒子相对 DoorLock 的世界坐标偏移。")]
    [SerializeField] private Vector2 doorHitEffectOffset = new Vector2(-0.35f, 0.25f);

    [Header("公共按钮查找")]
    [Tooltip("等待 GamePlayPanel 创建时的最大查找次数。")]
    [SerializeField, Min(1)] private int jumpButtonResolveAttempts = 30;

    [Tooltip("两次查找 jump_btn 之间的实时时间间隔。")]
    [SerializeField, Min(0.01f)] private float jumpButtonResolveInterval = 0.1f;

    #endregion

    #region Runtime State

    private enum KeyState
    {
        Hidden,
        ReadyOnPlatform,
        FollowingActor,
        Consumed,
        Installing,
        Equipped
    }

    private sealed class KeyRuntime
    {
        public Transform Transform;
        public Transform OriginalParent;
        public SpriteRenderer Renderer;
        public Collider2D Collider;
        public MoveFloatHandle FloatHandle;
        public FollowTarget FollowTarget;
        public ItemJumpOutExecutor JumpOut;
        public TriggerExecutor PickupExecutor;
        public Level113KeyDrag Drag;
        public Vector3 OriginalLocalScale;
        public Color OriginalColor;
        public int OriginalSortingOrder;
        public KeyState State;
    }

    private sealed class ChestRuntime
    {
        public Transform Transform;
        public Collider2D Collider;
        public ActorSpineCtrl Spine;
        public SwitchSortLayerListener KeySort;
        public ItemScaleListener KeyScale;
        public ItemFlyPathListener KeyFly;
        public bool Opened;
    }

    private readonly List<KeyRuntime> keys = new List<KeyRuntime>(KeyNames.Length);
    private readonly List<ChestRuntime> chests = new List<ChestRuntime>(ChestNames.Length);
    private readonly List<GameObject> spawnedChestEffects = new List<GameObject>(ChestNames.Length);
    private readonly Dictionary<Transform, KeyRuntime> keyByTransform = new Dictionary<Transform, KeyRuntime>();
    private readonly Dictionary<Transform, ChestRuntime> chestByTransform = new Dictionary<Transform, ChestRuntime>();

    private Vector3 keyPlatformPosition;
    private KeyRuntime carriedKey;
    private KeyRuntime equippedKey;
    private ChestRuntime expectedChest;
    private Transform toolAnchor;
    private GameObject shinyTemplate;
    private Transform doorLockRoot;
    private ActorSpineCtrl doorLockSpine;
    private ParticleSystem doorHitEffect;
    private Collider2D exitDoorCollider;
    private bool operationLocked;
    private bool toolButtonReady;
    private bool unlockInProgress;
    private bool hitAnimationCompleted;
    private bool lockAnimationCompleted;
    private bool isCleaningUp;

    private RectTransform jumpButtonRect;
    private Canvas jumpButtonCanvas;
    private CustomButton jumpButton;
    private Image jumpButtonIcon;
    private ClickScaleAnim jumpButtonScaleFeedback;
    private bool toolButtonPointerDown;
    private int toolButtonTouchId = -1;
    private bool jumpButtonInitialEnabled;
    private bool jumpButtonInitialInteractable;
    private int jumpButtonInitialSiblingIndex;
    private bool hasJumpButtonState;
    private Sprite jumpIconInitialSprite;
    private Color jumpIconInitialColor;
    private bool jumpIconInitialPreserveAspect;
    private Quaternion jumpIconInitialRotation;
    private Vector2 jumpIconInitialPosition;
    private Vector2 jumpIconInitialSize;
    private int jumpIconInitialSiblingIndex;
    private bool hasJumpIconState;

    /// <summary>
    /// 场景拖拽使用的世界相机；只读，不改变公共相机配置。
    /// </summary>
    public Camera InputCamera => Camera.main;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveCoreReferences();
        if (!BuildKeyAndChestRuntime())
        {
            enabled = false;
            return;
        }

        PrepareDoor();
        PrepareInitialKeyState();
    }

    private IEnumerator Start()
    {
        for (int attempt = 0; attempt < Mathf.Max(1, jumpButtonResolveAttempts); attempt++)
        {
            if (TryResolveJumpButton())
                yield break;

            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, jumpButtonResolveInterval));
        }

        Debug.LogWarning("[Level113Ctrl] 未找到 Root/jump_btn，钥匙暂时无法安装到按钮。", this);
    }

    private void Update()
    {
        if (toolButtonPointerDown)
            HandleToolButtonRelease();

        bool isPlaying = IsGamePlaying();
        if (!isPlaying || isCleaningUp)
            return;

        if (toolButtonReady && !operationLocked && !unlockInProgress)
            HandleToolButtonInput();
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
    }

    private void OnDestroy()
    {
        CleanupRuntimeState();
    }

    #endregion

    #region Key And Chest Chain

    /// <summary>
    /// 判断指定钥匙当前是否允许接管指针拖拽。
    /// 调用边界：仅供本关 Level113KeyDrag 查询；只允许台面、未跟随、未安装的钥匙。
    /// </summary>
    public bool CanOperateKey(Level113KeyDrag source)
    {
        if (source == null || operationLocked || equippedKey != null || !IsGamePlaying())
            return false;

        for (int index = 0; index < keys.Count; index++)
        {
            KeyRuntime key = keys[index];
            if (key.Drag == source)
                return key.State == KeyState.ReadyOnPlatform;
        }

        return false;
    }

    /// <summary>
    /// 接收本关钥匙触发器回调；验证确为当前 Actor 后，把台面钥匙切入原 FollowTarget 跟随链。
    /// </summary>
    public void OnKeyContact(Transform keyTransform, Collider2D other)
    {
        if (!IsCurrentActor(other) || operationLocked || equippedKey != null ||
            keyTransform == null || !keyByTransform.TryGetValue(keyTransform, out KeyRuntime key) ||
            key.State != KeyState.ReadyOnPlatform || key.Drag.IsDragging)
        {
            return;
        }

        SetKeyFollowingActor(key);
    }

    /// <summary>
    /// 接收本关箱子触发器回调；只有当前跟随钥匙与箱子颜色链匹配时才开箱。
    /// </summary>
    public void OnChestContact(Transform chestTransform, Collider2D other)
    {
        if (!IsCurrentActor(other) || operationLocked || carriedKey == null ||
            chestTransform == null || !chestByTransform.TryGetValue(chestTransform, out ChestRuntime chest) ||
            chest != expectedChest || chest.Opened)
        {
            return;
        }

        OpenChest(chest);
    }

    private void SetKeyFollowingActor(KeyRuntime key)
    {
        key.State = KeyState.FollowingActor;
        key.Drag.SetReady(false);
        if (key.Collider != null)
            key.Collider.enabled = false;
        if (key.FollowTarget != null)
        {
            key.FollowTarget.SetTarget(actor.transform);
            key.FollowTarget.enabled = false;
        }

        carriedKey = key;
        int keyIndex = keys.IndexOf(key);
        expectedChest = keyIndex >= 0 && keyIndex < chests.Count ? chests[keyIndex] : null;
        if (key.PickupExecutor != null)
            key.PickupExecutor.Execute();
        else
            Debug.LogError($"[Level113Ctrl] {key.Transform.name} 缺少原 TriggerExecutor，无法复用原钥匙拾取表现。", this);
    }

    private void OpenChest(ChestRuntime chest)
    {
        operationLocked = true;
        chest.Opened = true;
        if (chest.Collider != null)
            chest.Collider.enabled = false;

        KeyRuntime usedKey = carriedKey;
        carriedKey = null;
        expectedChest = null;
        if (usedKey.FollowTarget != null)
            usedKey.FollowTarget.enabled = false;
        if (usedKey.PickupExecutor != null)
            usedKey.PickupExecutor.enabled = false;

        int chestIndex = chests.IndexOf(chest);
        KeyRuntime nextKey = chestIndex + 1 < keys.Count ? keys[chestIndex + 1] : null;
        StartCoroutine(OpenChestWithOriginalPresentation(chest, usedKey, nextKey));
    }

    private IEnumerator OpenChestWithOriginalPresentation(
        ChestRuntime chest,
        KeyRuntime usedKey,
        KeyRuntime nextKey)
    {
        chest.KeySort?.ResetState();
        chest.KeySort?.Execute();
        yield return null;

        chest.KeyScale?.ResetState();
        chest.KeyScale?.Execute();
        yield return null;

        // 原 BaseItemExecutor 按帧推进排序、缩放和飞入箱子三个节点。
        chest.KeyFly?.ResetState();
        chest.KeyFly?.Execute();
        while (!isCleaningUp && chest.KeyFly != null && !chest.KeyFly.IsDone)
            yield return null;

        if (isCleaningUp)
            yield break;

        AudioManager.PlaySound("openChest");
        yield return null;

        chest.Spine?.PlayAnimation(ChestOpenAnimationName, false, false);
        yield return new WaitForSeconds(chestKeyAppearDelay);

        if (isCleaningUp)
            yield break;

        StartCoroutine(HideConsumedKeyAfterOriginalDelay(usedKey));
        yield return null;

        SpawnChestShiny(chest);
        yield return null;

        if (nextKey == null)
        {
            operationLocked = false;
            yield break;
        }

        nextKey.Transform.SetParent(nextKey.OriginalParent, true);
        nextKey.Transform.gameObject.SetActive(true);
        nextKey.Transform.localScale = nextKey.OriginalLocalScale;
        nextKey.Renderer.enabled = true;
        nextKey.Renderer.color = nextKey.OriginalColor;
        nextKey.Renderer.sortingOrder = nextKey.OriginalSortingOrder;
        if (nextKey.FloatHandle != null)
            nextKey.FloatHandle.enabled = false;
        if (nextKey.FollowTarget != null)
            nextKey.FollowTarget.enabled = false;
        if (nextKey.Collider != null)
            nextKey.Collider.enabled = false;
        nextKey.State = KeyState.Hidden;

        if (nextKey.JumpOut == null)
        {
            Debug.LogError($"[Level113Ctrl] {nextKey.Transform.name} 缺少原 ItemJumpOutExecutor，无法保持原钥匙出现表现。", this);
            SetKeyReadyOnPlatform(nextKey);
            operationLocked = false;
            yield break;
        }

        bool jumpCompleted = false;
        nextKey.JumpOut.PlayBetween(chest.Transform.position, keyPlatformPosition, () => jumpCompleted = true);
        while (!isCleaningUp && !jumpCompleted)
            yield return null;

        if (isCleaningUp)
            yield break;

        SetKeyReadyOnPlatform(nextKey);
        operationLocked = false;
    }

    private IEnumerator HideConsumedKeyAfterOriginalDelay(KeyRuntime usedKey)
    {
        yield return new WaitForSeconds(OriginalConsumedKeyHideDelay);
        if (isCleaningUp || usedKey == null)
            yield break;

        usedKey.State = KeyState.Consumed;
        usedKey.Transform.gameObject.SetActive(false);
    }

    private void SpawnChestShiny(ChestRuntime chest)
    {
        if (shinyTemplate == null || chest?.Transform == null)
            return;

        GameObject effect = Instantiate(shinyTemplate, transform);
        effect.name = $"Level113_Shiny_{chest.Transform.name}";
        effect.transform.position = chest.Transform.position + Vector3.up * 0.5f;
        effect.SetActive(true);
        spawnedChestEffects.Add(effect);
    }

    private void SetKeyReadyOnPlatform(KeyRuntime key)
    {
        key.State = KeyState.ReadyOnPlatform;
        key.Transform.position = keyPlatformPosition;
        key.Transform.localScale = key.OriginalLocalScale;
        key.Renderer.enabled = true;
        key.Renderer.color = key.OriginalColor;
        key.Renderer.sortingOrder = key.OriginalSortingOrder;
        if (key.FollowTarget != null)
            key.FollowTarget.enabled = false;
        if (key.FloatHandle != null)
            key.FloatHandle.enabled = true;
        if (key.Collider != null)
            key.Collider.enabled = true;
        if (key.PickupExecutor != null)
            key.PickupExecutor.enabled = true;
        key.Drag.SetReady(true);
    }

    #endregion

    #region Drag To Jump Button

    /// <summary>
    /// 尝试把松手位置命中的台面钥匙安装到 jump_btn。
    /// 调用边界：仅由 Level113KeyDrag 在指针结束时调用；失败不改变钥匙状态。
    /// </summary>
    public bool TryInstallKey(Level113KeyDrag source, Vector2 screenPoint)
    {
        if (!CanOperateKey(source) || !TryResolveJumpButton() || !IsJumpButtonScreenPoint(screenPoint))
            return false;

        KeyRuntime selected = FindKeyByDrag(source);

        if (selected == null)
            return false;

        return BeginToolInstallation(selected, source.KeySprite);
    }

    private bool BeginToolInstallation(KeyRuntime selected, Sprite keySprite)
    {
        if (selected == null || actor == null)
            return false;

        operationLocked = true;
        equippedKey = selected;
        selected.State = KeyState.Installing;
        selected.Drag.CompleteInstallation();
        if (selected.FloatHandle != null)
            selected.FloatHandle.enabled = false;
        if (selected.FollowTarget != null)
            selected.FollowTarget.enabled = false;

        for (int index = 0; index < keys.Count; index++)
            keys[index].Drag.SetReady(false);

        ApplyInstalledButtonVisual(keySprite);
        DisableSharedJumpButton();
        levelCtrl?.SetCtrlable(false);
        actor?.StopMoving();
        EnsureToolAnchor();
        AttachEquippedKey(selected);
        actor.PlayObtain(CompleteObtain);
        return true;
    }

    private void AttachEquippedKey(KeyRuntime key)
    {
        key.Renderer.enabled = true;
        key.Renderer.color = key.OriginalColor;
        key.Renderer.sortingOrder = key.OriginalSortingOrder;

        if (toolAnchor != null)
        {
            key.Transform.SetParent(toolAnchor, false);
            key.Transform.localPosition = Vector3.zero;
            key.Transform.localRotation = Quaternion.identity;
            key.Transform.localScale = key.OriginalLocalScale;
        }

        key.State = KeyState.Equipped;
    }

    private void CompleteObtain()
    {
        if (isCleaningUp || equippedKey == null)
            return;

        operationLocked = false;
        toolButtonReady = true;
        RestoreActorControl();
    }

    private void EnsureToolAnchor()
    {
        if (toolAnchor != null || actor == null)
            return;

        GameObject anchorObject = new GameObject("Level113_PointHitAnchor");
        toolAnchor = anchorObject.transform;
        toolAnchor.SetParent(transform, false);

        SkeletonAnimation skeleton = actor.GetComponentInChildren<SkeletonAnimation>(true);
        if (skeleton != null)
        {
            BoneFollower follower = anchorObject.AddComponent<BoneFollower>();
            follower.SkeletonRenderer = skeleton;
            follower.Initialize();
            if (follower.valid && follower.SetBone(ToolBoneName))
            {
                follower.LateUpdate();
                return;
            }

            Destroy(follower);
        }

        Debug.LogWarning($"[Level113Ctrl] 未找到 Spine 挂点 {ToolBoneName}，降级挂到 Actor 根节点。", this);
        toolAnchor.SetParent(actor.transform, false);
        toolAnchor.localPosition = Vector3.up;
    }

    #endregion

    #region Tool Hit And Door Unlock

    private void HandleToolButtonInput()
    {
        if (Input.touchCount > 0)
        {
            for (int index = 0; index < Input.touchCount; index++)
            {
                Touch touch = Input.GetTouch(index);
                if (touch.phase == TouchPhase.Began && IsJumpButtonScreenPoint(touch.position))
                {
                    BeginToolButtonFeedback(touch.fingerId);
                    UseEquippedKey();
                    return;
                }
            }

            return;
        }

        if (Input.GetMouseButtonDown(0) && IsJumpButtonScreenPoint(Input.mousePosition))
        {
            BeginToolButtonFeedback(-1);
            UseEquippedKey();
        }
    }

    private void BeginToolButtonFeedback(int touchId)
    {
        if (toolButtonPointerDown)
            EndToolButtonFeedback();

        toolButtonPointerDown = true;
        toolButtonTouchId = touchId;
        jumpButtonScaleFeedback?.OnButtonDown(true);
    }

    private void HandleToolButtonRelease()
    {
        if (toolButtonTouchId < 0)
        {
            if (Input.GetMouseButton(0))
                return;

            EndToolButtonFeedback();
            return;
        }

        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (touch.fingerId != toolButtonTouchId)
                continue;

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                EndToolButtonFeedback();
            return;
        }

        EndToolButtonFeedback();
    }

    private void EndToolButtonFeedback()
    {
        if (!toolButtonPointerDown)
            return;

        toolButtonPointerDown = false;
        toolButtonTouchId = -1;
        jumpButtonScaleFeedback?.OnButtonUp(true);
    }

    private void UseEquippedKey()
    {
        if (!toolButtonReady || operationLocked || equippedKey == null || actor == null)
            return;

        bool isNearLock = doorLockRoot != null && doorLockRoot.gameObject.activeInHierarchy &&
                          Vector2.Distance(actor.transform.position, doorLockRoot.position) <= unlockUseDistance;
        operationLocked = true;
        toolButtonReady = false;
        levelCtrl?.SetCtrlable(false);
        actor.StopMoving();

        if (!isNearLock)
        {
            actor.PlayHit(actor.transform.position.x + (actor.FaingRight ? 1f : -1f), CompleteMissHit);
            return;
        }

        unlockInProgress = true;
        hitAnimationCompleted = false;
        lockAnimationCompleted = false;
        actor.PlayHit(doorLockRoot.position.x, () => hitAnimationCompleted = true);
        StartCoroutine(PlayDoorHitEffectAfterDelay());
        PlayDoorLockUnlock();
        StartCoroutine(WaitForDoorUnlock());
    }

    private IEnumerator PlayDoorHitEffectAfterDelay()
    {
        float elapsed = 0f;
        while (!isCleaningUp && elapsed < doorHitEffectDelay)
        {
            if (IsGamePlaying())
                elapsed += Time.deltaTime;
            yield return null;
        }

        if (isCleaningUp || doorHitEffect == null || doorLockRoot == null)
            yield break;

        Transform effectTransform = doorHitEffect.transform;
        Vector3 effectPosition = doorLockRoot.position + (Vector3)doorHitEffectOffset;
        effectPosition.z = effectTransform.position.z;
        effectTransform.position = effectPosition;
        doorHitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        doorHitEffect.Play(false);
    }

    private void CompleteMissHit()
    {
        if (isCleaningUp)
            return;

        operationLocked = false;
        toolButtonReady = true;
        RestoreActorControl();
    }

    private void PlayDoorLockUnlock()
    {
        AudioManager.PlaySound("unlock");
        Spine.TrackEntry entry = doorLockSpine != null
            ? doorLockSpine.PlayAnimation("unlock", false, false)
            : null;
        if (entry == null)
        {
            lockAnimationCompleted = true;
            return;
        }

        entry.Complete += _ => lockAnimationCompleted = true;
    }

    private IEnumerator WaitForDoorUnlock()
    {
        float elapsed = 0f;
        while (!isCleaningUp && (!hitAnimationCompleted || !lockAnimationCompleted) && elapsed < unlockTimeout)
        {
            if (IsGamePlaying())
                elapsed += Time.deltaTime;
            yield return null;
        }

        if (!isCleaningUp)
            CompleteDoorUnlock();
    }

    private void CompleteDoorUnlock()
    {
        unlockInProgress = false;
        operationLocked = false;
        toolButtonReady = false;

        if (doorLockRoot != null)
            doorLockRoot.gameObject.SetActive(false);
        if (exitDoorCollider != null)
            exitDoorCollider.enabled = true;
        RestoreActorControl();
    }

    #endregion

    #region Runtime Setup

    private void ResolveCoreReferences()
    {
        levelCtrl ??= GetComponent<BaseLevelCtrl>();
        actor ??= levelCtrl != null ? levelCtrl.Actor as Level113ActorCtrl : null;
        actor ??= GetComponentInChildren<Level113ActorCtrl>(true);
    }

    private bool BuildKeyAndChestRuntime()
    {
        if (levelCtrl == null || actor == null)
        {
            Debug.LogError("[Level113Ctrl] 缺少 BaseLevelCtrl 或 Level113ActorCtrl。", this);
            return false;
        }

        for (int index = 0; index < KeyNames.Length; index++)
        {
            Transform keyTransform = FindDescendant(transform, KeyNames[index]);
            if (keyTransform == null)
            {
                Debug.LogError($"[Level113Ctrl] 缺少钥匙节点: {KeyNames[index]}", this);
                return false;
            }

            KeyRuntime key = CreateKeyRuntime(keyTransform);
            keys.Add(key);
            keyByTransform.Add(keyTransform, key);
        }

        keyPlatformPosition = keys[0].Transform.position;

        for (int index = 0; index < ChestNames.Length; index++)
        {
            Transform chestTransform = FindDescendant(transform, ChestNames[index]);
            if (chestTransform == null)
            {
                Debug.LogError($"[Level113Ctrl] 缺少箱子节点: {ChestNames[index]}", this);
                return false;
            }

            ChestRuntime chest = CreateChestRuntime(chestTransform);
            chests.Add(chest);
            chestByTransform.Add(chestTransform, chest);
        }

        Transform shinyTransform = FindDescendant(transform, "Shiny");
        shinyTemplate = shinyTransform != null ? shinyTransform.gameObject : null;

        return true;
    }

    private KeyRuntime CreateKeyRuntime(Transform keyTransform)
    {
        foreach (OnTrigger2DHandle handle in keyTransform.GetComponents<OnTrigger2DHandle>())
            handle.enabled = false;
        foreach (StateTriggerSwitchStateHandle handle in keyTransform.GetComponents<StateTriggerSwitchStateHandle>())
            handle.enabled = false;
        ItemJumpOutExecutor jumpOut = keyTransform.GetComponent<ItemJumpOutExecutor>();
        foreach (ItemJumpOutExecutor executor in keyTransform.GetComponents<ItemJumpOutExecutor>())
            executor.enabled = false;

        SpriteRenderer renderer = keyTransform.GetComponent<SpriteRenderer>();

        KeyRuntime key = new KeyRuntime
        {
            Transform = keyTransform,
            OriginalParent = keyTransform.parent,
            Renderer = renderer,
            Collider = keyTransform.GetComponent<Collider2D>(),
            FloatHandle = keyTransform.GetComponent<MoveFloatHandle>(),
            FollowTarget = keyTransform.GetComponent<FollowTarget>(),
            JumpOut = jumpOut,
            PickupExecutor = keyTransform.GetComponent<TriggerExecutor>(),
            OriginalLocalScale = keyTransform.localScale,
            OriginalColor = renderer != null ? renderer.color : Color.white,
            State = KeyState.Hidden
        };
        key.OriginalSortingOrder = key.Renderer != null ? key.Renderer.sortingOrder : 0;

        if (key.FloatHandle != null)
            key.FloatHandle.enabled = false;
        if (key.FollowTarget != null)
            key.FollowTarget.enabled = false;

        Level113TriggerRelay relay = keyTransform.GetComponent<Level113TriggerRelay>();
        relay ??= keyTransform.gameObject.AddComponent<Level113TriggerRelay>();
        relay.Configure(this, Level113TriggerRelay.TriggerKind.Key);

        Level113KeyDrag drag = keyTransform.GetComponent<Level113KeyDrag>();
        drag ??= keyTransform.gameObject.AddComponent<Level113KeyDrag>();
        drag.Configure(this, key.Renderer, key.Collider, key.FloatHandle);
        key.Drag = drag;
        return key;
    }

    private ChestRuntime CreateChestRuntime(Transform chestTransform)
    {
        foreach (StateTriggerDisplayHandle handle in chestTransform.GetComponents<StateTriggerDisplayHandle>())
            handle.enabled = false;

        Level113TriggerRelay relay = chestTransform.GetComponent<Level113TriggerRelay>();
        relay ??= chestTransform.gameObject.AddComponent<Level113TriggerRelay>();
        relay.Configure(this, Level113TriggerRelay.TriggerKind.Chest);

        return new ChestRuntime
        {
            Transform = chestTransform,
            Collider = chestTransform.GetComponent<Collider2D>(),
            Spine = chestTransform.GetComponentInChildren<ActorSpineCtrl>(true),
            KeySort = chestTransform.GetComponent<SwitchSortLayerListener>(),
            KeyScale = chestTransform.GetComponent<ItemScaleListener>(),
            KeyFly = chestTransform.GetComponent<ItemFlyPathListener>()
        };
    }

    private void PrepareInitialKeyState()
    {
        for (int index = 0; index < keys.Count; index++)
        {
            KeyRuntime key = keys[index];
            if (index == 0)
            {
                key.Transform.gameObject.SetActive(true);
                SetKeyReadyOnPlatform(key);
            }
            else
            {
                key.State = KeyState.Hidden;
                key.Drag.SetReady(false);
                key.Transform.gameObject.SetActive(false);
            }
        }
    }

    private void PrepareDoor()
    {
        Transform doorRoot = FindDescendant(transform, "door_0");
        exitDoorCollider = doorRoot != null ? doorRoot.GetComponent<Collider2D>() : null;
        if (exitDoorCollider != null)
            exitDoorCollider.enabled = false;

        doorLockRoot = FindDescendant(transform, "DoorLock");
        if (doorLockRoot == null)
        {
            Debug.LogError("[Level113Ctrl] 缺少 DoorLock。", this);
            return;
        }

        foreach (StateTriggerDisplayHandle handle in doorLockRoot.GetComponents<StateTriggerDisplayHandle>())
            handle.enabled = false;
        doorLockSpine = doorLockRoot.GetComponentInChildren<ActorSpineCtrl>(true);

        Transform hitEffectRoot = FindDescendant(transform, "Knock_Explosion");
        doorHitEffect = hitEffectRoot != null
            ? hitEffectRoot.GetComponentInChildren<ParticleSystem>(true)
            : null;
        doorHitEffect?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    #endregion

    #region Jump Button

    private bool TryResolveJumpButton()
    {
        if (jumpButtonRect != null)
            return jumpButtonIcon != null;
        if (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null)
            return false;

        Transform panel = PlayCtrl.Instance.CurrentGamePlayPanel.transform;
        Transform slot = panel.Find("Root/jump_btn");
        if (slot == null && panel.childCount > 0)
            slot = panel.GetChild(0).Find("jump_btn");

        jumpButtonRect = slot as RectTransform;
        if (jumpButtonRect == null)
            return false;

        jumpButtonCanvas = jumpButtonRect.GetComponentInParent<Canvas>();
        jumpButton = jumpButtonRect.GetComponent<CustomButton>();
        jumpButton ??= jumpButtonRect.GetComponentInChildren<CustomButton>(true);
        jumpButtonScaleFeedback = jumpButtonRect.GetComponent<ClickScaleAnim>();
        jumpButtonScaleFeedback ??= jumpButtonRect.GetComponentInChildren<ClickScaleAnim>(true);

        // 复用面板显式绑定的箭头图标，避免把 bg 背景当作替换目标。
        jumpButtonIcon = PlayCtrl.Instance.CurrentGamePlayPanel.JumpButtonIcon;

        CacheJumpButtonState();
        return jumpButtonIcon != null;
    }

    private void CacheJumpButtonState()
    {
        if (!hasJumpButtonState && jumpButton != null)
        {
            jumpButtonInitialEnabled = jumpButton.enabled;
            jumpButtonInitialInteractable = jumpButton.Interactable;
            jumpButtonInitialSiblingIndex = jumpButtonRect.GetSiblingIndex();
            hasJumpButtonState = true;
        }

        if (!hasJumpIconState && jumpButtonIcon != null)
        {
            RectTransform iconRect = jumpButtonIcon.rectTransform;
            jumpIconInitialSprite = jumpButtonIcon.sprite;
            jumpIconInitialColor = jumpButtonIcon.color;
            jumpIconInitialPreserveAspect = jumpButtonIcon.preserveAspect;
            jumpIconInitialRotation = iconRect.localRotation;
            jumpIconInitialPosition = iconRect.anchoredPosition;
            jumpIconInitialSize = iconRect.sizeDelta;
            jumpIconInitialSiblingIndex = iconRect.GetSiblingIndex();
            hasJumpIconState = true;
        }
    }

    private void ApplyInstalledButtonVisual(Sprite keySprite)
    {
        if (jumpButtonIcon == null || keySprite == null)
            return;

        jumpButtonRect.SetAsLastSibling();
        RectTransform iconRect = jumpButtonIcon.rectTransform;
        iconRect.SetAsLastSibling();
        jumpButtonIcon.sprite = keySprite;
        jumpButtonIcon.color = Color.white;
        jumpButtonIcon.preserveAspect = true;
        iconRect.localRotation = Quaternion.identity;
        iconRect.anchoredPosition = Vector2.zero;
        Vector2 buttonSize = jumpButtonRect.rect.size;
        float iconSide = Mathf.Max(1f, Mathf.Min(buttonSize.x, buttonSize.y) * 0.82f);
        iconRect.sizeDelta = Vector2.one * iconSide;
    }

    private void DisableSharedJumpButton()
    {
        if (jumpButton == null)
            return;
        jumpButton.Interactable = false;
        jumpButton.enabled = false;
    }

    private bool IsJumpButtonScreenPoint(Vector2 screenPoint)
    {
        return jumpButtonRect != null && RectTransformUtility.RectangleContainsScreenPoint(
            jumpButtonRect,
            screenPoint,
            GetJumpButtonEventCamera());
    }

    private Camera GetJumpButtonEventCamera()
    {
        return jumpButtonCanvas != null && jumpButtonCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? jumpButtonCanvas.worldCamera
            : null;
    }

    private void RestoreJumpButtonState()
    {
        if (jumpButton != null && hasJumpButtonState)
        {
            jumpButton.enabled = jumpButtonInitialEnabled;
            jumpButton.Interactable = jumpButtonInitialInteractable;
            jumpButtonRect.SetSiblingIndex(Mathf.Min(jumpButtonInitialSiblingIndex, jumpButtonRect.parent.childCount - 1));
        }

        if (jumpButtonIcon != null && hasJumpIconState)
        {
            RectTransform iconRect = jumpButtonIcon.rectTransform;
            jumpButtonIcon.sprite = jumpIconInitialSprite;
            jumpButtonIcon.color = jumpIconInitialColor;
            jumpButtonIcon.preserveAspect = jumpIconInitialPreserveAspect;
            iconRect.localRotation = jumpIconInitialRotation;
            iconRect.anchoredPosition = jumpIconInitialPosition;
            iconRect.sizeDelta = jumpIconInitialSize;
            iconRect.SetSiblingIndex(Mathf.Min(jumpIconInitialSiblingIndex, iconRect.parent.childCount - 1));
        }
    }

    #endregion

    #region Helpers And Cleanup

    private bool IsCurrentActor(Collider2D other)
    {
        return other != null && actor != null && other.GetComponentInParent<ActorCtrl>() == actor;
    }

    private KeyRuntime FindKeyByDrag(Level113KeyDrag source)
    {
        for (int index = 0; index < keys.Count; index++)
        {
            if (keys[index].Drag == source)
                return keys[index];
        }

        return null;
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        if (root == null)
            return null;
        if (root.name == targetName)
            return root;

        for (int index = 0; index < root.childCount; index++)
        {
            Transform result = FindDescendant(root.GetChild(index), targetName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static bool IsGamePlaying()
    {
        return PlayCtrl.Instance != null && PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
    }

    private void RestoreActorControl()
    {
        actor?.ResumeMoving();
        levelCtrl?.SetCtrlable(true);
    }

    private void CleanupRuntimeState()
    {
        if (isCleaningUp)
            return;

        isCleaningUp = true;
        StopAllCoroutines();
        EndToolButtonFeedback();
        RestoreJumpButtonState();
        actor?.ClearLevel113ToolState();

        for (int index = 0; index < spawnedChestEffects.Count; index++)
        {
            if (spawnedChestEffects[index] != null)
                Destroy(spawnedChestEffects[index]);
        }
        spawnedChestEffects.Clear();

        if (toolAnchor != null)
        {
            Destroy(toolAnchor.gameObject);
            toolAnchor = null;
        }
    }

    #endregion
}

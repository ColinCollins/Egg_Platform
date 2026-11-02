using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.ItemEvent;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Flags]
public enum Level111RepairKind
{
    None = 0,
    Door = 1 << 0,
    DoorLock = 1 << 1,
    LevelBadge = 1 << 2,
    ShopIcon = 1 << 3,
    PauseTipsIcons = 1 << 4,
    All = Door | DoorLock | LevelBadge | ShopIcon | PauseTipsIcons
}

/// <summary>
/// Level111「恢复异常」总控：五处异常全部修复后显示钥匙，后续继续复用关卡原有钥匙、门锁与出口链。
/// </summary>
public sealed class Level111Ctrl : MonoBehaviour
{
    [Header("Repair Feedback")]
    [SerializeField] private Sprite repairCheckSprite;
    [SerializeField, Min(0.1f)] private float doorSnapRadius = 1.8f;
    [SerializeField, Min(0.1f)] private float doorLockSnapRadius = 1.55f;
    [SerializeField, Min(0.05f)] private float worldSnapDuration = 0.24f;
    [SerializeField, Min(0.05f)] private float assembledDoorRevealDuration = 0.3f;
    [SerializeField, Range(0.5f, 1f)] private float assembledDoorRevealStartScale = 0.88f;
    [SerializeField, Min(0.1f)] private float keyRevealDuration = 0.32f;
    [SerializeField, Min(0.1f)] private float keyDropDuration = 0.55f;

    private BaseLevelCtrl levelCtrl;
    private Transform lowerDoorHalf;
    private Transform upperDoorHalf;
    private Transform assembledDoor;
    private Transform doorLock;
    private Transform doorLockPoint;
    private Vector3 assembledDoorAuthoredScale = Vector3.one;
    private GameObject key;
    private ItemJumpOutExecutor[] keyAutoJumpExecutors = Array.Empty<ItemJumpOutExecutor>();
    private Level111RepairKind repairedKinds;
    private Level111UiRepairController uiRepairController;
    private Canvas feedbackCanvas;
    private bool keyRevealed;

    private void Awake()
    {
        levelCtrl = GetComponent<BaseLevelCtrl>();
        levelCtrl?.SetEnterDoorAnimationEnabled(false);

        assembledDoor = transform.Find("door_0");
        if (assembledDoor != null)
        {
            assembledDoorAuthoredScale = assembledDoor.localScale;
            doorLockPoint = FindDescendant(assembledDoor, "LockPoint");
            assembledDoor.gameObject.SetActive(false);
        }

        key = transform.Find("Key")?.gameObject;
        if (key != null)
        {
            DisableKeyAutoJumpExecutors();
            key.SetActive(false);
        }
    }

    private void Start()
    {
        SetupWorldRepairs();
        SetupUiRepairsAsync().Forget();
    }

    private void SetupWorldRepairs()
    {
        lowerDoorHalf = transform.Find("Sp_l111_zs_01");
        upperDoorHalf = transform.Find("Sp_l111_zs_02") ?? transform.Find("Sp_l111_zs_01 (1)");
        doorLock = transform.Find("DoorLock");

        if (upperDoorHalf != null)
        {
            Level111WorldDragItem halfDrag = upperDoorHalf.GetComponent<Level111WorldDragItem>();
            if (halfDrag == null)
                halfDrag = upperDoorHalf.gameObject.AddComponent<Level111WorldDragItem>();
            halfDrag.Configure(this, Level111RepairKind.Door);
        }
        else
        {
            Debug.LogError("[Level111] Upper door half is missing.", this);
        }

        if (doorLock != null)
        {
            Level111WorldDragItem lockDrag = doorLock.GetComponent<Level111WorldDragItem>();
            if (lockDrag == null)
                lockDrag = doorLock.gameObject.AddComponent<Level111WorldDragItem>();
            lockDrag.Configure(this, Level111RepairKind.DoorLock);
        }
        else
        {
            Debug.LogError("[Level111] DoorLock is missing.", this);
        }
    }

    private async UniTaskVoid SetupUiRepairsAsync()
    {
        try
        {
            await UniTask.WaitUntil(
                () => PlayCtrl.Instance != null && PlayCtrl.Instance.CurrentGamePlayPanel != null,
                cancellationToken: this.GetCancellationTokenOnDestroy());

            GamePlayPanel panel = PlayCtrl.Instance.CurrentGamePlayPanel;
            uiRepairController = new Level111UiRepairController(this, panel);
            if (!uiRepairController.TryInitialize())
                Debug.LogError("[Level111] GamePlayPanel repair targets are incomplete.", this);
        }
        catch (OperationCanceledException)
        {
            // The level was replaced before the shared UI finished opening.
        }
    }

    public bool TryCompleteWorldRepair(Level111WorldDragItem item)
    {
        if (item == null || (repairedKinds & item.Kind) != 0)
            return false;

        switch (item.Kind)
        {
            case Level111RepairKind.Door:
                return TryAssembleDoor(item);
            case Level111RepairKind.DoorLock:
                return TryPlaceDoorLock(item);
            default:
                return false;
        }
    }

    private bool TryAssembleDoor(Level111WorldDragItem item)
    {
        if (lowerDoorHalf == null || upperDoorHalf == null || assembledDoor == null)
            return false;

        Vector3 targetPosition = CalculateUpperDoorHalfTarget();
        if (Vector2.Distance(item.transform.position, targetPosition) > doorSnapRadius)
            return false;

        item.SnapTo(
            targetPosition,
            Quaternion.identity,
            item.OriginScale,
            worldSnapDuration,
            () =>
            {
                if (assembledDoor == null)
                    return;

                assembledDoor.DOKill();
                assembledDoor.localScale = assembledDoorAuthoredScale *
                                           Mathf.Clamp(assembledDoorRevealStartScale, 0.5f, 1f);
                assembledDoor.gameObject.SetActive(true);

                if (lowerDoorHalf != null)
                    lowerDoorHalf.gameObject.SetActive(false);
                if (upperDoorHalf != null)
                    upperDoorHalf.gameObject.SetActive(false);

                assembledDoor.DOScale(assembledDoorAuthoredScale, assembledDoorRevealDuration)
                    .SetEase(Ease.OutBack)
                    .SetLink(assembledDoor.gameObject)
                    .OnComplete(() => CompleteRepair(
                        Level111RepairKind.Door,
                        WorldToScreen(GetVisualCenter(assembledDoor))));
            },
            true);
        return true;
    }

    private Vector3 CalculateUpperDoorHalfTarget()
    {
        SpriteRenderer lowerRenderer = lowerDoorHalf.GetComponent<SpriteRenderer>();
        SpriteRenderer upperRenderer = upperDoorHalf.GetComponent<SpriteRenderer>();

        float lowerHeight = GetUnrotatedWorldHeight(lowerRenderer);
        float upperHeight = GetUnrotatedWorldHeight(upperRenderer);
        float centerDistance = Mathf.Max(0.1f, (lowerHeight + upperHeight) * 0.5f - 0.03f);

        Vector3 target = lowerDoorHalf.position + Vector3.up * centerDistance;
        target.z = upperDoorHalf.position.z;
        return target;
    }

    private static float GetUnrotatedWorldHeight(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null)
            return 1f;

        return renderer.sprite.bounds.size.y * Mathf.Abs(renderer.transform.lossyScale.y);
    }

    private bool TryPlaceDoorLock(Level111WorldDragItem item)
    {
        if ((repairedKinds & Level111RepairKind.Door) == 0 || doorLockPoint == null)
            return false;

        Vector3 targetPosition = doorLockPoint.position;
        targetPosition.z = item.transform.position.z;
        if (Vector2.Distance(item.transform.position, targetPosition) > doorLockSnapRadius)
            return false;

        Vector3 targetScale = item.OriginScale;
        targetScale.x = Mathf.Abs(targetScale.x);

        item.SnapTo(
            targetPosition,
            Quaternion.identity,
            targetScale,
            worldSnapDuration,
            () => CompleteRepair(
                Level111RepairKind.DoorLock,
                WorldToScreen(GetVisualCenter(item.transform))));
        return true;
    }

    public void CompleteUiRepair(Level111RepairKind kind, RectTransform target, Canvas canvas)
    {
        Vector2 screenPosition = target != null
            ? RectTransformUtility.WorldToScreenPoint(canvas != null ? canvas.worldCamera : null, target.position)
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        CompleteRepair(kind, screenPosition);
    }

    private void CompleteRepair(Level111RepairKind kind, Vector2 screenPosition)
    {
        if (kind == Level111RepairKind.None || (repairedKinds & kind) != 0)
            return;

        repairedKinds |= kind;
        ShowRepairFeedback(screenPosition);

        if ((repairedKinds & Level111RepairKind.All) == Level111RepairKind.All)
            RevealKey();
    }

    private void RevealKey()
    {
        if (keyRevealed || key == null)
            return;

        keyRevealed = true;
        DisableKeyAutoJumpExecutors();
        Transform keyTransform = key.transform;
        Vector3 authoredScale = keyTransform.localScale;
        Vector3 landingPosition = keyTransform.position;
        Vector3 revealPosition = ScreenToWorld(
            new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
            landingPosition.z);
        landingPosition.x = revealPosition.x;

        Collider2D[] keyColliders = key.GetComponentsInChildren<Collider2D>(true);
        bool[] keyColliderStates = new bool[keyColliders.Length];
        for (int index = 0; index < keyColliders.Length; index++)
        {
            Collider2D keyCollider = keyColliders[index];
            if (keyCollider == null)
                continue;

            keyColliderStates[index] = keyCollider.enabled;
            keyCollider.enabled = false;
        }

        MoveFloatHandle floatHandle = key.GetComponent<MoveFloatHandle>();
        bool floatHandleWasEnabled = floatHandle != null && floatHandle.enabled;
        if (floatHandleWasEnabled)
            floatHandle.enabled = false;

        keyTransform.DOKill();
        keyTransform.position = revealPosition;
        keyTransform.localScale = Vector3.zero;
        key.SetActive(true);

        Sequence sequence = DOTween.Sequence().SetLink(key);
        float safeRevealDuration = Mathf.Max(0.1f, keyRevealDuration);
        float safeDropDuration = Mathf.Max(0.1f, keyDropDuration);
        Vector3 stretchScale = new Vector3(
            authoredScale.x * 0.82f,
            authoredScale.y * 1.18f,
            authoredScale.z);
        Vector3 squashScale = new Vector3(
            authoredScale.x * 1.08f,
            authoredScale.y * 0.92f,
            authoredScale.z);

        sequence.Append(keyTransform.DOScale(stretchScale, safeRevealDuration * 0.56f).SetEase(Ease.OutBack));
        sequence.Append(keyTransform.DOScale(squashScale, safeRevealDuration * 0.18f).SetEase(Ease.OutQuad));
        sequence.Append(keyTransform.DOScale(authoredScale, safeRevealDuration * 0.26f).SetEase(Ease.OutBack));
        sequence.Append(keyTransform.DOMoveY(landingPosition.y, safeDropDuration).SetEase(Ease.InQuad));
        sequence.OnComplete(() =>
        {
            keyTransform.position = landingPosition;
            for (int index = 0; index < keyColliders.Length; index++)
            {
                if (keyColliders[index] != null)
                    keyColliders[index].enabled = keyColliderStates[index];
            }

            if (floatHandleWasEnabled && floatHandle != null)
                floatHandle.enabled = true;
        });
    }

    private void DisableKeyAutoJumpExecutors()
    {
        if (key == null)
            return;

        if (keyAutoJumpExecutors == null || keyAutoJumpExecutors.Length == 0)
            keyAutoJumpExecutors = key.GetComponentsInChildren<ItemJumpOutExecutor>(true);

        for (int index = 0; index < keyAutoJumpExecutors.Length; index++)
        {
            ItemJumpOutExecutor executor = keyAutoJumpExecutors[index];
            if (executor == null)
                continue;

            executor.StopAllCoroutines();
            executor.enabled = false;
        }
    }

    private void ShowRepairFeedback(Vector2 screenPosition)
    {
        AudioManager.PlaySound("verifticationCorrect");
        if (repairCheckSprite == null)
            return;

        EnsureFeedbackCanvas();
        if (feedbackCanvas == null)
            return;

        GameObject checkObject = new GameObject(
            "Level111_RepairCheck",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        checkObject.transform.SetParent(feedbackCanvas.transform, false);

        RectTransform rect = (RectTransform)checkObject.transform;
        rect.sizeDelta = new Vector2(92f, 80f);
        rect.position = screenPosition;
        rect.localScale = Vector3.zero;

        Image image = checkObject.GetComponent<Image>();
        image.sprite = repairCheckSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        CanvasGroup group = checkObject.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(checkObject);
        sequence.Append(rect.DOScale(1.18f, 0.22f).SetEase(Ease.OutBack));
        sequence.Append(rect.DOScale(1f, 0.1f).SetEase(Ease.OutQuad));
        sequence.AppendInterval(0.24f);
        sequence.Append(group.DOFade(0f, 0.26f).SetEase(Ease.InQuad));
        sequence.OnComplete(() => Destroy(checkObject));
    }

    private void EnsureFeedbackCanvas()
    {
        if (feedbackCanvas != null)
            return;

        GameObject canvasObject = new GameObject(
            "Level111_RepairFeedbackCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        feedbackCanvas = canvasObject.GetComponent<Canvas>();
        feedbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        feedbackCanvas.overrideSorting = true;
        feedbackCanvas.sortingOrder = 32760;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        if (root == null)
            return null;

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < descendants.Length; index++)
        {
            if (descendants[index] != null && descendants[index].name == targetName)
                return descendants[index];
        }

        return null;
    }

    private static Vector2 WorldToScreen(Vector3 worldPosition)
    {
        Camera camera = Camera.main;
        return camera != null
            ? (Vector2)camera.WorldToScreenPoint(worldPosition)
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    private static Vector3 GetVisualCenter(Transform target)
    {
        if (target == null)
            return Vector3.zero;

        Renderer[] targetRenderers = target.GetComponentsInChildren<Renderer>(false);
        bool hasBounds = false;
        Bounds combinedBounds = default;
        for (int index = 0; index < targetRenderers.Length; index++)
        {
            Renderer targetRenderer = targetRenderers[index];
            if (targetRenderer == null || !targetRenderer.enabled)
                continue;

            if (!hasBounds)
            {
                combinedBounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(targetRenderer.bounds);
            }
        }

        return hasBounds ? combinedBounds.center : target.position;
    }

    private static Vector3 ScreenToWorld(Vector2 screenPosition, float worldZ)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return new Vector3(0f, 0f, worldZ);

        float depth = Mathf.Abs(camera.transform.position.z - worldZ);
        Vector3 worldPosition = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        worldPosition.z = worldZ;
        return worldPosition;
    }

    private void OnDestroy()
    {
        uiRepairController?.RestoreSharedUi();
        uiRepairController = null;

        if (feedbackCanvas != null)
        {
            feedbackCanvas.transform.DOKill();
            Destroy(feedbackCanvas.gameObject);
            feedbackCanvas = null;
        }
    }
}

/// <summary>Level111 对共享 GamePlayPanel 的临时异常状态与手势收尾。</summary>
public sealed class Level111UiRepairController
{
    private const float BadgeSnapTolerance = 34f;
    private const float ShopAngleTolerance = 14f;
    private const float UiReturnDuration = 0.2f;

    private readonly Level111Ctrl owner;
    private readonly GamePlayPanel panel;

    private Canvas canvas;
    private RectTransform levelBadge;
    private RectTransform shopButton;
    private RectTransform pauseButton;
    private RectTransform tipsButton;
    private RectTransform pauseIcon;
    private RectTransform hintIcon;
    private RectTransform warningBubble;
    private Graphic pauseIconGraphic;
    private Graphic hintIconGraphic;
    private CustomButton pauseCustomButton;
    private CustomButton tipsCustomButton;

    private Transform pauseIconNormalParent;
    private Transform hintIconNormalParent;
    private Transform bubbleNormalParent;
    private int pauseIconNormalSibling;
    private int hintIconNormalSibling;
    private int bubbleNormalSibling;
    private Vector2 pauseIconNormalPosition;
    private Vector2 hintIconNormalPosition;
    private Vector2 bubbleNormalPosition;
    private Quaternion pauseIconNormalRotation;
    private Quaternion hintIconNormalRotation;
    private Quaternion bubbleNormalRotation;
    private Vector3 pauseIconNormalScale;
    private Vector3 hintIconNormalScale;
    private Vector3 bubbleNormalScale;
    private bool pauseIconNormalRaycastTarget;
    private bool hintIconNormalRaycastTarget;
    private Vector2 badgeNormalPosition;
    private Vector2 badgeAbnormalPosition;
    private Quaternion shopNormalRotation;
    private Quaternion shopAbnormalRotation;
    private Vector3 pauseSlotWorldPosition;
    private Vector3 tipsSlotWorldPosition;
    private Vector3 bubbleNormalWorldPosition;

    private Level111UiGestureHandle badgeHandle;
    private Level111UiGestureHandle shopHandle;
    private Level111UiGestureHandle pauseIconHandle;
    private Level111UiGestureHandle hintIconHandle;
    private Level111UiGestureHandle activeIconHandle;
    private Canvas activeIconDragCanvas;
    private bool activeIconDragCanvasAdded;
    private bool activeIconCanvasOriginalOverrideSorting;
    private int activeIconCanvasOriginalSortingOrder;
    private Vector2 activeIconReturnPosition;
    private float shopStartPointerAngle;
    private float shopStartRotation;
    private bool badgeCompleted;
    private bool shopCompleted;
    private bool iconSwapCompleted;
    private bool disposed;

    public Level111UiRepairController(Level111Ctrl owner, GamePlayPanel panel)
    {
        this.owner = owner;
        this.panel = panel;
    }

    public bool TryInitialize()
    {
        if (owner == null || panel == null)
            return false;

        Transform root = panel.transform.Find("Root");
        levelBadge = root?.Find("levelBlock") as RectTransform;
        shopButton = root?.Find("shop_btn") as RectTransform;
        pauseButton = root?.Find("pause_btn") as RectTransform;
        tipsButton = root?.Find("tips_btn") as RectTransform;
        pauseIcon = pauseButton?.Find("UI_youxizhong_icon_zanting_01") as RectTransform;
        hintIcon = tipsButton?.Find("UI_youxizhong_icon_tishi_01") as RectTransform;
        pauseIconGraphic = pauseIcon != null ? pauseIcon.GetComponent<Graphic>() : null;
        hintIconGraphic = hintIcon != null ? hintIcon.GetComponent<Graphic>() : null;
        canvas = panel.GetComponentInParent<Canvas>();

        GamePlayPanelTipsSwitcher tipsSwitcher = tipsButton != null
            ? tipsButton.GetComponent<GamePlayPanelTipsSwitcher>()
            : null;
        warningBubble = tipsSwitcher != null
            ? tipsSwitcher.TargetObject?.transform as RectTransform
            : null;

        if (levelBadge == null || shopButton == null || pauseButton == null || tipsButton == null ||
            pauseIcon == null || hintIcon == null || pauseIconGraphic == null || hintIconGraphic == null ||
            canvas == null)
            return false;

        pauseCustomButton = pauseButton.GetComponent<CustomButton>();
        tipsCustomButton = tipsButton.GetComponent<CustomButton>();
        CacheNormalPose();
        ApplyAbnormalPose();
        AttachGestureHandles();
        return true;
    }

    private void CacheNormalPose()
    {
        badgeNormalPosition = levelBadge.anchoredPosition;
        shopNormalRotation = shopButton.localRotation;
        shopAbnormalRotation = Quaternion.Euler(0f, 0f, shopButton.localEulerAngles.z + 180f);

        pauseIconNormalParent = pauseIcon.parent;
        hintIconNormalParent = hintIcon.parent;
        pauseIconNormalSibling = pauseIcon.GetSiblingIndex();
        hintIconNormalSibling = hintIcon.GetSiblingIndex();
        pauseIconNormalPosition = pauseIcon.anchoredPosition;
        hintIconNormalPosition = hintIcon.anchoredPosition;
        pauseIconNormalRotation = pauseIcon.localRotation;
        hintIconNormalRotation = hintIcon.localRotation;
        pauseIconNormalScale = pauseIcon.localScale;
        hintIconNormalScale = hintIcon.localScale;
        pauseIconNormalRaycastTarget = pauseIconGraphic.raycastTarget;
        hintIconNormalRaycastTarget = hintIconGraphic.raycastTarget;

        if (warningBubble != null)
        {
            bubbleNormalParent = warningBubble.parent;
            bubbleNormalSibling = warningBubble.GetSiblingIndex();
            bubbleNormalPosition = warningBubble.anchoredPosition;
            bubbleNormalRotation = warningBubble.localRotation;
            bubbleNormalScale = warningBubble.localScale;
            bubbleNormalWorldPosition = warningBubble.position;
        }
    }

    private void ApplyAbnormalPose()
    {
        RectTransform badgeParent = levelBadge.parent as RectTransform;
        badgeAbnormalPosition = badgeNormalPosition;
        if (badgeParent != null)
            badgeAbnormalPosition.y = badgeParent.rect.yMax;
        else
            badgeAbnormalPosition.y += levelBadge.rect.height * 0.75f;
        levelBadge.anchoredPosition = badgeAbnormalPosition;

        shopButton.localRotation = shopAbnormalRotation;

        pauseIcon.SetParent(tipsButton, false);
        pauseIcon.anchoredPosition = pauseIconNormalPosition;
        pauseIcon.localRotation = pauseIconNormalRotation;
        pauseIcon.localScale = pauseIconNormalScale;

        hintIcon.SetParent(pauseButton, false);
        hintIcon.anchoredPosition = hintIconNormalPosition;
        hintIcon.localRotation = hintIconNormalRotation;
        hintIcon.localScale = hintIconNormalScale;

        // 公共 HUD 的白色图标默认不接收射线，点击由父按钮承接。
        // Level111 需要直接拖动图标，因此只在本关运行期临时开启。
        pauseIconGraphic.raycastTarget = true;
        hintIconGraphic.raycastTarget = true;

        pauseSlotWorldPosition = hintIcon.position;
        tipsSlotWorldPosition = pauseIcon.position;

        if (warningBubble != null)
        {
            warningBubble.SetParent(pauseButton, false);
            warningBubble.anchoredPosition = bubbleNormalPosition;
            warningBubble.localRotation = bubbleNormalRotation;
            warningBubble.localScale = bubbleNormalScale;
        }

        panel.SetPauseTipsFunctionsSwapped(true);
    }

    private void AttachGestureHandles()
    {
        badgeHandle = GetOrAddHandle(levelBadge, Level111UiGestureKind.LevelBadge);
        shopHandle = GetOrAddHandle(shopButton, Level111UiGestureKind.ShopRotate);
        pauseIconHandle = GetOrAddHandle(pauseIcon, Level111UiGestureKind.PauseIcon);
        hintIconHandle = GetOrAddHandle(hintIcon, Level111UiGestureKind.HintIcon);
    }

    private Level111UiGestureHandle GetOrAddHandle(RectTransform target, Level111UiGestureKind kind)
    {
        Level111UiGestureHandle handle = target.GetComponent<Level111UiGestureHandle>();
        if (handle == null)
            handle = target.gameObject.AddComponent<Level111UiGestureHandle>();
        handle.Configure(this, kind);
        return handle;
    }

    public void OnGestureBegin(Level111UiGestureHandle handle, PointerEventData eventData)
    {
        if (disposed || handle == null)
            return;

        switch (handle.Kind)
        {
            case Level111UiGestureKind.LevelBadge when !badgeCompleted:
                levelBadge.DOKill();
                break;
            case Level111UiGestureKind.ShopRotate when !shopCompleted:
                shopButton.DOKill();
                shopStartPointerAngle = PointerAngle(eventData.position, shopButton.position);
                shopStartRotation = shopButton.localEulerAngles.z;
                SetShopClickBlocked(true);
                break;
            case Level111UiGestureKind.PauseIcon:
            case Level111UiGestureKind.HintIcon:
                if (iconSwapCompleted || activeIconHandle != null)
                    return;
                activeIconHandle = handle;
                activeIconReturnPosition = handle.RectTransform.anchoredPosition;
                handle.RectTransform.DOKill();
                SetPauseTipsClickBlocked(true);
                handle.RectTransform.SetAsLastSibling();
                RaiseActiveIconSorting(handle);
                break;
        }
    }

    public void OnGestureDrag(Level111UiGestureHandle handle, PointerEventData eventData)
    {
        if (disposed || handle == null)
            return;

        float scaleFactor = Mathf.Max(0.01f, canvas.scaleFactor);
        switch (handle.Kind)
        {
            case Level111UiGestureKind.LevelBadge when !badgeCompleted:
            {
                Vector2 position = levelBadge.anchoredPosition;
                position.y += eventData.delta.y / scaleFactor;
                position.y = Mathf.Clamp(
                    position.y,
                    badgeNormalPosition.y - 40f,
                    badgeAbnormalPosition.y + 30f);
                levelBadge.anchoredPosition = position;
                break;
            }
            case Level111UiGestureKind.ShopRotate when !shopCompleted:
            {
                float pointerAngle = PointerAngle(eventData.position, shopButton.position);
                float delta = Mathf.DeltaAngle(shopStartPointerAngle, pointerAngle);
                shopButton.localRotation = Quaternion.Euler(0f, 0f, shopStartRotation + delta);
                break;
            }
            case Level111UiGestureKind.PauseIcon:
            case Level111UiGestureKind.HintIcon:
                if (handle != activeIconHandle || iconSwapCompleted)
                    return;
                handle.RectTransform.anchoredPosition += eventData.delta / scaleFactor;
                break;
        }
    }

    public void OnGestureEnd(Level111UiGestureHandle handle, PointerEventData eventData)
    {
        if (disposed || handle == null)
            return;

        switch (handle.Kind)
        {
            case Level111UiGestureKind.LevelBadge when !badgeCompleted:
                FinishBadgeDrag();
                break;
            case Level111UiGestureKind.ShopRotate when !shopCompleted:
                FinishShopRotation();
                break;
            case Level111UiGestureKind.PauseIcon:
            case Level111UiGestureKind.HintIcon:
                if (handle == activeIconHandle && !iconSwapCompleted)
                    FinishIconDrag(handle, eventData);
                break;
        }
    }

    private void FinishBadgeDrag()
    {
        bool success = Mathf.Abs(levelBadge.anchoredPosition.y - badgeNormalPosition.y) <= BadgeSnapTolerance;
        levelBadge.DOKill();
        if (!success)
        {
            levelBadge.DOAnchorPos(badgeAbnormalPosition, UiReturnDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(levelBadge.gameObject);
            return;
        }

        badgeCompleted = true;
        badgeHandle.enabled = false;
        levelBadge.DOAnchorPos(badgeNormalPosition, UiReturnDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(levelBadge.gameObject)
            .OnComplete(() => owner.CompleteUiRepair(Level111RepairKind.LevelBadge, levelBadge, canvas));
    }

    private void FinishShopRotation()
    {
        float current = shopButton.localEulerAngles.z;
        float normal = shopNormalRotation.eulerAngles.z;
        bool success = Mathf.Abs(Mathf.DeltaAngle(current, normal)) <= ShopAngleTolerance;
        shopButton.DOKill();

        if (!success)
        {
            shopButton.DOLocalRotate(shopAbnormalRotation.eulerAngles, UiReturnDuration, RotateMode.Fast)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(shopButton.gameObject)
                .OnComplete(() => SetShopClickBlocked(false));
            return;
        }

        shopCompleted = true;
        shopHandle.enabled = false;
        shopButton.DOLocalRotate(shopNormalRotation.eulerAngles, UiReturnDuration, RotateMode.Fast)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(shopButton.gameObject)
            .OnComplete(() =>
            {
                SetShopClickBlocked(false);
                owner.CompleteUiRepair(Level111RepairKind.ShopIcon, shopButton, canvas);
            });
    }

    private void FinishIconDrag(Level111UiGestureHandle handle, PointerEventData eventData)
    {
        RectTransform other = handle.Kind == Level111UiGestureKind.PauseIcon ? hintIcon : pauseIcon;
        bool success = RectTransformUtility.RectangleContainsScreenPoint(
            other,
            eventData.position,
            canvas.worldCamera);

        if (!success)
        {
            handle.RectTransform.DOAnchorPos(activeIconReturnPosition, UiReturnDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(handle.gameObject)
                .OnComplete(() =>
                {
                    ClearActiveIconSorting();
                    activeIconHandle = null;
                    SetPauseTipsClickBlocked(false);
                });
            return;
        }

        iconSwapCompleted = true;
        pauseIconHandle.enabled = false;
        hintIconHandle.enabled = false;
        pauseIcon.DOKill();
        hintIcon.DOKill();
        warningBubble?.DOKill();

        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(panel.gameObject);
        sequence.Join(pauseIcon.DOMove(pauseSlotWorldPosition, UiReturnDuration).SetEase(Ease.OutBack));
        sequence.Join(hintIcon.DOMove(tipsSlotWorldPosition, UiReturnDuration).SetEase(Ease.OutBack));
        if (warningBubble != null)
            sequence.Join(warningBubble.DOMove(bubbleNormalWorldPosition, UiReturnDuration).SetEase(Ease.OutBack));
        sequence.OnComplete(() =>
        {
            ClearActiveIconSorting();
            activeIconHandle = null;
            RestorePauseTipsNormalPose();
            owner.CompleteUiRepair(Level111RepairKind.PauseTipsIcons, tipsButton, canvas);
        });
    }

    public void RestoreSharedUi()
    {
        if (disposed)
            return;
        disposed = true;

        levelBadge?.DOKill();
        shopButton?.DOKill();
        pauseIcon?.DOKill();
        hintIcon?.DOKill();
        warningBubble?.DOKill();
        ClearActiveIconSorting();

        if (levelBadge != null)
            levelBadge.anchoredPosition = badgeNormalPosition;
        if (shopButton != null)
            shopButton.localRotation = shopNormalRotation;
        SetShopClickBlocked(false);
        RestorePauseTipsNormalPose();

        if (badgeHandle != null)
            badgeHandle.enabled = false;
        if (shopHandle != null)
            shopHandle.enabled = false;
        if (pauseIconHandle != null)
            pauseIconHandle.enabled = false;
        if (hintIconHandle != null)
            hintIconHandle.enabled = false;
    }

    private void RestorePauseTipsNormalPose()
    {
        if (pauseIcon != null && pauseIconNormalParent != null)
        {
            pauseIcon.SetParent(pauseIconNormalParent, false);
            pauseIcon.SetSiblingIndex(pauseIconNormalSibling);
            pauseIcon.anchoredPosition = pauseIconNormalPosition;
            pauseIcon.localRotation = pauseIconNormalRotation;
            pauseIcon.localScale = pauseIconNormalScale;
        }

        if (hintIcon != null && hintIconNormalParent != null)
        {
            hintIcon.SetParent(hintIconNormalParent, false);
            hintIcon.SetSiblingIndex(hintIconNormalSibling);
            hintIcon.anchoredPosition = hintIconNormalPosition;
            hintIcon.localRotation = hintIconNormalRotation;
            hintIcon.localScale = hintIconNormalScale;
        }

        if (pauseIconGraphic != null)
            pauseIconGraphic.raycastTarget = pauseIconNormalRaycastTarget;
        if (hintIconGraphic != null)
            hintIconGraphic.raycastTarget = hintIconNormalRaycastTarget;

        if (warningBubble != null && bubbleNormalParent != null)
        {
            warningBubble.SetParent(bubbleNormalParent, false);
            warningBubble.SetSiblingIndex(bubbleNormalSibling);
            warningBubble.anchoredPosition = bubbleNormalPosition;
            warningBubble.localRotation = bubbleNormalRotation;
            warningBubble.localScale = bubbleNormalScale;
        }

        panel?.SetPauseTipsFunctionsSwapped(false);
        SetPauseTipsClickBlocked(false);
    }

    private void RaiseActiveIconSorting(Level111UiGestureHandle handle)
    {
        ClearActiveIconSorting();
        if (handle == null)
            return;

        activeIconDragCanvas = handle.GetComponent<Canvas>();
        activeIconDragCanvasAdded = activeIconDragCanvas == null;
        if (activeIconDragCanvasAdded)
            activeIconDragCanvas = handle.gameObject.AddComponent<Canvas>();

        activeIconCanvasOriginalOverrideSorting = activeIconDragCanvas.overrideSorting;
        activeIconCanvasOriginalSortingOrder = activeIconDragCanvas.sortingOrder;
        activeIconDragCanvas.overrideSorting = true;
        activeIconDragCanvas.sortingOrder = short.MaxValue;
    }

    private void ClearActiveIconSorting()
    {
        if (activeIconDragCanvas == null)
            return;

        if (activeIconDragCanvasAdded)
        {
            activeIconDragCanvas.overrideSorting = false;
            UnityEngine.Object.Destroy(activeIconDragCanvas);
        }
        else
        {
            activeIconDragCanvas.overrideSorting = activeIconCanvasOriginalOverrideSorting;
            activeIconDragCanvas.sortingOrder = activeIconCanvasOriginalSortingOrder;
        }

        activeIconDragCanvas = null;
        activeIconDragCanvasAdded = false;
    }

    private void SetPauseTipsClickBlocked(bool blocked)
    {
        if (pauseCustomButton != null)
            pauseCustomButton.IsBlockClick = blocked;
        if (tipsCustomButton != null)
            tipsCustomButton.IsBlockClick = blocked;
    }

    private void SetShopClickBlocked(bool blocked)
    {
        if (shopButton != null && shopButton.TryGetComponent(out CustomButton shopCustomButton))
            shopCustomButton.IsBlockClick = blocked;
    }

    private float PointerAngle(Vector2 pointerPosition, Vector3 targetWorldPosition)
    {
        Vector2 center = RectTransformUtility.WorldToScreenPoint(
            canvas != null ? canvas.worldCamera : null,
            targetWorldPosition);
        return Mathf.Atan2(pointerPosition.y - center.y, pointerPosition.x - center.x) * Mathf.Rad2Deg;
    }
}

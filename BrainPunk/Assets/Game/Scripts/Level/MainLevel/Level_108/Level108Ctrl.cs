using System.Collections;
using DG.Tweening;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Level108 总控：负责门跳入水管、下箭头安装和角色下管通关。
/// </summary>
public sealed class Level108Ctrl : MonoBehaviour
{
    #region Inspector

    [Header("核心引用")]
    [Tooltip("公共关卡控制器；只复用出生和标准成功结算。")]
    [SerializeField] private BaseLevelCtrl levelCtrl;

    [Tooltip("本关角色。")]
    [SerializeField] private ActorCtrl actor;

    [Tooltip("Level20 复制来的门角色根节点；本关只复用它的 Spine 表现。")]
    [SerializeField] private Transform doorRoot;

    [Tooltip("门角色的 Spine 控制器。")]
    [SerializeField] private ActorSpineCtrl doorSpine;

    [Tooltip("门角色遗留刚体；初始化时关闭物理模拟，由本关 Tween 驱动。")]
    [SerializeField] private Rigidbody2D doorBody;

    [Tooltip("门角色整体排序组，用于跳起后切到水管下层。")]
    [SerializeField] private SortingGroup doorSortingGroup;

    [Tooltip("第一个可调触发点：角色走到该点后，门只站起来。")]
    [SerializeField] private Transform doorStandPoint;

    [Tooltip("第二个可调触发点：门已经站起且角色走到该点后，门才跳进水管。")]
    [SerializeField] private Transform doorJumpPoint;

    [Tooltip("绿色水管的实体碰撞体，同时用于计算管口和站立判定。")]
    [SerializeField] private BoxCollider2D pipeCollider;

    [Tooltip("绿色水管渲染器，用于确定进入水管后的前后层级。")]
    [SerializeField] private SpriteRenderer pipeRenderer;

    [Header("门进入水管")]
    [Tooltip("门从地面跳到最高点的高度。")]
    [SerializeField, Min(0.1f)] private float doorJumpHeight = 3f;

    [Tooltip("门以一条连续抛物线跳到管口的总时长。")]
    [SerializeField, Min(0.1f)] private float doorJumpDuration = 0.78f;

    [Tooltip("门落到管口时，根节点相对管口顶面的高度。")]
    [SerializeField] private float doorPipeEntryYOffset = 0.95f;

    [Tooltip("门顺着水管继续下沉的时长。")]
    [SerializeField, Min(0.05f)] private float doorSinkDuration = 0.55f;

    [Tooltip("门从管口向管体内部下沉的距离；到位后立即隐藏，避免从管底漏出。")]
    [SerializeField, Min(0.1f)] private float doorSinkDepth = 1.2f;

    [Tooltip("门进入水管时缩放到初始尺寸的比例，并在同一阶段渐隐。")]
    [SerializeField, Range(0.1f, 1f)] private float doorInsidePipeScaleRatio = 0.6f;

    [Tooltip("门跳跃进度达到该比例后才开始缩小渐隐，避免刚越过最高点便消失。")]
    [SerializeField, Range(0.5f, 0.95f)] private float doorVanishStartNormalized = 0.75f;

    [Tooltip("门起跳阶段的渲染顺序。")]
    [SerializeField] private int doorAirborneSortingOrder = 240;

    [Tooltip("门越过最高点后切换到的水管下方渲染顺序。")]
    [SerializeField] private int doorInsidePipeSortingOrder = 150;

    [Header("角色进入水管")]
    [Tooltip("角色脚底与管口顶面的最大允许误差。")]
    [SerializeField, Min(0.01f)] private float actorPipeStandingTolerance = 0.28f;

    [Tooltip("按下下箭头后，角色水平对齐管口中心的时长。")]
    [SerializeField, Min(0.01f)] private float actorAlignDuration = 0.16f;

    [Tooltip("角色顺着水管下沉的时长。")]
    [SerializeField, Min(0.05f)] private float actorSinkDuration = 1f;

    [Tooltip("角色进入管体的最小下沉距离；运行时会按当前可见渲染范围自动延长到完全进入管口。")]
    [SerializeField, Min(0.1f)] private float actorSinkDepth = 1.15f;

    [Tooltip("角色进入水管后使用的渲染顺序。")]
    [SerializeField] private int actorInsidePipeSortingOrder = 150;

    [Header("按钮绑定")]
    [Tooltip("等待公共 GamePlayPanel 生成时的最大尝试次数。")]
    [SerializeField, Min(1)] private int jumpButtonResolveAttempts = 30;

    [Tooltip("每次查找公共跳跃按钮之间的实时时间间隔。")]
    [SerializeField, Min(0.01f)] private float jumpButtonResolveInterval = 0.1f;

    #endregion

    #region Runtime State

    private Tween doorSequence;
    private Tween actorSinkSequence;
    private Collider2D actorBodyCollider;
    private Collider2D[] actorColliders;
    private SortingGroup actorSortingGroup;
    private Vector3 doorInitialLocalScale;
    private bool hasDoorInitialLocalScale;
    private GameObject actorShadow;
    private bool actorShadowInitialActive;
    private bool hasActorShadowState;
    private bool actorShadowHiddenByLevel;
    private GameObject doorShadow;

    private RectTransform jumpButtonRect;
    private Canvas jumpButtonCanvas;
    private CustomButton jumpButton;
    private bool jumpButtonInitialEnabled;
    private bool jumpButtonInitialInteractable;
    private bool hasJumpButtonState;

    private Image jumpButtonIcon;
    private Sprite jumpIconInitialSprite;
    private Quaternion jumpIconInitialRotation;
    private Vector2 jumpIconInitialPosition;
    private Vector2 jumpIconInitialSizeDelta;
    private bool jumpIconInitialPreserveAspect;
    private Color jumpIconInitialColor;
    private bool hasJumpIconState;

    private bool doorHasStoodUp;
    private bool doorJumpStarted;
    private bool downArrowInstalled;
    private bool actorEnteringPipe;
    private bool isCleaningUp;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        PrepareDoorSpineOnly();
        CacheActorState();
    }

    private IEnumerator Start()
    {
        for (int attempt = 0; attempt < Mathf.Max(1, jumpButtonResolveAttempts); attempt++)
        {
            if (TryResolveJumpButton())
                yield break;

            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, jumpButtonResolveInterval));
        }

        Debug.LogWarning("[Level108Ctrl] 未找到 Root/jump_btn，下箭头暂时无法安装。", this);
    }

    private void Update()
    {
        bool isPlaying = IsGamePlaying();
        SyncTweenPauseState(doorSequence, isPlaying);
        SyncTweenPauseState(actorSinkSequence, isPlaying);

        if (!isPlaying || isCleaningUp)
            return;

        if (!doorHasStoodUp && HasActorReachedPoint(doorStandPoint))
        {
            StandDoorUp();
        }

        if (doorHasStoodUp && !doorJumpStarted && HasActorReachedPoint(doorJumpPoint))
        {
            PlayDoorPipeSequence();
        }

        if (!actorEnteringPipe)
            SetActorShadowHidden(IsActorStandingOnPipe());

        if (downArrowInstalled)
            HandleInstalledDownButtonInput();
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

    #region Door Sequence

    private void PrepareDoorSpineOnly()
    {
        if (doorBody != null)
        {
            doorBody.linearVelocity = Vector2.zero;
            doorBody.angularVelocity = 0f;
            doorBody.simulated = false;
        }

        if (doorRoot != null)
        {
            doorInitialLocalScale = doorRoot.localScale;
            hasDoorInitialLocalScale = true;

            Collider2D[] colliders = doorRoot.GetComponents<Collider2D>();
            for (int index = 0; index < colliders.Length; index++)
                colliders[index].enabled = false;
        }

        Transform doorShadowTransform = FindDescendant(doorRoot, "Shadow");
        doorShadow = doorShadowTransform != null ? doorShadowTransform.gameObject : null;
        SetDoorShadowVisible(false);
        doorSpine?.SetAlpha(1f);
        doorSpine?.PlayAnimation("idle", true, false);
    }

    private void StandDoorUp()
    {
        if (doorHasStoodUp || doorRoot == null)
            return;

        doorHasStoodUp = true;
        doorSpine?.PlayAnimation("idle01", true, false);
        SetDoorShadowVisible(true);
        AudioManager.PlaySound("doorStandup");
    }

    private void PlayDoorPipeSequence()
    {
        if (doorJumpStarted || doorRoot == null || pipeCollider == null)
            return;

        doorJumpStarted = true;
        doorSortingGroup ??= doorRoot.GetComponent<SortingGroup>();
        SetSortingRelativeToPipe(doorSortingGroup, doorAirborneSortingOrder);

        Vector3 startPosition = doorRoot.position;
        Bounds pipeBounds = pipeCollider.bounds;
        Vector3 pipeEntryPosition = new Vector3(
            pipeBounds.center.x,
            pipeBounds.max.y + doorPipeEntryYOffset,
            startPosition.z);
        float hiddenY = pipeEntryPosition.y - Mathf.Max(0.1f, doorSinkDepth);
        float jumpDuration = Mathf.Max(0.1f, doorJumpDuration);
        float jumpApexTime = jumpDuration * 0.5f;
        float sinkDuration = Mathf.Max(0.05f, doorSinkDuration);
        float vanishStartTime = jumpDuration *
                                Mathf.Clamp(doorVanishStartNormalized, 0.5f, 0.95f);
        float vanishDuration = jumpDuration + sinkDuration - vanishStartTime;

        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.AppendCallback(() =>
        {
            doorSpine?.PlayAnimation("jump_up", false, false);
            AudioManager.PlaySound("jump");
        });
        sequence.Append(doorRoot.DOJump(
                pipeEntryPosition,
                Mathf.Max(0.1f, doorJumpHeight),
                1,
                jumpDuration)
            .SetEase(Ease.Linear));
        sequence.InsertCallback(jumpApexTime, () =>
        {
            doorSpine?.PlayAnimation("jump_down", false, false);
        });

        sequence.AppendCallback(() =>
        {
            SetSortingRelativeToPipe(doorSortingGroup, doorInsidePipeSortingOrder);
        });
        sequence.Append(doorRoot.DOMoveY(hiddenY, sinkDuration)
            .SetEase(Ease.InSine));

        sequence.InsertCallback(vanishStartTime, () => SetDoorShadowVisible(false));

        if (hasDoorInitialLocalScale)
        {
            Vector3 hiddenScale = doorInitialLocalScale *
                                  Mathf.Clamp(doorInsidePipeScaleRatio, 0.1f, 1f);
            sequence.Insert(vanishStartTime, doorRoot.DOScale(hiddenScale, vanishDuration)
                .SetEase(Ease.InSine));
        }

        if (doorSpine != null)
        {
            float doorAlpha = 1f;
            sequence.Insert(vanishStartTime, DOTween.To(
                    () => doorAlpha,
                    value =>
                    {
                        doorAlpha = value;
                        doorSpine.SetAlpha(value);
                    },
                    0f,
                    vanishDuration)
                .SetEase(Ease.InSine));
        }

        sequence.OnComplete(() =>
        {
            doorSequence = null;
            if (doorRoot != null)
                doorRoot.gameObject.SetActive(false);
        });
        doorSequence = sequence;
    }

    #endregion

    #region Arrow And Button

    /// <summary>
    /// 尝试把场景下箭头安装到公共跳跃按钮。
    /// 调用边界：仅由本关的 Level108DownArrowDrag 在拖拽松手时调用。
    /// 注意事项：只有箭头中心落在 jump_btn 矩形内才会成功；失败时由拖拽组件复位。
    /// </summary>
    public bool TryInstallDownArrow(Level108DownArrowDrag source)
    {
        if (source == null || downArrowInstalled || !IsGamePlaying())
            return false;

        if (!TryResolveJumpButton())
            return false;

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
            return false;

        Vector2 arrowScreenPoint = worldCamera.WorldToScreenPoint(source.transform.position);
        if (!IsJumpButtonScreenPoint(arrowScreenPoint))
            return false;

        downArrowInstalled = true;
        ApplyInstalledDownButtonVisual(source.ArrowSprite, source.ArrowScreenSize);
        DisableSharedJumpButton();
        source.CompleteInstallation();
        return true;
    }

    private bool TryResolveJumpButton()
    {
        if (jumpButtonRect != null)
            return true;

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
        if (jumpButton == null)
            jumpButton = jumpButtonRect.GetComponentInChildren<CustomButton>(true);

        // 复用面板显式绑定的箭头图标，避免把 bg 背景当作替换目标。
        jumpButtonIcon = PlayCtrl.Instance.CurrentGamePlayPanel.JumpButtonIcon;

        CacheJumpButtonState();
        return true;
    }

    private void CacheJumpButtonState()
    {
        if (!hasJumpButtonState && jumpButton != null)
        {
            jumpButtonInitialEnabled = jumpButton.enabled;
            jumpButtonInitialInteractable = jumpButton.Interactable;
            hasJumpButtonState = true;
        }

        if (!hasJumpIconState && jumpButtonIcon != null)
        {
            RectTransform iconRect = jumpButtonIcon.rectTransform;
            jumpIconInitialSprite = jumpButtonIcon.sprite;
            jumpIconInitialRotation = iconRect.localRotation;
            jumpIconInitialPosition = iconRect.anchoredPosition;
            jumpIconInitialSizeDelta = iconRect.sizeDelta;
            jumpIconInitialPreserveAspect = jumpButtonIcon.preserveAspect;
            jumpIconInitialColor = jumpButtonIcon.color;
            hasJumpIconState = true;
        }
    }

    private void ApplyInstalledDownButtonVisual(Sprite downSprite, Vector2 sourceScreenSize)
    {
        if (jumpButtonIcon == null || downSprite == null)
            return;

        RectTransform iconRect = jumpButtonIcon.rectTransform;
        jumpButtonIcon.sprite = downSprite;
        jumpButtonIcon.preserveAspect = false;
        jumpButtonIcon.color = Color.white;
        iconRect.localRotation = Quaternion.identity;
        iconRect.anchoredPosition = Vector2.zero;

        if (TryConvertScreenSizeToIconLocalSize(iconRect, sourceScreenSize, out Vector2 localSize))
            iconRect.sizeDelta = localSize;
    }

    private bool TryConvertScreenSizeToIconLocalSize(
        RectTransform iconRect,
        Vector2 screenSize,
        out Vector2 localSize)
    {
        localSize = iconRect != null ? iconRect.sizeDelta : Vector2.zero;
        if (iconRect == null || iconRect.parent == null || screenSize.x <= 0f || screenSize.y <= 0f)
            return false;

        Camera eventCamera = null;
        if (jumpButtonCanvas != null && jumpButtonCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = jumpButtonCanvas.worldCamera;

        Transform parent = iconRect.parent;
        Vector2 origin = RectTransformUtility.WorldToScreenPoint(
            eventCamera,
            parent.TransformPoint(Vector3.zero));
        Vector2 oneUnitRight = RectTransformUtility.WorldToScreenPoint(
            eventCamera,
            parent.TransformPoint(Vector3.right));
        Vector2 oneUnitUp = RectTransformUtility.WorldToScreenPoint(
            eventCamera,
            parent.TransformPoint(Vector3.up));
        float pixelsPerLocalX = Vector2.Distance(origin, oneUnitRight);
        float pixelsPerLocalY = Vector2.Distance(origin, oneUnitUp);
        if (pixelsPerLocalX <= 0.001f || pixelsPerLocalY <= 0.001f)
            return false;

        localSize = new Vector2(
            screenSize.x / pixelsPerLocalX,
            screenSize.y / pixelsPerLocalY);
        return true;
    }

    private void DisableSharedJumpButton()
    {
        if (jumpButton == null)
            return;

        jumpButton.Interactable = false;
        jumpButton.enabled = false;
    }

    private void HandleInstalledDownButtonInput()
    {
#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0) && IsJumpButtonScreenPoint(Input.mousePosition))
            TryBeginActorPipeSink();
#elif UNITY_ANDROID || UNITY_IOS
        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (touch.phase == TouchPhase.Began && IsJumpButtonScreenPoint(touch.position))
            {
                TryBeginActorPipeSink();
                break;
            }
        }
#else
        if (Input.GetMouseButtonDown(0) && IsJumpButtonScreenPoint(Input.mousePosition))
            TryBeginActorPipeSink();
#endif
    }

    private bool IsJumpButtonScreenPoint(Vector2 screenPoint)
    {
        if (jumpButtonRect == null)
            return false;

        Camera eventCamera = null;
        if (jumpButtonCanvas != null && jumpButtonCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = jumpButtonCanvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(jumpButtonRect, screenPoint, eventCamera);
    }

    private void RestoreJumpButtonState()
    {
        if (jumpButton != null && hasJumpButtonState)
        {
            jumpButton.Interactable = jumpButtonInitialInteractable;
            jumpButton.enabled = jumpButtonInitialEnabled;
        }

        if (jumpButtonIcon != null && hasJumpIconState)
        {
            RectTransform iconRect = jumpButtonIcon.rectTransform;
            jumpButtonIcon.sprite = jumpIconInitialSprite;
            jumpButtonIcon.preserveAspect = jumpIconInitialPreserveAspect;
            jumpButtonIcon.color = jumpIconInitialColor;
            iconRect.localRotation = jumpIconInitialRotation;
            iconRect.anchoredPosition = jumpIconInitialPosition;
            iconRect.sizeDelta = jumpIconInitialSizeDelta;
        }
    }

    #endregion

    #region Actor Pipe Entry

    private void CacheActorState()
    {
        if (actor == null)
            return;

        actorSortingGroup = actor.GetComponent<SortingGroup>();
        actorColliders = actor.GetComponentsInChildren<Collider2D>(true);
        Rigidbody2D actorBody = actor.RB;
        for (int index = 0; index < actorColliders.Length; index++)
        {
            Collider2D candidate = actorColliders[index];
            if (candidate == null || candidate.isTrigger)
                continue;

            if (actorBody == null || candidate.attachedRigidbody == actorBody)
            {
                actorBodyCollider = candidate;
                break;
            }
        }

        Transform shadowTransform = FindDescendant(actor.transform, "Shadow");
        if (shadowTransform != null)
        {
            actorShadow = shadowTransform.gameObject;
            actorShadowInitialActive = actorShadow.activeSelf;
            hasActorShadowState = true;
        }
    }

    private bool IsActorStandingOnPipe()
    {
        if (actor == null || pipeCollider == null || actorBodyCollider == null ||
            !pipeCollider.enabled || !actorBodyCollider.enabled)
        {
            return false;
        }

        Bounds pipeBounds = pipeCollider.bounds;
        Bounds actorBounds = actorBodyCollider.bounds;
        bool horizontallyInside = actorBounds.center.x >= pipeBounds.min.x &&
                                  actorBounds.center.x <= pipeBounds.max.x;
        bool feetAtPipeTop = Mathf.Abs(actorBounds.min.y - pipeBounds.max.y) <=
                             Mathf.Max(0.01f, actorPipeStandingTolerance);
        bool notRising = actor.RB == null || actor.RB.linearVelocityY <= 0.25f;
        return horizontallyInside && feetAtPipeTop && notRising;
    }

    /// <summary>
    /// 计算角色完全进入管口所需的下落距离。
    /// 以当前启用的可见 Renderer 顶边为准，确保动画结束时角色顶部已经越过管口。
    /// </summary>
    private float CalculateActorSinkDistance(Bounds pipeBounds)
    {
        float sinkDistance = Mathf.Max(0.1f, actorSinkDepth);
        if (actor == null)
            return sinkDistance;

        Renderer[] renderers = actor.GetComponentsInChildren<Renderer>(false);
        float actorVisualTop = float.NegativeInfinity;
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            actorVisualTop = Mathf.Max(actorVisualTop, renderer.bounds.max.y);
        }

        if (!float.IsNegativeInfinity(actorVisualTop))
        {
            float fullEntryDistance = actorVisualTop - pipeBounds.max.y + 0.01f;
            sinkDistance = Mathf.Max(sinkDistance, fullEntryDistance);
        }

        return sinkDistance;
    }

    private void TryBeginActorPipeSink()
    {
        if (actorEnteringPipe || !downArrowInstalled || !IsActorStandingOnPipe())
            return;

        actorEnteringPipe = true;
        levelCtrl?.SetCtrlable(false);
        actor.StopMoving();
        SetActorShadowHidden(true);

        if (actor.RB != null)
        {
            actor.RB.linearVelocity = Vector2.zero;
            actor.RB.angularVelocity = 0f;
            actor.RB.simulated = false;
        }

        if (actorColliders != null)
        {
            for (int index = 0; index < actorColliders.Length; index++)
            {
                if (actorColliders[index] != null)
                    actorColliders[index].enabled = false;
            }
        }

        SetSortingRelativeToPipe(actorSortingGroup, actorInsidePipeSortingOrder);

        actor.SpineCtrl?.PlayAnimation("jump_down", false, false);

        Bounds pipeBounds = pipeCollider.bounds;
        float sinkDistance = CalculateActorSinkDistance(pipeBounds);
        float hiddenY = actor.transform.position.y - sinkDistance;
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Append(actor.transform.DOMoveY(hiddenY, Mathf.Max(0.05f, actorSinkDuration))
            .SetEase(Ease.InSine));
        sequence.Join(actor.transform.DOMoveX(pipeBounds.center.x, Mathf.Max(0.01f, actorAlignDuration))
            .SetEase(Ease.OutSine));
        sequence.OnComplete(CompleteActorPipeEntry);
        actorSinkSequence = sequence;
    }

    private void CompleteActorPipeEntry()
    {
        actorSinkSequence = null;
        if (actor != null)
            actor.gameObject.SetActive(false);

        if (levelCtrl == null)
            return;

        levelCtrl.SetSuccessAnim(null);
        levelCtrl.SuccessStrightly();
    }

    private void SetActorShadowHidden(bool hidden)
    {
        if (!hasActorShadowState || actorShadow == null)
            return;

        if (hidden)
        {
            if (!actorShadowHiddenByLevel && actorShadow.activeSelf)
            {
                actorShadow.SetActive(false);
                actorShadowHiddenByLevel = true;
            }

            return;
        }

        if (!actorShadowHiddenByLevel)
            return;

        actorShadow.SetActive(actorShadowInitialActive);
        actorShadowHiddenByLevel = false;
    }

    #endregion

    #region Helpers And Cleanup

    private void ResolveReferences()
    {
        levelCtrl ??= GetComponent<BaseLevelCtrl>();
        actor ??= levelCtrl != null ? levelCtrl.Actor : null;
        doorSpine ??= doorRoot != null ? doorRoot.GetComponentInChildren<ActorSpineCtrl>(true) : null;
        doorBody ??= doorRoot != null ? doorRoot.GetComponent<Rigidbody2D>() : null;
        doorSortingGroup ??= doorRoot != null ? doorRoot.GetComponent<SortingGroup>() : null;
        pipeRenderer ??= pipeCollider != null ? pipeCollider.GetComponent<SpriteRenderer>() : null;
    }

    private bool HasActorReachedPoint(Transform point)
    {
        return actor != null && point != null && actor.transform.position.x >= point.position.x;
    }

    private void SetSortingRelativeToPipe(SortingGroup sortingGroup, int sortingOrder)
    {
        if (sortingGroup == null)
            return;

        if (pipeRenderer != null)
            sortingGroup.sortingLayerID = pipeRenderer.sortingLayerID;

        sortingGroup.sortingOrder = sortingOrder;
    }

    private void SetDoorShadowVisible(bool visible)
    {
        if (doorShadow != null && doorShadow.activeSelf != visible)
            doorShadow.SetActive(visible);
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        if (root == null)
            return null;

        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);
            if (child.name == targetName)
                return child;

            Transform nested = FindDescendant(child, targetName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static void SyncTweenPauseState(Tween tween, bool shouldPlay)
    {
        if (tween == null || !tween.IsActive())
            return;

        if (shouldPlay)
        {
            if (!tween.IsPlaying())
                tween.Play();
        }
        else if (tween.IsPlaying())
        {
            tween.Pause();
        }
    }

    private static bool IsGamePlaying()
    {
        return PlayCtrl.Instance != null && PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
    }

    private void CleanupRuntimeState()
    {
        if (isCleaningUp)
            return;

        isCleaningUp = true;
        doorSequence?.Kill();
        actorSinkSequence?.Kill();
        doorSequence = null;
        actorSinkSequence = null;
        RestoreJumpButtonState();
        SetActorShadowHidden(false);

        if (doorRoot != null && hasDoorInitialLocalScale)
            doorRoot.localScale = doorInitialLocalScale;

        doorSpine?.SetAlpha(1f);
    }

    #endregion
}

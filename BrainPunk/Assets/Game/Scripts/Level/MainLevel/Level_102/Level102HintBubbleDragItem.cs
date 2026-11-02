using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Level102 物理提示气泡的拖拽适配器。
/// Tips 的显示门槛仍由现有全局提示计时/失败次数逻辑触发；开始拖动后只通知本关控制器锁存气泡。
/// </summary>
public sealed class Level102HintBubbleDragItem : DragableItem,
    IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerClickHandler
{
    #region Inspector

    [Tooltip("Level102 局部控制器；为空时从父级查找。")]
    [SerializeField] private Level102Ctrl levelCtrl;

    [Tooltip("是否在本组件被启用时自动开启拖拽；通常由关卡控制器在提示出现后控制。")]
    [SerializeField] private bool enableDragOnAwake = false;

    [Tooltip("warningBubble 的真实可视矩形；通常绑定 Image 子节点，避免根锚点扩大命中区。")]
    [SerializeField] private RectTransform interactionRect;

    [Tooltip("实际被移动的 warningBubble 根节点；组件可挂在 Image 上接收射线，但拖动整个气泡。")]
    [SerializeField] private RectTransform dragTarget;

    [Tooltip("拖拽时使用的 UI 平面；通常绑定 warningBubble 所在的 World Space Canvas。")]
    [SerializeField] private RectTransform dragPlane;

    #endregion

    #region Runtime State

    private bool hasNotifiedDrag;
    private CustomButton parentTipsButton;
    private bool parentTipsButtonBlockApplied;
    private bool parentTipsButtonWasBlocked;
    private Coroutine releaseParentTipsButtonRoutine;
    private int parentTipsButtonBlockVersion;
    private bool pointerDragEnabled;
    private bool pointerDragging;
    private Vector2 pointerDragOffset;

    /// <summary>当前提示气泡是否已经开始过拖动。</summary>
    public bool HasNotifiedDrag => hasNotifiedDrag;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveLevelCtrl();
        // warningBubble 是可射线检测的 World Space UI；使用 EventSystem 原生
        // 指针事件拖动。关闭 DragableItem 的轮询输入，避免同一帧写入两套坐标。
        SetEnableDrag(false);
        pointerDragEnabled = enableDragOnAwake && levelCtrl != null && levelCtrl.CanAcceptGameplayInput;
    }

    #endregion

    #region UI Pointer Drag

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!pointerDragEnabled || eventData == null ||
            !IsTouchOnObject(eventData.position))
            return;

        RectTransform bubbleRect = ResolveDragTargetRect();
        RectTransform parentRect = bubbleRect != null ? bubbleRect.parent as RectTransform : null;
        if (bubbleRect == null || parentRect == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                eventData.pressEventCamera ?? ResolveCanvasCamera(),
                out Vector2 pointerLocalPosition))
            return;

        ResolveLevelCtrl();
        if (levelCtrl == null || !levelCtrl.TryBeginBubbleDrag(this))
            return;

        pointerDragging = true;
        hasNotifiedDrag = true;
        pointerDragOffset = bubbleRect.anchoredPosition - pointerLocalPosition;
        BeginParentTipsButtonBlock();
        eventData.Use();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!pointerDragging || eventData == null)
            return;

        RectTransform bubbleRect = ResolveDragTargetRect();
        RectTransform parentRect = bubbleRect != null ? bubbleRect.parent as RectTransform : null;
        if (bubbleRect == null || parentRect == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                eventData.pressEventCamera ?? ResolveCanvasCamera(),
                out Vector2 pointerLocalPosition))
            return;

        bubbleRect.anchoredPosition = pointerLocalPosition + pointerDragOffset;
        eventData.Use();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pointerDragging)
            return;

        pointerDragging = false;
        ScheduleParentTipsButtonRelease();
        eventData?.Use();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // warningBubble 自身是可拖动物，不能把同一次点击继续传给父 tips_btn，
        // 否则父按钮会派发隐藏事件并让气泡看起来“无法拖动”。
        eventData?.Use();
    }

    #endregion

    #region Drag Callbacks

    /// <summary>
    /// warningBubble 的根 RectTransform 在面板中只是定位锚点；真正可见的 Image
    /// 子节点才是玩家应当按下的区域。绑定失败时才回退到共享 Collider 命中。
    /// </summary>
    protected override bool IsTouchOnObject(Vector2 screenPosition)
    {
        Camera eventCamera = ResolveCanvasCamera();
        RectTransform visualRoot = interactionRect;
        if (visualRoot != null)
        {
            RectTransform[] visuals = visualRoot.GetComponentsInChildren<RectTransform>(true);
            for (int index = 0; index < visuals.Length; index++)
            {
                RectTransform visual = visuals[index];
                if (visual == null || !visual.gameObject.activeInHierarchy)
                    continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(visual, screenPosition, eventCamera))
                    return true;
            }

            // 已经绑定真实 Image 时不要再用根节点的 fallbackPickRadius，
            // 否则玩家点到视觉外的锚点也会开始拖动。
            return false;
        }

        // 未提供 UI 视觉矩形的兼容面板，继续使用共享 Collider/半径判定。
        return base.IsTouchOnObject(screenPosition);
    }

    protected override bool IsMouseOnObject(Vector2 screenPosition)
    {
        return IsTouchOnObject(screenPosition);
    }

    protected override Vector3 ScreenToWorldPosition(Vector2 screenPosition)
    {
        RectTransform rectTransform = dragPlane != null
            ? dragPlane
            : GetComponentInParent<Canvas>()?.transform as RectTransform;
        Camera eventCamera = ResolveCanvasCamera();
        if (rectTransform != null &&
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                rectTransform,
                screenPosition,
                eventCamera,
                out Vector3 worldPosition))
        {
            return worldPosition;
        }

        return base.ScreenToWorldPosition(screenPosition);
    }

    protected override void OnDragStart()
    {
        ResolveLevelCtrl();
        if (levelCtrl == null)
        {
            SetEnableDrag(false);
            return;
        }

        // 先由控制器验证当前实例、游戏状态和活动气泡，再锁存“已被玩家接管”。
        // DragableItem 已在调用虚方法前设置了内部 dragging 标记；拒绝时关闭
        // 自身输入会让共享组件回滚这次半开始的拖拽。
        if (!levelCtrl.TryBeginBubbleDrag(this))
        {
            SetEnableDrag(false);
            return;
        }

        hasNotifiedDrag = true;
        BeginParentTipsButtonBlock();
        base.OnDragStart();
    }

    protected override void OnDragEnd()
    {
        base.OnDragEnd();
        ScheduleParentTipsButtonRelease();
    }

    #endregion

    #region UI References

    private Camera ResolveCanvasCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera != null ? canvas.worldCamera : InputCamera;
    }

    private RectTransform ResolveDragTargetRect()
    {
        if (dragTarget != null)
            return dragTarget;

        return transform as RectTransform;
    }

    #endregion

    #region Public API

    /// <summary>
    /// 重置本次尝试的拖动锁存，并按指定值恢复输入。
    /// 调用边界：仅由 Level102Ctrl 在同一实例复位/重新显示气泡时调用。
    /// </summary>
    public void ResetForAttempt(bool enableDrag)
    {
        hasNotifiedDrag = false;
        pointerDragging = false;
        ResolveLevelCtrl();
        SetBubbleDragEnabled(enableDrag && levelCtrl != null && levelCtrl.CanAcceptGameplayInput);
    }

    /// <summary>
    /// 在气泡由计时逻辑显示后开启拖拽。
    /// </summary>
    public void EnableBubbleDrag()
    {
        ResolveLevelCtrl();
        SetBubbleDragEnabled(levelCtrl != null && levelCtrl.CanAcceptGameplayInput);
    }

    /// <summary>
    /// 关闭气泡输入但保留其 Collider，供控制器在暂停或结算阶段使用。
    /// </summary>
    public void DisableBubbleDrag()
    {
        pointerDragging = false;
        pointerDragEnabled = false;
        SetEnableDrag(false);
        ScheduleParentTipsButtonRelease();
    }

    public void SetBubbleDragEnabled(bool enable)
    {
        pointerDragEnabled = enable;
        SetEnableDrag(false);
        if (!enable && pointerDragging)
        {
            pointerDragging = false;
            ScheduleParentTipsButtonRelease();
        }
    }

    /// <summary>
    /// 显式绑定本关控制器，避免气泡父节点改变后无法通过层级查找控制器。
    /// 调用边界：仅由 Level102Ctrl 在运行时创建并配置气泡时调用。
    /// </summary>
    public void BindController(Level102Ctrl controller)
    {
        levelCtrl = controller;
    }

    /// <summary>
    /// 绑定真实可视区域。调用边界：仅由 Level102Ctrl 配置 warningBubble 时调用。
    /// </summary>
    public void BindInteractionRect(RectTransform visualRect)
    {
        interactionRect = visualRect;
    }

    /// <summary>
    /// 绑定实际移动的气泡根节点。组件挂在可射线检测的 Image 上时仍保持整泡移动。
    /// </summary>
    public void BindDragTarget(RectTransform targetRect)
    {
        dragTarget = targetRect;
    }

    /// <summary>
    /// 绑定拖拽投影平面。调用边界：仅由 Level102Ctrl 配置 World Space UI 时调用。
    /// </summary>
    public void BindDragPlane(RectTransform canvasRect)
    {
        dragPlane = canvasRect;
    }

    #endregion

    #region References

    private void ResolveLevelCtrl()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponentInParent<Level102Ctrl>();
    }

    private void BeginParentTipsButtonBlock()
    {
        if (parentTipsButton == null)
            parentTipsButton = GetComponentInParent<CustomButton>(true);

        if (parentTipsButton == null)
            return;

        if (releaseParentTipsButtonRoutine != null)
        {
            StopCoroutine(releaseParentTipsButtonRoutine);
            releaseParentTipsButtonRoutine = null;
        }

        parentTipsButtonBlockVersion++;
        if (!parentTipsButtonBlockApplied)
        {
            parentTipsButtonWasBlocked = parentTipsButton.IsBlockClick;
            parentTipsButtonBlockApplied = true;
        }

        // warningBubble is a child of tips_btn. A pointer released after dragging
        // would otherwise bubble to the parent and open the Tips panel instead of
        // leaving the physics bubble under player control.
        parentTipsButton.IsBlockClick = true;
    }

    private void ScheduleParentTipsButtonRelease()
    {
        if (!parentTipsButtonBlockApplied)
            return;

        if (releaseParentTipsButtonRoutine != null)
            StopCoroutine(releaseParentTipsButtonRoutine);

        int version = parentTipsButtonBlockVersion;
        releaseParentTipsButtonRoutine = StartCoroutine(ReleaseParentTipsButtonNextFrame(version));
    }

    private IEnumerator ReleaseParentTipsButtonNextFrame(int version)
    {
        yield return null;

        releaseParentTipsButtonRoutine = null;
        if (version != parentTipsButtonBlockVersion || IsDragging())
            yield break;

        if (parentTipsButton != null)
            parentTipsButton.IsBlockClick = parentTipsButtonWasBlocked;
        parentTipsButtonBlockApplied = false;
    }

    private void OnDisable()
    {
        if (releaseParentTipsButtonRoutine != null)
            StopCoroutine(releaseParentTipsButtonRoutine);

        releaseParentTipsButtonRoutine = null;
        parentTipsButtonBlockVersion++;
        pointerDragging = false;
        pointerDragEnabled = false;
        if (parentTipsButtonBlockApplied && parentTipsButton != null)
            parentTipsButton.IsBlockClick = parentTipsButtonWasBlocked;

        parentTipsButtonBlockApplied = false;
    }

    #endregion
}

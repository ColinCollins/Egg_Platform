using System;
using UnityEngine;

/// <summary>
/// Level075 专用的向上按钮面板控制器，只管理本关按钮的拖拽、点击和交互状态。
/// </summary>
public sealed class Level75GamePlayPanelCtrl : MonoBehaviour
{
    #region Constants

    private const string UpButtonPath = "Root/up_btn";
    private const int DraggingClickBlockFrame = int.MaxValue;

    #endregion

    #region Inspector

    [Tooltip("Level075 独立向上按钮；不得绑定共享 jump_btn。")]
    [SerializeField] private CustomButton upButton;

    [Tooltip("向上按钮的 UI 拖拽组件；拖拽范围由面板 Prefab 配置。")]
    [SerializeField] private UIDrag upButtonDrag;

    #endregion

    #region Runtime State

    private Action upClickHandler;
    private Action upDragEndHandler;
    private int clickBlockThroughFrame = -1;
    private bool dragLocked;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        SetInteractable(false);
    }

    private void Update()
    {
        if (clickBlockThroughFrame < 0 ||
            clickBlockThroughFrame == DraggingClickBlockFrame ||
            Time.frameCount <= clickBlockThroughFrame)
        {
            return;
        }

        clickBlockThroughFrame = -1;
        ApplyClickBlockState();
    }

    private void OnDisable()
    {
        Unbind();
        SetInteractable(false);
    }

    private void OnDestroy()
    {
        Unbind();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Level075 向上按钮的 RectTransform，仅供本关控制器执行目标区域命中判断。
    /// 引用缺失时返回 null，不会回退到共享 jump_btn。
    /// </summary>
    public RectTransform UpButtonRectTransform =>
        upButton != null ? upButton.transform as RectTransform : null;

    /// <summary>
    /// 绑定本关向上按钮点击回调，并把按钮恢复到 Prefab 初始位置和可交互状态。
    /// 仅允许 Level075 运行时控制器调用；重复绑定会先清理旧回调。
    /// </summary>
    /// <param name="onUpClicked">玩家完成一次独立点击后执行的本关回调。</param>
    /// <param name="onUpDragEnded">玩家结束拖拽后执行的本关吸附判定回调。</param>
    public void Bind(Action onUpClicked, Action onUpDragEnded)
    {
        Unbind();
        ResolveReferences();

        if (upButton == null || upButtonDrag == null)
        {
            Debug.LogWarning(
                "[Level75GamePlayPanelCtrl] up_btn 引用不完整，Level075 向上按钮保持不可交互。",
                this);
            SetInteractable(false);
            return;
        }

        upButton.gameObject.SetActive(true);
        dragLocked = false;
        upButtonDrag.ResetPosition();
        ResetClickBlock();

        upClickHandler = onUpClicked;
        upDragEndHandler = onUpDragEnded;
        upButton.OnClick += OnUpButtonClicked;
        upButtonDrag.OnDragBegin += OnUpButtonDragBegin;
        upButtonDrag.OnDragEnd += OnUpButtonDragEnd;
        SetInteractable(true);
    }

    /// <summary>
    /// 清理 Level075 向上按钮的点击与拖拽回调。
    /// 可在面板关闭、销毁或关卡控制器解绑时重复调用。
    /// </summary>
    public void Unbind()
    {
        if (upButton != null)
            upButton.OnClick -= OnUpButtonClicked;

        if (upButtonDrag != null)
        {
            upButtonDrag.OnDragBegin -= OnUpButtonDragBegin;
            upButtonDrag.OnDragEnd -= OnUpButtonDragEnd;
        }

        upClickHandler = null;
        upDragEndHandler = null;
        dragLocked = false;
        ResetClickBlock();
    }

    /// <summary>
    /// 切换 Level075 向上按钮的点击和拖拽能力。
    /// false 会同时关闭 CustomButton 与 UIDrag，避免结算期间继续移动按钮。
    /// </summary>
    /// <param name="interactable">是否允许玩家点击和拖拽向上按钮。</param>
    public void SetInteractable(bool interactable)
    {
        ResolveReferences();

        if (upButton != null)
            upButton.Interactable = interactable;

        if (upButtonDrag != null)
            upButtonDrag.enabled = interactable && !dragLocked;

        ApplyClickBlockState();
    }

    /// <summary>
    /// 获取向上按钮中心在当前根 Canvas 下的屏幕坐标。
    /// </summary>
    public bool TryGetUpButtonScreenPosition(out Vector2 screenPosition)
    {
        screenPosition = default;

        RectTransform buttonRect = UpButtonRectTransform;
        Canvas rootCanvas = ResolveRootCanvas();
        if (buttonRect == null || rootCanvas == null)
            return false;

        screenPosition = RectTransformUtility.WorldToScreenPoint(
            ResolveEventCamera(rootCanvas),
            buttonRect.position);
        return true;
    }

    /// <summary>
    /// 将向上按钮中心移动到指定屏幕坐标，用于吸附后跟随场景红框。
    /// </summary>
    public bool TrySetUpButtonScreenPosition(Vector2 screenPosition)
    {
        RectTransform buttonRect = UpButtonRectTransform;
        RectTransform parentRect = buttonRect != null ? buttonRect.parent as RectTransform : null;
        Canvas rootCanvas = ResolveRootCanvas();
        if (buttonRect == null || parentRect == null || rootCanvas == null)
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPosition,
                ResolveEventCamera(rootCanvas),
                out Vector2 parentLocalPoint))
        {
            return false;
        }

        Vector3 localPosition = buttonRect.localPosition;
        localPosition.x = parentLocalPoint.x;
        localPosition.y = parentLocalPoint.y;
        buttonRect.localPosition = localPosition;
        return true;
    }

    /// <summary>
    /// 锁定或解锁向上按钮拖拽；锁定不会关闭按钮点击。
    /// </summary>
    public void SetDragLocked(bool locked)
    {
        dragLocked = locked;
        if (upButtonDrag != null)
            upButtonDrag.enabled = !dragLocked && upButton != null && upButton.Interactable;
    }

    /// <summary>
    /// 投放未命中时把向上按钮恢复到本关面板起点。
    /// </summary>
    public void ResetUpButtonPosition()
    {
        if (!dragLocked && upButtonDrag != null)
            upButtonDrag.ResetPosition();
    }

    #endregion

    #region Input Handling

    private void OnUpButtonClicked(CustomButton _)
    {
        if (upButton == null || !upButton.Interactable || IsClickBlocked())
            return;

        upClickHandler?.Invoke();
    }

    private void OnUpButtonDragBegin()
    {
        clickBlockThroughFrame = DraggingClickBlockFrame;
        ApplyClickBlockState();
    }

    private void OnUpButtonDragEnd()
    {
        clickBlockThroughFrame = Time.frameCount;
        ApplyClickBlockState();
        upDragEndHandler?.Invoke();
    }

    private bool IsClickBlocked()
    {
        return clickBlockThroughFrame >= 0;
    }

    private void ResetClickBlock()
    {
        clickBlockThroughFrame = -1;
        ApplyClickBlockState();
    }

    private void ApplyClickBlockState()
    {
        if (upButton != null)
            upButton.IsBlockClick = !upButton.Interactable || IsClickBlocked();
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        if (upButton == null)
        {
            Transform buttonTransform = transform.Find(UpButtonPath);
            if (buttonTransform != null)
                upButton = buttonTransform.GetComponent<CustomButton>();
        }

        if (upButtonDrag == null && upButton != null)
            upButtonDrag = upButton.GetComponent<UIDrag>();
    }

    private Canvas ResolveRootCanvas()
    {
        if (upButton == null)
            return null;

        Canvas buttonCanvas = upButton.GetComponentInParent<Canvas>();
        return buttonCanvas != null ? buttonCanvas.rootCanvas : null;
    }

    private static Camera ResolveEventCamera(Canvas rootCanvas)
    {
        return rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;
    }

    #endregion
}

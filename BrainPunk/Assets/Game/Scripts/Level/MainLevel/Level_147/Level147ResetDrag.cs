using System.Collections;
using Game.Play;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>147 私有重来按钮：轻点仍重开，真正拖拽只搬动镜像箭头，使用后底板仍可点击。</summary>
public sealed class Level147ResetDrag : MonoBehaviour, IPointerDownHandler, IBeginDragHandler,
    IDragHandler, IEndDragHandler, IPointerUpHandler, ICancelHandler
{
    #region 显式绑定
    /// <summary>本组件直接挂在重来按钮上，引用真实内层 Canvas，避免相机和拖拽坐标错位。</summary>
    [SerializeField] private CustomButton resetButton;
    [SerializeField] private Image arrowImage;
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private Canvas panelCanvas;
    #endregion

    #region 本次指针与箭头状态
    private Level147Ctrl owner;
    private RectTransform arrow, originalParent;
    private Vector2 homePosition;
    private Vector3 homeScale;
    private int pointerId = int.MinValue, releaseFrame = -1;
    private PointerEventData pointerData;
    private Camera eventCamera;
    private bool dragging, recognized, consumed, absorbing;
    private Transform targetWheel;
    private float absorbElapsed;
    private Vector2 absorbStartScreen;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        arrow = arrowImage.rectTransform;
        originalParent = (RectTransform)arrow.parent;
        homePosition = arrow.anchoredPosition;
        homeScale = arrow.localScale;
    }

    private IEnumerator Start()
    {
        while (PlayCtrl.Instance == null || PlayCtrl.Instance.LevelCtrl == null) yield return null;
        // 当前关卡实例的公开入口，不跨场景查找节点，不给旧 HUD 绑定未来的新局。
        owner = PlayCtrl.Instance.LevelCtrl.GetComponent<Level147Ctrl>();
    }

    private void Update()
    {
        if (releaseFrame >= 0 && Time.frameCount > releaseFrame)
        {
            resetButton.IsBlockClick = false;
            releaseFrame = -1;
        }
        if (pointerId != int.MinValue && (owner == null || !owner.IsPlaying)) CancelDrag();
        if (!absorbing || owner == null || !owner.IsPlaying) return;
        absorbElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(absorbElapsed / 0.22f);
        Vector2 destination = owner.LevelCamera.WorldToScreenPoint(targetWheel.position);
        MoveArrow(Vector2.Lerp(absorbStartScreen, destination, t));
        arrow.localScale = homeScale * (1f - t);
        if (t >= 1f)
        {
            absorbing = false;
            RestoreArrow();
        }
    }

    /// <summary>由按钮 EventSystem 调用，记录单一指针与 UI 相机，不抢占原点击。</summary>
    public void OnPointerDown(PointerEventData data)
    {
        if (pointerId != int.MinValue)
        {
            data.eligibleForClick = false;
            return;
        }
        if (!resetButton.Interactable) return;
        pointerId = data.pointerId;
        pointerData = data;
        eventCamera = data.pressEventCamera != null ? data.pressEventCamera : panelCanvas.worldCamera;
        recognized = false;
    }

    /// <summary>超过拖动阈值后屏蔽本次重开点击；仅符合比赛条件时搬动箭头。</summary>
    public void OnBeginDrag(PointerEventData data)
    {
        if (data.pointerId != pointerId) return;
        // EventSystem 已超过拖拽阈值：即使此时不具备道具使用资格，也不能误触重开。
        recognized = true;
        data.eligibleForClick = false;
        resetButton.IsBlockClick = true;
        if (consumed || owner == null || !owner.CanUseReverse) return;
        dragging = true;
        arrow.SetParent(dragLayer, true);
        arrow.localScale = homeScale;
        arrow.SetAsLastSibling();
        MoveArrow(data.position);
    }

    /// <summary>跟随当前指针；比赛资格失效时取消拖动并复位未使用箭头。</summary>
    public void OnDrag(PointerEventData data)
    {
        if (data.pointerId != pointerId || !dragging) return;
        data.eligibleForClick = false;
        if (!owner.CanUseReverse) { CancelDrag(); return; }
        MoveArrow(data.position);
    }

    /// <summary>拖动结束交给同一松手处理，防止与 PointerUp 重复消费。</summary>
    public void OnEndDrag(PointerEventData data) => Release(data);
    /// <summary>鼠标或触摸松手时判定车轮命中；普通轻点仍交给原按钮。</summary>
    public void OnPointerUp(PointerEventData data) => Release(data);

    private void Release(PointerEventData data)
    {
        if (data.pointerId != pointerId) return;
        if (recognized)
        {
            data.eligibleForClick = false;
            resetButton.IsBlockClick = true;
            releaseFrame = Time.frameCount;
        }
        if (dragging)
        {
            if (owner.TryUseReverse(data.position, out targetWheel))
            {
                consumed = true;
                absorbing = true;
                absorbElapsed = 0;
                absorbStartScreen = data.position;
            }
            else RestoreArrow();
        }
        dragging = false;
        pointerId = int.MinValue;
        pointerData = null;
    }

    private void MoveArrow(Vector2 screen)
    {
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(dragLayer, screen, eventCamera, out Vector3 world))
            arrow.position = world;
    }

    private void RestoreArrow()
    {
        arrow.SetParent(originalParent, false);
        arrow.anchoredPosition = homePosition;
        arrow.localScale = homeScale;
        // 消耗后只隐藏图片，不关闭按钮底板、点击区域或原有 OnClick。
        arrowImage.enabled = !consumed;
    }

    /// <summary>UI 取消事件只终止当前拖动，不恢复已经消耗的道具。</summary>
    public void OnCancel(BaseEventData data) => CancelDrag();

    private void CancelDrag()
    {
        if (pointerData != null) pointerData.eligibleForClick = false;
        pointerData = null;
        pointerId = int.MinValue;
        dragging = false;
        resetButton.IsBlockClick = true;
        releaseFrame = Time.frameCount;
        if (!absorbing) RestoreArrow();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying || arrow == null) return;
        CancelDrag();
        absorbing = false;
        RestoreArrow();
        resetButton.IsBlockClick = false;
    }
    #endregion
}

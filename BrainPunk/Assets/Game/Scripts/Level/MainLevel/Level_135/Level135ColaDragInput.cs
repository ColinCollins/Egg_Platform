using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 本关柜台可乐的指针捕获器。仅占有拖瓶指针的 raycast，防其滑入共享移动按钮；其他指针仍走原 UI。
/// </summary>
public sealed class Level135ColaDragInput : BaseRaycaster, IPointerDownHandler,
    IPointerUpHandler, IInitializePotentialDragHandler, IDragHandler
{
    #region 显式绑定与指针捕获
    /// <summary>只绑定135主控、三瓶库存和玩法相机，捕获一个拖瓶指针。</summary>
    [SerializeField] private Level135Ctrl owner;
    /// <summary>显式绑定本关库存组件，按命中瓶子的索引转发拖动。</summary>
    [SerializeField] private Level135ColaSupply supply;
    /// <summary>屏幕坐标转换所用的本关玩法相机。</summary>
    [SerializeField] private Camera gameplayCamera;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private int? activePointer;
    #endregion

    #region 输入处理

    /// <summary>
    /// 本 raycaster 不声明事件相机，避免 UI 相机 depth 比较抢在屏幕指针捕获优先级之前。
    /// 世界坐标换算仍只使用 Inspector 绑定的 <c>gameplayCamera</c>。
    /// </summary>
    public override Camera eventCamera => null;

    /// <summary>将拖瓶捕获层置于其他 BaseRaycaster 之后处理。</summary>
    public override int sortOrderPriority => int.MaxValue;

    /// <summary>将拖瓶捕获层置于其他 Raycaster 的渲染顺序之后处理。</summary>
    public override int renderOrderPriority => int.MaxValue;

    /// <summary>
    /// 仅在指针命中瓶子且当前交互有效时返回本组件的 RaycastResult；已捕获的指针移出瓶子后仍保持命中。
    /// </summary>
    /// <param name="data">当前指针的位置、按钮和指针编号。</param>
    /// <param name="results">接收本 raycaster 命中结果的列表。</param>
    public override void Raycast(PointerEventData data, List<RaycastResult> results)
    {
        if (owner == null || !owner.CanInteract || supply == null || !supply.Ready || gameplayCamera == null)
            return;

        bool captured = activePointer == data.pointerId;
        if (!captured)
        {
            if (activePointer.HasValue || !TryWorldPoint(data.position, out Vector3 hit))
                return;
            if (supply.Hit(hit) < 0) return;
            // 起手优先尊重弹窗/HUD；不递归调用 EventSystem.RaycastAll。
            foreach (BaseRaycaster raycaster in RaycasterManager.GetRaycasters())
            {
                if (!(raycaster is GraphicRaycaster graphic) || !graphic.isActiveAndEnabled)
                    continue;
                uiHits.Clear();
                graphic.Raycast(data, uiHits);
                if (uiHits.Count > 0) return;
            }
        }
        results.Add(new RaycastResult
        {
            gameObject = gameObject,
            module = this,
            distance = 0f,
            index = results.Count,
            depth = int.MaxValue,
            sortingOrder = int.MaxValue,
            screenPosition = data.position
        });
    }

    /// <summary>
    /// 尝试捕获左键指针，并记录指针与瓶子的起始偏移。
    /// 已有指针、非左键或当前不可交互时不会改变捕获状态。
    /// </summary>
    /// <param name="data">Unity EventSystem 派发的按下事件。</param>
    public void OnPointerDown(PointerEventData data)
    {
        if (!owner.CanInteract || activePointer.HasValue || data.button != PointerEventData.InputButton.Left ||
            !TryWorldPoint(data.position, out Vector3 world))
            return;
        int slot = supply.Hit(world);
        if (slot < 0) return;
        activePointer = data.pointerId;
        supply.BeginDrag(slot, world);
    }

    /// <summary>关闭本瓶子的拖拽阈值，使按下后即可开始更新位置。</summary>
    /// <param name="data">Unity EventSystem 派发的潜在拖拽事件。</param>
    public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;

    /// <summary>
    /// 更新当前已捕获指针对瓶子的拖拽位置，允许水平或竖直来回摇晃。
    /// </summary>
    /// <param name="data">Unity EventSystem 派发的拖拽事件。</param>
    public void OnDrag(PointerEventData data)
    {
        if (!owner.CanInteract || activePointer != data.pointerId || !TryWorldPoint(data.position, out Vector3 world))
            return;
        supply.Drag(world);
    }

    /// <summary>释放当前捕获指针；非当前捕获指针的抬起事件不会清除捕获。</summary>
    /// <param name="data">Unity EventSystem 派发的抬起事件。</param>
    public void OnPointerUp(PointerEventData data)
    {
        if (activePointer == data.pointerId) Clear();
    }

    /// <summary>
    /// 清除当前拖瓶指针并让原瓶归位。
    /// 由指针抬起、组件禁用或应用失去焦点时调用，可重复调用。
    /// </summary>
    public void Clear()
    {
        activePointer = null;
        if (supply != null) supply.Release();
    }

    private bool TryWorldPoint(Vector2 screen, out Vector3 world)
    {
        Ray ray = gameplayCamera.ScreenPointToRay(screen);
        Plane plane = new Plane(Vector3.forward, supply.transform.position);
        if (plane.Raycast(ray, out float distance))
        {
            world = ray.GetPoint(distance);
            return true;
        }
        world = default;
        return false;
    }

    protected override void OnDisable()
    {
        Clear();
        base.OnDisable();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) Clear();
    }
    #endregion
}

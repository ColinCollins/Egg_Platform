using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 空的 UI Raycast 目标。
/// 不绘制图形，但可以被 GraphicRaycaster 命中。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
[AddComponentMenu("UI/Empty Raycast Target")]
public class UIEmptyRaycastTarget : Graphic
{
    protected override void Awake()
    {
        base.Awake();
        raycastTarget = true;
    }

    /// <summary>
    /// 不生成任何可见网格。
    /// </summary>
    /// <param name="vh">UI 顶点辅助器。</param>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
    }
}

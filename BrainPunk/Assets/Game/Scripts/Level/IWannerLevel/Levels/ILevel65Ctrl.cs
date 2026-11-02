using UnityEngine;

/// <summary>
/// 第 065 关的编辑器辅助组件。
/// 调用边界：只为自定义 Inspector 保存 Target 快速创建参数，不参与 PlayMode 关卡逻辑。
/// 注意事项：本组件不监听输入或事件，也不修改角色、结算、存档、配置或打点状态。
/// </summary>
[DisallowMultipleComponent]
public sealed class ILevel65Ctrl : MonoBehaviour
{
#if UNITY_EDITOR
    #region Editor Quick Create

    /// <summary>
    /// 快速创建对象时使用的二维排列轴向；负方向通过填写负 offset 表示。
    /// </summary>
    private enum QuickCreateAxis
    {
        X,
        Y
    }

    /// <summary>
    /// 用途：作为编辑器批量创建的完整对象模板。
    /// 调用边界：仅允许绑定当前场景或 Prefab Stage 中的对象；工具不会移动或修改该模板。
    /// 注意事项：新对象会复制模板的完整层级，并与模板保持同一父节点。
    /// </summary>
    [Header("Editor Quick Create")]
    [Tooltip("作为批量复制模板的场景对象；模板本身不会被移动或计入新建数量")]
    [SerializeField] private GameObject quickCreateTarget;

    /// <summary>
    /// 用途：指定新对象沿世界坐标 X 轴或 Y 轴排列。
    /// 调用边界：仅供 ILevel65Ctrl 自定义 Inspector 读取。
    /// 注意事项：如需反方向排列，请使用负 offset。
    /// </summary>
    [Tooltip("新对象沿世界坐标 X 轴或 Y 轴排列；负 offset 表示反方向")]
    [SerializeField] private QuickCreateAxis quickCreateAxis = QuickCreateAxis.X;

    /// <summary>
    /// 用途：指定第一个新对象在场景世界坐标中的 X、Y 起点。
    /// 调用边界：可在 Inspector 输入，也可通过 Scene 视图手柄调整。
    /// 注意事项：Z 坐标继续使用 Target 当前的世界坐标 Z。
    /// </summary>
    [Tooltip("第一个新对象的世界坐标 X、Y；Z 保持 Target 当前值")]
    [SerializeField] private Vector2 quickCreateStartPosition;

    /// <summary>
    /// 用途：指定相邻新对象沿所选轴向的固定世界坐标间距。
    /// 调用边界：仅供编辑器批量创建使用。
    /// 注意事项：允许负值；创建多个对象时不能为 0。
    /// </summary>
    [Tooltip("相邻新对象的固定世界坐标间距；允许负值")]
    [SerializeField] private float quickCreateOffset = 1f;

    /// <summary>
    /// 用途：指定本次新建的 Target 副本数量。
    /// 调用边界：仅供编辑器批量创建使用。
    /// 注意事项：该数量不包含作为模板的 Target。
    /// </summary>
    [Min(1)]
    [Tooltip("本次新建数量，不包含作为模板的 Target")]
    [SerializeField] private int quickCreateCount = 1;

    #endregion
#endif
}

using UnityEngine;

/// <summary>
/// Level117 专用的关卡与 HUD 通信桥。桥只在当前 Level117 启用期间持有引用，避免跨关卡残留。
/// </summary>
public static class Level117RuntimeBridge
{
    private static Level117Ctrl activeController;

    /// <summary>
    /// 当前启用的 Level117 控制器；没有进入本关时返回 null。
    /// </summary>
    public static Level117Ctrl ActiveController => activeController;

    /// <summary>
    /// 注册当前 Level117 控制器，仅供 Level117Ctrl 生命周期调用。
    /// </summary>
    internal static void Bind(Level117Ctrl controller)
    {
        if (controller != null)
            activeController = controller;
    }

    /// <summary>
    /// 清理当前 Level117 控制器，仅当传入引用仍是当前控制器时生效。
    /// </summary>
    internal static void Unbind(Level117Ctrl controller)
    {
        if (ReferenceEquals(activeController, controller))
            activeController = null;
    }

    /// <summary>
    /// 向当前 Level117 上报商店按钮遮挡太阳的屏幕覆盖率，范围会自动限制在 0 到 1。
    /// </summary>
    /// <param name="coverage">太阳被按钮遮挡的比例。</param>
    /// <returns>当前关卡存在且已接收数据时返回 true。</returns>
    public static bool SetEclipseCoverage(float coverage)
    {
        if (activeController == null)
            return false;

        activeController.SetEclipseCoverage(coverage);
        return true;
    }

    /// <summary>
    /// 获取当前 Level117 控制器，供专用 HUD 初始化时使用。
    /// </summary>
    /// <param name="controller">当前控制器输出。</param>
    /// <returns>找到控制器时返回 true。</returns>
    public static bool TryGetController(out Level117Ctrl controller)
    {
        controller = activeController;
        return controller != null;
    }
}

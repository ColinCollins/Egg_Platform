using System.Collections.Generic;

/// <summary>
/// 红点业务注册辅助入口。用于在运行时初始化阶段集中注册一组 IRedDotRegister。
/// 注意：本类不自动发现业务注册器，避免编辑器态扫描或反射带来额外生命周期风险。
/// </summary>
public static class RedDotRegisterHub
{
    /// <summary>
    /// 注册指定业务模块并刷新当前红点状态。
    /// </summary>
    /// <param name="registers">业务红点注册器集合。</param>
    public static void RegisterAndRefresh(IEnumerable<IRedDotRegister> registers)
    {
        RedDotManager.RegisterRange(registers);
        RedDotManager.RefreshAll();
    }

    /// <summary>
    /// 注册指定业务模块并刷新当前红点状态。
    /// </summary>
    /// <param name="registers">业务红点注册器数组。</param>
    public static void RegisterAndRefresh(params IRedDotRegister[] registers)
    {
        RedDotManager.RegisterRange(registers);
        RedDotManager.RefreshAll();
    }
}

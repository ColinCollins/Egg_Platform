namespace Game.Play
{
    /// <summary>
    /// 为单个主线关卡提供提示按钮气泡等待时间覆盖值。
    /// </summary>
    public interface ILevelTipsWarningIntervalProvider
    {
        /// <summary>
        /// 尝试返回当前关卡专用的提示气泡等待秒数。
        /// 调用边界：主线 Playing 状态进入时读取一次；实现方不得修改远端配置。
        /// </summary>
        /// <param name="seconds">有效的正数等待秒数。</param>
        /// <returns>需要覆盖默认等待时间时返回 true。</returns>
        bool TryGetTipsWarningInterval(out float seconds);
    }
}

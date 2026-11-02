namespace Game.Play
{
    /// <summary>
    /// 玩法控制器类型。
    /// 用途：作为 Policy 解析结果和 ControllerManager 注册键。
    /// 注意事项：新增枚举值后必须同时注册对应 Policy 与 IPlayController，不能只增加入口类型。
    /// </summary>
    public enum PlayModeType
    {
        Main,
        Draw,
        IWanner,
        Dig,
    }

    /// <summary>
    /// 玩法入口请求。
    /// 用途：承载用户准备进入的玩法类型，后续玩法专属参数应在需求明确后再扩展。
    /// 调用边界：仅由入口层构造，并交给 PlayControllerManager 解析和切换。
    /// </summary>
    public readonly struct PlayEntryRequest
    {
        /// <summary>
        /// 用户请求进入的玩法类型。
        /// </summary>
        public PlayModeType RequestedMode { get; }

        /// <summary>
        /// 创建一条显式玩法入口请求。
        /// </summary>
        /// <param name="requestedMode">用户准备进入的玩法类型。</param>
        public PlayEntryRequest(PlayModeType requestedMode)
        {
            RequestedMode = requestedMode;
        }
    }
}

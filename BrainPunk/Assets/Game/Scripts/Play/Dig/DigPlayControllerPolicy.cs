namespace Game.Play
{
    /// <summary>将独立 Dig 首页请求映射到 DigPlayCtrl，不处理其它模式。</summary>
    public sealed class DigPlayControllerPolicy : IPlayControllerPolicy
    {
        /// <inheritdoc />
        public bool CanHandle(in PlayEntryRequest request) => request.RequestedMode == PlayModeType.Dig;
        /// <inheritdoc />
        public bool TryResolve(in PlayEntryRequest request, out PlayModeType mode, out string error)
        { mode = PlayModeType.Dig; error = null; return CanHandle(request); }
    }
}

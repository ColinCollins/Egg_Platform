namespace Game.Play
{
    /// <summary>
    /// 玩法入口解析 Policy。
    /// 用途：判断一条入口请求应交给哪个 IPlayController。
    /// 调用边界：只负责选择玩法类型，不执行控制器生命周期。
    /// </summary>
    public interface IPlayControllerPolicy
    {
        /// <summary>
        /// 判断当前 Policy 是否负责这条入口请求。
        /// </summary>
        bool CanHandle(in PlayEntryRequest request);

        /// <summary>
        /// 将入口请求解析为已注册控制器的玩法类型。
        /// </summary>
        bool TryResolve(in PlayEntryRequest request, out PlayModeType mode, out string error);
    }

    /// <summary>
    /// 主线玩法入口 Policy。
    /// 用途：把明确请求 Main 的入口映射到现有 PlayCtrl。
    /// </summary>
    public sealed class MainPlayControllerPolicy : IPlayControllerPolicy
    {
        /// <inheritdoc />
        public bool CanHandle(in PlayEntryRequest request)
        {
            return request.RequestedMode == PlayModeType.Main;
        }

        /// <inheritdoc />
        public bool TryResolve(in PlayEntryRequest request, out PlayModeType mode, out string error)
        {
            mode = PlayModeType.Main;
            error = null;
            return CanHandle(request);
        }
    }

    /// <summary>
    /// 画线玩法入口 Policy。
    /// 用途：把明确请求 Draw 的入口映射到 DrawPlayCtrl。
    /// </summary>
    public sealed class DrawPlayControllerPolicy : IPlayControllerPolicy
    {
        /// <inheritdoc />
        public bool CanHandle(in PlayEntryRequest request)
        {
            return request.RequestedMode == PlayModeType.Draw;
        }

        /// <inheritdoc />
        public bool TryResolve(in PlayEntryRequest request, out PlayModeType mode, out string error)
        {
            mode = PlayModeType.Draw;
            error = null;
            return CanHandle(request);
        }
    }

    /// <summary>
    /// IWanner 玩法入口 Policy。
    /// 用途：把明确请求 IWanner 的入口映射到 IWannerPlayCtrl。
    /// </summary>
    public sealed class IWannerPlayControllerPolicy : IPlayControllerPolicy
    {
        /// <inheritdoc />
        public bool CanHandle(in PlayEntryRequest request)
        {
            return request.RequestedMode == PlayModeType.IWanner;
        }

        /// <inheritdoc />
        public bool TryResolve(in PlayEntryRequest request, out PlayModeType mode, out string error)
        {
            mode = PlayModeType.IWanner;
            error = null;
            return CanHandle(request);
        }
    }
}

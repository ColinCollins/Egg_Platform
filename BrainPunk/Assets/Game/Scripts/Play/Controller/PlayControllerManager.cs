using System;
using System.Collections.Generic;

namespace Game.Play
{
    /// <summary>
    /// 玩法控制器生命周期管理器。
    /// 用途：隔离 GameManager 与具体玩法控制器，统一负责注册、Policy 解析、切换、Tick 和释放。
    /// 注意事项：本类是 GameManager 持有的普通对象，不创建第二个全局单例。
    /// </summary>
    public sealed class PlayControllerManager
    {
        #region Fields and Properties

        private readonly Dictionary<PlayModeType, IPlayController> _controllers =
            new Dictionary<PlayModeType, IPlayController>();
        private readonly PlayControllerContext _context;
        private readonly PlayControllerPolicyResolver _policyResolver;

        /// <summary>
        /// 当前已激活的玩法控制器。
        /// </summary>
        public IPlayController ActiveController { get; private set; }

        /// <summary>
        /// 当前玩法提供给通用暂停弹窗的控制能力。
        /// </summary>
        public IPlayPauseHost ActivePauseHost => ActiveController;

        #endregion

        #region Construction

        /// <summary>
        /// 创建玩法控制器管理器。
        /// </summary>
        /// <param name="context">跨玩法共享运行时依赖。</param>
        /// <param name="policyResolver">入口 Policy 解析器。</param>
        public PlayControllerManager(
            PlayControllerContext context,
            PlayControllerPolicyResolver policyResolver)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _policyResolver = policyResolver ?? throw new ArgumentNullException(nameof(policyResolver));
        }

        #endregion

        #region Registration and Switching

        /// <summary>
        /// 注册一个玩法控制器。
        /// 同一玩法类型重复注册同一实例视为成功，注册不同实例会返回失败。
        /// </summary>
        public bool Register(IPlayController controller, out string error)
        {
            if (controller == null)
            {
                error = "Cannot register a null play controller.";
                return false;
            }

            if (_controllers.TryGetValue(controller.Mode, out IPlayController registered))
            {
                if (ReferenceEquals(registered, controller))
                {
                    error = null;
                    return true;
                }

                error = $"A different play controller is already registered for mode: {controller.Mode}.";
                return false;
            }

            _controllers.Add(controller.Mode, controller);
            error = null;
            return true;
        }

        /// <summary>
        /// 根据入口 Policy 切换当前玩法。
        /// 初始化或激活失败时会尝试恢复之前的控制器，避免入口层进入无控制器状态。
        /// </summary>
        public bool TryEnter(in PlayEntryRequest request, out string error)
        {
            if (!_policyResolver.TryResolve(request, out PlayModeType mode, out error))
                return false;

            if (!_controllers.TryGetValue(mode, out IPlayController nextController))
            {
                error = $"No play controller is registered for mode: {mode}.";
                return false;
            }

            if (ReferenceEquals(ActiveController, nextController))
            {
                error = null;
                return true;
            }

            try
            {
                if (!nextController.IsInitialized)
                    nextController.Initialize(_context);
            }
            catch (Exception exception)
            {
                error = $"Failed to initialize play controller {mode}: {exception}";
                return false;
            }

            IPlayController previousController = ActiveController;
            try
            {
                previousController?.Deactivate();
                nextController.Activate();
                ActiveController = nextController;
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                nextController.Deactivate();
                previousController?.Activate();
                ActiveController = previousController;
                error = $"Failed to activate play controller {mode}: {exception}";
                return false;
            }
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// 驱动当前玩法的逐帧逻辑。
        /// </summary>
        public void Tick()
        {
            ActiveController?.Tick();
        }

        /// <summary>
        /// 释放全部已注册控制器。
        /// 调用后管理器不再保留控制器引用。
        /// </summary>
        public void Shutdown()
        {
            foreach (IPlayController controller in _controllers.Values)
                controller.Shutdown();

            ActiveController = null;
            _controllers.Clear();
        }

        #endregion
    }
}

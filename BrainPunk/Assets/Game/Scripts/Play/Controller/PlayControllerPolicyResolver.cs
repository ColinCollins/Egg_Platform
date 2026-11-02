using System;
using System.Collections.Generic;

namespace Game.Play
{
    /// <summary>
    /// 玩法入口 Policy 解析器。
    /// 用途：按注册顺序寻找首个可处理请求的 Policy，并返回目标玩法类型。
    /// 注意事项：同一入口请求只能有一个业务上有效的 Policy，注册顺序用于处理未来的优先级。
    /// </summary>
    public sealed class PlayControllerPolicyResolver
    {
        private readonly List<IPlayControllerPolicy> _policies = new List<IPlayControllerPolicy>();

        /// <summary>
        /// 注册一条玩法入口 Policy。
        /// </summary>
        /// <param name="policy">不可为空的入口解析策略。</param>
        public void Register(IPlayControllerPolicy policy)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            if (!_policies.Contains(policy))
                _policies.Add(policy);
        }

        /// <summary>
        /// 解析入口请求对应的玩法类型。
        /// </summary>
        public bool TryResolve(in PlayEntryRequest request, out PlayModeType mode, out string error)
        {
            for (int i = 0; i < _policies.Count; i++)
            {
                IPlayControllerPolicy policy = _policies[i];
                if (policy.CanHandle(request))
                    return policy.TryResolve(request, out mode, out error);
            }

            mode = default;
            error = $"No play controller policy can handle mode: {request.RequestedMode}.";
            return false;
        }
    }
}

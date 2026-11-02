#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Auth.Games;
using Guru.SDK.Framework.Core.Account.Data;
using Guru.SDK.Framework.Core.Spec.Protocols.Account;
using Guru.SDK.Framework.Utils.Auth;
using Guru.SDK.Framework.Utils.Log;

namespace Game
{
    /// <summary>
    /// 游戏账号认证管理器
    /// 实现 IAccountAuthDelegate 接口并在 Guru SDK Protocol 中注册
    /// </summary>
    public class GameSDKAccountManager : IAccountAuthDelegate
    {
        private const string TAG = "GameSDKAccountManager";

        private static readonly Lazy<GameSDKAccountManager> LazyInstance = new(() =>
            new GameSDKAccountManager(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static GameSDKAccountManager Instance => LazyInstance.Value;

        /// <summary>
        /// 构建支持的认证凭证委托列表
        /// </summary>
        public UniTask<List<IAuthCredentialDelegate>> BuildSupportedAuthCredentialDelegates()
        {
            return new UniTask<List<IAuthCredentialDelegate>>(new List<IAuthCredentialDelegate>
            {
                new GamesCredentialDelegate()
            });
        }

        /// <summary>
        /// 登录成功回调
        /// </summary>
        public UniTask<bool> OnLogin(AccountAuth auth)
        {
            Log.D($"[{TAG}] OnLogin: {auth}", TAG);
            // TODO: 登录成功后的业务逻辑（如加载玩家数据、同步远程存档等）
            return new UniTask<bool>(true);
        }

        /// <summary>
        /// 登出成功回调
        /// </summary>
        public UniTask<bool> OnLogout(GuruUser logoutUser)
        {
            Log.D($"[{TAG}] OnLogout: {logoutUser}", TAG);
            return new UniTask<bool>(true);
        }

        /// <summary>
        /// 匿名登出回调
        /// </summary>
        public UniTask<bool> OnAnonymousLogout(GuruUser logoutUser)
        {
            Log.D($"[{TAG}] OnAnonymousLogout: {logoutUser}", TAG);
            return new UniTask<bool>(true);
        }

        /// <summary>
        /// 匿名登录回调
        /// </summary>
        public UniTask<bool> OnAnonymousLogin(AccountAuth auth)
        {
            Log.D($"[{TAG}] OnAnonymousLogin: {auth}", TAG);
            return new UniTask<bool>(true);
        }

        /// <summary>
        /// 账号冲突回调
        /// </summary>
        public UniTask<bool> OnConflict()
        {
            Log.D($"[{TAG}] OnConflict", TAG);
            return new UniTask<bool>(true);
        }

        /// <summary>
        /// 账号删除回调
        /// </summary>
        public UniTask<bool> OnAccountDeleted()
        {
            Log.D($"[{TAG}] OnAccountDeleted", TAG);
            return new UniTask<bool>(true);
        }

        /// <summary>
        /// 自动过渡回调
        /// </summary>
        public UniTask<bool> OnAutoTransition(TransitionInfo transitionInfo)
        {
            Log.D($"[{TAG}] OnAutoTransition: {transitionInfo}", TAG);
            return new UniTask<bool>(true);
        }

        /// <summary>
        /// 账号切换回调
        /// </summary>
        public UniTask<bool> OnSwitch(AccountSwitchContext context)
        {
            Log.D($"[{TAG}] OnSwitch: {context}", TAG);
            return new UniTask<bool>(true);
        }
    }
}

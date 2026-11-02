#nullable enable
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Core;
using Guru.SDK.Framework.Core.Account;
using Guru.SDK.Framework.Core.Account.Data;
using Guru.SDK.Framework.Utils.Auth;
using Guru.SDK.Framework.Utils.Log;
#if UNITY_ANDROID
using Guru.UniKit.Google.Games;
#endif

namespace Game
{
    /// <summary>
    /// 游戏账号工具类
    /// 封装 Guru SDK Account 模块的常用操作
    /// </summary>
    public static class GameAccountUtils
    {
        private const string TAG = "GameAccountUtils";

        private static bool IsGameAccountApiDisabledOnThisPlatform()
        {
#if false //UNITY_IOS
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// 当前是否已使用游戏服务登录（Google Play Games / Game Center）
        /// </summary>
        public static bool IsGameLoggedIn
        {
            get
            {
#if false//UNITY_IOS
                return false;
#else
                return AuthCredentialManager.Instance.IsLogin(AuthType.Game);
#endif
            }
        }

        /// <summary>
        /// 当前是否存在有效用户（任意登录态）
        /// </summary>
        public static bool HasValidUser
        {
            get
            {
                var user = AccountDataStore.Instance.User;
                return user != null && user.IsValid;
            }
        }

        /// <summary>
        /// 获取当前登录用户的 Uid，未登录返回 null
        /// </summary>
        public static string? CurrentUid => AccountDataStore.Instance.User?.Uid;

        /// <summary>
        /// 获取当前 GuruUser
        /// </summary>
        public static GuruUser? CurrentUser => AccountDataStore.Instance.User;

        /// <summary>
        /// 触发游戏服务登录（Google Play Games / Game Center）
        /// </summary>
        public static async UniTask<bool> LoginWithGame(bool isForceLogin = false)
        {
            if (IsGameAccountApiDisabledOnThisPlatform())
            {
                Log.D("LoginWithGame skipped: disabled on iOS", TAG);
                return false;
            }

            if (IsGameLoggedIn)
            {
                Log.D("Already logged in with Game", TAG);
                return true;
            }

            if (!isForceLogin)
            {
                if (DB.GameSetting.HasTryingGoogleLogin)
                {
                    return false;
                }

                DB.GameSetting.HasTryingGoogleLogin = true;
                DB.GameSetting.Save();
            }

#if UNITY_ANDROID
            // Android 平台直接使用 Google Play Games 手动认证，不再走 LoginWith
            try
            {
                await GooglePlayGamesAuthAgent.Instance.ManualAuthenticateAsync();
            }
            catch (Exception ex)
            {
                Log.D($"Google Play Games manual authenticate error: {ex.Message}", TAG);
                return false;
            }

            var result = IsGameLoggedIn;
            Log.D($"LoginWithGame (Google) result: {result}", TAG);
            return result;
#else
            var success = await AccountManager.Instance.LoginWith(AuthType.Game);
            Log.D($"LoginWithGame result: {success}", TAG);
            return success;
#endif
        }

        // LoginWithGame 的 Android 分支已经包含了 Google Play Games 手动认证，
        // 外部统一调用 LoginWithGame 即可，不需要单独暴露 LoginWithGoogle。

        /// <summary>
        /// 登出游戏服务账号
        /// 登出后若当前无其他凭证，AccountManager 会自动创建匿名账号
        /// </summary>
        public static async UniTask<bool> LogoutGame()
        {
            if (IsGameAccountApiDisabledOnThisPlatform())
            {
                Log.D("LogoutGame skipped: disabled on iOS", TAG);
                return true;
            }

            if (!IsGameLoggedIn)
            {
                Log.D("Not logged in with Game, skip logout", TAG);
                return true;
            }

            var logoutUser = await AccountManager.Instance.LogoutAsync(
                switching: false,
                authTypes: new HashSet<AuthType> { AuthType.Game }
            );

            var success = logoutUser != null;
            if (success)
            {
                // 登出后重置自动登录标记，允许下次自动登录再尝试一次
                DB.GameSetting.HasTryingGoogleLogin = false;
                DB.GameSetting.Save();
            }
            Log.D($"LogoutGame result: {success}", TAG);
            return success;
        }

        /// <summary>
        /// 判断是否支持游戏服务登录
        /// iOS 始终支持（依赖系统 Game Center）
        /// Android 需要配置 game_service_client_id
        /// </summary>
        public static bool IsGameLoginSupported()
        {
#if UNITY_IOS
            return true;
#elif UNITY_ANDROID
            return !string.IsNullOrEmpty(GuruSdk.Instance.Details.GameServiceClientId);
#else
            return false;
#endif
        }

        /// <summary>
        /// 检查并尝试自动恢复/绑定游戏登录
        /// 通常在 AccountManager 初始化后会自动调用，业务层一般不需要手动调用
        /// </summary>
        public static async UniTask<bool> TryAutoLoginGame()
        {
            if (IsGameAccountApiDisabledOnThisPlatform())
            {
                Log.D("TryAutoLoginGame skipped: disabled on iOS", TAG);
                return false;
            }

            if (!IsGameLoginSupported())
            {
                Log.D("Game login not supported, skip auto login", TAG);
                return false;
            }

            if (IsGameLoggedIn)
            {
                Log.D("Already logged in with Game", TAG);
                return true;
            }

            // 直接调用 LoginWithGame 触发静默或弹窗登录
            return await LoginWithGame();
        }
    }
}

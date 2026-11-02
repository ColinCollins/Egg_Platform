#nullable enable
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Utils.Package;
using SDK.Remote;

namespace Game
{
    public class GameRoot : GuruRoot
    {
        public GameRoot(List<GuruPackage> children, int priority = 0) : base(children, priority)
        {
            // 注册 GameServicePackage（成就系统）
            var builder = new Guru.SDK.Framework.GameService.Core.Package.GameServicePackage.Builder();
            
            #if UNITY_ANDROID && !UNITY_EDITOR
            builder.AddService(Guru.SDK.Framework.GameService.GooglePlayGames.GooglePlayGamesServiceBuilder.Build());
            #elif UNITY_IOS && !UNITY_EDITOR
            builder.AddService(Guru.SDK.Framework.GameService.AppleGameCenter.AppleGameCenterServiceBuilder.Build());
            #endif
            
            builder.UseLocalAchievements(Achievements.AllAchievements);
            children?.Add(builder.Build());
        }

        /// <summary>
        /// SDK 已准备 Remote Config 缓存、尚未派发自动拉取时记录 getter 身份。
        /// 仅在运行态初始化使用，不额外请求配置或延长 SDK 初始化等待。
        /// </summary>
        protected override UniTask Initialize()
        {
            RemoteConfigService.CaptureSdkPreparedGetter();
            return base.Initialize();
        }
    }
}

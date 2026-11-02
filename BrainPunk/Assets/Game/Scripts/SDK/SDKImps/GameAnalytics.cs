using System;
using System.Collections.Generic;
using System.Threading;
using Guru.SDK.Framework.Analytics.Appsflyer;
using Guru.SDK.Framework.Core.Analytics.Strategy;
using Guru.SDK.Framework.Core.Analytics.Transmitter;
using Guru.SDK.Framework.Core.Spec.Protocols.Analytics;

namespace Game
{
    /// <summary>游戏侧统计协议配置；复用 Guru 生命周期，不直接初始化原生 SDK 或执行深链。</summary>
    public class GameAnalytics : IAnalyticsDelegate
    {
        #region 单例与协议配置

        private static readonly Lazy<GameAnalytics> LazyInstance =
            new(() => new GameAnalytics(), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>由 GameProtocol 获取的共享实例；首次访问只构造接点，不启动原生请求。</summary>
        public static GameAnalytics Instance => LazyInstance.Value;

        /// <summary>固定顺序的接点，由 Guru 依次 Prepare 和 Initialize；不得调整深链接点与 AF 的相对顺序。</summary>
        public IReadOnlyList<IAnalyticsTransmitter> CustomTransmitters => _customTransmitters;

        /// <summary>不提供游戏侧显式规则；null 由 SDK 接受，继续使用既有配置策略。</summary>
        public Dictionary<string, StrategyRule> ExplicitRules => null;

        /// <summary>不提供游戏侧优先规则；不额外分配空集合或覆盖 SDK 的规则顺序。</summary>
        public List<StrategyRule> PriorityRules => null;

        private readonly IAnalyticsTransmitter[] _customTransmitters;

        #endregion

        #region 生命周期接点

        private GameAnalytics()
        {
            // 实现 Appsflyer 打点协议
            _customTransmitters = new IAnalyticsTransmitter[]
            {
                // 状态同步后的 Initialize 建立原生首开窗口；不表示用户已经完成 ATT 授权。
                new OneLinkManager.FacebookDdlHook(),
                new OneLinkManager.Ga4fDdlHook(),
                GuruAppsflyer.Prepare(CreateAppsFlyerConfig()),
                new OneLinkManager.AppsFlyerHook(),
            };
        }

        #endregion

        #region AppsFlyer 事件策略

        // Appsflyer 买量点扩展
        private static AppsFlyerConfig CreateAppsFlyerConfig()
        {
            const string afDevKey = "JGsA3FFVn5q2jGvJJScebV";  // GuruGame 统一的 Key
            const string appleId = "6757749388";        // Game AppleId 需要到 AppleConnect 后台查看
            
            var strategy = AppsFlyerStrategy.Create(new List<AppsflyerEventDefinition>
            {
                AppsflyerEventDefinition.Define("iap_purchase", AppsflyerEventRule.Revenue),
                AppsflyerEventDefinition.Define("sub_purchase", AppsflyerEventRule.Revenue),
                AppsflyerEventDefinition.Define("iap_ret_true"),
                // OneLink 安装归因的开始、失败及返回指令观测；使用默认事件转换规则。
                AppsflyerEventDefinition.Define("onelink_gcd"),
                // =================================================== 自定义数据
                AppsflyerEventDefinition.Define("level_end_success_1"),
                AppsflyerEventDefinition.Define("level_end_success_2"),
                AppsflyerEventDefinition.Define("level_end_success_3"),
                AppsflyerEventDefinition.Define("level_end_success_4"),
                AppsflyerEventDefinition.Define("level_end_success_5"),
                AppsflyerEventDefinition.Define("level_end_success_7"),
                AppsflyerEventDefinition.Define("level_end_success_9"),
                AppsflyerEventDefinition.Define("level_end_success_11"),
                AppsflyerEventDefinition.Define("level_end_success_13"),
                AppsflyerEventDefinition.Define("level_end_success_15"),
                AppsflyerEventDefinition.Define("level_end_success_20"),
                AppsflyerEventDefinition.Define("level_end_success_25"),
                AppsflyerEventDefinition.Define("level_end_success_30"),
                AppsflyerEventDefinition.Define("level_end_success_35"),
                AppsflyerEventDefinition.Define("level_end_success_36"),
            });
            // 关闭 AppsFlyer 原生调试日志；保留正常统计及 OneLink 回调。
            return new AppsFlyerConfig(afDevKey, appleId, strategy, debugMode: false);
        }

        #endregion
    }
}

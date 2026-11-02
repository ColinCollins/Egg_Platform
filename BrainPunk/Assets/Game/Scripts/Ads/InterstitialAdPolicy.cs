using System;
using Bear.Game;
using Bear.Logger;
using Config.Game;
using Game.ConfigModule;
using GameCommon;

namespace Game
{
    #region Policy Contracts

    /// <summary>
    /// 插屏广告策略统一契约。
    /// 用途：由当前玩法控制器提供对应策略，隔离不同玩法的配置、计数和冷却状态。
    /// 注意事项：策略只处理广告资格与状态，不负责调用广告 SDK。
    /// </summary>
    public interface IInterstitialAdPolicy : IDisposable
    {
        /// <summary>
        /// 当前策略剩余插屏冷却时间；小于等于 0 表示无冷却。
        /// </summary>
        int CurrentInterstitialCD { get; }

        /// <summary>
        /// 判断指定关卡和广告位是否允许展示插屏。
        /// </summary>
        bool CanShowInterstitial(int currentLevel, InterstitialPlacement placement);

        /// <summary>
        /// 记录一次成功展示的插屏，并按广告位重置对应计数。
        /// 调用边界：仅由广告 SDK 成功回调调用一次。
        /// </summary>
        void OnInterstitialShown(InterstitialPlacement placement);

        /// <summary>
        /// 记录一次成功播放的激励视频，用于本策略独立的插屏冷却。
        /// </summary>
        void OnRewardAdShown();

        /// <summary>
        /// 记录一次失败或手动重试。
        /// </summary>
        void OnPlayerFailedOrRetried();

        /// <summary>
        /// 记录一次关卡胜利，用于胜利插屏间隔计数。
        /// </summary>
        void OnLevelSucceeded();
    }

    /// <summary>
    /// 可提供插屏策略的玩法控制器能力。
    /// 用途：InterstitialAdHelper 根据当前活动控制器取得策略和玩法内关卡序号。
    /// 注意事项：异步广告请求应捕获请求发起时的 Host，不得在回调时重新解析活动控制器。
    /// </summary>
    public interface IInterstitialAdPolicyHost
    {
        /// <summary>
        /// 当前玩法用于插屏最小关卡判断的关卡序号。
        /// </summary>
        int InterstitialLevel { get; }

        /// <summary>
        /// 当前玩法独立持有的插屏策略。
        /// </summary>
        IInterstitialAdPolicy InterstitialAdPolicy { get; }
    }

    #endregion

    #region Shared Policy

    /// <summary>
    /// 插屏广告公共策略算法。
    /// 用途：复用免广告、冷却、胜利间隔和失败阈值判断。
    /// 注意事项：具体配置与存档必须由玩法子类提供，禁止跨玩法共享计数或时间戳。
    /// </summary>
    public abstract class InterstitialAdPolicyBase : IInterstitialAdPolicy
    {
        private bool _disposed;

        protected abstract string PolicyName { get; }
        protected abstract bool IsStateAvailable { get; }
        protected abstract long LastInterstitialDate { get; set; }
        protected abstract long LastRewardAdDate { get; set; }
        protected abstract int InterstitialFailCount { get; set; }
        protected abstract int InterstitialLevelSuccessCount { get; set; }

        protected abstract int GetInterstitialCD();
        protected abstract int GetRewardResetInterstitialCD();
        protected abstract int GetInterstitialShowMinLevel();
        protected abstract int GetInterstitialShowIntervalCount(int currentLevel);
        protected abstract int GetInterstitialShowFailedCount(int currentLevel);
        protected abstract bool SaveState();

        /// <summary>
        /// 当前玩法自上次胜利插屏成功展示后的普通小关通过次数。
        /// 注意事项：默认玩法不支持该条件；仅由有小关概念的玩法覆写。
        /// </summary>
        protected virtual int InterstitialPassedLevelCount
        {
            get => 0;
            set { }
        }

        /// <summary>
        /// 获取普通小关通过次数的插屏门槛；小于 0 表示关闭该条件。
        /// </summary>
        protected virtual int GetInterstitialShowPassedLevelCount()
        {
            return -1;
        }

        protected virtual bool IsNoAdForTest => InterstitialAdPolicy.isNoADForTest;

        /// <summary>
        /// 当前策略剩余插屏冷却时间；小于等于 0 表示无冷却。
        /// </summary>
        public int CurrentInterstitialCD
        {
            get
            {
                if (!CanUseState(nameof(CurrentInterstitialCD)))
                    return 0;

                long now = GetNowSeconds();
                int interstitialRemain = GetRemainingSeconds(
                    now,
                    LastInterstitialDate,
                    GetInterstitialCD());
                int rewardRemain = GetRemainingSeconds(
                    now,
                    LastRewardAdDate,
                    GetRewardResetInterstitialCD());
                return Math.Max(interstitialRemain, rewardRemain);
            }
        }

        /// <summary>
        /// 记录一次成功展示的插屏，并按广告位重置对应计数。
        /// </summary>
        public void OnInterstitialShown(InterstitialPlacement placement)
        {
            if (!CanUseState(nameof(OnInterstitialShown)))
                return;

            long now = GetNowSeconds();
            LastInterstitialDate = now;

            if (placement == InterstitialPlacement.InGameFailed)
                InterstitialFailCount = 0;
            else if (placement == InterstitialPlacement.InGameVictory)
            {
                InterstitialLevelSuccessCount = 0;
                InterstitialPassedLevelCount = 0;
            }

            SaveStateWithLog(nameof(OnInterstitialShown));
            Log(
                $"OnInterstitialShown -> placement:{placement}, " +
                $"lastInterstitial:{now}, failureCount:{InterstitialFailCount}, " +
                $"successCount:{InterstitialLevelSuccessCount}, " +
                $"passedLevelCount:{InterstitialPassedLevelCount}");
        }

        /// <summary>
        /// 记录一次成功播放的激励视频。
        /// </summary>
        public void OnRewardAdShown()
        {
            if (!CanUseState(nameof(OnRewardAdShown)))
                return;

            LastRewardAdDate = GetNowSeconds();
            SaveStateWithLog(nameof(OnRewardAdShown));
            Log($"OnRewardAdShown -> lastRewardTimestamp:{LastRewardAdDate}");
        }

        /// <summary>
        /// 记录一次失败或手动重试。
        /// </summary>
        public void OnPlayerFailedOrRetried()
        {
            if (!CanUseState(nameof(OnPlayerFailedOrRetried)))
                return;

            InterstitialFailCount++;
            SaveStateWithLog(nameof(OnPlayerFailedOrRetried));
            Log($"OnPlayerFailedOrRetried -> failureCount:{InterstitialFailCount}");
        }

        /// <summary>
        /// 记录一次关卡胜利。
        /// </summary>
        public void OnLevelSucceeded()
        {
            if (!CanUseState(nameof(OnLevelSucceeded)))
                return;

            InterstitialLevelSuccessCount++;
            SaveStateWithLog(nameof(OnLevelSucceeded));
            Log($"OnLevelSucceeded -> successCount:{InterstitialLevelSuccessCount}");
        }

        /// <summary>
        /// 记录一次普通小关通过，用于支持按小关通过次数补充触发胜利插屏。
        /// 调用边界：仅在玩法确认推进到下一小关或完成当前 Group 时调用一次；TBN 不调用。
        /// 注意事项：配置小于 0 时不累计，避免关闭功能期间产生无效存档写入。
        /// </summary>
        public void OnMiniLevelSucceeded()
        {
            if (!CanUseState(nameof(OnMiniLevelSucceeded)))
                return;

            int threshold = GetInterstitialShowPassedLevelCount();
            if (threshold < 0)
                return;

            InterstitialPassedLevelCount++;
            SaveStateWithLog(nameof(OnMiniLevelSucceeded));
            Log(
                $"OnMiniLevelSucceeded -> passedLevelCount:{InterstitialPassedLevelCount}, " +
                $"threshold:{threshold}");
        }

        /// <summary>
        /// 判断指定关卡和广告位是否允许展示插屏。
        /// </summary>
        public bool CanShowInterstitial(int currentLevel, InterstitialPlacement placement)
        {
            if (_disposed)
            {
                Log($"{nameof(CanShowInterstitial)} ignored because policy is disposed");
                return false;
            }

            if (IsNoAdForTest)
                return false;

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.Bag == null)
            {
                Log("CanShowInterstitial blocked because GameManager or Bag is unavailable");
                return false;
            }

            if (gameManager.Bag.HasTool(GameProps.NoAds))
                return false;

            if (ShouldBlockByBusinessFlow(currentLevel, placement))
                return false;

            if (!CanUseState(nameof(CanShowInterstitial)))
                return false;

            long now = GetNowSeconds();
            long sinceLastInterstitial = LastInterstitialDate > 0
                ? now - LastInterstitialDate
                : -1;
            Log(
                $"CanShowInterstitial start -> level:{currentLevel}, placement:{placement}, " +
                $"now:{now}, lastInterstitial:{LastInterstitialDate}, " +
                $"sinceLastInterstitial:{sinceLastInterstitial}");

            int interstitialCd = GetInterstitialCD();
            if (interstitialCd > 0 &&
                LastInterstitialDate > 0 &&
                now - LastInterstitialDate < interstitialCd)
            {
                long remain = interstitialCd - (now - LastInterstitialDate);
                Log(
                    $"CanShowInterstitial blocked by interstitial CD -> " +
                    $"cd:{interstitialCd}, remain:{remain}");
                return false;
            }

            int rewardResetCd = GetRewardResetInterstitialCD();
            if (rewardResetCd > 0 &&
                now - LastRewardAdDate < rewardResetCd)
            {
                long remain = rewardResetCd - (now - LastRewardAdDate);
                Log(
                    $"CanShowInterstitial blocked by reward reset CD -> " +
                    $"cd:{rewardResetCd}, remain:{remain}");
                return false;
            }

            int minLevel = GetInterstitialShowMinLevel();
            int interval = GetInterstitialShowIntervalCount(currentLevel);
            int failCountThreshold = GetInterstitialShowFailedCount(currentLevel);
            int passedLevelThreshold = GetInterstitialShowPassedLevelCount();

            bool levelTrigger = false;
            if (currentLevel >= minLevel && interval > 0)
            {
                int delta = currentLevel - minLevel;
                if (delta >= 0 && InterstitialLevelSuccessCount - interval >= 0)
                    levelTrigger = true;
            }

            bool failTrigger =
                failCountThreshold > 0 &&
                InterstitialFailCount >= failCountThreshold;
            bool passedLevelTrigger =
                currentLevel >= minLevel &&
                passedLevelThreshold >= 0 &&
                InterstitialPassedLevelCount >= passedLevelThreshold;
            bool canShow;

            if (placement == InterstitialPlacement.InGameFailed)
                canShow = failTrigger;
            else if (placement == InterstitialPlacement.InGameVictory)
                canShow = levelTrigger || passedLevelTrigger;
            else
                canShow = levelTrigger || passedLevelTrigger || failTrigger;

            Log(
                $"CanShowInterstitial triggers -> placement:{placement}, " +
                $"levelTrigger:{levelTrigger}, passedLevelTrigger:{passedLevelTrigger}, " +
                $"failTrigger:{failTrigger}, " +
                $"resultByPlacement:{canShow}, minLevel:{minLevel}, interval:{interval}, " +
                $"passedLevelThreshold:{passedLevelThreshold}, " +
                $"passedLevelCount:{InterstitialPassedLevelCount}, " +
                $"failThreshold:{failCountThreshold}, failureCount:{InterstitialFailCount}");

            if (!canShow)
            {
                Log("CanShowInterstitial result:false -> no trigger matched");
                return false;
            }

            Log("CanShowInterstitial result:true");
            return true;
        }

        /// <summary>
        /// 释放策略；释放后所有判断和状态写入都会被拒绝。
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
        }

        /// <summary>
        /// 判断玩法专属业务流程是否需要阻止当前插屏。
        /// </summary>
        protected virtual bool ShouldBlockByBusinessFlow(
            int currentLevel,
            InterstitialPlacement placement)
        {
            return false;
        }

        private bool CanUseState(string operation)
        {
            if (_disposed)
            {
                Log($"{operation} ignored because policy is disposed");
                return false;
            }

            if (IsStateAvailable)
                return true;

            Log($"{operation} ignored because policy state is unavailable");
            return false;
        }

        private void SaveStateWithLog(string operation)
        {
            if (!SaveState())
                Log($"{operation} failed to save policy state");
        }

        private void Log(string message)
        {
            BearLogger.Log($"[{PolicyName}] {message}", GetType());
        }

        private static int GetRemainingSeconds(long now, long lastTimestamp, int cooldown)
        {
            if (cooldown <= 0 || lastTimestamp <= 0)
                return 0;

            long elapsed = now - lastTimestamp;
            return elapsed < cooldown
                ? (int)(cooldown - elapsed)
                : 0;
        }

        private static long GetNowSeconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    #endregion

    #region Main Policy

    /// <summary>
    /// 主线插屏策略。
    /// 使用主线 RemoteConfig、DB.GameData 广告状态以及主线弹窗保护规则。
    /// </summary>
    public class InterstitialAdPolicy : InterstitialAdPolicyBase
    {
        public static bool isNoADForTest = false;

        protected override string PolicyName => nameof(InterstitialAdPolicy);
        protected override bool IsStateAvailable => DB.GameData != null;

        protected override long LastInterstitialDate
        {
            get => DB.GameData.LastInterstitialDate;
            set => DB.GameData.LastInterstitialDate = value;
        }

        protected override long LastRewardAdDate
        {
            get => DB.GameData.LastRewardAdDate;
            set => DB.GameData.LastRewardAdDate = value;
        }

        protected override int InterstitialFailCount
        {
            get => DB.GameData.InterstitialFailCount;
            set => DB.GameData.InterstitialFailCount = value;
        }

        protected override int InterstitialLevelSuccessCount
        {
            get => DB.GameData.InterstitialLevelSuccessCount;
            set => DB.GameData.InterstitialLevelSuccessCount = value;
        }

        protected override int GetInterstitialCD()
        {
            return ConfigManager.RemoteConfig.GetAdInterstitialCD();
        }

        protected override int GetRewardResetInterstitialCD()
        {
            return ConfigManager.RemoteConfig.GetAdRewardResetInterstitialCD();
        }

        protected override int GetInterstitialShowMinLevel()
        {
            return ConfigManager.RemoteConfig.GetAdInterstitialShowMinLevel();
        }

        protected override int GetInterstitialShowIntervalCount(int currentLevel)
        {
            return ConfigManager.RemoteConfig.GetAdInterstitialShowIntervalCount(currentLevel);
        }

        protected override int GetInterstitialShowFailedCount(int currentLevel)
        {
            return ConfigManager.RemoteConfig.GetAdInterstitialShowFailedCount(currentLevel);
        }

        protected override bool SaveState()
        {
            return DB.GameData.Save();
        }

        protected override bool ShouldBlockByBusinessFlow(
            int currentLevel,
            InterstitialPlacement placement)
        {
            if (PostcardGuideFlow.IsShow(currentLevel) &&
                placement == InterstitialPlacement.InGameVictory)
            {
                return true;
            }

            if (RatingPopupPolicy.TryToShowRating(currentLevel) &&
                placement == InterstitialPlacement.InGameVictory)
            {
                return true;
            }

            return placement == InterstitialPlacement.InGameVictory &&
                IsDailyChallengeFirstShow(currentLevel);
        }

        private static bool IsDailyChallengeFirstShow(int currentLevel)
        {
            if (!ConfigManager.RemoteConfig.GetDailyChallengeIsOpen() ||
                DB.GameData.HasShowDailyChallenge)
            {
                return false;
            }

            int unlockLevel = ConfigManager.RemoteConfig.GetDailyChallengeUnlockLevel();
            if (currentLevel < unlockLevel)
                return false;

            var passedLevels = DB.GameData?.PassedLevels;
            if (passedLevels == null)
                return false;

            return !passedLevels.Contains(currentLevel);
        }
    }

    #endregion

    #region Draw Policy

    /// <summary>
    /// DrawGame 独立插屏策略。
    /// 使用 drawGame_* RemoteConfig 与 DB.DrawGame 独立广告状态；
    /// 不读取或修改主线插屏时间、胜败计数和主线弹窗保护状态。
    /// </summary>
    public sealed class DrawInterstitialAdPolicy : InterstitialAdPolicyBase
    {
        protected override string PolicyName => nameof(DrawInterstitialAdPolicy);
        protected override bool IsStateAvailable => DB.DrawGame != null;

        protected override long LastInterstitialDate
        {
            get => DB.DrawGame.LastInterstitialDate;
            set => DB.DrawGame.LastInterstitialDate = value;
        }

        protected override long LastRewardAdDate
        {
            get => DB.DrawGame.LastRewardAdDate;
            set => DB.DrawGame.LastRewardAdDate = value;
        }

        protected override int InterstitialFailCount
        {
            get => DB.DrawGame.InterstitialFailCount;
            set => DB.DrawGame.InterstitialFailCount = value;
        }

        protected override int InterstitialLevelSuccessCount
        {
            get => DB.DrawGame.InterstitialLevelSuccessCount;
            set => DB.DrawGame.InterstitialLevelSuccessCount = value;
        }

        protected override int GetInterstitialCD()
        {
            return ConfigManager.RemoteConfig.GetDrawGameInterstitialCD();
        }

        protected override int GetRewardResetInterstitialCD()
        {
            return ConfigManager.RemoteConfig.GetDrawGameRewardResetInterstitialCD();
        }

        protected override int GetInterstitialShowMinLevel()
        {
            return ConfigManager.RemoteConfig.GetDrawGameInterstitialShowMinLevel();
        }

        protected override int GetInterstitialShowIntervalCount(int currentLevel)
        {
            return ConfigManager.RemoteConfig.GetDrawGameInterstitialShowIntervalCount(currentLevel);
        }

        protected override int GetInterstitialShowFailedCount(int currentLevel)
        {
            return ConfigManager.RemoteConfig.GetDrawGameInterstitialShowFailedCount(currentLevel);
        }

        protected override bool SaveState()
        {
            return DB.SaveDrawGame();
        }
    }

    #endregion

    #region IWanner Policy

    /// <summary>
    /// IWannerGame 独立插屏策略。
    /// 使用 iWannerGame_* RemoteConfig 与 DB.IWannerGame 独立广告状态；
    /// 最低展示门槛按 IWanner Group 在配置中的一基序号判断，不读取 GroupId。
    /// </summary>
    public sealed class IWannerInterstitialAdPolicy : InterstitialAdPolicyBase
    {
        protected override string PolicyName => nameof(IWannerInterstitialAdPolicy);
        protected override bool IsStateAvailable => DB.IWannerGame != null;

        protected override long LastInterstitialDate
        {
            get => DB.IWannerGame.LastInterstitialDate;
            set => DB.IWannerGame.LastInterstitialDate = value;
        }

        protected override long LastRewardAdDate
        {
            get => DB.IWannerGame.LastRewardAdDate;
            set => DB.IWannerGame.LastRewardAdDate = value;
        }

        protected override int InterstitialFailCount
        {
            get => DB.IWannerGame.InterstitialFailCount;
            set => DB.IWannerGame.InterstitialFailCount = value;
        }

        protected override int InterstitialLevelSuccessCount
        {
            get => DB.IWannerGame.InterstitialLevelSuccessCount;
            set => DB.IWannerGame.InterstitialLevelSuccessCount = value;
        }

        protected override int InterstitialPassedLevelCount
        {
            get => DB.IWannerGame.InterstitialPassedLevelCount;
            set => DB.IWannerGame.InterstitialPassedLevelCount = value;
        }

        protected override int GetInterstitialCD()
        {
            return ConfigManager.RemoteConfig.GetIWannerGameInterstitialCD();
        }

        protected override int GetRewardResetInterstitialCD()
        {
            return ConfigManager.RemoteConfig.GetIWannerGameRewardResetInterstitialCD();
        }

        protected override int GetInterstitialShowMinLevel()
        {
            return ConfigManager.RemoteConfig.GetIWannerGameInterstitialShowMinGroup();
        }

        protected override int GetInterstitialShowIntervalCount(int currentLevel)
        {
            return ConfigManager.RemoteConfig.GetIWannerGameInterstitialShowIntervalCount(currentLevel);
        }

        protected override int GetInterstitialShowFailedCount(int currentLevel)
        {
            return ConfigManager.RemoteConfig.GetIWannerGameInterstitialShowFailedCount(currentLevel);
        }

        protected override int GetInterstitialShowPassedLevelCount()
        {
            return ConfigManager.RemoteConfig.GetIWannerGameInterstitialShowPassedLevelCount();
        }

        protected override bool SaveState()
        {
            return DB.SaveIWannerGame();
        }
    }

    #endregion
}

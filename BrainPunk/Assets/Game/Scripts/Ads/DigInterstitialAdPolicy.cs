using Game.ConfigModule;

namespace Game
{
    #region Dig 独立插屏
    /// <summary>
    /// DigGame 独立插屏策略。
    /// 使用 digGame_* RemoteConfig 与 DB.DigGame 独立广告状态；
    /// 不读取或修改主线插屏时间、胜败计数和主线弹窗保护状态。
    /// </summary>
    public sealed class DigInterstitialAdPolicy : InterstitialAdPolicyBase
    {
        protected override string PolicyName => nameof(DigInterstitialAdPolicy);
        protected override bool IsStateAvailable => DB.DigGame != null;

        protected override long LastInterstitialDate
        {
            get => DB.DigGame.LastInterstitialDate;
            set => DB.DigGame.LastInterstitialDate = value;
        }

        protected override long LastRewardAdDate
        {
            get => DB.DigGame.LastRewardAdDate;
            set => DB.DigGame.LastRewardAdDate = value;
        }

        protected override int InterstitialFailCount
        {
            get => DB.DigGame.InterstitialFailCount;
            set => DB.DigGame.InterstitialFailCount = value;
        }

        protected override int InterstitialLevelSuccessCount
        {
            get => DB.DigGame.InterstitialLevelSuccessCount;
            set => DB.DigGame.InterstitialLevelSuccessCount = value;
        }

        protected override int GetInterstitialCD()
        {
            return ConfigManager.RemoteConfig.GetDigGameInterstitialCD();
        }

        protected override int GetRewardResetInterstitialCD()
        {
            return ConfigManager.RemoteConfig.GetDigGameRewardResetInterstitialCD();
        }

        protected override int GetInterstitialShowMinLevel()
        {
            return ConfigManager.RemoteConfig.GetDigGameInterstitialShowMinLevel();
        }

        protected override int GetInterstitialShowIntervalCount(int currentLevel)
        {
            return ConfigManager.RemoteConfig.GetDigGameInterstitialShowIntervalCount(currentLevel);
        }

        protected override int GetInterstitialShowFailedCount(int currentLevel)
        {
            return ConfigManager.RemoteConfig.GetDigGameInterstitialShowFailedCount(currentLevel);
        }

        protected override bool SaveState()
        {
            return DB.SaveDigGame();
        }
    }

    #endregion
}

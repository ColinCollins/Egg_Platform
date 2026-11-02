using System.Collections.Generic;
using Config.Game;
using Game.ConfigModule;
using UnityEngine;

namespace GameCommon
{
    /// <summary>
    /// Coin 相关的模块内容 1.5.0
    /// </summary>
    public partial class SimpleBag
    {
        private const string CoinMigrationVersion = "1.5.0";
        private const string CoinMigrationVersionKey = "SimpleBag.CoinMigrationVersion";

        /// <summary>
        /// 同步之前版本，旧版本的 GameProps.Tips 要按照比例转化为 Coin。
        /// 如果购买过无限 UnlimitTips 对象，自动获取 MaxCoin 值。
        /// </summary>
        public void SyncOldVersion()
        {
            var gameData = DB.GameData;
            if (gameData == null)
            {
                Debug.LogError("[SimpleBag] SyncOldVersion failed: GameData is null.");
                return;
            }

            if (gameData.GameTools == null)
                gameData.GameTools = new Dictionary<string, int>();

            if (gameData.RemoteConfigCache == null)
                gameData.RemoteConfigCache = new Dictionary<string, string>();

            /* if (gameData.RemoteConfigCache.TryGetValue(CoinMigrationVersionKey, out string migratedVersion) &&
                migratedVersion == CoinMigrationVersion)
            {
                return;
            } */

            int oldTipsCount = GetToolCount(GameProps.Tips);
            int currentCoinCount = GetToolCount(GameProps.Coin);
            bool hasUnlimitTips = HasTool(GameProps.UnlimitTips);

            int Tips2CoinROI = ConfigManager.RemoteConfig.GetGameTipsPopupCoinCost();
            int targetCoinCount = currentCoinCount;
            if (oldTipsCount > 0)
            {
                long convertedCoinCount = (long)oldTipsCount * Tips2CoinROI;
                long totalCoinCount = currentCoinCount + convertedCoinCount;
                targetCoinCount = (int)totalCoinCount;
            }

            bool hasChanged = false;
            if (targetCoinCount != currentCoinCount)
            {
                SetToolCount(GameProps.Coin, targetCoinCount, false);
                hasChanged = true;
            }

            if (oldTipsCount > 0)
            {
                DeleteTool(GameProps.Tips, false);
                hasChanged = true;
            }

            if (hasUnlimitTips)
            {
                DeleteTool(GameProps.UnlimitTips, false);
                SetToolCount(GameProps.UnlimitCoin, 1, false);
                hasChanged = true;
            }

            gameData.RemoteConfigCache[CoinMigrationVersionKey] = CoinMigrationVersion;
            if (!hasChanged)
            {
                DB.SaveGameData();
                return;
            }

            DB.SaveGameData();
            Debug.Log($"[SimpleBag] SyncOldVersion success. oldTips:{oldTipsCount}, currentCoin:{currentCoinCount}, targetCoin:{targetCoinCount}, hasUnlimitTips:{hasUnlimitTips}");
        }
    }
}

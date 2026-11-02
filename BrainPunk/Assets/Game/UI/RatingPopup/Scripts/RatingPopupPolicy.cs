using System;
using Game.ConfigModule;
using UnityEngine;

namespace Bear.Game
{
    /// <summary>
    /// RatingPopup 触发策略
    /// </summary>
    public static class RatingPopupPolicy
    {

        /// <summary>
        /// 检查是否应该显示评分弹窗
        /// </summary>
        public static bool TryToShowRating(int completedLevel)
        {
            // 只在通关第4关时触发一次
            if (completedLevel < ConfigManager.RemoteConfig.GetShowRatingLevel() || DB.GameData.HasRatingPopup)
                return false;

            return true;
        }

        /// <summary>
        /// 记录已显示
        /// </summary>
        public static void MarkShown()
        {
            DB.GameData.HasRatingPopup = true;
            DB.GameData.Save();
        }
    }
}

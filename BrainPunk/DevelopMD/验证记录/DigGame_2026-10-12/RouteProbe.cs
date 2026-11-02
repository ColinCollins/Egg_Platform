using System;using Config;using Config.Level;using GuruLog = Guru.SDK.Framework.Utils.Log.Log;
public static class DigRouteProbe {
        public static bool TryEnterDig(string levelPath, out string error)
        {
            GameManager manager = GameManager.Instance;
            DigGameLevelCtrl controller = manager.DigGameLevel;
            if (controller?.IsReady != true || DB.DigGame == null)
            {
                error = "Dig configuration/save is unavailable.";
                return false;
            }
            int levelId = 0;
            if (controller.TryGetLevel(DigGameLevelCtrl.TbcLevelId, out DigLevelData tbc) && Matches(levelPath, tbc.Path))
                levelId = DigGameLevelCtrl.TbcLevelId;
            foreach (DigLevelSort sort in controller.Levels)
                if (controller.TryGetLevel(sort.Id, out DigLevelData data) && Matches(levelPath, data.Path)) { levelId = sort.Id; break; }
            bool fallback = levelId == 0;
            if (fallback) levelId = controller.ResolveLowestUnpassed();
            controller.TryGetLevel(levelId, out DigLevelData target);
            LogTarget(GameType.Dig, levelPath, target?.Path, levelId, -1, fallback);
            return manager.TryEnterDigLevel(levelId, out error, allowLockedEntry: true);
        }

        private static bool Matches(string requestedPath, string configuredPath) =>
            !string.IsNullOrEmpty(requestedPath) &&
            string.Equals(requestedPath, configuredPath, StringComparison.OrdinalIgnoreCase);

        private static void LogTarget(GameType gameType, string requestedPath, string resolvedPath,
            int sortId, int levelIndex, bool fallback)
        {
            GuruLog.I($"[OneLink] stage=target_resolved, gameType={gameType}, requested={requestedPath}, " +
                      $"path={resolvedPath}, sortId={sortId}, index={levelIndex}, fallback={fallback}.");
        }


}
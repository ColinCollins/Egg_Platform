using System;
using System.Globalization;
using Bear.UI;
using Config;
using Config.Level;
using Game.ConfigModule;
using GuruLog = Guru.SDK.Framework.Utils.Log.Log;

namespace Game
{
    /// <summary>首页 OneLink 专用进关路由；使用当前已生效配置及各玩法现有进关链，不加载任意 URL 资源。</summary>
    internal static class OneLinkLevelRouter
    {
        #region 路由

        /// <summary>只在 OneLinkManager 确认首页空闲后调用；返回请求受理结果，不代表异步资源已加载成功。</summary>
        internal static bool TryEnter(OneLinkRequest request, StartPanel panel, out string error)
        {
            if (!OneLinkManager.Instance.TryBeginEntry(request.GameType, out error, request.BypassEntryHistory))
                return false;

            bool accepted = false;
            try
            {
                switch (request.GameType)
                {
                    case GameType.Default: accepted = TryEnterMain(request.LevelPath, panel, out error); break;
                    case GameType.Draw: accepted = TryEnterDraw(request.LevelPath, out error); break;
                    case GameType.IWanner: accepted = TryEnterIWanner(request.LevelPath, out error); break;
                    case GameType.Dig: accepted = TryEnterDig(request.LevelPath, out error); break;
                    default: error = "Unsupported OneLink game type."; break;
                }
                return accepted;
            }
            finally
            {
                if (!accepted)
                    OneLinkManager.Instance.EndEntry(request.GameType);
            }
        }

        #endregion

        #region 主线

        private static bool TryEnterMain(string levelPath, StartPanel panel, out string error)
        {
            LevelCtrl controller = GameManager.Instance.PlayCtrl?.Level;
            if (controller == null)
            {
                error = "Main level controller is unavailable.";
                return false;
            }

            LevelSort sort = null;
            LevelData data = null;
            foreach (LevelSort candidate in controller.LevelSorts)
            {
                if (candidate == null || candidate.Id <= 0)
                    continue;
                LevelData candidateData = controller.GetLevelDataById(candidate.Path);
                if (candidateData == null || !Matches(levelPath, candidateData.Path) ||
                    (sort != null && candidate.Id >= sort.Id))
                    continue;
                sort = candidate;
                data = candidateData;
            }

            bool fallback = sort == null;
            if (fallback && !controller.TryGetLowestUnpassedOrLastLevel(out sort, out data, out error))
                return false;
            if (string.IsNullOrWhiteSpace(data.Path))
            {
                error = "Main level resource path is empty.";
                return false;
            }

            LogTarget(GameType.Default, levelPath, data.Path, sort.Id, -1, fallback);
            // OneLink 是用户明确允许的直接解锁入口，只补齐本次目标，不改通关记录。
            controller.UnlockLevel(sort.Id);
            panel.EnterOneLinkMainLevel(sort);
            error = null;
            return true;
        }

        #endregion

        #region Draw

        private static bool TryEnterDraw(string levelPath, out string error)
        {
            GameManager manager = GameManager.Instance;
            DrawGameLevelCtrl controller = manager.DrawGameLevel;
            if (!ConfigManager.RemoteConfig.GetDrawGameIsOpen() || controller == null || !controller.IsReady ||
                DB.DrawGame?.PassedLevels == null)
            {
                error = "Draw is disabled or its configuration/save is unavailable.";
                return false;
            }

            DrawLevelSort sort = null;
            DrawLevelSort firstValid = null;
            DrawLevelSort firstUnpassed = null;
            foreach (DrawLevelSort candidate in controller.LevelSorts)
            {
                if (candidate == null || candidate.Id <= 0)
                    continue;
                DrawLevelData candidateData = controller.GetLevelDataById(candidate.Path);
                if (candidateData == null || string.IsNullOrWhiteSpace(candidateData.Path))
                    continue;

                // 与 Draw 传送门一样按最终列表顺序选取；OneLink 允许解锁，因此不筛掉锁定项。
                firstValid ??= candidate;
                if (!DB.DrawGame.PassedLevels.Contains(candidate.Id))
                    firstUnpassed ??= candidate;
                if (sort == null && Matches(levelPath, candidateData.Path))
                    sort = candidate;
            }

            bool fallback = sort == null;
            sort ??= firstUnpassed ?? firstValid;
            DrawLevelData data = sort == null ? null : controller.GetLevelDataById(sort.Path);
            if (data == null || string.IsNullOrWhiteSpace(data.Path))
            {
                error = "No matching Draw level in the current sequence.";
                return false;
            }

            LogTarget(GameType.Draw, levelPath, data.Path, sort.Id, -1, fallback);
            if (!manager.TryEnterDraw(out error))
                return false;

            DrawGame_ChoiceLevelPanel selection = UIManager.Instance.GetUI<DrawGame_ChoiceLevelPanel>();
            if (selection != null && selection.TryEnterOneLinkLevel(sort, data, out error))
                return true;

            string entryError = error ?? "Draw selection panel is unavailable.";
            if (!manager.ReturnToStartPanel(out string restoreError))
                entryError += " Restore home failed: " + restoreError;
            error = entryError;
            return false;
        }

        #endregion

        #region IWanner

        private static bool TryEnterIWanner(string levelPath, out string error)
        {
            GameManager manager = GameManager.Instance;
            IWannerGameLevelCtrl controller = manager.IWannerGameLevel;
            if (!ConfigManager.RemoteConfig.GetIWannerGameIsOpen() || controller == null || !controller.IsReady ||
                DB.IWannerGame == null)
            {
                error = "IWanner is disabled or its configuration/save is unavailable.";
                return false;
            }

            // 此入口不创建选组弹窗，须复用弹窗原有的旧档集合补齐步骤。
            if (DB.IWannerGame.EnsureValidCollections() && !DB.SaveIWannerGame())
                GuruLog.W("[OneLink] stage=save_warning, gameType=IWanner, reason=collection_repair.");

            // TBN 不属于普通 Group；直达时放行本次入口，不伪造最后一组的通关记录。
            if (Matches(levelPath, "ILevel_999"))
            {
                LogTarget(GameType.IWanner, levelPath, "ILevel_999", 0, 0, false);
                return manager.TryEnterIWannerTbn(out error, allowLockedEntry: true);
            }

            IWannerLevelSort group = null;
            IWannerLevelData data = null;
            int levelIndex = -1;
            const string groupPrefix = "IGroup_";
            int groupId = 0;
            bool isGroupTarget = levelPath?.StartsWith(groupPrefix, StringComparison.Ordinal) == true &&
                                 int.TryParse(levelPath.Substring(groupPrefix.Length), NumberStyles.None,
                                     CultureInfo.InvariantCulture, out groupId) &&
                                 groupId > 0;

            // IGroup_{id} 不指定小关，交给普通选组的同组续关／跨组首关规则；未命中仍走原回退。
            if (isGroupTarget)
                controller.TryGetGroup(groupId, out group, out _);
            else
            {
                foreach (IWannerLevelSort candidate in controller.LevelGroups)
                {
                    if (candidate == null || (group != null && candidate.Id >= group.Id) ||
                        !controller.TryGetGroup(candidate.Id, out _, out _))
                        continue;
                    for (int i = 0; i < candidate.Path.Length; i++)
                    {
                        if (!controller.TryGetLevelData(candidate.Id, i, out IWannerLevelData candidateData, out _) ||
                            !string.Equals(levelPath, candidateData.Path, StringComparison.Ordinal))
                            continue;
                        group = candidate;
                        data = candidateData;
                        levelIndex = i;
                        break;
                    }
                }
            }

            bool fallback = group == null;
            if (fallback)
            {
                foreach (IWannerLevelSort candidate in controller.LevelGroups)
                {
                    if (candidate == null || DB.IWannerGame.PassedLevels.Contains(candidate.Id) ||
                        (group != null && candidate.Id >= group.Id) ||
                        !controller.TryGetGroup(candidate.Id, out _, out _))
                        continue;
                    for (int i = 0; i < candidate.Path.Length; i++)
                    {
                        if (!controller.TryGetLevelData(candidate.Id, i, out IWannerLevelData candidateData, out _) ||
                            string.IsNullOrWhiteSpace(candidateData.Path) ||
                            (DB.IWannerGame.LevelSuccessCount.TryGetValue(candidateData.Path, out int count) && count > 0))
                            continue;
                        group = candidate;
                        data = candidateData;
                        levelIndex = i;
                        break;
                    }
                }
                if (group == null)
                {
                    LogTarget(GameType.IWanner, levelPath, "ILevel_999", 0, 0, true);
                    return manager.TryEnterIWannerTbn(out error);
                }
            }

            // 仅组号入口的 path 留空、index 为 -1；不预报原续关策略尚未解析的实际小关。
            LogTarget(GameType.IWanner, levelPath, data?.Path, group.Id, levelIndex, fallback);
            if (!DB.IWannerGame.UnlockGroups.Contains(group.Id))
            {
                DB.IWannerGame.UnlockGroups.Add(group.Id);
                if (!DB.SaveIWannerGame())
                    GuruLog.W($"[OneLink] stage=save_warning, gameType=IWanner, group={group.Id}.");
            }
            return manager.TryEnterIWannerGroup(group.Id, out error, levelIndex < 0 ? (int?)null : levelIndex);
        }

        #endregion

        #region 路径与诊断

        private static bool TryEnterDig(string levelPath, out string error)
        {
            GameManager manager = GameManager.Instance;
            DigGameLevelCtrl controller = manager.DigGameLevel;
            if (!ConfigManager.RemoteConfig.GetDigGameIsOpen() || controller?.IsReady != true || DB.DigGame == null)
            {
                error = "Dig configuration/save is unavailable.";
                return false;
            }
            int levelId = 0;
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

        #endregion
    }
}

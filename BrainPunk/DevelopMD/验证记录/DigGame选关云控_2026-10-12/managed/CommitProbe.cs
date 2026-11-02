using System;using System.Collections.Generic;using UnityEngine;using Game.ConfigModule;using GuruLog=Guru.SDK.Framework.Utils.Log.Log;
public static class CommitProbe {
 private static string pendingDrawConfig,pendingDigConfig;
 private static int pendingDrawCallbackSequence,pendingDigCallbackSequence,latestDrawCallbackSequence,latestDigCallbackSequence;
 private static bool isCacheReady=true;
 private const string DrawGameConfigKey="drawgame_config",DigGameConfigKey="diggame_config";
 private static Dictionary<string,string> latestValidConfigs=new();
 public static bool Commit(Dictionary<string,string> entries,bool callback=true)=>PersistResolvedConfigs(entries,callback);
 public static bool HasPendingDig=>pendingDigConfig!=null;
        private static bool PersistResolvedConfigs(
            IReadOnlyDictionary<string, string> resolvedConfigs,
            bool isCallbackUpdate = false,
            string source = "loading_or_manual",
            int callbackId = 0)
        {
            if (resolvedConfigs.Count == 0)
            {
                return true;
            }

            var gameManager = GameManager.IsInitialized ? GameManager.Instance : null;
            var drawLevels = gameManager?.DrawGameLevel;
            List<Config.DrawLevelSort> drawCandidate = null;
            bool refreshDraw = false;
            bool drawDeferredOrRejected = false;
            IReadOnlyDictionary<string, string> entries = resolvedConfigs;
            string trace = $"[RemoteConfigService] callback={callbackId}, source={source}, lastObservedDrawCallback={latestDrawCallbackSequence}, lastObservedDigCallback={latestDigCallbackSequence}";
            GuruLog.I($"{trace}, stage=commit_begin, keys=[{string.Join(",", resolvedConfigs.Keys)}], drawReady={drawLevels?.IsReady == true}, generation={drawLevels?.ConfigGeneration ?? 0}.");
            if (drawLevels?.IsReady == true && resolvedConfigs.TryGetValue(DrawGameConfigKey, out string drawJson))
            {
                ConfigManager.RemoteConfig.TryGetRaw(DrawGameConfigKey, out string previousJson);
                if (drawJson == previousJson)
                {
                    // 新回调可以撤销旧候选；手动请求超时后的缓存回退不能丢掉已收到的待提交更新。
                    if (isCallbackUpdate) pendingDrawConfig = null;
                    GuruLog.I($"{trace}, stage=draw_unchanged, reason=same_json, pendingCleared={isCallbackUpdate}, generation={drawLevels.ConfigGeneration}.");
                }
                else if (!drawLevels.TryPrepareRemoteConfig(drawJson, out drawCandidate, out string error))
                {
                    GuruLog.W($"{trace}, stage=draw_rejected, reason={error}; previous cache and sequence retained.");
                    if (pendingDrawConfig != null || previousJson != null)
                        latestValidConfigs[DrawGameConfigKey] = pendingDrawConfig ?? previousJson;
                    else
                        latestValidConfigs.Remove(DrawGameConfigKey);
                    drawDeferredOrRejected = true;
                }
                else if (gameManager.IsDrawRemoteConfigRefreshBlocked)
                {
                    pendingDrawConfig = drawJson;
                    pendingDrawCallbackSequence = callbackId;
                    drawDeferredOrRejected = true;
                    GuruLog.I($"{trace}, stage=draw_deferred, {gameManager.DrawRemoteConfigRefreshState}.");
                }
                else
                {
                    pendingDrawConfig = null;
                    refreshDraw = true;
                }

                if (drawDeferredOrRejected)
                {
                    var acceptedEntries = new Dictionary<string, string>();
                    foreach (var entry in resolvedConfigs)
                        if (entry.Key != DrawGameConfigKey) acceptedEntries[entry.Key] = entry.Value;
                    entries = acceptedEntries;
                }
            }
            else if (resolvedConfigs.ContainsKey(DrawGameConfigKey))
            {
                GuruLog.I($"{trace}, stage=draw_wait_initialization; cache is committed first, Loading initializes the sequence afterwards.");
            }

            var digLevels = gameManager?.DigGameLevel;
            List<Config.DigLevelSort> digCandidate = null;
            bool refreshDig = false;
            bool digDeferredOrRejected = false;
            if (digLevels?.IsReady == true && resolvedConfigs.TryGetValue(DigGameConfigKey, out string digJson))
            {
                ConfigManager.RemoteConfig.TryGetRaw(DigGameConfigKey, out string previousJson);
                if (digJson == previousJson)
                {
                    // 新回调可以撤销旧候选；手动请求超时后的缓存回退不能丢掉已收到的待提交更新。
                    if (isCallbackUpdate) pendingDigConfig = null;
                    GuruLog.I($"{trace}, stage=dig_unchanged, reason=same_json, pendingCleared={isCallbackUpdate}, generation={digLevels.ConfigGeneration}.");
                }
                else if (!digLevels.TryPrepareRemoteConfig(digJson, out digCandidate, out string error))
                {
                    GuruLog.W($"{trace}, stage=dig_rejected, reason={error}; previous cache and sequence retained.");
                    if (pendingDigConfig != null || previousJson != null)
                        latestValidConfigs[DigGameConfigKey] = pendingDigConfig ?? previousJson;
                    else
                        latestValidConfigs.Remove(DigGameConfigKey);
                    digDeferredOrRejected = true;
                }
                else if (gameManager.IsDigRemoteConfigRefreshBlocked)
                {
                    pendingDigConfig = digJson;
                    pendingDigCallbackSequence = callbackId;
                    digDeferredOrRejected = true;
                    GuruLog.I($"{trace}, stage=dig_deferred, {gameManager.DigRemoteConfigRefreshState}.");
                }
                else
                {
                    pendingDigConfig = null;
                    refreshDig = true;
                }

                if (digDeferredOrRejected)
                {
                    var acceptedEntries = new Dictionary<string, string>();
                    foreach (var entry in entries)
                        if (entry.Key != DigGameConfigKey) acceptedEntries[entry.Key] = entry.Value;
                    entries = acceptedEntries;
                }
            }
            else if (resolvedConfigs.ContainsKey(DigGameConfigKey))
            {
                GuruLog.I($"{trace}, stage=dig_wait_initialization; cache is committed first, Loading initializes the sequence afterwards.");
            }

            // 批量写入，避免多 key 场景下逐 key 落盘
            var saved = ConfigManager.RemoteConfig.TrySetRawBatch(entries);
            GuruLog.I($"{trace}, stage=cache_commit, saved={saved}, drawIncluded={entries.ContainsKey(DrawGameConfigKey)}, drawDeferredOrRejected={drawDeferredOrRejected}, digIncluded={entries.ContainsKey(DigGameConfigKey)}, digDeferredOrRejected={digDeferredOrRejected}.");
            if (!saved)
            {
                GuruLog.E($"{trace}, stage=cache_failed; previous business cache retained.");
            }
            else if (refreshDraw)
            {
                // 保存与列表切换之间不得异步让出，避免业务看到新 JSON 配旧排序。
                int previousGeneration = drawLevels.ConfigGeneration;
                drawLevels.ApplyRemoteConfig(drawCandidate);
                GuruLog.I($"{trace}, stage=draw_applied, generation={previousGeneration}->{drawLevels.ConfigGeneration}, count={drawLevels.LevelSorts.Count}; requesting existing view refresh.");
                gameManager.RefreshDrawRemoteConfigViews();
                GuruLog.I($"{trace}, stage=view_dispatch_complete; see DrawRemoteConfigUI for refreshed or skipped panels.");
            }
            if (saved && refreshDig)
            {
                // 与 Draw 相同：缓存与有效排序之间不异步让出，现存 UI 按新版本刷新。
                digLevels.ApplyRemoteConfig(digCandidate);
                gameManager.RefreshDigRemoteConfigViews();
            }
            return saved && !drawDeferredOrRejected && !digDeferredOrRejected;
        }
        internal static void TryApplyPendingDigConfig(GameManager gameManager)
        {
            if (!Application.isPlaying || pendingDigConfig == null || !isCacheReady ||
                gameManager.DigGameLevel?.IsReady != true || gameManager.IsDigRemoteConfigRefreshBlocked)
                return;

            string candidate = pendingDigConfig;
            int callbackId = pendingDigCallbackSequence;
            pendingDigConfig = null;
            GuruLog.I($"[RemoteConfigService] callback={callbackId}, stage=dig_deferred_resume, {gameManager.DigRemoteConfigRefreshState}.");
            PersistResolvedConfigs(new Dictionary<string, string> { [DigGameConfigKey] = candidate }, source: "deferred", callbackId: callbackId);
        }
}
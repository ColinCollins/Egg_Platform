
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Config;
using Config.Game;
using Cysharp.Threading.Tasks;
using GameCommon;

// 游戏事件接口
public partial class GameSDKService
{
    private const string ScreenGameView = "GameView";
    private const string DrawLevelItemCategory = "draw";
    private const string IWannerLevelItemCategory = "iwanner";

    private const int InterstitialShownIntervalLevelCount = 3;  // 插屏 间隔关卡
    private const int MinInterstitialEnableLevel = 3; // 插屏 新手保护关卡
    private const int MinBannerEnableLevel = 2; // Banner 新手保护关卡
    private const int MaxLevelSuccessReportLevelId = 50; // 最大成功关卡上报数

    private int _currentIvIntervalLevelCount = 0;
    private int _curLevelId = 0;
    private string _lastStartedLevelPath;

    public void LevelStartEvent(int status)
    {
        var playCtrl = PlayCtrl.Instance;
        var levelPath = GetCurrentLevelPathOrDefault();
        DB.GameData.LevelPlayCount.TryGetValue(levelPath, out int playCount);

        if (playCount <= 0)
            UnityEngine.Debug.LogWarning($"Wrong playcount in level: {levelPath}");

        var count = GetCombinedLevelPassedCount();
        // 关卡启动事件        
        LevelStartEvent(
            levelId: count + 1,
            levelPath: levelPath,
            playCount: playCount,
            levelStartType: status);
    }

    /// <summary>
    /// Level 启动事件
    /// </summary>
    /// <param name="levelId">关卡 ID</param>
    /// <param name="levelStartType">关卡启动类型</param>
    /// <param name="scene">当前的游戏场景</param>
    public void LevelStartEvent(int levelId,
    string levelPath,
    int playCount,
    int levelStartType = 0, string scene = "GameView")
    {
        RecordLastStartedLevelPath(levelPath);

        // 上报 Screen
        SetScreen(ScreenGameView);

        // levelStartType
        // 0: 新关卡开始
        // 1: 重新开始
        // 2: 其他定义

        _curLevelId = levelId;

        // 打点上报
        var data = new Dictionary<string, object>()
        {
            ["level"] = levelId,
            ["level_name"] = levelPath,
            ["item_category"] = "main",
            ["play_count"] = playCount
        };

        var startType = GetLevelStartType(levelStartType);
        if (!string.IsNullOrEmpty(startType))
        {
            data["start_type"] = startType;
        }

        LogEvent("level_start", data);
    }

    /// <summary>
    /// 上报 Draw 关卡启动事件。
    /// 调用边界：仅由 DrawPlayCtrl 在新关卡进入、Reset 或 VictoryPanel Replay 已确认后调用。
    /// 注意事项：level 使用主线、DrawGame、IWannerGame 与 DigGame 的累计通关次数生成；start_type 由当前关卡是否已通关决定，不修改主线关卡的 _curLevelId。
    /// </summary>
    /// <param name="levelPath">DrawLevelData.Path。</param>
    /// <param name="playCount">当前关卡累计进入与重试次数。</param>
    /// <param name="levelStartType">关卡启动类型。</param>
    public void DrawLevelStartEvent(
        string levelPath,
        int playCount,
        int levelStartType = 0)
    {
        RecordLastStartedLevelPath(levelPath);
        SetScreen(ScreenGameView);

        int level = GetCombinedLevelPassedCount() + 1;
        var data = new Dictionary<string, object>()
        {
            ["level"] = level,
            ["level_name"] = levelPath,
            ["item_category"] = DrawLevelItemCategory,
            ["play_count"] = playCount
        };

        var startType = GetLevelStartType(levelStartType);
        if (!string.IsNullOrEmpty(startType))
            data["start_type"] = startType;

        LogEvent("level_start", data);
    }

    /// <summary>
    /// 上报 IWanner 普通小关或 TBN 的启动事件。
    /// 调用边界：仅由 IWannerLevelRuntimeStatistics 在可玩实例即将开放输入前调用。
    /// 注意事项：level 使用主线、DrawGame、IWannerGame 与 DigGame 的累计通关次数生成；不修改主线关卡的 _curLevelId。
    /// </summary>
    /// <param name="levelPath">IWannerLevelData.Path；TBN 使用 ILevel_999。</param>
    /// <param name="playCount">当前小关累计进入与重试次数。</param>
    /// <param name="levelStartType">由当前小关是否已有成功记录决定的启动类型。</param>
    public void IWannerLevelStartEvent(
        string levelPath,
        int playCount,
        int levelStartType = 0)
    {
        RecordLastStartedLevelPath(levelPath);
        SetScreen(ScreenGameView);

        int level = GetCombinedLevelPassedCount() + 1;
        var data = new Dictionary<string, object>()
        {
            ["level"] = level,
            ["level_name"] = levelPath,
            ["item_category"] = IWannerLevelItemCategory,
            ["play_count"] = playCount
        };

        var startType = GetLevelStartType(levelStartType);
        if (!string.IsNullOrEmpty(startType))
            data["start_type"] = startType;

        LogEvent("level_start", data);
    }

    private string GetLevelStartType(int levelStartType)
    {
        return levelStartType switch
        {
            LevelStartTypeNewGame => "play",
            LevelStartTypeReplay => "replay",
            LevelStartTypeContinue => "continue",
            _ => ""
        };
    }


    /*     private async UniTask DelayShowBanner()
        {
            if (BannerIsShown) return;
            await UniTask.Delay(TimeSpan.FromSeconds(2));
            ShowBanner();
        } */

    public void LevelEndEvent(int status)
    {
        var playCtrl = PlayCtrl.Instance;
        var levelPath = GetCurrentLevelPathOrDefault();

        // 记录时间
        playCtrl.Level.CurrentLevelState.RecordLevelDuration();
        DB.GameData.LevelPlayDuration.TryGetValue(levelPath, out var totalDuration);
        var count = GetCombinedLevelPassedCount();

        var level = playCtrl.Level;
        var passedCount = status == GameSDKService.LevelEndTypeSuccess ? count : count + 1;
        var levelId = level.CurrentLevelState.LevelId;
        var duration = level?.CurrentLevelState != null
            ? level.CurrentLevelState.GetCurrentLevelTimeSeconds()
            : 0L;
        var reviveCount = level?.CurrentLevelState != null
            ? level.CurrentLevelState.ResetCount + level.CurrentLevelState.FailCount
            : 0;

        // totoal Duration 暂时用 durtion，因为BI 打点统计的规范问题    
        LevelEndEvent(
            levelId: levelId,
            passedCount: passedCount,
            levelPath: levelPath,
            duration: duration,
            totalDuration: totalDuration,
            reviveCount: reviveCount,
            levelEndType: status);
    }

    /// <summary>
    /// 关卡结束事件
    /// </summary>
    /// <param name="levelId"></param>
    /// <param name="levelEndType"></param>
    /// <param name="scene"></param>
    /// <param name="duration">单局游戏时长</param>
    /// <param name="totalDuration">这关总时长</param>
    /// 
    public void LevelEndEvent(int levelId,
    int passedCount,
    string levelPath,
    long duration,
    long totalDuration,
    int reviveCount,
    int levelEndType = 0, string scene = "GameView")
    {
        // levelEndType
        // 0: 关卡成功
        // 1: 关卡失败
        // 2: 其他定义

        // HideBanner();

        // TODO: 这里只放打点范例, 并非最终打点参数, 项目组需要自己补全
        // 上报 Level End
        var data = new Dictionary<string, object>()
        {
            ["level"] = passedCount,
            ["level_name"] = levelPath,
            ["item_category"] = "main",
            ["duration"] = duration,
            ["level_duration"] = duration,
            ["revive"] = reviveCount,
            ["total_duration"] = totalDuration
        };

        var leveResult = GetLevelResult(levelEndType);
        if (!string.IsNullOrEmpty(leveResult))
        {
            data["result"] = leveResult;
        }

        // 打点上报
        LogEvent("level_end", data);

        if (levelEndType == LevelEndTypeSuccess)
        {
            UpdateBPlay();
            var count = GetCombinedLevelPassedCount();
            LevelEndSuccessEvent(count, levelId);
        }
    }

    /// <summary>
    /// 上报 Draw 关卡结束事件。
    /// 调用边界：仅由 DrawPlayCtrl 在 Reset、成功结算或离开当前玩法时调用。
    /// 注意事项：level 使用主线、DrawGame、IWannerGame 与 DigGame 的累计通关次数生成；成功时继续发送 level_end_success_N 买量事件。
    /// </summary>
    /// <param name="levelDataId">当前 DrawLevelData.Id；仅成功时用作 level_end_success_N 的实际关卡 ID。</param>
    /// <param name="levelPath">DrawLevelData.Path。</param>
    /// <param name="duration">当前进入周期内该关累计游玩时长，单位毫秒。</param>
    /// <param name="totalDuration">该关历史累计游玩时长，单位毫秒。</param>
    /// <param name="reviveCount">当前关卡运行周期内的主动重开与失败次数之和。</param>
    /// <param name="levelEndType">关卡结束类型。</param>
    public void DrawLevelEndEvent(
        string levelDataId,
        string levelPath,
        long duration,
        long totalDuration,
        int reviveCount,
        int levelEndType = 0)
    {
        int combinedPassedCount = GetCombinedLevelPassedCount();
        int level = levelEndType == LevelEndTypeSuccess
            ? combinedPassedCount
            : combinedPassedCount + 1;
        var data = new Dictionary<string, object>()
        {
            ["level"] = level,
            ["level_name"] = levelPath,
            ["item_category"] = DrawLevelItemCategory,
            ["duration"] = duration,
            ["level_duration"] = duration,
            ["revive"] = reviveCount,
            ["total_duration"] = totalDuration
        };

        var levelResult = GetLevelResult(levelEndType);
        if (!string.IsNullOrEmpty(levelResult))
            data["result"] = levelResult;

        LogEvent("level_end", data);

        if (levelEndType == LevelEndTypeSuccess)
        {
            UpdateBPlay();

            if (int.TryParse(levelDataId, out int drawLevelId) && drawLevelId > 0)
            {
                LevelEndSuccessEvent(combinedPassedCount, drawLevelId);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    $"Draw level_end_success skipped because DrawLevelData.Id is invalid: {levelDataId}");
            }
        }
    }

    /// <summary>
    /// 上报 IWanner 小关结束事件。
    /// 调用边界：仅由 IWannerLevelRuntimeStatistics 在失败、成功或退出当前小关时调用。
    /// 注意事项：成功次数必须在调用前写入 IWanner 存档；成功时使用实际 IWannerLevelData.Id 派生 level_end_success_N。
    /// </summary>
    /// <param name="levelDataId">当前 IWannerLevelData.Id；TBN 无配置数据时为空。</param>
    /// <param name="levelPath">当前小关资源 Path。</param>
    /// <param name="duration">当前尝试时长，单位毫秒。</param>
    /// <param name="totalDuration">当前小关历史累计时长，单位毫秒。</param>
    /// <param name="reviveCount">当前小关运行周期内累计失败与手动 Reset 次数。</param>
    /// <param name="levelEndType">成功、失败或退出。</param>
    public void IWannerLevelEndEvent(
        string levelDataId,
        string levelPath,
        long duration,
        long totalDuration,
        int reviveCount,
        int levelEndType = 0)
    {
        int combinedPassedCount = GetCombinedLevelPassedCount();
        int level = levelEndType == LevelEndTypeSuccess
            ? combinedPassedCount
            : combinedPassedCount + 1;
        var data = new Dictionary<string, object>()
        {
            ["level"] = level,
            ["level_name"] = levelPath,
            ["item_category"] = IWannerLevelItemCategory,
            ["duration"] = duration,
            ["level_duration"] = duration,
            ["revive"] = reviveCount,
            ["total_duration"] = totalDuration
        };

        var levelResult = GetLevelResult(levelEndType);
        if (!string.IsNullOrEmpty(levelResult))
            data["result"] = levelResult;

        LogEvent("level_end", data);

        if (levelEndType != LevelEndTypeSuccess)
            return;

        UpdateBPlay();
        if (int.TryParse(levelDataId, out int iwannerLevelId) && iwannerLevelId > 0)
        {
            LevelEndSuccessEvent(combinedPassedCount, iwannerLevelId);
            return;
        }

        UnityEngine.Debug.LogWarning(
            $"IWanner level_end_success skipped because IWannerLevelData.Id is invalid: {levelDataId}");
    }

    // 买量点上报
    private void LevelEndSuccessEvent(int count, int levelId)
    {
        if (count > MaxLevelSuccessReportLevelId)
            return;


        LogEvent($"level_end_success_{count}", new Dictionary<string, object>()
        {
            ["level"] = levelId,
        });
    }



    private string GetLevelResult(int levelEndType)
    {
        return levelEndType switch
        {
            LevelEndTypeSuccess => "success",
            LevelEndTypeFailed => "fail",
            LevelEndTypeExit => "exit",
            _ => ""
        };
    }

    /// <summary>
    /// 获得灯泡
    /// </summary>
    public void BulbsGet(int value, int oldCount, int newCount, RewardType type)
    {
        var (levelPath, totalDuration, reviveCount) = GetLevelRuntimeContext();

        var data = new Dictionary<string, object>
        {
            ["level_name"] = levelPath,
            ["item_category"] = type.ToString(),
            ["value"] = value,
            ["balance"] = newCount,
            ["balance_before"] = oldCount,
            ["revive"] = reviveCount,
            ["level_duration"] = totalDuration,
        };

        LogEvent("bulbs_get", data);
    }

    /// <summary>
    /// 灯泡消耗
    /// </summary>
    public void BulbsCost(int value, int oldCount, int newCount, CostType type)
    {
        var (levelPath, totalDuration, reviveCount) = GetLevelRuntimeContext();

        var data = new Dictionary<string, object>
        {
            ["level_name"] = levelPath,
            ["item_category"] = type.ToString(),
            ["value"] = value,
            ["balance"] = newCount,
            ["balance_before"] = oldCount,
            ["revive"] = reviveCount,
            ["level_duration"] = totalDuration,
        };

        LogEvent("bulbs_cost", data);
    }

    /// <summary>
    /// 获取当前关卡用于打点的基础上下文
    /// </summary>
    private (string levelPath, long totalDuration, int reviveCount) GetLevelRuntimeContext()
    {
        var levelPath = GetCurrentLevelPathOrDefault();

        DB.GameData.LevelPlayDuration.TryGetValue(levelPath, out var totalDuration);
        var runtimeState = PlayCtrl.Instance?.Level?.CurrentLevelState;
        int reviveCount = runtimeState == null
            ? 0
            : runtimeState.ResetCount + runtimeState.FailCount;

        return (levelPath, totalDuration, reviveCount);
    }

    /// <summary>
    /// 安全获取当前关卡路径，避免 CurrentLevelData 为空导致异常
    /// </summary>
    private string GetCurrentLevelPathOrDefault()
    {
        var playCtrl = PlayCtrl.Instance;
        var level = playCtrl?.Level;
        var levelData = level?.CurrentLevelData;
        if (levelData == null)
        {
            UnityEngine.Debug.LogWarning("CurrentLevelData is null when reporting game analytics. Use 'no_start' as level path.");
            return "no_start";
        }

        return levelData.Path;
    }

    private void RecordLastStartedLevelPath(string levelPath)
    {
        if (!string.IsNullOrEmpty(levelPath))
            _lastStartedLevelPath = levelPath;
    }

    private string GetLastStartedLevelPathOrDefault()
    {
        return string.IsNullOrEmpty(_lastStartedLevelPath)
            ? GetCurrentLevelPathOrDefault()
            : _lastStartedLevelPath;
    }

    /// <summary>
    /// 获取道具或者金币时候
    /// </summary>
    /// <param name="prop"></param>
    /// <param name="value"></param>
    /// <param name="type"></param>
    /// <param name="newCount"></param>
    public void EarnVirtualCurrentcy(GameProps prop, int value, RewardType type, int newCount)
    {
        var levelPath = GetLastStartedLevelPathOrDefault();

        var data = new Dictionary<string, object>()
        {
            ["virtual_currency_name"] = prop.ToString(),
            ["item_category"] = type == RewardType.Purchase ? "iap_buy" : type.ToString(),
            ["level_name"] = levelPath,
            ["value"] = value,
            ["balance"] = newCount,
        };

        LogEvent("earn_virtual_currency", data);
    }

    /// <summary>
    /// 获取道具或者金币时候
    /// </summary>
    /// <param name="prop"></param>
    /// <param name="value"></param>
    /// <param name="type"></param>
    /// <param name="newCount"></param>
    public void SpendVirtualCurrentcy(GameProps prop, int value, CostType type, int newCount)
    {
        var levelPath = GetLastStartedLevelPathOrDefault();

        var data = new Dictionary<string, object>()
        {
            ["virtual_currency_name"] = prop.ToString(),
            ["item_category"] = type.ToString(),
            ["level_name"] = levelPath,
            ["value"] = value,
            ["balance"] = newCount,
        };

        LogEvent("spend_virtual_currency", data);
    }

    public void Rating_Star(int star, int status)
    {
        var data = new Dictionary<string, object>()
        {
            ["star"] = star.ToString(),
            ["status"] = status.ToString(),
        };
        LogEvent("rating_star", data);
    }

    /// <summary>
    ///  胜利评价
    /// </summary>
    /// <param name="isLike"></param>
    public void LevelVictoryReaction(string levelPath, bool isLike)
    {
        var data = new Dictionary<string, object>()
        {
            ["isLike"] = isLike,
            ["level_name"] = levelPath,
        };
        LogEvent("victory_reaction", data);
    }

    /// <summary>
    /// 获取方式:1. purchase 2. Ad 3. Postcard
    /// </summary>
    /// <param name="skinData"></param>
    /// <param name="getType"></param>
    public void GetNewSkin(string skinId, GetSkinWayType getType)
    {
        var data = new Dictionary<string, object>()
        {
            ["skin_id"] = skinId,
            ["getType"] = getType
        };

        LogEvent("getNewSkin", data);
    }

    /// <summary>
    /// 上报玩家通过 TBC 关卡传送门触发玩法跳转。
    /// 调用边界：仅在 Level_999 的目标玩法进关请求已被接受后调用，失败或被门禁拦截时不得上报。
    /// </summary>
    /// <param name="type">传送门所属玩法：0 主线，1 DrawGame，2 IWannerGame。</param>
    public void EnterTBCPortal(int type)
    {
        var data = new Dictionary<string, object>()
        {
            ["type"] = type,
        };

        LogEvent("enter_tbc_portal", data);
    }



    #region Obsolete
    //--------- 局间插屏显示 Obsolete 不用这个 ---------------

    private void TryShowLevelInterstitialAd(string placement = AdsSceneGameStart)
    {
        ShowInterstitial(placement, (scene, success) =>
        {
            // TODO:插屏展示完毕, 项目组可根据结果处理对用的回调
        }, GameInterstitialValidator);
    }


    /// <summary>
    /// 插屏验证器, 项目组自行加入扩展
    /// </summary>
    /// <returns></returns>
    private UniTask<bool> GameInterstitialValidator()
    {
        // 新手保护
        if (_curLevelId < MinInterstitialEnableLevel)
            return UniTask.FromResult(false);

        // 局间计数不足
        if (_currentIvIntervalLevelCount < InterstitialShownIntervalLevelCount)
        {
            _currentIvIntervalLevelCount++; // 关卡间隔数递增
            return UniTask.FromResult(false);
        }

        // 条件判定成功, 可显示插屏
        _currentIvIntervalLevelCount = 0; // 标记清零
        return UniTask.FromResult(true);
    }

    #endregion
}

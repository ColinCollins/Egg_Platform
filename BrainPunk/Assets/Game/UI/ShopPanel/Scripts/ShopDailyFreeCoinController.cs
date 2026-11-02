using System;
using Bear.Logger;
using Bear.SaveModule;
using Cysharp.Threading.Tasks;
using Game.ConfigModule;
using Game.Scripts.Common;
using GameCommon;
using TMPro;
using UnityEngine;

/// <summary>
/// ShopPanel 每日免费金币卡片控制器。
/// 独立负责配置展示、当地自然日资格判断、领取落盘、倒计时与金币飞行动画。
/// </summary>
public sealed class ShopDailyFreeCoinController : MonoBehaviour, IDebuger
{
    #region Constants

    private const string WaitingCooldownTipKey = "U_Shop_WaitingCD_01";

    #endregion

    #region Inspector

    [Header("Interaction")]
    [Tooltip("每日免费金币卡片点击入口；必须绑定 DailyCoin/Root 上的 CustomButton。")]
    [SerializeField] private CustomButton claimButton;

    [Tooltip("所属 ShopPanel；仅用于领取成功后抑制旧的关闭商店奖励提示。")]
    [SerializeField] private ShopPanel ownerPanel;

    [Header("Presentation")]
    [Tooltip("金币数量文本，运行时优先显示 level_config.dailyFreeCoinCount，缺失时回退 GlobalConst。")]
    [SerializeField] private TMP_Text rewardCountText;

    [Tooltip("可领取时显示的 Free 文案根节点。")]
    [SerializeField] private GameObject freeStateRoot;

    [Tooltip("冷却期间显示到下一当地自然日 00:00 的倒计时。")]
    [SerializeField] private TMP_Text countdownText;

    [Tooltip("领取成功后播放金币飞行动画的公共控制器。")]
    [SerializeField] private CommonUIRewardAnimCtrl rewardAnimCtrl;

    [Tooltip("单次领取表现生成的飞行金币数量，与实际发奖数量解耦。")]
    [SerializeField] private int rewardAnimCount = 8;

    #endregion

    #region Runtime State

    private bool _isAvailable;
    private bool _hasRefreshedState;
    private long _nextClaimTimestamp;
    private long _lastRenderedCountdownSecond = long.MinValue;
    private float _nextTickTime;
    private bool _hasLoggedClockChange;
    private bool _isRuntimeActive;
    private long _lastKnownClaimTimestamp = long.MinValue;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        _isRuntimeActive = false;
        if (DailyGiftCoinRules.IsRewardDisabled(ConfigManager.Instance))
        {
            gameObject.SetActive(false);
            return;
        }

        _isRuntimeActive = true;
        BindButton();
        RecordObservedTime();
        RefreshState();
    }

    private void OnDisable()
    {
        if (_isRuntimeActive)
        {
            RecordObservedTime();
        }

        _isRuntimeActive = false;
        UnbindButton();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            RecordObservedTime();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || Time.unscaledTime < _nextTickTime)
        {
            return;
        }

        _nextTickTime = Time.unscaledTime + 1f;
        long currentClaimTimestamp = DB.GameSchedule?.DailyFreeCoinLastClaimTime ?? 0;
        if (currentClaimTimestamp != _lastKnownClaimTimestamp)
        {
            RefreshState();
            return;
        }

        if (_isAvailable)
        {
            return;
        }

        long nowTimestamp = GetGuardedCurrentTimestamp();
        if (_nextClaimTimestamp <= 0 || nowTimestamp >= _nextClaimTimestamp)
        {
            RefreshState(nowTimestamp);
            return;
        }

        RefreshCountdown(_nextClaimTimestamp - nowTimestamp);
    }

    private void OnValidate()
    {
        rewardAnimCount = Mathf.Max(1, rewardAnimCount);
    }

    #endregion

    #region Claim Flow

    private void TryClaim(CustomButton button)
    {
        ConfigManager configManager = ConfigManager.Instance;
        GameSchedule schedule = DB.GameSchedule;
        long claimTimestamp = GetGuardedCurrentTimestamp();
        bool hasState = DailyGiftCoinRules.TryGetState(
                configManager,
                schedule,
                claimTimestamp,
                out int rewardCount,
                out bool isAvailable,
                out _);
        if (!hasState)
        {
            RefreshState(claimTimestamp);
            return;
        }

        if (!isAvailable)
        {
            RefreshState(claimTimestamp);
            SystemTips.Show(I2.Loc.LocalizationManager.GetTranslation(WaitingCooldownTipKey));
            return;
        }

        var bag = GameManager.Instance?.Bag;
        if (schedule == null || bag == null)
        {
            this.LogError("[ShopDailyFreeCoinController] Claim failed. GameSchedule or Bag is unavailable.");
            return;
        }

        long previousClaimTimestamp = schedule.DailyFreeCoinLastClaimTime;
        long previousObservedTimestamp = schedule.DailyFreeCoinLastObservedTime;
        schedule.DailyFreeCoinLastClaimTime = claimTimestamp;
        schedule.DailyFreeCoinLastObservedTime = Math.Max(previousObservedTimestamp, claimTimestamp);
        if (!DB.SaveGameSchedule())
        {
            schedule.DailyFreeCoinLastClaimTime = previousClaimTimestamp;
            schedule.DailyFreeCoinLastObservedTime = previousObservedTimestamp;
            this.LogError("[ShopDailyFreeCoinController] Claim failed. Claim timestamp was not saved.");
            return;
        }

        int coinCountBeforeDelivery = bag.GetToolCount(Config.Game.GameProps.Coin);
        int coinCountAfterDelivery = bag.AddTool(
            Config.Game.GameProps.Coin,
            rewardCount,
            RewardType.Other);
        if (coinCountAfterDelivery < (long)coinCountBeforeDelivery + rewardCount)
        {
            RollbackClaimTimestamp(schedule, previousClaimTimestamp);
            RefreshState();
            return;
        }

        ownerPanel?.SuppressRewardTipAutoShowOnClose();
        RefreshState(claimTimestamp);
        AudioManager.PlaySound("shopGetReward");
        PlayRewardAnimationAsync().Forget();
        this.Log($"[ShopDailyFreeCoinController] Claimed. count:{rewardCount}, timestamp:{claimTimestamp}");
    }

    private void RollbackClaimTimestamp(GameSchedule schedule, long previousClaimTimestamp)
    {
        schedule.DailyFreeCoinLastClaimTime = previousClaimTimestamp;
        if (!DB.SaveGameSchedule())
        {
            this.LogError("[ShopDailyFreeCoinController] Coin delivery failed and claim timestamp rollback could not be saved.");
            return;
        }

        this.LogError("[ShopDailyFreeCoinController] Coin delivery failed. Claim timestamp was rolled back.");
    }

    private async UniTask PlayRewardAnimationAsync()
    {
        if (rewardAnimCtrl == null || claimButton == null)
        {
            this.LogWarning("[ShopDailyFreeCoinController] Reward animation skipped. Controller or source is missing.");
            return;
        }

        CommonUIRewardPlayResult result = await rewardAnimCtrl.PlayFrom(
            claimButton.transform,
            rewardAnimCount);
        if (result != CommonUIRewardPlayResult.Completed)
        {
            this.LogWarning($"[ShopDailyFreeCoinController] Reward animation did not complete. result:{result}");
        }
    }

    #endregion

    #region State And Countdown

    private void RefreshState()
    {
        RefreshState(GetGuardedCurrentTimestamp());
    }

    private void RefreshState(long nowTimestamp)
    {
        bool previousAvailability = _isAvailable;
        GameSchedule schedule = DB.GameSchedule;
        _lastKnownClaimTimestamp = schedule?.DailyFreeCoinLastClaimTime ?? 0;
        bool hasState = DailyGiftCoinRules.TryGetState(
            ConfigManager.Instance,
            schedule,
            nowTimestamp,
            out int rewardCount,
            out _isAvailable,
            out _nextClaimTimestamp);

        if (rewardCountText != null)
        {
            rewardCountText.text = $"x{Mathf.Max(0, rewardCount)}";
        }

        if (!hasState)
        {
            _isAvailable = false;
            _nextClaimTimestamp = 0;
        }

        _lastRenderedCountdownSecond = long.MinValue;
        _nextTickTime = Time.unscaledTime + 1f;

        if (claimButton != null)
        {
            claimButton.Interactable = hasState;
        }

        if (freeStateRoot != null)
        {
            freeStateRoot.SetActive(_isAvailable);
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(!_isAvailable);
            if (!_isAvailable)
            {
                long remainingSeconds = _nextClaimTimestamp > 0
                    ? _nextClaimTimestamp - nowTimestamp
                    : 0;
                RefreshCountdown(remainingSeconds);
            }
        }

        if (!_hasRefreshedState || previousAvailability != _isAvailable)
        {
            _hasRefreshedState = true;
            RedDotManager.Refresh(RedDotKey.DailyGiftCoin);
        }
    }

    private void RefreshCountdown(long remainingSeconds)
    {
        if (countdownText == null)
        {
            return;
        }

        remainingSeconds = Math.Max(0, remainingSeconds);
        if (_lastRenderedCountdownSecond == remainingSeconds)
        {
            return;
        }

        _lastRenderedCountdownSecond = remainingSeconds;
        long hours = Math.Min(99, remainingSeconds / 3600);
        long minutes = remainingSeconds % 3600 / 60;
        long seconds = remainingSeconds % 60;
        countdownText.text = $"{hours:00}:{minutes:00}:{seconds:00}";
    }

    private long GetGuardedCurrentTimestamp()
    {
        long timestamp = DailyGiftCoinClockGuard.GetCurrentTimestamp(out bool deviceClockChanged);
        if (deviceClockChanged && !_hasLoggedClockChange)
        {
            _hasLoggedClockChange = true;
            this.LogWarning("[ShopDailyFreeCoinController] Device clock change detected. Using monotonic session time.");
        }

        return timestamp;
    }

    private void RecordObservedTime()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        GameSchedule schedule = DB.GameSchedule;
        if (schedule == null)
        {
            return;
        }

        long observedTimestamp = GetGuardedCurrentTimestamp();
        if (observedTimestamp <= schedule.DailyFreeCoinLastObservedTime)
        {
            return;
        }

        long previousObservedTimestamp = schedule.DailyFreeCoinLastObservedTime;
        schedule.DailyFreeCoinLastObservedTime = observedTimestamp;
        if (!DB.SaveGameSchedule())
        {
            schedule.DailyFreeCoinLastObservedTime = previousObservedTimestamp;
            this.LogError("[ShopDailyFreeCoinController] Failed to save the daily gift time high-water mark.");
        }
    }

    #endregion

    #region Binding

    private void BindButton()
    {
        if (claimButton == null)
        {
            return;
        }

        claimButton.OnClick -= TryClaim;
        claimButton.OnClick += TryClaim;
    }

    private void UnbindButton()
    {
        if (claimButton != null)
        {
            claimButton.OnClick -= TryClaim;
        }
    }

    #endregion
}

/// <summary>
/// 每日免费金币的只读规则集合，供卡片控制器和红点 checker 共享同一份配置与自然日口径。
/// </summary>
internal static class DailyGiftCoinRules
{
    #region Query

    /// <summary>
    /// 判断每日免费金币是否被非正数配置关闭。
    /// 调用边界：仅在配置已就绪且数量小于等于 0 时返回 true；配置未就绪时交由原状态刷新流程处理。
    /// </summary>
    internal static bool IsRewardDisabled(ConfigManager configManager)
    {
        return TryGetConfiguredRewardCount(configManager, out int rewardCount) && rewardCount <= 0;
    }

    /// <summary>
    /// 读取奖励数量并计算当前领取资格与下次可领取时间。
    /// 调用边界：只读 Config 和 GameSchedule，不写存档、不发奖励、不触发资源加载。
    /// </summary>
    internal static bool TryGetState(
        ConfigManager configManager,
        GameSchedule schedule,
        long nowTimestamp,
        out int rewardCount,
        out bool isAvailable,
        out long nextClaimTimestamp)
    {
        rewardCount = 0;
        isAvailable = false;
        nextClaimTimestamp = 0;

        if (schedule == null || !TryGetConfiguredRewardCount(configManager, out rewardCount))
        {
            return false;
        }

        if (rewardCount <= 0)
        {
            return false;
        }

        isAvailable = IsAvailable(
            schedule.DailyFreeCoinLastClaimTime,
            schedule.DailyFreeCoinLastObservedTime,
            nowTimestamp,
            out nextClaimTimestamp);
        return true;
    }

    private static bool TryGetConfiguredRewardCount(ConfigManager configManager, out int rewardCount)
    {
        rewardCount = 0;
        if (configManager == null || !configManager.IsInitialized)
        {
            return false;
        }

        rewardCount = ConfigManager.RemoteConfig.GetDailyFreeCoinCount();
        return true;
    }

    #endregion

    #region Local Day Rule

    private static bool IsAvailable(
        long lastClaimTimestamp,
        long lastObservedTimestamp,
        long nowTimestamp,
        out long nextClaimTimestamp)
    {
        nextClaimTimestamp = 0;
        if (lastObservedTimestamp > 0 && nowTimestamp < lastObservedTimestamp)
        {
            nextClaimTimestamp = lastObservedTimestamp;
            return false;
        }

        if (lastClaimTimestamp <= 0)
        {
            return true;
        }

        try
        {
            TimeZoneInfo localTimeZone = TimeZoneInfo.Local;
            DateTime lastClaimLocalDate = TimeZoneInfo.ConvertTime(
                DateTimeOffset.FromUnixTimeSeconds(lastClaimTimestamp),
                localTimeZone).Date;
            DateTime nextClaimLocalDate = lastClaimLocalDate.AddDays(1);
            TimeSpan nextClaimOffset = localTimeZone.GetUtcOffset(nextClaimLocalDate);
            nextClaimTimestamp = new DateTimeOffset(nextClaimLocalDate, nextClaimOffset).ToUnixTimeSeconds();
            return nowTimestamp >= nextClaimTimestamp;
        }
        catch (ArgumentOutOfRangeException)
        {
            nextClaimTimestamp = long.MaxValue;
            return false;
        }
    }

    #endregion
}

/// <summary>
/// 每日免费金币当前进程内的单调时间保护。
/// 设备墙钟发生明显跳变时继续使用应用启动后的实际流逝时间，避免运行中改时间跨日领取。
/// </summary>
internal static class DailyGiftCoinClockGuard
{
    #region Constants

    private const long AllowedClockDriftSeconds = 60;

    #endregion

    #region Runtime State

    private static bool _isInitialized;
    private static long _sessionStartWallTimestamp;
    private static double _sessionStartRealtime;

    #endregion

    #region Query

    /// <summary>
    /// 返回当前进程的有效 Unix 秒时间戳。
    /// 调用边界：只读系统时间与 Unity 单调计时，不写存档；首次调用建立本进程基线。
    /// </summary>
    internal static long GetCurrentTimestamp(out bool deviceClockChanged)
    {
        long wallTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        double realtime = Time.realtimeSinceStartupAsDouble;
        if (!_isInitialized)
        {
            _isInitialized = true;
            _sessionStartWallTimestamp = wallTimestamp;
            _sessionStartRealtime = realtime;
            deviceClockChanged = false;
            return wallTimestamp;
        }

        long elapsedSeconds = Math.Max(0L, (long)(realtime - _sessionStartRealtime));
        long monotonicTimestamp = _sessionStartWallTimestamp + elapsedSeconds;
        deviceClockChanged = Math.Abs(wallTimestamp - monotonicTimestamp) > AllowedClockDriftSeconds;
        return deviceClockChanged ? monotonicTimestamp : wallTimestamp;
    }

    #endregion
}

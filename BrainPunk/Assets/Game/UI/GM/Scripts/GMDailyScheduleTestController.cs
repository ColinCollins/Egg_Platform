using System;
using System.Collections.Generic;
using System.Text;
using Bear.Logger;
using Bear.SaveModule;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Debug-only controller for validating DailyScheduleModule behavior from the GM panel.
/// </summary>
public sealed class GMDailyScheduleTestController : MonoBehaviour, IDebuger
{
    private enum ButtonAction
    {
        PrepareCrossDay,
        QuickCheckNow
    }

    private const string DailyModuleKey = "Daily";
    private const string PassColor = "#00C853";
    private const string FailColor = "#FF3B30";

    [Header("View")]
    [SerializeField] private TextMeshProUGUI debugText;

    [Header("Buttons")]
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private CustomButton buttonTemplate;

    [Header("Behavior")]
    [SerializeField] private bool refreshViewOnEnable = true;
    [SerializeField] private float realtimeRefreshInterval = 0.25f;

    private readonly List<CustomButton> _generatedButtons = new();
    private readonly Dictionary<ButtonAction, CustomButton> _buttonMap = new();
    private bool _isPrepared;
    private string _lastAction = "空闲";
    private float _nextRefreshRealtime;

    private void OnEnable()
    {
        RebuildButtons();
        EnableRichText();
        _nextRefreshRealtime = 0f;

        if (refreshViewOnEnable)
        {
            RefreshView();
        }
    }

    private void Update()
    {
        if (!refreshViewOnEnable)
        {
            return;
        }

        if (realtimeRefreshInterval > 0f && Time.unscaledTime < _nextRefreshRealtime)
        {
            return;
        }

        _nextRefreshRealtime = Time.unscaledTime + Mathf.Max(0.05f, realtimeRefreshInterval);
        RefreshView();
    }

    private void OnDisable()
    {
        ClearGeneratedButtons();
    }

    /// <summary>
    /// Refreshes the GM text and button states from current schedule data.
    /// </summary>
    public void RefreshView()
    {
        EnableRichText();

        if (debugText != null)
        {
            debugText.text = BuildDebugText();
            debugText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(debugText.rectTransform);
            Canvas.ForceUpdateCanvases();
        }
    }

    /// <summary>
    /// 一键模拟玩家跨到下一个当地自然日：回退 Daily、ChestGift 当日分配日期与商店免费金币领取日期，
    /// 执行完整 Daily 刷新、刷新免费金币资格后直接打开刷新后的 ChestGift，供运行态联调。
    /// 注意事项：该入口会真实保存调试数据；商品配置、永久购买次数与功能开关仍按当前数据校验。
    /// </summary>
    public void PrepareCrossDay()
    {
        if (!TryGetGameSchedule(out GameSchedule gameSchedule))
        {
            _lastAction = "PrepareCrossDay 失败: DB.GameSchedule 为空";
            RefreshView();
            return;
        }

        _ = GiftManager.Instance.ChestGift.DailyRefreshCount;
        if (!TryGetGiftData(out GiftData giftData))
        {
            _lastAction = "PrepareCrossDay 失败: DB.GiftData 为空";
            RefreshView();
            return;
        }

        long yesterdayLocalStart = GetYesterdayLocalStartTimestamp();
        gameSchedule.SetScheduleLastRefreshTime(DailyModuleKey, yesterdayLocalStart);
        gameSchedule.DailyFreeCoinLastClaimTime = yesterdayLocalStart;
        giftData.ChestGiftOfferLocalDay = yesterdayLocalStart;
        bool scheduleSaveResult = DB.SaveGameSchedule();
        bool giftSaveResult = DB.SaveGiftData();

        _isPrepared = true;
        _lastAction =
            $"PrepareCrossDay -> {FormatLocalTimestamp(yesterdayLocalStart)}, " +
            $"Schedule保存={FormatBoolText(scheduleSaveResult)}, Gift保存={FormatBoolText(giftSaveResult)}";
        this.Log($"[GMDailyScheduleTest] {_lastAction}");

        if (!scheduleSaveResult)
            this.LogWarning("[GMDailyScheduleTest] SaveGameSchedule failed during PrepareCrossDay. Continue with in-memory schedule check.");
        if (!giftSaveResult)
            this.LogWarning("[GMDailyScheduleTest] SaveGiftData failed during PrepareCrossDay. Continue with in-memory ChestGift test.");

        if (!ExecuteScheduleCheck(
            "PrepareCrossDay + Check",
            $"Set Daily, ChestGift assigned day and Shop free coin claim day to yesterday local start: {FormatLocalTimestamp(yesterdayLocalStart)}; " +
            $"scheduleSave={FormatBoolText(scheduleSaveResult)}, giftSave={FormatBoolText(giftSaveResult)}; " +
            "then call ScheduleSystem.CheckAllModules()"))
        {
            return;
        }

        RedDotManager.Refresh(RedDotKey.DailyGiftCoin);
        OpenChestGiftForCrossDayTest();
    }

    /// <summary>
    /// Directly invokes ScheduleSystem.CheckAllModules for fast iteration.
    /// </summary>
    public void QuickCheckNow()
    {
        ExecuteScheduleCheck("刷新 Daily 数据", "直接调用: ScheduleSystem.CheckAllModules()");
    }

    /// <summary>
    /// Builds a human-readable summary of current Daily schedule debug state.
    /// </summary>
    public string BuildDebugText()
    {
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        long todayLocalStart = DailyScheduleModule.GetTodayStartTimestamp();
        long dailyLastRefreshTime = 0;

        bool hasGameSchedule = TryGetGameSchedule(out GameSchedule gameSchedule);
        if (hasGameSchedule)
        {
            dailyLastRefreshTime = gameSchedule.GetScheduleLastRefreshTime(DailyModuleKey);
        }

        DailyScheduleModule dailyModule = ScheduleSystem.Instance.GetModule<DailyScheduleModule>();
        bool isModuleRegistered = dailyModule != null;
        bool isDailyChallengeRegistered = dailyModule?.GetRefreshable<DailyChallengeManager>() != null;
        bool shouldRefresh = dailyLastRefreshTime < todayLocalStart;
        bool hasGiftData = TryGetGiftData(out GiftData giftData);

        var sb = new StringBuilder(512);
        sb.AppendLine("[每日调度测试]");
        sb.AppendLine($"当前 UTC: {FormatUtcTimestamp(nowUtc.ToUnixTimeSeconds())}");
        sb.AppendLine($"今日当地起点: {ColorPassValue(FormatLocalTimestamp(todayLocalStart))}");
        sb.AppendLine($"Daily 最后刷新: {ColorBoolValue(dailyLastRefreshTime > 0, FormatUtcTimestamp(dailyLastRefreshTime), FormatUtcTimestamp(dailyLastRefreshTime))}");
        sb.AppendLine($"Daily 最后刷新 (本地): {FormatLocalTimestamp(dailyLastRefreshTime)}");
        sb.AppendLine($"ChestGift 日刷次数: {(hasGiftData ? giftData.ChestGiftDailyRefreshCount.ToString() : "N/A")}");
        sb.AppendLine($"ChestGift 当前档位: {(hasGiftData ? giftData.ChestGiftOfferIndex.ToString() : "N/A")}");
        sb.AppendLine($"ChestGift 当日分配日: {(hasGiftData ? FormatLocalTimestamp(giftData.ChestGiftOfferLocalDay) : "N/A")}");
        sb.AppendLine($"ChestGift 当日商品: {(hasGiftData ? giftData.ChestGiftOfferProductId : "N/A")}");
        sb.AppendLine($"ChestGift 最近完成商品: {(hasGiftData ? giftData.ChestGiftCompletedOfferProductId : "N/A")}");
        sb.AppendLine($"每日计数.插屏免广告弹窗: {GetDailyInterstitialNoAdPopupCount(gameSchedule, hasGameSchedule)}");
        sb.AppendLine($"每日计数.商店免费金币: {GetDailyShopFreeCoinCount(gameSchedule, hasGameSchedule)}");
        sb.AppendLine($"每日挑战.累计进度: {GetDailyChallengeProgress(gameSchedule, hasGameSchedule)}");
        sb.AppendLine($"每日挑战.已领取奖励阶段: {GetDailyChallengeRewardIds(gameSchedule, hasGameSchedule)}");
/*         sb.AppendLine($"预判: {ColorBoolValue(shouldRefresh, "应触发", "应跳过")}");
        sb.AppendLine($"跨天准备: {ColorBoolValue(_isPrepared, "是", "否")}"); */
        sb.AppendLine($"模块已注册: {ColorBoolValue(isModuleRegistered, "是", "否")}");
        sb.AppendLine($"DailyChallenge 已注册: {ColorBoolValue(isDailyChallengeRegistered, "是", "否")}");
        sb.AppendLine($"GameSchedule 就绪: {ColorBoolValue(hasGameSchedule, "是", "否")}");
        sb.AppendLine($"最后操作: {ColorActionText(_lastAction)}");
        return sb.ToString().TrimEnd();
    }

    private bool ExecuteScheduleCheck(string actionName, string executionMode)
    {
        if (!TryGetGameSchedule(out GameSchedule gameSchedule))
        {
            _lastAction = $"{actionName} 失败: DB.GameSchedule 为空";
            RefreshView();
            return false;
        }

        EnsureDailyModuleRegistered();

        long before = gameSchedule.GetScheduleLastRefreshTime(DailyModuleKey);
        ScheduleSystem.Instance.CheckAllModules();
        long after = gameSchedule.GetScheduleLastRefreshTime(DailyModuleKey);
        _isPrepared = after < DailyScheduleModule.GetTodayStartTimestamp();

        _lastAction = $"{actionName} -> {executionMode}, 之前={FormatUtcTimestamp(before)}, 之后={FormatUtcTimestamp(after)}";
        this.Log($"[GMDailyScheduleTest] {_lastAction}");
        RefreshView();
        return true;
    }

    private void RebuildButtons()
    {
        ClearGeneratedButtons();

        if (buttonTemplate == null)
        {
            return;
        }

        Transform parent = buttonContainer != null ? buttonContainer : buttonTemplate.transform.parent;
        if (parent == null)
        {
            return;
        }

        buttonTemplate.gameObject.SetActive(false);

        CreateButton(parent, ButtonAction.PrepareCrossDay, "跨天测试");
        CreateButton(parent, ButtonAction.QuickCheckNow, "刷新 daily 数据");
    }

    private void ClearGeneratedButtons()
    {
        foreach (CustomButton button in _generatedButtons)
        {
            if (button == null)
            {
                continue;
            }

            button.OnClick -= OnPrepareCrossDayClick;
            button.OnClick -= OnQuickCheckNowClick;
            Destroy(button.gameObject);
        }

        _generatedButtons.Clear();
        _buttonMap.Clear();
    }

    private void CreateButton(Transform parent, ButtonAction action, string label)
    {
        CustomButton instance = Instantiate(buttonTemplate, parent, false);
        instance.gameObject.name = label;
        instance.gameObject.SetActive(true);
        SetButtonLabel(instance, label);
        BindGeneratedButton(instance, action);

        _generatedButtons.Add(instance);
        _buttonMap[action] = instance;
    }

    private void BindGeneratedButton(CustomButton button, ButtonAction action)
    {
        switch (action)
        {
            case ButtonAction.PrepareCrossDay:
                button.OnClick += OnPrepareCrossDayClick;
                break;
            case ButtonAction.QuickCheckNow:
                button.OnClick += OnQuickCheckNowClick;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    private static void SetButtonLabel(CustomButton button, string label)
    {
        TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
        {
            tmpText.text = label;
        }

        Text legacyText = button.GetComponentInChildren<Text>(true);
        if (legacyText != null)
        {
            legacyText.text = label;
        }
    }

    private void EnableRichText()
    {
        if (debugText != null)
        {
            debugText.richText = true;
        }
    }

    private static bool TryGetGameSchedule(out GameSchedule gameSchedule)
    {
        gameSchedule = DB.GameSchedule;
        return gameSchedule != null;
    }

    private static bool TryGetGiftData(out GiftData giftData)
    {
        giftData = DB.GiftData;
        return giftData != null;
    }

    private static string GetDailyInterstitialNoAdPopupCount(GameSchedule gameSchedule, bool hasGameSchedule)
    {
        return hasGameSchedule ? gameSchedule.Daily_interstitialNoAdPopupShowCount.ToString() : "N/A";
    }

    private static string GetDailyShopFreeCoinCount(GameSchedule gameSchedule, bool hasGameSchedule)
    {
        return hasGameSchedule ? gameSchedule.Daily_shopFreeCoinShowCount.ToString() : "N/A";
    }

    private static string GetDailyChallengeProgress(GameSchedule gameSchedule, bool hasGameSchedule)
    {
        return hasGameSchedule ? gameSchedule.DailyChallengeCurrentProcess.ToString() : "N/A";
    }

    private static string GetDailyChallengeRewardIds(GameSchedule gameSchedule, bool hasGameSchedule)
    {
        if (!hasGameSchedule)
        {
            return "N/A";
        }

        List<int> rewardIds = gameSchedule.DailyChallengeHasRewardId;
        return rewardIds.Count > 0 ? string.Join(", ", rewardIds) : "无";
    }

    private static void EnsureDailyModuleRegistered()
    {
        if (ScheduleSystem.Instance.GetModule<DailyScheduleModule>() != null)
        {
            return;
        }

        ScheduleSystem.Instance.RegisterModule(new DailyScheduleModule());
    }

    private static long GetYesterdayLocalStartTimestamp()
    {
        long todayLocalStart = DailyScheduleModule.GetTodayStartTimestamp();
        DateTimeOffset previousLocalDay = DateTimeOffset.FromUnixTimeSeconds(todayLocalStart - 1);
        return DailyScheduleModule.GetDayStartTimestamp(previousLocalDay, TimeZoneInfo.Local);
    }

    private void OpenChestGiftForCrossDayTest()
    {
        GiftManager giftManager = GiftManager.Instance;
        if (!giftManager.ChestGift.TryResolveCurrentIndex(out int giftIndex))
        {
            _lastAction = "跨天测试失败: 没有可购买的 ChestGift 档位";
            this.LogWarning($"[GMDailyScheduleTest] {_lastAction}");
            RefreshView();
            return;
        }

        GiftPopup popup = giftManager.Open(GiftPopupType.ChestGift);
        if (popup == null)
        {
            _lastAction = $"跨天测试失败: ChestGiftPopup 打开失败, GiftIndex={giftIndex}";
            this.LogWarning($"[GMDailyScheduleTest] {_lastAction}");
            RefreshView();
            return;
        }

        _lastAction = $"跨天测试完成: ChestGiftPopup 已打开, GiftIndex={giftIndex}";
        this.Log($"[GMDailyScheduleTest] {_lastAction}");
    }

    private static string FormatUtcTimestamp(long unixSeconds)
    {
        if (unixSeconds <= 0)
        {
            return "Invalid";
        }

        try
        {
            DateTimeOffset time = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToUniversalTime();
            return $"{time:yyyy-MM-dd HH:mm:ss} UTC";
        }
        catch (ArgumentOutOfRangeException)
        {
            return "OutOfRange";
        }
    }

    private static string FormatLocalTimestamp(long unixSeconds)
    {
        if (unixSeconds <= 0)
        {
            return "Invalid";
        }

        try
        {
            DateTimeOffset time = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
            return $"{time:yyyy-MM-dd HH:mm:ss} Local";
        }
        catch (ArgumentOutOfRangeException)
        {
            return "OutOfRange";
        }
    }

    private static string FormatBoolText(bool value)
    {
        return value ? "Success" : "Failed";
    }

    private static string ColorBoolValue(bool isPass, string passText, string failText)
    {
        return isPass ? ColorPassValue(passText) : ColorFailValue(failText);
    }

    private static string ColorPassValue(string value)
    {
        return $"<color={PassColor}>{value}</color>";
    }

    private static string ColorFailValue(string value)
    {
        return $"<color={FailColor}>{value}</color>";
    }

    private static string ColorActionText(string actionText)
    {
        if (string.IsNullOrWhiteSpace(actionText))
        {
            return ColorFailValue("None");
        }

        bool isFailure =
            actionText.IndexOf("失败", StringComparison.OrdinalIgnoreCase) >= 0 ||
            actionText.IndexOf("为空", StringComparison.OrdinalIgnoreCase) >= 0 ||
            actionText.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
            actionText.IndexOf("null", StringComparison.OrdinalIgnoreCase) >= 0;

        return isFailure ? ColorFailValue(actionText) : ColorPassValue(actionText);
    }

    private void OnPrepareCrossDayClick(CustomButton _)
    {
        PrepareCrossDay();
    }

    private void OnQuickCheckNowClick(CustomButton _)
    {
        QuickCheckNow();
    }
}

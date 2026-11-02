using Bear.Logger;
using Bear.SaveModule;
using Config.Game;
using Game.ConfigModule;

/// <summary>
/// 插屏免广告弹窗自动展示模块。
/// 每日限制展示次数，超出后不再自动弹出。
/// 通过 ScheduleSystem 的 DailyScheduleModule 每日重置计数。
/// 实例由 DailyScheduleModule 统一持有和注册。
/// </summary>
public class InterstitialNOAdPopupAutoShowModule : IScheduleRefreshable, IDebuger
{
    public string RefreshKey => "InterstitialNOAdPopupAutoShow";

    #region Daily Refresh

    /// <summary>
    /// 每日刷新回调：重置当日展示计数。
    /// </summary>
    public void OnScheduleRefresh()
    {
        DB.GameSchedule.Daily_interstitialNoAdPopupShowCount = 0;
        DB.SaveGameSchedule();

        DrawGame drawSaveData = DB.DrawGame;
        if (drawSaveData != null)
        {
            drawSaveData.DailyInterstitialNoAdPopupShowCount = 0;
            DB.SaveDrawGame();
        }

        IWannerGame iwannerSaveData = DB.IWannerGame;
        if (iwannerSaveData != null)
        {
            iwannerSaveData.DailyInterstitialNoAdPopupShowCount = 0;
            DB.SaveIWannerGame();
        }

        DigGame digSaveData = DB.DigGame;
        if (digSaveData != null)
        {
            digSaveData.DailyInterstitialNoAdPopupShowCount = 0;
            DB.SaveDigGame();
        }

        this.Log($"[{RefreshKey}] Main, Draw, IWanner and Dig daily counts reset.");
    }

    #endregion

    #region Main

    /// <summary>
    /// 判断是否允许展示免广告弹窗。
    /// 规则：当日展示次数未达到远程配置上限时允许展示。
    /// </summary>
    public bool ShouldShowNoAdPopup()
    {
        int currentCount = DB.GameSchedule.Daily_interstitialNoAdPopupShowCount;
        int maxCount = ConfigManager.RemoteConfig.GetAdDailyNOAdPopupAutoShowCount();

        if (maxCount <= 0)
        {
            this.Log($"[{RefreshKey}] MaxCount={maxCount}, disabled.");
            return false;
        }

        if (GameManager.Instance.Bag.HasTool(GameProps.NoAds))
        {
            this.Log($"[{RefreshKey}] Player has NoAds, no need to show popup.");
            return false;
        }

        bool canShow = currentCount < maxCount;
        this.Log($"[{RefreshKey}] Count={currentCount}/{maxCount}, canShow={canShow}");
        return canShow;
    }

    /// <summary>
    /// 记录一次弹窗展示。应在实际弹出后调用。
    /// </summary>
    public void RecordShow()
    {
        DB.GameSchedule.Daily_interstitialNoAdPopupShowCount++;
        DB.SaveGameSchedule();
        this.Log($"[{RefreshKey}] Show recorded. Count={DB.GameSchedule.Daily_interstitialNoAdPopupShowCount}");
    }

    #endregion

    #region DrawGame

    /// <summary>
    /// 判断 DrawGame 当前是否允许自动展示免广告弹窗。
    /// 规则与主线一致，但次数上限读取 drawGame_dailyNOAdPopupAutoShowCount，计数读取 DB.DrawGame。
    /// </summary>
    public bool ShouldShowDrawNoAdPopup()
    {
        DrawGame saveData = DB.DrawGame;
        if (saveData == null)
        {
            this.LogWarning($"[{RefreshKey}] DrawGame save data is unavailable.");
            return false;
        }

        int currentCount = saveData.DailyInterstitialNoAdPopupShowCount;
        int maxCount = ConfigManager.RemoteConfig.GetDrawGameDailyNOAdPopupAutoShowCount();
        if (maxCount <= 0)
        {
            this.Log($"[{RefreshKey}] Draw MaxCount={maxCount}, disabled.");
            return false;
        }

        if (GameManager.Instance.Bag.HasTool(GameProps.NoAds))
        {
            this.Log($"[{RefreshKey}] Player has NoAds, no need to show Draw popup.");
            return false;
        }

        bool canShow = currentCount < maxCount;
        this.Log($"[{RefreshKey}] Draw Count={currentCount}/{maxCount}, canShow={canShow}");
        return canShow;
    }

    /// <summary>
    /// 记录 DrawGame 实际自动展示的一次免广告弹窗。
    /// 调用边界：只允许在 Draw 的 NoAdsPopup 已创建后调用，不得增加主线每日计数。
    /// </summary>
    public void RecordDrawShow()
    {
        DrawGame saveData = DB.DrawGame;
        if (saveData == null)
        {
            this.LogWarning($"[{RefreshKey}] DrawGame save data is unavailable.");
            return;
        }

        saveData.DailyInterstitialNoAdPopupShowCount++;
        DB.SaveDrawGame();
        this.Log(
            $"[{RefreshKey}] Draw show recorded. Count={saveData.DailyInterstitialNoAdPopupShowCount}");
    }

    #endregion

    #region DigGame

    /// <summary>
    /// 判断 DigGame 当前是否允许自动展示免广告弹窗。
    /// 规则与主线一致，但次数上限读取 digGame_dailyNOAdPopupAutoShowCount，计数读取 DB.DigGame。
    /// </summary>
    public bool ShouldShowDigNoAdPopup()
    {
        DigGame saveData = DB.DigGame;
        if (saveData == null)
        {
            this.LogWarning($"[{RefreshKey}] DigGame save data is unavailable.");
            return false;
        }

        int currentCount = saveData.DailyInterstitialNoAdPopupShowCount;
        int maxCount = ConfigManager.RemoteConfig.GetDigGameDailyNOAdPopupAutoShowCount();
        if (maxCount <= 0)
        {
            this.Log($"[{RefreshKey}] Dig MaxCount={maxCount}, disabled.");
            return false;
        }

        if (GameManager.Instance.Bag.HasTool(GameProps.NoAds))
        {
            this.Log($"[{RefreshKey}] Player has NoAds, no need to show Dig popup.");
            return false;
        }

        bool canShow = currentCount < maxCount;
        this.Log($"[{RefreshKey}] Dig Count={currentCount}/{maxCount}, canShow={canShow}");
        return canShow;
    }

    /// <summary>
    /// 记录 DigGame 实际自动展示的一次免广告弹窗。
    /// 调用边界：只允许在 Dig 的 NoAdsPopup 已创建后调用，不得增加主线每日计数。
    /// </summary>
    public void RecordDigShow()
    {
        DigGame saveData = DB.DigGame;
        if (saveData == null)
        {
            this.LogWarning($"[{RefreshKey}] DigGame save data is unavailable.");
            return;
        }

        saveData.DailyInterstitialNoAdPopupShowCount++;
        DB.SaveDigGame();
        this.Log(
            $"[{RefreshKey}] Dig show recorded. Count={saveData.DailyInterstitialNoAdPopupShowCount}");
    }

    #endregion

    #region IWannerGame

    /// <summary>
    /// 用途：判断 IWannerGame 当前是否允许自动展示免广告弹窗。
    /// 调用边界：仅供 IWanner 在成功插屏后的下一次可操作 Gameplay 状态读取。
    /// 注意事项：次数上限和存档计数均为 IWanner 独立状态，不得消费主线或 Draw 计数。
    /// </summary>
    public bool ShouldShowIWannerNoAdPopup()
    {
        IWannerGame saveData = DB.IWannerGame;
        if (saveData == null)
        {
            this.LogWarning($"[{RefreshKey}] IWannerGame save data is unavailable.");
            return false;
        }

        int currentCount = saveData.DailyInterstitialNoAdPopupShowCount;
        int maxCount = ConfigManager.RemoteConfig.GetIWannerGameDailyNOAdPopupAutoShowCount();
        if (maxCount <= 0)
        {
            this.Log($"[{RefreshKey}] IWanner MaxCount={maxCount}, disabled.");
            return false;
        }

        GameManager gameManager = GameManager.Instance;
        if (gameManager?.Bag == null)
        {
            this.LogWarning($"[{RefreshKey}] IWanner Bag is unavailable.");
            return false;
        }

        if (gameManager.Bag.HasTool(GameProps.NoAds))
        {
            this.Log($"[{RefreshKey}] Player has NoAds, no need to show IWanner popup.");
            return false;
        }

        bool canShow = currentCount < maxCount;
        this.Log($"[{RefreshKey}] IWanner Count={currentCount}/{maxCount}, canShow={canShow}");
        return canShow;
    }

    /// <summary>
    /// 用途：记录 IWannerGame 实际自动展示的一次免广告弹窗。
    /// 调用边界：只允许在 IWanner 的 NoAdsPopup 已创建后调用。
    /// 注意事项：保存失败由 DB 层返回值表示；本方法仅记录日志，不回滚内存计数。
    /// </summary>
    public void RecordIWannerShow()
    {
        IWannerGame saveData = DB.IWannerGame;
        if (saveData == null)
        {
            this.LogWarning($"[{RefreshKey}] IWannerGame save data is unavailable.");
            return;
        }

        saveData.DailyInterstitialNoAdPopupShowCount++;
        if (!DB.SaveIWannerGame())
            this.LogWarning($"[{RefreshKey}] Failed to save IWanner no-ad popup count.");

        this.Log(
            $"[{RefreshKey}] IWanner show recorded. Count={saveData.DailyInterstitialNoAdPopupShowCount}");
    }

    #endregion
}

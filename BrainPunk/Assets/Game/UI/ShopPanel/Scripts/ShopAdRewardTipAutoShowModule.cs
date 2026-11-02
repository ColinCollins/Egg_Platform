using Bear.Logger;
using Config.Game;
using Game.ConfigModule;
using Game.Play;

/// <summary>
/// 商店免费金币奖励提示弹窗自动展示模块。
/// 每日限制展示次数，超出后不再自动弹出。
/// 通过 ScheduleSystem 的 DailyScheduleModule 每日重置计数。
/// 实例由 DailyScheduleModule 统一持有和注册。
/// </summary>
public class ShopAdRewardTipAutoShowModule : IScheduleRefreshable, IDebuger
{
    public string RefreshKey => "ShopAdRewardTipAutoShow";

    /// <summary>
    /// 每日刷新回调：重置当日展示计数。
    /// </summary>
    public void OnScheduleRefresh()
    {
        DB.GameSchedule.Daily_shopFreeCoinShowCount = 0;
        DB.SaveGameSchedule();
        this.Log($"[{RefreshKey}] Daily count reset.");
    }

    /// <summary>
    /// 判断是否允许展示商店免费金币奖励提示弹窗。
    /// 规则：
    /// 1. 已拥有 UnlimitCoin
    /// 2. 当日展示次数未达到远程配置上限时允许展示
    /// </summary>
    public bool ShouldShowRewardTip()
    {
        var bag = GameManager.Instance?.Bag;
        if (bag != null)
        {
            if (bag.HasTool(GameProps.UnlimitCoin))
            {
                this.Log($"[{RefreshKey}] Has UnlimitCoin, skip.");
                return false;
            }
        }

        int currentCount = DB.GameSchedule.Daily_shopFreeCoinShowCount;
        int maxCount = ConfigManager.RemoteConfig.GetShopFreeCoinShowCount();

        if (maxCount <= 0)
        {
            this.Log($"[{RefreshKey}] MaxCount={maxCount}, disabled.");
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
        DB.GameSchedule.Daily_shopFreeCoinShowCount++;
        DB.SaveGameSchedule();
        this.Log($"[{RefreshKey}] Show recorded. Count={DB.GameSchedule.Daily_shopFreeCoinShowCount}");
    }
}

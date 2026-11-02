using System.Collections.Generic;

public partial class GameSDKService
{
    /// <summary>
    /// IAP_IMP 商店展示
    /// </summary>
    /// <param name="category">界面跳转来源</param>
    public void IAP_IMP(string category = "GamePlayPanel")
    {
        // TODO: 这里只放打点范例, 并非最终打点参数, 项目组需要自己补全
        // 打点上报
        var data = new Dictionary<string, object>()
        {
            ["item_category"] = category,
        };

        LogEvent("iap_imp", data);
    }

    /// <summary>
    /// IAP_CLK 商店展示
    /// </summary>
    /// <param name="productName">界面跳转来源</param>
    public void IAP_CLK(string productName)
    {
        // TODO: 这里只放打点范例, 并非最终打点参数, 项目组需要自己补全
        // 打点上报
        var data = new Dictionary<string, object>()
        {
            ["item_id"] = productName,
            ["product_id"] = productName,
            ["item_category"] = "store",
        };

        LogEvent("iap_clk", data);
    }

    /// <summary>
    /// IAP_CLOSE 商店展示
    /// </summary>
    /// <param name="category">界面跳转来源</param>
    public void IAP_CLOSE(string category = "GamePlayPanel")
    {
        // TODO: 这里只放打点范例, 并非最终打点参数, 项目组需要自己补全
        // 打点上报
        var data = new Dictionary<string, object>()
        {
            ["item_category"] = category,
        };

        LogEvent("iap_close", data);
    }
}
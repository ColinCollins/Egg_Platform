using Config.Game;

public partial class GameSDKService
{
    public void UpdateIAPCoin()
    {
        /* DB.GameData.PurchaseGetTools.TryGetValue(GameProps.Tips.ToString(), out int count);
        SetUserProperty("iap_coin", count.ToString()); */
    }

    public void UpdateNonIAPCoin()
    {
        /*         DB.GameData.AdGetTools.TryGetValue(GameProps.Tips.ToString(), out int count);
                SetUserProperty("noniap_coin", count.ToString()); */
    }

    public void UpdateCoin()
    {
        /*         DB.GameData.GameTools.TryGetValue(GameProps.Tips.ToString(), out int count);
                SetUserProperty("coin", count.ToString()); */
    }

    // 当前通关总数    
    public int GetLevelPassedCount()
    {
        int count = 0;
        foreach (var pair in DB.GameData.LevelSuccessCount)
        {
            count += pair.Value;
        }
        return count;
    }

    /// <summary>
    /// 获取主线、DrawGame、IWannerGame 与 DigGame 的历史累计通关次数。
    /// 调用边界：仅用于跨玩法共用的关卡序号和 b_play 用户属性，不改变任一玩法的存档。
    /// </summary>
    private int GetCombinedLevelPassedCount()
    {
        int count = GetLevelPassedCount();
        var drawGame = DB.DrawGame;
        if (drawGame?.LevelSuccessCount != null)
        {
            foreach (var pair in drawGame.LevelSuccessCount)
            {
                count += pair.Value;
            }
        }

        var iwannerGame = DB.IWannerGame;
        if (iwannerGame?.LevelSuccessCount != null)
        {
            foreach (var pair in iwannerGame.LevelSuccessCount)
            {
                count += pair.Value;
            }
        }

        var digGame = DB.DigGame;
        if (digGame?.LevelSuccessCount != null)
        {
            foreach (var pair in digGame.LevelSuccessCount)
                count += pair.Value;
        }

        return count;
    }

    /// <summary>
    /// 获取最大通过关卡
    /// </summary>
    /// <returns></returns>
    public int GetMaxPassedLevel()
    {
        var passed = DB.GameData.PassedLevels;
        if (passed == null || passed.Count == 0)
            return 0;
        int max = passed[0];
        for (int i = 1; i < passed.Count; i++)
        {
            if (passed[i] > max)
                max = passed[i];
        }
        return max;
    }

    public void UpdateBPlay()
    {
        int count = GetCombinedLevelPassedCount();
        SetUserProperty("b_play", count.ToString());
    }

    public void UpdateBLevel()
    {
        SetUserProperty("b_level",GetMaxPassedLevel().ToString());
    }
}

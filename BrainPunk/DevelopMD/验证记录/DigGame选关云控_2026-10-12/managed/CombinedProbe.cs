public partial class GameSDKService {
    public int GetLevelPassedCount()
    {
        int count = 0;
        foreach (var pair in DB.GameData.LevelSuccessCount)
        {
            count += pair.Value;
        }
        return count;
    }
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
}
using UnityEngine;

/// <summary>
/// 用于处理一些 Level 相关的数据
/// </summary>
public class LevelCtrl
{
    private bool isReady = false;

    private int _currentLevel;
    public int CurrentLevel
    {
        get
        {
            RefreshLevel();
            return _currentLevel;
        }
    }

    public void Init()
    {
        if (isReady)
            return;
    
        // 数据加载配置
        isReady = true;

        if (DB.GameData.CurrentLevel > 0)
        {
            _currentLevel = DB.GameData.CurrentLevel;
        }
    }

    public void Victory()
    {
        if (!DB.GameData.PassedLevels.Contains(CurrentLevel))
        {
            DB.GameData.PassedLevels.Add(CurrentLevel);
            DB.GameData.Save();
        }
    }

    // 根据 pass 和 unlock 确定当前的 最小 level
    public void RefreshLevel()
    {
        // check current config and DB 数据对比
        _currentLevel = 1;
    }
}

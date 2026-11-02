using System.Collections.Generic;
using Bear.Logger;
using Config;
using TMPro;
using UnityEngine;

/// <summary>
/// DrawGame 主界面入口进度展示。
/// </summary>
[DisallowMultipleComponent]
public sealed class DrawGameEnter : MonoBehaviour, IDebuger
{
    #region Constants

    private const int MaxStarCountPerLevel = 3;

    #endregion

    #region References

    /// <summary>
    /// 用途：展示当前有效 Draw 关卡已永久获得的星星总数。
    /// 调用边界：由 DrawGameEnter prefab 绑定到 ui_zhuye_03_txt。
    /// 注意事项：只读取 DB.DrawGame，不修改或保存星星数据。
    /// </summary>
    [SerializeField] private TextMeshProUGUI collectedStarCountText;

    /// <summary>
    /// 用途：以“/{总数}”格式展示当前有效 Draw 关卡的星星总数。
    /// 调用边界：由 DrawGameEnter prefab 绑定到 ui_zhuye_04_txt。
    /// 注意事项：无星级配置的等待关卡不计入总数。
    /// </summary>
    [SerializeField] private TextMeshProUGUI totalStarCountText;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        Refresh();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 用途：按当前 Draw 有效排序和永久最高星存档刷新入口进度。
    /// 调用边界：入口启用时自动调用，也可在 StartPanel 复用打开后主动调用。
    /// 注意事项：配置控制器未就绪时显示 0/0，不触发配置初始化或存档写入。
    /// </summary>
    public void Refresh()
    {
        if (!Application.isPlaying)
            return;

        DrawGameLevelCtrl levelCtrl = GameManager.Instance?.DrawGameLevel;
        if (levelCtrl == null || !levelCtrl.IsReady)
        {
            SetProgress(0, 0);
            this.LogWarning("[DrawGameEnter] DrawGameLevelCtrl is not ready.");
            return;
        }

        IReadOnlyList<DrawLevelSort> levelSorts = levelCtrl.LevelSorts;
        Dictionary<string, int> levelStars = DB.DrawGame?.LevelCollectedStar;
        var countedLevelIds = new HashSet<string>();
        int collectedStarCount = 0;
        int totalStarCount = 0;

        for (int i = 0; i < levelSorts.Count; i++)
        {
            DrawLevelSort sortData = levelSorts[i];
            DrawLevelData levelData = sortData == null
                ? null
                : levelCtrl.GetLevelDataById(sortData.Path);
            if (!HasStarProgress(levelData) || !countedLevelIds.Add(levelData.Id))
                continue;

            totalStarCount += MaxStarCountPerLevel;
            if (levelStars != null &&
                levelStars.TryGetValue(levelData.Id, out int levelStarCount))
            {
                collectedStarCount += Mathf.Clamp(levelStarCount, 0, MaxStarCountPerLevel);
            }
        }

        SetProgress(collectedStarCount, totalStarCount);
    }

    #endregion

    #region Progress Helpers

    private static bool HasStarProgress(DrawLevelData levelData)
    {
        return levelData != null &&
               !string.IsNullOrEmpty(levelData.Id) &&
               (levelData.InkStarStage2 > 0f || levelData.InkStarStage3 > 0f);
    }

    private void SetProgress(int collectedStarCount, int totalStarCount)
    {
        if (collectedStarCountText != null)
            collectedStarCountText.text = collectedStarCount.ToString();

        if (totalStarCountText != null)
            totalStarCountText.text = $"/{totalStarCount}";
    }

    #endregion
}

using System.Collections.Generic;
using Config;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draw 选关项星星组控制器，只读取 DB.DrawGame 中对应关卡的星星数量并刷新显示。
/// </summary>
public sealed class DrawGame_ChoiceLevelStarGroupCtrl : MonoBehaviour
{
    #region Inspector

    /// <summary>
    /// 用途：按顺序展示当前 Draw 关卡已获得星星状态的 Toggle 列表。
    /// 调用边界：由 Draw 选关项 prefab 绑定，运行时刷新最多读取前三个有效位置。
    /// 注意事项：列表为空或未绑定时会安全跳过刷新，不应阻断 Draw 选关界面打开。
    /// </summary>
    [SerializeField] private List<Toggle> starToggles;

    /// <summary>
    /// 用途：当前 Draw 关卡状态下，星星组相对普通/已通关状态的显示偏移。
    /// 调用边界：仅由 Refresh 根据 isNormal 参数在运行时应用。
    /// 注意事项：偏移始终基于初始锚点位置计算，避免重复刷新时累计位移。
    /// </summary>
    [SerializeField] private Vector2 stateOffset;

    #endregion

    #region Runtime State

    private RectTransform starGroupRt;
    private Vector2 initialAnchoredPosition;
    private bool hasInitialAnchoredPosition;

    #endregion

    #region Public API

    /// <summary>
    /// 用途：根据 DB.DrawGame 刷新 Draw 选关项中的星星显示，并按当前关卡状态应用位置偏移。
    /// 调用边界：由 DrawGame_LevelChoiceItem 在设置关卡数据和状态后调用。
    /// 注意事项：该方法只读永久最高星数，不写存档，也不读取或消费 VictoryPanel 等待动画数据。
    /// </summary>
    public void Refresh(DrawLevelData levelData, bool isShow, bool isNormal = true)
    {
        ResolveRt();
        ApplyStatePosition(isNormal);
        ResetAllToggles();

        gameObject.SetActive(isShow);

        if (!isShow || levelData == null || string.IsNullOrEmpty(levelData.Id))
            return;

        int toggleCount = GetToggleCount();
        if (toggleCount <= 0)
            return;

        for (int i = 0; i < toggleCount; i++)
        {
            Toggle toggle = starToggles[i];
            if (toggle != null)
                toggle.gameObject.SetActive(true);
        }

        Dictionary<string, int> levelStars = DB.DrawGame?.LevelCollectedStar;
        if (levelStars == null || !levelStars.TryGetValue(levelData.Id, out int collectedStarCount))
            return;

        int visibleStarCount = Mathf.Clamp(collectedStarCount, 0, toggleCount);
        for (int i = 0; i < visibleStarCount; i++)
        {
            Toggle toggle = starToggles[i];
            if (toggle != null)
                SetToggleOn(toggle, true);
        }
    }

    #endregion

    #region RectTransform Helpers

    private void ResolveRt()
    {
        if (starGroupRt == null)
            starGroupRt = transform as RectTransform;

        if (starGroupRt != null && !hasInitialAnchoredPosition)
        {
            initialAnchoredPosition = starGroupRt.anchoredPosition;
            hasInitialAnchoredPosition = true;
        }
    }

    private void ApplyStatePosition(bool isNormal)
    {
        if (starGroupRt == null || !hasInitialAnchoredPosition)
            return;

        starGroupRt.anchoredPosition = isNormal
            ? initialAnchoredPosition
            : initialAnchoredPosition + stateOffset;
    }

    #endregion

    #region Toggle Helpers

    private int GetToggleCount()
    {
        return starToggles == null ? 0 : Mathf.Min(3, starToggles.Count);
    }

    private void ResetAllToggles()
    {
        int toggleCount = GetToggleCount();
        for (int i = 0; i < toggleCount; i++)
        {
            Toggle toggle = starToggles[i];
            if (toggle != null)
                SetToggleOn(toggle, false);
        }
    }

    private static void SetToggleOn(Toggle toggle, bool isOn)
    {
        toggle.SetIsOnWithoutNotify(isOn);
    }

    #endregion
}

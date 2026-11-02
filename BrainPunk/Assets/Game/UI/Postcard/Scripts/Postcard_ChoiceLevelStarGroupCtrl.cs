using System.Collections.Generic;
using Config;
using UnityEngine;
using UnityEngine.UI;

public class Postcard_ChoiceLevelStarGroupCtrl : MonoBehaviour
{
    #region Inspector

    /// <summary>
    /// 用途：关卡选择界面中按顺序展示当前关卡已收集明信片星星状态的 Toggle 列表。
    /// 调用边界：由关卡选择项 prefab 绑定，运行时刷新只读取前三个有效位置。
    /// 注意事项：列表为空或未绑定时会安全跳过刷新，不应阻断关卡选择界面打开。
    /// </summary>
    [SerializeField] private List<Toggle> starToggles;

    /// <summary>
    /// 用途：当前最高可进入关卡状态下，星星组相对普通/已通关状态的显示偏移。
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
    /// 用途：刷新关卡选择项中的明信片星星显示，并按当前关卡状态应用位置偏移。
    /// 调用边界：由 LevelChoiceItem.RefreshState 在设置关卡数据和状态后调用。
    /// 注意事项：该方法只读取已收集星星数量，不写入存档、不消费等待动画数据。
    /// </summary>
    public void Refresh(LevelData levelData, bool isShow, bool isNormal = true)
    {
        ResolveRt();
        ApplyStatePosition(isNormal);
        ResetAllToggles();

        gameObject.SetActive(isShow);

        if (!isShow || levelData == null || string.IsNullOrEmpty(levelData.Id))
        {
            return;
        }

        int toggleCount = GetToggleCount();
        if (toggleCount <= 0)
            return;

        PostcardManager postcardManager = GameManager.Instance?.Postcard;
        if (postcardManager == null)
            return;

        // 配置限制展示
        for (int i = 0; i < toggleCount; i++)
        {
            Toggle toggle = starToggles[i];
            toggle.gameObject.SetActive(levelData.LevelSumStarCount > i);
        }

        List<int> star = postcardManager.GetLevelCollectedStarIds(levelData.Id);
        int count = Mathf.Min(star?.Count ?? 0, toggleCount);
        for (int i = 0; i < count; i++)
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

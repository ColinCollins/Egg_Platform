using Bear.Logger;
using Game.ConfigModule;
using TMPro;
using UnityEngine;

/// <summary>
/// IWanner 主界面入口。
/// 当前临时行为是点击后打开 Group 选择弹窗，由选项进入对应 Group 的第 0 小关。
/// </summary>
[DisallowMultipleComponent]
public sealed class IWannerGameEnter : MonoBehaviour, IDebuger
{
    #region References

    /// <summary>
    /// 用途：触发进入 IWanner 的按钮；为空时运行态从当前入口节点子级自动查找。
    /// 调用边界：由入口 prefab 绑定，不应引用其它玩法按钮。
    /// 注意事项：自动查找只执行本节点范围，不扫描整个场景。
    /// </summary>
    [SerializeField] private CustomButton enterButton;

    /// <summary>
    /// 用途：展示当前配置中已经完整通过的 IWanner Group 数量。
    /// 调用边界：由 WannerGameEnter prefab 绑定到 ui_zhuye_05_txt。
    /// 注意事项：只读取 PassedLevels，不修改或保存 Group 进度。
    /// </summary>
    [SerializeField] private TextMeshProUGUI passedGroupCountText;

    /// <summary>
    /// 用途：以“/{总数}”格式展示当前有效 IWanner Group 总数。
    /// 调用边界：由 WannerGameEnter prefab 绑定到 ui_zhuye_06_txt。
    /// 注意事项：配置控制器未就绪时显示 /0。
    /// </summary>
    [SerializeField] private TextMeshProUGUI totalGroupCountText;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        RefreshEntryState();
    }

    private void OnDisable()
    {
        UnbindButton();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 用途：重新读取 IWanner 入口开关，并刷新入口根节点显隐与按钮绑定。
    /// 调用边界：仅在运行态使用；适用于入口 prefab 动态重建或 Loading 后主动刷新。
    /// 注意事项：当前项目不在游戏内实时更新 RemoteConfig，本接口不会主动拉取远端配置。
    /// </summary>
    public void Refresh()
    {
        if (!Application.isPlaying)
            return;

        RefreshEntryState();
    }

    #endregion

    #region Entry

    private void ResolveButton()
    {
        if (enterButton == null)
            enterButton = GetComponentInChildren<CustomButton>(true);
    }

    private void RefreshEntryState()
    {
        UnbindButton();

        bool isOpen = ConfigManager.RemoteConfig.GetIWannerGameIsOpen();
        if (gameObject.activeSelf != isOpen)
        {
            gameObject.SetActive(isOpen);
            return;
        }

        if (!isOpen)
            return;

        RefreshGroupProgress();
        ResolveButton();
        if (enterButton == null)
        {
            this.LogWarning("[IWannerGameEnter] Enter button is missing.");
            return;
        }

        enterButton.OnClick += OnEnterClicked;
    }

    private void UnbindButton()
    {
        if (enterButton != null)
            enterButton.OnClick -= OnEnterClicked;
    }

    private void RefreshGroupProgress()
    {
        IWannerGameLevelCtrl levelCtrl = GameManager.Instance?.IWannerGameLevel;
        if (levelCtrl == null || !levelCtrl.IsReady)
        {
            SetGroupProgress(0, 0);
            this.LogWarning("[IWannerGameEnter] IWannerGameLevelCtrl is not ready.");
            return;
        }

        var levelGroups = levelCtrl.LevelGroups;
        var passedLevels = DB.IWannerGame?.PassedLevels;
        var countedGroupIds = new System.Collections.Generic.HashSet<int>();
        int passedGroupCount = 0;

        for (int i = 0; i < levelGroups.Count; i++)
        {
            var group = levelGroups[i];
            if (group == null || group.Id <= 0 || !countedGroupIds.Add(group.Id))
                continue;

            if (passedLevels?.Contains(group.Id) == true)
                passedGroupCount++;
        }

        SetGroupProgress(passedGroupCount, countedGroupIds.Count);
    }

    private void SetGroupProgress(int passedGroupCount, int totalGroupCount)
    {
        if (passedGroupCountText != null)
            passedGroupCountText.text = passedGroupCount.ToString();

        if (totalGroupCountText != null)
            totalGroupCountText.text = $"/{totalGroupCount}";
    }

    private void OnEnterClicked(CustomButton _)
    {
        if (!ConfigManager.RemoteConfig.GetIWannerGameIsOpen())
        {
            RefreshEntryState();
            return;
        }

        if (IWannerGame_ChoiceLevelPopup.Create() == null)
            this.LogError("[IWannerGameEnter] Create IWanner Group choice popup failed.");
    }

    #endregion
}

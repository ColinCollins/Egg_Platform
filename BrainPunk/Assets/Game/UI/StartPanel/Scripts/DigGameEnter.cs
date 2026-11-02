using Bear.Logger;
using Game.ConfigModule;
using TMPro;
using UnityEngine;

/// <summary>Dig 首页独立入口；引用由 Prefab 显式绑定，无运行时节点查找。</summary>
public sealed class DigGameEnter : MonoBehaviour, IDebuger
{
    #region Prefab 绑定
    /// <summary>点击进入有效当前关的按钮。</summary>
    [SerializeField] private CustomButton enterButton;
    /// <summary>已通关数/有效关卡总数，独立读取 Dig 存档。</summary>
    [SerializeField] private TMP_Text progressText;
    #endregion

    #region 生命周期
    private void OnEnable() { if (Application.isPlaying) Refresh(); }
    private void OnDisable() { if (enterButton) enterButton.OnClick -= Enter; }
    /// <summary>StartPanel 打开后刷新配置门禁和进度，不变更其它模式入口。</summary>
    public void Refresh()
    {
        if (!Application.isPlaying || !enterButton || !progressText) return;
        enterButton.OnClick -= Enter;
        enterButton.OnClick += Enter;
        gameObject.SetActive(ConfigManager.RemoteConfig.GetDigGameIsOpen());
        var levels = GameManager.Instance?.DigGameLevel;
        enterButton.Interactable = levels?.IsReady == true;
        int passed = 0, total = 0;
        if (levels?.IsReady == true)
            foreach (var level in levels.LevelSorts)
            {
                if (level.Path == DigGameLevelCtrl.TbcDataId) continue;
                total++;
                if (DB.DigGame.PassedLevels.Contains(level.Id)) passed++;
            }
        progressText.text = $"DIG\n{passed}/{total}";
    }
    private void Enter(CustomButton button)
    {
        if (!GameManager.Instance.TryEnterDig(out string error)) this.LogError($"[DigGameEnter] {error}");
    }
    #endregion
}

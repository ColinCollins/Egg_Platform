using System;
using UnityEngine;

/// <summary>145 私有 HUD 的额外敲击键，保留共享面板原移动和跳跃键。</summary>
public sealed class Level145GamePanelFeature : MonoBehaviour
{
    #region 显式引用与生命周期
    /// <summary>Inspector 绑定新增敲击键；显隐与可点击条件由关卡实例决定。</summary>
    [SerializeField] private CustomButton interactionButton;
    /// <summary>本 HUD 点击回调，仅当前 Level145 控制器订阅。</summary>
    public event Action HitRequested;

    private void Awake()
    {
        interactionButton.OnClick += OnHitClicked;
        SetAvailable(false);
    }

    /// <summary>角色持槌且靠近有效门时显示；动作、暂停、死亡和过关期间隐藏。</summary>
    public void SetAvailable(bool available)
    {
        interactionButton.Interactable = available;
        if (interactionButton.gameObject.activeSelf != available)
            interactionButton.gameObject.SetActive(available);
    }

    private void OnHitClicked(CustomButton button) => HitRequested?.Invoke();

    private void OnDestroy()
    {
        interactionButton.OnClick -= OnHitClicked;
        HitRequested = null;
    }
    #endregion
}

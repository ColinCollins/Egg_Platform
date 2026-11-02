using Bear.UI;
using UnityEngine;
using System;
using Bear.Logger;

public partial class DrawGame_GameGuidePopup : BaseUIView, IDebuger
{
    #region 绑定与状态

    /// <summary>
    /// Prefab 中显式绑定的确认按钮；不得使用运行时节点查找替代绑定。
    /// </summary>
    [SerializeField] private CustomButton closeBtn;

    private bool _isClosing;
    private Action _onClosed;

    #endregion

    #region 生命周期

    public override void OnOpen()
    {
        base.OnOpen();
        _isClosing = false;
        if (closeBtn != null)
            closeBtn.Interactable = true;
    }

    public override void OnCreate()
    {
        base.OnCreate();
        if (closeBtn != null)
        {
            closeBtn.OnClick -= Close;
            closeBtn.OnClick += Close;
        }
        else
            this.LogError("[DrawGame_GameGuidePopup] Close button is not bound.");
    }

    public override void OnClose()
    {
        base.OnClose();
        _isClosing = true;
        Action onClosed = _onClosed;
        _onClosed = null;
        onClosed?.Invoke();
    }

    public override void OnDestroyView()
    {
        if (closeBtn != null)
            closeBtn.OnClick -= Close;
        base.OnDestroyView();
    }

    private void Close(CustomButton btn)
    {
        if (_isClosing)
            return;

        _isClosing = true;
        closeBtn.Interactable = false;
        UIManager.Instance.DestroyUI(this);
    }

    #endregion

    #region 创建入口

    /// <summary>
    /// 运行态创建操作引导，成功后保存独立已展示记录；暂停与恢复由对应玩法宿主负责。
    /// 关闭回调最多执行一次；加载失败或确认按钮未绑定时返回 null，不消耗展示记录。
    /// </summary>
    public static DrawGame_GameGuidePopup Create(Action onClosed = null)
    {
        if (!Application.isPlaying)
            return null;

        var panel = UIManager.Instance.OpenUI<DrawGame_GameGuidePopup>(nameof(DrawGame_GameGuidePopup), UILayer.Popup);
        if (panel == null)
            return null;

        if (panel.closeBtn == null)
        {
            UIManager.Instance.DestroyUI(panel);
            return null;
        }

        panel._onClosed = onClosed;
        DB.GameData.HasDrawGameGuidePopup = true;
        if (!DB.GameData.Save())
            panel.LogWarning("[DrawGame_GameGuidePopup] Failed to save shown flag; it remains set for this session.");
        return panel;
    }

    #endregion
}

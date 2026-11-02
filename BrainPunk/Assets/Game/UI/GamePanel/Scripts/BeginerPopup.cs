using System;
using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Game.Events;
using Game.Play;
using UnityEngine;

public partial class BeginerPopup : BaseUIView, IDebuger, IEventSender
{
    [SerializeField] private CustomButton closeBtn;

    private bool _needResumeGame = true;
    private bool _isClosing;
    private Action _onClosed;

    public override void OnCreate()
    {
        base.OnCreate();

        if (closeBtn != null)
        {
            closeBtn.OnClick -= Close;
            closeBtn.OnClick += Close;
        }
        else
        {
            this.LogError("[BeginerPopup] OnCreate failed. close button not found.");
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        _isClosing = false;
        SetCloseInteractable(true);
    }

    public override void OnShow()
    {
        base.OnShow();
        if (_needResumeGame)
        {
            this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PAUSE);
        }
    }

    public override void OnClose()
    {
        base.OnClose();

        if (_needResumeGame)
        {
            this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
        }

        _onClosed?.Invoke();
        _onClosed = null;
    }

    private void Close(CustomButton btn)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        SetCloseInteractable(false);
        UIManager.Instance.DestroyUI(this);
    }

    private void SetCloseInteractable(bool interactable)
    {
        if (closeBtn != null)
        {
            closeBtn.Interactable = interactable;
        }
    }

    /// <summary>仅运行态创建一次主线操作说明；成功打开后保存已展示标记，清档前不再重复弹出。</summary>
    /// <param name="needResumeGame">是否沿用主线暂停与关闭后恢复；默认主线进关使用 true。</param>
    /// <param name="onClosed">弹窗关闭回调；未创建时不调用。</param>
    /// <returns>成功打开的弹窗；已展示、非运行态或资源加载失败时返回 null。</returns>
    public static BeginerPopup Create(bool needResumeGame = true, Action onClosed = null)
    {
        if (!Application.isPlaying || DB.GameData.HasBegineTutorial)
            return null;

        var panel = UIManager.Instance.OpenUI<BeginerPopup>($"{typeof(BeginerPopup).Name}", UILayer.Popup);
        if (panel == null)
            return null;

        panel._needResumeGame = needResumeGame;
        panel._onClosed = onClosed;

        DB.GameData.HasBegineTutorial = true;
        DB.GameData.Save();

        return panel;
    }
}

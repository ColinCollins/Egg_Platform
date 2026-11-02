using Bear.EventSystem;
using Game.Events;
using Game.Play;
using UnityEngine;

/// <summary>
/// Level70 专用 GamePlayPanel 子类；负责 `GamePlayPanel_025_70` 的 Jump/Catch 按钮切换，
/// 并把 catch_btn 作为本关 useBtn 交给 Level70Ctrl，不通过共享跳跃事件执行抓钩操作。
/// </summary>
public class Level70GamePlayPanel : GamePlayPanel
{
    #region Constants

    private const string JumpButtonPath = "Root/jump_btn";
    private const string CatchButtonPath = "Root/catch_btn";

    #endregion

    #region Fields

    private EventSubscriber subscriber;
    private CustomButton jumpButton;
    private CustomButton catchButton;
    private bool isPause;

    #endregion

    #region Lifecycle

    public override void OnCreate()
    {
        base.OnCreate();
        ResolveLevel70Buttons();
        SetLevel70CatchButtonVisible(false);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        isPause = false;
        BindLevel70Events();
        SetLevel70CatchButtonVisible(false);
    }

    public override void OnClose()
    {
        EventsUtils.ResetEvents(ref subscriber);
        base.OnClose();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 切换 Level70 抓钩控制态的 Jump/Catch 按钮；仅供 Level70Ctrl 在运行时调用。
    /// control 状态会停用并隐藏共享 jump_btn，同时返回 catch_btn 作为本关局部 useBtn。
    /// </summary>
    /// <param name="isCatchMode">true 时显示 catch_btn 并隐藏 jump_btn；false 时恢复 jump_btn 并隐藏 catch_btn。</param>
    /// <returns>进入 control 状态时返回可绑定的 catch_btn；退出时返回 null。</returns>
    public CustomButton SetLevel70CatchButtonVisible(bool isCatchMode)
    {
        ResolveLevel70Buttons();

        if (jumpButton != null)
        {
            if (isCatchMode)
            {
                jumpButton.Interactable = false;
                jumpButton.enabled = false;
                jumpButton.gameObject.SetActive(false);
            }
            else
            {
                jumpButton.gameObject.SetActive(true);
                jumpButton.enabled = true;
                jumpButton.Interactable = true;
            }
        }

        if (catchButton != null)
        {
            catchButton.gameObject.SetActive(isCatchMode);
            RefreshUseButtonInteractable(isCatchMode);
        }

        return isCatchMode ? catchButton : null;
    }

    #endregion

    #region Binding

    private void BindLevel70Events()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GameResumeEvent>(OnGamePanelResume);
        subscriber.Subscribe<SwitchGameStateEvent>(OnGameStateChanged);
    }

    private void ResolveLevel70Buttons()
    {
        if (jumpButton == null)
            jumpButton = ResolveButton(JumpButtonPath);

        if (catchButton == null)
            catchButton = ResolveButton(CatchButtonPath);
    }

    private CustomButton ResolveButton(string path)
    {
        Transform buttonTransform = transform.Find(path);
        return buttonTransform != null ? buttonTransform.GetComponent<CustomButton>() : null;
    }

    #endregion

    #region Event Handlers

    private void OnGamePanelResume(GameResumeEvent evt)
    {
        isPause = false;
        RefreshUseButtonInteractable(catchButton != null && catchButton.gameObject.activeSelf);
    }

    private void OnGameStateChanged(SwitchGameStateEvent evt)
    {
        if (evt == null)
            return;

        isPause = !evt.NewState.Equals(GamePlayStateName.PLAYING);
        RefreshUseButtonInteractable(catchButton != null && catchButton.gameObject.activeSelf);
    }

    #endregion

    #region State

    private void RefreshUseButtonInteractable(bool isCatchMode)
    {
        if (catchButton == null)
            return;

        bool canUse = isCatchMode && !isPause;
        catchButton.Interactable = canUse;
        catchButton.enabled = canUse;
    }

    #endregion
}

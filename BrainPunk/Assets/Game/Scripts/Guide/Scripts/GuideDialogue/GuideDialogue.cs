using System;
using System.Collections.Generic;
using System.Threading;
using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using I2.Loc;
using UnityEngine;

/// <summary>
/// GuideDialogue 的实际 Prefab Variant；枚举值对应 Resources 文件名后缀。
/// </summary>
public enum GuideDialogueVariant
{

    /// <summary>首次免费提示前的说明对话。</summary>
    GameTipsFree = 10001,

    /// <summary>答案弹窗由 X 关闭后的收尾对话。</summary>
    GameTipsClosing = 10002,
}

/// <summary>
/// 引导对话表现，只负责当前 Prefab Variant 的文案播放、点击推进和关闭通知。
/// </summary>
public partial class GuideDialogue : BaseUIView, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 当前 Prefab Variant 配置的本地化文案 key，按列表顺序播放。
    /// </summary>
    [SerializeField] private List<string> tipsKey;

    /// <summary>
    /// 全屏对话点击区，仅在弹窗打开期间用于推进文案。
    /// </summary>
    [SerializeField] private CustomButton btn;

    /// <summary>
    /// 单条文案的逐字播放时长；小于等于 0 时立即显示完整文本。
    /// </summary>
    [SerializeField] private float textAnimDuration = 1f;

    #endregion

    #region Runtime State

    private Tweener _contentTween;
    private Action _closeCallback;
    private int _step;
    private bool _isTipPlaying;

    #endregion

    #region BaseUIView Lifecycle

    public override void OnOpen()
    {
        base.OnOpen();

        _step = 0;
        _isTipPlaying = false;
        _closeCallback = null;
        KillContentTween();

        if (UiYindao201Txt != null)
            UiYindao201Txt.text = string.Empty;

        BindBtn();
    }

    public override void OnClose()
    {
        UnbindBtn();
        KillContentTween();
        _isTipPlaying = false;

        base.OnClose();
        InvokeCloseCallback();
    }

    #endregion

    #region Dialogue Playback

    private void PlayNextStep(CustomButton clickedBtn)
    {
        if (_isTipPlaying || !IsOpen)
            return;

        _step++;
        if (tipsKey == null || _step >= tipsKey.Count)
        {
            UnbindBtn();
            UIManager.Instance?.DestroyUI(this);
            return;
        }

        PlayTip();
    }

    private void PlayTip()
    {
        if (UiYindao201Txt == null || tipsKey == null || _step < 0 || _step >= tipsKey.Count)
            return;

        KillContentTween();

        string key = tipsKey[_step];
        string content = string.IsNullOrWhiteSpace(key)
            ? string.Empty
            : LocalizationManager.GetTranslation(key);
        if (string.IsNullOrEmpty(content))
            content = key ?? string.Empty;

        float duration = Mathf.Max(0f, textAnimDuration);
        if (duration <= 0f || string.IsNullOrEmpty(content))
        {
            UiYindao201Txt.text = content;
            _isTipPlaying = false;
            return;
        }

        _isTipPlaying = true;
        UiYindao201Txt.text = content;
        UiYindao201Txt.UpdateFontAsset();
        UiYindao201Txt.ForceMeshUpdate(true, true);
        UiYindao201Txt.text = string.Empty;
        _contentTween = UiYindao201Txt
            .DOText(content, duration)
            .OnComplete(() =>
            {
                _contentTween = null;
                _isTipPlaying = false;
            });
    }

    #endregion

    #region Public API

    /// <summary>
    /// 用途：按 `GuideDialogueVariant` 打开对应的 `GuideDialogue_10001` 至 `GuideDialogue_10003` Prefab Variant。
    /// 调用边界：仅在 Unity 运行态主线程调用；文案由目标 Variant 自己配置。
    /// 注意事项：每次调用都按枚举解析资源名，不复用或覆盖其它 Variant 的配置。
    /// </summary>
    /// <param name="step">需要打开的引导阶段。</param>
    /// <param name="closeCallback">对话关闭后执行一次的可选回调。</param>
    /// <returns>创建成功时返回对话实例，否则返回 null。</returns>
    public static GuideDialogue Create(
        GuideDialogueVariant variant,
        Action closeCallback = null)
    {
        if (!Application.isPlaying)
            return null;

        UIManager uiManager = UIManager.Instance;
        if (uiManager == null)
            return null;

        string prefabName = $"{nameof(GuideDialogue)}_{(int)variant}";
        GuideDialogue panel = uiManager.OpenUI<GuideDialogue>(prefabName, UILayer.Top, false);
        if (panel == null)
            return null;

        if (panel.StartDialogue(closeCallback))
            return panel;

        uiManager.DestroyUI(panel);
        return null;
    }

    /// <summary>
    /// 用途：打开指定 Variant，并等待当前对话关闭。
    /// 调用边界：由 Guide Flow 在 Unity 主线程调用，取消令牌沿用当前流程。
    /// 注意事项：外部取消会销毁本次打开的对话并返回 Cancelled。
    /// </summary>
    /// <param name="variant">需要打开的对话 Variant。</param>
    /// <param name="cancellationToken">当前 Guide Flow 的取消令牌。</param>
    /// <returns>对话正常关闭、取消或创建失败对应的 GuideFlowResult。</returns>
    public static async UniTask<GuideFlowResult> ShowAsync(
        GuideDialogueVariant variant,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return GuideFlowResult.Cancelled;

        var completionSource = new UniTaskCompletionSource<GuideFlowResult>();
        GuideDialogue panel = Create(
            variant,
            () => completionSource.TrySetResult(GuideFlowResult.Completed));
        if (panel == null)
            return GuideFlowResult.Failed;

        try
        {
            return await completionSource.Task.AttachExternalCancellation(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (panel != null && panel.IsOpen)
                UIManager.Instance?.DestroyUI(panel);

            return GuideFlowResult.Cancelled;
        }
    }

    #endregion

    #region Internal Helpers

    private bool StartDialogue(Action closeCallback)
    {
        if (btn == null || UiYindao201Txt == null || tipsKey == null || tipsKey.Count == 0)
        {
            this.LogWarning("[GuideDialogue] Required UI binding or tipsKey is missing.");
            return false;
        }

        _closeCallback = closeCallback;
        _step = 0;
        PlayTip();
        return true;
    }

    private void BindBtn()
    {
        if (btn == null)
            return;

        btn.OnClick -= PlayNextStep;
        btn.OnClick += PlayNextStep;
    }

    private void UnbindBtn()
    {
        if (btn != null)
            btn.OnClick -= PlayNextStep;
    }

    private void KillContentTween()
    {
        if (_contentTween != null && _contentTween.IsActive())
            _contentTween.Kill();

        _contentTween = null;
    }

    private void InvokeCloseCallback()
    {
        Action callback = _closeCallback;
        _closeCallback = null;
        if (callback == null)
            return;

        try
        {
            callback.Invoke();
        }
        catch (Exception exception)
        {
            this.LogError($"[GuideDialogue] Close callback failed. error:{exception}");
        }
    }

    #endregion
}

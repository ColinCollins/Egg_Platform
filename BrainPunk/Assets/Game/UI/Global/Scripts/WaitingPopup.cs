using System;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.ItemEvent;
using Guru.SDK.Framework.Utils.Ads.Data;
using I2.Loc;
using UnityEngine;

public partial class WaitingPopup : BaseUIView
{
    /// <summary>最大等待时间（秒），超时后自动关闭。0 或以下表示不限制。</summary>
    [SerializeField] private float maxWaitingSeconds = 10f;
    [SerializeField] private float manuallyCloseShowInternalSeconds = 3f;
    [SerializeField] private RotateFloatHandle rotateHandle;

    /// <summary>在关闭（包括按钮点击或超时调用 CloseStraightly）时触发的取消回调。</summary>
    private static Action? _onCancel;
    private float _startTime;
    private bool _started;
    private bool _closeBtnShown;
    private Vector3 _closeBtnOriginalScale = Vector3.one;

    private bool isAutoClose = true;

    public override void OnCreate()
    {
        base.OnCreate();

        if (CloseBtn != null)
        {
            CloseBtn.OnClick -= OnClickClose;
            CloseBtn.OnClick += OnClickClose;
            _closeBtnOriginalScale = CloseBtn.transform.localScale;
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        ResetState();
    }

    void OnEnable()
    {
        rotateHandle?.StartRotate();
    }

    private void Update()
    {
        EnsureTimerStarted();

        float elapsedSeconds = Time.unscaledTime - _startTime;
        TryShowCloseButton(elapsedSeconds);

        if (!isAutoClose)
        {
            return;
        }

        if (maxWaitingSeconds > 0f && elapsedSeconds >= maxWaitingSeconds)
        {
            SystemTips.Show(LocalizationManager.GetTranslation("U_HurryTips_GetAnswer_failed"));
            CloseStraightly();
        }
    }

    public static WaitingPopup Create(bool isAutoClose = true, float maxWaitingSeconds = 10f)
    {
        var panel = UIManager.Instance.OpenUI<WaitingPopup>(nameof(WaitingPopup), UILayer.System);
        panel.isAutoClose = isAutoClose;
        panel.maxWaitingSeconds = maxWaitingSeconds;
        return panel;
    }

    /// <summary>
    /// 设置在本次 WaitingPopup 关闭时要触发的回调（可选）。
    /// 传入 null 表示清除回调。
    /// </summary>
    public static void SetOnCancel(Action? onCancel)
    {
        _onCancel = onCancel;
    }

    public static void CloseStraightly()
    {
        _onCancel?.Invoke();
        _onCancel = null;
        UIManager.Instance.CloseUI<WaitingPopup>();
    }

    /// <summary>
    /// WaitingPopup 上的手动关闭按钮回调。
    /// 主动关闭当前 loading，并关闭等待弹窗。
    /// </summary>
    public void OnClickCancel()
    {
        CloseStraightly();
    }

    private void OnClickClose(CustomButton btn)
    {
        OnClickCancel();
    }

    public static async UniTask<AdCause> WaitingAdLoad(UniTaskCompletionSource<AdCause> completionSource)
    {
        WaitingPopup.Create();

        try
        {
            var cause = await completionSource.Task;

            if (cause == AdCause.Canceled)
            {
                // 用户在 loadingDialog 中主动取消，只关闭等待弹窗，不再提示失败
                Debug.Log($"[WaitingPopup] cause: {cause} (canceled by user)");
                return cause;
            }

            if (!cause.IsSuccess())
            {
                SystemTips.Show(LocalizationManager.GetTranslation("U_HurryTips_RewardVideo_failed"));
            }

            Debug.Log($"[WaitingPopup] cause: {cause}");

            return cause;
        }
        finally
        {
            WaitingPopup.CloseStraightly();
        }
    }

    private void ResetState()
    {
        _started = false;
        _closeBtnShown = false;
        SetCloseButtonVisible(false);
    }

    private void EnsureTimerStarted()
    {
        if (_started)
        {
            return;
        }

        _startTime = Time.unscaledTime;
        _started = true;
    }

    private void TryShowCloseButton(float elapsedSeconds)
    {
        if (_closeBtnShown || manuallyCloseShowInternalSeconds < 0f)
        {
            return;
        }

        if (elapsedSeconds < manuallyCloseShowInternalSeconds)
        {
            return;
        }

        _closeBtnShown = true;
        SetCloseButtonVisible(true);
    }

    private void SetCloseButtonVisible(bool isVisible)
    {
        if (CloseBtn == null)
        {
            return;
        }

        CloseBtn.gameObject.SetActive(isVisible);
        CloseBtn.Interactable = isVisible;
        CloseBtn.transform.DOKill();
        CloseBtn.transform.localScale = isVisible ? Vector3.zero : _closeBtnOriginalScale;

        if (isVisible)
        {
            CloseBtn.transform
                .DOScale(_closeBtnOriginalScale, 0.3f)
                .SetUpdate(true)
                .SetEase(Ease.OutBack, 3f);
        }
    }
}

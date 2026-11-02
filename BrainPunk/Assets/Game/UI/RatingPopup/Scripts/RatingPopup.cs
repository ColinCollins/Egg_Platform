using System;
using System.Collections.Generic;
using System.Threading;
using Bear.EventSystem;
using Bear.Game;
using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
#if UNITY_ANDROID
using Google.Play.Review;
#endif
using I2.Loc;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 评分弹窗：使用 prefab 内五星 Toggle、文案与提交按钮，流程对齐 Guru RatingController（无 Guru 视图依赖、无打开动画）。
/// </summary>
public partial class RatingPopup : BaseUIView, IDebuger, IEventSender
{
    private Action<int, string> _onRatingComplete;
    private Action _onRatingClosed;
    private UniTaskCompletionSource _closeCompletionSource;

    private int _ratingValue;
    private bool _ratingFinished;
    private int closeStatus = 0;
    private string _defaultRateDesText;
    private CancellationTokenSource _fiveStarCts;
    [SerializeField] private CustomButton submitBtn;
    [SerializeField] private GameObject disableBtn;

    [SerializeField] private UISpineCtrl spine;

    private readonly List<UnityAction<bool>> _starToggleHandlers = new List<UnityAction<bool>>();

    public override void OnCreate()
    {
        base.OnCreate();
        CloseBtn.OnClick += OnCloseClick;
#if UNITY_ANDROID
        _reviewManager = new ReviewManager();
#endif
    }

    public override void OnOpen()
    {
        base.OnOpen();
        ShowRating();

        spine.PlayAnimation("in", false).Complete += (track) =>
        {
            spine.PlayAnimation("idle", true);
        };
    }

    public override void OnClose()
    {
        TearDownRatingUi();

        base.OnClose();
        Action onRatingClosed = _onRatingClosed;
        _onRatingClosed = null;
        try
        {
            onRatingClosed?.Invoke();
        }
        finally
        {
            CompleteCloseWait();
        }

        // 数据打点
        GameSDKService.Instance.Rating_Star(_ratingValue, closeStatus);
    }

    /// <summary>
    /// 显示评分界面
    /// </summary>
    private void ShowRating()
    {
        _ratingFinished = false;
        _ratingValue = 0;
        StopFiveStarRoutine();

        _defaultRateDesText = LocalizationManager.GetTranslation("U_Rate_Des_01");

        ApplyStarToggles(0);
        UpdateDescriptionForRating(0);
        BindStarToggles();
        if (submitBtn != null)
        {
            submitBtn.OnClick -= OnSubmitClick;
            submitBtn.OnClick += OnSubmitClick;
        }
    }

    private void TearDownRatingUi()
    {
        UnbindStarToggles();
        StopFiveStarRoutine();
        if (submitBtn != null)
        {
            submitBtn.OnClick -= OnSubmitClick;
        }
    }

    private void BindStarToggles()
    {
        UnbindStarToggles();
        if (UIPingfenXing01Toggles == null || UIPingfenXing01Toggles.Count == 0)
        {
            return;
        }

        for (int i = 0; i < UIPingfenXing01Toggles.Count; i++)
        {
            int idx = i;
            UnityAction<bool> handler = on => OnStarToggleChanged(idx, on);
            _starToggleHandlers.Add(handler);
            UIPingfenXing01Toggles[i].interactable = true;
            UIPingfenXing01Toggles[i].onValueChanged.AddListener(handler);
        }
    }

    private void UnbindStarToggles()
    {
        if (UIPingfenXing01Toggles == null)
        {
            _starToggleHandlers.Clear();
            return;
        }

        for (int i = 0; i < _starToggleHandlers.Count && i < UIPingfenXing01Toggles.Count; i++)
        {
            UIPingfenXing01Toggles[i].interactable = false;
            UIPingfenXing01Toggles[i].onValueChanged.RemoveListener(_starToggleHandlers[i]);
        }

        _starToggleHandlers.Clear();
    }

    private void OnStarToggleChanged(int index, bool isOn)
    {
        if (_ratingFinished)
        {
            return;
        }

        if (isOn)
        {
            _ratingValue = index + 1;
            ApplyStarToggles(_ratingValue);
        }
        else
        {
            _ratingValue = index + 1;
            ApplyStarToggles(_ratingValue);
        }


        disableBtn.SetActive(_ratingValue <= 0);
        submitBtn.gameObject.SetActive(_ratingValue > 0);

        UpdateDescriptionForRating(_ratingValue);
        OnRatingValueChangedAfterSync();
    }

    private void OnRatingValueChangedAfterSync()
    {
        StopFiveStarRoutine();
        if (_ratingValue == 5)
        {
            _fiveStarCts = new CancellationTokenSource();
            CoFiveStarAutoComplete(_fiveStarCts.Token).Forget();
        }
    }

    private async UniTask CoFiveStarAutoComplete(CancellationToken cancellationToken)
    {
        if (submitBtn != null)
        {
            submitBtn.OnClick -= OnSubmitClick;
            // CloseBtn.OnClick -= OnCloseClick;
            UnbindStarToggles();
        }

        await UniTask.Delay(1000, cancellationToken: cancellationToken);
        CompleteRating(5, string.Empty);
    }

    private void StopFiveStarRoutine()
    {
        _fiveStarCts?.Cancel();
        _fiveStarCts?.Dispose();
        _fiveStarCts = null;
    }

    private void ApplyStarToggles(int rating)
    {
        if (UIPingfenXing01Toggles == null)
        {
            return;
        }

        for (int i = 0; i < UIPingfenXing01Toggles.Count; i++)
        {
            UIPingfenXing01Toggles[i].SetIsOnWithoutNotify(i < rating);
        }
    }

    private void UpdateDescriptionForRating(int rating)
    {
        if (URateDes01Txt == null)
        {
            return;
        }

        if (rating <= 0)
        {
            URateDes01Txt.text = _defaultRateDesText;
            return;
        }

        switch (rating)
        {
            case 5:
                URateDes01Txt.text = LocalizationManager.GetTranslation("U_Rate_Des_02");
                return;
            case 4:
                URateDes01Txt.text = LocalizationManager.GetTranslation("U_Rate_Des_03");
                return;
            default:
                URateDes01Txt.text = LocalizationManager.GetTranslation("U_Rate_Des_04");
                return;
        }
    }

    private void OnSubmitClick(CustomButton btn)
    {
        if (_ratingFinished || _ratingValue < 1)
        {
            return;
        }

        CompleteRating(_ratingValue, string.Empty);
    }

    private void CompleteRating(int stars, string message)
    {
        if (_ratingFinished)
        {
            return;
        }

        closeStatus = 2;
        _ratingFinished = true;
        StopFiveStarRoutine();
        OnRatingResult(stars, message);
    }

    /// <summary>
    /// 评分结果回调
    /// </summary>
    private void OnRatingResult(int stars, string message)
    {
        this.Log($"[RatingPopup] User rated: {stars} stars, feedback: {message}");

        // _onRatingClosed = null;
        _onRatingComplete?.Invoke(stars, message);

        if (stars == 5)
        {
            RequestStoreReview();
            return;
        }
        else if (stars < 5)
        {
            // 安全获取邮箱地址
            string emailUrl = string.Empty;
            if (GameSDKService.Instance == null)
            {
                this.LogError("[RatingPopup] GameSDKService.Instance is null");
            }
            else if (GameSDKService.Instance.MainAppSpec == null)
            {
                this.LogError("[RatingPopup] GameSDKService.Instance.MainAppSpec is null");
            }
            else if (GameSDKService.Instance.MainAppSpec.AppDetails == null)
            {
                this.LogError("[RatingPopup] AppDetails is null");
            }
            else
            {
                emailUrl = GameSDKService.Instance.MainAppSpec.AppDetails.EmailUrl ?? string.Empty;
                if (string.IsNullOrEmpty(emailUrl))
                {
                    this.LogError("[RatingPopup] EmailUrl is null or empty");
                }
                else
                {
                    this.Log($"[RatingPopup] EmailUrl = {emailUrl}");
                }
            }

            // 安全获取 uid
            string uid = string.Empty;
            var account = Guru.SDK.Framework.Core.Account.AccountDataStore.Instance;
            if (account == null)
            {
                this.LogError("[RatingPopup] AccountDataStore.Instance is null");
            }
            else
            {
                uid = account.Uid ?? string.Empty;
                if (string.IsNullOrEmpty(uid))
                {
                    this.LogError("[RatingPopup] account.Uid is null or empty");
                }
                else
                {
                    this.Log($"[RatingPopup] uid = {uid}");
                }
            }

            // 安全获取 deviceId
            string deviceId = string.Empty;
            if (account == null)
            {
                this.LogError("[RatingPopup] account is null, 无法获取 deviceId");
            }
            else if (account.CurrentDevice == null)
            {
                this.LogError("[RatingPopup] account.CurrentDevice is null");
            }
            else
            {
                deviceId = account.CurrentDevice.DeviceId ?? string.Empty;
                if (string.IsNullOrEmpty(deviceId))
                {
                    this.LogError("[RatingPopup] account.CurrentDevice.DeviceId is null or empty");
                }
                else
                {
                    this.Log($"[RatingPopup] deviceId = {deviceId}");
                }
            }

            // 发送邮件（即使数据为空也要尝试发送）
            string[] toEmail = new[] { emailUrl };
            this.Log("[RatingPopup] 准备发送邮件");
            EmailUtils.SendMailByRate(toEmail, uid, deviceId, stars);
            this.Log("[RatingPopup] 邮件调用完成");
        }

        UIManager.Instance.DestroyUI(this);
    }

    /// <summary>
    /// 关闭按钮点击
    /// </summary>
    private void OnCloseClick(CustomButton btn)
    {
        closeStatus = 1;
        UIManager.Instance.DestroyUI(this);
    }

    /// <summary>
    /// 请求商店评价
    /// </summary>
    private void RequestStoreReview()
    {
#if UNITY_IOS
        // iOS：不使用 Google Play In-App Review；如需系统评分可启用 StoreKit 接口
        // UnityEngine.iOS.Device.RequestStoreReview();
        UIManager.Instance.DestroyUI(this);
#elif UNITY_ANDROID
        RequestReview();
#else
        UIManager.Instance.DestroyUI(this);
#endif
    }

#if UNITY_ANDROID
    private ReviewManager _reviewManager;

    public void RequestReview()
    {
        if (_reviewManager == null)
        {
            Debug.LogError("[RatingPopup] ReviewManager is null");
            UIManager.Instance.DestroyUI(this);
            return;
        }

        var requestFlowOperation = _reviewManager.RequestReviewFlow();
        requestFlowOperation.Completed += (operation) =>
        {
            if (operation.Error != ReviewErrorCode.NoError)
            {
                Debug.LogError("请求评分失败: " + operation.Error);
                UIManager.Instance.DestroyUI(this);
                return;
            }

            var launchFlowOperation = _reviewManager.LaunchReviewFlow(operation.GetResult());
            launchFlowOperation.Completed += (launchOperation) =>
            {
                if (launchOperation.Error == ReviewErrorCode.NoError)
                {
                    Debug.Log("评分弹窗已关闭");
                    UIManager.Instance.DestroyUI(this);
                }
            };
        };
    }
#endif


    /// <summary>
    /// 打开评分弹窗并等待其完成关闭生命周期。
    /// 调用边界：供需要串行编排评分后续流程的运行时入口调用。
    /// 注意事项：外部取消会关闭仍打开的评分弹窗并继续抛出取消，由流程宿主决定是否继续。
    /// </summary>
    /// <param name="cancellationToken">流程宿主的生命周期取消令牌。</param>
    public static async UniTask ShowAndWaitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RatingPopup panel = Create();
        if (panel == null)
            return;

        try
        {
            await panel.WaitForCloseAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (panel != null && panel.IsOpen)
                UIManager.Instance?.DestroyUI(panel);

            throw;
        }
    }

    /// <summary>
    /// 创建评分弹窗
    /// </summary>
    public static RatingPopup Create(Action<int, string> onComplete = null, Action onClosed = null)
    {
        var panel = UIManager.Instance?.OpenUI<RatingPopup>(nameof(RatingPopup), UILayer.Popup);
        if (panel == null)
            return null;

        panel._onRatingComplete = onComplete;
        panel._onRatingClosed = onClosed;

        RatingPopupPolicy.MarkShown();

        return panel;
    }

    private async UniTask WaitForCloseAsync(CancellationToken cancellationToken)
    {
        if (!IsOpen)
            return;

        UniTaskCompletionSource completionSource = new UniTaskCompletionSource();
        _closeCompletionSource = completionSource;
        try
        {
            await completionSource.Task.AttachExternalCancellation(cancellationToken);
        }
        finally
        {
            if (ReferenceEquals(_closeCompletionSource, completionSource))
                _closeCompletionSource = null;
        }
    }

    private void CompleteCloseWait()
    {
        UniTaskCompletionSource completionSource = _closeCompletionSource;
        _closeCompletionSource = null;
        completionSource?.TrySetResult();
    }
}

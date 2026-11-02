using System;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Common;
using TMPro;
using UnityEngine;

/// <summary>
/// 提供一个定时器，默认时间为 72 小时, 定时器在 Update 中逐渐减少时间。格式为 00:00:00
/// SpeedUP 点击一次可以减少 1s.
/// 检测 customSlider 当前角度到目标角度的进度，在 1 倍到 Multiple 倍之间插值消耗。
/// </summary>
public partial class Level43LoadingPopup : BaseUIView
{
    [SerializeField] private CustomButton SpeedUpBtn;
    [SerializeField] private CustomButton OkBtn;
    [SerializeField] private UICustomSlider customSlider;

    [SerializeField] private ScoreFlyText scoreFlyTextPrefab;

    [Header("加速配置")]
    [Tooltip("点击一次减少的时间（秒）")]
    [SerializeField] private float clickCostTime = 5f;

    [Header("角度检测配置")]
    [Tooltip("用于角度检测的滑块（带旋转的）")]
    [SerializeField] private UIDragRotate angleSlider;

    [Tooltip("目标角度（达到此角度时应用最大倍率）")]
    [SerializeField] private float angleThreshold = 45f;

    [Tooltip("超过阈值时的倍率")]
    [SerializeField] private float speedUpMultiple = 2f;

    [SerializeField] private float fakeSpeedUpMultiple = 2f;

    [Tooltip("是否使用绝对角度")]
    [SerializeField] private bool useAbsoluteAngle = true;

    // 默认 72 小时 = 72 * 3600 秒
    private const float DEFAULT_TOTAL_TIME = 8f * 3600f;
    private float currentTime;
    private float totalTime;

    private bool isStart = false;
    private bool isFinished = false;

    private bool fakeSpeedUp = false;

    public bool isClickOK = false;

    private Action OnComplete;
    private Tween _fakeSpeedUpTween;

    public override void OnOpen()
    {
        base.OnOpen();
        ResetTimer();
    }

    public override void OnCreate()
    {
        base.OnCreate();

        // 注册加速按钮点击事件
        if (SpeedUpBtn != null)
        {
            SpeedUpBtn.OnClick += OnSpeedUpClick;
        }

        if (OkBtn != null)
        {
            OkBtn.gameObject.SetActive(false);
            OkBtn.OnClick += CompleteLoading;
        }

        // 防护：检查关键组件是否缺失
        if (SpeedUpBtn == null)
            Debug.LogWarning("[Level43LoadingPopup] SpeedUpBtn is not assigned!", this);
        if (OkBtn == null)
            Debug.LogWarning("[Level43LoadingPopup] OkBtn is not assigned!", this);
        if (customSlider == null)
            Debug.LogWarning("[Level43LoadingPopup] customSlider is not assigned!", this);
        if (angleSlider == null)
            Debug.LogWarning("[Level43LoadingPopup] angleSlider is not assigned!", this);
        if (scoreFlyTextPrefab == null)
            Debug.LogWarning("[Level43LoadingPopup] scoreFlyTextPrefab is not assigned!", this);
    }

    private void CompleteLoading(CustomButton btn)
    {
        // 防护：防止重复触发
        if (isClickOK)
            return;

        isClickOK = true;
        isFinished = true;
        OnComplete?.Invoke();
        OnComplete = null;
    }

    public override void OnClose()
    {
        base.OnClose();

        // 移除按钮监听
        if (SpeedUpBtn != null)
        {
            SpeedUpBtn.OnClick -= OnSpeedUpClick;
        }

        if (OkBtn != null)
        {
            OkBtn.OnClick -= CompleteLoading;
        }

        // 防护：清理 DOTween，防止对象销毁后 tween 回调访问已销毁对象
        _fakeSpeedUpTween?.Kill();
        _fakeSpeedUpTween = null;
    }

    void Update()
    {
        if (isFinished || !isStart)
            return;

        // 倒计时逻辑
        if (currentTime > 0)
        {
            // 检测角度，自然消耗按当前角度到目标角度的进度插值应用倍率
            float currentSpeedUpMultiple = fakeSpeedUp ? fakeSpeedUpMultiple : GetAngleSpeedUpMultiplier();
            float deltaTime = Time.deltaTime * currentSpeedUpMultiple;

            currentTime -= deltaTime;
            if (currentTime < 0)
            {
                currentTime = 0;
            }
            UpdateUI();
        }
        else
        {
            isFinished = true;

            if (OkBtn != null)
                OkBtn.gameObject.SetActive(true);
            if (SpeedUpBtn != null)
                SpeedUpBtn.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 重置计时器为默认值
    /// </summary>
    private void ResetTimer()
    {
        totalTime = DEFAULT_TOTAL_TIME;
        currentTime = totalTime;
        isStart = true;
        UpdateUI();

        fakeSpeedUp = true;
        WaitingToCloseSpeedUp();
    }

    private void WaitingToCloseSpeedUp()
    {
        DOTween.To(() => fakeSpeedUpMultiple, (x) =>
        {
            fakeSpeedUpMultiple = x;
        }, 0, 3f).SetEase(Ease.Linear).OnComplete(() =>
        {
            fakeSpeedUp = false;
        });
    }

    /// <summary>
    /// 更新 UI 显示
    /// </summary>
    private void UpdateUI()
    {
        // 更新时间文本
        if (UiJiazai4Txt != null)
        {
            UiJiazai4Txt.text = FormatTime(currentTime);
        }

        // 更新进度条
        if (customSlider != null)
        {
            float progress = 1f - (currentTime / totalTime);
            customSlider.SetProcess(progress);
        }
    }

    /// <summary>
    /// 将秒数格式化为 00:00:00
    /// </summary>
    private string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.FloorToInt(seconds);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int secs = totalSeconds % 60;
        return $"{hours:D2}:{minutes:D2}:{secs:D2}";
    }

    /// <summary>
    /// 加速按钮点击回调
    /// </summary>
    private void OnSpeedUpClick(CustomButton btn)
    {
        // 防护：状态与空引用检查
        if (isFinished || !isStart || SpeedUpBtn == null)
        {
            return;
        }

        // 减少点击配置的固定时间
        currentTime -= clickCostTime;
        CreateScoreFlyText((int)clickCostTime);
        if (currentTime < 0)
        {
            currentTime = 0;
        }
        UpdateUI();
    }

    /// <summary>
    /// 创建分数飞字效果
    /// </summary>
    private void CreateScoreFlyText(int scoreValue)
    {
        if (scoreFlyTextPrefab == null || ObjectPoolManager.Instance == null)
        {
            return;
        }

        ScoreFlyText flyText = ObjectPoolManager.Instance.Get<ScoreFlyText>();
        if (flyText == null)
        {
            Debug.LogWarning("[Level31Ctrl] Failed to get ScoreFlyText from pool!");
            return;
        }

        // 使用 score UI 的位置作为起始位置
        if (UiJiazai4Txt == null)
        {
            Debug.LogWarning("[Level43LoadingPopup] UiJiazai4Txt is null, cannot create score fly text.");
            return;
        }
        Vector3 startPosition = UiJiazai4Txt.transform.position + new Vector3(50, 30f, 0);

        var parent = scoreFlyTextPrefab.transform.parent;
        if (parent == null)
        {
            Debug.LogWarning("[Level43LoadingPopup] scoreFlyTextPrefab.transform.parent is null, cannot create score fly text.");
            return;
        }

        flyText.Setup(scoreValue, startPosition, parent, 50f, 0.3f);
    }

    /// <summary>
    /// 根据当前角度到目标角度的进度计算加速倍率
    /// </summary>
    private float GetAngleSpeedUpMultiplier()
    {
        if (angleSlider == null)
        {
            return 1f;
        }

        float currentAngle = angleSlider.GetCurrentAngle();
        float progress;

        if (useAbsoluteAngle)
        {
            float targetAngle = Mathf.Abs(angleThreshold);
            progress = Mathf.Approximately(targetAngle, 0f)
                ? 1f
                : Mathf.InverseLerp(0f, targetAngle, Mathf.Abs(currentAngle));
        }
        else
        {
            progress = Mathf.Approximately(angleThreshold, 0f)
                ? 1f
                : Mathf.InverseLerp(0f, angleThreshold, currentAngle);
        }

        return Mathf.Lerp(1f, speedUpMultiple, progress);
    }

    /// <summary>
    /// 设置剩余时间（用于保存/恢复进度）
    /// </summary>
    public void SetRemainTime(float remainSeconds)
    {
        currentTime = Mathf.Max(0, remainSeconds);
        UpdateUI();
    }

    /// <summary>
    /// 获取剩余时间
    /// </summary>
    public float GetRemainTime()
    {
        return currentTime;
    }

    /// <summary>
    /// 设置完成回调
    /// </summary>
    public void SetCompleteCallback(Action complete)
    {
        OnComplete = complete;
    }

    /// <summary>
    /// 添加完成回调
    /// </summary>
    public void AddCompleteCallback(Action complete)
    {
        OnComplete += complete;
    }

    /// <summary>
    /// 设置角度检测参数
    /// </summary>
    public void SetAngleCheckParams(float threshold, float multiple, bool absolute = true)
    {
        angleThreshold = threshold;
        speedUpMultiple = multiple;
        useAbsoluteAngle = absolute;
    }

    public static Level43LoadingPopup Create(Action complete = null)
    {
        if (UIManager.Instance == null)
        {
            Debug.LogError("[Level43LoadingPopup] UIManager.Instance is null. Cannot create popup.");
            return null;
        }

        var panel = UIManager.Instance.OpenUI<Level43LoadingPopup>($"{typeof(Level43LoadingPopup).Name}", UILayer.Normal);
        if (panel == null)
        {
            Debug.LogError("[Level43LoadingPopup] Failed to open Level43LoadingPopup from UIManager.");
            return null;
        }

        panel.OnComplete = complete;
        return panel;
    }
}

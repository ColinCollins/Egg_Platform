using DG.Tweening;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// Level099 问号块的私有顶击反馈。每次触发都会中断旧动画、硬复位并立即重播。
/// </summary>
public sealed class Level99QuestionBlockFeedback : BaseItemEventHandle
{
    #region Serialized References And Parameters

    /// <summary>只移动问号块的显示节点，不移动射线和碰撞体。</summary>
    [Header("References")]
    [SerializeField] private Transform blockVisual;
    /// <summary>达到本关金币目标后显示的灰色问号块节点。</summary>
    [SerializeField] private Transform lockedBlockVisual;
    /// <summary>从问号块后方冒出的金币显示节点。</summary>
    [SerializeField] private Transform coinVisual;
    /// <summary>金币渐显、渐隐使用的 SpriteRenderer。</summary>
    [SerializeField] private SpriteRenderer coinRenderer;

    /// <summary>问号块被顶起的高度。</summary>
    [Header("Block Bump")]
    [SerializeField, Min(0f)] private float blockBumpHeight = 0.38f;
    /// <summary>问号块向上顶起的时长。</summary>
    [SerializeField, Min(0.01f)] private float blockRiseDuration = 0.055f;
    /// <summary>问号块快速回落到基线下方的距离。</summary>
    [SerializeField, Min(0f)] private float blockReturnOvershoot = 0.035f;
    /// <summary>问号块快速回落的时长。</summary>
    [SerializeField, Min(0.01f)] private float blockReturnDuration = 0.075f;
    /// <summary>问号块从轻微下压位置复位的时长。</summary>
    [SerializeField, Min(0.01f)] private float blockSettleDuration = 0.035f;

    /// <summary>金币出现时相对原始位置的抬升量。</summary>
    [Header("Coin Pop")]
    [SerializeField] private float coinStartOffsetY = 0.5f;
    /// <summary>金币第一段上升的目标高度。</summary>
    [SerializeField] private float coinRiseHeight = 1.8f;
    /// <summary>金币第一段上升时长。</summary>
    [SerializeField, Min(0.01f)] private float coinRiseDuration = 0.18f;
    /// <summary>金币在最高点附近停留的时长。</summary>
    [SerializeField, Min(0f)] private float coinHoldDuration = 0.06f;
    /// <summary>金币渐隐阶段的最终高度。</summary>
    [SerializeField] private float coinFadeHeight = 2.2f;
    /// <summary>金币最后上浮并渐隐的时长。</summary>
    [SerializeField, Min(0.01f)] private float coinFadeDuration = 0.1f;
    #endregion

    #region Runtime State

    private Sequence activeSequence;
    private Vector3 blockRestLocalPosition;
    private Vector3 coinRestLocalPosition;
    private Vector3 coinRestLocalScale;
    private Color coinRestColor;
    private bool initialized;
    private bool pauseRequested;
    private bool completedVisual;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        EnsureInitialized();
        RestoreVisualPose();
    }

    #endregion

    #region Public Control

    /// <summary>播放一次顶击动画；表现立即结束事件节点，不阻塞下一次顶击。</summary>
    public override void Execute()
    {
        EnsureInitialized();
        PlayAnimation();
        IsRunning = false;
        IsDone = true;
    }

    /// <summary>随关卡暂停冻结当前顶击动画。</summary>
    public void PauseAnimation()
    {
        pauseRequested = true;
        if (activeSequence != null && activeSequence.IsActive())
            activeSequence.Pause();
    }

    /// <summary>仅在关卡重新进入 PLAYING 后继续当前顶击动画。</summary>
    public void ResumeAnimation()
    {
        pauseRequested = false;
        if (activeSequence != null && activeSequence.IsActive())
            activeSequence.Play();
    }

    /// <summary>中止动画并恢复问号块与金币的初始显示状态。</summary>
    public void ResetVisual()
    {
        EnsureInitialized();
        activeSequence?.Kill(false);
        activeSequence = null;
        completedVisual = false;
        RestoreVisualPose();
        IsRunning = false;
        IsDone = false;
    }

    /// <summary>切换为不可继续顶击的灰色问号块，并保留正在播放的最后一枚金币反馈。</summary>
    public void ShowCompletedVisual()
    {
        EnsureInitialized();
        completedVisual = true;

        if (blockVisual != null)
        {
            blockVisual.localPosition = blockRestLocalPosition;
            blockVisual.gameObject.SetActive(false);
        }
        if (lockedBlockVisual != null)
            lockedBlockVisual.gameObject.SetActive(true);

        IsRunning = false;
        IsDone = true;
    }

    #endregion

    #region Animation

    private void PlayAnimation()
    {
        activeSequence?.Kill(false);
        activeSequence = null;
        RestoreVisualPose();

        if (blockVisual == null)
            return;

        float riseDuration = Mathf.Max(0.01f, blockRiseDuration);
        float returnDuration = Mathf.Max(0.01f, blockReturnDuration);
        float settleDuration = Mathf.Max(0.01f, blockSettleDuration);
        activeSequence = DOTween.Sequence().SetLink(gameObject);

        activeSequence.Insert(
            0f,
            blockVisual.DOLocalMoveY(
                    blockRestLocalPosition.y + Mathf.Max(0f, blockBumpHeight),
                    riseDuration)
                .SetEase(Ease.OutQuad));
        activeSequence.Insert(
            riseDuration,
            blockVisual.DOLocalMoveY(
                    blockRestLocalPosition.y - Mathf.Max(0f, blockReturnOvershoot),
                    returnDuration)
                .SetEase(Ease.InQuad));
        activeSequence.Insert(
            riseDuration + returnDuration,
            blockVisual.DOLocalMoveY(blockRestLocalPosition.y, settleDuration)
                .SetEase(Ease.OutQuad));

        AddCoinAnimation(activeSequence);
        Sequence sequence = activeSequence;
        sequence.OnComplete(() =>
        {
            if (activeSequence != sequence)
                return;

            activeSequence = null;
            RestoreVisualPose();
        });

        if (pauseRequested)
            sequence.Pause();
    }

    private void AddCoinAnimation(Sequence sequence)
    {
        if (sequence == null || coinVisual == null || coinRenderer == null)
            return;

        coinVisual.gameObject.SetActive(true);
        Vector3 startPosition = coinRestLocalPosition + Vector3.up * coinStartOffsetY;
        coinVisual.localPosition = startPosition;

        Color visibleColor = coinRestColor;
        visibleColor.a = 1f;
        coinRenderer.color = visibleColor;

        float riseDuration = Mathf.Max(0.01f, coinRiseDuration);
        float fadeDuration = Mathf.Max(0.01f, coinFadeDuration);
        float fadeStart = riseDuration + Mathf.Max(0f, coinHoldDuration);

        sequence.Insert(
            0f,
            coinVisual.DOLocalMoveY(coinRestLocalPosition.y + coinRiseHeight, riseDuration)
                .SetEase(Ease.OutCubic));
        sequence.Insert(
            fadeStart,
            coinVisual.DOLocalMoveY(coinRestLocalPosition.y + coinFadeHeight, fadeDuration)
                .SetEase(Ease.InQuad));
        sequence.Insert(
            fadeStart,
            coinRenderer.DOFade(0f, fadeDuration).SetEase(Ease.InQuad));
    }

    #endregion

    #region Initialization And Cleanup

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        blockVisual ??= transform.Find("img");
        lockedBlockVisual ??= transform.Find("img_lock");
        coinVisual ??= transform.Find("Coin");
        coinRenderer ??= coinVisual != null ? coinVisual.GetComponent<SpriteRenderer>() : null;

        if (blockVisual != null)
            blockRestLocalPosition = blockVisual.localPosition;

        if (coinVisual != null)
        {
            coinRestLocalPosition = coinVisual.localPosition;
            coinRestLocalScale = coinVisual.localScale;
        }

        if (coinRenderer != null)
            coinRestColor = coinRenderer.color;

        initialized = true;
    }

    private void RestoreVisualPose()
    {
        if (!initialized)
            return;

        if (blockVisual != null)
        {
            blockVisual.gameObject.SetActive(!completedVisual);
            blockVisual.localPosition = blockRestLocalPosition;
        }
        if (lockedBlockVisual != null)
            lockedBlockVisual.gameObject.SetActive(completedVisual);

        if (coinVisual == null)
            return;

        coinVisual.gameObject.SetActive(true);
        coinVisual.localPosition = coinRestLocalPosition;
        coinVisual.localScale = coinRestLocalScale;

        if (coinRenderer != null)
        {
            Color hiddenColor = coinRestColor;
            hiddenColor.a = 0f;
            coinRenderer.color = hiddenColor;
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        StopActiveSequence();
        RestoreVisualPose();
    }

    private void OnDestroy() => StopActiveSequence();

    private void StopActiveSequence()
    {
        activeSequence?.Kill(false);
        activeSequence = null;
    }

    #endregion
}

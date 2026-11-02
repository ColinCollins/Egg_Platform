using System;
using DG.Tweening;
using Game.ItemEvent;
using Game.Level083;
using UnityEngine;

/// <summary>
/// Level 083 单个乐器候选气泡；点击后把选择交给本关控制器。
/// 弹出完成后进入气泡待机浮动；收回时播放弹出的倒放镜像；
/// 选中后气泡底原地 InBack 缩小消失，乐器独自以小抛物线飞手并逐渐缩小到
/// 对齐 Spine 演奏素材大小的目标缩放。
/// </summary>
public sealed class Level83InstrumentOption : MonoBehaviour
{
    private const float ShowDuration = 0.3f;
    private const float DismissDuration = 0.3f;

    #region Inspector

    /// <summary>该候选对应的乐器类型。</summary>
    [SerializeField] private Level83Instrument instrument;

    /// <summary>接收选择结果的 Level 083 本地控制器。</summary>
    [SerializeField] private Level83GameplayCtrl gameplayCtrl;

    /// <summary>气泡内部实际移动到主角手中的乐器图形。</summary>
    [SerializeField] private Transform instrumentVisual;

    /// <summary>选中后乐器从候选位置抛物线移动到主角手中的时长。</summary>
    [SerializeField] private float moveToHandDuration = 0.45f;

    /// <summary>乐器抛物线飞手的单段弧高（DOJump jumpPower，世界单位）。</summary>
    [SerializeField] private float flightJumpPower = 0.5f;

    /// <summary>到手时乐器缩放相对候选缩放的目标系数，用于对齐 Spine 演奏素材大小。</summary>
    [SerializeField] private float handScaleFactor = 1f;

    /// <summary>气泡待机浮动组件（共享 MoveFloatHandle，本关只调用公开 API）。</summary>
    [SerializeField] private MoveFloatHandle floatHandle;

    #endregion

    #region Runtime State

    private bool isSelectable;
    private Transform bubbleVisual;
    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Vector3 originalBubbleScale;
    private Vector3 originalInstrumentScale;
    private Vector3 originalInstrumentLocalPosition;
    private Sequence showSequence;
    private Sequence dismissSequence;
    private Sequence moveToHandSequence;
    private bool hasCachedVisualScale;

    #endregion

    #region Unity Messages

    private void OnMouseUpAsButton()
    {
        if (isSelectable)
            gameplayCtrl?.SelectInstrument(instrument, this);
    }

    private void OnDestroy()
    {
        showSequence?.Kill();
        dismissSequence?.Kill();
        moveToHandSequence?.Kill();
        bubbleVisual?.DOKill();
        instrumentVisual?.DOKill();
    }

    #endregion

    #region Public API

    /// <summary>控制候选是否响应点击；隐藏或演奏锁定期间必须关闭。</summary>
    public void SetSelectable(bool selectable)
    {
        isSelectable = selectable;
    }

    /// <summary>
    /// 以气泡自身中心播放候选弹出表现；延迟由编排器用于三个候选的错峰出现。
    /// 弹出完成后进入气泡待机浮动。
    /// </summary>
    public void PlayShow(float delay)
    {
        CacheVisualScale();
        showSequence?.Kill();
        dismissSequence?.Kill();
        dismissSequence = null;
        bubbleVisual?.DOKill();
        instrumentVisual?.DOKill();

        StopIdleFloat();
        transform.localPosition = originalLocalPosition;

        if (bubbleVisual != null)
        {
            bubbleVisual.gameObject.SetActive(true);
            bubbleVisual.localScale = Vector3.zero;
        }
        if (instrumentVisual != null)
        {
            instrumentVisual.gameObject.SetActive(true);
            instrumentVisual.localScale = Vector3.zero;
        }

        showSequence = DOTween.Sequence().SetLink(gameObject);
        if (delay > 0f)
            showSequence.AppendInterval(delay);
        if (bubbleVisual != null)
            showSequence.Append(bubbleVisual.DOScale(originalBubbleScale, ShowDuration).SetEase(Ease.OutBack));
        if (instrumentVisual != null)
        {
            Tween instrumentTween = instrumentVisual.DOScale(originalInstrumentScale, ShowDuration).SetEase(Ease.OutBack);
            if (bubbleVisual != null)
                showSequence.Join(instrumentTween);
            else
                showSequence.Append(instrumentTween);
        }
        showSequence.OnComplete(() =>
        {
            showSequence = null;
            StartIdleFloat();
        });
    }

    /// <summary>
    /// 播放弹出的倒放镜像：停止浮动后按 InBack 缩小至消失；延迟由编排器用于反向错峰。
    /// </summary>
    public void PlayDismiss(float delay, Action onCompleted = null)
    {
        CacheVisualScale();
        showSequence?.Kill();
        showSequence = null;
        dismissSequence?.Kill();
        bubbleVisual?.DOKill();
        instrumentVisual?.DOKill();

        StopIdleFloat();

        dismissSequence = DOTween.Sequence().SetLink(gameObject);
        if (delay > 0f)
            dismissSequence.AppendInterval(delay);
        if (bubbleVisual != null)
            dismissSequence.Append(bubbleVisual.DOScale(Vector3.zero, DismissDuration).SetEase(Ease.InBack));
        if (instrumentVisual != null)
        {
            Tween instrumentTween = instrumentVisual.DOScale(Vector3.zero, DismissDuration).SetEase(Ease.InBack);
            if (bubbleVisual != null)
                dismissSequence.Join(instrumentTween);
            else
                dismissSequence.Append(instrumentTween);
        }
        dismissSequence.OnComplete(() =>
        {
            dismissSequence = null;
            onCompleted?.Invoke();
        });
    }

    /// <summary>
    /// 停止候选弹出与浮动并恢复静态摆放；已选乐器移动到手部后不得中断其移动 Tween。
    /// </summary>
    public void HideImmediately(bool preserveInstrumentVisual)
    {
        CacheVisualScale();
        showSequence?.Kill();
        showSequence = null;
        dismissSequence?.Kill();
        dismissSequence = null;
        StopIdleFloat();

        if (transform.parent == originalParent)
            transform.localPosition = originalLocalPosition;

        if (bubbleVisual != null)
        {
            bubbleVisual.DOKill();
            bubbleVisual.localScale = originalBubbleScale;
        }

        if (preserveInstrumentVisual || instrumentVisual == null)
            return;

        moveToHandSequence?.Kill();
        moveToHandSequence = null;
        instrumentVisual.DOKill();
        if (instrumentVisual.parent != transform)
        {
            instrumentVisual.SetParent(transform, true);
            instrumentVisual.localPosition = originalInstrumentLocalPosition;
        }
        instrumentVisual.gameObject.SetActive(true);
        instrumentVisual.localScale = originalInstrumentScale;
    }

    /// <summary>
    /// 停止浮动后，气泡底点击瞬间原地 InBack 缩小消失；乐器独自脱离气泡，
    /// 以 DOJump 单段上凸抛物线飞到主角手部锚点，并同时从候选缩放逐渐缩小到
    /// 原缩放 × handScaleFactor（对齐 Spine 演奏素材大小）；到手后隐藏候选乐器图形。
    /// 每次选定（含乐器重选）都会调用。
    /// </summary>
    public void MoveInstrumentToHand(Transform handAnchor, Action onCompleted = null)
    {
        if (instrumentVisual == null || handAnchor == null)
        {
            onCompleted?.Invoke();
            return;
        }

        CacheVisualScale();
        showSequence?.Kill();
        showSequence = null;
        dismissSequence?.Kill();
        dismissSequence = null;
        moveToHandSequence?.Kill();
        StopIdleFloat();

        instrumentVisual.gameObject.SetActive(true);
        instrumentVisual.DOKill();
        bubbleVisual?.DOKill();

        // 选中气泡底：原地 InBack 缩小消失，不跟随乐器飞行。
        if (bubbleVisual != null)
        {
            bubbleVisual.localScale = originalBubbleScale;
            bubbleVisual.DOScale(Vector3.zero, DismissDuration)
                .SetEase(Ease.InBack)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    if (bubbleVisual != null)
                        bubbleVisual.gameObject.SetActive(false);
                });
        }

        // 乐器独自抛物线飞手：脱离气泡挂到手部锚点下，DOJump 上凸弧 + 线性缩小。
        instrumentVisual.SetParent(handAnchor, true);
        Vector3 flightStartScale = instrumentVisual.localScale;
        moveToHandSequence = DOTween.Sequence().SetLink(gameObject);
        moveToHandSequence.Join(
            instrumentVisual.DOJump(handAnchor.position, flightJumpPower, 1, moveToHandDuration)
                .SetEase(Ease.Linear));
        moveToHandSequence.Join(
            instrumentVisual.DOScale(flightStartScale * handScaleFactor, moveToHandDuration)
                .SetEase(Ease.Linear));
        moveToHandSequence.OnComplete(() =>
        {
            moveToHandSequence = null;
            instrumentVisual.gameObject.SetActive(false);
            onCompleted?.Invoke();
        });
    }

    /// <summary>
    /// 乐器重选前把上一轮飞手的候选乐器图形静默归位：恢复父级、本地位置与候选缩放并保持隐藏，
    /// 不做飞回动画（与胜利收口时乐器从手中消失一致）；归位后由 PlayShow 统一弹出。
    /// </summary>
    public void RestoreInstrumentVisualSilently()
    {
        CacheVisualScale();
        moveToHandSequence?.Kill();
        moveToHandSequence = null;
        if (instrumentVisual == null)
            return;

        instrumentVisual.DOKill();
        if (instrumentVisual.parent != transform)
        {
            instrumentVisual.SetParent(transform, true);
            instrumentVisual.localPosition = originalInstrumentLocalPosition;
        }
        instrumentVisual.localScale = originalInstrumentScale;
        instrumentVisual.gameObject.SetActive(false);
    }

    #endregion

    #region Helpers

    private void StartIdleFloat()
    {
        if (floatHandle != null)
            floatHandle.StartMove();
    }

    private void StopIdleFloat()
    {
        if (floatHandle != null)
            floatHandle.StopMove();
    }

    private void CacheVisualScale()
    {
        if (hasCachedVisualScale)
            return;

        bubbleVisual = transform.Find("Bubble");
        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalBubbleScale = bubbleVisual != null ? bubbleVisual.localScale : Vector3.one;
        originalInstrumentScale = instrumentVisual != null ? instrumentVisual.localScale : Vector3.one;
        originalInstrumentLocalPosition = instrumentVisual != null
            ? instrumentVisual.localPosition
            : Vector3.zero;
        hasCachedVisualScale = true;
    }

    #endregion
}

using DG.Tweening;
using UnityEngine;

/// <summary>
/// Level110 题面图标拖拽；无效投放统一回原位，只有草莓可被角色吃掉。
/// </summary>
public sealed class Level110DraggableItem : DragableItem
{
    #region Inspector

    [Tooltip("Level110 总控。")]
    [SerializeField] private Level110Ctrl owner;

    [Tooltip("当前图标是否为唯一可被角色吃掉的草莓。")]
    [SerializeField] private bool isStrawberry;

    [Tooltip("图标渲染器；同时用于精确点击和投放范围判断。")]
    [SerializeField] private SpriteRenderer itemRenderer;

    [Tooltip("无效投放返回初始位置的持续时间。")]
    [SerializeField, Min(0.05f)] private float returnDuration = 0.24f;

    [Tooltip("拖动时临时提高的渲染顺序，避免图标被题面或角色遮住。")]
    [SerializeField, Min(1)] private int draggingOrderOffset = 300;

    #endregion

    #region Runtime State

    private Tween motionTween;
    private Vector3 originWorldPosition;
    private Vector3 originLocalScale;
    private Color originColor = Color.white;
    private int originSortingOrder;
    private bool hasCachedOrigin;
    private bool isConsumed;

    /// <summary>
    /// 当前图标的 SpriteRenderer，供 Level110 总控执行角色投放相交判定。
    /// </summary>
    public SpriteRenderer ItemRenderer
    {
        get
        {
            EnsureCached();
            return itemRenderer;
        }
    }

    #endregion

    #region Drag Overrides

    protected override bool IsTouchOnObject(Vector2 screenPosition)
    {
        EnsureCached();
        if (itemRenderer == null)
            return base.IsTouchOnObject(screenPosition);

        Vector3 worldPosition = ScreenToWorldPosition(screenPosition);
        return itemRenderer.bounds.Contains(new Vector3(
            worldPosition.x,
            worldPosition.y,
            itemRenderer.bounds.center.z));
    }

    protected override void OnDragStart()
    {
        EnsureCached();
        motionTween?.Kill();
        motionTween = null;

        if (itemRenderer != null)
        {
            itemRenderer.color = originColor;
            itemRenderer.sortingOrder = originSortingOrder + draggingOrderOffset;
        }

        transform.localScale = originLocalScale;
        base.OnDragStart();
    }

    protected override void OnDragEnd()
    {
        base.OnDragEnd();
        if (isConsumed)
            return;

        if (isStrawberry && owner != null && owner.TryConsumeStrawberry(this))
            return;

        ReturnToOrigin();
    }

    #endregion

    #region Consume And Reset

    /// <summary>
    /// 播放草莓向角色嘴部靠拢、缩小并淡出的消费效果。
    /// 调用边界：只由 Level110Ctrl 在草莓投放命中角色后调用。
    /// 注意事项：完成后仅隐藏当前实例；关卡重开由预制体重建恢复初态。
    /// </summary>
    public void BeginConsume(Vector3 targetWorldPosition, float duration)
    {
        if (isConsumed)
            return;

        EnsureCached();
        isConsumed = true;
        SetEnableDrag(false);
        motionTween?.Kill();

        float safeDuration = Mathf.Max(0.05f, duration);
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Join(transform.DOMove(targetWorldPosition, safeDuration).SetEase(Ease.InQuad));
        sequence.Join(transform.DOScale(Vector3.zero, safeDuration).SetEase(Ease.InBack));
        if (itemRenderer != null)
            sequence.Join(itemRenderer.DOFade(0f, safeDuration).SetEase(Ease.InQuad));
        sequence.OnComplete(() => gameObject.SetActive(false));
        motionTween = sequence;
    }

    private void ReturnToOrigin()
    {
        EnsureCached();
        motionTween?.Kill();

        float safeDuration = Mathf.Max(0.05f, returnDuration);
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Join(transform.DOMove(originWorldPosition, safeDuration).SetEase(Ease.OutQuad));
        sequence.Join(transform.DOScale(originLocalScale, safeDuration).SetEase(Ease.OutQuad));
        if (itemRenderer != null)
            sequence.Join(itemRenderer.DOFade(originColor.a, safeDuration).SetEase(Ease.OutQuad));
        sequence.OnComplete(() =>
        {
            if (itemRenderer != null)
                itemRenderer.sortingOrder = originSortingOrder;
            motionTween = null;
        });
        motionTween = sequence;
    }

    private void EnsureCached()
    {
        if (hasCachedOrigin)
            return;

        itemRenderer ??= GetComponent<SpriteRenderer>();
        originWorldPosition = transform.position;
        originLocalScale = transform.localScale;
        if (itemRenderer != null)
        {
            originColor = itemRenderer.color;
            originSortingOrder = itemRenderer.sortingOrder;
        }

        hasCachedOrigin = true;
    }

    #endregion
}

using System;
using DG.Tweening;
using UnityEngine;

/// <summary>Level111 世界物件拖拽；无效投放回异常初始位，有效投放交给总控吸附。</summary>
public sealed class Level111WorldDragItem : DragableItem
{
    private const int DragSortingOffset = 500;

    private Level111Ctrl owner;
    private Renderer[] renderers;
    private Collider2D[] colliders;
    private int[] originalSortingOrders;
    private Vector3 originPosition;
    private Quaternion originRotation;
    private Vector3 originScale;
    private Tween motionTween;
    private bool configured;

    public Level111RepairKind Kind { get; private set; }
    public Vector3 OriginScale => originScale;

    public void Configure(Level111Ctrl levelOwner, Level111RepairKind repairKind)
    {
        owner = levelOwner;
        Kind = repairKind;
        originPosition = transform.position;
        originRotation = transform.rotation;
        originScale = transform.localScale;
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);
        originalSortingOrders = new int[renderers.Length];
        for (int index = 0; index < renderers.Length; index++)
            originalSortingOrders[index] = renderers[index] != null ? renderers[index].sortingOrder : 0;
        configured = true;
        SetEnableDrag(true);
    }

    protected override bool IsTouchOnObject(Vector2 screenPosition)
    {
        if (!configured)
            return base.IsTouchOnObject(screenPosition);

        Vector3 world = ScreenToWorldPosition(screenPosition);
        Vector2 point = new Vector2(world.x, world.y);

        for (int index = 0; index < colliders.Length; index++)
        {
            Collider2D collider = colliders[index];
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy && collider.OverlapPoint(point))
                return true;
        }

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            Bounds bounds = renderer.bounds;
            if (point.x >= bounds.min.x && point.x <= bounds.max.x &&
                point.y >= bounds.min.y && point.y <= bounds.max.y)
                return true;
        }

        return false;
    }

    protected override bool IsMouseOnObject(Vector2 screenPosition)
    {
        return IsTouchOnObject(screenPosition);
    }

    protected override void OnDragStart()
    {
        motionTween?.Kill();
        motionTween = null;
        SetSortingRaised(true);
        base.OnDragStart();
    }

    protected override void OnDragEnd()
    {
        base.OnDragEnd();
        if (owner != null && owner.TryCompleteWorldRepair(this))
            return;

        ReturnToOrigin();
    }

    public void SnapTo(
        Vector3 targetPosition,
        Quaternion targetRotation,
        Vector3 targetScale,
        float duration,
        Action onComplete,
        bool deferPoseChangeUntilArrival = false)
    {
        SetEnableDrag(false);
        motionTween?.Kill();
        SetSortingRaised(false);

        float safeDuration = Mathf.Max(0.05f, duration);
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Append(transform.DOMove(targetPosition, safeDuration)
            .SetEase(deferPoseChangeUntilArrival ? Ease.OutCubic : Ease.OutBack));
        if (!deferPoseChangeUntilArrival)
        {
            sequence.Join(transform.DORotate(targetRotation.eulerAngles, safeDuration, RotateMode.Fast).SetEase(Ease.OutBack));
            sequence.Join(transform.DOScale(targetScale, safeDuration).SetEase(Ease.OutBack));
        }
        sequence.OnComplete(() =>
        {
            motionTween = null;
            if (deferPoseChangeUntilArrival)
            {
                transform.position = targetPosition;
                transform.rotation = targetRotation;
                transform.localScale = targetScale;
            }
            onComplete?.Invoke();
        });
        motionTween = sequence;
    }

    private void ReturnToOrigin()
    {
        SetEnableDrag(false);
        motionTween?.Kill();

        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Join(transform.DOMove(originPosition, 0.22f).SetEase(Ease.OutQuad));
        sequence.Join(transform.DORotate(originRotation.eulerAngles, 0.22f, RotateMode.Fast).SetEase(Ease.OutQuad));
        sequence.Join(transform.DOScale(originScale, 0.22f).SetEase(Ease.OutQuad));
        sequence.OnComplete(() =>
        {
            motionTween = null;
            SetSortingRaised(false);
            SetEnableDrag(true);
        });
        motionTween = sequence;
    }

    private void SetSortingRaised(bool raised)
    {
        if (renderers == null || originalSortingOrders == null)
            return;

        for (int index = 0; index < renderers.Length; index++)
        {
            if (renderers[index] != null)
            {
                renderers[index].sortingOrder = originalSortingOrders[index] +
                                                (raised ? DragSortingOffset : 0);
            }
        }
    }

    private void OnDisable()
    {
        motionTween?.Kill();
        motionTween = null;
        SetSortingRaised(false);
    }
}

using DG.Tweening;
using UnityEngine;

/// <summary>
/// Level 106 第 11 层移动钥匙物件的世界空间拖拽组件。
/// 释放到 HUD 左移键或右移键时，消费钥匙并解锁玩家选择的方向。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level106MoveKeyDrag : MonoBehaviour
{
    private Level106Ctrl owner;
    private Camera gameplayCamera;
    private SpriteRenderer spriteRenderer;
    private Collider2D keyCollider;
    private Vector3 settledPosition;
    private Vector3 dragOffset;
    private int settledSortingOrder;
    private bool settledIsTrigger;
    private bool interactionEnabled;
    private bool isDragging;
    private Tween returnTween;

    internal void Initialize(
        Level106Ctrl newOwner,
        Camera camera,
        SpriteRenderer renderer,
        Collider2D collider)
    {
        owner = newOwner;
        gameplayCamera = camera;
        spriteRenderer = renderer;
        keyCollider = collider;
        interactionEnabled = false;
        isDragging = false;
    }

    internal void EnableInteraction()
    {
        settledPosition = transform.position;
        settledSortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder : 0;
        settledIsTrigger = keyCollider != null && keyCollider.isTrigger;
        if (keyCollider != null)
        {
            keyCollider.enabled = true;
        }

        interactionEnabled = true;
    }

    private void Update()
    {
        if (!interactionEnabled || owner == null || gameplayCamera == null)
        {
            return;
        }

        if (!isDragging)
        {
            if (IsPointerDown() && keyCollider != null)
            {
                Vector2 screenPosition = GetPointerScreenPosition();
                if (keyCollider.OverlapPoint(GetPointerWorldPosition(screenPosition)))
                {
                    BeginDrag(screenPosition);
                }
            }

            return;
        }

        if (IsPointerUp())
        {
            EndDrag();
            return;
        }

        if (IsPointerHeld())
        {
            UpdateDrag(GetPointerScreenPosition());
        }
    }

    private void BeginDrag(Vector2 screenPosition)
    {
        transform.DOKill();
        isDragging = true;
        settledPosition = transform.position;
        dragOffset = transform.position - GetPointerWorldPosition(screenPosition);

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = settledSortingOrder + 100;
        }

        if (keyCollider != null)
        {
            keyCollider.isTrigger = true;
        }
    }

    private void UpdateDrag(Vector2 screenPosition)
    {
        Vector3 targetPosition = GetPointerWorldPosition(screenPosition) + dragOffset;
        targetPosition.z = transform.position.z;
        transform.position = targetPosition;
    }

    private void EndDrag()
    {
        isDragging = false;
        if (owner != null && owner.TryDropMoveKey(this, GetPointerScreenPosition()))
        {
            interactionEnabled = false;
            gameObject.SetActive(false);
            return;
        }

        RestoreAfterRejectedDrop();
    }

    private void OnDisable()
    {
        transform.DOKill();
        returnTween = null;
        isDragging = false;
    }

    internal void ApplyWorldShift(Vector3 delta)
    {
        settledPosition += delta;
        if (returnTween != null && returnTween.IsActive())
        {
            returnTween.Kill(false);
            RestoreAfterRejectedDrop();
        }
    }

    private void RestoreAfterRejectedDrop()
    {
        interactionEnabled = false;
        returnTween = transform.DOMove(settledPosition, 0.2f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                returnTween = null;
                if (spriteRenderer != null)
                {
                    spriteRenderer.sortingOrder = settledSortingOrder;
                }

                if (keyCollider != null)
                {
                    keyCollider.isTrigger = settledIsTrigger;
                }

                interactionEnabled = true;
            });
    }

    private bool IsPointerDown()
    {
        return Input.GetMouseButtonDown(0)
            || TryGetTouchPhase(out TouchPhase phase) && phase == TouchPhase.Began;
    }

    private bool IsPointerHeld()
    {
        return Input.GetMouseButton(0)
            || TryGetTouchPhase(out TouchPhase phase)
                && (phase == TouchPhase.Began
                    || phase == TouchPhase.Moved
                    || phase == TouchPhase.Stationary);
    }

    private bool IsPointerUp()
    {
        return Input.GetMouseButtonUp(0)
            || TryGetTouchPhase(out TouchPhase phase)
                && (phase == TouchPhase.Ended || phase == TouchPhase.Canceled);
    }

    private static bool TryGetTouchPhase(out TouchPhase phase)
    {
        if (Input.touchCount > 0)
        {
            phase = Input.GetTouch(0).phase;
            return true;
        }

        phase = default;
        return false;
    }

    private Vector2 GetPointerScreenPosition()
    {
        if (Input.touchCount > 0)
        {
            return Input.GetTouch(0).position;
        }

        return Input.mousePosition;
    }

    private Vector3 GetPointerWorldPosition(Vector2 screenPosition)
    {
        Vector3 worldPosition = screenPosition;
        worldPosition.z = Mathf.Abs(gameplayCamera.transform.position.z - transform.position.z);
        return gameplayCamera.ScreenToWorldPoint(worldPosition);
    }
}

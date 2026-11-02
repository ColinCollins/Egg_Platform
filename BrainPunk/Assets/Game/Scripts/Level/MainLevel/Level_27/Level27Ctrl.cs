using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.Rendering;

public class Level27Ctrl : MonoBehaviour
{
    private const int InactiveSortingOrder = 101;
    private const int ActiveSortingOrder = 201;

    private Transform currentMask;
    [SerializeField] private BoxCollider2D enemyCollider;

    [SerializeField] private Transform startDoor;

    public void SwitchMask(Transform target)
    {
        if (target == null)
            return;

        if (currentMask != null && currentMask == target)
            return;

        UpdateMaskState(currentMask, false);

        currentMask = target;
        UpdateMaskState(currentMask, true);

        if (enemyCollider != null)
            enemyCollider.enabled = currentMask != startDoor;

        AudioManager.PlaySound("button");
    }

    private void UpdateMaskState(Transform mask, bool isActive)
    {
        if (mask == null)
            return;

        SetBehaviourEnabled<FollowTarget>(mask, isActive);
        SetBehaviourEnabled<FollowTargetRotate>(mask, isActive);
        SetBehaviourEnabled<FollowTargetScale>(mask, isActive);

        if (!isActive && mask.TryGetComponent<ItemJumpOutExecutor>(out var jumpOutExecutor))
            jumpOutExecutor.Play();

        SetSorting(mask, isActive ? ActiveSortingOrder : InactiveSortingOrder);
    }

    private static void SetBehaviourEnabled<T>(Component target, bool isEnabled) where T : Behaviour
    {
        if (target != null && target.TryGetComponent<T>(out var behaviour))
            behaviour.enabled = isEnabled;
    }

    private void SetSorting(Transform mask, int sortingOrder)
    {
        if (mask == null)
            return;

        if (mask != startDoor)
        {
            if (mask.TryGetComponent<SpriteRenderer>(out var spriteRenderer))
                spriteRenderer.sortingOrder = sortingOrder;

            return;
        }

        var sortingGroup = mask.GetComponentInChildren<SortingGroup>();
        if (sortingGroup != null)
            sortingGroup.sortingOrder = sortingOrder;
    }
}

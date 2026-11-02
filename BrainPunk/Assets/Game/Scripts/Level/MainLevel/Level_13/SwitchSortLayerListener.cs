using Game.ItemEvent;
using UnityEngine;

public class SwitchSortLayerListener : BaseItemEventHandle
{
    [SerializeField] private SpriteRenderer target;

    [Tooltip("目标 Sorting Layer 名称")]
    [SerializeField] private string sortingLayerName = "Default";

    [Tooltip("目标 Sorting Order（渲染顺序）")]
    [SerializeField] private int sortingOrder = 0;

    public override void Execute()
    {
        if (target != null)
        {
            target.sortingLayerName = sortingLayerName;
            target.sortingOrder = sortingOrder;
            IsDone = true;
        }
    }
}

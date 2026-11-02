using Game.ItemEvent;
using UnityEngine;

[RequireComponent(typeof(ItemJumpOutExecutor))]
public class PlayItemJumpOutListener : BaseItemEventHandle
{
    private ItemJumpOutExecutor itemJumpOut;
    public override void Execute()
    {
        itemJumpOut.Play();
        IsDone = true;
    }

    void Start()
    {
        itemJumpOut = GetComponent<ItemJumpOutExecutor>();
    }
}

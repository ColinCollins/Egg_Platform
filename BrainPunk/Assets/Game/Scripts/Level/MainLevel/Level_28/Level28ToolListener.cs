using Game.ItemEvent;
using UnityEngine;

public class Level28ToolListener : BaseItemEventHandle
{
    public int state = 0;

    [SerializeField] private Level28ActorCtrl actorCtrl;
    public override void Execute()
    {
        Debug.Log("Trigger -----------------");
        switch (state)
        {
            case 0:
                actorCtrl.AddMoveSpeed();
                break;
            case 1:
                actorCtrl.SwitchDouleJump();
                break;
            case 2:
                actorCtrl.SwitchTripleJump();
                break;
        }

        IsDone = true;
    }
}

using Game.ItemEvent;
using UnityEngine;

public class SwitchComponentListener : BaseItemEventHandle
{
    [SerializeField] private Behaviour Target;

    [SerializeField] private bool isEnable = true;

    public override void Execute()
    {
        if (Target != null)
        {
            Target.enabled = isEnable;
            IsDone = true;
        }

    }
}

using Game.ItemEvent;
using UnityEngine;

public class SwitchGameObjectListener : BaseItemEventHandle
{
    [SerializeField] private GameObject target;

    [SerializeField] private bool isActive = true;

    public override void Execute()
    {
        if (target != null)
        {
            target.SetActive(isActive);
            IsDone = true;
        }
    }
}

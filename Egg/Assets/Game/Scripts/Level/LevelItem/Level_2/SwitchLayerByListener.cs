using UnityEngine;

namespace Game.ItemEvent
{
    public class SwitchLayerByListener : BaseItemEventHandle
    {
        [SerializeField] private GameObject target;

        public LayerMask NewLayer;

        public override void Execute()
        {
            target.layer = (int)Mathf.Log(NewLayer.value, 2);
            IsDone = true;
        }
    }

}

using Bear.EventSystem;
using UnityEngine;

namespace Game.ItemEvent
{
    public class SwitchCollision2DStateListener : BaseItemEventHandle
    {
        [SerializeField] private Collider2D target;

        public override void Execute()
        {
            if (target != null)
            {
                var collision2D = (target as PolygonCollider2D);
                collision2D.isTrigger = !collision2D.isTrigger;

                IsDone = true;
            }
        }
    }

}

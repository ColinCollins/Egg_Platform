using System.Diagnostics;
using UnityEngine;
namespace Game.ItemEvent
{
    public class Raycast2DExecutor : BaseItemExecutor
    {
        [SerializeField] private float distance;
        [SerializeField] private Vector2 direction;
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private LayerMask whatIsTarget;
        [SerializeField] private Color gizmosColor;

        private bool isCollision = false;
        private bool hasTriggeredOnCurrentCollision = false;

        protected override void OnUpdate()
        {
            base.OnUpdate();

            isCollision = IsCollision();
            // add detected
            if (isCollision)
            {
                if (!hasTriggeredOnCurrentCollision)
                    Execute();
                
                hasTriggeredOnCurrentCollision = true;
                return;
            }

            hasTriggeredOnCurrentCollision = false;
        }

        bool IsCollision()
        {
            return Physics2D.CircleCast(transform.position, radius, direction, distance, whatIsTarget);
        }

        [Conditional("UNITY_EDITOR")]
        private void OnDrawGizmos()
        {
            var pos = transform.position;
            Gizmos.color = gizmosColor;
            var endPos = new Vector2(pos.x, pos.y) + direction * distance;
            Gizmos.DrawLine(pos, endPos);
            Gizmos.DrawWireSphere(endPos, radius);
        }
    }
}

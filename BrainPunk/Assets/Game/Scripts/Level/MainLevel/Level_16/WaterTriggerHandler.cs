

using UnityEngine;

public class WaterTriggerHandler : MonoBehaviour
{
    [SerializeField] private LayerMask _waterMask;
    // [SerializeField] private GameObject _splashParticles;

    private EdgeCollider2D _edgeCollider;
    private InteractableWater _water;


    void Awake()
    {
        _water = GetComponent<InteractableWater>();
        _edgeCollider = GetComponent<EdgeCollider2D>();
    }

    /// Sent when another object enters a trigger collider attached to this
    /// object (2D physics only).
    /// </summary>
    /// <param name="other">The other Collider2D involved in this collision.</param>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if ((_waterMask.value & (1 << other.gameObject.layer)) > 0)
        {
            Rigidbody2D rb = other.GetComponentInParent<Rigidbody2D>();

             if (rb != null)
            {
               /*  Vector2 localPos = gameObject.transform.localPosition;
                Vector2 hitObjectPos = other.transform.position;
                Bounds hitObjectBounds = other.bounds; */

                float multiplier = 1;
                if (rb.linearVelocityY < 0)
                    multiplier = -1;

                float vel = rb.linearVelocityY * _water.ForceMultiplier;
                vel = Mathf.Clamp(Mathf.Abs(vel), 0, _water.MaxForce);
                vel *= multiplier;

                _water.Splash(other, vel);
            }
        }
    }
}
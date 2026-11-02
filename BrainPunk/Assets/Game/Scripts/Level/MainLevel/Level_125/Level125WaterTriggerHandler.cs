using UnityEngine;

/// <summary>
/// Level 125 专属水体触发器：复制自 Level_16 WaterTriggerHandler 并按本关命名隔离。
/// 仅负责把入水冲击转成 Level125InteractableWater 的波浪，不承载任何死亡/失败判定。
/// </summary>
public class Level125WaterTriggerHandler : MonoBehaviour
{
    [SerializeField] private LayerMask _waterMask;

    private EdgeCollider2D _edgeCollider;
    private Level125InteractableWater _water;


    void Awake()
    {
        _water = GetComponent<Level125InteractableWater>();
        _edgeCollider = GetComponent<EdgeCollider2D>();
    }

    /// <summary>
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

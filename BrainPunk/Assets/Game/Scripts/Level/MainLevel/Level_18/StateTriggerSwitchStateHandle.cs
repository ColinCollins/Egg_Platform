using UnityEngine;

/// <summary>
/// 需要配合 StateTriggerOwner 使用，修改 state
/// </summary>
public class StateTriggerSwitchStateHandle : MonoBehaviour
{
    [SerializeField] private int state;
    [SerializeField] private int newState;


    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision != null)
        {
            var owner = collision.GetComponent<StateTriggerOwner>();
            if (owner == null || owner.CurrentState != state)
                return;

            owner.SetState(newState);
            Destroy(GetComponent<Collider2D>());
        }
    }
}

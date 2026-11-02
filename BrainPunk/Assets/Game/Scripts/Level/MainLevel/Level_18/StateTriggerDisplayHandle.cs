using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// 需要配合 StateTriggerOwner 使用，检测 state，执行操作
/// </summary>
public class StateTriggerDisplayHandle : BaseItemExecutor
{
    // 展示
/*     [SerializeField] private GameObject[] displayTargets;

    [SerializeField] private GameObject[] destroyTargets; */

    [SerializeField] private int state;
    public int State => state;

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision != null)
        {
            var owner = collision.GetComponent<StateTriggerOwner>();
            if (owner == null || owner.CurrentState != state)
                return;

            /* for (int i = 0; i < displayTargets.Length; i++)
            {
                displayTargets[i].SetActive(true);
            } */

            Execute();

            /* for (int i = 0; i < destroyTargets.Length; i++)
            {
                destroyTargets[i].SetActive(false);
            } */
        }
    }
}

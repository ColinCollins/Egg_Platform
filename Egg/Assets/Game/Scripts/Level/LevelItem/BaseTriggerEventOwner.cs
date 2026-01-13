using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
namespace Game.ItemEvent
{
    public enum ExecuteMode
    {
        // 顺序执行
        Sequence,
        // 并行
        Parallel
    }

    public class BaseTriggerEventOwner : MonoBehaviour
    {
        [SerializeField] ExecuteMode Mode;

        [SerializeField] private GameObject Target;
        [SerializeField] private List<BaseItemEventHandle> items;

        private int index = 0;
        private bool isRunning = false;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Target != other.gameObject)
                return;

            if (items.Count <= 0 || isRunning)
                return;

            index = 0;
            isRunning = true;
        }

        void Update()
        {
            if (!isRunning || index >= items.Count)
            {
                isRunning = false;
                return;
            }

            // 同步执行直接过
            if (items[index].IsDone || Mode == ExecuteMode.Parallel)
            {
                index++;
            }
            else if (items[index].IsRunning)
                return;

            if (index < items.Count)
                items[index].Execute();
        }
    }
}
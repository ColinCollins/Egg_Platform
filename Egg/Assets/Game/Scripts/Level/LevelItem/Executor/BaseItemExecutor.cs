using System.Collections.Generic;
using UnityEngine;
namespace Game.ItemEvent
{
    public enum ExecuteMode
    {
        // 顺序执行
        Sequence,
        // 并行
        Parallel
    }

    /// <summary>
    /// item 基础触发类
    /// </summary>
    public class BaseItemExecutor : MonoBehaviour
    {
        [SerializeField] ExecuteMode Mode;

        [SerializeField] private List<BaseItemEventHandle> items;

        private int index = 0;
        private bool isRunning = false;
        private List<BaseItemEventHandle> runtimeItems = new List<BaseItemEventHandle>();

        public virtual void Execute()
        {
            if (items.Count <= 0 || isRunning)
                return;

            BuildRuntimeItems();
            if (runtimeItems.Count <= 0)
                return;

            index = 0;
            isRunning = true;
        }

        void Update()
        {
            OnUpdate();
        }

        protected virtual void OnUpdate()
        {
            if (!isRunning || index >= runtimeItems.Count)
            {
                isRunning = false;
                return;
            }

            // 同步执行直接过
            if (runtimeItems[index].IsDone || Mode == ExecuteMode.Parallel)
            {
                index++;
            }
            else if (runtimeItems[index].IsRunning)
                return;

            if (index < runtimeItems.Count)
                runtimeItems[index].Execute();
        }

        private void BuildRuntimeItems()
        {
            runtimeItems.Clear();
            runtimeItems = new List<BaseItemEventHandle>(items.Count);

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                item.ResetState();
                runtimeItems.Add(item);
            }
        }
    }
}
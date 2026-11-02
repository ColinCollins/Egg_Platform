using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

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
        protected bool isRunning = false;
        private List<BaseItemEventHandle> runtimeItems = new List<BaseItemEventHandle>();

        /// <summary>
        /// 用于重置 running 状态
        /// </summary>
        protected void ResetRunning()
        {
            isRunning = false;
        }

        public virtual void Execute()
        {
            if (items == null || items.Count <= 0 || isRunning)
                return;

            BuildRuntimeItems();
            if (runtimeItems.Count <= 0)
                return;

            index = 0;
            isRunning = true;
        }

        private void Update()
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

            if (Mode == ExecuteMode.Parallel)
            {
                while (index < runtimeItems.Count)
                {
                    runtimeItems[index].Execute();
                    index++;
                }

                return;
            }

            // 顺序执行时，当前项完成后推进到下一项
            if (runtimeItems[index].IsDone)
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

            if (items == null || items.Count == 0)
                return;

            runtimeItems = new List<BaseItemEventHandle>(items.Count);

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                item.ResetState();
                runtimeItems.Add(item);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 使用 Odin 按钮自动收集当前 Transform 下的所有 BaseItemEventHandle 组件到 items 列表中。
        /// 会先清理原有 items，再重新填充。仅 Editor 可用。
        /// </summary>
        [Button("Auto Collect Items")]
        private void AutoCollectItems()
        {
            var collected = GetComponentsInChildren<BaseItemEventHandle>(true);

            if (items == null)
                items = new List<BaseItemEventHandle>(collected.Length);
            else
                items.Clear();

            foreach (var item in collected)
            {
                if (item == null)
                    continue;

                items.Add(item);
            }
        }

        /// <summary>
        /// 按 items 列表顺序，修改当前物体上 BaseItemEventHandle 在 Inspector 中的组件先后顺序。仅 Editor 可用。
        /// </summary>
        [Button("Sync Component Order By List")]
        private void SyncComponentOrderByList()
        {
            if (items == null || items.Count == 0)
                return;

            var onThis = new List<BaseItemEventHandle>();
            foreach (var item in items)
            {
                if (item != null && item.gameObject == gameObject)
                    onThis.Add(item);
            }

            if (onThis.Count == 0)
                return;

            var current = new List<BaseItemEventHandle>(GetComponents<BaseItemEventHandle>());
            for (int targetIndex = 0; targetIndex < onThis.Count; targetIndex++)
            {
                var component = onThis[targetIndex];
                int currentIndex = current.IndexOf(component);
                if (currentIndex < 0 || currentIndex <= targetIndex)
                    continue;

                for (int k = 0; k < currentIndex - targetIndex; k++)
                {
                    Undo.RecordObject(component, "Sync Component Order");
                    ComponentUtility.MoveComponentUp(component);
                }

                current.Clear();
                current.AddRange(GetComponents<BaseItemEventHandle>());
            }
        }
#endif
    }
}

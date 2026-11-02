#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Editor.UIGenerator
{
    /// <summary>
    /// UIGenerator 将场景中的临时 UI 保存为 Prefab 之后、销毁根物体之前，会按 Order 升序调用所有实现类。
    /// 可在实现里对 prefabRoot（仍为场景实例）增删组件、改层级等，管线会再次执行 SaveAsPrefab 写回磁盘。
    /// </summary>
    public interface IUIGeneratorPostProcessor
    {
        /// <summary>数值越小越先执行。</summary>
        int Order { get; }

        /// <param name="prefabRoot">生成结果根物体（一般为含 Canvas 的整棵 UI）</param>
        /// <param name="prefabAssetPath">Prefab 的 Assets 相对路径</param>
        /// <param name="sourceJsonPath">本次生成使用的 JSON 路径</param>
        void OnPostProcessPrefab(GameObject prefabRoot, string prefabAssetPath, string sourceJsonPath);
    }

    /// <summary>
    /// 收集并执行 <see cref="IUIGeneratorPostProcessor"/> 实现。
    /// </summary>
    public static class UIGeneratorPostProcessPipeline
    {
        /// <summary>
        /// 对当前生成结果执行所有后处理；若任一步抛错会记录日志并继续后续处理器。
        /// </summary>
        public static void Invoke(GameObject prefabRoot, string prefabAssetPath, string sourceJsonPath)
        {
            if (prefabRoot == null)
            {
                return;
            }

            foreach (IUIGeneratorPostProcessor processor in CreateProcessorsOrdered())
            {
                try
                {
                    processor.OnPostProcessPrefab(prefabRoot, prefabAssetPath, sourceJsonPath);
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[UIGeneratorPostProcess] {processor.GetType().Name} failed: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        private static IEnumerable<IUIGeneratorPostProcessor> CreateProcessorsOrdered()
        {
            var list = new List<IUIGeneratorPostProcessor>();
            foreach (Type type in GetConcreteImplementorTypes())
            {
                try
                {
                    object instance = Activator.CreateInstance(type);
                    if (instance is IUIGeneratorPostProcessor p)
                    {
                        list.Add(p);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UIGeneratorPostProcess] Skip {type.Name}: {ex.Message}");
                }
            }

            return list.OrderBy(p => p.Order);
        }

        private static IEnumerable<Type> GetConcreteImplementorTypes()
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type.IsInterface || type.IsAbstract)
                    {
                        continue;
                    }

                    if (typeof(IUIGeneratorPostProcessor).IsAssignableFrom(type))
                    {
                        yield return type;
                    }
                }
            }
        }
    }
}
#endif

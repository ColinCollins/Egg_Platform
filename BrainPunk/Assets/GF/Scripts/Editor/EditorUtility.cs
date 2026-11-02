using System;
using System.Collections.Generic;
using System.Reflection;

namespace GF.Editor
{
    public class EditorUtility
    {
        private static readonly string[] RuntimeAssemblyNames =
        {
            "Assembly-CSharp",
        };

        private static readonly string[] RuntimeOrEditorAssemblyNames =
        {
            "Assembly-CSharp",
            "Assembly-CSharp-Editor",
        };


        /// <summary>
        /// 在运行时程序集中获取指定基类的所有子类的名称。
        /// </summary>
        /// <param name="typeBase">基类类型。</param>
        /// <returns>指定基类的所有子类的名称。</returns>
        public static Type[] GetRuntimeTypes<T>() where T : class
        {
            return GetTypes<T>(RuntimeAssemblyNames);
        }

        /// <summary>
        /// 在运行时或编辑器程序集中获取指定基类的所有子类的名称。
        /// </summary>
        /// <param name="typeBase">基类类型。</param>
        /// <returns>指定基类的所有子类的名称。</returns>
        public static Type[] GetRuntimeOrEditorTypeNames<T>() where T : class
        {
            return GetTypes<T>(RuntimeOrEditorAssemblyNames);
        }

        private static Type[] GetTypes<T>(string[] assemblyNames) where T : class
        {
            var typeBase = typeof(T);
            List<Type> typeList = new List<Type>();
            foreach (string assemblyName in assemblyNames)
            {
                Assembly assembly = null;
                try
                {
                    assembly = Assembly.Load(assemblyName);
                }
                catch
                {
                    continue;
                }

                if (assembly == null)
                {
                    continue;
                }

                System.Type[] types = assembly.GetTypes();
                foreach (System.Type type in types)
                {
                    if (type.IsClass && !type.IsAbstract && typeBase.IsAssignableFrom(type))
                    {
                        typeList.Add(type);
                    }
                }
            }

            return typeList.ToArray();
        }
    }
}
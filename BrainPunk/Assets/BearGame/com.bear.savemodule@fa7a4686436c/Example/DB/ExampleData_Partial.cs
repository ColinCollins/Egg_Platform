using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bear.SaveModule
{
    public partial class ExampleData
    {
        /// <summary>
        /// 初始化数据（设置默认值）
        /// </summary>
        public override void Init()
        {
            number = 1;
            stringArray = new List<string>();
            dic = new Dictionary<string, object>();
        }

        public int Number
        {
            get => number;
            set => number = value;
        }

        public List<string> StringArray
        {
            get => stringArray;
            set => stringArray = value;
        }

        public Dictionary<string, object> Dic
        {
            get => dic;
            set => dic = value;
        }

    }
}

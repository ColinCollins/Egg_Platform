using System;
using System.Collections.Generic;
using UnityEngine;

namespace GF
{
    /// <summary>
    /// 内置资源清单
    /// </summary>
    [CreateAssetMenu(fileName = "BuildinFileManifest", menuName = "Create BuildinFileManifest", order = 1)]
    public class BuildinFileManifest : ScriptableObject
    {
        [Serializable]
        public class Element
        {
            public string PackageName;
            public string FileName;
            public string FileCRC32;
        }

        public List<Element> BuildinFiles = new List<Element>();
    }
}
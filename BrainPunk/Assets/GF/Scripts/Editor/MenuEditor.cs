using UnityEditor;
using UnityEngine;

namespace GF.Editor
{
    public class MenuEditor
    {
        //打开持久化目录
        [MenuItem("GF/Open PersistentPath", false)]
        public static void OpenPersistentPath()
        {
            UnityEditor.EditorUtility.OpenWithDefaultApp(Application.persistentDataPath);
        }
        
        //清除app的PlayerPrefs
        [MenuItem("GF/Clear PlayerPrefs", false)]
        public static void ClearPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
        }

        //清除Editor的EditorPrefs
        [MenuItem("GF/Clear EditorPrefs", false)]
        public static void ClearEditorPlayerPrefs()
        {
            EditorPrefs.DeleteAll();
        }
    }
}
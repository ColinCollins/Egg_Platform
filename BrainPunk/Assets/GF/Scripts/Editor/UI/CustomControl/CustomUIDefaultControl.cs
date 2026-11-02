using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GF.Editor
{
    public static class CustomUIDefaultControl
    {
        [MenuItem("GameObject/UI/LoopList/LoopList")]
        public static void CreateLoopListView(MenuCommand menuCommand)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/GF/Scripts/Editor/UI/CustomControl/ControlPrefabs/LoopList.prefab");
            GameObject go = Object.Instantiate(prefab);
            go.name = "LoopList";
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            go.transform.localPosition = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
        }
        
        [MenuItem("GameObject/UI/LoopList/PageList")]
        public static void CreateLoopPageView(MenuCommand menuCommand)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/GF/Scripts/Editor/UI/CustomControl/ControlPrefabs/PageList.prefab");
            GameObject go = Object.Instantiate(prefab);
            go.name = "PageList";
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            go.transform.localPosition = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
        }
        
        [MenuItem("GameObject/UI/LoopList/GridList")]
        public static void CreateLoopGridList(MenuCommand menuCommand)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/GF/Scripts/Editor/UI/CustomControl/ControlPrefabs/GridList.prefab");
            GameObject go = Object.Instantiate(prefab);
            go.name = "GridView";
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            go.transform.localPosition = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
        }
    }
}
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MelenitasDev.SoundsGood.Domain;

namespace MelenitasDev.SoundsGood.Editor
{
    public static class QuickPrefabMenu
    {
        [MenuItem("GameObject/Sounds Good/Music Zone", false, 10)]
        private static void CreateMusicZone (MenuCommand cmd) =>
            InstantiatePrefab(AssetLocator.Instance.MusicZonePrefab, "Music Zone", cmd.context);
        
        [MenuItem("GameObject/Sounds Good/UI/Output Volume Slider", false, 11)]
        private static void CreateOutputSlider (MenuCommand cmd) =>
            InstantiatePrefab(AssetLocator.Instance.OutputVolumeSliderPrefab, "Output Volume Slider", cmd.context);

        [MenuItem("GameObject/Sounds Good/UI/Generic Slider", false, 12)]
        private static void CreateGenericSlider (MenuCommand cmd) =>
            InstantiatePrefab(AssetLocator.Instance.GenericSliderPrefab, "Generic Slider", cmd.context);

        private static void InstantiatePrefab (GameObject prefab, string prettyName, Object context)
        {
            if (prefab == null)
            {
                Debug.LogError($"[Sounds Good] The prefab reference for '{prettyName}' " +
                               $"is not set in AssetLocator.");
                return;
            }

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, $"Create {prettyName}");
            
            if (context is GameObject parent)
                go.transform.SetParent(parent.transform, false);

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
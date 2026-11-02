using System.IO;
using MelenitasDev.SoundsGood.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MelenitasDev.SoundsGood.Editor
{
    public static class OpenDemoSceneMenuItem
    {
        [MenuItem("Tools/Melenitas Dev/Sounds Good/Open Demo Scene", priority = 100)]
        public static void OpenDemo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            
            var demoAsset = AssetLocator.Instance.DemoScene;
            if (demoAsset == null)
            {
                EditorUtility.DisplayDialog(
                    "Sounds Good",
                    "Demo scene is not imported.",
                    "OK"
                );
                return;
            }
            
            string scenePath = AssetDatabase.GetAssetPath(demoAsset);
            if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
            {
                EditorUtility.DisplayDialog(
                    "Sounds Good",
                    $"Demo scene asset exists but could not find it at path:\n{scenePath}\n",
                    "OK"
                );
                return;
            }
            
            EditorSceneManager.OpenScene(scenePath);
        }
    }
}
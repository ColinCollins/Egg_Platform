using UnityEditor;
using UnityEngine;

namespace BrainPunk.Editor.BuildPipeline
{
    public static class BPAssetBuildMenu
    {
        private const string CONFIG_PATH = "Assets/Resources/BPAssetBuildConfig.asset";

        private static BPAssetBuildConfig GetOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<BPAssetBuildConfig>(CONFIG_PATH);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BPAssetBuildConfig>();
                AssetDatabase.CreateAsset(config, CONFIG_PATH);
                AssetDatabase.SaveAssets();
                Debug.Log($"[BPAssetBuildMenu] Created default config at: {CONFIG_PATH}");
            }
            return config;
        }

        [MenuItem("BrainPunk/Build/Build AssetBundles (Current Config)", false, 0)]
        private static void MenuBuildAssetBundles()
        {
            var config = GetOrCreateConfig();
            BPAssetBuilder.Build(EditorUserBuildSettings.activeBuildTarget, config, forceCleanCache: false);
        }

        [MenuItem("BrainPunk/Build/Build AssetBundles (Force Clean)", false, 1)]
        private static void MenuBuildAssetBundlesForceClean()
        {
            var config = GetOrCreateConfig();
            bool confirm = EditorUtility.DisplayDialog("Force Clean Build",
                "This will clear SBP Build Cache before building.\nEquivalent to 'ForceRebuild' in BBP.",
                "Proceed", "Cancel");
            if (confirm)
            {
                BPAssetBuilder.Build(EditorUserBuildSettings.activeBuildTarget, config, forceCleanCache: true);
            }
        }

        [MenuItem("BrainPunk/Build/Toggle Pipeline (SBP ↔ BBP)", false, 50)]
        private static void MenuTogglePipeline()
        {
            var config = GetOrCreateConfig();
            config.UseScriptableBuildPipeline = !config.UseScriptableBuildPipeline;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            Debug.Log($"[BPAssetBuildMenu] Switched to: {(config.UseScriptableBuildPipeline ? "SBP" : "BBP")}");
        }

        [MenuItem("BrainPunk/Build/Open Config Asset", false, 51)]
        private static void MenuOpenConfig()
        {
            var config = GetOrCreateConfig();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }
    }
}

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Restores Unity's editor-only Scene Visibility and picking state once after
/// this asset is imported. It never changes prefab content or runtime state.
/// </summary>
[InitializeOnLoad]
internal static class ChestGiftSceneVisibilityRecovery
{
    private const string RecoverySessionKey = "ChestGift.SceneVisibilityRecovery.20260907";

    static ChestGiftSceneVisibilityRecovery()
    {
        if (!SessionState.GetBool(RecoverySessionKey, false))
            EditorApplication.delayCall += RestoreAll;
    }

    [MenuItem("Tools/BrainPunk/Chest Gift/Restore Scene Visibility")]
    private static void RestoreFromMenu()
    {
        RestoreAll();
    }

    private static void RestoreAll()
    {
        SceneVisibilityManager manager = SceneVisibilityManager.instance;
        manager.ShowAll();
        manager.EnableAllPicking();

        SessionState.SetBool(RecoverySessionKey, true);
        EditorApplication.RepaintHierarchyWindow();
        SceneView.RepaintAll();
        UnityEngine.Debug.Log("[ChestGift] Scene Visibility and Scene Picking have been restored.");
    }
}
#endif

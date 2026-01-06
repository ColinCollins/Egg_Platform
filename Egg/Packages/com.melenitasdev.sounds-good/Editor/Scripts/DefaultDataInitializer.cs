using UnityEditor;
using UnityEngine;
using System.IO;
using MelenitasDev.SoundsGood.Domain;

namespace MelenitasDev.SoundsGood.Editor
{
    static class DefaultDataInitializer
    {
        private const string USER_DATA_PATH = "Assets/SoundsGood/Data/";
        private const string DEFAULT_DATA_PATH = "Packages/com.melenitasdev.sounds-good/Runtime/Data/Default/";
        private static bool subscribedToUpdate;
        private static bool enumsCreated;
        private static bool enumsInitialized;

        [InitializeOnLoadMethod]
        private static void OnInitialize ()
        {
            if (subscribedToUpdate) return;
            
            EditorApplication.update += OnEditorUpdate;
            subscribedToUpdate = true;
        }

        private static void OnEditorUpdate ()
        {
            CopyAllDefaultsToUserData();

            if (enumsCreated && !enumsInitialized)
            {
                WriteEnumsContent();
            }
        }
        
        private static void CopyAllDefaultsToUserData ()
        {
            bool refreshAssets = false;
            
            EnsureFolderExists("Assets", "SoundsGood");
            EnsureFolderExists("Assets/SoundsGood", "Data");
            
            // ===== Collections
            if (!AssetDatabase.IsValidFolder(USER_DATA_PATH + "Collections"))
            {
                EnsureFolderExists("Assets/SoundsGood/Data", "Collections");
                CopyAssetIfNotExists(DEFAULT_DATA_PATH + "Collections/DefaultSoundCollection.asset",
                    USER_DATA_PATH + "Collections/SoundCollection.asset");
                CopyAssetIfNotExists(DEFAULT_DATA_PATH + "Collections/DefaultMusicCollection.asset",
                    USER_DATA_PATH + "Collections/MusicCollection.asset");
                CopyAssetIfNotExists(DEFAULT_DATA_PATH + "Collections/DefaultOutputCollection.asset",
                    USER_DATA_PATH + "Collections/OutputCollection.asset");
                refreshAssets = true;
            }
            
            // ===== Mixers
            if (!AssetDatabase.IsValidFolder(USER_DATA_PATH + "Mixers"))
            {
                EnsureFolderExists("Assets/SoundsGood/Data", "Mixers");
                CopyAssetIfNotExists(DEFAULT_DATA_PATH + "Mixers/DefaultMaster.mixer",
                    USER_DATA_PATH + "Mixers/Master.mixer");
                refreshAssets = true;
            }

            // ===== Generated
            if (!AssetDatabase.IsValidFolder(USER_DATA_PATH + "Generated"))
            {
                EnsureFolderExists("Assets/SoundsGood/Data", "Generated");
                File.WriteAllText(USER_DATA_PATH + "Generated/SFX_Generated.cs", "");
                File.WriteAllText(USER_DATA_PATH + "Generated/Track_Generated.cs", "");
                File.WriteAllText(USER_DATA_PATH + "Generated/Output_Generated.cs", "");
                CopyAssetIfNotExists(DEFAULT_DATA_PATH + "Generated/SoundsGood.Application.asmref",
                    USER_DATA_PATH + "Generated/SoundsGood.Application.asmref");
                enumsInitialized = false;
                enumsCreated = true;
                refreshAssets = true;
            }

            if (!refreshAssets) return;
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            string result = $"[Sounds Good] Assets/SoundsGood/Data created with:\n" +
                            $"{AssetLocator.Instance.SoundDataCollection}, {AssetLocator.Instance.MusicDataCollection}, " +
                            $"{AssetLocator.Instance.OutputDataCollection}, {AssetLocator.Instance.SfxEnum}, " +
                            $"{AssetLocator.Instance.TracksEnum}, {AssetLocator.Instance.OutputsEnum}, " +
                            $"{AssetLocator.Instance.MasterAudioMixer}";
            Debug.Log(result);
        }

        private static void CopyAssetIfNotExists (string sourcePath, string destPath)
        {
            if (File.Exists(Path.Combine(Application.dataPath, "../", destPath))) return;

            if (AssetDatabase.CopyAsset(sourcePath, destPath))
                Debug.Log($"[SoundsGood] Copied: {destPath}");
            else
                Debug.LogError($"[SoundsGood] Failed to copy {sourcePath} to {destPath}");
        }

        private static void EnsureFolderExists (string parent, string newFolderName)
        {
            string fullPath = parent + "/" + newFolderName;
            if (!AssetDatabase.IsValidFolder(fullPath)) AssetDatabase.CreateFolder(parent, newFolderName);
        }

        private static void WriteEnumsContent ()
        {
            if (AssetLocator.Instance == null || 
                AssetLocator.Instance.SoundDataCollection == null ||
                AssetLocator.Instance.MusicDataCollection == null || 
                AssetLocator.Instance.OutputDataCollection == null || 
                AssetLocator.Instance.SfxEnum == null || 
                AssetLocator.Instance.TracksEnum == null || 
                AssetLocator.Instance.OutputsEnum == null)
            {
                return;
            }
            
            EditorHelper.SaveCollectionChanges(Sections.Sounds, false);
            EditorHelper.SaveCollectionChanges(Sections.Music, false);
            EditorHelper.ReloadOutputsDatabase(false);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            enumsInitialized = true;
            EditorApplication.update -= WriteEnumsContent;
        }
    }
}
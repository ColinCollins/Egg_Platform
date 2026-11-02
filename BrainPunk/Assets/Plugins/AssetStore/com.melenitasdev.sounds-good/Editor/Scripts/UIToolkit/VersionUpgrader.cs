using System.IO;
using MelenitasDev.SoundsGood.Domain;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UIElements;

namespace MelenitasDev.SoundsGood.Editor
{
    public class VersionUpgrader : EditorWindow
    {
        // ----- Serialized Fields
        [SerializeField] private VisualTreeAsset tree;
        
        // ----- UI Elements
        private Button upgradeButton;
        private Button removeOldDatabaseButton;
        
        void CreateGUI ()
        {
            tree.CloneTree(rootVisualElement);
            
            upgradeButton = rootVisualElement.Q<Button>("UpgradeButton");
            removeOldDatabaseButton = rootVisualElement.Q<Button>("RemoveOldDatabaseButton");
            
            bool usingNewDatabase = Resources.Load<AudioMixer>("Melenitas Dev/Sounds Good/Outputs/Master") == null;
            bool oldDatabaseExists = Directory.Exists("Assets/Resources/Melenitas Dev/Sounds Good");
            if (usingNewDatabase) DisableUpgradeButton();
            SetActiveRemoveOldDatabaseButton(usingNewDatabase && oldDatabaseExists);
           
            RegisterEvents();
        }
        
        // ----- Public Methods
        [MenuItem("Tools/Melenitas Dev/Sounds Good/Version Upgrader (1.0 → 2.0)", false, 150)]
        public static void ShowWindow ()
        {
            var window = GetWindow(typeof(VersionUpgrader));
            window.titleContent = new GUIContent("Version Upgrader");
        }
        void OnDisable ()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
        
        // ----- Private Methods
        private void RegisterEvents ()
        {
            // Buttons
            upgradeButton.clicked += Upgrade;
            removeOldDatabaseButton.clicked += RemoveOldDatabase;
            
            // Play Mode
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged (PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) rootVisualElement.SetEnabled(false);
            else if (state == PlayModeStateChange.ExitingPlayMode) rootVisualElement.SetEnabled(true);
        }
        
        private void Upgrade ()
        {
            string resourcesPath = "Melenitas Dev/Sounds Good/";
            var soundDataCollection = Resources.Load<SoundDataCollection>(resourcesPath + "SoundCollection");
            var musicDataCollection = Resources.Load<MusicDataCollection>(resourcesPath + "MusicCollection");
            var masterMixer = Resources.Load<AudioMixer>(resourcesPath + "Outputs/Master");

            if (soundDataCollection != null)
            {
                foreach (SoundData soundData in soundDataCollection.Sounds)
                {
                    AssetLocator.Instance.SoundDataCollection.CreateSound(soundData.Clips, soundData.Tag,
                        soundData.CompressionPreset, soundData.ForceToMono, out string result);
                }
            }

            if (musicDataCollection != null)
            {
                foreach (SoundData soundData in musicDataCollection.MusicTracks)
                {
                    AssetLocator.Instance.MusicDataCollection.CreateMusicTrack(soundData.Clips, soundData.Tag, 
                        soundData.CompressionPreset, soundData.ForceToMono, out string result);
                }
            }
            
            EditorHelper.SaveCollectionChanges(Sections.Sounds, false);
            EditorHelper.SaveCollectionChanges(Sections.Music, false);

            if (masterMixer != null)
            {
                string currentMixerPath = "Assets/Resources/Melenitas Dev/Sounds Good/Outputs/Master.mixer";
                string newMixerPath = "Assets/SoundsGood/Data/Mixers/Master.mixer";
                AssetDatabase.CopyAsset(currentMixerPath, newMixerPath);
                AssetDatabase.RenameAsset(currentMixerPath, "old_Master");
            }
            
            EditorHelper.ReloadOutputsDatabase();
            
            Debug.Log("Every database has been upgraded to SOUNDS GOOD 2.0! :D");
            
            DisableUpgradeButton();
            SetActiveRemoveOldDatabaseButton(true);
        }

        private void RemoveOldDatabase ()
        {
            EditorHelper.DeleteDirectoryAndContent("Assets/Resources/Melenitas Dev/Sounds Good");
            SetActiveRemoveOldDatabaseButton(false);
        }

        private void DisableUpgradeButton ()
        {
            upgradeButton.RemoveFromClassList("light-orange-button");
            upgradeButton.SetEnabled(false);
            upgradeButton.text = "You're now using the 2.0 system";
        }

        private void SetActiveRemoveOldDatabaseButton (bool active)
        {
            removeOldDatabaseButton.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

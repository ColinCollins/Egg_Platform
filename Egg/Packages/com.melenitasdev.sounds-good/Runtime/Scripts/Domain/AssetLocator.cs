using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UIElements;

[assembly: InternalsVisibleTo("SoundsGood.Domain")]
[assembly: InternalsVisibleTo("SoundsGood.Editor")]
[assembly: InternalsVisibleTo("SoundsGood.Application")]

namespace MelenitasDev.SoundsGood.Domain
{
    internal class AssetLocator : ScriptableObject
    {
        [Header("Data")]
        [SerializeField] private SoundDataCollection soundDataCollection;
        [SerializeField] private MusicDataCollection musicDataCollection;
        [SerializeField] private OutputDataCollection outputDataCollection;
        [SerializeField] private AudioMixer masterAudioMixer;
        
        [Header("Enumerators")]
        [SerializeField] private TextAsset sfxEnum;
        [SerializeField] private TextAsset tracksEnum;
        [SerializeField] private TextAsset outputsEnum;
        
        [Header("VisualTreeAssets")]
        [SerializeField] private VisualTreeAsset audioClipTemplate;
        [SerializeField] private VisualTreeAsset audioTemplate;
        [SerializeField] private VisualTreeAsset outputTemplate;

        [Header("Textures")] 
        [SerializeField] private Texture2D createGroupImage;
        [SerializeField] private Texture2D renameGroupImage;
        [SerializeField] private Texture2D exposeVolumeImage;
        [SerializeField] private Texture2D locateExposedParamImage;
        [SerializeField] private Texture2D renameExposedParamImage;
        
#if UNITY_EDITOR
        [Header("Scenes")]
        [SerializeField] private SceneAsset demoScene;
#endif
        
        [Header("Prefabs")]
        [SerializeField] private GameObject outputVolumeSliderPrefab;
        [SerializeField] private GameObject genericSliderPrefab;
        [SerializeField] private GameObject musicZonePrefab;
        
        [Header("URLs")]
        [SerializeField] private string englishDocumentationUrl;
        [SerializeField] private string spanishDocumentationUrl;

        private static AssetLocator instance;
        internal static AssetLocator Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<AssetLocator>("AssetLocator");
                }
        
                return instance;
            }
        }
        
        internal SoundDataCollection SoundDataCollection
        {
            get
            {
#if UNITY_EDITOR
                if (soundDataCollection == null)
                    soundDataCollection = GetFromUserData<SoundDataCollection>("SoundCollection");
#endif
                return soundDataCollection;
            }
        }

        internal MusicDataCollection MusicDataCollection
        {
            get
            {
#if UNITY_EDITOR
                if (musicDataCollection == null)
                    musicDataCollection = GetFromUserData<MusicDataCollection>("MusicCollection");
#endif
                return musicDataCollection;
            }
        }
        
        internal OutputDataCollection OutputDataCollection
        {
            get
            {
#if UNITY_EDITOR
                if (outputDataCollection == null)
                    outputDataCollection = GetFromUserData<OutputDataCollection>("OutputCollection");
#endif
                return outputDataCollection;
            }
        }
        
        internal AudioMixer MasterAudioMixer        
        {
            get
            {
#if UNITY_EDITOR
                if (masterAudioMixer == null)
                    masterAudioMixer = GetFromUserData<AudioMixer>("Master");
#endif
                return masterAudioMixer;
            }
        }
        
        internal TextAsset SfxEnum
        {
            get
            {
#if UNITY_EDITOR
                if (sfxEnum == null)
                    sfxEnum = GetFromUserData<TextAsset>("SFX_Generated");
#endif
                return sfxEnum;
            }
        }
        
        internal TextAsset TracksEnum
        {
            get
            {
#if UNITY_EDITOR
                if (tracksEnum == null)
                    tracksEnum = GetFromUserData<TextAsset>("Track_Generated");
#endif
                return tracksEnum;
            }
        }
        
        internal TextAsset OutputsEnum
        {
            get
            {
#if UNITY_EDITOR
                if (outputsEnum == null)
                    outputsEnum = GetFromUserData<TextAsset>("Output_Generated");
#endif
                return outputsEnum;
            }
        }
        
        internal VisualTreeAsset AudioClipTemplate => audioClipTemplate;
        internal VisualTreeAsset AudioTemplate => audioTemplate;
        internal VisualTreeAsset OutputTemplate => outputTemplate;
        internal Texture2D CreateGroupImage => createGroupImage;
        internal Texture2D RenameGroupImage => renameGroupImage;
        internal Texture2D ExposeVolumeImage => exposeVolumeImage;
        internal Texture2D LocateExposedParamImage => locateExposedParamImage;
        internal Texture2D RenameExposedParamImage => renameExposedParamImage;
#if UNITY_EDITOR
        internal SceneAsset DemoScene => demoScene;
#endif
        internal GameObject OutputVolumeSliderPrefab => outputVolumeSliderPrefab;
        internal GameObject GenericSliderPrefab => genericSliderPrefab;
        internal GameObject MusicZonePrefab => musicZonePrefab;
        internal string EnglishDocumentationUrl => englishDocumentationUrl;
        internal string SpanishDocumentationUrl => spanishDocumentationUrl;

#if UNITY_EDITOR
        private static T GetFromUserData<T> (string filenameWithoutExtension) where T : Object
        {
            // 1) Search only inside Assets/SoundsGood/Data
            var searchFolders = new[] { "Assets/SoundsGood/Data" };

            // 2) Build a filter to look for filenameWithoutExtension and type T
            string filter = $"{filenameWithoutExtension} t:{typeof(T).Name}";

            // 3) Query AssetDatabase for matching GUIDs
            string[] guids = AssetDatabase.FindAssets(filter, searchFolders);
            if (guids == null || guids.Length <= 0)
            {
                Debug.LogError($"[SoundsGood] Asset of type {typeof(T).Name} named '{filenameWithoutExtension}' not found in Assets/SoundsGood/Data/.");
                return null;
            }
            
            // 4) Take the first match, get its path, and load it
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null) Debug.LogError($"[SoundsGood] Found asset path '{assetPath}', " +
                                              $"but failed to load as type {typeof(T).Name}.");
            return asset;
        }
#endif
    }
}

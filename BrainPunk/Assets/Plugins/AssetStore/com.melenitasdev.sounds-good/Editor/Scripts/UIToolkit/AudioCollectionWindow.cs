using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using MelenitasDev.SoundsGood.Domain;

namespace MelenitasDev.SoundsGood.Editor
{
    public class AudioCollectionWindow : EditorWindow
    {
        [SerializeField] private VisualTreeAsset tree;
        
        private Button soundsButton;
        private Button musicButton;
        private TextField searchTagField;
        private ObjectField searchClipField;
        private Button clearTagButton;
        private Button clearClipButton;
        private ListView audioList;
        private VisualElement audioListContainer;
        
        private Sections currentSection = Sections.Sounds;
        private bool isSoundsSection => currentSection == Sections.Sounds;
        private string searchTag = "";
        private AudioClip searchClip;
        private bool removeMode;
        private bool removedContent;
        
        [MenuItem("Tools/Melenitas Dev/Sounds Good/Audio Collection", false, 51)]
        public static void ShowWindow ()
        {
            var window = GetWindow(typeof(AudioCollectionWindow));
            window.titleContent = new GUIContent("Audio Collection");
        }

        void CreateGUI ()
        {
            tree.CloneTree(rootVisualElement);
            soundsButton = rootVisualElement.Q<Button>("Sounds");
            musicButton = rootVisualElement.Q<Button>("Music");
            searchTagField = rootVisualElement.Q<TextField>("SearchTagField");
            audioList = rootVisualElement.Q<ListView>("AudioList");
            audioListContainer = audioList.Q<VisualElement>("unity-content-container");
            searchClipField = new ObjectField { objectType = typeof(AudioClip), 
                style = { flexShrink = 1, marginRight = 5, marginTop = 4, width = new StyleLength(Length.Percent(100)) }};
            rootVisualElement.Q<VisualElement>("SearchClip").Add(searchClipField);
            
            CreateClearSearchButtons();
            
            RegisterEvents();
            
            ChangeTab(Sections.Sounds);
        }
        
        void OnDisable ()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
        
        private void RegisterEvents ()
        {
            // Buttons
            soundsButton.clicked += () => ChangeTab(Sections.Sounds);
            musicButton.clicked += () => ChangeTab(Sections.Music);
            
            // Text Fields
            searchTagField.RegisterValueChangedCallback(evt => OnSearchTag(evt.newValue));
            searchClipField.RegisterValueChangedCallback(evt => OnSearchClip((AudioClip)evt.newValue));
            
            // User Inputs
            rootVisualElement.RegisterCallback<MouseOverEvent>(evt =>
            {
                rootVisualElement.focusable = true;
                rootVisualElement.Focus();
            });
            rootVisualElement.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.altKey && !removeMode) SetActiveRemoveMode(true);
            });
            rootVisualElement.RegisterCallback<KeyUpEvent>(evt =>
            {
                if (!evt.altKey && removeMode) SetActiveRemoveMode(false);
            });
            
            // Play Mode
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged (PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) rootVisualElement.SetEnabled(false);
            else if (state == PlayModeStateChange.ExitingPlayMode) rootVisualElement.SetEnabled(true);
        }
        
        private void CreateClearSearchButtons ()
        {
            var clearTagButton = new Button(ClearTagField) 
            {
                style = 
                {
                    unityBackgroundImageTintColor = new StyleColor(new Color(1, 0.7f, 0.4f, 1)),
                    height = 18,
                    marginLeft = -3,
                    display = DisplayStyle.None
                },
                text = "x"
            };
            clearTagButton.AddToClassList("button");
            rootVisualElement.Q<VisualElement>("SearchTag").hierarchy.Add(clearTagButton);
            this.clearTagButton = clearTagButton;
            clearTagButton.style.display = DisplayStyle.None;
            
            var clearClipButton = new Button(ClearClipField)
            {
                style = 
                {
                    unityBackgroundImageTintColor = new StyleColor(new Color(1, 0.7f, 0.4f, 1)),
                    height = 18,
                    marginLeft = -3,
                    display = DisplayStyle.None
                },
                text = "x"
            };
            clearClipButton.AddToClassList("button");
            rootVisualElement.Q<VisualElement>("SearchClip").hierarchy.Add(clearClipButton);
            this.clearClipButton = clearClipButton;
        }
        
        private void ChangeTab (Sections section)
        {
            currentSection = section;
            soundsButton.style.unityBackgroundImageTintColor = 
                new StyleColor(isSoundsSection ? EditorHelper.ORANGE_COLOR : EditorHelper.GREY_COLOR);
            musicButton.style.unityBackgroundImageTintColor = 
                new StyleColor(isSoundsSection ? EditorHelper.GREY_COLOR : EditorHelper.ORANGE_COLOR);
            
            Search();
        }

        private void OnSearchTag (string tag)
        {
            searchTag = tag;
            Search();
            
            clearTagButton.style.display = string.IsNullOrWhiteSpace(tag) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnSearchClip (AudioClip clip)
        {
            searchClip = clip;
            Search();
            
            clearClipButton.style.display = clip == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void Search ()
        {
            bool searchByTag = !string.IsNullOrWhiteSpace(searchTag);
            bool searchByClip = searchClip != null;
            
            if (!searchByTag && !searchByClip)
            {
                DrawAllAudios();
                return;
            }
            
            SoundData[] audios = isSoundsSection
                ? AssetLocator.Instance.SoundDataCollection.Sounds
                : AssetLocator.Instance.MusicDataCollection.MusicTracks;
            List<SoundData> searchResults = new List<SoundData>();
            foreach (var soundData in audios)
            {
                if (searchByTag && searchByClip)
                {
                    if (soundData.Tag.ToLower().StartsWith(searchTag.ToLower()) &&
                        soundData.Clips.Any(clip => clip == searchClip))
                    {
                        searchResults.Add(soundData);
                    }
                    continue;
                }

                if (searchByTag)
                {
                    if (soundData.Tag.ToLower().StartsWith(searchTag.ToLower()))
                    {
                        searchResults.Add(soundData);
                    }
                    continue;
                }

                if (soundData.Clips.Any(clip => clip == searchClip))
                {
                    searchResults.Add(soundData);
                }
            }

            DrawSearchResults(searchResults.ToArray());
        }

        private void DrawAllAudios ()
        {
            audioListContainer.Clear();
            
            SoundData[] audios = isSoundsSection
                ? AssetLocator.Instance.SoundDataCollection.Sounds
                : AssetLocator.Instance.MusicDataCollection.MusicTracks;
            foreach (var audio in audios)
            {
                var audioTemplate = new AudioTemplate(audio, currentSection, OnApplyChanges, OnRemove);
                audioListContainer.Add(audioTemplate);
            }
            SetActiveRemoveMode(removeMode);
        }

        private void DrawSearchResults (SoundData[] searchAudios)
        {
            audioListContainer.Clear();
            
            if (searchAudios.Length == 0)
            {
                audioListContainer.Add(new HelpBox("", HelpBoxMessageType.Info)
                {
                    text = " No results found :( ",
                    style =
                    {
                        paddingBottom = 10,
                        paddingTop = 10,
                        paddingLeft = 10,
                        paddingRight = 10,
                        alignSelf = new StyleEnum<Align>(Align.Stretch),
                        justifyContent = new StyleEnum<Justify>(Justify.Center),
                        marginTop = 10,
                    }
                });
            }
            
            foreach (var audio in searchAudios)
            {
                var audioTemplate = new AudioTemplate(audio, currentSection, OnApplyChanges, OnRemove);
                audioListContainer.Add(audioTemplate);
            }
            SetActiveRemoveMode(removeMode);
        }

        private void SetActiveRemoveMode (bool active)
        {
            removeMode = active;
            foreach (AudioTemplate audioTemplate in audioListContainer.Children())
            {
                audioTemplate.SetActiveRemoveMode(active);
            }

            if (!active && removedContent)
            {
                EditorHelper.SaveCollectionChanges(currentSection);
                removedContent = false;
            }
        }

        private void OnRemove ()
        {
            removedContent = true;
        }
        
        private void OnApplyChanges ()
        {
            ClearTagField();
            ClearClipField();
            
            Search();
        }

        private void ClearTagField ()
        {
            searchTagField.value = "";
        }
        
        private void ClearClipField ()
        {
            searchClipField.value = null;
        }
    }
}
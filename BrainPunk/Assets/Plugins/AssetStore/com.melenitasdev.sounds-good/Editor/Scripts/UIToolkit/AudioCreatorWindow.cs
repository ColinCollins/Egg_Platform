using System.Collections.Generic;
using System.Linq;
using MelenitasDev.SoundsGood.Domain;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MelenitasDev.SoundsGood.Editor
{
    public class AudioCreatorWindow : EditorWindow
    {
        // ----- Serialized Fields
        [SerializeField] private VisualTreeAsset tree;
        
        // ----- UI Elements
        private Button soundsButton;
        private Button musicButton;
        private TextField tagTextField;
        private VisualElement dropZone;
        private ScrollView clipsList;
        private VisualElement clipsListContent;
        private EnumField compressionPresetEnumField;
        private Label compressionPresetInfoLabel;
        private Toggle forceToMonoToggle;
        private Button createButton;
        private VisualElement resultMessageContainerLabel;
        private Label resultMessageLabel;
        private Button clearMessageButton;

        // ----- Fields
        private Sections currentSection = Sections.Sounds;
        private List<AudioClip> importedAudioClips = new List<AudioClip>();
        private string currentTag = "";
        private bool waitingForPickerResult = false;
        private CompressionPreset currentCompressionPreset = CompressionPreset.FrequentSound;
        
        // ----- Constants Fields
        private const int AUDIOCLIP_PICKER_ID = 1234;
        private const string FREQUENT_SOUND_INFO = "Sound that is generally short, not very heavy " +
                                                   "and will be played many times (shot, steps, UI...).";
        private const string OCCASIONAL_SOUND_INFO = "A sound that is generally short, not very heavy, " +
                                                     "and will not be played very frequently.";
        private const string AMBIENT_MUSIC_INFO = "Music that is generally long and heavy " +
                                                  "that will be played for a long time.";

        [MenuItem("Tools/Melenitas Dev/Sounds Good/Audio Creator", false, 50)]
        public static void ShowWindow ()
        {
            var window = GetWindow(typeof(AudioCreatorWindow));
            window.titleContent = new GUIContent("Audio Creator");
        }

        // ----- Unity Events
        void CreateGUI ()
        {
            tree.CloneTree(rootVisualElement);
            soundsButton = rootVisualElement.Q<Button>("Sounds");
            musicButton = rootVisualElement.Q<Button>("Music");
            tagTextField = rootVisualElement.Q<TextField>("TagField");
            dropZone = rootVisualElement.Q<VisualElement>("DragAndDrop").ElementAt(0);
            clipsList = rootVisualElement.Q<ScrollView>("ClipsList");
            clipsListContent = clipsList.Q<VisualElement>("unity-content-container");
            compressionPresetEnumField = rootVisualElement.Q<EnumField>("CompressionPreset");
            compressionPresetInfoLabel = rootVisualElement.Q<Label>("PresetInfoLabel");
            forceToMonoToggle = rootVisualElement.Q<Toggle>("ForceToMono");
            createButton = rootVisualElement.Q<Button>("CreateButton");
            resultMessageContainerLabel = rootVisualElement.Q<VisualElement>("ResultMessageLabel");
            resultMessageLabel = rootVisualElement.Q<Label>("ResultMessage");
            clearMessageButton = rootVisualElement.Q<Button>("ClearMessageButton");
            rootVisualElement.Q<ScrollView>("CreatorScroll").Q<VisualElement>("unity-content-container")
                .Add(rootVisualElement.Q<VisualElement>("CreatorContent"));
            
            clipsList.style.display = DisplayStyle.None;
            importedAudioClips.Clear();
            resultMessageContainerLabel.visible = false;
            
            RegisterEvents();

            ChangeTab(Sections.Sounds);
            ChangeCompressionPreset((CompressionPreset)compressionPresetEnumField.value);
            TryActiveCreateButton();
        }
        
        void OnGUI ()
        {
            if (!waitingForPickerResult || Event.current.commandName != "ObjectSelectorSelectionDone" ||
                EditorGUIUtility.GetObjectPickerControlID() != AUDIOCLIP_PICKER_ID) return;
            
            waitingForPickerResult = false;
            AudioClip selectedClip = (AudioClip)EditorGUIUtility.GetObjectPickerObject();
                
            if (selectedClip == null) return;
                
            AddAudioClip(selectedClip);
            Repaint();
        }
        
        void OnDisable ()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
        
        // ----- Private Methods
        private void RegisterEvents ()
        {
            // Buttons
            soundsButton.clicked += () => ChangeTab(Sections.Sounds);
            musicButton.clicked += () => ChangeTab(Sections.Music);
            createButton.clicked += CreateAudio;
            clearMessageButton.clicked += () => resultMessageContainerLabel.visible = false;
            
            // Text Fields
            tagTextField.RegisterValueChangedCallback(changeValueEvent =>
            {
                OnChangeTagField(changeValueEvent.newValue);
            });
            
            // Enum Fields
            compressionPresetEnumField.RegisterValueChangedCallback(changeValueEvent =>
            {
                ChangeCompressionPreset((CompressionPreset)changeValueEvent.newValue);
            });
            
            // Drag and Drop
            dropZone.RegisterCallback<DragEnterEvent>(OnDragEnter);
            dropZone.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            dropZone.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            dropZone.RegisterCallback<DragPerformEvent>(OnDragPerform);
            dropZone.RegisterCallback<ClickEvent>(evt =>
            {
                waitingForPickerResult = true;
                EditorGUIUtility.ShowObjectPicker<AudioClip>(null, 
                    false, "", AUDIOCLIP_PICKER_ID);
            });
            
            // Play Mode
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
        
        private void OnPlayModeStateChanged (PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) rootVisualElement.SetEnabled(false);
            else if (state == PlayModeStateChange.ExitingPlayMode) rootVisualElement.SetEnabled(true);
        }
        
        private void ChangeTab (Sections section)
        {
            currentSection = section;
            bool isSounds = section == Sections.Sounds;
            soundsButton.style.unityBackgroundImageTintColor =
                new StyleColor(isSounds ? EditorHelper.ORANGE_COLOR : EditorHelper.GREY_COLOR);
            musicButton.style.unityBackgroundImageTintColor = 
                new StyleColor(isSounds ? EditorHelper.GREY_COLOR : EditorHelper.ORANGE_COLOR);
            compressionPresetEnumField.value =
                isSounds ? CompressionPreset.FrequentSound : CompressionPreset.AmbientMusic;
        }

        private void OnChangeTagField (string newTag)
        {
            currentTag = newTag;
            
            if (!EditorHelper.IsTagValid(newTag)) tagTextField.AddToClassList("text-field-error");
            else tagTextField.RemoveFromClassList("text-field-error");
            
            TryActiveCreateButton();
        }
        
        private void ChangeCompressionPreset (CompressionPreset newPreset)
        {
            compressionPresetInfoLabel.text = newPreset switch
            {
                CompressionPreset.AmbientMusic => AMBIENT_MUSIC_INFO,
                CompressionPreset.FrequentSound => FREQUENT_SOUND_INFO,
                CompressionPreset.OccasionalSound => OCCASIONAL_SOUND_INFO,
                _ => compressionPresetInfoLabel.text
            };
            
            currentCompressionPreset = newPreset;
        }

        #region Drag and Drop
        private void OnDragEnter (DragEnterEvent evt)
        {
            dropZone.AddToClassList("dragging");
        }

        private void OnDragLeave (DragLeaveEvent evt)
        {
            dropZone.RemoveFromClassList("dragging");
        }

        private void OnDragUpdated (DragUpdatedEvent evt)
        {
            bool isValid = DragAndDrop.objectReferences.All(obj => obj is AudioClip);

            DragAndDrop.visualMode = isValid ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
        }

        private void OnDragPerform (DragPerformEvent evt)
        {
            dropZone.RemoveFromClassList("dragging");

            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is not AudioClip clip) continue;
                
                AddAudioClip(clip);
            }

            DragAndDrop.AcceptDrag();
        }
        #endregion

        private void AddAudioClip (AudioClip clip)
        {
            clipsList.style.display = DisplayStyle.Flex;
            importedAudioClips.Add(clip);
            AudioClipTemplate clipView = new AudioClipTemplate(clip, clip.name, RemoveClipFromList);
            clipsListContent.Add(clipView);
            
            TryActiveCreateButton();
        }

        private void RemoveClipFromList (AudioClipTemplate clipView)
        {
            clipsListContent.Remove(clipView);
            importedAudioClips.Remove(clipView.Clip);
            if (importedAudioClips.Count == 0) clipsList.style.display = DisplayStyle.None;
            
            TryActiveCreateButton();
        }

        private void TryActiveCreateButton ()
        {
            if (importedAudioClips.Count == 0 || !EditorHelper.IsTagValid(currentTag))
            {
                createButton.SetEnabled(false);
                createButton.RemoveFromClassList("create-button");
                return;
            }
            
            createButton.SetEnabled(true);
            createButton.AddToClassList("create-button");
        }
        
        private void CreateAudio ()
        {
            string resultMessage;
            resultMessageContainerLabel.visible = true;

            if (!EditorHelper.IsTagValid(currentTag))
            {
                resultMessageLabel.style.color = new StyleColor(EditorHelper.RED_COLOR);
                resultMessage = "Tag cannot contain special characters or start with a number";
                resultMessageLabel.text = resultMessage;
                return;
            }

            bool forceToMono = forceToMonoToggle.value;
            bool creationSuccess;
            List<AudioClip> validAudioClips = importedAudioClips.Where(audioClip => audioClip != null).ToList();

            if (currentSection == Sections.Sounds)
            {
                creationSuccess = AssetLocator.Instance.SoundDataCollection
                    .CreateSound(validAudioClips.ToArray(), currentTag,
                        currentCompressionPreset, forceToMono, out resultMessage);
            }
            else
            {
                creationSuccess = AssetLocator.Instance.MusicDataCollection
                    .CreateMusicTrack(validAudioClips.ToArray(), currentTag, 
                        currentCompressionPreset, forceToMono, out resultMessage);
            }

            resultMessageLabel.style.color = new StyleColor(creationSuccess ? Color.white : EditorHelper.RED_COLOR);
            resultMessageLabel.text = resultMessage;
            
            if (!creationSuccess) return;

            EditorHelper.ChangeAudioClipImportSettings(validAudioClips.ToArray(), currentCompressionPreset, forceToMono);
            EditorHelper.SaveCollectionChanges(currentSection);

            importedAudioClips.Clear();
        }
    }
}
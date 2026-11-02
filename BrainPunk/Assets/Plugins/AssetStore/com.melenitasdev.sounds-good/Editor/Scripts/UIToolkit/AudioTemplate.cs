using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using MelenitasDev.SoundsGood.Domain;

namespace MelenitasDev.SoundsGood.Editor
{
    public class AudioTemplate : VisualElement
    {
        // ----- UI Elements
        private TextField tagTextField => this.Q<TextField>("TagTextField");
        private ListView clipsList => this.Q<ListView>("ClipsList");
        private VisualElement clipsListContainer => clipsList.Q<VisualElement>("unity-content-container");
        private Label compressionPresetLabel => this.Q<Label>("CompressionPresetLabel");
        private Label outputLabel => this.Q<Label>("OutputLabel");
        private Button addClipButton;
        private VisualElement applyChangesLabel => this.Q<VisualElement>("ApplyChangeLabel");
        private Label applyChangesWarningLabel => this.Q<Label>("ApplyChangesWarningLabel");
        private Button applyChangesButton => this.Q<Button>("ApplyChangesButton");
        private Button undoButton => this.Q<Button>("UndoButton");
        private VisualElement removeLabel => this.Q<VisualElement>("RemoveLabel");
        private VisualElement removeConfirmLabel => this.Q<VisualElement>("RemoveConfirmLabel");
        private Button cancelRemoveButton => this.Q<Button>("CancelRemoveButton");
        private Button removeButton => this.Q<Button>("RemoveButton");

        // ----- Fields
        private string tag;
        private AudioClip[] clips;
        private CompressionPreset compressionPreset;
        private bool forceToMono;
        private string currentTag;
        private Dictionary<ObjectField, AudioClip> currentClipsDictionary = new Dictionary<ObjectField, AudioClip>();
        private Sections mySection;
        private Action onApplyChanges;
        private Action onRemove;
        private bool waitingRemoveResult;
        private bool removeConfirmJustCancelled;
        
        // ----- Const Fields
        private const string APPLY_QUESTION =
            "Do you want to apply the changes?";
        private const string TAG_CHANGE_WARNING =
            "References to this audio in the code could be lost.";
        
        // ----- Public Methods
        public AudioTemplate (SoundData soundData, Sections section, Action onApplyChanges, Action onRemove)
        {
            VisualTreeAsset asset = AssetLocator.Instance.AudioTemplate;
            asset.CloneTree(this);

            tag = soundData.Tag;
            currentTag = soundData.Tag;
            clips = soundData.Clips;
            compressionPreset = soundData.CompressionPreset;
            forceToMono = soundData.ForceToMono;
            mySection = section;
            this.onApplyChanges = onApplyChanges;
            this.onRemove = onRemove;

            tagTextField.value = tag;
            CreateAddClipButton();
            
            foreach (AudioClip clip in clips)
            {
                CreateClip(clip);
            }
            
            compressionPresetLabel.text = soundData.CompressionPreset.ToString();
            outputLabel.text = soundData.ForceToMono ? "Mono" : "Stereo";
            
            applyChangesLabel.style.display = DisplayStyle.None;
            removeLabel.style.display = DisplayStyle.None;
            removeConfirmLabel.style.display = DisplayStyle.None;
            
            RegisterEvents();
        }

        public void SetActiveRemoveMode (bool active)
        {
            removeLabel.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            if (!active) CancelRemove(false);
        }
        
        // ----- Private Methods
        private void RegisterEvents ()
        {
            tagTextField.RegisterValueChangedCallback(evt => OnTagChange(evt.newValue));

            applyChangesButton.clicked += ApplyChanges;
            undoButton.clicked += Undo;
            
            removeButton.RegisterCallback<ClickEvent>(evt => Remove());
            cancelRemoveButton.RegisterCallback<ClickEvent>(evt => CancelRemove(true));
            removeLabel.RegisterCallback<ClickEvent>(ShowRemoveConfirmLabel);
        }
        
        private void AddClip ()
        {
            CreateClip(null);
            
            CheckChanges();
        }

        private void CreateClip (AudioClip clip)
        {
            var clipField = new ObjectField
            {
                objectType = typeof(AudioClip),
                value = clip
            };
            clipsListContainer.Add(clipField);

            clipField.RegisterValueChangedCallback(evt => OnClipChange((AudioClip)evt.newValue, clipField));
            
            var removeButton = new Button();
            removeButton.AddToClassList("button");
            removeButton.style.unityBackgroundImageTintColor = new StyleColor(new Color(1, 0.7f, 0.4f, 1));
            removeButton.text = "x";
            removeButton.clicked += () => RemoveClip(clipField);
            clipField.hierarchy.Add(removeButton);
            
            currentClipsDictionary.Add(clipField, clip);
        }

        private void RemoveClip (ObjectField clipField)
        {
            currentClipsDictionary.Remove(clipField);
            clipField.RemoveFromHierarchy();
            
            CheckChanges();
        }

        private void CreateAddClipButton ()
        {
            addClipButton = new Button();
            addClipButton.AddToClassList("button");
            addClipButton.style.unityBackgroundImageTintColor = new StyleColor(new Color(1, 0.8f, 0.65f, 1));
            addClipButton.text = "Add";
            addClipButton.clicked += AddClip;
            tagTextField.hierarchy.Add(addClipButton);
        }

        private void OnTagChange (string newTag)
        {
            currentTag = newTag;

            if (!EditorHelper.IsTagValid(newTag)) tagTextField.AddToClassList("text-field-error");
            else tagTextField.RemoveFromClassList("text-field-error");
            
            CheckChanges();
        }

        private void OnClipChange (AudioClip newClip, ObjectField clipField)
        {
            currentClipsDictionary[clipField] = newClip;
            
            CheckChanges();
        }

        private void CheckChanges ()
        {
            bool differentTag = !currentTag.Equals(tag);
            bool differentClips = clips.Length != currentClipsDictionary.Count;
            if (!differentClips)
            {
                int i = 0;
                foreach (var currentClip in currentClipsDictionary.Values)
                {
                    if (clips[i] != currentClip)
                    {
                        differentClips = true;
                        break;
                    }
                    i++;
                }
            }

            if (!differentTag && !differentClips)
            {
                applyChangesLabel.style.display = DisplayStyle.None;
                return;
            }

            applyChangesLabel.style.display = DisplayStyle.Flex;
            applyChangesWarningLabel.text = APPLY_QUESTION;
            if (differentTag) applyChangesWarningLabel.text += "\n" + TAG_CHANGE_WARNING;
            if (EditorHelper.IsTagValid(currentTag) 
                && currentClipsDictionary.Values.ToList().TrueForAll(clip => clip != null))
            {
                applyChangesButton.SetEnabled(true);
                applyChangesButton.AddToClassList("button");
            }
            else
            {
                applyChangesButton.SetEnabled(false);
                applyChangesButton.RemoveFromClassList("button");
            }
        }

        private void ApplyChanges ()
        {
            string resultMessage;
            bool result;
            if (mySection == Sections.Sounds)
            {
                result = AssetLocator.Instance.SoundDataCollection
                    .EditSound(tag, currentTag, currentClipsDictionary.Values.ToArray(), out resultMessage);
            }
            else
            {
                result = AssetLocator.Instance.MusicDataCollection
                    .EditMusic(tag, currentTag, currentClipsDictionary.Values.ToArray(), out resultMessage);
            }

            if (!result)
            {
                applyChangesWarningLabel.text = resultMessage;
                return;
            }

            tag = currentTag;
            clips = currentClipsDictionary.Values.ToArray();
            EditorHelper.ChangeAudioClipImportSettings(clips, compressionPreset, forceToMono);
            applyChangesLabel.style.display = DisplayStyle.None;
            Debug.Log(resultMessage);
            
            onApplyChanges?.Invoke();
            
            EditorHelper.SaveCollectionChanges(mySection);
        }

        private void Undo ()
        {
            currentTag = tag;
            tagTextField.value = tag;
            
            currentClipsDictionary.Clear();
            clipsListContainer.Clear();
            foreach (AudioClip clip in clips)
            {
                CreateClip(clip);
            }
            
            applyChangesLabel.style.display = DisplayStyle.None;
        }

        private void ShowRemoveConfirmLabel (ClickEvent evt)
        {
            if (waitingRemoveResult) return;
            if (removeConfirmJustCancelled)
            {
                removeConfirmJustCancelled = false;
                return;
            }
            
            waitingRemoveResult = true;
            removeConfirmLabel.style.display = DisplayStyle.Flex;
        }

        private void CancelRemove (bool cancelledWithClick)
        {
            waitingRemoveResult = false;
            removeConfirmJustCancelled = cancelledWithClick;
            removeConfirmLabel.style.display = DisplayStyle.None;
        }
        
        private void Remove ()
        {
            if (mySection == Sections.Sounds)
            {
                AssetLocator.Instance.SoundDataCollection.RemoveSound(tag);
            }
            else
            {
                AssetLocator.Instance.MusicDataCollection.RemoveMusicTrack(tag);
            }
            
            onRemove?.Invoke();
            onApplyChanges?.Invoke();
        }
    }
}

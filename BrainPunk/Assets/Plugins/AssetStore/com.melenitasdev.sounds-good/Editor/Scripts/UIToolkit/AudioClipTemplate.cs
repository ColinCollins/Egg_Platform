using System;
using MelenitasDev.SoundsGood.Domain;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MelenitasDev.SoundsGood.Editor
{
    public class AudioClipTemplate : VisualElement
    {
        public AudioClip Clip;
        
        private Label clipLabel => this.Q<Label>("ClipName");
        private Button removeButton => this.Q<Button>("RemoveButton");

        public AudioClipTemplate (AudioClip clip, string clipName, Action<AudioClipTemplate> removeCallback)
        {
            Init(clip, clipName, removeCallback);
        }
        
        private void Init (AudioClip clip, string clipName, Action<AudioClipTemplate> removeCallback)
        {
            VisualTreeAsset asset = AssetLocator.Instance.AudioClipTemplate;
            
            asset.CloneTree(this);
            
            Clip = clip;
            clipLabel.text = clipName;
            removeButton.clicked += () =>
            {
                removeCallback?.Invoke(this);
            };
        }
    }
}

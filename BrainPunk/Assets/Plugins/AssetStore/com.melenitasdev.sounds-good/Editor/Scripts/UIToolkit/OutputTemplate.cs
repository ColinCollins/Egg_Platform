using System;
using UnityEngine.UIElements;
using MelenitasDev.SoundsGood.Domain;
using UnityEngine;

namespace MelenitasDev.SoundsGood.Editor
{
    public class OutputTemplate : VisualElement
    {
        // ----- UI Elements
        private Label outputNameLabel => this.Q<Label>("OutputNameLabel");
        private Slider volumeSlider => this.Q<Slider>("VolumeSlider");
        private Label volumePercentageLabel => this.Q<Label>("VolumePercentageLabel");
        private VisualElement volumeLabel => this.Q<VisualElement>("VolumeLabel");
        private VisualElement exposeVolumeWarningLabel => this.Q<VisualElement>("ExposeVolumeWarningLabel");
        
        // ----- Fields
        private string outputName;
        private Action<string, float> onValueChange;
        
        // ----- Public Methods
        public OutputTemplate (string outputName, float volume, Action<string, float> onValueChange)
        {
            VisualTreeAsset asset = AssetLocator.Instance.OutputTemplate;
            asset.CloneTree(this);

            volumeSlider.RegisterValueChangedCallback(evt => OnSliderValueChange(evt.newValue));
            
            outputNameLabel.text = outputName;
            volumePercentageLabel.text = $"{volume * 100:F0}%";
            
            this.outputName = outputName;
            this.onValueChange = onValueChange;
            if (volume != -1)
            {
                volumeSlider.value = volume;
                SwitchVolumeLabel(true);
            }
            else
            {
                SwitchVolumeLabel(false);
            }
        }

        public void SetBoldName ()
        {
            outputNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
        
        // ----- Private Methods
        private void OnSliderValueChange (float volume)
        {
            volumePercentageLabel.text = $"{volume * 100:F0}%";
            onValueChange?.Invoke(outputName, volume);
        }

        private void SwitchVolumeLabel (bool exposed)
        {
            volumeLabel.style.display = exposed ? DisplayStyle.Flex : DisplayStyle.None;
            exposeVolumeWarningLabel.style.display = exposed ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}

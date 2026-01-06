using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace MelenitasDev.SoundsGood.Demo
{
    public partial class SG_DynamicMusicVolumeSlider // Serialized Fields
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI percentageLabel;
        
        [Header("Settings")]
        [SerializeField] private Track targetTrack;
    }

    public partial class SG_DynamicMusicVolumeSlider // Fields
    {
        private Slider slider;
    }
    
    public partial class SG_DynamicMusicVolumeSlider // Properties
    {
        public float Volume => slider.value;
        public Track TargetTrack => targetTrack;
        public Action<Track, float> OnValueChange { get; set; }
    }

    [RequireComponent(typeof(Slider))]
    public partial class SG_DynamicMusicVolumeSlider : MonoBehaviour
    {
        void Awake ()
        {
            slider = GetComponent<Slider>();
            slider.onValueChanged.AddListener(OnChangeValue);
        }

        void Start ()
        {
            slider.value = 0.5f;
            OnChangeValue(0.5f);
        }
    }

    public partial class SG_DynamicMusicVolumeSlider // Private Methods
    {
        private void OnChangeValue (float value)
        {
            ChangeVolume(targetTrack, value);
        }
        
        private void ChangeVolume (Track track, float volume)
        {
            OnValueChange?.Invoke(track, volume);
            percentageLabel.text = $"{(volume * 100):F0}%";
        }
    }
}

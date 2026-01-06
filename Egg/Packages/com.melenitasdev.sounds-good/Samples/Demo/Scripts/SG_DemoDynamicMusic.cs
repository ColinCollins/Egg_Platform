/*
 * All rights to the Sounds Good plugin, © Created by Melenitas Dev, are reserved.
 * Distribution of the standalone asset is strictly prohibited,
 * but it can be used within your projects."
 */
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MelenitasDev.SoundsGood.Demo
{
    public partial class SG_DemoDynamicMusic // Serialized Fields
    {
        [Header("References")]
        [Header("Customization")]
        [SerializeField] private Toggle loopToggle;
        [SerializeField] private TMP_InputField fadeInInput;
        [SerializeField] private TMP_InputField fadeOutInput;
        [SerializeField] private SG_DynamicMusicVolumeSlider[] volumeSliders;

        [Header("Player")]
        [SerializeField] private Image playerProgressBar;
        [SerializeField] private TextMeshProUGUI clipDurationLabel;
        [SerializeField] private TextMeshProUGUI clipNameLabel;
        [SerializeField] private TextMeshProUGUI playedTimeLabel;
    }

    public partial class SG_DemoDynamicMusic // Fields
    {
        private DynamicMusic dynamicMusic;
    }

    public partial class SG_DemoDynamicMusic : MonoBehaviour
    {
        void OnEnable ()
        {
            dynamicMusic = new DynamicMusic(new [] { 
                "gravityBass",
                "gravityKick",
                "gravityPercussion",
                "gravitySnare",
                "gravityHitHat",
                "gravityGuitar",
                "gravityCyberline",
                "gravityAlert"
            });

            foreach (var volumeSlider in volumeSliders)
            {
                volumeSlider.OnValueChange += ChangeVolume;
            }

            clipNameLabel.text = "------";
            dynamicMusic.OnComplete(() => playerProgressBar.fillAmount = 1);
        }

        void Update ()
        {
            if (!dynamicMusic.Playing) return;
            
            playedTimeLabel.text = FormatSecsToTime(dynamicMusic.CurrentLoopCycleTime);
            playerProgressBar.fillAmount = dynamicMusic.CurrentLoopCycleTime / dynamicMusic.ClipDuration;
        }

        void OnDestroy ()
        {
            foreach (var volumeSlider in volumeSliders)
            {
                volumeSlider.OnValueChange -= ChangeVolume;
            }
        }
    }

    public partial class SG_DemoDynamicMusic // Public Methods
    {
        public void Play ()
        {
            if (dynamicMusic.Paused)
            {
                dynamicMusic.Resume();
                return;
            }
            
            foreach (var volumeSlider in volumeSliders)
            {
                dynamicMusic.SetTrackVolume(volumeSlider.TargetTrack, volumeSlider.Volume);
            }
            
            dynamicMusic
                .SetLoop(loopToggle.isOn)
                .SetSpatialSound(false)
                .SetFadeOut(Int32.Parse(fadeOutInput.text))
                .Play(Int32.Parse(fadeInInput.text));
            
            clipNameLabel.text = dynamicMusic.Clips[0].name;
            clipDurationLabel.text = FormatSecsToTime(dynamicMusic.ClipDuration);
        }
        
        public void Pause ()
        {
            dynamicMusic.Pause();
        }

        public void Stop ()
        {
            dynamicMusic.Stop(Int32.Parse(fadeOutInput.text));
            
            playedTimeLabel.text = "0:00";
            playerProgressBar.fillAmount = 0;
        }

        public void ChangeVolume (Track track, float volume)
        {
            if (!dynamicMusic.Playing) return;
            
            dynamicMusic.ChangeTrackVolume(track, volume);
        }
    }
    
    public partial class SG_DemoDynamicMusic // Private Methods
    {
        private string FormatSecsToTime (float secs)
        {
            int formatMins = (int)((secs % 3600) / 60);
            int formatSecs = Mathf.CeilToInt(secs % 60);
            return string.Format("{0:D1}:{1:D2}", formatMins, formatSecs);
        }
    }
}

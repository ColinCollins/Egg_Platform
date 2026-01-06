using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MelenitasDev.SoundsGood.Demo
{
    public partial class SG_DemoMusic // Serialized Fields
    {
        [Header("References")]
        [Header("Customization")]
        [SerializeField] private Toggle loopToggle;
        [SerializeField] private TMP_InputField fadeInInput;
        [SerializeField] private TMP_InputField fadeOutInput;
        [SerializeField] private Slider volumeSlider;

        [Header("Player")]
        [SerializeField] private Image playerProgressBar;
        [SerializeField] private TextMeshProUGUI clipDurationLabel;
        [SerializeField] private TextMeshProUGUI clipNameLabel;
        [SerializeField] private TextMeshProUGUI playedTimeLabel;
    }

    public partial class SG_DemoMusic // Fields
    {
        private Music music;
    }

    public partial class SG_DemoMusic : MonoBehaviour
    {
        void OnEnable ()
        {
            clipNameLabel.text = "------";
            
            music = new Music("guitarLoop");
            music.OnComplete(() => playerProgressBar.fillAmount = 1);
        }

        void Update ()
        {
            if (!music.Playing) return;
            
            playedTimeLabel.text = FormatSecsToTime(music.CurrentLoopCycleTime);
            playerProgressBar.fillAmount = music.CurrentLoopCycleTime / music.ClipDuration;
        }
    }

    public partial class SG_DemoMusic // Public Methods
    {
        public void Play ()
        {
            if (music.Paused)
            {
                music.Resume();
                return;
            }
            
            music
                .SetLoop(loopToggle.isOn)
                .SetVolume(volumeSlider.value)
                .SetSpatialSound(false)
                .SetFadeOut(Int32.Parse(fadeOutInput.text))
                .Play(Int32.Parse(fadeInInput.text));
            
            clipNameLabel.text = music.Clip.name;
            clipDurationLabel.text = FormatSecsToTime(music.ClipDuration);
        }
        
        public void Pause ()
        {
            music.Pause();
        }

        public void Stop ()
        {
            music.Stop(Int32.Parse(fadeOutInput.text));
            
            playedTimeLabel.text = "0:00";
            playerProgressBar.fillAmount = 0;
        }

        public void ChangeVolume (float volume)
        {
            if (!music.Playing) return;
            
            music.ChangeVolume(volume);
        }
    }

    public partial class SG_DemoMusic // Private Methods
    {
        private string FormatSecsToTime (float secs)
        {
            int formatMins = (int)((secs % 3600) / 60);
            int formatSecs = Mathf.CeilToInt(secs % 60);
            return string.Format("{0:D1}:{1:D2}", formatMins, formatSecs);
        }
    }
}

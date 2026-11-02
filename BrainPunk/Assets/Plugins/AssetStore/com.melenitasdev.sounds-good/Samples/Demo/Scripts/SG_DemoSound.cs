using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MelenitasDev.SoundsGood.Demo
{
    public partial class SG_DemoSound // Serialized Fields
    {
        [Header("References")]
        [Header("Customization")]
        [SerializeField] private Toggle loopToggle;
        [SerializeField] private Toggle randomClipToggle;
        [SerializeField] private Toggle randomPitchToggle;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle laserClipToggle;
        [SerializeField] private Toggle coinClipToggle;
        [SerializeField] private Toggle hitClipToggle;
        
        [Header("Player")]
        [SerializeField] private Image playerProgressBar;
        [SerializeField] private TextMeshProUGUI clipDurationLabel;
        [SerializeField] private TextMeshProUGUI clipNameLabel;
        [SerializeField] private TextMeshProUGUI playedTimeLabel;
    }

    public partial class SG_DemoSound // Fields
    {
        private Sound sound;
    }

    public partial class SG_DemoSound : MonoBehaviour
    {
        void OnEnable ()
        {
            clipNameLabel.text = "------";
            
            sound = new Sound("laser");
            sound.OnComplete(() => playerProgressBar.fillAmount = 1);
            
            laserClipToggle.onValueChanged.AddListener(OnSelectClip);
            coinClipToggle.onValueChanged.AddListener(OnSelectClip);
            hitClipToggle.onValueChanged.AddListener(OnSelectClip);
        }

        void Update ()
        {
            if (!sound.Playing) return;
            
            playedTimeLabel.text = FormatSecsToTime(sound.CurrentLoopCycleTime);
            playerProgressBar.fillAmount = sound.CurrentLoopCycleTime / sound.ClipDuration;
        }
    }

    public partial class SG_DemoSound // Public Methods
    {
        public void Play ()
        {
            if (sound.Paused)
            {
                sound.Resume();
                return;
            }

            if (sound.Playing) Stop();
            
            if (randomPitchToggle.isOn) sound.SetRandomPitch();
            sound.SetLoop(loopToggle.isOn)
                .SetRandomClip(randomClipToggle.isOn)
                .SetVolume(volumeSlider.value)
                .SetSpatialSound(false)
                .Play();

            clipNameLabel.text = sound.Clip.name;
            clipDurationLabel.text = FormatSecsToTime(sound.ClipDuration);
        }
        
        public void Pause ()
        {
            sound.Pause();
        }

        public void Stop ()
        {
            sound.Stop();
            
            playerProgressBar.fillAmount = 0;
            playedTimeLabel.text = "0:00";
        }
        
        public void ChangeVolume (float volume)
        {
            if (!sound.Playing) return;
            
            sound.ChangeVolume(volume);
        }
    }

    public partial class SG_DemoSound // Private Methods
    {
        private void OnSelectClip (bool isOn)
        {
            string sfx;
            if (coinClipToggle.isOn) sfx = "coin";
            else if (laserClipToggle.isOn) sfx = "laser";
            else sfx = "hit";
            sound.SetClip(sfx);
        }
        
        private string FormatSecsToTime (float secs)
        {
            int formatMins = (int)((secs % 3600) / 60);
            int formatSecs = Mathf.CeilToInt(secs % 60);
            return string.Format("{0:D1}:{1:D2}", formatMins, formatSecs);
        }
    }
}

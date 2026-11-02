using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MelenitasDev.SoundsGood.Demo
{
    public partial class SG_DemoPlaylist // Serialized Fields
    {
        [Header("References")] 
        [Header("Customization")] 
        [SerializeField] private Toggle loopToggle;

        [SerializeField] private TMP_InputField fadeInInput;
        [SerializeField] private TMP_InputField fadeOutInput;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TMP_Dropdown[] trackDropdowns;

        [Header("Player")] 
        [SerializeField] private Image playerProgressBar;
        [SerializeField] private TextMeshProUGUI clipDurationLabel;
        [SerializeField] private TextMeshProUGUI clipNameLabel;
        [SerializeField] private TextMeshProUGUI playedTimeLabel;
    }

    public partial class SG_DemoPlaylist // Fields
    {
        private Playlist playlist;

        private readonly string[] availableTracks = new string[]
        {
            "DuckDescending",
            "LittleGreenMen",
            "StudyFirst"
        };
    }

    public partial class SG_DemoPlaylist : MonoBehaviour
    {
        void OnEnable ()
        {
            clipNameLabel.text = "------";
            
            playlist = new Playlist(GetPlaylist());
            playlist.OnComplete(() => playerProgressBar.fillAmount = 1);
            playlist.OnNextTrackStart(() =>
            {
                clipNameLabel.text = playlist.CurrentPlaylistClip.name;
                clipDurationLabel.text = FormatSecsToTime(playlist.CurrentClipDuration);
            });
        }

        void Update ()
        {
            if (!playlist.Playing) return;

            playedTimeLabel.text = FormatSecsToTime(playlist.CurrentLoopCycleTime);
            playerProgressBar.fillAmount = playlist.CurrentLoopCycleTime / playlist.CurrentClipDuration;
        }
    }

    public partial class SG_DemoPlaylist // Public Methods
    {
        public void Play ()
        {
            if (playlist.Paused)
            {
                playlist.Resume();
                return;
            }

            playlist
                .SetLoop(loopToggle.isOn)
                .SetVolume(volumeSlider.value)
                .SetPlaylist(GetPlaylist())
                .SetSpatialSound(false)
                .SetFadeOut(Int32.Parse(fadeOutInput.text))
                .SetFadeIn(Int32.Parse(fadeInInput.text))
                .Play();

            clipNameLabel.text = playlist.CurrentPlaylistClip.name;
            clipDurationLabel.text = FormatSecsToTime(playlist.CurrentClipDuration);
        }

        public void Pause ()
        {
            playlist.Pause();
        }

        public void Stop ()
        {
            playlist.Stop(Int32.Parse(fadeOutInput.text));

            playedTimeLabel.text = "0:00";
            playerProgressBar.fillAmount = 0;
        }

        public void ChangeVolume (float volume)
        {
            if (!playlist.Playing) return;

            playlist.ChangeVolume(volume);
        }
    }

    public partial class SG_DemoPlaylist // Private Methods
    {
        private string FormatSecsToTime (float secs)
        {
            int formatMins = (int)((secs % 3600) / 60);
            int formatSecs = Mathf.CeilToInt(secs % 60);
            return string.Format("{0:D1}:{1:D2}", formatMins, formatSecs);
        }
        
        private string[] GetPlaylist ()
        {
            string[] playlistTracks = new string[trackDropdowns.Length];
            for (int i = 0; i < playlistTracks.Length; i++)
            {
                playlistTracks[i] = availableTracks[trackDropdowns[i].value];
            }
            return playlistTracks;
        }
    }
}
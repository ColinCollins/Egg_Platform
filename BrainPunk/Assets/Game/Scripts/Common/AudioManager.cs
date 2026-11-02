using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.SaveModule;
using Game.Events;
using Game.Play;
using MelenitasDev.SoundsGood;
using UnityEngine;

namespace Game.Scripts.Common
{
    /// <summary>
    /// Static wrapper for Sounds Good: music and one-shot sound.
    /// Music/SFX tags must be created in Sounds Good Audio Creator.
    /// </summary>
    public static class AudioManager
    {
        private static Music _currentMusic;
        private static string _currentMusicTag = string.Empty;
        private const float DefaultMusicVolume = .5f;
        private const float DefaultSoundVolume = .4f;
        private const float DefaultFadeIn = 0f;
        private const float DefaultFadeOut = 0f;

        private static readonly Dictionary<string, Sound> _activeSounds = new Dictionary<string, Sound>();
        private static long _nextSoundId;

        private static bool musicIsOn = false;
        private static bool sfxIsOn = false;
        private static EventSubscriber _subscriber;

        public static void Init()
        {
            AddListener();

            musicIsOn = DB.GameSetting.MusicOn;
            sfxIsOn = DB.GameSetting.SfxOn;
        }

        private static void AddListener()
        {
            EventsUtils.ResetEvents(ref _subscriber);
            _subscriber.Subscribe<MusicToggleEvent>(OnMuiscToggleChange);
             _subscriber.Subscribe<SfxToggleEvent>(OnSfxToggleChange);
        }

        private static void OnSfxToggleChange(SfxToggleEvent @event)
        {
            sfxIsOn = @event.isOn;
        }

        private static void OnMuiscToggleChange(MusicToggleEvent @event)
        {
            if (!@event.isOn)
            {
                PauseMusic();
            }
            else
            {
                ResumeMusic();
            }

            musicIsOn = @event.isOn;
            if (@event.isOn && _currentMusic == null)
            {
                PlayMusic(PlayCtrl.Instance.CheckState(GamePlayStateName.START) ? "musicOutGame" : "musicInGame");
            }
        }


        // ----- Music -----

        /// <summary>Play music by tag. Replaces current music if already playing.</summary>
        /// <param name="tag">Track tag from Audio Creator (e.g. "musicOutgame")</param>
        /// <param name="loop">Loop or play once</param>
        /// <param name="volume">0..1</param>
        /// <param name="fadeInSeconds">Fade in duration</param>
        public static void PlayMusic(string tag, bool loop = true, float volume = DefaultMusicVolume, float fadeInSeconds = DefaultFadeIn)
        {
            if (!musicIsOn)
                return;
            if (_currentMusic != null && _currentMusic.Playing)
                _currentMusic.Stop(DefaultFadeOut);

            _currentMusic = new Music(tag);
            _currentMusicTag = tag;
            _currentMusic.SetLoop(loop)
            .SetVolume(volume)
            .SetSpatialSound(false)
            .Play(fadeInSeconds);
        }

        /// <summary>Play music by Track. Replaces current music if already playing.</summary>
        public static void PlayMusic(Track track, bool loop = true, float volume = DefaultMusicVolume, float fadeInSeconds = DefaultFadeIn)
        {
            PlayMusic(track.ToString(), loop, volume, fadeInSeconds);
        }

        /// <summary>Stop current music with optional fade out.</summary>
        public static void StopMusic(float fadeOutSeconds = DefaultFadeOut)
        {
            if (_currentMusic == null || !_currentMusic.Using) return;
            _currentMusic.Stop(fadeOutSeconds);
            _currentMusic = null;
            _currentMusicTag = string.Empty;
        }

        /// <summary>Pause current music.</summary>
        public static void PauseMusic(float fadeOutSeconds = DefaultFadeOut)
        {
            if (_currentMusic == null || !_currentMusic.Using) return;
            _currentMusic.Pause(fadeOutSeconds);
        }

        /// <summary>Resume paused music.</summary>
        public static void ResumeMusic(float fadeInSeconds = DefaultFadeIn)
        {
            if (_currentMusic == null || !_currentMusic.Using) return;
            _currentMusic.Resume(fadeInSeconds);
        }

        /// <summary>Change volume of current music (0..1).</summary>
        public static void SetMusicVolume(float volume, float lerpTime = 0f)
        {
            if (_currentMusic == null || !_currentMusic.Playing) return;
            _currentMusic.ChangeVolume(volume, lerpTime);
        }

        /// <summary>True if any music is playing.</summary>
        public static bool IsMusicPlaying => _currentMusic != null && _currentMusic.Playing;

        /// <summary>Current music tag. Empty if no tracked music.</summary>
        public static string CurrentMusicTag => _currentMusicTag;

        /// <summary>True if current playing music tag equals target tag.</summary>
        public static bool IsCurrentMusicTag(string tag, StringComparison comparison = StringComparison.Ordinal)
        {
            if (!IsMusicPlaying || string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(_currentMusicTag))
                return false;

            return string.Equals(_currentMusicTag, tag, comparison);
        }

        /// <summary>True if current playing music tag equals target Track.</summary>
        public static bool IsCurrentMusicTag(Track track, StringComparison comparison = StringComparison.Ordinal)
        {
            return IsCurrentMusicTag(track.ToString(), comparison);
        }

        /// <summary>True if current playing music tag is in target tags.</summary>
        public static bool IsCurrentMusicInTags(StringComparison comparison = StringComparison.Ordinal, params string[] tags)
        {
            if (!IsMusicPlaying || tags == null || tags.Length == 0 || string.IsNullOrEmpty(_currentMusicTag))
                return false;

            for (int i = 0; i < tags.Length; i++)
            {
                if (string.Equals(_currentMusicTag, tags[i], comparison))
                    return true;
            }

            return false;
        }

        // ----- Sound (one-shot) -----

        /// <summary>Play sound by tag. Returns the Sound instance (e.g. to stop later); null if SFX off or invalid.</summary>
        /// <param name="tag">SFX tag from Audio Creator (e.g. "coin", "laser", "hit")</param>
        /// <param name="volume">0..1</param>
        /// <param name="clipIndex">指定音频片段索引，负数表示使用默认随机策略</param>
        /// <param name="randomPitch">是否应用随机音高</param>
        /// <param name="loop">是否循环播放，默认 false（播一次）</param>
        public static Sound PlaySound(string tag, float volume = DefaultSoundVolume, int clipIndex = -1, bool randomPitch = false, bool loop = false)
        {
            if (!sfxIsOn)
                return null;

            string id = "snd_" + (++_nextSoundId);

            var sound = new Sound(tag)
                .SetVolume(volume)
                .SetSpatialSound(false)
                .SetLoop(loop)
                .SetId(id)
                .OnComplete(() => RemoveSound(id));

            if (clipIndex >= 0)
                sound.SetClipByIndex(clipIndex);

            if (randomPitch)
                sound.SetRandomPitch();

            _activeSounds[id] = sound;
            sound.Play();
            if (!sound.Using)
                _activeSounds.Remove(id);
            return sound;
        }

        private static void RemoveSound(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            _activeSounds.Remove(id);
        }

        /// <summary>Play sound by SFX. Returns the Sound instance; null if SFX off or invalid.</summary>
        public static Sound PlaySound(SFX sfx, float volume = DefaultSoundVolume, int clipIndex = -1, bool randomPitch = false, bool loop = false)
        {
            return PlaySound(sfx.ToString(), volume, clipIndex, randomPitch, loop);
        }

        // ----- Global -----

        /// <summary>Stop all music, sounds, playlists (Sounds Good).</summary>
        public static void StopAll()
        {
            _currentMusic = null;
            _currentMusicTag = string.Empty;
            StopAllSound(0f);
            SoundsGoodManager.StopAll();
        }

        /// <summary>Stop all sounds currently recorded by PlaySound. Use when switching scene or need to clear SFX.</summary>
        /// <param name="fadeOutTime">淡出时长（秒），0 为立即停止</param>
        public static void StopAllSound(float fadeOutTime = 0f)
        {
            if (_activeSounds.Count == 0)
                return;

            // 拷贝一份快照，避免在 Stop 的 OnComplete 回调里修改字典导致枚举异常
            var sounds = new List<Sound>(_activeSounds.Values);
            _activeSounds.Clear();

            foreach (var sound in sounds)
            {
                if (sound != null && sound.Using)
                    sound.Stop(fadeOutTime);
            }
        }
    }
}

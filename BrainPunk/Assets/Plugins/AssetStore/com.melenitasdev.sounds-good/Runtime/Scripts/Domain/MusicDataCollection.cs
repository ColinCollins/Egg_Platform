/*
 * All rights to the Sounds Good plugin, © Created by Melenitas Dev, are reserved.
 * Distribution of the standalone asset is strictly prohibited.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MelenitasDev.SoundsGood.Domain
{
    public class MusicDataCollection : ScriptableObject
    {
        [SerializeField] private SoundData[] musicTracks = Array.Empty<SoundData>();
        
        private Dictionary<string, SoundData> musicTracksDictionary = new Dictionary<string, SoundData>();

        public SoundData[] MusicTracks => musicTracks;

        void OnEnable ()
        {
            Init();
        }

        private void Init ()
        {
            musicTracksDictionary.Clear();
            foreach (SoundData soundData in musicTracks)
            {
                musicTracksDictionary.Add(soundData.Tag, soundData);
            }
        }
        
        public SoundData GetMusicTrack (string tag)
        {
            if (musicTracksDictionary.TryGetValue(tag, out SoundData soundData)) return soundData;
            
            Debug.LogWarning($"Sound with tag '{tag}' does not exist.");
            return musicTracks[0];
        }
        
        public bool CreateMusicTrack (AudioClip[] clips, string tag, CompressionPreset compressionPreset, bool forceToMono, out string result)
        {
            if (string.IsNullOrEmpty(tag))
            {
                result = "Tag required! Please, write a tag to identify this music track.";
                return false;
            }

            if (musicTracks.Any(soundData => soundData.Tag == tag))
            {
                result = $"The tag '{tag}' already exist!";
                return false;
            }
            
            if (clips.Length <= 0)
            {
                result = "You must add at least 1 audio clip.";
                return false;
            }
            
            SoundData newSound = new SoundData(tag, clips, compressionPreset, forceToMono);
            SoundData[] previousSounds = musicTracks;
            musicTracks = new SoundData[musicTracks.Length + 1];
            for (int i = 0; i < musicTracks.Length - 1; i++)
            {
                musicTracks[i] = previousSounds[i];
            }
            musicTracks[musicTracks.Length - 1] = newSound;
            result = $"Music track '{tag}' has been created successfully.";
            return true;
        }
        
        public bool EditMusic (string tag, string newTag, AudioClip[] clips, out string result)
        {
            if (tag != newTag && musicTracks.Any(soundData => soundData.Tag == newTag))
            {
                result = $"The tag '{newTag}' already exists!";
                return false;
            }
            
            SoundData soundData = GetMusicTrack(tag);
            soundData.Tag = newTag;
            soundData.Clips = clips;
            
            musicTracksDictionary.Remove(tag);
            musicTracksDictionary.Add(newTag, soundData);
            
            result = $"Music '{newTag}' has been updated successfully.";
            return true;
        }

        public void RemoveMusicTrack (string tagToRemove)
        {
            List<SoundData> newMusicList = new List<SoundData>();
            newMusicList = musicTracks.ToList();
            foreach (var soundData in musicTracks)
            {
                if (!soundData.Tag.Equals(tagToRemove)) continue;
                newMusicList.Remove(soundData);
                break;
            }
            musicTracks = newMusicList.ToArray();
        }

        public void RemoveAll ()
        {
            musicTracks = Array.Empty<SoundData>();
        }
    }
}

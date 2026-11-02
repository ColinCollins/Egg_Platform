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
    public class SoundDataCollection : ScriptableObject
    {
        [SerializeField] private SoundData[] sounds = Array.Empty<SoundData>();

        private Dictionary<string, SoundData> soundsDictionary = new Dictionary<string, SoundData>();

        public SoundData[] Sounds => sounds;

        void OnEnable ()
        {
            Init();
        }

        private void Init ()
        {
            soundsDictionary.Clear();
            foreach (SoundData soundData in sounds)
            {
                soundsDictionary.Add(soundData.Tag, soundData);
            }
        }
        
        public SoundData GetSound (string tag)
        {
            if (soundsDictionary.TryGetValue(tag, out SoundData soundData)) return soundData;
            
            Debug.LogWarning($"Sound with tag '{tag}' does not exist.");
            return sounds[0];
        }

        public bool CreateSound (AudioClip[] clips, string tag, CompressionPreset compressionPreset, bool forceToMono, out string result)
        {
            if (tag == "")
            {
                result = "Tag required! Please, write a tag to identify this sound.";
                return false;
            }

            if (sounds.Any(soundData => soundData.Tag == tag))
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
            SoundData[] previousSounds = sounds;
            sounds = new SoundData[sounds.Length + 1];
            for (int i = 0; i < sounds.Length - 1; i++)
            {
                sounds[i] = previousSounds[i];
            }
            sounds[sounds.Length - 1] = newSound;
            result = $"Sound '{tag}' has been created successfully.";
            return true;
        }

        public bool EditSound (string tag, string newTag, AudioClip[] clips, out string result)
        {
            if (tag != newTag && sounds.Any(soundData => soundData.Tag == newTag))
            {
                result = $"The tag '{newTag}' already exists!";
                return false;
            }
            
            SoundData soundData = GetSound(tag);
            soundData.Tag = newTag;
            soundData.Clips = clips;

            soundsDictionary.Remove(tag);
            soundsDictionary.Add(newTag, soundData);
            
            result = $"Sound '{newTag}' has been updated successfully.";
            return true;
        }
        
        public void RemoveSound (string tagToRemove)
        {
            List<SoundData> newSoundsList = new List<SoundData>();
            newSoundsList = sounds.ToList();
            foreach (var soundData in sounds)
            {
                if (!soundData.Tag.Equals(tagToRemove)) continue;
                newSoundsList.Remove(soundData);
                break;
            }
            sounds = newSoundsList.ToArray();
        }

        public void RemoveAll ()
        {
            sounds = Array.Empty<SoundData>();
        }
    }
}
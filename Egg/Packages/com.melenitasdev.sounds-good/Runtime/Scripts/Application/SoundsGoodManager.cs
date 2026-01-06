/*
 * All rights to the Sounds Good plugin, © Created by Melenitas Dev, are reserved.
 * Distribution of the standalone asset is strictly prohibited.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Audio;
using MelenitasDev.SoundsGood.Domain;

[assembly: InternalsVisibleTo("SoundsGood.Editor")]

namespace MelenitasDev.SoundsGood
{
    public partial class SoundsGoodManager // Fields
    {
        private static readonly List<SoundsGoodAudioSource> audioSourcePool = new List<SoundsGoodAudioSource>();
        
        private static GameObject audioSourcesPoolParent;
    }

    public partial class SoundsGoodManager : MonoBehaviour
    {
        
    }

    public partial class SoundsGoodManager // Internal Static Methods
    {
        internal static SoundsGoodAudioSource GetSource ()
        {
            return GetSoundSourceFromPool();
        }

        internal static AudioMixerGroup GetOutput (Output output)
        {
            return GetOutput(output.ToString());
        }
        
        internal static AudioClip GetSFX (string tag, int index = -1)
        {
            if (AssetLocator.Instance.SoundDataCollection == null) return null;
            SoundData soundData = AssetLocator.Instance.SoundDataCollection.GetSound(tag);
            return soundData.GetClip(index);
        }
        
        internal static AudioClip GetTrack (string tag, int index = -1)
        {
            if (AssetLocator.Instance.MusicDataCollection == null) return null;
            SoundData soundData = AssetLocator.Instance.MusicDataCollection.GetMusicTrack(tag);
            return soundData.GetClip(index);
        }
        
        internal static float GetSavedOutputVolume (string outputName)
        {
            if (PlayerPrefs.HasKey(outputName)) return PlayerPrefs.GetFloat(outputName);
            PlayerPrefs.SetFloat(outputName, 1f);
            return 1f;
        }
    }
    
    public partial class SoundsGoodManager // Public Static Methods
    {
        /// <summary>
        /// Get last saved output volume.
        /// </summary>
        /// <param name="output">Target output</param>
        [Obsolete("The output volume is now auto-updated to the last saved volume. " + 
                  "There is no longer any reason to use this method.")]
        public static float GetLastSavedOutputVolume (Output output)
        {
            if (PlayerPrefs.HasKey(output.ToString())) return PlayerPrefs.GetFloat(output.ToString());
            Debug.LogWarning($"The {output.ToString()}'s volume has not been saved yet");
            return 0.5f;
        }
        
        /// <summary>
        /// Change Output volume.
        /// </summary>
        /// <param name="output">Target output</param>
        /// <param name="value">Target volume: min 0, Max: 1</param>
        public static void ChangeOutputVolume (Output output, float value)
        {
            ChangeOutputVolume(output.ToString(), value);
        }
        
        /// <summary>
        /// Change Output volume.
        /// </summary>
        /// <param name="outputName">Target output name</param>
        /// <param name="value">Target volume: min 0, Max: 1</param>
        public static void ChangeOutputVolume (string outputName, float value)
        {
            AudioMixer mixer = GetOutput(outputName).audioMixer;
            
            if (mixer == null)
            {
                Debug.LogError($"Can't change mixer volume because {outputName} don't exist." +
                               $"Make sure you have updated the outputs database on Outputs Manager window");
                return;
            }
            
            if (Application.isPlaying) SetAudioMixerLinearVolume(mixer, outputName, value);
            PlayerPrefs.SetFloat(outputName, value);
        }

        /// <summary>
        /// Pause all sounds, music, dynamic music and playlists.
        /// </summary>
        /// <param name="fadeOutTime">Seconds that fade out will last</param>
        public static void PauseAll (float fadeOutTime = 0)
        {
            foreach (SoundsGoodAudioSource sourcePoolElement in audioSourcePool)
            {
                sourcePoolElement.Pause(fadeOutTime);
            }
        }
        
        /// <summary>
        /// Pause specific sound, music, dynamic music or playlist without having the sound reference.
        /// </summary>
        /// <param name="id">The Id you've set to the music</param>
        /// <param name="fadeOutTime">Seconds that fade out will last</param>
        public static void Pause (string id, float fadeOutTime = 0)
        {
            var sourcePoolElement = audioSourcePool.FirstOrDefault(sourceElement => sourceElement.Id == id);
            if (sourcePoolElement == null || !sourcePoolElement.Using) 
            {
                Debug.LogWarning($"There is no music with the id '{id}'");
                return;
            }
            sourcePoolElement.Pause(fadeOutTime);
        }
        
        
        /// <summary>
        /// Stop all sounds, music, dynamic music and playlists.
        /// </summary>
        /// <param name="fadeOutTime">Seconds that fade out will last</param>
        public static void StopAll (float fadeOutTime = 0)
        {
            foreach (SoundsGoodAudioSource sourcePoolElement in audioSourcePool)
            {
                sourcePoolElement.Stop(fadeOutTime);
            }
        }
        
        /// <summary>
        /// Stop specific sound, music, dynamic music or playlist without having the sound reference.
        /// </summary>
        /// <param name="id">The Id you've set to the sound</param>
        /// <param name="fadeOutTime">Seconds that fade out will last</param>
        public static void Stop (string id, float fadeOutTime = 0)
        {
            var sourcePoolElement = audioSourcePool.FirstOrDefault(sourceElement => sourceElement.Id == id);
            if (sourcePoolElement == null || !sourcePoolElement.Using)
            {
                Debug.LogWarning($"There is no sound reproducing with the id '{id}'");
                return;
            }
            sourcePoolElement.Stop(fadeOutTime);
        }
        
        /// <summary>
        /// Resume all sounds, music, dynamic music and playlists.
        /// </summary>
        /// <param name="fadeInTime">Seconds that fade in will last</param>
        public static void ResumeAll (float fadeInTime = 0)
        {
            foreach (SoundsGoodAudioSource sourcePoolElement in audioSourcePool)
            {
                if (sourcePoolElement.Paused) sourcePoolElement.Resume(fadeInTime);
            }
        }
                
        /// <summary>
        /// Resume specific sound, music, dynamic music or playlist without having the sound reference.
        /// </summary>
        /// <param name="id">The Id you've set to the sound</param>
        /// <param name="fadeInTime">Seconds that fade in will last</param>
        public static void Resume (string id, float fadeInTime = 0)
        {
            var sourcePoolElement = audioSourcePool.FirstOrDefault(s => s.Id == id);
            if (sourcePoolElement == null || !sourcePoolElement.Paused)
            {
                Debug.LogWarning($"There is no sound paused with the id '{id}'");
                return;
            }
            sourcePoolElement.Resume(fadeInTime);
        }
        
        // ----- Obsoletes methods
        
        /// <summary> [OBSOLETE] Use <see cref="Stop(string,float)"/>. </summary>
        [Obsolete("Use SoundsGoodManager.Stop(id, fadeOutTime) instead. " +
                  "This method will be removed in a future version.")]
        public static void StopSound (string id, float fadeOutTime = 0) => Stop(id, fadeOutTime);

        /// <summary> [OBSOLETE] Use <see cref="Stop(string,float)"/>. </summary>
        [Obsolete("Use SoundsGoodManager.Stop(id, fadeOutTime) instead. " +
                  "This method will be removed in a future version.")]
        public static void StopMusic (string id, float fadeOutTime = 0) => Stop(id, fadeOutTime);
        
        /// <summary> [OBSOLETE] Use <see cref="StopAll(float)"/>. </summary>
        [Obsolete("Use SoundsGoodManager.StopAll(fadeOutTime) instead. " +
                  "This method will be removed in a future version.")]
        public static void StopAllSounds (float fadeOutTime = 0) => StopAll(fadeOutTime);
        
        /// <summary> [OBSOLETE] Use <see cref="StopAll(float)"/>. </summary>
        [Obsolete("Use SoundsGoodManager.StopAll(fadeOutTime) instead. " +
                  "This method will be removed in a future version.")]
        public static void StopAllMusic (float fadeOutTime = 0) => StopAll(fadeOutTime);
        
        /// <summary> [OBSOLETE] Use <see cref="Pause(string,float)"/>. </summary>
        [Obsolete("Use SoundsGoodManager.Pause(id, fadeOutTime) instead. " +
                  "This method will be removed in a future version.")]
        public static void PauseSound (string id, float fadeOutTime = 0) => Pause(id, fadeOutTime);
        
        /// <summary> [OBSOLETE] Use <see cref="Pause(string,float)"/>. </summary>
        [Obsolete("Use SoundsGoodManager.Pause(id, fadeOutTime) instead. " +
                  "This method will be removed in a future version.")]
        public static void PauseMusic (string id, float fadeOutTime = 0) => Pause(id, fadeOutTime);
        
        /// <summary> [OBSOLETE] Use <see cref="PauseAll(float)"/>. </summary>
        [Obsolete("Use SoundsGoodManager.PauseAll(fadeOutTime) instead. " +
                  "This method will be removed in a future version.")]
        public static void PauseAllSounds (float fadeOutTime = 0) => PauseAll(fadeOutTime);
        
        /// <summary> [OBSOLETE] Use <see cref="PauseAll(float)"/>. </summary>
        [Obsolete("Use SoundsGoodManager.PauseAll(fadeOutTime) instead. " +
                  "This method will be removed in a future version.")]
        public static void PauseAllMusic (float fadeOutTime = 0) => PauseAll(fadeOutTime);
    }

    public partial class SoundsGoodManager // Private Methods
    {
        private static SoundsGoodAudioSource GetSoundSourceFromPool ()
        {
            if (audioSourcesPoolParent == null || !audioSourcesPoolParent.activeInHierarchy)
            {
                audioSourcesPoolParent = new GameObject("Sources Pool Parent");
                DontDestroyOnLoad(audioSourcesPoolParent);
            }

            if (audioSourcePool.Count != audioSourcesPoolParent.transform.childCount)
            {
                audioSourcePool.Clear();
            }
            
            foreach (SoundsGoodAudioSource element in audioSourcePool)
            {
                if (!element.Using) return element;
            }

            GameObject newSourceInstance = new GameObject($"Audio Source {audioSourcePool.Count}");
            DontDestroyOnLoad(newSourceInstance);
            newSourceInstance.transform.SetParent(audioSourcesPoolParent.transform);

            AudioSource source = newSourceInstance.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0;
            SoundsGoodAudioSource soundsGoodAudioSourceElement = newSourceInstance.AddComponent<SoundsGoodAudioSource>().Init(source);

            audioSourcePool.Add(soundsGoodAudioSourceElement);
            return soundsGoodAudioSourceElement;
        }
        
        private static AudioMixerGroup GetOutput (string outputName)
        {
            if (AssetLocator.Instance.OutputDataCollection == null) return null;
            
            AudioMixerGroup audioMixerGroup = AssetLocator.Instance.OutputDataCollection.GetOutput(outputName);

            if (audioMixerGroup == null) return null;
            
            float lastSavedVolume = GetSavedOutputVolume(outputName);
            SetAudioMixerLinearVolume(audioMixerGroup.audioMixer, outputName, lastSavedVolume);
            return audioMixerGroup;
        }
        
        private static void SetAudioMixerLinearVolume (AudioMixer audioMixer, string volumeParameterName, float volume)
        {
            audioMixer.SetFloat(volumeParameterName, 
                Mathf.Log10(Mathf.Clamp(volume, 0.001f, 0.99f)) * 20);
        }
    }
}

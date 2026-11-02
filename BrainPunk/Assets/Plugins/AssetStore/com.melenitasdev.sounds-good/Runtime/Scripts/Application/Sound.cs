/*
 * All rights to the Sounds Good plugin, © Created by Melenitas Dev, are reserved.
 * Distribution of the standalone asset is strictly prohibited.
 */
using System;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

namespace MelenitasDev.SoundsGood
{
    public partial class Sound // Fields
    {
        private SoundsGoodAudioSource soundsGoodAudioSource;

        private float volume = 1;
        private float minHearDistance = 3;
        private float maxHearDistance = 500;
        private AudioRolloffMode audioRolloffMode;
        private AnimationCurve customVolumeCurve;
        private float pitch = 1;
        private float dopplerLevel = 1;
        private Vector2 pitchRange = new Vector2(0.85f, 1.15f);
        private string id = null;
        private Vector3 position = Vector3.zero;
        private Transform followTarget = null;
        private bool loop = false;
        private bool spatialSound = true;
        private float fadeOutTime = 0;
        private bool randomClip = true;
        private int clipIndex = -1;
        private float playProbability = 1;
        private bool forgetSourcePoolOnStop = false;
        private AudioClip clip = null;
        private AudioMixerGroup output = null;
        private string cachedSoundTag;
    }

    public partial class Sound // Fields (Callbacks)
    {
        private Action onPlay;
        private Action onComplete;
        private Action onLoopCycleComplete;
        private Action onPause;
        private Action onPauseComplete;
        private Action onResume;
    }

    public partial class Sound // Properties
    {
        /// <summary>It's true when it's being used. When it's paused, it's true as well</summary>
        public bool Using => soundsGoodAudioSource != null;
        /// <summary>It's true when audio is playing.</summary>
        public bool Playing => Using && soundsGoodAudioSource.Playing;
        /// <summary>It's true when audio paused (it ignore the fade out time).</summary>
        public bool Paused => Using && soundsGoodAudioSource.Paused;
        /// <summary>Volume level between [0,1].</summary>
        public float Volume => volume;
        /// <summary>Clip index in Sound clips array. Returns -1 when isn't reproducing a specific clip.</summary>
        public int ClipIndex => clipIndex;
        /// <summary>Total time in seconds that it have been playing.</summary>
        public float PlayingTime => Using ? soundsGoodAudioSource.PlayingTime : 0;
        /// <summary>Reproduced time in seconds of current loop cycle.</summary>
        public float CurrentLoopCycleTime => Using ? soundsGoodAudioSource.CurrentLoopCycleTime : 0;
        /// <summary>Times it has looped.</summary>
        public int CompletedLoopCycles => Using ? soundsGoodAudioSource.CompletedLoopCycles : 0;
        /// <summary>Duration in seconds of matched clip.</summary>
        public float ClipDuration => clip != null ? clip.length : 0;
        /// <summary>Matched clip.</summary>
        public AudioClip Clip => clip;
    }

    public partial class Sound // Public Methods
    {
        /// <summary>
        /// Create new Sound object.
        /// </summary>
        public Sound () { }
        
        /// <summary>
        /// Create new Sound object given a clip.
        /// </summary>
        /// <param name="sfx">Sound you've created before on Audio Creator window</param>
        public Sound (SFX sfx)
        {
            cachedSoundTag = sfx.ToString();
        }
        
        /// <summary>
        /// Create new Sound object given a tag.
        /// </summary>
        /// <param name="tag">The tag you've used to create the sound on Audio Creator window</param>
        public Sound (string tag)
        {
            cachedSoundTag = tag;
        }

        /// <summary>
        /// Store volume parameters BEFORE play sound.
        /// </summary>
        /// <param name="volume">Volume: min 0, Max 1</param>
        public Sound SetVolume (float volume)
        {
            this.volume = volume;
            return this;
        }
        
        /// <summary>
        /// Store volume parameters BEFORE play sound.
        /// </summary>
        /// <param name="volume">Volume: min 0, Max 1</param>
        /// <param name="hearDistance">Distance range to hear sound</param>
        [Obsolete("This method has been deprecated. If you need to change the hear distance, " +
                  "use the method SetHearDistance(float minHearDistance, float maxHearDistance) instead.")]
        public Sound SetVolume (float volume, Vector2 hearDistance)
        {
            this.volume = volume;
            minHearDistance = hearDistance.x;
            maxHearDistance = hearDistance.y;
            return this;
        }
        
        /// <summary>
        /// Sets the minimum and maximum hearing distances for the AudioSource.
        /// Sounds will start to fade in at the maximum distance and be fully audible until the minimum distance is reached.
        /// </summary>
        /// <param name="minHearDistance">Distance at which the sound be fully audible.</param>
        /// <param name="maxHearDistance">Distance at which the sound starts becoming audible.</param>
        public Sound SetHearDistance (float minHearDistance, float maxHearDistance)
        {
            this.minHearDistance = minHearDistance;
            this.maxHearDistance = maxHearDistance;
            return this;
        }

        /// <summary>
        /// Sets how the sound volume fades over distance using one of the predefined curve types.
        /// </summary>
        /// <param name="Logarithmic">fades more naturally, similar to real-world sounds.</param>
        /// <param name="Linear">fades at a steady, constant rate.</param>
        public Sound SetVolumeRolloffCurve (VolumeRolloffCurve volumeRolloffCurve)
        {
            audioRolloffMode = volumeRolloffCurve switch
            {
                VolumeRolloffCurve.Logarithmic => AudioRolloffMode.Logarithmic,
                VolumeRolloffCurve.Linear => AudioRolloffMode.Linear,
                _ => AudioRolloffMode.Logarithmic
            };
            return this;
        }
        
        /// <summary>
        /// Sets a custom curve that controls how the sound volume fades with distance.  
        /// Use this if you want full control over how the fade behaves.
        /// </summary>
        /// <param name="customVolumeCurve">
        /// An AnimationCurve that defines how the sound volume decreases as the listener moves away.</param>
        public Sound SetCustomVolumeRolloffCurve (AnimationCurve customVolumeCurve)
        {
            audioRolloffMode = AudioRolloffMode.Custom;
            this.customVolumeCurve = customVolumeCurve;
            return this;
        }

        /// <summary>
        /// Change volume while sound is reproducing.
        /// </summary>
        /// <param name="newVolume">New volume: min 0, Max 1</param>
        /// <param name="lerpTime">Time to lerp current to new volume</param>
        public void ChangeVolume (float newVolume, float lerpTime = 0)
        {
            if (volume == newVolume) return;
            
            volume = newVolume;
            
            if (!Using) return;
            
            soundsGoodAudioSource.SetVolume(newVolume, lerpTime);
        }

        /// <summary>
        /// Set given pitch. Make your sounds sound different :)
        /// </summary>
        public Sound SetPitch (float pitch)
        {
            this.pitch = pitch;
            return this;
        }
        
        /// <summary>
        /// Set my recommended random pitch. Range is (0.85, 1.15). It's useful to avoid sounds be repetitive.
        /// </summary>
        public Sound SetRandomPitch ()
        {
            pitch = Random.Range(0.85f, 1.15f);
            return this;
        }
        
        /// <summary>
        /// Set random pitch between given range. It's useful to avoid sounds be repetitive.
        /// </summary>
        /// <param name="pitchRange">Pitch range (min, Max)</param>
        public Sound SetRandomPitch (Vector2 pitchRange)
        {
            pitch = Random.Range(pitchRange.x, pitchRange.y);
            return this;
        }

        /// <summary>
        /// Sets how strongly the Doppler effect is applied to the sound when the listener or sound source is moving.
        /// </summary>
        /// <param name="dopplerLevel">Value between 0 and 5; 1 is the default and recommended for realistic results.</param>
        public Sound SetDopplerLevel (float dopplerLevel)
        {
            this.dopplerLevel = Mathf.Clamp(dopplerLevel, 0, 5);
            return this;
        }

        /// <summary>
        /// Set an id to identify this sound on AudioManager static methods.
        /// </summary>
        public Sound SetId (string id)
        {
            this.id = id;
            return this;
        }

        /// <summary>
        /// Make your sound loops for infinite time. If you need to stop it, use Stop() method.
        /// </summary>
        public Sound SetLoop (bool loop)
        {
            this.loop = loop;
            return this;
        }
        
        /// <summary>
        /// Change the AudioClip of this Sound BEFORE play it.
        /// </summary>
        /// <param name="tag">The tag you've used to create the sound on Audio Creator</param>
        public Sound SetClip (string tag)
        {
            cachedSoundTag = tag;
            clip = SoundsGoodManager.GetSFX(tag);
            return this;
        }
        
        /// <summary>
        /// Change the AudioClip of this Sound BEFORE play it.
        /// </summary>
        /// <param name="sfx">Sound you've created before on Audio Creator</param>
        public Sound SetClip (SFX sfx)
        {
            SetClip(sfx.ToString());
            return this;
        }
        
        /// <summary>
        /// Make the sound clip change with each new Play().
        /// It'll choose a random sound from those you have added with the same tag in the Audio Creator.
        /// </summary>
        /// <param name="random">Use random clip</param>
        public Sound SetRandomClip (bool random)
        {
            randomClip = random;
            return this;
        }

        /// <summary>
        /// Set a specific clip using the index in clips you have added with the same tag in the Audio Creator.
        /// Useful to reproduce clips in a specific order.
        /// </summary>
        /// <param name="index">Index in Sound clips array that you've created in the Audio Creator</param>
        public Sound SetClipByIndex (int index)
        {
            if (index < 0)
            {
                Debug.LogWarning("Clip index can't be lower than 0");
                return this;
            }

            if (string.IsNullOrEmpty(cachedSoundTag))
            {
                Debug.LogWarning("You need to set a Sound before selecting one of its clips.");
                return this;
            }
            
            clipIndex = index;
            clip = SoundsGoodManager.GetSFX(cachedSoundTag, index);
            SetRandomClip(false);
            return this;
        }

        /// <summary>
        /// Sets the probability (0 to 1) that this sound will play when Play() is called.
        /// Useful for adding random variation (e.g., footsteps with a chance of creaking wood).
        /// </summary>
        /// <param name="playProbability">A value between 0 (never plays) and 1 (always plays).</param>
        public Sound SetPlayProbability (float playProbability)
        {
            this.playProbability = Mathf.Clamp01(playProbability);
            return this;
        }
        
        /// <summary>
        /// Set the position of the sound emitter.
        /// </summary>
        public Sound SetPosition (Vector3 position)
        {
            this.position = position;
            return this;
        }
        
        /// <summary>
        /// Set a target to follow. Audio source will update its position every frame.
        /// </summary>
        /// <param name="followTarget">Transform to follow</param>
        public Sound SetFollowTarget (Transform followTarget)
        {
            this.followTarget = followTarget;
            return this;
        }

        /// <summary>
        /// Set spatial sound.
        /// </summary>
        /// <param name="true">Your sound will be 3D</param>
        /// <param name="false">Your sound will be global / 2D</param>
        public Sound SetSpatialSound (bool activate = true)
        {
            spatialSound = activate;
            return this;
        }
        
        /// <summary>
        /// Set fade out duration. It'll be used when sound ends.
        /// </summary>
        /// <param name="fadeOutTime">Seconds that fade out will last</param>
        public Sound SetFadeOut (float fadeOutTime)
        {
            this.fadeOutTime = fadeOutTime;
            return this;
        }
        
        /// <summary>
        /// Set the audio output to manage the volume using the Audio Mixers.
        /// </summary>
        /// <param name="output">Output you've created before inside Master AudioMixer
        /// (Remember reload the outputs database on Output Manager Window)</param>
        public Sound SetOutput (Output output)
        {
            this.output = SoundsGoodManager.GetOutput(output);
            return this;
        }
        
        /// <summary>
        /// Define a callback that will be invoked on sound start playing.
        /// </summary>
        /// <param name="onPlay">Method will be invoked</param>
        public Sound OnPlay (Action onPlay)
        {
            this.onPlay = onPlay;
            return this;
        }
        
        /// <summary>
        /// Define a callback that will be invoked on sound complete.
        /// If "loop" is active, it'll be called when you Stop the sound manually.
        /// </summary>
        /// <param name="onComplete">Method will be invoked</param>
        public Sound OnComplete (Action onComplete)
        {
            this.onComplete = onComplete;
            return this;
        }
        
        /// <summary>
        /// Define a callback that will be invoked on loop cycle complete.
        /// You need to set loop on true to use it.
        /// </summary>
        /// <param name="onLoopCycleComplete">Method will be invoked</param>
        public Sound OnLoopCycleComplete (Action onLoopCycleComplete)
        {
            this.onLoopCycleComplete = onLoopCycleComplete;
            return this;
        }
        
        /// <summary>
        /// Define a callback that will be invoked on sound pause.
        /// It will ignore the fade out time.
        /// </summary>
        /// <param name="onPause">Method will be invoked</param>
        public Sound OnPause (Action onPause)
        {
            this.onPause = onPause;
            return this;
        }
        
        /// <summary>
        /// Define a callback that will be invoked on sound pause and fade out ends.
        /// </summary>
        /// <param name="onPauseComplete">Method will be invoked</param>
        public Sound OnPauseComplete (Action onPauseComplete)
        {
            this.onPauseComplete = onPauseComplete;
            return this;
        }
        
        /// <summary>
        /// Define a callback that will be invoked on resume/unpause sound.
        /// </summary>
        /// <param name="onResume">Method will be invoked</param>
        public Sound OnResume (Action onResume)
        {
            this.onResume = onResume;
            return this;
        }

        /// <summary>
        /// Reproduce sound.
        /// </summary>
        /// <param name="fadeInTime">Seconds that fade in will last</param>
        public void Play (float fadeInTime = 0)
        {
            if (clip == null && string.IsNullOrEmpty(cachedSoundTag))
            {
                Debug.LogError("You need to set a clip before reproduce this");
                return;
            }
            
            if (Using && Playing && loop)
            {
                Stop();
                forgetSourcePoolOnStop = true;
            }
            
            if (randomClip || clip == null)
            {
                SetClip(cachedSoundTag);
            }
            else
            {
                if (clipIndex != -1)
                {
                    SetClipByIndex(clipIndex);
                }
            }
            
            if (Random.value > playProbability) return;

            soundsGoodAudioSource = SoundsGoodManager.GetSource();
            soundsGoodAudioSource
                .SetVolume(volume)
                .SetHearDistance(minHearDistance, maxHearDistance)
                .SetVolumeRolloffCurve(audioRolloffMode, customVolumeCurve)
                .SetPitch(pitch)
                .SetDopplerLevel(dopplerLevel)
                .SetLoop(loop)
                .SetClip(clip)
                .SetPosition(position)
                .SetFollowTarget(followTarget)
                .SetSpatialSound(spatialSound)
                .SetFadeOut(fadeOutTime)
                .SetId(id)
                .SetOutput(output)
                .OnPlay(onPlay)
                .OnComplete(onComplete)
                .OnLoopCycleComplete(onLoopCycleComplete)
                .OnPause(onPause)
                .OnPauseComplete(onPauseComplete)
                .OnResume(onResume)
                .Play(fadeInTime);
        }

        /// <summary>
        /// Pause sound.
        /// </summary>
        /// <param name="fadeOutTime">Seconds that fade out will last before pause</param>
        public void Pause (float fadeOutTime = 0)
        {
            if (!Using) return;
            
            soundsGoodAudioSource.Pause(fadeOutTime);
        }

        /// <summary>
        /// Resume/Unpause sound.
        /// </summary>
        /// <param name="fadeInTime">Seconds that fade in will last</param>
        public void Resume (float fadeInTime = 0)
        {
            if (!Using) return;
            
            soundsGoodAudioSource.Resume(fadeInTime);
        }

        /// <summary>
        /// Stop sound.
        /// </summary>
        /// <param name="fadeOutTime">Seconds that fade out will last before stop</param>
        public void Stop (float fadeOutTime = 0)
        {
            if (!Using) return;
            
            if (forgetSourcePoolOnStop)
            {
                soundsGoodAudioSource.Stop(fadeOutTime);
                soundsGoodAudioSource = null;
                forgetSourcePoolOnStop = false;
                return;
            }
            soundsGoodAudioSource.Stop(fadeOutTime, () => soundsGoodAudioSource = null);
        }
    }
}

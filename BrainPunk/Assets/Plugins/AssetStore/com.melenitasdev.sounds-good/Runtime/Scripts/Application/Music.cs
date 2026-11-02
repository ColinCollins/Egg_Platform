/*
 * All rights to the Sounds Good plugin, © Created by Melenitas Dev, are reserved.
 * Distribution of the standalone asset is strictly prohibited.
 */
using System;
using UnityEngine;
using UnityEngine.Audio;

namespace MelenitasDev.SoundsGood
{
    public partial class Music // Fields
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
        private bool spatialSound = false;
        private float fadeOutTime = 0;
        private bool randomClip = true;
        private int clipIndex = -1;
        private bool forgetSourcePoolOnStop = false;
        private AudioClip clip = null;
        private AudioMixerGroup output = null;
        private string cachedSoundTag;
    }

    public partial class Music // Fields (Callbacks)
    {
        private Action onPlay;
        private Action onComplete;
        private Action onLoopCycleComplete;
        private Action onPause;
        private Action onPauseComplete;
        private Action onResume;
    }

    public partial class Music // Properties
    {
        /// <summary>It's true when it's being used. When it's paused, it's true as well</summary>
        public bool Using => soundsGoodAudioSource != null;
        /// <summary>It's true when audio is playing.</summary>
        public bool Playing => Using && soundsGoodAudioSource.Playing;
        /// <summary>It's true when audio paused (it ignore the fade out time).</summary>
        public bool Paused => Using && soundsGoodAudioSource.Paused;
        /// <summary>Volume level between [0,1].</summary>
        public float Volume => volume;
        /// <summary>Clip index in Music clips array. Returns -1 when isn't reproducing a specific clip.</summary>
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

    public partial class Music // Public Methods
    {
        /// <summary>
        /// Create new Music object.
        /// </summary>
        public Music () { }
        
        /// <summary>
        /// Create new Music object given a Track.
        /// </summary>
        /// <param name="track">Music track you've created before on Audio Creator window</param>
        public Music (Track track)
        {
            cachedSoundTag = track.ToString();
        }
        
        /// <summary>
        /// Create new Music object given a tag.
        /// </summary>
        /// <param name="tag">The tag you've used to create the sound on Audio Creator</param>
        public Music (string tag)
        {
            cachedSoundTag = tag;
        }
        
        /// <summary>
        /// Store volume parameters BEFORE play music.
        /// </summary>
        /// <param name="volume">Volume: min 0, Max 1</param>
        public Music SetVolume (float volume)
        {
            this.volume = volume;
            return this;
        }
        
        /// <summary>
        /// Set volume parameters BEFORE play music.
        /// </summary>
        /// <param name="volume">Volume: min 0, Max 1</param>
        /// <param name="hearDistance">Distance range to hear music</param>
        [Obsolete("This method has been deprecated. If you need to change the hear distance, " +
                  "use the method SetHearDistance(float minHearDistance, float maxHearDistance) instead.")]
        public Music SetVolume (float volume, Vector2 hearDistance)
        {
            this.volume = volume;
            minHearDistance = hearDistance.x;
            maxHearDistance = hearDistance.y;
            return this;
        }
        
        /// <summary>
        /// Sets the minimum and maximum hearing distances for the AudioSource.
        /// Music will start to fade in at the maximum distance and be fully audible until the minimum distance is reached.
        /// </summary>
        /// <param name="minHearDistance">Distance at which the music be fully audible.</param>
        /// <param name="maxHearDistance">Distance at which the music starts becoming audible.</param>
        public Music SetHearDistance (float minHearDistance, float maxHearDistance)
        {
            this.minHearDistance = minHearDistance;
            this.maxHearDistance = maxHearDistance;
            return this;
        }
        
        /// <summary>
        /// Sets how the music volume fades over distance using one of the predefined curve types.
        /// </summary>
        /// <param name="Logarithmic">fades more naturally, similar to real-world sounds.</param>
        /// <param name="Linear">fades at a steady, constant rate.</param>
        public Music SetVolumeRolloffCurve (VolumeRolloffCurve volumeRolloffCurve)
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
        /// Sets a custom curve that controls how the music volume fades with distance.  
        /// Use this if you want full control over how the fade behaves.
        /// </summary>
        /// <param name="customVolumeCurve">
        /// An AnimationCurve that defines how the sound volume decreases as the listener moves away.</param>
        public Music SetCustomVolumeRolloffCurve (AnimationCurve customVolumeCurve)
        {
            audioRolloffMode = AudioRolloffMode.Custom;
            this.customVolumeCurve = customVolumeCurve;
            return this;
        }
        
        /// <summary>
        /// Change volume while music is reproducing.
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
        /// Set given pitch. Make your music sound different :)
        /// </summary>
        public Music SetPitch (float pitch)
        {
            this.pitch = pitch;
            return this;
        }
        
        /// <summary>
        /// Sets how strongly the Doppler effect is applied to the music when the listener or sound source is moving.
        /// </summary>
        /// <param name="dopplerLevel">Value between 0 and 5; 1 is the default and recommended for realistic results.</param>
        public Music SetDopplerLevel (float dopplerLevel)
        {
            this.dopplerLevel = Mathf.Clamp(dopplerLevel, 0, 5);
            return this;
        }

        /// <summary>
        /// Set an id to identify this music on AudioManager static methods.
        /// </summary>
        public Music SetId (string id)
        {
            this.id = id;
            return this;
        }
        
        /// <summary>
        /// Make your music loops for infinite time. If you need to stop it, use Stop() method.
        /// </summary>
        public Music SetLoop (bool loop)
        {
            this.loop = loop;
            return this;
        }
        
        /// <summary>
        /// Change the AudioClip of this Music.
        /// </summary>
        /// <param name="tag">The tag you've used to save the music track on Audio Creator window</param>
        public Music SetClip (string tag)
        {
            cachedSoundTag = tag;
            clip = SoundsGoodManager.GetTrack(tag);
            return this;
        }
        
        /// <summary>
        /// Set a new track BEFORE play it.
        /// </summary>
        /// <param name="track">Music track you've created before on Audio Creator window</param>
        public Music SetClip (Track track)
        {
            SetClip(track.ToString());
            return this;
        }
        
        /// <summary>
        /// Make the music clip change with each new Play().
        /// A random clip from those you have added together in the Audio Creator will be played.
        /// </summary>
        /// <param name="random">Use random clip</param>
        public Music SetRandomClip (bool random)
        {
            randomClip = random;
            return this;
        }

        /// <summary>
        /// Set a specific clip using the index in clips you have added with the same tag in the Audio Creator.
        /// Useful to reproduce clips in a specific order.
        /// </summary>
        /// <param name="index">Index in Music clips array that you've created in the Audio Creator</param>
        public Music SetClipByIndex (int index)
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
        /// Set the position of the sound emitter.
        /// </summary>
        public Music SetPosition (Vector3 position)
        {
            this.position = position;
            return this;
        }
        
        /// <summary>
        /// Set a target to follow. Audio source will update its position every frame.
        /// </summary>
        /// <param name="followTarget">Transform to follow</param>
        public Music SetFollowTarget (Transform followTarget)
        {
            this.followTarget = followTarget;
            return this;
        }
        
        /// <summary>
        /// Set spatial sound.
        /// </summary>
        /// <param name="true">Your sound will be 3D</param>
        /// <param name="false">Your sound will be global / 2D</param>
        public Music SetSpatialSound (bool activate = true)
        {
            spatialSound = activate;
            return this;
        }
        
        /// <summary>
        /// Set fade out duration. It'll be used when music ends.
        /// </summary>
        /// <param name="fadeOutTime">Seconds that fade out will last</param>
        public Music SetFadeOut (float fadeOutTime)
        {
            this.fadeOutTime = fadeOutTime;
            return this;
        }
        
        /// <summary>
        /// Set the audio output to manage the volume using the Audio Mixers.
        /// </summary>
        /// <param name="output">Output you've created before inside Master AudioMixer
        /// (Remember reload the outputs database on Output Manager Window)</param>
        public Music SetOutput (Output output)
        {
            this.output = SoundsGoodManager.GetOutput(output);
            return this;
        }
        
        /// <summary>
        /// Define a callback that will be invoked on music start playing.
        /// </summary>
        /// <param name="onPlay">Method will be invoked</param>
        public Music OnPlay (Action onPlay)
        {
            this.onPlay = onPlay;
            return this;
        }
        
        /// <summary>
        /// Define a callback that will be invoked on music complete.
        /// If "loop" is active, it'll be called when you Stop the music manually.
        /// </summary>
        /// <param name="onComplete">Method will be invoked</param>
        public Music OnComplete (Action onComplete)
        {
            this.onComplete = onComplete;
            return this;
        }
        
        /// <summary>
        /// Define a callback that will be invoked on loop cycle complete.
        /// You need to set loop on true to use it.
        /// </summary>
        /// <param name="onLoopCycleComplete">Method will be invoked</param>
        public Music OnLoopCycleComplete (Action onLoopCycleComplete)
        {
            this.onLoopCycleComplete = onLoopCycleComplete;
            return this;
        }
        
        /// <summary>
        /// Define a callback that will be invoked on music pause.
        /// It will ignore the fade out time.
        /// </summary>
        /// <param name="onPause">Method will be invoked</param>
        public Music OnPause (Action onPause)
        {
            this.onPause = onPause;
            return this;
        }
        
        /// <summary>
        /// Define a callback that will be invoked on music pause and fade out ends.
        /// </summary>
        /// <param name="onPauseComplete">Method will be invoked</param>
        public Music OnPauseComplete (Action onPauseComplete)
        {
            this.onPauseComplete = onPauseComplete;
            return this;
        }
        
        /// <summary>
        /// Define a callback that will be invoked on resume/unpause music.
        /// </summary>
        /// <param name="onResume">Method will be invoked</param>
        public Music OnResume (Action onResume)
        {
            this.onResume = onResume;
            return this;
        }

        /// <summary>
        /// Reproduce music.
        /// </summary>
        /// <param name="fadeInTime">Seconds that fade in will last</param>
        public void Play (float fadeInTime = 0)
        {
            if (clip == null && string.IsNullOrEmpty(cachedSoundTag))
            {
                Debug.LogError("You need to set a clip before reproduce this");
                return;
            }
            
            if (Using && Playing)
            {
                Stop();
                forgetSourcePoolOnStop = true;
            }
            
            if (clip == null || randomClip) SetClip(cachedSoundTag);
            
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
        /// Pause music.
        /// </summary>
        /// <param name="fadeOutTime">Seconds that fade out will last before pause</param>
        public void Pause (float fadeOutTime = 0)
        {
            if (!Using) return;
            
            soundsGoodAudioSource.Pause(fadeOutTime);
        }

        /// <summary>
        /// Resume/Unpause music.
        /// </summary>
        /// <param name="fadeInTime">Seconds that fade in will last</param>
        public void Resume (float fadeInTime = 0)
        {
            if (!Using) return;
            
            soundsGoodAudioSource.Resume(fadeInTime);
        }

        /// <summary>
        /// Stop music.
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
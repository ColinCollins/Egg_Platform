using System;
using UnityEngine;
using YooAsset;

namespace GF
{
    /// <summary>
    /// Audio Agent
    /// </summary>
    public class AudioAgent
    {
        private GameObject _root;
        private AudioSource _audioSource;
        public SoundType soundType;
        public PlayState playState;
        public string path;
        private Action<AudioSource> _onComplete;

        public AudioAgent(GameObject root,SoundType type)
        {
            _root = root;
            soundType = type;
            playState = PlayState.Free;
            _audioSource = _root.AddComponent<AudioSource>();
        }

        /// <summary>
        /// 设置音量
        /// </summary>
        /// <param name="value"></param>
        public void SetVolume(float value)
        {
            if (_audioSource != null)
            {
                _audioSource.volume = value;
            }
        }

        /// <summary>
        /// 设置静音
        /// </summary>
        /// <param name="mute"></param>
        public void SetMute(bool mute)
        {
            if (_audioSource != null)
            {
                _audioSource.mute = mute;
            }
        }

        /// <summary>
        /// 设置pitch
        /// </summary>
        /// <param name="value"></param>
        public void SetPitch(float value)
        {
            if (_audioSource != null)
            {
                _audioSource.pitch = value;
            }
        }

        public void OnUpdate()
        {
            if (_audioSource != null)
            {
                if (playState == PlayState.Playing && !_audioSource.isPlaying)
                {
                    playState = PlayState.Complete;
                    _onComplete?.Invoke(_audioSource);
                }

                if (playState == PlayState.Stop || playState == PlayState.Complete)
                {
                    playState = PlayState.Free;
                }
            }
        }

        /// <summary>
        /// 播放音频
        /// </summary>
        /// <param name="path"></param>
        /// <param name="isLoop"></param>
        /// <param name="volume"></param>
        /// <param name="isMute"></param>
        /// <param name="complete"></param>
        public void PlaySound(string path, bool isLoop, float volume,bool isMute, Action<AudioSource> complete=null, string packageName = null)
        {
            this.path = path;
            playState = PlayState.Playing;
            AudioClip clip = App.Res.LoadAsset<AudioClip>(path, path, packageName);
            
            if (clip == null)
            {
                LogKit.E("加载音效失败："+path);
                playState = PlayState.Stop;
                return;
            }

            _audioSource.clip = clip;
            _audioSource.loop = isLoop;
            _audioSource.volume = volume;
            _audioSource.mute = isMute;
            _audioSource.Play();
            _onComplete = complete;
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        public void Stop()
        {
            playState = PlayState.Stop;
            if (_audioSource != null)
            {
                _audioSource.Stop();
            }
        }
        
        /// <summary>
        /// 暂停播放
        /// </summary>
        public void Pause()
        {
            playState = PlayState.Pause;
            if (_audioSource != null)
            {
                _audioSource.Pause();
            }
        }

        /// <summary>
        /// 继续播放
        /// </summary>
        public void Resume()
        {
            playState = PlayState.Playing;
            if (_audioSource != null)
            {
                _audioSource.UnPause();
            }
        }
        
        public AudioSource GetAudioSource()
        {
            return _audioSource;
        }

        /// <summary>
        /// 释放agent
        /// </summary>
        public void Destroy()
        {
            if (playState == PlayState.Playing || playState == PlayState.Pause)
            {
                // AudioService.m_loader.UnloadSound(path);
                //todo:卸载
            }
            
            if (_audioSource != null)
            {
                GameObject.Destroy(_audioSource);
            }
        }
    }
    
    /// <summary>
    /// 播放状态
    /// </summary>
    public enum PlayState
    {
        Free,
        Playing,
        Pause,
        Stop,
        Complete
    }
}
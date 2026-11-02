using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using UnityEngine;

namespace GF
{
    public partial class AudioKit
    {
        private GameObject _root;
        private AudioListener _listener;
        
        private List<AudioAgent> _bgmAgents = new List<AudioAgent>();
        private List<AudioAgent> _voiceAgents = new List<AudioAgent>();
        private List<AudioAgent> _uiAgents = new List<AudioAgent>();
        private List<AudioAgent> _sceneAgents = new List<AudioAgent>();
        

        #region Volume
        
        private float _bgmVolume = DefaultBGMVolume;
        /// <summary>
        /// bgm音量
        /// </summary>
        public float BGMVolume
        {
            get
            {
                return _bgmVolume;
            }
            set
            {
                _bgmVolume = value;
                foreach (AudioAgent agent in _bgmAgents)
                {
                    agent.SetVolume(_bgmVolume);
                }
                
                App.LocalStorage.SetData(BGMVolumeKey, _bgmVolume);
            }
        }

        private float _voiceVolume = DefaultVoiceVolume;
        /// <summary>
        /// voice音量
        /// </summary>
        public float VoiceVolume
        {
            get
            {
                return _voiceVolume;
            }
            set
            {
                _voiceVolume = value;
                foreach (AudioAgent agent in _voiceAgents)
                {
                    agent.SetVolume(_voiceVolume);
                }
                App.LocalStorage.SetData(VoiceVolumeKey, _voiceVolume);
            }
        }
        private float _uiVolume = DefaultUIVolume;
        /// <summary>
        /// ui音量
        /// </summary>
        public float UIVolume
        {
            get
            {
                return _uiVolume;
            }
            set
            {
                _uiVolume = value;
                foreach (AudioAgent agent in _uiAgents)
                {
                    agent.SetVolume(_uiVolume);
                }
                App.LocalStorage.SetData(UIVolumeKey, _uiVolume);
            }
        }
        private float _sceneVolume = DefaultSceneVolume;
        /// <summary>
        /// scene音量
        /// </summary>
        public float SceneVolume
        {
            get
            {
                return _sceneVolume;
            }
            set
            {
                _sceneVolume = value;
                foreach (AudioAgent agent in _sceneAgents)
                {
                    agent.SetVolume(_sceneVolume);
                }
                App.LocalStorage.SetData(SceneVolumeKey, _sceneVolume);
            }
        }
        #endregion

        #region Mute
        
        private bool m_bgmMute = false;
        /// <summary>
        /// bgm静音
        /// </summary>
        public bool BGMMute
        {
            get
            {
                return m_bgmMute;
            }
            set
            {
                m_bgmMute = value;
                foreach (AudioAgent agent in _bgmAgents)
                {
                    agent.SetMute(m_bgmMute);
                }

                App.LocalStorage.SetData(BGMMuteKey, m_bgmMute ? 1 : 0);
            }
        }
        
        private bool m_voiceMute = false;
        /// <summary>
        /// voice静音
        /// </summary>
        public bool VoiceMute
        {
            get
            {
                return m_voiceMute;
            }
            set
            {
                m_voiceMute = value;
                foreach (AudioAgent agent in _voiceAgents)
                {
                    agent.SetMute(m_voiceMute);
                }
                
                App.LocalStorage.SetData(VoiceMuteKey, m_voiceMute ? 1 : 0);
            }
        }
        
        private bool m_uiMute = false;
        /// <summary>
        /// ui静音
        /// </summary>
        public bool UIMute
        {
            get
            {
                return m_uiMute;
            }
            set
            {
                m_uiMute = value;
                foreach (AudioAgent agent in _uiAgents)
                {
                    agent.SetMute(m_uiMute);
                }
                
                App.LocalStorage.SetData(UIMuteKey, m_uiMute ? 1 : 0);
            }
        }
        
        private bool m_sceneMute = false;
        /// <summary>
        /// scene静音
        /// </summary>
        public bool SceneMute
        {
            get
            {
                return m_sceneMute;
            }
            set
            {
                m_sceneMute = value;
                foreach (AudioAgent agent in _sceneAgents)
                {
                    agent.SetMute(m_sceneMute);
                }
                
                App.LocalStorage.SetData(SceneMuteKey, m_sceneMute ? 1 : 0);
            }
        }
        
        #endregion

        /// <summary>
        /// 初始化音频工具
        /// </summary>
        public void Init()
        {
            _root = new GameObject("[Audio]");
            GameObject.DontDestroyOnLoad(_root);
            
            _listener=MonoBehaviour.FindObjectOfType<AudioListener>();
            if (!_listener)
            {
                _listener = _root.AddComponent<AudioListener>();
            }

            for (int i = 0; i < BGMMaxCount; i++)
            {
                AudioAgent agent=new AudioAgent(_root,SoundType.BGM);
                _bgmAgents.Add(agent);
            }

            for (int i = 0; i < VoiceMaxCount; i++)
            {
                AudioAgent agent=new AudioAgent(_root,SoundType.Voice);
                _voiceAgents.Add(agent);
            }
            
            for (int i = 0; i < UIMaxCount; i++)
            {
                AudioAgent agent=new AudioAgent(_root,SoundType.UI);
                _uiAgents.Add(agent);
            }
            
            for (int i = 0; i < SceneMaxCount; i++)
            {
                AudioAgent agent=new AudioAgent(_root,SoundType.Scene);
                _sceneAgents.Add(agent);
            }

            float bgmVol = App.LocalStorage.GetData(BGMVolumeKey, 1);
            float voiceVol = App.LocalStorage.GetData(VoiceVolumeKey, 1);
            float uiVol = App.LocalStorage.GetData(UIVolumeKey, 1);
            float sceneVol = App.LocalStorage.GetData(SceneVolumeKey, 1);
            BGMVolume = bgmVol;
            VoiceVolume = voiceVol;
            UIVolume = uiVol;
            SceneVolume = sceneVol;

            bool bgmMute = App.LocalStorage.GetData(BGMMuteKey, 0) == 1;
            bool voiceMute = App.LocalStorage.GetData(VoiceMuteKey, 0) == 1;
            bool uiMute = App.LocalStorage.GetData(UIMuteKey, 0) == 1;
            bool sceneMute = App.LocalStorage.GetData(SceneMuteKey, 0) == 1;
            BGMMute = bgmMute;
            VoiceMute = voiceMute;
            UIMute = uiMute;
            SceneMute = sceneMute;
        }

        /// <summary>
        /// 释放音频工具
        /// </summary>
        public void Destroy()
        {
            foreach (AudioAgent agent in _bgmAgents)
            {
                agent.Destroy();
            }
            _bgmAgents.Clear();
            
            foreach (AudioAgent agent in _voiceAgents)
            {
                agent.Destroy();
            }
            _voiceAgents.Clear();
            
            foreach (AudioAgent agent in _uiAgents)
            {
                agent.Destroy();
            }
            _uiAgents.Clear();
            
            foreach (AudioAgent agent in _sceneAgents)
            {
                agent.Destroy();
            }
            _sceneAgents.Clear();
        }

        #region Sound
        

        /// <summary>
        /// 播放音频
        /// </summary>
        /// <param name="path">音频路径</param>
        /// <param name="type">类型</param>
        /// <param name="isLoop">是否循环播放</param>
        /// <param name="onComplete">播放完成回调</param>
        /// <returns>音频channel</returns>
        public int PlaySound(string path, SoundType type, bool isLoop=false, Action<AudioSource> onComplete = null, string packageName = null)
        {
            var tuple = PopFreeAgent(type);
            tuple.Item1.PlaySound(path, isLoop, GetVolByType(type), GetMuteByType(type), onComplete, packageName);
            return tuple.Item2;
        }
        
        /// <summary>
        /// 停止播放
        /// </summary>
        /// <param name="channel">音频channel</param>
        /// <param name="type">音频类型</param>
        public void StopSound(int channel,SoundType type)
        {
            var agents = GetListByType(type);
            if (agents.Count > channel)
            {
                if (agents[channel].playState != PlayState.Stop)
                {
                    agents[channel].Stop();
                }
            }
            else
            {
                LogKit.W("不存在该channel："+channel);
            }
        }

        /// <summary>
        /// 暂停播放音频
        /// </summary>
        /// <param name="channel">音频channel</param>
        /// <param name="type">音频类型</param>
        public void PauseSound(int channel,SoundType type)
        {
            var agents = GetListByType(type);
            if (agents.Count > channel)
            {
                if (agents[channel].playState == PlayState.Playing)
                {
                    agents[channel].Pause();
                }
            }
            else
            {
                LogKit.W("该音频不处于活动状态："+channel);
            }
        }

        /// <summary>
        /// 继续播放
        /// </summary>
        /// <param name="channel">音频channel</param>
        /// <param name="type">音频类型</param>
        public void ResumeSound(int channel,SoundType type)
        {
            var agents = GetListByType(type);
            if (agents.Count > channel)
            {
                if (agents[channel].playState == PlayState.Pause)
                {
                    agents[channel].Resume();
                }
            }
            else
            {
                LogKit.W("该音频不处于活动状态："+channel);
            }
        }
        
        #endregion


        public void Update()
        {
            foreach (AudioAgent agent in _bgmAgents)
            {
                agent.OnUpdate();
            }
            
            foreach (AudioAgent agent in _voiceAgents)
            {
                agent.OnUpdate();
            }
            
            foreach (AudioAgent agent in _uiAgents)
            {
                agent.OnUpdate();
            }
            
            foreach (AudioAgent agent in _sceneAgents)
            {
                agent.OnUpdate();
            }
        }
        #region Inner

        /// <summary>
        /// 获取一个空闲的音频agent
        /// </summary>
        /// <param name="type">音频类型</param>
        /// <returns>返回agent，channel</returns>
        private (AudioAgent,int) PopFreeAgent(SoundType type)
        {
            AudioAgent agent = null;
            int channel = -1;
            var agents = GetListByType(type);
            float time = int.MinValue;
            int fallbackIndex = -1;
            for (int i = 0; i < agents.Count; i++)
            {
                AudioSource audioSource = agents[i].GetAudioSource();
                if (audioSource.time > time)
                {
                    time = audioSource.time;
                    fallbackIndex = i;
                }

                if (agents[i].playState == PlayState.Free)
                {
                    agent = agents[i];
                    channel = i;
                    break;
                }
            }

            if (agent == null && fallbackIndex != -1)
            {
                agent = agents[fallbackIndex];
                agent.Stop();
                channel = fallbackIndex;
                LogKit.W("强制停止播放器："+agents.Count);
            }

            return (agent, channel);
        }

        /// <summary>
        /// 获取对应类型所有的的agent
        /// </summary>
        /// <param name="type">音频类型</param>
        /// <returns>agent列表</returns>
        private List<AudioAgent> GetListByType(SoundType type)
        {
            List<AudioAgent> agents = null;
            switch (type)
            {
                case SoundType.BGM:
                    agents = _bgmAgents;
                    break;
                case SoundType.Voice:
                    agents = _voiceAgents;
                    break;
                case SoundType.UI:
                    agents = _uiAgents;
                    break;
                case SoundType.Scene:
                    agents = _sceneAgents;
                    break;
            }

            return agents;
        }
        
        /// <summary>
        /// 获取对应类型的音量
        /// </summary>
        /// <param name="type">音频类型</param>
        /// <returns>音量大小</returns>
        private float GetVolByType(SoundType type)
        {
            float vol = 1;
            switch (type)
            {
                case SoundType.BGM:
                    vol = _bgmVolume;
                    break;
                case SoundType.Voice:
                    vol = _voiceVolume;
                    break;
                case SoundType.UI:
                    vol = _uiVolume;
                    break;
                case SoundType.Scene:
                    vol = _sceneVolume;
                    break;
            }

            return vol;
        }
        
        /// <summary>
        /// 获取对应类型是否静音
        /// </summary>
        /// <param name="type">音频类型</param>
        /// <returns>是否静音</returns>
        private bool GetMuteByType(SoundType type)
        {
            bool mute = false;
            switch (type)
            {
                case SoundType.BGM:
                    mute = m_bgmMute;
                    break;
                case SoundType.Voice:
                    mute = m_voiceMute;
                    break;
                case SoundType.UI:
                    mute = m_uiMute;
                    break;
                case SoundType.Scene:
                    mute = m_sceneMute;
                    break;
            }

            return mute;
        }

        #endregion
    }

    /// <summary>
    /// 音频类型
    /// </summary>
    public enum SoundType
    {
        Voice,
        UI,
        Scene,
        BGM
    }
}
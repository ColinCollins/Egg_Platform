using UnityEngine;
using UnityEngine.UI;
using Bear.SaveModule;
using Bear.EventSystem;
using Game.Events;

public partial class GameSettingContent : MonoBehaviour, IAutoUIBind, IEventSender
{
    private GameSetting _gameSetting;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        InitToggles();
    }

    #region Toggles
    private void InitToggles()
    {
        // 获取 GameSetting 实例
        _gameSetting = DBManager.Instance.Get<GameSetting>();
        if (_gameSetting == null)
        {
            Debug.LogError("[GameSettingContent] Failed to get GameSetting instance!");
            return;
        }

        // 初始化音乐开关
        if (MusicToggle != null)
        {
            // 先移除监听，避免触发回调
            MusicToggle.onValueChanged.RemoveAllListeners();
            // 设置初始值
            MusicToggle.isOn = _gameSetting.MusicOn;
            // 添加监听
            MusicToggle.onValueChanged.AddListener(OnMusicToggleChanged);
        }

        // 初始化音效开关
        if (SfxToggle != null)
        {
            SfxToggle.onValueChanged.RemoveAllListeners();
            SfxToggle.isOn = _gameSetting.SfxOn;
            SfxToggle.onValueChanged.AddListener(OnSfxToggleChanged);
        }

        // 初始化震动开关
        if (VibrationToggle != null)
        {
            VibrationToggle.onValueChanged.RemoveAllListeners();
            VibrationToggle.isOn = _gameSetting.VibrationOn;
            VibrationToggle.onValueChanged.AddListener(OnVibrationToggleChanged);
        }
    }

    private void OnMusicToggleChanged(bool isOn)
    {
        if (_gameSetting != null)
        {
            _gameSetting.MusicOn = isOn;
            _gameSetting.Save();

            this.DispatchEvent(Witness<MusicToggleEvent>._, isOn);
        }
    }

    private void OnSfxToggleChanged(bool isOn)
    {
        if (_gameSetting != null)
        {
            _gameSetting.SfxOn = isOn;
            _gameSetting.Save();

            this.DispatchEvent(Witness<SfxToggleEvent>._, isOn);
        }
    }

    private void OnVibrationToggleChanged(bool isOn)
    {
        if (_gameSetting != null)
        {
            _gameSetting.VibrationOn = isOn;
            _gameSetting.Save();

            this.DispatchEvent(Witness<VibrationToggleEvent>._, isOn);
        }
    }
    #endregion
}

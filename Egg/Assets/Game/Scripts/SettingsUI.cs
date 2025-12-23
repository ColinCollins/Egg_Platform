using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    [Header("UI Components")]
    public CanvasGroup canvasGroup;
    public Button closeButton;
    public Slider masterVolumeSlider;
    public Slider sfxVolumeSlider;

    private bool isVisible = false;

    void Start()
    {
        // 初始化UI状态
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        // 绑定按钮事件
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseSettings);
        }

        // 初始化音量滑块
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        // 初始化音效滑块
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }
    }

    public void ShowSettings()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            isVisible = true;
        }
    }

    public void CloseSettings()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            isVisible = false;
        }
    }

    public void ToggleSettings()
    {
        if (isVisible)
        {
            CloseSettings();
        }
        else
        {
            ShowSettings();
        }
    }

    private void OnMasterVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("MasterVolume", value);
        PlayerPrefs.Save();
        AudioListener.volume = value;
    }

    private void OnSFXVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("SFXVolume", value);
        PlayerPrefs.Save();
    }
}


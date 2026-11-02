using System;
using Bear.EventSystem;
using Bear.Game;
using Bear.Logger;
using Bear.UI;
using Game.Events;
using Game.Play;
using UnityEngine;

public partial class GameSettingPopup : BaseUIView, IEventSender, IDebuger
{
    private bool isPlaying = false;
    private IPlayPauseHost _pauseHost;

    [SerializeField] private CustomButton gmBtn;

    [SerializeField] private bool isGamePause = false;

    [SerializeField] private PostcardEnter postcardEnter;


    public override void OnCreate()
    {
        base.OnCreate();

        CloseBtn.OnClick += Close;
        ResumeBtn.OnClick += Close;
        MenuBtn.OnClick += ExitLevel;
        LanguageBtn.OnClick += OpenLocalizationPopup;

        if (PrivacyPolicyBtn)
        {
            PrivacyPolicyBtn.OnClick += ShowPrivacyPolicy;
        }

        if (TermsOfBtn)
        {
            TermsOfBtn.OnClick += ShowTermsOfUse;
        }

        if (ContactBtn)
        {
            ContactBtn.OnClick += SendEmali;
        }

        if (gmBtn)
        {
            gmBtn.OnClick += TryToOpenGM;
        }

        if (VersionTxt)
        {
            var data = Guru.GuruAppVersion.Load();
            VersionTxt.text = data.ToString();
        }
    }

    private int _gmClickCount;
    private void TryToOpenGM(CustomButton btn)
    {
        _gmClickCount++;
        this.Log("----------- : " + _gmClickCount);
        if (_gmClickCount >= 5)
        {
            _gmClickCount = 0;
            GMPasswordPopup.Create();
        }
    }

    private void SendEmali(CustomButton btn)
    {
        try
        {
            // 安全获取邮箱地址
            string emailUrl = string.Empty;
            if (GameSDKService.Instance == null)
            {
                this.LogError("[SendEmail] GameSDKService.Instance is null");
            }
            else if (GameSDKService.Instance.MainAppSpec == null)
            {
                this.LogError("[SendEmail] GameSDKService.Instance.MainAppSpec is null");
            }
            else if (GameSDKService.Instance.MainAppSpec.AppDetails == null)
            {
                this.LogError("[SendEmail] AppDetails is null");
            }
            else
            {
                emailUrl = GameSDKService.Instance.MainAppSpec.AppDetails.EmailUrl ?? string.Empty;
                if (string.IsNullOrEmpty(emailUrl))
                {
                    this.LogError("[SendEmail] EmailUrl is null or empty");
                }
                else
                {
                    this.Log($"[SendEmail] EmailUrl = {emailUrl}");
                }
            }

            // 安全获取 uid
            string uid = string.Empty;
            var account = Guru.SDK.Framework.Core.Account.AccountDataStore.Instance;
            if (account == null)
            {
                this.LogError("[SendEmail] AccountDataStore.Instance is null");
            }
            else
            {
                uid = account.Uid ?? string.Empty;
                if (string.IsNullOrEmpty(uid))
                {
                    this.LogError("[SendEmail] account.Uid is null or empty");
                }
                else
                {
                    this.Log($"[SendEmail] uid = {uid}");
                }
            }

            // 安全获取 deviceId
            string deviceId = string.Empty;
            if (account == null)
            {
                this.LogError("[SendEmail] account is null, 无法获取 deviceId");
            }
            else if (account.CurrentDevice == null)
            {
                this.LogError("[SendEmail] account.CurrentDevice is null");
            }
            else
            {
                deviceId = account.CurrentDevice.DeviceId ?? string.Empty;
                if (string.IsNullOrEmpty(deviceId))
                {
                    this.LogError("[SendEmail] account.CurrentDevice.DeviceId is null or empty");
                }
                else
                {
                    this.Log($"[SendEmail] deviceId = {deviceId}");
                }
            }

            // 发送邮件（即使数据为空也要尝试发送）
            string[] toEmail = new[] { emailUrl };
            this.Log($"[SendEmail] 准备发送邮件，isGamePause = {isGamePause}");

            if (isGamePause)
            {
                EmailUtils.SendMailInGame(toEmail, uid, deviceId);
            }
            else
            {
                EmailUtils.SendMailByMailApp(toEmail, uid, deviceId);
            }

            this.Log("[SendEmail] 邮件调用完成");
        }
        catch (Exception e)
        {
            this.LogError($"[SendEmail] 异常: {e}");
        }
    }

    private void ShowTermsOfUse(CustomButton btn)
    {
        var url = GameSDKService.Instance.MainAppSpec.AppDetails.TermsUrl;
        Application.OpenURL(url);
    }

    private void ShowPrivacyPolicy(CustomButton btn)
    {
        var url = GameSDKService.Instance.MainAppSpec.AppDetails.PolicyUrl;
        Application.OpenURL(url);
    }

    public override void OnOpen()
    {
        RefreshBtns();

        if (postcardEnter)
            postcardEnter.Refresh();
    }

    private void RefreshBtns()
    {
        MenuBtn.gameObject.SetActive(isPlaying);
    }

    private void Close(CustomButton btn)
    {
        UIManager.Instance.CloseUI(this);
        if (isPlaying)
        {
            if (_pauseHost != null)
                _pauseHost.Resume();
            else
                this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
        }
    }

    private void ExitLevel(CustomButton btn)
    {
        IPlayPauseHost pauseHost = _pauseHost;
        UIManager.Instance.CloseUI(this);

        if (pauseHost != null)
        {
            pauseHost.ExitToLevelSelection();
            return;
        }

        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.FAILED);
        ClickTransformPanel.Create(() => ChoiceLevelPanel.Create());
    }

    private void OpenLocalizationPopup(CustomButton btn)
    {
        LocalizationPopup.Create((keyCode) =>
        {
            this.DispatchEvent(Witness<SwitchLanguageEvent>._, keyCode);
        });
    }

    /// <summary>
    /// 创建设置弹窗。
    /// 游戏内打开时可注入当前玩法的暂停能力；为空时回退到主线选关流程。
    /// </summary>
    /// <param name="isPlaying">是否从玩法内打开。</param>
    /// <param name="pauseHost">当前激活玩法提供的暂停、恢复和返回选关能力。</param>
    public static GameSettingPopup Create(bool isPlaying = false, IPlayPauseHost pauseHost = null)
    {
        var prefabName = isPlaying ? $"{typeof(GameSettingPopup).Name}" : $"{typeof(GameSettingPopup).Name}2";
        var panel = UIManager.Instance.OpenUI<GameSettingPopup>(prefabName, Bear.UI.UILayer.Popup);
        panel.isPlaying = isPlaying;
        panel._pauseHost = isPlaying ? pauseHost : null;
        panel.RefreshBtns();
        return panel;
    }

    void OnGUI()
    {
#if DEBUG_MODE
        if (gameObject.activeSelf)
        {
            var rowGap = 10f;
            var rowHeight = 60f + rowGap;
            var bottomY = Screen.height - 50f;
            var failCountRect = new Rect(20f, bottomY, 730f * 2f, 60);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                normal = { textColor = Color.white }
            };
            GUI.Label(failCountRect, $"当前模式: IsDebug", style);
        }
#endif
    }
}

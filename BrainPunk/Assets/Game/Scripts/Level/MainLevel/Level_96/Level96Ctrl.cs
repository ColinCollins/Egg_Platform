using System.Collections;
using Bear.EventSystem;
using Bear.UI;
using Game.Events;
using Game.Play;
using UnityEngine;

/// <summary>
/// Level096 本地运行时控制：配置本关密码面板，并只在游戏内设置暂停页打开时显示密码提示。
/// </summary>
public sealed class Level96Ctrl : MonoBehaviour
{
    private const string PasswordPopupName = "PasswordPopup_96_3316";

    /// <summary>
    /// 场景中已摆放的密码提示；初始隐藏，仅在游戏内设置暂停页显示期间激活。
    /// </summary>
    [SerializeField] private RectTransform passwordClue;

    private EventSubscriber _subscriber;
    private Coroutine _panelSetupCoroutine;
    private Coroutine _pauseCheckCoroutine;

    #region Unity lifecycle

    private void OnEnable()
    {
        SetPasswordClueVisible(false);
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        _subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        _subscriber.Subscribe<GameResumeEvent>(OnGameResume);
        _subscriber.Subscribe<GameResetEvent>(OnGameReset);

        _panelSetupCoroutine = StartCoroutine(ConfigurePasswordPopupWhenReady());
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
        SetPasswordClueVisible(false);
    }

    private void OnDestroy() => CleanupRuntimeState();

    private void CleanupRuntimeState()
    {
        CancelPendingCoroutines();
        EventsUtils.ResetEvents(ref _subscriber);
    }

    #endregion

    #region Password popup

    private IEnumerator ConfigurePasswordPopupWhenReady()
    {
        // PlayCtrl 会先实例化关卡，再在同一帧替换旧 HUD；延后一帧只配置新面板。
        yield return null;

        while (isActiveAndEnabled
               && (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null))
        {
            yield return null;
        }

        _panelSetupCoroutine = null;
        if (!isActiveAndEnabled || PlayCtrl.Instance == null)
            yield break;

        Level6GamePanelFeature panelFeature = PlayCtrl.Instance.CurrentGamePlayPanel
            .GetComponent<Level6GamePanelFeature>();
        if (panelFeature == null)
        {
            Debug.LogWarning("[Level96Ctrl] Level6GamePanelFeature is missing from GamePlayPanel_001.");
            yield break;
        }

        panelFeature.ConfigurePasswordPopup(PasswordPopupName);
    }

    #endregion

    #region Pause clue

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt.NewState == GamePlayStateName.PAUSE)
            return;

        CancelPauseCheck();
        SetPasswordClueVisible(false);
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        CancelPauseCheck();
        _pauseCheckCoroutine = StartCoroutine(RevealClueAfterSettingsPopupCreated());
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        CancelPauseCheck();
        SetPasswordClueVisible(false);
    }

    private void OnGameReset(GameResetEvent evt)
    {
        CancelPauseCheck();
        SetPasswordClueVisible(false);
    }

    private IEnumerator RevealClueAfterSettingsPopupCreated()
    {
        yield return null;
        _pauseCheckCoroutine = null;

        if (!isActiveAndEnabled || PlayCtrl.Instance == null || UIManager.Instance == null)
            yield break;

        GameSettingPopup settingPopup = UIManager.Instance
            .GetUI<GameSettingPopup>(nameof(GameSettingPopup));
        bool isSettingsPause = PlayCtrl.Instance.CheckState(GamePlayStateName.PAUSE)
                               && settingPopup != null
                               && settingPopup.IsVisible;
        SetPasswordClueVisible(isSettingsPause);
    }

    private void SetPasswordClueVisible(bool isVisible)
    {
        if (passwordClue != null && passwordClue.gameObject.activeSelf != isVisible)
            passwordClue.gameObject.SetActive(isVisible);
    }

    #endregion

    #region Cleanup

    private void CancelPendingCoroutines()
    {
        if (_panelSetupCoroutine != null)
        {
            StopCoroutine(_panelSetupCoroutine);
            _panelSetupCoroutine = null;
        }

        CancelPauseCheck();
    }

    private void CancelPauseCheck()
    {
        if (_pauseCheckCoroutine == null)
            return;

        StopCoroutine(_pauseCheckCoroutine);
        _pauseCheckCoroutine = null;
    }

    #endregion
}

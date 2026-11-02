using System.Collections;
using Bear.EventSystem;
using Bear.UI;
using Game.Events;
using Game.Play;
using UnityEngine;

/// <summary>
/// Level151 本地运行时控制：配置固定密码 1358 的弹窗，并只在游戏内设置暂停页打开时显示问号提示。
/// </summary>
public sealed class Level151Ctrl : MonoBehaviour
{
    private const string PasswordPopupName = "PasswordPopup_151_1358";

    /// <summary>
    /// 场景中已摆放的四问号提示；初始隐藏，仅在游戏内设置暂停页显示期间激活。
    /// </summary>
    [SerializeField] private RectTransform passwordClue;

    private EventSubscriber _subscriber;
    private Level6GamePanelFeature _panelFeature;
    private Coroutine _panelSetupCoroutine;
    private Coroutine _pauseCheckCoroutine;
    private bool _passwordSolved;

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
        _subscriber.Subscribe<OnTiggerItemEvent>(OnTiggerItem);

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
        _panelFeature = null;
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

        _panelFeature = PlayCtrl.Instance.CurrentGamePlayPanel
            .GetComponent<Level6GamePanelFeature>();
        if (_panelFeature == null)
        {
            Debug.LogWarning("[Level151Ctrl] Level6GamePanelFeature is missing from GamePlayPanel_001.");
            yield break;
        }

        _panelFeature.ConfigurePasswordPopup(PasswordPopupName);
        ApplyPasswordEntryState();
    }

    private void OnTiggerItem(OnTiggerItemEvent evt)
    {
        if (evt == null || evt.EventId != 1 || _passwordSolved)
            return;

        _passwordSolved = true;
        ApplyPasswordEntryState();
    }

    private void ApplyPasswordEntryState()
    {
        CustomButton useButton = _panelFeature != null ? _panelFeature.Use_btn : null;
        if (useButton == null)
            return;

        useButton.IsBlockClick = _passwordSolved;
        useButton.Interactable = !_passwordSolved;
        // 未解锁时只恢复点击能力；入口是否显示仍由场景区域事件控制。
        if (_passwordSolved)
            useButton.gameObject.SetActive(false);
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

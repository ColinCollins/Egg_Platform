using System.Collections;
using Bear.Logger;
using Game.ItemEvent;
using Game.Level;
using UnityEngine;

/// <summary>
/// Level 115 专属旁挂控制器：管理课桌测验入口、HUD 手掌状态、测验完成门禁，
/// 并在五题全对后直接触发门锁解锁链与门碰撞恢复。
/// </summary>
public sealed class Level115Ctrl : MonoBehaviour, IDebuger
{
    private const float DoorColliderEnableDelay = 1f;

    [Header("通关门解锁（测验完成后直接开锁）")]
    [SerializeField] private EventHandleListener doorLockUnlockHandle;
    [SerializeField] private Collider2D exitDoorTrigger;

    private BaseLevelCtrl _baseLevelCtrl;
    private GamePlayPanel _gamePlayPanel;
    private Level115GamePanelFeature _gamePanelFeature;
    private Level115QuizPopup _quizPopup;
    private bool _actorInQuizRange;
    private bool _quizPopupOpen;
    private bool _quizCompleted;
    private bool _doorUnlockStarted;
    private Coroutine _doorUnlockRoutine;

    public string LogTag => nameof(Level115Ctrl);
    public bool LogEnabled { get; set; } = true;
    public ActorCtrl Actor => _baseLevelCtrl != null ? _baseLevelCtrl.Actor : null;

    private void Awake()
    {
        _baseLevelCtrl = GetComponent<BaseLevelCtrl>();
    }

    private void Start()
    {
        BindGamePanelFeature();
        RefreshHandPrompt();
    }

    /// <summary>
    /// 由课桌 Trigger 报告玩家是否仍在 5.28×1.78 的世界命中范围内。
    /// </summary>
    public void SetActorInQuizRange(bool isInRange)
    {
        _actorInQuizRange = isInRange;
        if (_gamePanelFeature == null)
            BindGamePanelFeature();
        RefreshHandPrompt();
    }

    /// <summary>
    /// Popup 在任意关闭路径结束时回报，用于清理 HUD 旧移动状态并恢复手掌生命周期。
    /// </summary>
    public void NotifyQuizPopupClosed()
    {
        _quizPopup = null;
        _quizPopupOpen = false;
        CutGameplayInput();
        RefreshHandPrompt();
        if (_gamePanelFeature != null)
            _gamePanelFeature.ResetInteractionState();
    }

    /// <summary>
    /// 五题全对后、Popup 关闭前先锁定本局课桌入口；门锁解锁仍在 Popup 关闭并显示统计后触发。
    /// </summary>
    public void PrepareQuizCompletion()
    {
        if (_quizCompleted)
            return;

        _quizCompleted = true;
        RefreshHandPrompt();
    }

    /// <summary>
    /// 开学测验完成入口。同局只解锁一次门锁；入口禁用与开锁触发分离，确保 Popup 先关闭。
    /// </summary>
    public void NotifyQuizCompleted()
    {
        PrepareQuizCompletion();

        if (_doorUnlockStarted)
            return;

        if (doorLockUnlockHandle == null || exitDoorTrigger == null)
        {
            this.LogWarning("[Level115Ctrl] DoorLock 解锁句柄或通关门触发碰撞体未绑定，无法直接开锁。");
            return;
        }

        _doorUnlockStarted = true;
        _doorUnlockRoutine = StartCoroutine(UnlockExitDoorRoutine());
    }

    private void TryOpenQuiz()
    {
        if (_quizCompleted || _quizPopupOpen || !_actorInQuizRange)
            return;

        if (_gamePanelFeature == null && !BindGamePanelFeature())
        {
            this.LogWarning("[Level115Ctrl] Level 115 专属 Gameplay HUD Feature 未找到，无法打开测验。");
            return;
        }

        CutGameplayInput();
        _quizPopupOpen = true;

        _quizPopup = Level115QuizPopup.Create(this);
        if (_quizPopup != null)
            return;

        _quizPopupOpen = false;
        CutGameplayInput();
        RefreshHandPrompt();
        this.LogWarning("[Level115Ctrl] Level115QuizPopup 创建失败。");
    }

    private bool BindGamePanelFeature()
    {
        GamePlayPanel panel = PlayCtrl.Instance != null ? PlayCtrl.Instance.CurrentGamePlayPanel : null;
        if (panel == null)
            return false;

        Level115GamePanelFeature feature = panel.GetComponent<Level115GamePanelFeature>();
        if (feature == null)
            return false;

        _gamePlayPanel = panel;
        if (_gamePanelFeature != null && _gamePanelFeature != feature)
            _gamePanelFeature.Configure(null);

        _gamePanelFeature = feature;
        _gamePanelFeature.Configure(TryOpenQuiz);
        return true;
    }

    private void RefreshHandPrompt()
    {
        if (_gamePanelFeature == null)
            return;

        _gamePanelFeature.SetVisible(_actorInQuizRange && !_quizCompleted);
    }

    private void CutGameplayInput()
    {
        // Popup Mask blocks new HUD pointers. Reset only this level's active panel and actor input
        // so an already-held direction cannot continue underneath or recover after the Popup closes.
        if (_gamePlayPanel != null)
            _gamePlayPanel.ResetState();

        Actor?.SetMoveInput(0f);
    }

    private IEnumerator UnlockExitDoorRoutine()
    {
        // DoorLock EventHandleListener → BaseItemExecutor：unlock 动画 + unlock 音效 + 锁自毁。
        doorLockUnlockHandle.Execute();

        // 与 Level 025 直接开锁一致：稍后再启用门触发碰撞，避免锁动画未完成就进门。
        if (DoorColliderEnableDelay > 0f)
            yield return new WaitForSeconds(DoorColliderEnableDelay);

        if (exitDoorTrigger != null)
            exitDoorTrigger.enabled = true;

        _doorUnlockRoutine = null;
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
    }

    private void OnDestroy()
    {
        CleanupRuntimeState();
    }

    private void CleanupRuntimeState()
    {
        if (_doorUnlockRoutine != null)
        {
            StopCoroutine(_doorUnlockRoutine);
            _doorUnlockRoutine = null;
        }

        Level115QuizPopup popup = _quizPopup;
        _quizPopup = null;
        if (popup != null)
            popup.CancelFromOwner(this);

        CutGameplayInput();
        _quizPopupOpen = false;
        _actorInQuizRange = false;

        if (_gamePanelFeature != null)
        {
            _gamePanelFeature.Configure(null);
            _gamePanelFeature.SetVisible(false);
            _gamePanelFeature = null;
        }

        _gamePlayPanel = null;
    }
}

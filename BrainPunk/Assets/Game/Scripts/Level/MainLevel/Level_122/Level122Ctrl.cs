using System;
using System.Collections;
using Bear.EventSystem;
using Bear.Logger;
using Bear.SaveModule;
using Config.Game;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level 122 专属控制器：玩家靠近桌子后自动打开叠层三消面板，
/// 通过捕获场景门锁再匹配两张锁牌，或清空全部元素，完成三消并解锁出口。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level122Ctrl : MonoBehaviour, IEventSender, IDebuger
{
    private const float CompletionHoldDuration = 1f;
    private const float DeadlockTipDuration = 1.5f;

    [Header("场景门锁")]
    [SerializeField] private Collider2D exitDoorTrigger;
    [Tooltip("只作为 UI 方格吸附目标；三消前绝不移动此 Transform。")]
    [SerializeField] private Transform sceneLockTarget;

    [Header("三消素材")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite slotSprite;
    [SerializeField] private Sprite lockSprite;
    [SerializeField] private Sprite[] propSprites;

    [Header("反馈音效")]
    [SerializeField] private string matchSfxTag = "passwordCorrect";
    [SerializeField] private string failedSfxTag = "failed";

    [Header("死局上浮提示（随当前语言选择）")]
    [TextArea] [SerializeField] private string deadlockTipZhCn = "没有可消除的组合，正在重新开始";
    [TextArea] [SerializeField] private string deadlockTipEn = "No matches left. Restarting...";
    [TextArea] [SerializeField] private string deadlockTipPtBr = "Não há combinações. Reiniciando...";
    [TextArea] [SerializeField] private string deadlockTipId = "Tidak ada kombinasi. Memulai ulang...";
    [TextArea] [SerializeField] private string deadlockTipTl = "Wala nang maititerno. Magsisimulang muli...";
    [TextArea] [SerializeField] private string deadlockTipKo = "더 이상 맞출 수 없습니다. 다시 시작합니다.";
    [TextArea] [SerializeField] private string deadlockTipJa = "消せる組み合わせがありません。やり直します。";
    [TextArea] [SerializeField] private string deadlockTipZhTw = "沒有可消除的組合，正在重新開始";
    [TextArea] [SerializeField] private string deadlockTipRu = "Нет доступных комбинаций. Перезапуск…";
    [TextArea] [SerializeField] private string deadlockTipEsEs = "No quedan combinaciones. Reiniciando...";
    [TextArea] [SerializeField] private string deadlockTipEsUs = "No quedan combinaciones. Reiniciando...";
    [TextArea] [SerializeField] private string deadlockTipTr = "Eşleşme kalmadı. Yeniden başlatılıyor...";

    // 仅全部元素三消完成时显示；由当前语言选择，不写入共享语言表或存档。
    [Header("全部消除上浮提示（随当前语言选择）")]
    [TextArea] [SerializeField] private string completionTipZhCn = "恭喜完成！过关拉！";
    [TextArea] [SerializeField] private string completionTipEn = "Congratulations! You cleared it!";
    [TextArea] [SerializeField] private string completionTipPtBr = "Parabéns! Você conseguiu!";
    [TextArea] [SerializeField] private string completionTipId = "Selamat! Kamu berhasil!";
    [TextArea] [SerializeField] private string completionTipTl = "Binabati kita! Natapos mo na!";
    [TextArea] [SerializeField] private string completionTipKo = "축하합니다! 성공했어요!";
    [TextArea] [SerializeField] private string completionTipJa = "おめでとう！クリアしたよ！";
    [TextArea] [SerializeField] private string completionTipZhTw = "恭喜完成！過關啦！";
    [TextArea] [SerializeField] private string completionTipRu = "Поздравляем! Всё получилось!";
    [TextArea] [SerializeField] private string completionTipEsEs = "¡Enhorabuena! ¡Lo has conseguido!";
    [TextArea] [SerializeField] private string completionTipEsUs = "¡Felicidades! ¡Lo lograste!";
    [TextArea] [SerializeField] private string completionTipTr = "Tebrikler! Başardın!";

    private BaseLevelCtrl _baseLevelCtrl;
    private Level122MatchPanel _matchPanel;
    private Coroutine _doorUnlockRoutine;
    private Coroutine _deadlockRoutine;
    private bool _actorInTableRange;
    private bool _closedUntilTableExit;
    private bool _completed;
    private bool _deadlockStarted;
    private bool _teardownStarted;

    public string LogTag => nameof(Level122Ctrl);
    public bool LogEnabled { get; set; } = true;
    public ActorCtrl Actor => _baseLevelCtrl != null ? _baseLevelCtrl.Actor : null;

    internal Sprite PanelSprite => panelSprite;
    internal Sprite SlotSprite => slotSprite;
    internal Sprite LockSprite => lockSprite;
    internal Sprite[] PropSprites => propSprites;
    internal Transform SceneLockTarget => sceneLockTarget;
    internal string MatchSfxTag => matchSfxTag;

    private void Awake()
    {
        _baseLevelCtrl = GetComponent<BaseLevelCtrl>();
    }

    private void Update()
    {
        // CurrentGamePlayPanel 可能比关卡对象晚一帧创建；保持在桌边时自动补开。
        if (_actorInTableRange && !_closedUntilTableExit && !_completed && !_deadlockStarted && _matchPanel == null)
            TryOpenPanel();
    }

    /// <summary>由桌子范围 Trigger 报告玩家是否仍在交互范围内。</summary>
    public void SetActorInTableRange(bool isInRange)
    {
        _actorInTableRange = isInRange;
        if (!isInRange)
            _closedUntilTableExit = false;
        if (isInRange)
            TryOpenPanel();
    }

    /// <summary>玩家主动关闭页面后，离开桌边前不再立即自动弹出。</summary>
    internal void NotifyPanelClosedByUser(Level122MatchPanel panel)
    {
        if (_teardownStarted || panel == null || panel != _matchPanel)
            return;

        _closedUntilTableExit = true;
        ClosePanel(resumeActor: true);
    }

    /// <summary>面板与实体锁消除结束后调用一次；只解锁出口，不直接触发整关结算。</summary>
    internal void NotifyMatchCompleted(Level122MatchPanel panel, bool clearedBoard)
    {
        if (_teardownStarted || _completed || _deadlockStarted || panel == null || panel != _matchPanel)
            return;

        _completed = true;
        if (exitDoorTrigger != null)
            exitDoorTrigger.enabled = true;
        else
            this.LogWarning("[Level122Ctrl] 出口碰撞体未绑定。");
        if (clearedBoard)
            SystemTips.Show(GetCompletionMessage());
        _doorUnlockRoutine = StartCoroutine(UnlockExitDoorRoutine());
    }

    /// <summary>六槽填满且没有三连时依次播放错误音效、上浮提示，停留后收起页面并重开。</summary>
    internal void NotifyDeadlock(Level122MatchPanel panel)
    {
        if (_teardownStarted || _completed || _deadlockStarted || panel == null || panel != _matchPanel)
            return;

        _deadlockStarted = true;
        SetGameplayLocked(true);
        AudioManager.PlaySound(failedSfxTag);
        SystemTips.Show(GetDeadlockMessage());
        _deadlockRoutine = StartCoroutine(RestartAfterDeadlockRoutine());
    }

    private void TryOpenPanel()
    {
        if (_teardownStarted || _completed || _deadlockStarted || _closedUntilTableExit ||
            !_actorInTableRange || _matchPanel != null)
            return;

        if (panelSprite == null || slotSprite == null || lockSprite == null ||
            propSprites == null || propSprites.Length == 0 || sceneLockTarget == null)
        {
            this.LogWarning("[Level122Ctrl] 三消面板素材或场景锁吸附点未完整绑定。");
            return;
        }

        GamePlayPanel gamePlayPanel = PlayCtrl.Instance != null
            ? PlayCtrl.Instance.CurrentGamePlayPanel
            : null;
        Canvas canvas = gamePlayPanel != null ? gamePlayPanel.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
            return;

        _matchPanel = Level122MatchPanel.Create(this, canvas);
        if (_matchPanel == null)
        {
            this.LogWarning("[Level122Ctrl] 三消面板创建失败。");
            return;
        }

        SetGameplayLocked(true);
    }

    private void ClosePanel(bool resumeActor, bool immediate = false)
    {
        Level122MatchPanel panel = _matchPanel;
        _matchPanel = null;
        if (panel != null)
            panel.CloseFromOwner(this, immediate);

        if (resumeActor)
            SetGameplayLocked(false);
    }

    private void SetGameplayLocked(bool locked)
    {
        if (_baseLevelCtrl != null)
            _baseLevelCtrl.SetCtrlable(!locked);

        ActorCtrl actor = Actor;
        if (actor == null)
            return;

        actor.SetMoveInput(0f);
        if (locked)
            actor.StopMoving();
        else
            actor.ResumeMoving();
    }

    private IEnumerator UnlockExitDoorRoutine()
    {
        yield return new WaitForSecondsRealtime(CompletionHoldDuration);
        ClosePanel(resumeActor: false);
        yield return new WaitForSecondsRealtime(Level122MatchPanel.PageCloseDuration);
        SetGameplayLocked(false);
        _doorUnlockRoutine = null;
    }

    private IEnumerator RestartAfterDeadlockRoutine()
    {
        yield return new WaitForSecondsRealtime(DeadlockTipDuration);
        ClosePanel(resumeActor: false);
        yield return new WaitForSecondsRealtime(Level122MatchPanel.PageCloseDuration);
        _deadlockRoutine = null;
        this.DispatchEvent(Witness<GameResetEvent>._, GameResetType.Failed);
    }

    private string GetDeadlockMessage()
    {
        string code = DB.GameSetting.CurrentLanguageKeyCode ?? string.Empty;
        switch (code.ToLowerInvariant())
        {
            case "zh-cn": return deadlockTipZhCn;
            case "pt-br": return deadlockTipPtBr;
            case "id": return deadlockTipId;
            case "tl": return deadlockTipTl;
            case "ko": return deadlockTipKo;
            case "ja": return deadlockTipJa;
            case "zh-tw": return deadlockTipZhTw;
            case "ru": return deadlockTipRu;
            case "es-es": return deadlockTipEsEs;
            case "es-us": return deadlockTipEsUs;
            case "tr": return deadlockTipTr;
            default: return deadlockTipEn;
        }
    }

    private string GetCompletionMessage()
    {
        string code = DB.GameSetting.CurrentLanguageKeyCode ?? string.Empty;
        switch (code.ToLowerInvariant())
        {
            case "zh-cn": return completionTipZhCn;
            case "pt-br": return completionTipPtBr;
            case "id": return completionTipId;
            case "tl": return completionTipTl;
            case "ko": return completionTipKo;
            case "ja": return completionTipJa;
            case "zh-tw": return completionTipZhTw;
            case "ru": return completionTipRu;
            case "es-es": return completionTipEsEs;
            case "es-us": return completionTipEsUs;
            case "tr": return completionTipTr;
            default: return completionTipEn;
        }
    }

    private void OnDisable()
    {
        CleanupRuntime();
    }

    private void OnDestroy()
    {
        CleanupRuntime();
    }

    private void CleanupRuntime()
    {
        if (_teardownStarted)
            return;

        _teardownStarted = true;
        if (_doorUnlockRoutine != null)
        {
            StopCoroutine(_doorUnlockRoutine);
            _doorUnlockRoutine = null;
        }

        if (_deadlockRoutine != null)
        {
            StopCoroutine(_deadlockRoutine);
            _deadlockRoutine = null;
        }

        ClosePanel(resumeActor: false, immediate: true);
        _actorInTableRange = false;
        _closedUntilTableExit = false;
    }
}

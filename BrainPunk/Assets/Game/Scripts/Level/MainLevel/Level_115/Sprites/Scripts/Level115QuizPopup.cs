using System;
using Bear.EventSystem;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using I2.Loc;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Level 115「开学测验」：固定五题、六位置自由交换、单题六秒统一判题与统计收口。
/// </summary>
public partial class Level115QuizPopup : BaseUIView
{
    private const int EquationSlotCount = 5;
    private const int TopPositionIndex = 5;
    private const int PositionCount = 6;
    private const int NoPointer = int.MinValue;
    private const float QuestionDuration = 6f;
    private const float TransitionDuration = 0.2f;
    /// <summary>
    /// 单题失败即结束后的关窗前停留。固定 1s，略长于 passwordError 资源时长约 0.984s，
    /// 给玩家看清叉反馈的反应时间；正确切题/第五题全对仍用 TransitionDuration。
    /// </summary>
    private const float WrongFinalHoldDuration = 1f;
    private const string InitialTopContent = "×";
    private const string StatisticsTerm = "S_LevelTips_Des_level_115_1";
    /// <summary>单题失败关窗后的固定上浮文案词条；仅 zh-CN，内容「你答错了！！！」。</summary>
    private const string WrongAnswerTipTerm = "S_LevelTips_Des_level_115_2";
    private const string InstructionTerm = "S_LevelTips_Des_level_115";
    private const float MultiplyImageSize = 46f;
    /// <summary>
    /// 对齐 Level 78：在字号 100 时约 4px 外侧描边（outlineWidth=FaceDilate=0.2）。
    /// </summary>
    private const float EquationOutlineWidth = 0.2f;
    private static readonly Color32 EquationOutlineColor = new Color32(0x66, 0x5a, 0x4f, 0xff);

    /// <summary>
    /// 复用 006 密码音效 Tag；仅在单题 6 秒到时判题瞬间各播一次，不截断、不随暂停链静音。
    /// </summary>
    private const string PasswordCorrectSfxTag = "passwordCorrect";
    private const string PasswordErrorSfxTag = "passwordError";

    /// <summary>
    /// 进度点反馈必须落在切题/第五题结果停留的 0.2s 窗口内。
    /// </summary>
    private const float ProgressFeedbackDuration = 0.2f;
    private const float CorrectDotBounceScale = 1.12f;
    private const float WrongDotShakeStrengthX = 10f;

    private static readonly string[][] Questions =
    {
        new[] { "5", "+", "2", "=", "3" },
        new[] { "4", "=", "1", "-", "3" },
        new[] { "3", "+", "6", "=", "3" },
        // 特殊题身份固定为本数组第 4 项（零基 3）；显示位置由会话题序约束到第 4/5 题。
        new[] { "4", "+", "2", "=", "8" },
        new[] { "2", "-", "7", "=", "9" },
    };

    /// <summary>
    /// 特殊题「4 + 2 = 8」在 <see cref="Questions"/> 中的固定源索引。
    /// 每次 BeginSession 仅将其显示位等概率放到第 4 或第 5 题（零基 3/4）。
    /// </summary>
    private const int SpecialQuestionSourceIndex = 3;

    [SerializeField] private Image QuizTimerBackground;
    [SerializeField] private Image QuizTimerFill;
    [SerializeField] private Image[] ProgressDotBackgrounds;
    [SerializeField] private Image[] ProgressDotCorrectIcons;
    [SerializeField] private Image[] ProgressDotWrongIcons;

    private enum QuizPhase
    {
        None,
        Question,
        Transition,
        FinalHold,
    }

    private enum ProgressDotState
    {
        Empty,
        Correct,
        Wrong,
    }

    private enum QuizCloseIntent
    {
        Active,
        System,
    }

    private readonly string[] _contents = new string[PositionCount];
    private readonly ProgressDotState[] _progressDotStates = new ProgressDotState[EquationSlotCount];
    private readonly Tween[] _progressDotTweens = new Tween[EquationSlotCount];
    private readonly Vector3[] _progressDotRootBaseScales = new Vector3[EquationSlotCount];
    private readonly Vector2[] _progressDotRootBaseAnchored = new Vector2[EquationSlotCount];
    private readonly Vector3[] _progressCorrectIconBaseScales = new Vector3[EquationSlotCount];
    private readonly Vector3[] _progressWrongIconBaseScales = new Vector3[EquationSlotCount];

    private EventSubscriber _subscriber;
    private Level115Ctrl _owner;
    private TextMeshProUGUI[] _slotTexts;
    private Image[] _slotFrames;
    private Image[] _slotMultiplyImages;
    private RectTransform[] _hitRects;
    private TextMeshProUGUI _topContentText;
    private RectTransform _dragVisualRoot;
    private TextMeshProUGUI _dragVisualText;
    private Image _dragVisualImage;
    private RectTransform[] _progressDotRoots;

    private QuizPhase _phase;
    private int _currentQuestionIndex;
    /// <summary>
    /// 本次测验显示顺序：元素为 Questions 源索引。仅 BeginSession 构造一次，
    /// 同次测验内 LoadQuestion / 进度点 / 通关计数只按显示位消费，不再随机。
    /// </summary>
    private readonly int[] _questionOrder = new int[EquationSlotCount];
    private int _correctCount;
    private int _wrongCount;
    private int _interactionVersion;
    private int _dragSourceIndex = -1;
    private int _dragPointerId = NoPointer;
    private int _hideVersion;
    private float _remainingQuestionTime;
    private float _remainingTransitionTime;
    private double _lastClockTime;
    private bool _viewReady;
    private bool _sessionActive;
    private bool _inputLocked;
    private bool _gamePaused;
    private bool _applicationPaused;
    private bool _applicationFocused = true;
    private bool _viewHidden;
    private bool _externalPauseOwned;
    private bool _isClosing;
    private bool _pendingSilentClose;

    public override void OnCreate()
    {
        base.OnCreate();

        ResolveSerializedReferences();
        _viewReady = BuildRuntimeView();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        _hideVersion++;
        _viewHidden = false;
        _isClosing = false;
        _pendingSilentClose = false;
        _lastClockTime = RealtimeNow;
    }

    public override void OnShow()
    {
        base.OnShow();

        _hideVersion++;
        _viewHidden = false;
        _lastClockTime = RealtimeNow;

        if (_pendingSilentClose)
        {
            CloseSilentlyNow();
            return;
        }

        if (!_externalPauseOwned)
        {
            SyncProgressDotFeedbackPlayback();
            return;
        }

        _externalPauseOwned = false;
        IPlayPauseHost pauseHost = GameManager.Instance?.ControllerManager?.ActivePauseHost;
        PlayCtrl playCtrl = PlayCtrl.Instance;
        if (pauseHost != null && playCtrl != null && playCtrl.CheckState(GamePlayStateName.PAUSE))
            pauseHost.Resume();

        SyncProgressDotFeedbackPlayback();
    }

    public override void OnHide()
    {
        _viewHidden = true;
        _lastClockTime = RealtimeNow;
        InvalidatePointerInteractions();
        CancelCurrentDrag();
        SyncProgressDotFeedbackPlayback();

        int version = ++_hideVersion;
        bool shouldPauseForCover = !_isClosing && _sessionActive;
        base.OnHide();

        if (shouldPauseForCover)
            PauseForCoveringPopupAsync(version).Forget();
    }

    public override void OnClose()
    {
        base.OnClose();

        _hideVersion++;
        _sessionActive = false;
        _phase = QuizPhase.None;
        _inputLocked = true;
        _pendingSilentClose = false;
        _externalPauseOwned = false;
        _isClosing = false;
        CancelCurrentDrag();
        KillAllProgressDotTweens();
        EventsUtils.ResetEvents(ref _subscriber);

        Level115Ctrl owner = _owner;
        _owner = null;
        if (owner != null)
            owner.NotifyQuizPopupClosed();
    }

    private void Update()
    {
        AdvanceClockToNow();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        AdvanceClockToNow();
        _applicationPaused = pauseStatus;
        _lastClockTime = RealtimeNow;

        if (pauseStatus)
        {
            InvalidatePointerInteractions();
            CancelCurrentDrag();
        }

        // PlayCtrl 暂停不改 timeScale；应用切后台也要显式冻结进度点 Tween。
        SyncProgressDotFeedbackPlayback();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        AdvanceClockToNow();
        _applicationFocused = hasFocus;
        _lastClockTime = RealtimeNow;

        if (!hasFocus)
        {
            InvalidatePointerInteractions();
            CancelCurrentDrag();
        }

        SyncProgressDotFeedbackPlayback();
    }

    /// <summary>
    /// 打开并初始化一次全新的五题测验。每次调用都会重建受约束题序、恢复第一题显示位、
    /// 统计与右上角初始 ×。右上角内容在同一次测验内跨题继承；特殊题参考解法依赖 ×，
    /// 若玩家此前已换走则不保证仍可用，本实现不强制在特殊题到来时重置右上角。
    /// </summary>
    private bool BeginSession(Level115Ctrl owner)
    {
        if (!_viewReady || owner == null || _sessionActive)
            return false;

        _owner = owner;
        _sessionActive = true;
        _phase = QuizPhase.Question;
        _currentQuestionIndex = 0;
        _correctCount = 0;
        _wrongCount = 0;
        BuildConstrainedQuestionOrder();
        ResetProgressDots();
        _remainingQuestionTime = QuestionDuration;
        _remainingTransitionTime = 0f;
        _inputLocked = false;
        _applicationPaused = false;
        _applicationFocused = Application.isFocused;
        _viewHidden = false;
        _isClosing = false;
        _pendingSilentClose = false;
        _externalPauseOwned = false;
        _contents[TopPositionIndex] = InitialTopContent;

        PlayCtrl playCtrl = PlayCtrl.Instance;
        _gamePaused = playCtrl != null && !playCtrl.CheckState(GamePlayStateName.PLAYING);
        _lastClockTime = RealtimeNow;

        RegisterEvents();
        LoadQuestion(0);
        return true;
    }

    /// <summary>
    /// 构造本次测验的显示题序：特殊题源索引固定为
    /// <see cref="SpecialQuestionSourceIndex"/>，显示位等概率为 3 或 4；
    /// 其余四题 Fisher–Yates 打乱后填入剩余显示位。不改 Questions 内容，每题各一次。
    /// </summary>
    private void BuildConstrainedQuestionOrder()
    {
        // 特殊题显示位：第 4 或第 5 题（零基 3/4），等概率。
        int specialDisplayIndex = UnityEngine.Random.Range(0, 2) == 0 ? 3 : 4;

        int otherCount = Questions.Length - 1;
        // 栈上临时缓冲，避免每次开局分配。
        Span<int> others = stackalloc int[otherCount];
        int write = 0;
        for (int source = 0; source < Questions.Length; source++)
        {
            if (source == SpecialQuestionSourceIndex)
                continue;
            others[write++] = source;
        }

        // Fisher–Yates：只打乱非特殊题。
        for (int i = otherCount - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int tmp = others[i];
            others[i] = others[j];
            others[j] = tmp;
        }

        int otherRead = 0;
        for (int display = 0; display < Questions.Length; display++)
        {
            if (display == specialDisplayIndex)
                _questionOrder[display] = SpecialQuestionSourceIndex;
            else
                _questionOrder[display] = others[otherRead++];
        }
    }

    private void RegisterEvents()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        _subscriber.Subscribe<GameResumeEvent>(OnGameResume);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        _subscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        PauseQuizClock();
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        ResumeQuizClock();
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt.NewState == GamePlayStateName.PLAYING)
        {
            ResumeQuizClock();
            return;
        }

        if (evt.NewState == GamePlayStateName.PAUSE)
        {
            PauseQuizClock();
            return;
        }

        RequestSilentClose();
    }

    private void OnGameReset(GameResetEvent evt)
    {
        RequestSilentClose();
    }

    private void PauseQuizClock()
    {
        AdvanceClockToNow();
        _gamePaused = true;
        _lastClockTime = RealtimeNow;
        InvalidatePointerInteractions();
        CancelCurrentDrag();
        // 视觉反馈与 0.2s 过渡共用暂停链；音效不在此截断。
        SyncProgressDotFeedbackPlayback();
    }

    private void ResumeQuizClock()
    {
        InvalidatePointerInteractions();
        CancelCurrentDrag();
        _gamePaused = false;
        _lastClockTime = RealtimeNow;
        SyncProgressDotFeedbackPlayback();
    }

    private void AdvanceClockToNow()
    {
        double now = RealtimeNow;
        double elapsed = now - _lastClockTime;
        _lastClockTime = now;

        if (elapsed <= 0d || !CanAdvanceClock())
            return;

        AdvanceClock((float)elapsed);
    }

    private void AdvanceClock(float delta)
    {
        int boundaryGuard = Questions.Length * 2 + 1;
        while (delta > 0f && _sessionActive && boundaryGuard-- > 0)
        {
            if (_phase == QuizPhase.Question)
            {
                float consumed = Mathf.Min(delta, _remainingQuestionTime);
                _remainingQuestionTime = Mathf.Max(0f, _remainingQuestionTime - consumed);
                delta -= consumed;
                SetTimerRatio(_remainingQuestionTime / QuestionDuration);

                if (_remainingQuestionTime <= 0f)
                {
                    CompleteCurrentQuestion();
                    continue;
                }

                return;
            }

            if (_phase == QuizPhase.Transition || _phase == QuizPhase.FinalHold)
            {
                float consumed = Mathf.Min(delta, _remainingTransitionTime);
                _remainingTransitionTime = Mathf.Max(0f, _remainingTransitionTime - consumed);
                delta -= consumed;

                if (_remainingTransitionTime <= 0f)
                {
                    if (_phase == QuizPhase.FinalHold)
                    {
                        CompleteFinalHold();
                        return;
                    }

                    CompleteTransition();
                    continue;
                }
            }

            return;
        }
    }

    private bool CanAdvanceClock()
    {
        if (!_sessionActive || _gamePaused || _applicationPaused || !_applicationFocused || _viewHidden)
            return false;

        PlayCtrl playCtrl = PlayCtrl.Instance;
        return playCtrl != null && playCtrl.CheckState(GamePlayStateName.PLAYING);
    }

    private void CompleteCurrentQuestion()
    {
        if (!_sessionActive || _phase != QuizPhase.Question)
            return;

        InvalidatePointerInteractions();
        CancelCurrentDrag();
        _inputLocked = true;
        SetTimerRatio(0f);

        bool correct = IsCurrentEquationValid();
        if (correct)
            _correctCount++;
        else
            _wrongCount++;

        // 仅对刚刚判定的那一题进度点写状态并播反馈；正确/错误音效各一次，允许与切题/关窗/门锁重叠。
        SetProgressDotState(
            _currentQuestionIndex,
            correct ? ProgressDotState.Correct : ProgressDotState.Wrong);
        PlayJudgementSound(correct);
        PlayProgressDotFeedback(_currentQuestionIndex, correct);

        // 单题失败即结束：到时不成立用加长结果停留（约对齐 passwordError），随后系统关窗且不显示统计。
        // 全对路径：前四题 0.2s 中性切题；第五题正确后 0.2s FinalHold，再关窗+统计+完成链。
        if (!correct)
        {
            _phase = QuizPhase.FinalHold;
            _remainingTransitionTime = WrongFinalHoldDuration;
            return;
        }

        if (_currentQuestionIndex >= Questions.Length - 1)
        {
            _phase = QuizPhase.FinalHold;
            _remainingTransitionTime = TransitionDuration;
            return;
        }

        _phase = QuizPhase.Transition;
        _remainingTransitionTime = TransitionDuration;
    }

    private void CompleteFinalHold()
    {
        if (!_sessionActive || _phase != QuizPhase.FinalHold)
            return;

        EndSession(QuizCloseIntent.System);
    }

    private void CompleteTransition()
    {
        if (!_sessionActive || _phase != QuizPhase.Transition)
            return;

        _currentQuestionIndex++;
        LoadQuestion(_currentQuestionIndex);
    }

    /// <param name="questionIndex">本次会话的显示位索引（0..4），不是 Questions 源索引。</param>
    private void LoadQuestion(int questionIndex)
    {
        InvalidatePointerInteractions();
        // 只消费 BeginSession 已构造的顺序；此处不再随机。右上角 _contents[Top] 跨题保留。
        string[] question = Questions[_questionOrder[questionIndex]];
        for (int i = 0; i < EquationSlotCount; i++)
            _contents[i] = question[i];

        _phase = QuizPhase.Question;
        _remainingQuestionTime = QuestionDuration;
        _remainingTransitionTime = 0f;
        _inputLocked = false;
        _lastClockTime = RealtimeNow;
        RefreshAllContent();
        SetTimerRatio(1f);
    }

    private bool IsCurrentEquationValid()
    {
        if (_contents[3] == "=" && IsOperator(_contents[1]))
        {
            return TryParseOperand(_contents[0], out int left) &&
                   TryParseOperand(_contents[2], out int right) &&
                   TryParseOperand(_contents[4], out int result) &&
                   TryEvaluate(left, _contents[1], right, out int value) &&
                   value == result;
        }

        if (_contents[1] == "=" && IsOperator(_contents[3]))
        {
            return TryParseOperand(_contents[0], out int result) &&
                   TryParseOperand(_contents[2], out int left) &&
                   TryParseOperand(_contents[4], out int right) &&
                   TryEvaluate(left, _contents[3], right, out int value) &&
                   result == value;
        }

        return false;
    }

    private static bool TryParseOperand(string value, out int result)
    {
        return int.TryParse(value, out result);
    }

    private static bool IsOperator(string value)
    {
        return value == "+" || value == "-" || value == "×";
    }

    private static bool TryEvaluate(int left, string operation, int right, out int result)
    {
        switch (operation)
        {
            case "+":
                result = left + right;
                return true;
            case "-":
                result = left - right;
                return true;
            case "×":
                result = left * right;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    internal int InteractionVersion => _interactionVersion;

    internal void HandlePositionClick(int positionIndex, int interactionVersion)
    {
        if (positionIndex != TopPositionIndex || interactionVersion != _interactionVersion)
            return;

        AdvanceClockToNow();
        if (!CanInteract() || !_sessionActive || _dragSourceIndex >= 0 ||
            interactionVersion != _interactionVersion)
        {
            return;
        }

        EndSession(QuizCloseIntent.Active);
    }

    internal bool TryBeginDrag(int positionIndex, int interactionVersion, PointerEventData eventData)
    {
        if (interactionVersion != _interactionVersion)
            return false;

        AdvanceClockToNow();
        if (!CanInteract() || eventData == null || _dragSourceIndex >= 0 ||
            interactionVersion != _interactionVersion)
        {
            return false;
        }

        _dragSourceIndex = positionIndex;
        _dragPointerId = eventData.pointerId;
        SetPositionContentVisible(positionIndex, false);
        ShowDragVisual(_contents[positionIndex]);
        UpdateDragVisualPosition(eventData);
        return true;
    }

    internal void ContinueDrag(int positionIndex, PointerEventData eventData)
    {
        if (_dragSourceIndex != positionIndex || eventData == null || eventData.pointerId != _dragPointerId)
            return;

        UpdateDragVisualPosition(eventData);
    }

    internal void EndDrag(int positionIndex, int pointerId, PointerEventData eventData)
    {
        if (_dragSourceIndex != positionIndex || pointerId != _dragPointerId)
            return;

        AdvanceClockToNow();
        if (_dragSourceIndex != positionIndex || !CanInteract() || eventData == null)
        {
            CancelCurrentDrag();
            return;
        }

        int targetIndex = FindDropTarget(eventData);
        if (targetIndex >= 0 && targetIndex != positionIndex)
        {
            string source = _contents[positionIndex];
            _contents[positionIndex] = _contents[targetIndex];
            _contents[targetIndex] = source;
        }

        FinishCurrentDrag();
    }

    internal void CancelDrag(int positionIndex, int pointerId)
    {
        if (_dragSourceIndex == positionIndex && _dragPointerId == pointerId)
            CancelCurrentDrag();
    }

    private bool CanInteract()
    {
        return _sessionActive &&
               _phase == QuizPhase.Question &&
               !_inputLocked &&
               !_gamePaused &&
               !_applicationPaused &&
               _applicationFocused &&
               !_viewHidden &&
               CanAdvanceClock();
    }

    private int FindDropTarget(PointerEventData eventData)
    {
        for (int i = 0; i < _hitRects.Length; i++)
        {
            RectTransform hitRect = _hitRects[i];
            if (hitRect != null && RectTransformUtility.RectangleContainsScreenPoint(
                    hitRect,
                    eventData.position,
                    eventData.pressEventCamera))
            {
                return i;
            }
        }

        return -1;
    }

    private void FinishCurrentDrag()
    {
        _dragSourceIndex = -1;
        _dragPointerId = NoPointer;
        if (_dragVisualRoot != null)
            _dragVisualRoot.gameObject.SetActive(false);
        RefreshAllContent();
    }

    private void InvalidatePointerInteractions()
    {
        unchecked
        {
            _interactionVersion++;
        }
    }

    private void CancelCurrentDrag()
    {
        if (_dragSourceIndex < 0)
        {
            if (_dragVisualRoot != null)
                _dragVisualRoot.gameObject.SetActive(false);
            return;
        }

        FinishCurrentDrag();
    }

    private void EndSession(QuizCloseIntent closeIntent)
    {
        if (!_sessionActive)
            return;

        CancelCurrentDrag();
        KillAllProgressDotTweens();
        _sessionActive = false;
        _inputLocked = true;
        _phase = QuizPhase.None;
        _isClosing = true;

        int correct = _correctCount;
        int wrong = _wrongCount;
        bool completed = closeIntent == QuizCloseIntent.System && correct == Questions.Length && wrong == 0;
        // 统计：主动关闭或五题全对。错题固定提示：系统关闭且未完成（单题失败即结束）。
        bool showStatistics = closeIntent == QuizCloseIntent.Active || completed;
        bool showWrongAnswerTip = closeIntent == QuizCloseIntent.System && !completed && wrong > 0;
        Level115Ctrl owner = _owner;

        if (completed && owner != null)
            owner.PrepareQuizCompletion();

        UIManager.Instance.CloseUI(this);
        if (showStatistics)
            ShowStatistics(correct, wrong);
        else if (showWrongAnswerTip)
            ShowWrongAnswerTip();

        if (completed && owner != null)
            owner.NotifyQuizCompleted();
    }

    private static void ShowStatistics(int correct, int wrong)
    {
        string template = LocalizationManager.GetTranslation(StatisticsTerm);
        string content = template;
        if (!string.IsNullOrEmpty(template))
        {
            try
            {
                content = string.Format(template, correct, wrong);
            }
            catch (FormatException)
            {
                content = template;
            }
        }

        SystemTips.Show(content);
    }

    private static void ShowWrongAnswerTip()
    {
        string content = LocalizationManager.GetTranslation(WrongAnswerTipTerm);
        SystemTips.Show(content);
    }

    internal void CancelFromOwner(Level115Ctrl owner)
    {
        if (ReferenceEquals(_owner, owner))
            RequestSilentClose();
    }

    private void RequestSilentClose()
    {
        if (!IsOpen || _isClosing)
            return;

        _sessionActive = false;
        _inputLocked = true;
        _phase = QuizPhase.None;
        _isClosing = true;
        CancelCurrentDrag();
        KillAllProgressDotTweens();

        if (IsVisible)
        {
            UIManager.Instance.CloseUI(this);
            return;
        }

        _pendingSilentClose = true;
    }

    private void CloseSilentlyNow()
    {
        _pendingSilentClose = false;
        _isClosing = true;
        UIManager.Instance.CloseUI(this);
    }

    private async UniTaskVoid PauseForCoveringPopupAsync(int version)
    {
        await UniTask.Yield();

        if (version != _hideVersion || !_viewHidden || !_sessionActive || !IsOpen || _isClosing)
            return;

        PlayCtrl playCtrl = PlayCtrl.Instance;
        IPlayPauseHost pauseHost = GameManager.Instance?.ControllerManager?.ActivePauseHost;
        if (playCtrl == null || pauseHost == null || !playCtrl.CheckState(GamePlayStateName.PLAYING))
            return;

        _externalPauseOwned = true;
        pauseHost.Pause();
    }

    private void ResolveSerializedReferences()
    {
        SLeveltipsDesLevel115Txt ??= FindComponentByName<TextMeshProUGUI>("s_leveltips_des_level_115_txt");
        UiKaixueceyan05Txt ??= FindComponentByName<TextMeshProUGUI>("ui_kaixueceyan_05_txt");
        UiKaixueceyan04Txt ??= FindComponentByName<TextMeshProUGUI>("ui_kaixueceyan_04_txt");
        UiKaixueceyan03Txt ??= FindComponentByName<TextMeshProUGUI>("ui_kaixueceyan_03_txt");
        UiKaixueceyan02Txt ??= FindComponentByName<TextMeshProUGUI>("ui_kaixueceyan_02_txt");
        UiKaixueceyan01Txt ??= FindComponentByName<TextMeshProUGUI>("ui_kaixueceyan_01_txt");

        UIKaixueceyanX201Img ??= FindComponentByName<Image>("UI_kaixueceyan_img_X2_01");
        UIKaixueceyanX101Img ??= FindComponentByName<Image>("UI_kaixueceyan_img_X1_01");
        UIKaixueceyanShuzidikuang014Img ??= FindComponentByName<Image>("UI_kaixueceyan_img_shuzidikuang_01_4");
        UIKaixueceyanShuzidikuang013Img ??= FindComponentByName<Image>("UI_kaixueceyan_img_shuzidikuang_01_3");
        UIKaixueceyanShuzidikuang012Img ??= FindComponentByName<Image>("UI_kaixueceyan_img_shuzidikuang_01_2");
        UIKaixueceyanShuzidikuang011Img ??= FindComponentByName<Image>("UI_kaixueceyan_img_shuzidikuang_01_1");
        UIKaixueceyanShuzidikuang01Img ??= FindComponentByName<Image>("UI_kaixueceyan_img_shuzidikuang_01");

        QuizTimerBackground ??= FindComponentByName<Image>("UI_kaixueceyan_bar_1_01");
        QuizTimerFill ??= FindComponentByName<Image>("UI_kaixueceyan_bar_2_01");
        ResolveProgressDotReferences();
    }

    private void ResolveProgressDotReferences()
    {
        if (HasValidProgressDotArrays())
            return;

        ProgressDotBackgrounds = new Image[EquationSlotCount];
        ProgressDotCorrectIcons = new Image[EquationSlotCount];
        ProgressDotWrongIcons = new Image[EquationSlotCount];

        for (int i = 0; i < EquationSlotCount; i++)
        {
            Transform dotRoot = FindTransformByName($"ProgressDot_{i}");
            if (dotRoot == null)
                continue;

            ProgressDotBackgrounds[i] = FindChildImage(dotRoot, "UI_kaixueceyan_img_yuan_01");
            ProgressDotCorrectIcons[i] = FindChildImage(dotRoot, "UI_kaixueceyan_img_gou_01");
            ProgressDotWrongIcons[i] = FindChildImage(dotRoot, "UI_kaixueceyan_img_cha_01");
        }
    }

    private bool HasValidProgressDotArrays()
    {
        return ProgressDotBackgrounds != null && ProgressDotBackgrounds.Length == EquationSlotCount &&
               ProgressDotCorrectIcons != null && ProgressDotCorrectIcons.Length == EquationSlotCount &&
               ProgressDotWrongIcons != null && ProgressDotWrongIcons.Length == EquationSlotCount;
    }

    private static Image FindChildImage(Transform root, string childName)
    {
        if (root == null)
            return null;

        Transform child = root.Find(childName);
        if (child != null && child.TryGetComponent(out Image image))
            return image;

        return null;
    }

    private Transform FindTransformByName(string objectName)
    {
        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform child = descendants[i];
            if (child.name == objectName)
                return child;
        }

        return null;
    }

    private bool BuildRuntimeView()
    {
        _slotTexts = new[]
        {
            UiKaixueceyan05Txt,
            UiKaixueceyan04Txt,
            UiKaixueceyan03Txt,
            UiKaixueceyan02Txt,
            UiKaixueceyan01Txt,
        };

        _slotFrames = new[]
        {
            UIKaixueceyanShuzidikuang014Img,
            UIKaixueceyanShuzidikuang013Img,
            UIKaixueceyanShuzidikuang012Img,
            UIKaixueceyanShuzidikuang011Img,
            UIKaixueceyanShuzidikuang01Img,
        };

        if (SLeveltipsDesLevel115Txt == null || UIKaixueceyanX201Img == null ||
            UIKaixueceyanX101Img == null || QuizTimerBackground == null || QuizTimerFill == null)
        {
            Debug.LogError("[Level115QuizPopup] 必需的 Popup 引用缺失。");
            return false;
        }

        for (int i = 0; i < EquationSlotCount; i++)
        {
            if (_slotTexts[i] == null || _slotFrames[i] == null)
            {
                Debug.LogError($"[Level115QuizPopup] 等式槽位 {i} 引用缺失。");
                return false;
            }
        }

        SLeveltipsDesLevel115Txt.text = LocalizationManager.GetTranslation(InstructionTerm) ?? string.Empty;
        SLeveltipsDesLevel115Txt.raycastTarget = false;

        for (int i = 0; i < EquationSlotCount; i++)
            ApplyEquationTextOutline(_slotTexts[i]);

        _topContentText = CreateTopContentText();
        CreateSlotMultiplyImages();
        CreateDragVisual();
        ConfigureInteractionHandles();
        ConfigureProgressDots();
        SetTimerRatio(1f);
        ResetProgressDots();
        return true;
    }

    private void ConfigureProgressDots()
    {
        if (!HasValidProgressDotArrays())
        {
            Debug.LogWarning("[Level115QuizPopup] 五题进度点引用缺失；将跳过进度点刷新。");
            return;
        }

        _progressDotRoots = new RectTransform[EquationSlotCount];
        for (int i = 0; i < EquationSlotCount; i++)
        {
            DisableRaycast(ProgressDotBackgrounds[i]);
            DisableRaycast(ProgressDotCorrectIcons[i]);
            DisableRaycast(ProgressDotWrongIcons[i]);

            Image background = ProgressDotBackgrounds[i];
            Image correctIcon = ProgressDotCorrectIcons[i];
            Image wrongIcon = ProgressDotWrongIcons[i];
            if (background == null || correctIcon == null || wrongIcon == null)
                continue;

            RectTransform root = background.rectTransform.parent as RectTransform;
            if (root == null)
                root = background.rectTransform;

            _progressDotRoots[i] = root;
            _progressDotRootBaseScales[i] = root.localScale;
            _progressDotRootBaseAnchored[i] = root.anchoredPosition;
            _progressCorrectIconBaseScales[i] = correctIcon.rectTransform.localScale;
            _progressWrongIconBaseScales[i] = wrongIcon.rectTransform.localScale;
        }
    }

    private static void DisableRaycast(Image image)
    {
        if (image != null)
            image.raycastTarget = false;
    }

    private void ResetProgressDots()
    {
        KillAllProgressDotTweens();

        for (int i = 0; i < EquationSlotCount; i++)
            _progressDotStates[i] = ProgressDotState.Empty;

        RefreshProgressDots();
    }

    private void SetProgressDotState(int questionIndex, ProgressDotState state)
    {
        if (questionIndex < 0 || questionIndex >= EquationSlotCount)
            return;

        KillProgressDotTween(questionIndex);
        _progressDotStates[questionIndex] = state;
        RefreshProgressDot(questionIndex);
    }

    private void RefreshProgressDots()
    {
        for (int i = 0; i < EquationSlotCount; i++)
            RefreshProgressDot(i);
    }

    private void RefreshProgressDot(int questionIndex)
    {
        if (!HasValidProgressDotArrays() ||
            questionIndex < 0 ||
            questionIndex >= EquationSlotCount)
            return;

        Image background = ProgressDotBackgrounds[questionIndex];
        Image correctIcon = ProgressDotCorrectIcons[questionIndex];
        Image wrongIcon = ProgressDotWrongIcons[questionIndex];
        if (background == null || correctIcon == null || wrongIcon == null)
            return;

        RestoreProgressDotTransforms(questionIndex);

        ProgressDotState state = _progressDotStates[questionIndex];
        background.gameObject.SetActive(true);
        correctIcon.gameObject.SetActive(state == ProgressDotState.Correct);
        wrongIcon.gameObject.SetActive(state == ProgressDotState.Wrong);
    }

    /// <summary>
    /// 到时判题音效边界：只在 CompleteCurrentQuestion 调用一次；不随暂停/切题/关窗 Kill。
    /// </summary>
    private static void PlayJudgementSound(bool correct)
    {
        AudioManager.PlaySound(correct ? PasswordCorrectSfxTag : PasswordErrorSfxTag);
    }

    /// <summary>
    /// 仅对当前判定进度点播勾/叉弹出；正确点带回弹，错误点短促左右抖动后归位。
    /// </summary>
    private void PlayProgressDotFeedback(int questionIndex, bool correct)
    {
        if (!HasValidProgressDotArrays() ||
            _progressDotRoots == null ||
            questionIndex < 0 ||
            questionIndex >= EquationSlotCount)
        {
            return;
        }

        Image correctIcon = ProgressDotCorrectIcons[questionIndex];
        Image wrongIcon = ProgressDotWrongIcons[questionIndex];
        RectTransform root = _progressDotRoots[questionIndex];
        if (correctIcon == null || wrongIcon == null || root == null)
            return;

        KillProgressDotTween(questionIndex);
        RestoreProgressDotTransforms(questionIndex);

        Image activeIcon = correct ? correctIcon : wrongIcon;
        Image inactiveIcon = correct ? wrongIcon : correctIcon;
        activeIcon.gameObject.SetActive(true);
        inactiveIcon.gameObject.SetActive(false);

        RectTransform iconRect = activeIcon.rectTransform;
        Vector3 iconBaseScale = correct
            ? _progressCorrectIconBaseScales[questionIndex]
            : _progressWrongIconBaseScales[questionIndex];
        Vector3 rootBaseScale = _progressDotRootBaseScales[questionIndex];
        iconRect.localScale = Vector3.zero;

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .SetTarget(root);

        // 勾/叉从小弹出，时长对齐 0.2s 切题/结果停留窗口。
        sequence.Join(iconRect.DOScale(iconBaseScale, ProgressFeedbackDuration).SetEase(Ease.OutBack));

        if (correct)
        {
            float half = ProgressFeedbackDuration * 0.5f;
            sequence.Join(
                DOTween.Sequence()
                    .SetUpdate(true)
                    .Append(root.DOScale(rootBaseScale * CorrectDotBounceScale, half).SetEase(Ease.OutQuad))
                    .Append(root.DOScale(rootBaseScale, half).SetEase(Ease.InOutQuad)));
        }
        else
        {
            sequence.Join(
                root.DOShakeAnchorPos(
                        ProgressFeedbackDuration,
                        new Vector2(WrongDotShakeStrengthX, 0f),
                        vibrato: 18,
                        randomness: 0f,
                        snapping: false,
                        fadeOut: true)
                    .SetUpdate(true));
        }

        sequence.OnKill(() =>
        {
            if (_progressDotTweens[questionIndex] == sequence)
                _progressDotTweens[questionIndex] = null;
        });
        sequence.OnComplete(() =>
        {
            RestoreProgressDotTransforms(questionIndex);
            if (_progressDotTweens[questionIndex] == sequence)
                _progressDotTweens[questionIndex] = null;
        });

        _progressDotTweens[questionIndex] = sequence;

        // 若此刻已在暂停链/切后台，创建后立即冻结，恢复后再继续剩余时长。
        if (!ShouldPlayProgressDotFeedback())
            sequence.Pause();
    }

    private void KillAllProgressDotTweens()
    {
        for (int i = 0; i < EquationSlotCount; i++)
            KillProgressDotTween(i);
    }

    private void KillProgressDotTween(int questionIndex)
    {
        if (questionIndex < 0 || questionIndex >= EquationSlotCount)
            return;

        Tween tween = _progressDotTweens[questionIndex];
        _progressDotTweens[questionIndex] = null;
        if (tween != null && tween.IsActive())
            tween.Kill();

        RestoreProgressDotTransforms(questionIndex);
    }

    private void RestoreProgressDotTransforms(int questionIndex)
    {
        if (_progressDotRoots == null ||
            questionIndex < 0 ||
            questionIndex >= EquationSlotCount)
        {
            return;
        }

        RectTransform root = _progressDotRoots[questionIndex];
        if (root != null)
        {
            root.localScale = _progressDotRootBaseScales[questionIndex];
            root.anchoredPosition = _progressDotRootBaseAnchored[questionIndex];
        }

        if (!HasValidProgressDotArrays())
            return;

        Image correctIcon = ProgressDotCorrectIcons[questionIndex];
        if (correctIcon != null)
            correctIcon.rectTransform.localScale = _progressCorrectIconBaseScales[questionIndex];

        Image wrongIcon = ProgressDotWrongIcons[questionIndex];
        if (wrongIcon != null)
            wrongIcon.rectTransform.localScale = _progressWrongIconBaseScales[questionIndex];
    }

    private bool ShouldPlayProgressDotFeedback()
    {
        if (!_sessionActive || _gamePaused || _applicationPaused || !_applicationFocused || _viewHidden)
            return false;

        PlayCtrl playCtrl = PlayCtrl.Instance;
        return playCtrl != null && playCtrl.CheckState(GamePlayStateName.PLAYING);
    }

    /// <summary>
    /// 进度点 Tween 使用 unscaled，且项目 Pause 不改 timeScale，必须显式 Pause/Play。
    /// </summary>
    private void SyncProgressDotFeedbackPlayback()
    {
        bool shouldPlay = ShouldPlayProgressDotFeedback();
        for (int i = 0; i < EquationSlotCount; i++)
        {
            Tween tween = _progressDotTweens[i];
            if (tween == null || !tween.IsActive())
                continue;

            if (shouldPlay)
            {
                if (!tween.IsPlaying())
                    tween.Play();
            }
            else if (tween.IsPlaying())
            {
                tween.Pause();
            }
        }
    }

    private void ConfigureInteractionHandles()
    {
        _hitRects = new RectTransform[PositionCount];
        for (int i = 0; i < EquationSlotCount; i++)
        {
            _slotTexts[i].raycastTarget = false;
            _slotFrames[i].raycastTarget = true;
            _hitRects[i] = _slotFrames[i].rectTransform;
            ConfigureHandle(_slotFrames[i].gameObject, i);
        }

        UIKaixueceyanX201Img.raycastTarget = true;
        UIKaixueceyanX101Img.raycastTarget = false;
        _hitRects[TopPositionIndex] = UIKaixueceyanX201Img.rectTransform;
        ConfigureHandle(UIKaixueceyanX201Img.gameObject, TopPositionIndex);
    }

    private void ConfigureHandle(GameObject target, int positionIndex)
    {
        Level115QuizDragHandle handle = target.GetComponent<Level115QuizDragHandle>();
        handle ??= target.AddComponent<Level115QuizDragHandle>();
        handle.Configure(this, positionIndex);
    }

    private TextMeshProUGUI CreateTopContentText()
    {
        GameObject textObject = new GameObject("UI_kaixueceyan_top_txt", typeof(RectTransform));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(UIKaixueceyanX201Img.transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(76f, 76f);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        CopyTextStyle(_slotTexts[0], text);
        text.enableAutoSizing = true;
        text.fontSizeMin = 24f;
        text.fontSizeMax = 68f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        ApplyEquationTextOutline(text);
        textObject.SetActive(false);
        return text;
    }

    private void CreateSlotMultiplyImages()
    {
        _slotMultiplyImages = new Image[EquationSlotCount];
        for (int i = 0; i < EquationSlotCount; i++)
        {
            GameObject imageObject = new GameObject($"UI_kaixueceyan_slot_x1_{i}", typeof(RectTransform));
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.SetParent(_slotFrames[i].transform, false);
            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.sizeDelta = new Vector2(MultiplyImageSize, MultiplyImageSize);

            Image image = imageObject.AddComponent<Image>();
            image.sprite = UIKaixueceyanX101Img.sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            imageObject.SetActive(false);
            _slotMultiplyImages[i] = image;
        }
    }

    private void CreateDragVisual()
    {
        GameObject rootObject = new GameObject("DragContent", typeof(RectTransform), typeof(CanvasGroup));
        _dragVisualRoot = rootObject.GetComponent<RectTransform>();
        _dragVisualRoot.SetParent(transform, false);
        _dragVisualRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _dragVisualRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _dragVisualRoot.pivot = new Vector2(0.5f, 0.5f);
        _dragVisualRoot.sizeDelta = new Vector2(160f, 160f);

        CanvasGroup canvasGroup = rootObject.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject imageObject = new GameObject("MultiplyImage", typeof(RectTransform));
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.SetParent(_dragVisualRoot, false);
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = Vector2.zero;
        imageRect.sizeDelta = new Vector2(MultiplyImageSize, MultiplyImageSize);
        _dragVisualImage = imageObject.AddComponent<Image>();
        _dragVisualImage.sprite = UIKaixueceyanX101Img.sprite;
        _dragVisualImage.preserveAspect = true;
        _dragVisualImage.raycastTarget = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(_dragVisualRoot, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;
        _dragVisualText = textObject.AddComponent<TextMeshProUGUI>();
        CopyTextStyle(_slotTexts[0], _dragVisualText);
        _dragVisualText.alignment = TextAlignmentOptions.Center;
        _dragVisualText.raycastTarget = false;
        ApplyEquationTextOutline(_dragVisualText);

        rootObject.SetActive(false);
    }

    private static void CopyTextStyle(TextMeshProUGUI source, TextMeshProUGUI destination)
    {
        destination.font = source.font;
        destination.fontSharedMaterial = source.fontSharedMaterial;
        destination.color = source.color;
        destination.fontSize = source.fontSize;
        destination.fontStyle = source.fontStyle;
        destination.alignment = source.alignment;
        destination.overflowMode = TextOverflowModes.Overflow;
    }

    /// <summary>
    /// 等式相关 TMP（五槽、右上角非叉内容、拖拽字）统一套 #665a4f、约 4px 外侧描边。
    /// 使用 fontMaterial 实例，避免改到字体共享材质；叉图 X1 自带描边，不走此路径。
    /// </summary>
    private static void ApplyEquationTextOutline(TMP_Text text)
    {
        if (text == null)
            return;

        text.outlineColor = EquationOutlineColor;
        text.outlineWidth = EquationOutlineWidth;

        // 与 Level 78 相同：描边默认居中吃字芯，同步 FaceDilate 让 4px 落在外侧。
        text.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, EquationOutlineWidth);
        text.UpdateMeshPadding();
    }

    private void SetTimerRatio(float ratio)
    {
        if (QuizTimerFill == null)
            return;

        // Prefab 已将内层配成 Horizontal Filled / Origin=Left；此处只驱动剩余比例。
        QuizTimerFill.fillAmount = Mathf.Clamp01(ratio);
    }

    private static bool IsMultiplyContent(string content)
    {
        return content == InitialTopContent;
    }

    private void RefreshAllContent()
    {
        if (_slotTexts == null || _slotMultiplyImages == null)
            return;

        for (int i = 0; i < EquationSlotCount; i++)
            RefreshSlotContent(i);

        RefreshTopContent();
    }

    private void RefreshSlotContent(int slotIndex)
    {
        string content = _contents[slotIndex];
        bool showMultiplyImage = IsMultiplyContent(content);

        _slotMultiplyImages[slotIndex].gameObject.SetActive(showMultiplyImage);
        _slotTexts[slotIndex].gameObject.SetActive(!showMultiplyImage);
        if (!showMultiplyImage)
            _slotTexts[slotIndex].text = content;
    }

    private void RefreshTopContent()
    {
        string content = _contents[TopPositionIndex];
        bool showMultiplyImage = IsMultiplyContent(content);

        UIKaixueceyanX101Img.gameObject.SetActive(showMultiplyImage);
        _topContentText.gameObject.SetActive(!showMultiplyImage);
        if (!showMultiplyImage)
            _topContentText.text = content;
    }

    private void SetPositionContentVisible(int positionIndex, bool visible)
    {
        if (positionIndex < EquationSlotCount)
        {
            if (!visible)
            {
                _slotTexts[positionIndex].gameObject.SetActive(false);
                _slotMultiplyImages[positionIndex].gameObject.SetActive(false);
                return;
            }

            RefreshSlotContent(positionIndex);
            return;
        }

        if (!visible)
        {
            UIKaixueceyanX101Img.gameObject.SetActive(false);
            _topContentText.gameObject.SetActive(false);
            return;
        }

        RefreshTopContent();
    }

    private void ShowDragVisual(string content)
    {
        _dragVisualRoot.gameObject.SetActive(true);
        _dragVisualRoot.SetAsLastSibling();

        bool showMultiplyImage = IsMultiplyContent(content);
        _dragVisualImage.gameObject.SetActive(showMultiplyImage);
        _dragVisualText.gameObject.SetActive(!showMultiplyImage);
        if (!showMultiplyImage)
            _dragVisualText.text = content;
    }

    private void UpdateDragVisualPosition(PointerEventData eventData)
    {
        RectTransform root = transform as RectTransform;
        if (root == null || _dragVisualRoot == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                root,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            _dragVisualRoot.anchoredPosition = localPoint;
        }
    }

    private T FindComponentByName<T>(string objectName) where T : Component
    {
        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform child = descendants[i];
            if (child.name == objectName && child.TryGetComponent(out T component))
                return component;
        }

        return null;
    }

    private static double RealtimeNow => Time.realtimeSinceStartupAsDouble;

    public static Level115QuizPopup Create(Level115Ctrl owner)
    {
        Level115QuizPopup panel = UIManager.Instance.OpenUI<Level115QuizPopup>(
            nameof(Level115QuizPopup),
            UILayer.Popup,
            isShowMask: true,
            isCloseByMask: false);

        if (panel == null)
            return null;

        if (panel._sessionActive)
            return panel;

        if (panel.BeginSession(owner))
            return panel;

        UIManager.Instance.CloseUI(panel);
        return null;
    }
}

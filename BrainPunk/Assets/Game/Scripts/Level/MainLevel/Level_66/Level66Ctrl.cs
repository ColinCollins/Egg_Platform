using System.Collections.Generic;
using System.Threading;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level 66 local controller for the hat trick puzzle.
/// 狗和钥匙一开始需要显示在场景中。三个帽子在 1.5s 动画播放完成后，狗和钥匙需要隐藏。然后播放帽子戏法的动画，用程序控制就可以。
/// 点击帽子之后，帽子需要有一个掀开的动画，然后渐隐消失。
/// 增加测试按钮，需要两个 GUI 按钮，editor 使用，触发两个帽子和触发三个帽子
/// </summary>
public class Level66Ctrl : MonoBehaviour
{
    private static readonly int FightAnimStopHash = Animator.StringToHash("isStop");
    #region 类型与绑定数据

    /// <summary>
    /// Level66 本地流程状态，用于限制点击、调试按钮、狗移动和清理时机。
    /// </summary>
    private enum Level66State
    {
        Idle,
        OpeningLocked,
        Shuffling,
        WaitingForFirstClick,
        SelectionWindow,
        Revealing,
        DogChase,
        DogConfrontationDelay,
        DogConfrontationMoving,
        FightShown,
        Inactive
    }

    /// <summary>
    /// 三顶帽子的稳定身份，点击顺序和验证分支都使用这个身份记录。
    /// </summary>
    private enum HatId
    {
        Hat1 = 0,
        Hat2 = 1,
        Hat3 = 2
    }

    /// <summary>
    /// 单顶帽子的场景引用与运行时缓存。缓存内容用于重开后恢复位置、缩放、显隐和透明度。
    /// </summary>
    [System.Serializable]
    private class HatBinding
    {
        public HatId id;
        public Transform hat;
        public ClickItemEventHandle clickHandle;

        [System.NonSerialized] public Vector3 initialLocalPosition;
        [System.NonSerialized] public Vector3 initialLocalScale;
        [System.NonSerialized] public bool initialActive;
        [System.NonSerialized] public SpriteRenderer[] visualRenderers;
        [System.NonSerialized] public Color[] initialRendererColors;
        [System.NonSerialized] public Vector3 revealWorldPosition;
        [System.NonSerialized] public bool hasRevealWorldPosition;
    }

    /// <summary>
    /// 狗对象及其移动控制引用。Level66 只驱动移动输入，不改共享狗组件行为。
    /// </summary>
    [System.Serializable]
    private class DogBinding
    {
        public Transform root;
        public DogEnemyCtrl movement;

        [System.NonSerialized] public Vector3 initialLocalPosition;
        [System.NonSerialized] public Vector3 initialLocalScale;
        [System.NonSerialized] public Quaternion initialLocalRotation;
        [System.NonSerialized] public bool initialActive;
        [System.NonSerialized] public PuppyEnemyCtrl dangerCtrl;
        [System.NonSerialized] public bool initialDangerEnabled;
    }

    #endregion

    #region Inspector 配置

    [Header("Level References")]
    [SerializeField] private BaseLevelCtrl baseLevelCtrl;
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private Animator hatsAnimator;

    [Header("Hats")]
    [SerializeField] private HatBinding[] hats = new HatBinding[3];

    [Header("Reveal Objects")]
    [SerializeField] private Transform keyRoot;
    [SerializeField] private DogBinding dogA;
    [SerializeField] private DogBinding dogB;
    [SerializeField] private Transform fightPresentation;

    [Header("Timing")]
    [SerializeField] private Level66ActorCtrl level66Actor;
    [SerializeField] private float openingLockSeconds = 1.5f;
    [SerializeField] private float selectionWindowSeconds = 0.2f;
    [SerializeField] private float twoDogWaitSeconds = 1f;

    [Header("Shuffle")]
    [SerializeField] private int shuffleSwapCount = 6;
    [Tooltip("shuffleSwapDuration.x 为单次洗牌最短时间，y 为最长时间；每次交换会按洗牌次数从最长逐渐过渡到最短。")]
    [SerializeField] private Vector2 shuffleSwapDuration = new Vector2(0.1f, 1f);
    [Tooltip("控制洗牌单次时长的过渡曲线。x 为洗牌进度，y 为从最长时间过渡到最短时间的比例。")]
    [SerializeField] private AnimationCurve shuffleDurationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private Ease shuffleEase = Ease.Linear;

    [Header("Presentation")]
    [SerializeField] private float clickFeedbackScale = 1.08f;
    [SerializeField] private float clickFeedbackDuration = 0.08f;
    [SerializeField] private float hatOpenLocalYOffset = 0.28f;
    [SerializeField] private float hatOpenScale = 0.92f;
    [SerializeField] private float hatOpenDuration = 0.12f;
    [SerializeField] private float hatFadeDuration = 0.18f;
    [SerializeField] private float revealYOffset = 0.1f;

    [Header("狗打架")]
    [Tooltip("两狗互撞打架触发最短距离")]
    [SerializeField] private float dogTouchDistance = 0.75f;
    [SerializeField] private float fightDuration = 2f;
    [SerializeField] private Animator fightAnims;
    [SerializeField] private Transform[] dogsDeadBodys;

    #endregion

    #region 运行时状态

    private readonly List<HatBinding> _selectedHats = new List<HatBinding>(3);
    private readonly Vector3[] _hatPositions = new Vector3[3];
    private readonly int[] _hatPositionByIndex = { 0, 1, 2 };
    private readonly int[] _shuffleBuildPositionByHat = { 0, 1, 2 };

    private EventSubscriber _subscriber;
    private CancellationTokenSource _sessionCts;
    private Sequence _shuffleSequence;
    private Sequence _revealSequence;
    private Level66State _state = Level66State.Idle;
    private Vector3 _keyInitialLocalPosition;
    private Vector3 _keyInitialLocalScale;
    private Quaternion _keyInitialLocalRotation;
    private bool _keyInitialActive;
    private bool _cachedInitialState;
    private bool _started;
    private bool _isPaused;
    private bool _isShuttingDown;
    private bool _outcomeResolved;
    private bool _editorValidationTriggered;
    private DogBinding _chasingDog;
    private DogBinding _leftConfrontDog;
    private DogBinding _rightConfrontDog;
    private bool[] _deadBodyInitialActiveStates;

    private CancellationToken SessionToken => _sessionCts != null ? _sessionCts.Token : this.GetCancellationTokenOnDestroy();

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        ResolveReferences();
        CacheInitialState();
        RegisterEvents();
        RestoreInitialState(false);
    }

    private void Start()
    {
        _started = true;
        StartLevelSession();
    }

    private void OnEnable()
    {
        if (!_started)
            return;

        RegisterEvents();
        StartLevelSession();
    }

    private void Update()
    {
        if (_isPaused)
            return;

        UpdateDogChase();
        UpdateDogConfrontation();
        UpdateDogsOnce();
        CheckDogConfrontationReached();
    }

    private void OnDisable()
    {
        if (_isShuttingDown)
            return;

        StopActiveRuntime(true);
    }

    private void OnDestroy()
    {
        _isShuttingDown = true;
        StopActiveRuntime(true);
        EventsUtils.ResetEvents(ref _subscriber);
    }

    #endregion

    #region 帽子点击与 Editor 验证入口

    public void ClickHat1(Transform clicker, Vector2 worldPoint)
    {
        OnHatClicked(HatId.Hat1);
    }

    public void ClickHat2(Transform clicker, Vector2 worldPoint)
    {
        OnHatClicked(HatId.Hat2);
    }

    public void ClickHat3(Transform clicker, Vector2 worldPoint)
    {
        OnHatClicked(HatId.Hat3);
    }

    public void ClickHat1()
    {
        OnHatClicked(HatId.Hat1);
    }

    public void ClickHat2()
    {
        OnHatClicked(HatId.Hat2);
    }

    public void ClickHat3()
    {
        OnHatClicked(HatId.Hat3);
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!CanTriggerEditorValidation())
            return;

        GUILayout.BeginArea(new Rect(16f, 16f, 180f, 72f), GUI.skin.box);
        if (GUILayout.Button("Level66 Two Hats"))
            TriggerEditorValidationBranch(2);
        if (GUILayout.Button("Level66 Three Hats"))
            TriggerEditorValidationBranch(3);
        GUILayout.EndArea();
    }
#endif

    #endregion

    #region 事件订阅与关卡生命周期控制

    private void RegisterEvents()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        // _subscriber.Subscribe<GameResetEvent>(OnGameReset);
        _subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        _subscriber.Subscribe<GameResumeEvent>(OnGameResume);
        _subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        _isPaused = true;
        PauseTweens();
        StopLevelOwnedDogMovement();
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        _isPaused = false;
        ResumeTweens();
        ResumeLevelOwnedDogMovement();
    }

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        if (_isPaused || _state != Level66State.DogChase || _chasingDog == null)
            return;

        if (_chasingDog.root == null || !_chasingDog.root.gameObject.activeInHierarchy || _chasingDog.movement == null)
            return;

        _chasingDog.movement.TriggerJump();
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt == null)
            return;

        if (evt.NewState == GamePlayStateName.PAUSE)
        {
            _isPaused = true;
            PauseTweens();
            StopLevelOwnedDogMovement();
            return;
        }

        if (evt.NewState == GamePlayStateName.PLAYING)
        {
            _isPaused = false;
            ResumeTweens();
            ResumeLevelOwnedDogMovement();
            return;
        }

    }

    #endregion

    #region Session 启停与清理

    /// <summary>
    /// 开启一次新的 Level66 尝试：恢复初始状态，创建新的取消令牌，再进入开场流程。
    /// </summary>
    private void StartLevelSession()
    {
        if (!isActiveAndEnabled)
            return;

        ResolveReferences();
        CacheInitialState();
        StopActiveRuntime(false);
        RestoreInitialState(true);
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        RunOpeningFlowAsync(SessionToken).Forget();
    }

    /// <summary>
    /// 停止当前尝试拥有的所有异步任务、DOTween 和狗移动，避免旧回调影响下一次进入。
    /// </summary>
    private void StopActiveRuntime(bool restore)
    {
        CancelSessionTasks();
        KillTweens();
        StopLevelOwnedDogMovement();
        _selectedHats.Clear();
        _outcomeResolved = false;
        _editorValidationTriggered = false;
        _chasingDog = null;
        _leftConfrontDog = null;
        _rightConfrontDog = null;
        SetHatInteraction(false);

        if (restore)
            RestoreInitialState(true);

        _state = Level66State.Inactive;
    }

    private void CancelSessionTasks()
    {
        if (_sessionCts == null)
            return;

        if (!_sessionCts.IsCancellationRequested)
            _sessionCts.Cancel();

        _sessionCts.Dispose();
        _sessionCts = null;
    }

    private void KillTweens()
    {
        if (_shuffleSequence != null)
        {
            _shuffleSequence.Kill();
            _shuffleSequence = null;
        }

        if (_revealSequence != null)
        {
            _revealSequence.Kill();
            _revealSequence = null;
        }

        foreach (HatBinding hat in hats)
        {
            if (hat == null || hat.hat == null)
                continue;

            hat.hat.DOKill();
            KillHatVisualTweens(hat);
        }
    }

    private void PauseTweens()
    {
        _shuffleSequence?.Pause();
        _revealSequence?.Pause();

        foreach (HatBinding hat in hats)
        {
            if (hat == null || hat.hat == null)
                continue;

            hat.hat.DOPause();
            PauseHatVisualTweens(hat);
        }

    }

    private void ResumeTweens()
    {
        _shuffleSequence?.Play();
        _revealSequence?.Play();

        foreach (HatBinding hat in hats)
        {
            if (hat == null || hat.hat == null)
                continue;

            hat.hat.DOPlay();
            ResumeHatVisualTweens(hat);
        }

    }

    #endregion

    #region 开场展示与洗牌流程

    /// <summary>
    /// 开场 1.5 秒显示钥匙和两只狗，期间锁住玩家和帽子；结束后隐藏对象并启动洗牌。
    /// </summary>
    private async UniTaskVoid RunOpeningFlowAsync(CancellationToken cancellationToken)
    {
        _state = Level66State.OpeningLocked;
        _isPaused = false;
        LockActorControl(true);
        SetHatInteraction(false);
        ShowOpeningObjects();

        if (hatsAnimator != null)
            hatsAnimator.enabled = true;

        if (!HasMinimumReferences())
        {
            Debug.LogWarning("[Level66Ctrl] Missing required references. Level66 hat flow is waiting for prefab binding.", this);
            _state = Level66State.WaitingForFirstClick;
            LockActorControl(false);
            SetHatInteraction(true);
            return;
        }

        try
        {
            level66Actor.PlayMagic();
            await WaitLevelSecondsAsync(Mathf.Max(0f, openingLockSeconds), cancellationToken);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || !isActiveAndEnabled)
            return;

        if (hatsAnimator != null)
            hatsAnimator.enabled = false;

        ConcealRevealObjects();
        PlayShuffleAfterOpening(cancellationToken).Forget();
    }

    /// <summary>
    /// 初始化洗牌位置缓存。开场 Animator 结束后由程序接管三顶帽子的 Transform。
    /// </summary>
    private bool PrepareShuffleState()
    {
        if (!HasAllHats())
            return false;

        if (_shuffleSequence != null)
        {
            _shuffleSequence.Kill();
            _shuffleSequence = null;
        }

        for (int i = 0; i < hats.Length; i++)
        {
            _hatPositions[i] = hats[i].hat.position;
            _hatPositionByIndex[i] = i;
            _shuffleBuildPositionByHat[i] = i;
        }

        _state = Level66State.Shuffling;
        return true;
    }

    /// <summary>
    /// 按配置次数逐次随机交换帽子，每次都等待当前交换完成后再计算下一次。
    /// </summary>
    private async UniTaskVoid PlayShuffleAfterOpening(CancellationToken cancellationToken)
    {
        if (!PrepareShuffleState())
        {
            EnterWaitingForFirstClick();
            return;
        }

        try
        {
            int swapCount = Mathf.Max(1, shuffleSwapCount);

            for (int i = 0; i < swapCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float duration = CalculateShuffleSwapDuration(i, swapCount);
                SelectRandomHatPair(out int first, out int second);
                await PlayHatSwapAsync(first, second, duration, cancellationToken);
            }
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || !isActiveAndEnabled)
            return;

        EnterWaitingForFirstClick();
    }

    private float CalculateShuffleSwapDuration(int swapIndex, int swapCount)
    {
        float minDuration = Mathf.Max(0.01f, Mathf.Min(shuffleSwapDuration.x, shuffleSwapDuration.y));
        float maxDuration = Mathf.Max(0.01f, Mathf.Max(shuffleSwapDuration.x, shuffleSwapDuration.y));

        if (swapCount <= 1)
            return maxDuration;

        float normalizedIndex = Mathf.Clamp01((float)swapIndex / (swapCount - 1));
        float curveProgress = shuffleDurationCurve != null ? shuffleDurationCurve.Evaluate(normalizedIndex) : normalizedIndex;
        return Mathf.Lerp(maxDuration, minDuration, Mathf.Clamp01(curveProgress));
    }

    private void EnterWaitingForFirstClick()
    {
        if (_outcomeResolved)
            return;

        _state = Level66State.WaitingForFirstClick;
        LockActorControl(false);
        SetHatInteraction(true);
    }

    /// <summary>
    /// 将两顶帽子沿水平位置互换，并在动画完成后同步内部“帽子 -> 当前位置”映射。
    /// </summary>
    private async UniTask PlayHatSwapAsync(int firstHatIndex, int secondHatIndex, float duration, CancellationToken cancellationToken)
    {
        if (firstHatIndex == secondHatIndex)
            return;

        HatBinding firstHat = hats[firstHatIndex];
        HatBinding secondHat = hats[secondHatIndex];

        if (firstHat == null || secondHat == null || firstHat.hat == null || secondHat.hat == null)
            return;

        int firstPositionIndex = _shuffleBuildPositionByHat[firstHatIndex];
        int secondPositionIndex = _shuffleBuildPositionByHat[secondHatIndex];
        Vector3 firstStart = BuildXOnlyTarget(firstHat.hat.localPosition, _hatPositions[firstPositionIndex]);
        Vector3 secondStart = BuildXOnlyTarget(secondHat.hat.localPosition, _hatPositions[secondPositionIndex]);
        Vector3 firstTarget = BuildXOnlyTarget(firstStart, _hatPositions[secondPositionIndex]);
        Vector3 secondTarget = BuildXOnlyTarget(secondStart, _hatPositions[firstPositionIndex]);

        firstHat.hat.localPosition = firstStart;
        secondHat.hat.localPosition = secondStart;
        _shuffleSequence?.Kill();
        bool completed = false;
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        _shuffleSequence = sequence;
        sequence.Append(firstHat.hat.DOLocalMove(firstTarget, duration).SetEase(shuffleEase));
        sequence.Join(secondHat.hat.DOLocalMove(secondTarget, duration).SetEase(shuffleEase));
        sequence.JoinCallback(() =>
        {
            AudioManager.PlaySound("level66Shuffle");
        });
        sequence.OnComplete(() => completed = true);

        while (!completed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sequence == null || !sequence.IsActive())
                break;

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        if (cancellationToken.IsCancellationRequested || !isActiveAndEnabled)
            return;

        firstHat.hat.localPosition = firstTarget;
        secondHat.hat.localPosition = secondTarget;
        _shuffleBuildPositionByHat[firstHatIndex] = secondPositionIndex;
        _shuffleBuildPositionByHat[secondHatIndex] = firstPositionIndex;
        _hatPositionByIndex[firstHatIndex] = secondPositionIndex;
        _hatPositionByIndex[secondHatIndex] = firstPositionIndex;
        if (_shuffleSequence == sequence)
        {
            _shuffleSequence.Kill();
            _shuffleSequence = null;
        }
    }

    /// <summary>
    /// 随机选择两顶不同帽子。第二个索引用偏移法生成，避免 while 重抽。
    /// </summary>
    private void SelectRandomHatPair(out int firstHatIndex, out int secondHatIndex)
    {
        int hatCount = Mathf.Max(1, hats.Length);
        firstHatIndex = Random.Range(0, hatCount);
        secondHatIndex = Random.Range(0, hatCount - 1);
        if (secondHatIndex >= firstHatIndex)
            secondHatIndex++;
    }

    private Vector3 BuildXOnlyTarget(Vector3 current, Vector3 targetPosition)
    {
        current.x = targetPosition.x;
        return current;
    }

    #endregion

    #region 选择窗口与 Editor 验证分支

    private void OnHatClicked(HatId hatId)
    {
        if (_state != Level66State.WaitingForFirstClick && _state != Level66State.SelectionWindow)
            return;

        HatBinding hat = GetHat(hatId);
        if (hat == null || hat.hat == null)
            return;

        if (_selectedHats.Contains(hat))
            return;

        _selectedHats.Add(hat);
        PlayHatClickFeedback(hat);

        if (_state == Level66State.WaitingForFirstClick)
        {
            _state = Level66State.SelectionWindow;
            RunSelectionWindowAsync(SessionToken).Forget();
        }

        AudioManager.PlaySound("level64Block");
    }

    /// <summary>
    /// Editor 验证按钮只能在洗牌完成、等待首次点击、未暂停且本次尝试未结算时触发。
    /// </summary>
    private bool CanTriggerEditorValidation()
    {
        return Application.isPlaying
               && isActiveAndEnabled
               && !_isPaused
               && !_outcomeResolved
               && !_editorValidationTriggered
               && _state == Level66State.WaitingForFirstClick;
    }

    /// <summary>
    /// 使用当前内部洗牌顺序模拟两帽或三帽选择，并复用正常结算流程。
    /// </summary>
    private void TriggerEditorValidationBranch(int hatCount)
    {
        if (!CanTriggerEditorValidation())
            return;

        _selectedHats.Clear();
        AddHatsByCurrentInternalOrder(Mathf.Clamp(hatCount, 1, 3));
        if (_selectedHats.Count <= 0)
        {
            Debug.LogWarning("[Level66Ctrl] Editor validation skipped because no hats are available.", this);
            return;
        }

        _editorValidationTriggered = true;
        ResolveSelectionOutcome();
    }

    /// <summary>
    /// 按当前内部位置顺序取帽子，保证验证按钮使用洗牌后的实际顺序。
    /// </summary>
    private void AddHatsByCurrentInternalOrder(int hatCount)
    {
        if (hats == null)
            return;

        int targetCount = Mathf.Min(hatCount, hats.Length);
        for (int positionIndex = 0; positionIndex < hats.Length && _selectedHats.Count < targetCount; positionIndex++)
        {
            for (int hatIndex = 0; hatIndex < hats.Length; hatIndex++)
            {
                if (hatIndex >= _hatPositionByIndex.Length || _hatPositionByIndex[hatIndex] != positionIndex)
                    continue;

                HatBinding hat = hats[hatIndex];
                if (hat != null && hat.hat != null && !_selectedHats.Contains(hat))
                    _selectedHats.Add(hat);

                break;
            }
        }
    }

    private async UniTaskVoid RunSelectionWindowAsync(CancellationToken cancellationToken)
    {
        try
        {
            await WaitLevelSecondsAsync(Mathf.Max(0f, selectionWindowSeconds), cancellationToken);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || !isActiveAndEnabled)
            return;

        ResolveSelectionOutcome();
    }

    private void ResolveSelectionOutcome()
    {
        if (_outcomeResolved)
            return;

        _outcomeResolved = true;
        _state = Level66State.Revealing;
        SetHatInteraction(false);
        ResolveSelectionOutcomeAsync(SessionToken).Forget();
    }

    /// <summary>
    /// 等帽子打开和淡出完成后再揭示狗/钥匙，避免帽子挡住结果对象。
    /// </summary>
    private async UniTaskVoid ResolveSelectionOutcomeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await OpenSelectedHatsAsync(cancellationToken);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || !isActiveAndEnabled)
            return;

        if (_selectedHats.Count <= 0)
        {
            _state = Level66State.WaitingForFirstClick;
            _outcomeResolved = false;
            SetHatInteraction(true);
            return;
        }

        if (_selectedHats.Count == 1)
        {
            RevealDogAtHat(dogA, _selectedHats[0]);
            StartDogChase(dogA);
            return;
        }

        if (_selectedHats.Count == 2)
        {
            RevealDogAtHat(dogA, _selectedHats[0]);
            RevealKeyAtHat(_selectedHats[1]);
            StartDogChase(dogA);
            return;
        }

        RevealDogAtHat(dogA, _selectedHats[0]);
        RevealKeyAtHat(_selectedHats[1]);
        RevealDogAtHat(dogB, _selectedHats[2]);
        StartDogConfrontationAsync(SessionToken).Forget();
    }

    #endregion

    #region 帽子打开、淡出与视觉缓存

    private void PlayHatClickFeedback(HatBinding hat)
    {
        if (hat == null || hat.hat == null)
            return;

        hat.hat.DOKill();
        hat.hat.localScale = hat.initialLocalScale;
        hat.hat
            .DOScale(hat.initialLocalScale * Mathf.Max(0.01f, clickFeedbackScale), Mathf.Max(0.01f, clickFeedbackDuration))
            .SetEase(Ease.OutQuad)
            .SetLoops(2, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    /// <summary>
    /// 选中的帽子先轻微上移/缩放作为掀开表现，然后 SpriteRenderer 透明度渐隐到 0。
    /// </summary>
    private async UniTask OpenSelectedHatsAsync(CancellationToken cancellationToken)
    {
        if (_revealSequence != null)
            _revealSequence.Kill();

        if (_selectedHats.Count <= 0)
            return;

        bool completed = false;
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        _revealSequence = sequence;
        float openDuration = Mathf.Max(0.01f, hatOpenDuration);
        foreach (HatBinding hat in _selectedHats)
        {
            if (hat == null || hat.hat == null)
                continue;

            hat.hat.DOKill();
            hat.revealWorldPosition = hat.hat.position;
            hat.hasRevealWorldPosition = true;
            Vector3 openPosition = hat.hat.localPosition + Vector3.up * hatOpenLocalYOffset;
            sequence.Insert(0f, hat.hat.DOLocalMove(openPosition, openDuration).SetEase(Ease.OutQuad));
            sequence.Insert(0f, hat.hat.DOScale(hat.initialLocalScale * Mathf.Max(0.01f, hatOpenScale), openDuration).SetEase(Ease.OutQuad));
            InsertHatFade(hat, sequence, openDuration);
        }

        sequence.OnComplete(() => completed = true);

        while (!completed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sequence == null || !sequence.IsActive())
                break;

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        if (cancellationToken.IsCancellationRequested || !isActiveAndEnabled)
            return;

        HideSelectedHatsAfterFade();
        if (_revealSequence == sequence)
        {
            _revealSequence.Kill();
            _revealSequence = null;
        }
    }

    /// <summary>
    /// 将帽子下所有 SpriteRenderer 插入同一淡出时间点，保持多层帽子视觉同步消失。
    /// </summary>
    private void InsertHatFade(HatBinding hat, Sequence sequence, float startTime)
    {
        if (hat == null || sequence == null || hat.visualRenderers == null)
            return;

        float duration = Mathf.Max(0.01f, hatFadeDuration);
        for (int i = 0; i < hat.visualRenderers.Length; i++)
        {
            SpriteRenderer renderer = hat.visualRenderers[i];
            if (renderer == null)
                continue;

            sequence.Insert(startTime, renderer.DOFade(0f, duration).SetEase(Ease.OutQuad));
        }
    }

    /// <summary>
    /// 淡出结束后隐藏帽子对象并关闭点击，直到重开或重新进入恢复。
    /// </summary>
    private void HideSelectedHatsAfterFade()
    {
        foreach (HatBinding hat in _selectedHats)
        {
            if (hat == null || hat.hat == null)
                continue;

            if (hat.clickHandle != null)
            {
                hat.clickHandle.ResetClickCount();
                hat.clickHandle.SetEnableClick(false);
            }

            hat.hat.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 缓存帽子所有 SpriteRenderer 的初始颜色，主要用于恢复淡出后的 alpha。
    /// </summary>
    private void CacheHatVisualState(HatBinding hat)
    {
        if (hat == null || hat.hat == null)
            return;

        hat.visualRenderers = hat.hat.GetComponentsInChildren<SpriteRenderer>(true);
        if (hat.visualRenderers == null || hat.visualRenderers.Length == 0)
        {
            hat.initialRendererColors = System.Array.Empty<Color>();
            return;
        }

        hat.initialRendererColors = new Color[hat.visualRenderers.Length];
        for (int i = 0; i < hat.visualRenderers.Length; i++)
            hat.initialRendererColors[i] = hat.visualRenderers[i] != null ? hat.visualRenderers[i].color : Color.white;
    }

    /// <summary>
    /// 恢复帽子透明度和颜色，防止上一轮淡出状态残留到下一轮。
    /// </summary>
    private void RestoreHatVisualState(HatBinding hat)
    {
        if (hat == null || hat.visualRenderers == null || hat.initialRendererColors == null)
            return;

        int count = Mathf.Min(hat.visualRenderers.Length, hat.initialRendererColors.Length);
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer renderer = hat.visualRenderers[i];
            if (renderer == null)
                continue;

            renderer.color = hat.initialRendererColors[i];
        }
    }

    private void KillHatVisualTweens(HatBinding hat)
    {
        if (hat == null || hat.visualRenderers == null)
            return;

        foreach (SpriteRenderer renderer in hat.visualRenderers)
        {
            if (renderer != null)
                renderer.DOKill();
        }
    }

    private void PauseHatVisualTweens(HatBinding hat)
    {
        if (hat == null || hat.visualRenderers == null)
            return;

        foreach (SpriteRenderer renderer in hat.visualRenderers)
        {
            if (renderer != null)
                renderer.DOPause();
        }
    }

    private void ResumeHatVisualTweens(HatBinding hat)
    {
        if (hat == null || hat.visualRenderers == null)
            return;

        foreach (SpriteRenderer renderer in hat.visualRenderers)
        {
            if (renderer != null)
                renderer.DOPlay();
        }
    }

    #endregion

    #region 狗、钥匙与打架表现

    /// <summary>
    /// 在帽子淡出前缓存的揭示位置放出狗，避免使用帽子掀开后的偏移位置。
    /// </summary>
    private void RevealDogAtHat(DogBinding dog, HatBinding hat)
    {
        if (dog == null || dog.root == null || hat == null || hat.hat == null)
        {
            Debug.LogWarning("[Level66Ctrl] Dog reveal skipped because dog or hat reference is missing.", this);
            return;
        }

        dog.root.position = GetRevealWorldPosition(hat) + Vector3.up * revealYOffset;
        dog.root.gameObject.SetActive(true);
        if (dog.movement != null)
            dog.movement.ResumeMoving();
    }

    /// <summary>
    /// 在帽子淡出前缓存的揭示位置放出钥匙，钥匙拾取和通关仍走原有关卡对象逻辑。
    /// </summary>
    private void RevealKeyAtHat(HatBinding hat)
    {
        if (keyRoot == null || hat == null || hat.hat == null)
        {
            Debug.LogWarning("[Level66Ctrl] Key reveal skipped because key or hat reference is missing.", this);
            return;
        }

        keyRoot.position = GetRevealWorldPosition(hat) + Vector3.up * revealYOffset;
        keyRoot.gameObject.SetActive(true);
    }

    /// <summary>
    /// 返回结算时记录的帽子揭示位置；没有缓存时退回当前帽子位置。
    /// </summary>
    private Vector3 GetRevealWorldPosition(HatBinding hat)
    {
        if (hat == null)
            return Vector3.zero;

        if (hat.hasRevealWorldPosition)
            return hat.revealWorldPosition;

        return hat.hat != null ? hat.hat.position : Vector3.zero;
    }

    /// <summary>
    /// 单狗分支和两帽分支会让第一只狗进入追击 actor 的状态。
    /// </summary>
    private async void StartDogChase(DogBinding dog)
    {
        if (dog == null || dog.root == null || dog.movement == null)
        {
            Debug.LogWarning("[Level66Ctrl] Dog chase skipped because dog movement reference is missing.", this);
            _state = Level66State.DogChase;
            return;
        }

        _chasingDog = dog;

        _chasingDog.movement.SetFaceDir(actor.transform.position.x >= _chasingDog.root.position.x);

        await UniTask.WaitForSeconds(1f);
        _state = Level66State.DogChase;
    }

    /// <summary>
    /// 每帧根据 actor 和狗的水平位置驱动狗向 actor 方向移动。
    /// </summary>
    private void UpdateDogChase()
    {
        if (_state != Level66State.DogChase || _chasingDog == null || _chasingDog.root == null || _chasingDog.movement == null || actor == null)
            return;

        float direction = actor.transform.position.x >= _chasingDog.root.position.x ? 1f : -1f;
        _chasingDog.movement.SetMoveInput(direction);
    }

    /// <summary>
    /// 三帽分支：两只狗先朝向彼此，等待指定时间后相向移动。
    /// </summary>
    private async UniTaskVoid StartDogConfrontationAsync(CancellationToken cancellationToken)
    {
        if (dogA == null || dogA.root == null || dogA.movement == null || dogB == null || dogB.root == null || dogB.movement == null)
        {
            Debug.LogWarning("[Level66Ctrl] Dog confrontation skipped because dog references are missing.", this);
            return;
        }

        try
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || !isActiveAndEnabled)
            return;

        _chasingDog = null;
        SetupConfrontationDogs();
        _state = Level66State.DogConfrontationDelay;

        try
        {
            await WaitLevelSecondsAsync(Mathf.Max(0f, twoDogWaitSeconds), cancellationToken);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || !isActiveAndEnabled)
            return;

        _state = Level66State.DogConfrontationMoving;
        ResumeLevelOwnedDogMovement();
    }

    /// <summary>
    /// 确定左右狗并设置朝向，为 1 秒等待和相向移动做准备。
    /// </summary>
    private void SetupConfrontationDogs()
    {
        bool dogAIsLeft = dogA.root.position.x <= dogB.root.position.x;
        _leftConfrontDog = dogAIsLeft ? dogA : dogB;
        _rightConfrontDog = dogAIsLeft ? dogB : dogA;

        FaceDog(_leftConfrontDog, 1f);
        FaceDog(_rightConfrontDog, -1f);
        StopLevelOwnedDogMovement();

        SetDogDangerEnabled(_leftConfrontDog, false);
        SetDogDangerEnabled(_rightConfrontDog, false);
    }

    /// <summary>
    /// 借用狗移动组件刷新朝向，然后立刻停住，避免等待期间继续移动。
    /// </summary>
    private void FaceDog(DogBinding dog, float direction)
    {
        if (dog == null || dog.movement == null)
            return;

        dog.movement.SetMoveInput(direction);
        dog.movement.OnUpdate();
        dog.movement.SetMoveInput(0f);
        dog.movement.OnUpdate();
    }

    /// <summary>
    /// 两只狗相向移动，距离足够近时隐藏狗并显示打架表现。
    /// </summary>
    private void UpdateDogConfrontation()
    {
        if (_state != Level66State.DogConfrontationMoving || _leftConfrontDog == null || _rightConfrontDog == null)
            return;

        if (_leftConfrontDog.root == null || _rightConfrontDog.root == null || _leftConfrontDog.movement == null || _rightConfrontDog.movement == null)
            return;

        _leftConfrontDog.movement.SetMoveInput(1f);
        _rightConfrontDog.movement.SetMoveInput(-1f);
    }

    /// <summary>
    /// Level66 owns DogEnemyCtrl update calls for dogA/dogB. Inputs are set first, then each dog is updated once per frame.
    /// </summary>
    private void UpdateDogsOnce()
    {
        UpdateDogOnce(dogA);
        UpdateDogOnce(dogB);
    }

    private void UpdateDogOnce(DogBinding dog)
    {
        if (dog == null || dog.movement == null)
            return;

        dog.movement.OnUpdate();
    }

    private void CheckDogConfrontationReached()
    {
        if (_state != Level66State.DogConfrontationMoving || _leftConfrontDog == null || _rightConfrontDog == null)
            return;

        if (_leftConfrontDog.root == null || _rightConfrontDog.root == null)
            return;

        float distance = Mathf.Abs(_leftConfrontDog.root.position.x - _rightConfrontDog.root.position.x);
        if (distance <= Mathf.Max(0.01f, dogTouchDistance))
            ShowFightPresentation();
    }

    /// <summary>
    /// 对撞完成后的终态表现：停狗、隐藏两只狗、显示打架对象。
    /// </summary>
    private void ShowFightPresentation()
    {
        if (_state == Level66State.FightShown)
            return;

        StopLevelOwnedDogMovement();

        if (dogA != null && dogA.root != null)
            dogA.root.gameObject.SetActive(false);
        if (dogB != null && dogB.root != null)
            dogB.root.gameObject.SetActive(false);

        ResetFightPresentationState();
        if (fightPresentation != null)
        {
            fightPresentation.gameObject.SetActive(true);
        }
        else
            Debug.LogWarning("[Level66Ctrl] Fight presentation is missing.", this);

        _state = Level66State.FightShown;
        FinishFightPresentationAsync(SessionToken).Forget();


        AudioManager.PlaySound("dogFight");
    }

    private async UniTaskVoid FinishFightPresentationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await WaitLevelSecondsAsync(Mathf.Max(0f, fightDuration), cancellationToken);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || !isActiveAndEnabled || _state != Level66State.FightShown)
            return;

        if (fightAnims != null)
            fightAnims.SetBool(FightAnimStopHash, true);

        SetDeadBodiesActive(true);
    }

    #endregion

    #region 移动与点击开关

    private void StopLevelOwnedDogMovement()
    {
        SetDogInput(dogA, 0f);
        SetDogInput(dogB, 0f);
    }

    private void ResumeLevelOwnedDogMovement()
    {
        if (_state == Level66State.DogChase)
            UpdateDogChase();
        else if (_state == Level66State.DogConfrontationMoving)
            UpdateDogConfrontation();
    }

    private void SetDogInput(DogBinding dog, float input)
    {
        if (dog == null || dog.movement == null)
            return;

        dog.movement.SetMoveInput(input);
    }

    /// <summary>
    /// 开场锁输入时同时停住 actor；洗牌完成后恢复 actor 控制。
    /// </summary>
    private void LockActorControl(bool isLocked)
    {
        if (baseLevelCtrl != null)
            baseLevelCtrl.SetCtrlable(!isLocked);

        if (actor == null)
            return;

        actor.SetMoveInput(0f);
        if (isLocked)
            actor.StopMoving();
        else
            actor.ResumeMoving();
    }

    /// <summary>
    /// 统一控制三顶帽子的点击开关，并重置 ClickItemEventHandle 的点击次数。
    /// </summary>
    private void SetHatInteraction(bool enabled)
    {
        foreach (HatBinding hat in hats)
        {
            if (hat == null || hat.clickHandle == null)
                continue;

            hat.clickHandle.ResetClickCount();
            hat.clickHandle.SetEnableClick(enabled);
        }
    }

    #endregion

    #region 场景对象显隐与状态恢复

    /// <summary>
    /// 开场展示阶段显示钥匙和两只狗，让玩家先看到即将被帽子隐藏的对象。
    /// </summary>
    private void ShowOpeningObjects()
    {
        if (keyRoot != null)
            keyRoot.gameObject.SetActive(true);
        if (dogA != null && dogA.root != null)
            dogA.root.gameObject.SetActive(true);
        if (dogB != null && dogB.root != null)
            dogB.root.gameObject.SetActive(true);
        if (fightPresentation != null)
            fightPresentation.gameObject.SetActive(false);
    }

    /// <summary>
    /// 隐藏钥匙、狗和打架表现，用于洗牌前遮蔽以及重置后的默认遮蔽。
    /// </summary>
    private void ConcealRevealObjects()
    {
        if (keyRoot != null)
            keyRoot.gameObject.SetActive(false);
        if (dogA != null && dogA.root != null)
            dogA.root.gameObject.SetActive(false);
        if (dogB != null && dogB.root != null)
            dogB.root.gameObject.SetActive(false);
        if (fightPresentation != null)
            fightPresentation.gameObject.SetActive(false);

        ResetFightPresentationState();
    }

    /// <summary>
    /// 恢复本地缓存的初始状态。重开后会再次进入开场展示，所以这里最后保持揭示对象隐藏。
    /// </summary>
    private void RestoreInitialState(bool restoreActorControl)
    {
        KillTweens();

        foreach (HatBinding hat in hats)
        {
            if (hat == null || hat.hat == null)
                continue;

            hat.hat.gameObject.SetActive(hat.initialActive);
            hat.hat.localPosition = hat.initialLocalPosition;
            hat.hat.localScale = hat.initialLocalScale;
            hat.hasRevealWorldPosition = false;
            RestoreHatVisualState(hat);
        }

        RestoreTransform(keyRoot, _keyInitialLocalPosition, _keyInitialLocalRotation, _keyInitialLocalScale, _keyInitialActive);
        RestoreDog(dogA);
        RestoreDog(dogB);
        ResetFightPresentationState();
        ConcealRevealObjects();

        _selectedHats.Clear();
        _outcomeResolved = false;
        _editorValidationTriggered = false;
        _chasingDog = null;
        _leftConfrontDog = null;
        _rightConfrontDog = null;
        SetHatInteraction(false);

        if (restoreActorControl)
            LockActorControl(false);
    }

    /// <summary>
    /// 恢复狗的 authored transform 和输入状态。
    /// </summary>
    private void RestoreDog(DogBinding dog)
    {
        if (dog == null)
            return;

        RestoreTransform(dog.root, dog.initialLocalPosition, dog.initialLocalRotation, dog.initialLocalScale, dog.initialActive);
        RestoreDogDangerState(dog);
        if (dog.movement != null)
            dog.movement.SetMoveInput(0f);
    }

    /// <summary>
    /// 通用 Transform 恢复 helper，避免钥匙、狗、fight presentation 重复写恢复逻辑。
    /// </summary>
    private void RestoreTransform(Transform target, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, bool active)
    {
        if (target == null)
            return;

        target.localPosition = localPosition;
        target.localRotation = localRotation;
        target.localScale = localScale;
        target.gameObject.SetActive(active);
    }

    #endregion

    #region 初始状态缓存与引用解析

    /// <summary>
    /// 只在首次 Awake 后缓存 authored 初始状态，后续 session 复用这份基线。
    /// </summary>
    private void CacheInitialState()
    {
        if (_cachedInitialState)
            return;

        foreach (HatBinding hat in hats)
        {
            if (hat == null || hat.hat == null)
                continue;

            hat.initialLocalPosition = hat.hat.localPosition;
            hat.initialLocalScale = hat.hat.localScale;
            hat.initialActive = hat.hat.gameObject.activeSelf;
            CacheHatVisualState(hat);
        }

        if (keyRoot != null)
        {
            _keyInitialLocalPosition = keyRoot.localPosition;
            _keyInitialLocalRotation = keyRoot.localRotation;
            _keyInitialLocalScale = keyRoot.localScale;
            _keyInitialActive = keyRoot.gameObject.activeSelf;
        }

        CacheDog(dogA);
        CacheDog(dogB);

        if (dogsDeadBodys != null)
        {
            _deadBodyInitialActiveStates = new bool[dogsDeadBodys.Length];
            for (int i = 0; i < dogsDeadBodys.Length; i++)
            {
                Transform deadBody = dogsDeadBodys[i];
                _deadBodyInitialActiveStates[i] = deadBody != null && deadBody.gameObject.activeSelf;
            }
        }

        _cachedInitialState = true;
    }

    /// <summary>
    /// 缓存单只狗的 authored transform 和 active 状态。
    /// </summary>
    private void CacheDog(DogBinding dog)
    {
        if (dog == null || dog.root == null)
            return;

        dog.initialLocalPosition = dog.root.localPosition;
        dog.initialLocalRotation = dog.root.localRotation;
        dog.initialLocalScale = dog.root.localScale;
        dog.initialActive = dog.root.gameObject.activeSelf;
        dog.dangerCtrl = dog.root.GetComponent<PuppyEnemyCtrl>();
        dog.initialDangerEnabled = dog.dangerCtrl != null && dog.dangerCtrl.enabled;
    }

    private void SetDogDangerEnabled(DogBinding dog, bool enabled)
    {
        if (dog == null || dog.root == null)
            return;

        if (dog.dangerCtrl == null)
            dog.dangerCtrl = dog.root.GetComponent<PuppyEnemyCtrl>();

        if (dog.dangerCtrl != null)
            dog.dangerCtrl.enabled = enabled;
    }

    private void RestoreDogDangerState(DogBinding dog)
    {
        if (dog == null)
            return;

        if (dog.dangerCtrl == null && dog.root != null)
            dog.dangerCtrl = dog.root.GetComponent<PuppyEnemyCtrl>();

        if (dog.dangerCtrl != null)
            dog.dangerCtrl.enabled = dog.initialDangerEnabled;
    }

    private void ResetFightPresentationState()
    {
        if (fightAnims != null)
            fightAnims.SetBool(FightAnimStopHash, false);

        SetDeadBodiesActive(false, true);
    }

    private void SetDeadBodiesActive(bool active, bool useInitialState = false)
    {
        if (dogsDeadBodys == null)
            return;

        for (int i = 0; i < dogsDeadBodys.Length; i++)
        {
            Transform deadBody = dogsDeadBodys[i];
            if (deadBody == null)
                continue;

            bool targetActive = active;
            if (useInitialState && _deadBodyInitialActiveStates != null && i < _deadBodyInitialActiveStates.Length)
                targetActive = _deadBodyInitialActiveStates[i];

            deadBody.gameObject.SetActive(targetActive);
        }
    }

    /// <summary>
    /// 尽量从当前 prefab 层级补齐引用，减少手动漏绑导致的硬崩。
    /// </summary>
    private void ResolveReferences()
    {
        if (baseLevelCtrl == null)
            baseLevelCtrl = GetComponent<BaseLevelCtrl>();

        if (actor == null && baseLevelCtrl != null)
            actor = baseLevelCtrl.Actor;

        if (hatsAnimator == null)
        {
            Transform hatsRoot = FindChildByName("Hats");
            if (hatsRoot != null)
                hatsAnimator = hatsRoot.GetComponent<Animator>();
        }

        ResolveHatReferences();
        ResolveDogReference(dogA);
        ResolveDogReference(dogB);
    }

    /// <summary>
    /// 可暂停的本地计时。暂停时不累计 elapsed，恢复后继续等待剩余时间。
    /// </summary>
    private async UniTask WaitLevelSecondsAsync(float seconds, CancellationToken cancellationToken)
    {
        if (seconds <= 0f)
            return;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

            if (_isPaused)
                continue;

            elapsed += Time.deltaTime;
        }
    }

    /// <summary>
    /// 三顶帽子可手动配置，也可按 magicHat_1/2/3 名称自动查找。
    /// </summary>
    private void ResolveHatReferences()
    {
        if (hats == null || hats.Length != 3)
        {
            hats = new HatBinding[3];
            for (int i = 0; i < hats.Length; i++)
                hats[i] = new HatBinding { id = (HatId)i };
        }

        for (int i = 0; i < hats.Length; i++)
        {
            if (hats[i] == null)
                hats[i] = new HatBinding { id = (HatId)i };

            hats[i].id = (HatId)i;
            if (hats[i].hat == null)
                hats[i].hat = FindChildByName($"magicHat_{i + 1}");

            if (hats[i].clickHandle == null && hats[i].hat != null)
                hats[i].clickHandle = hats[i].hat.GetComponent<ClickItemEventHandle>();
        }
    }

    /// <summary>
    /// 狗的 Transform 和 DogEnemyCtrl 允许任一项先配置，另一项自动补齐。
    /// </summary>
    private void ResolveDogReference(DogBinding dog)
    {
        if (dog == null)
            return;

        if (dog.movement == null && dog.root != null)
            dog.movement = dog.root.GetComponent<DogEnemyCtrl>();
        if (dog.root == null && dog.movement != null)
            dog.root = dog.movement.transform;
        if (dog.dangerCtrl == null && dog.root != null)
            dog.dangerCtrl = dog.root.GetComponent<PuppyEnemyCtrl>();
    }

    /// <summary>
    /// 包含 inactive 子节点的按名称查找，用于容错补引用。
    /// </summary>
    private Transform FindChildByName(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }

    /// <summary>
    /// 根据 HatId 获取对应绑定，所有点击入口最终都会走这里。
    /// </summary>
    private HatBinding GetHat(HatId hatId)
    {
        int index = (int)hatId;
        if (hats == null || index < 0 || index >= hats.Length)
            return null;

        return hats[index];
    }

    /// <summary>
    /// 最小可运行引用检查：缺狗/钥匙/fight 时只 warning，缺帽子或 actor 才无法正常开始玩法。
    /// </summary>
    private bool HasMinimumReferences()
    {
        bool hasMinimum = baseLevelCtrl != null && actor != null && HasAllHats();

        if (!hasMinimum)
            return false;

        if (keyRoot == null)
            Debug.LogWarning("[Level66Ctrl] Key reference is missing. Key reveal branches will skip key placement.", this);
        if (dogA == null || dogA.root == null || dogA.movement == null)
            Debug.LogWarning("[Level66Ctrl] Dog A reference is missing. Dog reveal/chase may be skipped.", this);
        if (dogB == null || dogB.root == null || dogB.movement == null)
            Debug.LogWarning("[Level66Ctrl] Dog B reference is missing. Three-hat confrontation may be skipped.", this);
        if (fightPresentation == null)
            Debug.LogWarning("[Level66Ctrl] Fight presentation reference is missing. Three-hat fight visual will be skipped.", this);
        if (fightPresentation != null && fightAnims == null)
            Debug.LogWarning("[Level66Ctrl] Fight animator reference is missing. Fight stop pose will be skipped.", this);
        if (fightPresentation != null && (dogsDeadBodys == null || dogsDeadBodys.Length == 0))
            Debug.LogWarning("[Level66Ctrl] Dead body references are missing. Fight result visuals will be skipped.", this);

        return true;
    }

    private bool HasAllHats()
    {
        if (hats == null || hats.Length < 3)
            return false;

        for (int i = 0; i < 3; i++)
        {
            if (hats[i] == null || hats[i].hat == null || hats[i].clickHandle == null)
            {
                Debug.LogWarning($"[Level66Ctrl] Hat {i + 1} or its ClickItemEventHandle is missing.", this);
                return false;
            }
        }

        return true;
    }

    #endregion
}

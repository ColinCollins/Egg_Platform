using System;
using System.Threading;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Common;
using Game.ItemEvent;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using I2.Loc;
using TMPro;
using UnityEngine;

/// <summary>
/// version1.0 
/// 游戏状态分为 idle，playing，success 三个状态。gamestate。
/// 1. ClickItemEventHandle 需求一个 API，点击的时候触发 FakeSystemTips show 的一个功能。gamestate 进入 playing 状态。
/// 长按 button，FakeSystemtips 不消失会一直停在画面中间，松手，fakeSystemtips 播放 out 动画。/
/// 2. FakeSystemTips 动画状态分为两个，In，Out。参考 SystemTips。In 和 Out 分离，再 Click 松手后，才会自然触发 OUt，否则就停在 In 的最后一个状态。
/// 3. Plyaing 状态下，需要 PoolObjectManager 生成 missileObj 从一个 StartPoint 位置开始生成。每次生成都要重新获取 startPoint 的位置。
/// 4. playing 状态开始，计时 20s。20s 后进入 success 状态。fakeSystemtips 自动 OUt。触发真正的 SystemTips 传入内容待定。
/// 5. Level69MissileCtrl 默认向下移动，速度可调。
/// 
/// version2.0 
/// spawnmissile 逻辑优化，分为三个阶段。
/// 阶段一，固定三个生成位置，只移动 x 轴，写好我来调节，1.5s 生成一次。阶段结束。
/// 阶段二，固定移动路径，只移动 x 轴，从 A -> B, 来回一次，0.3s 生成间隔，移动时间为 2s (来回一共 4s)。阶段结束。
/// 阶段三，范围内，随机 x 位置，生成。0.2s 间隔，直到 playing 状态结束。
/// 
/// verison 3.0 
/// 逻辑推翻重写。
///初始状态是左图，场景中有一个按钮。
/// 点击按钮后出现急切提示和倒计时（这个急切提示是有碰撞体积的），急切提示消失后开始从天上掉下来炸弹，掉到地上或触碰到玩家播放爆炸特效（特效库找一个）碰到玩家就会把玩家炸飞然后游戏结束。
/// 时间结束后如果玩家还存活则出现急切提示（恭喜你坚持到底了！）并显示钥匙，后续钥匙与门锁交互由场景组件负责。
/// 这关的过法是手持续按住急切提示，提示可以挡住炸弹。
/// 炸弹释放逻辑先不改。
/// 
/// </summary>
[DefaultExecutionOrder(50)]
public class Level69Ctrl : MonoBehaviour, IDebuger
{
    #region Version 3.0 状态定义

    public enum GameState
    {
        Idle,
        Intro,
        Playing,
        Failed,
        SurvivalCleared
    }

    private enum PromptPhase
    {
        Hidden,
        Entering,
        HoldWindow,
        Defending,
        Exiting
    }

    private enum MissileSpawnPhase
    {
        FixedPoints,
        MovingPath,
        RandomRange
    }

    #endregion

    #region Version 3.0 序列化配置

    [Header("引用")]
    [Tooltip("导弹 Prefab，需挂载 Level69MissileCtrl")]
    [SerializeField] private Level69MissileCtrl missile;

    [Tooltip("导弹生成起点，为空则使用 missile 的初始位置")]
    [SerializeField] private Transform startPoint;

    [Tooltip("假提示组件")]
    [SerializeField] private FakeSystemTips fakeSystemTips;

    [Tooltip("只负责按钮按下、松开以及开始/召回的拖拽监听；不得承担提示区域的按住防御。")]
    [SerializeField] private DragEventsListener dragInput;

    [Tooltip("FakeSystemTips 本体上的单指按住输入组件；与盾牌碰撞体相互独立。")]
    [SerializeField] private Level69HoldInput holdInput;

    [Tooltip("仅在物理按钮按下期间显示的按下态视觉节点；松开时隐藏。")]
    [SerializeField] private GameObject pressBtn;

    [Header("游戏设置")]
    [Tooltip("playing 状态持续时间（秒）")]
    [SerializeField] private float playingDuration = 20f;

    [Tooltip("Playing 状态剩余时间显示，格式为 ss:ms。为空时只跳过文本刷新，不影响关卡流程。")]
    [SerializeField] private TMP_Text countdown_txt;

    [Tooltip("导弹生成间隔（秒）")]
    [SerializeField] private float missileSpawnInterval = 0.5f;

    [Tooltip("每次生成导弹数量")]
    [SerializeField] private int missileSpawnCount = 1;

    [Header("导弹生成阶段设置")]
    // 以下参数只影响 Level69 本地导弹生成阶段；Y/Z 仍以 startPoint 为准，避免改动导弹移动和对象池边界。
    [Tooltip("阶段一第一个固定生成点的世界 X 坐标，Y/Z 每次从 startPoint 获取。")]
    [SerializeField] private float phaseOneSpawnX1 = -3f;

    [Tooltip("阶段一第二个固定生成点的世界 X 坐标，Y/Z 每次从 startPoint 获取。")]
    [SerializeField] private float phaseOneSpawnX2;

    [Tooltip("阶段一第三个固定生成点的世界 X 坐标，Y/Z 每次从 startPoint 获取。")]
    [SerializeField] private float phaseOneSpawnX3 = 3f;

    [Tooltip("阶段一每个固定点的生成间隔，生成三个固定点后进入阶段二。")]
    [SerializeField] private float phaseOneSpawnInterval = 1.5f;

    [Tooltip("阶段二 A 点的世界 X 坐标，Y/Z 每次从 startPoint 获取。")]
    [SerializeField] private float phaseTwoPointAX = -3f;

    [Tooltip("阶段二 B 点的世界 X 坐标，Y/Z 每次从 startPoint 获取。")]
    [SerializeField] private float phaseTwoPointBX = 3f;

    [Tooltip("阶段二从 A 到 B 或从 B 回 A 的单程移动时间。")]
    [SerializeField] private float phaseTwoOneWayDuration = 2f;

    [Tooltip("阶段二沿 A/B 路径移动时的导弹生成间隔。")]
    [SerializeField] private float phaseTwoSpawnInterval = 0.3f;

    [Tooltip("阶段三随机范围的最小世界 X 坐标，Y/Z 每次从 startPoint 获取。")]
    [SerializeField] private float phaseThreeMinX = -3f;

    [Tooltip("阶段三随机范围的最大世界 X 坐标，Y/Z 每次从 startPoint 获取。")]
    [SerializeField] private float phaseThreeMaxX = 3f;

    [Tooltip("阶段三随机生成的导弹生成间隔。")]
    [SerializeField] private float phaseThreeSpawnInterval = 0.2f;

    [Header("Version 3.0 流程设置")]
    // 以下参数只控制 Level69 的开场等待与成功提示；炸弹三阶段释放参数保持不变。
    [Tooltip("FakeSystemTips 抵达中间点后可接受按住的时长；开场允许提前按住。")]
    [SerializeField] private float holdWindowDuration = 0.7f;

    [Tooltip("成功时真正 SystemTips 显示的内容")]
    [SerializeField] private string successTipsContent = "恭喜你坚持到底了！";

    [Tooltip("Level69 胜利后直接显示的 Key；后续交互由场景现有组件负责。")]
    [SerializeField] private GameObject keyObject;

    [SerializeField] private GameObject star;

    [Tooltip("战斗开始时下落的第二颗星星；可不绑定，缺失时不阻止开战。")]
    [SerializeField] private GameObject star2;

    [Tooltip("战斗开始时 star2 移动到与 star 相同落点高度的时间，单位秒；负值按 0 秒处理。")]
    [SerializeField] private float star2MoveDuration = 10f;

    #endregion

    #region Version 3.0 运行态状态

    private GameState _currentState = GameState.Idle;
    private PromptPhase _promptPhase = PromptPhase.Hidden;
    private MissileSpawnPhase _missileSpawnPhase = MissileSpawnPhase.FixedPoints;
    private BaseLevelCtrl _baseLevelCtrl;
    private Vector3 _missileInitialPosition;
    private float _playingTimer;
    private float _spawnTimer;
    private float _missileSpawnPhaseTimer;
    private int _phaseOneSpawnIndex;
    private int _lastDisplayedCountdownCentiseconds = -1;
    private int _lastPlayedCountdownSecond = -1;
    private int _sessionGeneration;
    private int _promptGeneration;
    private bool _isOpeningPrompt;
    private bool _bombardmentStarted;
    private bool _resultClosed;
    private bool _timerExpiryPending;
    private bool _ownsPoolRegistration;
    private bool _hasLoggedBindingError;
    private bool _hasStarted;
    private bool _battleStarted;
    private bool _isCleaningUp;
    private bool _buttonInteractable;
    private bool _battleSoundStarted;
    // 仅在本关实例 Start 时记录音乐开关；成功恢复还需尊重玩家当前设置，不在重新启用时覆盖。
    private bool _musicOnAtLevelEntry;
    private MelenitasDev.SoundsGood.Sound _battleSound;
    private CancellationTokenSource _sessionCts;
    private CancellationTokenSource _promptCts;

    #endregion

    #region Version 3.0 Unity 生命周期

    private void Awake()
    {
        CacheMissileInitialPosition();
        CacheCountdownTextReference();
        RegisterLocalInputEvents();
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        _musicOnAtLevelEntry = DB.GameSetting.MusicOn;
        _hasStarted = true;
        InitializeIdleState();
    }

    private void OnEnable()
    {
        bool canAcceptInput = !_battleStarted;
        if (dragInput != null)
            dragInput.SetEnableDrag(canAcceptInput);

        if (_hasStarted && canAcceptInput)
            InitializeIdleState();
    }

    private void Update()
    {
        if (_resultClosed || (_currentState != GameState.Intro && _currentState != GameState.Playing))
            return;

        if (IsActorDead())
        {
            CommitObservedActorFailure();
            return;
        }

        _playingTimer += Time.deltaTime;
        float remainingTime = GetRemainingPlayingTime();
        UpdateCountdownText(remainingTime, false);
        UpdateCountdownSound(remainingTime, false);

        if (_playingTimer >= Mathf.Max(0.01f, playingDuration))
        {
            _timerExpiryPending = true;
            return;
        }

        if (_currentState == GameState.Playing && _bombardmentStarted)
            UpdateMissileSpawn(Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (!_timerExpiryPending || _resultClosed ||
            (_currentState != GameState.Intro && _currentState != GameState.Playing))
            return;

        _timerExpiryPending = false;
        if (IsActorDead())
        {
            CommitObservedActorFailure();
            return;
        }

        EnterSurvivalCleared();
    }

    private void OnDisable()
    {
        if (dragInput != null)
            dragInput.SetEnableDrag(false);

        CleanupBattle();
    }

    private void OnDestroy()
    {
        CleanupBattle();
        Level69MissileCtrl.RecycleAllActive();
        UnregisterObjectPool();
        UnregisterLocalInputEvents();
    }

    #endregion

    #region Version 3.0 点击与按住输入

    private void RegisterLocalInputEvents()
    {
        if (holdInput != null)
        {
            holdInput.HoldStarted += OnPromptHoldStarted;
            holdInput.HoldEnded += OnPromptHoldEnded;
        }
    }

    private void UnregisterLocalInputEvents()
    {
        if (holdInput != null)
        {
            holdInput.HoldStarted -= OnPromptHoldStarted;
            holdInput.HoldEnded -= OnPromptHoldEnded;
        }
    }

    /// <summary>
    /// 按钮按下时调用：只允许首次有效按下显示按下态并开始战斗。
    /// </summary>
    public void OnButtonPressed()
    {
        if (!_buttonInteractable)
            return;

        SetPressButtonVisible(true);
        OnButtonClick(null, Vector2.zero);
    }

    /// <summary>
    /// 按钮松开时调用：隐藏按下态并在首次开战后关闭后续 Drag；不干预提示区域的防御 owner。
    /// </summary>
    public void OnButtonReleased()
    {
        SetPressButtonVisible(false);
        if (_battleStarted && dragInput != null)
            dragInput.SetEnableDrag(false);
    }

    /// <summary>
    /// 按钮按下后的单次开始入口；按钮松手表现由 DragEventsListener 的 EndDrag 单独处理。
    /// 提示区域的按住防御继续由 Level69HoldInput 独立负责。
    /// </summary>
    public void OnButtonClick(Transform clicker, Vector2 worldPoint)
    {
        if (_currentState != GameState.Idle || _battleStarted)
            return;

        if (!HasRequiredBindings())
        {
            SetButtonInteractable(false);
            return;
        }

        AudioManager.PlaySound("l69button");
        BeginBattle();
    }

    private void OnPromptHoldStarted(int fingerId)
    {
        if (_promptPhase == PromptPhase.HoldWindow)
            AcceptDefense();
    }

    private void OnPromptHoldEnded(int fingerId)
    {
        if (_promptPhase == PromptPhase.Defending)
            StartPromptExit(_promptGeneration);
    }

    private void AcceptDefense()
    {
        if (_promptPhase != PromptPhase.HoldWindow || holdInput == null || !holdInput.IsHolding)
            return;

        _promptPhase = PromptPhase.Defending;
        fakeSystemTips.SetShieldEnabled(true);
        UpdateButtonState();
    }

    #endregion

    #region Version 3.0 会话与提示流程

    private void BeginBattle()
    {
        if (_battleStarted || _currentState != GameState.Idle)
            return;

        _battleStarted = true;
        _sessionGeneration++;
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        _currentState = GameState.Intro;
        _resultClosed = false;
        StartStar2Move();
        _playingTimer = 0f;
        _bombardmentStarted = false;
        _timerExpiryPending = false;
        _lastPlayedCountdownSecond = -1;
        SetCountdownVisible(true);
        UpdateCountdownText(playingDuration, true);
        UpdateCountdownSound(playingDuration, true);
        PlayBattleSoundOnce();
        RegisterObjectPool();
        ResetMissileSpawnPhases();
        UpdateButtonState();
        BeginPromptAttempt(true);
    }

    private void StartStar2Move()
    {
        if (star2 == null)
            return;

        star2.transform.DOMoveY(-0.76f, Mathf.Max(0f, star2MoveDuration)).OnComplete(() =>
        {
            if (star2 == null)
                return;

            MoveFloatHandle moveFloatHandle = star2.GetComponent<MoveFloatHandle>();
            if (moveFloatHandle != null)
                moveFloatHandle.enabled = true;
        });
    }

    private void BeginPromptAttempt(bool opening)
    {
        CancelPromptTask();
        _promptGeneration++;
        int generation = _promptGeneration;
        _isOpeningPrompt = opening;
        _promptPhase = PromptPhase.Entering;
        fakeSystemTips.SetShieldEnabled(false);
        holdInput.SetTrackingEnabled(opening);
        UpdateButtonState();

        _promptCts = CancellationTokenSource.CreateLinkedTokenSource(
            _sessionCts != null ? _sessionCts.Token : this.GetCancellationTokenOnDestroy());
        CancellationToken token = _promptCts.Token;
        UniTaskCompletionSource entered = new UniTaskCompletionSource();
        fakeSystemTips.PlayIn(() => entered.TrySetResult());
        RunPromptAttemptAsync(opening, generation, entered, token).Forget();
    }

    private async UniTaskVoid RunPromptAttemptAsync(
        bool opening,
        int generation,
        UniTaskCompletionSource entered,
        CancellationToken cancellationToken)
    {
        try
        {
            await entered.Task.AttachExternalCancellation(cancellationToken);
            if (!IsCurrentPrompt(generation) || _promptPhase != PromptPhase.Entering)
                return;

            _promptPhase = PromptPhase.HoldWindow;
            holdInput.SetTrackingEnabled(true);
            if (opening && holdInput.IsHolding)
                AcceptDefense();

            await UniTask.WaitForSeconds(Mathf.Max(0f, holdWindowDuration), cancellationToken: cancellationToken);
            if (!IsCurrentPrompt(generation))
                return;

            if (_promptPhase != PromptPhase.Defending)
            {
                if (_promptPhase != PromptPhase.Hidden && _promptPhase != PromptPhase.Exiting)
                    StartPromptExit(generation);

                await UniTask.WaitUntil(
                    () => !IsCurrentPrompt(generation) || _promptPhase == PromptPhase.Hidden,
                    cancellationToken: cancellationToken);
            }

            if (opening && IsCurrentPrompt(generation) && _currentState == GameState.Intro && !_resultClosed)
            {
                _currentState = GameState.Playing;
                _bombardmentStarted = true;
                UpdateButtonState();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void StartPromptExit(int generation)
    {
        if (!IsCurrentPrompt(generation) || _promptPhase == PromptPhase.Hidden ||
            _promptPhase == PromptPhase.Exiting)
            return;

        _promptPhase = PromptPhase.Exiting;
        fakeSystemTips.SetShieldEnabled(false);
        holdInput.SetTrackingEnabled(false);
        UpdateButtonState();
        fakeSystemTips.PlayOut(() =>
        {
            if (!IsCurrentPrompt(generation))
                return;

            _promptPhase = PromptPhase.Hidden;
            UpdateButtonState();
        });
    }

    private bool IsCurrentPrompt(int generation)
    {
        bool canFinishHeldPrompt = _currentState == GameState.SurvivalCleared &&
                                   (_promptPhase == PromptPhase.Defending || _promptPhase == PromptPhase.Exiting);
        return generation == _promptGeneration && (!_resultClosed || canFinishHeldPrompt);
    }

    private void CancelPromptTask()
    {
        if (_promptCts == null)
            return;

        _promptCts.Cancel();
        _promptCts.Dispose();
        _promptCts = null;
    }

    private void CancelPromptAndHide()
    {
        _promptGeneration++;
        CancelPromptTask();
        _promptPhase = PromptPhase.Hidden;
        _isOpeningPrompt = false;
        if (holdInput != null)
            holdInput.SetTrackingEnabled(false);
        if (fakeSystemTips != null)
            fakeSystemTips.AbortAndHide();
    }

    private void UpdateButtonState()
    {
        bool interactable = !_battleStarted && _currentState == GameState.Idle;
        SetButtonInteractable(interactable);
    }

    private void SetPressButtonVisible(bool visible)
    {
        if (pressBtn != null)
            pressBtn.SetActive(visible);
    }

    private void SetButtonInteractable(bool interactable)
    {
        _buttonInteractable = interactable;
    }

    #endregion

    #region Version 3.0 失败与生存成功仲裁

    internal bool TryCommitPlayerHit(
        ActorCtrl actor,
        Vector2 knockbackDirection,
        float knockbackForce,
        float knockbackDuration)
    {
        ActorCtrl actorToUse = actor != null ? actor : ResolveBaseLevelCtrl()?.Actor;
        if (actorToUse == null || actorToUse.IsDied)
            return false;

        _resultClosed = true;
        _timerExpiryPending = false;
        actorToUse.Die();
        actorToUse.Knockback(knockbackDirection, knockbackForce, knockbackDuration).Forget();

        EnterFailed();
        return true;
    }

    internal bool IsMissileBlockedByShield(Collider2D missileCollider)
    {
        if (_promptPhase != PromptPhase.Defending || fakeSystemTips == null ||
            !fakeSystemTips.IsShieldEnabled || fakeSystemTips.ShieldCollider == null || missileCollider == null)
            return false;

        Collider2D shield = fakeSystemTips.ShieldCollider;
        return shield.IsTouching(missileCollider) || shield.bounds.Intersects(missileCollider.bounds);
    }

    private void CommitObservedActorFailure()
    {
        if (_resultClosed)
            return;

        _resultClosed = true;
        _timerExpiryPending = false;
        EnterFailed();
    }

    private void EnterFailed()
    {
        _currentState = GameState.Failed;
        _bombardmentStarted = false;
        CancelSessionToken();
        CancelPromptAndHide();
        SetCountdownVisible(false);
        SetButtonInteractable(false);
    }

    /// <summary>
    /// 手动进入 success 状态
    /// </summary>
    public void EnterSuccess()
    {
        if (!_resultClosed && (_currentState == GameState.Intro || _currentState == GameState.Playing) &&
            !IsActorDead())
            EnterSurvivalCleared();
    }

    private void EnterSurvivalCleared()
    {
        if (_resultClosed || IsActorDead())
            return;

        _resultClosed = true;
        _timerExpiryPending = false;
        _currentState = GameState.SurvivalCleared;
        _bombardmentStarted = false;
        UpdateCountdownText(0f, true);
        SetCountdownVisible(false);
        SetButtonInteractable(false);

        if (!string.IsNullOrEmpty(successTipsContent))
            SystemTips.Show(LocalizationManager.GetTranslation(successTipsContent));

        keyObject.SetActive(true);

        if (star)
        {
            star.transform.DOMoveY(-0.76f, 3f).OnComplete(() =>
{
    star.GetComponent<MoveFloatHandle>().enabled = true;
});
        }


        if (_musicOnAtLevelEntry && DB.GameSetting.MusicOn)
            AudioManager.ResumeMusic();
    }

    #endregion

    #region Version 3.0 初始化、降级与清理

    private void InitializeIdleState()
    {
        _currentState = GameState.Idle;
        _resultClosed = false;
        _hasLoggedBindingError = false;
        _playingTimer = 0f;
        _timerExpiryPending = false;
        _bombardmentStarted = false;
        _lastPlayedCountdownSecond = -1;
        CancelPromptAndHide();
        UpdateCountdownText(playingDuration, true);
        SetCountdownVisible(false);
        SetPressButtonVisible(false);
        SetButtonInteractable(!_battleStarted);
    }

    private bool HasRequiredBindings()
    {
        BaseLevelCtrl baseLevelCtrl = ResolveBaseLevelCtrl();
        bool isValid = missile != null && fakeSystemTips != null && dragInput != null && holdInput != null &&
                       countdown_txt != null && keyObject != null && baseLevelCtrl != null &&
                       baseLevelCtrl.Actor != null && ObjectPoolManager.Instance != null;
        if (!isValid && !_hasLoggedBindingError)
        {
            _hasLoggedBindingError = true;
            this.LogError("Level69 缺少按钮拖拽、按住、提示、炸弹、倒计时、Key、Actor 或对象池绑定，已安全阻止开始；不会自动判胜或持续刷错。");
        }

        return isValid;
    }

    private void CleanupBattle()
    {
        if (_isCleaningUp)
            return;

        _isCleaningUp = true;
        _sessionGeneration++;
        _resultClosed = true;
        _timerExpiryPending = false;
        _bombardmentStarted = false;
        CancelSessionToken();
        CancelPromptAndHide();
        StopBattleSound();
        _lastPlayedCountdownSecond = -1;
        SetCountdownVisible(false);
        SetPressButtonVisible(false);
        SetButtonInteractable(false);
        _isCleaningUp = false;
    }

    private void CancelSessionToken()
    {
        if (_sessionCts == null)
            return;

        _sessionCts.Cancel();
        _sessionCts.Dispose();
        _sessionCts = null;
    }

    private BaseLevelCtrl ResolveBaseLevelCtrl()
    {
        if (_baseLevelCtrl == null)
            _baseLevelCtrl = GetComponent<BaseLevelCtrl>() ?? GetComponentInParent<BaseLevelCtrl>();
        return _baseLevelCtrl;
    }

    private bool IsActorDead()
    {
        BaseLevelCtrl baseLevelCtrl = ResolveBaseLevelCtrl();
        return baseLevelCtrl != null && baseLevelCtrl.Actor != null && baseLevelCtrl.Actor.IsDied;
    }

    private void CacheMissileInitialPosition()
    {
        if (missile == null)
        {
            _missileInitialPosition = transform.position;
            return;
        }

        _missileInitialPosition = missile.transform.position;
        // 场景中的模板导弹不直接参与游戏，避免提前移动或触发碰撞
        missile.gameObject.SetActive(false);
    }

    private void CacheCountdownTextReference()
    {
        if (countdown_txt != null)
            return;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name.Equals(nameof(countdown_txt), StringComparison.OrdinalIgnoreCase))
            {
                countdown_txt = texts[i];
                return;
            }
        }
    }

    private void SetCountdownVisible(bool visible)
    {
        if (countdown_txt != null)
            countdown_txt.gameObject.SetActive(visible);
    }

    private float GetRemainingPlayingTime()
    {
        return Mathf.Max(0f, playingDuration - _playingTimer);
    }

    private void UpdateCountdownText(float remainingTime, bool force)
    {
        if (countdown_txt == null)
            return;

        int totalCentiseconds = Mathf.Clamp(Mathf.CeilToInt(remainingTime * 100f), 0, 599999);
        if (!force && totalCentiseconds == _lastDisplayedCountdownCentiseconds)
            return;

        _lastDisplayedCountdownCentiseconds = totalCentiseconds;
        int displaySeconds = totalCentiseconds / 100;
        int displayMilliseconds = totalCentiseconds % 100;
        countdown_txt.SetText("{0:00}:{1:00}", displaySeconds, displayMilliseconds);
    }

    private void UpdateCountdownSound(float remainingTime, bool force)
    {
        int countdownSecond = Mathf.Clamp(Mathf.CeilToInt(remainingTime), 0, 5999);
        if (!force && countdownSecond == _lastPlayedCountdownSecond)
            return;

        _lastPlayedCountdownSecond = countdownSecond;
        AudioManager.PlaySound("l69countdown");
    }

    private void PlayBattleSoundOnce()
    {
        if (_battleSoundStarted)
            return;

        _battleSoundStarted = true;
        _battleSound = AudioManager.PlaySound("l69battle");
        AudioManager.PauseMusic();
    }

    private void StopBattleSound()
    {
        _battleSound?.Stop();
        _battleSound = null;
        _battleSoundStarted = false;
    }

    #endregion

    #region Version 3.0 导弹对象池与投放

    private void RegisterObjectPool()
    {
        if (ObjectPoolManager.Instance == null || missile == null)
            return;

        if (ObjectPoolManager.Instance.IsPoolRegistered<Level69MissileCtrl>())
        {
            _ownsPoolRegistration = false;
            return;
        }

        ObjectPoolManager.Instance.RegisterPool<Level69MissileCtrl>(() =>
        {
            GameObject go = Instantiate(missile.gameObject);
            return go.GetComponent<Level69MissileCtrl>();
        }, initialSize: 3, maxSize: 30);
        _ownsPoolRegistration = true;
    }

    private void UnregisterObjectPool()
    {
        if (!_ownsPoolRegistration || ObjectPoolManager.Instance == null)
            return;

        ObjectPoolManager.Instance.UnregisterPool<Level69MissileCtrl>();
        _ownsPoolRegistration = false;
    }

    private void SpawnMissile()
    {
        if (missile == null || ObjectPoolManager.Instance == null || Level69MissileCtrl.ActiveCount >= 30)
            return;

        Vector3 spawnPos = GetMissileSpawnPosition();
        ActorCtrl actor = ResolveBaseLevelCtrl()?.Actor;
        for (int i = 0; i < missileSpawnCount; i++)
        {
            if (Level69MissileCtrl.ActiveCount >= 30)
                break;

            Level69MissileCtrl missileCtrl = ObjectPoolManager.Instance.Get<Level69MissileCtrl>();
            if (missileCtrl == null)
                break;

            missileCtrl.transform.position = spawnPos;
            missileCtrl.Init(this, actor);
        }

        AdvanceAfterMissileSpawn();
    }

    /// <summary>
    /// 每次生成时重新获取 startPoint 的位置
    /// </summary>
    private Vector3 GetStartPointPosition()
    {
        return startPoint != null ? startPoint.position : _missileInitialPosition;
    }

    private void ResetMissileSpawnPhases()
    {
        _missileSpawnPhase = MissileSpawnPhase.FixedPoints;
        _missileSpawnPhaseTimer = 0f;
        _phaseOneSpawnIndex = 0;
        _spawnTimer = GetCurrentMissileSpawnInterval();
    }

    private void UpdateMissileSpawn(float deltaTime)
    {
        if (missile == null || ObjectPoolManager.Instance == null)
            return;

        MissileSpawnPhase phaseBeforeTick = _missileSpawnPhase;
        _spawnTimer += deltaTime;
        if (_spawnTimer < GetCurrentMissileSpawnInterval())
        {
            _missileSpawnPhaseTimer += deltaTime;
            TryCompletePhaseTwo();
            return;
        }

        _spawnTimer = 0f;
        SpawnMissile();
        if (_missileSpawnPhase == phaseBeforeTick)
        {
            _missileSpawnPhaseTimer += deltaTime;
            TryCompletePhaseTwo();
        }
    }

    private void AdvanceAfterMissileSpawn()
    {
        if (_missileSpawnPhase != MissileSpawnPhase.FixedPoints)
            return;

        _phaseOneSpawnIndex++;
        if (_phaseOneSpawnIndex >= 3)
            EnterMissileSpawnPhase(MissileSpawnPhase.MovingPath);
    }

    private void EnterMissileSpawnPhase(MissileSpawnPhase phase)
    {
        _missileSpawnPhase = phase;
        _missileSpawnPhaseTimer = 0f;
        _spawnTimer = GetCurrentMissileSpawnInterval();
    }

    private Vector3 GetMissileSpawnPosition()
    {
        Vector3 spawnPos = GetStartPointPosition();
        switch (_missileSpawnPhase)
        {
            case MissileSpawnPhase.FixedPoints:
                spawnPos.x = GetPhaseOneSpawnX();
                break;
            case MissileSpawnPhase.MovingPath:
                spawnPos.x = GetPhaseTwoSpawnX();
                break;
            case MissileSpawnPhase.RandomRange:
                spawnPos.x = GetPhaseThreeSpawnX();
                break;
        }

        return spawnPos;
    }

    private float GetPhaseOneSpawnX()
    {
        switch (_phaseOneSpawnIndex)
        {
            case 0: return phaseOneSpawnX1;
            case 1: return phaseOneSpawnX2;
            default: return phaseOneSpawnX3;
        }
    }

    private float GetPhaseTwoSpawnX()
    {
        float oneWayDuration = Mathf.Max(0.01f, phaseTwoOneWayDuration);
        float totalDuration = oneWayDuration * 2f;
        float phaseTime = Mathf.Clamp(_missileSpawnPhaseTimer, 0f, totalDuration);
        if (phaseTime <= oneWayDuration)
            return Mathf.Lerp(phaseTwoPointAX, phaseTwoPointBX, phaseTime / oneWayDuration);

        return Mathf.Lerp(
            phaseTwoPointBX,
            phaseTwoPointAX,
            (phaseTime - oneWayDuration) / oneWayDuration);
    }

    private float GetPhaseThreeSpawnX()
    {
        return UnityEngine.Random.Range(
            Mathf.Min(phaseThreeMinX, phaseThreeMaxX),
            Mathf.Max(phaseThreeMinX, phaseThreeMaxX));
    }

    private float GetCurrentMissileSpawnInterval()
    {
        switch (_missileSpawnPhase)
        {
            case MissileSpawnPhase.FixedPoints: return Mathf.Max(0.01f, phaseOneSpawnInterval);
            case MissileSpawnPhase.MovingPath: return Mathf.Max(0.01f, phaseTwoSpawnInterval);
            case MissileSpawnPhase.RandomRange: return Mathf.Max(0.01f, phaseThreeSpawnInterval);
            default: return Mathf.Max(0.01f, missileSpawnInterval);
        }
    }

    private void TryCompletePhaseTwo()
    {
        if (_missileSpawnPhase == MissileSpawnPhase.MovingPath &&
            _missileSpawnPhaseTimer >= Mathf.Max(0.01f, phaseTwoOneWayDuration) * 2f)
            EnterMissileSpawnPhase(MissileSpawnPhase.RandomRange);
    }

    #endregion
}

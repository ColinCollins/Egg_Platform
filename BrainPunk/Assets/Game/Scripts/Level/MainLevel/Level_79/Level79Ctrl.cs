using System;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.ItemEvent;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level079 私有控制器：负责石像推盘、八帧滑动、真假钥匙演出和出口解锁。
/// </summary>
public sealed class Level79Ctrl : MonoBehaviour
{
    private const int StatueFrameCount = 8;
    private const int KeyVisibleFrameIndex = 4;
    private const int MaxFrameStepsPerUpdate = 3;
    private const float KeyFlightTargetScaleRatio = 0.5f;
    private const float KeyFlightScaleDurationRatio = 0.8f;
    private const float KeyFlightFadeDelayRatio = 0.4f;
    private const float KeyFlightFadeDurationRatio = 0.5f;
    private const string MechanismMoveSfxTag = "l89StonePillarDrop";

    private enum PointerMode
    {
        None,
        StatueSwipe,
        KeyTap
    }

    #region Inspector References

    [Header("Level References")]
    [Tooltip("关卡基础控制器，用于在钥匙演出期间锁定角色输入。")]
    [SerializeField] private BaseLevelCtrl baseLevelCtrl;

    [Tooltip("本关玩家角色。")]
    [SerializeField] private ActorCtrl actor;

    [Tooltip("世界交互和震屏使用的游戏相机。")]
    [SerializeField] private Camera worldCamera;

    [Header("Statue")]
    [Tooltip("可由角色推动的石像物理根节点。")]
    [SerializeField] private Transform statueRoot;

    [Tooltip("仅切换贴图、不参与物理旋转的石像显示节点。")]
    [SerializeField] private SpriteRenderer statueRenderer;

    [Tooltip("石像命中与圆盘位置判断使用的碰撞体。")]
    [SerializeField] private Collider2D statueCollider;

    [Tooltip("石像推动使用的动态刚体。")]
    [SerializeField] private Rigidbody2D statueBody;

    [Tooltip("按 01 到 08 顺序配置的石像八方向贴图。")]
    [SerializeField] private Sprite[] statueFrames = new Sprite[StatueFrameCount];

    [Header("Plate And Fake Reward")]
    [Tooltip("石像需要推到的右侧圆盘。")]
    [SerializeField] private Transform plate;

    [Tooltip("左上机关中向左移开的石块。")]
    [SerializeField] private Transform leftMechanismStone;

    [Tooltip("左上机关中向右移开的石块。")]
    [SerializeField] private Transform rightMechanismStone;

    [Tooltip("仅作迷惑展示的假钥匙；该节点不得挂点击碰撞体。")]
    [SerializeField] private SpriteRenderer fakeKeyRenderer;

    [Header("Real Key And Door")]
    [Tooltip("跟随石像、起飞时会脱离石像的真钥匙根节点。")]
    [SerializeField] private Transform realKeyRoot;

    [Tooltip("真钥匙的渐显、命中和飞行动画显示节点。")]
    [SerializeField] private SpriteRenderer realKeyRenderer;

    [Tooltip("现有 door_0 根节点，内部包含 LockPoint 和成功 Trigger。")]
    [SerializeField] private Transform successDoor;

    [Tooltip("现有 DoorLock 根节点，使用其共享开锁执行器播放标准表现。")]
    [SerializeField] private Transform doorLockRoot;

    #endregion

    #region Inspector Tuning

    [Header("Swipe Input")]
    [Tooltip("横滑累计超过该屏幕像素值时切换一帧。")]
    [SerializeField, Min(8f)] private float swipeStepPixels = 48f;

    [Tooltip("横向位移至少达到纵向位移的该倍数，才判定为旋转手势。")]
    [SerializeField, Min(1f)] private float horizontalDominance = 1.25f;

    [Tooltip("钥匙按下到松手允许的最大屏幕位移，超过后按拖动处理。")]
    [SerializeField, Min(1f)] private float keyTapMoveThresholdPixels = 18f;

    [Tooltip("开启后，按在 UI 上的指针不会被本关世界交互认领。")]
    [SerializeField] private bool ignoreUiPointer = true;

    [Header("Plate Trigger")]
    [Tooltip("石像显示中心与圆盘中心的水平容差。")]
    [SerializeField, Min(0.05f)] private float plateHorizontalTolerance = 0.72f;

    [Tooltip("石像底部与圆盘高度的垂直容差。")]
    [SerializeField, Min(0.05f)] private float plateVerticalTolerance = 0.6f;

    [Tooltip("石像已进入圆盘后，离开判定额外增加的容差，避免边缘反复开合。")]
    [SerializeField, Min(0f)] private float plateReleaseMargin = 0.2f;

    [Tooltip("机关石块向左右各自移开的本地距离。")]
    [SerializeField, Min(0f)] private float mechanismOpenDistance = 1.45f;

    [Tooltip("机关石块晃动着移开的持续时间。")]
    [SerializeField, Min(0.01f)] private float mechanismOpenDuration = 0.8f;

    [Tooltip("机关石块移动期间的旋转晃动角度。")]
    [SerializeField, Min(0f)] private float mechanismShakeAngle = 7f;

    [Tooltip("石像落盘时镜头震动的持续时间。")]
    [SerializeField, Min(0.01f)] private float cameraShakeDuration = 0.45f;

    [Tooltip("石像落盘时镜头震动的位移强度。")]
    [SerializeField, Min(0f)] private float cameraShakeStrength = 0.18f;

    [Header("Real Key")]
    [Tooltip("05 帧真钥匙快速渐显的时间；离开 05 帧时立即隐藏。")]
    [SerializeField, Min(0.01f)] private float keyFadeDuration = 0.1f;

    [Tooltip("真钥匙飞到门锁的持续时间。")]
    [SerializeField, Min(0.01f)] private float keyFlightDuration = 0.55f;

    [Tooltip("真钥匙抛物线中段相对直线插值的附加高度。")]
    [SerializeField, Min(0f)] private float keyFlightArcHeight = 3f;

    [Tooltip("等待标准 DoorLock 动画销毁的最长时间，超时后仍会开放出口避免卡关。")]
    [SerializeField, Min(0.1f)] private float doorUnlockTimeout = 2f;

    #endregion

    #region Runtime State

    private EventSubscriber _subscriber;
    private PointerMode _pointerMode;
    private int _activeFingerId = -1;
    private bool _usingEditorMouse;
    private Vector2 _pointerStartScreenPosition;
    private Vector2 _segmentStartScreenPosition;
    private Vector2 _latestScreenPosition;

    private Collider2D _successDoorCollider;
    private BaseItemExecutor _doorLockExecutor;
    private Transform _lockPoint;

    private Vector3 _leftStoneInitialLocalPosition;
    private Vector3 _rightStoneInitialLocalPosition;
    private Quaternion _leftStoneInitialLocalRotation;
    private Quaternion _rightStoneInitialLocalRotation;
    private Vector3 _cameraInitialLocalPosition;

    private Tween _keyFadeTween;
    private Tween _keyFlightTween;
    private Tween _cameraShakeTween;
    private Sequence _mechanismSequence;

    private int _statueFrameIndex;
    private int _sessionVersion;
    private bool _keyFrameVisible;
    private bool _statueOnPlate;
    private bool _keyFlying;
    private bool _unlocking;
    private bool _doorUnlocked;
    private bool _actorLocked;
    private bool _actorWasCtrlable;
    private bool _isPaused;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        CacheInitialState();
        InitializePresentation();
        ValidateConfiguration();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RegisterEvents();
        ClearPointer();
    }

    private void Update()
    {
        if (_isPaused || !CanReceiveWorldInput())
        {
            ClearPointer();
            return;
        }

        HandlePointerInput();
    }

    private void FixedUpdate()
    {
        if (_isPaused || !IsGameplayPlaying())
            return;

        UpdatePlateState();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            ClearPointer();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            ClearPointer();
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        CleanupRuntime();
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        CleanupRuntime();
    }

    #endregion

    #region Pointer Input

    private void HandlePointerInput()
    {
        if (Input.touchCount > 0)
        {
            HandleTouchInput();
            return;
        }

        if (_pointerMode != PointerMode.None && !_usingEditorMouse)
        {
            EndPointer(true, _latestScreenPosition);
            return;
        }

#if UNITY_EDITOR
        HandleMouseInput();
#endif
    }

#if UNITY_EDITOR
    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            BeginPointer(Input.mousePosition, -1, true);
        }
        else if (_pointerMode != PointerMode.None &&
                 _usingEditorMouse &&
                 Input.GetMouseButton(0))
        {
            UpdatePointer(Input.mousePosition);
        }
        else if (_pointerMode != PointerMode.None &&
                 _usingEditorMouse &&
                 Input.GetMouseButtonUp(0))
        {
            EndPointer(false, Input.mousePosition);
        }
    }
#endif

    private void HandleTouchInput()
    {
        if (_pointerMode != PointerMode.None && !_usingEditorMouse)
        {
            if (!TryGetActiveTouch(out Touch activeTouch))
            {
                EndPointer(true, _latestScreenPosition);
                return;
            }

            if (activeTouch.phase == TouchPhase.Ended ||
                activeTouch.phase == TouchPhase.Canceled)
            {
                EndPointer(
                    activeTouch.phase == TouchPhase.Canceled,
                    activeTouch.position);
            }
            else
            {
                UpdatePointer(activeTouch.position);
            }

            return;
        }

        if (_pointerMode != PointerMode.None)
            return;

        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (touch.phase != TouchPhase.Began)
                continue;

            BeginPointer(touch.position, touch.fingerId, false);
            if (_pointerMode != PointerMode.None)
                break;
        }
    }

    private void BeginPointer(Vector2 screenPosition, int fingerId, bool usingEditorMouse)
    {
        if (!CanReceiveWorldInput())
            return;

        if (ignoreUiPointer && IsPointerOverUi(screenPosition, fingerId))
            return;

        Vector2 worldPosition = ScreenToWorldPosition(screenPosition);
        PointerMode selectedMode = PointerMode.None;

        if (CanTapRealKey() && ContainsRealKey(worldPosition))
            selectedMode = PointerMode.KeyTap;
        else if (statueCollider != null && statueCollider.OverlapPoint(worldPosition))
            selectedMode = PointerMode.StatueSwipe;

        if (selectedMode == PointerMode.None)
            return;

        _pointerMode = selectedMode;
        _activeFingerId = fingerId;
        _usingEditorMouse = usingEditorMouse;
        _pointerStartScreenPosition = screenPosition;
        _segmentStartScreenPosition = screenPosition;
        _latestScreenPosition = screenPosition;
    }

    private void UpdatePointer(Vector2 screenPosition)
    {
        _latestScreenPosition = screenPosition;
        if (_pointerMode != PointerMode.StatueSwipe)
            return;

        Vector2 delta = screenPosition - _segmentStartScreenPosition;
        float horizontalDistance = Mathf.Abs(delta.x);
        float verticalDistance = Mathf.Abs(delta.y);
        if (horizontalDistance < swipeStepPixels ||
            horizontalDistance < verticalDistance * horizontalDominance)
        {
            return;
        }

        int direction = delta.x > 0f ? 1 : -1;
        int stepCount = Mathf.Clamp(
            Mathf.FloorToInt(horizontalDistance / swipeStepPixels),
            1,
            MaxFrameStepsPerUpdate);

        for (int step = 0; step < stepCount; step++)
            StepStatueFrame(direction);

        _segmentStartScreenPosition = new Vector2(
            _segmentStartScreenPosition.x + direction * swipeStepPixels * stepCount,
            screenPosition.y);
    }

    private void EndPointer(bool canceled, Vector2 screenPosition)
    {
        PointerMode completedMode = _pointerMode;
        Vector2 startPosition = _pointerStartScreenPosition;

        if (!canceled && completedMode == PointerMode.StatueSwipe)
            UpdatePointer(screenPosition);

        ClearPointer();

        if (canceled || completedMode != PointerMode.KeyTap)
            return;

        float moveDistance = Vector2.Distance(startPosition, screenPosition);
        if (moveDistance > keyTapMoveThresholdPixels)
            return;

        Vector2 worldPosition = ScreenToWorldPosition(screenPosition);
        if (IsGameplayPlaying() &&
            CanTapRealKey() &&
            ContainsRealKey(worldPosition))
        {
            StartKeyFlight();
        }
    }

    private bool TryGetActiveTouch(out Touch activeTouch)
    {
        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (touch.fingerId != _activeFingerId)
                continue;

            activeTouch = touch;
            return true;
        }

        activeTouch = default;
        return false;
    }

    private void ClearPointer()
    {
        _pointerMode = PointerMode.None;
        _activeFingerId = -1;
        _usingEditorMouse = false;
        _pointerStartScreenPosition = Vector2.zero;
        _segmentStartScreenPosition = Vector2.zero;
        _latestScreenPosition = Vector2.zero;
    }

    private bool IsPointerOverUi(Vector2 screenPosition, int fingerId)
    {
        return fingerId >= 0
            ? InputUtils.IsPointerOverUI(fingerId)
            : InputUtils.IsPointerOverUI(screenPosition);
    }

    private Vector2 ScreenToWorldPosition(Vector2 screenPosition)
    {
        if (worldCamera == null)
            ResolveReferences();

        if (worldCamera == null)
            return screenPosition;

        float targetZ = statueRoot != null
            ? statueRoot.position.z
            : transform.position.z;
        float distance = Mathf.Abs(worldCamera.transform.position.z - targetZ);
        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, distance));
        return new Vector2(worldPosition.x, worldPosition.y);
    }

    #endregion

    #region Statue Frames And Real Key Visibility

    private void StepStatueFrame(int direction)
    {
        int normalizedDirection = direction >= 0 ? 1 : -1;
        int nextIndex = (_statueFrameIndex + normalizedDirection + StatueFrameCount) %
                        StatueFrameCount;
        SetStatueFrame(nextIndex, false);
    }

    private void SetStatueFrame(int frameIndex, bool instant)
    {
        if (statueFrames == null || statueFrames.Length != StatueFrameCount)
            return;

        int safeIndex = (frameIndex + StatueFrameCount) % StatueFrameCount;
        Sprite targetSprite = statueFrames[safeIndex];
        if (statueRenderer != null && targetSprite != null)
            statueRenderer.sprite = targetSprite;

        _statueFrameIndex = safeIndex;
        bool shouldShowKey = IsKeyVisibleFrame(safeIndex);
        if (_keyFrameVisible == shouldShowKey && !instant)
            return;

        _keyFrameVisible = shouldShowKey;
        SetRealKeyAlpha(
            shouldShowKey ? 1f : 0f,
            instant || !shouldShowKey);
    }

    private static bool IsKeyVisibleFrame(int frameIndex)
    {
        return frameIndex == KeyVisibleFrameIndex;
    }

    private void SetRealKeyAlpha(float targetAlpha, bool instant)
    {
        if (realKeyRenderer == null || _keyFlying)
            return;

        KillTween(ref _keyFadeTween);
        if (instant)
        {
            Color color = realKeyRenderer.color;
            color.a = targetAlpha;
            realKeyRenderer.color = color;
            return;
        }

        _keyFadeTween = realKeyRenderer
            .DOFade(targetAlpha, Mathf.Max(0.01f, keyFadeDuration))
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject)
            .OnComplete(() => _keyFadeTween = null);
    }

    private bool CanTapRealKey()
    {
        return !_keyFlying &&
               !_unlocking &&
               !_doorUnlocked &&
               IsKeyVisibleFrame(_statueFrameIndex) &&
               realKeyRoot != null &&
               realKeyRenderer != null &&
               realKeyRoot.gameObject.activeInHierarchy &&
               realKeyRenderer.color.a >= 0.35f;
    }

    private bool ContainsRealKey(Vector2 worldPosition)
    {
        if (realKeyRenderer == null)
            return false;

        Bounds bounds = realKeyRenderer.bounds;
        return worldPosition.x >= bounds.min.x &&
               worldPosition.x <= bounds.max.x &&
               worldPosition.y >= bounds.min.y &&
               worldPosition.y <= bounds.max.y;
    }

    #endregion

    #region Plate And Fake Key Presentation

    private void UpdatePlateState()
    {
        if (statueCollider == null || plate == null)
            return;

        float statueCenterX = statueCollider.bounds.center.x;
        float horizontalDistance = Mathf.Abs(statueCenterX - plate.position.x);
        float verticalDistance = Mathf.Abs(statueCollider.bounds.min.y - plate.position.y);
        float releaseMargin = _statueOnPlate
            ? Mathf.Max(0f, plateReleaseMargin)
            : 0f;
        bool shouldOpen =
            horizontalDistance <= plateHorizontalTolerance + releaseMargin &&
            verticalDistance <= plateVerticalTolerance + releaseMargin;

        if (shouldOpen == _statueOnPlate)
            return;

        _statueOnPlate = shouldOpen;
        PlayMechanismTransition(shouldOpen);
        if (shouldOpen)
            PlayCameraShake();
    }

    private void PlayMechanismTransition(bool open)
    {
        KillSequence(ref _mechanismSequence);
        RestoreMechanismStoneRotations();
        AudioManager.PlaySound(MechanismMoveSfxTag);
        _mechanismSequence = DOTween.Sequence().SetLink(gameObject);

        float duration = Mathf.Max(0.01f, mechanismOpenDuration);
        if (leftMechanismStone != null)
        {
            float targetX = _leftStoneInitialLocalPosition.x +
                            (open ? -mechanismOpenDistance : 0f);
            _mechanismSequence.Join(
                leftMechanismStone
                    .DOLocalMoveX(targetX, duration)
                    .SetEase(Ease.InOutSine));
            _mechanismSequence.Join(
                leftMechanismStone.DOShakeRotation(
                    duration,
                    Vector3.forward * mechanismShakeAngle,
                    18,
                    80f,
                    true));
        }

        if (rightMechanismStone != null)
        {
            float targetX = _rightStoneInitialLocalPosition.x +
                            (open ? mechanismOpenDistance : 0f);
            _mechanismSequence.Join(
                rightMechanismStone
                    .DOLocalMoveX(targetX, duration)
                    .SetEase(Ease.InOutSine));
            _mechanismSequence.Join(
                rightMechanismStone.DOShakeRotation(
                    duration,
                    Vector3.forward * mechanismShakeAngle,
                    18,
                    80f,
                    true));
        }

        _mechanismSequence.OnComplete(() =>
        {
            RestoreMechanismStoneRotations();
            _mechanismSequence = null;
        });
    }

    private void RestoreMechanismStoneRotations()
    {
        if (leftMechanismStone != null)
            leftMechanismStone.localRotation = _leftStoneInitialLocalRotation;
        if (rightMechanismStone != null)
            rightMechanismStone.localRotation = _rightStoneInitialLocalRotation;
    }

    private void PlayCameraShake()
    {
        if (worldCamera == null || cameraShakeStrength <= 0f)
            return;

        KillTween(ref _cameraShakeTween);
        Transform cameraTransform = worldCamera.transform;
        cameraTransform.localPosition = _cameraInitialLocalPosition;
        _cameraShakeTween = cameraTransform
            .DOShakePosition(
                Mathf.Max(0.01f, cameraShakeDuration),
                cameraShakeStrength,
                20,
                90f,
                false,
                true)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(RestoreCameraPosition)
            .OnKill(RestoreCameraPosition);
    }

    private void RestoreCameraPosition()
    {
        if (worldCamera != null)
            worldCamera.transform.localPosition = _cameraInitialLocalPosition;

        _cameraShakeTween = null;
    }

    #endregion

    #region Key Flight And Door Unlock

    private void StartKeyFlight()
    {
        if (!IsGameplayPlaying() ||
            !CanTapRealKey() ||
            realKeyRoot == null ||
            _lockPoint == null)
        {
            return;
        }

        _keyFlying = true;
        _unlocking = true;
        ClearPointer();
        LockActor();
        KillTween(ref _keyFadeTween);

        realKeyRoot.SetParent(transform, true);

        AudioManager.PlaySound("getKey");

        Vector3 startPosition = realKeyRoot.position;
        Vector3 endPosition = _lockPoint.position;
        Vector3 startScale = realKeyRoot.localScale;
        float progress = 0f;
        int currentSession = _sessionVersion;
        float flightDuration = Mathf.Max(0.01f, keyFlightDuration);

        KillTween(ref _keyFlightTween);
        Sequence flightSequence = DOTween.Sequence().SetLink(gameObject);
        flightSequence.Join(
            DOTween.To(
                () => progress,
                value =>
                {
                    progress = value;
                    if (realKeyRoot == null)
                        return;

                    float arc = 4f * keyFlightArcHeight * progress * (1f - progress);
                    realKeyRoot.position = new Vector3(
                        Mathf.Lerp(startPosition.x, endPosition.x, progress),
                        Mathf.Lerp(startPosition.y, endPosition.y, progress) + arc,
                        Mathf.Lerp(startPosition.z, endPosition.z, progress));
                },
                1f,
                flightDuration)
                .SetEase(Ease.Linear));
        flightSequence.Join(
            realKeyRoot
                .DOScale(
                    startScale * KeyFlightTargetScaleRatio,
                    flightDuration * KeyFlightScaleDurationRatio)
                .SetEase(Ease.InQuad));
        flightSequence.Insert(
            flightDuration * KeyFlightFadeDelayRatio,
            realKeyRenderer
                .DOFade(
                    0f,
                    flightDuration * KeyFlightFadeDurationRatio)
                .SetEase(Ease.Linear));
        flightSequence.OnComplete(() =>
        {
            _keyFlightTween = null;
            if (currentSession != _sessionVersion || !isActiveAndEnabled)
                return;

            realKeyRoot.position = endPosition;
            CompleteDoorUnlockAsync(currentSession).Forget();
        });
        flightSequence.OnKill(() =>
        {
            if (_keyFlightTween == flightSequence)
            {
                _keyFlightTween = null;
            }
        });
        _keyFlightTween = flightSequence;
    }

    private async UniTask CompleteDoorUnlockAsync(int currentSession)
    {
        if (realKeyRoot != null)
            realKeyRoot.gameObject.SetActive(false);

        if (_doorLockExecutor != null)
        {
            _doorLockExecutor.Execute();
        }
        else
        {
            Debug.LogError("[Level79Ctrl] DoorLock executor is missing.", this);
        }

        float elapsed = 0f;
        try
        {
            while ((_isPaused || !IsGameplayPlaying() || doorLockRoot != null) &&
                   elapsed < doorUnlockTimeout)
            {
                await UniTask.Yield(
                    PlayerLoopTiming.Update,
                    this.GetCancellationTokenOnDestroy());

                if (currentSession != _sessionVersion || !isActiveAndEnabled)
                    return;

                if (!_isPaused && IsGameplayPlaying() && doorLockRoot != null)
                    elapsed += Time.unscaledDeltaTime;
            }

            while (_isPaused || !IsGameplayPlaying())
            {
                await UniTask.Yield(
                    PlayerLoopTiming.Update,
                    this.GetCancellationTokenOnDestroy());

                if (currentSession != _sessionVersion || !isActiveAndEnabled)
                    return;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (currentSession != _sessionVersion || !isActiveAndEnabled)
            return;

        if (doorLockRoot != null)
        {
            Debug.LogWarning(
                "[Level79Ctrl] DoorLock did not finish before timeout; opening the exit trigger to avoid a blocked level.",
                this);
        }

        if (_successDoorCollider != null)
        {
            _successDoorCollider.isTrigger = true;
            _successDoorCollider.enabled = true;
        }
        else
        {
            Debug.LogError("[Level79Ctrl] Success door collider is missing.", this);
        }

        _keyFlying = false;
        _unlocking = false;
        _doorUnlocked = true;
        UnlockActor();
    }

    private void LockActor()
    {
        if (_actorLocked)
            return;

        _actorWasCtrlable = baseLevelCtrl == null || baseLevelCtrl.ActorCtrlable;
        baseLevelCtrl?.SetCtrlable(false);
        if (actor != null)
        {
            actor.SetMoveInput(0f);
            actor.StopMoving();
        }

        _actorLocked = true;
    }

    private void UnlockActor()
    {
        if (!_actorLocked)
            return;

        bool shouldRestoreControl =
            _actorWasCtrlable &&
            !_isPaused &&
            isActiveAndEnabled;
        if (shouldRestoreControl)
        {
            actor?.ResumeMoving();
            baseLevelCtrl?.SetCtrlable(true);
        }

        _actorLocked = false;
        _actorWasCtrlable = false;
    }

    #endregion

    #region Events, Setup And Cleanup

    private void RegisterEvents()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        _subscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt == null)
            return;

        bool shouldPause = !evt.NewState.Equals(GamePlayStateName.PLAYING);
        _isPaused = shouldPause;
        ClearPointer();
        SetTweensPaused(shouldPause);
    }

    private void OnGameReset(GameResetEvent evt)
    {
        _isPaused = true;
        CleanupRuntime();
    }

    private static bool IsGameplayPlaying()
    {
        PlayCtrl playCtrl = PlayCtrl.Instance;
        return playCtrl != null &&
               playCtrl.IsInitialized &&
               playCtrl.IsActive &&
               playCtrl.CheckState(GamePlayStateName.PLAYING);
    }

    private bool CanReceiveWorldInput()
    {
        bool allowStatueSwipeDuringKeyFlow =
            _actorLocked &&
            _actorWasCtrlable &&
            (_keyFlying || _unlocking);

        return IsGameplayPlaying() &&
               (baseLevelCtrl == null ||
                baseLevelCtrl.ActorCtrlable ||
                allowStatueSwipeDuringKeyFlow);
    }

    private void ResolveReferences()
    {
        baseLevelCtrl ??= GetComponent<BaseLevelCtrl>();
        actor ??= baseLevelCtrl != null
            ? baseLevelCtrl.Actor
            : GetComponentInChildren<ActorCtrl>(true);

        statueRoot ??= transform.Find("shixiang");
        statueRenderer ??= statueRoot != null
            ? statueRoot.GetComponentInChildren<SpriteRenderer>(true)
            : null;
        statueCollider ??= statueRoot != null
            ? statueRoot.GetComponent<Collider2D>()
            : null;
        statueBody ??= statueRoot != null
            ? statueRoot.GetComponent<Rigidbody2D>()
            : null;

        plate ??= transform.Find("Sp_l79_sz_01");
        Transform mechanismRoot = transform.Find("jiguan");
        leftMechanismStone ??= mechanismRoot?.Find("Sp_l89_01");
        rightMechanismStone ??= mechanismRoot?.Find("Sp_l89_02");
        fakeKeyRenderer ??= mechanismRoot
            ?.Find("Sp_key_1_01")
            ?.GetComponent<SpriteRenderer>();

        realKeyRoot ??= statueRoot?.Find("TrueKey_079");
        realKeyRenderer ??= realKeyRoot != null
            ? realKeyRoot.GetComponent<SpriteRenderer>()
            : null;

        successDoor ??= transform.Find("door_0");
        _successDoorCollider ??= successDoor != null
            ? successDoor.GetComponent<Collider2D>()
            : null;
        _lockPoint ??= successDoor?.Find("LockPoint");

        doorLockRoot ??= transform.Find("DoorLock");
        _doorLockExecutor ??= doorLockRoot != null
            ? doorLockRoot.GetComponent<BaseItemExecutor>()
            : null;

        if (worldCamera == null)
            worldCamera = Camera.main;
        if (worldCamera == null)
            worldCamera = FindFirstObjectByType<Camera>();
    }

    private void CacheInitialState()
    {
        if (leftMechanismStone != null)
        {
            _leftStoneInitialLocalPosition = leftMechanismStone.localPosition;
            _leftStoneInitialLocalRotation = leftMechanismStone.localRotation;
        }

        if (rightMechanismStone != null)
        {
            _rightStoneInitialLocalPosition = rightMechanismStone.localPosition;
            _rightStoneInitialLocalRotation = rightMechanismStone.localRotation;
        }

        if (worldCamera != null)
            _cameraInitialLocalPosition = worldCamera.transform.localPosition;
    }

    private void InitializePresentation()
    {
        _statueFrameIndex = 0;
        _keyFrameVisible = false;
        _statueOnPlate = false;
        _keyFlying = false;
        _unlocking = false;
        _doorUnlocked = false;
        _actorLocked = false;
        _actorWasCtrlable = false;
        _isPaused = false;

        if (_successDoorCollider != null)
            _successDoorCollider.enabled = false;

        if (realKeyRoot != null)
            realKeyRoot.gameObject.SetActive(true);

        SetStatueFrame(0, true);
    }

    private void ValidateConfiguration()
    {
        if (statueFrames == null || statueFrames.Length != StatueFrameCount)
        {
            Debug.LogError(
                $"[Level79Ctrl] Statue frame array must contain exactly {StatueFrameCount} sprites.",
                this);
        }
        else
        {
            for (int index = 0; index < statueFrames.Length; index++)
            {
                if (statueFrames[index] == null)
                    Debug.LogError($"[Level79Ctrl] Statue frame {index + 1:00} is missing.", this);
            }
        }

        if (fakeKeyRenderer != null &&
            fakeKeyRenderer.GetComponent<Collider2D>() != null)
        {
            Debug.LogError(
                "[Level79Ctrl] Fake key must not have a Collider2D.",
                fakeKeyRenderer);
        }

        if (statueRoot == null ||
            statueRenderer == null ||
            statueCollider == null ||
            statueBody == null ||
            plate == null ||
            leftMechanismStone == null ||
            rightMechanismStone == null ||
            fakeKeyRenderer == null ||
            realKeyRenderer == null ||
            _lockPoint == null ||
            _successDoorCollider == null ||
            doorLockRoot == null ||
            _doorLockExecutor == null)
        {
            Debug.LogError("[Level79Ctrl] One or more required Level079 references are missing.", this);
        }
    }

    private void SetTweensPaused(bool paused)
    {
        SetTweenPaused(_keyFadeTween, paused);
        SetTweenPaused(_keyFlightTween, paused);
        SetTweenPaused(_cameraShakeTween, paused);
        SetTweenPaused(_mechanismSequence, paused);
    }

    private static void SetTweenPaused(Tween tween, bool paused)
    {
        if (tween == null || !tween.IsActive())
            return;

        if (paused)
            tween.Pause();
        else
            tween.Play();
    }

    private void CleanupRuntime()
    {
        _sessionVersion++;
        ClearPointer();
        KillTween(ref _keyFadeTween);
        KillTween(ref _keyFlightTween);
        KillTween(ref _cameraShakeTween);
        KillSequence(ref _mechanismSequence);
        RestoreMechanismStoneRotations();
        RestoreCameraPosition();
        UnlockActor();
        _keyFlying = false;
        _unlocking = false;
    }

    private static void KillTween(ref Tween tween)
    {
        if (tween != null && tween.IsActive())
            tween.Kill();

        tween = null;
    }

    private static void KillSequence(ref Sequence sequence)
    {
        if (sequence != null && sequence.IsActive())
            sequence.Kill();

        sequence = null;
    }

    #endregion
}

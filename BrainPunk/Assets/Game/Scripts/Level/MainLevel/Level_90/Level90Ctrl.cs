using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level 090 双水槽控制器。
/// 仅负责本关的潜水按钮、中央隔板拖动边界、两侧水体与角色的联动，以及延迟镜头跟随。
/// </summary>
public sealed class Level90Ctrl : MonoBehaviour
{
    #region Inspector

    [Header("角色与操作")]
    [Tooltip("本关角色；用于响应潜水事件和判断镜头跟随时机。")]
    [SerializeField] private ActorCtrl actor;

    [Tooltip("中央可横向拖动的隔板根节点。")]
    [SerializeField] private Transform divider;

    [Tooltip("本关主相机；用于输入坐标换算和角色横向跟随。")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("潜水按钮开局隐藏时长，保持与 Level 024 相同的两秒节奏。")]
    [Min(0f)]
    [SerializeField] private float downButtonDelay = 2f;

    [Header("镜头跟随")]
    [Tooltip("角色到达该屏幕横向位置后，本轮开始跟随；0.5 表示屏幕中线。")]
    [Range(0f, 1f)]
    [SerializeField] private float cameraFollowStartViewportX = 0.5f;

    [Tooltip("镜头横向跟随的平滑时间；0 表示立即跟随。")]
    [Min(0f)]
    [SerializeField] private float cameraFollowSmoothTime = 0.2f;

    [Header("双水槽")]
    [Tooltip("左侧完整 Water 根节点，必须包含 InteractableWater 与 Buoyancy 子节点。")]
    [SerializeField] private Transform leftWater;

    [Tooltip("右侧完整 Water 根节点，必须包含 InteractableWater 与 Buoyancy 子节点。")]
    [SerializeField] private Transform rightWater;

    [Tooltip("两侧水槽内部底面世界坐标 Y；用于计算各自初始水量。")]
    [SerializeField] private float basinBottomY;

    [Tooltip("水面允许到达的最高世界坐标 Y；任一侧到顶后会限制隔板继续移动。")]
    [SerializeField] private float maxSurfaceY;

    [Tooltip("隔板单侧额外人工位移上限；0 表示不加人工限制，仅由液面到顶条件决定。")]
    [Min(0f)]
    [SerializeField] private float maxDividerTravel;

    #endregion

    #region Runtime state

    private const float MinimumWaterWidth = 0.05f;
    private const float PositionEpsilon = 0.0001f;

    private EventSubscriber _subscriber;
    private Transform _downButton;

    private Vector3 _cameraInitialPosition;
    private float _cameraFollowVelocityX;
    private bool _cameraFollowReady;
    private bool _hasStartedCameraFollow;

    private InteractableWater _leftWaterComponent;
    private InteractableWater _rightWaterComponent;
    private Rigidbody2D _actorBody;
    private Collider2D _actorCollider;
    private Collider2D _leftBuoyancyCollider;
    private Collider2D _rightBuoyancyCollider;
    private Collider2D _pendingActorLiftWater;

    private Vector3 _dividerStartPosition;
    private Vector3 _leftWaterStartPosition;
    private Vector3 _rightWaterStartPosition;
    private Vector3 _leftWaterStartScale;
    private Vector3 _rightWaterStartScale;

    private float _leftInitialWidth;
    private float _rightInitialWidth;
    private float _leftVisualHeight;
    private float _rightVisualHeight;
    private float _leftVolume;
    private float _rightVolume;
    private float _leftSurfaceY;
    private float _rightSurfaceY;
    private float _pendingActorLift;
    private float _minimumDividerDelta;
    private float _maximumDividerDelta;
    private float _lastDividerX;
    private bool _balanceReady;
    private bool _isDividerDragging;
    private bool _isPaused;
    private float _dividerDragOffsetX;

    #endregion

    #region Unity lifecycle

    private void Awake()
    {
        AddListener();
        InitializeWaterBalance();
        InitializeCameraFollow();
    }

    private void Start()
    {
        ShowDiveButton().Forget();
    }

    private void Update()
    {
        if (_balanceReady && !_isPaused)
            HandleDividerInput();
    }

    private void FixedUpdate()
    {
        ApplyPendingActorLift();
    }

    private void LateUpdate()
    {
        if (_balanceReady &&
            divider != null &&
            Mathf.Abs(divider.position.x - _lastDividerX) > PositionEpsilon)
        {
            SyncWaterBalance();
        }

        UpdateCameraFollow();
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
    }

    #endregion

    #region Input

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerDiveEvent>(OnPlayerDive);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
    }

    private void OnPlayerDive(PlayerDiveEvent evt)
    {
        actor?.AddDiveVelocity();
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        _isPaused = !evt.NewState.Equals(GamePlayStateName.PLAYING);
        if (_isPaused)
        {
            _isDividerDragging = false;
            _cameraFollowVelocityX = 0f;
            _pendingActorLift = 0f;
            _pendingActorLiftWater = null;
        }
    }

    private async UniTask ShowDiveButton()
    {
        var panel = PlayCtrl.Instance?.CurrentGamePlayPanel;
        if (panel == null)
            return;

        _downButton = panel.transform.Find("Root/down_btn");
        if (_downButton == null)
            return;

        _downButton.gameObject.SetActive(false);
        await UniTask.WaitForSeconds(
            downButtonDelay,
            ignoreTimeScale: true,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        if (_downButton != null)
            _downButton.gameObject.SetActive(true);
    }

    #endregion

    #region Camera follow

    private void InitializeCameraFollow()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null || actor == null)
        {
            Debug.LogWarning("[Level90Ctrl] 角色或主相机引用不完整，镜头跟随未启用。", this);
            return;
        }

        _cameraInitialPosition = mainCamera.transform.position;
        _cameraFollowReady = true;
    }

    private void UpdateCameraFollow()
    {
        if (!_cameraFollowReady || _isPaused || mainCamera == null || actor == null)
            return;

        if (!_hasStartedCameraFollow)
        {
            Vector3 actorViewportPosition =
                mainCamera.WorldToViewportPoint(actor.transform.position);
            if (actorViewportPosition.z <= 0f ||
                actorViewportPosition.x < Mathf.Clamp01(cameraFollowStartViewportX))
            {
                return;
            }

            _hasStartedCameraFollow = true;
            _cameraFollowVelocityX = 0f;
        }

        Vector3 cameraPosition = mainCamera.transform.position;
        float targetX = Mathf.Max(_cameraInitialPosition.x, actor.transform.position.x);
        cameraPosition.x = cameraFollowSmoothTime > 0f
            ? Mathf.SmoothDamp(
                cameraPosition.x,
                targetX,
                ref _cameraFollowVelocityX,
                cameraFollowSmoothTime)
            : targetX;
        cameraPosition.y = _cameraInitialPosition.y;
        cameraPosition.z = _cameraInitialPosition.z;
        mainCamera.transform.position = cameraPosition;
    }

    #endregion

    #region Water balance

    private void InitializeWaterBalance()
    {
        if (divider == null || leftWater == null || rightWater == null)
        {
            Debug.LogWarning("[Level90Ctrl] 双水槽引用不完整，液面联动未启用。", this);
            return;
        }

        _leftWaterComponent = leftWater.GetComponent<InteractableWater>();
        _rightWaterComponent = rightWater.GetComponent<InteractableWater>();
        if (_leftWaterComponent == null || _rightWaterComponent == null)
        {
            Debug.LogWarning("[Level90Ctrl] Water 根节点缺少 InteractableWater，液面联动未启用。", this);
            return;
        }

        InitializeActorLift();

        float maximumDepth = maxSurfaceY - basinBottomY;
        if (maximumDepth <= 0f)
        {
            Debug.LogWarning("[Level90Ctrl] 水槽顶面必须高于底面，液面联动未启用。", this);
            return;
        }

        _dividerStartPosition = divider.position;
        _leftWaterStartPosition = leftWater.position;
        _rightWaterStartPosition = rightWater.position;
        _leftWaterStartScale = leftWater.localScale;
        _rightWaterStartScale = rightWater.localScale;

        _leftInitialWidth = GetWorldWidth(_leftWaterComponent);
        _rightInitialWidth = GetWorldWidth(_rightWaterComponent);
        _leftVisualHeight = GetWorldHeight(_leftWaterComponent);
        _rightVisualHeight = GetWorldHeight(_rightWaterComponent);

        if (_leftInitialWidth <= MinimumWaterWidth || _rightInitialWidth <= MinimumWaterWidth)
        {
            Debug.LogWarning("[Level90Ctrl] Water 宽度无效，液面联动未启用。", this);
            return;
        }

        float leftInitialSurface = _leftWaterStartPosition.y + _leftVisualHeight * 0.5f;
        float rightInitialSurface = _rightWaterStartPosition.y + _rightVisualHeight * 0.5f;
        float leftInitialDepth = Mathf.Clamp(leftInitialSurface - basinBottomY, 0f, maximumDepth);
        float rightInitialDepth = Mathf.Clamp(rightInitialSurface - basinBottomY, 0f, maximumDepth);

        _leftVolume = _leftInitialWidth * leftInitialDepth;
        _rightVolume = _rightInitialWidth * rightInitialDepth;
        _leftSurfaceY = basinBottomY + leftInitialDepth;
        _rightSurfaceY = basinBottomY + rightInitialDepth;

        float configuredTravel = maxDividerTravel > PositionEpsilon
            ? maxDividerTravel
            : float.PositiveInfinity;
        float leftWidthAtTop = Mathf.Max(MinimumWaterWidth, _leftVolume / maximumDepth);
        float rightWidthAtTop = Mathf.Max(MinimumWaterWidth, _rightVolume / maximumDepth);

        _minimumDividerDelta = Mathf.Max(-configuredTravel, leftWidthAtTop - _leftInitialWidth);
        _maximumDividerDelta = Mathf.Min(configuredTravel, _rightInitialWidth - rightWidthAtTop);
        _minimumDividerDelta = Mathf.Min(0f, _minimumDividerDelta);
        _maximumDividerDelta = Mathf.Max(0f, _maximumDividerDelta);

        if (mainCamera == null)
            mainCamera = Camera.main;

        _balanceReady = true;
        SyncWaterBalance();
    }

    private void SyncWaterBalance()
    {
        bool actorInLeftWater = IsActorInsideWater(_leftBuoyancyCollider);
        bool actorInRightWater = IsActorInsideWater(_rightBuoyancyCollider);
        float previousLeftSurfaceY = _leftSurfaceY;
        float previousRightSurfaceY = _rightSurfaceY;

        float dividerDelta = Mathf.Clamp(
            divider.position.x - _dividerStartPosition.x,
            _minimumDividerDelta,
            _maximumDividerDelta);

        Vector3 dividerPosition = divider.position;
        dividerPosition.x = _dividerStartPosition.x + dividerDelta;
        dividerPosition.y = _dividerStartPosition.y;
        dividerPosition.z = _dividerStartPosition.z;
        divider.position = dividerPosition;

        float leftWidth = Mathf.Max(MinimumWaterWidth, _leftInitialWidth + dividerDelta);
        float rightWidth = Mathf.Max(MinimumWaterWidth, _rightInitialWidth - dividerDelta);

        _leftSurfaceY = ApplyWaterState(
            leftWater,
            _leftWaterStartPosition,
            _leftWaterStartScale,
            _leftInitialWidth,
            _leftVisualHeight,
            _leftVolume,
            leftWidth,
            dividerDelta);
        _rightSurfaceY = ApplyWaterState(
            rightWater,
            _rightWaterStartPosition,
            _rightWaterStartScale,
            _rightInitialWidth,
            _rightVisualHeight,
            _rightVolume,
            rightWidth,
            dividerDelta);

        Collider2D actorWater = SelectActorWater(actorInLeftWater, actorInRightWater);
        if (actorWater != null && actorWater == _leftBuoyancyCollider)
        {
            AccumulateActorLift(actorWater, previousLeftSurfaceY, _leftSurfaceY);
        }
        else if (actorWater != null && actorWater == _rightBuoyancyCollider)
        {
            AccumulateActorLift(actorWater, previousRightSurfaceY, _rightSurfaceY);
        }
        else
        {
            _pendingActorLift = 0f;
            _pendingActorLiftWater = null;
        }

        _lastDividerX = divider.position.x;
    }

    private float ApplyWaterState(
        Transform waterTransform,
        Vector3 initialPosition,
        Vector3 initialScale,
        float initialWidth,
        float visualHeight,
        float volume,
        float currentWidth,
        float dividerDelta)
    {
        Vector3 scale = initialScale;
        scale.x = initialScale.x * currentWidth / initialWidth;
        waterTransform.localScale = scale;

        float surfaceY = Mathf.Min(maxSurfaceY, basinBottomY + volume / currentWidth);
        Vector3 position = initialPosition;
        position.x = initialPosition.x + dividerDelta * 0.5f;
        position.y = surfaceY - visualHeight * 0.5f;
        waterTransform.position = position;
        return surfaceY;
    }

    private void InitializeActorLift()
    {
        if (actor != null)
        {
            _actorBody = actor.GetComponent<Rigidbody2D>();
            _actorCollider = actor.GetComponent<Collider2D>();
        }

        _leftBuoyancyCollider = GetBuoyancyCollider(leftWater);
        _rightBuoyancyCollider = GetBuoyancyCollider(rightWater);
        if (_actorBody == null ||
            _actorCollider == null ||
            _leftBuoyancyCollider == null ||
            _rightBuoyancyCollider == null)
        {
            Debug.LogWarning(
                "[Level90Ctrl] 角色或 Buoyancy 碰撞体引用不完整，水面抬升角色未启用。",
                this);
        }
    }

    private bool IsActorInsideWater(Collider2D waterCollider)
    {
        if (_actorBody == null ||
            _actorCollider == null ||
            waterCollider == null ||
            !_actorBody.simulated ||
            !_actorCollider.enabled ||
            !waterCollider.enabled)
        {
            return false;
        }

        Bounds actorBounds = _actorCollider.bounds;
        Bounds waterBounds = waterCollider.bounds;
        return actorBounds.center.x >= waterBounds.min.x - PositionEpsilon &&
               actorBounds.center.x <= waterBounds.max.x + PositionEpsilon &&
               actorBounds.min.y <= waterBounds.max.y + PositionEpsilon &&
               actorBounds.max.y >= waterBounds.min.y - PositionEpsilon;
    }

    private Collider2D SelectActorWater(bool actorInLeftWater, bool actorInRightWater)
    {
        if (!actorInLeftWater)
            return actorInRightWater ? _rightBuoyancyCollider : null;
        if (!actorInRightWater)
            return _leftBuoyancyCollider;

        return _actorCollider.bounds.center.x <= divider.position.x
            ? _leftBuoyancyCollider
            : _rightBuoyancyCollider;
    }

    private void AccumulateActorLift(
        Collider2D waterCollider,
        float previousSurfaceY,
        float currentSurfaceY)
    {
        if (_actorCollider == null)
        {
            _pendingActorLift = 0f;
            _pendingActorLiftWater = null;
            return;
        }

        if (_pendingActorLiftWater != waterCollider)
            _pendingActorLift = 0f;

        _pendingActorLiftWater = waterCollider;
        float surfaceDelta = currentSurfaceY - previousSurfaceY;
        float maximumLift = Mathf.Max(
            0f,
            currentSurfaceY - _actorCollider.bounds.center.y);
        _pendingActorLift = Mathf.Clamp(
            _pendingActorLift + surfaceDelta,
            0f,
            maximumLift);
    }

    private void ApplyPendingActorLift()
    {
        float liftDelta = _pendingActorLift;
        _pendingActorLift = 0f;
        _pendingActorLiftWater = null;
        if (liftDelta <= PositionEpsilon ||
            _isPaused ||
            actor == null ||
            actor.IsDied ||
            actor.IsKnocked ||
            _actorBody == null ||
            !_actorBody.simulated)
        {
            return;
        }

        Vector2 actorPosition = _actorBody.position;
        actorPosition.y += liftDelta;
        _actorBody.position = actorPosition;
    }

    private static Collider2D GetBuoyancyCollider(Transform waterTransform)
    {
        if (waterTransform == null)
            return null;

        BuoyancyEffector2D effector =
            waterTransform.GetComponentInChildren<BuoyancyEffector2D>(true);
        return effector != null ? effector.GetComponent<Collider2D>() : null;
    }

    private static float GetWorldWidth(InteractableWater water)
    {
        return water.Width * Mathf.Abs(water.transform.lossyScale.x);
    }

    private static float GetWorldHeight(InteractableWater water)
    {
        return water.Height * Mathf.Abs(water.transform.lossyScale.y);
    }

    #endregion

    #region Divider drag

    private void HandleDividerInput()
    {
        if (mainCamera == null)
            return;

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
        {
            TryBeginDividerDrag(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0) && _isDividerDragging)
        {
            UpdateDividerDrag(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _isDividerDragging = false;
        }
#elif UNITY_ANDROID || UNITY_IOS
        if (Input.touchCount <= 0)
            return;

        Touch touch = Input.GetTouch(0);
        if (touch.phase == TouchPhase.Began)
        {
            TryBeginDividerDrag(touch.position);
        }
        else if (touch.phase == TouchPhase.Moved && _isDividerDragging)
        {
            UpdateDividerDrag(touch.position);
        }
        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            _isDividerDragging = false;
        }
#endif
    }

    private void TryBeginDividerDrag(Vector2 screenPosition)
    {
        Vector3 worldPosition = ScreenToWorldPosition(screenPosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform != divider && !hitTransform.IsChildOf(divider))
                continue;

            _isDividerDragging = true;
            _dividerDragOffsetX = divider.position.x - worldPosition.x;
            return;
        }
    }

    private void UpdateDividerDrag(Vector2 screenPosition)
    {
        Vector3 worldPosition = ScreenToWorldPosition(screenPosition);
        float targetX = worldPosition.x + _dividerDragOffsetX;
        float clampedDelta = Mathf.Clamp(
            targetX - _dividerStartPosition.x,
            _minimumDividerDelta,
            _maximumDividerDelta);

        Vector3 position = divider.position;
        position.x = _dividerStartPosition.x + clampedDelta;
        position.y = _dividerStartPosition.y;
        position.z = _dividerStartPosition.z;
        divider.position = position;
        SyncWaterBalance();
    }

    private Vector3 ScreenToWorldPosition(Vector2 screenPosition)
    {
        float zDistance = Mathf.Abs(mainCamera.transform.position.z - divider.position.z);
        return mainCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, zDistance));
    }

    #endregion
}

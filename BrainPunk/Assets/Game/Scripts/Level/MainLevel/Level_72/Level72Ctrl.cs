using Bear.EventSystem;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level 72 专用旋转控制器：从 plane 的碰撞区域开始拖动时，旋转整个 RotateGroup。
/// </summary>
public sealed class Level72Ctrl : MonoBehaviour
{
    private const int NoFinger = int.MinValue;

    #region 序列化配置

    [Header("旋转对象")]
    [Tooltip("本关需要整体旋转的根节点。")]
    [SerializeField] private Transform rotateGroup;

    [Tooltip("旋转输入区域根节点；其子层级启用中的 Collider2D 都可开始拖动。")]
    [SerializeField] private Transform plane;

    [Tooltip("输入坐标转换使用的关卡相机。")]
    [SerializeField] private Camera inputCamera;

    [Header("旋转手感")]
    [Tooltip("手指角度变化映射到 RotateGroup 的倍率。")]
    [Min(0f)]
    [SerializeField] private float sensitivity = 1f;

    [Tooltip("是否反转手指拖动方向。")]
    [SerializeField] private bool invertRotation;

    [Tooltip("指针距离旋转中心小于该屏幕像素值时暂停计算，避免中心附近角度抖动。")]
    [Min(0f)]
    [SerializeField] private float centerDeadZonePixels = 12f;

    #endregion

    #region 运行时状态

    private Collider2D[] _planeAreas;
    private EventSubscriber _subscriber;
    private Quaternion _initialLocalRotation;
    private bool _isPaused;
    private bool _isDragging;
    private bool _usingEditorMouse;
    private bool _hasPointerAngle;
    private int _activeFingerId = NoFinger;
    private float _previousPointerAngle;

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        ResolveReferences();
        CachePlaneAreas();

        if (rotateGroup != null)
            _initialLocalRotation = rotateGroup.localRotation;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        _subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        _subscriber.Subscribe<GameResumeEvent>(OnGameResume);
        _subscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref _subscriber);
        CancelDrag();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref _subscriber);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            CancelDrag();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            CancelDrag();
    }

    private void Update()
    {
        if (_isPaused || rotateGroup == null || plane == null || inputCamera == null)
        {
            CancelDrag();
            return;
        }

#if UNITY_EDITOR
        if (_isDragging)
        {
            if (_usingEditorMouse)
                UpdateMouseInput();
            else
                UpdateTouchInput();

            return;
        }

        if (Input.touchCount == 0)
        {
            UpdateMouseInput();
            return;
        }
#endif

        UpdateTouchInput();
    }

    #endregion

    #region 输入

#if UNITY_EDITOR
    private void UpdateMouseInput()
    {
        if (!_isDragging)
        {
            if (Input.GetMouseButtonDown(0))
                TryBeginDrag(Input.mousePosition, -1, true);

            return;
        }

        if (!_usingEditorMouse)
            return;

        if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
        {
            CancelDrag();
            return;
        }

        UpdateRotation(Input.mousePosition);
    }
#endif

    private void UpdateTouchInput()
    {
        if (_isDragging)
        {
            if (_usingEditorMouse)
                return;

            if (!TryGetActiveTouch(out Touch activeTouch))
            {
                CancelDrag();
                return;
            }

            if (activeTouch.phase == TouchPhase.Ended || activeTouch.phase == TouchPhase.Canceled)
            {
                CancelDrag();
                return;
            }

            UpdateRotation(activeTouch.position);
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began)
                continue;

            if (TryBeginDrag(touch.position, touch.fingerId, false))
                break;
        }
    }

    private bool TryBeginDrag(Vector2 screenPosition, int fingerId, bool usingEditorMouse)
    {
        if (_isDragging || IsPointerOverUi(screenPosition, fingerId) || !IsInsidePlane(screenPosition))
            return false;

        if (!TryGetPointerAngle(screenPosition, out float pointerAngle))
            return false;

        _isDragging = true;
        _usingEditorMouse = usingEditorMouse;
        _activeFingerId = fingerId;
        _previousPointerAngle = pointerAngle;
        _hasPointerAngle = true;
        return true;
    }

    private void UpdateRotation(Vector2 screenPosition)
    {
        if (!TryGetPointerAngle(screenPosition, out float pointerAngle))
        {
            _hasPointerAngle = false;
            return;
        }

        if (!_hasPointerAngle)
        {
            _previousPointerAngle = pointerAngle;
            _hasPointerAngle = true;
            return;
        }

        float angleDelta = Mathf.DeltaAngle(_previousPointerAngle, pointerAngle);
        _previousPointerAngle = pointerAngle;

        if (invertRotation)
            angleDelta = -angleDelta;

        rotateGroup.Rotate(0f, 0f, angleDelta * Mathf.Max(0f, sensitivity), Space.Self);
    }

    private void CancelDrag()
    {
        _isDragging = false;
        _usingEditorMouse = false;
        _hasPointerAngle = false;
        _activeFingerId = NoFinger;
    }

    private bool TryGetActiveTouch(out Touch activeTouch)
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != _activeFingerId)
                continue;

            activeTouch = touch;
            return true;
        }

        activeTouch = default;
        return false;
    }

    private bool IsInsidePlane(Vector2 screenPosition)
    {
        if (_planeAreas == null || _planeAreas.Length == 0)
            return false;

        Vector2 worldPoint = ScreenToWorldPoint(screenPosition);
        for (int i = 0; i < _planeAreas.Length; i++)
        {
            Collider2D area = _planeAreas[i];
            if (area != null && area.enabled && area.gameObject.activeInHierarchy && area.OverlapPoint(worldPoint))
                return true;
        }

        return false;
    }

    private bool TryGetPointerAngle(Vector2 screenPosition, out float pointerAngle)
    {
        Vector2 pivotScreenPosition = inputCamera.WorldToScreenPoint(rotateGroup.position);
        Vector2 direction = screenPosition - pivotScreenPosition;
        float deadZone = Mathf.Max(0f, centerDeadZonePixels);
        if (direction.sqrMagnitude <= deadZone * deadZone)
        {
            pointerAngle = 0f;
            return false;
        }

        pointerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return true;
    }

    private Vector2 ScreenToWorldPoint(Vector2 screenPosition)
    {
        float zDistance = Mathf.Abs(inputCamera.transform.position.z - plane.position.z);
        Vector3 worldPosition = inputCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, zDistance));
        return new Vector2(worldPosition.x, worldPosition.y);
    }

    private static bool IsPointerOverUi(Vector2 screenPosition, int fingerId)
    {
        return fingerId >= 0
            ? InputUtils.IsPointerOverUI(fingerId)
            : InputUtils.IsPointerOverUI(screenPosition);
    }

    #endregion

    #region 生命周期事件

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt == null)
            return;

        _isPaused = !evt.NewState.Equals(GamePlayStateName.PLAYING);
        if (_isPaused)
            CancelDrag();
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        _isPaused = true;
        CancelDrag();
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        _isPaused = false;
    }

    private void OnGameReset(GameResetEvent evt)
    {
        CancelDrag();

        if (rotateGroup != null)
            rotateGroup.localRotation = _initialLocalRotation;
    }

    #endregion

    #region 引用解析

    private void ResolveReferences()
    {
        if (rotateGroup == null)
            rotateGroup = transform.Find("RotateGroup");

        if (plane == null && rotateGroup != null)
            plane = rotateGroup.Find("plane");

        if (inputCamera == null)
            inputCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
    }

    private void CachePlaneAreas()
    {
        _planeAreas = plane != null
            ? plane.GetComponentsInChildren<Collider2D>(true)
            : null;

        if (rotateGroup == null || plane == null || inputCamera == null || _planeAreas == null || _planeAreas.Length == 0)
        {
            Debug.LogWarning(
                "[Level72Ctrl] RotateGroup、plane、关卡相机或 plane 子碰撞盒缺失，旋转输入将保持关闭。",
                this);
        }
    }

    #endregion
}

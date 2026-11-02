using Bear.EventSystem;
using Game.Events;
using Game.Play;
using UnityEngine;

/// <summary>
/// Level123 的场景按钮输入。按钮在吸附前仍然是普通跳跃按钮，
/// 只有明显位移才进入拖拽；吸附后拖拽被锁定，点击交给关卡机关。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class Level123WorldButtonClick : MonoBehaviour, IEventSender
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Level123Ctrl levelCtrl;
    [SerializeField, Min(0f)] private float dragThresholdPixels = 60f;
    [SerializeField] private bool clampToScreen = true;

    private Collider2D buttonCollider;
    private Camera inputCamera;
    private bool clickEnabled;
    private bool dragEnabled;
    private bool pointerTracking;
    private bool pointerMoved;
    private bool pointerDragging;
    private Vector2 pointerStartPosition;
    private Vector2 pointerWorldOffset;
    private float pointerZ;
    private int activeFingerId = -1;

    private void Awake()
    {
        buttonCollider = GetComponent<Collider2D>();
        ResolveCamera();
    }

    private void Update()
    {
        if (!IsGamePlaying())
        {
            if (pointerTracking)
                CancelPointer();
            return;
        }

        if ((!clickEnabled && !dragEnabled) || buttonCollider == null)
            return;

#if UNITY_EDITOR
        HandleMouseInput();
#elif UNITY_ANDROID || UNITY_IOS
        HandleTouchInput();
#else
        HandleMouseInput();
#endif
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryBeginPointer(Input.mousePosition);
            return;
        }

        if (!pointerTracking)
            return;

        Vector2 screenPosition = Input.mousePosition;
        if (Input.GetMouseButton(0))
            TrackPointer(screenPosition);
        else if (Input.GetMouseButtonUp(0))
            CompletePointer(screenPosition, false);
    }

    private void HandleTouchInput()
    {
        bool activeTouchFound = false;
        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (!pointerTracking && touch.phase == TouchPhase.Began)
            {
                if (TryBeginPointer(touch.position))
                {
                    activeFingerId = touch.fingerId;
                    activeTouchFound = true;
                }
                continue;
            }

            if (!pointerTracking || touch.fingerId != activeFingerId)
                continue;

            activeTouchFound = true;
            switch (touch.phase)
            {
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    TrackPointer(touch.position);
                    break;
                case TouchPhase.Ended:
                    CompletePointer(touch.position, false);
                    break;
                case TouchPhase.Canceled:
                    CompletePointer(touch.position, true);
                    break;
            }
        }

        if (pointerTracking && activeFingerId >= 0 && !activeTouchFound)
            CancelPointer();
    }

    private bool TryBeginPointer(Vector2 screenPosition)
    {
        if (!IsPointerOnButton(screenPosition))
            return false;

        pointerTracking = true;
        pointerMoved = false;
        pointerDragging = false;
        pointerStartPosition = screenPosition;
        pointerWorldOffset = (Vector2)transform.position - ScreenToWorldPoint(screenPosition);
        pointerZ = transform.position.z;
        return true;
    }

    private void TrackPointer(Vector2 screenPosition)
    {
        if (!pointerTracking)
            return;

        float threshold = Mathf.Max(0f, dragThresholdPixels);
        if (!pointerMoved)
        {
            pointerMoved = (screenPosition - pointerStartPosition).sqrMagnitude > threshold * threshold;
        }

        if (!pointerDragging && pointerMoved && dragEnabled)
            pointerDragging = true;

        if (pointerDragging)
            MovePointer(screenPosition);
    }

    private void CompletePointer(Vector2 screenPosition, bool canceled)
    {
        TrackPointer(screenPosition);
        bool wasDragging = pointerDragging;
        bool shouldClick = !canceled && !pointerMoved && clickEnabled;

        CancelPointer();
        if (canceled || !IsGamePlaying())
            return;

        if (wasDragging)
        {
            levelCtrl?.TryInstallWorldUpButton();
            return;
        }

        if (!shouldClick)
            return;

        if (levelCtrl == null || !levelCtrl.IsWorldUpButtonInstalled)
            this.DispatchEvent(Witness<PlayerJumpEvent>._);

        levelCtrl?.ClickWorldUpButton();
    }

    private void MovePointer(Vector2 screenPosition)
    {
        Vector2 worldPosition = ScreenToWorldPoint(screenPosition) + pointerWorldOffset;
        Vector3 nextPosition = new Vector3(worldPosition.x, worldPosition.y, pointerZ);

        if (clampToScreen && inputCamera != null)
        {
            Vector3 screenPoint = inputCamera.WorldToScreenPoint(nextPosition);
            screenPoint.x = Mathf.Clamp(screenPoint.x, 0f, Screen.width);
            screenPoint.y = Mathf.Clamp(screenPoint.y, 0f, Screen.height);
            float zDistance = Mathf.Abs(inputCamera.transform.position.z - pointerZ);
            nextPosition = inputCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, zDistance));
            nextPosition.z = pointerZ;
        }

        transform.position = nextPosition;
    }

    private bool IsPointerOnButton(Vector2 screenPosition)
    {
        ResolveCamera();
        return inputCamera != null && buttonCollider != null &&
               buttonCollider.OverlapPoint(ScreenToWorldPoint(screenPosition));
    }

    private void CancelPointer()
    {
        pointerTracking = false;
        pointerMoved = false;
        pointerDragging = false;
        activeFingerId = -1;
    }

    private Vector2 ScreenToWorldPoint(Vector2 screenPosition)
    {
        float zDistance = Mathf.Abs(inputCamera.transform.position.z - transform.position.z);
        Vector3 worldPosition = inputCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, zDistance));
        return worldPosition;
    }

    private void ResolveCamera()
    {
        if (inputCamera != null)
            return;
        inputCamera = targetCamera != null ? targetCamera : Camera.main;
        if (inputCamera == null)
            inputCamera = FindFirstObjectByType<Camera>();
    }

    private static bool IsGamePlaying()
    {
        return PlayCtrl.Instance != null &&
               PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
    }

    /// <summary>开关短按分支；吸附后保持开启以便点击刺墙机关。</summary>
    public void SetEnableClick(bool enable)
    {
        clickEnabled = enable;
        if (!enable && !dragEnabled)
            CancelPointer();
    }

    /// <summary>开关拖拽分支；按钮吸附成功后由关卡控制器关闭。</summary>
    public void SetEnableDrag(bool enable)
    {
        dragEnabled = enable;
        if (!enable && pointerDragging)
            CancelPointer();
    }

    private void OnDisable()
    {
        clickEnabled = false;
        dragEnabled = false;
        CancelPointer();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            CancelPointer();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            CancelPointer();
    }
}

using System;
using UnityEngine;

/// <summary>
/// 第 69 关专用的单指按住输入跟踪器，只负责 FakeSystemTips 输入区域，不直接开启物理盾牌。
/// </summary>
public sealed class Level69HoldInput : MonoBehaviour
{
    #region 序列化配置

    [Header("输入区域")]
    [Tooltip("仅用于触摸命中的输入碰撞体；必须与阻挡炸弹的盾牌碰撞体分离。")]
    [SerializeField] private Collider2D holdArea;

    [Tooltip("触摸坐标转换使用的相机；为空时运行态使用 Main Camera。")]
    [SerializeField] private Camera inputCamera;

    #endregion

    #region 运行态状态

    private bool _trackingEnabled;
    private int _ownerFingerId = int.MinValue;

    /// <summary>
    /// 当前是否存在仍位于输入区域内的按住手指；仅在本关活动会话内有效。
    /// </summary>
    public bool IsHolding => _ownerFingerId != int.MinValue;

    /// <summary>
    /// 当前按住所有者的 fingerId；Editor 鼠标为 -1，无所有者时为 int.MinValue。
    /// </summary>
    public int OwnerFingerId => _ownerFingerId;

    /// <summary>
    /// 首次在输入区域内按下时触发；一次只允许一个 owner，其他手指不会覆盖。
    /// </summary>
    public event Action<int> HoldStarted;

    /// <summary>
    /// owner 松开、取消、移出、暂停或组件禁用时触发；不会响应其他手指的结束。
    /// </summary>
    public event Action<int> HoldEnded;

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        if (holdArea == null)
            holdArea = GetComponent<Collider2D>();

        SetAreaEnabled(false);
    }

    private void Update()
    {
        if (!_trackingEnabled || holdArea == null)
            return;

#if UNITY_EDITOR
        UpdateMouseInput();
#endif
        UpdateTouchInput();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            CancelCurrentHold();
    }

    private void OnDisable()
    {
        _trackingEnabled = false;
        SetAreaEnabled(false);
        CancelCurrentHold();
    }

    private void OnDestroy()
    {
        HoldStarted = null;
        HoldEnded = null;
    }

    #endregion

    #region 对外控制

    /// <summary>
    /// 开启或关闭本次提示尝试的输入跟踪；关闭时会立即取消当前 owner。
    /// </summary>
    /// <param name="enabled">是否允许新的按住输入并继续跟踪 owner。</param>
    public void SetTrackingEnabled(bool enabled)
    {
        _trackingEnabled = enabled;
        SetAreaEnabled(enabled);

        if (!enabled)
            CancelCurrentHold();
    }

    /// <summary>
    /// 主动清除当前 owner；用于关卡失败、重开、退出和提示尝试切换。
    /// </summary>
    public void CancelCurrentHold()
    {
        if (!IsHolding)
            return;

        int releasedFingerId = _ownerFingerId;
        _ownerFingerId = int.MinValue;
        HoldEnded?.Invoke(releasedFingerId);
    }

    #endregion

    #region 输入处理

#if UNITY_EDITOR
    private void UpdateMouseInput()
    {
        if (_ownerFingerId == -1)
        {
            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0) || !ContainsScreenPoint(Input.mousePosition))
                ReleaseOwner(-1);
            return;
        }

        if (!IsHolding && Input.GetMouseButtonDown(0) && ContainsScreenPoint(Input.mousePosition))
            AcquireOwner(-1);
    }
#endif

    private void UpdateTouchInput()
    {
        if (IsHolding && _ownerFingerId >= 0)
        {
            bool ownerFound = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId != _ownerFingerId)
                    continue;

                ownerFound = true;
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled ||
                    !ContainsScreenPoint(touch.position))
                {
                    ReleaseOwner(touch.fingerId);
                }

                break;
            }

            if (!ownerFound)
                ReleaseOwner(_ownerFingerId);

            return;
        }

        if (IsHolding)
            return;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began || !ContainsScreenPoint(touch.position))
                continue;

            AcquireOwner(touch.fingerId);
            break;
        }
    }

    private void AcquireOwner(int fingerId)
    {
        if (IsHolding || !_trackingEnabled)
            return;

        _ownerFingerId = fingerId;
        HoldStarted?.Invoke(fingerId);
    }

    private void ReleaseOwner(int fingerId)
    {
        if (_ownerFingerId != fingerId)
            return;

        _ownerFingerId = int.MinValue;
        HoldEnded?.Invoke(fingerId);
    }

    private bool ContainsScreenPoint(Vector2 screenPosition)
    {
        Camera cameraToUse = inputCamera != null ? inputCamera : Camera.main;
        if (cameraToUse == null || holdArea == null || !holdArea.enabled)
            return false;

        Vector3 worldPosition = cameraToUse.ScreenToWorldPoint(screenPosition);
        return holdArea.OverlapPoint(new Vector2(worldPosition.x, worldPosition.y));
    }

    private void SetAreaEnabled(bool enabled)
    {
        if (holdArea != null)
            holdArea.enabled = enabled;
    }

    #endregion
}

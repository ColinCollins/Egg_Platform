using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level107 卡死齿轮：每次点击让整条杠杆平台轻微转动后复位，但不抛射角色。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level107JammedGear : MonoBehaviour
{
    private const string GearRotateSfxTag = "l93GearRotate";

    #region Inspector 配置

    /// <summary>
    /// 需要表现卡住效果的整条杠杆平台；点击命中仍由齿轮 Collider 提供。
    /// </summary>
    [SerializeField] private Transform jammedPlatform;

    /// <summary>
    /// 齿轮点击命中区域；为空时读取当前物体上的 Collider2D。
    /// </summary>
    [SerializeField] private Collider2D gearHitArea;

    /// <summary>
    /// 每次点击尝试转动的角度，完成后必定回到本次点击前角度。
    /// </summary>
    [SerializeField, Min(0f)] private float jamRotationDegrees = 5f;

    /// <summary>
    /// 齿轮转向卡点所需时间，单位为秒。
    /// </summary>
    [SerializeField, Min(0.01f)] private float jamRotationDuration = 0.1f;

    /// <summary>
    /// 齿轮从卡点复原所需时间，单位为秒。
    /// </summary>
    [SerializeField, Min(0.01f)] private float restoreDuration = 0.16f;

    #endregion

    #region 运行时状态

    private Camera inputCamera;
    private Sequence jamSequence;
    private EventSubscriber eventSubscriber;
    private Vector3 restingEulerAngles;
    private bool isGameplayActive = true;
    private bool isAnimating;
    private bool hasLoggedMissingReferences;

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        ResolveReferences();
        restingEulerAngles = jammedPlatform != null ? jammedPlatform.localEulerAngles : Vector3.zero;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        eventSubscriber.Subscribe<GamePauseEvent>(OnGamePause);
        eventSubscriber.Subscribe<GameResumeEvent>(OnGameResume);
        SetGameplayActive(PlayCtrl.Instance != null && PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING));
    }

    private void Update()
    {
        if (!isGameplayActive
            || isAnimating
            || Time.timeScale <= 0f
            || !ResolveReferences())
        {
            return;
        }

        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (touch.phase == TouchPhase.Began && TryHandleClick(touch.position))
            {
                return;
            }
        }

        if (Input.touchCount == 0 && Input.GetMouseButtonDown(0))
        {
            TryHandleClick(Input.mousePosition);
        }
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        CleanupAnimation();
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        CleanupAnimation();
    }

    #endregion

    #region 输入与表现

    private bool TryHandleClick(Vector2 screenPosition)
    {
        if (!TryGetWorldPoint(screenPosition, out Vector2 worldPoint)
            || gearHitArea == null
            || !gearHitArea.OverlapPoint(worldPoint))
        {
            return false;
        }

        restingEulerAngles = jammedPlatform.localEulerAngles;
        Vector3 jammedEulerAngles = restingEulerAngles;
        jammedEulerAngles.z -= Mathf.Abs(jamRotationDegrees);

        isAnimating = true;
        AudioManager.PlaySound(GearRotateSfxTag);
        jamSequence?.Kill();
        jamSequence = DOTween.Sequence()
            .SetLink(gameObject)
            .Append(jammedPlatform.DOLocalRotate(
                jammedEulerAngles,
                Mathf.Max(0.01f, jamRotationDuration),
                RotateMode.Fast).SetEase(Ease.OutQuad))
            .Append(jammedPlatform.DOLocalRotate(
                restingEulerAngles,
                Mathf.Max(0.01f, restoreDuration),
                RotateMode.Fast).SetEase(Ease.OutBack))
            .OnComplete(CompleteAnimation);
        return true;
    }

    private void CompleteAnimation()
    {
        if (jammedPlatform != null)
        {
            jammedPlatform.localEulerAngles = restingEulerAngles;
        }

        jamSequence = null;
        isAnimating = false;
    }

    #endregion

    #region 状态、引用与清理

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        SetGameplayActive(evt.NewState.Equals(GamePlayStateName.PLAYING));
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        SetGameplayActive(false);
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        SetGameplayActive(true);
    }

    private void SetGameplayActive(bool isActive)
    {
        isGameplayActive = isActive;
        if (isGameplayActive)
        {
            jamSequence?.Play();
        }
        else
        {
            jamSequence?.Pause();
        }
    }

    private bool ResolveReferences()
    {
        gearHitArea ??= GetComponent<Collider2D>();
        if (inputCamera == null)
        {
            inputCamera = Camera.main;
            if (inputCamera == null)
            {
                inputCamera = FindFirstObjectByType<Camera>();
            }
        }

        bool isValid = jammedPlatform != null && gearHitArea != null && inputCamera != null;
        if (!isValid && Application.isPlaying && !hasLoggedMissingReferences)
        {
            hasLoggedMissingReferences = true;
            Debug.LogWarning("[Level107JammedGear] 缺少杠杆平台、齿轮点击区域或输入相机，卡死表现保持关闭。", this);
        }

        return isValid;
    }

    private bool TryGetWorldPoint(Vector2 screenPosition, out Vector2 worldPoint)
    {
        if (inputCamera == null)
        {
            worldPoint = default;
            return false;
        }

        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        Plane clickPlane = new Plane(Vector3.forward, transform.position);
        if (!clickPlane.Raycast(ray, out float distance))
        {
            worldPoint = default;
            return false;
        }

        worldPoint = ray.GetPoint(distance);
        return true;
    }

    private void CleanupAnimation()
    {
        jamSequence?.Kill();
        jamSequence = null;
        isAnimating = false;
        if (jammedPlatform != null)
        {
            jammedPlatform.localEulerAngles = restingEulerAngles;
        }
    }

    #endregion
}

using System.Collections;
using Game.Scripts.Common;
using Game.Turntable;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System;
using Bear.Logger;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Lofelt.NiceVibrations;

public class Level58Ctrl : MonoBehaviour, IDebuger
{
    [Header("Fake Door - Button1")]
    [SerializeField] private Level51PushButton button1;
    [SerializeField] private Transform fakeDoor;
    [SerializeField] private Vector3 fakeDoorTargetPos;
    [SerializeField] private float fakeDoorDownDuration = 1f;
    [SerializeField] private float fakeDoorUpDuration = 0.2f;
    [SerializeField] private AnimationCurve fakeDoorDownCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve fakeDoorUpCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private float ropeUpperReturnDuration = 0.1f;
    [SerializeField] private Ease ropeUpperReturnEase = Ease.OutQuad;

    [Header("碰撞体设置")]
    [SerializeField] private Vector2 colliderSize = new Vector2(1f, 1f);
    [SerializeField] private float colliderZPosition = 0f;
    [SerializeField] private Camera sceneCamera;

    [Header("自动退出设置")]
    [SerializeField] private float autoExitDelay = 2f;

    /// <summary>
    /// lineRenderer 的 width 根据 startPoint 和 endPoint 距离逐渐 lerp 从 lineWidthRange.x -> lineWidthRange.y
    /// 在超过最 lineMaxDistance 后 door 不能超出 distance。hold 超过 duration 时间后触发 line break 函数
    /// door 有 DragStart，和 DragEnd
    /// </summary>
    [Header("Line Renderer")]
    [SerializeField] private Transform ropeUpper;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Vector2 lineWidthRange = new Vector2(0.1f, 0.5f);
    [SerializeField] private float lineMaxDistance = 5f;
    [SerializeField] private float lineDistanceTolerance = 0.01f;
    [SerializeField] private float lineHoldDuration = 1f;
    [SerializeField] private float lineBreakEndPointOffsetY = 0.2f;
    [SerializeField] private float lineBreakEndPointReturnDuration = 0.1f;
    [SerializeField] private Ease lineBreakEndPointReturnEase = Ease.OutQuad;
    [SerializeField] private UnityEvent lineBreakEvent;
    [SerializeField] private ParticleSystem breakPartical;


    [Header("Rigidbody")]
    [SerializeField] private Rigidbody2D doorRigid;
    [SerializeField] private FollowTarget doorFollower;
    [SerializeField] private FollowTarget doorColliderFollower;


    private Vector3 _fakeDoorOriginPos;
    private float _fakeDoorMoveStartY;
    private float _fakeDoorMoveTargetY;
    private float _lineUpperMoveStartY;
    private Vector3 _initialRopeUpperPosition;
    private float _fakeDoorMoveDuration;
    private float _fakeDoorMoveElapsed;
    private bool _isFakeDoorMoving;
    private bool _isFakeDoorMovingDown;
    private AnimationCurve _currentFakeDoorMoveCurve;
    private MelenitasDev.SoundsGood.Sound _findItemsSound;
    private MelenitasDev.SoundsGood.Sound _ropeStretchSound;
    private Level58TrackLine _trackLine;
    private Transform _lineStartPoint;
    private Transform _lineEndPoint;
    private DragableItem _fakeDoorDrag;
    private float _lineOverDistanceElapsed;
    private bool _isDoorDragging;
    private bool _wasDoorDragging;
    private bool _isLineBroken;
    private bool _hasInitialRopeUpperPosition;
    private Tweener _fakeDoorReturnTweener;
    private Tweener _ropeUpperReturnTweener;
    private Tweener _lineBreakEndPointTweener;

    private Coroutine _autoExitCoroutine;
    private Collider2D _lastTriggerCollider;
    private bool _isAutoExiting;
    private List<GameObject> worldColliders;
    private List<RectTransform> levelBlockRects;
    private Canvas canvas;
    private Camera uiCamera;
    private Camera worldCamera;
    private bool isInitialized = false;
    private UIDrag _targetBlockDrag;
    // Cache the project-wide physics step so the level can restore it on teardown.
    private float _originalFixedDeltaTime;
    private bool _hasOverriddenFixedDeltaTime;

    private void OnEnable()
    {
        if (!isInitialized)
        {
            Invoke(nameof(InitializeColliders), 0.01f);
        }
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(InitializeColliders));
    }

    private void Start()
    {
        if (fakeDoor != null)
        {
            _fakeDoorOriginPos = fakeDoor.position;
            _fakeDoorDrag = fakeDoor.GetComponentInParent<DragableItem>() ?? fakeDoor.GetComponentInChildren<DragableItem>();
        }

        InitializeLineRenderer();
        CaptureInitialRopeUpperPosition();

        if (button1 != null)
        {
            // The button controls both the fake-door motion and the delayed auto-release flow.
            button1.SetEnter(() => MoveFakeDoor(true));
            button1.SetExit(OnFakeDoorButtonExit);
            button1.SetEnter(OnButtonEnter);
            button1.SetExit(OnButtonExit);
        }
    }

    private void Update()
    {
        UpdateFakeDoorMovement();
        UpdateColliderPositions();
    }

    private void LateUpdate()
    {
        // Update the rope after transforms have been moved for this frame.
        UpdateLineRenderer();
    }

    private void OnDestroy()
    {
        _fakeDoorReturnTweener?.Kill();
        _fakeDoorReturnTweener = null;

        _ropeUpperReturnTweener?.Kill();
        _ropeUpperReturnTweener = null;

        _lineBreakEndPointTweener?.Kill();
        _lineBreakEndPointTweener = null;

        _findItemsSound?.Stop();
        _findItemsSound = null;

        _ropeStretchSound?.Stop();
        _ropeStretchSound = null;

        if (_autoExitCoroutine != null)
        {
            StopCoroutine(_autoExitCoroutine);
            _autoExitCoroutine = null;
        }

        if (_targetBlockDrag != null)
        {
            _targetBlockDrag.OnDragEnd -= OnTargetBlockDragEnd;
        }

        UnbindFakeDoorButton();

        if (worldColliders != null)
        {
            foreach (GameObject obj in worldColliders)
            {
                if (obj != null)
                    Destroy(obj);
            }
            worldColliders.Clear();
        }

        if (_hasOverriddenFixedDeltaTime)
        {
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            _hasOverriddenFixedDeltaTime = false;
        }
    }

    private void MoveFakeDoor(bool down)
    {
        if (fakeDoor == null || _isLineBroken)
            return;

        if (down)
        {
            StartFakeDoorDownMovement();
            _findItemsSound ??= AudioManager.PlaySound("findItems", loop: true);
        }
        else
        {
            HandleFakeDoorRelease();
        }
    }

    private void OnFakeDoorButtonExit()
    {
        if (_isLineBroken)
            return;

        StopFakeDoorDownMovement();
        if (IsDoorDragging())
            return;

        ReturnFakeDoorToInitialPosition();
    }

    private void UnbindFakeDoorButton()
    {
        if (button1 == null)
            return;

        button1.SetEnter((Action)null);
        button1.SetExit((Action)null);
        button1.SetEnter((Action<Collider2D>)null);
        button1.SetExit((Action<Collider2D>)null);
    }

    private void HandleFakeDoorRelease()
    {
        if (_isLineBroken)
        {
            StopFakeDoorDownMovement();
            return;
        }

        StopFakeDoorDownMovement();
        ReturnFakeDoorToInitialPosition();
    }

    private void StartFakeDoorDownMovement()
    {
        if (_isLineBroken || fakeDoor == null)
            return;

        _fakeDoorReturnTweener?.Kill();
        _fakeDoorReturnTweener = null;

        _ropeUpperReturnTweener?.Kill();
        _ropeUpperReturnTweener = null;

        float targetY = fakeDoorTargetPos.y;
        _isFakeDoorMovingDown = true;
        _fakeDoorMoveStartY = fakeDoor.position.y;
        _fakeDoorMoveTargetY = targetY;
        if (ropeUpper != null)
            _lineUpperMoveStartY = ropeUpper.position.y;

        if (Mathf.Abs(_fakeDoorMoveStartY - targetY) <= Mathf.Epsilon || fakeDoorDownDuration <= 0f)
        {
            Vector3 position = fakeDoor.position;
            position.y = targetY;
            fakeDoor.position = position;
            UpdateLineUpperPosition(1f);
            _isFakeDoorMoving = false;
            _isFakeDoorMovingDown = false;
            return;
        }

        _fakeDoorMoveDuration = fakeDoorDownDuration;
        _fakeDoorMoveElapsed = 0f;
        _currentFakeDoorMoveCurve = fakeDoorDownCurve;
        _isFakeDoorMoving = true;
    }

    private void ReturnFakeDoorToInitialPosition()
    {
        if (_isLineBroken || fakeDoor == null)
            return;

        StopFakeDoorDownMovement();

        _fakeDoorReturnTweener?.Kill();
        _fakeDoorReturnTweener = null;

        ReturnRopeUpperToInitialPosition();

        float duration = Mathf.Max(0f, fakeDoorUpDuration);
        if (duration <= 0f)
        {
            fakeDoor.position = _fakeDoorOriginPos;
            return;
        }

        _fakeDoorReturnTweener = fakeDoor
            .DOMove(_fakeDoorOriginPos, duration)
            .SetEase(fakeDoorUpCurve)
            .OnComplete(() => _fakeDoorReturnTweener = null);
    }

    private void StopFakeDoorDownMovement()
    {
        _isFakeDoorMoving = false;
        _isFakeDoorMovingDown = false;
        StopFindItemsSound();
    }

    private void UpdateFakeDoorMovement()
    {
        if (!_isFakeDoorMoving || fakeDoor == null || _isLineBroken)
            return;

        _fakeDoorMoveElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_fakeDoorMoveElapsed / _fakeDoorMoveDuration);
        float curveProgress = _currentFakeDoorMoveCurve != null ? _currentFakeDoorMoveCurve.Evaluate(progress) : progress;

        Vector3 position = fakeDoor.position;
        position.y = Mathf.LerpUnclamped(_fakeDoorMoveStartY, _fakeDoorMoveTargetY, curveProgress);
        fakeDoor.position = position;
        UpdateLineUpperPosition(curveProgress);

        if (progress >= 1f)
        {
            position.y = _fakeDoorMoveTargetY;
            fakeDoor.position = position;
            _isFakeDoorMoving = false;
            _isFakeDoorMovingDown = false;
        }
    }

    private void UpdateLineUpperPosition(float progress)
    {
        if (!_isFakeDoorMovingDown || ropeUpper == null)
            return;

        Vector3 position = ropeUpper.position;
        position.y = _lineUpperMoveStartY + (_fakeDoorMoveTargetY - _fakeDoorMoveStartY) * progress;
        ropeUpper.position = position;
    }

    private void CaptureInitialRopeUpperPosition()
    {
        if (ropeUpper == null)
            return;

        _initialRopeUpperPosition = ropeUpper.position;
        _hasInitialRopeUpperPosition = true;
    }

    private void ReturnRopeUpperToInitialPosition()
    {
        if (ropeUpper == null || !_hasInitialRopeUpperPosition)
            return;

        _ropeUpperReturnTweener?.Kill();

        float duration = Mathf.Max(0f, ropeUpperReturnDuration);
        if (duration <= 0f)
        {
            ropeUpper.position = _initialRopeUpperPosition;
            return;
        }

        _ropeUpperReturnTweener = ropeUpper
            .DOMove(_initialRopeUpperPosition, duration)
            .SetEase(ropeUpperReturnEase);
    }

    private void InitializeLineRenderer()
    {
        if (lineRenderer == null)
        {
            _trackLine = GetComponentInChildren<Level58TrackLine>();
            if (_trackLine != null)
                lineRenderer = _trackLine.GetComponent<LineRenderer>();
        }

        if (lineRenderer == null)
            return;

        if (_trackLine == null)
            _trackLine = lineRenderer.GetComponent<Level58TrackLine>();

        if (_trackLine != null)
        {
            _lineStartPoint = _trackLine.GetStartPoint();
            _lineEndPoint = _trackLine.GetEndPoint();
            if (ropeUpper == null && _lineStartPoint != null)
                ropeUpper = _lineStartPoint.parent;
        }

        lineRenderer.positionCount = 2;
        lineRenderer.enabled = true;
        UpdateLineWidth(0f);
    }

    private void UpdateLineRenderer()
    {
        if (_isLineBroken)
        {
            LogLineRendererStep("skip_line_broken");
            StopRopeStretchSound();
            return;
        }

        if (lineRenderer == null || _lineStartPoint == null || _lineEndPoint == null)
            InitializeLineRenderer();

        if (lineRenderer == null || _lineStartPoint == null || _lineEndPoint == null)
        {
            LogLineRendererStep("skip_missing_refs");
            StopRopeStretchSound();
            return;
        }

        bool doorDragging = UpdateDoorDragState();
        float distance = Vector3.Distance(_lineStartPoint.position, _lineEndPoint.position);
        float normalizedDistance = lineMaxDistance > 0f ? Mathf.Clamp01(distance / lineMaxDistance) : 0f;
        LogLineRendererStep("measure_distance", distance, normalizedDistance, doorDragging);
        UpdateLineWidth(normalizedDistance);

        if (lineMaxDistance <= 0f || distance <= lineMaxDistance + lineDistanceTolerance)
        {
            LogLineRendererStep("under_max_reset_elapsed", distance, normalizedDistance, doorDragging);
            _lineOverDistanceElapsed = 0f;
            StopRopeStretchSound();
            return;
        }

        if (!doorDragging && !_isFakeDoorMovingDown)
        {
            LogLineRendererStep("at_limit_but_idle", distance, normalizedDistance, doorDragging);
            StopRopeStretchSound();
            return;
        }

        // Clamp the rope length first, then count how long the player keeps pulling at max range.
        LogLineRendererStep("clamp_to_max_distance", distance, normalizedDistance, doorDragging);
        ClampFakeDoorToLineMaxDistance();
        if (!doorDragging)
        {
            LogLineRendererStep("clamped_but_drag_stopped_reset_elapsed", distance, normalizedDistance, doorDragging);
            _lineOverDistanceElapsed = 0f;
            StopRopeStretchSound();
            return;
        }

        _lineOverDistanceElapsed += Time.deltaTime;
        LogLineRendererStep("hold_accumulating", distance, normalizedDistance, doorDragging);
        _ropeStretchSound ??= AudioManager.PlaySound("ropeStretch", loop: true);

        if (_lineOverDistanceElapsed >= lineHoldDuration)
        {
            LogLineRendererStep("line_break_trigger", distance, normalizedDistance, doorDragging);
            LineBreak().Forget(ex => this.LogError($"[Level58Ctrl] LineBreak failed: {ex}"));
        }
    }

    private void LogLineRendererStep(string step, float distance = -1f, float normalizedDistance = -1f, bool? doorDragging = null)
    {
        if (true)
            return;

        this.Log(
            $"[Level58Ctrl] UpdateLineRenderer step={step}, " +
            $"distance={distance:F3}, normalizedDistance={normalizedDistance:F3}, " +
            $"lineMaxDistance={lineMaxDistance:F3}, lineDistanceTolerance={lineDistanceTolerance:F3}, lineOverDistanceElapsed={_lineOverDistanceElapsed:F3}, " +
            $"lineHoldDuration={lineHoldDuration:F3}, doorDragging={(doorDragging.HasValue ? doorDragging.Value.ToString() : "n/a")}, " +
            $"isFakeDoorMovingDown={_isFakeDoorMovingDown}, isLineBroken={_isLineBroken}");
    }

    private void UpdateLineWidth(float normalizedDistance)
    {
        if (lineRenderer == null)
            return;

        float width = Mathf.Lerp(lineWidthRange.x, lineWidthRange.y, normalizedDistance);
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
    }

    private void ClampFakeDoorToLineMaxDistance()
    {
        if (fakeDoor == null || _lineStartPoint == null || _lineEndPoint == null || lineMaxDistance <= 0f)
            return;

        Vector3 start = _lineStartPoint.position;
        Vector3 end = _lineEndPoint.position;
        Vector3 direction = end - start;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Vector3 clampedEnd = start + direction.normalized * lineMaxDistance;
        Vector3 fakeDoorOffset = fakeDoor.position - end;
        // Keep the door/end-point relative offset so only the rope length is constrained.
        fakeDoor.position = clampedEnd + fakeDoorOffset;
    }

    private bool IsDoorDragging()
    {
        if (_fakeDoorDrag != null)
            return _fakeDoorDrag.IsDragging();

        return _isDoorDragging;
    }

    private bool UpdateDoorDragState()
    {
        bool isDragging = IsDoorDragging();
        if (isDragging && !_wasDoorDragging)
        {
            _isDoorDragging = true;
            _lineOverDistanceElapsed = 0f;
        }
        else if (!isDragging && _wasDoorDragging)
        {
            _isDoorDragging = false;
            _lineOverDistanceElapsed = 0f;
            HandleFakeDoorRelease();
        }

        _wasDoorDragging = isDragging;
        return isDragging;
    }

    public void OnDragStart()
    {
        _lineBreakEndPointTweener?.Kill();
        _lineBreakEndPointTweener = null;

        _fakeDoorReturnTweener?.Kill();
        _fakeDoorReturnTweener = null;

        _isDoorDragging = true;
        _wasDoorDragging = true;
        _lineOverDistanceElapsed = 0f;
    }

    public void OnDragStart(Transform target)
    {
        OnDragStart();
    }

    public void OnDragEnd()
    {
        _isDoorDragging = false;
        _wasDoorDragging = false;
        _lineOverDistanceElapsed = 0f;

        if (_isLineBroken)
            return;

        HandleFakeDoorRelease();
    }

    public void OnDragEnd(Transform target)
    {
        OnDragEnd();
    }

    public async UniTask LineBreak()
    {
        if (_isLineBroken)
            return;

        _isLineBroken = true;
        UnbindFakeDoorButton();
        _lineOverDistanceElapsed = 0f;
        _isFakeDoorMoving = false;
        _isFakeDoorMovingDown = false;
        _fakeDoorReturnTweener?.Kill();
        _fakeDoorReturnTweener = null;

        if (lineRenderer != null)
            lineRenderer.enabled = true;

        ReturnEndPointToBreakPosition();

        StopFindItemsSound();
        StopRopeStretchSound();
        AudioManager.PlaySound("ropeBroke");
        HapticPatterns.PlayPreset(HapticPatterns.PresetType.HeavyImpact);

        PlayBreakPartical();
        lineBreakEvent?.Invoke();

        await UniTask.WaitForSeconds(0.1f);

        var dragItem = fakeDoor.GetComponentInParent<DragableItem>()
                      ?? fakeDoor.GetComponentInChildren<DragableItem>();
        if (dragItem != null)
            Destroy(dragItem);

        SwitchFollower(true);
    }

    private void ReturnEndPointToBreakPosition()
    {
        if (_lineStartPoint == null || _lineEndPoint == null)
            InitializeLineRenderer();

        if (_lineStartPoint == null || _lineEndPoint == null)
            return;

        _lineBreakEndPointTweener?.Kill();

        Vector3 targetPosition = _lineStartPoint.position + Vector3.up * lineBreakEndPointOffsetY;
        float duration = Mathf.Max(0f, lineBreakEndPointReturnDuration);
        if (duration <= 0f)
        {
            this.Log("EndPoint back");
            _lineEndPoint.position = targetPosition;
            RefreshBrokenLineRenderer();
            return;
        }

        _lineBreakEndPointTweener = _lineEndPoint
            .DOMove(targetPosition, duration)
            .SetEase(lineBreakEndPointReturnEase)
            .OnUpdate(RefreshBrokenLineRenderer)
            .OnComplete(() =>
            {
                RefreshBrokenLineRenderer();
                if (_trackLine != null)
                {
                    Destroy(_trackLine);
                    _trackLine = null;
                }
            });
    }

    private void RefreshBrokenLineRenderer()
    {
        if (lineRenderer == null || _lineStartPoint == null || _lineEndPoint == null)
            return;

        if (lineRenderer.positionCount != 2)
            lineRenderer.positionCount = 2;

        lineRenderer.SetPosition(0, _lineStartPoint.position);
        lineRenderer.SetPosition(1, _lineEndPoint.position);

        float distance = Vector3.Distance(_lineStartPoint.position, _lineEndPoint.position);
        float normalizedDistance = lineMaxDistance > 0f ? Mathf.Clamp01(distance / lineMaxDistance) : 0f;
        UpdateLineWidth(normalizedDistance);
    }

    private void PlayBreakPartical()
    {
        if (breakPartical == null)
            return;

        if (_lineEndPoint == null)
            InitializeLineRenderer();

        if (_lineEndPoint != null)
            breakPartical.transform.position = _lineEndPoint.position;

        breakPartical.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        breakPartical.Play();
    }

    private void StopFindItemsSound()
    {
        _findItemsSound?.Stop();
        _findItemsSound = null;
    }

    private void StopRopeStretchSound()
    {
        _ropeStretchSound?.Stop();
        _ropeStretchSound = null;
    }

    private void OnTargetBlockDragEnd()
    {
        if (_targetBlockDrag != null)
        {
            _targetBlockDrag.enabled = false;
        }

        if (worldColliders != null && worldColliders.Count > 0 && worldColliders[0] != null)
        {
            if (worldColliders[0].GetComponent<Rigidbody2D>() == null)
            {
                var rb = worldColliders[0].AddComponent<Rigidbody2D>();
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                rb.gravityScale = 3;
            }
        }
    }

    private void OnButtonEnter(Collider2D other)
    {
        if (other != null && other.CompareTag("Player"))
            return;

        _lastTriggerCollider = other;

        if (_autoExitCoroutine != null)
            StopCoroutine(_autoExitCoroutine);
        _autoExitCoroutine = StartCoroutine(AutoExitAfterDelay());
    }

    private void OnButtonExit(Collider2D other)
    {
        if (other != null && other.CompareTag("Player"))
            return;

        if (_autoExitCoroutine != null)
        {
            StopCoroutine(_autoExitCoroutine);
            _autoExitCoroutine = null;
        }

        if (!_isAutoExiting)
            return;

        if (other != null && other.CompareTag("tools"))
        {
            Rigidbody2D rb = other.attachedRigidbody;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.AddForce(new Vector2(.5f, 1.5f).normalized * 10f, ForceMode2D.Impulse);
            }
        }
    }

    private IEnumerator AutoExitAfterDelay()
    {
        yield return new WaitForSeconds(autoExitDelay);
        _autoExitCoroutine = null;
        _isAutoExiting = true;
        // Reuse the button's normal exit path so pressed state and callbacks stay in sync.
        button1?.ManuallyExit(_lastTriggerCollider);
        _isAutoExiting = false;
        _lastTriggerCollider = null;
    }

    #region Collision
    private void InitializeColliders()
    {
        if (isInitialized)
            return;

        worldColliders = new List<GameObject>();
        levelBlockRects = new List<RectTransform>();

        if (sceneCamera == null)
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
                worldCamera = FindFirstObjectByType<Camera>();
        }
        else
        {
            worldCamera = sceneCamera;
        }

        canvas = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[Level58Ctrl] Canvas not found");
            return;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
            uiCamera = canvas.worldCamera;
        else if (canvas.renderMode == RenderMode.WorldSpace)
            uiCamera = canvas.worldCamera;
        else
            uiCamera = null;

        Transform panelRoot = PlayCtrl.Instance.CurrentGamePlayPanel.transform;
        FindLevelBlocks(panelRoot);

        if (levelBlockRects.Count == 0)
        {
            Debug.LogWarning("[Level58Ctrl] No levelBlock found");
            return;
        }

        for (int i = 0; i < levelBlockRects.Count; i++)
        {
            CreateColliderForLevelBlock(levelBlockRects[i], i);
        }

        isInitialized = true;

        if (!_hasOverriddenFixedDeltaTime)
        {
            _originalFixedDeltaTime = Time.fixedDeltaTime;
            _hasOverriddenFixedDeltaTime = true;
        }

        // The UI-to-world proxy blocks need a tighter physics tick after they are spawned.
        Time.fixedDeltaTime = 1f / 120f;

        // 延迟一帧等 Canvas 布局完成后再对齐 button1
        Invoke(nameof(AlignButton1ToLevelBlock), 0.01f);
    }

    private void FindLevelBlocks(Transform parent)
    {
        if (parent.name == "levelBlock")
        {
            RectTransform rect = parent as RectTransform;
            if (rect != null)
                levelBlockRects.Add(rect);
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            FindLevelBlocks(parent.GetChild(i));
        }
    }

    private void CreateColliderForLevelBlock(RectTransform levelBlockRect, int index)
    {
        GameObject colliderObj = new GameObject($"LevelBlockCollider_{index}_{levelBlockRect.name}");
        colliderObj.transform.SetParent(transform);

        BoxCollider2D boxCollider = colliderObj.AddComponent<BoxCollider2D>();
        boxCollider.size = colliderSize;
        colliderObj.tag = "tools";
        colliderObj.layer = LayerMask.NameToLayer("Stuff");
        colliderObj.transform.position = new Vector3(0, 0, colliderZPosition);

        worldColliders.Add(colliderObj);
    }

    private void UpdateColliderPositions()
    {
        if (canvas == null || worldCamera == null)
            return;
        if (worldColliders == null || levelBlockRects == null || levelBlockRects.Count != worldColliders.Count)
            return;

        for (int i = 0; i < levelBlockRects.Count && i < worldColliders.Count; i++)
        {
            if (levelBlockRects[i] == null || worldColliders[i] == null)
                continue;

            // Before the target block is detached, UI drives the world proxy.
            // After detachment, the world proxy drives the UI block back.
            if (i == 0 && _targetBlockDrag != null && !_targetBlockDrag.enabled)
            {
                // targetBlock 拖拽结束后：collider 世界坐标 → 投影到 Canvas → UI 坐标
                ApplyWorldPositionToBlock(levelBlockRects[i], worldColliders[i].transform.position);
            }
            else
            {
                Vector3 worldPosition = ConvertUIToWorldPosition(levelBlockRects[i]);
                worldColliders[i].transform.position = worldPosition;
            }
        }
    }

    private void ApplyWorldPositionToBlock(RectTransform blockRect, Vector3 worldPosition)
    {
        if (canvas == null || blockRect == null)
            return;

        if (canvas.renderMode == RenderMode.WorldSpace)
        {
            Vector3 pos = blockRect.position;
            pos.x = worldPosition.x;
            pos.y = worldPosition.y;
            blockRect.position = pos;
            return;
        }

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : (uiCamera != null ? uiCamera : worldCamera);

        // Convert a simulated world position back into the canvas space used by the draggable UI block.
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, eventCamera, out Vector2 canvasLocalPoint))
            return;

        Vector3 canvasWorldPoint = canvasRect.TransformPoint(canvasLocalPoint);
        RectTransform parentRect = blockRect.parent as RectTransform;
        if (parentRect == null)
        {
            blockRect.position = new Vector3(canvasWorldPoint.x, canvasWorldPoint.y, blockRect.position.z);
            return;
        }

        Vector2 parentLocalPoint = parentRect.InverseTransformPoint(canvasWorldPoint);
        if (parentRect == canvasRect)
        {
            blockRect.anchoredPosition = canvasLocalPoint;
        }
        else
        {
            blockRect.localPosition = new Vector3(parentLocalPoint.x, parentLocalPoint.y, blockRect.localPosition.z);
        }
    }

    private Vector3 ConvertUIToWorldPosition(RectTransform rect)
    {
        return ConvertUIToWorldPosition(rect, colliderZPosition);
    }

    private Vector3 ConvertUIToWorldPosition(RectTransform rect, float targetWorldZ)
    {
        if (canvas == null || worldCamera == null)
            return Vector3.zero;

        // WorldSpace Canvas: 直接用 rect 的世界坐标，仅替换 z
        if (canvas.renderMode == RenderMode.WorldSpace)
            return new Vector3(rect.position.x, rect.position.y, targetWorldZ);

        // 先获取屏幕坐标
        Vector2 screenPoint;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            screenPoint = RectTransformUtility.WorldToScreenPoint(null, rect.position);
        else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && uiCamera != null)
            screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.position);
        else
            screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera != null ? uiCamera : worldCamera, rect.position);

        // 计算从相机到目标 z 平面的距离（带符号，沿相机朝向方向）
        float zDistance;
        float camZ = worldCamera.transform.position.z;
        float camForwardZ = worldCamera.transform.forward.z;

        if (Mathf.Abs(camForwardZ) < 0.001f)
        {
            zDistance = Mathf.Abs(camZ - targetWorldZ);
        }
        else
        {
            zDistance = (targetWorldZ - camZ) / camForwardZ;
            if (zDistance <= 0)
            {
                Debug.LogWarning($"[Level58Ctrl] zDistance <= 0 (camZ={camZ}, forwardZ={camForwardZ}, targetZ={targetWorldZ}), fallback to abs diff");
                zDistance = Mathf.Abs(camZ - targetWorldZ);
            }
        }

        Vector3 worldPos = worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, zDistance));
        worldPos.z = targetWorldZ;
        return worldPos;
    }

    private void AlignButton1ToLevelBlock()
    {
        if (button1 == null || levelBlockRects == null || levelBlockRects.Count == 0)
            return;

        RectTransform targetBlock = levelBlockRects[0];
        if (targetBlock == null)
            return;

        Transform targetTransform = button1.transform.parent != null ? button1.transform.parent : button1.transform;
        float targetZ = targetTransform.position.z;
        Vector3 targetWorldPos = ConvertUIToWorldPosition(targetBlock, targetZ);

        Vector3 pos = targetTransform.position;
        pos.x = targetWorldPos.x;
        targetTransform.position = pos;

        _targetBlockDrag = targetBlock.GetComponent<UIDrag>();
        if (_targetBlockDrag != null)
        {
            _targetBlockDrag.OnDragEnd -= OnTargetBlockDragEnd;
            _targetBlockDrag.OnDragEnd += OnTargetBlockDragEnd;
        }
    }
    #endregion

    private void SwitchFollower(bool isBreak = false)
    {
        if (doorColliderFollower != null)
            doorColliderFollower.enabled = !isBreak;
        if (doorRigid != null)
            doorRigid.simulated = isBreak;
        if (doorFollower != null)
            doorFollower.enabled = isBreak;
    }
}

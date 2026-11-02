using System;
using System.Collections.Generic;
using Game.Scripts.Common;
using UnityEngine;


/// <summary>
/// 乌鸦拉屎
/// </summary>
public class Level57Ctrl : MonoBehaviour
{
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private Vector2 colliderSize = new Vector2(1f, 1f);
    [SerializeField] private float colliderZPosition = 0f;

    [Header("Water")]
    [SerializeField] private Transform water;
    [SerializeField] private float waterRiseAmount = 0.1f;
    [SerializeField] private float maxOffsetY = 1f;

    private Vector3 _waterOriginPos;
    private float _currentWaterOffsetY;
    private CrowCtrl _crowCtrl;
    private List<GameObject> _worldColliders;
    private List<RectTransform> _buttonRects;
    private List<UIDrag> _buttonDrags;
    private List<bool> _buttonSyncEnabled;
    private List<Action> _dragBeginHandlers;
    private List<Action> _dragEndHandlers;
    private Canvas _canvas;
    private RectTransform _canvasRectTransform;
    private Camera _uiCamera;
    private Camera _worldCamera;
    private bool _isButtonFollowInitialized;
    private bool _ownsWorldColliders;
    private HashSet<Collider2D> _enteredColliders = new HashSet<Collider2D>();


    void Start()
    {
        if (water != null)
            _waterOriginPos = water.position;

        Invoke(nameof(InitializeButtonFollow), 0.2f);
    }

    private void AddRigidbodiesToWorldColliders()
    {
        if (_worldColliders == null || _worldColliders.Count == 0)
            return;

        foreach (GameObject obj in _worldColliders)
        {
            if (obj != null && obj.GetComponent<Rigidbody2D>() == null)
            {
                var rigibody = obj.AddComponent<Rigidbody2D>();
                rigibody.constraints = RigidbodyConstraints2D.FreezeRotation;
                rigibody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
        }
    }

    private void LateUpdate()
    {
        UpdateButtonsFollowColliders();
    }

    private void InitializeButtonFollow()
    {
        if (_isButtonFollowInitialized)
            return;

        if (!TryInitializeWorldCamera())
        {
            Invoke(nameof(InitializeButtonFollow), 0.1f);
            return;
        }

        if (!TryCacheButtonsAndCanvas())
        {
            Invoke(nameof(InitializeButtonFollow), 0.1f);
            return;
        }

        if (!CreateWorldCollidersFromButtons())
        {
            Invoke(nameof(InitializeButtonFollow), 0.1f);
            return;
        }

        BindDragListeners();
        _isButtonFollowInitialized = true;
    }

    private bool TryInitializeWorldCamera()
    {
        if (_worldCamera != null)
            return true;

        if (sceneCamera != null)
        {
            _worldCamera = sceneCamera;
            return true;
        }

        _worldCamera = Camera.main;
        if (_worldCamera == null)
            _worldCamera = FindFirstObjectByType<Camera>();

        return _worldCamera != null;
    }

    private bool TryCacheButtonsAndCanvas()
    {
        if (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null)
            return false;

        if (_canvas == null)
        {
            _canvas = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponentInParent<Canvas>();
            if (_canvas == null)
                return false;

            _canvasRectTransform = _canvas.GetComponent<RectTransform>();
            if (_canvasRectTransform == null)
                return false;

            if (_canvas.renderMode == RenderMode.ScreenSpaceCamera && _canvas.worldCamera != null)
                _uiCamera = _canvas.worldCamera;
            else if (_canvas.renderMode == RenderMode.WorldSpace)
                _uiCamera = _canvas.worldCamera;
            else
                _uiCamera = null;
        }

        if (_buttonRects != null && _buttonRects.Count > 0)
            return true;

        GamePlayPanel_DragButtons dragButtons = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<GamePlayPanel_DragButtons>();
        if (dragButtons == null)
            return false;

        List<CustomButton> buttons = dragButtons.GetButtons();
        if (buttons == null || buttons.Count == 0)
            return false;

        _buttonRects = new List<RectTransform>();
        _buttonDrags = new List<UIDrag>();
        _buttonSyncEnabled = new List<bool>();
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] == null)
                continue;

            RectTransform buttonRect = buttons[i].GetComponent<RectTransform>();
            if (buttonRect == null)
                continue;

            UIDrag uidrag = buttons[i].GetComponent<UIDrag>();
            if (uidrag == null)
                uidrag = buttons[i].gameObject.AddComponent<UIDrag>();

            _buttonRects.Add(buttonRect);
            _buttonDrags.Add(uidrag);
            _buttonSyncEnabled.Add(true);
        }

        return _buttonRects.Count > 0;
    }

    private bool CreateWorldCollidersFromButtons()
    {
        if (_worldColliders != null && _worldColliders.Count > 0)
            return true;

        if (_buttonRects == null || _buttonRects.Count == 0)
            return false;

        _worldColliders = new List<GameObject>();
        _ownsWorldColliders = true;

        for (int i = 0; i < _buttonRects.Count; i++)
        {
            RectTransform buttonRect = _buttonRects[i];
            if (buttonRect == null)
                continue;

            GameObject colliderObj = new GameObject($"ButtonCollider_{i}_{buttonRect.name}");
            colliderObj.transform.SetParent(transform);
            colliderObj.tag = "tools";
            colliderObj.layer = LayerMask.NameToLayer("Ground");

            BoxCollider2D boxCollider = colliderObj.AddComponent<BoxCollider2D>();
            boxCollider.size = colliderSize;

            colliderObj.transform.position = ConvertUIToWorldPosition(buttonRect, colliderZPosition);
            _worldColliders.Add(colliderObj);
        }

        return _worldColliders.Count > 0;
    }

    private void BindDragListeners()
    {
        if (_buttonDrags == null || _worldColliders == null)
            return;

        if ((_dragBeginHandlers != null && _dragBeginHandlers.Count > 0)
            || (_dragEndHandlers != null && _dragEndHandlers.Count > 0))
            return;

        _dragBeginHandlers = new List<Action>();
        _dragEndHandlers = new List<Action>();
        int count = Mathf.Min(_buttonDrags.Count, _worldColliders.Count);
        for (int i = 0; i < count; i++)
        {
            UIDrag uidrag = _buttonDrags[i];
            if (uidrag == null)
            {
                _dragBeginHandlers.Add(null);
                _dragEndHandlers.Add(null);
                continue;
            }

            int index = i;
            Action beginHandler = () => SetButtonSyncEnabled(index, false);
            Action endHandler = () => OnButtonDragEnd(index);
            uidrag.OnDragBegin += beginHandler;
            uidrag.OnDragEnd += endHandler;
            _dragBeginHandlers.Add(beginHandler);
            _dragEndHandlers.Add(endHandler);
        }
    }

    private void SetButtonSyncEnabled(int index, bool enabled)
    {
        if (_buttonSyncEnabled == null || index < 0 || index >= _buttonSyncEnabled.Count)
            return;

        _buttonSyncEnabled[index] = enabled;
    }

    private void OnButtonDragEnd(int index)
    {
        SyncWorldColliderToButton(index);
        SetButtonSyncEnabled(index, true);
        EnsureRigidbodyOnWorldCollider(index);
    }

    private void SyncWorldColliderToButton(int index)
    {
        if (_buttonRects == null || _worldColliders == null)
            return;

        if (index < 0 || index >= _buttonRects.Count || index >= _worldColliders.Count)
            return;

        RectTransform buttonRect = _buttonRects[index];
        GameObject colliderObject = _worldColliders[index];
        if (buttonRect == null || colliderObject == null)
            return;

        colliderObject.transform.position = ConvertUIToWorldPosition(buttonRect, colliderZPosition);
    }

    private void EnsureRigidbodyOnWorldCollider(int index)
    {
        if (_worldColliders == null || index < 0 || index >= _worldColliders.Count)
            return;

        GameObject targetCollider = _worldColliders[index];
        if (targetCollider == null)
            return;

        Rigidbody2D rigibody = targetCollider.GetComponent<Rigidbody2D>();
        if (rigibody != null)
            return;

        rigibody = targetCollider.AddComponent<Rigidbody2D>();
        rigibody.constraints = RigidbodyConstraints2D.FreezeRotation;
        rigibody.mass = 10;
    }

    private void UpdateButtonsFollowColliders()
    {
        if (!_isButtonFollowInitialized || _canvas == null || _worldCamera == null)
            return;

        if (_buttonRects == null || _worldColliders == null)
            return;

        int count = Mathf.Min(_buttonRects.Count, _worldColliders.Count);
        for (int i = 0; i < count; i++)
        {
            if (_buttonSyncEnabled != null && i < _buttonSyncEnabled.Count && !_buttonSyncEnabled[i])
                continue;

            RectTransform buttonRect = _buttonRects[i];
            GameObject colliderObject = _worldColliders[i];
            if (buttonRect == null || colliderObject == null)
                continue;

            ApplyWorldPositionToButton(buttonRect, colliderObject.transform.position);
        }
    }

    private Vector3 ConvertUIToWorldPosition(RectTransform rect, float targetWorldZ)
    {
        if (_canvas == null || _worldCamera == null || rect == null)
            return Vector3.zero;

        if (_canvas.renderMode == RenderMode.WorldSpace)
            return new Vector3(rect.position.x, rect.position.y, targetWorldZ);

        Vector2 screenPoint;
        if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            screenPoint = RectTransformUtility.WorldToScreenPoint(null, rect.position);
        else if (_canvas.renderMode == RenderMode.ScreenSpaceCamera && _uiCamera != null)
            screenPoint = RectTransformUtility.WorldToScreenPoint(_uiCamera, rect.position);
        else
            screenPoint = RectTransformUtility.WorldToScreenPoint(_uiCamera ?? _worldCamera, rect.position);

        float camZ = _worldCamera.transform.position.z;
        float camForwardZ = _worldCamera.transform.forward.z;
        float zDistance;

        if (Mathf.Abs(camForwardZ) < 0.001f)
        {
            zDistance = Mathf.Abs(camZ - targetWorldZ);
        }
        else
        {
            zDistance = (targetWorldZ - camZ) / camForwardZ;
            if (zDistance <= 0f)
                zDistance = Mathf.Abs(camZ - targetWorldZ);
        }

        Vector3 worldPos = _worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, zDistance));
        worldPos.z = targetWorldZ;
        return worldPos;
    }

    private void ApplyWorldPositionToButton(RectTransform buttonRect, Vector3 worldPosition)
    {
        if (_canvas.renderMode == RenderMode.WorldSpace)
        {
            Vector3 uiWorldPos = buttonRect.position;
            uiWorldPos.x = worldPosition.x;
            uiWorldPos.y = worldPosition.y;
            buttonRect.position = uiWorldPos;
            return;
        }

        Vector3 screenPoint = _worldCamera.WorldToScreenPoint(worldPosition);
        Camera eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : (_uiCamera != null ? _uiCamera : _worldCamera);

        if (_canvasRectTransform == null)
            _canvasRectTransform = _canvas.GetComponent<RectTransform>();

        if (_canvasRectTransform == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRectTransform, screenPoint, eventCamera, out Vector2 canvasLocalPoint))
            return;

        Vector3 canvasWorldPoint = _canvasRectTransform.TransformPoint(canvasLocalPoint);
        RectTransform parentRect = buttonRect.parent as RectTransform;
        if (parentRect == null)
        {
            buttonRect.position = new Vector3(canvasWorldPoint.x, canvasWorldPoint.y, buttonRect.position.z);
            return;
        }

        Vector2 parentLocalPoint = parentRect.InverseTransformPoint(canvasWorldPoint);
        if (parentRect == _canvasRectTransform)
        {
            buttonRect.anchoredPosition = canvasLocalPoint;
        }
        else
        {
            buttonRect.localPosition = new Vector3(parentLocalPoint.x, parentLocalPoint.y, buttonRect.localPosition.z);
        }
    }

    private void OnDestroy()
    {
        CancelInvoke(nameof(InitializeButtonFollow));

        if (_buttonDrags != null)
        {
            int beginCount = _dragBeginHandlers != null ? Mathf.Min(_buttonDrags.Count, _dragBeginHandlers.Count) : 0;
            for (int i = 0; i < beginCount; i++)
            {
                if (_buttonDrags[i] != null && _dragBeginHandlers[i] != null)
                    _buttonDrags[i].OnDragBegin -= _dragBeginHandlers[i];
            }

            int endCount = _dragEndHandlers != null ? Mathf.Min(_buttonDrags.Count, _dragEndHandlers.Count) : 0;
            for (int i = 0; i < endCount; i++)
            {
                if (_buttonDrags[i] != null && _dragEndHandlers[i] != null)
                    _buttonDrags[i].OnDragEnd -= _dragEndHandlers[i];
            }
        }

        if (!_ownsWorldColliders || _worldColliders == null)
            return;

        foreach (GameObject obj in _worldColliders)
        {
            if (obj != null)
                Destroy(obj);
        }

        _worldColliders.Clear();
    }

    public void OnShitEnterWater()
    {
        if (water == null)
            return;

        if (_currentWaterOffsetY >= maxOffsetY)
            return;

        float rise = Mathf.Min(waterRiseAmount, maxOffsetY - _currentWaterOffsetY);
        _currentWaterOffsetY += rise;

        Vector3 pos = water.position;
        pos.y = _waterOriginPos.y + _currentWaterOffsetY;
        water.position = pos;

        AudioManager.PlaySound("jumpToWater");
    }

    public void OnColliderEnter(Collider2D collider)
    {
        if (!_enteredColliders.Add(collider))
            return;

        AudioManager.PlaySound("jumpToWater");
    }

    public void ClickCrow(Transform crow, Vector2 worldPoint)
    {
        CrowCtrl crowCtrl = GetCrowCtrl();
        if (crowCtrl == null)
        {
            Debug.LogWarning("[Level57Ctrl] ClickCrow skipped. CrowCtrl is missing.", this);
            return;
        }

        crowCtrl.ClickCrow(crow, worldPoint);
    }

    public void ClickCrow(Transform crow)
    {
        ClickCrow(crow, crow != null ? (Vector2)crow.position : Vector2.zero);
    }

    private CrowCtrl GetCrowCtrl()
    {
        if (_crowCtrl == null)
            _crowCtrl = GetComponent<CrowCtrl>();

        return _crowCtrl;
    }
}

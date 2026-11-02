using UnityEngine;
using Bear.EventSystem;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;

/// <summary>
/// 关卡设计。
/// 监听 Input.touch 事件 down 的时候 raycast 判断是否有碰到 TriggerITem。如果有的话，触发选中 Level62BlockItemCtrl 选中逻辑。
/// 选中逻辑为 selectBox 是否显示。Level62BlockItemCtrl 包含 selectBox 控制以及 SpriteRendererGroupAlpha 控制。
/// 脚本控制 Level62BlockItemCtrl List. 触发选中逻辑的时候，选中其中一个 block 其他的 block 取消选中状态。
/// 如果没有碰到 TriggerItem，则触发圈选逻辑。控制选中框 Selector 按照 startposition 和 currentPosition 进行 size 控制。
/// EndTouch 的时候，在 select area 的范围内的 blockItem 会触发选中逻辑。不会互相取消。
/// </summary>
public class Level62Ctrl : MonoBehaviour
{
    private enum SelectionMode
    {
        Idle,
        AreaSelecting
    }

    [Header("选择对象")]
    [SerializeField] private Level62BlockItemCtrl[] blockItems;

    [Header("复选框")]
    [SerializeField] private SpriteRenderer selectorBox;
    [SerializeField] private string selectorBoxName = "SP_L62_xzk_img_02";

    [Header("输入")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask selectableLayers = -1;
    [SerializeField] private bool ignoreUiPointer = true;

    private SelectionMode _selectionMode = SelectionMode.Idle;
    private Vector3 _areaStartPosition;
    private Vector3 _areaCurrentPosition;
    private bool _hasActiveTouch;
    private int _activeFingerId = -1;
    private bool _usingEditorMouse;
    private EventSubscriber _subscriber;
    private bool _isPaused;
    private Vector3 _selectorOriginalPosition;
    private Vector3 _selectorOriginalScale = Vector3.one;
    private Vector2 _selectorOriginalSize = Vector2.one;
    private SpriteDrawMode _selectorOriginalDrawMode = SpriteDrawMode.Simple;
    private float _selectorZ;

    private void Awake()
    {
        ResolveReferences();
        CacheSelectorOriginalSize();
        ResetSelectionState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        AddListener();
        ResetSelectionState();
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        ClearActiveTouch();
        HideSelectorBox();
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        ClearActiveTouch();
        HideSelectorBox();
    }

    private void Update()
    {
        HandleInput();
    }

    public void ResetSelectionState()
    {
        ResolveReferences();
        ClearActiveTouch();
        HideSelectorBox();
        ResetAllBlocks();
    }

    private void HandleInput()
    {
        if (_isPaused)
        {
            return;
        }

#if UNITY_EDITOR
        HandleMouseInput();
#endif
        HandleTouchInput();
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            BeginInput(Input.mousePosition, -1, true);
        }
        else if (_hasActiveTouch && _usingEditorMouse && Input.GetMouseButton(0))
        {
            UpdateInput(Input.mousePosition);
        }
        else if (_hasActiveTouch && _usingEditorMouse && Input.GetMouseButtonUp(0))
        {
            EndInput(false);
        }
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount <= 0)
        {
            if (_hasActiveTouch && !_usingEditorMouse)
            {
                EndInput(true);
            }

            return;
        }

        if (_hasActiveTouch && !_usingEditorMouse)
        {
            if (!TryGetActiveTouch(out Touch touch))
            {
                EndInput(true);
                return;
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                EndInput(touch.phase == TouchPhase.Canceled);
            }
            else
            {
                UpdateInput(touch.position);
            }

            return;
        }

        if (_hasActiveTouch)
        {
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began)
            {
                continue;
            }

            BeginInput(touch.position, touch.fingerId, false);
            break;
        }
    }

    private void BeginInput(Vector2 screenPosition, int fingerId, bool usingEditorMouse)
    {
        if (ignoreUiPointer && IsPointerOverUi(screenPosition, fingerId))
        {
            return;
        }

        _hasActiveTouch = true;
        _activeFingerId = fingerId;
        _usingEditorMouse = usingEditorMouse;

        Vector3 worldPosition = ScreenToWorldPosition(screenPosition);
        Level62BlockItemCtrl touchedBlock = FindTouchedBlock(worldPosition);

        if (touchedBlock != null)
        {
            SelectSingleBlock(touchedBlock);
            return;
        }

        BeginAreaSelection(worldPosition);
    }

    private void UpdateInput(Vector2 screenPosition)
    {
        if (_selectionMode != SelectionMode.AreaSelecting)
        {
            return;
        }

        _areaCurrentPosition = ScreenToWorldPosition(screenPosition);
        UpdateSelectorBox();
    }

    private void EndInput(bool canceled)
    {
        if (_selectionMode == SelectionMode.AreaSelecting && !canceled)
        {
            SelectBlocksInArea();
        }

        ClearActiveTouch();
        HideSelectorBox();
    }

    private void BeginAreaSelection(Vector3 worldPosition)
    {
        _selectionMode = SelectionMode.AreaSelecting;
        _areaStartPosition = worldPosition;
        _areaCurrentPosition = worldPosition;
        ShowSelectorBox();
        UpdateSelectorBox();
    }

    private void SelectSingleBlock(Level62BlockItemCtrl selectedBlock)
    {
        if (selectedBlock == null)
        {
            return;
        }

        ResolveReferences();
        for (int i = 0; i < blockItems.Length; i++)
        {
            Level62BlockItemCtrl blockItem = blockItems[i];
            if (blockItem == null)
            {
                continue;
            }

            blockItem.SetSelected(blockItem == selectedBlock);
        }
    }

    private void SelectBlocksInArea()
    {
        ResolveReferences();
        Rect area = BuildAreaRect(_areaStartPosition, _areaCurrentPosition);

        for (int i = 0; i < blockItems.Length; i++)
        {
            Level62BlockItemCtrl blockItem = blockItems[i];
            if (blockItem == null)
            {
                continue;
            }

            Vector3 centerPosition = blockItem.CenterPosition;
            if (area.Contains(new Vector2(centerPosition.x, centerPosition.y), true))
            {
                blockItem.Select();
            }
        }
    }

    private Level62BlockItemCtrl FindTouchedBlock(Vector3 worldPosition)
    {
        ResolveReferences();
        if (blockItems == null)
        {
            return null;
        }

        for (int i = 0; i < blockItems.Length; i++)
        {
            Level62BlockItemCtrl blockItem = blockItems[i];
            if (blockItem == null || ((1 << blockItem.gameObject.layer) & selectableLayers) == 0)
            {
                continue;
            }

            if (blockItem.ContainsWorldPoint(worldPosition))
            {
                return blockItem;
            }
        }

        return null;
    }

    private bool IsConfiguredBlock(Level62BlockItemCtrl target)
    {
        ResolveReferences();
        for (int i = 0; i < blockItems.Length; i++)
        {
            if (blockItems[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    private Rect BuildAreaRect(Vector3 startPosition, Vector3 currentPosition)
    {
        float minX = Mathf.Min(startPosition.x, currentPosition.x);
        float minY = Mathf.Min(startPosition.y, currentPosition.y);
        float width = Mathf.Abs(currentPosition.x - startPosition.x);
        float height = Mathf.Abs(currentPosition.y - startPosition.y);
        return new Rect(minX, minY, width, height);
    }

    private void UpdateSelectorBox()
    {
        if (selectorBox == null)
        {
            return;
        }

        Rect area = BuildAreaRect(_areaStartPosition, _areaCurrentPosition);
        Transform selectorTransform = selectorBox.transform;
        selectorTransform.position = new Vector3(area.center.x, area.center.y, _selectorZ);
        selectorBox.drawMode = SpriteDrawMode.Sliced;
        selectorBox.size = WorldSizeToSelectorLocalSize(area.size);
    }

    private void ShowSelectorBox()
    {
        if (selectorBox != null)
        {
            selectorBox.enabled = true;
        }
    }

    private void HideSelectorBox()
    {
        if (selectorBox == null)
        {
            return;
        }

        selectorBox.enabled = false;
        selectorBox.drawMode = _selectorOriginalDrawMode;
        selectorBox.size = _selectorOriginalSize;
        selectorBox.transform.position = _selectorOriginalPosition;
        selectorBox.transform.localScale = _selectorOriginalScale;
    }

    private void ResetAllBlocks()
    {
        if (blockItems == null)
        {
            return;
        }

        for (int i = 0; i < blockItems.Length; i++)
        {
            if (blockItems[i] != null)
            {
                blockItems[i].ResetSelection();
            }
        }
    }

    private void ClearActiveTouch()
    {
        _selectionMode = SelectionMode.Idle;
        _hasActiveTouch = false;
        _activeFingerId = -1;
        _usingEditorMouse = false;
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        _subscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        _isPaused = !evt.NewState.Equals(GamePlayStateName.PLAYING);
        if (_isPaused)
        {
            ClearActiveTouch();
            HideSelectorBox();
        }
    }

    private void OnGameReset(GameResetEvent evt)
    {
        ResetSelectionState();
    }

    private void ResolveReferences()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                worldCamera = FindFirstObjectByType<Camera>();
            }
        }

        if (blockItems == null || blockItems.Length == 0)
        {
            blockItems = GetComponentsInChildren<Level62BlockItemCtrl>(true);
        }

        if (selectorBox == null)
        {
            selectorBox = FindSelectorBox();
            CacheSelectorOriginalSize();
        }
    }

    private SpriteRenderer FindSelectorBox()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer rendererItem = renderers[i];
            if (rendererItem != null && rendererItem.name == selectorBoxName)
            {
                return rendererItem;
            }
        }

        return null;
    }

    private void CacheSelectorOriginalSize()
    {
        if (selectorBox == null)
        {
            return;
        }

        Transform selectorTransform = selectorBox.transform;
        _selectorOriginalPosition = selectorTransform.position;
        _selectorOriginalScale = selectorTransform.localScale;
        _selectorOriginalSize = selectorBox.size;
        _selectorOriginalDrawMode = selectorBox.drawMode;
        _selectorZ = selectorTransform.position.z;

        if (_selectorOriginalSize.x <= 0f || _selectorOriginalSize.y <= 0f)
        {
            Sprite sprite = selectorBox.sprite;
            if (sprite != null)
            {
                _selectorOriginalSize = sprite.bounds.size;
            }
        }
    }

    private Vector2 WorldSizeToSelectorLocalSize(Vector2 worldSize)
    {
        if (selectorBox == null)
        {
            return worldSize;
        }

        Vector3 lossyScale = selectorBox.transform.lossyScale;
        float scaleX = Mathf.Abs(lossyScale.x);
        float scaleY = Mathf.Abs(lossyScale.y);

        return new Vector2(
            scaleX > 0f ? worldSize.x / scaleX : worldSize.x,
            scaleY > 0f ? worldSize.y / scaleY : worldSize.y
        );
    }

    private bool TryGetActiveTouch(out Touch activeTouch)
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId == _activeFingerId)
            {
                activeTouch = touch;
                return true;
            }
        }

        activeTouch = default;
        return false;
    }

    private Vector3 ScreenToWorldPosition(Vector2 screenPosition)
    {
        ResolveReferences();
        if (worldCamera == null)
        {
            return screenPosition;
        }

        float zDistance = Mathf.Abs(worldCamera.transform.position.z - transform.position.z);
        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, zDistance));
        worldPosition.z = transform.position.z;
        return worldPosition;
    }

    private bool IsPointerOverUi(Vector2 screenPosition, int fingerId)
    {
        if (fingerId >= 0)
        {
            return InputUtils.IsPointerOverUI(fingerId);
        }

        return InputUtils.IsPointerOverUI(screenPosition);
    }
}

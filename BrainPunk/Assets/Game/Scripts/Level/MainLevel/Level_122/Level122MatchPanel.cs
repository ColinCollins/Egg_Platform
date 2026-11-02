using System.Collections.Generic;
using DG.Tweening;
using Game.Scripts.Common;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Level 122 三消面板。可视骨架来自 Resources/Level122MatchPopup.prefab，
/// 运行时只生成随机叠放的道具与交互状态。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level122MatchPanel : MonoBehaviour
{
    private const int TrayCapacity = 6;
    private const int LockType = 0;
    private const int LockCount = 6;
    private const int PropTripleCount = 76;
    private const float PropTileSize = 122f;
    private const float LockTileSize = 100f;
    private const float TrayPropSize = 92f;
    private const float TrayLockSize = 78f;
    private const float PageOpenDuration = 0.28f;
    internal const float PageCloseDuration = 0.2f;
    private const float TileMoveDuration = 0.24f;
    private const float MatchShakeDuration = 0.2f;
    private const float MatchDisappearDuration = 0.18f;

    private sealed class TileRuntime
    {
        public int Id;
        public int Type;
        public bool Removed;
        public Level122TileView View;
    }

    private sealed class TrayEntry
    {
        public int Type;
        public int SlotIndex;
        public bool IsExternalLock;
        public RectTransform View;
        public SpriteRenderer WorldView;
    }

    private Level122Ctrl _owner;
    private Canvas _canvas;
    private RectTransform _root;
    private RectTransform _pagePanel;
    private RectTransform _boardRoot;
    private RectTransform _trayRoot;
    private CanvasGroup _pageCanvasGroup;
    private SortingGroup _temporaryLockSortingGroup;
    private SpriteRenderer _dragWorldView;
    private SkeletonRenderer _sceneLockSkeleton;
    private float[] _lockVertices;
    private readonly List<TileRuntime> _tiles = new List<TileRuntime>();
    private readonly Dictionary<int, TileRuntime> _tilesById = new Dictionary<int, TileRuntime>();
    private readonly List<TrayEntry> _trayEntries = new List<TrayEntry>();
    private readonly RectTransform[] _slotRects = new RectTransform[TrayCapacity];
    private readonly Image[] _slotImages = new Image[TrayCapacity];

    private RectTransform _dragGhost;
    private int _draggingSlotIndex = -1;
    private bool _externalLockCaptured;
    private bool _interactionLocked;
    private bool _finished;
    private bool _closing;
    private Sequence _matchSequence;
    private Transform _matchingSceneLock;
    private Vector3 _sceneLockOriginalPosition;
    private Vector3 _sceneLockOriginalScale;
    private float _sceneLockOriginalAlpha;

    public static Level122MatchPanel Create(Level122Ctrl owner, Canvas canvas)
    {
        if (owner == null || canvas == null)
            return null;

        Level122MatchPanel prefab = Resources.Load<Level122MatchPanel>("Level122MatchPopup");
        if (prefab == null)
        {
            Debug.LogWarning("[Level122MatchPanel] Resources/Level122MatchPopup.prefab 未找到。");
            return null;
        }

        Level122MatchPanel panel = Instantiate(prefab, canvas.transform, false);
        panel.gameObject.SetActive(false);
        panel.gameObject.layer = LayerMask.NameToLayer("UI");

        RectTransform root = panel.transform as RectTransform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;

        panel.Initialize(owner, canvas, root);
        panel.gameObject.SetActive(true);
        panel.transform.SetAsLastSibling();
        panel.PlayOpenAnimation();
        return panel;
    }

    private void Initialize(Level122Ctrl owner, Canvas canvas, RectTransform root)
    {
        _owner = owner;
        _canvas = canvas;
        _root = root;
        _sceneLockSkeleton = owner.SceneLockTarget.GetComponentInChildren<SkeletonRenderer>();

        _pagePanel = transform.Find("PagePanel") as RectTransform;
        _boardRoot = _pagePanel != null ? _pagePanel.Find("RandomPile") as RectTransform : null;
        _trayRoot = _pagePanel != null ? _pagePanel.Find("SixSlotTray") as RectTransform : null;
        Level122CloseButton closeButton = _pagePanel != null
            ? _pagePanel.GetComponentInChildren<Level122CloseButton>(true)
            : null;

        if (_boardRoot == null || _trayRoot == null || closeButton == null)
        {
            Debug.LogWarning("[Level122MatchPanel] 页面 Prefab 缺少 RandomPile、SixSlotTray 或 CloseButton。");
            return;
        }

        closeButton.Configure(this);
        _pageCanvasGroup = _pagePanel.GetComponent<CanvasGroup>();
        if (_pageCanvasGroup == null)
            _pageCanvasGroup = _pagePanel.gameObject.AddComponent<CanvasGroup>();
        _pagePanel.localScale = Vector3.one * 0.82f;
        _pageCanvasGroup.alpha = 0f;
        Canvas.ForceUpdateCanvases();
        BuildTray();
        BuildRandomBoard();
        RefreshBoardInteractivity();
    }

    private void BuildTray()
    {
        const float spacing = 126f;
        float firstX = -spacing * (TrayCapacity - 1) * 0.5f;
        for (int i = 0; i < TrayCapacity; i++)
        {
            Image slot = CreateImage(
                $"Slot_{i + 1}",
                _trayRoot,
                _owner.SlotSprite,
                new Vector2(110f, 108f),
                new Vector2(firstX + spacing * i, 0f),
                Color.white,
                true);
            _slotRects[i] = slot.rectTransform;
            _slotImages[i] = slot;

            Level122SlotDragHandle dragHandle = slot.gameObject.AddComponent<Level122SlotDragHandle>();
            dragHandle.Configure(this, i);
        }
    }

    private void PlayOpenAnimation()
    {
        if (_pagePanel == null || _pageCanvasGroup == null)
            return;

        _pagePanel.DOKill();
        _pageCanvasGroup.DOKill();
        _pagePanel.localScale = Vector3.one * 0.82f;
        _pageCanvasGroup.alpha = 0f;
        _pagePanel.DOScale(Vector3.one, PageOpenDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(gameObject);
        _pageCanvasGroup.DOFade(1f, PageOpenDuration * 0.72f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void BuildRandomBoard()
    {
        List<int> contentTypes = BuildPropDeck();
        Shuffle(contentTypes);

        // 锁仍然随机落位，但绘制顺序放在最后一小段，避免被两百多个普通道具完全埋住。
        // 最后一段仍混入普通道具，不让六把锁整齐地浮在最上方。
        const int visibleBandPropCount = 12;
        List<int> visibleBand = new List<int>(visibleBandPropCount + LockCount);
        for (int i = 0; i < visibleBandPropCount && contentTypes.Count > 0; i++)
        {
            int lastIndex = contentTypes.Count - 1;
            visibleBand.Add(contentTypes[lastIndex]);
            contentTypes.RemoveAt(lastIndex);
        }
        for (int i = 0; i < LockCount; i++)
            visibleBand.Add(LockType);
        Shuffle(visibleBand);
        contentTypes.AddRange(visibleBand);

        // 按椭圆面积均匀散点，并用多频轮廓扰动和少量外溢点打破规则边界。
        // 这里只控制画面密度，不参与任何“上层/下层”可选规则。
        List<Vector2> positions = BuildOrganicPilePositions(contentTypes.Count);
        Shuffle(positions);

        for (int i = 0; i < contentTypes.Count; i++)
            AddBoardTile(i, contentTypes[i], positions[i]);
    }

    private static List<Vector2> BuildOrganicPilePositions(int count)
    {
        // 红圈更接近圆角矩形而非椭圆；用四次方超椭圆填满四角区域，
        // 同时保留曲线边界，避免重新出现规整的矩形牌墙。
        const float maxCenterHalfWidth = 535f;
        const float maxCenterHalfHeight = 214f;
        float contourPhase = Random.Range(0f, Mathf.PI * 2f);
        List<Vector2> positions = new List<Vector2>(count);
        for (int i = 0; i < count; i++)
        {
            float normalizedX;
            float normalizedY;
            float superEllipseRadius;
            do
            {
                normalizedX = Random.Range(-1f, 1f);
                normalizedY = Random.Range(-1f, 1f);
                superEllipseRadius = Mathf.Pow(Mathf.Abs(normalizedX), 4f) +
                                     Mathf.Pow(Mathf.Abs(normalizedY), 4f);
            } while (superEllipseRadius > 1f);

            float angle = Mathf.Atan2(normalizedY, normalizedX);
            float contour = 1f
                + Mathf.Sin(angle * 3f + contourPhase) * 0.035f
                + Mathf.Sin(angle * 7f - contourPhase * 0.65f) * 0.025f
                + Mathf.Sin(angle * 11f + contourPhase * 1.3f) * 0.015f;
            float x = normalizedX * maxCenterHalfWidth * contour + Random.Range(-9f, 9f);
            float y = normalizedY * maxCenterHalfHeight * contour + Random.Range(-8f, 8f);

            float boundaryValue =
                Mathf.Pow(Mathf.Abs(x / maxCenterHalfWidth), 4f) +
                Mathf.Pow(Mathf.Abs(y / maxCenterHalfHeight), 4f);
            if (boundaryValue > 1f)
            {
                float boundaryScale = Mathf.Pow(1f / boundaryValue, 0.25f) * Random.Range(0.97f, 1f);
                x *= boundaryScale;
                y *= boundaryScale;
            }
            positions.Add(new Vector2(x, y));
        }
        return positions;
    }

    private List<int> BuildPropDeck()
    {
        Sprite[] props = _owner.PropSprites;
        List<int> availableTypes = new List<int>(props.Length);
        for (int i = 0; i < props.Length; i++)
        {
            if (props[i] != null)
                availableTypes.Add(i + 1);
        }

        Shuffle(availableTypes);
        List<int> deck = new List<int>(PropTripleCount * 3);
        for (int triple = 0; triple < PropTripleCount; triple++)
        {
            int type = availableTypes[triple % availableTypes.Count];
            deck.Add(type);
            deck.Add(type);
            deck.Add(type);
        }

        Shuffle(deck);
        return deck;
    }

    private void AddBoardTile(int id, int type, Vector2 position)
    {
        RectTransform visual = CreateBoardTileVisual($"Tile_{id}", _boardRoot, type, position);
        visual.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        Level122TileView view = visual.gameObject.AddComponent<Level122TileView>();
        view.Configure(this, id);

        TileRuntime tile = new TileRuntime
        {
            Id = id,
            Type = type,
            Removed = false,
            View = view,
        };
        _tiles.Add(tile);
        _tilesById.Add(id, tile);
    }

    internal void HandleTileClicked(int tileId)
    {
        if (_interactionLocked || _finished || !_tilesById.TryGetValue(tileId, out TileRuntime tile) ||
            tile.Removed)
        {
            return;
        }

        int slotIndex = FindFirstFreeSlot();
        if (slotIndex < 0)
        {
            BeginDeadlock();
            return;
        }

        _interactionLocked = true;
        tile.Removed = true;
        tile.View.SetInteractive(false);
        RefreshBoardInteractivity();

        RectTransform view = tile.View.RectTransform;
        Vector3 worldPosition = view.position;
        view.SetParent(_root, true);
        view.position = worldPosition;
        view.SetAsLastSibling();
        tile.View.SetTrayVisual();

        TrayEntry entry = new TrayEntry
        {
            Type = tile.Type,
            SlotIndex = slotIndex,
            IsExternalLock = false,
            View = view,
        };
        _trayEntries.Add(entry);

        Sequence move = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        move.Join(view.DOAnchorPos(GetSlotRootPosition(slotIndex), TileMoveDuration).SetEase(Ease.OutBack));
        move.Join(view.DORotate(Vector3.zero, TileMoveDuration).SetEase(Ease.OutQuad));
        float traySize = tile.Type == LockType ? TrayLockSize : TrayPropSize;
        move.Join(view.DOSizeDelta(Vector2.one * traySize, TileMoveDuration).SetEase(Ease.OutQuad));
        move.OnComplete(ResolveAfterTrayChanged);
    }

    internal bool TryBeginSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (_closing || _interactionLocked || _finished || _externalLockCaptured || _dragGhost != null ||
            slotIndex < 0 || slotIndex >= TrayCapacity || IsSlotOccupied(slotIndex))
        {
            return false;
        }

        _draggingSlotIndex = slotIndex;
        _interactionLocked = true;
        RefreshBoardInteractivity();
        _dragGhost = CreateCaptureSquare(
            "LockCaptureSquare",
            _root,
            GetSlotRootPosition(slotIndex),
            false);
        _dragGhost.SetAsLastSibling();
        // 原格从暂存区消失，拖动体保持同一外观，视觉上是把这一个方框直接拿走。
        _slotImages[slotIndex].enabled = false;
        ContinueSlotDrag(slotIndex, eventData);
        return true;
    }

    internal void ContinueSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (_dragGhost == null || slotIndex != _draggingSlotIndex || eventData == null)
            return;

        if (TryScreenToRootLocal(eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            _dragGhost.anchoredPosition = localPoint;

        // Overlay UI 无法排到场景锁后面。进入锁附近只切换绘制方式，位置仍跟随手指，绝不提前吸附。
        bool nearLock = IsPointerOverSceneLock(eventData.position);
        if (nearLock && _dragWorldView == null)
            _dragWorldView = CreateCapturedWorldSquare(_dragGhost);
        if (_dragWorldView != null)
        {
            _dragWorldView.enabled = nearLock;
            if (nearLock)
            {
                Camera camera = Camera.main;
                Vector3 screenPoint = camera.WorldToScreenPoint(GetSceneLockWorldCenter(_owner.SceneLockTarget));
                screenPoint.x = eventData.position.x;
                screenPoint.y = eventData.position.y;
                SetSquareWorldCenter(_dragWorldView, camera.ScreenToWorldPoint(screenPoint));
            }
        }
        _dragGhost.GetComponent<Image>().enabled = !nearLock || _dragWorldView == null;
    }

    internal void EndSlotDrag(int slotIndex, PointerEventData eventData)
    {
        if (_dragGhost == null || slotIndex != _draggingSlotIndex || eventData == null)
            return;

        if (IsPointerOverSceneLock(eventData.position))
        {
            AcceptExternalLock(slotIndex);
            return;
        }

        ReturnDragGhost(slotIndex, 0.16f);
    }

    internal void CancelSlotDrag(int slotIndex)
    {
        if (_closing || _dragGhost == null || slotIndex != _draggingSlotIndex)
            return;

        ReturnDragGhost(slotIndex, 0.1f);
    }

    internal void HandleCloseRequested()
    {
        if (_closing || _finished)
            return;

        _owner?.NotifyPanelClosedByUser(this);
    }

    private void AcceptExternalLock(int slotIndex)
    {
        RectTransform capturedView = _dragGhost;
        SpriteRenderer capturedWorldView = _dragWorldView != null
            ? _dragWorldView
            : CreateCapturedWorldSquare(capturedView);
        if (capturedWorldView == null)
        {
            ReturnDragGhost(slotIndex, 0.1f);
            return;
        }

        _interactionLocked = true;
        _externalLockCaptured = true;
        _dragWorldView = null;
        capturedWorldView.enabled = true;
        SetSquareWorldCenter(capturedWorldView, GetSceneLockWorldCenter(_owner.SceneLockTarget));
        _dragGhost = null;
        _draggingSlotIndex = -1;
        _slotImages[slotIndex].enabled = false;

        TrayEntry entry = new TrayEntry
        {
            Type = LockType,
            SlotIndex = slotIndex,
            IsExternalLock = true,
            View = null,
            WorldView = capturedWorldView,
        };
        _trayEntries.Add(entry);

        if (capturedView != null)
        {
            // UI 在本帧渲染前立即隐藏；Destroy 的延迟销毁不能让拖拽图再盖住场景锁一帧。
            capturedView.gameObject.SetActive(false);
            Destroy(capturedView.gameObject);
        }
        ResolveAfterTrayChanged();
    }

    private SpriteRenderer CreateCapturedWorldSquare(RectTransform sourceSquare)
    {
        Transform target = _owner != null ? _owner.SceneLockTarget : null;
        Camera worldCamera = Camera.main;
        if (target == null || _owner.SlotSprite == null || worldCamera == null || sourceSquare == null)
            return null;

        Renderer[] lockRenderers = target.GetComponentsInChildren<Renderer>(true);
        Vector3 lockCenter = GetSceneLockWorldCenter(target, lockRenderers);

        GameObject squareObject = new GameObject("CapturedSquareBehindSceneLock");
        squareObject.layer = target.gameObject.layer;
        squareObject.transform.SetParent(target, false);
        squareObject.transform.position = lockCenter;
        squareObject.transform.rotation = Quaternion.identity;
        SpriteRenderer square = squareObject.AddComponent<SpriteRenderer>();
        square.sprite = _owner.SlotSprite;

        if (lockRenderers.Length > 0)
        {
            int minimumOrder = lockRenderers[0].sortingOrder;
            int sortingLayerId = lockRenderers[0].sortingLayerID;
            for (int i = 1; i < lockRenderers.Length; i++)
            {
                minimumOrder = Mathf.Min(minimumOrder, lockRenderers[i].sortingOrder);
            }

            // 当前门图为 100、锁为 101。给本关运行时锁增加局部组：整组在门前，组内方框在锁后。
            // 不改变共享门/锁资源、不移动锁，也不依赖不同材质在相同 Order 下的先后顺序。
            if (target.GetComponent<SortingGroup>() == null)
            {
                _temporaryLockSortingGroup = target.gameObject.AddComponent<SortingGroup>();
                _temporaryLockSortingGroup.sortingLayerID = sortingLayerId;
                _temporaryLockSortingGroup.sortingOrder = minimumOrder;
            }
            square.sortingLayerID = sortingLayerId;
            square.sortingOrder = minimumOrder - 1;
        }

        Vector2 screenSize = GetRectScreenSize(sourceSquare);
        Vector2 worldSize = GetWorldSizeAtPoint(screenSize, lockCenter, worldCamera);
        Vector3 parentScale = squareObject.transform.parent != null
            ? squareObject.transform.parent.lossyScale
            : Vector3.one;
        float parentScaleX = Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
        float parentScaleY = Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));
        float spriteWidth = Mathf.Max(0.0001f, square.sprite.bounds.size.x);
        float spriteHeight = Mathf.Max(0.0001f, square.sprite.bounds.size.y);
        squareObject.transform.localScale = new Vector3(
            worldSize.x / spriteWidth / parentScaleX,
            worldSize.y / spriteHeight / parentScaleY,
            1f);
        SetSquareWorldCenter(square, lockCenter);

        return square;
    }

    private static void SetSquareWorldCenter(SpriteRenderer square, Vector3 center)
    {
        // 按最终图形中心定位，而非依赖 Sprite 的 Pivot。
        square.transform.position += center - square.bounds.center;
    }

    private Vector3 GetSceneLockWorldCenter(Transform target, Renderer[] lockRenderers = null)
    {
        if (target == null)
            return Vector3.zero;

        // DoorLock 的 Mesh 同时包含两侧链条，整体 Bounds 中心不是锁体中心。
        // 读取本关已使用的 Spine "suo" 锁体附件，只计算它当前姿态的顶点范围。
        Spine.Slot lockSlot = _sceneLockSkeleton != null ? _sceneLockSkeleton.Skeleton?.FindSlot("suo") : null;
        if (lockSlot?.Attachment is Spine.VertexAttachment attachment && attachment.WorldVerticesLength >= 2)
        {
            if (_lockVertices == null || _lockVertices.Length != attachment.WorldVerticesLength)
                _lockVertices = new float[attachment.WorldVerticesLength];
            attachment.ComputeWorldVertices(lockSlot, _lockVertices);
            Vector3 first = _sceneLockSkeleton.transform.TransformPoint(_lockVertices[0], _lockVertices[1], 0f);
            Bounds lockBody = new Bounds(first, Vector3.zero);
            for (int i = 2; i < _lockVertices.Length; i += 2)
                lockBody.Encapsulate(_sceneLockSkeleton.transform.TransformPoint(_lockVertices[i], _lockVertices[i + 1], 0f));
            return lockBody.center;
        }

        Renderer[] renderers = lockRenderers ?? target.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            // 方框现为锁子节点，回退时不能把方框自身再次算进吸附中心。
            if (renderer != _dragWorldView && renderer.transform == target && renderer.bounds.size.sqrMagnitude > 0.0001f)
                return renderer.bounds.center;
        }

        Collider2D lockCollider = target.GetComponent<Collider2D>();
        if (lockCollider == null)
            lockCollider = target.GetComponentInChildren<Collider2D>(true);
        return lockCollider != null ? lockCollider.bounds.center : target.position;
    }

    private Vector2 GetRectScreenSize(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera
            : null;
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
        return new Vector2(
            Mathf.Abs(topRight.x - bottomLeft.x),
            Mathf.Abs(topRight.y - bottomLeft.y));
    }

    private static Vector2 GetWorldSizeAtPoint(Vector2 screenSize, Vector3 worldPoint, Camera worldCamera)
    {
        Vector3 centerScreen = worldCamera.WorldToScreenPoint(worldPoint);
        Vector3 bottomLeftScreen = new Vector3(
            centerScreen.x - screenSize.x * 0.5f,
            centerScreen.y - screenSize.y * 0.5f,
            centerScreen.z);
        Vector3 topRightScreen = new Vector3(
            centerScreen.x + screenSize.x * 0.5f,
            centerScreen.y + screenSize.y * 0.5f,
            centerScreen.z);
        Vector3 bottomLeftWorld = worldCamera.ScreenToWorldPoint(bottomLeftScreen);
        Vector3 topRightWorld = worldCamera.ScreenToWorldPoint(topRightScreen);
        return new Vector2(
            Mathf.Abs(topRightWorld.x - bottomLeftWorld.x),
            Mathf.Abs(topRightWorld.y - bottomLeftWorld.y));
    }

    private void ReturnDragGhost(int slotIndex, float duration)
    {
        if (_dragWorldView != null)
        {
            _dragWorldView.gameObject.SetActive(false);
            Destroy(_dragWorldView.gameObject);
            _dragWorldView = null;
        }
        RectTransform ghost = _dragGhost;
        _dragGhost = null;
        _draggingSlotIndex = -1;

        if (ghost != null)
        {
            Image ghostImage = ghost.GetComponent<Image>();
            if (ghostImage != null)
                ghostImage.enabled = true;
        }

        if (ghost == null)
        {
            RestoreSlotVisual(slotIndex);
            _interactionLocked = false;
            RefreshBoardInteractivity();
            return;
        }

        ghost.DOAnchorPos(GetSlotRootPosition(slotIndex), duration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                if (ghost != null)
                    Destroy(ghost.gameObject);
                RestoreSlotVisual(slotIndex);
                _interactionLocked = false;
                RefreshBoardInteractivity();
            });
    }

    private void ResolveAfterTrayChanged()
    {
        if (_finished || _closing)
            return;

        List<TrayEntry> match = FindMatch(out bool unlocksSceneLock);
        if (match != null)
        {
            AnimateMatch(match, unlocksSceneLock);
            return;
        }

        _interactionLocked = false;
        RefreshBoardInteractivity();
        CheckForDeadlock();
    }

    private List<TrayEntry> FindMatch(out bool unlocksSceneLock)
    {
        unlocksSceneLock = false;
        Dictionary<int, List<TrayEntry>> byType = new Dictionary<int, List<TrayEntry>>();
        for (int i = 0; i < _trayEntries.Count; i++)
        {
            TrayEntry entry = _trayEntries[i];
            if (!byType.TryGetValue(entry.Type, out List<TrayEntry> group))
            {
                group = new List<TrayEntry>();
                byType.Add(entry.Type, group);
            }
            group.Add(entry);
        }

        foreach (KeyValuePair<int, List<TrayEntry>> pair in byType)
        {
            List<TrayEntry> group = pair.Value;
            if (group.Count < 3)
                continue;

            if (pair.Key == LockType)
            {
                TrayEntry external = group.Find(entry => entry.IsExternalLock);
                if (external != null)
                {
                    List<TrayEntry> result = new List<TrayEntry>(3) { external };
                    for (int i = 0; i < group.Count && result.Count < 3; i++)
                    {
                        if (!group[i].IsExternalLock)
                            result.Add(group[i]);
                    }

                    if (result.Count == 3)
                    {
                        unlocksSceneLock = true;
                        return result;
                    }
                }

                // 没有外部方框时，三把普通锁仍遵循“任意三个相同元素”规则正常三消。
            }

            return group.GetRange(0, 3);
        }

        return null;
    }

    private void AnimateMatch(List<TrayEntry> match, bool unlocksSceneLock)
    {
        _interactionLocked = true;
        bool clearedBoard = _trayEntries.Count == match.Count && !_tiles.Exists(tile => !tile.Removed);
        bool completesPuzzle = unlocksSceneLock || clearedBoard;
        if (completesPuzzle)
            _finished = true;
        RefreshBoardInteractivity();

        Vector2 burstCenter = Vector2.zero;
        int burstCount = 0;
        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        _matchSequence = sequence;
        Vector2 sceneBurstCenter = Vector2.zero;
        if (completesPuzzle)
        {
            sceneBurstCenter = GetWorldViewRootPosition(GetSceneLockWorldCenter(_owner.SceneLockTarget));
            AddSceneLockMatchAnimation(sequence);
        }
        for (int i = 0; i < match.Count; i++)
        {
            TrayEntry entry = match[i];
            if (entry.View != null)
            {
                burstCenter += entry.View.anchoredPosition;
                burstCount++;
                sequence.Insert(0f, entry.View.DOShakeAnchorPos(MatchShakeDuration, 15f, 18, 70f, false, true));
            }
            else if (entry.WorldView != null)
            {
                burstCenter += GetWorldViewRootPosition(entry.WorldView.transform.position);
                burstCount++;
                // 已捕获的方框是锁的子节点，由锁带着一起摇晃，避免双重位移。
                if (_matchingSceneLock == null)
                    sequence.Insert(0f, entry.WorldView.transform.DOShakePosition(MatchShakeDuration, 0.12f, 18, 70f, false, true));
            }
        }

        for (int i = 0; i < match.Count; i++)
        {
            TrayEntry entry = match[i];
            if (entry.View != null)
            {
                CanvasGroup group = entry.View.GetComponent<CanvasGroup>();
                if (group == null)
                    group = entry.View.gameObject.AddComponent<CanvasGroup>();
                sequence.Insert(MatchShakeDuration, entry.View.DOScale(Vector3.one * 1.24f, MatchDisappearDuration).SetEase(Ease.InBack));
                sequence.Insert(MatchShakeDuration, group.DOFade(0f, MatchDisappearDuration));
            }
            else if (entry.WorldView != null)
            {
                if (_matchingSceneLock == null)
                    sequence.Insert(MatchShakeDuration, entry.WorldView.transform.DOScale(entry.WorldView.transform.localScale * 1.24f, MatchDisappearDuration)
                        .SetEase(Ease.InBack));
                sequence.Insert(MatchShakeDuration, entry.WorldView.DOFade(0f, MatchDisappearDuration));
            }
        }

        if (burstCount > 0)
            burstCenter /= burstCount;

        Vector2 finalBurstCenter = burstCenter;
        sequence.OnComplete(() =>
        {
            _matchSequence = null;
            SpawnMatchBurst(finalBurstCenter);
            AudioManager.PlaySound(_owner != null ? _owner.MatchSfxTag : "passwordCorrect");

            for (int i = 0; i < match.Count; i++)
            {
                TrayEntry entry = match[i];
                _trayEntries.Remove(entry);
                if (entry.View != null)
                    Destroy(entry.View.gameObject);
                if (entry.WorldView != null)
                    Destroy(entry.WorldView.gameObject);
            }

            if (completesPuzzle)
            {
                SpawnMatchBurst(sceneBurstCenter);
                if (_matchingSceneLock != null)
                {
                    _matchingSceneLock.gameObject.SetActive(false);
                    _matchingSceneLock = null;
                }
                Level122Ctrl owner = _owner;
                owner?.NotifyMatchCompleted(this, clearedBoard);
                return;
            }

            CompactTray();
        });
    }

    #region 实体锁三消表现与中断恢复
    private void AddSceneLockMatchAnimation(Sequence sequence)
    {
        _matchingSceneLock = _owner != null ? _owner.SceneLockTarget : null;
        if (_matchingSceneLock == null)
            return;

        _sceneLockOriginalPosition = _matchingSceneLock.localPosition;
        _sceneLockOriginalScale = _matchingSceneLock.localScale;
        _sceneLockOriginalAlpha = _sceneLockSkeleton != null && _sceneLockSkeleton.Skeleton != null
            ? _sceneLockSkeleton.Skeleton.A : 1f;
        sequence.Insert(0f, _matchingSceneLock.DOShakePosition(MatchShakeDuration, 0.12f, 18, 70f, false, true));
        sequence.Insert(MatchShakeDuration, _matchingSceneLock.DOScale(_sceneLockOriginalScale * 1.24f, MatchDisappearDuration)
            .SetEase(Ease.InBack));
        if (_sceneLockSkeleton != null && _sceneLockSkeleton.Skeleton != null)
        {
            sequence.Insert(MatchShakeDuration, DOTween.To(
                () => _sceneLockSkeleton.Skeleton.A,
                alpha => _sceneLockSkeleton.Skeleton.A = alpha,
                0f, MatchDisappearDuration));
        }
    }

    private void CancelMatchAnimation()
    {
        _matchSequence?.Kill();
        _matchSequence = null;
        if (_matchingSceneLock == null)
            return;

        _matchingSceneLock.localPosition = _sceneLockOriginalPosition;
        _matchingSceneLock.localScale = _sceneLockOriginalScale;
        if (_sceneLockSkeleton != null && _sceneLockSkeleton.Skeleton != null)
            _sceneLockSkeleton.Skeleton.A = _sceneLockOriginalAlpha;
        _matchingSceneLock = null;
    }
    #endregion

    private void CompactTray()
    {
        // 空 Sequence 不会可靠执行完成回调；三消后空槽时直接继续判定。
        if (_trayEntries.Count == 0)
        {
            ResolveAfterTrayChanged();
            return;
        }
        bool[] reserved = new bool[TrayCapacity];
        TrayEntry external = _trayEntries.Find(entry => entry.IsExternalLock);
        if (external != null)
            reserved[external.SlotIndex] = true;

        int nextSlot = 0;
        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        for (int i = 0; i < _trayEntries.Count; i++)
        {
            TrayEntry entry = _trayEntries[i];
            if (entry.IsExternalLock)
                continue;

            while (nextSlot < TrayCapacity && reserved[nextSlot])
                nextSlot++;
            if (nextSlot >= TrayCapacity)
                break;

            entry.SlotIndex = nextSlot;
            reserved[nextSlot] = true;
            if (entry.View != null)
            {
                sequence.Join(entry.View.DOAnchorPos(GetSlotRootPosition(nextSlot), 0.16f)
                    .SetEase(Ease.OutQuad));
            }
            nextSlot++;
        }

        sequence.OnComplete(() =>
        {
            _interactionLocked = false;
            RefreshBoardInteractivity();
            CheckForDeadlock();
        });
    }

    private void CheckForDeadlock()
    {
        if (_finished || _closing)
            return;

        bool boardHasTiles = _tiles.Exists(tile => !tile.Removed);
        if (_trayEntries.Count >= TrayCapacity || !boardHasTiles)
            BeginDeadlock();
    }

    private void BeginDeadlock()
    {
        if (_finished || _closing)
            return;

        _finished = true;
        _interactionLocked = true;
        _owner?.NotifyDeadlock(this);
    }

    private void RefreshBoardInteractivity()
    {
        for (int i = 0; i < _tiles.Count; i++)
        {
            TileRuntime tile = _tiles[i];
            if (!tile.Removed && tile.View != null)
                tile.View.SetInteractive(!_interactionLocked);
        }
    }

    private int FindFirstFreeSlot()
    {
        for (int i = 0; i < TrayCapacity; i++)
        {
            if (!IsSlotOccupied(i))
                return i;
        }
        return -1;
    }

    private bool IsSlotOccupied(int slotIndex)
    {
        return _trayEntries.Exists(entry => entry.SlotIndex == slotIndex);
    }

    private void RestoreSlotVisual(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slotImages.Length || _slotImages[slotIndex] == null)
            return;

        _slotImages[slotIndex].enabled = true;
        _slotImages[slotIndex].color = Color.white;
    }

    private void SpawnMatchBurst(Vector2 center)
    {
        for (int i = 0; i < 10; i++)
        {
            float angle = i * Mathf.PI * 2f / 10f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Image spark = CreateImage(
                $"MatchSpark_{i}",
                _root,
                _owner.SlotSprite,
                new Vector2(18f, 18f),
                center,
                i % 2 == 0
                    ? new Color(1f, 0.82f, 0.15f, 0.95f)
                    : new Color(0.35f, 0.9f, 1f, 0.95f),
                false);
            CanvasGroup group = spark.gameObject.AddComponent<CanvasGroup>();
            spark.rectTransform.localScale = Vector3.one * 0.7f;
            spark.rectTransform.DOAnchorPos(center + direction * 92f, 0.34f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(gameObject);
            spark.rectTransform.DOScale(Vector3.one * 0.15f, 0.34f)
                .SetUpdate(true)
                .SetLink(gameObject);
            group.DOFade(0f, 0.34f)
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    if (spark != null)
                        Destroy(spark.gameObject);
                });
        }
    }

    private RectTransform CreateBoardTileVisual(
        string objectName,
        RectTransform parent,
        int type,
        Vector2 position)
    {
        Image icon = CreateImage(
            objectName,
            parent,
            GetSpriteForType(type),
            Vector2.one * (type == LockType ? LockTileSize : PropTileSize),
            position,
            Color.white,
            true);
        icon.preserveAspect = true;
        return icon.rectTransform;
    }

    private RectTransform CreateCaptureSquare(
        string objectName,
        RectTransform parent,
        Vector2 position,
        bool raycastTarget)
    {
        return CreateImage(
            objectName,
            parent,
            _owner.SlotSprite,
            new Vector2(110f, 108f),
            position,
            Color.white,
            raycastTarget).rectTransform;
    }

    private Sprite GetSpriteForType(int type)
    {
        if (type == LockType)
            return _owner.LockSprite;

        Sprite[] props = _owner.PropSprites;
        int index = type - 1;
        return index >= 0 && index < props.Length ? props[index] : null;
    }

    private Vector2 GetSlotRootPosition(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slotRects.Length || _slotRects[slotIndex] == null)
            return Vector2.zero;
        return _root.InverseTransformPoint(_slotRects[slotIndex].position);
    }

    private Vector2 GetSceneLockScreenPosition()
    {
        Camera worldCamera = Camera.main;
        Transform target = _owner != null ? _owner.SceneLockTarget : null;
        if (worldCamera == null || target == null)
            return new Vector2(Screen.width * 0.84f, Screen.height * 0.38f);

        return RectTransformUtility.WorldToScreenPoint(worldCamera, GetSceneLockWorldCenter(target));
    }

    private bool IsPointerOverSceneLock(Vector2 pointerScreenPosition)
    {
        float dropRadius = Mathf.Max(90f, Screen.height * 0.11f);
        return Vector2.Distance(pointerScreenPosition, GetSceneLockScreenPosition()) <= dropRadius;
    }

    private Vector2 GetWorldViewRootPosition(Vector3 worldPosition)
    {
        Camera worldCamera = Camera.main;
        Vector2 screenPoint = worldCamera != null
            ? RectTransformUtility.WorldToScreenPoint(worldCamera, worldPosition)
            : Vector2.zero;
        Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera
            : null;
        return TryScreenToRootLocal(screenPoint, uiCamera, out Vector2 localPoint)
            ? localPoint
            : Vector2.zero;
    }

    private bool TryScreenToRootLocal(Vector2 screenPoint, Camera eventCamera, out Vector2 localPoint)
    {
        Camera camera = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : eventCamera ?? (_canvas != null ? _canvas.worldCamera : null);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPoint, camera, out localPoint);
    }

    private static RectTransform CreateRect(
        string objectName,
        RectTransform parent,
        Vector2 size,
        Vector2 position)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform));
        target.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static Image CreateImage(
        string objectName,
        RectTransform parent,
        Sprite sprite,
        Vector2 size,
        Vector2 position,
        Color color,
        bool raycastTarget)
    {
        RectTransform rect = CreateRect(objectName, parent, size, position);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycastTarget;
        image.preserveAspect = false;
        return image;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            T value = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = value;
        }
    }

    internal void CloseFromOwner(Level122Ctrl owner, bool immediate)
    {
        if (_closing || owner == null || owner != _owner)
            return;

        _closing = true;
        _owner = null;
        CancelMatchAnimation();
        ClearSceneCaptureVisuals();
        if (_dragGhost != null)
        {
            _dragGhost.DOKill();
            Destroy(_dragGhost.gameObject);
            _dragGhost = null;
        }

        if (immediate || _pagePanel == null || _pageCanvasGroup == null || !gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        _pagePanel.DOKill();
        _pageCanvasGroup.DOKill();
        Sequence closeSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        closeSequence.Join(_pagePanel.DOScale(Vector3.one * 0.82f, PageCloseDuration)
            .SetEase(Ease.InBack));
        closeSequence.Join(_pageCanvasGroup.DOFade(0f, PageCloseDuration * 0.78f)
            .SetEase(Ease.InQuad));
        closeSequence.OnComplete(() =>
        {
            if (this != null)
                Destroy(gameObject);
        });
    }

    private void OnDestroy()
    {
        _closing = true;
        _owner = null;
        CancelMatchAnimation();
        ClearSceneCaptureVisuals();
        if (_pagePanel != null)
            _pagePanel.DOKill();
        if (_pageCanvasGroup != null)
            _pageCanvasGroup.DOKill();
        transform.DOKill();
    }

    private void ClearSceneCaptureVisuals()
    {
        if (_dragWorldView != null)
        {
            Destroy(_dragWorldView.gameObject);
            _dragWorldView = null;
        }
        foreach (TrayEntry entry in _trayEntries)
        {
            if (entry.WorldView != null)
            {
                Destroy(entry.WorldView.gameObject);
                entry.WorldView = null;
            }
        }
        if (_temporaryLockSortingGroup != null)
        {
            Destroy(_temporaryLockSortingGroup);
            _temporaryLockSortingGroup = null;
        }
    }
}

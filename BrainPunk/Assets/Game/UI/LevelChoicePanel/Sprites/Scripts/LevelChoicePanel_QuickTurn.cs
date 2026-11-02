using Bear.UI;
using Bear.Logger;
using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 主线与 Draw 选关面板共用的快速分页拖动组件。
/// 组件维护拖动位置与区间显示；实际页码、数据刷新和存档由所属面板通过回调处理。
/// </summary>
public partial class LevelChoicePanel_QuickTurn : BaseUIView, IPointerDownHandler, IBeginDragHandler, IDragHandler,
    IEndDragHandler, IDebuger
{
    #region Inspector

    /// <summary>
    /// 用途：控制整个 QuickTurn 在按钮切页期间的透明度。
    /// 调用边界：必须绑定源 Prefab 根节点上的 CanvasGroup，不用于拖动即时换页。
    /// </summary>
    [SerializeField] private CanvasGroup quickTurnCanvasGroup;

    #endregion

    #region Runtime State

    private int _max;
    private int _interval;
    private int _groupCount;
    private int _currentPageIndex;
    private int _previewPageIndex;
    private Vector2 _minHandlePosition;
    private Vector2 _maxHandlePosition;
    private Vector2 _movementDirection;
    private float _movementLength;
    private Vector2 _pageTextOffset;
    private float _pointerOffset;
    private bool _isInitialized;
    private bool _isInteractable;
    private bool _isDragging;
    private Tween _canvasGroupAlphaTween;
    private Func<int> _getCurrentPageIndex;
    private Action<int> _onPageChanged;
    private Action<int> _onPageSelected;

    #endregion

    #region Public API

    /// <summary>
    /// 用途：按最大条目数量和每组数量建立快速分页，并同步当前组。
    /// 调用边界：由所属选关面板初始化；拖动回调请求父面板立即刷新，松手回调用于最终状态对齐。
    /// 注意事项：max 小于等于零时显示 0-0；interval 非正数会记录错误并按 1 处理。
    /// </summary>
    /// <param name="max">参与分页的最大条目数量。</param>
    /// <param name="interval">每个区间包含的条目数量。</param>
    /// <param name="currentPageIndex">所属面板当前页索引。</param>
    /// <param name="getCurrentPageIndex">每次按下或开始拖动时读取所属面板权威页码的回调。</param>
    /// <param name="onPageChanged">拖动跨过分页阈值时请求所属面板立即刷新当前页的回调。</param>
    /// <param name="onPageSelected">松手后的状态确认回调；当前不负责播放切页表现。</param>
    public void Initialize(int max, int interval, int currentPageIndex, Func<int> getCurrentPageIndex,
        Action<int> onPageChanged, Action<int> onPageSelected)
    {
        _max = Mathf.Max(0, max);
        _interval = interval;
        if (_interval <= 0)
        {
            this.LogError($"[LevelChoicePanel_QuickTurn] interval must be greater than zero. value:{interval}");
            _interval = 1;
        }

        _groupCount = _max <= 0 ? 0 : Mathf.CeilToInt(_max / (float)_interval);
        _getCurrentPageIndex = getCurrentPageIndex;
        _onPageChanged = onPageChanged;
        _onPageSelected = onPageSelected;
        _isInitialized = ValidateInspectorReferences();
        _isInteractable = _isInitialized && _groupCount > 1;

        if (!_isInitialized)
            return;

        _pageTextOffset = UiXuanguan21Txt.rectTransform.anchoredPosition -
                          UIXuanguanKsfybtn01Img.rectTransform.anchoredPosition;
        SetCurrentPage(currentPageIndex);
    }

    /// <summary>
    /// 用途：把 handle 与区间文本同步到所属面板已确认的页索引。
    /// 调用边界：初始化、按钮翻页、拖动松手确认、远端刷新或面板重新打开时调用。
    /// 注意事项：只刷新 UI，不触发切页回调。
    /// </summary>
    /// <param name="pageIndex">所属面板已确认的页索引。</param>
    public void SetCurrentPage(int pageIndex)
    {
        if (!_isInitialized || !ValidateInspectorReferences())
            return;

        _currentPageIndex = ClampPageIndex(pageIndex);
        _previewPageIndex = _currentPageIndex;
        RefreshHandleRange();
        SetHandlePosition(GetHandlePosition(_currentPageIndex));
        RefreshPageText(_currentPageIndex);
    }

    /// <summary>
    /// 用途：按所属面板 LevelItem 的切页节奏设置整个 QuickTurn 的透明度。
    /// 调用边界：仅供左右按钮的离散切页表现调用；拖动即时换页不应调用。
    /// 注意事项：新调用会终止尚未完成的旧透明度 Tween，Tween 不受 Time.timeScale 影响。
    /// </summary>
    /// <param name="value">目标透明度，自动限制在 0 到 1。</param>
    /// <param name="duration">渐变时长；非正数时立即赋值。</param>
    public void SetCanvasGroupAlpha(float value, float duration = 0f)
    {
        if (quickTurnCanvasGroup == null)
            return;

        _canvasGroupAlphaTween?.Kill();
        _canvasGroupAlphaTween = null;
        value = Mathf.Clamp01(value);

        if (duration <= 0f)
        {
            quickTurnCanvasGroup.alpha = value;
            return;
        }

        _canvasGroupAlphaTween = quickTurnCanvasGroup
            .DOFade(value, duration)
            .SetUpdate(true);
    }

    /// <summary>
    /// 用途：在所属面板的切页动画或业务门禁期间关闭拖动输入，结束后再恢复。
    /// 调用边界：由主线或 Draw 选关面板的分页生命周期调用。
    /// </summary>
    /// <param name="interactable">是否允许开始或继续拖动。</param>
    public void SetInteractable(bool interactable)
    {
        _isInteractable = interactable && _groupCount > 1;
        if (_isInteractable || !_isDragging)
            return;

        _isDragging = false;
        SetCurrentPage(_currentPageIndex);
    }

    #endregion

    #region Drag Input

    /// <summary>
    /// 用途：按下 handle 或轨道时立即以所属面板当前页校正 Comp 状态，避免沿用默认第 0 页。
    /// 调用边界：只同步显示和拖动基准，不触发父面板切页。
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_isInitialized || !_isInteractable)
            return;

        SyncCurrentPageFromOwner();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_isInitialized || !_isInteractable)
            return;

        SyncCurrentPageFromOwner();
        if (!TryGetPointerLocalPosition(eventData, out Vector2 pointerPosition))
            return;

        RefreshHandleRange();
        _isDragging = true;
        float handleDistance = GetMovementDistance(UIXuanguanKsfybtn01Img.rectTransform.anchoredPosition);
        _pointerOffset = handleDistance - GetMovementDistance(pointerPosition);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging || !_isInteractable ||
            !TryGetPointerLocalPosition(eventData, out Vector2 pointerPosition))
            return;

        float movementDistance = Mathf.Clamp(
            GetMovementDistance(pointerPosition) + _pointerOffset,
            0f,
            _movementLength);
        SetHandlePosition(_minHandlePosition + _movementDirection * movementDistance);

        int targetPageIndex = GetPageIndex(movementDistance);
        if (_previewPageIndex == targetPageIndex)
            return;

        _previewPageIndex = targetPageIndex;
        RefreshPageText(_previewPageIndex);
        _onPageChanged?.Invoke(_previewPageIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        int targetPageIndex = _previewPageIndex;
        if (targetPageIndex == _currentPageIndex || _onPageSelected == null)
        {
            SetCurrentPage(_currentPageIndex);
            return;
        }

        _onPageSelected.Invoke(targetPageIndex);
    }

    private void OnDisable()
    {
        _isDragging = false;
        _canvasGroupAlphaTween?.Kill();
        _canvasGroupAlphaTween = null;
        if (quickTurnCanvasGroup != null)
            quickTurnCanvasGroup.alpha = 1f;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!_isInitialized || _isDragging)
            return;

        SetCurrentPage(_currentPageIndex);
    }

    #endregion

    #region Presentation

    private void SyncCurrentPageFromOwner()
    {
        if (_getCurrentPageIndex == null)
            return;

        SetCurrentPage(_getCurrentPageIndex.Invoke());
    }

    private bool ValidateInspectorReferences()
    {
        bool isValid = quickTurnCanvasGroup != null && UiXuanguan21Txt != null &&
                       UIXuanguanKsfybg01Img != null && UIXuanguanKsfybtn01Img != null;
        if (!isValid)
            this.LogError("[LevelChoicePanel_QuickTurn] Required Inspector references are missing.");

        return isValid;
    }

    private void RefreshHandleRange()
    {
        RectTransform track = UIXuanguanKsfybg01Img.rectTransform;
        RectTransform handle = UIXuanguanKsfybtn01Img.rectTransform;
        RectTransform dragSpace = track.parent as RectTransform;
        if (dragSpace == null)
        {
            _minHandlePosition = handle.anchoredPosition;
            _maxHandlePosition = handle.anchoredPosition;
            _movementDirection = Vector2.right;
            _movementLength = 0f;
            return;
        }

        float minTrackX = track.rect.xMin + handle.rect.width * handle.pivot.x;
        float maxTrackX = track.rect.xMax - handle.rect.width * (1f - handle.pivot.x);
        float trackCenterY = track.rect.center.y;
        if (minTrackX > maxTrackX)
        {
            minTrackX = track.rect.center.x;
            maxTrackX = track.rect.center.x;
        }

        // 从轨道局部中心线生成父坐标系端点，确保 handle 跟随当前 1.5° 斜轨而不是固定 Y。
        _minHandlePosition = dragSpace.InverseTransformPoint(
            track.TransformPoint(new Vector3(minTrackX, trackCenterY, 0f)));
        _maxHandlePosition = dragSpace.InverseTransformPoint(
            track.TransformPoint(new Vector3(maxTrackX, trackCenterY, 0f)));

        Vector2 movement = _maxHandlePosition - _minHandlePosition;
        _movementLength = movement.magnitude;
        if (_movementLength > Mathf.Epsilon)
        {
            _movementDirection = movement / _movementLength;
            return;
        }

        Vector2 trackCenter = dragSpace.InverseTransformPoint(
            track.TransformPoint(new Vector3(track.rect.center.x, trackCenterY, 0f)));
        _minHandlePosition = trackCenter;
        _maxHandlePosition = trackCenter;
        _movementDirection = Vector2.right;
        _movementLength = 0f;
    }

    private bool TryGetPointerLocalPosition(PointerEventData eventData, out Vector2 localPosition)
    {
        localPosition = Vector2.zero;
        RectTransform dragSpace = UIXuanguanKsfybg01Img.rectTransform.parent as RectTransform;
        if (dragSpace == null)
            return false;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragSpace,
            eventData.position,
            eventData.pressEventCamera,
            out localPosition);
    }

    private int ClampPageIndex(int pageIndex)
    {
        return _groupCount <= 0 ? 0 : Mathf.Clamp(pageIndex, 0, _groupCount - 1);
    }

    private Vector2 GetHandlePosition(int pageIndex)
    {
        if (_groupCount <= 1)
            return Vector2.Lerp(_minHandlePosition, _maxHandlePosition, 0.5f);

        float progress = ClampPageIndex(pageIndex) / (float)(_groupCount - 1);
        return Vector2.Lerp(_minHandlePosition, _maxHandlePosition, progress);
    }

    private int GetPageIndex(float movementDistance)
    {
        if (_groupCount <= 1 || _movementLength <= Mathf.Epsilon)
            return 0;

        float progress = Mathf.Clamp01(movementDistance / _movementLength);
        return ClampPageIndex(Mathf.RoundToInt(progress * (_groupCount - 1)));
    }

    private float GetMovementDistance(Vector2 localPosition)
    {
        return Vector2.Dot(localPosition - _minHandlePosition, _movementDirection);
    }

    private void SetHandlePosition(Vector2 handlePosition)
    {
        RectTransform handle = UIXuanguanKsfybtn01Img.rectTransform;
        handle.anchoredPosition = handlePosition;

        RectTransform pageText = UiXuanguan21Txt.rectTransform;
        pageText.anchoredPosition = handlePosition + _pageTextOffset;
    }

    private void RefreshPageText(int pageIndex)
    {
        if (_max <= 0)
        {
            UiXuanguan21Txt.text = "0-0";
            return;
        }

        int start = ClampPageIndex(pageIndex) * _interval + 1;
        int end = Mathf.Min(start + _interval - 1, _max);
        UiXuanguan21Txt.text = $"{start}-{end}";
    }

    #endregion
}

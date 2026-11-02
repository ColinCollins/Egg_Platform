using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// ScrollRect-based horizontal page view with snap and optional navigation buttons.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScrollRect))]
public class PageView : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;

    [Header("Navigation")]
    [SerializeField] private Button[] navigationButtons;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;

    [Header("Snap")]
    [SerializeField] private float snapDuration = 0.25f;
    [SerializeField] private float dragThresholdNormalized = 0.15f;
    [SerializeField] private float velocityThreshold = 800f;
    [SerializeField] private bool lockInputWhileSnapping = true;

    private Tween _snapTween;
    private bool _isDragging;
    private int _pageCount;
    private float _dragStartProgress;
    private int _dragStartPageIndex;
    private UnityAction[] _navigationButtonClickHandlers;

    /// <summary>Raised when the selected page index changes.</summary>
    public event Action<int, int> OnPageChanged;

    /// <summary>Current logical page index.</summary>
    public int CurrentPageIndex { get; private set; }

    /// <summary>Total direct-child page count under Content.</summary>
    public int PageCount => _pageCount;

    /// <summary>Whether a snap tween is currently driving the scroll position.</summary>
    public bool IsSnapping => _snapTween != null && _snapTween.IsActive() && _snapTween.IsPlaying();

    /// <summary>Current horizontal scroll progress in [0, 1].</summary>
    public float ScrollProgress => GetScrollProgress();

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        RefreshPagesInternal(false, true);
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }

        if (prevButton != null)
        {
            prevButton.onClick.AddListener(PrevPage);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NextPage);
        }

        RegisterNavigationButtonListeners();
        RefreshPagesInternal(false, true);
    }

    private void OnDisable()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
        }

        if (prevButton != null)
        {
            prevButton.onClick.RemoveListener(PrevPage);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(NextPage);
        }

        UnregisterNavigationButtonListeners();
        KillSnapTween();
        _isDragging = false;
    }

    private void OnDestroy()
    {
        KillSnapTween();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();

        snapDuration = Mathf.Max(0f, snapDuration);
        dragThresholdNormalized = Mathf.Clamp01(dragThresholdNormalized);
        velocityThreshold = Mathf.Max(0f, velocityThreshold);

        if (!Application.isPlaying)
        {
            ClampNavigationButtons();
        }
    }
#endif

    /// <summary>
    /// Rebuilds the page count from Content children and snaps back to the current page.
    /// </summary>
    [ContextMenu("Refresh Pages")]
    public void RefreshPages()
    {
        RefreshPagesInternal(false, false);
    }

    /// <summary>
    /// Jumps or animates to the target page index.
    /// </summary>
    public void SetPage(int index, bool animated = true)
    {
        if (_pageCount <= 0)
        {
            CurrentPageIndex = 0;
            UpdateNavigationButtons();
            return;
        }

        if (lockInputWhileSnapping && IsSnapping)
        {
            return;
        }

        int targetIndex = Mathf.Clamp(index, 0, _pageCount - 1);
        float targetProgress = GetProgressForPage(targetIndex);

        ChangeCurrentPage(targetIndex);
        MoveToProgress(targetProgress, animated);
    }

    /// <summary>
    /// Moves to the previous page if one exists.
    /// </summary>
    public void PrevPage()
    {
        if (_isDragging || (lockInputWhileSnapping && IsSnapping))
        {
            return;
        }

        SetPage(CurrentPageIndex - 1);
    }

    /// <summary>
    /// Moves to the next page if one exists.
    /// </summary>
    public void NextPage()
    {
        if (_isDragging || (lockInputWhileSnapping && IsSnapping))
        {
            return;
        }

        SetPage(CurrentPageIndex + 1);
    }

    /// <summary>
    /// Records drag start state and interrupts any running snap.
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!enabled || scrollRect == null || !scrollRect.horizontal)
        {
            return;
        }

        KillSnapTween();
        _isDragging = true;
        _dragStartProgress = GetScrollProgress();
        _dragStartPageIndex = GetNearestPageIndex(_dragStartProgress);
    }

    /// <summary>
    /// Resolves the target page after dragging based on progress delta and release velocity.
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!enabled || scrollRect == null || !scrollRect.horizontal)
        {
            return;
        }

        _isDragging = false;

        if (_pageCount <= 0)
        {
            return;
        }

        float currentProgress = GetScrollProgress();
        float dragDelta = currentProgress - _dragStartProgress;
        int targetIndex = ResolveTargetPageIndex(currentProgress, dragDelta, scrollRect.velocity.x);
        SetPage(targetIndex, true);
    }

    private void ResolveReferences()
    {
        if (scrollRect == null)
        {
            scrollRect = GetComponent<ScrollRect>();
        }

        if (scrollRect == null)
        {
            return;
        }

        if (viewport == null)
        {
            viewport = scrollRect.viewport;
        }

        if (content == null)
        {
            content = scrollRect.content;
        }
    }

    private void RefreshPagesInternal(bool animated, bool silent)
    {
        ResolveReferences();
        ClampNavigationButtons();

        if (content == null)
        {
            _pageCount = 0;
            CurrentPageIndex = 0;
            UpdateNavigationButtons();
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        _pageCount = content.childCount;

        if (_pageCount <= 0)
        {
            CurrentPageIndex = 0;
            UpdateNavigationButtons();
            return;
        }

        int clampedIndex = Mathf.Clamp(CurrentPageIndex, 0, _pageCount - 1);
        if (!silent)
        {
            ChangeCurrentPage(clampedIndex);
        }
        else
        {
            CurrentPageIndex = clampedIndex;
            UpdateNavigationButtons();
        }

        MoveToProgress(GetProgressForPage(CurrentPageIndex), animated);
    }

    private void MoveToProgress(float targetProgress, bool animated)
    {
        KillSnapTween();

        if (scrollRect == null)
        {
            return;
        }

        if (!animated || !Application.isPlaying || snapDuration <= 0f)
        {
            ApplyScrollProgress(targetProgress);
            return;
        }

        float startProgress = GetScrollProgress();
        _snapTween = DOVirtual.Float(startProgress, targetProgress, snapDuration, ApplyScrollProgress)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                ApplyScrollProgress(targetProgress);
                _snapTween = null;
            })
            .OnKill(() => _snapTween = null);
    }

    private void ApplyScrollProgress(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);

        if (scrollRect != null)
        {
            scrollRect.horizontalNormalizedPosition = clampedProgress;
        }
    }

    private void OnScrollValueChanged(Vector2 _)
    {
        // Drag/snap visuals are driven by the ScrollRect itself. Page selection is committed by SetPage.
    }

    private void RegisterNavigationButtonListeners()
    {
        if (navigationButtons == null)
        {
            return;
        }

        _navigationButtonClickHandlers = new UnityAction[navigationButtons.Length];

        for (int i = 0; i < navigationButtons.Length; i++)
        {
            Button button = navigationButtons[i];
            if (button == null)
            {
                continue;
            }

            int pageIndex = i;
            UnityAction clickHandler = () => SetPage(pageIndex);
            _navigationButtonClickHandlers[i] = clickHandler;
            button.onClick.AddListener(clickHandler);
        }
    }

    private void UnregisterNavigationButtonListeners()
    {
        if (navigationButtons == null || _navigationButtonClickHandlers == null)
        {
            return;
        }

        for (int i = 0; i < navigationButtons.Length; i++)
        {
            Button button = navigationButtons[i];
            if (button == null)
            {
                continue;
            }

            UnityAction clickHandler = i < _navigationButtonClickHandlers.Length ? _navigationButtonClickHandlers[i] : null;
            if (clickHandler != null)
            {
                button.onClick.RemoveListener(clickHandler);
            }
        }

        _navigationButtonClickHandlers = null;
    }

    private void ClampNavigationButtons()
    {
        if (navigationButtons == null)
        {
            return;
        }

        for (int i = 0; i < navigationButtons.Length; i++)
        {
            if (navigationButtons[i] == null)
            {
                continue;
            }

            navigationButtons[i].interactable = i < _pageCount && i != CurrentPageIndex;
        }
    }

    private int ResolveTargetPageIndex(float currentProgress, float dragDelta, float releaseVelocityX)
    {
        if (_pageCount <= 1)
        {
            return 0;
        }

        if (Mathf.Abs(dragDelta) >= dragThresholdNormalized)
        {
            return Mathf.Clamp(_dragStartPageIndex + (dragDelta > 0f ? 1 : -1), 0, _pageCount - 1);
        }

        if (Mathf.Abs(releaseVelocityX) >= velocityThreshold)
        {
            int direction = releaseVelocityX < 0f ? 1 : -1;
            return Mathf.Clamp(_dragStartPageIndex + direction, 0, _pageCount - 1);
        }

        return GetNearestPageIndex(currentProgress);
    }

    private int GetNearestPageIndex(float progress)
    {
        if (_pageCount <= 1)
        {
            return 0;
        }

        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(progress) * (_pageCount - 1)), 0, _pageCount - 1);
    }

    private float GetProgressForPage(int pageIndex)
    {
        if (_pageCount <= 1)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)pageIndex / (_pageCount - 1));
    }

    private float GetScrollProgress()
    {
        if (scrollRect == null || _pageCount <= 1)
        {
            return 0f;
        }

        return Mathf.Clamp01(scrollRect.horizontalNormalizedPosition);
    }

    private void ChangeCurrentPage(int targetIndex)
    {
        int clampedIndex = Mathf.Clamp(targetIndex, 0, Mathf.Max(0, _pageCount - 1));
        if (clampedIndex == CurrentPageIndex)
        {
            UpdateNavigationButtons();
            return;
        }

        int previousIndex = CurrentPageIndex;
        CurrentPageIndex = clampedIndex;
        UpdateNavigationButtons();
        OnPageChanged?.Invoke(previousIndex, CurrentPageIndex);
    }

    private void UpdateNavigationButtons()
    {
        ClampNavigationButtons();

        if (prevButton != null)
        {
            prevButton.interactable = CurrentPageIndex > 0;
        }

        if (nextButton != null)
        {
            nextButton.interactable = CurrentPageIndex < _pageCount - 1;
        }
    }

    private void KillSnapTween()
    {
        if (_snapTween == null)
        {
            return;
        }

        _snapTween.Kill();
        _snapTween = null;
    }
}

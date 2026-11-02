using System;
using System.Collections.Generic;
using Bear.Logger;
using UnityEngine;

/// <summary>
/// verison 1.0 
/// 按照 index 开启对应 items 的点击功能。
/// 参考 PageItem 的代码，可以触发对应 PageItem 的 TryUnlockItem 逻辑
/// 注意：items 和 PageItem 持有的 items 是一一对应的。但是是不同的对象。
/// 所有的 items 都解锁后，关闭当前组件。
/// PostcardPopup 持有该组件，在 isGuiding = true 的状态下会触发显示，该组件显示之后 IsGuding 就可以设置为 false 了。
/// 
/// finger 组件会根据 index 的获取的 item 位置，进行 anchorPosition 调整，移动到对应 item 中心位置。
/// </summary>
public class Postcard_GuidePart : MonoBehaviour, IDebuger
{
    private const int InvalidIndex = -1;

    [SerializeField] private List<Postcard_ProgressItem> items;
    [SerializeField] private Postcard_PageItem pageItem;

    private Action _onCompleted;
    private readonly List<Postcard_ProgressItem> _runtimeGuideItems = new List<Postcard_ProgressItem>();
    private Postcard_PageItem _runtimePageItem;
    private GuideFinger _runtimeGuideFinger;
    private int index = InvalidIndex;
    private bool _isUnlocking;
    private int _unlockingIndex = InvalidIndex;
    private bool _isPageUnlockStable;
    private bool _isGuideUnlockVisualStable;

    #region Public API

    /// <summary>
    /// 用途：在明信片弹窗打开并确认处于引导状态后启动本组件的局部引导流程。
    /// 调用边界：仅由 `PostcardPopup` 在当前页面 `OnShow()` 完成后调用，传入当前真实页面实例。
    /// 注意事项：该方法不写入存档、不扣星、不加载资源；解锁仍通过 `Postcard_PageItem` 的引导入口完成。
    /// </summary>
    /// <param name="currentPageItem">当前弹窗中正在显示的真实明信片页面实例。</param>
    /// <param name="onCompleted">所有可引导目标结束后通知弹窗的回调，可为空。</param>
    /// <returns>找到可引导的锁定目标并显示组件时返回 true；无有效目标时返回 false。</returns>
    public bool TryStartGuide(Postcard_PageItem currentPageItem, Action onCompleted)
    {
        if (!Application.isPlaying)
            return false;

        StopGuideRuntime();
        _runtimePageItem = currentPageItem != null ? currentPageItem : pageItem;
        _onCompleted = onCompleted;
        ResolveGuideItems();

        if (_runtimePageItem == null)
        {
            this.LogWarning("[Postcard_GuidePart] Cannot start guide because page item is null.");
            HideSelf();
            return false;
        }

        if (_runtimeGuideItems.Count <= 0)
        {
            this.LogWarning("[Postcard_GuidePart] Cannot start guide because guide items are empty.");
            HideSelf();
            return false;
        }

        RefreshGuideItemVisuals();
        _runtimePageItem.SetGuideInteractionLocked(true);
        gameObject.SetActive(true);

        if (MoveToNextTarget(0))
            return true;

        CompleteGuide();
        return false;
    }

    /// <summary>
    /// 用途：由明信片弹窗切页、关闭或失去当前页面时主动停止引导并释放点击锁。
    /// 调用边界：仅由持有本组件的 `PostcardPopup` 或本组件生命周期清理路径调用。
    /// 注意事项：该方法不会调用完成回调，避免切页或关闭时误触发完成流程。
    /// </summary>
    public void StopGuide()
    {
        StopGuideRuntime();
        _onCompleted = null;
        HideSelf();
    }

    #endregion

    #region Unity Lifecycle

    private void OnDisable()
    {
        StopGuideRuntime();
        _onCompleted = null;
    }

    private void OnDestroy()
    {
        StopGuideRuntime();
        _onCompleted = null;
    }

    #endregion

    #region Guide Flow

    private bool MoveToNextTarget(int startIndex)
    {
        int safeStartIndex = Mathf.Max(0, startIndex);
        for (int i = safeStartIndex; i < _runtimeGuideItems.Count; i++)
        {
            if (!IsValidTargetPair(i))
                continue;

            if (!TryRefreshGuideItemVisual(i, out bool isUnlocked))
                continue;

            if (isUnlocked)
                continue;

            SetActiveIndex(i);
            return true;
        }

        return false;
    }

    private void SetActiveIndex(int nextIndex)
    {
        index = nextIndex;
        ResetUnlockingState();
        DisableGuideItemButtons();
        RefreshFingerPosition();
    }

    private void OnGuideFingerClick()
    {
        _runtimeGuideFinger = null;
        if (_isUnlocking)
            return;

        if (index == InvalidIndex || !IsValidTargetPair(index))
            return;

        _isUnlocking = true;
        _unlockingIndex = index;
        _isPageUnlockStable = false;
        _isGuideUnlockVisualStable = false;
        DisableGuideItemButtons();

        bool accepted = _runtimePageItem.TryUnlockGuidedItemByListIndex(index, OnGuidedUnlockStable);
        if (accepted)
        {
            PlayGuideUnlockAnim(_unlockingIndex);
            return;
        }

        ResetUnlockingState();
        RefreshGuideItemVisual(index);
        DisableGuideItemButtons();
        RefreshFingerPosition();
    }

    private void OnGuidedUnlockStable(bool accepted)
    {
        if (_runtimePageItem == null || index == InvalidIndex || !gameObject.activeInHierarchy)
        {
            ResetUnlockingState();
            return;
        }

        if (!accepted)
        {
            ResetUnlockingState();
            RefreshGuideItemVisual(index);
            DisableGuideItemButtons();
            RefreshFingerPosition();
            return;
        }

        _isPageUnlockStable = true;
        TryAdvanceAfterGuidedUnlock();
    }

    private void CompleteGuide()
    {
        Action onCompleted = _onCompleted;
        StopGuideRuntime();
        _onCompleted = null;
        HideSelf();
        onCompleted?.Invoke();
    }

    #endregion

    #region Guide Visuals

    private void PlayGuideUnlockAnim(int guideIndex)
    {
        if (!TryGetGuideItem(guideIndex, out Postcard_ProgressItem guideItem))
        {
            OnGuideUnlockVisualStable(guideIndex);
            return;
        }

        guideItem.PlayUnlockAnim(() => OnGuideUnlockVisualStable(guideIndex));
    }

    private void OnGuideUnlockVisualStable(int guideIndex)
    {
        if (guideIndex != _unlockingIndex)
            return;

        _isGuideUnlockVisualStable = true;
        TryAdvanceAfterGuidedUnlock();
    }

    private void RefreshGuideItemVisuals()
    {
        for (int i = 0; i < _runtimeGuideItems.Count; i++)
            RefreshGuideItemVisual(i);
    }

    private void RefreshGuideItemVisual(int guideIndex)
    {
        TryRefreshGuideItemVisual(guideIndex, out _);
    }

    private bool TryRefreshGuideItemVisual(int guideIndex, out bool isUnlock)
    {
        isUnlock = false;

        if (!TryGetGuideItem(guideIndex, out Postcard_ProgressItem guideItem))
            return false;

        if (_runtimePageItem == null
            || !_runtimePageItem.TryGetGuideProgressViewStateByListIndex(
                guideIndex,
                out isUnlock,
                out int unlockCount,
                out int currentStar))
        {
            return false;
        }

        guideItem.Refresh(isUnlock, unlockCount, currentStar);
        return true;
    }

    private void TryAdvanceAfterGuidedUnlock()
    {
        if (!_isUnlocking || !_isPageUnlockStable || !_isGuideUnlockVisualStable)
            return;

        int nextIndex = _unlockingIndex + 1;
        ResetUnlockingState();
        if (!MoveToNextTarget(nextIndex))
            CompleteGuide();
    }

    private void ResetUnlockingState()
    {
        _isUnlocking = false;
        _unlockingIndex = InvalidIndex;
        _isPageUnlockStable = false;
        _isGuideUnlockVisualStable = false;
    }

    #endregion

    #region Button State

    private void DisableGuideItemButtons()
    {
        for (int i = 0; i < _runtimeGuideItems.Count; i++)
        {
            CustomButton clickBtn = GetGuideButton(i);
            if (clickBtn == null)
                continue;

            clickBtn.Interactable = false;
        }
    }

    #endregion

    #region Finger

    private void RefreshFingerPosition()
    {
        CloseFinger();
        if (!TryGetGuideRect(index, out RectTransform targetRect))
            return;

        _runtimeGuideFinger = GuideFinger.Create(
            targetRect,
            transform as RectTransform,
            OnGuideFingerClick);
        if (_runtimeGuideFinger == null)
            this.LogWarning("[Postcard_GuidePart] Cannot create guide finger for the active target.");
    }

    private void CloseFinger()
    {
        if (_runtimeGuideFinger == null)
            return;

        GuideFinger currentGuideFinger = _runtimeGuideFinger;
        _runtimeGuideFinger = null;
        currentGuideFinger.Close();
    }

    #endregion

    #region Runtime Helpers

    private void ResolveGuideItems()
    {
        _runtimeGuideItems.Clear();

        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && !_runtimeGuideItems.Contains(items[i]))
                    _runtimeGuideItems.Add(items[i]);
            }
        }

        if (_runtimeGuideItems.Count > 0)
            return;

        Postcard_ProgressItem[] foundItems = GetComponentsInChildren<Postcard_ProgressItem>(true);
        for (int i = 0; i < foundItems.Length; i++)
        {
            if (foundItems[i] != null && !_runtimeGuideItems.Contains(foundItems[i]))
                _runtimeGuideItems.Add(foundItems[i]);
        }
    }

    private bool IsValidTargetPair(int targetIndex)
    {
        if (_runtimePageItem == null)
            return false;

        if (targetIndex < 0 || targetIndex >= _runtimeGuideItems.Count)
            return false;

        Postcard_ProgressItem guideItem = _runtimeGuideItems[targetIndex];
        if (guideItem == null)
            return false;

        return _runtimePageItem.TryGetRuntimeItemByListIndex(targetIndex, out Postcard_ProgressItem pageTarget)
            && pageTarget != null;
    }

    private CustomButton GetGuideButton(int targetIndex)
    {
        return TryGetGuideItem(targetIndex, out Postcard_ProgressItem guideItem)
            ? guideItem.ClickBtn
            : null;
    }

    private bool TryGetGuideItem(int targetIndex, out Postcard_ProgressItem guideItem)
    {
        guideItem = null;

        if (targetIndex < 0 || targetIndex >= _runtimeGuideItems.Count)
            return false;

        guideItem = _runtimeGuideItems[targetIndex];
        return guideItem != null;
    }

    private bool TryGetGuideRect(int targetIndex, out RectTransform targetRect)
    {
        targetRect = null;
        if (targetIndex < 0 || targetIndex >= _runtimeGuideItems.Count)
            return false;

        targetRect = _runtimeGuideItems[targetIndex] == null
            ? null
            : _runtimeGuideItems[targetIndex].transform as RectTransform;

        return targetRect != null;
    }

    private void StopGuideRuntime()
    {
        DisableGuideItemButtons();
        if (_runtimePageItem != null)
            _runtimePageItem.SetGuideInteractionLocked(false);

        index = InvalidIndex;
        ResetUnlockingState();
        _runtimePageItem = null;
        CloseFinger();
    }

    private void HideSelf()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    #endregion
}

using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 明信片弹窗提示气泡表现组件，负责首次展示、按钮重显、点击关闭和超时关闭。
/// </summary>
public sealed class PostcardPopupTipsBubble : MonoBehaviour, IPointerClickHandler
{
    #region Constants

    private const float ExpandDuration = 0.3f;
    private const float DisplayDuration = 3f;
    private const float CloseDuration = 0.2f;

    #endregion

    #region Serialized Fields

    /// <summary>
    /// 用途：首次提示触发后供玩家主动重新展示气泡的按钮。
    /// 调用边界：由 PostcardPopup prefab 显式绑定，不负责首次自动展示条件判断。
    /// 注意事项：首次提示尚未触发或气泡正在展示/关闭时会被设为不可交互。
    /// </summary>
    [SerializeField] private CustomButton replayButton;

    #endregion

    #region Runtime State

    private Sequence _displaySequence;
    private Vector3 _shownScale;
    private bool _hasCachedShownScale;
    private bool _isShown;
    private bool _isClosing;
    private bool _isReplayEnabled;
    private bool _isReplayButtonBound;

    #endregion

    #region Public API

    /// <summary>
    /// 用途：从零缩放以 OutBounce 展开气泡，并在完整展示 3 秒后自动缩放关闭。
    /// 调用边界：由运行态 `PostcardPopup` 的一次性门槛，或已开放的重显按钮在气泡隐藏后调用。
    /// 注意事项：本组件只控制本地 UI 表现，不读取或写入 DB；重复调用会重置当前表现计时。
    /// </summary>
    /// <returns>成功启动展示时返回 true；非运行态或父节点未激活时返回 false。</returns>
    public bool TryShow()
    {
        if (!Application.isPlaying || (transform.parent != null && !transform.parent.gameObject.activeInHierarchy))
            return false;

        CacheShownScale();
        StopDisplaySequence();

        _isShown = true;
        _isClosing = false;
        RefreshReplayButtonInteractable();
        transform.localScale = Vector3.zero;
        gameObject.SetActive(true);

        _displaySequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);
        _displaySequence.Append(transform
            .DOScale(_shownScale, ExpandDuration)
            .SetEase(Ease.OutBounce));
        _displaySequence.AppendInterval(DisplayDuration);
        _displaySequence.AppendCallback(() => _isClosing = true);
        _displaySequence.Append(transform.DOScale(Vector3.zero, CloseDuration));
        _displaySequence.OnComplete(CompleteHide);
        return true;
    }

    /// <summary>
    /// 用途：同步 CustomButton 的重显资格；首次提示已经成功触发后允许玩家再次打开气泡。
    /// 调用边界：仅由运行态 PostcardPopup 在打开刷新和首次提示启动成功后调用。
    /// 注意事项：此方法不展示气泡、不读写 DB；气泡展示或关闭过程中按钮仍保持不可交互。
    /// </summary>
    /// <param name="isEnabled">是否已经满足首次提示触发条件并允许按钮重显。</param>
    public void SetReplayEnabled(bool isEnabled)
    {
        if (!Application.isPlaying)
            return;

        _isReplayEnabled = isEnabled;
        EnsureReplayButtonBound();
        RefreshReplayButtonInteractable();
    }

    /// <summary>
    /// 用途：立即停止气泡 Tween 并恢复为隐藏状态，供 Popup 打开复位和关闭清理调用。
    /// 调用边界：可在气泡未展示或父 Popup 正在关闭时安全重复调用。
    /// 注意事项：不会播放关闭动画，也不会修改一次性展示存档。
    /// </summary>
    public void HideImmediately()
    {
        CacheShownScale();
        StopDisplaySequence();
        _isShown = false;
        _isClosing = false;
        gameObject.SetActive(false);
        transform.localScale = _shownScale;
    }

    /// <summary>
    /// 用途：接收气泡或其可射线子节点的点击，并播放缩放关闭表现。
    /// 调用边界：由 Unity EventSystem 在运行态派发，不应由业务代码直接模拟调用。
    /// 注意事项：关闭中或未展示时不会重复创建 Tween。
    /// </summary>
    /// <param name="eventData">当前指针点击事件数据。</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        CloseBubble();
    }

    #endregion

    #region Unity Lifecycle

    private void OnDisable()
    {
        StopDisplaySequence();
        _isShown = false;
        _isClosing = false;
        RefreshReplayButtonInteractable();
    }

    private void OnDestroy()
    {
        StopDisplaySequence();
        UnbindReplayButton();
    }

    #endregion

    #region Display Flow

    private void CloseBubble()
    {
        if (!_isShown || _isClosing)
            return;

        _isClosing = true;
        StopDisplaySequence();

        _displaySequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);
        _displaySequence.Append(transform.DOScale(Vector3.zero, CloseDuration));
        _displaySequence.OnComplete(CompleteHide);
    }

    private void CompleteHide()
    {
        _displaySequence = null;
        _isShown = false;
        _isClosing = false;
        gameObject.SetActive(false);
        transform.localScale = _shownScale;
        RefreshReplayButtonInteractable();
    }

    private void CacheShownScale()
    {
        if (_hasCachedShownScale)
            return;

        _shownScale = transform.localScale;
        _hasCachedShownScale = true;
    }

    private void StopDisplaySequence()
    {
        if (_displaySequence != null && _displaySequence.IsActive())
            _displaySequence.Kill();

        _displaySequence = null;
    }

    private void EnsureReplayButtonBound()
    {
        if (_isReplayButtonBound || replayButton == null)
            return;

        replayButton.OnClick += OnReplayButtonClick;
        _isReplayButtonBound = true;
    }

    private void UnbindReplayButton()
    {
        if (!_isReplayButtonBound || replayButton == null)
            return;

        replayButton.OnClick -= OnReplayButtonClick;
        _isReplayButtonBound = false;
    }

    private void RefreshReplayButtonInteractable()
    {
        if (replayButton == null)
            return;

        replayButton.Interactable = _isReplayEnabled && !_isShown && !_isClosing;
    }

    private void OnReplayButtonClick(CustomButton _)
    {
        if (!_isReplayEnabled || _isShown || _isClosing)
            return;

        TryShow();
    }

    #endregion
}

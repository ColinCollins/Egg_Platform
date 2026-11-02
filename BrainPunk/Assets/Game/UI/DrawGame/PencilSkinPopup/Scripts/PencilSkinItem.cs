using System;
using Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 绑定单个 PencilSkin 配置、笔刷预览图、点击回调和互斥状态部件。
/// 调用方负责解锁、扣费、广告和装备存档，本组件只维护当前 Item 的显示与点击路由。
/// </summary>
public sealed class PencilSkinItem : MonoBehaviour
{
    #region Constants

    private const float SelectedScaleMultiplier = 1.05f;

    #endregion

    /// <summary>
    /// PencilSkinItem 的显示状态。
    /// 只描述单个列表项的视觉状态，不负责解锁、扣费、广告或装备存档。
    /// </summary>
    public enum PencilSkinItemState
    {
        /// <summary>未绑定数据或需要隐藏全部状态部件。</summary>
        None = 0,

        /// <summary>已解锁但未选中的普通状态。</summary>
        Normal,

        /// <summary>当前已装备的选中状态。</summary>
        Selected,

        /// <summary>尚未解锁，需要观看广告的状态。</summary>
        AdUnlock,

        /// <summary>尚未解锁，需要消耗金币的状态。</summary>
        CoinUnlock,
    }

    #region Inspector Configuration

    [Header("状态")]
    [Tooltip("列表项启用时使用的初始显示状态；运行时由 SetState 更新。")]
    [SerializeField] private PencilSkinItemState state = PencilSkinItemState.Normal;

    [Tooltip("已解锁但未选中时显示的状态节点。")]
    [SerializeField] private GameObject normalPart;

    [Tooltip("当前笔刷已装备时显示的状态节点。")]
    [SerializeField] private GameObject selectedPart;

    [Tooltip("当前笔刷需要观看广告解锁时显示的状态节点。")]
    [SerializeField] private GameObject adUnlockPart;

    [Tooltip("当前笔刷需要消耗金币解锁时显示的状态节点。")]
    [SerializeField] private GameObject coinPart;

    [Tooltip("当前笔刷已装备时显示的勾选标记。")]
    [SerializeField] private GameObject selectedMark;

    [Header("数据绑定")]
    [Tooltip("笔刷主预览图；运行时会替换为 DrawBrushStyleAsset.PreviewSprite。")]
    [SerializeField] private Image mainImg;

    [Tooltip("金币解锁价格文本；只在 CoinUnlock 状态下写入 PencilSkin.CoinPrice。")]
    [SerializeField] private TextMeshProUGUI coinText;

    [Tooltip("当前列表项的点击区域；点击只会回调当前绑定的 PencilSkin 数据。")]
    [SerializeField] private CustomButton clickButton;

    #endregion

    #region Runtime State

    private PencilSkin data;
    private Action<PencilSkinItem> _clicked;
    private bool _isButtonBound;
    private Vector3 _defaultLocalScale;
    private bool _hasDefaultLocalScale;

    #endregion

    #region Public API

    /// <summary>
    /// 当前列表项绑定的 PencilSkin 配置；未绑定或清理后为空。
    /// </summary>
    public PencilSkin Data => data;

    /// <summary>
    /// 当前列表项的视觉状态；外部只读，不代表存档中的真实装备或解锁结果。
    /// </summary>
    public PencilSkinItemState State => state;

    /// <summary>
    /// 绑定笔刷配置、预览图、显示状态和点击回调。
    /// 调用边界：由 PencilSkinPopup 在读取两张配置表和 DrawGame 存档后调用；
    /// 本方法只刷新当前 Item，不执行扣费、广告、解锁或保存。
    /// </summary>
    /// <param name="skin">需要展示的 PencilSkin 配置行。</param>
    /// <param name="previewSprite">对应 DrawBrushStyleAsset 的 PreviewSprite。</param>
    /// <param name="newState">由 Popup 判定的当前显示状态。</param>
    /// <param name="clicked">点击当前绑定项时的回调。</param>
    /// <param name="interactable">当前配置是否允许响应点击。</param>
    public void Bind(
        PencilSkin skin,
        Sprite previewSprite,
        PencilSkinItemState newState,
        Action<PencilSkinItem> clicked,
        bool interactable)
    {
        BindButton();

        data = skin;
        _clicked = clicked;

        if (mainImg != null)
        {
            mainImg.sprite = previewSprite;
            mainImg.enabled = previewSprite != null;
        }

        if (coinText != null)
        {
            coinText.text = skin != null && skin.CoinPrice > 0
                ? skin.CoinPrice.ToString()
                : string.Empty;
        }

        SetState(skin != null ? newState : PencilSkinItemState.None);
        SetInteractable(skin != null && interactable);
    }

    /// <summary>
    /// 清理当前复用项的配置、预览图、状态和点击能力。
    /// 调用边界：仅用于 Popup 缩短列表或销毁前清理，不修改业务存档。
    /// </summary>
    public void Clear()
    {
        data = null;
        _clicked = null;

        if (mainImg != null)
        {
            mainImg.sprite = null;
            mainImg.enabled = false;
        }

        if (coinText != null)
        {
            coinText.text = string.Empty;
        }

        SetState(PencilSkinItemState.None);
        SetInteractable(false);
    }

    /// <summary>
    /// 切换列表项的互斥视觉状态。
    /// 调用边界：调用方必须先完成解锁方式、所有权和装备状态判断；本方法不修改任何业务数据。
    /// </summary>
    /// <param name="newState">需要显示的目标状态。</param>
    public void SetState(PencilSkinItemState newState)
    {
        state = newState;

        SetActive(normalPart, newState == PencilSkinItemState.Normal);
        SetActive(selectedPart, newState == PencilSkinItemState.Selected);
        SetActive(adUnlockPart, newState == PencilSkinItemState.AdUnlock);
        SetActive(coinPart, newState == PencilSkinItemState.CoinUnlock);
        SetActive(selectedMark, newState == PencilSkinItemState.Selected);
        ApplySelectedScale(newState == PencilSkinItemState.Selected);
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CaptureDefaultLocalScale();
        BindButton();
        SetState(state);
    }

    private void OnDestroy()
    {
        UnbindButton();
    }

    #endregion

    #region Private Helpers

    private void BindButton()
    {
        if (_isButtonBound || clickButton == null)
        {
            return;
        }

        clickButton.OnClick += OnButtonClicked;
        _isButtonBound = true;
    }

    private void UnbindButton()
    {
        if (!_isButtonBound)
        {
            return;
        }

        if (clickButton != null)
        {
            clickButton.OnClick -= OnButtonClicked;
        }

        _isButtonBound = false;
    }

    private void OnButtonClicked(CustomButton _)
    {
        if (data == null || clickButton == null || !clickButton.Interactable)
        {
            return;
        }

        _clicked?.Invoke(this);
    }

    private void SetInteractable(bool interactable)
    {
        if (clickButton != null)
        {
            clickButton.Interactable = interactable;
        }
    }

    private void CaptureDefaultLocalScale()
    {
        if (_hasDefaultLocalScale)
        {
            return;
        }

        _defaultLocalScale = transform.localScale;
        _hasDefaultLocalScale = true;
    }

    private void ApplySelectedScale(bool isSelected)
    {
        CaptureDefaultLocalScale();
        Vector3 targetScale = isSelected
            ? _defaultLocalScale * SelectedScaleMultiplier
            : _defaultLocalScale;

        if (transform.localScale != targetScale)
        {
            transform.localScale = targetScale;
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    #endregion
}

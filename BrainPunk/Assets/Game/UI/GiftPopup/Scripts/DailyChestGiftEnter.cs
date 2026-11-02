using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Game.Events;
using TMPro;
using UnityEngine;

/// <summary>
/// 每日 ChestGift 入口。
/// 只负责入口显隐、点击和主页资格确认；ChestGift 业务状态由 ChestGiftController 管理，弹窗互斥交由 GiftManager 管理。
/// </summary>
[DisallowMultipleComponent]
public sealed class DailyChestGiftEnter : MonoBehaviour, IDebuger
{
    #region Inspector

    /// <summary>
    /// 入口点击区域。应绑定当前 prefab 内的 clickArea/CustomButton；运行时缺失时会在子节点中回退查找。
    /// </summary>
    [Tooltip("绑定 clickArea 上的 CustomButton。")]
    [SerializeField] private CustomButton enterButton;

    /// <summary>
    /// 入口上的当地自然日倒计时文本。
    /// 调用边界：必须在 Prefab Inspector 直接绑定 countdown_txt，不在运行时搜索节点。
    /// </summary>
    [Tooltip("绑定入口上的 countdown_txt，显示到设备当地次日 00:00 的剩余时间。")]
    [SerializeField] private TMP_Text countdownText;

    /// <summary>
    /// 五档 ChestGift 对应的入口图标，数组索引 0～4 依次对应业务档位 1～5。
    /// 调用边界：必须在 Prefab Inspector 按红、蓝、绿、紫、橙顺序显式绑定，不在运行时搜索节点。
    /// </summary>
    [Tooltip("按红、蓝、绿、紫、橙顺序绑定五档 ChestGift 入口图标。")]
    [SerializeField] private GameObject[] chestIconsByVariant;

    #endregion

    #region 运行时状态

    private float _nextCountdownRefreshTime;
    private Dictionary<int, GameObject> _chestIconsByGiftIndex;
    private EventSubscriber _restoreSubscriber;

    #endregion

    #region 生命周期

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        BindRestoreListener();
        GiftManager.Instance.AvailabilityChanged -= OnAvailabilityChanged;
        GiftManager.Instance.AvailabilityChanged += OnAvailabilityChanged;
        Refresh();
    }

    private void OnDisable()
    {
        UnbindButton();
        // 入口因当日不可用而隐藏自身时仍需接收下一次 Daily 通知，以便重新激活。
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
            return;

        _restoreSubscriber?.Dispose();
        _restoreSubscriber = null;
        GiftManager.Instance.AvailabilityChanged -= OnAvailabilityChanged;
    }

    private void Update()
    {
        if (!Application.isPlaying || Time.unscaledTime < _nextCountdownRefreshTime)
            return;

        RefreshCountdown(force: false);
    }

    #endregion

    #region 对外入口

    /// <summary>
    /// 刷新普通 ChestGift 入口。
    /// 调用边界：父页面每次打开时调用；在主页使用时应改调 RefreshForHome，以正确消费首次主页资格。
    /// </summary>
    public void Refresh()
    {
        if (!Application.isPlaying)
            return;

        bool visible = GiftManager.Instance.IsEntryAvailable(GiftPopupType.ChestGift);
        if (gameObject.activeSelf != visible)
        {
            gameObject.SetActive(visible);
            return;
        }

        if (!visible)
        {
            UnbindButton();
            return;
        }

        BindButton();
        RefreshChestIcon();
        RefreshCountdown(force: true);
    }

    /// <summary>
    /// 主页打开时确认本次启动已经成功到达主页，再按业务资格刷新入口。
    /// 新存档首次到达主页时只落盘并保持隐藏；旧存档和后续启动可正常展示。
    /// </summary>
    public void RefreshForHome()
    {
        if (!Application.isPlaying)
            return;

        GiftManager.Instance.ChestGift.ConfirmHomeReached();
        Refresh();
    }

    #endregion

    #region 按钮与通知

    private void BindRestoreListener()
    {
        if (_restoreSubscriber != null)
            return;

        _restoreSubscriber = new EventSubscriber();
        _restoreSubscriber.Subscribe<OnRestoreSuccessEvent>(OnRestoreSuccess);
    }

    private void BindButton()
    {
        enterButton ??= GetComponentInChildren<CustomButton>(true);
        if (enterButton == null)
        {
            this.LogError("[DailyChestGiftEnter] CustomButton is missing; entry is hidden.");
            gameObject.SetActive(false);
            return;
        }

        enterButton.OnClick -= OnEnterClick;
        enterButton.OnClick += OnEnterClick;
        enterButton.Interactable = true;
    }

    private void UnbindButton()
    {
        if (enterButton != null)
            enterButton.OnClick -= OnEnterClick;
    }

    private void OnEnterClick(CustomButton _)
    {
        if (GiftManager.Instance.Open(GiftPopupType.ChestGift) != null)
            return;

        this.LogWarning("[DailyChestGiftEnter] No available ChestGift could be opened; refreshing entry state.");
        Refresh();
    }

    private void OnAvailabilityChanged(GiftPopupType popupType)
    {
        if (popupType == GiftPopupType.ChestGift)
            Refresh();
    }

    private void OnRestoreSuccess(OnRestoreSuccessEvent _)
    {
        Refresh();
    }

    #endregion

    #region 宝箱图标

    private void RefreshChestIcon()
    {
        EnsureChestIconLookup();

        GameObject activeIcon = null;
        bool hasCurrentChest = GiftManager.Instance.ChestGift.TryResolveCurrentIndex(out int giftIndex);
        if (hasCurrentChest && !_chestIconsByGiftIndex.TryGetValue(giftIndex, out activeIcon))
        {
            this.LogWarning(
                $"[DailyChestGiftEnter] Chest icon mapping is missing. giftIndex:{giftIndex}");
        }

        int iconCount = chestIconsByVariant?.Length ?? 0;
        for (int index = 0; index < iconCount; index++)
        {
            GameObject chestIcon = chestIconsByVariant[index];
            if (chestIcon != null)
                chestIcon.SetActive(chestIcon == activeIcon);
        }
    }

    /// <summary>
    /// 按当前五档商品顺序强制对应原入口 icon：1 绿、2 蓝、3 紫、4 橙、5 红。
    /// 只引用 Prefab 已绑定对象，不修改 Enter 层级、Transform 或图片资源。
    /// </summary>
    private void EnsureChestIconLookup()
    {
        if (_chestIconsByGiftIndex != null)
            return;

        _chestIconsByGiftIndex = new Dictionary<int, GameObject>();
        if (chestIconsByVariant == null || chestIconsByVariant.Length < ChestGiftController.VariantCount)
        {
            this.LogWarning("[DailyChestGiftEnter] Five original ChestGift icons must be bound.");
            return;
        }

        _chestIconsByGiftIndex[1] = chestIconsByVariant[2];
        _chestIconsByGiftIndex[2] = chestIconsByVariant[1];
        _chestIconsByGiftIndex[3] = chestIconsByVariant[3];
        _chestIconsByGiftIndex[4] = chestIconsByVariant[4];
        _chestIconsByGiftIndex[5] = chestIconsByVariant[0];
    }

    #endregion

    #region 当地自然日倒计时

    private void RefreshCountdown(bool force)
    {
        if (!force && Time.unscaledTime < _nextCountdownRefreshTime)
            return;

        _nextCountdownRefreshTime = Time.unscaledTime + 1f;
        if (countdownText == null)
            return;

        TimeSpan remaining = GiftManager.Instance.GetDailyCountdown(GiftPopupType.ChestGift);
        countdownText.text = $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    #endregion
}

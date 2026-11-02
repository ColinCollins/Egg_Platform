using System.Collections.Generic;
using System;
using Bear.UI;
using Config.Game;
using Cysharp.Threading.Tasks;
using Game.ConfigModule;
using GameCommon;
using Game.Scripts.Common;
using UnityEngine;


/// <summary>
/// version 1.0 通用的领取奖励界面
/// 传入数据，打开显示内容
/// 分为普通领取和看广告后翻倍领取
/// 默认打开界面后直接发放普通奖励；grantNormalReward 为 false 时只展示普通奖励。
/// 点击 adBtn 后，按照 multiple - 1 的倍数提供给玩家额外奖励. multiple <= 0 隐藏 adBtn
///
/// 根据 Reward GameProps 进行获取 sprite 传入 item 实例中
/// 目前只考虑 coin，star 和 skin 切换 sprite
/// </summary>
public partial class CommonGetRewardPopup : BaseUIView
{
    [SerializeField] private CommonYellowBtn normalBtn;
    [SerializeField] private CommonYellowBtn adBtn;

    // DestroyUI 关闭界面
    [SerializeField] private CustomButton closeBtn;

    [SerializeField] private RectTransform container;

    // 根据奖励创建内容
    [SerializeField] private CommonRewardItem itemPrefab;

    [SerializeField] private Sprite coinSprite;
    [SerializeField] private Sprite starSprite;

    [SerializeField] private PropBlock prop;
    [SerializeField] private CommonUIRewardAnimCtrl rewardAnimCtrl;
    /*     [Tooltip("Skin 奖励展示图标；仅用于 GameProps.Skin 的通用图标切换，不处理具体皮肤 id。")]
        [SerializeField] private Sprite skinSprite; */

    [SerializeField] public RectTransform BtnGroup;

    /// <summary>
    /// 用途：领奖关闭动画开始后拦截当前奖励弹窗的全部额外点击，直到弹窗真正关闭。
    /// 调用边界：由 ClosePopup 接受关闭请求时开启，弹窗打开、数据重置或关闭时恢复为关闭状态。
    /// 注意事项：应位于奖励弹窗普通内容之上；明信片引导 Finger 运行时动态创建在其后，并在首次点击后立即销毁。
    /// </summary>
    [SerializeField] private GameObject clickBlock;

    private int multiple = -1;
    private readonly Dictionary<GameProps, int> _rewards = new Dictionary<GameProps, int>();
    private readonly List<CommonRewardItem> _rewardItems = new List<CommonRewardItem>();
    private readonly List<Config.Skin> _skinRewards = new List<Config.Skin>();
    private CommonRewardItem _coinRewardItem;
    private int _skinIconLoadVersion;
    private bool _hasGrantedNormalReward;
    private bool _hasGrantedAdReward;
    private bool _isRewardAdRequesting;
    private bool _isClosing;
    private bool _finishCloseWhenShown;
    private bool _finishCloseScheduled;
    private int _closeAttemptVersion;
    private bool _usesPreGrantedNormalReward;
    private int _remainingHiddenCoinAfterReward;
    private Action _closeCallback;

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        SetClickBlocked(_isClosing);
        if (_finishCloseWhenShown && IsVisible)
            FinishCloseWhenShownAsync().Forget();
    }

    private void OnDisable()
    {
        if (Application.isPlaying && clickBlock != null)
            clickBlock.SetActive(false);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        SetClickBlocked(false);
        BindButtons();
        RefreshBtns();
    }

    /// <summary>
    /// 用途：关闭动画在弹窗隐藏期间结束后，恢复显示时继续完成关闭。
    /// 调用边界：只安排栈切换结束后的销毁，不在 OnShow 中重入 UIManager。
    /// </summary>
    public override void OnShow()
    {
        base.OnShow();
        if (_finishCloseWhenShown)
            FinishCloseWhenShownAsync().Forget();
    }

    private void RefreshBtns()
    {
        if (adBtn)
        {
            adBtn.gameObject.SetActive(multiple > 1 && _rewards.Count > 0);
            if (multiple > 0)
                adBtn.Content.text = string.Format(adBtn.Content.text, multiple);
        }

        RefreshButtonStates();
    }

    public override void OnCreate()
    {
        base.OnCreate();
        // TODO: 注册按钮等（示例见 PictureVerifticationPopup）
        BindButtons();
    }

    public override void OnClose()
    {
        _finishCloseWhenShown = false;
        SetClickBlocked(false);
        base.OnClose();
        UnbindButtons();
        _skinIconLoadVersion++;
        _skinRewards.Clear();
        _isRewardAdRequesting = false;
        _usesPreGrantedNormalReward = false;
        _remainingHiddenCoinAfterReward = 0;
        InvokeCloseCallback();
    }

    private void BindButtons()
    {
        UnbindButtons();

        if (normalBtn != null && normalBtn.Btn != null)
            normalBtn.Btn.OnClick += OnNormalBtnClick;

        if (adBtn != null && adBtn.Btn != null)
            adBtn.Btn.OnClick += OnAdBtnClick;

        if (closeBtn != null)
            closeBtn.OnClick += OnCloseBtnClick;
    }

    private void UnbindButtons()
    {
        if (normalBtn != null && normalBtn.Btn != null)
            normalBtn.Btn.OnClick -= OnNormalBtnClick;

        if (adBtn != null && adBtn.Btn != null)
            adBtn.Btn.OnClick -= OnAdBtnClick;

        if (closeBtn != null)
            closeBtn.OnClick -= OnCloseBtnClick;
    }

    private void SetData<TValue>(
        IEnumerable<KeyValuePair<GameProps, TValue>> rewards,
        int rewardMultiple,
        bool grantNormalReward,
        int remainingHiddenCoinAfterReward)
    {
        _rewards.Clear();
        _skinRewards.Clear();
        _coinRewardItem = null;
        _skinIconLoadVersion++;

        if (rewards != null)
        {
            foreach (var reward in rewards)
            {
                ApplyRewardParam(reward.Key, reward.Value);
            }
        }

        multiple = rewardMultiple;
        _hasGrantedNormalReward = false;
        _hasGrantedAdReward = false;
        _isRewardAdRequesting = false;
        _isClosing = false;
        _finishCloseWhenShown = false;
        _closeAttemptVersion++;
        _usesPreGrantedNormalReward = !grantNormalReward;
        _remainingHiddenCoinAfterReward = _usesPreGrantedNormalReward
            ? Mathf.Max(0, remainingHiddenCoinAfterReward)
            : 0;
        SetClickBlocked(false);

        RefreshRewardItems();
        RefreshBtns();
        if (grantNormalReward)
            GrantNormalReward();
        else
            BindPreGrantedCoinDisplay();
    }

    private void BindPreGrantedCoinDisplay()
    {
        if (prop == null
            || !TryGetPreGrantedCoinDisplayCount(includeCurrentReward: true, out int displayCoinCount))
        {
            return;
        }

        prop.BindData(displayCoinCount);
    }

    private void RefreshCoinDisplayAfterReward()
    {
        if (prop == null)
            return;

        if (!_usesPreGrantedNormalReward)
        {
            prop.UpdateData();
            return;
        }

        if (TryGetPreGrantedCoinDisplayCount(includeCurrentReward: false, out int displayCoinCount))
            prop.UpdateData(displayCoinCount);
    }

    private bool TryGetPreGrantedCoinDisplayCount(bool includeCurrentReward, out int displayCoinCount)
    {
        displayCoinCount = 0;
        var bag = GameManager.Instance?.Bag;
        if (bag == null)
            return false;

        long hiddenCoinCount = _remainingHiddenCoinAfterReward;
        if (includeCurrentReward
            && _rewards.TryGetValue(GameProps.Coin, out int currentCoinReward)
            && currentCoinReward > 0)
        {
            hiddenCoinCount += currentCoinReward;
        }

        long actualCoinCount = bag.GetToolCount(GameProps.Coin);
        long visibleCoinCount = actualCoinCount - hiddenCoinCount;
        displayCoinCount = visibleCoinCount <= 0
            ? 0
            : visibleCoinCount >= int.MaxValue
                ? int.MaxValue
                : (int)visibleCoinCount;
        return true;
    }

    private void ApplyRewardParam(GameProps prop, object rewardValue)
    {
        if (prop == GameProps.Skin)
        {
            Config.Skin skinReward = ResolveSkinReward(rewardValue);
            if (skinReward != null)
                _skinRewards.Add(skinReward);
            return;
        }

        if (!IsSupportedReward(prop) || !TryGetRewardCount(rewardValue, out int count) || count <= 0)
            return;

        if (_rewards.TryGetValue(prop, out int currentCount))
        {
            long totalCount = (long)currentCount + count;
            if (totalCount > int.MaxValue)
                return;

            count = (int)totalCount;
        }

        _rewards[prop] = count;
    }

    private void RefreshRewardItems()
    {
        ClearRewardItems();

        if (container == null || itemPrefab == null)
            return;

        foreach (var reward in _rewards)
        {
            var sprite = ResolveRewardSprite(reward.Key);
            if (sprite == null)
                continue;

            var item = Instantiate(itemPrefab, container);
            item.gameObject.SetActive(true);
            item.Refresh(sprite, reward.Value);
            if (reward.Key == GameProps.Coin)
                _coinRewardItem = item;

            _rewardItems.Add(item);
        }

        if (_skinRewards.Count == 0)
            return;

        int loadVersion = ++_skinIconLoadVersion;
        foreach (Config.Skin skinReward in _skinRewards)
        {
            var skinItem = Instantiate(itemPrefab, container);
            skinItem.gameObject.SetActive(true);
            skinItem.RefreshSkin(null, 1);
            _rewardItems.Add(skinItem);
            LoadSkinRewardIconAsync(skinReward, skinItem, loadVersion).Forget();
        }
    }

    private void ClearRewardItems()
    {
        _skinIconLoadVersion++;
        _coinRewardItem = null;

        for (int i = 0; i < _rewardItems.Count; i++)
        {
            if (_rewardItems[i] != null)
                Destroy(_rewardItems[i].gameObject);
        }

        _rewardItems.Clear();
    }

    private void GrantNormalReward()
    {
        if (_hasGrantedNormalReward)
            return;

        _hasGrantedNormalReward = true;
        GameManager.Instance?.Bag?.GrantRewards(_rewards, RewardType.Other);
        GrantSkinReward();
    }

    private void GrantAdReward()
    {
        if (_hasGrantedAdReward)
            return;

        int extraMultiple = multiple - 1;
        if (extraMultiple <= 0)
            return;

        _hasGrantedAdReward = true;
        GameManager.Instance?.Bag?.GrantRewards(_rewards, RewardType.Ad, extraMultiple);
    }

    private void RefreshButtonStates()
    {
        bool interactable = !_isRewardAdRequesting;
        interactable &= !_isClosing;

        if (normalBtn != null && normalBtn.Btn != null)
            normalBtn.Btn.Interactable = interactable;

        if (adBtn != null && adBtn.Btn != null)
            adBtn.Btn.Interactable = interactable && !_hasGrantedAdReward;

        if (closeBtn != null)
            closeBtn.Interactable = interactable;
    }

    public void OnNormalBtnClick(CustomButton btn)
    {
        ClosePopup();
    }

    private void OnCloseBtnClick(CustomButton btn)
    {
        ClosePopup();
    }

    private void OnAdBtnClick(CustomButton btn)
    {
        if (_isRewardAdRequesting || _hasGrantedAdReward || multiple <= 1)
            return;

        _isRewardAdRequesting = true;
        RefreshButtonStates();

        RewardAdHelper.TryToShowRewardAd(
            RewardPlacement.ShopCloseReward.ToString(),
            onResult: OnRewardAdCallback);
    }

    private void OnRewardAdCallback(string placement, bool isSuc)
    {
        _isRewardAdRequesting = false;

        if (this == null || !isActiveAndEnabled)
            return;

        if (!isSuc)
        {
            RefreshButtonStates();
            return;
        }

        GrantAdReward();
        ClosePopup();
    }

    private void ClosePopup()
    {
        if (_isRewardAdRequesting || _isClosing)
            return;

        _isClosing = true;
        SetClickBlocked(true);
        RefreshButtonStates();
        AudioManager.PlaySound("shopGetReward");
        ClosePopupAsync(++_closeAttemptVersion).Forget();
    }

    private void SetClickBlocked(bool isBlocked)
    {
        if (clickBlock == null)
        {
            Transform clickBlockTransform = transform.Find("ClickBlock");
            clickBlock = clickBlockTransform != null ? clickBlockTransform.gameObject : null;
        }

        bool shouldShowBlock = isActiveAndEnabled && isBlocked;
        if (clickBlock != null && clickBlock.activeSelf != shouldShowBlock)
            clickBlock.SetActive(shouldShowBlock);
    }

    private void SetCloseCallback(Action callback)
    {
        _closeCallback = callback;
    }

    private void InvokeCloseCallback()
    {
        var callback = _closeCallback;
        _closeCallback = null;
        callback?.Invoke();
    }

    private async UniTaskVoid ClosePopupAsync(int closeAttemptVersion)
    {
        try
        {
            await PlayCoinRewardAnimIfNeededAsync();
        }
        finally
        {
            if (this != null && IsOpen && closeAttemptVersion == _closeAttemptVersion &&
                IsVisible && isActiveAndEnabled)
            {
                UIManager.Instance?.DestroyUI(this);
            }
            else if (this != null && IsOpen && closeAttemptVersion == _closeAttemptVersion)
            {
                _finishCloseWhenShown = true;
            }
        }
    }

    private async UniTaskVoid FinishCloseWhenShownAsync()
    {
        if (_finishCloseScheduled)
            return;

        _finishCloseScheduled = true;
        await UniTask.NextFrame();
        _finishCloseScheduled = false;

        if (this == null || !IsOpen || !IsVisible || !isActiveAndEnabled || !_finishCloseWhenShown)
            return;

        _finishCloseWhenShown = false;
        UIManager.Instance?.DestroyUI(this);
    }

    private async UniTask PlayCoinRewardAnimIfNeededAsync()
    {
        if (!_rewards.TryGetValue(GameProps.Coin, out int coinCount) || coinCount <= 0)
            return;

        var destroyToken = destroyCancellationToken;

        try
        {
            if (rewardAnimCtrl != null)
                await rewardAnimCtrl.PlayFrom(coinSprite, GetCoinRewardAnimStartPoint());
        }
        finally
        {
            if (this != null && isActiveAndEnabled && prop != null)
            {
                RefreshCoinDisplayAfterReward();
                await UniTask.WaitForSeconds(1f, ignoreTimeScale: true, cancellationToken: destroyToken)
                    .SuppressCancellationThrow();
            }
        }
    }

    private Transform GetCoinRewardAnimStartPoint()
    {
        return _coinRewardItem != null
            ? _coinRewardItem.transform
            : transform;
    }

    private bool IsSupportedReward(GameProps prop)
    {
        return prop == GameProps.Coin
            || prop == GameProps.Star;
    }

    private Sprite ResolveRewardSprite(GameProps prop)
    {
        return prop switch
        {
            GameProps.Coin => coinSprite,
            GameProps.Star => starSprite,
            // GameProps.Skin => skinSprite,
            _ => null,
        };
    }

    private static Config.Skin ResolveSkin(string skinId)
    {
        if (string.IsNullOrEmpty(skinId))
            return null;

        return ConfigManager.Instance?.Tables?.TbSkin?.GetOrDefault(skinId);
    }

    private static Config.Skin ResolveSkinReward(object rewardValue)
    {
        switch (rewardValue)
        {
            case Config.Skin skin:
                return skin;
            default:
                return BaseParamUtils.TryGetString(rewardValue, out var skinId)
                    ? ResolveSkin(skinId)
                    : null;
        }
    }

    private static bool TryGetRewardCount(object rewardValue, out int count)
    {
        return BaseParamUtils.TryGetInt(rewardValue, out count);
    }

    private async UniTaskVoid LoadSkinRewardIconAsync(Config.Skin skin, CommonRewardItem item, int loadVersion)
    {
        if (skin == null || item == null || string.IsNullOrEmpty(skin.TextureResources))
            return;

        string location = SkinUtils.ResolveIconLocation(skin);
        if (string.IsNullOrEmpty(location))
            return;

        try
        {
            var sprite = await SkinUtils.LoadIconAsync(location);
            if (this == null ||
                loadVersion != _skinIconLoadVersion ||
                !_skinRewards.Contains(skin) ||
                !_rewardItems.Contains(item) ||
                item == null)
            {
                return;
            }

            item.RefreshSkin(sprite, 1);
        }
        catch (Exception)
        {
        }
    }

    private void GrantSkinReward()
    {
        var bag = GameManager.Instance?.Bag;
        if (bag == null)
            return;

        foreach (Config.Skin skinReward in _skinRewards)
        {
            if (skinReward == null || string.IsNullOrEmpty(skinReward.Id))
                continue;

            bag.GetNewSkin(skinReward.Id, GetSkinWayType.Reward);
        }
    }

    /// <summary>
    /// 传入奖励以及倍数
    /// 用途：打开通用奖励弹窗，并按需直接发放普通奖励，在弹窗关闭时可选通知调用方。
    /// 调用边界：供运行时奖励领取或纯展示入口使用，callback 只记录本次弹窗实例。
    /// 注意事项：grantNormalReward 为 false 时，调用方必须在外部负责普通奖励入包；广告额外奖励仍由弹窗处理。
    /// </summary>
    /// <param name="rewards">奖励字典，key 为奖励类型，value 为可解析的奖励参数。</param>
    /// <param name="multiple"></param>
    /// <param name="callback">弹窗关闭回调；普通关闭、关闭按钮或广告成功关闭最终进入 OnClose 时调用。</param>
    /// <param name="grantNormalReward">是否在弹窗初始化时直接发放普通奖励；外部已发放时传 false，避免重复领取。</param>
    /// <param name="remainingHiddenCoinAfterReward">外部还未展示、且在本次奖励动画后仍需隐藏的 Coin 数量；仅 grantNormalReward 为 false 时生效。</param>
    /// <returns></returns>
    public static CommonGetRewardPopup Create(
        Dictionary<GameProps, object> rewards,
        int multiple = 1,
        Action callback = null,
        bool grantNormalReward = true,
        int remainingHiddenCoinAfterReward = 0)
    {
        var panel = OpenPopup();
        if (panel != null)
        {
            panel.SetCloseCallback(callback);
            panel.SetData(rewards, multiple, grantNormalReward, remainingHiddenCoinAfterReward);
        }

        return panel;
    }

    /// <summary>
    /// 用途：直接接收 Luban ParamBase 奖励配置，内部解析 IntParam 数量奖励和 StringParam 皮肤奖励。
    /// 调用边界：供配置驱动的奖励领取或纯展示入口使用，不修改传入配置对象。
    /// 注意事项：grantNormalReward 为 false 时不发放普通数量奖励和 Skin；广告额外奖励仍由弹窗处理。
    /// </summary>
    /// <param name="rewards">Luban 生成的奖励配置，key 为奖励类型，value 为参数包装。</param>
    /// <param name="multiple">广告额外领取倍率，只有 Coin / Star 数量奖励会参与。</param>
    /// <param name="callback">弹窗关闭回调；普通关闭、关闭按钮或广告成功关闭最终进入 OnClose 时调用一次。</param>
    /// <param name="grantNormalReward">是否在弹窗初始化时直接发放普通奖励；外部已发放时传 false，避免重复领取。</param>
    /// <param name="remainingHiddenCoinAfterReward">外部还未展示、且在本次奖励动画后仍需隐藏的 Coin 数量；仅 grantNormalReward 为 false 时生效。</param>
    /// <returns>打开成功时返回弹窗实例，否则返回 null。</returns>
    public static CommonGetRewardPopup Create(
        Dictionary<GameProps, Config.ParamBase> rewards,
        int multiple = 1,
        Action callback = null,
        bool grantNormalReward = true,
        int remainingHiddenCoinAfterReward = 0)
    {
        var panel = OpenPopup();
        if (panel != null)
        {
            panel.SetCloseCallback(callback);
            panel.SetData(rewards, multiple, grantNormalReward, remainingHiddenCoinAfterReward);
        }

        return panel;
    }

    /// <summary>
    /// 用途：直接接收 Shop 的有序奖励列表并打开通用奖励弹窗，保留重复 Skin 奖励项。
    /// 调用边界：供 Shop 奖励领取或外部已完成发奖后的纯展示流程使用；不会修改传入列表。
    /// 注意事项：grantNormalReward 为 false 时调用方必须已完成普通奖励入包；Coin 关闭动画仍只处理 Coin，Skin 仅展示。
    /// </summary>
    /// <param name="rewards">Shop 有序奖励列表，重复 Coin/Star 会合计，重复 Skin 会逐项展示。</param>
    /// <param name="multiple">广告额外领取倍率；只有 Coin / Star 数量奖励参与。</param>
    /// <param name="callback">弹窗关闭回调；最终进入 OnClose 时调用一次。</param>
    /// <param name="grantNormalReward">是否在弹窗初始化时直接发放普通奖励。</param>
    /// <param name="remainingHiddenCoinAfterReward">外部还未展示、且在本次奖励动画后仍需隐藏的 Coin 数量。</param>
    /// <returns>打开成功时返回弹窗实例，否则返回 null。</returns>
    public static CommonGetRewardPopup Create(
        List<Config.ShopReward> rewards,
        int multiple = 1,
        Action callback = null,
        bool grantNormalReward = true,
        int remainingHiddenCoinAfterReward = 0)
    {
        var panel = OpenPopup();
        if (panel != null)
        {
            var orderedRewards = new List<KeyValuePair<GameProps, Config.ParamBase>>(rewards?.Count ?? 0);
            if (rewards != null)
            {
                foreach (Config.ShopReward reward in rewards)
                {
                    if (reward != null)
                        orderedRewards.Add(new KeyValuePair<GameProps, Config.ParamBase>(reward.Key, reward.Value));
                }
            }

            panel.SetCloseCallback(callback);
            panel.SetData(
                orderedRewards,
                multiple,
                grantNormalReward,
                remainingHiddenCoinAfterReward);
        }

        return panel;
    }

    /// <summary>
    /// 用途：从明信片配置直接打开通用奖励弹窗，读取配置内 Rewards 和 RewardMultiple。
    /// 调用边界：供明信片配置驱动的奖励领取或纯展示入口使用；配置为空时不打开弹窗。
    /// 注意事项：grantNormalReward 为 false 时不发放普通奖励，配置与存档仍不会被该入口修改。
    /// </summary>
    /// <param name="postcardConfig">Luban 生成的明信片配置。</param>
    /// <param name="callback">弹窗关闭回调；配置为空未打开弹窗时不会调用。</param>
    /// <param name="grantNormalReward">是否在弹窗初始化时直接发放普通奖励；外部已发放时传 false，避免重复领取。</param>
    /// <param name="remainingHiddenCoinAfterReward">外部还未展示、且在本次奖励动画后仍需隐藏的 Coin 数量；仅 grantNormalReward 为 false 时生效。</param>
    /// <returns>打开成功时返回弹窗实例，否则返回 null。</returns>
    public static CommonGetRewardPopup Create(
        Config.PostcardConfig postcardConfig,
        Action callback = null,
        bool grantNormalReward = true,
        int remainingHiddenCoinAfterReward = 0)
    {
        return postcardConfig == null
            ? null
            : Create(
                postcardConfig.Rewards,
                postcardConfig.RewardMultiple,
                callback,
                grantNormalReward,
                remainingHiddenCoinAfterReward);
    }

    private static CommonGetRewardPopup OpenPopup()
    {
        return UIManager.Instance?.OpenUI<CommonGetRewardPopup>($"{typeof(CommonGetRewardPopup).Name}", UILayer.Top);
    }

}
